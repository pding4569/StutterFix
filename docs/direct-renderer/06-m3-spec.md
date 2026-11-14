# 코덱스 작업: 직통 렌더러 M3 — 순서를 Unity 정렬에 맡기는 대리 렌더러 방식 (2026-11-15)

작업 폴더 `E:\StutterFixWork\wt-v14prod`. 커밋·게임 실행·설치 DLL 교체 금지. 한국어 보고. 결과: `docs/direct-renderer/06-m3-result.md`.

## M2 실게임 결과 (dp7, 3440 렌더, 정상 모드)
| 맵 | 맡은 장식 | 결과 |
|---|---|---|
| HELLO 50초 | 19 / 2531 (거부: 배경 쪽 남는 것 위 1535, 앞쪽 아래 488) | 3프레임 만에 **프레임 오류로 꺼짐**: `NullReferenceException at GameObject.set_layer ← DirectDeco.RetireSequence ← ApplyBoundaries ← Commit ← PreCull`. 준비 3.2초, commit_max 47ms |
| Windflower | 155 / 6290 | 꺼짐(같은 계열) |
| DD | 572 / 6314 (앞쪽 아래 4821) | 꺼짐, 픽셀 차이 0.013% |
| Exit | 42 / 3524 | 꺼짐 |
- "끼워 그리는 렌더러" 2,613~12,927개를 DrawRenderer 로 차례에 넣으려니 준비·재기록 비용이 크고, 여전히 순서 경계 때문에 거의 못 맡는다. 플러그인 이벤트를 카메라 이벤트 두 지점에만 넣는 구조로는 Unity 의 반투명 큐 중간에 끼어들 수 없다는 것이 근본 원인.

## 새 구조: 대리 렌더러(proxy) 정렬
- 맡은 장식을 정렬 키 순서로 묶어 "연속 구간"(그 사이에 맡지 않은 렌더러가 끼지 않는 묶음, 그리고 같은 텍스처·블렌드·셰이더 변형)마다 **대리 MeshRenderer 하나**를 만든다. 대리의 sortingLayer/sortingOrder/위치(거리)를 구간 첫 장식과 같게 두고 Unity 가 다른 렌더러와 함께 정렬하게 한다. → **경계 개념이 사라진다**: 맡지 않은 렌더러는 그냥 Unity 가 원래대로 그린다.
- 대리의 메시 = 구간 장식 수 × 사각형(또는 빈 칸 뺀 도형) 정점, 정점 셰이더가 SV_VertexID 로 전역 버퍼(채널 16·정적 32·슬롯 목록)를 읽어 위치·UV·색을 계산(지금 dr2.hlsl 식 그대로). 버퍼는 네이티브가 바뀐 슬롯만 올린다(지금 dr2 의 채널·정적 업로드 재사용, Unity GraphicsBuffer 의 네이티브 포인터에 직접 쓰기 또는 SetData 부분 갱신).
- 셰이더는 Unity 셰이더 에셋이어야 한다(재질로 쓰려면). 기존 번들 빌드 방식(`tools/gpudeco-lab`, effects 번들, `GpuDecoBatch.shader`)을 따라 `StutterFix/DirectDeco` 셰이더를 번들로 만들고, 블렌드(선곱·더하기)·점 샘플링·스텐실 마스크(Unity SpriteMask 와 같은 Stencil 규칙: maskInteraction 대응 Ref/Comp)를 재질 속성/키워드로. 스텐실을 Unity 규칙대로 쓰면 맡지 않은 SpriteMask 와도 그대로 맞물린다.
- 동점(같은 정렬 층·순서·거리)의 Unity 순서 규칙은 셰이더 ID → 재질 ID(`SceneRenderOrder`, 메모 unity-tie-sort-rule). 대리는 새 재질이라 동점 묶음에서 순서가 바뀔 수 있다 → GpuDeco 의 해결책(거리 아주 조금 보정 `NudgeTieSpans`/`TieNudge`, `GpuDecoDrawCollapseRenderer.cs`)을 재사용하거나, 동점 묶음 안의 장식은 대리로 묶지 않고 남긴다.
- 대리 bounds 는 구간 장식들의 화면 범위 합(컬링 정확). 구간 안 장식이 모두 안 보이면 대리 렌더러 끔.
- 장식 쪽은 지금처럼 Unity 장식 오브젝트·스크립트는 그대로 두되 원래 렌더러는 forceRenderingOff, Transform 쓰기(TightLate/LogicUpdate)는 건너뛰고 필드만 읽기(지금 Poll).
- M1c/M2 의 경계·DrawRenderer·RetireSequence·숨김 층 코드는 이 방식에서 필요 없으면 제거(진단용만 남김). 위 NullReferenceException 은 원인만 기록.

## 성능 목표
- 준비(재생 준비 안에서) < 300ms(HELLO 기준), 곡 중 commit_max < 2ms, 평소 메인 스레드 직통 비용 < 50µs/프레임 + 대리 렌더러 수에 비례하는 Unity 비용(구간 수를 로그).
- 맡는 비율 목표: 이미지 장식(Sprites/Tile·더하기·Grab 블렌드·마스크/마스크 대상) 대부분. 남는 이유를 사유별로 로그.

## 검증
- `dr2 verify`(식 대조), `dr2 seqcheck`(대리 구간 순서가 원래 정렬 키와 같은지, 동점 묶음 경고), 4종 빌드 오류 0, export 보존.
- Claude 가 돌릴 것: drPix(정상 모드 픽셀 대조, 목표 다른 픽셀 < 0.1%·최대 채널 ≤ 3), drOn vs ppGpuCam ABBA.
