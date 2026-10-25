#if AUTOTEST
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        static bool gpuImageCandidate,gpuImageOwn=true,gpuImageUploadFault,gpuImageMatrix,gpuImageRefreshPose;
        static bool[] gpuImageEligible=Array.Empty<bool>(),gpuImageAffected=Array.Empty<bool>();
        static readonly GpuDecoImageWakeQueue gpuImageWake=new GpuDecoImageWakeQueue();
        static HashSet<int> gpuImagePending=>gpuImageWake.Pending;
        static HashSet<int> gpuImageAlphaWatch=>gpuImageWake.Watching;
        // Read the existing event queue before FlushDirty consumes it. No patch
        // or mutation of the concurrently owned GpuDecoExport.cs is needed.
        static readonly AccessTools.FieldRef<GpuLiveSlice,List<int>> gpuImageAlphaEvents=
            AccessTools.FieldRefAccess<GpuLiveSlice,List<int>>("pendingAlpha");
        static readonly List<int> gpuImagePendingScratch=new List<int>();
        static readonly Dictionary<int,HashSet<int>> gpuImagePlannedTargets=new Dictionary<int,HashSet<int>>();
        static readonly MaterialPropertyBlock gpuImageBlock=new MaterialPropertyBlock();
        static Dictionary<int,Vector4> gpuImageCollapseAddresses;
        static Dictionary<int,Texture2D> gpuImageCollapseRegisteredTextures;
        static ComputeBuffer gpuImageCollapseAddressOwner;
        static readonly Vector4[] gpuImageCollapseTextureUpload=new Vector4[2];
        // Hooks for PrepareDrawCollapse's owner. Register before allocating
        // arrays, then retain the addresses AFTER allocating the texture buffer.
        static void RegisterGpuImageCollapseVariants(Dictionary<int,Texture2D> textures)
        {
            gpuImageCollapseRegisteredTextures=null;
            if(!gpuImageCandidate||!gpuImageOwn||gpuImageVariants==null)return;
            foreach(var v in gpuImageVariants)
            {
                if(v.Status!=GpuDecoImageVariant.Kind.ReadyQuad)continue;
                var sprite=v.Source?.sprite;var t=sprite?sprite.texture:null;
                RequireLive(t&&t.mipmapCount==1,"image collapse variant texture/mips unsupported");
                // The original V15 event setter/Defer applies Clamp. Preparation
                // must not change a shared texture's wrapping ahead of its event.
                textures[t.GetInstanceID()]=t;
            }
            // Keep the complete union, not just the addresses admitted by the
            // renderer's bounded texture banks. Future image textures may be
            // absent from every current item at preparation time.
            gpuImageCollapseRegisteredTextures=textures;
        }
        static void RememberGpuImageCollapseAddresses(Dictionary<int,Vector4> addresses)
        {
            gpuImageCollapseAddresses=null;gpuImageCollapseAddressOwner=null;
            if(gpuImageCandidate&&gpuImageOwn)
            {
                RequireLive(gpuImageCollapseRegisteredTextures!=null,"image collapse texture registry missing");
                int missing=0,missingDirect=0,missingArray=0,directRequired=0,variantMissing=0;
                foreach(var pair in gpuImageCollapseRegisteredTextures)
                {
                    var t=pair.Value;bool direct=CollapseSize(t)==0;
                    if(direct)directRequired++;
                    if(addresses.ContainsKey(pair.Key))continue;
                    missing++;if(direct)missingDirect++;else missingArray++;
                    if(missing<=8)GpuLog("GPUIMG_DIAG mode=collapsecoverage_missing texture_id="+pair.Key
                        +" width="+t.width+" height="+t.height+" format="+t.graphicsFormat+" tier="+CollapseSize(t));
                }
                foreach(var v in gpuImageVariants)
                    if(v.Status==GpuDecoImageVariant.Kind.ReadyQuad&&!addresses.ContainsKey(v.TextureId))variantMissing++;
                bool valid=missing==0&&variantMissing==0;
                GpuLog("GPUIMG_DIAG mode=collapsecoverage registered="+gpuImageCollapseRegisteredTextures.Count
                    +" addressed="+addresses.Count+" missing="+missing+" missing_direct="+missingDirect+" missing_array="+missingArray
                    +" direct_required="+directRequired+" direct_limit="+DrawCollapsePolicy.DirectBindings
                    +" arrays_used="+gpuCollapseArrays.Count+" array_limit="+DrawCollapsePolicy.ArrayBindings
                    +" variant_entries_missing="+variantMissing+" valid="+valid+" stage=prepare performance=False");
                if(!valid)GpuLog("IMAGEOWN=FAIL fps_before=NA fps_after=NA gain_pct=NA pixel_max=NA reason=COLLAPSE_TEXTURE_COVERAGE");
                // Fail before StartGpuLive detaches any CPU records. Do not hide
                // overflow by skipping updates or relaxing the fallback gate.
                RequireLive(valid,"image collapse texture coverage incomplete; inspect GPUIMG_DIAG and change texture-bank preparation before retry");
            }
            gpuImageCollapseAddresses=addresses;gpuImageCollapseAddressOwner=gpuCollapseTexture;
        }
        static readonly float[] gpuImageFields=new float[16];
        static readonly AccessTools.FieldRef<scrVisualDecoration,Vector2> gpuImageOriginalSize=
            AccessTools.FieldRefAccess<scrVisualDecoration,Vector2>("<spriteUnscaledSize>k__BackingField");
        static long gpuImageSetters,gpuImageUploads,gpuImageFlushes,gpuImageRestores,gpuImagePoseUpdates,gpuImageAlphaChecks,gpuImageAlphaDirty;
        static bool ImageOnlyEvent(ffxMoveDecorationsPlus f)=>gpuImageCandidate&&f.imageFilenameUsed&&!f.originalSizeUsed&&!f.smoothingUsed&&!f.cropPivotOffsetUsed;
        static void ClearGpuImagePlaybackMap()
        {
            StopGpuImageVertexDiagnostic(false);
            gpuImageRefreshPose=false;
            gpuImageCollapseAddresses=null;gpuImageCollapseAddressOwner=null;gpuImageCollapseRegisteredTextures=null;
            gpuImageEligible=Array.Empty<bool>();gpuImageAffected=Array.Empty<bool>();gpuImageWake.Clear();gpuImagePendingScratch.Clear();gpuImagePlannedTargets.Clear();
        }
        static void PrepareGpuImageAdmission()
        {
            gpuImageWake.Clear();gpuImageEligible=Array.Empty<bool>();
            if(!gpuImageCandidate)return;
            RequireLive(gpuScope.WholeSong&&GpuDecoNativeBatch.Enabled&&!GpuDecoNativeBatch.StaticCombine&&GpuDecoCopy.Enabled,
                "image playback requires song scope and independent native copy");
            RequireLive(gpuImageTimeline!=null&&gpuImageVariants!=null&&gpuImageTimeline.RepeatedSources==0&&!gpuImageShadow,"compile unique image variants before GPU preparation");
            gpuImageEligible=new bool[baselineObjects.Length];var affected=gpuImageAffected=new bool[baselineObjects.Length];var bad=new bool[baselineObjects.Length];
            gpuImageSourceMap.Clear();gpuImagePlannedTargets.Clear();
            for(int e=0;e<gpuImageTimeline.EventCount;e++)
            {
                var ev=gpuImageTimeline.Event(e);var targets=gpuImageTimeline.Targets(e);
                gpuImageSourceMap.Add(ev.Source,e);gpuImagePlannedTargets.Add(ev.Source,new HashSet<int>(targets.ToArray()));
                var v=gpuImageVariants[ev.Selector];
                foreach(int index in targets)
                {
                    affected[index]=true;
                    var d=baselineObjects[index] as scrVisualDecoration;
                    if(ev.VisualFlags!=1||v.Status!=GpuDecoImageVariant.Kind.ReadyQuad||!d||!d.spriteRenderer||!d.spriteRenderer.sprite
                        ||gpuImageOriginalSize(d)==Vector2.zero||d.hitbox!=0||LazySprite.GpuPeekPending(d,out _)) {bad[index]=true;continue;}
                    var tri=d.spriteRenderer.sprite.triangles;
                    if(tri.Length!=v.Triangles.Length||!tri.SequenceEqual(v.Triangles))bad[index]=true;
                }
            }
            for(int i=0;i<affected.Length;i++)gpuImageEligible[i]=affected[i]&&!bad[i];
            gpuImageWake.Reserve(gpuImageEligible.Count(x=>x));
            gpuImagePendingScratch.Capacity=Math.Max(gpuImagePendingScratch.Capacity,baselineObjects.Length);
            GpuLog("image_admission affected="+affected.Count(x=>x)+" eligible="+gpuImageEligible.Count(x=>x)+" excluded="+bad.Count(x=>x)+" null_argument_policy=CPU");
        }
        static bool GpuImageAdmit(int index)=>!gpuImageCandidate||!gpuImageAffected[index]||gpuImageEligible[index];
        static bool GpuImageActor(int index)=>(uint)index<(uint)gpuImageEligible.Length&&gpuImageEligible[index];
        static void PreflightGpuImageTarget(ffxMoveDecorationsPlus fx,scrDecoration d,int master)
        {
            if(!fx.imageFilenameUsed)return;
            RequireLive(gpuImageCandidate&&gpuImageOwn&&ImageOnlyEvent(fx)&&GpuImageActor(gpuLiveOwnedIndices[master]),"unprepared GPU image target");
            RequireLive(gpuImageSourceMap.TryGetValue(fx.GetInstanceID(),out int slot)
                &&gpuImageTimeline.HeaderMatches(slot,FastMove.GpuImageSelector(fx),1)
                &&gpuImagePlannedTargets[fx.GetInstanceID()].Contains(gpuLiveOwnedIndices[master]),"image event changed after preparation");
            var v=gpuImageVariants[gpuImageTimeline.Event(slot).Selector];
            RequireLive(FastMove.TryGpuImageSprite(FastMove.GpuImageSelector(fx),out var source)&&ReferenceEquals(source,v.Source)
                &&source!=null&&source.sprite&&source.sprite.GetInstanceID()==v.SpriteId&&source.sprite.texture&&source.sprite.texture.GetInstanceID()==v.TextureId,
                "image asset changed after preparation");
        }
        static void SyncGpuImageFields(int master,scrVisualDecoration d)
        {
            for(int c=0;c<16;c++)
            {
                gpuImageFields[c]=EvalMasterSlotCurrent(master*16+c);
                RequireLive(DecoBakeCore.Finite(gpuImageFields[c]),"nonfinite image logical state");
            }
            DecoAnim.CopyGpuImageFields(d,gpuImageFields);
            InvisibleSkip.SyncGpuImageColor(d,new Color(gpuImageFields[7],gpuImageFields[8],gpuImageFields[9],gpuImageFields[10]*gpuImageFields[11]));
        }
        static void RefreshGpuImageGeometry(int master,scrVisualDecoration d)
        {
            // Explicit diagnostic A only; no per-frame Transform writes.
            // The logical fields must already be current before this call.
            if(gpuImageRefreshPose){d.UpdatePosition();gpuImagePoseUpdates++;}
            var source=d.spriteRenderer;
            RequireLive(source&&source.sprite&&source.sprite.texture&&!source.sprite.associatedAlphaSplitTexture,"GPU image missing physical sprite");
            var variant=GpuDecoImageVariant.CaptureSprite(source.sprite);
            RequireLive(variant.Status==GpuDecoImageVariant.Kind.ReadyQuad,"GPU image no longer a quad");
            var next=ImageFixtureItem(d,variant,out var geometry);
            for(int s=0;s<gpuLiveSlices.Count;s++)
            {
                var slice=gpuLiveSlices[s];int item=gpuLiveMasterToSliceItem[master][s];
                if(item<0)continue;
                RequireLive(slice.Independent&&slice.Gpu!=null&&slice.Gpu.HasNativeLiveKernel&&slice.Renderers.Count==slice.Data.Items.Length,"GPU image renderer mapping");
                var prior=slice.Data.Items[item];var renderer=slice.Renderers[item];
                RequireLive(renderer&&source.sharedMaterial&&renderer.sharedMaterial
                    &&source.sharedMaterial.shader==renderer.sharedMaterial.shader
                    &&source.sharedMaterial.renderQueue==renderer.sharedMaterial.renderQueue
                    &&source.sortingLayerID==renderer.sortingLayerID&&source.sortingOrder==renderer.sortingOrder,"GPU image material/order changed");
                next.Index=prior.Index;next.Id=prior.Id;next.MaterialId=source.sharedMaterial.GetInstanceID();
                next.Initial=prior.Initial;next.Tracks=prior.Tracks;next.Anchors=prior.Anchors;
                if(gpuImageMatrix)
                {
                    RequireLive(slice.Gpu.HasImageGeometry&&geometry.SupportsImageMatrix,"image matrix chain unsupported");
                    slice.Gpu.UpdateImageMatrix(item,geometry.ImageMatrix,variant.Vertices,source.flipX,source.flipY);
                    next.Flags|=64;
                }
                slice.Gpu.UpdateImageGeometry(item,next);
                UpdateDrawCollapseTextureSlot(item,source.sprite.texture,source.sharedMaterial);
                if(gpuImageUploadFault){gpuImageUploadFault=false;throw new InvalidOperationException("injected_image_upload");}
                renderer.sharedMaterial=source.sharedMaterial;
                gpuImageBlock.Clear();renderer.GetPropertyBlock(gpuImageBlock,0);
                gpuImageBlock.SetTexture(Shader.PropertyToID("_MainTex"),source.sprite.texture);
                gpuImageBlock.SetColor(Shader.PropertyToID("_RendererColor"),Color.white);
                gpuImageBlock.SetVector(Shader.PropertyToID("_Flip"),Vector4.one);
                renderer.SetPropertyBlock(gpuImageBlock,0);
                gpuImageUploads++;
            }
        }
        static void CaptureGpuImageActivation()
        {
            StopGpuImageVertexDiagnostic(false);
            gpuImageWake.Clear();gpuImageSetters=gpuImageUploads=gpuImageFlushes=gpuImageRestores=gpuImagePoseUpdates=gpuImageAlphaChecks=gpuImageAlphaDirty=0;
            if(!gpuImageCandidate||!gpuImageOwn)return;
            for(int m=0;m<gpuLiveOwnedIndices.Length;m++)
            {
                if(GpuCpuFallback(m)||!GpuImageActor(gpuLiveOwnedIndices[m]))continue;
                var d=baselineObjects[gpuLiveOwnedIndices[m]] as scrVisualDecoration;
                RefreshGpuImageGeometry(m,d);
                if(LazySprite.GpuPeekPending(d,out _))gpuImageWake.Defer(m);
            }
            GpuLog("GPUIMG_DIAG stage=activation refreshpose="+gpuImageRefreshPose+" pose_updates="+gpuImagePoseUpdates+" pixel_max=NA performance=False");
        }
        // Called only AFTER the scalar target was committed. Never rerun that
        // target's scalar effect if the separate image upload fails.
        internal static void RunGpuImageEffect(ffxMoveDecorationsPlus fx,scrDecoration decoration)
        {
            if(!gpuImageCandidate||!gpuLiveActive||!fx.imageFilenameUsed||!gpuLiveMasterByDeco.TryGetValue(decoration,out int master)||GpuCpuFallback(master))return;
            var d=(scrVisualDecoration)decoration;int stage=0;
            try
            {
                SyncGpuImageFields(master,d);
                stage=1;FastMove.RunGpuImageBlock(fx,d);gpuImageSetters++;stage=2;
                if(!gpuLiveActive)return;
                if(LazySprite.GpuPeekPending(d,out _))gpuImageWake.Defer(master);
                else {gpuImageWake.Remove(master);RefreshGpuImageGeometry(master,d);}
            }
            catch(Exception ex)
            {
                gpuImageRestores++;RecoverGpuLive("image_stage"+stage+":"+ex.GetType().Name,false);
                if(stage==0){FastMove.RunGpuImageBlock(fx,d);gpuImageSetters++;}
                else if(stage==1)throw; // Original setter failed: do not invoke it twice.
            }
            finally {if(gpuLiveActive&&d.spriteRenderer)d.spriteRenderer.forceRenderingOff=true;}
        }
        internal static void GpuImageSpriteFlushed(scrVisualDecoration d)
        {
            if(!gpuImageCandidate||!gpuLiveActive||!gpuLiveMasterByDeco.TryGetValue(d,out int master)||GpuCpuFallback(master)||!GpuImageActor(gpuLiveOwnedIndices[master]))return;
            try {gpuImageWake.Remove(master);RefreshGpuImageGeometry(master,d);gpuImageFlushes++;}
            catch(Exception ex){gpuImageRestores++;RecoverGpuLive("image_flush:"+ex.GetType().Name,false);throw;}
        }
        internal static void GpuImageSpriteWillFlush(scrVisualDecoration d)
        {
            if(!gpuImageCandidate||!gpuLiveActive||!gpuLiveMasterByDeco.TryGetValue(d,out int master)
                ||GpuCpuFallback(master)||!GpuImageActor(gpuLiveOwnedIndices[master]))return;
            try {SyncGpuImageFields(master,d);}
            catch(Exception ex)
            {
                gpuImageRestores++;RecoverGpuLive("image_preflush:"+ex.GetType().Name,false);
                // LazySprite removed this pending entry already. Continue with
                // its original setter exactly once, now under CPU ownership.
            }
        }
        static void FlushGpuImagesBeforeVisible()
        {
            if(!gpuImageCandidate||gpuImagePending.Count==0)return;
            foreach(var slice in gpuLiveSlices)
                foreach(int item in gpuImageAlphaEvents(slice))
                {
                    int master=slice.MasterItems[item];
                    if(gpuImageWake.AlphaDirty(master))gpuImageAlphaDirty++;
                }
            // A dirty start may still have alpha=0. Keep ONLY pending images
            // with animated alpha until they become visible or static. Looking
            // only at MarkDirty would miss the following frame's 0 -> positive.
            gpuImageWake.Snapshot(gpuImagePendingScratch);
            foreach(int master in gpuImagePendingScratch)
            {
                if(!gpuLiveActive)break;
                if(GpuCpuFallback(master)||!gpuImagePending.Contains(master)){gpuImageAlphaWatch.Remove(master);continue;}
                var d=baselineObjects[gpuLiveOwnedIndices[master]] as scrVisualDecoration;
                if(!LazySprite.GpuPeekPending(d,out _)){gpuImageWake.Remove(master);continue;}
                gpuImageAlphaChecks++;
                float alpha=EvalMasterSlotCurrent(master*16+10)*EvalMasterSlotCurrent(master*16+11);
                bool animated=gpuMasterChannels[master*16+10].sourceIndex>=0||gpuMasterChannels[master*16+11].sourceIndex>=0;
                if(gpuImageWake.ShouldFlush(master,alpha,animated))
                {
                    SyncGpuImageFields(master,d);
                    if(LazySprite.GpuPeekPending(d,out _))LazySprite.Flush(d);
                }
            }
        }
        static void AuditGpuImagePixels()
        {
            RequireLive(gpuLiveActive&&FrameCompare.Paused&&gpuImageCandidate,"image audit requires paused active GPU");
            int visible=0,poseBad=0,colorBad=0,uvBad=0,materialBad=0,raw=0;float maxWorld=0,maxDegrees=0;
            var examples=new List<string>();
            for(int s=0;s<gpuLiveSlices.Count;s++)
            {
                var slice=gpuLiveSlices[s];var points=slice.Gpu.ReadImageVertices();
                for(int i=0;i<slice.MasterItems.Length;i++)
                {
                    int master=slice.MasterItems[i];if(GpuCpuFallback(master)||!GpuImageActor(gpuLiveOwnedIndices[master]))continue;
                    var d=(scrVisualDecoration)baselineObjects[gpuLiveOwnedIndices[master]];var r=d.spriteRenderer;
                    if(r.color.a<=0)continue;visible++;raw++;
                    var corners=r.sprite.vertices;var uv=r.sprite.uv;float error=0;
                    bool col=false,tex=false;
                    for(int k=0;k<4;k++)
                    {
                        var local=corners[k];if(r.flipX)local.x=-local.x;if(r.flipY)local.y=-local.y;
                        var expected=r.transform.TransformPoint(local);var point=points[i*4+k];
                        error=Mathf.Max(error,Vector3.Distance(expected,point.position));
                        if(!GpuDecoGpu.DrawColor(point.color).Equals(GpuDecoGpu.DrawColor(r.color)))col=true;
                        if(!point.uv.Equals(uv[k]))tex=true;
                    }
                    if(col)colorBad++;if(tex)uvBad++;
                    if(error>1e-5f){poseBad++;if(examples.Count<8)examples.Add(gpuLiveOwnedIndices[master]+":"+error.ToString("R",Inv)+":"+d.rotAngle.ToString("R",Inv));}
                    maxWorld=Mathf.Max(maxWorld,error);maxDegrees=Mathf.Max(maxDegrees,Mathf.Abs(d.rotAngle));
                    gpuImageBlock.Clear();slice.Renderers[i].GetPropertyBlock(gpuImageBlock,0);
                    if(gpuImageBlock.GetTexture(Shader.PropertyToID("_MainTex"))!=r.sprite.texture||slice.Renderers[i].sharedMaterial!=r.sharedMaterial)materialBad++;
                }
            }
            GpuLog("image_audit visible="+visible+" pose_error_gt_1e5="+poseBad+" max_world_error="+maxWorld.ToString("R",Inv)
                +" color8_mismatch="+colorBad+" UV_mismatch="+uvBad+" material_texture_mismatch="+materialBad+" max_abs_degrees="+maxDegrees.ToString("R",Inv)
                +" examples_index_error_degrees="+string.Join(",",examples)+" scope=boundary_CPU_transform_not_live_reference performance=False");
        }
        static void LogGpuImagePlayback()=>GpuLog("image_playback candidate="+gpuImageCandidate+" own="+gpuImageOwn+" setters="+gpuImageSetters+" uploads="+gpuImageUploads+" flushes="+gpuImageFlushes+" pending="+gpuImagePending.Count+" recoveries="+gpuImageRestores+" GPU_active="+gpuLiveActive
            +" alpha_watch="+gpuImageAlphaWatch.Count+" alpha_checks="+gpuImageAlphaChecks+" alpha_dirty="+gpuImageAlphaDirty+" refreshpose="+gpuImageRefreshPose+" pose_updates="+gpuImagePoseUpdates);
        // Explicit paused-boundary diagnostic only. CPU transforms are a valid
        // reference immediately after activation, before any live GPU motion.
        static void LocateGpuImageDifference(bool cpuVertices=false)
        {
            RequireLive(gpuLiveActive&&FrameCompare.Paused&&gpuImageCandidate,"image locate requires paused activation");
            if(cpuVertices){StartGpuImageVertexDiagnostic();return;}
            var regions=new[]{new Rect(1566,798,68,36),new Rect(254,638,68,36)};
            var cameras=Camera.allCameras;int matches=0;
            foreach(var slice in gpuLiveSlices)
            {
                var points=slice.Gpu.ReadImageVertices();
                for(int i=0;i<slice.MasterItems.Length;i++)
                {
                    int master=slice.MasterItems[i];if(GpuCpuFallback(master)||!GpuImageActor(gpuLiveOwnedIndices[master]))continue;
                    var d=(scrVisualDecoration)baselineObjects[gpuLiveOwnedIndices[master]];var r=d.spriteRenderer;
                    if(r.color.a<=0||!r.enabled||!r.gameObject.activeInHierarchy)continue;
                    var local=r.sprite.vertices;
                    foreach(var camera in cameras)
                    {
                        if(!camera.enabled||!camera.targetTexture||(camera.cullingMask&(1<<r.gameObject.layer))==0)continue;
                        Vector2 lo=new Vector2(float.PositiveInfinity,float.PositiveInfinity),hi=-lo;float screenError=0;
                        for(int k=0;k<4;k++)
                        {
                            Vector3 v=local[k];if(r.flipX)v.x=-v.x;if(r.flipY)v.y=-v.y;
                            Vector2 a=camera.WorldToScreenPoint(r.transform.TransformPoint(v)),b=camera.WorldToScreenPoint(points[i*4+k].position);
                            lo=Vector2.Min(lo,Vector2.Min(a,b));hi=Vector2.Max(hi,Vector2.Max(a,b));screenError=Mathf.Max(screenError,Vector2.Distance(a,b));
                        }
                        var rect=Rect.MinMaxRect(lo.x,lo.y,hi.x,hi.y);if(!regions.Any(x=>x.Overlaps(rect)))continue;
                        matches++;var item=slice.Data.Items[i];
                        string V(Vector3 v)=>v.x.ToString("R",Inv)+","+v.y.ToString("R",Inv)+","+v.z.ToString("R",Inv);
                        var matrix=r.localToWorldMatrix;
                        GpuLog("image_locate index="+gpuLiveOwnedIndices[master]+" camera_id="+camera.GetInstanceID()+" layer="+r.gameObject.layer+" order="+r.sortingOrder
                            +" rect="+V(lo)+";"+V(hi)+" screen_error="+screenError.ToString("R",Inv)+" angle="+d.rotAngle.ToString("R",Inv)
                            +" position="+V(d.pivotTrans.position)+" scale="+V(d.pivotTrans.localScale)+" child_offset="+V(d.childTransform.localPosition)
                            +" matrix="+V(matrix.GetColumn(0))+";"+V(matrix.GetColumn(1))+";"+V(matrix.GetColumn(3))
                            +" raw_corners="+string.Join(";",local.Select(x=>V(x)))+" gpu_corners="+string.Join(";",Enumerable.Range(0,4).Select(k=>V(points[i*4+k].position)))
                            +" color="+r.color.ToString("R")+" constants="+string.Join(",",item.Constants.Select(x=>x.ToString("R",Inv)))+" flags="+item.Flags);
                    }
                }
            }
            GpuLog("image_locate matches="+matches+" scope=explicit_activation_boundary performance=False");
        }
        static bool GpuImagePlaybackCommand(string command)
        {
            if(GpuImageDiagnosticCommand(command))return true;
            if(command=="images matrix on"||command=="images matrix off")
            {RequireLive(FrameCompare.Paused&&!gpuLiveActive,"image matrix toggle requires paused CPU");gpuImageMatrix=command.EndsWith(" on",StringComparison.Ordinal);return true;}
            if(command=="images playback on"||command=="images playback off")
            {
                RequireLive(!ready&&!gpuLiveActive&&!gpuLivePrepared,"select image candidate before map snapshot");
                gpuImageCandidate=command.EndsWith(" on",StringComparison.Ordinal);gpuImageOwn=true;LogGpuImagePlayback();return true;
            }
            if(command=="images own on"||command=="images own off")
            {
                RequireLive(FrameCompare.Paused&&!gpuLiveActive&&gpuImageCandidate,"image ownership toggle requires paused CPU");
                gpuImageOwn=command.EndsWith(" on",StringComparison.Ordinal);LogGpuImagePlayback();return true;
            }
            if(command=="images playback state"){LogGpuImagePlayback();return true;}
            if(command=="images audit"){AuditGpuImagePixels();return true;}
            if(command=="images locate"){LocateGpuImageDifference();return true;}
            if(command=="images isolated"){throw new NotSupportedException("images isolated: unfinished draft (excluded from build)");}
            if(command=="images requireactive")
            {LogGpuImagePlayback();RequireLive(gpuLiveActive&&gpuImageSetters>0&&gpuImageRestores==0&&GpuDecoCopy.ReadTotals().Errors==0,"GPU image window not retained");return true;}
            if(command=="images requirefallback")
            {LogGpuImagePlayback();RequireLive(!gpuLiveActive&&gpuImageRestores>0&&gpuLastRestoreComplete,"GPU image failure did not restore CPU");return true;}
            if(command=="images failupload")
            {RequireLive(gpuLiveActive&&gpuImageCandidate,"active image candidate required");gpuImageUploadFault=true;return true;}
            return false;
        }
    }
}
#endif
