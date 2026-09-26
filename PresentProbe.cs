using System;
using Unity.Profiling;
using UnityEngine;

namespace StutterFix
{
    // (개발자용) 판마다 갈리는 "화면 대기 1.7ms 상태" 조사.
    //
    // 2026-09-27 까지 확인: 에디터에서 Play 로 시작한 판은 대개 곡 내내 프레임마다 1.7ms 를 기다리고(약 200 FPS), 죽고 다시 하기로
    // 시작한 판은 기다리지 않는다(약 320 FPS). 한 판 안에서는 끝까지 같은 상태다. 화면 출력 방식, 멀티스레드 그리기, 프레임 시간 통계,
    // NVIDIA 저지연 모드, 게임의 maxQueuedFrames(2 -> 3) 모두 상관없었다. 메인 스레드가 "방금 넘긴 프레임을 GPU 가 끝낼 때까지"
    // 기다리는 모양이다.
    //
    // 여기서는 (1) 유니티 안쪽 구간(메인 스레드가 그래픽 스레드의 화면 넘기기를 기다린 시간 등)을 판마다 적고,
    // (2) 느린 상태가 2초 이어지면 곡 중에 풀 수 있는 방법을 하나씩 짧게 시험해 어느 것이 상태를 바꾸는지 적는다.
    // 시험은 그림을 바꾸지 않는다: 몇 프레임 동안 프레임 속도·수직동기·미리 준비하는 프레임 수만 바꿨다가 되돌린다.
    internal static class PresentProbe
    {
        // ── (1) 유니티 안쪽 구간 ──
        private static readonly string[] markerNames = { "Gfx.WaitForPresentOnGfxThread", "Gfx.PresentFrame", "Gfx.WaitForGfxCommandsFromMainThread", "Gfx.WaitForRenderThread" };
        private static ProfilerRecorder[] recs;
        private static readonly double[] sums = new double[4];
        private static int recFrames;

        // ── (2) 시험 ──
        private static int state;          // 0 지켜봄, 1 바꾸는 중, 2 되돌린 뒤 재는 중, 3 끝
        private static int remedy, holdLeft, measureSec, highStreak;
        private static float before;
        private static int origQueued, origVsync, origFps;
        private static double secMs, secWait; private static int secN;
        private static bool wasPlaying;
        private static readonly string[] remedyNames = { "앞서 준비하는 프레임 1 (30프레임)", "수직동기 켬 (5프레임)", "목표 FPS 120 (3프레임)" };
        private static string results = "";

        internal static void Frame(bool playing, float ms, float wait)
        {
            if (!Edition.Dev) return;
            if (playing != wasPlaying)
            {
                if (!playing) End();
                wasPlaying = playing;
                if (playing) Begin();
            }
            if (!playing) return;

            if (recs != null)
            {
                for (int i = 0; i < recs.Length; i++) if (recs[i].Valid) sums[i] += recs[i].LastValue / 1e6;
                recFrames++;
            }

            // 바꾸는 중: 정해진 프레임 수가 지나면 되돌린다
            if (state == 1 && --holdLeft <= 0) { Restore(); state = 2; measureSec = 0; secMs = secWait = 0; secN = 0; return; }

            if (ms > 500f) return;
            secMs += ms; secWait += wait; secN++;
            if (secMs < 1000) return;
            float avg = (float)(secWait / secN);
            secMs = secWait = 0; secN = 0;

            if (state == 0)
            {
                highStreak = avg >= 1.0f ? highStreak + 1 : 0;
                if (highStreak >= 2) { before = avg; remedy = 0; Apply(); }
            }
            else if (state == 2)
            {
                if (++measureSec < 2) return;   // 되돌린 뒤 첫 1초는 넘어가는 중
                results += string.Format(" | {0}: {1:F2} -> {2:F2}ms", remedyNames[remedy], before, avg);
                if (avg < 0.3f) { results += " (풀림)"; state = 3; return; }
                before = avg;
                if (++remedy < remedyNames.Length) Apply(); else { results += " | 모두 안 풀림"; state = 3; }
            }
        }

        private static void Apply()
        {
            try
            {
                origQueued = QualitySettings.maxQueuedFrames; origVsync = QualitySettings.vSyncCount; origFps = Application.targetFrameRate;
                if (remedy == 0) { QualitySettings.maxQueuedFrames = 1; holdLeft = 30; }
                else if (remedy == 1) { QualitySettings.vSyncCount = 1; holdLeft = 5; }
                else { Application.targetFrameRate = 120; holdLeft = 3; }
                state = 1;
            }
            catch (Exception ex) { results += " | 시험 실패: " + ex.Message; state = 3; }
        }

        private static void Restore()
        {
            try
            {
                if (QualitySettings.maxQueuedFrames != origQueued) QualitySettings.maxQueuedFrames = origQueued;
                if (QualitySettings.vSyncCount != origVsync) QualitySettings.vSyncCount = origVsync;
                if (Application.targetFrameRate != origFps) Application.targetFrameRate = origFps;
            }
            catch { }
        }

        private static void Begin()
        {
            state = 0; highStreak = 0; results = ""; secMs = secWait = 0; secN = 0;
            Array.Clear(sums, 0, sums.Length); recFrames = 0;
            if (recs == null)
            {
                recs = new ProfilerRecorder[markerNames.Length];
                for (int i = 0; i < markerNames.Length; i++)
                {
                    try { recs[i] = ProfilerRecorder.StartNew(ProfilerCategory.Render, markerNames[i]); } catch { }
                }
            }
        }

        private static void End()
        {
            if (state == 1) { Restore(); results += " | 판이 끝나 되돌림"; }
            if (recFrames > 0)
            {
                var sb = new System.Text.StringBuilder("[화면 대기 안쪽] 프레임당:");
                for (int i = 0; i < markerNames.Length; i++)
                    sb.AppendFormat(" {0} {1}", markerNames[i], recs != null && recs[i].Valid ? (sums[i] / recFrames).ToString("F2") + "ms" : "못 잼");
                sb.Append(" (" + recFrames + "프레임)");
                Main.Entry.Logger.Log(sb.ToString());
            }
            if (results.Length > 0) Main.Entry.Logger.Log("[화면 대기 풀기 시험]" + results);
            state = 0;
        }
    }
}
