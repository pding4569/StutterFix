
## Codex 3회차 분석 및 수정 (게임 시험 미실행)

- 외부 run2: 3440×1440@164.999, 네 창 소유 23547·native 오류 0. A0/B1/B2/A3=167.994/169.166/168.853/168.609 FPS, A/B 평균 168.301500/169.009500, +0.420674%로 latecache1은 185.131650 FPS 하한에 미달했다.
- 지정 증거 76줄 중 75줄 원시 완전 일치, 첫 줄은 원시 157행 앞 1500자 발췌다. B 캐시 재구축 1074/1077회·제외 37개로 같은 캐시 방식 반복은 하지 않는다. 전환 복원은 네 창 10854/10854이며 픽셀/전체 안정성은 NA다.
- 다음 후보 (2): 평가·기존 정규화·native 복사를 한 CommandBuffer 제출로 합친다. submitsingle 기본 false·일반 Player 제외, 동적 입력 때문에 객체를 재사용하면서 매 프레임 명령은 다시 기록한다. CPU 처리·소스 진행·마스크·기존 fallback 유지.
- 새 기본 계획은 119단계 off/on/on/off, tag `modcpu-260-270-abba-submitsingle2`. 목록/effect 캐시는 끄고 각 창 양끝 환경·소유·후보 실행·fallback/오류 0을 검사한다. 기존 baseline/abba 모드는 이전 JSON 전체와 같다.
- 빌드 DEV/Player/PlayerAuto 오류 0/0/0(경고 46/4/39, Player 증분), 제출/기록 실제 소스+managed adapter 35개·생성기 5개 통과. 새 후보 FPS·픽셀·안정성 NA, `MODCPU=FAIL` 유지. 게임 실행·설치·커밋·푸시·자동 장치 호출·설정/제어 파일 변경 0회.
- 상세 `codex-modcpu-result.md`, 수치 `modcpu-autoloop-ledger.md`, 10줄 `codex-modcpu-result-o.txt`. 아래 2회차는 후보 외부 시험 전의 기록이다.

## Codex 2회차 분석 및 수정 (게임 시험 미실행)

- 지정된 외부 run1 원문 38줄과 원시 `night-results/al-mc-1.log` 대조 완료. 3440×1440@165Hz, 소유 두 창 23547, FPS 167.918/168.088, 평균 168.003. 이는 실패 후보가 아니라 최초 기준선이다.
- 후보 (1)만 추가: TightLate에서 GPU 소유 항목을 제외한 CPU 배열 캐시, B에서 새 Sleep 때만 Dormancy 압축. CPU fallback·순서·소유 전환·복원 및 매 프레임 GpuLiveFrame 유지, 기본 false·일반 Player 제외.
- 새 생성기는 118단계 off/on/on/off를 출력한다. 양끝 환경·소유 검사와 캐시 통계를 추가했다. 기존 66단계 기준선은 `--mode baseline`으로 동일하게 재현된다.
- DEV/Player/PlayerAuto 빌드 오류 0/0/0(경고 46/36/39), 실제 Dormancy+캐시 검사 87개, 생성기 검사 4개 통과.
- `MODCPU=FAIL fps_before=168.003 fps_after=NA gain_pct=NA reason=CANDIDATE_NOT_RUN`. 후보 성능·픽셀·안정성 미검증, 게임 실행·설치·커밋·푸시·자동 장치 호출 0회. 상세는 `codex-modcpu-result.md`, 수치는 `modcpu-autoloop-ledger.md`.

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

## 회차 2
- Codex 요약: 인계대로 `TightLate` 목록 캐시 후보와 A/B/B/A 시험 계획을 준비했습니다.

- 기준선: **168.003 FPS**, 목표 하한 **184.8033 FPS**
- 빌드 3종 오류 0개, 오프라인 검사 91개 통과
- 후보 성능·픽셀은 미측정이므로 아직 PASS가 아닙니다.
- 게임 실행·설치·커밋은 하지 않았습니다.

[결과 문서](E:/StutterFixWork/wt-gpu/docs/handoff/codex-modcpu-result.md)에 변경 내용과 남은 검증을 기록했습니다.
- 시험 핵심 줄:
  - [StutterFix] [gpudeco] cost_probe enabled=False frames=0 cpu_only=True advance_ms=0.000000 dirty_alpha_ms=0.000000 source_upload_ms=0.000000 dirty_calls_avg=0.000000 dirty_bytes_avg=0.000 source_slots
  - [StutterFix] [gpudeco] prepare_ms=7352.321500 phase=live_prepare slices=1 owned_union=23547 total_runs=23547 total_submeshes=23547 live_kernel=EvaluateLive_sourceE native_sort=True native_copy=True ei
  - [StutterFix] [gpudeco] on=True scope=song_union owned=23547 active_sources=2 slice=0 slice_owned=23547 runs=23547 submeshes=23547 transition_ms=102.565300 prepare_ms=7352.321500
  - [StutterFix] [gpudeco] render_census window=-1 edge=explicit slice=0 allocated_renderers=23547 enabled_renderers=1880 visible_renderers=1880 items=23547 proven_constant_zero_alpha=21667 animated_alpha
  - [StutterFix] [gpudeco] cost scope=live_off frames=2431 cpu_scope=managed_only native_copy_accounting=G2_window_stats total_ms=0.418981 progress_ms=0.137396 uniform_ms=0.005989 dispatch_ms=0.272922 mis
  - [StutterFix] [gpudeco] on=False owned=0 originals_restored=True reason=live_off song=270.048546 resumed_recs=10854 resume_expected=10854 unobserved_order=0 restore_ms=27.724200
  - [StutterFix] [gpudeco] on=True scope=song_union owned=23547 active_sources=2 slice=0 slice_owned=23547 runs=23547 submeshes=23547 transition_ms=103.173200 prepare_ms=7352.321500
  - [StutterFix] [gpudeco] render_census window=-1 edge=explicit slice=0 allocated_renderers=23547 enabled_renderers=1880 visible_renderers=1880 items=23547 proven_constant_zero_alpha=21667 animated_alpha
  - [StutterFix] [gpudeco] cost scope=live_off frames=4877 cpu_scope=managed_only native_copy_accounting=G2_window_stats total_ms=0.419849 progress_ms=0.138250 uniform_ms=0.005955 dispatch_ms=0.272935 mis
  - [StutterFix] [gpudeco] on=False owned=0 originals_restored=True reason=live_off song=270.040242 resumed_recs=10854 resume_expected=10854 unobserved_order=0 restore_ms=25.013800
  - [StutterFix] [gpudeco] on=True scope=song_union owned=23547 active_sources=2 slice=0 slice_owned=23547 runs=23547 submeshes=23547 transition_ms=103.428200 prepare_ms=7352.321500
  - [StutterFix] [gpudeco] render_census window=-1 edge=explicit slice=0 allocated_renderers=23547 enabled_renderers=1880 visible_renderers=1880 items=23547 proven_constant_zero_alpha=21667 animated_alpha

## 회차 3
- Codex 요약: 기존 목록 캐시는 **168.3015→169.0095 FPS(+0.4207%)**로 목표 미달을 확인했습니다.

- 평가·복사를 한 번에 제출하는 새 후보와 119단계 ABBA 계획 준비
- 빌드 3종 오류 0개, 오프라인 검사 40개 통과
- 새 후보 성능·픽셀은 미측정이며 `MODCPU=FAIL` 유지

게임 실행·설치·커밋은 하지 않았습니다. [결과 문서](E:/StutterFixWork/wt-gpu/docs/handoff/codex-modcpu-result.md)에 기록했습니다.


- 시험 핵심 줄:
  - [StutterFix] [gpudeco] cost_probe enabled=False frames=0 cpu_only=True advance_ms=0.000000 dirty_alpha_ms=0.000000 source_upload_ms=0.000000 dirty_calls_avg=0.000000 dirty_bytes_avg=0.000 source_slots
  - [StutterFix] [gpudeco] prepare_ms=7326.045500 phase=live_prepare slices=1 owned_union=23547 total_runs=23547 total_submeshes=23547 live_kernel=EvaluateLive_sourceE native_sort=True native_copy=True ei
  - [StutterFix] [gpudeco] on=True scope=song_union owned=23547 active_sources=2 slice=0 slice_owned=23547 runs=23547 submeshes=23547 transition_ms=101.137300 prepare_ms=7326.045500
  - [StutterFix] [gpudeco] render_census window=-1 edge=explicit slice=0 allocated_renderers=23547 enabled_renderers=1880 visible_renderers=1880 items=23547 proven_constant_zero_alpha=21667 animated_alpha
  - [StutterFix] [gpudeco] cost scope=live_off frames=2425 cpu_scope=managed_only native_copy_accounting=G2_window_stats total_ms=0.420981 progress_ms=0.139126 uniform_ms=0.006059 dispatch_ms=0.273258 mis
  - [StutterFix] [gpudeco] on=False owned=0 originals_restored=True reason=live_off song=270.043963 resumed_recs=10854 resume_expected=10854 unobserved_order=0 restore_ms=26.731300
  - [StutterFix] [gpudeco] on=True scope=song_union owned=23547 active_sources=2 slice=0 slice_owned=23547 runs=23547 submeshes=23547 transition_ms=99.109800 prepare_ms=7326.045500
  - [StutterFix] [gpudeco] render_census window=-1 edge=explicit slice=0 allocated_renderers=23547 enabled_renderers=1880 visible_renderers=1880 items=23547 proven_constant_zero_alpha=21667 animated_alpha
  - [StutterFix] [gpudeco] cost scope=live_off frames=4773 cpu_scope=managed_only native_copy_accounting=G2_window_stats total_ms=0.431295 progress_ms=0.142765 uniform_ms=0.004071 dispatch_ms=0.281872 mis
  - [StutterFix] [gpudeco] on=False owned=0 originals_restored=True reason=live_off song=270.049718 resumed_recs=10854 resume_expected=10854 unobserved_order=0 restore_ms=24.826300
  - [StutterFix] [gpudeco] on=True scope=song_union owned=23547 active_sources=2 slice=0 slice_owned=23547 runs=23547 submeshes=23547 transition_ms=98.729300 prepare_ms=7326.045500
  - [StutterFix] [gpudeco] render_census window=-1 edge=explicit slice=0 allocated_renderers=23547 enabled_renderers=1880 visible_renderers=1880 items=23547 proven_constant_zero_alpha=21667 animated_alpha
