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
            if (!Edition.Dev) return;
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
            if (now - stepStart > 180f) { Finish("시간 초과 (" + steps[idx] + ")"); return; }
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
                        RestartAdvisor.BeginOpen(arg); openPhase = 1; Log("맵 열기: " + arg);
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
                    openPhase = 0; Log("맵 열림");
                    return true;
                case "play":
                    if (ed == null) throw new Exception("에디터가 아님");
                    if (ed.playMode) { Log("이미 재생 중"); return true; }
                    if (autoChanged) RDC.auto = desiredAuto;   // 메뉴에서 켠 값이 에디터에 들어가며 풀렸다(2026-09-27)
                    Log("재생 (자동 플레이 " + (RDC.auto ? "켬" : "끔") + ")");
                    ed.Play();
                    return true;
                case "stop":
                    if (ed == null) throw new Exception("에디터가 아님");
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
            Log(why + " - 게임을 끕니다");
            steps = null;
            try { if (autoChanged) RDC.auto = prevAuto; } catch { }
            RestartAdvisor.ForceQuitNext();
            Application.Quit();
        }
    }
}
