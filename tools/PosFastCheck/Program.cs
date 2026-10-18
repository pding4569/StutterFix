// Managed surrogate checks of the production guards. No game process or Unity runtime.
using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using StutterFix;
using UnityEngine;

namespace UnityEngine {
    public class Object { public static void Destroy(Object o) {} }
    public class Transform : Object { public Vector3 localPosition; public void SetParent(Transform t,bool b) {} }
    public class GameObject : Object { public Transform transform=new Transform(); public GameObject(string s) {} }
    public struct Color { public float a; public static Color white=>new Color {a=1}; }
    public struct Vector2 {
        public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;}
        public static Vector2 one=>new Vector2(1,1);
        public static Vector2 operator +(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
        public static Vector2 operator /(Vector2 a,Vector2 b)=>new Vector2(a.x/b.x,a.y/b.y);
        public static Vector2 operator /(Vector2 a,float b)=>new Vector2(a.x/b,a.y/b);
        public static Vector2 operator *(float a,Vector2 b)=>new Vector2(a*b.x,a*b.y);
        public static implicit operator Vector3(Vector2 a)=>new Vector3(a.x,a.y,0);
    }
    public struct Vector3 { public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;} }
}
public enum DecPlacementType {Tile,Global,Camera,CameraAspect}
public class scrParallax { public bool clampToScreen; public Vector2 screenRelativePos; }
public class scrDecoration {
    public scrParallax parallax=new scrParallax(); public Transform childTransform=new Transform();
    public Vector2 pivotPosVec,pivotOffsetVec,croppedPivotOffset,textureScaleMultiplier=Vector2.one;
    public float scaleMultiplier=1; public Color color=Color.white; public DecPlacementType placementType;
    public Vector2 trueScaleMultiplier=>scaleMultiplier*textureScaleMultiplier;
    public string name="surrogate"; public int Calls;
    public void SetPosition(Vector2 pos,Vector2 off) {
        Calls++; pivotPosVec=pos; pivotOffsetVec=off;
        childTransform.localPosition=(off+croppedPivotOffset)/trueScaleMultiplier;
        parallax.screenRelativePos=(pos+off)/20f+new Vector2(.5f,.5f);
    }
    public void SetPositionX() {} public void SetPositionY() {} public void UpdateScreenClamp() {}
}
public class scrVisualDecoration : scrDecoration {}
public static class ADOBase {public static object editor;}
namespace HarmonyLib {
    static class AccessTools {internal const BindingFlags all=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;}
    sealed class Info {internal string[] Owners;}
    static class Harmony { internal static string Owner; internal static Info GetPatchInfo(MethodBase m)=>Owner==null?null:new Info {Owners=new[]{Owner}}; }
}
namespace StutterFix {
    static class Main {internal static EntryType Entry=new EntryType(); internal class EntryType {public InfoType Info=new InfoType();public LoggerType Logger=new LoggerType();} internal class InfoType {public string Id="StutterFix";} internal class LoggerType {public void Log(string s) {Console.WriteLine(s);}}}
    static class Hitch {internal static bool Playing=false;}
    static class DecoAnim {internal class Rec {public scrDecoration D;public int Key=1;internal int PosSlot=-1,PosGeneration;}}
    static class MoveApply {
        internal static bool Late=true;
        internal static bool SkipForLate(scrDecoration d)=>Late;
        internal static void RegisterLate(scrDecoration d,HashSet<scrDecoration> set,List<scrDecoration> list){if(set.Add(d))list.Add(d);}
    }
}
class Program {
    static int assertions;
    static void Need(bool v,string message){assertions++;if(!v)throw new Exception(message);}
    static DecoAnim.Rec Fresh(scrDecoration d=null){var r=new DecoAnim.Rec{D=d??new scrVisualDecoration()};PosFast.Bind(r);return r;}
    static void Full(DecoAnim.Rec r){r.D.SetPosition(new Vector2(3,4),r.D.pivotOffsetVec);PosFast.Remember(r);}
    static bool Fast(DecoAnim.Rec r)=>PosFast.Try(r,new Vector2(3,4));
    static void Miss(string name,Action<scrDecoration> change){var r=Fresh();Full(r);Need(Fast(r),name+" baseline");change(r.D);Need(!Fast(r),name+" fallback");}
    static void DumpIL(string path) {
        string dir=Path.GetDirectoryName(path);
        AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{var p=Path.Combine(dir,new AssemblyName(e.Name).Name+".dll");return File.Exists(p)?Assembly.LoadFrom(p):null;};
        var asm=Assembly.LoadFrom(path);var type=asm.GetType("scrDecoration");
        var ops=new Dictionary<short,OpCode>();foreach(var f in typeof(OpCodes).GetFields()){var op=(OpCode)f.GetValue(null);ops[op.Value]=op;}
        Console.WriteLine("DecPlacementType: "+string.Join(", ",Enum.GetNames(asm.GetType("DecPlacementType"))));
        foreach(string name in new[]{"UpdateScreenClamp","get_trueScaleMultiplier","SetTextureScaleMultiplier","SetParallax"}) {
            var owner=name=="SetTextureScaleMultiplier"?asm.GetType("scrVisualDecoration"):type;
            var m=owner.GetMethod(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);Console.WriteLine("== "+m);
            if(m==null)throw new Exception("Missing method: "+name);
            byte[] il=m.GetMethodBody().GetILAsByteArray();int pc=0;
            while(pc<il.Length){int at=pc;short code=il[pc++];if(code==0xfe)code=(short)(0xfe00|il[pc++]);var op=ops[code];string value="";
                int size=op.OperandType switch{OperandType.InlineNone=>0,OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar=>1,OperandType.InlineVar=>2,OperandType.InlineI8 or OperandType.InlineR=>8,OperandType.InlineSwitch=>4+4*BitConverter.ToInt32(il,pc),_=>4};
                if(op.OperandType==OperandType.InlineField||op.OperandType==OperandType.InlineMethod||op.OperandType==OperandType.InlineType) value=m.Module.ResolveMember(BitConverter.ToInt32(il,pc)).ToString();
                else if(size==1)value=il[pc].ToString();else if(size==4)value=BitConverter.ToInt32(il,pc).ToString();
                pc+=size;Console.WriteLine($"{at:x4} {op} {value}");
            }
        }
    }
    static void Main(string[] args){
        if(args.Length>0){DumpIL(args[0]);return;}
        string dir=Path.Combine(Path.GetTempPath(),"posfast-check-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        try {
            PosFast.Configure(dir);Need(!PosFast.Enabled,"default off");Need(!Fast(Fresh()),"off no fast");
            File.WriteAllText(Path.Combine(dir,"posfast-on"),"");PosFast.Configure(dir);Need(PosFast.Enabled,"opt in");
            var r=Fresh();Need(!Fast(r),"first full required");Full(r);Need(Fast(r),"seeded fast");Need(r.D.pivotPosVec.x==3,"position stored");
            Need(!PosFast.Try(r,new Vector2(5,6)),"unconditional screen write must fallback even without clamp");Full(r);
            Miss("pivot offset",d=>d.pivotOffsetVec.x=1);Miss("crop",d=>d.croppedPivotOffset.y=2);
            Miss("scale",d=>d.scaleMultiplier=2);Miss("texture scale",d=>d.textureScaleMultiplier.x=2);
            Miss("signed zero",d=>d.pivotOffsetVec.x=BitConverter.Int32BitsToSingle(unchecked((int)0x80000000)));
            Miss("NaN crop bits",d=>d.croppedPivotOffset.x=BitConverter.Int32BitsToSingle(unchecked((int)0x7fc00001)));
            Miss("placement",d=>d.placementType=DecPlacementType.Global);
            Miss("camera",d=>d.placementType=DecPlacementType.Camera);Miss("aspect",d=>d.placementType=DecPlacementType.CameraAspect);
            Miss("clamp",d=>d.parallax.clampToScreen=true);Miss("child",d=>d.childTransform=new Transform());
            Miss("parallax identity",d=>d.parallax=new scrParallax());Miss("null parallax",d=>d.parallax=null);
            Miss("alpha",d=>d.color=default);Need(!Fast(Fresh(new scrDecoration())),"exact visual type");
            r=Fresh();Full(r);ADOBase.editor=new object();Need(!Fast(r),"editor fallback");ADOBase.editor=null;
            Full(r);MoveApply.Late=false;Need(!Fast(r),"late fallback");MoveApply.Late=true;
            Full(r);r.D.croppedPivotOffset.x=2;Need(!Fast(r),"crop changed");Full(r);Need(Fast(r),"new full reseeds");
            PosFast.Forget();Need(!Fast(r),"stale generation");
            HarmonyLib.Harmony.Owner="foreign";r=Fresh();Full(r);Need(!Fast(r),"foreign patch");
            HarmonyLib.Harmony.Owner=null;PosFast.Forget();r=Fresh();Full(r);
            // Production sampler and rejection logic, using a managed setter oracle.
            PosFast.Command("verify");for(int i=0;i<64;i++)Need(Fast(r),"verify equal");
            Need(PosFast.State().Contains("checked=1 mismatch=0"),"one sample per 64");
            r.D.childTransform.localPosition=new Vector3(99,0,0);
            for(int i=0;i<64;i++)Need(Fast(r),"sample before rejection");
            Need(PosFast.State().Contains("mismatch=1"),"child mismatch detected");Need(!Fast(r),"rejected persists");
            PosFast.Bind(r);Full(r);Need(!Fast(r),"effect rebind does not clear rejection");
            PosFast.Forget();r=Fresh();Full(r);Need(Fast(r),"new level clears rejection");
            PosFast.Uninstall();Need(!PosFast.Enabled&&!Fast(r),"unload disables");
            Console.WriteLine("PASS assertions="+assertions+"; production PosFast.cs linked; managed Unity/Harmony/setter surrogates; no runtime/performance claim");
        } finally {Directory.Delete(dir,true);}
    }
}
