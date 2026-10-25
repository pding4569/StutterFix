# 자동 반복 8회차

먼저 `docs/handoff/codex-master-20261022.md`(마스터 인계문서)를 읽어라.

직전 게임 시험 결과(자동 수집): `docs/handoff/im-autoloop-run7-evidence.txt`. 먼저 `docs/handoff/im-autoloop-ledger.md` 장부를 읽고, 이미 실패한 접근은 되풀이하지 말고 결과에서 배운 점으로 다른/더 나은 접근을 택해라(같은 코드를 다시 돌리는 것은 금지). 게임 코드는 `E:/StutterFixWork/decompiled/Assembly-CSharp/<타입>.cs` 디컴파일본으로 읽어라(근사, ilscan 으로 교차확인). 성공 조건(정규식) `IMAGEOWN=PASS` 이 아직 안 나왔다. 결과를 분석해 원인을 코드로 찾아 고치고, 안 되는 이유가 설계 한계면 결과 문서에 구체적으로 적고 멈춰라. 아래는 원래 임무다.


---
# Codex 지시서: 이미지 장식 약 4,976개를 GPU 소유 + draw 통합에 편입 (픽셀 정확하게)
먼저 `docs/handoff/codex-master-20261022.md`, `docs/handoff/claude-gpudeco-image-playback-20261024.md`(이전 중단 시점 인계), `docs/handoff/gemini-tie-image-400fps-plan.md`(제미나이의 코드 대조 분석 — 3장이 이 작업의 핵심)를 읽어라.

## 상황 (측정)
- Arche 260~270초, 3440x1440@165. 현재 GPU 소유 23,547개 + draw 통합(안전 모드): 약 202 FPS(끔 약 165), 프레임 4.94ms. 정지 장식 맵 2.0ms.
- 남은 큰 덩어리 = **GPU 소유에서 빠진 이미지 장식 약 5천 개**(안개 `nesFires` 3,231 + `nesFires2` 1,731 등)의 CPU 이동·관리: `TightLate.List/Process/Apply`, `DecoAnim.StepPair/SetPosition`, `FastMove.Prefix`, 워커 `TransformChangeDispatch` 합쳐 약 1ms.
- `gpudeco images own on` 으로 이미지 4,976개 소유는 **동작하지만 실제 게임 픽셀 차이가 42~121픽셀(최대 차이 5~25) 남는다**(이전 인계). 원인 미확정.

## 이번 일 (순서대로, 막히면 근거 로그를 장부에 남기고 다음으로)
1. 픽셀 차이 원인 확정: 제미나이 분석 3.1의 진단 둘을 먼저 한다. (a) `RefreshGpuImageGeometry` 시작 지점에서 `d.UpdatePosition()` 1회 호출 후 정지 경계 픽셀 비교, (b) `LocateGpuImageDifference` 에서 CPU `r.transform.TransformPoint` 정점을 GPU VB 에 넣었을 때 차이 0 인지(0 이면 정점 수식 오차, 아니면 정렬/텍스처/Flush 타이밍). 결과를 `GPUIMG_DIAG` 로 로그.
2. 원인에 맞게 수정해 `images own on` 상태에서 260/262/270초 픽셀 최대 차이 3 이하.
3. 그 상태에서 draw 통합 편입: `GpuDecoDrawCollapse.cs` 의 `GpuLiveOwned==23547` 입구 검사 완화, `PrepareDrawCollapse` 가 이미지 변형 텍스처를 배열에 등록, `RefreshGpuImageGeometry` 가 `_CollapseTexture` 슬롯을 부분 갱신(제미나이 3.2). `FlushGpuImagesBeforeVisible` 의 매 프레임 4,476개 전수 순회를 알파 더티 이벤트 구동으로 바꾼다(3.3 마지막 표).
4. 성공 기준: 260~270초 ABBA(`images own on` + draw 통합) FPS 가 현재 약 202 에서 **+20% 이상**, 픽셀 최대 차이 3 이하, 폴백 0, 30ms 이상 끊김 0. 결과 문서 첫 줄에 `IMAGEOWN=PASS` 또는 `FAIL` 과 숫자.

## 파일 소유 규칙 (Claude 가 동시에 작업 중이니 충돌 금지)
- **Codex 가 고쳐도 되는 파일**: `GpuDecoImage*.cs`, `LazySprite.cs`, `GpuDecoImagePlayback.cs`, 이미지용 시험 계획 `tools/optimization/imageown-steps.py`, 이미지 관련 문서.
- **Claude 가 고치는 중(건드리지 마라, 필요하면 결과 문서에 요청으로 남겨라)**: `GpuDecoDrawCollapseRenderer.cs`, `DrawCollapsePolicy.cs`, `Dormancy.cs`, `TightLate.cs`, `FastMove.cs`, `GpuDecoExport.cs`. 단 위 3번의 `GpuDecoDrawCollapse.cs` 입구 검사 한 줄과 `UpdateDrawCollapseTextureSlot` 헬퍼 추가는 허용(작게, 다른 줄 건드리지 말 것).
- 시험은 `tools/optimization/imageown-steps.py` 가 만드는 계획(ABBA+픽셀, 약 10분)을 자동 장치가 돌린다. 게임 시험은 `GameLock` 으로 직렬화되어 Claude 의 시험과 번갈아 돈다.

## 2026-10-25 16:50 Claude 지시 (4회전 결과 반영): 텍스처 등록은 "창 범위"로 줄여라 — 이게 `image_texture_not_registered_in_collapse` 의 해법이다
증거: `GpuDecoImagePlayback.cs` 의 `RegisterGpuImageCollapseVariants` 는 `gpuImageVariants` 의 **곡 전체** `ReadyQuad` 변형을 전부 `textures` 에 넣는다. 그래서 `DRAWCOLLAPSE_ARRAY_TOTAL` 이 `planned_arrays_known=3 direct_textures=32 active_texture_slots=35`(상한 36) 로 가득 차고 `live on` 이 `image_texture_not_registered_in_collapse` 로 막힌다. 하지만 draw 통합은 한 창(준비 창 약 257.5~270.25초)만 그린다.
해야 할 일 (이미지 파일 영역이니 네가 한다):
1. 이미지 타임라인(`GpuDecoImageTimeline.cs`, `GpuDecoImagePlan.cs`)에서 **각 이미지 변형이 적용되는 곡 시각**을 얻어, 창 `[257.5, 270.25]` 안에서 적용되는 변형 + 창 시작 시점에 이미 적용돼 있는 상태(초기 스프라이트)만 `textures` 에 등록하라. 창 밖(곡 다른 곳)에서만 쓰이는 변형은 등록하지 않는다. 창 범위 정보가 이미 있는 함수가 있으면 재사용(예: `VisibleInWindow`, `table`).
2. 등록 후 `GPUIMG_DIAG mode=collapsecoverage` 로그의 `registered`, `missing`, `direct_required`, `arrays_used` 를 확인한다. `missing=0` 이고 `direct_required <= 32`, 배열 ≤ 4 이면 통합으로 진행. **그래도 넘치면** 숫자(포맷별·크기별 개수, 필요 슬롯 수)를 결과 문서에 적고 멈춰라 — 그때는 Claude 가 형식별 은행/아틀라스 설계를 제미나이와 한다. 32 상한을 늘리거나 누락 검사를 끄지 마라.
3. 이어서 `imageown` 계획(333단계)을 다시 돌려 통합 픽셀·FPS 를 얻는다.
(참고: Claude 가 `GpuDecoDrawCollapseRenderer.cs` 에 재계획 키 캐시 최적화를 합쳤다. `RegisterGpuImageCollapseVariants`/`RememberGpuImageCollapseAddresses` 호출 2곳은 이미 연결돼 있다.)

## 2026-10-25 17:05 Claude 지시 (6회전 시험 결과 반영): `live on` 의 `image collapse changed mip/wrap` — wrap 모드 검사를 UV 범위 검사로 바꿔라
증거: 슬롯 48개로 늘린 뒤(Claude 가 `DirectBindings=32→48` + 번들 재빌드, 필요 36개 ≤ 48) 텍스처 커버리지 `missing=0` 통과, 다음 관문 `GpuDecoDrawCollapse.cs:27` 의 `RequireLive(texture.mipmapCount==1 && texture.wrapModeU==Clamp && texture.wrapModeV==Clamp, "image collapse changed mip/wrap")` 에서 막혔다. 원본 이미지 텍스처의 초기 wrap 은 Repeat 이고 게임 V15 setter/Defer 가 이벤트 시점에 Clamp 로 바꾼다. 우리 draw 통합 셰이더는 **자체 clamp 샘플러**를 쓰므로, 스프라이트 정점 UV 가 모두 [0,1](+작은 오차) 안이면 원본 Repeat 샘플링과 결과가 같다.
해야 할 일: 위 검사에서 wrap 모드 조건을 빼고, 대신 **해당 변형의 모든 UV 성분이 [-1e-4, 1+1e-4] 안**이고 `mipmapCount==1` 인지를 검사하라(변형 정보 `GpuDecoImageVariant.UV` 는 이미 있다). UV 가 범위를 벗어나는 변형(타일링 이미지)만 CPU 복귀/폴백으로 처리한다. 텍스처 wrap 을 미리 바꾸지 마라(다른 장식과 공유). `RequireLive` 메시지는 `image_uv_outside_unit_range` 로 구분해 원인 추적이 되게 하라. 이 수정은 이미지 영역 코드이며 `GpuDecoDrawCollapse.cs:27` 한 줄 수정만 허용한다.
