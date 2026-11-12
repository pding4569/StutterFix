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
        static bool collapseDiagReady,collapseDiagFreeze,collapseDiagCount;
        static long collapseDiagDispatches,collapseDiagSkips;
        static bool[] collapseDiagEnabled;
        static bool? collapseDiagPreviousHide;
        static Renderer[] collapseDiagSuppressed;
        static bool[] collapseDiagSuppressedFlags;
        static bool RestoreCollapseDiagSuppression()
        {
            if(collapseDiagSuppressed==null)return true;
            bool restored=true;
            for(int i=0;i<collapseDiagSuppressed.Length;i++)
                try {var r=collapseDiagSuppressed[i];if(r)r.forceRenderingOff=collapseDiagSuppressedFlags[i];}
                catch(Exception e){restored=false;GpuLog("COLLAPSE_DIAG suppression_restore_error="+e.GetType().Name);}
            if(restored){collapseDiagSuppressed=null;collapseDiagSuppressedFlags=null;}
            return restored;
        }
        static string CollapseTagCensus(string tag)
        {
            RequireLive(gpuLivePrepared && gpuLiveActive && g2Window==-1,"tag census needs active prepared GPU outside measurement");
            // Explicit boundary diagnostic only; never admission or mutation.
            var mgr=scrDecorationManager.instance;
            var field=HarmonyLib.AccessTools.Field(typeof(scrDecorationManager),"taggedDecorations");
            var dict=field?.GetValue(mgr) as System.Collections.Generic.Dictionary<string,System.Collections.Generic.List<scrDecoration>>;
            RequireLive(dict!=null && dict.ContainsKey(tag),"tag census missing tag");
            var entries=dict[tag];var index=new System.Collections.Generic.Dictionary<scrDecoration,int>();
            for(int i=0;i<baselineObjects.Length;i++)if(baselineObjects[i])index[baselineObjects[i]]=i;
            var reasons=new System.Collections.Generic.Dictionary<string,int>();
            var types=new System.Collections.Generic.Dictionary<string,int>();
            int owned=0,cpu=0,hidden=0,lazyImage=0,lazyPosition=0,nulls=0;
            foreach(var d in entries)
            {
                if(!d){nulls++;continue;}
                string type=d.GetType().Name;types.TryGetValue(type,out int nt);types[type]=nt+1;
                if(InvisibleSkip.IsHidden(d))hidden++;
                if(InvisibleSkip.ResearchPendingPosition(d))lazyPosition++;
                if(d is scrVisualDecoration visual && LazySprite.ResearchPending(visual))lazyImage++;
                if(gpuLiveOwnedSet.Contains(d) && gpuLiveMasterByDeco.TryGetValue(d,out int ownedMaster) && !GpuCpuFallback(ownedMaster)){owned++;continue;}cpu++;
                string reason;
                if(gpuLiveMasterByDeco.TryGetValue(d,out int master))reason=GpuCpuFallback(master)?"live_CPU_fallback":"allocated_not_owned";
                else if(index.TryGetValue(d,out int b))reason=table.Decorations[b].Reasons.Count==0?"live_geometry_or_scope":string.Join("+",table.Decorations[b].Reasons.OrderBy(x=>x,StringComparer.Ordinal));
                else reason="outside_baseline";
                reasons.TryGetValue(reason,out int nr);reasons[reason]=nr+1;
            }
            return "TAG_CENSUS tag="+tag+" conductor_song="+scrConductor.instance.songposition_minusi.ToString("F6",Inv)
                +" frame="+Time.frameCount+" count="+entries.Count+" owned="+owned+" cpu="+cpu+" nulls="+nulls+" hidden_original="+hidden
                +" lazy_images="+lazyImage+" lazy_positions="+lazyPosition+" types="+string.Join(";",types.Select(k=>k.Key+":"+k.Value))
                +" reasons="+string.Join(";",reasons.Select(k=>k.Key+":"+k.Value))+" diagnostic_only=True";
        }
        static string CollapseDiagState()
        {
            int enabled=0,visible=0,forced=0;
            foreach(var s in gpuCollapseSpans)if(s.Renderer)
            {if(s.Renderer.enabled)enabled++;if(s.Renderer.isVisible)visible++;if(s.Renderer.forceRenderingOff)forced++;}
            return "active="+gpuLiveActive+" collapse="+gpuCollapseOn+" ready="+collapseDiagReady+" paused="+GpuPlaybackState.Paused
                +" freeze="+collapseDiagFreeze+" hide="+DrawCollapsePolicy.HideProxies+" dispatches="+collapseDiagDispatches
                +" skips="+collapseDiagSkips+" spans="+gpuCollapseSpans.Count+" enabled="+enabled+" visible="+visible+" forced="+forced;
        }
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
            if(sub.StartsWith("singlearray ",StringComparison.Ordinal)){GpuLog(SingleArrayCommand(sub.Substring(12)));return true;}
            if(args.Length==2 && args[0]=="mapbatch"){GpuLog(MapBatchCommand(args[1]));return true;}
            if(args.Length==2 && args[0]=="tagcensus"){GpuLog(CollapseTagCensus(args[1]));return true;}
            if(args[0]=="diag")
            {
                RequireLive(args.Length>=2 && gpuLiveActive && gpuCollapseOn && GpuPlaybackState.Paused,"collapse diagnostic requires paused active GPU");
                if(args[1]=="reset")
                {collapseDiagDispatches=collapseDiagSkips=0;collapseDiagCount=true;}
                else if(args[1]=="freeze")
                {RequireLive(args.Length==3 && collapseDiagReady,"collapse diagnostic needs initialized vertices");collapseDiagFreeze=args[2]=="on";}
                else if(args[1]=="suppress")
                {
                    RequireLive(args.Length==3 && (args[2]=="on" || args[2]=="off"),"collapse diagnostic suppress on|off");
                    if(args[2]=="on")
                    {
                        RequireLive(collapseDiagSuppressed==null,"already suppressed");
                        collapseDiagSuppressed=gpuCollapseSpans.Select(s=>(Renderer)s.Renderer).ToArray();
                        collapseDiagSuppressedFlags=collapseDiagSuppressed.Select(r=>r && r.forceRenderingOff).ToArray();
                        try{foreach(var r in collapseDiagSuppressed)if(r)r.forceRenderingOff=true;}
                        catch{RestoreCollapseDiagSuppression();throw;}
                    }
                    else RequireLive(RestoreCollapseDiagSuppression(),"suppression flags not restored");
                    GpuLog("COLLAPSE_DIAG suppression="+(collapseDiagSuppressed!=null)+" renderer_enabled_unchanged=True");
                }
                else if(args[1]=="draw")
                {
                    RequireLive(args.Length==3,"collapse diagnostic draw on|off");
                    if(!collapseDiagPreviousHide.HasValue)collapseDiagPreviousHide=DrawCollapsePolicy.HideProxies;
                    DrawCollapsePolicy.HideProxies=args[2]=="off";
                    if(DrawCollapsePolicy.HideProxies)
                    {
                        if(collapseDiagEnabled==null)collapseDiagEnabled=gpuCollapseSpans.Select(s=>s.Renderer && s.Renderer.enabled).ToArray();
                        foreach(var s in gpuCollapseSpans)if(s.Renderer)s.Renderer.enabled=false;
                    }
                    else if(collapseDiagEnabled!=null)
                    {
                        RequireLive(collapseDiagEnabled.Length==gpuCollapseSpans.Count,"collapse diagnostic plan changed");
                        for(int k=0;k<gpuCollapseSpans.Count;k++)if(gpuCollapseSpans[k].Renderer)gpuCollapseSpans[k].Renderer.enabled=collapseDiagEnabled[k];
                        collapseDiagEnabled=null;
                    }
                }
                else RequireLive(args[1]=="state","collapse diagnostic reset|freeze|draw|state");
                GpuLog("COLLAPSE_DIAG "+CollapseDiagState()+" diagnostic_only=True");return true;
            }
            if(sub=="reset")
            {
                RequireLive(!gpuLiveActive && collapseWindow<0,"drawcollapse reset requires inactive measurement");
                collapseDraws.Dispose();collapseDraws=default;collapseGate=new DrawCollapseGatePolicy();
                collapseWidth=collapseHeight=0;collapseHz=0;gpuCollapseFault=null;gpuCollapsePrepare=false;
                GpuLog("drawcollapse_gate_reset=True");return true;
            }
            if(args.Length==3 && args[0]=="policy")
            {
                if(args[1]=="shapeonly"){DrawCollapsePolicy.ShapeOnly=args[2]=="-"?null:args[2];GpuLog("DRAWCOLLAPSE_POLICY shapeonly="+args[2]);return true;}
                if(args[1]=="shapegrid"){DrawCollapsePolicy.ShapeGrid=int.Parse(args[2],Inv);GpuLog("DRAWCOLLAPSE_POLICY shapegrid="+DrawCollapsePolicy.ShapeGrid);return true;}
                if(args[1]=="trimmin"){DrawCollapsePolicy.TrimMinSide=int.Parse(args[2],Inv);GpuLog("DRAWCOLLAPSE_POLICY trimmin="+DrawCollapsePolicy.TrimMinSide);return true;}
                if(args[1]=="tightmin"){DrawCollapsePolicy.TightMinSide=int.Parse(args[2],Inv);GpuLog("DRAWCOLLAPSE_POLICY tightmin="+DrawCollapsePolicy.TightMinSide);return true;}
                bool on=args[2]=="on";
                switch(args[1])
                {
                    case "tight":DrawCollapsePolicy.TightLargeQuads=on;break;
                    case "minimal":DrawCollapsePolicy.MinimalDispatch=on;break;
                    case "cull":DrawCollapsePolicy.CullIdleSpans=on;break;
                    case "keepafter":DrawCollapsePolicy.KeepAfterBarrier=on;break;
                    case "clip":DrawCollapsePolicy.ClipOffscreen=on;break;
                    case "direct":DrawCollapsePolicy.DirectDispatch=on;break;
                    case "disableowned":DrawCollapsePolicy.DisableOwnedRenderers=on;break;
                    case "globalverts":DrawCollapsePolicy.GlobalVerts=on;break;
                    case "skiporder":DrawCollapsePolicy.SkipOrderSweep=on;break;
                    case "skipcompute":DrawCollapsePolicy.SkipPausedCompute=on;break;
                    case "nocompute":DrawCollapsePolicy.SkipComputeForTest=on;break;
                    case "splitorder":DrawCollapsePolicy.SplitByOrder=on;break;
                    case "hideproxies":DrawCollapsePolicy.HideProxies=on;break;
                    case "shape":DrawCollapsePolicy.ShapeCells=on;break;
                    case "runlodsplit":DrawCollapsePolicy.RunLodSplit=on;break;
                    case "compact":DrawCollapsePolicy.CompactIndices=on;DecoBake.ForceIndexRebuild();break;
                    case "nudge":DrawCollapsePolicy.NudgeTieSpans=on;break;   // 측정: 구간 그리기만 끔(계산은 그대로)
                    default:throw new ArgumentException("policy tight|keepafter|clip|skiporder|skipcompute|globalverts on|off");
                }
                GpuLog("DRAWCOLLAPSE_POLICY tight="+DrawCollapsePolicy.TightLargeQuads+" keepafter="+DrawCollapsePolicy.KeepAfterBarrier);return true;
            }
            if(args.Length==1 && args[0]=="alphacount")
            {
                var src=gpuLiveSlices[0];var vals=src.Gpu.ReadValuesForDiag();int n=src.Data.Items.Length,pos=0,inc=0,posInc=0,fb=0;
                for(int i=0;i<n;i++)
                {
                    bool p=vals[i*16+10]*vals[i*16+11]>0;if(p)pos++;
                    if(gpuCollapseIncluded!=null&&gpuCollapseIncluded[i]){inc++;if(p)posInc++;}
                    if(GpuCpuFallback(src.MasterItems[i]))fb++;
                }
                Main.Entry.Logger.Log("[gpudeco] alphacount 항목 "+n+" 알파>0 "+pos+" | 통합 포함 "+inc+" 중 알파>0 "+posInc+" | CPU처리 "+fb);return true;
            }
            if(args.Length==2 && args[0]=="spans"){DiagSpans(args[1]);return true;}
            if(args.Length==2 && args[0]=="excl"){DiagExcl(args[1]);return true;}
            if(args.Length==1 && args[0]=="fullcheck"){RunFullCheck();return true;}
            if(args.Length==1 && args[0]=="trimstats"){Main.Entry.Logger.Log("[gpudeco] GPUTRIM_STATS bounds="+gpuTrimBounds.Count+" trimmed="+gpuTrimTrimmed+" kept="+gpuTrimKept);return true;}
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
            if(args.Length==2 && args[0]=="clipscale"){DrawCollapsePolicy.ClipScale=float.Parse(args[1],System.Globalization.CultureInfo.InvariantCulture);GpuLog("DRAWCOLLAPSE_CLIPSCALE "+DrawCollapsePolicy.ClipScale);return true;}
            if(args.Length==2 && args[0]=="sharemat"){DiagShareMaterial(args[1]=="on");return true;}
            if(args.Length==2 && args[0]=="hideproxy")
            {
                DrawCollapsePolicy.HideProxies=args[1]=="on";
                MarkAllSpansDirty();
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
