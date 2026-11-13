# Present 경로 오프라인 분석·시제품 결과 (2026-11-13)

요청된 오프라인 분석과 AUTOTEST 시제품을 완료했다. **1.5ms 제거 또는 FPS 향상은 아직 측정하지 않았다.** 게임 실행, 설치 DLL 교체, 커밋·푸시, 공용 `E:\StutterFixWork\bench_v14.py` 수정은 하지 않았다. 작업 위치는 `E:\StutterFixWork\wt-v14prod`, 기준 HEAD는 `a9244d6`이다. 시작 시 추적 파일 변경은 없었으며 기존 미추적 연구 산출물은 보존했다.

## 핵심 결론

1. `BeginFrame`의 이번 대기는 기존 WaitSkip과 **다른 동기화 경로**다. 렌더 워커가 직전 Present 명령을 끝냈다는 신호를 소비한다. `QualitySettings.maxQueuedFrames`가 직접 이 대기를 N프레임으로 바꾸는 구조는 아니다.
2. 이 Unity에는 이미 **flip-model + ALLOW_TEARING + FRAME_LATENCY_WAITABLE_OBJECT** 생성 코드와 `Present(0, ALLOW_TEARING)` 전달 코드가 있다. 따라서 “tearing을 추가하면 해결”이라는 가설은 실제 desc/입력 플래그 확인 전에는 성립하지 않는다.
3. 시제품은 `latency4`와 `tearing` 두 가지다. 같은 후크 비용을 갖는 `observe` 대조군도 제공한다. 원래 값이 이미 4이거나 tearing이 이미 전달되는 경우에는 **무효/no-op**로 거부한다. 세마포어 임의 증설·대기 호출 삭제는 신호 회계를 깨뜨릴 수 있어 구현하지 않았다.

## 바이너리·PDB 확인과 재현

- 설치본: `D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\UnityPlayer.dll`, Unity `6000.3.25f1`, x64 D3D11.
- DLL SHA256: `7e362b4c6ecee6fb493b1872cda5a5a0caf44f3b131b3c4bad132f0d1309f436`.
- RSDS GUID `0BD60840-8142-4C16-9079-D97BFE021469`, age `1`.
- 로컬 `E:\StutterFixWork\symbols\UnityPlayer_Win64_player_mono_x64.pdb\0BD6084081424C169079D97BFE0214691\UnityPlayer_Win64_player_mono_x64.pdb`의 **MSF info stream GUID·age와 직접 대조하여 일치 확인**.
- 주소 이름은 `night-results/unityplayer-syms.tsv`를 사용. TSV SHA256 `eecb98cdaa06e94bfc922dfe9c690dcdee184a1297c0fa379aac5895f4ecbf9e`.
- [오프라인 분석 스크립트](../../tools/analysis/present-path-disasm.py): `py -3.12 tools/analysis/present-path-disasm.py`.
- [전체 역어셈블](../../out/present-path/disasm.txt), [식별 정보](../../out/present-path/identity.txt). 주소는 모두 **RVA**. 근접 심볼 이름은 범위 설명이며 완전한 타입 복원은 아니다.

## 생성·Present·대기 분석

| 경로 | RVA / 확인한 동작 |
|---|---|
| Flip 선택 | `0x947920` `ShouldUseFlipSwapchainModel`. 인자와 전역 fallback 조건 확인 후 `force-d3d11-flip-model`/`force-d3d11-flip-mode`를 HasARGV로 검사. bitblt 관련 BootConfig 키가 있으면 flip 거부. 마지막으로 Windows 버전 검사 |
| 현대 생성 | `0x948A70` `CreateForHWND`. factory2의 vtable `+0x78`, `0x948C87`에서 `CreateSwapChainForHwnd`. flip 여부는 객체 `+0x342`, waitable 선택은 `+0x360` |
| DESC1 내용 | `0x948C06..0x948C58`. BufferCount는 flip이면 **2**, 아니면 **1**. SwapEffect는 flip이면 **3 (FLIP_SEQUENTIAL)**, 아니면 **0 (DISCARD)**. SampleDesc.Count=1, BufferUsage=`0x30`, Scaling=0, AlphaMode=0 |
| 생성 플래그 | `0x948B96..0x948BB4`: 기본 `0x2`(ALLOW_MODE_SWITCH), flip이면서 전역 `0x22744AA`가 참이면 `0x802`(추가 ALLOW_TEARING). `0x948C1D..0x948C30`에서 waitable 선택이면 `0x40`을 추가하여 **0x842** 가능 |
| 구형 fallback | `0x948D02` 이후. flip fallback 전역을 설정한 뒤 재판정. `0x948DF1`의 factory vtable `+0x50`이 `CreateSwapChain`. 해당 DESC는 BufferCount=1, SwapEffect=0, Flags는 조건부 `0x40` 포함. 실제 어느 경로가 사용됐는지는 runtime desc로 확인해야 함 |
| SyncInterval | `0x9478E0` `GetSyncInterval`: 특별 override 객체가 활성일 때 0, 일반 경로에서는 객체 `+0x338`. `0x930CEB`도 같은 필드를 사용 |
| Present 플래그 | `0x947A40`: sync=0(또는 특별 override), 전역 tearing 지원 바이트 참, 객체 `+0x341` 거짓, `+0x342` 참일 때 `0x200`(PRESENT_ALLOW_TEARING), 그 외 0. `+0x341`의 C++ 원래 필드명은 복원하지 않음 |
| 실제 Present | `0x930C84`에서 전역 `0x221EB10` 로드 → 객체 `+0x320`의 스왑체인. `0x930CF9`에서 vtable `+0x40`(Present)에 `edx=sync`, `r8d=flags` 전달 |
| 메인 BeginFrame | `0xA27680`에서 client `+0x2658`이 0이면 `0xA2768E` → `WaitForPendingPresent(0xA32F30)` |
| Pending 검사 | `0xA32F79`에서 `client+0x2768`이 0이면 대기 없음. 1이면 `worker=client+0x2680`, `worker+0x30`의 신호를 `0xA32FD8` 또는 `0xA3306E`에서 소비. 끝에서 pending 비트와 `+0x276C`를 0으로 정리 |
| 워커 완료 신호 | client `PresentFrame(0xA27A00)`는 pending을 1로 만들고 opcode `0x2776`과 최초 pending 여부를 명령 큐에 기록. 워커 `0x13AD66F`에서 device PresentFrame 가상 함수를 호출하고, **돌아온 뒤** `0x13AD67E`에서 `SignalEvent(0)` 호출. `SignalEvent(0x13A7B30)`는 `worker+0x30` 핸들에 **1개** Release |
| 세마포어 생성 | client 생성자 `0xA18BF9`에서 첫 핸들 저장 위치가 `worker+0x30`. `0xA18C1D` initial=0, `0xA18C26` max=`0x7fffffff`, `0xA18C2F` CreateSemaphoreExW. 이 최대값은 커널 카운트 상한이며 허용 선행 프레임 수가 아님 |
| WaitForSignal | `0x91CC00`: 메인 스레드/설정에 따라 message pump 경로, 아니면 `0x91CC85` `WaitForSingleObjectEx(INFINITE)` |
| maxQueuedFrames | setter `0x89010`은 -1..10으로 제한하여 가상 setter 호출. client `0xA19800` → 렌더 명령, D3D11 `0x930E90`은 장치 `+0x1EAC`, 쿼리/빚과 장치 세마포어 `+0x6750`을 조정. waitable이면 `0x930EC8`에서 `SwapChainWaitingContext::SetMaximumFrameLatencyForWaitableObject(0x9BF2F0)`도 호출 |
| DXGI waitable | 위 함수는 GetFrameLatencyWaitableObject(`+0x108`)와 Get/SetMaximumFrameLatency(`+0x100`/`+0xF8`)를 사용. 입력은 1..16으로 제한. 이 프레임 지연 대기와 worker의 Present 완료 세마포어는 별개 |

기존 WaitSkip은 `0x5D0A62`의 TimeUpdate 분기와 `0x8E815F`/`0x8E8176`의 대기 폴링을 바꾼다. 이번 `0xA2768E`와 두 semaphore wait call은 건드리지 않는다. `GfxProbe.cs`의 기존 RVA는 과거 Unity용이며 이번 시제품은 그것을 재사용하지 않는다. 이번 설치본의 전역과 instruction guard, DLL SHA256을 별도로 검사한다.

문자열에서는 `window-mode`, `force-d3d11-singlethreaded`, `force-d3d11-nothreads`, `force-windows-7-like-d3d11`, `gfx-disable-mt-rendering`, `gfx-enable-gfx-jobs`, `gfx-enable-native-gfx-jobs`, flip/bitblt 항목을 찾았다. bitblt 항목은 위 함수에서 **BootConfig 조회**이므로 모든 문자열이 곧 명령줄 스위치라는 뜻은 아니다. 선택한 ASCII 문자열 검색에서는 독립적인 tearing/latency 명령줄 이름을 발견하지 못했다. 이 결과는 다른 인코딩·숫자 설정·미분석 경로의 부재를 증명하지 않는다. `window-mode` 값별 분기의 전체 소스 의미도 이번 범위에서 복원하지 않았으며, 실제 fullscreen 상태는 DXGI에서 조회한다.

## 원인 가설과 적용 범위

인계 문서의 194.5 FPS·GPU 0.17ms·현재대기 1.50ms는 **Claude의 기존 측정값**이다. 기존 resolved 파일에서 12,114개 표본 중 `WaitForPendingPresent`를 포함하는 줄은 3,530개(약 29.14%)로 확인했다. 표본 비율은 호출 횟수나 CPU 실행 시간 비율로 바꾸어 해석하지 않는다.

가장 직접적인 설명은 “메인이 다음 BeginFrame에서 **렌더 워커의 직전 Present 완료**를 기다린다”이다. 워커가 Present에서 DWM/드라이버/표시 큐 때문에 늦게 돌아오면, GPU가 한가해도 이 신호가 늦는다. 다만 기존 표본만으로 1.5ms 전체를 DWM 합성 비용으로 확정할 수 없다. Present 호출 시간, 표시 모드, 큐/동기화 설정을 같은 창에서 비교해야 한다. 시제품의 `presentLastUs/presentMeanUs`도 드라이버·오버레이·스케줄링을 포함한 wall time이며 GPU busy가 아니다.

- `latency4`: waitable 플래그가 있으면 IDXGISwapChain2, 없으면 IDXGIDevice1의 최대 프레임 지연을 4로 설정. 관찰 결과가 4인지 읽고, off에서 저장값으로 되돌려 재조회한다. **QualitySettings.maxQueuedFrames와 BufferCount는 변경하지 않는다.** DXGI 한도를 분리해 보는 시험이며 worker 프로토콜 자체를 다중 프레임 큐로 바꾸지는 않는다. 원래 DXGI 값이 4라면 no-op으로 거부된다.
- `tearing`: 기존 생성 플래그에 ALLOW_TEARING이 있는 flip/windowed 스왑체인에서, 실제 fullscreen=false, SyncInterval=0인 일반 Present에만 `0x200`을 OR한다. TEST/DO_NOT_WAIT는 보존한다. 엔진이 이미 요청하면 변경 수가 늘지 않아 assert에서 거부한다. **Independent Flip 승격을 보장하지 않는다.** 사용자 tearing 선택을 위한 실험 옵션이며 기본 꺼짐이다.
- 생성 플래그가 없으면 tearing 시제품은 거부한다. ALLOW_TEARING은 ResizeBuffers로 추가·제거할 수 없으며 재생성이 필요하다. 다음 실행의 flip 강제 옵션을 비교할 수 있지만 지원되는 장치에서는 Unity가 이미 같은 플래그를 선택할 수 있다. 이번에는 생성 후크·영구 설정·게임 스왑체인 교체를 추가하지 않았다.
- BufferCount 증가는 Unity의 기존 backbuffer 참조를 해제하고 렌더 타깃을 다시 묶는 작업이 필요하다. 관리되지 않는 ResizeBuffers 강제 호출과 worker semaphore 신호의 임의 선충전은 제외했다. pending 비트를 남기거나 신호가 쌓이면 후속 프레임에서 잘못된 완료 판정이 가능하다.

API 근거: [DXGI 생성 플래그](https://learn.microsoft.com/en-us/windows/win32/api/dxgi/ne-dxgi-dxgi_swap_chain_flag), [tearing 사용 조건](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/variable-refresh-rate-displays), [waitable 프레임 지연](https://learn.microsoft.com/en-us/windows/uwp/gaming/reduce-latency-with-dxgi-1-3-swap-chains). ResizeBuffers에는 기존 ALLOW_TEARING 값을 유지해야 한다.

## 명령·복원·오버레이

구현: [PresentPath.cs](../../PresentPath.cs), [native presentpath.h](../../native/sfnative/presentpath.h). 관리 코드는 csproj의 AUTOTEST 조건 안에만 있고, native에는 호출 전까지 아무 동작도 하지 않는 export 5개를 추가했다.

```text
presentpath state
presentpath on observe
wait 1
presentpath assert
presentpath state
presentpath off
presentpath state
```

`observe`를 `latency4` 또는 `tearing`으로 바꾸면 된다. `on`은 렌더 이벤트를 예약한다. `state`와 `assert`는 렌더 스레드의 새 조회 ACK를 받을 때까지 같은 자동시험 step에서 기다린다. `off` 역시 복원 ACK까지 기다린다. 생성/적용이 거부되면 assert는 예외로 실패하며 성공으로 기록하지 않는다.

상태에는 GetDesc1 HRESULT, 실제 크기·BufferCount·SwapEffect·creationFlags, Windowed, DXGI latency/API, Present 호출 수·SyncInterval·입력/출력 Flags·HRESULT·마지막/평균 소요 µs·변경 수·실패 수, vtable 소유 여부, FrameTiming의 `cpuMainThreadPresentWaitTime` ms와 표본 유효 개수를 출력한다. `currentWaitSamples=0`, 값 -1이면 미수집이다. 이는 마지막 FrameTiming 값이며 전체 창의 평균은 기존 fpsmark 출력으로 확인한다. 원자 카운터들은 실시간 조회값이므로 정밀한 동시 스냅샷으로 간주하지 않는다.

공용 DXGI vtable은 한 번도 수정하지 않는다. 식별된 게임 스왑체인의 **객체 전용 vtable**을 연결하고 당시 Present 체인(Steam 등 포함)을 그대로 호출한다. 표준 IDXGISwapChain~4의 알려진 vtable 길이까지 복사한다. 프레임생성과 상호 배타적이며 native setup에서도 교차 활성화를 거부한다. 스왑체인 교체나 다른 오버레이의 vptr 교체는 assert 실패다.

Main의 끄기·UninstallAll(Unload 경로), 자동시험 EndBatch·Stop에서 Stop을 호출한다. Stop은 즉시 플래그 변경을 비활성화하고 렌더 이벤트에서 latency/연결을 복원한다. **상태 0이 복원 완료**, 2는 대기, -1은 적용 거부, -2는 복원 실패, -3은 오버레이가 vptr을 소유한 상태다. Unload 자체는 메인 스레드를 막지 않고 복원을 예약하므로, 마지막 렌더 이벤트의 실행 여부는 실게임에서 확인해야 한다.

다른 오버레이가 나중에 vptr을 교체한 경우 그 연결을 덮어쓰지 않는다. 이 경우 -3을 보고하고, 재활성화를 막고, 콜백/참조를 보존한 채 pass-through 상태로 남긴다. 이 예외는 완전한 연결 해제 성공이 아니다. 진짜 Steam Overlay·Alt-Tab·모드 재로드 공존 및 unload ACK는 미검증이다.

## 빌드·CPU 전용 검증

| 항목 | 결과 / 로그 |
|---|---|
| AUTOTEST Release | 오류 0, 경고 63. [로그](../../out/present-path/build-autotest.txt) |
| Player Release | 오류 0, 경고 63. [로그](../../out/present-path/build-player.txt) |
| DEV Release | 오류 0, 경고 84. [로그](../../out/present-path/build-dev.txt) |
| sfnative | 빌드 성공. C++ `/W4 /WX`; ISPC scatter 성능 경고 있음. [로그](../../out/present-path/build-native.txt) |
| CPU COM fixture | 통과. 관찰 전달, tearing 조건·기존 플래그, 지연값 변경/복원·실패·no-op, 참조 수, 공유 vtable 불변, 오버레이 takeover의 비활성 pass-through. [결과](../../out/present-path/fixture.txt) |
| 산출물 검사 | 기존 export 55개의 이름·ordinal 보존, 새 export 5개. PresentPath 타입은 AUTOTEST에만 존재하며 Player·DEV에는 없음. [JSON](../../out/present-path/artifact-check.json) |

재현:

```powershell
dotnet build StutterFix.csproj -c Release -p:Edition=Player -p:AutoTestBuild=1
dotnet build StutterFix.csproj -c Release -p:Edition=Player
dotnet build StutterFix.csproj -c Release
& native/build-sfnative.bat 'C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.44.35207' C:\SFBundle\winsdk\microsoft.windows.sdk.cpp\c\Include\10.0.26100.0 C:\SFBundle\winsdk\microsoft.windows.sdk.cpp.x64\c C:\SFBundle\tools\ispc-v1.31.0-windows\bin
& tools/analysis/build-present-path-fixture.ps1
py -3.12 tools/analysis/check-present-path-artifacts.py
```

fixture는 실제 GPU/창/게임/DLL 로딩 없이 가짜 COM 객체만 사용한다. 실제 드라이버 적용, 픽셀 일치, 표시 모드, FPS, tail latency를 검증한 결과는 아니다. `sfnative.def`로 기존 export 이름·ordinal을 고정하고 새 export는 뒤에 추가했다.

## Claude 측정용 CFG2 예시 — 공용 파일에는 아직 반영하지 않음

`bench_v14.py`의 기존 `CFG_MID`, `CFG_POST` 초기화 이후에 넣을 예시다. **설정값과 기존 rlNR 명령을 그대로 복제**하며 측정 직전에 켠다.

```python
for key, mode in [("ppObs", "observe"), ("ppLat4", "latency4"), ("ppTear", "tearing")]:
    CFG2[key] = (dict(CFG2["rlNR"][0]), list(CFG2["rlNR"][1]))
    CFG_MID[key] = [f"presentpath on {mode}", "wait 1",
                    "presentpath state", "presentpath assert"]
    CFG_POST[key] = ["presentpath state", "presentpath assert",
                     "presentpath off", "presentpath state"]
```

Claude가 현재 측정을 마친 뒤 AUTOTEST와 새 native를 함께 배치하고, 자신이 관리하는 실행/복원 절차로 비교할 것. 다음은 **미실행 예시**다.

```powershell
$env:BENCH_WT = 'wt-v14prod'
$env:BENCH_RT = '3440x1440'
py -3.12 E:/StutterFixWork/bench_v14.py pp-latency hl50 ppObs,ppLat4,ppLat4,ppObs --probe
# tearing은 사용자가 허용한 실험에서만:
py -3.12 E:/StutterFixWork/bench_v14.py pp-tearing hl50 ppObs,ppTear,ppTear,ppObs --probe
```

기존 `cpA-hl50-cpCeil.log`의 시작 명령에서 `rtsize 3440 1440`, 창 `2560x1600 FullScreenWindow`를 확인했다. BENCH_RT는 폭·높이를 모두 지정해야 하며 단일 인자 `3440`은 해상도 강제를 해제한다. 기존 비교와 동일한 FullScreenWindow/해상도/주사율/오버레이/장면 조건을 유지한다. 먼저 `rlNR` 대 `ppObs`로 계측 후크 비용을 확인한다. 유효 조건은 양 끝 `accepted <mode>`, 실패 Present 0, 원래 스왑체인 유지, off 이후 status=0·mode=0이며 tearing은 modified>0, latency4는 실제 읽은 값=4여야 한다. 현재 bench 요약 parser는 PresentPath 실패를 자동 판별하지 않으므로 **요약 TSV만 보고 성공으로 채택하면 안 된다**. 실패/no-op 실행은 제외한다.

우선순위는 observe로 실제 생성 플래그/Present 입력과 호출 시간을 확인하는 것이다. 엔진이 이미 tearing을 보내고 DXGI latency도 4라면 두 후보 모두 추가 이득이 없는 것으로 분류한다. 그때는 worker 완료 신호를 임의로 늘리는 대신 렌더 큐·소유권·프레임 자원 수명을 함께 설계해야 한다. 별도 실게임 픽셀/원상복원/Steam·모드변경 검증과 성능 측정 전까지 제품 기본값에는 반영하지 않는다.
