#if DEV || AUTOTEST
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Globalization;
using System.IO;

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
        private const int MaxSamples = 40000, Depth = 32, FullSamples = 150000;
        private static int capacity;
        private static bool fullMode;
        private static volatile float publishedSong = float.NaN;
        private static volatile int publishedFrame = -1;
        private static float[] sampleSongs;
        private static int[] sampleFrames;
        private static long[] sampleStamps, suspendTicks;
        private static long suspendTotal, suspendMax, attempts, contextFailures, suspendFailures;
        internal static void PublishFrame(float song, int frame) { publishedSong = song; publishedFrame = frame; }
        private static ulong[] ips;       // capacity * Depth (ring은 RingN), 0 이면 끝
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
        // 링 모드(profsamp begin 500 native ring): 곡 내내 표본을 링 버퍼에 돌려 쓰고, 모니터가 끊김을 잡으면 그 프레임 구간의 표본만 따로 보관한다(2026-11-22, "게임 바깥" 끊김 중 메인 스레드가 어디서 기다렸는지 보려고).
        private static bool ringMode; private const int RingN = 8192, MaxHitches = 500;
        private static long[] rts; private static long ringHead;
        private sealed class HitchCap { internal float Ms, SongT; internal string Wall; internal List<ulong[]> Stacks = new List<ulong[]>(); internal List<double> AgeMs = new List<double>(); }
        private static readonly List<HitchCap> hitchCaps = new List<HitchCap>();
        internal static void OnHitch(float ms, float songT)
        {
            if (!ringMode || !run || hitchCaps.Count >= MaxHitches) return;
            long now = Stopwatch.GetTimestamp();
            long from = now - (long)((ms + 6f) * Stopwatch.Frequency / 1000.0);
            long head = Interlocked.Read(ref ringHead);
            var cap = new HitchCap { Ms = ms, SongT = songT, Wall = DateTime.Now.ToString("HH:mm:ss.fff") };
            long first = Math.Max(0, head - RingN + 4);
            for (long k = first; k < head - 1; k++)
            {
                int slot = (int)(k % RingN);
                long ts = rts[slot];
                if (ts < from || ts > now) continue;
                var st = new ulong[Depth];
                Array.Copy(ips, slot * Depth, st, 0, Depth);
                cap.Stacks.Add(st); cap.AgeMs.Add((now - ts) * 1000.0 / Stopwatch.Frequency);
            }
            hitchCaps.Add(cap);
        }
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
            if (run || (thread != null && thread.IsAlive)) return "이미 도는 중";
            var args = (arg ?? "").Trim().Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
            if (args.Skip(1).Any(a => a != "native" && a != "ring" && a != "full" && !a.StartsWith("max=") && !a.StartsWith("thread=")))
                throw new ArgumentException("profsamp begin [hz] [native] [ring|full] [max=150000] [thread=이름 일부]");
            int hz = 1000; if (args.Length > 0) int.TryParse(args[0], out hz); if (hz < 50 || hz > 4000) hz = 1000; hzUsed = hz;
            nativeUnwind = args.Contains("native"); ringMode = args.Contains("ring"); hitchCaps.Clear(); ringHead = 0;
            fullMode = args.Contains("full"); capacity = fullMode ? FullSamples : MaxSamples;
            if (ringMode && fullMode) throw new ArgumentException("ring과 full은 함께 사용 불가");
            var limit = args.Where(a => a.StartsWith("max=")).ToArray();
            if (limit.Length > 1 || (limit.Length == 1 && (!fullMode || !int.TryParse(limit[0].Substring(4), out capacity) || capacity < 1 || capacity > 1000000)))
                throw new ArgumentException("max는 full에서만 1..1000000 (표본당 약 280바이트)");
            string threadName = args.Where(a => a.StartsWith("thread=")).Select(a => a.Substring(7).Replace('_', ' ')).FirstOrDefault();
            nativeFrames = 0; failure = null;
            suspendTotal = suspendMax = attempts = contextFailures = suspendFailures = 0;
            // Resolve modules while the target is running. Never enter Mono/JIT
            // function-table callbacks or enumerate modules with it suspended.
            var ranges = new List<ImageRange>();
            if (nativeUnwind)
                using (var process = Process.GetCurrentProcess())
                    foreach (ProcessModule module in process.Modules)
                        ranges.Add(new ImageRange { Start = (ulong)module.BaseAddress.ToInt64(),
                            End = (ulong)module.BaseAddress.ToInt64() + (ulong)module.ModuleMemorySize });
            images = ranges.ToArray();
            ips = new ulong[(ringMode ? RingN : capacity) * Depth]; count = 0; if (ringMode) rts = new long[RingN];
            sampleSongs = fullMode ? new float[capacity] : null; sampleFrames = fullMode ? new int[capacity] : null;
            sampleStamps = fullMode ? new long[capacity] : null; suspendTicks = fullMode ? new long[capacity] : null;
            uint tid = GetCurrentThreadId();
            if (threadName != null) tid = FindThread(threadName);
            mainHandle = OpenThread(0x0002 | 0x0008 | 0x0040, false, tid);   // SUSPEND_RESUME | GET_CONTEXT | QUERY_INFORMATION
            if (mainHandle == IntPtr.Zero) return "OpenThread 실패 " + Marshal.GetLastWin32Error();
            run = true; t0 = Stopwatch.GetTimestamp();
            thread = new Thread(Loop) { IsBackground = true, Priority = ThreadPriority.AboveNormal, Name = "StutterFix.ProfSamp" };
            thread.Start();
            return "시작 (" + hz + "Hz, 스레드 " + tid + ", native_unwind=" + nativeUnwind + ", full=" + fullMode + ", capacity=" + (ringMode ? RingN : capacity) + ")";
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
                while (run && (ringMode || count < capacity))
                {
                    next += period;
                    while (Stopwatch.GetTimestamp() < next) { Thread.SpinWait(50); if (!run) break; }
                    if (!run) break;
                    long suspendStart = Stopwatch.GetTimestamp(); int sampleIndex = count;
                    attempts++;
                    if (SuspendThread(mainHandle) == 0xFFFFFFFF) { suspendFailures++; continue; }
                    try
                    {
                        Marshal.WriteInt32(ctx, 0x30, 0x1000B);   // CONTEXT_FULL (x64)
                        if (GetThreadContext(mainHandle, ctx))
                        {
                            if (fullMode)
                            {
                                sampleSongs[count] = publishedSong; sampleFrames[count] = publishedFrame;
                                sampleStamps[count] = Stopwatch.GetTimestamp();
                            }
                            if (ringMode)
                            {
                                long h = Interlocked.Read(ref ringHead); int slot = (int)(h % RingN);
                                Array.Clear(ips, slot * Depth, Depth);
                                CaptureNative(ctx, slot * Depth); rts[slot] = Stopwatch.GetTimestamp();
                                Interlocked.Increment(ref ringHead); count++;
                            }
                            else if (nativeUnwind) { CaptureNative(ctx, count * Depth); count++; }
                            else
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
                        }
                        else contextFailures++;
                    }
                    finally
                    {
                        ResumeThread(mainHandle);
                        long cost = Stopwatch.GetTimestamp() - suspendStart;
                        suspendTotal += cost; if (cost > suspendMax) suspendMax = cost;
                        if (fullMode && count > sampleIndex) suspendTicks[sampleIndex] = cost;
                    }
                    // 지연 뒤 몰아서 연속 정지하지 않는다. 실제 표본률/누락은 meta로 보고한다.
                    long after = Stopwatch.GetTimestamp(); if (next < after - period) next = after;
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
            if (!run && count == 0 && mainHandle == IntPtr.Zero) return "시작 안 함";
            run = false;
            if (thread != null && !thread.Join(2000)) return "표본 스레드 종료 대기 초과: 핸들/배열 보존, end 재시도 필요";
            t1 = Stopwatch.GetTimestamp();
            try { if (mainHandle != IntPtr.Zero) CloseHandle(mainHandle); } catch { }
            mainHandle = IntPtr.Zero;
            double secs = (t1 - t0) / (double)Stopwatch.Frequency;
            if (framesDir != null)
            {
                // 정지 시작 직전~ResumeThread 복귀의 벽시계 상한 추정. OS 선점 시간도 포함한다.
                string meta = string.Format(CultureInfo.InvariantCulture,
                    "{{\"full\":{0},\"samples\":{1},\"capacity\":{2},\"capacity_reached\":{3},\"requested_hz\":{4},\"elapsed_s\":{5:R},\"attempts\":{6},\"failed_contexts\":{7},\"failed_suspends\":{11},\"suspend_total_ms\":{8:R},\"suspend_max_ms\":{9:R},\"sampling_error\":{10}}}",
                    fullMode ? "true" : "false", count, ringMode ? RingN : capacity, !ringMode && count >= capacity ? "true" : "false", hzUsed, secs, attempts, contextFailures,
                    suspendTotal * 1000.0 / Stopwatch.Frequency, suspendMax * 1000.0 / Stopwatch.Frequency, failure == null ? "false" : "true", suspendFailures);
                File.WriteAllText(Path.Combine(framesDir, "profsamp-meta.json"), meta);
            }
            if (ringMode)
            {
                IntPtr dom = IntPtr.Zero; try { dom = mono_get_root_domain(); } catch (Exception ex) { return "mono_get_root_domain 실패: " + ex.Message; }
                var fb2 = new StringBuilder();
                foreach (var cap in hitchCaps)
                {
                    fb2.Append("### ").Append(cap.Ms.ToString("F0")).Append("ms song=").Append(cap.SongT.ToString("F1")).Append(" wall=").Append(cap.Wall).Append(" samples=").Append(cap.Stacks.Count).Append('\n');
                    for (int i = 0; i < cap.Stacks.Count; i++)
                    {
                        fb2.Append('@').Append(cap.AgeMs[i].ToString("F1")).Append(' ');
                        var st = cap.Stacks[i];
                        for (int d = 0; d < Depth; d++)
                        {
                            ulong ip = st[d]; if (ip == 0) break;
                            bool g; string n = Resolve(ip, dom, out g);
                            if (d > 0) fb2.Append(';');
                            if (g) fb2.Append("M:").Append(n);
                            else if (n.StartsWith("[UnityPlayer.dll+0x")) fb2.Append("U:").Append(n.Substring(19, n.Length - 20));
                            else fb2.Append("X:").Append(n);
                        }
                        fb2.Append('\n');
                    }
                }
                if (framesDir != null) File.WriteAllText(Path.Combine(framesDir, "profsamp-hitches.txt"), fb2.ToString());
                string ringResult = "링 모드: 끊김 " + hitchCaps.Count + "개 보관(상한 500), 표본 " + count + "개 / " + secs.ToString("F1") + "초";
                ringMode = false; ips = null; rts = null; images = null; count = 0; hitchCaps.Clear(); cache.Clear();
                return ringResult;
            }
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
                WriteFrames(framesDir, domain);
            }
            var sbo = new StringBuilder();
            sbo.Append("native_unwind=").Append(nativeUnwind).Append(" native_frames=").Append(nativeFrames).Append(" max_depth=").Append(Depth).Append('\n');
            sbo.AppendFormat(CultureInfo.InvariantCulture, "full={0} capacity_reached={1} suspend_total_ms={2:F3} suspend_max_ms={3:F3} attempts={4} failed_contexts={5} failed_suspends={6}\n",
                fullMode, count >= capacity, suspendTotal * 1000.0 / Stopwatch.Frequency, suspendMax * 1000.0 / Stopwatch.Frequency, attempts, contextFailures, suspendFailures);
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
            ips = null; images = null; sampleSongs = null; sampleFrames = null; sampleStamps = null; suspendTicks = null; count = 0; cache.Clear();
            return sbo.ToString();
        }

        private static void WriteFrames(string directory, IntPtr domain)
        {
            using (var w = new StreamWriter(Path.Combine(directory, "profsamp-frames.txt"), false, new UTF8Encoding(false)))
                for (int i = 0; i < count; i++)
                {
                    if (fullMode) w.Write(string.Format(CultureInfo.InvariantCulture, "T:{0:R};F:{1};W:{2:R};S:{3:R};",
                        sampleSongs[i], sampleFrames[i], sampleStamps[i] / (double)Stopwatch.Frequency, suspendTicks[i] * 1000000.0 / Stopwatch.Frequency));
                    for (int d = 0; d < Depth; d++)
                    {
                        ulong ip = ips[i * Depth + d]; if (ip == 0) break;
                        bool managed; string name = Resolve(ip, domain, out managed);
                        if (d > 0) w.Write(';');
                        if (managed) w.Write("M:" + name);
                        else if (name.StartsWith("[UnityPlayer.dll+0x")) w.Write("U:" + name.Substring(19, name.Length - 20));
                        else w.Write("X:" + name);
                    }
                    w.WriteLine();
                }
        }
        private static List<KeyValuePair<string, int>> Top(Dictionary<string, int> d, int n)
        {
            var l = new List<KeyValuePair<string, int>>(d); l.Sort((a, b) => b.Value.CompareTo(a.Value)); if (l.Count > n) l.RemoveRange(n, l.Count - n); return l;
        }
    }
}
#endif
