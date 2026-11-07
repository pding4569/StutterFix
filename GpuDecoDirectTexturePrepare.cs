#if DEV || AUTOTEST || GPUDECO
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        // Run before any dense ownership/slice/texture tables are built. Count
        // decorations, not event occurrences: one actor may repeat a selector.
        static void DemoteDirectTextureOverflowRows(List<GpuLiveRow> rows,Action<int,string> rejected)
        {
            var dependencies=new Dictionary<int,HashSet<int>>();
            var textures=new Dictionary<int,Texture2D>();
            var firstUse=new Dictionary<int,(int Owner,int Selector)>();
            void Add(int owner,Texture2D texture,int selector=-1)
            {
                if(!texture || CollapseSize(texture)!=0)return;
                int id=texture.GetInstanceID();
                dependencies[owner].Add(id);textures[id]=texture;
                if(!firstUse.TryGetValue(id,out var prior) || owner<prior.Owner || owner==prior.Owner && selector<prior.Selector)
                    firstUse[id]=(owner,selector);
            }
            foreach(var row in rows)
            {
                if(row.Item<0)continue;
                if(!dependencies.ContainsKey(row.Item))dependencies.Add(row.Item,new HashSet<int>());
                Add(row.Item,(row.R as SpriteRenderer)?.sprite?.texture);
            }
            if(gpuImageCandidate && gpuImageOwn && gpuImageVariants!=null)
            {
                RequireLive(gpuImageTimeline!=null && gpuImageTimeline.SelectorCount==gpuImageVariants.Length,
                    "image collapse timeline missing");
                void AddVariant(int owner,int selector)
                {
                    var v=gpuImageVariants[selector];
                    if(v.Status!=GpuDecoImageVariant.Kind.ReadyQuad)return;
                    var sprite=v.Source?.sprite;var t=sprite?sprite.texture:null;
                    RequireLive(t && t.mipmapCount==1,"image collapse variant texture/mips unsupported");
                    Add(owner,t,selector);
                }
                // Mirror RegisterGpuImageCollapseVariants: all song variants in
                // Player; window events plus each actor's entry state in the lab.
                double start=GpuDecoPlayer.Product?double.MinValue:GpuImageCollapseStart;
                double end=GpuDecoPlayer.Product?double.MaxValue:GpuImageCollapseEnd;
                foreach(int owner in dependencies.Keys)
                {
                    int prior=-1;
                    foreach(int e in gpuImageTimeline.EventsForDecoration(owner))
                    {
                        var ev=gpuImageTimeline.Event(e);
                        if(ev.Scheduled>end)continue;
                        if(ev.Scheduled<start)prior=ev.Selector;
                        else AddVariant(owner,ev.Selector);
                    }
                    if(prior>=0)AddVariant(owner,prior);
                }
            }
            if(DrawCollapsePolicy.ExactBigArray)
            {
                var bigGrps=textures.Values.Where(t=>CollapseSize(t)==0 && CollapseSupportedFormat(t) && (long)t.width*t.height<=16777216L)
                    .GroupBy(t=>t.width+"x"+t.height+"/"+t.graphicsFormat)
                    .Where(g=>g.Count()>=DrawCollapsePolicy.ExactBigMin)
                    .OrderByDescending(g=>g.Count()).ThenBy(g=>g.Key,StringComparer.Ordinal).Take(2).ToArray();
                foreach(var bigGrp in bigGrps)
                {
                    foreach(var bt in bigGrp.Take(SystemInfo.maxTextureArraySlices))
                    {
                        int bid=bt.GetInstanceID();
                        textures.Remove(bid);
                        foreach(var hs in dependencies.Values)hs.Remove(bid);
                    }
                }
            }
            if(textures.Count<=DrawCollapsePolicy.DirectBindings)return;
            var counts=new Dictionary<int,int>();
            foreach(var ids in dependencies.Values)foreach(int id in ids)
            {counts.TryGetValue(id,out int count);counts[id]=count+1;}
            // Instance IDs identify assets, but must never decide which assets
            // survive. The final (baseline index, selector) pair distinguishes
            // even equal-name/equal-size assets without enumeration order.
            var ranked=textures.Keys.OrderByDescending(id=>counts[id])
                .ThenBy(id=>textures[id].name??"",StringComparer.Ordinal)
                .ThenBy(id=>textures[id].width).ThenBy(id=>textures[id].height)
                .ThenBy(id=>firstUse[id].Owner).ThenBy(id=>firstUse[id].Selector).ToArray();
            var retained=new HashSet<int>(ranked.Take(DrawCollapsePolicy.DirectBindings));
            var cpu=new HashSet<int>(dependencies.Where(p=>p.Value.Any(id=>!retained.Contains(id))).Select(p=>p.Key));
            foreach(var row in rows)
            {
                if(row.Item<0 || !cpu.Contains(row.Item))continue;
                rejected?.Invoke(row.Item,"direct-texture-overflow");row.Item=-1;
            }
            foreach(var row in rows)if(cpu.Contains(row.AuxiliaryOwner))row.AuxiliaryOwner=-1;
            var samples=ranked.Skip(DrawCollapsePolicy.DirectBindings).Take(3)
                .Select(id=>textures[id].width+"x"+textures[id].height);
            Main.Entry.Logger.Log("[GPU 장식] 직접 텍스처 한도 초과 정리: 필요 "+textures.Count
                +" > 한도 "+DrawCollapsePolicy.DirectBindings+", 내린 텍스처 "+(textures.Count-retained.Count)
                +"장, 내린 장식 "+cpu.Count+"개, 내린 텍스처 상위 3개 크기: "+string.Join(" | ",samples));
        }
    }
}
#endif
