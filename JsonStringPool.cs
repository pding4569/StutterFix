using System;
using System.Buffers;

namespace StutterFix
{
    // Parse-scoped, bounded, collision-checked; never process-wide String.Intern.
    // Returned DOM owns its strings. Only scratch lookup arrays return to pools.
    internal sealed class JsonStringPool : IDisposable
    {
        private int[] slots;
        private string[] strings;
        private int count;
        private const int Capacity=4096, SlotCount=8192, MaxLength=128;
        internal JsonStringPool()
        {
            try
            {
                slots=ArrayPool<int>.Shared.Rent(SlotCount);
                Array.Clear(slots,0,SlotCount);
                strings=ArrayPool<string>.Shared.Rent(Capacity);
            }
            catch { Dispose();throw; }
        }
        internal string Get(string source,int start,int length)
        {
            if(length>MaxLength||length==0||count>=Capacity)return source.Substring(start,length);
            uint hash=2166136261;
            for(int i=0;i<length;i++)hash=unchecked((hash^source[start+i])*16777619);
            int slot=(int)(hash&(SlotCount-1));
            for(int probe=0;probe<16;probe++,slot=(slot+1)&(SlotCount-1))
            {
                int n=slots[slot];
                if(n==0)
                {
                    string created=source.Substring(start,length);
                    strings[count]=created;slots[slot]=++count;return created;
                }
                string previous=strings[n-1];
                if(previous.Length==length&&string.CompareOrdinal(source,start,previous,0,length)==0)return previous;
            }
            return source.Substring(start,length);
        }
        public void Dispose()
        {
            if(strings!=null){ArrayPool<string>.Shared.Return(strings,true);strings=null;}
            if(slots!=null){ArrayPool<int>.Shared.Return(slots,true);slots=null;}
            count=0;
        }
    }
}
