# bake-9 결과: 채택 비용 계측·숨김 복구·실제 트리거 구동

2026-10-21, `E:\StutterFixWork\wt-bake`, `codex/night-bake`.
게임 실행·설치·커밋 없이 구현과 오프라인 검증을 수행했다. 기본 꺼짐·일반 Player 제외를 유지했다. 새 Harmony 패치와 런타임 `Module.ResolveSignature` 호출은 추가하지 않았다.

**성능 목표 달성 여부는 미판정이다.** 인계에 있는 OFF 3.16~3.21ms, ON 3.62~3.66ms, 시작 프레임 580~590ms는 이전 실게임 수치다. 이번 코드의 장식 CPU 합계, FPS, 숨김 복구에 따른 절감량, 시작 지연, 카나리아 <0.05ms는 측정하지 않았다. ≤2.5ms 또는 OFF보다 빠르다는 결론을 내릴 근거가 없다.

## 제안별 검토와 반영

|제안|코드/fixture에서 확인한 내용|이번 처리와 한계|
|---|---|---|
|최종 알파 숨김 누락|기존 `BakeWrite`에는 `InvisibleSkip.After` 호출이 없었다. `ColorFast.Try*`는 양의 알파만 직접 처리하고 나머지는 원래 setter로 폴백한다.|색·불투명도를 먼저 적용한 후 `After`를 호출한다. `rendererColor.a`는 원래 setter가 계산한 `color.a × opacity × floor`를 사용한다. 지원 O는 `stickToFloor`를 제외하므로 floor=1이다. 셰이더 허용 목록·마스크·히트박스·lazy 복귀 규칙은 기존 구현을 쓴다. 실제 불일치가 0이 되는지는 실게임 확인 필요.|
|투명 Transform 호출 미루기|기존 회전/크기는 `MoveApply.DeferTransform`만 사용했다. 숨김 전용 경로는 별도로 필요하다.|`InvisibleSkip.LazyCan`이 허용하면 위치·피벗·시차 오프셋·회전·크기를 C# 필드에만 쓰고 `LazyStore`로 등록한다. 알파가 양수가 되면 기존 `After`→`ApplyLazy`가 미룬 값을 반영한다. 마스크 등 거부 조건에서는 원래 setter를 유지한다.|
|plateau 순회 제거|기존 `ActiveEvaluator`는 이미 끝난 채널을 제거한다. 따라서 끝난 채널의 반복 순회가 11,452개의 원인이라는 주장은 코드와 맞지 않는다.|새 `BakeLive`도 종료값을 한 번 전달한 뒤 활성 목록에서 제거한다. 시작값=목표값인 상수 구간도 즉시 제거한다. 기존부터 활성인 정지 구간은 첫 구동에서 제거한다. 실제 활성 수의 원인은 새 채널별·장식별·소스별·긴 구간·교체 카운터로 측정해야 한다. 600~800개라는 가정은 채택하지 않았다.|
|트리거 앵커 시계|실제 시작 프레임을 쓰면 예약 시각 지터를 피할 수 있다. 그러나 전역 double 누적값의 차는 원본 `float Pos += td`와 같지 않다. 기존 fixture도 이를 입증한다.|실제 `FastMove.Prefix` 관찰을 이용해 소스별 **float 진행값**을 공유한다. 같은 소스의 여러 채널은 프레임당 한 번만 같은 dt를 더한다. 관찰된 새 소스는 곡 시각으로 발사하지 않는다. 무조건 시간 지터 0이라는 주장은 하지 않는다.|
|같은 판 카나리아|같은 프레임 비교에는 OFF/ON 별도 파일의 시간 이웃 정렬이 필요 없다.|고정 인덱스 해시 순서로 최대 96개를 뽑아 소유/쓰기/Kill 대상에서 뺀다. 원본 Rec를 유지하면서 같은 `BakeLive` 계산만 실행한다. 관리자 LateUpdate 종료 후 논리 위치·피벗·시차 오프셋·크기·알파·실제 forceRenderingOff를 비교한다. 화면 픽셀/Transform 모서리 대조는 기존 서명 검사가 담당한다.|
|켤 때 긴 프레임|켜기에는 바인딩·구동기 구축·기존 트윈 완료·전체 초기 평가/쓰기가 있다.|재생 중 `adopt on`은 거부한다. 게임 정지/일시정지 상태에서만 준비한다. 프레임 분할은 구현하지 않았다. 준비 전체 및 단계별 ms를 출력한다. 긴 준비 자체를 없앴다는 뜻은 아니다.|

## 실행 경로와 정확성 범위

`DecoBake.Live.cs`의 `BakeLive`는 Unity 참조 없이 실제 구동과 계약 fixture에서 같은 코드를 사용한다. 원래 무상태 `Table.Eval`, `ActiveEvaluator`, `AdoptState`, `verify`, 서명 파일은 유지했다. 런타임 채택 구동은 `AdoptState` 대신 `BakeLive`를 사용한다.

- 켜기 때 기존 예약 목록을 검증한 뒤 현재 Source ID를 표의 Source ID에 연결한다. seek로 인스턴스 ID가 달라져도 내용 검증을 통과한 순서대로 연결한다. 미확인 시작/중복 시작/비유한 값/세대 변경/원본 우회는 기존 실패 복귀 경로로 소유권을 반환한다.
- `FastMove.Prefix`가 실제 시작을 관찰하면 새 활성 슬롯을 등록한다. Update 전에 시작한 소스는 그 Update부터, Update 뒤에 시작한 소스는 다음 Update부터 진행한다. 0초 효과는 트리거 때 즉시 끝값을 전달한다. 같은 프레임의 여러 소스는 실제 관찰 순서대로 처리한다.
- 교체는 상대 위치의 기준값을 **완료 전**에 읽고, 이전 채널의 `End`를 적용한 뒤 새 보간을 시작한다. 상대 목표는 실제 마지막 계산값으로 다시 만든다. 크기와 시차 배율은 OnComplete 목표 대입이 없으므로 끝에서도 `start + (target-start) × ease` 산술을 보존한다. 새 live 경로에서 이를 검사했다. 기존 tb 표의 시차 배율 끝 산술까지 이번에 변경한 것은 아니다.
- dt는 `DecoAnim.UpdatePrefix`와 같은 `(useSmoothDeltaTime ? smoothDeltaTime : deltaTime) × DOTween.timeScale`이다. 원본과 같은 1E-06f 미만의 작은 dt 검사도 사용한다. `controller.paused`만 보고 dt=0이라고 가정하지 않는다. 실제 일시정지가 dt·스케줄·원본 Rec에 미치는 영향은 게임 확인이 필요하다.
- 켤 때 이미 진행 중인 효과, seek/되감기, 일시정지/재개로 SongClock 재동기가 발생한 구간은 `tb`로 전체 상태를 복구한다. 그 뒤 새로 관찰한 소스부터 float 앵커 경로를 사용한다. **기존 진행 효과의 정확한 과거 dt 이력을 복원하지 않는다.** 따라서 카나리아 불일치에는 tb 폴백 자체의 오차가 남을 수 있다. `anchored`/`tb`와 카나리아의 `관찰시작채널`/`tb`를 함께 봐야 한다.
- 채택을 끌 때 현재 활성 채널을 완료하고 소유권을 푼다. setter가 실패한 종료 채널도 복구하도록 소유 채널의 마지막 상태를 강제로 다시 전달하며, 한 채널의 복구 예외가 다른 채널의 처리를 막지 않는다. 카나리아는 복구 때도 쓰지 않는다. 카나리아 수는 큰 맵에서 96개이며 작은 맵에서는 지원 장식 최소 1개를 소유 대상으로 남긴다. 표본이 64개 미만인 작은 맵은 대표성 검증에 사용하지 않는다.
- 새 비교는 위치/크기 절대 오차 0.0001, 알파 절대 오차 0.00001 초과 또는 비유한 값을 불일치로 센다. 숨김은 허용 셰이더에서 예측 최종 알파≤0과 실제 `forceRenderingOff`를 비교한다. 색 RGB·회전·화면 픽셀 전체를 대조하는 검사가 아니며 기존 서명을 대체해 삭제하지 않았다.

## 새 비용 분해 로그

`decobake adopt state`에 다음 로그가 함께 나온다. `fuse begin`의 기존 `BeginAdoptMeasure` 연결로 구간 통계를 초기화한다.

|로그/항목|측정 범위와 해석|
|---|---|
|`[장식 이동/굽기채택]` 평균/p99/최악/resync최악|Update 훅의 채택 구동 전체. p99는 최근 최대 65,536프레임. 기존과 같은 위치의 시간이다.|
|`[굽기채택 분해] 평가ms`|활성 순회·이징·마지막 값 비교·StageWrite. tb 전체 재동기도 포함한다.|
|`쓰기목록ms`|Update에서 FlushWrites 전체를 감싼 **포함 시간**. 아래 setter 단계와 중복이므로 합산하지 않는다.|
|`검사ms`|세대·경로 검사 및 120프레임마다 외부 패치 확인.|
|`트리거누적ms`|실제 효과 시작 시 활성 전환·Kill 완료값 처리·즉시 FlushWrites. FastMove 효과 시작 시간 안에 이미 들어간다. Update 채택 시간과 구분한다.|
|`목록준비정리추정ms`|쓰기 대상의 null/색 마스크 준비/Precheck 준비를 64호출당 1회 표본 추정하고, 마스크 목록 정리는 매 Flush마다 측정한다. 트리거 쓰기도 포함한다. foreach 루프 자체만의 정확한 독점 시간은 아니다.|
|`[굽기채택 쓰기] 위치/회전/크기+시차배율/색/숨김ms`|BakeWrite 내부 64호출당 1회 ×64 표본 추정. 트리거와 Update 쓰기를 모두 포함한다. 쓰기목록ms와 더하지 않는다. timer/표본 편향으로 작은 값은 정밀 분해 근거가 약하다.|
|채널/프레임·채널별합|0~15는 위치XY, 피벗XY, 회전, 크기XY, RGBA, 불투명도, 시차배율XY, 시차오프셋XY. 같은 장식의 여러 채널을 구분한다.|
|평가장식/프레임·anchored소스/프레임·교체채널·길이10초이상/프레임|채널 수가 많은 이유를 구분하는 자료. 소스 수는 anchored에 한정한다. 카나리아 계산도 평가 수에 포함된다.|
|`[굽기채택 준비]`|전체, 검사, 바인딩, 구축, Kill완료, 초기평가쓰기. 준비 중에는 정지/일시정지여야 한다.|
|`[굽기채택 카나리아]`|표본/비교/오차 최대/숨김 불일치/관찰 채널/시간 평균·최악. 평균 <0.05ms는 아직 미검증이다.|

카나리아 호출은 `TightLate.ManagerEnd`의 기존 서명 호출 옆이다. **TightLate 자체 managerTicks 종료 뒤이므로 그 내부 타이머에는 들어가지 않는다.** FuseLate가 외부 훅까지 포함하는지에 따라 카나리아 시간의 포함 여부를 확인하고, 전체 합산에서는 한 번만 센다. 기존 효과 시작에는 트리거 비용이 포함된다. 단순히 `Update + 채택 + 트리거 + 쓰기분해 + Late`를 모두 더하면 중복 계산이다.

## 오프라인 검증

증거: `tools/optimization/validation-bake9/`.

|검사|결과|
|---|---|
|Release 개발 빌드|오류 0, 경고 38|
|Release `-p:Edition=Player`|오류 0, 최종 증분 빌드 경고 4(앞선 컴파일 경고 36)|
|Release `-p:Edition=Player -p:AutoTestBuild=1`|오류 0, 경고 38|
|LoadCheck 개발/AUTOTEST|FieldRef 267곳, 불일치 0. 기존 Setup/PrepVfx 계약 각각 정확히 1개|
|LoadCheck 일반 Player|FieldRef 229곳, 불일치 0. DecoBake/Core/AdoptState/**BakeLive**와 BakeWrite 제외 확인|
|계약 fixture|121개 통과. 실제 live 구동기의 float 누적·곡 시각 독립·교체 전 상대 기준·같은 프레임 순서·0초 즉시 완료·한 소스 다채널 1회 누적·종료/상수 제거·카나리아 쓰기 0·tb 폴백·크기/시차 끝 산술·비유한 dt 거부 포함|
|DecoSim fixture|12개 통과. managed stub 검사이며 Unity/Mono 성능 증거가 아님|
|서명 fixture|10개 통과. 기존 파일 형식/허용 오차/불완전 증거 거부 검사 유지|

검사는 로컬 참조 DLL을 읽는 빌드/LoadCheck와 순수 계산 fixture이다. Unity 게임을 실행하지 않았다. 빌드 경고에는 기존 어셈블리 버전 충돌·구식 API·미사용 필드가 포함된다. LoadCheck의 실행 중 결정되는 기존 FieldRef 5곳은 정적 검사의 한계다. 실제 setter/렌더 화면/프레임 순서/원본 동시 실행의 동등성 및 성능은 남아 있다.

재현 명령(설치/게임 실행 명령 없음):

```powershell
dotnet build -c Release --no-restore
dotnet build -c Release -p:Edition=Player --no-restore
dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1 --no-restore
dotnet build tools/LoadCheck/LoadCheck.csproj -c Release --no-restore
dotnet tools/LoadCheck/bin/Release/net8.0/LoadCheck.dll bin/Release/StutterFix.dll --bake
dotnet tools/LoadCheck/bin/Release/net8.0/LoadCheck.dll bin/Player/StutterFix.dll --no-bake
dotnet tools/LoadCheck/bin/Release/net8.0/LoadCheck.dll bin/PlayerAuto/StutterFix.dll --bake
dotnet build tools/decobake-contract-check/DecoBakeContractCheck.csproj -c Release --no-restore
dotnet tools/decobake-contract-check/bin/Release/net8.0/DecoBakeContractCheck.dll
dotnet build tools/decosim-fixture/DecoSimFixture.csproj -c Release --no-restore
dotnet tools/decosim-fixture/bin/Release/net8.0/DecoSimFixture.dll
py -3 tools/optimization/adoptsig-fixture.py
```

## 내가 돌릴 순서

이 절은 다음 담당자가 게임 실행/설치를 명시적으로 허가받은 뒤 수행할 순서이며, 이번에는 실행하지 않았다. 대상은 이전에 측정한 Arche 한 맵이다. 100만 타일 맵은 열지 않는다.

1. 별도 워밍업 한 판을 버린다. 매 판 새 프로세스, 같은 설정·해상도·워밍업·seek 경로를 사용한다. FrameGen 장식 힌트 동시 사용은 계속 거부된다. 개발용 부가 검증과 서명은 성능 판에서 끈다.
2. 기존 bake-8 준비처럼 `decobake on`→맵 open→초기 ready→stop→같은 seek/재준비→새 ready를 확인한다. **실제 곡 시각이 257초를 넘고 catch-up이 끝난 뒤** 게임을 일시정지한다. 준비가 늦어 258초를 넘겼으면 그 판은 버리고 더 이른 lead-in으로 다시 준비한다.
3. ON 판은 일시정지 중 `decobake adopt on`, `decobake adopt state`를 실행하고 `on=True`, 기대 O(지원 수에서 카나리아를 뺀 값), 표본 96, 준비 단계별 시간을 기록한다. OFF 판도 같은 위치에서 같은 일시정지/재개를 거친 뒤 `adopt off`를 실행한다. 여기서 말하는 일시정지는 **게임 UI의 일시정지**다. 기존 AutoTest에는 `pause`/`resume` 명령이 없으므로 그 이름을 steps로 보내지 않는다. `stop`은 편집 복귀라 대체할 수 없다. 실행자가 UI 재개와 AutoTest 대기열을 조율해야 한다.
4. 재개 뒤 아래 공통 구간을 실행한다. `waitsongpos`는 임계 도달 검사이므로 준비/명령 지연으로 이미 258초를 넘긴 판을 258~278초 판으로 간주하지 않는다. 실제 Fuse 시작/끝 곡 시각과 구간 프레임 수를 함께 확인한다.

```text
waitsongpos 258 120
fuse begin
waitsongpos 278 120
fuse end
decobake adopt state
stop
decobake off
```

5. **서명 없는 OFF/ON/ON/OFF(ABBA)**를 수행한다. 각 판에서 `FuseLate`, `[장식 이동]`의 원본 모드 갱신, 효과 시작, `[장식 이동/굽기채택]`, 새 분해/카나리아 로그를 저장한다. ON 내내 소유권 유지·`skippedRec` 증가·예외/자동 복귀 없음·표본 비교 횟수>0을 확인한다. anchored/tb 커버리지와 숨김 불일치도 반드시 기록한다. 카나리아 비용은 ON의 실제 비용에 포함한 결과로 판단한다.
6. 주 대조는 같은 판 카나리아다. 불일치가 있으면 새로 관찰한 소스와 tb 폴백을 구분하여 같은 프레임의 교체/일시정지/0초 경계를 조사한다. 초반 새 소스, 중간 교체, 일시정지/재개, 되감기·재준비 후의 별도 검증도 필요하다. 카나리아 0건을 전체 장식/화면의 동등성 증명으로 쓰지 않는다.
7. 기존 `verify`/서명 비교는 별도 정확성 판에서 유지한다. bake-8의 `adoptsig 이름 초 표시Hz`와 비교 도구를 사용하되 캡처 부하로 시간 정렬이 실패하면 그 결과를 통과로 처리하지 않는다. 서명을 켠 판의 FPS는 성능 ABBA에서 제외한다.

최종 표는 `판/O/카나리아/실제구간/FPS/원본갱신ms/효과시작ms/채택구동ms/FuseLate ms/카나리아ms/중복없는합계ms/anchored·tb/숨김불일치/기타오차/준비ms`로 만든다. 목표는 ON의 중복 없는 장식 CPU 합계가 OFF보다 낮고 ≤2.5ms인지다. 충족하지 못하면 평가·쓰기·숨김·목록·Late 중 실제 큰 단계를 숫자로 결론낸다. 이번 결과에는 그 실측 표를 채울 숫자가 없다.

## 작업 관찰 전달

task-observer는 제한된 쓰기 권한을 가진 worktree에 영구 관찰 저장소를 새로 만들지 않고 이 문서로 전달한다. 공유 시계에서 double 차분과 원본 float 누적을 동등하다고 가정하지 않는 점은 기존 계약 fixture가 이미 포착하고 있어 중복 스킬/메모리 항목은 만들지 않았다. 스킬·메모리·게임 파일을 수정하지 않았다.
