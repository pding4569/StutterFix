# S3 sortprobe 구현 결과 (2026-10-20)

인계 문서의 **커밋·게임 실행·설치 금지**를 준수했다. `SortProbe.cs`, AutoTest 연결, 조건부 Compile을 구현했다. 게임에서 실행하지 않았으므로 실측 일치율과 Unity의 확정 타이브레이크 규칙은 아직 없다. 아래 `미측정`은 0%가 아니다.

## 구현 및 실행량

`sortprobe all [반복=50, 50..200] [seed=20261020]`은 아래 각 케이스를 **2개/3개/6개 각각 50회**, 기본 총 **2,550 trials** 실행한다. trial마다 생성 순서와 대상 변경 순서를 seed로 무작위화한다. `creation`/`creation-reverse`는 논리 라벨의 생성 순서를 고정하여 두 방향을 비교한다. `mixed-random`은 거리·형제 순서·재질·렌더러 종류를 함께 무작위화하는 별도 복합 가설 검증이다.

|케이스|변경 변수|기본 반복|실측 일치율|
|---|---|---:|---|
|creation / creation-reverse|라벨별 생성 방향|각 150|미측정|
|siblings|같은 부모 아래 형제 순서|150|미측정|
|parents|부모 분리, 월드 위치 유지|150|미측정|
|z-small / z-large|월드 z, 쌍에서 ±0.001 / ±0.1|각 150|미측정|
|materials|동일 shader/texture, 별도 재질 인스턴스|150|미측정|
|queue|별도 재질의 renderQueue 3000+k|150|미측정|
|textures|동일 sharedMaterial, 별도 흰 텍스처를 가진 sprite|150|미측정|
|sprites|동일 texture, 별도 sprite|150|미측정|
|enable|순서를 무작위화하여 disable/render/enable|150|미측정|
|force-off|forceRenderingOff true/render/false|150|미측정|
|move|화면 밖 이동/render/원위치|150|미측정|
|reparent|다른 부모로 SetParent(worldPositionStays=true)|150|미측정|
|mesh-first / mesh-last|MeshRenderer 하나를 처음/마지막 생성|각 150|미측정|
|mixed-random|6개 포함 무작위 복합 속성|150|미측정|

가설 10개를 뒤→앞 **전체 순서가 정확히 일치한 trial 수 / 유효 trial 수**로 비교한다: instanceID 오름/내림, 생성 순서 오름/내림, siblingIndex 오름/내림, `(거리 내림, ID 오름)`, `(거리 내림, siblingIndex 오름, ID 오름)`, `(queue 오름, 거리 내림, ID 오름)`, `(material ID 오름, renderer ID 오름)`. sibling 동률은 ID로 해소한다. 정렬 거리 계산은 카메라 및 GraphicsSettings의 transparencySortMode/axis를 반영한다.

## 관측과 정리

- 편집기이며 `playMode=false`이고 활성 정사영 카메라가 있어야 한다. trial마다 조건을 다시 확인하며 재생이 시작되면 중단한다.
- 장면의 Renderer가 사용하지 않는 Unity layer를 선택한다. 장면 카메라/렌더러/재질은 수정하지 않는다. 독립적인 비활성 Camera를 만들어 원본의 회전·위치·투명 정렬 모드/축을 적용한다. 정사영 크기 2, near/far 0.1/100, 16×16 ARGB32/depth24, MSAA 없음이다. 게임 카메라의 컴포넌트/후처리는 복제하지 않는다.
- `Sprites/Tile` shader를 사용한다. 없거나 지원하지 않으면 실패하며 다른 shader로 대체하지 않는다. 흰 텍스처, alpha=1, 쌍은 검정/흰색, 다중 묶음은 구분 가능한 회색 3/6단계다. MeshRenderer에는 같은 색을 vertex color로 넣는다.
- 초기 전체 묶음의 픽셀을 먼저 저장한다. 각 renderer를 단독으로 그려 실제 shader/color-space 출력을 교정하고, 중앙 3×3 패치로 위쪽 renderer를 식별한다. 색이 구별되지 않으면 invalid다.
- 쌍별 승패로 전체 순서를 복원하고, 승수 0..n-1의 전순서 여부, 초기 전체 묶음의 맨 위, 맨 위부터 제거하는 peeling 결과를 교차 검사한다. 동적 변경 직전/직후의 맨 위도 별도 기록한다. 순서 측정을 3회 추가 반복하여 안정성을 센다.
- 표에는 total/valid, 안정률, 변경률, 정적 케이스의 무변경 불일치율, 가설별 분자·분모·일치율이 들어간다. invalid 표본은 가설 분모에서 제외하므로 **valid가 50보다 적으면 해당 케이스를 50회 유효 검증한 것이 아니다**. 모순을 숨기거나 결정적 규칙으로 승격하지 않는다.
- 각 trial이 AutoTest의 한 번의 poll 안에서 끝난다. `finally`에서 이전 RenderTexture.active/GL.sRGBWrite를 복구하고 직접 만든 GameObject·Mesh·Material·Sprite·Texture·RT를 즉시 정리한다. trial 사이에는 Unity 객체가 남지 않는다. `sortprobe stop`은 실행 카운터를 끝내고 부분 보고서를 만든다. `state`는 진행 상태를 로그에 적는다.

출력은 `<모드 폴더>/sortprobe/<UTC시각-고유값>/`에 둔다.

- `environment.txt`: Unity/GPU/API/MVID/seed/카메라/전역 정렬 조건.
- `trials.csv`: 생성 순서, renderer/material/sprite/texture instanceID, 종류, 부모, sibling, 월드 z, 정렬 거리, queue, 처음/변경 직전/직후 맨 위, 변경 전후 전체 순서, 유효성·안정성.
- `report.md`: 실험별 일치율 표, 100% 후보 목록 및 동률/불안정 표시, instanceID 오름차순 가설의 관측 일치 수, 해석 한계. 예외·취소 때에도 완료된 trial의 부분 보고서를 쓴다.

## 게임에서 실행할 명령

이 문서는 실행 절차만 제공한다. autotest.txt 생성·설치·실행은 이번 작업에서 하지 않았다. `<HELLO>`는 실제 `.adofai` 파일 경로로 교체한다.

```text
keep
timeout 1800
open <HELLO>
stop
wait 2
sortprobe all 50 20261020
log sortprobe-first-complete
```

`keep`은 실패/시간 초과 시 게임 종료를 막는다. 기본 AutoTest 제한 180초보다 넉넉하게 1800초를 지정했다. GPU readback을 많이 하므로 성능 측정과 동시에 실행하지 않는다. 동일 seed 재실행, 다른 seed(예: `sortprobe all 50 20261021`), 새 게임 프로세스에서 동일 seed 재실행 결과를 비교해야 실행 간 변동을 평가할 수 있다. 이 명령은 실패해도 AutoTest 단계 자체를 종료하고 `[sortprobe] FAILED/REFUSED/aborted`를 로그에 남길 수 있으므로 완료 마커만으로 성공을 판정하지 말고 report의 `Status: complete`, 2550 trials, 각 행의 valid/stable을 확인한다.

## 현재 결론과 제한

**규칙: 미확정.** `decorender`의 instanceID 오름차순 가설은 아직 지지/기각할 실측이 없다. 생성 순서와 ID가 강하게 연동되어 여러 가설이 모두 맞으면, 가장 간단한 유일 규칙으로 단정할 수 없다. 런타임 보고서도 이 경우 후보를 나란히 표시한다.

그룹 MeshRenderer를 어느 ID/생성 위치에 놓아야 하는지도 미확정이다. 실측으로 얻은 순서에서 관리 장식이 외부 SpriteRenderer 사이에 여러 구간으로 나뉘어 있으면, **한 MeshRenderer의 정렬 위치 하나로는 외부 renderer를 메시 내부 삼각형 사이에 끼울 수 없다**. 이때 구간별 분할이나 별도 방식의 검토가 필요하지만 이번 작업은 DecoRender 알고리즘을 변경하지 않는다.

이 도구는 **동일 trial 내 명시적인 Camera.Render 호출 사이의 변화**를 관측한다. 별도 프레임의 LateUpdate/렌더 루프를 거친 결과와 같다는 보장은 없다. 전체 순서를 복원하는 과정도 renderer.enabled를 바꾸므로, 초기 삽입 상태의 맨 위는 검증해도 그 아래 순서 전체가 최초 네이티브 삽입 순서와 같다고 보장할 수 없다. 이 한계 때문에 생성 시 고정/매 프레임 재계산을 확정하는 도구로 쓰면 안 된다. 실제 장식의 기존 재질 인스턴스·sprite atlas·SortingGroup·카메라 스택·기존 batching 환경에서의 동일성 역시 별도 검증이 필요하다. 이번 변경은 재현 가능한 합성 실험 장치이며 실제 게임 측정은 남아 있다.

## 검증

- DEV / Player / PlayerAuto 빌드: 오류 0개. 경고는 각각 38/36/38개(기존 참조 버전 충돌, obsolete API, 미사용 필드 등).
- MSBuild Compile 항목 확인: DEV=1, Player=0, PlayerAuto=1 (`SortProbe.cs`). 소스도 `#if DEV || AUTOTEST`로 감쌌다.
- `git diff --check`: 통과.
- 게임 실행·설치·커밋·실측: 수행하지 않음.
