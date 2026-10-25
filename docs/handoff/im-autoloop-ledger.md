
## 회차 1
- Codex 요약: 진단 2종, 알파 더티 감시, 시험 계획을 구현했습니다. 빌드 3종 오류 0, LoadCheck 오류 0, 검사 4,500개가 통과했습니다.

게임 실행·설치·커밋은 하지 않았습니다. 실제 픽셀·FPS는 미측정이고 Claude 소유 파일의 연결 2곳이 남아, `IMAGEOWN=FAIL`로 기록했습니다.

[결과와 남은 작업](/E:/StutterFixWork/wt-gpu/docs/handoff/codex-imageown-result.md)


- 시험 핵심 줄:
  - [StutterFix] [gpudeco] cost_probe enabled=False frames=0 cpu_only=True advance_ms=0.000000 dirty_alpha_ms=0.000000 source_upload_ms=0.000000 dirty_calls_avg=0.000000 dirty_bytes_avg=0.000 source_slots
  - [StutterFix] [gpudeco] prepare_ms=8713.423500 phase=live_prepare slices=1 owned_union=28523 total_runs=28523 total_submeshes=28523 live_kernel=EvaluateLive_sourceE native_sort=True native_copy=True ei
  - [StutterFix] [gpudeco] on=True scope=song_union owned=28523 active_sources=2135 slice=0 slice_owned=28523 runs=28523 submeshes=28523 transition_ms=158.035400 prepare_ms=8713.423500
  - [StutterFix] [gpudeco] cost scope=live_off frames=33 cpu_scope=managed_only native_copy_accounting=G2_window_stats total_ms=0.385324 progress_ms=0.011964 uniform_ms=0.007345 dispatch_ms=0.216142 misc_
  - [StutterFix] [gpudeco] on=False owned=0 originals_restored=True reason=live_off song=260.019671 resumed_recs=11821 resume_expected=11821 unobserved_order=0 restore_ms=12.552100
  - [StutterFix] [gpudeco] on=True scope=song_union owned=28523 active_sources=2135 slice=0 slice_owned=28523 runs=28523 submeshes=28523 transition_ms=156.975300 prepare_ms=8713.423500
  - [StutterFix] [gpudeco] cost scope=recovery:FastMoveOriginal frames=39 cpu_scope=managed_only native_copy_accounting=G2_window_stats total_ms=0.508033 progress_ms=0.011915 uniform_ms=0.007326 dispatch_
  - [StutterFix] [gpudeco] on=False owned=0 originals_restored=True reason=recovery:FastMoveOriginal song=260.019671 resumed_recs=0 resume_expected=0 unobserved_order=0 restore_ms=6.112300
