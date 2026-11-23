# DropScan 구현·오프라인 검증 결과 (2026-11-23)

## 작업 상태

인계 문서 `docs/handoff/dropscan-20261123.md`에 따라 전곡 프레임 기록, ProfSamp full 모드, 오프라인 분석기를 구현했다. **게임 실행·종료, 설치 DLL 교체, 커밋·푸시는 하지 않았다.** 빌드 출력은 이 체크아웃의 `bin/`에만 생성했다. 기존 미추적 인계 문서와 `out/` 자료는 보존했다.

- 작업 폴더: `E:\StutterFixWork\wt-v14prod`
- 브랜치: `claude/v3-282port`
- 작업 기준 HEAD: `ec2673ccac423efc4777fa8d2eea28690a6fc9af`
- 게임 측정·실제 병목 확정·FPS 개선 검증은 하지 않았다. 아래 예시는 Claude가 이후 실행할 명령이다.

## 변경 파일

경로는 작업 폴더 기준이다.

| 경로 | 변경 내용 |
|---|---|
| `DropScan.cs` | AUTOTEST 전용. 시작 시 60만 행 배열 확보, 매 프레임 값 형식 대입, 종료 시 CSV와 상한 메타데이터 저장 |
| `PerfOverlay.cs` | Update 시작점에서 songposition_minusi·Unity 프레임 번호 공개, DropScan 기록, 기존 지연 CPU/GPU 값 재사용 |
| `ProfSamp.cs` | full/max 모드, 표본별 T/F/W/S, 기본 15만 표본·깊이 32, ring 끊김 보관 상한 500, 정지 비용 계수, 스트리밍 출력 |
| `AutoTest.cs` | `dropscan begin` / `dropscan end` 명령 등록 |
| `StutterFix.csproj` | AUTOTEST에서만 `DropScan.cs` 컴파일 |
| `tools/dropscan/analyze.py` | Python 3.12 표준 라이브러리 분석기. 맵별 프레임·표본·로그 입력, 원인별 통합 MD/JSON 출력 |
| `tools/dropscan/test_analyze.py` | 가짜 입력 회귀 시험 5개. 실제 게임 자료·게임 실행 불필요 |
| `tools/dropscan/Fixture/Fixture.csproj` | 실제 DropScan/ProfSamp 소스를 연결한 독립 .NET 8 시험 프로젝트 |
| `tools/dropscan/Fixture/Program.cs` | 60만 행 상한·할당 0·구간 시각·문화권·기존/신규 표본 출력 검증. 네이티브 정지 API는 호출하지 않음 |
| `docs/handoff/dropscan-20261123-result.md` | 이 결과 및 다음 측정 절차 |

기존 normal/ring 모드의 `M:…;U:…;X:…`와 ring의 `###` / `@` 줄은 유지했다. `T/F/W/S`는 full 모드에만 붙는다. 파일 쓰기 실패는 성공처럼 숨기지 않고 호출자에게 전달한다. 표본 스레드가 2초 안에 끝나지 않으면 핸들·배열을 보존하고 end 재시도를 알린다. 지연 뒤 표본을 연속으로 몰아서 수집하는 동작도 억제했다.

## 검증 결과

| 검사 | 결과 | 근거 파일 |
|---|---|---|
| DEV Release 빌드 | 오류 0, 경고 85 | `out/dropscan-dev-build.txt` |
| Player Release 빌드 | 오류 0, 최종 증분 빌드 경고 4 (전체 컴파일 때 65) | `out/dropscan-player-build.txt` |
| Player + AUTOTEST Release 빌드 | 오류 0, 경고 65 | `out/dropscan-auto-build.txt` |
| DEV LoadCheck | FieldRefAccess 314곳, 틀림 0, 동적 이름 7곳 | `out/dropscan-dev-loadcheck.txt` |
| Player LoadCheck | 255곳, 틀림 0, 동적 이름 7곳 | `out/dropscan-player-loadcheck.txt` |
| AUTOTEST LoadCheck | 315곳, 틀림 0, 동적 이름 8곳 | `out/dropscan-auto-loadcheck.txt` |
| 분석기 자체 시험 | 5개 통과 | `out/dropscan-analysis-selftest.txt` |
| 기록/표본 출력 fixture | 600000행, 누락 2행 표시, 프레임당 할당 0바이트, legacy/full 직렬화 통과 | `out/dropscan-frame-selftest.txt` |
| 실제 로컬 symb_lib 로드 | 가짜 자료 분석·MD/JSON 생성 성공 | `out/dropscan-symbol-smoke.txt`, `out/dropscan-native-smoke/` |

경고는 0이 아니다. LoadCheck는 이름이 정적으로 알려진 FieldRefAccess 형식 검사이며 동적 이름과 Unity/Mono 런타임 동작까지 검증하지 않는다. 실제 symb_lib 시험도 가짜 RVA를 사용한 로더 연결 시험으로, 실제 게임 함수·병목 검증은 아니다.

자체 시험은 정상 곡의 검출 0, 단일 끊김, 2초 동안 새 프레임이 없는 정지, 1초 이상 지속되는 저FPS, 시작/끝 경계, 재귀 함수 중복 제거, 겹치는 끊김/드랍 시간 중복 합산 방지, 두 맵의 같은 네이티브 함수 통합, 기호 로더, 기존 시각 없는 형식의 명시적 제외, 상한/메타 누락 경고, 잘못된 시간·seek 역행 거부를 확인한다.

재현 명령(PowerShell, 작업 폴더에서):

```powershell
dotnet build -c Release --no-restore
dotnet build -c Release -p:Edition=Player --no-restore
dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1 --no-restore
dotnet build tools/LoadCheck -c Release --no-restore
dotnet tools/LoadCheck/bin/Release/net8.0/LoadCheck.dll bin/Release/StutterFix.dll
dotnet tools/LoadCheck/bin/Release/net8.0/LoadCheck.dll bin/Player/StutterFix.dll
dotnet tools/LoadCheck/bin/Release/net8.0/LoadCheck.dll bin/PlayerAuto/StutterFix.dll
py -3.12 tools/dropscan/test_analyze.py
dotnet run -c Release --project tools/dropscan/Fixture -- out/dropscan-frame-fixture
```

## Claude의 맵별 측정 순서

각 맵마다 AUTOTEST 명령을 한 줄씩 실행한다. 아래 `game` 인자는 표시 이름이 아니라 **해당 맵의 실제 .adofai 절대 경로**로 바꾼다.

```text
auto on
game <맵의 실제 .adofai 절대 경로>
press
dropscan begin
profsamp begin 250 native full
waitend 800
profsamp end frames
dropscan end
quit
```

순서대로 적용할 맵:

```text
HELLO
Windflower
7777
DD
Exit
scam
eLadia
Timeline
PLUM
Lost Requiem
Flower
Arche
```

`full` 기본 상한은 150000개로 250Hz 기준 약 600초다. 10분을 넘는 곡 또는 800초 대기 전체를 덮어야 하면 같은 명령 줄을 다음처럼 바꾼다. 인자는 1..1000000 범위이며 큰 값은 메모리를 더 쓴다.

```text
profsamp begin 250 native full max=210000
```

`waitend 800`은 시간 초과 시에도 단계를 끝낸다. 로그의 `곡 끝남`을 확인하고, `곡이 끝나지 않음`이 있으면 완주 자료로 취급하지 않는다. 본 작업에서는 이 명령을 실행하지 않았다.

매 판 종료 후 모드 폴더의 아래 파일과 그 판의 Player.log를 맵별 별도 폴더에 보관한다. 고정 파일명이므로 다음 판에서 덮어쓴다.

```text
dropscan-frames.csv
dropscan-meta.json
profsamp-frames.txt
profsamp-meta.json
Player.log
```

`profsamp-meta.json`의 `capacity_reached` / `sampling_error` / `failed_contexts` / `failed_suspends`와 `dropscan-meta.json`의 `truncated` / `skipped_frames`를 함께 확인한다. 상한 도달 또는 수집 실패가 있는 판은 전곡 원인 포괄성을 주장할 수 없다.

## 출력 형식과 시간 연결

프레임 CSV 헤더:

```csv
frame,song_s,song_end_s,wall_start_s,wall_end_s,frame_ms,cpu_delayed_ms,gpu_delayed_ms,playing,record_us
```

- `frame`: **구간 시작점**의 Unity frameCount. 다음 Update 시작점까지의 시간·표본을 이 번호에 연결한다.
- `song_s`, `song_end_s`: 구간 양 끝에서 메인 스레드가 읽은 conductor 시각. 음수 또는 conductor가 없으면 NaN도 보존한다.
- `wall_start_s`, `wall_end_s`: 같은 프로세스의 Stopwatch ticks / Frequency. UTC 시각이 아니다.
- `frame_ms`: 같은 Update 지점 사이의 Stopwatch 차이. 구간 마지막 불완전 행과 CSV 쓰기 자체는 제외한다.
- `cpu_delayed_ms`, `gpu_delayed_ms`: 기존 PerfOverlay가 가진 최신 지연 값. 수집이 꺼졌거나 값이 없으면 NaN. 새 GPU 캡처나 OS CPU 시간 조회는 추가하지 않았다. CPU 값은 환경에 따라 전체 CPU 프레임 시간 fallback도 포함하며 **OS 메인 스레드 실행 시간 몫이 아니다.**
- `playing`: 구간 시작점의 Hitch.Playing. 이 플래그도 기존 게임 상태 추정이다.
- `record_us`: 기록 함수 본문 일부의 벽시계 비용. conductor/Time 접근·공개 변수 갱신·함수 전체 진입/종료 비용까지 포함한 총계가 아니다.

full 표본 한 줄 예시:

```text
T:262.125;F:123456;W:98765.4321;S:8.4;M:StutterFix.SomeClass:Method();U:ABCDEF;X:[ntdll.dll+0x1234]
```

`T`는 메인 스레드가 마지막 Update에서 공개한 volatile float 곡 시각이고 `F`는 공개한 프레임 번호다. **긴 프레임 안에서는 T가 진행하지 않는다.** 분석기는 이 문제를 피하려고 `W`(표본 채취 Stopwatch 초)를 프레임 CSV의 실제 구간에 연결한다. 공개 변수 두 개를 쓰는 중 정지되어 F와 W가 어긋난 경계 표본도 W로 연결하고 수를 보고한다. `S`는 SuspendThread 직전부터 ResumeThread 복귀까지의 마이크로초다.

기존 `profsamp begin 1000 native` + `profsamp end frames`는 시간 접두어 없이 기존 형식을 출력한다. 기존 ring은 `profsamp-hitches.txt` 형식을 유지하고 끊김을 최대 500개 보관한다. 기존 분석 도구에 full 파일을 주면 접두어 때문에 리프 위치 해석이 달라질 수 있으므로 새 full 자료는 DropScan 분석기로 분석한다. 시각 없는 과거 frames/ring 자료로 전곡 시간 분석을 복원하지 않는다.

## 오프라인 분석 명령

`--map` 하나가 맵 이름·프레임 CSV·표본 파일·Player.log 네 인자를 받는다. 여러 번 반복하면 맵을 가로질러 원인을 묶는다. Player.log가 없으면 네 번째 인자에 `-`를 넣으면 되고, 보고서에 누락을 표시한다.

```powershell
py -3.12 tools/dropscan/analyze.py --map "HELLO" "out/dropscan/HELLO/dropscan-frames.csv" "out/dropscan/HELLO/profsamp-frames.txt" "out/dropscan/HELLO/Player.log" --map "Arche" "out/dropscan/Arche/dropscan-frames.csv" "out/dropscan/Arche/profsamp-frames.txt" "out/dropscan/Arche/Player.log" --symb-lib "E:/StutterFixWork/tools-sym/symb_lib.py" --out "out/dropscan/combined"
```

위 `out/dropscan/<맵>/`는 Claude가 수집 자료를 보관할 경로 예시이며 이번 작업에서 실제 측정 파일을 만들었다는 뜻이 아니다. 기호 없이도 실행할 수 있으며 `--symb-lib`를 생략하면 U:는 RVA별로 유지한다. 저장소 밖 로컬 모듈의 `name(int RVA)` 함수를 호출한다. symb_lib 내부의 UnityPlayer/PDB 경로와 버전은 실제 측정 빌드와 맞아야 한다. 그 로컬 파일은 수정하거나 저장소에 복사하지 않았다.

생성 파일:

```text
<출력 폴더>/dropscan-report.md
<출력 폴더>/dropscan-report.json
```

분석 기준:

1. 시작·기록 끝 5초를 제외한 재생 프레임 시간 중앙값을 정상 기준으로 삼는다. 중앙 구간이 없으면 전체 재생 자료로 대체하고 경고한다.
2. 끊김은 `frame_ms > max(20ms, 중앙값 × 2.5)`. 재생 전·재생 밖의 긴 프레임도 별도 경계로 표시한다.
3. 벽시계 0.5초 고정 창 FPS가 해당 맵 창 중앙값의 80% 미만인 창이 연속 2개 이상이면 드랍이다. 창을 가로지르는 프레임을 시간 비례로 배분해 프레임 하나가 2초 멈춘 경우도 검출한다. 완전히 채워지지 않은 창은 FPS 기준에서 제외한다.
4. 끊김/드랍의 각 함수 포함 표본 비율에서 그 맵 정상 구간의 포함 비율을 뺀다. 재귀는 한 표본에서 한 번만 센다. 관리 M:와 엔진 네이티브 U: 중 양의 차이를 보이는 상위 10개를 JSON에 남긴다.
5. 겹친 끊김/드랍 프레임의 합집합을 연속 구간으로 나누고, 가장 큰 포함 비율 차이를 보이는 대표 함수 하나로 맵 간 묶음을 만든다. 대표 함수별 `Σ(구간 frame_ms − 정상 frame_ms) × 포함 비율 차이`를 합산한다. 시작·끝·재생 밖 추정 시간은 본 구간과 다른 열로 표시한다.
6. 각 구간의 표본 수, 표본이 있는 프레임 수, 표본 정지 비용 합의 상한 추정, 실제 모니터 분류 줄, 수집 상한/누락 경고와 오버헤드 요약도 JSON에 보존한다. 정상 표본이 없거나 구간 표본이 0이면 원인·절감 시간을 지어내지 않는다.

우리 모드·게임/외부 모드 관리 코드·엔진/런타임 관리 코드·엔진 네이티브를 구분한다. 관리 함수 문자열만으로 다른 모드와 게임을 완벽하게 구별할 수 없어 게임/외부 모드는 함께 표시한다. X:는 원시 표본에 보존하지만 M:/U: 원인 순위에서는 제외한다. Player.log의 모니터 곡 시각은 conductor 시각과 다를 수 있어 ±1초 근처 분류는 **미확정 참고**로만 연결한다.

## 오버헤드와 판정 한계

프레임 기록은 고정 배열 대입과 Stopwatch 조회만 사용한다. .NET 8 fixture에서 600002회 추가 Tick 루프 **35.334ms**, 프레임당 할당 **0바이트**였다. 이는 Unity Mono 안의 실제 비용이 아니며 실제 게임에서 같은 수치를 보장하지 않는다. CSV/문자열 생성은 end에서만 한다.

기본 full 배열은 `150000 × (32 × 8 + 4 + 4 + 8 + 8) = 42000000바이트`(약 40.1MiB)이며 배열 헤더·기호 해석 캐시 등은 별도다. 프레임 행은 대략 56바이트 × 600000 = 33.6MB다. begin에서만 확보하며 이후 매 프레임 새로 만들지 않는다. `max=210000`이면 full 배열은 약 58.8MB다.

표본 스레드 우선순위는 기존 **AboveNormal**을 유지했다. 기존 SpinWait 대기도 유지하므로 메인 스레드 정지 시간 이외에 작업 스레드가 코어 하나를 상당 부분 사용할 수 있다. 부하가 큰 CPU에서는 이 경합도 영향을 줄 수 있다.

250Hz의 정지 비용은 측정 후 다음처럼 평가한다.

```text
표본당 평균 정지 상한(ms) = suspend_total_ms / (attempts − failed_suspends)
전 구간 정지 상한 비율(%) = suspend_total_ms / (elapsed_s × 1000) × 100
예: 평균 10us라면 250 × 10us = 2.5ms/초, 약 0.25%
예: 평균 100us라면 25ms/초, 약 2.5%
```

예시 10us/100us는 실측값이 아니다. `profsamp-meta.json`의 전체 정지 합/최대, 분석 JSON의 중앙값/p99/최대와 구간별 `suspend_upper_estimate_ms`를 확인한다. SuspendThread 직전~ResumeThread 복귀 벽시계에는 래퍼·OS 선점이 포함되므로 엄밀한 메인 스레드 정지 시간이나 GPU/Present 대기 시간이 아니다. `attempts`는 전체 시도, `failed_suspends`는 정지 실패, `failed_contexts`는 정지 후 GetThreadContext 실패다. 성공 정지 횟수가 0이면 평균을 계산하지 않는다.

**실제 게임을 실행하지 않았으므로 250Hz가 끊김을 만들지 않는다고 판정하지 않았다.** 표본 수집 판은 원인 후보를 찾는 진단 자료다. 최적화 전후 성능 채택은 표본 수집을 끈 별도 판에서 검증해야 한다.

추가 한계:

- 250Hz 간격은 약 4ms이며 짧은 프레임에는 표본이 없을 수 있다. 깊이 32 밖 호출자는 보이지 않는다. 표본 수가 적은 후보는 불확실하다.
- `press` 이후 begin이므로 그 이전 프레임은 기록하지 않는다. 첫 완전한 Update 구간부터 기록한다. ProfSamp begin의 배열/모듈 초기화와 end의 기호 해석·파일 쓰기가 시작/끝 경계에 잡힐 수 있으므로 본 구간 이득과 분리한다.
- 60만 프레임은 1000FPS에서 약 600초, 500FPS에서 약 1200초다. 상한 뒤 행은 버리되 개수를 메타데이터로 남긴다.
- 끝 5초는 **기록된 재생 끝** 기준이다. 완주를 자동으로 증명하지 않는다. `waitend` 결과와 대조한다.
- 0.5초 고정 창 경계에 따라 정확히 1초인 저하가 한 창과 두 부분 창으로 나뉘어 검출되지 않을 수 있다. 단일 긴 프레임 목록은 별도로 남긴다.
- 정상 기준은 그 맵의 중앙값이므로 곡 대부분이 계속 느린 경우에는 더 빠른 절대 목표 FPS를 추론하지 않는다.
- 함수 포함 비율은 호출 계층끼리 겹치고 대기 함수도 포함한다. 대표 함수에 배정한 초과 시간은 **상관 기반 우선순위 추정**이며 실제 제거 가능한 CPU 시간·확정 병목·예상 FPS 이득이 아니다. 다른 후보 행을 다시 더하지 않는다.
- PDB와 바이너리 일치는 호출자가 확인해야 한다. 기호 없는 RVA들은 같은 함수여도 따로 묶일 수 있다.
- 실제 SuspendThread/GetThreadContext/native unwind, Mono 기호 해석, 완주 중 안정성, 실제 픽셀/정렬/충돌 동일성은 이번 오프라인 시험에서 검증하지 않았다.

작업 관찰: 초기 Python `TemporaryDirectory` 시험은 이 제한된 Windows 작업 계정에서 하위 디렉터리 접근이 WinError 5로 실패했다. 시험 자료를 허용된 `out/dropscan-selftest/<시험 이름>/`의 일반 디렉터리에 남기도록 바꾼 최종 시험은 통과했다. 최초 실패를 제품 동작 검증으로 세지 않았다. 전역 스킬/메모리 폴더는 수정하지 않았다.
