#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Reflection;
using StutterFix;
namespace UnityEngine { public static class Time { public static int frameCount; } }
public class FloorMesh { [MethodImpl(MethodImplOptions.NoInlining)] public static void UpdateAllRequired() { } }
public class FloorMeshRenderer { [MethodImpl(MethodImplOptions.NoInlining)] public void set_color() { } }
public class FloorSpriteRenderer { [MethodImpl(MethodImplOptions.NoInlining)] public void set_color() { } }
public class scrFloor
{
    public static bool Throw;
    [MethodImpl(MethodImplOptions.NoInlining)] public void ColorFloor() { SetColor(); }
    [MethodImpl(MethodImplOptions.NoInlining)] public void SetTrackStyle() { }
    [MethodImpl(MethodImplOptions.NoInlining)] public void SetColor() { if(Throw) throw new InvalidOperationException("oracle"); new FloorMeshRenderer().set_color(); }
    [MethodImpl(MethodImplOptions.NoInlining)] public void TweenColor() { }
    [MethodImpl(MethodImplOptions.NoInlining)] public void TweenOpacity() { }
    [MethodImpl(MethodImplOptions.NoInlining)] public void Update() { ColorFloor(); }
    [MethodImpl(MethodImplOptions.NoInlining)] public void LateUpdate() { }
}
namespace StutterFix
{
    internal static class Hitch { internal static bool Playing=true; }
    internal static class TileSim { internal static bool Capturing; }
    internal static class Main
    {
        internal static EntryType Entry=new EntryType();
        internal class EntryType { public LoggerType Logger=new LoggerType(); }
        internal class LoggerType { public List<string> Lines=new List<string>(); public void Log(string s) { Lines.Add(s); Console.WriteLine(s); } }
    }
    internal static class FloorAnim
    {
        internal static long Fallbacks, Real, ZeroWrites, Errors;
        [MethodImpl(MethodImplOptions.NoInlining)] internal static void UpdatePrefix() { Apply(); }
        [MethodImpl(MethodImplOptions.NoInlining)] internal static void Apply() { long a=TileCost.EvalBegin(); System.Threading.Thread.SpinWait(30); TileCost.EvalEnd(a); }
        [MethodImpl(MethodImplOptions.NoInlining)] internal static void StartPrefix() { }
        [MethodImpl(MethodImplOptions.NoInlining)] internal static void ZeroPos() { }
        [MethodImpl(MethodImplOptions.NoInlining)] internal static void ZeroRot() { }
        [MethodImpl(MethodImplOptions.NoInlining)] internal static void ZeroScale() { }
    }
    internal static class TileCull
    {
        [MethodImpl(MethodImplOptions.NoInlining)] internal static void Tick() { Step(); }
        [MethodImpl(MethodImplOptions.NoInlining)] internal static void Step() { RecolorDefer.AfterStep(); }
    }
    internal static class RecolorDefer
    {
        [MethodImpl(MethodImplOptions.NoInlining)] internal static void AfterStep() { Apply(); FloorMesh.UpdateAllRequired(); }
        [MethodImpl(MethodImplOptions.NoInlining)] internal static void Apply() { new scrFloor().ColorFloor(); }
        [MethodImpl(MethodImplOptions.NoInlining)] internal static void ColorFloorPrefix() { }
        [MethodImpl(MethodImplOptions.NoInlining)] internal static void SetTrackStylePrefix() { }
    }
}
static class Program
{
    static void Require(bool v,string label) { if(!v) throw new Exception(label); }
    static readonly MethodInfo pre=typeof(TileCost).GetMethod("Pre",BindingFlags.Static|BindingFlags.NonPublic);
    static readonly MethodInfo final=typeof(TileCost).GetMethod("Final",BindingFlags.Static|BindingFlags.NonPublic);
    static void Scope(Type type,string method,Action body)
    {
        var target=type.GetMethod(method,BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        object[] args={target,null}; pre.Invoke(null,args);
        Exception caught=null;
        try { body(); } catch(Exception ex) { caught=ex; }
        var original=(Exception)final.Invoke(null,new object[]{caught,args[1]});
        Require(ReferenceEquals(caught,original),"exception identity");
        if(original!=null) throw original;
    }
    static void Frame()
    {
        Scope(typeof(FloorAnim),"UpdatePrefix",()=>Scope(typeof(FloorAnim),"Apply",()=>FloorAnim.Apply()));
        Scope(typeof(TileCull),"Tick",()=>Scope(typeof(TileCull),"Step",()=>
            Scope(typeof(RecolorDefer),"AfterStep",()=>Scope(typeof(FloorMesh),"UpdateAllRequired",()=>FloorMesh.UpdateAllRequired()))));
    }
    static void Main()
    {
        TileCost.Command("begin"); Require(TileCost.Active,"begin");
        Frame();
        UnityEngine.Time.frameCount++; // unsampled frame
        Frame();
        UnityEngine.Time.frameCount=16;
        scrFloor.Throw=true;
        try { Scope(typeof(scrFloor),"ColorFloor",()=>Scope(typeof(scrFloor),"SetColor",()=>new scrFloor().SetColor())); throw new Exception("exception swallowed"); }
        catch(InvalidOperationException e) { Require(e.Message=="oracle","original exception identity"); }
        scrFloor.Throw=false; Scope(typeof(FloorAnim),"UpdatePrefix",()=>Scope(typeof(FloorAnim),"Apply",()=>FloorAnim.Apply()));
        TileCost.Command("end"); Require(!TileCost.Active,"end");
        var logs=StutterFix.Main.Entry.Logger.Lines;
        Require(logs.Any(s=>s.Contains("unaccounted-ms=0.000000")),"exclusive conservation");
        Require(logs.Any(s=>s.Contains("ease-eval calls(sampled)=2")),"sampling and recovery after exception");
        Require(logs.Any(s=>s.Contains("patch-failures=0")),"all hooks installed");
        Require(logs.Any(s=>s.Contains("failures=2")),"exception observed in both nested scopes");
        int n=logs.Count; FloorAnim.UpdatePrefix(); Require(logs.Count==n,"unpatched");
        Require(HarmonyLib.Harmony.Unpatched==1,"unpatch requested");
        Console.WriteLine("TileCost accounting fixture passed: nesting, sampling, exception identity, unpatch request; Harmony registration is stubbed; live Mono patching unverified");
    }
}

#endif
