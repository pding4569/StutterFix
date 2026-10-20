# Codex 임무 gpusplit-3: 이미지 장식 GPU 비용의 정체는 draw call/SetPass — 살아 있는(live) 장식만 정확히 분류하고 화면 동일 절감 후보를 증명 가능하게

2026-10-21. 작업 폴더 `E:\StutterFixWork\wt-archegpu` (브랜치 `codex/night-archegpu`, gpusplit-2 위). 게임 실행·설치·커밋 금지(Claude 가 한다). 결과 `docs/handoff/codex-gpusplit-3-20261021-result.md` + `-o`(10줄).

## 게임에서 나온 사실 (Arche 262~266초 같은 구간 반복, 2560×1440 120Hz, 기준 창 11개가 5.61~5.82ms 로 안정; 원본 `E:\StutterFixWork\night-results\gs3.log`, 정리표 `docs/optimization-36.md` 새벽 절)
- 기준: GPU 약 5.7ms, **전체 프레임 draw 2,074 · SetPass 1,946 · batches 2,070 · 삼각형 13,250 · 정점 20,100**.
- `deco-image` 전부 숨김(86,484개): GPU **3.30ms(−2.4)**, draw **235 · SetPass 107**, 삼각형 9,522, 정점 12,694. 즉 **이미지 장식이 draw ~1,840 · SetPass ~1,840 을 만들고, 삼각형은 겨우 3,760개** → **비용은 정점/채우기가 아니라 draw call/상태 변경**(SetPass ≈ draw 라 거의 배칭이 안 됨; 장식마다 재질 인스턴스가 달라서(정렬 동률 규칙이 재질 인스턴스 ID 를 쓴다) 보인다). 대략 draw 하나당 GPU 1.3~1.5µs.
- 하위 필터(스냅샷 258초 기준 멤버십): `offscreen`(86,484 = 전부) GPU 3.55 · `small=1`(전부) 3.32 · `alpha`(전부) 3.32 · `transparent`(27,451) **3.78(−1.9, draw 318)** · **`onscreen` 0개 / `big=1` 0개 / `additive` 0개 선택**. census: 이미지 렌더러 86,484 중 활성 28,828, 이미 forceRenderingOff 27,451, **unforced_live 1,377**.
- 해석 문제 두 가지: (1) `onscreen` 이 0 이고 `offscreen` 이 전부라는 건 **분류 오류가 의심**된다(실제로 draw 가 있는 장식이 화면에 보이는데). 본 카메라(`Camera#102298`, 이름이 정확히 "Camera") frustum 평면/렌더러 AABB 월드 좌표/직교 카메라 처리(`Camera.orthographic`, `GeometryUtility.CalculateFrustumPlanes`)나 카메라 배율(camRT·`Overlay`) 때문일 수 있다. 원인을 코드로 찾아 고친다. (2) `transparent` 는 258초 스냅샷 멤버십이라 그 뒤 262~266초에 불투명해지는 장식(페이드 인)까지 숨겨 **화면이 달라졌을 수 있다**. 그래서 −1.9ms 를 "헛제출 절약"이라 단정할 수 없다. 이 둘이 이번 임무의 핵심.

## 해야 할 것
1. **분류 버그 진단·수정**: 왜 `onscreen=0` 인지. `gpusplit census` 가 샘플 렌더러 몇 개(첫 5개)의 bounds center/extents, 본 카메라 위치/ortho size/aspect/frustum 평면, 교차 판정 결과를 로그로 보여주게 하고(개발 전용) 원인을 확정·수정. 같은 census 에서 '보이는 것'이 1,300개 안팎(unforced_live 1,377)이어야 정상.
2. **동적 멤버십 모드**: 스냅샷이 아니라 **매 프레임(Camera.onPreCull) 현재 상태로** 분류하는 필터 `live`, `live:visible`(onscreen 이면서 현재 알파>임계), `live:invisible`(현재 알파≤임계 또는 화면 밖인데 forceRenderingOff 아님), `live:tiny`(투영 면적<임계). 숨기기 전용 진단(화면 바뀜)과 별개로 **"숨겼을 때 같은 프레임 화면이 같은가"를 자동 증명**하는 `gpusplit pixeldiff <필터>`: 같은 프레임에서 본 카메라를 두 번(숨김 없음/있음) RenderTexture 로 렌더해 RGBA 최대 차이·다른 픽셀 수를 로그(개발 전용, GPU 동기·읽기 비용은 성능 창과 분리; `FilterIdentityProbe`/`RenderVerify` 패턴 참고, 하지만 이번엔 UI 제외 본 카메라 RT 만). 필터별 `pixeldiff` 결과가 0 이어야 "화면 동일 절감"으로 인정.
3. **측정 steps**: 같은 구간(Arche 262~266초) 반복, 앞뒤 기준 창, 후보: `live:invisible`, `live:tiny`, `live:invisible+tiny`, `live:visible`(비교용). 각각 GPU·draw·SetPass 변화 + `pixeldiff` 결과를 한 번에 얻는 `sf_run` steps 를 결과 문서에(`tools/optimization/gpusplit3-steps.py` 생성기). draw 카운터는 전체 프레임 값임을 유지하되 본 카메라 외 draw 가 안정적임을 기준 창으로 확인.
4. **화면 동일 절감 구현 후보 설계(구현은 `pixeldiff` 0 인 필터가 있을 때만, 기본 꺼짐)**: 예) 현재 알파가 임계 이하이거나 화면 밖인 live 장식을 `forceRenderingOff` 로 자동 처리(기존 `Dormancy`/투명 장식 처리와 중복되지 않게; 그 구현이 왜 이 장식들을 놓쳤는지 `Dormancy.cs`/`InvisibleSkip` 코드를 읽어 설명). 비용(프레임당 순회)을 계측해 이득이 순회 비용을 넘는지 계산.
5. 빌드 3종(`-c Release`; `-p:Edition=Player`; `-p:Edition=Player -p:AutoTestBuild=1`) 오류 0 + LoadCheck + 기존 GpuSplitCheck fixture 통과(새 분류 로직 fixture 추가). Player 제외 유지. 새 Harmony 패치는 이름+인자 수+호출 규약으로 정확히 하나, `Module.ResolveSignature` 금지.

## 규칙
- 헤르츠 맵 이름·비공개 프로젝트 언급 금지, 100만 타일 맵 열기 금지. 그림자·진단 모드 유지(제품 경로 변경 금지).
- 이전 Tight 메시 시험은 990조건 중 832조건 픽셀 불일치였다 → 메시를 바꾸는 접근은 이번 범위 밖.
