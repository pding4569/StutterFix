#if DEV || AUTOTEST || GPUDECO || DRAWCOLLAPSE_CHECK
using System;
using System.Collections.Generic;

namespace StutterFix
{
    // Immutable capacity proof: static textures share identical content, while
    // every image-changing item owns a distinct slice. No time prediction,
    // eviction, visible-only assumption or per-frame scan is involved.
    internal sealed class ImageArrayLeasePlan
    {
        internal readonly Dictionary<int,int> StaticSlots=new Dictionary<int,int>();
        internal readonly Dictionary<int,int> ActorSlots=new Dictionary<int,int>();
        readonly Dictionary<int,HashSet<int>> allowed=new Dictionary<int,HashSet<int>>();
        readonly int[] initial;
        internal int Slices { get; private set; }
        internal ImageArrayLeasePlan(int[] group,int[] initialTextures,int[][] variants)
        {
            if(group==null || initialTextures==null || variants==null || initialTextures.Length!=variants.Length)
                throw new ArgumentException("image array plan shape");
            initial=(int[])initialTextures.Clone();
            var ids=new HashSet<int>();
            foreach(int id in group)if(id==0 || !ids.Add(id))throw new ArgumentException("duplicate/zero texture");
            var staticIds=new HashSet<int>();
            for(int item=0;item<initial.Length;item++)
            {
                var options=variants[item];
                if(options==null)throw new ArgumentException("unknown image variants");
                if(options.Length==0)
                {if(ids.Contains(initial[item]))staticIds.Add(initial[item]);continue;}
                var set=new HashSet<int>();
                if(ids.Contains(initial[item]))set.Add(initial[item]);
                foreach(int id in options)if(ids.Contains(id))set.Add(id);
                if(set.Count>0)allowed.Add(item,set);
            }
            foreach(int id in group)if(staticIds.Contains(id))StaticSlots.Add(id,Slices++);
            for(int item=0;item<initial.Length;item++)if(allowed.ContainsKey(item))ActorSlots.Add(item,Slices++);
        }
        internal int Resolve(int item,int texture)
        {
            if((uint)item>=(uint)initial.Length)throw new ArgumentOutOfRangeException(nameof(item));
            if(ActorSlots.TryGetValue(item,out int slot))
            {
                if(!allowed[item].Contains(texture))throw new InvalidOperationException("unplanned actor image");
                return slot;
            }
            if(initial[item]!=texture || !StaticSlots.TryGetValue(texture,out slot))
                throw new InvalidOperationException("unplanned static image");
            return slot;
        }
    }
}
#endif
