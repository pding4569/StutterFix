# Codex 임무 T3b: 쓰기 훅이 매 프레임 모든 청크를 더럽히는 문제 고치기

2026-10-20. 작업 폴더 `E:\StutterFixWork\wt-tilerange` (브랜치 `codex/night-tilerange`, T3 커밋 위에서). 게임 실행·설치·커밋 금지(Claude 가 한다). 출력: `docs/handoff/codex-tilerange-3-20261020-result.md` + `-o` 파일.

## 게임에서 나온 결과 (Lost Requiem 531.8초, `tilerange shadow`)
- 쓰기 훅 53개 설치 성공(`patch-fail=0 patched=53`; Mono 에서 `Module.ResolveSignature` 가 안 돼 이름+인자 수+호출 규약으로 고침 — 이미 커밋 eb8e0c3).
- 그런데 **8초 동안 훅 198만 번 호출**(`hook-calls=1979657 hook-normal=1979657 dirty-marks=1979657`). 타일 8,171개 × 약 242프레임 = 매 프레임 모든 타일이 훅을 탄다. 그래서 모든 청크가 항상 dirty → `skipped-chunks=0 tiles-processed=8171`, 효과 시작 25.7ms(끔 기준과 같음).
- 안전망 `safety-ms=147`(57,373 표본), `hook-ms=173.8`(8초) — 훅이 계속 불려서 비용만 든다. shadow 불일치 `mismatch=0`, `late-changes=0`.

## 의심
`scrFloor.LateUpdate`(매 프레임 모든 타일에서 돎)는 IL 상 `lastPos`, `lastScale`, `opacityLastFrame`, `glowMultiplierLastFrame` 에 **매 프레임** 필드를 쓴다(`t3-scrfloor-il.txt` LateUpdate 마지막 줄들). 이것은 "지난 프레임 값 기록용" 장부이지 타일 상태 변경이 아니다. 우리 변환이 `opacity*`/`startPos`류 쓰기를 이름 패턴으로 잡다가 이 장부 쓰기까지 표시하고 있을 가능성이 크다. `Transform.set_position` 호출 1개도 `isTweening` 같은 조건 안쪽일 수 있다(이 경우 조건이 거짓이면 실행 안 되므로 정상).

## 해야 할 것
1. **사이트별 계수**: Rewrite 변환이 넣는 Mark 호출마다 사이트 id(메서드 이름+IL 오프셋+종류: transform-setter/field-write/필드명)를 부여하고, `tilerange status` 에 호출 수 상위 12개 사이트를 출력한다(기본 꺼짐 진단, 비용은 `Interlocked.Increment` 수준만; 해시 조회 금지 — 사이트마다 정적 배열 슬롯). 이게 있으면 내가 한 번 돌려 어느 사이트가 매 프레임 불리는지 안다.
2. **LateUpdate 장부 쓰기 제외**: 위 4개 필드 쓰기는 표시하지 않는다(장부이므로). 단 IL 에서 이 4개 필드가 다른 메서드에서 "타일 상태"의 의미로 읽히는지 확인해, 읽힌다면 제외하지 말고 이유를 문서에 적는다. `set_position` 이 실제 타일 위치를 바꾸는 호출이면 그 호출 직전 값과 비교해 **값이 바뀔 때만** 표시하는 방식(호출 직전 get 은 비용이 크니 `lastPos` 와 다를 때만 실행되는 분기 안쪽 호출이라면 그대로 표시)을 IL 근거와 함께 정한다.
3. 같은 방식으로 `scrFloor` 의 다른 매 프레임 메서드(`Update` 등)와 효과/특수 연출 훅 53개 전부에서 **매 프레임 무조건 실행되는 장부성 쓰기**가 더 있는지 `tools/ilscan` TILE_WRITERS 출력으로 확인하고, 있으면 같은 방식으로 정리. 각 판단의 IL 근거를 결과 문서 표에 적는다.
4. 안전망 비용: 목표 `safety-ms + hook-ms` 합이 **평균 0.15ms/프레임 이하**(8,171타일, 240FPS 기준 8초에 약 290ms 이하). 훅이 정상 호출만 남으면 대부분 해결될 것이다. 청크 검사 순환(프레임당 `ceil(청크 수/32)` 청크 × 최대 4타일)이 8,171타일(128청크)에서 프레임당 몇 µs 인지 계산해 문서에 적는다.
5. 오프라인 fixture(기존 70검사)에 "장부 쓰기는 dirty 를 만들지 않는다" 검사를 추가해 통과시킨다(`tools/tilerange-fixture`).
6. 빌드 3종 오류 0 + LoadCheck 0.

## 규칙
- Mono 에서 `Module.ResolveSignature`/`ResolveMethod(MetadataToken)` 류는 쓰지 않는다(이미 한 번 53개 전부 실패했다).
- 새 Harmony 패치는 정확히 하나만 고르고 실패하면 전부 해제(기존 방식 유지).
- 헤르츠 맵 이름·비공개 프로젝트 언급 금지. 100만 타일 맵 열기 금지.
- 결과 문서에 "내가 돌릴 순서"(Lost Requiem 531.8초 shadow → status 에서 사이트별 호출 상위 12개 → skipped-chunks 가 늘었는지 → 끔/켬 시작 시간)를 적는다.
- `-o` 파일은 10줄 이내 한국어 요약.
