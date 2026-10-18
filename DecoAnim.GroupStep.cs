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
        internal static bool GroupEnabled = true;
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
                if (!state.Same(r) || GroupNative(r))
                {
                    GroupReason(2); GroupIndividual(r, td); continue;
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
                    if (!r.Started) Startup(r); // Scale Y reads AFTER Scale X's setter.
                    r.Pos = to; r.Stepped = true;
                    ApplyE(r, e);
                    OnUpdate(r);
                    if (done) { r.Running = false; Completed++; OnComplete(r); }
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
            internal Vector2 VLast, VStart, VChange, Pivot, Scale;
            internal float Rot;
            internal GroupExpected(Rec r, float td)
            {
                Pos = r.Pos + td; Done = Pos >= r.Dur; if (Done) Pos = r.Dur;
                // Independent Step oracle: same float operation order, independent Eval.
                float ease = ZeroTween.Eval(r.E, Pos, r.Dur, r.Over, r.Period);
                Pivot = pivotPosRef(r.D); Scale = scaleRef(r.D); Rot = rotRef(r.D);
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
        internal static string GroupCommand(string arg)
        {
            switch ((arg ?? "").Trim().ToLowerInvariant())
            {
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
