using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StutterFix;

internal static class Program
{
    private static int checks;
    private static void Check(bool ok, string name)
    {
        if (!ok) throw new Exception("FAIL: " + name);
        checks++;
        Console.WriteLine("PASS: " + name);
    }

    // 숫자를 지운 뒤에도 서로 다른 키를 만든다.
    private static string Key(int n) => "diagnostic " + (char)('A' + n / 26) + (char)('a' + n % 26);

    private static int Main(string[] args)
    {
        try
        {
            Policy();
            Repeat();
            Global();
            ThreadsAndAllocations();
            Console.WriteLine("LogCheck: " + checks + " checks, 0 failures");
            if (args.Length != 0) Offline(args[0]);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void Policy()
    {
        string[] essential = { "설치 실패", "오류 발생", "예외 발생", "경고 발생", "하지 못 했음",
            "Error occurred", "error occurred", "failed", "Exception thrown", "This game is modded (StutterFix)",
            "[안정성] 안전 모드", "[곡] 평균 165 FPS", "Version '2.8.2'. Loading.", "켜짐: 패치 설치 완료 (플레이어용)" };
        Check(essential.All(Lg.IsEssential), "all required essential messages");
        string[] details = { "patched scnGame.Awake", "[이미지] 해독 시간 50ms", "[상태] Start -> 플레이", "[로딩] 설치", "end hook: scrController.FailAction" };
        Check(details.All(s => !Lg.IsEssential(s)) && !Lg.IsEssential(null), "verbose-only installation, image timing and state messages");
        var lines = new List<(Lg.Level Level, string Text)>();
        Lg.SetTarget(() => 0, (l, s) => lines.Add((l, s)));
        Lg.Verbose = false;
        Lg.I("detail");
#if DEV || AUTOTEST
        Check(Lg.Verbose && lines.Count == 1, "diagnostic edition ignores false verbosity");
        for (int i = 0; i < 200; i++) Lg.I("detail");
        Check(lines.Count == 201 && lines.All(x => x.Text == "detail"), "diagnostic edition preserves every original line");
#else
        Check(!Lg.Verbose && lines.Count == 0, "Player normal mode hides details");
#endif
        lines.Clear();
        Lg.I("[곡] summary"); Lg.W("plain warning"); Lg.E("plain error");
        Check(lines.Count == 3 && lines[1] == (Lg.Level.Warning, "plain warning")
            && lines[2] == (Lg.Level.Error, "plain error"), "essential and severity routing preserves message text");
        Lg.Verbose = true; Lg.I("extra detail");
        Check(lines.Last().Text == "extra detail", "verbose mode passes details");
#if !DEV && !AUTOTEST
        lines.Clear();
        for (int i = 0; i < 10; i++) Lg.I("verbose repeated");
        Check(lines.Count == 3, "verbose mode still limits repeats");
#endif
    }

    private static void Repeat()
    {
        long now = 0;
        var lines = new List<string>();
        var gate = new Lg.Limiter(() => now, (_, s) => lines.Add(s));
        for (int i = 0; i < 10; i++) gate.Write(Lg.Level.Error, "Error frame " + i);
        Check(lines.Count == 3, "numbers share a key; three messages per ten seconds");
        now = 9999; gate.Flush();
        Check(lines.Count == 3, "no repeat summary before ten seconds");
        now = 10000; gate.Flush(); gate.Flush();
        Check(lines.Count == 4 && lines.Last() == "(같은 로그 7번 생략: Error frame )", "idle flush emits exactly one counted repeat summary");
        for (int i = 0; i < 4; i++) gate.Write(Lg.Level.Error, "Error frame " + i);
        Check(lines.Count == 7, "repeat window resets after expiry");
        now = 20000; gate.Write(Lg.Level.Error, "Error frame 123");
        Check(lines.Count == 9 && lines[7].Contains("1번 생략"), "next write also expires and summarizes repeat window");

        lines.Clear(); now = 0;
        gate = new Lg.Limiter(() => now, (_, s) => lines.Add(s));
        for (int i = 0; i < 4; i++) gate.Write(Lg.Level.Info, new string('x', 60) + (char)('a' + i));
        gate.Write(Lg.Level.Info, new string('x', 59) + "y");
        Check(lines.Count == 4, "only the normalized first sixty characters form the key");
        for (int i = 0; i < 4; i++) gate.Write(Lg.Level.Info, "unicode " + (char)('\u0660' + i));
        Check(lines.Count == 7, "Unicode digits are removed too");
        gate.Write(Lg.Level.Info, null); gate.Flush();
        Check(lines.Count == 8, "null message does not break limiter");
    }

    private static void Global()
    {
        long now = 0;
        var lines = new List<string>();
        var gate = new Lg.Limiter(() => now, (_, s) => lines.Add(s));
        for (int i = 0; i < 150; i++) gate.Write(Lg.Level.Warning, Key(i));
        Check(lines.Count == 100, "global ceiling covers all severity levels");
        now = 999; gate.Flush();
        Check(lines.Count == 100, "no global summary before next second");
        now = 1000; gate.Flush(); gate.Flush();
        Check(lines.Count == 101 && lines.Last() == "(로그 초과 50줄 생략)", "idle global flush counts overflow once");
        for (int i = 150; i < 250; i++) gate.Write(Lg.Level.Info, Key(i));
        Check(lines.Count == 200, "global summary also consumes one of the next hundred lines");
        now = 2000; gate.Write(Lg.Level.Info, Key(300));
        Check(lines[^2] == "(로그 초과 1줄 생략)" && lines.Last() == Key(300), "write-triggered rollover retains overflow accounting");
        now = 0; gate.Write(Lg.Level.Info, Key(301));
        Check(lines.Last() == Key(301), "late thread timestamps do not roll the global window backward");

        lines.Clear(); now = 0;
        gate = new Lg.Limiter(() => now, (_, s) => lines.Add(s));
        for (int i = 0; i < 1000; i++) gate.Write(Lg.Level.Info, "repeat");
        now = 10000; gate.Flush();
        Check(lines.Count == 4 && lines.Last().Contains("997번 생략"), "repeat suppression is not double counted as global overflow");
        // 많은 같은 줄 요약도 전체 100줄 한도를 우회하지 않는다.
        lines.Clear(); now = 0;
        gate = new Lg.Limiter(() => now, (_, s) => lines.Add(s));
        for (int i = 0; i < 150; i++) for (int j = 0; j < 5; j++) gate.Write(Lg.Level.Info, Key(i));
        int before = lines.Count; now = 10000; gate.Flush();
        Check(lines.Count - before <= 100, "repeat summaries obey global ceiling");
    }

    private static void ThreadsAndAllocations()
    {
        long now = 0;
        var lines = new ConcurrentQueue<string>();
        var gate = new Lg.Limiter(() => Interlocked.Read(ref now), (_, s) => lines.Enqueue(s));
        Parallel.For(0, 10000, i => gate.Write(Lg.Level.Info, Key(i % 600)));
        Check(lines.Count <= 100 && lines.Count > 0, "concurrent writers keep atomic global ceiling");
        now = 10000;
        Parallel.For(0, 8, _ => gate.Flush());
        Check(lines.Count <= 200, "concurrent expiry summaries remain globally bounded");

        int outputs = 0;
        gate = new Lg.Limiter(() => 0, (_, _) => outputs++);
        for (int i = 0; i < 1000; i++) gate.Write(Lg.Level.Info, "same");
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) gate.Write(Lg.Level.Info, "same");
        Check(GC.GetAllocatedBytesForCurrentThread() - bytes == 0 && outputs == 3,
            "ten thousand suppressed hot-path calls allocate zero bytes");

        now = 0;
        gate = new Lg.Limiter(() => now, (_, _) => { });
        for (int i = 0; i < 1000; i++) { now += 10000; gate.Write(Lg.Level.Info, "allowed"); }
        bytes = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) { now += 10000; gate.Write(Lg.Level.Info, "allowed"); }
        Check(GC.GetAllocatedBytesForCurrentThread() - bytes == 0, "accepted hot-path key checks also allocate zero bytes");

        Lg.Limiter recursive = null; int emitted = 0;
        recursive = new Lg.Limiter(() => 0, (_, _) => { emitted++; recursive.Write(Lg.Level.Error, "recursive"); });
        recursive.Write(Lg.Level.Info, "outer");
        Check(emitted == 1, "output reentry cannot recurse forever");
    }

    private static void Offline(string path)
    {
        const string prefix = "[StutterFix] ";
        string[] all = File.ReadLines(path).Where(s => s.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        string[] kept = all.Where(s => Lg.IsEssential(s.Substring(prefix.Length))).ToArray();
        Console.WriteLine("OFFLINE: " + all.Length + " -> " + kept.Length + " lines (filter only, no timestamps in source)");
        foreach (string line in kept) Console.WriteLine(line);
        int managerLines = all.Count(s => s.Substring(prefix.Length) == "Active."
            || s.Substring(prefix.Length).StartsWith("Loading time ", StringComparison.Ordinal));
        Console.WriteLine("UMM-owned passthrough lines: " + managerLines);
        Console.WriteLine("Expected total including UMM-owned lines: " + (kept.Length + managerLines));
    }
}
