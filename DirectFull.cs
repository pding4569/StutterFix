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

    internal static partial class DirectFull
    {
        const string Owner = "StutterFix.DirectFull";
        // Names verified against the installed game's TagManager, not sorting layers.
        static readonly string[] MapLayers = { "Foreground", "Planet", "Floor", "BG", "BG Static", "TopGlow", "FloorTemp", "textDeco" };
        static readonly DirectFullMaskLease lease = new DirectFullMaskLease();
        static Camera camera;
        static GameObject lifetime;
        static DirectFullLifetime driver;
        static Harmony hooks;
        static bool subscribed;
        static int startedFrame;
        static double startedAt;
        static long cameraFrames;
        internal static bool Active => lease.Active;
        static void Log(string message) => Main.Entry.Logger.Log("[drfull] " + message);

        internal static string Command(string arg)
        {
            switch (arg.Trim().ToLowerInvariant())
            {
                case "ceil": case "ceil on": StartCeiling(); return State();
                case "off": case "ceil off": Stop("command off"); return State();
                case "state": return State();
                case "census": return Census();
                case "plan": return Plan();
                case "assert ceil": CheckLease(); if (!Active || Rendering) throw new InvalidOperationException("drfull INVALID: ceiling lease not active"); return "assert ceil PASS";
                case "assert on": CheckLease(); if (!Active || !Rendering) throw new InvalidOperationException("drfull INVALID: full renderer not active"); AssertFull(); return "assert on PASS";
                case "assert off": CheckLease(); if (Active) throw new InvalidOperationException("drfull INVALID: ceiling lease still active"); return "assert off PASS";
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
            if (Rendering) Stop("switch full to ceiling");
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
                hooks = new Harmony(Owner);
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

        internal static void Stop() => Stop("shutdown/batch end");
        internal static void Stop(string reason)
        {
            bool wasActive = Active;
            bool wasFull = Rendering;
            int original = lease.Original;
            bool cameraAlive = wasActive && camera;
            int restoredMask = 0;
            // A destroyed camera uses Unity's fake-null semantics. Never write to it.
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
            if (!Active) return "mode=off active=False originalShaderRendering=available";
            if (Rendering) return FullState();
            double seconds = Time.realtimeSinceStartupAsDouble - startedAt;
            return FormattableString.Invariant($"mode=M4a-ceiling active=True draws=0 originalMask={lease.Original} removedMask={lease.Original & lease.Removed} currentMask={camera.cullingMask} frames={Time.frameCount - startedFrame} cameraFrames={cameraFrames} elapsed_s={seconds:F3} mean_fps={(seconds > 0 ? (Time.frameCount - startedFrame) / seconds : 0):F3} scope=since-ceil-includes-warmup-use-cpuwindow-for-bench");
        }
    }
}
#endif
