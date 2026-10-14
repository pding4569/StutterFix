# PerfView 임무 결과

측정: 2026-10-15 00:22~01:06 KST. **새 성능 수정 0개, 이미지 한도 규칙 변경 0개.** Windflower의 같은 구간에서 36ms 프레임을 다시 잡아 메인 스레드의 출력 대기와 렌더/모드 네이티브 출력 스레드의 잠금 연결을 확인했다. 이 경로에는 임무가 수정 금지한 프레임 생성 코드가 포함된다. 따라서 수정하거나 설정을 끄지 않았다. NVIDIA 내부 지연의 더 깊은 원인은 미확인이다. HELLO 106.8초·Arche 271초·Timeline 3.74초의 원래 프레임은 이번 시도에서 재현되지 않았다. 미재현을 해결 완료로 표시하지 않는다.

## 조건과 한계

- 매 판 새 게임, `tools/sfmeasure/server.py`의 `sf_run` 직접 호출. 설정은 원본을 저장한 뒤 `ImageMaxSide=3072`만 시험용으로 고정하고 판마다 복원했다. 프레임 생성 설정은 원래 `FrameGenOutside=4` 등을 유지했다. 프레임 생성·셰이더·다른 모드 코드를 변경하지 않았다.
- Windflower/Arche/HELLO/Timeline 게임 화면은 처음부터 재생했다. `seek`는 **에디터 전용**이므로 별도 두 판은 에디터 재생이며 게임 화면과 같은 조건의 반복/A/B로 세지 않는다. 곡 전체 완주 시험이 아니라 지정 구간 조사다.
- 관리자 에이전트의 직접 `start mission-probe 5` 요청에 응답과 정상 ETL 생성이 확인됐다. 이후 자동 시험 `trace`로 ThreadTime 기록했다. PresentMon 대체나 `SaveCPUStacks`는 쓰지 않았다.
- Unity DLL의 현재 RSDS는 `E150A764C9E744F08907DE2DB3872D5D1`. 기존 캐시의 다른 버전 PDB를 쓰지 않고 공식 Unity 심볼 서버에서 해당 PDB를 받았다. 처음 필요한 Windows 심볼을 확보한 뒤 재분석은 로컬 캐시를 사용했다. NVIDIA·sfnative 일부는 주소로 남는다.
- 빌드·ETLX 변환·스택 분석은 게임 종료 뒤 수행했다. ETW 수집 자체의 비용은 남으므로 이 수치를 비계측 A/B의 성능 개선으로 세지 않는다. 기록의 병합은 대상 구간이 지난 뒤에 발생했다.
- HELLO 게임 화면 판에는 다른 Python 창이 게임 위에 겹치고 다른 프로세스 GPU 부하가 있었던 구간이 로그에 있다. 해당 판은 조건이 같은 FPS/개선 비교에 쓰지 않는다. 창을 강제로 닫거나 사용자 프로세스를 종료하지 않았다.
- 모니터의 `시각`은 `DateTime.Now`로 **알림을 출력한 시각**이다. 끊긴 프레임의 정확한 시작/끝이 아니므로 앞뒤 구간과 여러 스레드의 연결을 함께 해석했다. `seek`의 곡 시간은 선택한 타일의 `entryTime`과 재생 경과의 합으로 대략 대응하며 게임 화면의 시작 기준 위치와 완전히 같지 않다.
- 이번 중간 종료 판에는 곡 끝의 `[임무30]` 전체 프레임 배열이 출력되지 않는다. **모니터 알림 없음은 모든 30ms 초과 프레임 수가 0이라는 증명이 아니다.** 이번 결과는 대상 알림의 재현과 ETW 스택 조사이며 앞 임무의 전체 곡 집계를 대체하지 않는다.

## 수행한 판

| 판 | 경로·구간 | 기록 | 결과 |
|---|---|---|---|
| Windflower seek | 에디터, 타일 824/239.1초에서 약 45초 | mission-wind-seek, 65초 | 경과 22.9초 36ms, 효과 3.1ms, CPU 9.0/GPU 16.9ms, GC 0. 원래 구간 근처의 별도 조건 증거 |
| Windflower 게임 첫 시도 | 게임 화면, 처음부터 | 대상 기록 전 종료 | `wait 235`가 단계 제한 180초를 넘어 자동 시험이 정상 종료. 제외 |
| Windflower 게임 재시도 | 게임 화면, 처음부터 약 304초 | mission-wind-game2, 65초 | **264.4초 36ms** 재현. 효과 0.0ms, CPU 7.0/GPU 19.6ms, GC 0, 알림 시각 00:37:59.422 |
| HELLO 게임 | 게임 화면, 처음부터 약 170초, 맵 열기도 기록 | mission-hello-open-game, 180초 | 원래 106.8초 55/41ms 알림 미재현. 창 겹침 조건 있음 |
| Arche 게임 | 게임 화면, 처음부터 약 309초 | mission-arche-game, 65초 | 원래 271초 52ms 알림 미재현 |
| HELLO seek | 에디터, 타일 6758/82.0초에서 약 45초 | mission-hello-seek, 65초 | 경과 7.0초 35ms, GPU 33.2/CPU 7.0ms, 효과 0.1ms, GC 0. **원래 106.8초와 다른 위치** |
| Timeline 게임 | 게임 화면, 처음부터 약 60초 | mission-timeline-game, 45초 | 원래 3.74초 37ms 알림 미재현 |

총 게임 실행 7판, 유효 조사 6판, 단계 시간 초과로 제외 1판. 모두 게임 내 자동 종료를 사용했고 sfmeasure의 강제 종료는 없었다. 제외 판 외에는 StutterFix/자동 시험 설치 실패·단계 실패 없음. 기존 게임 필터 예외가 전혀 없었다는 뜻은 아니다. 각 판의 명령·로그 경로는 `perfview-evidence/*.json`에 남겼다. 긴 대기는 두 단계로 나눠 단계 제한을 지켰다.

## 1. Windflower: 출력 완료 대기와 네이티브 출력 경합

게임 PID **17808**. ETW 시작 00:37:34.256. 모니터 알림 전후 약 105ms를 분석했다. 아래는 수정된 도구의 `wind-game2-corrected.txt`에서 얻은 값이다.

| 스레드 | 직접 확인한 대기 | 깨운 쪽·의미 |
|---|---|---|
| 메인 24516 | `RenderOffscreenCameras → Camera::CustomRender → GfxDeviceClient::BeginFrame → WaitForPendingPresent → PlatformGfxSemaphore::WaitForSignal → WaitForSingleObjectEx`, **UserRequest/Wait 24.4ms** (기록 상대 25115.6~25140.0ms). 인접한 이전 대기 25.7ms도 있음 | 게임 렌더 스레드 19524가 `GfxDeviceWorker::RunCommand → Baselib_SystemSemaphore_Release`로 깨움 |
| 렌더 19524 | `GeometryJobTasks::ScheduleGeometryJobs → BufferD3D11::BeginWrite → D3D11 Map → CondObjectLock → RtlEnterCriticalSection`, **이유 37/Wait 14.9ms** (25083.4~25098.4ms) | 출력 스레드 21144가 `sfnative → RtlLeaveCriticalSection`으로 풀어 줌 |
| 네이티브 출력 21144 | `sfnative → RtlEnterCriticalSection`, **이유 37/Wait 17.0ms** (25123.0~25140.0ms) | 렌더 19524가 `GfxDeviceD3D11::PresentFrame → sfnative → Steam overlay → DXGI Present → RtlLeaveCriticalSection`에서 풀어 줌 |

CPU 표본에는 `DXGI FlipPresentToDWM → D3D11 Present → nvwgf2umx → SleepEx/NtDelayExecution`가 반복된다. 일부는 메인 대기와 겹친다. 이는 **Present 완료를 기다리는 메인 스레드, D3D11 컨텍스트의 잠금을 공유하는 렌더/네이티브 출력 스레드**라는 직접 확인된 연결이다. 두 잠금은 순서가 달라 시간 합을 하나의 프레임 비용으로 합산하지 않는다. 한 스레드가 항상 유일한 범인이라고 단정하지도 않는다.

해당 메인 대기와 거의 겹치는 24.4ms 좁은 창의 게임 전체 CPU 표본은 **29개**다. 105ms 넓은 창은 **165개**, 메인 28개·렌더 55개·출력 45개였다. 메인은 기다리지만 게임 전체 CPU가 0은 아니다. 표본 수는 여러 코어의 합이며 정확한 CPU 시간/사용률이 아니다.

**VRAM 규칙 변경 근거는 확보하지 못했다.** 로딩 뒤 VRAM 전체 7006MB, 게임 5401MB였다. 같은 창의 System 분석과 게임의 대기 연결에서 `PageInOneAllocation`/VidMm Evict가 주원인임을 확인하지 못했다. 모든 순간에 VRAM 이동이 없었다는 증명은 아니다. 사용량이나 GPU 표본만으로 이미지 한도를 낮추거나 `VramGuard`에 영구 기록하지 않았다.

소스의 `native/sfnative/framegen.h`에는 D3D11 `ContextLock`의 Enter/Leave와 그 보호 아래의 Present가 있어 관측한 연결과 부합한다. 다만 sfnative PDB가 없어 각 주소를 정확한 C++ 함수/행으로 확정하지 못했다. **프레임 생성 수정 금지**에 따라 해당 파일·설정·셰이더를 바꾸지 않았으며, 켬/끔 A/B도 하지 않았다. NVIDIA Present가 왜 그때 길어지는지는 추가 미확인이다. ‘드라이버/OS 자체라 모드와 무관’으로 닫지 않는다.

## 2. HELLO·Arche·Timeline

HELLO 게임 화면의 원래 106.8초 두 프레임과 Arche 271초·Timeline 3.74초 프레임은 이번 시도에서 같은 알림으로 잡히지 않았다. 근처 ETW를 분석했지만 **존재하지 않는 원래 끊김의 대기 원인을 추정해 붙이지 않았다.** 해결 수치나 전→후 비교 없음.

HELLO 에디터의 추가 35ms 프레임은 다른 위치다. 알림 시각 00:58:47.078, PID 22816. 메인 22860이 **UserRequest/Wait 29.3ms** 대기하고 렌더 7788이 깨운다(`hello-seek-stall.txt`). GPU 표본은 33.2ms였지만 이 표본만으로 VidMm 이동을 확정할 수 없다. 이 판은 원래 HELLO 106.8초의 재현 횟수에 포함하지 않는다.

## 3. 끊김 밖 비용·할당·로딩

CPU 표본에서 모드 네이티브 경로가 포함된 비중은 다음과 같다. **자체 CPU 비중이 아니라 포함 스택 비중**이다. 드라이버·엔진 일을 네이티브 모드의 자체 비용으로 계산하면 안 된다.

| 분석 창 | 게임 전체 표본 | sfnative 포함 표본 | 해석 |
|---|---:|---:|---|
| Windflower 알림 앞뒤 105ms | 165 | 89 (53.9%) | 출력 경로가 관측 표본의 큰 몫. 드라이버 Present도 포함 |
| HELLO 원래 대상 근처 2초 | 5790 | 347 (6.0%) | 원래 끊김 미재현, 배경 창/부하 조건 있음 |
| Arche 271초 근처보다 조금 뒤의 2초 창 | 5389 | 126 (2.3%) | 끊김 밖 CPU 분포 참고용. 대상 구간은 별도 넓은 창에서도 분석 |

- 상위 엔진/게임 경로: 렌더의 D3D11 Present/Map/버퍼·셰이더 값 준비, 메인의 Mono/JIT 실행·Canvas 업데이트·일부 FMOD spectrum 처리. HELLO 메인 표본 1931개 중 미해결 Mono/JIT 후보 1115개가 있어 **관리 코드 StutterFix/게임/다른 모드의 정확한 분리 비중은 계산하지 못했다.** 함수 이름이 없는 주소를 모드 함수로 간주하지 않았다.
- 곡 중 `mono_gc_alloc_string`, `mono_gc_alloc_vector`, `GC_malloc_kind`, `Material_CUSTOM_GetShaderKeywords` 등이 보인다. 이는 할당 경로이지 GC 정리 발생 증거가 아니다. 주 끊김 로그는 GC 0회였다. `/ClrEvents:None`인 ThreadTime 기록으로 정확한 바이트/초나 모든 GC 횟수를 산출하지 않았다. 호출자를 JIT 함수 이름으로 연결하거나 기존 코드와 같은 결과를 입증하지 못해 캐싱·생략을 추가하지 않았다.
- **HELLO 맵 열기 메인 대기 122.7ms:** `InstantiateAsyncObjectsData → ujob_wait_for`, 게임 작업 스레드의 `job_completed`가 깨운다. 기존 `DecoBatch`/Unity 비동기 복제의 잔여 작업 대기다. Awake·객체 순서·다른 모드 결과를 보존하는 새로운 생략 근거 없음.
- **HELLO 로딩 약 100ms 대기 여러 번:** 메인의 Mono `Monitor_wait`, 같은 시각 작업 스레드의 sfnative 이미지 경로 및 turbojpeg 활동이 보인다. 기존 이미지 작업의 완료/대기 경로와 일치하나 익명 JIT 호출자를 완전히 연결하지 못했다. 100ms 대기가 보인다는 이유만으로 폴링 주기를 줄여 성능 이득을 주장하지 않았다.
- 로딩 CPU 상위에는 D3D11 Texture2D 생성, 이미지 풀기/압축, 프리팹 생성이 있다. HELLO 게임 화면 열림 13.8초(2초 안정화 포함), 기존 이미지 전체 6.2초·이미지 기다림 2.259초·로딩 후 GC 103ms. 이 수치는 기존 최적화의 잔여 비용이며 새 성과가 아니다. 에디터 열기는 경로가 달라 같은 표의 A/B로 비교하지 않는다.

**수정 후보 채택/A/B 0개.** 직접 관측한 큰 곡 중 경합은 금지 범위에 있고, 다른 후보는 호출자·동일 화면·예상 이득을 입증하지 못했다. 따라서 A/B 4판을 하지 않았으며 성능 개선을 주장하지 않는다. 새 기본 설정·효과 차단·추측성 VRAM 규칙을 남기지 않았다.

## 4. 분석 도구 수정과 검증

`tools/etwstall/Program.cs`만 코드 변경했다. 게임 DLL에 새 측정·Harmony·정적 이벤트·PlayerLoop를 추가하지 않았으므로 새 `Main.Unload` 복원 대상도 없다.

- CSwitch의 일반 `CallStack()`은 **잠든 스레드의 스택이 아니었다.** 다른 프로세스의 스택이 표시되는 것을 발견하고 TraceEvent 공식 API XML에서 `BlockingStack()`을 확인했다. `log.CallStacks[c.BlockingStack()]`로 바꿔 차단된 스택을 표시한다.
- 깨운 스레드 소속은 해당 시각의 `GetThread(tid, time)`으로 조회해 ID 재사용 오해를 줄인다.
- 구간 전 최대 200ms의 문맥 전환도 상태 복원에 사용한다. CPU 표본 0개인 대기 스레드 출력, PID 목록, 로컬 심볼 전용 모드, 게임 전체/스레드별 표본 집계를 추가했다.
- 수정 도구 빌드: 오류 0/경고 0. 실제 Windflower 두 기록과 HELLO 로딩·구간, Arche, Timeline을 다시 분석했다. 메인 차단 스택이 `WaitForPendingPresent`로 풀리고 깨운 렌더 스레드와 일치하는지 확인했다. CPU 표본 0개인 대기 스레드도 출력됐고 빈 표본의 Max 예외 없음.
- **최종 증거는 `*-corrected.txt`와 최종 manifest가 지정한 파일이다.** 이전 `*-stall.txt`/`*-symbols.txt` 중 Windflower와 HELLO 게임 로딩의 최초 분석은 일반 CallStack 오류가 있어 최종 대기 근거로 쓰지 않는다. HELLO seek의 `hello-seek-stall.txt`는 수정 빌드로 만든 최종 파일이다.
- 도중 BlockingStack의 인덱스 형식 때문에 빌드 1회 실패했고, 실행 중인 분석 exe를 덮어쓰려는 빌드도 실패했다. 종료 뒤 재빌드·재분석했다. 최종 성공 결과만 근거로 사용했다.

사용법과 남은 한계는 `tools/etwstall/README.md`에 적었다. 도구 수정은 게임 성능 수정과 구분한다.

## 5. 종료·복원 확인

- 자동 시험 Player 빌드와 정식 Player 빌드 모두 오류 0. 정식 빌드 기존 경고 25개. LoadCheck 종료 코드 0, 이름 기반 필드 참조 검사 통과.
- 설치 DLL은 **정식 `bin/Player/StutterFix.dll`**과 SHA-256 일치: `CB634D735758EBE0E38A1E45320F0DD7D2919C7F7408E6BA0520814C3AD46858`. 앞 임무 종료 시 정식 DLL과도 동일하다. 자동 시험 DLL을 남기지 않았다.
- Settings.xml 원본/복원 SHA-256 일치: `A73F5C606FB838641BA3B6268BD7C88A3DC1BD9E01B2A399559CC836F6DDCE4A`. 원래 자동 이미지 한도 -1과 프레임 생성 설정을 포함해 바이트까지 복원했다.
- 게임 프로세스 0개, 대기 `autotest.txt` 없음, `.sfmeasure.bak` 없음. `perfview-evidence/restoration.json`에 검사 시각·값 보존.
- 커밋·푸시·태그·릴리스·브랜치 변경 없음. 기존 `.codex/config.toml` 및 사용자 작업 파일은 보존했다.

검토할 것은 **Windflower의 출력 완료/네이티브 경합 근거와 금지 범위로 수정하지 않은 판단**, 원래 HELLO/Arche/Timeline 프레임의 미재현 한계, 오프라인 분석 도구의 BlockingStack 수정이다. 이번 임무로 끊김을 줄였다는 수치는 없다.

## 증거 위치

- `perfview-evidence/manifest.json`: 판별 원본 로그·최종 분석·ETL/ETLX 경로, 모니터 줄, 명령, 실패 판 구분.
- `perfview-evidence/wind-game2-corrected.txt`, `wind-seek-corrected.txt`, `wind-main-wait-cpu.txt`, `wind-system-corrected.txt`: 핵심 출력 대기·CPU·System 근거.
- `perfview-evidence/hello-open-corrected.txt`, `hello-target-corrected.txt`, `hello-seek-stall.txt`, `arche-target-wide.txt`, `timeline-target.txt`, `wind-outside.txt`: 로딩·대상 근처·추가 프레임·끊김 밖 분석.
- 원본 ETL/ETLX 및 수집 성공 로그: `C:/Users/Public/StutterFixTrace/mission-*.etl`, `.etlx`, `*-collect.log`. 큰 바이너리를 저장소에 복사하지 않았다. 최종 manifest는 이 임무에서 만든 파일만 명시한다.
- `perfview-evidence/build-auto.log`, `build-player.log`, `loadcheck-player.log`, `restoration.json`: 빌드와 복원 근거.

## 추가 확인 (Claude, 2026-10-15): 남은 곡 중 끊김은 프레임 늘리기가 켜져 있을 때만 나온다

Windflower 곡 전체(362초 완주), 같은 DLL(2.7.2 자동 시험판)·같은 설정에서 프레임 늘리기만 바꿈:

| | 30ms 초과 프레임 | 곡 중 가장 긴 프레임 | 평균 FPS |
|---|---|---|---|
| 프레임 늘리기 켬(사용자 설정, 고정 4배) | 5개: 263.8초 31ms, 263.9초 40ms, 264.5초 34ms, 264.55초 60ms(+시작 직후 37ms) | 60ms | 214 |
| 프레임 늘리기 끔 | **0개** | 24ms | 267 |

- 앞 임무 표의 곡 중 30ms 초과 프레임 7개는 모두 사용자 설정(프레임 늘리기 켬)에서 잰 것이다. Windflower 는 끄면 사라진다.
- 시험한 수정: 출력 스레드의 `Present` 를 D3D11 컨텍스트 잠금 밖으로 옮김 → 같은 구간 31/40/34/60ms 로 **변화 없음(오히려 나쁨)**, 되돌림. 잠금 보유가 원인의 전부가 아니다.
- 따라서 남은 곡 중 끊김은 프레임 늘리기의 GPU 포화 구간(Windflower 263~265초, GPU 완료 13~20ms) 비용이다. 프레임 늘리기를 끈 사용자에게는 이 맵들에서 곡 중 30ms 초과가 사실상 0이다.
