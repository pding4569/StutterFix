# Codex 과제 S5: decorender 빌드가 왜 아직 프레임당 1초인가 — 구간별 계측 + 매 프레임 전체 감시 제거

같은 규칙(E:\StutterFixWork\wt-decorender2, 커밋·게임 실행·설치 금지, `#if DEV || AUTOTEST`, 사용량 절약). 결과 문서 `docs/handoff/codex-decorender-11-20261020-result.md`.

## 게임 결과 (S4 증분 빌드, `decorender on 0 scope=all atlas=128`, Arche 곡 252~262초)
`state`: `build-ms/mean-build=1029.7 (p99 1118.9, n=8)`, **`collect:mean=71.6ms`**, `compute-ms=2.59`, `upload-ms=11.18`, **`changed-items=27447`**, `uploaded-vertices=109788`, `builds=8`(10초에 8번 = 게임이 약 1 FPS), `active-barriers=207`, `waiting-barriers=31411`.
- 수집+계산+업로드 합이 약 85ms 인데 총 빌드가 1,030ms: **약 940ms 가 어느 구간에도 안 잡힌 비용**이다(추정: `order-audit … per-camera live audit` — 장벽 렌더러 6.5만 개 상태를 카메라 4개마다 매 프레임 읽기, 대기 장벽 31,411개 감시).
- `changed-items=27447`: 매 빌드마다 거의 **모든 가져간 장식이 바뀐 것으로** 나온다. 증분이 작동하지 않는다. 움직이는 장식(보이는 것)은 약 1,100~1,800개여야 한다.

## 고칠 것
1. **전 구간 계측**: 한 프레임 빌드 안의 모든 단계에 `Stopwatch`(QueryPerformanceCounter 틱)를 둔다 — (a) 장벽 감시/감사(audit), (b) 변화 감지·dirty 판정, (c) SoA 수집(collect), (d) 정점 계산(compute, 병렬 job 시작·완료 대기 분리), (e) 메시 업로드, (f) 그리기 구간 on/off, (g) 그 밖. 단계별 평균/p99/최대와 호출 횟수를 `state` 에 표로 낸다. 합이 총 빌드 시간과 맞는지(남는 시간이 0 에 가깝게) 확인하는 `unaccounted-ms` 도.
2. **장벽 감시를 매 프레임·카메라마다 하지 말 것**: 감사는 프레임당 **한 번**(첫 카메라), 그것도 전수 스캔이 아니라 증분: (i) 장식 자식 `MeshRenderer`(28,827개)는 폴링하지 않고 `scrVisualDecoration.EnableMeshRenderer`/`meshRendererEnabled` 변경을 받는 곳(Dormancy 가 이미 추적)에서만 이벤트로 받는다. (ii) 비장식 장벽은 현재 활성 약 200개 + 대기 약 1,400(타일 메시 등) — **이 약 1,600개만** 프레임당 한 번 `enabled`/`forceRenderingOff`/`gameObject.activeInHierarchy` 를 읽는다(전체 6.5만 개 금지). 대기(`waiting`)로 분류된 나머지는 감시 대상 목록을 만들 때 한 번만 분류하고 이후 폴링하지 않는다. 그리는 상태가 바뀐 비장식 렌더러만 영향 구간 재구성.
3. **변화 감지가 전부 "바뀜"으로 나오는 원인을 찾아 고친다**: `changed-items=27447` 은 아마 비교가 비트 정확 비교 실패나 매 프레임 갱신 플래그 때문이다. 깨어 있고 보이는 장식(Dormancy 의 그 프레임 깨어 있는 목록) 중 **이전 프레임과 값이 다른 것**만 dirty 로 하고, 잠든 장식은 건드리지 않는다. 처음 1회 전체 구성 외에는 `changed-items` 가 약 1,000~2,000 수준이어야 한다. 로그에 (프레임, changed 수, 깨어 있는 장식 수) 처음 10프레임 + 이후 평균.
4. **목표**: `fuse` 12초 창에서 프레임이 정상(수백 프레임 이상)이 되고 빌드 총합이 평균 0.5ms 이하. 못 맞추면 `unaccounted` 포함 단계별 지배 비용을 결과 문서에 적는다.
5. 정확성은 계속 `compare` 로 본다(최대 ≤2/255).

## 결과 문서에
구현 요약(감사 방식 변경, dirty 판정 수정), 단계별 계측 항목, 빌드 결과, 게임에서 돌릴 순서(`decorender on 0 scope=all atlas=128`, `state`, `compare`×N, `fuse begin/end`).
