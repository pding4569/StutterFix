#if AUTOTEST
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix
{
    // Explicit, non-persistent research lease. No initialization until a command.
    internal static class PresentPath
    {
        [DllImport("sfnative", CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sf_presentpath_event_ptr();
        [DllImport("sfnative", CallingConvention=CallingConvention.Cdecl)] static extern long sf_presentpath_stat(int i);
        [DllImport("sfnative", CallingConvention=CallingConvention.Cdecl)] static extern void sf_presentpath_pin(IntPtr p);
        [DllImport("sfnative", CallingConvention=CallingConvention.Cdecl)] static extern void sf_presentpath_disable();
        [DllImport("sfnative", CallingConvention=CallingConvention.Cdecl)] static extern void sf_presentpath_pending();
        [DllImport("sfnative", CallingConvention=CallingConvention.Cdecl)] static extern int sf_framegen_installed();
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandleW(string name);
        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr process, IntPtr addr, byte[] bytes, IntPtr size, out IntPtr read);
        static IntPtr callback, captured;
        static bool stopping;
        static string requested;
        static int startFrame;
        static bool assertPending;
        static long refreshBefore;
        static bool statePending;
        static long stateRefreshBefore;
        static readonly FrameTiming[] timing=new FrameTiming[1];
        static long Stat(int i) { return callback==IntPtr.Zero ? 0 : sf_presentpath_stat(i); }
        static byte[] Read(long address, int length)
        {
            var b=new byte[length]; IntPtr n;
            if(address==0 || !ReadProcessMemory(GetCurrentProcess(), (IntPtr)address, b, (IntPtr)length, out n) || (long)n!=length)
                throw new Exception("presentpath: unreadable address");
            return b;
        }
        static IntPtr Resolve()
        {
            if(Application.unityVersion!="6000.3.25f1" || SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Direct3D11)
                throw new Exception("presentpath: requires pinned Unity 6000.3.25f1 D3D11");
            var path=Path.Combine(Path.GetDirectoryName(Application.dataPath), "UnityPlayer.dll");
            using(var hash=SHA256.Create()) using(var f=File.OpenRead(path))
                if(BitConverter.ToString(hash.ComputeHash(f)).Replace("-", "").ToLowerInvariant()!="7e362b4c6ecee6fb493b1872cda5a5a0caf44f3b131b3c4bad132f0d1309f436")
                    throw new Exception("presentpath: UnityPlayer SHA256 mismatch");
            long module=(long)GetModuleHandleW("UnityPlayer.dll");
            // PresentFrame loads settings from this RIP-relative global, then chain at +0x320.
            var guard=Read(module+0x930c84, 7);
            if(BitConverter.ToString(guard)!="48-8B-05-85-DE-8E-01") throw new Exception("presentpath: runtime instruction mismatch");
            long settings=BitConverter.ToInt64(Read(module+0x221eb10,8),0);
            long sc=BitConverter.ToInt64(Read(settings+0x320,8),0);
            if(sc==0) throw new Exception("presentpath: no swapchain");
            return (IntPtr)sc;
        }
        static void Issue(int id, IntPtr ptr)
        {
            using(var cb=new CommandBuffer())
            {
                cb.IssuePluginEventAndData(callback,id,ptr);
                Graphics.ExecuteCommandBuffer(cb);
            }
            GL.Flush();
        }
        internal static bool Step(string arg)
        {
            var a=arg.Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries);
            if(a.Length==1 && a[0]=="off")
            {
                Stop();
                if(callback!=IntPtr.Zero && Stat(-1)<0) throw new Exception("presentpath: restore failed status="+Stat(-1));
                if(callback!=IntPtr.Zero && Stat(-1)!=0) return false;
                State(); return true;
            }
            if(a.Length==1 && a[0]=="state")
            {
                if(stopping) {if(Stat(-1)==2) return false;State();return true;}
                if(requested!=null && Stat(-1)<0) {State();return true;}
                if(statePending)
                {
                    if(Stat(18)<=stateRefreshBefore) return false;
                    statePending=false;State();return true;
                }
                if(callback!=IntPtr.Zero && requested!=null) {stateRefreshBefore=Stat(18);Issue(4,IntPtr.Zero);}
                else
                {
                    if(!SfNative.Ready) throw new Exception("presentpath: native unavailable");
                    var sc=Resolve();callback=sf_presentpath_event_ptr();
                    if(callback==IntPtr.Zero) throw new Exception("presentpath: missing native event");
                    stateRefreshBefore=Stat(18);
                    sf_presentpath_pin(sc);Issue(5,sc); // read-only snapshot; no instance hook
                }
                statePending=true;
                return false; // wait for render-thread snapshot ACK before logging actual state
            }
            if(a.Length==2 && a[0]=="on")
            {
                int id=a[1]=="observe"?1:a[1]=="tearing"?2:a[1]=="latency4"?3:0;
                if(id==0) throw new Exception("presentpath: unknown candidate");
                if(requested!=null || stopping) throw new Exception("presentpath: finish off before on");
                if(!SfNative.Ready || sf_framegen_installed()!=0) throw new Exception("presentpath: native unavailable or framegen installed");
                captured=Resolve();callback=sf_presentpath_event_ptr();
                if(callback==IntPtr.Zero) throw new Exception("presentpath: missing native event");
                requested=a[1];startFrame=Time.frameCount;
                FrameTimingManager.CaptureFrameTimings();
                sf_presentpath_pin(captured);sf_presentpath_pending();Issue(id,captured);Log("queued "+requested+"; verify with assert after elapsed frames");return true;
            }
            if(a.Length==1 && a[0]=="assert")
            {
                if(callback==IntPtr.Zero || requested==null || stopping) throw new Exception("presentpath INVALID: no active lease");
                if(Stat(-1)<0) throw new Exception("presentpath INVALID: native rejected status="+Stat(-1));
                if(Stat(-1)==2) return false;
                if(!assertPending) {refreshBefore=Stat(18);assertPending=true;Issue(4,IntPtr.Zero);return false;}
                if(Stat(18)<=refreshBefore) return false;
                assertPending=false;
                if(requested==null || stopping || Stat(-1)!=1 || Time.frameCount<=startFrame || Stat(7)==0 || Resolve()!=captured || Stat(15)!=0)
                    throw new Exception("presentpath INVALID: inactive/recreated/failed/no observed Present");
                if(Stat(19)!=1) throw new Exception("presentpath INVALID: overlay replaced instance vtable");
                if(requested=="tearing" && Stat(14)==0) throw new Exception("presentpath INVALID: tearing no-op (already enabled or ineligible)");
                if(requested=="latency4" && Stat(6)!=4) throw new Exception("presentpath INVALID: latency target not observed");
                State();Log("accepted "+requested);return true;
            }
            throw new Exception("presentpath state|on observe|on tearing|on latency4|assert|off");
        }
        internal static void Stop()
        {
            if(callback==IntPtr.Zero || requested==null || stopping) return;
            // Immediately stops flag changes; render event restores COM latency and vptr, with ACK.
            sf_presentpath_disable();sf_presentpath_pending();stopping=true;assertPending=statePending=false;Issue(0,IntPtr.Zero);
            Log("off queued; status=0 is restoration ACK");
        }
        static void State()
        {
            if(stopping && Stat(-1)==0) {stopping=false;requested=null;}
            uint samples=FrameTimingManager.GetLatestTimings(1,timing);
            FrameTimingManager.CaptureFrameTimings();
            Log("state requested="+(requested??"off")+" stopping="+stopping+" status="+Stat(-1)+" mode="+Stat(-2)+
                " desc1HR="+Stat(17)+" size="+Stat(0)+"x"+Stat(1)+" buffers="+Stat(2)+" effect="+Stat(3)+" creationFlags="+Stat(4)+
                " windowed="+Stat(5)+" latency="+Stat(6)+" latencyAPI="+Stat(16)+" presents="+Stat(7)+" sync="+Stat(8)+
                " inputFlags="+Stat(9)+" outputFlags="+Stat(10)+" presentHR="+Stat(11)+" presentLastUs="+Stat(12)+
                " presentMeanUs="+(Stat(7)>0?Stat(13)/(double)Stat(7):0)+" modified="+Stat(14)+" failures="+Stat(15)+
                " refresh="+Stat(18)+" ownsVtable="+Stat(19)+" currentWaitSamples="+samples+" currentWaitMs="+(samples>0?timing[0].cpuMainThreadPresentWaitTime:-1)+
                " waitScope=latest_FrameTiming_not_exclusive_CPU_time");
        }
        static void Log(string s) {Main.Entry.Logger.Log("[presentpath] "+s);}
    }
}
#endif
