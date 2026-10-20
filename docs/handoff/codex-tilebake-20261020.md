# Codex 임무 tilebake: 타일 애니메이션 굽기 프로토타입 (그림자 모드)

2026-10-20. 작업 폴더 `E:\StutterFixWork\wt-tilebake` (브랜치 `codex/night-tilebake`, `codex/night-bake` 의 bake-2 위에서 분기). 게임 실행·설치·커밋 금지. 결과 `docs/handoff/codex-tilebake-20261020-result.md` + `-o` 파일(10줄).

## 목표와 이유
장식 굽기(`DecoBake.cs`, `DecoBake.Core.cs`, 곡 시각 기반 무상태 평가, 그림자 모드)와 같은 방식을 **타일 이동 효과**에 적용한다. 효과 시작 때 타일마다 기록(`FloorAnim` 의 Rec)을 만드는 비용이 스파이크 원인이다(Lost Requiem 531.8초 한 프레임 22~25ms, 타일 8,171개, 타일당 약 2.7µs; `docs/optimization-36.md` 의 `tilesim`/`tilecost` 절). 굽으면 시작 비용이 맵 열기로 옮겨진다. 사용자는 이 무상태 표를 **에디터 미리보기(임의 시각 점프·되감기)**에도 쓰려 한다 → 평가 API 가 `Eval(타일, 속성, 곡 시각)` 로 순수해야 한다(문서에 API 초안도).

## 이미 있는 것 (재사용)
- `FloorAnim.cs`: 모드가 직접 도는 타일 이동 효과(`ffxMoveFloorPlus`)의 의미. 이징·채널(위치 x/y, 회전, 크기 x/y, 불투명도)·교체(`Prev/Kill(true)`)·길이 0·OpaOnly 분기.
- `TileSim.cs`(개발용): 타일 테이프 캡처와 SoA 배치 평가 비트 검증(494,780 비교 불일치 0, 평가 단일 0.31ms/병렬 0.12ms).
- `DecoBake.Core.cs`: 구간 표 + 이진 탐색 평가기, 교체 규칙(상대 기준 → 이전 완료 → 새 시작값), 시간 의미(T0 = startTime−startEffectOffset, elapsed=(곡시각−T0)/pitch*timeScale). **같은 시간 의미를 쓴다**(사용자 승인: 곡 시각 기반, 시작 위상 차이 ≤ 1프레임 허용, 제품화 전 실측 재확인).
- `docs/handoff/codex-bake-20261020-result.md`, `codex-bake-2-20261020-result.md`: 읽을 지점(`ApplyEvent` 반환 시 `ffxPlusBase.startTime/startEffectOffset/duration`, `PrepVfx` 정렬 순서, `scrLevelMaker.CalculateFloorEntryTimes` 이후 `entryTime`).
- T3 쓰기 훅 목록(`wt-tilerange` 의 `FloorRangeWriterManifest.cs`, `docs/handoff/tilerange-evidence/t3b-game-writers.txt`): 타일 Transform/불투명도를 쓰는 게임·모드 메서드 53개. 이 쓰기 주체들이 있는 타일은 굽기에서 **제외**해야 한다(예: ffxFloorAppear/Disappear, 특수 연출).

## 할 일
1. `FloorBake.cs` + `FloorBake.Core.cs`(`#if DEV || AUTOTEST`, 기본 꺼짐, Player 제외, `Main.Unload` 되돌리기): 효과 확정 지점에서 `ffxMoveFloorPlus`(및 모드가 대체하는 불투명도 전용 변형)의 타일 범위·속성 목표·상대/절대·이징·길이·반복 전개 인스턴스를 읽어 타일×채널 구간 표를 굽는다. 타일 범위 열거는 게임 의미(`startTile..endTile`, 오프셋, 반복) 그대로. 교체 규칙·길이 0·OpaOnly 는 `FloorAnim.cs` 를 읽어 정확히 구현, 모르는 갈래는 구현하지 말고 **제외 사유**로 센다(그 타일 전체를 굽지 않음). 가능하면 `DecoBake.Core.cs` 평가기를 공유(복붙 최소), 불가하면 이유를 문서에.
2. 그림자 검증 명령 `tilebake on/off/ready/verify/bench`: `tilesim capture` 테이프의 프레임별 곡 시각(없으면 TileSim 에 기록 추가)과 굽기 평가를 대조. 오차는 비트 동일이 아니라 통계: 속성별 최대/p99/상위 8개 타일, 시작 위상 차이(프레임), 제외 사유별 타일 수·비율, 위치는 월드 단위와 화면 픽셀 환산. 눈에 안 띄는 기준 제안: 위치 0.01px, 시작 ≤ 1프레임.
3. `tilebake bench`: 굽기 시간·메모리(타일 8,171~9만, 효과 수천 개 규모 합성 포함하되 Lost Requiem 실측과 구분), 평가 시간, **효과 시작 비용이 사라지는지**를 추정할 근거(시작 때 하는 일이 표 인덱스 하나로 줄어드는지) 설명.
4. 에디터 미리보기용 API 초안(문서만): `Eval(tileIndex, channel, songTime)`, 임의 시각 일괄 평가(타일 범위), 표 재굽기 트리거(이벤트 편집 시 부분 재굽기). 구현 금지, 설계만.
5. 오프라인 fixture(`tools/tilebake-fixture` 신규 가능) : 교체 규칙 예, 길이 0, 상대 기준, NaN 축, 이징 3465개 중간값 대조(`DecoBake` fixture 의 방식 참고), 제외 사유.
6. 빌드 3종(`-c Release`; `-p:Edition=Player`; `-p:Edition=Player -p:AutoTestBuild=1`) 오류 0 + LoadCheck. 새 Harmony 패치는 최소·이름+인자 수+호출 규약으로 정확히 하나(**`Module.ResolveSignature` 금지**), 실패하면 설치 안 함.

## 규칙
- 기존 경로 변경 금지(그림자: 읽기+계산만). 소유권 이전·렌더 변경 금지.
- 헤르츠 맵 이름·비공개 프로젝트 언급 금지, **100만 타일 맵 열기 금지**(합성/추정만).
- "내가 돌릴 순서": Lost Requiem(`D:\얼불춤 맵 파일\Lost Requiem\Ludicin-Lost-Requiem.adofai`) `open` → `tilebake on` 재생 → `seek 524 8` → `tilesim capture 4` → `tilebake verify` → `tilebake bench`. 531.8초 효과 구간이 포함되게.
