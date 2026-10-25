#if DEV || AUTOTEST || GPUDECO
using System;
using System.Collections.Generic;
using UnityEngine;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        static List<DecoAnim.GpuResumeSpec> BuildGpuResumePlan(out int unobservedOrder)
        {
            // Validate every group BEFORE relinquishing GPU ownership. Never
            // manufacture a color/vector tween from unrelated source clocks.
            var result=new List<DecoAnim.GpuResumeSpec>();unobservedOrder=0;
            for(int m=0;m<gpuLiveOwnedIndices.Length;m++)
            {
                if(GpuCpuFallback(m))continue;
                var d=baselineObjects[gpuLiveOwnedIndices[m]] as scrVisualDecoration;
                if(!d)continue;
                foreach(int first in gpuRecFirstChannels)
                {
                    int n=first==7?4:first==12?2:1,at=m*16+first,source=-1;
                    for(int c=0;c<n;c++)
                    {
                        int s=gpuMasterChannels[at+c].sourceIndex;
                        if(s>=0){if(source>=0 && source!=s)throw new InvalidOperationException("GPU resume mixed source group");source=s;}
                    }
                    if(source<0)continue;
                    var clock=gpuSourcePool[source];
                    RequireLive(clock.Active && clock.Pos>=0f && clock.Pos<clock.Dur && DecoBakeCore.Finite(clock.Pos)
                        && DecoBakeCore.Finite(clock.Dur) && DecoBakeCore.Finite(clock.Over) && DecoBakeCore.Finite(clock.Period)
                        && GpuDecoGpu.Supported((int)clock.Ease),"GPU resume source contract");
                    int key=first<=4?first+1:first==5?7:first==6?8:first==7?9:first==11?10:first==12?11:first-2;
                    var spec=new DecoAnim.GpuResumeSpec {D=d,Key=key,Source=clock.SourceId,StartFrame=clock.StartFrame,
                        StartSong=clock.StartSong,Generation=Generation,CaptureEpoch=gpuCaptureEpoch,
                        Duration=clock.Dur,Position=clock.Pos,Ease=clock.Ease,Over=clock.Over,Period=clock.Period,
                        Order=gpuMasterRecOrder[at],EngineOrder=gpuMasterEngineOrder[at],OrderObserved=gpuMasterOrderObserved[at]};
                    if(spec.Order==0 || !spec.OrderObserved)
                    {
                        // Table-anchored native tweens have no observed Rec
                        // insertion order. Report, rather than claiming identity.
                        unobservedOrder++;spec.OrderObserved=false;
                        if(spec.Order==0){spec.Order=DecoAnim.NextGpuCreationOrder();spec.EngineOrder=DecoEngine.NextOrder();}
                    }
                    for(int c=0;c<n;c++)
                    {
                        var channel=gpuMasterChannels[at+c];var anchor=gpuMasterAnchors[at+c];
                        RequireLive(channel.sourceIndex==source && gpuMasterSlotSource[at+c]==source && anchor.valid!=0
                            && DecoBakeCore.Finite(channel.start) && DecoBakeCore.Finite(channel.change)
                            && DecoBakeCore.Finite(anchor.segment.target),"GPU resume channel contract");
                        int component=key==8?1:c;
                        spec.Start[component]=channel.start;spec.Target[component]=anchor.segment.target;spec.Change[component]=channel.change;
                    }
                    if(d.eventTweens.TryGetValue((global::TweenType)key,out var old) && old?.id is DecoAnim.Rec r && r.Running)
                        throw new InvalidOperationException("GPU resume ownership changed externally");
                    result.Add(spec);
                }
            }
            result.Sort((a,b)=>a.Order.CompareTo(b.Order));
            return result;
        }
    }
}
#endif
