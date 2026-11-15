# M4h 구현 및 오프라인 검증 결과

2026-11-15. 작업 폴더 `E:\StutterFixWork\wt-m4`, 브랜치 `claude/drfull`, 기준 HEAD `ff682316dd91f7994cc7df7e01ce5f5844a0f2b2`.

기존 미커밋 변경을 `git diff`로 검토하고 GPU 구간/메모리 계측, explicit 제출 제한, 메모리 보유량 절감, Exit 비용 경로 개선, 반올림 대조 시험과 M4h 계획을 보강했다. **실게임 DD의 9배 GPU 원인 확정, Exit 전체 추가 비용 <0.95ms, 실제 Exit ±1~2의 장식별 귀속은 미측정이다.** 게임 실행 금지 조건 안에서 얻은 코드·WARP 증거와 실게임 목표를 구분한다.

커밋, 게임 실행·조작, 설치 DLL 교체·복원은 하지 않았다. 기존 명세 추가 문구, `13-hello-color-notes.md`, 기존 산출물은 보존했다. 아래 빌드/fixture 수치는 `out/direct-renderer/m4h/check-final.log`와 `m4h-audit.json`의 최종 통과 결과를 기준으로 한다.

## DD: 카메라 GPU와 메모리를 같은 표본으로 관측

기존 실게임 수치는 명세에서 제공된 자료다. 이번 작업에서 다시 측정한 수치가 아니다. DD B1의 카메라 31,469µs는 A1 3,567µs의 약 8.82배이고, 같은 빌드 B2는 4,324µs였다. native 이벤트 평균도 B1 1,087µs, B2 166µs로 달랐다. 동일 빌드의 간헐적 변화는 확인되지만, 이 집계만으로 VRAM 페이지 이동이나 특정 explicit renderer를 원인으로 확정할 수 없다.

`DirectFull.Diagnostics.cs`, `drfull-timeline.h`에 다음 계측을 완성했다.

| state | 의미 |
|---|---|
| `gpu_camera_us`, `gpu_camera_max_us` | 같은 완료 표본들의 카메라 구간 평균/최대 |
| `gpu_native_frame_us`, `gpu_explicit_us`, `gpu_composite_us`, `gpu_other_us` | 카메라 구간을 네 범주로 분할; 합계가 카메라 시간과 일치 |
| `gpu_opaque_us`, `gpu_opaque_alpha_gap_us`, `gpu_alpha_us`, `gpu_post_alpha_us`, `gpu_effects_us`, `gpu_tail_us` | 같은 표본을 여섯 렌더 단계로 분할; 별도로 합계 검사 |
| `gpu_auxiliary_us`, `gpu_auxiliary_samples` | 보조 카메라 구간; 메인 카메라 시간에서 빼지 않음 |
| `gpu_camera_last_us`, `gpu_last_submit_*`, `gpu_last_sample_serial` | 마지막 완료 GPU 표본과 그 표본 제출 시점의 DXGI usage/budget/non-local usage |
| `gpu_max_submit_*`, `gpu_max_sample_serial` | 최대 카메라 GPU 표본과 짝지은 제출 시점 메모리 |
| `gpu_timeline_pending/disjoint/invalid/overflow/skipped` | 미완료/무효/용량 초과/표본 생략을 구분 |

범위는 **BeforeForwardOpaque부터 AfterEverything까지**다. 그보다 이른 clear/depth prepass 등은 포함하지 않는다. 64회마다 표본을 수집하며, `GetData(DONOTFLUSH)`로 완료 결과만 읽는다. 쿼리 대기·spin·강제 Flush를 넣지 않았다. 범주 합과 단계 합은 각각 카메라 합계와 일치하지만 서로 겹치는 분류이므로 둘을 합산하면 안 된다. GPU timestamp 경과 시간은 GPU busy나 실제 page fault 측정과도 다르다. 기존 `gpu_event_us`는 **native segment 단위 평균**이며 카메라 전체 평균과 직접 빼서 원인을 계산하지 않는다.

WARP에서 메인 3표본·보조 3표본, 네 범주·여섯 단계의 합계 일치, overflow 표본 거부를 검사했다. DXGI 관측을 timestamp slot의 serial과 연결하는 검사도 추가했다. 제출 시점 usage와 나중에 완료되는 GPU 시간이 짝지어질 뿐, GPU 실행 순간의 residency를 직접 읽는 것은 아니다.

## GPU 자원 보유량과 절감

`drfull-memory.h`는 자원 포인터를 중복 제거하여 GPU buffer/texture의 논리 바이트를 집계한다. geometry, pose, draw, index, 사전 변환, 상수, 색, Grab, Canvas 색을 분리하고, managed가 보유한 공유 깊이 바이트를 더한 값이 `gpu_path_bytes`다. 외부 Unity 원본 이미지·메시, 메인 카메라 RT, CPU packet ring, 드라이버 rename/alignment/임시 자원과 쿼리 객체는 이 소유 바이트 합계에 포함하지 않는다. **DXGI는 이 프로세스의 local/non-local 사용량과 예산을 따로 관측한다. 장치 전체 VRAM 총사용량과 같지 않다.**

`vram_before_usage/budget`은 native 준비 시작 시점, `vram_prepared_usage/budget`은 첫 자원 집계 시점이다. 후자는 모든 GPU 지연 할당이 끝났다는 뜻이 아니다. 곡 중 주기적 관측과 `state` 조회에 현재/peak usage, non-local usage/budget, over-budget 표본 수·최대 초과 바이트·최소 여유 바이트를 남긴다. 논리 peak도 관측한 시점들의 peak다. 아주 짧은 압박은 표본 사이에 지나갈 수 있다. 미지원/실패는 `vram_supported`, `vram_failures`로 드러낸다. WARP의 지원 여부를 하드웨어 VRAM 측정 성공으로 해석하지 않는다.

적용한 절감은 다음과 같다.

1. **Canvas 깊이 버퍼 공유.** 기존에는 각 run의 black/white RGBAFloat RT마다 깊이 버퍼가 있었다. 색은 그대로 유지하고 모든 순차 보조 렌더가 깊이 버퍼 하나를 공유한다. 실제 깊이 포맷의 block size로 바이트를 센다. D24S8, 3440×1440이면 기존 깊이 비용은 run당 39,628,800B, 새 비용은 모든 run 합계 19,814,400B다. R개 run의 절감은 `(2R−1)×19,814,400B`다. 예를 들어 R=2라면 59,443,200B 절감이다. 색은 여전히 run당 158,515,200B이므로 큰 항목이다. 실제 DD의 run 수는 아직 읽지 않았다.
2. 사용하지 않는 Canvas run의 RT와 native composite SRV 참조를 회수한다. native 참조 해제는 교체 packet이 render thread에서 소비된 뒤 수행한다. composite 슬롯을 다시 쓸 수 있게 했다.
3. GPU stream 용량을 두 배 예약에서 **12.5% 여유 + 64원소 정렬**로 바꿨다. GPU 경로에서 쓰지 않는 CPU fallback VB는 만들지 않고 실제 사용량만큼 지연 할당한다.
4. unnamed Grab은 모든 draw가 표면 하나를 재사용한다. named Grab은 이름별 snapshot을 유지한다. 256프레임 이상 사용하지 않은 표면은 주기 점검 때 해제하고 다음 실제 사용에서 재생성한다. 분리된 재질 상수의 pool 단독 참조도 정리한다. 원본 named snapshot 수명을 임의로 합치지 않는다.
5. 자원 변화가 없으면 주기 메모리 계측에서 전체 소유 자원 재집계를 생략한다. 상시 resource 정렬로 새로운 render-thread 스파이크를 만들지 않도록 했다.

7,000소스 fixture의 실제 논리 소유 합계는 **7,667,240B**였다. pose 1,269,760B, draw 507,904B, index 189,184B, 사전 변환 2,084,112B를 확인했다. 이는 해당 작은 합성 geometry의 결과이며 DD의 메모리 절감량이나 FPS 수치가 아니다.

Grab fixture는 unnamed/named 각각 최초 98,304B → 화면 밖·idle 회수 후 0B → 재사용 후 98,304B를 확인했다. 화면 밖 추가 복사 0, 재생성 후 픽셀 동일이다. 보이는 unnamed Grab은 Unity 의미대로 매번 복사한다. **임의 셰이더의 projective/distortion UV가 geometry 바깥을 읽을 수 있어 Grab을 검증 없이 사각형 crop하지 않았다.** 이 구현은 복사 크기를 줄였다는 주장이 아니라 보유량·수명·불필요한 복사를 줄인 변경이다.

공유 깊이 fixture는 전용 깊이와 공유 깊이의 출력이 같은 6조건을 통과했다. clear를 일부러 생략하면 이전 depth/stencil로 출력이 달라지는 음성 대조도 확인했다. 실제 Unity `Camera.Render/SetTargetBuffers` 실행은 금지 조건 때문에 시험하지 않았다. 보조 카메라의 SolidColor clear와 대상 복원은 4맵 pixel gate에서 확인해야 한다.

## Explicit 제출과 Exit CPU/GPU 비용 경로

기존 `DrawRenderer(..., -1)` 대신 패스의 LightMode와 활성 여부를 검사한다. 검증된 unlit forward 경로는 empty/Always/ForwardBase만 제출하고 ShadowCaster, Meta, MotionVectors, DepthOnly/DepthNormals, Deferred, Prepass는 제외한다. ForwardAdd 등 검증되지 않은 mode와 LODGroup fallback은 원본 카메라가 필요하다고 거부한다. 재질을 인스턴스화하지 않는다.

explicit source는 Unity renderer bounds, 카메라 frustum, layer distance를 사용해 보수적으로 검사한다. 비정상 bounds는 그리기를 유지한다. 가시성이나 패스 구성이 바뀌면 명령을 다시 기록한다. 인접 explicit 묶음은 타임스탬프 한 쌍을 공유하고 빈 native 경계는 segment 0의 packet 소비가 필요한 경우를 제외하고 생략한다. 실제 Unity batching/occlusion/LOD와 완전히 동등하다는 실게임 증거는 없다.

진단 명령은 renderer가 off일 때만 바꿀 수 있다.

```text
drfull skip explicit     # explicit 그리기만 생략; 픽셀/정상 ABBA 판정에서 제외
drfull explicit on       # explicit 복구
drfull explicit all      # 기존 -1 패스 제출 비교; 가시성 조건은 유지
drfull explicit forward  # 정상 후보 패스 정책
drfull precision local   # 원본 로컬 정점 경로; 사전 변환/배치 분리 대조
drfull precision batch   # 정상 후보 복구
```

Exit 비용에 대해서는 다음 경로를 줄였다.

- 동일 frame의 같은 상수 storage는 patch/memcmp/upload를 한 번만 평가하고 segment 사이에서도 재사용한다. 같은 stage/slot의 CB bind도 재사용한다. 개별 draw와 배치의 상수 storage는 분리한다.
- 개별 native draw도 pose 버전별 world bounds를 재사용한다. draw마다 VP×World와 8개 corner를 다시 계산하던 경로를 보수적 frustum 검사로 바꿨다. 화면 밖 Grab은 snapshot 전에 제외한다.
- pose 행렬·center·정렬 축이 모두 같으면 거리 키 계산을 건너뛴다. 기존의 실제 상대 순서 비교도 유지한다. 거리 통보 수와 실제 replan 수를 동일하게 취급하지 않는다.
- 기존 `key-distance/key-bucket`, `avoided_order_replans`, `max_collect/pose/sort/canvas/packet`, `max_gc_collections`, `commit_peak_*`와 새 상수/가시성 카운터를 함께 남긴다.

7,000소스 안정/전체 이동/131소스 희소 이동/전체 반전 fixture의 C++ heap 추가 할당 0과 기존 전송량 검사를 유지한다. 두 native segment에 같은 binding을 사용하는 대조에서는 상수 평가 1,320회·재사용 768회를 확인했다. 실제 Exit의 replan 518건, main 최대 8.373ms가 어느 소스에서 발생했는지, main 0.994ms·render 2.068ms·GPU 증가 0.86ms가 얼마로 줄었는지는 이번 작업에서 측정하지 않았다. CPU/GPU 구간은 겹칠 수 있으므로 이 값을 단순 합산하지도 않는다. **전체 <0.95ms 예산 달성을 선언하지 않는다.**

## 반올림: 증명한 원인과 남은 귀속

새 WARP 대조는 Canvas 합성에 쓰이는 source-over/UI alpha-squared 계열의 반투명 4레이어를 사용한다. **VS, PS, 정점, draw 순서, blend 상태를 동일하게 두고 중간 색 타깃 포맷만 UNORM8과 FLOAT32로 바꾼다.** FLOAT32 경로는 마지막에 texel `Load`로 한 번만 UNORM8로 옮긴다. 이 복사에는 UV 보간·색 보정이 없다. 초기 배경도 1/255 단위로 맞췄다.

| 조건 | 중간 UNORM8 반복 vs FLOAT32 후 1회 저장: 다른 픽셀 | 최대 채널 차이 |
|---|---:|---:|
| source alpha, 배경 0/1/2 | 3,072 / 3,072 / 3,072 | 1 / 1 / 1 |
| premultiplied alpha, 배경 0/1/2 | 1,584 / 1,704 / 2,268 | 1 / 1 / 1 |

합계 14,772픽셀이다. 같은 셰이더·같은 순서에서도 **중간 색 타깃의 포맷과 단계별 blend/저장 양자화**만으로 ±1이 생기는 원인 하나를 증명했다. 이 대조에서 정점 변환, 보간, draw 순서 변경은 원인이 아니다.

별도로 black/white affine 합성과 float 직접 렌더의 최대 float 차이는 `1.19209e-7`이다. 이를 UNORM8로 옮기면 경계에서 추가 1 차이가 날 수 있으며, `rounding.tsv`에 이 잔차도 따로 기록한다. 기존 24조건 합성 회귀는 최대 2를 유지했다. 중간 UNORM8 양자화와 float affine 산술 잔차를 하나의 원인으로 뭉뚱그리지 않는다. 색에 ±1 보정을 넣거나 RGBAFloat를 half로 낮추지 않았다.

**이것은 합성 fixture의 원인 증명이다. 실제 Exit 11.3%의 어느 셰이더·어느 장식인지 증명한 자료는 아니다.** 실제 원본 UI DXBC/Canvas 장식과 일시정지 캡처를 동시에 재생한 것이 아니므로 해당 귀속은 남아 있다. `m4h-sh-precision-local-pixel`을 정상 pixel 계획과 비교하면 사전 변환이 원인인지 먼저 분리할 수 있다. 같은 잔차가 남으면 Canvas/셰이더별 격리 캡처가 필요하다. 기존 M4g의 원본 DXBC 배치 56조건·가는 정점 1,344변환의 정확 일치는 유지했다.

## 검증과 계획

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/direct-renderer/check-m4h.ps1
```

이 명령은 로컬 빌드, WARP, 정적/관리 fixture, 계획 생성만 수행한다. 게임이나 설치 경로에 쓰는 동작은 없다.

| 검사 | 최종 결과 |
|---|---|
| native / DEV / Player / PlayerAuto | 4종 오류 0; 기존 managed 경고는 남음 |
| native export | name/ordinal/hint 74/74 보존 |
| ABI/리소스 | 기존 Frame 408B, Pose 128B 및 opcode 보존; 새 timeline 288B, memory 208B; DXBC/metadata roundtrip 통과 |
| 패키지 | managed 3종에 현재 native 포함; Player 진단 리소스 제외 유지 |
| 원본 DXBC 배치 / 가는 정점 | 56조건 / 1,344변환 정확 일치 |
| Grab | unnamed 8 + named 8 픽셀 차이 0; 프레임/segment/name 수명·회수·복원 통과 |
| 상수 / GPU timeline / 메모리 | frame 내 재사용, 구간 합계·serial 연결, 무효 표본 거부, 논리 바이트 합계·중복 제거 통과 |
| 합성 / stencil / 공유 깊이 | 24 / 9 / 6조건 통과 |
| 반올림 | 포맷만 바꾼 6조건, 14,772픽셀·최대 1 재현 |
| explicit 패스 정책 | 허용 3, 제외 8, 미검증 거부 3 통과 |
| 수명/준비/복원 | 기존 25 lifecycle, 10,000 masks, 안정 검사 100,000회 및 준비 pump 통과 |
| 순서 / SPSC / FrameCompare | 기존 7,000회 oracle, packet 128, 19+15+15+17 회귀 통과 |

정상 계획은 `out/direct-renderer/m4h/plans/`의 **24개**다. `m4h-{hl50,wf,dd,sh}-{plan,pixel,A1,B1,B2,A2}`, 시작 위치는 각각 50/258/50/88초, RT 3440×1440, 측정 12초, 순서 A1/B1/B2/A2다. 정상 계획마다 explicit/precision 진단 상태를 초기화한다. B와 pixel만 warm하며 준비는 pause 안에서 끝낸다. pixel의 off/off 잡음 0, A/B의 safe=0 및 기존 오류·복원 gate를 유지한다.

진단 계획은 **6개**를 추가했다.

- `m4h-dd-skip-explicit-B1/B2`: explicit 비용 분리, 정상 pixel/ABBA 판정에서 제외.
- `m4h-dd-explicit-all-B1/B2`: forward 정책과 기존 -1 패스 비교, 정상 ABBA에서 제외.
- `m4h-{dd,sh}-precision-local-pixel`: 로컬 정점 경로와 정상 후보 픽셀 비교.

모두 생성·감사만 했고 실행하지 않았다. 모든 판에서 JALib 비활성 및 같은 렌더/품질/asset 조건이 전제다. 생성기가 JALib 설정을 변경하지는 않는다. 진단 종료 시 explicit/precision 상태를 복구한다. 기존 pixel 허용 기준은 changed <0.1%, max ≤3이며, 이것을 실제 화면 정확 일치로 표현하지 않는다.

산출물과 근거는 `check-final.log`, `m4h-audit.json`, `final-hashes.json`, `validation/`, `composite/rounding.tsv`, `grab.log`, `named-grab.log`, `plans/manifest.json`에 있다.

기존 validation의 `installed-before.json`과 현재 설치 native hash는 다르다. 기존 snapshot은 이어받은 파일이라 이 차이가 이번 turn 중 발생했다고 단정하지 않는다. 덮어쓰거나 복원하지 않았다. 별도로 최종 검증 시작 시점의 `installed-resume-before.json`과 끝의 `installed-resume-after.json`은 **2/2 동일**하다. 따라서 기존 snapshot부터 2/2 보존했다고 보고하지 않으며, 이번 검증 구간의 보존만 확인한다.

## 남은 실게임 판정

이 결과는 구현과 오프라인 검증 완료이며, M4h 실게임 성능/원인 조사 전체의 달성 보고는 아니다.

1. DD 정상 B1/B2와 skip-explicit을 반복해 빠른 판과 느린 판을 모두 확보한다. 같은 GPU 표본의 usage/budget/non-local, 범주 시간, Canvas run/보유 바이트를 대조한다. usage 초과는 압박의 증거이며 page migration 확정에는 VidMm/ETW 등 추가 증거가 필요하다.
2. Exit pixel gate 후 ABBA로 전체 추가 비용 <0.95ms 및 main 스파이크 감소를 확인한다. replan 원인은 `why`의 key-distance/key-bucket/visibility와 최대 프레임 phase/GC를 함께 판정한다.
3. Exit precision-local과 정상 pixel의 차이, 이어 Canvas/셰이더별 격리 캡처로 실제 ±1~2의 발생 장식을 귀속한다. 합성 fixture의 양자화 증명을 그 장식의 실게임 증명으로 대체하지 않는다.
