"""Exercise the actual BufferResize class with a deferred fake Unity/DXGI environment.
No game, display API, registry, GPU, or installed DLL is used.
"""
from pathlib import Path
import subprocess
root = Path(__file__).resolve().parents[2]
source = (root/'PresentPath.cs').read_text(encoding='utf-8-sig')
start = source.index('        sealed class BufferResize')
end = source.index('        // Preserve raw hashed', start)
pump = source[start:end]
out = root/'out/present-buffers/pump-fixture'
out.mkdir(parents=True, exist_ok=True)
(out/'Pump.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="Program.cs" /></ItemGroup></Project>')
prefix = r'''
using System;
using UnityEngine;
namespace UnityEngine {
    class Object { public static void DontDestroyOnLoad(Object o){} public static void Destroy(Object o){} }
    class GameObject : Object { public GameObject(string n){} public T AddComponent<T>() where T:new(){return new T();} }
    class MonoBehaviour : Object {}
    enum FullScreenMode { FullScreenWindow=1,Windowed=3 }
    static class Time { public static int frameCount; public static float realtimeSinceStartup; }
    static class Screen {
        public static int width=1280,height=720;
        public static FullScreenMode fullScreenMode=FullScreenMode.FullScreenWindow;
        public static int pending;
        public static void SetResolution(int w,int h,FullScreenMode mode){pending=w;}
    }
}
class Test {
    class ResizePump : MonoBehaviour {}
    class ScreenPrefs {internal int Count=>17; internal void Restore(){prefRestores++;}}
    static int prefRestores;
    static long[] stats=new long[28];
    static int status=1,target=3;
    static bool ignore,reject,identity=true;
    static IntPtr captured=(IntPtr)1;
    static BufferResize resize;
    static long Stat(int i)=>i==-1?status:stats[i];
    static IntPtr Resolve()=>identity?captured:(IntPtr)2;
    static void Log(string s){}
    static void sf_presentpath_disable(){target=2;}
    static void sf_presentpath_pending(){status=2;}
    static void Issue(int id,IntPtr p){if(id==0)status=stats[2]==2?0:2;}
'''
suffix = r'''
    static void Expect(bool value,string name){if(!value)throw new Exception(name);}
    static void Reset(){
        stats=new long[28];stats[2]=stats[26]=2;stats[19]=1;
        status=1;target=3;ignore=reject=false;identity=true;prefRestores=0;
        Screen.width=1280;Screen.height=720;Screen.pending=0;Time.frameCount=0;Time.realtimeSinceStartup=0;
        resize=new BufferResize();
    }
    static void Tick(){
        Time.frameCount++;Time.realtimeSinceStartup+=0.1f;
        if(Screen.pending!=0){
            int w=Screen.pending;Screen.pending=0;
            if(!ignore){Screen.width=w;stats[20]++;stats[21]=reject?-1:0;
                if(reject)stats[25]++;else stats[2]=target;}
        }
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
        Until(()=>resize.Done);Restored();Expect(resize.Error!="none","failed apply stays rejected");
        Reset();ignore=true;Until(()=>resize.Error!="none");ignore=false;
        Until(()=>resize.Done);Restored();
        Reset();identity=false;Until(()=>resize.Done);
        Expect(resize.Error!="none" && !resize.Ready,"recreation rejected");
        Console.WriteLine("PASS actual BufferResize state machine: apply/off; early off; queued off; apply failure cleanup; ignored-resolution timeout cleanup; swapchain recreation refusal");
    }
}
'''
(out/'Program.cs').write_text(prefix+pump+suffix, encoding='utf-8')
result = subprocess.run(['dotnet','run','--project',str(out/'Pump.csproj'),'-c','Release'],cwd=root,capture_output=True,text=True)
(out.parent/'pump-fixture.txt').write_text(result.stdout+result.stderr,encoding='utf-8')
print(result.stdout+result.stderr)
raise SystemExit(result.returncode)
