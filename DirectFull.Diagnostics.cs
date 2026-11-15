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
        static N.DrawProbeConfiguration drawProbeConfig = new N.DrawProbeConfiguration { Count = 256, Interval = 4 };
        static N.ResourceConfiguration resourceConfig = new N.ResourceConfiguration { Pretransform = 1, StableUploads = 1, TrimBindings = 1 };
        static string ResourceCommand(string arg)
        {
            if (Active || preparing) throw new InvalidOperationException("resource configuration requires off");
            string[] a = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (arg == "default") resourceConfig = new N.ResourceConfiguration { Pretransform = 1, StableUploads = 1, TrimBindings = 1 };
            else if (a.Length == 2 && (a[1] == "on" || a[1] == "off"))
            {
                uint value = a[1] == "on" ? 1u : 0u;
                switch (a[0]) { case "grab": resourceConfig.SkipGrab = 1-value; break; case "pretransform": resourceConfig.Pretransform = value; break; case "stable": resourceConfig.StableUploads = value; break; case "trim": resourceConfig.TrimBindings = value; break; default: throw new ArgumentException("resources default | grab/pretransform/stable/trim on/off"); }
            }
            else throw new ArgumentException("resources default | grab/pretransform/stable/trim on/off");
            return $"resources skip_grab={resourceConfig.SkipGrab} pretransform={resourceConfig.Pretransform} stable={resourceConfig.StableUploads} trim={resourceConfig.TrimBindings} diagnostic_skip_grab_changes_pixels={resourceConfig.SkipGrab != 0}";
        }
        static string SafePath(Transform t) { string path = t.name; for (t = t.parent; t; t = t.parent) path = t.name + "/" + path; return Clean(path); }
        static string DrawGpuCommand(string arg)
        {
            if (arg == "assert")
            {
                if (!Rendering) throw new InvalidOperationException("drawgpu assert requires on");
                N.DrawProbeReadback read = new N.DrawProbeReadback { Index = uint.MaxValue }; N.Require(N.Op.DrawProbeRead, fullToken, &read);
                N.DrawProbeState status = default; N.Require(N.Op.DrawProbeStatus, fullToken, &status);
                if (read.Total == 0) throw new InvalidOperationException($"drawgpu has no completed samples: first={drawProbeConfig.First} actual_max_visible={status.MaxVisible} pending={read.Pending} skipped={read.Skipped}; use first=0 and interval=4, then extend render time");
                return "drawgpu assert PASS rows=" + read.Total;
            }
            if (arg == "dump")
            {
                if (!Rendering) throw new InvalidOperationException("drawgpu dump requires on");
                N.DrawProbeReadback q = new N.DrawProbeReadback { Index = uint.MaxValue }; N.Require(N.Op.DrawProbeRead, fullToken, &q);
                string prefix = Path.Combine(Main.Entry.Path, "drfull-drawgpu-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
                N.DrawProbeState state = default; N.Require(N.Op.DrawProbeStatus, fullToken, &state);
                var rows = new StringBuilder("frame\tsegment\tordinal\tdraw\tfirst_list\tsources\tindices\tgpu_us\tsegment_us\tusage\tbudget\tprepare_gpu_us\tprepare_constants_us\tgrab_us\n");
                var ids = new HashSet<uint>();
                for (uint i = 0; i < q.Total; i++)
                {
                    q.Index = i; N.Require(N.Op.DrawProbeRead, fullToken, &q); var r = q.Row; ids.Add(r.Draw);
                    N.DrawProbeDetail detail = new N.DrawProbeDetail { Index = i }; N.Require(N.Op.DrawProbeDetailRead, fullToken, &detail);
                    rows.AppendLine(FormattableString.Invariant($"{r.Frame}\t{r.Segment}\t{r.Ordinal}\t{r.Draw}\t{r.First}\t{r.Count}\t{r.Indices}\t{r.Us:R}\t{r.SegmentUs:R}\t{r.Usage}\t{r.Budget}\t{detail.PrepareGpuUs:R}\t{detail.PrepareConstantsUs:R}\t{detail.GrabUs:R}"));
                }
                // Preserve the usable timing dump even if Unity object metadata is gone.
                File.WriteAllText(prefix + ".tsv", rows.ToString(), new UTF8Encoding(false));
                var identities = new StringBuilder("draw\tpath\tshader\tgrab\tgrab_name\n");
                foreach (uint id in ids)
                {
                    Bound bound = null;
                    foreach (var source in sources) if (source?.Passes != null) foreach (var b in source.Passes) if (b != null && b.NativeId == id) bound = b;
                    try { identities.AppendLine($"{id}\t{(bound?.Source?.R ? SafePath(bound.Source.R.transform) : "retired-or-composite")}\t{bound?.Shader?.Name}\t{bound?.Pass?.Grab}\t{bound?.Pass?.GrabName}"); }
                    catch (Exception e) { identities.AppendLine(id + "\terror\t" + Clean(e.ToString())); }
                }
                File.WriteAllText(prefix + "-draws.tsv", identities.ToString(), new UTF8Encoding(false));
                var textures = new StringBuilder("draw\ttexture_index\tstage\tslot\tsampler_slot\tpath\tshader\ttexture\tunity_width\tunity_height\tunity_filter\tunity_wrapU\tunity_wrapV\tunity_aniso\tunity_srgb\twidth\theight\tmips\tresource_format\tsrv_format\tmost_detailed_mip\tview_mips\tfilter\twrapU\twrapV\taniso\tmip_bias\tmin_lod\tmax_lod\n");
                foreach (uint id in ids)
                try
                {
                    Bound bound = null;
                    foreach (var source in sources) if (source != null && source.Passes != null) foreach (var b in source.Passes) if (b != null && b.NativeId == id) bound = b;
                    N.TextureProbeInfo t = new N.TextureProbeInfo { Draw = id }; N.Require(N.Op.TextureProbe, fullToken, &t);
                    for (uint j = 0; j < t.Total; j++)
                    try
                    {
                        t.Index = j; N.Require(N.Op.TextureProbe, fullToken, &t);
                        var tex = bound != null && bound.TextureObjects != null && j < bound.TextureObjects.Length ? bound.TextureObjects[j] : null;
                        textures.AppendLine(FormattableString.Invariant($"{id}\t{j}\t{t.Stage}\t{t.Slot}\t{t.SamplerSlot}\t{(bound != null && bound.Source != null && bound.Source.R ? SafePath(bound.Source.R.transform) : "retired-or-composite")}\t{bound?.Shader?.Name}\t{tex?.name}\t{(tex ? tex.width : 0)}\t{(tex ? tex.height : 0)}\t{(tex ? (int)tex.filterMode : -1)}\t{(tex ? (int)tex.wrapModeU : -1)}\t{(tex ? (int)tex.wrapModeV : -1)}\t{(tex ? tex.anisoLevel : -1)}\t{(tex && tex.isDataSRGB)}\t{t.Width}\t{t.Height}\t{t.Mips}\t{t.ResourceFormat}\t{t.ViewFormat}\t{t.MostDetailedMip}\t{t.ViewMips}\t{t.Filter}\t{t.WrapU}\t{t.WrapV}\t{t.Aniso}\t{t.MipBias:R}\t{t.MinLod:R}\t{t.MaxLod:R}"));
                    }
                    catch (Exception e) { textures.AppendLine(id + "\t" + j + "\terror\t" + Clean(e.ToString())); }
                }
                catch (Exception e) { textures.AppendLine(id + "	error	" + Clean(e.ToString())); }
                File.WriteAllText(prefix + "-textures.tsv", textures.ToString(), new UTF8Encoding(false));
                string coverage = drawProbeConfig.First >= state.MaxVisible ? "OUT_OF_RANGE" : q.Total == 0 ? "NO_COMPLETED_SAMPLES" : "SAMPLED";
                File.WriteAllText(prefix + "-status.txt", $"{coverage} last_visible={state.LastVisible} max_visible={state.MaxVisible} empty_windows={state.EmptyWindows} sampled_segments={state.SampledSegments} completed_segments={state.CompletedSegments} pending={q.Pending} skipped={q.Skipped}", new UTF8Encoding(false));
                return FormattableString.Invariant($"drawgpu coverage={coverage} last_visible={state.LastVisible} max_visible={state.MaxVisible} rows={q.Total} pending={q.Pending} skipped={q.Skipped} invalid={q.Invalid} overwritten_rows={q.Overflow} first={drawProbeConfig.First} count={drawProbeConfig.Count} interval={drawProbeConfig.Interval} output={prefix} quality_aniso={QualitySettings.anisotropicFiltering} color_space={QualitySettings.activeColorSpace} gpu_elapsed_not_busy=True Unity_actual_SRV=UNMEASURED");
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
        static readonly string[] cacheVariableNames = { "material", "unity_ObjectToWorld", "unity_WorldToObject", "unity_MatrixVP", "unity_MatrixV", "glstate_matrix_projection", "unity_MatrixMVP", "unity_MatrixITMV", "_WorldSpaceCameraPos", "_ScreenParams", "_ProjectionParams", "unity_OrthoParams", "_Time", "_SinTime", "_CosTime", "unity_DeltaTime", "_RendererColor", "_Flip" };
        static string TimelineState()
        {
            N.TimelinePerformance p = default; N.Require(N.Op.TimelinePerf, fullToken, &p);
            N.SubmissionPerformance submit = default; N.Require(N.Op.SubmissionPerf, fullToken, &submit);
            N.ResourcePerformance resource = default; N.Require(N.Op.ResourcePerf, fullToken, &resource);
            N.CachePerformance cache = default; N.Require(N.Op.CachePerf, fullToken, &cache);
            N.CacheDetails detail = default; N.Require(N.Op.CacheDetail, fullToken, &detail);
            string submissionState = FormattableString.Invariant($"submit_requested={(deferredSubmission ? "deferred" : "immediate")} submit_immediate_available={submit.ImmediateAvailable} submit_immediate_segments={submit.ImmediateSegments} submit_deferred_segments={submit.DeferredSegments} submit_state_restores={submit.StateRestores} submit_state_us_total={submit.StateUs:F3} submit_record_us_total={submit.RecordUs:F3} order_descriptor_bytes={submit.OrderBytes} order_gpu_dispatches={submit.OrderDispatches} cb_ring_available={submit.RingAvailable} cb_ring_maps={submit.RingMaps} cb_ring_bytes={submit.RingBytes} cb_ring_bindings={submit.RingBindings} ");
            submissionState += $"resource_skip_grab={resourceConfig.SkipGrab} resource_pretransform={resourceConfig.Pretransform} resource_stable={resourceConfig.StableUploads} resource_trim={resourceConfig.TrimBindings} resource_discard_maps={resource.DiscardMaps} resource_nooverwrite_maps={resource.NoOverwriteMaps} resource_default_updates={resource.DefaultUpdates} resource_default_bytes={resource.DefaultBytes} resource_grab_skips={resource.GrabSkips} resource_srv_unbinds={resource.SrvUnbinds} constant_candidates={resource.ConstantCandidates} constant_visible={resource.ConstantVisible} cb_ring_rotations={resource.RingRotations} retired_draws={resource.RetiredDraws} ";
            submissionState += FormattableString.Invariant($"grab_groups={cache.GrabGroups} grab_merged={cache.GrabMerged} grab_rect_tests={cache.RectTests} grab_rect_mismatches={cache.RectMismatches} grab_rect_fallbacks={cache.RectFallbacks} grab_rect_copies={cache.RectCopies} cache_requested={commandCache} cache_indirect_batches={indirectCacheBatches} cache_available={cache.Available} cache_segments={cache.Segments} cache_replays={cache.Replays} cache_records={cache.Records} cache_plan_builds={cache.PlanBuilds} cache_constant_pages={cache.ConstantPages} cache_constant_bytes={cache.ConstantBytes} cache_constant_evaluations={cache.ConstantEvaluations} cache_cull_dispatches={cache.CullDispatches} cache_indirect_draws={cache.IndirectDraws} cache_fallback_segments={cache.FallbackSegments} rt_cache_apply_us={cache.ApplyUs:F3} rt_cache_pose_us={cache.PoseUs:F3} rt_cache_constants_us={cache.ConstantsUs:F3} rt_cache_cull_us={cache.CullUs:F3} rt_cache_plan_us={cache.PlanUs:F3} rt_cache_record_us={cache.RecordUs:F3} rt_cache_execute_us={cache.ExecuteUs:F3} ");
            submissionState += $"cache_shared_bindings={detail.SharedBindings} cache_private_bindings={detail.PrivateBindings} cache_frame_evaluations={detail.FrameEvaluations} cache_draw_evaluations={detail.DrawEvaluations} cache_frame_bytes={detail.FrameBytes} cache_draw_bytes={detail.DrawBytes} cache_same_lists={detail.SameLists} cache_same_runs={detail.SameRuns} cache_order_plans={detail.OrderPlans} cache_batch_plans={detail.BatchPlans} cache_direct_draws={detail.DirectDraws} cache_detail_indirect_draws={detail.IndirectDraws} cache_cull_items={detail.CullItems} cache_material_detaches={detail.MaterialDetaches} distance_order_skips={distanceOrderSkips} ";
            for (int i = 0; i < cacheVariableNames.Length; i++) submissionState += $"cache_var_{cacheVariableNames[i]}_evaluations={detail.VariableEvaluations[i]} cache_var_{cacheVariableNames[i]}_changes={detail.VariableChanges[i]} ";
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
