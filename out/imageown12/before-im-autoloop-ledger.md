## 재시작 3회차(누적11회차) — run2 통합 픽셀 국소화·소유 경계 중단

최신 run2 직접79줄을 원시158~838행과 순서 대조했다. 통합260/262초 changed28818/22961·max3/4·반복/복원0이며 262초에서 중단, 270초/ABBA/FPS/끊김은NA다. 로그와 일치한 첫 PNG 오프라인 분석: O/G 3픽셀·max1, G/B 22958픽셀·max4, O/B 22961픽셀·max4; 초과3픽셀 모두 O=G이며 G/B=4다. 실패를 독립 이미지 소유가 아닌 통합 전환 단계로 국소화했다. 정확한 정렬/샘플링/셰이더 산술 원인은미확정이다. renderer가 별도 GpuDecoBatch 재질/셰이더로 교체하고 기존40합성검사는 Sprites/Default·흰 정점색만대조해 게임 Sprites/Tile·중첩안개의동일성을증명하지못한다. 필요한통합renderer/셰이더수정은소유규칙밖이므로 현재허용범위의설계경계에서중단·소유자에게인계한다. 전체설계불가능판정아님. 기준3/정렬보호/48슬롯/이미지소유/실제카운터유지·코드/생성기/기존JSON변경0·새계획/반복시험요청0·빌드/LoadCheck반복0이다. 보호292개중코드/게임DLL/번들/설정등285개SHA유지, 공유PNG는외부덮어쓰기감지로보호수에서분리·후속분석거부했다. 게임·설치·커밋·푸시·장치·Editor·설정/제어변경0회. IMAGEOWN=FAIL reason=COLLAPSE_PIXEL_SCOPE_LIMIT·상세결과/out/imageown11참조.

## 재시작 2회차(누적10회차) — 외부run1 분석·완료순번 수정

최신 외부 run1 직접289줄을 원시158~1971행과 순서 대조했다. 통합260/262/270초 changed28603/22550/9186·max3/3/2·반복/복원0·이미지제외0·비이미지정렬보호14개로 픽셀선행을 통과했다. A0/B1/B2=211.823440/208.592248/197.047153FPS·끊김0/0/4회·창복원3/3 뒤 A3 begin에서 중단됐다. 완료순번을 성공창수로 세는 오류를 이미지fixture에서 수정하고, 실패는 최종FAIL에 유지하면서 복원 뒤 다음창으로 진행하게 했다. 끊김16개 고정버퍼에 관측시각/길이/GC누계 저장·창종료후로그·초과수명시, 일시적런타임실패도 현재창에반영한다. 정확히+20%경계판정오류 수정·진입소스의픽셀4허용을지시된3으로복원했다. 정렬보호/실제카운터/renderer/policy/셰이더/48슬롯/번들 유지다. 최종격리3빌드 오류0/0/0·경고48/4/40·LoadCheck279/229/281곳틀림0·실제5메서드56검사/생성기8검사통과·명시12출력/기존JSON보존·새abba5-completion333단계생성만. IMAGEOWN=FAIL reason=ABBA_INCOMPLETE_AFTER_B2_SPIKES; 수정후게임/FPS/+20%/끊김해결/전곡/설치복원NA. 기존4끊김의원인·시각은미확정이며설계전체불가능판정아님. 게임·설치·커밋·푸시·장치·Editor·설정/제어변경0회. 보호284개중의도한코드2개외282개SHA유지·상세결과/out/imageown10참조.

## 회차 9 — 17:15 지시 반영, 이미지 무폴백 판정 일관성 수정

외부 run8 직접53줄을 원시158~544행과 순서 대조했다. 등록93/주소93/누락0·직접34/48·배열4/4·290.312500MiB·GPU28523/CPU fallback0 뒤 비이미지 정렬 보호14개로 collapsecheck 실패다. 17:15추가지시를 우선해 이미 적용된 선행 검사 완화를 보존하고 픽셀/ABBA의 남은 전체폴백0 조건도 같은 기준으로 맞췄다. 이미지 제외/주소 누락0·비이미지 정렬 보호<=1%이며, 측정 전 승인집합을 저장하고 매 프레임 개수/멤버/배열소유자 변경을 거부한다. 정렬보호/실제카운터/독립GPU경로/48슬롯/셰이더/번들은 유지했다. 격리3빌드 오류0/0/0·경고4/4/40·LoadCheck279/229/281곳틀림0·실제5메서드28검사/생성기8검사 통과다. 명시12출력/기존JSON 보존·새abba4-orderguard333단계 생성만. IMAGEOWN=FAIL reason=ACCEPTANCE_FIX_NOT_RUN; 통합픽셀/FPS/+20%/끊김/전곡/설치복원NA. 게임·설치·커밋·푸시·장치·Editor·설정/제어변경0회. 상세결과/out/imageown9 참조.

## 회차 8 — 외부 run7 분석, 비이미지 정렬 제외14개로 중단

외부 run7 직접53줄을 원시158~543행과 순서 대조했다. 등록93/주소93/누락0·직접34/48·배열4/4·290.312500MiB에서 GPU28523 활성화 성공·CPU fallback0이다. 재계획 정렬 제외14·포함14595→14581·구간82→80, collapsecheck의 등록됐지만 제외14/이미지 제외0/전체폴백14로 실패했다. 원시 reason=COLLAPSE_PREFLIGHT_FAILED, 분석 IMAGEOWN=FAIL reason=ORDERING_FALLBACK_DESIGN_LIMIT.

같은 정렬 묶음의 활성 장벽 때문에 비이미지14개가 독립 GPU renderer에 남는다. 전체폴백0 기준/카운터/정렬 보호는 유지한다. 필요한 renderer/policy 변경은 Claude 소유 금지 영역이라 지정된 설계 한계에서 중단·순서 보존 설계 수정을 결과 문서에 인계했다. 특정14개 ID·장벽 사이 위치는 미확정이며 모든 통합 설계의 불가능 판정은 아니다. 원인 수정 전 같은333단계 반복 요청 없음·새계획 없음.

O/O2·G/N 반복0은 통합픽셀 증명이 아니며 종료FastMoveOriginal 복구Rec0/0·native_contexts0이다. 통합픽셀/FPS/+20%/30ms 끊김/전곡/설치복원NA. 코드/생성기/기존JSON/tag/셰이더/번들/설정/제어파일 보존·문서5개와 자체분석만 작성. 게임 디컴파일/설치DLL 읽기 전용 ILScan·보호283개SHA·10줄요약·diff 검증, 코드 변경 없어3빌드/LoadCheck/기존검사 반복0회다. 게임·설치·커밋·푸시·장치·Editor 실행0회·자동 장치 직접 중지 없음. 상세결과/out/imageown8 참조. 아래7회차 미실행 표기는 이전 이력이다.

## 회차 7 — 17:05 추가 지시 적용, UV 범위 검사로 대체·게임 미실행

외부run6 직접38줄 원시158~491행 순서대조. 등록93/주소93/누락0·직접필요34/48·배열4/4·필요38/선언52슬롯·290.312500MiB·준비82구간14595개/59복사 후 live on의 image collapse changed mip/wrap에서 실패했다. Rec11821/11821 rollback·종료native_contexts0·CPU O/O2반복0이며 통합픽셀/FPS는NA다. 실패ID/mip/wrap값은 미기록이다.

지시서 끝17:05추가 지시가 이전32상한중단보다 우선한다. Claude의 정책48/번들은 보존하고 허용helper의 기존RequireLive문 하나를 이미지검사 호출한줄로 바꿨다. 이미지코드는 실제업로드된 변형UV8성분의[-1e-4,1+1e-4]범위/유한성/길이와mip1을 검사한다. UV오류image_uv_outside_unit_range/item/texture진단·공유wrap불변·기존주소/슬롯/복구/32바이트부분업로드 유지다.

격리3빌드 오류0/0/0·경고48/36/40·LoadCheck279/229/281곳틀림0·실제3메서드330검사/생성기8검사통과. 의도한코드2개이외 보호280개SHA유지·기존333단계JSON/생성기/tag보존·문서5개/10줄/diff검증. IMAGEOWN=FAIL reason=SAMPLING_FIX_NOT_RUN; 통합픽셀/FPS/+20%/폴백/끊김/전곡/설치복원NA. 게임·설치·커밋·푸시·장치·Editor·설정/제어변경0회. 기존333단계는 새managed코드로 외부확인대상이며 이번실행/새계획없음. 상세결과/out/imageown7참조.

## 회차 6 — Codex 외부 run5 분석, 지정된 설계 한계로 중단

외부run5 직접79줄 원시158~432행 순서대조. 창[257.5,270.25]에서 변형10개 선택·93개 제외 후 등록93/주소89/누락4(직접4)·직접필요36/32·배열4/4·필요40슬롯/선언36·282.3125MiB다. live prepare에서 중단, 통합픽셀/FPS/끊김/복원은NA. 크기·포맷44그룹/93개와 누락4개를 결과에 기록하고 지정된 설계한계 중단조건을 적용한다. 런타임/생성기/기존JSON 변경·새계획/반복시험 요청 없음. 형식별 은행/아틀라스 설계는Claude 담당. 동시작업의 정책48/셰이더 변경을 관측했으나 손대거나 되돌리지 않았고 32상한 외부run5와 혼합하지 않는다. IMAGEOWN=FAIL reason=WINDOW_TEXTURE_CAPACITY_EXCEEDED. 게임·설치·커밋·푸시·장치·Editor·설정/제어변경·빌드 반복0회. 상세결과/10줄/out/imageown6 참조.

## 회차 5 — Codex 창 범위 등록·새 계획 생성, 게임 미실행

외부run4 직접37줄 원시순서대조. 등록185/주소149/누락36(직접36)·직접필요68/32·배열3/4·318.75MiB에서 live prepare 실패, 통합픽셀/FPS NA. 곡전체 ReadyQuad 등록을 소유 대상별 창[257.5,270.25] 이벤트+직전상태+기존물리스프라이트 집합으로 좁히고 coverage/collapsecheck도 동기화했다. 숨은소유 업로드대상은 유지·32/4상한/가드 유지·초과그룹로그 추가. 새accept는 활성화257.5초·333단계abba3-window 생성만, 기존명시12출력/기존JSON 보존. 격리3빌드 오류0/0/0·경고48/36/40·LoadCheck279/229/281곳 틀림0·실제timeline+추출3메서드227/생성기8검사 통과·보호14SHA유지. IMAGEOWN=FAIL reason=WINDOW_COVERAGE_NOT_RUN, 창축소후 registered/missing/direct_required/arrays_used·통합픽셀/FPS/끊김/전곡/설치복원NA. 게임·설치·커밋·푸시·장치·Editor·설정/제어변경0회. 새coverage가 넘치면 수치기록후 중단·소유자설계 인계, 상세결과/10줄/out/imageown5 참조.


## 회차 4 — Codex 분석·미래 이미지 주소 검증, 외부 새 시험 없음

- IMAGEOWN=FAIL reason=TEXTURE_ADDRESS_MISSING; 통합 픽셀/FPS/끊김 NA.
- 외부run3 직접35줄 원시대조. 준비82구간·14595포함·배열3/117복사·318.75MiB·직접32/32·폴백0 뒤 live on의 주소누락으로 실패. Rec11821/11821복원·종료native_contexts0.
- 준비당시 현재항목만 검사해 미래변형 주소누락을 놓치는 계약 수정. 전체등록합집합 대 실제주소를 준비중검사, CPU기록분리 전에 실패·직접/배열분류 로그. helper실패시 등록됐는지 구분, 기존가드 유지.
- 누락ID/총요청수는 옛로그에 없어 실제슬롯초과/자산ID변화 세부귀속 미확정. 실제수용량증가·통합불가능 판정 아님. renderer/policy/셰이더 배정은 소유자 인계.
- 격리3빌드 오류0·LoadCheck279/229/281곳 틀림0·실제메서드추출20검사·기존생성기8검사 통과. 보호14개SHA/기존JSON/tag/생성기/번들 유지.
- 새계획/새시험요청 없음·소유자원인수정 전 같은333단계 재시도 중단 필요. 게임·설치·커밋·푸시·장치·Editor·설정/제어변경0회. 상세결과·10줄·imageown장부 참조.

## 회차 3 — Codex 분석·통합 선행 검사, 외부 새 시험 없음

- IMAGEOWN=FAIL reason=INTEGRATED_ABBA_NOT_RUN; 독립 소유 pixel_max1, 통합 픽셀·FPS·끊김 NA.
- 외부 run2 직접78줄 원시 순서 일치. 260/262/270초 GPU384/172/61(max1), pose동일, CPU정점489/280/258(max1), 반복/복원0.
- 두 진단의 개선 근거 없음·잔여 원인 미확정·허용3 이내. 기본 pose off·수식 유지, 같은 diag1/diag2 반복 금지.
- 기존 통합 연결 존재. 새 collapsecheck는 현재/변형 주소 누락·등록됐지만 제외된 항목/이미지·폴백을 기록, 정렬 보호 유지.
- 기본 accept333단계 생성만: 통합 픽셀3개·최대7장 반복/복원·무폴백 선행 후 ABBA. 실패 시 ABBA 거부. 번들 지원 확인 전 실행 불가.
- 빌드3종 오류0·LoadCheck279/229/281곳 틀림0·생성기8검사·과거 명시 출력12사례 동일. 소유자/기존JSON/native/bundle 해시 유지.
- 게임·설치·커밋·푸시·장치·Editor·설정/제어 변경0회. 상세 codex-imageown-result.md·imageown-autoloop-ledger.md 참조.

## 회차 2 — Codex 분석·수정, 외부 새 시험 없음

- IMAGEOWN=FAIL reason=DIAG2_NOT_RUN; 실제 FPS·원본 대비 픽셀·끊김 NA.
- 외부 직접 증거42줄 대조. GPU28523·CPU fallback0·refreshpose0/4976호출·정점주입286개, 반복/복원 차이0은 관측했으나 O/G·O/D·O/U 비교 전 중단.
- 원인 확정: 기존 diag1의9개 이름이 상한8 위반. FastMoveOriginal은 실패 후 종료 복원 중 발생했으며 가드 보존.
- 새 기본 diag2 165단계는 CPU 반복 대조/저장 후 O2 재사용·최대8장·후보 diff/PNG 즉시 기록. 생성만 했으며 같은 diag1 반복 금지.
- renderer 연결2곳 현재 존재. 픽셀 원인 미확정으로 ABBA 승격 보류.3빌드 오류0·LoadCheck 틀림0·생성기7검사·과거 출력8사례 호환.
- 게임·설치·커밋·푸시·장치 호출·Editor·설정/제어 변경0회. 상세 codex-imageown-result.md·imageown-autoloop-ledger.md.

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

## 회차 2
- Codex 요약: 이전 진단이 캡처 9장을 요청해 상한 8장에서 중단된 원인을 수정했습니다. 새 165단계 계획을 준비했고, 빌드 3종·LoadCheck·생성기 검사 7개가 통과했습니다.

게임 실행·설치·커밋은 하지 않았습니다. 실제 픽셀·FPS는 미검증이므로 `IMAGEOWN=FAIL`을 유지했습니다.

[결과와 다음 단계](/E:/StutterFixWork/wt-gpu/docs/handoff/codex-imageown-result.md)
- 시험 핵심 줄:
  - [StutterFix] [gpudeco] cost_probe enabled=False frames=0 cpu_only=True advance_ms=0.000000 dirty_alpha_ms=0.000000 source_upload_ms=0.000000 dirty_calls_avg=0.000000 dirty_bytes_avg=0.000 source_slots
  - [StutterFix] [gpudeco] prepare_ms=8549.611400 phase=live_prepare slices=1 owned_union=28523 total_runs=28523 total_submeshes=28523 live_kernel=EvaluateLive_sourceE native_sort=True native_copy=True ei
  - [StutterFix] [gpudeco] on=True scope=song_union owned=28523 active_sources=2135 slice=0 slice_owned=28523 runs=28523 submeshes=28523 transition_ms=158.285900 prepare_ms=8549.611400
  - [StutterFix] [gpudeco] cost scope=live_off frames=40 cpu_scope=managed_only native_copy_accounting=G2_window_stats total_ms=0.367660 progress_ms=0.011843 uniform_ms=0.007233 dispatch_ms=0.224128 misc_
  - [StutterFix] [gpudeco] on=False owned=0 originals_restored=True reason=live_off song=260.019671 resumed_recs=11821 resume_expected=11821 unobserved_order=0 restore_ms=12.192900
  - [StutterFix] [gpudeco] on=True scope=song_union owned=28523 active_sources=2135 slice=0 slice_owned=28523 runs=28523 submeshes=28523 transition_ms=159.747200 prepare_ms=8549.611400
  - [StutterFix] [gpudeco] cost scope=live_off frames=58 cpu_scope=managed_only native_copy_accounting=G2_window_stats total_ms=0.421598 progress_ms=0.011693 uniform_ms=0.006997 dispatch_ms=0.232302 misc_
  - [StutterFix] [gpudeco] on=False owned=0 originals_restored=True reason=live_off song=260.019671 resumed_recs=11821 resume_expected=11821 unobserved_order=0 restore_ms=11.611800
  - [StutterFix] [gpudeco] on=True scope=song_union owned=28523 active_sources=2041 slice=0 slice_owned=28523 runs=28523 submeshes=28523 transition_ms=158.295900 prepare_ms=8549.611400
  - [StutterFix] [gpudeco] cost scope=live_off frames=98 cpu_scope=managed_only native_copy_accounting=G2_window_stats total_ms=0.391962 progress_ms=0.012028 uniform_ms=0.006937 dispatch_ms=0.222619 misc_
  - [StutterFix] [gpudeco] on=False owned=0 originals_restored=True reason=live_off song=262.014330 resumed_recs=11186 resume_expected=11186 unobserved_order=0 restore_ms=13.595600
  - [StutterFix] [gpudeco] on=True scope=song_union owned=28523 active_sources=2041 slice=0 slice_owned=28523 runs=28523 submeshes=28523 transition_ms=259.291900 prepare_ms=8549.611400

## 회차 3
- Codex 요약: 외부 결과를 분석하고 통합 선행 검사와 새 333단계 계획을 준비했습니다. 독립 이미지 소유의 최대 픽셀 차이는 세 시점 모두 1로 허용치 이내입니다.

빌드 3종·LoadCheck·생성기 검사 8개가 통과했습니다. 게임 실행·설치·커밋은 하지 않았습니다. 통합 픽셀과 FPS는 미측정이라 `IMAGEOWN=FAIL`을 유지했습니다.

[결과와 남은 조건](E:/StutterFixWork/wt-gpu/docs/handoff/codex-imageown-result.md)


- 시험 핵심 줄:
  - [StutterFix] [gpudeco] cost_probe enabled=False frames=0 cpu_only=True advance_ms=0.000000 dirty_alpha_ms=0.000000 source_upload_ms=0.000000 dirty_calls_avg=0.000000 dirty_bytes_avg=0.000 source_slots
  - [StutterFix] [gpudeco] DRAWCOLLAPSE_TIE_EXCLUDED owned_items=0 active_buckets=73
  - [StutterFix] [gpudeco] DRAWCOLLAPSE_PREPARED spans=82 included=14595 arrays=3 bytes=334233600 copy_calls=117 texture_fallback_items=0 texture_fallback_dispatch=False submeshes_per_renderer=1 direct_ev
  - [StutterFix] [gpudeco] prepare_ms=9769.198100 phase=live_prepare slices=1 owned_union=28523 total_runs=28523 total_submeshes=28523 live_kernel=EvaluateLive_sourceE native_sort=True native_copy=True ei

## 회차 4
- Codex 요약: 이미지 텍스처 주소 누락을 준비 단계에서 검출하도록 보강했습니다. 빌드 3종·LoadCheck·검사 28개가 통과했습니다.

실제 통합은 미해결로 `IMAGEOWN=FAIL`입니다. Claude 소유 코드에 남은 확인 사항과 동일 시험 반복 중단 조건을 [결과 문서](E:/StutterFixWork/wt-gpu/docs/handoff/codex-imageown-result.md)에 기록했습니다.

게임 실행·설치·커밋은 하지 않았습니다.
- 시험 핵심 줄:
  - [StutterFix] [gpudeco] cost_probe enabled=False frames=0 cpu_only=True advance_ms=0.000000 dirty_alpha_ms=0.000000 source_upload_ms=0.000000 dirty_calls_avg=0.000000 dirty_bytes_avg=0.000 source_slots
  - [StutterFix] [gpudeco] IMAGEOWN=FAIL fps_before=NA fps_after=NA gain_pct=NA pixel_max=NA reason=COLLAPSE_TEXTURE_COVERAGE

## 회차 5
- Codex 요약: 이미지 변형 등록을 준비 창 `[257.5, 270.25]` 기준으로 수정하고, 새 333단계 계획을 생성했습니다.

빌드 3종 오류 0, LoadCheck 불일치 0, 코드 검사 227개·생성기 검사 8개가 통과했습니다.

게임 실행·설치·커밋은 하지 않았습니다. 실제 슬롯 수·통합 픽셀·FPS는 미검증이므로 `IMAGEOWN=FAIL`을 유지했습니다.

[결과와 남은 조건](E:/StutterFixWork/wt-gpu/docs/handoff/codex-imageown-result.md)


- 시험 핵심 줄:
  - [StutterFix] [gpudeco] cost_probe enabled=False frames=0 cpu_only=True advance_ms=0.000000 dirty_alpha_ms=0.000000 source_upload_ms=0.000000 dirty_calls_avg=0.000000 dirty_bytes_avg=0.000 source_slots
  - [StutterFix] [gpudeco] IMAGEOWN=FAIL fps_before=NA fps_after=NA gain_pct=NA pixel_max=NA reason=COLLAPSE_TEXTURE_COVERAGE

## 회차 6
- Codex 요약: 지시대로 분석하고, 설계 한계 중단 조건을 적용했습니다. `IMAGEOWN=FAIL`입니다.

- 창 축소 후에도 직접 텍스처 **36개/상한 32개**, **4개 누락**
- 통합 픽셀·FPS는 미측정
- 결과·장부·10줄 요약 갱신 및 검증 완료
- 게임 실행·설치·커밋 없음

[상세 결과](E:/StutterFixWork/wt-gpu/docs/handoff/codex-imageown-result.md)
- 시험 핵심 줄:
  - [StutterFix] [gpudeco] cost_probe enabled=False frames=0 cpu_only=True advance_ms=0.000000 dirty_alpha_ms=0.000000 source_upload_ms=0.000000 dirty_calls_avg=0.000000 dirty_bytes_avg=0.000 source_slots
  - [StutterFix] [gpudeco] DRAWCOLLAPSE_TIE_EXCLUDED owned_items=0 active_buckets=73
  - [StutterFix] [gpudeco] DRAWCOLLAPSE_PREPARED spans=82 included=14595 arrays=4 bytes=304414720 copy_calls=59 texture_fallback_items=0 texture_fallback_dispatch=False submeshes_per_renderer=1 direct_eva
  - [StutterFix] [gpudeco] prepare_ms=9791.141900 phase=live_prepare slices=1 owned_union=28523 total_runs=28523 total_submeshes=28523 live_kernel=EvaluateLive_sourceE native_sort=True native_copy=True ei

## 회차 7
- Codex 요약: 17:05 추가 지시까지 반영해 wrap 검사를 UV 범위·mip 검사로 교체했습니다.

- 빌드 3종 오류 0, LoadCheck 불일치 0
- 오프라인 검사 330개·생성기 검사 8개 통과
- 게임 실행·설치·커밋 없음

실게임 픽셀·FPS는 미검증이므로 `IMAGEOWN=FAIL`을 유지했습니다. [결과 문서](/E:/StutterFixWork/wt-gpu/docs/handoff/codex-imageown-result.md)


- 시험 핵심 줄:
  - [StutterFix] [gpudeco] cost_probe enabled=False frames=0 cpu_only=True advance_ms=0.000000 dirty_alpha_ms=0.000000 source_upload_ms=0.000000 dirty_calls_avg=0.000000 dirty_bytes_avg=0.000 source_slots
  - [StutterFix] [gpudeco] DRAWCOLLAPSE_TIE_EXCLUDED owned_items=0 active_buckets=73
  - [StutterFix] [gpudeco] DRAWCOLLAPSE_PREPARED spans=82 included=14595 arrays=4 bytes=304414720 copy_calls=59 texture_fallback_items=0 texture_fallback_dispatch=False submeshes_per_renderer=1 direct_eva
  - [StutterFix] [gpudeco] prepare_ms=9557.028600 phase=live_prepare slices=1 owned_union=28523 total_runs=28523 total_submeshes=28523 live_kernel=EvaluateLive_sourceE native_sort=True native_copy=True ei
  - [StutterFix] [gpudeco] on=True scope=song_union owned=28523 active_sources=2135 slice=0 slice_owned=28523 runs=28523 submeshes=28523 transition_ms=163.495300 prepare_ms=9557.028600
  - [StutterFix] [gpudeco] DRAWCOLLAPSE_TIE_EXCLUDED owned_items=14 active_buckets=251
  - [StutterFix] [gpudeco] IMAGEOWN=FAIL fps_before=NA fps_after=NA gain_pct=NA pixel_max=NA reason=COLLAPSE_PREFLIGHT_FAILED
  - [StutterFix] [gpudeco] cost scope=recovery:FastMoveOriginal frames=17 cpu_scope=managed_only native_copy_accounting=G2_window_stats total_ms=0.557471 progress_ms=0.013518 uniform_ms=0.008506 dispatch_
  - [StutterFix] [gpudeco] on=False owned=0 originals_restored=True reason=recovery:FastMoveOriginal song=260.019671 resumed_recs=0 resume_expected=0 unobserved_order=0 restore_ms=6.196000

## 회차 8
- Codex 요약: 지시된 설계 한계에서 기록하고 중단했습니다. `IMAGEOWN=FAIL`입니다.

- GPU 소유 28,523개 활성화 성공, 텍스처 누락 0개
- 비이미지 장식 14개의 정렬 제외가 ‘전체 폴백 0개’ 조건을 막음
- 해결에 필요한 renderer·정렬 정책은 수정 금지 영역
- 결과·장부·10줄 요약 갱신, 보호 파일 283개와 문서 검증 통과
- 게임 실행·설치·커밋 없음

[상세 결과](E:/StutterFixWork/wt-gpu/docs/handoff/codex-imageown-result.md)


- 시험 핵심 줄:
  - [StutterFix] [gpudeco] cost_probe enabled=False frames=0 cpu_only=True advance_ms=0.000000 dirty_alpha_ms=0.000000 source_upload_ms=0.000000 dirty_calls_avg=0.000000 dirty_bytes_avg=0.000 source_slots
  - [StutterFix] [gpudeco] DRAWCOLLAPSE_TIE_EXCLUDED owned_items=0 active_buckets=73
  - [StutterFix] [gpudeco] DRAWCOLLAPSE_PREPARED spans=82 included=14595 arrays=4 bytes=304414720 copy_calls=59 texture_fallback_items=0 texture_fallback_dispatch=False submeshes_per_renderer=1 direct_eva
  - [StutterFix] [gpudeco] prepare_ms=9579.658300 phase=live_prepare slices=1 owned_union=28523 total_runs=28523 total_submeshes=28523 live_kernel=EvaluateLive_sourceE native_sort=True native_copy=True ei
  - [StutterFix] [gpudeco] on=True scope=song_union owned=28523 active_sources=2135 slice=0 slice_owned=28523 runs=28523 submeshes=28523 transition_ms=174.011600 prepare_ms=9579.658300
  - [StutterFix] [gpudeco] DRAWCOLLAPSE_TIE_EXCLUDED owned_items=14 active_buckets=251
  - [StutterFix] [gpudeco] IMAGEOWN=FAIL fps_before=NA fps_after=NA gain_pct=NA pixel_max=NA reason=COLLAPSE_PREFLIGHT_FAILED
  - [StutterFix] [gpudeco] cost scope=recovery:FastMoveOriginal frames=17 cpu_scope=managed_only native_copy_accounting=G2_window_stats total_ms=0.621294 progress_ms=0.013582 uniform_ms=0.008865 dispatch_
  - [StutterFix] [gpudeco] on=False owned=0 originals_restored=True reason=recovery:FastMoveOriginal song=260.019671 resumed_recs=0 resume_expected=0 unobserved_order=0 restore_ms=6.481300

## 회차 1
- Codex 요약: 17:15 추가 지시에 맞춰 픽셀·ABBA 판정을 수정했습니다. 이미지 제외는 실패로 처리하고, 비이미지 정렬 보호 폴백은 1% 이하로 허용합니다.

- 빌드 3종·LoadCheck 통과
- 판정 검사 28개·생성기 검사 8개 통과
- 새 333단계 계획 생성 완료
- 게임 실행·설치·커밋 없음

실게임 픽셀·FPS는 미측정이므로 `IMAGEOWN=FAIL`을 유지했습니다. [결과 문서](/E:/StutterFixWork/wt-gpu/docs/handoff/codex-imageown-result.md)


- 시험 핵심 줄:
  - [StutterFix] [gpudeco] cost_probe enabled=False frames=0 cpu_only=True advance_ms=0.000000 dirty_alpha_ms=0.000000 source_upload_ms=0.000000 dirty_calls_avg=0.000000 dirty_bytes_avg=0.000 source_slots
  - [StutterFix] [gpudeco] DRAWCOLLAPSE_TIE_EXCLUDED owned_items=0 active_buckets=73
  - [StutterFix] [gpudeco] DRAWCOLLAPSE_PREPARED spans=82 included=14595 arrays=4 bytes=300220416 copy_calls=58 texture_fallback_items=0 texture_fallback_dispatch=False submeshes_per_renderer=1 direct_eva
  - [StutterFix] [gpudeco] prepare_ms=9666.723700 phase=live_prepare slices=1 owned_union=28523 total_runs=28523 total_submeshes=28523 live_kernel=EvaluateLive_sourceE native_sort=True native_copy=True ei
  - [StutterFix] [gpudeco] on=True scope=song_union owned=28523 active_sources=2135 slice=0 slice_owned=28523 runs=28523 submeshes=28523 transition_ms=167.138000 prepare_ms=9666.723700
  - [StutterFix] [gpudeco] DRAWCOLLAPSE_TIE_EXCLUDED owned_items=14 active_buckets=251
  - [StutterFix] [gpudeco] cost scope=live_off frames=45 cpu_scope=managed_only native_copy_accounting=G2_window_stats total_ms=0.455113 progress_ms=0.012438 uniform_ms=0.038098 dispatch_ms=0.289280 misc_
  - [StutterFix] [gpudeco] on=False owned=0 originals_restored=True reason=live_off song=260.019671 resumed_recs=11821 resume_expected=11821 unobserved_order=0 restore_ms=13.404400
  - [StutterFix] [gpudeco] on=True scope=song_union owned=28523 active_sources=2041 slice=0 slice_owned=28523 runs=28523 submeshes=28523 transition_ms=163.486400 prepare_ms=9666.723700
  - [StutterFix] [gpudeco] DRAWCOLLAPSE_TIE_EXCLUDED owned_items=0 active_buckets=317
  - [StutterFix] [gpudeco] cost scope=live_off frames=90 cpu_scope=managed_only native_copy_accounting=G2_window_stats total_ms=0.439686 progress_ms=0.011822 uniform_ms=0.035719 dispatch_ms=0.278926 misc_
  - [StutterFix] [gpudeco] on=False owned=0 originals_restored=True reason=live_off song=262.014330 resumed_recs=11186 resume_expected=11186 unobserved_order=0 restore_ms=12.331500

## 회차 2
- Codex 요약: ABBA 순번 오류를 수정하고 끊김 진단을 추가했습니다. 빌드 3종·LoadCheck·판정 검사 56개·생성기 검사 8개가 통과했습니다.

최신 실측은 픽셀 최대 차이 3/3/2, B2 창 끊김 4회로 `IMAGEOWN=FAIL`입니다. 성능 개선은 미입증입니다. 새 333단계 계획은 생성만 했으며, 게임 실행·설치·커밋은 하지 않았습니다.

[상세 결과](E:/StutterFixWork/wt-gpu/docs/handoff/codex-imageown-result.md)


- 시험 핵심 줄:
  - [StutterFix] [gpudeco] cost_probe enabled=False frames=0 cpu_only=True advance_ms=0.000000 dirty_alpha_ms=0.000000 source_upload_ms=0.000000 dirty_calls_avg=0.000000 dirty_bytes_avg=0.000 source_slots
  - [StutterFix] [gpudeco] DRAWCOLLAPSE_TIE_EXCLUDED owned_items=0 active_buckets=73
  - [StutterFix] [gpudeco] DRAWCOLLAPSE_PREPARED spans=82 included=14595 arrays=4 bytes=304414720 copy_calls=59 texture_fallback_items=0 texture_fallback_dispatch=False submeshes_per_renderer=1 direct_eva
  - [StutterFix] [gpudeco] prepare_ms=9696.609200 phase=live_prepare slices=1 owned_union=28523 total_runs=28523 total_submeshes=28523 live_kernel=EvaluateLive_sourceE native_sort=True native_copy=True ei
  - [StutterFix] [gpudeco] on=True scope=song_union owned=28523 active_sources=2135 slice=0 slice_owned=28523 runs=28523 submeshes=28523 transition_ms=168.499500 prepare_ms=9696.609200
  - [StutterFix] [gpudeco] DRAWCOLLAPSE_TIE_EXCLUDED owned_items=14 active_buckets=251
  - [StutterFix] [gpudeco] cost scope=live_off frames=45 cpu_scope=managed_only native_copy_accounting=G2_window_stats total_ms=0.444211 progress_ms=0.012393 uniform_ms=0.037931 dispatch_ms=0.277120 misc_
  - [StutterFix] [gpudeco] on=False owned=0 originals_restored=True reason=live_off song=260.019671 resumed_recs=11821 resume_expected=11821 unobserved_order=0 restore_ms=13.673400
  - [StutterFix] [gpudeco] on=True scope=song_union owned=28523 active_sources=1984 slice=0 slice_owned=28523 runs=28523 submeshes=28523 transition_ms=161.360800 prepare_ms=9696.609200
  - [StutterFix] [gpudeco] DRAWCOLLAPSE_TIE_EXCLUDED owned_items=0 active_buckets=317
  - [StutterFix] [gpudeco] IMAGEOWN=FAIL fps_before=NA fps_after=NA gain_pct=NA pixel_max=4 reason=INTEGRATED_PIXEL_PREFLIGHT_FAILED
  - [StutterFix] [gpudeco] cost scope=recovery:FastMoveOriginal frames=87 cpu_scope=managed_only native_copy_accounting=G2_window_stats total_ms=0.446245 progress_ms=0.011741 uniform_ms=0.037036 dispatch_

## 회차 3
- Codex 요약: `IMAGEOWN=FAIL`입니다. 262초의 최대 픽셀 차이 4가 **통합 렌더링 단계에서 발생**함을 확인했습니다. 독립 이미지 GPU는 최대 차이 1이었습니다.

정확한 내부 원인은 미확정이며, 통합 렌더러·셰이더는 수정 허용 범위 밖이라 근거와 후속 작업을 [결과 문서](E:/StutterFixWork/wt-gpu/docs/handoff/codex-imageown-result.md)에 기록하고 중단했습니다.

문서 5개와 10줄 요약을 갱신했습니다. 보호 파일 285개는 동일하며, 게임 실행·설치·커밋은 모두 0회입니다.

- 시험 핵심 줄:
  - [StutterFix] [gpudeco] cost_probe enabled=False frames=0 cpu_only=True advance_ms=0.000000 dirty_alpha_ms=0.000000 source_upload_ms=0.000000 dirty_calls_avg=0.000000 dirty_bytes_avg=0.000 source_slots
  - [StutterFix] [gpudeco] DRAWCOLLAPSE_TIE_EXCLUDED owned_items=0 active_buckets=73
  - [StutterFix] [gpudeco] DRAWCOLLAPSE_PREPARED spans=82 included=14595 arrays=4 bytes=304414720 copy_calls=59 texture_fallback_items=0 texture_fallback_dispatch=False submeshes_per_renderer=1 direct_eva
  - [StutterFix] [gpudeco] prepare_ms=9768.597100 phase=live_prepare slices=1 owned_union=28523 total_runs=28523 total_submeshes=28523 live_kernel=EvaluateLive_sourceE native_sort=True native_copy=True ei
  - [StutterFix] [gpudeco] on=True scope=song_union owned=28523 active_sources=2135 slice=0 slice_owned=28523 runs=28523 submeshes=28523 transition_ms=167.552400 prepare_ms=9768.597100
  - [StutterFix] [gpudeco] DRAWCOLLAPSE_TIE_EXCLUDED owned_items=14 active_buckets=251
  - [StutterFix] [gpudeco] cost scope=live_off frames=45 cpu_scope=managed_only native_copy_accounting=G2_window_stats total_ms=0.432471 progress_ms=0.013749 uniform_ms=0.037313 dispatch_ms=0.270531 misc_
  - [StutterFix] [gpudeco] on=False owned=0 originals_restored=True reason=live_off song=260.019671 resumed_recs=11821 resume_expected=11821 unobserved_order=0 restore_ms=12.151700
  - [StutterFix] [gpudeco] on=True scope=song_union owned=28523 active_sources=2040 slice=0 slice_owned=28523 runs=28523 submeshes=28523 transition_ms=158.378900 prepare_ms=9768.597100
  - [StutterFix] [gpudeco] DRAWCOLLAPSE_TIE_EXCLUDED owned_items=0 active_buckets=317
  - [StutterFix] [gpudeco] IMAGEOWN=FAIL fps_before=NA fps_after=NA gain_pct=NA pixel_max=5 reason=INTEGRATED_PIXEL_PREFLIGHT_FAILED
  - [StutterFix] [gpudeco] cost scope=recovery:FastMoveOriginal frames=88 cpu_scope=managed_only native_copy_accounting=G2_window_stats total_ms=0.426973 progress_ms=0.012334 uniform_ms=0.036264 dispatch_
