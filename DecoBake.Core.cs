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
            var result = new Table { Decorations = decorations, Events = events.Length, Pitch = pitch, TweenScale = tweenScale,
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
