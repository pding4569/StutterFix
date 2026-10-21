# Codex 임무 framecmp-2: 첫 게임 실측에서 캡처 실패 — 원인 진단·수정

작업 폴더 `E:\StutterFixWork\wt-framecmp` (브랜치 `codex/night-framecmp`). 게임 실행·설치·커밋 금지. 결과 `docs/handoff/codex-framecmp-2-20261021-result.md` + `-o`(10줄). 근거 로그: `docs/handoff/framecmp-run1-evidence.txt`, 전체 `E:\StutterFixWork\night-results\framecmp.log`.

## 실측 (Arche, `seek 262 8` → `waitsongpos 264 40` → `pause` → `framecmp filters off` → `framecmp ui exclude` → `framecmp noise N1 N2`)
- `pause` 는 동작: `controller.paused=1 Time.timeScale=0 DOTween.timeScale=0 AudioListener.pause=1`.
- 첫 캡처에서 **`valid=0 error=resolution or renderer state changed during readback`** 로 실패(`noise N1 N2` 단계 실패 → 시험 중단 → resume 복원은 정상).
## 해야 할 것
1. 어떤 조건 때문에 "resolution or renderer state changed" 가 났는지 코드로 확정하고, 로그를 더 상세히(어떤 필드가 달라졌는지: 화면 해상도, RT 크기, 렌더러 개수/목록 해시/enabled·forceRenderingOff 변화, 카메라 상태) 남기게 한다. 의심: (a) 정지 중에도 모드/게임이 렌더러 상태를 바꿈(Dormancy·InvisibleSkip·TileCull 이 프레임마다 enabled/forceRenderingOff 를 갱신, 또는 `framecmp filters off` 가 매 캡처 컴포넌트를 토글), (b) 다른 카메라/전체 화면 전환, (c) readback 비동기 대기 동안 프레임이 2개 이상 지나감. 정지 상태에서 렌더러 상태 변화를 막거나(캡처 동안 해당 갱신 일시정지) 변화를 비교 대상에서 의미 있게 구분(상태 해시를 캡처 전후 비교하되 유효 변화와 무시 가능한 변화 분리).
2. 고친 뒤 `framecmp-steps.py` 의 시험이 한 번에 끝까지 가도록(잡음 바닥 0, 양성 대조군 검출, 음성 대조군 0) 한다. 정지 상태에서 같은 프레임을 두 번 캡처해 차이가 0 이 아니면 이유(시간 의존 셰이더, MSAA, 비동기 업로드 등)를 분석하고 가능한 한 제거한다.
3. 빌드 3종 오류 0 + LoadCheck + fixture. Player 제외 유지.
