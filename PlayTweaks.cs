using System;
using System.Collections.Generic;
using System.Reflection;
using ADOFAI;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // 연출 끄기 (설정 창 "연출 끄기" 페이지, 전부 기본 꺼짐: 켜면 화면이 달라진다)
    //  - 맵 효과 종류별 끄기: 그 효과의 StartEffect 를 건너뛴다(시작하지 않음). 이미 켜진 효과는 다음 다시 하기부터.
    //  - 판정 글자 숨기기(전부 / 완벽만): scrHitTextManager.ShowHitText 를 건너뛴다.
    //  - 플레이 중 마우스 휠로 화면 크기 바꾸기 막기: 에디터 재생 중 휠로 부르는 scnEditor.ZoomCamera 를 건너뛴다.
    // Quartz 의 효과 제거·판정 숨기기와 함께 켜도 둘 다 건너뛸 뿐이라 부딪히지 않는다.
    internal static class PlayTweaks
    {
        internal sealed class Kind
        {
            public string Ko, En, Event, Class;
            public bool Off;
        }

        internal static readonly Kind[] Kinds =
        {
            new Kind { Ko = "필터", En = "Filters", Event = "SetFilter", Class = "ffxSetFilterPlus" },
            new Kind { Ko = "고급 필터", En = "Advanced filters", Event = "SetFilterAdvanced", Class = "ffxSetFilterAdvancedPlus" },
            new Kind { Ko = "블룸", En = "Bloom", Event = "Bloom", Class = "ffxBloomPlus" },
            new Kind { Ko = "플래시", En = "Flash", Event = "Flash", Class = "ffxFlashPlus" },
            new Kind { Ko = "거울의 방", En = "Hall of mirrors", Event = "HallOfMirrors", Class = "ffxHallOfMirrorsPlus" },
            new Kind { Ko = "화면 흔들기", En = "Screen shake", Event = "ShakeScreen", Class = "ffxShakeScreenPlus" },
            new Kind { Ko = "화면 타일", En = "Screen tile", Event = "ScreenTile", Class = "ffxScreenTilePlus" },
            new Kind { Ko = "화면 스크롤", En = "Screen scroll", Event = "ScreenScroll", Class = "ffxScreenScrollPlus" },
            new Kind { Ko = "프레임레이트 연출", En = "Frame rate effects", Event = "SetFrameRate", Class = "ffxSetFrameRatePlus" },
        };

        internal static bool HideJudgeAll, HideJudgePerfect, NoPlayZoom;
        internal static long Skipped, HiddenJudge, BlockedZoom;
        private static readonly Dictionary<MethodBase, Kind> byMethod = new Dictionary<MethodBase, Kind>();

        internal static void Install(Harmony h)
        {
            int n = 0;
            foreach (var k in Kinds)
            {
                try
                {
                    var t = AccessTools.TypeByName(k.Class);
                    var m = t == null ? null : t.GetMethod("StartEffect", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, new[] { typeof(scrPlanet) }, null);   // StartEffect(scrPlanet) (부모의 StartEffect() 도 이걸 부른다)
                    if (m == null) { Main.Entry.Logger.Log("[연출 끄기] " + k.Ko + ": 효과 함수 없음"); continue; }
                    h.Patch(m, prefix: new HarmonyMethod(typeof(PlayTweaks), nameof(EffectPrefix)) { priority = Priority.First });
                    byMethod[m] = k; n++;
                }
                catch (Exception ex) { Main.Entry.Logger.Log("[연출 끄기] " + k.Ko + " 설치 실패: " + ex.Message); }
            }
            try
            {
                var show = AccessTools.Method(AccessTools.TypeByName("scrHitTextManager"), "ShowHitText");
                if (show != null) h.Patch(show, prefix: new HarmonyMethod(typeof(PlayTweaks), nameof(HitTextPrefix)) { priority = Priority.First });
                var zoom = AccessTools.Method(typeof(scnEditor), "ZoomCamera");
                if (zoom != null) h.Patch(zoom, prefix: new HarmonyMethod(typeof(PlayTweaks), nameof(ZoomPrefix)) { priority = Priority.First });
            }
            catch (Exception ex) { Main.Entry.Logger.Log("[연출 끄기] 판정 글자·확대 설치 실패: " + ex.Message); }
            Main.Entry.Logger.Log("[연출 끄기] 설치 (효과 " + n + "종)");
        }

        public static bool EffectPrefix(MethodBase __originalMethod)
        {
            Kind k;
            if (byMethod.TryGetValue(__originalMethod, out k) && k.Off) { Skipped++; return false; }
            return true;
        }

        public static bool HitTextPrefix(HitMargin hitMargin)
        {
            if (HideJudgeAll || (HideJudgePerfect && hitMargin == HitMargin.Perfect)) { HiddenJudge++; return false; }
            return true;
        }

        // 휠로 부른 것만 (에디터 재생 중, Ctrl 없이 휠을 굴린 프레임)
        public static bool ZoomPrefix(scnEditor __instance)
        {
            if (!NoPlayZoom || __instance == null || !__instance.playMode) return true;
            if (Mathf.Abs(Input.mouseScrollDelta.y) <= 0.05f) return true;
            BlockedZoom++;
            return false;
        }

        // 지금 맵에 효과가 종류별로 몇 개 있는지 (설정 창이 맵이 바뀔 때만 다시 센다)
        private static object countedFor;
        private static readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        internal static int CountIn(string eventName)
        {
            var ld = scnGame.instance != null ? scnGame.instance.levelData : null;
            if (!ReferenceEquals(ld, countedFor))
            {
                countedFor = ld; counts.Clear();
                if (ld != null && ld.levelEvents != null)
                    foreach (var ev in ld.levelEvents)
                    {
                        if (ev == null) continue;
                        string name = ev.eventType.ToString();
                        int c; counts.TryGetValue(name, out c); counts[name] = c + 1;
                    }
            }
            int r; return ld == null ? -1 : counts.TryGetValue(eventName, out r) ? r : 0;
        }
        internal static bool HaveLevel { get { return scnGame.instance != null && scnGame.instance.levelData != null; } }
    }
}
