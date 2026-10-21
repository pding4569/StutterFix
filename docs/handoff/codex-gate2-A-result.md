# Gate 2 A 최신 수정 — ToggleVisible 오분류 (2026-10-21, 로그 run3)

**표준 프리팹의 ToggleVisible을 외부 자식 스크립트로 거부하는 원인을 수정했다. 오프라인 검증은 통과했고 수정 후 소유 수·픽셀·FPS는 미측정이다. Gate 2 PASS를 선언하지 않는다.** codex-al-ga-2.md의 장부와 최신 증거부터 읽고 기존 계층/프로파일 수정을 보존했다. 시작 시 작업 트리는 깨끗했고 HEAD는 863096b였다. 게임 실행·설치·커밋·푸시·config 수정은 하지 않았다.

## 최신 전달 시험

[자동 수집 증거](ga-autoloop-run1-evidence.txt)는 이전 계층/프로파일 수정 이후의 결과다. 이번 세션에서 게임을 측정한 값이 아니다.

| 항목 | 전달 실측 |
|---|---|
| 소유 / 구간 | 0 / 0 |
| 제외 | foreign-child-script 2,088개, repeat-tile 38개, keywords 1개 |
| 프로파일 | enabled=True, samples=957,795, valid=True |
| 설정 비율 | set_pct=68.852079% |
| 실패 | livedeco on: no eligible images, GATE2=ERROR / autotest_aborted |
| 픽셀 / FPS | 단계 미도달 |

프로파일은 이전 수정으로 표본이 생겼다. **68.852079%는 Step에서 표본 수집한 Apply/OnUpdate의 설정 비중**이다. Prof의 PairXY/GroupStep 억제, StepPair·OnComplete·setter 이후 엔진 작업 제외 한계가 있어 평상시 전체 애니메이션 비용의 비율로 확대하지 않는다. 실패 입장 프레임 scan_ms=478.261100도 정상 재생 프레임 비용이나 FPS 근거가 아니다.

## 원인과 확인 근거

기존 Admit은 자기 장식·parallax·blend·mask 참조 이외의 활성 MonoBehaviour를 모두 foreign-child-script로 거부했다. 게임 표준 이미지 프리팹의 sprite 노드에는 활성 ToggleVisible이 들어 있으므로 이 프리팹은 그 조건에 걸린다.

외부 패키지 설치 없이 표준 라이브러리만 사용하는 [에셋 감사 도구](../../tools/optimization/livedeco-prefab-audit.py)를 추가했다. 현재 설치 sharedassets4.assets의 프리팹 490 → ToggleVisible 471 → **자기 scrVisualDecoration 490** 연결, ignoreIfDisabled=False를 직접 확인했다([프리팹/해시](gate2-A-run3-prefab.log)). 프리팹은 1개 확인했으며 이것만으로 전달된 2,088개 인스턴스가 모두 같은 연결이었다고 단정하지 않는다. 도구는 Unity v22 직렬화 형식만 지원하고 다른 버전은 거부한다.

디컴파일 ToggleVisible.cs와 [설치 DLL IL](gate2-A-run3-visibility-il.log)을 대조했다. OnBecameVisible/Invisible은 scriptToToggle.enabled만 설정하며 invisible 분기에서 자신의 gameObject.activeSelf를 확인한다. Transform·SpriteRenderer.color·장식 논리 값은 읽거나 쓰지 않는다. [관리자 IL](gate2-A-run3-manager-il.log)의 LateUpdate는 enabled 검사 없이 null이 아닌 항목의 LogicUpdate를 직접 부른다. scrVisualDecoration → scrDecoration → ADOBase → RDTools.RDBaseDll의 메서드 목록에 Update/LateUpdate/OnEnable/OnDisable이 없음을 확인했다([visual](gate2-A-run3-visual-types.log), [decoration](gate2-A-run3-decoration-types.log), [ADOBase](gate2-A-run3-adobase-types.log), [RDBaseDll](gate2-A-run3-base-il.log)). 이 연결에서 enabled 변화로 장식 갱신이 중단되거나 Transform 소비 콜백이 추가 실행되는 관리 경로는 확인되지 않았다.

게임 DLL SHA256은 F692210F9A342F7EED4BE40AAD463286AB40BF046599959823942EA02F4CA3FF로 이전과 같다([해시](gate2-A-run3-game-hash.txt)). 감사는 파일만 읽고 게임 메서드는 실행하지 않았다. 워크트리에 없는 로드맵은 정식 체크아웃 docs/ROADMAP-EXTREME-20261021.md에서, freeze 결과는 wt-freeze/docs/handoff/codex-freeze-20261021-result.md에서 읽었다. freeze 문서는 오프라인 구현 결과여서 새 실게임 절감 수치로 쓰지 않았다.

## 수정

- 새 LiveDecoScripts.Reason은 **정확한 게임 ToggleVisible 타입 + 자기 SpriteRenderer와 같은 Transform + 자기 장식 대상 참조**일 때만 추가 허용한다. 동명 외부 타입·상속 타입·잘못된/누락된 대상·다른 노드는 거부한다. 콜백이나 enabled를 강제로 변경하지 않는다.
- 입장 당시 캐시한 스크립트의 활성 상태와 연결을 소유 중 재검사한다. 캐시된 비활성 외부 스크립트가 켜지거나 ToggleVisible 대상이 바뀌면 기존 Release로 복원한다. **입장 후 임의 외부 AddComponent 전부를 실시간 탐지하는 기능은 아니다.** 기존 Setup/경계 사건 재입장 검사를 유지한다.
- 제외 이유는 foreign-child-script:<전체 타입명>, visibility-node, visibility-target:<타입명 또는 missing>으로 세분화했다. 시작에 renderer_version=3 visibility_policy=bound-self, 정점 검사에 실제 허용된 콜백 수 visibility_admitted를 남긴다. 정점 오차 1e-4 및 마스크·히트박스·반복·특수 블렌드·정렬·텍스처 조건을 유지했다.
- 외부 Harmony 패치 거부 검사에 ToggleVisible을 포함했다. 새 콜백 패치는 설치하지 않는다. 논리 필드·엔진 쓰기 차단·off/종료 복원은 기존 경로를 유지한다. 일반 Player 제외·기본 꺼짐을 유지한다.
- 기존 129단계 생성기는 유지했다. 변경된 코드의 프로파일→3시점 픽셀/복원→ABBA 검증을 준비했고 같은 게임 시험은 재실행하지 않았다. 아틀라스·게이트 3·워커 분할은 추가하지 않았다.

## 오프라인 검증

| 검사 | 결과 | 로그 |
|---|---|---|
| Release DEV / Player / PlayerAuto | 오류 0/0/0, 경고 38/36/38 | gate2-A-run3-build-dev.log / -player.log / -auto.log |
| LoadCheck DEV / Player / PlayerAuto | 276/229/276곳, 틀림 0/0/0 | gate2-A-run3-loadcheck-dev.log / -player.log / -auto.log |
| 생산 계층 / 새 생산 스크립트 판정 | 2,729 / 15 checks 통과 | [geometry](gate2-A-run3-geometry-fixture.log) |
| 구간/첫 멤버 앵커 / Gate 2 판정 | 106,460 / 18 checks 통과 | [fixture](gate2-A-run3-fixture.log) |
| edition / 게임 패치 대상 | 31 / 17 checks 통과 | 같은 로그 |
| MatShare / FrameCompare | 25,401 / 11+15+14+17+13 checks 통과 | gate2-A-run3-matshare-fixture.log / -framecmp-fixture.log |
| Gate 1 / Gate 2 생성기 | 25 / 129단계, UTF-8 CLI 통과 | gate2-A-run3-gate1-steps-fixture.log / -steps-fixture.log |
| 현재 설치 프리팹 | 프리팹 1개, 자기 장식 대상 ToggleVisible 1개 | gate2-A-run3-prefab.log |
| git diff --check | 통과 | 최종 로컬 검사 |

새 fixture는 실제 생산 정책 소스를 연결해 정상 바인딩·자기 장식 비활성 상태·잘못된/누락된 대상·다른 노드·동명 외부 타입·상속 타입·입장 후 대상 변경·캐시된 외부 스크립트 활성화를 검사한다. 관리 어댑터 검사이며 Unity 콜백·렌더링·Mono Harmony 실행·성능 증명이 아니다. LoadCheck 동적 이름 5곳은 런타임 확인 대상이다.

## 자동 시험 인계와 한계

renderer_version=3, visibility_admitted, nested_admitted, vertexcheck max_err/count/excluded, 제외 타입 분포, owned/runs 및 비용을 확인한다. **수정 후 이 값들과 실픽셀·FPS·GC·재시작/사망/맵 재열기/언로드 복원은 미측정**이다. 약 1,200구간은 예상치다. 세 시점 ab_out=0, 복원·기준 상태 안정성, ABBA 켬 평균 FPS ≥ +15%를 모두 만족해야 PASS다. 현재 증거에는 수정 가능한 코드 원인이 있으므로 설계 한계로 확정하지 않았고 성공 조건도 완화하지 않았다.

task-observer v3.5.0 관찰 반환(사용자 범위 스킬/메모리 쓰기 없음): 문제 — 스크립트를 한 제외 이유로 집계하면 표준 프리팹의 보조 컴포넌트와 외부 Transform 소비자를 구별할 수 없다. 개선 — 현재 직렬화 연결과 콜백 IL을 대조한 뒤 정확한 타입·역할·대상 참조로 판정하고 거부 타입을 로그에 남긴다. 원칙 — 증명한 바인딩 범위만 허용하고 실게임 미측정을 구분한다.

---

아래는 보존한 이전 수정 결과다.


# Gate 2 A 자동 반복 2회차 결과 — 2026-10-21

**소유 0개로 중단한 계층 판정과 프로파일 표본 0개의 원인을 수정하고 오프라인 검증을 완료했다. 수정 후 게임 측정은 없으며 Gate 2 PASS를 선언하지 않는다.** 중단된 2회차의 미커밋 수정본을 이어서 검토·보완했다. 현재 HEAD는 `7758ce9`이며 커밋하지 않았다. 게임 실행·설치·푸시와 `.codex/config.toml` 수정도 하지 않았다. 아래 1회차 결과는 이력으로 보존한다.

## 전달된 실패와 원인

장부 [ga-autoloop-ledger.md](ga-autoloop-ledger.md)와 [run1 증거](ga-autoloop-run1-evidence.txt)를 먼저 읽었다. 직전 게임 측정은 다음 단계에서 끝났으며 픽셀/FPS 판정까지 도달하지 않았다.

| 전달 항목 | run1 실측 |
|---|---|
| 소유 / 구간 | 0 / 0 |
| 제외 이유 | child-parent 2,089개, repeat-tile 38개 |
| 설정 비용 프로파일 | enabled=False, samples=0, valid=False |
| 자동 시험 실패 | livedeco on: no eligible images |
| 마지막 판정 | GATE2=ERROR, autotest_aborted |

기존 `GeometryReason`의 `child-parent`는 피벗 부모, child의 직계 부모, renderer와 child의 동일성 조건을 하나로 합쳤다. SpriteRenderer가 논리 오프셋의 대상인 `childTransform`과 같아야 한다는 가정 때문에, 그 사이에 고정 변환이 있는 계층을 처리할 수 없었다. **기존 집계만으로 세 하위 조건별 실패 개수나 실제 계층 깊이를 역산할 수는 없다.** 새 구현은 피벗 부모 제약을 유지하면서 나머지를 실제 계층 합성으로 대체하고 실패 이유를 분리한다. 따라서 수정 후 2,089개가 전부 소유된다고 예측하지 않는다.

디컴파일 `scrDecoration.SetPosition`은 `(pivotOffset + croppedPivotOffset) / trueScaleMultiplier`를 **childTransform.localPosition**에 쓰고, `UpdatePosition`은 pivotTrans 및 parallax·회전·스케일을 갱신한다. `scrDecorationManager.CreateDecoration`은 이미지 프리팹을 관리자 아래 생성하고 scrVisualDecoration을 얻는다. 이 관리 코드는 프리팹 내부 renderer와 child가 같은 객체라고 보장하지 않는다. 설치 게임 IL과 교차확인했다: [장식 IL](gate2-A-run2-decoration-il.log), [시차 IL](gate2-A-run2-parallax-il.log), [관리자 IL](gate2-A-run2-manager-il.log). [DLL 해시](gate2-A-run2-game-hash.txt)는 기존 `F692210F9A342F7EED4BE40AAD463286AB40BF046599959823942EA02F4CA3FF`와 같다. IL 도구는 메타데이터만 읽었고 게임 메서드는 실행하지 않았다.

프로파일은 `DecoAnim.Prof`가 PlayerAuto 기본 상태에서 false인 채 읽히는 것이 직접 원인이었다. 기존 `profilebegin`은 카운터만 저장하고 활성화하지 않아 표본이 생기지 않았다.

## 이어서 완성한 수정

- 새 `LiveDecoGeometry.cs`는 renderer→child 아래 구간, child의 회전·스케일, child→pivot 위 구간을 고정 로컬 변환으로 합성한다. 매 프레임 논리 child XY 오프셋만 올바른 위치에 넣는다. flip은 renderer 로컬 정점에 먼저 적용하며 월드 행렬 역변환은 사용하지 않아 크기 0도 처리한다.
- 평면이며 pivot까지 연결되는 최대 16개 로컬 구간만 지원한다. 끊긴 계층·순환·과도한 깊이·비평면·고정 구간의 런타임 변경은 거부하거나 원본으로 돌린다. 피벗 부모 항등성, 마스크·히트박스·반복·특수 블렌드·외부 스크립트 제외는 유지한다. 소유 진입 전 원본 정점/바운드와 **1e-4** 검사를 그대로 수행한다. `renderer_version=2`, `nested_admitted`로 새 경로 사용 여부를 확인할 수 있다.
- 소유 중 정점은 계속 논리 필드에서 계산하고, 기존 TightLate·setter·ColorFast의 Transform/색 쓰기 차단과 off 복원을 유지한다. 아틀라스·새 셰이더·게이트 3·워커 분할은 추가하지 않았다. 구간별 업로드 비용과 호출 수를 분리하는 기존 `cost`도 유지했다.
- `profilebegin`은 DEV/AutoTest에서만 Prof를 켜고 원래 값을 보관한다. profileend/reset/abort/result/SceneReset/Main.Unload에서 복원한다. samples=0이면 `profile_no_samples`로 최종 ERROR를 남겨 무효한 프로파일을 숨기지 않는다. 일반 Player에서는 Prof의 readonly 속성까지 그대로 유지한다.
- **프로파일 해석 제한:** 기존 Prof 경로는 PairXY/GroupStep을 억제하고 Step 표본을 사용한다. `pairxy_suppressed=1 groupstep_suppressed=1 baseline_path_unchanged=0`을 명시해 이 비율을 최적화된 평상시 경로 전체의 설정 비율로 오해하지 않게 했다. StepPair·OnComplete·setter 이후 엔진 작업도 측정 범위 밖이다. 실제 set_pct는 아직 없으므로 “설정 60% 이상”은 확인되지 않은 가설이다. 프로파일 재생은 픽셀/FPS 창과 분리하며 원래 Prof 값을 복원한다.

## 이번 오프라인 검증

최종 소스에 대해 아래를 실행했다. 빌드 경고는 기존 어셈블리 충돌·deprecated·미사용 필드 등이며 오류는 없다.

| 검사 | 결과 | 로그 |
|---|---|---|
| Release DEV / Player / PlayerAuto | 오류 0/0/0, 경고 38/4/38 | gate2-A-run2-build-dev.log / -player.log / -auto.log |
| LoadCheck DEV / Player / PlayerAuto | 276/229/276곳, 틀림 0/0/0 | gate2-A-run2-loadcheck-dev.log / -player.log / -auto.log |
| 실제 LiveDecoGeometry 소스 연결 fixture | 2,729 checks 통과 | [geometry](gate2-A-run2-geometry-fixture.log) |
| 구간·첫 멤버 앵커 모델 / Gate 2 판정 | 106,460 / 18 checks 통과 | [fixture](gate2-A-run2-fixture.log) |
| edition 메타데이터 | 28 checks 통과: 일반 Player 실험 타입/참조 없음, Prof는 DEV/AutoTest에서만 변경 가능 | 같은 fixture 로그 |
| 설치 게임 Harmony 대상 유일성 | 17개 통과 | 같은 fixture 로그 |
| MatShare / FrameCompare 기존 fixture | 25,401 / 11+15+14+17 checks 통과 | gate2-A-run2-matshare-fixture.log / -framecmp-fixture.log |
| 기존 Gate 1 / Gate 2 생성기 | 25 / 129단계, UTF-8 CLI 통과 | gate2-A-run2-gate1-steps-fixture.log / -steps-fixture.log |
| git diff --check | 통과 | 최종 로컬 검사 |

계층 fixture는 0~여러 개 중간 구간, child 위/아래 변환, 음수·0 스케일, flip, 논리 오프셋 변화, 고정 구간의 위치·회전·크기 변경, 재부모화·파괴·순환, 깊이 16/17 경계를 검사한다. 생산 알고리즘을 연결했지만 수학 어댑터는 System.Numerics이며 실제 Unity 부동소수점·SpriteRenderer·픽셀 검증은 아니다. LoadCheck 동적 이름 5곳과 Mono Harmony 실행도 런타임 확인 대상이다.

## 자동 루프 인계와 현재 한계

생성기 `tools/optimization/livedeco2-steps.py`의 129단계는 유지했다. 수정된 코드로 프로파일 → 262/270/276초 픽셀·복원 → off/on/on/off 262→276초 FPS를 수집하도록 되어 있다. 이번에 같은 게임 시험을 다시 실행하지 않았다. 설계 한계로 확정할 새 수치는 없고, 직전 오류에는 코드상 수정 가능한 원인이 있었다.

다음 자동 시험에서는 `PROFILE enabled=True`, samples>0, `renderer_version=2`, vertexcheck 및 `nested_admitted`, 제외 이유별 개수, owned/runs와 cost를 확인해야 한다. 약 1,200구간은 예상이며 실측으로 쓰지 않는다. 세 시점 `ab_out=0`과 복원 성공, ABBA 평균 FPS +15%를 모두 만족해야 PASS다. **수정 후 소유 수·정점 오차·픽셀·FPS·GC·실제 수명 복원은 모두 미측정**이다. 이 조건들을 완화하거나 성공 로그를 미리 만들지 않았다.

task-observer 관찰은 결과 문서로 반환하며 사용자 범위 스킬·메모리는 수정하지 않았다. 문제: 여러 계층 제약을 하나의 제외 이유로 합치면 집계만으로 실제 실패 조건을 구별할 수 없다. 개선: 실패 조건을 분리하고 새 허용 경로의 진입 수를 함께 남긴다. 원칙: 전달된 실패 수치와 코드상 추론, 수정 후 미측정을 구분한다.

---

아래는 보존한 1회차 결과다.

# Gate 2 A 결과 — 2026-10-21

변형 A의 개발 전용·기본 꺼짐 프로토타입과 자동 검증 생성기를 구현했다. **실게임 Gate 2는 미측정이며 PASS를 선언하지 않는다.** 게임 실행·설치·커밋·푸시는 하지 않았다. 기준 HEAD는 `5bf7413`이다. 작업 중 공통 인계에 추가된 논리 필드 전용 갱신 지시도 반영했다. 게이트 3나 워커 분할은 만들지 않았다.

## 구현

- `livedeco on|off|state`는 `LiveDecoRenderer.cs`를 호출한다. 기존 `census`와 Gate 1 정책은 유지했다. 다른 소유 렌더러·장식 엔진과의 동시 실행, SRP, 원근/비-Z 정렬, 비항등 관리자 부모는 거부한다. 시작 로그에 `currentRenderPipeline=null`을 남긴다.
- 모든 live 렌더러/재질 슬롯을 장벽으로 포함한다. 정렬된 행에서 소유 장식이 연속이고 정렬 버킷·Unity layer·재질 서명·실제 텍스처가 같을 때만 같은 구간이다. 떨어진 같은 텍스처는 합치지 않는다. **재질 동률은 소유끼리라도 검증되지 않은 순서를 만들지 않고 제외한다.** 첫 멤버의 실제 재질 인스턴스, sortingLayerID, sortingOrder, Z를 빌린 씬 MeshRenderer를 쓴다. 재질·텍스처 복제와 UV 재배치는 없다.
- 디컴파일 `scrDecoration.UpdatePosition/SetPosition/SetRotation/SetScale/camScaleMultiplier`, `scrParallax.SetTrans`와 설치 DLL의 IL을 대조했다. 논리 위치·피벗/크롭 오프셋·PPU가 반영된 실제 sprite 정점·child 회전/스케일·시차·카메라 회전/배율 잠금·화면 고정·축 고정·flip을 계산한다. 바닥 부착·행성 따라가기·비평면 자식·반복/타일·비쿼드·특수 블렌드·마스크·히트박스·활성 종속 렌더러/외부 자식 스크립트는 제외한다.
- 소유 진입 전 원본을 갱신한 뒤 계산 정점 4개와 `TransformPoint` 정점 및 SpriteRenderer.bounds의 min/max를 비교한다. `[livedeco] vertexcheck max_err=... count=... excluded=... threshold=0.0001`을 남기고, 비유한 값이나 1e-4 초과는 제외한다. 이 검사는 진입 시 검사이며 모든 미래 프레임의 동일성 증명이 아니다.
- `TightLate.Process`의 직통 Transform 경로와 Harmony `UpdatePosition`을 모두 막는다. `SetPosition`은 논리 위치/피벗과 screen clamp 필드만, 회전/크기는 논리 필드만 갱신한다. 색/불투명도 setter·ApplyColor·`ColorFast.TrySet`도 소유 중 엔진 색 쓰기를 생략하고 `rendererColor`까지 갱신한다. `DecoAnim.Apply/OnUpdate/OnComplete`의 다음 효과 시작값을 유지한다. 알파가 0 이하로 넘어가면 원본에 돌려 숨김/깨우기 bookkeeping을 처리한다.
- 색은 `color.WithAlpha(color.a * opacity * 1f)`의 기존 순서로 보존하고 Color32 정점에 넣는다. 원래 `Sprites/Tile` 셰이더를 그대로 빌려 텍스처 샘플·색 곱·프리멀티플라이를 셰이더에서 한 번 수행한다. 반복은 1인 경우만 소유한다. `Sprites/Default` 등 센서스 기본형 밖 셰이더는 제외한다. Color32 양자화, Quaternion 정규화 차이, SpriteRenderer와 메시의 내부 처리 차이는 실픽셀 검증 대상이다.
- 위치/Color32/UV를 인터리브한 공용 상주 구조체 배열 하나에 모든 구간을 채운 뒤 구간별 source offset으로 `SetVertexBufferData` 및 지정된 3개 플래그를 쓴다. 인덱스는 원본 sprite 인덱스와 구간 순서를 유지한다. 큰 고정 bounds를 쓰고 범위 밖 정점은 오류로 중단한다. 같은 Z, 활성 상태, forceRenderingOff를 매 프레임 다시 쓰지 않는다.
- 행의 진입/이탈/키 변경은 이진 삽입/제거, 구간은 기존 첫 멤버·멤버 버전 대조로 **바뀐 메시만 교체**한다. `transition`에 생성/재사용/폐기 수와 시간을 기록한다. 순서 장벽을 추적하는 캐시된 전체 렌더러 스캔과 구간 설명의 선형 순회는 남는다. 장식 Setup/경계 변경 시 레지스트리를 보충한다. 임의 외부 코드가 별도로 생성하는 비장식 렌더러를 전부 실시간 발견한다는 보장은 없다.
- `cost`는 최대 120프레임 묶음의 **프레임당 평균** 정점 계산/업로드/재구성/스캔 ms, upload_calls를 분리한다. 약 1,200구간이라는 예상치를 실측으로 쓰지 않는다. 메인 스레드 allocated_bytes, steady_allocated_bytes/steady_samples, 힙 증감을 남긴다. 비용·정점 로그 자체의 할당은 카운터 밖이고 전환 할당은 따로 포함된다. **실게임 GC=0은 아직 확인하지 못했다.**
- off, AutoTest 종료/오류, SceneReset, Main.Unload, 루트 파괴에서 원본 forceRenderingOff·현재 논리 상태의 child Transform/위치/색을 복원하고 패치·메시·오브젝트를 정리한다. 빌린 재질/텍스처는 파괴하지 않는다. 복원 오류는 최종 ERROR에 반영한다. 실제 재시작/사망/맵 재열기/언로드의 누수 검사는 미실행이다.

## Transform 소비자 감사

게임 DLL SHA256은 `F692210F9A342F7EED4BE40AAD463286AB40BF046599959823942EA02F4CA3FF`이다. [해시](gate2-A-game-hash.txt), [장식 IL](gate2-A-decoration-il.log), [시차 IL](gate2-A-parallax-il.log), [읽기 IL](gate2-A-readers-il.log), [쓰기 IL](gate2-A-guards-il.log)을 남겼다. IL 도구는 메타데이터만 읽었고 게임 메서드를 실행하지 않았다.

직접 Transform 필드/크기/시차 위치 관련 읽기 참조 16행을 수집했다. `GetDecorationWorldSize` 호출자는 gizmo LateUpdate/Drag 2곳, `GetPositionWithParallax` 호출자는 gizmo LateUpdate/DragStart/Drag 3곳이다. gizmo LateUpdate는 `isEditingLevel`에서만 실행한다. 활성 선택/히트박스 테두리, 히트박스, 마스크, 바닥/부모 의존 장식은 소유에서 제외한다. `SpriteAlphaMask.maskSize`와 `SpriteAlphaMaskUtils.ApplyAlphaMaskToRT`/closure는 마스크 Transform을 읽으므로 마스크 및 마스킹된 이미지를 제외한다. 관리자 테두리/히트박스 크기 계산도 디컴파일에서 확인했다.

`ffxMoveDecorationsPlus`의 시작값은 pivotPosVec, pivotOffsetVec, rotAngle, scaleVec, color, opacity 등 논리 상태다. 해당 필드는 계속 갱신한다. 텍스처 전체 크기를 읽는 함수는 텍스처를 바꾸지 않아 유지된다. 이 감사는 설치 게임 관리 코드의 직접 참조를 조사한 것이며 네이티브 내부·반사·임의 외부 모드의 alias 소비자 전체를 증명하지 않는다.

## ProfApply / ProfSet와 자동 검증

**설정 비중은 미측정이다.** 현재 저장 근거에서 새 구간의 ProfApply/ProfSet 수치가 없어 Gemini의 “60% 이상”을 결과로 쓰지 않았다. 생성기의 첫 별도 재생에서 262→276초 `livedeco2 profilebegin/profileend`로 누적 카운터 차이를 구한다. `[livedeco2] PROFILE key=... apply_us=... set_us=...` 및 표본 가중 `set_pct`를 출력한다. 이 카운터는 Step의 Apply/OnUpdate 표본이며 StepPair·OnComplete·setter 이후 엔진 처리 전체를 포함하지 않는다. Prof가 꺼져 있으면 samples=0/valid=False로 표시한다. 프로파일 재생은 픽셀/FPS 측정 창 밖이다.

```powershell
py -3 tools/optimization/livedeco2-steps.py --chart '<Arche FULL.adofai 절대 경로>' --output x.json
```

이 명령은 JSON만 만든다. 생성기 [livedeco2-steps.py](../../tools/optimization/livedeco2-steps.py)는 총 129단계다. 262/270/276초 각각 독립 seek → pause → 잡음 바닥 → A → on → B → off → A2를 캡처한다. 중앙 가로/세로 각 20% 제외 영역 밖 픽셀 수·최대 채널 차이·상위 영역 좌표·복원·상태 안정성을 `PIXEL`에 남긴다. 1/255 차이도 PIXEL_NEAR로만 보고한다.

그 뒤 같은 게임에서 네 번 독립 seek, 252초 모드 설정, 262초 측정 시작, 276초 측정 종료로 **off/on/on/off** 순서를 지킨다. pause 없이 fuse begin/end와 FPS·ms·실제 시작/끝 곡 시각·프레임 수를 기록한다. 두 on FPS 평균이 두 off 평균보다 15% 이상이고 세 픽셀 표본이 정확히 0일 때만 PASS다. 누락/중복/순서 오류·불안정한 기준/복원 실패·창 중 렌더러 중단은 ERROR다. 정상 결과 및 AutoTest 오류 종료 모두 끔으로 끝난다.

| 실게임 항목 | 결과 |
|---|---|
| vertexcheck 최대 오차·소유 수·구간 수 | 미측정 |
| 262/270/276초 ab_out / aa2_out / maxdiff | 미측정 |
| ABBA 네 창 FPS / 향상률 | 미측정 |
| 정점/업로드/구간 재구성/스캔 ms 및 GC | 미측정 |
| ProfApply/ProfSet 설정 비중 | 미측정 |
| Gate 2 | 미측정, PASS 아님 |

## 오프라인 검증

| 검사 | 결과 | 근거 |
|---|---|---|
| Release DEV / Player / PlayerAuto 빌드 | 오류 0 / 0 / 0 | gate2-A-build-dev.log / gate2-A-build-player.log / gate2-A-build-auto.log |
| LoadCheck DEV / Player / PlayerAuto | 276 / 229 / 276곳, 틀림 0 / 0 / 0 | gate2-A-loadcheck-*.log |
| LiveDeco 구간·첫 멤버 앵커 모델 | 106,460 checks 통과 | gate2-A-fixture.log |
| Gate 2 판정 경계 fixture | 18 checks 통과 | 같은 로그 |
| edition 메타데이터 | 22 checks 통과, 일반 Player 실험 타입/참조 없음 | 같은 로그 |
| 설치 게임 패치 대상 유일성 | 17개 통과 | 같은 로그 |
| MatShare 기존 fixture | 25,401 checks, errors=0 | gate2-A-matshare-fixture.log |
| FrameCompare 기존 fixture | 11+15+14+17 checks 통과 | gate2-A-framecmp-fixture.log |
| Gate 1 기존 steps / 새 Gate 2 steps | 25단계 / 129단계, UTF-8 CLI 포함 통과 | gate2-A-gate1-steps-fixture.log / gate2-A-steps-fixture.log |

LoadCheck의 동적 이름 5곳과 Harmony의 실제 Mono 패치 실행은 런타임 확인 대상이다. 모델 fixture·메타데이터 검사로 Unity 렌더링·부동소수점·픽셀·성능·누수 검증을 대체하지 않는다. 기존 deprecated/미사용/어셈블리 참조 경고는 남는다. `.codex/config.toml`은 수정하지 않았다. 공통 인계 파일에 외부에서 추가된 지시는 보존했다.

## 작업 관찰 반환

task-observer v3.5.0의 사용자 범위 헤더와 원칙을 읽었다. 저장소의 스킬·메모리는 수정하지 않고 여기로 반환한다. 문제: 일반 setter Harmony 패치만으로는 TightLate/ColorFast의 직통 엔진 쓰기가 남는다. 개선: 논리 setter와 직통 경로를 함께 추적하고 렌더러 소유권으로 같은 차단 조건을 적용한다. 원칙: 우회 경로를 포함해 엔진 쓰기의 실제 종착점을 감사한다. steps CLI의 임시 파일 검증은 제한된 외부 Temp 대신 워크스페이스의 고유 파일로 실행하고 즉시 지운다.
