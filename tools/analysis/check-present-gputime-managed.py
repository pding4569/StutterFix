"""Compile actual managed command/lifecycle methods against a fake Unity/native API."""
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[2]
source = (root / 'PresentPath.cs').read_text(encoding='utf-8-sig')

def method(marker):
    start = source.index(marker)
    brace = source.index('{', start)
    depth = 1
    end = brace + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]

methods = '\n'.join(method(m) for m in [
    'static void GpuCamera(', 'static void StopGpuCamera(',
    'internal static bool Step(', 'internal static void Stop(', 'static void State()'])
prefix = r'''
using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace UnityEngine {
    class Camera {
        public delegate void CameraCallback(Camera c);
        public static event CameraCallback onPreRender;
        public object targetTexture;
        public static int Subscribers=>onPreRender?.GetInvocationList().Length??0;
        public static void Render(Camera c)=>onPreRender?.Invoke(c);
    }
    static class Graphics { public static void ExecuteCommandBuffer(CommandBuffer c){Test.Issue(c.id,IntPtr.Zero);} }
    static class Time { public static int frameCount=10; }
    struct FrameTiming {public double cpuMainThreadPresentWaitTime;}
    static class FrameTimingManager {
        public static void CaptureFrameTimings(){}
        public static uint GetLatestTimings(uint n,FrameTiming[] f)=>0;
    }
}
namespace UnityEngine.Rendering {
    class CommandBuffer : IDisposable {
        public string name;public int id;
        public static int Created,Released;
        public CommandBuffer(){Created++;}
        public void IssuePluginEventAndData(IntPtr p,int i,IntPtr data){id=i;}
        public void Release(){Released++;}
        public void Dispose()=>Release();
    }
}
static class SfNative {internal static bool Ready=true;}
class Test {
    static IntPtr callback,captured;
    static bool stopping,assertPending,statePending;
    static string requested;
    static int startFrame;
    static long refreshBefore,stateRefreshBefore;
    static CommandBuffer gpuBegin;
    static readonly Camera.CameraCallback gpuCameraCallback=GpuCamera;
    static readonly FrameTiming[] timing=new FrameTiming[1];
    static long[] stats=new long[61];
    static long status,mode;
    static BufferResize resize;
    static string log="";
    class BufferResize {
        internal bool Done=true,Ready=true;internal string Error="none";
        internal BufferResize(bool p){} internal void Check(){} internal void Restore(){}
    }
    static long Stat(int i)=>i==-1?status:i==-2?mode:stats[i];
    static void VerifyBinary(){}
    static IntPtr Resolve()=>(IntPtr)23;
    static IntPtr sf_presentpath_event_ptr()=>(IntPtr)1;
    static int sf_framegen_installed()=>0;
    static void sf_presentpath_pin(IntPtr p){}
    static void sf_presentpath_pending(){status=2;}
    static void sf_presentpath_disable(){mode=0;}
    internal static void Issue(int id,IntPtr p) {
        if(id==0){status=0;stats[60]=0;return;}
        if(id==4){stats[18]++;return;}
        if(id==10){stats[36]++;return;}
        mode=id;status=1;stats[7]=1;stats[19]=1;stats[60]=id==9?1:0;
    }
    static void Log(string s){log+=s+"\n";}
'''
suffix = r'''
    static void Expect(bool p,string text){if(!p)throw new Exception(text);}
    static void AssertLease(){Expect(!Step("assert"),"assert queues refresh");Expect(Step("assert"),"assert ACK");}
    static void Main(){
        for(int cycle=0;cycle<3;cycle++) {
            Expect(Step("on gputime") && mode==9 && Camera.Subscribers==1,"start");
            int created=CommandBuffer.Created;
            long before=stats[36];
            Camera.Render(new Camera{targetTexture=new object()});Expect(stats[36]==before,"offscreen excluded");
            for(int i=0;i<100;i++)Camera.Render(new Camera());
            Expect(CommandBuffer.Created==created && stats[36]==before+100,"reused command buffer");
            Time.frameCount++;AssertLease();
            Expect(Step("off") && status==0 && mode==0 && Camera.Subscribers==0,"off ACK removes callback");
            Expect(CommandBuffer.Created==CommandBuffer.Released,"released once");
            Camera.Render(new Camera());Expect(stats[36]==before+100,"no callback after off");
        }
        Step("on gputime");Time.frameCount++;stats[53]=1;
        Expect(!Step("assert"),"failure refresh");bool rejected=false;
        try {Step("assert");}catch(Exception){rejected=true;}
        Expect(rejected,"query failure rejected");Stop(); // same entry point as Unload
        Expect(Camera.Subscribers==0 && gpuBegin==null,"Unload cleanup");Step("off");stats[53]=0;
        Step("on observe");Expect(Camera.Subscribers==0,"observe dormant GPU callback");Step("off");
        Expect(log.Contains("renderMeanUs=") && log.Contains("queryFailures="),"GPU state fields");
        Console.WriteLine("PASS actual Step/Stop/State/GpuCamera: mode9, 3 restart cycles, offscreen exclusion, 100 callbacks reuse one buffer, ACK, query failure refusal, Unload entry cleanup, observe isolation");
    }
}
'''
out = root / 'out/present-gputime/managed-fixture'
out.mkdir(parents=True, exist_ok=True)
(out / 'Test.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="Program.cs" /></ItemGroup></Project>', encoding='utf-8')
(out / 'Program.cs').write_text(prefix + methods + suffix, encoding='utf-8')
r = subprocess.run(['dotnet', 'run', '--project', str(out / 'Test.csproj'), '-c', 'Release'], cwd=root, capture_output=True, text=True)
(out.parent / 'managed-fixture.txt').write_text(r.stdout + r.stderr, encoding='utf-8')
print(r.stdout + r.stderr)
raise SystemExit(r.returncode)
