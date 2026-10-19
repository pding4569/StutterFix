#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace StutterFix
{
    // (자동 시험 profsamp begin [hz] / end) 메인 스레드 표본 프로파일러. 함수에 계측을 넣지 않아 비용이 부풀지 않는다.
    //   다른 스레드가 일정 간격으로 메인 스레드를 잠깐 멈추고(SuspendThread) 명령어 위치(RIP)와 프레임 포인터(RBP) 체인의 반환 주소를 미리 잡아 둔 배열에 저장한 뒤 바로 재개한다.
    //   (멈춘 동안에는 할당·잠금이 필요한 일을 하지 않는다: 메인 스레드가 힙/GC 잠금을 쥔 채 멈췄을 수 있다.)
    //   끝낸 뒤에 주소를 Mono JIT 정보(mono_jit_info_table_find)로 관리 함수 이름으로 풀고, 아니면 네이티브 모듈+RVA 로 묶어 상위를 보고한다.
    //   리프(바로 그 순간 실행 중인 함수)와 포함(스택에 한 번이라도 있는 함수) 두 가지.
    internal static class ProfSamp
    {
        private const int MaxSamples = 40000, Depth = 12;
        private static ulong[] ips;       // MaxSamples * Depth, 0 이면 끝
        private static int count;
        private static volatile bool run;
        private static Thread thread;
        private static IntPtr mainHandle;
        private static long t0, t1;
        private static int hzUsed;

        [DllImport("kernel32.dll")] private static extern IntPtr OpenThread(uint access, bool inherit, uint id);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("kernel32.dll")] private static extern uint SuspendThread(IntPtr h);
        [DllImport("kernel32.dll")] private static extern int ResumeThread(IntPtr h);
        [DllImport("kernel32.dll")] private static extern bool GetThreadContext(IntPtr h, IntPtr ctx);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool GetModuleHandleExW(uint flags, IntPtr addr, out IntPtr module);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern uint GetModuleFileNameW(IntPtr module, StringBuilder name, uint size);
        [DllImport("mono-2.0-bdwgc", EntryPoint = "mono_get_root_domain")] private static extern IntPtr mono_get_root_domain();
        [DllImport("mono-2.0-bdwgc", EntryPoint = "mono_jit_info_table_find")] private static extern IntPtr mono_jit_info_table_find(IntPtr domain, IntPtr addr);
        [DllImport("mono-2.0-bdwgc", EntryPoint = "mono_jit_info_get_method")] private static extern IntPtr mono_jit_info_get_method(IntPtr ji);
        [DllImport("mono-2.0-bdwgc", EntryPoint = "mono_method_full_name")] private static extern IntPtr mono_method_full_name(IntPtr method, bool signature);
        [DllImport("mono-2.0-bdwgc", EntryPoint = "mono_free")] private static extern void mono_free(IntPtr p);

        internal static string Begin(string arg)
        {
            if (run) return "이미 도는 중";
            int hz = 1000; int.TryParse((arg ?? "").Trim(), out hz); if (hz < 50 || hz > 4000) hz = 1000; hzUsed = hz;
            ips = new ulong[MaxSamples * Depth]; count = 0;
            uint tid = GetCurrentThreadId();
            mainHandle = OpenThread(0x0002 | 0x0008 | 0x0040, false, tid);   // SUSPEND_RESUME | GET_CONTEXT | QUERY_INFORMATION
            if (mainHandle == IntPtr.Zero) return "OpenThread 실패 " + Marshal.GetLastWin32Error();
            run = true; t0 = Stopwatch.GetTimestamp();
            thread = new Thread(Loop) { IsBackground = true, Priority = ThreadPriority.AboveNormal, Name = "StutterFix.ProfSamp" };
            thread.Start();
            return "시작 (" + hz + "Hz, 메인 스레드 " + tid + ")";
        }

        private static void Loop()
        {
            IntPtr raw = Marshal.AllocHGlobal(1232 + 32);
            IntPtr ctx = (IntPtr)(((long)raw + 15) & ~15L);
            long period = Stopwatch.Frequency / hzUsed; long next = Stopwatch.GetTimestamp();
            try
            {
                while (run && count < MaxSamples)
                {
                    next += period;
                    while (Stopwatch.GetTimestamp() < next) { Thread.SpinWait(50); if (!run) break; }
                    if (!run) break;
                    if (SuspendThread(mainHandle) == 0xFFFFFFFF) continue;
                    // 멈춘 동안: 컨텍스트를 읽고 RBP 체인을 따라 반환 주소만 배열에 복사 (할당·잠금 금지)
                    Marshal.WriteInt32(ctx, 0x30, 0x10003);   // CONTEXT_CONTROL | CONTEXT_INTEGER
                    bool ok = GetThreadContext(mainHandle, ctx);
                    if (ok)
                    {
                        int b = count * Depth; ulong rip = (ulong)Marshal.ReadInt64(ctx, 0xF8), rsp = (ulong)Marshal.ReadInt64(ctx, 0x98), rbp = (ulong)Marshal.ReadInt64(ctx, 0xA0);
                        ips[b] = rip; int d = 1;
                        while (d < Depth && rbp > rsp && rbp - rsp < (1UL << 20) && (rbp & 7) == 0)
                        {
                            ulong ret = (ulong)Marshal.ReadInt64((IntPtr)(long)(rbp + 8)); ulong nbp = (ulong)Marshal.ReadInt64((IntPtr)(long)rbp);
                            if (ret < 0x10000) break; ips[b + d++] = ret;
                            if (nbp <= rbp) break; rbp = nbp;
                        }
                        count++;
                    }
                    ResumeThread(mainHandle);
                }
            }
            finally { Marshal.FreeHGlobal(raw); }
        }

        private static readonly Dictionary<ulong, string> cache = new Dictionary<ulong, string>();
        private static string Resolve(ulong addr, IntPtr domain, out bool managed)
        {
            managed = false;
            string s; if (cache.TryGetValue(addr, out s)) { managed = s.Length > 0 && s[0] != '['; return s; }
            try
            {
                IntPtr ji = mono_jit_info_table_find(domain, (IntPtr)(long)addr);
                if (ji != IntPtr.Zero)
                {
                    IntPtr m = mono_jit_info_get_method(ji);
                    if (m != IntPtr.Zero) { IntPtr p = mono_method_full_name(m, false); if (p != IntPtr.Zero) { s = Marshal.PtrToStringAnsi(p); mono_free(p); managed = true; cache[addr] = s; return s; } }
                }
            }
            catch { }
            IntPtr mod;
            if (GetModuleHandleExW(0x4 | 0x2, (IntPtr)(long)addr, out mod) && mod != IntPtr.Zero)   // FROM_ADDRESS | UNCHANGED_REFCOUNT
            {
                var sb = new StringBuilder(260); GetModuleFileNameW(mod, sb, 260);
                string n = sb.ToString(); int i = n.LastIndexOf('\\'); if (i >= 0) n = n.Substring(i + 1);
                s = "[" + n + "+0x" + (addr - (ulong)(long)mod).ToString("X") + "]";
            }
            else s = "[?]";
            cache[addr] = s; return s;
        }

        internal static string End()
        {
            if (!run && count == 0) return "시작 안 함";
            run = false; if (thread != null) thread.Join(2000); t1 = Stopwatch.GetTimestamp();
            try { CloseHandle(mainHandle); } catch { }
            double secs = (t1 - t0) / (double)Stopwatch.Frequency;
            IntPtr domain = IntPtr.Zero; try { domain = mono_get_root_domain(); } catch (Exception ex) { return "mono_get_root_domain 실패: " + ex.Message; }
            var leaf = new Dictionary<string, int>(); var incl = new Dictionary<string, int>(); var mod = new Dictionary<string, int>();
            var callers = new Dictionary<string, Dictionary<string, int>>();   // 관리 리프 → 가장 가까운 관리 호출자 (리프가 관리 함수이거나 네이티브면 그 위의 첫 관리 함수)
            int managedLeaf = 0;
            for (int i = 0; i < count; i++)
            {
                bool mg; string l = Resolve(ips[i * Depth], domain, out mg);
                // 네이티브 리프는 모듈+RVA 를 모듈 단위와 RVA 단위로 센다
                if (mg) managedLeaf++;
                leaf[l] = (leaf.TryGetValue(l, out int a) ? a : 0) + 1;
                if (!mg) { int p = l.IndexOf('+'); string m = p > 0 ? l.Substring(0, p) + "]" : l; mod[m] = (mod.TryGetValue(m, out int c) ? c : 0) + 1; }
                // 리프(또는 리프가 네이티브면 가장 가까운 관리 함수) 와 그 바로 위 호출자(관리 함수)
                {
                    string near = null, up = null; int found = 0;
                    for (int d = 0; d < Depth && found < 2; d++)
                    {
                        ulong ip = ips[i * Depth + d]; if (ip == 0) break;
                        bool g; string n = Resolve(ip, domain, out g);
                        if (!g) { if (found == 0 && d == 0) near = n; continue; }
                        if (near == null || found > 0 || d > 0 && near != null && found == 0) { }
                        if (found == 0) { if (near == null) near = n; else near = near + " < " + n; }
                        else up = n;
                        found++;
                    }
                    if (near != null)
                    {
                        Dictionary<string, int> cd; if (!callers.TryGetValue(near, out cd)) callers[near] = cd = new Dictionary<string, int>();
                        string u = up ?? "(없음)"; cd[u] = (cd.TryGetValue(u, out int cc) ? cc : 0) + 1;
                    }
                }
                var seen = new HashSet<string>();
                for (int d = 0; d < Depth; d++)
                {
                    ulong ip = ips[i * Depth + d]; if (ip == 0) break;
                    bool g; string n = Resolve(ip, domain, out g);
                    if (g && seen.Add(n)) incl[n] = (incl.TryGetValue(n, out int b) ? b : 0) + 1;
                }
            }
            var sbo = new StringBuilder();
            sbo.Append("표본 ").Append(count).Append("개 / ").Append(secs.ToString("F1")).Append("초 (").Append(hzUsed).Append("Hz 요청), 관리 코드 리프 ").Append((100.0 * managedLeaf / Math.Max(1, count)).ToString("F1")).Append("%\n");
            sbo.Append("[네이티브 모듈별 리프] ");
            foreach (var kv in Top(mod, 8)) sbo.Append(kv.Key).Append(' ').Append((100.0 * kv.Value / count).ToString("F1")).Append("%, ");
            sbo.Append("\n[리프 상위] ");
            foreach (var kv in Top(leaf, 40)) sbo.Append(kv.Key).Append(' ').Append((100.0 * kv.Value / count).ToString("F1")).Append("%\n  ");
            sbo.Append("\n[관리 함수 포함(스택 어디든)] ");
            foreach (var kv in Top(incl, 40)) sbo.Append(kv.Key).Append(' ').Append((100.0 * kv.Value / count).ToString("F1")).Append("%\n  ");
            sbo.Append("
[리프(네이티브면 가장 가까운 관리 함수 포함) ← 호출자] ");
            var nearTot = new Dictionary<string, int>(); foreach (var kv in callers) { int t = 0; foreach (var c in kv.Value.Values) t += c; nearTot[kv.Key] = t; }
            foreach (var kv in Top(nearTot, 30))
            {
                sbo.Append(kv.Key).Append(' ').Append((100.0 * kv.Value / count).ToString("F1")).Append("% ← ");
                foreach (var c in Top(callers[kv.Key], 3)) sbo.Append(c.Key).Append(' ').Append((100.0 * c.Value / count).ToString("F1")).Append("%; ");
                sbo.Append("
  ");
            }
            ips = null; count = 0; cache.Clear();
            return sbo.ToString();
        }
        private static List<KeyValuePair<string, int>> Top(Dictionary<string, int> d, int n)
        {
            var l = new List<KeyValuePair<string, int>>(d); l.Sort((a, b) => b.Value.CompareTo(a.Value)); if (l.Count > n) l.RemoveRange(n, l.Count - n); return l;
        }
    }
}
#endif
