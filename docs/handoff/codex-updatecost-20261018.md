# Codex 작업 7: Update 단계(DecoAnim) 장식당 비용 3가지 — 프로파일링 + 안전한 것만 구현 (게임 실행·Mods 설치·커밋 금지)

선행: `codex-jobs` 작업(DecoJobShadow)이 끝난 뒤 시작한다(같은 파일 AutoTest.cs/StutterFix.cs/csproj 를 건드림 — 먼저 `git status`/`git diff` 로 그 변경을 확인하고 덮지 말 것). 결과: `docs/handoff/codex-updatecost-20261018-result.md`(한국어, 추측은 "추정"). 빌드 셋 오류 0(`-p:Edition=Player -p:AutoTestBuild=1`, `-p:Edition=Player`, 기본). 새 측정 코드는 DEV/AUTOTEST 전용(`UpdateCostProbe.cs`), 제품 동작 변경은 **모든 것이 플래그(모드 폴더 `<이름>-off` 파일, DevPresentFlags 와 같은 방식)로 끌 수 있고, 켠 상태와 끈 상태의 결과가 비트 동일함을 보이는 것만** 넣는다.

## 배경 (Claude 실측 + 사용자가 가져온 분석 — 아래 주장은 모두 검증 대상, 확인 안 된 것은 "미확인")

Arche 곡 270초 구간(CPU 메인 병목, 프레임 10.47ms): Update 스크립트 4.64ms, 그중 모드 `DecoAnim` 애니메이션 갱신 2.4~3.5ms. 애니메이션 도는 장식은 프레임당 약 808개(이 구간 평균; 가장 무거운 때 약 2,100개), 애니메이션 기록(Rec)은 장식당 5~6개(위치X·Y·회전·크기X·Y·색). 한 장식당 Update 단계에서 약 3~4µs 쓰이는 것으로 보인다. 모드 코드: `DecoAnim.cs`(Step/StepPair/Apply, `ZeroTween.Eval`, `NativeMotion` 준비), `MoveApply.cs`(`SkipForLate`, `DeferTransform`, `lateSkippedSet`), `InvisibleSkip.cs`(`LazyPrefix`, `After`), `Dormancy.cs`(`WakePost`), `FastMove.cs`(`Run`, 효과 시작 때 장식마다 `Dictionary<TweenType,Tween>` 조회).

## 1. 보이는 장식의 PosX/PosY 에서 SetPosition 호출을 생략할 수 있나 (사용자 가설)

가설: 회전·크기는 보이는 장식(`MoveApply.SkipForLate(d) == true`)이면 필드만 쓰고 LateUpdate(`UpdatePosition`)에 맡기지만, 위치는 PairXY 이후에도 `scrDecoration.SetPosition(pivotPos, pivotOffset)` 을 부른다. `SetPosition` 은 (게임 IL `scrDecoration.SetPosition`): parallax==null 이면 반환, `vector=(pivotOffset+croppedPivotOffset)/trueScaleMultiplier`, `pivotPosVec=pivotPos`, `childTransform.localPosition=vector`, (에디터면 선택 테두리 등), `pivotOffsetVec=pivotOffset`, `UpdateScreenClamp()`, `UpdatePosition()`. 위치 애니메이션은 pivotOffset 을 바꾸지 않으므로 이전 호출과 같은 pivotOffset·croppedPivotOffset·trueScaleMultiplier 라면 `childTransform.localPosition` 쓰기와 `UpdateScreenClamp()` 는 아무 값도 바꾸지 않는다(`MoveApply.SetLocal` 이 같은 값 쓰기를 `PosSkips` 로 건너뛰는 것이 그 증거). 그런데도 장식마다 `LazyPrefix` + 네이티브 호출들(`op_Implicit(parallax)`, `op_Implicit(camy)`, `visualTrans.get_localPosition`, `op_Implicit(screenClamp)`, `GetVisible()` = `spriteRenderer.enabled` + `activeInHierarchy`) + `lateSkippedSet.Add` 가 매 프레임 돈다는 주장이다(**미확인**: IL·현재 모드 패치 체인에서 실제로 무엇이 불리는지 확인할 것).
할 일:
 a. 현재 보이는 장식의 위치 애니메이션 한 스텝(`DecoAnim.StepPair` → `setPos` → 모드 패치(`InvisibleSkip.LazyPrefix`, `MoveApply.*`, `TileCull`, `Dormancy`, 다른 Harmony 패치) → 게임 `SetPosition`)이 호출하는 모든 함수·네이티브 호출을 IL/Harmony 패치 목록(`Harmony.GetPatchInfo`)으로 나열하고 장식당 비용을 표본 Stopwatch(16번에 1번)로 쪼개서 잰다(`updatecost begin/end`, 구간 252~278초 Arche).
 b. 생략 가능한 조건을 엄밀히: `!useCamScale`/`screenClamp` 상태, `pivotOffsetVec` 이 마지막 완전 호출과 같음, `croppedPivotOffset`/`trueScaleMultiplier`/`camScaleMultiplier` 가 마지막 완전 호출 이후 안 바뀜(SetSprite/SetTextureScaleMultiplier/카메라 크기 변화로 바뀔 수 있음 — 어디서 바뀌는지 IL 로 전수), 에디터 아님(`ADOBase.editor == null`), `parallax` 있음, `SkipForLate` 가능(LateUpdate 가 이번 프레임 그 장식에 UpdatePosition 을 부를 것). 이때 `pivotPosVec` 만 쓰고 `lateSkipped` 등록(MoveApply 의 기존 방식)만 하면 최종 값이 같은지.
 c. 안전하다면 플래그 `posfast-off` 로 끌 수 있는 구현 + 그림자 비교(`updatecost verify`: 64번에 한 번 원래 `SetPosition` 경로를 같은 입력으로 실행해 pivotPosVec·pivotOffsetVec·childTransform.localPosition·screenClamp 관련 값·pivotTrans 위치/회전/크기를 비트 비교; 원래 경로 재실행이 부작용 없음을 근거로 적을 것)와 켜기/끄기 곡 끝 장식 상태 대조(`decosig`, 비교 스크립트 기존 /tmp/cmp.py 방식)를 준비. 줄어드는 시간은 Claude 가 게임에서 잰다.

## 2. 같은 이벤트에서 시작된 (PosX, PosY, Rot, ScaleX, ScaleY) 묶음 평가 비용

가설: 광선(`nevRayID`)은 한 이벤트가 한 장식의 Key 1, 2, 5, 7, 8 을 완전히 동일한 (Dur, Ease)로 동시에 돌려서 Rec 5개가 매 프레임 같은 입력으로 `ZeroTween.Eval` 을 5번 부른다(**미확인**: 실제로 같은 (Pos, Dur, Ease) 인 Rec 묶음이 얼마나 되는지 센다). 효과 시작(`FastMove.Run`) 때 `Dictionary<TweenType,Tween>` 조회가 장식당 10~15번(**미확인**). `DecoAnim.PrepareNative`(`NativeMotion`)가 매 프레임 recs 전체를 한 번 더 훑고 배열 복사·6개 필드 대조하는 시간이 `Ease.Linear` 단순 나눗셈보다 더 들지 않는지(**미확인**).
할 일:
 a. 통계: 한 프레임의 활성 Rec 중 같은 장식에서 (r.Pos 비트 동일, r.Dur 동일, r.E 동일)인 Rec 의 묶음 크기 분포, Ease 별 개수(Linear 비율), `ZeroTween.Eval` 한 번의 비용(표본 측정), `PrepareNative` 한 프레임 비용과 `NativeMotion.Enabled` 켠/끈 상태의 프레임당 DecoAnim 총시간 차이를 낼 수 있게 하는 `updatecost` 항목(NativeMotion 을 끄고 켜는 설정/명령이 있는지 `NativeMotion.cs` 확인; 판마다 새 프로세스 A/B 는 Claude 가).
 b. 안전한 최적화만 구현(플래그 `evalmemo-off`): 같은 프레임 안에서 직전에 평가한 (Pos, Dur, E, Over, Period) 가 비트 동일하면 `ZeroTween.Eval` 결과를 재사용(결과는 결정적 함수라 비트 동일). 이것이 항상 정확히 같은 값인지 `ZeroTween.Eval` 코드로 증명(부작용·전역 상태 없음). 그림자 검증 포함.
 c. `FastMove.Run` 의 Dictionary 조회 횟수(장식당)를 실제로 세고, 줄일 수 있으면(같은 장식에서 같은 Dictionary 를 속성마다 다시 조회 → 한 번 조회해 재사용) 같은 결과의 변경만 구현(플래그 `fastdict-off`). 이 비용은 효과 시작 프레임에 몰리므로 `fuse`(FuseProbe)가 잰 "장식 이동 효과 시작 0.20~0.24ms/프레임" 안의 몫이라 큰 이득이 아닐 수 있다 — 숫자로 말할 것.

## 3. 색(Key 9)·불투명도(Key 10) 의 ApplyColor 후처리

가설: 이미 보이고 깨어 있는(`!IsHidden`, `!dormant`) 장식이 색/불투명도 트윈으로 매 프레임 `scrDecoration.ApplyColor`(→ `scrVisualDecoration.ApplyColor` 가 `SpriteRenderer.color` 를 씀)를 부를 때 모드의 후처리 패치 `InvisibleSkip.After`(`hidden.TryGetValue`), `Dormancy.WakePost`(`dormant.Remove`), `UpdateRendererEnable()` 가 매 프레임 얼마나 쓰는지.
할 일: 색/불투명도 한 스텝의 `setColor`/`setOpacity` → 게임 `ApplyColor` → 모드 postfix 체인 각 구간 비용을 표본 Stopwatch 로 재서 `updatecost` 에 포함(장식 보임/숨김·깨어 있음 별로). 같은 색·불투명도 값을 다시 쓰는 비율(이미 `ColorNoop` 같은 장치 있음 — `InstantMove.ColorNoop`)과 `DecoAnim` Key 9/10 에서 값이 안 바뀌는 프레임(변화량 0)의 비율을 센다. 안전한 건너뛰기 가능성(`SpriteRenderer.color` 같은 값 쓰기 생략이 원래와 같음, `rendererColor` 계산이 알파 0 판정 등 상태에 영향) 설계만. 구현은 비트 동일이 자명한 경우에만(플래그 `colorskip-off`).

## 공통

- `updatecost begin|end|verify`: Arche 252~278초 구간 보고서. 항목별 프레임당 ms 와 호출 수(표본 비율 보정), 장식당 µs. 측정 코드 자체 비용(관측 비용)은 따로 표시.
- 결과 문서에 Claude 가 돌릴 명령 순서(플래그 파일 on/off 새 프로세스 A/B 포함)를 적어라. `sf_ab` 는 설정 필드를 쓰므로 필요하면 플래그를 설정 필드로(예: Settings.xml 의 비공개 항목)로 바꾸는 안도 제안.
- 모든 제품 변경은 기본 켬이어도 되지만 비트 비교로 불일치 0 이 확인되기 전에는 "기본 끔"으로 둔다고 결과에 명시.
