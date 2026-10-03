using System;
using System.Collections.Generic;
using System.Diagnostics;
using ADOFAI;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // 곡 중에 처음 쓰는 효과음(박자 소리 등)을 재생 준비 때 미리 불러 두기.
    //
    // 2026-10-04 Windflower 곡 17초 무렵 30ms(엔진 단계 바깥으로 보였던 끊김): PerfView 로 보니 메인 스레드가 38ms 동안 쉬지 않고 돌았고,
    // 샘플 대부분이 Resources.Load -> AudioClip 불러오기 -> FMOD Vorbis 압축 풀기였다. 게임은 소리를 처음 쓸 때
    // AudioManager.FindOrLoadAudioClip("snd" + 종류) 로 불러와 audioLib 에 담는다(Resources.Load, 압축을 통째로 풂).
    // 박자 소리 종류가 곡 중에 바뀌면(ffxSetHitsound) 그 소리를 처음 예약하는 순간 메인 스레드에서 수십 ms 멈춘다.
    //
    // 재생 준비(ApplyEventsToFloors) 끝에 이 맵이 쓸 수 있는 박자 소리를 넉넉하게 모아 같은 함수로 미리 불러 둔다:
    // 맵 기본 소리, 타일의 박자 소리 변경(ffxSetHitsound), 자유 이동 타일의 박자 소리, 행성 수가 바뀌는 타일의 소리,
    // 박자 소리 종류를 쓰는 소리 재생 효과(ffxPlaySound), 카운트다운·곡 시작 소리, 누르는 박자 소리(기본 + ffxSetHoldsound). 같은 이름이면 같은 클립이 같은 사전에 들어가므로 결과는 같다.
    // 쓰이지 않는 소리를 더 불러도 메모리만 조금 더 쓴다. 빠뜨린 소리는 원래처럼 처음 쓸 때 불린다.
    internal static class SoundWarm
    {
        internal static bool Enabled = true;
        private static readonly HashSet<HitSound> want = new HashSet<HitSound>();
        private static Func<ffxPlaySound, bool> useHit;

        private static void AddHold(HashSet<string> set, object start, object loop, object mid, object end)
        {
            string a = start == null ? "None" : start.ToString(), b = loop == null ? "None" : loop.ToString(), c = mid == null ? "None" : mid.ToString(), d = end == null ? "None" : end.ToString();
            if (a != "None") set.Add("sndHeldbeatStart" + a);
            if (b != "None") set.Add("sndHeldbeatLoop" + b);
            if (c != "None") set.Add("sndHeldbeatMid" + c);
            if (d != "None") set.Add("sndHeldbeatEnd" + d);
        }

        internal static void Install(Harmony h)
        {
            var aetf = AccessTools.Method(typeof(scnGame), "ApplyEventsToFloors", new[] { typeof(List<scrFloor>), typeof(LevelData), typeof(scrLevelMaker), typeof(List<LevelEvent>) });
            var fol = AccessTools.Method(typeof(AudioManager), "FindOrLoadAudioClip", new[] { typeof(string), typeof(string), typeof(bool) });
            if (aetf == null || fol == null) { Main.Entry.Logger.Log("[소리 미리 불러오기] 게임 코드 모양이 달라 안 함"); return; }
            var f = AccessTools.Field(typeof(ffxPlaySound), "useHitSound");
            var p = f == null ? AccessTools.Property(typeof(ffxPlaySound), "useHitSound") : null;
            if (f != null) useHit = x => (bool)f.GetValue(x);
            else if (p != null) useHit = x => (bool)p.GetValue(x, null);
            h.Patch(aetf, postfix: new HarmonyMethod(typeof(SoundWarm), nameof(Postfix)) { priority = Priority.Last });
            // (개발자용) 곡 중에 새로 불러오는 소리: 이름과 시간 (빠뜨린 것 찾기)
            if (Edition.Dev) h.Patch(fol, prefix: new HarmonyMethod(typeof(SoundWarm), nameof(FolPre)), postfix: new HarmonyMethod(typeof(SoundWarm), nameof(FolPost)));
        }

        private static bool folNew; private static int folLogged;
        public static void FolPre(AudioManager __instance, string clipName, out long __state) { __state = Stopwatch.GetTimestamp(); folNew = clipName != null && !__instance.audioLib.ContainsKey(System.IO.Path.GetFileName(clipName)); }
        public static void FolPost(string clipName, long __state)
        {
            if (!folNew || folLogged >= 50) return;
            folLogged++;
            Main.Entry.Logger.Log(string.Format("[소리 미리 불러오기] (개발자용) 새로 불러옴 '{0}' {1:F1}ms, 곡 중 {2}, 실시간 {3:F1}초", clipName, (Stopwatch.GetTimestamp() - __state) * 1000.0 / Stopwatch.Frequency, Hitch.Playing, Time.realtimeSinceStartup));
        }

        public static void Postfix(LevelData levelData)
        {
            if (!Enabled) return;
            try { Run(levelData); } catch (Exception ex) { Main.Entry.Logger.Log("[소리 미리 불러오기] 실패: " + ex.Message); }
        }

        private static void Run(LevelData levelData)
        {
            var am = ADOBase.audioManager;
            var lm = ADOBase.lm;
            if (am == null || lm == null || lm.listFloors == null) return;
            want.Clear();
            try { if (levelData != null) want.Add(levelData.hitsound); } catch { }
            try { var c = ADOBase.conductor; if (c != null) want.Add(c.hitSound); } catch { }
            var floors = lm.listFloors;
            for (int i = 0; i < floors.Count; i++)
            {
                var fl = floors[i];
                if (fl == null) continue;
                if ((object)fl.setHitsound != null && fl.setHitsound != null) want.Add(fl.setHitsound.hitSound);
                want.Add(fl.freeroamSoundOnBeat);
                want.Add(fl.freeroamSoundOffBeat);
                if (fl.prevfloor != null && fl.numPlanets != fl.prevfloor.numPlanets) { want.Add(HitSound.VehiclePositive); want.Add(HitSound.VehicleNegative); }
                if (useHit != null && fl.plusEffects != null)
                    for (int k = 0; k < fl.plusEffects.Count; k++)
                    {
                        var ps = fl.plusEffects[k] as ffxPlaySound;
                        if ((object)ps != null && useHit(ps)) want.Add(ps.hitSound);
                    }
            }
            want.Remove(HitSound.None);
            long t0 = Stopwatch.GetTimestamp();
            int loaded = 0;
            var names = new List<string> { "sndHat", "sndCymbalCrash" };   // 카운트다운, 곡 시작
            foreach (var hs in want) names.Add("snd" + hs);
            // 누르는 박자 소리: "sndHeldbeat" + Start/Loop/Mid/End + 종류 (scrConductor.PlayHitTimes). 기본은 지휘자 필드, 타일의 ffxSetHoldsound 가 바꾼다.
            // Windflower 곡 20초 무렵: sndHeldbeatStartFuse 7ms + sndHeldbeatLoopFuse 24ms 를 한 프레임에 처음 불러 31ms 멈췄다.
            bool anyHold = false;
            for (int i = 0; i < floors.Count && !anyHold; i++) if (floors[i] != null && floors[i].holdLength > -1) anyHold = true;
            if (anyHold)
            {
                var hold = new HashSet<string>();
                try
                {
                    var c = Traverse.Create(ADOBase.conductor);
                    AddHold(hold, c.Field("holdStartSound").GetValue(), c.Field("holdLoopSound").GetValue(), c.Field("holdMidSound").GetValue(), c.Field("holdEndSound").GetValue());
                }
                catch { }
                for (int i = 0; i < floors.Count; i++)
                {
                    var fl = floors[i];
                    if (fl == null || fl.plusEffects == null) continue;
                    for (int k = 0; k < fl.plusEffects.Count; k++)
                    {
                        var hs = fl.plusEffects[k] as ffxSetHoldsound;
                        if ((object)hs != null) AddHold(hold, hs.holdStartSound, hs.holdLoopSound, hs.holdMidSound, hs.holdEndSound);
                    }
                }
                names.AddRange(hold);
            }
            foreach (var n in names)
            {
                if (am.audioLib.ContainsKey(n)) continue;
                if (am.FindOrLoadAudioClip(n) != null) loaded++;
            }
            if (loaded > 0 || Edition.Dev)
                Main.Entry.Logger.Log(string.Format("[소리 미리 불러오기] 쓸 수 있는 소리 {0}가지 중 새로 불러온 것 {1}개 {2:F0}ms",
                    names.Count, loaded, (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency));
        }
    }
}
