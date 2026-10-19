using System;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using UnityEngine;

namespace UnityEngine {
    static class Time { internal static float smoothDeltaTime=1f/120,deltaTime=1f/120; }
    struct Vector2 {
        public float x,y;
        public Vector2(float a,float b){x=a;y=b;}
        public static Vector2 operator -(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
    }
    struct Color {
        public float r,g,b,a;
        public static Color operator -(Color x,Color y)=>new Color{r=x.r-y.r,g=x.g-y.g,b=x.b-y.b,a=x.a-y.a};
    }
}
class scrDecoration { internal Vector2 Pivot=new Vector2(3,-2), Scale=new Vector2(2,4); internal float Rot=7; internal bool Deferred; internal Color Color = new Color {a=1}; internal float Opacity=1; internal bool GetVisible()=>Deferred; }
class scrVisualDecoration : scrDecoration { }
class scrParallax { internal Vector2 multiplier; }
namespace HarmonyLib {
    static class AccessTools { internal const BindingFlags all=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance; }
    sealed class PatchInfo { internal string[] Owners=Array.Empty<string>(); }
    static class Harmony { internal static PatchInfo GetPatchInfo(MethodBase m)=>null; }
}
namespace StutterFix {
    static class UpdateEvalMemo { internal static void NewFrame(){} }
    static class PosFast {
        internal static bool Enabled=false;
        internal static bool Try(DecoAnim.Rec r,Vector2 v)=>false;
        internal static void Completing(DecoAnim.Rec r){}
        internal static void Remember(DecoAnim.Rec r){}
    }
    static class NativeMotion { internal static bool Enabled,Failed; }
    static class InvisibleSkip { internal static bool MaskOrUnknown(scrVisualDecoration d)=>false; }
    static class Edition { internal const bool Dev=false; }
    static class MoveApply { internal static bool DeferTransform(scrDecoration d)=>d.Deferred; }
    static class ZeroTween {
        private delegate float EvalFn(Ease e,EaseFunction f,float t,float d,float o,float p);
        private static readonly EvalFn eval=(EvalFn)Delegate.CreateDelegate(typeof(EvalFn),typeof(Ease).Assembly.GetType("DG.Tweening.Core.Easing.EaseManager").GetMethod("Evaluate",HarmonyLib.AccessTools.all,null,new[]{typeof(Ease),typeof(EaseFunction),typeof(float),typeof(float),typeof(float),typeof(float)},null));
        internal static float Eval(Ease e,float t,float d,float o,float p)=>eval(e,null,t,d,o,p);
    }
    internal static partial class DecoAnim {
        private static readonly List<Rec> recs=new List<Rec>();
        private static double LastFrameMs,UpdateMs;
        private static long Frames;
        private static void PrepareNative(float td){}
        private static long activeNativeEpoch,Steps,Completed,profN;
        private static readonly long[] ProfN=new long[16],ProfApply=new long[16],ProfSet=new long[16];
        private const bool Prof=false;
        private static bool PairXY;
        private static long PairSteps;
        private static bool PairAllowed()=>true;
        private static Action<scrDecoration,Vector2,Vector2> setPos = PairSet;
        private static bool pairThrows;
        private static int pairCalls;
        private static Vector2 pairInput, pairOffset;
        private static void PairSet(scrDecoration d,Vector2 v,Vector2 off) {
            pairCalls++; pairInput=v; pairOffset=off;
            if(pairThrows){pairThrows=false;throw new InvalidOperationException("pair setter");}
            Hit(12);d.Pivot=v;
        }
        private static int errors;
        private static bool mutation;
        private static bool benchStubs;
        private static int throwCall,calls;
        private static readonly List<int> trace=new List<int>();
        private static int B(float f)=>BitConverter.SingleToInt32Bits(f);
        private static ref Vector2 pivotPosRef(scrDecoration d)=>ref d.Pivot;
        private static ref Vector2 scaleRef(scrDecoration d)=>ref d.Scale;
        private static ref float rotRef(scrDecoration d)=>ref d.Rot;
        private static ref Color colRef(scrDecoration d)=>ref d.Color;
        private static ref float opaRef(scrDecoration d)=>ref d.Opacity;
        private static void DropAll(){ BatchRestoreAll(); foreach(var r in recs)r.Running=false; recs.Clear(); }
        private static Vector2 pivotOffRef(scrDecoration d)=>new Vector2(0.125f,-0.75f);
        private static scrParallax parallaxRef(scrDecoration d)=>new scrParallax();
        private static void Error(Exception ex){errors++;}
        private static void Apply(Rec r,float to,bool native=false)=>ApplyE(r,ZeroTween.Eval(r.E,to,r.Dur,r.Over,r.Period));
        private static void Hit(int key) {
            if(benchStubs)return;
            var callback=onHit;onHit=null;callback?.Invoke();
            trace.Add(key); calls++;
            if(mutation && calls==1) { recs[2].Pos+=0.25f; recs[3].Running=false; }
            if(calls==throwCall)throw new InvalidOperationException("injected setter");
        }
        private static Action onHit;
        private static void setPosX(scrDecoration d,float f,Vector2 off){Hit(1);d.Pivot.x=f;}
        private static void setPosY(scrDecoration d,float f,Vector2 off){Hit(2);d.Pivot.y=f;}
        private static void setRot(scrDecoration d,float f){Hit(5);d.Rot=f;}
        private static void setScale(scrDecoration d,Vector2 f){Hit(7);d.Scale=f;}
        private static void setPivX(scrDecoration d,float f){}
        private static void setPivY(scrDecoration d,float f){}
        private static void setParOffX(scrDecoration d,float f){}
        private static void setParOffY(scrDecoration d,float f){}
        private static void setOpa(scrDecoration d,float f){d.Opacity=f;}
        private static void setCol(scrDecoration d,Color f){d.Color=f;}
        private static void PositionUpdate(Rec r,float f){if(r.Key==1)setPosX(r.D,f,pivotOffRef(r.D));else setPosY(r.D,f,pivotOffRef(r.D));}

        private static void Require(bool b,string why){if(!b)throw new Exception(why);}
        private static void Setup(bool deferred,Ease ease,int fail,bool mutate,float pos,float dur,int[] keys) {
            recs.Clear(); errors=0; calls=pairCalls=0; pairThrows=false; Completed=Steps=0; trace.Clear();throwCall=fail;mutation=mutate;
            var d=new scrDecoration{Deferred=deferred};
            foreach(int key in keys)recs.Add(new Rec{D=d,Key=key,E=ease,Dur=dur,Pos=pos,Over=1.70158f,Period=0,
                FStart=11.25f,FEnd=-17.75f,VEnd=new Vector2(-3,8)});
        }
        private static string Signature() {
            var s=new System.Text.StringBuilder();
            foreach(var r in recs) {
                s.Append(r.Key+":"+r.Running+":"+r.Started+":"+r.Stepped+":");
                foreach(float f in new[]{r.Pos,r.FLast,r.FChange,r.VStart.x,r.VStart.y,r.VChange.x,r.VChange.y,r.VLast.x,r.VLast.y,r.D.Pivot.x,r.D.Pivot.y,r.D.Scale.x,r.D.Scale.y,r.D.Rot})s.Append(B(f)+",");
            }
            return s+" completed="+Completed+" steps="+Steps+" errors="+errors+" trace="+string.Join(",",trace);
        }
        private static void Tick(bool grouped,float dt) {
            for(int i=0;i<recs.Count;i++) {
                if(!recs[i].Running)continue;
                int n=grouped?GroupCount(i):1;
                if(n>1){StepGroup(i,n,dt);i+=n-1;}
                else if(PairXY && recs[i].Key==1 && i+1<recs.Count && recs[i+1].Key==2 && recs[i+1].Running && ReferenceEquals(recs[i].D,recs[i+1].D)) {
                    Steps+=2; PairSteps++; StepPair(recs[i],recs[i+1],dt); i++;
                }
                else GroupIndividual(recs[i],dt);
            }
        }
        private static string BatchCase(bool enabled, int[] keys, Ease ease, float td, int scenario)
        {
            BatchRestoreAll(); recs.Clear(); batchFree.Clear(); batchRetired.Clear(); batchUsed=batchCount=0;
            batchSchedule.Clear(); batchPending.Clear(); batchReturned.Clear();
            if(br==null)BatchAllocate();
            batchOn=enabled; batchVerify=true; batchSerial=0; batchChecks=batchMismatch=0;
            GroupEnabled=false; PairXY=true; fastDirty=true; FastLane=true;
            Completed=Steps=errors=0; pairThrows=false; throwCall=0; mutation=false; calls=0; trace.Clear(); onHit=null;
            var ds=new[]{new scrVisualDecoration{Deferred=true},new scrVisualDecoration{Deferred=true}};
            var owned=new List<Rec>();
            foreach(var d in ds)foreach(int key in keys)
            {
                var r=new Rec{D=d,Key=key,E=ease,Dur=1,Over=1.70158f,Period=0.3f,FStart=11.25f,FEnd=-17.75f,VEnd=new Vector2(-3,8),BatchOrder=++batchOrder,InList=true};
                recs.Add(r);owned.Add(r);
            }
            float oldDelta=Time.deltaTime,oldSmooth=Time.smoothDeltaTime;
            Time.deltaTime=Time.smoothDeltaTime=td;
            for(int f=0;f<7;f++)
            {
                if(scenario==1 && f==2)ds[0].Deferred=false;
                if(scenario==1 && f==4)ds[0].Deferred=true;
                if(scenario==2 && f==2) { ds[0].Opacity=0; ds[1].Color=new Color{a=0}; }
                if(scenario==2 && f==4) { ds[0].Opacity=1; ds[1].Color=new Color{a=1}; }
                if(scenario==3 && f==3)Complete(owned[0]);
                if(scenario==4 && f==3)NativeMotion.Enabled=true;
                UpdatePrefix();
                Require(batchMismatch==0,"batch verification "+batchFirst);
            }
            NativeMotion.Enabled=false; Time.deltaTime=oldDelta;Time.smoothDeltaTime=oldSmooth;
            var sb=new System.Text.StringBuilder();
            foreach(var r in owned)
            {
                if(r.BatchSlot>=0)BatchSync(r.BatchSlot);
                sb.Append(r.Key+":"+r.Running+":"+r.Started+":"+r.Stepped+":"+r.InList+":");
                foreach(float v in new[]{r.Pos,r.FLast,r.VLast.x,r.VLast.y,r.D.Pivot.x,r.D.Pivot.y,r.D.Rot,r.D.Scale.x,r.D.Scale.y,r.D.Opacity,r.D.Color.a})sb.Append(B(v)+",");
            }
            sb.Append("done="+Completed+" steps="+Steps+" errors="+errors);
            BatchRestoreAll();recs.Clear();batchOn=false;
            return sb.ToString();
        }
        private static void BatchTests()
        {
            int count=0;
            foreach(int[] keys in new[]{new[]{1,2,5,7,8},new[]{7,8,1,2,5},new[]{8,7,5,2,1},new[]{1,2,10,5,7,8},new[]{5,9,7,8},new[]{7,8}})
            foreach(Ease ease in new[]{Ease.Linear,Ease.OutCubic,Ease.OutBack,Ease.OutBounce,Ease.InElastic,Ease.Flash})
            foreach(float td in new[]{0.016f,0.5f,1f,-0.02f,0f})
            foreach(int scenario in new[]{0,1,2,3,4})
            {
                string baseline=BatchCase(false,keys,ease,td,scenario);
                string actual=BatchCase(true,keys,ease,td,scenario);
                Require(actual==baseline,"resident parity keys="+string.Join(",",keys)+" ease="+ease+" td="+td+" scenario="+scenario+"\n"+baseline+"\n"+actual);
                count++;
            }
            Console.WriteLine("BATCH PASS actual UpdatePrefix parity cases="+count+" (7 frames, merge/order/scale startup/color/opacity/hidden/Complete/native fallback/zero dt)");
            // Force slot holes, natural completion, live admission failure and
            // re-enrollment in one run; a released schedule entry must never
            // advance a newly reused slot a second time.
            BatchRestoreAll();recs.Clear();batchVerify=false;batchOn=true;GroupEnabled=false;PairXY=true;
            var dummy=new scrVisualDecoration{Deferred=true};
            for(int wave=0;wave<40;wave++)
            {
                foreach(int key in new[]{1,2,5,7,8})recs.Add(new Rec{D=dummy,Key=key,E=Ease.OutBounce,Dur=.025f,Over=1.7f,FEnd=3,VEnd=new Vector2(3,4),BatchOrder=++batchOrder,InList=true});
                dummy.Deferred=true;UpdatePrefix();dummy.Deferred=wave%2==0;UpdatePrefix();UpdatePrefix();
                Require(batchCount==0,"slot leak wave "+wave);
                Require(batchDecorationIds.Count==0,"decoration leak wave "+wave);
            }
            var stable=new Rec{D=dummy,Key=5,E=Ease.Linear,Dur=1000000,FEnd=4,BatchOrder=++batchOrder,InList=true};
            dummy.Deferred=true; recs.Add(stable);UpdatePrefix();
            for(int f=0;f<100;f++)UpdatePrefix();
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int f=0;f<10000;f++)UpdatePrefix();
            long alloc=GC.GetAllocatedBytesForCurrentThread()-before;
            Require(alloc==0,"batch steady allocation "+alloc);
            BatchForget();Require(batchCount==0 && batchDecorationIds.Count==0 && recs.Count==0,"forget leak");
            var fault=new Rec{D=dummy,Key=5,E=Ease.Linear,Dur=1,FStart=2,FEnd=4,BatchOrder=++batchOrder,InList=true};
            recs.Add(fault);UpdatePrefix();BatchSync(fault.BatchSlot);
            var expectedFault=new GroupExpected(fault,Time.deltaTime);
            bs[fault.BatchSlot]=999;batchVerify=true;batchSerial=15;
            UpdatePrefix();
            Require(batchMismatch>0 && B(dummy.Rot)==B(expectedFault.Rot),"calculation mismatch fallback");
            Require(!BatchAllowed(),"mismatch must disable resident path");
            BatchForget();batchMismatch=0;batchFirst="none";
            batchOn=false;
            Console.WriteLine("BATCH lifecycle PASS 40 reuse/fallback/completion waves; steady 10000 frames allocated="+alloc+"; Forget references=0; injected mismatch restored baseline and disabled batch");
            PairXY=false; batchVerify=false;
        }
        internal static void Main() {
            BatchTests();
            int cases=0;
            int[][] orders={new[]{1,2,5,7,8},new[]{7,8,1,2,5},new[]{8,7,5,2,1},new[]{1,5,7},new[]{7,8},new[]{1,2}};
            foreach(var keys in orders)foreach(bool deferred in new[]{false,true})
            foreach(Ease ease in new[]{Ease.Linear,Ease.OutCubic,Ease.InCubic,Ease.OutBack,Ease.OutBounce,Ease.Flash})
            foreach(float dt in new[]{0.016f,0.5f,1f,1.1f,-0.02f})
            foreach(int fail in new[]{0,1,2,3,4,5,6,7,8}) {
                Setup(deferred,ease,fail,false,0,1,keys); Tick(false,dt); Tick(false,dt); string expected=Signature();
                Setup(deferred,ease,fail,false,0,1,keys); Tick(true,dt); Tick(true,dt);
                Require(expected==Signature(),"parity "+string.Join(",",keys)+" "+ease+" dt="+dt+" fail="+fail);cases++;
            }
            foreach(bool grouped in new[]{false,true}) {
                Setup(false,Ease.OutCubic,0,true,0,1,orders[0]);Tick(grouped,0.016f);
                if(!grouped)mutationExpected=Signature();else Require(mutationExpected==Signature(),"setter state mutation");
            }
            string[] fields={"Pos","Dur","Over","Period","Started","Running","E"};
            foreach(string field in fields) {
                Setup(false,Ease.OutCubic,0,false,0,1,orders[0]); var r=recs[1];
                if(field=="Pos")r.Pos=-0f; if(field=="Dur")r.Dur=2; if(field=="Over")r.Over=0;
                if(field=="Period")r.Period=-0f; if(field=="Started")r.Started=true;
                if(field=="Running")r.Running=false; if(field=="E")r.E=Ease.Linear;
                Require(GroupCount(0)==1,"state guard "+field);
            }
            Setup(false,Ease.Linear,0,false,0,1,orders[0]); NativeMotion.Enabled=true; activeNativeEpoch=1; recs[0].NativeEpoch=1;
            Require(GroupCount(0)==1,"native guard");NativeMotion.Enabled=false;
            Setup(false,Ease.Linear,0,false,0,1,new[]{1,1,2});Require(GroupCount(0)==1,"duplicate guard");
            Setup(false,Ease.Linear,0,false,0,1,orders[0]);recs[2].D=new scrDecoration();GroupCensus();
            Require(groupSeparated>0,"noncontiguous census");
            ResetGroup();groupProbe=groupVerify=true;
            for(int i=0;i<128;i++){Setup((i&1)==0,Ease.OutCubic,0,false,0,1,orders[0]);Tick(true,i<64?0.016f:1f);}
            Require(groupChecks==10 && groupMismatch==0,"verification oracle "+GroupStat());
            PairXY=true;
            int pairCases=0;
            foreach(bool deferred in new[]{false,true})foreach(Ease ease in new[]{Ease.Linear,Ease.OutCubic,Ease.OutBack,Ease.OutBounce,Ease.Flash})
            foreach(float dt in new[]{0.016f,0.5f,1f,1.1f,-0.02f})foreach(var keys in orders) {
                groupProbe=groupVerify=false;
                Setup(deferred,ease,0,false,0,1,keys);Tick(false,dt);Tick(false,dt);string expected=Signature();
                Setup(deferred,ease,0,false,0,1,keys);Tick(true,dt);Tick(true,dt);
                Require(expected==Signature(),"pair parity "+string.Join(",",keys)+" "+ease+" dt="+dt);pairCases++;
            }
            foreach(bool grouped in new[]{false,true}) {
                Setup(false,Ease.OutCubic,0,true,0,1,orders[0]);Tick(grouped,.016f);
                if(!grouped)mutationExpected=Signature();else Require(mutationExpected==Signature(),"pair setter state mutation");
            }
            foreach(float dt in new[]{.016f,1f})foreach(bool grouped in new[]{false,true}) {
                Setup(true,Ease.OutBack,0,false,.25f,1,orders[0]);
                foreach(var r in recs){Startup(r);r.FStart+=r.Key;r.FChange-=r.Key*.5f;}
                Tick(grouped,dt);
                if(!grouped)mutationExpected=Signature();else Require(mutationExpected==Signature(),"already-started pair");
            }
            foreach(bool grouped in new[]{false,true}) {
                Setup(false,Ease.OutCubic,0,false,0,1,orders[0]); recs[1].Dur=2;recs[1].E=Ease.InCubic;
                Tick(grouped,.5f);
                if(!grouped)mutationExpected=Signature();else Require(mutationExpected==Signature(),"singleton keeps baseline pair");
            }
            ResetGroup();groupProbe=groupVerify=true;
            for(int i=0;i<128;i++){Setup((i&1)==0,Ease.OutCubic,0,false,0,1,orders[0]);Tick(true,i<64?0.016f:1f);}
            Require(groupChecks==12 && groupMismatch==0 && groupVerifyErrors==0,"pair oracle "+GroupStat());
            Require(pairCalls==1 && GroupV(pairInput,new Vector2(-17.75f,-17.75f)) && GroupV(pairOffset,new Vector2(.125f,-.75f)),"setPos input");
            groupProbe=groupVerify=false;
            // A failed combined write resumes through scalar safety boundaries.
            PairXY=false;Setup(false,Ease.OutCubic,2,false,0,1,orders[0]);Tick(false,.016f);string scalarFailure=Signature();
            PairXY=true;Setup(false,Ease.OutCubic,2,false,0,1,orders[0]);pairThrows=true;Tick(true,.016f);
            Require(scalarFailure==Signature() && pairCalls==1,"pair exception scalar fallback");
            PairXY=false;
            groupProbe=groupVerify=false;
            Setup(true,Ease.OutCubic,0,false,0,1,orders[0]);Tick(true,0);
            for(int i=0;i<100;i++){trace.Clear();Tick(true,0);}
            long allocationStart=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<10000;i++){trace.Clear();Tick(true,0);}
            long allocated=GC.GetAllocatedBytesForCurrentThread()-allocationStart;
            Require(allocated==0,"disabled probe allocation="+allocated);
            Setup(false,Ease.OutCubic,0,false,0,1,new[]{1,2});
            foreach(var r in recs)r.D=null;
            groupProbe=groupVerify=true;groupSerial=63;Tick(true,0.016f);
            Require(groupVerifyErrors==1 && errors==2 && Steps==2 && Completed==0,"oracle exception isolation");
            foreach(var r in recs)Require(r.Started && !r.Running && B(r.Pos)==B(0.016f),"oracle must not alter failure state");
            // No Verify/Census/Stopwatch calls are performed by StepGroup with probe off.
            Console.WriteLine("PASS scalar cases="+cases+" pair cases="+pairCases+"; state guards=7; native/duplicate/separated/mutation/oracle-exception/pair-fallback PASS; probe-off 10000 frames allocated="+allocated);
            BenchOffline();
            FastOffline();
        }
        private static void FastOffline()
        {
            fastCompatible=true; FastLane=FastInline=FastCache=true;
            benchStubs=true; mutation=false; PairXY=false; fastVerify=true;
            int cases=0;
            foreach(int[] keys in new[]{new[]{5,7,8},new[]{8,7,5},new[]{7,7,8},new[]{5,1,8}})
            foreach(bool deferred in new[]{false,true})
            foreach(Ease ease in (Ease[])Enum.GetValues(typeof(Ease)))
            foreach(float dt in new[]{0f,.016f,1f,1.1f,-.02f})
            {
                if(ease==Ease.INTERNAL_Custom || ease==Ease.INTERNAL_Zero)continue;
                Setup(deferred,ease,0,false,0,1,keys); foreach(var r in recs)GroupIndividual(r,dt); string expected=Signature();
                Setup(deferred,ease,0,false,0,1,keys); FastInvalidate();
                foreach(var r in recs){Steps++;if(!FastTryStep(r,dt))Step(r,dt);}
                Require(expected==Signature(),"fast parity "+ease+" "+dt+" "+string.Join(",",keys));cases++;
            }
            Require(fastMismatch==0,"fast verify mismatch");
            Console.WriteLine("FAST PASS cases="+cases+" "+FastCommand("state"));
            // Exercise the actual production compactor and sticky dirty gate.
            for(int mask=0;mask<256;mask++){
                recs.Clear();fastDirty=false;
                var dead=new List<Rec>();var alive=new List<Rec>();
                for(int i=0;i<8;i++){
                    var r=new Rec{Running=(mask&(1<<i))==0,InList=true};recs.Add(r);
                    if(r.Running)alive.Add(r);else {dead.Add(r);fastDirty=true;}
                }
                Require(FastMaySkipCompact()==(mask==0),"compact gate");
                // All InList bits must survive until the end-of-frame pass.
                foreach(var r in dead)Require(r.InList,"early reuse");
                Compact();Require(recs.Count==alive.Count,"compact size");
                for(int i=0;i<recs.Count;i++)Require(ReferenceEquals(recs[i],alive[i]),"compact stable order");
                foreach(var r in dead)Require(!r.InList,"removed InList");
            }
            recs.Clear();recs.Add(new Rec{Running=false,InList=true});fastDirty=false;fastSerial=63;
            Require(!FastMaySkipCompact() && !FastLane && fastMismatch==1,"compact verify catches omitted dirty");
            FastLane=true;fastMismatch=0;fastVerify=false;Compact();
            Console.WriteLine("FAST COMPACT PASS 256 stable-order/InList cases; verify fail-closed PASS");
            int updateCases=0;
            foreach(bool grouped in new[]{false,true})foreach(bool pair in new[]{false,true})
            foreach(float dt in new[]{0f,.016f,1f,1.1f,-.02f})foreach(int fail in new[]{0,1,2,4})
            {
                string baseline=null;
                for(int mode=0;mode<2;mode++){
                    Setup(false,Ease.OutCubic,fail,false,0,1,new[]{1,2,5,7,8});
                    GroupEnabled=grouped;PairXY=pair;benchStubs=false;FastLane=mode==1;fastDirty=false;
                    UnityEngine.Time.deltaTime=UnityEngine.Time.smoothDeltaTime=dt;
                    UpdatePrefix();UpdatePrefix();
                    string state=Signature();
                    if(mode==0)baseline=state;else Require(state==baseline,"UpdatePrefix compact parity");
                }
                updateCases++;
            }
            GroupEnabled=false;FastLane=true;benchStubs=true;
            string callbackBaseline=null;
            for(int mode=0;mode<2;mode++){
                Setup(false,Ease.OutCubic,0,false,0,1,new[]{5,7,8});
                FastLane=mode==1;PairXY=false;benchStubs=false;fastDirty=false;
                UnityEngine.Time.deltaTime=UnityEngine.Time.smoothDeltaTime=.016f;
                onHit=()=>{
                    Complete(recs[0]);
                    Require(recs[0].InList,"completed record released inside callback");
                    recs.Add(new Rec{D=recs[1].D,Key=5,Dur=1,E=Ease.OutCubic,FEnd=12,InList=true});
                };
                // Setup's records need the same list membership as NewRec.
                foreach(var r in recs)r.InList=true;
                UpdatePrefix();
                if(mode==0)callbackBaseline=Signature();else Require(callbackBaseline==Signature(),"append + complete earlier record");
            }
            FastLane=true;benchStubs=true;
            Console.WriteLine("FAST UPDATE PASS actual UpdatePrefix cases="+updateCases+" (pair/group/dt/exception)");
            Console.WriteLine("FAST UPDATE callback append/earlier Complete/InList timing PASS");
            fastVerify=false;
            const int n=2150,repeat=200;
            foreach(int key in new[]{5,7,8,14,15,16})
            {
                recs.Clear();
                benchPairs.Clear();
                for(int i=0;i<n;i++)recs.Add(new Rec{D=new scrDecoration{Deferred=true},Key=key==14?1:key,Dur=1000000,E=Ease.OutCubic,FEnd=15,VEnd=new Vector2(2,3),Over=1.70158f,InList=true});
                if(key==14)foreach(var r in recs)benchPairs[r]=new Rec{D=r.D,Key=2,Dur=r.Dur,E=r.E,FEnd=r.FEnd,Over=r.Over};
                var samples=new double[7];
                string line="FAST BENCH key="+key+" us/call baseline/fast median=";
                for(int mode=0;mode<2;mode++)
                {
                    for(int warm=0;warm<10;warm++)BenchFastTick(key,mode);
                    for(int s=0;s<7;s++){
                        long t=System.Diagnostics.Stopwatch.GetTimestamp();
                        for(int f=0;f<repeat;f++)BenchFastTick(key,mode);
                        samples[s]=(System.Diagnostics.Stopwatch.GetTimestamp()-t)*1e6/System.Diagnostics.Stopwatch.Frequency/repeat/n;
                    }
                    Array.Sort(samples);line+=samples[3].ToString("F4")+"/";
                }
                Console.WriteLine(line);
            }
            recs.Clear();GroupEnabled=false;PairXY=false;fastVerify=false;
            for(int i=0;i<6417;i++)recs.Add(new Rec{D=new scrDecoration{Deferred=true},Key=5,Dur=1000000,E=Ease.OutCubic,FEnd=15});
            UnityEngine.Time.deltaTime=UnityEngine.Time.smoothDeltaTime=1f/120;
            foreach(bool enabled in new[]{false,true}){
                FastLane=enabled;fastDirty=false;
                for(int warm=0;warm<100;warm++)UpdatePrefix();
                var samples=new double[7];long alloc=GC.GetAllocatedBytesForCurrentThread();
                for(int s=0;s<7;s++){
                    long t=System.Diagnostics.Stopwatch.GetTimestamp();
                    for(int f=0;f<repeat;f++)UpdatePrefix();
                    samples[s]=(System.Diagnostics.Stopwatch.GetTimestamp()-t)*1000.0/System.Diagnostics.Stopwatch.Frequency/repeat;
                }
                alloc=GC.GetAllocatedBytesForCurrentThread()-alloc;Array.Sort(samples);
                Require(alloc==0,"production Update allocation");
                Console.WriteLine("FAST WHOLE UPDATE enabled="+enabled+" 6417 records ms/frame median="+samples[3].ToString("F4")+" allocated="+alloc);
            }
        }
        private static void BenchFastTick(int key,int mode)
        {
            if(key==16){if(mode==0)Compact();else if(fastDirty)Compact();return;}
            foreach(var r in recs){
                if(key==15){if(!r.Running)throw new Exception();continue;}
                if(key==14){var next=benchPairs[r];StepPair(r,next,1f/120);continue;}
                fastLast=r.D;
                if(mode==0)Step(r,1f/120);else FastTryStep(r,1f/120);
            }
        }
        private static readonly Dictionary<Rec,Rec> benchPairs=new Dictionary<Rec,Rec>();
        private static void BenchOffline() {
            const int groups=2150,repeat=200;
            groupProbe=groupVerify=false; PairXY=true;throwCall=0;mutation=false;benchStubs=true;trace.Clear();
            recs.Clear();
            for(int i=0;i<groups;i++) {
                var d=new scrDecoration{Deferred=true};
                foreach(int key in new[]{1,2,5,7,8})recs.Add(new Rec{D=d,Key=key,E=Ease.OutCubic,Dur=1000000,Over=1.70158f,FStart=2,FEnd=15,VEnd=new Vector2(2,3)});
            }
            setPos=(d,v,off)=>d.Pivot=v;
            var samples=new double[7];
            for(int warm=0;warm<50;warm++)Tick(true,1f/120);
            long allocated=GC.GetAllocatedBytesForCurrentThread();
            for(int s=0;s<samples.Length;s++) {
                long start=System.Diagnostics.Stopwatch.GetTimestamp();
                for(int frame=0;frame<repeat;frame++)Tick(true,1f/120);
                samples[s]=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1e6/System.Diagnostics.Stopwatch.Frequency/repeat/groups;
            }
            allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
            Array.Sort(samples);
            Require(allocated==0,"pair bench allocation="+allocated);
            Console.WriteLine("OFFLINE .NET8 managed stubs only (NO Unity Transform/Mono): GroupCount+StepGroup 2150x5 R=200 samples=7 us/group median="+samples[3].ToString("F4")+" min="+samples[0].ToString("F4")+" max="+samples[6].ToString("F4")+" allocated="+allocated);
            for(int warm=0;warm<50;warm++)Tick(false,1f/120);
            for(int s=0;s<samples.Length;s++) {
                long start=System.Diagnostics.Stopwatch.GetTimestamp();
                for(int frame=0;frame<repeat;frame++)Tick(false,1f/120);
                samples[s]=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1e6/System.Diagnostics.Stopwatch.Frequency/repeat/groups;
            }
            Array.Sort(samples);
            Console.WriteLine("OFFLINE same managed stubs baseline StepPair+3 Step us/group median="+samples[3].ToString("F4")+" min="+samples[0].ToString("F4")+" max="+samples[6].ToString("F4"));
        }
        private static string mutationExpected;
    }
}
