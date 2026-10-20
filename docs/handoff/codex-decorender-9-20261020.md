# Codex 과제 S2j: decorender — 꺼진 자식 MeshRenderer 는 순서 장벽이 아니다(켜질 때만 복귀) + 보이는 구간 통계

같은 규칙(E:\StutterFixWork\wt-decorender2, 커밋·게임 실행·설치 금지, `#if DEV || AUTOTEST`, 사용량 절약). 결과 문서 `docs/handoff/codex-decorender-9-20261020-result.md`.

## 게임 결과 (S2i, `decorender on 0 scope=all atlas=128`, Arche)
- **화면 동일성은 매우 좋다**: 장식 **28,396개**를 가져간 상태로 곡 253~293초 8번 `compare`: 최대 0~1/255, 다른 픽셀 0~270개(전체 368만 중).
- 그러나 `run-state={candidates=28534 runs=26677 taken=26677 average-run=1.0 max-run=1}`: 구간이 전부 크기 1이다. 장벽 집계 `barrier-renderers={decoration-child:MeshRenderer=28827, non-decoration:MeshRenderer=1400, changed=1719, atlas-budget=138, large-texture=98, repeat-not-one=177, wrap=17, non-decoration:SpriteRenderer=11, point-sampling=1}`. 즉 **장식마다 자식 `MeshRenderer`(decoration-child) 가 하나씩 있고, 이게 정렬 줄에서 장식 사이에 끼어 매번 구간을 끊는다**(전부 `enabled=false` 일 것이다: 장식의 `meshRendererEnabled`/`EnableMeshRenderer` 용).

## 고칠 것
1. **장벽 규칙**: `decoration-child:MeshRenderer` 를 **지금 안 그려지는 상태(`enabled=false` 또는 `gameObject.activeInHierarchy=false` 또는 `forceRenderingOff`)이면 장벽에서 제외**(정렬 줄에서 무시). 안 그려지는 비장식 렌더러(`non-decoration:*` 중 꺼진 것)도 같게 — 다만 비장식 렌더러는 나중에 켜질 수 있으므로 감시한다: 켜지는 순간(그 프레임 PreRender 의 `enabled`/`forceRenderingOff` 변화 확인)에 그 렌더러가 속한 키의 영향받는 구간을 재구성(기존 "변화 감지 시 구간 재구성" 경로 재사용). 장식 자식 MeshRenderer 는 `scrVisualDecoration.EnableMeshRenderer`(및 `meshRendererEnabled` 필드)가 켜지는 걸 PreRender 에서 장식 단위로 확인하거나 이미 있는 Dormancy 의 해당 감시(`Dormancy` 가 `meshRendererEnabled` 를 어떻게 추적하는지 참고)를 읽는다. 켜지면 그 장식과 그 장식이 속한 구간을 복귀(재구성).
2. **그리는 중인 장벽 종류 분리**: 현재 그려지는 비관리 렌더러(활성 `enabled && !forceRenderingOff`, 장식 아님 포함)만 "활성 장벽", 꺼진 것은 "대기 장벽" 으로 나눠 개수를 통계로.
3. **run 통계 확장** (`state` 와 `on` 로그): 후보 수, **run 수(활성 장벽만 기준)**, **그중 "보이는 장식(현재 정점을 내보내는 장식)이 하나라도 있는 run 수"** 의 평균·최대(프레임별 표본: 최근 N프레임 평균), 평균/최대 run 크기, 분포(1, 2~5, 6~20, 21~100, 101+), 활성 장벽 종류별 개수, 키 수. 보이는 run 이 몇 개나 그려져야 하는지가 엔진 렌더러 개수(핵심 비용)이다. 정점이 0개인 run 의 MeshRenderer 는 `enabled=false` 로 둬서 그리기 비용을 없애는 처리도 추가(그리는 run 수를 `state` 에 표시).
4. 나머지(scope, atlas, compare, 증분 빌드, 재질 빌리기)는 그대로.

## 결과 문서에
구현 요약, 새 `state` 항목, 빌드 결과, 게임에서 돌릴 순서(`decorender on 0 scope=all`, `compare`×N, `state`, `off`).
