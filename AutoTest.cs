using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // (개발자용) 자동 시험: 모드 폴더의 autotest.txt 를 한 줄씩 해서, 사람이 직접 누르지 않아도 맵 열기·재생·재기·끄기를 한다.
    // Claude 가 게임을 직접 켜서(steam://rungameid/977950) 여러 번 되풀이해 재려고 만들었다(첫 판 FPS, Play 시간 등).
    // 결과는 평소 로그([곡], [재시작 시간], [화면 대기], [로딩 검증] ...)에 그대로 남고, 단계마다 "[자동 시험]" 줄을 적는다.
    //
    // 명령 (한 줄에 하나, # 은 주석):
    //   open <맵 파일 경로>   에디터로 가서 맵을 연다 (다 열릴 때까지 기다림)
    //   auto on|off          자동 플레이
    //   play                 에디터 재생 (scnEditor.Play)
    //   stop                 편집으로 돌아가기 (scnEditor.SwitchToEditMode)
    //   wait <초>            기다리기
    //   log <글>             로그에 표시 남기기
    //   quit                 저장하지 않고 게임 끄기
    // 파일은 켤 때 읽고 autotest.done 으로 이름을 바꾼다(다음 실행에서 되풀이하지 않게). 어디서든 3분 넘게 멈추면 그만두고 끈다.
    internal static class AutoTest
    {
        private static List<string> steps;
        private static int idx;
        private static float stepStart, waitSec;
        private static bool started, prevAuto, autoChanged, desiredAuto;
        private static readonly System.Reflection.FieldInfo loadingField = AccessTools.Field(typeof(scnEditor), "isLoading");

        internal static bool Active { get { return steps != null; } }

        internal static void Init(string modPath)
        {
            if (!Edition.AutoTest) return;
            try
            {
                string f = Path.Combine(modPath, "autotest.txt");
                if (!File.Exists(f)) return;
                steps = new List<string>();
                foreach (var raw in File.ReadAllLines(f)) { var l = raw.Trim(); if (l.Length > 0 && !l.StartsWith("#")) steps.Add(l); }
                string done = Path.Combine(modPath, "autotest.done");
                if (File.Exists(done)) File.Delete(done);
                File.Move(f, done);
                Main.Entry.Logger.Log("[자동 시험] 시작: " + steps.Count + "단계 - " + string.Join(" / ", steps.ToArray()));
            }
            catch (Exception ex) { steps = null; Main.Entry.Logger.Log("[자동 시험] 읽기 실패: " + ex.Message); }
        }

        private static void Log(string s) { Main.Entry.Logger.Log("[자동 시험] " + s); }

        // 매 프레임 (OnUpdate)
        internal static void Tick()
        {
            if (steps == null) return;
            float now = Time.realtimeSinceStartup;
            if (!started) { started = true; stepStart = now; }
            if (!ummClosed || (idx < steps.Count && steps[idx].StartsWith("play", StringComparison.OrdinalIgnoreCase))) CloseUmm();
            if (idx >= steps.Count) { Finish("끝"); return; }
            if (now - stepStart > stepTimeout) { Finish("시간 초과 (" + steps[idx] + ")"); return; }
            string line = steps[idx];
            int sp = line.IndexOf(' ');
            string cmd = (sp < 0 ? line : line.Substring(0, sp)).ToLowerInvariant();
            string arg = sp < 0 ? "" : line.Substring(sp + 1).Trim();
            try
            {
                if (Step(cmd, arg, now)) { idx++; stepStart = now; waitSec = 0f; }
            }
            catch (Exception ex) { Log("단계 실패 '" + line + "': " + (ex.InnerException ?? ex).Message); Finish("오류로 그만둠"); }
        }

        // UMM 창이 열려 있으면 그리는 비용 때문에 FPS 가 조금 낮게 나온다(사용자 관찰, 2026-09-27). 시작할 때와 재생마다 닫는다.
        private static bool ummClosed;
        private static void CloseUmm()
        {
            try
            {
                var ui = UnityModManagerNet.UnityModManager.UI.Instance;
                if (ui == null) return;
                if (ui.Opened) { ui.ToggleWindow(false); Log("UMM 창 닫음"); }
                ummClosed = true;
            }
            catch { ummClosed = true; }
        }

        // 판마다 재기: play/retry 뒤 2초부터 다음 명령 전까지 프레임 시간과 화면 대기(PresentWatch.Frame 에서 받음)
        private static float runStart = -1f; private static string runKind = ""; private static int runNo;
        private static double accMs, accWait; private static int accN;
        internal static void Sample(bool playing, float ms, float wait)
        {
            if (runStart < 0f || ms > 500f || Time.realtimeSinceStartup - runStart < 2f) return;
            accMs += ms; accWait += wait; accN++;
        }
        private static void EndRun()
        {
            if (runStart >= 0f && accN > 30)
                Log(string.Format("판 #{0} ({1}): 평균 {2:F0} FPS, 화면 대기 {3:F2}ms, 프레임 {4}개 -> {5}", runNo, runKind, 1000.0 / (accMs / accN), accWait / accN, accN, accWait / accN >= 0.5 ? "느림" : "빠름"));
            runStart = -1f; accMs = accWait = 0; accN = 0;
        }
        private static void BeginRun(string kind) { EndRun(); runNo++; runKind = kind; runStart = Time.realtimeSinceStartup; }

        private static float stepTimeout = 180f;   // 단계마다 최대 (timeout 명령으로 바꿈, 큰 맵 열기용)
        private static float openStartedAt;
        private static int openPhase;

        // 끝났으면 true
        private static bool Step(string cmd, string arg, float now)
        {
            var ed = ADOBase.isLevelEditor ? scnEditor.instance : null;
            switch (cmd)
            {
                case "wait":
                    if (waitSec == 0f) { float.TryParse(arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out waitSec); if (waitSec <= 0f) waitSec = 0.01f; }
                    return now - stepStart >= waitSec;
                case "log":
                    Log(arg); return true;
                case "allocscan":
                    // 곡 중 누가 메모리를 잡는지 15초 동안 모은다 (F9 와 같음, 개발자용)
                    if (!Edition.Dev) { Log("할당 추적은 개발자용만"); return true; }
                    AllocScan.Toggle(); Log("할당 추적 시작");
                    return true;
                case "timeout":
                    float.TryParse(arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out stepTimeout);
                    if (stepTimeout < 10f) stepTimeout = 180f;
                    Log("단계 최대 시간 " + stepTimeout + "초");
                    return true;
                case "alttab":
                    {
                        // 옆 스레드가 <지연>초 뒤 Alt+Tab 으로 다른 창으로 갔다가 <머묾>초 뒤 Alt+Tab 으로 돌아온다(사람이 창을 오가는 것 흉내).
                        // 바로 다음 play 의 멈춤 동안 하려고. 글자 입력은 없다.
                        var parts = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        float delay = 1f, hold = 2f;
                        if (parts.Length > 0) float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out delay);
                        if (parts.Length > 1) float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out hold);
                        new System.Threading.Thread(() =>
                        {
                            System.Threading.Thread.Sleep((int)(delay * 1000));
                            AltTab(); Main.Entry.Logger.Log("[자동 시험] Alt+Tab 나감 (옆 스레드)");
                            System.Threading.Thread.Sleep((int)(hold * 1000));
                            AltTab(); Main.Entry.Logger.Log("[자동 시험] Alt+Tab 돌아옴 (옆 스레드)");
                        }) { IsBackground = true, Name = "StutterFix.AutoTestAltTab" }.Start();
                        Log(string.Format("Alt+Tab 예약: {0}초 뒤 나갔다가 {1}초 뒤 돌아옴", delay, hold));
                        return true;
                    }
                case "retry":
                    {
                        // 에디터에서 죽은 뒤 키를 눌렀을 때와 같은 다시 하기 (scrController.ResetCustomLevel 코루틴)
                        var ctrl = ADOBase.controller;
                        if (ctrl == null) throw new Exception("scrController 없음");
                        if (ed != null && !ed.playMode) throw new Exception("재생 중이 아님");
                        if (autoChanged) RDC.auto = desiredAuto;
                        ctrl.StartCoroutine(ctrl.ResetCustomLevel());
                        Log("다시 하기");
                        BeginRun("다시 하기");
                        return true;
                    }
                case "mark":
                    {
                        // 같은 판 안에서 재기 구간을 끊는다 (킥 전/뒤 비교). 새 구간은 0.5초 뒤부터
                        EndRun(); runNo++; runKind = arg; runStart = now - 1.5f;
                        return true;
                    }
                case "kick":
                    {
                        // 느린 판(화면 대기 1.7ms)을 빠른 판으로 바꿀 수 있는지 시험하는 한 번짜리 동작
                        var parts = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        string kind = parts.Length > 0 ? parts[0] : "";
                        int n = parts.Length > 1 ? int.Parse(parts[1]) : 1;
                        if (kind == "present") { for (int i = 0; i < n; i++) WindowGhost.PresentOnce(0); }          // 보통 Present(0,0) n번 (그래픽 스레드)
                        else if (kind == "presentdns") { for (int i = 0; i < n; i++) WindowGhost.PresentOnce(0x21); }
                        else if (kind == "freeze") System.Threading.Thread.Sleep(n);                                      // 메인 스레드 n ms 멈춤
                        else throw new Exception("모르는 킥: " + kind);
                        Log("킥: " + arg);
                        return true;
                    }
                case "tune":
                    PcTune.Measure();
                    Log("PC 맞춤 추천: " + string.Join(", ", PcTune.RecommendLabels().ToArray()));
                    return true;
                case "tunesim":
                    {
                        // 약한 PC(내장 그래픽 + 느린 CPU)인 척 적용했다가 되돌려, 설정이 전과 같은지 본다 (저장 파일도)
                        Func<string> snap = () => { var sb = new System.Text.StringBuilder(); foreach (var f in typeof(Settings).GetFields()) sb.Append(f.Name).Append("=").Append(Convert.ToString(f.GetValue(Main.Config), System.Globalization.CultureInfo.InvariantCulture)).Append(";"); return sb.ToString(); };
                        string before = snap();
                        PcTune.Measure(); PcTune.Integrated = PcTune.GpuWeak = PcTune.CpuWeak = true;
                        var done = PcTune.Apply();
                        string mid = snap();
                        PcTune.Undo();
                        string after = snap();
                        Log("PC 맞춤 시험 적용: " + string.Join(", ", done.ToArray()) + " | 바뀐 값 " + (before == mid ? "없음(이상)" : "있음") + " | 되돌린 뒤 " + (before == after ? "전과 같음" : "다름: " + Diff(before, after)));
                        PcTune.HasResult = false;
                        return true;
                    }
                case "setstate":
                    {
                        // scrController.currentState 를 직접 바꾼다. 에디터 밖에서 죽은 뒤 에디터로 오면 Fail 이 남아 있는 상황을 흉내 내려고.
                        var ctrl = ADOBase.controller;
                        if (ctrl == null) throw new Exception("scrController 없음");
                        var st = (States)Enum.Parse(typeof(States), arg, true);
                        AccessTools.FieldRefAccess<scrController, States>("currentState")(ctrl) = st;
                        Log("게임 상태를 " + st + " 로 바꿈");
                        return true;
                    }
                case "jiggle":
                    {
                        // 옆 스레드가 N초 동안 50ms 마다 마우스 커서를 몇 픽셀씩 움직인다(클릭 없음). 바로 다음 play 의 멈춤 동안
                        // 사람이 마우스를 건드린 것처럼 입력이 쌓이게 하려고. 끝나면 커서를 제자리로.
                        float sec; float.TryParse(arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out sec);
                        if (sec <= 0f) sec = 5f;
                        int ms = (int)(sec * 1000);
                        new System.Threading.Thread(() =>
                        {
                            POINT p0; GetCursorPos(out p0);
                            var sw = System.Diagnostics.Stopwatch.StartNew(); int n = 0;
                            while (sw.ElapsedMilliseconds < ms) { n++; SetCursorPos(p0.x + (n % 2 == 0 ? 6 : -6), p0.y + (n % 4 < 2 ? 4 : -4)); System.Threading.Thread.Sleep(50); }
                            SetCursorPos(p0.x, p0.y);
                        }) { IsBackground = true, Name = "StutterFix.AutoTestJiggle" }.Start();
                        Log("마우스 움직이기 " + sec + "초 (클릭 없음)");
                        return true;
                    }
                case "auto":
                    {
                        bool on = arg.Equals("on", StringComparison.OrdinalIgnoreCase);
                        if (!autoChanged) { prevAuto = RDC.auto; autoChanged = true; }
                        desiredAuto = on; RDC.auto = on; Log("자동 플레이 " + (on ? "켬" : "끔") + " (재생 때마다 다시 맞춤)");
                        return true;
                    }
                case "open":
                    if (openPhase == 0)
                    {
                        if (!File.Exists(arg)) throw new Exception("맵 파일 없음: " + arg);
                        RestartAdvisor.BeginOpen(arg); openPhase = 1; openStartedAt = now; Log("맵 열기: " + arg);
                        return false;
                    }
                    if (openPhase == 1)
                    {
                        if (RestartAdvisor.Opening || ed == null) return false;
                        if (loadingField != null && (bool)loadingField.GetValue(ed)) return false;
                        if (!SamePath(ADOBase.levelPath, arg)) return false;
                        openPhase = 2; waitSec = now;
                        return false;
                    }
                    if (now - waitSec < 2f) return false;   // 열린 뒤 2초 (이미지 결과 창 등 정리)
                    openPhase = 0; Log(string.Format("맵 열림 ({0:F1}초, 열기 요청부터 2초 기다림 포함)", now - openStartedAt));
                    return true;
                case "play":
                    if (ed == null) throw new Exception("에디터가 아님");
                    if (ed.playMode) { Log("이미 재생 중"); return true; }
                    if (autoChanged) RDC.auto = desiredAuto;   // 메뉴에서 켠 값이 에디터에 들어가며 풀렸다(2026-09-27)
                    Log("재생 (자동 플레이 " + (RDC.auto ? "켬" : "끔") + ")");
                    ed.Play();
                    BeginRun("Play");
                    return true;
                case "stop":
                    if (ed == null) throw new Exception("에디터가 아님");
                    EndRun();
                    if (ed.playMode) ed.SwitchToEditMode();
                    Log("편집으로 돌아감");
                    return true;
                case "quit":
                    Finish("끝");
                    return true;
                default:
                    throw new Exception("모르는 명령: " + cmd);
            }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] private struct POINT { public int x, y; }
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
        private static void AltTab()
        {
            const byte VK_MENU = 0x12, VK_TAB = 0x09; const uint KEYUP = 2;
            keybd_event(VK_MENU, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(30);
            keybd_event(VK_TAB, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(30);
            keybd_event(VK_TAB, 0, KEYUP, UIntPtr.Zero); System.Threading.Thread.Sleep(30);
            keybd_event(VK_MENU, 0, KEYUP, UIntPtr.Zero);
        }

        private static string Diff(string a, string b)
        {
            var x = a.Split(';'); var y = b.Split(';'); var d = new List<string>();
            for (int i = 0; i < Math.Min(x.Length, y.Length); i++) if (x[i] != y[i]) d.Add(x[i] + " -> " + y[i]);
            return string.Join(", ", d.ToArray());
        }

        private static bool SamePath(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); } catch { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
        }

        private static bool finished;
        private static void Finish(string why)
        {
            if (finished) return;
            finished = true;
            EndRun();
            Log(why + " - 게임을 끕니다");
            steps = null;
            try { if (autoChanged) RDC.auto = prevAuto; } catch { }
            RestartAdvisor.ForceQuitNext();
            Application.Quit();
        }
    }
}
