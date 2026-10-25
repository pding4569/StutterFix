#if DEV || AUTOTEST || DRAWCOLLAPSE_CHECK
using System;
using System.Collections.Generic;

namespace StutterFix
{
    // Pure painter-order and memory arithmetic shared by preparation and checks.
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
        // Renderer stores the source item index for owned rows. Never regroup
        // A/B/A textures or move a quad across even a currently hidden barrier.
        internal static List<int[]> Plan(List<Row> rows)
        {
            rows.Sort((a,b)=>a.Key.CompareTo(b.Key));
            for(int i=1;i<rows.Count;i++)
                if(rows[i].Key.SameMaterialTie(rows[i-1].Key) && (rows[i].Owned || rows[i-1].Owned))
                    throw new InvalidOperationException("drawcollapse ambiguous material tie");
            var spans=new List<int[]>();var items=new List<int>();
            foreach(var row in rows)
            {
                if(row.Owned){items.Add(row.Renderer);continue;}
                if(items.Count>0){spans.Add(items.ToArray());items.Clear();}
            }
            if(items.Count>0)spans.Add(items.ToArray());
            return spans;
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
        internal static int SmallDxtTier(int width,int height,bool dxt5,int mips)
        {
            if(width<=0 || height<=0)throw new ArgumentOutOfRangeException();
            if(!dxt5 || mips!=1 || width%4!=0 || height%4!=0)return 0;
            foreach(int size in new[]{512,1024,2048})if(width<=size && height<=size)return size;
            return 0; // Borrow the original 2D texture; never decompress or resample it.
        }
        internal static string SpanDecision(int spans)
        {
            if(spans<0)throw new ArgumentOutOfRangeException(nameof(spans));
            return spans<=150?"eligible":spans>400?"too_many_barriers":"hold_151_to_400";
        }
    }
    internal sealed class DrawCollapseGatePolicy
    {
        internal readonly bool[] Windows=new bool[4],Pixels=new bool[3],Restores=new bool[4];
        internal readonly double[] Fps=new double[4];
        internal bool Failed;
        internal void Window(int i,double fps,int draws,int spikes,bool valid)
        {
            if(i<0 || i>3 || Windows[i] || double.IsNaN(fps) || double.IsInfinity(fps) || fps<=0
                || draws<=0 || spikes<0 || !valid){Failed=true;return;}
            Windows[i]=true;Fps[i]=fps;
            if((i==1 || i==2) && (fps<220 || draws>300 || spikes>0))Failed=true;
        }
        internal void Pixel(int i,int maximum,bool stable)
        {
            if(i<0 || i>2 || Pixels[i] || maximum<0 || maximum>255){Failed=true;return;}
            Pixels[i]=true;if(maximum>3 || !stable)Failed=true;
        }
        internal void Restore(int i,bool valid)
        {
            if(i<0 || i>3 || Restores[i]){Failed=true;return;}
            Restores[i]=true;if(!valid)Failed=true;
        }
        internal bool Complete=>Array.TrueForAll(Windows,x=>x) && Array.TrueForAll(Pixels,x=>x) && Array.TrueForAll(Restores,x=>x);
        internal bool Passed=>Complete && !Failed;
    }
}
#endif
