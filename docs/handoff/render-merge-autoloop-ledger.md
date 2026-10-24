# render-merge 장부

## 재생 중 전환 코드 준비 (2026-10-25, 게임 실행 없음)

- 입력: render-merge 인계 05:40 보충과 기존 `rm-abba2.log` 344~389/458~469행. 환경은 통과했고 pause/resume 뒤 FastMove 중첩으로 GPU 소유가 꺼졌다. 준비 소유 26개도 기존 23,547개와 달라 별도 원인 확인이 남는다.
- 변경: ABBA 진입 pause/resume 제거, live ready는 사전 생성 fallback 경계가 같을 때만 무재생성 검증, live on/off 허용. 경계 불일치 시 재생 중 거부/일시정지 준비만 허용. 창 시작 소유 수/census와 동일 ABBA 화면 환경 검증 추가.
- 생성기 tag=`rendermerge-260-270-abba-live-toggle3`, 새 JSON=`tools/optimization/out/render-merge-live-toggle3-steps.json`. 생성만 했고 자동 장치/게임을 호출하지 않았다. 기존 JSON·설정·제어 파일·사용자 문서 이동은 보존했다.
- 검증: Dev/Player/PlayerAuto 빌드 오류 0(경고 46/4/39), 생성기 8개, RenderMergeCheck 44개 통과. T2 원시 메인 포함 표본 3,846개 확인·기존 구현/기록 보존. diff check 확인.
- 판정: `RENDERMERGE=ERROR reason=GAME_NOT_RUN`; FPS/픽셀/렌더러/호출/끊김/복원 NA. 26개 소유를 원래 목표 장면 성능으로 해석 금지. 같은 코드 반복 시험 요청 없음. 상세 결과·10줄 요약 참조.

## 자동 반복 2회차 코드 수정 (2026-10-25)

- 외부 gm 1회차는 `decobake ready` 고정 계약 변경으로 측정 전 중단. 원시 로그에서 예정된 음악 시작 완료 전에 pause한 순서와 실제 3440×1440@165 환경을 확인했다. 개별 계약 값은 과거 로그에 없어 정확한 변화 순간 직접 계측은 남았다.
- `OnMusicScheduled → Scrub → AudioListener.set_pause` 호출을 설치 DLL ilscan으로 확인. 모든 seek 뒤 실제 음악 시작 대기(40초 상한), 최초 준비 250초 후 pause, 맵 로드 전 2560×1440@120 사전 검사, 계약 필드·단계 실패 보존을 추가했다. 렌더 병합 본체나 수용 기준을 완화하지 않았다.
- 새 생성기 tag=`rendermerge-260-270-abba-startsync2`, JSON `tools/optimization/out/render-merge-round2-steps.json` 생성. 기존 계획의 반복 시험은 요청하지 않는다. 자동 장치 환경이 목표와 다르면 설정 변경 없이 ERROR로 종료한다.
- 빌드 3종 오류 0, RenderMergeCheck 44개·FrameCompareCheck 81개·생성기 7개 통과. 수정판 실측 NA, PASS 아님. 게임 실행·설치·커밋·푸시·설정 변경 없음.
- 상세 `codex-render-merge-result.md`, 10줄 `codex-render-merge-result-o.txt`, 외부 시험 장부 `gm-autoloop-ledger.md`.

## 코드 준비 회차 (게임 실행 없음)

- 입력: `codex-render-merge-20261025.md`, `codex-master-20261022.md`, `claude-perf-260-270-20261024.md`.
- 변경: 구간 렌더러·희소 컴퓨트 복사·실제 알파/CPU fallback 검증, ABBA 및 픽셀/Draw Calls/수명 판정, ETW 정확 주소 이름 치환·INC/LEAF, steps 생성기.
- 오프라인 검증: Dev/Player/PlayerAuto 및 ETW 빌드 오류 0, RenderMergeCheck 44개, 생성기 계약 5개, diff check 통과. 전체 UI 인코딩 검사는 기존 UTF-16 산출물에서 중단됨(결과 문서 참조).
- 시험 실행 이름/게임 로그: 없음. 자동 장치 시작·상태 조회, 게임 실행, 설치, 커밋, 푸시 0회. 설정 파일 수정 없음.
- 현재 `RENDERMERGE=ERROR reason=GAME_NOT_RUN`; FPS/픽셀/렌더러/게임 안정성 미측정. 코드 준비와 실제 채택을 구분한다.
- 다음 자동 장치용 생성기: `tools/optimization/render-merge-steps.py --chart <Arche FULL.adofai> --output <워크트리 안 새 JSON>`. 한 프로세스·seek 260 기반이며 코드 변경 없는 재시험을 요청하지 않는다.
- 결과: `codex-render-merge-result.md`, 10줄 요약: `codex-render-merge-result-o.txt`.
