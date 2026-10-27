using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace HarmonyLib
{
    public class HarmonyMethod { public HarmonyMethod(Type t, string name) { } }
    public class Patch { public string owner = "Foreign"; }
    public class Patches
    {
        public List<string> Owners = new();
        public List<Patch> Transpilers = new(), Prefixes = new();
    }
    public class Harmony
    {
        public static bool Foreign;
        public static List<string> Installed = new();
        public void Patch(MethodBase m, HarmonyMethod prefix = null, HarmonyMethod postfix = null) => Installed.Add(m.DeclaringType.Name + "." + m.Name);
        public static Patches GetPatchInfo(MethodBase m) => Foreign ? new Patches { Owners = new() { "Foreign" }, Prefixes = new() { new Patch() } } : null;
    }
    public static class AccessTools
    {
        public delegate ref F FieldRef<T, F>(T instance);
        public static bool MissingBridge;
        public static FieldInfo Field(Type t, string name) => t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        public static MethodBase Method(Type t, string name) => MissingBridge && t == typeof(ffxLightBridge) ? null : t.GetMethod(name);
        public static FieldRef<T, F> FieldRefAccess<T, F>(string name) => FieldRefAccess<T, F>(Field(typeof(T), name));
        public static FieldRef<T, F> FieldRefAccess<T, F>(FieldInfo field)
        {
            var method = new DynamicMethod("fieldref", typeof(F).MakeByRefType(), new[] { typeof(T) }, typeof(AccessTools).Module, true);
            var il = method.GetILGenerator(); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldflda, field); il.Emit(OpCodes.Ret);
            return (FieldRef<T, F>)method.CreateDelegate(typeof(FieldRef<T, F>));
        }
    }
}
namespace UnityEngine
{
    public class Object
    {
        public static implicit operator bool(Object value) => value is not null;
    }
    public struct Color : IEquatable<Color>
    {
        public float r, g, b, a;
        public Color(float value) { r = g = b = value; a = value; }
        public bool Equals(Color c) => r.Equals(c.r) && g.Equals(c.g) && b.Equals(c.b) && a.Equals(c.a);
    }
    public class Sprite : Object { public int id; }
    public class SpriteRenderer : Object
    {
        public static int Reads;
        private Color c;
        private Sprite s;
        public Color color { get { Reads++; return c; } set => c = value; }
        public Sprite sprite { get { Reads++; return s; } set => s = value; }
    }
    public static class Time { public static int frameCount; }
}
public class ADOBase
{
    public static scrController controller;
    public static scrLevelMaker lm;
    public virtual void OnBeat() { }
}
public class scrController { public bool gameworld = true; }
public class scrConductor
{
    public List<ADOBase> onBeats = new();
    public int onBeatFrame;
    public void PropagateOnBeat() { }
}
public class scrVfx
{
    public static scrVfx instance;
    public bool overrideTileSprites, overrideGlowSprites;
    public int style, overrideStyle;
}
public class scrLevelMaker
{
    public List<scrFloor> listFloors = new();
    public void MakeLevel() { }
}
public class scrFloor : ADOBase
{
    public scrVfx vfx;
    public bool hasLit, dontChangeMySprite, violateNoOp;
    public int id, calls, randomNumberForLitAndGlow = 7;
    public SpriteRenderer topGlow = new(), bottomGlow = new(), floorRenderer = new();
    public static uint Rng;
    public static int Beat;
    public static readonly List<int> WorkOrder = new();
    public static readonly Sprite[] Sprites = Enumerable.Range(0, 8).Select(i => new Sprite { id = i }).ToArray();
    public void LightUp() { hasLit = true; }
    public void Reset() { hasLit = false; dontChangeMySprite = false; }
    public void TweenOpacity() { dontChangeMySprite = true; }
    public override void OnBeat()
    {
        calls++;
        if (dontChangeMySprite) { if (violateNoOp) topGlow.color = new Color(0.75f); return; }
        if (!hasLit) { if (vfx.overrideTileSprites) { WorkOrder.Add(id); floorRenderer.sprite = Sprites[0]; } return; }
        WorkOrder.Add(id);
        if (vfx.style == 1) { floorRenderer.color = new Color(Next() % 10); topGlow.color = new Color(0); if (bottomGlow) bottomGlow.color = new Color(0); }
        if (vfx.style >= 2 && vfx.style <= 4) { topGlow.color = new Color(vfx.style + Beat % 2); if (bottomGlow && vfx.style != 4) bottomGlow.color = topGlow.color; }
        randomNumberForLitAndGlow = vfx.overrideStyle == 1 ? (int)(Next() % 8) : vfx.overrideStyle == 2 ? (id + Beat) % 2 : 0;
        if (vfx.overrideTileSprites) floorRenderer.sprite = Sprites[randomNumberForLitAndGlow];
        if (vfx.overrideGlowSprites) { topGlow.sprite = Sprites[randomNumberForLitAndGlow]; if (bottomGlow) bottomGlow.sprite = topGlow.sprite; }
        var top = topGlow.color; top.a = Math.Min(top.a, floorRenderer.color.a); topGlow.color = top;
        if (bottomGlow) { var bottom = bottomGlow.color; bottom.a = Math.Min(bottom.a, floorRenderer.color.a); bottomGlow.color = bottom; }
    }
    private static uint Next() => Rng = unchecked(Rng * 1664525 + 1013904223);
}
public class ffxLightBridge
{
    public List<scrFloor> brigeFloors = new();
    public void StartEffect(scrPlanet p) { foreach (var f in brigeFloors) f.dontChangeMySprite = true; }
}
public class scrPlanet { }
namespace StutterFix
{
    public static class Edition { public const bool Dev = true; }
    public static class Main
    {
        public static ModEntry Entry = new();
        public class ModEntry { public LogSink Logger = new(); }
        public class LogSink { public void Log(string s) { } }
    }
}
