using System;
using System.Collections.Generic;

namespace StutterFix
{
    internal sealed class CompiledTagTable<T>
    {
        private readonly Dictionary<string,int> ids;
        internal readonly List<T>[] Rows;
        internal CompiledTagTable(Dictionary<string,List<T>> source)
        {
            ids=new Dictionary<string,int>(source.Comparer);Rows=new List<T>[source.Count];
            int i=0;foreach(var kv in source){ids.Add(kv.Key,i);Rows[i++]=kv.Value;}
        }
        internal int[] Compile(List<string> tags)
        {
            int[] plan=new int[tags.Count];
            for(int i=0;i<plan.Length;i++)
            {
                if(tags[i]==null)throw new ArgumentException("Null tag uses original path");
                int id;plan[i]=ids.TryGetValue(tags[i],out id)?id:-1;
            }
            return plan;
        }
    }
}
