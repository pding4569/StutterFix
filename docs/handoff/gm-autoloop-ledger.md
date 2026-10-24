
## Codex 회차 2 수정 (2026-10-25, 게임 실행 없음)

- 지정된 run1은 FPS 104.609/104.534/104.176/104.260이지만 소유 26개 전부 투명·독립 enabled=0이다. 픽셀 2점 차이 0·retry 통과는 이 모집단에서만 관측됐다. 실패는 B1/B2 측정이 아니라 song_end 준비의 fallback 2개이며 stability=1/4다.
- 최신 05:45 지시대로 최초 준비를 곡 처음에서 수행 후 정지하고, ABBA는 seek 260으로 한다. PrepVfx 순서 스냅샷도 보관해 scrub 중 예약 목록 소모로 기준이 줄어드는 경로를 수정했다. 생산 게이트에 소유 ≥20,000·A enabled>0·동일 ABBA 소유 개수 조건을 추가했다.
- 256초 activation/paused ready/CPU 복원/정지로 측정 밖 경계를 준비한다. song_end는 296초 paused ready를 사용한다. 복구/정렬 가드와 기존 성능·픽셀·복원 기준은 유지한다.
- 새 계획 `tools/optimization/out/render-merge-schedule4-steps.json`(333단계) 생성만. Dev/Player/PlayerAuto 오류 0, 생성기 11개·RenderMergeCheck 54개·굽기 코어 계약 통과. 전체 UI 인코딩 검사만 기존 UTF-16 산출물 때문에 실패했다.
- 수정판 `RENDERMERGE=ERROR reason=GAME_NOT_RUN`; 소유 수 회복·FPS·픽셀·복원은 NA다. 게임 실행·설치·커밋·푸시·자동 장치 호출 없음. 같은 코드 반복 시험 요청 없음. 상세 `codex-render-merge-result.md` 및 10줄 요약을 따른다.

## 회차 1
- Codex 요약: 최신 보충 인계에 따라 ABBA의 pause/resume을 제거하고, 재생 중 준비된 렌더러 전환을 허용했습니다.

- 빌드 3종 오류 0, 검사 52개 통과.
- 게임 실행·설치·커밋·푸시 없음.
- FPS·픽셀·복원은 미검증입니다. 기존 소유 수 26개와 기준 23,547개의 불일치도 남아 있습니다.

[상세 결과](/E:/StutterFixWork/wt-gpu/docs/handoff/codex-render-merge-result.md) · [10줄 요약](/E:/StutterFixWork/wt-gpu/docs/handoff/codex-render-merge-result-o.txt)


- 시험 핵심 줄:
  - [StutterFix] [gpudeco] cost_probe enabled=False frames=0 cpu_only=True advance_ms=0.000000 dirty_alpha_ms=0.000000 source_upload_ms=0.000000 dirty_calls_avg=0.000000 dirty_bytes_avg=0.000 source_slots
  - [StutterFix] [gpudeco] rendermerge_prepared start=250.0 end=252.5 items=0 renderers=0 submeshes=0
  - [StutterFix] [gpudeco] rendermerge_prepared start=252.5 end=255.0 items=2 renderers=2 submeshes=2
  - [StutterFix] [gpudeco] rendermerge_prepared start=255.0 end=257.5 items=2 renderers=2 submeshes=2
  - [StutterFix] [gpudeco] rendermerge_prepared start=257.5 end=260.0 items=2 renderers=2 submeshes=2
  - [StutterFix] [gpudeco] rendermerge_prepared start=260.0 end=262.5 items=0 renderers=0 submeshes=0
  - [StutterFix] [gpudeco] rendermerge_prepared start=262.5 end=265.0 items=2 renderers=2 submeshes=2
  - [StutterFix] [gpudeco] rendermerge_prepared start=265.0 end=267.5 items=5 renderers=5 submeshes=5
  - [StutterFix] [gpudeco] rendermerge_prepared start=267.5 end=270.0 items=6 renderers=6 submeshes=6
  - [StutterFix] [gpudeco] rendermerge_prepared start=270.0 end=272.5 items=0 renderers=0 submeshes=0
  - [StutterFix] [gpudeco] rendermerge_prepared start=272.5 end=275.0 items=1 renderers=1 submeshes=1
  - [StutterFix] [gpudeco] rendermerge_prepared start=275.0 end=277.5 items=5 renderers=5 submeshes=5

## 회차 2
- Codex 요약: 소유 범위·예약 목록 수집·fallback 준비 절차를 수정했습니다.

- 빌드 3종 오류 0, 생성기 11개·RenderMerge 검사 54개·굽기 계약 검사 통과.
- 전체 인코딩 검사는 기존 UTF-16 산출물로 실패했고, 변경 소스는 통과했습니다.
- 게임 실행·설치·커밋은 하지 않았습니다. 수정판 성능·픽셀·복원은 미검증이며 PASS가 아닙니다.

[상세 결과](E:/StutterFixWork/wt-gpu/docs/handoff/codex-render-merge-result.md) · [10줄 요약](E:/StutterFixWork/wt-gpu/docs/handoff/codex-render-merge-result-o.txt)


- 시험 핵심 줄:
  - [StutterFix] [gpudeco] cost_probe enabled=False frames=0 cpu_only=True advance_ms=0.000000 dirty_alpha_ms=0.000000 source_upload_ms=0.000000 dirty_calls_avg=0.000000 dirty_bytes_avg=0.000 source_slots
  - [StutterFix] [gpudeco] rendermerge_prepared start=250.0 end=252.5 items=38 renderers=38 submeshes=38
  - [StutterFix] [gpudeco] rendermerge_prepared start=252.5 end=255.0 items=40 renderers=40 submeshes=40
  - [StutterFix] [gpudeco] rendermerge_prepared start=255.0 end=257.5 items=940 renderers=940 submeshes=940
  - [StutterFix] [gpudeco] rendermerge_prepared start=257.5 end=260.0 items=3282 renderers=3282 submeshes=3282
  - [StutterFix] [gpudeco] rendermerge_prepared start=260.0 end=262.5 items=4580 renderers=4580 submeshes=4580
  - [StutterFix] [gpudeco] rendermerge_prepared start=262.5 end=265.0 items=4632 renderers=4632 submeshes=4632
  - [StutterFix] [gpudeco] rendermerge_prepared start=265.0 end=267.5 items=4642 renderers=4642 submeshes=4642
  - [StutterFix] [gpudeco] rendermerge_prepared start=267.5 end=270.0 items=4493 renderers=4493 submeshes=4493
  - [StutterFix] [gpudeco] rendermerge_prepared start=270.0 end=272.5 items=4875 renderers=4875 submeshes=4875
  - [StutterFix] [gpudeco] rendermerge_prepared start=272.5 end=275.0 items=3477 renderers=3477 submeshes=3477
  - [StutterFix] [gpudeco] rendermerge_prepared start=275.0 end=277.5 items=931 renderers=931 submeshes=931
