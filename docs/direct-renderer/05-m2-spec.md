# 코덱스 작업: 직통 렌더러 M2 — 경계 없는 완전 소유(메인 카메라 반투명 전체를 우리 차례로) (2026-11-15)

작업 폴더 `E:\StutterFixWork\wt-v14prod`. 커밋·게임 실행·설치 DLL 교체 금지. 한국어 보고. 결과: `docs/direct-renderer/05-m2-result.md`.
앞 단계: `03-m1b-result.md`, `04-m1c-result.md`. 실게임 로그: `E:\StutterFixWork\night-results\dp6-{hl50,wf,dd,sh}-drPix.log`.

## 왜 바꾸나 (M1c 실게임)
| 맵 | 맡은 장식 / 전체 | 상태 |
|---|---|---|
| HELLO | 17 / 2531 | 배경 쪽 1535, 앞쪽 490 이 "순서 경계"로 거부. canvasBounds=14923 |
| Windflower | 55 → 곡 중 경계 변화 23번으로 **55개 모두 released** | 경계 연쇄(놓친 장식이 다시 경계가 됨) |
| DD | 46 / 6314 | canvasBounds=5500 |
| Exit | 활성 안 됨 | |
- 경계 사유: `MeshRenderer:Legacy Shaders/Diffuse passes=3`(10개), `Hidden/BlendModes/SpritesDefault/Grab passes=2`, 활성 캔버스 수천 개, 마스크 대상 남는 것.
- verify 는 통과(오차 1e-3 이하), 화면 차이도 맡은 범위에선 거의 0. **문제는 순서 모델**: 우리 묶음을 BeforeForwardAlpha/AfterForwardAlpha 두 지점에만 넣고 나머지는 Unity 가 그리게 하니, 그 사이에 끼는 Unity 렌더러가 경계가 되어 맡는 범위가 무너진다(GpuDeco·deco-v2 와 같은 벽).

## 새 원칙: 경계 0
메인 카메라가 그리는 **반투명 렌더러 전부**(정렬 층 Bg 부터 맨 위까지, 타일·행성·파티클·글자·남는 장식 포함)를 우리 차례(sequence) 하나로 그린다.
- 맡은 이미지 장식 = 네이티브 인스턴스 구간.
- 그 밖의 모든 렌더러 = 같은 차례 안에서 `CommandBuffer.DrawRenderer(r, mat, submesh, -1)`(모든 패스). Unity 의 원래 그리기는 숨김 층(카메라 cullingMask 에서 뺀 층)으로 옮기거나 forceRenderingOff 로 막는다. 원래 정렬 키 순서(정렬 층 → 순서 → 큐 → 거리 → 셰이더 ID → 재질 ID, `SceneRenderOrder`)를 그대로 따른다.
- 다중 패스(Legacy Diffuse 등), GrabPass 셰이더: DrawRenderer 로 그려 보고 안 되는 것만 따로(Grab 은 M1b 의 네이티브 화면 복사 경로로, 또는 구간 경계에서 camRT 를 임시 RT 로 복사해 `_GrabTexture` 류 이름에 바인딩).
- 월드 캔버스: 먼저 **실제로 무엇이 그려지는지** 조사(14923 개의 정체: 타일 번호? 판정 글자? 비활성 그래픽만 가진 캔버스? `Canvas.isActiveAndEnabled`, 자식 Graphic 의 enabled/알파, `CanvasRenderer.cull`, 화면 안 여부). 그려지는 것이 있으면 그 캔버스들만 경계로 남기되(캔버스는 DrawRenderer 불가), 대부분이 실제로 안 그려지면 경계에서 뺀다. 가능하면 캔버스를 별도 카메라/RT 로 그려 차례 안에서 합성하는 방법도 검토.
- 마스크: 우리 차례가 스텐실 전체를 소유하므로 Unity 마스크(SpriteMask)도 우리 차례 안에서 들어감/나옴 그리기로 재현하고, 마스크 대상 남는 렌더러도 DrawRenderer 전에 같은 스텐실 상태가 적용되도록(필요하면 대상 렌더러를 우리 스텐실 시험 재질로 그림). 어렵다면 1단계에서는 마스크·대상이 있는 정렬 범위만 Unity 에 남기되 그 범위를 Unity 가 "한 덩어리로" 그리게 할 방법(정렬 층 단위 분리 카메라 등)을 조사.
- 켜짐/꺼짐이 곡 중 바뀌는 렌더러: 차례는 고정 목록 + 켜짐 비트(매 프레임 전수 확인 금지, 후크/더티 표시). 비활성 풀(판정 글자 등)은 꺼낼 때 후크로.
- 행성·타일 등 매 프레임 바뀌는 것도 DrawRenderer 는 현재 Transform/재질을 그대로 쓰므로 정확하다(그리기 시점의 상태). 정렬 키가 바뀌는 것(SetDepth, 타일 sortingOrder 변경)은 후크로 차례 재정렬.
- 이렇게 하면 "경계로 놓침 → 연쇄" 가 없어진다. M1c 의 boundary/released 로직은 제거하거나 진단용으로만.

## 비용 원칙
- 메인 스레드: 차례 재구성은 더티일 때만, 평소 프레임 < 50µs(직통 장식 부분) + DrawRenderer 명령 기록은 CommandBuffer 재사용(바뀔 때만 다시 기록). 할당 0.
- DrawRenderer 개수가 많아도(수백~수천) Unity 렌더 스레드 비용이 원래 그리기와 비슷해야 한다. 측정 지표를 state 에 넣어라(DrawRenderer 수, 재기록 횟수, 기록 시간).

## 검증
- `dr2 verify` 유지. 새로 `dr2 seqcheck`: 우리 차례 순서와 Unity 원래 정렬 키 순서가 같은지(전수) 검사.
- 4종 빌드 오류 0, export 보존. WARP/오프라인 테스트 가능 범위.
- Claude 가 돌릴 bench: drPix(정상 모드 픽셀 대조, 목표 다른 픽셀 < 0.1%, 최대 채널 ≤ 3), drOn vs ppGpuCam ABBA(FPS, 메인/렌더 스레드).
