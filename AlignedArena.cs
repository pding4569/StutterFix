using System;
using System.Runtime.InteropServices;
using System.Threading;
namespace StutterFix
{
    internal sealed class AlignedArena:IDisposable
    {
        private IntPtr allocation;
        private readonly Action<IntPtr> release;
        internal bool NativeAllocated{get;private set;}
        internal IntPtr Pointer{get;private set;}
        internal int Capacity{get;private set;}
        internal int Used{get;private set;}
        internal AlignedArena(int capacity,Func<int,IntPtr> nativeAllocate=null,Action<IntPtr> nativeFree=null)
        {
            if(capacity<=0||capacity>64*1024*1024)throw new ArgumentOutOfRangeException(nameof(capacity));
            if(nativeAllocate!=null&&nativeFree!=null)
            {
                try
                {
                    var p=nativeAllocate(capacity);
                    if(p!=IntPtr.Zero)
                    {
                        if((p.ToInt64()&63)==0){allocation=p;release=nativeFree;NativeAllocated=true;}
                        else nativeFree(p);
                    }
                }
                catch{}
            }
            if(allocation==IntPtr.Zero){allocation=Marshal.AllocHGlobal(checked(capacity+63));release=Marshal.FreeHGlobal;}
            if(allocation==IntPtr.Zero)throw new OutOfMemoryException();
            Pointer=new IntPtr((allocation.ToInt64()+63)&~63L);Capacity=capacity;
        }
        internal IntPtr Allocate(int bytes)
        {
            if(allocation==IntPtr.Zero)throw new ObjectDisposedException(nameof(AlignedArena));
            if(bytes<=0)throw new ArgumentOutOfRangeException(nameof(bytes));
            int start=checked((Used+63)&~63);
            if(bytes>Capacity-start)return IntPtr.Zero;
            Used=start+bytes;return IntPtr.Add(Pointer,start);
        }
        internal void Reset()
        {
            if(allocation==IntPtr.Zero)throw new ObjectDisposedException(nameof(AlignedArena));
            Used=0;
        }
        public void Dispose(){Release();GC.SuppressFinalize(this);}
        private void Release()
        {
            var address=Interlocked.Exchange(ref allocation,IntPtr.Zero);
            Pointer=IntPtr.Zero;Used=Capacity=0;
            if(address!=IntPtr.Zero)try{release(address);}catch{}
        }
        ~AlignedArena(){Release();}
    }
}
