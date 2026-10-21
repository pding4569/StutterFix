// Actual DecoFreeze callbacks with engine/patch registration substitutes.
// Does not test Harmony detours or Unity rendering; never loads game assemblies.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using StutterFix;
using UnityEngine;

namespace UnityEngine
{
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x=x; this.y=y; } }
    public static class Time { public static int frameCount; }
    public enum LogType { Error, Assert, Warning, Log, Exception }
    public static class Application
    {
        public static event Action<string,string,LogType> logMessageReceived;
        public static void Emit() { logMessageReceived?.Invoke("fixture oracle", "", LogType.Exception); }
    }
}
public class scrDecoration
{
    public scrParallax parallax;
    public Vector2 pivotPosVec, pivotOffsetVec, scaleVec;
    public float rotAngle;
    public int Clamps;
    private void UpdateScreenClamp() { Clamps++; }
    public void UpdatePosition() { }
    public void SetPosition(Vector2 p, Vector2 o) { }
    public void SetRotation(float x) { }
    public virtual void SetScale(Vector2 x) { }
}
public class scrVisualDecoration : scrDecoration { }
public class DerivedImage : scrVisualDecoration { }
public class scrParallax { public scrDecoration decoration; public void SetTrans() { } }
public class scrConductor { public static scrConductor instance = new(); public double songposition_minusi; }
namespace StutterFix
{
    internal static class Hitch { internal static bool Playing = true; }
    internal static class SceneReset { internal static bool Resetting; }
    internal static class TightLate { internal static int Invalidations; internal static void Invalidate() { Invalidations++; } }
    internal static class DecoAnim { internal static bool Enabled=true, Installed=true; internal static long Errors; }
    internal static class Edition { internal static bool Dev; }
    internal static class Main { internal static Entry Entry = new(); }
    internal class Entry { internal Logger Logger = new(); }
    internal class Logger { internal readonly List<string> Lines = new(); internal void Log(string s) { Lines.Add(s); } }
}
namespace HarmonyLib
{
    public static class Priority { public const int First=800; }
    public class HarmonyMethod { public int priority; public HarmonyMethod(Type t, string n) { } }
    public class Harmony
    {
        public static int Registered, FailAt=-1;
        public Harmony(string owner) { }
        public void Patch(MethodInfo m, HarmonyMethod prefix)
        { if (++Registered == FailAt) throw new InvalidOperationException("patch oracle"); }
        public void UnpatchAll(string owner) { Registered=0; }
    }
    public static class AccessTools
    {
        public static MethodInfo DeclaredMethod(Type t, string n, Type[] args) =>
            t.GetMethod(n, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly, null, args, null);
        public static T MethodDelegate<T>(MethodInfo m) where T : Delegate => (T)m.CreateDelegate(typeof(T));
    }
}
static class Program
{
    static int checks;
    static void Check(bool condition, string why) { checks++; if (!condition) throw new Exception(why); }
    static void Throws(Action a) { bool threw=false; try { a(); } catch (InvalidOperationException) { threw=true; } Check(threw, "missing rejection"); }
    static void Main(string[] args)
    {
        try { Run(args); }
        catch (Exception ex)
        {
            foreach (var line in StutterFix.Main.Entry.Logger.Lines.Where(s=>s.Contains("RESULT") || s.Contains("SUMMARY"))) Console.WriteLine(line);
            Console.Error.WriteLine(ex); Environment.ExitCode=1;
        }
    }
    static void Run(string[] args)
    {
        var image = new scrVisualDecoration { parallax = new scrParallax() };
        image.parallax.decoration=image;
        Check(DecoFreeze.PositionPrefix(image), "default off");
        DecoFreeze.Command("reset");
        Check(HarmonyLib.Harmony.Registered==5, "five exact hooks");
        DecoFreeze.Command("pos");
        Check(!DecoFreeze.PositionPrefix(image), "image position frozen");
        Check(DecoFreeze.PositionPrefix(new DerivedImage()), "unknown subclass untouched");
        Check(DecoFreeze.PositionPrefix(new scrDecoration()), "non-image untouched");
        Check(!DecoFreeze.SetPositionPrefix(image, new(2,3), new(4,5)) && image.pivotPosVec.x==2 && image.pivotOffsetVec.y==5 && image.Clamps==1, "managed position preserved");
        Check(!DecoFreeze.RotationPrefix(image, 37) && image.rotAngle==37, "rotation preserved");
        Check(!DecoFreeze.ScalePrefix(image, new(6,7)) && image.scaleVec.y==7, "scale preserved");
        Check(!DecoFreeze.ParallaxPrefix(image.parallax), "image parallax frozen");
        Check(DecoFreeze.ParallaxPrefix(new()), "foreign parallax untouched");
        Check(DecoFreeze.SkipTight(image) && !DecoFreeze.SkipAnimation(), "pos backend scope");
        SceneReset.Resetting=true;
        Check(DecoFreeze.PositionPrefix(image), "reset original path");
        SceneReset.Resetting=false; Hitch.Playing=false;
        Check(DecoFreeze.PositionPrefix(image), "edit mode original path"); Hitch.Playing=true;
        DecoFreeze.Command("anim");
        Check(DecoFreeze.PositionPrefix(image) && DecoFreeze.SkipAnimation(), "anim backend scope");
        scrConductor.instance.songposition_minusi=0; Throws(DecoFreeze.Begin);
        scrConductor.instance.songposition_minusi=262; DecoFreeze.Begin();
        Throws(() => DecoFreeze.Command("pos"));
        Time.frameCount+=10; DecoAnim.Errors++; Application.Emit();
        scrConductor.instance.songposition_minusi=276; DecoFreeze.End("fixture fuse");
        Check(StutterFix.Main.Entry.Logger.Lines.Any(s=>s.Contains("valid=False") && s.Contains("decoanim_errors=1 unity_errors=1")), "invalid exception sample");
        Check(StutterFix.Main.Entry.Logger.Lines.Any(s=>s.Contains("[Error] invalid_window")), "sf_errors visible");
        DecoFreeze.Command("reset");
        foreach (string mode in new[] { "off", "pos", "anim", "both", "off", "both", "anim", "pos", "off" })
        {
            DecoFreeze.Command(mode); scrConductor.instance.songposition_minusi=262; DecoFreeze.Begin();
            DecoFreeze.PositionPrefix(image); DecoFreeze.SkipAnimation();
            System.Threading.Thread.SpinWait(1000); // ensure nonzero elapsed time for synthetic windows
            Time.frameCount+=10; scrConductor.instance.songposition_minusi=276; DecoFreeze.End("fixture fuse");
        }
        DecoFreeze.Command("summary");
        Check(StutterFix.Main.Entry.Logger.Lines.Last().Contains("SUMMARY complete=True"), "nine valid samples");
        Check(DecoFreeze.PositionPrefix(image) && !DecoFreeze.SkipAnimation(), "summary turns off");
        DecoFreeze.Command("reset"); DecoFreeze.Command("both"); Edition.Dev=true;
        scrConductor.instance.songposition_minusi=262; DecoFreeze.Begin();
        DecoFreeze.PositionPrefix(image); DecoFreeze.SkipAnimation(); Time.frameCount++;
        scrConductor.instance.songposition_minusi=276; DecoFreeze.End("dev oracle");
        Check(StutterFix.Main.Entry.Logger.Lines.Last().Contains("valid=False"), "DEV shadow excluded");
        Edition.Dev=false; DecoFreeze.Shutdown();
        Check(HarmonyLib.Harmony.Registered==0 && DecoFreeze.PositionPrefix(image), "shutdown removes hooks");
        HarmonyLib.Harmony.FailAt=3; Throws(()=>DecoFreeze.Command("both"));
        Check(HarmonyLib.Harmony.Registered==0 && DecoFreeze.PositionPrefix(image), "partial patch rollback");
        foreach (var file in args)
        {
            using var pe = new PEReader(File.OpenRead(file)); var md = pe.GetMetadataReader();
            bool player = Path.GetFileName(Path.GetDirectoryName(file))=="Player";
            bool found = md.TypeDefinitions.Any(h=>md.GetString(md.GetTypeDefinition(h).Name)=="DecoFreeze");
            Check(found != player, "edition type exclusion " + file);
            if (player)
            {
                Check(!md.TypeReferences.Any(h=>md.GetString(md.GetTypeReference(h).Name).Contains("DecoFreeze")), "Player type reference");
                Check(!md.MemberReferences.Any(h=>md.GetString(md.GetMemberReference(h).Name).Contains("DecoFreeze")), "Player member reference");
                Check(File.ReadAllBytes(file).AsSpan().IndexOf(System.Text.Encoding.Unicode.GetBytes("decofreeze")) < 0, "Player command string");
            }
            Console.WriteLine("PASS metadata: " + file + " DecoFreeze=" + found);
        }
        Console.WriteLine($"PASS: {checks} callback/lifecycle/metadata assertions; Harmony registration and Unity are substitutes, no game executed");
    }
}
