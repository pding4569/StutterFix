#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using HarmonyLib;
using UnityEngine;
namespace StutterFix
{
    // No deferred state or readers. A complete uninterrupted sweep renews clean certificates.
    internal static class FloorRange
    {
        internal const byte Position=1, Rotation=2, Scale=4, Opacity=8, All=15;
        internal sealed class Chunk
        {
            internal byte Dirty, Checking;
            internal int Cursor, Generation, CheckGeneration;
            internal float BaseOpacity;
            internal bool UniformOpacity=true, Blocked;
        }
        private static bool enabled, ready, shadow, verifying, mismatchLatched;
        private static List<scrFloor> floors;
        private static Chunk[] chunks;
        private static int count, sweep;
        private static readonly Dictionary<scrFloor,int> indices=new Dictionary<scrFloor,int>();
        private static readonly Dictionary<Transform,int> transforms=new Dictionary<Transform,int>();
        private static readonly Dictionary<string,long> reasons=new Dictionary<string,long>();
        private static long effects, skipped, processed, hookCalls, hookHits, dirtyMarks, lateChanges, samples, comparisons, mismatch;
        private static long startTicks, classifyTicks, skipTicks, legacyTicks, safetyTicks, hookTicks;
        private static double perTileMs;
        private static bool siteDiagnostics;
        private static readonly long[] siteCalls=new long[4096];
        private static readonly string[] siteNames=new string[4096];
        private static int siteCount;
        internal static int RegisterSite(string name)
        {
            // Preparation only. Re-transpiling the same method keeps its slot.
            for(int i=0;i<siteCount;i++) if(siteNames[i]==name) return i;
            if(siteCount==siteNames.Length) throw new InvalidOperationException("writer-site-budget");
            siteNames[siteCount]=name; return siteCount++;
        }
        public static void CountSite(int site)
        { if(siteDiagnostics) Interlocked.Increment(ref siteCalls[site]); }
        private static string SiteStatus()
        {
            string s=" sites="+siteDiagnostics+" site-slots="+siteCount;
            var order=new List<int>();
            var snapshot=new long[siteCount];
            for(int i=0;i<snapshot.Length;i++) { snapshot[i]=Interlocked.Read(ref siteCalls[i]); if(snapshot[i]>0) order.Add(i); }
            order.Sort((a,b)=>snapshot[b].CompareTo(snapshot[a]));
            for(int i=0;i<Math.Min(12,order.Count);i++)
            { int id=order[i]; s+=" | site["+id+"] calls="+snapshot[id]+" "+siteNames[id]; }
            return s;
        }
        private static int pipelineDepth, pipelineFrame=-1;
        private static long pipelineStamp, pipelineTicks, pipelinePeak, pipelinePrevious, pipelineTotal;
        internal static void PipelineEnter()
        {
            if(!enabled) return;
            if(pipelineDepth++!=0) return;
            if(pipelineFrame!=Time.frameCount)
            { pipelinePrevious=pipelineTicks; pipelineFrame=Time.frameCount; pipelineTicks=0; }
            pipelineStamp=Stopwatch.GetTimestamp();
        }
        internal static void PipelineExit()
        {
            if(!enabled||pipelineDepth==0) return;
            if(--pipelineDepth==0)
            {
                long elapsed=Stopwatch.GetTimestamp()-pipelineStamp;
                pipelineTicks+=elapsed; pipelineTotal+=elapsed;
                if(pipelineTicks>pipelinePeak) pipelinePeak=pipelineTicks;
            }
        }
        internal static void Log(string s) { Main.Entry.Logger.Log("[타일구간] "+s); }
        internal static void Reason(string s) { long n; reasons.TryGetValue(s,out n); reasons[s]=n+1; }
        internal static void Configure(bool on) { enabled=on; }
        internal static void Install(Harmony h) { FloorRangeWriters.InstallLifecycle(h); }
        public static void PreparedHook() { if(enabled) SafePrepare(); }
        private static void SafePrepare()
        {
            try { Prepare(); }
            catch(Exception ex) { ResetMap(); Reason("prepare:"+ex.GetType().Name); }
            Log(Status());
        }
        internal static void Command(string arg)
        {
            switch(arg.Trim())
            {
                case "on": enabled=true; shadow=false; SafePrepare(); return;
                case "shadow": enabled=true; shadow=true; SafePrepare(); return;
                case "off": Shutdown(); break;
                case "flush": Flush("command"); break;
                case "status": break;
                case "sites on": Array.Clear(siteCalls,0,siteCalls.Length); siteDiagnostics=true; break;
                case "sites off": siteDiagnostics=false; break;
                default: Log("tilerange on/shadow/off/flush/status/sites on/sites off; on/shadow는 재생 전 정지 상태에서"); return;
            }
            Log(Status());
        }
        private static void Prepare()
        {
            ResetMap(); mismatchLatched=false;
            if(!FloorRangeWriters.Install()) { Reason("writer-install-failed"); return; }
            var list=ADOBase.lm==null ? null : ADOBase.lm.listFloors;
            if(list==null || list.Count==0) { Reason("prepare-no-map"); return; }
            floors=list; count=list.Count; chunks=new Chunk[(count+63)/64];
            for(int ch=0;ch<chunks.Length;ch++) chunks[ch]=new Chunk();
            for(int i=0;i<count;i++)
            {
                var f=list[i]; var ch=chunks[i/64];
                if(f==null || indices.ContainsKey(f) || f.transform==null || transforms.ContainsKey(f.transform))
                { ch.Blocked=true; Reason("prepare-null-alias"); continue; }
                indices.Add(f,i); transforms.Add(f.transform,i);
                if((i&63)==0) ch.BaseOpacity=f.opacityVal;
                else if(!Bits(ch.BaseOpacity,f.opacityVal)) ch.UniformOpacity=false;
                ch.Dirty|=ActualDirty(f,ch.BaseOpacity);
            }
            ready=true;
            Log("prepared floors="+count+" chunks="+chunks.Length+" writers="+FloorRangeWriters.Installed+" shadow="+shadow);
        }
        private static bool HasRunning(scrFloor f)
        {
            if(f.moveTweens==null) return true;
            foreach(var t in f.moveTweens.Values)
            {
                if((object)t==null) continue;
                var r=t.id as FloorAnim.Rec;
                if(r!=null ? r.Running : t.active) return true;
            }
            return false;
        }
        private static byte ActualDirty(scrFloor f,float opacity)
        {
            if(f==null || f.freeroamArea!=null || f.floorRenderer==null || f.floorRenderer.material==null || HasRunning(f)) return All;
            var t=f.transform; var p=t.position; var s=t.localScale; byte mask=0;
            var bp=f.startPos+new Vector3(0f,0f,0f);
            if(!Mathf.Approximately(p.x,bp.x) || !Mathf.Approximately(p.y,bp.y)) mask|=Position;
            if(!Mathf.Approximately(t.eulerAngles.z,(f.startRot+new Vector3(0f,0f,0f)).z)) mask|=Rotation;
            if(!s.ApproximatelyXY(s.WithX(1f)) || !s.ApproximatelyXY(s.WithY(1f))) mask|=Scale;
            if(!Mathf.Approximately(f.opacity,opacity)) mask|=Opacity;
            return mask;
        }
        internal static void Mark(scrFloor f,byte channels=All)
        {
            if(!ready || verifying) return;
            PipelineEnter(); long t=Stopwatch.GetTimestamp(); hookCalls++; int i;
            if(!ReferenceEquals(f,null) && indices.TryGetValue(f,out i)) { hookHits++; MarkIndex(i,channels); }
            hookTicks+=Stopwatch.GetTimestamp()-t;
            PipelineExit();
        }
        internal static void Mark(Transform transform,byte channels)
        {
            if(!ready || verifying) return;
            PipelineEnter(); long t=Stopwatch.GetTimestamp(); hookCalls++; int i;
            if(!ReferenceEquals(transform,null) && transforms.TryGetValue(transform,out i)) { hookHits++; MarkIndex(i,channels); }
            hookTicks+=Stopwatch.GetTimestamp()-t;
            PipelineExit();
        }
        private static void MarkIndex(int i,byte channels)
        { var c=chunks[i/64]; c.Dirty|=channels; c.Generation++; dirtyMarks++; }
        internal static void BeforeLegacy(scrFloor f) { Mark(f); }
        // Compatibility sites from T2: there is no deferred state to drain.
        internal static void Materialize(scrFloor f,string reason) { }
        internal static void SetCulled(scrFloor f,bool value) { }
        internal static void Flush(string reason) { }
        internal static void ResetMap()
        { ready=false; floors=null; chunks=null; indices.Clear(); transforms.Clear(); count=sweep=0; }
        internal static void Shutdown() { ResetMap(); enabled=shadow=siteDiagnostics=false; FloorRangeWriters.Shutdown(); }
        private static bool Target(ref FloorAnim.Ctx c,out byte mask)
        {
            mask=0;
            if(c.PosUsed)
            {
                if((!float.IsNaN(c.Pos.x)&&c.Pos.x!=0f)||(!float.IsNaN(c.Pos.y)&&c.Pos.y!=0f)) return false;
                if(!float.IsNaN(c.Pos.x)||!float.IsNaN(c.Pos.y)) mask|=Position;
            }
            if(c.RotUsed) { if(c.Rot.z!=0f) return false; mask|=Rotation; }
            if(c.ScaleUsed)
            {
                if((!float.IsNaN(c.Scale.x)&&c.Scale.x!=1f)||(!float.IsNaN(c.Scale.y)&&c.Scale.y!=1f)) return false;
                if(!float.IsNaN(c.Scale.x)||!float.IsNaN(c.Scale.y)) mask|=Scale;
            }
            if(c.OpaUsed) mask|=Opacity;
            return mask!=0;
        }
        internal static bool TryStart(List<scrFloor> list,int start,int end,int step,ref FloorAnim.Ctx c)
        {
            if(!enabled) return false;
            if(!ready||!ReferenceEquals(list,floors)||list.Count!=count) { Reason("not-ready-map-changed"); return false; }
            if(mismatchLatched) { Reason("shadow-mismatch-latched"); return false; }
            if(FloorAnim.Split||FloorAnim.RangePending) { Reason("split-or-pending"); return false; }
            if(TileSim.Capturing) { Reason("tilesim-capture"); return false; }
            if(step!=1||start<0||end>=count||end<start) { Reason("gap-or-invalid-range"); return false; }
            if(end-start+1<1000) { Reason("below-1000"); return false; }
            byte mask;
            if(!Target(ref c,out mask)) { Reason("non-default-target"); return false; }
            long begin=Stopwatch.GetTimestamp(), classify=0, skip=0, legacy=0;
            int skippedChunks=0, dirtyChunks=0, done=0, examined=0; effects++;
            for(int k=start/64;k<=end/64;k++)
            {
                long t=Stopwatch.GetTimestamp(); var ch=chunks[k];
                int lo=Math.Max(start,k*64), hi=Math.Min(end,k*64+63);
                bool clean=!mismatchLatched&&!ch.Blocked&&(ch.Dirty&mask)==0;
                // Approximately is not transitive: target must have the exact
                // baseline bits against which every tile was certified.
                if(c.OpaUsed&&(!ch.UniformOpacity||!Bits(c.Opacity,ch.BaseOpacity)))
                { clean=false; Reason("chunk-opacity-target"); }
                classify+=Stopwatch.GetTimestamp()-t; examined++; t=Stopwatch.GetTimestamp();
                int lastOracle=-1;
                if(clean&&shadow)
                {
                    // Preflight all samples before executing any oracle.
                    for(int i=lo;i<=hi;i++) if((i&15)==0&&!NoOp(floors[i],ref c))
                    { clean=false; lateChanges++; mismatch++; mismatchLatched=true; MarkIndex(i,All); Reason("shadow-preflight");
                      if(mismatch<=8) Log("mismatch preflight tile="+i+" actual path would write or complete a tween"); break; }
                }
                if(clean)
                {
                    if(shadow) for(int i=lo;i<=hi;i++) if((i&15)==0)
                    { lastOracle=i; if(!Verify(floors[i],i,ref c)) { clean=false; break; } }
                    if(clean) { skippedChunks++; skipped++; skip+=Stopwatch.GetTimestamp()-t; continue; }
                }
                skip+=Stopwatch.GetTimestamp()-t;
                dirtyChunks++; Reason(ch.Blocked?"chunk-blocked":"chunk-dirty"); t=Stopwatch.GetTimestamp();
                for(int i=lo;i<=hi;i++)
                { if(!((i&15)==0&&i<=lastOracle)) FloorAnim.RangeLegacy(list[i],ref c); done++; }
                legacy+=Stopwatch.GetTimestamp()-t;
            }
            long elapsed=Stopwatch.GetTimestamp()-begin;
            if(done>0) perTileMs=Ms(legacy)/done;
            processed+=done; classifyTicks+=classify; skipTicks+=skip; legacyTicks+=legacy; startTicks+=elapsed;
            Log(string.Format("start effects={0} chunks={1} skipped-chunks={2} dirty-chunks={3} tiles-processed={4} start-ms={5:F3} original-ms-estimate={6:F3} estimate-source={7} classify-ms={8:F3} skip-shadow-ms={9:F3} original-ms={10:F3} unaccounted-ms={11:F3}",
                effects,examined,skippedChunks,dirtyChunks,done,Ms(elapsed),perTileMs*(end-start+1),perTileMs>0?"observed-dirty-tiles":"unavailable",Ms(classify),Ms(skip),Ms(legacy),Ms(elapsed-classify-skip-legacy)));
            return true;
        }
        private static bool NoOp(scrFloor f,ref FloorAnim.Ctx c)
        {
            if(f.freeroamArea!=null||HasRunning(f)||f.floorRenderer==null||f.floorRenderer.material==null) return false;
            var t=f.transform; var p=t.position; var s=t.localScale; var v=f.startPos+c.Pos;
            if(c.PosUsed&&((!float.IsNaN(v.x)&&!Mathf.Approximately(p.x,v.x))||(!float.IsNaN(v.y)&&!Mathf.Approximately(p.y,v.y)))) return false;
            if(c.RotUsed&&!Mathf.Approximately(t.eulerAngles.z,(f.startRot+c.Rot).z)) return false;
            if(c.ScaleUsed&&((!float.IsNaN(c.Scale.x)&&!s.ApproximatelyXY(s.WithX(c.Scale.x)))||(!float.IsNaN(c.Scale.y)&&!s.ApproximatelyXY(s.WithY(c.Scale.y))))) return false;
            return !c.OpaUsed||Mathf.Approximately(f.opacity,c.Opacity);
        }
        private static bool Verify(scrFloor f,int i,ref FloorAnim.Ctx c)
        {
            var t=f.transform; var p=t.position; var r=t.eulerAngles; var s=t.localScale;
            var tr=f.tweenRot; float o=f.opacity; bool sprite=f.dontChangeMySprite;
            var mt=f.moveTweens; var entries=new List<KeyValuePair<global::TweenType,DG.Tweening.Tween>>(mt);
            verifying=true;
            try { FloorAnim.RangeLegacy(f,ref c); } finally { verifying=false; }
            bool same=Bits(p,t.position)&&Bits(r,t.eulerAngles)&&Bits(s,t.localScale)&&Bits(tr,f.tweenRot)&&Bits(o,f.opacity)
                &&sprite==f.dontChangeMySprite&&ReferenceEquals(mt,f.moveTweens)&&mt.Count==entries.Count;
            foreach(var pair in entries)
            { DG.Tweening.Tween value; same&=mt.TryGetValue(pair.Key,out value)&&ReferenceEquals(pair.Value,value); }
            comparisons++;
            if(!same)
            {
                mismatch++; mismatchLatched=true; MarkIndex(i,All);
                if(mismatch<=8) Log("mismatch tile="+i+" position="+Hex(p)+"/"+Hex(t.position)+" rotation="+Hex(r)+"/"+Hex(t.eulerAngles)
                    +" scale="+Hex(s)+"/"+Hex(t.localScale)+" opacity="+o.ToString("R")+"/"+f.opacity.ToString("R")+" tweens="+entries.Count+"/"+mt.Count);
            }
            return same;
        }
        internal static void Tick()
        {
            if(!ready) return;
            if(ADOBase.lm==null||!ReferenceEquals(floors,ADOBase.lm.listFloors)||floors.Count!=count)
            { ResetMap(); Reason("tick-map-changed"); return; }
            PipelineEnter(); long t=Stopwatch.GetTimestamp();
            // 1/32 chunks per frame, four samples per chunk. Renewal needs every
            // tile with no intervening hooked write, not just four matches.
            int budget=Math.Max(1,(chunks.Length+31)/32);
            for(int n=0;n<budget;n++)
            {
                int k=sweep; sweep=(sweep+1)%chunks.Length; var ch=chunks[k]; if(ch.Blocked) continue;
                if(ch.Cursor==0||ch.CheckGeneration!=ch.Generation)
                { ch.Cursor=0; ch.Checking=0; ch.CheckGeneration=ch.Generation; }
                int size=Math.Min(64,count-k*64);
                for(int j=0;j<4&&ch.Cursor<size;j++,ch.Cursor++)
                {
                    int i=k*64+ch.Cursor; var f=floors[i]; int tracked;
                    if(f==null||!indices.TryGetValue(f,out tracked)||tracked!=i||!transforms.TryGetValue(f.transform,out tracked)||tracked!=i)
                    { ch.Blocked=true; ch.Dirty=All; Reason("safety-identity-changed"); break; }
                    byte actual=ActualDirty(f,ch.BaseOpacity); samples++;
                    if((actual&~ch.Dirty)!=0) { lateChanges++; ch.Dirty|=actual; Reason("late-write-detected"); }
                    ch.Checking|=actual;
                }
                if(ch.Cursor>=size) { ch.Dirty=ch.Checking; ch.Cursor=0; }
            }
            safetyTicks+=Stopwatch.GetTimestamp()-t;
            PipelineExit();
        }
        private static bool Bits(float a,float b) { return BitConverter.SingleToInt32Bits(a)==BitConverter.SingleToInt32Bits(b); }
        private static bool Bits(Vector3 a,Vector3 b) { return Bits(a.x,b.x)&&Bits(a.y,b.y)&&Bits(a.z,b.z); }
        private static string Hex(Vector3 v) { return BitConverter.SingleToInt32Bits(v.x).ToString("X8")+","+BitConverter.SingleToInt32Bits(v.y).ToString("X8")+","+BitConverter.SingleToInt32Bits(v.z).ToString("X8"); }
        private static double Ms(long t) { return t*1000.0/Stopwatch.Frequency; }
        private static string Status()
        {
            string s="enabled="+enabled+" ready="+ready+" shadow="+shadow+" patch-fail="+FloorRangeWriters.Failures+" patched="+FloorRangeWriters.Installed
                +" effects="+effects+" skipped-chunks="+skipped+" tiles-processed="+processed+" verify="+comparisons+" mismatch="+mismatch+" mismatch-latched="+mismatchLatched
                +" hook-calls="+hookCalls+" hook-normal="+hookHits+" dirty-marks="+dirtyMarks+" safety-samples="+samples+" late-changes="+lateChanges;
            s+=string.Format(" start-ms={0:F3} classify-ms={1:F3} skip-shadow-ms={2:F3} original-ms={3:F3} unaccounted-ms={4:F3} safety-ms={5:F3} hook-ms={6:F3}",
                Ms(startTicks),Ms(classifyTicks),Ms(skipTicks),Ms(legacyTicks),Ms(startTicks-classifyTicks-skipTicks-legacyTicks),Ms(safetyTicks),Ms(hookTicks));
            s+=" pipeline-frame="+pipelineFrame+" pipeline-ms="+Ms(pipelineTicks).ToString("F3")+" pipeline-previous-ms="+Ms(pipelinePrevious).ToString("F3")
                +" pipeline-peak-ms="+Ms(pipelinePeak).ToString("F3")+" pipeline-total-ms="+Ms(pipelineTotal).ToString("F3")+" hook-untracked="+(hookCalls-hookHits);
            foreach(var pair in reasons) s+=" | "+pair.Key+"="+pair.Value;
            return s+SiteStatus();
        }
    }
}
#endif
