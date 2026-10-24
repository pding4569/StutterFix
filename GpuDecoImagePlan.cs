#if AUTOTEST
using System;
using System.Collections.Generic;
using UnityEngine;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        static GpuDecoImageTimeline gpuImageTimeline;
        static GpuDecoImageVariant[] gpuImageVariants;
        static bool gpuImageShadow;
        static readonly Dictionary<int,int> gpuImageSourceMap=new Dictionary<int,int>();
        static readonly Dictionary<scrDecoration,int> gpuImageTargetMap=new Dictionary<scrDecoration,int>();
        static long gpuImageObserved,gpuImageMatched,gpuImageUnknown,gpuImageMismatch,gpuImageErrors;
        static long gpuImageAssetChecks,gpuImageAssetMismatch,gpuImagePost,gpuImageImmediate,gpuImageDeferred,gpuImagePostMismatch,gpuImageNullArgs,gpuImagePostUnknown;
        static long gpuImageClamp,gpuImageOtherWrap;
        static double gpuImageFirstBad=double.NaN;
        static double gpuImageFirstAssetBad=double.NaN,gpuImageFirstPostBad=double.NaN;
        internal static bool GpuImageShadowActive=>gpuImageShadow;
        static void ClearGpuImagePlan()
        {
            ClearGpuImagePlaybackMap();
            gpuImageShadow=false;gpuImageTimeline=null;gpuImageVariants=null;gpuImageSourceMap.Clear();gpuImageTargetMap.Clear();
        }
        // Read-only, explicit shadow. Exceptions must never change the original
        // effect's control flow. Only calls routed through FastMove.Run observed.
        internal static void ObserveGpuImagePlan(ffxMoveDecorationsPlus fx,IReadOnlyList<scrDecoration> targets)
        {
            if(!gpuImageShadow||!fx.imageFilenameUsed)return;
            gpuImageObserved++;
            try
            {
                bool good=false;
                if(!gpuImageSourceMap.TryGetValue(fx.GetInstanceID(),out int slot))gpuImageUnknown++;
                else
                {
                    int flags=(fx.imageFilenameUsed?1:0)|(fx.originalSizeUsed?2:0)|(fx.smoothingUsed?4:0)|(fx.cropPivotOffsetUsed?8:0);
                    var planned=gpuImageTimeline.Targets(slot);
                    good=gpuImageTimeline.HeaderMatches(slot,FastMove.GpuImageSelector(fx),flags)&&planned.Length==targets.Count;
                    for(int i=0;good&&i<planned.Length;i++)good=gpuImageTargetMap.TryGetValue(targets[i],out int d)&&d==planned[i];
                    if(good)
                    {
                        gpuImageMatched++;gpuImageAssetChecks++;
                        var entry=gpuImageTimeline.Event(slot);
                        if(!gpuImageVariants[entry.Selector].SameAsset(GpuDecoImageVariant.Capture(FastMove.GpuImageSelector(fx))))
                        {gpuImageAssetMismatch++;if(double.IsNaN(gpuImageFirstAssetBad))gpuImageFirstAssetBad=scrConductor.instance?scrConductor.instance.songposition_minusi:double.NaN;}
                    }
                    else gpuImageMismatch++;
                }
                if(!good&&double.IsNaN(gpuImageFirstBad))gpuImageFirstBad=scrConductor.instance?scrConductor.instance.songposition_minusi:double.NaN;
            }
            catch {gpuImageErrors++;gpuImageShadow=false;}
        }
        // Observe the result of the original image assignment, before later
        // size/smoothing/masking blocks. No Flush, SetSprite or Transform calls.
        internal static void ObserveGpuImageResult(ffxMoveDecorationsPlus fx,scrVisualDecoration d,TextureManager.CustomSprite requested,bool deferred)
        {
            if(!gpuImageShadow)return;
            gpuImagePost++;
            try
            {
                if(!gpuImageSourceMap.TryGetValue(fx.GetInstanceID(),out int slot)){gpuImagePostUnknown++;return;}
                var v=gpuImageVariants[gpuImageTimeline.Event(slot).Selector];
                bool good=ReferenceEquals(v.Source,requested);
                if(requested==null)gpuImageNullArgs++;
                if(deferred)
                {
                    gpuImageDeferred++;
                    good&=LazySprite.GpuPeekPending(d,out var pending)&&ReferenceEquals(pending,requested);
                }
                else
                {
                    gpuImageImmediate++;
                    // Outcome is not inferred from a null argument.
                    if(requested!=null)
                    {
                        var sprite=d.spriteRenderer?d.spriteRenderer.sprite:null;
                        good&=sprite&&sprite.GetInstanceID()==v.SpriteId&&sprite.texture&&sprite.texture.GetInstanceID()==v.TextureId;
                    }
                }
                if(requested!=null&&requested.sprite&&requested.sprite.texture)
                {
                    if(requested.sprite.texture.wrapMode==TextureWrapMode.Clamp)gpuImageClamp++;else gpuImageOtherWrap++;
                }
                if(!good)
                {gpuImagePostMismatch++;if(double.IsNaN(gpuImageFirstPostBad))gpuImageFirstPostBad=scrConductor.instance?scrConductor.instance.songposition_minusi:double.NaN;}
            }
            catch {gpuImageErrors++;gpuImageShadow=false;}
        }
        static void LogGpuImageShadow()
        {
            GpuLog("image_shadow observed="+gpuImageObserved+" matched="+gpuImageMatched+" unknown_source="+gpuImageUnknown
                +" mismatch="+gpuImageMismatch+" errors="+gpuImageErrors+" first_bad_song="+gpuImageFirstBad.ToString("R",Inv)
                +" observed_nonempty="+(gpuImageObserved>0)+" scope=FastMove_Run_image_events_only CPU_image_setters_unchanged=True GPU_image_playback=False");
            GpuLog("image_results asset_checks="+gpuImageAssetChecks+" asset_mismatch="+gpuImageAssetMismatch+" post_assignments="+gpuImagePost
                +" immediate="+gpuImageImmediate+" deferred="+gpuImageDeferred+" post_mismatch="+gpuImagePostMismatch
                +" null_arguments="+gpuImageNullArgs+" unplanned_post="+gpuImagePostUnknown+" clamp_samples="+gpuImageClamp+" other_wrap_samples="+gpuImageOtherWrap
                +" first_asset_bad_song="+gpuImageFirstAssetBad.ToString("R",Inv)+" first_post_bad_song="+gpuImageFirstPostBad.ToString("R",Inv)
                +" physical_pixels=UNMEASURED scope=image_assignment_before_later_size_smoothing_mask_blocks");
        }
        static bool GpuImagePlanCommand(string command)
        {
            if(!command.StartsWith("images ",StringComparison.Ordinal))return false;
            if(GpuImagePlaybackCommand(command))return true;
            RequireLive(!gpuLiveActive&&g2Window<0,"image plan requires CPU ownership outside performance gates");
            RequireLive(ready&&baselineEvents!=null&&baselineObjects!=null,"image snapshot missing");
            ValidateBaseline();
            if(command=="images texturefixture"){RunGpuImageTextureFixture();return true;}
            if(command=="images geometryfixture"){RunGpuImageGeometryFixture();return true;}
            if(command=="images shadow off"){gpuImageShadow=false;LogGpuImageShadow();return true;}
            if(command=="images shadow state"){LogGpuImageShadow();return true;}
            if(command=="images shadow on")
            {
                RequireLive(gpuImageTimeline!=null&&gpuImageVariants!=null&&gpuImageTimeline.RepeatedSources==0,"unique resolved image plan required; do not guess repeated Source IDs");
                gpuImageShadow=false;gpuImageSourceMap.Clear();gpuImageTargetMap.Clear();
                for(int i=0;i<gpuImageTimeline.EventCount;i++)gpuImageSourceMap.Add(gpuImageTimeline.Event(i).Source,i);
                for(int i=0;i<baselineObjects.Length;i++)gpuImageTargetMap.Add(baselineObjects[i],i);
                gpuImageObserved=gpuImageMatched=gpuImageUnknown=gpuImageMismatch=gpuImageErrors=0;gpuImageFirstBad=double.NaN;
                gpuImageAssetChecks=gpuImageAssetMismatch=gpuImagePost=gpuImageImmediate=gpuImageDeferred=gpuImagePostMismatch=gpuImageNullArgs=gpuImagePostUnknown=0;
                gpuImageClamp=gpuImageOtherWrap=0;
                gpuImageFirstAssetBad=gpuImageFirstPostBad=double.NaN;
                gpuImageShadow=true;LogGpuImageShadow();return true;
            }
            RequireLive(command=="images compile"||command=="images state","images compile|state|shadow on|off|state");
            RequireLive(FrameCompare.Paused&&!gpuImageShadow,"image compilation requires paused CPU ownership with shadow off");
            if(command=="images compile")
            {
                ClearGpuImagePlan(); // Failed compilation cannot expose a prior success.
                RequireLive(ready&&baselineEvents!=null&&baselineVisualFlags!=null&&baselineImageSelectors!=null
                    &&baselineEvents.Length==baselineVisualFlags.Length&&baselineEvents.Length==baselineImageSelectors.Length,"image snapshot metadata missing");
                var input=new GpuDecoImageTimeline.Input[baselineEvents.Length];
                for(int i=0;i<input.Length;i++)
                {
                    var e=baselineEvents[i];input[i]=new GpuDecoImageTimeline.Input(i,e.Source,e.T0,baselineVisualFlags[i],baselineImageSelectors[i],e.Targets);
                }
                gpuImageTimeline=GpuDecoImageTimeline.Compile(baselineObjects.Length,input);
                var variants=new GpuDecoImageVariant[gpuImageTimeline.SelectorCount];
                for(int i=0;i<variants.Length;i++)variants[i]=GpuDecoImageVariant.Capture(gpuImageTimeline.Selector(i));
                gpuImageVariants=variants;
            }
            RequireLive(gpuImageTimeline!=null&&gpuImageVariants!=null,"resolved image plan not compiled");
            var p=gpuImageTimeline;
            GpuLog("image_tape events="+p.EventCount+" selectors="+p.SelectorCount+" target_references="+p.TargetReferences
                +" decorations="+p.DecorationsWithImages+" empty_selector_events="+p.EmptySelectorEvents+" source_id_reuses="+p.RepeatedSources
                +" payload_lower_bound_bytes="+p.PayloadBytesLowerBound+" metadata_only="+(!gpuImageCandidate)+" GPU_image_playback_candidate="+gpuImageCandidate+" admission_changed="+gpuImageCandidate);
            var counts=new int[6];foreach(var v in gpuImageVariants)counts[(int)v.Status]++;
            GpuLog("image_variants ready_quad="+counts[0]+" null_argument="+counts[1]+" missing="+counts[2]+" non_quad="+counts[3]
                +" split_alpha="+counts[4]+" invalid="+counts[5]+" borrowed_assets_destroyed=0 asset_getters_only=True");
            return true;
        }
    }
}
#endif
