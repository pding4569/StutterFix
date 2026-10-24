#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace StutterFix
{
    // Explicit G2 boundary samples only: no PlayerLoop, per-frame timer,
    // scheduling changes, elevated access, GPU readback, or persistent worker.
    // Keep real handles so a recycled thread ID cannot join unrelated samples.
    internal sealed class GpuDecoThreadCpu : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] struct ThreadEntry
        { internal uint Size, Usage, Id, Process; internal int BasePriority, DeltaPriority; internal uint Flags; }
        [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr CreateToolhelp32Snapshot(uint flags,uint process);
        [DllImport("kernel32.dll", SetLastError=true)] static extern bool Thread32First(IntPtr snapshot,ref ThreadEntry entry);
        [DllImport("kernel32.dll", SetLastError=true)] static extern bool Thread32Next(IntPtr snapshot,ref ThreadEntry entry);
        [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr OpenThread(uint access,bool inherit,uint id);
        [DllImport("kernel32.dll", SetLastError=true)] static extern bool GetThreadTimes(IntPtr thread,out ulong created,out ulong exit,out ulong kernel,out ulong user);
        [DllImport("kernel32.dll")] static extern int GetThreadDescription(IntPtr thread,out IntPtr text);
        [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr memory);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        [DllImport("kernel32.dll")] static extern uint GetCurrentProcessId();
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        internal static bool Enabled; // default off, research builds only
        sealed class Sample
        { internal IntPtr Handle; internal uint Id; internal ulong Created,Cpu; internal long Clock; internal string Name; }
        readonly List<Sample> samples=new List<Sample>();
        uint mainId;
        int skipped;
        double beginMs;
        static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
        internal int HandleCount=>samples.Count;

        internal static GpuDecoThreadCpu Begin(Action<string> log)
        {
            var value=new GpuDecoThreadCpu();
            try {value.Capture();return value;}
            catch(Exception e) {value.Dispose();log("threadcpu status=UNMEASURED reason="+e.GetType().Name);return null;}
        }
        void Capture()
        {
            long begin=Stopwatch.GetTimestamp();
            mainId=GetCurrentThreadId();
            IntPtr snapshot=CreateToolhelp32Snapshot(4,0);
            if(snapshot==new IntPtr(-1))throw new InvalidOperationException("Thread inventory unavailable");
            try
            {
                uint process=GetCurrentProcessId();
                var entry=new ThreadEntry {Size=(uint)Marshal.SizeOf<ThreadEntry>()};
                if(!Thread32First(snapshot,ref entry))throw new InvalidOperationException("Thread inventory empty");
                do
                {
                    if(entry.Process!=process)continue;
                    IntPtr handle=OpenThread(0x0800,false,entry.Id); // QUERY_LIMITED_INFORMATION only
                    if(handle==IntPtr.Zero) {skipped++;continue;}
                    try
                    {
                        string name="unnamed";IntPtr text=IntPtr.Zero;
                        try {if(GetThreadDescription(handle,out text)>=0 && text!=IntPtr.Zero)name=Marshal.PtrToStringUni(text)??"unnamed";}
                        catch(EntryPointNotFoundException) {} // old OS: CPU totals remain usable
                        finally {if(text!=IntPtr.Zero)LocalFree(text);}
                        if(!GetThreadTimes(handle,out var created,out _,out var kernel,out var user)) {skipped++;continue;}
                        samples.Add(new Sample {Handle=handle,Id=entry.Id,Created=created,Cpu=kernel+user,Clock=Stopwatch.GetTimestamp(),Name=name});
                        handle=IntPtr.Zero;
                    }
                    finally {if(handle!=IntPtr.Zero)CloseHandle(handle);}
                    entry.Size=(uint)Marshal.SizeOf<ThreadEntry>();
                } while(Thread32Next(snapshot,ref entry));
            }
            finally {CloseHandle(snapshot);}
            if(samples.Count==0)throw new InvalidOperationException("Thread counters unavailable");
            beginMs=(Stopwatch.GetTimestamp()-begin)*1000.0/Stopwatch.Frequency;
        }
        internal void Complete(int index,int frames,double fpsWindowSeconds,uint renderId,Action<string> log)
        {
            long endBegin=Stopwatch.GetTimestamp();
            var rows=new List<Tuple<Sample,double,double>>();int failed=0;
            try
            {
                foreach(var s in samples)
                {
                    if(!GetThreadTimes(s.Handle,out var created,out _,out var kernel,out var user) || created!=s.Created || kernel+user<s.Cpu) {failed++;continue;}
                    double cpu=(kernel+user-s.Cpu)/10000.0;
                    double span=(Stopwatch.GetTimestamp()-s.Clock)/(double)Stopwatch.Frequency;
                    rows.Add(Tuple.Create(s,cpu,span));
                }
                double endMs=(Stopwatch.GetTimestamp()-endBegin)*1000.0/Stopwatch.Frequency;
                rows.Sort((a,b)=>b.Item2.CompareTo(a.Item2));
                log("threadcpu window="+index+" status="+(failed==0&&skipped==0?"SAMPLED":"PARTIAL")+" threads="+rows.Count+" skipped_begin="+skipped+" failed_end="+failed
                    +" frames="+frames+" fps_window_s="+fpsWindowSeconds.ToString("F6",Inv)+" begin_probe_ms="+beginMs.ToString("F6",Inv)+" end_probe_ms="+endMs.ToString("F6",Inv)
                    +" main_tid="+mainId+" render_tid="+renderId+" scope=boundary_cpu_upper_bound new_threads=UNMEASURED waits=UNMEASURED");
                for(int i=0;i<rows.Count;i++)
                {
                    var r=rows[i];var s=r.Item1;
                    // Include the main and verified native render thread even outside top 12.
                    if(i>=12 && s.Id!=mainId && s.Id!=renderId)continue;
                    string name=s.Name.Replace(' ','_').Replace('\r','_').Replace('\n','_');if(name.Length>80)name=name.Substring(0,80);
                    log("threadcpu_row window="+index+" tid="+s.Id+" role="+(s.Id==mainId?"main":s.Id==renderId?"render":"other")+" name="+name
                        +" cpu_ms="+r.Item2.ToString("F6",Inv)+" cpu_per_frame_ms="+(r.Item2/Math.Max(1,frames)).ToString("F6",Inv)+" span_s="+r.Item3.ToString("F6",Inv));
                }
            }
            catch(Exception e) {log("threadcpu window="+index+" status=UNMEASURED reason="+e.GetType().Name);}
            finally {Dispose();}
        }
        public void Dispose()
        {
            foreach(var s in samples)if(s.Handle!=IntPtr.Zero) {CloseHandle(s.Handle);s.Handle=IntPtr.Zero;}
            samples.Clear();
        }
    }
}
#endif
