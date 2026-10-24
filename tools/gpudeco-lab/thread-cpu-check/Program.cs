using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using StutterFix;

static class Program
{
    [DllImport("kernel32.dll")] static extern int SetThreadDescription(IntPtr thread,[MarshalAs(UnmanagedType.LPWStr)] string text);
    [DllImport("kernel32.dll")] static extern IntPtr GetCurrentThread();
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll")] static extern bool GetProcessHandleCount(IntPtr process,out uint count);
    static void Require(bool value,string message) {if(!value)throw new Exception(message);}
    static void Main()
    {
        Require(!GpuDecoThreadCpu.Enabled,"must default off");
        var ready=new ManualResetEventSlim();var run=new ManualResetEventSlim();uint workerId=0;
        var worker=new Thread(()=>
        {
            workerId=GetCurrentThreadId();SetThreadDescription(GetCurrentThread(),"sf-threadcpu-fixture");ready.Set();run.Wait();
            var timer=Stopwatch.StartNew();double n=1;while(timer.ElapsedMilliseconds<200)n=Math.Sqrt(n+3);GC.KeepAlive(n);
        });worker.Start();ready.Wait();
        var lines=new List<string>();var sample=GpuDecoThreadCpu.Begin(lines.Add);Require(sample!=null&&sample.HandleCount>0,"capture failed");
        run.Set();worker.Join(); // Held kernel handle must still report an exited thread.
        sample.Complete(0,20,.2,workerId,lines.Add);Require(sample.HandleCount==0,"handles retained after complete");
        string row=lines.Single(x=>x.StartsWith("threadcpu_row ")&&x.Contains("tid="+workerId+" "));
        Require(row.Contains("role=render")&&row.Contains("name=sf-threadcpu-fixture"),"identity lost");
        double cpu=double.Parse(row.Split(' ').Single(x=>x.StartsWith("cpu_ms=")).Substring(7),CultureInfo.InvariantCulture);
        Require(cpu>=50,"busy worker CPU missing");
        sample.Dispose(); // idempotent
        // Warm runtime metadata, then verify repeated abort cleanup does not leak handles.
        for(int i=0;i<4;i++)GpuDecoThreadCpu.Begin(lines.Add)?.Dispose();
        Require(GetProcessHandleCount(GetCurrentProcess(),out var before),"handle count failed");
        for(int i=0;i<32;i++) {var s=GpuDecoThreadCpu.Begin(lines.Add);Require(s!=null,"repeat failed");s.Dispose();s.Dispose();}
        Require(GetProcessHandleCount(GetCurrentProcess(),out var after),"handle count failed");
        Require(after<=before+4,"handle leak");
        foreach(string line in lines.Take(15))Console.WriteLine(line);
        Console.WriteLine("THREAD_CPU_FIXTURE=PASS exited_identity=True busy_cpu_ms="+cpu.ToString("F3",CultureInfo.InvariantCulture)+" handle_delta="+((long)after-before)+" default_off=True per_frame_callbacks=0");
    }
}
