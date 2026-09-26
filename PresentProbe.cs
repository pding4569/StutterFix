using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix
{
    // (개발자용) 판마다 갈리는 "화면 대기 1.7ms 상태" 조사.
    //
    // 2026-09-27 까지 확인: 에디터에서 Play 로 시작한 판은 대개 곡 내내 프레임마다 1.7ms 를 기다리고(약 200 FPS), 죽고 다시 하기로
    // 시작한 판은 기다리지 않는다(약 320 FPS). 한 판 안에서는 끝까지 같은 상태다. 화면 출력 방식, 멀티스레드 그리기, 프레임 시간 통계,
    // NVIDIA 저지연 모드, 게임의 maxQueuedFrames 모두 상관없었다.
    // 곡 중에 상태를 흔들어 보는 시험(앞서 준비 프레임 1 로 30프레임, 수직동기 5프레임, 목표 FPS 120 으로 3프레임)도 모두 1.70 -> 1.70ms
    // 그대로였다. 파이프라인이 한 번 비어도 그대로라서 "타이밍이 어긋난 상태" 가 아니라 장면에 무언가가 더 있는 쪽으로 본다:
    // 느린 판은 GPU 시간도 프레임당 1.2~1.4ms 로 빠른 판(0.9~1.0ms)보다 길다. 유니티 안쪽 구간 기록기(Gfx.WaitForPresentOnGfxThread 등)는
    // 플레이어 빌드에서 값이 안 나와 뺐다.
    //
    // 그래서 판마다 (1) 곡 시작 3초 뒤의 카메라 목록(이름, 그리는 곳, 순서)과 캔버스 수, (2) 곡 중 CPU 가 GPU 를 기다리게 만들 수 있는
    // 유니티 호출(ReadPixels, GetNativeTexturePtr, Camera.Render, Texture2D.Apply, Graphics.Blit, AsyncGPUReadback.WaitAllRequests,
    // GL.Flush)의 횟수와 처음 부른 곳을 적어 느린 판과 빠른 판을 비교한다.
    internal static class PresentProbe
    {
        private static bool wasPlaying;
        private static float songStart;
        private static bool censusDone;
        private static double secMs, secWait; private static int secN, secCount, highSec;

        // 부른 횟수 (곡 중에만 센다)
        private class Count { public string Name; public long N; public List<string> Callers = new List<string>(); }
        private static readonly Dictionary<MethodBase, Count> counts = new Dictionary<MethodBase, Count>();
        private static bool counting;
        private static string installNote = "";

        internal static void Install(Harmony harmony)
        {
            var prefix = new HarmonyMethod(typeof(PresentProbe), nameof(Hit));
            var targets = new List<MethodBase>();
            Action<Type, string> all = (t, name) =>
            {
                if (t == null) return;
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    if (m.Name == name && !m.IsGenericMethodDefinition) targets.Add(m);
            };
            all(typeof(Texture2D), "ReadPixels");
            all(typeof(Texture), "GetNativeTexturePtr");
            all(typeof(Camera), "Render");
            all(typeof(Texture2D), "Apply");
            all(typeof(Graphics), "Blit");
            all(typeof(AsyncGPUReadback), "WaitAllRequests");
            all(typeof(GL), "Flush");
            int ok = 0, fail = 0;
            foreach (var m in targets)
            {
                try { harmony.Patch(m, prefix: prefix); ok++; }
                catch { fail++; }   // 몸체 없는 extern 은 못 건다
            }
            installNote = "걸린 함수 " + ok + "개, 못 건 것 " + fail + "개";
        }

        public static void Hit(MethodBase __originalMethod)
        {
            if (!counting) return;
            try
            {
                Count c;
                if (!counts.TryGetValue(__originalMethod, out c))
                {
                    c = new Count { Name = __originalMethod.DeclaringType.Name + "." + __originalMethod.Name };
                    counts[__originalMethod] = c;
                }
                c.N++;
                if (c.Callers.Count < 3)
                {
                    string who = Caller();
                    if (!c.Callers.Contains(who)) c.Callers.Add(who);
                }
            }
            catch { }
        }

        // 유니티·Harmony·이 클래스가 아닌 첫 호출자
        private static string Caller()
        {
            var st = new StackTrace(2, false);
            for (int i = 0; i < st.FrameCount && i < 12; i++)
            {
                var m = st.GetFrame(i).GetMethod();
                if (m == null || m.DeclaringType == null) continue;
                string asm = m.DeclaringType.Assembly.GetName().Name;
                if (asm.StartsWith("UnityEngine") || asm.StartsWith("0Harmony") || m.DeclaringType == typeof(PresentProbe)) continue;
                return m.DeclaringType.FullName + "." + m.Name;
            }
            return "?";
        }

        internal static void Frame(bool playing, float ms, float wait)
        {
            if (!Edition.Dev) return;
            if (playing != wasPlaying)
            {
                if (!playing) End();
                wasPlaying = playing;
                if (playing) { counts.Clear(); counting = true; songStart = Time.realtimeSinceStartup; censusDone = false; secMs = secWait = 0; secN = secCount = highSec = 0; }
            }
            if (!playing) return;
            if (!censusDone && Time.realtimeSinceStartup - songStart > 3f) { censusDone = true; Census(); }
            if (ms > 500f) return;
            secMs += ms; secWait += wait; secN++;
            if (secMs < 1000) return;
            if (secWait / secN >= 1.0) highSec++;
            secCount++;
            secMs = secWait = 0; secN = 0;
        }

        private static string census = "";
        private static void Census()
        {
            try
            {
                var sb = new StringBuilder();
                var cams = Camera.allCameras;
                sb.Append("카메라 ").Append(cams.Length).Append("개:");
                foreach (var c in cams)
                {
                    if (c == null) continue;
                    var rt = c.targetTexture;
                    sb.AppendFormat(" [{0} 순서 {1}, {2}, 지우기 {3}, 레이어 0x{4:X}]", c.name, c.depth, rt != null ? "RT " + rt.width + "x" + rt.height : "화면", c.clearFlags, c.cullingMask);
                }
                int canv = 0, canvOverlay = 0;
                foreach (var cv in UnityEngine.Object.FindObjectsOfType<Canvas>()) { if (!cv.isActiveAndEnabled) continue; canv++; if (cv.renderMode == RenderMode.ScreenSpaceOverlay) canvOverlay++; }
                sb.AppendFormat(" | 켜진 캔버스 {0}개(화면 위 {1}개)", canv, canvOverlay);
                census = sb.ToString();
            }
            catch (Exception ex) { census = "조사 실패: " + ex.Message; }
        }

        private static void End()
        {
            counting = false;
            if (secCount == 0) return;
            var sb = new StringBuilder();
            sb.AppendFormat("[화면 대기 장면] 이번 판 {0}초 중 대기 1ms 넘은 초 {1} ({2}) | {3} | 곡 중 호출:", secCount, highSec, highSec * 2 > secCount ? "느린 판" : "빠른 판", census);
            if (counts.Count == 0) sb.Append(" 없음");
            foreach (var c in counts.Values)
                sb.AppendFormat(" {0} {1}번(초당 {2:F1}, 부른 곳: {3})", c.Name, c.N, c.N / (double)secCount, string.Join(", ", c.Callers.ToArray()));
            sb.Append(" | ").Append(installNote);
            Main.Entry.Logger.Log(sb.ToString());
        }
    }
}
