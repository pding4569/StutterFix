using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DG.Tweening;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // 화면 밖(TileCull 이 렌더러를 꺼 둔) 타일의 색 바꾸기 엔진 호출을 타일이 다시 켜질 때까지 미룬다. 기본 꺼짐.
    //
    // 원래 코드(ffxRecolorFloorPlus.StartEffect, 타일마다): styleNum 대입, UpdateAngle(false), (스타일이 있으면) SetTrackStyle,
    // 움직임 정리(Kill(true)), ColorFloor. 실측(HELLO): 타일 하나 6.5us 중 SetTrackStyle 3.2 / ColorFloor 1.5 / UpdateAngle 1.1us,
    // 한 효과가 타일 4,800개(대부분 화면 밖)를 칠해 한 번에 7~10ms 였다. 화면 밖 타일은 그리지 않으므로 엔진 쪽 값(재질 값, 메시 각도,
    // 색 애니메이션)은 그 타일이 다시 켜질 때(TileCull.Uncull, 움직여 관리에서 뺄 때, 전부 켤 때) 하나로 모아 적용해도 같다.
    //
    // 미루지 않는 것: 지금 화면 안(렌더러 켜짐)인 타일, 자유 이동 영역 타일, 무지개 색, 모양 렌더러가 아닌 타일 -> 원래 호출 그대로.
    // 미루는 호출은 관리 값(styleNum, special* 필드, specialTimeOffset)을 원래와 똑같이 즉시 쓰고, 엔진 쪽만 기록한다.
    //   UpdateAngle: 있었는지만 (각도는 곡 중 안 바뀜). SetTrackStyle: 마지막 인자 하나 (모든 재질 값을 덮어쓰므로).
    //   색: 순서대로 목록. 마지막 하나는 지난 시간이 길이보다 짧으면 같은 애니메이션을 만들어 그 시간만큼 앞으로 보낸다(Goto).
    //   앞의 것은 원래도 다음 효과의 Kill(true)로 끝값이 되므로 끝값만 적용한다.
    // 어느 한 호출이라도 미룰 수 없으면 그 타일의 밀린 것을 먼저 적용하고 원래대로 부른다(순서 유지).
    internal static class RecolorDefer
    {
        internal static bool Enabled;
        internal static bool Installed;
        internal static long Deferred, FlushedFloors, Fallbacks, Skipped, Drained;
        internal static double FlushMs, FlushWorstMs;
        internal static int Count { get { return pending.Count; } }

        private struct ColorReq { public TrackColorType Type; public Color C1, C2; public int Start; public float Dur; public Ease Ease; public float Time; }
        private sealed class Pending
        {
            public bool Angle, Style, Initial;
            public TrackStyle StyleVal;
            public Color? Shadow;
            public readonly List<ColorReq> Colors = new List<ColorReq>(2);
        }
        private sealed class RefEq : IEqualityComparer<scrFloor>
        {
            public bool Equals(scrFloor a, scrFloor b) { return ReferenceEquals(a, b); }
            public int GetHashCode(scrFloor o) { return RuntimeHelpers.GetHashCode(o); }
        }
        private static readonly Dictionary<scrFloor, Pending> pending = new Dictionary<scrFloor, Pending>(new RefEq());
        private static readonly Stack<Pending> pool = new Stack<Pending>();
        private static int depth;
        private static bool bypass, flushedThisStep;
        // 미뤄 둔 것을 오래 쌓아 두면 효과가 많은 타일을 한꺼번에 움직여(타일 이동 효과가 움직이는 타일을 관리에서 빼며 밀린 것을 적용) 그 프레임이 무거워진다.
        // 그래서 프레임마다 DrainMs 만큼은 먼저 들어온 순서대로 적용해 쌓인 양을 줄인다(적용은 언제 해도 원래 호출을 늦춘 것과 같다).
        internal static float DrainMs = 0.6f;
        private static readonly List<scrFloor> order = new List<scrFloor>();
        private static int drainAt;

        internal static void Install(Harmony h)
        {
            try
            {
                System.Reflection.MethodInfo se = null;
                foreach (var m in typeof(ffxRecolorFloorPlus).GetMethods(AccessTools.all))
                    if (m.Name == "StartEffect" && m.DeclaringType == typeof(ffxRecolorFloorPlus) && !m.IsAbstract) se = m;
                if (se == null) { Main.Entry.Logger.Log("[색 바꾸기 미루기] StartEffect 없음"); return; }
                var ua = AccessTools.Method(typeof(scrFloor), "UpdateAngle", new[] { typeof(bool) });
                var st = AccessTools.Method(typeof(scrFloor), "SetTrackStyle", new[] { typeof(TrackStyle), typeof(bool), typeof(Color?) });
                var cf = AccessTools.Method(typeof(scrFloor), "ColorFloor", new[] { typeof(TrackColorType), typeof(Color), typeof(Color), typeof(float), typeof(TrackColorPulse), typeof(float), typeof(int), typeof(float), typeof(Ease) });
                if (ua == null || st == null || cf == null) { Main.Entry.Logger.Log("[색 바꾸기 미루기] 대상 함수 없음 (UpdateAngle " + (ua != null) + ", SetTrackStyle " + (st != null) + ", ColorFloor " + (cf != null) + ")"); return; }
                h.Patch(se, prefix: new HarmonyMethod(typeof(RecolorDefer), nameof(EnterPrefix)), finalizer: new HarmonyMethod(typeof(RecolorDefer), nameof(ExitFinalizer)));
                h.Patch(ua, prefix: new HarmonyMethod(typeof(RecolorDefer), nameof(UpdateAnglePrefix)));
                h.Patch(st, prefix: new HarmonyMethod(typeof(RecolorDefer), nameof(SetTrackStylePrefix)));
                h.Patch(cf, prefix: new HarmonyMethod(typeof(RecolorDefer), nameof(ColorFloorPrefix)));
                Installed = true;
            }
            catch (Exception ex) { Main.Entry.Logger.Log("[색 바꾸기 미루기] 설치 실패: " + ex.Message); }
        }

        public static void EnterPrefix() { depth++; }
        public static void ExitFinalizer() { if (depth > 0) depth--; }

        private static bool CanDefer(scrFloor f)
        {
            return Enabled && Installed && depth > 0 && !bypass && !ReferenceEquals(f, null) && f.freeroamArea == null && !f.freeroamGenerated
                && f.floorRenderer is FloorMeshRenderer && TileCull.IsCulled(f);
        }

        private static Pending Get(scrFloor f)
        {
            Pending p;
            if (pending.TryGetValue(f, out p)) return p;
            p = pool.Count > 0 ? pool.Pop() : new Pending();
            pending[f] = p;
            order.Add(f);
            return p;
        }

        // ── 가로채기 ──
        public static bool UpdateAnglePrefix(scrFloor __instance, bool rotate)
        {
            if (bypass) return true;
            if (rotate || !CanDefer(__instance)) { FlushIf(__instance); return true; }
            Get(__instance).Angle = true;
            Skipped++;
            return false;
        }

        public static bool SetTrackStylePrefix(scrFloor __instance, TrackStyle style, bool initial, Color? customShadowColor)
        {
            if (bypass) return true;
            if (initial || !CanDefer(__instance)) { FlushIf(__instance); return true; }
            var p = Get(__instance);
            p.Style = true; p.StyleVal = style; p.Initial = initial; p.Shadow = customShadowColor;
            Skipped++;
            return false;
        }

        public static bool ColorFloorPrefix(scrFloor __instance, TrackColorType colorType, Color color1, Color color2, float animDuration, TrackColorPulse pulseType, float pulseLength, int startOfColorChange, float duration, Ease ease)
        {
            if (bypass) return true;
            bool supported = colorType == TrackColorType.Single || colorType == TrackColorType.Stripes || colorType == TrackColorType.Glow
                || colorType == TrackColorType.Blink || colorType == TrackColorType.Switch || colorType == TrackColorType.Volume;
            if (!supported || !CanDefer(__instance)) { FlushIf(__instance); if (depth > 0 && Enabled) Fallbacks++; return true; }
            var f = __instance;
            // 원래 ColorFloor 의 관리 값 쓰기 (엔진 호출 앞부분)
            f.specialColorType = colorType;
            f.specialColorTypeStart = startOfColorChange;
            f.specialColor1 = color1;
            f.specialColor2 = color2;
            f.specialAnimDuration = animDuration;
            f.specialColorPulse = pulseType;
            f.specialPulseLength = pulseLength;
            if ((colorType == TrackColorType.Single || colorType == TrackColorType.Stripes) && startOfColorChange == -1)
            { Skipped++; return false; }   // 원래도 여기서 돌아간다 (펄스 계산 앞)
            Get(f).Colors.Add(new ColorReq { Type = colorType, C1 = color1, C2 = color2, Start = startOfColorChange, Dur = duration, Ease = ease, Time = Time.time });
            switch (pulseType)
            {
                case TrackColorPulse.None:
                    f.specialTimeOffset = 0f;
                    break;
                case TrackColorPulse.Forward:
                    f.specialTimeOffset = (1f - 1f / pulseLength * ((float)f.seqID % pulseLength)) * animDuration;
                    break;
                case TrackColorPulse.Backward:
                    f.specialTimeOffset = 1f / pulseLength * ((float)f.seqID % pulseLength) * animDuration;
                    break;
            }
            Deferred++;
            return false;
        }

        // ── 적용 ──
        private static void FlushIf(scrFloor f)
        {
            if (pending.Count == 0 || ReferenceEquals(f, null)) return;
            Pending p;
            if (!pending.TryGetValue(f, out p)) return;
            Apply(f, p);
        }

        // TileCull.Uncull 에서: 렌더러를 켜기 직전
        internal static void FlushFloor(scrFloor f)
        {
            if (pending.Count == 0) return;
            FlushIf(f);
        }

        internal static void FlushAll()
        {
            if (pending.Count == 0) return;
            var list = new List<KeyValuePair<scrFloor, Pending>>(pending);
            foreach (var kv in list) Apply(kv.Key, kv.Value);
        }

        // PlayerLoop 의 TileCull 단계가 끝난 뒤: 방금 적용한 타일 메시를 그리기 전에 만든다
        internal static void AfterStep()
        {
            if (pending.Count > 0 && DrainMs > 0f) Drain();
            if (pending.Count == 0 && order.Count > 0) { order.Clear(); drainAt = 0; }
            if (!flushedThisStep) return;
            flushedThisStep = false;
            try { FloorMesh.UpdateAllRequired(); } catch (Exception ex) { Main.Entry.Logger.Log("[색 바꾸기 미루기] 메시 갱신 실패: " + ex.Message); }
        }

        private static void Drain()
        {
            long t0 = Stopwatch.GetTimestamp();
            long limit = (long)(DrainMs / 1000.0 * Stopwatch.Frequency);
            while (drainAt < order.Count && Stopwatch.GetTimestamp() - t0 < limit)
            {
                var f = order[drainAt++];
                Pending p;
                if (!ReferenceEquals(f, null) && pending.TryGetValue(f, out p)) { Apply(f, p); Drained++; }
            }
            if (drainAt >= order.Count) { order.Clear(); drainAt = 0; }
            else if (drainAt > 8192) { order.RemoveRange(0, drainAt); drainAt = 0; }
        }

        private static void Apply(scrFloor f, Pending p)
        {
            long t0 = Stopwatch.GetTimestamp();
            pending.Remove(f);
            bool prev = bypass;
            bypass = true;
            try
            {
                if (!ReferenceEquals(f, null) && f)
                {
                    if (p.Angle) f.UpdateAngle(false);
                    if (p.Style) f.SetTrackStyle(p.StyleVal, p.Initial, p.Shadow);
                    int n = p.Colors.Count;
                    for (int i = 0; i < n; i++) ApplyColor(f, p.Colors[i], i == n - 1);
                    flushedThisStep = true;
                }
            }
            catch (Exception ex) { Main.Entry.Logger.Log("[색 바꾸기 미루기] 적용 오류: " + ex.Message); }
            finally
            {
                bypass = prev;
                p.Angle = p.Style = p.Initial = false; p.Shadow = null; p.Colors.Clear();
                pool.Push(p);
                FlushedFloors++;
                double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                FlushMs += ms; if (ms > FlushWorstMs) FlushWorstMs = ms;
            }
        }

        private static void ApplyColor(scrFloor f, ColorReq r, bool last)
        {
            switch (r.Type)
            {
                case TrackColorType.Single: Tween(f, r.C1, r, last); break;
                case TrackColorType.Stripes:
                    {
                        bool flag = (r.Start - f.seqID) % 2 == 0;   // 자유 이동 영역 타일은 미루지 않았다
                        Tween(f, flag ? r.C1 : r.C2, r, last);
                        break;
                    }
                default:   // Glow, Blink, Switch, Volume
                    f.SetColor(Color.white);
                    f.floorRenderer.color = r.C1;
                    break;
            }
        }

        // 원래: TweenColor(색, 길이, 이징). 같은 DOTween 을 만들어(끝값 계산이 시작값 + 변화량 x 1 이라 SetColor(끝값)과 마지막 비트가 다를 수 있다)
        // 앞의 것은 다음 효과의 Kill(true)처럼 끝내고, 마지막은 지난 시간이 길이를 넘었으면 끝내고 아니면 그 시간만큼 앞으로 보낸다.
        private static void Tween(scrFloor f, Color color, ColorReq r, bool last)
        {
            var t = f.TweenColor(color, r.Dur, r.Ease);
            if (t == null) return;
            float elapsed = Time.time - r.Time;
            if (!last || !(r.Dur > 0f) || elapsed >= r.Dur) t.Kill(true);
            else if (elapsed > 0f) t.Goto(elapsed, true);
        }

        internal static void Shutdown() { Enabled = false; FlushAll(); }
        internal static string State
        {
            get
            {
                return "enabled=" + Enabled + " installed=" + Installed + " pending=" + pending.Count + " deferred=" + Deferred + " skippedCalls=" + Skipped
                    + " flushedFloors=" + FlushedFloors + " flushMs=" + FlushMs.ToString("F1") + " flushWorstMs=" + FlushWorstMs.ToString("F2") + " fallbacks=" + Fallbacks + " drained=" + Drained;
            }
        }
    }
}
