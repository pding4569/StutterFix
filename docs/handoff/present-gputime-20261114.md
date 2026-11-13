# Present 2.8ms 의 정체: 실제 GPU 시간 재기 (2026-11-14)

## 사실 (Claude 측정)
- HELLO 50초, 3440 렌더, 2560x1440 FSW. 메인 스레드 표본 29% 가 `GfxDeviceClient::BeginFrame → WaitForPendingPresent`(직전 프레임 Present 완료 대기, RenderOffscreenCameras 안에서). 현재대기 1.35~1.45ms/프레임, 프레임 5.3ms.
- presentpath observe: 렌더 스레드의 **Present 호출 자체가 평균 2.83ms(wall)**. 버퍼 3장으로 바꾸면 오히려 3.86ms, FPS 이득 없음(HELLO +1.8%, Exit -8~-28%) → 버퍼 부족이 원인 아님. WaitSkip v5/v6 켬/끔 FPS 차이 없음.
- 벤치의 "GPU" 값(0.26~0.29ms)은 실제 GPU 프레임 전체가 아닐 수 있음(RunLod 켜면 2.99 로 뜀 — 의미 불명). 관리자 PresentMon 은 지금 못 씀.
- Exit(sh)는 같은 Present 2.83ms 인데 현재대기 ≈0, 335 FPS.

## 할 일 (게임 실행 금지 — Claude 가 측정)
기존 presentpath 객체 전용 후크(native/sfnative/presentpath.h, PresentPath.cs)에 AUTOTEST 모드 `presentpath on gputime` 추가:
1. D3D11 타임스탬프 쿼리(Disjoint + Timestamp)로 **실제 GPU 시간**: (a) 프레임 첫 렌더 이벤트(첫 화면 카메라 onPreRender 또는 기존 렌더 이벤트) 시점, (b) 원래 Present 호출 직전, (c) Present 직후. 링 8칸 이상, `GetData(... D3D11_ASYNC_GETDATA_DONOTFLUSH)` 로 기다리지 않고 회수. 프레임별 GPU 구간 a→b, 연속 b→b 간격, disjoint 플래그 프레임은 제외.
2. Present wall 시간을 쪼개기: 원래 Present 호출 전후 QPC, 그리고 같은 프레임의 GPU 완료 시각(가능하면 이벤트 쿼리 D3D11_QUERY_EVENT 를 Present 직전에 넣고 다음 프레임에 완료 여부로 "Present 반환 시 GPU 가 이미 끝났나" 비율).
3. 상태 줄: 회수 프레임 수, GPU a→b 평균/p50/p95/최대 µs, Present wall 평균/p95, "Present 반환 시점 GPU 미완료 비율", disjoint 수, 쿼리 실패 0.
4. 측정 비용 최소(프레임당 쿼리 3~4개, 매 프레임 할당 0). 끄기·Unload 원복.
5. 해석 가이드: GPU a→b ≈ Present wall 이면 GPU(드라이버 큐) 대기, GPU 작으면 DWM/OS 쪽 대기. 근거로 다음 후보(예: Present 앞당기기, 같은 프레임 안 GPU 작업 줄이기, 필터 블릿 정리)를 제안.
6. bench_v14 용 CFG2 `ppGpu` 예시(ppObs 와 같은 패턴). 4종 빌드 오류 0, export 보존.

결과: `docs/handoff/present-gputime-20261114-result.md`. 커밋·게임 실행·설치 DLL 교체 금지. 잰 것만 숫자로.
