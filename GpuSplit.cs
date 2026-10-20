#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using UnityEngine;

namespace StutterFix
{
    // Explicit picture diagnostic. Nothing registered or allocated per frame while off.
    internal sealed class GpuSplit : MonoBehaviour
    {
        static GpuSplit instance;
        static GpuSplit censusRequest;
        internal static bool Active => instance != null;
        internal static bool Busy => Active || censusRequest || GpuSplitPixelDiff.Busy;
        readonly GpuSplitLedger<Renderer> ledger = new GpuSplitLedger<Renderer>(r => r, r => r.forceRenderingOff, (r, value) => r.forceRenderingOff = value);
        GpuSplitPolicy.Selection selection;
        string selector;
        int frame = -1;
        long overwrites;
        Camera mainCamera;
        readonly List<SpriteRenderer> images = new List<SpriteRenderer>();
        readonly Plane[] livePlanes = new Plane[6];
        long liveTicks, liveFrames;
        long currentFrameTicks;
        int liveSelected;
        int liveMinSelected = int.MaxValue, liveMaxSelected;
        double liveWorstMs;
        bool censusPending;

        internal static void Command(string arg)
        {
            arg = arg.Trim();
            if (arg.StartsWith("pixeldiff ", StringComparison.Ordinal))
            {
                if (Busy || GpuHide.Busy || GpuCensus.Busy) throw new InvalidOperationException("pixeldiff requires gpusplit/gpuhide/census off");
                var parsedDiff = GpuSplitPolicy.ParseSelection(arg.Substring(10).Trim());
                if (!parsedDiff.Live) throw new ArgumentException("pixeldiff requires live filter");
                GpuSplitPixelDiff.Begin(parsedDiff, arg.Substring(10).Trim()); return;
            }
            if (arg == "census")
            {
                if (GpuSplitPixelDiff.Busy || GpuHide.Busy || GpuCensus.Busy) throw new InvalidOperationException("pixeldiff/gpuhide/census active");
                if (Active) { instance.censusPending = true; return; }
                if (censusRequest) return;
                var census = new GameObject("StutterFix.GpuSplit.Census").AddComponent<GpuSplit>();
                census.selection = GpuSplitPolicy.ParseSelection("deco-image"); census.selector = "none";
                censusRequest = census; Camera.onPreCull += census.CensusOnce;
                return;
            }
            if (arg == "off" || arg == "hide none") { Stop(); Log("hide=none restored=1"); return; }
            if (arg == "reset")
            {
                if (!Active) return;
                instance.liveTicks = instance.liveFrames = instance.currentFrameTicks = 0;
                instance.liveWorstMs = 0; instance.liveMinSelected = int.MaxValue; instance.liveMaxSelected = 0;
                return;
            }
            if (arg == "state")
            {
                Log("state=" + (Active ? instance.selector : "none") + " selected=" + (Active ? instance.selection.Live ? instance.liveSelected : instance.ledger.Count : 0) + " external_unhide_writes=" + (Active ? instance.overwrites : 0));
                if (Active) Log(FormattableString.Invariant($"live_frames={instance.liveFrames} traversal_mean_ms={(instance.liveFrames == 0 ? 0 : instance.liveTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency / instance.liveFrames):F6} traversal_worst_ms={instance.liveWorstMs:F6} selected_min={(instance.liveFrames == 0 ? 0 : instance.liveMinSelected)} selected_max={instance.liveMaxSelected} cached_images={instance.images.Count}; includes classification, hide and post-render restore; exclude census/pixeldiff frames"));
                return;
            }
            if (arg == "refresh")
            {
                if (!Active) throw new InvalidOperationException("gpusplit refresh: no hidden group");
                try { instance.Scan(); } catch { Stop(); throw; }
                return;
            }
            if (!arg.StartsWith("hide ", StringComparison.Ordinal)) throw new ArgumentException("gpusplit hide <group>|refresh|state|off");
            string target = arg.Substring(5).Trim();
            var parsed = GpuSplitPolicy.ParseSelection(target); // Invalid input must not change the current session.
            if (GpuHide.Busy || GpuCensus.Busy || GpuSplitPixelDiff.Busy) throw new InvalidOperationException("gpuhide/census/pixeldiff active");
            if (Active && instance.selector == target)
            {
                try { instance.Scan(); } catch { Stop(); throw; }
                return;
            }
            Stop();
            var go = new GameObject("StutterFix.GpuSplit"); DontDestroyOnLoad(go);
            var p = go.AddComponent<GpuSplit>(); instance = p; p.selection = parsed; p.selector = target;
            try { p.Scan(!parsed.Live); Camera.onPreCull += Enforce; Camera.onPostRender += RestoreLive; }
            catch { Stop(); throw; }
        }

        void Scan(bool hide = true)
        {
            if (selection.Live) hide = false;
            Camera main = FindMain(); mainCamera = main;
            images.Clear();
            // TileCull's explicit renderer/topGlow ownership also covers detached glow renderers.
            var floors = new HashSet<Renderer>();
            foreach (var f in Resources.FindObjectsOfTypeAll<scrFloor>())
            {
                if (!f.gameObject.scene.IsValid()) continue;
                if (f.floorRenderer != null && f.floorRenderer.renderer) floors.Add(f.floorRenderer.renderer);
                if (f.topGlow) floors.Add(f.topGlow);
            }
            var planets = new HashSet<Renderer>();
            foreach (var p in Resources.FindObjectsOfTypeAll<PlanetRenderer>())
            {
                if (!p.gameObject.scene.IsValid()) continue;
                AddPlanet(p.tailParticles, planets); AddPlanet(p.tailParticlesCoop, planets);
                AddPlanet(p.coreParticles, planets); AddPlanet(p.sparks, planets); AddPlanet(p.deathExplosion, planets);
            }
            int addedBefore = ledger.Count;
            var counts = new int[6];
            var planes = GeometryUtility.CalculateFrustumPlanes(main.cullingMatrix);
            var projectionPlanes = GeometryUtility.CalculateFrustumPlanes(main.projectionMatrix * main.worldToCameraMatrix);
            var legacyPlanes = GeometryUtility.CalculateFrustumPlanes(main);
            Log("camera_geometry frame=" + Time.frameCount + " callback=" + (Camera.current == main) + " pos=" + main.transform.position.ToString("G9")
                + " ortho=" + main.orthographic + " size=" + main.orthographicSize + " aspect=" + main.aspect + " near=" + main.nearClipPlane + " far=" + main.farClipPlane
                + " rt=" + (main.targetTexture ? main.targetTexture.width + "x" + main.targetTexture.height : "backbuffer")
                + " view=" + main.worldToCameraMatrix.ToString("G9") + " projection=" + main.projectionMatrix.ToString("G9") + " culling=" + main.cullingMatrix.ToString("G9"));
            for (int i = 0; i < planes.Length; i++) Log("frustum_plane=" + i + " normal=" + planes[i].normal.ToString("G9") + " distance=" + planes[i].distance);
            int samples = 0, unforcedSamples = 0;
            int unforcedOnscreen = 0, boundsRecovered = 0, projectionDisagree = 0, legacyDisagree = 0;
            var imageCounts = new int[9]; var liveCounts = new int[9];
            double areaSum = 0, maxArea = 0; long vertices = 0, triangles = 0;
            var blends = new Dictionary<string, int>();
            int chosen = 0, chosenLive = 0, chosenAlreadyOff = 0;
            foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (!r || !r.gameObject.scene.IsValid()) continue;
                bool deco = r.GetComponentInParent<scrDecoration>(true) != null;
                var group = GpuSplitPolicy.Classify(true, (main.cullingMask & (1 << r.gameObject.layer)) != 0,
                    r.GetComponentInParent<Canvas>(true) != null, deco, r is SpriteRenderer,
                    r.GetComponentInParent<TMPro.TMP_Text>(true) != null || r.GetComponentInParent<TextMesh>(true) != null,
                    r is ParticleSystemRenderer, floors.Contains(r) || r.GetComponentInParent<scrFloor>(true) != null,
                    planets.Contains(r) || r.GetComponentInParent<scrPlanet>(true) != null || r.GetComponentInParent<PlanetRenderer>(true) != null);
                for (int i = 0; i < counts.Length; i++) if ((int)group == (1 << i)) counts[i]++;
                bool matches = true;
                if (group == GpuSplitGroup.Image)
                {
                    var sr = (SpriteRenderer)r;
                    images.Add(sr);
                    var bounds = ConservativeBounds(sr);
                    bool onscreen = GeometryUtility.TestPlanesAABB(planes, bounds);
                    bool nativeInside = GeometryUtility.TestPlanesAABB(planes, r.bounds);
                    if (!nativeInside && onscreen) boundsRecovered++;
                    if (onscreen != GeometryUtility.TestPlanesAABB(projectionPlanes, bounds)) projectionDisagree++;
                    if (nativeInside != GeometryUtility.TestPlanesAABB(legacyPlanes, r.bounds)) legacyDisagree++;
                    double area = AreaPercent(main, bounds);
                    string blend = Blend(sr);
                    bool transparent = r.enabled && sr.color.a <= 0;
                    bool live = r.enabled && r.gameObject.activeInHierarchy;
                    bool sample = samples < 5;
                    if (sample) samples++;
                    bool unforcedSample = live && !r.forceRenderingOff && unforcedSamples < 5;
                    if (unforcedSample) unforcedSamples++;
                    if (sample || unforcedSample)
                        Log("bounds_sample renderer=" + r.GetInstanceID() + " unforced_live=" + (live && !r.forceRenderingOff)
                            + " native_center=" + r.bounds.center.ToString("G9") + " native_extents=" + r.bounds.extents.ToString("G9")
                            + " local_center=" + r.localBounds.center.ToString("G9") + " local_extents=" + r.localBounds.extents.ToString("G9")
                            + " conservative_center=" + bounds.center.ToString("G9") + " conservative_extents=" + bounds.extents.ToString("G9")
                            + " native_inside=" + GeometryUtility.TestPlanesAABB(planes, r.bounds) + " conservative_inside=" + onscreen
                            + " projection_inside=" + GeometryUtility.TestPlanesAABB(projectionPlanes, bounds)
                            + " viewport=" + main.WorldToViewportPoint(bounds.center).ToString("G9") + " alpha=" + sr.color.a);
                    bool[] flags = { onscreen, !onscreen, area > selection.Percent, area <= selection.Percent,
                        blend == "additive", blend == "alpha", transparent, blend == "unknown", live && !r.forceRenderingOff };
                    for (int i = 0; i < flags.Length; i++) if (flags[i]) { imageCounts[i]++; if (live) liveCounts[i]++; }
                    if (live && onscreen && !r.forceRenderingOff)
                    {
                        unforcedOnscreen++;
                        areaSum += area; maxArea = Math.Max(maxArea, area);
                        if (sr.sprite) { vertices += sr.sprite.vertices.Length; triangles += sr.sprite.triangles.Length / 3; }
                    }
                    string key = BlendEvidence(sr.sharedMaterial);
                    blends.TryGetValue(key, out int n); blends[key] = n + 1;
                    matches = selection.Live ? selection.MatchesLive(live, r.forceRenderingOff, onscreen, sr.color.a, area) : selection.Matches(onscreen, area, blend, transparent);
                }
                if ((group & selection.Groups) != 0 && matches)
                {
                    chosen++; if (r.enabled && r.gameObject.activeInHierarchy) chosenLive++;
                    if (r.forceRenderingOff) chosenAlreadyOff++;
                    if (hide) ledger.Hide(r);
                }
            }
            Log("hide=" + selector + " selected=" + ledger.Count + " added=" + (ledger.Count - addedBefore)
                + " census_image_text_particle_tiles_planets_other=" + string.Join(",", counts)
                + " camera_id=" + main.GetInstanceID() + " mask=" + main.cullingMask + " external_unhide_writes=" + overwrites);
            Log(FormattableString.Invariant($"image_filters=onscreen,offscreen,big,small,additive,alpha,transparent,unknown,unforced_live threshold_percent={selection.Percent} all={string.Join(",", imageCounts)} enabled_active={string.Join(",", liveCounts)} matched_now={chosen} matched_enabled_active={chosenLive} matched_already_off={chosenAlreadyOff} membership={(selection.Live ? "main-preCull-dynamic-postRender-restore" : "snapshot-union-until-restore")}"));
            Log(FormattableString.Invariant($"image_live_onscreen_unforced count={unforcedOnscreen} projected_aabb_screen_percent_sum={areaSum:F3} max={maxArea:F3} sprite_vertices={vertices} sprite_triangles={triangles} bounds_recovered={boundsRecovered} culling_vs_projection_disagree={projectionDisagree} legacy_vs_culling_disagree={legacyDisagree}; geometry not actual draw counters; bounds area unclipped"));
            foreach (var b in blends) Log("blend_evidence count=" + b.Value + " " + b.Key);
        }
        internal static Camera FindMain()
        {
            Camera main = null;
            foreach (var c in Resources.FindObjectsOfTypeAll<Camera>())
                if (c.gameObject.scene.IsValid() && c.name == "Camera" && c.gameObject.activeInHierarchy)
                {
                    if (main) throw new InvalidOperationException("gpusplit: ambiguous main Camera");
                    main = c;
                }
            if (!main) throw new InvalidOperationException("gpusplit: main Camera missing");
            return main;
        }
        void CensusOnce(Camera camera)
        {
            if (!camera || camera.name != "Camera") return;
            Camera.onPreCull -= CensusOnce;
            try { Scan(false); }
            catch (Exception e) { Log("census_failed=" + e.Message); }
            finally { if (censusRequest == this) censusRequest = null; Destroy(gameObject); }
        }
        internal static bool IsLiveImage(SpriteRenderer r, Camera camera) => r && r.gameObject.scene.IsValid()
            && (camera.cullingMask & (1 << r.gameObject.layer)) != 0 && r.GetComponentInParent<scrDecoration>(true)
            && !r.GetComponentInParent<Canvas>(true) && !r.GetComponentInParent<TMPro.TMP_Text>(true)
            && !r.GetComponentInParent<TextMesh>(true);

        // Stale native SpriteRenderer bounds must not turn an on-screen image into
        // an invisible candidate. Union with transformed local bounds (also handles
        // sliced/tiled size); do not shrink or overwrite Unity's own culling bound.
        internal static Bounds ConservativeBounds(SpriteRenderer r)
        {
            var result = r.bounds;
            var b = r.localBounds; var m = r.localToWorldMatrix;
            var e = b.extents;
            var x = m.MultiplyVector(new Vector3(e.x, 0, 0));
            var y = m.MultiplyVector(new Vector3(0, e.y, 0));
            var z = m.MultiplyVector(new Vector3(0, 0, e.z));
            result.Encapsulate(new Bounds(m.MultiplyPoint3x4(b.center), new Vector3(
                Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z)) * 2));
            return result;
        }
        internal static double AreaPercent(Camera camera, Bounds b)
        {
            var area = new GpuSplitPolicy.ProjectionArea();
            for (int i = 0; i < 8; i++)
            {
                var p = camera.WorldToViewportPoint(b.center + Vector3.Scale(b.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                area.Include(p.x, p.y, p.z, camera.nearClipPlane);
            }
            return area.Percent;
        }
        static string Blend(SpriteRenderer r)
        {
            var m = r.sharedMaterial;
            if (!m || !m.shader) return "unknown";
            // Explicit fixed-function factors only; do not infer a shader's pass from its name/queue.
            if (m.HasProperty("_SrcBlend") && m.HasProperty("_DstBlend"))
            {
                if (m.HasProperty("_BlendOp") && m.GetFloat("_BlendOp") != 0) return "unknown";
                float src = m.GetFloat("_SrcBlend"), dst = m.GetFloat("_DstBlend");
                if ((src == 1 || src == 5) && dst == 1) return "additive";
                if ((src == 1 || src == 5) && dst == 10) return "alpha";
                return "unknown";
            }
            // BlendModes owns the effective equation, including grab-pass variants.
            var effect = r.GetComponent<BlendModes.BlendModeEffect>();
            if (effect && effect.enabled)
                return effect.BlendMode == BlendModes.BlendMode.LinearDodge ? "additive" : "unknown";
            // Serialized Sprites/Tile pass: One/OneMinusSrcAlpha, Add (all four
            // PIXELSNAP_ON / USE_POINT_SAMPLING variants); evidence in handoff.
            foreach (string keyword in m.shaderKeywords)
                if (keyword != "PIXELSNAP_ON" && keyword != "USE_POINT_SAMPLING") return "unknown";
            return m.shader.name == "Sprites/Tile" ? "alpha" : "unknown";
        }
        static string BlendEvidence(Material m)
        {
            if (!m || !m.shader) return "missing";
            string value = "shader=" + m.shader.name + " keywords=" + string.Join(",", m.shaderKeywords);
            foreach (string p in new[] { "_BlendOp", "_SrcBlend", "_DstBlend" })
                value += " " + p + "=" + (m.HasProperty(p) ? m.GetFloat(p).ToString(System.Globalization.CultureInfo.InvariantCulture) : "absent");
            return value;
        }
        static void AddPlanet(Component owner, HashSet<Renderer> renderers)
        {
            if (!owner) return;
            foreach (var r in owner.GetComponentsInChildren<Renderer>(true)) renderers.Add(r);
        }
        static void Enforce(Camera camera)
        {
            var p = instance;
            if (!p || camera != p.mainCamera) return;
            if (p.censusPending) { p.censusPending = false; p.Scan(false); }
            if (p.selection.Live)
            {
                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                p.currentFrameTicks = 0;
                try
                {
                    p.ledger.Restore();
                    GeometryUtility.CalculateFrustumPlanes(camera.cullingMatrix, p.livePlanes);
                    int selected = 0;
                    foreach (var r in p.images)
                    {
                        if (!r || !r.enabled || !r.gameObject.activeInHierarchy || r.forceRenderingOff ||
                            (camera.cullingMask & (1 << r.gameObject.layer)) == 0) continue;
                        var b = ConservativeBounds(r);
                        if (!p.selection.MatchesLive(true, false, GeometryUtility.TestPlanesAABB(p.livePlanes, b), r.color.a, AreaPercent(camera, b))) continue;
                        p.ledger.Hide(r); selected++;
                    }
                    p.liveSelected = selected; p.liveFrames++;
                    p.liveMinSelected = Math.Min(p.liveMinSelected, selected); p.liveMaxSelected = Math.Max(p.liveMaxSelected, selected);
                }
                catch (Exception ex) { Log("live_failed=" + ex.Message); Stop(); }
                finally { p.AddTicks(start); }
                return;
            }
            if (p.frame == Time.frameCount) return;
            p.frame = Time.frameCount;
            p.overwrites += p.ledger.Enforce(); // List iteration + delegates only, no per-frame allocation/scan.
        }
        void AddTicks(long start)
        {
            long ticks = System.Diagnostics.Stopwatch.GetTimestamp() - start;
            liveTicks += ticks;
            currentFrameTicks += ticks;
            liveWorstMs = Math.Max(liveWorstMs, currentFrameTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
        }
        static void RestoreLive(Camera camera)
        {
            var p = instance;
            if (!p || !p.selection.Live || camera != p.mainCamera) return;
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            try { p.ledger.Restore(); } finally { p.AddTicks(start); }
        }
        // Fallback if rendering aborts before onPostRender.
        void LateUpdate()
        {
            if (selection != null && selection.Live) ledger.Restore();
            if (instance == this && !mainCamera) Stop();
        }
        internal static void Log(string message) => Main.Entry.Logger.Log("[gpusplit] " + message);
        internal static void Stop()
        {
            GpuSplitPixelDiff.Stop();
            if (censusRequest)
            {
                Camera.onPreCull -= censusRequest.CensusOnce;
                Destroy(censusRequest.gameObject); censusRequest = null;
            }
            Camera.onPreCull -= Enforce;
            Camera.onPostRender -= RestoreLive;
            var p = instance; instance = null;
            if (!p) return;
            p.ledger.Restore();
            Log("restored=" + p.selector + " external_unhide_writes=" + p.overwrites);
            Destroy(p.gameObject);
        }
        void OnDisable() { Camera.onPreCull -= CensusOnce; if (instance == this) Stop(); else ledger.Restore(); }
        void OnDestroy() { Camera.onPreCull -= CensusOnce; if (instance == this) Stop(); else ledger.Restore(); }
    }
}
#endif
