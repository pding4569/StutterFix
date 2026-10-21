#if DEV || AUTOTEST
using System;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;

namespace StutterFix
{
    internal static class LiveDecoGate2
    {
        static LiveDecoGate2Policy evidence = new LiveDecoGate2Policy();
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static int window = -1, frame;
        static long clock;
        static bool mode;
        static double songStart;
        static bool running;
        static readonly long[] profileN = new long[16], profileApply = new long[16], profileSet = new long[16];
        static bool profiling;
        static double Song => scrConductor.instance ? scrConductor.instance.songposition_minusi : double.NaN;
        static void Log(string s) => Main.Entry.Logger.Log("[livedeco2] " + s);
        internal static void Fail(string reason) { if (evidence.Error == null) evidence.Error = reason; }
        internal static void RendererStopped() { if (running && window >= 0 && mode) Fail("renderer_stopped_in_window"); }
        internal static void RendererStarted() { if (running && window >= 0 && !mode) Fail("renderer_started_in_off_window"); }
        internal static void Abort(string reason)
        {
            if (!running) return;
            running = false; Fail("autotest_aborted:" + reason);
            LiveDeco.Stop(); Log("GATE2=ERROR reason=" + evidence.Error + " variant=A on=False");
        }
        internal static bool Step(string arg)
        {
            var a = arg.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (a.Length == 1 && a[0] == "profilebegin")
            {
                if (LiveDecoRenderer.Active || FrameCompare.Paused) throw new InvalidOperationException("profile baseline must be off and playing");
                for (int k = 0; k < 16; k++)
                { profileN[k] = DecoAnim.ProfN[k]; profileApply[k] = DecoAnim.ProfApply[k]; profileSet[k] = DecoAnim.ProfSet[k]; }
                profiling = true; Log("PROFILE begin enabled=" + DecoAnim.Prof + " mode=off"); return true;
            }
            if (a.Length == 1 && a[0] == "profileend")
            {
                if (!profiling) throw new InvalidOperationException("profile not started");
                long totalApply = 0, totalSet = 0, samples = 0;
                for (int k = 1; k <= 13; k++)
                {
                    long n = DecoAnim.ProfN[k] - profileN[k], apply = DecoAnim.ProfApply[k] - profileApply[k], set = DecoAnim.ProfSet[k] - profileSet[k];
                    if (n <= 0) continue;
                    totalApply += apply; totalSet += set; samples += n;
                    Log("PROFILE key=" + k + " samples=" + n + " apply_us=" + (apply * 1e6 / Stopwatch.Frequency / n).ToString("F6", Inv)
                        + " set_us=" + (set * 1e6 / Stopwatch.Frequency / n).ToString("F6", Inv));
                }
                Log("PROFILE samples=" + samples + " set_pct=" + (100.0 * totalSet / Math.Max(1, totalApply + totalSet)).ToString("F6", Inv)
                    + " scope=DecoAnim.Step-sampled-Apply-and-OnUpdate excludes=StepPair-OnComplete-engine-work-after-setter valid=" + (samples > 0));
                profiling = false; return true;
            }
            if (a.Length == 1 && a[0] == "reset")
            { LiveDeco.Stop(); evidence = new LiveDecoGate2Policy(); window = -1; running = true; Log("reset variant=A"); return true; }
            if (a.Length == 2 && a[0] == "pixel")
            {
                int t = int.Parse(a[1], Inv), index = t == 262 ? 0 : t == 270 ? 1 : t == 276 ? 2 : -1;
                if (!FrameCompare.Paused || LiveDecoRenderer.Active || double.IsNaN(Song) || Math.Abs(Song - t) > 1)
                    throw new InvalidOperationException("pixel requires paused, restored sample at requested song time");
                var result = FrameCompare.GatePixel(out long restored, out bool stable);
                int max = Math.Max(Math.Max(result.Max[0], result.Max[1]), Math.Max(result.Max[2], result.Max[3]));
                evidence.Pixel(index, result.Changed, max, restored == 0, stable);
                Log("PIXEL t=" + t + " song=" + Song.ToString("F6", Inv) + " ab_out=" + result.Changed
                    + " maxdiff=" + max + " aa2_out=" + restored + " stable=" + stable + " regions=" + result.Regions);
                if (result.Changed != 0 && max <= 1) Log("PIXEL_NEAR t=" + t + " ab_out=" + result.Changed + " maxdiff=" + max + " pass=0");
                return true;
            }
            if (a.Length == 3 && a[0] == "begin")
            {
                if (window != -1) throw new InvalidOperationException("window already open");
                mode = a[1] == "on";
                if (a[1] != "on" && a[1] != "off") throw new ArgumentException("mode on|off");
                window = int.Parse(a[2], Inv); songStart = Song;
                if (window < 0 || window > 3 || mode != LiveDecoGate2Policy.On(window) || FrameCompare.Paused
                    || mode != LiveDecoRenderer.Active || (mode && LiveDecoRenderer.Owned == 0)
                    || double.IsNaN(songStart) || Math.Abs(songStart - 262) > 0.5)
                    throw new InvalidOperationException("invalid ABBA window start");
                frame = Time.frameCount; clock = Stopwatch.GetTimestamp();
                Log("window begin index=" + window + " mode=" + a[1] + " song=" + songStart.ToString("F6", Inv)); return true;
            }
            if (a.Length == 1 && a[0] == "end")
            {
                if (window == -1) throw new InvalidOperationException("no FPS window");
                double seconds = (Stopwatch.GetTimestamp() - clock) / (double)Stopwatch.Frequency;
                int frames = Time.frameCount - frame; double fps = frames / seconds, songEnd = Song;
                bool valid = !FrameCompare.Paused && mode == LiveDecoRenderer.Active && (!mode || LiveDecoRenderer.Owned > 0)
                    && frames > 0 && seconds > 0 && !double.IsNaN(songEnd) && Math.Abs(songEnd - 276) < 0.5
                    && songEnd > songStart && Hitch.Playing;
                evidence.Window(window, mode, fps, valid);
                Log("FPS mode=" + (mode ? "on" : "off") + " index=" + window + " fps=" + fps.ToString("F6", Inv)
                    + " ms=" + (1000 / fps).ToString("F6", Inv) + " frames=" + frames + " elapsed_s=" + seconds.ToString("F6", Inv)
                    + " song_begin=" + songStart.ToString("F6", Inv) + " song_end=" + songEnd.ToString("F6", Inv) + " valid=" + valid);
                window = -1; return true;
            }
            if (a.Length == 1 && a[0] == "result")
            {
                try
                {
                    if (window != -1) Fail("unfinished_window");
                    LiveDeco.Stop();
                    string verdict = evidence.Verdict(out string reason);
                    Log("GATE2=" + verdict + " reason=" + reason + " gain_pct=" + evidence.Gain.ToString("F6", Inv)
                        + " variant=A on=False");
                }
                finally { running = false; LiveDeco.Stop(); }
                return true;
            }
            throw new ArgumentException("livedeco2 reset|profilebegin|profileend|pixel 262/270/276|begin on/off index|end|result");
        }
    }
}
#endif
