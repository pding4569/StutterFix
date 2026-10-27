using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
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
        private static bool directKill;
        internal static bool HitMeterDirectKill
        {
            get { return directKill; }
            set
            {
                if (directKill == value) return;
                directKill = value;
                ClearHandles();
            }
        }
        internal static long KillFast, KillSlow, HandKilled;
        private static int swapped, tickSwapped, handSwapped;
        private static ConditionalWeakTable<object, TickHandles> ticks = new ConditionalWeakTable<object, TickHandles>();
        private static ConditionalWeakTable<RectTransform, HandHandle> hands = new ConditionalWeakTable<RectTransform, HandHandle>();
        private static int generation;

        private sealed class Handle
        {
            internal Tween Tween;
            internal string Id;
            internal TweenCallback OnKill;
        }
        private sealed class TickHandles
        {
            internal readonly Handle[] Slots = new Handle[2];
        }
        private sealed class HandHandle { internal Handle Last; }

        internal static void Install(Harmony h)
        {
            try
            {
                var t = AccessTools.TypeByName("scrHitErrorMeter");
                if (t == null) { Main.Entry.Logger.Log("[오차 막대] 형식 없음"); return; }
                swapped = 0;
                tickSwapped = handSwapped = 0;
                // 옛 게임: 눈금 그리기 함수가 DOColor 를 직접 부른다. 알파 3.4(2026-10-06)부터는 눈금 풀(ADOFAI.ErrorMeterTick)의 Show 가 부른다.
                foreach (var name in new[] { "DrawStraightTick", "DrawCurvedTick" })
                {
                    var m = AccessTools.Method(t, name);
                    if (m != null) h.Patch(m, transpiler: new HarmonyMethod(typeof(HitMeterFade), nameof(Transpiler)));
                }
                var tick = AccessTools.TypeByName("ADOFAI.ErrorMeterTick");
                if (tick != null)
                {
                    var show = AccessTools.Method(tick, "Show");
                    if (show != null) h.Patch(show, transpiler: new HarmonyMethod(typeof(HitMeterFade), nameof(ShowTranspiler)));
                }
                var add = AccessTools.Method(t, "AddHit");
                if (add != null) h.Patch(add, transpiler: new HarmonyMethod(typeof(HitMeterFade), nameof(HandTranspiler)));
                Main.Entry.Logger.Log("[오차 막대] 눈금 가볍게 사라지기 설치 (바꾼 곳 " + swapped + "개)");
                Main.Entry.Logger.Log("[오차 막대 직접 종료] 눈금 Kill " + tickSwapped + "/2곳, 손 " + handSwapped + "/2곳 (기본 꺼짐)");
            }
            catch (Exception ex) { Main.Entry.Logger.Log("[오차 막대] 설치 실패: " + ex.Message); }
        }

        // Show 전체는 그대로 두고 Kill 과 OnKill 연결만 바꾼다. 모양이 달라진 게임/다른 모드면 원래 호출을 남긴다.
        public static IEnumerable<CodeInstruction> ShowTranspiler(IEnumerable<CodeInstruction> ins)
        {
            return TickTranspiler(Transpiler(ins));
        }

        public static IEnumerable<CodeInstruction> TickTranspiler(IEnumerable<CodeInstruction> ins)
        {
            tickSwapped = 0;
            var code = new List<CodeInstruction>(ins);
            int kills = 0, callbacks = 0;
            foreach (var c in code)
            {
                if (IsKill(c)) kills++;
                if (IsOnKill(c)) callbacks++;
            }
            if (kills != 2 || callbacks != 2) return code;
            tickSwapped = kills;
            var result = new List<CodeInstruction>();
            kills = callbacks = 0;
            foreach (var c in code)
            {
                bool kill = IsKill(c), callback = IsOnKill(c);
                if (kill || callback)
                {
                    var owner = new CodeInstruction(OpCodes.Ldarg_0);
                    c.MoveLabelsTo(owner);
                    c.MoveBlocksTo(owner);
                    result.Add(owner);
                    result.Add(new CodeInstruction(OpCodes.Ldc_I4, kill ? kills++ : callbacks++));
                    var m = (MethodInfo)c.operand;
                    c.opcode = OpCodes.Call;
                    c.operand = kill ? AccessTools.Method(typeof(HitMeterFade), nameof(KillTick))
                        : AccessTools.Method(typeof(HitMeterFade), nameof(TrackTick)).MakeGenericMethod(m.GetGenericArguments());
                }
                result.Add(c);
            }
            return result;
        }

        private static bool IsKill(CodeInstruction c)
        {
            var m = c.operand as MethodInfo;
            if (c.opcode != OpCodes.Call || m == null || m.DeclaringType != typeof(DOTween) || m.Name != "Kill") return false;
            var p = m.GetParameters();
            return p.Length == 2 && p[0].ParameterType == typeof(object) && p[1].ParameterType == typeof(bool) && m.ReturnType == typeof(int);
        }
        private static bool IsOnKill(CodeInstruction c)
        {
            var m = c.operand as MethodInfo;
            return c.opcode == OpCodes.Call && m != null && m.DeclaringType == typeof(TweenSettingsExtensions)
                && m.Name == "OnKill" && m.IsGenericMethod && m.GetParameters().Length == 2;
        }

        public static int KillTick(object id, bool complete, object owner, int slot)
        {
            TickHandles state;
            Handle handle = null;
            if (HitMeterDirectKill && ticks.TryGetValue(owner, out state)) handle = state.Slots[slot];
            if (!HitMeterDirectKill || handle == null || !string.Equals(handle.Id, id as string, StringComparison.Ordinal)
                || (handle.Tween != null && !string.Equals(handle.Tween.stringId, handle.Id, StringComparison.Ordinal)))
            {
                KillSlow++;
                int killed = DOTween.Kill(id, complete);
                // 외곽선이 한 번도 켜지지 않은 눈금도 검색 뒤에는 비어 있음을 기억한다.
                if (HitMeterDirectKill) ticks.GetOrCreateValue(owner).Slots[slot] = new Handle { Id = id as string };
                return killed;
            }
            KillFast++;
            var tween = handle.Tween;
            if (tween == null || !tween.IsActive()) return 0;
            tween.Kill(complete);
            return 1;
        }

        public static T TrackTick<T>(T tween, TweenCallback callback, object owner, int slot) where T : Tween
        {
            if (!HitMeterDirectKill) return tween.OnKill(callback);
            var handle = new Handle { Tween = tween, Id = tween.stringId };
            ticks.GetOrCreateValue(owner).Slots[slot] = handle;
            // 게임의 OnKill 을 그대로 부른다. 재활용된 DOTween 객체를 나중에 잘못 죽이지 않게 먼저 참조만 놓는다.
            return tween.OnKill(() => { handle.Tween = null; if (callback != null) callback(); });
        }

        public static IEnumerable<CodeInstruction> HandTranspiler(IEnumerable<CodeInstruction> ins)
        {
            handSwapped = 0;
            foreach (var c in ins)
            {
                var m = c.operand as MethodInfo;
                if (c.opcode == OpCodes.Call && m != null)
                {
                    var p = m.GetParameters();
                    MethodInfo replacement = null;
                    if (m.DeclaringType == typeof(ShortcutExtensions) && m.Name == "DORotateQuaternion"
                        && p.Length == 3 && p[0].ParameterType == typeof(Transform)
                        && p[1].ParameterType == typeof(Quaternion) && p[2].ParameterType == typeof(float))
                        replacement = AccessTools.Method(typeof(HitMeterFade), nameof(RotateHand));
                    if (m.DeclaringType.FullName == "DG.Tweening.DOTweenModuleUI" && m.Name == "DOAnchorPos"
                        && p.Length == 4 && p[0].ParameterType == typeof(RectTransform)
                        && p[1].ParameterType == typeof(Vector2) && p[2].ParameterType == typeof(float) && p[3].ParameterType == typeof(bool))
                        replacement = AccessTools.Method(typeof(HitMeterFade), nameof(MoveHand));
                    if (replacement != null && replacement.ReturnType == m.ReturnType) { c.operand = replacement; handSwapped++; }
                }
                yield return c;
            }
        }

        public static TweenerCore<Quaternion, Quaternion, NoOptions> RotateHand(Transform target, Quaternion endValue, float duration)
        {
            var tween = target.DORotateQuaternion(endValue, duration);
            var rect = target as RectTransform;
            if (!HitMeterDirectKill || rect == null) return tween;
            var previous = RememberHand(rect, tween);
            if (!HitMeterDirectKill || previous == null) return tween;
            int epoch = generation;
            var getter = tween.getter;
            tween.getter = () => { var value = getter(); RetireHand(previous, tween, epoch); return value; };
            return tween;
        }

        public static TweenerCore<Vector2, Vector2, VectorOptions> MoveHand(RectTransform target, Vector2 endValue, float duration, bool snapping)
        {
            var tween = DOTween.To(() => target.anchoredPosition, x => target.anchoredPosition = x, endValue, duration);
            tween.SetOptions(snapping);
            tween.SetTarget(target);
            if (!HitMeterDirectKill) return tween;
            var previous = RememberHand(target, tween);
            if (!HitMeterDirectKill || previous == null) return tween;
            int epoch = generation;
            var getter = tween.getter;
            tween.getter = () => { var value = getter(); RetireHand(previous, tween, epoch); return value; };
            return tween;
        }

        private static Handle RememberHand(RectTransform target, Tween tween)
        {
            var state = hands.GetOrCreateValue(target);
            var previous = state.Last;
            var handle = new Handle { Tween = tween };
            state.Last = handle;
            handle.OnKill = () => handle.Tween = null;
            tween.onKill += handle.OnKill;
            return previous;
        }
        private static void RetireHand(Handle previous, Tween current, int epoch)
        {
            // 만들 때 끊으면 다음 프레임의 이전 트윈 진행분이 시작값에서 빠진다. 원래 getter 가 읽은 직후 끊는다.
            if (epoch != generation || !HitMeterDirectKill) return;
            var tween = previous.Tween;
            if (tween == null || !tween.IsActive()) return;
            // 다른 모드가 콜백이나 재생 규칙을 바꿨으면 조기 종료하지 않는다.
            if (tween.onKill != previous.OnKill || tween.onComplete != null || tween.onUpdate != null
                || tween.onStepComplete != null || tween.onRewind != null || tween.onPlay != null
                || !tween.IsPlaying() || tween.Loops() != 1 || tween.Delay() != 0f
                || tween.Duration(false) != current.Duration(false) || tween.timeScale != current.timeScale) return;
            tween.Kill(false);   // 목표값을 쓰지 않고 내부 OnKill 로 참조만 놓는다.
            HandKilled++;
        }

        internal static void Shutdown()
        {
            HitMeterDirectKill = false;
            ClearHandles();
            Fast = Slow = KillFast = KillSlow = HandKilled = 0;
            swapped = tickSwapped = handSwapped = 0;
        }

        private static void ClearHandles()
        {
            generation++;
            ticks = new ConditionalWeakTable<object, TickHandles>();
            hands = new ConditionalWeakTable<RectTransform, HandHandle>();
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

        internal static string Summary() { return string.Format("오차 막대 눈금 가볍게 {0}개 (원래대로 {1}개), Kill 직접 {2}/검색 {3}, 손 종료 {4}", Fast, Slow, KillFast, KillSlow, HandKilled); }
    }
}
