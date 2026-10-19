// Production ColorFast linked against managed surrogates, never the game/Unity runtime.
using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using StutterFix;
using UnityEngine;

namespace UnityEngine
{
    public class Object { public static void Destroy(Object o) {} }
    public class GameObject : Object { public GameObject(string s) {} public void SetActive(bool b) {} public T AddComponent<T>() where T:new() => new T(); }
    public struct Color { public float r,g,b,a; public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;} public static Color white=>new Color(1,1,1,1); }
    public class SpriteRenderer : Object
    {
        private Color c=Color.white; public int Writes,Reads; public bool Corrupt;
        public Color color { get { Reads++; return Corrupt ? new Color(9,9,9,9) : c; } set { Writes++; c=value; } }
        public bool enabled=true,forceRenderingOff;
    }
}
public static class ExtensionMethods { public static Color WithAlpha(this Color c,float a) { c.a=a;return c; } }
public class scrDecoration
{
    public Color color=Color.white,rendererColor=Color.white; public float opacity=1f; public bool rendererEnabled=true,stickToFloor;
    public void SetColor(Color c){color=c;ApplyColor();} public void SetOpacity(float o){opacity=o;ApplyColor();}
    protected virtual void ApplyColor(){}
}
public class scrVisualDecoration : scrDecoration
{
    public SpriteRenderer spriteRenderer=new SpriteRenderer();
    protected override void ApplyColor(){rendererColor=color.WithAlpha(color.a*opacity*1f);spriteRenderer.color=rendererColor;}
    public float GetAlpha()=>rendererColor.a; public bool GetVisible()=>rendererEnabled&&GetAlpha()!=0f;
    public void SetVisible(bool v){rendererEnabled=v;}
}
public class DerivedVisual : scrVisualDecoration {}
public static class ADOBase {public static object customLevel,editor;}
namespace StutterFix
{
    static class Hitch {internal static bool Playing;}
    static class InvisibleSkip {internal static bool Enabled=true;public static void After() {}}
    static class Dormancy {internal static bool Enabled=true;public static void WakePost() {}}
    static class Precheck {internal static int Active;}
    static class FrameParts {public static void SetPre(){} public static void SetPost(){}}
}
namespace HarmonyLib
{
    public class Patch {public string owner="foreign"; public MethodInfo PatchMethod=typeof(Program).GetMethod(nameof(Program.Foreign));}
    public class Patches {public List<Patch> Prefixes=new(),Postfixes=new(),Transpilers=new(),Finalizers=new();}
    public static class Harmony {public static Patches Inject;public static Patches GetPatchInfo(MethodBase m)=>Inject;}
    public static class AccessTools
    {
        public delegate ref F FieldRef<T,F>(T value);
        public static FieldRef<T,F> FieldRefAccess<T,F>(string name)
        {
            var dm=new DynamicMethod("field",typeof(F).MakeByRefType(),new[]{typeof(T)},typeof(AccessTools).Module,true);
            var il=dm.GetILGenerator();il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldflda,typeof(T).GetField(name));il.Emit(OpCodes.Ret);
            return (FieldRef<T,F>)dm.CreateDelegate(typeof(FieldRef<T,F>));
        }
        public static MethodInfo DeclaredMethod(Type t,string n)=>t.GetMethod(n,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly);
        public static MethodInfo Method(Type t,string n,Type[] args)=>t.GetMethod(n,args);
        public static D MethodDelegate<D>(MethodInfo m) where D:Delegate => (D)m.CreateDelegate(typeof(D));
    }
}
public class Program
{
    static int assertions;
    static void Need(bool v,string s){assertions++;if(!v)throw new Exception(s);}
    public static void Foreign(){}
    static void Writers(string path)
    {
        using var stream=File.OpenRead(path);using var pe=new PEReader(stream);var md=pe.GetMetadataReader();
        var ops=new Dictionary<short,OpCode>();foreach(var f in typeof(OpCodes).GetFields()){var op=(OpCode)f.GetValue(null);ops[op.Value]=op;}
        foreach(var th in md.TypeDefinitions)
        foreach(var mh in md.GetTypeDefinition(th).GetMethods())
        {
            var method=md.GetMethodDefinition(mh);if(method.RelativeVirtualAddress==0)continue;
            var il=pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes();int pc=0;
            while(pc<il.Length)
            {
                int at=pc;short code=il[pc++];if(code==0xfe)code=(short)(0xfe00|il[pc++]);var op=ops[code];
                int size=op.OperandType switch{OperandType.InlineNone=>0,OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar=>1,OperandType.InlineVar=>2,OperandType.InlineI8 or OperandType.InlineR=>8,OperandType.InlineSwitch=>4+4*BitConverter.ToInt32(il,pc),_=>4};
                if(op==OpCodes.Stfld||op==OpCodes.Ldflda)
                {
                    var handle=MetadataTokens.EntityHandle(BitConverter.ToInt32(il,pc));string name="",owner="";
                    if(handle.Kind==HandleKind.FieldDefinition){var field=md.GetFieldDefinition((FieldDefinitionHandle)handle);name=md.GetString(field.Name);owner=md.GetString(md.GetTypeDefinition(field.GetDeclaringType()).Name);}
                    if(owner=="scrDecoration"&&(name=="color"||name=="opacity"||name=="rendererColor"||name=="rendererEnabled"))
                        Console.WriteLine(md.GetString(md.GetTypeDefinition(th).Name)+"."+md.GetString(method.Name)+" "+at.ToString("X4")+" "+op.Name+" "+name);
                }
                pc+=size;
            }
        }
    }
    static bool Bit(float a,float b)=>BitConverter.SingleToInt32Bits(a)==BitConverter.SingleToInt32Bits(b);
    static bool Bit(Color a,Color b)=>Bit(a.r,b.r)&&Bit(a.g,b.g)&&Bit(a.b,b.b)&&Bit(a.a,b.a);
    static void Miss(string label,Action<scrVisualDecoration> change,Color? c=null)
    {
        var d=new scrVisualDecoration();change(d);var old=d.color;float o=d.opacity;var rc=d.rendererColor;int writes=d.spriteRenderer?.Writes??0;
        Need(!ColorFast.TryColor(d,c??new Color(.2f,.3f,.4f,1)),label);
        Need(Bit(old,d.color)&&Bit(o,d.opacity)&&Bit(rc,d.rendererColor)&&(d.spriteRenderer?.Writes??0)==writes,label+" untouched");
    }
    static void DumpIL(string path)
    {
        string dir=Path.GetDirectoryName(path);
        AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{var p=Path.Combine(dir,new AssemblyName(e.Name).Name+".dll");return File.Exists(p)?Assembly.LoadFrom(p):null;};
        var asm=Assembly.LoadFrom(path); Console.WriteLine("MVID="+asm.ManifestModule.ModuleVersionId);
        var ops=new Dictionary<short,OpCode>();foreach(var f in typeof(OpCodes).GetFields()){var op=(OpCode)f.GetValue(null);ops[op.Value]=op;}
        foreach(var spec in new[]{("scrDecoration","SetColor"),("scrDecoration","SetOpacity"),("scrVisualDecoration","ApplyColor"),
            ("scrVisualDecoration","GetAlpha"),("scrVisualDecoration","GetVisible"),("scrVisualDecoration","SetVisible"),
            ("scrDecoration","UpdatePosition"),("scrParallax","SetTrans"),("scrDecoration","SetRotation"),("scrDecoration","SetScale"),("ExtensionMethods","WithAlpha")})
        {
            var type=asm.GetType(spec.Item1);
            var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly;
            var m=spec.Item2=="WithAlpha" ? Array.Find(type.GetMethods(flags),x=>x.Name=="WithAlpha"&&x.GetParameters()[0].ParameterType.FullName=="UnityEngine.Color") : type.GetMethod(spec.Item2,flags);
            Console.WriteLine("== "+spec.Item1+"."+spec.Item2);
            if(m==null)throw new Exception("missing "+spec);
            byte[] il=m.GetMethodBody().GetILAsByteArray();int pc=0;
            while(pc<il.Length)
            {
                int at=pc;short code=il[pc++];if(code==0xfe)code=(short)(0xfe00|il[pc++]);var op=ops[code];string value="";
                int size=op.OperandType switch{OperandType.InlineNone=>0,OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar=>1,OperandType.InlineVar=>2,OperandType.InlineI8 or OperandType.InlineR=>8,OperandType.InlineSwitch=>4+4*BitConverter.ToInt32(il,pc),_=>4};
                if(op.OperandType==OperandType.InlineField||op.OperandType==OperandType.InlineMethod||op.OperandType==OperandType.InlineType)
                {var member=m.Module.ResolveMember(BitConverter.ToInt32(il,pc));value=member.DeclaringType?.Name+"::"+member;}
                else if(op.OperandType==OperandType.ShortInlineR)value=BitConverter.ToSingle(il,pc).ToString("R");
                else if(size>0)value=BitConverter.ToString(il,pc,size);
                Console.WriteLine(at.ToString("X4")+" "+op.Name+" "+value);pc+=size;
            }
        }
    }
    public static void Main(string[] args)
    {
        if(args.Length==2&&args[0]=="--il"){DumpIL(args[1]);return;}
        if(args.Length==2&&args[0]=="--writers"){Writers(args[1]);return;}
        string dir=Path.Combine(Path.GetTempPath(),"colorfast-check-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        try
        {
            ColorFast.Configure(dir);Need(!ColorFast.Enabled,"default off");Miss("off",d=>{});
            File.WriteAllText(Path.Combine(dir,"colorfast-on"),"");ColorFast.Configure(dir);Need(ColorFast.Enabled,"opt in");
            File.Delete(Path.Combine(dir,"colorfast-on"));Need(ColorFast.Enabled,"flag read once");File.WriteAllText(Path.Combine(dir,"colorfast-on"),"");
            Need(!ColorFast.TryColor(new scrDecoration(),Color.white),"base type");Need(!ColorFast.TryColor(new DerivedVisual(),Color.white),"derived type");Need(!ColorFast.TryColor(null,Color.white),"null");
            Miss("stick",d=>d.stickToFloor=true);Miss("renderer disabled",d=>d.rendererEnabled=false);
            foreach(float a in new[]{0f,-0f,-1f,float.NaN})
            {Miss("old alpha",d=>d.rendererColor=new Color(1,1,1,a));Miss("new alpha",d=>{},new Color(1,1,1,a));}
            Miss("missing renderer",d=>d.spriteRenderer=null);Miss("force off",d=>d.spriteRenderer.forceRenderingOff=true);
            Precheck.Active=1;Miss("precheck",d=>{});Precheck.Active=0;
            Dormancy.Enabled=false;Miss("dormancy off",d=>{});Dormancy.Enabled=true;
            InvisibleSkip.Enabled=false;Miss("invisible off",d=>{});InvisibleSkip.Enabled=true;
            var rand=new Random(73);int accepted=0;
            ColorFast.Command("verify");
            for(int i=0;i<8192;i++)
            {
                float raw()=>BitConverter.Int32BitsToSingle((int)rand.NextInt64(int.MinValue,(long)int.MaxValue+1));
                Color c=new Color(raw(),raw(),raw(),.1f+(i%97)*.01f);float o=.1f+(i%89)*.01f;
                var d=new scrVisualDecoration();d.spriteRenderer.enabled=(i&1)==0;
                var original=new scrVisualDecoration();original.spriteRenderer.enabled=d.spriteRenderer.enabled;
                Need(ColorFast.TryColor(d,c),"random color accepted");accepted++;original.SetColor(c);
                Need(Bit(d.color,original.color)&&Bit(d.rendererColor,original.rendererColor)&&Bit(d.spriteRenderer.color,original.spriteRenderer.color),"color differential");
                Need(ColorFast.TryOpacity(d,o),"random opacity accepted");accepted++;original.SetOpacity(o);
                Need(Bit(d.opacity,original.opacity)&&Bit(d.rendererColor,original.rendererColor)&&Bit(d.spriteRenderer.color,original.spriteRenderer.color),"opacity differential");
                Need(d.spriteRenderer.Writes==2,"exactly one native write per call");
            }
            Need(ColorFast.State().Contains("mismatch=0"),"pure verification");Need(ColorFast.State().Contains("checked=512"),"1/32 sample rate");
            // Force a native getter divergence on the next verification sample: fail closed.
            var bad=new scrVisualDecoration();bad.spriteRenderer.Corrupt=true;
            for(int i=0;i<32;i++)ColorFast.TryColor(bad,Color.white);
            Need(ColorFast.State().Contains("mismatch=1"),"mismatch detected");Miss("sticky failure",d=>{});
            ColorFast.Configure(dir);Need(ColorFast.TryColor(new scrVisualDecoration(),Color.white),"reload clears failure");
            HarmonyLib.Harmony.Inject=new HarmonyLib.Patches();HarmonyLib.Harmony.Inject.Prefixes.Add(new HarmonyLib.Patch());
            ColorFast.Configure(dir);Miss("foreign prefix",d=>{});
            HarmonyLib.Harmony.Inject=new HarmonyLib.Patches();HarmonyLib.Harmony.Inject.Transpilers.Add(new HarmonyLib.Patch());
            ColorFast.Configure(dir);Miss("transpiler",d=>{});
            HarmonyLib.Harmony.Inject=null;ColorFast.Configure(dir);ColorFast.Uninstall();Miss("unload",d=>{});
            Console.WriteLine("PASS assertions="+assertions+" accepted="+accepted+" sampled=512; production ColorFast.cs linked; managed surrogates, no Unity/Mono/performance claim");
        }
        finally {Directory.Delete(dir,true);}
    }
}
