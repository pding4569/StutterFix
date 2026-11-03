1. 앞선 simplify 연구 위에서 관리 코드 최적화 7항목을 개별 로컬 커밋으로 완료했고, 전처리 경계를 추가 보정했다.
2. 제품 프레임 계측·성공 진단 문자열·중복 카메라 읽기·중복 유니폼 설정을 줄였다. 안전 검사와 실패 시 복귀는 유지했다.
3. source는 변경 범위만 업로드하고 무변경 프레임은 생략한다. 채널은 기존 dirty 묶음을 유지하면서 양 끝을 줄인다.
4. Player/PlayerAuto 각 1회 빌드 오류 0, 기존 GpuDecoPlayerCheck 1회·438검사 오류 0. 게임 FPS·픽셀·절감 ms는 미측정이다.
5. 게임 실행·user_playing.flag 삭제·run_locked.py 호출·Mods 변경·푸시는 하지 않았다. 기존 G1 픽셀 불합격과 NotifyBarrierWatch=false는 유지된다.

2026-11-03. 요청: `E:\StutterFixWork\codex-extreme-prompt.md`.
작업 트리 `E:\StutterFixWork\wt-partial`, 브랜치 `claude/gpudeco-cheap`.
시작 커밋 `9bef849`; 최종 소스/빌드 기준 `0a6067e`.
앞선 [simplify-research.md](simplify-research.md)의 검증 계약·알림 연구를 이어받았다.

## 기준 비용

지정 로그는 저장소 내부가 아닌 `E:\StutterFixWork\night-results\g1ab-sh-1-partial.log`에 있다.
`[gpudeco] cost` 일치 **전체 1줄**, 1680행을 읽었고
[source-check.json](evidence/extreme/source-check.json)에 원문 전체를 보존했다.
89157프레임의 과거 Exit 결과이며 이번 후보의 측정값이 아니다.

| 지표 | 과거 평균 ms |
|---|---:|
| total / whole_frame | 0.086435 / 0.093487 |
| validate / cam / check / clock | 0.057507 / 0.000738 / 0.006495 / 0.000141 |
| progress / uniform / dispatch / misc | 0.008827 / 0.062700 / 0.014010 / 0.000898 |
| dd / exec / proxy / after | 0.013617 / 0.006304 / 0.000234 / 0.000105 |
| fb_eval / fb_copy / fb_submit / fb_exec | 0.001633 / 0.004137 / 0.001690 / 0.002222 |

중첩 구간이므로 표의 시간을 합산하지 않는다. max 8.377100ms,
span_tog 0.02/frame, alpha_tog 0.10/frame도 원문에 포함된다.
0.057507ms 전체나 0.006495ms 전체를 없앴다는 뜻이 아니다.

## 변경 표

시간 절감은 모두 **예상·미측정**이다. 아래 호출/바이트 수는 코드 경로의 작업량 계산이며
실제 ms, GPU 시간, FPS 향상으로 환산하지 않는다.

| 내용 | 커밋 | 코드 위치 | 항목 | 예상 절감 | 위험 | 필요한 게임 검증 |
|---|---|---|---|---|---|---|
| 프레임 타임스탬프·cost 누적·span/alpha/skip/제출 진단 증가를 AUTOTEST로 제한 | `f6b21f4` + 경계 보정 `0a6067e` | `GpuDecoExport.cs:1316` DispatchGpuLiveSlice, `:1443` GpuLiveFrame; `GpuDecoDrawCollapseRenderer.cs:1261,1409`; `GpuDecoCopy.cs:118` | 1 계측 | 정상 Player 해당 메서드의 GetTimestamp 0회. 진단 증가 제거. ms 미측정 | 낮음. 실제 복사 오류 managedFaults/ReadTotals는 보존. DEV 명령용 선언은 남고 프레임 계측은 AUTOTEST 전용 | PlayerAuto cost 구간 경계·기존 오류 복귀, 일반 Player 정상 재생 |
| 성공한 검사에서 문자열/숫자 포맷을 만들지 않음 | `3bbf589` | `GpuDecoDrawCollapseRenderer.cs:1261` ValidateDrawCollapse, `:1373` CheckDrawCollapseKey | 3 할당·6 검사 | 방문 owned 행마다 성공 label 문자열 제거, 성공 camera dot 포맷 및 full 순서 진단 문자열 제거 예상 | 낮음. bool 조건·오류 메시지·순서 콜백·예외는 동일. 클로저 추가 없음 | 키/카메라/대표 소실을 주입해 기존 메시지와 복귀 확인 |
| 같은 dispatch에서 카메라 값 재사용, 소유 검사 내부 sprite/texture/shader 지역 저장 | `492bd3c` | `GpuDecoExport.cs:647,1316,1443`; `GpuDecoDrawCollapseRenderer.cs:1409`; `GpuDecoPlayer.cs:747` | 2 네이티브 호출 | 공통+minimal 경로 position 읽기 4→1, eulerAngles 2→1, orthoSize 3→1 예상. frameCount 중복 읽기도 축소. ms 미측정 | 낮음. 프레임 간 캐시 없음. 검사에서 Unity 객체 생존 비교는 유지 | 이동/회전/줌/해상도·aspect 변경과 이미지 Missing/복귀 |
| source 업로드를 writer dirty 범위로 제한하고 무변경 프레임 생략 | `99fd52c` | `GpuDecoExport.cs:461,519,623,689,1355`; `GpuDecoGpu.cs:231` | 4 업로드·8 자료 구조 | 같은 slice에서 변경 없으면 SetData 1→0, 변경 시 [min,max]만. high-water 전체보다 작을 수 있음. 쓰기마다 min/max 비용 추가, ms 미측정 | 중간. Allocate/Advance/Complete/Reset 쓰기를 모두 추적. slice 전환·첫 활성은 전체 사용 prefix 재업로드, 업로드 성공 뒤 dirty 소거 | pause/resume, source 완료·덮어쓰기·재사용·pool 경계, 다시하기/seek, 다중 slice, legacy bundle |
| collapse 자체가 설정하는 공통 유니폼 중복 제출 생략 | `c80d903` | `GpuDecoExport.cs:1380`; `GpuDecoDrawCollapseRenderer.cs:1498`; `GpuDecoGpu.cs:241,316` | 5 디스패치 | 독립 fallback 없는 self-contained collapse에서 SetInt 3+SetVector 1+SetFloat 2, 총 6호출 제거 예상. fallback 있는 프레임은 평가 직전에 동일 6호출 복원하여 순감소 0 | 낮음~중간. 공유 shader의 프레임 간 값은 캐시하지 않음. 직접/기록/minimal 및 fallback별 설정 경로를 분리 | minimal/direct/recorded/span 전환, texture overflow 있음/없음, 알파 조기 종료와 복사 fallback |
| dirty block의 연속 묶음은 유지하면서 양 끝 채널 범위 축소 | `209bacf` | `GpuDecoExport.cs:115,141,148,168`; `GpuDecoPackets.cs:328` | 4 업로드·8 자료 구조 | 고립된 1채널이면 live payload 4096→16B 예상, 한 장식 16채널이면 4096→256B 가능. 기존 묶음당 SetData 횟수는 증가시키지 않음. min/max 배열 2개와 쓰기 비용 추가 | 중간. native packet은 개별 채널 범위를 모르므로 해당 dirty block 전체로 확장. 알파 pending·anchor 갱신·완료 업로드 유지 | block 255/256 경계, 인접/분리 block, 마지막 부분 block, native packet+관리 writer 혼합, alpha 시작/완료 |
| 이벤트당 중복 준비 호출 및 불필요한 현재 채널 평가 제거 | `ac6b921` | `GpuDecoExport.cs:332,695,720` | 7 시작·완료·8 자료 구조 | GPU 대상당 두 번째 PrepareGpuEffect 검사 1회 제거. 상대 위치 X/Y 외 EvalMasterSlotCurrent 제거 예상. ms 미측정 | 낮음. 단일 호출자가 undo 캡처 전에 preflight/준비를 완료. endpoint·kill 순서·Rec 순서·완료 source 처리는 동일 | 절대/상대 이동, scale/parallax endpoint, 중복/덮어쓰기 이벤트, fault 주입 후 CPU 복귀 |

`0a6067e`는 최종 소스 검토에서 이미지 처리/진단 #if 경계를 명시적으로 복구하고
원래 UTF-8 형식을 유지한 보정이다. 전처리 중첩을 검사한 다음 최종 빌드를 수행했다.

## 경로별 검토와 건드리지 않은 곳

- `GpuLiveFrame → CheckPlayerFrame`: 4프레임 주기, 최대 4개 순환 소유 검사, native copy error 검사를 유지했다.
  SpriteRenderer 소실·mask/SortingGroup·외부 enabled/active/forceRenderingOff·재질/텍스처 변경은
  알림만으로 완전히 관측되지 않는다. 생존 검사를 ReferenceEquals로 대체하지 않았다.
- `AdvanceClock`: 기존 SongClock, once-per-frame, pause/continuity/pitch 의미를 유지했다.
  여러 Update/LateUpdate 호출자가 있어 전역 프레임 캐시 계약을 새로 만들지 않았다.
- `DispatchGpuLiveSlice`: 선택/전환·이미지 flush·복구 fault·독립 평가·copy/native/command fallback을 유지했다.
  shared ComputeShader에는 다른 slice, native copy, command buffer가 값을 쓸 수 있어
  같은 값이라고 프레임을 넘어 SetInt/SetVector/SetBuffer를 생략하는 캐시는 넣지 않았다.
- `AdvanceGpuLiveSources/CompleteGpuLiveSource`: source 하나당 double easing 1회, StartFrame 장벽,
  swap-remove active 목록, static endpoint·anchor·CPU 복귀 metadata와 채널 완료 업로드를 유지했다.
  완료 후 source 슬롯은 재사용된다. sourceIndex를 그대로 두고 완료 채널 업로드를 생략하면
  나중 이벤트의 시계가 이전 장식을 다시 움직일 수 있어 해당 최적화는 적용하지 않았다.
- `FlushDirty/UploadSourceE`: 영구 배열/List를 사용한다. List<int>/배열 foreach는 해당 코드에서
  인터페이스 열거로 바꾸지 않았다. dirty block을 채널마다 잘게 쪼개면 SetData 호출이 늘어나므로
  기존 연속 block 병합을 유지하고 양 끝만 줄였다. sparse source 사이의 빈 공간은 보수적으로 포함한다.
- `CheckDrawCollapseBeforeDispatch/ValidateDrawCollapse`: 8프레임 검증, 카메라 축/정렬 모드,
  auxiliary·owned·대표 renderer 생존 및 serious failure 예외를 유지했다.
  앞선 NotifyBarrierWatch는 기본 false 그대로다. 외부 직접 쓰기를 모두 알림으로 바꿀 수 있다는
  근거가 없어 주기를 늘리거나 안전 검사를 제거하지 않았다. 기존 DrawCollapseGuard의
  할당/상태 관리는 유지했다. 성공 문자열 제거는 검사를 줄이는 변경이 아니다.
- `DynamicOrderTick/ExcludedVisibilityTick`: 튜플은 값 형식의 typed Equals다. 박싱을 확인하지 않고
  단순 비교로 바꾸지 않았다. Dictionary/HashSet은 장벽 bucket membership과 변경 목록을 보존한다.
  임의 order/material/활성 변경과 알파 연속 변화를 처리하므로 통째 순회를 없애지 않았다.
- `DispatchDrawCollapse`: texture fallback이 없거나 copy mask가 0이면 두 번째 Evaluate를 건너뛰는
  조건은 이미 존재했다. 이를 새 개선으로 세지 않는다. mask는 chunk 단위이며 texture overflow와
  동적 제외/복귀가 결합되어 있다. fallback 항목만 evaluate하려면 별도 index/커널/실제 bundle 및
  픽셀 검증이 필요하므로 이번 관리 코드 수정에서 GPU 알고리즘을 바꾸지 않았다.
- `ApplySpanCulling`: dirty span만 보고 상태가 다를 때 enabled를 쓰는 기존 구조를 유지했다.
  외부 writer와 renderer 파괴가 가능하므로 프레임 간 enabled/Unity-null 캐시를 추가하지 않았다.
- `FastMove → GpuLiveSkip → ApplyGpuLiveEffectToTarget`: 준비된 src/대상 조회, GPU 소유 dictionary,
  recovery undo, G1 visible/image, 위치 상대 기준과 Rec 생성 순서를 유지했다.
  MasterSlots.Remove는 List 검색/이동을 포함하지만 swap-remove나 intrusive 목록으로 바꾸면
  완료·pendingAlpha·packet rollback의 순서/위치 계약까지 바뀐다. 게임 시험 없이 구조를 교체하지 않았다.
- 준비 단계의 Stopwatch/5분 budget·복귀 로그 시간은 매 프레임 cost 계측과 다르므로 남겼다.
  오류 판단용 managedFaults/native totals는 진단 전용 카운터로 취급해 제거하지 않았다.
- 새 위험 연구 플래그나 셰이더/네이티브 코드 변경은 없다. 이번 변경은 동일 입력·동일 값 쓰기를
  유지하는 구조 축소다. 런타임 안전성/픽셀 합격은 오프라인 검사만으로 확정하지 않는다.

## 로컬 검증

최종 소스 이후 각 빌드를 **1회씩** 실행했다. 실패 재빌드나 반복 빌드는 없었다.
`-c Release -p:Edition=Player -m:1 -p:BuildInParallel=false --nologo`,
PlayerAuto는 추가로 `-p:AutoTestBuild=1`.

| 검사 | 횟수 | 결과 |
|---|---:|---|
| 변경 소스 검토 및 top-level C# #if 중첩 | 가벼운 소스 검사 | 통과 |
| Player/PlayerAuto/DEV 5메서드 계측 전처리 경계 | 15조합 | 통과. 이미지 flush 이후 제품 본체도 남는지 확인 |
| Player Release | 1 | 오류 0, 경고 48 |
| PlayerAuto Release | 1 | 오류 0, 경고 47 |
| 기존 GpuDecoPlayerCheck | 1 | 438검사, 오류 0 |
| git diff --check | 최종 확인 | 통과 |
| 게임·실제 pixel/성능·GPU/native 실행 | 0 | 미실행·미측정 |

[Player 빌드](evidence/extreme/build-player.log),
[PlayerAuto 빌드](evidence/extreme/build-playerauto.log),
[기존 checker 결과](evidence/extreme/player-check.log),
[소스 경계/과거 cost 원문](evidence/extreme/source-check.json),
[아티팩트 메타데이터](evidence/extreme/build-artifacts.json).
checker는 정책·PE metadata·내장 bundle 검사이며 게임 assembly를 로드하거나 재생하지 않는다.
경고는 해결 완료로 보고하지 않는다. 기존 참조 충돌/사용하지 않는 코드 경고 등이 로그에 남아 있다.

| DLL | SHA-256 |
|---|---|
| `bin/Player/StutterFix.dll` | `3ecae9fb0a13f0bb44ee2a559218e9f336d20e9889661d8b335e5edcba2424bf` |
| `bin/PlayerAuto/StutterFix.dll` | `13c76b1f08f0b848dd06f566e441a30a297db1347d741e7dfa0b7ab94cf8a9c0` |

내장 bundle은 두 DLL 모두 90828B,
SHA-256 `30e0945f4064d8cb00acbda19d6c3d840b05a7d0ea7f8ed419258c0defe74d07`로 동일하다.
기존 G1 최대 채널 차이 4/허용 3 초과 기록을 이번 작업의 합격으로 바꾸지 않았다.

## 사용자 복귀 뒤 측정 순서

1. 사용자가 게임을 마치고 실행/설치를 허가한 후 별도 시험 세션을 연다. 이번 작업에서는
   `user_playing.flag`·`run_locked.py`·Mods·게임 실행을 다루지 않았고 자동 예약도 하지 않았다.
2. `9bef849`와 이번 소스의 PlayerAuto를 같은 map/settings/native/bundle 조건으로 비교한다.
   위 변경별 게임 검증을 먼저 한다. source 완료 직후 재사용, pause/resume, block 경계,
   다중 slice, packet/legacy 연구 경로, 재시작·seek·실패 복귀를 포함한다.
3. CPU 원본 독립 실행·기존 GPU·이번 GPU·복원·noise의 화면을 같은 시점에 비교한다.
   이동/회전/줌, alpha 0 경계, Missing image/복귀, texture overflow와 동적 정렬 제외를 포함하고
   Arche 262초의 기존 G1 불합격을 먼저 해결한다. checker 통과를 pixel 합격으로 쓰지 않는다.
4. 정확성 통과 후 Exit·DDONGSSADA의 동일한 무거운 창을 프로세스별 off/on/on/off로 측정한다.
   AUTOTEST에서 cost/reset 및 total/whole/validate/check/progress/dispatch/fb 계측, 실제 FPS,
   frame-time 분포·긴 프레임·복귀 여부를 함께 남긴다. 비교 양쪽에 같은 계측을 쓴다.
5. 제품 계측 제거 효과는 AUTOTEST cost만으로 직접 측정할 수 없다. 일반 Player끼리 외부
   frame-time 측정으로 따로 확인한다. 미세 변경은 항목별 부모/해당 커밋을 대조하고
   SetData 호출 수·전송 바이트·GC 할당을 별도 한정 창에서 확인한다.
6. 변화가 noise 이내면 미확인으로 보고한다. 전체곡·다른 맵·사용자 조작·복귀까지 통과한 뒤
   채택을 판단한다. NotifyBarrierWatch의 완화된 감사 주기는 이 변경과 분리해 기본 꺼짐을 유지한다.
