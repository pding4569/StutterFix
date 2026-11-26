using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace StutterFix
{
    internal static class MemoryBudget
    {
        internal static bool Enabled;
        private static bool failed;
        private static float next;
        private static int cached;
        internal static bool Active => Enabled && !failed && cached>0;
        internal static int Limit(int fallback,int total,long heap)
        {
            int legacy=MemoryBudgetPolicy.Legacy(fallback,total);
            if(!Enabled) { failed=false;next=0;return legacy; }
            if(failed)return legacy;
            float now=Time.realtimeSinceStartup;
            if(now<next&&cached>0)return cached;
            next=now+1f;
            try
            {
                long totalMB,availMB,workingSetMB;
                if(!TryReadMemoryMB(out totalMB,out availMB,out workingSetMB))
                    throw new InvalidOperationException("Memory snapshot unavailable");
                cached=MemoryBudgetPolicy.Calculate(fallback,totalMB,availMB,workingSetMB,heap);
                return cached;
            }
            catch(Exception ex)
            {
                failed=true;
                try { Lg.I("[GC] 동적 메모리 한도 실패: 기존 한도 사용 ("+ex.Message+")"); }catch{}
                return legacy;
            }
        }
        internal static void Reset(){next=0;cached=0;failed=false;}

        private static bool TryReadMemoryMB(out long totalMB,out long availMB,out long workingSetMB)
        {
            totalMB=0;availMB=0;workingSetMB=0;
            if(Platform.IsWindows)
            {
                var m=new MemoryStatus { Length=(uint)Marshal.SizeOf<MemoryStatus>() };
                var p=new ProcessMemory { Size=(uint)Marshal.SizeOf<ProcessMemory>() };
                if(!GlobalMemoryStatusEx(ref m)||!K32GetProcessMemoryInfo(GetCurrentProcess(),ref p,p.Size))
                    return false;
                totalMB=(long)(m.TotalPhysical>>20);
                availMB=(long)(m.AvailablePhysical>>20);
                workingSetMB=(long)(p.WorkingSet.ToUInt64()>>20);
                return true;
            }
            if(Platform.IsLinux)
            {
                long totalKb,availKb;
                if(!TryReadLinuxMemInfoKb(out totalKb,out availKb)) return false;
                long rssBytes,rssKb;
                if(TryReadLinuxStatmRssBytes(out rssBytes)) workingSetMB=rssBytes>>20;
                else if(TryReadLinuxVmRssKb(out rssKb)) workingSetMB=rssKb>>10;
                else return false;
                totalMB=totalKb>>10;
                availMB=availKb>>10;
                return true;
            }
            return false;
        }

        internal static bool TryReadLinuxMemInfoKb(out long totalKb,out long availKb)
        {
            totalKb=0;availKb=0;
            try
            {
                long total=-1,avail=-1,free=-1,buffers=0,cachedMem=0;
                using(var r=new StreamReader("/proc/meminfo"))
                {
                    string line;
                    while((line=r.ReadLine())!=null)
                    {
                        long v;
                        if(total<0&&TryParseProcKb(line,"MemTotal:",out v)) total=v;
                        else if(avail<0&&TryParseProcKb(line,"MemAvailable:",out v)) avail=v;
                        else if(free<0&&TryParseProcKb(line,"MemFree:",out v)) free=v;
                        else if(TryParseProcKb(line,"Buffers:",out v)) buffers=v;
                        else if(TryParseProcKb(line,"Cached:",out v)) cachedMem=v;
                        if(total>=0&&avail>=0) break;
                    }
                }
                if(total<=0) return false;
                if(avail<0)
                {
                    if(free<0) return false;
                    avail=free+buffers+cachedMem;
                }
                if(avail>total) avail=total;
                if(avail<0) avail=0;
                totalKb=total;
                availKb=avail;
                return true;
            }
            catch { return false; }
        }

        internal static bool TryReadLinuxVmRssKb(out long rssKb)
        {
            rssKb=0;
            try
            {
                using(var r=new StreamReader("/proc/self/status"))
                {
                    string line;
                    while((line=r.ReadLine())!=null)
                    {
                        if(TryParseProcKb(line,"VmRSS:",out rssKb)) return rssKb>=0;
                    }
                }
            }
            catch { }
            return false;
        }

        private static bool TryReadLinuxStatmRssBytes(out long rssBytes)
        {
            rssBytes=0;
            try
            {
                string text=File.ReadAllText("/proc/self/statm");
                int i=0,n=text.Length;
                while(i<n&&text[i]<=' ') i++;
                while(i<n&&text[i]>' ') i++;
                while(i<n&&text[i]<=' ') i++;
                int start=i;
                while(i<n&&text[i]>='0'&&text[i]<='9') i++;
                long pages;
                if(i>start&&long.TryParse(text.Substring(start,i-start),NumberStyles.None,CultureInfo.InvariantCulture,out pages)&&pages>=0)
                {
                    long pageSize=Environment.SystemPageSize;
                    if(pageSize>0) { rssBytes=pages*pageSize; return true; }
                }
            }
            catch { }
            return false;
        }

        private static bool TryParseProcKb(string line,string prefix,out long kb)
        {
            kb=0;
            if(!line.StartsWith(prefix,StringComparison.Ordinal)) return false;
            int i=prefix.Length,n=line.Length;
            while(i<n&&(line[i]==' '||line[i]=='\t')) i++;
            int start=i;
            while(i<n&&line[i]>='0'&&line[i]<='9') i++;
            return i>start&&long.TryParse(line.Substring(start,i-start),NumberStyles.None,CultureInfo.InvariantCulture,out kb);
        }
        [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus
        {
            public uint Length,Load;
            public ulong TotalPhysical,AvailablePhysical,TotalPageFile,AvailablePageFile,TotalVirtual,AvailableVirtual,Extended;
        }
        [StructLayout(LayoutKind.Sequential)] private struct ProcessMemory
        {
            public uint Size,PageFaultCount;
            public UIntPtr PeakWorkingSet,WorkingSet,PeakPagedPool,PagedPool,PeakNonPagedPool,NonPagedPool,Pagefile,PeakPagefile,Private;
        }
        [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus m);
        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool K32GetProcessMemoryInfo(IntPtr process,ref ProcessMemory p,uint size);
    }
}
