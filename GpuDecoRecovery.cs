#if DEV || AUTOTEST
using System;
using DG.Tweening;
using UnityEngine;
using System.Linq;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        static bool gpuRecoveryRunning,gpuRecoveryBlocked,gpuLastRestoreComplete=true;
        struct TargetUndo
        {
            internal int Slot,Source;
            internal GpuDecoGpu.LiveChannel Channel;
            internal GpuDecoGpu.Anchor Anchor;
            internal long Order,Engine;
            internal bool Observed;
        }
        // One synchronous FastMove target at a time, no per-target allocations.
        static readonly TargetUndo[] gpuTargetUndo=new TargetUndo[16];
        static int gpuTargetUndoCount;
        static scrDecoration gpuTargetUndoDecoration;
        static DecPlacementType gpuTargetUndoPlacement;
        static Vector2 gpuTargetUndoStart;
        // Research candidate: immutable CopyEvent values, in original channel order.
        // No per-target allocations; disabled until the short comparison passes.
        static bool gpuEffectCache;
        static readonly int[] gpuEffectChannels=new int[16];
        static int gpuEffectChannelCount;
        static float gpuEffectEaseEnd=1f,gpuEffectEaseDurEnd=1f;
        static void CacheGpuEffect(DecoBakeCore.Event e,bool anim)
        {
            gpuEffectChannelCount=0;bool scale=false;
            for(int c=0;c<16;c++)if(!float.IsNaN(e.Values[c]))
            {gpuEffectChannels[gpuEffectChannelCount++]=c;scale|=c==5||c==6||c==12||c==13;}
            gpuEffectEaseEnd=!anim&&scale?ZeroTween.Eval((Ease)e.Ease,1f,1f,e.Over,e.Period):1f;
            gpuEffectEaseDurEnd=anim&&scale?ZeroTween.Eval((Ease)e.Ease,e.Duration,e.Duration,e.Over,e.Period):1f;
        }
        static partial void GpuRecoveryFault(string stage);
        static bool GpuRecoveryTesting
        {
            get
            {
#if AUTOTEST
                return gpuRecoveryTestDepth!=0;
#else
                return false;
#endif
            }
        }
        static void PrepareGpuEffect(ffxMoveDecorationsPlus fx)
        {
            if(gpuCurrentFxParsed && ReferenceEquals(fx,gpuCurrentFx))return;
            GpuRecoveryFault("preflight");
            var e=CopyEvent(fx);
            if(e.Reasons.Length!=0 || !GpuDecoGpu.Supported(e.Ease) || !DecoBakeCore.Finite(e.Over) || !DecoBakeCore.Finite(e.Period)
                || fx.colorUsed && (float.IsNaN(e.Values[7]) || float.IsNaN(e.Values[8]) || float.IsNaN(e.Values[9]) || float.IsNaN(e.Values[10]))
                || fx.parallaxUsed && (float.IsNaN(e.Values[12]) || float.IsNaN(e.Values[13])))
                throw new NotSupportedException("GPU effect outside prepared contract");
            var source=e.Duration>0f?AllocateGpuLiveSource(fx.GetInstanceID(),Time.frameCount,e.Duration,(Ease)e.Ease,e.Over,e.Period,0f):null;
            if(gpuEffectCache)CacheGpuEffect(e,e.Duration>0f&&source!=null);
            gpuCurrentFx=fx;gpuCurrentEvent=e;gpuCurrentAnimSource=source;gpuCurrentFxParsed=true;
        }
        static void SaveGpuTarget(int master,scrDecoration d)
        {
            gpuTargetUndoDecoration=d;gpuTargetUndoPlacement=d.placementType;gpuTargetUndoStart=d.startPos;gpuTargetUndoCount=0;
            for(int i=0,n=gpuEffectCache?gpuEffectChannelCount:16;i<n;i++)
            {
                int c=gpuEffectCache?gpuEffectChannels[i]:i;
                if(!gpuEffectCache&&float.IsNaN(gpuCurrentEvent.Values[c]))continue;
                int at=master*16+c;
                gpuTargetUndo[gpuTargetUndoCount++]=new TargetUndo{Slot=at,Source=gpuMasterSlotSource[at],
                    Channel=gpuMasterChannels[at],Anchor=gpuMasterAnchors[at],Order=gpuMasterRecOrder[at],
                    Engine=gpuMasterEngineOrder[at],Observed=gpuMasterOrderObserved[at]};
            }
        }
        static void RollbackGpuTarget()
        {
            for(int i=0;i<gpuTargetUndoCount;i++)
            {
                var old=gpuTargetUndo[i];int current=gpuMasterSlotSource[old.Slot];
                if(current>=0)gpuSourcePool[current].MasterSlots.Remove(old.Slot);
                gpuMasterSlotSource[old.Slot]=old.Source;
                if(old.Source>=0 && !gpuSourcePool[old.Source].MasterSlots.Contains(old.Slot))gpuSourcePool[old.Source].MasterSlots.Add(old.Slot);
                gpuMasterChannels[old.Slot]=old.Channel;gpuMasterAnchors[old.Slot]=old.Anchor;
                gpuMasterRecOrder[old.Slot]=old.Order;gpuMasterEngineOrder[old.Slot]=old.Engine;gpuMasterOrderObserved[old.Slot]=old.Observed;
            }
            if(gpuTargetUndoDecoration){gpuTargetUndoDecoration.placementType=gpuTargetUndoPlacement;gpuTargetUndoDecoration.startPos=gpuTargetUndoStart;}
            // No GPU replay after rollback: hand back all ownership immediately.
        }
        internal static bool RecoverGpuLive(string reason,bool report=true)
        {
            if(!gpuLiveActive)return true;
            if(gpuRecoveryRunning)return false;
            gpuRecoveryRunning=true;bool continued=false;
            try
            {
                if(report && !GpuRecoveryTesting)Resilience.RecordFeatureFailure("GpuDecoLive",reason);
                try {GpuRecoveryFault("resume_preflight");StopGpuLive("recovery:"+reason);continued=gpuLastRestoreComplete;}
                catch(Exception ex)
                {
                    GpuLog("recovery_resume_rejected="+ex.GetType().Name+" continuity=DEGRADED forced_quiescence=True");
                    // Invalid/foreign animation state cannot be invented. Always
                    // release rendering ownership and label the loss explicitly.
                    Camera.onPreCull-=OnGpuLivePreCull;
                    gpuLiveActive=false;gpuLiveCurrentSlice=-1;
                    foreach(var slice in gpuLiveSlices)
                        foreach(var r in slice.Renderers)try{if(r)r.enabled=false;}catch{}
                    for(int i=0;i<gpuLiveOwnedRenderers.Length;i++)
                        try{if(!GpuCpuFallback(i) && gpuLiveOwnedRenderers[i])gpuLiveOwnedRenderers[i].forceRenderingOff=RestoredGpuRendererOff(i);}catch{}
                    ResetGpuLiveSourcePool();
                }
                GpuLog("recovery active="+gpuLiveActive+" continued="+continued+" reason="+reason+" injected="+GpuRecoveryTesting);
                return continued;
            }
            finally {gpuRecoveryRunning=false;gpuRecoveryBlocked=true;gpuTargetUndoCount=0;gpuTargetUndoDecoration=null;}
        }
#if AUTOTEST
        static long gpuMicroTick;
        static int gpuMicroFrame;
        static double gpuMicroSong;
        static GpuDecoThreadCpu gpuMicroThreads;
        static GpuDecoRenderMetrics gpuMicroMetrics;
        static void ResetGpuMicroWindow()
        {
            gpuMicroTick=0;
            gpuMicroThreads?.Dispose();gpuMicroThreads=null;
            gpuMicroMetrics?.Dispose();gpuMicroMetrics=null;
        }
        static void GpuMicroWindow(string edge)
        {
            RequireLive(gpuLiveActive&&Hitch.Playing&&!FrameCompare.Paused&&!GpuDecoCostProbe.Enabled,"micro window requires playing GPU ownership without costprobe");
            if(edge=="begin")
            {
                RequireLive(gpuMicroTick==0,"micro window already open");
                // Explicit boundary-only attribution; never new per-frame hooks
                // or timers. Setup precedes the FPS clock. Thread CPU remains
                // a boundary upper bound, not GPU time or waiting time.
                if(GpuDecoThreadCpu.Enabled)
                {
                    gpuMicroThreads=GpuDecoThreadCpu.Begin(GpuLog);
                    gpuMicroMetrics=new GpuDecoRenderMetrics();
                }
                gpuMicroFrame=Time.frameCount;gpuMicroSong=scrConductor.instance.songposition_minusi;
                gpuMicroTick=System.Diagnostics.Stopwatch.GetTimestamp();
                GpuLog("micro_begin song="+gpuMicroSong.ToString("F6",Inv)+" cache="+gpuEffectCache+" "+TightLate.GpuListStats+" per_frame_probe=False");return;
            }
            RequireLive(edge=="end"&&gpuMicroTick!=0,"micro window is not open");
            long tick=System.Diagnostics.Stopwatch.GetTimestamp();int frames=Time.frameCount-gpuMicroFrame;
            double elapsed=(tick-gpuMicroTick)/(double)System.Diagnostics.Stopwatch.Frequency;gpuMicroTick=0;
            var native=GpuDecoCopy.ReadTotals();
            GpuLog("micro_end song_begin="+gpuMicroSong.ToString("F6",Inv)+" song_end="+scrConductor.instance.songposition_minusi.ToString("F6",Inv)
                +" elapsed_s="+elapsed.ToString("F6",Inv)+" frames="+frames+" real_fps="+(frames/elapsed).ToString("F3",Inv)
                +" cache="+gpuEffectCache+" active="+gpuLiveActive+" owned="+GpuLiveOwned+" native_executed="+native.Executed+" native_errors="+native.Errors+" "+TightLate.GpuListStats+" physical_display=UNMEASURED");
            var threads=gpuMicroThreads;gpuMicroThreads=null;
            var metrics=gpuMicroMetrics;gpuMicroMetrics=null;
            try {threads?.Complete(-1,frames,elapsed,GpuDecoCopy.RenderThreadId(),GpuLog);metrics?.Report(-1,GpuLog);}
            finally {threads?.Dispose();metrics?.Dispose();}
        }
        static void TestGpuEffectCache()
        {
            RequireLive(!gpuLiveActive,"effect cache fixture requires inactive GPU ownership");
            var e=new DecoBakeCore.Event();int masks=0,eases=0;
            try
            {
                for(int mask=0;mask<65536;mask++)
                {
                    for(int c=0;c<16;c++)e.Values[c]=(mask&(1<<c))!=0?c+.25f:float.NaN;
                    e.Ease=(int)Ease.Linear;e.Duration=0;CacheGpuEffect(e,false);int at=0;
                    for(int c=0;c<16;c++)if(!float.IsNaN(e.Values[c]))
                        RequireLive(at<gpuEffectChannelCount&&gpuEffectChannels[at++]==c,"cached effect channel order changed");
                    RequireLive(at==gpuEffectChannelCount,"cached effect channel count changed");masks++;
                }
                for(int id=0;id<=36;id++)if(GpuDecoGpu.Supported(id))
                    foreach(float duration in new[]{0f,.01f,1f,2.25f})
                        foreach(float over in new[]{0f,1.70158f,4f})
                            foreach(float period in new[]{0f,.3f,.7f})
                            {
                                e.Ease=id;e.Duration=duration;e.Over=over;e.Period=period;e.Values[5]=.75f;
                                bool anim=duration>0;CacheGpuEffect(e,anim);
                                float a=anim?1f:ZeroTween.Eval((Ease)id,1f,1f,over,period);
                                float b=anim?ZeroTween.Eval((Ease)id,duration,duration,over,period):1f;
                                RequireLive(InstantMove.Bits(a)==InstantMove.Bits(gpuEffectEaseEnd)
                                    &&InstantMove.Bits(b)==InstantMove.Bits(gpuEffectEaseDurEnd),"cached effect easing bits changed");eases++;
                            }
                GpuLog("effect_cache_fixture masks="+masks+" easing_cases="+eases+" mismatch=0 performance=False");
            }
            finally {gpuEffectChannelCount=0;gpuCurrentFxParsed=false;}
        }
        static string gpuRecoveryFaultStage;
        static int gpuRecoveryTestDepth;
        static partial void GpuRecoveryFault(string stage)
        {
            if(gpuRecoveryFaultStage!=stage)return;
            gpuRecoveryFaultStage=null;
            throw new InvalidOperationException("injected_gpu_recovery_"+stage);
        }
        static void RunGpuRecoveryFault(string stage)
        {
            int depth=gpuRecoveryTestDepth;gpuRecoveryTestDepth=depth+1;
            try
            {
                gpuRecoveryFaultStage=stage;
                if(stage=="render_before" || stage=="render_after")
                {
                    gpuLiveLastDispatchFrame=-1;GpuLiveFrame();
                }
                else
                {
                    // Existing game effect, seeded target only. Earlier targets
                    // are not replayed; normal FastMove resumes the failing one.
                    var fx=effectList.OfType<ffxMoveDecorationsPlus>().FirstOrDefault(f=>f && f.duration>0 && Reasons(f).Length==0
                        && CopyEvent(f).Values.Any(v=>!float.IsNaN(v)));
                    RequireLive(fx!=null,"no supported game effect for recovery fixture");
                    var d=gpuLiveOwnedSet.First();
                    BeginGpuSource(fx);
                    try {RequireLive(!GpuLiveSkip(fx,d,0),"injected target unexpectedly stayed GPU owned");}
                    finally {EndGpuSource();}
                }
                RequireLive(gpuRecoveryFaultStage==null && !gpuLiveActive && CountUnrestoredOwned()==0,"recovery fault not consumed or renderers unrestored");
                GpuLog("recovery_fixture stage="+stage+" returned_CPU=True unrestored=0 scope=seeded_fault_not_normal_performance");
            }
            finally {gpuRecoveryFaultStage=null;gpuRecoveryTestDepth=depth;}
        }
        static void TestGpuForcedQuiescence()
        {
            int depth=gpuRecoveryTestDepth;gpuRecoveryTestDepth=depth+1;
            try
            {
                gpuRecoveryFaultStage="resume_preflight";
                bool continued=RecoverGpuLive("injected_invalid_resume",false);
                RequireLive(!continued && !gpuLiveActive && CountUnrestoredOwned()==0,"forced handback contract");
                DisposeGpuLive();
                RequireLive(GpuDecoCopy.ActiveContexts==0 && gpuLiveSlices.Count==0 && !gpuLiveRoot,"forced disposal still owns resources");
                GpuLog("recovery_force continued=False unrestored=0 native_contexts=0 slices=0 continuity=DEGRADED expected_injection=True");
            }
            finally {gpuRecoveryFaultStage=null;gpuRecoveryTestDepth=depth;}
        }
#endif
    }
}
#endif
