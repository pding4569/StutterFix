GpuDeco 기본값은 0을 유지했고, 푸시는 하지 않았다. 이번 후보에서 기본 켬으로 채택한 성능 변경은 없다.
Flash 32~35는 CPU 라이브 이징 경로로 지원했다. DOTween 대조 180건 불일치 0, Windflower 12초 복귀 제거를 확인했다.
CPU 후보 두 개는 값 대조를 통과했지만 ABBA 차이가 판 간 변동보다 작아 기본 꺼짐·일반 Player 제외로 남겼다.
변경 알림의 Floor 감시 누락을 수정해 Exit·Arche·DDONGSSADA 모두 불일치 0을 확인했지만, 비용 ABBA는 악화돼 기본 꺼짐을 유지했다.
wt-cand에서도 Arche 262초 통합 경로의 최대 RGBA 차이 4,4,4,0과 허용차 초과 8픽셀이 재현돼 픽셀 채택 조건은 미통과다.

## 조건과 판독

2026-11-03, `E:\StutterFixWork\wt-partial`, `claude/gpudeco-cheap`, 시작 `f03037a`.
게임은 전부 `E:\StutterFixWork`에서 `run_locked.py`를 통해 판마다 새 프로세스로 실행했다.
2560×1440, VSync 0, 목표 10000 FPS, FrameGenOutside=0, FrameGenRefresh=false.
게임 실행 중 빌드·분석을 병행하지 않았다. 다른 `engine-prof.py`의 공유 잠금 때문에 기다린 구간도 있다.
CPU·알림 비용은 에디터 `seek`로 Exit 90–100초만 측정했다. Flash는 게임 화면 Windflower 8–22초다.
전체곡이 필요한 변경 알림 종료 검증과 원래 명세의 Arche 262초 대조는 해당 명세를 유지했다.

아래 프레임 ms는 각 판의 로그 FPS로 계산한 `mean(1000/FPS)`다. FPS 표시는 소수점 한 자리이므로 계산 결과의 마지막 자리는 측정 정밀도가 아니다.
관리 비용은 기존 `gpudeco cost`의 **관리 코드 구간 경과 시간**이며, 전체 메인 스레드 실행 시간·순수 CPU busy·GPU 시간과 다르다.
새 함수별 스톱워치 계측은 추가하지 않았다. oracle/값 대조 판을 성능 판에 섞지 않았다.
각 조건 2판의 범위로 흔들림을 확인했으며 통계적 신뢰구간을 산출한 것은 아니다.

## 항목별 결과

| 내용 | 구현 커밋 | 전후 ms: 끔 → 켬 | 판 수 | 기본값 상태 | 검증 방법·판정 |
|---|---|---:|---:|---|---|
| Flash CPU 라이브 이징 | `5d446d8` | Windflower 2.841717 → 4.624561, +1.782844 | 구간 ABBA 4 + 짧은 재확인 | GpuDeco=0 유지 | DOTween Goto 180건 비트 불일치 0; 20/22초에도 GPU 소유 유지·native 오류 0. 기능 수정이며 이 구간 성능 채택 근거는 없음 |
| 일 없는 scrFloor.Update 생략 | `9d11cab` | 3.270179 → 3.246775, -0.023404 | ABBA 4 + oracle 1 | 꺼짐, Player 제외 `27496b1` | 원본 호출 357,725건 색 비트 불일치 0. 꺼짐 판 간 범위 0.088744ms보다 작은 차이: 이득 미확정 |
| 홀드 함수 내부 material 조회 재사용 | `5eb6415` | 3.242023 → 3.239408, -0.002616 | ABBA 4 + oracle 1 | 꺼짐, AUTOTEST 전용 | 같은 호출 내 material 참조 대조 796,949건 불일치 0. 조건 내 범위 0.009460/0.014691ms보다 작은 차이: 이득 미확정 |
| 장식 후크 밖 Floor 장벽의 기존 검사 유지 | `4bfa6a8` | 변경 알림 비용 행 참조 | 수정 후 전체곡 3 | NotifyBarrierWatch=false 유지 | 최초 Exit 110.6초 불일치 1 → 수정 후 Exit·Arche·DDONGSSADA 불일치 0 |
| notify on의 관리 비용 | `4bfa6a8`, 최종 관리 소스 `27496b1` | 관리 경과 0.077812 → 0.139485, +0.061673 | oracle 없는 ABBA 4 | 꺼짐 | 구간 전체는 3.438430 → 3.542367ms. 관리 비용 악화가 두 쌍에서 반복돼 기본값 변경 후보에서 제외 |
| 작업량 기반 일반 활성화 후보 | `27496b1` | 새 성능 비교 미측정 | 실제 준비 경로 3 | admission=false 유지 | 이름 없이 소유 비율·가시 적용률·평균 가시 이동량으로 Arche 허용, Exit·Windflower 거부. 경계/비유한 값 검사 포함 |
| wt-cand Arche 262초 잔여 픽셀 | 측정 당시 wt-cand DLL, 아래 SHA 기록 | 성능 측정 아님 | 원래 명세 1 | 픽셀 채택 보류 | 노이즈 0, A/A2 0, C/I·C/A 최대 4/255 및 초과 8픽셀; I/A 최대 1/255 |
| UnityPlayer+0x1C5747B 심벌 | 결과 문서 커밋 | 전후 시간 미측정 | 오프라인 RVA 조회 | 동작 변경 없음 | 제공된 PDB에서 memcpy_repmovs. 원래 프로파일 리프 6.0%의 복사 대상·호출자는 미확인 |

CPU 후보의 oracle는 생략 조건에 해당하는 원본 실행을 관찰했다. 타일 후보는 원본 Update가 쓰는 색 값, 홀드 후보는 재사용하는 material 참조를 대조했다.
전체곡 모든 상태·모든 모드의 동등성을 증명한 것은 아니다. 두 후보 모두 이득이 불확실해 제품 경로에 넣지 않았다.

## ABBA 원시 수치

| 조건 | A 끔 | B 켬 | B 켬 | A 끔 | 결론 |
|---|---:|---:|---:|---:|---|
| idlefloor, Exit 90–100초 | 310.0 | 308.8 | 307.2 | 301.7 | 차이가 변동 안 |
| holdreads, 같은 구간 | 308.0 | 308.0 | 309.4 | 308.9 | 차이가 변동 안 |
| GpuDeco Flash 수정 후, Windflower 8–22초 | 351.8 | 221.3 | 211.4 | 352.0 | 이 구간 GPU 소유 손실 |
| notify, Exit 90–100초 | 295.4 | 283.2 | 281.4 | 286.4 | 성능 개선 근거 없음 |

단위 FPS. notify의 꺼짐 두 판 자체 범위는 0.106380ms다. 전체 프레임 차이 0.103937ms 하나만으로 정확한 손실 원인을 단정하지 않는다.
반면 기존 관리 구간 경과 시간은 off 0.076847/0.078777ms, on 0.138426/0.140544ms로 두 쌍 모두 증가했다.
이는 모든 장식 후크의 비용을 별도로 계측한 값이 아니므로 증가분 전체를 특정 setter 후크에 귀속하지 않는다.
네 판 모두 active=True, 소유 약 2,996–2,998개, native_errors=0, 전체 복귀 없음이었다.

## Flash 처리와 끝값

`GpuDecoGpu.Supported`는 GPU의 baked/anchor 이징 구현에 해당하는 기존 제한을 유지한다.
새 `LiveSupported`만 0–36을 허용하며, 라이브 effect 준비·활성화·CPU 복귀·패킷 계획에서 사용한다.
GPU에서 직접 Flash를 계산하도록 바꾸거나 compute 번들을 교체하지 않았다.

곡 중 `AdvanceGpuLiveSources`가 활성 source마다 `ZeroTween.Eval`을 한 번 계산해 SourceE에 업로드한다.
`CopyEvent`는 Flash overshoot를 원래 SetEase 규칙대로 정수화한다.
완료 시 위치·색·불투명도처럼 OnComplete setter가 있는 채널은 target을, 크기와 시차 5/6/12/13은 plugin의 이징 끝점 산술을 유지한다.
정적 채널 전환은 저장된 end를 사용하고, Kill(true)의 시작값도 이전 end에서 가져온다.
기존 DecoBake.Core에도 5/6/12/13 끝값 처리가 이미 있어 중복 수정하지 않았다.

`flash_fixture`는 실제 DOTween을 만들어 Flash/InFlash/OutFlash/InOutFlash, 길이 3종, overshoot 3종, 진행률 5종을 비교했다.
위치의 완료 callback과 scale plugin 끝값을 포함한 180건에서 비트 불일치 0이다.
일반 효과 캐시 대조도 Flash를 포함해 65,536 masks/1,332 easing cases 불일치 0이었다.
이 fixture는 모든 장식의 렌더링·복귀 경계를 검증하는 전체곡 픽셀 시험을 대신하지 않는다.

## 일반 활성화 후보의 근거와 한계

기존 사용자 제공 전체곡 측정에서 이득인 Arche와 손실/무이득인 Exit·Windflower를 구분할 준비 단계 지표를 만들었다.
매 프레임 탐색하지 않는다. 전체 장식 대비 최종 소유 비율, 소유 행에 적용되는 가시 이벤트 target 수/곡 초,
마지막 타일 시각까지 128개 중점 표본의 평균 가시 장식 수와 가시 이동 장식 수를 집계한다.
가시는 visible·opacity 채널이 양수인 상태, 이동은 기하·시차 채널의 진행 중이며 start와 target이 다른 segment다.
현재 화면에 안 보인다는 이유로 개별 장식을 제거하거나 구간마다 소유권을 변경하지 않는다.

| 집계 판 | 소유/전체 | 가시 적용/초 | 평균 가시 추정 | 평균 가시 이동 추정 | 후보 판단 |
|---|---:|---:|---:|---:|---|
| cpu-profile-ar | 28,523/28,835 = 0.989180 | 250.849544 | 196.265625 | 173.265625 | 허용 |
| cpu-notify-exit-diagnose | 2,999/3,524 = 0.851022 | 31.109931 | 168.375000 | 13.015625 | 거부 |
| cpu-profile-wf | 2,813/6,290 = 0.447218 | 21.828945 | 779.421875 | 18.195313 | 거부 |

기준은 이득이 관측된 준비 표본의 하한이다: ownership≥0.989179, visible applications/s≥250.8495, mean visible moving≥173.265625를 모두 요구한다.
앞 두 수치는 출력 반올림으로 경계 판이 거부되지 않도록 관측값 아래로 절삭한 값이다. 임의의 FPS·맵 이름·시각 조건은 없다.
실제 `gpudeco admission on` 준비 시험에서 Arche owned=28,523, Exit·Windflower owned=0을 확인했다.
현재 판정은 GPU 준비 뒤 최종 소유 행이 확정된 시점에 수행한다. 거부된 판은 재생 중 소유하지 않지만 준비 비용 자체를 줄인 것은 아니다.
교차 검증된 손익 분기 모델이 아니며, 처음 보는 맵의 이득이나 최악 구간을 예측하지 못한다. 따라서 후보는 기본 꺼짐이다.
표본 수 128은 집계 해상도이며 시각 변경 상수가 아니다. 짧은 움직임이나 Flash의 가시성 변화를 놓칠 수 있다.
초기 버전은 긴 효과 종료 시각을 horizon으로 잘못 잡았다. 이전 cpu-notify-{exit,ar,dd}의 평균·적용률은 폐기했고 위 표와 기준에 사용하지 않았다.

## 변경 알림 검증

최초 Exit에서 song≈110.6초의 `Floor` MeshRenderer 서명이 변했지만 장식 후크가 알리지 않아 mismatches=1, 원본 복귀가 발생했다.
장식의 spriteRenderer/meshRenderer와 정확히 대응하는 행만 알림 계약에 넣고, 타일·선 등 나머지는 기존 8프레임 검사를 유지했다.
UpdateShader 변경도 장식 알림에 포함했다. 생략된 행은 기존 서명 oracle와 같은 dispatch에서 비교한다.

| 최종 동일 DLL 전체곡 | 알림 행/기존 검사 행 | 생략 행 비교 수 | 불일치 | 종료 verify |
|---|---:|---:|---:|---|
| Exit | 889/57 | 7,340,847 | 0 | 통과 |
| Arche | 610/0 | 9,650,926 | 0 | 통과 |
| DDONGSSADA | 9,755/5,862 | 19,079,664 | 0 | 통과 |

각 판에서 준비 전에 check를 켜고 `waitend` 뒤 state·verify를 실행했다. 최종 세 판과 비용 네 판의 PlayerAuto SHA는 모두 같다.
DDONGSSADA의 check oracle 자체는 매우 무거웠고 일부 프레임의 관리 경과 시간이 약 150ms였다. 이 oracle 판의 FPS를 제품 성능이나 정상 프레임률의 검증으로 사용하지 않았다.
oracle를 끈 on/off 비용 비교는 별도 네 프로세스에서 수행했다. 의미 검증 통과와 성능 채택은 별개이며 현재는 채택하지 않는다.

## Arche 262초 픽셀

`docs/deco-spec/results/evidence/g1/g1-ar-262-controls.steps.json`의 game/press/waitsongpos/정지/필터 on/UI 제외/마스크 없음/캡처 순서를 유지해 wt-cand를 실행했다.
현재 작업 트리 CPU 후보가 아닌 wt-cand DLL을 설치했고, DLL SHA와 원시 PNG SHA는 evidence/cpu-extreme/summary.json에 보존했다.

| 대조 | 최대 RGBA | 허용차 초과 픽셀 |
|---|---|---:|
| N1/N2, threshold 0 | 0,0,0,0 | 0 |
| A/A2, threshold 0 | 0,0,0,0 | 0 |
| C/I, threshold 3 | 4,4,4,0 | 8/3,686,400 |
| I/A, threshold 3 | 1,1,1,0 | 0 |
| C/A, threshold 3 | 4,4,4,0 | 8/3,686,400 |

최대 차이 4는 wt-cand에도 존재한다. 통합 쪽의 C/I 대조에도 차이가 있어 이번 CPU 후보에 고유한 회귀로 취급하지 않는다.
캡처 사이 Flash 화면 오브젝트 행렬·숨김 Ring 등의 상태 변화 advisory도 원시 로그에 남았다. 노이즈 및 A/A2 0은 이 정지 시점의 관측이며 모든 동적 상태가 불변이었다는 뜻은 아니다.
명세에 order/hide control은 없어 결과의 ctl_order/ctl_hide=NA를 별도 통과로 쓰지 않았다.
필터 포함·마스크 없음에서 threshold 3 초과가 있으므로 통합 렌더러의 픽셀 채택 조건은 계속 막힌다.
원시 PNG 네 장(C/I/A/A2)은 `E:\StutterFixWork\night-results\cpu-ar262-cand-artifacts`에 복사했다. noise 명령의 N1/N2는 파일로 저장되지 않아 로그 수치를 보존했다.

## 조사했으나 추가 변경하지 않은 것

- **Hold UpdateMesh 재생성:** 원본은 매 프레임 정점/삼각형 메시를 다시 만들지 않는다. 재질 값 6개와 bounds를 다시 쓴다. 함수 내부 material getter 재사용만 시험했고 이득은 미확정이다. 프레임 간 전체 생략은 head/tail/시작 위치·scale·tileSize·radius·mesh·외부 material 쓰기를 모두 추적해야 하므로 구현하지 않았다.
- **Floor LateUpdate:** 기존 FloorLateFix가 다중 행성 선이 없는 타일의 position/scale 읽기 4곳을 이미 줄인다. 원본은 opacity/glow 변화 감지와 아이콘·dummy planet·line 갱신도 하므로 전체 LateUpdate를 끄지 않았다. 별도 미측정 변경을 추가하지 않았다.
- **동일 색 쓰기:** FloorMeshRenderer.color setter는 Material.SetColor와 cachedColor를 쓴다. cachedColor만 보고 생략하면 외부 material 수정·교체를 놓칠 수 있다. 새 native GetColor 비교는 절감 대신 읽기를 추가할 수 있다. 기존 ColorFast도 native 색 쓰기는 보존한다. 안전한 소유·무효화 근거와 이득이 없어 별도 후보를 채택하지 않았다.
- **TMP 동일 문자열 재빌드:** 기존 UiProf가 Canvas/TMP를 측정하지만 이번 포함 표본만으로 동일 문자열 재빌드인지 알 수 없다. scrTextDecoration.SetText는 문자열 대입 외 SetCollider 코루틴도 시작한다. 폰트·레이아웃·재질 변화가 없는 증거 없이 텍스트/Canvas 재빌드를 생략하지 않았다. 잦은 함수 스톱워치 계측도 추가하지 않았다.
- **TightLate 남은 호출:** 기존 List는 카메라/부모 transform을 invocation/epoch 단위로 저장하고 slot 배열로 따뜻한 반복의 records 해시 조회를 피한다. Eligible은 다수의 managed field/ref 비교를 이미 사용한다. 남은 Unity 생존·renderer·parent 검사를 없애는 수명 보장은 확보하지 못해 같은 최적화를 중복하거나 검증을 삭제하지 않았다.
- **큰 native 비용:** RVA 0x1C5747B는 memcpy_repmovs다. 단일 RVA 심벌 조회용 합성 입력 1줄을 쓴 것이며 그 도구의 100% 출력은 새 프로파일 비중이 아니다. 원래 Exit 프로파일의 6.0%는 유지하되 복사 대상·스레드 대기·GPU 병목을 추정하지 않는다. 호출자/복사량 근거 없이 복사를 제거하지 않았다.
- **추가 전체곡 성능 승격:** CPU 두 후보와 notify는 이득 조건을 못 넘었고 픽셀 최대 4가 남는다. 기본값 변경, 일반 맵 이득 주장, 출시·배포·푸시는 하지 않았다. 활성화 후보의 전체곡 ABBA·미관측 맵 교차 검증·추가 픽셀 경계 검증은 안 했다.

## 빌드·증거·복원

Player/PlayerAuto/DEV 최종 빌드는 오류 0, 기존 경고는 남아 있다. DEV의 AUTOTEST 전용 참조 3곳을 해당 조건으로 제한했다.
GpuDecoPlayerCheck 455건, DrawCollapseCheck 52건 통과. Player PE 검사로 IdleFloorUpdate/HoldMaterialReads가 일반 Player에 없음을 확인했다.
shader/native copy/내장 drawcollapse5 번들은 변경하지 않았다. 번들 SHA256은 `30E0945F4064D8CB00ACBDA19D6C3D840B05A7D0EA7F8ED419258C0DEFE74D07`다.

보존된 integrity 기록 31개 모두 설치 DLL이 restore/StutterFix-rc-player.dll과 같고 gpuprobe.dll이 없으며 GpuDeco=0이다.
초기 미지원 validate 명령, game 화면의 잘못된 seek 등 실행 명세 실패는 성능 판에서 제외했다. 해당 오류를 성공으로 재분류하지 않았다.
집계 수 31은 현재 보존된 integrity 파일 수이며, 초기 실패·중단을 포함한 전체 실행 시도 수가 아니다.

- [기계 판독 요약·DLL/PNG SHA](evidence/cpu-extreme/summary.json)
- [최종 Exit 알림 대조](evidence/cpu-extreme/cpu-notify-fixed-exit.txt), [Arche](evidence/cpu-extreme/cpu-notify-fixed-ar.txt), [DDONGSSADA](evidence/cpu-extreme/cpu-notify-fixed-dd.txt)
- [wt-cand 픽셀 원시 수치 발췌](evidence/cpu-extreme/cpu-ar262-cand.txt)
- [오프라인 Player/정책 검사](evidence/cpu-extreme/cpu-player-check.txt), [통합 정책 검사](evidence/cpu-extreme/cpu-collapse-check.txt), [심벌 조회](evidence/cpu-extreme/cpu-native-symbol.txt)
- [직렬 실행기](../../../tools/optimization/cpu_extreme_runs.py), [오프라인 집계기](../../../tools/optimization/collect_cpu_extreme.py), [판별 명세](cpu-steps)

최종 동일 DLL 시험의 PlayerAuto SHA256: `495742335b8f57930933684ea5120d8d34693c466b4d77580104cbe4eb36da16`.
일반 Player SHA와 측정별 DLL SHA는 summary.json을 따른다. 원시 전체 로그와 각 판 integrity JSON은 `E:\StutterFixWork\night-results`에 남겼다.
