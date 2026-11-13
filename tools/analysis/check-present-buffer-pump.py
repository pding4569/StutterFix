"""Exercise the actual BufferResize class with a deferred fake Unity/DXGI environment.
No game, display API, registry, GPU, or installed DLL is used.
"""
from pathlib import Path
import subprocess
root = Path(__file__).resolve().parents[2]
source = (root/'PresentPath.cs').read_text(encoding='utf-8-sig')
start = source.index('        sealed class BufferResize')
end = source.index('        // Preserve raw hashed', start)
pump = source[start:end].replace('System.Diagnostics.Stopwatch', 'FakeClock').replace('Main.Entry.Path', 'global::Main.Entry.Path')
out = root/'out/present-buffers/pump-fixture'
out.mkdir(parents=True, exist_ok=True)
(out/'Pump.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="Program.cs" /></ItemGroup></Project>')
prefix = r'''
using System;
using System.IO;
using UnityEngine;
class FakeClock {public static FakeClock StartNew()=>new FakeClock(); public long ElapsedMilliseconds=>(long)(Time.realtimeSinceStartup*1000);}
static class Main {internal static class Entry {internal static string Path=>AppContext.BaseDirectory;}}
namespace UnityEngine {
    class Object { public static void DontDestroyOnLoad(Object o){} public static void Destroy(Object o){} }
    class GameObject : Object { public GameObject(string n){} public T AddComponent<T>() where T:new(){return new T();} }
    class MonoBehaviour : Object {}
    enum FullScreenMode { FullScreenWindow=1,Windowed=3 }
    static class Time { public static int frameCount; public static float realtimeSinceStartup; }
    static class Screen {
        public static int width=1280,height=720;
        public static FullScreenMode fullScreenMode=FullScreenMode.FullScreenWindow;
        public static void SetResolution(int w,int h,FullScreenMode mode){throw new Exception("display request forbidden");}
    }
}
class Test {
    class ResizePump : MonoBehaviour {}
    class ScreenPrefs {internal int Count=>17; internal void Restore(){prefRestores++;}}
    static int prefRestores;
    static long[] stats=new long[36];
    static int status=1,target=3;
    static bool ignore,reject,identity=true,triggerPending,triggerFail;
    static string logs="";
    static IntPtr captured=(IntPtr)1;
    static BufferResize resize;
    static long Stat(int i)=>i==-1?status:stats[i];
    static IntPtr Resolve()=>identity?captured:(IntPtr)2;
    static void Log(string s){logs+=s+"\n";}
    static void sf_presentpath_disable(){target=2;}
    static void sf_presentpath_pending(){status=2;}
    static void Issue(int id,IntPtr p){if(id==0)status=stats[2]==2?0:2; if(id==8)triggerPending=true;}
'''
suffix = r'''
    static void Expect(bool value,string name){if(!value)throw new Exception(name);}
    static void Reset(){
        if(resize!=null)resize.Disarm();
        stats=new long[36];stats[2]=stats[26]=2;stats[19]=1;
        status=1;target=3;ignore=reject=false;identity=true;prefRestores=0;
        triggerPending=triggerFail=false;logs="";
        Screen.width=1280;Screen.height=720;stats[0]=1280;stats[1]=720;Time.frameCount=0;Time.realtimeSinceStartup=0;
        resize=new BufferResize();
    }
    static void Tick(){
        Time.frameCount++;Time.realtimeSinceStartup+=0.1f;stats[7]++;
        if(triggerPending){triggerPending=false;if(!ignore){stats[30]++;stats[31]=triggerFail?0:1;stats[32]=2;
            if(!triggerFail){stats[20]++;stats[21]=reject?-1:0;if(reject)stats[25]++;else stats[2]=target;}}}
        resize.Pump();
    }
    static void Until(Func<bool> done){for(int i=0;i<400 && !done();i++)Tick();Expect(done(),"bounded completion");}
    static void Off(){sf_presentpath_disable();sf_presentpath_pending();Issue(0,IntPtr.Zero);resize.Restore();}
    static void Restored(){Expect(resize.Done && status==0 && stats[2]==2 && Screen.width==1280 && prefRestores>0,"off restored");}
    static void Main(){
        Reset();Until(()=>resize.Ready);Expect(Screen.width==1280 && stats[2]==3,"apply ready");
        Off();Until(()=>resize.Done);resize.Check();Restored();
        Reset();Off();Until(()=>resize.Done);resize.Check();Restored(); // off before first resize
        Reset();Tick();Off();Until(()=>resize.Done);resize.Check();Restored(); // off with queued pulse
        Reset();reject=true;Until(()=>resize.Error!="none");reject=false;
        Until(()=>resize.Done);Restored();Expect(resize.Error!="none" && logs.Contains("status=-1 failed:") && logs.Contains("rollback ACK received"),"failed apply stays rejected and logs after rollback without Check");
        Reset();ignore=true;Until(()=>resize.Error!="none");ignore=false;
        Until(()=>resize.Done);Restored();Expect(logs.Contains("status=-1 failed:") && logs.Contains("rollback ACK received"),"timeout logs after rollback without Check");
        Reset();resize.Disarm();target=2;resize=new BufferResize(true);Until(()=>resize.Ready);
        Expect(stats[2]==2 && stats[20]>0,"probe count unchanged");Off();Until(()=>resize.Done);Restored();
        Reset();triggerFail=true;Until(()=>resize.Done);Expect(resize.Error!="none" && logs.Contains("failed"),"Adjust false rejected");
        Reset();stats[0]=1279;Until(()=>resize.Done);Expect(stats[30]==0 && resize.Error!="none","dimension guard");
        Reset();Time.realtimeSinceStartup=16;resize.Watchdog(null);Expect(logs.Contains("status=-1") && target==2,"watchdog without main pump");
        Until(()=>resize.Done);Expect(resize.Error!="none","watchdog rejection retained");
        Reset();identity=false;Until(()=>resize.Done);
        Expect(resize.Error!="none" && !resize.Ready,"recreation rejected");
        resize.Disarm();
        Console.WriteLine("PASS probe; Adjust refusal; exact size guard; watchdog without Pump; actual BufferResize state machine: apply/off; early off; queued off; apply failure cleanup; ignored-resolution timeout cleanup; swapchain recreation refusal");
    }
}
'''
(out/'Program.cs').write_text(prefix+pump+suffix, encoding='utf-8')
result = subprocess.run(['dotnet','run','--project',str(out/'Pump.csproj'),'-c','Release'],cwd=root,capture_output=True,text=True)
(out.parent/'pump-fixture.txt').write_text(result.stdout+result.stderr,encoding='utf-8')
print(result.stdout+result.stderr)
raise SystemExit(result.returncode)
