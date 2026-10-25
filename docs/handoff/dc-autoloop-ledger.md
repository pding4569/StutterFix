
## Codex 회차 2 준비 (2026-10-25, 새 게임 시험 없음)

- 외부 run1 증거 68줄을 원시 `E:/StutterFixWork/night-results/al-dc-1.log`와 순서·중복까지 대조했다.
- 첫 진단만 완료: 2560×1440@164.999·260.008990초·GPU23547, 후보 소유1628/장벽316/구간17/같은 텍스처run1177. 확정 S는 NA·gate=HOLD다.
- 실제 GPU 담당 텍스처81종·35티어, 추가 압축210.989090MiB/총615.006191MiB를 합산 대조했다. 할당·복사·여유 VRAM은 미검증이다.
- 진단 재개 뒤 260.066747초 FastMoveOriginal→CPU10252/10252 복귀, 다음 validate 실패다. 코드상 재개 catch-up 보호 경로를 확인했고 이번 세부 Take 거절 추적은 없다.
- 보호 코드를 유지하고 각 표본을 새 seek→GPU on→정지 진단→GPU off→resume/stop으로 분리했다. 새 기본 diag2-isolated 103단계는 생성만, 기존 diag1 68단계 파일·명시 모드 출력 보존이다.
- 3빌드 오류0/0/0·경고46/36/4(PlayerAuto 증분)·LoadCheck279/229/280곳 오류0·정책27개·생성기6개 통과. 게임·설치·커밋·푸시·자동 장치 호출·설정/제어 파일 변경은 이번 모두0회다.
- `DRAWCOLLAPSE=FAIL reason=GATE1_INCOMPLETE_AFTER_RECOVERY`. 후보17로 2관문에 진입하지 않는다. 새 계획 성공·FPS·원본 픽셀·엔진 draw·끊김·전체 수명 안정성 NA, 병합 미구현이다.
- 상세 `codex-drawcollapse-result.md`·10줄 `codex-drawcollapse-result-o.txt`·준비 장부 `drawcollapse-autoloop-ledger.md`를 따른다. 새 시험 예약/실행 기록이 아니다.

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

## 회차 2
- Codex 요약: 외부 실패 로그를 분석하고, GPU 소유 중 재개하지 않도록 진단 계획을 수정했습니다.

- 새 103단계 계획은 **생성만** 했습니다.
- 3종 빌드·최신 LoadCheck·정책 27개·생성기 검사 6개 통과.
- 장벽 구간 17개는 후보값이므로 **1관문 HOLD**, 병합 구현은 보류했습니다.
- 게임 실행·설치·커밋·푸시 모두 0회입니다.

[상세 결과](E:/StutterFixWork/wt-gpu/docs/handoff/codex-drawcollapse-result.md)에 근거와 미검증 항목을 기록했습니다.


- 시험 핵심 줄:
  - [StutterFix] [gpudeco] cost_probe enabled=False frames=0 cpu_only=True advance_ms=0.000000 dirty_alpha_ms=0.000000 source_upload_ms=0.000000 dirty_calls_avg=0.000000 dirty_bytes_avg=0.000 source_slots
  - [StutterFix] [gpudeco] prepare_ms=7325.625200 phase=live_prepare slices=1 owned_union=23547 total_runs=23547 total_submeshes=23547 live_kernel=EvaluateLive_sourceE native_sort=True native_copy=True ei
  - [StutterFix] [gpudeco] on=True scope=song_union owned=23547 active_sources=2 slice=0 slice_owned=23547 runs=23547 submeshes=23547 transition_ms=99.592700 prepare_ms=7325.625200
  - [StutterFix] [gpudeco] cost scope=live_off frames=758 cpu_scope=managed_only native_copy_accounting=G2_window_stats total_ms=0.348960 progress_ms=0.085589 uniform_ms=0.006084 dispatch_ms=0.251406 misc
  - [StutterFix] [gpudeco] on=False owned=0 originals_restored=True reason=live_off song=260.008990 resumed_recs=10854 resume_expected=10854 unobserved_order=0 restore_ms=30.435200
  - [StutterFix] [gpudeco] on=True scope=song_union owned=23547 active_sources=2 slice=0 slice_owned=23547 runs=23547 submeshes=23547 transition_ms=97.820600 prepare_ms=7325.625200
  - [StutterFix] [gpudeco] cost scope=live_off frames=1849 cpu_scope=managed_only native_copy_accounting=G2_window_stats total_ms=0.369585 progress_ms=0.098500 uniform_ms=0.006134 dispatch_ms=0.259922 mis
  - [StutterFix] [gpudeco] on=False owned=0 originals_restored=True reason=live_off song=262.009448 resumed_recs=9954 resume_expected=9954 unobserved_order=0 restore_ms=34.508100
  - [StutterFix] [gpudeco] on=True scope=song_union owned=23547 active_sources=2 slice=0 slice_owned=23547 runs=23547 submeshes=23547 transition_ms=100.068900 prepare_ms=7325.625200
  - [StutterFix] [gpudeco] cost scope=live_off frames=3277 cpu_scope=managed_only native_copy_accounting=G2_window_stats total_ms=0.383498 progress_ms=0.107488 uniform_ms=0.006123 dispatch_ms=0.265463 mis
  - [StutterFix] [gpudeco] on=False owned=0 originals_restored=True reason=live_off song=264.008990 resumed_recs=10507 resume_expected=10507 unobserved_order=0 restore_ms=39.006200
  - [StutterFix] [gpudeco] on=True scope=song_union owned=23547 active_sources=2 slice=0 slice_owned=23547 runs=23547 submeshes=23547 transition_ms=102.781800 prepare_ms=7325.625200
