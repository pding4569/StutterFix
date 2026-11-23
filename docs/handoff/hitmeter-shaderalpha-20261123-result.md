# 판정 오차 막대 셰이더 알파 작업 결과

작성일: 2026-11-23. 작업 폴더: `E:\StutterFixWork\wt-v14prod`.
브랜치: `claude/v3-282port`, 기준 HEAD: `3579d055bc70ce5c0955f1f81a2293a91af98633`.
작업 지시: [hitmeter-shaderalpha-20261123.md](hitmeter-shaderalpha-20261123.md).

## 현재 결과와 차단 사항

C# 후보 경로, 셰이더 소스, 번들 빌드 도구, 알파 비교 탐침 및 복구 검사를 구현했다. **`HitMeterShaderAlpha=false`가 기본값**이다. 지정한 빌드 3종과 LoadCheck는 모두 오류 0이며, 아래 오프라인 검사도 통과했다.

**셰이더 번들 생성은 완료하지 못했다. 현재 DLL에는 후보 번들이 없으므로 후보가 적용되지 않는다.** Unity 6000.3.10f1 batch Editor의 첫 시도는 Package Manager 로컬 IPC 연결 실패로 종료됐다. `-noUpm` 재시도는 `LicenseClient-나로컴` 연결 거절 후 라이선스 초기화 대기에서 멈췄다. 라이선스 자체가 없다고 판정한 것은 아니다. 재시도 Editor는 이 작업이 시작한 PID와 실행 파일을 확인한 뒤 종료했고, 게임·공유 라이선스 클라이언트에는 종료 명령을 보내지 않았다.

근거는 [Editor 로그](../../out/hitmeter-alpha-lab/editor-build.log) 및 [시도 기록](../../out/hitmeter-shaderalpha-20261123/bundle-attempts.txt)이다. 번들이 없을 때 C# 빌드는 명시적 경고를 내고, 런타임은 원래 알파 경로를 사용한다. 비어 있는 가짜 번들을 제품에 넣지 않았다. **번들 컴파일, 실제 `alphaprobe`의 바이트 일치, 화면 비교, 배치 수 및 성능은 미검증**이다. 후속 작업은 먼저 정상 IPC 환경에서 번들을 만든 뒤 DLL을 다시 빌드해야 한다.

이번 작업에서 커밋·푸시, 게임 실행·종료, 설치 DLL 교체는 하지 않았다. `bin/Player`와 `bin/PlayerAuto`의 작업 폴더 빌드 산출물만 갱신했다. 시작 때 있던 `EngineCensus.cs`, `AutoTest.cs`/`HitMeterFade.cs`의 진단, `QuartzFrameTrim.cs` 및 관련 설정·프로젝트 변경은 보존했다. 설치 파일의 사전/사후 해시 비교는 수행하지 않았다.

## 설계와 적용 범위

- 기존 `HitMeterFade.Fade`의 DOTween Color getter/setter 계산, `a0`, 종료값, duration, target 및 게임이 붙이는 완료·Kill 처리를 유지한다. 허용된 눈금의 setter는 같은 `x.a / a0`를 고정 `float[256]` 슬롯에 쓴다. 후보가 적용되지 않은 눈금과 복구된 기존 트윈은 `CanvasRenderer.SetAlpha`를 계속 사용한다.
- Body/Outline에는 공용 재질 한 개를 사용한다. `BaseMeshEffect`가 정점의 UV1.x에 슬롯 번호를 기록한다. Image 교체, 형제 순서 변경, 좌표·스프라이트·정점 수 변경은 하지 않는다. 매 프레임 알파 쓰기로 메시를 다시 dirty 처리하지 않는다. 새 트윈 시작 시에는 기존처럼 렌더러 알파를 1로 초기화한다.
- `Canvas.willRenderCanvases`에서 배열 전체를 올린다. 정상 렌더에서는 프레임당 한 번이다. 같은 프레임에 `ForceUpdateCanvases`가 먼저 호출되고 그 뒤 트윈 값이 바뀌면 다시 올린다. 오래된 알파를 그리는 것을 피하기 위한 예외이며, 값 변경 없는 중복 콜백은 업로드하지 않는다. 프레임별 임시 배열은 만들지 않는다.
- 현재 실행에서 탐침을 통과해야 적용한다. 켤 때 보정 결과가 없으면 탐침을 실행한다. 결과는 설정 파일에 저장하지 않는다. 번들 없음, 셰이더 미지원, 탐침 불일치, 예외는 적용을 차단한다. 재시도는 `off` → `on`이다.
- 원래 빠른 길 조건에 더해 `ADOFAI.ErrorMeterTick` 아래의 정확한 Image, 기본 재질, 다른 메시 효과 없음, Mask/RectMask2D/CanvasGroup 없음 조건을 확인한다. 사용자 재질·다른 컴포넌트·슬롯 부족이면 기존 경로로 간다. 시작·끝 RGB 변경 및 시작 알파 0은 기존 느린 길을 따른다.
- 외부 색/메시 dirty, 재질 변경, 캔버스 변경, 상속 알파 변경, 메시 효과 비활성화, 보정 범위 `[0,1]` 밖 값 및 NaN을 만나면 복구한다. 다른 모드가 이미 교체한 재질은 덮어쓰지 않는다. 기본 재질의 원래 명시적 값이 `null`인지도 복원한다(`Graphic.m_Material` 필드가 없으면 적용 거절).
- 끄기·장면 전환·`scnGame.ResetScene`·언로드에서 원래 재질과 **마지막 트윈 알파**로 복구한다. 기존 setter가 가진 상태 객체는 비활성 상태가 되어 이후에도 원래 렌더러에 값을 쓴다. 사용한 캔버스 UV1 채널 비트도 마지막 소유자가 빠질 때 복구한다.
- 눈금 재사용의 Kill → Show → Fade가 한 프레임 안에 일어나는 경우를 위해, 개별 복구에서는 자체 메시 효과를 비활성 상태로 재사용한다. 전역 정리에서 효과를 제거하고, 언로드에서는 장면/캔버스 이벤트와 번들도 정리한다.

켜기는 **이후 생성되는 Fade부터** 적용된다. 일시정지한 뒤 켜기만 하면 남아 있는 트윈이 셰이더로 옮겨지지 않는다. 픽셀 시험은 재생 전에 켜고, 눈금들이 갱신된 뒤 일시정지하여 켬 → 끔 순서로 비교해야 한다. `state`의 `active`가 0인 비교를 후보 성공으로 판정하지 않는다.

## 알파 탐침

`alphaprobe`와 `meteralpha probe`는 같은 동기식 진단이다. 전용 ScreenSpaceCamera 캔버스, 카메라, 64×64 ARGB32 RT, 16×16 Image 격자를 만들고 원래 재질+`SetAlpha(c)`와 후보 재질+배열 c의 전체 RGBA 바이트를 비교한다. 마지막에 생성 객체·RT·읽기 텍스처를 제거하고 기존 active RT를 복구한다. 기존 후보 눈금은 탐침 시작 때 원래 경로로 돌린다.

- 정점 알파 표본: `0,1,2,3,7,15,31,63,64,95,127,128,159,191,223,254,255`.
- 각 표본에 `c=0..1` 4,097개와 `(b+0.5)/v`의 인접 float 두 개 및 경계값을 추가한다.
- 흰색/유색 입력을 검사한다. Body 스프라이트를 찾으면 흰 Image에 더해 Body도 검사한다. 찾지 못했으면 `bodySprite=False`를 로그에 남긴다.
- 후보 모드 0: 실수 곱, 1: `floor(v*c+0.5)`, 2: HLSL `round(v*c)`, 3: `floor(v*c)`에 해당하는 정점 알파 처리다. 셰이더의 UI fragment 알파 정밀도 처리도 비교 대상에 포함된다. 어떤 모드가 엔진과 같은지는 아직 측정하지 못했다.
- 모드별 불일치 픽셀 수, 첫 사례의 v/c·색·텍스처·픽셀 좌표와 양쪽 RGBA를 기록한다. 전체 불일치 0인 첫 모드만 선택한다. 일부를 불투명하게 잘못 그리는 음성 대조군 차이가 0이면 통과시키지 않는다.

탐침은 다수의 동기 렌더·ReadPixels·배열 할당을 수행한다. 일시정지 등 진단 구간에서 실행하고 성능 표본에서 제외한다. 표본 통과는 모든 실수·모든 화면 구성의 동일성을 증명하지 않으므로 실제 막대 `shot` 비교도 필수다.

## 이번 변경 파일

| 명시적 경로 | 변경 내용 |
|---|---|
| `HitMeterShaderAlpha.cs` | 슬롯·공용 재질·정점 효과·업로드·적용 거절·복구·상태 명령 |
| `HitMeterAlphaProbe.cs` | 원래/후보 RGBA 비교 및 반올림식 선택 |
| `HitMeterFade.cs` | 기존 setter의 선택적 슬롯 쓰기 연결 |
| `SceneReset.cs` | 게임 ResetScene 전 복구 |
| `StutterFix.cs` | 설정 기본 꺼짐, 설정 반영 및 설치/언로드 연결 |
| `StutterFix.csproj` | 소스·선택적 번들 리소스와 번들 누락 경고 |
| `AutoTest.cs` | `meteralpha on/off/state/probe`, `alphaprobe` |
| `tools/hitmeter-alpha/HitMeterAlpha.shader` | UI 알파 슬롯 및 반올림 후보 셰이더 소스 |
| `tools/hitmeter-alpha/Build.cs` | Unity 6000.3.10f1 Windows64 번들 빌드 |
| `tools/hitmeter-alpha/build.ps1` | 독립 batch 프로젝트 준비, 시간 제한 및 성공 시 번들 복사 |
| `tools/optimization/HitMeterShaderAlphaCheck/HitMeterShaderAlphaCheck.csproj` | 실제 소유/복구 코드를 링크하는 검사 프로젝트 |
| `tools/optimization/HitMeterShaderAlphaCheck/Program.cs` | 상태·복구·예외·할당 검사 |
| `tools/optimization/HitMeterShaderAlphaCheck/Stubs.cs` | Unity 저장값/이벤트 어댑터와 보정 결과 모사 |
| `tools/optimization/HitMeterDirectCheck/Adapters.cs` | 기본 꺼짐 Fade 회귀를 위한 새 연결 어댑터 |
| `docs/handoff/hitmeter-shaderalpha-20261123-result.md` | 이 결과 문서 |

`tools/hitmeter-alpha/hitmeteralpha.bundle`은 **미생성**이다. 검사 프로젝트가 사용하는 모사 리소스는 검사 어셈블리에만 들어가며 제품 번들이 아니다.

## 수행한 확인

| 확인 | 결과 | 로그 |
|---|---|---|
| Release PlayerAuto 빌드 | 오류 0, 경고 66(번들 누락 경고 포함) | [빌드](../../out/hitmeter-shaderalpha-20261123/release-auto-build.txt) |
| Debug Player 빌드 | 오류 0, 경고 66 | [빌드](../../out/hitmeter-shaderalpha-20261123/debug-player-build.txt) |
| Release Player 빌드 | 오류 0, 경고 66 | [빌드](../../out/hitmeter-shaderalpha-20261123/release-player-build.txt) |
| PlayerAuto LoadCheck | 이름 지정 315곳, 틀림 0, 동적 8곳 | [검사](../../out/hitmeter-shaderalpha-20261123/release-auto-loadcheck.txt) |
| Debug Player LoadCheck | 이름 지정 255곳, 틀림 0, 동적 7곳 | [검사](../../out/hitmeter-shaderalpha-20261123/debug-player-loadcheck.txt) |
| Release Player LoadCheck | 이름 지정 255곳, 틀림 0, 동적 7곳 | [검사](../../out/hitmeter-shaderalpha-20261123/release-player-loadcheck.txt) |
| 새 소유/복구 검사 | 40검사, 100,000회 쓰기+캔버스 콜백 관리 힙 할당 0바이트 | [검사](../../out/hitmeter-shaderalpha-20261123/owner-check.txt) |
| 기존 Fade/직접 Kill 회귀 | 2,304시나리오, 276,480프레임, 12,732,916비트값, 272계약 검사 통과. 별도 Fade 256시퀀스/6,144프레임 | [회귀](../../out/hitmeter-shaderalpha-20261123/fade-regression.txt) |
| `git diff --check` | 오류 0 | 공백 검사 |

소유/복구 검사는 실제 `HitMeterShaderAlpha.cs`를 실행하지만 Unity 저장값·이벤트·번들 로딩·보정 결과는 어댑터다. Fade 회귀도 기존 네이티브 UI 대신 어댑터를 사용한다. Mono 할당, 셰이더 수학, 실제 Harmony 적용, 픽셀, 캔버스 배치 및 FPS를 검증한 결과로 확대하지 않는다. LoadCheck의 동적 필드 이름은 런타임 확인 대상이다.

재현 명령(PowerShell, 작업 폴더 루트):

```powershell
# 이번 실행에서는 실패: 먼저 정상 Unity IPC 환경에서 성공시켜야 한다.
& tools/hitmeter-alpha/build.ps1

dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1
dotnet run --project tools/LoadCheck -c Release -- bin/PlayerAuto/StutterFix.dll
dotnet build -c Debug -p:Edition=Player
dotnet run --project tools/LoadCheck -c Release -- bin/Player/StutterFix.dll
dotnet build -c Release -p:Edition=Player
dotnet run --project tools/LoadCheck -c Release -- bin/Player/StutterFix.dll
dotnet run --project tools/optimization/HitMeterShaderAlphaCheck -c Release
dotnet run --project tools/optimization/HitMeterDirectCheck -c Release
git diff --check
```

## Claude가 이어서 할 게임 확인

1. 번들 생성 및 3종 재빌드부터 완료한다. 번들 누락 경고가 사라졌는지 확인한다.
2. 기존 HELLO 게임 시험 절차에서 막대가 생성된 뒤, 성능 수집 밖에서 아래 진단을 실행한다. `mode>=0`, 선택 모드 불일치 0, `negativePixels>0`을 확인한다. `bodySprite=False`였다면 막대가 있는 상태에서 다시 검사한다.

```text
meternoalpha off
meteralpha off
alphaprobe
meteralpha state
```

3. 다음 재생 시작 전에 `meteralpha on`을 적용한다. HELLO 게임 화면 20초까지 재생하고 `pause` 후 비교한다. 아래 두 `shot` 사이에는 곡 시각·카메라·창 크기·표시 배율을 바꾸지 않는다. HUD 동적 표시가 있다면 별도로 제외 영역을 명시한다.

```text
set OverlayMode 0
meteralpha on
# 기존 시험의 재생 절차로 HELLO 게임 화면 20초까지 진행
pause
meteralpha state
shot meter-alpha-on
meteralpha off
wait 0.1
shot meter-alpha-off
meteralpha state
```

켜짐 캡처 직전 `active>0`, 끈 뒤 `active=0`인지 확인하고 **막대 영역 최대 RGBA 차이 0 및 다른 픽셀 수 0**을 요구한다. 카메라 `framecmp`는 Overlay 막대를 담지 않으므로 이 검증에 쓰지 않는다. 원래 재질/렌더러 알파와 그림 순서, 후보 전후 draw/batch 수, 다음 눈금 재사용·장면 전환·언로드 복구도 확인한다.

4. 픽셀/복구 통과 후 기존 `ab_steps.py`/설정 ABBA 절차로 HELLO·Windflower 12~52초를 새 실행마다 측정한다. 후보 실행은 수집 전 보정과 눈금 적용을 마치고, 탐침·화면 읽기·진단 출력은 수집 구간에서 제외한다. 기본 꺼짐 대조와 켜짐의 `active`/업로드 상태를 함께 기록한다. 이 작업에서는 해당 스크립트를 실행하지 않았다.

## 남은 위험

- **현재 가장 앞선 차단점은 번들 생성이다.** C# 빌드 성공만으로 게임 시험 가능한 후보가 준비됐다고 해석하면 안 된다.
- 셰이더 소스는 아직 Unity shader compiler를 통과하지 못했다. UI/Default의 버전별 정밀도·감마·샘플링 차이가 있으면 모든 반올림 후보가 실패할 수 있다. 이 경우 보정식을 근거에 맞게 고치고 다시 빌드해야 한다.
- 공용 재질 하나여도 배경·바늘의 기본 재질과 경계가 생겨 배치 수가 늘 수 있다. 정점 채널 추가와 메시 효과 자체의 비용도 남으므로 실제 이득은 측정해야 한다. 기존 측정의 +3.3%를 이번 후보의 이득으로 쓰지 않는다.
- 조건 거절·외부 dirty 때문에 일부 눈금만 적용될 수 있다. `active`와 실제 SetAlpha/UpdateBatches 감소를 확인해야 한다. 사용자 재질·마스크·CanvasGroup을 쓰는 구성은 보수적으로 기존 경로를 유지한다.
- 동기 탐침의 메모리·정지 시간, Unity의 실제 이벤트 순서, nested canvas의 UV1 전달, 실제 DOTween 완료·Kill·맵 재시작, Mono 관리 할당은 게임에서 확인해야 한다.

픽셀 기준을 완화하거나 성능 성공을 선언하지 않았다. 후속 우선순위는 **번들 빌드 → 런타임 탐침 → 실제 막대 shot/복구/배치 확인 → 두 맵 ABBA**다.

## Claude 게임 확인과 고친 것 (2026-11-23)

- 번들: Codex 환경의 실패는 `-nographics` 때문이었다(Unity Personal 라이선스에 `com.unity.editor.headless` 없음). `build.ps1` 을 `-batchmode -quit -force-d3d11 -noUpm` 으로 고쳐 빌드했다.
- 셰이더: 처음 판은 `Blend SrcAlpha OneMinusSrcAlpha` 라 탐침 4모드 모두 불일치. Unity 2022.2+ UI/Default 처럼 `Blend One OneMinusSrcAlpha` + 프래그먼트 `color.rgb *= color.a` 로 고친 뒤 **모드 1(floor(v*c+0.5)) 불일치 0**(흰 그림 2,392,064px, Body 스프라이트 포함 4,784,128px), 다른 모드는 불일치 있음.
- 실제 화면: 일시정지(`controller.paused`)는 게임이 막대를 끈다(`UpdateErrorMeterVisibility`). `pause` → `metershow`(진단, 막대 다시 켬) → `shot` 켬 두 번 → `meteralpha off` → `shot`: HELLO 20초 눈금 120개 적용 상태에서 전체 화면 픽셀 차이 0, 대조 0.
- 성능: 처음 판은 HELLO 324.2→321.1(−1%). 프로파일에서 `Flush` 의 매 프레임 120개 검사가 2.89%(그중 `GetInheritedAlpha` 1.64%). 프레임당 한 칸씩 돌아가며 검사 + 바뀐 때만 업로드로 고친 뒤 **HELLO 323.4→329.6(+1.9%, 4판 같은 방향), WF 426.1→427.5(흔들림 안)**. 상한(투명도 쓰기를 아예 막음)은 +3.3%.
- 켤 때 탐침: 전체(모드 4개 × 74,503표본)는 메인 스레드 약 2.3초 → 자동 보정은 quick(모드 1, c 1/256 간격 + 바이트 경계 이웃 9,223표본) 221ms, 불일치 0. 전체 탐침은 `alphaprobe` 진단으로 남김.
- 기본 켬(DefaultsGen 9). 다른 GPU 에서 탐침이 맞지 않으면 원래 경로.
