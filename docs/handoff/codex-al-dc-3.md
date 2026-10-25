# 자동 반복 3회차

먼저 `docs/handoff/codex-master-20261022.md`(마스터 인계문서)를 읽어라.

직전 게임 시험 결과(자동 수집): `docs/handoff/dc-autoloop-run2-evidence.txt`. 먼저 `docs/handoff/dc-autoloop-ledger.md` 장부를 읽고, 이미 실패한 접근은 되풀이하지 말고 결과에서 배운 점으로 다른/더 나은 접근을 택해라(같은 코드를 다시 돌리는 것은 금지). 게임 코드는 `E:/StutterFixWork/decompiled/Assembly-CSharp/<타입>.cs` 디컴파일본으로 읽어라(근사, ilscan 으로 교차확인). 성공 조건(정규식) `DRAWCOLLAPSE=PASS` 이 아직 안 나왔다. 결과를 분석해 원인을 코드로 찾아 고치고, 안 되는 이유가 설계 한계면 결과 문서에 구체적으로 적고 멈춰라. 아래는 원래 임무다.


---
# Codex 지시서: 가시 장벽 구간(Barrier Span) 단일 draw 통합 — 1관문부터
먼저 `docs/handoff/codex-master-20261022.md` 와 `docs/handoff/gemini-drawcall-collapse-prompt.md`(제미나이 설계안, 아래 보정을 우선)를 읽어라.

## 보정 (Claude, 측정 근거)
1. 목표 기준: Arche 260~270초 GPU 소유 상태 **168 FPS(6.0ms)**. 장식 이동 이벤트를 뺀 정지 장식 맵은 같은 구간 **492~500 FPS(2.0ms)**. 차이 약 4ms 가 움직이는 장식이 만드는 프레임당 비용이다. "장식 그리기를 빼면 261" 은 틀린 한계값이니 쓰지 마라.
2. 텍스처는 약 100종이 아니라 **301종**(이전 실측, 해상도 제각각). `Texture2DArray` 를 전부 2048² RGBA32 로 만들면 약 5GB → VRAM 초과(8GB 카드, 맵이 이미 6GB). 배열은 **압축 포맷 그대로(DXT5 등 같은 포맷끼리 `CopyTexture` GPU 복사) + 해상도 티어별**로 만들고 메모리를 먼저 계산해 로그에 남겨라. 불가능하면 1관문 결과와 함께 중단 보고.
3. 이전 렌더 합치기(`codex-render-merge-result.md`)가 실패한 이유(서브메시 1,478개 → draw 3,815)를 반복하지 마라: 병합 렌더러는 **subMeshCount==1, 재질 1개**여야 한다.
4. 렌더 큐 자체: 같은 정렬 키 안에서는 재질 인스턴스 ID 오름차순이다(우리 실측). 병합 렌더러의 정렬 키를 구간 첫 장식의 `(sortingLayerID, sortingOrder, 월드 Z)` 로 줘서 가시 비소유 장벽과의 앞뒤만 보존.

## 1관문 (먼저, 게임 1회): 장벽 구간 수 실측 — 이 숫자가 나오기 전에 병합 코드를 짜지 마라
`gpudeco rendermerge ...` 계열이 쓰던 페인터 순서 정렬을 재사용해, Arche 260~270초 GPU 소유 상태(owned≥20,000)에서 로그 한 줄:
`DRAWCOLLAPSE_DIAG owned_visible=N barrier_visible=M active_barrier_spans=S same_tex_runs=R textures_in_owned=T array_mem_mb_dxt=X`
- 가시 비소유 장벽 = 정렬 순서에서 소유 장식 사이에 끼어 실제로 픽셀을 그리는 비소유 렌더러(enabled, !forceRenderingOff, 알파>0). 보이지 않는 것은 장벽이 아니다.
- 판정: **S ≤ 150** 이면 2관문(구현)으로 간다. **S > 400** 이면 이 방식은 draw 를 못 줄이므로 `DRAWCOLLAPSE=FAIL reason=too_many_barriers` 를 결과 문서 첫 줄에 쓰고 멈춰라(구현 금지).
- 시험 계획은 `tools/optimization/drawcollapse-steps.py`(증명된 순서: 곡 처음에서 준비 → seek 260 → live on → 10초 창). 필요하면 확장하되 `owned=23547` 확인.

## 2관문 (S≤150 일 때만): 구현과 ABBA
Step A(같은 텍스처 단일 subMesh) 는 건너뛰고 바로 Step B(Texture2DArray + 구간당 draw 1)를 시도해도 된다. 합격: 원본 대비 픽셀 `maxdiff ≤ 3`, 엔진 draw ≤ 300, 260~270초 FPS ≥ 220(기준 168), 30ms 이상 끊김 0, 3빌드+LoadCheck 통과, 설치 원본 복원. 결과 문서 첫 줄에 `DRAWCOLLAPSE=PASS` 또는 `FAIL` 과 숫자.
