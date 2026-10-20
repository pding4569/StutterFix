# Codex 과제 T2: 타일 대형 효과 시작 스파이크(22~26ms) 제거 — 구간 기술자 + 청크 지연 평가 (검증 우선, 큰 덩어리)

작업 폴더 E:\StutterFixWork\wt-tilerange (브랜치 codex/night-tilerange, main 에서 갈라짐: `FloorAnim.cs`, `TileSim.cs`/`TileCost.cs`(타일 테이프·검증·비용 분해), `TileCull.cs`, `RecolorDefer.cs`, `MeshWarm.cs` 가 있다). 규칙: 커밋·게임 실행·설치 금지, 새 코드는 `#if DEV || AUTOTEST` 또는 **기본 꺼진 연구 플래그**(일반 Player 에 코드가 들어가면 기본 꺼짐 + 설정/파일 플래그 `tilerange-on`, Main.Unload 정리, 모든 폴백 카운터), 사용량 절약. 결과 문서 `docs/handoff/codex-tilerange-20261020-result.md`. **먼저 `docs/roadmap-deco-pipeline-20261020.md`(원칙)와 사용자 제공 설계 제안 `docs/handoff/gemini-sorting-and-tile-o1-20261020.md` 의 2절을 읽는다. 제안은 외부 모델이 쓴 것이라 코드와 대조해 사실부터 검증할 것: 틀린 전제가 있으면 그것을 고쳐서 구현한다(아래 확인 목록).**

## 문제 (측정)
Lost Requiem 531.8초 `[타일 이동 효과] 22.44ms 타일 8171개`(한 프레임), HELLO 125.6초(8,933타일, 길이 0 이동 22ms·불투명도만 효과) — `FloorAnim.Run` 이 `start..end` 타일을 즉시 순회해 타일마다 `Prev` 조회·`transform` 읽기·`Rec` 생성(타일당 약 2.7µs). 표 평가(`tilesim`)는 시작 비용을 못 줄인다(시작은 프레임당 26개로 작음; 문제는 한 프레임에 수천 개). 이게 한 번 터지는 끊김이다 — 목표는 **효과 시작 비용을 타일 수와 무관하게 거의 O(1)로**, 단 **값은 비트 동일**.

## 제안 설계 요약 (검증 대상)
한 효과 안의 모든 타일은 `Dur/Ease/Over/Period` 와 목표 오프셋(`Pos/Rot/Scale/Opacity`)이 같으므로, 타일별 `Rec` 대신 **`RangeEffect` 1개(구간·파라미터)**를 만들고 타일을 64개 청크로 묶어 청크에 효과 ID 만 기록, 이징 비율 `k` 는 효과당 프레임에 1번, 청크 단위 지연 반영(카메라에 보이는 청크 + 플레이어 주변만 갱신, 나머지는 보이거나 외부 코드가 만지기 직전(Materialize)에 반영). 이전 효과를 덮을 때 시작값은 `Prev` 가 `Kill(true)`(완주)하므로 "직전 효과의 목표값"이라 청크마다 (직전 목표 오프셋, 현재 효과 ID) 두 값만 기억하면 된다.

## 먼저 확인할 것 (코드로) — 결과 문서의 표로
1. 한 효과의 모든 타일이 정말 같은 `Dur/Ease/목표 오프셋` 인가? **타일별 지연/간격(`gapLength` 등), 거리/인덱스 의존 오프셋, `eventTag`, 타일별 `Prev` 결과 차이(예: 타일마다 이전 효과 유무·상태가 다름), `freeroamArea`** 가 있는가. 같지 않은 요소는 청크 기술자에 어떻게 넣을지(타일 인덱스의 닫힌식으로 가능한지) 또는 그 효과는 원래 경로 폴백.
2. `Prev` 의 `Kill(true)`/`Complete(r)` 의미(`FloorAnim.cs` ~220~260행)가 제안 설명과 같은가: 이전 진행 중 트윈이 끊길 때 완주값이 새 시작값이 되는가, 아니면 현재값에서 시작하는 경로도 있는가(`Running`, `active` 상태별).
3. **타일 상태를 읽는 코드**(재생 중): `scrFloor.transform.position/rotation/localScale`, `opacity`, `startPos`, `moveTweens`, 히트/플래닛 위치 계산(`scrPlanet`·`scrController` 가 현재·다음 타일 위치를 읽음), 장식·홀드가 타일 불투명도를 읽는 곳, `TileCull`/`RecolorDefer`/`MeshWarm`/`ColorFloor`. IL 스캔(`tools/ilscan`)으로 목록 → 각각 "청크 지연 반영(Materialize) 전에 읽혀도 되는가"를 판정하고, 안 되는 읽기 지점에는 Materialize 훅.
4. 효과 종류별 비율: 대형 효과(≥1000 타일)가 `ffxMoveFloorPlus` 의 어떤 갈래(`OpaOnly`, `PosUsed`, 회전/크기, `Zero`(길이 0))인가. 이 맵들에서 이득이 큰 갈래부터.

## 구현 (확인 결과에 따라)
- `FloorRange`(새 파일, 연구 플래그): `StartEffect` 에서 ≥N(기본 1000) 타일이고 균일 조건을 만족하면 `RangeEffect` + 청크 기록으로 O(청크 수) 시작; 균일하지 않으면 원래 경로. 청크 안 타일 값 계산은 **원래 식 그대로**(`ZeroTween.Eval` 등 같은 식 모양, Mono float 의미)로 지연 반영.
- 반영 시점: 카메라에 보이는 청크·플레이어 현재 타일 주변 청크는 프레임마다, 그 외는 외부 읽기 직전(훅)·보이게 되는 순간·효과 종료 시점. 효과가 끝나면 최종값을 한 번에 기록.
- 장식 파이프라인에서 겪은 교훈을 처음부터 반영: **전수 순회·문자열 API 금지, 대체/덮어쓰기는 정상 경로(Stop 아님), 모든 폴백·Materialize 원인 카운터, 단계별 `Stopwatch` + `unaccounted-ms`, 위반 시 그 청크/타일만 원래 경로로(전체 Stop 최소)**.
- **검증 하네스(필수)**: 기존 `FloorAnim` 을 병행 실행하는 shadow 모드(타일 1/16 표본 + 가시 타일 전수)로 매 프레임 `position/eulerAngles/localScale/opacity` 를 비트 비교, 불일치 수·처음 몇 개 상세. 곡 전체 `tilesig`(`tilesig-*.txt`)·`decosig` 비교 가능하게. 효과 시작 프레임 시간(`[타일 이동 효과] ms`)을 켬/끔 비교 로그로.

## 시험 맵 (게임에서 내가 돌림)
Lost Requiem 531초, HELLO 2026 125.6초, 합성 `C:\SFBundle\autotest\maps\h90000`, 그리고 사용자의 "9만 타일 맵"(이름은 어떤 문서·코드·로그에도 쓰지 말 것: "9만 타일 맵"으로만 표기; 100만 타일 맵은 열지 않는다).

## 결과 문서에
확인 목록 표(제안의 맞고 틀린 점), 구현 요약, 플래그/훅 목록과 해제 위치, 계측 항목, 빌드 결과, 알려진 위험·미구현, 게임에서 돌릴 순서.
