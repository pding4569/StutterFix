#if DEV || AUTOTEST || GPUDECO
using System;
using System.Runtime.InteropServices;
using DG.Tweening;
using UnityEngine;
using LiveChannel=StutterFix.GpuDecoGpu.LiveChannel;
using Anchor=StutterFix.GpuDecoGpu.Anchor;
using EffectSegment=StutterFix.GpuDecoGpu.Segment;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        // Research only. Recovery fault injection retains channel-by-channel commits.
        static bool gpuEffectBatch = true;   // 2026-11-23 기본 켬(같은 근거, 효과 캐시와 함께)
        static void PropagateEffectToSlices(int master,int mask)
        {
            var map=gpuLiveMasterToSliceItem[master];
            for(int s=0;s<gpuLiveSlices.Count;s++)
            {
                int item=map[s];if(item<0)continue;
                var slice=gpuLiveSlices[s];
                bool anchors=slice.Gpu==null || !slice.Gpu.HasNativeLiveKernel;
                for(int c=0;c<16;c++)if((mask&(1<<c))!=0)
                {
                    slice.Channels[item*16+c]=gpuMasterChannels[master*16+c];
                    if(anchors)slice.FallbackAnchors[item*16+c]=gpuMasterAnchors[master*16+c];
                }
                slice.MarkEffectDirty(item,mask);
            }
        }
#if AUTOTEST
        static bool gpuEffectCheck;
        static long gpuEffectChecks,gpuEffectMismatches,gpuEffectWholeChecks,gpuEffectWholeMismatches;
        sealed class EffectWhole
        {
            internal LiveChannel[] Channels;
            internal Anchor[] Anchors;
            internal GpuLiveSlice[] Slices;
        }
        static EffectWhole gpuEffectWhole;
        static void BeginEffectWhole()
        {
            if(gpuEffectWhole!=null)return;
            var w=new EffectWhole{Channels=(LiveChannel[])gpuMasterChannels.Clone(),Anchors=(Anchor[])gpuMasterAnchors.Clone(),
                Slices=new GpuLiveSlice[gpuLiveSlices.Count]};
            for(int s=0;s<w.Slices.Length;s++)w.Slices[s]=gpuLiveSlices[s].EffectWholeClone();
            gpuEffectWhole=w;
        }
        static void FinishEffectWhole()
        {
            var w=gpuEffectWhole;gpuEffectWhole=null;if(w==null)return;
            bool ok=EffectBits(w.Channels,gpuMasterChannels)&&EffectBits(w.Anchors,gpuMasterAnchors)&&w.Slices.Length==gpuLiveSlices.Count;
            if(w.Slices.Length==gpuLiveSlices.Count)for(int s=0;s<w.Slices.Length;s++)ok&=gpuLiveSlices[s].EffectWholeEquals(w.Slices[s]);
            gpuEffectWholeChecks++;
            if(!ok){gpuEffectWholeMismatches++;GpuLog("effectcheck whole_mismatch checks="+gpuEffectWholeChecks);}
        }
        static bool EffectBits<T>(T[] a,T[] b) where T:struct
            =>MemoryMarshal.AsBytes(a.AsSpan()).SequenceEqual(MemoryMarshal.AsBytes(b.AsSpan()));
        static bool EffectBits<T>(Span<T> a,Span<T> b) where T:struct
            =>MemoryMarshal.AsBytes(a).SequenceEqual(MemoryMarshal.AsBytes(b));
        sealed class EffectShadow
        {
            internal LiveChannel[] Channels=new LiveChannel[16];
            internal Anchor[] Anchors=new Anchor[16];
            internal GpuLiveSlice.EffectCopy[] Slices;
            internal Vector2 Start;
            internal DecPlacementType Placement;
        }
        // Independent old 16-channel arithmetic and per-slot dirty marking on
        // copies only: never allocates sources, stamps orders or touches renderers.
        static EffectShadow CaptureEffectShadow(int master,scrDecoration d)
        {
            BeginEffectWhole();
            var e=gpuCurrentEvent;var result=new EffectShadow();
            Array.Copy(gpuMasterChannels,master*16,result.Channels,0,16);
            Array.Copy(gpuMasterAnchors,master*16,result.Anchors,0,16);
            result.Start=d.startPos;result.Placement=d.placementType;
            if(e.ResetGlobalOrigin)
            {
                var row=table.Decorations[gpuLiveOwnedIndices[master]];
                result.Start=new Vector2(row.GlobalX,row.GlobalY);result.Placement=DecPlacementType.Global;
            }
            var src=gpuCurrentAnimSource;bool anim=e.Duration>0f && src!=null;
            bool scaleUsed=!float.IsNaN(e.Values[5])||!float.IsNaN(e.Values[6])||!float.IsNaN(e.Values[12])||!float.IsNaN(e.Values[13]);
            float ease=scaleUsed?ZeroTween.Eval((Ease)e.Ease,anim?e.Duration:1f,anim?e.Duration:1f,e.Over,e.Period):1f;
            result.Slices=new GpuLiveSlice.EffectCopy[gpuLiveSlices.Count];
            for(int s=0;s<result.Slices.Length;s++)
            {int item=gpuLiveMasterToSliceItem[master][s];if(item>=0)result.Slices[s]=gpuLiveSlices[s].EffectClone(item);}
            for(int c=0;c<16;c++)
            {
                float raw=e.Values[c];if(float.IsNaN(raw))continue;
                float before=c<2&&e.RelativePosition?EvalMasterSlotCurrent(master*16+c):0f;
                float start=result.Channels[c].end,target=raw;
                if(c<2)target=(e.RelativePosition?before:(c==0?result.Start.x:result.Start.y))+raw;
                bool scale=c==5||c==6||c==12||c==13;
                float end=scale?start+(target-start)*ease:target;
                result.Channels[c]=anim?new LiveChannel{start=start,change=target-start,end=end,sourceIndex=src.Slot}
                    :new LiveChannel{start=end,change=0f,end=end,sourceIndex=-1};
                result.Anchors[c]=new Anchor{valid=1u,clock=anim?(uint)src.Slot:0u,segment=new EffectSegment{
                    duration=anim?src.Dur:0f,start=anim?start:end,end=end,target=anim?target:end,
                    over=anim?src.Over:0f,period=anim?src.Period:0f,ease=anim?(uint)src.Ease:1u,scale=scale?1u:0u}};
                for(int s=0;s<result.Slices.Length;s++)
                {
                    int item=gpuLiveMasterToSliceItem[master][s];if(item<0)continue;
                    var copy=result.Slices[s];
                    copy.Channels[c]=result.Channels[c];
                    var live=gpuLiveSlices[s];
                    if(live.Gpu==null||!live.Gpu.HasNativeLiveKernel)copy.Anchors[c]=result.Anchors[c];
                    copy.MarkDirty(c);
                }
            }
            return result;
        }
        static void CheckEffectShadow(int master,scrDecoration d,EffectShadow expected)
        {
            Array.Copy(expected.Channels,0,gpuEffectWhole.Channels,master*16,16);
            Array.Copy(expected.Anchors,0,gpuEffectWhole.Anchors,master*16,16);
            for(int s=0;s<expected.Slices.Length;s++)if(expected.Slices[s]!=null)gpuEffectWhole.Slices[s].EffectOverlay(expected.Slices[s]);
            var channels=new LiveChannel[16];var anchors=new Anchor[16];
            Array.Copy(gpuMasterChannels,master*16,channels,0,16);Array.Copy(gpuMasterAnchors,master*16,anchors,0,16);
            bool ok=EffectBits(channels,expected.Channels)&&EffectBits(anchors,expected.Anchors)
                && BitConverter.SingleToInt32Bits(d.startPos.x)==BitConverter.SingleToInt32Bits(expected.Start.x)
                && BitConverter.SingleToInt32Bits(d.startPos.y)==BitConverter.SingleToInt32Bits(expected.Start.y)&&d.placementType==expected.Placement;
            for(int s=0;s<expected.Slices.Length;s++)if(expected.Slices[s]!=null)ok&=gpuLiveSlices[s].EffectEquals(expected.Slices[s]);
            gpuEffectChecks++;
            if(!ok){gpuEffectMismatches++;GpuLog("effectcheck mismatch master="+master+" checks="+gpuEffectChecks);}
        }
        static bool GpuEffectCostCommand(string command)
        {
            if(command=="effectbatch on"||command=="effectbatch off")
            {
                RequireLive(!gpuLiveActive,"effectbatch toggle requires inactive GPU ownership");
                gpuEffectBatch=command.EndsWith(" on",StringComparison.Ordinal);
                GpuLog("effectbatch="+gpuEffectBatch+" default=True");return true;
            }
            if(command=="effectcheck on"||command=="effectcheck off")gpuEffectCheck=command.EndsWith(" on",StringComparison.Ordinal);
            else if(command=="effectcheck reset"){gpuEffectChecks=gpuEffectMismatches=gpuEffectWholeChecks=gpuEffectWholeMismatches=0;}
            else if(command!="effectcheck status")return false;
            GpuLog("effectcheck on="+gpuEffectCheck+" batch="+gpuEffectBatch+" cache="+gpuEffectCache+" checks="+gpuEffectChecks+" mismatch="+gpuEffectMismatches
                +" whole_checks="+gpuEffectWholeChecks+" whole_mismatch="+gpuEffectWholeMismatches+" performance=False");
            return true;
        }
#endif
    }
}
#endif
