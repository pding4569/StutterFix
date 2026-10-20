# Codex 임무 gpusplit-2: 이미지 장식이 GPU 의 절반 — 무엇이 비싼지 쪼개고, 화면 같은 절감 후보 찾기

2026-10-21. 작업 폴더 `E:\StutterFixWork\wt-archegpu` (브랜치 `codex/night-archegpu`, gpusplit 커밋 위). 게임 실행·설치·커밋 금지(Claude 가 한다). 결과 `docs/handoff/codex-gpusplit-2-20261021-result.md` + `-o`(10줄).

## 실측 (같은 곡 구간 Arche 262~266초를 그룹마다 다시 재생, 본 카메라 GPU 경과, 2560×1440 120Hz, 드리프트 0.005ms)
기준 **5.73ms**. 숨기면 줄어드는 몫: **deco-image +2.93ms (51%)**, tiles +0.11, planets −0.05, other −0.95(표본 34, 해석 보류), all +2.62. renderer 수(census): 이미지 86,484 · 타일 8,283 · 행성 322 · 기타 61,893 (`docs/handoff/night-results-20261021/gpusplit2.md`, `docs/optimization-36.md` 끝). 즉 GPU 의 절반이 이미지 장식이다. 목표(트레이스로 CPU 를 없앤 뒤 GPU 바닥)를 낮추려면 이 몫을 줄여야 한다.

## 알고 싶은 것
이 2.93ms 가 (a) draw call/상태 변경 수(작은 스프라이트 많음), (b) 정점/삼각형 수, (c) 픽셀 채우기(큰 쿼드의 투명 가장자리 포함 겹쳐 그리기), (d) 알파 블렌딩/애디티브 비용, (e) 화면 밖/완전 투명 장식의 헛제출 중 무엇인가.

## 해야 할 것
1. **gpusplit 하위 그룹**: `gpusplit hide deco-image:<필터>` 필터 추가 — `onscreen`/`offscreen`(렌더러 bounds 가 본 카메라 frustum 과 겹침 여부), `big`/`small`(화면 투영 면적이 전체 화면의 N% 초과/이하; N 을 인자로, 기본 1%), `additive`/`alpha`(재질/셰이더 블렌드 모드 — `Sprites/Tile` 셰이더 키워드/`material.GetFloat("_BlendOp"...)` 등 코드로 확인해 분류), `transparent`(현재 색 알파가 0 이하인데 렌더러가 켜져 있는 것). 필터 조합 가능. census 에 필터별 개수 로그. 복원은 기존 gpusplit 과 동일(`forceRenderingOff` 최초 값 복원).
2. **그리기 카운터**: 창마다 `Recorder`(batches/SetPass/삼각형/정점 수; 이미 있는 `FreeFpsProbe` 의 Recorder 사용법 참고, 개발 전용)로 본 카메라 한 프레임 평균 draw call·SetPass·삼각형·정점 수를 `gpuprobe state` 와 같은 창에 로그. 숨긴 그룹별 감소량이 GPU ms 감소와 비례하는지 볼 수 있게.
3. **스프라이트 메시 타이트니스 조사(오프라인 + 코드)**: 장식 스프라이트가 어떻게 만들어지는지(`scrDecoration.GetOrAddSprite`/`TextureManager` 의 `Sprite.Create` 인자: `SpriteMeshType.FullRect` vs `Tight`, extrude, pivot) 코드로 확정. FullRect 이면 투명 여백까지 쿼드가 덮는다. Arche 의 이미지(`D:\얼불춤 맵 파일\Arche\` 하위, 정확한 경로는 맵 파일의 `decorationImage` 로 찾는다)를 읽는 **읽기 전용** 스크립트로 이미지별 "알파 0 이 아닌 픽셀의 경계 상자 / 전체" 비율을 구하고, 장식 인스턴스 수로 가중해 **Tight 메시로 바꿀 때 줄어드는 채우기 면적 비율(%)**을 추정. Tight 는 투명 영역만 줄이므로 화면 동일(알파 0 영역은 원래 안 보임)이 기대되지만, 윤곽 정점이 늘면 정점 비용·정렬/배칭 영향이 있다 → 둘 다 숫자로. **구현은 하지 않는다**(결과에 구현 계획만).
4. **화면 같은 절감 후보 목록**(근거 포함): 예) 완전 투명 렌더러가 켜진 채 제출되는지, 화면 밖인데 Unity 컬링이 안 되는 경우, 거대한 쿼드(화면 몇 배)의 클리핑, 겹침 심한 영역. 각 후보에 필요한 측정을 `sf_run` steps 로(숨김 A/B 같은 구간 반복 방식 — 구간이 다른 창끼리 비교하지 말 것, 기준 창을 앞뒤에).
5. 빌드 3종(`-c Release`; `-p:Edition=Player`; `-p:Edition=Player -p:AutoTestBuild=1`) 오류 0 + LoadCheck + 기존 GpuSplitCheck fixture 통과. Player 제외 유지. 새 Harmony 패치는 이름+인자 수+호출 규약으로 정확히 하나, `Module.ResolveSignature` 금지.

## 규칙
- 헤르츠 맵 이름·비공개 프로젝트 언급 금지, 100만 타일 맵 열기 금지.
- 실행 순서(`run_gpu2.py` 형태: 같은 구간을 그룹마다 `seek` 재생)를 결과 문서에 `sf_run` steps 로 제공.
