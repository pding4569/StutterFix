using System;
using System.Runtime.InteropServices;

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
    //   - 유니티 대기 단계(TimeUpdate.WaitForLastPresentationAndUpdateTime)를 한 프레임 건너뛰기 / 두 번 돌리기: 그대로
    //   - 곡 중에 Alt+Tab 으로 나갔다 오기: 그대로
    //   - WPR 기록을 켜 두고(부하) Play 하거나, PowerShell 에서 Alt+Tab 으로 돌아온 직후 Play 하면 빨랐다(2번 중 2번)
    // 곡 중에는 무엇을 해도 안 바뀌니 상태는 판이 시작되기 전에 정해진다. 느린 판이 나온 Arche 에디터 Play 는 한 프레임이 5.2~5.8초,
    // 늘 빠른 다시 하기는 3.2~3.9초, 빠른 MEGAMIX Play 는 2.7~3.1초였다. 윈도우는 창이 5초 넘게 메시지를 처리하지 않으면(그 사이 입력이
    // 있으면) "응답 없음" 고스트 창으로 바꿔 둔다. 입력 여부에 따라 걸리거나 안 걸리므로 운처럼 보이고, 5초를 넘는 Play 에서만 나온다.
    // 고스트 창을 거친 뒤 윈도우가 게임 창의 화면을 다르게 다룬다는 가설을 시험하려고 DisableProcessWindowsGhosting 으로 고스트 창을 끈다.
    // (고스트 창을 끄면 게임이 정말 멈췄을 때 "응답 없음" 표시와 그 상태의 창 옮기기·최소화가 안 된다. 많은 게임이 끈다.)
    internal static class PresentProbe
    {
        [DllImport("user32.dll")] private static extern void DisableProcessWindowsGhosting();

        internal static string GhostNote = "";

        internal static void NoGhosting()
        {
            try { DisableProcessWindowsGhosting(); GhostNote = "고스트 창 끔"; }
            catch (Exception ex) { GhostNote = "고스트 창 끄기 실패: " + ex.Message; }
            Main.Entry.Logger.Log("[화면 대기] " + GhostNote);
        }
    }
}
