# 임무 2: PerfView 로 남은 끊김의 원인을 찾고, 다른 원인·최적화도 본다 (Codex 자율)

앞 임무(`mission-big-perf.md`, 결과 `big-perf-findings.md`)에서 곡 중 30ms 넘은 프레임 7개의 원인이 미확인으로 남았다:
HELLO 2026 106.77초(55ms)·106.78초(41ms), Arche 271.33초(52ms), Windflower 263.43·264.50·264.55초(34·46·46ms, 첫 판에서도 264.6초 42ms 로 같은 구간 재현), Timeline 3.74초(37ms).
이 끊김은 효과 처리 시간이 0.01~3ms 라 모드/게임 효과 코드 원인이 아니고, CPU/GPU 표본이 한쪽으로 몰려 드라이버·VRAM·OS 쪽이 의심된다.

## 방법

- 관리자 에이전트가 이미 떠 있다(`C:\Users\Public\StutterFixTrace\agent.ps1`). 자동 시험 `trace <이름> <초>` 명령이 그 에이전트에 PerfView ThreadTime 기록을 시작시킨다. AGENTS.md "엔진 안쪽 보기(PerfView)" 절을 먼저 읽는다.
  에이전트가 받지 않으면(상태 파일 `agent-status.txt` 가 갱신 안 됨) 사용자에게 알리지 말고 `cmd.txt` 규칙(`start <이름> <초>`)으로 직접 시험해 본다. 그래도 안 되면 그 사실을 결과에 적고 PresentMon(`pm`)으로 가능한 데까지만 한다.
- 기록 길이가 곡보다 짧을 수 있으니, 끊김 구간 약 20초 앞에서 기록을 시작하게 `seek <곡 초> [앞 여유]` 로 그 시점 근처부터 재생한다(AGENTS.md 자동 시험 설명).
- 해석은 `tools/etwstall`(`EtwStall <etlx> <PID> <HH:mm:ss.fff> [앞ms] [뒤ms]`). 끊긴 시각은 측정용 빌드의 끊김 줄 "시각" 또는 `[임무30]` 위치로 맞춘다.
  PerfView `SaveCPUStacks` 는 40분 넘게 걸린 적이 있으니 쓰지 말고 etwstall 만 쓴다(로컬 심볼, 5초).

## 해야 할 일

1. Windflower(263.4~264.6초)부터 PerfView 로 잡아 etwstall 로 메인 스레드의 대기 원인을 분류한다: 어떤 스레드가 무엇을 기다렸는지, 깨운 쪽(VidMm/dxgkrnl, 드라이버, 모드 스레드, 다른 프로세스), CPU/GPU, 그 동안 게임 전체 CPU.
   같은 방법으로 Arche 271초, HELLO 106.8초도 한다(시간이 오래 걸리면 Windflower 와 HELLO 만).
2. **원인이 VRAM 이동**(VidMm PageIn/Evict, `PageInOneAllocation`)이면: 그 맵에 이미지 한도를 한 단계 낮추는 것(`VramGuard`, `ImagePrefetch` 의 3072→2048)이 이미 쓰이는 방식이다. 이 맵 이미지 수·VRAM 사용량을 보고 한도를 `VramGuard` 기록에 반영하는 규칙을 개선할 수 있으면 구현하고 같은 구간을 A/B(4판 이상)로 확인한다.
3. **원인이 모드 스레드/잠금**이면 스택으로 찾아 고친다(원래 화면 동일 대조 포함).
4. **원인이 드라이버/OS 자체**이면(모드로 못 고침) 증거(스택, 대기 이유)를 `docs/handoff/perfview-findings.md` 에 적고 닫는다.
5. **다른 원인·최적화도 본다**: 기록한 etl 안에서 끊김 구간 밖에도 눈에 띄는 것이 있으면 적는다.
   - 프레임당 CPU 상위 스택(메인 스레드, 렌더 스레드)에서 모드 코드(`StutterFix`)가 차지하는 몫과 상위 게임/엔진 함수.
   - 곡 중 GC·할당, 스레드 경합, 큰 잠금 대기.
   - 맵 열기 중(큰 단위 대기) 메인 스레드가 놀고 있는 구간.
   이 중 코드로 잡히고 원래 화면을 지킬 수 있는 것은 구현·A/B 까지 한다(이득 작으면 넣지 않고 이유만).

## 규칙

앞 임무와 같다: 프레임 늘리기·쉐이더 코드 금지, 커밋·푸시·릴리스 금지(작업 폴더에 둔다), `Main.Unload` 되돌리기, 설정 기본값 보수적으로,
끝날 때 게임 꺼짐 확인 + Mods 에 **정식 `bin/Player/StutterFix.dll`** 복원 + Settings.xml 원래 값, 마지막 메시지에 상위 3줄 요약.
`.codex/config.toml` 의 `writable_roots` 가 이미 있다(Mods, StutterFix 로그, Temp, StutterFixTrace, SFBundle). PerfView 결과 파일은 `C:\Users\Public\StutterFixTrace` 또는 `%TEMP%\PerfView` 에 있다.
