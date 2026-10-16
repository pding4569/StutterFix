using System;
namespace StutterFix
{
    internal struct WorkingSetBounds
    {
        internal ulong Minimum,Maximum;internal uint Flags;
        internal bool Same(WorkingSetBounds other)=>Minimum==other.Minimum&&Maximum==other.Maximum&&Flags==other.Flags;
    }
    internal interface IPageLockApi
    {
        ulong AvailableBytes{get;}
        bool GetWorkingSet(out WorkingSetBounds bounds);
        bool SetWorkingSet(WorkingSetBounds bounds);
        IntPtr Allocate(int bytes);
        bool Free(IntPtr pointer);
        bool Lock(IntPtr pointer,int bytes);
        bool Unlock(IntPtr pointer,int bytes);
    }
    internal sealed class PageLockLease:IDisposable
    {
        private readonly IPageLockApi api;
        private WorkingSetBounds original,applied;
        private bool quotaChanged,locked;
        private int bytes;
        internal IntPtr Pointer{get;private set;}
        internal bool Released=>Pointer==IntPtr.Zero&&!quotaChanged;
        internal string Status{get;private set;}="not allocated";
        internal string OriginalSnapshot=>original.Minimum+"/"+original.Maximum+"/"+original.Flags;
        private PageLockLease(IPageLockApi api){this.api=api;}
        internal static bool Plan(WorkingSetBounds old,int bytes,ulong available,out WorkingSetBounds result)
        {
            result=old;
            if(bytes<=0||bytes>4*1024*1024||available<512UL*1024*1024+(ulong)bytes*4||old.Minimum==0||old.Minimum>1UL<<48||old.Maximum<old.Minimum||(old.Flags&~5u)!=0)return false;
            ulong minimum=old.Minimum+(ulong)bytes+65536;
            if((old.Flags&4)!=0&&minimum>old.Maximum)return false;
            result.Minimum=minimum;result.Maximum=Math.Max(old.Maximum,minimum+65536);result.Flags=old.Flags|1u;return true;
        }
        // Always returns the owner so even a partial failure can retain cleanup state.
        internal static PageLockLease Create(IPageLockApi api,int bytes)
        {
            var lease=new PageLockLease(api);lease.bytes=bytes;
            try
            {
                if(!api.GetWorkingSet(out lease.original)){lease.Status="working set query unavailable";return lease;}
                if(!Plan(lease.original,bytes,api.AvailableBytes,out lease.applied)){lease.Status="budget or working set limits";return lease;}
                if(!api.SetWorkingSet(lease.applied)){lease.Status="working set request rejected";return lease;}
                lease.quotaChanged=true;
                // Read back the real, possibly rounded result; refuse an unowned mismatch.
                WorkingSetBounds observed;
                lease.Status="working set readback mismatch";
                if(!api.GetWorkingSet(out observed)||!observed.Same(lease.applied))throw new InvalidOperationException("Working set readback mismatch");
                lease.Status="private page allocation rejected";
                lease.Pointer=api.Allocate(bytes);
                if(lease.Pointer==IntPtr.Zero)throw new OutOfMemoryException();
                lease.Status="page locking rejected";
                if(!api.Lock(lease.Pointer,bytes))throw new InvalidOperationException("Page locking rejected");
                lease.locked=true;lease.Status="locked bytes="+bytes;return lease;
            }
            catch{string reason=lease.Status;lease.Dispose();lease.Status=reason+(lease.Released?"":"; "+lease.Status);return lease;}
        }
        public void Dispose()
        {
            try
            {
                if(Pointer!=IntPtr.Zero)
                {
                    if(locked&&api.Unlock(Pointer,bytes))locked=false;
                    if(!api.Free(Pointer)){Status="owned page release pending";return;}
                    Pointer=IntPtr.Zero;
                    locked=false;
                }
                if(quotaChanged)
                {
                    WorkingSetBounds current;
                    if(!api.GetWorkingSet(out current)){Status="working set restore pending";return;}
                    if(current.Same(applied)&&!api.SetWorkingSet(original)){Status="working set restore rejected";return;}
                    quotaChanged=false;
                }
                Status="released";
            }
            catch{Status="owned page cleanup pending";}
        }
    }
}
