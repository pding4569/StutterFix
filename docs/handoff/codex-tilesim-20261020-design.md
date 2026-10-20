# TileSim / 타일 렌더 구조 조사 — 2026-10-20

## 판정

`FloorAnim`의 6개 키는 이벤트 테이프 → 타일×키 SoA 슬롯 → 단일/병렬 평가로 옮길 수 있다. 구현은 읽기 전용 관찰·오프라인 재생이며, 실제 게임의 평가나 엔진 쓰기를 대체하지 않는다. 게임 실행 금지 조건 때문에 실제 맵 비트 동일성·시간 절감은 아직 판정하지 않았다.

타일 색은 `FloorAnim.Rec`에 없다. `ColorFloor → TweenColor → DOTween`과 `scrFloor.Update`의 특수 색 경로를 따로 다뤄야 한다. 따라서 이번 `tilesim verify`의 0 mismatch를 색·Transform·렌더 결과의 동일성으로 해석하면 안 된다. 색 계산 대체까지 포함하는 전체 타일 파이프라인은 미완성이다.

## 근거와 재현

게임 설치 파일은 읽기만 했다. `Assembly-CSharp.dll` SHA256:

`F692210F9A342F7EED4BE40AAD463286AB40BF046599959823942EA02F4CA3FF`

`dotnet build tools/ilscan/ILScan.csproj` 후 PowerShell에서 다음처럼 스캔했다.

```powershell
$env:METHOD = 'FloorMesh' # scrFloor, FloorRenderer, FloorMeshRenderer, FloorSpriteRenderer도 각각 실행
$env:FIELDS = '1'
dotnet tools/ilscan/bin/Debug/net8.0/ILScan.dll '<GameManaged>/Assembly-CSharp.dll' ZZZ
Remove-Item Env:METHOD, Env:FIELDS
```

원문 증거:

- [FloorMesh](codex-tilesim-il-floormesh.txt)
- [scrFloor](codex-tilesim-il-floor.txt)
- [FloorRenderer](codex-tilesim-il-floorrenderer.txt)
- [구체 렌더러](codex-tilesim-il-concrete-renderers.txt)
- [형식·필드·메서드 목록](codex-tilesim-il-floor-types.txt)

## 애니메이션 소유권과 계산 의미

| 키 | 실제 시작값 | 평가·적용 |
|---|---|---|
| 0 PX | `Transform.position.x` | `FStart + FChange * ease`, 해당 축 쓰기 |
| 1 PY | `Transform.position.y` | 같은 식, Y 축만 쓰기 |
| 2 ROT | `floor.tweenRot.z` | 같은 식 → tweenRot → eulerAngles |
| 3 SX | `Transform.localScale` | `VStart.x + VChange.x * ease`, X 축만 쓰기 |
| 4 SY | `Transform.localScale` | 같은 식, Y 축만 쓰기 |
| 5 OPA | `floor.opacity` 필드 | float 필드 쓰기; 재질 반영은 게임 LateUpdate |

`New`는 시작값을 읽지 않는다. 첫 `Step` 또는 명시적 `Complete`에서 `Startup`이 읽는다. `New` 직후 호출자가 목표값을 대입하므로 Create 이벤트의 목표값만으로 평가하면 안 된다. Startup 이벤트의 실제 시작·끝·변화값으로 입력을 갱신한다.

`Prev`는 이전 슬롯을 `Kill(true)`와 동일하게 완료한 다음 새 기록을 만든다. 대체는 정상 이벤트이다. 완료 출력은 목표값으로 스냅하지 않고 마지막에도 위 산술식을 쓴다. `Kill(false)`는 끝값을 쓰지 않는다. 재사용된 Rec 객체도 새로운 token을 받아 이전 생명주기와 구분한다. 자연 완료 프레임의 `Running=false`, 진행 위치와 출력도 표본으로 남긴다. `abs(dt)<1e-6`의 엄격한 양쪽 비교, 음수 dt와 상한 clamp도 원본과 같다.

기록 생성/Startup/완료/취소 훅만 추가했다. 캡처 시작에 타일 목록·활성 Rec를 한 번 읽고, 이후 기존 Step에서 인덱스%4==0만 표본화한다. 매 프레임 별도 타일/렌더러 전수 검사나 문자열 속성 API를 추가하지 않는다. SoA 평가는 테이프의 숫자만 사용하고 워커가 Unity 객체를 읽거나 쓰지 않는다.

캡처에는 FloorAnim 외 경로 제외 수를 표시한다: 길이 0의 즉시 쓰기, 진짜 DOTween과 겹친 이동, 원래 효과 폴백. 분할 작업이 남아 있으면 거부하고 캡처 도중 생기면 무효화한다. 자유 이동 하위 타일이 주 타일 목록에 없거나 알려지지 않은 키/동시 슬롯 소유권이 나오면 원인과 함께 무효화한다. 전체 색 기록으로 조용히 간주하지 않는다.

## 렌더러·재질·스타일

1. `FloorMesh.Awake`는 MeshFilter/MeshRenderer를 얻는다. 모양·각도·폭·길이 등의 setter는 `floorMeshesThatNeedUpdate` 집합에 등록한다. `UpdateAllRequired`는 이 집합만 처리하고 비운다. `UpdateMesh`는 문자열 키의 공유 메시 캐시를 조회하고 필요하면 `GetPositions/GenerateMesh`를 수행한 후 MeshFilter.mesh에 넣는다. 이 문자열 API는 기존 게임 코드이며 새 프레임 루프에 복제하지 않았다.
2. `FloorRenderer`는 추상 색/스프라이트 인터페이스다. `FloorMeshRenderer.set_color`는 `material.SetColor(ShaderProperty_Color)`와 cachedColor를 쓴다. `FloorSpriteRenderer.set_color`는 SpriteRenderer.color를 쓴다. 따라서 추상 베이스 setter가 아니라 두 구체 구현을 계측한다.
3. `FloorRenderer.Awake`는 `renderer.material`을 읽어 필드에 보관한다. `SetMaterial`은 renderer.material에 쓴다. 코드상 renderer별 재질 인스턴스가 생기는 경로가 있으므로 타일 전체가 같은 sharedMaterial을 쓴다고 가정할 수 없다. 실제 인스턴스 ID 분포는 게임 관찰 전까지 미확인이다.
4. `scrFloor.SetTrackStyle`은 기본/보석/커스텀 텍스처, perlin·edge 텍스처, 크기·색·정수 속성을 설정하고, 메시 렌더러면 FloorMesh의 length/width/isSprite를 바꾼다. 네온 계열 edge 텍스처도 이 경로다. 색만 평가하는 표로 스타일 변경이나 새 메시 생성은 줄어들지 않는다.
5. 타일당 본체 렌더러 외에 topGlow/bottomGlow, 아이콘, outline, hold/line, 포털·자유 이동 등의 조건부 렌더러가 있다. 고정된 '타일당 N개' 숫자는 IL만으로 정할 수 없다. `SetIconSprite/SetIconOutlineSprite`는 재생 중 재질 텍스처에 통합하는 경로와 별도 SpriteRenderer 경로를 모두 갖는다. 실제 개수는 맵/상태별 census가 필요하다.
6. `SetSortingOrder`는 본체, outline, icon, glow의 정렬을 함께 갱신한다. glow 쪽은 layer와 GameObject layer도 반영한다. 같은 layer/order의 재질 ID·z 규칙은 `docs/optimization-36.md` 끝의 기존 장식 실험 근거를 참고해야 한다. 이번에 타일용 정렬 실험을 실행한 것은 아니다.

## opacity와 색이 반영되는 시점

opacity는 프로퍼티 setter가 아니라 필드이다. `FloorAnim.Apply(OPA)`만 재면 float 저장 비용만 보인다. `scrFloor.LateUpdate`가 이전 opacity/glow 상태를 비교하고 Material.SetFloat, 아이콘·topGlow 등의 색과 관련 표시를 갱신한다. 이 부분을 제외하고 '불투명도 쓰기 비용'이라고 보고하면 누락된다.

`ColorFloor`는 특수 색 타입·두 색·주기·펄스 등을 저장하고 `TweenColor` 또는 직접 SetColor 경로를 호출한다. `TweenColor`는 자체 currentColor 클로저, DOTween.To, OnUpdate, easing, delay, moveTweens 사전 슬롯을 사용한다. `scrFloor.Update`는 시간·Cos·HSV·EasedValue·음량 등도 읽는다. FloorAnim 6개 키의 dt 테이프만으로 이들을 재생할 수 없다. 후속 색 테이프에는 지연/취소/완료/시작값 소유권과 음악·벽시계·오디오 입력의 명세가 별도로 필요하다.

## TileCull / RecolorDefer / MeshWarm과의 관계

TileCull은 본체 MeshRenderer와 topGlow를 공간 격자로 관리하고 바뀐 셀의 enabled만 변경한다. 이동·모양 변경은 pin/uncull 경로로 빠진다. `forceRenderingOff`만 쓰는 것과 enabled를 내리는 것은 엔진 컬링 비용이 다르다. 그룹 메시로 옮기면 개별 Renderer.bounds, enabled, OnBecameVisible/Invisible, 원래 상태를 대신하는 규약이 필요하다.

RecolorDefer는 컬링된 타일의 각도·스타일·색 명령을 미루고 uncull 전에 FlushFloor로 반영한다. `AfterStep`은 예산 내 drain과 FloorMesh.UpdateAllRequired도 실행할 수 있다. 따라서 RecolorDefer와 mesh 비용을 단순히 inclusive 합산하면 이중 계산된다. TileCost는 중첩 자식 시간을 부모에서 빼서 분류한다.

MeshWarm은 열 때 색 이벤트에서 만들어질 메시 키를 예측해 캐시를 채운다. 키에 없는 hexagon/inset 조건이 다르면 건너뛴다. 표 평가 대체와 메시 생성 병목은 다른 문제이며 기존 캐시·warmup 적격 조건을 유지해야 한다.

## 그룹 메시 가능성과 관문

기술적으로 같은 메시 토폴로지·재질·정렬 구간에 속한 타일의 TRS/색을 정점으로 옮기는 것은 가능하다. 그러나 현재 코드가 타일별 material의 텍스처·알파·색·스타일·아이콘 속성을 바꾸며, 추가 glow 렌더러와 Collider도 독립적으로 존재한다. 단순한 메시 결합은 배치 절감을 보장하지 않는다. 메시 캐시 공유는 재질/드로우 공유의 증거가 아니다.

후속 구현의 사전 적격 판정은 dynamic-style/texture, material-instance/order collision, extra-renderer, freeroam, collider/transform reader, visibility-state 등을 이유별로 구분해야 한다. 메시는 렌더 전용으로 합치고 Collider와 논리 Transform 유지 비용은 따로 재야 한다. 공유 키에 끼어드는 비관리 렌더러와 서로 다른 재질 ID를 임의로 하나의 새 재질로 합치면 안 된다. 렌더 경로를 바꾸기 전에 실제 ID·queue·z·order 분포와 픽셀 compare를 확보해야 한다.

예상 이득은 실측 전 숫자로 단정할 수 없다. 표 계산만 교체할 때의 상한은 기존 평가·관리 시간에서 테이프 이벤트+SoA 평가 비용을 뺀 값이다. Transform/native setter, opacity LateUpdate 반영, 스타일/메시, TileCull/RecolorDefer 비용은 그대로 남는다. 그룹 메시까지 대체할 경우에만 일부 엔진 쓰기·렌더러 개체 비용을 추가로 줄일 여지가 있고, 위 적격성·정렬·게임 의미 보존 비용을 뺀 순이득으로 판정해야 한다.
