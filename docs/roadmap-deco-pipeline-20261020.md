# 장식 파이프라인(그룹 메시 + 표 계산) 로드맵 — 2026-10-20

## 지금까지 확인된 것 (측정)
- 표 계산: Arche 4초(93만 비교) 비트 동일, 평가 프레임당 0.5~0.8ms (`decosim`).
- 그룹 메시로 그리기(`decorender`): 장식 2.7만 개 → 구간(run) 493개, 한 프레임에 그리는 구간 68개(지금 SpriteRenderer 약 1,800개), `compare` 최대 1~2/255.
- 순서 규칙: 같은 (layer, order) 안은 (렌더 큐, 거리 z, 재질 인스턴스 ID). 재질 빌려 쓰기로 해결. 꺼진 장식 자식 MeshRenderer 는 장벽이 아니다.
- 병목: 프레임마다 도는 **안전 검사**(PruneChanged 16.7%, AuditLateBarriers 10.6%, UniformsUnchanged 10.3%, Takeover 5.9%, Build 12.1% — PreCull 안 46%). 빌드 자체(수집·정점·업로드)는 약 2ms.
- 렌더 쪽 이득 상한(같은 장면 벤치): 전체 프레임 -1.0~-1.4ms(그룹 메시), 표 계산 대체 -1.8ms 추정. 현재 장식 처리 ≈ 4.1ms(갱신 2.6 + Late 1.5), 한 프레임 약 7.1ms.

## 원칙 (Codex 사용량 절약 + 시행착오 줄이기)
1. **"미리 다 판단한다"**: 맵을 열 때(`on`)의 정적 분석으로 끝낼 수 있는 검사는 런타임에서 하지 않는다. 런타임에는 ① 이벤트 훅(게임 함수 postfix) ② 전체 끄기 신호(`Stop`) ③ 시간 분할 안전망(프레임당 1/N)만.
2. **프레임당 전수 순회·문자열 API 금지**: 렌더러/재질/Transform 속성을 장식 수만큼 매 프레임 읽지 않는다. `Shader.PropertyToID`, 캐시, 이벤트.
3. **Codex 작업은 한 번에 크게, 명세에 예산을 쓴다**: 모든 과제에 (a) 단계별 `Stopwatch` 계측 + `unaccounted-ms`, (b) 모든 폴백/거부 경로 카운터와 원인 로그, (c) 정확성 `compare`/`verify` 훅을 처음부터 요구. (오프라인 검사 통과가 게임에서의 성능·정확성을 보장하지 않았다 — 게임 한 판에 원인이 다 보이도록 자기진단을 넣게 한다.)
4. **내가 하는 일은 자동화**: `E:\StutterFixWork\run_dr_suite.py` 한 번으로 on → compare×6 → 켬/끔 `fuse` A/B → profsamp 심볼 요약을 낸다(Claude 사용량도 절약).
5. 각 단계에 **관문(숫자)**: 통과 못 하면 원인 분석 후 우회(설계 변경), 접지 않는다.

## 단계
**A. 런타임 안전 검사 제거(진행 중, Codex S8)**: 이벤트 훅 + 라운드로빈. 관문: DecoRender 프레임 총합 ≤0.5ms, `fuse` 창 프레임이 끈 판 이상, compare ≤2/255.

**B. 열 때 정적 사전 판정 (Codex S9)**
- 이벤트 정적 분석: 레벨 이벤트(`MoveDecorations`, `SetObject`, `SetText`, `EmitParticle`, 사용자 정의 PACL2 이벤트 등)에서 장식별로 곡 중에 sprite/material/sortingOrder(깊이)/mask/hitbox/보임(disable)/부모가 바뀔 수 있는 장식을 찾아 적격에서 제외(`dynamic-sprite` 등 이유 카운터). 이벤트가 장식을 바꾸지 않는 속성(위치·회전·크기·색·불투명도·시차)만 쓰는 장식은 유지.
- 순서 충돌 정적 판정: 정렬 키마다 그려질 수 있는 비장식 렌더러(타일, 행성, 파티클…)가 있으면 키 제외(또는 그 렌더러가 항상 꺼져 있다고 증명되면 허용). 런타임 `Audit` 삭제.
- 재질 속성 고정: 빌린 재질은 우리가 소유 — 확인 코드 삭제(on 때 한 번만).
- 알려지지 않은 개입(다른 모드·편집기)은 훅으로 `Stop`.
관문: 안전 검사 코드의 프레임당 호출 0(훅 제외), compare 유지.

**C. 렌더러 대체 연결 (1차 이득)**: 가져간 장식의 SpriteRenderer 는 이미 `forceRenderingOff`. 이제 그 장식의 **Transform 3개 쓰기·`SpriteRenderer.color` 쓰기를 건너뛴다**(`TightLate.Apply`, `ColorFast`, `MoveApply.SetLocal` 의 해당 경로). 먼저 IL 스캔으로 가져간 장식의 Transform/SpriteRenderer 를 읽는 게임 코드(히트박스, 편집기 선택, `GetVisible`, `UpdateScreenClamp`, 시차)가 있는지 목록화해 읽는 곳이 있으면 최소 갱신(Transform 만)을 유지. 값은 `TightLate` 의 SoA(최종 TRS+색)에서 정점으로. 관문: `fuse` 에서 끈 판 대비 프레임당 -0.5ms 이상, compare ≤2/255.

**D. 계산 대체 (2차 이득)**: 가져간 장식의 `DecoAnim` 기록 평가·적용을 `decosim` 의 SoA 배치 평가로 대체(기록 생성은 게임 코드가 한 그대로, 시작값도 그대로 — 실제 시작값을 쓰는 정확한 의미 유지). 평가는 워커 스레드(`Parallel`/job), 결과가 정점 계산 입력. `DecoAnim.Apply/MoveApply/TightLate` 의 해당 장식 경로 건너뛰기. 관문: `decosim verify` 를 라이브(실제 값 대조)로 상시 샘플링 불일치 0, `fuse` 에서 -1ms 이상.

**E. 견고성**: 편집기(편집·재생·되감기·일시정지·배속), 다시 하기, 맵 전환(`Main.Unload`·`LeakGuard` 와 정리), 아틀라스 VRAM 예산(이미지 크기별), 안정판 게임 호환(`CompatCheck`), Quartz 공존, `Resilience` 자동 끄기(예외 시), 설정 UI(실험, 기본 끔), 개발자용 진단 최소화(Player 빌드 비용 0).

**F. 검증 세트**: Arche(장식 과다), HELLO 2026(타일), Windflower(이미지), 합성 맵: `compare` 최대/비율, 곡 전체 `decosig` 비교, FPS/1% low A/B(켬/끔 ABBA), 끊김 수, 메모리. 통과 기준을 문서화.

**G. 타일 확장(별도)**: `FloorAnim` 기록으로 같은 표 계산 + 타일 메시 처리(색·스타일 메시)의 비용 구조를 먼저 분석(`decosim` 같은 캡처·검증). 장식 파이프라인이 안정된 뒤.

## Codex 과제 묶음 계획 (사용량 절약: 큰 덩어리 4개)
1. S8(진행): 안전 검사 제거.
2. **S9**: B 정적 사전 판정 + 훅 정리 + 자기진단(런타임 폴백 원인 카운터 전부).
3. **S10**: C 렌더러 대체 연결 — 사전에 IL 스캔 목록(읽는 곳)을 Codex 가 먼저 만들고, 읽는 곳이 없으면 건너뛰기 구현, 있으면 최소 갱신 구현. `DecoRender` 가 값을 `TightLate` SoA 로 받는 경로.
4. **S11**: D 계산 대체 + E 견고성(Unload·편집기·재생 상태 머신·Resilience·설정 UI) 일부.
(그 사이 내 일: 게임 측정 자동화 스위트, 기본 on/off A/B, 문서/커밋.)
