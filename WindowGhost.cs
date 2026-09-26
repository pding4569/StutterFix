using System;
using System.Runtime.InteropServices;

namespace StutterFix
{
    // 첫 판 FPS 떨어짐 막기: 이 게임에서만 윈도우의 "응답 없음" 고스트 창을 끈다.
    //
    // 증상(2026-09-26~27, Arche, RTX 4060 Ti, 3440x1440 165Hz): 맵을 열고 에디터에서 Play 한 판은 곡 내내 프레임마다 1.7ms 를 더
    // 기다려 약 200 FPS, 죽고 다시 하기로 시작한 판은 약 320 FPS. 한 판 안에서는 끝까지 같은 상태였고, 첫 판 쪽이 화면에 나오기까지도
    // 약 5ms 늦었다(PresentMon). 기다리는 곳은 유니티의 TimeUpdate.WaitForLastPresentationAndUpdateTime: 방금 넘긴 프레임을 GPU 가
    // 끝낼 때까지 기다리는 모양이었다(모든 프레임이 1.65ms 씩 통째로 밀림).
    //
    // 원인이 아니었던 것: 화면 출력 방식(BitBlt/Flip), 멀티스레드 그리기, 프레임 시간 통계, NVIDIA 저지연 모드, maxQueuedFrames,
    //   화면 위 다른 창(Lilith), 디스코드. 장면도 같았다(카메라·캔버스·프레임당 Blit 수 같음, GPU 를 기다리게 하는 호출 없음).
    //   곡 중에 해 본 것은 전부 효과 없음: 수직동기·프레임 제한·미리 준비 프레임 수 바꾸기, 화면 넘기기 직전 그래픽 스레드에 일 얹기,
    //   GPU 바쁘게, 유니티 대기 단계 한 프레임 건너뛰기/두 번, Alt+Tab.
    // 단서: 느린 판은 Play 를 누르고 곡이 시작되기까지 한 프레임이 5초를 넘은 경우(Arche 에디터 Play 5.2~5.8초)에만 나왔다.
    //   다시 하기(3.2~3.9초), MEGAMIX Play(2.7~3.1초)는 늘 빨랐다. 윈도우는 창이 5초 넘게 메시지를 처리하지 않고 그 사이 입력이 있으면
    //   창을 "응답 없음" 고스트 창으로 바꿔 둔다 - 입력에 따라 걸리거나 안 걸려 운처럼 보였다.
    // 확인: 맵을 연 뒤 첫 에디터 Play 는 13번 중 10번 느렸는데(Alt+Tab 직후를 빼면 11번 중 10번), 고스트 창을 끈 뒤 새로 켠 게임 2번의
    //   첫 Play 는 둘 다 빨랐다(302 / 297 FPS, 대기 0).
    //
    // DisableProcessWindowsGhosting 은 되돌리는 API 가 없다. 설정에서 끄면 다음 실행부터 원래대로다(모드를 꺼도 이번 실행 동안은 꺼진 채).
    // 부작용: 게임이 정말 멈췄을 때 "응답 없음" 표시가 안 뜨고, 그 상태에서 창을 옮기거나 최소화할 수 없다.
    //
    // 고스트 창만 꺼서는 모자랐다(2026-09-27): 고스트 창을 끈 채로도 Play 멈춤이 5.45초였던 판은 느렸다(197 FPS, 대기 1.7ms).
    // 같은 날 플레이어용 2.3.0 은 Play 멈춤이 4.7초라 빨랐다. 윈도우는 고스트 창과 별개로 "5초 넘게 메시지를 확인하지 않은 창" 을
    // 멈춘 창으로 판정하고(IsHungAppWindow), 느린 상태는 이 판정 자체에서 생기는 것으로 본다. 그래서 긴 프레임 동안 모드가 이미 걸어 둔
    // 자리(장식 이미지 파일 시각, 충돌 상자 켜고 끄기, 이미지 넣기, 장면 되돌리기·재생 준비 앞뒤)에서, 프레임이 0.5초를 넘으면 0.5초마다
    // 입력 큐를 확인만 한다(PeekMessage, PM_NOREMOVE | PM_QS_INPUT). 꺼내지 않으므로 입력은 게임이 원래처럼 다음 프레임에 처리한다.
    // 시험(C:\SFBundle\HungTest, UI 스레드를 8초 멈추고 1초마다 호출, IsHungAppWindow 를 0.05초마다 확인):
    //   아무것도 안 함 / 게시된 메시지만 확인(PM_QS_POSTMESSAGE) / 보낸 것만 / GetInputState / GetQueueStatus -> 판정됨
    //   MsgWaitForMultipleObjectsEx 0ms -> 입력이 쌓여 있으면 판정됨
    //   PeekMessage 입력만(PM_QS_INPUT) 또는 전부 -> 입력이 쌓여 있어도 판정 안 됨
    //   그중 입력만 확인하는 쪽은 다른 스레드가 SendMessage 로 보낸 메시지를 그 자리에서 처리하지 않음(0번, 전부 확인은 1번 처리)
    //   -> 긴 프레임 한가운데서 게임의 창 처리가 끼어들지 않는다. 메인 스레드에서만 부른다.
    // (2026-09-27 첫 시도는 PM_QS_POSTMESSAGE 로 해서 효과가 없었다: 맵 불러오기 17.5초 프레임에서 15번 확인하고도 23.4초 판정)
    internal static class WindowGhost
    {
        [StructLayout(LayoutKind.Sequential)] private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int x, y; }
        [DllImport("user32.dll")] private static extern bool PeekMessageW(out MSG msg, IntPtr hWnd, uint min, uint max, uint flags);
        private const uint PM_NOREMOVE = 0x0, PM_NOYIELD = 0x2, PM_QS_INPUT = 0x0407u << 16;   // QS_MOUSE | QS_KEY | QS_RAWINPUT

        internal static bool KeepResponsive = true;
        private static int frame = -1;
        private static long frameStart, lastPeek;
        private static int peeksThisFrame;
        internal static long Peeks;

        internal static int MainThread = -1;   // Load 에서 적는다(메인 스레드 말고는 아무것도 안 한다)
        private static int everyCount;

        // 아주 자주 불리는 곳(맵 파일 해석, 이벤트 읽기, 타일 만들기)용: 1024번에 한 번만 Tick
        internal static void TickEvery() { if ((++everyCount & 1023) == 0) Tick(); }

        // 메인 스레드가 5초 넘게 확인하지 않던 단계에도 확인 자리를 더 둔다(2026-09-27 로그: 입력 큐 확인으로 바꾼 뒤에도 맵 불러오기 5.8초,
        // Play 0.3~0.5초 판정이 남았음 - 맵 파일 해석·이벤트 읽기, Play 앞쪽의 타일 다시 만들기 구간).
        internal static void Install(HarmonyLib.Harmony h)
        {
            FastJsonParser.Progress = TickEvery;
            DecodeFix.Progress = TickEvery;
            var reset = HarmonyLib.AccessTools.Method(typeof(scrLevelMaker), "ResetFloor");
            if (reset != null) h.Patch(reset, prefix: new HarmonyLib.HarmonyMethod(typeof(WindowGhost), nameof(TickEvery)));
            var core = HarmonyLib.AccessTools.Method(typeof(scnGame), "ApplyCoreEventsToFloors");
            if (core != null) h.Patch(core, prefix: new HarmonyLib.HarmonyMethod(typeof(WindowGhost), nameof(Tick)), postfix: new HarmonyLib.HarmonyMethod(typeof(WindowGhost), nameof(Tick)));
        }

        // 긴 프레임 안에서 자주 불리는 곳에서 부른다. 짧은 프레임에서는 시각 비교만 한다.
        internal static void Tick()
        {
            if (!KeepResponsive) return;
            if (MainThread != -1 && Environment.CurrentManagedThreadId != MainThread) return;
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            int f;
            try { f = UnityEngine.Time.frameCount; } catch { return; }   // 메인 스레드가 아니면 아무것도 안 한다
            if (f != frame)
            {
                string note; while (notes.TryDequeue(out note)) Main.Entry.Logger.Log(note);
                if (peeksThisFrame > 0)
                    Main.Entry.Logger.Log(string.Format("[첫 판 FPS] {0:F1}초 멈춘 프레임 동안 윈도우 메시지 확인 {1}번 (멈춘 창 판정 막기)",
                        (lastPeek - frameStart) / (double)System.Diagnostics.Stopwatch.Frequency, peeksThisFrame));
                frame = f; frameStart = now; lastPeek = now; peeksThisFrame = 0;
                return;
            }
            if (now - lastPeek < System.Diagnostics.Stopwatch.Frequency / 2) return;   // 프레임 0.5초 뒤부터 0.5초마다
            lastPeek = now;
            try { MSG m; PeekMessageW(out m, IntPtr.Zero, 0, 0, PM_NOREMOVE | PM_NOYIELD | PM_QS_INPUT); peeksThisFrame++; Peeks++; }
            catch { KeepResponsive = false; }
        }

        [DllImport("user32.dll")] private static extern void DisableProcessWindowsGhosting();

        // (개발자용) 윈도우가 게임 창을 멈춘 창으로 판정하는지 옆 스레드에서 0.25초마다 본다(IsHungAppWindow 는 아무 스레드에서나 부를 수 있다).
        // 판정이 나면 몇 초째였는지 모아 두었다가 메인 스레드(Tick)에서 적는다.
        [DllImport("user32.dll")] private static extern bool IsHungAppWindow(IntPtr hwnd);
        private static System.Threading.Thread watch;
        private static readonly System.Collections.Concurrent.ConcurrentQueue<string> notes = new System.Collections.Concurrent.ConcurrentQueue<string>();
        private static volatile bool watching;
        internal static void StartWatch()
        {
            if (watch != null) return;
            watching = true;
            watch = new System.Threading.Thread(() =>
            {
                IntPtr hwnd = IntPtr.Zero; bool hung = false; long since = 0;
                while (watching)
                {
                    try
                    {
                        if (hwnd == IntPtr.Zero) hwnd = PresentWatch.FindGameWindow();
                        bool h = hwnd != IntPtr.Zero && IsHungAppWindow(hwnd);
                        long now = System.Diagnostics.Stopwatch.GetTimestamp();
                        if (h && !hung) since = now;
                        if (!h && hung) notes.Enqueue(string.Format("[첫 판 FPS] 윈도우가 게임 창을 멈춘 창으로 판정했음 ({0:F1}초 동안)", (now - since) / (double)System.Diagnostics.Stopwatch.Frequency));
                        hung = h;
                    }
                    catch { }
                    System.Threading.Thread.Sleep(250);
                }
            }) { IsBackground = true, Name = "StutterFix.HungWatch", Priority = System.Threading.ThreadPriority.BelowNormal };
            watch.Start();
        }
        internal static void StopWatch() { watching = false; watch = null; }
        // 매 프레임 (PresentWatch.Frame): 옆 스레드가 모은 판정 기록을 적는다
        internal static void Flush() { string note; while (notes.TryDequeue(out note)) Main.Entry.Logger.Log(note); }

        internal static bool Done;
        internal static string Status = "";

        internal static void Disable()
        {
            if (Done) return;
            try { DisableProcessWindowsGhosting(); Done = true; Status = "응답 없음 창 끔"; }
            catch (Exception ex) { Status = "응답 없음 창 끄기 실패: " + ex.Message; }
            Main.Entry.Logger.Log("[첫 판 FPS] " + Status);
        }
    }
}
