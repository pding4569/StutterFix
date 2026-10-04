using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

namespace StutterFix
{
    // 순간 끊김의 큰 축은 GC였다.
    //
    // A/B 측정 (0.8초마다 GC를 멈췄다 켜며 125쌍 비교):
    //   GC 멈춤: 평균 124fps, 최악 프레임 평균 18.9ms, 33ms 넘은 구간 12/125
    //   GC 정상: 평균 106fps, 최악 프레임 평균 45.3ms, 33ms 넘은 구간 118/125
    //
    // 그래서 곡을 플레이하는 동안에는 치우지 않고 쌓아 두기만 하고, 곡이 끝나면 한 번에 정리한다.
    // "곡이 끝났는지"는 예전에 곡 위치가 흐르는지로 봤는데 메뉴에서도 곡이 흘러서 오판했다.
    // 지금은 게임이 직접 들고 있는 상태값(scrController.currentState)을 읽는다.
    public static class GcControl
    {
        internal static bool Enabled = true;
        internal static bool NoCollectDuringSong = true;  // 곡 중에는 조금씩 치우기도 하지 않는다
        internal static int HardLimitMB = 6000;           // 여기 넘으면 끊김을 감수하고 완전 정리
        // 예전에는 300초(5분)였다. 여기 힙에서는 한 번 정리에 700ms 쯤 걸려서, 1시간짜리 맵이면 5분마다 크게 끊겼다.
        // 곡이 끝난 걸 놓친 경우는 아래 "10초간 조용함" 이 잡으므로, 이 시간은 마지막 안전장치로만 둔다.
        // 메모리는 HardLimitMB 가 따로 지킨다 (곡 중 쌓이는 양은 1분에 100MB 안팎이었다).
        internal static float MaxPauseSeconds = 7200f;
        internal static float EndDelaySeconds = 3f;       // 곡이 끝나고 이만큼 기다렸다 정리한다

        // 완주 직후는 마무리 연출이 돌아가는 중이라, 그 순간 정리하면 연출이 끊긴다.
        // 연출이 끝날 때까지 기다렸다가 치운다.
        private static float resumeCountdown = -1f;
        private static string resumeReason;
        // 죽은 뒤(FailAction)에는 3초 뒤에 치우지 않고, 다음 화면 전환(재시작·재생·편집 복귀)까지 미룬다.
        // 사용자 로그(2.0.0, 에디터에서 죽고 다시 하기 반복): 죽고 3초 뒤 실패 화면에서 160~340ms 정리가 끊김 알림으로 떠서
        // "메모리 정리 때문에 끊긴다" 로 보였다. 재시작 순간은 어차피 곡 준비로 400~700ms 멈추므로 그때 같이 치운다.
        // 아무것도 안 하고 10초 있으면(10초간 조용함) 그때 치운다.
        private static bool holdAfterFail, holdAfterWin;
        internal static int IncrementalStartMB = 800;     // 조금씩 치우기 모드에서만 쓴다
        internal static float SliceMs = 2f;
        internal static int SliceEveryFrames = 4;
        // (시험) gc-garbage.txt: 곡 중 초당 이만큼(MB) 쓰레기를 만든다 - 긴 곡의 "힙 6GB 한계" 를 몇 분 만에 재현 (살아 있는 메모리는 그대로)
        internal static float GarbageMBps;
        private static float garbageDebt;
        internal static object GarbageSink;
        private static void MakeGarbage(float dt)
        {
            garbageDebt += GarbageMBps * Time.unscaledDeltaTime;
            // 실제 곡 쓰레기처럼 작은 객체로 (큰 배열 하나는 GC 가 다루는 방식이 달라 재현이 안 된다): 0.1MB 마다 작은 배열 약 1600개
            while (garbageDebt >= 0.1f) { object last = null; for (int i = 0; i < 1600; i++) { var a = new object[6]; a[0] = last; last = (i & 63) == 0 ? null : a; } GarbageSink = last; garbageDebt -= 0.1f; }
        }
        internal static string SliceStyle = "";   // (시험) gc-slice.txt: old = 2.4.0 까지의 한계 치우기(무한 반복 재현용)

        // (개발자용) 조각 치우기 5초마다 요약: 몇 번, 시간 합·최대, 바퀴가 끝난 횟수, GC 횟수 증가, 힙
        private static int sN, sDone, sGc; private static double sMs, sMax; private static long sHeapStart = -1; private static float sT0;
        private static void SliceStat(double ms, bool done, int gcDelta, long heap)
        {
            if (sHeapStart < 0) { sHeapStart = heap; sT0 = Time.realtimeSinceStartup; }
            sN++; sMs += ms; if (ms > sMax) sMax = ms; if (done) sDone++; sGc += gcDelta;
            if (Time.realtimeSinceStartup - sT0 < 5f) return;
            Main.Entry.Logger.Log(string.Format("[GC] (개발자용) 조각 치우기 {0:F1}초: {1}번, 합계 {2:F0}ms, 최대 {3:F1}ms, 바퀴 끝 {4}번, GC 횟수 +{5}, 힙 {6} -> {7}MB ({8})",
                Time.realtimeSinceStartup - sT0, sN, sMs, sMax, sDone, sGc, sHeapStart, heap, SliceStyle));
            sN = sDone = sGc = 0; sMs = sMax = 0; sHeapStart = -1;
        }

        internal static string LastScene = "?";
        internal static int PeakHeapMB;
        internal static long IncrementalSlices;
        internal static long ForcedCollects;
        internal static bool Paused;

        private static float pausedFor;
        private static float quietTimer;
        private static int quietSeq = -2;
        private static bool limitSlicing;   // 곡 중 힙 한계: 한꺼번에 대신 조금씩 치우는 중
        private static bool lastSliceMore;   // 마지막 조각 치우기 뒤 남은 일이 있었는지
        internal static float LimitRestSeconds = 30f;   // 한계 치우기 한 바퀴 뒤 쉬는 시간
        private static float restUntil = -1f;
        private static bool restCheck;       // 쉬는 시간이 끝나면 힙이 줄었는지 한 번 본다
        private static int raisedLimit;      // 이 곡 동안 올린 한계 (0 = 안 올림)
        private static long quietHeapMark;
        private static int frameCounter;
        private static int slowFrame, ramMB;
        private static float slowDt;
        private static bool patched;

        internal static void Install()
        {
            if (patched) return;
            try
            {
                var harmony = new Harmony("StutterFix.GcControl");

                // 맵 파일 읽기(LevelData.LoadLevel: 파일 전체를 문자열로 읽고 JSON 해석, 이벤트 수만 개 만들기) 동안 GC 멈추기
                var levelData = AccessTools.TypeByName("ADOFAI.LevelData") ?? AccessTools.TypeByName("LevelData");
                if (levelData != null) foreach (var m in levelData.GetMethods(AccessTools.all))
                    if (m.Name == "LoadLevel" && !m.IsAbstract && m.DeclaringType == levelData)
                        harmony.Patch(m, prefix: new HarmonyMethod(typeof(GcControl), nameof(ParsePrefix)), finalizer: new HarmonyMethod(typeof(GcControl), nameof(ParseFinalizer)));

                var scnGame = AccessTools.TypeByName("scnGame");
                if (scnGame != null)
                {
                    var load = AccessTools.Method(scnGame, "LoadLevel");
                    if (load != null)
                        harmony.Patch(load, postfix: new HarmonyMethod(typeof(GcControl), nameof(AfterLoad)));
                }

                // 상태값을 지켜보는 대신 "끝나는 순간에 불리는 함수"를 직접 가로챈다.
                // currentState 는 이 버전에서 재생 중에도 None으로 남아 믿을 수 없었다.
                var ends = new[]
                {
                    new[] { "scnEditor", "SwitchToEditMode" },   // 편집으로 복귀
                    new[] { "scrController", "FailAction" },     // 실패
                    new[] { "scrController", "Fail2Action" },
                    new[] { "scrController", "OnLandOnPortal" }, // 완주(포탈 도착)
                    new[] { "scrController", "QuitToMainMenu" },
                    // Checkpoint_Exit 는 넣으면 안 된다. 중간부터 시작할 때 체크포인트 상태에서 플레이로 넘어가며 불리는
                    // 상태 종료 함수(카메라 끄기, 볼륨 되돌리기)다. 곡 끝으로 오인해 3초 뒤 곡 도중에 정리하고, 그 곡 내내 GC를 안 멈췄다.
                    new[] { "scnEditor", "QuitToMenu" },
                    new[] { "scnEditor", "TryQuitToMenu" },
                    new[] { "scnEditor", "SaveAndQuit" },
                };

                // 재시작과 재생 시작은 종료가 아니라 "여기서 한 번 치우고 계속"이다.
                var restarts = new[]
                {
                    new[] { "scrController", "Restart" },
                    new[] { "scnEditor", "Play" },
                    // 에디터에서 죽은 뒤 아무 키나 눌러 다시 할 때는 Restart 가 아니라 이것이다 (Fail2_Update IL 로 확인).
                    // 빠져 있어서 2.0.1 에서는 실패 뒤 정리를 기다리는 동안 다시 한 판들을 곡으로 못 알아보고 GC 를 계속 꺼 둔 채였다(힙 1140MB).
                    new[] { "scrController", "ResetCustomLevel" },
                };

                foreach (var e in ends)
                {
                    try
                    {
                        var type = AccessTools.TypeByName(e[0]);
                        if (type == null) continue;
                        foreach (var m in type.GetMethods(AccessTools.all))
                        {
                            if (m.Name != e[1] || m.IsAbstract || m.ContainsGenericParameters) continue;
                            bool exit = e[1] == "SwitchToEditMode";
                            harmony.Patch(m, prefix: new HarmonyMethod(typeof(GcControl), nameof(OnSongEnd)), finalizer: exit ? new HarmonyMethod(typeof(GcControl), nameof(ExitFinalizer)) : null);
                            if (exit) exitFinalizer = true;
                            Main.Entry.Logger.Log("end hook: " + e[0] + "." + e[1]);
                        }
                    }
                    catch (Exception ex)
                    {
                        Main.Entry.Logger.Error("end hook failed " + e[0] + "." + e[1] + ": " + ex.Message);
                    }
                }

                foreach (var e in restarts)
                {
                    try
                    {
                        var type = AccessTools.TypeByName(e[0]);
                        if (type == null) continue;
                        foreach (var m in type.GetMethods(AccessTools.all))
                        {
                            if (m.Name != e[1] || m.IsAbstract || m.ContainsGenericParameters) continue;
                            harmony.Patch(m, prefix: new HarmonyMethod(typeof(GcControl), nameof(OnSongRestart)));
                            Main.Entry.Logger.Log("restart hook: " + e[0] + "." + e[1]);
                        }
                    }
                    catch (Exception ex)
                    {
                        Main.Entry.Logger.Error("restart hook failed " + e[0] + "." + e[1] + ": " + ex.Message);
                    }
                }

                // 재시작 시간 나누기: 장면 초기화(ResetScene)와 재생 준비(Play)
                if (scnGame != null)
                    foreach (var name in new[] { "ResetScene", "Play" })
                    {
                        var m = AccessTools.Method(scnGame, name);
                        if (m == null) continue;
                        try { harmony.Patch(m, prefix: new HarmonyMethod(typeof(GcControl), nameof(PartPre)), finalizer: new HarmonyMethod(typeof(GcControl), name == "Play" ? nameof(PlayPost) : nameof(ScenePost))); } catch { }
                    }

                // 나가는 길은 종류가 많아 다 잡기 어렵다. 씬이 바뀌는 것은 무조건 끝난 것이므로
                // 마지막 그물로 걸어 둔다.
                UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;

                patched = true;
                Main.Entry.Logger.Log("gc control installed");
            }
            catch (Exception ex)
            {
                Main.Entry.Logger.Error("gc control install failed: " + ex.Message);
            }
        }

        private static void OnSceneChanged(UnityEngine.SceneManagement.Scene from, UnityEngine.SceneManagement.Scene to)
        {
            endedByHook = true;
            Hitch.Report();
            EffectBudget.Reset();   // 이전 씬의 밀린 효과는 버린다
            PerfOverlay.MarkLoading(SettingsWindow.T("화면 전환", "Scene change"));
            Resume("씬 바뀜");
            LeakGuard.ScheduleCensus("장면 " + to.name, 2f);
        }

        // 모드를 다시 불러오기 전에 부른다. GC를 멈춘 채로 두고 내려가면 새 모드가 그 사실을 모른다.
        internal static void Shutdown()
        {
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnSceneChanged;
            resumeCountdown = -1f;
            Resume("모드 꺼짐");
            patched = false;   // 다시 켜면 다시 건다
        }

        // ── 맵 파일 읽는 동안 GC 멈추기 ──
        // Arche(42MB 맵 파일)에서 LevelData.LoadLevel 7.5초 동안 GC 가 16번 돌았다. 해석하며 생기는 임시 데이터로 힙이 자라서
        // 한 번에 0.1~0.3초씩 걸린다. 이 동안만 GC 를 멈추고 끝나면 원래대로 켠다(쌓인 것은 뒤이은 이미지 불러오기 끝의 정리나
        // 다음 GC 가 치운다). 멈춘 동안 힙이 파일 크기의 약 50배 늘어서(Arche 42MB -> +2.2GB, 7.56초 -> 6.30초), RAM 이 12GB 이상이고
        // 파일 x 60 이 RAM 의 4분의 1 이하일 때만 한다.
        internal static bool ParsePause = true;
        private static bool parsePaused;
        private static long parseT0, parseHeap0; private static int parseGc0;
        public static void ParsePrefix(object[] __args)
        {
            Resilience.Phase("맵 파일 읽는 중");
            if (!Enabled || !ParsePause || Paused || parsePaused) return;
            try
            {
                if (ramMB == 0) { try { ramMB = SystemInfo.systemMemorySize; } catch { ramMB = -1; } }
                if (ramMB < 12000) return;
                string path = __args != null && __args.Length > 0 ? __args[0] as string : null;
                long size = 0; try { if (path != null && System.IO.File.Exists(path)) size = new System.IO.FileInfo(path).Length; } catch { }
                // Arche(42MB)에서 힙이 2.2GB(파일의 약 52배) 늘었다. 파일 x 60 이 RAM 의 4분의 1 을 넘으면 하지 않는다
                if (size * 60 > (long)ramMB * 1048576 / 4) return;
                if (GarbageCollector.GCMode != GarbageCollector.Mode.Enabled) return;
                parseHeap0 = GC.GetTotalMemory(false); parseGc0 = GC.CollectionCount(0); parseT0 = System.Diagnostics.Stopwatch.GetTimestamp();
                GarbageCollector.GCMode = GarbageCollector.Mode.Disabled;
                parsePaused = true;
            }
            catch { }
        }
        public static Exception ParseFinalizer(Exception __exception)
        {
            if (!parsePaused) return __exception;
            parsePaused = false;
            try
            {
                GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
                double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - parseT0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                Main.Entry.Logger.Log(string.Format("[맵 파일 읽기] GC 멈춤 {0:F0}ms(그중 이벤트로 바꾸기 {4:F0}ms{5}), 힙 {1}MB -> {2}MB (그 사이 GC {3}번)", ms, parseHeap0 / 1048576, GC.GetTotalMemory(false) / 1048576, GC.CollectionCount(0) - parseGc0, DecodeFix.LastDecodeMs, ParallelDecode.Last.Length > 0 ? ": " + ParallelDecode.Last : ""));
            }
            catch { }
            return __exception;
        }

        public static void AfterLoad() { endedByHook = false; EffectBudget.Reset(); LeakGuard.ScheduleCensus("맵 불러온 뒤", 2f); PerfOverlay.MarkLoading(SettingsWindow.T("맵 불러오기", "Level load")); PerfOverlay.LevelActivity(); Resume("맵 로딩"); }

        public static void OnSongEnd(MethodBase __originalMethod)
        {
            endedByHook = true;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            InvisibleSkip.LastAllN = 0; InvisibleSkip.LastAllMs = 0;
            Hitch.Report();
            double rms = Ms(t0);
            if (rms >= 30) Main.Entry.Logger.Log(string.Format("[곡 끝] {0}: 곡 끝 요약 {1:F0}ms{2}", __originalMethod.Name, rms,
                InvisibleSkip.LastAllN > 0 ? string.Format(" (투명 장식 위치 반영 {0}개 {1:F0}ms)", InvisibleSkip.LastAllN, InvisibleSkip.LastAllMs) : ""));
            // 편집 화면으로 돌아가거나 메뉴로 나갈 때는 타일/장식을 다시 만드느라 멈춘다 (완주 연출은 끊김으로 본다)
            string n = __originalMethod.Name;
            if (n == "SwitchToEditMode" && exitFinalizer)
            {
                // 편집으로 나가기: 나가기 작업(타일·효과·장식 다시 만들기)이 만드는 쓰레기까지 한 번에 치우도록 끝(ExitFinalizer)에서 치운다.
                // 앞에서 치우면 그 뒤 쓰레기로 편집 화면에서 유니티 자동 GC 가 곧 돌았다(9만 타일 맵 72ms). 나가는 동안은 멈춘 채로 둔다.
                PerfOverlay.MarkLoading(SettingsWindow.T("편집 화면으로", "Back to editor"));
                exitPending = true;
                return;
            }
            if (n == "SwitchToEditMode" || n.Contains("Quit"))
            {
                // 화면이 바뀌며 어차피 멈추는 순간이라 바로 치운다
                PerfOverlay.MarkLoading(SettingsWindow.T("편집 화면으로", "Back to editor"));
                Resume(n);
                return;
            }
            if (n == "FailAction" || n == "Fail2Action") { if (Paused) holdAfterFail = true; return; }
            // 완주: 3초 뒤 치우면 큰 맵에서 0.36~0.47초 멈춰 "곡 끝나자마자 끊김" 으로 보였다(2.4.4 사용자 기록, 힙 1.1GB -> 0.5GB).
            // 다음 전환(편집으로 나가기·다시 하기·메뉴)까지 미룬다. 그 순간은 어차피 멈춘다. 에디터 밖에서는 곡이 끝나고 10초간 조용하면 치운다.
            if (n == "OnLandOnPortal") { if (Paused) holdAfterWin = true; return; }
            ScheduleResume(__originalMethod.Name);
        }

        private static bool exitFinalizer, exitPending;
        public static Exception ExitFinalizer(Exception __exception)
        {
            if (exitPending) { exitPending = false; Resume("SwitchToEditMode"); }
            return __exception;
        }

        internal static void ScheduleResume(string reason)
        {
            if (!Paused) return;
            if (resumeCountdown > 0f) return;
            resumeCountdown = EndDelaySeconds;
            resumeReason = reason;
        }

        // 재시작 시간: 재시작 함수가 불린 순간부터 곡이 다시 도는 첫 프레임까지 (Hitch.SongStarted 가 기록)
        internal static long RestartAt;
        internal static string RestartWhy = "";
        internal static int RestartFrame; internal static float RestartMaxMs; internal static long RestartGcMs;   // 그 사이 프레임 수, 가장 긴 프레임(PerfOverlay), 메모리 정리 시간
        // 재시작 시간 나누기 (Arche 한 판 뒤 재시작 8.8초 중 장면 초기화+재생 준비는 3.4초뿐이라 나머지를 찾으려고, 2026-09-26)
        internal static double RestartReportMs, RestartSceneMs, RestartPlayMs;
        private static double Ms(long from) { return (System.Diagnostics.Stopwatch.GetTimestamp() - from) * 1000.0 / System.Diagnostics.Stopwatch.Frequency; }
        public static void PartPre(out long __state) { WindowGhost.Tick(); __state = System.Diagnostics.Stopwatch.GetTimestamp(); }
        public static Exception ScenePost(long __state, Exception __exception) { WindowGhost.Tick(); if (RestartAt != 0) RestartSceneMs += Ms(__state); return __exception; }
        public static Exception PlayPost(long __state, Exception __exception) { WindowGhost.Tick(); if (RestartAt != 0) RestartPlayMs += Ms(__state); return __exception; }
        internal static string RestartParts(double total)
        {
            double other = total - RestartReportMs - RestartGcMs - RestartSceneMs - RestartPlayMs;
            return string.Format(" | 나눔: 곡 끝 요약 {0:F0}ms{1}, 메모리 정리 {2}ms, 장면 초기화 {3:F0}ms, 재생 준비 {4:F0}ms, 그 밖 {5:F0}ms",
                RestartReportMs, InvisibleSkip.RestartLazyN > 0 ? string.Format(" (투명 장식 위치 반영 {0}개 {1:F0}ms)", InvisibleSkip.RestartLazyN, InvisibleSkip.RestartLazyMs) : "",
                RestartGcMs, RestartSceneMs, RestartPlayMs, other);
        }

        public static void OnSongRestart(MethodBase __originalMethod)
        {
            RestartAt = System.Diagnostics.Stopwatch.GetTimestamp(); RestartWhy = __originalMethod.Name;
            RestartFrame = UnityEngine.Time.frameCount; RestartMaxMs = 0; RestartGcMs = 0;
            RestartReportMs = RestartSceneMs = RestartPlayMs = 0; InvisibleSkip.RestartLazyN = 0; InvisibleSkip.RestartLazyMs = 0;
            Hitch.Report();
            RestartReportMs = Ms(RestartAt);
            PerfOverlay.MarkLoading(SettingsWindow.T("곡 준비", "Level start"));
            PerfOverlay.BeginStartPhase();
            // 재시작은 어차피 화면이 바뀌는 순간이라 바로 치운다.
            // (재생 누르는 순간부터 GC 를 꺼 두는 것도 해 봤는데, 곡 시작 시간은 그대로였고 시작 직후
            //  "곡 아님" 으로 보이는 순간에 3초 뒤 정리가 예약되어 곡 초반에 끊겼다. 되돌렸다.)
            Resume(__originalMethod.Name);
            // 대신 자동 GC 만 막는다(Paused 로 두지 않으므로 위의 3초 뒤 정리 예약은 생기지 않는다). 아래 StartHold 설명.
            StartHold();
            EffectBudget.Reset();
            EffectBudget.Suspend(3f);
            endedByHook = false;
        }


        // 종료 함수가 불린 뒤에는 다시 멈추지 않는다.
        // 완주해도 에디터는 playMode를 켜 둔 채라서, 이것이 없으면 다음 프레임에 도로 멈춘다.
        private static bool endedByHook;

        // ── 곡 중 GC 멈추기: Disabled 가 아니라 Manual ──
        // 2026-09-27 자동 시험(작은 맵 HELLO_BPM_2021, 에디터 다시 하기 26번씩, 판마다 곡 2초 뒤부터 잼)으로 "첫 판 FPS 떨어짐"의 원인을 찾았다.
        //   GCMode.Disabled 로 멈춤(예전): 26판 중 9판이 판 내내 프레임마다 화면 대기 1.70ms (447 -> 245 FPS). 느린 판은 늘 GC 주기의 같은 자리(3판마다)
        //   곡 중 GC 미루기 끔: 26판 모두 빠름
        //   남은 점진적 GC 일을 모드가 끝낸 뒤 Disabled: 26판 모두 느림
        //   GCMode.Manual 로 멈춤: 26판 모두 빠름(447 FPS, 대기 0), 곡 중 GC 끊김 0, 쓰레기는 판마다 쌓이고 판 사이 전환 때만 정리됨
        // Disabled 는 GC 를 완전히 막고, Manual 은 자동 GC 만 막는다(GC.Collect / CollectIncremental 은 된다). Disabled 상태에서 유니티가
        // 프레임마다 화면 대기 자리에서 GC 일을 하려다 막히는 것으로 보인다. 큰 맵은 불러온 뒤 쓰레기가 많아 첫 판에 잘 걸렸다.
        // 그래서 곡 중에는 Manual 로 멈춘다. (시험용: 개발자용 gc-mode.txt 에 disabled / help 를 쓰면 예전 방식 / 끝내고 Disabled)
        private static bool pausePending; private static float pendingSince;
        internal static string PauseMode = "manual";
        internal static GarbageCollector.Mode PausedMode { get { return PauseMode == "manual" ? GarbageCollector.Mode.Manual : GarbageCollector.Mode.Disabled; } }
        internal static long PendingPauses, PendingFrames;
        private static bool IncrementalWorkLeft(bool help)
        {
            try
            {
                if (GarbageCollector.GCMode != GarbageCollector.Mode.Enabled || !GarbageCollector.isIncremental) return false;
                return GarbageCollector.CollectIncremental(help ? 1000000UL : 0UL);
            }
            catch { return false; }
        }

        // 끈 채로 곡을 시작했으면 true
        private static bool Pause()
        {
            if (Paused) return true;
            if (lastCleanMB < 0) { try { lastCleanMB = GC.GetTotalMemory(false) / 1048576; } catch { } }   // 아직 치운 적이 없으면 곡 시작 때 힙이 기준
            if (PauseMode == "help" && IncrementalWorkLeft(pausePending))
            {
                if (!pausePending) { pausePending = true; pendingSince = Time.realtimeSinceStartup; PendingPauses++; }
                PendingFrames++;
                if (Time.realtimeSinceStartup - pendingSince < 5f) return false;
                Main.Entry.Logger.Log("[GC] 곡 시작 때 돌던 GC 가 5초 안에 안 끝나 그냥 멈춤");
            }
            if (pausePending)
            {
                if (Edition.Dev) Main.Entry.Logger.Log(string.Format("[GC] 곡 시작 때 돌던 점진적 GC 를 끝낸 뒤 멈춤 ({0:F2}초)", Time.realtimeSinceStartup - pendingSince));
                pausePending = false;
            }
            try { GarbageCollector.GCMode = PausedMode; Paused = true; }
            catch (Exception ex) { Main.Entry.Logger.Error("GC 멈춤 실패: " + ex.Message); }
            return Paused;
        }

        // ── 재시작 때 정리 줄이기 ──
        // 2.1.0 사용자 로그(에디터에서 짧은 맵 재시도 반복): 재시작마다 정리에 214~480ms 를 썼는데 줄어든 건 15~100MB 뿐이었다.
        // Mono 의 정리 시간은 쌓인 쓰레기가 아니라 살아 있는 메모리(여기서 460MB)를 훑는 데 들어서, 조금만 쌓여도 매번 0.2초가 든다.
        // 그래서 곡 사이 짧은 전환(재시작, 재생, 편집 복귀, 완주, 실패 뒤, 곡 종료)에서는 지난 정리 뒤로 쌓인 양이 충분할 때만 치우고,
        // 아니면 GC 만 다시 켠다(다음 곡에서 또 멈추므로 쌓인 것은 몇 판 뒤 한 번에 치운다). 새 맵 불러오기·화면 전환은 어차피 멈추는
        // 순간이라 전처럼 치운다. 다른 모드(Quartz 의 부드러운 GC 등)가 이미 치웠으면 쌓인 양이 적어 자연히 건너뛴다.
        internal static int DebtMinMB = 192;
        private static long lastCleanMB = -1;
        internal static void NoteClean() { try { lastCleanMB = GC.GetTotalMemory(false) / 1048576; } catch { } }   // 다른 곳에서 한 정리 뒤 기준
        internal static long SkippedCollects;

        // ── 편집으로 나간 뒤 조금씩 치우기 ──
        // 에디터에서 한동안 플레이하고 편집으로 나가면 곡 중에 미뤄 둔 쓰레기를 여기서 한꺼번에 치웠다: 자동 시험 기록 684 / 748 / 1024 / 750ms
        // (Arche, 힙 1~2.5GB). 편집 화면은 곧바로 곡이 도는 곳이 아니라서, 유니티의 점진적 GC 로 프레임마다 조금씩(ExitSliceMs) 치운다.
        // 한 바퀴가 끝나고도 50MB 넘게 줄었으면 한 바퀴 더(최대 3바퀴, 한꺼번에 치우기의 "안 줄 때까지" 와 같은 뜻).
        // 끝나기 전에 다시 재생하면 거기서 멈춘다(곡 중에는 Manual 로 멈춤 - 도는 중에 멈춰도 느린 판이 생기지 않음을 2.3.4 에서 확인).
        // 2.4.5: 기본으로 끔. 큰 힙(타일 수만 개 맵, 1GB 안팎)에서는 한 조각이 3ms 가 아니라 40~46ms 씩 걸려(유니티 점진적 GC 의 나눌 수 없는 단계),
        // 나간 직후 편집 화면에서 움직일 때마다 끊겼다(사용자 기록 2026-09-28: 33프레임 최대 42.9ms, 99프레임 최대 45.6ms). 나가기는 이미 1~2초
        // 멈추는 순간이라 그 안에서 한 번에 치우는 편이 낫다(이 맵들에서 약 0.3초).
        internal static bool ExitSlices = false;
        internal static float ExitSliceMs = 3f;
        private static bool bgActive;
        private static int bgFrames, bgCycles;
        private static long bgBefore, bgCycleStart;
        private static double bgMs, bgMaxMs;
        private static float bgSince;
        internal static long BgRuns, BgFrames;
        private static void BgStep(bool playing)
        {
            long now = GC.GetTotalMemory(false) / 1048576;
            if (playing || Paused || !Enabled)
            {
                bgActive = false;
                Main.Entry.Logger.Log(string.Format("[GC] 편집 화면에서 조금씩 치우다 재생 시작으로 멈춤: {0}MB -> {1}MB, {2}프레임 {3:F0}ms", bgBefore, now, bgFrames, bgMs));
                return;
            }
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            bool more = true;
            try { GarbageCollector.GCMode = GarbageCollector.Mode.Enabled; more = GarbageCollector.CollectIncremental((ulong)(ExitSliceMs * 1000000f)); }
            catch { more = false; }
            double ms = Ms(t0);
            bgMs += ms; if (ms > bgMaxMs) bgMaxMs = ms; bgFrames++; BgFrames++;
            if (more && Time.realtimeSinceStartup - bgSince < 60f) return;
            bgCycles++;
            if (!more && bgCycles < 3 && now < bgCycleStart - 50) { bgCycleStart = now; return; }
            bgActive = false; BgRuns++;
            lastCleanMB = now;
            ModCost.Add(SettingsWindow.T("메모리 정리 (모드, 조금씩)", "Memory cleanup (mod, sliced)"), bgMs);
            Main.Entry.Logger.Log(string.Format("[GC] 편집 화면에서 조금씩 치움 끝: {0}MB -> {1}MB, {2}바퀴, {3}프레임, 합계 {4:F0}ms, 한 프레임 최대 {5:F1}ms, {6:F1}초 걸림",
                bgBefore, now, bgCycles, bgFrames, bgMs, bgMaxMs, Time.realtimeSinceStartup - bgSince));
        }
        private static bool QuickTransition(string reason)
        {
            switch (reason)
            {
                case "ResetCustomLevel": case "Restart": case "Play": case "SwitchToEditMode": case "OnLandOnPortal": case "10초간 조용함": return true;
            }
            return reason.StartsWith("곡 종료", StringComparison.Ordinal) || reason.StartsWith("Fail", StringComparison.Ordinal);
        }

        // ── 재생 시작 준비 중 자동 GC 막기 ──
        // 재생 준비(타일·효과 다시 만들기)가 쓰레기를 수백 MB 만들어, 편집으로 나갈 때 정리를 건너뛴 경우 유니티 자동 GC 가 곡 시작 직후
        // (곡 1초 무렵) 돌아 0.2초 멈췄다(2.4.4 사용자 기록, 93858타일 맵 "메모리 정리 203ms"). 재생·다시 하기를 누른 순간부터 곡이 돌기 시작할
        // 때까지 자동 GC 만 막는다(Manual: GC.Collect 는 된다). 곡이 시작되면 곡 중 멈춤(Pause)이 이어받고, 쌓인 것은 다음 전환에서 치운다.
        // 30초 안에 곡이 안 돌면(불러오기 실패 등) 원래대로 켠다.
        private static bool startHold;
        private static float startHoldAt;
        private static void StartHold()
        {
            if (!Enabled || Paused) return;
            try
            {
                if (GarbageCollector.GCMode != GarbageCollector.Mode.Enabled) return;
                GarbageCollector.GCMode = GarbageCollector.Mode.Manual;
                startHold = true; startHoldAt = Time.realtimeSinceStartup;
            }
            catch { }
        }
        private static void EndStartHold(string why)
        {
            if (!startHold) return;
            startHold = false;
            if (Paused) return;   // 곡 중 멈춤이 이어받음
            try { GarbageCollector.GCMode = GarbageCollector.Mode.Enabled; } catch { }
            if (why != null) Main.Entry.Logger.Log("[GC] 재생 준비 중 자동 GC 막기 끝: " + why);
        }

        internal static void Resume(string reason)
        {
            EndStartHold(null);
            if (!Paused) return;
            try
            {
                holdAfterFail = false; holdAfterWin = false;
                if (QuickTransition(reason))
                {
                    long heap = GC.GetTotalMemory(false) / 1048576;
                    // 힙이 기준보다 작으면 그 사이 누가 치운 것이다(맵 불러온 뒤 GC 등). 큰 맵의 기준이 남아 있으면 작은 맵에서 쌓인 양이
                    // -1380MB 처럼 음수로 나와 2.4GB 가 될 때까지 안 치웠다(2026-09-26). 지금 힙을 새 기준으로.
                    if (lastCleanMB < 0 || heap < lastCleanMB) lastCleanMB = heap;
                    long debt = heap - lastCleanMB;
                    long need = Math.Max(DebtMinMB, lastCleanMB * 35 / 100);
                    // 편집으로 나가기는 건너뛰어도 소용이 없다: 곧 곡이 다시 멈춰 주는 재시작과 달리 편집 화면에서는 GC 가 켜진 채라, 곡 중
                    // 쌓인 것 때문에 몇 프레임 뒤 유니티 자동 GC 가 바로 돈다(9만 타일 맵 69~79ms, "편집 화면에서 끊김"). 나가기 멈춤 안에서 치운다.
                    if (reason == "SwitchToEditMode") need = 32;
                    if (debt < need)
                    {
                        GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
                        Paused = false;
                        resumeCountdown = -1f;
                        SkippedCollects++;
                        Main.Entry.Logger.Log(string.Format("GC 재개 ({0}) 정리 생략: 지난 정리 뒤 쌓인 것 {1}MB < {2}MB (힙 {3}MB)", reason, debt, need, heap));
                        return;
                    }
                    // 편집으로 나가기: 한꺼번에 치우지 않고 편집 화면에서 프레임마다 조금씩 (아래 BgStep)
                    bool inc = false;
                    try { inc = GarbageCollector.isIncremental; } catch { }
                    if (reason == "SwitchToEditMode" && ExitSlices && inc)
                    {
                        GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
                        Paused = false;
                        resumeCountdown = -1f;
                        bgActive = true; bgFrames = 0; bgCycles = 0; bgBefore = bgCycleStart = heap; bgMs = 0; bgMaxMs = 0; bgSince = Time.realtimeSinceStartup;
                        Main.Entry.Logger.Log(string.Format("GC 재개 ({0}): 지난 정리 뒤 쌓인 것 {1}MB (힙 {2}MB) - 멈추지 않고 편집 화면에서 프레임마다 {3}ms 씩 치움", reason, debt, heap, ExitSliceMs));
                        return;
                    }
                }
                // 곡 밖에서 하는 정리는 곡 중 끊김이 아니다(곡 중에 날 정리를 미뤄 둔 것). 모니터에는 회색으로 따로 적는다.
                // (재시작·맵 로딩처럼 이미 불러오기로 적힌 순간이면 그 이름을 그대로 둔다)
                bool outside = false;
                try { outside = !IsPlaying(); } catch { }
                if (outside && !PerfOverlay.IsLoadingNow)
                    PerfOverlay.MarkLoading(SettingsWindow.T("곡 밖 메모리 정리", "Memory cleanup outside play"),
                        SettingsWindow.T("곡 중에 끊기지 않게 미뤄 둔 메모리 정리를 곡 밖에서 했습니다. 끊김으로 세지 않습니다",
                            "Memory cleanup postponed from gameplay ran outside play; not counted as a hitch"));
                GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
                Paused = false;
                resumeCountdown = -1f;
                long before = GC.GetTotalMemory(false) / 1048576;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                // 유니티의 점진적 GC는 한 번 불러서는 한 주기를 끝내지 않는다.
                // 실제로 6001MB가 704ms 걸려 5671MB로만 줄었고, 4초 뒤 다시 불렀을 때 비로소 757MB가 됐다.
                // 그러니 더 이상 줄지 않을 때까지 이어서 돌린다. 어차피 멈출 거면 한 번에 끝내는 편이 낫다.
                long prev = before;
                for (int i = 0; i < 5; i++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    long now = GC.GetTotalMemory(false) / 1048576;
                    if (now > prev - 50) break;   // 50MB도 안 줄면 끝난 것이다
                    prev = now;
                }

                sw.Stop();
                lastCleanMB = GC.GetTotalMemory(false) / 1048576;
                ModCost.Add(SettingsWindow.T("메모리 정리 (모드)", "Memory cleanup (mod)"), sw.Elapsed.TotalMilliseconds);
                ForcedCollects++;
                if (RestartAt != 0) RestartGcMs += sw.ElapsedMilliseconds;
                Main.Entry.Logger.Log(string.Format("GC 재개 및 정리 ({0}) {1}MB -> {2}MB, {3}ms",
                    reason, before, GC.GetTotalMemory(false) / 1048576, sw.ElapsedMilliseconds));
            }
            catch (Exception ex) { Main.Entry.Logger.Error("GC 재개 실패: " + ex.Message); }
        }

        internal static void Tick(float dt)
        {
            // 넘겨받는 dt 는 게임 시간이라 일시정지나 메뉴에서 0이 된다.
            // 그 시간으로 세면 "곡 끝나고 3초 뒤 정리", "10초간 조용하면 정리", 시간 제한이
            // 전부 얼어붙어서, 플레이 중에 나가면 GC가 멈춘 채로 영영 남는다.
            dt = Time.unscaledDeltaTime;

            if (!Enabled)
            {
                // 곡 중에 꺼지면(설정 창, 자동 보호) 한꺼번에 치우지 않고 GC 만 원래대로 켠다. 치우기는 게임의 GC 에 맡긴다(원래 게임과 같음).
                if (Paused && IsPlaying())
                {
                    try { GarbageCollector.GCMode = GarbageCollector.Mode.Enabled; } catch { }
                    Paused = false; resumeCountdown = -1f; holdAfterFail = false; holdAfterWin = false;
                    Main.Entry.Logger.Log("[GC] 곡 중에 꺼짐: 한꺼번에 치우지 않고 GC 만 원래대로 켬");
                    return;
                }
                Resume("기능 꺼짐");
                return;
            }

            bool playing = IsPlaying();
            if (Edition.Dev && GarbageMBps > 0 && playing) MakeGarbage(dt);
            long hq = System.Diagnostics.Stopwatch.GetTimestamp();
            Hitch.Tick(dt, playing);
            Main.TickCost[16] += System.Diagnostics.Stopwatch.GetTimestamp() - hq;

            if (bgActive) BgStep(playing);   // 편집으로 나간 뒤 조금씩 치우기 (재생이 시작되면 거기서 멈춘다)
            if (wantGap && playing && Paused && !limitPhase) GapCheck();   // 한계 가까이: 입력이 없는 틈을 매 프레임 본다
            if (playing && !Paused) { if (Pause()) { pausedFor = 0f; PeakHeapMB = 0; quietTimer = 0f; quietHeapMark = GC.GetTotalMemory(false) / 1048576; } }
            if (!playing) pausePending = false;
            else if (!playing && Paused) { Hitch.Report(); if (!holdAfterFail && !holdAfterWin) ScheduleResume("곡 종료 [" + LastScene + "]"); }
            if (startHold && (playing || Paused || Time.realtimeSinceStartup - startHoldAt > 30f)) EndStartHold(playing ? null : "30초 안에 곡이 시작되지 않음");

            // 곡이 끝났으면 연출이 끝나기를 기다렸다 치운다.
            if (resumeCountdown > 0f)
            {
                resumeCountdown -= dt;
                if (resumeCountdown <= 0f)
                {
                    resumeCountdown = -1f;
                    Resume(resumeReason);
                    return;
                }
            }

            if (!Paused) return;

            // 상태 감지가 어떤 이유로든 실패해도 메모리가 무한정 늘지 않도록 시간 제한을 둔다.
            pausedFor += dt;
            if (pausedFor > MaxPauseSeconds)
            {
                pausedFor = 0f;
                Resume("시간 제한 " + (int)MaxPauseSeconds + "초");
                return;
            }

            // 아래 판단(힙 최고치, 10초간 조용함, 실패 뒤 곡 감지, 힙 한계)은 느슨한 것이라 8프레임에 한 번만 한다.
            // 예전에는 매 프레임 힙 크기·음악 재생 여부·시스템 RAM 크기(엔진 호출)를 읽었다.
            slowDt += dt;
            if (++slowFrame < 8) return;
            dt = slowDt; slowDt = 0f; slowFrame = 0;
            long heapNow = GC.GetTotalMemory(false) / 1048576;
            if (heapNow > PeakHeapMB) PeakHeapMB = (int)heapNow;

            // 나가는 길을 다 잡지 못해도, 아무 일도 일어나지 않는 상태로 오래 있으면 끝난 것이다.
            // 예전에는 "곡이 도는 중에는 초당 몇 MB씩 쌓인다" 고 보고 힙이 10초간 3MB 도 안 늘면 끝났다고 했는데,
            // 모드가 할당을 많이 줄인 뒤로는 곡 초반 10초 동안 거의 안 늘어서, 곡이 도는 중에 정리(850ms)를 해 버렸다.
            // 지금은 곡 소리가 재생 중이면 끝난 것으로 보지 않는다.
            bool songRunning = false;
            try { var cd = scrConductor.instance; songRunning = cd != null && cd.song != null && cd.song.isPlaying; } catch { }
            // 안전장치: 실패 뒤 정리를 기다리는데 모르는 길로 곡이 다시 돌기 시작했다(곡으로 못 알아봄).
            // 여기서 한꺼번에 치우면 곡 중에 멈추므로, 정리 없이 GC 만 원래대로 켠다(게임 기본 동작과 같음).
            if (holdAfterFail && songRunning && !playing)
            {
                holdAfterFail = false;
                try { GarbageCollector.GCMode = GarbageCollector.Mode.Enabled; Paused = false; resumeCountdown = -1f; } catch { }
                Main.Entry.Logger.Log("[GC] 실패 뒤 곡이 다시 도는 것을 감지: 정리 없이 GC 를 켬 (힙 " + heapNow + "MB)");
                return;
            }
            // 곡 소리만 보면 곡 없는 맵이나 음악이 채보보다 먼저 끝나는 맵에서, 타일을 치는 중인데도 "조용함" 으로 보고
            // 곡 도중에 정리(0.8초 멈춤)할 수 있었다. 타일이 넘어가고 있으면(현재 타일 번호가 바뀜) 곡이 도는 것으로 본다.
            int seq = -1;
            try { var c = scrController.instance; var f = c != null ? c.currFloor : null; if (f != null) seq = f.seqID; } catch { }
            bool advancing = seq != quietSeq; quietSeq = seq;
            // 에디터에서 완주한 뒤에는 편집으로 나가는 순간(어차피 멈춤)까지 기다린다 (그 사이 힙은 아래 한계가 지킨다)
            bool edWin = false;
            if (holdAfterWin) { try { edWin = ADOBase.isLevelEditor; } catch { } }
            if (songRunning || (playing && advancing) || edWin) { quietTimer = 0f; quietHeapMark = heapNow; }
            else quietTimer += dt;
            if (quietTimer >= 10f)
            {
                if (heapNow - quietHeapMark < 3)
                {
                    Resume("10초간 조용함");
                    return;
                }
                quietHeapMark = heapNow;
                quietTimer = 0f;
            }

            // RAM 이 적은 컴퓨터에서는 한계를 낮춘다 (RAM 의 40%, 최소 1.5GB)
            int limit = HardLimitMB;
            if (ramMB == 0) { try { ramMB = SystemInfo.systemMemorySize; } catch { ramMB = -1; } }   // 바뀌지 않으므로 한 번만
            if (ramMB > 0) limit = Mathf.Min(limit, Mathf.Max(1500, ramMB * 2 / 5));
            if (!playing) { limitSlicing = false; limitPhase = false; wantGap = false; restUntil = -1f; restCheck = false; raisedLimit = 0; }
            // ── 곡 중 힙 한계 ──
            // 2026-09-27 저사양 사용자 로그(Ryzen 7 5825U, RAM 16GB, 25분 넘는 곡): 곡 1457초에 힙 6GB 한계 -> 조금씩 치우기로 바뀐 뒤
            // 곡이 끝날 때까지 계속 치워서 약 8초마다 80ms 씩 멈췄다. 2.3.2 에서 "힙이 한계의 절반 아래이고 한 바퀴가 끝났으면 멈춤" 으로 했지만
            // 2026-09-27 1시간 맵(230BPM 100ksub, 곡 31분에 힙 6GB)에서도 멈추지 않았다: 0.3초마다 74ms 씩 멈추기를 나갈 때까지 47초(116번).
            // 개발자용 재현(곡 중 쓰레기 초당 40MB, gc-garbage.txt)으로 찾은 까닭: 유니티의 점진적 GC 는 한 번 시작시키면 그 바퀴를 알아서
            // 끝까지 돌린다(곡 중 Manual 로 돌려놓아도). 그래서 CollectIncremental 은 늘 "남은 일 있음" 을 돌려주고, 첫 시작 뒤 7초 안에 힙이
            // 6006 -> 807MB 로 줄었는데도 모드는 몇 프레임마다 새 바퀴를 또 시작시켰다. 바퀴마다 끝에서 게임을 한 번 세운다(힙이 클수록 길다).
            // 이제: 한계에 닿으면 한 번만 시작시키고 힙이 줄었는지만 본다. 줄면 끝, LimitRestSeconds 동안 다시 시작하지 않는다.
            // 안 줄면 KickWaitSeconds 마다 다시 시작(최대 3번). 줄고도 한계 가까이면(살아 있는 메모리가 한계에 가까움) 이 곡 동안 한계를 올린다
            // (최대 1.4배). 원래 한계의 1.5배를 넘으면 메모리가 우선이라 한 번 멈춰 치운다(안전장치).
            if (SliceStyle == "old") { OldLimitSlicing(heapNow, limit, playing); return; }
            float nowT = Time.realtimeSinceStartup;
            if (limitPhase)
            {
                if (heapNow <= kickHeap * 6 / 10)
                {
                    limitPhase = false;
                    restUntil = nowT + LimitRestSeconds; restCheck = true;
                    LimitCleans++;
                    Main.Entry.Logger.Log(string.Format("[GC] 곡 중 힙 한계 치우기 끝: {0} -> {1}MB ({2:F1}초, 시작 {3}번). {4:F0}초 동안 다시 시작 안 함",
                        kickHeap, heapNow, nowT - kickStart, kicks, LimitRestSeconds));
                }
                else if (nowT - kickAt >= KickWaitSeconds)
                {
                    if (kicks < 3) Kick(heapNow);
                    else
                    {
                        limitPhase = false;
                        restUntil = nowT + LimitRestSeconds; restCheck = true;
                        Main.Entry.Logger.Log(string.Format("[GC] 곡 중 힙 한계 치우기: {0}번 시작했는데 힙이 {1} -> {2}MB 로 덜 줄어 그만둠", kicks, kickHeap, heapNow));
                    }
                }
            }
            if (restCheck && nowT >= restUntil)
            {
                restCheck = false;
                int cur = Math.Max(limit, raisedLimit);
                if (heapNow > cur * 9L / 10)
                {
                    raisedLimit = (int)Math.Min(limit * 14L / 10, Math.Max(cur, heapNow * 5L / 4));
                    Main.Entry.Logger.Log("[GC] 치운 뒤에도 힙 " + heapNow + "MB (한계 " + cur + "MB 가까이): 이 곡 동안 한계를 " + raisedLimit + "MB 로 올림");
                }
            }
            int baseLimit = limit;
            if (raisedLimit > limit) limit = raisedLimit;
            bool over = heapNow >= baseLimit * 3L / 2;
            // 한계의 GapZone(80%) 부터는 입력이 없는 틈(다음 타일까지 "예상 멈춤 + GapMargin" 이상, 또는 일시정지)을 기다렸다가 그 순간 한 번에 치운다(GapCheck).
            // 2026-09-28 측정(6GB 힙): 한 번에 크게 조각 치우기 180~200ms 가 그 호출 안에서 끝남(GC.Collect 265ms). 작게 시작만 시키면 95ms 지만 0.7초 안 어느 순간에 온다. 틈이 끝까지 없으면 한계에서 작게 시작만 시킨다.
            wantGap = !limitPhase && nowT >= restUntil && heapNow >= (long)(limit * GapZone) && heapNow <= limit;
            if (!limitPhase && heapNow > limit && (nowT >= restUntil || over))
            {
                bool canSlice = false;
                try { canSlice = GarbageCollector.isIncremental; } catch { }
                if (playing && canSlice && !over)
                {
                    // 곡 중에 한꺼번에 치우면 0.8초 넘게 멈춰 그 자리에서 죽을 수 있다: 점진적 GC 를 한 번 시작시킨다
                    limitPhase = true; kicks = 0; kickHeap = heapNow; kickStart = nowT;
                    Main.Entry.Logger.Log("[GC] 곡 중 힙 한계 " + heapNow + "MB (한계 " + limit + "MB): 한꺼번에 치우지 않고 점진적 GC 를 한 번 시작시킴 (기다리는 동안 가장 긴 틈 " + maxGapSeen.ToString("F2") + "초)"); maxGapSeen = 0;
                    Kick(heapNow);
                    return;
                }
            }
            if (over && (limitPhase || heapNow > limit))
            {
                // 안전장치. 여기까지 오면 어쩔 수 없이 한 번 멈춘다.
                limitPhase = false;
                Resume("힙 한계 " + heapNow + "MB");
                Pause();
                return;
            }
        }

        internal static float GapZone = 0.8f;          // 한계의 이만큼부터 틈을 기다린다
        internal static double GapMargin = 0.15;       // 멈춤이 끝난 뒤 다음 타일까지 남길 여유 (일찍 누르는 판정 범위 포함)
        internal static ulong BigBudgetNs = 400000000UL;   // 틈에서 한 번에 치울 때 최대 400ms (6GB 힙에서 실제 180~200ms 에 끝남)
        private static double lastBigMs;
        internal static long BigUnfinished;
        // 예상 멈춤: GB 당 50ms(6GB 에서 300ms, 실측 180~265ms), 지난번 실측의 1.2배 중 큰 것
        private static double PredictMs(long heapMB) { return Math.Max(heapMB * 50.0 / 1024.0, lastBigMs * 1.2); }
        internal static long GapKicks, PauseKicks;
        private static bool wantGap;
        private static double maxGapSeen;   // 틈을 기다리는 동안 본 가장 긴 틈 (기록용)
        private static void GapCheck()
        {
            try
            {
                var ctl = scrController.instance;
                var cd = scrConductor.instance;
                if (ctl == null || cd == null) return;
                string why = null;
                if (ctl.paused) why = "일시정지";
                else
                {
                    var f = ctl.currFloor;
                    var next = f != null ? f.nextfloor : null;
                    if (next == null || f.holdLength > 0) return;   // 누르고 있는 타일 중이면 기다린다
                    float pitch = cd.song != null ? Math.Max(0.01f, cd.song.pitch) : 1f;
                    double gap = (next.entryTime - cd.songposition_minusi) / Math.Max(1f, pitch);
                    if (gap > maxGapSeen) maxGapSeen = gap;
                    double need = PredictMs(GC.GetTotalMemory(false) / 1048576) / 1000.0 + GapMargin;
                    if (gap >= need) why = string.Format("다음 타일까지 {0:F2}초, 필요 {1:F2}초", gap, need);
                }
                if (why == null) return;
                long heap = GC.GetTotalMemory(false) / 1048576;
                wantGap = false;
                limitPhase = true; kicks = 0; kickHeap = heap; kickStart = Time.realtimeSinceStartup;
                if (ctl.paused) PauseKicks++; else GapKicks++;
                Main.Entry.Logger.Log("[GC] 곡 중 힙 " + heap + "MB (한계 가까이): 입력이 없는 틈(" + why + ")에 점진적 GC 를 한 번 시작시킴");
                Kick(heap, true);
            }
            catch { wantGap = false; }
        }

        internal static float KickWaitSeconds = 15f;   // 시작시킨 뒤 힙이 줄기를 기다리는 시간
        internal static long LimitCleans;
        private static bool limitPhase;
        private static int kicks;
        private static long kickHeap;
        private static float kickAt, kickStart;
        private static void Kick(long heapNow, bool big = false)
        {
            try
            {
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
                bool more = GarbageCollector.CollectIncremental(big ? BigBudgetNs : (ulong)(SliceMs * 1000000f));
                double ms = Ms(t0);
                if (big) { lastBigMs = ms; if (more) BigUnfinished++; }
                GarbageCollector.GCMode = PausedMode;
                kicks++; kickAt = Time.realtimeSinceStartup; IncrementalSlices++;
                if (Edition.Dev || big) Main.Entry.Logger.Log(string.Format("[GC] 점진적 GC 시작 {0}번째{4}: {1:F1}ms, 남은 일 {2}, 힙 {3}MB", kicks, ms, more, heapNow, big ? " (틈에서 한 번에)" : ""));
            }
            catch { }
        }

        // (시험, gc-slice.txt = old) 예전 방식: 몇 프레임마다 조각 치우기, "한계의 절반 아래이고 바퀴 끝" 이면 멈춤 - 무한 반복 재현용
        private static void OldLimitSlicing(long heapNow, int limit, bool playing)
        {
            if (limitSlicing && playing && heapNow < limit / 2 && !lastSliceMore) { limitSlicing = false; Main.Entry.Logger.Log("[GC] (시험, 예전 방식) 힙 " + heapNow + "MB: 다시 곡 중 정리 멈춤"); }
            if (heapNow > limit && !limitSlicing)
            {
                bool canSlice = false;
                try { canSlice = GarbageCollector.isIncremental; } catch { }
                if (playing && canSlice && heapNow < limit * 3L / 2) { limitSlicing = true; Main.Entry.Logger.Log("[GC] 곡 중 힙 한계 " + heapNow + "MB (한계 " + limit + "MB): 한꺼번에 치우지 않고 조금씩 치움"); }
                else { Resume("힙 한계 " + heapNow + "MB"); Pause(); return; }
            }
            if (!limitSlicing) return;
            if (heapNow > IncrementalStartMB && ++frameCounter >= SliceEveryFrames)
            {
                frameCounter = 0;
                try
                {
                    long st0 = System.Diagnostics.Stopwatch.GetTimestamp(); int gc0 = GC.CollectionCount(0);
                    GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
                    lastSliceMore = GarbageCollector.CollectIncremental((ulong)(SliceMs * 1000000f));
                    GarbageCollector.GCMode = PausedMode;
                    IncrementalSlices++;
                    if (Edition.Dev) SliceStat(Ms(st0), !lastSliceMore, GC.CollectionCount(0) - gc0, heapNow);
                }
                catch { }
            }
        }

        // ── 게임 상태 읽기 ──────────────────────────────────────────────
        // scrController.currentState 는 None / Start / Countdown / Checkpoint / PlayerControl / Fail / Fail2 / Won.
        // 그런데 이 버전에서는 재생 중에도 None으로 남아 있다(패널에서 확인). 쓰지 않는 필드로 보인다.
        // 그래서 끝난 상태로 바뀔 때만 종료 신호로 쓰고, 판정 자체는 gameworld + 에디터 재생 여부로 한다.
        // 종료는 상태값에 기대지 않고 아래 Install()에서 실제 종료 함수들을 직접 가로채 처리한다.
        private static readonly string[] StopStates = { "Fail", "Fail2", "Won" };

        private static PropertyInfo controllerProp, pausedProp, playModeProp, pausedInPlayProp;
        private static FieldInfo gameworldField, editorInstanceField, stateField, floorField;
        private static bool reflectionReady;
        private static string loggedScene = "";

        private static void PrepareReflection()
        {
            if (reflectionReady) return;
            reflectionReady = true;
            try
            {
                var adoBase = AccessTools.TypeByName("ADOBase");
                controllerProp = AccessTools.Property(adoBase, "controller");

                var ctrl = AccessTools.TypeByName("scrController");
                gameworldField = AccessTools.Field(ctrl, "gameworld");
                pausedProp = AccessTools.Property(ctrl, "paused");
                stateField = AccessTools.Field(ctrl, "currentState");
                floorField = AccessTools.Field(ctrl, "currentFloorID");

                var editor = AccessTools.TypeByName("scnEditor");
                if (editor != null)
                {
                    editorInstanceField = AccessTools.Field(editor, "instance");
                    playModeProp = AccessTools.Property(editor, "playMode");
                    pausedInPlayProp = AccessTools.Property(editor, "pausedInPlayMode");
                }
                Main.Entry.Logger.Log("gc reflection: state=" + (stateField != null) +
                    " gameworld=" + (gameworldField != null) + " playMode=" + (playModeProp != null) +
                    " floor=" + (floorField != null));
            }
            catch (Exception ex) { Main.Entry.Logger.Error("gc reflection 실패: " + ex.Message); }
        }

        internal static int CurrentFloor()
        {
            try
            {
                if (controllerProp == null || floorField == null) return -1;
                var ctrl = controllerProp.GetValue(null);
                if (ctrl == null) return -1;
                return Convert.ToInt32(floorField.GetValue(ctrl));
            }
            catch { return -1; }
        }

        // ── 빠른 판단 ──
        // 예전에는 매 프레임 리플렉션 7번(값 상자 만들기 포함) + 상태 이름 ToString + LastScene 문자열 이어 붙이기를 했다.
        // 논이펙 맵 측정에서 UMM 의 UI.Update(모든 모드의 OnUpdate 를 부름)가 프레임당 0.08ms 였다. 같은 값을 델리게이트로 읽고,
        // 상태 문자열은 값이 바뀔 때만 만든다. 판단 결과와 로그는 예전과 같다.
        private static bool fastTried, fastOk;
        private static Func<scrController> ctrlGet;
        private static Func<scrController, bool> pausedGet;
        private static Func<scnEditor, bool> playModeGet, pausedInPlayGet;
        private static AccessTools.FieldRef<scrController, bool> gameworldRef;
        private static AccessTools.FieldRef<scrController, States> stateRef;
        private static AccessTools.FieldRef<scnEditor> editorInstRef;
        private static int lastKey = int.MinValue;

        private static bool PrepareFast()
        {
            if (fastTried) return fastOk;
            fastTried = true;
            try
            {
                ctrlGet = AccessTools.MethodDelegate<Func<scrController>>(AccessTools.PropertyGetter(typeof(ADOBase), "controller"));
                pausedGet = AccessTools.MethodDelegate<Func<scrController, bool>>(AccessTools.PropertyGetter(typeof(scrController), "paused"));
                playModeGet = AccessTools.MethodDelegate<Func<scnEditor, bool>>(AccessTools.PropertyGetter(typeof(scnEditor), "playMode"));
                var pip = AccessTools.PropertyGetter(typeof(scnEditor), "pausedInPlayMode");
                pausedInPlayGet = pip != null ? AccessTools.MethodDelegate<Func<scnEditor, bool>>(pip) : null;
                gameworldRef = AccessTools.FieldRefAccess<scrController, bool>("gameworld");
                stateRef = AccessTools.FieldRefAccess<scrController, States>("currentState");
                fastOk = ctrlGet != null && pausedGet != null && playModeGet != null && gameworldRef != null && stateRef != null
                         && AccessTools.Field(typeof(scnEditor), "instance") != null;
            }
            catch (Exception ex) { fastOk = false; Main.Entry.Logger.Log("[GC] 빠른 판단 준비 실패, 예전 방식 사용: " + ex.Message); }
            return fastOk;
        }

        private static bool IsPlayingFast()
        {
            bool gameworld = false, paused = false, playMode = true, hasEditor = false;
            int st = -1;
            var ctrl = ctrlGet();
            if (ctrl != null)
            {
                gameworld = gameworldRef(ctrl);
                paused = pausedGet(ctrl);
                st = (int)stateRef(ctrl);
            }
            var editor = scnEditor.instance;
            if (editor != null)
            {
                hasEditor = true;
                playMode = playModeGet(editor);
                if (pausedInPlayGet != null && pausedInPlayGet(editor)) paused = true;
            }
            TrackStart(gameworld && !paused && playMode && !endedByHook, st);
            bool stateOk = !IsStopState(st) || (wasCandidate && st == stateAtStart);
            bool playing = gameworld && !paused && playMode && stateOk && !endedByHook;

            // 상태 이름 문자열은 값이 바뀔 때만 만든다 (예전과 같은 모양)
            int key = (st + 1) | (endedByHook ? 1 << 8 : 0) | (gameworld ? 1 << 9 : 0) | (hasEditor ? 1 << 10 : 0) | (playMode ? 1 << 11 : 0) | (paused ? 1 << 12 : 0);
            if (key != lastKey)
            {
                lastKey = key;
                string stateName = st < 0 ? "?" : ((States)st).ToString();
                LastScene = stateName
                          + (endedByHook ? " 종료됨" : "")
                          + (gameworld ? "" : " world:X")
                          + (hasEditor ? (playMode ? " 에디터재생" : " 편집중") : "")
                          + (paused ? " 일시정지" : "");
                if (LastScene != loggedScene)
                {
                    loggedScene = LastScene;
                    Main.Entry.Logger.Log("[상태] " + LastScene + (playing ? "  -> 플레이" : "  -> 정지"));
                }
            }
            return playing;
        }

        // 게임은 에디터에서 다시 Play 할 때 currentState 를 되돌리지 않는다. 한 번 죽으면 Fail 이 그대로 남아, 그 뒤 Play 가 모두
        // "끝난 곡" 으로 보여 곡 중 GC 미루기가 꺼져 있었다(2026-09-27, 모니터에 메모리 정리 "대기" 가 계속 뜨고 곡 중 GC 50~67ms).
        // 그래서 재생이 시작될 때(일시정지 풀림 포함) 이미 들어 있던 끝 상태는 무시하고, 재생 중에 그 값으로 바뀔 때만 끝으로 본다.
        // 죽음·클리어는 원래도 종료 함수를 직접 가로채서(endedByHook) 잡는다.
        private static bool wasCandidate; private static int stateAtStart = int.MinValue;
        private static bool IsStopState(int st) { return st == (int)States.Fail || st == (int)States.Fail2 || st == (int)States.Won; }
        private static void TrackStart(bool candidate, int st)
        {
            if (candidate && !wasCandidate) stateAtStart = st;   // 재생이 막 시작됨: 지금 상태를 기준으로
            wasCandidate = candidate;
        }

        private static bool IsPlaying()
        {
            try { if (PrepareFast()) return IsPlayingFast(); } catch { }
            return IsPlayingSlow();
        }

        private static bool IsPlayingSlow()
        {
            PrepareReflection();
            try
            {
                bool gameworld = false, paused = false, playMode = true, hasEditor = false;
                string stateName = "?";

                var ctrl = controllerProp != null ? controllerProp.GetValue(null) : null;
                if (ctrl != null)
                {
                    if (gameworldField != null) gameworld = Convert.ToBoolean(gameworldField.GetValue(ctrl));
                    if (pausedProp != null) paused = Convert.ToBoolean(pausedProp.GetValue(ctrl));
                    if (stateField != null)
                    {
                        var v = stateField.GetValue(ctrl);
                        if (v != null) stateName = v.ToString();
                    }
                }

                if (editorInstanceField != null && playModeProp != null)
                {
                    var editor = editorInstanceField.GetValue(null);
                    if (editor != null)
                    {
                        hasEditor = true;
                        playMode = Convert.ToBoolean(playModeProp.GetValue(editor));
                        if (pausedInPlayProp != null && Convert.ToBoolean(pausedInPlayProp.GetValue(editor))) paused = true;
                    }
                }

                int stNum = -1; try { if (ctrl != null && stateField != null) stNum = Convert.ToInt32(stateField.GetValue(ctrl)); } catch { }
                TrackStart(gameworld && !paused && playMode && !endedByHook, stNum);
                bool stateOk = Array.IndexOf(StopStates, stateName) < 0 || (wasCandidate && stNum == stateAtStart);
                bool playing = gameworld && !paused && playMode && stateOk && !endedByHook;

                LastScene = stateName
                          + (endedByHook ? " 종료됨" : "")
                          + (gameworld ? "" : " world:X")
                          + (hasEditor ? (playMode ? " 에디터재생" : " 편집중") : "")
                          + (paused ? " 일시정지" : "");

                // 상태 이름이 바뀔 때만 남긴다. 감지가 또 어긋나면 이 줄만 보면 된다.
                if (LastScene != loggedScene)
                {
                    loggedScene = LastScene;
                    Main.Entry.Logger.Log("[상태] " + LastScene + (playing ? "  -> 플레이" : "  -> 정지"));
                }
                return playing;
            }
            catch (Exception ex)
            {
                LastScene = "판단 실패: " + ex.Message;
                return false;   // 판단이 안 되면 안전하게 GC를 켠 상태로 둔다
            }
        }

        internal static string Status
        {
            get
            {
                long heap = GC.GetTotalMemory(false) / 1048576;
                return (Paused ? "GC 멈춤" : "GC 정상") + " [" + LastScene + "]"
                     + ", 힙 " + heap + "MB (곡 중 최대 " + PeakHeapMB + "MB)"
                     + ", 할당 " + Hitch.AllocMBPerSec.ToString("F0") + "MB/s"
                     + ", 전체정리 " + ForcedCollects + "회"
                     + (NoCollectDuringSong ? "" : ", 조각정리 " + IncrementalSlices + "회");
            }
        }
    }
}
