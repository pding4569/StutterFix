# bake-5 결과 — 2026-10-21

코드와 오프라인 검증을 수행했다. **153.716797 단위 오차의 원인은 확정 못 했다.** 기존 로그에는 프레임별 실제값·Rec 진행값·효과 시작 기록이 없어, 시간 오프셋을 복원할 수 없다. 임의 보정은 넣지 않았다. 게임 실행·설치·커밋은 하지 않았다. 그림자 모드와 기존 게임 평가 경로를 유지했고 일반 Player에서 개발 타입을 제외했다.

완료 범위는 진단 출력, 지원 활성 Rec 우선 표본, 수집 단계 계측, 활성 평가 벤치와 fixture다. **위치 ≤0.01px, 시작 위상 ≤1프레임, 메인 slice ≤16ms, Arche evalActive ≤0.5ms는 미검증이다. 특히 수집 16ms 분할 요구는 미완료다.** 기존 Collect의 4ms 협력 분할을 유지했으며, 동기 Setup 호출 묶음과 단일 태그 API 호출을 프레임 간 분할하지 못했다.

## 시간·값 해석을 확인한 근거

설치 DLL을 실행하지 않고 ILSpy로 읽었다. DLL SHA256은 `F692210F9A342F7EED4BE40AAD463286AB40BF046599959823942EA02F4CA3FF`다. 발췌에는 원래 디컴파일 행 번호가 붙어 있다.

- [scrVfxPlus.Update](bake-evidence/bake5-scrVfxPlus.cs.txt): 일시정지/곡 시작 전에는 반환하고, `songposition_minusi >= startTime-startEffectOffset`에서 StartEffect를 호출한다. 굽기 T0와 같은 식이다. 이 식을 production `ScheduledTime`으로 모아 fixture에 연결했다.
- [ffxPlusBase.SetStartTime/ScrubToTime](bake-evidence/bake5-ffxPlusBase.cs.txt): startTime은 바닥 entryTime과 각도/박자 오프셋으로 정해진다. 별도의 calibration/addoffset 항은 없다. 반면 중간 시각으로 스크럽할 때는 `(t-startTime)/pitch-calibration_i`를 Goto에 넘긴다. 일반 재생 시작과 스크럽의 진행 초기화는 같은 경로가 아니다.
- [scrConductor](bake-evidence/bake5-scrConductor.cs.txt): DSP 경로의 곡 시각은 `float(dspTime-dspTimeSong-calibration_i)*pitch-addoffset`이다. `dspTimeSongPosZero=dspTimeSong+addoffset/pitch`는 절대 DSP 원점이다. 절대값을 곡 초에 그대로 더할 근거가 없다. countdown 여부와 에디터의 중간 시작은 DSP/오디오 원점 설정에 관여한다. 구형 conductor 식도 다르므로 실제 관측이 필요하다.
- `DecoAnim.UpdatePrefix/Step`은 Unity dt 또는 smooth dt에 DOTween.timeScale을 곱해 float Rec.Pos에 누적한다. 표는 곡 시각 차이를 pitch로 나눈다. 실제 시작 프레임·Unity와 DSP 시계·스크럽 이력이 같다는 증거가 없어 두 값의 일치를 단정할 수 없다.
- 위치의 실제 비교값은 Rec 위치축 출력이며 `pivotPosVec`를 갱신한다. 목표는 Global/Tile 초기 `startPos`에 targetPos를 더하거나 LastPosition의 현재값에 더한다. 명시적 Global 전환은 원점을 다시 설정한다. 기존 production fixture의 Tile 원점·Global 전환·LastPosition·Kill(true) 교체 검사는 유지했다. 실제 문제 장식의 원점/시작값이 맞는지는 새 진단 로그가 필요하다.

## 추가한 진단과 필요한 다음 관측

`decobake verify`는 원단위 최대 오차 상위 3개 장식을 선택한다. 각 장식의 모든 입력 효과에 T0/startTime/startEffectOffset/duration/ease/상대위치/Global 전환, 채널별 시작·끝·목표·교체 여부를 출력한다. 관측된 StartEffect 프레임·곡 시각도 Source ID로 연결한다.

Rec 시작값과 다른 첫 관측 프레임·곡 시각, 최초 관측 Rec.Pos, 동일 token의 첫 값 변화, 최대 오차 채널 부근 최대 12개 행을 출력한다. 행에는 UnityFrame/songposition_minusi/actual/baked/dt/RecPos가 있다. dt는 해당 Rec.Step에 실제 사용한 직전 프레임 델타이며 DOTween 배율을 포함한다. 캡처 전에 이미 움직인 Rec는 좌측절단으로 명시한다. 그러한 Rec의 진짜 최초 이동 프레임은 이 캡처로 알 수 없다.

상수 오프셋은 `baked(songposition_minusi+offset)` 대 실제값의 제곱오차를 최소화한다. 최악 채널의 최대 512개 균등 간격 표본으로 [-32,+32]곡초를 0.125초 간격 탐색하고 세 차례 세분해 최종 0.000125초 격자를 쓴다. 전체 표본의 값 RMS/최대 잔차도 별도로 계산한다. 격자 탐색의 근사 최소이며 반복·평탄 구간의 유일한 해를 보장하지 않는다. 평탄 손실은 0을 유지한다.

초 단위 잔차는 시작/목표/길이/ease가 일치하는 진행중 Rec만 골라 `(song+fitOffset)-(T0+RecPos*pitch/tweenScale)`의 RMS/최대를 낸다. 일치 표본이 없으면 관측 불가다. 같은 모양의 반복 효과는 Source 대응이 모호할 수 있다. 캡처 당시 calibration_i, addoffset, pitch, DSP 원점, countdown 길이/사용 여부와 editor 여부를 저장하고 오프셋과의 차이를 출력한다. 값 일치만으로 인과를 확정하지 않는다.

다음 관측에서 확인할 순서:

1. 유효하고 단조인 264초 이후 테이프에서 top 장식의 실제 StartEffect `song-T0`가 1프레임 이내인지 확인한다.
2. 실제/굽기의 시작·목표와 원점을 비교한다. 시작/목표가 다르면 시간 보정보다 값 해석을 먼저 조사한다.
3. 장식별 최적 오프셋과 초 잔차가 공통인지 확인하고 calibration*pitch, addoffset, countdown과 비교한다. 단지 수치가 비슷한 것은 원인 확정이 아니다.
4. 단조 구간에서도 Rec.Pos와 곡 시각의 진행 차이가 커지는지 확인한다. 필요하면 별도 캡처에서 StartEffect 직전/직후 conductor 및 DOTween 실행 순서와 실제 DSP/Unity dt를 추가 관측해야 한다. 새 로그 전에는 보정하지 않는다.

## 표본 확대

`decosim capture 12 moving`을 추가했다. 인덱스 4분의 1 추출 대신 **굽기 지원 장식의 활성 Rec를 Key별 최초 32개 장식 슬롯까지** 고른다. 캡처 도중 생긴 Rec도 대상이다. 불투명도·회전 등 드문 Key를 위치가 밀어내지 않는다. 색은 RGBA, 시차는 XY를 함께 비교한다. 슬롯 선택은 캡처 동안 유지하므로 계속 교체되는 첫 장식이 표본을 독점할 가능성은 남는다. 표본 상한과 기존 512MiB/프레임/슬롯 상한을 유지한다.

모든 채널의 n>0을 보장할 수는 없다. 그 구간에 지원 활성 Rec가 없는 채널은 `관측 불가(활성 지원 Rec 없음)`라고 출력한다. 실제 n은 게임 금지로 아직 측정하지 않았다. 기존 `capture 12` 동작은 유지했다.

## 수집과 메모리 비용

수집 단계 CPU 합과 단계별 한 Tick 최대 시간을 추가했다: Setup, 장식 복사, 효과 읽기, 태그 열거, 조건 효과, worker 전달. Setup은 동일 프레임 누적 최대를 별도 출력한다. 게임의 lazy 태그 열거 API는 그대로 쓰며 단일 MoveNext 최대도 측정한다. 완료 시 계측값을 보관하므로 stop 뒤 Setup 카운터가 초기화되어도 bench 값이 사라지지 않는다.

기존 183.6527ms의 주범은 옛 로그만으로 구분할 수 없다. Collect는 이미 4ms마다 양보하지만 게임이 같은 프레임에 Setup을 연속 호출하면 이 postfix에서 yield할 수 없다. lazy Distinct의 한 MoveNext가 중복 원소를 많이 통과하거나 GC가 발생해도 16ms를 넘을 수 있다. **이번 변경은 이 문제의 측정 준비이며 16ms 해결은 아니다.** 새 최대치가 Setup에 몰리면 초기값을 보존하면서 게임의 Setup 호출 자체를 분산하거나 초기값 수집 방식을 재설계할 근거가 필요하다. 기존 경로를 변경하는 작업은 이번에 하지 않았다.

옛 payload 45.4490356MiB는 다음 산식과 정확히 일치한다.

| 항목 | 산식 | 바이트 |
|---|---|---:|
| 구간 | 858,286 × 48B | 41,197,728 |
| 조밀 트랙 참조 | 28,835 × 16 × 8B | 3,690,880 |
| 장식 추정 | 28,835 × (16 × 4 + 32)B | 2,768,160 |
| 합계 | | 47,656,768 |

fixture에서 Segment의 unmanaged stride 48B를 확인했다. payload는 실제 heap 총량이 아니다. 비어 있지 않은 트랙별 배열 header, 장식/Reasons 객체, 임시 list/복사, GC, 다른 게임 할당을 포함하지 않아 heap +292MiB와 직접 일치하지 않는다. 상세 진단 때문에 이번에는 InputEvents/targets/Values도 보유한다. bench에 이 입력 개수·대상 참조 개수·배열 payload와 비어 있지 않은 트랙 수를 별도로 출력한다. header는 런타임 의존이므로 정확한 heap 크기라고 쓰지 않았다.

줄일 후보는 채널 희소 인덱스로 16개 참조를 줄이는 것, Source/ease/bool을 별도 메타데이터로 묶어 48B stride를 줄이는 것, 정밀 진단 뒤 InputEvents를 해제하는 것이다. float 시각 축소나 Target/End 통합은 경계 정밀도와 scale 이징 끝값을 바꿀 수 있어 적용하지 않았다. 실측 전에는 17MB 도달을 약속할 수 없다.

## eval 비용과 검증

기존 eval은 지원 장식 전체의 16채널을 각각 이진 탐색하는 33회 시험이며 첫 회를 버린다. 시각은 `pass*2.125`초다. 따라서 옛 중앙 6.5975ms/p99 6.8559ms는 프레임 활성 비용이 아니다. bench에 이 정의를 명시했다.

새 evalActive는 같은 표의 유효한 완료 테이프의 프레임 곡 시각을 사용한다. 채널별 커서, 시각순 예약 dispatch, 활성 채널만 평가하며 완료값은 한 번 평가한 뒤 제거한다. 최초 scheduler 구축·첫 seek를 제외하고 이후 dispatch와 완료 처리를 측정에 포함한다. 비단조 테이프는 측정을 거부한다. Unity setter/렌더 비용은 포함하지 않는 그림자 계산 시험이다. 구축용 정렬 예약 배열과 커서는 bench 지역 객체이며 프레임 경로에는 연결하지 않았다.

| 확인 | 결과 | 근거 |
|---|---|---|
| Release 개발 빌드 | 오류 0 | [로그](bake-evidence/bake5-build-dev.txt) |
| Release Player 빌드 | 오류 0 | [로그](bake-evidence/bake5-build-player.txt) |
| Release Player AutoTest 빌드 | 오류 0 | [로그](bake-evidence/bake5-build-playerauto.txt) |
| LoadCheck 3종 | 개발/AutoTest 타입 포함, Player 제외; FieldRef 오류 0 | [개발](bake-evidence/bake5-loadcheck-dev.txt), [Player](bake-evidence/bake5-loadcheck-player.txt), [AutoTest](bake-evidence/bake5-loadcheck-playerauto.txt) |
| production core fixture | 기존 3,465개 이징 중간값 및 교체/원점/시계 검사 통과; +0.75초 복원, 평탄 손실, 활성 커서 예약/교체/완료/즉시/되감기 검사 추가 | [로그](bake-evidence/bake5-contract-check.txt) |
| 합성 .NET8 evalActive | 720프레임, 중앙 0.035500ms / p99 0.055800ms, 평균 1,016.672채널 | 같은 fixture 로그; Arche/Mono 성능 증거 아님 |

빌드 경고는 별도 로그에 남겼다. LoadCheck는 정적 .NET 메타데이터 검사이며 실제 Mono 후크/캡처 실행의 증거가 아니다. Module.ResolveSignature를 추가하지 않았다.

## 내가 돌릴 순서

아래는 사용자 후속 실행용이며 이번 작업에서 실행하지 않았다. AutoTest.cs에서 `seek`는 타일 선택 후 재생 시작이다. 이미 재생 중이면 `이미 재생 중`으로 반환하므로 처음 `play` 뒤 **stop을 먼저 한다**. seek 262 8은 262초 자체가 아니라 약 254초의 타일부터 재생한다. seek로 Setup/Prep가 다시 실행될 가능성에 대비해 ready를 다시 기다린다.

```text
decobake on
open <Arche FULL.adofai의 전체 경로>
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

`waitsongpos`는 곡 시계가 목표 이상이고 재생 중이어야 통과한다. 최대 대기 120초를 명시했다. 캡처는 wall 12초이므로 종료 후 로그의 valid/실제 곡 시각 범위도 확인해야 한다. 새 테이프에서 모든 채널 n과 관측 불가 채널, top3 진단, 시작 위상, 수집 단계별 최대, evalActive p99를 함께 받아야 남은 문제를 수정할 수 있다.
