#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using N = StutterFix.DirectFullNative;

namespace StutterFix
{
    internal static unsafe partial class DirectFull
    {
        static bool skipExplicit, allExplicitPasses, localPrecision;
        static bool deferredSubmission, legacyConstants;
        static N.DrawProbeConfiguration drawProbeConfig = new N.DrawProbeConfiguration { Count = 256, Interval = 64 };
        static string SafePath(Transform t) { string path = t.name; for (t = t.parent; t; t = t.parent) path = t.name + "/" + path; return Clean(path); }
        static string DrawGpuCommand(string arg)
        {
            if (arg == "dump")
            {
                if (!Rendering) throw new InvalidOperationException("drawgpu dump requires on");
                N.DrawProbeReadback q = new N.DrawProbeReadback { Index = uint.MaxValue }; N.Require(N.Op.DrawProbeRead, fullToken, &q);
                string prefix = Path.Combine(Main.Entry.Path, "drfull-drawgpu-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
                var rows = new StringBuilder("frame\tsegment\tordinal\tdraw\tfirst_list\tsources\tindices\tgpu_us\tsegment_us\tusage\tbudget\n");
                var ids = new HashSet<uint>();
                for (uint i = 0; i < q.Total; i++)
                {
                    q.Index = i; N.Require(N.Op.DrawProbeRead, fullToken, &q); var r = q.Row; ids.Add(r.Draw);
                    rows.AppendLine(FormattableString.Invariant($"{r.Frame}\t{r.Segment}\t{r.Ordinal}\t{r.Draw}\t{r.First}\t{r.Count}\t{r.Indices}\t{r.Us:R}\t{r.SegmentUs:R}\t{r.Usage}\t{r.Budget}"));
                }
                var textures = new StringBuilder("draw\ttexture_index\tstage\tslot\tsampler_slot\tpath\tshader\ttexture\tunity_width\tunity_height\tunity_filter\tunity_wrapU\tunity_wrapV\tunity_aniso\tunity_srgb\twidth\theight\tmips\tresource_format\tsrv_format\tmost_detailed_mip\tview_mips\tfilter\twrapU\twrapV\taniso\tmip_bias\tmin_lod\tmax_lod\n");
                foreach (uint id in ids)
                try
                {
                    Bound bound = null;
                    foreach (var source in sources) if (source != null && source.Passes != null) foreach (var b in source.Passes) if (b != null && b.NativeId == id) bound = b;
                    N.TextureProbeInfo t = new N.TextureProbeInfo { Draw = id }; N.Require(N.Op.TextureProbe, fullToken, &t);
                    for (uint j = 0; j < t.Total; j++)
                    {
                        t.Index = j; N.Require(N.Op.TextureProbe, fullToken, &t);
                        var tex = bound != null && bound.TextureObjects != null && j < bound.TextureObjects.Length ? bound.TextureObjects[j] : null;
                        textures.AppendLine(FormattableString.Invariant($"{id}\t{j}\t{t.Stage}\t{t.Slot}\t{t.SamplerSlot}\t{(bound != null && bound.Source != null && bound.Source.R ? SafePath(bound.Source.R.transform) : "retired-or-composite")}\t{bound?.Shader?.Name}\t{tex?.name}\t{(tex ? tex.width : 0)}\t{(tex ? tex.height : 0)}\t{(tex ? (int)tex.filterMode : -1)}\t{(tex ? (int)tex.wrapModeU : -1)}\t{(tex ? (int)tex.wrapModeV : -1)}\t{(tex ? tex.anisoLevel : -1)}\t{(tex && tex.isDataSRGB)}\t{t.Width}\t{t.Height}\t{t.Mips}\t{t.ResourceFormat}\t{t.ViewFormat}\t{t.MostDetailedMip}\t{t.ViewMips}\t{t.Filter}\t{t.WrapU}\t{t.WrapV}\t{t.Aniso}\t{t.MipBias:R}\t{t.MinLod:R}\t{t.MaxLod:R}"));
                    }
                }
                catch (Exception e) { textures.AppendLine(id + "	error	" + e.GetType().Name + ": " + e.Message); }
                File.WriteAllText(prefix + ".tsv", rows.ToString(), new UTF8Encoding(false));
                File.WriteAllText(prefix + "-textures.tsv", textures.ToString(), new UTF8Encoding(false));
                return FormattableString.Invariant($"drawgpu rows={q.Total} pending={q.Pending} skipped={q.Skipped} invalid={q.Invalid} overwritten_rows={q.Overflow} first={drawProbeConfig.First} count={drawProbeConfig.Count} interval={drawProbeConfig.Interval} output={prefix} quality_aniso={QualitySettings.anisotropicFiltering} color_space={QualitySettings.activeColorSpace} gpu_elapsed_not_busy=True Unity_actual_SRV=UNMEASURED");
            }
            if (Active || preparing) throw new InvalidOperationException("drawgpu configuration requires off");
            if (arg == "off") { drawProbeConfig.Enabled = 0; return "drawgpu enabled=False"; }
            string[] a = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (a.Length != 4 || a[0] != "on" || !uint.TryParse(a[1], out uint first) || !uint.TryParse(a[2], out uint count) || !uint.TryParse(a[3], out uint interval) || count == 0 || count > 256 || interval == 0 || first > uint.MaxValue-count)
                throw new ArgumentException("drawgpu on <first-visible-draw> <count 1..256> <sample-frame-interval> | off | dump");
            drawProbeConfig = new N.DrawProbeConfiguration { Enabled = 1, First = first, Count = count, Interval = interval };
            return "drawgpu enabled=True diagnostic_only=True";
        }
        static int? savedTextureLimit;
        static string PressureMip(int value)
        {
            if (Active || preparing) throw new InvalidOperationException("pressure diagnostic requires off");
            if (value < -1 || value > 16) throw new ArgumentOutOfRangeException(nameof(value));
            if (value < 0) { if (savedTextureLimit.HasValue) QualitySettings.globalTextureMipmapLimit = savedTextureLimit.Value; savedTextureLimit = null; }
            else { if (!savedTextureLimit.HasValue) savedTextureLimit = QualitySettings.globalTextureMipmapLimit; QualitySettings.globalTextureMipmapLimit = value; }
            return "pressure_mip=" + QualitySettings.globalTextureMipmapLimit + " diagnostic_only=True verify_actual_DXGI_budget=True";
        }
        static bool recordingExplicit;
        static IntPtr timelineCallback;
        static CommandBuffer auxiliaryTimelineStart, auxiliaryTimelineEnd;
        static readonly List<(CameraEvent Event, CommandBuffer Buffer)> timelineBuffers = new List<(CameraEvent, CommandBuffer)>();
        static readonly Plane[] explicitPlanes = new Plane[6];
        static readonly HashSet<Source> explicitSources = new HashSet<Source>();
        static long explicitTests, explicitCulled, explicitVisibilityChanges, explicitSubmitted, explicitPassesRejected;
        static float[] explicitLayerDistances; static bool explicitDistancesDirty = true;
        static void Timeline(CommandBuffer cb, int marker) => cb.IssuePluginEvent(timelineCallback, (fullToken << 16) | marker);
        static void InstallTimeline()
        {
            timelineCallback = new IntPtr(N.Call(N.Op.TimelineEvent, fullToken));
            if (timelineCallback == IntPtr.Zero) throw new InvalidOperationException("GPU timeline callback missing");
            auxiliaryTimelineStart = new CommandBuffer { name = "StutterFix.DirectFull.Timeline.AuxBegin" };
            auxiliaryTimelineEnd = new CommandBuffer { name = "StutterFix.DirectFull.Timeline.AuxEnd" };
            Timeline(auxiliaryTimelineStart, 2); Timeline(auxiliaryTimelineEnd, 3);
            // BeforeForwardOpaque is the first guaranteed event in this forward
            // camera. Depth prepasses/clear before it are outside this envelope.
            foreach (var pair in new[] {
                (CameraEvent.AfterForwardOpaque, 17), (CameraEvent.AfterForwardAlpha, 19),
                (CameraEvent.BeforeImageEffects, 20), (CameraEvent.AfterImageEffects, 21), (CameraEvent.AfterEverything, 1) })
            {
                var cb = new CommandBuffer { name = "StutterFix.DirectFull.Timeline." + pair.Item1 };
                Timeline(cb, pair.Item2); camera.AddCommandBuffer(pair.Item1, cb); timelineBuffers.Add((pair.Item1, cb));
            }
        }
        static void ReleaseTimeline()
        {
            foreach (var item in timelineBuffers) { if (camera) camera.RemoveCommandBuffer(item.Event, item.Buffer); item.Buffer.Release(); }
            timelineBuffers.Clear(); timelineCallback = IntPtr.Zero; explicitSources.Clear(); recordingExplicit = false;
            if (auxiliary) auxiliary.RemoveAllCommandBuffers();
            auxiliaryTimelineStart?.Release(); auxiliaryTimelineEnd?.Release(); auxiliaryTimelineStart = auxiliaryTimelineEnd = null;
            explicitDistancesDirty = true; explicitLayerDistances = null;
            explicitTests = explicitCulled = explicitVisibilityChanges = explicitSubmitted = explicitPassesRejected = 0;
        }
        static long explicitUnknownPasses;
        static int[] ExplicitPasses(Material material)
        {
            if (allExplicitPasses) return new[] { -1 }; // Diagnostic only; same visibility as the candidate.
            var result = new List<int>();
            for (int i = 0; i < material.passCount; i++)
            {
                string mode = material.shader.FindPassTagValue(i, new ShaderTagId("LightMode")).name;
                string name = material.GetPassName(i);
                if (!material.GetShaderPassEnabled(name)) { explicitPassesRejected++; continue; }
                if (DirectFullExplicitPolicy.CameraPass(mode)) result.Add(i);
                else if (DirectFullExplicitPolicy.NonCameraPass(mode)) explicitPassesRejected++;
                else { explicitUnknownPasses++; return new[] { -1 }; } // 확인 안 된 LightMode(GRABPASS 등): M4d 처럼 모든 패스(-1)로 그린다
            }
            return result.ToArray();
        }
        static void RefreshExplicitPasses(Bound bound)
        {
            int[] next = ExplicitPasses(bound.Material), old = bound.Entry.ExplicitPasses;
            bool same = next.Length == old.Length;
            if (same) for (int i = 0; i < next.Length; i++) if (next[i] != old[i]) { same = false; break; }
            if (!same) { bound.Entry.ExplicitPasses = next; orderCommandsReady = false; orderDirty = true; }
        }
        static bool ExplicitVisible(Source source)
        {
            var r = source.R;
            if (!r || !source.Live || !r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy) return false;
            Bounds b = r.bounds; Vector3 c = b.center, e = b.extents;
            if (!float.IsFinite(c.x) || !float.IsFinite(c.y) || !float.IsFinite(c.z) || !float.IsFinite(e.x) || !float.IsFinite(e.y) || !float.IsFinite(e.z)) return true;
            // Conservative bounds; skinned/particle/line bounds come from Unity,
            // including local/world simulation and renderer-specific expansion.
            b.Expand(Mathf.Max(1, Mathf.Max(c.magnitude, e.magnitude)) * 0.00002f);
            if (!GeometryUtility.TestPlanesAABB(explicitPlanes, b)) return false;
            float distance = explicitLayerDistances[r.gameObject.layer];
            if (distance > 0) { Vector3 offset = c - camera.transform.position; float near = (camera.layerCullSpherical ? offset.magnitude : Vector3.Dot(offset, camera.transform.forward)) - e.magnitude; if (near > distance) return false; }
            return true;
        }
        static void UpdateExplicitVisibility()
        {
            if (explicitSources.Count == 0) return;
            if (explicitDistancesDirty) { explicitLayerDistances = camera.layerCullDistances; explicitDistancesDirty = false; }
            GeometryUtility.CalculateFrustumPlanes(camera, explicitPlanes);
            foreach (var source in explicitSources)
            {
                explicitTests++; bool visible = ExplicitVisible(source); if (!visible) explicitCulled++;
                if (visible != source.ExplicitVisible) { source.ExplicitVisible = visible; explicitVisibilityChanges++; orderDirty = true; }
            }
        }
        static void RecordExplicit(CommandBuffer cb, Entry e, ref int start)
        {
            if (skipExplicit || !e.Source.ExplicitVisible || e.ExplicitPasses.Length == 0) return;
            if (listCount != start || segmentCount == 0) RecordSegment(cb, ref start);
            if (!recordingExplicit) { Timeline(cb, 5); recordingExplicit = true; }
            foreach (int pass in e.ExplicitPasses)
            {
                if (RecordBakedParticle(cb, e, pass)) { explicitSubmitted++; continue; }
                cb.DrawRenderer(e.Source.R, e.Fallback, e.Source.Filter && e.Source.Filter.sharedMesh ? Math.Min(e.Slot, e.Source.Filter.sharedMesh.subMeshCount - 1) : 0, pass);
                explicitSubmitted++;
            }
        }
        static string TimelineState()
        {
            N.TimelinePerformance p = default; N.Require(N.Op.TimelinePerf, fullToken, &p);
            N.SubmissionPerformance submit = default; N.Require(N.Op.SubmissionPerf, fullToken, &submit);
            string submissionState = FormattableString.Invariant($"submit_requested={(deferredSubmission ? "deferred" : "immediate")} submit_immediate_available={submit.ImmediateAvailable} submit_immediate_segments={submit.ImmediateSegments} submit_deferred_segments={submit.DeferredSegments} submit_state_restores={submit.StateRestores} submit_state_us_total={submit.StateUs:F3} submit_record_us_total={submit.RecordUs:F3} order_descriptor_bytes={submit.OrderBytes} order_gpu_dispatches={submit.OrderDispatches} cb_ring_available={submit.RingAvailable} cb_ring_maps={submit.RingMaps} cb_ring_bytes={submit.RingBytes} cb_ring_bindings={submit.RingBindings} ");
            return FormattableString.Invariant($"particles_baked={bakedParticles} particle_bakes={particleBakes} particle_baked_vertices={particleVertices} particle_empty_bakes={particleEmpty} ") + submissionState + FormattableString.Invariant($"gpu_camera_us={p.CameraUs:F3} gpu_native_frame_us={p.NativeUs:F3} gpu_explicit_us={p.ExplicitUs:F3} gpu_composite_us={p.CompositeUs:F3} gpu_other_us={p.OtherUs:F3} gpu_auxiliary_us={p.AuxiliaryUs:F3} gpu_opaque_us={p.OpaqueUs:F3} gpu_opaque_alpha_gap_us={p.GapUs:F3} gpu_alpha_us={p.AlphaUs:F3} gpu_post_alpha_us={p.PostAlphaUs:F3} gpu_effects_us={p.EffectsUs:F3} gpu_tail_us={p.TailUs:F3} gpu_camera_max_us={p.MaxCameraUs:F3} gpu_camera_last_us={p.LastCameraUs:F3} gpu_last_submit_vram_usage={p.LastUsage} gpu_last_submit_vram_budget={p.LastBudget} gpu_last_submit_nonlocal_usage={p.LastNonLocal} gpu_last_sample_serial={p.LastSerial} gpu_max_submit_vram_usage={p.MaxUsage} gpu_max_submit_vram_budget={p.MaxBudget} gpu_max_submit_nonlocal_usage={p.MaxNonLocal} gpu_max_sample_serial={p.MaxSerial} gpu_camera_samples={p.Samples} gpu_auxiliary_samples={p.AuxiliarySamples} gpu_timeline_pending={p.Pending} gpu_timeline_disjoint={p.Disjoint} gpu_timeline_invalid={p.Invalid} gpu_timeline_overflow={p.Overflow} gpu_timeline_skipped={p.Skipped} gpu_timeline_intervals={p.Intervals} gpu_event_unit=segment gpu_camera_scope=BeforeForwardOpaque..AfterEverything skip_explicit={skipExplicit} explicit_all_passes={allExplicitPasses} explicit_tests={explicitTests} explicit_culled={explicitCulled} explicit_visibility_changes={explicitVisibilityChanges} explicit_recorded_draws={explicitSubmitted} explicit_rejected_passes={explicitPassesRejected} precision_local={localPrecision} constant_evaluations={p.ConstantEvaluations} constant_reuses={p.ConstantReuses} constant_bind_skips={p.ConstantBindSkips} empty_native_segments={p.EmptySegments} visible_grab_draws={p.VisibleGrabs} culled_grab_draws={p.CulledGrabs}");
        }
        static string MemoryState()
        {
            N.MemoryPerformance p = default; N.Require(N.Op.MemoryPerf, fullToken, &p);
            long depthBytes = canvasDepth ? (long)canvasDepth.width * canvasDepth.height * UnityEngine.Experimental.Rendering.GraphicsFormatUtility.GetBlockSize(canvasDepth.depthStencilFormat) : 0;
            return FormattableString.Invariant($"gpu_owned_bytes={p.OwnedBytes} gpu_owned_peak_bytes={p.PeakOwnedBytes} gpu_geometry_bytes={p.GeometryBytes} gpu_pose_bytes={p.PoseBytes} gpu_draw_bytes={p.DrawBytes} gpu_index_bytes={p.IndexBytes} gpu_pretransform_bytes={p.PretransformBytes} gpu_constant_bytes={p.ConstantBytes} gpu_color_bytes={p.ColorBytes} gpu_grab_allocated_bytes={p.GrabBytes} gpu_canvas_color_bytes={p.CanvasColorBytes} gpu_canvas_depth_bytes={depthBytes} gpu_path_bytes={p.OwnedBytes+(ulong)depthBytes} vram_supported={p.Supported} vram_before_usage={p.BeforeUsage} vram_before_budget={p.BeforeBudget} vram_prepared_usage={p.PreparedUsage} vram_prepared_budget={p.PreparedBudget} vram_usage={p.CurrentUsage} vram_budget={p.CurrentBudget} vram_peak_usage={p.PeakUsage} vram_nonlocal_usage={p.NonLocalUsage} vram_nonlocal_budget={p.NonLocalBudget} vram_queries={p.Queries} vram_failures={p.Failures} vram_over_budget_samples={p.OverBudgetSamples} vram_max_over_budget_bytes={p.MaxOverBudgetBytes} vram_min_headroom_bytes={p.MinHeadroomBytes}");
        }
    }
}
#endif
