#if DEV || AUTOTEST || GPUDECO
using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

namespace StutterFix
{
    internal static partial class DecoAnim
    {
        static long gpuCreationSerial;
        internal static int GpuCpuUpdateFrame=-1;
        internal static long NextGpuCreationOrder()=>++gpuCreationSerial;
#if !DEV && !AUTOTEST
        internal static void ClearGpuRecordMetadata() => GpuDecoRecordStore.Clear();
#endif

        internal sealed class GpuResumeSpec
        {
            internal scrDecoration D;
            internal int Key,Source,StartFrame,Generation;
            internal long Order,EngineOrder;
            internal double StartSong;
            internal int CaptureEpoch;
            internal bool OrderObserved=true;
            internal float Duration,Position,Over,Period;
            internal Ease Ease;
            internal readonly float[] Start=new float[4],Target=new float[4],Change=new float[4];
        }

        // Boundary only: release resident representations before re-registering
        // records. NewRec must not attach an incompletely seeded resident slot.
        internal static void BeginGpuResume()
        {
            DecoEngine.RestoreAll();
            if(batchCount!=0)BatchRestoreAll();
            Compact();
        }
        internal static void ResumeGpuRecord(GpuResumeSpec s)
        {
            if(!s.D || s.D.eventTweens==null || !(s.Duration>0f) || s.Position<0f || s.Position>=s.Duration)
                throw new InvalidOperationException("GPU resume lifetime contract");
            if(s.D.eventTweens.TryGetValue((global::TweenType)s.Key,out var previous)
                && previous?.id is Rec old && old.Running)
                throw new InvalidOperationException("GPU resume would replace a running CPU record");
            var r=NewRec(s.D,s.D.eventTweens,s.Key,s.Duration,s.Ease);
            r.Pos=s.Position;r.Over=s.Over;r.Period=s.Period;r.Started=true;
            r.Engine=null;r.OwnerHint=null;r.EngineOrder=s.EngineOrder;r.GpuOrder=s.Order;r.GpuOrderObserved=s.OrderObserved;
            r.GpuSource=s.Source;r.GpuStartFrame=s.StartFrame;r.GpuGeneration=s.Generation;
            r.GpuStartSong=s.StartSong;r.GpuCaptureEpoch=s.CaptureEpoch;
            r.EngineSkipFrame=GpuCpuUpdateFrame;
            if(s.Key==9)
            {
                r.CStart=new Color(s.Start[0],s.Start[1],s.Start[2],s.Start[3]);
                r.CEnd=new Color(s.Target[0],s.Target[1],s.Target[2],s.Target[3]);
                r.CChange=new Color(s.Change[0],s.Change[1],s.Change[2],s.Change[3]);
            }
            else if(s.Key==7 || s.Key==8 || s.Key==11)
            {
                r.VStart=new Vector2(s.Start[0],s.Start[1]);r.VEnd=new Vector2(s.Target[0],s.Target[1]);
                r.VChange=new Vector2(s.Change[0],s.Change[1]);
            }
            else {r.FStart=s.Start[0];r.FEnd=s.Target[0];r.FChange=s.Change[0];}
        }
        internal static void EndGpuResume()
        {
            // Includes unowned legacy records; restore creation order across
            // both sets, then give future batch schedules the same order.
            recs.Sort((a,b)=>a.GpuOrder.CompareTo(b.GpuOrder));
            if(batchOn)foreach(var r in recs)r.BatchOrder=++batchOrder;
            fastDirty=true;DecoEngine.Dirty=true;
        }
        internal static GpuResumeSpec SnapshotGpuRecord(Rec r)
        {
            var s=new GpuResumeSpec{D=r.D,Key=r.Key,Duration=r.Dur,Position=r.Pos,Ease=r.E,Over=r.Over,Period=r.Period,
                Order=r.GpuOrder,EngineOrder=r.EngineOrder,Source=r.GpuSource,StartFrame=r.GpuStartFrame,StartSong=r.GpuStartSong,
                Generation=r.GpuGeneration,CaptureEpoch=r.GpuCaptureEpoch,OrderObserved=r.GpuOrderObserved};
            if(r.Key==9)for(int c=0;c<4;c++){s.Start[c]=r.CStart[c];s.Target[c]=r.CEnd[c];s.Change[c]=r.CChange[c];}
            else if(r.Key==7 || r.Key==8 || r.Key==11)for(int c=0;c<2;c++){s.Start[c]=r.VStart[c];s.Target[c]=r.VEnd[c];s.Change[c]=r.VChange[c];}
            else {s.Start[0]=r.FStart;s.Target[0]=r.FEnd;s.Change[0]=r.FChange;}
            return s;
        }
#if AUTOTEST
        internal static string GpuCpuCensus(Func<scrDecoration,bool> owned,Func<scrDecoration,string> exclusion=null)
        {
            int running=0,ownedRunning=0;
            var residual=new Dictionary<scrDecoration,int>();
            foreach(var r in BatchView())if(r.Running && r.D)
            {
                running++;
                if(owned(r.D))ownedRunning++;
                else {residual.TryGetValue(r.D,out int mask);residual[r.D]=mask|(1<<r.Key);}
            }
            var reasons=new SortedDictionary<string,int>(StringComparer.Ordinal);
            var channels=new SortedDictionary<int,int>();
            foreach(var row in residual)
            {
                string reason=exclusion?.Invoke(row.Key)??"not_classified";
                reasons.TryGetValue(reason,out int n);reasons[reason]=n+1;
                channels.TryGetValue(row.Value,out int c);channels[row.Value]=c+1;
            }
            return "CPU_census running_recs="+running+" owned_running_recs="+ownedRunning
                +" residual_decorations="+residual.Count
                +" residual_reasons="+string.Join(";",reasons.Select(p=>p.Key+":"+p.Value))
                +" residual_channel_masks="+string.Join(",",channels.Select(p=>p.Key+":"+p.Value))
                +" scope=boundary_records_not_transform_calls_or_GPU_time";
        }
        // Synchronous paused diagnostic. It exercises the actual CPU constructors,
        // setters, completion and replacement on one otherwise idle decoration.
        // It does NOT exercise GPU channel extraction or prove whole-song identity.
        internal static string GpuResumeKeyFixture(scrDecoration d,Action gpuTransition=null)
        {
            if(!FrameCompare.Paused || !d || d.eventTweens==null || d.eventTweens.Count!=0 || !HasParallax(d)
                || batchOn || DecoEngine.Enabled || resumeAuditOn || !CanPivotParallax)
                throw new InvalidOperationException("key fixture requires paused idle decoration, no resident engine/audit");
            BeginGpuResume();InvisibleSkip.ApplyAllLazy();
            var saved=ResumeFixtureValues(d);
            bool oldPos=PosFast.Enabled,oldDefer=MoveApply.DeferRotScale;
            int cases=0,comparisons=0,mismatch=0;
            string first="none";
            var keys=gpuTransition==null?new[]{1,2,3,4,5,7,8,9,10,11,12,13}:new[]{3,4,10,11,12,13};
            var eases=gpuTransition==null?new[]{Ease.Linear,Ease.InOutCubic,Ease.OutBack,Ease.OutElastic}:new[]{Ease.OutBack};
            void Restore()
            {
                foreach(var t in d.eventTweens.Values) if(t?.id is Rec r) {r.Running=false;fastDirty=true;}
                d.eventTweens.Clear();Compact();
                setPivX(d,saved[2]);setPivY(d,saved[3]);
                setParOffX(d,saved[14]);setParOffY(d,saved[15]);
                setPos(d,new Vector2(saved[0],saved[1]),new Vector2(saved[2],saved[3]));
                setRot(d,saved[4]);setScale(d,new Vector2(saved[5],saved[6]));
                setCol(d,new Color(saved[7],saved[8],saved[9],saved[10]));setOpa(d,saved[11]);
                parallaxRef(d).multiplier=new Vector2(saved[12],saved[13]);
                InvisibleSkip.ApplyAllLazy();d.UpdatePosition();
            }
            Rec Make(int key,Ease ease)
            {
                const float dur=.55f;
                if(key<=2)Pos(d,d.eventTweens,key,0f,saved[key-1]+.75f,dur,ease);
                else if(key<=4)Piv(d,d.eventTweens,key,saved[key-1]+.125f,dur,ease);
                else if(key==5)Rot(d,d.eventTweens,saved[4]+37.25f,dur,ease);
                else if(key<=8)Scale(d,d.eventTweens,key,new Vector2(saved[5]+.3f,saved[6]+.2f),dur,ease);
                else if(key==9)Col(d,d.eventTweens,new Color(.3f,.6f,.8f,.4f),dur,ease);
                else if(key==10)Opa(d,d.eventTweens,.35f,dur,ease);
                else if(key==11)Par(d,d.eventTweens,new Vector2(.75f,.9f),dur,ease);
                else ParOff(d,d.eventTweens,key,saved[key+2]+.125f,dur,ease);
                return (Rec)d.eventTweens[(global::TweenType)key].id;
            }
            try
            {
                PosFast.Enabled=false;MoveApply.DeferRotScale=false;
                foreach(int key in keys)
                foreach(var ease in eases)
                foreach(int mode in new[]{0,1,2})
                {
                    Restore();var original=Make(key,ease);Step(original,.23f);
                    if(mode==1)original.GpuOrderObserved=false; // Inject unknown provenance; it must stay unknown.
                    var s=ResumeFixtureSpec(original);
                    var values=new List<float[]>();var positions=new List<float>();var running=new List<bool>();
                    var deltas=new[]{.013f,.007f,.18f,.3f};
                    if(mode==0)foreach(float dt in deltas)
                    {Step(original,dt);InvisibleSkip.ApplyAllLazy();values.Add(ResumeFixtureValues(d));positions.Add(original.Pos);running.Add(original.Running);}
                    else
                    {
                        original.Proxy.Kill(true);
                        if(mode==2){original=Make(key,Ease.OutQuad);Step(original,.03125f);}
                        InvisibleSkip.ApplyAllLazy();values.Add(ResumeFixtureValues(d));positions.Add(original.Pos);running.Add(original.Running);
                    }
                    Restore();ResumeGpuRecord(s);EndGpuResume();
                    gpuTransition?.Invoke();
                    var restored=(Rec)d.eventTweens[(global::TweenType)key].id;
                    comparisons++;
                    if(restored.GpuOrderObserved!=s.OrderObserved){mismatch++;if(first=="none")first="order_provenance_key"+key;}
                    for(int i=0;i<values.Count;i++)
                    {
                        if(mode==0)Step(restored,deltas[i]);
                        else {restored.Proxy.Kill(true);if(mode==2){restored=Make(key,Ease.OutQuad);Step(restored,.03125f);}}
                        InvisibleSkip.ApplyAllLazy();var actual=ResumeFixtureValues(d);
                        bool ok=SameResume(restored.Pos,positions[i]) && restored.Running==running[i];comparisons+=2;
                        for(int c=0;c<actual.Length;c++){comparisons++;if(!SameResume(actual[c],values[i][c]))ok=false;}
                        if(!ok){mismatch++;if(first=="none")first="key"+key+"_ease"+(int)ease+"_mode"+mode+"_step"+i;}
                    }
                    cases++;
                }
            }
            finally
            {
                try {Restore();var actual=ResumeFixtureValues(d);for(int i=0;i<saved.Length;i++)if(!SameResume(saved[i],actual[i]))mismatch++;}
                finally {PosFast.Enabled=oldPos;MoveApply.DeferRotScale=oldDefer;}
            }
            string result="resume_key_fixture keys="+keys.Length+" cases="+cases+" comparisons="+comparisons+" mismatch="+mismatch+" first="+first
                +" remaining_test_tweens="+d.eventTweens.Count+" GPU_handoff="+(gpuTransition!=null)
                +" scope=paused_seeded_logical_lifecycle_not_pixels_or_performance";
            if(cases!=keys.Length*eases.Length*3 || mismatch!=0)throw new InvalidOperationException(result);
            return result;
        }
        static float[] ResumeFixtureValues(scrDecoration d)
        {
            var p=pivotPosRef(d);var o=pivotOffRef(d);var scale=scaleRef(d);var c=colRef(d);var par=parallaxRef(d).multiplier;var off=parOffRef(d);
            return new[]{p.x,p.y,o.x,o.y,rotRef(d),scale.x,scale.y,c.r,c.g,c.b,c.a,opaRef(d),par.x,par.y,off.x,off.y};
        }
        static GpuResumeSpec ResumeFixtureSpec(Rec r)=>SnapshotGpuRecord(r);
        sealed class ResumeAudit {internal GpuResumeSpec S;internal Rec R;internal float Position;}
        static readonly List<ResumeAudit> resumeAudit=new List<ResumeAudit>();
        static int resumeAuditFrames,resumeAuditMismatch,resumeAuditCompared,resumeAuditReplaced,resumeAuditContinued;
        static bool resumeAuditOn;
        static readonly int[] resumeAuditKeys=new int[16];
        static string resumeAuditScope="none";
        static bool SameResume(float a,float b)=>BitConverter.SingleToInt32Bits(a)==BitConverter.SingleToInt32Bits(b);
        internal static string GpuResumeAuditResult=>"resume_audit frames="+resumeAuditFrames+" compared="+resumeAuditCompared
            +" mismatch="+resumeAuditMismatch+" continued="+resumeAuditContinued+" replaced="+resumeAuditReplaced+" active="+resumeAuditOn
            +" keys="+string.Join(",",resumeAuditKeys)
            +" scope="+resumeAuditScope+" pixel_game=UNMEASURED";
        internal static void CancelGpuResumeAudit(){resumeAuditOn=false;resumeAudit.Clear();}
        internal static string TestGpuResumeRoundtrip(Action transition,Func<scrDecoration,bool> eligible)
        {
            CancelGpuResumeAudit();BeginGpuResume();
            resumeAuditScope="observed_Rec_roundtrip_and_next_CPU_values";
            resumeAuditFrames=resumeAuditMismatch=resumeAuditCompared=resumeAuditReplaced=resumeAuditContinued=0;
            Array.Clear(resumeAuditKeys,0,resumeAuditKeys.Length);
            foreach(var r in recs)
            {
                if(!r.Running || !r.Started || !r.GpuOrderObserved || r.Pos<0 || r.Pos>=r.Dur || !eligible(r.D)
                    || (uint)r.Key>=16 || resumeAuditKeys[r.Key]>=64)continue;
                var s=new GpuResumeSpec {D=r.D,Key=r.Key,Duration=r.Dur,Position=r.Pos,Ease=r.E,Over=r.Over,Period=r.Period,
                    Order=r.GpuOrder,EngineOrder=r.EngineOrder};
                if(r.Key==9)for(int c=0;c<4;c++){s.Start[c]=r.CStart[c];s.Target[c]=r.CEnd[c];s.Change[c]=r.CChange[c];}
                else if(r.Key==7 || r.Key==8 || r.Key==11)
                    for(int c=0;c<2;c++){s.Start[c]=r.VStart[c];s.Target[c]=r.VEnd[c];s.Change[c]=r.VChange[c];}
                else {s.Start[0]=r.FStart;s.Target[0]=r.FEnd;s.Change[0]=r.FChange;}
                resumeAudit.Add(new ResumeAudit{S=s,Position=r.Pos});
                resumeAuditKeys[r.Key]++;
            }
            return StartGpuResumeAudit(transition);
        }
        internal static string TestGpuResumeHandoff(IEnumerable<GpuResumeSpec> specs,Action transition)
        {
            CancelGpuResumeAudit();resumeAuditScope="observed_order_GPU_handoff_and_next_CPU_values";
            resumeAuditFrames=resumeAuditMismatch=resumeAuditCompared=resumeAuditReplaced=resumeAuditContinued=0;
            Array.Clear(resumeAuditKeys,0,resumeAuditKeys.Length);
            foreach(var s in specs)
            {
                if(!s.OrderObserved || resumeAuditKeys[s.Key]>=64)continue;
                resumeAuditKeys[s.Key]++;resumeAudit.Add(new ResumeAudit{S=s,Position=s.Position});
            }
            return StartGpuResumeAudit(transition);
        }
        static string StartGpuResumeAudit(Action transition)
        {
            if(resumeAudit.Count==0)throw new InvalidOperationException("resume audit has no observed active records");
            // Dispose intentionally drops diagnostic map references. Keep this
            // explicit test's samples locally, then arm its independent audit
            // after the destructive transition has finished.
            var samples=resumeAudit.ToArray();
            try {transition();}
            catch {CancelGpuResumeAudit();throw;}
            resumeAudit.Clear();resumeAudit.AddRange(samples);
            foreach(var a in resumeAudit)
            {
                var s=a.S;a.R=s.D.eventTweens.TryGetValue((global::TweenType)s.Key,out var t)?t?.id as Rec:null;
                var r=a.R;
                if(r==null || !r.Running || !r.Started || !SameResume(r.Pos,s.Position) || !SameResume(r.Dur,s.Duration)
                    || r.E!=s.Ease || !SameResume(r.Over,s.Over) || !SameResume(r.Period,s.Period)
                    || r.GpuOrder!=s.Order || r.EngineOrder!=s.EngineOrder){resumeAuditMismatch++;continue;}
                int count=s.Key==9?4:s.Key==11?2:1;
                for(int c=0;c<count;c++)
                {
                    int v=s.Key==8?1:c;
                    float start=s.Key==9?r.CStart[c]:s.Key==7 || s.Key==8 || s.Key==11?r.VStart[v]:r.FStart;
                    float end=s.Key==9?r.CEnd[c]:s.Key==7 || s.Key==8 || s.Key==11?r.VEnd[v]:r.FEnd;
                    float change=s.Key==9?r.CChange[c]:s.Key==7 || s.Key==8 || s.Key==11?r.VChange[v]:r.FChange;
                    if(!SameResume(start,s.Start[v]) || !SameResume(end,s.Target[v]) || !SameResume(change,s.Change[v]))resumeAuditMismatch++;
                }
            }
            resumeAuditOn=true;
            return "resume_roundtrip records="+resumeAudit.Count+" metadata_mismatch="+resumeAuditMismatch+" following_frames=12 performance=False";
        }
        static partial void GpuResumeAuditFrame(float dt)
        {
            if(!resumeAuditOn || dt<1e-6f && dt>-1e-6f)return;
            // Explicit diagnostic: sync resident storage once per frame, not
            // once per record. No claim about performance while this is active.
            BeginGpuResume();resumeAuditFrames++;
            foreach(var a in resumeAudit)
            {
                var s=a.S;var r=a.R;if(r==null)continue;
                if(!s.D || !s.D.eventTweens.TryGetValue((global::TweenType)s.Key,out var t) || !ReferenceEquals(t?.id,r) || r.GpuOrder!=s.Order)
                {resumeAuditReplaced++;a.R=null;continue;}
                a.Position=Math.Min(a.Position+dt,s.Duration);
                if(!SameResume(r.Pos,a.Position) || r.Running!=(a.Position<s.Duration))resumeAuditMismatch++;
                float e=ZeroTween.Eval(s.Ease,a.Position,s.Duration,s.Over,s.Period);
                int n=s.Key==9?4:s.Key==11?2:1;
                for(int c=0;c<n;c++)
                {
                    int v=s.Key==8?1:c;
                    float value=s.Start[v]+s.Change[v]*e;
                    if(a.Position>=s.Duration && s.Key!=7 && s.Key!=8 && s.Key!=11)value=s.Target[v];
                    float actual=s.Key==1?s.D.pivotPosVec.x:s.Key==2?s.D.pivotPosVec.y:s.Key==3?s.D.pivotOffsetVec.x
                        :s.Key==4?s.D.pivotOffsetVec.y:s.Key==5?s.D.rotAngle:s.Key==7?s.D.scaleVec.x:s.Key==8?s.D.scaleVec.y
                        :s.Key==9?s.D.color[c]:s.Key==10?s.D.opacity:s.Key==11?s.D.parallax.multiplier[c]
                        :s.Key==12?s.D.parallaxOffset.x:s.D.parallaxOffset.y;
                    resumeAuditCompared++;if(!SameResume(actual,value))resumeAuditMismatch++;
                }
                if(a.Position>s.Position)resumeAuditContinued++;
                if(a.Position>=s.Duration)a.R=null;
            }
            if(resumeAuditFrames>=12)
            {resumeAuditOn=false;Main.Entry.Logger.Log("[gpudeco] "+GpuResumeAuditResult);resumeAudit.Clear();}
        }
#endif
    }
}
#endif
