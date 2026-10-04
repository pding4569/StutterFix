using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace StutterFix
{
    // 박자 알림에서 아무것도 안 하는 타일 건너뛰기.
    //
    // scrConductor.PropagateOnBeat 는 박자마다(한 프레임에 한 박자씩만 따라간다) 맵의 모든 타일에 OnBeat 를 부른다.
    // 2026-09-28 측정(9만 타일 시험 맵, 개발자용):
    //   편집 화면: 맵을 연 직후 밀린 박자를 따라잡는 약 40초 동안 매 프레임 불려 scrConductor.Update 가 프레임당 6.3ms ("게임 처리" 표시).
    //   BPM 100000 곡: 매 프레임 불려 1.8ms.
    // scrFloor.OnBeat 는 아직 안 빛난 타일(hasLit=false)에서는 vfx.overrideTileSprites 일 때만 무언가를 한다(스프라이트 바꾸기).
    // 그래서 빛난 타일 목록(번호순)을 따로 들고 있다가, overrideTileSprites 가 꺼져 있으면 그 목록의 타일에만 OnBeat 를 부른다.
    // 빛난 타일은 원래 순서대로 부르므로 무작위 색의 난수 순서도 같다.
    //   hasLit 를 바꾸는 게임 코드는 6곳(IL 전체 검색): scrFloor.LightUp(켬), scrFloor.Reset(끔), 타일 만들기 4곳(MakeLevel 안).
    //   LightUp·Reset 뒤에 목록을 고치고, 타일 목록이 달라지면(MakeLevel, 개수·처음·끝이 다름) 처음부터 다시 만든다.
    // 원래 반복을 쓰는 경우: 타일 종류가 scrFloor 그 자체가 아닌 것이 있거나, 타일의 vfx 가 없거나 서로 다르거나, overrideTileSprites 가
    //   켜져 있거나, 다른 모드가 scrFloor.OnBeat 를 패치했거나 PropagateOnBeat 를 고쳐 쓰는(transpiler·다른 모드의 prefix) 경우.
    // 개발자용 검증(beat-verify): 박자 64번마다 타일 전부의 hasLit 와 목록을 비교한다.
    internal static class BeatFix
    {
        internal static bool Enabled = true;
        internal static bool Verify;
        internal static long Skipped, Called, Fast, Slow, Checks, Mismatch;
        private static readonly AccessTools.FieldRef<scrFloor, scrVfx> vfxRef = AccessTools.FieldRefAccess<scrFloor, scrVfx>("vfx");
        private static MethodBase onBeat, prop;
        private static scrFloor[] arr = new scrFloor[0];
        private static bool[] isLit = new bool[0];
        private static readonly List<int> lit = new List<int>();
        private static readonly Dictionary<scrFloor, int> idx = new Dictionary<scrFloor, int>();
        private static bool dirty = true, eligible;
        private static scrVfx vfx0;

        internal static void Install(Harmony h)
        {
            onBeat = AccessTools.Method(typeof(scrFloor), "OnBeat");
            prop = AccessTools.Method(typeof(scrConductor), "PropagateOnBeat");
            var light = AccessTools.Method(typeof(scrFloor), "LightUp");
            var reset = AccessTools.Method(typeof(scrFloor), "Reset");
            var make = AccessTools.Method(typeof(scrLevelMaker), "MakeLevel");
            if (onBeat == null || prop == null || light == null || reset == null || make == null) { Main.Entry.Logger.Log("[박자 알림] 게임 코드 모양이 달라 쓰지 않음"); return; }
            h.Patch(prop, prefix: new HarmonyMethod(typeof(BeatFix), nameof(PropPrefix)));
            if (Edition.Dev) Main.Entry.Logger.Log("[박자 알림] 목록 바뀜 횟수 읽기: " + (verRef != null ? "됨" : "안 됨 (개수만 봄)"));
            h.Patch(light, postfix: new HarmonyMethod(typeof(BeatFix), nameof(LitChanged)));
            h.Patch(reset, postfix: new HarmonyMethod(typeof(BeatFix), nameof(LitChanged)));
            var mark = new HarmonyMethod(typeof(BeatFix), nameof(MarkDirty));
            h.Patch(make, prefix: mark, postfix: mark);
            foreach (var n in new[] { "InstantiateStringFloors", "InstantiateFloatFloors" }) { var m = AccessTools.Method(typeof(scrLevelMaker), n); if (m != null) h.Patch(m, postfix: mark); }
        }

        public static void MarkDirty() { dirty = true; lDirty = true; dirtyGen++; }
        // 새 맵 파일을 열 때: 지난 맵 타일 번호표를 놓는다 (다음 박자에 다시 만든다)
        internal static void ForgetLevel() { MarkDirty(); arr = new scrFloor[0]; idx.Clear(); lit.Clear(); lActive.Clear(); lHead.Clear(); }
        private static int dirtyGen, litGen;

        // 다른 모드가 고쳐 쓰는지: scrFloor.OnBeat 에 패치가 있거나, PropagateOnBeat 에 transpiler 나 이 모드 밖의 prefix 가 있으면
        private static bool Foreign()
        {
            try
            {
                var a = Harmony.GetPatchInfo(onBeat);
                if (a != null && a.Owners.Count > 0) return true;
                var b = Harmony.GetPatchInfo(prop);
                if (b == null) return false;
                if (b.Transpilers.Count > 0) return true;
                foreach (var p in b.Prefixes) if (!p.owner.StartsWith("StutterFix", StringComparison.Ordinal)) return true;
                return false;
            }
            catch { return true; }
        }

        // ── 첫 반복(onBeats): 타일은 Awake 마다(타일 다시 설정 때도) 자기를 또 넣어 같은 타일이 여러 번 들어 있다(맵을 열고 편집할수록 늘어난다).
        //    원래와 같은 순서로, 무언가를 하는 항목(타일이 아닌 것, 빛난 타일 등)만 부른다. 목록이 바뀌면(개수·레벨 다시 만들기) 원래 반복을
        //    그대로 한 번 돌며(지워진 항목 빼기 포함, 아무것도 안 하는 타일만 안 부름) 번호를 다시 만든다.
        //    아무것도 안 하는 타일 항목이 그 사이 지워졌으면 원래보다 늦게 목록에서 빠진다(부르지 않으므로 동작은 같다).
        private static List<ADOBase> lRef;
        private static int lCount = -1;
        private static bool lDirty = true, lOverride;
        private static readonly List<int> lActive = new List<int>();
        private sealed class RefEq : IEqualityComparer<scrFloor>
        {
            internal static readonly RefEq I = new RefEq();
            public bool Equals(scrFloor a, scrFloor b) { return ReferenceEquals(a, b); }
            public int GetHashCode(scrFloor o) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o); }
        }
        private static readonly Dictionary<scrFloor, int> lHead = new Dictionary<scrFloor, int>(RefEq.I);
        private static int[] lNext = new int[0];
        private static int[] lBuf = new int[64];
        private static long lChecks;
        private static readonly System.Diagnostics.Stopwatch rebuildSw = new System.Diagnostics.Stopwatch();

        // 이 타일의 OnBeat 가 아무것도 안 하는가 (타일 종류가 scrFloor 그 자체이고 vfx 가 있을 때만 판단)
        private static bool NoOp(scrFloor f, out bool plain)
        {
            plain = false;
            if (ReferenceEquals(f, null) || f.GetType() != typeof(scrFloor)) return false;
            var v = vfxRef(f);
            if (ReferenceEquals(v, null)) return false;
            plain = true;
            return f.dontChangeMySprite || (!f.hasLit && !v.overrideTileSprites);
        }

        private static void Listeners(List<ADOBase> list)
        {
            // 타일들의 vfx 는 scrVfx.instance (scrFloor.Awake). vfx0 은 게임 화면에서만 채워지므로 편집 화면에서도 바뀜을 보려면 instance 를 본다
            var vi = scrVfx.instance;
            bool ov = !ReferenceEquals(vi, null) && vi.overrideTileSprites;
            // 목록의 바뀜 횟수(List._version)로 지난 번호 매기기 뒤 무엇이 있었는지 본다. 늘어난 개수만큼만 바뀌었으면 끝에 더하기(Add)뿐이다
            // (Clear·RemoveAt·Insert·덮어쓰기는 개수와 바뀜 횟수가 어긋난다). 그때는 새 항목에만 번호를 매긴다. 편집 화면에서 알림을 받는
            // 물체가 새로 생길 때마다 9만 항목을 다시 훑지 않게. 바뀜 횟수를 못 읽으면 예전처럼 개수만 본다.
            if (!lDirty && ReferenceEquals(list, lRef) && ov == lOverride && verRef != null)
            {
                int dc = list.Count - lCount;
                if (dc > 0 && verRef(list) - lVersion == dc) Append(list);
            }
            if (!lDirty && ReferenceEquals(list, lRef) && list.Count == lCount && ov == lOverride && (verRef == null || verRef(list) == lVersion))
            {
                if (Verify && (++lChecks & 63) == 0)
                {
                    int k2 = 0, bad = 0;
                    for (int k = 0; k < list.Count; k++)
                    {
                        bool plain;
                        if (NoOp(list[k] as scrFloor, out plain)) continue;
                        if (k2 >= lActive.Count || lActive[k2] != k) bad++; else k2++;
                    }
                    if (k2 != lActive.Count) bad++;
                    if (bad > 0) { Mismatch += bad; lDirty = true; }
                }
                int cnt = lActive.Count;
                bool alive = true;
                for (int j = 0; j < cnt; j++) if (list[lActive[j]] == null) { alive = false; break; }
                if (alive)
                {
                    if (lBuf.Length < cnt) lBuf = new int[Math.Max(cnt, lBuf.Length * 2)];
                    lActive.CopyTo(lBuf);
                    for (int j = 0; j < cnt; j++) list[lBuf[j]].OnBeat();
                    Called += cnt; Skipped += list.Count - cnt;
                    return;
                }
            }
            // 원래 반복 그대로 (아무것도 안 하는 타일만 안 부름). 번호도 같은 반복에서 만든다(9만 항목을 두 번 훑으면 68ms 였다).
            // 항목은 끝에만 더해지므로(Awake 의 Add) 반복 중 OnBeat 가 새 항목을 넣어도 앞 번호는 그대로이고, 뒤에 붙은 것만 따로 번호를 매긴다.
            rebuildSw.Restart();
            lDirty = true;   // 반복 중 OnBeat 안에서 빛남이 바뀌어도(LitChanged) 만드는 중인 번호를 건드리지 않게, 끝난 뒤에 푼다
            lActive.Clear(); lHead.Clear();
            int g0 = litGen, d0 = dirtyGen;
            int num = list.Count;
            if (lNext.Length < num) lNext = new int[Math.Max(num, lNext.Length * 2)];
            int i = 0;
            while (i < num)
            {
                var e = list[i];
                if (e == null) { list.RemoveAt(i); num--; continue; }
                bool plain;
                bool noop = NoOp(e as scrFloor, out plain);
                if (plain) Link((scrFloor)e, i);
                if (noop) Skipped++;
                else { lActive.Add(i); e.OnBeat(); Called++; }
                i++;
            }
            if (lNext.Length < list.Count) Array.Resize(ref lNext, list.Count);
            int from = num;
            // 반복 중 누군가의 OnBeat 가 타일 빛남을 바꿨으면 앞에서 매긴 번호가 낡았을 수 있다: 처음부터 다시 매긴다
            if (litGen != g0) { lActive.Clear(); lHead.Clear(); from = 0; }
            for (int k = from; k < list.Count; k++)
            {
                var f = list[k] as scrFloor;
                bool plain;
                bool noop = NoOp(f, out plain);
                if (plain) Link(f, k);
                if (!noop) lActive.Add(k);
            }
            lRef = list; lCount = list.Count; lOverride = ov; lVersion = verRef != null ? verRef(list) : 0;
            lDirty = dirtyGen != d0;   // 반복 중 타일 목록을 다시 만들었으면 다음 박자에 또 만든다
            RebuildMs += rebuildSw.Elapsed.TotalMilliseconds; Rebuilds++;
            if (Edition.Dev && rebuildSw.ElapsedMilliseconds >= 3) Main.Entry.Logger.Log("[박자 알림] 첫 반복 다시 만들기 " + rebuildSw.ElapsedMilliseconds + "ms (항목 " + list.Count + "개)");
        }
        internal static double RebuildMs; internal static long Rebuilds, Appends;

        private static int lVersion;
        private static readonly AccessTools.FieldRef<List<ADOBase>, int> verRef = VersionRef();
        private static AccessTools.FieldRef<List<ADOBase>, int> VersionRef()
        {
            try
            {
                var f = AccessTools.Field(typeof(List<ADOBase>), "_version");
                return f != null && f.FieldType == typeof(int) ? AccessTools.FieldRefAccess<List<ADOBase>, int>(f) : null;
            }
            catch { return null; }
        }

        // 끝에 더해진 항목에만 번호 매기기 (앞 번호는 그대로)
        private static void Append(List<ADOBase> list)
        {
            int n = list.Count;
            if (lNext.Length < n) Array.Resize(ref lNext, Math.Max(n, lNext.Length * 2));
            for (int k = lCount; k < n; k++)
            {
                var f = list[k] as scrFloor;
                bool plain;
                bool noop = NoOp(f, out plain);
                if (plain) Link(f, k);
                if (!noop) lActive.Add(k);
            }
            lCount = n; lVersion = verRef(list); Appends++;
        }

        // 타일별 번호 사슬: lHead[타일] = 마지막 번호, lNext[번호] = 같은 타일의 앞 번호 (-1 끝). 타일마다 목록을 만들지 않는다.
        private static void Link(scrFloor f, int k)
        {
            int h;
            lNext[k] = lHead.TryGetValue(f, out h) ? h : -1;
            lHead[f] = k;
        }

        // 타일의 hasLit 가 바뀌었을 때 첫 반복의 번호도 고친다
        private static void ListenersLitChanged(scrFloor f)
        {
            int head;
            if (lDirty || !lHead.TryGetValue(f, out head)) return;
            bool plain;
            bool noop = NoOp(f, out plain);
            for (int k = head; k >= 0; k = lNext[k])
            {
                int at = lActive.BinarySearch(k);
                if (!noop) { if (at < 0) lActive.Insert(~at, k); }
                else if (at >= 0) lActive.RemoveAt(at);
            }
        }

        // PropagateOnBeat 를 그대로 따라 하되, 타일 반복만 빛난 타일로
        public static bool PropPrefix(scrConductor __instance)
        {
            if (!Enabled || Foreign()) { Slow++; return true; }
            Listeners(__instance.onBeats);
            if (ADOBase.controller != null && ADOBase.controller.gameworld)
            {
                List<scrFloor> lf = ADOBase.lm.listFloors;
                if (!FastFloors(lf))
                {
                    int count = lf.Count;
                    for (int k = 0; k < count; k++) lf[k].OnBeat();
                    Called += count;
                }
            }
            __instance.onBeatFrame = UnityEngine.Time.frameCount;
            return false;
        }

        private static bool FastFloors(List<scrFloor> lf)
        {
            int n = lf.Count;
            if (dirty || arr.Length != n || (n > 0 && (!ReferenceEquals(arr[0], lf[0]) || !ReferenceEquals(arr[n - 1], lf[n - 1])))) Rebuild(lf);
            if (!eligible || vfx0.overrideTileSprites) { Slow++; return false; }
            if (Verify && (++Checks & 63) == 0) Check(lf);
            // 부르는 동안 목록이 바뀌어도(OnBeat 안에서 LightUp 등) 원래처럼 이번 박자 시작 때의 순서대로 (원래 반복도 타일 목록을 앞에서부터 본다)
            int cnt = lit.Count;
            if (litBuf.Length < cnt) litBuf = new int[Math.Max(cnt, litBuf.Length * 2)];
            lit.CopyTo(litBuf);
            for (int j = 0; j < cnt; j++) arr[litBuf[j]].OnBeat();
            Called += cnt; Skipped += n - cnt; Fast++;
            return true;
        }
        private static int[] litBuf = new int[64];

        private static void Rebuild(List<scrFloor> lf)
        {
            dirty = false;
            int n = lf.Count;
            arr = lf.ToArray();
            isLit = new bool[n];
            lit.Clear(); idx.Clear();
            eligible = true; vfx0 = null;
            for (int i = 0; i < n; i++)
            {
                var f = arr[i];
                if (ReferenceEquals(f, null) || f.GetType() != typeof(scrFloor)) { eligible = false; continue; }
                idx[f] = i;
                var v = vfxRef(f);
                if (ReferenceEquals(v, null) || (!ReferenceEquals(vfx0, null) && !ReferenceEquals(v, vfx0))) eligible = false;
                if (ReferenceEquals(vfx0, null)) vfx0 = v;
                if (f.hasLit) { isLit[i] = true; lit.Add(i); }
            }
            if (ReferenceEquals(vfx0, null)) eligible = false;
        }

        // LightUp / Reset 뒤: 그 타일의 hasLit 에 맞춰 목록을 고친다 (번호순 유지)
        public static void LitChanged(scrFloor __instance)
        {
            litGen++;
            ListenersLitChanged(__instance);
            int i;
            if (dirty || !idx.TryGetValue(__instance, out i)) return;
            bool now = __instance.hasLit;
            if (now == isLit[i]) return;
            isLit[i] = now;
            int at = lit.BinarySearch(i);
            if (now) { if (at < 0) lit.Insert(~at, i); }
            else if (at >= 0) lit.RemoveAt(at);
        }

        // (개발자용) 타일 전부의 hasLit 와 목록 비교
        private static void Check(List<scrFloor> lf)
        {
            int bad = 0;
            for (int i = 0; i < lf.Count; i++)
            {
                var f = lf[i];
                if (!ReferenceEquals(f, arr[i])) { bad++; continue; }
                if (!ReferenceEquals(f, null) && f.hasLit != isLit[i]) bad++;
            }
            int prev = -1;
            foreach (var i in lit) { if (i <= prev || !isLit[i]) bad++; prev = i; }
            if (bad > 0) { Mismatch += bad; Rebuild(lf); }
        }
    }
}
