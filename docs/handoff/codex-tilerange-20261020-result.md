# T2 타일 구간 연구 결과 — 2026-10-20

## 상태와 범위

**T2 전체 미완료.** 기본 꺼짐 연구 구현을 추가했다. 현재 가져가는 것은 **1,000개 이상·간격 1·길이 0·불투명도 전용** 효과다. 길이가 있는 효과와 위치·회전·크기 효과는 기존 `FloorAnim` 경로를 유지한다. Lost Requiem 531초의 위치 효과를 해결했다고 보고하지 않는다.

커밋·게임 실행·설치·배포는 하지 않았다. 일반 Player에는 `FloorRange`와 `FloorRangeLedger`가 컴파일되지 않는다. 아래 빌드/fixture 결과는 게임의 정확성·성능 측정을 대신하지 않는다. 특히 효과 함수의 시작 시간이 줄어도 **같은 프레임의 Materialize 비용이 커지면 실제 끊김은 남는다.**

먼저 읽은 문서: `docs/roadmap-deco-pipeline-20261020.md`, `docs/handoff/gemini-sorting-and-tile-o1-20261020.md` 2절, 과제 인계 문서. 작업 브랜치는 `codex/night-tilerange`이며 기존 사용자 변경이 없는 상태에서 시작했다.

## 제안 사실 확인

| 확인 항목 | 코드·IL에서 확인한 사실 | 구현에 반영한 판단 |
|---|---|---|
| 같은 효과의 duration/ease/목표 파라미터 | `FloorAnim.Run`의 `Ctx`는 효과마다 한 번 생성. 게임 `ffxMoveFloorPlus.StartEffect` IL도 목표 벡터를 한 번 구성한다. | 공통 기술자를 만들 수 있는 근거다. **타일의 시작 상태까지 같다는 뜻은 아니다.** |
| 위치·회전 목표 | `target.startPos + c.Pos`, `(target.startRot + c.Rot).z`이므로 절대 목표값은 타일마다 다르다. | Transform 효과는 보류. 덧셈을 분배하거나 목표 오프셋만 보관해 원래 float 식을 바꾸지 않았다. |
| gapLength | 현재 코드는 타일당 시간 지연이 아닌 `step = 1 + gapLength`의 선택 간격이다. | `step != 1`은 이유를 세고 기존 경로. gap을 duration 지연으로 해석하지 않았다. |
| eventTag·인덱스 의존 | 현재 `StartEffect`/타일 처리 IL에서 eventTag를 타일마다 읽는 갈래는 찾지 못했다. Decode는 타일 구간과 목표를 먼저 구성한다. | 이 메서드 밖의 사용자 정의 효과까지 균일하다고 일반화하지 않았다. |
| freeroamArea | 선택 타일 외에 구역의 `isLandable` 타일까지 순회한다. 게임 DLL의 `freeroamArea` 쓰기는 `FreeroamArea` 생성자에서 발견했다. | 준비 때 해당 청크를 제외. 나중에 구역이 생성되면 pending을 반영하고 준비 상태를 폐기. 청크 폴백은 기존 자유 이동 순회도 수행한다. |
| Prev: 모드 기록 Running | `Prev`는 `r.Running`일 때 `Complete(r)` 호출. Complete는 아직 시작 전이면 `Startup`을 하고 원래 이징 식으로 끝점을 계산한다. | 타일을 실제 반영할 때 기존 `Prev`를 호출하여 원래 완주 의미를 유지한다. |
| Prev: 종료/중단된 모드 기록 | `Running == false`이면 다시 완주시키지 않는다. Kill(false)한 기록도 여기에 해당한다. | 직전 목표값을 무조건 새 시작값으로 쓰는 제안을 채택하지 않았다. |
| Prev: 진짜 DOTween | `old.active`일 때만 `Kill(true)`. 콜백·다른 플러그인 효과가 있을 수 있다. | 준비 때 진짜 활성 Tween이 있는 청크 제외. 이후 사전이 외부에 노출되면 반영 후 해당 청크를 영구 폴백시킨다(재준비 때 재판정). |
| 첫 시작값 | `New`에서 읽지 않고 첫 `Step`/`Complete`의 `Startup`에서 읽는다. 크기는 타일별 생성 시점의 localScale도 목표 벡터에 영향을 준다. | 양수 duration을 늦게 `New`한 뒤 시간만 당기는 구현은 하지 않았다. 기존 Split의 근사 동작으로 대체하지 않았다. |
| 완주값 = 목표값? | 실제 스칼라 식은 `FStart + FChange * Eval(...)`. float로 저장한 차이와 반올림 때문에 목표값과 비트가 같다는 보장이 없다. | `prevOffset = TargetOffset`의 체인 소거는 사용하지 않는다. 같은 목표라도 Approximately 및 dontChangeMySprite의 이력이 다를 수 있어 순서대로 재생한다. |
| 길이 0 불투명도 | 원래 `OpaOnly`는 Prev 후 Approximately 검사, 다르면 `opacity = target`, `dontChangeMySprite = true`. 이징·Transform·새 Rec·새 사전 엔트리가 없다. | 이 갈래만 독립 후보로 구현. 원래 메서드를 shadow 기준으로 사용한다. |
| material 읽기 | 이 설치판의 `floorRenderer.material`은 `FloorRenderer`의 **필드**다. `Renderer.material` 복제 getter 호출과 다르다. | 준비 때 재질을 복제/재발급하지 않았다. 원래처럼 필드를 읽는다. 별도 `FloorRenderer.SetFlash`의 `Renderer.get_material`과 혼동하지 않는다. |
| TileCull와 이동 bounds | 원래 제안의 baseBounds 평행 이동은 서로 다른 시작 상태·회전·크기·다른 진행 효과까지 설명하지 못한다. | 이동 bounds를 새로 추정하지 않았다. opacity 후보는 TileCull의 기존 판정과 Cull/Uncull 이벤트만 사용한다. |

근거: `FloorAnim.cs`, `tilerange-evidence/movefloor-il.txt`, `tilerange-evidence/floorrenderer-il.txt`.

## 외부 읽기 조사와 동기화

`tools/ilscan`에 `TILE_READERS=1`을 추가했다. `tools/ilscan/tile-readers.py`는 게임 Managed, 설치된 Mods, 현재 개발 DLL을 **메타데이터로만** 조사한다. 결과 `tilerange-evidence/readers-inventory.json`에는 경로·SHA-256·성공 여부, `readers.tsv`에는 각 메서드/명령/대상과 판정이 있다. Transform 참조는 **별칭/데이터 흐름의 증명으로 해석하지 않는다.**

최종 조사 수치는 `validation-summary.json` 참조. 비 PE인 macOS 리소스 포크 1개는 헤더 검사로 제외하며, 네이티브 PE는 managed metadata가 없어 참조 행이 없다. 처음 비 PE 파일을 도구에 넘겨 지연되었던 실행은 최종 성공 결과에 포함하지 않았다.

| 소비자 | 판정과 훅 |
|---|---|
| `scrPlanet`, `scrController`, 카메라·타일 hit/위치 계산의 Transform 읽기 | 이번 후보는 position/eulerAngles/localScale/startPos를 변경하지 않으므로 기존 값이 곧 정답. 변환 getter를 일괄 가로채지 않는다. 이 판정은 Transform 확장의 허가가 아니다. |
| `scrFloor.LateUpdate`, `scrLevelMaker.DrawMultiPlanet` | opacity를 읽기 전에 typed Materialize barrier. |
| `scrHoldRenderer.UpdateColor` | opacity barrier. |
| `scrObjectDecoration.GetAlpha/SetFloorGlowColor`, `scrVisualDecoration.ApplyColor` | opacity barrier. 장식은 오래된 불투명도를 읽지 않아야 한다. |
| `dontChangeMySprite` 읽기/쓰기: `scrFloor.OnBeat/SetToRandomColor`, 관련 sprite 효과 | 읽기/쓰기 모두 이전 pending부터 반영. |
| `moveTweens`: appear/disappear/move/recolor/setobject 효과 및 freeroam 제어 | 사전 반환/교체 전에 반영하고 청크 fence. 사전 참조를 얻은 뒤 보관하는 외부 코드를 무조건 안전하다고 취급하지 않는다. |
| `floorRenderer` 읽기/교체 | 읽기 전에 반영. 교체는 반영 후 청크 fence. |
| TileCull | Cull에서 가시 개수 갱신, Uncull에서 켜기 전에 해당 타일 반영. StopAll은 모두 반영. 준비 때 기존 IsCulled 상태를 읽어 가시 개수를 초기화한다. |
| RecolorDefer/MeshWarm/ColorFast 등 모드 코드 | 자기 DLL도 조사·transpile 대상. 위 4개 필드를 직접 쓰는 곳이 있으면 같은 barrier 적용. 문자열 검색에 없다는 이유만으로 다른 모드 전체가 안전하다고 판단하지 않는다. |
| FloorAnim 내부 | 자체 transpile에서 제외하고 TweenFloor/Step/개별 Kill·Complete/PlayingTweens/KillAll/FlushPrefix/DropAll에 명시적 연결. 새 zero 효과가 이전 진행 기록의 다음 Step보다 먼저 적용되게 한다. |
| tilesig | pending부터 반영. 기존 색·메시 기록에 position/eulerAngles/localScale도 추가. decosig 형식은 변경하지 않았다. |

런타임에는 준비할 때 로드된 관련 어셈블리 전체를 다시 훑어 typed `ldfld/stfld`를 치환한다. `ldflda`, 해석 실패, generic reader, 패치 실패 등 커버리지 문제가 있으면 **준비 실패로 전체 후보를 받지 않는다.** 이것은 게임 안에서 실제 패치 성공을 확인한 결과가 아니다.

## 구현과 복잡도

- `FloorRangeLedger.cs`: 64타일 청크, 순서가 있는 zero-opacity 기술자, 타일별 소비 cursor. 시작 때 타일 순회 없이 덮는 청크에 기술자만 추가한다. **O(N/64)이며 O(1)이라고 부르지 않는다.** 8,933타일을 0부터 덮으면 140청크, 90,000타일이면 1,407청크다.
- 청크 안의 경계도 기술자의 start/end 검사로 보존. 여러 겹·부분 덮어쓰기에서도 기존 효과부터 재생. 부분 소비 후 추가된 효과를 빠뜨리지 않는다.
- 깊이 32 또는 제외 청크는 그 청크의 대상 타일만 pending부터 반영한 뒤 기존 경로로 처리. 이전 효과를 대체하는 일은 Stop이 아니다. 폴백이 많으면 시작이 다시 O(N)이 될 수 있으며 `eager`로 드러난다.
- 프레임별 가시성은 TileCull 이벤트가 갱신한 청크 개수를 사용. 가시 청크·현재 타일 주변·shadow 표본만 반영. 비가시 청크 하나를 프레임마다 순환 처리하여 zero 효과가 끝난 뒤에도 남은 기술자를 결국 회수한다. 준비 이외에 전체 타일의 Transform/Renderer native 속성을 매 프레임 조사하지 않는다.
- shadow는 1/16 표본 + TileCull 기준 비가시가 아닌 타일에서 후보를 실행하고, 상태를 되돌린 뒤 원래 `FloorAnim.OpaOnly`로 비교한다. position/eulerAngles/localScale/opacity의 각 float 비트와 dontChangeMySprite를 비교한다. zero의 반영 시점에 검사하며 **양수 duration의 매 프레임 병행 shadow는 아직 없다.** 처음 8개 상세와 mismatch latch를 남기고 이후 새 후보를 거부한다.
- 처음 준비의 인덱스/배열/reader scan 비용은 O(전체 타일·관련 IL)이며 효과 시작에서 실행하지 않는다. 준비는 플래그가 켜졌을 때 이벤트 적용 완료 훅 또는 명시적 명령에서만 수행한다.

## 플래그·명령·해제

기본 꺼짐, DEV/AUTOTEST 전용. 일반 Player에는 파일 플래그·명령·연구 타입이 없다.

| 동작 | 의미 |
|---|---|
| 모드 폴더의 `tilerange-on` | 로드 시 연구 허용. `scnGame.ApplyEventsToFloors` postfix에서 맵 준비. 게임 파일은 이번 작업에서 생성하지 않았다. |
| `tilerange on` | 현재 맵에서 준비/reader 패치. shadow 끔. 재생 중 처음 준비하면 조사 비용이 크므로 맵 준비 뒤 정지 상태에서 사용. |
| `tilerange shadow` | 같은 후보 + 원래 경로 대조. 성능 판과 분리. |
| `tilerange off` | pending 반영, 맵 참조 해제, 연구 reader 패치 제거, assembly-load 구독 해제. |
| `tilerange flush` | 현재까지의 논리적 zero 쓰기를 모두 반영. |
| `tilerange status` | 누적 계측·거부 원인. 켬/끔 모두 큰 효과 종류의 집계는 남긴다. |
| Main.UninstallAll/Unload | `FloorRange.Shutdown` 호출. pending 반영 후 인덱스·청크·패치·이벤트 구독 해제. |
| FloorAnim.DropAll/장면 정리 | pending zero 쓰기를 먼저 반영하고 맵 참조 폐기. |
| 관련 어셈블리 추가 로드 | 메인 스레드면 즉시 pending 반영, 이후 재준비 전 새 후보 거부. 백그라운드 로드는 다음 메인 tick에서 반영하는 한계가 있다. |

## 계측

`[타일구간] start tiles=... ms=... deferred-chunks-total=... eager-tiles-total=...`

`status`: enabled/ready/shadow, 수락 effects/chunks, 즉시 처리 eager, pending 기술자, 실제 반영 apply, verify/mismatch/patch-fail. 큰 효과 수 large와 zero/duration/opa-only/pos/rot/scale 카운터. 채널별 수는 중복될 수 있어 단순 합계로 비율을 계산하면 안 된다.

QPC 누적: start-ms, enqueue-ms, eager-ms, start의 unaccounted-ms, pull-ms, tick-ms, verify-ms. `start - enqueue - eager`는 시작 경로의 미분류 시간이다. pull은 tick/flush/읽기에서 실행될 수 있고 verify는 pull 내부이므로 **세 수치를 독립 합산하지 않는다.** 외부 전체 `[타일 이동 효과]`/FloorAnim.StartMs와 실제 프레임 계측도 함께 본다.

모든 명시적 거부·청크 fence·깊이 초과·Materialize 사유·커버리지 실패·예외에는 이유 카운터를 둔다. 길이/다른 채널/간격/범위/작은 효과/준비 안 됨/Split/TileSim 캡처/assembly 변경 등도 구분한다. `tilerange status`의 카운터는 누적이므로 독립 프로세스의 판을 비교하거나 전후 차이를 계산한다.

## 빌드와 오프라인 확인

최종 명령·수치·DLL SHA는 `tilerange-evidence/validation-summary.json` 및 개별 로그 참조.

- 개발, Player, Player+AutoTest 빌드: 오류 0개. 기존 게임 DLL 참조 버전 충돌·deprecated/unused 경고가 남음. 새 타입이 일반 Player에 없는 것을 메타데이터로 확인.
- `LoadCheck`: 기존 FieldRefAccess 검증. runtime Harmony detour 성공 검증은 아니다.
- `TileRangeFixture`: production ledger 소스 연결, **128,767조건 통과**, 적용 7,418,592회. 1/63/64/65/127/128/129/1000/8171/8933/90000 타일, 경계/겹침/외부 쓰기/NaN/무한대/부호 0/32깊이/예외 재시도를 검사.
- `TileRangeIntegration`: production FloorRange와 ledger 연결, **69조건 통과**. Unity/FloorAnim/Harmony 등록은 모델이며 실제 엔진을 실행하지 않음. 준비/읽기·쓰기/사전·renderer 교체/기존 기록 완주/청크 폴백/캡처·Split 거부/해제를 검사.
- 두 fixture는 별도의 obj/bin을 사용하도록 고정. 중간에 같은 폴더의 obj/apphost 공유 때문에 잘못된 형제 실행 파일이 실행된 출력을 발견하여 버렸고, 분리 후 두 명령을 각각 다시 통과시켰다.
- 순수 청크 enqueue의 .NET 8 타이머 숫자는 게임의 0.05ms 목표 통과 근거로 사용하지 않는다.

## 남은 위험과 미구현

1. **양수 duration/Transform 구간 기술자, 공통 k 계산, 실제 매 프레임 병행 shadow 미구현.** 이전 효과의 Running/초기화/타일별 시작값과 proxy 관찰 계약을 먼저 가상 상태로 표현해야 한다. 직전 목표값만으로 대체하면 틀린다. 기존 FloorAnim/TileSim 경로는 유지한다.
2. **시작 함수만 빨라지고 프레임 스파이크가 남을 수 있다.** TileCull이 꺼져 있으면 전 청크를 가시로 취급한다. scrFloor의 직접 읽기나 다른 효과가 모든 타일을 읽어도 반영이 몰린다. 실제 프레임이 줄지 않으면 이 후보는 성능 목표 미달이다.
3. 준비 이전에 유출된 사전 별칭, reflection/unsafe 접근, 이미 인라인된 코드, 백그라운드에서 새로 로드된 모드가 즉시 읽는 경우는 이 직접 IL 참조 조사만으로 증명할 수 없다. 따라서 연구 기본 꺼짐이다. 게임 runtime reader 패치 실패가 1개라도 있으면 받지 않는다.
4. 그림/판정/곡 전체 비트 동일성·씬 전환·해제는 **실제 게임 미검증**. 관문은 mismatch 0, 비교 가능한 tilesig/decosig 차이 0, 전체 프레임/시작 프레임 개선이다. 전체 hash만으로 모든 픽셀의 동일성을 주장하지 않는다.
5. 큰 효과 종류의 **맵별 실제 비율은 아직 측정하지 않았다.** 인계의 HELLO 125.6초(길이 0, 불투명도만)와 Lost Requiem 531.8초(위치 사용)는 선택 근거이며 새 측정이 아니다. 새 census를 기준 판에서 수집해야 한다.
6. zero의 물리적 최종 쓰기는 보임·읽기·순환 sweep·flush/unload 때 수행한다. 종료 순간에 전수 쓰기를 강제하면 시작 스파이크를 같은 프레임으로 돌려놓으므로 그렇게 구현하지 않았다. 이는 원래 제안의 “효과 종료 즉시 전수 반영”과 다른 선택이다. 읽기 barrier의 완전성이 관문이다.

## 사용자가 게임에서 돌릴 순서

1. 개발 또는 AutoTest DLL을 별도로 설치한 후, 파일 플래그 없이 시작. 맵 준비·정지 상태에서 `tilerange off`, 기준 판에서 census/기존 시작 로그/전체 프레임·tilesig/decosig를 수집한다.
2. **HELLO 2026 125.6초**: 같은 시작 조건에서 `tilerange shadow`. ready=True, patch-fail=0 확인. 이벤트 뒤 `tilerange status`, 동일 음악 시각의 `tilesig hello-on`/`decosig hello-on` 대조. eager가 크거나 mismatch가 있으면 성능 판을 진행하지 말고 원인 카운터부터 확인한다.
3. 같은 맵의 **shadow 없는** 독립 끔/켬/켬/끔 판. `tilerange on`은 정지 상태에서 실행. 시작 로그뿐 아니라 해당 프레임 전체·후속 tick/pull 비용을 비교한다. 서명/검사/일괄 flush 프레임을 FPS 판에 섞지 않는다.
4. Lost Requiem 531초: 현재 위치 갈래는 기존 경로이므로 수락되지 않는 것이 예상값이다. census와 폴백 이유·서명 회귀만 검사. 22ms 개선을 기대하는 판으로 세지 않는다.
5. 합성 `C:\SFBundle\autotest\maps\h90000`: zero-opacity 대형 효과가 실제로 있는지 census부터 확인. 없다면 이 후보의 성능 검증으로 세지 않는다. 부분 겹침·64 경계·외부 appear/disappear·개별/전체 Kill·되감기·맵 전환·off/unload를 검사한다.
6. **9만 타일 맵**은 위 관문 후 사용자가 실행. 문서·코드·로그에서는 이 표기만 사용한다. 100만 타일 맵은 열지 않는다.

실제 게임 결과를 받기 전 채택/기본 켬/일반 Player 이식은 하지 않는다.
