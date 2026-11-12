#if DEV || AUTOTEST
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace StutterFix
{
    // (profsamp begin [hz] [native] / end) 명시적 진단 전용. 스레드 정지와
    // 스택 풀기 비용이 있으므로 이 판의 FPS를 성능 비교에 사용하지 않는다.
    //   다른 스레드가 일정 간격으로 메인 스레드를 잠깐 멈추고(SuspendThread) 명령어 위치(RIP)와 프레임 포인터(RBP) 체인의 반환 주소를 미리 잡아 둔 배열에 저장한 뒤 바로 재개한다.
    //   (멈춘 동안에는 할당·잠금이 필요한 일을 하지 않는다: 메인 스레드가 힙/GC 잠금을 쥔 채 멈췄을 수 있다.)
    //   끝낸 뒤에 주소를 Mono JIT 정보(mono_jit_info_table_find)로 관리 함수 이름으로 풀고, 아니면 네이티브 모듈+RVA 로 묶어 상위를 보고한다.
    //   리프(바로 그 순간 실행 중인 함수)와 포함(스택에 한 번이라도 있는 함수) 두 가지.
    internal static class ProfSamp
    {
        private const int MaxSamples = 40000, Depth = 32;
        private static ulong[] ips;       // MaxSamples * Depth, 0 이면 끝
        private static int count;
        private static volatile bool run;
        private static Thread thread;
        private static IntPtr mainHandle;
        private static long t0, t1;
        private static int hzUsed;
        private static bool nativeUnwind;
        private struct ImageRange { internal ulong Start, End; }
        private static ImageRange[] images;
        private static long nativeFrames;
        private static string failure;
        [DllImport("kernel32.dll")] private static extern void RtlCaptureContext(IntPtr context);
        [DllImport("kernel32.dll")] private static extern IntPtr RtlLookupFunctionEntry(ulong pc, out ulong imageBase, IntPtr history);
        [DllImport("kernel32.dll")] private static extern IntPtr RtlVirtualUnwind(uint flags, ulong imageBase, ulong pc,
            IntPtr entry, IntPtr context, out IntPtr handlerData, out ulong establisher, IntPtr contextPointers);

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

        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
        [DllImport("kernel32.dll")] private static extern bool Thread32First(IntPtr snap, ref ThreadEntry e);
        [DllImport("kernel32.dll")] private static extern bool Thread32Next(IntPtr snap, ref ThreadEntry e);
        [DllImport("kernel32.dll")] private static extern int GetThreadDescription(IntPtr thread, out IntPtr text);
        [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr mem);
        [StructLayout(LayoutKind.Sequential)]
        private struct ThreadEntry { internal uint Size, Usage, Id, Owner; internal int BasePri, DeltaPri; internal uint Flags; }

        // 이름에 name 이 들어간 첫 스레드. 못 찾으면 있는 이름을 전부 보여 준다.
        private static uint FindThread(string name)
        {
            uint pid = (uint)Process.GetCurrentProcess().Id;
            IntPtr snap = CreateToolhelp32Snapshot(4, 0);
            var names = new List<string>();
            try
            {
                var e = new ThreadEntry { Size = (uint)Marshal.SizeOf(typeof(ThreadEntry)) };
                for (bool ok = Thread32First(snap, ref e); ok; ok = Thread32Next(snap, ref e))
                {
                    if (e.Owner != pid) continue;
                    IntPtr h = OpenThread(0x0800, false, e.Id);   // QUERY_LIMITED_INFORMATION
                    if (h == IntPtr.Zero) continue;
                    string text = "";
                    if (GetThreadDescription(h, out IntPtr p) >= 0 && p != IntPtr.Zero) { text = Marshal.PtrToStringUni(p); LocalFree(p); }
                    CloseHandle(h);
                    if (text.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) return e.Id;
                    names.Add(text);
                }
            }
            finally { CloseHandle(snap); }
            throw new ArgumentException("스레드 없음: " + name + " / 있는 이름: " + string.Join(",", names.Distinct()));
        }

        internal static string Begin(string arg)
        {
            if (run) return "이미 도는 중";
            var args = (arg ?? "").Trim().Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
            if (args.Length > 3 || args.Skip(1).Any(a => a != "native" && !a.StartsWith("thread=")))
                throw new ArgumentException("profsamp begin [hz] [native] [thread=이름 일부]");
            int hz = 1000; if (args.Length > 0) int.TryParse(args[0], out hz); if (hz < 50 || hz > 4000) hz = 1000; hzUsed = hz;
            nativeUnwind = args.Contains("native");
            string threadName = args.Where(a => a.StartsWith("thread=")).Select(a => a.Substring(7).Replace('_', ' ')).FirstOrDefault();
            nativeFrames = 0; failure = null;
            // Resolve modules while the target is running. Never enter Mono/JIT
            // function-table callbacks or enumerate modules with it suspended.
            var ranges = new List<ImageRange>();
            if (nativeUnwind)
                using (var process = Process.GetCurrentProcess())
                    foreach (ProcessModule module in process.Modules)
                        ranges.Add(new ImageRange { Start = (ulong)module.BaseAddress.ToInt64(),
                            End = (ulong)module.BaseAddress.ToInt64() + (ulong)module.ModuleMemorySize });
            images = ranges.ToArray();
            ips = new ulong[MaxSamples * Depth]; count = 0;
            uint tid = GetCurrentThreadId();
            if (threadName != null) tid = FindThread(threadName);
            mainHandle = OpenThread(0x0002 | 0x0008 | 0x0040, false, tid);   // SUSPEND_RESUME | GET_CONTEXT | QUERY_INFORMATION
            if (mainHandle == IntPtr.Zero) return "OpenThread 실패 " + Marshal.GetLastWin32Error();
            run = true; t0 = Stopwatch.GetTimestamp();
            thread = new Thread(Loop) { IsBackground = true, Priority = ThreadPriority.AboveNormal, Name = "StutterFix.ProfSamp" };
            thread.Start();
            return "시작 (" + hz + "Hz, 메인 스레드 " + tid + ", native_unwind=" + nativeUnwind + ")";
        }

        private static bool NativeImage(ulong pc)
        {
            for (int i = 0; i < images.Length; i++)
                if (pc >= images[i].Start && pc < images[i].End) return true;
            return false;
        }

        private static void CaptureNative(IntPtr ctx, int offset)
        {
            ulong firstSp = (ulong)Marshal.ReadInt64(ctx, 0x98);
            for (int d = 0; d < Depth; d++)
            {
                ulong pc = (ulong)Marshal.ReadInt64(ctx, 0xF8), sp = (ulong)Marshal.ReadInt64(ctx, 0x98);
                if (pc < 0x10000 || sp < firstSp || sp - firstSp >= (1UL << 20) || (sp & 7) != 0) break;
                ips[offset + d] = pc;
                if (NativeImage(pc))
                {
                    var entry = RtlLookupFunctionEntry(pc, out ulong imageBase, IntPtr.Zero);
                    if (entry != IntPtr.Zero)
                    {
                        RtlVirtualUnwind(0, imageBase, pc, entry, ctx, out _, out _, IntPtr.Zero);
                        nativeFrames++;
                    }
                    else // Native leaf: the return address is at RSP.
                    {
                        Marshal.WriteInt64(ctx, 0xF8, Marshal.ReadInt64((IntPtr)(long)sp));
                        Marshal.WriteInt64(ctx, 0x98, (long)(sp + 8));
                    }
                }
                else // Mono's existing frame-pointer convention; do not guess JIT unwind tables.
                {
                    ulong bp = (ulong)Marshal.ReadInt64(ctx, 0xA0);
                    if (bp < sp || bp - firstSp >= (1UL << 20) - 16 || (bp & 7) != 0) break;
                    Marshal.WriteInt64(ctx, 0xF8, Marshal.ReadInt64((IntPtr)(long)(bp + 8)));
                    Marshal.WriteInt64(ctx, 0x98, (long)(bp + 16));
                    Marshal.WriteInt64(ctx, 0xA0, Marshal.ReadInt64((IntPtr)(long)bp));
                }
                if ((ulong)Marshal.ReadInt64(ctx, 0x98) <= sp) break;
            }
        }

        private static void Loop()
        {
            IntPtr raw = Marshal.AllocHGlobal(1232 + 32);
            IntPtr ctx = (IntPtr)(((long)raw + 15) & ~15L);
            long period = Stopwatch.Frequency / hzUsed; long next = Stopwatch.GetTimestamp();
            try
            {
                // Warm the JIT and P/Invoke wrappers on this worker's own stack
                // before suspending the main thread. Discard this warmup sample.
                if (nativeUnwind)
                {
                    RtlCaptureContext(ctx); CaptureNative(ctx, 0);
                    Array.Clear(ips, 0, Depth); nativeFrames = 0;
                }
                while (run && count < MaxSamples)
                {
                    next += period;
                    while (Stopwatch.GetTimestamp() < next) { Thread.SpinWait(50); if (!run) break; }
                    if (!run) break;
                    if (SuspendThread(mainHandle) == 0xFFFFFFFF) continue;
                    try
                    {
                        Marshal.WriteInt32(ctx, 0x30, 0x1000B);   // CONTEXT_FULL (x64)
                        if (GetThreadContext(mainHandle, ctx))
                        {
                            if (nativeUnwind) { CaptureNative(ctx, count * Depth); count++; continue; }
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
                    }
                    finally { ResumeThread(mainHandle); }
                }
            }
            catch (Exception ex) { failure = ex.GetType().Name + ": " + ex.Message; run = false; }
            finally { Marshal.FreeHGlobal(raw); }
        }

        private static readonly Dictionary<ulong, string> cache = new Dictionary<ulong, string>();
        internal static bool Running => run;
        internal static string ResolveLeaf(ulong address,out bool managed)=>Resolve(address,mono_get_root_domain(),out managed);
        internal static void ClearLeafCache()=>cache.Clear();
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

        internal static string End() { return End(null); }
        internal static string End(string framesDir)
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
            if (framesDir != null)
            {
                // 표본마다 프레임을 한 줄로 (UnityPlayer 는 RVA 만): tools/symrva 가 PDB 로 함수 이름을 푼다
                var fb = new StringBuilder();
                for (int i = 0; i < count; i++)
                {
                    for (int d = 0; d < Depth; d++)
                    {
                        ulong ip = ips[i * Depth + d]; if (ip == 0) break;
                        bool g; string n = Resolve(ip, domain, out g);
                        if (d > 0) fb.Append(';');
                        if (g) fb.Append("M:").Append(n);
                        else if (n.StartsWith("[UnityPlayer.dll+0x")) fb.Append("U:").Append(n.Substring(19, n.Length - 20));
                        else fb.Append("X:").Append(n);
                    }
                    fb.Append((char)10);
                }
                try { System.IO.File.WriteAllText(System.IO.Path.Combine(framesDir, "profsamp-frames.txt"), fb.ToString()); } catch { }
            }
            var sbo = new StringBuilder();
            sbo.Append("native_unwind=").Append(nativeUnwind).Append(" native_frames=").Append(nativeFrames).Append(" max_depth=").Append(Depth).Append('\n');
            if (failure != null) sbo.Append("sampling_error=").Append(failure).Append('\n');
            sbo.Append("표본 ").Append(count).Append("개 / ").Append(secs.ToString("F1")).Append("초 (").Append(hzUsed).Append("Hz 요청), 관리 코드 리프 ").Append((100.0 * managedLeaf / Math.Max(1, count)).ToString("F1")).Append("%\n");
            sbo.Append("[네이티브 모듈별 리프] ");
            foreach (var kv in Top(mod, 8)) sbo.Append(kv.Key).Append(' ').Append((100.0 * kv.Value / count).ToString("F1")).Append("%, ");
            sbo.Append("\n[리프 상위] ");
            foreach (var kv in Top(leaf, 40)) sbo.Append(kv.Key).Append(' ').Append((100.0 * kv.Value / count).ToString("F1")).Append("%\n  ");
            sbo.Append("\n[관리 함수 포함(스택 어디든)] ");
            foreach (var kv in Top(incl, 40)) sbo.Append(kv.Key).Append(' ').Append((100.0 * kv.Value / count).ToString("F1")).Append("%\n  ");
            sbo.Append("\n[리프(네이티브면 가장 가까운 관리 함수 포함) ← 호출자] ");
            var nearTot = new Dictionary<string, int>(); foreach (var kv in callers) { int t = 0; foreach (var c in kv.Value.Values) t += c; nearTot[kv.Key] = t; }
            foreach (var kv in Top(nearTot, 30))
            {
                sbo.Append(kv.Key).Append(' ').Append((100.0 * kv.Value / count).ToString("F1")).Append("% ← ");
                foreach (var c in Top(callers[kv.Key], 3)) sbo.Append(c.Key).Append(' ').Append((100.0 * c.Value / count).ToString("F1")).Append("%; ");
                sbo.Append("\n  ");
            }
            ips = null; images = null; count = 0; cache.Clear();
            return sbo.ToString();
        }
        private static List<KeyValuePair<string, int>> Top(Dictionary<string, int> d, int n)
        {
            var l = new List<KeyValuePair<string, int>>(d); l.Sort((a, b) => b.Value.CompareTo(a.Value)); if (l.Count > n) l.RemoveRange(n, l.Count - n); return l;
        }
    }
}
#endif
