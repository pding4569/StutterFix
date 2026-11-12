#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;
using System.Threading;
namespace StutterFix
{
    // Explicit diagnostic only. Fixed buffer, no stack dereference/unwind,
    // no symbol lookup/allocation/logging while the target is suspended.
    // A sleeping worker replaces the old profiler's full-core spin loop.
    // FPS/stutters from this run are excluded from performance evidence.
    internal static class LeafProbe
    {
        const int Capacity=4096;
        [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
        [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenThread(uint access,bool inherit,uint id);
        [DllImport("kernel32.dll")]static extern uint SuspendThread(IntPtr thread);
        [DllImport("kernel32.dll")]static extern int ResumeThread(IntPtr thread);
        [DllImport("kernel32.dll")]static extern bool GetThreadContext(IntPtr thread,IntPtr context);
        [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
        static AutoResetEvent wake;
        static IntPtr target;
        static Thread worker;
        static volatile bool running;
        static ulong[] addresses;
        static int count,failures,resumeFailures,foreignSuspensions,hz;
        static long begin,end,pauseTicks,maxPauseTicks;
        static uint tid;
        static string path,error;
        [DataContract] public sealed class Row {[DataMember]public string Symbol;[DataMember]public bool Managed;[DataMember]public int Samples;}
        [DataContract]
        public sealed class Report
        {
            [DataMember]public string Scope="main-thread leaf samples; not CPU execution attribution, physical display or FPS evidence";
            [DataMember]public string Error;
            [DataMember]public int RequestedHz;
            [DataMember]public int Samples;
            [DataMember]public int ContextFailures;
            [DataMember]public int ResumeFailures;
            [DataMember]public int ForeignSuspensions;
            [DataMember]public uint ThreadId;
            [DataMember]public double Seconds;
            [DataMember]public double PauseUpperBoundMs;
            [DataMember]public double MaxPauseUpperBoundMs;
            [DataMember]public bool WorkerStopped;
            [DataMember]public bool HandlesClosed;
            [DataMember]public bool TimingContaminated=true;
            [DataMember]public Row[] Rows;
            [DataMember]public string[] Addresses;
        }
        internal static string Command(string arg)
        {
            if(arg=="end")return Finish();
            if(arg=="off"){Stop();return "LEAF_PROBE stopped=True handles=0";}
            if(!arg.StartsWith("begin ",StringComparison.Ordinal))throw new ArgumentException("leafprobe begin <50..250Hz> <new absolute JSON>|end|off");
            if(worker!=null||ProfSamp.Running)throw new InvalidOperationException("another sampler is active or awaiting completion");
            string options=arg.Substring(6).Trim();int split=options.IndexOf(' ');
            if(split<1||!int.TryParse(options.Substring(0,split),out hz)||hz<50||hz>250)throw new ArgumentException("bounded sampling frequency required");
            path=options.Substring(split+1).Trim();
            if(!Path.IsPathRooted(path)||File.Exists(path)||!Directory.Exists(Path.GetDirectoryName(path)))throw new ArgumentException("new owned output file required");
            try
            {
                tid=GetCurrentThreadId();target=OpenThread(0x0002|0x0008|0x0040,false,tid);
                if(target==IntPtr.Zero)throw new InvalidOperationException("OpenThread failed "+Marshal.GetLastWin32Error());
                addresses=new ulong[Capacity];count=failures=resumeFailures=foreignSuspensions=0;pauseTicks=maxPauseTicks=0;error=null;
                wake=new AutoResetEvent(false);running=true;begin=Stopwatch.GetTimestamp();
                worker=new Thread(Loop){IsBackground=true,Name="StutterFix.LeafProbe",Priority=ThreadPriority.Normal};worker.Start();
                return "LEAF_PROBE started=True hz="+hz+" main_tid="+tid+" timing_contaminated=True no_stack_unwind=True";
            }
            catch{running=false;worker=null;wake?.Dispose();wake=null;if(target!=IntPtr.Zero){CloseHandle(target);target=IntPtr.Zero;}addresses=null;throw;}
        }
        static void Loop()
        {
            IntPtr raw=Marshal.AllocHGlobal(1264);IntPtr context=(IntPtr)((raw.ToInt64()+15)&~15L);
            try
            {
                // Warm every interop/Marshal path without suspending any thread.
                Marshal.WriteInt32(context,0x30,0x100001);SuspendThread(IntPtr.Zero);GetThreadContext(IntPtr.Zero,context);ResumeThread(IntPtr.Zero);Marshal.ReadInt64(context,0xF8);
                while(running&&count<Capacity)
                {
                    if(wake.WaitOne(Math.Max(1,(int)Math.Ceiling(1000.0/hz)))||!running)break;
                    long start=Stopwatch.GetTimestamp();uint previous=SuspendThread(target);
                    if(previous==uint.MaxValue){failures++;continue;}
                    try
                    {
                        if(previous!=0){foreignSuspensions++;continue;}
                        Marshal.WriteInt32(context,0x30,0x100001);
                        if(GetThreadContext(target,context))addresses[count++]=(ulong)Marshal.ReadInt64(context,0xF8);else failures++;
                    }
                    finally
                    {
                        if(ResumeThread(target)<0){resumeFailures++;running=false;}
                        long ticks=Stopwatch.GetTimestamp()-start;pauseTicks+=ticks;if(ticks>maxPauseTicks)maxPauseTicks=ticks;
                    }
                    // No catch-up bursts. Stop on excessive observed pause cost.
                    if(maxPauseTicks>Stopwatch.Frequency/100){running=false;error="pause_upper_bound_exceeded_10ms";}
                }
            }
            catch(Exception ex){error=ex.GetType().Name;running=false;}
            finally{Marshal.FreeHGlobal(raw);end=Stopwatch.GetTimestamp();}
        }
        static void StopWorker()
        {
            running=false;wake?.Set();
            if(worker!=null&&!worker.Join(2000))throw new InvalidOperationException("sampler worker retained=UNCONFIRMED; handles not closed");
            worker=null;if(target!=IntPtr.Zero){CloseHandle(target);target=IntPtr.Zero;}wake?.Dispose();wake=null;
        }
        static string Finish()
        {
            if(addresses==null)return "LEAF_PROBE status=UNMEASURED reason=no_sample";
            StopWorker();
            var rows=new Dictionary<string,Row>();
            try
            {
                for(int i=0;i<count;i++)
                {
                    bool managed;string symbol=ProfSamp.ResolveLeaf(addresses[i],out managed);
                    if(!rows.TryGetValue(symbol,out var row)){row=new Row{Symbol=symbol,Managed=managed};rows.Add(symbol,row);}row.Samples++;
                }
                var report=new Report{Error=error,RequestedHz=hz,Samples=count,ContextFailures=failures,ResumeFailures=resumeFailures,ForeignSuspensions=foreignSuspensions,ThreadId=tid,
                    Seconds=(end-begin)/(double)Stopwatch.Frequency,PauseUpperBoundMs=pauseTicks*1000.0/Stopwatch.Frequency,MaxPauseUpperBoundMs=maxPauseTicks*1000.0/Stopwatch.Frequency,
                    WorkerStopped=true,HandlesClosed=target==IntPtr.Zero,Rows=rows.Values.OrderByDescending(r=>r.Samples).ToArray(),Addresses=addresses.Take(count).Select(a=>a.ToString("X")).ToArray()};
                using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read))new DataContractJsonSerializer(typeof(Report)).WriteObject(stream,report);
                return "LEAF_PROBE completed=True samples="+count+" handles=0 timing_contaminated=True path="+path;
            }
            finally{addresses=null;ProfSamp.ClearLeafCache();}
        }
        internal static void Stop(){StopWorker();addresses=null;}
    }
}
#endif
