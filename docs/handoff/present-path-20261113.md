# 화면 출력 경로(Present) 대기 제거 조사·시제품 (2026-11-13)

## 근거 (Claude 측정)
- HELLO 50초, 3440 렌더(rtsize), 장식·타일·행성 전부 숨김 + 타일 스크립트 건너뜀(cpCeil): 194.5 FPS, GPU 0.17ms, 메인 3.68ms, 렌더 스레드 0.74ms, **현재대기 1.50ms**.
- 네이티브 표본(night-results/nat-hl50-cpCeil.resolved.txt, symtop.txt): 메인 스레드 표본의 **29.1% 가 `GfxDeviceClient::BeginFrame → WaitForPendingPresent → PlatformGfxSemaphore::WaitForSignal`**. GPU 는 거의 놀고 렌더 스레드 일도 0.74ms 뿐인데, 메인이 직전 프레임 Present 완료를 기다린다.
- 기준(rlNR, 내용 그림)도 현재대기 1.38~1.41ms(2560x1600 144Hz FullScreenWindow), 1024x768 60Hz 창에서도 0.94ms.
- 과거(2026-10-04) 사용자 실제 모니터 PresentMon: FullScreenWindow 는 `Composed: Flip`(DWM 합성), 독점 전체 화면(ExpFullscreen=1)은 `Hardware: Independent Flip` + AllowsTearing 1, 지연 절반이지만 FPS 209→187.
- 기존 엔진 패치: WaitSkip(0x5D0A62 등, 화면 대기 건너뛰기)는 이미 켜져 있다 — 이번 대기는 그것과 다른 경로인지 먼저 확인할 것. sfnative 에는 프레임 늘리기 연구의 게임 스왑체인 Present 연결 경험이 있다(CLAUDE.md 연구 13~15·32장: 공용 vtable 즉시 복원, 객체 전용 연결, Steam 오버레이 주의).

## 목표
테두리 없는 전체 화면(FullScreenWindow)에서 이 Present 대기를 없애 메인 스레드가 다음 프레임을 바로 시작하게 한다. 화면 내용은 원본과 같아야 한다(찢어짐 허용 여부는 사용자 결정 사항이므로 **켜고 끄는 실험 설정**으로만).

## 할 일 (게임 실행 금지, 게임은 Claude 가 측정에 사용 중)
1. **오프라인 분석**: UnityPlayer.dll(설치본, PDB 일치 확인, tools/analysis/engine-hot-disasm.py 방식)에서
   - 스왑체인 생성 경로: `CreateSwapChainForHwnd`/`CreateSwapChain` 호출부, DXGI_SWAP_CHAIN_DESC1 의 SwapEffect·BufferCount·Flags(ALLOW_TEARING, FRAME_LATENCY_WAITABLE_OBJECT) 를 무엇으로 채우는지, 어떤 조건(창 모드/vSync/명령줄 옵션)에 따라 바뀌는지.
   - Present 호출부: SyncInterval·Flags(DXGI_PRESENT_ALLOW_TEARING) 결정 로직.
   - `GfxDeviceClient::WaitForPendingPresent`·`WaitForSignal` 의 의미: 몇 프레임 앞서기를 허용하는지(세마포어 초기값/최대값), MaxQueuedFrames(QualitySettings.maxQueuedFrames) 및 우리 WaitSkip 패치와의 관계.
   - Unity 명령줄/숨은 옵션(문자열 검색): 예 `-force-d3d11-flip-model`, `-window-mode`, tearing·latency 관련 플래그가 있는지.
2. **원인 가설 정리**: Present 가 왜 1.5ms 막히는가(DWM 합성 + 버퍼 수/최대 지연, 프레임 지연 대기 객체 등). 근거 주소와 함께.
3. **시제품(AUTOTEST 전용 명령, 기본 꺼짐)**: 근거가 가장 강한 1~2개만.
   후보 예: (a) 스왑체인 생성 시 ALLOW_TEARING 플래그 + Present 에 ALLOW_TEARING(SyncInterval 0 일 때만) → DWM 이 테두리 없는 전체 화면을 Independent Flip 으로 올리게, (b) BufferCount 늘리기, (c) 메인이 기다리는 세마포어 허용치 조정.
   - 스왑체인 생성 연결은 게임 시작 때만 가능할 수 있음: 그 경우 ResizeBuffers 로 플래그를 다시 줄 수 있는지(ALLOW_TEARING 은 생성 때 플래그와 ResizeBuffers 플래그가 같아야 함) 확인하고, 실행 중 전환이 안 되면 "다음 실행부터 적용" 설정으로.
   - 명령: `presentpath state|on <후보>|off`, 상태 출력에 실제 스왑체인 desc(GetDesc1)·Present 플래그·현재대기 값을 찍어 적용 여부를 검증 가능하게.
   - Main.Unload·끄기에서 원래대로(연결 해제). Steam 오버레이와 공존.
4. bench_v14.py 에 넣을 CFG2 항목 예시(rlNR 복제)와 실행 방법을 결과 문서에 적기(실행·수정은 하지 않음 — 공용 파일).

## 제한
- 기본값·제품 동작 변경 금지. 새 코드는 AUTOTEST 빌드에만(필요하면 sfnative 에 새 export 추가, 기존 export 보존).
- 커밋·푸시·게임 실행·설치 DLL 교체 금지.
- 빌드 오류 0 (AUTOTEST, Player, DEV, sfnative).
- 막히면 비정상적인 아이디어도 빠르게 시험. 숫자는 잰 것만, 미측정은 미측정이라고.
- 결과: `docs/handoff/present-path-20261113-result.md`.
