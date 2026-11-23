# Quartz 프레임 비용 조사 결과 — 2026-11-23

> **2026-11-23 Claude 결론**: 상한이 프레임당 약 0.006ms 라 접었다. 코드(`QuartzFrameTrim.cs`, 검사 프로젝트)는 저장소에서 빼고 `E:StutterFixWorkpatchesquartztrim` 에 보관. Quartz 캔버스 전체 끔 상한은 HELLO +2.5%(키뷰어 약 0.7%, 패널 약 0.8%, 콤보 0).

## 결론

Quartz 3.0.0-alpha-4에서 제시된 주요 경로를 확인했다. **파티클 비활성화·링 색 쓰기·패널 문자열 갱신은 이미 상당 부분 조건부 처리여서, 큰 이득을 기대할 안전한 매 프레임 쓰기 생략 후보는 찾지 못했다.** 실제 입력·시간·펄스가 변하는 일의 빈도는 줄이지 않았다.

작은 시험 후보로 `QuartzFrameTrim`을 구현했다. 기본 꺼짐이며, Quartz의 판정 숨김 후처리 안에 있는 `GameObject.SetActive` 호출 **한 곳만** 감싼다. 요청값과 **지금 엔진에서 읽은 `activeSelf`**가 같을 때 생략한다. 매 프레임 호출 전체를 건너뛰는 기능은 아니다. 판정 발생 시 호출되며, 원래 Show가 매번 오브젝트를 켠다면 생략 횟수는 0일 수 있다. 새 getter 비용 때문에 오히려 느려질 가능성도 있다. **성능 개선·실제 화면 동일성·실제 Mono 패치 설치는 미측정이며, 기본 켬으로 올릴 근거가 없다.**

요청한 세 빌드와 LoadCheck는 오류 0개다. 자체 코드의 오프라인 검사 26개도 통과했다. 게임을 실행·종료하거나 설치 DLL을 복사·교체하는 명령, 커밋·푸시는 실행하지 않았다. 다만 종료 시점의 설치본 해시가 시작 시점과 달랐고 게임 프로세스도 관측되었다. 이 외부 상태 변화는 아래에 별도로 기록했다.

## 작업 기준과 분석 방법

- 작업 폴더: `E:\StutterFixWork\wt-v14prod`
- 브랜치: `claude/v3-282port`
- 시작 및 최종 HEAD: `3579d055bc70ce5c0955f1f81a2293a91af98633`
- `Runtime/state.json`의 Current: `3.0.0-alpha-4`를 읽어 확인했다.
- 분석 DLL: `D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\Mods\Quartz\Runtime\versions\3.0.0-alpha-4\QuartzUmm.dll`
- DLL SHA-256: `2BAA5754697E17BEE286E4C0651D9FF1B54F4DFA7C9CF299619A10E287ABED0E`
- ModuleVersionId: `a77c7a71-25b7-49c9-95c1-24a0078a5d4b`

`ilspycmd`의 형식별 출력을 메모리/도구 출력으로 읽고, `tools/ilscan`으로 호출 관계를 교차 확인했다. **디컴파일한 Quartz 소스는 파일로 저장하지 않았다.** 아래에는 함수 이름과 동작 요약만 적었다. 설정 파일도 관련 기능의 JSON만 읽었으며 인증 파일은 읽지 않았다.

인계 문서의 1kHz 표본과 메인 스레드 약 3.1ms/프레임은 기존 측정 근거다. 이번 작업에서는 재측정하지 않았다. 부모·자식 함수의 표본 비율은 겹칠 수 있으므로 아래 수치를 합산해 총 절감량으로 쓰면 안 된다.

## 함수별 분석

| 함수/경로 | 현재 하는 일, 값의 변화·할당 | 판단 |
|---|---|---|
| `VisualTweaks.ApplyBallCoreParticlesTweak` (0.77%) | Unity의 파괴된 객체 검사, 설정/복원 상태 검사 후 행성별 core/sparks를 얻어 각각 처리한다. 두 파티클의 값 튜플은 값 형식이며 정상 캐시 적중 경로에서 새 배열·문자열을 만들지 않는다. | 함수 전체 생략은 복원과 객체 교체를 놓칠 수 있다. |
| `VisualTweaks.GetPlanetParticles` (0.13%) | renderer instance ID로 파티클 두 개를 사전에서 찾는다. 캐시 미스 때만 이름 기반 멤버 조회를 거쳐 보관한다. 매 프레임 새 문자열로 반사 조회하는 구조가 아니다. | 현재 게임 필드로 무조건 대체하면 Quartz의 캐시 의미가 달라진다. 제외했다. |
| `VisualTweaks.ApplyPlanetParticleTweak` (0.63%) | `gameObject`, instance ID, `activeSelf`, 복원 사전을 확인한다. 이미 기록된 비활성 객체면 즉시 반환한다. 처음 제거할 때 Stop/Clear, emission 비활성화·rate 0·maxParticles 0, renderer 끄기, SetActive를 수행한다. 하위 객체 탐색 배열과 사전 확장은 이 초기 처리/복원 경로에 있다. | “매 프레임 같은 ParticleSystem 모듈 재설정”이라는 최초 가설과 다르다. 기존 조기 반환을 또 감싸는 것은 이득이 불명확하다. |
| `PanelsOverlay.Updater.Update` / `UpdatePanel` (0.51% / 0.47%) | 표시·재배치 상태와 raycaster를 확인한다. 텍스트 refresh는 상태 변경/Dirty 또는 약 0.05초 간격이다. 숨긴 패널은 이미 빠르게 반환한다. 활성 상태와 크기도 현재 값과 비교한다. 재사용 StringBuilder 내용을 LastBody와 비교해 같으면 새 결과 문자열을 만들지 않는다. 변한 통계의 문자열 생성은 여전히 가능하다. | 숨긴 패널 처리는 이미 짧다. updater 전체를 멈추면 다시 표시되는 시점·Dirty·재배치 상태를 놓칠 수 있다. 20Hz 통계 갱신을 더 늦추는 것은 동작 변경이다. |
| `LoaderUmm.OnUpdate` (0.51%) | 현재 loader 인스턴스와 Active/Waiting 상태를 확인한다. Active면 MainCore.Tick으로 넘기고, Waiting이면 소유권 확보·초기화를 처리한다. | 초기화/전역 tick 빈도를 줄이지 않았다. |
| `RuntimeTicks.Tick` (0.47%) | 등록된 runtime service를 순서대로 호출한다. 구체적인 List의 열거 경로이며 루프 자체에서 새 문자열을 만들지 않는다. | 내부 tick들의 동적 처리를 통째로 생략할 근거가 없다. |
| `FeatureService.TickAdapter.Tick` (0.35%) | FeatureService.Tick으로 전달한다. 각 기능의 Active/Ticks를 확인하고 OnTick을 호출하며 예외 시 해당 기능을 비활성화한다. Features는 인터페이스로 노출되어 열거자 할당 후보가 있으나 실제 Mono 할당량은 미측정이다. | 인덱스 루프로 바꾸면 콜백 도중 목록 변경의 예외 동작까지 검토해야 한다. 이번에는 타 모드의 dispatcher를 재작성하지 않았다. |
| `PlanetRendererLateUpdatePatch.Postfix` / `PlanetColors.ApplyPlanetRing` (0.45% / 0.43%) | 매 LateUpdate에 링 설정을 반영하고 overlay 표시도 조정한다. 별도 색/슬롯 설정과 onlyRing 상태를 읽는다. 링 재색칠을 하지 않는 경로는 필요 시 현재 alpha만 0으로 만든다. | 게임/다른 코드가 앞서 색을 바꿀 수 있어 마지막 설정값만 기억해서 생략할 수 없다. |
| `GameApi.SetRingColor` (0.34%) | 링 renderer 접근은 getter delegate를 우선 사용하고, 불가하면 멤버 조회로 돌아간다. LineRenderer의 startColor/endColor 또는 SpriteRenderer.color를 **현재 엔진 값과 비교한 뒤** 필요한 것만 쓴다. | 이미 요구한 형태의 비교가 있다. 추가 prefix는 현재 값 조회와 분기를 중복시킬 수 있다. 실제로 바뀐 색 쓰기를 제거하지 않았다. |
| `KeyViewerOverlay.Updater.Update` (0.31%) | 시간·포커스·게임 여부, 입력 큐·JS 런타임, 표시/재배치와 손/발 패널을 처리한다. root/drag/raycaster 등의 상태 쓰기는 대부분 현재 값 비교 뒤 수행한다. | 숨겨져도 입력·통계·JS 타이머에 의미가 있으므로 updater를 멈추지 않았다. |
| `UpdateDmNote` / `PollUncoveredKeys` (0.24% / 0.12%) | 입력 이벤트를 비우고 hook 미지원 키/재동기화 키를 폴링한다. 키 표시 지연, 최소 발광시간, 초당 입력 수, 카운트와 폰트를 처리한다. 숫자는 재사용 버퍼/변경 검사 경로를 사용한다. 매 프레임 입력·시간에 따라 결과가 바뀔 수 있다. | 폴링 주기를 낮추거나 자동 플레이 때 생략하지 않았다. 현재 설정은 IndependentInput=true, DmCssEnabled=false였다. CSS 상태 재질 쓰기 후보는 현재 설정의 상시 비용 근거가 없다. |
| `SongTitleOverlay.HideGameTitlePatch.Postfix` (0.25%) | 게임 제목인지·대체 제목 사용 여부·게임 상태를 검사한다. Graphic 참조를 캐시하고 enabled일 때만 끈다. 캐시 미스/파괴 객체 정리 때만 GetComponent/임시 목록 등이 생길 수 있다. | 이미 현재 상태를 검사한다. 제목을 계속 숨기는 책임을 제거하지 않았다. |
| `SongTitleOverlay.Updater.Update` (0.13%) | 표시·폰트·제목 본문을 확인한다. 폰트와 본문 변경 시에만 관련 쓰기/그림자를 적용하고 본문 생성에도 캐시가 있다. | 제목/표시 상태가 실제로 바뀌는 경우를 유지했다. |
| `JudgementPopupHider.HitTextShowPatch.Postfix` (0.20%) | 숨길 판정인지 매번 판단한다. 대상이면 dead 표시, 라벨 비우기, 숨김 위치/크기 적용과 비활성화를 한다. 마지막 SetActive는 별도 비교 없이 호출된다. 판정 시 호출이며 반드시 매 프레임 발생하는 것은 아니다. | 마지막 활성 상태 쓰기 한 곳만 후보로 골랐다. 판정 판단, 원래 Show, 위치/크기와 라벨 처리 등은 계속 실행한다. |
| `ComboOverlay.Updater.Update` (0.13%) | 콤보 수·폰트·pulse scale/intensity로 글자 크기/색·레이아웃을 계산한다. 수/크기/색/배치 변경 검사와 preferred-size 캐시가 이미 있다. 펄스 중에는 실제 값이 변한다. | 수치나 pulse 갱신 빈도를 줄이지 않았다. |

## 구현 범위와 동일성 근거

`QuartzFrameTrim.cs`는 Harmony 소유자 `quartz.feature.hidejudgements`로 등록된 정확한 nested postfix를 찾는다. 형식 이름·메서드 이름·정적 void 서명·인수 형식·위 ModuleVersionId를 확인한다. helper에 다른 Harmony 수정이 있거나 SetActive 호출 개수가 1개가 아니면 설치를 거부하고 자기 패치만 해제한다. 전용 ID는 `StutterFix.QuartzFrameTrim`이다. 게임의 Show 패치나 Quartz 소유 패치를 제거·재등록하지 않는다.

대상 IL의 호출 한 곳만 자체 wrapper로 바꾼다. 입력 인수, 분기 label과 예외 블록을 보존한다. wrapper는 이전 요청값이나 instance ID를 기억하지 않고 호출 순간의 `activeSelf`를 읽는다. 부모가 꺼져 있어도 자식의 로컬 상태를 판단하기 위해 `activeInHierarchy`는 사용하지 않는다. 값이 다르면 원래 SetActive를 호출한다. 따라서 다른 코드가 그사이 다시 켰다면 실제 비활성화를 수행한다.

`verify on`에서는 생략 후보마다 엔진 값을 한 번 더 읽고 요청값과 대조한다. 다르면 mismatch를 올리고 원래 쓰기로 돌아간다. 성공적으로 반환한 SetActive만 written에 센다. skip마다 비교한 수는 checks이며, mismatch가 없는 실행에서는 reset 이후 checks=skipped여야 한다. **이 검사는 bool 대입 생략 조건의 검사다. 원래 Show 전체·타 모드 콜백 순서·실제 화면 동일성을 대신하지 않는다.**

정상 부착 뒤 Tick과 setter wrapper에는 반사 조회·컬렉션 생성·문자열 생성이 없다. 찾기/설치와 명령 응답 때의 할당은 별도다. Quartz가 없으면 로그를 한 번 남기며, 매 프레임 설치 시도를 반복하지 않는다. 비활성 Quartz가 나중에 활성화되는 것은 확인한다. **Quartz가 Active 상태인 채 runtime을 자체 교체한 경우 자동 재탐색하지 않는다. `quartztrim off` → `quartztrim on` 또는 StutterFix 다시 불러오기가 필요하다.** 모드 로드 순서 때문에 최초 조회 때 postfix가 없었던 경우에도 재활성화로 다시 시도한다.

`Main.Unload` → `UninstallAll` → `QuartzFrameTrim.Shutdown`에서 전용 패치를 해제하고 타 모드 참조를 놓는다. 설정을 끌 때도 바로 해제한다. 엔진 값을 캐시하거나 별도로 소유하지 않으므로 되돌려 쓸 값은 없다. 게임/Mono에서의 실제 부착·해제 시험은 남아 있다.

### 기대 효과의 한계

전체 postfix 0.20%를 모두 제거해도 인계 문서의 3.1ms 기준 약 **0.0062ms/프레임**에 해당한다. 이번 후보는 그 안의 SetActive 한 호출 일부에만 영향을 준다. 이는 표본에서 산출한 규모 설명이며 실측 절감치가 아니다. 기존 Unity SetActive 자체가 같은 상태를 빠르게 처리할 수도 있다. **skipped=0이거나 실제 ABBA 이득이 없으면 이 후보를 채택하지 않는 것이 맞다. Quartz 합계 약 4%를 없앤 기능으로 설명하면 안 된다.**

## 명시적 변경 파일

아래 경로는 모두 `E:\StutterFixWork\wt-v14prod` 기준이다.

| 경로 | 변경 |
|---|---|
| `QuartzFrameTrim.cs` | 기본 꺼짐 호환 패치, 현재 상태 비교, 진단 카운터, 해제 경로 |
| `StutterFix.cs` | Settings.QuartzFrameTrim=false, 설정 적용·설치 시 반영, Tick 및 Shutdown 연결 |
| `StutterFix.csproj` | 새 자체 코드 compile 포함 |
| `AutoTest.cs` | `quartztrim` 명령 한 줄 추가 |
| `tools/optimization/QuartzFrameTrimCheck/QuartzFrameTrimCheck.csproj` | 자체 소스를 직접 포함하는 오프라인 검사 프로젝트 |
| `tools/optimization/QuartzFrameTrimCheck/Program.cs` | 가짜 엔진 저장소 검사, 실제 Quartz 메타데이터 형태 검사 |
| `docs/handoff/quartz-frame-cost-20261123-result.md` | 이 결과 문서 |

기존 `HitMeterFade.cs`와 `EngineCensus.cs`는 시작 때와 SHA-256이 같다. AutoTest는 새 quartztrim 명령 한 줄을 바이트 수준에서 제외하면 시작 때의 SHA-256과 같다. 기존 ProbeNoAlpha/meternoalpha/canvaslist/canvasoff 변경을 보존했다. 인계 원본도 수정하지 않았다.

## 이번에 완료한 검증

실행 로그는 `out/quartz-frame-cost-20261123/` 아래에 있다. 디컴파일 소스는 포함하지 않는다.

| 명령/검사 | 결과 | 근거 파일 |
|---|---|---|
| `dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1` | 오류 0, 경고 65 | `build-autotest.txt` |
| `dotnet build -c Debug -p:Edition=Player` | 오류 0, 경고 65 | `build-debug.txt` |
| `dotnet build -c Release -p:Edition=Player` | 오류 0, 경고 65 | `build-release.txt` |
| AUTOTEST LoadCheck | FieldRefAccess 315곳, 틀림 0, 동적 이름 8곳은 런타임 확인 대상 | `loadcheck-autotest.txt` |
| Debug / Release Player LoadCheck | 각각 255곳, 틀림 0, 동적 이름 7곳은 런타임 확인 대상 | `loadcheck-debug.txt`, `loadcheck-release.txt` |
| 자체 오프라인 검사 | 26개 통과, guard 120,000회 관리 힙 할당 0B, absent-mod Tick 100,000회 0B | `offline-check.txt` |
| `git diff --check` | 오류 없음 | 도구 실행 결과 |

오프라인 검사 재현:

```powershell
dotnet run --project tools/optimization/QuartzFrameTrimCheck -c Release
dotnet tools/LoadCheck/bin/Release/net8.0/LoadCheck.dll bin/PlayerAuto/StutterFix.dll
dotnet tools/LoadCheck/bin/Release/net8.0/LoadCheck.dll bin/Player/StutterFix.dll
```

검사에는 끔 상태 원래 호출, 같음/다름, 외부 상태 변경, 비활성 부모, verify 전수 대조, 의도적으로 두 읽기 사이 값을 바꾼 오류 주입의 원래 쓰기 복귀, 예외 전달, IL label/예외 블록 보존, 호출 0개/2개 거부, 실제 설치 Quartz의 MVID/서명/호출 1개 확인이 포함된다. 오류 주입의 mismatch 1은 복귀 분기 시험이며 실제 게임에서 발견한 불일치가 아니다. 가짜 엔진 저장소·.NET 8 결과이므로 실제 Unity 네이티브 부작용·Mono detour·픽셀·게임 FPS를 검증한 것은 아니다.

최종 로컬 산출물:

- `bin/PlayerAuto/StutterFix.dll` SHA-256: `71986D94207876CB698D7F3A04F45186714846122AAAEC3F0AE96CF8C9E2FB9A`
- `bin/Player/StutterFix.dll` SHA-256: `CA73F5DE40889306DFF78ACD1C9E8B0D7352A7738B595911C9FFA8704F0E3564`
- Debug는 동일한 Player 경로를 사용하므로 최종 Release 빌드로 덮였다. Debug LoadCheck 결과는 로그에 보존했다.

## Claude가 수행할 실제 게임 시험

아래는 **아직 실행하지 않은 절차**다. 현재 진행 중인 다른 게임/측정과 겹치지 않는 다음 시험에서 수행한다. 기존 runner의 설치·정상 종료·설정/파일 복원 절차를 사용한다.

### 1. 부착·비교 조건 확인

AUTOTEST 산출물 하나를 A/B 공통으로 사용한다. 실제 HELLO 2026 파일 경로, Quartz 버전/기능 설정, 해상도, 표시 모드, GPU·StutterFix 설정을 판마다 같게 기록한다. 기존 ProbeNoAlpha/canvasoff 실험 상태도 같은 정상 조건으로 맞춘다. 이번 후보는 `hidejudgements`와 해당 판정 숨김 설정이 활성화되어야 호출된다.

명령은 `quartztrim state`, `quartztrim on|off`, `quartztrim reset`, `quartztrim verify on|off`다. `on/off`는 메모리의 설정도 바꾸므로 runner의 자동 복원 추적을 쓰려면 **`set QuartzFrameTrim true|false`를 우선 사용**한다. 새 Player 설정 필드는 존재하지만 별도 설정창 항목은 추가하지 않았다.

켠 판에서 `sites=1`, `state=attached: judgement SetActive sites=1`이 나오는지 확인한다. 0이면 버전/소유자/로드 순서를 확인한다. 이 상태로 성능 A/B를 성공 처리하지 않는다. 스위치 껐다 켜기 및 StutterFix unload/reload 후에도 부착 수가 중복되지 않고, 끄면 sites=0인지 확인한다. Quartz runtime 자체 교체 후에는 명시적으로 재활성화한다.

### 2. verify와 실제 화면

게임 화면에서는 기존 `game <HELLO 파일 경로>` → `auto on` → `press` 흐름을 사용한다. `play/seek`는 편집기 경로이므로 기존 게임 화면 측정과 섞지 않는다.

켠 상태에서 시작 직전 `quartztrim reset` → `quartztrim verify on`, 구간/전곡 종료 후 `quartztrim state`를 수집한다. **mismatch=0, checks=skipped, skipped>0**을 요구한다. skipped=0이면 비교 경로가 검증되지 않은 것이며, written>0이어도 생략 효과는 없다고 기록한다. 실제 키 입력, 판정 종류/숨김 설정 변경, 다시 하기, 게임 화면/편집기 전환과 Quartz 기능 끔/켬도 별도 검사한다.

화면 검사는 성능 측정과 분리한다. 끔/켬에 같은 곡 시각의 **`shot` 캡처**를 사용한다. 예약할 때는 `shotat <곡 초> <이름>`이 기존 shot 경로로 연결된다. 예를 들어 40초와 실제 기존 병목 구간에서 A/A/B/B 캡처를 남긴다. 로그의 실제 요청 곡 시각·프레임과 오버레이 카운트도 맞춰 본다. 시간/동작 차이가 있는 캡처를 픽셀 동일성 근거로 쓰지 않는다. ScreenSpaceOverlay가 빠지는 framecmp만으로 통과 처리하지 않는다. 기존 maxRGBA<=3 기준을 완화하지 않고, 반복 A/A 잡음과 구분되는 새 차이가 있으면 실패로 남긴다.

### 3. HELLO ABBA 성능

기존 `ab_steps.py`가 사용하는 설정 A/B 방식을 유지하거나, 같은 sfmeasure 계획을 네 번 실행하되 순서를 **A=false → B=true → B=true → A=false**로 명시한다. 이 checkout에서 ab_steps.py 경로는 찾지 못했으므로 임의의 실행 경로는 적지 않았다. 판마다 새 프로세스로 같은 준비/워밍업/측정 범위를 유지한다.

짧은 부착 확인용 명령 배열의 형태는 다음과 같다. `<HELLO 파일 경로>`는 Claude가 실제 파일로 대체한다.

```text
game <HELLO 파일 경로>
auto on
set QuartzFrameTrim false     # B에서는 true
wait 3
quartztrim state
quartztrim verify off
quartztrim reset
press
wait 40
quartztrim state
quit
```

이 40초 배열은 smoke/동일 구간 비교용이며 전곡·중후반 병목 검증을 대체하지 않는다. 최종 판단에는 기존 HELLO 전곡 또는 기존 표본과 같은 구간의 ABBA를 쓴다. verify와 shot은 성능 판에서는 끈다. 평균 FPS, 프레임 시간, 1%/0.1% low, 긴 프레임 수, 판정 숨김 callback 표본, skipped/written을 함께 기록한다. 차이가 판간 변동 이하거나 퇴보하면 효과 없음으로 정리하고 기본 꺼짐을 유지한다. 현재 예상으로는 이 후보만으로 의미 있는 FPS 개선이 나올 가능성이 낮다.

## 설치본 및 병행 작업 관측

이 에이전트는 설치 디렉터리 쓰기, 게임 시작/종료, 커밋/푸시를 실행하지 않았다. 빌드 프로젝트의 실행 target도 인코딩 검사이며 설치 복사 target은 없었다. `.claude`에는 Edit/Write용 자동 build-install 훅이 있지만 이 작업에서는 그 훅을 실행하지 않았다.

그럼에도 시작 스냅샷과 마지막 읽기 사이 설치된 `Mods\StutterFix\StutterFix.dll` 해시가 달라졌다.

- 시작: `12360AC29CD1386FB1B12023E86BC9EE805AA3F912B2C2B943B6596453BF89DE`
- 종료 관측: `2FF9E1C0BCF261186667D3196FCB7C612B0418BE1856ADB5388E90DBDEFDD11F`
- 종료 관측 파일은 이번 최종 Player/AUTOTEST 산출물 어느 쪽과도 해시가 같지 않다. 메타데이터에서 `QuartzFrameTrim` 형식도 0개였다.
- 설치본 LastWriteTimeUtc: 2026-11-22 22:06:45. 게임 PID 5496, 로컬 시작 시각 2026-11-23 07:06:45를 읽기 전용으로 관측했다. 다른 Claude 프로세스들도 있었다. 작성 주체는 확인하지 않았다.
- 함께 기록한 Quartz DLL과 native DLL 3개는 시작/종료 해시가 같았다. HEAD도 변하지 않았다.

따라서 **설치본 전체가 불변이었다고 주장하지 않는다.** 병행 작업의 파일을 되돌릴 수 있어 설치본/게임에 손대지 않았다. `before.json`과 수정된 `integrity.json`에 비교 근거가 있다. Claude는 현재 진행 중인 측정과 이 변경을 구분한 뒤 다음 설치/시험을 진행해야 한다.
