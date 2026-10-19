using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    internal sealed class FrameQueueDelay:IOptimizationModule
    {
        internal static readonly FrameQueueDelay Instance=new FrameQueueDelay();
        public string Key=>"input-frame-delay";
        internal static bool Enabled;
        private static RawKeyboardClock clock;
        private static FieldInfo keysField;
        private static FieldInfo oldClock,webClock;
        private static long received,pollQpc,pollUtc;private static double pollGameDsp;private static int sampleFrame=-1,usedFrame=-1;
        [ThreadStatic] private static bool correcting;
        [ThreadStatic] private static long dspTick;
        [ThreadStatic] private static long baseDspTick;
        internal static long Corrected,Skipped,AlreadyAsync;internal static double LastDelayMs;
        internal static string Status=>!Enabled?"off":AsyncInputManager.isActive?"game-already-timestamped; correction=0":clock==null?"waiting":clock.Status;
        public void Install(Harmony h)
        {
            keysField=typeof(RDInputType.MainStateCount).GetField("keys",AccessTools.all);
            oldClock=typeof(GCS).GetField("d_oldConductor",AccessTools.all);webClock=typeof(GCS).GetField("d_webglConductor",AccessTools.all);
            var poll=AccessTools.DeclaredMethod(typeof(RDInputType_Keyboard),"GetStateCountIgnoreActive",new[]{typeof(ButtonState)});
            var player=AccessTools.DeclaredMethod(typeof(scrPlayer),"Simulated_PlayerControl_Update",new[]{typeof(long?)});
            var angle=AccessTools.DeclaredMethod(typeof(AsyncInputUtils),"AdjustAngle",new[]{typeof(scrPlayer),typeof(long)});
            if(keysField==null||keysField.FieldType!=typeof(List<AnyKeyCode>)||oldClock==null||webClock==null||poll==null||player==null||angle==null)throw new MissingMethodException("Legacy timestamp adapter contract changed");
            foreach(var method in new MethodBase[]{angle,AccessTools.DeclaredMethod(typeof(scrPlanet),"AsyncRefreshAngles",Type.EmptyTypes)})
            {
                if(method==null)throw new MissingMethodException("Game angle helper unavailable");
                var patches=Harmony.GetPatchInfo(method);
                if(patches!=null)foreach(string owner in patches.Owners)if(owner!=h.Id)throw new NotSupportedException("Input angle helper already modified by "+owner);
            }
            h.Patch(poll,postfix:new HarmonyMethod(typeof(FrameQueueDelay),nameof(Polled)));
            h.Patch(player,prefix:new HarmonyMethod(typeof(FrameQueueDelay),nameof(Begin)),finalizer:new HarmonyMethod(typeof(FrameQueueDelay),nameof(End)));
            h.Patch(angle,prefix:new HarmonyMethod(typeof(FrameQueueDelay),nameof(Adjust)));
            Application.focusChanged+=FocusChanged;
        }
        internal static void Tick()
        {
            if(!Enabled)return;
            try
            {
                if(AsyncInputManager.isActive||!Hitch.Playing||!Application.isFocused||SettingsWindow.Open||Hotkey.Capturing)
                {if(clock!=null){clock.Dispose();clock=null;}sampleFrame=-1;return;}
                if(clock==null){clock=new RawKeyboardClock();clock.Start();}
            }
            catch{if(clock!=null){clock.Dispose();clock=null;}sampleFrame=-1;}
        }
        // Only consume a single unambiguous legacy main-key down. No controller/mouse/remap guessing.
        private static void Polled(RDInputType_Keyboard __instance,ButtonState __0,RDInputType.MainStateCount __result)
        {
            if(!Enabled||__0!=ButtonState.WentDown||__instance!=RDInput.keyboardInput||__result==null||clock==null||!clock.Ready||sampleFrame==Time.frameCount||usedFrame==Time.frameCount)return;
            try
            {
                if(AsyncInputManager.isActive)return;
                var keys=keysField.GetValue(__result) as List<AnyKeyCode>;if(keys==null||keys.Count!=1){sampleFrame=-1;return;}
                int count=keys.Count,vk=VirtualKey(keys[0].Cast<KeyCode>());
                sampleFrame=-1;
                if(count!=1||vk==0||!clock.Take(vk,out long qpc)){Skipped++;return;}
                if(scrConductor.instance==null)return;
                received=qpc;pollQpc=Stopwatch.GetTimestamp();pollUtc=DateTime.UtcNow.Ticks;pollGameDsp=scrConductor.instance.dspTime;sampleFrame=Time.frameCount;
            }
            catch{sampleFrame=-1;Skipped++;}
        }
        internal static int VirtualKey(KeyCode key)
        {
            if(key>=KeyCode.A&&key<=KeyCode.Z)return 0x41+(int)key-(int)KeyCode.A;
            if(key>=KeyCode.Alpha0&&key<=KeyCode.Alpha9)return 0x30+(int)key-(int)KeyCode.Alpha0;
            return key==KeyCode.Space?0x20:0;
        }
        private struct Scope{internal bool Active,Prior;internal long PriorTarget,PriorDsp,PriorBase;}
        private static void Begin(scrPlayer __instance,ref long? __0,out Scope __state)
        {
            __state=default;
            if(!Enabled)return;
            try
            {
                if(__0.HasValue||AsyncInputManager.isActive){AlreadyAsync++;return;}
                if(sampleFrame!=Time.frameCount||correcting||RDC.auto||SettingsWindow.Open||Hotkey.Capturing||ADOBase.controller==null||ADOBase.controller.paused||__instance!=ADOBase.controller.playerOne||__instance.holdKeys.Count!=0)return;
                if(__instance.planetarySystem==null||__instance.planetarySystem.chosenPlanet==null)return;
                if((bool)oldClock.GetValue(null)||(bool)webClock.GetValue(null))return;
                sampleFrame=-1;
                if(!FrameQueueDelayPolicy.TryCorrect(received,pollQpc,Stopwatch.Frequency,pollUtc,false,Application.isFocused,Hitch.Playing,out long timestamp,out double delay))return;
                if(timestamp==pollUtc)return;
                // Keep the game's polled audio time base; subtract ONLY reception -> polling.
                // Reading fresh AudioSettings.dspTime here could add an unintended phase change.
                double delivered=pollGameDsp-(pollQpc-received)/(double)Stopwatch.Frequency;
                if(!double.IsFinite(delivered)||delivered<0||delivered>long.MaxValue/10000000.0)return;
                __state=new Scope{Active=true,Prior=correcting,PriorDsp=dspTick,PriorBase=baseDspTick,PriorTarget=Convert.ToInt64(AsyncInputManager.targetSongTick)};
                dspTick=(long)(delivered*10000000.0);baseDspTick=(long)(pollGameDsp*10000000.0);correcting=true;__0=timestamp;LastDelayMs=delay;usedFrame=Time.frameCount;
            }
            catch{if(__state.Active)Restore(__state);__state=default;}
        }
        private static bool Adjust(scrPlayer __0,long __1)
        {
            if(!correcting)return true;
            long old=Convert.ToInt64(AsyncInputManager.targetSongTick);
            scrPlanet planet=null;double angle=0;
            try
            {
                if(__0==null||__0.planetarySystem==null||__0.planetarySystem.chosenPlanet==null)return true;
                planet=__0.planetarySystem.chosenPlanet;angle=planet.angle;
                if(dspTick==baseDspTick)return false;
                TickCompat.Set(ref AsyncInputManager.targetSongTick,baseDspTick);planet.AsyncRefreshAngles();double atPoll=planet.angle;
                TickCompat.Set(ref AsyncInputManager.targetSongTick,dspTick);planet.AsyncRefreshAngles();double atReceive=planet.angle;
                double difference=atReceive-atPoll;
                if(!double.IsFinite(difference)||!double.IsFinite(angle+difference))throw new InvalidOperationException("Invalid game angle delta");
                // Preserve the legacy path's existing phase, calibration and accumulated turns.
                // Only the angle traversed between reception and polling is removed.
                planet.angle=difference==0?angle:angle+difference;Corrected++;return false;
            }
            catch{if(planet!=null)planet.angle=angle;TickCompat.Set(ref AsyncInputManager.targetSongTick,old);return true;}
        }
        private static Exception End(Exception __exception,Scope __state){if(__state.Active)Restore(__state);return __exception;}
        private static void Restore(Scope state){correcting=state.Prior;dspTick=state.PriorDsp;baseDspTick=state.PriorBase;TickCompat.Set(ref AsyncInputManager.targetSongTick,state.PriorTarget);}
#if DEV || AUTOTEST
        internal static string AngleFixture()
        {
            if(!Enabled||!AsyncInputManager.isActive||!Hitch.Playing)return "applied=0 requires-existing-async-play";
            var player=ADOBase.controller.playerOne;var planet=player.planetarySystem.chosenPlanet;
            var f=typeof(AsyncInputManager).GetField("offsetTick",AccessTools.all);var ready=typeof(AsyncInputManager).GetField("offsetTickUpdated",AccessTools.all);
            if(f==null||ready==null||!(bool)ready.GetValue(null))return "applied=0 game-clock-not-calibrated";
            long offset=(long)f.GetValue(null),now=DateTime.UtcNow.Ticks,oldTarget=Convert.ToInt64(AsyncInputManager.targetSongTick),oldDsp=dspTick,oldBase=baseDspTick,oldCount=Corrected;
            double oldAngle=planet.angle;bool oldScope=correcting;int mismatch=0,checks=0;
            try
            {
                foreach(double phase in new[]{0.0,Math.PI*4+.1257})for(int i=0;i<=20;i++)
                {
                    long timestamp=now-i*25000;correcting=false;AsyncInputUtils.AdjustAngle(player,now);double atPoll=planet.angle;
                    AsyncInputUtils.AdjustAngle(player,timestamp);double atReceive=planet.angle;
                    double initial=atPoll+phase,difference=atReceive-atPoll;planet.angle=initial;
                    double expected=difference==0?initial:initial+difference;
                    correcting=true;baseDspTick=now-offset;dspTick=timestamp-offset;Adjust(player,timestamp);
                    checks++;if(BitConverter.DoubleToInt64Bits(expected)!=BitConverter.DoubleToInt64Bits(planet.angle))mismatch++;
                }
                if(mismatch!=0)throw new InvalidOperationException("Game relative input angle mismatch "+mismatch);
                return "game_delta_conditions="+checks+" mismatch="+mismatch+" hit_calls=0 input_injected=0 preserves_existing_phase=1 zero_delay_unchanged=1 physical_keys_not_tested=1";
            }
            finally{planet.angle=oldAngle;TickCompat.Set(ref AsyncInputManager.targetSongTick,oldTarget);dspTick=oldDsp;baseDspTick=oldBase;correcting=oldScope;Corrected=oldCount;}
        }
#endif
        private static void FocusChanged(bool focused){if(clock!=null)clock.Clear();sampleFrame=-1;usedFrame=-1;}
        public void Uninstall(){Enabled=false;Application.focusChanged-=FocusChanged;if(clock!=null){clock.Dispose();clock=null;}sampleFrame=-1;correcting=false;}
    }

    // 게임 버전에 따라 AsyncInputManager.targetSongTick 이 long(3.4 알파)이거나 ulong(일반 버전)이다. 둘 다 컴파일·동작하게 한다.
    internal static class TickCompat
    {
        internal static void Set<T>(ref T field, long value) where T : struct { field = (T)System.Convert.ChangeType(value, typeof(T)); }
    }
}
