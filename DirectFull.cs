#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StutterFix
{
    // M4a ceiling and M4c original-shader rendering share one reversible mask lease.
    internal sealed class DirectFullLifetime : MonoBehaviour
    {
        void Update() { DirectFull.CheckLease(); }
        void OnDisable() { DirectFull.LifetimeEnded(this); }
        void OnDestroy() { DirectFull.LifetimeEnded(this); }
    }
    internal sealed class DirectFullPreparation : MonoBehaviour
    {
        void Update() => DirectFull.PrepareTick();
        void OnDisable() => DirectFull.PreparationEnded(this);
        void OnDestroy() => DirectFull.PreparationEnded(this);
    }

    internal static partial class DirectFull
    {
        const string Owner = "StutterFix.DirectFull";
        static bool commandCache = true, indirectCacheBatches, directCacheSingles;
        // Names verified against the installed game's TagManager, not sorting layers.
        static readonly string[] MapLayers = { "Foreground", "Planet", "Floor", "BG", "BG Static", "TopGlow", "FloorTemp", "textDeco" };
        static readonly DirectFullMaskLease lease = new DirectFullMaskLease();
        static Camera camera;
        static GameObject lifetime;
        static DirectFullLifetime driver;
        static Harmony hooks;
        static GameObject preparationObject;
        static DirectFullPreparation preparationDriver;
        static Camera preparationCamera;
        internal static void PrepareTick()
        {
            if (!preparing) return;
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                if (!preparationCamera || !preparationCamera.isActiveAndEnabled || preparationCamera.cullingMask != prepareOriginal)
                    throw new InvalidOperationException("preparation camera changed");
                while (preparing)
                {
                    BeginPreparationWork(); bool more;
                    try { more = preparation.MoveNext(); } finally { EndPreparationWork(); }
                    if (!more)
                    {
                        if (preparing) throw new InvalidOperationException("preparation ended without publishing");
                        break;
                    }
                    if ((System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency >= 4) break;
                }
                if (!preparing)
                {
                    (preparation as IDisposable)?.Dispose(); preparation = null;
                    var old = preparationObject; preparationObject = null; preparationDriver = null;
                    if (old) UnityEngine.Object.Destroy(old);
                }
            }
            catch (Exception e) { Log("preparation failed: " + e); Stop("preparation failed; restored"); }
            finally
            {
                long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - start;
                prepareSlices++; prepareWorkTicks += elapsed; prepareMaxTicks = Math.Max(prepareMaxTicks, elapsed);
            }
        }
        internal static void PreparationEnded(DirectFullPreparation source)
        {
            if (ReferenceEquals(source, preparationDriver)) Stop("preparation lifetime ended");
        }
        static int siteStartFrame;
        static bool subscribed;
        static int startedFrame;
        static double startedAt;
        static long cameraFrames;
        internal static bool Active => lease.Active;
        static void Log(string message) => Main.Entry.Logger.Log("[drfull] " + message);

        internal static string Command(string arg)
        {
            if (arg.Trim().StartsWith("drawgpu", StringComparison.OrdinalIgnoreCase)) return DrawGpuCommand(arg.Trim().Substring(7).Trim());
            if (arg.Trim().StartsWith("resources ", StringComparison.OrdinalIgnoreCase)) return ResourceCommand(arg.Trim().Substring(10).Trim());
            if (arg.Trim().StartsWith("cache ", StringComparison.OrdinalIgnoreCase))
            {
                if (Active || preparing) throw new InvalidOperationException("cache mode requires off");
                string mode = arg.Trim().Substring(6).Trim().ToLowerInvariant();
                if (mode != "on" && mode != "off" && mode != "indirect" && mode != "direct") throw new ArgumentException("cache on/off/indirect/direct");
                commandCache = mode != "off"; indirectCacheBatches = mode == "indirect"; directCacheSingles = mode == "direct"; return "cache=" + commandCache + " indirect_batches=" + indirectCacheBatches + " direct_singles=" + directCacheSingles;
            }
            if (arg.Trim().StartsWith("probe ", StringComparison.OrdinalIgnoreCase)) return ProbeCommand(arg.Trim().Substring(6));
            if (arg.Trim().StartsWith("pressure mip ", StringComparison.OrdinalIgnoreCase)) return PressureMip(int.Parse(arg.Trim().Substring(13), System.Globalization.CultureInfo.InvariantCulture));
            switch (arg.Trim().ToLowerInvariant())
            {
                case "ceil": case "ceil on": StartCeiling(); return State();
                case "off": case "ceil off": Stop("command off"); return State();
                case "state": return State();
                case "census": return Census();
                case "colors": return ColorAudit();
                case "why": return WhyReplan();
                case "residency": return ResidencyState();
                case "floors":
                {
                    int total = 0, enabled = 0, visibleUnity = 0;
                    foreach (var f in Resources.FindObjectsOfTypeAll<scrFloor>()) { if (!f || !f.gameObject.scene.IsValid() || !f.gameObject.activeInHierarchy) continue; total++; if (f.enabled) enabled++; var fr = f.GetComponent<Renderer>(); if (fr && fr.isVisible) visibleUnity++; }
                    string r = "floors active=" + total + " enabled=" + enabled + " unity_visible=" + visibleUnity + " rendering=" + Rendering; Log(r); return r;
                }
                case "stale": return StaleAudit();
                case "sites reset": DirectSequenceChanges.ResetSiteCounts(); siteStartFrame = Time.frameCount; return "sites reset frame=" + siteStartFrame;
                case "sites": { long f = Time.frameCount - siteStartFrame; string top = DirectSequenceChanges.SiteTop(60, f); foreach (var line in top.Split((char)10)) if (line.Length > 0) Log("site " + line); return "sites frames=" + f; }
                case "constcheck": return ConstCheck();
                case "poke constants": return Poke("constants");
                case "poke order": return Poke("order");
                case "poke refresh": return Poke("refresh");
                case "poke poses": return Poke("poses");
                case "poke textures": return Poke("textures");
                case "poke geometry": return Poke("geometry");
                case "poke rebind": return Poke("rebind");
                case "poke canvases": return Poke("canvases");
                case "poke canvasunits": return Poke("canvasunits");
                case "pressure restore": return PressureMip(-1);
                case "particles baked": case "particles explicit":
                    if (Active || preparing) throw new InvalidOperationException("particle route requires off");
                    bakedParticles = arg.Trim().Equals("particles baked", StringComparison.OrdinalIgnoreCase); return "particles=" + (bakedParticles ? "baked-diagnostic" : "explicit");
                case "submit immediate": case "submit deferred":
                    if (Active || preparing) throw new InvalidOperationException("submission mode requires off");
                    deferredSubmission = arg.Trim().Equals("submit deferred", StringComparison.OrdinalIgnoreCase); return "submit=" + (deferredSubmission ? "deferred" : "immediate");
                case "constants ring": case "constants legacy":
                    if (Active || preparing) throw new InvalidOperationException("constant mode requires off");
                    legacyConstants = arg.Trim().Equals("constants legacy", StringComparison.OrdinalIgnoreCase); return "constants=" + (legacyConstants ? "legacy" : "ring");
                case "assert clean": if (Resilience.SafeMode) throw new InvalidOperationException("drfull INVALID: safe=1"); return "safe=0 PASS";
                case "precision local": case "precision batch":
                    if (Active || preparing) throw new InvalidOperationException("precision diagnostic requires off");
                    localPrecision = arg.Trim().Equals("precision local", StringComparison.OrdinalIgnoreCase); return "precision_local=" + localPrecision;
                case "skip explicit": case "explicit on": case "explicit all": case "explicit forward":
                    if (Active || preparing) throw new InvalidOperationException("explicit diagnostic requires off");
                    if (arg.Trim().Equals("skip explicit", StringComparison.OrdinalIgnoreCase)) skipExplicit = true;
                    else if (arg.Trim().Equals("explicit on", StringComparison.OrdinalIgnoreCase)) skipExplicit = false;
                    else allExplicitPasses = arg.Trim().Equals("explicit all", StringComparison.OrdinalIgnoreCase);
                    return "skip_explicit=" + skipExplicit + " explicit_all_passes=" + allExplicitPasses;
                case "grab unity": case "grab native":
                    if (Active || preparing) throw new InvalidOperationException("drfull grab route requires off");
                    unityGrab = arg.Trim().Equals("grab unity", StringComparison.OrdinalIgnoreCase);
                    return "grab=" + (unityGrab ? "unity-diagnostic" : "native");
                case "plan": return Plan();
                case "assert ceil": CheckLease(); if (!Active || Rendering) throw new InvalidOperationException("drfull INVALID: ceiling lease not active"); return "assert ceil PASS";
                case "assert on": CheckLease(); if (!Active || !Rendering) throw new InvalidOperationException("drfull INVALID: full renderer not active"); AssertFull(); return "assert on PASS";
                case "assert cache": CheckLease(); if (!Active || !Rendering) throw new InvalidOperationException("drfull INVALID: cache renderer not active"); AssertFull(); AssertCache(); return "assert cache PASS";
                case "assert off": CheckLease(); if (Active || preparing) throw new InvalidOperationException("drfull INVALID: lease/preparation still active"); return "assert off PASS";
                case "warm": Warm(); return "warm_ms=" + warmMs.ToString("F3");
                case "cold": if (Active || preparing) throw new InvalidOperationException("drfull cold requires off"); Cold(); return "warm=False";
                case "on": StartFull(); return State();
                default: throw new ArgumentException("drfull on|ceil|off|state|census|plan|assert on|assert off");
            }
        }

        static int ResolveMapMask()
        {
            int mask = 0;
            foreach (string name in MapLayers)
            {
                int layer = LayerMask.NameToLayer(name);
                if (layer < 0) throw new InvalidOperationException("drfull: required layer missing: " + name);
                mask |= 1 << layer;
            }
            return mask;
        }

        static void StartCeiling()
        {
            if (Rendering || preparing) Stop("switch full to ceiling");
            if (Active) { CheckLease(); if (Active) return; }
            // Explicit control starts from Unity's camera path. Never silently change
            // the caller's renderer configuration to manufacture a ceiling result.
            if (DrProbe.Active || DirectDeco.Enabled || DirectDeco.Active || Main.Config.GpuDeco != 0 || Main.Config.RunLod)
                throw new InvalidOperationException("drfull ceil requires drx off, dr2 off, GpuDeco=0, RunLod=false");
            var main = GpuDecoScene.FindMain();
            if (!main.isActiveAndEnabled) throw new InvalidOperationException("drfull: main Camera disabled");
            int removed = ResolveMapMask();
            // Existing command buffers may redraw culled content (drx/dr2/other mods).
            // Do not strip anyone's buffer or call that a valid camera-only ceiling.
            foreach (UnityEngine.Rendering.CameraEvent evt in Enum.GetValues(typeof(UnityEngine.Rendering.CameraEvent)))
                foreach (var e in main.GetCommandBuffers(evt))
                    if (e.name.StartsWith("DR", StringComparison.Ordinal) || e.name.StartsWith("StutterFix.Direct", StringComparison.Ordinal))
                        throw new InvalidOperationException("drfull: competing command buffer " + e.name);
            try
            {
                BeginLease(main, removed, "StutterFix.DirectFull.Ceiling");
                Log("CEILING ON draws=0 originalMask=" + lease.Original + " removedMask=" + (lease.Original & lease.Removed) +
                    " currentMask=" + main.cullingMask + " layers=" + LayerNames(lease.Original & lease.Removed) +
                    " target=" + (main.targetTexture ? main.targetTexture.width + "x" + main.targetTexture.height : "screen") +
                    " diagnostic=blank-map-render-only scripts=unchanged physics=unchanged");
            }
            catch { Stop("activation failed"); throw; }
        }

        static void BeginLease(Camera main, int removed, string objectName)
        {
                camera = main; lease.Begin(main.cullingMask, removed);
                if (hooks == null) hooks = new Harmony(Owner);
                foreach (var pair in new[] {
                    (typeof(scrController), "Restart"), (typeof(scrController), "ResetCustomLevel"), (typeof(scrController), "QuitToMainMenu"),
                    (typeof(scnEditor), "SwitchToEditMode"), (typeof(scnEditor), "QuitToMenu"), (typeof(scnEditor), "Play"), (typeof(scnGame), "ResetScene") })
                {
                    var method = AccessTools.Method(pair.Item1, pair.Item2);
                    if (method != null) hooks.Patch(method, prefix: new HarmonyMethod(typeof(DirectFull), nameof(Ending)) { priority = Priority.First });
                }
                var prep = AccessTools.Method(typeof(scnGame), "PrepVfx", new[] { typeof(List<scrFloor>), typeof(int), typeof(List<ADOFAI.LevelEvent>), typeof(bool) });
                if (prep == null) throw new MissingMethodException("PrepVfx");
                hooks.Patch(prep, prefix: new HarmonyMethod(typeof(DirectFull), nameof(Ending)) { priority = Priority.First });
                SceneManager.activeSceneChanged += SceneChanged;
                SceneManager.sceneUnloaded += SceneUnloaded;
                Camera.onPreCull += PreCull;
                subscribed = true;
                lifetime = new GameObject(objectName);
                UnityEngine.Object.DontDestroyOnLoad(lifetime);
                driver = lifetime.AddComponent<DirectFullLifetime>();
                main.cullingMask = lease.Expected;
                startedFrame = Time.frameCount; startedAt = Time.realtimeSinceStartupAsDouble; cameraFrames = 0;
        }

        static string LayerNames(int mask)
        {
            var names = new List<string>();
            for (int i = 0; i < 32; i++) if ((mask & (1 << i)) != 0) names.Add(i + ":" + LayerMask.LayerToName(i));
            return string.Join(",", names);
        }
        static void Ending(System.Reflection.MethodBase __originalMethod) => Stop(__originalMethod.Name);
        static void SceneChanged(Scene a, Scene b) => Stop("active scene changed");
        static void SceneUnloaded(Scene scene) { if (Active) Stop("scene unloaded"); }
        static void PreCull(Camera c) { if (Active && c == camera) { CheckLease(); if (Active) { cameraFrames++; if (Rendering) FullPreCull(); } } }
        internal static void LifetimeEnded(DirectFullLifetime source)
        {
            // Destroy is deferred. A previous lease's callback must not stop a
            // new ceiling started in the same frame after off.
            if (ReferenceEquals(source, driver)) Stop("diagnostic lifetime ended");
        }
        internal static void CheckLease()
        {
            if (!Active) return;
            if (!camera || !camera.isActiveAndEnabled) { Stop("camera destroyed/disabled"); return; }
            if (camera.cullingMask != lease.Expected) { Stop("camera mask externally changed; sample invalid"); return; }
            if (DrProbe.Active || DirectDeco.Enabled || DirectDeco.Active || Main.Config.GpuDeco != 0 || Main.Config.RunLod)
                Stop("competing renderer enabled; sample invalid");
        }

        internal static void Stop()
        {
            try { RestoreProbe(); probeRenderers.Clear(); Stop("shutdown/batch end"); }
            finally { if (!Active && !preparing) PressureMip(-1); }
        }
        internal static void Stop(string reason)
        {
            preparing = false; (preparation as IDisposable)?.Dispose(); preparation = null;
            var oldPrepare = preparationObject; preparationObject = null; preparationDriver = null; preparationCamera = null;
            if (oldPrepare) UnityEngine.Object.Destroy(oldPrepare);
            bool wasActive = Active;
            bool wasFull = Rendering;
            int original = lease.Original;
            bool cameraAlive = wasActive && camera;
            int restoredMask = 0;
            // A destroyed camera uses Unity's fake-null semantics. Never write to it.
            try { RestoreVisibilityCulling(); } catch (Exception e) { Log("culling restore failed: " + e.Message); }
            try { StopVisibility(); } catch (Exception e) { Log("visibility stop failed: " + e.Message); }
            try { ReleaseFull(); }
            finally
            {
                if (cameraAlive) { camera.cullingMask = original; restoredMask = camera.cullingMask; }
                lease.Clear(); camera = null;
                if (subscribed)
                {
                    Camera.onPreCull -= PreCull;
                    SceneManager.activeSceneChanged -= SceneChanged;
                    SceneManager.sceneUnloaded -= SceneUnloaded;
                    subscribed = false;
                }
                var oldHooks = hooks; hooks = null;
                try { oldHooks?.UnpatchAll(Owner); }
                finally
                {
                    var oldLifetime = lifetime; lifetime = null; driver = null;
                    if (oldLifetime) UnityEngine.Object.Destroy(oldLifetime);
                }
                if (wasActive) Log((wasFull ? "FULL" : "CEILING") + " OFF originalMask=" + original + " cameraAlive=" + cameraAlive + " restoredMask=" + restoredMask + " reason=" + reason);
            }
        }

        static string State()
        {
            CheckLease();
            if (preparing) return FormattableString.Invariant($"mode=preparing active=False stage={prepareStage} sources={sources.Count} dormant={dormant.Count} slices={prepareSlices} slice_max_ms={prepareMaxTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency:F3} work_ms={prepareWorkTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency:F3}");
            if (!Active) return "mode=off active=False originalShaderRendering=available";
            if (Rendering) return FullState();
            double seconds = Time.realtimeSinceStartupAsDouble - startedAt;
            return FormattableString.Invariant($"mode=M4a-ceiling active=True draws=0 originalMask={lease.Original} removedMask={lease.Original & lease.Removed} currentMask={camera.cullingMask} frames={Time.frameCount - startedFrame} cameraFrames={cameraFrames} elapsed_s={seconds:F3} mean_fps={(seconds > 0 ? (Time.frameCount - startedFrame) / seconds : 0):F3} scope=since-ceil-includes-warmup-use-cpuwindow-for-bench");
        }
    }
}
#endif
