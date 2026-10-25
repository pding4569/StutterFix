#if AUTOTEST
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        static GpuLiveSlice gpuImageDiagnosticSlice;
        static GpuDecoGpu.Vertex[] gpuImageDiagnosticVertices;
        static double gpuImageDiagnosticTime;
        static bool GpuImageVertexDiagnosticActive=>gpuImageDiagnosticVertices!=null;
        static void StopGpuImageVertexDiagnostic(bool restore)
        {
            Camera.onPreCull-=ApplyGpuImageVertexDiagnostic;
            bool active=GpuImageVertexDiagnosticActive;
            gpuImageDiagnosticVertices=null;gpuImageDiagnosticSlice=null;
            if(active&&restore&&gpuLiveActive)DispatchGpuLiveSlice(BakeTime);
        }
        static void StartGpuImageVertexDiagnostic()
        {
            RequireLive(gpuLiveActive&&FrameCompare.Paused&&gpuImageCandidate&&gpuImageOwn&&g2Window<0
                &&!gpuCollapseOn&&!gpuRenderMergeOn&&!GpuDecoCopy.SingleSubmission
                &&GpuLiveOwned==28523&&gpuLiveCpuFallbackCount==0&&gpuLiveSlices.Count==1&&Math.Abs(BakeTime-gpuLiveTurnOnBakeTime)<=1e-6,
                "CPU vertex injection requires fresh paused image activation, independent submission and no performance gate");
            StopGpuImageVertexDiagnostic(true);
            var slice=gpuLiveSlices[0];
            RequireLive(slice.Independent&&slice.SharedBatch==null&&slice.Gpu!=null&&slice.NativeCopy!=null,
                "CPU vertex injection requires native independent copies");
            var points=slice.Gpu.ReadImageVertices();int images=0,visible=0;float maximum=0;
            for(int i=0;i<slice.MasterItems.Length;i++)
            {
                int master=slice.MasterItems[i];
                if(GpuCpuFallback(master)||!GpuImageActor(gpuLiveOwnedIndices[master]))continue;
                var r=gpuLiveOwnedRenderers[master];
                RequireLive(r&&r.sprite&&r.sprite.vertices.Length==4,"CPU vertex diagnostic sprite missing");
                if(r.color.a<=0||!r.enabled||!r.gameObject.activeInHierarchy)continue;
                var corners=r.sprite.vertices;images++;
                if(slice.Renderers[i].enabled)visible++;
                for(int k=0;k<4;k++)
                {
                    Vector3 v=corners[k];if(r.flipX)v.x=-v.x;if(r.flipY)v.y=-v.y;
                    var expected=r.transform.TransformPoint(v);
                    RequireLive(DecoBakeCore.Finite(expected.x)&&DecoBakeCore.Finite(expected.y)&&DecoBakeCore.Finite(expected.z),"nonfinite CPU vertex");
                    maximum=Mathf.Max(maximum,Vector3.Distance(expected,points[i*4+k].position));
                    // Preserve color/UV and every non-image vertex exactly.
                    points[i*4+k].position=expected;
                }
            }
            RequireLive(images>0&&visible>0,"CPU vertex injection must include visible images");
            gpuImageDiagnosticSlice=slice;gpuImageDiagnosticVertices=points;gpuImageDiagnosticTime=BakeTime;
            Camera.onPreCull-=ApplyGpuImageVertexDiagnostic;Camera.onPreCull+=ApplyGpuImageVertexDiagnostic;
            try {ApplyGpuImageVertexDiagnostic(gpuLiveCamera);}
            catch {StopGpuImageVertexDiagnostic(true);throw;}
            GpuLog("GPUIMG_DIAG mode=cpu_vertices images="+images+" visible="+visible+" max_world_delta="+maximum.ToString("R",Inv)
                +" fresh_activation=True persistent_until_off=True color_uv_unchanged=True performance=False pixel_max=NA "+GpuDecoRenderMetrics.ScreenState());
        }
        static void ApplyGpuImageVertexDiagnostic(Camera camera)
        {
            if(!GpuImageVertexDiagnosticActive)return;
            if(!gpuLiveActive||!FrameCompare.Paused||gpuLiveSlices.Count!=1||!ReferenceEquals(gpuLiveSlices[0],gpuImageDiagnosticSlice)
                ||Math.Abs(BakeTime-gpuImageDiagnosticTime)>1e-6||gpuCollapseOn||gpuRenderMergeOn)
            {StopGpuImageVertexDiagnostic(false);return;}
            if(camera!=gpuLiveCamera)return;
            try
            {
                // The normal pre-cull fallback evaluates every paused frame.
                // Copy AFTER it; a one-shot SetData would disappear before grab.
                GpuLiveFrame();
                if(!gpuLiveActive){StopGpuImageVertexDiagnostic(false);return;}
                var slice=gpuImageDiagnosticSlice;
                slice.Gpu.VertexBuffer.SetData(gpuImageDiagnosticVertices);
                RequireLive(slice.NativeCopy.Execute(slice.CopyMask?.Low??ulong.MaxValue,slice.CopyMask?.High??ulong.MaxValue),
                    "CPU vertex diagnostic native copy failed");
            }
            catch
            {
                StopGpuImageVertexDiagnostic(false);RecoverGpuLive("image_cpu_vertex_diagnostic",false);throw;
            }
        }
        static bool GpuImageDiagnosticCommand(string command)
        {
            if(GpuImageAcceptanceCommand(command))return true;
            if(command=="images refreshpose on"||command=="images refreshpose off")
            {
                RequireLive(FrameCompare.Paused&&!gpuLiveActive&&gpuImageCandidate,"refreshpose selection requires paused image CPU state");
                gpuImageRefreshPose=command.EndsWith(" on",StringComparison.Ordinal);
                GpuLog("GPUIMG_DIAG mode=refreshpose enabled="+gpuImageRefreshPose+" default_off=True performance=False");return true;
            }
            if(command=="images locate cpuvertices on"){LocateGpuImageDifference(true);return true;}
            if(command=="images locate cpuvertices off")
            {
                RequireLive(FrameCompare.Paused,"restore CPU vertex diagnostic while paused");
                StopGpuImageVertexDiagnostic(true);GpuLog("GPUIMG_DIAG mode=cpu_vertices enabled=False restored=True performance=False");return true;
            }
            if(command=="images diag pixel"||command=="images diag pixel bounded")
            {
                RequireLive(FrameCompare.Paused&&gpuLiveActive&&!GpuImageVertexDiagnosticActive,"finish vertex injection before diagnostic comparisons");
                // diag1 kept nine names and hit FrameCompare's eight-shot cap.
                // The bounded plan asserts/saves CPU O/O2 before reusing O2
                // for the second pose capture. Keep the legacy command intact.
                bool bounded=command=="images diag pixel bounded";
                var gpu=FrameCompare.CompareShots("O","G",out _);
                var pose=FrameCompare.CompareShots("O","U",out _);
                var cpu=FrameCompare.CompareShots("O","D",out _);
                var noise=FrameCompare.CompareShots("G","N",out bool quiet);
                var restored=FrameCompare.CompareShots("G","G2",out bool same);
                var cpuNoise=FrameCompare.CompareShots("D","DN",out bool cpuQuiet);
                var poseNoise=FrameCompare.CompareShots("U",bounded?"O2":"UN",out bool poseQuiet);
                GpuLog("GPUIMG_DIAG second="+MergeSong.ToString("F6",Inv)+" gpu_changed="+gpu.Changed+" gpu_max="+gpu.Max.Max()
                    +" refreshpose_changed="+pose.Changed+" refreshpose_max="+pose.Max.Max()+" cpu_vertices_changed="+cpu.Changed+" cpu_vertices_max="+cpu.Max.Max()
                    +" noise="+noise.Changed+" restore="+restored.Changed+" cpu_noise="+cpuNoise.Changed+" pose_noise="+poseNoise.Changed
                    +" controls="+(quiet&&same&&cpuQuiet&&poseQuiet&&noise.Changed==0&&restored.Changed==0&&cpuNoise.Changed==0&&poseNoise.Changed==0)
                    +" capture_layout="+(bounded?"eight_reuse_O2_after_CPU_assert":"legacy_nine")
                    +" cause=UNCONFIRMED performance=False");return true;
            }
            return false;
        }
        static readonly double[] imageOwnFps=new double[4];
        static readonly bool[] imageOwnWindows=new bool[4],imageOwnRestores=new bool[4],imageOwnPixels=new bool[3];
        static int imageOwnWindow=-1,imageOwnNextWindow,imageOwnFrame,imageOwnObservedFrame=-1,imageOwnObserved,imageOwnSpikes;
        static int imageOwnMaxPixel=-1,imageOwnCpuFallback,imageOwnTextureFallback,imageOwnTotalSpikes;
        static long imageOwnTick;
        const int ImageOwnPixelMax=3;
        static bool imageOwnFailed,imageOwnWindowFailed,imageOwnPixelOrderingOk;
        struct ImageOwnSpike
        {
            internal int Frame,Gc0,Gc1,Gc2;
            internal double Song,Milliseconds;
        }
        // Bounded, allocation-free capture. Format/log only after the window.
        static readonly ImageOwnSpike[] imageOwnSpikeSamples=new ImageOwnSpike[16];
        static int imageOwnGc0,imageOwnGc1,imageOwnGc2;
        static double imageOwnMaxFrameMs;
        static bool imageOwnGatePose;
        static bool imageOwnRequirePixelPreflight;
        static readonly System.Collections.Generic.List<int> imageOwnOrderingFallbacks=new System.Collections.Generic.List<int>();
        static bool[] imageOwnOrderingFallbackOwner;
        // Snapshot outside timing. Only registered, non-image exclusions may
        // remain on independent GPU renderers; never rewrite their mask/count.
        static bool CaptureImageOwnOrderingFallbacks()
        {
            imageOwnOrderingFallbackOwner=null;imageOwnOrderingFallbacks.Clear();
            if(gpuLiveSlices.Count!=1||gpuCollapseTextureFallback==null||gpuImageCollapseAddresses==null
                ||!ReferenceEquals(gpuImageCollapseAddressOwner,gpuCollapseTexture)||gpuCollapseFault!=null)return false;
            var source=gpuLiveSlices[0];
            if(gpuCollapseTextureFallback.Length!=source.Data.Items.Length||source.MasterItems.Length!=source.Data.Items.Length)return false;
            for(int i=0;i<source.Data.Items.Length;i++)
            {
                if(!gpuCollapseTextureFallback[i])continue;
                if(GpuImageActor(gpuLiveOwnedIndices[source.MasterItems[i]])
                    ||!gpuImageCollapseAddresses.ContainsKey(source.Data.Items[i].TextureId))return false;
                imageOwnOrderingFallbacks.Add(i);
            }
            if(imageOwnOrderingFallbacks.Count!=gpuCollapseTextureFallbackCount
                ||gpuCollapseTextureFallbackCount*100L>source.Data.Items.Length)return false;
            imageOwnOrderingFallbackOwner=gpuCollapseTextureFallback;
            return true;
        }
        static bool ImageOwnOrderingFallbacksUnchanged()
        {
            if(imageOwnOrderingFallbackOwner==null||!ReferenceEquals(imageOwnOrderingFallbackOwner,gpuCollapseTextureFallback)
                ||!ReferenceEquals(gpuImageCollapseAddressOwner,gpuCollapseTexture)||gpuCollapseFault!=null
                ||imageOwnOrderingFallbacks.Count!=gpuCollapseTextureFallbackCount)return false;
            // Count plus membership also rejects a same-count replacement by
            // an image. Check the approved subset (run8: 14), not 28,523 items.
            foreach(int item in imageOwnOrderingFallbacks)
                if(!gpuCollapseTextureFallback[item])return false;
            return true;
        }
        static void CheckGpuImageCollapse()
        {
            RequireLive(gpuLiveActive&&FrameCompare.Paused&&gpuImageCandidate&&gpuImageOwn&&GpuLiveOwned==28523
                &&gpuLiveCpuFallbackCount==0&&!gpuCollapseOn&&gpuCollapseTexture!=null&&gpuLiveSlices.Count==1,
                "image collapsecheck requires paused prepared image ownership before collapse on");
            RequireLive(ReferenceEquals(gpuImageCollapseAddressOwner,gpuCollapseTexture)&&gpuImageCollapseAddresses!=null,
                "image collapse address hooks missing");
            int missing=0,registeredExcluded=0,imageExcluded=0,variantsMissing=0;
            var source=gpuLiveSlices[0];
            for(int i=0;i<source.Data.Items.Length;i++)
            {
                if(!gpuImageCollapseAddresses.ContainsKey(source.Data.Items[i].TextureId))missing++;
                else if(gpuCollapseTextureFallback[i])registeredExcluded++;
                if(gpuCollapseTextureFallback[i]&&GpuImageActor(gpuLiveOwnedIndices[source.MasterItems[i]]))imageExcluded++;
            }
            RequireLive(gpuImageCollapseVariants!=null,"image collapse window selection missing");
            for(int i=0;i<gpuImageVariants.Length;i++)
                if(gpuImageCollapseVariants[i]&&gpuImageVariants[i].Status==GpuDecoImageVariant.Kind.ReadyQuad
                    &&!gpuImageCollapseAddresses.ContainsKey(gpuImageVariants[i].TextureId))variantsMissing++;
            // 이미지 장식이 하나도 빠지지 않고 텍스처 누락이 없으면 통과한다. 이미지가 아닌 소유 장식의 정렬 보호 폴백(같은 정렬 묶음의 활성 장벽 사이)은
            // 의도된 독립 렌더러 경로이므로 허용하되, 소유 수의 1% 이하여야 한다.
            bool valid=missing==0&&imageExcluded==0&&variantsMissing==0&&CaptureImageOwnOrderingFallbacks();
            GpuLog("GPUIMG_DIAG mode=collapsecheck current_texture_missing="+missing+" registered_but_excluded="+registeredExcluded
                +" image_excluded="+imageExcluded+" variant_entries_missing="+variantsMissing+" fallback="+gpuCollapseTextureFallbackCount
                +" nonimage_excluded="+(gpuCollapseTextureFallbackCount-imageExcluded)+" nonimage_limit="+(source.Data.Items.Length/100)
                +" spans="+gpuCollapseSpans.Count+" valid="+valid+" performance=False");
            if(!valid)GpuLog("IMAGEOWN=FAIL fps_before=NA fps_after=NA gain_pct=NA pixel_max=NA reason=COLLAPSE_PREFLIGHT_FAILED");
            RequireLive(valid,"image collapse preflight failed; retain ordering/texture guards and inspect GPUIMG_DIAG");
        }
        static void RequireImageOwnEnvironment()
        {
            RequireLive(Screen.width==3440&&Screen.height==1440&&Math.Abs(Screen.currentResolution.refreshRateRatio.value-165)<.02,
                "imageown requires 3440x1440@165; do not change the display automatically");
        }
        static void ObserveImageOwnWindow(Camera camera)
        {
            if(imageOwnWindow<0||camera!=gpuLiveCamera||imageOwnObservedFrame==Time.frameCount)return;
            imageOwnObservedFrame=Time.frameCount;
            bool images=imageOwnWindow==1||imageOwnWindow==2;
            imageOwnCpuFallback=Math.Max(imageOwnCpuFallback,gpuLiveCpuFallbackCount);
            if(images)imageOwnTextureFallback=Math.Max(imageOwnTextureFallback,gpuCollapseTextureFallbackCount);
            if(!gpuLiveActive||!gpuCollapseOn||FrameCompare.Paused||!Hitch.Playing||gpuImageCandidate!=images
                ||GpuLiveOwned!=(images?28523:23547)||gpuLiveCpuFallbackCount!=0||gpuCollapseFault!=null
                ||(images&&!ImageOwnOrderingFallbacksUnchanged())||DrawCollapseGuard.IgnoreOrderViolations
                ||gpuImageRefreshPose!=(images&&imageOwnGatePose)||GpuImageVertexDiagnosticActive)imageOwnFailed=imageOwnWindowFailed=true;
            if(Time.frameCount<=imageOwnFrame)return;
            imageOwnObserved++;
            double ms=Time.unscaledDeltaTime*1000.0;
            if(ms<=0||double.IsNaN(ms)||double.IsInfinity(ms))imageOwnFailed=imageOwnWindowFailed=true;
            else imageOwnMaxFrameMs=Math.Max(imageOwnMaxFrameMs,ms);
            if(ms>=30)
            {
                if(imageOwnSpikes<imageOwnSpikeSamples.Length)
                    imageOwnSpikeSamples[imageOwnSpikes]=new ImageOwnSpike{Frame=Time.frameCount,Song=MergeSong,Milliseconds=ms,
                        Gc0=GC.CollectionCount(0)-imageOwnGc0,Gc1=GC.CollectionCount(1)-imageOwnGc1,Gc2=GC.CollectionCount(2)-imageOwnGc2};
                imageOwnSpikes++;
            }
        }
        static bool GpuImageAcceptanceCommand(string command)
        {
            var args=command.Split(' ');
            if(command=="images gate reset")
            {
                RequireLive(!gpuLiveActive&&imageOwnWindow<0,"imageown gate reset requires inactive GPU");
                Camera.onPreCull-=ObserveImageOwnWindow;
                Array.Clear(imageOwnFps,0,4);Array.Clear(imageOwnWindows,0,4);Array.Clear(imageOwnRestores,0,4);Array.Clear(imageOwnPixels,0,3);
                imageOwnFailed=imageOwnWindowFailed=false;imageOwnNextWindow=0;imageOwnGatePose=false;imageOwnRequirePixelPreflight=false;
                imageOwnOrderingFallbackOwner=null;imageOwnOrderingFallbacks.Clear();
                imageOwnMaxPixel=-1;imageOwnCpuFallback=imageOwnTextureFallback=imageOwnTotalSpikes=0;return true;
            }
            if(command=="images gate preflight on")
            {
                RequireLive(!gpuLiveActive&&imageOwnWindow<0&&imageOwnNextWindow==0,"select image preflight before ABBA");
                imageOwnRequirePixelPreflight=true;return true;
            }
            if(command=="images collapsecheck"){CheckGpuImageCollapse();return true;}
            if(command=="images gate refreshpose on"||command=="images gate refreshpose off")
            {
                RequireLive(!gpuLiveActive&&imageOwnWindow<0&&imageOwnNextWindow==0,"select measured pose mode before ABBA");
                imageOwnGatePose=command.EndsWith(" on",StringComparison.Ordinal);return true;
            }
            if(args.Length==3&&args[0]=="images"&&args[1]=="begin")
            {
                int index=int.Parse(args[2],Inv);bool images=index==1||index==2;
                RequireImageOwnEnvironment();
                RequireLive(!imageOwnRequirePixelPreflight||imageOwnPixels.All(x=>x),"complete all three integrated pixel checks before ABBA");
                // A failed measurement still completes its place in A/B/B/A.
                // Keep validity latched for the final gate; require safe restore
                // before advancing, and never replay a failed index as a retry.
                RequireLive(index>=0&&index<4&&imageOwnWindow<0&&index==imageOwnNextWindow
                    &&(index==0||imageOwnRestores[index-1])&&gpuLiveActive
                    &&gpuCollapseOn&&GpuLiveOwned==(images?28523:23547)&&gpuLiveCpuFallbackCount==0
                    &&gpuImageCandidate==images&&(!images||gpuImageOwn)&&gpuImageRefreshPose==(images&&imageOwnGatePose)
                    &&!GpuImageVertexDiagnosticActive&&!DrawCollapseGuard.IgnoreOrderViolations&&!FrameCompare.Paused&&Hitch.Playing&&Math.Abs(MergeSong-260)<.6,
                    "imageown begin requires clean A/B/B/A 260s image ownership with collapse");
                RequireLive(!images||CaptureImageOwnOrderingFallbacks(),"imageown begin found image/unregistered/excess collapse fallback");
                imageOwnWindow=index;imageOwnFrame=Time.frameCount;imageOwnObservedFrame=-1;imageOwnObserved=imageOwnSpikes=0;
                imageOwnWindowFailed=false;imageOwnMaxFrameMs=0;
                imageOwnGc0=GC.CollectionCount(0);imageOwnGc1=GC.CollectionCount(1);imageOwnGc2=GC.CollectionCount(2);
                imageOwnTick=System.Diagnostics.Stopwatch.GetTimestamp();
                Camera.onPreCull-=ObserveImageOwnWindow;Camera.onPreCull+=ObserveImageOwnWindow;
                GpuLog("IMAGEOWN_BEGIN index="+index+" images="+images+" owned="+GpuLiveOwned+" collapse=True");return true;
            }
            if(command=="images end")
            {
                RequireLive(imageOwnWindow>=0,"imageown window not open");
                ObserveImageOwnWindow(gpuLiveCamera);
                int index=imageOwnWindow;imageOwnWindow=-1;Camera.onPreCull-=ObserveImageOwnWindow;
                RequireImageOwnEnvironment();
                double seconds=(System.Diagnostics.Stopwatch.GetTimestamp()-imageOwnTick)/(double)System.Diagnostics.Stopwatch.Frequency;
                int frames=Time.frameCount-imageOwnFrame;
                bool valid=!imageOwnWindowFailed&&seconds>0&&frames>0&&imageOwnObserved>=Math.Max(1,frames-1)&&Math.Abs(MergeSong-270)<.6
                    &&gpuLiveActive&&gpuCollapseOn&&gpuLiveCpuFallbackCount==0&&gpuCollapseFault==null&&gpuImageRestores==0
                    &&(!(index==1||index==2)||ImageOwnOrderingFallbacksUnchanged())
                    &&GpuDecoCopy.ReadTotals().Errors==0&&Resilience.TotalErrors==0&&imageOwnSpikes==0;
                imageOwnTotalSpikes+=imageOwnSpikes;
                imageOwnFps[index]=frames/Math.Max(seconds,1e-9);imageOwnWindows[index]=valid;imageOwnNextWindow=index+1;
                imageOwnFailed|=!valid;
                GpuLog("IMAGEOWN_WINDOW index="+index+" fps="+imageOwnFps[index].ToString("F6",Inv)+" frames="+frames
                    +" observed="+imageOwnObserved+" spikes_30ms="+imageOwnSpikes+" fallback="+gpuLiveCpuFallbackCount
                    +" texture_fallback="+gpuCollapseTextureFallbackCount+" valid="+valid
                    +" max_frame_ms="+imageOwnMaxFrameMs.ToString("F6",Inv)+" runtime_ok="+!imageOwnWindowFailed
                    +" gc0="+(GC.CollectionCount(0)-imageOwnGc0)+" gc1="+(GC.CollectionCount(1)-imageOwnGc1)+" gc2="+(GC.CollectionCount(2)-imageOwnGc2)
                    +" spike_samples="+Math.Min(imageOwnSpikes,imageOwnSpikeSamples.Length)+" spike_samples_dropped="+Math.Max(0,imageOwnSpikes-imageOwnSpikeSamples.Length));
                for(int n=0;n<Math.Min(imageOwnSpikes,imageOwnSpikeSamples.Length);n++)
                {
                    var spike=imageOwnSpikeSamples[n];
                    GpuLog("GPUIMG_DIAG mode=windowspike index="+index+" frame="+spike.Frame+" song="+spike.Song.ToString("F6",Inv)
                        +" ms="+spike.Milliseconds.ToString("F6",Inv)+" gc0_since_begin="+spike.Gc0+" gc1_since_begin="+spike.Gc1+" gc2_since_begin="+spike.Gc2
                        +" attribution=UNPROVEN logged_after_window=True");
                }
                LogGpuImagePlayback();return true;
            }
            if(args.Length==3&&args[0]=="images"&&args[1]=="restore")
            {
                int index=int.Parse(args[2],Inv);
                RequireLive(index>=0&&index<4&&imageOwnWindow<0&&index==imageOwnNextWindow-1,"imageown restore requires the last completed window");
                imageOwnRestores[index]=!gpuLiveActive&&!gpuCollapseOn&&gpuLastRestoreComplete&&CountUnrestoredOwned()==0
                    &&gpuCollapseFault==null&&Resilience.TotalErrors==0;
                imageOwnFailed|=!imageOwnRestores[index];
                GpuLog("IMAGEOWN_RESTORE index="+index+" valid="+imageOwnRestores[index]);return true;
            }
            if(args.Length==3&&args[0]=="images"&&(args[1]=="pixel"||args[1]=="pixelchecked"))
            {
                int index=int.Parse(args[2],Inv);double[] targets={260,262,270};
                RequireImageOwnEnvironment();
                RequireLive(index>=0&&index<3&&gpuImageCandidate&&gpuImageOwn&&gpuLiveActive&&GpuLiveOwned==28523
                    &&gpuLiveCpuFallbackCount==0&&gpuImageRefreshPose==imageOwnGatePose&&FrameCompare.Paused&&!gpuCollapseOn
                    &&!GpuImageVertexDiagnosticActive&&Math.Abs(MergeSong-targets[index])<.6,"imageown pixels require frozen 260/262/270 comparison");
                var pixels=FrameCompare.CompareShots("O","B",out _);
                var noise=FrameCompare.CompareShots("G","N",out bool quiet);
                var restore=FrameCompare.CompareShots("G","G2",out bool same);
                bool controls=true;
                if(args[1]=="pixelchecked")
                {
                    var originalNoise=FrameCompare.CompareShots("O","O2",out bool originalQuiet);
                    var collapseNoise=FrameCompare.CompareShots("B","BN",out bool collapseQuiet);
                    controls=originalQuiet&&collapseQuiet&&originalNoise.Changed==0&&collapseNoise.Changed==0;
                    GpuLog("GPUIMG_DIAG mode=collapse_controls original_noise="+originalNoise.Changed+" collapse_noise="+collapseNoise.Changed);
                }
                imageOwnMaxPixel=Math.Max(imageOwnMaxPixel,pixels.Max.Max());
                imageOwnPixelOrderingOk=CaptureImageOwnOrderingFallbacks();
                imageOwnPixels[index]=pixels.Max.Max()<=ImageOwnPixelMax&&quiet&&same&&noise.Changed==0&&restore.Changed==0
                    &&controls&&gpuCollapseFault==null&&GpuDecoCopy.ReadTotals().Errors==0&&imageOwnPixelOrderingOk;
                GpuLog("IMAGEOWN_PIXEL_CONDS max_ok="+(pixels.Max.Max()<=ImageOwnPixelMax)+" quiet="+quiet+" same="+same+" noise0="+(noise.Changed==0)+" restore0="+(restore.Changed==0)
                    +" controls="+controls+" fault_null="+(gpuCollapseFault==null)+" read_err0="+(GpuDecoCopy.ReadTotals().Errors==0)+" ordering_ok="+imageOwnPixelOrderingOk);
                imageOwnFailed|=!imageOwnPixels[index];
                GpuLog("IMAGEOWN_PIXEL second="+targets[index]+" changed="+pixels.Changed+" maxdiff="+pixels.Max.Max()
                    +" noise="+noise.Changed+" restore="+restore.Changed+" fallback="+gpuCollapseTextureFallbackCount
                    +" nonimage_limit="+(gpuLiveSlices[0].Data.Items.Length/100)+" valid="+imageOwnPixels[index]);
                if(imageOwnRequirePixelPreflight)
                {
                    if(!imageOwnPixels[index])GpuLog("IMAGEOWN=FAIL fps_before=NA fps_after=NA gain_pct=NA pixel_max="+imageOwnMaxPixel+" reason=INTEGRATED_PIXEL_PREFLIGHT_FAILED");
                    RequireLive(imageOwnPixels[index],"integrated image pixels failed; do not start ABBA");
                }
                return true;
            }
            if(command=="images result")
            {
                bool measured=imageOwnNextWindow==4&&imageOwnWindow<0;
                bool complete=measured&&imageOwnRestores.All(x=>x)&&imageOwnPixels.All(x=>x);
                double before=(imageOwnFps[0]+imageOwnFps[3])*.5,after=(imageOwnFps[1]+imageOwnFps[2])*.5;
                double gain=before>0?(after/before-1)*100:double.NaN;
                // Compare FPS directly so an exact +20% does not fail when
                // converting (1.2 - 1) to percent rounds below 20.
                bool pass=complete&&imageOwnWindows.All(x=>x)&&!imageOwnFailed&&before>0&&after>=before*1.20;
                GpuLog("IMAGEOWN="+(pass?"PASS":"FAIL")+" fps_before="+(measured?before.ToString("F6",Inv):"NA")+" fps_after="+(measured?after.ToString("F6",Inv):"NA")
                    +" gain_pct="+(measured?gain.ToString("F6",Inv):"NA")+" pixel_max="+(imageOwnMaxPixel>=0?imageOwnMaxPixel.ToString(Inv):"NA")
                    +" fallback_max="+(measured?imageOwnCpuFallback.ToString(Inv):"NA")+" texture_fallback_B_max="+(measured?imageOwnTextureFallback.ToString(Inv):"NA")
                    +" spikes_30ms="+(measured?imageOwnTotalSpikes.ToString(Inv):"NA")+" completed="+imageOwnNextWindow+"/4 windows="+imageOwnWindows.Count(x=>x)+"/4 pixels="+imageOwnPixels.Count(x=>x)
                    +"/3 restores="+imageOwnRestores.Count(x=>x)+"/4 reason="+(pass?"GAME_GATE_COMPLETE":complete?"ACCEPTANCE_FAILED":"EVIDENCE_INCOMPLETE")
                    +" installation_restore=EXTERNAL full_lifetime=UNMEASURED");return true;
            }
            return false;
        }
        static GpuDecoData.Item ImageFixtureItem(scrVisualDecoration d,GpuDecoImageVariant v)=>ImageFixtureItem(d,v,out _);
        static GpuDecoData.Item ImageFixtureItem(scrVisualDecoration d,GpuDecoImageVariant v,out LiveDecoGeometry g)
        {
            var r=d.spriteRenderer;
            g=LiveDecoGeometry.Capture(d.pivotTrans,d.childTransform,r.transform,v.Vertices,r.flipX,r.flipY);
            RequireLive(g!=null,"image fixture unsupported child chain");
            var ts=d.scaleMultiplier*d.textureScaleMultiplier;var c=d.color;var px=d.parallax.multiplier;
            var item=new GpuDecoData.Item
            {
                Id=d.GetInstanceID(),TextureId=v.TextureId,
                Flags=(d.lockScale?1:0)|(d.lockRotation?2:0)|(d.parallax.dontAlterX?4:0)|(d.parallax.dontAlterY?8:0)|(r.flipX?16:0)|(r.flipY?32:0),
                Constants=new[]{ts.x,ts.y,d.textureScaleMultiplier.x,d.textureScaleMultiplier.y,d.startRot,d.croppedPivotOffset.x,d.croppedPivotOffset.y,d.pivotTrans.position.z},
                Initial=new[]{d.pivotPosVec.x,d.pivotPosVec.y,d.pivotOffsetVec.x,d.pivotOffsetVec.y,d.rotAngle,d.scaleVec.x,d.scaleVec.y,c.r,c.g,c.b,c.a,d.opacity,px.x,px.y,d.parallaxOffset.x,d.parallaxOffset.y},
                Triangles=v.Triangles.Select(x=>(int)x).ToArray()
            };
            for(int ch=0;ch<16;ch++)item.Tracks[ch]=Array.Empty<DecoBakeCore.Segment>();
            for(int n=0;n<4;n++)
            {
                PutVec(item.Shape,n*3,g.Shape[n]);item.UV[n*2]=v.UV[n].x;item.UV[n*2+1]=v.UV[n].y;
            }
            var origin=g.Offset(Vector2.zero);PutVec(item.OffsetBasis,0,origin);
            PutVec(item.OffsetBasis,3,g.Offset(Vector2.right)-origin);PutVec(item.OffsetBasis,6,g.Offset(Vector2.up)-origin);
            return item;
        }
        static GpuDecoData ImageFixtureData(GpuDecoData.Item a,GpuDecoData.Item b)=>new GpuDecoData
        {Items=new[]{a,b},Pitch=1,TweenScale=1,Camera=new[]{0f,0f,0f,1f}};
        static int ImageVertexDifferences(GpuDecoGpu.Vertex[] a,GpuDecoGpu.Vertex[] b)
        {
            int bad=0;
            for(int i=0;i<a.Length;i++)
            {
                var x=a[i];var y=b[i];
                // Exact fresh-vs-update GPU equality, not a screen-pixel gate.
                if(!x.position.Equals(y.position)||!x.color.Equals(y.color)||!x.uv.Equals(y.uv))bad++;
                if(!DecoBakeCore.Finite(x.position.x)||!DecoBakeCore.Finite(x.position.y)||!DecoBakeCore.Finite(x.position.z))bad++;
            }
            return bad;
        }
        static void RunGpuImageGeometryFixture()
        {
            RequireLive(FrameCompare.Paused&&!gpuLiveActive&&!gpuLivePrepared&&!gpuImageShadow&&g2Window<0,"image fixture requires paused CPU and no prepared GPU resources");
            RequireLive(gpuImageVariants!=null&&!string.IsNullOrEmpty(gpuBundlePath),"image fixture requires compiled variants and bundle");
            var variants=gpuImageVariants.Where(v=>v.Status==GpuDecoImageVariant.Kind.ReadyQuad).ToArray();
            RequireLive(variants.Length>0,"no quad image variants");
            scrVisualDecoration d=null;
            foreach(var obj in baselineObjects)
            {
                var candidate=obj as scrVisualDecoration;
                if(!candidate||!candidate.spriteRenderer||!candidate.spriteRenderer.sprite)continue;
                var ts=candidate.scaleMultiplier*candidate.textureScaleMultiplier;
                if(ts.x==0||ts.y==0||candidate.textureScaleMultiplier.x==0||candidate.textureScaleMultiplier.y==0)continue;
                if(LiveDecoGeometry.Capture(candidate.pivotTrans,candidate.childTransform,candidate.spriteRenderer.transform,
                    variants[0].Vertices,candidate.spriteRenderer.flipX,candidate.spriteRenderer.flipY)==null)continue;
                d=candidate;break;
            }
            RequireLive(d,"no valid image fixture child chain");
            AssetBundle bundle=null;
            int updated=0,topologyRejected=0,bad=0,rejected=0,pointerChanges=0;
            try
            {
                bundle=AssetBundle.LoadFromFile(System.IO.Path.GetFullPath(gpuBundlePath));RequireLive(bundle,"image fixture bundle load");
                var shader=bundle.LoadAsset<ComputeShader>("Assets/Shaders/GpuDeco.compute");RequireLive(shader,"image fixture compute asset");
                var original=ImageFixtureItem(d,variants[0]);var neighbor=ImageFixtureItem(d,variants[0]);
                neighbor.Initial=(float[])neighbor.Initial.Clone();neighbor.Initial[0]+=17f;neighbor.Initial[7]=.3125f;
                var data=ImageFixtureData(original,neighbor);
                using(var gpu=new GpuDecoGpu(shader,data){UseAnchors=false})
                {
                    var pointer=gpu.VertexBuffer.GetNativeBufferPtr();
                    foreach(var variant in variants)
                    {
                        for(int lane=0;lane<2;lane++)
                        {
                            var next=ImageFixtureItem(d,variant);
                            if(!next.Triangles.SequenceEqual(original.Triangles)){topologyRejected++;continue;}
                            next.Initial=data.Items[lane].Initial;next.Tracks=data.Items[lane].Tracks;next.Anchors=data.Items[lane].Anchors;
                            var before=gpu.Dispatch(0,true).vertices;
                            gpu.UpdateImageGeometry(lane,next);
                            var actual=gpu.Dispatch(0,true).vertices;
                            using(var fresh=new GpuDecoGpu(shader,ImageFixtureData(data.Items[0],data.Items[1])){UseAnchors=false})
                                bad+=ImageVertexDifferences(actual,fresh.Dispatch(0,true).vertices);
                            for(int i=0;i<4;i++)
                            {
                                int n=(1-lane)*4+i;
                                if(!before[n].position.Equals(actual[n].position)||!before[n].color.Equals(actual[n].color)||!before[n].uv.Equals(actual[n].uv))bad++;
                            }
                            if(pointer!=gpu.VertexBuffer.GetNativeBufferPtr())pointerChanges++;
                            updated++;
                        }
                    }
                    // Invalid input must not alter either lane or publish next.
                    for(int test=0;test<12;test++)
                    {
                        var next=ImageFixtureItem(d,variants[0]);int lane=0;
                        next.Initial=data.Items[0].Initial;next.Tracks=data.Items[0].Tracks;next.Anchors=data.Items[0].Anchors;
                        switch(test)
                        {
                            case 0:lane=-1;break;case 1:lane=2;break;case 2:next=null;break;
                            case 3:next.Shape=new float[11];break;case 4:next.Triangles[0]=(next.Triangles[0]+1)%4;break;
                            case 5:next.UV[0]=float.NaN;break;case 6:next.Shape[0]=float.PositiveInfinity;break;
                            case 7:next.OffsetBasis[0]=float.NaN;break;case 8:next.Constants[0]=float.NaN;break;
                            case 9:next.Constants[2]=0;break;case 10:next.Flags|=128;break;case 11:next.Initial=(float[])next.Initial.Clone();break;
                        }
                        var before=gpu.Dispatch(0,true).vertices;var prior=data.Items[0];bool threw=false;
                        try{gpu.UpdateImageGeometry(lane,next);}catch(InvalidOperationException){threw=true;rejected++;}
                        RequireLive(threw,"invalid image geometry accepted");
                        bad+=ImageVertexDifferences(before,gpu.Dispatch(0,true).vertices);
                        RequireLive(ReferenceEquals(prior,data.Items[0]),"rejected image metadata published");
                    }
                    // Restore geometry while retaining each lane's original logical channel arrays.
                    gpu.UpdateImageGeometry(0,original);gpu.UpdateImageGeometry(1,neighbor);
                    using(var fresh=new GpuDecoGpu(shader,ImageFixtureData(original,neighbor)){UseAnchors=false})
                        bad+=ImageVertexDifferences(gpu.Dispatch(0,true).vertices,fresh.Dispatch(0,true).vertices);
                    if(pointer!=gpu.VertexBuffer.GetNativeBufferPtr())pointerChanges++;
                }
            }
            finally{if(bundle)bundle.Unload(true);}
            GpuLog("image_geometry_fixture variants="+variants.Length+" lane_updates="+updated+" topology_rejected="+topologyRejected
                +" vertex_mismatch="+bad+" invalid_rejected="+rejected+" vertex_buffer_replaced="+pointerChanges
                +" CPU_setters_changed=0 GPU_texture_binding=False physical_pixels=UNMEASURED performance=False");
            RequireLive(updated>0&&bad==0&&rejected==12&&pointerChanges==0,"image geometry fixture failed");
        }
    }
}
#endif
