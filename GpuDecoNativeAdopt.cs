#if DEV || AUTOTEST || GPUDECO
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins;
using DG.Tweening.Plugins.Options;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        // Activation-only snapshots. Never resurrect a killed/recycled DOTween object.
        sealed class NativeAdoptSpec
        {
            internal scrVisualDecoration D;
            internal Dictionary<global::TweenType,Tween> Dictionary;
            internal int Master;
            internal readonly List<NativeAdoptChannel> Channels=new List<NativeAdoptChannel>();
        }
        sealed class NativeAdoptChannel
        {
            internal global::TweenType Key;
            internal int First;
            internal Tween Tween;
            internal Tween Replacement;
            internal object Closure,CompleteClosure,Root;
            internal Delegate Getter,Setter,OnUpdate,OnComplete;
            internal string Link;
            internal ffxMoveDecorationsPlus Fx;
            internal float Pos,Dur,Over,Period,Delay,ElapsedDelay;
            internal float[] Start,End,Change,Current;
            internal Ease Ease;
            internal bool IsScale=>Key==global::TweenType.ScaleX || Key==global::TweenType.ScaleY;
        }
        static readonly Dictionary<int,NativeAdoptSpec> gpuNativeAdoptions=new Dictionary<int,NativeAdoptSpec>();
        static readonly HashSet<int> gpuNativeSourceIds=new HashSet<int>();
        const BindingFlags NativeFields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        static readonly Guid NativeGameMvid=new Guid("c0baa18f-e115-4186-bf86-1b2288496232");
        static bool nativeUpdateHooked,nativeUpdateRunning,nativeActivationNeedsStep;
        static int nativeHookFrame=-1,nativeUpdateCompleted=-1;
        static int nativeSourceId=int.MinValue;
        static MethodInfo nativeUpdateMethod;
        static readonly Type NativeManager=typeof(Tween).Assembly.GetType("DG.Tweening.Core.TweenManager");
        static readonly Type NativeComponent=typeof(Tween).Assembly.GetType("DG.Tweening.Core.DOTweenComponent");
        static readonly Module NativeGameModule=typeof(ffxMoveDecorationsPlus).Module;
        static readonly bool NativeGameMatches=NativeGameModule.ModuleVersionId==NativeGameMvid;
        // Metadata/readers live for the process; patch/compatibility results and
        // manager state live only for this synchronous activation transaction.
        static class NativeField<T>
        {
            internal static readonly Dictionary<(Type,string),Func<object,T>> Readers=new Dictionary<(Type,string),Func<object,T>>();
        }
        static T NativeRead<T>(object value,string name,Type owner=null)
        {
            var type=owner??value.GetType();
            var key=(type,name);
            if(!NativeField<T>.Readers.TryGetValue(key,out var read))
            {
                var field=type.GetField(name,NativeFields|BindingFlags.Static)??throw new MissingFieldException(type.FullName,name);
                if(!typeof(T).IsAssignableFrom(field.FieldType))throw new InvalidCastException("native field type: "+name);
                try
                {
                    var dm=new DynamicMethod("StutterFix_Native_"+name,typeof(T),new[]{typeof(object)},typeof(DecoBake).Module,true);
                    var il=dm.GetILGenerator();
                    if(field.IsStatic)il.Emit(OpCodes.Ldsfld,field);
                    else {il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Castclass,field.DeclaringType);il.Emit(OpCodes.Ldfld,field);}
                    if(field.FieldType.IsValueType && typeof(T)!=field.FieldType)il.Emit(OpCodes.Box,field.FieldType);
                    il.Emit(OpCodes.Ret);
                    read=(Func<object,T>)dm.CreateDelegate(typeof(Func<object,T>));
                }
                catch {read=o=>(T)field.GetValue(o);} // Same fail-closed reads on runtimes without emit.
                NativeField<T>.Readers.Add(key,read);
            }
            return read(value);
        }
        static readonly Dictionary<(Type,string),MethodInfo> nativeMethods=new Dictionary<(Type,string),MethodInfo>();
        static readonly Dictionary<(MethodBase,bool),bool> nativePatches=new Dictionary<(MethodBase,bool),bool>();
        static MethodInfo NativeMethod(Type type,string name)
        {
            var key=(type,name);
            if(!nativeMethods.TryGetValue(key,out var method))
            {
                method=type==null?null:AccessTools.Method(type,name);
                nativeMethods.Add(key,method);
            }
            return method;
        }
        static MethodInfo nativeKillMethod;
        static readonly string[] NativeFlags={"isInverted","isIndependentUpdate","isFrom","isBlendable","isSpeedBased","isSequenced"};
        static IDictionary nativeLinks;
        static bool nativeManagerUpdating;
        enum NativeReject
        {
            None=0, UpdateRunning=1, ReadException=16,
            HookFrame=20, RunningRec, UnsupportedKey,
            Inactive=30, Id, Target, StringId, IntId, Uninitialized, NotPlaying, Complete,
            Relative, Backwards, TimeScale, DelayNonfinite, DelayNegative,
            ElapsedDelayNonfinite, ElapsedDelayNegative, DelayRemaining, DelayElapsedMismatch, Loops, CompletedLoops,
            Inverted=50, IndependentUpdate, From, Blendable, SpeedBased, Sequenced,
            UpdateType=60, LoopType, DelayIncomplete, AutoKill, SequenceParent, SpecialStartup,
            CustomEase, OnStart, OnKill, OnPlay, OnPause, OnRewind, OnStepComplete, OnWaypointChange,
            LinksUnavailable=80, Linked, ManagerUpdating, PluginType, PluginPatched, DespawnPatched, KillPatched,
            ScaleType=90, ScaleSnapping, ScaleAxis, ColorType, ColorAlphaOnly, FloatType, FloatSnapping,
            Getter=100, Setter, ScaleOnUpdate, ScaleOnComplete, OnUpdate, OnComplete, CompleteClosure,
            Decoration=110, Fx,
            PositionNonfinite=120, DurationNonfinite, OvershootNonfinite, PeriodNonfinite,
            StartNonfinite, EndNonfinite, ChangeNonfinite, CurrentNonfinite, ExpectedNonfinite, LogicalNonfinite,
            Duration=130, PositionNegative, PositionAtEnd, EaseUnset, EaseUnsupported, ScaleUnusedChange,
            EndMismatch=140, ChangeMismatch,
            ClosureValueMismatch=150, LogicalValueMismatch,
            QuartzOptimizer=160, ForeignSetter, Version, UpdatePatched
        }
        static NativeReject nativeGlobalReject;
        static long nativeCheckTicks,nativeCaptureTicks,nativeCommitTicks;
        static void BeginNativeAdoptionActivation()
        {
            nativeCheckTicks=nativeCaptureTicks=nativeCommitTicks=0;
            ResetNativeRejects();
            if(!DrawCollapsePolicy.AdoptNativeTweens)return;
            long started=Stopwatch.GetTimestamp();
            try
            {
                if(!nativeUpdateHooked)SetNativeAdoption(true);
                if(nativeKillMethod==null)nativeKillMethod=AccessTools.Method(typeof(TweenExtensions),"Kill",new[]{typeof(Tween),typeof(bool)});
                if(Compat.QuartzOptimizer)nativeGlobalReject=NativeReject.QuartzOptimizer;
                else if(ForeignSetter())nativeGlobalReject=NativeReject.ForeignSetter;
                else if(DOTween.Version!="1.2.765")nativeGlobalReject=NativeReject.Version;
                else if(NativePatched(nativeUpdateMethod,true))nativeGlobalReject=NativeReject.UpdatePatched;
                nativeLinks=NativeRead<IDictionary>(null,"_TweenLinks",NativeManager);
                nativeManagerUpdating=NativeRead<bool>(null,"isUpdateLoop",NativeManager);
                if(nativeGlobalReject==0 && nativeLinks==null)nativeGlobalReject=NativeReject.LinksUnavailable;
                if(nativeGlobalReject==0 && nativeManagerUpdating)nativeGlobalReject=NativeReject.ManagerUpdating;
                // Warm the shared method checks once, including all supported plugins.
                NativePatched(NativeMethod(NativeManager,"Despawn"));NativePatched(nativeKillMethod,true);
                NativePatched(NativeMethod(typeof(FloatPlugin),"EvaluateAndApply"));
                NativePatched(NativeMethod(typeof(Vector2Plugin),"EvaluateAndApply"));
                NativePatched(NativeMethod(typeof(ColorPlugin),"EvaluateAndApply"));
            }
            catch(Exception ex){nativeGlobalReject=NativeReject.ReadException;nativeLastEx=ex.GetType().Name+":"+ex.Message;}
            finally {nativeCheckTicks+=Stopwatch.GetTimestamp()-started;}
        }
        static void ClearNativeAdoptionActivation()
        {
            nativePatches.Clear();nativeLinks=null;nativeManagerUpdating=false;nativeGlobalReject=0;
        }
        static void LogNativeAdoptionTiming()
        {
#if AUTOTEST
            if(DrawCollapsePolicy.AdoptNativeTweens)
                Main.Entry.Logger.Log("[GPU 장식] 이어받기 검사 "+(nativeCheckTicks*1000.0/Stopwatch.Frequency).ToString("F3",Inv)
                    +"ms / 캡처 "+(nativeCaptureTicks*1000.0/Stopwatch.Frequency).ToString("F3",Inv)
                    +"ms / commit "+(nativeCommitTicks*1000.0/Stopwatch.Frequency).ToString("F3",Inv)+"ms");
#endif
        }
        static bool NativeBits(float a,float b)=>BitConverter.SingleToInt32Bits(a)==BitConverter.SingleToInt32Bits(b);
        static bool NativePatched(MethodBase method,bool allowOurs=false)
        {
            if(method==null)return true;
            var key=(method,allowOurs);
            if(nativePatches.TryGetValue(key,out bool patched))return patched;
            var info=Harmony.GetPatchInfo(method);
            patched=false;
            if(info!=null)foreach(string owner in info.Owners)
                if(!allowOurs || owner!=Main.Entry.Info.Id){patched=true;break;}
            nativePatches.Add(key,patched);return patched;
        }
        // Installed only on opt-in; the default-off path has no new frame hook.
        static void SetNativeAdoption(bool enabled)
        {
            RequireLive(!gpuLiveActive,"native adoption toggle requires inactive GPU ownership");
            if(enabled && !nativeUpdateHooked)
            {
                nativeUpdateMethod=NativeMethod(NativeComponent,"Update");
                RequireLive(nativeUpdateMethod!=null,"native update boundary unavailable");
                var h=new Harmony(Main.Entry.Info.Id);
                h.Patch(nativeUpdateMethod,
                    prefix:new HarmonyMethod(typeof(DecoBake),nameof(NativeUpdateBegin)){priority=Priority.First},
                    finalizer:new HarmonyMethod(typeof(DecoBake),nameof(NativeUpdateEnd)){priority=Priority.Last});
                nativeHookFrame=Time.frameCount;nativeUpdateCompleted=-1;nativeUpdateRunning=false;nativeUpdateHooked=true;
            }
            if(!enabled && nativeUpdateHooked)
            {
                var h=new Harmony(Main.Entry.Info.Id);
                h.Unpatch(nativeUpdateMethod,NativeMethod(typeof(DecoBake),nameof(NativeUpdateBegin)));
                h.Unpatch(nativeUpdateMethod,NativeMethod(typeof(DecoBake),nameof(NativeUpdateEnd)));
                nativeUpdateHooked=false;
            }
            DrawCollapsePolicy.AdoptNativeTweens=enabled;
        }
        static void NativeUpdateBegin()=>nativeUpdateRunning=true;
        static void NativeUpdateEnd(Exception __exception)
        {
            nativeUpdateRunning=false;
            if(__exception==null)nativeUpdateCompleted=Time.frameCount;
        }
        static bool NativePaused()
        {
#if DEV || AUTOTEST
            if(FrameCompare.Paused)return true;
#endif
            return ADOBase.controller && ADOBase.controller.paused || Time.timeScale==0f;
        }
        static bool NativeDelegate(Delegate value,int token,object target=null)
        {
            if(value==null || value.GetInvocationList().Length!=1)return false;
            var m=value.Method;
            return NativeGameMatches && m.Module==NativeGameModule
                && m.MetadataToken==token && value.Target!=null && value.Target.GetType()==m.DeclaringType
                && (target==null || ReferenceEquals(value.Target,target)) && !NativePatched(m);
        }
        static readonly Dictionary<string,int> nativeCombos=new Dictionary<string,int>();
        static readonly int[] nativeRejects=new int[200];
        static string nativeLastEx;
        static NativeReject nativeLastReject;
        static global::TweenType? nativeRejectChannel;
        // 방금 만들어져 아직 한 번도 갱신되지 않은 트윈(시작값 미정)은 이어받을 수 없다: 다음 프레임에 다시 시도하도록 활성화를 미룬다.
        internal static int nativeUninitSeen;
        static bool NR(NativeReject code){nativeLastReject=code;return false;}
        static void ResetNativeRejects()
        {
            Array.Clear(nativeRejects,0,nativeRejects.Length);nativeCombos.Clear();
            nativeLastEx=null;nativeLastReject=NativeReject.None;nativeRejectChannel=null;
        }
        static bool NativeRunning(Tween t)=>t!=null && (t.active || t.id is DecoAnim.Rec r && r.Running);
        static void RecordNativeCombo(scrVisualDecoration d)
        {
            // One first failure per decoration, including its failing channel.
            // Capture/commit rechecks must not inflate classification counts.
            nativeRejects[(int)nativeLastReject]++;
            var sb=new System.Text.StringBuilder();
            try
            {
                var parts=new List<KeyValuePair<global::TweenType,Tween>>();
                foreach(var p in d.eventTweens)if(NativeRunning(p.Value))parts.Add(p);
                parts.Sort((a,b)=>((int)a.Key).CompareTo((int)b.Key));
                foreach(var p in parts)sb.Append(p.Key).Append(p.Value.id is DecoAnim.Rec?"(rec)":"(nat)").Append('+');
            }
            catch {sb.Append("unreadable");} // Preserve the original rejection even if census fails.
            sb.Append(" code=").Append((int)nativeLastReject).Append('/').Append(nativeLastReject)
                .Append(" channel=").Append(nativeRejectChannel?.ToString()??"bundle");
            string key=sb.ToString();nativeCombos.TryGetValue(key,out int count);nativeCombos[key]=count+1;
        }
        internal static string NativeRejectSummary()
        {
            var sb=new System.Text.StringBuilder();
            for(int i=1;i<nativeRejects.Length;i++)if(nativeRejects[i]>0)sb.Append(i).Append('=').Append(nativeRejects[i]).Append('(').Append((NativeReject)i).Append(") ");
            foreach(var kv in nativeCombos)sb.Append(" [").Append(kv.Key).Append("]=").Append(kv.Value);
            return sb+(nativeLastEx!=null?" ex="+nativeLastEx:"");
        }
        // All running entries must be verified native channels. Never adopt a
        // subset, including when a running Rec shares this decoration.
        static bool TryReadNativeChannels(scrVisualDecoration d,int master,out NativeAdoptSpec spec)
        {
            spec=null;
            nativeLastReject=NativeReject.None;nativeRejectChannel=null;
            long started=Stopwatch.GetTimestamp();
            try
            {
                bool any=false;
                foreach(var p in d.eventTweens)if(NativeRunning(p.Value)){any=true;break;}
                if(!any)return false;
                if(!TryReadNativeBundle(d,master,out spec)){RecordNativeCombo(d);return false;}
                return true;
            }
            catch(Exception ex)
            {
                spec=null;nativeLastEx=ex.GetType().Name+":"+ex.Message;
                NR(NativeReject.ReadException);RecordNativeCombo(d);return false; // Unknown schema/state stays on CPU.
            }
            finally {nativeCheckTicks+=Stopwatch.GetTimestamp()-started;}
        }
        static bool TryReadNativeBundle(scrVisualDecoration d,int master,out NativeAdoptSpec spec)
        {
            spec=null;
            if(nativeUpdateRunning)return NR(NativeReject.UpdateRunning);
            if(!NativePaused() && nativeHookFrame==Time.frameCount)return NR(NativeReject.HookFrame);
            var bundle=new NativeAdoptSpec{D=d,Dictionary=d.eventTweens,Master=master};
            foreach(var p in d.eventTweens)
                if(NativeRunning(p.Value))
                {
                    nativeRejectChannel=p.Key;
                    if(p.Value.id is DecoAnim.Rec)return NR(NativeReject.RunningRec);
                    if(!TryReadNativeChannel(d,p.Key,p.Value,out var channel))return false;
                    bundle.Channels.Add(channel);
                }
            if(bundle.Channels.Count==0)return false;
            bundle.Channels.Sort((a,b)=>((int)a.Key).CompareTo((int)b.Key));
            spec=bundle;return true;
        }
        static bool NativeTweenContract(Tween t,Type pluginType)
        {
            if(!t.active)return NR(NativeReject.Inactive);
            if(t.id!=null)return NR(NativeReject.Id);
            if(t.target!=null)return NR(NativeReject.Target);
            if(t.stringId!=null)return NR(NativeReject.StringId);
            if(t.intId!=-999)return NR(NativeReject.IntId);
            if(!t.IsInitialized()){nativeUninitSeen++;return NR(NativeReject.Uninitialized);}
            if(!t.IsPlaying())return NR(NativeReject.NotPlaying);
            if(t.IsComplete())return NR(NativeReject.Complete);
            if(t.isRelative)return NR(NativeReject.Relative);
            if(t.isBackwards)return NR(NativeReject.Backwards);
            if(!NativeBits(t.timeScale,1f))return NR(NativeReject.TimeScale);
            float delay=t.Delay(),elapsed=t.ElapsedDelay();
            if(!DecoBakeCore.Finite(delay))return NR(NativeReject.DelayNonfinite);
            if(delay<0f)return NR(NativeReject.DelayNegative);
            if(!DecoBakeCore.Finite(elapsed))return NR(NativeReject.ElapsedDelayNonfinite);
            if(elapsed<0f)return NR(NativeReject.ElapsedDelayNegative);
            if(elapsed<delay)return NR(NativeReject.DelayRemaining);
            if(!NativeBits(elapsed,delay))return NR(NativeReject.DelayElapsedMismatch);
            if(t.Loops()!=1)return NR(NativeReject.Loops);
            if(t.CompletedLoops()!=0)return NR(NativeReject.CompletedLoops);
            if(nativeGlobalReject!=0)return NR(nativeGlobalReject);
            for(int i=0;i<NativeFlags.Length;i++)
                if(NativeRead<bool>(t,NativeFlags[i],typeof(Tween)))return NR((NativeReject)((int)NativeReject.Inverted+i));
            if(NativeRead<UpdateType>(t,"updateType",typeof(Tween))!=UpdateType.Normal)return NR(NativeReject.UpdateType);
            if(NativeRead<LoopType>(t,"loopType",typeof(Tween))!=LoopType.Restart)return NR(NativeReject.LoopType);
            // DOTween 1.2.765 skips UpdateDelay once delayComplete is true.
            // Only a fully consumed delay is safe: GPU/resume have no pending-delay state.
            if(!NativeRead<bool>(t,"delayComplete",typeof(Tween)))return NR(NativeReject.DelayIncomplete);
            if(!NativeRead<bool>(t,"autoKill",typeof(Tween)))return NR(NativeReject.AutoKill);
            if(NativeRead<object>(t,"sequenceParent",typeof(Tween))!=null)return NR(NativeReject.SequenceParent);
            if(Convert.ToInt32(NativeRead<object>(t,"specialStartupMode",typeof(Tween)))!=0)return NR(NativeReject.SpecialStartup);
            if(NativeRead<object>(t,"customEase",typeof(Tween))!=null)return NR(NativeReject.CustomEase);
            if(NativeRead<object>(t,"onStart",typeof(ABSSequentiable))!=null)return NR(NativeReject.OnStart);
            if(t.onKill!=null)return NR(NativeReject.OnKill);
            if(t.onPlay!=null)return NR(NativeReject.OnPlay);
            if(t.onPause!=null)return NR(NativeReject.OnPause);
            if(t.onRewind!=null)return NR(NativeReject.OnRewind);
            if(t.onStepComplete!=null)return NR(NativeReject.OnStepComplete);
            if(t.onWaypointChange!=null)return NR(NativeReject.OnWaypointChange);
            if(nativeLinks==null)return NR(NativeReject.LinksUnavailable);
            if(nativeLinks.Contains(t))return NR(NativeReject.Linked);
            if(nativeManagerUpdating)return NR(NativeReject.ManagerUpdating);
            var plugin=NativeRead<object>(t,"tweenPlugin");
            if(plugin?.GetType()!=pluginType)return NR(NativeReject.PluginType);
            if(NativePatched(NativeMethod(pluginType,"EvaluateAndApply")))return NR(NativeReject.PluginPatched);
            if(NativePatched(NativeMethod(NativeManager,"Despawn")))return NR(NativeReject.DespawnPatched);
            if(NativePatched(nativeKillMethod,true))return NR(NativeReject.KillPatched);
            return true;
        }
        // Tokens and literal closure links below were checked against StartEffect
        // IL for NativeGameMvid. Do not infer schemas from delegate/field names.
        static bool TryReadNativeChannel(scrVisualDecoration d,global::TweenType key,Tween tween,out NativeAdoptChannel spec)
        {
            spec=null;
            int first,get,set,update,complete;
            string link;
            switch(key)
            {
                case global::TweenType.PositionX: first=0;get=0x06002EEA;set=0x06002EEB;update=0x06002EEC;complete=0x06002EE1;link="CS$<>8__locals2";break;
                case global::TweenType.PositionY: first=1;get=0x06002EEE;set=0x06002EEF;update=0x06002EF0;complete=0x06002EE2;link="CS$<>8__locals3";break;
                case global::TweenType.Rotation: first=4;get=0x06002F02;set=0x06002F03;update=0x06002F04;complete=0x06002ED8;link="CS$<>8__locals8";break;
                case global::TweenType.ScaleX: first=5;get=0x06002ED9;set=0x06002EDA;update=complete=0;link=null;break;
                case global::TweenType.ScaleY: first=6;get=0x06002EDB;set=0x06002EDC;update=complete=0;link=null;break;
                case global::TweenType.Color: first=7;get=0x06002F06;set=0x06002F07;update=0x06002F08;complete=0x06002EDD;link="CS$<>8__locals9";break;
                default:return NR(NativeReject.UnsupportedKey);
            }
            var s=new NativeAdoptChannel{Key=key,First=first,Tween=tween,Link=link};
            Delegate getter,setter;
            Type plugin;
            if(s.IsScale)
            {
                var t=tween as TweenerCore<Vector2,Vector2,VectorOptions>;
                if(t==null || t.GetType()!=typeof(TweenerCore<Vector2,Vector2,VectorOptions>))return NR(NativeReject.ScaleType);
                if(t.plugOptions.snapping)return NR(NativeReject.ScaleSnapping);
                if(t.plugOptions.axisConstraint!=(first==5?AxisConstraint.X:AxisConstraint.Y))return NR(NativeReject.ScaleAxis);
                int axis=first-5;
                // Vector2Plugin zeros the unconstrained change; the unused end
                // may legitimately be NaN when only one scale axis was supplied.
                if(!NativeBits(t.changeValue[1-axis],0f))return NR(NativeReject.ScaleUnusedChange);
                s.Start=new[]{t.startValue[axis]};s.End=new[]{t.endValue[axis]};s.Change=new[]{t.changeValue[axis]};
                s.Current=new[]{d.scaleVec[axis]};getter=t.getter;setter=t.setter;plugin=typeof(Vector2Plugin);
            }
            else if(key==global::TweenType.Color)
            {
                var t=tween as TweenerCore<Color,Color,ColorOptions>;
                if(t==null || t.GetType()!=typeof(TweenerCore<Color,Color,ColorOptions>))return NR(NativeReject.ColorType);
                if(t.plugOptions.alphaOnly)return NR(NativeReject.ColorAlphaOnly);
                s.Start=new[]{t.startValue.r,t.startValue.g,t.startValue.b,t.startValue.a};
                s.End=new[]{t.endValue.r,t.endValue.g,t.endValue.b,t.endValue.a};
                s.Change=new[]{t.changeValue.r,t.changeValue.g,t.changeValue.b,t.changeValue.a};
                getter=t.getter;setter=t.setter;plugin=typeof(ColorPlugin);
            }
            else
            {
                var t=tween as TweenerCore<float,float,FloatOptions>;
                if(t==null || t.GetType()!=typeof(TweenerCore<float,float,FloatOptions>))return NR(NativeReject.FloatType);
                if(t.plugOptions.snapping)return NR(NativeReject.FloatSnapping);
                s.Start=new[]{t.startValue};s.End=new[]{t.endValue};s.Change=new[]{t.changeValue};
                getter=t.getter;setter=t.setter;plugin=typeof(FloatPlugin);
            }
            if(!NativeTweenContract(tween,plugin))return false;
            s.Delay=tween.Delay();s.ElapsedDelay=tween.ElapsedDelay();
            if(!NativeDelegate(getter,get))return NR(NativeReject.Getter);
            if(!NativeDelegate(setter,set,getter.Target))return NR(NativeReject.Setter);
            s.Getter=getter;s.Setter=setter;s.OnUpdate=tween.onUpdate;s.OnComplete=tween.onComplete;
            s.Closure=getter.Target;
            if(s.IsScale)
            {
                if(tween.onUpdate!=null)return NR(NativeReject.ScaleOnUpdate);
                if(tween.onComplete!=null)return NR(NativeReject.ScaleOnComplete);
                s.Root=s.Closure;
            }
            else
            {
                if(!NativeDelegate(tween.onUpdate,update,s.Closure))return NR(NativeReject.OnUpdate);
                if(!NativeDelegate(tween.onComplete,complete))return NR(NativeReject.OnComplete);
                s.CompleteClosure=NativeRead<object>(s.Closure,link);
                if(!ReferenceEquals(s.CompleteClosure,tween.onComplete.Target))return NR(NativeReject.CompleteClosure);
                s.Root=first<=1?NativeRead<object>(s.CompleteClosure,"CS$<>8__locals1"):s.CompleteClosure;
            }
            if(!ReferenceEquals(NativeRead<object>(s.Root,"dec"),d))return NR(NativeReject.Decoration);
            s.Fx=NativeRead<ffxMoveDecorationsPlus>(s.Root,"<>4__this");
            if(!s.Fx)return NR(NativeReject.Fx);
            float[] expected,logical;
            if(first<=1)
            {
                var start=NativeRead<Vector2>(s.CompleteClosure,"startPos");
                expected=new[]{start[first]+s.Fx.targetPos[first]};
                s.Current=new[]{NativeRead<Vector2>(s.Closure,"newPos")[first]};logical=new[]{d.pivotPosVec[first]};
            }
            else if(first==4)
            {
                expected=new[]{s.Fx.targetRot};s.Current=new[]{NativeRead<float>(s.Closure,"newRot")};logical=new[]{d.rotAngle};
            }
            else if(s.IsScale)
            {
                expected=new[]{s.Fx.targetScaleV2[first-5]};logical=s.Current;
            }
            else
            {
                var color=NativeRead<Color>(s.Closure,"newColor");var end=s.Fx.targetColor;var current=d.color;
                s.Current=new[]{color.r,color.g,color.b,color.a};expected=new[]{end.r,end.g,end.b,end.a};
                logical=new[]{current.r,current.g,current.b,current.a};
            }
            s.Ease=NativeRead<Ease>(tween,"easeType",typeof(Tween));
            s.Pos=tween.Elapsed(false);s.Dur=tween.Duration(false);s.Over=tween.easeOvershootOrAmplitude;s.Period=tween.easePeriod;
            if(!DecoBakeCore.Finite(s.Pos))return NR(NativeReject.PositionNonfinite);
            if(!DecoBakeCore.Finite(s.Dur))return NR(NativeReject.DurationNonfinite);
            if(!DecoBakeCore.Finite(s.Over))return NR(NativeReject.OvershootNonfinite);
            if(!DecoBakeCore.Finite(s.Period))return NR(NativeReject.PeriodNonfinite);
            if(s.Dur<=0f)return NR(NativeReject.Duration);
            if(s.Pos<0f)return NR(NativeReject.PositionNegative);
            if(s.Pos>=s.Dur)return NR(NativeReject.PositionAtEnd);
            if(s.Ease==Ease.Unset)return NR(NativeReject.EaseUnset);
            if(!GpuDecoGpu.Supported((int)s.Ease))return NR(NativeReject.EaseUnsupported);
            float e=s.Pos>0f?ZeroTween.Eval(s.Ease,s.Pos,s.Dur,s.Over,s.Period):0f;
            for(int c=0;c<s.Start.Length;c++)
            {
                if(!DecoBakeCore.Finite(s.Start[c]))return NR(NativeReject.StartNonfinite);
                if(!DecoBakeCore.Finite(s.End[c]))return NR(NativeReject.EndNonfinite);
                if(!DecoBakeCore.Finite(s.Change[c]))return NR(NativeReject.ChangeNonfinite);
                if(!DecoBakeCore.Finite(s.Current[c]))return NR(NativeReject.CurrentNonfinite);
                if(!DecoBakeCore.Finite(expected[c]))return NR(NativeReject.ExpectedNonfinite);
                if(!DecoBakeCore.Finite(logical[c]))return NR(NativeReject.LogicalNonfinite);
                if(!NativeBits(expected[c],s.End[c]))return NR(NativeReject.EndMismatch);
                if(!NativeBits(s.End[c]-s.Start[c],s.Change[c]))return NR(NativeReject.ChangeMismatch);
                float value=s.Pos>0f?(float)((double)s.Start[c]+(double)s.Change[c]*(double)e):s.Start[c];
                if(!NativeBits(s.Current[c],value))return NR(NativeReject.ClosureValueMismatch);
                if(!NativeBits(logical[c],value))return NR(NativeReject.LogicalValueMismatch);
            }
            spec=s;return true;
        }
        static void PrepareNativeAdoptionSources()
        {
            long started=Stopwatch.GetTimestamp();
            try
            {
                gpuNativeSourceIds.Clear();
                foreach(var d in baselineObjects)
                    if(d && d.eventTweens!=null)foreach(var t in d.eventTweens.Values)
                        if(t?.id is DecoAnim.Rec r && r.Running)gpuNativeSourceIds.Add(r.GpuSource);
            }
            finally {nativeCaptureTicks+=Stopwatch.GetTimestamp()-started;}
        }
        static void CaptureNativeAdoption(NativeAdoptSpec s)
        {
            long started=Stopwatch.GetTimestamp();
            try
            {
                RequireLive(NativeAdoptionUnchanged(s),"native adoption changed before capture");
                foreach(var channel in s.Channels)
                {
                    channel.Replacement=FloorAnim.NewProxy(); // Allocate before the irreversible commit.
                    CaptureNativeChannel(s.Master,channel);
                }
            }
            finally {nativeCaptureTicks+=Stopwatch.GetTimestamp()-started;}
        }
        static void CaptureNativeChannel(int master,NativeAdoptChannel s)
        {
            // Reserve one independent clock per native tween. Color components
            // share this clock, but distinct native channels must never be merged.
            int sourceId;
            bool used;
            do
            {
                sourceId=nativeSourceId;nativeSourceId=checked(nativeSourceId+1);used=false;
                foreach(int slot in gpuActiveSourceSlots)if(gpuSourcePool[slot].SourceId==sourceId){used=true;break;}
                used|=gpuNativeSourceIds.Contains(sourceId);
            }while(used);
            gpuNativeSourceIds.Add(sourceId);
            var source=AllocateGpuLiveSource(sourceId,Time.frameCount-1,s.Dur,s.Ease,s.Over,s.Period,s.Pos);
            float easeEnd=ZeroTween.Eval(s.Ease,s.Dur,s.Dur,s.Over,s.Period);
            for(int c=0;c<s.Start.Length;c++)
            {
                float end=s.IsScale?s.Start[c]+s.Change[c]*easeEnd:s.End[c];
                RequireLive(DecoBakeCore.Finite(end),"native adoption nonfinite endpoint");
                SetMasterSlotAnimated(master,s.First+c,s.Start[c],s.End[c],end,source,s.IsScale);
                int slot=master*16+s.First+c;
                RequireLive(NativeBits(gpuMasterChannels[slot].change,s.Change[c])
                    && NativeBits(EvalMasterSlotCurrent(slot),s.Current[c]),"native adoption channel mismatch");
            }
            // No Rec existed: BuildGpuResumePlan supplies an explicitly unobserved order.
        }
        // Reuse the verified bundle. This path allocates no second spec, channel,
        // value array or invocation list and performs no metadata/patch lookup.
        // Mutable tween/options/delegate/closure/value state is still checked.
        static bool NativeAdoptionUnchanged(NativeAdoptSpec s)
        {
            if(!s.D || !ReferenceEquals(s.Dictionary,s.D.eventTweens) || nativeUpdateRunning
                || !NativePaused() && nativeHookFrame==Time.frameCount)return false;
            int count=0;
            foreach(var p in s.Dictionary)if(NativeRunning(p.Value))
            {
                if(p.Value.id is DecoAnim.Rec || !NativeBundleContains(s,p.Key,p.Value))return false;
                count++;
            }
            if(count!=s.Channels.Count)return false;
            foreach(var channel in s.Channels)
            {
                if(!s.Dictionary.TryGetValue(channel.Key,out var tween) || !ReferenceEquals(tween,channel.Tween)
                    || !NativeChannelUnchanged(s.D,channel))return false;
            }
            return true;
        }
        static bool NativeValueUnchanged(NativeAdoptChannel s,int c,float start,float end,float change,float current,float logical,float expected)
        {
            return NativeBits(start,s.Start[c]) && NativeBits(end,s.End[c]) && NativeBits(change,s.Change[c])
                && NativeBits(current,s.Current[c]) && NativeBits(logical,s.Current[c]) && NativeBits(expected,s.End[c]);
        }
        static bool NativeChannelUnchanged(scrVisualDecoration d,NativeAdoptChannel s)
        {
            var t=s.Tween;
            if(!NativeBits(t.Elapsed(false),s.Pos) || !NativeBits(t.Duration(false),s.Dur)
                || !NativeBits(t.Delay(),s.Delay) || !NativeBits(t.ElapsedDelay(),s.ElapsedDelay)
                || NativeRead<Ease>(t,"easeType",typeof(Tween))!=s.Ease
                || !NativeBits(t.easeOvershootOrAmplitude,s.Over) || !NativeBits(t.easePeriod,s.Period)
                || !Equals(t.onUpdate,s.OnUpdate) || !Equals(t.onComplete,s.OnComplete))return false;
            if(!ReferenceEquals(NativeRead<object>(s.Root,"dec"),d)
                || !ReferenceEquals(NativeRead<ffxMoveDecorationsPlus>(s.Root,"<>4__this"),s.Fx) || !s.Fx)return false;
            if(s.Link!=null && (!ReferenceEquals(NativeRead<object>(s.Closure,s.Link),s.CompleteClosure)
                || s.First<=1 && !ReferenceEquals(NativeRead<object>(s.CompleteClosure,"CS$<>8__locals1"),s.Root)))return false;
            if(s.IsScale)
            {
                var v=(TweenerCore<Vector2,Vector2,VectorOptions>)t;int axis=s.First-5;
                return NativeTweenContract(t,typeof(Vector2Plugin)) && Equals(v.getter,s.Getter) && Equals(v.setter,s.Setter)
                    && !v.plugOptions.snapping && v.plugOptions.axisConstraint==(axis==0?AxisConstraint.X:AxisConstraint.Y)
                    && NativeBits(v.changeValue[1-axis],0f)
                    && NativeValueUnchanged(s,0,v.startValue[axis],v.endValue[axis],v.changeValue[axis],d.scaleVec[axis],d.scaleVec[axis],s.Fx.targetScaleV2[axis]);
            }
            if(s.Key==global::TweenType.Color)
            {
                var v=(TweenerCore<Color,Color,ColorOptions>)t;
                if(!NativeTweenContract(t,typeof(ColorPlugin)) || !Equals(v.getter,s.Getter) || !Equals(v.setter,s.Setter) || v.plugOptions.alphaOnly)return false;
                var current=NativeRead<Color>(s.Closure,"newColor");var logical=d.color;var expected=s.Fx.targetColor;
                for(int c=0;c<4;c++)
                    if(!NativeValueUnchanged(s,c,v.startValue[c],v.endValue[c],v.changeValue[c],current[c],logical[c],expected[c]))return false;
                return true;
            }
            var f=(TweenerCore<float,float,FloatOptions>)t;
            if(!NativeTweenContract(t,typeof(FloatPlugin)) || !Equals(f.getter,s.Getter) || !Equals(f.setter,s.Setter) || f.plugOptions.snapping)return false;
            float value,decValue,endValue;
            if(s.First<=1)
            {
                value=NativeRead<Vector2>(s.Closure,"newPos")[s.First];decValue=d.pivotPosVec[s.First];
                endValue=NativeRead<Vector2>(s.CompleteClosure,"startPos")[s.First]+s.Fx.targetPos[s.First];
            }
            else {value=NativeRead<float>(s.Closure,"newRot");decValue=d.rotAngle;endValue=s.Fx.targetRot;}
            return NativeValueUnchanged(s,0,f.startValue,f.endValue,f.changeValue,value,decValue,endValue);
        }
        static bool NativeBundleContains(NativeAdoptSpec bundle,global::TweenType key,Tween tween)
        {
            foreach(var channel in bundle.Channels)
                if(channel.Key==key && ReferenceEquals(channel.Tween,tween))return true;
            return false;
        }
        static int NativeAdoptionChannelCount()
        {
            int count=0;
            foreach(var bundle in gpuNativeAdoptions.Values)count+=bundle.Channels.Count;
            return count; // Native channel count: a Color tween counts once (four GPU slots).
        }
        static void ValidateNativeAdoptions()
        {
            int mismatch=0;
            foreach(var s in gpuNativeAdoptions.Values)
            {
                RequireLive(NativeAdoptionUnchanged(s),
                    "native adoption changed before commit");
                foreach(var channel in s.Channels)
                {
                    RequireLive(channel.Replacement!=null,"native adoption proxy missing before commit");
                    for(int c=0;c<channel.Current.Length;c++)
                        if(!NativeBits(EvalMasterSlotCurrent(s.Master*16+channel.First+c),channel.Current[c]))mismatch++;
                }
            }
#if AUTOTEST
            if(DrawCollapsePolicy.AdoptNativeTweens)Main.Entry.Logger.Log("[GPU 장식] 이어받기: "+gpuNativeAdoptions.Count+"개(장식), 채널 "+NativeAdoptionChannelCount()+"개, 값 불일치 "+mismatch+"개, 거절 "+NativeRejectSummary());
            ResetNativeRejects();
#endif
            RequireLive(mismatch==0,"native adoption current value mismatch");
            nativeActivationNeedsStep=gpuNativeAdoptions.Count>0 && !NativePaused() && nativeUpdateCompleted!=Time.frameCount;
        }
        static void CommitNativeAdoptions()
        {
            foreach(var s in gpuNativeAdoptions.Values)
            {
                foreach(var channel in s.Channels)
                {
                    channel.Tween.Kill(false); // onKill is null; no completion/update invocation.
                    RequireLive(!channel.Tween.active,"native tween kill refused");
                    s.Dictionary[channel.Key]=channel.Replacement;
                }
            }
            // A native Update still due this frame must be represented exactly once.
            if(nativeActivationNeedsStep)
            {
                gpuLiveLastSourceStepFrame=Time.frameCount-1;
                gpuLiveLastDispatchFrame=-1; // Let LateUpdate/pre-cull publish that step this frame.
            }
        }
        static bool NativeStillOwns(scrDecoration d,int key)
        {
            foreach(var s in gpuNativeAdoptions.Values)
                if(ReferenceEquals(s.D,d))foreach(var channel in s.Channels)
                    if((int)channel.Key==key && channel.Tween.active && s.Dictionary.TryGetValue(channel.Key,out var t)
                        && ReferenceEquals(t,channel.Tween))return true;
            return false;
        }
    }
}
#endif
