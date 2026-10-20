# Codex 과제 S6: decorender — `topology` 784ms/프레임 제거(증분), 남은 209ms 미계측 비용, SoA 훅이 안 맞는 이유

같은 규칙(E:\StutterFixWork\wt-decorender2, 커밋·게임 실행·설치 금지, `#if DEV || AUTOTEST`, 사용량 절약). 결과 문서 `docs/handoff/codex-decorender-12-20261020-result.md`.

## 게임 결과 (S5, `decorender on 0 scope=all atlas=128`, Arche 곡 252~262초)
`build-cpu-ms/window2048`: **total mean=1001.4ms**(p99 1086, n=8 빌드 / 10초: 게임이 약 1 FPS), `collect=7.26ms`, `vertices-job+wait=0.29ms`, `upload+bounds=0.88ms`, **`topology=783.8ms`**(p99 791), 합이 약 792ms 이므로 **약 209ms 가 아직 어디에도 안 잡힘**. `changed-items=2850`(이전 27,447 에서 크게 줄음: 좋음), `collect-items=27455`, **`soa-hits=0`, `transform-fallbacks=1570`**(TightLate SoA 기록이 안 맞아 Transform 을 읽는다), `uploaded-vertices=11400`.
→ 이제 지배 비용은 **`topology`**(정렬 줄·구간(run) 재구성으로 추정)이고, 프레임마다 전체를 다시 한다. 구간 토폴로지는 장벽의 활성 상태가 바뀔 때 그 키의 구간만 다시 만들어야 한다.

## 고칠 것
1. **`topology` 증분화**: 키마다 렌더러의 정렬(렌더 큐, z 거리, 재질 ID)과 관리 후보 여부는 정적(장식 생성·상태 변경 이벤트 때만 바뀜). 구간은 "활성 장벽"의 위치로만 정해진다. 활성 장벽 목록의 변화(`enabled/forceRenderingOff/active` 변경)가 없는 키는 구간을 그대로 두고, **변화가 있는 키의 구간만** 다시 계산한다. `on` 시점의 정렬은 한 번만. 프레임당 `topology` ms 는 변화 키 수에 비례하도록(평균 0.1ms 이하, 변화 없으면 ~0). 변화 키 수·구간 재구성 횟수를 `state` 에.
2. **남은 약 209ms**: 총 빌드(total)와 단계 합의 차이를 모든 구간에 `Stopwatch` 를 더 넣어 정확히 찾는다(감사/audit, 장벽 감시 폴링, 재질 빌림 갱신, 구간 on/off 토글, 변화 키 처리, `Camera.onPreRender` 진입 전후 등). `unaccounted-ms` 를 0 에 가깝게. 지배 항목을 `state` 표에 올려라.
3. **`soa-hits=0`**: TightLate SoA 기록 훅이 한 번도 적중하지 않았다. 원인(훅이 호출되지 않는다/조건이 안 맞는다/인덱스 매핑 실패/가져간 장식이 TightLate 대상이 아니다 등)을 조사해서 고치고, 안 되면 이유를 결과 문서에. `transform-fallbacks=1570` 이 계속 남더라도 `collect` 가 7ms → 1ms 이하가 되는지(깨어 있고 보이는 장식만 읽도록) 확인. 
4. 목표: `fuse` 6~12초 창 프레임이 정상 수준(수백 프레임 이상), 빌드 총합 평균 ≤0.5ms. 못 맞추면 단계별 지배 비용을 쓴다.
5. 정확성: `compare` 최대 ≤2/255 유지.

## 결과 문서에
구현 요약(topology 증분 방식), 새 계측 항목, SoA 훅 원인, 빌드 결과, 게임에서 돌릴 순서(`decorender on 0 scope=all atlas=128`, `state`, `fuse begin/end`, `compare`×N).
