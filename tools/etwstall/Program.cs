// 끊긴 프레임 하나를 ETW 기록(PerfView ThreadTime)에서 들여다본다.
//   EtwStall <etl 또는 etlx> <게임 PID> <끊김 시각 HH:mm:ss.fff> [앞ms=80] [뒤ms=10]
// 게임 프로세스의 스레드마다 그 구간의 CPU 샘플(스택)과 문맥 전환(잠든 시간, 잠든 이유, 깨운 스레드)을 적는다.
// 심볼은 게임 프로세스의 그래픽·엔진·커널 모듈만 찾는다(PerfView SaveCPUStacks 는 모든 프로세스 심볼을 찾느라 수십 분 걸렸다).
// 심볼 경로: C:\Users\Public\StutterFixTrace\symbols (유니티 심볼은 거기 직접 받아 둔다) + 마이크로소프트 서버.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;

static class Program
{
    static int Main(string[] a)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        if (a.Length < 3) { Console.WriteLine("EtwStall <etl|etlx> <PID> <HH:mm:ss.fff> [앞ms=80] [뒤ms=10]"); return 1; }
        string path = a[0]; int pid = int.Parse(a[1]);
        double before = a.Length > 3 ? double.Parse(a[3]) : 80, after = a.Length > 4 ? double.Parse(a[4]) : 10;
        string etlx = path.EndsWith(".etlx", StringComparison.OrdinalIgnoreCase) ? path : TraceLog.CreateFromEventTraceLogFile(path);
        using var log = new TraceLog(etlx);
        var start = log.SessionStartTime;
        var hitT = DateTime.Parse(start.ToString("yyyy-MM-dd") + " " + a[2]);
        double hit = (hitT - start).TotalMilliseconds;
        double t0 = hit - before, t1 = hit + after;
        Console.WriteLine($"기록 시작 {start:HH:mm:ss.fff}, 끊김 {hit:F1}ms, 구간 {t0:F1} ~ {t1:F1}");

        var proc = log.Processes.FirstOrDefault(p => p.ProcessID == pid);
        if (proc == null) { Console.WriteLine("프로세스 없음"); return 1; }

        // 필요한 모듈 심볼만
        string symPath = @"C:\Users\Public\StutterFixTrace\symbols;SRV*C:\Users\Public\StutterFixTrace\symbols*https://msdl.microsoft.com/download/symbols";
        using (var reader = new SymbolReader(TextWriter.Null, symPath))
        {
            reader.SecurityCheck = _ => true;
            var want = new[] { "unityplayer", "ntoskrnl", "dxgkrnl", "dxgmms2", "d3d11", "dxgi", "nvwgf2umx", "nvlddmkm", "ntdll", "kernelbase", "win32kbase", "win32kfull", "mono-2.0-bdwgc", "fmodstudio", "fmod" };
            foreach (var m in proc.LoadedModules)
            {
                string n = Path.GetFileNameWithoutExtension(m.ModuleFile.FilePath).ToLowerInvariant();
                if (!want.Contains(n)) continue;
                try { log.CodeAddresses.LookupSymbolsForModule(reader, m.ModuleFile); } catch (Exception ex) { Console.WriteLine("심볼 실패 " + n + ": " + ex.Message); }
            }
            // 커널 모듈(프로세스 목록 밖)
            foreach (var mf in log.ModuleFiles)
            {
                string n = Path.GetFileNameWithoutExtension(mf.FilePath).ToLowerInvariant();
                if (n == "ntoskrnl" || n == "dxgkrnl" || n == "dxgmms2" || n == "nvlddmkm" || n == "win32kbase" || n == "win32kfull")
                    try { log.CodeAddresses.LookupSymbolsForModule(reader, mf); } catch { }
            }
        }

        // 스레드별 CPU 샘플 수(구간 전체) -> 메인·렌더 스레드 찾기
        var samplesByThread = new Dictionary<int, List<(double t, TraceCallStack st)>>();
        var cs = new List<(double t, int tidIn, int tidOut, int cpu, string why, string state, TraceCallStack st)>();
        var ready = new List<(double t, int tid, int by, TraceCallStack st)>();
        var runningOnCpu = new Dictionary<int, int>();
        var procThreads = new HashSet<int>(proc.Threads.Select(x => x.ThreadID));
        var kinds = new Dictionary<string, int>();
        foreach (var ev in log.Events)
        {
            double t = ev.TimeStampRelativeMSec;
            if (t < t0 - 200) continue;
            if (t > t1 + 50) break;
            if (Environment.GetEnvironmentVariable("ETW_DEBUG") == "1" && t >= t0 && t <= t1)
            {
                string k = ev.GetType().Name + " " + ev.ProviderName + "/" + ev.EventName + " pid " + ev.ProcessID;
                kinds[k] = kinds.TryGetValue(k, out var kc) ? kc + 1 : 1;
            }
            if (ev is SampledProfileTraceData sp)
            {
                if (sp.ProcessID != pid || t < t0 || t > t1) continue;
                if (!samplesByThread.TryGetValue(sp.ThreadID, out var l)) samplesByThread[sp.ThreadID] = l = new List<(double, TraceCallStack)>();
                l.Add((t, sp.CallStack()));
            }
            else if (ev is CSwitchTraceData c)
            {
                runningOnCpu[c.ProcessorNumber] = c.NewThreadID;
                if (procThreads.Contains(c.NewThreadID) || procThreads.Contains(c.OldThreadID))
                    cs.Add((t, c.NewThreadID, c.OldThreadID, c.ProcessorNumber, c.OldThreadWaitReason.ToString(), c.OldThreadState.ToString(), c.CallStack()));
            }
            else if (ev is DispatcherReadyThreadTraceData r)
            {
                if (procThreads.Contains(r.AwakenedThreadID))
                    ready.Add((t, r.AwakenedThreadID, runningOnCpu.TryGetValue(r.ProcessorNumber, out var b) ? b : -1, r.CallStack()));
            }
        }

        foreach (var kv in kinds.OrderByDescending(x => x.Value).Take(40)) Console.WriteLine("  이벤트 " + kv.Value + " " + kv.Key);
        string Who(int tid)
        {
            var th = log.Threads.FirstOrDefault(x => x.ThreadID == tid && x.Process != null);
            return th == null ? "스레드 " + tid : th.Process.Name + "(" + th.Process.ProcessID + ") 스레드 " + tid;
        }

        foreach (var kv in samplesByThread.OrderByDescending(k => k.Value.Count).Take(6))
        {
            int tid = kv.Key;
            Console.WriteLine();
            Console.WriteLine($"=== 스레드 {tid}: 샘플 {kv.Value.Count}개 (1ms 간격이면 그만큼 돌았다)");
            // 잠든 구간 (나갔다가 들어온 사이)
            double outAt = -1; string why = ""; TraceCallStack outStack = null;
            foreach (var c in cs.Where(x => x.t >= t0 && x.t <= t1))
            {
                if (c.tidOut == tid) { outAt = c.t; why = c.why + "/" + c.state; outStack = c.st; }
                if (c.tidIn == tid && outAt >= 0)
                {
                    double gap = c.t - outAt;
                    if (gap >= 1.0)
                    {
                        var rd = ready.LastOrDefault(r => r.tid == tid && r.t <= c.t && r.t >= outAt);
                        Console.WriteLine($"  잠듦 {outAt:F1} ~ {c.t:F1} ({gap:F1}ms) 이유 {why} | 깨운 것: {(rd.by != 0 || rd.t > 0 ? Who(rd.by) : "?")}");
                        if (gap >= 8 && outStack != null) Console.WriteLine("     잠든 곳: " + Stack(outStack, 40));
                        if (gap >= 8 && rd.st != null) Console.WriteLine("     깨운 곳: " + Stack(rd.st, 30));
                    }
                    outAt = -1;
                }
            }
            // 샘플을 시간 순으로, 같은 스택이 이어지면 묶는다
            string last = null; double from = 0; int n = 0;
            void Flush(double to) { if (last != null) Console.WriteLine($"  {from:F1}~{to:F1} ({n}개) {last}"); }
            foreach (var s in kv.Value.OrderBy(x => x.t))
            {
                string st = Stack(s.st, 14);
                if (st != last) { Flush(s.t); last = st; from = s.t; n = 0; }
                n++;
            }
            Flush(kv.Value.Max(x => x.t));
        }
        return 0;
    }

    static string Stack(TraceCallStack st, int max)
    {
        var parts = new List<string>();
        for (var c = st; c != null && parts.Count < max; c = c.Caller)
        {
            string m = c.CodeAddress.ModuleName ?? "?";
            string f = c.CodeAddress.FullMethodName;
            if (string.IsNullOrEmpty(f)) f = "0x" + c.CodeAddress.Address.ToString("x");
            if (f.Length > 70) f = f.Substring(0, 70);
            parts.Add(m + "!" + f);
        }
        return string.Join(" < ", parts);
    }
}
