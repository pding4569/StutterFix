#if AUTOTEST
using System;
using System.Globalization;
using System.Linq;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        // Preparation-only census. Midpoint quadrature is explicitly an estimate,
        // not a visibility proof or a frame-time predictor. No per-frame work.
        static void LogGpuWorkload(double end)
        {
            const int samples=128;
            if(table==null || table.InputEvents==null || gpuLiveOwnedIndices.Length==0)return;
            if(end<=0 || double.IsInfinity(end) || double.IsNaN(end))return;
            var owned=new bool[table.Decorations.Length];
            foreach(int d in gpuLiveOwnedIndices)owned[d]=true;
            long references=0,visibleReferences=0,visible=0,moving=0;
            foreach(var e in table.InputEvents)
            {
                if(e.T0<0 || e.T0>end || e.Targets==null || e.Reasons.Length!=0)continue;
                foreach(int d in e.Targets)
                    if((uint)d<(uint)owned.Length && owned[d])
                    {
                        references++;
                        if(table.Eval(d,10,e.T0)>0 && table.Eval(d,11,e.T0)>0)visibleReferences++;
                    }
            }
            for(int sample=0;sample<samples;sample++)
            {
                double t=end*(sample+.5)/samples;
                foreach(int d in gpuLiveOwnedIndices)
                {
                    if(!(table.Eval(d,10,t)>0 && table.Eval(d,11,t)>0))continue;
                    visible++;
                    for(int c=0;c<16;c++)
                    {
                        if(c>=7 && c<=11)continue;
                        var track=table.Tracks[d*16+c];int at=DecoBakeCore.Find(track,t);
                        if(at<0)continue;
                        var s=track[at];
                        if(s.Duration>0 && s.Start!=s.Target && t<s.T0+s.Duration*table.Pitch/table.TweenScale)
                        {moving++;break;}
                    }
                }
            }
            string F(double x)=>x.ToString("F6",CultureInfo.InvariantCulture);
            GpuLog("WORKLOAD owned="+gpuLiveOwnedIndices.Length+" total="+owned.Length
                +" ownership="+F((double)gpuLiveOwnedIndices.Length/owned.Length)
                +" horizon_s="+F(end)+" event_targets="+references+" visible_event_targets="+visibleReferences
                +" visible_applications_per_s="+F(visibleReferences/end)
                +" mean_visible_est="+F((double)visible/samples)+" mean_visible_moving_est="+F((double)moving/samples)
                +" midpoint_samples="+samples+" horizon=last_floor_entry performance_predictor=False");
        }
    }
}
#endif
