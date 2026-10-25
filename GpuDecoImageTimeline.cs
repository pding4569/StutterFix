#if AUTOTEST || GPUDECO || GPU_IMAGE_PLAN_FIXTURE
using System;
using System.Collections.Generic;

namespace StutterFix
{
    // Pending images with static nonpositive alpha sleep until an alpha event.
    // Animated alpha stays watched across frames, including easing overshoot.
    internal sealed class GpuDecoImageWakeQueue
    {
        internal readonly HashSet<int> Pending=new HashSet<int>(),Watching=new HashSet<int>();
        internal void Reserve(int count){Pending.EnsureCapacity(count);Watching.EnsureCapacity(count);}
        internal void Clear(){Pending.Clear();Watching.Clear();}
        internal void Defer(int master){Pending.Add(master);Watching.Add(master);}
        internal void Remove(int master){Pending.Remove(master);Watching.Remove(master);}
        internal bool AlphaDirty(int master)
        {if(!Pending.Contains(master))return false;Watching.Add(master);return true;}
        internal bool ShouldFlush(int master,float alpha,bool animated)
        {
            if(!Pending.Contains(master)){Watching.Remove(master);return false;}
            if(alpha>0)return true;
            if(!animated)Watching.Remove(master);
            return false;
        }
        internal void Snapshot(List<int> work){work.Clear();work.AddRange(Watching);}
    }
    // Immutable discrete image tape, not a time-based playback predictor. The
    // eventual consumer must bind actual effect occurrences in original order.
    // Names are kept private in memory; this type performs no file or Unity IO.
    internal sealed class GpuDecoImageTimeline
    {
        internal readonly struct Input
        {
            internal readonly int Ordinal,Source,VisualFlags;
            internal readonly double Scheduled;
            internal readonly string Selector; // null=unused, empty=used empty name; setter semantics not inferred
            internal readonly int[] Targets;
            internal Input(int ordinal,int source,double scheduled,int visualFlags,string selector,int[] targets)
            {Ordinal=ordinal;Source=source;Scheduled=scheduled;VisualFlags=visualFlags;Selector=selector;Targets=targets;}
        }
        internal readonly struct Entry
        {
            internal readonly int Ordinal,Source,Selector,VisualFlags,First,Count;
            internal readonly double Scheduled;
            internal Entry(Input e,int selector,int first)
            {Ordinal=e.Ordinal;Source=e.Source;Scheduled=e.Scheduled;VisualFlags=e.VisualFlags;Selector=selector;First=first;Count=e.Targets.Length;}
        }
        readonly Entry[] entries;
        readonly string[] selectors;
        readonly int[] targets,offsets,references;
        internal int EventCount=>entries.Length;
        internal int SelectorCount=>selectors.Length;
        internal int TargetReferences=>targets.Length;
        internal int DecorationCount=>offsets.Length-1;
        internal readonly int DecorationsWithImages,EmptySelectorEvents,RepeatedSources;
        internal readonly long PayloadBytesLowerBound;
        GpuDecoImageTimeline(Entry[] e,string[] s,int[] t,int[] o,int[] r,int decorated,int clears,int repeated)
        {
            entries=e;selectors=s;targets=t;offsets=o;references=r;
            DecorationsWithImages=decorated;EmptySelectorEvents=clears;RepeatedSources=repeated;
            long chars=0;foreach(string name in s)chars+=name.Length;
            PayloadBytesLowerBound=(long)e.Length*32+(long)(t.Length+o.Length+r.Length)*4+chars*2;
        }
        internal Entry Event(int index)=>entries[index];
        internal string Selector(int index)=>selectors[index];
        internal bool HeaderMatches(int index,string selector,int visualFlags)
        {var e=entries[index];return e.VisualFlags==visualFlags&&string.Equals(selectors[e.Selector],selector,StringComparison.Ordinal);}
        internal ReadOnlySpan<int> Targets(int index)
        {var e=entries[index];return new ReadOnlySpan<int>(targets,e.First,e.Count);}
        internal ReadOnlySpan<int> EventsForDecoration(int index)
        {
            if((uint)index>=(uint)DecorationCount)throw new ArgumentOutOfRangeException(nameof(index));
            return new ReadOnlySpan<int>(references,offsets[index],offsets[index+1]-offsets[index]);
        }
        // Preparation-only texture coverage. Keep original equal-time occurrence
        // order; never predict playback by sorting the discrete tape ourselves.
        // The caller separately retains the physical sprites in its initial mesh.
        internal bool[] SelectWindowVariants(double start,double end,IReadOnlyList<int> owned)
        {
            if(double.IsNaN(start)||double.IsInfinity(start)||double.IsNaN(end)||double.IsInfinity(end)
                ||end<start||owned==null)throw new ArgumentException("invalid image texture window");
            var included=new bool[DecorationCount];
            foreach(int d in owned)
            {
                if((uint)d>=(uint)DecorationCount)throw new ArgumentException("invalid image window target");
                included[d]=true;
            }
            var selected=new bool[SelectorCount];var prior=new int[DecorationCount];
            Array.Fill(prior,-1);double previous=double.NegativeInfinity;
            for(int e=0;e<entries.Length;e++)
            {
                var row=entries[e];
                // A nonmonotone tape needs the complete scheduler's blocking
                // semantics; fail closed rather than discard a required texture.
                if(row.Scheduled<previous)throw new ArgumentException("nonmonotone image texture window schedule");
                previous=row.Scheduled;
                if(row.Scheduled>end)continue;
                foreach(int d in Targets(e))
                {
                    if(!included[d])continue;
                    if(row.Scheduled<start)prior[d]=row.Selector;
                    else selected[row.Selector]=true; // Both window boundaries included.
                }
            }
            // Each decoration can have a different image at window entry.
            // Include the predecessor even if an event occurs exactly at start.
            foreach(int selector in prior)if(selector>=0)selected[selector]=true;
            return selected;
        }
        internal static GpuDecoImageTimeline Compile(int decorations,IReadOnlyList<Input> input)
        {
            if(decorations<0||decorations>100000||input==null||input.Count>300000)throw new ArgumentException("image tape budget");
            var names=new List<string>();var ids=new Dictionary<string,int>(StringComparer.Ordinal);
            var rows=new List<Entry>();var targetList=new List<int>();var counts=new int[decorations];
            var sources=new HashSet<int>();int repeated=0,clears=0,previous=-1;
            for(int i=0;i<input.Count;i++)
            {
                var e=input[i];
                if(e.Ordinal<=previous||double.IsNaN(e.Scheduled)||double.IsInfinity(e.Scheduled)||e.Targets==null
                    ||(e.VisualFlags&~15)!=0||(e.Selector!=null)!=((e.VisualFlags&1)!=0))throw new ArgumentException("invalid image occurrence");
                previous=e.Ordinal;
                foreach(int d in e.Targets)if((uint)d>=(uint)decorations)throw new ArgumentException("invalid image target");
                if(e.Selector==null)continue;
                if(e.Selector.Length>32768||targetList.Count>4000000-e.Targets.Length)throw new ArgumentException("image tape payload budget");
                if(!ids.TryGetValue(e.Selector,out int selector))
                {
                    if(ids.Count>=4096)throw new ArgumentException("image selector budget");
                    ids.Add(e.Selector,selector=names.Count);names.Add(e.Selector);
                }
                if(!sources.Add(e.Source))repeated++; // IDs are not occurrence keys.
                if(e.Selector.Length==0)clears++;
                rows.Add(new Entry(e,selector,targetList.Count));
                // Keep duplicate targets and equal-time occurrences. Image setter
                // side effects have not been proved safe to deduplicate.
                foreach(int d in e.Targets){targetList.Add(d);counts[d]++;}
            }
            var starts=new int[decorations+1];int affected=0;
            for(int i=0;i<decorations;i++){starts[i+1]=checked(starts[i]+counts[i]);if(counts[i]>0)affected++;}
            var refs=new int[targetList.Count];var cursor=(int[])starts.Clone();var all=rows.ToArray();var targetArray=targetList.ToArray();
            for(int i=0;i<all.Length;i++)for(int k=all[i].First;k<all[i].First+all[i].Count;k++)refs[cursor[targetArray[k]]++]=i;
            return new GpuDecoImageTimeline(all,names.ToArray(),targetArray,starts,refs,affected,clears,repeated);
        }
    }
}
#endif
