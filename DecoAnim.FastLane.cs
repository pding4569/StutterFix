using System;
using System.Diagnostics;
using DG.Tweening;
using UnityEngine;

namespace StutterFix
{
    internal static partial class DecoAnim
    {
        internal static bool FastLane = true, FastCache, FastInline, FastCompact = true;
        private static bool fastCompatible, fastVerify;
        private static scrDecoration fastLast;
        private static long fastInlineN, fastFallback, fastHits, fastInvalidations, fastChecks, fastMismatch;
        private static long fastSerial, fastCompactSkipped;
        private static long fastCompactDirty, fastCompactOff;
        // Sticky until Compact: includes kills outside Update and completed/error records.
        private static bool fastDirty = true;

        private static void ConfigureFastLane(string path)
        {
            FastLane = !System.IO.File.Exists(System.IO.Path.Combine(path, "fastlane-off"));
            fastCompatible = false; // Candidate rejected by the offline benchmark; never used by live Update.
            FastInvalidate(); fastDirty = true;
        }
        private static void FastInvalidate()
        {
            if ((object)fastLast != null) fastInvalidations++;
            fastLast = null;
        }
#if DEV || AUTOTEST || FASTLANE_FIXTURE
        private static bool FastTryStep(Rec r, float td)
        {
            if (!FastLane || !FastInline || !fastCompatible || Prof || GroupNative(r)
                || (r.Key != 5 && r.Key != 7 && r.Key != 8))
            { fastFallback++; FastInvalidate(); return false; }
            bool cached = FastCache && ReferenceEquals(fastLast, r.D);
            bool admitted;
            if (cached) { fastHits++; admitted = true; }
            else admitted = MoveApply.DeferTransform(r.D);
            if (!admitted) { fastFallback++; FastInvalidate(); return false; }
            // Cache only successful admission, and only across field-only writes.
            // Every ordinary Step/StepPair/group/exception boundary invalidates it.
            fastLast = r.D;
            bool check = fastVerify && (++fastSerial & 63) == 0;
            GroupExpected expected = default;
            if (check)
            {
                expected = new GroupExpected(r, td);
                fastChecks++;
                if (cached && !MoveApply.DeferTransform(r.D)) fastMismatch++;
            }
            if (!r.Started) Startup(r);
            float to = r.Pos + td;
            bool done = to >= r.Dur;
            if (done) to = r.Dur;
            r.Pos = to; r.Stepped = true;
            float e = ZeroTween.Eval(r.E, to, r.Dur, r.Over, r.Period);
            if (r.Key == 5)
            {
                r.FLast = r.FStart + r.FChange * e;
                rotRef(r.D) = r.FLast;
            }
            else
            {
                var v = scaleRef(r.D);
                BatchShadowScale(r, v);
                if (r.Key == 7) v.x = r.VStart.x + r.VChange.x * e;
                else v.y = r.VStart.y + r.VChange.y * e;
                r.VLast = v; scaleRef(r.D) = v;
            }
            if (done)
            {
                r.Running = false; Completed++; fastDirty = true;
                if (r.Key == 5) rotRef(r.D) = r.FEnd;
            }
            fastInlineN++;
            if (check)
            {
                bool same = B(r.Pos) == B(expected.Pos) && r.Running == !expected.Done
                    && B(r.FLast) == B(expected.FLast) && B(r.FChange) == B(expected.FChange)
                    && GroupV(r.VStart, expected.VStart) && GroupV(r.VChange, expected.VChange)
                    && GroupV(r.VLast, expected.VLast) && GroupV(scaleRef(r.D), expected.Scale)
                    && B(rotRef(r.D)) == B(expected.Rot);
                if (!same) fastMismatch++;
                if (fastMismatch != 0) { FastLane = false; FastInvalidate(); }
            }
            return true;
        }
#endif
        private static bool FastMaySkipCompact()
        {
            if (!FastLane || !FastCompact) { fastCompactOff++; return false; }
            if (fastDirty) { fastCompactDirty++; return false; }
            if (fastVerify && (++fastSerial & 63) == 0)
            {
                // Explicit diagnostic only: compare the admission with the actual
                // Compact predicate. No stopwatch/delegates/allocations in live Update.
                fastChecks++;
                for (int i = 0; i < recs.Count; i++)
                    if (!recs[i].Running)
                    { fastMismatch++; FastLane = false; return false; }
            }
            fastCompactSkipped++;
            return true;
        }
        internal static string FastCommand(string arg)
        {
            switch ((arg ?? "").Trim().ToLowerInvariant())
            {
                case "verify": fastVerify = true; fastSerial = fastChecks = fastMismatch = 0; break;
                case "verify off": fastVerify = false; break;
#if DEV || AUTOTEST
                case "bench": return FastBench();
#endif
            }
            return "enabled=" + FastLane + " live=compact-only candidateCompatible=" + fastCompatible + " compact/cache/inline="
                + FastCompact + "/" + FastCache + "/" + FastInline + " inline=" + fastInlineN
                + " fallback=" + fastFallback + " cacheHits=" + fastHits + " invalidations=" + fastInvalidations
                + " compactSkipped=" + fastCompactSkipped + " compactDirty=" + fastCompactDirty
                + " compactOff=" + fastCompactOff + " devShadowPass=" + Edition.Dev + " verify=" + fastVerify
                + " checks=" + fastChecks + " mismatch=" + fastMismatch;
        }
#if DEV || AUTOTEST
        private static string FastBench()
        {
            const int count = 2150, repeat = 200;
            var root = new GameObject("fastlane-bench");
            var records = new Rec[count];
            var pairs = new Rec[count];
            var oldRot = setRot; var oldScale = setScale; var oldPos = setPos;
            bool oldDefer = MoveApply.DeferRotScale, oldLane = FastLane, oldCompatible = fastCompatible;
            bool oldVerify = fastVerify, oldInline = FastInline, oldCache = FastCache;
            bool oldPosFast = PosFast.Enabled;
            bool oldDirty = fastDirty;
            long oldCompleted = Completed;
            long oldProfN = profN;
            var oldN = (long[])ProfN.Clone(); var oldApply = (long[])ProfApply.Clone(); var oldSet = (long[])ProfSet.Clone();
            long oldInlineN = fastInlineN, oldFallback = fastFallback, oldHits = fastHits, oldInvalidations = fastInvalidations;
            try
            {
                MoveApply.DeferRotScale = false; FastLane = FastInline = FastCache = fastCompatible = true; fastVerify = false;
                PosFast.Enabled = false;
                setRot = (d, v) => rotRef(d) = v; setScale = (d, v) => scaleRef(d) = v;
                setPos = (d, v, off) => { pivotPosRef(d) = v; d.pivotTrans.localPosition = new Vector3(v.x, v.y, 0); };
                for (int i = 0; i < count; i++)
                {
                    var d = (scrDecoration)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(scrVisualDecoration));
                    var go = new GameObject("fl" + i); go.transform.SetParent(root.transform, false); d.pivotTrans = go.transform;
                    scaleRef(d) = Vector2.one;
                    records[i] = new Rec { D = d, Dur = 1000000, E = Ease.OutCubic, FEnd = 15, VEnd = new Vector2(2, 3), Over = 1.70158f };
                    pairs[i] = new Rec { D = d, Key = 2, Dur = 1000000, E = Ease.OutCubic, FEnd = 15, Over = 1.70158f };
                }
                var result = new System.Text.StringBuilder("N=2150 R=200 us/call baseline/hot-cache-inline: ");
                foreach (int key in new[] { 5, 7, 8 })
                {
                    result.Append("key").Append(key).Append('=');
                    for (int mode = 0; mode < 2; mode++)
                    {
                        foreach (var r in records) { r.Key = key; r.Started = false; r.Pos = 0; }
                        for (int warm = 0; warm < 5; warm++) foreach (var r in records) { fastLast = r.D; if (mode == 0 || Prof) Step(r, 1f / 120); else FastTryStep(r, 1f / 120); }
                        long start = Stopwatch.GetTimestamp();
                        for (int f = 0; f < repeat; f++) foreach (var r in records) { fastLast = r.D; if (mode == 0 || Prof) Step(r, 1f / 120); else FastTryStep(r, 1f / 120); }
                        result.Append(((Stopwatch.GetTimestamp() - start) * 1e6 / Stopwatch.Frequency / repeat / count).ToString("F4")).Append('/');
                    }
                    result.Append(' ');
                }
                var list = new System.Collections.Generic.List<Rec>(records);
                foreach (string part in new[] { "pair", "loop", "compact" })
                {
                    result.Append(part).Append('=');
                    for (int mode = 0; mode < 2; mode++)
                    {
                        foreach (var r in records) { r.Key = 1; r.Started = false; }
                        long start = Stopwatch.GetTimestamp();
                        for (int f = 0; f < repeat; f++)
                        {
                            if (part == "compact") { if (mode == 0) CompactRecords(list); }
                            else for (int i = 0; i < count; i++)
                            {
                                var r = records[i];
                                if (!r.Running) continue;
                                if (part == "pair") StepPair(r, pairs[i], 1f / 120);
                            }
                        }
                        result.Append(((Stopwatch.GetTimestamp() - start) * 1e6 / Stopwatch.Frequency / repeat / count).ToString("F4")).Append('/');
                    }
                }
                result.Append("surrogate fields; hot successful admission seeded; excludes cold eligibility. Prof=").Append(Prof)
                    .Append(" (Prof=true measures baseline twice). pair and loop unchanged; compact all-running skip. No live records advanced.");
                return result.ToString();
            }
            finally
            {
                setRot = oldRot; setScale = oldScale; setPos = oldPos;
                MoveApply.DeferRotScale = oldDefer; FastLane = oldLane; fastCompatible = oldCompatible;
                fastVerify = oldVerify; FastInline = oldInline; FastCache = oldCache;
                PosFast.Enabled = oldPosFast; profN = oldProfN;
                Array.Copy(oldN, ProfN, ProfN.Length); Array.Copy(oldApply, ProfApply, ProfApply.Length); Array.Copy(oldSet, ProfSet, ProfSet.Length);
                fastDirty = oldDirty; Completed = oldCompleted; FastInvalidate(); UnityEngine.Object.Destroy(root);
                fastInlineN = oldInlineN; fastFallback = oldFallback; fastHits = oldHits; fastInvalidations = oldInvalidations;
            }
        }
#endif
    }
}
