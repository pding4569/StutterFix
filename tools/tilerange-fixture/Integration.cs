// Control-flow fixture. Patching registration and Unity state are substitutes;
// this does not claim Harmony/Mono detours or engine float/native parity.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using StutterFix;

namespace UnityEngine
{
    public struct Vector3 { public float x,y,z; }
    public class Transform { public Vector3 position, eulerAngles, localScale; }
    public static class Mathf { public static bool Approximately(float a,float b)=>Math.Abs(a-b)<Math.Max(0.000001f*Math.Max(Math.Abs(a),Math.Abs(b)),float.Epsilon*8); }
}
namespace DG.Tweening { public class Tween { public bool active; public object id; } }
public enum TweenType { Opacity }
public class Renderer { public bool isVisible; }
public class FloorRenderer { public Renderer renderer=new Renderer(); public object material=new object(); }
public class scrFloor
{
    public int seqID;
    public object freeroamArea;
    public FloorRenderer floorRenderer=new FloorRenderer();
    public UnityEngine.Transform transform=new UnityEngine.Transform();
    public float opacity;
    public bool dontChangeMySprite;
    public Dictionary<TweenType,DG.Tweening.Tween> moveTweens=new Dictionary<TweenType,DG.Tweening.Tween>();
    public float Read() => opacity;
}
public class scrLevelMaker { public List<scrFloor> listFloors; }
public class FreeroamArea { }
public class Controller { public scrFloor currFloor; }
public class ADOBase { public static scrLevelMaker lm; public static Controller controller; }
namespace ADOFAI { public class LevelData {} public class LevelEvent {} }
public class scnGame { public static void ApplyEventsToFloors(List<scrFloor> f,ADOFAI.LevelData d,scrLevelMaker m,List<ADOFAI.LevelEvent> e) {} }
namespace HarmonyLib
{
    public static class AccessTools
    {
        public const BindingFlags all=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        public static FieldInfo Field(Type t,string name)=>t.GetField(name,all);
        public static MethodInfo Method(Type t,string name)=>t.GetMethods(all).FirstOrDefault(m=>m.Name==name);
        public static MethodInfo Method(Type t,string name,Type[] p)=>t.GetMethod(name,all,null,p,null);
    }
    public class CodeInstruction { public OpCode opcode; public object operand; public CodeInstruction(OpCode op,object o){opcode=op;operand=o;} }
    public class HarmonyMethod { public HarmonyMethod(Type t,string n) {} }
    public class Harmony
    {
        public string Id; public static int Patches,Unpatches;
        public Harmony(string id){Id=id;}
        public void Patch(MethodBase m,HarmonyMethod postfix=null,HarmonyMethod transpiler=null,HarmonyMethod prefix=null){Patches++;}
        public void UnpatchAll(string id){Unpatches++;}
    }
    public static class PatchProcessor
    {
        public static IEnumerable<CodeInstruction> GetOriginalInstructions(MethodBase method)
        {
            // Small exact fixture inventory; production uses Harmony's full decoder.
            if(method.DeclaringType==typeof(scrFloor)&&method.Name=="Read")
                yield return new CodeInstruction(OpCodes.Ldfld,typeof(scrFloor).GetField("opacity"));
        }
    }
}
namespace StutterFix
{
    internal static class Main
    {
        internal static EntryType Entry=new EntryType();
        internal class EntryType { public LoggerType Logger=new LoggerType(); }
        internal class LoggerType { public List<string> Lines=new List<string>(); public void Log(string s){Lines.Add(s);} }
    }
    internal static class TileSim { internal static bool Capturing; }
    internal static class TileCull
    {
        internal static HashSet<scrFloor> Hidden=new HashSet<scrFloor>();
        internal static bool IsCulled(scrFloor f)=>Hidden.Contains(f);
    }
    internal static class FloorAnim
    {
        internal static bool Split;
        internal sealed class Rec { public bool Running=true; public float Target; }
        internal struct Ctx { public UnityEngine.Vector3 Pos,Scale; public bool Zero,OpaOnly,PosUsed,RotUsed,ScaleUsed; public float Opacity; }
        internal static void RangeLegacy(scrFloor f,ref Ctx c){FloorRange.BeforeLegacy(f);RangeCompleteOpacity(f);RangeReferenceOpacity(f,c.Opacity);}
        internal static void RangeCompleteOpacity(scrFloor f)
        {
            if(f.moveTweens.TryGetValue(TweenType.Opacity,out var t)&&t.id is Rec r&&r.Running){r.Running=false;f.opacity=r.Target;}
        }
        internal static void RangeWriteOpacity(scrFloor f,float target)
        {if(!UnityEngine.Mathf.Approximately(f.opacity,target)){f.opacity=target;f.dontChangeMySprite=true;}}
        internal static void RangeReferenceOpacity(scrFloor f,float target)
        {if(!UnityEngine.Mathf.Approximately(f.opacity,target)){f.opacity=target;f.dontChangeMySprite=true;}}
    }
}
class Integration
{
    static int checks;
    static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
    static List<scrFloor> Make()
    {
        var list=Enumerable.Range(0,2048).Select(i=>new scrFloor{seqID=i,opacity=0.2f}).ToList();
        ADOBase.lm=new scrLevelMaker{listFloors=list};
        ADOBase.controller=new Controller{currFloor=list[0]};
        TileCull.Hidden.Clear();foreach(var f in list)TileCull.Hidden.Add(f);
        return list;
    }
    static FloorAnim.Ctx C(float value)=>new FloorAnim.Ctx{Zero=true,OpaOnly=true,Opacity=value};
    static bool Start(List<scrFloor> f,float value,int lo=0,int hi=2047){var c=C(value);return FloorRange.TryStart(f,lo,hi,1,ref c);}
    static void Main()
    {
        var list=Make();FloorRange.Install(new HarmonyLib.Harmony("fixture-lifecycle"));FloorRange.Command("on");
        Check(HarmonyLib.Harmony.Patches>0,"reader patch registered");
        Check(Start(list,0.4f),"first accept");Check(list[1600].opacity==0.2f,"offscreen deferred");
        Check(FloorRange.ReadOpacity(list[1600])==0.4f,"read barrier");
        Check(Start(list,0.7f,64,1900),"overlap");FloorRange.WriteOpacity(list[1601],0.9f);
        Check(FloorRange.ReadOpacity(list[1601])==0.9f,"write barrier consumes both generations");
        Check(FloorRange.ReadSprite(list[1601]),"sprite side effect");
        FloorRange.WriteSprite(list[1601],false);Check(!FloorRange.ReadSprite(list[1601]),"sprite write order");
        var before=list[1700].moveTweens;Check(ReferenceEquals(before,FloorRange.ReadTweens(list[1700])),"dictionary identity");
        Check(Start(list,0.1f),"mixed chunk fallback");Check(list[1700].opacity==0.1f,"fenced chunk eager");
        list[1300].moveTweens[TweenType.Opacity]=new DG.Tweening.Tween{id=new FloorAnim.Rec{Target=0.6f}};
        Check(Start(list,0.6f),"pending legacy predecessor");
        Check(FloorRange.ReadOpacity(list[1300])==0.6f,"predecessor completed before approximately");
        FloorRange.Command("shadow");Check(Start(list,0.3f),"shadow accept");FloorRange.Tick();FloorRange.Flush("fixture");
        Check(list.All(f=>FloorRange.ReadOpacity(f)==0.3f),"full flush");
        Check(MainLog().Contains("mismatch=0"),"shadow mismatch zero");
        var c=C(1);c.Zero=false;Check(!FloorRange.TryStart(list,0,2047,1,ref c),"duration fallback");
        c=C(1);c.OpaOnly=false;Check(!FloorRange.TryStart(list,0,2047,1,ref c),"channel fallback");
        c=C(1);Check(!FloorRange.TryStart(list,0,2047,2,ref c),"gap fallback");
        Check(FloorRange.TryStart(list,0,999,1,ref c),"1000 threshold");
        Check(!FloorRange.TryStart(list,0,998,1,ref c),"999 fallback");
        TileSim.Capturing=true;Check(!Start(list,1),"capture fallback");TileSim.Capturing=false;
        FloorAnim.Split=true;Check(!Start(list,1),"split fallback");FloorAnim.Split=false;
        for(int n=0;n<36;n++)Check(Start(list,n/36f),"depth continuation");
        FloorRange.Flush("fixture-depth");
        Check(list.All(f=>f.opacity==35/36f),"depth local fallback final value");
        FloorRange.Command("off");Check(HarmonyLib.Harmony.Unpatches>0,"off removes research detours");
        Check(!Start(list,0),"off rejects");FloorRange.Command("on");Check(Start(list,0.8f),"reenable");
        FloorRange.ResetMap();Check(list.All(f=>f.opacity==0.8f),"reset flushes logical zero writes");
        Check(!Start(list,0),"reset clears map");
        var instructions=FloorRange.Rewrite(new[]{new HarmonyLib.CodeInstruction(OpCodes.Ldfld,typeof(scrFloor).GetField("opacity")),new HarmonyLib.CodeInstruction(OpCodes.Stfld,typeof(scrFloor).GetField("dontChangeMySprite"))}).ToArray();
        Check(instructions.All(i=>i.opcode==OpCodes.Call),"typed field rewrite");
        bool address=false;try{FloorRange.Rewrite(new[]{new HarmonyLib.CodeInstruction(OpCodes.Ldflda,typeof(scrFloor).GetField("opacity"))}).ToArray();}catch(InvalidOperationException){address=true;}
        Check(address,"field address rejects coverage");FloorRange.Shutdown();
        // Structural writes consume pending state before replacing its renderer.
        list=Make();FloorRange.Command("on");Check(Start(list,0.5f),"structural test start");
        var renderer=new FloorRenderer();FloorRange.WriteRenderer(list[1800],renderer);
        Check(list[1800].opacity==0.5f&&ReferenceEquals(renderer,FloorRange.ReadRenderer(list[1800])),"renderer replacement barrier");
        FloorRange.Shutdown();
        Console.WriteLine($"PASS integration checks={checks}; real FloorRange source, modeled Unity state and patch registration; engine detours untested");
    }
    static string MainLog(){FloorRange.Command("status");return StutterFix.Main.Entry.Logger.Lines.Last();}
}
