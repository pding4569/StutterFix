# Gate 2 변형 B 결과 — 2026-10-21

**최신 5회차: `atlas-vram-budget-512MiB` 중단을 분석하고, sampler별 페이지 계획과 총 512MiB 예산 검사를 결합했다. 계획한 좌표를 실제 할당에도 사용한다. 3종 빌드·LoadCheck·회귀 검사는 통과했다. 실제 아틀라스·소유 수·픽셀·FPS 및 GATE2 통과는 미확인이다.** 게임 실행·설치·커밋·푸시 및 `.codex/config.toml` 변경은 하지 않았다.

## 5회차 — sampler별 아틀라스 계획과 총 메모리 예산

시작 git status/diff는 깨끗했다. 장부와 [최신 전달 증거](gb-autoloop-run2-evidence.txt)를 먼저 읽었다. admission version=4를 지나 `renderer_is_child=False child_parent_is_pivot=True depth=2 path=None count=2127`을 기록한 뒤 최초 on에서 `atlas-vram-budget-512MiB final_on=False`로 중단됐다. 이전 추상 대상·진단 정지·계층 수정은 보존했다. 이 로그는 정점·픽셀 실패나 성능 이득 없음의 근거가 아니다.

### 확인한 결함과 증거 범위

기존 Plan은 모든 sampler의 텍스처를 한 Shelf에 배치해 전역 preferredSize 하나를 골랐다. 실제 Get은 filter/sRGB/aniso별로 페이지를 분리하면서 **각 페이지에 그 전역 크기를 재사용**했다. 계획 당시의 좌표도 버리고 소유 편입 순서로 재배치했다. 따라서 단일 큰 페이지 또는 sampler 분리에 따른 같은 크기의 페이지 중복으로 예산을 넘길 수 있다. RGBA32 8192² 한 장은 256MiB, 세 장은 768MiB이며 16384² 한 장은 1024MiB다.

전달 로그에는 원본 텍스처의 크기·sampler별 수·이미 할당한 페이지 수가 없다. **실제 실패가 위 두 경로 중 어느 쪽인지, 원본 내용 자체가 512MiB를 초과했는지는 미확인**이다. 이들을 확정한 원인이라고 단정하지 않고, 코드로 확인한 계획/할당 불일치를 수정했다.

게임 관리 코드는 `E:/StutterFixWork/decompiled/Assembly-CSharp/scrVisualDecoration.cs:206–245`의 두 SetSprite가 wrapMode=Clamp를 쓰고, `scrDecorationManager.cs:425–450`의 tiling 갱신이 공유 텍스처 wrapModeU/V를 바꾸는 것을 확인했다. [실제 DLL ilscan](gate2-B-run5-sampler-il.log)의 SetPointSamplingVariant·set_wrapMode·set_wrapModeU/V 호출과 교차 확인했다. 게임 메서드는 실행하지 않았다. 이 코드는 정적인 sampler 고정을 보장하지 않으므로 편입 후 sampler 변화 검사도 유지·보강했다.

### 수정

- 생산과 fixture가 같은 `LiveDecoAtlasPolicy.Plan`을 사용한다. filter/sRGB/aniso별로 분리한 뒤 그룹마다 2048부터 장치 한도까지 2의 거듭제곱 정사각 페이지를 열거한다(장치 한도가 작으면 그 이하). 높이·너비 내림차순 first-fit Shelf로 각 그룹의 배치를 만든다. 한 장이 예산을 초과하는 크기는 후보에 넣지 않는다.
- 그룹별 배치 조합을 총 **536,870,912B 이내**에서 선택한다. 열거된 조합 중 페이지 수가 가장 적은 것을 택하고, 페이지 수가 같으면 총 바이트가 작은 것을 택한다. 이는 그룹별 동일 크기 정사각 Shelf 후보 안의 선택이며, 임의 직사각형·혼합 크기 페이지의 전역 최적 배치를 증명하지 않는다.
- 계획의 실제 Shelf 상태와 페이지/좌표를 반환하고 모든 초기 입력의 영역을 예약한다. Get은 해당 좌표를 그대로 쓰고, 해당 페이지가 처음 필요할 때 Texture2D를 할당한다. 편입 순서가 달라도 배치가 달라지지 않는다. 나중에 새로 live가 된 텍스처는 예약되지 않은 같은 sampler의 빈 공간 또는 필요한 크기의 새 페이지를 쓴다. 기존 UV·페이지를 재배치하지 않으며, 새 페이지는 초기 미할당 예약까지 포함한 총 예산으로 검사한다.
- `atlas_plan version=5`, 원본 제외 분포, sampler별 텍스처 수/최대 크기/padded_bytes, 예정 페이지별 크기/바이트, reserved_bytes/allocated_bytes, 성장 요청·실패 로그를 추가했다. 계획이 실패하면 GPU 페이지를 할당하기 전에 입력 규모와 이유가 남는다. cost의 atlas_pages/atlas_bytes는 실제 할당한 페이지/바이트다. CPU readable 사본·readback 임시 메모리는 이 GPU 페이지 예산과 별도다.
- Entry.Valid에 너비·높이·aniso·sRGB 검사를 추가했다. 기존 filter/wrap/mip 검사도 유지한다. 변경된 캐시 엔트리를 그대로 재사용하지 않고 ERROR/복원으로 종료한다. 기존 논리 setter/색 차단, 정점 오차 1e-4, 원본 복원, 2px extrusion·4px 배치·double UV 계산·RGBA32 경로, Player 제외·기본 꺼짐·엄격 판정은 유지했다.

### 오프라인 검증

| 검사 | 결과 | 근거 |
|---|---|---|
| Release Dev / Player / PlayerAuto | 오류 0/0/0, 경고 38/4/38 | gate2-B-run5-build-dev/player/auto.log |
| LoadCheck Dev / Player / Auto | 275/229/275곳, 틀림 0/0/0 | gate2-B-run5-loadcheck-dev/player/auto.log |
| 새 공유 아틀라스 계획 | 118,278 checks | gate2-B-run5-fixture.log |
| 기존 계층 포함 LiveDeco / 아틀라스·판정 | 23,009 / 1,052,924 checks | 같은 로그 |
| LiveDeco edition / 실제 DLL 패치 계획 | 22 / 38 checks | 같은 로그 / gate2-B-run5-patch-check.log |
| FrameCompare | RGBA 11, mask 15, state 14, control 49, edition 13 | gate2-B-run5-framecmp-fixture.log |
| MatShare / 진단 edition | 25,401 checks / 진단 32·필수 6·폐기 5 타입 조건 통과 | gate2-B-run5-matshare-fixture.log / gate2-B-run5-edition-check.log |
| Gate1/Gate2 steps | 25/144단계·UTF-8 CLI 통과 | gate2-B-run5-gate1-steps-check.log / gate2-B-run5-steps-check.log |

합성 회귀 입력 ①2044² 텍스처 17개는 기존 한 장 계산 16384²=1024MiB 대신 8192² 두 장=512MiB로 계획한다. ②같은 크기 텍스처 5개씩 sampler 3그룹은 전역 8192²를 세 그룹에 복제한 768MiB 대신 총 다섯 장=512MiB로 계획한다. **Arche의 실측 텍스처나 실제 할당 결과가 아니다.** 패딩 포함 내용 자체가 예산을 넘는 33개 입력은 계속 거절한다. 역순 편입·초기 예약과 후속 영역의 비중첩·sampler 분리·결정적 좌표·동일 페이지 수에서 최소 바이트 선택·80개 무작위 입력도 검사한다.

첫 fixture 실행은 장치 한도 128에서 sampler 두 그룹을 단일 최소 페이지 예산으로 허용한다고 잘못 설정해 실패했다. 해당 시험 예산/기대값을 최소 페이지 두 장으로 바로잡았으며, 생산 예산과 통과 기준은 변경하지 않았다. 첫 로그는 gate2-B-run5-plan-first.log에 보존했다. 실패한 오프라인 검사 프로세스가 apphost 파일을 잡고 있어 해당 실행의 PID·경로를 확인해 종료한 뒤 최종 검사를 완료했다. 최종 fixture 빌드는 경고/오류 0/0이다.

LoadCheck의 기존 동적 이름 5곳과 기존 빌드 경고는 남는다. 일반 Player DLL 및 전달 144 steps JSON 해시는 run4와 동일하다. [최종 산출물·소스 해시](gate2-B-run5-artifacts.txt)를 남겼다. 순수 배치·메타데이터·IL 검사는 Unity native 할당/정점·픽셀·게임 Mono GC·VRAM 피크·FPS 증명이 아니다.

### 판정과 다음 관측

전달 ProfSet은 **971,762표본 / 69.830267%**다. sampled Step/Apply/OnUpdate 표본 시간 비율이며, 전체 DecoAnim/native 쓰기 몫은 미입증이다.

**GATE2=PASS 미확인.** 실제 소유/런·정점·픽셀/복원·GC/VRAM·FPS는 미측정이다. 현재 증거로 RGBA32가 구조적으로 예산을 만족할 수 없다고 결론 내릴 수 없다. 변경된 Dev/Auto DLL과 기존 144 steps를 인계한다. 다음 외부 시험은 기존 admission version=4와 새 atlas_plan version=5, atlas_sampler/atlas_plan_page/ready/failed를 먼저 확인한다. padded_bytes가 예산을 초과하면 이 전체 원본 텍스처 RGBA32·패딩 방식으로 전부 담을 수 없는 수치 근거가 된다. padded_bytes가 예산 이하면 단순 면적만으로 가능/불가능을 단정하지 말고 sampler별 페이지와 배치 여유를 봐야 한다. 새 배치에서도 페이지 수 증가에 따른 런 수·FPS 이득은 미보장이다.

소유가 생겨도 262/270/276초 모두 ab_out=0·복원과 자연 재생 ABBA 평균 FPS +15%가 필요하다. 1/255 차이를 PASS로 세지 않으며, Texture2DArray 비교는 아래에 보존했다. 게이트 3·워커 분할·예산 상향·텍스처 축소는 하지 않았다. 게임 실행·설치·커밋·푸시·config 수정은 하지 않았다.

task-observer v3.5.0 관찰은 report-back으로 반환한다. 문제: 단일 페이지의 수용 가능성으로 크기를 정한 뒤 실제 할당에서는 sampler별로 복제하여 총 메모리 한도를 뒤늦게 초과할 수 있다. 개선: 실제 분리 키와 모든 페이지를 포함해 계획하고, 계획 좌표를 할당과 공유하며 원본 면적·예약·할당을 구분해 남긴다. 원칙: 자원 계획과 실행은 같은 분할 규칙·배치 결과·총예산을 사용해야 한다. 유형은 프로젝트 내부 구현/검증 관찰이다. 사용자 범위 관찰 저장소는 쓰기 범위 밖이므로 스킬·메모리는 수정하지 않았다.

## 4회차 — 실제 renderer 계층 합성

시작 시 git status/diff는 깨끗했다. 장부와 [최신 전달 증거](gb-autoloop-run1-evidence.txt)를 읽고 이전 추상 패치·정지 수정은 보존했다. 전달 증거는 admission version=3, editing=True, diagnostic_playback_pause=True를 지나 **live=2074, owned=0, runs=0, remaining_reason=child-hierarchy count=2074**, vertexcheck count=0으로 끝난다. cost는 vertex_ms=8.7111, upload_ms=0.8408, runs_ms=28.8660이다. 최초 실패 프레임 비용이며 정상 재생 성능·픽셀 실패의 근거는 아니다.

### 코드 대조와 확인 범위

기존 InputReason은 pivot/child 누락, renderer.transform!=childTransform, childTransform.parent!=pivotTrans를 같은 사유로 기록했다. **전달 로그만으로는 실패한 세부 조건이나 실제 계층 형태를 확정할 수 없다.** 특정 형태가 2,074개라고 추정하지 않는다.

디컴파일 `E:/StutterFixWork/decompiled/Assembly-CSharp/scrDecorationManager.cs:258–273`의 Image 갈래는 이미지 프리팹을 Instantiate하고 scrVisualDecoration을 얻는다. pivot/child를 대입하는 285–286행은 별도 Prefab 갈래다. 이미지의 pivotTrans·childTransform·spriteRenderer는 별도 직렬화 참조이며, 이 관리 소스는 같은 Transform 또는 직접 부모임을 보장하지 않는다. `scrDecoration.SetPosition`(229–249행)은 child localPosition에 offset을 쓰며 SetRotation·SetScale·UpdatePosition은 pivot을 갱신한다. [실제 DLL ilscan 호출/필드 목록](gate2-B-run4-hierarchy-il.log)과 대조했다. 이는 관리 코드 대조이며 **프리팹 자산의 직렬화 계층을 읽은 증거는 아니다.**

기존 Calculate는 pivotWorld * childLocal만 계산해 렌더러가 더 아래에 있으면 로컬 변환을 누락한다. 제외 조건만 제거하면 잘못된 정점이 생기므로 판정과 계산을 함께 변경했다.

### 수정

- 생산/fixture 공유 `LiveDecoHierarchy.Compose`가 renderer부터 pivot 직전까지 각 local TRS를 왼쪽에 곱한다. 그 경로에 child가 반드시 있어야 한다. child 위·아래의 중간 노드도 계산한다. owned일 때 **child 노드 위치만** 논리 ChildPosition으로 치환한다. 나머지 위치와 각 노드의 rotation/scale은 매번 읽고, pivot 월드 계산·sprite flip은 기존 순서로 결합한다.
- 최대 16노드로 제한한다. 누락, child=pivot, child를 지나지 않는 renderer, pivot 미도달, 깊이 초과는 각각 child-hierarchy-missing/child-is-pivot/renderer-outside-child/pivot-not-ancestor/depth-limit로 제외한다. 매 Tick 재검사하므로 범위 밖 재부모화는 소유를 해제한다. 형제 renderer 지원을 가정하지 않는다.
- 초기 실제 SpriteRenderer 월드 정점과의 **1e-4** 대조, 편집 독자·히트박스·마스크·floor·filter·pivot 부모 identity 제한은 유지한다. 논리 setter·색·원본 복원·기존 patch_plan version=2도 유지했다.
- 새 식별 줄은 `admission version=4 hierarchy=renderer-to-pivot logical_child=1 max_depth=16`이다. 초기 live 집합을 `hierarchy renderer_is_child=... child_parent_is_pivot=... depth=... path=... count=...`로 집계한다. 다음 시험에서 기존 가정 중 어느 조건이 실패했는지 확인할 수 있다. 이름은 기록하지 않는다.
- 생산 순회는 구조체 어댑터를 사용하며 경로별 배열·delegate를 만들지 않는다. 초기 분포 dictionary/string은 전환 시에만 만든다. 더 긴 경로의 native getter·행렬 비용은 증가할 수 있어 실제 cost/GC/FPS 검증이 남는다.

### 오프라인 검증

| 검사 | 결과 | 근거 |
|---|---|---|
| Release Dev / Player / PlayerAuto | 오류 0/0/0, 경고 38/36/38 | gate2-B-run4-build-dev/player/auto.log |
| LoadCheck Dev / Player / Auto | 275/229/275곳, 틀림 0/0/0 | gate2-B-run4-loadcheck-dev/player/auto.log |
| 공유 계층 fixture | 25 checks | gate2-B-run4-fixture.log |
| LiveDeco 정책/정렬 (위 25개 포함) | 23,009 checks | 같은 로그 |
| 아틀라스/판정 / LiveDeco edition | 1,052,924 / 22 checks | 같은 로그 |
| 실제 DLL 패치 계획 | 38 checks, 18개 대상/추상 2개 제외 | gate2-B-run4-patch-check.log |
| FrameCompare | RGBA 11, mask 15, state 14, control 49, edition 13 | gate2-B-run4-framecmp-fixture.log |
| MatShare / 진단 edition | 25,401 checks / 기존 분리 검사 통과 | gate2-B-run4-matshare-fixture.log / gate2-B-run4-edition-check.log |
| Gate1/Gate2 steps | 25/144단계·UTF-8 CLI 통과, 기존 JSON 해시 유지 | gate2-B-run4-gate1-steps-check.log / gate2-B-run4-steps-check.log |

계층 fixture는 직접 child, child 위·아래 중간 노드, 비균일 스케일·회전·음수 스케일의 네 코너, 논리 위치 치환·무쓰기, 누락·형제·재부모화·깊이 제한을 검사한다. 공유 루프 10,000회 반복의 .NET 관리 할당은 0B다. System.Numerics 행벡터 규약은 어댑터에서 곱셈 순서를 뒤집는다. **Unity native 정점·게임 Mono 할당·실제 픽셀의 증거는 아니다.** LoadCheck 기존 동적 이름 5곳과 기존 경고는 남는다. 일반 Player DLL SHA256은 run3와 같다. [산출물/소스 해시](gate2-B-run4-artifacts.txt)를 남겼다.

이번 전달 ProfSet 비율은 **967,476표본 / 69.881109%**다. 아래 과거 수치와 구분한다. sampled Step/Apply/OnUpdate 설정 시간 비율이며 전체 DecoAnim 또는 native 쓰기만의 비율은 미입증이다.

### 판정과 다음 관측

**GATE2=PASS 미확인.** 실제 계층·소유/런·정점·픽셀/복원·GC/VRAM·FPS를 새로 측정하지 않았다. 계층 제한이 설계상 전원 소유를 불가능하게 만든다고 확정할 근거도 아직 없다. 이전 실패 코드를 반복하지 않고 변경된 Dev/Auto DLL과 기존 144 steps를 인계한다. 다음 외부 시험에서는 admission version=4, 초기 hierarchy 분포, vertexcheck count/max_err 및 최초 owned/runs/remaining_reason부터 확인한다. 범위 밖 경로라면 세부 수치로 한계를 판단한다.

소유가 생겨도 262/270/276초 ab_out=0·복원과 ABBA 평균 FPS +15%를 모두 만족해야 PASS다. 1/255 허용이나 gate 3·워커 분할은 추가하지 않았다. Texture2DArray 비교는 아래 절에 보존했다. 게임 실행·설치·커밋·푸시·config 변경은 하지 않았다.

task-observer v3.5.0 관찰을 report-back으로 반환한다. 문제: 여러 계층 가정을 단일 제외 사유로 묶어 전원 거절에서도 실패 조건을 구분하지 못했다. 개선: 실제 참조 관계를 초기 분포로 남기고, 계층 지원 확장에는 대응하는 행렬 계산과 정점 대조를 함께 둔다. 원칙: 소유 판정의 범위를 넓힐 때 출력 계산도 같은 범위를 지원해야 한다. 유형은 프로젝트 내부 구현/검증 관찰이다. 사용자 범위 관찰 저장소는 쓰기 범위 밖이므로 스킬·메모리는 수정하지 않았다.

## 3회차 — 진단 정지 소유 판정 (중단 작업 이어하기)

장부와 최신 [전달 증거](gb-autoloop-run1-evidence.txt)를 먼저 읽고 시작 시 변경된 `FrameCompare.cs`, `FrameCompareControlPolicy.cs`, `LiveDecoPolicy.cs`, `LiveDecoRuntime.cs`를 보존해 이어서 수정했다. 이번 로그는 `patch_plan version=2 targets=18 abstract_declarations_skipped=2` 및 RGBA32 경로를 지나 최초 on에서 `zero-owned-see-exclusions final_on=False`로 끝난다. 이전 추상 패치 실패 수정을 반복하지 않았다.

### 원인과 증거 범위

전달 steps는 편집기 재생 중 pause 후 on을 호출한다. `FrameCompare.Pause()`는 `controller.paused=true`를 설정한다. `E:/StutterFixWork/decompiled/Assembly-CSharp/ADOBase.cs:79`의 `isEditingLevel`은 편집기에서 바로 `controller.paused`를 반환한다. 기존 InputReason은 이 값이 true이면 모든 장식에 `transform-reader-editor`를 반환했다. 즉 이 정지 경로에서는 개별 장식의 실제 편집기 독자 여부와 무관하게 전원 거절되는 결함이 있다.

설치 DLL의 [ilscan 결과](gate2-B-run3-admission-il.log)에서도 `get_isEditingLevel`의 `get_isLevelEditor`, `get_controller`, `scrController.get_paused` 호출을 확인했다. 게임 DLL SHA256은 기존과 같은 `F692210F9A342F7EED4BE40AAD463286AB40BF046599959823942EA02F4CA3FF`다. 게임 메서드는 실행하지 않았다.

기존 Tick은 구간 생성/정점 검사 변화 또는 120프레임마다만 제외 분포를 출력했다. 첫 Tick에서 전원 거절되면 조건이 모두 false이고 Begin이 곧바로 예외를 던져 `remaining_reason`이 유실된다. 전달 로그에는 원인별 수가 없으므로 **전체 소유 0의 유일한 원인이 이 조건이었다거나 수정 후 소유 수가 몇 개인지는 확정하지 않는다.** 정지 판정 결함 자체는 코드로 확인했다.

### 수정

- `FrameCompare.FrozenPlayback`은 실제 재생에서 진입했고 원래 paused=false였으며, 같은 controller가 살아 있고 controller.paused·Time.timeScale=0·DOTween.timeScale=0·AudioListener.pause가 유지될 때만 true다. Resume 시 진입 정보를 초기화한다. 일반 편집 정지·재생 외 정지·교체된 controller·손상된 정지에는 예외를 부여하지 않는다. 시작/매 프레임 수명 검사도 이 조건을 사용한다.
- `LiveDecoPolicy.EditorTransformReader`는 이 진단 정지에 한해 전역 편집 상태만으로 전체를 거절하지 않는다. 선택 테두리·히트박스 테두리·현재 선택 장식 및 실제 활성 기즈모의 읽기 대상은 계속 제외한다. 다른 소유 조건·vertexcheck·픽셀/FPS PASS 조건은 유지했다.
- `DecoTransformGizmoHolder.cs:41`의 LateUpdate는 기즈모 표시 검사 전에 캐시된 `currentDec.GetDecorationWorldSize()`와 Transform을 읽는다. [실제 IL](gate2-B-run3-gizmo-il.log)도 이 읽기를 확인했다. Begin에서 기즈모 목록을 보관하고 활성 기즈모의 private currentDec를 검사한다. 선택 해제 뒤 캐시만 남은 대상도 제외한다. 새 FieldRefAccess는 LoadCheck로 확인했다. 기존 고정 scene snapshot의 동적 생성 제한은 그대로다.
- 첫 Tick에도 cost·vertexcheck·모든 nonzero `remaining_reason`을 출력한다. 새 식별 줄은 `[livedeco] admission version=3 editing=... diagnostic_playback_pause=...`이다. 기존 patch_plan version=2도 유지한다. 다음 외부 시험은 소유 0 실패에서도 구체 분포를 남긴다.

### 오프라인 검증

| 검사 | 결과 | 근거 |
|---|---|---|
| Release Dev / Player / PlayerAuto | 오류 0/0/0, 경고 38/4/38 | gate2-B-run3-build-dev/player/auto.log |
| LoadCheck Dev / Player / Auto | 275/229/275곳, 틀림 0/0/0 | gate2-B-run3-loadcheck-dev/player/auto.log |
| 실제 DLL 패치 계획 | 38 checks, 본문 18개/추상 제외 2개 | gate2-B-run3-patch-check.log |
| LiveDeco 정책/정렬 | 22,984 checks (편집 독자 조건 32조합 추가) | gate2-B-run3-fixture.log |
| 아틀라스/판정 및 LiveDeco edition | 1,052,924 / 19 checks | 같은 로그 |
| FrameCompare | RGBA 11, mask 15, state 14, control 49, edition 13 checks | gate2-B-run3-framecmp-fixture.log |
| MatShare | 25,401 checks, 오류 0 | gate2-B-run3-matshare-fixture.log |
| 진단 edition | 32/32 분리, 런타임 6/6 유지, 폐기 타입 5/5 양쪽 부재 | gate2-B-run3-edition-check.log |
| Gate1/Gate2 steps | 25/144단계·UTF-8 CLI 통과, 기존 입력 해시 유지 | gate2-B-run3-gate1-steps-check.log / gate2-B-run3-steps-check.log |

FrameCompare control에는 정지 출처 32조합을 추가했다. 위 검사는 순수 정책·관리 메타데이터 증거이며 Unity의 실제 소유·정점·수명·복원·픽셀·성능을 증명하지 않는다. LoadCheck의 기존 동적 이름 5곳과 기존 빌드 경고는 남는다. 산출물/소스 SHA256과 입력 해시는 [run3 목록](gate2-B-run3-artifacts.txt)에 남겼다. 일반 Player DLL은 run2와 동일한 SHA256이다.

이번 전달 프로파일은 **962,183표본, set_pct=70.352910%**다. 아래 역사 절의 960,572/70.130186%와 구분한다. 이는 sampled Step/Apply/OnUpdate의 ProfSet 비율이며 DecoAnim 전체나 순수 native 쓰기 비중은 아니다. 특히 크기 setter 일부는 ProfApply에 포함된다.

### 다음 관측과 중단 판단

현재는 재현 가능한 정지/분류 코드 결함을 수정한 상태이며 설계 한계로 확정하지 않았다. 변경된 DLL과 기존 144 steps를 인계한다. 최초 pause/on에서 admission version=3 및 `editing=True diagnostic_playback_pause=True` 여부, 첫 cost의 owned/live/runs 및 vertexcheck·remaining_reason부터 확인해야 한다. 여전히 소유 0이면 새 분포로 원인을 판단한다. 같은 DLL의 실패 시험 반복을 해결책으로 요청하지 않는다.

이후 관측은 실제 아틀라스/메모리·GC·세 시점 ab_out/maxdiff/aa2_out·복원·ABBA FPS다. **GATE2=PASS는 미확인**이며 1/255 차이도 PASS로 낮추지 않았다. 게임 실행 금지를 지키면서 새 게임 수치를 만들지는 않았다. Texture2DArray 비교와 기타 구현 제한은 아래 절에 보존했다. 게이트 3·워커 분할은 구현하지 않았다.

작업 관찰은 task-observer v3.5.0의 report-back 방식으로 이 문서에 반환한다. 문제: 진단 도구가 설정한 paused 값이 게임의 편집 상태와 같아 보수적 분류가 모든 대상을 거절하고 최초 실패에는 주기 로그도 남지 않았다. 개선: 진단 상태의 진입 출처·생존 조건을 추적하고 실제 독자는 개별 제외하며 최초 실패 분포는 항상 기록한다. 원칙: 상태를 일시 변경하는 검증 도구와 그 상태를 읽는 최적화의 상호작용도 검증 대상이다. 유형은 프로젝트 내부 구현/검증 관찰이다. 사용자 범위 관찰 저장소는 현재 쓰기 허용 범위 밖이므로 별도 관찰 파일·스킬·메모리는 수정하지 않았다.

## 2회차 오류 분석과 수정

장부와 [직전 자동 시험](gb-autoloop-run1-evidence.txt)을 먼저 확인했다. 최초 `livedeco on`이 `BadImageFormatException:Method has no body`로 끝났고 `final_on=False`였다. 세 시점 픽셀·ABBA FPS까지 진행하지 못했다. 따라서 이 로그는 아틀라스의 픽셀 실패나 이득 없음의 근거가 아니다.

디컴파일 `E:/StutterFixWork/decompiled/Assembly-CSharp/scrDecoration.cs:189,193`은 `SetVisible(bool)`·`SetDepth(int)`가 추상 선언임을 보여 준다. 기존 Runtime.Begin은 구조 setter를 이름으로 모으면서 추상 메서드도 포함해 총 **20개**를 Harmony에 넘기려 했다. `tools/ilscan`의 [메서드 목록](gate2-B-run2-patch-il.log), 실제 DLL의 reflection·PE 메타데이터를 사용하는 [새 회귀 검사](gate2-B-run2-patch-check.log)로 두 선언의 **Abstract=true, RVA=0**을 확인했다. `scrVisualDecoration.cs:327,431`의 재정의에는 실제 본문이 있다. 게임 DLL SHA256은 `F692210F9A342F7EED4BE40AAD463286AB40BF046599959823942EA02F4CA3FF`다. 전달 로그에 스택은 없지만, 기존 대상 목록에 본문 없는 메서드가 포함된 코드 결함은 확정했다.

- `LiveDecoPatchPlan.cs`에서 **18개 실제 구현 대상**과 **제외한 추상 선언 2개**를 구분한다. 추상 선언을 제외할 때 실제 visual 재정의가 존재하고 같은 virtual slot인지 검증하므로, SetDepth/SetVisible의 소유 해제 훅은 유지한다. SetSprite 두 오버로드와 나머지 구조 변경 훅도 유지했다.
- 이름·정확한 인자 타입·인스턴스 호출 규약·void 반환·유일성·관리 본문을 모두 확인한다. 필수 대상이 추상/본문 없음이면 이름을 포함해 실패한다. 다른 모드의 Harmony 소유자 검사도 유지한다. 전체 계획을 아틀라스 할당과 패치 적용 **전에** 검증한다.
- 설치가 다른 원인으로 실패하면 `patch-install <타입>.<메서드>:<예외>:<내용>`을 남겨 다음 실패 지점을 식별한다. 기존 StopRuntime/UnpatchAll/렌더러 복원 경로는 유지했다.
- 새 빌드 식별 로그는 `[livedeco] patch_plan version=2 targets=18 abstract_declarations_skipped=2`다. 일반 Player에서는 새 계획 타입도 제외한다. 기본 꺼짐·판정 조건·아틀라스/정점 계산·144-step 시험 입력은 유지했다. 게이트 3은 구현하지 않았다.

새 검사는 생산 코드와 같은 계획 생성기를 링크하고 설치 DLL을 읽는다. **38 checks**로 추상 선언/실제 PE RVA, 본문 존재, 중복 없음, 8개 상태 쓰기 훅, 구조 setter 보존, 두 SetSprite 오버로드, 필수 추상 대상 거절을 확인했다. 게임 메서드·Unity native·Harmony detour는 실행하지 않았다. 명령은 `dotnet run --project tools/optimization/LiveDecoPatchCheck -c Release -- "<게임 Managed>/Assembly-CSharp.dll"`이다.

이는 본문 없는 대상을 배제하는 오프라인 검증이며, 게임 Mono에서 Harmony 설치가 성공했음을 증명하지 않는다. 다음 외부 자동 시험은 **변경된 DLL**로 기존 144 steps를 사용하고 위 version=2 로그부터 확인해야 한다. 이후 owned/runs·vertexcheck·atlas 메모리·GC·픽셀·복원·ABBA FPS를 관측한다. 같은 코드의 실패 시험을 반복하지 않았다. 새 빌드 해시는 [산출물 목록](gate2-B-run2-artifacts.txt)에 기록한다.

공통 명세의 로드맵은 이 워크트리에 없어 `E:/StutterFixWork/StutterFix/docs/ROADMAP-EXTREME-20261021.md`를 읽었다. freeze 결과 문서는 이 워크트리와 canonical 문서 경로 모두에 없었다. 시작 시 이미 변경된 `codex-al-gb-2.md`와 중단 전에 남긴 run2 로그는 이어받았고, 사용자 인계 변경은 보존했다.

공통 명세가 게이트 1을 통과로 결정했으므로 기존 MPB 전체 이름 열거 문제를 다시 중단 조건으로 삼지 않았다. 알려진 커스텀 MPB는 제외하고 미지의 속성 문제는 실제 최종 화면 비교에 남겼다. 작업 중 공통 문서에 추가된 DecoAnim 채널 쓰기 차단 지시도 반영했다. 공통 문서의 추가 3줄은 외부에서 들어온 변경이며 이 작업에서 작성하거나 되돌리지 않았다.

## 구현

- `livedeco on|off|state`는 Dev/AutoTest에서만 제공한다. 설정·로드 시 자동 활성화하지 않는다. 기존 `census`도 유지했다.
- `LiveDecoRuntime.cs`가 원래 scene 렌더러의 정렬 키와 재질 슬롯을 모은다. 소유 장식이 연속인 구간에서 첫 멤버의 **원래 재질 인스턴스**, sortingLayer/order, z를 빌린 MeshRenderer로 그린다. 원본 재질을 변경하거나 복제하지 않고 메시의 MPB에만 아틀라스를 바인딩한다. 텍스처 자체는 구간을 나누지 않으며 실제 아틀라스 페이지·재질 서명·Unity layer·정렬 bucket이 다르면 분리한다. 비소유 장벽과 동률 불확실성을 유지한다.
- 원래 `Sprites/Tile` 셰이더와 재질을 그대로 사용한다. 새 셰이더·번들·키워드는 추가하지 않았다. 재질의 색·블렌드·출력 식은 원래 경로에 맡긴다. 텍스처 ST가 identity가 아니거나 RepeatX/Y가 1이 아니면 제외한다. 타일링·특수 블렌드·mask·split-alpha·비사각 sprite도 원래 경로에 남긴다.
- 정점은 게임의 논리 필드에서 직접 계산한다. `pivotPosVec`, `pivotOffsetVec`, `croppedPivotOffset`, PPU/피벗을 포함한 원래 sprite.vertices/uv/triangles, flip, child 위치·회전·크기, parallax, 화면 고정, followPlanet, 카메라 회전/배율 잠금을 처리한다. 타일에 붙은 장식과 시차 축의 이전 위치에 의존하는 장식은 제외한다. 소유 직전 실제 SpriteRenderer의 월드 정점 4개와 비교하며 최대 오차가 1e-4를 넘거나 비유한 값이면 제외한다.
- `scrDecoration.UpdatePosition` Harmony Prefix와 `TightLate.Process`의 직접 Transform 경로를 모두 차단한다. `DecoAnim`의 일반·완료·XY 합침 setter delegate도 명시적으로 감싸, 위치·피벗·회전·크기·색·불투명도·시차 offset의 논리 필드만 갱신한다. `MoveApply.DeferTransform/UpdateNow`, `ColorFast`의 직접 색 쓰기, `InvisibleSkip.After`의 우회도 처리했다. 논리 rendererColor를 계산하므로 다음 효과의 시작값과 GetVisible/GetAlpha 입력을 보존한다. 게이트 3 평가기·워커 분할은 만들지 않았다.
- 소유 중 원본 SpriteRenderer에는 `forceRenderingOff`를 적용한다. 해제할 때 현재 논리 색·child offset·UpdatePosition을 원래 렌더러에 반영한다. 장식의 구조 setter는 먼저 소유를 해제한다. `SceneReset`, `ExitFix.LoadLevelPrefix`, scene 변경, 기존 `Main.Unload`/AutoTest 정리가 Stop으로 이어진다. 이 수명 동작의 실제 게임 검증은 남아 있다.
- 모든 구간이 **하나의 재사용 인터리브 Vertex 배열**(Vector3 + Color32 + Vector2)을 공유한다. 각 mesh에 필요한 부분만 SetVertexBufferData로 올린다. 지정한 DontValidateIndices/DontRecalculateBounds/DontNotifyMeshUsers 플래그와 고정 bounds를 쓴다. 변하지 않은 구간의 메시·멤버 배열·재질은 재사용하며, 바뀐 구간만 생성/폐기한다.
- `cost`는 정점 계산·업로드·정렬/구간 갱신 시간과 new/reused runs, live/owned, atlas pages/textures/bytes, GC thread allocation delta를 기록한다. 전환 프레임과 120프레임마다 출력하며 프레임 카운터는 매 프레임 집계한다. 정상 프레임의 실제 0B 할당은 아직 입증하지 않았다. 편입·페이지 추가·구간 변경은 전환 할당으로 분리한다.

## 아틀라스 선택과 제한

`LiveDecoAtlas.cs`는 RGBA32 시험 경로다. 원본이 readable RGBA32이면 그대로 읽고, 나머지는 동일 크기의 GPU 출력→RGBA32 readback을 사용한다. RT active와 GL.sRGBWrite는 finally에서 복원한다. 이 변환의 sRGB·양자화 동일성은 게임 픽셀 비교 대상이다.

압축 내부 영역을 4px CopyTexture로 복사하는 것만으로는 임의 DXT 경계 블록의 바이트 보존과 실제 가장자리 색으로 만든 2px extrusion을 함께 보장하지 못한다. 현재 구현은 재압축을 넣지 않고 허용된 RGBA32로 대체한다. **DXT 바이트 그대로 복사하는 최적화는 구현하지 않았다.**

각 원본 텍스처 전체를 최소 2px의 실제 가장자리/모서리 색으로 확장한다. 배치 사각형은 4px 단위, 내용 원점은 짝수 픽셀이다. 원래 sprite UV를 double의 uMin/uMax로 변환하고 마지막에 float로 한 번만 내린다. 자동 리사이즈·텍스처 축소·재압축은 하지 않는다. filter/sRGB/aniso가 다른 소스는 별도 페이지로 나누고, mip chain 및 non-Clamp wrap은 제외 사유별 개수를 남긴다. 최신 5회차는 위 절처럼 sampler별 다중 페이지를 총 512MiB 안에서 계획하고 실제 좌표도 공유한다. 최소 페이지 크기는 2048(장치 한도가 작으면 그 이하)이다. CPU readable 사본 메모리는 별도로 든다. 페이지가 여러 장이면 런 수도 증가한다.

| 경로 | 텍스처/UV 특성 | 남는 위험·비용 |
|---|---|---|
| 현재 RGBA32 아틀라스 | 크기 변경 없이 2px extrusion, UV 한 번 변환 | readback/sRGB, UV 보간, Color32 양자화; 실제 페이지 수/VRAM/런 수 미측정 |
| 같은 크기끼리 Texture2DArray 대안 | 원본 UV.xy를 유지하고 uv.z=slice, 호환 포맷이면 slice별 CopyTexture로 압축 바이트 유지 가능 | 크기·포맷·filter·mip·wrap별 배열 분리로 런 증가 가능; 배열 셰이더와 원래 재질 ID 앵커 보존 설계 필요 |

아틀라스에서 최대 채널 차이 1/255가 관측되면 `[livedeco2] PIXEL_NEAR`로 표시하고 PASS로 세지 않는다. 원인이 UV/샘플링이면 배열 대안을 비교한다. 배열은 Color32 색 양자화나 잘못된 정점을 해결하지 않으므로 위치별 차이·maxdiff와 함께 원인을 구분해야 한다. 자동으로 배열 경로로 전환하거나 동일 시험을 코드 변경 없이 반복하지 않는다.

## Transform 읽기 감사

읽기 전용 디컴파일을 현재 설치 게임 DLL의 IL과 교차 확인했다. [메서드·필드 IL](gate2-B-il.log), [전체 지정 필드/호출 읽기 목록](gate2-B-transform-readers.log). Assembly-CSharp SHA256은 `F692210F9A342F7EED4BE40AAD463286AB40BF046599959823942EA02F4CA3FF`이며 지정 읽기 목록은 52행이다. 도구는 PE 메타데이터를 읽었으며 게임 코드를 실행하지 않았다.

- scrDecoration.UpdatePosition은 localPosition→parallax start/posCamAtStart→SetTrans→SetRotation→SetScale 순서다. SetRotation은 startRot+rotAngle에 floor 또는 camera 회전을 더하고, SetScale은 scaleVec*camScaleMultiplier*floor scale이다. 지원 범위는 같은 연산 순서로 계산하고 floor 부착은 제외한다.
- scrParallax.SetTrans의 화면 고정/카메라 시차·parallaxOffset, private startPosition/endPosition을 확인했다. start/end 상태도 렌더 직전에 논리적으로 갱신한다. dontAlterX/Y의 이전 Transform 위치 의존은 제외한다.
- `GetDecorationWorldSize`는 pivot.localScale을 읽는다. `DecoTransformGizmoHolder`의 gizmo 경로와 drag는 편집기 경로다. 편집 상태·활성 선택/히트박스 테두리를 소유에서 제외한다.
- `SpriteAlphaMaskUtils`는 obj.transform과 SpriteAlphaMask.pivotTrans/childTrans를 읽는다. mask·다른 메시 출력·mask interaction·사용 중인 per-decoration filter는 제외한다.
- 히트박스/활성 collider 및 floor 부착은 제외한다. 최신 4회차는 renderer→pivot 경로에 child가 포함되는 최대 16노드 계층을 합성한다. pivot 부모의 world matrix는 여전히 identity여야 한다. 초기 renderer=child·직접 부모 제한은 위 4회차 절처럼 변경했다.
- 이동 효과 StartEffect/클로저는 pivotPosVec/offset/rotAngle/scaleVec의 논리 필드를 읽는다. 이 값들은 계속 갱신한다. manager tiling/wrap 갱신은 소스를 변경하므로 sampler 변경을 재검사하고 소유를 해제한다.

이 감사는 해당 게임 코드의 지정 참조 목록과 디컴파일 검토다. 다른 모드의 reflection·Transform alias 사용 전체를 증명하지 않는다. 외부 Harmony hook은 편입 시 거절한다. 고정 scene snapshot 이후의 임의 외부 renderer 생성은 지원하지 않으며, manager 목록 크기 변경과 scene/reset은 정리 후 종료한다. 실제 live 집합의 켜짐·꺼짐·깊이·이미지 변경은 원래 목록 안에서 재분할한다. 원근/비스듬한 카메라/custom sort axis, 외부 SortingGroup, 불확실한 재질 동률도 ERROR로 복원한다.

## DecoAnim 설정 비용

직전 자동 시험이 전달한 표본은 **960,572개, set_pct=70.130186%**다. 채널별 apply_us/set_us는 위치 X 0.072866/0.562924, 위치 Y 0.073281/0.216343, 회전 0.073331/0.129199, 크기 X 0.142760/0.027674, 크기 Y 0.134887/0.030378, 색 0.073243/0.262872, 불투명도 0.079391/0.575806이다. 이는 표본 Step/Apply/OnUpdate 시간에서 ProfSet 비율이며, DecoAnim 전체 1.9ms의 70.13% 또는 순수 native 쓰기 비율이라는 뜻은 아니다. 특히 크기 setter는 ProfApply에 포함된다. 게임을 새로 실행하지 않고 전달 로그에서 인용한 수치다.

`livedeco profile begin|end`를 추가했다. 기존 ProfN/ProfApply/ProfSet을 차분하여 채널별 apply_us/set_us와 총 표본 시간의 set_pct를 출력한다. PlayerAuto에서도 이 별도 구간 동안만 샘플링을 켜고 원래 상태로 복원한다. 생성 steps는 다른 시험보다 먼저 livedeco가 꺼진 262→276초에서 이를 측정한다. ProfApply에는 ApplyE의 크기 setter가 포함되므로, 출력의 set_pct를 모든 native 쓰기 비중이나 DecoAnim 전체 비용 비중으로 해석하면 안 된다. 프로파일 창은 ABBA FPS 근거에 포함하지 않는다.

## 자동 시험 입력과 판정

- 생성기: [livedeco2-steps.py](../../tools/optimization/livedeco2-steps.py)
- 생성 완료 입력: [gate2-B-steps.json](gate2-B-steps.json), 기존 Arche chart 인자를 재사용한 **144 steps**, 실행하지 않음.
- 프로파일 창 다음에 262/270/276초 독립 seek→pause→A→on→B→off→A2를 수행한다. 최종 카메라 체인·원래 필터는 켠 채, UI 제외/중앙 20% 마스크를 사용한다. PNG와 outside 차이 수·위치·maxdiff, A2 복원 차이를 남긴다. 각 시점 후 캡처 버퍼를 비운다.
- 다음은 자연 재생의 off/on/on/off 4개 262→276초 창이다. 켬 준비는 251초부터 수행하여 측정 시작 전 여유를 준다. 각 창에 fuse begin/end를 넣고 실제 Stopwatch 경과 시간·Unity frame 차이로 FPS/ms를 기록한다. pause·캡처·setter 프로파일은 포함하지 않는다. 준비가 늦어 262초 허용 창을 넘으면 유효 측정으로 인정하지 않는다.
- 기록된 A/B/A2의 모드·정지 song time·복원 상태, 세 시점 완비, ABBA 순서·자연 재생·동일 runtime 생존·on 소유 수를 확인한다. 세 시점 모두 ab_out=0, 복원 성공, on 평균 FPS가 off 평균보다 15% 이상일 때만 GATE2=PASS다. 그 밖에는 PIXEL_FAIL/NO_GAIN/ERROR와 이유 코드를 출력한다. 마지막에는 반드시 off다.

## 오프라인 검증

| 검사 | 이번 결과 | 근거 |
|---|---|---|
| 실제 게임 DLL 패치 계획 | 38 checks, 본문 있는 18개/추상 제외 2개 | gate2-B-run2-patch-check.log |
| Release Dev / Player / PlayerAuto | 오류 0/0/0, 경고 38/36/38 | gate2-B-run2-build-dev/player/auto.log |
| LoadCheck Dev / Player / Auto | 274/229/274곳, 틀림 0/0/0 | gate2-B-run2-loadcheck-dev/player/auto.log |
| 기존 LiveDeco 정렬·앵커 정책 | 22,952 checks 통과 | gate2-B-run2-fixture.log |
| 신규 아틀라스·판정 정책 | 1,052,924 checks 통과 | 같은 로그 |
| LiveDeco edition 메타데이터 | 19 checks, 일반 Player 타입/참조 없음 | 같은 로그 |
| 기존 MatShare 정책 | 25,401 checks, 오류 0 | gate2-B-run2-matshare-fixture.log |
| 기존 FrameCompare fixture | RGBA 11 + mask 15 + state 14 + control 17 통과 | gate2-B-run2-framecmp-fixture.log |
| Gate1 / Gate2 steps | 25 / 144 steps 검증, UTF-8 CLI 통과 | gate2-B-run2-gate1-steps-check.log / gate2-B-run2-steps-check.log |
| 기존 진단 edition 검사 | 진단 타입 32/32 분리, 런타임 6/6 유지, 폐기 타입 5/5 양쪽 부재 | gate2-B-run2-edition-check.log |
| 최종 diff/인코딩 | git diff --check 및 UTF-8 문서 검사 통과 | 로컬 검사 |

진단 edition 검사는 처음에 `MaterialPoolFixture`의 Dev 존재를 요구해 실패했다. HEAD 소스 목록·현 csproj·타입 정의에서 재질 풀/크롭 연구 코드가 이미 제거된 것을 확인했다. 검사기의 폐기 타입 5개를 양쪽 빌드 모두 부재여야 하는 조건으로 수정했다. 살아 있는 진단 타입의 Player 제외 요구와 필수 런타임 타입 유지 요구는 유지했다. 실패를 숨기거나 없는 타입을 다시 추가하지 않았다.

LoadCheck의 동적 필드 이름 5곳은 기존처럼 실행 중 확인 대상이다. 빌드의 기존 어셈블리 충돌/폐기 API/미사용 경고는 남아 있다. 순수 fixture·정적 IL·메타데이터 검사는 Unity 정점/픽셀·실제 복원·VRAM·GC·FPS 증명이 아니다.

## 남은 실제 관측

실제 on의 live/owned/coverage, sampler 제외 분포, atlas 페이지 수/VRAM, 실제 런 수, vertexcheck, 전환 비용, stable_alloc_bytes, 세 시점 ab_out/maxdiff/aa2_out, ABBA FPS가 필요하다. ProfApply/ProfSet은 위 전달 표본만 확보했다. 전달된 센서스의 71~78구간을 이 구현의 실제 구간 수로 대체하지 않는다. 게임 실행 금지를 지키기 위해 새 게임 관측은 수행하지 않았고, 자동 루프에 변경된 빌드와 기존 입력을 인계한다.

## 작업 관찰 반환

task-observer v3.5.0을 읽고 기존 사용자 범위 관찰 헤더·활성 원칙을 확인했다. 저장소는 현재 쓰기 허용 범위 밖이므로 수정하지 않고 이 문서로 반환한다. 문제: 게임 메서드 Prefix만 추가하면 이미 바인딩된 setter delegate와 모드의 직접 쓰기 경로가 남을 수 있다. 개선: 논리 필드를 만드는 채널→모드 빠른 길→게임 setter→native 쓰기를 함께 감사하고 모두 같은 소유 검사로 통과시킨다. 원칙: 대체 렌더러의 원본 억제는 표시 플래그뿐 아니라 실제 상태 쓰기 경로 전체를 검증한다. 유형은 프로젝트 내부 구현/검증 관찰이다. 스킬·메모리는 수정하지 않았다.

2회차 관찰도 동일한 report-back 방식으로 반환한다. 문제: LoadCheck는 FieldRefAccess를 검증하고 기존 fixture는 순수 정책/edition을 검증하므로, 런타임에서 이름으로 수집한 추상 Harmony 대상은 둘 다 통과할 수 있다. 개선: 생산 코드의 패치 계획 생성기를 오프라인 검사와 공유하고, 실제 DLL의 본문·추상 여부·구체 override를 패치 전에 검사한다. 원칙: 런타임에 구성하는 훅 목록은 실제 구현의 존재까지 검증해야 한다. 유형은 프로젝트 내부 검증 관찰이며 별도 전역 관찰 파일·스킬·메모리는 수정하지 않았다.
