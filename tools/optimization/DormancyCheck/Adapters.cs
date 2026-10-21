// Managed adapters only. No installed game methods or Unity lifecycle execute.
using System.Reflection;
using System.Reflection.Emit;
namespace HarmonyLib
{
    public static class AccessTools
    {
        public delegate ref F FieldRef<T, F>(T instance);
        public static FieldRef<T, F> FieldRefAccess<T, F>(string name)
        {
            var field = typeof(T).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var method = new DynamicMethod(name, typeof(F).MakeByRefType(), new[] { typeof(T) }, typeof(AccessTools), true);
            var il = method.GetILGenerator(); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldflda, field); il.Emit(OpCodes.Ret);
            return method.CreateDelegate<FieldRef<T, F>>();
        }
        public static MethodInfo Method(Type type, string name) => typeof(object).GetMethod("ToString");
    }
    public class Harmony { public void Patch(MethodInfo method, HarmonyMethod postfix) { } }
    public class HarmonyMethod { public HarmonyMethod(Type type, string method) { } }
}
namespace UnityEngine
{
    public class Object { public static implicit operator bool(Object value) => value != null; }
    public class SpriteRenderer { public bool enabled = true, forceRenderingOff, isVisible = true; }
    public static class Time { public static float realtimeSinceStartup, timeScale = 1; public static int frameCount; }
    public static class AudioListener { public static bool pause; }
}
namespace DG.Tweening { public static class DOTween { public static float timeScale = 1; } }
public class scrDecoration { public int hitbox; public bool Visible; public virtual bool GetVisible() => Visible; }
public class scrVisualDecoration : scrDecoration
{
    public bool meshRendererEnabled;
    public UnityEngine.SpriteRenderer spriteRenderer = new();
}
public class scrDecorationManager { public List<scrDecoration> allDecorations = new(); }
public class scrController : UnityEngine.Object { public static scrController instance = new(); public bool disableV15Features = true, paused; }
public static class ADOBase { public static scrController controller => scrController.instance; }
namespace StutterFix
{
    public static class MoveApply { public static bool LogicSkip = true; }
    public static class Hitch { public static bool Playing; }
    internal sealed partial class FrameCompare
    {
        static FrameCompare instance = new();
        internal static bool FailRestore;
        void RestoreCapture() { }
        static void FreezePlanets() { }
        static void RestorePlanets() { if (FailRestore) throw new InvalidOperationException("restore adapter failure"); }
        static void Log(string message) => Console.WriteLine("[framecmp] " + message);
    }
    internal static class SceneReset { internal static int Count = 0; internal static bool Resetting; }
    public static class Edition { public const bool Dev = true; }
    public static class Main
    {
        public static bool MeasureBuild;
        public static readonly EntryType Entry = new();
        public class EntryType { public LoggerType Logger = new(); }
        public class LoggerType { public void Log(string message) => Console.WriteLine(message); }
    }
    public static class LiveDecoRenderer
    {
        public static bool Active;
        public static int Changes, Rebuilds, Resets, Restores;
        public static void DormancyChanged(scrDecoration d) { Changes++; }
        public static void DormancyRebuilt() { Rebuilds++; Restore(); }
        public static void DormancyReset() { Resets++; Restore(); }
        static void Restore() { if (Active) { Active = false; Restores++; } }
        public static void DormancyAudit(long ticks) { }
    }
    public static class DecoEngine { public static void DropLevel() { } }
    public static class DecoAnim { public static void BatchForget() { } }
    public static class PosFast { public static void Forget() { } }
    public static class TightLate { public static void Forget() { } }
    public static class InvisibleSkip { public static bool IsHidden(scrDecoration d) => !d.Visible; }
}
