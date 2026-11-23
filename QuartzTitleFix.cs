using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace StutterFix
{
    // 곡 제목 글자 켜고 끄기 왕복 없애기 (Quartz 곡 제목 기능과 같이 쓸 때).
    // 게임 scrHUDText.Update 는 매 프레임 제목 글자(gui)를 켜고(gui.enabled = flag), Quartz 의 SongTitleOverlay postfix 는
    // isTitle && TakesOverTitle && GameStats.InGame 이면 바로 뒤에 같은 Graphic 을 끈다. 그래서 프레임마다 OnEnable/OnDisable 이 생기고
    // 캔버스가 다시 계산된다(HELLO 2026 메인 스레드의 scrHUDText.Update 1.2%, 그중 74% 가 Behaviour.SetEnabled).
    // 같은 조건이고 글자가 이미 꺼져 있으면 게임의 "켜기"만 건너뛰고 색 설정은 원래 순서대로 한다. 끝 상태(꺼짐)는 같다.
    // Quartz 의 속성을 찾지 못하거나 읽기에 실패하면 원래대로 둔다.
    internal static class QuartzTitleFix
    {
        internal static bool Enabled = true;
        internal static long Skipped;
        private static Func<bool> takesOver, inGame;
        private static int resolveFrame = -100000;
        private static bool resolved;
        private static readonly AccessTools.FieldRef<scrHUDText, Graphic> guiRef = AccessTools.FieldRefAccess<scrHUDText, Graphic>("gui");
        private static readonly AccessTools.FieldRef<scrHUDText, Shadow> shadowRef = AccessTools.FieldRefAccess<scrHUDText, Shadow>("shadow");

        internal static void Install(Harmony h)
        {
            try
            {
                var m = AccessTools.Method(typeof(scrHUDText), "Update");
                if (m == null) return;
                h.Patch(m, prefix: new HarmonyMethod(typeof(QuartzTitleFix), nameof(Prefix)) { priority = Priority.Last });
            }
            catch (Exception ex) { Lg.I("[제목 글자] 설치 실패: " + ex.Message); }
        }

        private static bool Resolve()
        {
            if (resolved) return takesOver != null;
            int f = Time.frameCount;
            if (f - resolveFrame < 300) return false;   // Quartz 가 아직 안 불렸으면 가끔만 다시 찾는다
            resolveFrame = f;
            try
            {
                var t = AccessTools.TypeByName("Quartz.Features.SongTitle.SongTitleOverlay");
                var g = AccessTools.TypeByName("Quartz.Game.Stats.GameStats");
                var a = t?.GetProperty("TakesOverTitle", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetGetMethod(true);
                var b = g?.GetProperty("InGame", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetGetMethod(true);
                if (a == null || b == null || a.ReturnType != typeof(bool) || b.ReturnType != typeof(bool)) return false;
                takesOver = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), a);
                inGame = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), b);
                resolved = true;
                Lg.I("[제목 글자] Quartz 곡 제목 기능과 맞춤");
                return true;
            }
            catch { return false; }
        }

        public static bool Prefix(scrHUDText __instance)
        {
            if (!Enabled || !__instance.isTitle || !Resolve()) return true;
            bool quartzHides;
            try { quartzHides = takesOver() && inGame(); } catch { return true; }
            if (!quartzHides) return true;
            var gui = guiRef(__instance);
            if ((object)gui == null || !gui || gui.enabled) return true;   // 첫 프레임 등은 원래대로(게임이 켜고 Quartz 가 끈다)
            // 원래 Update 의 flag 와 같은 식
            bool flag = (RDC.partialNoHud || (!RDC.noHud && (!GCS.d_dontShowTitles || !__instance.isTitle)))
                && (!ADOBase.isLevelEditor || !ADOBase.editor.shouldHideGameplayUIForAutoplay);
            if (!flag) return true;   // 원래도 끄는 경우: 그대로
            // 원래: gui.enabled = true(바로 뒤 Quartz 가 false) -> 건너뜀. 색은 원래 순서대로.
            if (__instance.changeColor)
            {
                gui.color = scrVfx.instance.currentColourScheme.colourText;
                var shadow = shadowRef(__instance);
                if ((bool)shadow && ADOBase.customLevel != null)
                    shadow.effectColor = scrVfx.instance.currentColourScheme.colourTextShadow;
            }
            Skipped++;
            return false;
        }
    }
}
