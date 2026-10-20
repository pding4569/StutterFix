# S2c decorender 결과 — 2026-10-20

`DecoRender.cs`에 `Sprites/Tile`의 실측 속성 조건을 반영했다. 코드는 전부 기존 `#if DEV || AUTOTEST` 안에 있다. 커밋·게임 실행·설치는 하지 않았다.

## 허용 규칙과 제외 집계

기존의 정확한 `Sprites/Default` 허용(1 pass, 키워드는 `PIXELSNAP_ON`만)을 유지하고, 정확한 `Sprites/Tile`도 다음 조건에서 후보로 받는다.

- 1 pass, 키워드 없음.
- 장식 필드 repeatX/Y가 각각 1이며, 재질의 `RepeatX`/`RepeatY` 실효값도 각각 1. `PixelSnap` 실효값은 0. 세 속성이 없는 Tile 재질은 거절한다. 실효값은 해당 MaterialPropertyBlock(MPB) float가 있으면 그것을, 없으면 원본 재질 값을 사용한다.
- `_MainTex`의 재질 scale/offset이 정확히 (1,1)/(0,0). MPB `_MainTex_ST`가 있으면 정확히 (1,1,0,0). 관찰 이후 검증에서도 정확 비교한다.
- 스프라이트 원본 텍스처의 U/V wrap이 모두 Clamp.

기존 활성 상태·알파·mask/hitbox/mesh·SortingGroup·drawMode·크기·split alpha·지원하지 않는 MPB 등의 제한은 유지했다. 이름이 비슷한 다른 셰이더는 추가로 허용하지 않는다.

`tiling-shader` 카운터는 제거하고 다음 항목으로 분리했다. 모든 카운터는 0건이어도 state/선택 로그에 나온다. 제외 집계는 장식당 최초 해당 이유 하나이므로 중복 합산하지 않는다.

| 항목 | 조건 |
| --- | --- |
| point-sampling | `USE_POINT_SAMPLING` 키워드. 다른 키워드도 함께 있으면 이 항목을 우선 기록 |
| special-keyword | Tile의 나머지 모든 키워드, Default의 기존 비허용 키워드 |
| repeat-not-one | 장식 repeatX/Y 또는 Tile 재질/MPB RepeatX/Y가 1이 아님 |
| pixel-snap | Tile의 재질/MPB PixelSnap이 0이 아님 |
| maintex-st | 재질 또는 MPB의 `_MainTex` ST가 항등값이 아님 |
| wrap | 원본 텍스처 U/V 중 하나라도 Clamp가 아님 |

`shader`, `large-texture`, `static`, `mask-hitbox-mesh`, `other`, `material-unsupported`, `atlas-budget`, `changed-during-observation`도 유지한다. 점 샘플링은 텍스처 크기에 의존할 가능성이 있어 아틀라스 대체에서 제외한다.

## 그룹 재질과 색상

원래 장식 재질을 `new Material(item.Source)`로 복제하고 셰이더·키워드·렌더 큐를 유지한다. 기존 `MaterialState`가 셰이더에 선언된 재질/MPB 실효값을 서명에 넣고 복제 재질에 복사하므로, `_Color`가 다르면 그룹도 갈라진다. `_MainTex`를 아틀라스로 교체하고 Tile은 `RepeatX=1`, `RepeatY=1`, `PixelSnap=0`을 명시한다. 정점색은 매 프레임 읽은 `SpriteRenderer.color`다.

저장된 게임 IL `docs/handoff/decoengine-evidence/scrVisualDecoration-il.txt`의 `ApplyColor`는 장식 color/opacity와 바닥 opacity를 반영한 rendererColor를 `SpriteRenderer.color`에 쓰는 경로를 보여 준다. `SetTiling`에는 RepeatX/RepeatY 재질 속성 기록 경로가 있다. 게임은 실행하지 않았다.

저장소의 effects/tools 셰이더 자료에서는 `Sprites/Tile` 구현을 찾지 못했다. 인계의 런타임 속성 목록에는 `_Color`가 있지만 `_RendererColor`는 없다. 속성 목록만으로 셰이더 내부의 **정점색 × `_Color` × `_RendererColor` 계산식은 미확인**이다. 기존 정책대로 instancing을 끄고 `_RendererColor`가 재질 속성으로 선언된 경우 white로 설정하며, 커스텀 MPB `_RendererColor` 제한을 유지한다. `_Color` 실효값과 정점색을 추가로 곱해 중복 적용하지 않는다.

그룹 생성 로그에 `_Color` 실효값·정점색 출처·RendererColor 정책과 미확인 상태를 추가했다. `state`에도 active/off에 관계없이 `tile-color=미확인(...;compare-required)`를 출력한다. 실제 색상 동일성은 아래 compare로 판단해야 한다.

## 유지한 동작

기본 20프레임 관찰, 관찰 중 위치/색/flip이 변한 장식만 선택, 변화한 장식 개별 복구, 아틀라스 예산과 샘플러 분리, 정렬 정책, compare의 전체/선택 bounds 안/밖 통계는 유지했다. 셰이더 키워드·RepeatX/Y·PixelSnap 등 선언 속성이 선택 이후 변하면 기존 재질 서명 검증으로 해당 장식을 복구한다. MPB ST가 비항등값으로 변해도 복구한다.

## 검증

| 명령 | 결과 |
| --- | --- |
| `dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1` | 성공, 경고 37개 / 오류 0개. DecoRender.cs 경고 없음 |
| `dotnet build -c Release -p:Edition=Player` | 성공, 경고 4개 / 오류 0개 |
| `py -3 tools/check-ui-encoding.py` | `SFENC001: clean` |
| `git diff --check` | 통과 |

빌드 로그는 `obj/decorender-3-build-autotest.log`, `obj/decorender-3-build-player.log`에 있다. AutoTest 결과물은 `bin/PlayerAuto/StutterFix.dll`(3,458,560 bytes)이다. 일반 Player는 기존 조건부 Compile에 따라 DecoRender를 포함하지 않는다. 경고 수는 재컴파일 범위가 달라 직접 비교하지 않는다.

## 게임에서 돌릴 순서

사용자가 AutoTest DLL을 설치한 뒤 실제 Arche 경로로 실행한다.

```text
keep
open <Arche 경로>
auto on
seek 262 8
waitsongpos 264
decorender on 20
decorender compare
decorender compare
decorender compare
decorender compare
decorender compare
decorender state
decorender off
decorender state
```

1. 관찰 완료 로그의 selected/source-textures/groups/atlas-bytes와 제외 항목을 확인한다. `point-sampling`, `wrap`, `repeat-not-one`, `pixel-snap`, `maintex-st`로 탈락 원인을 구분한다. selected=0 표본은 대체 화면 검증으로 쓰지 않는다.
2. 각 compare의 snapshotStable=True 여부와 전체/영역 안/밖 최대차 및 >0/>1/>2/>4/>8 픽셀 수를 기록한다. PNG 원본/대체/증폭 차이와 그룹의 `_Color` 로그를 함께 확인한다. Tile 색상식은 아직 미확인이다.
3. state에서 managed/selected, changes/restored-items와 마지막 compare를 확인한다. off 후 active=False 및 화면 복원을 확인한다.

실제 선택 수·픽셀 동일성·실행 중 개별 복구는 이번 오프라인 빌드로 확인되지 않았다.

작업 관찰: task-observer v3.5.0의 기존 공유 로그를 읽었으며 이번 코드 변경에서 재사용할 새 스킬 관찰은 없었다. 공유 로그는 이 작업 폴더의 쓰기 허용 범위 밖이므로 수정하지 않았다.
