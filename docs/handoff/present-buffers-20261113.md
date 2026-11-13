# 스왑체인 버퍼 수 늘리기 시제품 (2026-11-13, present-path 후속)

## 측정 사실 (Claude, HELLO 50초, 3440 렌더, FullScreenWindow 2560x1600 144Hz)
`presentpath on observe` 상태 줄: `buffers=2 effect=3 creationFlags=2114(0x842: MODE_SWITCH|ALLOW_TEARING|WAITABLE) windowed=1 latency=4 sync=0 inputFlags=512(ALLOW_TEARING 이미 전달)`.
→ latency4·tearing 후보는 둘 다 이미 적용된 상태라 무효(no-op). **Present 호출 자체가 평균 2.83ms(wall)**, 2,421회. 메인의 WaitForPendingPresent 는 이 Present 반환을 기다린다(present-path-20261113-result.md).
가설: FLIP_SEQUENTIAL + BufferCount=2 + DWM 합성(창 모드)에서 DWM 이 한 버퍼를 다음 합성까지 잡고 있어, 다음 Present 가 남은 버퍼를 기다리며 막힌다. 버퍼 3개면 막힘이 줄 것.

## 할 일 (게임 실행 금지 — Claude 가 측정)
1. 기존 PresentPath(객체 전용 vtable, 복원 규칙 그대로)에 모드 `buffers3` 추가:
   - 게임 스왑체인의 `ResizeBuffers`(IDXGISwapChain vtable 슬롯 13) 를 객체 전용으로 가로채, Unity 가 부르는 BufferCount 를 3 으로 바꿔 넘긴다(Flags 는 그대로 — ALLOW_TEARING 일치 필수). 끄기 때는 2(원래 값, 상태에서 읽은 값)로 되돌리는 같은 방식.
   - **Unity 가 스스로 ResizeBuffers 를 부르게** 만든다: 백버퍼 참조 해제·재바인딩은 Unity 가 하므로 안전. 후보: `Screen.SetResolution(현재 w, h, 현재 모드)` 또는 창 크기를 1px 바꿨다 되돌리기 등. 실제로 Unity 가 ResizeBuffers 를 부르는 경로를 UnityPlayer 역어셈블(이전 present-path-disasm.py 방식)로 확인하고, 같은 해상도 SetResolution 이 무시되는지도 확인. 레지스트리 Screenmanager 값이 바뀌지 않게(또는 원래대로) — CLAUDE.md/메모: `-screen-*` 와 SetResolution 이 레지스트리를 오염시킨 전례 있음.
   - 상태 줄에 GetDesc1 BufferCount 실측(3 이 됐는지), ResizeBuffers 호출 수·HRESULT, Present 평균 µs 유지.
   - assert: buffers=3 실측, Present 실패 0, 원래 스왑체인 유지. off 뒤 buffers=원래 값.
2. ResizeBuffers 가로채기가 위험하거나 불가하면 대안을 근거와 함께: 예) 시작 시점 CreateSwapChainForHwnd 연결(모드가 늦게 로드되므로 불가하면 그 이유), DXGI 의 다른 방법.
3. CPU fixture 로 ResizeBuffers 인자 변경·복원·참조 수 검증(기존 present-path-fixture 확장).
4. bench_v14.py 용 CFG2 예시(`ppBuf3`, rlNR 복제, CFG_MID/POST 패턴은 ppObs 와 같게).

## 제한
기본값·제품 동작 변경 금지(AUTOTEST 전용, native export 는 뒤에 추가·기존 보존). 커밋·게임 실행·설치 DLL 교체 금지. 4종 빌드 오류 0. 결과: `docs/handoff/present-buffers-20261113-result.md`. 잰 것만 숫자로.
