# S2d decorender 결과 — 2026-10-20

`DecoRender.cs`에 정렬 키 충돌 조사·제외와 `floatcolor` 옵션을 구현했다. 변경 코드는 모두 기존 `#if DEV || AUTOTEST` 안에 있다. 커밋·게임 실행·설치는 하지 않았다. **267.7초 트랙 영역 이상치의 해소 여부는 아직 실측하지 않았다.**

## 정렬 충돌 조사와 제외

20프레임(또는 지정 프레임) 관찰, 움직이는 장식 선별, 아틀라스 예산 적용이 끝난 시점에 `Object.FindObjectsOfType<Renderer>()`를 딱 한 번 호출한다. 대체용 MeshRenderer를 만들기 전이며, 이때 남은 실제 후보의 Renderer instanceID로 관리/비관리를 구분한다. 정적 장식, 관찰 중 탈락 장식, 예산 탈락 장식도 비관리로 취급하므로 후보와 같은 키를 쓰면 후보가 제외된다.

각 `(sortingLayerID, sortingOrder)`에 관리 수, 비관리 수, 비관리 종류별 수, 예정 그룹 수를 집계한다. 비관리 종류는 부모를 포함한 컴포넌트 이름/타입으로 `planet`, `other-deco`, `tile`, `other`를 분류하고 실제 Renderer 타입도 붙인다. `scrFloor`/Tile 이름은 tile, Planet 이름은 planet, `scrDecoration`은 other-deco 분류에 사용한다. **이 분류는 로그용 휴리스틱이며 제외 여부에 영향을 주지 않는다.** 모든 종류의 Renderer를 검사한다. 발견된 비활성화 Renderer(`enabled=false`, `forceRenderingOff=true`)도 보수적으로 충돌에 포함한다. API 기본 동작상 inactive GameObject와 HideAndDontSave 오브젝트는 이 스캔 범위 밖이다.

- `shared-order`: 비관리 Renderer가 같은 키를 하나라도 쓰면 그 키의 관리 후보 전부를 제외한다. 원본 SpriteRenderer를 숨기지 않는다.
- `split-order`: 비관리 충돌이 없는 키라도 예정 그룹이 둘 이상이면 키 전체를 제외한다. 그룹 판정은 실제 생성과 같은 함수로 수행하며 아틀라스 페이지, Unity layer, 재질/MPB 실효 속성 서명을 모두 포함한다.
- 두 조건이 겹치면 제외 수는 `shared-order`에만 넣는다. 별도의 `split-detected-*`에는 중복된 split 검출도 포함한다.

제외 뒤 쓰이지 않는 원본 텍스처와 빈 아틀라스 페이지는 GPU 할당에서 뺀다. 남은 페이지를 다시 패킹하지 않으므로 그룹 판정 당시의 페이지 분리를 유지한다. 제외 카운터는 관찰/예산 단계의 최초 제외 이유와 중복 합산하지 않는다.

이 조사는 선택 완료 시점의 한 번의 스냅샷이다. 실행 중 비관리 Renderer 생성/키 변경이나 관리 장식의 개별 복구로 새로 생기는 외부 충돌을 지속 재조사하지 않는다. 기존 개별 복구는 그대로 유지했다. 그런 상태 변경 뒤의 재선택은 `off` 후 `on`으로 한다. 한 그룹 안에서의 거리/instanceID 정렬 휴리스틱도 기존과 같으며 Unity 내부 동률 순서의 일반적 동일성을 보장하지 않는다.

## 새 state / 로그

`state`의 기존 `excluded={...}`에 `shared-order`, `split-order`를 추가했다. 0건이어도 표시한다. `vertex-color`와 `order-audit={...}`는 `off` 뒤에도 마지막 결과를 유지한다.

| 항목 | 의미 |
| --- | --- |
| `scan-renderers`, `scan-keys`, `unmanaged` | 스냅샷의 전체 Renderer 수, 정렬 키 수, 비관리 Renderer 수 |
| `shared-order-keys`, `shared-order-items` | 외부와 충돌한 키 수와 그 이유로 제외한 장식 수 |
| `split-order-keys`, `split-order-items` | shared와 겹치지 않은 split 키 수와 제외 장식 수 |
| `split-detected-keys`, `split-detected-items` | shared와 중복된 경우까지 포함한 split 검출 수 |
| `details` | 처음 최대 20개 충돌 키의 관리/비관리 수, 예정 그룹 수, 비관리 종류별 수, 키당 최대 4개 Renderer 이름·instanceID·종류·enabled/forceOff 예시 |
| `vertex-color` | 기본 `UNorm8x4(Color32)` 또는 `Float32x4(explicit-buffer)` |
| `tile-vertex-input` | `Float32x4(saved-D3D11-asm)` — 저장된 D3D11 자료의 확인 범위 |

선택 완료 때 `[decorender] order-collision ...`과 `order-audit ...`도 기록한다. 외부 충돌 없이 split만 있는 키는 비관리 예시가 비어 있고 `groups>1`로 식별한다. 최종 `selected`/`managed`/`groups`는 정렬 제외 후 값이다. 기존 `changes`·관찰·compare 전체/선택 bounds 안/밖 통계는 유지했다.

## 정점 색과 확인된 전제 차이

`decorender on 20 floatcolor`는 position(Float32×3), color(Float32×4), UV(Float32×2)의 36바이트 정점을 선언하고 `SetVertexBufferData`로 업로드한다. `Color`를 Color32로 변환하지 않는다. 최초 토폴로지와 프레임 갱신, compare 모두 같은 업로드 함수를 쓴다. 비어 있는 그룹은 메시를 비우고 렌더러를 끈다. 비어 있지 않은 메시의 실제 색 attribute 형식/차원과 float 모드 stride/구조체 크기를 검사하며 불일치하면 기존 실패 정리 경로로 원본을 복구한다.

**인계 문서의 “기존 SetColors가 8비트”라는 전제는 실제 코드와 달랐다.** 기존 `g.Colors`는 `List<Color>`였으며, 현재 게임의 `UnityEngine.CoreModule.dll`을 ILSpy로 읽으면 `SetColors(List<Color>) → SetListForChannel → VertexAttributeFormat.Float32`다. Color32 오버로드가 UNorm8이다. 확인 출력은 로컬 `obj/decorender-4-mesh-api.txt`에 저장했다.

요청한 기본 8비트/옵션 float 비교가 명확하도록 이번 기본 경로는 **명시적인 `List<Color32>` + `SetColors`로 변경**했다. 따라서 기본 모드는 이전 S2c의 실제 Float32 경로와 색 양자화가 달라진다. 과거 최대 1~2/255 차이를 기존 정점 버퍼의 8비트 반올림 때문이라고 결론 내릴 수 없으며, `floatcolor`가 과거 표본보다 개선된다는 보장도 없다. 색 정밀도 비교는 이번 두 모드 사이에서 새로 해야 한다.

저장소의 `docs/handoff/codex-gpufree-20261019-evidence-shaders.txt`에서 `Sprites_Tile-0..3.asm`의 정점 입력/출력 COLOR가 float4이고, `mul o1.xyzw, v1.xyzw, cb0[2].xyzw` 연산을 확인했다. `Sprites_Tile-4..7.asm`의 픽셀 입력도 float4이며 텍스처×색과 RGB premultiply 경로가 보인다. **보관된 D3D11 프로그램에서는 Float32 색 입력을 받을 수 있어 셰이더 교체가 필요 없다.** 이번에 게임 셰이더를 다시 추출하거나 실행하지 않았으며 다른 그래픽 API까지 확인한 것은 아니다. 상수 레지스터의 `_Color`/`_RendererColor` 이름 대응 및 원본 SpriteRenderer와의 최종 색 동일성은 여전히 미확인이므로 기존 실효값 복사 정책과 compare 검증을 유지한다.

## 검증 결과

| 확인 | 결과 |
| --- | --- |
| `dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1` | 성공, 경고 38개 / 오류 0개 |
| `dotnet build -c Release -p:Edition=Player` | 성공, 경고 4개 / 오류 0개 |
| 실제 소스의 정렬 제외 메서드를 추출한 오프라인 검사 | 12개 시나리오 통과 |
| `py -3 tools/check-ui-encoding.py` | `SFENC001: clean` |
| `git diff --check` | 통과 |

AutoTest의 새 경고 1개는 요청한 `Object.FindObjectsOfType<Renderer>()`의 Unity 6 deprecated 경고(CS0618)다. 나머지는 기존 경고이며 빌드 범위가 달라 경고 수를 성능/품질 지표로 비교하지 않는다. 로그는 `obj/decorender-4-build-autotest.log`, `obj/decorender-4-build-player.log`다. AutoTest DLL은 `bin/PlayerAuto/StutterFix.dll`(3,463,680 bytes)이다. 일반 Player의 기존 조건부 Compile은 그대로다.

오프라인 검사는 소스의 실제 `ExcludeUnsafeOrders`, 그룹 키, 분류, 집계 메서드를 추출해 가짜 Renderer/씬 API로 실행했다. 같은 키/같은 그룹 유지, 타일 외부 충돌, 아틀라스 분리, 재질 분리, Unity layer 분리, 다른 sorting layer/order 독립성, shared/split 중복 집계, disabled 외부 장식, 다른 키 외부 행성, 빈 선택, 상세 20개 상한을 확인했다. 생성기·하네스·로그는 로컬 `obj/decorender-4-order-check.py`, `obj/decorender-4-order-check/`, `obj/decorender-4-order-check.log`다. Unity 메시/GPU 실행이나 픽셀 동일성 시험은 아니다.

## 게임에서 돌릴 순서

사용자가 AutoTest DLL을 설치한 뒤 실제 Arche 경로로 실행한다. 두 모드를 같은 곡 구간에서 비교하도록 중간에 다시 seek한다.

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
seek 262 8
waitsongpos 264
decorender on 20 floatcolor
decorender compare
decorender compare
decorender compare
decorender compare
decorender compare
decorender state
decorender off
decorender state
```

1. 각 선택 완료 로그에서 shared/split 키·제외 수·실제 비관리 이름/종류와 최종 selected/groups/atlas-bytes를 확인한다. `selected=0`은 대체 화면 검증 표본으로 세지 않는다.
2. compare는 기존처럼 같은 상태의 원본/그룹을 동기 촬영한다. 위 5회만으로 267.7초를 반드시 포착하는 것은 아니므로, 원래 이상치 구간인 **곡 267.7초 전후 및 264~273초**를 실제 compare의 song 값으로 확인하며 추가 촬영한다. 별도 seek로 252초 구간도 두 모드에서 재확인한다.
3. `snapshotStable=True`, 전체/선택 bounds 안/밖 최대 차이, >0/>1/>2/>4/>8 픽셀 수와 diff16 트랙 줄무늬를 확인한다. 두 모드의 선택 수와 정렬 충돌 현황도 함께 기록한다.
4. `state`의 managed/selected, changes/restored-items를 확인하고, `off` 뒤 active=False 및 원본 화면 복원을 확인한다. 새 비관리 충돌이 생길 수 있는 상태 변경 후에는 다시 선택한다.

task-observer v3.5.0의 기존 공유 로그와 활성 원칙을 읽었다. 공유 로그는 허용된 쓰기 범위 밖이므로 수정하지 않았으며, 이번 작업에서 새 스킬 생성/수정은 하지 않았다.
