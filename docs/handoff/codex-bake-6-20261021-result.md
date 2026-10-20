# bake-6 결과 — 매끄러운 평가 시계와 seek 재준비

2026-10-21, `E:\StutterFixWork\wt-bake`. 게임 실행·설치·커밋 없이 소스 수정과 오프라인 검사만 수행했다. 그림자 모드이며 게임 출력은 바꾸지 않는다. Player에서 DecoBake/DecoBakeCore 제외, 새 Harmony 패치 없음, `Module.ResolveSignature` 추가 없음.

## 평가 시계

`DecoBakeCore.SongClock`은 `predicted = 이전 tb + tweenDelta * pitch / DOTween.timeScale`, `tb = max(이전 tb, predicted + 0.01 * (raw - predicted))`로 진행한다. tweenDelta는 기존 DecoAnim과 같은 `(smoothDeltaTime 또는 deltaTime) * DOTween.timeScale`이다. 표 평가기가 내부에서 `/pitch * tweenScale`하므로 시계에서는 tween 배율을 제거하고 pitch를 반영한다. 배율 0에서는 예측 진행을 멈춘다.

프레임 델타 누적(원본 Rec)과 곡 시각 기준(굽기)의 절충이다. **Table.Eval/트랙 값 계산은 그대로 무상태이며 입력 시각만 바꿨다.** 고정 오프셋·calibration/addoffset 보정은 적용하지 않는다. 정상 재생 중 단조를 보장하고, 예측과 raw 차이가 50ms 초과이거나 raw가 50ms 초과 되감기면 즉시 raw로 재동기한다. 정지·재개, 초기화, Setup/Prep 세대 변경에서도 재동기한다. 재생 상태는 conductor.hasSongStarted와 controller.paused를 직접 읽는다. 정지 상태/에디터 비재생 미리보기는 raw에 즉시 맞춘다.

기존 DecoAnim UpdatePrefix에서 빈 Rec 조기 반환 전에 프레임당 한 번 진행한다. 캡처 시작 시 초기화하지 않으므로 캡처 전 진행 이력을 보존한다. 테이프는 `SongTime`과 `BakeTime`을 함께 보관하고 verify/활성 벤치는 BakeTime을 사용한다. 장식 setter/Rec/tween은 건드리지 않는다.

## 오프라인 정확도

[계약 검사 로그](bake-evidence/bake6-contract-check.txt): production core를 직접 링크한다. 증거 파일에서 장식 -587466의 frame 22630~22641 12행을 파싱하고 T0=9.9, duration=0.9, linear, 5812.06055→−5647.93945를 사용했다. **첫 raw 시각만으로 cold 초기화했으며 actual/RecPos는 시계 초기화나 보정에 사용하지 않았다.** RecPos는 actual과의 일치 검사에만 사용했다.

| 12행 재생 | raw | tb |
|---|---:|---:|
| 최대 시간 오차 | 11.776410ms | 3.881764ms |
| RMS 시간 오차 | 10.790334ms | 3.449368ms |
| 최대 위치X 원단위 오차 | 149.952957 | 49.427800 |

tb의 표본별 최대 프레임 환산은 **2.034044프레임**이다. 12행은 짧고 cold 시작에 이미 위상 오차가 있으므로 전체 p99≤2프레임 충족의 증거가 아니다. 이 표본에서는 개선만 확인했다. 전체 효과의 시작부터 캡처한 실제 게임 결과는 아직 없다.

| 합성 600초, 480FPS | 최대 시간 오차 | 말미 8,000프레임 평균 오차 |
|---|---:|---:|
| seed 고정 균등 ±6ms | 1.125132ms | −0.013007ms |
| −6~+6ms 버퍼 계단 반복 | 0.080396ms | −0.000002ms |

두 합성 입력에서 단조, 최대≤2프레임(4.166667ms), 말미 평균 절대값<0.2ms를 검사했다. 정지/재개/되감기/재시작/262초 점프 즉시 재동기, pitch=2/tweenScale=0.5 환산도 통과했다. 모든 종류의 오디오 시계 편향을 제거한다는 의미는 아니다. 지속적인 raw 편향은 작은 이득으로 따라간다.

## seek 뒤 ready 복구

Collect가 `count`/`effects`를 저장한 뒤 여러 프레임에 걸쳐 `all[i]`/`effectList[i]`를 읽는데, 기존 크기·세대 검사는 마지막에 있었다. 목록이 yield 사이에서 줄면 마지막 검사에 도달하기 전에 ArgumentOutOfRangeException이 발생할 수 있다. **인계의 `night-results/bake5.log` 원본은 이 체크아웃에 없어 예외 스택의 정확한 발생 지점까지 확정하지 않았다.**

인덱스 접근 전에 크기/세대를 검사하는 production SnapshotItem을 추가했다. 축소/구세대는 SnapshotChangedException으로 구분해 Tick에서 기존 collector/worker를 취소하고 최근 PrepVfx floors로 재준비한다. ArgumentOutOfRangeException도 재준비로 복구한다. floor/plusEffects 열거는 배열 복사로 yield 중 열거자 무효화를 방지한다. 초기 Setup 누락, 미지원 설정 같은 별도 계약 실패는 기존 중단 사유를 유지한다. 세대 변경 중 캡처 테이프는 무효화해 이전 표/새 상태를 섞지 않는다.

fixture는 축소된 목록의 이전 인덱스, 같은 크기이지만 바뀐 세대, 새 세대로 다시 읽기를 검사한다. 실제 seek→ready 전체 경로는 게임 금지로 미검증이다. 복구 시 `[장식굽기/그림자] 재준비: 목록/세대 변경` 다음 `준비됨 (읽기/계산만)`을 확인한다.

## verify 로그와 evalActive

각 속성에 다음 줄을 추가했다:

```text
[장식굽기/그림자] 시간환산 tb 위치X n=... 최대ms=... p99ms=... RMSms=... NA=... 프레임n=... 최대프레임=... p99프레임=... RMS프레임=...
[장식굽기/그림자] 효과시작위상 tb(실제 StartEffect UnityFrame - 테이프 tb T0 통과 UnityFrame) n=... 최소=... 최대=...
[장식굽기/그림자] evalActive 프레임n=... 중앙ms=... p99ms=... 재동기n=... 재동기ms=...
```

시간 오차는 `|actual - Eval(tb)| / |속도|`로 계산한다. 속도는 같은 연속 구간의 tb±0.1ms 수치 미분이며, 단위는 속성단위/곡초다. 구간 경계/즉시 변경/끝점/속도≤1e−6은 NA. 비선형 이징에서는 국소 시간 환산값이며 정확한 역함수 오차가 아니다. 프레임 길이도 `Dt*pitch/tweenScale` 곡초로 맞춰 표본마다 환산한다. 기존 원단위·크기 상대오차·명목 픽셀 지표는 유지하며 이제 tb 평가 결과다.

시작 위상은 tb의 단조 연속 구간에서만 T0 교차를 찾는다. 전체 테이프의 작은 raw 후퇴 때문에 전부 NA로 만들지 않는다. 되감기 뒤 같은 Source의 반복 교차는 실제 stamp와 가장 가까운 프레임을 택한다. 제안 기준은 시작 위상 절대차≤1프레임, 시간 환산 p99≤2프레임이며 이번 작업에서 실게임 충족을 선언하지 않는다. top 진단 행에는 `tb`/`tbBaked`를 병기했다. 기존 오프셋 탐색/시간잔차 진단은 raw 기준으로 유지한다.

evalActive는 tb 되감기에서 기존 ActiveEvaluator의 Reset으로 커서를 재설정하고 계속 측정한다. 되감기/50ms 초과 점프 프레임은 별도 재동기 비용으로 빼고 단조 구간만 집계한다. 초기 구축/첫 seek/Unity setter/렌더는 제외한다. fixture에 카운트다운 되감기와 전진 seek를 섞어 재동기 2프레임을 제외하고 5프레임 측정을 계속하는 검사를 추가했다. 합성 희소 벤치는 로그를 참조하며, 실제 맵/Mono에서 ≤0.5ms는 미검증이다.

## 수집 비용 판단

기존 게임 증거의 최대 185.9119ms는 맵 열기 동안 게임이 같은 프레임에 동기 호출한 Setup postfix 28,835회 중 누적 최대다. 재생 중 평가 시간이 아니다. Setup postfix에서 게임 호출을 프레임 간 yield할 수 없고, 맵 열기는 별도 로딩 구간이므로 이번 요구에서 이 동기 구간의 16ms 분할은 제외한다. Collect의 4ms 협력 예산과 단계별 최대 계측은 유지했다. 단일 게임 API 호출/GC가 예산을 넘을 수 있다. 기존 evalAll 중앙 6.47ms는 전체 858,286구간/지원 트랙 평가이며 활성 프레임 비용으로 해석하지 않는다.

## 검사

- Release 기본/Player/Player AutoTest: 모두 오류 0. [개발](bake-evidence/bake6-build-dev.txt), [Player](bake-evidence/bake6-build-player.txt), [AutoTest](bake-evidence/bake6-build-playerauto.txt). 기존 경고는 로그에 보존했다.
- LoadCheck 3종: FieldRefAccess 개발/AutoTest 266곳, Player 229곳 오류 0, 개발/AutoTest의 DecoBake 포함 및 정확한 기존 후크 계약, Player 제외 확인. [개발](bake-evidence/bake6-loadcheck-dev.txt), [Player](bake-evidence/bake6-loadcheck-player.txt), [AutoTest](bake-evidence/bake6-loadcheck-playerauto.txt).
- [production core 계약 fixture](bake-evidence/bake6-contract-check.txt): 기존 이징/교체/배치/크기/취소 검사와 bake-6 추가 검사 통과.
- [기존 DecoSim fixture](bake-evidence/bake6-decosim-fixture.txt): 12개 통과. 이전 BakeTable/진단 추가를 반영하지 못하던 스텁을 보완하고 production core를 링크했다. 게임 어셈블리 없이 캡처/재생 수명주기를 검사한다.

LoadCheck는 정적 검사이며 실제 Mono 로딩을 대신하지 않는다. 게임·설치·커밋은 하지 않았다. 작업 관찰 스킬은 이 임시 작업 트리에 별도 영구 로그를 만들지 않는 report-back 방식으로 적용했으며, 전달할 새 재사용 관찰은 없다.

## 내가 돌릴 순서

아래 두 순서는 사용자 후속 실행용이다. 이번 작업에서는 실행하지 않았다. 각각 독립적으로 on/open부터 시작한다.

### A. 초반 12초

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

### B. 장식 이동 밀집 구간

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

seek는 타일 선택 뒤 재생 시작이므로 반드시 stop 뒤 사용한다. `seek 262 8`은 262초 자체부터가 아니라 약 254초의 타일부터 재생한다. 새 캡처의 valid, `시간환산 tb`의 n/NA/p99프레임, `효과시작위상 tb`, `evalActive`의 측정 프레임n/p99/재동기 비용을 함께 확인한다. 채널 n=0은 관측 불가이며 통과가 아니다.
