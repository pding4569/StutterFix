# sfmeasure (측정 MCP)

Claude 가 게임을 직접 켜서 자동 시험(AutoTest)을 돌리고, 결과를 요약으로 받는 MCP 서버. PC(윈도우) 전용, 파이썬 3.8 이상 표준 라이브러리만 쓴다.

## 준비

1. 파이썬 설치 (`winget install Python.Python.3.12`), `python --version` 이 되는지 확인.
2. 자동 시험이 든 DLL 을 설치: 개발자용 빌드, 또는 플레이어용이면 `dotnet build -p:AutoTestBuild=1`.
3. 저장소 루트의 `.mcp.json` 에 등록돼 있다. Claude Code 를 저장소에서 켜고 `/mcp` 에서 sfmeasure 를 승인한다.
4. (선택) PresentMon: 환경 변수 `SF_PRESENTMON` 에 PresentMon.exe 경로. 관리자 권한 또는 Performance Log Users 그룹이 필요하다.

경로가 다르면 환경 변수로: `SF_GAME_DIR`(게임 폴더), `SF_PLAYER_LOG`, `SF_RUNS_DIR`(판 로그 보관, 기본 `%LOCALAPPDATA%\StutterFix\runs`).

## 도구

| 도구 | 하는 일 |
|---|---|
| `sf_status` | 게임 켜짐, 설치된 DLL 날짜와 자동 시험 포함 여부, boot.config 의 그래픽 줄 |
| `sf_run` | `autotest.txt` 를 쓰고 Steam 으로 게임을 켜서 끝날 때까지 기다린 뒤 요약. `settings` 로 모드 설정을 이번 판만 바꾼다(끝나면 Settings.xml 되돌림). `presentmon` 으로 곡 중 기록도 같이 |
| `sf_ab` | 설정 하나를 A/B 로 ABBA 순서 되풀이(판마다 게임 새로 켬), FPS·끊김 비교표 |
| `sf_log` | Player.log 또는 저장한 판 로그의 최근 판 요약 (`/log` 스킬과 같은 내용) |
| `sf_presentmon` | 켜져 있는 게임을 N초 재서 FPS, 1% 최저, GPU 바쁨, 화면 지연, 출력 방식(Flip/합성), 찢어짐 허용 |

자동 시험 명령(`open`, `auto on`, `play`, `wait`, `stop`, `retry`, `mark` …)은 `AutoTest.cs` 머리 주석에 있다.

## 저장된 연구 시험: 짧은 실행·조회

`sf_run`의 선택 인자 `trace`로 이미 만든 시험 계획을 재사용한다. 새 도구 이름이나 MCP 허용 목록 변경은 필요 없다. 먼저 `sf_status`에 `trace API 1`이 있는지 확인한다. 표시가 없다면 연결 중인 이전 서버를 새로 연결해야 한다. 아래 예시는 이 API가 있는 서버에서만 쓴다.

```json
{"steps":[],"trace":{"op":"catalog","workspace":"E:/StutterFixWork/wt-gpu"}}
{"steps":[],"trace":{"op":"start","workspace":"E:/StutterFixWork/wt-gpu","plan":"tools/optimization/out/round63-hello-full-steps.json","run":"round63-hello-full"}}
{"steps":[],"trace":{"op":"status","workspace":"E:/StutterFixWork/wt-gpu","run":"round63-hello-full"}}
{"steps":[],"trace":{"op":"note","workspace":"E:/StutterFixWork/wt-gpu","run":"round63-hello-full","song_s":10,"note":"사용자가 원격 접속한 시각. 원시 로그는 유지."}}
```

- `start`는 곧바로 작업 이름과 worker PID를 반환한다. `status`는 상태·진짜 FPS·최악 프레임·오류·복원 수만 반환한다. 원시 로그와 맵 경로는 로컬 `tools/optimization/out/<run>/`에 보존한다. 진행 중에는 전체 Player.log를 다시 읽지 않는다.
- 계획은 작업 폴더 안의 JSON이며 `steps`, `settings`, `timeout_min`(최대 30분)을 받는다. 선택 `source_dll`/`native_dll`도 해당 작업 폴더 안으로 제한한다. 계획의 명령은 기존 AutoTest 명령이며 Python·셸 코드를 실행하는 경로를 추가하지 않았다.
- 실행 시 managed/native/번들 SHA를 저장한다. 명령·설정·검사 조건·이 세 SHA가 같은 시험은 `DUPLICATE`로 돌려준다. 새 근거로 반복해야 할 때만 새 작업 이름과 `repeat_reason`을 준다. 기존 출력 폴더를 덮어쓰지 않는다.
- 켜 둔 게임에는 설치하거나 종료 명령을 보내지 않는다. 실행한 게임에만 정상 종료를 요청한다. 실패·시간 초과에도 taskkill을 쓰지 않는다. 게임이 살아 있으면 DLL을 덮어쓰지 않고 `RESTORE_DEFERRED`로 남긴다.
- DLL/native/Settings/boot 2개/자동 시험 파일 3개/자원 probe 파일의 **원래 존재 여부와 바이트 9개**를 복원한다. 비정상 중단으로 잠금이 남으면 자동으로 지우지 않고 복원 검토를 요구한다. 실행 중에는 다른 서버 호출의 시험·종료를 차단한다.
- `PASSED`는 해당 계획의 검사와 정상 종료·복원이 통과했다는 뜻이다. 픽셀·전곡 품질·물리적 표시 FPS까지 통과한 뜻이 아니다. `checks.portal_min_s`, `checks.gpu_until_portal`로 전곡 GPU 유지 조건을 명시할 수 있다. GPU를 준비하지 않는 CPU 대조는 `waitsongpos`→`fxstate`의 명시적 곡 시각에 `checks.song_min_s`를 적용할 수 있다. `waitend`의 "곡이 끝나지 않음"도 실패로 집계한다. 물리적 표시 FPS는 이 경로에서 `UNMEASURED`다.
- 구간 측정이 있으면 `status.micro_windows`에 최근 최대8개의 곡 시작/끝·QPC초·프레임수·진짜FPS·생략 진단 상태를 반환한다. `real_fps`는 기존 곡 요약이며 구간FPS로 쓰지 않는다. 선택적인 `thread_cpu_boundary_ms`의 main/render는 병렬 CPU 누적의 경계 상한이고 합산하지 않는다. 전체 Player.log를 매번 읽지 않고 한 계획에 여러 조건을 넣어 조회한다.
- 현재는 **한 DLL로 실행하는 명령 묶음**을 지원한다. 실제 서로 다른 DLL의 reload 도우미와 관리자 PresentMon 다중 판 묶음은 기존 전용 runner를 유지한다. 일반 빌드·실험 DLL을 자동 재빌드하지 않으며 시험 중 소스/DLL을 바꾸지 않는다.

검사: `python -m unittest discover -s tools/sfmeasure -p test_trace_runs.py -v`.

예: 그래픽 작업 분산 켬/끔 비교
```
sf_ab steps=["open D:/maps/x.adofai", "auto on", "play", "wait 90", "stop"] setting=LegacyGfxJobs a=true b=false repeats=2
```

## 주의

- 측정하는 동안 PC 를 쓰지 않는다(다른 창이 앞에 오면 화면 출력 방식이 바뀌어 결과가 달라진다).
- 게임이 켜져 있으면 `sf_run` 은 돌지 않는다. 시간 초과면 게임을 강제로 끈다.
- 판 로그는 `SF_RUNS_DIR` 에 남는다. 맵 경로가 들어 있으므로 저장소에 넣지 않는다.
