using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix
{
    // PC 맞춤 자동 설정: 짧게 재 보고(CPU 한 스레드 속도, GPU 메모리 대역, 내장 그래픽인지, RAM, 주사율) 약한 쪽에 맞는 저사양 기능을 켠다.
    //
    // 처음 켤 때(이 기능이 들어간 버전에서 한 번) 메뉴 화면에서 자동으로 잰다(0.3초 안팎). 강한 PC 는 아무것도 바꾸지 않는다.
    // 켜는 것은 전부 원래 "저사양" 페이지에 있던 기능이고, 바꾸기 전 값을 저장해 두어 되돌릴 수 있다.
    //
    // 내장 그래픽(인텔 UHD·Iris, AMD Radeon(TM) Graphics·Vega·780M 등): 그래픽 메모리가 따로 없고 시스템 RAM 과 대역·전력을 CPU 와 나눠 쓴다.
    //   그래서 GPU 를 덜 쓰게 하는 쪽이 CPU 에도 좋다: 자동 해상도(목표 FPS 를 못 맞출 때만 게임 화면을 작게 그리고 FSR 1 로 늘림),
    //   장식 이미지 DXT 압축(메모리·대역 1/4), 메뉴·에디터 FPS 제한(플레이 중이 아닐 때 전력 아끼기).
    //   화면 출력 Flip 방식(윈도우가 화면을 한 번 더 복사하는 것을 줄임)은 실험 기능이라 켜지 않고 권하기만 한다.
    internal static class PcTune
    {
        internal const int Version = 1;

        // 이 PC(개발, i5-9400F + RTX 4060 Ti, 3440x1440)에서 잰 값 = 100 (2026-09-27)
        internal static double RefCpu = 23100, RefGpuMsPerMpx = 0.0086;   // 세 번 재서 22789~23163, 0.0085~0.0087

        internal static bool HasResult;
        internal static double CpuScore = -1, GpuScore = -1, GpuMsPerMpx = -1;
        internal static bool Integrated, CpuWeak, GpuWeak, RamLow, VramLow;
        internal static string GpuName = "", CpuName = "";
        internal static int RamMB, Cores, Refresh;

        // ── 재기 ──
        private static double CpuBench(int ms)
        {
            var dict = new Dictionary<int, int>(1024);
            for (int i = 0; i < 1024; i++) dict[i] = i * 3;
            var list = new List<object>(64);
            double acc = 0; long it = 0;
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < ms)
            {
                for (int k = 0; k < 1000; k++)
                {
                    acc += Math.Sin(k * 0.001) * Math.Cos(it * 1e-6);
                    int v; if (dict.TryGetValue((k * 7 + (int)it) & 1023, out v)) acc += v;
                    if ((k & 31) == 0) { list.Add(new float[4]); if (list.Count >= 64) list.Clear(); }
                }
                it += 1000;
            }
            if (acc == 12345.678) UnityEngine.Debug.Log(acc);   // 계산이 지워지지 않게
            return it / sw.Elapsed.TotalMilliseconds;   // 1ms 에 도는 수
        }

        // 화면 크기 텍스처를 번갈아 복사하는 시간 (GPU 메모리 대역). n 번 복사 뒤 1픽셀 읽어 GPU 가 끝날 때까지 기다린다.
        private static double BlitMs(RenderTexture a, RenderTexture b, int n)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < n; i++) Graphics.Blit((i & 1) == 0 ? a : b, (i & 1) == 0 ? b : a);
            AsyncGPUReadback.Request((n & 1) == 0 ? a : b, 0, 0, 1, 0, 1, 0, 1).WaitForCompletion();
            return sw.Elapsed.TotalMilliseconds;
        }
        private static double GpuBench(out int w, out int h)
        {
            w = Mathf.Max(640, Screen.width); h = Mathf.Max(360, Screen.height);
            var a = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { name = "StutterFix.Bench.A" };
            var b = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { name = "StutterFix.Bench.B" };
            var prev = RenderTexture.active;
            try
            {
                a.Create(); b.Create();
                BlitMs(a, b, 4);   // 준비
                double best = double.MaxValue;
                for (int r = 0; r < 3; r++)   // 세 번 재서 가장 빠른 것 (다른 일에 끼인 것 빼기)
                {
                    double t5 = BlitMs(a, b, 5), t45 = BlitMs(a, b, 45);
                    best = Math.Min(best, Math.Max(0.001, (t45 - t5) / 40.0));
                }
                return best / (w * (double)h / 1e6);   // 100만 픽셀당 복사 한 번 ms
            }
            finally { RenderTexture.active = prev; a.Release(); b.Release(); UnityEngine.Object.Destroy(a); UnityEngine.Object.Destroy(b); }
        }

        internal static bool IsIntegrated(string name, int vendor)
        {
            string n = (name ?? "").ToLowerInvariant();
            if (n.Contains("microsoft basic render")) return true;
            if (vendor == 0x8086) return !n.Contains("arc");            // 인텔: Arc 말고는 내장
            if (vendor == 0x1002)                                         // AMD
            {
                if (Regex.IsMatch(n, @"radeon\s*(\(tm\))?\s*graphics")) return true;   // 라이젠 APU (번호 없는 이름, "AMD Radeon (TM) Graphics" 처럼 띄어 쓴 것도)
                if (Regex.IsMatch(n, @"vega\s*\d+\s*graphics")) return true;            // Vega 3/6/8/11 Graphics
                if (Regex.IsMatch(n, @"radeon\s*(\(tm\))?\s*\d{3}m\b")) return true;    // 680M, 760M, 780M, 890M
                return false;
            }
            return false;
        }

        internal static void Measure()
        {
            try
            {
                GpuName = SystemInfo.graphicsDeviceName ?? ""; CpuName = SystemInfo.processorType ?? "";
                RamMB = SystemInfo.systemMemorySize; Cores = SystemInfo.processorCount;
                try { Refresh = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value); } catch { Refresh = 60; }
                Integrated = IsIntegrated(GpuName, SystemInfo.graphicsDeviceVendorID);
                var sw = Stopwatch.StartNew();
                double cpu = CpuBench(200);
                int w, h; double gpu = GpuBench(out w, out h);
                CpuScore = 100.0 * cpu / RefCpu;
                GpuMsPerMpx = gpu;
                GpuScore = 100.0 * RefGpuMsPerMpx / gpu;
                CpuWeak = CpuScore < 55;
                GpuWeak = Integrated || GpuScore < 8;   // 따로 그래픽 카드는 이 PC 보다 12배 넘게 느릴 때만 (자동 해상도는 GPU 가 목표 FPS 를 못 맞출 때만 움직임)
                VramLow = !Integrated && SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize < 3000;
                RamLow = RamMB > 0 && RamMB < 12000;
                HasResult = true;
                Main.Entry.Logger.Log(string.Format("[PC 맞춤] 측정 {0}ms: CPU {1} ({2:F0}, 1ms 에 {3:F0}) | GPU {4}{5} ({6:F0}, {7}x{8} 복사 100만 픽셀당 {9:F4}ms) | RAM {10}MB, 코어 {11}, 주사율 {12}Hz -> {13}",
                    sw.ElapsedMilliseconds, CpuName, CpuScore, cpu, GpuName, Integrated ? " (내장 그래픽)" : "", GpuScore, w, h, gpu, RamMB, Cores, Refresh,
                    (CpuWeak || GpuWeak || RamLow) ? ("약한 쪽: " + (CpuWeak ? "CPU " : "") + (GpuWeak ? "GPU " : "") + (RamLow ? "RAM" : "")) : "충분함"));
            }
            catch (Exception ex) { HasResult = false; Main.Entry.Logger.Log("[PC 맞춤] 측정 실패: " + ex.Message); }
        }

        // ── 추천 ──
        private sealed class Change { public string Key; public string Label; public Func<object> Get; public Action<object> Set; public object Want; }
        private static List<Change> Recommend()
        {
            var c = Main.Config; var list = new List<Change>();
            Action<string, string, Func<object>, Action<object>, object> add = (k, label, get, set, want) => { if (!Equals(get(), want)) list.Add(new Change { Key = k, Label = label, Get = get, Set = set, Want = want }); };
            if (!HasResult) return list;
            if (GpuWeak)
            {
                add("LowAutoRes", SettingsWindow.T("자동 해상도", "Auto resolution"), () => c.LowAutoRes, v => c.LowAutoRes = (bool)v, true);
                add("LowAutoFps", SettingsWindow.T("자동 해상도 목표 FPS", "Auto resolution target FPS"), () => c.LowAutoFps, v => c.LowAutoFps = (int)v, Mathf.Clamp(Refresh, 60, 144));
                if (!c.LowSharpUpscale) add("LowFsr", SettingsWindow.T("FSR 1 로 늘리기", "FSR 1 upscaling"), () => c.LowFsr, v => c.LowFsr = (bool)v, true);
                // 이미지 압축은 화질이 아주 조금 바뀌므로 내장 그래픽(시스템 RAM 을 나눠 씀)에서만
                if (Integrated) add("LowCompressImages", SettingsWindow.T("장식 이미지 압축", "Compress decoration images"), () => c.LowCompressImages, v => c.LowCompressImages = (bool)v, true);
                if (Integrated && c.LowMenuFps == 0) add("LowMenuFps", SettingsWindow.T("메뉴·에디터 FPS 제한 60", "Menu/editor FPS cap 60"), () => c.LowMenuFps, v => c.LowMenuFps = (int)v, 60);
            }
            if (CpuWeak)
            {
                add("LowSplit", SettingsWindow.T("효과 몰림 잘게 나누기", "Finer effect splitting"), () => c.LowSplit, v => c.LowSplit = (int)v, Math.Max(1, c.LowSplit));
                add("LowPriority", SettingsWindow.T("게임 우선순위 높음", "High game priority"), () => c.LowPriority, v => c.LowPriority = (bool)v, true);
                add("LowNoThrottle", SettingsWindow.T("절전 제한 끄기", "No power throttling"), () => c.LowNoThrottle, v => c.LowNoThrottle = (bool)v, true);
                add("LowNoFft", SettingsWindow.T("음악 반응 계산 끄기(필요 없을 때)", "Skip music FFT when unused"), () => c.LowNoFft, v => c.LowNoFft = (bool)v, true);
                add("LowPauseParticles", SettingsWindow.T("화면 밖 파티클 멈추기", "Pause offscreen particles"), () => c.LowPauseParticles, v => c.LowPauseParticles = (bool)v, true);
            }
            if (RamLow || VramLow) add("LowCompressImages", SettingsWindow.T("장식 이미지 압축", "Compress decoration images"), () => c.LowCompressImages, v => c.LowCompressImages = (bool)v, true);
            // 같은 키는 한 번만
            var seen = new HashSet<string>(); var outList = new List<Change>();
            foreach (var ch in list) if (seen.Add(ch.Key)) outList.Add(ch);
            return outList;
        }

        internal static List<string> RecommendLabels() { var r = new List<string>(); foreach (var ch in Recommend()) r.Add(ch.Label); return r; }

        // 추천을 적용하고, 바꾸기 전 값을 저장한다. 바꾼 것 이름 목록
        internal static List<string> Apply()
        {
            var changes = Recommend(); var labels = new List<string>();
            if (changes.Count == 0) return labels;
            var backup = new List<string>();
            foreach (var ch in changes)
            {
                object before = ch.Get();
                backup.Add(ch.Key + "=" + Convert.ToString(before, System.Globalization.CultureInfo.InvariantCulture));
                ch.Set(ch.Want); labels.Add(ch.Label);
            }
            Main.Config.TuneBackup = string.Join(";", backup.ToArray());
            Main.ApplyConfig();
            try { Main.Config.Save(Main.Entry); } catch { }
            Main.Entry.Logger.Log("[PC 맞춤] 적용: " + string.Join(", ", labels.ToArray()) + " (되돌리기용 저장: " + Main.Config.TuneBackup + ")");
            return labels;
        }

        internal static bool CanUndo { get { return !string.IsNullOrEmpty(Main.Config.TuneBackup); } }
        internal static void Undo()
        {
            var c = Main.Config;
            foreach (var kv in (c.TuneBackup ?? "").Split(';'))
            {
                int eq = kv.IndexOf('='); if (eq <= 0) continue;
                string k = kv.Substring(0, eq), v = kv.Substring(eq + 1);
                try
                {
                    var f = typeof(Settings).GetField(k);
                    if (f == null) continue;
                    if (f.FieldType == typeof(bool)) f.SetValue(c, bool.Parse(v));
                    else if (f.FieldType == typeof(int)) f.SetValue(c, int.Parse(v, System.Globalization.CultureInfo.InvariantCulture));
                }
                catch { }
            }
            c.TuneBackup = ""; c.TuneNotice = "";
            Main.ApplyConfig();
            try { c.Save(Main.Entry); } catch { }
            Main.Entry.Logger.Log("[PC 맞춤] 되돌림");
        }

        // ── 처음 한 번 자동 ──
        private static float menuSince = -1f;
        internal static void Tick()
        {
            var c = Main.Config;
            if (c == null || c.TuneVer >= Version) return;
            if (Edition.Dev && AutoTest.Active) return;
            string scene;
            try { scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name; } catch { return; }
            bool menu = !(scene == "" || scene == "scnSplash" || scene == "scnLoading" || scene == "scnIntro" || scene == "scnEditor" || scene == "scnGame");
            if (!menu || Time.realtimeSinceStartup < 8f) { menuSince = -1f; return; }
            if (menuSince < 0f) { menuSince = Time.realtimeSinceStartup; return; }
            if (Time.realtimeSinceStartup - menuSince < 3f) return;   // 메뉴가 뜨고 3초 뒤
            c.TuneVer = Version;
            Measure();
            if (HasResult)
            {
                c.TuneLast = Describe();
                var done = Apply();
                c.TuneNotice = done.Count > 0 ? string.Join(", ", done.ToArray()) : "";
            }
            try { c.Save(Main.Entry); } catch { }
        }

        internal static string Describe()
        {
            if (!HasResult) return SettingsWindow.T("아직 안 잼", "Not measured yet");
            return string.Format(SettingsWindow.T("CPU {0:F0}점 · GPU {1:F0}점{2} · RAM {3}GB (이 모드 개발 PC = 100점)", "CPU {0:F0} · GPU {1:F0}{2} · RAM {3} GB (mod dev PC = 100)"),
                CpuScore, GpuScore, Integrated ? SettingsWindow.T(" (내장 그래픽)", " (integrated)") : "", Mathf.RoundToInt(RamMB / 1024f));
        }
    }
}
