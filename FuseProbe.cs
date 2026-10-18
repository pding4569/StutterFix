using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // (자동 시험 fuse begin / fuse end / decostruct) Arche 90 FPS 구간 연구용.
    //   fuse begin: 장식 관리자 LateUpdate 시간, 장식 이동 효과 시작 수·시간, 애니메이션 기록 새로 만들기(같은 장식·같은 속성을 다시 시작한 것, 끝나기 전에 끊은 것)를 세기 시작
    //   fuse end: 프레임당 평균으로 보고
    //   decostruct: 지금 애니메이션이 도는 장식들의 갈래(따라가기·타일 붙음·화면 고정·회전 고정·시차 배율·pivotTrans 가 자기 Transform 인지 등)
    internal static class FuseProbe
    {
        internal static bool On;
        private static bool installed;
        private static long t0Late, lateTicks, fxTicks, fxT0; private static int fxDepth;
        private static long frames0, fxStarts, recNew, recRestart, recCut, animTicks0;
        private static int frameAt;
        private static double animMs0;
        private static readonly Dictionary<scrDecoration, int> perDeco = new Dictionary<scrDecoration, int>();
        private static readonly int[] durHist = new int[8];   // 0~0.05 ~0.1 ~0.2 ~0.5 ~1 ~2 ~5 그 이상 (초, 곡 시간)

        private static void Install()
        {
            if (installed) return;
            installed = true;
            var h = new Harmony("StutterFix.fuseprobe");
            var late = AccessTools.Method(typeof(scrDecorationManager), "LateUpdate");
            h.Patch(late, prefix: new HarmonyMethod(typeof(FuseProbe), nameof(LatePre)) { priority = Priority.First }, postfix: new HarmonyMethod(typeof(FuseProbe), nameof(LatePost)) { priority = Priority.Last });
            foreach (var m in typeof(ffxMoveDecorationsPlus).GetMethods(AccessTools.all))
                if (m.Name == "StartEffect" && m.DeclaringType == typeof(ffxMoveDecorationsPlus) && !m.IsAbstract)
                    h.Patch(m, prefix: new HarmonyMethod(typeof(FuseProbe), nameof(FxPre)) { priority = Priority.First }, finalizer: new HarmonyMethod(typeof(FuseProbe), nameof(FxFin)));
        }
        public static void LatePre() { if (On) t0Late = Stopwatch.GetTimestamp(); }
        public static void LatePost() { if (On) lateTicks += Stopwatch.GetTimestamp() - t0Late; }
        public static void FxPre() { if (!On) return; if (fxDepth++ == 0) fxT0 = Stopwatch.GetTimestamp(); fxStarts++; }
        public static void FxFin() { if (!On || fxDepth == 0) return; if (--fxDepth == 0) fxTicks += Stopwatch.GetTimestamp() - fxT0; }

        // DecoAnim.NewRec 에서: wasRunning = 같은 칸의 이전 기록이 아직 돌고 있었음(끊고 다시 시작)
        internal static void NewRec(scrDecoration d, int key, float dur, bool hadPrev, bool wasRunning)
        {
            recNew++;
            if (hadPrev) recRestart++;
            if (wasRunning) recCut++;
            int c; perDeco.TryGetValue(d, out c); perDeco[d] = c + 1;
            int b = dur < 0.05f ? 0 : dur < 0.1f ? 1 : dur < 0.2f ? 2 : dur < 0.5f ? 3 : dur < 1f ? 4 : dur < 2f ? 5 : dur < 5f ? 6 : 7;
            durHist[b]++;
        }

        internal static string Begin()
        {
            Install();
            On = true; lateTicks = fxTicks = 0; fxStarts = recNew = recRestart = recCut = 0; perDeco.Clear(); Array.Clear(durHist, 0, durHist.Length);
            frameAt = Time.frameCount; animMs0 = DecoAnim.UpdateMs;
            return "세기 시작 (곡 " + SongTime() + "초)";
        }
        private static string SongTime() { var c = scrConductor.instance; return c == null ? "?" : c.songposition_minusi.ToString("F1"); }

        internal static string End()
        {
            On = false;
            int f = Math.Max(1, Time.frameCount - frameAt);
            double tk = Stopwatch.Frequency / 1000.0;
            int multi = 0, maxPer = 0; foreach (var kv in perDeco) { if (kv.Value > 6) multi++; if (kv.Value > maxPer) maxPer = kv.Value; }
            return string.Format("곡 {0}초까지 {1}프레임 | 프레임당: 장식 관리자 LateUpdate {2:F2}ms, 장식 이동 효과 시작 {3:F2}개 {4:F2}ms, 모드 애니메이션 갱신 {5:F2}ms | 애니메이션 기록 {6}개 (프레임당 {7:F1}) 중 같은 장식·속성 다시 시작 {8}, 끝나기 전에 끊고 다시 {9} | 장식 {10}개가 받음(7개 넘게 받은 장식 {11}, 가장 많이 {12}) | 길이 <0.05 {13} <0.1 {14} <0.2 {15} <0.5 {16} <1 {17} <2 {18} <5 {19} 그 이상 {20}",
                SongTime(), f, lateTicks / tk / f, (double)fxStarts / f, fxTicks / tk / f, (DecoAnim.UpdateMs - animMs0) / f,
                recNew, (double)recNew / f, recRestart, recCut, perDeco.Count, multi, maxPer,
                durHist[0], durHist[1], durHist[2], durHist[3], durHist[4], durHist[5], durHist[6], durHist[7]);
        }

        private static FieldInfo F(Type t, string n) { return AccessTools.Field(t, n); }
        internal static string Struct()
        {
            var decs = DecoAnim.RunningDecorations();
            var tD = typeof(scrDecoration); var tP = typeof(scrParallax);
            FieldInfo fPar = F(tD, "parallax"), fFollow = F(tD, "followPlanet"), fStick = F(tD, "stickToFloor"), fLock = F(tD, "lockRotation"), fPiv = F(tD, "pivotTrans"), fChild = F(tD, "childTransform"),
                fClamp = F(tP, "clampToScreen"), fAx = F(tP, "dontAlterX"), fAy = F(tP, "dontAlterY"), fMul = F(tP, "multiplier"), fMx = F(tP, "multiplier_x"), fMy = F(tP, "multiplier_y");
            int n = 0, pivSelf = 0, noPar = 0, parOff = 0, clamp = 0, alter = 0, follow = 0, stick = 0, lockR = 0, mul0 = 0, mul1 = 0, mulOther = 0, parentId = 0, visible = 0, hitbox = 0, vis = 0, img = 0;
            foreach (var d in decs)
            {
                if (d == null) continue;
                n++;
                if (d is scrVisualDecoration) img++;
                if (d.GetVisible()) vis++;
                if (d.hitbox != 0) hitbox++;
                var par = fPar.GetValue(d) as scrParallax;
                var piv = fPiv.GetValue(d) as Transform;
                if (par == null) { noPar++; continue; }
                if (piv != null && ReferenceEquals(piv, par.transform)) pivSelf++;
                if (!par.enabled) parOff++;
                if (fClamp != null && (bool)fClamp.GetValue(par)) clamp++;
                if ((fAx != null && (bool)fAx.GetValue(par)) || (fAy != null && (bool)fAy.GetValue(par))) alter++;
                if (fFollow != null && fFollow.GetValue(d) as UnityEngine.Object != null) follow++;
                if (fStick != null && (bool)fStick.GetValue(d)) stick++;
                if (fLock != null && (bool)fLock.GetValue(d)) lockR++;
                float mx = fMx != null ? (float)fMx.GetValue(par) : float.NaN, my = fMy != null ? (float)fMy.GetValue(par) : float.NaN;
                if (fMul != null && fMx == null) { var v = (Vector2)fMul.GetValue(par); mx = v.x; my = v.y; }
                if (mx == 0f && my == 0f) mul0++; else if (mx == 1f && my == 1f) mul1++; else mulOther++;
                var p = par.transform.parent;
                if (p == null || (p.position == Vector3.zero && p.rotation == Quaternion.identity && p.lossyScale == Vector3.one)) parentId++;
            }
            var keys = DecoAnim.KeyCombos();
            return "애니메이션 도는 장식 " + n + "개(이미지 " + img + ", 보임 " + vis + ", 히트박스 " + hitbox + ") | pivotTrans=자기 Transform " + pivSelf + ", 시차 없음 " + noPar + ", 시차 꺼짐 " + parOff
                + ", 화면 고정 " + clamp + ", 축 고정 " + alter + ", 행성 따라감 " + follow + ", 타일에 붙음 " + stick + ", 회전 카메라 고정 " + lockR
                + " | 시차 배율 0 " + mul0 + " / 1 " + mul1 + " / 그 밖 " + mulOther + " | 부모가 원점·회전 없음·크기 1 " + parentId + " | 속성 조합 " + keys;
        }
    }
}
