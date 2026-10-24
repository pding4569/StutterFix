
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
