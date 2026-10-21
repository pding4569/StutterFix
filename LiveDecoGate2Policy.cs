using System;

namespace StutterFix
{
    // Pure evidence gate. Near matches, missing samples and restoration failures
    // cannot be rounded into PASS. FPS is arithmetic mean of the two windows.
    internal sealed class LiveDecoGate2Policy
    {
        internal readonly long[] Pixels = { -1, -1, -1 };
        internal readonly int[] MaxDiff = { -1, -1, -1 };
        internal readonly double[] Fps = { double.NaN, double.NaN, double.NaN, double.NaN };
        internal string Error;
        internal static bool On(int index) => index == 1 || index == 2;
        internal void Pixel(int index, long changed, int max, bool restored, bool stable)
        {
            if (index < 0 || index >= 3 || Pixels[index] >= 0 || changed < 0 || max < 0 || max > 255)
            { Error = "invalid_pixel_sample"; return; }
            Pixels[index] = changed; MaxDiff[index] = max;
            if (!restored) Error = "restoration_failed";
            if (!stable) Error = "unstable_baseline";
        }
        internal void Window(int index, bool on, double fps, bool valid)
        {
            if (index < 0 || index >= 4 || !double.IsNaN(Fps[index]) || on != On(index)
                || !valid || double.IsNaN(fps) || double.IsInfinity(fps) || fps <= 0)
            { Error = "invalid_fps_window"; return; }
            for (int n = 0; n < index; n++) if (double.IsNaN(Fps[n])) { Error = "abba_order"; return; }
            Fps[index] = fps;
        }
        internal double Gain => ((Fps[1] + Fps[2]) / (Fps[0] + Fps[3]) - 1) * 100;
        internal string Verdict(out string reason)
        {
            reason = Error;
            if (reason != null) return "ERROR";
            foreach (long p in Pixels) if (p < 0) { reason = "missing_pixel_sample"; return "ERROR"; }
            foreach (double f in Fps) if (double.IsNaN(f)) { reason = "missing_fps_window"; return "ERROR"; }
            foreach (long p in Pixels) if (p != 0) { reason = "outside_pixels_changed"; return "PIXEL_FAIL"; }
            if ((Fps[1] + Fps[2]) < (Fps[0] + Fps[3]) * 1.15)
            { reason = "fps_gain_lt_15pct"; return "NO_GAIN"; }
            reason = "three_exact_pixels_abba_gain_ge_15pct"; return "PASS";
        }
    }
}
