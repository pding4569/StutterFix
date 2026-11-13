# WaitSkip 이 해상도 변경을 깨뜨림 — 제품 버그 수정 (2026-11-14, 3.0.0 막는 문제)

## 실측 (Claude, HELLO 50초, PlayerAuto, 화면 2560x1440 FullScreenWindow)
AUTOTEST 명령 `screenres <w> <h> fsw` (AutoTest.cs, 방금 추가, Screen.SetResolution 그대로 호출):
- 설정 `EngineWaitSkip=false` 로 켬 → 1920x1080 → 2560x1440 둘 다 정상, 곡 계속(209.7 FPS).
- 설정 `EngineWaitSkip=true`(3.0.0 기본값, DefaultsGen 2) → `Switching to resolution 1920x1080 failed` + `Screen: DX11 could not switch resolution (1920x1080 fs=1 hz=0/1)` → 게임 멈춤(치명적 오류 창 → exit 경로, present-buffers2 결과 문서의 0x13C40C8/0x8069DE/0xA4C5B4).
- presentpath resizeprobe 진단: `D3D11SwapChain::Adjust` 가드(`0x947BA4..0x947BD0`, GfxDevice +0x5E68 / +0x5E70) 값이 WaitSkip 켬에서 `adjGuardA=0 adjGuardB=10149`, 시작 때 켰다가 끈 경우 `A=1543 B=11221`. → WaitSkip 의 FwdSkip(0x5D0A62 je→jmp) 이 `WaitForLastPresentationAndGetTimestamp`(로그 경고 "PresentFrame() was called without calling ...WaitForLastPresentationAndGetTimestamp() first") 를 건너뛰어 A 가 늘지 않고, 한 번이라도 켜면 그 실행 내내 A≠B 라 **모든 해상도/창 크기/전체 화면 전환이 치명적 오류**가 되는 것으로 보임. 사용자 쪽 영향: 게임 설정 해상도 변경, Alt+Enter, 모니터 꺼짐/주사율 변경 등.

## 할 일 (게임 실행 금지 — Claude 가 측정)
1. 역어셈블(present-buffers-disasm.py 방식, 같은 설치본)로 확정: +0x5E68/+0x5E70 를 누가 언제 늘리는지(WaitForLastPresentationAndGetTimestamp 안? Present 쪽?), 0x5D0A62 분기가 정확히 무엇을 건너뛰는지, 가드가 지키는 것(대기 중 Present 가 없음을 보장?).
2. **WaitSkip 의 성능 이득은 유지하면서 A 가 B 와 맞게** 고친다. 근거가 가장 강한 방법 하나:
   (a) 건너뛰는 호출 안에서 대기만 빼고 카운터 증가는 하게(다른 바이트 패치 지점), 또는
   (b) 건너뛴 경로에 카운터 증가만 하는 짧은 코드(코드 동굴/트램펄린) — 가능하면 피함,
   (c) Unity 가 Adjust 를 부르기 직전(WinScreenSetup::SetResolution 입구 등)에만 A=B 로 맞추는 방법 — 가드 의미상 안전한지 근거 필요,
   (d) 해상도 변경 요청이 오면 WaitSkip 을 잠시 끄고 원래 대기를 한 번 실행해 A 를 따라잡게 하는 방법(가능하면).
   단, 대기 의미(지난 Present 완료)를 무시한 채 가드만 통과시키면 위험할 수 있음 — 근거를 써라.
3. 끄기(WaitSkip(false))·Main.Unload 원복 유지. 바이트 대조 가드 유지(버전 다르면 건너뜀).
4. AUTOTEST 진단: `waitskip state` 로 A/B 값 출력(presentpath 의 adjGuard 읽기 재사용).
5. 4종 빌드 오류 0, native export 보존. **제품 코드 변경 허용(이 버그 수정에 한함)**. 기본값 변경 금지.
6. Claude 검증 절차를 결과에 적기: bench_v14 의 `resWS`(EngineWaitSkip=true + screenres 1920→2560) 가 오류 없이 지나가야 하고, `rlNR` FPS 가 수정 전과 같아야 함(ABBA).

결과: `docs/handoff/waitskip-resize-20261114-result.md`. 커밋·게임 실행·설치 DLL 교체 금지. 잰 것만 숫자로.
