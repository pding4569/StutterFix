새로 기본 켬으로 채택한 성능 변경은 없다. GpuDeco=0을 유지했고 푸시·릴리스하지 않았다.
DD의 ntdll 대기 2,337표본은 모두 D3D11의 마지막 presentation 완료 대기 호출자 아래에 있었다.
Exit의 memcpy_repmovs 881표본 중 869개는 SpriteMask 렌더 노드 배열 재할당 경로였다. 홀드 메시 주원인 가설은 기각한다.
엔진 후킹 없이 네이티브 스택을 얻는 명시적 진단 도구를 추가했다. 새 성능 ABBA·픽셀 채택 검증은 하지 않았다.
7판 모두 DLL 복원·gpuprobe.dll 없음·GpuDeco=0을 확인했다. 제외 진단 3판의 명령 순서 오류는 무효로 보존했다.

## 범위와 조건

2026-11-03 작업을 이어서 `E:\StutterFixWork\wt-partial`, `claude/gpudeco-cheap`, 시작 `d90a307`에서 수행했다.
먼저 [cpu-extreme.md](cpu-extreme.md)를 읽었고 이미 이득 미확정으로 판정된 idle floor/holdreads/notify ABBA를 반복하지 않았다.
이번 실행은 우선순위 1·2의 호출자 미확인을 해소하는 데 집중했다. 아래의 미측정 항목은 완료나 채택으로 세지 않는다.

가설은 GPU 실행/출력 대기열, 렌더 스레드, 작업 스레드, 잠금, 필터·화면 복사·카메라·장식의 부담,
그리고 홀드 메시/재질 복사/엔진 렌더 목록 재할당이었다. 대기 함수의 호출자와 GPU timestamp를 분리해 조사했다.

게임 실행은 모두 `E:\StutterFixWork`에서 `run_locked.py`를 사용했다. 판마다 새 프로세스이며 실행 중 빌드·분석하지 않았다.
2560×1440 FullScreenWindow, D3D11, VSync=0, 목표 FPS=10000, GpuDeco=0, FrameGenOutside=0, FrameGenRefresh=false다.
나머지 설치 모드와 설정을 임의로 끄지 않았다. Quartz와 AdofaiGenBridge 등을 포함하는 현재 설치 환경의 결과다.
공유 게임 잠금을 사용했으며 CPU/GPU 클록이나 시스템 전체 부하를 고정한 시험은 아니다.

`phasewindow`는 대기가 포함된 프레임 단계 경과 시간이다. `gpuprobe`는 GPU timestamp 사이 경과 시간이며 GPU busy나 순수 shader 시간과 다르다.
명시적 샘플러는 메인 스레드를 잠시 정지하고 별도 스레드가 spin하므로 이 판의 FPS·표본 비율을 정상 플레이 성능으로 쓰지 않는다.
FPS 표시는 반올림되어 있으며 아래 프레임 ms도 그 정밀도를 넘는 측정값이 아니다.

## 항목별 판정

| 내용 | 커밋 | 전후 ms | 판 수·맵 | 기본값 | 검증·판정 |
|---|---|---|---|---|---|
| 1. 대기 호출자 | `6854a08` | 최적화 전후 미측정; DD 단계 기준 presentation 0.72ms | DD 단계/GPU 1, native 1 | 성능 동작 변경 없음 | native 14,931표본 중 해당 ntdll 리프 2,337개 모두 presentation 대기 호출자. 다른 맵까지 원인 확정하지 않음 |
| 1. GPU/카메라 경과 | `6854a08` | baseline GPU 3.3190ms; 본 카메라 3.2018ms | DD 50–65초 1 | GpuDeco=0 | 오류·overflow·unknown camera/effect 0; filter 시간은 카메라에 중첩되므로 합산 금지 |
| 1. 전체 렌더러 제외 진단 | `6854a08` | GPU 3.3190→1.2557ms; 본 카메라 3.2018→0.0443ms | DD 같은 구간 1+1 | 제품 적용 안 함 | 제외 상태 유지 확인. 화면 변경·큰 진단 비용 때문에 성능/픽셀 채택 대상 아님 |
| 1. 이미지·파티클·전체 제외 초기 명세 | `6854a08` | 원인 비교 무효 | DD 3 | 제품 적용 안 함 | gpuprobe 시작이 먼저 걸어 둔 gpusplit을 해제했음. 원시 보존·해석 제외 |
| 2. memcpy 호출자 | `6854a08` | 제거 전후 미측정 | Exit 88–103초 native 1 | 성능 동작 변경 없음 | 14,942표본, memcpy_repmovs 881개 중 SpriteMask 노드 재할당 869개. 복사량/호출 횟수 측정은 아님 |
| 3. FFT | 기존 `LowEnd.cs` 유지 | 새 비교 미측정 | 코드 확인만 | LowNoFft 기존 true 유지 | Volume 타일 없을 때 Float tracker Update 생략이 이미 있음. FMOD FFT 전체를 없앤다고 주장하지 않음 |
| 4. idle Update | 이전 cpu-extreme 결과 유지 | 새 비교 미측정 | 새 판 0 | 기존 꺼짐·Player 제외 | 이전 ABBA 무이득을 반복하지 않음 |
| 5. 재질·null·transform | 이전 결과 유지 | 새 비교 미측정 | 새 판 0 | 기존 유지 | hold material 재사용은 이전 무이득. 외부 재질 변경/객체 파괴 보장 없이 생존 검사를 제거하지 않음 |
| 6. PlayerLoop·카메라 | 변경 없음 | 새 비교 미측정 | 코드/기존 그래픽 로그 확인 | 기존 유지 | SendMouseEvents 사용처 존재. 기존 HDR/MSAA off 로그 확인. 전역 단계 제거 안 함 |
| 7. Mono 쓰기 장벽 등 | 변경 없음 | 미측정 | 새 최적화 시험 0 | 기존 유지 | 네이티브 호출자 도구만 추가. 값 형식/배열 전환의 성능·의미 검증은 미수행 |

## 대기: 확정된 호출자와 남은 원인

DD native 명세는 `waitsongpos 47` 이후 phasewindow를 설치하고 15초 수집했다. 대략 47–62초이며,
기존 `ep-dd`의 50–65초·RBP 12단계 표본과 같은 조건의 비율 비교가 아니다.
기존 48.7%와 이번 15.7%를 최적화 이득이나 대기 감소로 해석하지 않는다.

```
ntdll+0x9E0F4
  KERNELBASE+0x50D00
  GfxDeviceD3D11::WaitForLastPresentationAndGetTimestamp
  TimeUpdateWaitForLastPresentationAndUpdateTimeRegistrator::Forward
  ExecuteTimeUpdate
```

해당 리프 2,337/2,337개에 같은 호출자가 있다. 이 표본에서 일반 임의 잠금/작업 스레드 join으로 추정하던 불확실성을 줄였다.
presentation 경로라는 사실과 GPU shader 포화/드라이버 큐/출력 pacing의 세부 원인은 별개다.
현재 비관리자 세션이고 관리자 PerfView agent도 종료되어 있어 새 ThreadTime 기록은 얻지 않았다.
깨운 스레드/커널 대기 시간을 확정한 ETW 결과로 대체하지 않는다. HELLO·7777·Windflower native 대기는 이번에 재측정하지 않았다.

DD 50–65초의 별도 단계/GPU 판은 263.2 FPS, 단계 합계 약 3.80ms였다.
Update 0.95, presentation 단계 0.72, LateUpdate 0.66, FinishFrameRendering 0.58, Canvas 0.23ms다.
GPU 첫 카메라→end-of-frame 3.3190ms, 본 카메라 3.2018ms, BGMoving 0.0566, BGStatic 0.0091, Overlay 0.0366ms였다.
ScreenTile 효과는 전체 frame 평균 0.0016ms이며 활성 프레임 평균 0.0335ms였다. 활성 23프레임뿐이므로 두 평균을 섞지 않는다.
이 표본에서 그 효과 하나를 주원인으로 보는 근거는 없다. 전체 필터/모든 시점의 무비용 증명은 아니다.

## 제외 진단의 실패와 수정

초기 이미지/파티클/전체 제외 명세는 `gpusplit hide` 다음에 `gpuprobe on`을 호출했다.
`GpuProbe.StartProbe`가 `Stop`을 거치며 `GpuSplit.Stop`도 호출하므로 실제 측정 중에는 제외가 풀렸다.
이 3판의 거의 같은 GPU 시간을 이미지/파티클 비용 0의 근거로 쓰지 않는다.
초기 대화 중 그렇게 해석한 내용은 이 결과로 정정한다. 원시 로그·명세·SHA는 삭제하지 않았다.

수정된 `engine-phase-dd-all-v2`는 gpuprobe 시작 후 hide를 걸고 시작/종료 `state`가 모두 `all selected=47763`인지 검사했다.
GPU 오류 0, 외부 unhide 쓰기 누적 940건을 관찰했고 종료 시 복원했다.
본 카메라 구간은 0.0443ms로 줄었지만 전체 FPS는 123.6, 프레임 약 8.09ms였다.
FinishFrameRendering이 5.50ms로 커졌고, 모든 선택 renderer를 카메라마다 감시하는 진단 비용을 통제한 대조가 아니다.
draw 평균도 374.673→86.914로 감소했으나 모든 draw 0이 아니다. UI·카메라 culling mask 밖 객체도 남는다.
따라서 GPU 구간이 렌더 내용에 반응한다는 근거로만 남기고, 제품 이득·GPU busy·달성 가능한 FPS 상한으로 쓰지 않는다.
그룹별 올바른 재시험·진단 오버헤드가 같은 대조군·실제 보존 후보 ABBA는 미수행이다.

## memcpy: 홀드 메시 가설 대신 SpriteMask 렌더 목록

Exit native는 88초에 시작해 15초 수집했다. `memcpy_repmovs` 881/14,942 = 약 5.896%의 리프 표본이다.
그중 869/881 = 약 98.638%가 아래 경로다.

```
memcpy_repmovs
  ThreadsafeLinearAllocator<0>::Reallocate
  MemoryManager::Reallocate
  core::vector<RenderNode,...>::resize_buffer_nocheck
  SpriteMaskJobs::SpriteMaskPrepareRendererNodes<0>
  QueuePrepareIntegrateMainThreadObjects
  ExtractSceneRenderNodeQueue
  CullResults::GetOrCreateSharedRendererScene
```

전체 복사 표본의 대다수는 일반 `RenderOffscreenCameras`, 일부는 `scrCamera.UpdateCustomFrameRateScreen`의 별도 Camera.Render 경로였다.
이것은 복사 bytes·매 프레임 재할당 횟수나 개별 마스크 책임을 계측한 값이 아니다.
홀드 메시 함수의 매 프레임 정점 복사가 주원인이라는 앞선 의심을 이 표본에 적용하지 않는다.
일반 memcpy에는 FloorMeshRenderer.color의 재질 COW, UI shader properties, TMP mesh 등의 별도 경로도 있었다.

엔진 내부 배열 용량/할당자를 후킹해서 바꾸지 않았다. 마스크를 모두 끄는 것도 제품 후보로 넣지 않았다.
게임은 scrVisualDecoration.SetVisible/SetMaskingType, PlanetRenderer.ToggleSamurai, MaskLightGradient 등에서 실제 마스크를 사용한다.
대상/정렬 범위가 무효인 마스크만 안전하게 제거한다는 소유·무효화 계약은 아직 없으며, 원래 마스크를 캐시 값으로 대체하지 않았다.
후속 후보는 마스크/카메라별 렌더 목록을 줄일 수 있는 의미 보존 규칙이지만 이번에는 구현·ABBA·픽셀 검증하지 않았다.

## FFT·Update·프레임 단계의 중복/기각 확인

- `LowEnd.TrackerPrefix`는 LowNoFft=true에서 Volume 타일 여부를 확인해 scrVolumeTrackerFloat.Update를 이미 생략한다. 30프레임 감시와 ColorFloor 통지가 있는 기존 구현이며 이번에 새 최적화로 세지 않았다.
- scrConductor.spectrum getter는 getSpectrum=true를 세우며, Rewind에서 false로 초기화한다. scrVolumeTrackerScale은 그 값을 사용한다. scrVolumeTrackerFade/scrBarMaker 등도 GetSpectrumData를 부르므로 FMOD 리프 1.5%만 보고 결과 미사용으로 단정하지 않았다. 설치 모드의 모든 FFT 소비자는 이번에 전수 검증하지 않았다.
- 게임 코드에 TransformGizmo/XRHare의 OnMouseEnter/Exit와 firstpass의 OnMouseDown/Drag/Up이 있다. 사용처가 없다는 전제는 성립하지 않으므로 SendMouseEvents 전역 제거를 구현하지 않았다. 게임 화면 한정 사용처/설치 모드 전수 부재를 검증한 것은 아니다.
- HDR/MSAA를 다시 끄는 중복 변경은 하지 않았다. DD 그래픽 로그의 기존 값은 둘 다 false다. 깊이 텍스처·카메라별 추가 생략은 미검증이다.
- 이전 cpu-extreme에서 값 대조만 통과하고 ABBA 이득이 변동보다 작았던 idlefloor/holdreads는 그대로 보존했다. 이번 작업에서 성능 후보나 상시 플래그를 추가하지 않았다.

## 진단 구현·검증·복원

`profsamp begin 1000 native`에서만 현재 프로세스의 로드된 네이티브 image 범위를 준비하고
Windows x64 함수 테이블로 이전 호출 문맥을 읽는다. image 밖 Mono JIT 프레임은 기존 RBP 규칙으로만 진행한다.
모든 Mono/JIT 프레임을 Windows unwind로 완전히 푼다는 보장은 없다. stack scan으로 그럴듯한 주소를 채우지 않는다.
최대 깊이는 32이며 RSP 단조 증가/상한 검사를 두고, 대상 정지 구간은 finally에서 ResumeThread한다.
JIT/PInvoke 예열과 image 목록 준비는 대상 정지 전에 수행한다. 샘플러는 명시적 AUTOTEST/DEV 진단이며 일반 Player에는 없다.
Windows API 구현 근거는 [RtlVirtualUnwind](https://learn.microsoft.com/en-us/windows/win32/api/winnt/nf-winnt-rtlvirtualunwind)와 [x64 unwind 정보](https://learn.microsoft.com/en-us/cpp/build/exception-handling-x64)다.

실제 수집은 DD 14,931표본/native unwind 247,132프레임, Exit 14,942표본/262,281프레임이다.
측정 DLL은 `814c542cd75689c1406438b48915493f3587fea4fa2568213b90af2c71669309`다.
수집 뒤 잘못된 인자 거부/예외 보고·ResumeThread 정리를 다듬고 Player/PlayerAuto를 최종 재빌드했다.
최종 소스로 새 성능 시험을 했다고 주장하지 않는다. 측정 당시 DLL SHA와 각 명세는 요약에 보존했다.
Player 오류 0/경고 49, PlayerAuto 오류 0/경고 47; 일반 Player ILScan에서 ProfSamp 0개 형식을 확인했다.
새 픽셀 시험은 0판이다. 화면 보존이 필요한 제품 성능 후보를 채택하지 않았으므로 픽셀 통과로 보고할 항목도 없다.

UnityPlayer의 실제 RSDS GUID `0BD60840-8142-4C16-9079-D97BFE021469`, Age=1을 읽어 사용한 PDB 디렉터리와 일치함을 확인했다.
Unity DLL/PDB, 원시/해석 스택, 측정 DLL/로그/명세 SHA는 기계 판독 요약에 있다.
원시 로그/스택은 `E:\StutterFixWork\night-results\engine-*`에 그대로 남겼다.
7개 integrity 기록 및 최종 오프라인 확인에서 설치 DLL=restore/StutterFix-rc-player.dll, gpuprobe.dll 없음, GpuDeco=0이다.

- [요약·모든 판의 SHA/복원](evidence/engine-extreme/summary.json)
- [DD native 호출자](evidence/engine-extreme/engine-native-dd-callers.json), [Exit native 호출자](evidence/engine-extreme/engine-native-exit-callers.json)
- [DD 단계/GPU](evidence/engine-extreme/engine-phase-dd.txt), [수정된 제외 진단](evidence/engine-extreme/engine-phase-dd-all-v2.txt)
- [실행기](../../../tools/optimization/engine_extreme_runs.py), [오프라인 집계기](../../../tools/optimization/collect_engine_extreme.py), [명세](engine-steps)

성능 채택 0, 제품 변경 되돌릴 항목 0이다. 일시적 probe/split 상태는 종료·복원했고, 남긴 native 옵션은 유용한 호출자 진단 명령이다.
앞순위 두 항목의 호출자는 확인했으나, 모든 맵 공통 이득을 내는 수정은 아직 달성하지 않았다.
허용된 우선순위에 따라 뒤 항목의 새 최적화 실험은 진행하지 않았으며, 미측정 후보를 기본 켬으로 승격하지 않았다.
