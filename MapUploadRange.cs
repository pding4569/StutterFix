#if AUTOTEST || DRAWCOLLAPSE_CHECK
using System;
namespace StutterFix
{
    // Describes the smallest contiguous range containing all pending writes.
    // Values remain in the authoritative CPU array; repeated writes retain order.
    internal struct MapUploadRange
    {
        internal bool Pending { get; private set; }
        internal int First { get; private set; }
        int last;
        internal int Count=>Pending?last-First+1:0;
        internal void Add(int slot,int length)
        {
            if((uint)slot>=(uint)length)throw new ArgumentOutOfRangeException(nameof(slot));
            if(!Pending){First=last=slot;Pending=true;}
            else{if(slot<First)First=slot;if(slot>last)last=slot;}
        }
        internal void Clear(){this=default;}
    }
}
#endif
