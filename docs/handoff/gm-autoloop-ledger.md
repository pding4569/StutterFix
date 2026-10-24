
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
