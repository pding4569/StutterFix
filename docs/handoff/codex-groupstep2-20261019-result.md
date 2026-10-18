# GroupStep v2 작업 결과 — 2026-10-19

위치 X·Y 합치기 복원, 회전·크기 경로 단순화, `groupstep bench`, 오프라인 대조를 구현했다. **게임 실행·Mods 설치·커밋·푸시는 하지 않았다.** 기본 끔과 `groupstep-on` 파일로 켜는 방식을 유지한다. Unity/Mono의 실제 성능 개선과 엔진 포함 1.0µs 예산 달성은 미확인이다.

기준 HEAD: `d4b25004465d354daf0750fb581230af53e59dfc`. 이전 작업 파일은 되돌리지 않았다. 기존 `.codex/config.toml` 변경과 미추적 파일을 유지했다. 아래 `run.py`와 csproj도 시작 시 이미 미추적이었으며, csproj는 수정하지 않았다.

## 변경 파일

| 파일 | 변경 |
|---|---|
| `DecoAnim.GroupStep.cs` | GroupPair, 회전·크기 전용 GroupApply, 페어 입력·최종 상태 검증, 명령형 벤치 |
| `DecoAnim.cs` | 묶음 판정이 단독으로 끝나도 기존 PairXY 판정으로 이어짐 |
| `tools/GroupStepCheck/Program.cs` | 페어·예외·진행 중 상태·setter 변경 대조, 관리 코드 벤치 |
| `tools/GroupStepCheck/run.py` | 현재 소스에서 기존 StepPair도 추출하여 기준 경로로 사용 |
| 이 문서 | 결과와 후속 게임 확인 순서 |

새 Harmony 패치·정적 이벤트·PlayerLoop 변경은 없다. 빌드·설치 훅과 ship은 호출하지 않았다.

## 위치 쌍과 실행 순서

- 같은 묶음의 **나란한 키 1→2**이며 PairXY가 켜져 있을 때 위치를 합친다. 묶음 판정은 같은 장식 참조, Started/Running, Pos/Dur/Over/Period의 비트, Ease를 확인한다. `setPos` 존재와 기존 PairAllowed도 확인한다.
- 두 기록을 Startup한 뒤 공유 진행값·이징값으로 각각 `FLast = FStart + FChange * e`를 계산한다. `x = done ? r.FEnd : r.FLast`, `y = done ? n.FEnd : n.FLast`와 당시 pivotOffset을 **한 번의 setPos**로 전달한다. 성공한 뒤 Steps 두 개, PairSteps 하나를 세고, 완료이면 각 기록의 Running과 Completed를 처리한다. 위치 OnUpdate/OnComplete의 별도 축 setter는 호출하지 않는다.
- 정상적인 키 `[1,2,5,7,8]`의 순서는 위치 쌍 → 회전 → 크기 X → 크기 Y다. 키 사이에 다른 기록이 있으면 위치 쌍을 앞당기지 않는다. ScaleY의 Startup은 ScaleX 설정이 끝난 뒤 scaleVec를 읽는다.
- 합친 위치 설정이 실패하면 진행 필드(Pos/FChange/FLast/Started/Stepped)를 복원하고 각 축을 별도의 기존 Step 안전 경계로 처리한다. 한 축이 실패해도 다른 축과 나머지 기록은 진행한다. 이미 실패한 합친 setter가 일부 엔진 값을 쓴 경우 그 부수 효과를 되돌리는 트랜잭션은 아니다. 성공한 선행 기록을 재실행하지 않는다.
- 단독 후보 또는 두 축의 시계·이징 불일치로 묶을 수 없는 경우에도 기존 StepPair로 이어질 수 있게 했다. v1처럼 그 자리에서 개별 Step으로 확정하지 않는다.

## 관리 코드 비용과 수명

영속 그룹 슬롯·별도 그룹 목록은 도입하지 않았다. 인계 문서의 허용된 대안인 프레임별 판정을 유지한다. GroupCount는 최대 다섯 개만 보며 네 float의 정수 비트와 참조·상태·이징·키 중복을 비교한다. StepGroup의 첫 기록은 방금 판정한 상태이므로 같은 비교를 반복하지 않는다. setter가 뒤 기록을 바꿀 수 있으므로 나머지 기록은 실행 직전 재확인한다.

회전·크기는 ApplyE → OnUpdate → OnComplete의 여러 switch 대신 전용 GroupApply에서 계산하고 설정한다. 회전 완료 시 마지막 값 설정 → Running/Completed → FEnd 설정의 기존 순서, 크기별 다른 축 읽기, 각 DeferTransform 검사 횟수·시점은 유지한다. 프레임마다 새 객체·람다·박싱·문자열·별도 Stopwatch는 추가하지 않았다. 정상 프레임에서는 기존 ZeroTween.Eval을 묶음당 한 번 공유한다.

코드 확인: KillPrefix(false)는 Running을 끄고, KillPrefix(true)·CompletePrefix는 Complete로 끝낸다. Complete는 Startup/Pos/Running/Stepped를 바꾼다. KillAllPrefix는 복사본을 처리한 뒤 목록을 비우며, NewRec 재사용은 상태를 초기화하고 목록 끝에 추가한다. Compact는 살아 있는 기록의 순서를 유지하고 죽은 기록의 InList를 해제한다. 이 경로들은 수정하지 않았다. 영속 그룹 참조가 없으므로 별도 무효화가 필요 없고, 다음 순회에서 현재 기록 상태로 묶음을 판정한다. 실제 게임에서 이 경로들의 조합 검증은 남아 있다.

NativeMotion으로 현재 값이 준비된 기록은 기존 경로로 보낸다. 외부 setter·이징 패치에 대한 기존 호환성 검사도 유지한다. Dev/Prof의 실제 업데이트에서 GroupStep을 억제하는 조건은 변경하지 않았다. 정적 초기값도 false로 맞췄으며 ConfigureGroup은 계속 `groupstep-on` 존재만 읽는다.

## 검증

`py -3 tools/GroupStepCheck/run.py` 실행 결과:

- 실제 소스의 Rec/Startup/ApplyE/OnUpdate/OnComplete/Step/StepPair를 추출하고 실제 GroupStep 파일을 링크한다. 이징은 게임의 DOTween.dll, 장식·setter는 관리 코드 대역이다. 게임 프로세스는 실행하지 않는다.
- PairXY 끔: 개별 Step 대비 3,240건 통과. 순서 6종, Defer 켬/끔, 이징 6종, dt 5종, setter 실패 위치 9종에서 두 번 갱신한 기록·필드 비트, 완료·진행·예외 수, 호출 순서를 비교했다.
- PairXY 켬: 기존 StepPair + Step 대비 300건 통과. 순서 6종, Defer 켬/끔, 이징 5종, dt 5종을 비교했다.
- 추가 통과: 두 축의 서로 다른 시계·이징에서 기존 페어 유지, 이미 Startup된 기록, 음수 dt·완료 경계, setter가 뒤 기록의 Pos/Running을 바꾸는 경우(개별 및 페어), 합친 setter 실패 후 한 축 예외의 개별 폴백, 상태 불일치 7종(+0/-0 포함), 네이티브·중복 키·비연속 census, 검증기 예외 격리.
- 1/64 검증 대조: 개별 경로 검사 10회, 페어 포함 검사 12회, 각각 mismatch=0. 페어의 setPos 입력(x,y,off)과 기록·최종 필드를 검사한다. 완료 페어 입력은 두 FEnd이며 오프셋 대역도 0이 아닌 값으로 확인했다.
- 진단 끔 10,000프레임의 추가 관리 할당 0바이트. 페어 벤치 측정 구간의 추가 관리 할당도 0바이트.

`groupstep verify`는 기존처럼 진단 및 1/64 표본 검증을 켠다. 페어는 독립 Eval로 두 기록의 기준값을 구하고, StepPair의 최종 x/y를 조합해 실제 호출 직전의 입력과 비교한 뒤 두 기록의 최종 필드를 검사한다. 표본 검증이 실패하거나 예외가 나도 실제 애니메이션을 재실행하지 않는다. checks>0, mismatch=0, verifyErrors=0이 모두 필요하다. 검증을 켠 판의 시간은 성능 판과 분리한다.

## 벤치와 예산

오프라인 관리 코드 대역, .NET 8 Release, 키 `[1,2,5,7,8]` 2,150묶음, 측정당 200프레임·7표본의 마지막 실행:

| 경로 | 중앙값 µs/묶음 | 최소~최대 µs/묶음 |
|---|---:|---:|
| GroupCount + StepGroup v2 | 0.2040 | 0.1817~0.3435 |
| 기존 StepPair + Step 3개 | 0.1794 | 0.1692~0.3048 |

이 대역의 마지막 실행은 v2가 약 13.7% 느렸다. 앞 실행에서는 0.2116 대 0.2141µs로 거의 같았다. **오프라인 수치로 속도 이득을 확정할 수 없다.** 작은 Eval 비용, 매 프레임 상태 비교, 서로 다른 런타임·최적화가 결과에 영향을 줄 수 있다는 것은 추정이다. 관리 코드 대역은 1.0µs보다 낮지만 Unity/Mono·Transform 포함 예산의 통과 증거가 아니다.

게임을 실행하지 말라는 지시 때문에 **엔진 설정 포함·제외의 Unity/Mono 실측값은 둘 다 미측정**이다. 대신 PlayerAuto/Dev에서 실행할 `groupstep bench`를 추가했다. 명령을 구현하기 위해 게임을 실행하지 않았다.

이 명령은 GetUninitializedObject로 만든 scrVisualDecoration과 실제 더미 Transform 2,150개를 사용한다. 각 장식에 Rec 다섯 개를 만들고 GroupCount + StepGroup을 5프레임 워밍업 후 200프레임 잰다. 위치는 pivotPosVec 저장만 하는 모드와 localPosition을 한 번 쓰는 모드를 각각 출력한다. 회전·크기는 필드에 저장한다. `fields`, `+Transform`의 µs/묶음, 엔진 쓰기 포함 ms/frame, `budget<=1us`를 반환한다.

이 벤치의 위치 setter는 **pivotPosVec + Transform.localPosition 대역**이다. 실제 게임 SetPosition/UpdatePosition 호출 사슬, NativeMotion 준비, Compact, 진단·검증은 포함하지 않는다. 더미가 실제 MoveApply의 Late 목록에 들어가지 않도록 DeferRotScale을 잠시 끄므로 실제 보이는 장식의 defer 적격성 검사·목록 관리 비용도 제외된다. 따라서 1µs 이하가 나와도 실제 전체 경로의 통과로 해석하지 않는다.

실행 중인 recs와 setter·진단 플래그·PairXY/호환성 캐시·DeferRotScale·진행 카운터는 finally에서 복원하고 더미 root는 Destroy한다. 벤치는 명시적 명령으로만 실행하며 정상 업데이트에 타이머를 넣지 않는다. 실행 시 더미 생성·JIT·후속 Destroy 비용이 있으므로 성능 A/B와 다른 프로세스에서 실행한다.

## 예상 이득 — 추정

단독 5개 묶음 예산식은 `절약 ms = 묶음 수 × (2.05 − v2 실측 µs/묶음) / 1000`이다. 예를 들어 실측이 1.0µs라면 2,150묶음 기준 2.2575ms다. 이것은 10,750기록짜리 합성 부하의 계산이며 현재 약 4,500기록·1.87ms인 실제 프레임에 그대로 적용할 수 없다.

인계 문서의 v1 측정 2.29ms에서 위치 쌍 손실 약 0.4ms를 복구한다고 추정하면 약 1.89ms로, 기존 끔 1.87ms와 비슷하다. 따라서 페어 복구만으로 추가 이득을 주장하지 않는다. 관리 호출 단순화의 실제 이득·판정 비용을 포함한 최종 식은 `1.87 − v2의 같은 창 DecoAnim ms/프레임`이며 새 프로세스 ABBA로 채워야 한다. 기본값 변경은 하지 않았다.

## Claude의 실행 순서 — 이번 작업에서는 미실행

1. 같은 최종 PlayerAuto DLL, TightLate 켬, 해상도 2560×1440 120Hz, 다른 모드·설정을 고정한다. Prof/Dev 억제 여부와 compatibility를 state로 확인한다. 같은 DLL reload로 A/B하지 않는다.
2. 별도의 새 프로세스에서 `groupstep bench`를 실행해 fields/+Transform µs와 예산 결과를 기록한다. 벤치 포함 프로세스를 성능 판으로 재사용하지 않는다. 엔진 포함 1µs 초과이면 실제 setter·defer 검사 등 제외 비용과 함께 원인을 먼저 확인한다.
3. A=모드 폴더 `groupstep-on` 파일 없음, B=있음으로 **A→B→B→A, 판마다 새 프로세스**를 사용한다. 이번 작업에서는 해당 파일을 만들거나 지우지 않았다. 양쪽 진단은 `groupstep off`로 끈다. 이것은 최적화 플래그를 끄는 명령이 아니라 진단·검증만 끄는 명령이다.
4. Arche 257.8~278.8초의 같은 21초 창을 재생한다. 각 판에서 창 시작 `fuse begin`, 끝 `fuse end`로 DecoAnim 갱신 ms/프레임·프레임 수·끊김을 기록하고 A 두 판/B 두 판을 비교한다. Fuse 자체 진단 비용이 양쪽 동일하다는 전제다.
5. 별도 B 새 프로세스에서 `groupstep verify` 후 같은 구간 및 완료 경계를 확인한다. state의 checks>0, mismatch=0, verifyErrors=0과 PairSteps·폴백·묶음 크기를 남긴다. Dev/Prof 경로로 checks=0이면 검증 통과가 아니다.
6. Arche 전곡 A/B 새 프로세스의 곡 끝 `decosig`를 대조한다. 재시도·중간 시작·완료/Kill 경계도 포함한다. HELLO 전곡에서도 verify와 곡 끝 서명을 대조한다. 진단 판의 시간은 성능 평균에서 뺀다.
7. 비트 불일치·검증 예외 또는 성능 악화이면 `groupstep-on` 없는 새 프로세스로 돌아가고 first·폴백·서명을 남긴다. 기본 켬 전환은 검증과 이득이 확인된 뒤 Claude가 판단한다.

## 빌드·작업 경계·인코딩

세 빌드 모두 오류 0개를 확인했다: 기본 Dev, `-p:Edition=Player`, `-p:Edition=Player -p:AutoTestBuild=1`. 기존 참조 버전 충돌·폐기 API·미사용 필드 경고는 남는다. 빌드는 저장소 출력 경로에만 만들었고 설치 훅을 호출하지 않았다. 오프라인 대조는 Unity/Mono·실제 다른 모드 조합 검증을 대신하지 않는다.

설치 DLL의 작업 전후 SHA-256은 `F396BC176D2A385E0B0E5711062A6A4E0AE1012079B1FDB9E8344290B0DF2409`로 동일했다. HEAD도 기준 커밋 그대로다.

이 문서는 apply_patch로 첫 문자 U+FEFF를 포함해 작성했다. BOM `EF BB BF`, strict UTF-8 디코딩, 한글 문장 재읽기를 확인했다. 한글 2,848자를 확인한 첫 검사에서 대체 문자와 연속 물음표는 0개였고, 이 확인 문장을 반영한 뒤에도 같은 인코딩 검사를 다시 통과했다. 변경 소스의 strict UTF-8 검사와 git diff --check도 통과했다.
