#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;

namespace StutterFix
{
    internal static partial class LiveDeco
    {
        internal static bool Active => runtime != null;
        internal static int OwnedCount => runtime?.Owned ?? 0;
        static readonly Dictionary<int, long> pixelCounts = new Dictionary<int, long>();
        static readonly List<double> fpsOff = new List<double>(), fpsOn = new List<double>();
        static bool pixelValid = true, restoredValid = true, fpsValid = true, measuring;
        static int fpsFrame, measureIndex, completedMeasures;
        static long fpsClock;
        static string fpsMode;
        static Runtime measuredRuntime;
        static long[] profileStart;
        static bool oldProfile;
        static void CancelGate2Profile()
        { if (profileStart != null) { DecoAnim.LiveDecoProfile = oldProfile; profileStart = null; } }
        static void Gate2Error(string reason) { fpsValid = false; Log2("GATE2=ERROR reason=" + reason + " final_on=" + Active); }
        static bool Gate2Command(string arg)
        {
            var a = arg.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (a.Length == 2 && a[0] == "profile" && a[1] == "begin")
            {
                if (Active || measuring || FrameCompare.Paused || profileStart != null) throw new InvalidOperationException("profile requires baseline playback");
                profileStart = new long[48];
                Array.Copy(DecoAnim.ProfN, 0, profileStart, 0, 16); Array.Copy(DecoAnim.ProfApply, 0, profileStart, 16, 16); Array.Copy(DecoAnim.ProfSet, 0, profileStart, 32, 16);
                oldProfile = DecoAnim.LiveDecoProfile; DecoAnim.LiveDecoProfile = true;
                Log2("PROFILE_BEGIN mode=off fps_evidence=0"); return true;
            }
            if (a.Length == 2 && a[0] == "profile" && a[1] == "end")
            {
                if (profileStart == null) throw new InvalidOperationException("profile not begun");
                long totalApply = 0, totalSet = 0, samples = 0;
                for (int k = 0; k < 16; k++)
                {
                    long n = DecoAnim.ProfN[k] - profileStart[k], apply = DecoAnim.ProfApply[k] - profileStart[16 + k], set = DecoAnim.ProfSet[k] - profileStart[32 + k];
                    totalApply += apply; totalSet += set; samples += n;
                    if (n > 0) Log2("PROFILE key=" + k + " samples=" + n + " apply_us=" + (apply * 1e6 / Stopwatch.Frequency / n).ToString("F6", CultureInfo.InvariantCulture)
                        + " set_us=" + (set * 1e6 / Stopwatch.Frequency / n).ToString("F6", CultureInfo.InvariantCulture));
                }
                Log2("PROFILE_SET_SHARE samples=" + samples + " set_pct=" + (totalApply + totalSet > 0 ? 100.0 * totalSet / (totalApply + totalSet) : double.NaN).ToString("F6", CultureInfo.InvariantCulture)
                    + " scope=sampled-Step-Apply-OnUpdate includes_scale_setters_in_apply=1 full_DecoAnim_share_proven=0");
                CancelGate2Profile(); return true;
            }
            if (a.Length == 1 && a[0] == "reset")
            {
                Stop(); pixelCounts.Clear(); fpsOff.Clear(); fpsOn.Clear();
                pixelValid = restoredValid = fpsValid = true; measuring = false; completedMeasures = 0;
                Log2("reset variant=B"); return true;
            }
            if (a.Length == 5 && a[0] == "pixel")
            {
                int time = int.Parse(a[1], CultureInfo.InvariantCulture);
                if (time != 262 && time != 270 && time != 276) throw new ArgumentException("pixel time");
                bool valid = FrameCompare.Gate2Pixels(time, a[2], a[3], a[4], out long changed, out int max, out string regions, out long restored);
                if (pixelCounts.ContainsKey(time)) pixelValid = false;
                pixelCounts[time] = changed; pixelValid &= valid; restoredValid &= restored == 0 && !Active;
                Log2("PIXEL t=" + time + " ab_out=" + changed + " maxdiff=" + max + " aa2_out=" + restored + " valid=" + valid + " regions=" + regions);
                if (changed > 0 && max <= 1) Log2("PIXEL_NEAR t=" + time + " ab_out=" + changed + " maxdiff=" + max + " counts_as_pass=0 alternative=Texture2DArray-same-size-uv-unmodified");
                return true;
            }
            if (a.Length == 3 && a[0] == "perf" && a[1] == "begin")
            {
                if (measuring || FrameCompare.Paused || !Hitch.Playing) throw new InvalidOperationException("perf begin requires natural playback");
                fpsMode = a[2];
                if (fpsMode != "on" && fpsMode != "off") throw new ArgumentException("perf mode");
                bool expectedOn = completedMeasures == 1 || completedMeasures == 2;
                if (completedMeasures >= 4 || (fpsMode == "on") != expectedOn || Active != expectedOn || Math.Abs(Song2() - 262) > 0.25)
                    throw new InvalidOperationException("perf sequence ABBA/start-window/mode");
                measuredRuntime = runtime; measureIndex = completedMeasures; fpsFrame = Time.frameCount; fpsClock = Stopwatch.GetTimestamp(); measuring = true;
                Log2("PERF_BEGIN mode=" + fpsMode + " song=" + Song2().ToString("F6", CultureInfo.InvariantCulture)); return true;
            }
            if (a.Length == 2 && a[0] == "perf" && a[1] == "end")
            {
                if (!measuring) throw new InvalidOperationException("perf not begun");
                long ticks = Stopwatch.GetTimestamp() - fpsClock; int frames = Time.frameCount - fpsFrame;
                double elapsed = (double)ticks / Stopwatch.Frequency, fps = frames / elapsed;
                bool valid = !FrameCompare.Paused && Hitch.Playing && Math.Abs(Song2() - 276) <= 0.25 && frames > 1 && elapsed > 0
                    && (fpsMode == "on" ? runtime == measuredRuntime && runtime != null && runtime.Owned > 0 && runtimeFailure == "none" : !Active);
                fpsValid &= valid; (fpsMode == "on" ? fpsOn : fpsOff).Add(fps); measuring = false; completedMeasures++;
                Log2("FPS mode=" + fpsMode + " index=" + measureIndex + " fps=" + fps.ToString("F6", CultureInfo.InvariantCulture)
                    + " ms=" + (1000 / fps).ToString("F6", CultureInfo.InvariantCulture) + " frames=" + frames + " seconds=" + elapsed.ToString("F6", CultureInfo.InvariantCulture) + " valid=" + valid);
                return true;
            }
            if (a.Length == 1 && a[0] == "result")
            {
                bool wasActive = Active; StopRuntime();
                bool pixels = pixelValid && pixelCounts.Count == 3;
                int different = 0; foreach (var n in pixelCounts.Values) if (n != 0) different++;
                double off = fpsOff.Count == 2 ? (fpsOff[0] + fpsOff[1]) / 2 : 0, on = fpsOn.Count == 2 ? (fpsOn[0] + fpsOn[1]) / 2 : 0;
                string verdict = LiveDecoAtlasPolicy.Verdict(different, restoredValid && !wasActive, pixels, fpsValid && !measuring && completedMeasures == 4, off, on);
                Log2("GATE2=" + verdict + " reason=" + (verdict == "ERROR" ? "missing-or-invalid-evidence" : verdict == "PIXEL_FAIL" ? "nonzero-pixels" : verdict == "NO_GAIN" ? "fps-gain-below-15pct" : "all-thresholds-met")
                    + " pixel_samples=" + pixelCounts.Count + " off_fps=" + off.ToString("F6", CultureInfo.InvariantCulture) + " on_fps=" + on.ToString("F6", CultureInfo.InvariantCulture)
                    + " gain_pct=" + (off > 0 ? 100 * (on / off - 1) : double.NaN).ToString("F6", CultureInfo.InvariantCulture) + " final_on=False"); return true;
            }
            throw new ArgumentException("livedeco on|off|state|census|reset|profile begin/end|pixel T A B A2|perf begin off/on|perf end|result");
        }
        static double Song2() => scrConductor.instance ? scrConductor.instance.songposition_minusi : double.NaN;
        static void Log2(string s) => Main.Entry.Logger.Log("[livedeco2] " + s);
    }
}
#endif
