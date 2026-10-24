# modcpu 자동 시험 장부

## 첫 회전 — 기준선 준비 (2026-10-25)

지시: `codex-modcpu-20261025.md`. 첫 회전 코드 변경 금지에 따라 기존 GPU 경로 그대로 기준선을 준비했다. 자동 장치는 호출하지 않았다.

| 항목 | 값 |
|---|---|
| 소스 | codex/night-gpu / 4fd3a01 |
| 생성기 | tools/optimization/modcpu-steps.py, 수정 없음 |
| 계획 | tools/optimization/out/modcpu-baseline-steps.json |
| tag / 명령 수 | modcpu-260-270 / 66 |
| 요구 환경 | 3440×1440@165Hz |
| 요구 소유 | 두 창 모두 23547 |
| 기준선 창 1 / 창 2 FPS | NA / NA |
| fps_before / fps_after / gain_pct | NA / NA / NA |
| 실제 환경 / 소유 | NA / NA |
| onupdate / fuse / phasewindow | NA / NA / NA |
| 새 픽셀 불일치 / 안정성 | NA / NA |
| modcpu 원시 로그 | 없음 |
| 판정 | MODCPU=FAIL reason=BASELINE_NOT_RUN |

오프라인 계획 계약을 확인했다. 이번 새 코드·게임 실행·설치·커밋·푸시·자동 장치 호출·제어 파일 변경은 0회다. 기존 render-merge 장부와 문서는 유지한다. 실제 결과가 들어오면 원문·파일·행과 두 창 개별 FPS를 남기고 모집단/환경을 확인한 뒤 평균을 계산한다. 다음 후보는 (1) TightLate 순회 감소이며 기준선 수집 전에 적용하지 않는다. PASS에는 +10%와 183 FPS 이상, 기존 G1/G2 기준 유지 및 새 픽셀 불일치 0의 증거가 모두 필요하다.
