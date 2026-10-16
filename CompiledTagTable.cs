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
        internal bool CompileNative(List<string> tags,PrimitiveIdStore store,out PrimitiveIdStore.Slice slice)
        {
            slice=default;if(tags==null||store==null)return false;
            foreach(var tag in tags)if(tag==null)return false;
            if(!store.TryAllocate(tags.Count,out slice))return false;
            for(int i=0;i<tags.Count;i++)
            {
                int id;if(!ids.TryGetValue(tags[i],out id))id=-1;
                if(!slice.TrySet(i,id))return false;
            }
            return true;
        }
    }
}
