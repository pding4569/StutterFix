#if DEV || AUTOTEST || GPUDECO_FIXTURE
using System;

namespace StutterFix
{
    // Preparation-only policy. Song scope keeps one conservative union of all
    // eligible decorations, instead of extrapolating visibility from a sampled
    // time window or duplicating meshes for every few seconds of a long song.
    internal sealed class GpuDecoScopePlan
    {
        internal const int SongItemLimit = 32768;
        internal readonly bool WholeSong;
        internal readonly double[] Starts, Ends;
        GpuDecoScopePlan(bool wholeSong, double[] starts, double[] ends)
        { WholeSong = wholeSong; Starts = starts; Ends = ends; }
        internal static GpuDecoScopePlan Reference() => new GpuDecoScopePlan(false,
            new[] {257.0,260.0,262.5,265.0,267.5,270.0,272.5,275.0,277.5},
            new[] {260.0,262.5,265.0,267.5,270.0,272.5,275.0,277.5,300.0});
        internal static GpuDecoScopePlan Song() => new GpuDecoScopePlan(true,
            new[] {double.MinValue}, new[] {double.MaxValue});
        internal int Find(double time)
        {
            if (double.IsNaN(time) || double.IsInfinity(time)) return -1;
            for (int i=0;i<Starts.Length;i++)
                if(time>=Starts[i] && time<Ends[i])return i;
            return -1; // Never clamp an unprepared time to a sampled scene.
        }
        internal void CheckItemBudget(int items)
        {
            if(items<0 || (WholeSong && items>SongItemLimit))
                throw new InvalidOperationException("gpudeco song item budget exceeded: "+items);
        }
    }
}
#endif
