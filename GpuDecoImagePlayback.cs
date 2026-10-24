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
        static bool gpuImageCandidate,gpuImageOwn=true,gpuImageUploadFault,gpuImageMatrix;
        static bool[] gpuImageEligible=Array.Empty<bool>(),gpuImageAffected=Array.Empty<bool>();
        static readonly HashSet<int> gpuImagePending=new HashSet<int>();
        static readonly List<int> gpuImagePendingScratch=new List<int>();
        static readonly Dictionary<int,HashSet<int>> gpuImagePlannedTargets=new Dictionary<int,HashSet<int>>();
        static readonly MaterialPropertyBlock gpuImageBlock=new MaterialPropertyBlock();
        static readonly float[] gpuImageFields=new float[16];
        static readonly AccessTools.FieldRef<scrVisualDecoration,Vector2> gpuImageOriginalSize=
            AccessTools.FieldRefAccess<scrVisualDecoration,Vector2>("<spriteUnscaledSize>k__BackingField");
        static long gpuImageSetters,gpuImageUploads,gpuImageFlushes,gpuImageRestores;
        static bool ImageOnlyEvent(ffxMoveDecorationsPlus f)=>gpuImageCandidate&&f.imageFilenameUsed&&!f.originalSizeUsed&&!f.smoothingUsed&&!f.cropPivotOffsetUsed;
        static void ClearGpuImagePlaybackMap()
        {
            gpuImageEligible=Array.Empty<bool>();gpuImageAffected=Array.Empty<bool>();gpuImagePending.Clear();gpuImagePendingScratch.Clear();gpuImagePlannedTargets.Clear();
        }
        static void PrepareGpuImageAdmission()
        {
            gpuImagePending.Clear();gpuImageEligible=Array.Empty<bool>();
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
            gpuImagePending.Clear();gpuImageSetters=gpuImageUploads=gpuImageFlushes=gpuImageRestores=0;
            if(!gpuImageCandidate||!gpuImageOwn)return;
            for(int m=0;m<gpuLiveOwnedIndices.Length;m++)
            {
                if(GpuCpuFallback(m)||!GpuImageActor(gpuLiveOwnedIndices[m]))continue;
                var d=baselineObjects[gpuLiveOwnedIndices[m]] as scrVisualDecoration;
                RefreshGpuImageGeometry(m,d);
                if(LazySprite.GpuPeekPending(d,out _))gpuImagePending.Add(m);
            }
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
                if(LazySprite.GpuPeekPending(d,out _))gpuImagePending.Add(master);
                else {gpuImagePending.Remove(master);RefreshGpuImageGeometry(master,d);}
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
            try {gpuImagePending.Remove(master);RefreshGpuImageGeometry(master,d);gpuImageFlushes++;}
            catch(Exception ex){gpuImageRestores++;RecoverGpuLive("image_flush:"+ex.GetType().Name,false);throw;}
        }
        static void FlushGpuImagesBeforeVisible()
        {
            if(!gpuImageCandidate||gpuImagePending.Count==0)return;
            gpuImagePendingScratch.Clear();gpuImagePendingScratch.AddRange(gpuImagePending);
            foreach(int master in gpuImagePendingScratch)
            {
                if(!gpuLiveActive)break;
                var d=baselineObjects[gpuLiveOwnedIndices[master]] as scrVisualDecoration;
                if(!LazySprite.GpuPeekPending(d,out _)){gpuImagePending.Remove(master);continue;}
                float alpha=EvalMasterSlotCurrent(master*16+10)*EvalMasterSlotCurrent(master*16+11);
                if(alpha>0)
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
        static void LogGpuImagePlayback()=>GpuLog("image_playback candidate="+gpuImageCandidate+" own="+gpuImageOwn+" setters="+gpuImageSetters+" uploads="+gpuImageUploads+" flushes="+gpuImageFlushes+" pending="+gpuImagePending.Count+" recoveries="+gpuImageRestores+" GPU_active="+gpuLiveActive);
        // Explicit paused-boundary diagnostic only. CPU transforms are a valid
        // reference immediately after activation, before any live GPU motion.
        static void LocateGpuImageDifference()
        {
            RequireLive(gpuLiveActive&&FrameCompare.Paused&&gpuImageCandidate,"image locate requires paused activation");
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
            if(command=="images isolated"){RunGpuImageIsolatedFixture();return true;}
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
