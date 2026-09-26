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
    internal static class WindowGhost
    {
        [DllImport("user32.dll")] private static extern void DisableProcessWindowsGhosting();

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
