using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace StutterFix
{
    // 곡 중 "화면 대기"(메인 스레드가 윈도우에 화면을 넘기며 기다린 시간)가 늘어난 순간의 게임 바깥 상태를 남긴다.
    //
    // 같은 맵 같은 구간에서 CPU(3.7ms)·GPU(1.5ms) 일은 그대로인데 화면 대기만 0 → 1.7ms 로 늘어 FPS 가 250 → 186 이 된 판이
    // 섞여 나왔다(2026-09-26, 원인은 원격 데스크톱 StarDesk: 끄자 사라짐). 게임·모드가 하는 일이 아니라 윈도우가 화면을 받아 주는 쪽이다: 게임 창 위에 다른 창(오버레이,
    // 항상 위 창)이 겹치면 윈도우가 게임 화면을 바로 내보내지 못하고 한 번 더 합성하고, 다른 프로그램이 GPU 를 쓰면 게임 프레임이
    // 그 뒤에서 기다린다. 그래서 대기가 늘어난 순간과 다시 줄어든 순간에 (1) 게임 창이 앞에 있는지 (2) 게임 창 위에 겹친 창의
    // 프로그램 이름과 창 종류 (3) GPU 를 쓰는 다른 프로그램을 적는다. 창 제목은 적지 않는다(개인 정보).
    // 판정은 1초 평균으로 하고 2초 이어져야 상태를 바꾼다. 조사는 작업 스레드에서, 한 판에 최대 6번.
    internal static class PresentWatch
    {
        private const float HighMs = 0.8f;
        private static double secMs, secWait; private static int secN;
        private static bool high; private static int streak;
        private static int songSec, songHighSec, snaps; private static double songHighWait;
        private static bool wasPlaying;
        private static volatile bool busy;
        private static readonly List<string> pending = new List<string>();

        // 메인 스레드에서 매 프레임 (PerfOverlay.MeasureFrame)
        internal static void Frame(bool playing, float ms, float wait)
        {
            if (pending.Count > 0) lock (pending) { foreach (var p in pending) Main.Entry.Logger.Log(p); pending.Clear(); }
            if (playing != wasPlaying)
            {
                if (!playing && songHighSec > 0)
                    Main.Entry.Logger.Log(string.Format("[화면 대기] 이번 판 {0}초 중 {1}초 동안 늘어나 있었음 (그때 평균 {2:F1}ms)", songSec, songHighSec, songHighWait / songHighSec));
                string gpu = GpuSummary();
                if (!playing && gpu != null) Main.Entry.Logger.Log(gpu);

                wasPlaying = playing; secMs = secWait = 0; secN = 0; high = false; streak = 0; songSec = songHighSec = snaps = 0; songHighWait = 0;
            }
            if (!playing || ms > 500f) return;
            secMs += ms; secWait += wait; secN++;
            if (secMs < 1000) return;
            float avgWait = (float)(secWait / secN), avgMs = (float)(secMs / secN);
            secMs = secWait = 0; secN = 0;
            bool h = avgWait >= HighMs && avgWait >= avgMs * 0.15f;
            songSec++; if (h) { songHighSec++; songHighWait += avgWait; }
            if (Edition.Dev) SampleGpu(h);
            if (h == high) { streak = 0; return; }
            if (++streak < 2) return;
            high = h; streak = 0;
            string head = h ? string.Format("[화면 대기] 늘어남: 평균 {0:F1}ms (프레임 {1:F1}ms 중, 약 {2:F0} FPS)", avgWait, avgMs, 1000 / avgMs)
                            : string.Format("[화면 대기] 다시 줄어듦: 평균 {0:F1}ms (약 {1:F0} FPS)", avgWait, 1000 / avgMs);
            if (snaps >= 6 || busy) { Main.Entry.Logger.Log(head); return; }
            snaps++; busy = true;
            float fpsNow = 1000 / avgMs, fpsWithout = 1000 / Math.Max(0.5f, avgMs - avgWait);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                string line;
                try
                {
                    overNames.Clear(); encNames.Clear();
                    line = head + " | " + Snapshot();
                    if (h) MakeNotice(avgWait, fpsNow, fpsWithout);
                }
                catch (Exception ex) { line = head + " | 조사 실패: " + ex.Message; }
                lock (pending) pending.Add(line);
                busy = false;
            });
        }

        // ── (개발자용) 곡 중 GPU 클럭 (NVIDIA, GpuClock.cs) ──
        // 1초마다 작업 스레드에서 한 번 읽어 판마다 모은다. 화면 대기가 늘었던 초와 아닌 초를 나눠 비교한다.
        private static readonly object gpuLock = new object();
        private static int gpuGen, gpuN, gpuIdle;
        private static long gpuGr, gpuMem, gpuUtil;
        private static uint gpuMin = uint.MaxValue, gpuMax;
        private static readonly int[] gpuWaitN = new int[2];
        private static readonly long[] gpuWaitGr = new long[2], gpuWaitMem = new long[2];
        private static readonly SortedDictionary<int, int> gpuP = new SortedDictionary<int, int>();
        private static volatile bool gpuBusy;

        private static void SampleGpu(bool waitHigh)
        {
            if (gpuBusy) return;
            gpuBusy = true;
            int gen = gpuGen;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var s = GpuClock.Read();
                    if (!s.Ok) return;
                    lock (gpuLock)
                    {
                        if (gen != gpuGen) return;   // 그 사이 판이 바뀌었다
                        gpuN++; gpuGr += s.Gr; gpuMem += s.Mem; gpuUtil += s.Util;
                        if (s.Gr < gpuMin) gpuMin = s.Gr;
                        if (s.Gr > gpuMax) gpuMax = s.Gr;
                        if ((s.Reasons & GpuClock.ReasonIdle) != 0) gpuIdle++;
                        int k = waitHigh ? 1 : 0;
                        gpuWaitN[k]++; gpuWaitGr[k] += s.Gr; gpuWaitMem[k] += s.Mem;
                        int c; gpuP.TryGetValue(s.PState, out c); gpuP[s.PState] = c + 1;
                    }
                }
                catch { }
                finally { gpuBusy = false; }
            });
        }

        // 판이 바뀔 때(메인 스레드): 모은 것을 한 줄로 만들고 비운다. 모은 게 없으면 null.
        private static string GpuSummary()
        {
            lock (gpuLock)
            {
                gpuGen++;
                string line = null;
                if (gpuN > 0)
                {
                    var ps = new List<string>();
                    foreach (var kv in gpuP) ps.Add((kv.Key >= 0 ? "P" + kv.Key : "?") + " " + kv.Value + "초");
                    line = string.Format("[GPU 클럭] 이번 판 {0}초: 그래픽 평균 {1} MHz (최저 {2}, 최고 {3}), 메모리 평균 {4} MHz, 사용률 평균 {5}%, 성능 상태 {6}, 쉬는 중으로 클럭 내림 {7}초",
                        gpuN, gpuGr / gpuN, gpuMin, gpuMax, gpuMem / gpuN, gpuUtil / gpuN, string.Join(", ", ps.ToArray()), gpuIdle);
                    if (gpuWaitN[1] > 0)
                        line += string.Format(" | 화면 대기 늘었을 때 {0}초: 그래픽 {1} / 메모리 {2} MHz", gpuWaitN[1], gpuWaitGr[1] / gpuWaitN[1], gpuWaitMem[1] / gpuWaitN[1]);
                    if (gpuWaitN[0] > 0)
                        line += string.Format(" | 대기 없을 때 {0}초: 그래픽 {1} / 메모리 {2} MHz", gpuWaitN[0], gpuWaitGr[0] / gpuWaitN[0], gpuWaitMem[0] / gpuWaitN[0]);
                }
                else if (Edition.Dev && GpuClock.Status != "사용 중" && GpuClock.Status != "안 불러옴") line = "[GPU 클럭] 못 읽음: " + GpuClock.Status;
                gpuN = gpuIdle = 0; gpuGr = gpuMem = gpuUtil = 0; gpuMin = uint.MaxValue; gpuMax = 0;
                gpuWaitN[0] = gpuWaitN[1] = 0; gpuWaitGr[0] = gpuWaitGr[1] = gpuWaitMem[0] = gpuWaitMem[1] = 0; gpuP.Clear();
                return line;
            }
        }

        // ── 설정 창 홈에 보일 안내 (사용자가 원인 프로그램을 알 수 있게) ──
        internal static volatile string Notice;
        internal static bool NoticeDismissed;
        private static readonly List<string> overNames = new List<string>(), encNames = new List<string>();
        private static void MakeNotice(float wait, float fps, float fpsWithout)
        {
            string head = SettingsWindow.T(
                string.Format("곡 중에 윈도우가 게임 화면을 한 번 더 합성하느라 프레임마다 {0:F1}ms 를 기다렸습니다 (이때 약 {1:F0} FPS, 기다림이 없으면 약 {2:F0} FPS). 게임과 모드가 하는 일은 그대로입니다.", wait, fps, fpsWithout),
                string.Format("During play, Windows composited the game screen an extra time and the game waited {0:F1} ms per frame (about {1:F0} FPS; about {2:F0} FPS without the wait). The game and mod workload is unchanged.", wait, fps, fpsWithout));
            string cause;
            if (overNames.Count > 0)
                cause = SettingsWindow.T("게임 화면 위에 겹친 창: " + string.Join(", ", overNames.ToArray()) + ". 이 프로그램(데스크톱 캐릭터, 오버레이 등)을 끄면 FPS 가 오릅니다.",
                    "Windows on top of the game: " + string.Join(", ", overNames.ToArray()) + ". Closing these programs (desktop pets, overlays, etc.) should raise FPS.");
            else if (encNames.Count > 0)
                cause = SettingsWindow.T("화면을 캡처하는 프로그램: " + string.Join(", ", encNames.ToArray()) + " (원격 데스크톱·녹화·방송). 게임할 때 끄면 FPS 가 오릅니다.",
                    "Programs capturing the screen: " + string.Join(", ", encNames.ToArray()) + " (remote desktop, recording, streaming). Closing them while playing should raise FPS.");
            else
                cause = SettingsWindow.T("원인 프로그램을 찾지 못했습니다. 원격 데스크톱·녹화·오버레이·데스크톱 캐릭터 프로그램이 켜져 있는지 확인해 보세요.",
                    "Couldn't find the program responsible. Check for remote desktop, recording, overlay or desktop pet programs.");
            Notice = head + "\n" + cause;
            NoticeDismissed = false;
        }

        // ── 조사 (작업 스레드) ─────────────────────────────────────────
        private static readonly Dictionary<uint, string> names = new Dictionary<uint, string>();

        private static string Snapshot()
        {
            var sb = new StringBuilder();
            IntPtr game = FindGameWindow();
            if (game == IntPtr.Zero) return "게임 창 못 찾음 | " + OtherGpu();
            IntPtr fg = GetForegroundWindow();
            sb.Append(fg == game ? "게임 창이 앞에 있음" : "게임 창이 앞에 없음 (앞: " + Describe(fg) + ")");
            RECT gr;
            if (GetWindowRect(game, out gr))
            {
                var above = new List<string>();
                int guard = 0;
                for (IntPtr w = GetWindow(game, GW_HWNDPREV); w != IntPtr.Zero && guard < 1000; w = GetWindow(w, GW_HWNDPREV), guard++)
                {
                    if (!IsWindowVisible(w) || Cloaked(w)) continue;
                    RECT r; if (!GetWindowRect(w, out r)) continue;
                    int ix = Math.Min(r.Right, gr.Right) - Math.Max(r.Left, gr.Left), iy = Math.Min(r.Bottom, gr.Bottom) - Math.Max(r.Top, gr.Top);
                    if (ix <= 0 || iy <= 0) continue;
                    { uint op; GetWindowThreadProcessId(w, out op); string pn = ProcName(op); if (!overNames.Contains(pn)) overNames.Add(pn); }
                    if (above.Count < 8) above.Add(Describe(w) + " " + ix + "x" + iy + Flags(w));
                    else { above.Add("…"); break; }
                }
                sb.Append(" | 게임 창(").Append(gr.Right - gr.Left).Append('x').Append(gr.Bottom - gr.Top).Append(") 위에 겹친 창 ").Append(above.Count).Append("개");
                if (above.Count > 0) sb.Append(": ").Append(string.Join(", ", above.ToArray()));
            }
            sb.Append(" | ").Append(OtherGpu());
            return sb.ToString();
        }

        private static IntPtr FindGameWindow()
        {
            uint self = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            IntPtr found = IntPtr.Zero;
            var cls = new StringBuilder(64);
            EnumWindows((w, l) =>
            {
                uint pid; GetWindowThreadProcessId(w, out pid);
                if (pid != self || !IsWindowVisible(w)) return true;
                cls.Length = 0; GetClassName(w, cls, cls.Capacity);
                if (cls.ToString() != "UnityWndClass") return true;
                found = w; return false;
            }, IntPtr.Zero);
            return found;
        }

        private static string Describe(IntPtr w)
        {
            if (w == IntPtr.Zero) return "없음";
            uint pid; GetWindowThreadProcessId(w, out pid);
            var cls = new StringBuilder(64); GetClassName(w, cls, cls.Capacity);
            return ProcName(pid) + "/" + cls;
        }

        private static string ProcName(uint pid)
        {
            string n;
            lock (names) if (names.TryGetValue(pid, out n)) return n;
            try { n = System.Diagnostics.Process.GetProcessById((int)pid).ProcessName; } catch { n = ImageName(pid) ?? "pid " + pid; }
            lock (names) names[pid] = n;
            return n;
        }

        // 권한이 모자라 Process 로 이름을 못 읽는 프로세스(dwm 등)는 제한된 조회로 실행 파일 이름만 읽는다
        private static string ImageName(uint pid)
        {
            IntPtr hp = IntPtr.Zero;
            try
            {
                hp = OpenProcess(0x1000, false, pid);   // PROCESS_QUERY_LIMITED_INFORMATION
                if (hp == IntPtr.Zero) return null;
                var sb = new StringBuilder(512); int len = sb.Capacity;
                if (!QueryFullProcessImageName(hp, 0, sb, ref len)) return null;
                return System.IO.Path.GetFileNameWithoutExtension(sb.ToString());
            }
            catch { return null; }
            finally { if (hp != IntPtr.Zero) CloseHandle(hp); }
        }
        [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool QueryFullProcessImageName(IntPtr hp, int flags, StringBuilder name, ref int size);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);

        private static string Flags(IntPtr w)
        {
            long ex = GetWindowLongPtr(w, GWL_EXSTYLE).ToInt64();
            string s = "";
            if ((ex & WS_EX_TOPMOST) != 0) s += " 항상위";
            if ((ex & WS_EX_LAYERED) != 0) s += " 반투명";
            if ((ex & WS_EX_TRANSPARENT) != 0) s += " 클릭통과";
            return s;
        }

        private static bool Cloaked(IntPtr w)
        {
            try { int c; return DwmGetWindowAttribute(w, DWMWA_CLOAKED, out c, 4) == 0 && c != 0; } catch { return false; }
        }

        // GPU 를 쓰는 다른 프로그램 (0.5초 동안 두 번 읽은 차이). 3D 말고 영상 인코딩·복사 엔진도 본다:
        // 원격 데스크톱(StarDesk)·녹화·방송 프로그램은 화면을 캡처해 인코딩하므로 3D 는 거의 안 쓰고 이쪽을 쓴다.
        // StarDesk 를 끄자 화면 대기 1.7ms 판(186 FPS)이 사라지고 6판 모두 0ms, 280~293 FPS 가 됐다(2026-09-26).
        private static string OtherGpu()
        {
            var pdh = new SystemMonitor.Pdh();
            try
            {
                if (!pdh.Open()) return "GPU 사용 프로그램: 읽을 수 없음";
                Thread.Sleep(500);
                if (!pdh.Collect()) return "GPU 사용 프로그램: 읽을 수 없음";
                uint self = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
                var d3 = new Dictionary<uint, double>(); var enc = new Dictionary<uint, double>(); var copy = new Dictionary<uint, double>();
                double total = 0;
                pdh.ForEach(pdh.Engine, (name, v) =>
                {
                    if (!name.StartsWith("pid_", StringComparison.Ordinal)) return;
                    int end = name.IndexOf('_', 4), et = name.IndexOf("engtype_", StringComparison.Ordinal);
                    uint pid; if (end < 0 || et < 0 || !uint.TryParse(name.Substring(4, end - 4), out pid)) return;
                    string type = name.Substring(et + 8);
                    Dictionary<uint, double> into = type.Equals("3D", StringComparison.OrdinalIgnoreCase) ? d3
                        : type.StartsWith("VideoEncode", StringComparison.OrdinalIgnoreCase) ? enc
                        : type.Equals("Copy", StringComparison.OrdinalIgnoreCase) ? copy : null;
                    if (into == null) return;
                    double cur; into.TryGetValue(pid, out cur); into[pid] = cur + v;
                    if (into == d3) total += v;
                });
                double mine; d3.TryGetValue(self, out mine);
                var sum = new Dictionary<uint, double>();
                foreach (var m in new[] { d3, enc, copy }) foreach (var kv in m) { if (kv.Key == self) continue; double c; sum.TryGetValue(kv.Key, out c); sum[kv.Key] = c + kv.Value; }
                var list = new List<KeyValuePair<uint, double>>(sum);
                list.Sort((a, b) => b.Value.CompareTo(a.Value));
                var sb = new StringBuilder();
                sb.AppendFormat("GPU 3D 전체 {0:F0}%, 게임 {1:F0}%", total, mine);
                int shown = 0;
                foreach (var kv in list)
                {
                    if (kv.Value < 1 || shown >= 4) break;
                    double a, e, c; d3.TryGetValue(kv.Key, out a); enc.TryGetValue(kv.Key, out e); copy.TryGetValue(kv.Key, out c);
                    sb.Append(shown == 0 ? ", 다른 프로그램: " : ", ").Append(ProcName(kv.Key)).AppendFormat(" 3D {0:F0}%", a);
                    if (e >= 1) { sb.AppendFormat(" 영상 인코딩 {0:F0}%", e); string en = ProcName(kv.Key); if (!encNames.Contains(en)) encNames.Add(en); }
                    if (c >= 1) sb.AppendFormat(" 복사 {0:F0}%", c);
                    shown++;
                }
                if (shown == 0) sb.Append(", 다른 프로그램 1% 넘는 것 없음");
                return sb.ToString();
            }
            catch (Exception ex) { return "GPU 사용 프로그램: " + ex.Message; }
            finally { pdh.Close(); }
        }

        private const int GW_HWNDPREV = 3, GWL_EXSTYLE = -20, DWMWA_CLOAKED = 14;
        private const long WS_EX_TOPMOST = 0x8, WS_EX_TRANSPARENT = 0x20, WS_EX_LAYERED = 0x80000;
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
        private delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, int cmd);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
        [DllImport("user32.dll")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out int value, int size);
    }
}
