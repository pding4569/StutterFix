using System;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using UnityEngine;

namespace UnityEngine {
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
class scrDecoration { internal Vector2 Pivot=new Vector2(3,-2), Scale=new Vector2(2,4); internal float Rot=7; internal bool Deferred; }
class scrParallax { internal Vector2 multiplier; }
namespace HarmonyLib {
    static class AccessTools { internal const BindingFlags all=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance; }
    sealed class PatchInfo { internal string[] Owners=Array.Empty<string>(); }
    static class Harmony { internal static PatchInfo GetPatchInfo(MethodBase m)=>null; }
}
namespace StutterFix {
    static class NativeMotion { internal static bool Enabled,Failed; }
    static class Edition { internal const bool Dev=false; }
    static class MoveApply { internal static bool DeferTransform(scrDecoration d)=>d.Deferred; }
    static class ZeroTween {
        private delegate float EvalFn(Ease e,EaseFunction f,float t,float d,float o,float p);
        private static readonly EvalFn eval=(EvalFn)Delegate.CreateDelegate(typeof(EvalFn),typeof(Ease).Assembly.GetType("DG.Tweening.Core.Easing.EaseManager").GetMethod("Evaluate",HarmonyLib.AccessTools.all,null,new[]{typeof(Ease),typeof(EaseFunction),typeof(float),typeof(float),typeof(float),typeof(float)},null));
        internal static float Eval(Ease e,float t,float d,float o,float p)=>eval(e,null,t,d,o,p);
    }
    internal static partial class DecoAnim {
        private static readonly List<Rec> recs=new List<Rec>();
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
        private static Vector2 pivotOffRef(scrDecoration d)=>new Vector2(0.125f,-0.75f);
        private static scrParallax parallaxRef(scrDecoration d)=>new scrParallax();
        private static void Error(Exception ex){errors++;}
        private static void Apply(Rec r,float to,bool native=false)=>ApplyE(r,ZeroTween.Eval(r.E,to,r.Dur,r.Over,r.Period));
        private static void Hit(int key) {
            if(benchStubs)return;
            trace.Add(key); calls++;
            if(mutation && calls==1) { recs[2].Pos+=0.25f; recs[3].Running=false; }
            if(calls==throwCall)throw new InvalidOperationException("injected setter");
        }
        private static void setPosX(scrDecoration d,float f,Vector2 off){Hit(1);d.Pivot.x=f;}
        private static void setPosY(scrDecoration d,float f,Vector2 off){Hit(2);d.Pivot.y=f;}
        private static void setRot(scrDecoration d,float f){Hit(5);d.Rot=f;}
        private static void setScale(scrDecoration d,Vector2 f){Hit(7);d.Scale=f;}
        private static void setPivX(scrDecoration d,float f){}
        private static void setPivY(scrDecoration d,float f){}
        private static void setParOffX(scrDecoration d,float f){}
        private static void setParOffY(scrDecoration d,float f){}
        private static void setOpa(scrDecoration d,float f){}
        private static void setCol(scrDecoration d,Color f){}

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
        internal static void Main() {
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
        }
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
