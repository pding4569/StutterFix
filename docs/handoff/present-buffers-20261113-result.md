# Present buffers3 시제품 결과 (2026-11-13)

요청한 **AUTOTEST 전용 `presentpath on buffers3` 구현과 오프라인 검증을 완료**했다. 실게임의 버퍼 3개 적용·FPS·Present 대기 감소·화면 일치는 아직 측정하지 않았다. 인계 문서의 HELLO 수치와 buffers=2 관찰은 Claude의 선행 측정이며, 이번 작업의 측정값으로 재사용하지 않았다.

작업 위치: `E:\StutterFixWork\wt-v14prod`, 브랜치 `claude/adofai-renderer`, 기준 HEAD `e5bad446538ecc73ef1eb878e0a2b9b3c7e6c99d`. 기존 PresentPath 구현은 이 HEAD에 이미 있어 이어서 확장했다. 시작 시 추적 파일 변경은 없었고 기존 미추적 연구 자료는 보존했다. **게임 실행·설치 DLL 교체·커밋·공용 `E:\StutterFixWork\bench_v14.py` 수정은 하지 않았다.** 빌드 산출물은 이 체크아웃 안에서만 갱신했다.

## 구현

- [native presentpath.h](../../native/sfnative/presentpath.h): 기존 객체 전용 vtable에 ResizeBuffers 슬롯 13을 추가로 연결한다. 원래 함수 체인을 호출하며 **BufferCount만 3으로 바꾼다**. Width·Height·Format·Flags는 그대로 전달한다. ALLOW_TEARING 비트가 최초 desc와 다르면 버퍼 수 변경을 거부하고 진단 실패를 기록한다. 공유 vtable은 수정하지 않는다.
- 대상은 실제 GetDesc1을 읽을 수 있는 **원래 버퍼 2개인 flip/windowed 스왑체인**이다. DXGI exclusive fullscreen, 이미 3개인 no-op, 다른 버퍼 수는 거부한다. 원래 수는 조회값으로 저장하며 off 대상도 그 저장값이다. 엔진의 BufferCount=0(현재 수 유지) 요청도 활성 중에는 목표값으로 바꾼다.
- [PresentPath.cs](../../PresentPath.cs): 해상도 요청은 **Unity `Screen.SetResolution(w-1, h, 원래 모드)` → 원래 w,h**의 비동기 왕복이다. 현재 FullScreenWindow 또는 Windowed만 허용하며 모드·주사율 변경 요청은 하지 않는다. 각 단계에서 경과 프레임, Unity 화면 크기, 실제 ResizeBuffers 호출 증가, HRESULT, GetDesc1 버퍼 수, 원래 스왑체인/vtable을 확인한다. 한 단계의 ACK 제한은 코드상 15초다. 무시·실패·교체는 시험 실패다.
- 화면 왕복이 끝나고 설정 복원까지 끝나야 `assert`가 `accepted buffers3`를 낸다. 준비가 끝나면 임시 Update 컴포넌트를 파괴하므로 측정 구간에 왕복용 Update가 남지 않는다.
- off는 즉시 native 목표를 저장된 원래 수로 바꾸고, **동일하게 Unity의 resize 왕복을 요청**한다. 원래 버퍼 수로 돌아올 때까지 vtable/COM 참조를 유지한다. 왕복·설정·원래 스왑체인 확인과 native status=0 ACK까지 `off` step이 기다린다. 적용 전에 off가 들어오는 경우도 처리한다.
- 기존 EndBatch·Stop·모드 disable/unload의 `PresentPath.Stop` 연결을 재사용한다. 복원용 임시 MonoBehaviour는 자동시험 종료/모드 disable 이후에도 왕복을 진행하도록 구성했다. 실제 unload/재로드에서 마지막 이벤트와 Update가 끝나는지는 실게임 확인이 필요하다.
- 실패 시 한 번의 제한된 자동 원복을 시도하며 최초 오류를 보존한다. 원복도 실패하면 성공으로 표시하지 않는다. 버퍼 수가 남거나 overlay가 vptr을 가져가면 기존 규칙대로 참조/콜백을 보존한다. status=2 또는 -3은 완전한 복원이 아니다.

기존 native export **60개 이름·ordinal을 모두 보존**했다. 이번에는 export를 추가하지 않고 기존 렌더 이벤트에 id=6, 통계 배열 뒤에 인덱스 20..27을 추가했다. `PresentPath`, `BufferResize`, `ScreenPrefs` 타입은 AUTOTEST에만 존재한다. Player/DEV의 설정 기본값·기능 연결은 바꾸지 않았다. dormant native 코드는 기존처럼 세 관리 DLL에 내장된다.

## UnityPlayer 역어셈블 근거

재현: `py -3.12 tools/analysis/present-buffers-disasm.py`. [분석 코드](../../tools/analysis/present-buffers-disasm.py), [역어셈블](../../out/present-buffers/disasm.txt). 기존 present-path 분석의 PE RSDS/PDB GUID·age 확인을 재사용한다. 설치 DLL을 파일로 읽었으며 로드하거나 실행하지 않았다. Unity `6000.3.25f1`, DLL SHA256 `7e362b4c6ecee6fb493b1872cda5a5a0caf44f3b131b3c4bad132f0d1309f436`이다. 아래 주소는 RVA다.

| 경로 | 확인한 동작 |
|---|---|
| `Screen_CUSTOM_SetResolution` `0x7DED0` | `0x7DEDA..0x7DEE6`에서 요청 w/h/모드/refresh를 pending 필드에 저장한다. **같은 w/h라고 API 입구에서 버리는 비교는 없다.** |
| `ScreenManagerWin::SetRequestedResolution` `0x805DA0` | pending 항목의 -1 여부를 해석하고 `0x805E89`의 가상 호출로 적용한다. 요청 필드를 되돌린 뒤 진행한다. |
| `ScreenManagerWin::SetResolutionImmediate` `0x8060A0` | 모드·장치별 경로에서 `WinScreenSetup::SetResolution(0x13C37F0)`으로 연결된다. 예: `0x80632B`, `0x8063ED`, `0x8064D7`. |
| `WinScreenSetup::SetResolution` | 장치 enum=2 분기는 `0x13C3DC3..0x13C3DC6`에서 D3D11 블록 `0x13C3F99`로 간다. 체인이 없을 때만 `0x13C4009`에서 CreateForHWND, 존재하면 `0x13C4058`에서 `D3D11SwapChain::Adjust`를 호출한다. |
| `D3D11SwapChain::Adjust` `0x947B80` | `0x947BF2..0x947C04`의 크기 비교는 플래그를 정하며 그 자체로 반환하지 않는다. flip 선택이 달라지면 체인 재생성 분기가 있으므로 runtime 동일성 검사가 필요하다. 기존 flip 경로는 `0x947E77` ReleaseBackbufferResources → `0x947E8D` CreateBackBufferResources. |
| `CreateBackBufferResources` `0x947EC0` | `0x948053`에서 vtable +0x68, 즉 ResizeBuffers 슬롯 13을 로드한다. `0x948057..0x94807D`에서 엔진의 MODE_SWITCH/TEARING/WAITABLE 플래그를 만들고 `0x948089`에서 호출한다. 이후 엔진이 backbuffer/view를 다시 만든다. |

따라서 이 고정 바이너리에서 **동일 해상도 요청이 언제나 no-op이라는 근거는 없고**, 관찰한 정상 D3D11 경로는 Adjust/백버퍼 재구성으로 이어진다. 다만 모든 OS/모드 분기에서 실제 호출 발생까지 정적으로 보장하지 않는다. 시제품은 명시적인 1px 왕복과 호출 ACK를 사용한다. `w+1`은 `WinScreenSetup`의 폭 상한 clamp(`0x13C3AC4..0x13C3ADB`)에 걸릴 수 있어 `w-1`을 쓴다. FullScreenWindow의 물리 스왑체인 크기는 모니터 크기로 유지될 수 있으므로 **Screen.width 변경만으로 ResizeBuffers 적용을 인정하지 않는다**.

백버퍼 참조를 직접 해제하거나 ClearState/ResizeBuffers를 임의 호출하지 않는다. API 요구는 [Microsoft ResizeBuffers 문서](https://learn.microsoft.com/en-us/windows/win32/api/dxgi/nf-dxgi-idxgiswapchain-resizebuffers), 지연된 화면 변경 의미는 [Unity Screen.SetResolution 문서](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Screen.SetResolution.html)를 참고했다. 실제 엔진 경로 판단은 위 설치본 분석에 근거한다.

시작 시 CreateSwapChainForHwnd를 가로채는 대안은 사용하지 않았다. 이 모드가 명령을 받을 때는 게임 체인이 이미 생성되어 있으며, 뒤늦게 factory를 연결해도 기존 체인의 버퍼 수는 바뀌지 않는다. 기존 체인 교체나 Unity worker 세마포어 변경도 하지 않았다.

## Screenmanager 설정 보존

on 이전에 HKCU `Software\{Application.companyName}\{Application.productName}`에서 `Screenmanager ` 접두 값들의 **실제 해시 포함 이름·DWORD 바이트**와 대응하는 PlayerPrefs 캐시의 존재/값을 저장한다. 낯선 타입·해시 없는 이름·접근 실패는 해상도를 바꾸기 전에 거부한다.

왕복 완료 후 PlayerPrefs를 먼저 flush하여 아직 캐시에만 있는 새 Screenmanager 키도 찾는다. 새 키는 캐시/레지스트리 양쪽에서 지우고, 원래 캐시 값을 되돌려 Save한 다음 저장한 raw DWORD를 복원·재조회 대조한다. 다른 이름의 설정은 복원 대상으로 삼지 않는다. 후보 준비 완료 시점과 off 완료 시점 모두 이 절차를 수행한다. **실제 Unity 캐시/레지스트리와 종료 후의 동일성은 아직 미검증**이다. 프로세스 강제 종료 시 메모리 스냅샷으로 복원을 보장하지 않으므로 Claude의 외부 벤치 복원 절차에서도 원래 Screenmanager 값의 존재 여부/형식/값을 보존·대조해야 한다.

## 명령과 유효 조건

```text
presentpath state
presentpath on buffers3
wait 1
presentpath state
presentpath assert
# 측정 구간
presentpath state
presentpath assert
presentpath off
presentpath state
```

`on`은 예약 명령이다. `assert`는 준비 ACK를 기다리며 다음을 검사한다: 실제 GetDesc1 `buffers=3`, resizeCalls>0, resizeFailures=0, resizeRewrites>0, Present failures=0, 원래 스왑체인 포인터/vtable 유지, 원래 화면 크기·모드 복귀. 마지막 `off`는 `status=0 mode=0`, `buffers=originalBuffers`, `resizeDone=True`, `resizeError=none`이어야 한다. 재생성·외부 화면 변경·후크 소유권 상실은 무효다.

상태 줄에 `resizeCalls`, `resizeHR`, `resizeInputCount`, `resizeOutputCount`, `resizeFlags`, `resizeFailures`, `originalBuffers`, `resizeRewrites`, `resizeReady/Done/Error`를 추가했다. 기존 Present HRESULT/실패 수/평균 µs도 유지한다. `presentMeanUs`는 **활성 시작부터의 누적 wall time 평균**이며 준비 resize 구간도 포함한다. 측정 창만 비교하려면 시작/끝 presents·mean의 가중 차이를 사용하고, 창 전체 FPS/현재대기는 기존 벤치 출력을 사용한다. GPU busy나 순수 DWM 시간으로 해석하지 않는다.

## 오프라인 검증 결과

| 항목 | 결과와 로그 |
|---|---|
| AUTOTEST Release | 오류 0 — [로그](../../out/present-buffers/build-autotest.txt) |
| Player Release | 오류 0 — [로그](../../out/present-buffers/build-player.txt) |
| DEV Release | 오류 0 — [로그](../../out/present-buffers/build-dev.txt) |
| sfnative | 성공, C++ `/W4 /WX` 통과. ISPC 기존 scatter 경고는 존재 — [로그](../../out/present-buffers/build-native.txt) |
| CPU COM fixture | PASS: count=0/2→3, 크기/format/flags 전달, 2 복귀 후 참조 해제, resize 실패·복원 재시도, no-op/fullscreen 거부, 공유 vtable 불변, overlay takeover — [로그](../../out/present-buffers/fixture.txt) |
| 실제 BufferResize 소스 추출 fixture | PASS: 비동기 적용/off, 적용 전/도중 off, 적용 실패·해상도 무시 timeout 복구, 체인 교체 거부 — [로그](../../out/present-buffers/pump-fixture.txt) |
| 실제 ScreenPrefs 소스 추출 fixture | PASS: raw 값 복원, 캐시에만 생긴 키 제거, 이후 Save에도 재생성 없음, 다른 설정 보존, 반복 복원, non-DWORD 거부 — [로그](../../out/present-buffers/prefs-fixture.txt) |
| 산출물 검사 | HEAD export 60개 보존, 기존 최초 기준 export 55개 ordinal 보존, AUTOTEST 타입 격리, 세 관리 DLL 모두 최신 native 바이트 내장 — [JSON](../../out/present-buffers/artifact-check.json) |

관리 fixture는 실제 소스의 해당 클래스를 추출해 외부 Unity/DXGI/PlayerPrefs/레지스트리 API만 메모리 대역으로 바꾼다. 실제 Win32 레지스트리나 화면 API를 호출하지 않는다. 이 검증은 실드라이버·Unity 객체 수명·실제 backbuffer/view 재연결·픽셀 일치·Steam Overlay/Alt-Tab·실제 unload ACK 검증을 대신하지 않는다.

재현 순서(native를 먼저 빌드해야 관리 DLL 내장 리소스도 최신이 된다):

```powershell
& native/build-sfnative.bat 'C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.44.35207' C:\SFBundle\winsdk\microsoft.windows.sdk.cpp\c\Include\10.0.26100.0 C:\SFBundle\winsdk\microsoft.windows.sdk.cpp.x64\c C:\SFBundle\tools\ispc-v1.31.0-windows\bin
dotnet build StutterFix.csproj -c Release -p:Edition=Player -p:AutoTestBuild=1
dotnet build StutterFix.csproj -c Release -p:Edition=Player
dotnet build StutterFix.csproj -c Release
& tools/analysis/build-present-path-fixture.ps1
py -3.12 tools/analysis/check-present-buffer-pump.py
py -3.12 tools/analysis/check-present-buffer-prefs.py
py -3.12 tools/analysis/check-present-path-artifacts.py
```

최종 SHA256:

```text
native/sfnative.dll             94713a8f7ee73b180fe4ee4525ebcfa7dc4de5a4db7db5d619f8b1dafb89e213
bin/PlayerAuto/StutterFix.dll   eae5d7f927affb5d28ff465a58fd7748bf324506f93406988bc50419da014f2e
bin/Player/StutterFix.dll       66972d10974266fb7b67f21230cb509e8b48f5379ecdb116efe9e332d4e59069
bin/Release/StutterFix.dll      3cf51ca35bc01d4f5345099fed624bdf0acae56c8d5e132f7da53aa023de1031
```

## Claude용 CFG2 예시와 다음 측정

현재 공용 벤치에 있는 ppObs 정의 **뒤**에 넣을 예시다. 공용 파일에는 반영하지 않았다. rlNR 설정/시작 명령을 복제하고 MID/POST는 ppObs와 같은 패턴으로 둔다.

```python
CFG2["ppBuf3"] = (dict(CFG2["rlNR"][0]), list(CFG2["rlNR"][1]))
CFG_MID["ppBuf3"] = ["presentpath on buffers3", "wait 1",
                     "presentpath state", "presentpath assert"]
CFG_POST["ppBuf3"] = ["presentpath state", "presentpath assert",
                      "presentpath off", "presentpath state"]
```

미실행 예시:

```powershell
$env:BENCH_WT = 'wt-v14prod'
$env:BENCH_RT = '3440x1440'
py -3.12 E:/StutterFixWork/bench_v14.py pp-buffers hl50 ppObs,ppBuf3,ppBuf3,ppObs --probe
```

선행 HELLO와 같은 FullScreenWindow/해상도/주사율/오버레이/장면 조건을 유지한다. 먼저 짧은 왕복에서 accepted buffers3와 off ACK·정확한 설정 복원을 확인한 뒤 ABBA를 수행한다. 준비 resize가 벤치 FPS 창에 들어가지 않도록 assert 뒤에 기존 측정 시작점이 오는지 확인한다. 실패 판을 성능 표본에 섞지 않는다. 그림·순서 보존 및 실제 Present/FPS/tail 비교를 마치기 전에는 제품 기본값으로 채택하지 않는다.
