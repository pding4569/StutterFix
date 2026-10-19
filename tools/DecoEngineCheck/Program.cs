// Control-flow/ownership surrogate. It cannot validate Unity Mono float rounding,
// actual Transform internal calls, Harmony order, or any real game performance.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using StutterFix;
using UnityEngine;

namespace UnityEngine
{
    class Object { public static void Destroy(Object o) {} }
    class Transform : Object {
        private Vector3 local;
        public bool ThrowRead;
        public Vector3 localPosition { get {if(ThrowRead)throw new InvalidOperationException("destroyed child");return local;} set {local=value;} }
        public Vector3 position, localScale; public Quaternion rotation; public void SetParent(Transform t, bool b) {}
    }
    class GameObject : Object { public Transform transform = new Transform(); public GameObject(string n) {} }
    struct Vector2
    {
        public float x,y; public Vector2(float x,float y) {this.x=x; this.y=y;}
        public static Vector2 one => new Vector2(1,1); public static Vector2 zero => new Vector2();
        public static Vector2 operator +(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
        public static Vector2 operator *(Vector2 a,Vector2 b)=>new Vector2(a.x*b.x,a.y*b.y);
        public static Vector2 operator *(float a,Vector2 b)=>new Vector2(a*b.x,a*b.y);
        public static Vector2 operator /(Vector2 a,Vector2 b)=>new Vector2(a.x/b.x,a.y/b.y);
        public static implicit operator Vector3(Vector2 a)=>new Vector3(a.x,a.y,0);
        public static implicit operator Vector2(Vector3 a)=>new Vector2(a.x,a.y);
    }
    struct Vector3
    {
        public float x,y,z; public Vector3(float x,float y,float z) {this.x=x;this.y=y;this.z=z;}
        public static Vector3 one=>new Vector3(1,1,1);
        public static Vector3 operator -(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
    }
    struct Quaternion { public float x,y,z,w; public static Quaternion Euler(float x,float y,float z)=>new Quaternion {z=z,w=1}; }
    struct Color {public float r,g,b,a;}
    static class Time { public static int frameCount; }
}
enum DecPlacementType { Global,Tile,Camera,CameraAspect }
class scrParallax { public bool clampToScreen; public float multiplier_x,multiplier_y; }
class scrDecoration
{
    public Transform childTransform=new Transform(),pivotTrans=new Transform(); public scrParallax parallax=new scrParallax();
    public Vector2 pivotPosVec,pivotOffsetVec,croppedPivotOffset,scaleVec=Vector2.one,textureScaleMultiplier=Vector2.one,parallaxOffset;
    public float rotAngle,scaleMultiplier=1,startRot;
    public Vector2 trueScaleMultiplier=>scaleMultiplier*textureScaleMultiplier;
    public DecPlacementType placementType;
    public string name="surrogate";
}
class scrVisualDecoration : scrDecoration {}
static class ADOBase { public static object editor; }
namespace DG.Tweening { enum Ease { Linear,OutCubic } class Tween {} }
namespace HarmonyLib
{
    static class AccessTools { public const BindingFlags all=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance; }
    class PatchInfo { public string[] Owners=Array.Empty<string>(); }
    static class Harmony { public static PatchInfo GetPatchInfo(MethodBase m)=>null; }
}
namespace StutterFix
{
    static class Main { public static Entry Entry=new Entry(); }
    class Entry { public Logger Logger=new Logger(); public Info Info=new Info(); }
    class Info {public string Id="StutterFix";}
    class Logger {public void Log(string s)=>Console.WriteLine(s);}
    static class Hitch { public static bool Playing; }
    static class ZeroTween
    {
        internal static int ThrowAfter;
        public static float Eval(DG.Tweening.Ease e,float p,float d,float o,float period)
        { if(ThrowAfter>0 && --ThrowAfter==0)throw new InvalidOperationException("injected Eval"); if(e==DG.Tweening.Ease.Linear)return p/d; float t=p/d-1f; return t*t*t+1f; }
    }
    static class TightLate
    {
        internal class Entry {internal scrDecoration D; internal Transform Pivot; internal DecoEngine.State Engine; internal bool Rejected,EngineRejected;}
        internal static long writes,skipPos,skipRot,skipScale;
        internal static int Reason;
        private static readonly Dictionary<scrDecoration,Entry> entries=new Dictionary<scrDecoration,Entry>();
        internal static Entry EngineEntry(scrDecoration d)
        { if(!entries.TryGetValue(d,out var e))entries[d]=e=new Entry{D=d,Pivot=d.pivotTrans};return Reason==0&&!e.EngineRejected?e:null; }
        internal static int EngineReason(Entry e)=>Reason;
        internal static Entry EngineBenchEntry(scrDecoration d)=>EngineEntry(d);
        internal static int EngineBenchReason(Entry e,Transform t)=>Reason;
        internal static void EngineApply(Entry e,Vector3 cam,Vector2 pos,float angle,Vector2 scale)
        {e.Pivot.position=pos;e.Pivot.rotation=Quaternion.Euler(0,0,angle);e.Pivot.localScale=scale;}
    }
    internal static partial class DecoAnim
    {
        internal sealed class Rec
        {
            internal DecoEngine.State Engine;
            internal long EngineOrder;
            internal int EngineSkipFrame=-1;
            public scrDecoration D; public int Key,BatchSlot=-1;
            public float Pos,Dur,FStart,FEnd,FChange,FLast,Over,Period;
            public bool Running=true,Started=true,Stepped,InList=true;
            public Vector2 VStart,VEnd,VChange,VLast;
            public Color CStart,CEnd,CChange;
            public DG.Tweening.Tween Shadow;
            public DG.Tweening.Ease E;
        }
        private static readonly List<Rec> recs=new List<Rec>();
        private static bool fastDirty,batchOn,GroupEnabled;
        internal static bool Active=true;
        internal static long Steps,Completed;
        internal static void DropAll(){DecoEngine.RestoreAll();foreach(var r in recs){r.Running=false;r.InList=false;} recs.Clear();DecoEngine.Forget();}
    }
    static class NativeMotion {internal static bool Enabled;}
}
static class Program
{
    static int assertions;
    static void Assert(bool condition,string message) {assertions++;if(!condition)throw new Exception(message);}
    static bool Same(float a,float b)=>BitConverter.SingleToInt32Bits(a)==BitConverter.SingleToInt32Bits(b);
    static DecoAnim.Rec Make(scrDecoration d,int key,float duration=2)
    {
        var r=new DecoAnim.Rec {D=d,Key=key,Dur=duration,EngineOrder=DecoEngine.NextOrder(),FStart=2,FEnd=9,FChange=7,
            VStart=new Vector2(1,1),VEnd=new Vector2(4,5),VChange=new Vector2(3,4),E=DG.Tweening.Ease.OutCubic};
        DecoAnim.EngineRecords.Add(r);DecoEngine.Dirty=true;return r;
    }
    static void Reset(){DecoAnim.DropAll();TightLate.Reason=0;ADOBase.editor=null;}
    static void Main()
    {
        string path=Path.Combine(Path.GetTempPath(),"sf-decoengine-check-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"decoengine-on"),"");
        try
        {
            DecoEngine.Configure(path);Assert(DecoEngine.Enabled,"flag");
            var d=new scrVisualDecoration();var x=Make(d,1);var y=Make(d,2);var rot=Make(d,5);var sx=Make(d,7);var sy=Make(d,8);
            DecoEngine.Admit();Assert(DecoEngine.Count==1 && DecoAnim.EngineRecords.Count==0,"exclusive ownership");
            DecoEngine.Command("verify");
            DecoEngine.Update(.5f);
            Assert(Same(d.pivotPosVec.x,6.046875f) && Same(d.rotAngle,6.046875f),"scalar progression");
            Assert(Same(d.scaleVec.x,2.734375f) && Same(d.scaleVec.y,3.3125f),"scale axes");
            var list=new List<scrDecoration>();DecoEngine.Collect(list,new HashSet<scrDecoration>());Assert(list.Count==1,"collect");
            DecoEngine.ReleaseRecord(x);Assert(DecoEngine.Count==0 && DecoAnim.EngineRecords.Count==5,"release all channels");
            for(int i=1;i<5;i++)Assert(DecoAnim.EngineRecords[i-1].EngineOrder<DecoAnim.EngineRecords[i].EngineOrder,"restored order");
            Reset();d=new scrVisualDecoration();x=Make(d,1);Make(d,9);DecoEngine.Admit();Assert(DecoEngine.Count==0,"mixed color excluded");
            Reset();d=new scrVisualDecoration();x=Make(d,1);var color=Make(d,9);color.CStart.a=color.CEnd.a=1;DecoEngine.Admit();
            Assert(DecoEngine.Count==1&&DecoAnim.EngineRecords.Count==1&&ReferenceEquals(DecoAnim.EngineRecords[0],color),"RGB-only color coexists in original loop");
            Reset();d=new scrVisualDecoration();x=Make(d,1);x.Shadow=new DG.Tweening.Tween();DecoEngine.Admit();Assert(DecoEngine.Count==0,"shadow excluded");
            Reset();d=new scrVisualDecoration();x=Make(d,1);x.Started=false;DecoEngine.Admit();Assert(DecoEngine.Count==0,"startup must precede admission");
            Reset();d=new scrVisualDecoration();x=Make(d,1,.1f);DecoEngine.Admit();long completed=DecoAnim.Completed;DecoEngine.Update(.2f);
            Assert(!x.Running && !x.InList && x.Engine==null && DecoEngine.Count==0,"completion detaches");
            Assert(Same(d.pivotPosVec.x,9f)&&DecoAnim.Completed==completed+1,"completion exact FEnd");
            Reset();d=new scrVisualDecoration();x=Make(d,7,.1f);DecoEngine.Admit();DecoEngine.Update(.2f);Assert(Same(d.scaleVec.x,4f),"scale completes using plugin value");
            Reset();d=new scrVisualDecoration();x=Make(d,1);DecoEngine.Admit();TightLate.Reason=3;DecoEngine.Update(.5f);
            Assert(DecoAnim.EngineRecords.Count==1&&Same(x.Pos,0f),"eligibility exit does not advance twice");
            Reset();d=new scrVisualDecoration();x=Make(d,1);DecoEngine.Admit();d.pivotOffsetVec=new Vector2(1,0);DecoEngine.Update(.5f);
            Assert(DecoEngine.Count==0&&Same(x.Pos,0f),"offset mutation returns original");
            Reset();d=new scrVisualDecoration();x=Make(d,1);DecoEngine.Admit();d.childTransform.localPosition=new Vector3(10,0,0);DecoEngine.Update(.5f);
            Assert(DecoEngine.Count==0&&Same(x.Pos,0f),"external child mutation returns original");
            Reset();d=new scrVisualDecoration();x=Make(d,1);DecoEngine.Admit();d.childTransform.ThrowRead=true;DecoEngine.Update(.5f);
            Assert(DecoEngine.Count==0&&Same(x.Pos,0f),"native guard exception is decoration-local");
            Assert(DecoAnim.EngineRecords.Count==1&&x.Engine==null,"native guard exception restores ownership");
            Reset();DecoEngine.Command("verify off");Time.frameCount=99;d=new scrVisualDecoration();
            x=Make(d,1,2);y=Make(d,2,3);rot=Make(d,5,4);DecoEngine.Admit();ZeroTween.ThrowAfter=2;
            DecoEngine.Update(.5f);
            Assert(DecoEngine.Count==0&&DecoAnim.EngineRecords.Count==3,"exception releases whole decoration");
            Assert(x.EngineSkipFrame==99&&y.EngineSkipFrame!=99&&rot.EngineSkipFrame!=99,"only advanced records suppress duplicate frame");
            Assert(Same(x.Pos,.5f)&&Same(y.Pos,0)&&Same(rot.Pos,0),"partial advance clocks preserved");
            DecoEngine.Dirty=true;DecoEngine.Admit();Assert(DecoEngine.Count==0,"exception decoration stays excluded");
            DecoEngine.Command("verify");
            Reset();d=new scrVisualDecoration();x=Make(d,1);DecoEngine.Admit();d.pivotPosVec=new Vector2(100,200);DecoEngine.Update(.1f);
            Assert(Same(d.pivotPosVec.y,200),"unowned axis preserves intervening effect");
            Reset();d=new scrVisualDecoration();x=Make(d,1);DecoEngine.Admit();DecoEngine.DropLevel();Assert(!x.Running&&DecoEngine.Count==0&&DecoAnim.EngineRecords.Count==0,"level boundary drops");
            // Randomized independent clocks/eases and deliberately reversed scale order.
            var rng=new Random(19);
            for(int run=0;run<128;run++)
            {
                Reset();d=new scrVisualDecoration();int[] keys={8,1,7,5,2};var rs=new DecoAnim.Rec[5];
                float[] clock=new float[5];bool[] running={true,true,true,true,true};Vector2 pos=default,scale=Vector2.one;float angle=0;
                for(int i=0;i<5;i++){rs[i]=Make(d,keys[i],.1f+(float)rng.NextDouble()*3);rs[i].E=i%2==0?DG.Tweening.Ease.Linear:DG.Tweening.Ease.OutCubic;}
                DecoEngine.Admit();
                for(int frame=0;frame<400;frame++)
                {
                    float td=frame%3==0?1f/120:1f/75;
                    for(int i=0;i<5;i++)if(running[i])
                    {
                        var r=rs[i];clock[i]+=td;bool done=clock[i]>=r.Dur;if(done)clock[i]=r.Dur;
                        float e=ZeroTween.Eval(r.E,clock[i],r.Dur,r.Over,r.Period);
                        if(r.Key==7)scale.x=r.VStart.x+r.VChange.x*e;
                        else if(r.Key==8)scale.y=r.VStart.y+r.VChange.y*e;
                        else {float value=done?r.FEnd:r.FStart+r.FChange*e;if(r.Key==1)pos.x=value;else if(r.Key==2)pos.y=value;else angle=value;}
                        running[i]=!done;
                    }
                    DecoEngine.Update(td);
                    Assert(Same(pos.x,d.pivotPosVec.x)&&Same(pos.y,d.pivotPosVec.y)&&Same(angle,d.rotAngle),"random scalar fields");
                    Assert(Same(scale.x,d.scaleVec.x)&&Same(scale.y,d.scaleVec.y),"random scale fields");
                    for(int i=0;i<5;i++)Assert(Same(clock[i],rs[i].Pos)&&running[i]==rs[i].Running,"random clock/lifecycle");
                }
            }
            string state=DecoEngine.Command("state");Assert(state.Contains("mismatch=0"),"independent reference mismatch");
            DecoEngine.Uninstall();Assert(!DecoEngine.Enabled&&DecoEngine.Count==0,"uninstall");
            Console.WriteLine("PASS assertions="+assertions+"; production DecoEngine.cs linked; net8 surrogate only, no Unity/Mono performance claim");
            Console.WriteLine(state);
        }
        finally { Reset(); File.Delete(Path.Combine(path,"decoengine-on")); Directory.Delete(path); }
    }
}
