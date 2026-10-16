using System;
using System.Runtime.InteropServices;
namespace StutterFix
{
    // A real duplicated thread handle remains tied to this thread object even
    // after a TID is reused. No MMCSS: its revert must run on the registering thread.
    internal sealed class RenderThreadLease
    {
        [StructLayout(LayoutKind.Sequential)]private struct Affinity{internal UIntPtr Mask;internal ushort Group,R0,R1,R2;}
        [DllImport("kernel32.dll",SetLastError=true)]private static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll")]private static extern uint WaitForSingleObject(IntPtr h,uint ms);
        [DllImport("kernel32.dll",SetLastError=true)]private static extern int GetThreadPriority(IntPtr h);
        [DllImport("kernel32.dll",SetLastError=true)]private static extern bool SetThreadPriority(IntPtr h,int p);
        [DllImport("kernel32.dll",SetLastError=true)]private static extern bool GetThreadGroupAffinity(IntPtr h,out Affinity a);
        [DllImport("kernel32.dll",SetLastError=true)]private static extern UIntPtr SetThreadAffinityMask(IntPtr h,UIntPtr mask);
        private IntPtr handle;
        private bool priorityOwned;
        private int beforePriority;
        private ulong beforeMask,ownMask;
        internal bool Owned=>handle!=IntPtr.Zero;
#if DEV || AUTOTEST
        [DllImport("kernel32.dll")]private static extern uint GetThreadId(IntPtr h);
        internal string LastAfterForFixture="";
        internal string SnapshotForFixture()
        {
            if(!Owned)return "closed";
            Affinity a;return "tid="+GetThreadId(handle)+" priority="+GetThreadPriority(handle)+" mask="+(GetThreadGroupAffinity(handle,out a)?a.Mask.ToUInt64().ToString("X"):"unknown");
        }
#endif
        internal RenderThreadLease(IntPtr ownedHandle)
        {if(ownedHandle==IntPtr.Zero)throw new ArgumentException("Owned thread handle required");handle=ownedHandle;}
        internal bool Apply(bool priority,ulong mask)
        {
            if(!Owned||priorityOwned||ownMask!=0)return false;
            try
            {
                if(WaitForSingleObject(handle,0)!=258)return false;
                if(priority)
                {
                    beforePriority=GetThreadPriority(handle);
                    if(beforePriority==int.MaxValue||!SetThreadPriority(handle,15))return false;
                    priorityOwned=true;
                }
                if(mask!=0)
                {
                    Affinity before;
                    if(!GetThreadGroupAffinity(handle,out before)||before.Group!=0||(mask&before.Mask.ToUInt64())!=mask)return false;
                    var previous=SetThreadAffinityMask(handle,new UIntPtr(mask));
                    if(previous==UIntPtr.Zero)return false;
                    beforeMask=previous.ToUInt64();ownMask=mask;
                }
                return true;
            }
            catch{return false;}
        }
        internal bool Restore()
        {
            if(!Owned)return true;
            try
            {
                uint state=WaitForSingleObject(handle,0);
                if(state==0){priorityOwned=false;ownMask=0;} // exited thread: no live settings to restore
                else if(state!=258)return false;
                if(ownMask!=0)
                {
                    Affinity current;
                    if(!GetThreadGroupAffinity(handle,out current))return false;
                    if(current.Group==0&&current.Mask.ToUInt64()==ownMask&&SetThreadAffinityMask(handle,new UIntPtr(beforeMask))==UIntPtr.Zero)return false;
                    ownMask=beforeMask=0; // preserve a different mask installed by another owner
                }
                if(priorityOwned)
                {
                    int current=GetThreadPriority(handle);
                    if(current==int.MaxValue)return false;
                    if(current==15&&!SetThreadPriority(handle,beforePriority))return false;
                    priorityOwned=false;
                }
#if DEV || AUTOTEST
                LastAfterForFixture=state==0?"exited":SnapshotForFixture();
#endif
                if(!CloseHandle(handle))return false;
                handle=IntPtr.Zero;return true;
            }
            catch{return false;}
        }
    }
}
