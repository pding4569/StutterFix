#if DEV || AUTOTEST || DECOBAKE_FIXTURE
using System;
using System.Collections.Generic;

namespace StutterFix
{
    // Pure live scheduler, linked unchanged into the offline fixture.
    internal sealed class BakeLive
    {
        private sealed class Source
        {
            internal float Pos;
            internal int Frame = -1;
            internal bool Started;
            internal bool Relative;
            internal float[] Values;
            internal readonly List<int> Slots = new List<int>();
            internal readonly List<DecoBakeCore.Segment> Segments = new List<DecoBakeCore.Segment>();
        }
        private readonly DecoBakeCore.Table table;
        private readonly Dictionary<int, Source> sources = new Dictionary<int, Source>();
        private readonly DecoBakeCore.Segment[] current;
        private readonly Source[] clocks;
        private readonly int[] active, positions;
        private readonly int[] decorationFrame;
        private int statisticsFrame;
        private int count;
        internal readonly bool[] Owned;
        internal readonly float[] Last;
        internal bool Enabled = true;
        internal int Evaluated, Anchored, Fallback, Ended, Constant;
        internal int[] ByChannel = new int[16];
        internal int LongChannels;
        internal int DecorationsEvaluated, SourcesEvaluated, Replacements;
        internal bool AnchoredSlot(int slot) => clocks[slot] != null;
        internal BakeLive(DecoBakeCore.Table table)
        {
            this.table = table;
            Last = new float[table.Tracks.Length]; current = new DecoBakeCore.Segment[Last.Length];
            clocks = new Source[Last.Length]; active = new int[Last.Length]; positions = new int[Last.Length];
            Owned = new bool[table.Decorations.Length];
            decorationFrame = new int[Owned.Length];
            for (int d = 0; d < Owned.Length; d++) Owned[d] = table.Decorations[d].Reasons.Count == 0;
            for (int slot = 0; slot < Last.Length; slot++)
            {
                Last[slot] = table.Decorations[slot/16].Initial[slot%16];
                positions[slot] = -1;
                foreach (var s in table.Tracks[slot])
                {
                    if (!sources.TryGetValue(s.Source, out var source)) sources.Add(s.Source, source = new Source());
                    source.Slots.Add(slot); source.Segments.Add(s);
                }
            }
            foreach (var e in table.InputEvents)
                if (sources.TryGetValue(e.Source,out var source)) { source.Relative = e.RelativePosition; source.Values = e.Values; }
        }
        private void Add(int slot) { if (positions[slot] < 0) { positions[slot] = count; active[count++] = slot; } }
        private void Remove(int slot)
        {
            int p = positions[slot]; if (p < 0) return;
            int moved = active[--count]; active[p] = moved; positions[moved] = p; positions[slot] = -1;
        }
        private void Put(int slot, float value, Action<int,float> write)
        {
            if (!DecoBakeCore.Finite(value)) throw new InvalidOperationException("live nonfinite");
            if (Last[slot] != value) { Last[slot] = value; if (Owned[slot/16]) write(slot, value); }
        }
        internal void Resync(double time, Action<int,float> write)
        {
            count = 0;
            foreach (var source in sources.Values) source.Started = false;
            for (int slot = 0; slot < Last.Length; slot++)
            {
                positions[slot] = -1; clocks[slot] = null;
                if (table.Decorations[slot/16].Reasons.Count != 0) continue;
                var track = table.Tracks[slot]; int index = DecoBakeCore.Find(track,time);
                Last[slot] = float.NaN; Put(slot, table.Eval(slot/16,slot%16,time),write);
                if (index < 0) continue;
                var s = current[slot] = track[index];
                if ((time-s.T0)/table.Pitch*table.TweenScale < s.Duration) Add(slot);
            }
        }
        // Called at the real FastMove trigger, before its per-target loop. A Source is advanced
        // once at the existing DecoAnim hook, hence triggers after that hook start next frame.
        internal bool Start(int id, int frame, Action<int,float> write)
        {
            if (!sources.TryGetValue(id,out var source)) return false;
            if (source.Started) throw new InvalidOperationException("duplicate live source");
            source.Started = true; source.Pos = 0; source.Frame = -1;
            for (int i = 0; i < source.Slots.Count; i++)
            {
                int slot = source.Slots[i]; var s = source.Segments[i]; float before = Last[slot];
                // Kill(true): read relative base first, then complete the outgoing channel.
                float start = positions[slot] < 0 ? before : current[slot].End;
                if (positions[slot] >= 0) { Put(slot,start,write); Replacements++; }
                float target = source.Relative && slot%16 < 2 ? before+source.Values[slot%16] : s.Target;
                s.Start = start; s.Target = target;
                // Scale and parallax multiplier have no OnComplete target setter.
                s.End = s.IsScale || slot%16 == 12 || slot%16 == 13 ? start+(target-start)*table.Ease(s.Ease,s.Duration == 0 ? 1 : s.Duration,
                    s.Duration == 0 ? 1 : s.Duration,s.Over,s.Period) : target;
                current[slot] = s; clocks[slot] = source;
                if (s.Duration == 0) { Put(slot,s.End,write); Remove(slot); }
                else if (start == target) { Put(slot,s.End,write); Remove(slot); }
                else Add(slot);
            }
            return true;
        }
        internal void Step(double time, bool resync, Action<int,float> write, float dt, int frame)
        {
            if (!Enabled) return;
            if (!DecoBakeCore.Finite(time) || !DecoBakeCore.Finite(dt) || dt < 0) throw new InvalidOperationException("live clock");
            if (resync) Resync(time,write);
            Evaluated = Anchored = Fallback = Ended = Constant = LongChannels = DecorationsEvaluated = SourcesEvaluated = 0;
            statisticsFrame++; Array.Clear(ByChannel,0,16);
            // Same tiny-delta early return as DecoAnim.UpdatePrefix.
            if (dt < 1E-06f) return;
            for (int i = 0; i < count;)
            {
                int slot = active[i]; var s = current[slot]; var clock = clocks[slot]; float pos;
                if (clock != null)
                {
                    if (clock.Frame != frame) { clock.Pos += dt; clock.Frame = frame; SourcesEvaluated++; }
                    pos = clock.Pos; Anchored++;
                }
                else { pos = (float)((time-s.T0)/table.Pitch*table.TweenScale); Fallback++; }
                float to = Math.Min(pos,s.Duration);
                float value = to >= s.Duration ? s.End : s.Start+(s.Target-s.Start)*table.Ease(s.Ease,to,s.Duration,s.Over,s.Period);
                Put(slot,value,write); Evaluated++; ByChannel[slot%16]++; if (s.Duration >= 10) LongChannels++;
                if (decorationFrame[slot/16] != statisticsFrame) { decorationFrame[slot/16] = statisticsFrame; DecorationsEvaluated++; }
                if (pos >= s.Duration) { Ended++; Remove(slot); }
                else if (s.Start == s.Target) { Constant++; Remove(slot); }
                else i++;
            }
        }
        internal void Stop(double time, Action<int,float> write)
        {
            Enabled = false; Exception first = null;
            // A setter can fail AFTER evaluation removed an ended slot. Force the cached
            // state out as well; equality of Last does not prove the live setter committed.
            for (int slot = 0; slot < Last.Length; slot++) if (Owned[slot/16])
                try
                {
                    float value = positions[slot] >= 0 ? current[slot].End : Last[slot];
                    if (!DecoBakeCore.Finite(value)) throw new InvalidOperationException("live recovery nonfinite");
                    write(slot,value); Last[slot] = value;
                }
                catch (Exception e) { if (first == null) first = e; }
            if (first != null) throw first;
        }
    }
}
#endif
