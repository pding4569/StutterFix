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
        static DrawCollapseGatePolicy collapseGate=new DrawCollapseGatePolicy();
        static int collapseWindow=-1,collapseFrame,collapseObserved=-1,collapseObservedCount,collapseSpikes;
        static int collapseWidth,collapseHeight;static double collapseHz;
        static long collapseTick;static ProfilerRecorder collapseDraws;
        static void CollapseEnvironment()
        {
            double hz=Screen.currentResolution.refreshRateRatio.value;
            if(collapseWidth==0){collapseWidth=Screen.width;collapseHeight=Screen.height;collapseHz=hz;}
            RequireLive(Screen.width==collapseWidth && Screen.height==collapseHeight && Math.Abs(hz-collapseHz)<.01,
                "drawcollapse display changed inside one comparison");
        }
        static void ObserveDrawCollapseFrame()
        {
            if(collapseWindow<0 || collapseObserved==Time.frameCount)return;
            collapseObserved=Time.frameCount;
            if(!gpuLiveActive || gpuCollapseOn!=(collapseWindow==1 || collapseWindow==2) || !Hitch.Playing || FrameCompare.Paused)
                collapseGate.Failed=true;
            if(Time.frameCount<=collapseFrame)return;
            collapseObservedCount++;
            double ms=Time.unscaledDeltaTime*1000.0;
            if(ms<=0 || double.IsNaN(ms) || double.IsInfinity(ms))collapseGate.Failed=true;
            if(ms>=30)collapseSpikes++;
        }
        static bool DrawCollapseGateCommand(string sub)
        {
            var args=sub.Split(' ');
            if(sub=="reset")
            {
                RequireLive(!gpuLiveActive && collapseWindow<0,"drawcollapse reset requires inactive measurement");
                collapseDraws.Dispose();collapseDraws=default;collapseGate=new DrawCollapseGatePolicy();
                collapseWidth=collapseHeight=0;collapseHz=0;gpuCollapseFault=null;gpuCollapsePrepare=false;
                GpuLog("drawcollapse_gate_reset=True");return true;
            }
            if(args.Length==3 && args[0]=="policy")
            {
                if(args[1]=="tightmin"){DrawCollapsePolicy.TightMinSide=int.Parse(args[2],Inv);GpuLog("DRAWCOLLAPSE_POLICY tightmin="+DrawCollapsePolicy.TightMinSide);return true;}
                bool on=args[2]=="on";
                switch(args[1])
                {
                    case "tight":DrawCollapsePolicy.TightLargeQuads=on;break;
                    case "keepafter":DrawCollapsePolicy.KeepAfterBarrier=on;break;
                    case "clip":DrawCollapsePolicy.ClipOffscreen=on;break;
                    case "tightall":DrawCollapsePolicy.TightAllOwned=on;break;
                    case "tilecomp":DrawCollapsePolicy.TileComposite=on;break;
                    case "direct":DrawCollapsePolicy.DirectDispatch=on;break;
                    case "disableowned":DrawCollapsePolicy.DisableOwnedRenderers=on;break;
                    case "tilemerge":DrawCollapsePolicy.TileMerge=on;break;
                    case "globalverts":DrawCollapsePolicy.GlobalVerts=on;break;
                    case "skiporder":DrawCollapsePolicy.SkipOrderSweep=on;break;
                    case "skipcompute":DrawCollapsePolicy.SkipPausedCompute=on;break;
                    default:throw new ArgumentException("policy tight|tightall|tilecomp|keepafter|clip|skiporder|skipcompute|globalverts on|off");
                }
                GpuLog("DRAWCOLLAPSE_POLICY tight="+DrawCollapsePolicy.TightLargeQuads+" keepafter="+DrawCollapsePolicy.KeepAfterBarrier);return true;
            }
            if(args.Length==1 && args[0]=="fullcheck"){RunFullCheck();return true;}
            if(args.Length==1 && args[0]=="trimstats"){Main.Entry.Logger.Log("[gpudeco] GPUTRIM_STATS bounds="+gpuTrimBounds.Count+" trimmed="+gpuTrimTrimmed+" kept="+gpuTrimKept+" "+TrimStaticStats());return true;}
            if(args.Length==1 && args[0]=="dumpverts")
            {
                if(gpuCollapseAllVerts==null){GpuLog("DRAWCOLLAPSE_DUMP none");return true;}
                var all=new float[gpuCollapseAllCount*60];gpuCollapseAllVerts.GetData(all);
                int nonzero=0;for(int q=0;q<gpuCollapseAllCount;q++){bool nz=false;for(int c=0;c<12;c++)if(all[q*15*4/4*1+c]!=0f){nz=true;break;}}
                int visible=0;for(int q=0;q<gpuCollapseAllCount*4;q++)if(all[q*15]!=0f||all[q*15+1]!=0f)visible++;
                var sb=new System.Text.StringBuilder();
                for(int q=0;q<gpuCollapseAllCount*4 && sb.Length<900;q+=Math.Max(1,gpuCollapseAllCount*4/12)){sb.Append(" v").Append(q).Append("=(");for(int c=0;c<8;c++)sb.Append(all[q*15+c].ToString("G4",Inv)).Append(c<7?",":"");sb.Append(')');}
                GpuLog("DRAWCOLLAPSE_DUMP slots="+gpuCollapseAllCount+" nonzero_vertices="+visible+" spans="+gpuCollapseSpans.Count+" sample:"+sb);return true;
            }
            if(args.Length==2 && args[0]=="hideproxy")
            {
                DrawCollapsePolicy.HideProxies=args[1]=="on";
                GpuLog("DRAWCOLLAPSE_HIDEPROXY hide="+DrawCollapsePolicy.HideProxies);return true;
            }
            if(args.Length==2 && args[0]=="unsafe")
            {
                DrawCollapseGuard.IgnoreOrderViolations=args[1]=="on";
                GpuLog("DRAWCOLLAPSE_UNSAFE_ORDER ignore="+DrawCollapseGuard.IgnoreOrderViolations+" ignored="+DrawCollapseGuard.IgnoredOrderViolations);return true;
            }
            if(args.Length==2 && args[0]=="begin")
            {
                int i=int.Parse(args[1],Inv);
                RequireDrawCollapse();CollapseEnvironment();
                RequireLive(i>=0 && i<4 && collapseWindow<0 && i==collapseGate.Windows.Count(x=>x)
                    && gpuCollapseOn==(i==1||i==2) && !FrameCompare.Paused && Hitch.Playing
                    && Math.Abs(MergeSong-260)<1.6 && gpuLiveCpuFallbackCount==0,"drawcollapse begin requires ABBA at 260s");
                collapseDraws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",16384);
                collapseWindow=i;collapseFrame=Time.frameCount;collapseObserved=-1;collapseObservedCount=collapseSpikes=0;
                collapseTick=Stopwatch.GetTimestamp();
                GpuLog("DRAWCOLLAPSE_BEGIN index="+i+" merged="+gpuCollapseOn+" owned="+GpuLiveOwned+" "+GpuDecoRenderMetrics.ScreenState());return true;
            }
            if(sub=="end")
            {
                RequireLive(collapseWindow>=0,"drawcollapse window not open");
                int i=collapseWindow;collapseWindow=-1;CollapseEnvironment();
                double seconds=(Stopwatch.GetTimestamp()-collapseTick)/(double)Stopwatch.Frequency;
                int frames=Time.frameCount-collapseFrame;long maximum=0;
                bool valid=seconds>0 && frames>0 && collapseObservedCount>=Math.Max(1,frames-1) && gpuLiveActive
                    && gpuCollapseOn==(i==1 || i==2) && !FrameCompare.Paused && Hitch.Playing && Math.Abs(MergeSong-270)<.6
                    && GpuLiveOwned==23547 && gpuLiveCpuFallbackCount==0 && gpuCollapseFault==null && GpuDecoCopy.ReadTotals().Errors==0;
                try
                {
                    collapseDraws.Stop();valid&=collapseDraws.Valid && collapseDraws.Count>1 && !collapseDraws.WrappedAround;
                    for(int k=1;k<collapseDraws.Count;k++)maximum=Math.Max(maximum,collapseDraws.GetSample(k).Value);
                }
                finally {collapseDraws.Dispose();collapseDraws=default;}
                double fps=frames/Math.Max(seconds,1e-9);
                collapseGate.Window(i,fps,(int)Math.Min(maximum,int.MaxValue),collapseSpikes,valid);
                GpuLog("DRAWCOLLAPSE_IGNORED_ORDER total="+DrawCollapseGuard.IgnoredOrderViolations+" on="+gpuCollapseOn+" fault="+(gpuCollapseFault??"none")+" pending_fallback="+(gpuCollapsePendingFallback??"none"));
                GpuLog("DRAWCOLLAPSE_WINDOW index="+i+" fps="+fps.ToString("F6",Inv)+" engine_draws_max="+maximum
                    +" spikes_30ms="+collapseSpikes+" frames="+frames+" observed="+collapseObservedCount+" valid="+valid);return true;
            }
            if(args.Length==2 && args[0]=="pixel")
            {
                int i=int.Parse(args[1],Inv);double[] targets={260,262,270};
                RequireLive(i>=0 && i<3 && gpuLiveActive && !gpuCollapseOn && FrameCompare.Paused
                    && Math.Abs(MergeSong-targets[i])<.6,"drawcollapse pixel requires paused 260/262/270 original snapshots");
                var pixels=FrameCompare.CompareShots("O","B",out _);
                var restore=FrameCompare.CompareShots("A","A2",out bool same);
                var noise=FrameCompare.CompareShots("A","N",out bool quiet);
                collapseGate.Pixel(i,pixels.Max.Max(),same && quiet && restore.Changed==0 && noise.Changed==0 && gpuCollapseFault==null);
                GpuLog("DRAWCOLLAPSE_PIXEL second="+targets[i]+" original_merged="+pixels.Changed+" maxdiff="+pixels.Max.Max()
                    +" restore="+restore.Changed+" noise="+noise.Changed);return true;
            }
            if(args.Length==2 && args[0]=="restore")
            {
                int i=int.Parse(args[1],Inv);
                bool valid=!gpuLiveActive && !gpuCollapseOn && gpuLastRestoreComplete && CountUnrestoredOwned()==0
                    && gpuCollapseSpans.All(s=>!s.Renderer || !s.Renderer.enabled) && gpuCollapseFault==null && Resilience.TotalErrors==0;
                collapseGate.Restore(i,valid);GpuLog("DRAWCOLLAPSE_RESTORE index="+i+" valid="+valid);return true;
            }
            if(sub=="result")
            {
                if(collapseWindow>=0)collapseGate.Failed=true;
                // Installation restoration belongs to the external runner; this
                // verdict explicitly covers only the in-game gate.
                bool pass=collapseGate.Passed && gpuCollapseFault==null;
                GpuLog("DRAWCOLLAPSE="+(pass?"PASS":"FAIL")+" reason="+(pass?"GAME_GATE_COMPLETE":collapseGate.Complete?"ACCEPTANCE_FAILED":"EVIDENCE_INCOMPLETE")
                    +" fps="+string.Join(",",collapseGate.Fps.Select(f=>f.ToString("F6",Inv)))
                    +" pixels="+collapseGate.Pixels.Count(x=>x)+"/3 restores="+collapseGate.Restores.Count(x=>x)+"/4 install_restore=EXTERNAL");return true;
            }
            return false;
        }
    }
}
#endif
