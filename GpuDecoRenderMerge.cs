#if DEV || AUTOTEST || GPUDECO
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        // Research switch. Keep the whole-song master/clock/packet arrays and
        // independent backend intact. Only prebuilt draw/copy destinations change.
        static bool gpuRenderMergePrepare,gpuRenderMergePrepared,gpuRenderMergeOn;
        sealed class MergeMap
        {
            internal GraphicsBuffer Buffer;
            internal uint[] Values;
            internal MeshRenderer Renderer;
            internal int LiveGroups;
        }
        sealed class MergeGroup
        {
            internal MergeMap Map;
            internal int VertexStart;
            internal int[] Items;
            internal int Count=-1;
            internal bool[] Drawn;
            internal bool Initialized,Dirty;
        }
        sealed class MergeWindow
        {
            internal double Start,End;
            internal GpuLiveSlice Draw;
            internal bool[] Included;
            internal List<MergeGroup>[] GroupsByItem;
            internal bool[] Fallback;
            internal readonly List<MergeGroup> Groups=new List<MergeGroup>();
            internal readonly Dictionary<Mesh,MergeMap> Maps=new Dictionary<Mesh,MergeMap>();
            internal int LiveDraws,LiveRenderers;
        }
        static readonly List<MergeWindow> gpuMergeWindows=new List<MergeWindow>();
        static MergeWindow gpuMergeCurrent;
        static MergeWindow gpuMergeBuilding;
        static List<GpuLiveRow> gpuMergeRows;
        static string gpuMergeError;
        static readonly List<MergeGroup> gpuMergeDirtyGroups=new List<MergeGroup>();

        static void HideGpuMerge()
        {
            if(gpuMergeCurrent!=null)foreach(var r in gpuMergeCurrent.Draw.Renderers)if(r)r.enabled=false;
            gpuMergeCurrent=null;
        }
        static void DisposeGpuMerge()
        {
            HideGpuMerge();
            gpuMergeDirtyGroups.Clear();
            foreach(var w in gpuMergeWindows)
            {
                foreach(var r in w.Draw.Renderers)if(r){r.enabled=false;UnityEngine.Object.Destroy(r.gameObject);}
                w.Draw.Dispose();
            }
            gpuMergeWindows.Clear();gpuRenderMergePrepared=false;
        }
        static void MergeReject(string why)
        {
            gpuMergeError=why;
            throw new InvalidOperationException("rendermerge: "+why);
        }
        static void PrepareGpuMerge(List<GpuLiveRow> masterRows)
        {
            if(!gpuRenderMergePrepare)return;
            RequireLive(GpuPlaybackState.Paused,"rendermerge allocations require paused pre-playback preparation");
            RequireLive(gpuScope.WholeSong && gpuLiveSlices.Count==1 && GpuDecoNativeBatch.Enabled && !GpuDecoNativeBatch.NoMpb,
                "rendermerge requires song scope, independent baseline and nombp off");
            var source=gpuLiveSlices[0];
            gpuMergeRows=masterRows;
            // Same-material owned ties have no established Unity ordering rule.
            for(int i=1;i<masterRows.Count;i++)
                RequireLive(!masterRows[i-1].Key.SameMaterialTie(masterRows[i].Key)
                    || masterRows[i-1].Item<0 && masterRows[i].Item<0,"rendermerge ambiguous material tie");
            var itemByIndex=new Dictionary<int,int>();
            for(int i=0;i<source.Data.Items.Length;i++)itemByIndex.Add(source.Data.Items[i].Index,i);
            int kernel=gpuLiveShader.FindKernel("CopyMappedRun");
            // All allocations and command recording happen before playback.
            // The time table is only a layout hint. Authoritative live channels
            // validate every omission at entry and every subsequent alpha change.
            foreach(double start in Enumerable.Range(0,12).Select(i=>250+i*2.5).Concat(Enumerable.Range(0,10).Select(i=>290+i*2.5)))
            {
                var w=new MergeWindow{Start=start,End=start+2.5,Included=new bool[source.MasterItems.Length],
                    GroupsByItem=new List<MergeGroup>[source.MasterItems.Length],Fallback=new bool[source.MasterItems.Length],
                    Draw=new GpuLiveSlice{Data=source.Data,Commands=new CommandBuffer{name="GpuDeco merged window"}}};
                gpuMergeWindows.Add(w); // Own partial allocations before building.
                w.Draw.Commands.SetComputeBufferParam(gpuLiveShader,kernel,"_CopySource",source.Gpu.VertexBuffer);
                w.Draw.Commands.SetComputeIntParam(gpuLiveShader,"_CopyQuantizeColor",1);
                var rows=new List<GpuLiveRow>();
                int omittedAuxiliaries=0;
                foreach(var row in masterRows)
                {
                    if(row.AuxiliaryOwner>=0 && itemByIndex.TryGetValue(row.AuxiliaryOwner,out int owner)
                        && RenderMergeLayoutPolicy.OmitAuxiliary(true,GpuCpuFallback(source.MasterItems[owner]),
                            row.R && row.R.enabled && row.R.gameObject.activeInHierarchy && !row.R.forceRenderingOff))
                    {omittedAuxiliaries++;continue;}
                    if(row.Item<0){rows.Add(row);continue;}
                    int item=itemByIndex[row.Item];
                    if(gpuLiveActive && GpuCpuFallback(source.MasterItems[item]))
                    {
                        w.Fallback[item]=true;
                        rows.Add(new GpuLiveRow{R=row.R,M=row.M,Key=row.Key,Signature=row.Signature,Item=-1});continue;
                    }
                    if(!VisibleInWindow(table,row.Item,start-.25,start+2.75))continue;
                    w.Included[item]=true;
                    rows.Add(new GpuLiveRow{R=row.R,M=row.M,Key=row.Key,Signature=row.Signature,Item=item});
                }
                int splitBarrier=0,splitBucket=0,splitLayer=0,splitMaterial=0;
                for(int a=0;a<rows.Count;)
                {
                    if(rows[a].Item<0){a++;continue;}
                    int b=a+1;
                    while(b<rows.Count && rows[b].Item>=0 && rows[a].Key.SameBucket(rows[b].Key)
                        && rows[a].R.gameObject.layer==rows[b].R.gameObject.layer
                        && rows[b-1].M.GetInstanceID()<rows[b].M.GetInstanceID())b++;
                    if(b<rows.Count)
                    {
                        if(rows[b].Item<0)splitBarrier++;
                        else if(!rows[a].Key.SameBucket(rows[b].Key))splitBucket++;
                        else if(rows[a].R.gameObject.layer!=rows[b].R.gameObject.layer)splitLayer++;
                        else splitMaterial++;
                    }
                    int meshAt=w.Draw.Meshes.Count,rendererAt=w.Draw.Renderers.Count;
                    int groups=BuildLiveRun(w.Draw,rows,a,b,kernel,source.Data.Items[rows[a].Item].Constants[7],w);
                    w.Draw.RunCount++;w.Draw.SubmeshCount+=groups;
                    var mesh=w.Draw.Meshes[meshAt];var renderer=w.Draw.Renderers[rendererAt];
                    RequireLive(GpuDecoScene.SortKey(rows[a].R,rows[a].M,gpuLiveCamera).SameBucket(
                        GpuDecoScene.SortKey(renderer,renderer.sharedMaterial,gpuLiveCamera)),"rendermerge sort bucket changed");
                    for(int n=a;n<b;)
                    {
                        int end=n+1;
                        while(end<b && source.Data.Items[rows[n].Item].TextureId==source.Data.Items[rows[end].Item].TextureId
                            && rows[n].Signature==rows[end].Signature)end++;
                        var g=new MergeGroup{Map=w.Maps[mesh],VertexStart=(n-a)*4,
                            Items=rows.Skip(n).Take(end-n).Select(x=>x.Item).ToArray()};
                        g.Drawn=new bool[g.Items.Length];w.Groups.Add(g);
                        foreach(int item in g.Items)
                        {
                            if(w.GroupsByItem[item]==null)w.GroupsByItem[item]=new List<MergeGroup>();
                            w.GroupsByItem[item].Add(g);
                        }
                        n=end;
                    }
                    a=b;
                }
                GpuLog("rendermerge_prepared start="+start.ToString("F1",Inv)+" end="+w.End.ToString("F1",Inv)
                    +" items="+w.Included.Count(x=>x)+" renderers="+w.Draw.RunCount+" submeshes="+w.Draw.SubmeshCount
                    +" omitted_admitted_auxiliaries="+omittedAuxiliaries
                    +" barriers="+rows.Count(x=>x.Item<0)
                    +" split_barrier="+splitBarrier+" split_bucket="+splitBucket
                    +" split_layer="+splitLayer+" split_material="+splitMaterial);
            }
            gpuRenderMergePrepared=true;
        }
        static bool MergeHidden(int item)
        {
            var s=gpuLiveSlices[0];
            if(GpuCpuFallback(s.MasterItems[item]))return true;
            var a=s.Channels[item*16+10];var b=s.Channels[item*16+11];
            return GpuDecoAlphaPolicy.Hidden(a.end,a.sourceIndex,b.end,b.sourceIndex);
        }
        static void RefreshGpuMergeGroup(MergeGroup g)
        {
            int count=0;bool same=g.Initialized;
            for(int i=0;i<g.Items.Length;i++)
            {
                bool drawn=!MergeHidden(g.Items[i]);same&=drawn==g.Drawn[i];g.Drawn[i]=drawn;
                RenderMergeLayoutPolicy.MapQuad(g.Map.Values,g.VertexStart+i*4,g.Items[i],drawn);
                if(drawn)count++;
            }
            if(same)return;
            // Never SetIndices/SetSubMesh after GetVertexBuffer: CPU mesh edits
            // can replace the GPU buffer referenced by the recorded copy.
            int offset=g.VertexStart,length=g.Items.Length*4;
            g.Map.Buffer.SetData(g.Map.Values,offset,offset,length);
            int delta=(count>0?1:0)-(g.Count>0?1:0);
            bool wasLive=g.Map.LiveGroups>0;
            g.Map.LiveGroups+=delta;
            if(gpuMergeBuilding!=null)
            {
                gpuMergeBuilding.LiveDraws+=delta;
                if(wasLive!=(g.Map.LiveGroups>0))gpuMergeBuilding.LiveRenderers+=g.Map.LiveGroups>0?1:-1;
            }
            if(gpuMergeBuilding!=null && ReferenceEquals(gpuMergeBuilding,gpuMergeCurrent))
                g.Map.Renderer.enabled=g.Map.LiveGroups>0;
            g.Count=count;
            g.Initialized=true;
        }
        static void RefreshGpuMergeItem(int item)
        {
            var w=gpuMergeCurrent;if(w==null)return;
            if(!w.Included[item])
            {
                if(!MergeHidden(item))MergeReject("visible_item_outside_prepared_window item="+item+" start="+w.Start);
                return;
            }
            var groups=w.GroupsByItem[item];
            if(groups!=null)foreach(var g in groups)if(!g.Dirty){g.Dirty=true;gpuMergeDirtyGroups.Add(g);}
        }
        static void FlushGpuMergeGroups()
        {
            gpuMergeBuilding=gpuMergeCurrent;
            try {foreach(var g in gpuMergeDirtyGroups){g.Dirty=false;RefreshGpuMergeGroup(g);}}
            finally {gpuMergeDirtyGroups.Clear();gpuMergeBuilding=null;}
        }
        static bool GpuMergeFallbackMatches()
        {
            if(!gpuRenderMergePrepared || gpuMergeWindows.Count==0 || gpuLiveSlices.Count!=1)return false;
            var source=gpuLiveSlices[0];
            foreach(var w in gpuMergeWindows)
            {
                if(w.Fallback.Length!=source.MasterItems.Length)return false;
                for(int i=0;i<w.Fallback.Length;i++)
                    if(w.Fallback[i]!=GpuCpuFallback(source.MasterItems[i]))return false;
            }
            return true;
        }
        static void ValidateGpuMergeAuxiliaries()
        {
            foreach(var row in gpuMergeRows)
            {
                if(row.AuxiliaryOwner<0)continue;
                var d=baselineObjects[row.AuxiliaryOwner] as scrVisualDecoration;
                if(!d || !gpuLiveMasterByDeco.TryGetValue(d,out int master))continue;
                RenderMergeLayoutPolicy.OmitAuxiliary(true,GpuCpuFallback(master),
                    row.R && row.R.enabled && row.R.gameObject.activeInHierarchy && !row.R.forceRenderingOff);
            }
        }
        static void DispatchGpuMerge(double time)
        {
            var w=gpuMergeCurrent;
            if(w==null || time<w.Start || time>=w.End)
            {
                HideGpuMerge();
                w=gpuMergeWindows.FirstOrDefault(x=>time>=x.Start && time<x.End);
                if(w==null)MergeReject("outside_prepared_merge_scope song="+time.ToString("F6",Inv));
                // Fail closed if actual source clocks contradict the layout hint.
                for(int i=0;i<w.Included.Length;i++)if(!w.Included[i] && !MergeHidden(i))
                    MergeReject("visible_item_outside_prepared_window item="+i+" start="+w.Start);
                for(int i=0;i<w.Fallback.Length;i++)if(w.Fallback[i]!=GpuCpuFallback(gpuLiveSlices[0].MasterItems[i]))
                    MergeReject("cpu_fallback_changed; paused rendermerge ready required before next playback");
                gpuMergeBuilding=w;
                try {foreach(var g in w.Groups)RefreshGpuMergeGroup(g);}
                finally {gpuMergeBuilding=null;}
                gpuMergeCurrent=w;
                foreach(var map in w.Maps.Values)map.Renderer.enabled=map.LiveGroups>0;
            }
            Graphics.ExecuteCommandBuffer(w.Draw.Commands);
            ObserveGpuMergeCounts(w);
        }
        static void ObserveGpuMergeCounts(MergeWindow w)
        {
#if AUTOTEST
            if(mergeWindow>=0)
            {
                mergeMaxRenderers=Math.Max(mergeMaxRenderers,w.LiveRenderers);
                mergeMaxDraws=Math.Max(mergeMaxDraws,w.LiveDraws);
            }
#endif
        }
        static bool GpuRenderMergeCommand(string command)
        {
            if(!command.StartsWith("rendermerge ",StringComparison.Ordinal))return false;
            string sub=command.Substring(12);
            if(sub=="prepare")
            {
                RequireLive(!gpuLiveActive,"rendermerge prepare requires inactive ownership");
                gpuRenderMergePrepare=true;GpuLog("rendermerge_prepare=True default_off=True");return true;
            }
            if(sub=="ready")
            {
#if AUTOTEST
                RequireLive(mergeWindow<0,"close rendermerge measurement before checking layouts");
#endif
                RequireLive(gpuLiveActive && !gpuRenderMergeOn && gpuMergeRows!=null,
                    "rendermerge ready requires independent GPU ownership");
                ValidateGpuMergeAuxiliaries();
                // Live readiness only validates prebuilt barriers. Never allocate
                // meshes/command buffers or detach ownership in a playing arm.
                bool cached=GpuMergeFallbackMatches();
                if(!cached)
                {
                    RequireLive(GpuPlaybackState.Paused,"rendermerge fallback layout changed; prepare while paused before next playback");
                    var rows=gpuMergeRows;DisposeGpuMerge();PrepareGpuMerge(rows);
                }
                GpuLog("rendermerge_ready=True cached="+cached+" paused="+GpuPlaybackState.Paused
                    +" CPU_fallback_barriers="+gpuLiveCpuFallbackCount);return true;
            }
            if(sub=="on" || sub=="off")
            {
#if AUTOTEST
                RequireLive(mergeWindow<0,"close rendermerge measurement before changing mode");
#endif
                bool on=sub=="on";RequireLive(!on || gpuRenderMergePrepared,"rendermerge prepare/live prepare required");
                RequireLive(!on || !gpuLiveActive || GpuMergeFallbackMatches(),
                    "rendermerge fallback layout changed; ready required before enabling");
                if(on)ValidateGpuMergeAuxiliaries();
                HideGpuMerge();gpuRenderMergeOn=on;
                foreach(var s in gpuLiveSlices)s.RefreshAlpha();
                gpuLiveLastDispatchFrame=-1;
                if(gpuLiveActive)DispatchGpuLiveSlice(BakeTime);
                GpuLog("rendermerge_on="+on);return true;
            }
#if AUTOTEST
            return GpuRenderMergeGateCommand(sub);
#else
            throw new ArgumentException("rendermerge prepare|on|off");
#endif
        }
    }
}
#endif
