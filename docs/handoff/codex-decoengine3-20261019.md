# Codex 작업 L: DecoEngine 핫패스 줄이기 (프로파일 기반) — 소유율은 이미 81%, 이제 장식당 비용 — 게임 실행·Mods 설치·커밋 금지

결과: `docs/handoff/codex-decoengine3-20261019-result.md` (**apply_patch 로 UTF-8 BOM, 쓴 뒤 `Get-Content -Encoding UTF8` 로 한글 확인**), 한국어, 추측은 "추정". 빌드 셋(Dev/Player/PlayerAuto) 오류 0, `tools/DecoEngineCheck` 통과. 기본 끔 유지. 기준 커밋 fb4fe3f (2단계-a + 내가 고친 정렬 제거).

## 실측 (Claude, Arche 곡 252.0~273.0초, 새 프로세스, 검증 끔, 2560x1440 120Hz, `waitsongpos 252` 로 곡 시각을 맞춤)
| 판 | 모드 애니메이션 갱신 | 장식 관리자 LateUpdate | 합 | 창 프레임 수 |
|---|---:|---:|---:|---:|
| A 엔진 끔 | 1.90ms | 1.14ms | 3.04ms | 3,030 |
| B 엔진 켬 (정렬 버그 있던 판) | 3.43 | 1.07 | 4.50 | 2,503 |
| B2 엔진 켬 (복귀 때 전체 `List.Sort` → 이진 삽입으로 수정, fb4fe3f) | **2.25** | 1.19 | **3.44** | 2,861 |
- 정확성: `decoengine verify` 301만 번 검사 불일치 0 (2단계-a 판). 소유율 81%(steps), 창 구간 엔진이 프레임당 ~1,050 장식을 진행.
- 즉 아직 **순손해(+0.4ms)**. 목표는 합 ≤ 1.8ms. 창 구간 누적: 입장 33k·복귀 16k(알파 전환 `alpha-fallback` 16k)·`rej` 7(엔트리 없음) 44k·`missing[4]` 33k.

## 표본 프로파일 B2 (`profsamp`, 21,018 표본, 메인 스레드; 이름 옆은 포함 시간 % / 리프 %)
- `DecoEngine.Update` 21.1% 포함: `Advance` 8.8%(리프 6.1%), **`Stable` 4.9%**(리프 1.8%, 안에서 `Transform.get_localPosition` 네이티브 2.8%), `Admit` 1.9%, `AlphaSafe`/`AlphaRecordSafe`/`AlphaWillSet` ≈ 3.4%, `Same` 1.0%(리프)
- `DecoEngine.Late` 8.5% 포함: `TightLate.EngineApply` 7.7%(`Apply` 8.1%, 리프 3.6%), `TightLate.EngineReason`/`Eligible` 2.4~3.1%(리프 3.1%)
- 레거시 루프 `DecoAnim.UpdatePrefix` 29.9% 중 엔진 21.1 을 빼면 약 8.8%: `DecoAnim.Step` 5.8%, `OnUpdate` 5.1%, `ApplyColor_Patch2`(색) 4.1%, `Action<scrDecoration,Color>` 4.2%
- 해시/사전: `HashSet.Remove` 1.5, `HashSet.Contains` 0.9, `Dictionary.FindEntry` 1.2, `Dictionary<SpriteRenderer,int>.FindEntry` 1.1, `Dictionary.TryGetValue` 1.7(= 입장·복귀·`Dormancy.WakePost` 등)
- 쓰기 장벽/stelemref 합 약 3~5%, `IsNativeObjectAlive` 0.6%
- 엔진 밖 참고: `TightLate.Process` 10.7% 포함(비소유 장식 + 엔진 Late), `Dormancy.Prepare` 1.8%.

## 할 일 (우선순위)
1. **`Stable(State)` 매 프레임 네이티브 호출 제거**: `ReferenceEquals(Child)`, 관리 필드 비교는 싸지만 `childTransform.localPosition` 네이티브 읽기를 입장 때만(그리고 관리 필드 `pivotOffsetVec`/`croppedPivotOffset`/`trueScaleMultiplier`/`placementType` 가 바뀐 프레임에만) 하게 바꿔라. 이 localPosition 이 입장 후 외부에서 바뀔 수 있는 경로(`SetSprite`, `SetPivotOffset`, `SetScale`, 다른 모드, 에디터)를 IL 로 전수하고, 바뀔 수 있는 경우 어떤 관리 필드가 반드시 같이 바뀌는지(= 관리 필드만 감시하면 충분한지) 증명하거나, 불가능하면 N 프레임 주기 검사로 비용을 나누되 그 사이 위험을 문서화.
2. **`Advance` 비용**: 장식당 키 5개 슬롯 순회·`ZeroTween.Eval` 중복 호출(같은 Pos/Dur/Ease 공유), float 연산 외 불필요한 분기·배열 경계 검사를 줄여라. 같은 (Pos,Dur,Ease,Over,Period) 비트가 같은 키들은 Eval 결과 공유(이미 하고 있다면 적중률 계측).
3. **Late 의 `EngineReason`/`Eligible` 재검사**: 엔진 소유 장식은 입장 때 eligibility 를 이미 통과했다. 매 프레임 재검사는 보임 판정(`rendererEnabled && a != 0`)과 부모 정체 같은 *바뀔 수 있는 것*만 남기고 나머지(타입·히트박스·마스크·메시·follow/stick/lock·parallax)는 입장 시점 값을 신뢰하되 그 값이 바뀔 수 있는 경로를 IL 로 확인(바뀌면 복귀 트리거).
4. **입장·복귀 churn**: `alpha-fallback`(알파 0 전환 복귀) 16k/창, 입장 33k/창이 해시(Dictionary/HashSet)와 쓰기 장벽을 쓴다. 알파 0 전환 프레임만 임시로 기존 경로를 쓰고 곧 재입장하는 현재 구조를, 장식을 `active` 에 둔 채 그 프레임만 "기존 경로 한 프레임" 플래그로 처리하는 방식으로 바꿔 입장/복귀 비용(해시 3~4번)을 없앨 수 있는지 설계하고 구현. 불가하면 이유를 적어라.
5. **`rej` 7(엔트리 없음) 44k·`missing[4]` 33k** 원인 분해(`TightLate.EngineEntry` 가 null 인 이유별 카운터) 후 줄이기.
6. 레거시 루프 쪽 남은 비용은 색(키 9/10) 처리: 소유 장식의 색 기록은 지금 `DecoAnim.Step` → `OnUpdate` → `Action<scrDecoration,Color>` → `SetColor` → `ApplyColor_Patch2`. 이 사슬을 엔진 쪽에서 직접 호출해 `Step`/`OnUpdate` 오버헤드를 줄일 수 있는지(색 값 계산은 기존 `Apply` 와 비트 동일, `ApplyColor` 호출 인자 동일) 검토. 필수 아님.

## 규칙
- 모든 변경은 `decoengine verify` 불일치 0 이 유지되는 설계여야 한다(기존 `tools/DecoEngineCheck` 확장). 불확실하면 보수적으로 복귀 경로로.
- 각 변경의 예상 이득을 µs/장식·프레임으로 추정하고, **채택은 Claude 가 실게임 ABBA 로 판단**한다. 결과 문서에 항목별로 어떤 프로파일 줄이 얼마나 줄 것으로 예상하는지 적어라(표본 퍼센트 → ms 환산: 이 구간 메인 스레드 프레임 약 7ms).
- 결과 문서 끝에 Claude 가 돌릴 순서(`waitsongpos 252`, `fuse begin/end`, `decoengine state`, `profsamp begin 1000`/`end`, 새 프로세스 ABBA)를 적어라.
