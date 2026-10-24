#if DEV || AUTOTEST || GPUDECO_FIXTURE
using System;
namespace StutterFix
{
    // Exact renderer visibility, not screen-space or scheduled-time prediction.
    // Native command chunks contain 512 independent renderer buffers each.
    internal sealed class GpuDecoCopyMask
    {
        internal const int ChunkSize=512, ItemLimit=65536;
        readonly bool[] visible;
        readonly int[] counts;
        readonly int[] copyIndex;
        internal ulong Low {get;private set;}
        internal ulong High {get;private set;}
        internal GpuDecoCopyMask(int items,int[] order=null)
        {
            if(items<0 || items>ItemLimit)throw new ArgumentOutOfRangeException(nameof(items));
            visible=new bool[items];counts=new int[(items+ChunkSize-1)/ChunkSize];
            if(order!=null)
            {
                if(order.Length!=items)throw new ArgumentException("copy order length");
                copyIndex=new int[items];var seen=new bool[items];
                for(int i=0;i<items;i++)
                {
                    int item=order[i];
                    if((uint)item>=(uint)items || seen[item])throw new ArgumentException("copy order is not a permutation");
                    seen[item]=true;copyIndex[item]=i;
                }
            }
        }
        internal void Set(int item,bool enabled)
        {
            if(visible[item]==enabled)return;
            visible[item]=enabled;int chunk=(copyIndex==null?item:copyIndex[item])/ChunkSize;
            counts[chunk]+=enabled?1:-1;
            ulong bit=1UL<<(chunk&63);
            if(chunk<64)Low=counts[chunk]>0?Low|bit:Low&~bit;
            else High=counts[chunk]>0?High|bit:High&~bit;
        }
    }
}
#endif
