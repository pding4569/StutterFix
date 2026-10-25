# DrawCollapse 준비 장부

## 5회차 (2026-10-25): Unity64개 상한 수정·최종36슬롯·초과 개별 렌더러

NEED_EDITOR_BUILD

- 외부 run1은 현재182단계의 번들 명령 실패15줄(14 메시지 일치+시작1500자 접두부1), run3은294줄(293+접두부1)이다. 순서 대조 완료, 새 게임 시험은 없다.
- 외부 Editor의 실제 실패는 `Shader 'StutterFix/GpuDecoBatch' uses 70 texture parameters, more than the 64 supported by the current graphics device.`다. 라이선스/컴파일은 통과했고 -nographics는 별도 D3D11 unavailable 오류다.
- 10:50 추가 지시대로 최종4배열+32직접=36선언·C#/생성기 상한 통일·초과 장식만 기존 개별 GPU 렌더러와 장벽으로 보존했다. 현재81종은29슬롯·추가163.75MiB로 들어간다. 초과 실측·게임 할당·여유VRAM은NA다.
- 최종 DEV/Player/PlayerAuto 오류0/0/0·경고46/4/39(Player증분), LoadCheck279/229/280곳 오류0·일반 Player 연구 제외, 정책52·계획9·SM5컴파일4 통과, 랩C# 오류0·경고12다.
- 추가 지시 확인 전64슬롯 중간본은 독립 Editor return0·260정점·합성40사례 maxdiff0이다. 최종36슬롯/fallback/게임 증명이 아니며 지시 확인 후 Editor0회다. 이번 Editor 호출 총3회, 첫2회 프로젝트 점유return1이다.
- 최종drawcollapse5 번들은 아직 없다. 별도 ABBA5 182단계는 생성만, 기존diag1/diag2/ABBA4 JSON 해시·명시모드 보존이다. Claude 번들 빌드/재로드 뒤 외부 시험이 필요하다.
- `DRAWCOLLAPSE=FAIL reason=NEED_EDITOR_BUILD`; FPS·draw·원본픽셀·끊김·구간수·전체수명·설치복원NA. 게임·설치·커밋·푸시·장치·설정/제어변경0회다. 상세 결과 최신 절과10줄 요약을 따른다.

## 4회차 (2026-10-25): 단일 draw 구현·오프라인 검증, 새 번들 미완성

- 최신 지시대로 1관문을 통과로 수용하고 단일 submesh/재질 대표·직접 EvaluateCollapse Raw VB·압축 배열+원본2D·on/off 복구를 구현했다.
- 외부81종은 DXT5 배열3개55종 **163.75MiB** + 원본2D26종, 필요29슬롯이다. 구현 상한6배열+64직접, 실제 할당·여유VRAM 미검증이다.
- 3빌드 오류0/0/0·경고46/36/39, LoadCheck279/229/280곳 오류0·일반 Player 연구 제외, 정책46·생성기9·SM5컴파일4 통과다. Unity 랩 C# 오류0·경고12; CS0119 수정 완료다.
- 기존 Editor 로그에 패키지 서버 실패(return1)·`No valid Unity Editor license found. Please activate your license.`(return198)가 있다. 이번 Editor 반복0·새 번들 미완성이다.
- 새 기본182단계 ABBA4·260/262/270 원본픽셀은 생성만, 기존 명시 모드·diag1/diag2 JSON 해시 보존이다. 새 번들 성공 전 실행 불가다.
- `DRAWCOLLAPSE=FAIL reason=UNITY_BUNDLE_NOT_BUILT`, 실제 FPS·draw·픽셀·끊김·준비구간수·전체수명·설치복원NA다. 게임·설치·커밋·푸시·장치 호출·설정/제어 변경0회다. 상세 결과 최상단을 따른다.

## 3회차 (2026-10-25): 외부 run2 6표본 완료·현 진단 설계 한계로 반복 중단

| 항목 | 이번 상태 |
|---|---|
| 판정 | DRAWCOLLAPSE=FAIL reason=DIAGNOSTIC_PROOF_LIMIT, gate=HOLD |
| 증거 대조 | 294줄 중 293줄 원시 완전 일치·시작 1줄 1500자 접두부 일치, 순서/중복 보존 |
| 실행 계획 | 원시 시작 선언 103단계와 기존 diag2 JSON 일치(맵 경로 구분자 정규화), 완료 동사 103개 일치 |
| 환경·GPU 담당 | 6표본 모두 2560×1440@164.999 / 23547 / fallback0 |
| 후보 구간 / 후보 장벽 | 17/17/18/17/10/10 / 316/326/334/354/391/327, 확정 S·연속 구간 최대 NA |
| 텍스처·추가 배열 예상량 | 6회 모두 81종·35티어 / 압축210.989090MiB·총615.006191MiB, 할당·복사0 |
| 해제 복원 Rec | 10854/9954/10507/10854/9383/11104, 각 기대값 일치·unobserved_order0 |
| 이전 오류·종료 | recovery 줄 없음·자동 시험 끝·shutdown_disposed=True native_contexts=0 |
| 남은 실패 원인 | 후보 진단은 실제 픽셀 기여/엔진 순서 검사가 없고 HOLD를 고정 출력 |
| 코드 교차확인 | scrVisualDecoration 근사 디컴파일·설치 DLL ILScan으로 알파/셰이더/마스크 호출 확인, 미확인 renderer 원인 귀속 없음 |
| 결정 | 현 진단 설계의 증명 한계로 같은 코드/계획 반복 중단, 통합 방식 전체의 불가능·too_many_barriers 판정 아님 |
| 변경·보존 | 문서5개만 수정, 런타임·생성기·기존 JSON/tag·설정/제어 파일 유지, 새 계획/병합/시험 요청 없음 |
| 검증 | 원시·6회 메모리/복원·SHA256·문서/10줄/UTF-8/diff, 코드 변경 없어 빌드/LoadCheck/기존 검사 반복0회 |
| 게임·설치·커밋·푸시·자동 장치·설정/제어 파일 변경 | 이번 모두0회, 자동 장치 직접 중지도 없음 |
| FPS·픽셀·엔진draw·끊김·전체 수명·설치 복원 시험 | 모두 NA |

1관문은 HOLD·2관문 미구현이다. 실제 픽셀 기여·비소유와의 엔진 순서 증거 없이 후보 최대18을 확정 S≤150로 쓰지 않는다. 상세 결과 최신 절·10줄 요약을 따른다. 아래 2회차의 diag2 미실행 표기는 외부 run2 수신 전 이력이다.

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
