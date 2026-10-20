#if DEV || AUTOTEST || DECOBAKE_FIXTURE
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace StutterFix
{
    // Pure snapshot -> immutable tracks. No Unity objects, setters or frame accumulator.
    internal static class DecoBakeCore
    {
        internal const int Channels = 16;
        internal const int ScaleX = 5, ScaleY = 6;
        // Stateful clock adapter only; Table.Eval remains a stateless function.
        internal sealed class SongClock
        {
            internal double Time { get; private set; } = double.NaN;
            private double rawLast;
            private bool playingLast;
            internal int Resyncs;
            internal void Reset() { Time = double.NaN; }
            internal double Step(double raw, double tweenDelta, double pitch, double tweenScale, bool playing)
            {
                if (!Finite(raw)) { Reset(); return double.NaN; }
                double delta = tweenScale > 0 ? tweenDelta * pitch / tweenScale : 0;
                double predicted = Time + Math.Max(0, delta);
                if (!Finite(Time) || playing != playingLast || !playing || !Finite(delta)
                    || Math.Abs(raw - predicted) > .05 || raw - rawLast < -.05)
                { Time = raw; Resyncs++; }
                else Time = Math.Max(Time, predicted + .01 * (raw - predicted));
                rawLast = raw; playingLast = playing;
                return Time;
            }
        }
        internal sealed class SnapshotChangedException : InvalidOperationException { }
        internal static bool SameInstances<T>(IList<T> current, T[] saved, int[] ids, Func<T, int> id) where T : class
        {
            if (saved == null || ids == null || current.Count != saved.Length || ids.Length != saved.Length) return false;
            for (int i = 0; i < saved.Length; i++)
                if (current[i] == null || !ReferenceEquals(current[i], saved[i]) || id(current[i]) != ids[i]) return false;
            return true;
        }
        internal static Decoration[] CloneDecorations(Decoration[] source)
        {
            var copy = new Decoration[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var d = source[i];
                var c = new Decoration { Id = d.Id, Initial = (float[])d.Initial.Clone(), OriginalX = d.OriginalX, OriginalY = d.OriginalY,
                    GlobalX = d.GlobalX, GlobalY = d.GlobalY, ScaleUnitsX = d.ScaleUnitsX, ScaleUnitsY = d.ScaleUnitsY };
                foreach (string reason in d.Reasons) c.Reasons.Add(reason);
                copy[i] = c;
            }
            return copy;
        }
        // A coroutine may resume after seek mutated a list in place. Check BEFORE indexing.
        internal static T SnapshotItem<T>(IList<T> list, int index, int count, int epoch, int currentEpoch)
        {
            if (epoch != currentEpoch || list.Count != count || (uint)index >= (uint)list.Count)
                throw new SnapshotChangedException();
            return list[index];
        }
        // Symmetric derivative within one continuous segment; steps/endpoints/flat spans are NA.
        internal static double Speed(Table table, int d, int channel, double time)
        {
            const double h = .0001;
            var track = table.Tracks[d * Channels + channel];
            int i = Find(track, time);
            if (i < 0 || Find(track, time - h) != i || Find(track, time + h) != i) return double.NaN;
            var s = track[i];
            double end = s.T0 + s.Duration * table.Pitch / table.TweenScale;
            if (s.Duration <= 0 || time - h <= s.T0 || time + h >= end) return double.NaN;
            double speed = Math.Abs((double)Value(s, time + h, table.Pitch, table.TweenScale, table.Ease)
                - Value(s, time - h, table.Pitch, table.TweenScale, table.Ease)) / (2 * h);
            return speed > .000001 ? speed : double.NaN;
        }
        // scrVfxPlus.Update compares this directly with songposition_minusi.
        internal static double ScheduledTime(double startTime, double startEffectOffset) => startTime - startEffectOffset;
        internal static double FitOffset(Func<double, double> loss)
        {
            double offset = 0, best = loss(0), step = .125;
            for (int i = -256; i <= 256; i++) { double candidate = i * step, error = loss(candidate);
                if (error < best) { best = error; offset = candidate; } }
            for (int pass = 0; pass < 3; pass++) { double origin = offset; step /= 10;
                for (int i = -10; i <= 10; i++) { double candidate = Math.Max(-32, Math.Min(32, origin + i * step)), error = loss(candidate);
                    if (error < best) { best = error; offset = candidate; } } }
            return offset;
        }
        internal delegate float EaseFn(int id, float t, float duration, float over, float period);
        internal sealed class Decoration
        {
            internal int Id;
            internal float[] Initial = new float[Channels];
            internal float OriginalX, OriginalY, GlobalX, GlobalY;
            // Sprite bounds * constant scale multipliers. Nominal axis extent, not final rendered pixels.
            internal float ScaleUnitsX = float.NaN, ScaleUnitsY = float.NaN;
            internal readonly HashSet<string> Reasons = new HashSet<string>();
        }
        internal sealed class Event
        {
            internal double T0;
            internal double StartTime, StartEffectOffset;
            internal float AngleOffset;
            internal float Duration, Over, Period;
            internal int Ease, Source;
            internal bool RelativePosition, ResetGlobalOrigin;
            internal int[] Targets;
            // NaN denotes an unused channel, not a value to interpolate.
            internal float[] Values = EmptyValues();
            internal string[] Reasons = Array.Empty<string>();
        }
        internal struct Segment
        {
            internal double T0;
            internal float Duration, Start, End, Target, Over, Period;
            internal int Ease, Source;
            internal bool Replacement;
            internal bool IsScale;
        }
        internal sealed class Table
        {
            internal Event[] InputEvents; // Diagnostic snapshot only; excluded from track payload.
            internal Decoration[] Decorations;
            internal Segment[][] Tracks;
            internal bool[] Replaced;
            internal int Segments, Events, Supported;
            internal double CompileMs;
            internal long PayloadBytes;
            internal float Pitch, TweenScale;
            internal EaseFn Ease;
            internal float Eval(int d, int channel, double songTime)
            {
                var track = Tracks[d * Channels + channel];
                int index = Find(track, songTime);
                return index < 0 ? Decorations[d].Initial[channel] : Value(track[index], songTime, Pitch, TweenScale, Ease);
            }
        }
        // Benchmark-only scheduler: one cursor per channel, dispatches starts in song-time order.
        // Construction and warm seek are outside measured steady frames. No Unity writes.
        internal sealed class ActiveEvaluator
        {
            private struct Start { internal int Slot, Index; internal double Time; }
            private readonly Table table;
            private readonly Start[] starts;
            private readonly int[] cursor, active, position;
            private int next, count;
            private double last = double.NegativeInfinity;
            internal int Evaluated, Dispatched;
            internal ActiveEvaluator(Table table)
            {
                this.table = table;
                cursor = new int[table.Tracks.Length]; active = new int[cursor.Length]; position = new int[cursor.Length];
                var items = new List<Start>();
                for (int slot = 0; slot < cursor.Length; slot++)
                    for (int i = 0; i < table.Tracks[slot].Length; i++)
                        items.Add(new Start { Slot = slot, Index = i, Time = table.Tracks[slot][i].T0 });
                items.Sort((a, b) => { int c = a.Time.CompareTo(b.Time); if (c != 0) return c;
                    c = a.Slot.CompareTo(b.Slot); return c != 0 ? c : a.Index.CompareTo(b.Index); });
                starts = items.ToArray(); Reset();
            }
            private void Reset() { next = count = 0; last = double.NegativeInfinity;
                for (int i = 0; i < cursor.Length; i++) { cursor[i] = -1; position[i] = -1; } }
            internal double Step(double time)
            {
                if (time < last) Reset();
                last = time; Evaluated = Dispatched = 0;
                while (next < starts.Length && starts[next].Time <= time)
                {
                    var s = starts[next++]; cursor[s.Slot] = s.Index; Dispatched++;
                    if (position[s.Slot] < 0) { position[s.Slot] = count; active[count++] = s.Slot; }
                }
                double sum = 0;
                for (int i = 0; i < count;)
                {
                    int slot = active[i]; var s = table.Tracks[slot][cursor[slot]];
                    sum += Value(s, time, table.Pitch, table.TweenScale, table.Ease); Evaluated++;
                    if ((time - s.T0) / table.Pitch * table.TweenScale >= s.Duration)
                    { position[slot] = -1; int moved = active[--count]; active[i] = moved; if (i < count) position[moved] = i; }
                    else i++;
                }
                return sum;
            }
        }
        internal static float[] EmptyValues()
        {
            var v = new float[Channels]; for (int i = 0; i < v.Length; i++) v[i] = float.NaN; return v;
        }
        internal static int Find(Segment[] track, double time)
        {
            int lo = 0, hi = track.Length;
            while (lo < hi) { int mid = lo + (hi - lo) / 2; if (track[mid].T0 <= time) lo = mid + 1; else hi = mid; }
            return lo - 1; // Last event at an identical T0 wins, in PrepVfx enumeration order.
        }
        internal static float Value(Segment s, double time, float pitch, float scale, EaseFn ease)
        {
            if (s.Duration == 0) return s.End;
            float t = (float)((time - s.T0) / pitch * scale);
            if (t >= s.Duration) return s.End;
            if (t <= 0) return s.Start;
            return s.Start + ((s.IsScale ? s.Target : s.End) - s.Start) * ease(s.Ease, t, s.Duration, s.Over, s.Period);
        }
        internal static void CopyScale(Event e, bool used, float scalar, float x, float y)
        {
            if (!used) return;
            // Legacy scalar is already decoded; modern Vector2 was divided by 100 in Decode.
            e.Values[ScaleX] = float.IsNaN(scalar) ? x : scalar;
            e.Values[ScaleY] = float.IsNaN(scalar) ? y : scalar;
            // relativeTo only affects position. There is no relative scale branch or SetRelative call.
        }
        internal static bool Finite(double x) { return !double.IsNaN(x) && !double.IsInfinity(x); }
        internal static string PlacementReason(string placement, bool stickToFloor)
        {
            if (placement == "Camera" || placement == "CameraAspect") return "Camera-동적 화면 투영";
            if (placement == "Tile" && stickToFloor) return "Tile-동적 바닥 추종";
            return placement == "Global" || placement == "Tile" ? null : "초기 배치 미지원";
        }
        internal static string MovementReason(string placement, bool used)
        {
            if (!used || placement == "Global" || placement == "LastPosition") return null;
            if (placement == "Tile") return "Tile-동적 효과 시점 재배치";
            if (placement == "Camera" || placement == "CameraAspect") return "Camera-동적 배치 변경";
            return "Global 이외 배치 변경";
        }
        internal static Table Compile(Decoration[] decorations, Event[] events, float pitch, float tweenScale, EaseFn ease, CancellationToken cancel)
        {
            if (!Finite(pitch) || pitch <= 0 || !Finite(tweenScale) || tweenScale <= 0 || ease == null) throw new ArgumentException("clock/ease");
            var sw = Stopwatch.StartNew();
            var lists = new List<Segment>[decorations.Length * Channels];
            var globalOrigin = new bool[decorations.Length];
            var result = new Table { InputEvents = events, Decorations = decorations, Events = events.Length, Pitch = pitch, TweenScale = tweenScale,
                Ease = ease, Replaced = new bool[decorations.Length], Tracks = new Segment[lists.Length][] };
            // Propagate every exclusion before compiling anything: one unsupported branch excludes the entire decoration.
            double previous = double.NegativeInfinity;
            foreach (var e in events)
            {
                cancel.ThrowIfCancellationRequested();
                if (!Finite(e.T0) || e.T0 < previous) throw new ArgumentException("unsorted/invalid event time");
                previous = e.T0;
                foreach (int d in e.Targets) {
                    cancel.ThrowIfCancellationRequested();
                    foreach (string reason in e.Reasons) decorations[d].Reasons.Add(reason);
                    if (!Finite(e.Duration) || e.Duration < 0) decorations[d].Reasons.Add("길이 비유한/음수");
                }
            }
            foreach (var e in events)
            {
                cancel.ThrowIfCancellationRequested();
                foreach (int d in e.Targets)
                {
                    cancel.ThrowIfCancellationRequested();
                    var dec = decorations[d]; if (dec.Reasons.Count != 0) continue;
                    // SetPlacementType precedes forceDontTweenMovement/positionUsed checks.
                    // Once explicit Global resets startPos, omitted relativeTo retains that origin.
                    if (e.ResetGlobalOrigin) globalOrigin[d] = true;
                    for (int c = 0; c < Channels; c++)
                    {
                        float target = e.Values[c]; if (float.IsNaN(target)) continue;
                        if (!Finite(target)) throw new ArgumentException("nonfinite target");
                        int slot = d * Channels + c;
                        var list = lists[slot] ?? (lists[slot] = new List<Segment>());
                        float current = list.Count == 0 ? dec.Initial[c] : Value(list[list.Count - 1], e.T0, pitch, tweenScale, ease);
                        // Relative target is captured BEFORE Kill(true); start is read AFTER completion.
                        if (c < 2) target += e.RelativePosition ? current : globalOrigin[d]
                            ? (c == 0 ? dec.GlobalX : dec.GlobalY) : (c == 0 ? dec.OriginalX : dec.OriginalY);
                        bool replacement = list.Count != 0 && list[list.Count - 1].Duration > 0
                            && (e.T0 - list[list.Count - 1].T0) / pitch * tweenScale < list[list.Count - 1].Duration;
                        float start = list.Count == 0 ? current : list[list.Count - 1].End;
                        bool isScale = c == ScaleX || c == ScaleY;
                        // No OnComplete setter for scale: retain plugin arithmetic even at the endpoint.
                        // ZeroTween's zero-duration proxy evaluates ease at time=duration=1.
                        float end = isScale ? start + (target - start) * ease(e.Ease,
                            e.Duration == 0 ? 1 : e.Duration, e.Duration == 0 ? 1 : e.Duration, e.Over, e.Period) : target;
                        if (isScale && (!Finite(target - start) || !Finite(end)))
                        { dec.Reasons.Add("크기 산술 비유한"); break; }
                        list.Add(new Segment { T0 = e.T0, Duration = e.Duration, Start = start, End = end, Target = target, IsScale = isScale,
                            Ease = e.Ease, Over = e.Over, Period = e.Period, Source = e.Source, Replacement = replacement });
                        result.Replaced[d] |= replacement; result.Segments++;
                    }
                }
            }
            // A computed scale failure also excludes this decoration's earlier/other channels.
            for (int d = 0; d < decorations.Length; d++)
                if (decorations[d].Reasons.Count != 0)
                    for (int c = 0; c < Channels; c++)
                    {
                        int slot = d * Channels + c;
                        result.Segments -= lists[slot]?.Count ?? 0; lists[slot] = null;
                    }
            for (int i = 0; i < lists.Length; i++) { cancel.ThrowIfCancellationRequested(); result.Tracks[i] = lists[i]?.ToArray() ?? Array.Empty<Segment>(); }
            foreach (var d in decorations) if (d.Reasons.Count == 0) result.Supported++;
            // Managed array/object headers, temporary lists, input snapshots and GC are measured separately by caller.
            result.PayloadBytes = result.Segments * 48L + lists.Length * (long)IntPtr.Size + decorations.Length * (Channels * 4L + 32);
            result.CompileMs = sw.Elapsed.TotalMilliseconds;
            return result;
        }
    }
}
#endif
