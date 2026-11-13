#if AUTOTEST
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Text;
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
        static BufferResize resize;
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
                if(resize!=null && !resize.Done) {resize.Check();return false;}
                if(resize!=null) resize.Check();
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
                int id=a[1]=="observe"?1:a[1]=="tearing"?2:a[1]=="latency4"?3:a[1]=="buffers3"?6:0;
                if(id==0) throw new Exception("presentpath: unknown candidate");
                if(requested!=null || stopping) throw new Exception("presentpath: finish off before on");
                if(!SfNative.Ready || sf_framegen_installed()!=0) throw new Exception("presentpath: native unavailable or framegen installed");
                captured=Resolve();callback=sf_presentpath_event_ptr();
                if(callback==IntPtr.Zero) throw new Exception("presentpath: missing native event");
                if(id==6) resize=new BufferResize(); // snapshot before any resolution request
                else resize=null;
                requested=a[1];startFrame=Time.frameCount;
                FrameTimingManager.CaptureFrameTimings();
                sf_presentpath_pin(captured);sf_presentpath_pending();Issue(id,captured);Log("queued "+requested+"; verify with assert after elapsed frames");return true;
            }
            if(a.Length==1 && a[0]=="assert")
            {
                if(callback==IntPtr.Zero || requested==null || stopping) throw new Exception("presentpath INVALID: no active lease");
                if(resize!=null) {resize.Check();if(!resize.Ready)return false;}
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
                if(requested=="buffers3" && (Stat(2)!=3 || Stat(20)==0 || Stat(25)!=0 || Stat(27)==0 || Stat(17)<0))
                    throw new Exception("presentpath INVALID: buffers3 resize/count/desc verification failed");
                State();Log("accepted "+requested);return true;
            }
            throw new Exception("presentpath state|on observe|on tearing|on latency4|on buffers3|assert|off");
        }
        internal static void Stop()
        {
            if(callback==IntPtr.Zero || requested==null || stopping) return;
            // Immediately stops flag changes; render event restores COM latency and vptr, with ACK.
            sf_presentpath_disable();sf_presentpath_pending();stopping=true;assertPending=statePending=false;Issue(0,IntPtr.Zero);
            if(resize!=null) resize.Restore();
            Log("off queued; status=0 is restoration ACK");
        }
        static void State()
        {
            if(stopping && Stat(-1)==0 && (resize==null || resize.Done)) {stopping=false;requested=null;}
            uint samples=FrameTimingManager.GetLatestTimings(1,timing);
            FrameTimingManager.CaptureFrameTimings();
            Log("state requested="+(requested??"off")+" stopping="+stopping+" status="+Stat(-1)+" mode="+Stat(-2)+
                " desc1HR="+Stat(17)+" size="+Stat(0)+"x"+Stat(1)+" buffers="+Stat(2)+" effect="+Stat(3)+" creationFlags="+Stat(4)+
                " windowed="+Stat(5)+" latency="+Stat(6)+" latencyAPI="+Stat(16)+" presents="+Stat(7)+" sync="+Stat(8)+
                " inputFlags="+Stat(9)+" outputFlags="+Stat(10)+" presentHR="+Stat(11)+" presentLastUs="+Stat(12)+
                " presentMeanUs="+(Stat(7)>0?Stat(13)/(double)Stat(7):0)+" modified="+Stat(14)+" failures="+Stat(15)+
                " refresh="+Stat(18)+" ownsVtable="+Stat(19)+" currentWaitSamples="+samples+" currentWaitMs="+(samples>0?timing[0].cpuMainThreadPresentWaitTime:-1)+
                " resizeCalls="+Stat(20)+" resizeHR="+Stat(21)+" resizeInputCount="+Stat(22)+" resizeOutputCount="+Stat(23)+
                " resizeFlags="+Stat(24)+" resizeFailures="+Stat(25)+" originalBuffers="+Stat(26)+" resizeRewrites="+Stat(27)+
                " resizeReady="+(resize!=null && resize.Ready)+" resizeDone="+(resize!=null && resize.Done)+
                " resizeError="+(resize==null?"none":resize.Error)+
                " waitScope=latest_FrameTiming_not_exclusive_CPU_time");
        }
        // An independent Unity Update survives EndBatch/Stop and a mod disable long enough
        // to finish restoration. No game swapchain API is called from this main-thread pump.
        sealed class ResizePump : MonoBehaviour
        {
            void Update() {if(resize!=null) resize.Pump();}
        }
        sealed class BufferResize
        {
            readonly int w=Screen.width, h=Screen.height;
            readonly FullScreenMode screenMode=Screen.fullScreenMode;
            readonly ScreenPrefs prefs;
            GameObject driver;
            int phase=1, frame;
            float deadline=Time.realtimeSinceStartup+15;
            long calls;
            bool restore;
            string error;
            internal bool Ready, Done;
            internal string Error {get {return error??"none";}}
            internal BufferResize()
            {
                if(screenMode!=FullScreenMode.FullScreenWindow && screenMode!=FullScreenMode.Windowed)
                    throw new Exception("presentpath buffers3: requires windowed or FullScreenWindow");
                if(w<2 || h<2) throw new Exception("presentpath buffers3: invalid screen size");
                prefs=new ScreenPrefs();
                StartDriver();
                Log("buffers3 screen snapshot="+w+"x"+h+" mode="+screenMode+" registryValues="+prefs.Count);
            }
            internal void Check()
            {
                if(error!=null)throw new Exception("presentpath buffers3 INVALID: "+error);
                if((Ready || Done) && (Screen.width!=w || Screen.height!=h || Screen.fullScreenMode!=screenMode))
                    throw new Exception("presentpath buffers3 INVALID: screen changed after resize ACK");
            }
            internal void Restore()
            {
                if(restore || Done)return;
                restore=true;Ready=false;phase=1;deadline=Time.realtimeSinceStartup+15;
                if(driver==null)StartDriver();
            }
            void StartDriver()
            {
                driver=new GameObject("StutterFix.PresentBuffersRestore");
                UnityEngine.Object.DontDestroyOnLoad(driver);driver.AddComponent<ResizePump>();
            }
            void DestroyDriver() {UnityEngine.Object.Destroy(driver);driver=null;}
            void Request(int width)
            {
                calls=Stat(20);frame=Time.frameCount;deadline=Time.realtimeSinceStartup+15;
                Screen.SetResolution(width,h,screenMode);
            }
            internal void Pump()
            {
                if(Done || (phase==0 && !restore))return;
                try
                {
                    if(Time.realtimeSinceStartup>deadline)throw new Exception("resize ACK timeout phase="+phase);
                    if(phase==1)
                    {
                        if(!restore && Stat(-1)==2)return;
                        if(Stat(-1)<0)throw new Exception("native status="+Stat(-1));
                        // Off may beat the very first engine resize; there is then no count
                        // change to undo and native stop can already have detached safely.
                        if(restore && Stat(-1)==0) {Request(w);phase=5;return;}
                        Request(w-1);phase=2;return;
                    }
                    if(phase==2 || phase==3)
                    {
                        if(Time.frameCount<=frame+2)return;
                        if(Screen.fullScreenMode!=screenMode)throw new Exception("screen mode changed");
                        int targetWidth=phase==2?w-1:w;
                        if(Screen.width!=targetWidth || Screen.height!=h || Stat(20)<=calls)return;
                        if((!restore && Stat(25)!=0) || Stat(21)<0 || Stat(17)<0 || Stat(2)!=(restore?Stat(26):3))
                            throw new Exception("resize failed or target count not observed");
                        if(Resolve()!=captured || Stat(19)!=1)throw new Exception("swapchain/vtable replaced");
                        if(phase==2) {Request(w);phase=3;return;}
                        prefs.Restore();
                        if(!restore) {Ready=true;phase=0;DestroyDriver();Log("buffers3 ready; screen and preferences restored");return;}
                        Issue(0,IntPtr.Zero);phase=4;return;
                    }
                    if(phase==4)
                    {
                        if(Stat(-1)==2)return;
                        if(Stat(-1)!=0 || Stat(2)!=Stat(26) || Resolve()!=captured || Screen.width!=w || Screen.height!=h || Screen.fullScreenMode!=screenMode)
                            throw new Exception("off restoration rejected");
                        prefs.Restore();Done=true;DestroyDriver();
                        Log("buffers3 off ACK; buffers/screen/preferences restored");
                    }
                    if(phase==5 && Time.frameCount>frame+2 && Screen.width==w && Screen.height==h && Screen.fullScreenMode==screenMode)
                    {
                        if(Resolve()!=captured || Stat(-1)!=0 || Stat(2)!=Stat(26))throw new Exception("early off identity/count mismatch");
                        prefs.Restore();Done=true;DestroyDriver();
                        Log("buffers3 early off ACK; screen/preferences restored; count unchanged");
                    }
                }
                catch(Exception ex)
                {
                    error=(error==null?"":error+"; ")+ex.Message;Ready=false;
                    sf_presentpath_disable();
                    if(!restore)
                    {
                        // One bounded cleanup attempt also covers an apply failure. The
                        // original error remains visible to assertions and the off command.
                        Restore();sf_presentpath_pending();Issue(0,IntPtr.Zero);
                        Log("buffers3 failed; restoring: "+error);return;
                    }
                    // A failed pulse is never accepted. Make a best-effort engine-owned
                    // return to the original dimensions, retain native refs until count ACK.
                    Screen.SetResolution(w,h,screenMode);
                    try {prefs.Restore();}catch(Exception e) {error+="; preferences: "+e.Message;}
                    Issue(0,IntPtr.Zero);Done=true;DestroyDriver();
                    Log("buffers3 FAILED: "+error+"; native status must be checked; restoration not certified");
                }
            }
        }
        // Preserve raw hashed registry names/types and the corresponding PlayerPrefs cache.
        // Refuse unfamiliar types before touching Screen. Only Screenmanager values are owned.
        sealed class ScreenPrefs
        {
            [DllImport("advapi32.dll",CharSet=CharSet.Unicode)] static extern int RegOpenKeyEx(IntPtr root,string path,int options,int access,out IntPtr key);
            [DllImport("advapi32.dll")] static extern int RegCloseKey(IntPtr key);
            [DllImport("advapi32.dll",CharSet=CharSet.Unicode)] static extern int RegEnumValue(IntPtr key,uint i,StringBuilder name,ref uint length,IntPtr reserved,out uint type,byte[] data,ref uint size);
            [DllImport("advapi32.dll",CharSet=CharSet.Unicode)] static extern int RegSetValueEx(IntPtr key,string name,int reserved,uint type,byte[] data,int size);
            [DllImport("advapi32.dll",CharSet=CharSet.Unicode)] static extern int RegDeleteValue(IntPtr key,string name);
            readonly string path="Software\\"+Application.companyName+"\\"+Application.productName;
            readonly Dictionary<string,byte[]> saved;
            readonly Dictionary<string,int?> cached=new Dictionary<string,int?>();
            internal int Count {get {return saved.Count;}}
            IntPtr Open()
            {
                IntPtr key;int e=RegOpenKeyEx(new IntPtr(unchecked((int)0x80000001)),path,0,0x2001f,out key);
                if(e!=0)throw new Exception("Screenmanager registry open="+e);return key;
            }
            static Dictionary<string,byte[]> ReadPrefs(IntPtr key)
            {
                var values=new Dictionary<string,byte[]>();
                for(uint i=0;;i++)
                {
                    var name=new StringBuilder(16384);uint length=16384,type,size=65536;var data=new byte[size];
                    int e=RegEnumValue(key,i,name,ref length,IntPtr.Zero,out type,data,ref size);
                    if(e==259)break;if(e!=0)throw new Exception("Screenmanager enum="+e);
                    string n=name.ToString();if(!n.StartsWith("Screenmanager ",StringComparison.Ordinal))continue;
                    if(type!=4 || size!=4)throw new Exception("Screenmanager non-DWORD: "+n);
                    Array.Resize(ref data,4);values.Add(n,data);
                }
                return values;
            }
            internal ScreenPrefs()
            {
                var key=Open();try {saved=ReadPrefs(key);}finally {RegCloseKey(key);}
                foreach(var n in saved.Keys)
                {
                    int hash=n.LastIndexOf("_h",StringComparison.Ordinal);if(hash<0)throw new Exception("Screenmanager unhashed name: "+n);
                    string logical=n.Substring(0,hash);
                    cached[logical]=PlayerPrefs.HasKey(logical)?(int?)PlayerPrefs.GetInt(logical):null;
                }
            }
            internal void Restore()
            {
                var key=Open();
                try
                {
                    // Flush first so newly created native Screenmanager cache keys are
                    // enumerable and removed from BOTH cache and registry below.
                    PlayerPrefs.Save();
                    var current=ReadPrefs(key);
                    foreach(var n in current.Keys)
                    {
                        int hash=n.LastIndexOf("_h",StringComparison.Ordinal);
                        if(hash<0)throw new Exception("Screenmanager unhashed new name: "+n);
                        string logical=n.Substring(0,hash);
                        if(!cached.ContainsKey(logical))PlayerPrefs.DeleteKey(logical);
                    }
                    foreach(var p in cached) {if(p.Value.HasValue)PlayerPrefs.SetInt(p.Key,p.Value.Value);else PlayerPrefs.DeleteKey(p.Key);}
                    PlayerPrefs.Save();
                    current=ReadPrefs(key);
                    foreach(var n in current.Keys)if(!saved.ContainsKey(n) && RegDeleteValue(key,n)!=0)throw new Exception("Screenmanager delete failed");
                    foreach(var p in saved)if(RegSetValueEx(key,p.Key,0,4,p.Value,4)!=0)throw new Exception("Screenmanager restore failed");
                    var check=ReadPrefs(key);if(check.Count!=saved.Count)throw new Exception("Screenmanager count mismatch");
                    foreach(var p in saved)if(!check.ContainsKey(p.Key) || BitConverter.ToUInt32(check[p.Key],0)!=BitConverter.ToUInt32(p.Value,0))throw new Exception("Screenmanager value mismatch");
                }
                finally {RegCloseKey(key);}
            }
        }
        static void Log(string s) {Main.Entry.Logger.Log("[presentpath] "+s);}
    }
}
#endif
