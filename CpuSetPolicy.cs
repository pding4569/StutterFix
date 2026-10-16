using System;
namespace StutterFix
{
    internal static class CpuSetPolicy
    {
        internal static ulong SchedulingMask(byte[] data,ulong allowed,bool performanceCores,bool avoidLogicalZero,out bool hybrid)
        {
            hybrid=false;
            if(!performanceCores&&!avoidLogicalZero)return 0;
            ulong mask=performanceCores?PerformanceMask(data,allowed,out hybrid):allowed;
            // This is an explicit scheduling experiment, not an assertion that
            // Windows routes interrupts to CPU 0 on this machine.
            if(avoidLogicalZero)mask&=~1UL;
            return mask;
        }
        // Win32 CPU_SET_INFORMATION has a size-prefixed, extensible record layout.
        internal static ulong PerformanceMask(byte[] data,ulong allowed,out bool hybrid)
        {
            hybrid=false;if(data==null||data.Length>1024*1024)return 0;
            int min=256,max=-1;ulong mask=0;
            for(int at=0;at<data.Length;)
            {
                if(data.Length-at<8)return 0;
                uint size=BitConverter.ToUInt32(data,at);
                if(size<8||size>data.Length-at)return 0;
                if(BitConverter.ToUInt32(data,at+4)==0)
                {
                    if(size<32||BitConverter.ToUInt16(data,at+12)!=0)return 0;
                    int index=data[at+14],efficiency=data[at+18],flags=data[at+19];
                    if(index>=64)return 0;
                    min=Math.Min(min,efficiency);
                    if(efficiency>max){max=efficiency;mask=0;}
                    bool unavailable=(flags&1)!=0||((flags&2)!=0&&(flags&4)==0);
                    if(efficiency==max&&!unavailable)mask|=1UL<<index;
                }
                at+=(int)size;
            }
            hybrid=max>=0&&min<max;return hybrid?mask&allowed:0;
        }
    }
}
