# 자동 반복 2회차

먼저 `docs/handoff/codex-master-20261022.md`(마스터 인계문서)를 읽어라.

직전 게임 시험 결과(자동 수집): `docs/handoff/dc-autoloop-run1-evidence.txt`. 먼저 `docs/handoff/dc-autoloop-ledger.md` 장부를 읽고, 이미 실패한 접근은 되풀이하지 말고 결과에서 배운 점으로 다른/더 나은 접근을 택해라(같은 코드를 다시 돌리는 것은 금지). 게임 코드는 `E:/StutterFixWork/decompiled/Assembly-CSharp/<타입>.cs` 디컴파일본으로 읽어라(근사, ilscan 으로 교차확인). 성공 조건(정규식) `DRAWCOLLAPSE=PASS` 이 아직 안 나왔다. 결과를 분석해 원인을 코드로 찾아 고치고, 안 되는 이유가 설계 한계면 결과 문서에 구체적으로 적고 멈춰라. 아래는 원래 임무다.


---
# 자동 반복 4회차

먼저 `docs/handoff/codex-master-20261022.md`(마스터 인계문서)를 읽어라.

직전 게임 시험 결과(자동 수집): `docs/handoff/dc-autoloop-run3-evidence.txt`. 먼저 `docs/handoff/dc-autoloop-ledger.md` 장부를 읽고, 이미 실패한 접근은 되풀이하지 말고 결과에서 배운 점으로 다른/더 나은 접근을 택해라(같은 코드를 다시 돌리는 것은 금지). 게임 코드는 `E:/StutterFixWork/decompiled/Assembly-CSharp/<타입>.cs` 디컴파일본으로 읽어라(근사, ilscan 으로 교차확인). 성공 조건(정규식) `DRAWCOLLAPSE=PASS` 이 아직 안 나왔다. 결과를 분석해 원인을 코드로 찾아 고치고, 안 되는 이유가 설계 한계면 결과 문서에 구체적으로 적고 멈춰라. 아래는 원래 임무다.


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

## 2026-10-25 10:00 Claude 결정: 1관문은 통과로 확정한다. 이제 구현하라 (HOLD 로 더 머물지 말 것)
실측(al-dc-2): `owned_visible=1674 barrier_visible=327 active_barrier_spans=10`(후보 최대 18), `textures_in_owned=81`, 텍스처 배열 메모리 약 615MB(VRAM 7.9GB). 판정 기준(S≤150)을 넘었다. 픽셀·정렬 증명은 **진단이 아니라 구현을 해서 원본과 비교해야** 나오는 것이므로 더 이상 진단 회전을 돌리지 마라. 이번 회전부터 2관문을 구현한다.
구현 범위(최소부터): (1) 구간 대표 `MeshRenderer`(subMeshCount==1, 재질 1개, `Texture2DArray` 셰이더)를 맵 열 때 한 번 만들어 둔다(구간 수 ≈ 10~20). (2) 정점 슬롯은 원래 정렬 키 오름차순으로 배치해 구간 안 페인터 순서를 삼각형 나열 순서로 보존. (3) Evaluate 커널이 대표 메시 Raw 정점 버퍼에 직접 쓴다(복사 dispatch 제거). (4) 기존 개별 렌더러는 숨김(`forceRenderingOff` 또는 enabled=false, 알파0 소유 장식은 퇴화 삼각형). (5) 켜기/끄기는 `gpudeco drawcollapse on|off`(기본 끔, 연구 빌드만), 끄면 즉시 원래 개별 렌더러로 복구.
시험: 260/262/270초 정지 프레임 픽셀 비교(원본 대비 maxdiff ≤ 3), 260~270초 ABBA FPS·엔진 draw 수. 구현이 안 되는 구체적 이유(셰이더 컴파일, 번들 문제 등)가 있으면 그 에러를 장부에 그대로 적고 가장 가까운 단계까지 진행하라. 추측으로 HOLD 를 되풀이하는 것이 가장 나쁜 결과다.

## 2026-10-25 10:10 Claude 설계 보조: 텍스처 티어가 35개라 "배열 하나"는 불가능 — 이렇게 줄여라 (측정 근거: al-dc-3 DRAWCOLLAPSE_ARRAY)
소유 장식이 쓰는 81종 텍스처는 크기·포맷이 35가지다(포맷 DXT5/RGBA32/RGB24/ARGB32 혼재, 100x100 부터 14024x3506 까지). `Texture2DArray` 는 슬라이스 크기·포맷이 같아야 하므로 **배열 하나로는 못 쓴다**. 현실적인 방법:
1. **작은 DXT5 는 크기 등급으로 묶는다**: ≤512², ≤1024², ≤2048² 세 등급의 DXT5 배열을 만들고 슬라이스 좌하단 영역에 `Graphics.CopyTexture(src,0,0,0,0,w,h,dst,slice,0,0,0)`(압축 포맷은 4x4 블록 정렬 필요 — w,h 가 4 배수가 아니면 패딩 필요, 실패하면 그 텍스처는 단독 처리) 로 GPU 복사, UV 는 `(w/배열폭, h/배열높이)` 로 축소. 패딩 낭비는 등급이 3개라 작다(2048² 등급 기준 슬라이스당 4MB DXT5... 총 수십 MB).
2. **큰 텍스처(≥2500², 또는 DXT5 가 아닌 RGBA32/RGB24/ARGB32)는 배열에 넣지 않는다**. 이것들은 개수가 적고(여러 해 모양 큰 배경류) 이미 단독 `Texture2D` 로 있다. 셰이더에 `_Big0.._Big7` 같은 `Texture2D` 슬롯 8개를 두고 정점의 tier id 로 분기한다. 이때 분기 안에서 `SampleLevel` 이 아니라 **`SampleGrad` + 분기 밖에서 구한 ddx/ddy** 를 써서 그래디언트 불연속(경계 줄무늬)을 피한다. 포맷이 비압축인 것은 `CopyTexture` 로 합칠 수 없고(DXT5 배열과 포맷 불일치), 합치려면 `Blit` + 메모리 큰 비압축 배열이 필요해 5GB 문제가 되니 합치지 마라.
3. 등급이 `3 + 8 = 11` 텍스처 슬롯 정도면 D3D11 슬롯(128)에 여유가 있다. 한 구간(span)의 모든 장식이 한 draw 로 그려지고 셰이더가 tier id 로 고른다.
4. 합산 메모리와 슬롯 수를 `DRAWCOLLAPSE_ARRAY_TOTAL` 줄에 다시 적어라(지금 615MB 는 35개 개별 티어 합산이라 등급화하면 달라진다).
