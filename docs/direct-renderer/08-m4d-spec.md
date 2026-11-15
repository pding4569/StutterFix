# 코덱스 작업: M4d — "남는 섬 0" (캔버스 단위 보존 + 보조 카메라 합성 + 마스크 공유) (2026-11-15)

작업 폴더 `E:\StutterFixWork\wt-m4` (브랜치 claude/drfull). 커밋·게임 실행·설치 DLL 교체 금지. 한국어 보고. 결과: `docs/direct-renderer/08-m4d-result.md`.
앞 문서: `07-m4-spec.md`, `07-m4-result.md`. 사용자 목표: 원본보다 **압도적으로** 빠르게, 화면은 픽셀 대조로 같게.

## M4c 실게임 결과 (Claude, 2026-11-15, 3440 렌더)
- HELLO 50초 / Windflower 258초 / DD 50초 / Exit(sh) 88초 **네 맵 모두** `drfull on` 이 같은 예외로 거부:
  `retained Unity masks cannot share native stencil` (DirectFull.Render.cs:626).
- 원인(코드 읽기): 활성화 때 `Resources.FindObjectsOfTypeAll<Canvas>()` 로 **비활성 포함** 월드/카메라 캔버스를 찾고, 그 캔버스·자식 CanvasRenderer 가 있는 **층 전체**를 removedMask 에서 뺀다. SortingGroup 도 층 전체. 그 결과 맵 장식·마스크가 있는 층(예: 0/9)이 통째로 retained 가 되어 마스크 검사에 걸린다. 마스크를 고쳐도 다음은 `order intersects retained Unity interval` 에 걸릴 구조다(M1~M3 와 같은 "섬/경계" 벽).
- M4a 상한(맵 층을 카메라에서 빼기만): HELLO +62%, WF +77%, DD +46%(364.8 vs 251.6/245.6), sh +39%. 스크립트 합계는 상한에서도 ~1.0ms/프레임으로 변하지 않음(DD LateUpdate 이상은 재현 안 됨, 무시).

## 목표 구조
1. **층 단위 보존 금지.** 맵 층은 항상 메인 카메라에서 뺀다. Unity 에 남겨야 하는 것은 **개별 오브젝트**만, 전용 빈 층(사용 안 하는 층 번호를 찾아서, 예 31)으로 그 오브젝트(와 자식)만 옮겨 보조 카메라가 그리게 한다. 옮기기 전 콜라이더/Rigidbody/물리 층 의존이 있으면 옮기지 말고 사유 기록(캔버스는 보통 없음). off/재시작/맵 전환/Unload 에서 층 원복(원래 층 값 보관).
2. **비활성 캔버스·비활성 렌더러**: 활성화 때 전수 보존하지 말 것. 지금 실제로 그리는 것(활성·enabled·그래픽 있음·알파>0 근사)만 대상. 나중에 켜지는 것은 수명 컴포넌트(OnEnable)로 알림 → 그 프레임 재계획(할당 0, 드물면 됨). 몇 개가 실제 활성인지 census 에 기록(HELLO canvasBounds=14923 의 정체를 이번에 숫자로 밝혀라: 활성/비활성/그래픽 수).
3. **우리가 못 그리는 것(캔버스, 파티클, TMP, 미지원 셰이더)**:
   - DrawRenderer 가 가능한 Renderer(파티클·TMP MeshRenderer·미지원 셰이더)는 지금처럼 **우리 차례 안에서 `cb.DrawRenderer`** (원래 층에 있어도 됨, 카메라 mask 와 무관).
   - CanvasRenderer(DrawRenderer 불가)는 **보조 카메라 합성**: 같은 투영/위치/RT 크기의 보조 카메라(메인보다 먼저 렌더, cullingMask = 전용 층만)가 정렬 구간별 RT 에 그리고, 우리 차례의 그 캔버스 자리에서 선곱 알파로 합성. 정렬 구간이 여러 개면 연속한 캔버스끼리 묶어 RT 수 최소화(보조 카메라 한 대가 여러 번 그리기: 구간마다 mask 층을 바꿔 Render() 하거나 층 여러 개 사용). 합성 블렌드가 원래 캔버스 블렌드(보통 SrcAlpha/OneMinusSrcAlpha)와 수학적으로 같은지(투명 RT 에 그린 뒤 합성 = 직접 그리기) 확인하고, 다르면 그 캔버스는 사유 기록.
   - 화면에 실제로 보이는 캔버스가 0 이면 보조 카메라를 켜지 않는다(비용 0).
4. **마스크 공유**: 우리 네이티브 마스크 그리기와 DrawRenderer 로 그리는 Unity 마스크 대상(maskInteraction != None)이 같은 camRT 깊이/스텐실을 쓰므로, 우리 스텐실 값 규칙을 Unity SpriteMask 규칙과 같게 맞춰라(Unity 내장 Sprites/Default 의 Stencil Ref/Comp, SpriteMask 의 증감 방식 — 원래 DXBC 패스 상태/`Sprites/Mask` 에서 읽은 값 기준). 그러면 섞여도 맞물린다. SpriteMask 자체도 네이티브(이미 구현). SortingGroup 은 그룹 키로 평탄화(외부 키 → 내부 키 순, Unity 규칙)해서 차례에 넣고, 못 하면 그 그룹만 DrawRenderer.
5. **거부 방식 변경**: 첫 예외에서 멈추지 말 것. 활성화 때 모든 렌더러/캔버스를 분류하고 사유별 개수를 로그(`drfull plan` 명령: 켜지 않고 분류만 출력, 맵마다 1회). 켜기 자체는 "그 렌더러를 DrawRenderer/보조 카메라로 넘김"으로 처리하고, 정말 불가능한 것만 사유 기록 + 그 오브젝트만 원래 Unity 경로(전용 층이 아닌 원래 층 유지 = 메인 카메라가 그 층을 다시 포함해야 하므로 이것은 마지막 수단, 개수 로그).

## 성능 원칙 (그대로)
- 매 프레임 할당 0, 메인 스레드 직통 비용 < 0.3ms, 재계획은 드물게(곡 중 commit_max < 2ms 목표, state 에 replans·commit_max 기록).
- 보조 카메라 비용도 state 에 (RT 수, 보조 그리기 수, 시간).

## 검증
- 4종 빌드 오류 0, export 보존(`sf_z_drfull_v1` 포함 기존 export 그대로).
- 오프라인: 투명 RT 그리기→선곱 합성 = 직접 그리기 대조 fixture(WARP), 스텐실 규칙 fixture(네이티브 마스크 + Unity 방식 대상).
- Claude 가 잴 것: `drfull plan`(4맵), drPix(pause→on d1→off d0→diff, 목표 다른 픽셀 < 0.1%·최대 채널 ≤ 3, off/off 잡음 0), ABBA FPS. 계획 JSON 은 `tools/direct-renderer/plan-m4c.py` 를 고쳐 `out/direct-renderer/m4d/plans/` 에 (plan 판 4개 + pixel 4개 + ABBA 16개). 기존 plan 생성 규칙(seek, waitsongpos, fpsmark 구간) 유지.
- 막히면 비정상적인 아이디어도 빠르게 시도. 무엇이 실측 미확인인지 결과에 구분.
