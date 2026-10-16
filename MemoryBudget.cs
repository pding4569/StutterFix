using System;
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
                var m=new MemoryStatus { Length=(uint)Marshal.SizeOf<MemoryStatus>() };
                var p=new ProcessMemory { Size=(uint)Marshal.SizeOf<ProcessMemory>() };
                if(!GlobalMemoryStatusEx(ref m)||!K32GetProcessMemoryInfo(GetCurrentProcess(),ref p,p.Size))
                    throw new InvalidOperationException("Memory snapshot unavailable");
                cached=MemoryBudgetPolicy.Calculate(fallback,(long)(m.TotalPhysical>>20),(long)(m.AvailablePhysical>>20),
                    (long)(p.WorkingSet.ToUInt64()>>20),heap);
                return cached;
            }
            catch(Exception ex)
            {
                failed=true;
                try { Main.Entry.Logger.Log("[GC] 동적 메모리 한도 실패: 기존 한도 사용 ("+ex.Message+")"); }catch{}
                return legacy;
            }
        }
        internal static void Reset(){next=0;cached=0;failed=false;}
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
