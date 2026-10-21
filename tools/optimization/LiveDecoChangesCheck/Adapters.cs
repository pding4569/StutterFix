using System.Reflection;
using System.Reflection.Emit;

public class scrDecoration { }
namespace HarmonyLib
{
    public enum ExceptionBlockType { BeginExceptionBlock, EndExceptionBlock }
    public class ExceptionBlock { public ExceptionBlockType blockType; }
    public class CodeInstruction
    {
        public OpCode opcode; public object operand;
        public List<int> labels = new(); public List<ExceptionBlock> blocks = new();
        public CodeInstruction(OpCode op, object value = null) { opcode = op; operand = value; }
    }
    public class HarmonyMethod { public HarmonyMethod(Type t, string name) { } }
    public class Harmony { public void Patch(MethodBase m, HarmonyMethod transpiler) { } }
    public static class AccessTools { public const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static; }
    public static class PatchProcessor
    {
        public static IEnumerable<CodeInstruction> GetOriginalInstructions(MethodBase method, object generator = null) => Array.Empty<CodeInstruction>();
    }
}
namespace UnityEngine
{
    public class Object
    {
        public int Writes;
        public static Object Instantiate(Object original) => new GameObject { Writes = original.Writes + 1 };
        public static Object Instantiate(Object original, int a) => new GameObject { Writes = original.Writes + a };
        public static Object Instantiate(Object original, int a, string b) => new GameObject { Writes = original.Writes + a + b.Length };
        public static Object Instantiate(Object original, int a, string b, float c) => new GameObject { Writes = original.Writes + a + b.Length + (int)c };
        public static T Instantiate<T>(T original) where T : Object, new() => new T { Writes = original.Writes + 1 };
        public static void Destroy(Object obj) { obj.Writes = -1; }
        public static void DestroyImmediate(Object obj, bool assets) { obj.Writes = assets ? -3 : -2; }
    }
    public class Behaviour : Object { public bool enabled { set { Writes += value ? 1 : 2; } } }
    public class GameObject : Object
    {
        public void SetActive(bool value) { Writes += value ? 2 : 1; }
        public T AddComponent<T>() where T : Object, new() => new T { Writes = 11 };
        public Object AddComponent(Type type) => (Object)Activator.CreateInstance(type);
    }
    public class Transform : Object { public float localPosition { set { Writes += (int)value; } } }
    public class Renderer : Object
    {
        public bool enabled { set { Writes += value ? 1 : 2; } }
        public void SetPropertyBlock(string value, int slot) { Writes += value.Length + slot; }
    }
    public class Material : Object
    {
        public void SetZero() { Writes++; }
        public void SetFloat(string value, float a) { Writes += value.Length + (int)a; }
        public void SetThree(int a, float b, string c) { Writes += a + (int)b + c.Length; }
        public void SetFour(int a, float b, string c, bool d) { Writes += a + (int)b + c.Length + (d ? 1 : 0); }
        public void SetThrow(int a) { Writes = a; throw new InvalidOperationException("writer failure"); }
        public float GetFloat(string name) => name.Length;
    }
}
namespace StutterFix
{
    internal static class LiveDecoRenderer
    {
        internal static int Before, After;
        internal static UnityEngine.Object LastBefore, LastAfter;
        internal static void BeforeWrite(UnityEngine.Object target, int kind) { Before++; LastBefore = target; }
        internal static void AfterWrite(UnityEngine.Object target, int kind) { After++; LastAfter = target; }
    }
}
