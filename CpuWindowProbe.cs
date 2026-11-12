#if DEV || AUTOTEST
using System;
using System.Diagnostics;
using UnityEngine;
namespace StutterFix
{
    // Reuse the existing thread counter sampler at explicit boundaries only.
    // No per-frame hook, worker, scheduling change, or administrator access.
    internal static class CpuWindowProbe
    {
        static GpuDecoThreadCpu sample;
        static int frame;
        static long clock;
        static void Log(string message)=>Main.Entry.Logger.Log("[cpuwindow] "+message);
        internal static string Command(string arg)
        {
            if(arg=="off"){Stop();return "CPU_WINDOW stopped=True handles=0";}
            if(arg=="begin")
            {
                Stop();sample=GpuDecoThreadCpu.Begin(Log);frame=Time.frameCount;clock=Stopwatch.GetTimestamp();
                return "CPU_WINDOW started="+(sample!=null)+" handles="+(sample?.HandleCount??0)+" scope=boundary_cpu_upper_bound";
            }
            if(arg!="end")throw new ArgumentException("cpuwindow begin|end|off");
            var current=sample;sample=null;
            if(current==null)return "CPU_WINDOW status=UNMEASURED reason=no_sample";
            try{current.Complete(0,Time.frameCount-frame,(Stopwatch.GetTimestamp()-clock)/(double)Stopwatch.Frequency,GpuDecoCopy.RenderThreadId(),Log);}
            finally{current.Dispose();}
            return "CPU_WINDOW completed=True handles=0 waits=UNMEASURED";
        }
        internal static void Stop(){sample?.Dispose();sample=null;}
    }
}
#endif
