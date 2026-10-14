# EtwStall

게임 종료 뒤 ETL/ETLX의 CPU 표본, 문맥 전환 대기, ReadyThread의 깨운 쪽을 오프라인으로 본다.

```powershell
dotnet build tools/etwstall -c Release
$env:ETW_LOCAL = '1'
$env:ETW_ALL_THREADS = '1'
tools/etwstall/bin/Release/net8.0/EtwStall.exe <etl 또는 etlx> <PID> <HH:mm:ss.fff> <앞ms> <뒤ms>
```

- `ETW_PROCS=1`: PID 목록만 출력한다. 기존 세 인수는 필요하며 PID에는 0을 넣어도 된다. 분석 전 이 값을 제거한다.
- `ETW_LOCAL=1`: `C:\Users\Public\StutterFixTrace\symbols`의 로컬 심볼 캐시만 쓴다. 없으면 주소로 나온다. 기본 동작은 기존 Microsoft 심볼 서버도 쓴다.
- `ETW_ALL_THREADS=1`: CPU 표본이 없는 대기 스레드까지 출력한다. 기본은 CPU 표본 상위 6개 스레드다.
- ETL의 최초 ETLX 변환은 길고 메모리를 쓰므로 게임 측정과 동시에 돌리지 않는다. 이후에는 ETLX를 직접 입력한다.

‘잠든 곳’은 **CSwitch의 `BlockingStack()`**으로 조회한다. 일반 `CallStack()`은 차단된 스레드의 스택을 보장하지 않는다. 깨운 스레드 소속은 `GetThread(tid, time)`으로 해당 시각에 맞춘다. 구간 시작 이전부터 이어진 대기는 최대 앞 200ms에서 상태를 복원한다. 그보다 일찍 시작한 대기는 누락될 수 있다.

게임 CPU 표본 수는 모든 게임 스레드의 합이다. 1ms 샘플링이어도 대기 시간이나 CPU 사용률과 동일하지 않으며, 여러 코어의 합이 구간 길이보다 클 수 있다. `stacks including sfnative`는 드라이버·엔진을 포함한 **포함 스택 표본 수**이며 모드 자체 비용이 아니다. `unresolved Mono/JIT candidates`도 후보 수이며 StutterFix와 게임/다른 모드를 분리하지 못한다.

모니터 로그의 ‘시각’은 알림 출력 시각이다. 프레임 시작/끝의 정확한 타임스탬프처럼 쓰지 말고 앞뒤 구간과 스레드 연결을 함께 본다. Unity 심볼은 현재 DLL의 RSDS GUID+Age와 맞아야 한다.
