# 판정 오차 막대의 투명 Image 등록 해제 후보 — 작업 결과

> **Claude 게임 확인(2026-11-23) 뒤 접음.** HELLO 게임 화면 자동 플레이 20초: `hidden=0`(눈금 60개가 늘 보임: 판정이 초당 20번 넘게 와서 3초 수명이 끝나기 전에 다시 쓰임). 지운 것이 없으니 이득도 없다. 막대는 `ScreenSpaceOverlay` 캔버스라 **framecmp 캡처(카메라 그림)에는 안 찍힌다**: 아래 verify 절차의 A/B 0픽셀은 막대를 비교한 것이 아니다. 막대 픽셀 비교는 `shot`(실제 화면)으로 해야 한다. 에디터 재생 경로의 캡처에도 막대가 없었다. 진단 명령 `meterinfo` 를 남겼다(캔버스·그룹 투명도·막대 위치·보이는 그림 수). 후보 코드는 저장소에서 빼고 `E:\StutterFixWork\patches\hitmeter-batch-20261123.patch` + `HitMeterBatch.cs` 로 보관했다.
>
> 남은 비용(막대 끔 상한 +3.3%)은 보이는 눈금 60개의 투명도가 매 프레임 바뀌어 캔버스가 다시 묶이는 것이다. 줄이려면 투명도 변화를 셰이더(시각 기반)로 옮기는 식이어야 하고, 정점 색 양자화까지 같게 만들어야 한다.

작성일: 2026-11-23. 작업 폴더: `E:\StutterFixWork\wt-v14prod`.
브랜치: `claude/v3-282port`, 기준 HEAD: `28ec066254aaf09625046b782c2b02ab27a4c099`.
요청 원본: [hitmeter-batch-20261123.md](hitmeter-batch-20261123.md).

## 결과와 검증 범위

후보 B를 구현했다. **`HitMeterBatch=false`가 기본값**이며, 완전히 투명한 눈금 Image만 잠시 비활성화해 uGUI 등록에서 빼고, 같은 눈금이 다시 보일 때 복구한다. 메시를 합치지 않는다. 기존 `HitMeterFade`의 실수 렌더러 알파, 원래 게임의 트윈·색·좌표·스프라이트·외곽선 판단·재사용 순서는 유지한다.

빌드 2종과 LoadCheck는 오류 0이다. 새 오프라인 검사 37개가 통과했고 준비 후 재사용 100,000회에서 관리 힙 할당 0바이트였다. 기존 Fade/직접 Kill 회귀 검사도 통과했다. **게임은 실행하지 않았으므로 실제 픽셀 일치, Unity/Mono에서의 할당, FPS·메인 스레드 개선은 미확인**이다. 0.1ms를 줄였다고 판정하지 않는다.

이 작업에서는 게임 실행·종료, 설치 파일 복사·교체, 커밋·푸시를 하지 않았다. 디컴파일한 게임 소스는 저장소 파일로 저장하지 않았다. 아래 게임 시험은 Claude가 이후 수행할 절차이며 이번 실행 결과가 아니다.

## 코드·프리팹 조사와 병목 해석

설치된 `Assembly-CSharp.dll`의 `scrHitErrorMeter`, `ADOFAI.ErrorMeterTick`을 ILSpy CLI의 표준 출력으로 읽었다. ILSpy MCP는 이 세션의 승인 정책상 호출되지 않아 설치된 CLI를 사용했다. 기존 `tools/optimization/HitMeterDirectCheck`의 게임 IL 검사로 호출 형태도 확인했다.

- `Awake`: `tickCacheSize=60`개의 눈금과 트윈 ID를 캐시한다. `tickLife` 기본값은 3초다.
- `AddHit`: 판정 오차·평균 바늘 위치를 계산하고 곡선/직선 경로를 선택한다. 협동 모드의 색 덮어쓰기와 수명 배율 0.25도 원래 경로에 있다.
- `DrawCurvedTick`/`DrawStraightTick`: 캐시의 순환 인덱스를 사용해 회전 또는 위치를 정하고 `ErrorMeterTick.Show`를 호출한다. 이 계산과 순서를 수정하지 않았다.
- `Show`: 기존 채움/외곽선 트윈을 종료하고 색을 정한다. 채움은 항상 페이드하며, 외곽선 페이드는 XPerfect 표시 조건에서만 만든다. 사용하지 않는 외곽선의 색 알파는 0이다. 원래 OnKill은 채움 또는 외곽선 색을 알파 0으로 지운다.
- `Reset`: 원래 트윈 종료·눈금 좌표·바늘 초기화를 유지한다. `UpdateLayout`의 크기·모양·스프라이트 변경과 그 안의 Reset도 유지한다.
- 기존 `HitMeterFade`: Image 색의 매 프레임 변경을 CanvasRenderer 알파 변경으로 대체한다. 따라서 메시 재생성 감소 후에도 활성 Graphic 등록, CanvasRenderer 변경, 캔버스 배치 갱신과 페이드 콜백 비용이 남는다.

프리팹은 기존 `out/asset-reader-deps`의 UnityPy를 사용해 설치 에셋을 읽기 전용으로 조사했다. `resources.assets`의 Error Meter에는 루트와 Wrapper의 Canvas 두 개가 있고, 루트에 GraphicRaycaster는 없다. 눈금 구조는 다음과 같다. 이는 에셋 구조 요약이며 게임 소스의 복사본이 아니다.

```text
Tick                    RectTransform, CanvasRenderer, ErrorMeterTick
  Tick Container        RectTransform, CanvasRenderer
    Outline             RectTransform, CanvasRenderer, Image
    Body                RectTransform, CanvasRenderer, Image
```

직렬화된 두 Image는 사용자 지정 재질이 없고 시작 알파가 0이다. 둘 다 raycastTarget은 true여서, 후보는 **해당 Canvas에 GraphicRaycaster가 있으면 적용을 거절**한다. Image 자체 이외의 스크립트·메시 효과·레이아웃 컴포넌트가 같은 GameObject에 있어도 거절한다. 런타임에 실제 구성이 다르면 `rejected`와 `hidden`으로 구분한다.

인계 측정의 약 0.1ms는 **막대 캔버스 전체를 제거한 차이**다. 배경·바늘·보이는 눈금·보이지 않는 눈금·트윈 중 각각의 비용을 분리한 값은 아니다. 프로파일 비율도 모든 캔버스 및 상위/하위 호출을 포함하므로 합산해서 이 막대의 시간으로 환산할 수 없다. 소스상 한 풀은 Image 120개이며, 인계의 런타임 Graphic 244개와 동일한 집계 범위라고 가정하지 않았다.

이번 후보가 제거하는 부분은 투명한 채움/외곽선의 Graphic 등록과 비어 있는 그리기 경로다. 보이는 눈금의 알파 변경, 원래 DOTween 콜백, 배경·바늘은 남는다. 따라서 개선 가능량은 전체 제거 상한의 일부다. 네이티브 엔진이 이미 투명 메시를 효율적으로 제외한다면 이득이 없거나 재활성화 비용 때문에 손해일 수도 있다.

## 설계와 복구

새 [HitMeterBatch.cs](../../HitMeterBatch.cs)는 눈금별 Image의 dirty 콜백을 등록한다. 정상 페이드의 마지막 OnKill이 색을 지우거나 외곽선을 쓰지 않을 때, **`image.color.a == 0f`인 경우만** Image.enabled를 끈다. 작은 양수, 음수, NaN을 임의로 0으로 취급하지 않는다. CanvasRenderer 알파를 읽어 정점 색으로 변환하지 않는다.

`Show` prefix/finalizer는 호출 중간의 이전 OnKill·새 색 대입에 반응하지 않도록 범위를 표시한다. finalizer에서 최종 색이 보이는 Image만 다시 켠다. 사용하지 않는 외곽선은 매 판정마다 깨우지 않는다. 비활성화 중에도 게임의 색 저장·트윈 생성·렌더러 알파 갱신은 그대로 진행된다. 원래부터 비활성화된 Image는 후보가 소유한 비활성화가 아니므로 임의로 켜지 않는다.

한 메시로 합치는 A안은 이번 후보로 채택하지 않았다. 현재 렌더러의 실수 알파를 일반 UIVertex의 Color32로 옮기면 별도 양자화가 생길 수 있다. 보이는 메시를 그대로 두는 B안으로 이 위험을 피했다. A안의 구현·픽셀 검증을 마쳤다는 뜻은 아니다.

수명 관리:

- 기본 꺼짐 상태에서는 이 후보의 Harmony 패치·정적 이벤트·dirty 콜백을 설치하지 않는다.
- 켤 때만 전용 Harmony ID `StutterFix.HitMeterBatch`로 패치한다. 최초 탐색·풀 등록 때만 상태 객체와 콜백을 만든다. Canvas가 아직 없으면 첫 Show에서 등록을 재시도한다.
- 프레임 Update/LateUpdate, 전체 Image 순회, 새 GameObject는 추가하지 않았다. 준비 후 판정 경로는 사전 조회와 기존 콜백을 사용한다.
- Reset 전에 해당 풀의 콜백을 제거하고 후보 소유 enabled 상태를 복구한다. 원래 Reset 뒤 새 상태로 등록한다.
- 편집 복귀(`scnEditor.SwitchToEditMode`), 활성 장면 변경, 장면 언로드 시 풀 상태·콜백을 모두 해제하고 Image를 복구한다. 기능이 켜져 있는 동안 다음 맵의 Awake/첫 Show를 받는 생명주기 패치는 유지한다.
- `meterbatch off`와 `Main.Unload`에서는 Image 복구·콜백 제거뿐 아니라 전용 Harmony 패치와 장면 이벤트도 해제한다. 모드 끔/다시 켬 때 Config 값을 다시 반영한다.
- 설치·콜백·Show 처리 중 후보에서 예외가 나면 소유 상태를 복구하고 이번 활성화를 차단한다. 원래 게임의 예외는 삼키지 않는다. 다시 시도하려면 off → on으로 전환한다.

## 변경 파일

| 경로 | 내용 |
|---|---|
| `HitMeterBatch.cs` | 기본 꺼짐의 투명 Image 등록 해제, 복구, 진단 상태, 후보 적용 확인 |
| `StutterFix.cs` | 설치/언로드, 설정 반영, `Settings.HitMeterBatch=false` |
| `StutterFix.csproj` | 새 파일을 Player/PlayerAuto 빌드에 포함 |
| `AutoTest.cs` | `meterbatch on/off/state/verify`, 임시 설정 복구, 캡처 비교 단계 |
| `tools/optimization/HitMeterBatchCheck/HitMeterBatchCheck.csproj` | 실제 후보 소스를 링크하는 오프라인 검사 프로젝트 |
| `tools/optimization/HitMeterBatchCheck/Stubs.cs` | 저장값·dirty 콜백·생명주기 검사 어댑터 |
| `tools/optimization/HitMeterBatchCheck/Program.cs` | 복구·거절·예외·재사용·할당 검사 |
| `tools/optimization/HitMeterDirectCheck/Adapters.cs` | 기존 검사에 빠져 있던 현재 로그 API `Lg.I` 어댑터 추가 |
| `docs/handoff/hitmeter-batch-20261123-result.md` | 이 문서 |

`HitMeterFade.cs` 자체는 변경하지 않았다. 작업 도중 별도로 변경된 `EngineCensus.cs`, `Compat.cs`는 본 작업의 변경이 아니며 되돌리지 않았다. 최종 DLL 빌드는 공유 작업 폴더의 당시 소스를 포함한다.

## 오프라인 확인 결과

| 확인 | 결과 | 로그 |
|---|---|---|
| `dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1` | 오류 0, 경고 65 | [release-build](../../out/hitmeter-batch-release-build.txt) |
| `dotnet build -c Debug -p:Edition=Player` | 오류 0, 경고 64 | [debug-build](../../out/hitmeter-batch-debug-build.txt) |
| Release PlayerAuto LoadCheck | 이름 지정 FieldRefAccess 315곳, 틀림 0, 동적 8곳 | [release-loadcheck](../../out/hitmeter-batch-release-loadcheck.txt) |
| Debug Player LoadCheck | 이름 지정 FieldRefAccess 255곳, 틀림 0, 동적 7곳 | [debug-loadcheck](../../out/hitmeter-batch-debug-loadcheck.txt) |
| 새 후보 검사 | 37검사 통과, 준비 후 100,000회 재사용 할당 0바이트 | [offline-check](../../out/hitmeter-batch-offline-check.txt) |
| 기존 Fade/직접 Kill 회귀 | 2,304시나리오, 276,480프레임, 12,732,916개 비트값, 272계약 검사 통과. 별도 Fade 회귀 256시퀀스/6,144프레임 포함 | [fade-regression](../../out/hitmeter-batch-fade-regression.txt) |

새 검사는 실제 `HitMeterBatch.cs`를 실행하지만 Unity 저장값·콜백 및 Harmony 설치는 어댑터다. Unity 네이티브 GraphicRegistry/CanvasRenderer 동작, Mono 할당, Harmony 실제 적용, 픽셀, 성능을 검증한 결과로 확대하지 않는다. LoadCheck도 이름 기반 FieldRefAccess 검사이며 모든 런타임 패치의 성공 보증이 아니다.

재현 명령(PowerShell, 저장소 루트):

```powershell
dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1
dotnet build -c Debug -p:Edition=Player
dotnet run --project tools/LoadCheck -c Release -- bin/PlayerAuto/StutterFix.dll
dotnet run --project tools/LoadCheck -c Release -- bin/Player/StutterFix.dll
dotnet run --project tools/optimization/HitMeterBatchCheck -c Release
dotnet run --project tools/optimization/HitMeterDirectCheck -c Release
```

## Claude가 실행할 픽셀·복구 시험

이후 게임 시험용 DLL은 `bin/PlayerAuto/StutterFix.dll`이다. 이번 작업에서는 설치하지 않았다. 측정 전 다른 게임 시험/사용자 플레이와 겹치지 않게 하고, 설치 파일·설정·표시 모드를 보존하는 기존 시험 절차를 사용한다.

`meterbatch on/off`는 기존 AutoTest `set` 경로를 사용하므로 묶음 끝·오류·시간 초과 시 사용자 원래 설정으로 복구된다. `state`의 `active`, `failed`, `hidden`, `rejected`, `error`를 기록한다. 후보 픽셀 비교는 `active=True`, `hidden>0`이 아니면 실패한다.

다음은 AutoTest 줄 목록이다. sf_run을 사용하면 이 줄들을 `steps`로 전달한다. 게임을 켠 채 시험할 때는 기존 keep 묶음 절차를 사용한다.

```text
open D:/얼불춤 맵 파일/HELLO (BPM) 2026/level.adofai
auto on
set HitMeterFade true
set HitMeterDirectKill true
play
waitsongpos 20 180
pause
meterbatch verify
meterbatch state
resume
stop
```

`verify`는 일시정지를 필수로 요구하며 기존 FrameCompare 경로로 다음을 수행한다. 별도 Screenshot 렌더링 구현은 추가하지 않았다.

1. 후보 off, 기존 캡처 목록 clear, `filters on`, `ui include`, `mask none`.
2. 꺼짐 `mb_a`, `mb_a2` 캡처 후 `assertzero mb_a mb_a2`. 변하지 않는 동일 곡 시각의 음성 대조군이다.
3. 후보 on, 실제 적용 `hidden>0` 확인, `mb_b` 캡처 후 `assertpixels mb_a mb_b`. 바뀌는 enabled 상태는 의도한 것이므로 이 비교는 픽셀만 판정한다.
4. 후보 off, `mb_a3` 캡처 후 `assertzero mb_a mb_a3 png`. 픽셀과 FrameCompare 상태 복구를 확인한다.
5. `mb_b`, `mb_a2`도 저장하고 진입 전 설정을 복구한다. 성공 로그는 `[meterbatch verify] PASS`다.

네 PNG는 모드 폴더 `framecmp/mb_a.png`, `mb_a2.png`, `mb_b.png`, `mb_a3.png`에 남는다. 같은 이름은 다음 verify에서 덮어쓰므로 각 지점의 로그·PNG를 즉시 별도 보관한다. 필터를 유지한 전체 화면에서 A/A2부터 다르면 후보 통과로 해석하지 않는다. 움직이는 UI·시간 기반 셰이더 등 대조군 불안정 원인을 먼저 분리한다. 허용 오차나 마스크로 눈금 차이를 숨기지 않는다.

한 번의 정지 장면 외에 다음을 시험한다.

- 직선·곡선, 모든 막대 크기, XPerfect 외곽선 켬/끔, 빠른 재사용(60슬롯 순환), 협동 모드의 색·짧은 수명.
- 보이는 눈금이 많은 시점과 마지막 눈금이 사라진 뒤. `resume` 후 실제로 사라지고 다시 표시되는지 확인한다.
- 같은 일시정지 장면에서 HitMeterFade=true와 false 각각 verify. 새 후보가 기존 Fade의 어느 설정에도 알파를 재계산하지 않는지 확인한다.
- Reset/재시작, Play→편집→다른 맵→Play, UI 크기·모양 변경, 모드 끔/다시 켬 및 UMM 언로드. off 뒤 `ticks=0`, `hidden=0`과 원래 Image.enabled 복구를 확인한다. 다음 on에서 중복 콜백/중복 패치가 없어야 한다.
- 사용자 지정 재질·Image 하위 클래스·동일 GameObject의 추가 효과·GraphicRaycaster가 있는 경우 해당 Image가 거절되고 원래 경로를 유지하는지 확인한다.

## Claude가 실행할 HELLO ABBA

기존 sfmeasure `sf_ab`의 현재 구현은 `repeats=2`일 때 총 4판, **A(off) → B(on) → B(on) → A(off)**다. 판마다 새 게임 프로세스를 사용한다. 같은 판 안에서만 옵션을 바꾼 FPS는 채택 근거로 쓰지 않는다.

다음은 `sf_ab` 인자다. 이번 작업에서는 호출하지 않았다.

```json
{
  "setting": "HitMeterBatch",
  "a": false,
  "b": true,
  "repeats": 2,
  "timeout_min": 10,
  "steps": [
    "open D:/얼불춤 맵 파일/HELLO (BPM) 2026/level.adofai",
    "auto on",
    "set HitMeterFade true",
    "set HitMeterDirectKill true",
    "play",
    "meterbatch state",
    "waitsongpos 11.5 180",
    "mark meterbatch_12_52",
    "waitsongpos 52 180",
    "mark meterbatch_exclude_after_52",
    "meterbatch state",
    "stop",
    "quit"
  ]
}
```

`mark`는 현재 코드상 0.5초 뒤부터 Sample을 받는다. 따라서 피치 1에서 11.5초에 미리 걸어 약 12~52초를 모은다. 실제 명령 처리 프레임과 곡 시계의 경계 오차가 있으므로 **정확한 12.000≤songposition<52.000 비교는 기존 Claude의 곡 시계 gate/ETW 프레임 기록을 잘라서 계산**한다. 위 자동 시험의 정수 반올림 FPS만으로 0.1ms 이하의 개선을 결론내리지 않는다. sf_ab의 전체 요약에는 준비/제외 구간도 섞일 수 있으므로 이름이 `meterbatch_12_52`인 구간과 원시 프레임 기록만 따로 사용한다.

픽셀 검증용 pause/verify, GC 할당 진단, 상세 프로파일은 성능 4판과 분리한다. ABBA에서는 다른 최적화·해상도·표시 모드·프레임 제한·배경 프로그램을 동일하게 유지한다. 판별 지표는 평균 FPS, 메인/렌더 스레드 ms/frame, p95/p99, 최악 프레임·hitch 수, steady-state GC 할당, 후보 `hidden`/재활성화 횟수다. 메인 스레드 시간과 GPU/대기 시간을 겹쳐 더하지 않는다.

후보가 실제 적용된 두 B판에서 픽셀·복구가 통과하고 메인 스레드와 실제 FPS에 반복 가능한 이득이 있는지 확인한다. 전체 캔버스 off의 약 +3.3%는 이 후보의 기대 실측값이 아니다. 이득이 없거나 꼬리 지연이 나빠지면 기본 꺼짐을 유지한다.

## 남은 위험과 작업 중 외부 변경

- B안은 정상적으로 보이는 눈금의 메시·페이드 비용을 줄이지 않는다. Image 재활성화의 uGUI 등록·메시 재생성 비용이 절감보다 클 수 있다.
- Image가 비활성인 동안 게임 이외의 코드가 색·재질을 직접 바꾸면 dirty 콜백이 오지 않을 수 있다. 이번에 읽은 게임 경로는 Show/Start/OnKill이지만, 다른 모드의 임의 쓰기까지 보장하지 않는다. 동적 컴포넌트 추가·커스텀 셰이더·레이캐스터 추가도 실제 호환성 시험 전에는 지원 판정하지 않는다.
- 초기 거절 검사는 안전장치이지 전곡·모든 UI 구성을 검증했다는 증거가 아니다. CanvasRenderer.Clear/재등록, 마스크·캔버스 그룹, 네이티브 정점/배치 처리의 정확한 효과는 게임 캡처와 프로파일로 확인해야 한다.
- 오프라인 할당 0은 새 후보의 준비 후 저장값/콜백 경로에 한정한다. 최초 탐색·새 풀 등록·Reset 후 재등록·설정 전환·verify 캡처는 할당한다. Unity 내부 재등록과 Mono의 실제 프레임 할당은 미확인이다.
- 설치 DLL은 읽기 전용 해시 확인 사이에 외부에서 변경되었다. 최초 SHA-256은 `B1C9A525EFC975EB4109A9DD4FE3C75E77A7A18A31FD32B1CBD644AAF41E9601`, 이후 관측값은 `256CD80406183ABE7362F3097B8AD1683E184D2BAC723DAB648F65D6BC5CFAD3`였다. 본 작업은 복사·복원을 실행하지 않았다. 다음 시험은 당시 설치본/설정과 동시 작업 여부를 새로 확인해야 한다.

다음 우선순위는 실제 DLL의 Harmony 적용 상태와 `hidden>0` 확인 → UI 포함 A/A2/B/A3 픽셀 비교 → 표시 재사용·맵/언로드 복구 → 별도 HELLO ABBA다. 지금은 검증 대기 후보이며 일반 채택 상태가 아니다.
