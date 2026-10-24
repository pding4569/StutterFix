#if DEV || AUTOTEST
using System;
using System.Diagnostics;
using System.Globalization;

namespace StutterFix
{
    // Explicit diagnostic only. No per-decoration clock reads or file IO.
    // CPU intervals include any engine/API waits; none is GPU elapsed time.
    internal static class GpuDecoCostProbe
    {
        internal static bool Enabled;
        static long frames,advance,dirty,source,calls,bytes,alpha,chunkSum,chunkMax,sourceSlots;
        static long removals,missingRemovals,searchUpper,shiftedInts,largestList;
        internal static void Reset()
        {
            frames=advance=dirty=source=calls=bytes=alpha=chunkSum=chunkMax=sourceSlots=0;
            removals=missingRemovals=searchUpper=shiftedInts=largestList=0;
        }
        // Work counts only: IndexOf may vectorize, so this is not a measured
        // comparison count or CPU elapsed time. No per-target clock reads.
        internal static void SourceRemoval(int index,int count)
        {
            removals++;largestList=Math.Max(largestList,count);
            searchUpper+=index<0?count:index+1;
            if(index<0)missingRemovals++;
            else shiftedInts+=count-index-1;
        }
        internal static void Upload(int count,int stride) {calls++;bytes+=(long)count*stride;}
        internal static void AlphaToggle() {alpha++;}
        internal static void Frame(long a,long d,long s,int slots,ulong low,ulong high)
        {
            frames++;advance+=a;dirty+=d;source+=s;sourceSlots+=slots;
            int chunks=0;
            while(low!=0){low&=low-1;chunks++;}while(high!=0){high&=high-1;chunks++;}
            chunkSum+=chunks;chunkMax=Math.Max(chunkMax,chunks);
        }
        internal static string Report()
        {
            double n=Math.Max(1,frames),factor=1000.0/Stopwatch.Frequency/n;
            var c=CultureInfo.InvariantCulture;
            return "cost_probe enabled="+Enabled+" frames="+frames+" cpu_only=True"
                +" advance_ms="+(advance*factor).ToString("F6",c)
                +" dirty_alpha_ms="+(dirty*factor).ToString("F6",c)
                +" source_upload_ms="+(source*factor).ToString("F6",c)
                +" dirty_calls_avg="+(calls/n).ToString("F6",c)
                +" dirty_bytes_avg="+(bytes/n).ToString("F3",c)
                +" source_slots_avg="+(sourceSlots/n).ToString("F3",c)
                +" renderer_toggles_avg="+(alpha/n).ToString("F6",c)
                +" active_chunks_avg="+(chunkSum/n).ToString("F6",c)+" active_chunks_max="+chunkMax
                +" source_removals="+removals+" missing_removals="+missingRemovals
                +" source_search_element_upper_bound="+searchUpper+" source_shifted_ints="+shiftedInts
                +" source_max_list="+largestList+" removal_work_is_CPU_timing=False"
                +" additional_clock_reads_per_frame=2 gpu_time=UNMEASURED";
        }
    }
}
#endif
