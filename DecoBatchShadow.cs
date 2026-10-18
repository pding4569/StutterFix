using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DG.Tweening;
using UnityEngine;

namespace StutterFix
{
    internal static partial class DecoAnim
    {
        static partial void BatchShadowStart(Rec r) { if (DecoBatchShadow.On) try { DecoBatchShadow.Add(r); } catch (Exception ex) { DecoBatchShadow.Fail(ex); } }
        static partial void BatchShadowCancel(Rec r) { if (DecoBatchShadow.On) try { DecoBatchShadow.Remove(r); } catch (Exception ex) { DecoBatchShadow.Fail(ex); } }
        static partial void BatchShadowBegin(float td) { if (DecoBatchShadow.On) try { DecoBatchShadow.FrameBegin(td); } catch (Exception ex) { DecoBatchShadow.Fail(ex); } }
        static partial void BatchShadowEnd() { if (DecoBatchShadow.On) try { DecoBatchShadow.FrameEnd(); } catch (Exception ex) { DecoBatchShadow.Fail(ex); } }
        static partial void BatchShadowScale(Rec r, Vector2 v) { if (DecoBatchShadow.On) try { DecoBatchShadow.ScaleInput(r, v); } catch (Exception ex) { DecoBatchShadow.Fail(ex); } }
        internal static float BatchRead(scrDecoration d, int key)
        {
            switch (key) { case 1: return pivotPosRef(d).x; case 2: return pivotPosRef(d).y;
                case 5: return rotRef(d); case 7: return scaleRef(d).x; case 8: return scaleRef(d).y; default: return 0; }
        }
    }

    // Explicit opt-in only. Arrays own numeric state; no setters, patches, or native allocations.
    // Lifecycle timers run only on enrollment/removal, never once per steady-state record.
    internal static unsafe class DecoBatchShadow
    {
        internal const int Capacity = 65536;
        internal static bool On;
        private static DecoAnim.Rec[] records;
        private static scrDecoration[] decorations;
        private static int[] decorationRefs, freeDecorations, decorationIndex, keys, eases, mdone, ndone;
        private static readonly Dictionary<scrDecoration, int> decorationIds = new Dictionary<scrDecoration, int>();
        private static readonly Dictionary<string, long> unsupported = new Dictionary<string, long>();
        private static float[] duration, position, nativePosition, start, change, over, period, managed, native;
        private static int count, decoCount, freeCount, peak;
        private static float delta, readSink;
        private static bool inFrame, nativeOk;
        private static long frames, checks, mismatch, nativeChecks, nativeMismatch, overflow, added, removed;
        private static long maintenanceTicks, managedTicks, nativeTicks, compareTicks, readTicks, maxMaintenance;
        private static string first = "", nativeError = "";
        private static string shadowError = "";
        private static readonly double TickMs = 1000.0 / Stopwatch.Frequency;

        [DllImport("sfnative", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sf_decobatch_step(int n, int* ease, int* key, float* dur, float* pos,
            float* s, float* c, float* ov, float* pe, float* output, int* done, float td);

        internal static string Command(string arg)
        {
            switch (arg.Trim()) { case "shadow begin": return Begin(); case "shadow end": string s = State(); Stop(); return s;
                case "shadow state": return State(); default: return "decobatch shadow begin|end|state"; }
        }
        internal static string Begin()
        {
            Stop();
#if AUTOTEST
            if (DecoAnim.AblateMask != 0) return "시작 거부: decoablate를 먼저 끄세요";
#endif
            records = new DecoAnim.Rec[Capacity]; decorations = new scrDecoration[Capacity];
            decorationRefs = new int[Capacity]; freeDecorations = new int[Capacity]; decorationIndex = new int[Capacity];
            keys = new int[Capacity]; eases = new int[Capacity]; mdone = new int[Capacity]; ndone = new int[Capacity];
            duration = new float[Capacity]; position = new float[Capacity]; nativePosition = new float[Capacity];
            start = new float[Capacity * 4]; change = new float[Capacity * 4];
            over = new float[Capacity]; period = new float[Capacity]; managed = new float[Capacity * 4]; native = new float[Capacity * 4];
            decorationIds.EnsureCapacity(Capacity);
            frames = checks = mismatch = nativeChecks = nativeMismatch = overflow = added = removed = 0;
            maintenanceTicks = managedTicks = nativeTicks = compareTicks = readTicks = maxMaintenance = 0;
            peak = 0; first = nativeError = shadowError = ""; unsupported.Clear(); nativeOk = true; On = true;
            // One initial adoption pass, outside measured frames. New starts enroll at Startup.
            foreach (var r in DecoAnim.ProbeRecords) if (r.Running && r.Started) Add(r);
            maintenanceTicks = maxMaintenance = 0;
            return "시작 (제품 필드 쓰기 0, 초기 배열 할당은 측정 제외)";
        }
        internal static void Stop()
        {
            On = inFrame = false;
            if (records != null) for (int i = 0; i < count; i++) { records[i].BatchShadowSlot = -1; records[i] = null; }
            decorationIds.Clear();
            if (decorations != null) Array.Clear(decorations, 0, decorations.Length);
            count = decoCount = freeCount = 0;
            records = null; decorations = null; decorationRefs = freeDecorations = decorationIndex = keys = eases = mdone = ndone = null;
            duration = position = nativePosition = start = change = over = period = managed = native = null;
        }
        internal static void Fail(Exception ex) { shadowError = ex.GetType().Name + ": " + ex.Message; Stop(); }
        internal static int EaseCode(Ease e)
        {
            switch (e) { case Ease.Linear: return 0; case Ease.InQuad: return 1; case Ease.OutQuad: return 2;
                case Ease.InCubic: return 3; case Ease.OutCubic: return 4; case Ease.InOutQuad: return 5;
                case Ease.InOutCubic: return 6; default: return -1; }
        }
        internal static void Add(DecoAnim.Rec r)
        {
            if (r.BatchShadowSlot >= 0) return;
            long t = Stopwatch.GetTimestamp();
            if (count == Capacity) { overflow++; Maintenance(t); return; }
            int i = count++, b = i * 4, id;
            records[i] = r; r.BatchShadowSlot = i;
            if (!decorationIds.TryGetValue(r.D, out id))
            { id = freeCount > 0 ? freeDecorations[--freeCount] : decoCount++; decorationIds.Add(r.D, id); decorations[id] = r.D; }
            decorationRefs[id]++; decorationIndex[i] = id;
            keys[i] = r.Key; eases[i] = EaseCode(r.E);
            if (!(r.Key == 1 || r.Key == 2 || r.Key == 5 || r.Key == 7 || r.Key == 8 || r.Key == 9 || r.Key == 10) || eases[i] < 0)
            {
                string why = "key=" + r.Key + "/ease=" + r.E; long n;
                unsupported.TryGetValue(why, out n); unsupported[why] = n + 1; eases[i] = -1;
            }
            duration[i] = r.Dur; position[i] = nativePosition[i] = r.Pos; over[i] = r.Over; period[i] = r.Period;
            for (int j = 0; j < 4; j++) { start[b+j] = 0; change[b+j] = 0; }
            if (r.Key == 9)
            { start[b] = r.CStart.r; start[b+1] = r.CStart.g; start[b+2] = r.CStart.b; start[b+3] = r.CStart.a;
                change[b] = r.CChange.r; change[b+1] = r.CChange.g; change[b+2] = r.CChange.b; change[b+3] = r.CChange.a; }
            else if (r.Key == 7 || r.Key == 8)
            { start[b] = r.VStart.x; start[b+1] = r.VStart.y; change[b] = r.VChange.x; change[b+1] = r.VChange.y; }
            else { start[b] = r.FStart; change[b] = r.FChange; }
            added++; if (count > peak) peak = count; Maintenance(t);
        }
        internal static void ScaleInput(DecoAnim.Rec r, Vector2 v)
        {
            int i = r.BatchShadowSlot; if (i < 0) return;
            // Preserve the other axis at its actual ordered setter boundary, including signed zero.
            start[i*4 + (r.Key == 7 ? 1 : 0)] = r.Key == 7 ? v.y : v.x;
        }
        internal static void Remove(DecoAnim.Rec r)
        {
            int i = r.BatchShadowSlot; if (i < 0) return;
            long t = Stopwatch.GetTimestamp(); RemoveAt(i); Maintenance(t);
        }
        private static void RemoveAt(int i)
        {
            var r = records[i]; r.BatchShadowSlot = -1;
            int id = decorationIndex[i];
            if (--decorationRefs[id] == 0) { decorationIds.Remove(decorations[id]); decorations[id] = null; freeDecorations[freeCount++] = id; }
            int last = --count;
            if (i != last)
            {
                records[i] = records[last]; records[i].BatchShadowSlot = i;
                decorationIndex[i] = decorationIndex[last]; keys[i] = keys[last]; eases[i] = eases[last];
                duration[i] = duration[last]; position[i] = position[last]; nativePosition[i] = nativePosition[last];
                over[i] = over[last]; period[i] = period[last];
                Array.Copy(start, last*4, start, i*4, 4); Array.Copy(change, last*4, change, i*4, 4);
            }
            records[last] = null; removed++;
        }
        private static void Maintenance(long t)
        { long n = Stopwatch.GetTimestamp() - t; maintenanceTicks += n; }
        private static long maintenanceAt;
        internal static void FrameBegin(float td)
        {
#if AUTOTEST
            if (DecoAnim.AblateMask != 0) { Fail(new InvalidOperationException("decoablate enabled during shadow")); return; }
#endif
            delta = td; inFrame = true; maintenanceAt = maintenanceTicks;
        }

        // Keep exactly the operation order of DOTween EaseManager.Evaluate. No delegates per row.
        internal static float Eval(int e, float t, float d)
        {
            switch (e) {
                case 0: return t / d;
                case 1: t /= d; return t * t;
                case 2: t /= d; return -t * (t - 2);
                case 3: t /= d; return t * t * t;
                case 4: t = t / d - 1; return t * t * t + 1;
                case 5: t /= d * .5f; if (t < 1) return .5f * t * t; --t; return -.5f * (t * (t - 2) - 1);
                case 6: t /= d * .5f; if (t < 1) return .5f * t * t * t; t -= 2; return .5f * (t * t * t + 2);
                default: return float.NaN;
            }
        }
        internal static void ManagedStep(int n, int[] ease, int[] key, float[] dur, float[] pos,
            float[] s, float[] c, float[] output, int[] done, float td)
        {
            for (int i = 0; i < n; i++)
            {
                if (ease[i] < 0 || ease[i] > 6 || !(key[i] == 1 || key[i] == 2 || key[i] == 5 || key[i] == 7 || key[i] == 8 || key[i] == 9 || key[i] == 10)) { done[i] = -1; continue; }
                float p = pos[i] + td; bool end = p >= dur[i]; if (end) p = dur[i];
                pos[i] = p; done[i] = end ? 1 : 0; float e = Eval(ease[i], p, dur[i]); int b = i*4;
                int lanes = key[i] == 9 ? 4 : key[i] == 7 || key[i] == 8 ? 2 : 1;
                for (int j = 0; j < lanes; j++)
                    output[b+j] = (key[i] == 7 && j == 1) || (key[i] == 8 && j == 0) ? s[b+j] : s[b+j] + c[b+j] * e;
            }
        }
        internal static void FrameEnd()
        {
            if (!inFrame) return; inFrame = false; frames++;
            long t = Stopwatch.GetTimestamp();
            ManagedStep(count, eases, keys, duration, position, start, change, managed, mdone, delta);
            managedTicks += Stopwatch.GetTimestamp() - t;
            if (nativeOk)
            {
                t = Stopwatch.GetTimestamp();
                try {
                    fixed (int* e = eases, k = keys, dn = ndone)
                    fixed (float* d = duration, p = nativePosition, s = start, c = change, ov = over, pe = period, o = native)
                        if (sf_decobatch_step(count, e, k, d, p, s, c, ov, pe, o, dn, delta) != 1)
                            throw new InvalidOperationException("native returned failure");
                }
                catch (Exception ex) { nativeOk = false; nativeError = ex.GetType().Name + ": " + ex.Message; }
                nativeTicks += Stopwatch.GetTimestamp() - t;
            }
            t = Stopwatch.GetTimestamp();
            for (int i = 0; i < count; i++)
            {
                if (eases[i] < 0) continue;
                Check(i, managed, position, mdone, false);
                if (nativeOk) Check(i, native, nativePosition, ndone, true);
            }
            compareTicks += Stopwatch.GetTimestamp() - t;
            t = Stopwatch.GetTimestamp(); float sink = 0;
            for (int i = 0; i < count; i++) if (eases[i] >= 0 && decorations[decorationIndex[i]] != null)
                sink += DecoAnim.BatchRead(decorations[decorationIndex[i]], keys[i]);
            readSink = sink; readTicks += Stopwatch.GetTimestamp() - t;
            t = Stopwatch.GetTimestamp();
            for (int i = count-1; i >= 0; i--) if (!records[i].Running) RemoveAt(i);
            Maintenance(t); maxMaintenance = Math.Max(maxMaintenance, maintenanceTicks - maintenanceAt);
        }
        private static bool Bits(float a, float b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);
        private static void Check(int i, float[] output, float[] pos, int[] done, bool isNative)
        {
            var r = records[i]; int b = i*4;
            bool equal = Bits(pos[i], r.Pos) && (done[i] != 0) == !r.Running;
            float expected = r.Key == 7 ? r.VLast.x : r.Key == 8 ? r.VLast.y : r.Key == 9 ? r.CLast.r : r.FLast;
            float actual = output[b + (r.Key == 8 ? 1 : 0)]; equal &= Bits(expected, actual);
            if (r.Key == 7 || r.Key == 8) equal &= Bits(output[b], r.VLast.x) && Bits(output[b+1], r.VLast.y);
            if (r.Key == 9) equal &= Bits(output[b+1], r.CLast.g) && Bits(output[b+2], r.CLast.b) && Bits(output[b+3], r.CLast.a);
            if (isNative) { nativeChecks++; if (!equal) nativeMismatch++; } else { checks++; if (!equal) mismatch++; }
            if (!equal && first.Length == 0)
            {
                int lane = r.Key == 8 ? 1 : 0;
                if (r.Key == 7 || r.Key == 8)
                { lane = !Bits(output[b], r.VLast.x) ? 0 : 1; expected = lane == 0 ? r.VLast.x : r.VLast.y; }
                if (r.Key == 9)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        float v = j == 0 ? r.CLast.r : j == 1 ? r.CLast.g : j == 2 ? r.CLast.b : r.CLast.a;
                        if (!Bits(v, output[b+j])) { lane = j; expected = v; break; }
                    }
                }
                actual = output[b+lane];
                first = $"{(isNative ? "native" : "managed")} key={r.Key} ease={r.E} lane={lane} td={delta:R} Pos={r.Pos:R}/{pos[i]:R} Dur={r.Dur:R} value={expected:R}/{actual:R} bits={BitConverter.SingleToInt32Bits(expected):X8}/{BitConverter.SingleToInt32Bits(actual):X8} done={!r.Running}/{done[i]}";
            }
        }
        internal static string State()
        {
            double f = Math.Max(1, frames);
            return $"on={On} frames={frames} slots={count} peak={peak} add/remove={added}/{removed} overflow={overflow} " +
                $"managed checks/diff={checks}/{mismatch} native checks/diff={nativeChecks}/{nativeMismatch} nativeOK={nativeOk} " +
                $"ms/frame managed={managedTicks*TickMs/f:F4} native(PInvoke+fixed)={nativeTicks*TickMs/f:F4} " +
                $"maintenance={maintenanceTicks*TickMs/f:F4} maintenancePeak={maxMaintenance*TickMs:F4} " +
                $"readonlyField={readTicks*TickMs/f:F4} compare={compareTicks*TickMs/f:F4} " +
                $"managedTotal={(managedTicks+maintenanceTicks+readTicks)*TickMs/f:F4} nativeTotal={(nativeTicks+maintenanceTicks+readTicks)*TickMs/f:F4} " +
                $"미지원(등록수)={string.Join(", ", unsupported)} first={first} nativeError={nativeError} shadowError={shadowError} sink={readSink:R}";
        }
    }
}
