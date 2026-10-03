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

예: 그래픽 작업 분산 켬/끔 비교
```
sf_ab steps=["open D:/maps/x.adofai", "auto on", "play", "wait 90", "stop"] setting=LegacyGfxJobs a=true b=false repeats=2
```

## 주의

- 측정하는 동안 PC 를 쓰지 않는다(다른 창이 앞에 오면 화면 출력 방식이 바뀌어 결과가 달라진다).
- 게임이 켜져 있으면 `sf_run` 은 돌지 않는다. 시간 초과면 게임을 강제로 끈다.
- 판 로그는 `SF_RUNS_DIR` 에 남는다. 맵 경로가 들어 있으므로 저장소에 넣지 않는다.
