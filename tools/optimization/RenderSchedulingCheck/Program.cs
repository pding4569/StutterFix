using System;
using System.Runtime.InteropServices;
using System.Threading;
using StutterFix;
using UnityEngine;

var dll=NativeLibrary.Load(System.IO.Path.GetFullPath(args[0]));
NativeLibrary.SetDllImportResolver(typeof(RenderThreadScheduling).Assembly,(name,assembly,path)=>name=="sfnative"?dll:IntPtr.Zero);
int checks=0;
void Check(bool value,string why){checks++;if(!value)throw new Exception(why+" "+RenderThreadScheduling.FixtureState);}
try
{
    Hitch.Playing=true;RenderThreadScheduling.Configure(false,0,false,false);
    for(int i=0;i<100000;i++)RenderThreadScheduling.Tick();Check(GL.Events==0,"off emits no events");
    RenderThreadScheduling.Configure(true,0,false,true);RenderThreadScheduling.Tick();
    Time.unscaledTime=1;RenderThreadScheduling.Tick();Check(RenderThreadScheduling.FixtureState.Contains("owned=True"),"async worker capture applied");
    var masks=System.Text.RegularExpressions.Regex.Matches(RenderThreadScheduling.FixtureState,@"mask=([0-9A-F]+)\]");
    Check(masks.Count>=2&&Convert.ToUInt64(masks[1].Groups[1].Value,16)==(Convert.ToUInt64(masks[0].Groups[1].Value,16)&~1UL),"worker mask excludes only CPU0");
    RenderThreadScheduling.Configure(false,0,false,true);RenderThreadScheduling.Tick();Check(RenderThreadScheduling.FixtureState.Contains("owned=False")&&RenderThreadScheduling.FixtureState.Contains("restored=[tid="),"off restores before closing");
    RenderThreadScheduling.Configure(true,0,false,true);RenderThreadScheduling.Tick();Time.unscaledTime=2;RenderThreadScheduling.Tick();
    Hitch.Playing=false;RenderThreadScheduling.Tick();Check(RenderThreadScheduling.FixtureState.Contains("owned=False"),"leaving play restores");
    Hitch.Playing=true;GL.Drop=true;RenderThreadScheduling.Tick();Time.unscaledTime=6;RenderThreadScheduling.Tick();
    Check(RenderThreadScheduling.FixtureState.Contains("timeout")&&RenderThreadScheduling.FixtureState.Contains("pending=0"),"missing render callback cancels at timeout");
    GL.Drop=false;RenderThreadScheduling.Configure(false,0,false,true);RenderThreadScheduling.Tick();
    RenderThreadScheduling.Configure(true,0,false,true);RenderThreadScheduling.Tick();Time.unscaledTime=7;RenderThreadScheduling.Tick();Check(RenderThreadScheduling.FixtureState.Contains("owned=True"),"toggle retries after failed capture");
    RenderThreadScheduling.Shutdown();Check(RenderThreadScheduling.FixtureState.Contains("owned=False")&&RenderThreadScheduling.FixtureState.Contains("pending=0"),"unload releases and cancels");
    GL.Throw=true;RenderThreadScheduling.Configure(true,0,false,true);RenderThreadScheduling.Tick();
    Check(RenderThreadScheduling.FixtureState.Contains("pending=0")&&RenderThreadScheduling.FixtureState.Contains("owned=False"),"Unity event exception falls back");
    GL.Throw=false;RenderThreadScheduling.Configure(false,0,false,true);RenderThreadScheduling.Tick();
    RenderThreadScheduling.Configure(true,2,false,false);RenderThreadScheduling.Tick();Check(RenderThreadScheduling.FixtureState.Contains("owned=False"),"MMCSS alone does not tune render thread");
    Console.WriteLine($"Render scheduling lifecycle conditions {checks}, errors 0; fake Unity API / real owned worker; real game verification separate");
}
finally{RenderThreadScheduling.Shutdown();GL.Stop();NativeLibrary.Free(dll);}

namespace StutterFix
{
    internal static class Hitch{internal static bool Playing;}
    internal static class ThreadScheduling
    {
        internal static ulong SelectMask(IntPtr h,bool cores,bool avoid,out string detail)
        {detail="";Native.GetThreadGroupAffinity(h,out var a);return a.Mask.ToUInt64()&~1UL;}
    }
}
namespace UnityEngine
{
    public static class Time{public static float unscaledTime;}
    public static class GL
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate void Callback(int id);
        static readonly System.Collections.Concurrent.BlockingCollection<(IntPtr,int)> queue=new();
        static readonly AutoResetEvent done=new(false);
        static readonly Thread worker=new(()=>{foreach(var x in queue.GetConsumingEnumerable()){Marshal.GetDelegateForFunctionPointer<Callback>(x.Item1)(x.Item2);done.Set();}}){IsBackground=true};
        public static int Events;public static bool Drop,Throw;
        static GL(){worker.Start();}
        public static void IssuePluginEvent(IntPtr callback,int id)
        {Events++;if(Throw)throw new InvalidOperationException("fixture Unity event failure");if(Drop)return;queue.Add((callback,id));if(!done.WaitOne(3000))throw new Exception("fixture worker stalled");}
        public static void Stop(){queue.CompleteAdding();if(!worker.Join(3000))throw new Exception("worker stop timeout");done.Dispose();queue.Dispose();}
    }
}
internal static class Native
{
    [StructLayout(LayoutKind.Sequential)]internal struct Affinity{internal UIntPtr Mask;internal ushort Group,R0,R1,R2;}
    [DllImport("kernel32.dll")]internal static extern bool GetThreadGroupAffinity(IntPtr h,out Affinity a);
}
