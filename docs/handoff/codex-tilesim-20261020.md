# Codex 과제 T1: `tilesim` — 타일 애니메이션(`FloorAnim`·타일 색)을 표/배치 평가로 대체할 수 있는지 (장식 파이프라인의 타일 확장 1단계, 큰 덩어리)

작업 폴더 E:\StutterFixWork\wt-tilesim (브랜치 codex/night-tilesim, main 에서 갈라짐: `DecoSim.cs`(장식 테이프·SoA 배치 평가·verify·bench) 가 이미 있다). 규칙: 커밋·게임 실행·설치 금지, 새 코드는 `#if DEV || AUTOTEST`, 사용량 절약. 결과 문서 `docs/handoff/codex-tilesim-20261020-result.md`. **먼저 `docs/roadmap-deco-pipeline-20261020.md` 를 읽을 것(원칙: 미리 판정, 프레임당 전수 순회·문자열 API 금지, 모든 폴백 원인 카운터, 단계별 `Stopwatch` + `unaccounted-ms`, 게임 한 판에 원인이 다 보이게 자기진단).**

## 배경
장식은 "이벤트가 시작한 애니메이션 기록을 표/배치로 평가"(decosim 이 Arche 에서 비트 동일, 프레임당 0.5~0.8ms). 타일(`ffxMoveFloorPlus`, 색 바꾸기 `ffxChangeTrack`/`ColorFloor`, 투명도 등)은 이 모드가 이미 `FloorAnim`(DOTween 대신 직접 구동: 타일마다 트윈을 만들던 것을 표 방식으로) 으로 돌린다 — 타일 8~15천 개 맵(Lost Requiem 531.9초 타일 이동 효과 하나가 53~55ms, HELLO 125.6초 길이 0 이동 22ms)에서 큰 비용이었다. 그 외 이 모드 기능: `MeshWarm`(곡 중 새 타일 메시 미리 만들기), `RecolorDefer`(화면 밖 타일 색 바꾸기 미루기), `TileCull`(화면 밖 타일 렌더러 끄기).
질문: 타일 쪽에서 (a) 지금 비용이 프레임에서 어디에 몰려 있는가 (b) 장식과 같은 방식(기록 테이프 → SoA 배치 평가 → 값 비트 동일)이 가능한가 (c) 가능하다면 이득이 어디서 나는가(평가? 엔진 쓰기? 메시 갱신?).

## 만들 것 — 두 갈래를 한 번에
**1. `tilesim capture <초> / verify / bench`** (decosim 과 같은 구조·같은 규칙): 곡 재생 중 살아 있는 타일 애니메이션 기록(`FloorAnim` 의 Rec 류: 위치/회전/크기/불투명도/색 등 Key, 시작·끝·변화값, 지속, 이징, 대상 타일 인덱스, 진행 시간)의 초기 스냅샷과 이후 새 시작·끊김·완료·프레임 dt 를 테이프로 저장하고, 테이프만으로 SoA 슬롯(타일×Key)을 만들어 프레임마다 평가해 `FloorAnim` 이 낸 값(타일 1/N 표본)과 **비트 비교**(불일치 수·상세), 평가 비용(단일/병렬 `Parallel.For` 또는 job)을 잰다. 곡 중에만, 캡처 중에도 게임 동작을 바꾸지 않는다(읽기만, 훅은 dev). 같은 슬롯 재시작(대체)·완료·끊김 의미는 `FloorAnim` 코드를 읽어 맞춘다(장식에서 "대체를 Stop 으로 취급" 하는 실수를 했으니 대체가 정상 경로임을 처음부터 반영).
**2. 타일 비용 분해 조사**(코드 읽기 + 계측 명령): `tilecost begin/end` — 곡 중 프레임당 시간을 단계로 쪼갠다: (a) `FloorAnim` 갱신(Rec 평가), (b) 타일 Transform/렌더러 쓰기(위치·회전·크기·색), (c) `FloorMesh.UpdateAllRequired` 메시 갱신(타일 색 스타일 변경 시), (d) 타일 색 적용(`ColorFloor`, 머티리얼/스프라이트/메시 색), (e) `TileCull`/`RecolorDefer` 처리, (f) 그 밖. 모두 `Stopwatch`(QPC 틱)로 호출 횟수와 함께. 비용이 작은 단계도 정확히 0 이 아님을 확인(호출 비용이 지배하지 않게 샘플링 계측: 단계 경계에서만 측정). 분해 결과로 "표 대체"가 줄이는 몫이 어느 단계인지 결론.
**3. 렌더 구조 분석 문서**: 타일 렌더링이 어떻게 이뤄지는지(`FloorMesh` 의 메시/머티리얼/스타일(네온/글로우/텍스처), 타일 하나당 렌더러 수, 정렬 키·재질 인스턴스 구조, `TileCull`) 를 IL 스캔(`tools/ilscan`)·코드 읽기로 정리해, **장식처럼 그룹 메시로 그릴 수 있는지**(재질 인스턴스 ID 정렬 규칙 `docs/optimization-36.md` 끝 참고, 타일은 공유 재질일 수도 있음) 가능성·위험·예상 이득을 `docs/handoff/codex-tilesim-20261020-design.md` 에.

## 정확성·안전 기준
- `verify` 불일치는 0 이어야 하고, 0 이 아니면 원인(소유권 이동, 그룹 경로, `ZeroTween`/`FloorAnim` 특수 경로 등)을 코드에서 확인해 적는다.
- 새 Harmony 패치를 걸면 `Main.Unload` 에서 해제. Player 빌드에는 안 들어가게(`#if DEV || AUTOTEST`).

## 결과 문서에
명령 사용법, 구현 요약, 계측 항목, 빌드 결과, 설계 문서 요약, 게임에서 돌릴 순서(예: `open <Lost Requiem 또는 HELLO>`, `seek <초>`, `tilesim capture 4`, `tilesim verify`, `tilesim bench`(재생 정지 후), `tilecost begin/end`).
