using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // 장식 떼기: GPU 장식이 켜져 있는 동안 장식 루트를 장식 관리자에서 떼어 "하나의 거대한 Transform 계층"을 없앤다.
    //
    // 게임은 장식 전부(Arche 28,835개, Transform 약 14만)를 scrDecorationManager.transform 밑에 둔다. 유니티는 한 계층 안에서 하나라도
    // 바뀌면 그 계층 전체를 훑는다(TransformChangeDispatch::ScanAndHandleChangesJob). GPU 장식이 대부분을 맡아 CPU 에서는 안 움직여도,
    // 남은 몇 개가 움직일 때마다 14만 개를 매 프레임 훑었다.
    // 측정(2026-11-11, Arche 262~274초, 2560x1417, GPU 장식 켬, ABBA): 메인 스레드 3.49/3.37 -> 3.21/3.18ms, UpdateAllRenderers 0.20 -> 0.09ms.
    // 원본 방식(GPU 장식 끔)에서는 손해다(Arche 10.0 -> 11.3ms: 매 프레임 2만 8천 계층이 다 바뀜). 그래서 GPU 장식이 절반 넘게 맡았을 때만 뗀다.
    // "움직인 것만 떼기"는 효과가 없었다(0.20 -> 0.17ms).
    //
    // 관리자는 원점·회전 0·크기 1 이라(GPU 장식이 켜지는 조건) 로컬 값을 그대로 두는 SetParent(.., false)가 월드 값을 정확히 같게 둔다
    // (worldPositionStays=true 는 행렬을 다시 분해해 회전·크기가 큰 장식의 lossyScale 이 1e-5 쯤 달라졌다: 120배 크기 90도 장식에서 0.004). 한 번에 떼면 160~170ms 라 프레임마다 조금씩 뗀다.
    // 부모 관계에 기대는 곳: scrDecorationManager.ClearDecorations(자식을 훑어 지움) -> 그 직전에 전부 되붙인다.
    // GPU 장식이 꺼지면(곡 끝·죽음·CPU 복귀) 되붙이기를 시작하고 프레임마다 조금씩 끝낸다. 원래 형제 순서를 지킨다.
    internal static class DecoDetach
    {
        internal static bool Enabled;
        internal static double BudgetMs = 0.5;        // 곡 중 프레임당 떼기/되붙이기 시간
        private static Transform parent;
        private static Transform[] kids = new Transform[0];
        private static bool[] off = new bool[0];
        private static int n, pos, rank, offCount, waitFrames;
        private static int mode;                       // 0 다 붙어 있음, 1 떼는 중, 2 다 뗌, 3 되붙이는 중
        internal static long DetachCalls, ReattachCalls;
        internal static string State() { return "enabled=" + Enabled + " mode=" + mode + " children=" + n + " detached=" + offCount + " pos=" + pos + " detach_calls=" + DetachCalls + " reattach_calls=" + ReattachCalls; }

        internal static void Install(Harmony h)
        {
            var m = AccessTools.Method(typeof(scrDecorationManager), "ClearDecorations");
            if (m != null) h.Patch(m, prefix: new HarmonyMethod(typeof(DecoDetach), nameof(ClearPrefix)));
        }
        private static void ClearPrefix() { FinishReattach(); }

        // GPU 장식이 켜져 있는 프레임마다
        internal static void ActiveTick(Transform p, int owned, int total)
        {
            if (!Enabled || (object)p == null) { if (offCount > 0 && mode != 3) BeginReattach(); return; }
            if (mode == 2 && ReferenceEquals(parent, p)) return;
            if (mode == 0 || !ReferenceEquals(parent, p))
            {
                if (offCount > 0) FinishReattach();
                if (owned * 2 < total) return;                 // 절반도 못 맡았으면 떼지 않는다(원본 방식에서는 손해)
                if (++waitFrames < 30) return;                 // 켜진 직후의 무거운 프레임을 피한다
                Capture(p); mode = 1; pos = 0;
            }
            else if (mode == 3) { mode = 1; pos = 0; }         // 되붙이던 중에 다시 켜짐
            long t0 = Stopwatch.GetTimestamp(), limit = (long)(BudgetMs * Stopwatch.Frequency / 1000.0);
            while (pos < n)
            {
                if (!off[pos]) { var k = kids[pos]; if (k) { k.SetParent(null, false); off[pos] = true; offCount++; DetachCalls++; } }
                pos++;
                if ((pos & 15) == 0 && Stopwatch.GetTimestamp() - t0 > limit) break;
            }
            if (pos >= n) mode = 2;
        }
        private static void Capture(Transform p)
        {
            parent = p; n = p.childCount;
            if (kids.Length < n) { kids = new Transform[n]; off = new bool[n]; }
            for (int i = 0; i < n; i++) { kids[i] = p.GetChild(i); off[i] = false; }
            offCount = 0; waitFrames = 0;
        }
        // GPU 장식이 꺼질 때
        internal static void BeginReattach() { waitFrames = 0; if (offCount > 0) { mode = 3; pos = 0; rank = 0; } else if (mode != 0) Release(); }
        // 매 프레임(모드 갱신에서): 되붙이는 중이면 조금씩
        internal static void Pump()
        {
            if (mode != 3) return;
            StepReattach(Hitch.Playing ? BudgetMs : 4.0);
        }
        internal static void FinishReattach() { if (offCount > 0) { if (mode != 3) { mode = 3; pos = 0; rank = 0; } StepReattach(double.MaxValue); } else if (mode != 0) Release(); }
        private static void StepReattach(double budgetMs)
        {
            long t0 = Stopwatch.GetTimestamp(), limit = budgetMs >= 1e9 ? long.MaxValue : (long)(budgetMs * Stopwatch.Frequency / 1000.0);
            bool parentAlive = parent;
            // 낮은 번호부터: 이 장식을 붙일 때 앞 번호는 전부 붙어 있으므로 형제 자리 = 앞에 살아 있는 원래 장식 수
            while (pos < n)
            {
                var k = kids[pos];
                if (k)
                {
                    if (off[pos]) { if (parentAlive) { k.SetParent(parent, false); k.SetSiblingIndex(rank); ReattachCalls++; } off[pos] = false; offCount--; }
                    rank++;
                }
                else if (off[pos]) { off[pos] = false; offCount--; }
                pos++;
                if ((pos & 15) == 0 && Stopwatch.GetTimestamp() - t0 > limit) return;
            }
            Release();
        }
        private static void Release()
        {
            for (int i = 0; i < n; i++) kids[i] = null;
            mode = 0; n = 0; pos = 0; rank = 0; offCount = 0; parent = null; waitFrames = 0;
        }

#if DEV || AUTOTEST
        // 검증: 모든 장식 루트의 월드 위치·회전·크기와 형제 순서를 찍어 두고(verify a) 나중에(verify b) 비교한다.
        private struct Snap { public Transform T; public Vector3 P, S; public Quaternion R; public int Index; }
        private static readonly List<Snap> snaps = new List<Snap>();
        internal static string Command(string arg)
        {
            var a = arg.Trim().Split(' ');
            var dm = scrDecorationManager.instance;
            if (a[0] == "state") return State();
            if (a[0] == "on") { Enabled = true; return State(); }
            if (a[0] == "off") { Enabled = false; FinishReattach(); return State(); }
            if (a[0] == "finish") { FinishReattach(); return State(); }
            if (a[0] == "budget" && a.Length > 1) { BudgetMs = double.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture); return State(); }
            if (a[0] == "verify" && a.Length > 1 && dm)
            {
                var p = dm.transform;
                if (a[1] == "a")
                {
                    snaps.Clear();
                    for (int i = 0; i < p.childCount; i++) { var t = p.GetChild(i); snaps.Add(new Snap { T = t, P = t.position, R = t.rotation, S = t.lossyScale, Index = i }); }
                    return "verify a: " + snaps.Count + " children";
                }
                int moved = 0, order = 0, unparented = 0, gone = 0; float mp = 0, mr = 0, ms = 0; string first = "";
                foreach (var s in snaps)
                {
                    if (!s.T) { gone++; continue; }
                    if (s.T.position != s.P || s.T.rotation != s.R || s.T.lossyScale != s.S)
                    {
                        moved++; float dp = (s.T.position - s.P).magnitude, dr = Quaternion.Angle(s.T.rotation, s.R), ds = (s.T.lossyScale - s.S).magnitude;
                        if (dp > mp) mp = dp; if (dr > mr) mr = dr; if (ds > ms) ms = ds;
                        if (first.Length == 0) first = s.T.name + " dpos=" + dp.ToString("G4") + " drot=" + dr.ToString("G4") + " dscale=" + ds.ToString("G4") + " scale=" + s.S.ToString("G5") + " rot=" + s.R.eulerAngles.ToString("G5");
                    }
                    if (!ReferenceEquals(s.T.parent, p)) unparented++;
                    else if (s.T.GetSiblingIndex() != s.Index) order++;
                }
                return "verify b: snaps=" + snaps.Count + " transform_changed=" + moved + " not_under_manager=" + unparented + " sibling_index_changed=" + order + " max_dpos=" + mp.ToString("G4") + " max_drot_deg=" + mr.ToString("G4") + " max_dscale=" + ms.ToString("G4") + " first[" + first + "] destroyed=" + gone + " children_now=" + p.childCount + " | " + State();
            }
            return "decodetach state|on|off|finish|budget <ms>|verify a|verify b";
        }
#endif
    }
}
