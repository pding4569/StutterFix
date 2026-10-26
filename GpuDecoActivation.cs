#if DEV || AUTOTEST || GPUDECO
using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        // Boundary transaction only. Never retains map objects after commit,
        // rollback or Unload, and never runs on the per-frame rendering path.
        static readonly List<(DecoAnim.Rec Rec,DecoAnim.GpuResumeSpec Spec,bool Started)> gpuActivationRecords
            =new List<(DecoAnim.Rec,DecoAnim.GpuResumeSpec,bool)>();
        static bool[] gpuLiveCpuFallback=Array.Empty<bool>();
        static int gpuLiveCpuFallbackCount;
        static bool GpuCpuFallback(int master)=>(uint)master<(uint)gpuLiveCpuFallback.Length && gpuLiveCpuFallback[master];
        static void ClearGpuActivationUndo(){gpuActivationRecords.Clear();gpuNativeAdoptions.Clear();gpuNativeSourceIds.Clear();ClearNativeAdoptionActivation();}
        internal sealed class GpuActivationDeferredException : Exception {}
        internal static int gpuActivationDeferred;
        static void ClassifyGpuActivationOwnership()
        {
            ClearGpuActivationUndo();
            BeginNativeAdoptionActivation();
            unchecked { GpuLateOwnershipEpoch++; }
            TightLate.ClearGpuList();
            nativeUninitSeen=0;
            gpuLiveCpuFallback=new bool[gpuLiveOwnedIndices.Length];gpuLiveCpuFallbackCount=0;
            gpuLiveOwnedSet.Clear();gpuLiveOwnedVisuals.Clear();
#if AUTOTEST
            int whyDead=0,whyNative=0,whyMismatch=0,whyDur0=0,whyEase=0;var whyKeys=new Dictionary<string,int>();var whyEases=new Dictionary<int,int>();
#endif
            for(int m=0;m<gpuLiveOwnedIndices.Length;m++)
            {
                var d=baselineObjects[gpuLiveOwnedIndices[m]] as scrVisualDecoration;bool fallback=!d;
#if AUTOTEST
                if(!d)whyDead++;
#endif
                NativeAdoptSpec adoption=null;
                if(DrawCollapsePolicy.AdoptNativeTweens && d && d.eventTweens!=null)
                    TryReadNativeChannels(d,m,out adoption);
                if(adoption==null && d && d.eventTweens!=null)foreach(var pair in d.eventTweens)
                {
                    var t=pair.Value;var r=t?.id as DecoAnim.Rec;
                    if(t!=null && t.active && (r==null || !r.Running)
                        || r!=null && r.Running && (!ReferenceEquals(r.D,d) || r.Key!=(int)pair.Key || r.Dur<=0 || !GpuDecoGpu.Supported((int)r.E)))
                    {
                        fallback=true;
#if AUTOTEST
                        if(t!=null && t.active && (r==null || !r.Running)){whyNative++;string k=pair.Key.ToString();whyKeys.TryGetValue(k,out int kc);whyKeys[k]=kc+1;}
                        else if(!ReferenceEquals(r.D,d) || r.Key!=(int)pair.Key)whyMismatch++;
                        else if(r.Dur<=0)whyDur0++;
                        else {whyEase++;int ev=(int)r.E;whyEases.TryGetValue(ev,out int ec);whyEases[ev]=ec+1;}
#endif
                        break;
                    }
                }
#if AUTOTEST
                if(gpuImageCandidate&&!gpuImageOwn&&GpuImageActor(gpuLiveOwnedIndices[m]))fallback=true;
#endif
                gpuLiveCpuFallback[m]=fallback;
                if(!fallback && adoption!=null)gpuNativeAdoptions.Add(m,adoption);
                if(fallback)gpuLiveCpuFallbackCount++;
                else if(d){gpuLiveOwnedSet.Add(d);gpuLiveOwnedVisuals.Add(d);}
            }
#if AUTOTEST
            // Publish rejection details before renderer/sort validation can roll
            // activation back (including an ambiguous material tie).
            if(DrawCollapsePolicy.AdoptNativeTweens)Main.Entry.Logger.Log("[GPU 장식] 이어받기 분류: 후보 "+gpuNativeAdoptions.Count
                +"개(장식), 채널 "+NativeAdoptionChannelCount()+"개, 거절 "+NativeRejectSummary());
            if(gpuLiveCpuFallbackCount>0 || gpuNativeAdoptions.Count>0)Main.Entry.Logger.Log("[GPU 장식] CPU 처리 이유: 죽은 객체 "+whyDead+" | 네이티브 트윈 진행 중 "+whyNative+" (종류 "+string.Join(", ",whyKeys.OrderByDescending(x=>x.Value).Take(8).Select(x=>x.Key+"="+x.Value))+") | 이어받기 "+gpuNativeAdoptions.Count+" | 기록 키 어긋남 "+whyMismatch+" | 길이 0 "+whyDur0+" | 지원 안 하는 이징 "+whyEase+" ("+string.Join(", ",whyEases.OrderByDescending(x=>x.Value).Take(6).Select(x=>"ease"+x.Key+"="+x.Value))+") | 따라잡기 중 "+EffectBudget.InCatchUp+" 곡 위치 "+(scrConductor.instance?scrConductor.instance.songposition_minusi.ToString("F1"):"-")+" | FastMove 원래 코드로 보낸 이유별 "+FastMove.WhySummary());
#endif
            if(DrawCollapsePolicy.AdoptNativeTweens && nativeUninitSeen>0)
            {
                // 같은 프레임에 막 만들어진 트윈이 있다: 다음 프레임에 다시 시도한다(최대 30프레임).
                if(gpuActivationDeferred++<30){Array.Clear(gpuLiveCpuFallback,0,gpuLiveCpuFallback.Length);gpuLiveCpuFallbackCount=0;throw new GpuActivationDeferredException();}
            }
            if(gpuLiveCpuFallbackCount>0)foreach(var slice in gpuLiveSlices)
                RequireLive(slice.Independent,"native tween CPU fallback requires independent renderers");
            RequireLive(gpuLiveOwnedSet.Count>0,"no safe GPU targets at this activation boundary");
            GpuLog("activation_ownership allocated="+gpuLiveOwnedIndices.Length+" GPU_owned="+gpuLiveOwnedSet.Count
                +" CPU_fallback="+gpuLiveCpuFallbackCount+" inferred_source_clocks=False");
        }
        static void BeginGpuActivationUndo()
        {
            gpuActivationRecords.Clear(); // Classification's read-only native snapshots survive until commit/rollback.
            // Read the authoritative progress, including resident representations,
            // before recording or detaching any CPU animation.
            DecoAnim.BeginGpuResume();
        }
        static void SaveGpuActivationRecord(DecoAnim.Rec r)
        {
            gpuActivationRecords.Add((r,DecoAnim.SnapshotGpuRecord(r),r.Started));
        }
        static void CommitGpuActivationUndo()
        {
            ValidateNativeAdoptions(); // Any failure here still has every original native tween intact.
            gpuActivationRecords.Clear();
        }
        static bool RollbackGpuActivation(string reason)
        {
            TightLate.ClearGpuList();
            Camera.onPreCull-=OnGpuLivePreCull;
            gpuLiveActive=false;gpuLiveCurrentSlice=-1;
            bool ok=true;int restored=0;
            foreach(var slice in gpuLiveSlices)
                foreach(var renderer in slice.Renderers)try{if(renderer)renderer.enabled=false;}catch{ok=false;}
            try {DecoAnim.BeginGpuResume();}catch{ok=false;}
            foreach(var row in gpuActivationRecords)
            {
                try
                {
                    if(row.Rec.Running)continue; // It was not detached yet.
                    DecoAnim.ResumeGpuRecord(row.Spec);
                    var resumed=row.Spec.D.eventTweens[(global::TweenType)row.Spec.Key]?.id as DecoAnim.Rec;
                    if(resumed==null)throw new InvalidOperationException("activation rollback record missing");
                    resumed.Started=row.Started;restored++;
                }
                catch(Exception ex){ok=false;GpuLog("activation_resume_error="+ex.GetType().Name);}
            }
            try {DecoAnim.EndGpuResume();}catch{ok=false;}
            for(int i=0;i<gpuLiveOwnedRenderers.Length;i++)
                try{ReleaseOwnedEnabled(i);if(!GpuCpuFallback(i) && gpuLiveOwnedRenderers[i])gpuLiveOwnedRenderers[i].forceRenderingOff=RestoredGpuRendererOff(i);}catch{ok=false;}
            ResetGpuLiveSourcePool();gpuRecoveryBlocked=true;
            GpuLog("activation_rollback reason="+reason+" continued="+ok+" restored_recs="+restored
                +" saved_recs="+gpuActivationRecords.Count+" CPU_fallback="+gpuLiveCpuFallbackCount
                +" continuity="+(ok?"RESTORED":"DEGRADED")+" injected="+GpuRecoveryTesting);
            ClearGpuActivationUndo();return ok;
        }
#if AUTOTEST
        sealed class GpuResourceLease
        {
            public int version,process;
            public int[] tokens,objects;
            public string mvid;
            public bool originalAuto;
        }
        // Test artifact only: no per-frame IO or strong Unity-object references.
        static string ResourceProbePath=>System.IO.Path.Combine(Main.Entry.Path,"gpudeco-resource-probe.bin");
        static string ResourceProbeMarker=>"probe-a";
        static string GpuProbeMvid=>typeof(Main).Assembly.ManifestModule.ModuleVersionId.ToString();
        static void MarkGpuResources()
        {
            RequireLive(gpuLiveActive && gpuLivePrepared && g2Window==-1,"resource mark requires active GPU outside performance windows");
            var ids=new HashSet<int>();
            void Add(UnityEngine.Object o){if(o)ids.Add(o.GetInstanceID());}
            Add(gpuLiveRoot);Add(gpuLiveBundle);Add(gpuLiveShader);
            MarkGpuStorageBackup(Add);
            foreach(var window in gpuMergeWindows)
            {
                foreach(var mesh in window.Draw.Meshes)Add(mesh);
                foreach(var renderer in window.Draw.Renderers)if(renderer)Add(renderer.gameObject);
            }
            foreach(var slice in gpuLiveSlices)
            {
                Add(slice.Gpu?.Mesh);foreach(var mesh in slice.Meshes)Add(mesh);
                foreach(var renderer in slice.Renderers)if(renderer)Add(renderer.gameObject);
            }
            using var process=System.Diagnostics.Process.GetCurrentProcess();
            var data=new GpuResourceLease{version=1,process=process.Id,tokens=GpuDecoCopy.ProbeTokens(),objects=ids.ToArray(),
                mvid=GpuProbeMvid,originalAuto=AutoTest.GpuReloadOriginalAuto};
            RequireLive(data.tokens.Length>0 && data.tokens.Length<=64 && data.objects.Length>0,"resource mark must be nonempty/bounded");
            RequireLive(33L+4L*(data.tokens.Length+data.objects.Length)<=RenderMergeLayoutPolicy.ResourceFileLimit,
                "resource mark exceeds bounded probe file");
            using(var file=System.IO.File.Create(ResourceProbePath))
            using(var writer=new System.IO.BinaryWriter(file))
            {
                writer.Write(data.version);writer.Write(data.process);writer.Write(data.tokens.Length);
                foreach(int id in data.tokens)writer.Write(id);
                writer.Write(data.objects.Length);foreach(int id in data.objects)writer.Write(id);
                writer.Write(Guid.Parse(data.mvid).ToByteArray());writer.Write(data.originalAuto);
            }
            GpuLog("resource_probe marked="+data.objects.Length+" tokens="+data.tokens.Length+" mvid="+data.mvid+" marker="+ResourceProbeMarker+" performance=False");
        }
        static GpuResourceLease ReadGpuResourceLease()
        {
            var file=new System.IO.FileInfo(ResourceProbePath);
            RequireLive(file.Exists && file.Length>0 && file.Length<=RenderMergeLayoutPolicy.ResourceFileLimit,"resource probe file missing/oversized");
            using var process=System.Diagnostics.Process.GetCurrentProcess();
            var data=new GpuResourceLease();
            using(var stream=System.IO.File.OpenRead(file.FullName))
            using(var reader=new System.IO.BinaryReader(stream))
            {
                data.version=reader.ReadInt32();data.process=reader.ReadInt32();int n=reader.ReadInt32();
                RequireLive(data.version==1 && data.process==process.Id && n>0 && n<=64,"resource probe wrong process/schema/token count");
                data.tokens=new int[n];for(int i=0;i<n;i++)data.tokens[i]=reader.ReadInt32();
                n=reader.ReadInt32();RequireLive(RenderMergeLayoutPolicy.ValidResourceObjects(n,stream.Length-stream.Position),
                    "resource probe empty/truncated/oversized object mark");
                data.objects=new int[n];for(int i=0;i<n;i++)data.objects[i]=reader.ReadInt32();
                data.mvid=new Guid(reader.ReadBytes(16)).ToString();data.originalAuto=reader.ReadBoolean();
                RequireLive(stream.Position==stream.Length,"resource probe trailing data");
            }
            return data;
        }
        static void RestoreGpuProbeAuto()=>AutoTest.InheritGpuReloadAuto(ReadGpuResourceLease().originalAuto);
        static void CheckGpuResources(bool reload)
        {
            var data=ReadGpuResourceLease();
            if(reload)AutoTest.InheritGpuReloadAuto(data.originalAuto);
            RequireLive(!gpuLiveActive && !gpuLivePrepared && g2Window==-1,"release prepared ownership before checking resources");
            bool different=data.mvid!=GpuProbeMvid;
            RequireLive(!reload || different,"new DLL MVID was not loaded");
            var ids=new HashSet<int>(data.objects);int alive=0;
            void Count<T>()where T:UnityEngine.Object
            {foreach(var o in Resources.FindObjectsOfTypeAll<T>())if(o && ids.Contains(o.GetInstanceID()))alive++;}
            Count<GameObject>();Count<Mesh>();Count<AssetBundle>();Count<ComputeShader>();
            int nativeAlive=GpuDecoCopy.ProbeRetired(data.tokens);
            GpuLog("resource_probe checked="+data.objects.Length+" unity_alive="+alive+" retired_tokens="+(data.tokens.Length-nativeAlive)+"/"+data.tokens.Length
                +" old_mvid="+data.mvid+" new_mvid="+GpuProbeMvid+" different_DLL="+different+" marker="+ResourceProbeMarker+" physical_GPU_memory=UNMEASURED performance=False");
            RequireLive(alive==0 && nativeAlive==0,"marked Unity resources or retired native token still accessible");
        }
        static void RunGpuResumeFrameFixture(bool handoff)
        {
            var plan=handoff?BuildGpuResumePlan(out _):null;
            FrameCompare.Resume();
            RequireLive(FrameCompare.PlaybackContinuity && !FrameCompare.Paused && !Hitch.Playing,
                "same-frame stale playing condition not reproduced");
            if(handoff)
            {
                GpuLog(DecoAnim.TestGpuResumeHandoff(plan,()=>
                {
                    RequireLive(RecoverGpuLive("resume_fixture",false) && !gpuLiveActive && gpuLastRestoreComplete,
                        "same-frame CPU continuation not restored");
                }));
                GpuLog("resume_frame_fixture kind=handoff stale_playing=True CPU_continued=True expected_records="+plan.Count+" performance=False");
            }
            else
            {
                var fx=effectList.OfType<ffxMoveDecorationsPlus>().FirstOrDefault(f=>f && f.duration>0 && Reasons(f).Length==0
                    && CopyEvent(f).Values.Any(v=>!float.IsNaN(v))
                    && f.decManager.GetTaggedDecorations(f.targetTags).Any(d=>gpuLiveOwnedSet.Contains(d)));
                RequireLive(fx!=null,"no supported owned-target effect for resume fixture");
                long before=gpuLiveSkippedRecs;
                bool original=FastMove.Prefix(fx,Array.Empty<object>(),true);
                RequireLive(!original && gpuLiveActive && gpuLiveSkippedRecs>before,"supported resume effect lost GPU ownership");
                GpuLog("resume_frame_fixture kind=supported stale_playing=True active=True skipped_added="+(gpuLiveSkippedRecs-before)
                    +" scope=seeded_existing_effect_not_natural_event performance=False");
            }
        }
        static void ProbeGpuActivationValues()
        {
            RequireLive(FrameCompare.Paused && gpuLiveActive && gpuLiveCurrentSlice>=0 && g2Window==-1,
                "GPU value readback is paused diagnostic only");
            var slice=gpuLiveSlices[gpuLiveCurrentSlice];var actual=new float[slice.MasterItems.Length*16];
            slice.Gpu.ReadValues(actual);
            var logical=new float[16];var maxGpu=new double[16];var maxMaster=new double[16];
            var counts=new int[16];var worst=new int[16];var observed=new bool[16];
            for(int i=0;i<slice.MasterItems.Length;i++)
            {
                int master=slice.MasterItems[i];if(GpuCpuFallback(master))continue;
                var d=baselineObjects[gpuLiveOwnedIndices[master]];
                logical[0]=d.pivotPosVec.x;logical[1]=d.pivotPosVec.y;logical[2]=d.pivotOffsetVec.x;logical[3]=d.pivotOffsetVec.y;
                logical[4]=d.rotAngle;logical[5]=d.scaleVec.x;logical[6]=d.scaleVec.y;
                logical[7]=d.color.r;logical[8]=d.color.g;logical[9]=d.color.b;logical[10]=d.color.a;logical[11]=d.opacity;
                logical[12]=d.parallax.multiplier.x;logical[13]=d.parallax.multiplier.y;logical[14]=d.parallaxOffset.x;logical[15]=d.parallaxOffset.y;
                for(int c=0;c<16;c++)
                {
                    double gpu=Math.Abs((double)actual[i*16+c]-logical[c]);
                    double error=Math.Abs((double)EvalMasterSlotCurrent(master*16+c)-logical[c]);
                    if(gpu>1e-5)counts[c]++;
                    maxGpu[c]=Math.Max(maxGpu[c],gpu);
                    if(error>maxMaster[c]){maxMaster[c]=error;worst[c]=gpuLiveOwnedIndices[master];observed[c]=gpuMasterOrderObserved[master*16+c];}
                }
            }
            for(int c=0;c<16;c++)GpuLog("activation_values channel="+c+" items="+slice.MasterItems.Length+" GPU_vs_CPU_max="+maxGpu[c].ToString("R",Inv)
                +" master_vs_CPU_max="+maxMaster[c].ToString("R",Inv)+" GPU_gt_1e5="+counts[c]+" worst_index="+worst[c]
                +" worst_observed_order="+observed[c]+" performance=False scope=paused_turnon_CPU_logical_fields");
        }
        static void RunGpuActivationFault(string stage)
        {
            int depth=gpuRecoveryTestDepth;gpuRecoveryTestDepth=depth+1;
            try
            {
                var natives=new List<(scrDecoration D,global::TweenType Key,Tween Tween,float Position,bool Playing)>();
                foreach(int idx in gpuLiveOwnedIndices)
                {
                    var d=baselineObjects[idx];if(!d || d.eventTweens==null)continue;
                    foreach(var pair in d.eventTweens)
                        if(pair.Value!=null && pair.Value.active && !(pair.Value.id is DecoAnim.Rec))
                            natives.Add((d,pair.Key,pair.Value,pair.Value.Elapsed(false),pair.Value.IsPlaying()));
                }
                gpuRecoveryFaultStage=stage;StartGpuLive();
                RequireLive(gpuRecoveryFaultStage==null && !gpuLiveActive && CountUnrestoredOwned()==0,
                    "activation fault not consumed or ownership unrestored");
                foreach(var n in natives)RequireLive(n.D.eventTweens.TryGetValue(n.Key,out var t) && ReferenceEquals(t,n.Tween) && t.active
                    && BitConverter.SingleToInt32Bits(t.Elapsed(false))==BitConverter.SingleToInt32Bits(n.Position) && t.IsPlaying()==n.Playing,
                    "native CPU fallback identity/position/playing changed");
                GpuLog("activation_native_identity entries="+natives.Count+" mismatch=0 scope=paused_boundary_not_future_native_trajectory");
                GpuLog("activation_fixture stage="+stage+" returned_CPU=True unrestored=0 performance=False");
            }
            finally {gpuRecoveryFaultStage=null;gpuRecoveryTestDepth=depth;}
        }
        static void RunGpuCpuTransition(string kind)
        {
            int depth=gpuRecoveryTestDepth;gpuRecoveryTestDepth=depth+1;
            try
            {
                if(kind=="shutdown")Shutdown();
                else if(kind=="generation")CancelBuild();
                else RequireLive(GpuStep(kind),"CPU transition command failed");
                RequireLive(!gpuLiveActive && CountUnrestoredOwned()==0,"CPU transition left GPU ownership");
                GpuLog("transition_fixture kind="+kind+" returned_CPU=True unrestored=0 performance=False");
            }
            finally {gpuRecoveryTestDepth=depth;}
        }
#endif
    }
}
#endif
