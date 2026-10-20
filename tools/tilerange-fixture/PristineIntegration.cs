// Production T3 coordinator; modeled engine, original FloorAnim and writer installation.
using System;
using System.Collections.Generic;
using System.Linq;
using StutterFix;
using UnityEngine;
namespace UnityEngine
{
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z) { this.x=x;this.y=y;this.z=z; }
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
    }
    public class Transform { public Vector3 position,eulerAngles,localScale=new Vector3(1,1,1); }
    public static class Mathf { public static bool Approximately(float a,float b)=>Math.Abs(a-b)<Math.Max(1e-6f*Math.Max(Math.Abs(a),Math.Abs(b)),float.Epsilon*8); }
    public static class Time { public static int frameCount; }
}
public static class VectorExtensions
{
    public static Vector3 WithX(this Vector3 v,float x)=>new Vector3(x,v.y,v.z);
    public static Vector3 WithY(this Vector3 v,float y)=>new Vector3(v.x,y,v.z);
    public static bool ApproximatelyXY(this Vector3 a,Vector3 b)=>Mathf.Approximately(a.x,b.x)&&Mathf.Approximately(a.y,b.y);
}
namespace HarmonyLib { public class Harmony {} }
namespace DG.Tweening { public class Tween { public bool active; public object id; } }
public enum TweenType { PositionX, PositionY, Rotation, ScaleX, ScaleY, Opacity }
public class FloorRenderer { public object material=new object(); }
public class scrFloor
{
    public object freeroamArea;
    public Transform transform=new Transform();
    public Vector3 startPos,startRot,tweenRot;
    public FloorRenderer floorRenderer=new FloorRenderer();
    public float opacity=1,opacityVal=1;
    public bool dontChangeMySprite;
    public Dictionary<TweenType,DG.Tweening.Tween> moveTweens=new Dictionary<TweenType,DG.Tweening.Tween>();
}
public class scrLevelMaker { public List<scrFloor> listFloors; }
public class ADOBase { public static scrLevelMaker lm; }
namespace StutterFix
{
    internal static class Main
    {
        internal static EntryType Entry=new EntryType();
        internal class EntryType { public LoggerType Logger=new LoggerType(); }
        internal class LoggerType { public List<string> Lines=new List<string>(); public void Log(string s)=>Lines.Add(s); }
    }
    internal static class FloorRangeWriters
    {
        internal static int Installed=53,Failures;
        internal static bool Fail;
        internal static bool Install() { Failures=Fail?1:0; return !Fail; }
        internal static void InstallLifecycle(HarmonyLib.Harmony h) {}
        internal static void Shutdown() {}
    }
    internal static class TileSim { internal static bool Capturing; }
    internal static class FloorAnim
    {
        internal static bool Split,RangePending,CorruptOracle;
        internal static int Calls;
        internal sealed class Rec { public bool Running=true; public float Target=1; }
        internal struct Ctx
        { public Vector3 Pos,Rot,Scale; public bool Zero,OpaOnly,PosUsed,RotUsed,ScaleUsed,OpaUsed; public float Opacity,Dur; }
        internal static void RangeLegacy(scrFloor f,ref Ctx c)
        {
            Calls++; FloorRange.BeforeLegacy(f);
            foreach(var t in f.moveTweens.Values)
                if(t?.id is Rec r&&r.Running) { f.opacity=r.Target; r.Running=false; }
            var tform=f.transform; var p=f.startPos+c.Pos;
            if(c.PosUsed)
            {
                var v=tform.position;
                if(!float.IsNaN(p.x)&&!Mathf.Approximately(v.x,p.x)) v.x=p.x;
                if(!float.IsNaN(p.y)&&!Mathf.Approximately(v.y,p.y)) v.y=p.y;
                tform.position=v;
            }
            if(c.RotUsed&&!Mathf.Approximately(tform.eulerAngles.z,(f.startRot+c.Rot).z)) tform.eulerAngles=f.startRot+c.Rot;
            if(c.ScaleUsed)
            {
                var before=tform.localScale;
                if(!float.IsNaN(c.Scale.x)&&!tform.localScale.ApproximatelyXY(before.WithX(c.Scale.x))) tform.localScale=before.WithX(c.Scale.x);
                if(!float.IsNaN(c.Scale.y)&&!tform.localScale.ApproximatelyXY(before.WithY(c.Scale.y))) tform.localScale=before.WithY(c.Scale.y);
            }
            if(c.OpaUsed&&!Mathf.Approximately(f.opacity,c.Opacity)) { f.opacity=c.Opacity;f.dontChangeMySprite=true; }
            if(CorruptOracle) f.dontChangeMySprite=!f.dontChangeMySprite;
        }
    }
}
class PristineIntegration
{
    static int checks;
    static void Check(bool ok,string reason) { checks++;if(!ok)throw new Exception(reason); }
    static List<scrFloor> Make(int n=2048)
    {
        FloorRange.Shutdown();
        var f=Enumerable.Range(0,n).Select(i=>new scrFloor{startPos=new Vector3(i,2,0),transform=new Transform{position=new Vector3(i,2,0)}}).ToList();
        ADOBase.lm=new scrLevelMaker{listFloors=f}; FloorRange.Command("on"); return f;
    }
    static FloorAnim.Ctx Default()=>new FloorAnim.Ctx{PosUsed=true,RotUsed=true,ScaleUsed=true,OpaUsed=true,Scale=new Vector3(1,1,1),Opacity=1,Dur=1};
    static int Start(List<scrFloor> f,int lo=0,int hi=-1)
    {
        var c=Default(); FloorAnim.Calls=0;
        Check(FloorRange.TryStart(f,lo,hi<0?f.Count-1:hi,1,ref c),"accepted default target"); return FloorAnim.Calls;
    }
    static void Sweep(int n=600) { for(int i=0;i<n;i++){Time.frameCount++;FloorRange.Tick();} }
    static string Status() { FloorRange.Command("status");return StutterFix.Main.Entry.Logger.Lines.Last(); }
    static long Counter(string key)
    { var s=Status();var p=s.IndexOf(key+"=",StringComparison.Ordinal)+key.Length+1;return long.Parse(s.Substring(p).Split(' ')[0]); }
    static void Main()
    {
        foreach(int n in new[]{1000,1001,8171,8933,90000})
        {
            var tiles=Make(n);Check(Start(tiles)==0,"production clean path has zero per-tile legacy calls "+n);
            FloorRange.Command("shadow");int calls=Start(tiles);
            Check(calls==(n+15)/16,"1/16 oracle samples "+n); Check(Counter("mismatch")==0,"clean oracle bits");
        }
        var f=Make();
        var big=Make(90000);long sampled=Counter("safety-samples");Sweep(1);
        Check(Counter("safety-samples")-sampled==176,"90000 safety budget 44 chunks x 4 tiles, no full frame scan");
        f=Make();
        f[64].transform.position=new Vector3(-1,2,0);FloorRange.Mark(f[64],FloorRange.Position);
        Check(Start(f)==64,"only dirty chunk legacy");Check(f[64].transform.position.x==64,"legacy restored position");
        Check(Start(f)==64,"dirty remains until full sweep");Sweep();Check(Start(f)==0,"full uninterrupted sweep renews certificate");
        f[64].opacity=0;FloorRange.Mark(f[64],FloorRange.Opacity);
        var posOnly=Default();posOnly.OpaUsed=false;FloorAnim.Calls=0;
        Check(FloorRange.TryStart(f,0,2047,1,ref posOnly)&&FloorAnim.Calls==0,"unaffected channels skip independently");
        f=Make();f[65].opacity=0;FloorRange.Mark(f[65]);
        Check(Start(f,65,2000)==63,"partial chunk boundary");
        f=Make();var running=new FloorAnim.Rec{Target=0.5f};
        f[100].moveTweens[TweenType.Opacity]=new DG.Tweening.Tween{id=running};FloorRange.Mark(f[100]);
        Check(Start(f)==64&&!running.Running&&f[100].opacity==1,"inactive proxy Running predecessor completed");
        f=Make();f[110].moveTweens[TweenType.Opacity]=new DG.Tweening.Tween{active=true};FloorRange.Mark(f[110]);
        Sweep();Check(Start(f)==64,"active real tween blocks renewal");
        f=Make();long late=Counter("late-changes");f[0].opacity=0;Sweep(1);
        Check(Counter("late-changes")==late+1,"unhooked external write safety detection");Check(Start(f)==64,"late write dirty chunk");
        f=Make();f[0].opacity=0;FloorRange.Mark(f[0]);Sweep(100);
        FloorRange.Mark(f[0]);f[0].opacity=1;Sweep(100);Check(Start(f)==64,"writes invalidate partial certificate");
        Sweep();Check(Start(f)==0,"renewal after restarted full sweep");
        f=Make();var c=Default();c.Pos.x=0.01f;Check(!FloorRange.TryStart(f,0,2047,1,ref c),"nondefault target falls back");
        c=Default();c.Opacity=MathF.BitDecrement(1);Check(!float.IsNaN(c.Opacity)&&Mathf.Approximately(c.Opacity,1),"nearby target");
        FloorAnim.Calls=0;Check(FloorRange.TryStart(f,0,2047,1,ref c)&&FloorAnim.Calls==2048,"opacity Approximately is not transitively trusted");
        f=Make();c=Default();c.Pos.x=float.NaN;c.Scale.y=float.NaN;
        Check(FloorRange.TryStart(f,0,2047,1,ref c),"NaN unused axes");
        FloorAnim.Split=true;Check(!FloorRange.TryStart(f,0,2047,1,ref c),"split rejection");FloorAnim.Split=false;
        FloorAnim.RangePending=true;Check(!FloorRange.TryStart(f,0,2047,1,ref c),"pending jobs rejection");FloorAnim.RangePending=false;
        TileSim.Capturing=true;Check(!FloorRange.TryStart(f,0,2047,1,ref c),"capture rejection");TileSim.Capturing=false;
        Check(!FloorRange.TryStart(f,0,2047,2,ref c),"gap rejection");Check(!FloorRange.TryStart(f,0,998,1,ref c),"999 rejection");
        f=Make();f[0].freeroamArea=new object();FloorRange.Command("on");Check(Start(f)==64,"freeroam fallback");
        f=Make();f[0].floorRenderer.material=null;FloorRange.Command("on");Check(Start(f)==64,"missing material fallback");
        f=Make();f[0].transform=new Transform();Sweep(1);Check(Start(f)==64,"transform identity replacement fence");
        f=Make();ADOBase.lm=new scrLevelMaker{listFloors=new List<scrFloor>()};Sweep(1);c=Default();
        Check(!FloorRange.TryStart(f,0,2047,1,ref c),"map transition invalidates");
        FloorRangeWriters.Fail=true;Make();Check(Status().Contains("ready=False")&&Status().Contains("patch-fail=1"),"any writer failure fails closed");FloorRangeWriters.Fail=false;
        f=Make();FloorRange.Command("shadow");long mismatches=Counter("mismatch");f[0].opacity=0;
        Check(Start(f)==2048,"shadow preflight mismatch processes each tile once");Check(Counter("mismatch")==mismatches+1,"preflight mismatch counter");
        c=Default();Check(!FloorRange.TryStart(f,0,2047,1,ref c),"shadow mismatch latches");
        f=Make();FloorRange.Command("shadow");FloorAnim.CorruptOracle=true;FloorAnim.Calls=0;c=Default();
        Check(FloorRange.TryStart(f,0,2047,1,ref c)&&FloorAnim.Calls==2048,"oracle mismatch no double application");FloorAnim.CorruptOracle=false;
        FloorRange.Command("off");Check(!FloorRange.TryStart(f,0,2047,1,ref c),"off rejection");
        Console.WriteLine($"PASS T3 checks={checks}; production FloorRange; modeled Unity/FloorAnim/writer detours, no game performance claim");
    }
}
