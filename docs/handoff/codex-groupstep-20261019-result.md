# GroupStep 작업 결과 — 2026-10-19

구현과 오프라인 검사를 완료했다. **게임 실행·Mods 설치·커밋·푸시는 하지 않았다.** Unity/Mono에서의 비트 일치, Arche의 실제 연속 묶음 비율, 프레임 시간 개선은 미측정이다. 아래 예상 이득은 추정이며 성능 개선을 확정하지 않는다.

기준 HEAD는 `99e2c60b2e458b7f8a156e18d775892c6f181860`(작업 A TightLate)이다. A의 구현 파일 `TightLate.cs`, `MoveApply.cs`, `Dormancy.cs`, `SceneReset.cs`, `Hitch.cs`, `StutterFix.cs`는 수정하지 않았다. 기존 `.codex/config.toml` 변경과 미추적 파일도 유지했다.

## 변경 파일

| 파일 | 변경 |
|---|---|
| `DecoAnim.GroupStep.cs` | 연속 묶음 판정, 공유 진행·이징, 기록별 예외 처리, 표본 검증, 진단·순서 통계 |
| `DecoAnim.cs` | partial 연결, 기존 Apply의 계산·설정 부분을 ApplyE로 분리, 업데이트·Stat·Summary·ResetStats 연결 |
| `AutoTest.cs` | `groupstep` 자동 시험 명령 |
| `StutterFix.csproj` | 새 소스 Compile 항목 |
| `tools/GroupStepCheck/` | 실제 소스를 사용하는 오프라인 상태·순서·예외 대조 도구 |

새 Harmony 패치·이벤트 구독·PlayerLoop 삽입은 없다. 진단 사전은 순회 뒤 Clear하여 장식 참조를 보유하지 않는다. 새 맵의 기존 `DecoAnim.ResetStats`에서 통계도 초기화한다. 빌드·설치 훅과 `/ship`은 호출하지 않았다.

## 묶음 판정과 실행

- 코드 확인: `FastMove`는 대상 장식마다 `PosX(1) → PosY(2) → 시차 오프셋(12,13) → 피벗(3,4) → Rot(5) → ScaleX(7) → ScaleY(8) → 색·불투명도·시차` 순서로 생성한다. `NewRec`는 재사용 기록도 반드시 `recs.Add`로 끝에 넣는다. 따라서 해당 이벤트가 한 장식에 만드는 기록은 코드상 연속이다. 빠진 속성이나 중간의 다른 키는 구간을 나눈다. 완료·취소·Compact 이후 실제 분포는 게임 측정이 필요하다.
- 키 1·2·5·7·8 중 중복 없는 연속 2~5개만 묶는다. 장식 참조, Started, Running, Pos/Dur/Over/Period의 정수 비트, Ease가 모두 같아야 한다. 프레임마다 검사하며 실행 직전에도 변경 여부를 확인한다. 비연속 기록을 앞당겨 실행하거나 인덱스로 재배열하지 않는다.
- `to = Pos + td`, 끝 판정, `ZeroTween.Eval`은 묶음당 한 번이다. 기록별 Startup은 원래 순서대로 실행한다. 특히 ScaleY 시작값은 ScaleX의 설정이 끝난 뒤 읽는다.
- `ApplyE`는 원래 Apply의 키별 식을 옮긴 것이다. 스칼라 `FStart + FChange * e`, 크기의 성분별 계산과 다른 축 읽기, float 연산 순서를 유지한다. `OnUpdate`, 완료 시 Running=false·Completed++·OnComplete도 기록 순서대로 유지한다. Complete/Kill과 Compact의 기존 구현은 바꾸지 않았다.
- 한 기록에서 예외가 나면 그 기록만 멈춘다. 이미 실행한 기록을 재실행하지 않고 나머지는 기존 개별 Step으로 진행한다. 설정 함수가 뒤 기록을 취소하거나 상태를 바꾼 경우에도 변경된 입력으로 개별 처리한다.
- 위치는 **묶음 안에서도 기존 SetPositionX/Y 및 완료 콜백을 각 기록 순서대로 호출**한다. 두 축을 한 번의 setter로 합치면 예외가 난 축과 실행 시점을 분리하기 어려워 이번 묶음에서는 합치지 않았다. 따라서 기존 PairXY에 비해 엔진 설정 호출이 증가할 수 있으며 A/B에서 반드시 확인해야 한다.

`ZeroTween.Eval`은 `easeEval(ease, null, time, duration, over, period)`로 전달한다. 로컬 DOTween.dll을 ILScan으로 확인했으며 EaseManager.Evaluate, Bounce, Flash 경로에는 정적 상태 읽기·쓰기가 나오지 않았다. Math 및 Mathf의 계산과 보조 이징만 호출한다. 사용자 EaseFunction에는 null을 전달한다. 호환성 검사에서 외부 모드가 관련 위치·회전·크기 setter 또는 EaseManager/Bounce/Flash 함수를 패치한 경우 묶음을 끄고 기존 경로로 보낸다. 이 검사는 기존 PairXY와 같이 최초 사용 때 한 번 수행하며 상태에 compatibility를 출력한다.

## 기존 기능과의 결합

| 기능 | 처리 |
|---|---|
| NativeMotion | 현재 epoch의 네이티브 값이 준비된 기록은 묶지 않는다. 기존 Apply(nativeStep=true)와 가능한 PairXY 경로를 유지한다. |
| PairXY | GroupStep이 꺼져 있거나 호환성이 없을 때 기존 동작. 네이티브 준비 기록도 기존 경로. 실제 묶음과 비네이티브 개별 폴백은 기록 순서의 setter를 사용한다. |
| MoveApply / TightLate | 회전·크기에서 기존 DeferTransform 판정을 그대로 사용한다. 위치도 기존 setter를 통하므로 SkipForLate 등 기존 코드가 적용된다. |
| InstantMove / Dormancy | 생성·표시·LateUpdate 판정을 바꾸지 않는다. 투명 장식을 별도 경로로 우회하지 않는다. |
| UpdateEvalMemo | 수정하지 않았다. 개별 경로는 기존 메모 설정을 사용하고 묶음은 Eval 한 번을 공유한다. |
| Dev / Prof | 기존 StepPair와 같이 `Prof == true`이면 묶음을 실행하지 않는다. Dev의 실제 DOTween Shadow 검증은 그대로 유지된다. GroupStep 검증에는 Prof가 false인 PlayerAuto가 필요하다. |

## 파일과 명령

모드 폴더의 **`groupstep-off` 파일이 있으면 로드 때 GroupStep을 끈다.** 없으면 기본 켬이다. 파일 확인은 프레임마다 하지 않는다. 이번 작업에서는 Mods에 파일을 만들거나 설치하지 않았다.

| 자동 시험 명령 | 의미 |
|---|---|
| `groupstep state` 또는 `groupstep` | 상태와 누적 결과 출력 |
| `groupstep verify` | 통계 초기화 후 진단과 1/64 묶음 표본 검증 켬 |
| `groupstep verify off` | 표본 검증만 끔 |
| `groupstep probe` | 통계 초기화 후 검증 없이 진단만 켬 |
| `groupstep off` | 진단·검증을 끔. GroupStep 최적화를 끄는 명령은 아니다. |
| `groupstep reset` | 통계 초기화 |

검증은 Startup 전 입력과 장식의 pivotPosVec·rotAngle·scaleVec를 읽고, 기존 Step과 같은 순서의 별도 계산으로 기준값을 만든다. Eval은 기준 계산에서도 독립 호출한다. 실제 setter는 한 번만 실행한다. 각 기록 뒤 Pos·Started·Stepped·Running·FLast/FChange·VStart/VChange/VLast 및 세 장식 필드를 비트 비교한다. 기록마다 검사하므로 마지막 기록 뒤의 최종 필드도 검사한다. 검증기 자체의 예외는 별도 verifyErrors로 세며 실제 기록의 진행·실패 상태를 바꾸지 않는다.

`checks`, `mismatch`, `verifyErrors`, `first`에 비교 수·불일치 수·검증 실패 수·첫 예의 비트를 출력한다. Dev/Prof, 꺼진 파일, 외부 패치 등으로 `checks=0`이면 통과 증거가 아니다. 정상 플레이에서는 `verifyErrors=0`도 확인해야 한다.

진단 통계는 묶음/프레임·직전 프레임 묶음 수·기록 수·후보 크기 2/3/4/5 분포, 폴백 이유(key/native/state/duplicate/single/exception), LastFrameMs와 동일한 전체 업데이트 범위의 평균 시간이다. **StepGroup만 분리해 잰 시간이 아니라 기존 업데이트 전체 시간**이다. 기록별·묶음별 Stopwatch는 추가하지 않고 기존 프레임 시간의 tick 차를 재사용한다.

진단을 켠 경우에만 64 비어 있지 않은 프레임마다 O(n) 사전 순회로 같은 상태의 기록을 묶어 `census adjacent/separated`와 `[1,2,5,7,8]` 등 실제 목록상의 키 순서를 센다. 이 census는 LastFrameMs 계측 시작 전에 수행하지만 실제 프레임에는 비용이 있으므로 성능 A/B에서는 진단을 끈다. 진단을 끄면 census·표본 계산·추가 타이머·통계 증가·문자열 생성은 수행하지 않으며 제어용 bool 분기는 남는다.

폴백 카운터는 판정 실패 발생 횟수이므로 기록 수와 일대일 대응하지 않는다. 크기 분포는 진입 후보 기준이며, records는 실제 공유 경로로 진입한 기록 수다. 예외 후 개별 폴백 기록은 records에 더하지 않는다.

## 오프라인 확인

`py -3 tools/GroupStepCheck/run.py`는 현재 DecoAnim.cs의 Rec/Startup/ApplyE/OnUpdate/OnComplete/Step을 obj에 추출하고 실제 `DecoAnim.GroupStep.cs`를 링크한다. 실제 DOTween 이징을 호출하며 Unity 장식·setter는 관리 코드 대역이다. 게임 프로세스를 실행하지 않는다.

- 3,240개 대조 사례 통과: 키 순서 6종, DeferTransform 켬/끔, 이징 6종, 양수·음수·완료 경계 dt 5종, setter 예외 위치 9종. 두 번 갱신한 뒤 기록·장식 값 비트, 완료 수·진행 수·예외 수 및 setter 호출 순서가 일치했다.
- Pos/Dur/Over/Period/Started/Running/Ease 상태 불일치 7종 통과. +0/-0도 구분한다.
- native·중복 키·비연속 census·setter가 뒤 기록을 변경/취소하는 경우 통과.
- 1/64 기준 계산의 검사 10회, 불일치 0회. 별도 검증기 예외 주입도 실제 진행 상태를 바꾸지 않는지 확인했다.
- 진단·검증을 끈 뒤 워밍업 후 10,000프레임 추가 관리 할당 0바이트. 이는 대역 환경의 GroupStep 경로에 대한 결과이며 게임 전체 할당 0을 뜻하지 않는다.

세 빌드 모두 오류 0개: 기본 Dev, `-p:Edition=Player`, `-p:Edition=Player -p:AutoTestBuild=1`. 기존 참조 충돌·미사용 필드 등의 경고는 남는다. `git diff --check` 및 변경 텍스트의 strict UTF-8 검사도 통과했다. Mono의 코드 생성·Unity setter와 실제 다른 모드 조합은 이 검사로 확인되지 않는다.

설치 DLL SHA-256은 작업 전후 동일하다: `B0533BBD4835C370BDBB329AB055D63F026B52D734F806C650EE160F5C09B080`. HEAD도 기준 커밋 그대로다.

## 예상 이득 — 추정

인계 표본의 기록 수는 `54,950×5 + 24,106×3 + 67,736 + 2,891×2 = 420,586`, 묶음 수는 `149,683`이다. 같은 비율이 프레임당 기록 4,545개에 적용되고 모두 연속·상태 일치라고 **추정**하면:

`4,545 × 149,683 / 420,586 ≈ 1,618`회의 묶음/개별 진행으로 줄며, 감소량은 약 `2,927`회다. 기록당 중복 오버헤드 0.2~0.4µs를 가정한 단순식은 `2,927 × (0.2~0.4) / 1,000 = 0.585~1.171ms/프레임`이다.

이것은 기존 PairXY·상태 검사·NativeMotion 폴백 비용을 뺀 실측치가 아니다. 현재는 이미 Step 약 2,785 + StepPair 약 880 = 3,665회로 돌고 있다. PairXY의 실측 보정인 장식당 약 0.4µs를 880쌍에 적용하면 약 0.352ms/프레임이며, 이번 묶음의 분리 setter가 이 이득을 잃을 수 있다. 위 단순 추정에서 이를 전부 차감하면 약 0.233~0.819ms지만, 이 역시 거친 추정이다. 실제로는 비연속·상태 불일치·Linear 네이티브 폴백·추가 비교가 더해지고 Eval 자체도 싸므로 이득이 없거나 느려질 가능성도 있다. census와 새 프로세스 A/B로 판단해야 한다.

## Claude의 게임 검증 순서 — 이번에는 실행하지 않음

1. 동일한 PlayerAuto DLL, 동일 해상도·설정·다른 모드 조건을 준비한다. `Main.MeasureBuild=false`여야 하며 `groupstep state`에서 suppressed(Prof/Dev)=False를 확인한다. TightLate 조건도 양쪽에서 동일하게 유지한다.
2. Arche 250~278초를 `groupstep-off` 파일 있음/없음으로 **판마다 새 프로세스** A/B한다. 성능 판에서는 `groupstep off`로 진단을 끄고 같은 구간 프레임 시간·끊김·DecoAnim.UpdateMs/Frames를 비교한다. 같은 DLL reload는 쓰지 않는다.
3. 별도 진단 판에서 파일을 제거한 채 `groupstep verify`를 켠다. 같은 구간 뒤 `groupstep state`로 실제 groups·sizes·census 연속/비연속·키 순서·폴백 이유를 기록한다. checks>0, mismatch=0, verifyErrors=0을 확인한다. 비연속 후보는 현재 개별/연속 부분 처리이며 재배열하지 않는다.
4. Arche 곡 끝 `decosig`를 켬/끔의 새 프로세스 결과끼리 대조한다. 완료 프레임·재시도·중간 시작도 포함한다. 진단 판의 시간은 성능 A/B에 합치지 않는다.
5. HELLO 전곡에서도 표본 검증·곡 끝 decosig·끊김을 대조한다. Dev의 DOTween Shadow 검증은 기존 개별 경로 회귀 확인용이며 GroupStep 검증을 대신하지 않는다.
6. 비트 불일치·검증 예외·성능 악화가 있으면 `groupstep-off` 파일을 둔 새 프로세스로 되돌리고 해당 first·census·폴백 수를 남긴다. 특히 위치 setter 증가로 PairXY의 기존 이득을 잃는지 확인한다.

이 문서는 apply_patch로 UTF-8 BOM을 포함해 작성했다. 최종 검사는 BOM 바이트 `EF BB BF`, strict UTF-8 디코딩, `Get-Content -Encoding UTF8`의 한글 본문으로 확인한다.
