#if AUTOTEST
using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace StutterFix
{
    // Sample complete PlayerLoop iterations on the main thread. CPU cycles
    // rank execution work. Stopwatch intervals include scheduling and waits.
    internal sealed class PhaseCycleProbe
    {
        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentThread();
        [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool QueryThreadCycleTime(IntPtr thread, out ulong cycles);

        readonly string[] names;
        readonly ulong[] samples, totals;
        readonly byte[] requiredMarkers;
        readonly PhaseCallbackProbe callbacks;
        readonly int fixedFirst, fixedLast;
        readonly IntPtr thread;
        int lastIndex = -1, countdown;
        int requiredSeen;
        uint random = 920117;
        ulong previous, totalCycles;
        long readCalls, readTicks, frameReadTicks, maxFrameReadTicks;
        int seen, completed, badOrder, failedReads, skippedMarkers, fixedRepeats;

        internal int CallbackPhase => lastIndex < 0 ? -1 : requiredMarkers[lastIndex] == 1 ? 0 : requiredMarkers[lastIndex] == 2 ? 1 : -1;
        internal int SampleFrame => seen;
        internal bool ReadCallbackCycles(out ulong cycles) => Read(out cycles);

        internal PhaseCycleProbe(string[] markerNames, PhaseCallbackProbe callbackProbe = null)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || markerNames.Length < 2)
                throw new InvalidOperationException("Windows PlayerLoop cycle sampling unavailable");
            names = markerNames;
            callbacks = callbackProbe;
            samples = new ulong[names.Length];
            totals = new ulong[names.Length];
            requiredMarkers = new byte[names.Length];
            int required = 0;
            fixedFirst = fixedLast = -1;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i].StartsWith("FixedUpdate/", StringComparison.Ordinal))
                {
                    if (fixedFirst < 0) fixedFirst = i;
                    fixedLast = i;
                }
                if (names[i] == "Update/ScriptRunBehaviourUpdate") requiredMarkers[i] = 1;
                if (names[i] == "PreLateUpdate/ScriptRunBehaviourLateUpdate") requiredMarkers[i] = 2;
                if (names[i] == "PostLateUpdate/FinishFrameRendering") requiredMarkers[i] = 4;
                required |= requiredMarkers[i];
            }
            if (required != 7) throw new InvalidOperationException("Required CPU phase boundaries unavailable");
            thread = GetCurrentThread();
            if (!QueryThreadCycleTime(thread, out _))
                throw new InvalidOperationException("QueryThreadCycleTime unavailable");
        }

        bool Read(out ulong cycles)
        {
            long start = Stopwatch.GetTimestamp();
            bool success = QueryThreadCycleTime(thread, out cycles);
            long elapsed = Stopwatch.GetTimestamp() - start;
            readCalls++;
            readTicks += elapsed;
            frameReadTicks += elapsed;
            if (!success) failedReads++;
            return success;
        }

        internal void Record(int index)
        {
            if (index == 0)
            {
                seen++;
                if (lastIndex == names.Length - 1 && requiredSeen == 7 && Read(out ulong end) && end >= previous)
                {
                    samples[names.Length - 1] = end - previous;
                    for (int i = 0; i < totals.Length; i++)
                    {
                        totals[i] += samples[i];
                        totalCycles += samples[i];
                    }
                    completed++;
                    callbacks?.CompleteFrame();
                    maxFrameReadTicks = Math.Max(maxFrameReadTicks, frameReadTicks);
                }
                else if (lastIndex >= 0) badOrder++;
                lastIndex = -1;
                if (--countdown > 0) return;
                random = unchecked(random * 1664525u + 1013904223u);
                countdown = 13 + (int)((random >> 16) & 7u);
                frameReadTicks = 0;
                requiredSeen = 0;
                Array.Clear(samples, 0, samples.Length);
                if (Read(out previous))
                {
                    lastIndex = 0;
                    callbacks?.BeginFrame();
                }
                return;
            }
            if (lastIndex < 0) return;
            // A rendered frame can omit FixedUpdate or execute it repeatedly.
            // Attribute each interval to the previous marker that actually ran.
            bool fixedRepeat = fixedFirst >= 0 && index == fixedFirst && lastIndex == fixedLast;
            if ((index <= lastIndex && !fixedRepeat) || !Read(out ulong current) || current < previous)
            {
                badOrder++;
                lastIndex = -1;
                return;
            }
            if (fixedRepeat) fixedRepeats++;
            if (index > lastIndex + 1) skippedMarkers += index - lastIndex - 1;
            samples[lastIndex] += current - previous;
            previous = current;
            requiredSeen |= requiredMarkers[index];
            lastIndex = index;
        }

        internal string Report()
        {
            if (completed < 30 || totalCycles == 0)
                throw new InvalidOperationException("Insufficient complete CPU cycle samples");
            var inv = CultureInfo.InvariantCulture;
            var order = new int[names.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) => totals[b].CompareTo(totals[a]));
            double tickMs = 1000d / Stopwatch.Frequency;
            var text = new StringBuilder();
            text.Append("phase_cycles seen=").Append(seen).Append(" complete=").Append(completed)
                .Append(" bad_order=").Append(badOrder).Append(" read_failures=").Append(failedReads)
                .Append(" skipped_markers=").Append(skippedMarkers).Append(" fixed_repeats=").Append(fixedRepeats)
                .Append(" read_calls=").Append(readCalls).Append(" total_cycles=").Append(totalCycles)
                .Append(" read_wall_ms=").Append((readTicks * tickMs).ToString("F4", inv))
                .Append(" max_sample_read_wall_ms=").Append((maxFrameReadTicks * tickMs).ToString("F4", inv))
                .Append(" partial_tail=").Append(lastIndex >= 0 ? 1 : 0);
            foreach (int i in order)
            {
                if (totals[i] == 0) break;
                text.Append(" | ").Append(names[i]).Append(' ').Append(totals[i]).Append(' ')
                    .Append((100d * totals[i] / totalCycles).ToString("F4", inv)).Append('%');
            }
            if (callbacks != null)
            {
                int update = Array.IndexOf(requiredMarkers, (byte)1);
                int late = Array.IndexOf(requiredMarkers, (byte)2);
                text.Append('\n').Append(callbacks.Report(totals[update], totals[late]));
            }
            return text.ToString();
        }
    }
}
#endif
