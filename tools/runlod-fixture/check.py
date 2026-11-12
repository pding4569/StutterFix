"""Offline checks of the actual RunLod method bodies; never starts Unity/game."""
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
source = (ROOT / "RunLod.cs").read_text(encoding="utf-8-sig")


def method(name):
    import re
    match = re.search(r" +static [^\n]+\b" + name + r"(?:<T>)?\(", source)
    if not match:
        raise ValueError(name)
    start = match.start()
    body = source.index("{", match.end())
    depth = 1
    end = body + 1
    while depth:
        depth += (source[end] == "{") - (source[end] == "}")
        end += 1
    return source[start:end]


parts = [source[source.index("        static long Key("):source.index("        static Matrix4x4 vp;")],
         source[source.index("        static void DrawLegacyRuns("):source.index("        static void VerifyPlans()")],
         method("CurveMin"), method("CurveExtremum"), method("SizeMin")]
parts.append(method("Readers").replace("typeof(RunLod).Module", "typeof(Program).Module"))
raw = source[source.index("        struct RawCurve"):source.index("        // Unity 6's")]

stub = r'''
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
static class AccessTools {
 public static MethodInfo PropertyGetter(Type t,string name)=>t.GetProperty(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetGetMethod(true);
 public static FieldInfo Field(Type t,string name)=>t.GetField(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
}
static class Mathf {
 public static float Min(float x,float y)=>Math.Min(x,y);
 public static float Max(float x,float y)=>Math.Max(x,y);
 public static float Abs(float x)=>Math.Abs(x);
}
enum WeightedMode { None, In, Out, Both }
enum ParticleSystemCurveMode { Constant, Curve, TwoCurves, TwoConstants }
struct Keyframe { public float time,value,inTangent,outTangent; public WeightedMode weightedMode; }
class GameObject { public int layer; public T AddComponent<T>() where T:new()=>new T(); }
class Renderer {
 public bool isVisible,enabled=true,forceRenderingOff; public GameObject gameObject=new GameObject();
 public T GetComponent<T>() where T:class =>null;
}
class BarrierTag { public bool Vis; }
class Cand { public int Id; }
class Item { public Cand C; public int Q,Lv,Ord; public float X0,Y0,X1,Y1,Mag; }
static class Program {
 struct FakeNativeCurve {
  internal ParticleSystemCurveMode m_Mode;
  internal float m_CurveMultiplier;
  internal IntPtr m_CurveMin,m_CurveMax;
  internal float m_ConstantMin,m_ConstantMax;
 }
 struct FakeModule {
  public FakeNativeCurve Value;
  private FakeNativeCurve startSizeBlittable=>Value;
 }
 static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
 static class ParticleCurves {
  internal static readonly Dictionary<IntPtr,Keyframe[]> Curves=new Dictionary<IntPtr,Keyframe[]>();
  internal static int Length(IntPtr p)=>Curves[p].Length;
  internal static void Key(IntPtr p,int i,out Keyframe k)=>k=Curves[p][i];
 }
 static long[] barrierKey=Array.Empty<long>(); static Renderer[] barrierByKey=Array.Empty<Renderer>();
 static BarrierTag[] barrierTag=Array.Empty<BarrierTag>();
 static int[] barrierStamp=Array.Empty<int>(),emptyBlockStamp=Array.Empty<int>();
 static bool[] barrierValue=Array.Empty<bool>();
 const int BarrierBlock=32; static int queryStamp=1, BarrierChecks,tagsThisFrame,TagsPerFrame=300;
 static List<Item> items=new List<Item>();
 static int MinRun=2; static float MinSave=1, savedNow,rx0,ry0,rx1,ry1,lastSave; static int runDiv=4;
 static readonly List<RunPlan> drawn=new List<RunPlan>();
 static void Restore(Cand c){}
 static void Emit(int start,int count,int idx,int w,int h) {
  drawn.Add(new RunPlan { Start=start, Count=count, Div=runDiv, X0=rx0,Y0=ry0,X1=rx1,Y1=ry1,Save=savedNow-lastSave });
  lastSave=savedNow;
 }
 static void Require(bool ok,string why){if(!ok)throw new Exception(why);}
 static bool Equal(RunPlan a,RunPlan b)=>a.Start==b.Start&&a.Count==b.Count&&a.Div==b.Div&&a.X0==b.X0&&a.Y0==b.Y0&&a.X1==b.X1&&a.Y1==b.Y1;
 static void Compare(List<RunPlan> a,List<RunPlan>b) {
  Require(a.Count==b.Count,"run count");
  for(int i=0;i<a.Count;i++)Require(Equal(a[i],b[i]),"member IDs/rect/div");
 }
 static void Setup(Random rng,int n) {
  barrierKey=new long[n];barrierByKey=new Renderer[n];barrierTag=new BarrierTag[n];
  barrierStamp=new int[n];barrierValue=new bool[n];emptyBlockStamp=new int[(n+31)/32];
  for(int i=0;i<n;i++) {
   barrierKey[i]=Key(3000,rng.Next(-2,3),rng.Next(-15,16));
   var r=new Renderer { isVisible=rng.Next(3)==0,enabled=rng.Next(5)!=0,forceRenderingOff=rng.Next(8)==0 };
   r.gameObject.layer=rng.Next(3);barrierByKey[i]=rng.Next(30)==0?null:r;
  }
  Array.Sort(barrierKey,barrierByKey);
  for(int i=0;i<n;i++)if(barrierByKey[i]!=null && rng.Next(4)!=0)barrierTag[i]=new BarrierTag {Vis=barrierByKey[i].isVisible};
 }
 static void Main() {
  var rng=new Random(123456);int queries=0,frames=0;
  for(int scene=0;scene<150;scene++) {
   Setup(rng,rng.Next(0,400));
   for(int frame=0;frame<8;frame++) {
    queryStamp++;tagsThisFrame=0;
    for(int j=0;j<barrierByKey.Length;j++)if(barrierByKey[j]!=null) {
     var r=barrierByKey[j];r.isVisible=rng.Next(4)==0;r.enabled=rng.Next(4)!=0;r.gameObject.layer=rng.Next(3);
     if(barrierTag[j]!=null)barrierTag[j].Vis=r.isVisible;
    }
    int mask=rng.Next(1,8);
    for(int q=0;q<150;q++) {
     long a=Key(3000,rng.Next(-3,4),rng.Next(-20,21)),b=Key(3000,rng.Next(-3,4),rng.Next(-20,21));
     if(a>b)(a,b)=(b,a);
     Require(BarrierBetween(a,b,mask)==BarrierBetweenFast(a,b,mask),"range visibility");queries++;
    }
    items.Clear();var original=new List<Cand>();
    for(int i=0;i<150;i++) {
     var c=rng.Next(12)==0?null:new Cand{Id=i};original.Add(c);
     items.Add(new Item{C=c,Q=3000,Lv=-2+i/30,Ord=-15+i%30,X0=0,Y0=0,X1=1,Y1=1,Mag=rng.Next(2,8)});
    }
    MinSave=(frame%3==0)?-1:1;MinRun=(frame%4)+1;
    BuildRuns(false,mask,legacyRuns);BuildRuns(true,mask,fastRuns);Compare(legacyRuns,fastRuns);
    drawn.Clear();savedNow=lastSave=0;int ci=0;DrawLegacyRuns(mask,3440,1440,ref ci);Compare(drawn,fastRuns);
    for(int i=0;i<items.Count;i++)items[i].C=original[i];
    frames++;
   }
  }
  // Repeated wide empty ranges must examine each barrier only once per callback.
  Setup(rng,1024);for(int i=0;i<1024;i++){barrierByKey[i]=new Renderer();barrierTag[i]=new BarrierTag();}
  queryStamp++;BarrierChecks=0;
  for(int i=0;i<1000;i++)Require(!BarrierBetweenFast(long.MinValue,long.MaxValue,7),"empty block");
  Require(BarrierChecks==1024,"memoization count");
  long before=GC.GetAllocatedBytesForCurrentThread();
  for(int i=0;i<1000;i++)BuildRuns(true,7,fastRuns);
  long allocation=GC.GetAllocatedBytesForCurrentThread()-before;
  Require(allocation==0,"steady planning allocation="+allocation);
  var ptr=new IntPtr(1);
  ParticleCurves.Curves[ptr]=new[]{new Keyframe{time=0,value=1,outTangent=-4},new Keyframe{time=1,value=1,inTangent=4}};
  float dip=CurveMin(ptr);Require(dip<=0&&dip>=-0.00001f,"interior cubic minimum");
  ParticleCurves.Curves[ptr]=new[]{new Keyframe{time=0,value=2},new Keyframe{time=1,value=4}};
  Require(Math.Abs(CurveMin(ptr)-2)<0.00001f,"linear minimum");
  ParticleCurves.Curves[ptr][0].weightedMode=WeightedMode.Both;
  Require(float.IsNaN(CurveMin(ptr)),"weighted fail closed");
  Require(SizeMin(new RawCurve{m_Mode=ParticleSystemCurveMode.Constant,m_ConstantMax=5})==5,"constant");
  Require(SizeMin(new RawCurve{m_Mode=ParticleSystemCurveMode.TwoConstants,m_ConstantMin=3,m_ConstantMax=8})==3,"random constant");
  Require(float.IsNaN(SizeMin(new RawCurve{m_Mode=ParticleSystemCurveMode.Constant,m_ConstantMax=-1})),"negative fail closed");
  var read=Readers<FakeModule>("startSize")[0];
  var module=new FakeModule{Value=new FakeNativeCurve{m_Mode=ParticleSystemCurveMode.TwoConstants,m_CurveMultiplier=2,m_CurveMin=new IntPtr(12),m_CurveMax=new IntPtr(34),m_ConstantMin=3,m_ConstantMax=7}};
  var raw=read(module);
  Require(raw.m_Mode==module.Value.m_Mode&&raw.m_CurveMultiplier==2&&raw.m_CurveMin==new IntPtr(12)&&raw.m_CurveMax==new IntPtr(34)&&raw.m_ConstantMin==3&&raw.m_ConstantMax==7,"IL field copier");
  before=GC.GetAllocatedBytesForCurrentThread();
  for(int i=0;i<10000;i++){raw=read(module);Require(raw.m_ConstantMin==3,"IL read");}
  Require(GC.GetAllocatedBytesForCurrentThread()==before,"IL reader allocation");
  Console.WriteLine($"PASS: {queries} range queries; {frames} old Flush/new plan frame comparisons; 1024 checks for 1000 empty queries; steady managed planning allocation={allocation}; curve extrema and exclusions; IL copier fields and allocation=0");
 }
'''
folder = ROOT / "out/runlod-f-fixture"
folder.mkdir(parents=True, exist_ok=True)
(folder / "Program.cs").write_text(stub + raw + "\n".join(parts) + "\n}", encoding="utf-8")
(folder / "Fixture.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><EnableDefaultCompileItems>true</EnableDefaultCompileItems><NoWarn>0649</NoWarn></PropertyGroup></Project>', encoding="utf-8")
subprocess.run(["dotnet", "run", "--project", str(folder / "Fixture.csproj"), "-c", "Release"], check=True, cwd=ROOT)
