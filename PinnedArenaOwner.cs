using System;
using System.Runtime.InteropServices;
namespace StutterFix
{
    internal static class PinnedArenaOwner
    {
        private static readonly object gate=new object();
        private static PageLockLease lease;
        internal static string Status="off";
        internal static string OriginalSnapshot="unknown";
        internal static IntPtr Allocate(int bytes)
        {
            lock(gate)
            {
                if(lease!=null)return IntPtr.Zero;
                lease=PageLockLease.Create(new WindowsPages(),bytes);Status=lease.Status;OriginalSnapshot=lease.OriginalSnapshot;
                if(!Status.StartsWith("locked",StringComparison.Ordinal))
                {string reason=Status;lease.Dispose();Status=reason+(lease.Released?"; fallback":"; "+lease.Status);if(lease.Released)lease=null;return IntPtr.Zero;}
                return lease.Pointer;
            }
        }
        internal static bool Owns(IntPtr pointer){lock(gate)return lease!=null&&lease.Pointer==pointer&&pointer!=IntPtr.Zero;}
        internal static void Release(IntPtr pointer)
        {
            lock(gate)
            {
                if(lease==null||lease.Pointer!=pointer)return;
                lease.Dispose();Status=lease.Status;if(lease.Released)lease=null;
            }
        }
        internal static void RetryCleanup()
        {
            lock(gate){if(lease!=null){lease.Dispose();Status=lease.Status;if(lease.Released)lease=null;}}
        }
        internal static string WorkingSetSnapshot()
        {try{WorkingSetBounds b;return new WindowsPages().GetWorkingSet(out b)?b.Minimum+"/"+b.Maximum+"/"+b.Flags:"unknown";}catch{return "unknown";}}
        private sealed class WindowsPages:IPageLockApi
        {
            [StructLayout(LayoutKind.Sequential)]private struct MemoryStatus{internal uint Length,Load;internal ulong Total,Available,PageTotal,PageAvailable,VirtualTotal,VirtualAvailable,Extended;}
            [DllImport("kernel32.dll")]private static extern IntPtr GetCurrentProcess();
            [DllImport("kernel32.dll",SetLastError=true)]private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
            [DllImport("kernel32.dll",SetLastError=true)]private static extern bool GetProcessWorkingSetSizeEx(IntPtr process,out UIntPtr min,out UIntPtr max,out uint flags);
            [DllImport("kernel32.dll",SetLastError=true)]private static extern bool SetProcessWorkingSetSizeEx(IntPtr process,UIntPtr min,UIntPtr max,uint flags);
            [DllImport("kernel32.dll",SetLastError=true)]private static extern IntPtr VirtualAlloc(IntPtr address,UIntPtr size,uint allocation,uint protect);
            [DllImport("kernel32.dll",SetLastError=true)]private static extern bool VirtualFree(IntPtr address,UIntPtr size,uint kind);
            [DllImport("kernel32.dll",SetLastError=true)]private static extern bool VirtualLock(IntPtr address,UIntPtr size);
            [DllImport("kernel32.dll",SetLastError=true)]private static extern bool VirtualUnlock(IntPtr address,UIntPtr size);
            public ulong AvailableBytes{get{var info=new MemoryStatus{Length=(uint)Marshal.SizeOf(typeof(MemoryStatus))};return GlobalMemoryStatusEx(ref info)?info.Available:0;}}
            public bool GetWorkingSet(out WorkingSetBounds bounds)
            {
                UIntPtr min,max;uint flags;bounds=default;
                if(!GetProcessWorkingSetSizeEx(GetCurrentProcess(),out min,out max,out flags)||(flags&~15u)!=0)return false;
                bounds=new WorkingSetBounds{Minimum=min.ToUInt64(),Maximum=max.ToUInt64(),Flags=flags&5u};return true;
            }
            public bool SetWorkingSet(WorkingSetBounds b)
            {
                uint flags=((b.Flags&1)!=0?1u:2u)|((b.Flags&4)!=0?4u:8u);
                return SetProcessWorkingSetSizeEx(GetCurrentProcess(),new UIntPtr(b.Minimum),new UIntPtr(b.Maximum),flags);
            }
            public IntPtr Allocate(int bytes)=>VirtualAlloc(IntPtr.Zero,new UIntPtr((uint)bytes),0x3000,4);
            public bool Free(IntPtr p)=>VirtualFree(p,UIntPtr.Zero,0x8000);
            public bool Lock(IntPtr p,int bytes)=>VirtualLock(p,new UIntPtr((uint)bytes));
            public bool Unlock(IntPtr p,int bytes)=>VirtualUnlock(p,new UIntPtr((uint)bytes));
        }
    }
}
