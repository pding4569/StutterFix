using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.Rendering;

namespace StutterFix
{
    // 화면에 나갈 프레임만 그리기. 맵 화면은 곡 시각의 함수라 주사 사이 프레임의 그리기와 장식·트윈 갱신은 쓸모가 없다.
    // 주사율마다 한 번만 그리고(OnDemandRendering), 그 사이 프레임은 입력·판정·곡 시계만 돈다.
    // 트윈 시간은 모아 뒀다가 다음 갱신에 더한다. 효과가 시작될 프레임은 갱신해서 시작 시각이 어긋나지 않게 한다.
    // 히트박스 장식이 있는 맵은 위치가 판정에 쓰이므로 갱신은 건너뛰지 않는다.
    // 곡 전체 끔/켬(120Hz): HELLO 259 -> 519 FPS, Exit 370 -> 937 FPS, 끊김 0.
    internal static class DisplayPace
    {
        private struct Tick { }
        internal static bool Enabled;
        internal static bool Light;
        internal static long Frames, Draws;
        private static bool installed, playing, logicAllowed;
        private static long period, next;
        private static float pendingDt;
        private static readonly AccessTools.FieldRef<scrVfxPlus, int> vfxIndex = AccessTools.FieldRefAccess<scrVfxPlus, int>("currentVfxIndex");
        private static readonly AccessTools.FieldRef<scrDecorationManager, List<scrDecoration>> allDecos = AccessTools.FieldRefAccess<scrDecorationManager, List<scrDecoration>>("allDecorations");

        internal static void Install(Harmony h)
        {
            if (installed) return;
            installed = true;
            var comp = AccessTools.TypeByName("DG.Tweening.Core.DOTweenComponent");
            var mgr = AccessTools.TypeByName("DG.Tweening.Core.TweenManager");
            var skip = new HarmonyMethod(typeof(DisplayPace), nameof(SkipPrefix)) { priority = Priority.First };
            h.Patch(AccessTools.Method(comp, "Update"), prefix: skip, postfix: new HarmonyMethod(typeof(DisplayPace), nameof(ClearPostfix)));
            h.Patch(AccessTools.Method(mgr, "Update", new[] { AccessTools.TypeByName("DG.Tweening.UpdateType"), typeof(float), typeof(float) }), prefix: new HarmonyMethod(typeof(DisplayPace), nameof(ManagerPrefix)));
            h.Patch(AccessTools.Method(typeof(scrDecorationManager), "LateUpdate"), prefix: skip);
            h.Patch(AccessTools.Method(typeof(scrHoldRenderer), "Update"), prefix: skip);
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            var top = loop.subSystemList;
            for (int i = 0; i < top.Length; i++)
            {
                if (top[i].type != typeof(UnityEngine.PlayerLoop.EarlyUpdate)) continue;
                var subs = new PlayerLoopSystem[top[i].subSystemList.Length + 1];
                subs[0] = new PlayerLoopSystem { type = typeof(Tick), updateDelegate = OnFrame };
                Array.Copy(top[i].subSystemList, 0, subs, 1, top[i].subSystemList.Length);
                top[i].subSystemList = subs;
            }
            loop.subSystemList = top;
            PlayerLoop.SetPlayerLoop(loop);
        }

        internal static void Uninstall()
        {
            Stop();
            if (!installed) return;
            installed = false;
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            var top = loop.subSystemList;
            for (int i = 0; i < top.Length; i++)
            {
                if (top[i].type != typeof(UnityEngine.PlayerLoop.EarlyUpdate)) continue;
                var keep = new List<PlayerLoopSystem>(top[i].subSystemList);
                keep.RemoveAll(s => s.type == typeof(Tick));
                top[i].subSystemList = keep.ToArray();
            }
            loop.subSystemList = top;
            PlayerLoop.SetPlayerLoop(loop);
        }

        private static void Stop()
        {
            playing = false; Light = false; pendingDt = 0f;
            OnDemandRendering.renderFrameInterval = 1;
        }

        private static bool ShouldRun()
        {
            if (!Enabled || !Hitch.Playing || HalfRender.Enabled) return false;
            var c = scrController.instance;
            return c != null && !c.paused;
        }

        private static void Begin()
        {
            double hz = Screen.currentResolution.refreshRateRatio.value;
            period = (long)(Stopwatch.Frequency / (hz > 1 ? hz : 60));
            next = Stopwatch.GetTimestamp();
            logicAllowed = true;
            var m = scrDecorationManager.instance;
            var all = m != null ? allDecos(m) : null;
            if (all != null)
                for (int i = 0; i < all.Count; i++)
                    if (all[i] != null && all[i].useHitbox) { logicAllowed = false; break; }
            playing = true;
        }

        private static void OnFrame()
        {
            if (!ShouldRun()) { if (playing) Stop(); return; }
            if (!playing) Begin();
            long now = Stopwatch.GetTimestamp();
            bool draw = now >= next;
            if (draw) { next += period; if (next < now) next = now + period; }
            OnDemandRendering.renderFrameInterval = draw ? 1 : 1000000;
            Light = logicAllowed && !draw && !EffectDue();
            Frames++;
            if (draw) Draws++;
            if (Light) pendingDt += Time.deltaTime;
        }

        // 이번 프레임에 시작될 효과가 있으면 트윈을 먼저 맞춰 둔다
        private static bool EffectDue()
        {
            var vfx = scrVfxPlus.instance;
            var song = scrConductor.instance;
            if (vfx == null || song == null || vfx.effects == null) return false;
            int i = vfxIndex(vfx);
            if (i >= vfx.effects.Count) return false;
            var fx = vfx.effects[i];
            return fx != null && song.songposition_minusi + 2.0 * Time.deltaTime >= fx.startTime - fx.startEffectOffset;
        }

        internal static float TakeDt(float dt) { return dt + (Light ? 0f : pendingDt); }

        public static bool SkipPrefix() { return !Light; }

        public static void ClearPostfix() { if (!Light) pendingDt = 0f; }

        public static void ManagerPrefix(int updateType, ref float deltaTime)
        {
            if (updateType == 0) deltaTime += pendingDt * DG.Tweening.DOTween.timeScale;   // UpdateType.Normal
        }
    }
}
