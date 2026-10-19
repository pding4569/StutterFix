# Codex 작업 J: DecoTrack 조사 — "장식 이동 수천 개를 하나로" 새 효과 + 맵 열 때 자동 변환의 가능성 (조사·설계만, 코드 변경 금지) — 게임 실행·Mods 설치·커밋 금지

결과: `docs/handoff/codex-decotrack-research-20261019-result.md` (**apply_patch 로 UTF-8 BOM, 쓴 뒤 `Get-Content -Encoding UTF8` 로 한글 확인**), 한국어, 추측은 "추정". 분석 스크립트는 `tools/decotrack-research/`(Python, 읽기 전용)에 둬도 좋다. 소스(.cs)·csproj 는 건드리지 말 것(다른 Codex 작업 DecoEngine 이 진행 중).

## 사용자 목표 (2026-10-19)
1. 장식 이동 수천 개(Arche: MoveDecorations 9.5만 개, 동시에 약 3000개 처리)를 **하나로 처리해서 극단적으로 싸게** 만드는 **새 효과**를 만든다.
2. 맵이 구 `MoveDecorations` 를 쓰면 **맵을 여는 시점에 새 효과로 자동 대체(굽기)** 해서 열기도 빠르고 프레임도 훨씬 좋아지게.
3. 가능하면 **에디터의 맵 효과 칸**(하단 이벤트 추가 칸)에서 새 효과를 직접 쓸 수 있게.

## 조사 항목
A. **실제 맵 구조** (`D:\얼불춤 맵 파일\Arche\[𝐃𝐎 𝐍𝐎𝐓 𝐎𝐏𝐄𝐍 𝐓𝐇𝐈𝐒 𝐀𝐓 𝐇𝐎𝐌𝐄] FULL.adofai` 42MB, HELLO 2026, Windflower; 맵 이름은 외부 공개 금지 — 문서는 레포 내부용): .adofai(JSON, BOM/후행 쉼표 허용 파서 주의)의 MoveDecorations 이벤트를 분석:
   - 이벤트 수, 태그(`tag`)당 장식 수, 이벤트당 영향 장식 수 분포, 같은 장식이 받는 이벤트 수 분포, 이벤트 간격(floor/angleOffset 시간), duration·ease 분포, `positionOffset`/`rotationOffset`/`scale`/`opacity`/`color`/`pivot`/`parallax` 중 실제 쓰는 속성 조합, `relativeTo` 모드(Tile/Camera/Global/LastPosition 등) 분포, `eventTag`·반복(`RepeatEvents`)·`SetFilter` 같은 연관 이벤트와의 결합, 랜덤(`rotationOffsetRandom` 등) 사용 여부.
   - **굽기(bake) 가능 비율**: 이벤트를 "장식 하나의 시간 구간 목록(시작 시각, 길이, ease, 시작→끝)"으로 정적으로 풀 수 있는 비율. 풀 수 없는 이유(시작값이 이전 효과 결과에 의존하는 상대 모드, 랜덤, 시간이 재생 중 판정에 의존, 이벤트 겹침/Kill 의미 등) 분류.
   - 압축 후보 형태 비교: (1) 장식별 키프레임 트랙(정렬된 구간 배열 + 커서), (2) 같은 (시각, 길이, ease)를 공유하는 장식들의 묶음 한 항목(= "이벤트 하나가 N개 장식") 그대로 유지한 구조, (3) 수식 패턴(오프셋 등차). 각 형태의 메모리, 프레임당 비용 추정(장식당), 맵 열기에서 만들기 비용 추정.
B. **게임 쪽 이벤트 시스템** (`tools/ilscan`, `FIELDS=1`, 게임 Managed DLL 은 CLAUDE.md 경로): 새 이벤트 종류를 넣을 수 있는지: `LevelEventType` enum, `GlobalArray`/`LevelEventInfo` 표(이벤트 속성 정의, 에디터 설정 패널 생성 `PropertiesPanel`/`scnEditor` 이벤트 추가 칸 `EventButton`/탭 `LevelEventCategory`), 이벤트 시작(`scrDecorationManager`/`ffxPlusBase`/`scnGame` 이벤트 실행: `ApplyEvent`/`scrLevelMaker`), 저장·불러오기(`LevelData` JSON 의 알 수 없는 이벤트 처리: 모드 없는 사람이 열면 무엇이 일어나는지), 로컬라이즈(`RDString`). 가능하면 **기존 이벤트(예: MoveDecorations)를 확장/복제하는 가장 작은 방법**과 **모드 없이 맵을 열었을 때의 호환 대책**(새 이벤트를 쓰되 파일에는 구 MoveDecorations 로도 저장하는 이중 표현 등)을 제안. 코드 변경 금지, 후보와 위험만.
C. **자동 변환 의미 보존**: 굽기 변환이 현재 `DecoAnim`/`FastMove` 결과와 **비트 동일**하려면 무엇을 지켜야 하나(시작값 읽기 시점 `Startup`, Kill/Complete/겹침, 같은 프레임 여러 이벤트 순서, `GetVisible` 변화, `ExitFix` 가벼운 다시 설정, 에디터 되돌아가기 재생). 현재 코드(`FastMove.cs`, `DecoAnim*.cs`, `MoveApply.cs`)를 읽고, DecoEngine(`docs/handoff/codex-decoengine-20261019.md`)과 어떻게 합쳐지는지(DecoEngine 이 장식 단위 엔진이므로 굽기 결과를 그 엔진의 구간 배열로 공급하는 구조 제안).
D. **단계 계획**: 위험·이득이 큰 순으로 3~4단계 로드맵(각 단계 예상 이득 식, 검증 방법: 비트 비교, 곡 끝 `decosig`, 맵 열기 시간), 그리고 사용자에게 물어야 할 결정 사항 목록(모드 필수 맵 허용 여부 등).

## 추가 단서 (사용자 제보): PACL2 에 에디터 새 효과 추가 선례가 있다
다른 모드 **PACL2**(`D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\ModsOff\PACL2\PACL2.dll`, 지금은 ModsOff 에 꺼져 있음. 에셋 `win\PACL2_assets.bundle`)가 에디터 이벤트 칸에 새 효과를 넣은 것으로 보인다(글자 장식 `_textDecorationsOnPlay`·`VariableStateManager`·`FollowDecoration`/`OrbitDecoration` 등 — CLAUDE.md 참고). B 항목에서 **PACL2 가 새 이벤트 종류를 어떻게 등록했는지**를 IL(`ILScan`, 이 DLL 직접; 필요하면 `mcp ilspy` 디컴파일 대신 `tools/ilscan`)로 조사해라: `LevelEventType`/`GlobalArray` 표에 어떻게 끼웠는지(Harmony 패치 대상 메서드), 속성 정의, 에디터 패널·버튼·아이콘·로컬라이즈, 저장/불러오기, 모드 없이 열었을 때의 동작(PACL2 맵을 모드 없이 열면 무엇이 일어나는가). 그 방식을 그대로 따르는 설계를 우선 제안. PACL2 코드는 읽기만 하고 복사·재배포하지 말 것(구조 파악용, 이름·방식만 기록).

## 주의
- 이 판단은 조사 결과로 한다: 굽기 가능 비율이 낮거나 이득이 작으면 그렇게 쓸 것(과장 금지). 숫자는 실제 맵에서 센 값만 사실로, 나머지는 추정.
- 맵 JSON 분석은 읽기 전용. 큰 파일은 스트리밍/한 번 파싱 후 요약만 문서에 남기고 거대한 중간 파일은 `tools/decotrack-research/out/` 에 두되 커밋 대상 크기(수 MB)를 넘기지 말 것.
