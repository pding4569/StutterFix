using System;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // 타일 이동 애니메이션 직접 처리: 길이 있는 타일 이동 효과(ffxMoveFloorPlus, MoveTrack)의 애니메이션을 DOTween 대신 모드가 돌린다.
    //
    // 측정 (Lost Requiem 531.8초, 효과 하나 53ms, 타일 수천 개·트윈 2만 개): DOTween 16.8ms(끝내기 9.3, 크기 4.2, 만들기 2.8) + 불투명도 5.4
    // + transform 읽기 4.0 + 나머지 26.8 (타일마다 클로저·델리게이트, 사전 두 번 찾기 등). 장식(DecoAnim)과 같은 방식으로 맡는다.
    //
    // 원래 코드 (ffxMoveFloorPlus.StartEffect, 디컴파일로 확인): AdjustDurationForHardbake -> start/end 바꾸기 -> start..end (간격 gapLength) 타일과
    // 그 타일의 자유 이동 구역 중 착지 가능한 타일마다 TweenFloor:
    //   위치 X/Y: 이전 것 Kill(true) -> 지금 position 과 목표가 Approximately 면 안 만듦 -> DOTween.To(getter position.x, setter MoveX, 목표, 길이).SetEase.Done
    //   회전: Kill(true) -> eulerAngles.z 비교 -> To(getter tweenRot.z, setter tweenRot.z = r, 목표).SetEase.OnUpdate(eulerAngles = tweenRot).Done
    //   크기 X/Y: GetValueOrDefault?.Kill(true) -> localScale(만들 때).WithX(목표) 와 ApproximatelyXY -> DOScale(벡터, 길이).SetEase.SetOptions(축).Done
    //   불투명도: Kill(true) -> opacity 비교 -> TweenOpacity(목표, 길이, ease) (길이 > 0 이면 To(getter opacity, setter opacity), dontChangeMySprite = true)
    // 시작값은 DOTween 처럼 첫 갱신 때 getter 로 읽는다. 진행·끝내기·목록 순서는 DecoAnim 과 같다(같은 이징 함수, 같은 dt).
    // 길이 0 인 효과(Done 이 바로 끝냄)는 같은 계산으로 값을 바로 쓴다(ZeroTween 과 같은 식). 곡 시작 직후(되감기 구간)는 원래 코드가 돈다.
    //
    // 원래 게임은 같은 타일의 다른 진행 중 애니메이션(타일 나타나기의 위치·크기)과 겹치면 나중에 만든 것이 이긴다. 모드 애니메이션은
    // DOTween 갱신보다 먼저 돌아 순서가 바뀌므로, 그런 타일의 그 속성은 진짜 DOTween 으로 만든다(원래 코드와 같은 호출).
    //
    // 개발자용 검증: 64개 중 1개는 진짜 DOTween 애니메이션을 옆에 같이 돌려(값만 받아 둠) 매 프레임 비트 단위로 비교하고 끝나는 프레임도 비교한다.
    internal static class FloorAnim
    {
        internal static bool Enabled = true;
        internal static bool Installed;
        internal static long Effects, Fallbacks, Tiles, Created, Completed, Killed, Dropped, Frames, Steps, Reused, Real, VerifyN, VerifySteps, VerifyMismatch, Errors, Listed;
        internal static int Peak;
        internal static double UpdateMs, StartMs, LastFrameMs;
        internal static string First = "";
        private static readonly long[] whyNot = new long[4];

        private const int PX = 0, PY = 1, ROT = 2, SX = 3, SY = 4, OPA = 5;

        internal sealed class Rec
        {
            public scrFloor F; public Transform T; public int Key; public Tween Proxy;
            public float Dur, Pos; public bool Started, Running = true, Stepped, InList;
            public Ease E; public float Over, Period;
            public float FStart, FEnd, FChange, FLast;
            public Vector3 VStart, VEnd, VChange, VLast;
            // 개발자용 짝 (진짜 DOTween)
            public Tween Shadow; public float SF; public Vector3 SV; public bool SDone, SStepped;
        }

        private static readonly List<Rec> recs = new List<Rec>();
        private static MethodBase startEffect;

        private static readonly AccessTools.FieldRef<ffxMoveFloorPlus, int> startRef = AccessTools.FieldRefAccess<ffxMoveFloorPlus, int>("start");
        private static readonly AccessTools.FieldRef<ffxMoveFloorPlus, int> endRef = AccessTools.FieldRefAccess<ffxMoveFloorPlus, int>("end");
        private static readonly AccessTools.FieldRef<ffxMoveFloorPlus, int> gapRef = AccessTools.FieldRefAccess<ffxMoveFloorPlus, int>("gapLength");
        private static readonly AccessTools.FieldRef<ffxMoveFloorPlus, Vector2> tPosRef = AccessTools.FieldRefAccess<ffxMoveFloorPlus, Vector2>("targetPos");
        private static readonly AccessTools.FieldRef<ffxMoveFloorPlus, float> tRotRef = AccessTools.FieldRefAccess<ffxMoveFloorPlus, float>("targetRot");
        private static readonly AccessTools.FieldRef<ffxMoveFloorPlus, Vector2> tScaleRef = AccessTools.FieldRefAccess<ffxMoveFloorPlus, Vector2>("targetScaleV2");
        private static readonly AccessTools.FieldRef<ffxMoveFloorPlus, float> tOpaRef = AccessTools.FieldRefAccess<ffxMoveFloorPlus, float>("targetOpacity");
        private static readonly AccessTools.FieldRef<ffxMoveFloorPlus, bool> posUsedRef = AccessTools.FieldRefAccess<ffxMoveFloorPlus, bool>("positionUsed");
        private static readonly AccessTools.FieldRef<ffxMoveFloorPlus, bool> rotUsedRef = AccessTools.FieldRefAccess<ffxMoveFloorPlus, bool>("rotationUsed");
        private static readonly AccessTools.FieldRef<ffxMoveFloorPlus, bool> scaleUsedRef = AccessTools.FieldRefAccess<ffxMoveFloorPlus, bool>("scaleUsed");
        private static readonly AccessTools.FieldRef<ffxMoveFloorPlus, bool> opaUsedRef = AccessTools.FieldRefAccess<ffxMoveFloorPlus, bool>("opacityUsed");
        private static readonly AccessTools.FieldRef<ffxMoveFloorPlus, scrLevelMaker> lmRef = AccessTools.FieldRefAccess<ffxMoveFloorPlus, scrLevelMaker>("levelMaker");
        private static readonly AccessTools.FieldRef<ffxPlusBase, float> durRef = AccessTools.FieldRefAccess<ffxPlusBase, float>("duration");
        private static readonly AccessTools.FieldRef<ffxPlusBase, Ease> easeRef = AccessTools.FieldRefAccess<ffxPlusBase, Ease>("ease");

        internal static void Install(Harmony h)
        {
            try
            {
                if (!ZeroTween.CanEase) { Main.Entry.Logger.Log("[타일 애니메이션] 이징 함수가 없어 끔"); return; }
                foreach (var m in typeof(ffxMoveFloorPlus).GetMethods(AccessTools.all))
                    if (m.Name == "StartEffect" && m.DeclaringType == typeof(ffxMoveFloorPlus) && !m.IsAbstract && m.GetParameters().Length == 1) startEffect = m;
                var comp = AccessTools.TypeByName("DG.Tweening.Core.DOTweenComponent");
                var upd = comp == null ? null : AccessTools.Method(comp, "Update");
                if (startEffect == null || upd == null) { Main.Entry.Logger.Log("[타일 애니메이션] 게임 코드 모양이 달라 끔"); return; }
                // 원래 코드의 필드 이름을 먼저 확인 (없으면 위 FieldRef 가 예외)
                if (startRef == null || tScaleRef == null || opaUsedRef == null) return;

                var kill = AccessTools.Method(typeof(TweenExtensions), "Kill", new[] { typeof(Tween), typeof(bool) });
                h.Patch(kill, prefix: new HarmonyMethod(typeof(FloorAnim), nameof(KillPrefix)) { priority = Priority.First });
                foreach (var m in typeof(DOTween).GetMethods(AccessTools.all))
                    if (m.Name == "KillAll" && m.GetParameters().Length >= 1 && m.GetParameters()[0].ParameterType == typeof(bool))
                        h.Patch(m, prefix: new HarmonyMethod(typeof(FloorAnim), nameof(KillAllPrefix)) { priority = Priority.First });
                var playing = AccessTools.Method(typeof(DOTween), "PlayingTweens");
                if (playing != null) h.Patch(playing, prefix: new HarmonyMethod(typeof(FloorAnim), nameof(PlayingPrefix)), postfix: new HarmonyMethod(typeof(FloorAnim), nameof(PlayingPostfix)));
                // (저사양 나누기) 타일을 건드리는 다른 효과·게임의 효과 끊기/되감기 전에 남은 것을 끝까지
                foreach (var t in new[] { typeof(ffxFloorAppearPlus), typeof(ffxFloorDisappearPlus) })
                    foreach (var m in t.GetMethods(AccessTools.all))
                        if (m.Name == "StartEffect" && m.DeclaringType == t && !m.IsAbstract)
                            h.Patch(m, prefix: new HarmonyMethod(typeof(FloorAnim), nameof(FlushPrefix)) { priority = Priority.First });
                foreach (var n in new[] { "Kill", "ScrubToTime" })
                {
                    var m = AccessTools.DeclaredMethod(typeof(ffxPlusBase), n);
                    if (m != null) h.Patch(m, prefix: new HarmonyMethod(typeof(FloorAnim), nameof(FlushPrefix)) { priority = Priority.First });
                }
                foreach (var m in typeof(TweenExtensions).GetMethods(AccessTools.all))
                    if (m.Name == "Complete" && m.GetParameters().Length >= 1 && m.GetParameters()[0].ParameterType == typeof(Tween))
                        h.Patch(m, prefix: new HarmonyMethod(typeof(FloorAnim), nameof(CompletePrefix)) { priority = Priority.First });
                h.Patch(upd, prefix: new HarmonyMethod(typeof(FloorAnim), nameof(UpdatePrefix)) { priority = Priority.First },
                    postfix: new HarmonyMethod(typeof(FloorAnim), nameof(UpdatePostfix)) { priority = Priority.Last });
                // 다른 모드(효과 지우기 등)·효과 나누기가 먼저 막으면 아무것도 안 한다 (__runOriginal)
                h.Patch(startEffect, prefix: new HarmonyMethod(typeof(FloorAnim), nameof(StartPrefix)) { priority = Priority.Last });
                Installed = true;
                // 첫 효과에서 JIT 와 생성자 호출기 만들기(약 6ms)가 곡 중에 일어나지 않게 미리 한다
                try
                {
                    NewProxy();
                    foreach (var n in new[] { "Run", "TweenFloor", "Prev", "New", "Step", "Apply", "Startup", "Complete", "UpdatePrefix", "UpdatePostfix", "Compact", "ZeroPos", "ZeroRot", "ZeroScale", "MaybeShadow", "StartPrefix", "KillPrefix" })
                        foreach (var m in typeof(FloorAnim).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                            if (m.Name == n && !m.IsGenericMethodDefinition) System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(m.MethodHandle);
                }
                catch { }
                Main.Entry.Logger.Log("[타일 애니메이션] 설치");
            }
            catch (Exception ex) { Installed = false; Main.Entry.Logger.Log("[타일 애니메이션] 설치 실패: " + ex.Message); }
        }

        internal static bool Active { get { return Enabled && Installed; } }

        // ── 효과 시작 ──
        public static bool StartPrefix(ffxMoveFloorPlus __instance, bool __runOriginal)
        {
            if (!__runOriginal) return false;
            if (!Active || !Hitch.Playing) return true;
            if (EffectBudget.InGrace) { whyNot[0]++; Fallbacks++; return true; }
            // 길이는 AdjustDurationForHardbake 가 음악 속도로 나누기만 하므로(공식 맵만) 부호는 그대로다.
            // 0 이면 즉시 이동(Done 이 그 자리에서 끝냄)을 같은 계산으로 바로 쓴다. 음수는 원래 코드.
            float d0 = durRef(__instance);
            if (!(d0 > 0f) && !(d0 == 0f && ZeroOk)) { whyNot[1]++; Fallbacks++; return true; }
            var lm = lmRef(__instance); if (lm == null || lm.listFloors == null) { whyNot[2]++; Fallbacks++; return true; }
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            pHead = pPos = pRot = pScale = pOpa = pKillN = pRunning = pNewObj = 0; long tiles0 = Tiles, made0 = Created;
            Run(__instance);
            double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            StartMs += ms;
            if (Prof && ms > 5 && profLogged < 40)
            {
                profLogged++;
                double f = System.Diagnostics.Stopwatch.Frequency / 1000.0;
                Main.Entry.Logger.Log(string.Format("[타일 애니메이션] (개발자용) 효과 {0:F1}ms 타일 {1}개: 앞부분 {2:F1}, 위치 {3:F1}, 회전 {4:F1}, 크기 {5:F1}, 불투명도 {6:F1}ms, 나머지 {7:F1}ms | 이전 모드 표 {8}개(진행 중 {9}), 새로 만든 애니메이션 {10}개(새 표 객체 {11})",
                    ms, Tiles - tiles0, pHead / f, pPos / f, pRot / f, pScale / f, pOpa / f, ms - (pHead + pPos + pRot + pScale + pOpa) / f, pKillN, pRunning, Created - made0, pNewObj));
            }
            return false;
        }

        // 원래 StartEffect 와 같은 순서 (예외도 같은 자리에서 난다)
        private static void Run(ffxMoveFloorPlus fx)
        {
            fx.AdjustDurationForHardbake();
            if (endRef(fx) < startRef(fx)) { int n = endRef(fx); endRef(fx) = startRef(fx); startRef(fx) = n; }
            var tp = tPosRef(fx); var ts = tScaleRef(fx);
            var c = new Ctx
            {
                Pos = new Vector3(tp.x, tp.y, 0f), Rot = new Vector3(0f, 0f, tRotRef(fx)), Scale = new Vector3(ts.x, ts.y, 1f),
                PosUsed = posUsedRef(fx), RotUsed = rotUsedRef(fx), ScaleUsed = scaleUsedRef(fx), OpaUsed = opaUsedRef(fx),
                Opacity = tOpaRef(fx), Dur = durRef(fx), Ease = easeRef(fx)
            };
            ZeroTween.EaseParams(c.Ease, out c.Over, out c.Period);
            c.Zero = !(c.Dur > 0f);
            if (c.Zero) { c.K = ZeroTween.EaseEnd(c.Ease); ZeroEffects++; }
            Effects++;
            var floors = lmRef(fx).listFloors;
            int end = endRef(fx), step = 1 + gapRef(fx);
            if (Split && (end - startRef(fx)) / step + 1 > SplitMin) { RunSplit(floors, startRef(fx), end, step, c); return; }
            bool pend = jobs.Count > 0;
            for (int i = startRef(fx); i <= end; i += step)
            {
                scrFloor f = floors[i];
                if (pend) BeforeAll(f);
                TweenFloor(f, ref c);
                if (f.freeroamArea == null) continue;
                foreach (scrFloor lf in f.freeroamArea.listFloors)
                    if (lf.isLandable) { if (pend) BeforeAll(lf); TweenFloor(lf, ref c); }
            }
        }

        private struct Ctx
        {
            public Vector3 Pos, Rot, Scale;
            public bool PosUsed, RotUsed, ScaleUsed, OpaUsed;
            public float Opacity, Dur, Over, Period;
            public Ease Ease;
            public bool Zero; public float K;   // 길이 0: 이징 끝점
            public float Start;                 // (저사양 나누기) 늦게 만든 애니메이션이 이미 지났어야 할 시간
        }

        // (개발자용) 효과 하나 안의 시간 나눔: 타일 앞부분(transform·벡터) / 위치 / 회전 / 크기 / 불투명도 / 짝
        private static readonly bool Prof = Edition.Dev;
        private static int profLogged;
        private static long pHead, pPos, pRot, pScale, pOpa, pKillN, pRunning, pNewObj;
        private static long TS() { return System.Diagnostics.Stopwatch.GetTimestamp(); }

        // 원래 코드의 "이전 것 Kill(true)". 모드 표는 KillPrefix 와 같은 일을 바로 한다 (Harmony 패치 여러 겹을 거치지 않게).
        // 진짜 DOTween(또는 null)은 원래처럼 Kill(true) 를 부른다. 돌려준 값은 같은 칸 표를 다시 쓸지 정할 때만 쓴다.
        private static Tween Prev(Dictionary<global::TweenType, Tween> mt, global::TweenType k, bool skipNull)
        {
            Tween old;
            if (!mt.TryGetValue(k, out old) || (object)old == null) return null;   // Kill(null) 은 아무 일도 안 한다 (DOTween IL)
            var r = old.id as Rec;
            if (r != null)
            {
                if (Prof) pKillN++;
                if (r.Running) { if (Prof) pRunning++; Killed++; Complete(r); }
                return old;
            }
            if (old.active) old.Kill(true);   // 끝난(active = false) 진짜 DOTween 은 Kill 이 바로 돌아온다
            return null;
        }

        private static void TweenFloor(scrFloor target, ref Ctx c)
        {
            long a = Prof ? TS() : 0, b;
            Tiles++;
            Transform tt = target.transform;
            _ = target.floorRenderer.material;   // 원래 코드도 읽는다 (floorRenderer 가 없으면 같은 자리에서 예외)
            var mt = target.moveTweens;
            Vector3 vector = target.startPos + c.Pos;
            float z = (target.startRot + c.Rot).z;
            Tween old;
            if (Prof) { b = TS(); pHead += b - a; a = b; }
            if (c.PosUsed)
            {
                if (!float.IsNaN(vector.x))
                {
                    old = Prev(mt, global::TweenType.PositionX, false);
                    if (!Mathf.Approximately(tt.position.x, vector.x))
                    {
                        if (c.Zero) ZeroPos(tt, mt, true, vector.x, ref c);
                        else if (RealOverlap(mt, global::TweenType.Position)) mt[global::TweenType.PositionX] = RealPosX(tt, vector.x, c.Dur, c.Ease);
                        else New(target, tt, mt, PX, global::TweenType.PositionX, old, ref c).FEnd = vector.x;
                    }
                }
                if (!float.IsNaN(vector.y))
                {
                    old = Prev(mt, global::TweenType.PositionY, false);
                    if (!Mathf.Approximately(tt.position.y, vector.y))
                    {
                        if (c.Zero) ZeroPos(tt, mt, false, vector.y, ref c);
                        else if (RealOverlap(mt, global::TweenType.Position)) mt[global::TweenType.PositionY] = RealPosY(tt, vector.y, c.Dur, c.Ease);
                        else New(target, tt, mt, PY, global::TweenType.PositionY, old, ref c).FEnd = vector.y;
                    }
                }
                if (Prof) { b = TS(); pPos += b - a; a = b; }
            }
            if (c.RotUsed)
            {
                old = Prev(mt, global::TweenType.Rotation, false);
                if (!Mathf.Approximately(tt.eulerAngles.z, z))
                {
                    if (c.Zero) ZeroRot(target, tt, mt, (target.startRot + c.Rot).z, ref c);
                    else New(target, tt, mt, ROT, global::TweenType.Rotation, old, ref c).FEnd = (target.startRot + c.Rot).z;
                }
                if (Prof) { b = TS(); pRot += b - a; a = b; }
            }
            if (c.ScaleUsed)
            {
                Vector3 localScale = tt.localScale;
                if (!float.IsNaN(c.Scale.x))
                {
                    old = Prev(mt, global::TweenType.ScaleX, true);   // 원래: GetValueOrDefault(..)?.Kill(true) (null 이면 안 부름)
                    Vector3 v2 = localScale.WithX(c.Scale.x);
                    if (!tt.localScale.ApproximatelyXY(v2))
                    {
                        if (c.Zero) ZeroScale(tt, mt, true, v2, ref c);
                        else if (RealOverlap(mt, global::TweenType.Scale)) mt[global::TweenType.ScaleX] = RealScale(tt, v2, c.Dur, c.Ease, AxisConstraint.X);
                        else New(target, tt, mt, SX, global::TweenType.ScaleX, old, ref c).VEnd = v2;
                    }
                }
                if (!float.IsNaN(c.Scale.y))
                {
                    old = Prev(mt, global::TweenType.ScaleY, true);
                    Vector3 v3 = localScale.WithY(c.Scale.y);
                    if (!tt.localScale.ApproximatelyXY(v3))
                    {
                        if (c.Zero) ZeroScale(tt, mt, false, v3, ref c);
                        else if (RealOverlap(mt, global::TweenType.Scale)) mt[global::TweenType.ScaleY] = RealScale(tt, v3, c.Dur, c.Ease, AxisConstraint.Y);
                        else New(target, tt, mt, SY, global::TweenType.ScaleY, old, ref c).VEnd = v3;
                    }
                }
                if (Prof) { b = TS(); pScale += b - a; a = b; }
            }
            if (c.OpaUsed)
            {
                old = Prev(mt, global::TweenType.Opacity, false);
                if (!Mathf.Approximately(target.opacity, c.Opacity))
                {
                    // TweenOpacity: 길이 > 0 이면 애니메이션을 만들고, 0 이면 opacity 에 바로 쓰고 애니메이션은 없다(사전에 안 넣음). 둘 다 dontChangeMySprite = true
                    if (c.Zero) target.opacity = c.Opacity;
                    else New(target, tt, mt, OPA, global::TweenType.Opacity, old, ref c).FEnd = c.Opacity;
                    target.dontChangeMySprite = true;
                }
                if (Prof) { b = TS(); pOpa += b - a; a = b; }
            }
            MaybeShadow();
        }

        // ── (저사양) 타일 이동 나눠 처리 ──
        // 타일이 많은 효과(1000개 넘게)는 지금 타일에서 가까운 것부터 프레임당 예산만큼 처리하고 나머지는 다음 프레임들로 미룬다.
        // 늦게 만든 애니메이션은 그동안 지난 시간만큼 앞으로 당겨 두어(Pos) 끝나는 순간은 원래와 같다. 먼 타일이 처음 1~몇 프레임 늦게 움직이는 것만 다르다.
        // 다른 타일 이동이 남은 타일을 건드리면 그 타일만 먼저(원래 순서대로) 만들고, 타일 나타나기·사라지기가 시작되거나
        // 게임이 애니메이션을 모아 끊거나 완료할 때는 남은 것을 그 자리에서 끝까지 만든다.
        internal static bool Split;
        internal static float SplitBudgetMs = 4f;
        internal const int SplitMin = 1000;
        internal static long SplitEffects, SplitDeferred, SplitFlushes;
        private sealed class Job
        {
            public Ctx C; public List<scrFloor> Left = new List<scrFloor>(); public int Next; public float Elapsed;
            public Dictionary<scrFloor, int> Pending = new Dictionary<scrFloor, int>(RefEq.I);   // 아직 안 만든 타일 (같은 타일이 두 번 들어갈 수 있다)
        }
        private sealed class RefEq : IEqualityComparer<scrFloor>
        {
            internal static readonly RefEq I = new RefEq();
            public bool Equals(scrFloor a, scrFloor b) { return ReferenceEquals(a, b); }
            public int GetHashCode(scrFloor o) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o); }
        }
        private static readonly List<Job> jobs = new List<Job>();
        private static readonly List<scrFloor> orderBuf = new List<scrFloor>();

        private static void RunSplit(List<scrFloor> floors, int start, int end, int step, Ctx c)
        {
            SplitEffects++;
            int cur = -1;
            try { var ctl = ADOBase.controller; if (ctl != null && ctl.currFloor != null) cur = ctl.currFloor.seqID; } catch { }
            // 원래 순서대로 대상 목록 (자유 이동 구역의 착지 가능한 타일 포함). 인덱스는 원래처럼 읽는다(같은 자리에서 예외).
            orderBuf.Clear();
            for (int i = start; i <= end; i += step)
            {
                scrFloor f = floors[i];
                orderBuf.Add(f);
                if (f.freeroamArea == null) continue;
                foreach (scrFloor lf in f.freeroamArea.listFloors)
                    if (lf.isLandable) orderBuf.Add(lf);
            }
            var job = new Job { C = c };
            // 지금 타일에서 가까운 것부터: 목록은 타일 번호 순이므로 지금 타일 자리에서 양쪽으로 펼친다 (정렬 없이 O(n))
            int n = orderBuf.Count, p = 0;
            if (cur >= 0) { while (p < n && orderBuf[p].seqID < cur) p++; }
            int lo = p - 1, hi = p;
            while (lo >= 0 || hi < n)
            {
                if (hi >= n || (lo >= 0 && cur - orderBuf[lo].seqID < orderBuf[hi].seqID - cur)) job.Left.Add(orderBuf[lo--]);
                else job.Left.Add(orderBuf[hi++]);
            }
            orderBuf.Clear();
            for (int i = 0; i < job.Left.Count; i++) { int k; job.Pending.TryGetValue(job.Left[i], out k); job.Pending[job.Left[i]] = k + 1; }
            jobs.Add(job);
            long deadline = TS() + (long)(SplitBudgetMs * System.Diagnostics.Stopwatch.Frequency / 1000.0);
            Work(job, deadline);
            if (job.Next >= job.Left.Count) jobs.Remove(job);
            else SplitDeferred += job.Pending.Count;
        }
        private static void Work(Job job, long deadline)
        {
            int done = 0;
            while (job.Next < job.Left.Count)
            {
                var f = job.Left[job.Next++];
                if (TakeOne(job, f)) { Before(job, f); var c = job.C; c.Start = job.Elapsed; TweenFloor(f, ref c); }
                if ((++done & 31) == 0 && deadline != long.MaxValue && TS() > deadline) break;
            }
        }
        private static bool TakeOne(Job job, scrFloor f)
        {
            int k;
            if (!job.Pending.TryGetValue(f, out k) || k <= 0) return false;
            if (k == 1) job.Pending.Remove(f); else job.Pending[f] = k - 1;
            return true;
        }
        // 타일 하나를 만들기 전에, 그보다 먼저 시작된 효과가 그 타일에 남겨 둔 것을 원래 순서대로 먼저 만든다
        private static void Before(Job self, scrFloor f)
        {
            for (int i = 0; i < jobs.Count; i++)
            {
                var j = jobs[i];
                if (j == self) return;   // 자기보다 앞선 것만
                int k;
                while (j.Pending.TryGetValue(f, out k) && k > 0)
                {
                    TakeOne(j, f);
                    var c = j.C; c.Start = j.Elapsed; TweenFloor(f, ref c);
                    SplitPulled++;
                }
            }
        }
        // 나누지 않는 효과가 타일을 건드리기 전에 (jobs 에 없는 효과)
        private static void BeforeAll(scrFloor f) { if (jobs.Count > 0) Before(null, f); }
        internal static long SplitPulled;
        // 남은 것을 모두 지금 만든다 (먼저 시작된 효과부터)
        internal static void Flush()
        {
            if (jobs.Count == 0) return;
            SplitFlushes++;
            var copy = jobs.ToArray();
            foreach (var j in copy) Work(j, long.MaxValue);
            jobs.Clear();
        }
        // DOTween 갱신 앞: 예산만큼 이어서 만든다 (먼저 시작된 효과부터. 만든 것은 같은 갱신에서 바로 진행된다)
        private static void SplitTick()
        {
            long deadline = TS() + (long)(SplitBudgetMs * System.Diagnostics.Stopwatch.Frequency / 1000.0);
            for (int i = 0; i < jobs.Count; i++)
            {
                Work(jobs[i], deadline);
                if (TS() > deadline) break;
            }
            for (int i = jobs.Count - 1; i >= 0; i--) if (jobs[i].Pending.Count == 0) jobs.RemoveAt(i);
        }
        public static void FlushPrefix() { if (jobs.Count > 0) Flush(); }

        // ── 길이 0 (즉시 이동) ──
        // 원래: DOTween.To(...).SetEase(ease).Done() -> Done 이 길이 0 이면 그 자리에서 Complete:
        //   시작값 = getter(), 변화량 = 목표 - 시작 (float 로 저장), setter(시작 + 변화량 x 이징 끝점), (회전은 OnUpdate). 사전에는 죽은 애니메이션이 남는다.
        // 같은 계산은 ZeroTween 이 장식 26만 개로 DOTween 과 비트 단위로 맞춰 둔 것이다. 사전에는 꺼진 표 하나(모두 같은 것)를 넣는다:
        // 꺼진 애니메이션은 Kill·Goto·Play 모두 아무 일도 안 한다(원래 남는 죽은 애니메이션과 같음).
        private static bool ZeroOk { get { return ZeroTween.CanEase; } }
        internal static long ZeroEffects, ZeroWrites, ZeroChecked, ZeroMismatch;
        private static Tween dead;
        private static Tween Dead() { if ((object)dead == null) { dead = NewProxy(); } return dead; }
        private static float Calc(float s, float e, float k) { float ch = e - s; float m = ch * k; return s + m; }
        private static long zeroCounter;
        private static bool ZeroSample() { return Edition.Dev && (++zeroCounter & 63) == 0; }

        private static void ZeroPos(Transform tt, Dictionary<global::TweenType, Tween> mt, bool isX, float to, ref Ctx c)
        {
            ZeroWrites++;
            var p = tt.position;
            float v = Calc(isX ? p.x : p.y, to, c.K);
            if (ZeroSample())
            {
                // 원래 코드로 처리하고 결과를 비교
                var real = isX ? DOTween.To(() => tt.position.x, x => tt.MoveX(x), to, c.Dur).SetEase(c.Ease).Done()
                               : DOTween.To(() => tt.position.y, y => tt.MoveY(y), to, c.Dur).SetEase(c.Ease).Done();
                mt[isX ? global::TweenType.PositionX : global::TweenType.PositionY] = real;
                ZeroCheck(isX ? "위치X" : "위치Y", v, isX ? tt.position.x : tt.position.y);
                return;
            }
            tt.position = isX ? new Vector3(v, p.y, p.z) : new Vector3(p.x, v, p.z);
            mt[isX ? global::TweenType.PositionX : global::TweenType.PositionY] = Dead();
        }
        private static void ZeroRot(scrFloor f, Transform tt, Dictionary<global::TweenType, Tween> mt, float to, ref Ctx c)
        {
            ZeroWrites++;
            float v = Calc(f.tweenRot.z, to, c.K);
            if (ZeroSample())
            {
                var real = DOTween.To(() => f.tweenRot.z, r => { f.tweenRot.z = r; }, to, c.Dur).SetEase(c.Ease).OnUpdate(() => { tt.eulerAngles = f.tweenRot; }).Done();
                mt[global::TweenType.Rotation] = real;
                ZeroCheck("회전", v, f.tweenRot.z);
                return;
            }
            f.tweenRot.z = v;
            tt.eulerAngles = f.tweenRot;
            mt[global::TweenType.Rotation] = Dead();
        }
        private static void ZeroScale(Transform tt, Dictionary<global::TweenType, Tween> mt, bool isX, Vector3 to, ref Ctx c)
        {
            ZeroWrites++;
            var s0 = tt.localScale;   // 시작값 = getter()
            var res = s0;             // EvaluateAndApply 의 getter() (같은 값)
            if (isX) res.x = Calc(s0.x, to.x, c.K); else res.y = Calc(s0.y, to.y, c.K);
            if (ZeroSample())
            {
                var real = tt.DOScale(to, c.Dur).SetEase(c.Ease).SetOptions(isX ? AxisConstraint.X : AxisConstraint.Y).Done();
                mt[isX ? global::TweenType.ScaleX : global::TweenType.ScaleY] = real;
                var now = tt.localScale;
                ZeroCheck(isX ? "크기X" : "크기Y", isX ? res.x : res.y, isX ? now.x : now.y);
                return;
            }
            tt.localScale = res;
            mt[isX ? global::TweenType.ScaleX : global::TweenType.ScaleY] = Dead();
        }
        private static void ZeroCheck(string what, float mine, float real)
        {
            ZeroChecked++;
            if (InstantMove.Bits(mine) != InstantMove.Bits(real))
            {
                ZeroMismatch++;
                if (First.Length < 600) First += " [즉시 " + what + " " + mine.ToString("R") + " / " + real.ToString("R") + "]";
            }
        }

        // 같은 타일에 진짜 DOTween 애니메이션(타일 나타나기·사라지기의 위치/크기)이 아직 돌고 있는가
        private static bool RealOverlap(Dictionary<global::TweenType, Tween> mt, global::TweenType key)
        {
            Tween t;
            return mt.TryGetValue(key, out t) && (object)t != null && t.active && !(t.id is Rec);
        }
        // 원래 코드와 같은 호출 (겹치는 경우만)
        private static Tween RealPosX(Transform t, float to, float dur, Ease e) { Real++; return DOTween.To(() => t.position.x, x => t.MoveX(x), to, dur).SetEase(e).Done(); }
        private static Tween RealPosY(Transform t, float to, float dur, Ease e) { Real++; return DOTween.To(() => t.position.y, y => t.MoveY(y), to, dur).SetEase(e).Done(); }
        private static Tween RealScale(Transform t, Vector3 to, float dur, Ease e, AxisConstraint ax) { Real++; return t.DOScale(to, dur).SetEase(e).SetOptions(ax).Done(); }

        private static Rec lastNew;
        // 꺼진 DOTween 객체(모드 표). AccessTools.CreateInstance 는 부를 때마다 생성자를 리플렉션으로 찾아 하나 약 2us 였다 (효과 하나에 7천 개).
        private static Func<Tween> factory;
        internal static Tween NewProxy()
        {
            if (factory == null)
            {
                try
                {
                    var ctor = typeof(TweenerCore<float, float, FloatOptions>).GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                    var dm = new System.Reflection.Emit.DynamicMethod("StutterFix_NewFloorProxy", typeof(Tween), Type.EmptyTypes, typeof(FloorAnim).Module, true);
                    var il = dm.GetILGenerator();
                    il.Emit(System.Reflection.Emit.OpCodes.Newobj, ctor);
                    il.Emit(System.Reflection.Emit.OpCodes.Ret);
                    factory = (Func<Tween>)dm.CreateDelegate(typeof(Func<Tween>));
                }
                catch { factory = () => AccessTools.CreateInstance<TweenerCore<float, float, FloatOptions>>(); }
            }
            return factory();
        }
        private static Rec New(scrFloor f, Transform tt, Dictionary<global::TweenType, Tween> mt, int key, global::TweenType tk, Tween old, ref Ctx c)
        {
            // 같은 칸의 끝난 표를 다시 쓴다 (방금 Kill(true) 로 끝냈거나 이미 끝난 것). 기록이 목록에서 빠졌으면 기록도 다시 쓴다.
            Rec oldR = (object)old == null ? null : old.id as Rec; Rec r;
            bool reuseProxy = oldR != null && !oldR.Running && (!Edition.Dev || oldR.Shadow == null);
            if (reuseProxy && !oldR.InList && ReferenceEquals(oldR.F, f) && oldR.Key == key)
            {
                r = oldR; Reused++;
                r.Pos = 0f; r.Started = false; r.Running = true; r.Stepped = false;
                r.Shadow = null; r.SDone = false; r.SStepped = false;
            }
            else r = new Rec { F = f, T = tt, Key = key };
            r.Pos = c.Start;
            r.Dur = c.Dur; r.E = c.Ease; r.Over = c.Over; r.Period = c.Period;
            if (reuseProxy) r.Proxy = old;
            else
            {
                r.Proxy = NewProxy();
                if (Prof) pNewObj++;
            }
            r.Proxy.id = r;
            mt[tk] = r.Proxy;
            recs.Add(r); r.InList = true; Created++;
            if (recs.Count > Peak) Peak = recs.Count;
            lastNew = r;
            return r;
        }
        private static global::TweenType TT(int key)
        {
            switch (key)
            {
                case PX: return global::TweenType.PositionX;
                case PY: return global::TweenType.PositionY;
                case ROT: return global::TweenType.Rotation;
                case SX: return global::TweenType.ScaleX;
                case SY: return global::TweenType.ScaleY;
                default: return global::TweenType.Opacity;
            }
        }

        // ── 개발자용 짝 ──
        private static long sampleCounter;
        private static void MaybeShadow()
        {
            var r = lastNew; lastNew = null;
            if (r == null || !Edition.Dev || r.Pos != 0f || (++sampleCounter & 63) != 0) return;
            switch (r.Key)
            {
                case PX: r.Shadow = DOTween.To(() => r.Started ? r.FStart : r.T.position.x, v => { r.SF = v; r.SStepped = true; }, r.FEnd, r.Dur).SetEase(r.E).OnComplete(() => r.SDone = true); break;
                case PY: r.Shadow = DOTween.To(() => r.Started ? r.FStart : r.T.position.y, v => { r.SF = v; r.SStepped = true; }, r.FEnd, r.Dur).SetEase(r.E).OnComplete(() => r.SDone = true); break;
                case ROT: r.Shadow = DOTween.To(() => r.Started ? r.FStart : r.F.tweenRot.z, v => { r.SF = v; r.SStepped = true; }, r.FEnd, r.Dur).SetEase(r.E).OnComplete(() => r.SDone = true); break;
                case OPA: r.Shadow = DOTween.To(() => r.Started ? r.FStart : r.F.opacity, v => { r.SF = v; r.SStepped = true; }, r.FEnd, r.Dur).SetEase(r.E).OnComplete(() => r.SDone = true); break;
                case SX: case SY:
                    r.Shadow = DOTween.To(() => r.Started ? r.VStart : r.T.localScale, v => { r.SV = v; r.SStepped = true; }, r.VEnd, r.Dur).SetEase(r.E)
                        .SetOptions(r.Key == SX ? AxisConstraint.X : AxisConstraint.Y).OnComplete(() => r.SDone = true);
                    break;
            }
        }

        // ── 진행 ──
        private static void Startup(Rec r)
        {
            r.Started = true;
            switch (r.Key)
            {
                case PX: r.FStart = r.T.position.x; break;
                case PY: r.FStart = r.T.position.y; break;
                case ROT: r.FStart = r.F.tweenRot.z; break;
                case OPA: r.FStart = r.F.opacity; break;
                case SX: case SY: r.VStart = r.T.localScale; r.VChange = r.VEnd - r.VStart; return;
            }
            r.FChange = r.FEnd - r.FStart;
        }
        // setter (+ 회전은 OnUpdate). DOTween FloatPlugin: 시작 + 변화 x 이징 / Vector3Plugin 축 제한: getter() 의 그 축만 바꿔 setter
        private static void Apply(Rec r, float pos)
        {
            float e = ZeroTween.Eval(r.E, pos, r.Dur, r.Over, r.Period);
            switch (r.Key)
            {
                case PX: { float v = r.FStart + r.FChange * e; r.FLast = v; var p = r.T.position; r.T.position = new Vector3(v, p.y, p.z); break; }
                case PY: { float v = r.FStart + r.FChange * e; r.FLast = v; var p = r.T.position; r.T.position = new Vector3(p.x, v, p.z); break; }
                case ROT: { float v = r.FStart + r.FChange * e; r.FLast = v; r.F.tweenRot.z = v; r.T.eulerAngles = r.F.tweenRot; break; }
                case OPA: { float v = r.FStart + r.FChange * e; r.FLast = v; r.F.opacity = v; break; }
                case SX: { var s = r.T.localScale; s.x = r.VStart.x + r.VChange.x * e; r.VLast = s; r.T.localScale = s; break; }
                case SY: { var s = r.T.localScale; s.y = r.VStart.y + r.VChange.y * e; r.VLast = s; r.T.localScale = s; break; }
            }
        }
        private static void Step(Rec r, float td)
        {
            if (!r.Started) Startup(r);
            float to = r.Pos + td;
            bool done = false;
            if (to >= r.Dur) { to = r.Dur; done = true; }
            r.Pos = to;
            r.Stepped = true;
            Apply(r, to);
            if (done) { r.Running = false; Completed++; }
        }
        private static void Complete(Rec r)
        {
            if (!r.Running) return;
            if (!r.Started) Startup(r);
            r.Pos = r.Dur;
            r.Running = false;
            r.Stepped = true;
            try { Apply(r, r.Dur); }
            catch (Exception ex) { Error(ex); }
            if (Edition.Dev && r.Shadow != null && r.Shadow.active) { try { r.Shadow.Complete(true); } catch { } }
        }

        public static bool KillPrefix(Tween t, bool complete)
        {
            if ((object)t == null) return true;
            var r = t.id as Rec;
            if (r == null) return true;
            if (complete) { if (r.Running) Killed++; Complete(r); }
            else if (r.Running) { r.Running = false; Dropped++; if (r.Shadow != null) r.Shadow.Kill(false); }
            return false;
        }
        public static bool CompletePrefix(Tween t)
        {
            if ((object)t == null) return true;
            var r = t.id as Rec;
            if (r == null) return true;
            if (r.Running) { Killed++; Complete(r); }
            return false;
        }
        public static void PlayingPrefix() { if (jobs.Count > 0) Flush(); }
        public static void PlayingPostfix(List<Tween> __0, ref List<Tween> __result)
        {
            int n = 0;
            for (int i = 0; i < recs.Count; i++) if (recs[i].Running) n++;
            if (n == 0) return;
            if (__0 != null) AddRunning(__0);
            if (__result == null) __result = __0 ?? AddRunning(new List<Tween>(n));
            else if (!ReferenceEquals(__result, __0)) AddRunning(__result);
            Listed += n;
        }
        private static List<Tween> AddRunning(List<Tween> list)
        {
            for (int i = 0; i < recs.Count; i++) if (recs[i].Running) list.Add(recs[i].Proxy);
            return list;
        }
        public static void KillAllPrefix(bool complete)
        {
            if (jobs.Count > 0) Flush();
            if (recs.Count == 0) return;
            var copy = recs.ToArray();
            foreach (var r in copy)
            {
                if (complete) Complete(r);
                else if (r.Running) { r.Running = false; Dropped++; }
                r.InList = false;
            }
            recs.Clear();
        }
        internal static void DropAll() { jobs.Clear(); KillAllPrefix(false); }
        internal static void FinishAll() { KillAllPrefix(true); }

        public static void UpdatePrefix()
        {
            if (jobs.Count > 0) SplitTick();
            if (recs.Count == 0) { LastFrameMs = 0; AddElapsed(); return; }
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            float dt = (DOTween.useSmoothDeltaTime ? Time.smoothDeltaTime : Time.deltaTime) * DOTween.timeScale;
            float td = dt * 1f;
            Frames++;
            AddElapsed();
            if (!(td < 1E-06f && td > -1E-06f))
            {
                for (int i = 0; i < recs.Count; i++)
                {
                    var r = recs[i];
                    if (!r.Running) continue;
                    Steps++;
                    try { Step(r, td); }
                    catch (Exception ex) { r.Running = false; Error(ex); }
                }
            }
            if (!Edition.Dev) Compact();
            LastFrameMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            UpdateMs += LastFrameMs;
        }

        // 남은 타일의 애니메이션이 이번 갱신까지 지났어야 할 시간 (DOTween 과 같은 dt, 너무 작으면 진행 안 함)
        private static void AddElapsed()
        {
            if (jobs.Count == 0) return;
            float td = (DOTween.useSmoothDeltaTime ? Time.smoothDeltaTime : Time.deltaTime) * DOTween.timeScale;
            if (td < 1E-06f && td > -1E-06f) return;
            for (int i = 0; i < jobs.Count; i++) jobs[i].Elapsed += td;
        }

        public static void UpdatePostfix()
        {
            if (!Edition.Dev || recs.Count == 0) return;
            for (int i = 0; i < recs.Count; i++)
            {
                var r = recs[i];
                if (r.Shadow == null) { r.Stepped = false; continue; }
                if (r.Stepped || r.SStepped)
                {
                    VerifySteps++;
                    string diff = null;
                    if (r.Stepped != r.SStepped) diff = "진행한 프레임이 다름 (모드 " + r.Stepped + ", DOTween " + r.SStepped + ")";
                    else if (r.Key == SX) { if (B(r.VLast.x) != B(r.SV.x)) diff = "크기X " + r.VLast.x.ToString("R") + " / " + r.SV.x.ToString("R"); }
                    else if (r.Key == SY) { if (B(r.VLast.y) != B(r.SV.y)) diff = "크기Y " + r.VLast.y.ToString("R") + " / " + r.SV.y.ToString("R"); }
                    else if (B(r.FLast) != B(r.SF)) diff = "키 " + r.Key + " " + r.FLast.ToString("R") + " / " + r.SF.ToString("R");
                    if (diff == null && (!r.Running) != r.SDone) diff = "끝난 프레임이 다름 (모드 " + (!r.Running) + ", DOTween " + r.SDone + ")";
                    if (diff != null) { VerifyMismatch++; if (First.Length < 600) First += " [" + diff + ", 위치 " + r.Pos.ToString("R") + "/" + r.Dur.ToString("R") + "]"; }
                }
                r.Stepped = false; r.SStepped = false;
                if (!r.Running) { VerifyN++; if (r.Shadow.active) r.Shadow.Kill(false); r.Shadow = null; }
            }
            Compact();
        }
        private static int B(float f) { return InstantMove.Bits(f); }

        private static void Compact()
        {
            int w = 0;
            for (int i = 0; i < recs.Count; i++)
            {
                var r = recs[i];
                if (r.Running || (Edition.Dev && r.Shadow != null)) recs[w++] = r;
                else r.InList = false;
            }
            if (w < recs.Count) recs.RemoveRange(w, recs.Count - w);
        }

        private static void Error(Exception ex)
        {
            Errors++;
            if (First.Length < 600) First += " [예외: " + ex.GetType().Name + " " + ex.Message + "]";
        }

        internal static string Summary()
        {
            if (Effects == 0 && Fallbacks == 0) return "";
            string s = string.Format(" | 타일 이동 애니메이션 직접 처리: 효과 {0}개(타일 {1}개, 시작에 쓴 시간 {2:F0}ms), 원래 코드로 {3}개 [곡 시작 직후 {4}, 길이 음수 {5}, 기타 {6}], 만든 것 {7}개(동시 최대 {8}, 다시 씀 {9}, 겹쳐서 진짜 DOTween {10}), 끝까지 감 {11}, 끊겨서 완료 {12}, 버림 {13}, 갱신 {14:F0}ms ({15}프레임), 게임이 멈출 때 넘겨준 것 {16}{17}",
                Effects, Tiles, StartMs, Fallbacks, whyNot[0], whyNot[1], whyNot[2], Created, Peak, Reused, Real, Completed, Killed, Dropped, UpdateMs, Frames, Listed, Errors > 0 ? ", 예외 " + Errors : "");
            if (ZeroEffects > 0) s += string.Format(", 길이 0 효과 {0}개(바로 쓴 값 {1}개)", ZeroEffects, ZeroWrites);
            if (SplitEffects > 0) s += string.Format(", (저사양) 나눠 처리한 효과 {0}개(미룬 타일 {1}개, 다른 효과가 건드려 먼저 만든 것 {3}개, 남은 것을 한꺼번에 끝냄 {2}번)", SplitEffects, SplitDeferred, SplitFlushes, SplitPulled);
            if (Edition.Dev) s += " (검증: 진짜 DOTween 과 나란히 " + VerifyN + "개, 프레임 " + VerifySteps + "번 중 다름 " + VerifyMismatch + ", 길이 0 은 원래 코드 결과와 " + ZeroChecked + "번 비교 중 다름 " + ZeroMismatch + First + ")";
            else if (First.Length > 0) s += First;
            return s;
        }
        internal static void ResetStats() { Effects = Fallbacks = Tiles = Created = Completed = Killed = Dropped = Frames = Steps = Reused = Real = VerifyN = VerifySteps = VerifyMismatch = Errors = Listed = 0; ZeroEffects = ZeroWrites = ZeroChecked = ZeroMismatch = 0; SplitEffects = SplitDeferred = SplitFlushes = SplitPulled = 0; Peak = 0; UpdateMs = StartMs = 0; First = ""; Array.Clear(whyNot, 0, whyNot.Length); }
    }
}
