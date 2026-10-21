# Gate 2 변형 A — 7회차: 프레임당 전체 스캔 제거 (Dormancy 의 깨움/잠듦을 live 집합 근원으로)

먼저 읽을 것: `docs/handoff/gate2-common.md`(공통 지시, 맨 끝 "긴급"·"성능 지침" 포함), `docs/handoff/ga-autoloop-ledger.md`(1~6회차 요약), `docs/handoff/codex-gate2-A-result.md`, 직전 증거 `docs/handoff/ga-autoloop-run*-evidence.txt` 최신. 게임 실행·설치·커밋·푸시 금지. 한국어로.

## 지금까지 (A 안, Arche 262→276초)
- 4회차: 소유 1,478개/구간 1,114개, 262초 픽셀 `ab_out=674 maxdiff=2`(기준 안정). 정점 계산 2.75ms+업로드 1.4ms/프레임으로 느렸음(지침 반영 중).
- 5·6회차 재생 FPS 시험: **켬 14.7~15.1 FPS(66~68ms), 끔 120~123 FPS(8.1~8.3ms)**, `PIXEL_FAIL outside_pixels_changed`. 원인: `[livedeco] cost` 의 **`scan_ms≈29.6`** — 전체 렌더러 **157,254개를 프레임마다 훑는** 소유 판정/레지스트리 스캔. 그리고 재생 중 소유가 1,478→**18개**로 줄어듦(스캔에서 admitted 되는 게 거의 없음, 원인 미확인). 6회차 Codex 가 "안전한 증분 갱신에 필요한 변경 감지 구조가 미구현"이라 멈췄다.

## 이번 임무: 변경 감지 구조 만들기
**이미 있는 구조를 쓴다: `Dormancy.cs`.** 모드는 이미 장식 28,835개 중 "잠든" 것(scrVisualDecoration 이고, 메시 꺼짐, `GetVisible()==false`, 히트박스 없음 등)을 빼고 **깨어 있는 장식만 담은 목록**(`Dormancy.AwakeList`, `Wake()`/`Sleep()` 이벤트: `scrVisualDecoration.SetVisible/ApplyColor/SetMaskingType/EnableMeshRenderer`, `scrDecoration.Awake/Setup` 후킹)을 유지한다(헤더 주석 참고). 깨어 있는 장식 ≈ 보이는/움직이는 live 집합이다.
1. **live 집합의 근원을 `Dormancy` 의 깨움/잠듦 이벤트로 바꿔라.** 프레임당 전체 렌더러 스캔(157k)은 **완전히 제거**하고, 장식이 깨어날 때/잠들 때(+ 맵 다시 열기/`ResetScene`/언로드)만 소유 후보 판정(정적 조건: 기본형·MPB·키 분할·child-parent 등은 "처음 깨울 때 한 번" 계산해 캐시)을 한다. 안전망(이미 Dormancy 에 있는 1초 감사 방식)으로 놓친 것을 세라.
2. **소유 집합 일관성**: 전환 때 `owned` 가 갑자기 18로 줄던 원인(스캔에서 매 프레임 다시 판정해 탈락? visibility_policy=bound-self 가 매 프레임 흔들림?)을 로그로 확인해 고쳐라. 목표: 재생 중 소유 수가 깨어 있는 이미지 장식 수(약 1,500~2,000)와 비슷하게 유지.
3. **프레임당 비용 표**: `cost` 줄에 `scan_ms`(이제 ≈0 이어야 함)·`reconcile_ms`·`vertex_ms`·`upload_ms`·`awake=`·`owned=`·`dirty=` 를 남겨라. 켬/끔 프레임 시간 차이를 FPS 시험으로 확인(목표: 켬 ≥ 끔의 +15%).
4. **픽셀**: 위 1~3 이 끝나면 `outside_pixels_changed` 를 구역별로 분석하라(4회차 674개/최대 2/255 수준까지 가는 것이 목표, 0 이 최종 목표). 차이 구역이 파티클/행성/텍스트처럼 소유 밖이면 비교 제외 영역으로 합당한지 근거를 적어라(제외는 사용자 승인 사항으로 보고만).
한 번에 다 하지 말고 1→2→3→4 순서로, 각 단계 증거 수치를 결과 문서에. 설계 한계에 막히면 구체 수치로 적고 멈춰라(동일 시험 되풀이 요청 금지).
