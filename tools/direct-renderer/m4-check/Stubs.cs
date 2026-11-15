// Managed lifecycle fixture, not a Unity render/parity test. The implementation
// under test is the production DirectFull.cs, not a second lease controller.
using System;
using System.Collections.Generic;
using System.Reflection;

namespace UnityEngine
{
    public class Object
    {
        public bool Dead;
        public static implicit operator bool(Object o) => o != null && !o.Dead;
        public static void Destroy(Object o) { o.Dead = true; }
        public static void DontDestroyOnLoad(Object o) { }
    }
    public class MonoBehaviour : Object { }
    public class GameObject : Object
    {
        public static Object LastComponent;
        public GameObject(string name) { }
        public T AddComponent<T>() where T : Object, new() { var c = new T(); LastComponent = c; return c; }
    }
    public class RenderTexture : Object { public int width = 3440, height = 1440; }
    public class Camera : Object
    {
        public bool isActiveAndEnabled = true;
        public int cullingMask;
        public RenderTexture targetTexture = new RenderTexture();
        public Rendering.CommandBuffer[] Buffers = Array.Empty<Rendering.CommandBuffer>();
        public Rendering.CommandBuffer[] GetCommandBuffers(Rendering.CameraEvent e) => Buffers;
        public static event Action<Camera> onPreCull;
        public static void Cull(Camera c) => onPreCull?.Invoke(c);
        public static int Subscribers => onPreCull?.GetInvocationList().Length ?? 0;
    }
    public static class Time { public static int frameCount; public static double realtimeSinceStartupAsDouble; }
    public static class LayerMask
    {
        public static readonly Dictionary<string, int> Names = new Dictionary<string, int> {
            {"Foreground",7}, {"Planet",8}, {"Floor",9}, {"BG",10}, {"BG Static",11}, {"TopGlow",14}, {"FloorTemp",15}, {"textDeco",18}
        };
        public static int NameToLayer(string n) => Names.TryGetValue(n, out int i) ? i : -1;
        public static string LayerToName(int i) { foreach (var p in Names) if (p.Value == i) return p.Key; return ""; }
    }
}
namespace UnityEngine.Rendering
{
    public enum CameraEvent { BeforeForwardAlpha, AfterForwardAlpha }
    public class CommandBuffer { public string name; }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene { }
    public static class SceneManager
    {
        public static event Action<Scene, Scene> activeSceneChanged;
        public static event Action<Scene> sceneUnloaded;
        public static void Changed() => activeSceneChanged?.Invoke(default, default);
        public static void Unloaded() => sceneUnloaded?.Invoke(default);
        public static int Subscribers => (activeSceneChanged?.GetInvocationList().Length ?? 0) + (sceneUnloaded?.GetInvocationList().Length ?? 0);
    }
}
namespace HarmonyLib
{
    public static class Priority { public const int First = 800; }
    public class HarmonyMethod
    {
        internal MethodInfo Method;
        public int priority;
        public HarmonyMethod(Type t, string method) { Method = t.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic); }
    }
    public static class AccessTools
    {
        public static MethodInfo Method(Type t, string n, Type[] args = null) => args == null ? t.GetMethod(n) : t.GetMethod(n, args);
    }
    public class Harmony
    {
        public static int FailPatchAt = -1, PatchCount;
        public static readonly Dictionary<MethodInfo, HarmonyMethod> Patches = new Dictionary<MethodInfo, HarmonyMethod>();
        public Harmony(string owner) { PatchCount = 0; }
        public void Patch(MethodInfo m, HarmonyMethod prefix)
        {
            if (++PatchCount == FailPatchAt) throw new InvalidOperationException("injected patch failure");
            Patches.Add(m, prefix);
        }
        public void UnpatchAll(string owner) => Patches.Clear();
        public static void Invoke(Type t, string name) { var m = t.GetMethod(name); Patches[m].Method.Invoke(null, new object[] { m }); }
    }
}
namespace ADOFAI { public class LevelEvent { } }
public class scrFloor { }
public class scrController { public void Restart() {} public void ResetCustomLevel() {} public void QuitToMainMenu() {} }
public class scnEditor { public void SwitchToEditMode() {} public void QuitToMenu() {} public void Play() {} }
public class scnGame { public void ResetScene() {} public void PrepVfx(List<scrFloor> f, int n, List<ADOFAI.LevelEvent> e, bool b) {} }
namespace StutterFix
{
    internal static class Resilience { internal static bool SafeMode; }
    public static class DrProbe { public static bool Active; }
    public static class DirectDeco { public static bool Enabled, Active; }
    public static class GpuDecoScene
    {
        public static UnityEngine.Camera Main;
        public static UnityEngine.Camera FindMain() => Main;
    }
    public static class Main
    {
        public class Settings { public int GpuDeco; public bool RunLod; }
        public class LoggerType { public void Log(string message) { } }
        public class EntryType { public LoggerType Logger = new LoggerType(); }
        public static readonly Settings Config = new Settings();
        public static readonly EntryType Entry = new EntryType();
    }
    internal static partial class DirectFull
    {
        static System.Collections.IEnumerator preparation;
        static bool preparing, unityGrab, skipExplicit, allExplicitPasses, localPrecision, deferredSubmission, legacyConstants, bakedParticles;
        static string DrawGpuCommand(string arg) => "drawgpu stub";
        static string ResourceCommand(string arg) => "resources stub";
        static string ProbeCommand(string arg) => "stub-probe";
        static readonly System.Collections.Generic.List<object> probeRenderers = new System.Collections.Generic.List<object>();
        static void RestoreProbe() { }
        static string PressureMip(int value) => "stub-pressure";
        static string ColorAudit() => "stub-color-audit";
        static string WhyReplan() => "stub-order-reasons";
        static double warmMs;
        static void Warm() { warmMs = 0; }
        static void Cold() { }
        static void BeginPreparationWork() { }
        static void EndPreparationWork() { }
        static string prepareStage = "stub";
        static long prepareSlices, prepareMaxTicks, prepareWorkTicks;
        static int prepareOriginal;
        static readonly List<int> sources = new List<int>(), dormant = new List<int>();
        internal static void FixturePrepare(System.Collections.IEnumerator steps, UnityEngine.Camera c)
        {
            preparation = steps; preparing = true; camera = preparationCamera = c; prepareOriginal = c.cullingMask;
            hooks = new HarmonyLib.Harmony("fixture"); preparationObject = new UnityEngine.GameObject("prepare");
            preparationDriver = preparationObject.AddComponent<DirectFullPreparation>();
        }
        internal static void FixturePublish() => preparing = false;
        internal static bool FixturePreparing => preparing;
        static string Plan() => "fixture-plan-not-tested";
        static string Census() => "fixture-census-not-tested";
        internal static bool Rendering => false;
        static void StartFull() => throw new InvalidOperationException("Native renderer is tested by the WARP fixture; this fixture tests the shared mask lease");
        static void ReleaseFull() { }
        static void FullPreCull() { }
        static void AssertFull() => throw new InvalidOperationException("fixture native boundary");
        static string FullState() => "fixture-native-not-tested";
    }
}
