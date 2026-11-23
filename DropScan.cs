#if AUTOTEST
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace StutterFix
{
    // Update 시작점 사이의 구간. Frame은 구간 시작점에서 공개한 Unity 프레임 번호다.
    internal static class DropScan
    {
        private const int Capacity = 600000;
        private struct Row { internal int Frame; internal float Song, EndSong, Cpu, Gpu; internal long Start, End, Cost; internal bool Playing; }
        private static Row[] rows;
        private static int count, previousFrame;
        private static long previousStamp, skipped;
        private static float previousSong;
        private static bool active, previousPlaying;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal static bool Active => active;

        internal static string Begin()
        {
            if (active) return "이미 기록 중";
            rows = new Row[Capacity]; count = 0; skipped = 0; previousStamp = 0; active = true;
            return "시작: 최대 600000프레임, Update 시작점 간격 기록";
        }

        internal static void Tick(long stamp, int frame, float song, bool playing, float cpu, float gpu)
        {
            if (!active) return;
            long started = Stopwatch.GetTimestamp();
            if (previousStamp != 0)
            {
                if (count < Capacity)
                {
                    rows[count] = new Row { Frame = previousFrame, Song = previousSong, EndSong = song,
                        Start = previousStamp, End = stamp, Cpu = cpu, Gpu = gpu, Playing = previousPlaying };
                    rows[count++].Cost = Stopwatch.GetTimestamp() - started;
                }
                else skipped++;
            }
            previousStamp = stamp; previousFrame = frame; previousSong = song; previousPlaying = playing;
        }

        internal static string End(string directory)
        {
            if (!active && rows == null) return "시작 안 함";
            active = false; // 마지막 미완료 구간 및 파일 쓰기 시간은 기록에서 제외한다.
            string path = Path.Combine(directory, "dropscan-frames.csv");
            using (var w = new StreamWriter(path, false, new System.Text.UTF8Encoding(false)))
            {
                w.WriteLine("frame,song_s,song_end_s,wall_start_s,wall_end_s,frame_ms,cpu_delayed_ms,gpu_delayed_ms,playing,record_us");
                for (int i = 0; i < count; i++)
                {
                    var r = rows[i];
                    w.WriteLine(string.Join(",", r.Frame.ToString(Inv), r.Song.ToString("R", Inv), r.EndSong.ToString("R", Inv),
                        (r.Start / (double)Stopwatch.Frequency).ToString("R", Inv), (r.End / (double)Stopwatch.Frequency).ToString("R", Inv),
                        ((r.End - r.Start) * 1000.0 / Stopwatch.Frequency).ToString("R", Inv), r.Cpu.ToString("R", Inv), r.Gpu.ToString("R", Inv),
                        r.Playing ? "1" : "0", (r.Cost * 1000000.0 / Stopwatch.Frequency).ToString("R", Inv)));
                }
            }
            File.WriteAllText(Path.Combine(directory, "dropscan-meta.json"), "{\"frames\":" + count + ",\"capacity\":" + Capacity +
                ",\"skipped_frames\":" + skipped + ",\"truncated\":" + (skipped > 0 ? "true" : "false") + "}");
            rows = null;
            return "저장 " + path + " / " + count + "프레임, 상한 초과 누락 " + skipped;
        }
    }
}
#endif
