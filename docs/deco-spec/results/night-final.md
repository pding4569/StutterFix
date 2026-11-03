1. 실패한 CPU·장벽 알림·타일 합성·재질 복제 실험을 삭제하고 패킷·렌더 병합 연구를 진단 빌드로 제한했다.
2. 최종 Player·PlayerAuto 빌드 오류 0, GpuDecoPlayerCheck 492개 검사 오류 0이다.
3. C#·프로젝트 파일에서 827행 삭제·94행 추가(순감 733행), 고정 정책 11개를 상수화했다.
4. GpuDeco 기본값 0, 설치 DLL 복원본 일치·gpuprobe.dll 없음 확인. 게임·전곡·픽셀 시험은 실행하지 않았다.
5. 푸시·릴리스 없음. 기존 Arche 262초 최대 RGBA 차이 4,4,4,0과 초과 8픽셀은 미해결이다.

2026-11-03, `E:\StutterFixWork\wt-partial`, `claude/gpudeco-cheap`.
시작 커밋 `932545e`. 지침: `E:\StutterFixWork\codex-clean-prompt.md`.
기존 simplify-research/cpu-extreme/engine-extreme 문서를 먼저 확인했다.

## 최종 표

| 정리 | 호출 경로·근거 | 결과 |
|---|---|---|
| IdleFloorUpdate | 일반 Player 제외. DEV/AUTOTEST 설치·명령만 존재. cpu-extreme ABBA -0.023404ms로 판 간 변동 0.088744ms보다 작음 | 파일·설치·명령·컴파일 항목 삭제 |
| HoldMaterialReads | AUTOTEST에서만 명령으로 후크 설치. ABBA -0.002616ms로 조건 내 변동보다 작음 | 파일·명령·컴파일 항목 삭제 |
| NotifyBarrierWatch | Player에서 false, AUTOTEST 명령만 활성화. 관리 경과 0.077812→0.139485ms로 악화 | 후크·인덱스·pending·oracle·플래그 삭제. 기존 8프레임 장벽 검사 유지 |
| TileComposite | AUTOTEST 전용, 최대 픽셀 차이 6으로 불합격 | 합성 구현 352행·호출·정책·명령 삭제 |
| TightAllOwned | 기본 false. 1.7만 개 트림에도 GPU 약 -0.04ms로 보류된 실험 | 일반 스프라이트 트림·통계·호출 삭제. 큰 이미지용 TightVariant 유지 |
| CloneBarrierMaterials / TieAllBarriers | 기본 false. 복제는 원본 재질 변경을 놓쳐 화면 누락, 전체 동점 처리는 Arche 295→128 FPS | 복제 생성·교체·복원·사전·명령과 실패한 동점 분기 삭제. 현재 동적 제외 유지 |
| gpuSpanCausers / gpuBarrierCauses | 선언·Clear만 있고 값 저장·읽기 호출 없음 | 예전 분류 방식의 빈 사전 두 개 삭제 |
| 패킷·compiled target / RenderMerge | 모드·준비·on의 활성 writer는 진단 명령뿐. 일반 Player에는 GpuStep 진입점 없음 | DEV/AUTOTEST 전용 Compile로 제한. Player에서 계획·사전·배열·후크 진입점 제외, PlayerAuto의 기존 진단 유지 |
| 고정 정책 11개 | 저장소 전체 대입 검색에서 writer 없음. AdoptNativeTweens 등 실제 가변 값은 제외 | const로 고정하여 반대 분기를 컴파일 시 제거. 두 DLL의 메타데이터에서도 Literal 검사 |

## 켜진 기능과 유지한 진단

기존 제품 최적화 설정은 유지하며 새 성능 기능을 켜지 않았다. 설치 설정도 GpuDeco=0이다.
아래는 사용자가 GpuDeco=1로 명시적으로 켰을 때 적용되는 기존 기본 정책이며, 현재 GPU 소유 활성 상태라는 뜻은 아니다.

- 렌더 경로: PartialMaskAdmit, SplitByOrder, RecordedDispatch, MinimalDispatch, GlobalVerts,
  DisableOwnedRenderers, CullIdleSpans, ClipOffscreen.
- 준비·수명: PreStartActivate, DeferEndOnResult, ActivateInCatchUp, MidReusePlan,
  ExactBigArray, KeyCache, TightLargeQuads. Flash CPU 라이브 이징과 네이티브 트윈 인수 경로도 유지한다.
- 정렬: KeepBeforeBarrier, NudgeTieSpans, KeepAfterBarrier 및 기존 SkipOrderSweep 기본값.
  진입 전수검사, 기존 8프레임 장벽 감시, 안전 검사 실패 시 독립 렌더러·CPU 복귀는 유지한다.

const 목록은 PartialMaskAdmit, SplitByOrder, RecordedDispatch, ExactBigArray, KeyCache,
FullScanOnReplan(false), KeepBeforeBarrier, NudgeTieSpans, TightMarginTexels(4), TightMaxAreaRatio(0.8), MaxSpans(700)이다.
FullScanOnReplan=false여도 최초 스캔·신규 장벽 추적·캐시 무효 fallback은 유지한다.
측정 도구 phasewindow/gpuprobe/profsamp/framecmp/gpudeco cost는 보존한다.
HideProxies/ClipScale/SkipPausedCompute/DirectDispatch는 GPU 비용 분리·제출 방식 대조에 쓰는 진단 제어라 유지했다.
작업량 admission은 이득 검증 전 후보이며 기본 꺼짐 그대로다. 새 기능으로 채택하지 않았다.

## 검증과 한계

1차 정리: Player 오류 0/경고 49, PlayerAuto 오류 0/경고 47,
GpuDecoPlayerCheck 455개/오류 0. 삭제된 CPU 실험이 두 DLL 모두에 없는지 검사하도록 갱신했다.
2차 렌더 실험 정리도 Player 오류 0/경고 49, PlayerAuto 오류 0/경고 46, 검사 455개/오류 0이다.
기존 compute bundle은 SHA를 유지했다. 번들 속 미사용 TileComposite shader 자산은 런타임 호출이 없으며,
Unity 재패킹은 이번 빌드·오프라인 검증 범위에 넣지 않았다. 과거 증거·재현 명세도 보존한다.
빌드 로그와 검사 출력은 [evidence/night-final](evidence/night-final)에 보존한다.
빌드·메타데이터 검사는 화면 동등성이나 성능 향상 증명이 아니다.

최종 Player 오류 0/경고 50, PlayerAuto 오류 0/경고 46, GpuDecoPlayerCheck 492개/오류 0이다.
Player의 전체 형식 수는 505, PlayerAuto는 891이다. 패킷·병합 저장 형식 6종은 PlayerAuto에만 있고,
실패한 CPU 실험·장벽 복제는 두 빌드에서 빠졌음을 검사했다. 최종 git diff --check도 통과했다.
경고는 숨기지 않았으며 빌드 로그에 남겼다. 이번 작업에서 성능·픽셀 채택 결과를 새로 만들지 않았다.

격리 첫 빌드에서는 공용 BuildLiveRun의 MergeWindow 인자 참조가 남아 오류 1개,
다음 빌드에서는 UpdatePacketClock 두 호출·ClassifyPacketTargetGuards 참조로 오류 3개가 발생했다.
인자·매핑 블록과 호출을 진단 조건으로 묶어 해결했다. 실패 로그 두 개도 증거 폴더에 보존했다.
깨진 상태는 커밋하지 않았고 각 정리 커밋은 두 빌드와 오프라인 검사를 통과한 뒤 기록했다.

시작 932545e 대비 C#·csproj 물리 행 수는 삭제 827/추가 94/순감 733이다.
주석·중괄호·검사 코드도 포함하며, 제품 DLL에서 빠진 실행 명령 수나 성능 절감량과 다르다.
진단 빌드로 옮긴 세 파일의 소스는 보존했으므로 삭제 행에 더하지 않았다.
파일별 행 수·최종 DLL SHA·설치 무결성은 [summary.json](evidence/night-final/summary.json)을 따른다.
설치 DLL과 restore/StutterFix-rc-player.dll의 SHA256은 둘 다
`b0efe68d6ccce3217310da0ad65cba4048c19d2d25f738c97f14d7822898c795`다.
이번 작업에서 게임·Mods 설치·설정 변경은 하지 않았다.

## 사용자 결정 항목

- `GpuDeco` 기본값 변경 여부: 현재 0 유지.
- 릴리스 여부: 현재 로컬 정리만 수행, 푸시·릴리스 없음.
- 남은 픽셀 차이 처리: 기존 Arche 262초 최대 RGBA 4,4,4,0 및 threshold 3 초과 8픽셀을 미해결로 유지.
  이번 정리는 새 픽셀 결과를 만들지 않았으며 대기열의 별도 결과로 판단해야 한다.
