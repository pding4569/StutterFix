using System;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace StutterFix
{
    // 판정 오차 막대(scrHitErrorMeter) 눈금 가볍게 사라지기.
    // 게임은 눈금마다 tickImage.DOColor(색.WithAlpha(0), 3초) 로 사라지게 한다. Image.color 를 매 프레임 바꾸면
    // 그 눈금 그림(Graphic)을 다시 만들고(OnPopulateMesh) 캔버스도 다시 묶는다. 눈금이 최대 60개라 빠른 구간에서 매 프레임 수십 개.
    // 바꾸는 것은 투명도뿐이므로(시작·끝 RGB 가 같다) Image.color 는 시작 색 그대로 두고 CanvasRenderer 의 투명도만 바꾼다.
    // 화면 투명도 = 정점 투명도(시작 a0) x 렌더러 투명도. 같은 Color 트윈을 돌려 나온 값 x.a 로 렌더러 투명도 = x.a / a0 를 넣으므로
    // 결과는 a0 x (x.a / a0) = 원래 코드가 정점에 넣던 x.a (정점 색은 바이트로 반올림되므로 차이는 1/255 미만).
    // 이징·길이·ID·끊기(OnKill 에서 색을 지움)는 원래 코드가 그대로 붙인다. 바꾸는 것은 DOColor 호출 하나.
    internal static class HitMeterFade
    {
        internal static bool Enabled = true;
        internal static long Fast, Slow;
        private static int swapped;

        internal static void Install(Harmony h)
        {
            try
            {
                var t = AccessTools.TypeByName("scrHitErrorMeter");
                if (t == null) { Main.Entry.Logger.Log("[오차 막대] 형식 없음"); return; }
                swapped = 0;
                foreach (var name in new[] { "DrawStraightTick", "DrawCurvedTick" })
                {
                    var m = AccessTools.Method(t, name);
                    if (m != null) h.Patch(m, transpiler: new HarmonyMethod(typeof(HitMeterFade), nameof(Transpiler)));
                }
                Main.Entry.Logger.Log("[오차 막대] 눈금 가볍게 사라지기 설치 (바꾼 곳 " + swapped + "개)");
            }
            catch (Exception ex) { Main.Entry.Logger.Log("[오차 막대] 설치 실패: " + ex.Message); }
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
        {
            var my = AccessTools.Method(typeof(HitMeterFade), nameof(Fade));
            foreach (var c in ins)
            {
                var m = c.operand as MethodInfo;
                if ((c.opcode == System.Reflection.Emit.OpCodes.Call || c.opcode == System.Reflection.Emit.OpCodes.Callvirt) && m != null && m.Name == "DOColor" && m.IsStatic)
                {
                    var p = m.GetParameters();
                    if (p.Length == 3 && p[0].ParameterType == typeof(Image) && p[1].ParameterType == typeof(Color) && p[2].ParameterType == typeof(float) && m.ReturnType == my.ReturnType)
                    { c.opcode = System.Reflection.Emit.OpCodes.Call; c.operand = my; swapped++; }
                }
                yield return c;
            }
        }

        // DOTweenModuleUI.DOColor(Image, Color, float) 와 같은 모양
        public static TweenerCore<Color, Color, ColorOptions> Fade(Image target, Color endValue, float duration)
        {
            Color start = target.color;
            var cr = target.canvasRenderer;
            // 원래 길: 시작·끝 RGB 가 다르거나(다른 모드가 바꿈) 끄면
            if (!Enabled || cr == null || start.r != endValue.r || start.g != endValue.g || start.b != endValue.b || start.a <= 0f)
            {
                Slow++;
                return DOTween.To(() => target.color, x => target.color = x, endValue, duration).SetTarget(target);
            }
            Fast++;
            float a0 = start.a;
            cr.SetAlpha(1f);   // 지난 눈금이 남긴 렌더러 투명도 (정점 색은 원래 코드가 방금 시작 색으로 넣었다)
            Color cur = start;
            return DOTween.To(() => cur, x => { cur = x; cr.SetAlpha(x.a / a0); }, endValue, duration).SetTarget(target);
        }

        internal static string Summary() { return string.Format("오차 막대 눈금 가볍게 {0}개 (원래대로 {1}개)", Fast, Slow); }
    }
}
