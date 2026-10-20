# Codex 임무 matshare: 장식 재질 복제를 없애고(Run 단위 공유) draw call 을 줄이는 길 — 조사 + 센서스 + 프로토타입

2026-10-21. 작업 폴더 `E:\StutterFixWork\wt-archegpu` (브랜치 `codex/night-archegpu`). 게임 실행·설치·커밋 금지(Claude 가 한다). 결과 `docs/handoff/codex-matshare-20261021-result.md` + `-o`(10줄).

## 새로 확인된 사실 (내가 IL 로 확인)
- **draw call 1,840 의 원인 후보**: `UnityEngine.Renderer::get_material`(호출하면 재질을 **복제**) 의 호출처가 `scrVisualDecoration::SetSprite`(2곳)·`SetTile`·`SetBlendMode`·`UpdateShader`, `scrDecorationManager::UpdateDecorationTilingLateUpdate`, `FloorRenderer::Awake/SetFlash` 등이다(`ILScan <Assembly-CSharp.dll> get_material` 에서 `UnityEngine.Renderer::get_material` 로 필터). 즉 장식마다 재질 인스턴스가 달라져(`sharedMaterial.GetInstanceID()` 가 장식마다 다름) 배칭이 안 된다. 이 호출들을 `sharedMaterial` 로 바꾸고 재질을 **공유**하면 인접(정렬 순서상) + 같은 텍스처인 스프라이트가 한 draw 로 합쳐진다.
- **정렬 규칙**(우리 실측, `SortProbe`): 같은 `(sortingLayer, sortingOrder)`·같은 큐·같은 거리에서는 **재질 인스턴스 ID 오름차순(부호 있는 값)이 뒤→앞**. 새 재질의 ID 는 만들수록 더 작아진다(−1369400, −1369440, … 감소). 따라서 장식 재질을 "만든 순서의 역순"이 그리는 순서(먼저 만든 것이 앞)다. `rendererPriority` 는 정렬 키로 동작하지 않는 것으로 보인다(공유 재질 사례가 순서가 불안정, 재시험 중).
- **구간(Run) 단위 공유 아이디어**(제미나이): 원래 그리기 순서에서 **인접한 장식끼리 재질 상태(셰이더+키워드+블렌드+포인트샘플링+타일링+모든 재질 속성)와 텍스처가 같으면 한 Run**, 상태가 바뀌는 경계마다 새 Run. 각 Run 에 전용 공유 재질 1개를 **원래 순서가 보존되도록 만든 순서로** 준다. 전역 공유(18개)는 `A→B→A` 가 `A→A→B` 로 뒤집혀 실패했지만 Run 단위면 인접하지 않은 같은 상태끼리는 재질이 달라 안 뒤집힌다. 이전 분석의 최소 연속 구간 715~744개가 예상 draw 수의 상한 근처다.
- 장식의 정렬 키 (sortingLayer, sortingOrder, z/거리)는 곡 중 이벤트(깊이 변경 22개 등)로 바뀔 수 있고, 장식이 켜지고 꺼지면 "인접" 관계가 바뀐다 → Run 은 **숨겨진 장식까지 포함한 전체 순서** 기준으로 보수적으로 나눠야 한다.
- 다른 코드(게임의 `UpdateDecorationTilingLateUpdate`, PACL2 등 다른 모드)가 장식의 `renderer.material` 로 **장식별 속성**(`_RepeatX/_RepeatY`, 포인트샘플링, 색 등)을 쓰면 공유 재질이 오염된다 → Run 상태 키에 모든 장식별 재질 속성이 들어가야 하고, 곡 중 속성이 바뀌는 장식은 개별 재질로 남긴다.

## 해야 할 것
1. **센서스 `matshare census`**(개발 전용): 지금 장면의 모든 이미지 장식 렌더러에 대해 (a) 재질 인스턴스 ID 개수(= 현재 고유 재질 수), (b) 재질 **내용 서명**(셰이더, 켜진 키워드, `_SrcBlend/_DstBlend/_BlendOp/_ZTest/_ZWrite` 등, 모든 float/color/vector/texture 속성값, renderQueue)의 고유 개수와 상위 분포, (c) 정렬 키 `(sortingLayerID, sortingOrder, 큐, 거리)`로 줄 세운 뒤 **(재질 서명 + 스프라이트 텍스처) 기준 연속 구간(Run) 수** — 전체 렌더러(숨김 포함) 기준과 현재 live(활성·강제숨김 아님) 기준 둘 다 — 를 로그. **이 Run 수가 Run 단위 공유 시 예상 draw call 수**다. 장식별로 달라지는 속성이 무엇인지(서명을 쪼개는 속성 상위 5개)도.
2. **호출처 정밀 분석**: `scrVisualDecoration.SetSprite/SetTile/SetBlendMode/UpdateShader` 와 `scrDecorationManager.UpdateDecorationTilingLateUpdate` 가 재질에 정확히 무엇을 쓰는지(IL), 그 쓰기가 장식별 값인지 상수인지, 곡 중 다시 불리는 조건(이미지 변경 이벤트, 타일링, 블렌드 모드 이벤트, 매 프레임 `UpdateDecorationTilingLateUpdate` 인지)을 표로. 매 프레임 `get_material` 을 부르는 경로가 있으면 그 자체가 CPU 낭비(재질 복제 후 즉시 무변화)인지 확인.
3. **프로토타입 `matshare on|off|state`**(개발 전용, 기본 꺼짐, Player 제외, `Main.Unload`/`off` 에서 원래 재질로 복원): 장식 생성이 끝난 뒤(맵 열기 완료, 재생 전 정지 상태) **현재 재질 상태를 읽어** Run 을 계산해 각 장식의 `sharedMaterial` 을 Run 재질로 교체한다. 새 Harmony 패치 없이 시작(가능하면 후처리 방식). 곡 중 장식 상태가 바뀌는 장식(이미지 변경, 블렌드 변경, 타일링 변경, 깊이 변경 이벤트 대상)은 개별 재질 유지. Run 재질은 **그리는 순서가 보존되도록**(위 정렬 규칙: 부호 있는 ID 오름차순이 뒤→앞) 올바른 순서로 생성한다 — 생성 순서가 ID 순서로 이어진다는 가정은 `SortProbe`(`E:\StutterFixWork\wt-decorender2\SortProbe.cs`)로 검증하는 fixture 를 만든다. 복원 시 원래 재질 인스턴스가 필요하면 off 때 새 복제로.
4. **화면 동일 검증**: 본 카메라 `clear=Nothing` 때문에 기존 `pixeldiff` 가 캡처 실패했다("Nothing clear mode requires depth preservation"). 대신 정렬 보존을 **직접 관측**하는 방법을 설계해 구현: (a) 같은 프레임에서 `matshare` 켬/끔 각각의 **실제 렌더러 그리기 순서**를 알 수 있는 방법(예: 선택한 소수 장식 쌍을 sortprobe 방식의 별도 카메라/RT 로 재렌더해 승자 판독), 또는 (b) 게임 카메라 체인(BGStatic→BGMoving→본 카메라→Overlay)을 임시 `targetTexture` 로 순서대로 한 번 더 렌더해 비교하는 방법. 가능한 방법 하나를 골라 구현하고 한계를 문서에. 실패하면 이유를 구체적으로(어떤 카메라 설정 때문에 안 되는지).
5. 측정 steps(내가 돌릴 것): 같은 구간(Arche 262~266초) 반복, 앞뒤 기준 + `matshare on` 창, GPU 시간·draw·SetPass(전체 프레임) 비교, 그리고 4번 검증 결과. `tools/optimization/matshare-steps.py` 생성기.
6. 빌드 3종(`-c Release`; `-p:Edition=Player`; `-p:Edition=Player -p:AutoTestBuild=1`) 오류 0 + LoadCheck + fixture. Player 제외, `Module.ResolveSignature` 금지, 새 Harmony 패치는 이름+인자 수+호출 규약으로 정확히 하나.

## 규칙
- 헤르츠 맵 이름·비공개 프로젝트 언급 금지, 100만 타일 맵 열기 금지. 다른 모드 코드 복사 금지.
- 화면이 원본과 같다는 증명이 되기 전에는 기본 켬/Player 이식 금지. 기대 이득은 draw 1,840 → Run 수(예상 수백)이고 GPU 약 1~1.5ms 절감 + 엔진 CPU(컬링/정렬/배칭) 일부 — 센서스 수치로 먼저 확인하고 낙관하지 말 것.
