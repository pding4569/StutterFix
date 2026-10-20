# Codex 과제 T3: 타일 대형 효과 시작 스파이크 — "이미 목표 상태인 청크는 O(1)로 건너뛰기"(쓰기 훅 기반) + 계측

작업 폴더 E:\StutterFixWork\wt-tilerange (브랜치 codex/night-tilerange; `FloorRange.cs`/`FloorRangeLedger.cs`(T2, 길이 0·불투명도 전용 청크 지연), `FloorAnim.cs`, `TileSim.cs`/`TileCost.cs`, `docs/handoff/gemini-sorting-and-tile-o1-20261020.md`, `codex-tilerange-20261020-result.md` 가 있다). 규칙: 커밋·게임 실행·설치 금지, 새 코드는 `#if DEV || AUTOTEST` 또는 기본 꺼진 연구 플래그(`tilerange-on` 파일 플래그, `Main.Unload` 정리, 모든 폴백 카운터), 사용량 절약, **프로파일러(`profsamp`)는 쓰지 말 것**. 결과 문서 `docs/handoff/codex-tilerange-2-20261020-result.md`. 로드맵 `docs/roadmap-deco-pipeline-20261020.md` 의 원칙(전수 순회·문자열 API·장식마다 사전 조회 금지, 대체는 정상 경로, 모든 폴백 원인 카운터, 단계별 `Stopwatch` + 진짜 `unaccounted`, 프레임당 전수 검사 금지) 준수.

## 게임 결과 (T2 를 돌려 본 것)
- HELLO 125.6초 타일 8,933개 불투명도 전용 효과의 시작 비용은 이미 **3.90ms**(예전 22ms 는 `FloorAnim` 개선으로 줄었다). T2 의 길이 0·불투명도 갈래로 더 얻을 몫은 ≤3.9ms.
- **T2 의 `tilerange shadow` 는 시작을 못 했다**: `ready=False patch-fail=18872` — 읽는 메서드를 모든 어셈블리에서 훅하려다 시스템·컴파일러(`Microsoft.CodeAnalysis.*`, `System.*`) 메서드까지 걸어 `AmbiguousMatchException` 1.9만 번, 준비 126초, 시험 중단. **읽는 곳(readers) 전부를 훅하는 방식을 버린다.**
- **진짜 스파이크**: Lost Requiem 531.8초 `[타일 이동 효과] 22.44ms 타일 8171개 길이 1.00 불투명도만 False | 위치 True 회전 True 크기 True 불투명도 True | 값 위치 (0.00, 0.00) 회전 0 크기 (1.00, 1.00)`. 즉 **목표 오프셋이 전부 0/기본**(원래 상태로 되돌리는 효과)이다. 8,171개를 타일마다 읽고(`transform`, `material`, `opacity`) 비교해 대부분 "이미 같은 값이라 건너뜀"이 되는 것으로 추정(타일당 약 2.7µs; 이전 조사: 같은 값이면 트윈을 안 만든다). HELLO 125.6초(불투명도 전용 3.9ms), 7777 맵의 즉시 이동(7777 타일, 10~20ms)도 비슷한 구조.

## 설계 (이번에 구현)
1. **청크 "순수(pristine)" 추적 — 쓰기 훅만**: 타일을 64개 청크로 나누고, 청크·채널(위치/회전/크기/불투명도)마다 `dirty`(기본 상태에서 벗어났을 수 있음) 플래그를 둔다. 맵 준비 때 모든 청크 clean. 타일 상태를 **쓰는** 경로(소수)에서만 해당 청크를 dirty 로: ① `FloorAnim`(이 모드의 효과 구동: `New`/`Complete`/즉시 쓰기 `Zero`/`OpaOnly` 등) ② 게임의 타일 transform/opacity 쓰기 메서드(IL 스캔 `tools/ilscan` 으로 `scrFloor.transform.position/rotation/localScale` 세터·`opacity` 필드·`startPos` 갱신 호출자 목록 → **게임/모드 어셈블리의 그 몇 개 메서드만** `HarmonyPatch`; 시스템·유니티·외부 어셈블리 금지, 패치 대상은 이름+서명으로 정확히 지정(`AmbiguousMatch` 방지: `AccessTools.DeclaredMethod` 오버로드 주의 → 기존 CLAUDE.md 교훈대로 `GetMethods` 로 이름별 순회하여 정확한 것만) ③ 다른 모드가 건드리는 경로(Quartz/PACL2 등)는 훅이 못 잡으므로 **청크 안전망**: 매 N프레임 청크 수(약 140)의 1/N 만 샘플 타일 몇 개의 실제 값을 읽어 base 와 다르면 dirty(비용 프레임당 몇 µs), 늦게 잡은 변화는 카운터.
2. **효과 시작에서 O(청크 수)**: 효과의 목표가 기본(오프셋 0, 회전 0, 크기 1, 불투명도 base)인 채널은 **청크가 clean 이면 그 청크 전체를 건너뛴다**(원래 코드가 그 타일들에서 "같은 값이라 건너뜀"으로 아무것도 안 쓰는 것과 동일해야 함 — 코드로 증명: `FloorAnim.TweenFloor`/`OpaOnly`/`Zero` 경로에서 값 비교 후 쓰기를 건너뛰는 조건을 정확히 따라 `Approximately` 비교 의미까지 일치). 청크가 dirty 이면 그 청크의 타일만 원래 경로로 처리. 목표가 기본이 아닌 효과는 원래 경로(이번 범위는 "되돌리기/기본 목표" 갈래).
3. **정확성 하네스(필수)**: `shadow`: 건너뛴 청크의 타일 1/16 표본을 원래 경로 결과와 비교(실제로 아무것도 안 쓰는지: 타일 transform/opacity 전후 비트 비교, `moveTweens` 변화 없음), 불일치 카운터·처음 몇 개 상세. 곡 전체 `tilesig` 로 켬/끔 서명 같음.
4. **계측**: 효과 시작마다 `[타일구간] start effects=… chunks=… skipped-chunks=… dirty-chunks=… tiles-processed=… start-ms=… original-ms-estimate=…`, 단계별 `Stopwatch`(청크 분류, 건너뛰기, 원래 경로 처리, 안전망 샘플), `unaccounted`, 안전망 늦게 잡은 변화 수, 훅 호출 횟수와 정상 처리/dirty 표시 횟수. 훅 설치 결과(대상 메서드 목록, 설치 성공/실패 수)를 `on` 로그에 — **패치 실패가 한 건이라도 있으면 켜지 말고 이유를 낸다**(T2 의 1.9만 실패 같은 일이 없게 대상 목록 크기를 로그: 기대치 수십 개 이하).
5. **교훈 반영**: 대체/덮어쓰기는 정상, 전수 순회·문자열·사전 조회는 장식·타일 수에 비례해 매 프레임 하지 말 것, 계측이 비용을 숨기지 않게(프레임 안 모든 경로 합산 `pipeline-ms`).

## 시험 맵 (내가 게임에서)
Lost Requiem 531초(주 대상), HELLO 2026 125.6초(불투명도 전용 3.9ms), 합성 `C:\SFBundle\autotest\maps\h90000`, 사용자의 "9만 타일 맵"(이름은 문서·코드·로그에 쓰지 말 것). 100만 타일 맵은 열지 않는다.

## 결과 문서에
쓰기 경로 목록(IL 스캔 결과: 메서드·역할), 건너뛰기 의미 증명(원래 코드와의 일치 근거), 훅 대상 목록과 해제 위치, 계측 항목, 빌드 결과, 알려진 위험·미구현, 게임에서 돌릴 순서(`tilerange off` 기준, `tilerange shadow` 스모크(ready/patch-fail=0 확인), 켠 판 시작 로그 비교, `tilesig` 비교, 안전망 카운터).
