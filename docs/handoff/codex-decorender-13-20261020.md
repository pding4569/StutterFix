# Codex 과제 S7: decorender — topology 가 매 프레임 "전체 재구성"으로 떨어지는 원인 찾기·고치기

같은 규칙(E:\StutterFixWork\wt-decorender2, 커밋·게임 실행·설치 금지, `#if DEV || AUTOTEST`, 사용량 절약). 결과 문서 `docs/handoff/codex-decorender-13-20261020-result.md`.

## 게임 결과 (S6 빌드, Arche 곡 252~262초)
`topology:mean=961.9ms`(총 빌드 1182.7, n=7) — 이전(S5 784ms)보다 오히려 늘었다. 카운터: `topology-changed-keys-last=551 total=3857 key-rebuilds=0 full-rebuilds=8`(**빌드 7번에 전체 재구성 8번, 키 단위 증분 재구성 0번**), `changed-items=2577`(정상 수준), `soa-hits=0 transform-fallbacks=1555 capture-calls=6332 mapped=5619`(SoA 훅이 호출은 되지만(6,332) 적중 0: 매핑 5,619 인데 적중 0 → 키/인덱스 불일치 의심).
→ 증분 경로(`key-rebuilds`)가 한 번도 쓰이지 않고 **항상 전체 재구성**으로 간다. 코드에서 전체 재구성을 일으키는 조건: `DecoRender.Incremental.cs:342`(`i.Written && visible && !SameBits(i.Matrix.m23, matrix.m23)` → `topologyDirty = true` + `fallbackKeys.Add`), `DecoRender.cs:970`(`preparedSortZ` 가 `axis.z` 와 비트가 다르면 `topologyDirty`; 카메라 4개 중 `mainCamera` 만 비교하지만 `SceneCameras()` 순회 중 값이 매번 달라지는지 확인), `DecoRender.cs:~973`(`previousMerge != mergeCameras`), `Incremental.cs:118/124/469~491`(메시 활성 변경·watch 변경 등). `topology-changed-keys-last=551` 은 키 전체(461)보다 큰 값이라 거의 모든 키가 "변화"로 표시된다.

## 고칠 것
1. **원인 계측**: 전체 재구성/`topologyDirty` 를 세운 **원인별 카운터**(z 변화(`m23`), 정렬축 z 변화, mergeCameras 변화, 메시 활성 변화, watch 변화, 그 밖)와 마지막 원인, 원인별 장식 수(z 변화면 몇 개 장식이 얼마나(최대 |Δz|) 바뀌었나, 처음 10개의 (장식 인덱스, 이전 z, 새 z))를 `state` 에 낸다. 한 빌드마다 어떤 원인으로 전체 재구성을 했는지 `[decorender] topology-cause` 로 처음 20빌드 로그.
2. **원인 수정**: 가능성 높은 순서: (a) 가져간 장식의 `m23`(월드 z)이 매 프레임 미세하게 다르다(부모 합성/부동소수 잡음) → 비교를 "정렬에 영향 주는 z 변화"로 한정(원본 렌더러의 정렬 거리 `Vector3.Dot(position, sortAxis)` 대신, 같은 값 비교를 하되 바뀌었을 때도 **전체가 아니라 그 키만 증분 재구성**), (b) `preparedSortZ` 가 카메라마다 달라서 순회마다 토글 → 카메라별 값 저장, (c) 그 밖. 어떤 경우에도 **증분(키 단위)이 기본, 전체 재구성은 `on` 때와 카메라 목록 변경 때만**.
3. **SoA 적중 0**: 훅 호출 6,332, 매핑 5,619, 적중 0 — 적중 조건(어떤 키/인덱스로 조회하는지)을 점검해서 호출 시점·인덱스 매핑 불일치를 고친다. 적중이 되면 `collect`(현재 7ms)가 1ms 이하로 줄어야 한다.
4. 목표: `topology` 평균 ≤0.1ms, 총 빌드 ≤0.5ms. `key-rebuilds` 가 실제로 쓰이고 `full-rebuilds` 는 1(시작 때)로 유지.
5. 정확성: `compare` 최대 ≤2/255 유지.

## 결과 문서에
찾은 원인(코드 위치·조건), 수정 요약, 새 계측, 빌드 결과, 게임에서 돌릴 순서(`decorender on 0 scope=all atlas=128`, `state`, `fuse begin/end`, `compare`×N).
