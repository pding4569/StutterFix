
## 회차 4 분석 (Codex, 2026-10-25, 동일 환경 차단 재발)

- 원시 `E:/StutterFixWork/night-results/al-gm-3.log` 230~232행과 `gm-autoloop-run3-evidence.txt`를 대조했다. 요구 2560×1440@120, 실제 3440×1440@164.999로 맵 open 전에 차단됐다.
- 외부 2·3회차 원시 157행의 289단계 명령 목록은 완전히 같다. 동일 환경 거부가 연속 2회이며, 새 렌더 병합 측정이 아니다.
- FPS 4창·픽셀 2점·렌더러·Draw Calls·끊김·복원 4종 모두 NA다. 오류 요약의 0은 초기값이다. 시작 목록의 후속 명령을 실행 증거로 세지 않았다.
- 환경 검사·생성기 순서, 게임 ADOStartup 디컴파일·설치 DLL ilscan 재확인. 검사는 정상이며 렌더 병합 설계 한계는 입증되지 않았다.
- **자동 장치의 동일 조건 재시도 중단 필요.** 새 JSON/tag·반복 시험 요청 없음. 코드·검사·설정·기존 JSON·자동 장치 제어 파일을 유지한다. 목표 환경 확보의 새 근거 없이 다음 시험을 요청하지 않는다.
- 결과 문서·10줄 요약·측정 문서·마스터 인계·장부를 갱신했다. 원시 대조·문서 정합성·diff check 확인, 코드 변경이 없어 빌드 반복 없음. 게임 실행·설치·커밋·푸시·설정 변경 0회다.
- 판정: `RENDERMERGE=ERROR reason=ENVIRONMENT_MISMATCH`, 모든 후보 측정 NA, PASS 아님. T2 기존 결과와 220/400 FPS까지 남은 수치를 보존했다.

## 회차 3 분석 (Codex, 2026-10-25, 환경 불일치로 중단)

- 외부 2회차 원시 `E:/StutterFixWork/night-results/al-gm-2.log` 231~233행과 요약 증거 확인. 맵 open 전 환경 검사에서 요구 2560×1440@120, 실제 3440×1440@164.999로 거부됐다.
- FPS 4창·픽셀 2점·렌더러/Draw Calls·30ms 초과·복원 4종 모두 NA. 요약의 0은 초기값이다. 2회차 음악 시작 대기 수정도 실제 실행 전이다.
- `RequireGpuMergeEnvironment`와 생성기 검사 순서는 정상. 게임 ADOStartup 디컴파일과 설치 DLL의 ilscan으로 시작 시 해상도 처리 호출 관계 확인. 게임 시작 코드가 목표 환경을 보장하지 않는다.
- 렌더 병합 설계 한계의 증거가 아니다. 검사 완화·다른 해상도 승인·화면 설정 변경·근거 없는 렌더러 코드 수정 없음.
- **같은 환경·같은 코드·같은 계획의 반복 시험 금지. 새 JSON/tag와 재시험 요청 없음.** 외부 운영 단계에서 목표 화면 확보 근거가 있어야 후속 시험 검토 가능. 자동 장치 제어 파일은 수정하지 않았다.
- 문서·10줄 요약 갱신, 원시 증거/코드/IL·문서 정합성·diff check 확인. 코드 변경이 없어 빌드/기존 검사 재실행 없음.
- 판정 `RENDERMERGE=ERROR reason=ENVIRONMENT_MISMATCH`, FPS·픽셀·렌더러·안정성 NA. T2 기존 결과와 220/400 FPS 차이 보존, PASS 아님.
- 게임 실행·설치·커밋·푸시·설정 변경 0회. 상세 `codex-render-merge-result.md` 3회차 참조.

## 회차 2 코드 수정 (Codex, 2026-10-25, 게임 실행 없음)

- 회차 1 원시 `E:/StutterFixWork/night-results/al-gm-1.log`에서 `decobake ready` 고정 계약 변경 실패를 확인. ABBA·픽셀·복원 수집 전이며 FPS 0은 성능값이 아니다.
- 실패한 `seek → scheduled → pause` 순서를 반복하지 않는다. scheduled는 PrepVfx 관찰뿐이고 이후 `OnMusicScheduled → Scrub`이 오디오 pause를 해제할 수 있다(게임 디컴파일·설치 DLL ilscan으로 호출 관계 확인). 기존 로그에 필드별 값이 없어 변화 순간 직접 계측은 미완료.
- 모든 seek 뒤 실제 음악 시작 확인(40초 상한), 최초 준비는 250초 후 pause. 계약 실패 필드와 구체적인 실패 단계를 RENDERMERGE 오류에 보존한다. 기존 거부 조건과 렌더 수용 기준은 유지.
- 회차 1 실제 환경은 3440×1440@165. 목표 2560×1440@120 읽기 전용 사전 검사를 open 전에 추가. 해당 범위 불일치면 ERROR로 중단하며 설정은 바꾸지 않는다.
- 새 생성기 tag=`rendermerge-260-270-abba-startsync2`; JSON `tools/optimization/out/render-merge-round2-steps.json` 생성만 완료. 자동 장치는 수정 생성기와 새 실행 이름을 사용하며 기존 계획 그대로 재실행하지 않는다.
- 검증: Dev/Player/PlayerAuto 빌드 오류 0, RenderMergeCheck 44·FrameCompareCheck 81·생성기 계약 7개 통과, diff check 통과. 게임 실행·설치·커밋·푸시·설정 변경 없음.
- 현재 수정판은 GAME_NOT_RUN, FPS·픽셀·렌더러·끊김·복원 NA, PASS 아님. T2 기존 결과와 400 FPS까지 남은 수치 보존. 상세 `codex-render-merge-result.md`, 10줄 `codex-render-merge-result-o.txt`.

## 회차 1
- Codex 요약: 코드·ABBA 시험 생성기·결과 문서를 준비했습니다.

- 빌드 3종과 ETW 오류 0개, 오프라인 검사 44개·생성기 검사 5개 통과.
- 게임 실행·설치·커밋은 하지 않았습니다.
- 실제 FPS·픽셀·안정성은 미측정이며, PASS로 판정하지 않았습니다.

[결과 문서](E:/StutterFixWork/wt-gpu/docs/handoff/codex-render-merge-result.md) · [10줄 요약](E:/StutterFixWork/wt-gpu/docs/handoff/codex-render-merge-result-o.txt)


- 시험 핵심 줄:
  - [StutterFix] [gpudeco] cost_probe enabled=False frames=0 cpu_only=True advance_ms=0.000000 dirty_alpha_ms=0.000000 source_upload_ms=0.000000 dirty_calls_avg=0.000000 dirty_bytes_avg=0.000 source_slots

## 회차 2
- Codex 요약: 인계 문서대로 준비 실패를 분석하고 음악 시작 대기·해상도 사전 검사·오류 진단을 추가했습니다.

- 빌드 3종 오류 0개, 오프라인 검사 132개 통과.
- 수정된 시험 JSON과 결과 문서·장부 갱신 완료.
- 게임 실행·설치·커밋·푸시 0회.
- 실제 FPS·픽셀·안정성은 미검증으로, PASS 판정은 보류했습니다.

[결과 문서](E:/StutterFixWork/wt-gpu/docs/handoff/codex-render-merge-result.md) · [10줄 요약](E:/StutterFixWork/wt-gpu/docs/handoff/codex-render-merge-result-o.txt)


- 시험 핵심 줄:
  - [StutterFix] [gpudeco] cost_probe enabled=False frames=0 cpu_only=True advance_ms=0.000000 dirty_alpha_ms=0.000000 source_upload_ms=0.000000 dirty_calls_avg=0.000000 dirty_bytes_avg=0.000 source_slots

## 회차 3
- Codex 요약: 분석 후 환경 불일치로 중단했습니다.

- 요구: `2560×1440@120`
- 실제: `3440×1440@164.999`
- 맵 실행 전 차단되어 FPS·픽셀·안정성은 미측정입니다.
- 검사는 정상 작동해 코드·설정은 유지했습니다.

[결과 문서](/E:/StutterFixWork/wt-gpu/docs/handoff/codex-render-merge-result.md)와 10줄 요약·장부 등 문서 5개를 갱신했고 정합성 검사를 통과했습니다. 같은 조건의 재시험은 요청하지 않았습니다.

게임 실행·설치·커밋·푸시 0회입니다.


- 시험 핵심 줄:
  - [StutterFix] [gpudeco] cost_probe enabled=False frames=0 cpu_only=True advance_ms=0.000000 dirty_alpha_ms=0.000000 source_upload_ms=0.000000 dirty_calls_avg=0.000000 dirty_bytes_avg=0.000 source_slots

## 회차 4
- Codex 요약: 4회차 분석을 완료하고 문서 5개를 갱신했습니다.

- 요구 `2560×1440@120`, 실제 `3440×1440@164.999`로 동일 환경 차단이 재발했습니다.
- FPS·픽셀·안정성은 미측정이며 PASS가 아닙니다.
- 검사는 정상이라 코드와 시험 계획을 유지하고 반복 시험 중단을 기록했습니다.
- 문서 정합성·10줄 요약·diff 검사를 통과했습니다.

[결과 문서](/E:/StutterFixWork/wt-gpu/docs/handoff/codex-render-merge-result.md)

게임 실행·설치·커밋·푸시는 하지 않았습니다.


- 시험 핵심 줄:
  - [StutterFix] [gpudeco] cost_probe enabled=False frames=0 cpu_only=True advance_ms=0.000000 dirty_alpha_ms=0.000000 source_upload_ms=0.000000 dirty_calls_avg=0.000000 dirty_bytes_avg=0.000 source_slots
