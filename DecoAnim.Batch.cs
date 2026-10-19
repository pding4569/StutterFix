using System;
using System.Collections.Generic;
using System.Diagnostics;
using DG.Tweening;
using UnityEngine;

namespace StutterFix
{
    internal static partial class DecoAnim
    {
        // Stable slots, sorted schedule. Rec remains the proxy identity; InList
        // denotes ownership by either container. Only release clears ownership.
        private static bool batchOn, batchVerify;
        private static long batchOrder, batchFrames, batchRecords, batchChecks, batchMismatch, batchSync, batchDone, batchDecisions;
        private static long batchSerial, batchTicks;
        private static string batchFirst = "none";
        private static int batchCount, batchUsed;
        private const int BatchCapacity = 65536;
        private static Rec[] br;
        private static scrDecoration[] bd;
        private static int[] bdi, brefs;
        private static int batchDecorationUsed;
        private static readonly Dictionary<scrDecoration, int> batchDecorationIds = new Dictionary<scrDecoration, int>();
        private static readonly Stack<int> batchDecorationFree = new Stack<int>(BatchCapacity);
        private static int[] bk;
        private static Ease[] be;
        private static float[] bp, bt, bo, bperiod, bs, bc, blast;
        private static Vector2[] bvs, bvc, bvlast;
        private static readonly List<int> batchSchedule = new List<int>();
        private static readonly List<int> batchPending = new List<int>();
        private static readonly Stack<int> batchFree = new Stack<int>(BatchCapacity);
        private static readonly List<int> batchRetired = new List<int>();
        private static readonly List<Rec> batchReturned = new List<Rec>();
        private static readonly long[] batchReasons = new long[8];

        private static void ConfigureBatch(string path)
        {
            BatchRestoreAll();
#if DEV || AUTOTEST
            batchOn = System.IO.File.Exists(System.IO.Path.Combine(path, "decobatch-on"));
#else
            batchOn = false;
#endif
            batchVerify = false;
            if (!batchOn) return;
            BatchAllocate();
        }
        private static void BatchAllocate()
        {
            br = new Rec[BatchCapacity]; bd = new scrDecoration[BatchCapacity]; bk = new int[BatchCapacity]; be = new Ease[BatchCapacity];
            bdi = new int[BatchCapacity]; brefs = new int[BatchCapacity]; batchDecorationIds.EnsureCapacity(BatchCapacity);
            bp = new float[BatchCapacity]; bt = new float[BatchCapacity]; bo = new float[BatchCapacity]; bperiod = new float[BatchCapacity];
            bs = new float[BatchCapacity]; bc = new float[BatchCapacity]; blast = new float[BatchCapacity];
            bvs = new Vector2[BatchCapacity]; bvc = new Vector2[BatchCapacity]; bvlast = new Vector2[BatchCapacity];
            batchSchedule.Capacity = BatchCapacity; batchPending.Capacity = BatchCapacity;
            batchReturned.Capacity = BatchCapacity;
            batchRetired.Capacity = BatchCapacity;
        }
        private static bool BatchAllowed()
        {
            if (Prof || Edition.Dev || NativeMotion.Enabled || GroupEnabled || batchMismatch != 0) return false;
#if DEV || AUTOTEST
            if (DecoBatchShadow.On) return false;
#endif
#if AUTOTEST
            if (AblateMask != 0 || UpdateCostProbe.InWindow) return false;
#endif
            return true;
        }
        private static bool BatchKey(int k) => k == 1 || k == 2 || k == 5 || k == 7 || k == 8;
        // Admission is cached ONLY across contiguous field writes. Any setter,
        // especially opacity/color/pivot, invalidates it before the next record.
        private static bool BatchAdmission(scrDecoration d)
        {
            batchDecisions++;
            if ((object)d == null || d.GetType() != typeof(scrVisualDecoration)) { batchReasons[0]++; return false; }
            if (InvisibleSkip.MaskOrUnknown((scrVisualDecoration)d)) { batchReasons[1]++; return false; }
            if (!(colRef(d).a > 0f) || !(opaRef(d) > 0f)) { batchReasons[2]++; return false; }
            if (!d.GetVisible()) { batchReasons[3]++; return false; }
            if (!MoveApply.DeferTransform(d)) { batchReasons[4]++; return false; }
            return true;
        }
        private static void BatchEnroll(Rec r)
        {
            if (!r.Running || r.BatchSlot >= 0 || !r.Started || r.Shadow != null) return;
            if (batchFree.Count == 0 && batchUsed == BatchCapacity) { batchReasons[6]++; return; }
            int s = batchFree.Count != 0 ? batchFree.Pop() : batchUsed++;
            if (!batchDecorationIds.TryGetValue(r.D, out int deco))
            {
                deco = batchDecorationFree.Count != 0 ? batchDecorationFree.Pop() : batchDecorationUsed++;
                bd[deco] = r.D; batchDecorationIds.Add(r.D, deco);
            }
            brefs[deco]++; bdi[s] = deco;
            r.BatchSlot = s; br[s] = r; bk[s] = r.Key; be[s] = r.E;
            bp[s] = r.Pos; bt[s] = r.Dur; bo[s] = r.Over; bperiod[s] = r.Period;
            bs[s] = r.FStart; bc[s] = r.FChange; blast[s] = r.FLast;
            bvs[s] = r.VStart; bvc[s] = r.VChange; bvlast[s] = r.VLast;
            batchCount++; batchPending.Add(s); fastDirty = true;
        }
        private static void BatchSync(int s)
        {
            var r = br[s]; r.Pos = bp[s]; r.FLast = blast[s]; r.VLast = bvlast[s]; r.Stepped = true;
            batchSync++;
        }
        private static void BatchRelease(Rec r, bool sync)
        {
            int s = r.BatchSlot; if (s < 0) return;
            if (sync) BatchSync(s);
            int deco = bdi[s];
            if (--brefs[deco] == 0) { batchDecorationIds.Remove(bd[deco]); bd[deco] = null; batchDecorationFree.Push(deco); }
            br[s] = null; r.BatchSlot = -1; r.InList = false;
            batchCount--;
            // A schedule tombstone must not point at a newly reused slot.
            batchRetired.Add(s);
        }
        private static void BatchRestoreAll()
        {
            if (br == null) return;
            // Remove resident duplicates left by first-frame enrollment before
            // restoring. No setters or animation advancement here.
            for (int i = recs.Count - 1; i >= 0; i--) if (recs[i].BatchSlot >= 0) recs.RemoveAt(i);
            for (int s = 0; s < batchUsed; s++) if (br[s] != null)
            {
                var r = br[s]; BatchRelease(r, true);
                if (r.Running) { r.InList = true; recs.Add(r); }
            }
            foreach (var r in batchReturned) if (r.Running && !recs.Contains(r)) { r.InList = true; recs.Add(r); }
            batchReturned.Clear(); batchSchedule.Clear(); batchPending.Clear();
            foreach (int s in batchRetired) batchFree.Push(s);
            batchRetired.Clear();
            recs.Sort((a, b) => a.BatchOrder.CompareTo(b.BatchOrder)); fastDirty = true;
        }
        internal static void BatchForget()
        {
            // A level boundary drops old animations exactly as SceneReset does.
            if (batchOn) DropAll();
            batchSchedule.Clear(); batchPending.Clear(); batchReturned.Clear(); batchFree.Clear(); batchRetired.Clear(); batchUsed = batchCount = 0;
            batchDecorationIds.Clear(); batchDecorationFree.Clear(); batchDecorationUsed = 0;
        }
        internal static void BatchUninstall()
        {
            BatchForget(); batchOn = batchVerify = false;
            br = null; bd = null; bk = bdi = brefs = null; be = null;
            bp = bt = bo = bperiod = bs = bc = blast = null; bvs = bvc = bvlast = null;
        }

        private static bool BatchCalculate(int s, float td)
        {
            float to = bp[s] + td; bool done = false;
            if (to >= bt[s]) { to = bt[s]; done = true; }
            float e = ZeroTween.Eval(be[s], to, bt[s], bo[s], bperiod[s]);
            if (bk[s] == 7) { var v = scaleRef(bd[bdi[s]]); v.x = bvs[s].x + bvc[s].x * e; bvlast[s] = v; }
            else if (bk[s] == 8) { var v = scaleRef(bd[bdi[s]]); v.y = bvs[s].y + bvc[s].y * e; bvlast[s] = v; }
            else blast[s] = bs[s] + bc[s] * e;
            bp[s] = to;
            return done;
        }
        private static void BatchFinish(int s, bool done)
        {
            if (!done) return;
            var r = br[s]; BatchSync(s); r.Running = false; Completed++; batchDone++; fastDirty = true;
            BatchRelease(r, false);
        }
        private static void BatchCheck(int s, GroupExpected x, bool done, bool field, bool pair = false)
        {
            batchChecks++;
            string component = ""; int expected = 0, actual = 0;
            if (B(bp[s]) != B(x.Pos)) { component = "Pos"; expected = B(x.Pos); actual = B(bp[s]); }
            else if (done != x.Done) { component = "done"; expected = x.Done ? 1 : 0; actual = done ? 1 : 0; }
            else if (bk[s] == 7 || bk[s] == 8)
            {
                if (!GroupV(bvlast[s], x.VLast)) { component = "VLast"; bool axis = B(bvlast[s].x) != B(x.VLast.x); expected = B(axis ? x.VLast.x : x.VLast.y); actual = B(axis ? bvlast[s].x : bvlast[s].y); }
            }
            else if (B(blast[s]) != B(x.FLast)) { component = "FLast"; expected = B(x.FLast); actual = B(blast[s]); }
            if (component.Length == 0 && field)
            {
                var d = bd[bdi[s]]; int k = bk[s];
                if (k == 5) { expected = B(x.Rot); actual = B(rotRef(d)); }
                else if (k == 7 || k == 8) { expected = B(k == 7 ? x.Scale.x : x.Scale.y); actual = B(k == 7 ? scaleRef(d).x : scaleRef(d).y); }
                else { expected = B(k == 1 ? x.Pivot.x : x.Pivot.y); actual = B(k == 1 ? pivotPosRef(d).x : pivotPosRef(d).y); }
                if (expected != actual) component = pair ? "setPos/field" : "field";
            }
            if (component.Length == 0) return;
            batchMismatch++;
            if (batchFirst == "none") batchFirst = "key=" + bk[s] + " ease=" + be[s] + " component=" + component + " expected=0x" + expected.ToString("X8") + " actual=0x" + actual.ToString("X8");
        }
        private static void BatchStep(int s, int next, float td)
        {
            var r = br[s]; var n = next >= 0 ? br[next] : null;
            bool check = batchVerify && (++batchSerial & 15) == 0;
            GroupExpected x = default, y = default;
            if (check) { BatchSync(s); x = new GroupExpected(r, td); if (n != null) { BatchSync(next); y = new GroupExpected(n, td); } }
            bool done = BatchCalculate(s, td), ndone = n != null && BatchCalculate(next, td);
            if (check)
            {
                BatchCheck(s, x, done, false);
                if (n != null) BatchCheck(next, y, ndone, false);
                if (batchMismatch != 0) throw new InvalidOperationException("decobatch calculation mismatch: " + batchFirst);
            }
            if (n != null)
            {
                var pos = new Vector2(done ? r.FEnd : blast[s], ndone ? n.FEnd : blast[next]);
                // Preserve the existing pair's setter and PosFast bookkeeping.
                if (!PosFast.Try(r, pos)) { setPos(r.D, pos, pivotOffRef(r.D)); PosFast.Remember(r); }
                Steps += 2; PairSteps++; batchRecords += 2;
                if (check) { BatchCheck(s, x, done, true, true); BatchCheck(next, y, ndone, true, true); }
                BatchFinish(s, done); BatchFinish(next, ndone);
            }
            else
            {
                int k = bk[s];
                if (k == 7 || k == 8) scaleRef(r.D) = bvlast[s];
                else if (k == 5) rotRef(r.D) = done ? r.FEnd : blast[s];
                else
                {
                    // Single-axis paths retain both the update and completion
                    // setters (and their side effects), just as Step does.
                    r.FLast = blast[s]; OnUpdate(r); if (done) OnComplete(r);
                }
                Steps++; batchRecords++;
                if (check) BatchCheck(s, x, done, true);
                BatchFinish(s, done);
            }
        }
        private static void BatchFrame(float td)
        {
            long tick = Stopwatch.GetTimestamp(); batchFrames++;
            BatchSweep();
            int i = 0, j = 0, limit = batchSchedule.Count;
            scrDecoration cached = null; bool admitted = false;
            while (i < recs.Count || j < limit)
            {
                while (j < limit && br[batchSchedule[j]] == null) j++;
                if (i == recs.Count && j == limit) break;
                bool resident = j < limit && (i == recs.Count || br[batchSchedule[j]].BatchOrder < recs[i].BatchOrder);
                int s = resident ? batchSchedule[j++] : -1;
                var r = resident ? br[s] : recs[i++];
                if (!r.Running) { fastDirty = true; continue; }
                bool key = BatchKey(r.Key);
                if (!key) { batchReasons[5]++; cached = null; }
                if (key && !ReferenceEquals(cached, r.D))
                {
                    cached = r.D;
                    try { admitted = BatchAdmission(r.D); }
                    catch (Exception ex) { admitted = false; cached = null; batchReasons[7]++; Error(ex); }
                }
                bool use = key && admitted && r.Shadow == null;
                Rec n = null; int ns = -1;
                // Pair only adjacent entries in the GLOBAL schedule. Never cross
                // an intervening color/opacity/pivot or a record in the other list.
                if (r.Key == 1 && PairXY && PairAllowed())
                {
                    if (resident && j < limit)
                    {
                        int q = batchSchedule[j]; var candidate = br[q];
                        if (candidate != null && (i == recs.Count || candidate.BatchOrder < recs[i].BatchOrder)) { n = candidate; ns = q; }
                    }
                    else if (!resident && i < recs.Count && (j == limit || br[batchSchedule[j]] == null || recs[i].BatchOrder < br[batchSchedule[j]].BatchOrder)) n = recs[i];
                    if (n == null || n.Key != 2 || !n.Running || !ReferenceEquals(n.D, r.D)) { n = null; ns = -1; }
                }
                if (resident && use && batchMismatch == 0)
                {
                    float oldPos = bp[s], oldLast = blast[s]; Vector2 oldV = bvlast[s];
                    float oldNPos = ns >= 0 ? bp[ns] : 0, oldNLast = ns >= 0 ? blast[ns] : 0;
                    Vector2 oldNV = ns >= 0 ? bvlast[ns] : default;
                    try { BatchStep(s, ns, td); }
                    catch (Exception ex)
                    {
                        // Calculation failure is per-record: restore and use the
                        // established safety boundary, leaving other slots live.
                        batchReasons[7]++; Error(ex);
                        if (r.BatchSlot >= 0) { bp[s] = oldPos; blast[s] = oldLast; bvlast[s] = oldV; BatchReturn(r); }
                        if (n != null && n.BatchSlot >= 0) { bp[ns] = oldNPos; blast[ns] = oldNLast; bvlast[ns] = oldNV; BatchReturn(n); }
                        try { Step(r, td); Steps++; } catch (Exception fallback) { r.Running = false; Error(fallback); }
                        if (n != null) { try { Step(n, td); Steps++; } catch (Exception fallback) { n.Running = false; Error(fallback); } j++; }
                        cached = null;
                        continue;
                    }
                    if (ns >= 0) j++;
                    // PairAllowed excludes foreign position patches. These
                    // setters do not modify alpha/visibility, so admission holds.
                    continue;
                }
                if (resident) { BatchRelease(r, true); batchReturned.Add(r); r.InList = true; }
                if (n != null && ns >= 0) { BatchRelease(n, true); batchReturned.Add(n); n.InList = true; }
                try
                {
                    if (n != null) { StepPair(r, n, td); Steps += 2; PairSteps++; if (ns >= 0) j++; else i++; }
                    else { Step(r, td); Steps++; }
                    if (use) { BatchEnroll(r); if (n != null) BatchEnroll(n); }
                }
                catch (Exception ex) { r.Running = false; if (n != null) n.Running = false; fastDirty = true; Error(ex); }
                cached = null; // ordinary Step can execute arbitrary setters
            }
            // Enrollment does not mutate the schedule being read. Releases are
            // tombstones until this single order-preserving sweep.
            BatchSweep();
            if (batchPending.Count != 0)
            {
                batchSchedule.AddRange(batchPending); batchPending.Clear();
                batchSchedule.Sort((a, b) => br[a].BatchOrder.CompareTo(br[b].BatchOrder));
            }
            if (batchReturned.Count != 0) { recs.AddRange(batchReturned); batchReturned.Clear(); recs.Sort((a, b) => a.BatchOrder.CompareTo(b.BatchOrder)); }
            // Always remove newly enrolled records, even when FastCompact would
            // otherwise skip. Thereafter recs contains only ordinary records.
            Compact();
            batchTicks += Stopwatch.GetTimestamp() - tick;
        }
        private static void BatchSweep()
        {
            if (batchRetired.Count == 0) return;
            int w = 0;
            for (int k = 0; k < batchSchedule.Count; k++) { int s = batchSchedule[k]; if (br[s] != null) batchSchedule[w++] = s; }
            if (w < batchSchedule.Count) batchSchedule.RemoveRange(w, batchSchedule.Count - w);
            // Pending records can be killed by a later ordinary setter.
            for (int k = batchPending.Count - 1; k >= 0; k--) if (br[batchPending[k]] == null) batchPending.RemoveAt(k);
            foreach (int s in batchRetired) batchFree.Push(s);
            batchRetired.Clear();
        }
        private static void BatchReturn(Rec r)
        {
            BatchRelease(r, true); r.InList = true; batchReturned.Add(r);
        }
        private static IEnumerable<Rec> BatchView()
        {
            foreach (var r in recs) if (r.BatchSlot < 0) yield return r;
            if (br != null) for (int s = 0; s < batchUsed; s++) if (br[s] != null) { BatchSync(s); yield return br[s]; }
        }
#if DEV || AUTOTEST
        private static string BatchBench()
        {
            if (recs.Count != 0 || batchCount != 0) return "bench refused: stop all animations first";
            if (Prof || Edition.Dev || DecoBatchShadow.On) return "bench refused: use PlayerAuto, Prof/shadow off";
            // No live objects are touched. All setters are field-only surrogates;
            // this measures arithmetic/dispatch, not real late admission or FPS.
            bool oldOn = batchOn, oldVerify = batchVerify, oldDefer = MoveApply.DeferRotScale;
            var oldPos = setPos; var oldX = setPosX; var oldY = setPosY; var oldRot = setRot; var oldScale = setScale;
            long oldSteps = Steps, oldPairs = PairSteps, oldRecords = batchRecords;
            var text = new System.Text.StringBuilder();
            try
            {
                if (br == null) BatchAllocate();
                batchVerify = false; MoveApply.DeferRotScale = false;
                setPos = (d, v, off) => pivotPosRef(d) = v;
                setPosX = (d, v, off) => { var p = pivotPosRef(d); p.x = v; pivotPosRef(d) = p; };
                setPosY = (d, v, off) => { var p = pivotPosRef(d); p.y = v; pivotPosRef(d) = p; };
                setRot = (d, v) => rotRef(d) = v; setScale = (d, v) => scaleRef(d) = v;
                foreach (int count in new[] { 2150, 4500 })
                {
                    for (int s = 0; s < count; s += 5)
                    {
                        var d = (scrDecoration)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(scrVisualDecoration));
                        scaleRef(d) = Vector2.one;
                        int at = s;
                        foreach (int key in new[] { 1, 2, 5, 7, 8 })
                        {
                            var r = new Rec { D = d, Key = key, Dur = 1000000f, E = Ease.OutCubic,
                                FStart = 2f, FEnd = 15f, VEnd = new Vector2(2f, 3f) };
                            ZeroTween.EaseParams(r.E, out r.Over, out r.Period); Startup(r);
                            br[at] = r; bd[at] = d; bdi[at] = at; bk[at] = key; be[at] = r.E;
                            bt[at] = r.Dur; bp[at] = 0; bo[at] = r.Over; bperiod[at] = r.Period;
                            bs[at] = r.FStart; bc[at] = r.FChange; bvs[at] = r.VStart; bvc[at] = r.VChange;
                            at++;
                        }
                    }
                    for (int mode = 0; mode < 2; mode++)
                    {
                        long start = 0;
                        for (int frame = -10; frame < 200; frame++)
                        {
                            if (frame == 0) start = Stopwatch.GetTimestamp();
                            for (int s = 0; s < count; s += 5)
                            {
                                if (mode == 0) { StepPair(br[s], br[s + 1], 1f / 120f); for (int k = 2; k < 5; k++) Step(br[s + k], 1f / 120f); }
                                else { BatchStep(s, s + 1, 1f / 120f); for (int k = 2; k < 5; k++) BatchStep(s + k, -1, 1f / 120f); }
                            }
                        }
                        double us = (Stopwatch.GetTimestamp() - start) * 1e6 / Stopwatch.Frequency / 200 / count;
                        text.Append("N=").Append(count).Append(mode == 0 ? " Step=" : " batch=").Append(us.ToString("F4")).Append("us/record; ");
                    }
                    Array.Clear(br, 0, count); Array.Clear(bd, 0, count);
                }
                return text + "field-only surrogate; excludes admission, merge, maintenance, real setPos, LateUpdate; live ABBA required";
            }
            finally
            {
                Array.Clear(br, 0, br.Length); Array.Clear(bd, 0, bd.Length);
                batchOn = oldOn; batchVerify = oldVerify; MoveApply.DeferRotScale = oldDefer;
                setPos = oldPos; setPosX = oldX; setPosY = oldY; setRot = oldRot; setScale = oldScale;
                Steps = oldSteps; PairSteps = oldPairs; batchRecords = oldRecords;
            }
        }
        internal static string BatchCommand(string arg)
        {
            switch (arg.Trim())
            {
                case "verify": batchVerify = !batchVerify; return "verify=" + batchVerify + " " + BatchState();
                case "state": return BatchState();
                case "bench": return BatchBench();
                default: return "decobatch verify|state|bench / shadow begin|end|state";
            }
        }
        private static string BatchState() => "enabled=" + batchOn + " allowed=" + BatchAllowed() + " slots=" + batchCount
            + " decorations=" + batchDecorationIds.Count + " frames=" + batchFrames + " records/frame=" + (batchRecords / (double)Math.Max(1, batchFrames)).ToString("F1")
            + " ms/frame=" + (batchTicks * 1000.0 / Stopwatch.Frequency / Math.Max(1, batchFrames)).ToString("F4")
            + " decisions=" + batchDecisions + " completed=" + batchDone + " sync=" + batchSync
            + " checks=" + batchChecks + " mismatch=" + batchMismatch + " first=" + batchFirst
            + " fallback[type,mask,alpha,hidden,defer,key,capacity,error]=" + string.Join(",", batchReasons) + " unsupportedEase=0";
#endif
    }
}
