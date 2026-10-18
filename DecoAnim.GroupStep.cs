using System;
using System.Collections.Generic;
using System.Diagnostics;
using DG.Tweening;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    internal static partial class DecoAnim
    {
        internal static bool GroupEnabled;
        private static bool groupProbe, groupVerify;
        private static long groupSerial, groupFrames, groupCount, groupRecords, groupTicks, groupChecks, groupMismatch;
        private static long groupVerifyErrors;
        private static int groupAllowed;
        private static string groupCompatibility = "pending";
        private static int groupLastCount;
        private static string groupFirst = "none";
        private static readonly long[] groupSizes = new long[6], groupFallback = new long[6];
        private static readonly Dictionary<string, long> groupOrders = new Dictionary<string, long>();
        private static long groupAdjacent, groupSeparated;
        private static readonly Dictionary<GroupState, CensusEntry> groupCensus = new Dictionary<GroupState, CensusEntry>();
        private struct CensusEntry { internal int Last, Count; internal bool Adjacent; internal string Order; }

        private static void ConfigureGroup(string path)
        {
            GroupEnabled = System.IO.File.Exists(System.IO.Path.Combine(path, "groupstep-on"));   // 2026-10-19 A/B 에서 켜면 느려서(DecoAnim 1.87→2.29ms) 기본 끔. v2 에서 위치 쌍 합치기를 되살린 뒤 다시 판단
            groupProbe = groupVerify = false;
            groupAllowed = 0; groupCompatibility = "pending";
            ResetGroup();
        }
        private static bool GroupKey(int k) => k == 1 || k == 2 || k == 5 || k == 7 || k == 8;
        private static bool GroupNative(Rec r) => NativeMotion.Enabled && !NativeMotion.Failed
            && activeNativeEpoch != 0 && r.NativeEpoch == activeNativeEpoch;

        // Value snapshot: the first record itself is mutated by StepGroup.
        private struct GroupState : IEquatable<GroupState>
        {
            internal scrDecoration D;
            internal int Pos, Dur, Over, Period;
            internal Ease Ease;
            internal bool Started, Running;
            internal GroupState(Rec r)
            {
                D = r.D; Pos = B(r.Pos); Dur = B(r.Dur); Over = B(r.Over); Period = B(r.Period);
                Ease = r.E; Started = r.Started; Running = r.Running;
            }
            internal bool Same(Rec r) => ReferenceEquals(D, r.D) && Pos == B(r.Pos) && Dur == B(r.Dur)
                && Over == B(r.Over) && Period == B(r.Period) && Ease == r.E
                && Started == r.Started && Running == r.Running;
            public bool Equals(GroupState x) => ReferenceEquals(D, x.D) && Pos == x.Pos && Dur == x.Dur
                && Over == x.Over && Period == x.Period && Ease == x.Ease && Started == x.Started && Running == x.Running;
            public override bool Equals(object x) => x is GroupState && Equals((GroupState)x);
            public override int GetHashCode()
            {
                unchecked { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(D) * 397
                    ^ Pos ^ (Dur * 31) ^ (Over * 17) ^ Period ^ (int)Ease; }
            }
        }
        private static bool GroupAllowed()
        {
            if (groupAllowed != 0) return groupAllowed == 1;
            groupAllowed = 2;
            try
            {
                foreach (var m in typeof(scrDecoration).GetMethods(AccessTools.all))
                {
                    if (m.Name != "SetPositionX" && m.Name != "SetPositionY" && m.Name != "SetPosition"
                        && m.Name != "SetRotation" && m.Name != "SetScale") continue;
                    if (!GroupPatchSafe(m)) return false;
                }
                foreach (string name in new[] { "EaseManager", "Bounce", "Flash" })
                {
                    var type = typeof(Ease).Assembly.GetType("DG.Tweening.Core.Easing." + name);
                    if (type == null) { groupCompatibility = "missing easing type " + name; return false; }
                    foreach (var m in type.GetMethods(AccessTools.all))
                        if (m.DeclaringType == type && !GroupPatchSafe(m)) return false;
                }
                groupAllowed = 1; groupCompatibility = "ok";
            }
            catch (Exception ex) { groupCompatibility = ex.GetType().Name; }
            return groupAllowed == 1;
        }
        private static bool GroupPatchSafe(System.Reflection.MethodBase method)
        {
            var info = Harmony.GetPatchInfo(method);
            if (info != null) foreach (var owner in info.Owners)
                if (!owner.StartsWith("StutterFix")) { groupCompatibility = owner + ":" + method.Name; return false; }
            return true;
        }
        private static void GroupReason(int reason) { if (groupProbe) groupFallback[reason]++; }
        private static int GroupCount(int at)
        {
            var first = recs[at];
            if (!GroupKey(first.Key)) { GroupReason(0); return 1; }
            if (GroupNative(first)) { GroupReason(1); return 1; }
            var state = new GroupState(first);
            int n = 1, keys = 1 << first.Key;
            // NewRec appends, FastMove emits each decoration in key order. Never
            // move non-adjacent records across intervening setters or dead slots.
            while (n < 5 && at + n < recs.Count)
            {
                var next = recs[at + n];
                if (!ReferenceEquals(first.D, next.D) || !GroupKey(next.Key)) break;
                if (!state.Same(next)) { GroupReason(2); break; }
                if (GroupNative(next)) { GroupReason(1); break; }
                if ((keys & (1 << next.Key)) != 0) { GroupReason(3); break; }
                keys |= 1 << next.Key; n++;
            }
            if (n == 1) GroupReason(4);
            return n;
        }

        private static void GroupIndividual(Rec r, float td)
        {
            if (!r.Running) return;
            Steps++;
            try { Step(r, td); }
            catch (Exception ex) { r.Running = false; Error(ex); }
        }
        // Only progress fields are changed before setPos. Restore these if the
        // combined setter fails, then let each axis use its own safety boundary.
        private struct PairProgress
        {
            private float pos, change, last;
            private bool started, stepped;
            internal PairProgress(Rec r) { pos = r.Pos; change = r.FChange; last = r.FLast; started = r.Started; stepped = r.Stepped; }
            internal void Restore(Rec r) { r.Pos = pos; r.FChange = change; r.FLast = last; r.Started = started; r.Stepped = stepped; }
        }
        private static void GroupPair(Rec r, Rec n, float td, float to, float e, bool done, bool verify)
        {
            var sr = new PairProgress(r); var sn = new PairProgress(n);
            GroupExpected xr = default, xn = default;
            bool check = verify;
            if (check)
            {
                try
                {
                    xr = new GroupExpected(r, td); xn = new GroupExpected(n, td);
                    // StepPair's final input, not the intermediate X-only field.
                    xr.Pivot.y = xn.Pivot.y; xn.Pivot.x = xr.Pivot.x;
                }
                catch { check = false; groupVerifyErrors++; }
            }
            try
            {
                if (!r.Started) Startup(r);
                if (!n.Started) Startup(n);
                r.Pos = n.Pos = to; r.Stepped = n.Stepped = true;
                r.FLast = r.FStart + r.FChange * e;
                n.FLast = n.FStart + n.FChange * e;
                var xy = new Vector2(done ? r.FEnd : r.FLast, done ? n.FEnd : n.FLast);
                var off = pivotOffRef(r.D);
                if (check)
                {
                    try
                    {
                        groupChecks++;
                        if (!GroupV(xy, xr.Pivot) || !GroupV(off, xr.Offset))
                        {
                            groupMismatch++;
                            if (groupFirst == "none") groupFirst = "pair setPos input(x,y,off)";
                        }
                    }
                    catch { groupVerifyErrors++; }
                }
                setPos(r.D, xy, off);
                Steps += 2; PairSteps++;
                if (groupProbe) groupRecords += 2;
                if (done) { r.Running = n.Running = false; Completed += 2; }
            }
            catch
            {
                sr.Restore(r); sn.Restore(n); GroupReason(5);
                GroupIndividual(r, td); GroupIndividual(n, td);
                return;
            }
            if (check)
            {
                try { CheckGroup(r, xr); CheckGroup(n, xn); }
                catch { groupVerifyErrors++; }
            }
        }
        private static void GroupApply(Rec r, float e, bool done)
        {
            // Avoid ApplyE -> OnUpdate -> OnComplete switches for these keys.
            // Keep the scale startup/read/write order, including ScaleY after X.
            r.Stepped = true;
            switch (r.Key)
            {
                case 5:
                    r.FLast = r.FStart + r.FChange * e;
                    if (MoveApply.DeferTransform(r.D)) rotRef(r.D) = r.FLast;
                    else setRot(r.D, r.FLast);
                    if (done)
                    {
                        r.Running = false; Completed++;
                        if (MoveApply.DeferTransform(r.D)) rotRef(r.D) = r.FEnd;
                        else setRot(r.D, r.FEnd);
                    }
                    return;
                case 7: case 8:
                    var v = scaleRef(r.D);
                    BatchShadowScale(r, v);
                    if (r.Key == 7) v.x = r.VStart.x + r.VChange.x * e;
                    else v.y = r.VStart.y + r.VChange.y * e;
                    r.VLast = v;
                    if (MoveApply.DeferTransform(r.D)) scaleRef(r.D) = v;
                    else setScale(r.D, v);
                    if (done) { r.Running = false; Completed++; }
                    return;
                default:
                    ApplyE(r, e); OnUpdate(r);
                    if (done) { r.Running = false; Completed++; OnComplete(r); }
                    return;
            }
        }
        private static void StepGroup(int at, int count, float td)
        {
            var first = recs[at];
            var state = new GroupState(first);
            float to = first.Pos + td;
            bool done = to >= first.Dur;
            if (done) to = first.Dur;
            float e;
            try { e = ZeroTween.Eval(first.E, to, first.Dur, first.Over, first.Period); }
            catch
            {
                GroupReason(5);
                for (int j = 0; j < count; j++) GroupIndividual(recs[at + j], td);
                return;
            }
            bool verify = groupVerify && (++groupSerial & 63) == 0;
            if (groupProbe) { groupCount++; groupLastCount++; groupSizes[count]++; }
            for (int j = 0; j < count; j++)
            {
                var r = recs[at + j];
                // A setter may cancel/reuse a later record. Preserve its new state.
                if (j != 0 && (!state.Same(r) || GroupNative(r)))
                {
                    GroupReason(2); GroupIndividual(r, td); continue;
                }
                if (r.Key == 1 && PairXY && j + 1 < count && recs[at + j + 1].Key == 2
                    && (object)setPos != null && PairAllowed())
                {
                    var n = recs[at + j + 1];
                    if (state.Same(n) && !GroupNative(n))
                    {
                        GroupPair(r, n, td, to, e, done, verify); j++; continue;
                    }
                }
                Steps++;
                if (groupProbe) groupRecords++;
                try
                {
                    // Snapshot before Startup, and never call a game setter twice.
                    GroupExpected expected = default;
                    bool check = verify;
                    if (check)
                    {
                        try { expected = new GroupExpected(r, td); }
                        catch { check = false; groupVerifyErrors++; }
                    }
                    // Startup precedes position advancement just as in Step.
                    if (!r.Started) Startup(r);
                    r.Pos = to;
                    GroupApply(r, e, done);
                    if (check)
                    {
                        try { CheckGroup(r, expected); }
                        catch { groupVerifyErrors++; }
                    }
                }
                catch (Exception ex)
                {
                    // This record may already have written a field: never replay it.
                    // The rest are untouched and resume through the ordinary Step.
                    r.Running = false; Error(ex); GroupReason(5);
                    for (int k = j + 1; k < count; k++) GroupIndividual(recs[at + k], td);
                    return;
                }
            }
        }

        private struct GroupExpected
        {
            internal float Pos, FLast, FChange;
            internal bool Done;
            internal Vector2 VLast, VStart, VChange, Pivot, Scale, Offset;
            internal float Rot;
            internal GroupExpected(Rec r, float td)
            {
                Pos = r.Pos + td; Done = Pos >= r.Dur; if (Done) Pos = r.Dur;
                // Independent Step oracle: same float operation order, independent Eval.
                float ease = ZeroTween.Eval(r.E, Pos, r.Dur, r.Over, r.Period);
                Pivot = pivotPosRef(r.D); Scale = scaleRef(r.D); Rot = rotRef(r.D);
                Offset = pivotOffRef(r.D);
                FChange = r.Started ? r.FChange : r.FEnd - r.FStart;
                FLast = r.FLast; VStart = r.VStart; VChange = r.VChange; VLast = r.VLast;
                if (r.Key == 7 || r.Key == 8)
                {
                    if (!r.Started) { VStart = Scale; VChange = r.VEnd - VStart; }
                    VLast = Scale;
                    if (r.Key == 7) VLast.x = VStart.x + VChange.x * ease;
                    else VLast.y = VStart.y + VChange.y * ease;
                    Scale = VLast; // Scale has no OnComplete setter.
                    FChange = r.FChange;
                }
                else
                {
                    FLast = r.FStart + FChange * ease;
                    float field = Done ? r.FEnd : FLast;
                    if (r.Key == 1) Pivot.x = field;
                    else if (r.Key == 2) Pivot.y = field;
                    else Rot = field;
                }
            }
        }
        private static bool GroupV(Vector2 a, Vector2 b) => B(a.x) == B(b.x) && B(a.y) == B(b.y);
        private static void CheckGroup(Rec r, GroupExpected x)
        {
            groupChecks++;
            bool same = B(r.Pos) == B(x.Pos) && r.Started && r.Stepped && r.Running == !x.Done
                && B(r.FLast) == B(x.FLast) && B(r.FChange) == B(x.FChange)
                && GroupV(r.VStart, x.VStart) && GroupV(r.VChange, x.VChange) && GroupV(r.VLast, x.VLast)
                && GroupV(pivotPosRef(r.D), x.Pivot) && GroupV(scaleRef(r.D), x.Scale) && B(rotRef(r.D)) == B(x.Rot);
            if (same) return;
            groupMismatch++;
            if (groupFirst == "none") groupFirst = "key=" + r.Key + " pos=" + r.Pos.ToString("R")
                + " expected=" + x.Pos.ToString("R") + " ease=" + r.E
                + " FLast(bits)=" + B(r.FLast) + "/" + B(x.FLast)
                + " Pivot(bits)=" + B(pivotPosRef(r.D).x) + "," + B(pivotPosRef(r.D).y) + "/" + B(x.Pivot.x) + "," + B(x.Pivot.y)
                + " Scale(bits)=" + B(scaleRef(r.D).x) + "," + B(scaleRef(r.D).y) + "/" + B(x.Scale.x) + "," + B(x.Scale.y)
                + " Rot(bits)=" + B(rotRef(r.D)) + "/" + B(x.Rot);
        }
        private static void GroupFrameStart()
        {
            if (!groupProbe) return;
            groupFrames++; groupLastCount = 0;
            if ((groupFrames & 63) == 0) GroupCensus();
        }
        private static void GroupCensus()
        {
            // Probe only, once per 64 nonempty frames. No retained decoration keys.
            groupCensus.Clear();
            for (int i = 0; i < recs.Count; i++)
            {
                var r = recs[i]; if (!r.Running || !GroupKey(r.Key)) continue;
                var state = new GroupState(r);
                CensusEntry entry;
                if (!groupCensus.TryGetValue(state, out entry)) entry = new CensusEntry { Last = i - 1, Adjacent = true, Order = "" };
                entry.Adjacent &= i == entry.Last + 1; entry.Last = i; entry.Count++;
                if (entry.Count <= 5) entry.Order += (entry.Count == 1 ? "" : ",") + r.Key;
                else entry.Order = "over5";
                groupCensus[state] = entry;
            }
            foreach (var pair in groupCensus)
            {
                var entry = pair.Value; if (entry.Count < 2) continue;
                if (entry.Adjacent) groupAdjacent++; else groupSeparated++;
                long value; groupOrders.TryGetValue(entry.Order, out value); groupOrders[entry.Order] = value + 1;
            }
            groupCensus.Clear(); // Release all decoration references before returning.
        }
        private static void ResetGroup()
        {
            groupSerial = groupFrames = groupCount = groupRecords = groupTicks = groupChecks = groupMismatch = 0;
            groupVerifyErrors = 0;
            groupAdjacent = groupSeparated = 0; groupLastCount = 0; groupFirst = "none";
            Array.Clear(groupSizes, 0, groupSizes.Length); Array.Clear(groupFallback, 0, groupFallback.Length); groupOrders.Clear(); groupCensus.Clear();
        }
#if DEV || AUTOTEST
        private static string GroupBench()
        {
            const int groups = 2150, repeat = 200;
            // Isolated temporary workload; never advance the live animations.
            var live = recs.ToArray();
            var oldSet = setPos; var oldRot = setRot; var oldScale = setScale;
            bool oldProbe = groupProbe, oldVerify = groupVerify, oldPair = PairXY;
            bool oldDefer = MoveApply.DeferRotScale;
            int oldPairOk = pairOk;
            long oldSteps = Steps, oldCompleted = Completed, oldPairs = PairSteps;
            var root = new GameObject("groupstep-bench");
            try
            {
                recs.Clear(); groupProbe = groupVerify = false; PairXY = true; pairOk = 1;
                // Dummy decorations must never enter live MoveApply late lists.
                MoveApply.DeferRotScale = false;
                setRot = (d, v) => rotRef(d) = v;
                setScale = (d, v) => scaleRef(d) = v;
                for (int i = 0; i < groups; i++)
                {
                    var d = (scrDecoration)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(scrVisualDecoration));
                    var go = new GameObject("gs" + i); go.transform.SetParent(root.transform, false);
                    d.pivotTrans = go.transform; scaleRef(d) = Vector2.one;
                    foreach (int key in new[] { 1, 2, 5, 7, 8 })
                        recs.Add(new Rec { D = d, Key = key, Dur = 1000000f, E = Ease.OutCubic,
                            Over = 1.70158f, FStart = 2f, FEnd = 15f, VEnd = new Vector2(2f, 3f) });
                }
                double fields = 0, engine = 0;
                for (int mode = 0; mode < 2; mode++)
                {
                    bool writeTransform = mode == 1;
                    setPos = (d, v, off) =>
                    {
                        pivotPosRef(d) = v;
                        if (writeTransform) d.pivotTrans.localPosition = new Vector3(v.x, v.y, 0f);
                    };
                    for (int warm = 0; warm < 5; warm++) GroupBenchFrame();
                    long t = Stopwatch.GetTimestamp();
                    for (int frame = 0; frame < repeat; frame++) GroupBenchFrame();
                    double us = (Stopwatch.GetTimestamp() - t) * 1e6 / Stopwatch.Frequency / repeat / groups;
                    if (mode == 0) fields = us; else engine = us;
                }
                return "GroupStep v2 N=2150 keys=1,2,5,7,8 R=200 us/group fields=" + fields.ToString("F4")
                    + " +Transform=" + engine.ToString("F4") + " ms/frame=" + (engine * groups / 1000).ToString("F3")
                    + " budget<=1us=" + (engine <= 1)
                    + "; includes GroupCount+StepGroup, DeferTransform disabled fast check;"
                    + " surrogate setPos writes pivotPosVec + localPosition once, rotation/scale setters store fields;"
                    + " excludes real game SetPosition/UpdatePosition chain, live defer eligibility/list bookkeeping, native prepare, Compact, census, verification;"
                    + " standalone bench runs even in Dev/Prof (live grouping stays suppressed)";
            }
            finally
            {
                recs.Clear(); recs.AddRange(live);
                setPos = oldSet; setRot = oldRot; setScale = oldScale;
                groupProbe = oldProbe; groupVerify = oldVerify; PairXY = oldPair; pairOk = oldPairOk;
                MoveApply.DeferRotScale = oldDefer;
                Steps = oldSteps; Completed = oldCompleted; PairSteps = oldPairs;
                UnityEngine.Object.Destroy(root);
            }
        }
        private static void GroupBenchFrame()
        {
            for (int at = 0; at < recs.Count;)
            {
                int count = GroupCount(at);
                if (count > 1) StepGroup(at, count, 1f / 120f);
                else GroupIndividual(recs[at], 1f / 120f);
                at += count;
            }
        }
#endif
        internal static string GroupCommand(string arg)
        {
            switch ((arg ?? "").Trim().ToLowerInvariant())
            {
#if DEV || AUTOTEST
                case "bench": return GroupBench();
#endif
                case "verify": ResetGroup(); groupProbe = groupVerify = true; break;
                case "verify off": groupVerify = false; break;
                case "probe": ResetGroup(); groupProbe = true; groupVerify = false; break;
                case "off": groupProbe = groupVerify = false; break;
                case "reset": ResetGroup(); break;
            }
            return GroupStat();
        }
        private static string GroupStat()
        {
            string s = " | GroupStep enabled=" + GroupEnabled + " suppressed(Prof/Dev)=" + Prof
                + " compatibility=" + groupCompatibility + " probe=" + groupProbe + " verify=" + groupVerify;
            if (!groupProbe && groupFrames == 0) return s;
            s += " groups/frame=" + (groupFrames == 0 ? 0 : (double)groupCount / groupFrames).ToString("F1")
                + " last=" + groupLastCount + " groups=" + groupCount + " records=" + groupRecords
                + " sizes[2,3,4,5]=" + string.Join(",", new[] { groupSizes[2], groupSizes[3], groupSizes[4], groupSizes[5] })
                + " fallback[key,native,state,duplicate,single,exception]=" + string.Join(",", groupFallback)
                + " loop(same range as LastFrameMs)/frame ms=" + (groupFrames == 0 ? 0 : groupTicks * 1000.0 / Stopwatch.Frequency / groupFrames).ToString("F3")
                + " checks=" + groupChecks + " mismatch=" + groupMismatch + " verifyErrors=" + groupVerifyErrors + " first=" + groupFirst
                + " census adjacent=" + groupAdjacent + " separated=" + groupSeparated;
            foreach (var pair in groupOrders) s += " [" + pair.Key + "]=" + pair.Value;
            return s;
        }
    }
}
