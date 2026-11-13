# Present buffers3 실패 경로 수정 결과 (2026-11-13)

**최신 상태(2026-11-14 추가 인계 수행): 문서 마지막의 「OnAfterPresent 동기화 후속 수정」이 현재 구현이다.** 아래 기존 기록의 렌더 이벤트 안 즉시 Adjust 호출은 폐기했고, 해시·검증 로그도 마지막 절의 값으로 갱신했다. 기존 수정에서 이어서 작업했으며 게임 실행·설치 DLL 교체·커밋은 하지 않았다.

2026-11-14 이어서 검토·검증했다. 기존 구현을 보존하고, 적용 실패 뒤 자동 원복이 성공한 경우에도 후속 assert/off 없이 `status=-1 failed`를 출력하도록 보완했다. 두 복원 완료 경로에 공통 완료 처리를 적용하고, HRESULT 실패 및 무응답 timeout 후 원복 fixture에서 실패 줄 출력을 확인했다. native 및 관리 3종을 다시 빌드했고, COM/pump/prefs fixture·export/내장 리소스 검사·설치본 PE/PDB 정적 분석을 다시 통과했다. 아래 로그와 해시는 이번 최종 산출물로 갱신했다. 게임 실행·설치 DLL 교체·커밋은 이번에도 하지 않았다.

기존 HEAD `66dee59`의 시제품에서 이어서 구현·오프라인 검증을 완료했다. 작업 위치는 `E:\StutterFixWork\wt-v14prod`, 브랜치는 `claude/adofai-renderer`다. **게임 실행·설치 DLL 교체·커밋·공용 bench_v14.py 수정은 하지 않았다.** 일반 제품 기본값은 변경하지 않았다. 실게임 적용, FPS, 픽셀 일치, 실제 복원은 미측정이다.

핵심 변경은 1px Screen.SetResolution 왕복과 GL.Flush를 제거하고, 현재 크기의 Unity `D3D11SwapChain::Adjust`를 렌더 이벤트에서 호출하는 것이다. 실패 반환은 진단으로 처리하며 ScreenManager의 치명적 오류 UI로 전달하지 않는다. 같은 방아쇠를 쓰는 `resizeprobe`, 원래 ResizeBuffers HRESULT 보존·fallback, 독립 watchdog을 추가했다.

## 실패 원인: 정적으로 확인한 부분

분석 대상은 설치본을 파일로만 읽은 UnityPlayer `6000.3.25f1`, SHA256 `7e362b4c6ecee6fb493b1872cda5a5a0caf44f3b131b3c4bad132f0d1309f436`다. PE RSDS와 PDB GUID/age 대조는 기존 분석기를 재사용했다. 아래 주소는 RVA다. [분석 코드](../../tools/analysis/present-buffers-disasm.py), [최종 역어셈블](../../out/present-buffers2/disasm.txt).

| 근거 주소 | 확인한 동작과 의미 |
|---|---|
| `0x947BA4..0x947BD0` | Adjust 대상이 전역 주 체인 설정 객체인 경우 GfxDevice의 `+0x5E68`, `+0x5E70` 카운터를 비교한다. 다르면 `0x947DB6`으로 가서 false를 반환한다. ResizeBuffers보다 앞이다. 필드 이름은 PDB 타입으로 확정하지 않았으며, 이전 Present 분석과 함께 pending-present 동기화 가드로 해석한다. |
| `0x947C2E..0x947C39` | flip 선택이 기존 상태와 다르면 체인 재생성 경로다. 이 경로의 생성 실패도 Adjust=false가 될 수 있다. |
| `0x947E77`, `0x947E8D`, `0x947E92` | 기존 체인 경로는 ReleaseBackbufferResources → CreateBackBufferResources → true 반환이다. |
| `0x948089..0x9480AF` | ResizeBuffers 호출 직후 반환 HRESULT에 대한 성공/실패 분기가 없고, waitable latency 설정과 view 재생성으로 진행한다. 따라서 후크의 HRESULT만으로 이번 `Switching ... failed` 로그를 설명할 수 없다. |
| `0x13C4058..0x13C405F` | WinScreenSetup::SetResolution이 Adjust를 호출하고 bool 결과를 검사한다. |
| `0x13C40C8..0x13C40DA` | exclusive/stereo 재시도 여지가 없으면 문자열 `Switching to resolution %dx%d failed`를 AddErrorMessage에 넣고 false를 반환한다. **이 문자열 자체는 OS 디스플레이 모드 전환 실패의 증거가 아니다.** |
| `0x80633F..0x806342`, `0x8069DE..0x806A0C` | D3D11 SetResolution 실패는 `Screen: DX11 could not switch resolution ...`를 추가한 뒤 DisplayErrorMessagesAndQuit로 간다. |
| `0xA4C5B4..0xA4C5BE` | DisplayErrorMessages를 호출한 뒤 exit(1)한다. |
| `0xA4C578`, `0xA4C599` | DisplayErrorMessages 내부는 DialogBoxParamW 또는 MessageBoxA로 이어진다. 모달 창이 닫히기 전에는 메인 Update/자동시험 단계가 진행되지 않을 수 있다. |

기존 실게임 로그의 두 오류 문자열은 위의 **치명적 오류 표시 후 종료 경로 진입**과 일치한다. 따라서 15초 Update 타임아웃이 나오지 않은 이유를 설명하는 강한 근거는 모달 오류 표시 대기다. 당시 스택/덤프가 없어 실제 대기 지점은 확정하지 않는다. 추가 Present/Wait 불일치 경고와 기존 Issue의 GL.Flush는 동기화 가드 실패 가설과 맞지만, 인과관계를 실측한 것은 아니다.

기존 flip 경로에서 Adjust는 ResizeBuffers HRESULT를 확인하지 않고 true를 반환한다. 그러므로 **“BufferCount=3의 HRESULT 실패 때문에 저 해상도 오류가 났다”는 설명은 현재 증거로 지지되지 않는다.** 당시 HRESULT는 수집되지 않았다. 실제 디스플레이 모드 전환을 시도했는지도 해당 문자열만으로 확정할 수 없다.

## 대체 방아쇠와 선택 근거

동일 w/h/모드의 Screen.SetResolution도 API 입구에서 버리지 않으며, 기존 체인에서는 `0x13C4058`의 Adjust로 진행할 수 있다. `AdjustWindowForModeChange`의 `0x13C27D9..0x13C27F1`에는 같은 모드·크기이고 강제 비트가 없으면 창 조정을 건너뛰는 분기도 있다. 그러나 이 API는 여전히 Adjust=false를 치명적 오류 UI로 보내는 호출자다. refresh 변경도 같은 호출자를 통과한다.

WM_SIZE를 흉내 내는 방법도 선택하지 않았다. `HandleWindowResize`는 FullScreenWindow 관련 `0x80544A..0x8054C2`에서 SetResolutionImmediate를 다시 호출할 수 있다. Windowed로 전환하는 시험은 사용자의 원래 모드와 다른 실험이 된다.

이번 구현은 후보 (d), **Unity 내부 Adjust의 정상 기존 체인 경로**를 렌더 이벤트 id=8에서 호출한다.

- 관리 측 Resolve의 Unity 버전·전체 DLL SHA256·기존 RIP 명령 확인을 유지했다. native도 Adjust 진입 바이트와 전역 설정 객체의 실제 스왑체인 포인터·소유 vtable을 확인한다.
- Screen 크기와 실제 desc 크기가 같아야 한다. native에서 GetDesc, non-exclusive/windowed, 실제 client rect와 크기의 일치를 다시 확인한다. 외부 화면 변경이나 크기 불일치는 거부한다.
- 인자는 현재 w/h를 논리/백버퍼 크기 양쪽에 그대로 전달한다. viewport 16바이트와 마지막 엔진 옵션은 설정 객체에서 복사한다. viewport 임시 버퍼는 엔진의 movaps 읽기에 맞춰 16바이트 정렬한다. refresh는 HandleWindowResize와 같은 0/1, exclusive=false다.
- `ShouldResizeTargetWindow`는 format/fullscreen/client-size가 같으면 false로 돌아간다(`0x9BE56A..0x9BE5C5`, `0x9BE771..0x9BE791`). 따라서 현재 상태가 유지되는 정상 경로는 ResizeTarget/SetFullscreenState 호출을 건너뛴다. 창·모드·주사율 변경 요청은 별도로 하지 않는다.
- Adjust의 pending 가드가 거부하면 `adjustResult=0`으로 시험 실패 처리한다. 체인 교체/가드 실패는 `adjustResult=-1`이다. 무한 재시도하지 않는다. 거부되면 다음에 동기화 구간을 따로 설계해야 하며, 가드를 패치하거나 세마포어를 조작하지 않는다.
- 백버퍼 해제·view 재생성은 Unity가 수행한다. 플러그인이 ClearState나 자체 자원 소유 상태로 ResizeBuffers를 직접 실행하지 않는다. 후크는 Unity가 실제로 호출한 ResizeBuffers 인자 중 count만 바꾼다.
- Issue에서 GL.Flush를 제거했다. 설치·트리거·복원은 큐에 넣고 후속 프레임에서 ACK를 확인한다. 트리거 반환 뒤 Present 카운터를 새로 기준 삼아 그 이후 Present 증가까지 확인해야 ready가 된다.

**고정 바이너리의 내부 ABI를 쓰는 연구 경로다.** 오프라인 fixture는 인자 구성·정렬·상태 전이를 검증하지만, Unity가 플러그인 렌더 콜백 안에서 이 함수를 재진입시켜도 안전한지는 실게임에서 확인해야 한다. 게임 내부 잠금이나 드라이버 호출의 무한 대기를 오프라인 검증으로 배제할 수 없다.

## HRESULT 안전망과 진단

[native 코드](../../native/sfnative/presentpath.h)의 통계는 append-only로 확장했다.

| 인덱스/상태 필드 | 의미 |
|---|---|
| 20 resizeCalls | 후크 진입 시 증가. 호출이 막혀도 진입 여부를 알 수 있다. |
| 21 resizeHR | 첫 실제 ResizeBuffers의 반환 HRESULT. fallback/describe 전에 기록한다. |
| 22/23/24 | Unity 입력 count / 첫 시도 출력 count / flags |
| 25 resizeFailures | HRESULT 실패 또는 rewrite 가드 거부. fallback 성공이어도 실패가 남는다. |
| 26/27 | 원래 버퍼 수 / 성공한 count rewrite 수 |
| 28/29 fallbackHR/fallbackCalls | 같은 w/h/format/flags와 **Unity 원래 입력 count**로 재시도한 결과/횟수. count=0도 그대로다. |
| 30/31/32 triggerCalls/adjustResult/triggerPhase | 엔진 트리거 횟수 / -1 거부·0 false·1 true / 1 진입·2 반환 |

count를 바꾼 첫 호출이 실패하면 원래 count로 즉시 한 번 재시도한다. 성공하면 실제 성공 HRESULT를 Unity에 반환하고 후보는 실패로 남긴다. fallback도 실패하면 그 실패 HRESULT를 그대로 반환한다. 성공을 꾸며 반환하지 않는다. 이후 목표는 저장된 원래 count로 바꿔 추가 후보 rewrite를 막는다.

`presentpath on resizeprobe`는 id=7로 같은 Present/ResizeBuffers 관찰 후크와 같은 id=8 방아쇠를 쓴다. bufferTarget=0이므로 count/flags/나머지 인자를 그대로 전달한다. assert는 원래 count 유지, resizeCalls 증가, rewrite=0, 실패=0, 트리거 이후 Present 증가를 요구한다. probe의 off는 버퍼 변경이 없으므로 후크를 해제하고 설정 스냅샷을 대조한다.

## 실패 제한과 복원 범위

[관리 코드](../../PresentPath.cs)는 적용·복원 전 큐 로그와 엔진 반환 로그를 남긴다. 각 ACK 구간은 Stopwatch 기준 15초 제한이며, 적용 실패 뒤 복원 시도는 한 번이다. 진행 가능한 실패는 원래 count 복원·prefs 복원을 시도한 뒤 `status=-1 ... failed`를 출력한다. 자동 원복이 성공해도 `status=-1 failed: ...; rollback ACK received; preferences restored`를 즉시 출력하며 후속 명령에 의존하지 않는다. 원래 오류는 assert/off에서 계속 실패로 남는다. 복원 실패 때 후크/COM 참조를 성공처럼 해제하지 않는다.

독립 System.Threading.Timer watchdog은 메인 Update 없이도 제한을 검사한다. 만료되면 native의 원자적 목표 취소를 호출하고 다음 파일에 실패 줄을 먼저 기록한 뒤 UMM 로그에도 기록한다.

```text
<Main.Entry.Path>/presentpath-watchdog.txt
[presentpath] status=-1 failed watchdog ACK timeout; main/render progress unavailable; restoration pending ...
```

watchdog 스레드에서 Unity/COM/레지스트리 복원 API를 호출하지 않는다. 타이머 해제와 취소는 잠금으로 직렬화하여 이전 lease의 늦은 타이머가 다음 lease를 취소하는 것을 막는다. ready에서는 타이머와 Update 드라이버를 제거하며 off 전에 다시 활성화한다.

**“어떤 실패에도 게임이 절대 멈추지 않고 15초 안에 실제 복원 완료”는 보장하지 못한다.** Unity/드라이버 내부가 막히면 그 스레드에서만 가능한 자원 복원은 진행할 수 없다. watchdog의 원자적 취소는 실제 count 복원 ACK가 아니다. UMM 로깅까지 막히는 경우를 위해 sidecar를 먼저 기록했으므로, Claude 외부 벤치도 이 파일의 새 `status=-1` 줄을 감시해야 900초 대기를 독립적으로 끊을 수 있다. 타이머 스케줄링 때문에 출력 시점이 정확히 15.000초라고 보장하지도 않는다.

실게임 미실행이므로 실제 Screenmanager 캐시·레지스트리 동일성도 아직 미검증이다. 기존 raw 값·캐시 스냅샷 복원 코드를 유지했다. 외부 벤치의 원본 설정 보존·복원·대조는 계속 필요하다. timeout/복원 미완료 판은 성능 표본에서 제외한다.

## 오프라인 검증

| 항목 | 최종 결과 |
|---|---|
| native | 성공. C++ /W4 /WX 통과. 기존 ISPC scatter 경고는 남음. [로그](../../out/present-buffers2/build-native.txt) |
| AUTOTEST Release | 오류 0. [로그](../../out/present-buffers2/build-autotest.txt) |
| Player Release | 오류 0. [로그](../../out/present-buffers2/build-player.txt) |
| DEV Release | 오류 0. [로그](../../out/present-buffers2/build-dev.txt) |
| 실제 native 헤더 COM fixture | PASS. 첫 HRESULT·fallback 성공/실패, count=0 보존, probe count 불변, Adjust 인자/viewport 정렬/identity/false 거부, 복원·참조·overlay 경로. [로그](../../out/present-buffers2/fixture.txt) |
| 실제 BufferResize 추출 fixture | PASS. 적용/off, 초기/큐 중 off, 실패 복구, 무응답 timeout, probe, Adjust 거부, 크기 가드, 메인 Pump 없이 watchdog 호출, 체인 교체 거부. Screen.SetResolution은 호출 시 예외가 나도록 대역을 구성했다. [로그](../../out/present-buffers2/pump-fixture.txt) |
| 실제 ScreenPrefs 추출 fixture | PASS. raw 값 복원·캐시에만 생긴 키 삭제·이후 Save 안정성·다른 키 보존·반복 복원·non-DWORD 거부. [로그](../../out/present-buffers2/prefs-fixture.txt) |
| 산출물 | HEAD export 60개 이름/ordinal 전부 보존. AUTOTEST 타입 격리. 관리 DLL 세 종류에 최종 native 바이트 내장. [JSON](../../out/present-buffers2/artifact-check.json) |
| diff | git diff --check 통과 |

fixture의 Unity/COM/시계는 대역이다. watchdog 시험은 가상 시간을 진행시킨 뒤 실제 콜백을 직접 호출한 기능 검사이며, 실제 타이머 지연·교착 게임의 탈출 시간을 측정한 것이 아니다. native fixture는 실제 인자 구성 helper를 가짜 Adjust에 연결한다. Unity 내부 함수 본체와 OS client-rect preflight를 실행하지 않는다.

재현 순서는 이전 결과 문서의 native 빌드 명령을 사용한 뒤 다음과 같다.

```powershell
dotnet build StutterFix.csproj -c Release -p:Edition=Player -p:AutoTestBuild=1
dotnet build StutterFix.csproj -c Release -p:Edition=Player
dotnet build StutterFix.csproj -c Release
& tools/analysis/build-present-path-fixture.ps1
py -3.12 tools/analysis/check-present-buffer-pump.py
py -3.12 tools/analysis/check-present-buffer-prefs.py
py -3.12 tools/analysis/check-present-path-artifacts.py
py -3.12 tools/analysis/present-buffers-disasm.py
```

최종 SHA256은 [hashes.json](../../out/present-buffers2/hashes.json)에 저장했다.

```text
native/sfnative.dll            e7e0581faae48a9c436fc6ea639b59230eea8ae6c39664635a87098874ceee9e
bin/PlayerAuto/StutterFix.dll  ceaf811a33ee4be46460ebb73d4c53984c63d45f6cda110c689a5dcab959d2a4
bin/Player/StutterFix.dll      021ed4b401877178cb19f999f9f32d1783f425d668d9ea9aa94860004d6f0cf0
bin/Release/StutterFix.dll     ac0b75b68880bdb921c863b1a3af335351fadaa193b67116a4f19b8d5593b2de
```

## Claude 측정용 CFG2

공용 벤치에는 반영하지 않았다. 기존 ppObs 정의 뒤에 넣는 예시다.

```python
CFG2["ppProbe"] = (dict(CFG2["rlNR"][0]), list(CFG2["rlNR"][1]))
CFG_MID["ppProbe"] = ["presentpath on resizeprobe", "wait 1",
                       "presentpath state", "presentpath assert"]
CFG_POST["ppProbe"] = ["presentpath state", "presentpath assert",
                        "presentpath off", "presentpath state"]
```

먼저 외부에서 sidecar의 시작 파일 길이/오프셋을 저장한다. 실행 중 그 오프셋 뒤에 새 `status=-1` 줄이 생기면 기존 INVALID 판과 동일하게 실패 처리한다. 이전 실패 줄로 새 판을 오판하지 않는다. main/render가 막힌 경우엔 실제 복원이 미완료일 수 있으므로, 실패 감지 후 기존 외부 복원 절차의 결과를 반드시 확인한다.

1. 동일 HELLO/FullScreenWindow 조건에서 ppProbe 단독으로 트리거 도달·HRESULT·후속 Present·off 상태를 확인한다. `adjustResult=0/-1`, 호출 0, 오류, watchdog 발생 판은 실패다.
2. ppProbe가 통과해야 ppBuf3 짧은 적용/off를 진행한다. buffers3 assert는 count=3·resizeFailures=0·rewrite 증가를 요구한다. off는 nativeStatus=0·원래 count·화면·prefs 복원을 요구한다.
3. 기능 복원을 확인한 뒤에만 ppObs/ppBuf3 ABBA를 수행한다. 준비 단계는 FPS 측정 창 밖에 둔다. 실제 픽셀/정렬·FPS·tail·종료 후 설정 복원을 별도로 확인한다.

새 방아쇠의 실제 성공률과 성능 이득은 아직 측정하지 않았다. 제품 채택 근거는 없다.

## OnAfterPresent 동기화 후속 수정 (2026-11-14)

추가 인계의 `resizeprobe: adjust=0, resizeCalls=0`에 대응했다. 기존 HRESULT fallback·probe·설정 복원·watchdog·실패 시 `status=-1` 출력은 보존했다. **렌더 이벤트 id=8은 이제 Adjust를 실행하지 않고, 원래 Unity OnAfterPresent가 반환한 직후 한 번 실행하도록 예약한다.** 기본값·일반 제품 호출 경로는 변경하지 않았다.

### 두 카운터와 호출 시점의 정적 근거

같은 설치 UnityPlayer SHA256 `7e362b4c6ecee6fb493b1872cda5a5a0caf44f3b131b3c4bad132f0d1309f436` 및 PE/PDB GUID·age를 재검증했다. [분석기](../../tools/analysis/present-buffers-disasm.py)는 전체 실행 섹션에서 두 필드 displacement를 검색하고, 해당 심볼 시작부터 디코딩하여 실제 참조 명령을 출력한다. [최신 역어셈블](../../out/present-buffers2/disasm.txt) 마지막에도 전체 참조 목록을 보존했다. 아래 주소는 RVA다.

| 주소 | 확인한 동작 |
|---|---|
| `0x92552E`, `0x925535` | GfxDeviceD3D11Base 생성자가 두 필드를 초기화한다. |
| `0x93075F`, `0x930771`, `0x930810` | WaitForLastPresentationAndGetTimestamp가 A=`+0x5E68`, B=`+0x5E70`를 비교한다. 일반 경로에서 A를 `lock inc`한다. XR 관련 분기에서는 이 증가를 건너뛸 수 있다. |
| `0x930DCB..0x930E45` | 실제 D3D11 OnAfterPresent는 A>B인지 확인하고, 그렇지 않으면 Wait 없이 Present했다는 경고 경로를 거친다. 양쪽 경로 모두 `0x930E45`에서 B를 `lock inc`한다. |
| `0x930E4D..0x930E7B` | B 증가 뒤 XR/체인 상태에 따라 PushEventQuery를 수행한 뒤 반환한다. 이번 후크는 이 원래 함수 전체가 반환한 뒤 실행한다. |
| `0x930CF9`, `0x930D25..0x930D2B` | GfxDeviceD3D11::PresentFrame은 DXGI Present를 호출한 **뒤에** 가상 슬롯 `+0xBD8`의 OnAfterPresent를 호출한다. 따라서 DXGI Present 후크에서 원래 Present 반환 직후 Adjust를 실행해도 B 증가 전이다. |
| `0x947BBD..0x947BD0` | Adjust가 전역 device의 A/B를 비교하고 불일치면 false 반환한다. |
| `0x1E3BCD0..0x1E3C8B0` | PDB의 실제 GfxDeviceD3D11 primary vtable. 380개 함수 슬롯을 PE 실행 섹션 주소와 대조했다. `+0xBD8`은 `0x930DC0`이다. 다음 심볼은 secondary vtable이다. |

A는 **presentation wait 경로의 진행 횟수**, B는 **OnAfterPresent 호출 횟수**다. GPU fence 완료 횟수나 화면에 실제 표시된 횟수로 해석하지 않는다. 정상적인 wait/Present 한 쌍에서 B가 증가한 뒤 A=B가 되는 것이 정적 근거다. 이전 실게임 렌더 이벤트가 A=B+1이었는지는 당시 두 값을 수집하지 않아 직접 확정하지 않는다. 이번에는 실제 Adjust 호출 직전 값을 수집한다.

### 현재 구현

- AUTOTEST의 기존 바이너리 전체 SHA256 고정을 유지한다. id=8에서 전역 device의 vptr이 **정확히 원래 primary vtable**인지 확인한 경우만 예약한다. 다른 모드·다른 vptr은 거부한다.
- 객체 전용 primary vtable 사본 380슬롯과 앞의 RTTI 위치 1슬롯을 보존하고, 사본의 OnAfterPresent 슬롯만 바꾼다. 공유 vtable, Unity 명령 바이트, 카운터, 세마포어는 수정하지 않는다.
- 콜백은 원래 OnAfterPresent를 먼저 호출한다. 그 뒤 자기 사본 소유권을 확인하며 원래 vptr을 되돌리고, 전역 device 동일성을 다시 확인한다. **원래 vptr 복원 뒤에만** 같은 크기의 Adjust를 한 번 호출한다.
- Adjust 직전에 `adjGuardA/B`를 채집한다. 불일치면 Adjust를 호출하지 않고 실패 ACK를 남긴다. 동일성·실제 크기·client rect·windowed/exclusive·스왑체인 소유권·Adjust 진입 바이트 가드는 유지한다. 카운터가 같아질 때까지 기다리거나 재시도하지 않는다.
- off/독립 watchdog은 예약을 원자적으로 취소한다. 후속 렌더 off 이벤트 또는 다음 OnAfterPresent 콜백이 객체 vptr을 복원한다. watchdog 스레드가 직접 Unity vptr·COM을 건드리지는 않는다. 원래 버퍼 수가 남으면 원복 예약도 같은 OnAfterPresent 시점을 쓴다.
- 다른 후크가 device vptr을 가져가면 덮어쓰지 않고 `status=-3`으로 거부한다. 그 후크가 우리 콜백을 보존할 가능성이 있으므로 사본과 원래 호출 포인터를 유지한다. 이를 복원 성공으로 취급하지 않는다.

추가 통계는 append-only다. `33=adjGuardA`, `34=adjGuardB`, `35=afterPresentCalls`이며 state와 trigger 반환 로그에 출력된다. `triggerPhase=1`은 예약 중, `2`는 반환/거부 ACK다. 예약 거부나 취소에서는 A/B가 새로 채집되지 않을 수 있으므로 A/B만으로 성공을 판정하지 않는다.

### 이번 최종 검증 및 산출물

| 항목 | 결과 |
|---|---|
| native 빌드 | 성공. C++ `/W4 /WX` 오류 0. 기존 ISPC 경고는 남음. [로그](../../out/present-buffers2/build-native.txt) |
| AUTOTEST / Player / DEV | 각각 오류 0. 기존 경고 63 / 63 / 84개. [AUTOTEST](../../out/present-buffers2/build-autotest.txt), [Player](../../out/present-buffers2/build-player.txt), [DEV](../../out/present-buffers2/build-dev.txt) |
| 실제 native 헤더 fixture | PASS. 지연 1회 호출·원래 OnAfterPresent 선행·동일 카운터·vptr 복원 후 Adjust·전체 슬롯/RTTI 보존·취소·조기 off·3→2 복원·외부 vptr 충돌/재예약 거부를 추가 검증했다. 기존 COM/HRESULT/fallback 시험도 통과. [로그](../../out/present-buffers2/fixture.txt) |
| 실제 관리 소스 추출 fixture | Pump·prefs PASS. 통계 대역을 36칸으로 갱신했다. [Pump](../../out/present-buffers2/pump-fixture.txt), [prefs](../../out/present-buffers2/prefs-fixture.txt) |
| export/내장 native/격리 | HEAD의 60개 export 이름·ordinal 보존. 3종 DLL에 최종 native 내장. PresentPath/BufferResize/ScreenPrefs는 AUTOTEST만 포함. [JSON](../../out/present-buffers2/artifact-check.json) |
| 정적 분석·diff | PE/PDB identity, 380슬롯 주소 대조, 카운터 참조 분석, `git diff --check` 통과 |

fixture는 Unity·COM 대역으로 호출 순서와 상태를 확인한다. 실제 Unity 함수의 재진입 안전성, 드라이버 반응, 실제 타이머 지연, 게임 화면/설정 복원, FPS는 이번에 측정하지 않았다. 원래 OnAfterPresent나 Adjust/드라이버가 막히면 실제 자원 복원은 진행되지 않을 수 있다. 기존 sidecar 감시 및 실패 판 제외 조건은 그대로 필요하다.

최종 [hashes.json](../../out/present-buffers2/hashes.json):

```text
native/sfnative.dll            f35ec5c6970b96fe141aeba45c29f15c402d19e2991dc3075b69d8c877d1b145
bin/PlayerAuto/StutterFix.dll  df939e3500eabecb8bbd85e60e03c90af50bc0fbe124ef7437534376b5b33d69
bin/Player/StutterFix.dll      f70fe27874f393f189c0fa4e68e55694467936b3dc4a5c5e101ce0749091a86c
bin/Release/StutterFix.dll     2aa022936a0553e14f627fdf85f58d79fa7d9c78561ee4414a6679a24cc06564
```

재현 빌드·fixture 명령은 앞 절과 같다. 공용 벤치와 CFG2는 수정하지 않았다. Claude는 앞 절의 `ppProbe`를 같은 HELLO/FullScreenWindow 조건에서 먼저 실행한다. 새 로그에서 `afterPresentCalls` 증가, `adjGuardA==adjGuardB`, `adjust=1`, `resizeCalls` 증가, `resizeHR>=0`, count 불변, 후속 Present 증가 및 정상 off를 확인한다. 이 대조군이 통과한 뒤에만 buffers3의 3개 적용·2개 복원과 성능 측정으로 진행한다. **실게임 성공 또는 성능 개선을 완료로 보고하지 않는다.**
