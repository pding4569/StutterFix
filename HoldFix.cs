using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // scrHoldRenderer.Update 는 입력이 그대로여도 매 프레임 재질 값을 이름으로 다시 쓴다.
    // 그 함수가 읽는 값을 먼저 비교해 지난번과 모두 같으면 건너뛴다. 끝 표시(showHoldTiming)와 hit·unfilling 중에는 건드리지 않는다.
    // 다른 곳이 UpdateColor 를 직접 부르면 기록을 무효로 한다. verify 는 건너뛸 자리에서 원본을 돌려 재질 값이 안 바뀌는지 본다.
    internal static class HoldFix
    {
        private sealed class Rec
        {
            internal bool Valid;
            internal scrFloor Head, Tail;
            internal Transform StartT, HeadT, TailT, HeadRT, TailRT;
            internal Vector3 StartP, HeadP, TailP;
            internal float TileSize, StartR, TailR, HeadW, TailW, HeadO, TailO, HoldO, Alp, Fill, EndO;
            internal double Exit;
            internal Color Touch;
        }

        private static readonly AccessTools.FieldRef<scrHoldRenderer, scrFloor> headRef = AccessTools.FieldRefAccess<scrHoldRenderer, scrFloor>("headFloor");
        private static readonly AccessTools.FieldRef<scrHoldRenderer, scrFloor> tailRef = AccessTools.FieldRefAccess<scrHoldRenderer, scrFloor>("tailFloor");
        private static readonly AccessTools.FieldRef<scrHoldRenderer, float> alpRef = AccessTools.FieldRefAccess<scrHoldRenderer, float>("extendAlp");
        private static readonly Dictionary<scrHoldRenderer, Rec> recs = new Dictionary<scrHoldRenderer, Rec>();
        private static readonly string[] FloatNames = { "_StartTileSize", "_EndTileSize", "_StartWidth", "_EndWidth", "_Completion", "_StartAlpha", "_EndAlpha" };
        private static bool verify, inside;
        private static readonly float[] before = new float[FloatNames.Length];
        private static Vector4 beforeStart, beforeEnd;
        private static Color beforeFill;
        internal static long Calls, Skips, Checked, Mismatch;

        internal static bool Enabled;

        internal static void Install(Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(scrHoldRenderer), "Update"),
                prefix: new HarmonyMethod(typeof(HoldFix), nameof(UpdatePrefix)), postfix: new HarmonyMethod(typeof(HoldFix), nameof(UpdatePostfix)));
            h.Patch(AccessTools.Method(typeof(scrHoldRenderer), "UpdateColor", new[] { typeof(Color), typeof(float) }),
                postfix: new HarmonyMethod(typeof(HoldFix), nameof(ColorPostfix)));
            h.Patch(AccessTools.Method(typeof(scrLevelMaker), "DrawHolds"), postfix: new HarmonyMethod(typeof(HoldFix), nameof(ClearPostfix)));
        }

        public static void ClearPostfix() { recs.Clear(); }

        // 시험용: 건너뛸 자리에서 원본을 돌려 재질 값이 안 바뀌는지 본다
        internal static void Command(string arg)
        {
            if (arg == "report") { Main.Entry.Logger.Log("[holdskip] 호출 " + Calls + " 건너뜀 " + Skips + " 대조 " + Checked + " 다름 " + Mismatch + " 홀드 " + recs.Count); return; }
            verify = arg == "verify";
        }

        // 게임의 Update 앞부분과 같은 규칙으로 이번 프레임 값을 구해 기록과 비교한다.
        private static bool Same(scrHoldRenderer h, Rec r, bool store)
        {
            scrFloor start = h.startFloor, head = headRef(h), tail = tailRef(h);
            float alp = alpRef(h);
            if (start.extendAnim == 0f) { tail = start; alp = 0f; }
            else if (start.nextfloor != null && start.nextfloor.extendAnim > 0f) { tail = start.nextfloor; alp = tail.extendAnim; }
            if (head == null || tail == null) return false;
            bool same = r.Valid && ReferenceEquals(head, r.Head) && ReferenceEquals(tail, r.Tail);
            if (!same || store)
            {
                r.Head = head; r.Tail = tail;
                r.StartT = start.transform; r.HeadT = head.transform; r.TailT = tail.transform;
                r.HeadRT = head.floorRenderer.transform; r.TailRT = tail.floorRenderer.transform;
                same = false;
            }
            var sp = r.StartT.position;
            var hp = ReferenceEquals(r.HeadT, r.StartT) ? sp : r.HeadT.position;
            var tp = ReferenceEquals(r.TailT, r.StartT) ? sp : (ReferenceEquals(r.TailT, r.HeadT) ? hp : r.TailT.position);
            float hw = r.HeadRT.localScale.x;
            float tw = ReferenceEquals(r.TailRT, r.HeadRT) ? hw : r.TailRT.localScale.x;
            float ts = scrController.instance.tileSize;
            float endO = tail.freeroam ? tail.freeroamFloors[0].opacity : tail.opacity;
            same = same && sp == r.StartP && hp == r.HeadP && tp == r.TailP && hw == r.HeadW && tw == r.TailW
                && ts == r.TileSize && start.radiusScale == r.StartR && tail.radiusScale == r.TailR && start.exitangle == r.Exit
                && head.opacity == r.HeadO && tail.opacity == r.TailO && start.holdOpacity == r.HoldO && alp == r.Alp
                && endO == r.EndO && start.holdCompletionEased == r.Fill && h.touchColor == r.Touch;
            if (store)
            {
                r.StartP = sp; r.HeadP = hp; r.TailP = tp; r.HeadW = hw; r.TailW = tw; r.TileSize = ts;
                r.StartR = start.radiusScale; r.TailR = tail.radiusScale; r.Exit = start.exitangle;
                r.HeadO = head.opacity; r.TailO = tail.opacity; r.HoldO = start.holdOpacity; r.Alp = alp;
                r.EndO = endO; r.Fill = start.holdCompletionEased; r.Touch = h.touchColor;
                r.Valid = true;
            }
            return same;
        }

        public static bool UpdatePrefix(scrHoldRenderer __instance, out int __state)
        {
            __state = 0;   // 0: 건드리지 않음, 1: 끝난 뒤 기록, 2: 대조
            if (!Enabled) return true;
            Calls++;
            var h = __instance;
            if (h.startFloor == null || h.startFloor.showHoldTiming || h.hit || h.unfilling || h.m_meshRenderer == null) return true;
            if (!recs.TryGetValue(h, out var r)) recs[h] = r = new Rec();
            inside = true;
            if (!Same(h, r, false)) { __state = 1; return true; }
            if (!verify) { Skips++; inside = false; return false; }
            Capture(h, before, out beforeStart, out beforeEnd, out beforeFill);
            __state = 2;
            return true;
        }

        public static void UpdatePostfix(scrHoldRenderer __instance, int __state)
        {
            inside = false;
            if (__state == 0) return;
            var h = __instance;
            if (!recs.TryGetValue(h, out var r)) return;
            if (__state == 2)
            {
                var after = new float[FloatNames.Length];
                Capture(h, after, out var s, out var e, out var f);
                Checked++;
                bool bad = s != beforeStart || e != beforeEnd || f != beforeFill;
                for (int i = 0; i < after.Length && !bad; i++) bad = after[i] != before[i];
                if (bad) Mismatch++;
            }
            Same(h, r, true);
        }

        public static void ColorPostfix(scrHoldRenderer __instance)
        {
            if (inside) return;
            if (recs.TryGetValue(__instance, out var r)) r.Valid = false;
        }

        private static void Capture(scrHoldRenderer h, float[] floats, out Vector4 start, out Vector4 end, out Color fill)
        {
            var m = h.m_meshRenderer.sharedMaterial;
            for (int i = 0; i < floats.Length; i++) floats[i] = m.GetFloat(FloatNames[i]);
            start = m.GetVector("_StartPosition"); end = m.GetVector("_EndPosition"); fill = m.GetColor("_FillColor");
        }
    }
}
