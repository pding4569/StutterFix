using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using DG.Tweening;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // Phase 1 deliberately publishes managed fields every Update. No lazy readers or
    // extra Harmony patches; ownership is disjoint from recs, never order-merged.
    internal static class DecoEngine
    {
        internal sealed class State
        {
            internal scrDecoration D;
            internal TightLate.Entry LateEntry;
            internal readonly DecoAnim.Rec[] Slots = new DecoAnim.Rec[5];
            internal int N, Index, Mask, Advanced;
            internal bool Blocked;
            internal bool Sample;
            internal Transform Child;
            internal Vector2 Offset, Cropped, Multiplier;
            internal Vector3 ChildPosition;
            internal DecPlacementType Placement;
            internal Vector2 Position, Scale;
            internal float Angle;
        }
        private sealed class RefEq : IEqualityComparer<scrDecoration>
        {
            public bool Equals(scrDecoration a, scrDecoration b) => ReferenceEquals(a, b);
            public int GetHashCode(scrDecoration d) => RuntimeHelpers.GetHashCode(d);
        }
        private static readonly Dictionary<scrDecoration, State> owned = new Dictionary<scrDecoration, State>(new RefEq());
        private static readonly Dictionary<scrDecoration, State> candidates = new Dictionary<scrDecoration, State>(new RefEq());
        private static readonly List<State> active = new List<State>();
        private static readonly Stack<State> pool = new Stack<State>();
        internal static bool Enabled, Dirty;
        private static bool verify;
        private static long serial, admissions, frames, decorations, records, syncs, restores, fallbacks, checkedValues, mismatches;
        private static readonly long[] reasons = new long[10];
        private static string compatibility = "off", first = "none";
        internal static int Count => active.Count;
        internal static long NextOrder() => Enabled ? ++serial : 0;
        internal static void Configure(string path)
        {
            RestoreAll();
            Enabled = File.Exists(Path.Combine(path, "decoengine-on"));
            Dirty = Enabled;
            compatibility = Enabled ? "pending" : "off";
            if (!Enabled) return;
            // Bypassed setters must have no foreign hooks. Direct field consumers
            // in PACL2 may observe inter-decoration ordering: conservatively exclude.
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = a.GetName().Name;
                if (name.IndexOf("PACL", StringComparison.OrdinalIgnoreCase) >= 0)
                { compatibility = "excluded assembly " + name; return; }
            }
            foreach (var type in new[] { typeof(scrDecoration), typeof(scrVisualDecoration) })
            foreach (var m in type.GetMethods(AccessTools.all))
            {
                if (m.Name != "SetPosition" && m.Name != "SetPositionX" && m.Name != "SetPositionY"
                    && m.Name != "SetRotation" && m.Name != "SetScale" && m.Name != "UpdateScreenClamp"
                    && m.Name != "SetColor" && m.Name != "SetOpacity" && m.Name != "ApplyColor") continue;
                var info = Harmony.GetPatchInfo(m);
                if (info != null) foreach (string id in info.Owners)
                    if (id != Main.Entry.Info.Id && id != "StutterFix.movepos" && id != "StutterFix.fuseprobe")
                    { compatibility = "foreign setter " + id + ":" + m.Name; return; }
            }
            compatibility = "ok";
        }
        private static bool Key(int k) => k == 1 || k == 2 || k == 5 || k == 7 || k == 8;
        private static bool Independent(DecoAnim.Rec r)
        {
            // ApplyColor IL reads only color/opacity/stickToFloor/parentFloor.
            // stickToFloor is already excluded. Constant positive alpha keeps
            // GetVisible unchanged at every intermediate setter in either order.
            if (!r.Started || !Finite(r)) return false;
            if (r.Key == 9) return Same(r.CStart.a, r.CEnd.a) && r.CStart.a >= .001f
                && r.CStart.a <= 1f && r.CChange.a == 0f;
            return r.Key == 10 && Same(r.FStart, r.FEnd) && r.FStart >= .001f
                && r.FStart <= 100f && r.FChange == 0f;
        }
        private static bool Allowed => Enabled && compatibility == "ok" && DecoAnim.EngineAllowed;
        private static State Rent(scrDecoration d)
        {
            var s = pool.Count == 0 ? new State() : pool.Pop();
            s.D = d; s.N = s.Mask = 0; s.Blocked = false; s.LateEntry = null;
            s.Sample = false;
            return s;
        }
        private static void Recycle(State s)
        {
            Array.Clear(s.Slots, 0, s.Slots.Length); s.D = null; s.Child = null; s.LateEntry = null; s.N = 0; pool.Push(s);
        }
        // Run AFTER the original first Step/Startup, before Compact. Startup for
        // scale must see preceding setters; it cannot be hoisted across records.
        internal static void Admit()
        {
            if (!Enabled || !Dirty) return;
            Dirty = false;
            if (!Allowed) return;
            var list = DecoAnim.EngineRecords;
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                if (!r.Running || (object)r.D == null) continue;
                if (!candidates.TryGetValue(r.D, out var s)) { s = Rent(r.D); candidates.Add(r.D, s); }
                if (!Key(r.Key)) { if (!Independent(r)) s.Blocked = true; continue; }
                if (!r.Started || r.Shadow != null || r.BatchSlot >= 0 || !Finite(r)) { s.Blocked = true; continue; }
                int bit = 1 << r.Key;
                if ((s.Mask & bit) != 0 || s.N == 5) { s.Blocked = true; continue; }
                s.Mask |= bit; s.Slots[s.N++] = r;
            }
            foreach (var pair in candidates)
            {
                var s = pair.Value;
                TightLate.Entry e;
                try
                {
                    e = s.Blocked || s.N == 0 ? null : TightLate.EngineEntry(s.D);
                    if (e == null || !StableChild(s.D)) { reasons[8]++; Recycle(s); continue; }
                }
                catch { reasons[8]++; Recycle(s); continue; }
                s.LateEntry = e; s.Index = active.Count; e.Engine = s;
                s.Child = s.D.childTransform; s.Offset = s.D.pivotOffsetVec; s.Cropped = s.D.croppedPivotOffset;
                s.Multiplier = s.D.trueScaleMultiplier; s.Placement = s.D.placementType;
                s.ChildPosition = s.Child.localPosition;
                s.Sample = (++admissions & 15) == 0;
                s.Position = s.D.pivotPosVec; s.Scale = s.D.scaleVec; s.Angle = s.D.rotAngle;
                active.Add(s); owned.Add(s.D, s);
                for (int j = 0; j < s.N; j++) s.Slots[j].Engine = s;
            }
            candidates.Clear();
            int w = 0;
            for (int i = 0; i < list.Count; i++) if (list[i].Engine == null) list[w++] = list[i];
            if (w < list.Count) list.RemoveRange(w, list.Count - w);
            DecoAnim.EngineDirty();
        }
        private static bool Finite(DecoAnim.Rec r) => !float.IsNaN(r.Pos) && !float.IsInfinity(r.Pos)
            && !float.IsNaN(r.Dur) && !float.IsInfinity(r.Dur) && r.Dur > 0f;
        private static bool StableChild(scrDecoration d)
        {
            if (ADOBase.editor != null || (object)d.childTransform == null || d.parallax.clampToScreen
                || d.placementType == DecPlacementType.Camera || d.placementType == DecPlacementType.CameraAspect) return false;
            Vector3 expected = (d.pivotOffsetVec + d.croppedPivotOffset) / d.trueScaleMultiplier;
            var actual = d.childTransform.localPosition;
            return Same(actual.x, expected.x) && Same(actual.y, expected.y) && Same(actual.z, expected.z);
        }
        private static bool Same(float a, float b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);
        private static bool Same(Vector2 a, Vector2 b) => Same(a.x, b.x) && Same(a.y, b.y);
        private static bool Same(Vector3 a, Vector3 b) => Same(a.x, b.x) && Same(a.y, b.y) && Same(a.z, b.z);
        internal static bool ShouldVerify(State s) => verify && s.Sample;
        private static bool Stable(State s) => ReferenceEquals(s.Child, s.D.childTransform)
            && Same(s.Offset, s.D.pivotOffsetVec) && Same(s.Cropped, s.D.croppedPivotOffset)
            && Same(s.Multiplier, s.D.trueScaleMultiplier) && s.Placement == s.D.placementType
            && Same(s.ChildPosition, s.Child.localPosition);
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Update(float td)
        {
            if (active.Count == 0) return;
            if (!Allowed || ADOBase.editor != null) { RestoreAll(); return; }
            frames++;
            for (int i = 0; i < active.Count;)
            {
                var s = active[i];
                int reason;
                try
                {
                    reason = TightLate.EngineReason(s.LateEntry);
                    if (reason == 0 && !Stable(s)) reason = 9;
                }
                catch
                {
                    s.LateEntry.EngineRejected = true; reasons[7]++; Release(s); fallbacks++; continue;
                }
                if (reason != 0) { reasons[reason]++; Release(s); fallbacks++; continue; }
                try { Advance(s, td); }
                catch (Exception ex)
                {
                    // No replay of a partially advanced frame. Publish current
                    // state and return all remaining records for the next Update.
                    // The original loop runs after us. A partially stepped record
                    // must not receive td twice during recovery.
                    for (int j = 0; j < s.N; j++)
                        if ((s.Advanced & (1 << j)) != 0 && s.Slots[j] != null) s.Slots[j].EngineSkipFrame = Time.frameCount;
                    Publish(s);
                    s.LateEntry.EngineRejected = true;
                    Release(s); fallbacks++;
                    Main.Entry.Logger.Log("[decoengine] fallback " + ex.GetType().Name); continue;
                }
                decorations++;
                if (s.LateEntry.EngineRejected)
                {
                    for (int j = 0; j < s.N; j++) s.Slots[j].EngineSkipFrame = Time.frameCount;
                    Release(s); fallbacks++; continue;
                }
                if (s.N == 0) { Remove(s); continue; }
                i++;
            }
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Advance(State s, float td)
        {
            // Read once to preserve immediate effects/other Update writers.
            s.Position = s.D.pivotPosVec; s.Angle = s.D.rotAngle; s.Scale = s.D.scaleVec;
            s.Advanced = 0;
            var reference = verify && s.Sample ? Reference(s, td) : default(Expected);
            long previousMismatch = mismatches;
            int originalCount = s.N;
            bool hasEase = false;
            float lastPos = 0, lastDur = 0, lastOver = 0, lastPeriod = 0, lastEase = 0;
            Ease lastType = default(Ease);
            for (int j = 0; j < s.N; j++)
            {
                var r = s.Slots[j];
                if (r == null) continue;
                if (!r.Running) { r.Engine = null; r.InList = false; s.Slots[j] = null; continue; }
                float to = r.Pos + td; bool done = to >= r.Dur;
                if (done) to = r.Dur;
                float e;
                if (hasEase && lastType == r.E && Same(lastPos, to) && Same(lastDur, r.Dur)
                    && Same(lastOver, r.Over) && Same(lastPeriod, r.Period)) e = lastEase;
                else
                {
                    e = ZeroTween.Eval(r.E, to, r.Dur, r.Over, r.Period);
                    hasEase = true; lastType = r.E; lastPos = to; lastDur = r.Dur;
                    lastOver = r.Over; lastPeriod = r.Period; lastEase = e;
                }
                r.Pos = to; r.Stepped = true;
                s.Advanced |= 1 << j;
                if (r.Key == 7 || r.Key == 8)
                {
                    var v = s.Scale;
                    if (r.Key == 7) v.x = r.VStart.x + r.VChange.x * e;
                    else v.y = r.VStart.y + r.VChange.y * e;
                    r.VLast = v; s.Scale = v;
                }
                else
                {
                    r.FLast = r.FStart + r.FChange * e;
                    float value = done ? r.FEnd : r.FLast;
                    if (r.Key == 1) s.Position.x = value;
                    else if (r.Key == 2) s.Position.y = value;
                    else s.Angle = value;
                }
                DecoAnim.Steps++;
                if (done) { r.Running = false; r.Engine = null; r.InList = false; DecoAnim.Completed++; s.Slots[j] = null; }
                if (verify && s.Sample) CheckValue(r, reference, j);
            }
            int w = 0;
            for (int j = 0; j < s.N; j++) if (s.Slots[j] != null) s.Slots[w++] = s.Slots[j];
            Array.Clear(s.Slots, w, s.N - w); s.N = w;
            Publish(s);
            if (verify && s.Sample)
            {
                Check(Same(s.D.pivotPosVec, reference.Position) && Same(s.D.scaleVec, reference.Scale)
                    && Same(s.D.rotAngle, reference.Angle), "fields");
                if (mismatches != previousMismatch)
                {
                    s.Position = reference.Position; s.Scale = reference.Scale; s.Angle = reference.Angle;
                    Publish(s); s.LateEntry.EngineRejected = true;
                }
            }
            records += originalCount;
        }
        private static void Publish(State s)
        {
            s.D.pivotPosVec = s.Position; s.D.rotAngle = s.Angle; s.D.scaleVec = s.Scale; syncs++;
        }
        // Independent Step/ApplyE/OnComplete arithmetic, no game setters/field stores.
        // This oracle advances its own clock and invokes Eval separately.
        private struct Expected
        {
            internal Vector2 Position, Scale;
            internal float Angle;
            internal RecordExpected A, B, C, D, E;
            internal RecordExpected Get(int i) => i == 0 ? A : i == 1 ? B : i == 2 ? C : i == 3 ? D : E;
            internal void Set(int i, RecordExpected r) { if (i == 0) A = r; else if (i == 1) B = r; else if (i == 2) C = r; else if (i == 3) D = r; else E = r; }
        }
        private struct RecordExpected { internal float Pos, F; internal Vector2 V; internal bool Done; }
        private static Expected Reference(State s, float td)
        {
            var x = new Expected { Position = s.D.pivotPosVec, Scale = s.D.scaleVec, Angle = s.D.rotAngle };
            for (int j = 0; j < s.N; j++)
            {
                var r = s.Slots[j];
                float pos = r.Pos + td; bool done = false;
                if (pos >= r.Dur) { pos = r.Dur; done = true; }
                float e = ZeroTween.Eval(r.E, pos, r.Dur, r.Over, r.Period);
                var v = x.Scale; float f = 0f;
                if (r.Key == 7) { v.x = r.VStart.x + r.VChange.x * e; x.Scale = v; }
                else if (r.Key == 8) { v.y = r.VStart.y + r.VChange.y * e; x.Scale = v; }
                else
                {
                    f = r.FStart + r.FChange * e;
                    float final = done ? r.FEnd : f;
                    if (r.Key == 1) x.Position.x = final; else if (r.Key == 2) x.Position.y = final; else x.Angle = final;
                }
                x.Set(j, new RecordExpected { Pos = pos, F = f, V = v, Done = done });
            }
            return x;
        }
        private static void CheckValue(DecoAnim.Rec r, Expected reference, int index)
        {
            var x = reference.Get(index);
            Check(Same(r.Pos, x.Pos) && r.Stepped && r.Running == !x.Done
                && (r.Key == 7 || r.Key == 8 ? Same(r.VLast, x.V) : Same(r.FLast, x.F)), "key=" + r.Key);
        }
        private static void Check(bool same, string field)
        {
            checkedValues++;
            if (same) return;
            mismatches++;
            if (first == "none") first = field;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Late(State s, Vector3 cam)
        {
            long previousMismatch = mismatches;
            // Phase 1 fields are authoritative if another effect ran after Update.
            s.Position = s.D.pivotPosVec; s.Angle = s.D.rotAngle; s.Scale = s.D.scaleVec;
            TightLate.EngineApply(s.LateEntry, cam, s.Position, s.Angle, s.Scale);
            if (mismatches != previousMismatch) { s.LateEntry.EngineRejected = true; Release(s); fallbacks++; }
        }
        // Called inside the shared Apply before Transform setters. Compare setter
        // INPUTS: reading rotation back would include Unity's normalization.
        internal static void CheckLate(State s, Vector3 camera, Vector2 start, Vector3 camStart, Vector2 end,
            Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (!verify || !s.Sample) return;
            var d = s.D; var p = d.parallax;
            Vector2 pos = d.pivotPosVec + Vector2.zero;
            Vector3 pos3 = pos;
            Vector2 expectedStart = pos3;
            Vector3 expectedCam = pos;
            Vector2 u = (Vector2)(camera - expectedCam) * new Vector2(p.multiplier_x, p.multiplier_y) + expectedStart;
            Vector2 camScale = 1f * (d.scaleMultiplier * d.textureScaleMultiplier);
            Vector2 offset = d.parallaxOffset * camScale / d.textureScaleMultiplier;
            Vector2 expectedEnd = u + offset;
            var np = new Vector3(expectedEnd.x, expectedEnd.y, 0f);
            var nr = Quaternion.Euler(0f, 0f, d.startRot + d.rotAngle);
            Vector2 v = d.scaleVec * camScale * (Vector2)Vector3.one;
            var ns = new Vector3(v.x, v.y, 1f);
            Check(Same(start, expectedStart) && Same(camStart.x, expectedCam.x) && Same(camStart.y, expectedCam.y)
                && Same(camStart.z, expectedCam.z) && Same(end, expectedEnd), "parallax");
            Check(Same(position.x, np.x) && Same(position.y, np.y) && Same(position.z, np.z)
                && Same(rotation.x, nr.x) && Same(rotation.y, nr.y) && Same(rotation.z, nr.z) && Same(rotation.w, nr.w)
                && Same(scale.x, ns.x) && Same(scale.y, ns.y) && Same(scale.z, ns.z), "transform-input");
        }
        internal static void ReleaseDecoration(scrDecoration d)
        {
            if (active.Count != 0 && (object)d != null && owned.TryGetValue(d, out var s)) Release(s);
        }
        internal static void ExitDecoration(scrDecoration d, int reason)
        { if (active.Count == 0) return; reasons[reason]++; fallbacks++; ReleaseDecoration(d); }
        internal static void ReleaseRecord(DecoAnim.Rec r) { if (r.Engine != null) Release(r.Engine); }
        private static void Release(State s)
        {
            restores++;
            // Fields already published. Restore original creation order only on
            // ownership exit, never during either hot loop.
            for (int j = 0; j < s.N; j++)
            {
                var r = s.Slots[j]; if (r == null) continue; r.Engine = null;
                if (r.Running) { r.InList = true; DecoAnim.EngineRecords.Add(r); }
                else r.InList = false;
            }
            Remove(s); DecoAnim.EngineRecords.Sort((a, b) => a.EngineOrder.CompareTo(b.EngineOrder));
            DecoAnim.EngineDirty(); Dirty = true;
        }
        private static void Remove(State s)
        {
            s.LateEntry.Engine = null; owned.Remove(s.D);
            int i = s.Index, last = active.Count - 1;
            active[i] = active[last]; active[i].Index = i; active.RemoveAt(last); Recycle(s);
        }
        internal static void RestoreAll() { while (active.Count != 0) Release(active[active.Count - 1]); }
        internal static void Forget() { RestoreAll(); owned.Clear(); candidates.Clear(); pool.Clear(); Dirty = Enabled; }
        internal static void DropLevel() { if (active.Count != 0) DecoAnim.DropAll(); Forget(); }
        internal static void Uninstall() { RestoreAll(); Forget(); Enabled = verify = false; }
        internal static void Collect(List<scrDecoration> into, HashSet<scrDecoration> seen)
        { foreach (var s in active) if (seen.Add(s.D)) into.Add(s.D); }
        internal static string Command(string arg)
        {
            if (arg == "verify") verify = true;
            if (arg == "verify off") verify = false;
            if (arg == "bench") return Bench();
            return "enabled=" + Enabled + " compatibility=" + compatibility + " owned=" + active.Count
                + " frames=" + frames + " deco/frame=" + (frames == 0 ? 0 : decorations / (double)frames).ToString("F2")
                + " rec/frame=" + (frames == 0 ? 0 : records / (double)frames).ToString("F2")
                + " admissions=" + admissions + " sync=" + syncs + " restores=" + restores + " fallback=" + fallbacks + " reasons=" + string.Join("/", reasons)
                + " verify=" + verify + " checks=" + checkedValues + " mismatch=" + mismatches + " first=" + first;
        }
        private static string Bench()
        {
            if (Hitch.Playing || ADOBase.editor != null) return "bench refused outside menu";
            const int n = 2150, repeat = 200;
            var root = new GameObject("decoengine-bench");
            var states = new State[n];
            bool savedVerify = verify;
            long savedSteps = DecoAnim.Steps, savedCompleted = DecoAnim.Completed, savedSync = syncs, savedRecords = records;
            long savedWrites = TightLate.writes, savedP = TightLate.skipPos, savedR = TightLate.skipRot, savedS = TightLate.skipScale;
            try
            {
                verify = false;
                int[] keys = { 1, 2, 5, 7, 8 };
                for (int i = 0; i < n; i++)
                {
                    var go = new GameObject("de" + i); go.transform.SetParent(root.transform, false);
                    var d = (scrDecoration)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(scrVisualDecoration));
                    var p = (scrParallax)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(scrParallax));
                    d.parallax = p; d.pivotTrans = go.transform; d.scaleVec = Vector2.one;
                    var child = new GameObject("child"); child.transform.SetParent(go.transform, false); d.childTransform = child.transform;
                    d.textureScaleMultiplier = Vector2.one; d.scaleMultiplier = 1f;
                    p.multiplier_x = .3f; p.multiplier_y = .6f;
                    var s = states[i] = new State { D = d, N = 5, LateEntry = TightLate.EngineBenchEntry(d),
                        Child = child.transform, ChildPosition = child.transform.localPosition, Multiplier = d.trueScaleMultiplier,
                        Placement = d.placementType };
                    for (int k = 0; k < 5; k++) s.Slots[k] = new DecoAnim.Rec { D = d, Key = keys[k], Started = true,
                        Dur = 1000f, E = Ease.OutCubic, Over = 1.70158f, FStart = i * .01f, FChange = 2f,
                        FEnd = i * .01f + 2f, VStart = Vector2.one, VChange = new Vector2(.5f, .7f) };
                }
                Vector3 cam = new Vector3(2, 3, -10);
                for (int r = 0; r < 5; r++) for (int i = 0; i < n; i++) { Advance(states[i], 1f/120); Late(states[i], cam); }
                long update = 0, late = 0;
                for (int r = 0; r < repeat; r++)
                {
                    long t = Stopwatch.GetTimestamp();
                    for (int i = 0; i < n; i++)
                    {
                        if (TightLate.EngineBenchReason(states[i].LateEntry, root.transform) != 0 || !Stable(states[i]))
                            return "bench guard failed; no timing accepted";
                        Advance(states[i], 1f/120);
                    }
                    update += Stopwatch.GetTimestamp() - t;
                    cam.x = 2f + r * .01f;
                    t = Stopwatch.GetTimestamp();
                    for (int i = 0; i < n; i++)
                    {
                        if (TightLate.EngineBenchReason(states[i].LateEntry, root.transform) != 0)
                            return "bench late guard failed; no timing accepted";
                        Late(states[i], cam);
                    }
                    late += Stopwatch.GetTimestamp() - t;
                }
                double divisor = Stopwatch.Frequency * (double)repeat * n / 1e6;
                return "N=2150 R=200 update-us/deco=" + (update/divisor).ToString("F4")
                    + " late-us/deco=" + (late/divisor).ToString("F4") + " total-us/deco=" + ((update+late)/divisor).ToString("F4")
                    + " ms/frame=" + ((update+late)/divisor*n/1000).ToString("F3")
                    + "; includes eligibility twice + child guard + 5 Rec + publication + shared TightLate Transform setters; excludes admission/fallback/manager-list overhead; NOT live A/B evidence";
            }
            finally
            {
                verify = savedVerify; DecoAnim.Steps = savedSteps; DecoAnim.Completed = savedCompleted; syncs = savedSync; records = savedRecords;
                TightLate.writes = savedWrites; TightLate.skipPos = savedP; TightLate.skipRot = savedR; TightLate.skipScale = savedS;
                UnityEngine.Object.Destroy(root);
            }
        }
    }
    internal static partial class DecoAnim
    {
        internal static List<Rec> EngineRecords => recs;
        internal static void EngineDirty() { fastDirty = true; }
        internal static bool EngineAllowed => Active && !batchOn && !GroupEnabled && !NativeMotion.Enabled;
    }
}
