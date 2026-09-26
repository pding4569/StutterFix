using System;
using System.Collections;
using UnityEngine;

namespace StutterFix
{
    // (개발자용) 판마다 갈리는 "화면 대기 1.7ms 상태" 조사.
    //
    // 2026-09-27 까지 확인: 에디터에서 Play 로 시작한 판은 대개 곡 내내 프레임마다 1.7ms 를 기다리고(약 200 FPS), 죽고 다시 하기로
    // 시작한 판은 기다리지 않는다(약 320 FPS). 한 판 안에서는 끝까지 같은 상태다. 화면 출력 방식, 멀티스레드 그리기, 프레임 시간 통계,
    // NVIDIA 저지연 모드, 게임의 maxQueuedFrames 모두 상관없었다.
    //   - 곡 중 흔들기(앞서 준비 프레임 1 로 30프레임, 수직동기 5프레임, 목표 FPS 120 으로 3프레임): 모두 1.70 -> 1.70ms 그대로
    //   - 느린 판/빠른 판 장면 비교: 카메라 5개(이름·그리는 곳·순서 같음), 캔버스 31개 같음, 프레임당 Blit 수 같음,
    //     ReadPixels·GetNativeTexturePtr·Camera.Render·Texture2D.Apply·WaitAllRequests·GL.Flush 호출은 어느 판에도 없음
    // 장면이 같으니 남는 것은 순서 경쟁이다: 유니티는 프레임 시작(TimeUpdate)에 "마지막으로 화면에 넘긴 프레임" 을 GPU 가 끝낼 때까지
    // 기다린다. 메인 스레드가 프레임을 넘기자마자 다음 프레임을 시작하는데, 그 사이 그래픽 스레드가 먼저 화면 넘기기까지 끝내 버리면
    // 방금 넘긴 프레임(GPU 1.3ms + α)을 기다리게 되고, 그 상태로 굳는다(기다리는 동안 그래픽 스레드는 한가해서 다음에도 먼저 끝낸다).
    // 그렇다면 화면 넘기기 직전에 그래픽 스레드에 일을 조금 얹어 메인 스레드가 먼저 확인하게 하면 빠른 상태로 넘어갈 것이다.
    //
    // 시험: 느린 상태가 2초 이어지면 차례로 (1) 프레임 끝에 작은 복사 40번을 10프레임, (2) 300번을 10프레임, (3) 화면 크기 복사
    // 20번을 30프레임(GPU 를 잠깐 바쁘게) 해 보고, 끝난 뒤 1초를 재어 풀렸는지 적는다. 그림은 바뀌지 않는다(화면 밖 버퍼끼리 복사).
    internal static class PresentProbe
    {
        private static bool wasPlaying;
        private static int state;           // 0 지켜봄, 1 시험 중, 2 시험 뒤 재는 중, 3 끝
        private static int kick, measureSec, highStreak;
        private static float before;
        private static double secMs, secWait; private static int secN;
        private static bool kicking;
        private static string results = "";
        private static readonly string[] kickNames = { "프레임 끝 작은 복사 40번 x10프레임", "프레임 끝 작은 복사 300번 x10프레임", "화면 크기 복사 20번 x30프레임" };
        private static RenderTexture smallA, smallB, bigA, bigB;

        internal static void Frame(bool playing, float ms, float wait)
        {
            if (!Edition.Dev) return;
            if (playing != wasPlaying)
            {
                if (!playing) End();
                wasPlaying = playing;
                if (playing) { state = 0; highStreak = 0; results = ""; secMs = secWait = 0; secN = 0; }
            }
            if (!playing) return;
            if (state == 1) { if (!kicking) { state = 2; measureSec = 0; secMs = secWait = 0; secN = 0; } return; }
            if (ms > 500f) return;
            secMs += ms; secWait += wait; secN++;
            if (secMs < 1000) return;
            float avg = (float)(secWait / secN);
            secMs = secWait = 0; secN = 0;

            if (state == 0)
            {
                highStreak = avg >= 1.0f ? highStreak + 1 : 0;
                if (highStreak >= 2) { before = avg; kick = 0; Start(); }
            }
            else if (state == 2)
            {
                if (++measureSec < 2) return;   // 끝난 뒤 첫 1초는 넘어가는 중
                results += string.Format(" | {0}: {1:F2} -> {2:F2}ms", kickNames[kick], before, avg);
                if (avg < 0.3f) { results += " (풀림)"; state = 3; return; }
                before = avg;
                if (++kick < kickNames.Length) Start(); else { results += " | 모두 안 풀림"; state = 3; }
            }
        }

        private static void Start()
        {
            var host = PerfOverlay.Instance;
            if (host == null) { results += " | 시험 못 함(모니터 없음)"; state = 3; return; }
            state = 1; kicking = true;
            if (kick == 0) host.StartCoroutine(Kick(10, 40, false));
            else if (kick == 1) host.StartCoroutine(Kick(10, 300, false));
            else host.StartCoroutine(Kick(30, 20, true));
        }

        private static IEnumerator Kick(int frames, int blits, bool big)
        {
            var eof = new WaitForEndOfFrame();
            try
            {
                if (big)
                {
                    if (bigA == null) { bigA = new RenderTexture(Screen.width, Screen.height, 0); bigB = new RenderTexture(Screen.width, Screen.height, 0); }
                }
                else if (smallA == null) { smallA = new RenderTexture(64, 64, 0); smallB = new RenderTexture(64, 64, 0); }
            }
            catch (Exception ex) { results += " | 버퍼 못 만듦: " + ex.Message; kicking = false; yield break; }
            for (int f = 0; f < frames && Hitch.Playing; f++)
            {
                yield return eof;
                try
                {
                    var a = big ? bigA : smallA; var b = big ? bigB : smallB;
                    for (int i = 0; i < blits; i++) { if ((i & 1) == 0) Graphics.Blit(a, b); else Graphics.Blit(b, a); }
                }
                catch { }
            }
            kicking = false;
        }

        private static void End()
        {
            if (results.Length > 0) Main.Entry.Logger.Log("[화면 대기 풀기 시험]" + results);
            state = 0; kicking = false;
            try
            {
                if (bigA != null) { bigA.Release(); bigB.Release(); UnityEngine.Object.Destroy(bigA); UnityEngine.Object.Destroy(bigB); bigA = bigB = null; }
                if (smallA != null) { smallA.Release(); smallB.Release(); UnityEngine.Object.Destroy(smallA); UnityEngine.Object.Destroy(smallB); smallA = smallB = null; }
            }
            catch { }
        }
    }
}
