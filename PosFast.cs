using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // Default-off position setter experiment. Dictionary access occurs only at effect creation.
    internal static class PosFast
    {
        internal static bool Enabled;
        private struct Entry
        {
            internal scrDecoration D;
            internal scrParallax Parallax;
            internal Transform Child;
            internal Vector2 Offset, Cropped, Scale;
            internal DecPlacementType Placement;
            internal bool Has, Rejected, Clamp;
        }
        private static readonly Dictionary<scrDecoration, int> index = new Dictionary<scrDecoration, int>();
        private static Entry[] slots = new Entry[64];
        private static int count, generation, compatible; // 0 unknown, 1 yes, 2 no
        private static long skipped, full, samples, checkedN, mismatch;
        private static readonly long[] reasons = new long[14];
        private static bool verify;
        private static string first = "none", compatibility = "pending";

        internal static void Configure(string path)
        {
            Forget();
#if DEV || AUTOTEST
            Enabled = System.IO.File.Exists(System.IO.Path.Combine(path, "posfast-on"));
#else
            Enabled = false;
#endif
        }
        internal static void Uninstall() { Enabled = false; verify = false; Forget(); }
        internal static void Forget()
        {
            index.Clear(); Array.Clear(slots, 0, count); count = 0;
            unchecked { generation++; }
            compatible = 0; compatibility = "pending";
        }
        internal static void Bind(DecoAnim.Rec r)
        {
            r.PosSlot = -1;
            if (!Enabled || (r.Key != 1 && r.Key != 2)) return;
            if (!index.TryGetValue(r.D, out int slot))
            {
                slot = count++;
                if (count > slots.Length) Array.Resize(ref slots, slots.Length * 2);
                slots[slot].D = r.D; index.Add(r.D, slot);
            }
            r.PosSlot = slot; r.PosGeneration = generation;
            // Effect setup may have changed the crop, scale or child relationship.
            slots[slot].Has = false;
            Compatible();
        }
        private static bool Compatible()
        {
            if (compatible != 0) return compatible == 1;
            try
            {
                foreach (var m in typeof(scrDecoration).GetMethods(AccessTools.all))
                {
                    if (m.Name != "SetPosition" && m.Name != "SetPositionX" && m.Name != "SetPositionY"
                        && m.Name != "UpdateScreenClamp" && m.Name != "get_trueScaleMultiplier") continue;
                    var info = Harmony.GetPatchInfo(m);
                    if (info == null) continue;
                    foreach (string id in info.Owners)
                        if (id != Main.Entry.Info.Id)
                        { compatible = 2; compatibility = m.Name + " owner=" + id; return false; }
                }
                compatibility = "ok"; compatible = 1; return true;
            }
            catch (Exception ex) { compatible = 2; compatibility = ex.GetType().Name; return false; }
        }
        private static bool Bound(DecoAnim.Rec r) => r.PosGeneration == generation && r.PosSlot >= 0
            && r.PosSlot < count && ReferenceEquals(slots[r.PosSlot].D, r.D);
        private static bool Camera(DecPlacementType t) => t == DecPlacementType.Camera || t == DecPlacementType.CameraAspect;
        private static bool Bit(float a, float b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);
        private static bool Bit(Vector2 a, Vector2 b) => Bit(a.x,b.x) && Bit(a.y,b.y);
        private static bool Bit(Vector3 a, Vector3 b) => Bit(a.x,b.x) && Bit(a.y,b.y) && Bit(a.z,b.z);

        private static int Eligible(scrDecoration d)
        {
            if (d.GetType() != typeof(scrVisualDecoration)) return 1;
            if (d.parallax == null || d.childTransform == null) return 2;
            if (d.parallax.clampToScreen || Camera(d.placementType)) return 3;
            if (ADOBase.editor != null) return 4;
            if (compatible != 1) return 5;
            // A visible renderer with alpha=0 can still use InvisibleSkip's lazy path.
            // Require positive color alpha so a fallback really reaches the full setter.
            if (!(d.color.a > 0f)) return 6;
            return 0;
        }
        private static int Guard(ref Entry e, scrDecoration d)
        {
            int reason = Eligible(d);
            if (reason != 0) return reason;
            return MemoGuard(ref e, d);
        }
        private static int MemoGuard(ref Entry e, scrDecoration d)
        {
            if (e.Rejected) return 7;
            if (!e.Has) return 8;
            if (!ReferenceEquals(e.Child, d.childTransform) || !ReferenceEquals(e.Parallax, d.parallax)
                || e.Placement != d.placementType || e.Clamp != d.parallax.clampToScreen) return 9;
            if (!Bit(e.Offset,d.pivotOffsetVec) || !Bit(e.Cropped,CropField.Get(d))
                || !Bit(e.Scale,d.trueScaleMultiplier)) return 10;
            return 0;
        }
        internal static bool Try(DecoAnim.Rec r, Vector2 pos)
        {
            if (!Enabled) return false;
            if (!Bound(r)) { full++; reasons[0]++; return false; }
            ref Entry e = ref slots[r.PosSlot];
            int reason = Guard(ref e, r.D);
            // Raw game IL writes screenRelativePos unconditionally, even without clamp.
            // Retain the requested pivot-only store only when that omitted write is bit-identical.
            // CameraAspect was already rejected, so no screen aspect calculation is needed.
            // 2026-10-19: screenRelativePos 는 clampToScreen==false 이고 placement 가 Camera 계열이 아니면 아무도 읽지 않는 죽은 상태다
            // (IL 전수: 읽는 곳은 scrParallax.SetTrans 의 clampToScreen 분기 하나뿐, 쓰는 곳은 UpdateScreenClamp 뿐이고 clamp 가 켜질 때는
            // UpdateScreenClamp 가 그 시점의 위치로 다시 계산한다). 그래서 이 값의 비트 동일은 요구하지 않는다.
            if (reason == 0 && !MoveApply.SkipForLate(r.D)) reason = 11;
            if (reason != 0) { full++; reasons[reason]++; e.Has = false; return false; }
            r.D.pivotPosVec = pos;
            skipped++;
#if DEV || AUTOTEST
            if (verify && (++samples & 63) == 0) Compare(r, ref e);
#endif
            return true;
        }
        private static Vector2 Screen(Vector2 pos, Vector2 offset) => (pos + offset) / 20f + new Vector2(.5f,.5f);
        internal static void Completing(DecoAnim.Rec r)
        {
            full++; reasons[12]++;
            if (Bound(r)) slots[r.PosSlot].Has = false;
        }
        internal static void Remember(DecoAnim.Rec r)
        {
            if (!Enabled || !Bound(r)) return;
            ref Entry e = ref slots[r.PosSlot];
            e.Has = false;
            var d = r.D;
            if (e.Rejected || Eligible(d) != 0) return;
            // Read only on fallback: do not seed from an incomplete/lazy/epsilon-skipped write.
            Vector2 scale = d.trueScaleMultiplier;
            Vector3 expected = (d.pivotOffsetVec + CropField.Get(d)) / scale;
            if (!Bit(d.childTransform.localPosition, expected)) return;
            e.Offset = d.pivotOffsetVec; e.Cropped = CropField.Get(d); e.Scale = scale;
            e.Placement = d.placementType; e.Clamp = d.parallax.clampToScreen;
            e.Parallax = d.parallax; e.Child = d.childTransform; e.Has = true;
        }
#if DEV || AUTOTEST
        private static void Compare(DecoAnim.Rec r, ref Entry e)
        {
            var d = r.D; var p = d.parallax;
            var pos = d.pivotPosVec; var off = d.pivotOffsetVec;
            var child = d.childTransform.localPosition; var screen = p.screenRelativePos; bool clamp = p.clampToScreen;
            try { d.SetPosition(pos, off); }
            catch { e.Rejected = true; e.Has = false; throw; }
            checkedN++;
            string field = !Bit(pos,d.pivotPosVec) ? "pivotPosVec" : !Bit(off,d.pivotOffsetVec) ? "pivotOffsetVec"
                : !Bit(child,d.childTransform.localPosition) ? "child.localPosition"
                : clamp != p.clampToScreen ? "clampToScreen" : null;   // screenRelativePos 는 clamp 꺼짐일 때 죽은 상태라 비교하지 않는다
            if (field == null) return;
            mismatch++; e.Rejected = true; e.Has = false;
            if (first == "none") first = d.name + " field=" + field;
            if (mismatch == 1) Main.Entry.Logger.Log("[posfast] mismatch=" + mismatch + " first=" + first
                + "; decoration rejected; further counts in posfast state");
        }
        internal static string Command(string arg)
        {
            if (arg == "verify") { verify = true; samples = 0; }
            else if (arg == "verify off") verify = false;
            else if (arg == "bench") return Bench();
            return State();
        }
        private static string Bench()
        {
            if (Hitch.Playing) return "bench refused during playback; run in menu";
            const int n = 2048, repeat = 256;
            var root = new GameObject("posfast-bench");
            var es = new Entry[n]; var ds = new scrDecoration[n];
            var set = new HashSet<scrDecoration>(); var list = new List<scrDecoration>();
            try
            {
                for (int i = 0; i < n; i++)
                {
                    var go = new GameObject("pf" + i); go.transform.SetParent(root.transform, false);
                    var d = (scrDecoration)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(scrVisualDecoration));
                    var p = (scrParallax)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(scrParallax));
                    d.childTransform = go.transform; d.parallax = p; d.color = Color.white;
                    d.scaleMultiplier = 1f; d.textureScaleMultiplier = Vector2.one;
                    d.pivotPosVec = new Vector2(i, -i);
                    p.screenRelativePos = Screen(d.pivotPosVec, d.pivotOffsetVec);
                    ds[i] = d; es[i] = new Entry { D=d, Child=d.childTransform, Parallax=p, Has=true,
                        Scale=Vector2.one, Placement=d.placementType };
                }
                // Actual Guard, field store and actual SkipForLate registration, with only its
                // live-scene eligibility replaced. No fake full-chain timing is reported.
                int sink = 0, peakList = 0;
                for (int warm = 0; warm < 5; warm++)
                {
                    for (int i = 0; i < n; i++) sink += BenchStep(ref es[i], ds[i], i, set, list);
                    set.Clear(); list.Clear();
                }
                long start = Stopwatch.GetTimestamp();
                for (int r = 0; r < repeat; r++)
                {
                    for (int i = 0; i < n; i++) sink += BenchStep(ref es[i], ds[i], i, set, list);
                    peakList = Math.Max(peakList, list.Count); set.Clear(); list.Clear();
                }
                double us = (Stopwatch.GetTimestamp()-start)*1e6/Stopwatch.Frequency/repeat/n;
                return "guard+store+late registration us/deco=" + us.ToString("F4") + " budget<=0.15us=" + (us<=.15)
                    + " n=2048 repeat=256 sink=" + sink + " registered/frame=" + peakList
                    + "; successful unchanged-screen path; scene eligibility simulated; includes scratch-set cleanup; excludes real eligibility and full chain";
            }
            finally { UnityEngine.Object.Destroy(root); }
        }
        private static int BenchStep(ref Entry e, scrDecoration d, float value, HashSet<scrDecoration> set, List<scrDecoration> list)
        {
            // Uninitialized Unity components have no native pointer; their overloaded null
            // tests cannot pass. Simulate only scene/liveness checks, keep memo guards real.
            if (d.GetType() != typeof(scrVisualDecoration) || (object)d.parallax == null
                || (object)d.childTransform == null || d.parallax.clampToScreen || Camera(d.placementType)
                || !(d.color.a > 0f)) return 12;
            int reason = MemoGuard(ref e,d);
            if (reason != 0) return reason;
            var pos = new Vector2(value, -value);
            MoveApply.RegisterLate(d, set, list);
            d.pivotPosVec = pos;
            return 0;
        }
#endif
        internal static string State() => "enabled=" + Enabled + " slots=" + count + " skipped=" + skipped + " full=" + full
            + " reasons(unbound/type/null/clamp/editor/foreign/alpha/rejected/cold/identity/fields/late/complete/screen-change)=" + string.Join("/",reasons)
            + " verify=" + verify + " checked=" + checkedN + " mismatch=" + mismatch + " first=" + first
            + " compatibility=" + compatibility;
    }
}
