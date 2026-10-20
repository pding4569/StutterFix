# Codex 과제 S4: decorender 를 "실제로 싸게" — 깨어 있는 보이는 장식만 증분 빌드 (성능 단계)

같은 규칙(E:\StutterFixWork\wt-decorender2, 커밋·게임 실행·설치 금지, `#if DEV || AUTOTEST`, 사용량 절약). 결과 문서 `docs/handoff/codex-decorender-10-20261020-result.md`.

## 상황
`decorender on 0 scope=all atlas=128` (Arche 곡 250초~): 장식 26,674개를 가져가 **구간(run) 493개, 그중 한 프레임에 그리는 구간은 68개**, 현재 보이는 관리 장식 약 1,774개(SpriteRenderer 대신 68개 메시 렌더러). `compare` 8회 모두 최대 2/255, 다른 픽셀 ≤0.6%. 즉 화면은 거의 같다(재질 빌려 쓰기로 정렬 문제 해결).
**문제는 성능이다**: 프로토타입의 프레임당 빌드가 **평균 약 1,000ms**(`build-ms/mean-frame=1003`, 켠 판 82 FPS vs 끈 판 199 FPS). 장식 2.9만 개(꺼진 자식 MeshRenderer 2.9만 개 포함)를 매 프레임 돌며 속성을 읽는 구조라서다. 우리가 노리는 이득(같은 장면 벤치에서 SpriteRenderer 2,000개 → 인스턴싱/메시 소수로 전체 프레임 -1.0~-1.4ms)을 보려면 **빌드가 프레임당 0.3~0.5ms 이하**여야 한다.

## 목표 (이번 단계의 관문)
`decorender on 0 scope=all` 상태에서 Arche 곡 264~276초 `fuse`/프레임 시간이 **끈 판과 같거나 더 낫다**(지금 끈 판: 모드 갱신 2.54ms + Late 1.51ms, 한 12초 창 프레임 약 1,520). 빌드 자체의 CPU ms(평균/p99)를 `state` 에 크게 보여라. 화면 `compare` 도 지금처럼 최대 ≤2/255.

## 어떻게
1. **프레임에 한 번만, 깨어 있는 장식만**: 이 모드는 이미 장식을 "깨어 있는 것/잠든 것"으로 나눠 매 프레임 깨어 있는 장식(Arche 평균 약 1,166개)만 훑는다(`Dormancy.cs`: 잠든 조건=보임 아님·히트박스 없음 등, `AwakeFrames/AwakeTotal`, `List(...)`). 가져간 장식 중 **깨어 있고 보이는(`rendererEnabled && alpha>0 && !forceRenderingOff`) 것만** 정점을 만든다. 잠든 장식의 정점은 0개로 두고(이미 그렇게 해 둔 상태 유지), 장식이 깨어나거나 잠드는 시점의 변화는 기존 Dormancy 훅(`WakePost`, `SetVisible`, `ApplyColor` 훅)으로 받아 "그 장식 구간 dirty" 표시(프레임마다 2.9만 개를 폴링하지 않는다). 꺼진 자식 MeshRenderer 2.9만 개는 폴링하지 않는다 — 재활성은 `EnableMeshRenderer` 게임 함수/`meshRendererEnabled` 필드 변경을 Dormancy 가 이미 보는 곳(또는 같은 훅)에서 받는다.
2. **값 출처**: 매 프레임 `Transform.localToWorldMatrix` 같은 네이티브 호출을 장식마다 하지 않는다. `TightLate.Apply` 는 그 프레임에 각 장식의 최종 위치·회전·크기를 이미 계산해 Transform 에 쓴다(처리 장식 약 1,130~1,310개/프레임). 그 계산 결과를 가져간 장식에 한해 **SoA 배열(위치 x,y,z, 회전 quaternion 또는 z각, 크기 x,y, 색 rgba)로도 기록**하는 방법(`#if DEV || AUTOTEST` 훅)을 쓰고, TightLate 가 안 거치는 장식(예: `MoveApply.SetLocal` 직접 경로, `Eligible` 아닌 장식)은 폴백으로 그 장식만 Transform 을 읽는다. 기존 Transform/SpriteRenderer 쓰기는 이번엔 그대로 둔다(이번 단계는 렌더만 대체 — 읽을 때 Transform 값과 SoA 값이 비트 동일한지 샘플 검증 `verify` 도 넣어라).
3. **증분·병렬**: 바뀐(dirty) 장식의 정점만 다시 계산(이전 값과 비교), 정점 계산은 `IJobParallelFor`(NativeArray, Burst 없음)로 워커 스레드에서, 메시 업로드는 구간(run)마다 `SetVertexBufferData` 로 바뀐 범위만, 그리지 않는 구간은 `enabled=false`. 한 프레임 한 번 `Camera.onPreRender` 첫 카메라 시점(또는 LateUpdate 이후)에 처리하고 4개 카메라가 같은 메시를 쓰게 한다(지금도 같은 layer 공유라면 그대로).
4. **로그/통계**: `state` 에 프레임당 빌드 ms(평균/p99: 수집, 정점 계산, 업로드 분리), 이번 프레임에 정점을 다시 만든 장식 수, 그리는 구간 수, SoA 대조 불일치 수.
5. 정확성은 계속 `compare` 로 본다. 이전 단계의 변화 감지·구간 재구성·off 정리 동작은 유지.

## 결과 문서에
구현 요약, 새 훅 위치(파일·함수), `state` 항목, 빌드 결과, 게임에서 돌릴 순서(`decorender on 0 scope=all`, `compare`×N, `state`, `off`; 성능은 `fuse begin/end` 12초 창, 켬/끔 비교). 성능을 못 맞췄다면 어느 부분이 지배적인지(수집/계산/업로드)를 적는다.
