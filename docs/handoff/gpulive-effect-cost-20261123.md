# Codex 작업: 장식 이동 효과가 GPU 장식 표에 들어갈 때의 한 프레임 비용 줄이기 (2026-11-23)

작업 폴더 `E:\StutterFixWork\wt-v14prod`, 브랜치 `claude/v3-282port`. **게임 실행·종료, 설치 DLL 교체, git 커밋/푸시는 하지 않는다.** 게임 측정은 Claude 가 한다. 작업 트리의 다른 변경을 덮지 않는다. 디컴파일한 게임 코드를 저장소에 넣지 않는다.

## 문제
Arche(장식 28,835개, GPU 장식 켬 = 기본) 곡 219.6초와 274.8초에 장식 이동 효과(`ffxMoveDecorationsPlus`) 하나가 장식 수천 개를 한 프레임에 옮겨 그 프레임이 24~29ms(평소 2.7ms)다.
2kHz 프로파일(끊긴 프레임 51표본 ≈ 25.5ms):
- 30표본(15ms): `FastMove.Prefix → FastMove.Run → DecoBake.SkipAdopt → DecoBake.GpuLiveSkip → ApplyGpuLiveEffectToTarget → SetMasterSlotStatic/Animated → WriteMasterSlot / PropagateMasterSlotToSlices → GpuLiveSlice.MarkDirty`, `Dictionary<scrDecoration,int>.FindEntry`(gpuLiveMasterByDeco).
- 10표본(5ms): `TightLate.ManagerEnd → DecoBake.GpuLiveFrame → DispatchGpuLiveSlice` 안 `Renderer.get_enabled`, `GpuLiveSlice.HiddenAlpha`, `ApplyAlpha`.
이미 고친 것(이번 작업 트리): `MasterSlotList.Remove` O(1), `WriteMasterSlot`(48바이트 Anchor 를 배열 칸에 바로 써 Buffer.Memcpy 제거), `PropagateMasterSlotToSlices(in ..)`.

## 목표
같은 결과(GPU 장식 채널·앵커·슬라이스 배열·더티 표시·렌더러 상태·로그 순서까지 바이트 단위로 같음)를 유지하면서 그 프레임의 장식당 CPU 비용을 크게 줄인다. 효과 적용 시점(그 프레임)은 바꾸지 않는다.

## 후보 (스스로 더 찾을 것 — 막히면 엉뚱한 아이디어도 빠르게 시험)
1. 효과 단위 일괄: 같은 효과의 장식들은 채널 목록·이징 끝값·애니메이션 원본이 같다. 장식마다 다시 계산하는 것(PrepareGpuEffect, easeEnd 계산, 채널 루프 조건)을 효과당 한 번으로.
2. `PropagateMasterSlotToSlices`: 채널마다 `gpuLiveMasterToSliceItem[masterIdx]` 와 슬라이스 루프를 다시 돈다 → 장식당 한 번 찾아 16채널에 쓰기.
3. `MarkDirty` 가 칸마다 목록 추가라면 범위 표시(장식 단위)로.
4. `gpuLiveMasterByDeco` 사전 조회 → scrDecoration 에 붙일 수 있는 정수 인덱스(예: 이미 있는 다른 표, ConditionalWeakTable 이 아닌 배열) 재사용.
5. GpuLiveFrame 쪽 `Renderer.get_enabled`(엔진 호출)를 우리가 마지막으로 쓴 값 캐시로(게임·다른 코드가 바꿀 수 있으면 그 경로를 확인하고 근거를 적을 것).
각 후보마다 "원래와 같음" 근거(코드 경로)와 예상 절감(장식당 호출 수 × 단가)을 적는다.

## 검증 (Claude 가 돌릴 수 있게 명령으로)
- 자동 시험 명령으로 켜고 끌 수 있는 대조 모드: 새 경로와 원래 경로를 같은 효과에 대해 각각 계산해(원래 경로는 복사본 배열에) 채널·앵커·슬라이스 배열·더티 집합 비교, 불일치 수 로그. `slotcheck`(AutoTest.cs) 처럼 `#if AUTOTEST`.
- 빌드 3종(`-p:Edition=Player`, `-p:Edition=Debug`, `-p:Edition=Player -p:AutoTestBuild=1`) 오류 0.
- Claude 가 돌릴 단계 예: `auto on` → `game <Arche>` → `<대조 켬>` → `press` → `waitsongpos 280 700` → `<대조 상태>` → `quit`.

## 결과물
`docs/handoff/gpulive-effect-cost-20261123-result.md`: 바꾼 파일(명시적 경로), 각 변경의 같음 근거, 예상 절감, 대조 명령, 남은 위험.
