# Codex 작업 B2: GroupStep v2 — 위치 쌍 합치기를 되살리고 묶음 전체를 얇은 루프로 — 게임 실행·Mods 설치·커밋 금지

선행 결과: `docs/handoff/codex-groupstep-20261019-result.md`(v1, `DecoAnim.GroupStep.cs`). 결과: `docs/handoff/codex-groupstep2-20261019-result.md` (**apply_patch 로 UTF-8 BOM, 쓴 뒤 한글 확인**), 한국어, 추측은 "추정". 빌드 셋 오류 0. 켜는 방법: 모드 폴더 `groupstep-on` 파일(현재 기본 끔으로 바꿔 둠 — v2 가 A/B 에서 이득이면 Claude 가 기본값을 바꾼다), 검증 명령 `groupstep verify` 유지.

## v1 실측 결과 (Claude, Arche 곡 257.8~278.8초 21초 창, 2560×1440 120Hz, 판마다 새 프로세스, TightLate 켬 상태)

- 비트 검증 525,634번 불일치 0 — 정확성은 됨. 묶음 통계: 묶음 크기 5 → 530만, 3 → 160만, 2 → 114만 (키 [1,2,5,7,8] 묶음 83,197·[1,2,5] 24,205·[1,2] 16,995 …), 폴백: 키 없음(색 9·10 등) 624만, 단독 58만.
- 그러나 A/B: `DecoAnim` 갱신 **끔 1.87 → 켬 2.29ms/프레임**, 프레임 3,217 → 3,068개(−4.6%). 원인(추정, 일치함): v1 은 묶음 안에서 위치 X·Y 를 각각 `SetPositionX/Y`(→ 게임 `SetPosition` 체인)로 불러 기존 `StepPair`(X·Y 를 한 번의 `setPos`)가 아끼던 것(장식당 약 0.4~0.5µs, 프레임당 약 0.4ms)을 잃었다.
- 기준: PairXY(위치 쌍)는 270초 구간 DecoAnim 3.6 → 3.2ms 를 줄였다. 모드 `DecoAnim` 은 현재(끔 상태) 1.87ms/프레임이며 프레임당 기록 약 4,500개 → 기록당 약 0.41µs.
- 같은 세션 교훈(TightLate 성공): 장식당 관리 코드를 **정적 값 캐시 + 얇은 루프**로 바꾸면 비용이 1µs → 0.3µs. 엔진 Transform 쓰기는 장식당 약 0.1µs 로 싸다(3개 setter 2,150개 0.2ms). 비싼 것은 호출 사슬·조회·검사 같은 관리 코드다.

## 할 일

1. **위치는 묶음 안에서도 X·Y 를 한 번의 `setPos`(StepPair 와 같은 방식)로**: 묶음에 키 1·2 가 모두 있고 (r.D 같음) 같은 (Pos, Dur, Ease)이면 `StepPair` 의 합치기 로직(`x = dr ? r.FEnd : r.FLast`, `y = dn ? n.FEnd : n.FLast`, `setPos(r.D, new Vector2(x,y), pivotOffRef(D))`, 완료 처리)을 그대로. 한 축 예외 시 기록 단위로 풀어 개별 처리(v1 의 폴백 방식 유지). 위치 쌍 이후 회전·크기 순서는 원래 기록 순서(회전 → 크기X → 크기Y).
2. **묶음 처리 중 불필요한 관리 비용 제거(TightLate 식)**: 묶음마다 반복되는 검사(장식 참조 같음, Started, Running, Pos/Dur/Over/Period 비트, Ease)를 가능하면 묶음을 만들 때(효과 시작, `NewRec`/FastMove)만 하고 프레임마다는 가벼운 확인(예: 묶음 헤더의 버전/카운터)으로. 예를 들어 **효과 시작 때 같은 이벤트가 한 장식에 만든 기록들을 `Group` 객체(구조체 배열 슬롯)로 묶어 `recs` 대신 그룹 목록을 순회**하는 방법(기록이 중간에 Kill(true)/완료/교체되면 그룹이 깨지는 모든 경로 — `KillPrefix`, `CompletePrefix`, `KillAllPrefix`, `Complete`, `NewRec` 의 재사용, `Compact` — 를 코드로 전수). 순회 순서(만든 순서)와 부수 효과 순서는 원래와 같아야 한다. 이 변경이 너무 크면 하지 말고 v1 방식(프레임마다 검사)을 유지하되 검사를 정수 비트 비교 몇 개로 최소화.
3. **마이크로 벤치로 예산 확인 먼저**: TightLate 의 `tightlate bench`(`TightLate.cs`)처럼 더미 장식(GetUninitializedObject + 더미 Transform)과 더미 `Rec` 로 5개 묶음 2,150 그룹/프레임을 흉내 내는 `groupstep bench` 를 만들어 **묶음당 µs**(엔진 설정 제외·포함 둘 다)를 재서 결과 문서에 적어라. 예산: 5개 묶음 한 번에 엔진 설정(위치 1회 + 회전·크기는 필드 저장) 포함 약 1.0µs 이하(현재 기록 5개 × 0.41µs = 2.05µs 가 기준선). 안 되면 원인을 찾아 줄여라(필드 읽기 수, 델리게이트 호출, try/catch 영역, 박싱, 람다/클로저 할당).
4. **비교 검증**: `groupstep verify` 가 v2 에서도 1/64 로 개별 `Step`/`StepPair` 경로와 비트 비교(키별 값 + 최종 필드), 위치 쌍은 `setPos` 입력(x,y,off) 비교. 그리고 `Edition.Dev`/`Prof` 에서는 끄는 현재 방식 유지.
5. 결과 문서에 예상 이득(식)과 Claude 가 돌릴 순서(`groupstep-on` 파일 켬/끔 새 프로세스 ABBA, `fuse begin/end` 로 DecoAnim 갱신 ms, `groupstep verify`, 곡 끝 `decosig` 대조)를 적어라.
