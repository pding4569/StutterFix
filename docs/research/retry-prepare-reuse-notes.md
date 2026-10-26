# 게임 화면 다시 하기: 수집·표 재사용

구현일: 2026-10-26. 작업 지시: `docs/handoff/retry-prepare-reuse.md`.

## 구현

정상 복원을 마친 게임 화면 라운드의 `EndRoundPlayerKeep()`는 다음 참조를 그대로 보존한다.

- `baselineObjects`, `baselineIds`, `baselineDecorations`, `baselineEvents`
- 이미지 예약용 `baselineVisualFlags`, `baselineImageSelectors`
- `table`, `baselineSupported`, `baselineSortedCount`
- `effectList`, `prepEffectSnapshot`, `prepFloors`

기존 `initial`(처음 Setup 기록), GPU 버퍼·이미지 계획·준비 해시도 유지한다. 배열이나 표를 복제해서 보관하지 않는다. 종료 시 `CancelBuild()`는 계속 실행하므로 준비 상태, 시계, 세대 및 진행 중 작업은 새 라운드 경계에서 초기화된다.

다음 `PreparePlayer()`가 재사용 조건을 통과하면 `BeginPrepCollection()`과 worker 표 컴파일을 모두 생략하고 기존 표를 준비 상태로 되돌린다. PrepVfx가 효과 목록의 List만 바꿨다면 현재 목록에 다시 연결한다. 기존 GPU 자원 재사용, 통합 계획 재구성 및 검증은 유지한다. 활성화 시 현재 시작 상태를 읽는 경로도 유지한다.

조건이 맞지 않으면 기준 배열과 표를 분리한 후 전체 수집한다. 기준 배열이 남아 있으면 `Collect()`가 기존 스냅샷만 재컴파일하므로, 이 분리는 폴백과 검증 양쪽에 필요하다. 전체 수집 뒤 기존 `PlayerSetupHash()` 검사로 GPU 자원의 재사용 여부를 따로 결정한다.

`ClearBaseline()`과 `BeginPrepCollection()`은 재사용 식별값을 무효화한다. 보존 중 새 장식 객체의 Setup이 오면 보존 스냅샷을 먼저 분리하여 새 객체의 초기값 수집을 허용한다. 기존 객체의 `initial`은 덮어쓰지 않는다. `ReleasePlayerLevel()`은 식별값과 모든 보존 스냅샷을 버리는 기존 해제 경로를 따른다.

## 재사용 조건

다음 조건을 모두 만족해야 한다.

- `scnEditor.instance`가 없는 게임 화면이며, 직전 성공한 준비 결과를 정상 종료 시 보존했음.
- 같은 `ADOBase.customLevel`(`scnGame`), 같은 `levelData`, 같은 장식 관리자 참조.
- 장식의 개수·객체 참조·순서·인스턴스 ID가 같으며 파괴된 객체가 없음. `gpuLiveBaseline`도 같은 장식 순서임.
- `initial`, 기준 배열, 표, 이미지 예약 정보 및 전체 효과 예약 스냅샷이 존재함.
- GPU 준비 결과, 전체 곡 범위 및 준비 해시가 유효함. `gpuRecoveryBlocked`가 아니며 `gpuLastRestoreComplete`가 참이고 수집 실패 상태가 아님.
- 효과 예약 개수와 모든 효과 객체의 참조·순서가 같음. 효과 객체가 새로 만들어지면 보수적으로 전체 수집함.
- 아래의 가벼운 FNV 지문이 같음.

지문은 레벨 이벤트/장식 데이터 목록의 참조와 개수, 각 목록에서 양끝을 포함해 최대 16개 이벤트의 참조·floor·eventType·disabled 사전 참조/개수·angleOffset·duration을 포함한다. 타일은 전체 개수 및 모든 타일의 참조·seqID·entryTime·plusEffects 개수를 읽는다. 카메라 초기 줌, tileSize, 시각 품질, 공식/Miko 구분, 곡 pitch, DOTween 배율/이징 기본값, 이미지 옵션 및 예약 효과 개수도 포함한다. 지문 계산에 실패하면 전체 수집한다.

복원 성공 여부는 `StopGpuLive()` 전후에 모두 확인한다. 비활성 상태의 Stop 호출이 복원 플래그를 다시 참으로 설정해 과거 복원 실패를 가리는 것을 방지한다. 종료 후크가 반복되어도 보존 결과는 유지되고, 재사용 준비를 시작하면 해당 경계를 한 번 소비한다.

## AUTOTEST 명령

```text
gpudeco light prepreuse on
gpudeco light prepreuse off
gpudeco light prepreuse verify
```

- 기본값 `on`: 조건을 만족한 다음 게임 화면 다시 하기부터 수집·표를 재사용한다.
- `off`: 다음 준비에서 전체 수집·컴파일한다. 기존 GPU 자원 재사용 기능과는 별개다.
- `verify`: 재사용을 켜고, 조건을 만족하는 각 준비에서 전체 수집·컴파일을 추가로 한 번 수행한다. 최초 준비 또는 조건 불일치는 일반 전체 수집이므로 비교 로그가 없다.

검증 시 보존 결과의 `PlayerSetupHash()`, `baselineObjects` 순서, `table.Decorations.Length`, Initial 값의 비트 해시를 독립 수집 결과와 비교한다. `N`은 다른 비교 항목 수(0~4)다.

```text
[GPU 장식] 준비 재사용 검증: 같음 0
[GPU 장식] 준비 재사용 검증: 다름 N
```

같으면 원래 보존한 표와 배열로 계속 진행한다. 다르면 재사용 및 verify를 자동으로 끄고, 방금 전체 수집한 표를 폴백으로 사용하며 GPU 자원과 이미지 계획도 다시 준비한다. 명시적으로 `on` 또는 `verify`를 입력하기 전까지 자동으로 재사용을 켜지 않는다. 수집 자체가 예외로 실패하면 기존 제품 준비 실패 처리로 자원을 해제하고 원본 장식 경로를 유지한다.

준비 시간 로그에는 CPU 수집·표를 건너뛴 경우 `(수집·표 재사용)`을 표시한다. `verify`는 독립 수집 비용과 쓰레기를 의도적으로 추가하므로 시간·힙 목표 측정은 `on`으로 해야 한다. `off` 상태에서 이미 스냅샷을 버렸다면 다시 켠 후 한 번 전체 준비와 정상 종료를 거쳐야 재사용할 수 있다.

## 실행한 검증

게임 실행, DLL 설치, 커밋은 수행하지 않았다. `GpuDecoPlayer.Prepared()`와 `GcControl.cs`는 이 작업에서 수정하지 않았다. 공유 작업 폴더에서 다른 작업자가 추가한 종료 지연/시작 조건 변경은 보존했다.

| 검사 | 결과 |
| --- | --- |
| `dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1 -v q --nologo` | 성공, 오류 0개, 경고 47개 |
| `dotnet build -c Release -p:Edition=Player -v q --nologo` | 성공, 오류 0개, 경고 46개 |
| `dotnet run --project tools/optimization/RetryPrepReuseCheck` | 59개 검사 통과, 오류 0개 |
| `git diff --check` | 통과 |

`RetryPrepReuseCheck`는 빌드할 때 실제 `GpuDecoPlayer.cs`의 해시/지문/재사용/verify/라운드 종료 메서드를 추출해 컴파일하고, 실제 `DecoBake.Core.cs`도 연결한다. 게임·Unity 경계와 수집 결과 공급부만 스텁이다. 정상 재사용의 표/배열 참조 유지, 목록 재연결, 조건 변경 거부, 객체 파괴, 예약 순서 변경, 독립 검증 일치/불일치, 자동 해제, 반복 종료 및 복원 실패를 검사한다. 이 검사는 게임 DLL을 참조하거나 게임을 실행하지 않는다. 전체 PrepVfx·Unity 수집·GPU 재준비를 끝까지 실행한 검증은 아니다.

로그: `out/retry-prepare-reuse-autotest-build.log`, `out/retry-prepare-reuse-player-build.log`, `out/retry-prepare-reuse-check.log`.

추가로 실행한 기존 `GpuDecoPlayerCheck`는 `measured defaults unchanged`에서 실패한다. 테스트는 `DisableOwnedRenderers=false`를 기대하지만 현재 HEAD의 기본값은 이미 `true`다. 이번 변경에서 해당 정책과 기존 테스트는 수정하지 않았다. 저장소 전체 인코딩 검사는 기존 `out/image-matrix-candidate/scrDecoration.cs`의 UTF-16 인코딩 때문에 UTF-8 디코딩 예외로 중단된다. 해당 파일은 변경하지 않았다.

## 남은 위험과 측정 범위

- **준비 300ms 이하·힙 증가 50MB 이하 달성 여부는 미측정이다.** 이번 지시에 따라 게임을 실행하지 않았으므로 Arche 실제 다시 하기의 시간, 힙, 프레임 및 픽셀 동일성은 확인하지 않았다.
- 지문은 전체 레벨 직렬화나 모든 효과 필드의 깊은 비교가 아니다. 같은 객체와 개수를 유지한 채 표본 밖 이벤트, 태그 또는 효과 필드를 다른 모드가 제자리에서 수정하면 놓칠 수 있다. 64비트 해시 충돌 가능성도 남는다. 편집기는 항상 전체 수집하며, 진단할 때는 `verify`를 사용한다.
- 효과 객체/타일이 다시 만들어지는 게임 경로에서는 재사용 조건을 통과하지 못해 기존 비용이 남는다. 이 경우 재사용 로그와 verify 결과를 먼저 확인해야 한다.
- 수집·컴파일의 반복 할당을 없애는 대신 기준 배열과 표를 라운드 사이에도 유지하므로 살아 있는 관리 힙의 크기는 더 클 수 있다. 레벨 해제 시에는 보존 참조도 해제한다.
- 통합 계획 재구성, GPU 준비·복원, 활성화 및 다른 모듈의 할당 비용은 남는다. 이 구현만으로 실제 지연/힙 목표의 충족을 보장하지 않는다.
