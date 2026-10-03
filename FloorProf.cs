using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // (개발자용 측정) 타일 이동 효과(ffxMoveFloorPlus.StartEffect 의 지역 함수 TweenFloor) 안에서 DOTween 이 쓰는 몫.
    // Lost Requiem 531.8초: 효과 하나 45ms(타일 8,171개, 트윈 2만 개). 모드가 애니메이션을 직접 돌리면 줄일 수 있는 것은 DOTween 몫뿐이라 먼저 잰다.
    // DOTween.To / SetEase / OnUpdate / Done / Kill / DOScale 호출만 시간 재는 래퍼로 바꾼다. 효과가 끝날 때 5ms 넘으면 남긴다.
    internal static class FloorProf
    {
        private static double toMs, easeMs, updMs, doneMs, killMs, scaleMs;
        private static int toN, killN, doneN;

        internal static void Install(Harmony h)
        {
            var types = new List<Type> { typeof(ffxMoveFloorPlus) };
            types.AddRange(typeof(ffxMoveFloorPlus).GetNestedTypes(AccessTools.all));
            MethodInfo target = null;
            foreach (var t in types)
                foreach (var m in t.GetMethods(AccessTools.all))
                    if (m.DeclaringType == t && m.Name.Contains("g__TweenFloor")) target = m;
            if (target == null) { Main.Entry.Logger.Log("[타일 이동 측정] 지역 함수 없음"); return; }
            h.Patch(target, transpiler: new HarmonyMethod(typeof(FloorProf), nameof(Transpiler)));
            MethodBase se = null;
            foreach (var m in typeof(ffxMoveFloorPlus).GetMethods(AccessTools.all)) if (m.Name == "StartEffect" && m.DeclaringType == typeof(ffxMoveFloorPlus)) se = m;
            if (se != null) h.Patch(se, prefix: new HarmonyMethod(typeof(FloorProf), nameof(SePre)), postfix: new HarmonyMethod(typeof(FloorProf), nameof(SePost)));
            Main.Entry.Logger.Log("[타일 이동 측정] 설치 (바꾼 호출 " + replaced + "개)");
        }
        private static int replaced;
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> ins)
        {
            foreach (var c in ins)
            {
                var mi = c.operand as MethodInfo;
                MethodInfo rep = null;
                if (mi != null)
                {
                    if (mi.DeclaringType == typeof(DOTween) && mi.Name == "To" && mi.GetParameters().Length == 4 && mi.GetParameters()[2].ParameterType == typeof(float)) rep = AccessTools.Method(typeof(FloorProf), nameof(To));
                    else if (mi.DeclaringType == typeof(TweenExtensions) && mi.Name == "Kill" && !mi.IsGenericMethod && mi.GetParameters().Length == 2) rep = AccessTools.Method(typeof(FloorProf), nameof(Kill));
                    else if (mi.IsGenericMethod && mi.Name == "Done" && mi.GetGenericArguments()[0] == typeof(TweenerCore<float, float, FloatOptions>)) rep = AccessTools.Method(typeof(FloorProf), nameof(DoneF));
                    else if (mi.IsGenericMethod && mi.Name == "SetEase" && mi.GetParameters().Length == 2 && mi.GetParameters()[1].ParameterType == typeof(Ease) && mi.GetGenericArguments()[0] == typeof(TweenerCore<float, float, FloatOptions>)) rep = AccessTools.Method(typeof(FloorProf), nameof(EaseF));
                    else if (mi.IsGenericMethod && mi.Name == "OnUpdate" && mi.GetGenericArguments()[0] == typeof(TweenerCore<float, float, FloatOptions>)) rep = AccessTools.Method(typeof(FloorProf), nameof(UpdF));
                    else if (mi.Name == "TweenOpacity" && mi.DeclaringType == typeof(scrFloor)) rep = AccessTools.Method(typeof(FloorProf), nameof(Opa));
                    else if (mi.Name == "get_position" && mi.DeclaringType == typeof(Transform)) rep = AccessTools.Method(typeof(FloorProf), nameof(Pos));
                    else if (mi.Name == "get_eulerAngles" && mi.DeclaringType == typeof(Transform)) rep = AccessTools.Method(typeof(FloorProf), nameof(Eul));
                    else if (mi.Name == "get_localScale" && mi.DeclaringType == typeof(Transform)) rep = AccessTools.Method(typeof(FloorProf), nameof(Scl));
                    else if (mi.Name == "get_transform" && mi.DeclaringType == typeof(Component)) rep = AccessTools.Method(typeof(FloorProf), nameof(Trn));
                    else if (mi.Name == "DOScale" && mi.DeclaringType == typeof(ShortcutExtensions) && mi.GetParameters().Length == 3 && mi.GetParameters()[1].ParameterType == typeof(Vector3)) rep = AccessTools.Method(typeof(FloorProf), nameof(Scale));
                }
                if (rep != null) { c.opcode = OpCodes.Call; c.operand = rep; replaced++; }
                yield return c;
            }
        }
        private static long TS() { return Stopwatch.GetTimestamp(); }
        private static double Ms(long a) { return (TS() - a) * 1000.0 / Stopwatch.Frequency; }
        public static TweenerCore<float, float, FloatOptions> To(DOGetter<float> g, DOSetter<float> s, float e, float d) { long a = TS(); var r = DOTween.To(g, s, e, d); toMs += Ms(a); toN++; return r; }
        public static void Kill(Tween t, bool complete) { long a = TS(); t.Kill(complete); killMs += Ms(a); killN++; }
        public static TweenerCore<float, float, FloatOptions> DoneF(TweenerCore<float, float, FloatOptions> t) { long a = TS(); var r = t.Done(); doneMs += Ms(a); doneN++; return r; }
        public static TweenerCore<float, float, FloatOptions> EaseF(TweenerCore<float, float, FloatOptions> t, Ease e) { long a = TS(); var r = t.SetEase(e); easeMs += Ms(a); return r; }
        public static TweenerCore<float, float, FloatOptions> UpdF(TweenerCore<float, float, FloatOptions> t, TweenCallback cb) { long a = TS(); var r = t.OnUpdate(cb); updMs += Ms(a); return r; }
        public static TweenerCore<Vector3, Vector3, VectorOptions> Scale(Transform tr, Vector3 v, float d) { long a = TS(); var r = tr.DOScale(v, d); scaleMs += Ms(a); return r; }

        private static double opaMs, readMs; private static int opaN, readN;
        public static Tween Opa(scrFloor f, float v, float d, Ease e) { long a = TS(); var r = f.TweenOpacity(v, d, e); opaMs += Ms(a); opaN++; return r; }
        public static Vector3 Pos(Transform t) { long a = TS(); var r = t.position; readMs += Ms(a); readN++; return r; }
        public static Vector3 Eul(Transform t) { long a = TS(); var r = t.eulerAngles; readMs += Ms(a); readN++; return r; }
        public static Vector3 Scl(Transform t) { long a = TS(); var r = t.localScale; readMs += Ms(a); readN++; return r; }
        public static Transform Trn(Component c) { long a = TS(); var r = c.transform; readMs += Ms(a); readN++; return r; }
        private static long se0; private static int logged;
        public static void SePre() { se0 = TS(); toMs = easeMs = updMs = doneMs = killMs = scaleMs = 0; toN = killN = doneN = 0; opaMs = readMs = 0; opaN = readN = 0; }
        public static void SePost()
        {
            double ms = Ms(se0);
            if (ms < 5 || logged >= 40) return;
            logged++;
            double dt = toMs + easeMs + updMs + doneMs + killMs + scaleMs;
            Main.Entry.Logger.Log(string.Format("[타일 이동 측정] 효과 {0:F1}ms 중 DOTween {1:F1}ms (만들기 {2}번 {3:F1}, SetEase {4:F1}, OnUpdate {5:F1}, Done {6}번 {7:F1}, 끝내기 {8}번 {9:F1}, 크기 {10:F1}) | 불투명도 {12}번 {13:F1}ms | transform 읽기 {14}번 {15:F1}ms | 나머지 {11:F1}ms",
                ms, dt, toN, toMs, easeMs, updMs, doneN, doneMs, killN, killMs, scaleMs, ms - dt - opaMs - readMs, opaN, opaMs, readN, readMs));
        }
    }
}
