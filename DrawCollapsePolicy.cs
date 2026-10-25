#if DEV || AUTOTEST || DRAWCOLLAPSE_CHECK
using System;
using System.Collections.Generic;

namespace StutterFix
{
    // Gate 1 arithmetic only. No mesh/texture allocation and no renderer changes.
    internal static class DrawCollapsePolicy
    {
        internal readonly struct Row
        {
            internal readonly SceneRenderOrder.Key Key;
            internal readonly bool Owned;
            internal readonly int Renderer, Texture, GameLayer;
            internal readonly string Signature;
            internal Row(SceneRenderOrder.Key key, bool owned, int renderer, int texture = 0,
                string signature = "", int gameLayer = 0)
            { Key=key;Owned=owned;Renderer=renderer;Texture=texture;Signature=signature;GameLayer=gameLayer; }
        }
        internal sealed class Counts
        {
            internal int OwnedVisible, BarrierVisible, Spans, SameTextureRuns, AmbiguousTies;
        }
        internal static Counts Count(List<Row> rows)
        {
            rows.Sort((a,b)=>a.Key.CompareTo(b.Key));
            var result=new Counts();var barriers=new HashSet<int>();
            for(int i=1;i<rows.Count;i++)
                if(rows[i].Key.SameMaterialTie(rows[i-1].Key) && (rows[i].Owned || rows[i-1].Owned))result.AmbiguousTies++;
            int first=rows.FindIndex(r=>r.Owned),last=rows.FindLastIndex(r=>r.Owned);
            bool inSpan=false;Row previous=default;
            for(int i=first;i>=0 && i<=last;i++)
            {
                var row=rows[i];
                if(!row.Owned){barriers.Add(row.Renderer);inSpan=false;continue;}
                result.OwnedVisible++;
                if(!inSpan)result.Spans++;
                if(!inSpan || row.Texture!=previous.Texture || row.Signature!=previous.Signature
                    || row.GameLayer!=previous.GameLayer || !row.Key.SameBucket(previous.Key))result.SameTextureRuns++;
                previous=row;inSpan=true;
            }
            result.BarrierVisible=barriers.Count;
            return result;
        }
        // Tight dimensions are the resolution tiers: no 2048-square padding,
        // decompression, resampling, CPU readability or guessed compression ratio.
        internal static long TextureBytes(int width,int height,int mips,int blockWidth,int blockHeight,int blockBytes)
        {
            if(width<=0 || height<=0 || mips<=0 || blockWidth<=0 || blockHeight<=0 || blockBytes<=0)
                throw new ArgumentOutOfRangeException();
            long total=0;
            checked
            {
                for(int mip=0;mip<mips;mip++)
                {
                    total+=((width+(long)blockWidth-1)/blockWidth)*((height+(long)blockHeight-1)/blockHeight)*blockBytes;
                    if(width==1 && height==1 && mip+1<mips)throw new ArgumentException("excess mip count");
                    width=Math.Max(1,width/2);height=Math.Max(1,height/2);
                }
            }
            return total;
        }
        internal static string SpanDecision(int spans)
        {
            if(spans<0)throw new ArgumentOutOfRangeException(nameof(spans));
            return spans<=150?"eligible":spans>400?"too_many_barriers":"hold_151_to_400";
        }
    }
}
#endif
