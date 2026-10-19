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
        public static Vector2 operator -(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
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
    struct Color {public float r,g,b,a; public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;} public static Color white=>new Color(1,1,1,1); public static Color operator -(Color a,Color b)=>new Color(a.r-b.r,a.g-b.g,a.b-b.b,a.a-b.a);}
    static class Time { public static int frameCount; }
}
enum DecPlacementType { Global,Tile,Camera,CameraAspect }
class scrParallax { public bool clampToScreen; public float multiplier_x,multiplier_y; public Vector2 multiplier; }
class scrDecoration
{
    public Transform childTransform=new Transform(),pivotTrans=new Transform(); public scrParallax parallax=new scrParallax();
    public Vector2 pivotPosVec,pivotOffsetVec,croppedPivotOffset,scaleVec=Vector2.one,textureScaleMultiplier=Vector2.one,parallaxOffset;
    public float rotAngle,scaleMultiplier=1,startRot;
    public Color color=Color.white; public float opacity=100;
    public bool Visible=true,ThrowUpdate; public int Updates;
    public bool GetVisible()=>Visible;
    public void UpdatePosition(){if(ThrowUpdate)throw new InvalidOperationException("injected UpdatePosition");Updates++;pivotTrans.position=pivotPosVec;}
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
    static class MoveApply {
        internal static bool Enabled=true,LateSkip=true,DeferRotScale=true;
        internal static bool DeferTransform(scrDecoration d){if(!d.Visible)return false;if(DecoEngine.Replaying!=null)TightLate.EngineDefer(DecoEngine.Replaying.LateEntry);return true;}
    }
    static class SceneReset {internal static bool Resetting;}
    static class Dormancy {internal static bool Enabled=true;}
    static class PosFast {
        internal static bool Enabled;
        internal static bool Try(DecoAnim.Rec r,Vector2 p)=>false;
        internal static void Remember(DecoAnim.Rec r){}
        internal static void Completing(DecoAnim.Rec r){}
    }
    static class ZeroTween
    {
        internal static int ThrowAfter;
        internal static int EvalCalls;
        public static float Eval(DG.Tweening.Ease e,float p,float d,float o,float period)
        { EvalCalls++; if(ThrowAfter>0 && --ThrowAfter==0)throw new InvalidOperationException("injected Eval"); if(e==DG.Tweening.Ease.Linear)return p/d; float t=p/d-1f; return t*t*t+1f; }
    }
    static partial class TightLate
    {
        internal class Entry {internal scrDecoration D; internal Transform Pivot; internal DecoEngine.State Engine; internal bool Rejected,EngineRejected,Has; internal int EngineDeferredFrame=-100;}
        internal static long writes,skipPos,skipRot,skipScale;
        internal static long EngineFixups;
        internal static string EngineAdmissionReasons=>"surrogate";
        internal static bool EngineAdmissionVisible(scrDecoration d)=>d.Visible;
        internal static int PendingCount=>engineDeferred.Count;
        internal static int Reason;
        private static readonly Dictionary<scrDecoration,Entry> entries=new Dictionary<scrDecoration,Entry>();
        internal static Entry EngineEntry(scrDecoration d)
        { if(!entries.TryGetValue(d,out var e))entries[d]=e=new Entry{D=d,Pivot=d.pivotTrans};return Reason==0&&!e.EngineRejected?e:null; }
        internal static Entry EngineEntry(scrDecoration d,out int missing){missing=0;return EngineEntry(d);}
        private static bool installed=true,compatible=true;
        internal static bool Enabled=true;
        private static int preparedEpoch,epoch;
        internal static int completedFrame;
        private static Transform parentTransform,cameraTransform;
        private static bool parentIdentity=true;
        internal static int StructuralReason;
        private static int Eligible(Entry e,Transform parent,bool identity,Transform cam,bool allowHidden=false)
            =>Reason==3&&allowHidden?StructuralReason:Reason;
        internal static Entry EngineBenchEntry(scrDecoration d)=>EngineEntry(d);
        internal static int EngineBenchReason(Entry e,Transform t)=>Reason;
        internal static void EngineApply(Entry e,Vector3 cam,Vector2 pos,float angle,Vector2 scale)
        {e.Pivot.position=pos;e.Pivot.rotation=Quaternion.Euler(0,0,angle);e.Pivot.localScale=scale;}
    }
    internal static partial class DecoAnim
    {
        internal sealed class Rec
        {
            internal DecoEngine.State Engine,OwnerHint;
            internal long EngineOrder;
            internal int EngineSkipFrame=-1;
            public scrDecoration D; public int Key,BatchSlot=-1;
            public float Pos,Dur,FStart,FEnd,FChange,FLast,Over,Period;
            public bool Running=true,Started=true,Stepped,InList=true;
            public Vector2 VStart,VEnd,VChange,VLast;
            public Color CStart,CEnd,CChange,CLast;
            public DG.Tweening.Tween Shadow;
            public DG.Tweening.Ease E;
        }
        private static readonly List<Rec> recs=new List<Rec>();
        private static bool fastDirty,batchOn,GroupEnabled;
        internal static bool Active=true;
        internal static long Steps,Completed;
        // Game/native setters below are doubles. The Step, ApplyE, callbacks and
        // replay exception boundary are extracted verbatim from production.
        private static bool Prof;
        internal static bool PairXY=true;
        internal static long PairSteps;
        private static bool PairAllowed()=>true;
        private static readonly Action<scrDecoration,Vector2,Vector2> setPos=(d,p,off)=>{d.pivotPosVec=p;PositionSetter(d,14);};
        private static long profN;
        private static readonly long[] ProfN=new long[16],ProfApply=new long[16],ProfSet=new long[16];
        private static ref Vector2 scaleRef(scrDecoration d)=>ref d.scaleVec;
        private static ref Vector2 pivotOffRef(scrDecoration d)=>ref d.pivotOffsetVec;
        private static ref Vector2 pivotPosRef(scrDecoration d)=>ref d.pivotPosVec;
        private static ref float rotRef(scrDecoration d)=>ref d.rotAngle;
        private static scrParallax parallaxRef(scrDecoration d)=>d.parallax;
        private static void BatchShadowStart(Rec r){}
        private static void BatchShadowScale(Rec r,Vector2 v){}
        private static void Apply(Rec r,float p,bool native=false)=>ApplyE(r,ZeroTween.Eval(r.E,p,r.Dur,r.Over,r.Period));
        internal static readonly List<string> Trace=new List<string>();
        private static void setPosX(scrDecoration d,float v,Vector2 off){d.pivotPosVec.x=v;PositionSetter(d,1);}
        private static void setPosY(scrDecoration d,float v,Vector2 off){d.pivotPosVec.y=v;PositionSetter(d,2);}
        private static void PositionSetter(scrDecoration d,int k){Trace.Add(k+":"+d.Visible);if(d.Visible){if(DecoEngine.Replaying!=null)TightLate.EngineDefer(DecoEngine.Replaying.LateEntry);}else d.UpdatePosition();}
        private static void setPivX(scrDecoration d,float v){d.pivotOffsetVec.x=v;}
        private static void setPivY(scrDecoration d,float v){d.pivotOffsetVec.y=v;}
        private static void setParOffX(scrDecoration d,float v){d.parallaxOffset.x=v;}
        private static void setParOffY(scrDecoration d,float v){d.parallaxOffset.y=v;}
        private static void setRot(scrDecoration d,float v){d.rotAngle=v;d.pivotTrans.rotation=Quaternion.Euler(0,0,v);}
        private static void setScale(scrDecoration d,Vector2 v){d.scaleVec=v;d.pivotTrans.localScale=v;}
        private static void setCol(scrDecoration d,Color v){d.color=v;d.Visible=d.color.a*d.opacity!=0;Trace.Add("9:"+d.Visible);}
        private static void setOpa(scrDecoration d,float v){d.opacity=v;d.Visible=d.color.a*d.opacity!=0;Trace.Add("10:"+d.Visible);}
        private static void Error(Exception e){Trace.Add("error:"+e.GetType().Name);}
        internal static void DropAll(){DecoEngine.RestoreAll();foreach(var r in recs){r.Running=false;r.InList=false;} recs.Clear();DecoEngine.Forget();}
    }
    static class NativeMotion {internal static bool Enabled;}
}
static class Program
{
    static int assertions;
    static void Tick(float td) { Hitch.Playing=true;TightLate.completedFrame=Time.frameCount-1;DecoEngine.BeginFrame(); DecoEngine.Update(td); DecoEngine.EndFrame(); }
    static void Frame(float td)
    {
        Time.frameCount++;
        Tick(td);
        foreach(var r in DecoAnim.EngineRecords)
            if(r.Running&&r.EngineSkipFrame!=Time.frameCount)DecoAnim.EngineReplayStep(r,td);
        DecoEngine.Admit();TightLate.EngineLatePostfix();
    }
    static void Assert(bool condition,string message) {assertions++;if(!condition)throw new Exception(message);}
    static bool Same(float a,float b)=>BitConverter.SingleToInt32Bits(a)==BitConverter.SingleToInt32Bits(b);
    static DecoAnim.Rec Make(scrDecoration d,int key,float duration=2)
    {
        var r=new DecoAnim.Rec {D=d,Key=key,Dur=duration,EngineOrder=DecoEngine.NextOrder(),FStart=2,FEnd=9,FChange=7,
            VStart=new Vector2(1,1),VEnd=new Vector2(4,5),VChange=new Vector2(3,4),E=DG.Tweening.Ease.OutCubic};
        DecoAnim.EngineRecords.Add(r);DecoEngine.Dirty=true;return r;
    }
    static void Reset(){TightLate.EngineLatePostfix();DecoAnim.DropAll();TightLate.Reason=0;ADOBase.editor=null;}
    static void Main()
    {
        string path=Path.Combine(Path.GetTempPath(),"sf-decoengine-check-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"decoengine-on"),"");
        try
        {
            DecoEngine.Configure(path);Assert(DecoEngine.Enabled,"flag");
            DecoEngine.Command("stats on");
            var d=new scrVisualDecoration();var x=Make(d,1);var y=Make(d,2);var rot=Make(d,5);var sx=Make(d,7);var sy=Make(d,8);
            DecoEngine.Admit();Assert(DecoEngine.Count==1 && DecoAnim.EngineRecords.Count==0,"exclusive ownership");
            DecoEngine.Command("verify");
            Tick(.5f);
            Assert(Same(d.pivotPosVec.x,6.046875f) && Same(d.rotAngle,6.046875f),"scalar progression");
            Assert(Same(d.scaleVec.x,2.734375f) && Same(d.scaleVec.y,3.3125f),"scale axes");
            var list=new List<scrDecoration>();DecoEngine.Collect(list,new HashSet<scrDecoration>());Assert(list.Count==1,"collect");
            DecoEngine.ReleaseRecord(x);Assert(DecoEngine.Count==0 && DecoAnim.EngineRecords.Count==5,"release all channels");
            for(int i=1;i<5;i++)Assert(DecoAnim.EngineRecords[i-1].EngineOrder<DecoAnim.EngineRecords[i].EngineOrder,"restored order");
            Reset();d=new scrVisualDecoration();x=Make(d,1);Make(d,9);DecoEngine.Admit();
            Assert(DecoEngine.Count==1,"changing alpha admitted");Tick(.5f);
            Assert(DecoEngine.Count==1&&Same(x.Pos,.5f),"finite alpha replay retains ownership");
            Reset();d=new scrVisualDecoration();x=Make(d,1);var color=Make(d,9);color.CStart.a=color.CEnd.a=1;DecoEngine.Admit();
            Assert(DecoEngine.Count==1&&DecoAnim.EngineRecords.Count==1&&ReferenceEquals(DecoAnim.EngineRecords[0],color),"RGB-only color coexists in original loop");
            // The original color loop is deliberately retained; only transforms
            // are owned. Check every intermediate value, including completion.
            Reset();d=new scrVisualDecoration();x=Make(d,1);color=Make(d,9,1);
            color.CStart.a=1;color.CEnd.a=0;color.CChange.a=-1;color.E=DG.Tweening.Ease.Linear;
            DecoEngine.Admit();Tick(.25f);
            Assert(DecoEngine.Count==1&&Same(x.Pos,.25f)&&Same(color.Pos,0),"fade interior owned, color not advanced");
            color.Pos=.75f;d.color.a=.25f;Tick(.25f);
            Assert(DecoEngine.Count==1&&Same(x.Pos,.5f)&&!d.Visible,"zero completion retains transform clock and owner");
            Assert(DecoAnim.EngineRecords.Count==1&&ReferenceEquals(DecoAnim.EngineRecords[0],color)&&color.EngineSkipFrame==Time.frameCount,"color remains legacy member but skips duplicate Step");
            Reset();d=new scrVisualDecoration();x=Make(d,1);var opacity=Make(d,10,1);
            opacity.FStart=100;opacity.FEnd=0;opacity.FChange=-100;opacity.E=DG.Tweening.Ease.Linear;
            DecoEngine.Admit();Tick(.5f);Assert(DecoEngine.Count==1,"opacity interior owned");
            opacity.Pos=.5f;Tick(.5f);Assert(DecoEngine.Count==1&&!d.Visible,"opacity zero endpoint retained");
            Reset();d=new scrVisualDecoration();x=Make(d,1);color=Make(d,9,1);
            color.CStart.a=1;color.CEnd.a=1;color.CChange.a=float.NaN;DecoEngine.Admit();Tick(.1f);
            Assert(DecoEngine.Count==0&&Same(x.Pos,0),"nonfinite alpha conservatively falls back");
            // Same-key kill detaches only that slot; the actual Complete callbacks
            // still run in DecoAnim. Model their published result before NewRec.
            Reset();d=new scrVisualDecoration();x=Make(d,1);y=Make(d,2);DecoEngine.Admit();
            var retained=y.Engine;DecoEngine.DetachRecord(x);x.Running=false;d.pivotPosVec.x=x.FEnd;
            Assert(DecoEngine.Count==1&&retained.N==1&&ReferenceEquals(y.Engine,retained),"kill retains other channel");
            var preparation=DecoEngine.PrepareNew(d,1);var replacement=Make(d,1);replacement.FStart=d.pivotPosVec.x;
            DecoEngine.BindNew(preparation,replacement);DecoEngine.Admit();
            Assert(DecoEngine.Count==1&&ReferenceEquals(replacement.Engine,retained)&&retained.N==2,"same-key slot attachment without state restore");
            Assert(Same(replacement.FStart,9)&&!replacement.InList&&DecoAnim.EngineRecords.Count==0,"completion end read before new start, no duplicate membership");
            // Last slot and newly added key survive Update until original first Step.
            Reset();d=new scrVisualDecoration();x=Make(d,1);DecoEngine.Admit();retained=x.Engine;
            DecoEngine.DetachRecord(x);x.Running=false;preparation=DecoEngine.PrepareNew(d,5);
            rot=Make(d,5);DecoEngine.BindNew(preparation,rot);Tick(.1f);
            Assert(DecoEngine.Count==1&&retained.N==1,"new key binds to retained empty slot immediately");
            DecoEngine.Admit();Assert(ReferenceEquals(rot.Engine,retained),"new key attaches to retained empty state");
            preparation=DecoEngine.PrepareNew(d,9);color=Make(d,9);color.Started=false;
            color.CStart.a=1;color.CEnd.a=0;color.E=DG.Tweening.Ease.Linear;DecoEngine.BindNew(preparation,color);
            Tick(.1f);Assert(DecoEngine.Count==1,"new unstarted color is predicted from end-start");
            DecoEngine.PrepareNew(d,3);Assert(DecoEngine.Count==0,"unsupported new key releases before first setter");
            Reset();d=new scrVisualDecoration();x=Make(d,1);DecoEngine.Admit();
            preparation=DecoEngine.PrepareNew(d,2);y=Make(d,2);DecoEngine.BindNew(preparation,y);
            var unsupported=Make(d,3);DecoEngine.Admit();
            Assert(DecoEngine.Count==0&&DecoAnim.EngineRecords.Count==3,"late admission rejection restores without duplicate new records");
            var unique=new HashSet<DecoAnim.Rec>(DecoAnim.EngineRecords);Assert(unique.Count==3,"one-list invariant after attached-then-rejected");
            Reset();d=new scrVisualDecoration();color=Make(d,9);d.color.a=0;DecoEngine.Dirty=false;
            DecoEngine.AlphaWillSet(color,.1f);Assert(DecoEngine.Dirty,"unhide schedules re-admission without dictionary lookup");
            Reset();d=new scrVisualDecoration();x=Make(d,1);color=Make(d,9,1);
            color.CStart.a=1;color.CChange.a=-1;color.CEnd.a=1;color.E=DG.Tweening.Ease.Linear;
            DecoEngine.Admit();Tick(1);Assert(DecoEngine.Count==1&&Same(x.Pos,1)&&d.Visible,"OnUpdate zero then positive OnComplete replayed exactly");
            Reset();d=new scrVisualDecoration();x=Make(d,1);x.Shadow=new DG.Tweening.Tween();DecoEngine.Admit();Assert(DecoEngine.Count==0,"shadow excluded");
            Reset();d=new scrVisualDecoration();x=Make(d,1);x.Started=false;DecoEngine.Admit();Assert(DecoEngine.Count==0,"startup must precede admission");
            Reset();d=new scrVisualDecoration();x=Make(d,1,.1f);DecoEngine.Admit();long completed=DecoAnim.Completed;Tick(.2f);
            Assert(!x.Running && !x.InList && x.Engine==null && DecoEngine.Count==1,"completion detaches record but retains resident slot");
            Assert(Same(d.pivotPosVec.x,9f)&&DecoAnim.Completed==completed+1,"completion exact FEnd");
            Reset();d=new scrVisualDecoration();x=Make(d,7,.1f);DecoEngine.Admit();Tick(.2f);Assert(Same(d.scaleVec.x,4f),"scale completes using plugin value");
            Reset();d=new scrVisualDecoration();x=Make(d,1);DecoEngine.Admit();TightLate.Reason=4;Tick(.5f);
            Assert(DecoAnim.EngineRecords.Count==1&&Same(x.Pos,0f),"eligibility exit does not advance twice");
            Reset();d=new scrVisualDecoration();x=Make(d,1);DecoEngine.Admit();d.pivotOffsetVec=new Vector2(1,0);Tick(.5f);
            Assert(DecoEngine.Count==0&&Same(x.Pos,0f),"offset mutation returns original");
            Reset();d=new scrVisualDecoration();x=Make(d,1);DecoEngine.Admit();d.childTransform.localPosition=new Vector3(10,0,0);Tick(.5f);
            Assert(DecoEngine.Count==0&&Same(x.Pos,0f),"external child mutation returns original");
            Reset();d=new scrVisualDecoration();x=Make(d,1);DecoEngine.Admit();d.childTransform.ThrowRead=true;Tick(.5f);
            Assert(DecoEngine.Count==0&&Same(x.Pos,0f),"native guard exception is decoration-local");
            Assert(DecoAnim.EngineRecords.Count==1&&x.Engine==null,"native guard exception restores ownership");
            Reset();DecoEngine.Command("verify off");Time.frameCount=99;d=new scrVisualDecoration();
            x=Make(d,1,2);y=Make(d,2,3);rot=Make(d,5,4);DecoEngine.Admit();ZeroTween.ThrowAfter=2;
            Tick(.5f);
            Assert(DecoEngine.Count==0&&DecoAnim.EngineRecords.Count==3,"exception releases whole decoration");
            Assert(x.EngineSkipFrame==99&&y.EngineSkipFrame!=99&&rot.EngineSkipFrame!=99,"only advanced records suppress duplicate frame");
            Assert(Same(x.Pos,.5f)&&Same(y.Pos,0)&&Same(rot.Pos,0),"partial advance clocks preserved");
            DecoEngine.Dirty=true;DecoEngine.Admit();Assert(DecoEngine.Count==0,"exception decoration stays excluded");
            DecoEngine.Command("verify");
            Reset();d=new scrVisualDecoration();x=Make(d,1);DecoEngine.Admit();d.pivotPosVec=new Vector2(100,200);Tick(.1f);
            Assert(Same(d.pivotPosVec.y,200),"unowned axis preserves intervening effect");
            Reset();d=new scrVisualDecoration();x=Make(d,1);DecoEngine.Admit();DecoEngine.DropLevel();Assert(!x.Running&&DecoEngine.Count==0&&DecoAnim.EngineRecords.Count==0,"level boundary drops");
            Reset();d=new scrVisualDecoration();x=Make(d,1);y=Make(d,2,10000);DecoEngine.Admit();retained=x.Engine;
            for(int wave=0;wave<200;wave++)
            {
                DecoEngine.DetachRecord(x);x.Running=false;d.pivotPosVec.x=x.FEnd;
                preparation=DecoEngine.PrepareNew(d,1);
                x.FStart=d.pivotPosVec.x;x.FEnd=x.FStart+1;x.FChange=1;x.Pos=0;x.Running=true;x.Started=false;
                x.EngineOrder=DecoEngine.NextOrder();x.InList=true;DecoAnim.EngineRecords.Add(x);DecoEngine.Dirty=true;
                DecoEngine.BindNew(preparation,x);Tick(.01f);
                Assert(x.Started&&Same(x.Pos,.01f),"resident ordered lane performs original first Startup/Step");
                DecoEngine.Admit();Tick(.01f);
                Assert(ReferenceEquals(x.Engine,retained)&&ReferenceEquals(y.Engine,retained)&&retained.N==2,"repeated same-Rec reuse retains owner");
                Assert(!x.InList&&DecoAnim.EngineRecords.Count==0,"reuse wave has exactly one owner/list membership");
            }
            Assert(DecoEngine.Command("bench 101").StartsWith("usage:"),"bench percent validation");
            // Boundary order is observable: the position setter sees the alpha
            // value at its own original creation order, including exact zero.
            for(int reverse=0;reverse<2;reverse++)
            {
                Reset();d=new scrVisualDecoration();DecoAnim.Rec c;
                if(reverse==0){x=Make(d,1,10);c=Make(d,9,1);}
                else {c=Make(d,9,1);x=Make(d,1,10);}
                c.CStart=Color.white;c.CEnd=new Color(1,1,1,0);c.CChange=new Color(0,0,0,-1);c.E=DG.Tweening.Ease.Linear;
                DecoEngine.Admit();retained=x.Engine;retained.Sample=true;int stableIndex=retained.Index;
                long scans=Counter("admission-scans"),restored=Counter("restores");
                DecoAnim.Trace.Clear();Frame(1);
                string trace=string.Join(",",DecoAnim.Trace);
                Assert(trace==(reverse==0?"1:True,9:False,9:False":"9:False,9:False,1:False"),"color/position creation-order edge "+reverse);
                Assert(ReferenceEquals(x.Engine,retained)&&retained.Index==stableIndex&&!d.Visible,"zero edge keeps resident integer slot");
                Assert(Counter("admission-scans")==scans&&Counter("restores")==restored,"alpha edge causes no admission scan or ownership restore");
                Frame(.1f);Assert(ReferenceEquals(x.Engine,retained)&&Same(x.Pos,1.1f),"consecutive hidden frame advances without release");
                // New color and new scale start on a hidden resident. Startup
                // and Step remain in creation order, without re-admission.
                var next=DecoEngine.PrepareNew(d,9);var fadeIn=Make(d,9,1);fadeIn.Started=false;
                fadeIn.CStart=new Color(1,1,1,0);fadeIn.CEnd=Color.white;fadeIn.E=DG.Tweening.Ease.Linear;DecoEngine.BindNew(next,fadeIn);
                next=DecoEngine.PrepareNew(d,7);var scaleNew=Make(d,7,1);scaleNew.Started=false;DecoEngine.BindNew(next,scaleNew);
                Frame(.5f);
                Assert(ReferenceEquals(x.Engine,retained)&&ReferenceEquals(scaleNew.Engine,retained)&&d.Visible,"hidden to visible with new color/scale retains owner");
                Assert(scaleNew.Started&&Same(scaleNew.VStart.x,1)&&Same(scaleNew.Pos,.5f),"new scale Startup/Step is executed once");
                Assert(Counter("restores")==restored,"effect restart does not restore resident");
                // A completed/removed Rec can find the owner without a hash.
                var hinted=DecoEngine.PrepareNew(d,1,x);Assert(ReferenceEquals(hinted,retained),"effect boundary reuses checked owner hint");
            }
            // A hole never moves another decoration's integer slot.
            for(int split=0;split<2;split++)
            {
                Reset();d=new scrVisualDecoration();x=Make(d,1,2);
                if(split==0){y=Make(d,2,2);color=Make(d,9,1);}
                else {color=Make(d,9,1);y=Make(d,2,2);}
                color.CStart=Color.white;color.CChange=new Color(0,0,0,-1);color.CEnd=new Color(1,1,1,0);color.E=DG.Tweening.Ease.Linear;
                DecoEngine.Admit();x.Engine.Sample=true;DecoAnim.Trace.Clear();long pairs=DecoAnim.PairSteps;
                Frame(1);
                Assert(DecoAnim.PairSteps-pairs==(split==0?1:0),"pair only with original global adjacency");
                Assert(string.Join(",",DecoAnim.Trace)==(split==0?"14:True,9:False,9:False":"1:True,9:False,9:False,2:False"),"XY pair cannot cross color zero edge");
                Assert(Same(x.Pos,1)&&Same(y.Pos,1)&&ReferenceEquals(x.Engine,y.Engine),"paired and split clocks have one owner");
            }
            // A hole never moves another decoration's integer slot.
            Reset();d=new scrVisualDecoration();x=Make(d,1);var d2=new scrVisualDecoration();y=Make(d2,1);DecoEngine.Admit();
            int secondIndex=y.Engine.Index;DecoEngine.ReleaseRecord(x);
            Assert(y.Engine.Index==secondIndex,"release leaves stable integer slot for other owner");
            // Nonfinite/unsupported alpha exclusions are sticky for the level.
            Reset();d=new scrVisualDecoration();x=Make(d,1);color=Make(d,9);color.CChange.a=float.NaN;
            DecoEngine.Admit();Tick(.1f);DecoEngine.Dirty=true;DecoEngine.Admit();
            Assert(DecoEngine.Count==0&&x.Engine==null,"uncertain alpha cannot re-admit after permanent exclusion");
            Reset();d=new scrVisualDecoration();x=Make(d,1);DecoEngine.Admit();var hiddenEntry=x.Engine.LateEntry;
            TightLate.Reason=3;Time.frameCount++;TightLate.completedFrame=Time.frameCount-1;
            Assert(TightLate.EngineReason(hiddenEntry)==0,"real EngineReason retains hidden resident without entry heartbeat");
            TightLate.StructuralReason=4;Assert(TightLate.EngineReason(hiddenEntry)==4,"hidden does not bypass structural rejection");
            TightLate.StructuralReason=0;TightLate.completedFrame=Time.frameCount-2;
            Assert(TightLate.EngineReason(hiddenEntry)==7,"missing manager heartbeat rejects deferral");
            Reset();DecoEngine.Command("verify off");d=new scrVisualDecoration();x=Make(d,1);rot=Make(d,5);DecoEngine.Admit();
            d.color.a=0;d.Visible=false;ZeroTween.ThrowAfter=1;Tick(.1f);
            Assert(DecoEngine.Replaying==null&&DecoEngine.Count==0,"replay failure restores context and permanently excludes owner");
            Assert(x.EngineSkipFrame==Time.frameCount&&rot.EngineSkipFrame==Time.frameCount,"replay error recovery does not double-step legacy entries");
            DecoEngine.Command("verify");
            // Production guard scheduling: managed changes remain immediate;
            // native-only writes have the explicitly bounded sampling delay.
            Reset();DecoEngine.Command("verify off");Time.frameCount=1000;
            d=new scrVisualDecoration();x=Make(d,1,100);DecoEngine.Admit();
            int due=x.Engine.ChildCheckFrame;
            Assert(due>1000&&due<=1016,"admission staggers first native check");
            d.childTransform.localPosition=new Vector3(10,0,0);
            for(int frame=1001;frame<due;frame++){Time.frameCount=frame;Tick(.001f);Assert(DecoEngine.Count==1,"documented native-only window");}
            Time.frameCount=due;Tick(.001f);Assert(DecoEngine.Count==0,"sample detects native-only mutation at due frame");
            Reset();d=new scrVisualDecoration();x=Make(d,1,100);DecoEngine.Admit();
            d.pivotOffsetVec=new Vector2(1,0);Tick(.001f);Assert(DecoEngine.Count==0&&Same(x.Pos,0),"managed guard remains immediate without verify");
            Reset();d=new scrVisualDecoration();x=Make(d,1,100);DecoEngine.Admit();
            due=x.Engine.ChildCheckFrame;Time.frameCount=due;Tick(.001f);
            Assert(x.Engine.ChildCheckFrame==due+16,"steady-state native checks separated by sixteen frames");
            d.childTransform.ThrowRead=true;Time.frameCount=due+16;Tick(.001f);
            Assert(DecoEngine.Count==0,"sampled native exception restores original records");
            // Keep a full five-channel state unchanged until completion. Shared
            // ease must match every clock/parameter bit; mask survives no-op compaction.
            Reset();d=new scrVisualDecoration();int[] sharedKeys={1,2,5,7,8};var shared=new DecoAnim.Rec[5];
            for(int i=0;i<5;i++)shared[i]=Make(d,sharedKeys[i],100);
            DecoEngine.Admit();retained=shared[0].Engine;int calls=ZeroTween.EvalCalls;
            Tick(.01f);Assert(ZeroTween.EvalCalls-calls==1,"identical five channels evaluate once");
            Assert(retained.N==5&&retained.Mask==((1<<1)|(1<<2)|(1<<5)|(1<<7)|(1<<8)),"no-completion mask retained");
            shared[2].Period=BitConverter.Int32BitsToSingle(unchecked((int)0x80000000));calls=ZeroTween.EvalCalls;
            Tick(.01f);Assert(ZeroTween.EvalCalls-calls==3,"signed-zero ease parameter does not share");
            shared[1].Running=false;Tick(.01f);
            Assert(retained.N==4&&(retained.Mask&(1<<2))==0&&!shared[1].InList&&shared[1].Engine==null,"dead slot compacts once and clears mask");
            DecoEngine.Command("verify");
            Reset();d=new scrVisualDecoration();d.Visible=false;x=Make(d,1);Make(d,9);DecoEngine.Admit();
            Assert(DecoEngine.Count==0&&DecoAnim.EngineRecords.Count==2,"hidden prefilter preserves original records");
            d.Visible=true;DecoEngine.Dirty=true;DecoEngine.Admit();Assert(DecoEngine.Count==1,"unhidden record re-admits");
            Reset();d=new scrVisualDecoration();x=Make(d,1,.1f);DecoEngine.Admit();var entry=x.Engine.LateEntry;
            Tick(.2f);Assert(DecoEngine.Count==1&&TightLate.PendingCount==1,"last completion retains resident and deferred entry until manager postfix");
            TightLate.EngineDefer(entry);Assert(TightLate.PendingCount==1,"late registration deduplicated with frame integer");
            d.Visible=false;TightLate.EngineLatePostfix();
            Assert(d.Updates==1&&TightLate.PendingCount==0&&Same(d.pivotTrans.position.x,d.pivotPosVec.x),"hidden after update gets original late fixup even after state recycle");
            Time.frameCount++;TightLate.EngineDefer(entry);d.Visible=true;TightLate.EngineLatePostfix();
            Assert(d.Updates==1,"visible late endpoint needs no hidden fixup");
            Time.frameCount++;TightLate.EngineDefer(entry);d.Visible=false;d.ThrowUpdate=true;TightLate.EngineLatePostfix();
            Assert(entry.EngineRejected&&TightLate.PendingCount==0,"fixup exception drains and rejects entry");
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
                    Tick(td);
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
    static long Counter(string key)
    {
        foreach(string item in DecoEngine.Command("state").Split(' '))
            if(item.StartsWith(key+"="))return long.Parse(item.Substring(key.Length+1));
        throw new Exception("missing counter "+key);
    }
}
