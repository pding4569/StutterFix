#if DEV || AUTOTEST || DECOBAKE_FIXTURE
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace StutterFix
{
    // Pure tile snapshot compiler; shares segment search/time/easing with decoration baking.
    internal static class FloorBakeCore
    {
        internal const int Channels = 6;
        internal sealed class Tile
        {
            internal int Id;
            internal float[] Initial = new float[Channels];
            internal float X, Y, Rotation;
            internal readonly HashSet<string> Reasons = new HashSet<string>();
        }
        internal sealed class Event
        {
            internal double T0;
            internal float Duration, Over, Period, ZeroK = 1;
            internal int Ease, Source;
            internal int[] Targets = Array.Empty<int>();
            internal float[] Values = new[] { float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN };
            internal string[] Reasons = Array.Empty<string>();
        }
        internal sealed class Table
        {
            internal Tile[] Tiles;
            internal DecoBakeCore.Segment[][] Tracks;
            internal int Events, Segments, Supported;
            internal float Pitch, Scale;
            internal DecoBakeCore.EaseFn Ease;
            internal double CompileMs;
            internal long PayloadBytes;
            internal float Eval(int tile, int channel, double songTime)
            {
                var track = Tracks[tile * Channels + channel];
                int i = DecoBakeCore.Find(track, songTime);
                if (i < 0) return Tiles[tile].Initial[channel];
                var segment = track[i];
                // FloorAnim.Apply(duration) retains float arithmetic/ease endpoint, rather than assigning the raw target.
                if (segment.Duration > 0 && (songTime - segment.T0) / Pitch * Scale >= segment.Duration)
                    return segment.Start + (segment.End - segment.Start) * Ease(segment.Ease, segment.Duration, segment.Duration, segment.Over, segment.Period);
                return DecoBakeCore.Value(segment, songTime, Pitch, Scale, Ease);
            }
        }
        // Mathf.Approximately's actual tolerance, without a Unity dependency.
        internal static bool Approximately(float a, float b) => Math.Abs(b - a) < Math.Max(0.000001f * Math.Max(Math.Abs(a), Math.Abs(b)), float.Epsilon * 8);
        internal static Table Compile(Tile[] tiles, Event[] events, float pitch, float scale, DecoBakeCore.EaseFn ease, CancellationToken cancel)
        {
            if (!DecoBakeCore.Finite(pitch) || pitch <= 0 || !DecoBakeCore.Finite(scale) || scale <= 0 || ease == null) throw new ArgumentException("clock/ease");
            var sw = Stopwatch.StartNew();
            double previous = double.NegativeInfinity;
            foreach (var tile in tiles) {
                foreach (float v in tile.Initial) if (!DecoBakeCore.Finite(v)) tile.Reasons.Add("initial-nonfinite");
                if (!DecoBakeCore.Finite(tile.X) || !DecoBakeCore.Finite(tile.Y) || !DecoBakeCore.Finite(tile.Rotation)) tile.Reasons.Add("origin-nonfinite");
            }
            foreach (var e in events)
            {
                cancel.ThrowIfCancellationRequested();
                if (!DecoBakeCore.Finite(e.T0) || e.T0 < previous) throw new ArgumentException("unsorted/invalid-time");
                previous = e.T0;
                foreach (int d in e.Targets)
                {
                    if (d < 0 || d >= tiles.Length) throw new ArgumentException("target-index");
                    var tile = tiles[d];
                    foreach (string reason in e.Reasons) tile.Reasons.Add(reason);
                    if (!DecoBakeCore.Finite(e.Duration) || e.Duration < 0) tile.Reasons.Add("duration-invalid");
                    foreach (float v in e.Values) if (float.IsInfinity(v)) tile.Reasons.Add("target-nonfinite");
                    // Euler getter's wrap/Approximately comparison requires a separate rotation model.
                    if (!float.IsNaN(e.Values[2]) && (tile.Rotation + e.Values[2] < 0 || tile.Rotation + e.Values[2] >= 360)) tile.Reasons.Add("rotation-wrap");
                }
            }
            var lists = new List<DecoBakeCore.Segment>[tiles.Length * Channels];
            var table = new Table { Tiles = tiles, Tracks = new DecoBakeCore.Segment[lists.Length][], Events = events.Length, Pitch = pitch, Scale = scale, Ease = ease };
            foreach (var e in events) foreach (int d in e.Targets)
            {
                cancel.ThrowIfCancellationRequested();
                if (tiles[d].Reasons.Count != 0) continue;
                // StartEffect caches XY before either Kill. Only Y's ApproximatelyXY
                // test can see a changed OTHER axis. Deferred startup writes its own axis.
                float cachedX = tiles[d].Initial[3], currentX = cachedX;
                bool completedX = false;
                var xlist = lists[d * Channels + 3];
                if (xlist != null && xlist.Count != 0) {
                    var old = xlist[xlist.Count - 1];
                    cachedX = (e.T0 - old.T0) / pitch * scale >= old.Duration
                        ? Endpoint(old, ease) : DecoBakeCore.Value(old, e.T0, pitch, scale, ease);
                    currentX = cachedX;
                    // Even a nominally ended segment can still be waiting for its
                    // final frame. Don't infer the last displayed X from song time.
                    completedX = !float.IsNaN(e.Values[3]) && old.Duration > 0;
                    if (!float.IsNaN(e.Values[3])) currentX = Endpoint(old, ease);
                }
                for (int c = 0; c < Channels; c++)
                {
                    float target = e.Values[c]; if (float.IsNaN(target)) continue;
                    if (c == 0) target += tiles[d].X;
                    if (c == 1) target += tiles[d].Y;
                    if (c == 2) target += tiles[d].Rotation;
                    if (!DecoBakeCore.Finite(target)) throw new ArgumentException("target-overflow");
                    int slot = d * Channels + c;
                    var list = lists[slot] ?? (lists[slot] = new List<DecoBakeCore.Segment>());
                    // FloorAnim has no LastPosition mode: positional offsets use immutable startPos.
                    float start = tiles[d].Initial[c];
                    if (list.Count != 0) {
                        var old = list[list.Count - 1];
                        start = Endpoint(old, ease);
                    }
                    bool replace = list.Count != 0 && list[list.Count - 1].Duration > 0 && (e.T0 - list[list.Count - 1].T0) / pitch * scale < list[list.Count - 1].Duration;
                    bool skip = Approximately(start, target);
                    if (c == 4 && skip && start != target && !float.IsNaN(e.Values[3])) {
                        // With a running X, cachedX is the last actual frame value, not
                        // necessarily Value(T0). A forced near-Y tween changes its output.
                        if (completedX) { tiles[d].Reasons.Add("scale-cross-axis-frame-dependent-skip"); break; }
                        skip = Approximately(currentX, cachedX);
                    }
                    // Opacity duration-zero writes its target directly; transform channels use EaseEnd.
                    float end = skip ? start : e.Duration == 0 && c != 5 ? start + (target - start) * e.ZeroK : target;
                    if ((c == 3 || c == 4) && (!DecoBakeCore.Finite(target - start) || !DecoBakeCore.Finite(end))) {
                        tiles[d].Reasons.Add("scale-arithmetic-nonfinite"); break;
                    }
                    list.Add(new DecoBakeCore.Segment { T0 = e.T0, Duration = skip ? 0 : e.Duration, Start = start, End = end,
                        Ease = e.Ease, Over = e.Over, Period = e.Period, Source = e.Source, Replacement = replace });
                    table.Segments++;
                    if (c == 3) currentX = skip ? start : e.Duration == 0 ? end : start;
                    if (table.Segments > 4000000) throw new InvalidOperationException("segment-limit");
                }
            }
            // A semantic exclusion discovered late invalidates ALL earlier channels too.
            table.Segments = 0;
            for (int i = 0; i < lists.Length; i++) {
                table.Tracks[i] = tiles[i / Channels].Reasons.Count == 0 ? lists[i]?.ToArray() ?? Array.Empty<DecoBakeCore.Segment>() : Array.Empty<DecoBakeCore.Segment>();
                table.Segments += table.Tracks[i].Length;
            }
            foreach (var tile in tiles) if (tile.Reasons.Count == 0) table.Supported++;
            table.PayloadBytes = table.Segments * 40L + lists.Length * (long)IntPtr.Size + tiles.Length * 40L;
            table.CompileMs = sw.Elapsed.TotalMilliseconds;
            return table;
        }
        private static float Endpoint(DecoBakeCore.Segment s, DecoBakeCore.EaseFn ease)
            => s.Duration == 0 ? s.End : s.Start + (s.End - s.Start) * ease(s.Ease, s.Duration, s.Duration, s.Over, s.Period);
        internal static Table Synthetic(int count, DecoBakeCore.EaseFn ease)
        {
            var tiles = new Tile[count];
            for (int i = 0; i < count; i++) tiles[i] = new Tile { Id = i, Initial = new[] { 0f, 0f, 0f, 1f, 1f, 1f } };
            var events = new Event[4097];
            for (int i = 0; i < events.Length; i++) {
                var e = new Event { T0 = i / 8.0, Duration = 2, Ease = 1, Source = i + 1, Over = 1.70158f };
                e.Values[0] = i == 0 ? 10 : i % 100;
                e.Targets = new int[i == 0 ? count : 32];
                for (int j = 0; j < e.Targets.Length; j++) e.Targets[j] = i == 0 ? j : (i * 31 + j) % count;
                events[i] = e;
            }
            return Compile(tiles, events, 1, 1, ease, CancellationToken.None);
        }
    }
}
#endif
