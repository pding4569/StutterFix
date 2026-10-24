#if DEV || AUTOTEST
using System;
using System.Globalization;
using Unity.Profiling;
using UnityEngine;

namespace StutterFix
{
    // Explicit diagnostic windows only. Engine counters use an owned recorder;
    // no new managed per-frame callback or game method patch.
    internal sealed class GpuDecoRenderMetrics : IDisposable
    {
        static readonly string[] names={"Draw Calls Count","SetPass Calls Count","Batches Count","Vertices Count"};
        readonly ProfilerRecorder[] recorders=new ProfilerRecorder[names.Length];
        readonly string[] faults=new string[names.Length];
        const int Capacity=4096;
        internal GpuDecoRenderMetrics()
        {
            for(int i=0;i<names.Length;i++)
                try {recorders[i]=ProfilerRecorder.StartNew(ProfilerCategory.Render,names[i],Capacity);}
                catch(Exception e) {faults[i]=e.GetType().Name;}
        }
        internal void Report(int window,Action<string> log)
        {
            try
            {
                for(int i=0;i<names.Length;i++)
                {
                    var r=recorders[i];
                    if(faults[i]!=null || !r.Valid || r.Count<2 || r.WrappedAround)
                    {log("render_counter window="+window+" name="+names[i].Replace(' ','_')+" status=UNMEASURED reason="+(faults[i]??(!r.Valid?"unavailable":r.WrappedAround?"capacity":"no_samples")));continue;}
                    r.Stop();long min=long.MaxValue,max=0;double sum=0;int count=r.Count-1;
                    // First sample can straddle recorder creation; exclude it.
                    for(int k=1;k<r.Count;k++) {long v=r.GetSample(k).Value;min=Math.Min(min,v);max=Math.Max(max,v);sum+=v;}
                    log("render_counter window="+window+" name="+names[i].Replace(' ','_')+" status=SAMPLED samples="+count
                        +" mean="+(sum/count).ToString("F6",CultureInfo.InvariantCulture)+" min="+min+" max="+max+" scope=whole_completed_frames first_sample_excluded=True cpu_cost=UNMEASURED");
                }
            }
            catch(Exception e) {log("render_counter window="+window+" status=UNMEASURED reason="+e.GetType().Name);}
            finally {Dispose();}
        }
        internal static string ScreenState()
        {
            try {var r=Screen.currentResolution;return "window="+Screen.width+"x"+Screen.height+" display="+r.width+"x"+r.height
                +" hz="+r.refreshRateRatio.value.ToString("R",CultureInfo.InvariantCulture)+" mode="+Screen.fullScreenMode+" sync="+QualitySettings.vSyncCount
                +" graphics="+SystemInfo.graphicsDeviceType+" target="+Application.targetFrameRate;}
            catch(Exception e) {return "screen=UNMEASURED reason="+e.GetType().Name;}
        }
        public void Dispose()
        {for(int i=0;i<recorders.Length;i++)try {recorders[i].Dispose();recorders[i]=default;}catch {}}
    }
}
#endif
