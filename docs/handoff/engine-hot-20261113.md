# 코덱스 작업: 엔진(UnityPlayer) 메인 스레드 고정비 — 네이티브 패치 후보 찾기 (2026-11-13)

작업 폴더 `E:\StutterFixWork\wt-v14prod` (claude/adofai-renderer). 커밋·푸시·게임 실행·설치 DLL 교체 금지. 한국어 보고. 결과 `docs/handoff/engine-hot-20261113-result.md`.

## 배경
StutterFix 는 이미 `sfnative.dll` 로 실행 중 엔진 바이트를 고친다(화면 대기 건너뛰기 WaitSkip +14%, 마스크 노드 배열 MaskGrow Exit +11.8%). 근거 문서: docs/render-system/06-engine-internals.md, docs/deco-spec/results/engine-extreme.md. 이 두 문서를 먼저 읽고 끝난 조사는 반복하지 않는다.
3440 렌더에서 HELLO·Exit·Arche 는 메인 스레드 병목이다. HELLO 네이티브 포함 표본(E:\StutterFixWork\night-results\nat-hl.resolved.txt, nat-sh.resolved.txt, 집계 docs/handoff/hl-stacktree-20261113.txt / sh-...)에서 스크립트 밖 엔진 함수(포함%):
PlayerRender 22.2, RenderManager::RenderOffscreenCameras 18.6, Camera::CustomRender 15.7, **ujob_wait_for 10.9**(메인이 작업 스레드를 기다림), ujob_execute_job 8.3, CullResults::GetOrCreateSharedRendererScene 4.8, ExtractSceneRenderNodeQueue 4.65, RendererUpdateManager::UpdateAll 4.57, RenderManager::UpdateAllRenderers 4.52, TransformChangeDispatch::ScanAndHandleChangesJob 3.65, Camera::CustomCull 3.5, MeshRenderer::FinalizeUpdate 2.04, SyncFence 2.04.
엔진 6000.3.25f1, PDB `E:\StutterFixWork\symbols\UnityPlayer_Win64_player_mono_x64.pdb\0BD6084081424C169079D97BFE0214691\`, 심볼 도구 tools/symrva(SymRva.exe), 디스어셈블은 dumpbin/자체 도구.

## 할 일
1. 위 엔진 함수마다 표본 안쪽(자식) 분포를 resolved 파일에서 뽑아, 무엇을 몇 번 하는지(렌더러 수·카메라 수·작업 수에 비례하는지) 정리한다. 특히:
   - ujob_wait_for: 메인이 어떤 작업(컬링? 정렬? 변환 디스패치? 렌더 노드 추출?)을 기다리는지 호출자별 분포. 작업을 메인이 직접 돕는지(work stealing) 여부.
   - RenderOffscreenCameras: 카메라가 몇 개 그려지는지(게임: Camera, BGStaticCam, BGMovingCam 등)와 카메라마다 컬링/노드 추출을 다시 하는지.
   - TransformChangeDispatch: 어떤 시스템이 변경을 구독하는지(렌더러·물리·UI)와 이 게임에서 불필요한 구독(예: 물리 콜라이더 없는 장식).
2. 이 게임에 한해 결과가 같은데 줄일 수 있는 엔진 일을 찾는다(예: 렌더러가 하나도 없는 레이어만 그리는 카메라의 컬링, 쓰지 않는 TransformChange 구독, 작업 스레드 수/분할 크기). 각 후보에 불변 조건·위험·예상 ms.
3. 엔진 바이트 패치 없이 되는 것(설정·API: 카메라 cullingMask, Camera.useOcclusionCulling, 렌더러 정적 플래그, `JobsUtility.JobWorkerCount`, `-job-worker-count` 부팅 옵션, Physics.autoSyncTransforms 등)을 먼저, 바이트 패치가 필요한 것은 그 다음으로. 상한 측정용 AUTOTEST 명령(예: `engknob <이름> on|off`)을 만들고 bench_v14.py CFG2 에 rlNR 기반 항목을 추가한다(파일 E:\StutterFixWork\bench_v14.py, 기존 CFG_MID 방식).
4. 실행은 Claude. 무엇을 확인/미확인인지 구분해서 기록.

## 막히면
틀에 얽매이지 말 것(예: 렌더 스레드/작업 스레드 우선순위, 메인 스레드의 작업 대기 대신 다른 일 하기, 카메라 합치기). 메모: JALib 이 있는 실행은 Harmony 가 깨져 35 FPS 가 되는 일이 있다(bench_v14 가 거름).
