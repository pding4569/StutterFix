using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Reflection;
using System.Collections.Generic;
using StutterFix;

// 게임 참조 없이 실제 기록 코드를 검증한다. 성능 수치는 .NET 8 fixture 한정.
class Program
{
    static void Check(bool ok, string what) { if (!ok) throw new Exception(what); }
    static void Main(string[] args)
    {
        string directory = Path.GetFullPath(args[0]); Directory.CreateDirectory(directory);
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        Check(DropScan.End(directory) == "시작 안 함", "inactive end");
        DropScan.Begin();
        long stamp = Stopwatch.GetTimestamp(), step = Stopwatch.Frequency / 100;
        DropScan.Tick(stamp, 5, -1, false, float.NaN, float.NaN);
        DropScan.Tick(stamp + step, 6, -.99f, true, 3, 2);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew(); // measure allocation after the Stopwatch instance
        allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 2; i <= 600002; i++) DropScan.Tick(stamp + step * i, 5 + i, i * .01f - 1, true, 3, 2);
        long allocationDelta = GC.GetAllocatedBytesForCurrentThread() - allocated;
        sw.Stop();
        Check(allocationDelta == 0, "per-frame allocation=" + allocationDelta);
        string result = DropScan.End(directory);
        int n = 0;
        foreach (string line in File.ReadLines(Path.Combine(directory, "dropscan-frames.csv")))
        {
            if (n == 1)
            {
                var fields = line.Split(',');
                Check(fields.Length == 10, "CSV invariant culture");
                Check(fields[0] == "5" && fields[1] == "-1" && fields[8] == "0", "interval start frame/song/playing");
                Check(Math.Abs(double.Parse(fields[5], CultureInfo.InvariantCulture) - 10) < .001, "Stopwatch interval");
            }
            n++;
        }
        Check(n == 600001, "capacity rows=" + n);
        var meta = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "dropscan-meta.json"))).RootElement;
        Check(meta.GetProperty("truncated").GetBoolean(), "truncated flag");
        Check(meta.GetProperty("skipped_frames").GetInt32() == 2, "skipped frames");
        DropScan.Tick(stamp, 1, 0, true, 0, 0);
        Check(DropScan.End(directory) == "시작 안 함", "inactive tick/end");
        Console.WriteLine(FormattableString.Invariant($"PASS frames=600000 skipped=2 per_frame_alloc_bytes={allocationDelta} loop_ms={sw.Elapsed.TotalMilliseconds:F3}"));
        Console.WriteLine(result);
        ExportFixture(directory);
    }

    static void ExportFixture(string directory)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var type = typeof(ProfSamp);
        void Set(string name, object value) => type.GetField(name, flags).SetValue(null, value);
        var cache = (Dictionary<ulong, string>)type.GetField("cache", flags).GetValue(null);
        cache[1] = "StutterFix.Work:Apply()"; cache[2] = "[UnityPlayer.dll+0xABC]"; cache[3] = "[ntdll.dll+0x12]";
        var ips = new ulong[32]; ips[0] = 1; ips[1] = 2; ips[2] = 3;
        Set("ips", ips); Set("count", 1); Set("fullMode", false);
        var write = type.GetMethod("WriteFrames", flags);
        write.Invoke(null, new object[] { directory, IntPtr.Zero });
        string expected = "M:StutterFix.Work:Apply();U:ABC;X:[ntdll.dll+0x12]";
        string legacy = File.ReadAllText(Path.Combine(directory, "profsamp-frames.txt")).Trim();
        Check(legacy == expected, "legacy M/U/X exact output: " + legacy);
        File.WriteAllText(Path.Combine(directory, "legacy-frames.txt"), legacy);
        Set("sampleSongs", new float[] { -1.25f }); Set("sampleFrames", new int[] { 17 });
        Set("sampleStamps", new long[] { Stopwatch.Frequency * 123L });
        Set("suspendTicks", new long[] { Stopwatch.Frequency / 1000 }); Set("fullMode", true);
        write.Invoke(null, new object[] { directory, IntPtr.Zero });
        string full = File.ReadAllText(Path.Combine(directory, "profsamp-frames.txt")).Trim();
        Check(full == "T:-1.25;F:17;W:123;S:1000;" + expected, "full invariant T/F/W/S: " + full);
        Check((int)type.GetField("MaxHitches", flags).GetRawConstantValue() == 500, "ring cap 500");
        Console.WriteLine("PASS ProfSamp legacy/full serialization, invariant culture, M/U/X, ring cap=500 (no native APIs invoked)");
    }
}
