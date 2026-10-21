# bake-8 결과: 개발 전용 굽기 채택 1단계

2026-10-21, `E:\StutterFixWork\wt-bake`, `codex/night-bake`. 게임 실행·설치·커밋 없이 구현과 오프라인 검증을 수행했다. 기본 꺼짐이며 일반 Player에는 포함하지 않는다. 실게임 서명 대조와 성능 수치는 아직 없다. 채택 후보 확정·기본 켬·Player 이식의 근거로 사용하면 안 된다.

## 구현

- `decobake adopt on|off|state`: ready·동일 장식 객체/순서/ID·지원 O>0·기존 실행 경로를 확인한다. 매번 현재 예약 효과의 시각·길이·이징·값·대상·제외 사유를 최초 표와 대조한 뒤 소스 ID를 연결한다. 게임이 이미 시작한 효과를 예약 목록에서 제거하는 점을 반영해 과거 항목의 소모를 허용하지만, 남아 있는 항목의 변경·새 항목·미래 항목 누락은 거부한다. seek로 효과 객체 ID만 바뀌는 것은 허용한다. 이 연결 검사는 켜기 준비 비용이며 프레임 구동 시간에 포함되지 않는다.
- O는 `table.Decorations[d].Reasons.Count == 0`인 장식이다. 지원 판정과 기존 제외 사유 집계는 유지했다. **23,631은 인계 문서의 이전 실측이며 이번에 다시 측정한 값이 아니다.** 이번 코드로 그 맵의 O 수를 확인하지 않았다.
- 기존 `FastMove.Run`의 대상 루프에서 O만 건너뛴다. O 밖은 같은 효과 안에서도 기존 경로로 처리한다. `SetPlacementType`은 트윈 없는 메타데이터 동작이므로 기존 위치에서 남긴다. O의 길이 있는 Rec 생성·즉시 트윈 생성·사전 읽기/교체는 생략한다. 켜기 전에 기존 O 트윈을 `Kill(true)`로 완료하고 DecoEngine 소유 상태를 반환한다.
- `DecoAnim.UpdatePrefix`에서 기존 SongClock 전진 직후 채택 구동을 호출한다. `ActiveEvaluator`의 활성/방금 종료된 채널만 받아 장식별 마스크로 모아 쓴다. 순수 `AdoptState`는 채널의 마지막 값과 유한성을 검사한다. 같은 값은 쓰지 않고, 종료 채널은 이후 프레임의 활성 목록에서 빠진다. 0초 효과도 종료값을 한 번 전달한다.
- `DecoAnim.BakeWrite`는 기존 `setPosX/Y`, `setParOffX/Y`, `setPivX/Y`, `setRot`, `setScale`, `setCol`, `setOpa` 델리게이트를 공유한다. 회전·크기는 기존 `MoveApply.DeferTransform`, 색·불투명도는 `ColorFast.Try*`를 사용한다. 시차 배율은 원래처럼 필드에 쓴다. 크기는 두 축을 모아 같은 setter에 전달하고 색은 RGBA를 모아 쓴다. Rec/대리 Tween을 새로 만들지 않는다.

## 부수 동작·기존 최적화와의 관계

조건/수동/타격·시각 설정 조건·가시성/깊이 변경·이미지/크롭/마스크·추종/고정/히트박스·알 수 없는 장식 효과/태그 변경 가능성은 기존 굽기 제외 사유가 장식 전체로 전파되어 O 밖에 남는다. 원래 `DecoAnim`의 OnUpdate/OnComplete에 있던 장식 setter만 직접 구동한다. 태그/사운드/조건 콜백을 새로 실행하는 경로는 추가하지 않았다. 소스 재연결 때 제외 사유가 달라져도 채택을 거부한다. 다른 모드의 임의 콜백 주입까지 오프라인 검증한 것은 아니다.

`TightLate`/`EulerMemo`는 기존 setter 및 관리자 LateUpdate 경로를 유지한다. `InvisibleSkip`의 투명 위치 미루기와 보일 때 반영하기도 같은 setter 패치를 거친다. `Dormancy`의 색/가시성 wake 경로는 원래 setter에 남아 있다. O의 Rec가 없으므로 DecoEngine의 Rec별 alpha 예약은 만들지 않는다. 기존 엔진 상태는 켜기 전에 해제하며 O 밖 엔진은 계속 돈다. `PosFast`의 Rec별 캐시는 사용하지 않고 전체 위치 setter로 진입한다. 이로 인한 비용 차이는 실제 측정 대상이다.

충돌 가능성이 있는 두 경로는 코드로 막았다. 채택 중 FastMove의 `Precheck.TrySkip` 및 원래 코드를 한 번 더 실행하는 개발용 즉시 효과 검증을 건너뛴다. 채택 쓰기는 Precheck가 켜져 있으면 `InvisibleSkip.TouchDeco`로 계획을 무효화한다. FastMove가 정상 처리하지 못하고 원본 경로로 우회하면 채택 전체를 해제한다. 따라서 지원 밖 대상만 있는 효과라도 우회 조건을 만나면 채택이 중단될 수 있다. 실제 지원 범위/연속 구동 여부는 `on=True` 로그로 확인해야 한다.

추가로 `FrameGen.BuildHints`는 `DecoAnim.CollectMoving`의 Rec 기반 목록을 읽어 O의 움직임을 놓칠 수 있다. 이번 1단계에서는 `FrameGen.Active && FrameGenDecoHints` 조합을 on 시 거부하고, 구동 중 그 조합으로 바뀌어도 해제하도록 막았다. 해당 힌트 수집기까지 확장한 구현은 아니다.

외부 모드의 `scrDecoration`/`scrVisualDecoration`/`scrParallax` setter·Setup·UpdatePosition 및 `ffxMoveDecorationsPlus.StartEffect` 패치, 또는 Quartz optimizer가 있으면 거부한다. 켜기 때와 120 구동 프레임마다 재확인한다. 새 Harmony 패치는 추가하지 않았다. 기존 갱신·FastMove·TightLate 관리자 마무리 후크를 재사용했다. 런타임 `Module.ResolveSignature`를 추가하지 않았다.

## 상태 전이·복귀

SongClock 재동기 횟수가 바뀌면 O 전체 16채널을 현재 시각으로 한 번 평가하고, 실제 필드와 다른 값만 쓴다. 캐시 값이 같아도 seek가 필드를 초기화했을 수 있으므로 재동기 때에는 캐시를 다시 채운다. 정지한 일시정지 프레임은 재동기 횟수를 반복 증가시키지 않도록 보완했다. 일시정지 상태의 시각 변경과 재개는 재동기를 요청한다. 기존 50ms 시계 임계값과 1% 보정 이득은 유지했다.

동일 맵의 Setup/PrepVfx/재준비에서는 채택을 일단 풀고 원본으로 넘긴다. 최초 장식 기준을 검증한 새 ready가 나온 뒤 catch-up이 끝나면 소스 내용을 재검증하고 재채택·전체 resync한다. 재채택 거부 시 그림자 표는 유지한다. 맵 전환·`decobake off`·`Main.Unload`에서는 재채택 요청을 지운다. 서명 수집 도중 세대가 바뀌면 서명을 미완료로 닫는다.

평가·쓰기 예외/비유한 값에서는 즉시 소유권을 끄고, 마지막 유효 곡 시각 기준으로 O를 재동기하되 그 시각까지 시작한 채널을 끝값으로 완료한다. 그 뒤의 효과는 원본이 처리한다. 진행 중 효과를 같은 남은 길이의 Rec로 재생성하지 않는다. 복귀 setter 하나가 실패해도 다른 장식/채널의 복귀는 계속 시도한다. 계속 실패하는 게임 setter의 성공까지 보장할 수는 없으며 실패 로그를 남긴다. `Resilience.RecordFeatureFailure("DecoBakeAdopt", ...)`로 기능 이름별 집계한다.

전체 O resync는 한 프레임 안에서 끝낸다. 분할로 인한 혼합 상태는 만들지 않았다. `resync최악ms`를 실측한 뒤 분할 필요성을 판단해야 한다. 실게임을 실행하지 않았으므로 현재 resync 지연 수치는 없다.

## 계측·서명

`[장식 이동/굽기채택]`에 on/O/`skippedRec`/프레임 수/평균/p99/최악/resync최악/자체 검사 수를 출력한다. p99는 최근 최대 65,536프레임, 평균·최악은 측정 구간 전체다. `skippedRec`는 생략한 **길이 있는** 채널 생성 수다. 즉시 트윈 생략은 여기에 더하지 않는다. `fuse begin`이 채택 카운터도 초기화하므로 같은 측정 시작점으로 비교할 수 있다. 시작 resync 수치는 초기 `adopt state`에서 먼저 저장한다. 끔에서는 채택 평가·쓰기·시계 계측을 하지 않고 빠른 분기만 통과한다. 통계 버퍼도 최초 on 때 만든다.

자체 검사는 120 구동 프레임마다 최대 4장식×16채널의 `table.Eval(tb)`와 마지막 쓴 값을 비교한다. 원본 Rec와 대조하는 검사가 아니다. 전체 장식 시각적 동일성은 별도 서명 대조가 필요하다.

`decobake adoptsig <이름> [초=8] [표시Hz=120]`은 O 전체를 매 관리자 LateUpdate 마무리 후 기록한다. `adoptsig off`는 명시적으로 수집을 종료한다. 파일은 모드 폴더의 `adoptsig-<이름>.bin.gz`이며 기존 파일을 덮어쓰지 않는다. 30초/2,000프레임 상한, 빈 O/비유한 값/세대·채택 상태 변경/되감기 거부, 완료 footer를 갖는다. **이 수집은 정확도 전용이며 성능 측정에 섞지 않는다.** 큰 float/byte 버퍼를 재사용해 장식별 배열 생성과 압축 호출을 피했지만, 전체 O의 읽기·투영·압축·파일 기록 비용은 여전히 있다.

헤더의 표 지문은 채널·구간·초기값·지원 순서를 확인하는 용도다. 판 간 객체 ID를 비교하지 않고 최초 전체 장식 순번으로 정렬한다. 본문은 raw song time/tb/frame과 16개 논리 채널, pivotTrans의 실제 화면 XY, 두 축 world→pixel 환산값, 실제 스프라이트 세 모서리의 화면 좌표·렌더러 RGBA·활성/숨김/스프라이트 존재 플래그까지 35개 값을 기록한다. 논리 필드만 맞고 지연된 Transform/렌더러 색이 반영되지 않은 경우도 잡기 위한 구성이다. 오프 판에서도 지원 O 전체가 대상이다.

`tools/optimization/adoptsig-compare.py off.bin.gz on.bin.gz [--start 초 --end 초]`는 표/대상/표시Hz 일치와 서로 다른 채택 상태, 완료·비어 있지 않은 파일을 검사한다. ±1표시프레임 안의 후보 중 **한 장식의 모든 채널을 함께 만족시키는 한 프레임**을 찾으며 양방향으로 비교한다. 위치·피벗은 0.01px 환산, 실제 화면 XY/스프라이트 모서리는 0.01px, 크기는 상대 1e-4, 논리/렌더러 색·불투명도는 1/255다. 추가 회전·시차 채널은 절대 1e-4, 활성/숨김 플래그는 정확 일치를 사용한다. 오디오 시각 지터는 50ms 이내에서 정렬한다. 불일치 장식·채널·시각 상위 20을 허용 오차 배수로 출력한다. 시간 이웃이 없는 행도 실패이며, 공통 구간을 지정해도 파일 전체의 완료/유한성 검사를 생략하지 않는다. 반환값은 통과 0/불일치 1/파일·계약 불완전 2다.

서명은 장식 상태 대조이며 최종 합성 영상·충돌·물리 지연·곡 전체 품질의 증명이 아니다. 카메라/게임 타이밍 차이, 캡처 부하로 시간 이웃이 부족하면 통과로 처리하지 않는다.

## 오프라인 검증

근거 파일: `tools/optimization/validation-bake8/`.

|검사|결과|
|---|---|
|`dotnet build -c Release`|오류 0, 경고 38|
|`dotnet build -c Release -p:Edition=Player`|오류 0, 최종 증분 빌드 경고 4(앞선 전체 컴파일 36)|
|`dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1`|오류 0, 경고 38|
|LoadCheck 개발/AUTOTEST|이름 기반 FieldRef 267곳, 불일치 0; 기존 Setup/PrepVfx 계약 각각 정확히 1개|
|LoadCheck 일반 Player|FieldRef 229곳, 불일치 0; DecoBake/Core/AdoptState 및 DecoAnim.BakeWrite 제외 확인|
|실제 Core를 링크한 계약 fixture|99개 통과: resync/되감기/전진/초기화 복구/정확한 종료·0초/혼합 대상 분할/예외 복귀/비유한 값/일시정지·재개/소스 재연결·과거 예약 소모와 미래 변경 구분 포함|
|Python 서명 fixture|10개 테스트 통과: 픽셀·크기·색/불투명도 허용 오차, 실제 스프라이트 모서리·렌더러 색·가시성 차이, 시간 이웃, 장식 전체 채널의 공동 이웃, 양방향 검출, 공통 구간, 미완료/빈/다른 표/잘림/NaN 거부|

LoadCheck에는 실행 중 이름이 정해지는 기존 FieldRef 5곳의 정적 검사 한계가 있다. 빌드 경고에는 어셈블리 버전 충돌·구식 Unity API·미사용 필드가 포함된다. .NET 오프라인 검사는 Unity Mono의 실제 setter/프레임 순서/화면 대조를 대신하지 않는다.

## 내가 돌릴 순서 — 후속 실행자용, 이번에는 실행하지 않음

맵 파일은 읽기 전용 디렉터리 확인으로 한 개임을 확인했다.

```text
D:\얼불춤 맵 파일\Arche\[𝐃𝐎 𝐍𝐎𝐓 𝐎𝐏𝐄𝐍 𝐓𝐇𝐈𝐒 𝐀𝐓 𝐇𝐎𝐌𝐄] FULL.adofai
```

설치/게임 실행 권한이 있는 후속 실행자가 이번 개발 또는 AUTOTEST DLL을 적용한 뒤 진행한다. High 시각 설정, 동일 해상도·pitch·DOTween 배율·최적화 설정을 유지한다. 아래 `<Arche FULL 경로>`에는 위 전체 경로를 그대로 쓴다. 이전 시그니처 파일과 겹치지 않는 이름을 쓴다.

### 1. 서명 A/B

각 판을 독립적으로 시작한다. 다음은 OFF 판이며 ON 판은 `adopt off`→`adopt on`, `arche-off-01`→`arche-on-01`만 바꾼다. `seek 262 8`은 약 254초 타일부터 재생하므로 실제 캡처 위치는 `waitsongpos`로 맞춘다.

```text
decobake on
open <Arche FULL 경로>
select 0
auto on
play
wait 1
decobake ready
stop
seek 262 8
wait 1
decobake ready
decobake adopt off
decobake adopt state
waitsongpos 263 120
decobake adoptsig arche-off-01 10 120
waitsongpos 274 120
decobake adoptsig off
decobake adopt state
stop
decobake off
```

ON 판은 처음 상태 로그에서 `on=True`, 기대 O, 중간 해제/복귀 실패 없음, 수집 종료 `complete=True`를 확인한다. 지원/스케줄 변화·O=0·2,000프레임 상한·중간 오류가 있으면 그 판은 증거로 쓰지 않는다. 120은 실제 표시Hz에 맞게 양쪽 함께 바꾼다. 화면·렌더 프레임 수가 달라 시간 이웃이 부족하면 캡처 부하와 해당 시각을 조사한다.

```powershell
$adoptModDir = 'D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\Mods\StutterFix'
python tools/optimization/adoptsig-compare.py "$adoptModDir/adoptsig-arche-off-01.bin.gz" "$adoptModDir/adoptsig-arche-on-01.bin.gz" --start 264 --end 272
```

첫 번째 대조를 통과하면 초반 구간, 효과 교체 경계, 0초 효과, 일시정지→재개, 되감기/재시작 직후의 별도 OFF/ON 캡처도 같은 방식으로 비교한다. 전이 후 재준비가 있다면 새 ready·재채택이 완료된 다음 새 이름으로 수집한다. 전이 순간의 팝/깜빡임은 별도 화면 점검도 필요하다. 먼저 원본 OFF/OFF 반복의 동일 시각 안정성을 확인하면 게임 자체 지터와 채택 차이를 구분하는 데 도움이 된다.

### 2. 서명 없는 성능 ABBA

같은 준비(open→초기 ready→stop→seek→새 ready) 뒤 256초 부근에서 원하는 `adopt on/off`와 상태 확인을 끝낸다. 시작 resync 수치는 이때 보관한다. 다음 공통 측정 구간을 쓴다.

```text
waitsongpos 258 120
fuse begin
waitsongpos 278 120
fuse end
decobake adopt state
stop
decobake off
```

직접 `sf_ab(setting="DecoBakeAdopt", ...)`를 사용하면 안 된다. 채택은 Settings 필드가 아니라 명령이다. 기존 `sf_run`에 판별 steps를 주어 `sf_ab`와 같은 OFF/ON/ON/OFF 순서를 만든다. 예시 형태이며 이번 세션에서 import/실행하지 않았다.

```python
import sys
sys.path.insert(0, 'tools/sfmeasure')
from server import sf_run
# prepare: 위 공통 준비 단계. measure: 위 서명 없는 258~278 단계.
# map_path: 위에서 확인한 하나의 FULL.adofai 경로.
prepare = ['decobake on', 'open ' + map_path, 'select 0', 'auto on',
           'play', 'wait 1', 'decobake ready', 'stop', 'seek 262 8',
           'wait 1', 'decobake ready']
measure = ['decobake adopt state', 'waitsongpos 258 120', 'fuse begin',
           'waitsongpos 278 120', 'fuse end', 'decobake adopt state',
           'stop', 'decobake off', 'quit']
for i, mode in enumerate(('off', 'on', 'on', 'off')):
    text, metrics = sf_run(prepare + ['decobake adopt ' + mode] + measure,
                           settings={}, tag=f'bake8-{i}-{mode}')
    print(text)
```

워밍업은 별도 한 판 버린다. ON 구간 내내 채택이 유지되었는지 로그를 함께 저장한다. `sf_run` 전체 FPS에는 준비/lead-in도 포함될 수 있으므로 258~278 성능은 Fuse 구간 카운터 및 동일 구간 프레임 로그/PresentMon으로 구분한다. 기존 `[장식 이동]`·`[엔진 단계]`·FuseLate와 새 채택 시간을 모두 본다. O의 원본 갱신만 사라졌다고 전체 개선으로 해석하지 않는다. 효과 시작+원본 모드 갱신+채택 구동+관리자 LateUpdate의 중복 없는 합계를 비교한다. 서명 기록이 켜진 판의 FPS는 이 비교에서 제외한다.

기대 확인값은 OFF 채택 프레임/시간 0, ON의 O 유지·`skippedRec` 증가·자체 검사 실패 0·비정상 복귀 0·서명 불일치 0이다. 평균/p99/최악/resync최악과 기존 Rec 수 감소를 실제 숫자로 보고한다. CPU 2~3ms 절감, 최대 약 125FPS는 임무서의 측정 전 가설이며 이번 결과가 아니다. GPU draw 바닥 개선도 주장하지 않는다.

## 작업 관찰 전달

이 작업은 제한된 쓰기 권한을 가진 worktree이므로 task-observer는 결과 문서로 관찰을 전달한다. 테스트용 `TemporaryDirectory`가 이 환경에서 접근 불가 폴더를 만든 것을 확인했다. 디렉터리를 만들지 않는 고유 임시 파일로 fixture를 바꿔 통과했고, 작업 폴더에 생성했던 빈 폴더 16개는 절대 경로·직계 하위·빈 폴더 여부를 확인한 뒤 정리했다. 스킬/메모리 파일은 수정하지 않았다.
