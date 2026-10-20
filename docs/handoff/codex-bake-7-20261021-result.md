# bake-7 결과 (2026-10-21)

같은 맵의 첫 Setup 초기값과 첫 준비 타임라인을 보존해 재굽기에 사용하도록 수정했다. 게임 실행·설치·커밋은 하지 않았다. 실제 재준비 지원 23,631개 유지 및 크기 p99 개선은 아직 게임에서 검증하지 않았다.

## 재준비 지원 수 급감

`bake6heavy-evidence.txt`에서 첫 준비는 지원 23,631 / 이벤트 97,694 / 구간 858,286, seek 뒤에는 지원 34 / 이벤트 64,534 / 구간 332다. 제외 사유 `예약 목록 밖/조건/수동/타격`이 28,767개다. 코드의 Collect는 현재 예약 목록 밖의 floor 효과를 모두 조건 효과처럼 제외한다. seek로 이미 실행한 효과가 예약 목록에서 빠져도 이 경로로 들어간다. 이것이 로그로 확인되는 직접 원인이다. Setup 초기값 덮어쓰기 자체가 이번 급감의 원인이었다고 확정하지 않는다.

- `DecoBake.cs`의 SetupPostfix: 같은 장식의 첫 초기값만 저장한다. PrepVfx postfix의 효과 적용 후 상태를 초기값으로 다시 읽지 않는다. 따라서 기존 Setup 관찰 시점의 pre-ScrubToTime(0) 값이 유지된다. 새로운 패치는 추가하지 않았다.
- 첫 Collect가 성공하면 장식 객체/ID 순서, 전체 이벤트와 타깃, 초기값 및 제외 사유를 보존한다. `wt-tilebake/FloorBake.cs`의 같은 인스턴스 기준 재사용 방식을 참고했다. seek로 소모된 현재 예약 목록에서 전체 타임라인을 다시 추론하지 않는다.
- 재준비는 객체 참조·ID·개수·순서가 모두 같아야 한다. 다른 객체의 동일 ID도 거부한다. 새 객체 Setup/manager 변경/목록 변경은 이유를 로그로 남기고 중단한다. 새 맵 open 및 off는 기준을 해제한다.
- 기준 DTO는 worker에서 깊게 복사해 Compile의 사유 추가로부터 보호한다. Unity 객체는 worker에 전달하지 않는다. 재준비의 지원 수가 최초 완료값과 다르면 게시하지 않는다.
- CancelBuild의 세대 증가·테이프 무효화는 유지한다. SnapshotChangedException 재준비 및 예약 목록 객체 교체에서도 기준은 유지한다. 첫 기준 수집 도중 seek가 개입한 경우 전체 첫 타임라인이 없을 수 있으므로 첫 ready 이후 seek 순서를 지켜야 한다.

`SameInstances`, `CloneDecorations` 및 최초 타임라인 재굽기 fixture는 객체 재생성/순서/개수 변경 거부, 목록 소모 후 지원·구간·과거 값 보존, 배열/제외 사유 복사 분리를 검사한다. 실제 28,835개 목록·23,631개 지원의 게임 실측을 대체하지 않는다. 이벤트 내용이나 장식 설정을 같은 객체 안에서 편집했다면 맵을 다시 open해야 한다. 이 계약은 같은 맵의 seek/PrepVfx이며 실행 중 편집 추적은 아니다.

## 크기 시간 오차: 확인한 의미와 남은 관측

`DecoAnim.Startup`은 키 7/8의 시작값을 첫 Step에서 scaleRef로 읽는다. 교체는 축별 Kill(true) 완료 뒤 진행한다. Core는 이전 축 트랙의 완료 산술값을 새 시작으로 쓰며, 크기는 완료 때도 `start+(target-start)*ease`를 유지한다. 기존 fixture의 축별 교체, Y 미사용 유지, 동일 시각 여러 효과, 길이0, 완료 반올림, DOTween 이징 대조는 통과했다. 단, 첫 Step 전에 서로 다른 예약 시각의 효과가 한 프레임에 몰리는 경우와 실제 Startup 지연은 무상태 표가 완전히 재현한다고 보장할 수 없다.

현재 bake-6 로그만으로 (a) Startup 시작값 불일치, (b) 이징 불일치, (c) 위치/크기 T0 차이 중 하나를 확정할 수 없다. 크기만의 임의 시각 이동이나 시작값 보정은 하지 않았다.

`decobake verify`에 크기 X/Y 원단위 최대 오차를 합쳐 순위화한 **상위 3개 장식** 진단을 별도로 추가했다. 이 장식들의 최대 tb 오차 채널에 대해 다음을 출력한다.

- `크기상위3 진단 장식=...`, 모든 관련 효과 source/T0/startTime/startEffectOffset/duration/ease와 채널별 구간 T0/start/end/target/replacement. 같은 장식의 위치·크기 T0를 직접 비교할 수 있다.
- `진단 Startup`: token/채널/ease/duration/start/target. 캡처 이전 Rec는 최초 저장 상태이고, 캡처 중 시작은 Startup 상태다.
- Rec 시작값과 다른 첫 움직임 관측 프레임 및 최대 오차 주변 최대 12행. 캡처 전 시작의 실제 최초 프레임은 관측 불가로 해석한다.
- 각 행 `tb`, `tbBaked`, `rawDt`, `smoothDt`, `useSmooth`, 국소 속도로 환산한 `시간오차ms/시간오차프레임`. 평탄/경계/불연속은 NA다. 최대 행 선택을 verify 집계와 같은 tb 기준으로 맞췄다.

다음 A 실측에서 Startup start/target/ease/duration을 해당 구간과 먼저 맞춘다. 일치하는데 RecPos로 계산한 시간만 어긋나면 첫 움직임 프레임과 실제 StartEffect/T0를 비교한다. 위치와 크기의 source/T0가 다르면 동일 시간 오프셋을 가정하지 않는다. 시작값이 다르면 특히 교체 직전 완료값과 첫 Step까지의 효과 시작 순서를 추가 관측해야 한다. 새 테이프 전에는 크기 p99 2.67프레임의 해소 여부는 미확정이다.

## 무거운 구간 시계 fixture

SongClock은 현재 `DOTween.useSmoothDeltaTime`에 따라 smoothDeltaTime 또는 deltaTime을 쓰고, DOTween.timeScale을 적용한 뒤 pitch/scale로 곡초를 계산한다. 1% 보정의 시정수는 480FPS에서 0.207290초, 11ms 프레임에서 1.094491초로 약 5.28배 느리다. 이득·50ms 임계는 변경하지 않았다.

262~276초, 기본 11±2ms, raw 관측 ±6ms 지터, 97프레임마다 지연, seed=707의 합성 결과:

| 입력 델타 | 긴 프레임 | n | 최대 ms | p99 ms | 재동기 수 |
|---|---:|---:|---:|---:|---:|
| 실제 프레임 | 30ms | 1258 | 0.653434 | 0.523866 | 1 |
| 실제 프레임 | 60ms | 1225 | 0.653434 | 0.523866 | 1 |
| 비교용 EMA 0.1 | 30ms | 1258 | 19.364241 | 14.657373 | 1 |
| 비교용 EMA 0.1 | 60ms | 1225 | 44.332690 | 37.859489 | 1 |

재동기 1회는 초기화다. EMA는 게임 smoothDeltaTime의 실제 구현 재현이 아니다. 긴 프레임 후 평활 델타가 덜 전진하면 50ms보다 작은 예측차가 여러 프레임 남을 수 있다는 가설을 보여준다. 별도 fixture에서 60ms 실제 전진/11ms 델타의 예측차49ms는 재동기하지 않고, 다음 예측차50ms 초과에는 재동기함을 검사했다. 실제 프레임 입력에서는 60ms 지연도 올바르게 예측하므로 프레임 길이 자체만으로 재동기를 강제할 근거는 없다. 보정 이득을 키우면 raw 지터 전달도 달라진다. 새 B 테이프의 델타·tb·RecPos가 있어야 게임에 맞는 변경을 판단할 수 있다.

## 검증 및 증거

`docs/handoff/bake-evidence/bake7-*`에 원문을 저장했다.

- Release DEV: 오류 0, 경고 38 (`bake7-build-dev.txt`).
- Release Player: 오류 0, 경고 4 (`bake7-build-player.txt`).
- Release Player+AutoTestBuild=1: 오류 0, 경고 38 (`bake7-build-auto.txt`).
- LoadCheck 3종: FieldRefAccess 불일치 0. DEV/Auto 각 266곳, Player 229곳. DEV/Auto 기존 Setup·PrepVfx 정확 계약 각각 1개, Player DecoBake 타입 없음 (`bake7-load-*.txt`). 동적 이름 5곳은 실행 시 확인 영역이다.
- 계약 fixture: 통과 출력 75개 (`bake7-contract.txt`), 실패 없음. `dotnet run --project tools/decobake-contract-check -c Release`.
- DecoSim fixture: passed=12 (`bake7-decosim.txt`). `dotnet run --project tools/decosim-fixture -c Release`.
- `git diff --check` 통과. 변경 코드에 Module.ResolveSignature 사용 없음, 새 Harmony 패치 없음, 그림자 읽기/계산 전용 유지.

추가 기준 DTO 보존에 따른 실제 게임 메모리/재준비 wall 시간은 미측정이다. 복사는 worker에서 수행한다. 합성 fixture 수치는 게임 성능·262~276초 오차 개선의 증명이 아니다. task-observer 3.5.0 관찰: 이번 결과에는 별도 신규 스킬 제안 없이 위 실행 제약과 관측 한계를 남겼다.

## 내가 돌릴 순서

아래는 사용자 후속 실행용이며 이번에는 실행하지 않았다. A/B 각각 독립적으로 시작한다.

### A: 초반 12초

```text
decobake on
open <시험 맵 전체 경로>
select 0
play
wait 1
decobake ready
decosim capture 12 moving
wait 13
stop
decobake verify
decobake bench
decobake off
```

### B: 재준비 후 262~276초

```text
decobake on
open <동일 시험 맵 전체 경로>
select 0
play
wait 1
decobake ready
stop
seek 262 8
wait 1
decobake ready
waitsongpos 264 120
decosim capture 12 moving
wait 13
stop
decobake verify
decobake bench
decobake off
```

두 번째 ready에서 `기준 스냅샷 재사용`과 최초지원=23631, `준비됨 ... 지원=23631`을 우선 확인한다. 재굽기가 필요 없는 seek에서는 기존 표의 같은 지원 수가 유지되며 재사용 로그가 새로 나오지 않을 수 있다. 지원 34이거나 재사용 거부/중단이면 그 캡처를 무거운 구간 대표 표본으로 해석하지 않는다. seek 262 8은 약 254초 타일부터 재생하므로 waitsongpos로 캡처 위치를 확인한다.

새 로그 위치: `DecoBake.cs`의 PrepPostfix/Collect/Tick(기준 재사용·거부·지원 수), Verify의 scaleTop(크기 상위3), Diagnose의 Startup/진단 행(raw/smooth 델타와 시간환산). 정확한 행 번호는 `rg -n '기준 스냅샷|scaleTop|진단 Startup|rawDt' DecoBake.cs`로 찾을 수 있다. A는 위치/크기 시간환산 tb p99프레임·시작위상·evalActive를 함께 비교하고, B는 지원 수 확인 뒤 같은 지표 및 긴 프레임 주변 델타를 읽는다. 표본 n=0 또는 NA는 통과가 아니다.
