# Codex 임무 gpusplit: Arche 장면 그리기 GPU 시간을 그룹별로 나누는 자동 측정 만들기

2026-10-20. 작업 폴더 `E:\StutterFixWork\wt-archegpu` (브랜치 `codex/night-archegpu`, `archegpu`/`gpuprobe` 가 이미 있다). 게임 실행·설치·커밋 금지. 결과 `docs/handoff/codex-gpusplit-20261020-result.md` + `-o`(10줄).

## 배경
Arche 258~278초 GPU 프레임 4.24ms 중 본 카메라 4.06ms, 필터 합 약 0.65ms, 장면 그리기 약 3.4ms 이다(`docs/handoff/archegpu-evidence/`). 장식 CPU 를 트레이스로 없애면 GPU 가 바닥이 되므로(`docs/optimization-36.md` 끝 정정), **장면 3.4ms 가 장식/타일/행성/기타 중 무엇이 차지하는지** 알아야 한다. 이전 "렌더러 숨기면 +6%" 는 CPU 병목 FPS 라 GPU 몫을 과소평가할 수 있다. GPU 시간 자체(`gpuprobe`, CPU 병목 무관)로 재야 한다.

## 만들 것 (`DEV || AUTOTEST`, 기본 꺼짐, Player 제외, `Main.Unload`/`off` 에서 반드시 복구)
1. 자동 시험 명령 `gpusplit hide <그룹|none>` : 그룹별 렌더러를 `Renderer.enabled=false`(또는 `forceRenderingOff`)로 숨기고 `none` 에서 원래 상태로 복구(숨기기 전 값을 저장해 정확히 복원). 그룹: `deco-image`(SpriteRenderer 장식), `deco-text`, `deco-particle`, `tiles`(타일 렌더러·발광·아이콘), `planets`(행성·꼬리), `other`(위에 안 속한 장면 렌더러, UI 카메라 제외). 분류 기준은 기존 `DecoCensus`/`gpucensus`/`TileCull` 코드의 소유 관계 재사용. 숨김 중 새로 생기는 렌더러(곡 중 켜지는 장식)는 다음 `hide` 호출 때 다시 적용하는 `gpusplit refresh` 도 제공.
2. 한 번의 곡 구간에서 여러 창을 측정하는 단계 목록을 `docs/handoff/codex-gpusplit-20261020-result.md` 에 `sf_run` steps 로 제공: `archegpu on` → `gpuprobe on` → `seek 262 8` → 창마다 `gpusplit hide X` → `gpuprobe reset` → `waitsongpos <+4s>` → `gpuprobe state` → `gpusplit hide none`. 창 길이 4초 × (기준, deco-image, tiles, planets, deco-text+particle, other, 전체 숨김, 기준 다시) 로 한 번 5분 안팎에 끝나게. 같은 곡 구간의 내용이 시간에 따라 달라지므로 **기준 창을 앞뒤로** 넣어 드리프트를 보게 하고, 창마다 `archegpu stamp` 로 곡 시각을 남긴다.
3. 결과를 해석하는 스크립트 `tools/optimization/gpusplit-summary.py` : 로그에서 창별 본 카메라 평균 GPU ms 를 뽑아 `기준 − 숨김 = 그 그룹의 GPU 몫` 표를 낸다(기준 두 창의 차이를 오차로 표기, 그룹 몫의 합과 전체 숨김 차이의 불일치도 표시).
4. 주의: 렌더러를 숨기면 CPU 쪽 변화(`Camera.Render` 컬링)도 생기지만 `gpuprobe` 는 GPU timestamp 이므로 무관하다. 숨김 자체가 게임 로직(판정/충돌)을 바꾸지 않게 `enabled`/`forceRenderingOff` 만 건드리고 GameObject 활성은 건드리지 않는다. 메인 스레드 프레임당 새 할당을 만들지 않는다(그룹 목록은 `gpusplit hide` 때만 만든다).
5. 빌드 3종(`-c Release`; `-p:Edition=Player`; `-p:Edition=Player -p:AutoTestBuild=1`) 오류 0 + LoadCheck. 오프라인 fixture 가 가능한 범위(그룹 분류 결정 로직·복원 정확성)는 검사 추가.

## 규칙
- 헤르츠 맵 이름·비공개 프로젝트 언급 금지, 100만 타일 맵 열기 금지. `Module.ResolveSignature` 금지, 새 Harmony 패치는 이름+인자 수+호출 규약으로 정확히 하나.
- 맵: `D:\얼불춤 맵 파일\Arche\` 의 `*FULL.adofai`.
