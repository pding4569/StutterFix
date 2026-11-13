# 코덱스 작업: 타일 직접 그리기(인스턴싱) 가능성 조사와 상한 측정 도구 (2026-11-13)

작업 폴더 `E:\StutterFixWork\wt-v14prod` (claude/adofai-renderer). 커밋·푸시·게임 실행·설치 DLL 교체 금지. 한국어 보고. 결과 `docs/handoff/tilerender-20261113-result.md`.

## 배경 (지금까지 확인된 것, 다시 하지 말 것)
- 3440 렌더에서 HELLO·Exit·Arche 는 메인 스레드 병목. 타일 렌더러를 숨기기만 해도(스크립트는 그대로) HELLO +12.5%, Exit +14% (메모 11-10 bottleneck map). 무손실 후보 1순위로 남아 있었다.
- 엔진 쪽 비용: PlayerRender 31%, RenderManager::UpdateAllRenderers 4.5%, CullScene/ExtractSceneRenderNodeQueue 등 렌더러 수에 비례하는 일(nat-hl.resolved.txt, tools/analysis/stacktree.py).
- 실패한 길(반복 금지): 타일 굽기(애니메이션 미리 계산, LR 지원 0%: 트윈·나타남/사라짐·색 바꿈), 타일 색 MaterialPropertyBlock, 같은 색 건너뛰기(같은 값 1~11%), FloorFix(같은 스타일 건너뛰기 → 색 깨짐, CLAUDE.md "지운 것").
- 타일 메시 모양은 `MeshWarm`(재생 준비 때 같은 키로 미리 만듦)·`FloorMesh` 키로 종류가 제한적일 가능성이 크다. 타일 애니메이션은 우리 `FloorAnim` 이 직접 돌린다(쓰기 지점을 우리가 안다).
- 장식과 타일이 그리기 순서에서 섞이는지가 관건: GpuDeco 계획 로그에 HELLO `MeshRenderer:floorMeshLong(Clone) [활성]=13457` 이 장벽으로 나온다.

## 할 일 1: 조사 명령 `tilerender census` (DEV||AUTOTEST 전용, 일시정지 한 프레임)
메인 카메라가 그리는 타일(scrFloor 소유 MeshRenderer 등, 실제 타일 렌더러 종류를 코드에서 확인) 에 대해 로그:
- 켜진 타일 렌더러 수, 화면 안(카메라 컬링 통과) 수, 서로 다른 sharedMesh 수(상위 10개 메시와 개수), 서로 다른 셰이더·재질 키워드·렌더 큐, 재질 인스턴스 수와 "내용 같은 재질" 묶음 수(텍스처·색·키워드·속성 값 해시).
- 정렬: 타일의 sortingLayer/order 범위와, 같은 범위 안에 끼어드는 장식·행성·기타 렌더러 수(그리기 순서에서 타일 사이에 다른 것이 몇 번 끼는지 = 연속 구간 수).
- 프레임 간 변화: 연속 30프레임 동안 타일 transform(localToWorldMatrix)·재질 색/속성이 바뀐 타일 수 평균(곡 재생 중, pause 전에 측정하도록 명령을 `tilerender census` 가 30프레임 돈 뒤 로그).
## 할 일 2: 시제품 `tilerender on|off` (DEV||AUTOTEST 전용, 기본 꺼짐)
연속 구간이 적게 나오는 맵(조사 결과로 판단)에서: 타일 렌더러 forceRenderingOff + 같은 메시·같은 재질 내용끼리 `Graphics.RenderMeshInstanced`/CommandBuffer DrawMeshInstanced(또는 Indirect)로 그리기. 그리기 순서 보존이 정확히 되는 경우(타일 구간에 끼어드는 것이 없거나 구간마다 카메라 CommandBuffer 지점으로 나눌 수 있는 경우)만 켠다. 셰이더가 인스턴싱을 지원하지 않으면 우리 쪽 셰이더 사본(같은 계산, 인스턴스 속성으로 색/행렬)을 기존 번들 방식으로 추가. 매 프레임 비용: 바뀐 타일만 갱신(FloorAnim·색 바꾸기 쓰기 지점에 표시), 할당 0.
검증 도구: 기존 `framecmp grab/diff` 로 켬/끔 같은 일시정지 프레임 픽셀 차이 0 을 Claude 가 확인할 수 있게 명령 순서를 `E:\StutterFixWork\tilerender_run.py` 로 준비(맵 hl 50초, sh 88초, wf 260초, s7 120초; bench_v14 CFG2["rlNR"]; census → framecmp grab a → tilerender on → wait 1 → grab b → diff → off). FPS ABBA 는 bench_v14 에 CFG2 항목(`trOn` = rlNR + BENCH_MID "tilerender on")을 추가.
## 막히면
틀에 얽매이지 말 것: 예) 타일을 메시 하나로 합치기(정적 구간), CullingGroup 으로 Unity 컬링만 줄이기, 타일 렌더러를 렌더 레이어로 빼서 별도 카메라로. 확인/미확인을 구분해 문서화. 실행·커밋은 Claude.
