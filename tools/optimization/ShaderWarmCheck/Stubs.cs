// Fault injection for resource ownership/retry only. No GPU or game emulation claim.
using System;
using System.Collections.Generic;
using System.Reflection;

namespace UnityEngine
{
    public class Object
    {
        public static readonly List<Material> Destroyed = new List<Material>();
        public static void Destroy(Object value) { if (value is Material m) Destroyed.Add(m); }
    }
    public enum HideFlags { HideAndDontSave }
    public struct Color { public static Color clear => default; }
    public class Shader : Object
    {
        public string name; public bool isSupported = true; public int Passes = 3;
        public static bool Missing; public static int FindCalls;
        public static Shader Filter = new Shader { name = "fixture" };
        public static Shader Find(string name) { FindCalls++; return Missing ? null : Filter; }
        public static void WarmupAllShaders() { }
    }
    public class Material : Object
    {
        public static bool Fail; public static readonly List<Material> Created = new List<Material>();
        public HideFlags hideFlags; private readonly Shader shader;
        public Material(Shader value)
        {
            if (Fail) throw new Exception("material allocation");
            shader = value; Created.Add(this);
        }
        public int passCount => shader.Passes;
    }
    public class RenderTexture : Object
    {
        public static RenderTexture active;
        public static int Allocations, FailAt, NullAt;
        public static readonly List<RenderTexture> Acquired = new List<RenderTexture>();
        public static readonly List<RenderTexture> Released = new List<RenderTexture>();
        public static RenderTexture GetTemporary(int w, int h, int depth)
        {
            int n = ++Allocations;
            if (n == FailAt) throw new Exception("RT allocation");
            if (n == NullAt) return null;
            var rt = new RenderTexture(); Acquired.Add(rt); return rt;
        }
        public static void ReleaseTemporary(RenderTexture rt)
        {
            if (ReferenceEquals(active, rt)) throw new Exception("releasing active temporary");
            if (Released.Contains(rt)) throw new Exception("double release");
            Released.Add(rt);
        }
    }
    public static class GL
    {
        public static bool Fail; public static int Clears;
        public static void Clear(bool depth, bool color, Color value)
        {
            if (RenderTexture.active == null) throw new Exception("cleared the screen");
            Clears++; if (Fail) throw new Exception("clear failed");
        }
    }
    public static class Graphics
    {
        public static int Calls, FailAt;
        public static void Blit(RenderTexture a, RenderTexture b, Material m, int pass)
        {
            RenderTexture.active = b;
            if (++Calls == FailAt) throw new Exception("pass failed");
        }
    }
    public static class Resources
    {
        public static bool Missing; public static int Loads;
        public static Object Load(string name) { Loads++; return Missing ? null : new Object(); }
        public static T[] FindObjectsOfTypeAll<T>() => Array.Empty<T>();
    }
}
namespace HarmonyLib
{
    public static class AccessTools
    {
        public static MethodInfo Method(Type type, string name, Type[] args) => type.GetMethod(name, args);
    }
}
namespace ADOFAI { public enum LevelEventType { SetFilterAdvanced } }
public class LevelEvent
{
    public ADOFAI.LevelEventType eventType;
    public string GetString(string key) => typeof(FixtureFilter).AssemblyQualifiedName;
}
public class FixtureFilter : UnityEngine.Object
{
    public void Start() { UnityEngine.Shader.Find("fixture"); UnityEngine.Resources.Load("fixture-texture"); }
}
public class LevelData { public List<LevelEvent> levelEvents = new List<LevelEvent>(); }
public class Level : UnityEngine.Object { public LevelData levelData = new LevelData(); }
public static class ADOBase { public static string levelPath; public static Level customLevel; }
public class scrVfxPlus : UnityEngine.Object
{
    public static scrVfxPlus instance; public Dictionary<string, FixtureFilter> filterToComp;
}
namespace StutterFix
{
    public static class Hitch { public static bool Playing; }
    public static class Edition { public const bool Dev = false; }
    public static class SettingsWindow { public static string T(string a, string b) => a; }
    public static class ModCost { public static void Add(string name, double value) { } }
    public static class Main { public static Entry Entry = new Entry(); }
    public class Entry { public Logger Logger = new Logger(); }
    public class Logger { public void Log(string value) { } public void Error(string value) { } }
}
