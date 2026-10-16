using System;
using System.Collections.Generic;
namespace StutterFix
{
    internal sealed class HitchHistogram
    {
        internal struct Entry {internal int Floor,Count;internal double Total,Worst;}
        private readonly Dictionary<int,Entry> entries=new Dictionary<int,Entry>();
        internal int Missing,Overflow;
        internal void Add(int floor,double ms)
        {
            if(double.IsNaN(ms)||double.IsInfinity(ms)||ms<=0)return;
            if(floor<0){Missing++;return;}
            Entry entry;
            if(!entries.TryGetValue(floor,out entry))
            {if(entries.Count>=1024){Overflow++;return;}entry.Floor=floor;}
            entry.Count++;entry.Total+=ms;entry.Worst=Math.Max(entry.Worst,ms);entries[floor]=entry;
        }
        internal Entry[] Top()
        {
            var result=new List<Entry>(entries.Values);
            result.Sort((a,b)=>{int c=b.Worst.CompareTo(a.Worst);return c!=0?c:a.Floor.CompareTo(b.Floor);});
            if(result.Count>5)result.RemoveRange(5,result.Count-5);return result.ToArray();
        }
        internal void Clear(){entries.Clear();Missing=Overflow=0;}
    }
}
