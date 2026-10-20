# S2b decorender 보완 결과 — 2026-10-20

`DecoRender.cs`를 수정했다. 모든 코드는 기존 파일의 `#if DEV || AUTOTEST` 안에 있다. 게임 실행·설치·커밋은 하지 않았다. 실제 Arche의 선택 수·화면 동일성·복구 동작은 게임에서 확인해야 한다.

## 선택과 관찰

- `decorender on`은 `decorender on 20`과 같다. `on N`은 1~600프레임을 받는다. AutoTest는 관찰·아틀라스 준비가 끝날 때까지 해당 단계에서 기다린다. 카메라가 렌더하지 않으면 관찰도 진행하지 않으며 AutoTest의 기존 단계 제한 시간이 적용된다.
- 정확히 `scrVisualDecoration`, 활성 GameObject, enabled SpriteRenderer, Sprite 존재, rendererEnabled, 양쪽 알파가 0이 아닌 조건을 유지했다. mask/hitbox/mesh, maskInteraction, 부모 SortingGroup, Simple 이외 drawMode는 제외한다. 이전과 같이 `isVisible`에 의존하지 않으므로 화면 밖의 활성 장식도 후보가 될 수 있다.
- 셰이더는 정확히 `Sprites/Default`, 1 pass만 허용한다. 키워드는 `PIXELSNAP_ON`만 허용한다. 이름이 비슷한 사용자 셰이더는 자동 허용하지 않는다. `Sprites/Tile`, repeatX/Y가 1이 아닌 장식, Repeat/Mirror/MirrorOnce wrap, 한 변이 2048을 넘는 텍스처를 제외한다. 기존 split-alpha/외부 알파/지원하지 않는 MPB 거절도 유지한다.
- 관찰 중에는 원래 SpriteRenderer로 계속 그린다. 각 카메라 PreRender에서 최종 월드 행렬·SpriteRenderer.color·flip을 직전 표본과 비교한다. 행렬의 16개 float와 색은 정확 비교한다. 카메라가 여러 개여도 N은 서로 다른 `Time.frameCount`로 센다. 관찰 중 한 번이라도 값이 달라진 후보만 최종 선택한다. 정적 후보는 그대로 둔다.
- 제외 사유는 첫 해당 사유 하나로 집계한다. `tiling-shader`, `shader`, `special-keyword`, `wrap`, `large-texture`, `static`, `mask-hitbox-mesh`, `other`, `material-unsupported`, `atlas-budget`, `changed-during-observation`을 0건이어도 출력한다.

관찰 기간에 정지했다가 나중에 움직이는 장식은 원본으로 남는다. 새 집합을 고르려면 `off → on N`을 실행한다. 후보가 없어도 선택 0인 세션으로 완료되므로 `selected`를 먼저 확인한다.

## 아틀라스 예산

GPU 텍스처를 만들기 전에 텍스처를 면적 내림차순으로 배치하고, sampler 분리·4 texel 패딩·power-of-two 빈 공간·전체 mip의 RGBA32 크기를 계산한다. 원본 텍스처 중복은 한 번만 센다.

고유 원본 면적 합은 4096² texel 이하, 실제 아틀라스 할당 합은 **67,108,864 bytes(64 MiB) 이하**로 제한한다. 초과하면 가장 큰 원본 텍스처를 쓰는 장식들을 선택에서 제외하고 다시 배치한다. 제외 텍스처 ID·크기·장식 수·초과 시점 예상 bytes/원본 texel 수를 로그에 남긴다. mip 원본은 기존처럼 전용 페이지로 보존하며, mipless는 sampler가 같은 것끼리 공유한다. 예산에 맞아도 페이지가 반드시 한 장인 것은 아니다.

이 수치는 대체 아틀라스의 이론 GPU 바이트다. 게임 원본 텍스처, 임시 디코드 RT/CPU 버퍼, 드라이버 오버헤드는 포함하지 않는다. 실행 중 일부 장식을 복구해도 기존 페이지는 off까지 유지한다.

## 장식별 복구와 로그

PreCull과 Build 전에 검증한다. 파괴·sprite·material·재질/MPB 속성·sortingOrder·sortingLayer·mask/hitbox/mesh·렌더러 소유·Unity layer·SortingGroup·drawMode·repeat·wrap 변화는 해당 장식만 관리 목록과 그룹에서 빼고, 살아 있는 원래 SpriteRenderer의 `forceRenderingOff=false`로 복구한다. 이후 다시 끄지 않는다. 나머지 장식의 렌더 대체는 계속한다.

`allDecorations.Count` 변화만으로 전체 중단하던 조건은 제거했다. manager/list 객체 교체, main camera 교체, SceneReset, 활성 장면·재생 전환과 복구할 수 없는 렌더/자원 오류의 전체 복구는 유지했다. 나중에 추가된 장식은 원본으로 남는다.

최초 20건의 구조 변화 로그는 `index`(on 당시 allDecorations 인덱스), 원래 renderer ID, `field`, `before`, `after`를 출력한다. 재질 속성 변경은 이전/이후 실효 재질 서명을 출력한다. 이후에도 카운터는 계속 증가한다. 같은 장식에서 여러 항목이 동시에 바뀌면 항목별로 세고, `restored-items`는 장식당 한 번 센다. destroyed는 객체가 이미 사라져 복원할 renderer가 없는 경우도 포함한다. 정상적인 위치·색·flip 애니메이션은 복구 사유가 아니다.

## state와 compare

기존 Build 시간·그룹/페이지·카메라·마지막 compare·stop 사유에 다음을 추가했다.

| 항목 | 의미 |
| --- | --- |
| observing / observation | 관찰 여부, 진행 프레임/요청 프레임 |
| excluded | 선택 제외 사유별 개수 |
| selected / managed | 관찰·예산 처리 후 최초 선택 수 / 현재 관리 수(관찰 중에는 후보 수) |
| atlas-bytes | 페이지 전체 RGBA32 바이트 / 67,108,864 |
| selection | 최종 선택·제외·고유 원본 수·예산 요약. off 이후에도 보존 |
| changes | sprite, material, material-properties, sortingOrder, sortingLayer, mask, destroyed, renderer-owner 등 항목별 누적 및 restored-items. off 이후에도 보존 |
| detail-limit | 상세 구조 변화 이벤트 출력 상한 20 |

compare는 기존 동기 원본/대체 렌더, PNG 3장, 전체 임계값 >0/>1/>2/>4/>8을 유지한다. 선택된 보이는 장식의 world bounds 8개 꼭짓점을 비교 카메라로 투영한 사각형 합집합(1픽셀 여유) 안/밖에 대해 픽셀 수·최대차·임계값별 차이 수와 비율을 추가했다: `selected-bounds-pixels`, `inside-max`, `outside-max`, `inside>...`, `outside>...`. near plane을 가로지르는 bounds는 보수적으로 전체 화면으로 잡는다.

이 영역은 장식의 실제 불투명 픽셀이나 기여도 마스크가 아니다. bounds 안에는 배경과 다른 장식도 포함된다. 비교 렌더 중 구조가 변하면 snapshotStable=False로 처리하고 종료 시 해당 장식을 복구한다. 임시 카메라의 효과/command buffer/다른 카메라 합성 미복제와 같은 기존 비교 한계 및 같은 sortingOrder 내부 정렬 가설은 그대로다.

## 빌드와 정적 검사

실행 명령:

```powershell
dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1
dotnet build -c Release -p:Edition=Player
py -3 tools/check-ui-encoding.py
git diff --check
```

최종 AutoTest 빌드는 경고 37개 / 오류 0개로 성공했다. `DecoRender.cs` 경고는 없다. 일반 Player 빌드는 증분 빌드로 경고 4개 / 오류 0개로 성공했다. 경고 수는 재컴파일 범위가 달라 직접 비교하지 않는다. 인코딩 검사는 `SFENC001: clean`, `git diff --check`도 통과했다.

로그는 `obj/decorender-2-build-autotest.log`, `obj/decorender-2-build-player.log`다. 실행 대상은 `bin/PlayerAuto/StutterFix.dll`이며 일반 Player에는 기존 조건부 Compile에 따라 실험 코드가 포함되지 않는다. 최종 AutoTest DLL은 3,457,024 bytes다.

## 게임에서 실행할 순서

사용자가 PlayerAuto DLL을 설치한 뒤 실행한다. 경로는 실제 Arche 파일로 치환한다.

```text
keep
open <Arche 경로>
auto on
seek 262 8
waitsongpos 264
decorender on 20
decorender state
decorender compare
decorender compare
decorender compare
decorender compare
decorender compare
decorender state
decorender off
decorender state
```

1. 관찰 완료 로그에서 static/tiling/wrap/large-texture 제외 수, selected, source-textures, atlas-bytes 및 페이지 크기를 확인한다. 선택 수 약 1,100개·원본 10종은 목표 장면의 참고치이며 구현이 보장하는 수가 아니다.
2. 기존 실패 시점에서 active가 유지되는지, `changes`와 최초 상세 로그로 어떤 장식이 바뀌었는지 확인한다. `managed`가 줄어도 나머지 그룹은 계속 렌더해야 한다.
3. metrics의 snapshotStable=True와 전체/영역 안/밖 차이를 기록한다. selected=0인 비교는 움직이는 장식 대체의 검증 표본으로 쓰지 않는다.
4. off 직후 active=False와 실제 화면 복원을 확인한다. 별도로 on → 편집기 복귀/다시 열기/Unload의 자동 복구를 확인한다.

게임에서의 픽셀 동일성, 선택 수와 GPU 실제 할당, 장식별 복구는 이번 빌드 검증으로 확인되지 않았다.
