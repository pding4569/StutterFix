# DrawCollapse 준비 장부

## 1회차 (2026-10-25): 게임 실행 없이 1관문 진단 준비

| 항목 | 이번 상태 |
|---|---|
| 최종 판정 | DRAWCOLLAPSE=FAIL reason=GATE1_NOT_MEASURED |
| 실제 N/M/S/R/T·배열 메모리 | 모두 NA |
| 새 명령 | gpudeco drawcollapse validate / diag, DEV·PlayerAuto 전용 |
| 진단의 한계 | GPU quad·알파·frustum 후보 통계, 실제 픽셀 기여·그룹/마스크 순서 미증명, gate=HOLD |
| 배열 준비 | 정확 치수·graphicsFormat·밉·샘플러 tier별 바이트 계산만, 할당·복사 0 |
| 새 계획 | tools/optimization/out/drawcollapse-gate1-diag1-steps.json, 68단계, 생성만 |
| 표본 | 260/262/264/266/268/270초 일시정지 진단, FPS 창 아님 |
| 기존 모드 | baseline/abba/single 명시 모드 보존, 기본은 diag로 변경 |
| 빌드 오류 / 경고 (DEV/Player/PlayerAuto) | 0/0/0 / 46/36/39 |
| LoadCheck | FieldRefAccess 279/229/280곳, 각각 오류 0·동적 이름 5 |
| 오프라인 검사 | 정책 27개·생성기 4개 통과, 일반 Player 새 타입·명령 없음 |
| 게임·설치·커밋·푸시·자동 장치·설정/제어 파일 변경 | 모두 0회 |
| 2관문 구현·ABBA·픽셀·수명 검증 | 미실시 |

상세는 `codex-drawcollapse-result.md`, 10줄 요약은 `codex-drawcollapse-result-o.txt`를 따른다. 이 장부는 자동 장치를 실행하거나 새 시험을 예약한 기록이 아니다. 확정 S≤150 전에 병합 구현 금지, 확정 S>400이면 too_many_barriers로 중단, 151~400은 보류다. 후보 통계를 확정 픽셀 계수로 취급하지 않는다.
