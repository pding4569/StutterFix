using System;
using System.Threading;

namespace StutterFix
{
    // Primitive integers only. No Unity/managed object addresses or GCHandles.
    // Slices retain their owner; no reset/reuse, so old plans never alias new data.
    internal sealed unsafe class PrimitiveIdStore : IDisposable
    {
        private readonly int owner=Thread.CurrentThread.ManagedThreadId;
        private AlignedArena arena;
        internal int Used { get; private set; }
        internal int Capacity { get; }
        internal bool Active => arena!=null&&arena.Pointer!=IntPtr.Zero;
        internal PrimitiveIdStore(int capacity)
        {
            if(capacity<1||capacity>8*1024*1024)throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity=capacity;arena=new AlignedArena(checked(capacity*sizeof(int)));
        }
        internal bool TryAllocate(int count,out Slice slice)
        {
            slice=default;
            if(!Active||Thread.CurrentThread.ManagedThreadId!=owner||count<0||count>Capacity-Used)return false;
            slice=new Slice(this,Used,count);Used+=count;return true;
        }
        private bool TryAddress(int offset,int count,int index,out int* address)
        {
            address=null;
            if(Thread.CurrentThread.ManagedThreadId!=owner||!Active||offset<0||count<0||index<0||index>=count
                ||offset>Used||count>Used-offset)return false;
            address=(int*)arena.Pointer+offset+index;return true;
        }
        internal readonly struct Slice
        {
            private readonly PrimitiveIdStore store;
            private readonly int offset;
            internal readonly int Count;
            internal Slice(PrimitiveIdStore store,int offset,int count){this.store=store;this.offset=offset;Count=count;}
            internal bool Active=>store!=null&&store.Active;
            internal bool TryGet(int index,out int value)
            {
                value=0;int* address;
                if(store==null||!store.TryAddress(offset,Count,index,out address))return false;
                value=*address;GC.KeepAlive(store);return true;
            }
            internal bool TrySet(int index,int value)
            {
                int* address;if(store==null||!store.TryAddress(offset,Count,index,out address))return false;
                *address=value;GC.KeepAlive(store);return true;
            }
        }
        public void Dispose()
        {
            if(Thread.CurrentThread.ManagedThreadId!=owner)throw new InvalidOperationException("Primitive IDs must be disposed on their owning thread");
            var current=arena;arena=null;Used=0;current?.Dispose();
        }
    }
}
