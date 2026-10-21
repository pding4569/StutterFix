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
        static bool profiling, previousProf;
        internal static void RestoreProfile()
        {
            if (!profiling) return;
            DecoAnim.Prof = previousProf;
            profiling = false;
        }
        static double Song => scrConductor.instance ? scrConductor.instance.songposition_minusi : double.NaN;
        static void Log(string s) => Main.Entry.Logger.Log("[livedeco2] " + s);
        internal static void Fail(string reason) { if (evidence.Error == null) evidence.Error = reason; }
        internal static void OwnedLow(int owned, int awake)
        {
            if (!running) return;
            running = false; Fail("owned_lt_800_within_5s");
            RestoreProfile();
            Log("GATE2=OWNED_LOW reason=" + evidence.Error + " owned=" + owned + " awake=" + awake + " variant=A on=False");
            LiveDeco.Stop();
            AutoTest.StopLiveDecoTest("GATE2=OWNED_LOW");
        }
        internal static void RendererStopped() { if (running && window >= 0 && mode) Fail("renderer_stopped_in_window"); }
        internal static void RendererStarted() { if (running && window >= 0 && !mode) Fail("renderer_started_in_off_window"); }
        internal static void Abort(string reason)
        {
            RestoreProfile();
            if (!running) return;
            running = false; Fail("autotest_aborted:" + reason);
            LiveDeco.Stop(); Log("GATE2=ERROR reason=" + evidence.Error + " variant=A on=False");
        }
        internal static bool Step(string arg)
        {
            try { return StepCore(arg); }
            catch (Exception ex) { Log("ERROR " + ex.ToString()); throw; }
        }
        static bool StepCore(string arg)
        {
            var a = arg.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (a.Length == 1 && a[0] == "profilebegin")
            {
                if (profiling) throw new InvalidOperationException("profile already started");
                if (LiveDecoRenderer.Active || FrameCompare.Paused) throw new InvalidOperationException("profile baseline must be off and playing");
                for (int k = 0; k < 16; k++)
                { profileN[k] = DecoAnim.ProfN[k]; profileApply[k] = DecoAnim.ProfApply[k]; profileSet[k] = DecoAnim.ProfSet[k]; }
                previousProf = DecoAnim.Prof; profiling = true; DecoAnim.Prof = true;
                Log("PROFILE begin enabled=" + DecoAnim.Prof + " mode=off previous=" + previousProf
                    + " pairxy_suppressed=1 groupstep_suppressed=1"); return true;
            }
            if (a.Length == 1 && a[0] == "profileend")
            {
                if (!profiling) throw new InvalidOperationException("profile not started");
                RestoreProfile(); // Keep profiling out of all pixel/FPS windows, even if logging fails.
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
                    + " scope=DecoAnim.Step-sampled-Apply-and-OnUpdate excludes=StepPair-OnComplete-engine-work-after-setter"
                    + " baseline_path_unchanged=0 prof_restored=" + DecoAnim.Prof + " valid=" + (samples > 0));
                if (samples == 0) Fail("profile_no_samples");
                return true;
            }
            if (a.Length == 1 && a[0] == "reset")
            { RestoreProfile(); LiveDeco.Stop(); evidence = new LiveDecoGate2Policy(); window = -1; running = true; Log("reset variant=A"); return true; }
            if (a.Length == 2 && a[0] == "ready")
            {
                bool on = a[1] == "on";
                if (a[1] != "on" && a[1] != "off") throw new ArgumentException("mode on|off");
                if (!running || window != -1 || !FrameCompare.Paused || LiveDecoRenderer.Active != on)
                    throw new InvalidOperationException("preparation requires paused matching renderer, outside FPS window");
                if (on && !LiveDecoRenderer.Ready) return false; // Wait for the selected camera's first PreCull.
                double song = Song;
                if ((on && LiveDecoRenderer.Owned == 0) || double.IsNaN(song) || double.IsInfinity(song) || Math.Abs(song - 258) > .5)
                    throw new InvalidOperationException("invalid ABBA preparation song=" + song.ToString("F6", Inv)
                        + " owned=" + LiveDecoRenderer.Owned);
                Log("prepared mode=" + a[1] + " song=" + song.ToString("F6", Inv)
                    + " paused=True ready=" + (!on || LiveDecoRenderer.Ready)
                    + " transition_ms=" + LiveDecoRenderer.TransitionMs.ToString("F6", Inv)
                    + " resume_then_play_to=262 minimum_on_warmup_s=3");
                return true;
            }
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
                if (result.Changed != 0 && max <= 2) Log("PIXEL_NEAR t=" + t + " ab_out=" + result.Changed + " maxdiff=" + max + " pass=0");
                return true;
            }
            if (a.Length == 3 && a[0] == "begin")
            {
                if (window != -1) throw new InvalidOperationException("window already open");
                bool nextMode = a[1] == "on";
                if (a[1] != "on" && a[1] != "off") throw new ArgumentException("mode on|off");
                int nextWindow = int.Parse(a[2], Inv); double nextSong = Song;
                if (nextWindow < 0 || nextWindow > 3 || nextMode != LiveDecoGate2Policy.On(nextWindow) || FrameCompare.Paused
                    || nextMode != LiveDecoRenderer.Active || (nextMode && (!LiveDecoRenderer.Ready || LiveDecoRenderer.Owned == 0))
                    || (nextMode && LiveDecoRenderer.EnabledSeconds < 3)
                    || double.IsNaN(nextSong) || double.IsInfinity(nextSong) || Math.Abs(nextSong - 262) > 0.5)
                    throw new InvalidOperationException("invalid ABBA window start index=" + nextWindow + " mode=" + a[1]
                        + " song=" + nextSong.ToString("F6", Inv) + " paused=" + FrameCompare.Paused
                        + " active=" + LiveDecoRenderer.Active + " ready=" + LiveDecoRenderer.Ready + " owned=" + LiveDecoRenderer.Owned
                        + " warmup_s=" + LiveDecoRenderer.EnabledSeconds.ToString("F3", Inv)
                        + " transition_ms=" + LiveDecoRenderer.TransitionMs.ToString("F6", Inv));
                // Commit only an accepted window; a rejected begin cannot poison lifecycle tracking.
                mode = nextMode; window = nextWindow; songStart = nextSong;
                frame = Time.frameCount; clock = Stopwatch.GetTimestamp();
                Log("window begin index=" + window + " mode=" + a[1] + " song=" + songStart.ToString("F6", Inv)
                    + " warmup_excluded_s=" + (mode ? LiveDecoRenderer.EnabledSeconds.ToString("F3", Inv) : "0")
                    + " minimum_on_warmup_s=3 transition_ms=" + LiveDecoRenderer.TransitionMs.ToString("F6", Inv)); return true;
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
                RestoreProfile();
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
            throw new ArgumentException("livedeco2 reset|profilebegin|profileend|ready on/off|pixel 262/270/276|begin on/off index|end|result");
        }
    }
}
#endif
