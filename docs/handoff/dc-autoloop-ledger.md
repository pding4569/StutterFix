
## 회차 1
- Codex 요약: 1관문 진단 코드·메모리 계산·68단계 시험 계획을 준비했습니다.

- 3종 빌드·LoadCheck 통과, 정책 검사 27개·생성기 검사 4개 통과.
- 게임 실행·설치·커밋·푸시 0회.
- 실제 픽셀 기반 장벽 수는 미측정입니다. **1관문은 HOLD이며 병합 구현은 보류**했습니다.

[상세 결과](E:/StutterFixWork/wt-gpu/docs/handoff/codex-drawcollapse-result.md) · [10줄 요약](E:/StutterFixWork/wt-gpu/docs/handoff/codex-drawcollapse-result-o.txt)


- 시험 핵심 줄:
  - [StutterFix] [gpudeco] cost_probe enabled=False frames=0 cpu_only=True advance_ms=0.000000 dirty_alpha_ms=0.000000 source_upload_ms=0.000000 dirty_calls_avg=0.000000 dirty_bytes_avg=0.000 source_slots
  - [StutterFix] [gpudeco] prepare_ms=7309.856900 phase=live_prepare slices=1 owned_union=23547 total_runs=23547 total_submeshes=23547 live_kernel=EvaluateLive_sourceE native_sort=True native_copy=True ei
  - [StutterFix] [gpudeco] on=True scope=song_union owned=23547 active_sources=2 slice=0 slice_owned=23547 runs=23547 submeshes=23547 transition_ms=99.004600 prepare_ms=7309.856900
  - [StutterFix] [gpudeco] cost scope=recovery:FastMoveOriginal frames=767 cpu_scope=managed_only native_copy_accounting=G2_window_stats total_ms=0.347974 progress_ms=0.083775 uniform_ms=0.005777 dispatch
  - [StutterFix] [gpudeco] on=False owned=0 originals_restored=True reason=recovery:FastMoveOriginal song=260.066747 resumed_recs=10252 resume_expected=10252 unobserved_order=0 restore_ms=23.988800
