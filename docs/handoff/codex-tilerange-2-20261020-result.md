# T3 결과: 쓰기 훅 기반 pristine 청크 건너뛰기

2026-10-20. 작업 폴더 `E:\StutterFixWork\wt-tilerange`. 커밋·설치·게임 실행·`profsamp` 실행 없음. 개발자/AutoTest 전용, 기본 꺼짐 유지. 게임에서의 성능 개선과 곡 전체 동일성은 아직 미검증이다.

## 구현 및 오프라인 결과

- T2의 `InstallReaders`, 전체 AppDomain 어셈블리 탐색, 읽기 barrier, 지연 불투명도 반영을 제거했다. `FloorRangeLedger.cs`는 과거 fixture용으로 보존하고 모드 컴파일에서 제외했다.
- `FloorRange.cs`: 64타일 청크의 위치/회전/크기/불투명도 dirty 비트, 기본 목표 효과의 청크 분류, dirty 청크의 기존 경로 즉시 실행, 순환 안전망, 표본 shadow.
- `FloorRangeWriters.cs`와 `FloorRangeWriterManifest.cs`: 오프라인 IL 목록에서 정한 **53개 게임 메서드/생성자**, 상한 64개. 타입+메서드 이름+정확한 서명 blob으로 `GetMethods`/`GetConstructors`를 순회해 하나만 선택한다. `AccessTools.DeclaredMethod` 이름 조회나 시스템·Unity·외부 모드 어셈블리 패치는 없다.
- 개발자 빌드 오류 **0**, 경고 **38**. Player 오류 **0**, 경고 **36**. PlayerAuto 오류 **0**, 경고 **38**. 경고에는 참조 버전 충돌과 기존 코드의 사용되지 않는 필드 등이 포함된다.
- 컴파일된 DLL 메타데이터 검사: `FloorRange`/`FloorRangeWriters`는 개발자·AutoTest에만 존재. Player에는 없음. 구 `FloorRangeLedger`는 세 빌드 모두 없음.
- 실제 게임 DLL의 메타데이터에서 **53개 각각 이름+서명 일치 1개**, 관리 메서드 본문 존재 확인. `ffxPlusBase.floor`/`ffxFloorAppearPlus.prevFloor` 형식 및 `FloorRenderer.material` 필드 확인. **실제 Harmony 설치 성공 53개를 의미하지는 않는다.**
- 현재 `FloorRange.cs`를 직접 컴파일한 모델 fixture **70검사 통과**. 1,000·1,001·8,171·8,933·90,000타일의 clean 일반 경로에서 기존 타일 처리 호출 **0**. shadow에서는 1/16 표본만 oracle 실행. dirty 한 청크는 **64타일**, 부분 청크 경계는 해당 범위만 실행. 트윈·채널 분리·안전망·재인증·맵 교체·설치 실패·shadow 실패 시 중복 처리 방지 등을 검사했다.
- LoadCheck: 이름 고정 `FieldRefAccess` **266곳**, 틀림 **0**. 이름을 실행 중 정하는 곳 **5개**는 별도 런타임 확인 대상이다.

집계와 DLL SHA-256은 [t3-validation-summary.json](tilerange-evidence/t3-validation-summary.json)에 있다. 이 수치는 Unity/Mono 실행·실제 게임 효과 22.44ms 감소·FPS 개선·렌더링 동일성을 입증하지 않는다.

## 쓰기 경로 조사와 훅 목록

조사 입력은 현재 설치된 게임의 `Assembly-CSharp.dll`과 이번 개발자 `StutterFix.dll`이다. 게임 메서드는 실행하지 않고 `tools/ilscan`의 `TILE_WRITERS=1` 모드로 조사했다. Transform 참조와 쓰기가 한 메서드에 있다는 것은 별칭의 대상이 반드시 타일이라는 증명이 아니므로, Transform 쓰기 wrapper는 실제 준비한 타일 Transform일 때만 dirty를 표시한다.

- [게임 전체 후보 목록: 메서드·토큰·서명·필드/Transform 쓰기](tilerange-evidence/t3-game-writers.txt)
- [모드 쓰기 목록](tilerange-evidence/t3-mod-writers.txt)
- [scrFloor 호출/필드 상세](tilerange-evidence/t3-scrfloor-il.txt)
- [사라지기 효과의 트윈 생성 경로](tilerange-evidence/t3-disappear-il.txt)
- [실제 게임 DLL에서 53개 정확한 서명을 확인한 출력](tilerange-evidence/t3-writer-metadata.txt)
- 전체 패치 대상의 기계 판독 목록과 서명: [FloorRangeWriterManifest.cs](../../FloorRangeWriterManifest.cs)

| 소유 타입 | 대상 메서드 | 역할/처리 |
|---|---|---|
| `scnGame` | `ApplyEventsToFloors`, `PrepVfx` | 맵 준비 중 startPos/Transform 쓰기 위치에만 표시 |
| `scnGame` | `ResetScene` | 준비 기록 폐기 |
| `scrLevelMaker` | `MakeLevel` | 맵 생성 시작 시 준비 기록 폐기 |
| `scrLevelMaker` | `ResetFloor`, `MakeFreeroamGrid` | Transform/startPos/startRot/tweenRot 쓰기 표시 |
| `FreeroamArea` | `.ctor(scrFloor)` | 소유 타일 청크 전체 채널 무효화 |
| `scrFloor` | `Awake`, `Reset`, `SetRotation`, `ResetToLevelStart` | 인스턴스의 모든 채널을 진입 때 보수적으로 dirty |
| `scrFloor` | `TweenRotation`, `TweenOpacity`, `MoveToBack`, `MoveBackBy` | 나중 콜백이 쓸 트윈을 만들기 전부터 dirty |
| `scrFloor` | `LateUpdate` | 실제 Transform setter 호출 위치만 표시. LateUpdate 진입마다 무조건 dirty로 만들지 않음 |
| `scrFloor` | `<TweenRotation>b__203_1`, `<TweenRotation>b__203_2`, `<TweenOpacity>b__204_1` | 트윈 회전/불투명도 콜백 |
| `ffxLightBridge` 및 중첩 display class | `Start`, `StartEffect`, `<StartEffect>b__1` | 불투명도 직접 쓰기/콜백 |
| `ffxFloorAppearPlus` | `StartEffect` | `floor`, `nextfloor`, `prevFloor` 청크를 효과 시작 전에 dirty |
| `ffxFloorAppearPlus` | `FloorSetup`, `<StartEffect>b__7_1` | 초기 Transform 쓰기 위치에 표시. 관련 없는 extendAnim 필드 쓰기는 변경하지 않음 |
| `ffxFloorDisappearPlus` | `StartEffect` | DOMove/DOScale 등 지연 트윈 생성 전에 floor/nextfloor dirty |
| `ffxMoveFloorPlus` | `StartEffect` | **원래 본문이 실행될 때만** 준비 기록 폐기. FloorAnim prefix가 본문을 생략하면 이 barrier도 실행되지 않음 |
| `ffxMoveFloorPlus` 중첩 display class | `<StartEffect>b__6`, `<StartEffect>b__7` | 원래 회전 트윈 콜백의 쓰기 표시 |
| `DivineIntervention` | `Awake`, `Update` | 특수 연출의 Transform 쓰기 위치 표시 |
| `LaserSpawner` | `Update` | 동일 |
| `Mawaru` | `SoccerUpdate`, `HighFiveUpdate`, `RandomizeTiles`, `DefendUpdate`, `NishiGoatUpdate`, `Defend2Update`, `FinaleUpdate` | 동일 |
| `SingSing_Notefield` | `Update` | 동일 |
| `TaroBGScript` | `FloorResetPosition`, `FloorBeatVibe`, `FloorDrunkVibe`, `FloorDrunkVibeXPos` | 타일 위치/회전 쓰기 |
| `ThirdSun` | `Update` | 특수 연출의 Transform 쓰기 |
| `scnMinesweeper` | `ProcessTile`, `Fail`, `EndingUpdate` | 동일 |
| `Cutscene1` | `Awake`, `CreateNameEntry`, `SkewNameEntry` | 동일 |
| `PuzzleRoom` | `Update` | 동일. startPos 주소 참조는 보수적으로 dirty |
| `scnTaroMenu2` | `Start` | 동일 |

모드의 쓰기는 `FloorAnim.TweenFloor` 진입의 `BeforeLegacy`와 `New`/`Complete`/`Step` 경로에서 무효화한다. 따라서 Zero/OpaOnly의 즉시 쓰기, 진짜 DOTween으로 대체하는 분기, 트윈 재사용/완료도 포함된다. FloorAnim은 보수적으로 모든 채널을 dirty로 표시하고, 게임의 직접 setter/필드 wrapper는 해당 채널을 표시한다. 프레임별로 타일 수만큼 상태를 조회하는 새 루프는 없다. 사전 조회는 실제 쓰기 경로와 정해진 안전망 표본에서만 한다.

Transform은 position/localPosition/eulerAngles/localEulerAngles/rotation/localScale/SetPositionAndRotation 호출을 감싼다. scrFloor는 opacity/startPos/startRot/tweenRot 필드 쓰기를 감싸고, 이 벡터 필드의 주소를 넘기는 대상 메서드에서는 주소 취득 전에 보수적으로 무효화한다. 임의 읽기 메서드는 패치하지 않는다. 삽입 명령으로 분기 label/예외 시작을 옮기며, 예외 끝은 원래 명령에 남긴다. Harmony의 framework `Label` ABI는 준비 때 비제네릭 `IList`로 다룬다.

`on`에는 대상 수, 각 대상의 이름/서명과 설치 성공/실패를 출력한다. 하나라도 실패하면 설치한 연구 Harmony를 전부 해제하고 `ready=False`, `patch-fail>0`으로 거부한다. 미설치·필드 불일치·대상 상한 초과도 이유를 남긴다.

## clean 인증과 건너뛰기 의미

목표는 위치 오프셋 0, 회전 오프셋 0, 크기 1이다. NaN인 위치/크기 축은 원래 코드처럼 사용하지 않는 축으로 취급한다. 기본 불투명도는 준비 시 `opacityVal`이며, 청크의 값이 비트 단위로 균일할 때만 opacity 목표를 인증한다. 이미 바뀐 값을 무조건 clean으로 선언하지 않고, 준비 때 실제 상태와 진행 중 트윈을 한 번 확인해 dirty를 설정한다.

원래 `FloorAnim.TweenFloor`와 `OpaOnly`의 순서는 이전 해당 채널 트윈 `Prev/Kill(true)` → 현재값과 목표 비교 → 다를 때만 쓰기/새 트윈 생성이다. 아래 조건이면 같은 타일에 대해 쓰기나 트윈 변경이 발생하지 않는다.

1. 준비/안전망 확인 때 실제 position.x/y가 `startPos + (0,0,0)`과 `Mathf.Approximately`, eulerAngles.z가 `(startRot + (0,0,0)).z`와 `Mathf.Approximately`였다. 이후 해당 채널 쓰기 훅이 없었다.
2. scale 검사는 게임이 쓰는 **같은** `ApproximatelyXY`와 `WithX/WithY`를 호출한다. 독자적인 오차 임계값을 만들지 않았다. 실행 중 이전 트윈이 없으므로 `Prev`가 원래 캡처한 scale을 바꾸는 문제도 없다.
3. 불투명도는 실제 opacity와 인증 기준을 `Mathf.Approximately`로 확인한다. 효과 목표는 이 기준과 **비트 일치**해야 한다. Approximately의 비추이성 때문에 “둘 다 기준과 대략 같다”를 이어 붙여 판단하지 않는다.
4. 실제 DOTween은 active가 아니고, FloorAnim proxy의 `Rec.Running`도 false여야 한다. proxy는 `Tween.active`만 보면 진행 중을 놓칠 수 있어 별도로 검사한다. 그러면 `Prev`의 완료/콜백 부작용이 없다.
5. freeroam, 누락된 renderer/material/tween 사전, 중복/없는 타일 같은 상태는 clean 인증에서 제외한다. 실제 게임의 `FloorRenderer.material`은 **필드**다. 원래 코드의 `_ = floorRenderer.material` 읽기 자체가 복제 getter를 실행하는 경우는 이 DLL에서 확인되지 않았다.

효과 시작에서는 청크의 dirty 비트와 목표만 읽는다. clean 청크는 타일에 접근하지 않고 통째로 생략한다. dirty 청크는 부분 경계까지 포함해 `FloorAnim.RangeLegacy`로 그 자리에서 원래 순서대로 처리하고, freeroam 타일도 원래 경로를 따른다. 양수 duration과 Zero 모두 이 no-op 증명이 적용된다. 기본이 아닌 목표, gap, 작은 효과(<1,000), Split/대기 작업, TileSim 캡처, 준비/맵 불일치, shadow 실패는 원래 경로다. 대체/덮어쓰기를 전역 오류로 취급하지 않는다.

이 증명은 **포착한 쓰기 경로 사이에서만** 성립한다. 포착하지 못한 외부 쓰기는 아래 안전망 탐지 전까지 인증을 깨뜨릴 수 있다.

## 안전망과 shadow

매 프레임 `ceil(청크 수/32)`개의 청크에서 최대 4개 타일을 검사한다. 타일 검사 커서는 청크 안에서 이동한다. 90,000타일이면 1,407청크, 프레임당 최대 **44청크 × 4 = 176타일**이다. Unity에서 몇 µs인지 아직 측정하지 않았다. 실제 프레임 비용은 로그로 확인해야 한다.

dirty를 해제하려면 그 청크의 **모든 타일**을 검사해야 하고, 검사 도중 쓰기 훅의 generation이 바뀌면 처음부터 다시 검사한다. 네 표본만 같다고 clean으로 돌리지 않는다. 64타일 청크는 최소 16회 방문이 필요하며 약 512프레임의 안정 구간이 필요할 수 있다. 활성 트윈·freeroam·누락 상태가 있으면 해당 채널은 dirty를 유지한다. 타일/Transform 동일성이 바뀌면 청크를 영구 차단한다. 외부에서 늦게 발견한 채널 변화는 `late-changes`/`late-write-detected`에 합산한다.

shadow는 건너뛸 범위의 인덱스 `i % 16 == 0` 타일을 검사한다. 우선 원래 코드의 no-op 조건을 모든 표본에서 확인해, 실패한 표본 때문에 oracle가 예상치 못한 트윈을 만든 후 중복 적용하는 일을 방지한다. 통과한 표본에는 실제 `FloorAnim.RangeLegacy`를 실행하고 Transform position/eulerAngles/localScale, tweenRot, opacity의 비트, dontChangeMySprite, moveTweens 사전 참조·개수·키별 Tween 참조가 전후 그대로인지 검사한다.

preflight 실패도 mismatch로 센다. 처음 8건까지 상세를 출력하고, 해당 효과의 남은 경로와 이후 효과는 기존 경로로 돌린다. oracle 실행 뒤 불일치가 나도 이미 실행한 표본을 두 번 처리하지 않는다. `mismatch-latched=True`는 새 on/shadow 준비 전까지 유지한다. shadow에는 표본 스냅샷 할당·원래 함수 실행·비트 비교 비용이 있으므로 성능 판에서 끈다. `tilesig`의 기존 flush 호출은 T3에서 반영할 지연 상태가 없으므로 no-op이다.

## 계측과 수명

효과별 `[타일구간] start`:

- `effects`, `chunks`, `skipped-chunks`, `dirty-chunks`, `tiles-processed`, `start-ms`.
- `classify-ms`, `skip-shadow-ms`, `original-ms`, `unaccounted-ms = 측정한 start 구간 - 세 단계 합`. 음수 값을 0으로 숨기지 않는다.
- `original-ms-estimate`: 처리한 dirty 타일의 관측 평균으로 전체 타일 수를 외삽한 추정치다. 사용 가능한 관측이 없으면 0과 `estimate-source=unavailable`. **끄기 판 실측이나 개선량 근거로 쓰지 않는다.** 이전 효과의 관측값이 재사용될 수 있고, clean/dirty 타일의 비용도 다를 수 있다.

`status`에는 누적 단계 시간, safety/hook 시간, hook-calls/정상 추적 hook-normal/dirty-marks/hook-untracked, safety-samples/late-changes, verify/mismatch, patch-fail과 원인별 카운터가 있다. `pipeline-ms`는 `pipeline-frame` 프레임의 FloorAnim 시작·갱신·개발자 검증과 T3 안전망/쓰기 훅 구간을 중첩 없이 합한 값이다. 이전 프레임·최고 프레임·누적값도 보존한다. 단계 내부 hook 비용을 다시 더하지 않는다. 기존 경로 폴백과 start 로그 출력 비용도 바깥 계측에 들어간다. 렌더링/GPU/전체 DOTween 스케줄러 등 게임 프레임 전체를 뜻하지는 않는다. `start-ms`는 로그를 만들기 전까지이며 로그 비용은 바깥 pipeline 측정에 포함된다.

기존 `[타일 이동 효과]`의 Tiles는 실제 기존 함수가 처리한 수이므로 clean 생략 후 목표 전체 타일 수와 다를 수 있다. 전체 범위/생략 수는 새 start 로그로 확인한다.

정리 위치: `FloorAnim.Clear`의 `FloorRange.ResetMap`, `scnGame.ResetScene`/`scrLevelMaker.MakeLevel`의 기록 폐기, 목록 참조/개수 변화 감지, `tilerange off`의 `Shutdown`, 기존 **`Main.Unload` → `FloorRange.Shutdown`**. 연구 Harmony ID는 `StutterFix.FloorRange.Writers`. 준비 수명 postfix는 Main 소유 Harmony이고 Main의 기존 UnpatchAll에서 해제한다. AppDomain 이벤트를 새로 등록하지 않는다. 타일 Transform/필드에 미반영 상태를 남기지 않아 off/unload의 전수 flush도 없다.

## 알려진 위험과 미구현 검증

1. **실제 Harmony/Mono 패치·게임 실행 0회.** 이름/서명 일치와 C# 빌드 통과는 transpiler의 실제 엔진 적용을 증명하지 않는다. 패치 예외가 나면 ready가 되지 않도록 구현했으며 스모크에서 확인해야 한다.
2. **안전망은 즉시 탐지 보장이 아니다.** Quartz/PACL2 등 다른 모드의 직접 Transform/필드/사전 수정, 부모 Transform 이동, 동적 IL/이미 인라인된 코드, 준비 후 같은 길이 목록의 원소 교체는 포착한 쓰기 훅 밖일 수 있다. 샘플링 전 잘못 생략할 가능성이 있다. 1/16 shadow도 전체 타일을 증명하지 않는다. full-song tilesig를 통과하기 전 채택하지 않는다.
3. IL 목록은 참조 기반 후보 조사이며 전체 별칭/호출 그래프 증명이 아니다. 행성·카메라·편집 UI의 명백한 비타일 후보 등은 finite manifest에서 제외했다. 특수 연출도 목록의 직접 쓰기만 감싼다. 새로운 게임 버전/서명은 다시 조사해야 한다. 새 어셈블리 자동 탐색/자동 패치는 없다.
4. clean 비율이 실제로 낮으면 이득이 작다. 기본 scale 1과 다른 맵, 감김 전 회전값, 혼합 base opacity, 잦은 재생/되감기, 진행 중 트윈은 보수적으로 기존 경로를 유지한다. 특히 양수 길이 효과의 predecessor 완료를 생략할 수 있다고 가정하지 않았다.
5. 안전망·쓰기 훅·shadow·로그의 실제 비용, Lost Requiem 531.8초의 생략률과 시작 시간, HELLO 125.6초의 ≤3.9ms 잔여 이득, 전체 프레임 개선, 곡 전체 tilesig/그림/판정 동일성은 **미측정**이다. 기본 켬이나 Player 이식은 하지 않았다.

## 사용자가 게임에서 실행할 순서

1. 이번 개발자 또는 AutoTest DLL을 별도로 설치하고 파일 플래그 없이 시작한다. **`tilerange off` 기준 판**을 새 게임 프로세스에서 실행해 해당 효과의 원래 시작 로그, 전체 프레임, 같은 음악 시각의 tilesig를 저장한다. 진단/서명 프레임을 성능 구간에 섞지 않는다.
2. 준비 완료·재생 전 정지 상태에서 **`tilerange shadow`**. `writer-targets=53`, 각 writer-ok, `ready=True`, **`patch-fail=0`**을 확인한다. 실패면 시작 성능 판으로 넘어가지 말고 writer-fail/원인 로그를 전달한다. T2처럼 시스템 어셈블리를 탐색하지 않는다.
3. **Lost Requiem 531.8초**를 우선 실행. 이벤트 뒤 `tilerange status`로 mismatch=0, mismatch-latched=False, late-changes와 원인을 확인한다. `[타일구간] start`의 chunks/skipped-chunks/dirty-chunks/tiles-processed/각 단계 시간을 저장한다. 현재 실제 실행 결과가 없으므로 특정 생략 개수나 시간 목표 통과를 가정하지 않는다.
4. 끔/켬 판을 동일 조건·동일 시각에 중단해 `tilesig lr-off`, `tilesig lr-on` 등으로 비교한다. 곡 전체에 걸쳐 같은 시각의 체크포인트를 모아 비교한다. 이것은 연속 모든 프레임이나 픽셀 전체의 증명은 아니다. 다시 하기·되감기·씬 전환·off/unload도 별도로 확인한다.
5. shadow 스모크와 서명이 통과하면 **shadow 없는 `tilerange on` 성능 판**을 독립 프로세스의 끔/켬/켬/끔으로 실행한다. start만 아니라 같은 프레임 전체와 pipeline/safety/hook 비용을 함께 본다. 원래 경로 추정치는 비교 실측을 대신하지 않는다.
6. **HELLO 2026 125.6초**에서 같은 절차. opacity 목표가 준비된 base opacity와 다르거나 청크가 dirty면 기존 경로가 정상이다. 효과가 3.9ms였다는 기존 결과 이상을 새 이득으로 주장하지 않는다.
7. 합성 **`C:\SFBundle\autotest\maps\h90000`**에서 기본 목표 효과, 부분 경계·겹침·기존 활성 트윈·타일 나타나기/사라지기·안전망 카운터를 확인한다. on/shadow는 준비 완료 후 실행한다. 기본 목표 효과가 없으면 이 후보의 성능 검증으로 세지 않는다.
8. 위 관문 뒤 사용자의 **9만 타일 맵**을 실행한다. 문서·코드·로그에는 이 표기만 쓴다. **100만 타일 맵은 열지 않는다.** `profsamp`는 사용하지 않는다.

실행 명령/fixture 범위는 [tools/tilerange-fixture/README.md](../../tools/tilerange-fixture/README.md)에 있다. 현재 완료 범위는 T3 구현과 오프라인 검증까지다.
