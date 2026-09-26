using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;

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
    //   - 화면 넘기기 직전 그래픽 스레드에 일 얹기(작은 복사 40·300번 x10프레임), GPU 바쁘게(화면 크기 복사 x30프레임): 모두 그대로
    //   - WPR(CPU+GPU) 기록 중에는 에디터 Play 판도 빨랐다(기록 부하가 있으면 안 생김 - 디코 화면 공유, 무거운 맵과 같은 쪽)
    // 파이프라인을 비워도(수직동기, 프레임 제한) 그대로이니 타이밍이 아니라 "몇 번째 프레임을 기다릴지" 세는 값이 한 칸 밀려 굳은 것으로
    // 본다: 느린 판은 방금 넘긴 프레임(GPU 1.3ms + 깨어나는 시간)을, 빠른 판은 그 앞 프레임(이미 끝남)을 기다린다.
    // 그렇다면 대기 단계(TimeUpdate.WaitForLastPresentationAndUpdateTime)를 한 프레임만 건너뛰거나 두 번 돌리면 다시 맞춰질 수 있다.
    //
    // 시험: 느린 상태가 2초 이어지면 (1) 대기 단계를 한 프레임 건너뛰기, (2) 한 프레임 두 번 돌리기를 차례로 하고 1초 뒤를 재어 적는다.
    // 건너뛴 프레임은 시간 갱신도 한 번 빠진다(다음 프레임이 두 프레임 몫). 개발자용 시험에서만.
    internal static class PresentProbe
    {
        private static bool wasPlaying;
        private static int state;           // 0 지켜봄, 1 바꾼 뒤 되돌리기 기다림, 2 되돌린 뒤 재는 중, 3 끝
        private static int kick, measureSec, highStreak, restoreIn;
        private static float before;
        private static double secMs, secWait; private static int secN;
        private static string results = "";
        private static readonly string[] kickNames = { "대기 단계 한 프레임 건너뛰기", "대기 단계 한 프레임 두 번" };
        private static PlayerLoopSystem saved;
        private static bool changed;

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
            if (state == 1)
            {
                // 바꾼 루프로 한 프레임이 돌았다: 되돌린다
                if (--restoreIn <= 0) { Restore(); state = 2; measureSec = 0; secMs = secWait = 0; secN = 0; }
                return;
            }
            if (ms > 500f) return;
            secMs += ms; secWait += wait; secN++;
            if (secMs < 1000) return;
            float avg = (float)(secWait / secN);
            secMs = secWait = 0; secN = 0;

            if (state == 0)
            {
                highStreak = avg >= 1.0f ? highStreak + 1 : 0;
                if (highStreak >= 2) { before = avg; kick = 0; Apply(); }
            }
            else if (state == 2)
            {
                if (++measureSec < 2) return;   // 되돌린 뒤 첫 1초는 넘어가는 중
                results += string.Format(" | {0}: {1:F2} -> {2:F2}ms", kickNames[kick], before, avg);
                if (avg < 0.3f) { results += " (풀림)"; state = 3; return; }
                before = avg;
                if (++kick < kickNames.Length) Apply(); else { results += " | 모두 안 풀림"; state = 3; }
            }
        }

        // 지금 루프에서 대기 단계만 빼거나(0) 두 번 넣은(1) 루프로 바꾼다. 이 호출 다음 프레임에 적용되고, 그다음 Frame 에서 되돌린다.
        private static void Apply()
        {
            try
            {
                saved = PlayerLoop.GetCurrentPlayerLoop();
                var root = PlayerLoop.GetCurrentPlayerLoop();
                var tops = root.subSystemList;
                bool found = false;
                for (int i = 0; i < tops.Length && !found; i++)
                {
                    var top = tops[i];
                    if (top.subSystemList == null) continue;
                    var list = new List<PlayerLoopSystem>();
                    foreach (var c in top.subSystemList)
                    {
                        if (c.type != null && c.type.Name == "WaitForLastPresentationAndUpdateTime")
                        {
                            found = true;
                            if (kick == 1) { list.Add(c); list.Add(c); }
                            continue;
                        }
                        list.Add(c);
                    }
                    if (found) { top.subSystemList = list.ToArray(); tops[i] = top; }
                }
                if (!found) { results += " | 대기 단계를 못 찾음"; state = 3; return; }
                root.subSystemList = tops;
                PlayerLoop.SetPlayerLoop(root);
                changed = true; restoreIn = 1; state = 1;
            }
            catch (Exception ex) { results += " | 시험 실패: " + ex.Message; state = 3; Restore(); }
        }

        private static void Restore()
        {
            if (!changed) return;
            try { PlayerLoop.SetPlayerLoop(saved); } catch { }
            changed = false;
        }

        private static void End()
        {
            Restore();
            if (results.Length > 0) Main.Entry.Logger.Log("[화면 대기 풀기 시험]" + results);
            state = 0;
        }
    }
}
