# gpudeco 2단계 (G2): GPU 렌더러를 게임에 연결하고 재생 중 FPS·안정성을 확인한다

게임 실행·설치·커밋·푸시 금지(자동 루프가 한다). `.codex/config.toml` 수정 금지. 장식·맵 이름은 "Arche" 만. 한국어로.
전제: G1(픽셀)이 통과했다(`GPUDECO_G1=PASS`). 먼저 읽을 것: `docs/handoff/codex-gpudeco-1-result.md`, `docs/handoff/codex-gpudeco-lab-result.md`, `docs/handoff/gg-autoloop-ledger.md`, 직전 증거 `docs/handoff/gg-autoloop-run*-evidence.txt` 최신, `docs/optimization-36.md` 끝 절(livedeco gate 2 결과: 켬 51~56 FPS 로 실패했던 이유와 비용 분해).

## 목표 (측정으로 판정)
Arche 262→276초 재생(일시정지 없음)에서 `gpudeco on` 이 끔보다 **평균 FPS +25% 이상**(끔 약 124 FPS → 155 이상)이고 프레임당 CPU 합계(`[gpudeco] cost`: 진행값 갱신·유니폼·디스패치·기타)가 **≤0.5ms**. 곡 중 새 끊김(프레임 시간 30ms 초과) 0. GPUDECO_G2=PASS 판정은 위 둘을 모두 만족하고 안정성 항목이 통과일 때.

## 구현 요구
1. 소유 장식을 SpriteRenderer 대신 GPU 구동 구간 렌더러(정렬 경계로 나눈 렌더러 약 80개 + 서브메시)로 그린다(1단계에서 정한 설계 그대로). Transform/SpriteRenderer 쓰기·원본 트윈 평가는 소유 장식에서 막는다(`BakeLive` 소유 규칙). 소유 집합은 `Dormancy` 이벤트로만 증분 갱신(프레임당 전체 스캔·쓰기 훅 금지).
2. **켜기 준비는 재생 시작 전에 끝낸다**(곡 도중 수 초 멈추는 설계 금지). 맵을 연 직후 bake·표·메시·버퍼를 만들고 `gpudeco on` 은 일시정지/재생 시작 전 상태에서 적용. 준비 시간은 `[gpudeco] prepare_ms`.
3. 프레임당 CPU: 소스별 진행값 갱신(한 번의 `SetData`), 카메라 값, 디스패치 1(+렌더러별 복사 dispatch 약 80). 로그 `[gpudeco] cost` 에 항목별 ms.
4. 안정성(통과 필수): ① 켠 채로 곡 끝/죽고 다시 하기/재시작/에디터 복귀/맵 다시 열기/`Main.Unload`/모드 다시 불러오기에서 렌더러 복원(`forceRenderingOff` 해제)·버퍼·메시·재질 해제, 오류 0, 누수(힙·GraphicsBuffer 수) 증가 없음 ② 켠 채 일시정지·재개 ③ `sf_errors`(모드 오류 집계) 0.

## 시험 steps 생성기 `tools/optimization/gpudeco-perf-steps.py --chart <Arche *FULL.adofai> --output x.json`
`gpudeco-steps.py`·`decofreeze-steps.py`(ABBA 방식, 14초 창 262→276, `fuse begin/end`) 참고. 한 게임에서 **끔·켬·켬·끔** 창(켠 직후 3초는 평균에서 제외하되 `transition_ms` 로 별도 보고) + 끝나고 안정성 시나리오(곡 끝 → 다시 하기 → 맵 다시 열기 → 켬/끔 → 종료 순서와 로그 검사). 마지막 요약 한 줄: `GPUDECO_G2=PASS|FPS_FAIL|STABILITY_FAIL|ERROR` + 이유와 켬/끔 FPS·프레임당 CPU ms. 시험은 게임 화면 해상도가 매번 달라질 수 있으니(모니터 꺼짐 시 2560×1440 120Hz) 같은 게임 안 끔/켬 비교만 판정에 쓴다.

## 규칙
빌드 3종(`-c Release` Dev / `-p:Edition=Player` / `-p:Edition=Player -p:AutoTestBuild=1`) 오류 0, LoadCheck·기존 fixture 통과, 일반 Player 에 이 코드가 없어야 함(기본 꺼짐, 개발 전용). 같은 시험을 코드 변경 없이 되풀이 요청하지 마라. 막히면 수치로 적고 멈춰라. 결과 `docs/handoff/codex-gpudeco-2-result.md` + `-o`(10줄).
