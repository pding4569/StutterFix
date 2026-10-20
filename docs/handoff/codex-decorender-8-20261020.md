# Codex 과제 S2i: decorender — 정적 장식도 함께 가져가 연속 구간(run)을 키우기 + 프레임당 한 번 증분 빌드

같은 규칙(E:\StutterFixWork\wt-decorender2, 커밋·게임 실행·설치 금지, `#if DEV || AUTOTEST`, 사용량 절약). 결과 문서 `docs/handoff/codex-decorender-8-20261020-result.md`.

## 게임에서 얻은 결과 (S2h, Arche 곡 약 248초 이후)
`run-state={moving-candidates=6444 runs=6431 taken=6431 average-run=1.0 unmanaged-breaks=6271 keys=160 observation-change-fields={sprite=1354 sortingOrder=1}}`, `managed-visible=819 managed-hidden=5612`, `static=20735`.
→ **관리 후보(움직이는 장식)가 정렬 줄에서 전부 따로따로다**(구간 크기 1): 움직이는 장식 사이사이에 정적/비관리 장식이 끼어 있기 때문(같은 키 0/1 에 renderers=1762, 후보 866, 관리 109 …). 그래서 구간 단위로 묶어도 개수가 줄지 않는다. 또 한 프레임 빌드가 카메라당 2.4초(4대, 총 7.5초)여서 게임이 멈추고 `stopped: scene/manager/list/camera/playback transition` 으로 중단, compare 를 못 했다.

## 바꿀 것
1. **후보 범위 확장 `scope=all`(기본)**: 움직이는 장식뿐 아니라 **정적 장식도 후보**(조건은 같다: 정확히 `scrVisualDecoration`, `Sprites/Tile` 기본값 조건/Default 계열, Clamp, 한 변 ≤ 2048, 마스크/히트박스 아님, 점샘플링 아님, repeat==1…) — 그래야 정렬 줄에서 후보가 이어진다. 20프레임 관찰은 `scope=all` 에서 생략(변화 감지는 계속). `scope=moving` 은 지금처럼 유지.
2. **아틀라스 예산**: 정적 장식 텍스처까지 넣으면 예산 64MiB 를 넘는다 — `atlas=` 인수(MiB, 기본 128, 최대 256)를 두고, 넘으면 큰 텍스처의 장식부터 후보에서 빼서(`atlas-budget`) 그 장식이 구간을 끊는 효과를 통계에 나타낸다. 정적 장식이 쓰는 텍스처 종류/크기 분포(상위 20개)도 로그.
3. **빌드 비용**: 지금은 카메라마다 모든 장식을 다시 계산한다. **프레임당 한 번만** 계산하고(월드 정점은 카메라와 무관) 4개 카메라가 같은 메시를 그린다(Unity layer 로 이미 공유 중이면 그대로). **증분**: 장식별로 마지막으로 쓴 값(월드 행렬 해시/float 비트, 색, 보임)을 저장하고 바뀐 장식의 정점만 다시 계산, 정점 버퍼는 `SetVertexBufferData` 로 변경된 구간(또는 구간 전체)을 한 번에 업로드. 메시 개수(run 수)는 통계로. 프레임당 CPU ms(계산, 업로드)를 `state` 에 남긴다.
4. **중단 조건**: `scene/manager/list/camera/playback transition` 중단이 긴 프레임(수 초) 때문에 거짓으로 발동하지 않게 한다 — 무엇이 transition 으로 판정되는지(코드) 조사해 결과 문서에 적고, 진짜 전환(맵 다시 열기/편집기 복귀/카메라 목록 변화)에서만 멈추게 고친다.
5. **run 통계(핵심 판단 자료)**: `scope=all` 에서 후보 수, **run 수**, 평균/최대 run 크기, 분포(1, 2~5, 6~20, 21~100, 101+), run 을 끊는 비관리 렌더러 종류별 개수(제외 이유별: large-texture, repeat, wrap, 마스크, 비장식 렌더러 …), 키 수. 성패는 "run 수 ≪ 후보 수"인지다(목표: run 수가 수백 이하).
6. 나머지(compare, off, 재질 빌려 쓰기, 변화 감지 시 구간 재구성, floatcolor)는 그대로.

## 결과 문서에
구현, 새 인수(`scope`, `atlas`), 중단 조건 조사, 빌드 결과, 게임에서 돌릴 순서(`decorender on 0 scope=all`, `compare`×N, `state`, `off`).
