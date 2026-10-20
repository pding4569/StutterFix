using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using StutterFix;
using UnityEngine;
namespace UnityEngine {
    public struct Vector3 { public float x,y,z; }
    public struct Quaternion { }
    public class Transform {
        public Vector3 position {get;set;} public Vector3 localPosition {get;set;}
        public Vector3 eulerAngles {get;set;} public Vector3 localEulerAngles {get;set;}
        public Vector3 localScale {get;set;} public Quaternion rotation {get;set;}
        public void SetPositionAndRotation(Vector3 p,Quaternion r) {position=p;rotation=r;}
    }
}
public class scrFloor { public scrFloor nextfloor; public float opacity,opacityLastFrame,glowMultiplierLastFrame; public Vector3 startPos,startRot,tweenRot,lastPos,lastScale; }
public class scrLevelMaker {}
public class scnGame {}
public class ffxPlusBase { public scrFloor floor; }
public class ffxFloorAppearPlus:ffxPlusBase { public scrFloor prevFloor; }
namespace ADOFAI { public class LevelData {} public class LevelEvent {} }
namespace HarmonyLib {
    public static class PatchProcessor {public static List<CodeInstruction> Original;public static List<CodeInstruction> GetOriginalInstructions(MethodBase m,ILGenerator il)=>Original;}
    public enum ExceptionBlockType { BeginExceptionBlock,EndExceptionBlock }
    public struct ExceptionBlock { public ExceptionBlockType blockType; }
    public class CodeInstruction {
        public OpCode opcode; public object operand; public List<Label> labels=new(); public List<ExceptionBlock> blocks=new();
        public CodeInstruction(OpCode op,object arg=null) {opcode=op;operand=arg;}
    }
    public class HarmonyMethod {public HarmonyMethod(Type t,string n){} }
    public class Harmony {public string Id;public Harmony(string id){Id=id;}public void Patch(MethodBase m,HarmonyMethod prefix=null,HarmonyMethod postfix=null,HarmonyMethod transpiler=null){}public void UnpatchAll(string id){} }
}
namespace StutterFix {
    internal static class FloorRange {
        public const byte Position=1,Rotation=2,Scale=4,Opacity=8,All=15;
        public static int Dirty,Counts; public static readonly List<string> Sites=new();
        public static int RegisterSite(string s) {int i=Sites.IndexOf(s);if(i>=0)return i;Sites.Add(s);return Sites.Count-1;}
        public static void CountSite(int i){Counts++;}
        public static void Mark(scrFloor f,byte mask=All){Dirty++;}public static void Mark(Transform t,byte mask){Dirty++;}
        public static void PreparedHook(){} public static void ResetMap(){}public static void Reason(string s){}public static void Log(string s){}
    }
}
class WriterRewriteCheck {
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static MethodBase Origin=>typeof(WriterRewriteCheck).GetMethod(nameof(Main),BindingFlags.NonPublic|BindingFlags.Static);
    static HarmonyLib.CodeInstruction[] Rewrite(params HarmonyLib.CodeInstruction[] code) {HarmonyLib.PatchProcessor.Original=code.ToList();return FloorRangeWriters.Rewrite(code,Origin).ToArray();}
    static void Main(){
        foreach(string name in new[]{"lastPos","lastScale","opacityLastFrame","glowMultiplierLastFrame"}) {
            var field=typeof(scrFloor).GetField(name);
            var code=Rewrite(new(OpCodes.Ldarg_0),new(OpCodes.Ldarg_1),new(OpCodes.Stfld,field),new(OpCodes.Ret));
            var dm=new DynamicMethod("bookkeeping",typeof(void),new[]{typeof(scrFloor),field.FieldType});var il=dm.GetILGenerator();
            foreach(var ins in code) {if(ins.operand is FieldInfo f)il.Emit(ins.opcode,f);else il.Emit(ins.opcode);}
            var floor=new scrFloor();FloorRange.Dirty=FloorRange.Counts=0;
            object value=field.FieldType==typeof(float)?(object)0.5f:new Vector3{x=7};
            dm.Invoke(null,new[]{(object)floor,value});
            Check(FloorRange.Dirty==0&&FloorRange.Counts==0,"bookkeeping does not dirty: "+name);
            Check(field.GetValue(floor).Equals(value),"original store preserved: "+name);
        }
        var store=new HarmonyLib.CodeInstruction(OpCodes.Stfld,typeof(scrFloor).GetField("opacity"));
        var dm2=new DynamicMethod("opacity",typeof(void),new[]{typeof(scrFloor),typeof(float)});var gen=dm2.GetILGenerator();
        store.labels.Add(gen.DefineLabel());store.blocks.Add(new(){blockType=HarmonyLib.ExceptionBlockType.BeginExceptionBlock});
        var rewritten=Rewrite(store);
        Check(rewritten.Length==2&&rewritten[0].opcode==OpCodes.Ldc_I4&&rewritten[1].operand is MethodInfo,"tracked store has fixed-slot wrapper");
        Check(rewritten[0].labels.Count==1&&rewritten[0].blocks.Count==1&&store.labels.Count==0,"branch/exception entry moves to slot load");
        FloorRange.Dirty=FloorRange.Counts=0;var tile=new scrFloor();FloorRangeWriters.WriteOpacity(tile,0.25f,(int)rewritten[0].operand);
        Check(tile.opacity==0.25f&&FloorRange.Dirty==1&&FloorRange.Counts==1,"state write still marks once");
        Check(FloorRange.Sites.Any(s=>s.Contains("IL_0000 field-write/opacity")),"site has method offset kind and field");
        var address=Rewrite(new HarmonyLib.CodeInstruction(OpCodes.Ldflda,typeof(scrFloor).GetField("tweenRot")));
        Check(address.Length==4&&address[0].opcode==OpCodes.Dup&&address[1].opcode==OpCodes.Ldc_I4&&address[3].opcode==OpCodes.Ldflda,"address retains stack and fixed slot");
        var transform=Rewrite(new HarmonyLib.CodeInstruction(OpCodes.Callvirt,typeof(Transform).GetProperty("position").SetMethod));
        Check(transform.Length==2&&((MethodInfo)transform[1].operand).Name=="Position","transform setter retained");
        var original=new[]{new HarmonyLib.CodeInstruction(OpCodes.Nop),new HarmonyLib.CodeInstruction(OpCodes.Stfld,typeof(scrFloor).GetField("startPos"))};
        HarmonyLib.PatchProcessor.Original=original.ToList();
        var changed=new[]{new HarmonyLib.CodeInstruction(OpCodes.Ldc_I4,123),original[1]};
        var mapped=FloorRangeWriters.Rewrite(changed,Origin).ToArray();
        Check(FloorRange.Sites[(int)mapped[1].operand].Contains("IL_0001 field-write/startPos"),"offset comes from original IL despite prior transpiler size changes");
        HarmonyLib.PatchProcessor.Original=new();bool rejected=false;
        try {FloorRangeWriters.Rewrite(new[]{new HarmonyLib.CodeInstruction(OpCodes.Stfld,typeof(scrFloor).GetField("opacity"))},Origin).ToArray();}
        catch(InvalidOperationException){rejected=true;}
        Check(rejected,"unmatched original site fails closed");
        Console.WriteLine("PASS writer-rewrite checks="+checks+"; production transpiler and emitted bookkeeping IL; modeled engine/Harmony");
    }
}
