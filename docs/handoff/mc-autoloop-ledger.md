
## 회차 1
- Codex 요약: 인계의 **첫 회전은 코드 수정 없이 기준선 측정** 지시에 따라 준비했습니다.

- 기준선 계획 66단계 생성, 오프라인 검사 17개 통과.
- 런타임·생성기와 기존 수정 파일 보존.
- 게임 실행·설치·커밋 없음.
- 실제 성능은 미측정이며, 자동 장치의 기준선 결과 확인 후 최적화를 적용해야 합니다.

[결과 문서](E:/StutterFixWork/wt-gpu/docs/handoff/codex-modcpu-result.md) · [측정 장부](E:/StutterFixWork/wt-gpu/docs/handoff/modcpu-autoloop-ledger.md)
- 시험 핵심 줄:
  - [StutterFix] [gpudeco] cost_probe enabled=False frames=0 cpu_only=True advance_ms=0.000000 dirty_alpha_ms=0.000000 source_upload_ms=0.000000 dirty_calls_avg=0.000000 dirty_bytes_avg=0.000 source_slots
  - [StutterFix] [gpudeco] prepare_ms=7435.426500 phase=live_prepare slices=1 owned_union=23547 total_runs=23547 total_submeshes=23547 live_kernel=EvaluateLive_sourceE native_sort=True native_copy=True ei
  - [StutterFix] [gpudeco] on=True scope=song_union owned=23547 active_sources=2 slice=0 slice_owned=23547 runs=23547 submeshes=23547 transition_ms=98.373400 prepare_ms=7435.426500
  - [StutterFix] [gpudeco] render_census window=-1 edge=explicit slice=0 allocated_renderers=23547 enabled_renderers=1880 visible_renderers=1880 items=23547 proven_constant_zero_alpha=21667 animated_alpha
  - [StutterFix] [gpudeco] cost scope=live_off frames=2426 cpu_scope=managed_only native_copy_accounting=G2_window_stats total_ms=0.418991 progress_ms=0.137325 uniform_ms=0.005714 dispatch_ms=0.273212 mis
  - [StutterFix] [gpudeco] on=False owned=0 originals_restored=True reason=live_off song=270.034921 resumed_recs=10854 resume_expected=10854 unobserved_order=0 restore_ms=26.273700
  - [StutterFix] [gpudeco] on=True scope=song_union owned=23547 active_sources=2 slice=0 slice_owned=23547 runs=23547 submeshes=23547 transition_ms=96.942700 prepare_ms=7435.426500
  - [StutterFix] [gpudeco] render_census window=-1 edge=explicit slice=0 allocated_renderers=23547 enabled_renderers=1880 visible_renderers=1880 items=23547 proven_constant_zero_alpha=21667 animated_alpha
  - [StutterFix] [gpudeco] cost scope=live_off frames=4855 cpu_scope=managed_only native_copy_accounting=G2_window_stats total_ms=0.419483 progress_ms=0.137825 uniform_ms=0.005745 dispatch_ms=0.273191 mis
  - [StutterFix] [gpudeco] on=False owned=0 originals_restored=True reason=live_off song=270.027720 resumed_recs=10854 resume_expected=10854 unobserved_order=0 restore_ms=24.654200
