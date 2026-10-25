# DrawCollapse 준비 장부

## 2회차 (2026-10-25): 외부 run1 분석·소유 중 재개 제거

| 항목 | 이번 상태 |
|---|---|
| 판정 | DRAWCOLLAPSE=FAIL reason=GATE1_INCOMPLETE_AFTER_RECOVERY, gate=HOLD |
| 증거 대조 | 외부 run1 68줄 원시 순서·중복 일치, 첫 진단 1/6만 완료 |
| 환경·시점·GPU 소유 | 2560×1440@164.999 / 260.008990초 / 23547 |
| 후보 소유/장벽/구간/같은 텍스처run | 1628/316/17/1177, 확정 픽셀 기반 S는 NA |
| 실제 GPU 담당 텍스처·추가 배열 예상량 | 81종·35티어 / 압축210.989090MiB·총615.006191MiB |
| 실패 | 진단 재개 뒤 260.066747초 FastMoveOriginal 복귀, 다음 validate 실패 |
| CPU 복원 기록 | 10252/10252, unobserved_order=0, 23.988800ms; 픽셀/연속성 증명 아님 |
| 원인 범위 | 코드상 재개→60프레임 catch-up→FastMove 원본 보호, 이번 세부 Take 거절 추적 없음 |
| 수정 | 생성기만: 각 표본 새 seek·GPU on·정지 진단·GPU off 뒤 resume/stop |
| 새 계획 | diag2-isolated 103단계 생성만, 게임 성공 미검증 |
| 보존 | 기존 diag1 68단계 SHA256·baseline/abba/single 전체 명시 출력·런타임 보호 코드 |
| 오프라인 검사 | 3빌드 오류0/0/0·경고46/36/4(PlayerAuto 증분), LoadCheck279/229/280곳 오류0·정책27·생성기6 통과 |
| 게임·설치·커밋·푸시·자동 장치·설정/제어 파일 변경 | 이번 모두 0회 |
| 확정 S·2관문·FPS·픽셀·엔진 draw·끊김·전체 수명 안정성 | 미완료/미실시/NA |

실제 픽셀 기여·정렬 증명 전 2관문 진입 금지다. 같은 diag1 재시도를 하지 않는다. 이는 새 시험 예약/실행 기록이나 설계 전체의 불가능 판정이 아니다. 상세 결과 최신 절·10줄 요약을 따른다.

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
