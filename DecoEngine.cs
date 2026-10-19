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
            internal bool Blocked; internal int Why;
            internal bool Sample;
            internal bool Pending;
            internal DecoAnim.Rec Color, Opacity;
            internal Transform Child;
            internal Vector2 Offset, Cropped, Multiplier;
            internal Vector3 ChildPosition;
            internal int ChildCheckFrame;
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
        private static readonly long[] rej = new long[9]; // 1색무효 2불투명도무효 3기타키 4미시작/무한 5키중복 6빈 7엔트리없음 8자식변경
        private static readonly long[] missing = new long[8];
        private static long alphaFallbacks, replacements, attachments, allSteps, stepStart;
        private static long colorEvents, opacityEvents, colorZero, opacityZero;
        private static long hiddenAdmissions, easeHits, easeMisses, slotCompactions, childReads;
        private static bool stats;
        private static int lastMissingFrame = -100;
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
            // Color setters stay in original order. Before moving any transform
            // ahead of them, AlphaSafe checks every possible intermediate factor.
            return (r.Key == 9 || r.Key == 10) && Finite(r);
        }
        private static bool Positive(float v) => v >= .000001f && v <= 100f;
        private static bool AlphaRecordSafe(DecoAnim.Rec r, float td)
        {
            if (r == null || !r.Running) return true;
            if (!Independent(r)) return false;
            float p = r.Pos + td;
            bool done = p >= r.Dur;
            if (done) p = r.Dur;
            float start = r.Key == 9 ? r.CStart.a : r.FStart;
            float end = r.Key == 9 ? r.CEnd.a : r.FEnd;
            float change = r.Started ? (r.Key == 9 ? r.CChange.a : r.FChange) : end - start;
            if (change == 0f && Same(start, end)) return Positive(start);
            float e = ZeroTween.Eval(r.E, p, r.Dur, r.Over, r.Period);
            // Same scalar expression/store as ApplyE, including the OnUpdate
            // value BEFORE OnComplete's exact end store (overshoot is allowed).
            float value = start + change * e;
            return Positive(value) && (!done || Positive(end));
        }
        private static bool AlphaSafe(State s, float td) => Positive(s.D.color.a) && Positive(s.D.opacity)
            && AlphaRecordSafe(s.Color, td) && AlphaRecordSafe(s.Opacity, td);
        internal static void BeginFrame() { if (Enabled) stepStart = DecoAnim.Steps; }
        internal static void EndFrame() { if (Enabled) { frames++; allSteps += DecoAnim.Steps - stepStart; } }
        internal static void AlphaEvent(DecoAnim.Rec r)
        {
            if (!Enabled) return;
            if (r.Key == 9) { colorEvents++; if (r.CStart.a == 0f || r.CEnd.a == 0f) colorZero++; }
            else { opacityEvents++; if (r.FStart == 0f || r.FEnd == 0f) opacityZero++; }
        }
        internal static void AlphaWillSet(DecoAnim.Rec r, float next)
        {
            if (Enabled && Positive(next) && !Positive(r.Key == 9 ? r.D.color.a : r.D.opacity)) Dirty = true;
        }
        // Effect-boundary lookup only. A newly created transform runs Startup
        // and its first Step in the original loop, then joins the retained state.
        internal static State PrepareNew(scrDecoration d, int key)
        {
            if (!Enabled || active.Count == 0 || !owned.TryGetValue(d, out var s)) return null;
            if (!Key(key) && key != 9 && key != 10) { Release(s); return null; }
            s.Pending = true;
            return s;
        }
        internal static void BindNew(State s, DecoAnim.Rec r)
        {
            if (s == null) return;
            if (r.Key == 9) s.Color = r;
            if (r.Key == 10) s.Opacity = r;
        }
        internal static void DetachRecord(DecoAnim.Rec r)
        {
            var s = r.Engine;
            if (s == null) return;
            // Kill(true) still calls the original Apply/OnUpdate/OnComplete.
            // Keep the decoration and other slots; no publication or sorting.
            for (int i = 0; i < s.N; i++) if (ReferenceEquals(s.Slots[i], r))
            {
                for (int j = i + 1; j < s.N; j++) s.Slots[j - 1] = s.Slots[j];
                s.Slots[--s.N] = null; s.Mask &= ~(1 << r.Key); break;
            }
            r.Engine = null; r.InList = false; replacements++;
        }
        private static bool Allowed => Enabled && compatibility == "ok" && DecoAnim.EngineAllowed;
        private static State Rent(scrDecoration d)
        {
            var s = pool.Count == 0 ? new State() : pool.Pop();
            s.D = d; s.N = s.Mask = 0; s.Blocked = false; s.Why = 0; s.LateEntry = null;
            s.Sample = s.Pending = false; s.Color = s.Opacity = null;
            return s;
        }
        private static void Recycle(State s)
        {
            Array.Clear(s.Slots, 0, s.Slots.Length); s.D = null; s.Child = null; s.LateEntry = null;
            s.Color = s.Opacity = null; s.N = 0; pool.Push(s);
        }
        // Run AFTER the original first Step/Startup, before Compact. Startup for
        // scale must see preceding setters; it cannot be hoisted across records.
        internal static void Admit()
        {
            if (!Enabled || !Dirty) return;
            Dirty = false;
            if (!Allowed) return;
            bool retryMissing = lastMissingFrame != Time.frameCount - 1 && lastMissingFrame != Time.frameCount;
            bool sawMissing = false;
            var list = DecoAnim.EngineRecords;
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                if (!r.Running || (object)r.D == null) continue;
                if (owned.TryGetValue(r.D, out var existing))
                {
                    if (!Key(r.Key))
                    {
                        if (Independent(r)) { if (r.Key == 9) existing.Color = r; else existing.Opacity = r; }
                        else existing.Blocked = true;
                        continue;
                    }
                    if (!r.Started || r.Shadow != null || r.BatchSlot >= 0 || !Finite(r)
                        || (existing.Mask & (1 << r.Key)) != 0 || !Stable(existing))
                    { existing.Blocked = true; continue; }
                    int at = existing.N++;
                    while (at > 0 && existing.Slots[at - 1].EngineOrder > r.EngineOrder)
                    { existing.Slots[at] = existing.Slots[at - 1]; at--; }
                    existing.Slots[at] = r; existing.Mask |= 1 << r.Key; r.Engine = existing; attachments++;
                    continue;
                }
                // Hidden records remain in their original loop. Avoid renting a
                // candidate, hashing it again and running full eligibility only
                // to report missing[4]. AlphaWillSet schedules a fresh admission.
                if (!TightLate.EngineAdmissionVisible(r.D)) { hiddenAdmissions++; continue; }
                if (!candidates.TryGetValue(r.D, out var s)) { s = Rent(r.D); candidates.Add(r.D, s); }
                if (!Key(r.Key)) { if (!Independent(r)) { s.Blocked = true; if (s.Why == 0) s.Why = r.Key == 9 ? 1 : r.Key == 10 ? 2 : 3; }
                    else if (r.Key == 9) s.Color = r; else s.Opacity = r; continue; }
                if (!r.Started || r.Shadow != null || r.BatchSlot >= 0 || !Finite(r)) { s.Blocked = true; if (s.Why == 0) s.Why = 4; continue; }
                int bit = 1 << r.Key;
                if ((s.Mask & bit) != 0 || s.N == 5) { s.Blocked = true; if (s.Why == 0) s.Why = 5; continue; }
                s.Mask |= bit; s.Slots[s.N++] = r;
            }
            foreach (var pair in candidates)
            {
                var s = pair.Value;
                TightLate.Entry e;
                try
                {
                    int why = 0;
                    e = s.Blocked || s.N == 0 ? null : TightLate.EngineEntry(s.D, out why);
                    if (why != 0) missing[why]++;
                    if (why == 2) sawMissing = true;
                    if (e == null || !StableChild(s.D))
                    { reasons[8]++; rej[s.Blocked ? s.Why : s.N == 0 ? 6 : e == null ? 7 : 8]++; Recycle(s); continue; }
                }
                catch { reasons[8]++; Recycle(s); continue; }
                s.LateEntry = e; s.Index = active.Count; e.Engine = s;
                s.Child = s.D.childTransform; s.Offset = s.D.pivotOffsetVec; s.Cropped = s.D.croppedPivotOffset;
                s.Multiplier = s.D.trueScaleMultiplier; s.Placement = s.D.placementType;
                s.ChildPosition = s.Child.localPosition;
                s.ChildCheckFrame = Time.frameCount + 1 + (int)(admissions & 15);
                s.Sample = (++admissions & 15) == 0;
                s.Position = s.D.pivotPosVec; s.Scale = s.D.scaleVec; s.Angle = s.D.rotAngle;
                active.Add(s); owned.Add(s.D, s);
                for (int j = 0; j < s.N; j++) s.Slots[j].Engine = s;
            }
            candidates.Clear();
            // One follow-up for the first manager encounter. Orphan decorations
            // must not force the full admission scan on every later frame.
            if (sawMissing) { if (retryMissing) Dirty = true; lastMissingFrame = Time.frameCount; }
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var s = active[i]; s.Pending = false;
                if (s.Blocked) Release(s); else if (s.N == 0) Remove(s);
            }
            int w = 0;
            for (int i = 0; i < list.Count; i++)
                if (list[i].Engine == null) list[w++] = list[i]; else list[i].InList = false;
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
        private static bool Stable(State s)
        {
            if (!ReferenceEquals(s.Child, s.D.childTransform)
                || !Same(s.Offset, s.D.pivotOffsetVec) || !Same(s.Cropped, s.D.croppedPivotOffset)
                || !Same(s.Multiplier, s.D.trueScaleMultiplier) || s.Placement != s.D.placementType) return false;
            // No managed-field proof for arbitrary external Transform writers.
            // Spread the native guard over 16 frames; verify retains strict
            // per-frame checking. A native-only mutation can escape for 15 frames.
            if (!verify && unchecked(Time.frameCount - s.ChildCheckFrame) < 0) return true;
            childReads++;
            s.ChildCheckFrame = unchecked(Time.frameCount + 16);
            return Same(s.ChildPosition, s.Child.localPosition);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Update(float td)
        {
            if (active.Count == 0) return;
            if (!Allowed || ADOBase.editor != null) { RestoreAll(); return; }
            for (int i = 0; i < active.Count;)
            {
                var s = active[i];
                int reason;
                try
                {
                    reason = TightLate.EngineReason(s.LateEntry);
                    if (reason == 0 && !Stable(s)) reason = 9;
                    if (reason == 0 && !AlphaSafe(s, td)) { alphaFallbacks++; reason = 3; }
                }
                catch
                {
                    s.LateEntry.EngineRejected = true; reasons[7]++; Release(s); fallbacks++; continue;
                }
                if (reason != 0) { reasons[reason]++; Release(s); fallbacks++; continue; }
                try { if (s.N != 0) TightLate.EngineDefer(s.LateEntry); Advance(s, td); }
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
                if (s.N == 0 && !s.Pending) { Remove(s); continue; }
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
            bool compact = false;
            bool countEase = stats;
            int hits = 0, misses = 0;
            float lastPos = 0, lastDur = 0, lastOver = 0, lastPeriod = 0, lastEase = 0;
            Ease lastType = default(Ease);
            for (int j = 0; j < s.N; j++)
            {
                var r = s.Slots[j];
                if (r == null) continue;
                if (!r.Running) { r.Engine = null; r.InList = false; s.Slots[j] = null; compact = true; continue; }
                float to = r.Pos + td; bool done = to >= r.Dur;
                if (done) to = r.Dur;
                float e;
                if (hasEase && lastType == r.E && Same(lastPos, to) && Same(lastDur, r.Dur)
                    && Same(lastOver, r.Over) && Same(lastPeriod, r.Period)) { e = lastEase; if (countEase) hits++; }
                else
                {
                    e = ZeroTween.Eval(r.E, to, r.Dur, r.Over, r.Period);
                    if (countEase) misses++;
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
                if (done) { r.Running = false; r.Engine = null; r.InList = false; DecoAnim.Completed++; s.Slots[j] = null; compact = true; }
                if (verify && s.Sample) CheckValue(r, reference, j);
            }
            if (countEase) { easeHits += hits; easeMisses += misses; }
            if (compact)
            {
                slotCompactions++;
                int w = 0, mask = 0;
                for (int j = 0; j < s.N; j++)
                {
                    var r = s.Slots[j]; if (r == null) continue;
                    s.Slots[w++] = r; mask |= 1 << r.Key;
                }
                Array.Clear(s.Slots, w, s.N - w); s.N = w; s.Mask = mask;
            }
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
                if (r.Running) { if (!r.InList) { r.InList = true; InsertOrdered(DecoAnim.EngineRecords, r); } }
                else r.InList = false;
            }
            Remove(s);
            DecoAnim.EngineDirty(); Dirty = true;
        }
        // EngineRecords stays sorted by EngineOrder (creation order); re-entering records
        // are inserted by binary search instead of re-sorting the whole list (was ~17% of the main thread).
        private static void InsertOrdered(List<DecoAnim.Rec> list, DecoAnim.Rec r)
        {
            int lo = 0, hi = list.Count;
            while (lo < hi) { int mid = (lo + hi) >> 1; if (list[mid].EngineOrder <= r.EngineOrder) lo = mid + 1; else hi = mid; }
            list.Insert(lo, r);
        }
        private static void Remove(State s)
        {
            s.LateEntry.Engine = null; owned.Remove(s.D);
            int i = s.Index, last = active.Count - 1;
            active[i] = active[last]; active[i].Index = i; active.RemoveAt(last); Recycle(s);
        }
        internal static void RestoreAll() { while (active.Count != 0) Release(active[active.Count - 1]); }
        internal static void Forget() { RestoreAll(); owned.Clear(); candidates.Clear(); pool.Clear(); lastMissingFrame = -100; Dirty = Enabled; }
        internal static void DropLevel() { if (active.Count != 0) DecoAnim.DropAll(); Forget(); }
        internal static void Uninstall() { RestoreAll(); Forget(); Enabled = verify = stats = false; }
        internal static void Collect(List<scrDecoration> into, HashSet<scrDecoration> seen)
        { foreach (var s in active) if (seen.Add(s.D)) into.Add(s.D); }
        internal static string Command(string arg)
        {
            if (arg == "verify") verify = true;
            if (arg == "verify off") verify = false;
            if (arg == "stats on") stats = true;
            if (arg == "stats off") stats = false;
            if (arg == "bench") return Bench(0);
            if (arg.StartsWith("bench ", StringComparison.Ordinal))
                return int.TryParse(arg.Substring(6), out int percent) && percent >= 0 && percent <= 100
                    ? Bench(percent) : "usage: bench [alpha-transition-percent 0..100]";
            int currentRecords = 0; foreach (var s in active) currentRecords += s.N;
            return "enabled=" + Enabled + " compatibility=" + compatibility + " owned=" + active.Count
                + " owned-rec=" + currentRecords + " owned-steps=" + records + " all-steps=" + allSteps
                + " ownership%=" + (allSteps == 0 ? 0 : 100.0 * records / allSteps).ToString("F2")
                + " frames=" + frames + " deco-frames=" + decorations + " deco/frame=" + (frames == 0 ? 0 : decorations / (double)frames).ToString("F2")
                + " rec/frame=" + (frames == 0 ? 0 : records / (double)frames).ToString("F2")
                + " admissions=" + admissions + " sync=" + syncs + " restores=" + restores + " fallback=" + fallbacks + " reasons=" + string.Join("/", reasons) + " rej=" + string.Join("/", rej)
                + " missing=" + string.Join("/", missing) + " alpha-fallback=" + alphaFallbacks
                + " missing-eligible=" + TightLate.EngineAdmissionReasons
                + " hidden-admission-records=" + hiddenAdmissions + " child-reads=" + childReads
                + " compact=" + slotCompactions + " stats=" + stats + " ease-hit/miss=" + easeHits + "/" + easeMisses
                + " detach=" + replacements + " attach=" + attachments
                + " late-fixup=" + TightLate.EngineFixups
                + " alpha-events=" + colorEvents + "/" + opacityEvents + " zero-endpoints=" + colorZero + "/" + opacityZero
                + " verify=" + verify + " checks=" + checkedValues + " mismatch=" + mismatches + " first=" + first;
        }
        private static string Bench(int alphaPercent)
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
                    d.color = Color.white; d.opacity = 100f;
                    var child = new GameObject("child"); child.transform.SetParent(go.transform, false); d.childTransform = child.transform;
                    d.textureScaleMultiplier = Vector2.one; d.scaleMultiplier = 1f;
                    p.multiplier_x = .3f; p.multiplier_y = .6f;
                    var s = states[i] = new State { D = d, N = 5, LateEntry = TightLate.EngineBenchEntry(d),
                        Child = child.transform, ChildPosition = child.transform.localPosition, Multiplier = d.trueScaleMultiplier,
                        Placement = d.placementType };
                    if (i * 100 / n < alphaPercent)
                        s.Color = new DecoAnim.Rec { D = d, Key = 9, Started = true, Dur = .001f,
                            CStart = Color.white, CEnd = new Color(1, 1, 1, 0), CChange = new Color(0, 0, 0, -1), E = Ease.Linear };
                    for (int k = 0; k < 5; k++) s.Slots[k] = new DecoAnim.Rec { D = d, Key = keys[k], Started = true,
                        Dur = 1000f, E = Ease.OutCubic, Over = 1.70158f, FStart = i * .01f, FChange = 2f,
                        FEnd = i * .01f + 2f, VStart = Vector2.one, VChange = new Vector2(.5f, .7f) };
                }
                Vector3 cam = new Vector3(2, 3, -10);
                for (int r = 0; r < 5; r++) for (int i = 0; i < n; i++) { Advance(states[i], 1f/120); Late(states[i], cam); }
                long update = 0, late = 0, transitions = 0;
                for (int r = 0; r < repeat; r++)
                {
                    long t = Stopwatch.GetTimestamp();
                    for (int i = 0; i < n; i++)
                    {
                        if (TightLate.EngineBenchReason(states[i].LateEntry, root.transform) != 0 || !Stable(states[i]))
                            return "bench guard failed; no timing accepted";
                        if (!AlphaSafe(states[i], 1f/120)) transitions++;
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
                    + " simulated-alpha%=" + alphaPercent + " transition-checks=" + transitions
                    + " late-us/deco=" + (late/divisor).ToString("F4") + " total-us/deco=" + ((update+late)/divisor).ToString("F4")
                    + " ms/frame=" + ((update+late)/divisor*n/1000).ToString("F3")
                    + "; includes eligibility twice + alpha preflight + child guard + 5 Rec + publication + shared TightLate Transform setters; transitions are preflight-only (still Advance for equal workload), NOT real fallback replay; excludes admission/fallback/deferred-list/manager-list overhead; NOT live A/B evidence";
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
        internal static bool EngineAllowed => Active && !batchOn && !GroupEnabled && !NativeMotion.Enabled
            && MoveApply.LateSkip && MoveApply.DeferRotScale;
    }
}
