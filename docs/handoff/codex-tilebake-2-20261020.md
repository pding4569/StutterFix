# Codex 임무 tilebake-2: 타일 크기(scale) 지원 + 타일 지원률 오프라인 추정

2026-10-20. 작업 폴더 `E:\StutterFixWork\wt-tilebake` (브랜치 `codex/night-tilebake`, tilebake 커밋 96b4362 위). 게임 실행·설치·커밋 금지. 결과 `docs/handoff/codex-tilebake-2-20261020-result.md` + `-o`(10줄).

## 문제
tilebake 는 **크기(scale) 변경 효과가 있는 타일을 전체 제외**한다(지연 vector Startup 과 `Mathf.Approximately` XY 결합 때문). 그런데 목표인 **Lost Requiem 531.8초 효과(타일 8,171개, 길이 1.00, 위치 (0,0)·회전 0·크기 (1,1)·불투명도 모두 목표 = "기준값으로 되돌리기")는 크기 목표를 포함**한다. 즉 지금 상태로는 22~25ms 스파이크의 바로 그 효과가 굽기에서 빠진다. 크기 지원이 필수다.

## 해야 할 것
1. **타일 크기 의미 확정(코드 읽기)**: `FloorAnim.cs`(모드가 대체한 의미)와 `ffxMoveFloorPlus.StartEffect`/`Decode`, `scrFloor` 의 localScale 처리, `ApproximatelyXY`/`WithX`/`WithY`(게임 확장 메서드)를 읽어 (a) 크기 목표 X/Y 의 NaN 축(미사용 축), (b) 시작값을 언제·무엇에서 읽는지(첫 Step 지연 vs 시작 시점), (c) 축별 비교·쓰기 생략 규칙(현재값이 목표와 Approximately 이면 트윈을 만들지 않는 갈래; 이 생략이 결과 값에 영향을 주는지), (d) 교체 시 이전 크기 트랙 완료/Kill(true) 와 새 시작값 관계, (e) 길이 0 처리를 확정. 장식 쪽 구현(`DecoBake` 의 scale 채널, bake-3 결과 `docs/handoff/codex-bake-3-20261020-result.md`)을 참고하되 **타일은 별개 코드**이니 근거는 타일 코드에서.
2. 구현: `FloorBake.Core.cs`/`FloorBake.cs` 에 크기 X/Y 트랙 지원. 곡 시각 기반 무상태 평가, 이징 재사용. 모르는 갈래는 구현하지 말고 그 타일 전체를 **제외 사유**로 센다(사유 문자열 구분). `tilebake verify` 에 크기 오차 통계(상대 오차, 타일 크기 곱한 픽셀 환산 가능하면) 추가. 오프라인 fixture(`tools/tilebake-fixture`)에 크기 교체·NaN 축·길이 0·"기준값으로 되돌리기" 예제(531.8초 효과 형태) 추가, 크기 이징 중간값 3,000개 이상 대조.
3. **오프라인 지원률 추정 도구** `tools/optimization/tilebake-support-estimate.py`(읽기 전용): `D:\얼불춤 맵 파일\Lost Requiem\Ludicin-Lost-Requiem.adofai`(정확한 파일), 그리고 가능하면 HELLO 2026 의 `D:\얼불춤 맵 파일\HELLO (BPM) 2026\level.adofai` 를 읽어 타일별 제외 사유(다른 writer 효과 FloorAppear/Disappear, 조건/수동, 크기, 회전 wrap, 자유 이동, 이징 종류 등)를 이벤트 파일만으로 추정해 사유별 **고유 타일 수/비율**, 후보 비율, 531.8초 효과 형태(전체 되돌리기)가 굽기 대상에 들어가는지를 표로. 이벤트 파일로 판정 불가한 항목은 "미정" 칸으로 분리. 지원률이 낮으면 다음에 풀 사유 상위 5개를 코드 근거와 함께 제안.
4. 빌드 3종(`-c Release`; `-p:Edition=Player`; `-p:Edition=Player -p:AutoTestBuild=1`) 오류 0 + LoadCheck. Player 제외 유지.

## 규칙
- 그림자 모드 유지(기존 경로 변경 금지). `Module.ResolveSignature` 금지. 새 Harmony 패치 없이 기존 후크 재사용.
- 헤르츠 맵 이름·비공개 프로젝트 언급 금지, 100만 타일 맵 열기 금지.
- 결과 문서에 "내가 돌릴 순서"(기존 tilebake 순서 유지 + 크기 지표)와 오프라인 지원률 표를 넣는다.
