using System;
using System.IO;
using DG.Tweening;
using HarmonyLib;

namespace StutterFix
{
    // Experimental: default off until in-game bit comparison has passed.
    // Only Linear is admitted: its installed DOTween implementation is time / duration.
    internal static class UpdateEvalMemo
    {
        internal static bool Enabled, Verify;
        internal static long Calls, Hits, Checks, Mismatch;
        private static bool valid;
        private static int timeBits, durationBits, overBits, periodBits;
        private static float value;
        internal static void Configure(string dir)
        {
            Enabled = false; valid = false;
            Calls = Hits = Checks = Mismatch = 0;
#if DEV || AUTOTEST
            if (!File.Exists(Path.Combine(dir, "evalmemo-on")) || File.Exists(Path.Combine(dir, "evalmemo-off"))) return;
            // Even a mathematically pure original can have an impure external patch.
            var t = AccessTools.TypeByName("DG.Tweening.Core.Easing.EaseManager");
            if (t == null) return;
            foreach (var m in t.GetMethods(AccessTools.all))
                if (m.Name == "Evaluate")
                {
                    var info = Harmony.GetPatchInfo(m);
                    if (info != null) foreach (var owner in info.Owners) return;
                }
            Enabled = true;
#endif
        }
        internal static void NewFrame() { valid = false; }
        internal static float Eval(Ease ease, float time, float duration, float over, float period)
        {
            Calls++;
            if (ease != Ease.Linear) { valid = false; return ZeroTween.Eval(ease, time, duration, over, period); }
            int t = InstantMove.Bits(time), d = InstantMove.Bits(duration), o = InstantMove.Bits(over), p = InstantMove.Bits(period);
            if (valid && t == timeBits && d == durationBits && o == overBits && p == periodBits)
            {
                Hits++;
#if DEV || AUTOTEST
                if (Verify && (Hits & 63) == 0)
                {
                    float expected = ZeroTween.Eval(ease, time, duration, over, period);
                    Checks++;
                    if (InstantMove.Bits(value) != InstantMove.Bits(expected))
                    { Mismatch++; Enabled = false; valid = false; return expected; }
                }
#endif
                return value;
            }
            // Store only after evaluation succeeds; exceptions cannot publish a new entry.
            valid = false;
            float result = ZeroTween.Eval(ease, time, duration, over, period);
            timeBits = t; durationBits = d; overBits = o; periodBits = p; value = result; valid = true;
            return result;
        }
    }
}
