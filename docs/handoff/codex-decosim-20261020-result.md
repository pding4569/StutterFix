# decosim S1 결과 — 2026-10-20

구현과 로컬 검증 완료. **실제 Arche 테이프의 비트 동일성 및 프레임당 ≤1ms 관문은 미판정**이다. 요청에 따라 게임 실행·설치·커밋은 하지 않았다. 편집·빌드는 `E:\StutterFixWork\wt-decosim`, 브랜치 `codex/night-decosim`에서만 수행했다.

## 변경 사항

- `DecoSim.cs`: 메모리 테이프 캡처, `DecoBatchEval` SoA 재생, 비트 검증, 단일/병렬 벤치마크. 파일 전체가 `#if DEV || AUTOTEST` 안에 있다.
- `StutterFix.csproj`: 기존 진단 파일과 같은 조건으로 Compile 등록.
- `AutoTest.cs`: 기존 DEV/AUTOTEST 명령 구간에 `decosim` 연결.
- `DecoAnim.cs`: 조건부 캡처 훅만 추가. 시작값이 채워진 생성 지점, Startup, Step/StepPair 뒤, 명시적 Complete/Cancel, KillAll, 프레임 시작/끝. 각 훅은 `DecoSim.Capturing`을 먼저 확인한다. 새 Harmony 패치는 없다.
- `tools/decosim-fixture`: 실제 `DecoSim.cs`를 링크하는 .NET 8 합성 시험. 게임 참조/실행 없이 캡처·재생의 제어 흐름을 검사한다. 시험 코드도 AUTOTEST 조건부다.

## 명령

```text
decosim capture 12
decosim verify
decosim bench
decosim selftest
```

`capture`는 곡 중에만 시작하고 명령은 즉시 반환한다. 기간은 곡 시각이 아닌 Stopwatch 경과 초다(0초 초과~120초). `[장식시뮬] 캡처 완료 ... valid=True`를 확인한 뒤 verify/bench를 실행한다. 테이프는 같은 프로세스 메모리에만 있고 새 캡처가 대체한다. 게임 재시작/모드 다시 불러오기를 사이에 두지 않는다.

장식 인덱스는 시작 시 `allDecorations` 위치다. 현재 Running Rec를 초기 스냅샷으로 보관하고, 생성 시에는 동일 Rec 객체가 재사용돼도 새 세대 번호를 준다. 이벤트는 발생 순서와 프레임의 이벤트 구간으로 시점을 보존한다. 목록에서 빠진 기록도 다음 프레임 시작에 Remove 이벤트로 기록한다. 오래된 세대의 Remove가 새 슬롯을 취소하지 않는다.

표본은 장식 인덱스 `%4==0`인 기록의 실제 Step/StepPair 직후에 수집한다. 완료로 목록에서 빠질 기록도 Compact 전에 수집된다. 명시적 Complete/Kill(true)의 표본은 해당 이벤트와 함께 저장·비교하므로 같은 슬롯이 곧바로 재시작되어도 사라지지 않는다.

비교 대상은 Key별 의미가 있는 Rec 출력이다. float·색 RGBA·시차 배율 XY는 해당 성분, 크기 Key 7/8은 각각 움직이는 X/Y축을 비교한다. Pos 비트와 Running/세대도 비교한다. float는 문자열 근사가 아닌 32비트로 비교하며, 첫 8개 차이는 프레임·장식·Key·기대/실제 값과 16진 비트를 출력한다. 단일·병렬 각각 Key별 비교/불일치 수와 합계를 낸다. 표본 0은 판정불가다.

## 원본 의미와 재생

- 위치·회전·색·불투명도 등의 시작값은 이전 기록을 Kill(true)한 뒤 읽는 **실제 그 순간 장식 값**이다. 위치의 목표값은 `startAxis + target`이다. 이벤트의 목표값을 시작값으로 취급하지 않는다.
- 크기 X/Y 시작값은 NewRec가 아니라 첫 Startup에서 현재 `scaleVec`를 읽는다. 그 순간 Start/Change를 별도 이벤트로 기록한다. 시차 배율은 생성 시 담은 시작값이다.
- `td=(DOTween.useSmoothDeltaTime ? Time.smoothDeltaTime : Time.deltaTime)*DOTween.timeScale*1f`를 실제 UpdatePrefix가 사용한 값 그대로 프레임에 저장한다. 재생도 `Pos+td`를 float로 저장하고 Duration으로 제한한다. `-1e-6 < td < 1e-6`이면 평가하지 않는다.
- 이징은 원본 `ZeroTween.Eval`을 호출한다. `start + change * ease`, 색·시차의 `v += change * ease` 식을 분해하거나 곱셈 결과를 별도 float에 저장하지 않는다. 실제 Mono에서의 최종 동일성은 게임 캡처로 확인해야 한다.
- 자연 완료와 명시적 완료를 구분한다. Rec Last에는 평가 결과를 남기고, 위치·회전·색·불투명도 출력 배열에는 OnComplete처럼 정확한 목표값을 쓴다. 크기는 원본처럼 평가 결과를 쓴다.
- 출력은 장식 인덱스별 연속 `PosX/PosY/Rot/ScaleX/ScaleY/Red/Green/Blue/Alpha/Opacity` 배열이다. 캡처 시작 시 장식 필드로 초기화하고 이후 기록된 애니메이션으로 갱신한다. 평가 루프는 Unity 객체/Transform/SpriteRenderer를 읽거나 쓰지 않는다.

## 벤치마크 수치 읽기

곡을 멈춘 뒤에만 `bench`가 허용된다. 각 방식마다 전체 테이프로 이징/JIT/스레드풀을 예열하고, 테이프 전체를 정수 횟수 반복해 최소 300프레임을 측정한다. 회차 사이 초기화와 배열 할당은 프레임 시간에서 제외한다.

- `eval mean/p99`: 전체 SoA 순회·평가·출력 쓰기. 병렬에서는 `Parallel.For` 동기 호출 전체, 즉 분할·예약·실행·join을 포함한 메인 스레드 경과 시간이다. 순수 대기 시간과 메인 스레드에서 직접 실행된 작업을 별도로 분리했다고 주장하지 않는다.
- `events mean/p99`: Create/Startup/Complete/Cancel/Remove 처리 전체.
- `starts(subset) mean/p99`: Create+Startup 처리만의 부분합. **events에 이미 포함되므로 다시 더하지 않는다.** 시작별 Stopwatch 비용도 events/total에 포함되므로 보수적인 수치다.
- `total mean/p99`: events+eval을 같은 프레임에서 직접 잰 합계. ≤1ms 관문은 이 값을 보고, p99도 함께 기록한다.
- `worker범위경과합`: 병렬 구간들의 벽시계 시간 합. CPU 사용 시간이나 메인 스레드 시간과 같지 않고 total에 더하지 않는다.
- `N=min(논리 CPU 수,8,슬롯 수)` 구간을 사용한다. `slots`는 테이프 전체에서 등장한 장식/Key 슬롯 합집합이고, `evaluations/frame`은 실제 캡처의 프레임당 Step 수다. `starts/frame`은 새 생성 수다. checksum은 출력 소비 및 두 방식 결과 확인용이며 정확성 판정은 verify 결과를 사용한다.

## 제한과 실패 처리

보수적 메모리 예약 상한 **512MiB**, 장식 100000개, 슬롯 200000개, 프레임 12000개 상한이다. 목록 용량 증가·사전·SoA/출력 공간 여유를 포함한 예약량이며 프로세스 전체 메모리 상한은 아니다. 상한에 닿으면 로그를 남기고 중단하며 부분 테이프를 유효 판정하지 않는다. 필요하면 더 짧게 캡처한다. 캡처는 할당/관찰 부하가 있으므로 캡처 중 FPS를 새 파이프라인 성능으로 해석하지 않는다.

`decoengine`, `decobatch`, `groupstep`, `NativeMotion`, AUTOTEST ablation이 활성화되면 시작을 거부하고, 도중 변경도 중단한다. 소유 이동과 엔진 경로를 기본 경로의 정확성으로 섞지 않는다. 갱신 중 이미 Step한 뒤 재진입 Create/Complete/Cancel이 생기면 이벤트를 임의로 재배열하지 않고 테이프를 무효화한다. DecoAnim 예외 발생도 무효화한다. 정상적인 Startup은 각 기록의 첫 Step 앞에 모아 재생하되, Start/Change는 그 시점의 실제 읽기 값을 보존한다.

기본 ColorFast는 Rec Last 계산 뒤의 필드/렌더러 적용 경로이고, MoveApply.SetLocal은 Transform 위치 비교/쓰기와 epsilon 생략을 수행한다. 이번 verify는 이 적용 계층을 비교하지 않는다. **장식 필드 전체·즉시 이동 이벤트·LateUpdate·피벗 변환·부모/시차·그룹 메시 정점·최종 화면 동일성은 검증 대상 밖**이다. 특히 출력 배열은 Rec 외부의 즉시 setter 변경을 별도 캡처하지 않으므로 다음 파이프라인에 연결하기 전 그 입력도 보완해야 한다. Key 3/4/11/12/13도 Rec 출력 검증에는 포함하지만 요청한 기본 출력 배열 외 피벗/시차 출력 연결은 하지 않았다.

## 로컬 검증

```powershell
dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1
dotnet build -c Release -p:Edition=Player
dotnet build -c Release
dotnet run --project tools/decosim-fixture/DecoSimFixture.csproj -c Release
```

- 지정 PlayerAuto 빌드: **오류 0, 경고 37**. 일반 Player: **오류 0, 경고 36**. 개발 빌드: **오류 0, 경고 37**. 참조 버전 충돌(MSB3277), 기존 미사용 필드 등 경고가 있다. DecoSim.cs 경고는 없다.
- DLL 타입명 검사: PlayerAuto에는 `DecoSim/DecoBatchEval`이 있고 일반 Player에는 없다.
- 합성 시험: **12개 검사 통과**, 내장 selftest의 단일/병렬 총 **10개 검사** 포함. 7프레임·12슬롯, 사용되는 모든 Key(1/2/3/4/5/7/8/9/10/11/12/13)를 포함한 테이프를 단일/병렬 각각 **47회 비교, 불일치 0회**. 표본 1개 변조 시 각각 **불일치 1회** 검출. 두 방식 각각 **301프레임** 벤치 경로 실행과 동일 출력 checksum 확인. 시작 시 이미 진행 중인 기록, 0 dt, 크기 지연 시작, 동일 Rec 재사용, 강제 완료, 취소/제거, 끝값 출력, 상한/연구 경로/재진입 중단을 검사했다.
- 이 합성 시험의 시간은 작은 .NET 8 stub 부하이므로 실제 장식 성능 수치로 채택하지 않는다. 실제 Unity Mono 이징 전체, 약 12000개 살아 있는 기록과 약 95개 새 시작/프레임 부하는 미측정이다.
- `git diff --check` 통과. DecoAnim 원래 코드의 삭제/변경 없이 조건부 추가만 수행했다.

빌드/시험 원문은 [decosim-20261020-evidence](decosim-20261020-evidence/)에 보관했다.

## 사용자가 게임에서 실행할 순서

사용자가 PlayerAuto DLL을 설치하고 게임을 실행한다. 연구 플래그는 끈 상태여야 한다. `Arche 맵 절대경로`를 실제 경로로 바꾼다.

```text
open <Arche 맵 절대경로>
seek 262 8
waitsongpos 264
decosim capture 12
waitsongpos 277
stop
decosim verify
decosim bench
```

`stop` 전후 `[장식시뮬] 캡처 완료 ... valid=True` 또는 정상적인 `곡 재생 종료 ... valid=True`를 확인한다. 캡처 도중 stop하면 요청한 12초보다 짧은 테이프가 될 수 있다. `verify`의 single/parallel Key별 수치, `bench`의 evaluations/frame·starts/frame·total 평균/p99를 함께 전달하면 된다. 불일치가 나오면 첫 상세 로그도 포함한다. 아직 게임에서 실행하지 않았으므로 실제 정확성·≤1ms 통과 또는 다음 단계 진행을 결론 내리지 않았다.
