
## Codex 회차 4 분석: run3 후 현재 구조 중단 (2026-10-25, 게임 실행 없음)

- 외부 map5/run3: 3440×1440@164.999·소유 23547·A visible 1880, ABBA **165.929966/152.382090/152.765481/167.762695 FPS**. A/B 평균 166.846331/152.573786(**−8.5543%**), 창별 환산 시간 +0.560496ms. B 활성 renderer 69만 목표 통과, 활성 submesh 1478·엔진 draw 3815·FPS 실패, 네 창 30ms 초과 0.
- 픽셀 262초 원본↔독립/원본↔병합/독립↔병합=2/1434/1432(max4), 270초 0/11477/11477(max2), 복원/노이즈 0. retry/song_end/reopen/Shutdown **4/4**, 자원 검사 각각 48512개·unity_alive=0·retired_tokens=1/1. 전체 UMM reload/물리 GPU 메모리는 미측정. 최종 **RENDERMERGE=PIXEL_FAIL**, 성능 실패도 존재한다.
- 현재 원시 `night-results/al-gm-3.log`와 결과 **15줄**을 순서·중복까지 대조했다. `BuildLiveRun`은 고정 submesh·인덱스·재질 슬롯을 남기고 `RefreshGpuMergeGroup`은 renderer 단위로만 켜고 끈다. hidden quad 퇴화가 제출 슬롯을 없애지 않아 260~270초 준비 submesh 3230~3279개가 남는다. 현재 방식 한계이며 다른 모든 방식의 불가능·픽셀 원인 확정은 아니다.
- 인계 지시대로 **현재 고정 서브메시 방식 반복 중단**. 런타임·게이트·생성기·기존 JSON/tag·설정·제어 파일 유지, 새 계획/시험 요청 없음. 자동 장치를 직접 끄지 않았다. 오프라인 감사 JSON·게임 디컴파일/설치 DLL ILScan·문서만 확인, 코드 변경이 없어 빌드·기존 검사 반복 없음.
- 이번 게임 실행·설치·커밋·푸시·자동 장치 호출 **0회**. 문서 5개만 갱신, T2·과거 2560 수치 보존. 이번 3440 B는 220/400 FPS까지 2.008762/4.054216ms 감소 필요. 상세 `codex-render-merge-result.md`와 10줄 요약 참조.

## Codex 회차 3 수정 (2026-10-25, 게임 실행 없음)

- 최신 run2 실제 ABBA는 A0/B1/B2/A3 **166.844797/77.992570/77.943161/161.787018 FPS**, 소유 23,547·A visible 1,880이다. B 할당 renderer 4,875·엔진 draw 최대 2,527·30ms 초과 3/4회, 픽셀 262초 439,069(max33)/270초 3,207(max2)로 실패했다. retry/song_end/reopen 통과 후 111,382개 자원 mark를 reader의 100,000개 제한이 거부해 최종 ERROR, stability=3/4다. 원시 로그와 결과 11줄 일치.
- 지원된 GPU 스프라이트의 휴면 mesh/border 보조 객체만 병합 경계에서 제외하고 CPU fallback/미확인 객체 경계를 보존했다. 분할 원인별 로그를 추가했다. singleton 전체 원인 귀속은 실측 대기다.
- 알파 변경의 Mesh.SetIndices를 제거하고 별도 CopyMappedRun 복사표로 숨은 quad를 퇴화시킨다. 정점 버퍼 참조를 준비 후 고정한다. 자원 mark reader/writer를 동일한 1MiB·정확 payload 길이 기준으로 맞췄다. 기존 정렬·소유·픽셀·성능·복구 게이트 유지.
- Dev/Player/PlayerAuto 오류 0(경고 46/36/39), 생성기 11개·RenderMergeCheck 74개 통과. 새 계획 `tools/optimization/out/render-merge-map5-steps.json` 333단계 생성만. 기존 JSON/제어 파일/설정 유지.
- map5 `RENDERMERGE=ERROR reason=GAME_NOT_RUN`, 성능·픽셀·정렬·끊김·복원 미검증. 게임 실행·설치·커밋·푸시·자동 장치 호출 0회. 동일 코드 반복 시험 요청 없음. 상세 결과·10줄 요약을 따른다.

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

## 회차 3
- Codex 요약: run2 실패를 분석해 병합 경계·정점 버퍼 갱신·자원 검사 상한을 수정했습니다.

- 빌드 3종 오류 0, 생성기 검사 11개·정책 검사 74개 통과.
- 새 시험 계획과 결과 문서·10줄 요약 갱신.
- 게임 실행·설치·커밋은 하지 않았습니다. 실제 성능·픽셀 개선은 미검증이며 PASS가 아닙니다.

[상세 결과](/E:/StutterFixWork/wt-gpu/docs/handoff/codex-render-merge-result.md)

- 시험 핵심 줄:
  - [StutterFix] [gpudeco] cost_probe enabled=False frames=0 cpu_only=True advance_ms=0.000000 dirty_alpha_ms=0.000000 source_upload_ms=0.000000 dirty_calls_avg=0.000000 dirty_bytes_avg=0.000 source_slots
  - [StutterFix] [gpudeco] rendermerge_prepared start=250.0 end=252.5 items=38 renderers=17 submeshes=29 omitted_admitted_auxiliaries=70641 barriers=1778 split_barrier=16 split_bucket=1 split_layer=0 spli
  - [StutterFix] [gpudeco] rendermerge_prepared start=252.5 end=255.0 items=40 renderers=18 submeshes=30 omitted_admitted_auxiliaries=70641 barriers=1778 split_barrier=17 split_bucket=1 split_layer=0 spli
  - [StutterFix] [gpudeco] rendermerge_prepared start=255.0 end=257.5 items=940 renderers=68 submeshes=730 omitted_admitted_auxiliaries=70641 barriers=1778 split_barrier=25 split_bucket=43 split_layer=0 s
  - [StutterFix] [gpudeco] rendermerge_prepared start=257.5 end=260.0 items=3282 renderers=69 submeshes=2379 omitted_admitted_auxiliaries=70641 barriers=1778 split_barrier=25 split_bucket=44 split_layer=0
  - [StutterFix] [gpudeco] rendermerge_prepared start=260.0 end=262.5 items=4580 renderers=67 submeshes=3277 omitted_admitted_auxiliaries=70641 barriers=1778 split_barrier=24 split_bucket=43 split_layer=0
  - [StutterFix] [gpudeco] rendermerge_prepared start=262.5 end=265.0 items=4632 renderers=69 submeshes=3279 omitted_admitted_auxiliaries=70641 barriers=1778 split_barrier=25 split_bucket=44 split_layer=0
  - [StutterFix] [gpudeco] rendermerge_prepared start=265.0 end=267.5 items=4642 renderers=68 submeshes=3279 omitted_admitted_auxiliaries=70641 barriers=1778 split_barrier=25 split_bucket=43 split_layer=0
  - [StutterFix] [gpudeco] rendermerge_prepared start=267.5 end=270.0 items=4493 renderers=69 submeshes=3230 omitted_admitted_auxiliaries=70641 barriers=1778 split_barrier=26 split_bucket=43 split_layer=0
  - [StutterFix] [gpudeco] rendermerge_prepared start=270.0 end=272.5 items=4875 renderers=62 submeshes=3922 omitted_admitted_auxiliaries=70641 barriers=1778 split_barrier=19 split_bucket=43 split_layer=0
  - [StutterFix] [gpudeco] rendermerge_prepared start=272.5 end=275.0 items=3477 renderers=64 submeshes=2874 omitted_admitted_auxiliaries=70641 barriers=1778 split_barrier=21 split_bucket=43 split_layer=0
  - [StutterFix] [gpudeco] rendermerge_prepared start=275.0 end=277.5 items=931 renderers=63 submeshes=725 omitted_admitted_auxiliaries=70641 barriers=1778 split_barrier=20 split_bucket=43 split_layer=0 s
