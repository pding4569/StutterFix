#if AUTOTEST
using System;
using System.Diagnostics;
using System.Linq;
using Unity.Profiling;
using UnityEngine;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        static RenderMergeGatePolicy mergeGate=new RenderMergeGatePolicy();
        static int mergeWindow=-1,mergeWindowFrame,mergeObservedFrame=-1,mergeObservedCount,mergeSpikes,mergeMaxRenderers,mergeMaxDraws;
        static long mergeTick;
        static bool mergeWindowMode;
        static double mergeWorst;
        static ProfilerRecorder mergeDrawRecorder;
        static int mergeGenerationBeforeReopen=-1,mergeRetryCount,mergeRetryAtReset;
        static scrDecorationManager mergeManagerBeforeReopen;
        static bool mergeEndWatched,mergeRetryWatched;
        static bool mergeTestActive;
        static float mergePlaybackWaitAt=-1;
        static int mergeScreenWidth,mergeScreenHeight;
        static double mergeScreenHz;
        internal static void FinishGpuMergeTest(string why)
        {
            if(!mergeTestActive)return;
            mergeGate.Error=true;gpuMergeError=why;
            try {mergeDrawRecorder.Dispose();}catch { }
            mergeDrawRecorder=default;mergeWindow=-1;
            try {DisposeGpuLive();}catch { }
            GpuRenderMergeGateCommand("result");
        }
        internal static void ObserveGpuMergeRetry()
        {if(mergeRetryWatched)mergeRetryCount++;}
        static void ObserveGpuMergeRestore(bool restored)
        {if(mergeTestActive && !restored)mergeGate.StabilityFailure=true;}
        static void ObserveGpuMergeFrame()
        {
            if(mergeWindow<0 || mergeObservedFrame==Time.frameCount)return;
            mergeObservedFrame=Time.frameCount;
            if(!gpuLiveActive || !Hitch.Playing || FrameCompare.Paused || gpuRenderMergeOn!=mergeWindowMode)mergeGate.Error=true;
            if(Time.frameCount<=mergeWindowFrame)return;
            mergeObservedCount++;
            double ms=Time.unscaledDeltaTime*1000.0;
            if(double.IsNaN(ms) || double.IsInfinity(ms) || ms<=0){mergeGate.Error=true;return;}
            if(ms>30)mergeSpikes++;mergeWorst=Math.Max(mergeWorst,ms);
        }
        static double MergeSong=>scrConductor.instance?scrConductor.instance.songposition_minusi:double.NaN;
        static void RequireGpuMergeEnvironment()
        {
            double hz=Screen.currentResolution.refreshRateRatio.value;
            // 같은 실행 안의 A/B(ABBA)끼리만 비교하므로 모니터 상태 둘 다 허용한다(섞어 합치지 말 것).
            RequireLive((Screen.width==2560 && Screen.height==1440 && Math.Abs(hz-120)<.5) || (Screen.width==3440 && Screen.height==1440 && Math.Abs(hz-165)<1.5),
                "rendermerge acceptance scope requires 2560x1440@120 or 3440x1440@165; actual="+Screen.width+"x"+Screen.height+"@"+hz.ToString("F3",Inv));
            if(mergeScreenWidth==0)
            {mergeScreenWidth=Screen.width;mergeScreenHeight=Screen.height;mergeScreenHz=hz;}
            RequireLive(Screen.width==mergeScreenWidth && Screen.height==mergeScreenHeight && Math.Abs(hz-mergeScreenHz)<.01,
                "rendermerge display changed within ABBA; do not mix display environments");
        }
        static bool GpuRenderMergeGateCommand(string sub)
        {
            var a=sub.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
            if(sub=="environment")
            {
                RequireGpuMergeEnvironment();
                GpuLog("rendermerge_environment=True actual="+Screen.width+"x"+Screen.height+"@"+Screen.currentResolution.refreshRateRatio.value.ToString("F3",Inv));return true;
            }
            if(sub=="playbackready")
            {
                RequireLive(!FrameCompare.Paused && !gpuLiveActive,"playbackready requires unpaused CPU playback");
                var c=scrConductor.instance;var ctrl=ADOBase.controller;var ed=scnEditor.instance;
                // PrepVfx (gpudeco scheduled) precedes StartMusic's coroutine.
                // OnMusicScheduled -> Scrub writes AudioListener.pause=false;
                // do not freeze a diagnostic snapshot before that callback.
                bool started=c && c.hasSongStarted && ctrl && ctrl.gameworld && !ctrl.paused
                    && ed && ed.playMode && Hitch.Playing && !AudioListener.pause && Time.timeScale>0;
                if(started)
                {
                    mergePlaybackWaitAt=-1;
                    GpuLog("rendermerge_playback_ready=True song="+MergeSong.ToString("F6",Inv));
                }
                else
                {
                    if(mergePlaybackWaitAt<0)mergePlaybackWaitAt=Time.realtimeSinceStartup;
                    if(Time.realtimeSinceStartup-mergePlaybackWaitAt>40)
                        MergeReject("playback_start_timeout song="+MergeSong.ToString("F6",Inv));
                }
                return started;
            }
            if(sub=="reset")
            {
                RequireLive(mergeWindow<0 && !gpuLiveActive,"rendermerge reset requires no live measurement/ownership");
                mergeDrawRecorder.Dispose();mergeDrawRecorder=default;
                mergeGate=new RenderMergeGatePolicy();gpuMergeError=null;
                mergeTestActive=true;
                mergePlaybackWaitAt=-1;
                mergeScreenWidth=mergeScreenHeight=0;mergeScreenHz=0;
                mergeRetryAtReset=mergeRetryCount;mergeEndWatched=mergeRetryWatched=false;
                mergeGenerationBeforeReopen=-1;mergeManagerBeforeReopen=null;
                GpuLog("rendermerge_reset=True");return true;
            }
            if(a.Length==2 && a[0]=="begin")
            {
                int i=int.Parse(a[1],Inv);bool on=i==1||i==2;
                RequireLive(i>=0 && i<4 && i==mergeGate.Seen.Count(x=>x) && mergeWindow<0 && !mergeGate.Seen[i] && gpuLiveActive && gpuRenderMergeOn==on
                    && Hitch.Playing && !FrameCompare.Paused && Math.Abs(MergeSong-260)<.6 && !GpuDecoCostProbe.Enabled,
                    "rendermerge begin requires ABBA GPU ownership at 260s");
                RequireGpuMergeEnvironment();
                int independentVisible=gpuLiveSlices.Sum(s=>s.Renderers.Count(r=>r && r.enabled));
                int allocated=gpuLiveOwnedIndices.Length,owned=GpuLiveOwned,fallback=gpuLiveCpuFallbackCount;
                RequireLive(mergeGate.Population(i,on,allocated,owned,fallback,independentVisible),
                    "rendermerge population requires >=20000 GPU owners, nonempty A and identical ABBA ownership; allocated="
                    +allocated+" owned="+owned+" CPU_fallback="+fallback+" independent_enabled="+independentVisible);
                mergeDrawRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",16384);
                mergeWindow=i;mergeWindowMode=on;mergeWindowFrame=Time.frameCount;mergeObservedFrame=-1;
                mergeObservedCount=mergeSpikes=mergeMaxRenderers=mergeMaxDraws=0;mergeWorst=0;
                mergeTick=Stopwatch.GetTimestamp();
                GpuLog("rendermerge_window_begin index="+i+" merged="+on+" song="+MergeSong.ToString("F6",Inv)
                    +" allocated="+gpuLiveOwnedIndices.Length+" owned="+GpuLiveOwned+" independent_enabled="+independentVisible
                    +" CPU_fallback="+gpuLiveCpuFallbackCount);return true;
            }
            if(sub=="end")
            {
                RequireLive(mergeWindow>=0,"rendermerge window not open");
                int i=mergeWindow;mergeWindow=-1;
                double seconds=(Stopwatch.GetTimestamp()-mergeTick)/(double)Stopwatch.Frequency;
                int frames=Time.frameCount-mergeWindowFrame;
                RequireGpuMergeEnvironment();
                bool valid=seconds>0 && frames>0 && mergeObservedCount>=Math.Max(1,frames-1) && gpuLiveActive && Hitch.Playing && !FrameCompare.Paused
                    && gpuRenderMergeOn==mergeWindowMode && Math.Abs(MergeSong-270)<.6 && gpuMergeError==null;
                long engineDraws=0;
                try
                {
                    mergeDrawRecorder.Stop();
                    valid&=mergeDrawRecorder.Valid && mergeDrawRecorder.Count>1 && !mergeDrawRecorder.WrappedAround;
                    for(int k=1;k<mergeDrawRecorder.Count;k++)engineDraws=Math.Max(engineDraws,mergeDrawRecorder.GetSample(k).Value);
                }
                finally {mergeDrawRecorder.Dispose();mergeDrawRecorder=default;}
                var native=GpuDecoCopy.ReadTotals();valid&=native.Errors==0 && Resilience.TotalErrors==0;
                mergeGate.Window(i,mergeWindowMode,frames/Math.Max(seconds,1e-9),valid,mergeSpikes,mergeMaxRenderers,(int)Math.Min(int.MaxValue,engineDraws));
                GpuLog("rendermerge_window index="+i+" merged="+mergeWindowMode+" fps="+mergeGate.Fps[i].ToString("F6",Inv)
                    +" frames="+frames+" observed_frames="+mergeObservedCount+" elapsed_s="+seconds.ToString("F6",Inv)+" song_end="+MergeSong.ToString("F6",Inv)
                    +" spikes_30ms="+mergeSpikes+" worst_ms="+mergeWorst.ToString("F6",Inv)+" renderers="+mergeMaxRenderers
                    +" replacement_submeshes="+mergeMaxDraws+" engine_draws_max="+engineDraws+" valid="+valid);return true;
            }
            if(a.Length==2 && a[0]=="pixel")
            {
                int i=int.Parse(a[1],Inv);double target=i==0?262:270;
                RequireLive(i>=0 && i<2 && FrameCompare.Paused && gpuLiveActive && !gpuRenderMergeOn && Math.Abs(MergeSong-target)<.6,
                    "rendermerge pixel requires restored independent GPU at 262/270s");
                var direct=FrameCompare.CompareShots("A","B",out _);
                var restore=FrameCompare.CompareShots("A","A2",out bool same);
                var noise=FrameCompare.CompareShots("A","N",out bool quiet);
                var old=FrameCompare.CompareShots("O","A",out _);
                var next=FrameCompare.CompareShots("O","B",out _);
                mergeGate.Pixel(i,direct.Changed,direct.Max.Max(),old.Changed,old.Max.Max(),next.Changed,next.Max.Max(),
                    same && quiet && restore.Changed==0 && noise.Changed==0 && gpuMergeError==null);
                GpuLog("rendermerge_pixel index="+i+" original_independent="+old.Changed+" original_merged="+next.Changed
                    +" independent_merged="+direct.Changed+" max="+direct.Max.Max()+" restore="+restore.Changed+" noise="+noise.Changed);return true;
            }
            if(a.Length==2 && a[0]=="watch")
            {
                RequireLive(gpuLiveActive && gpuRenderMergeOn && gpuRenderMergePrepared,"watch lifecycle requires active merged ownership");
                if(a[1]=="song_end")mergeEndWatched=true;
                else if(a[1]=="retry")mergeRetryWatched=true;
                else if(a[1]=="reopen"){mergeGenerationBeforeReopen=Generation;mergeManagerBeforeReopen=manager;}
                else throw new ArgumentException("rendermerge watch song_end|retry|reopen");
                return true;
            }
            if(a.Length==2 && a[0]=="stability")
            {
                bool clean=!gpuLiveActive && CountUnrestoredOwned()==0 && gpuLastRestoreComplete && Resilience.TotalErrors==0 && gpuMergeError==null;
                int index;
                if(a[1]=="song_end"){index=0;clean&=mergeEndWatched && gpuLivePortalObserved;}
                else if(a[1]=="retry"){index=1;clean&=mergeRetryWatched && mergeRetryCount>mergeRetryAtReset;}
                else if(a[1]=="reopen")
                {
                    index=2;clean&=mergeGenerationBeforeReopen>=0 && Generation!=mergeGenerationBeforeReopen
                        && manager && manager!=mergeManagerBeforeReopen;mergeManagerBeforeReopen=null;
                }
                else if(a[1]=="unload")
                {
                    index=3;clean&=!gpuLivePrepared && gpuLiveSlices.Count==0 && gpuMergeWindows.Count==0 && GpuDecoCopy.ActiveContexts==0;
                }
                else throw new ArgumentException("rendermerge stability song_end|retry|reopen|unload");
                mergeGate.Stability(index,clean);
                GpuLog("rendermerge_stability kind="+a[1]+" pass="+clean+" unrestored="+CountUnrestoredOwned()
                    +" merged_windows="+gpuMergeWindows.Count+" native_contexts="+GpuDecoCopy.ActiveContexts);return true;
            }
            if(sub=="result")
            {
                if(mergeWindow>=0 || gpuMergeError!=null || Resilience.TotalErrors!=0)mergeGate.Error=true;
                GpuLog("RENDERMERGE="+mergeGate.Verdict()+" fps="+string.Join(",",mergeGate.Fps.Select(x=>x.ToString("F3",Inv)))
                    +" pixels="+mergeGate.Changed+" renderers="+mergeGate.Renderers+" engine_draws_max="+mergeGate.Draws
                    +" stability="+mergeGate.Stable.Count(x=>x)+"/4 error="+(gpuMergeError??"none"));mergeTestActive=false;return true;
            }
            throw new ArgumentException("rendermerge prepare|ready|on|off|reset|begin 0..3|end|pixel 0..1|watch KIND|stability KIND|result");
        }
    }
}
#endif
