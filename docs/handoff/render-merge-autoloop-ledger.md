# render-merge 장부

## 코드 준비 회차 (게임 실행 없음)

- 입력: `codex-render-merge-20261025.md`, `codex-master-20261022.md`, `claude-perf-260-270-20261024.md`.
- 변경: 구간 렌더러·희소 컴퓨트 복사·실제 알파/CPU fallback 검증, ABBA 및 픽셀/Draw Calls/수명 판정, ETW 정확 주소 이름 치환·INC/LEAF, steps 생성기.
- 오프라인 검증: Dev/Player/PlayerAuto 및 ETW 빌드 오류 0, RenderMergeCheck 44개, 생성기 계약 5개, diff check 통과. 전체 UI 인코딩 검사는 기존 UTF-16 산출물에서 중단됨(결과 문서 참조).
- 시험 실행 이름/게임 로그: 없음. 자동 장치 시작·상태 조회, 게임 실행, 설치, 커밋, 푸시 0회. 설정 파일 수정 없음.
- 현재 `RENDERMERGE=ERROR reason=GAME_NOT_RUN`; FPS/픽셀/렌더러/게임 안정성 미측정. 코드 준비와 실제 채택을 구분한다.
- 다음 자동 장치용 생성기: `tools/optimization/render-merge-steps.py --chart <Arche FULL.adofai> --output <워크트리 안 새 JSON>`. 한 프로세스·seek 260 기반이며 코드 변경 없는 재시험을 요청하지 않는다.
- 결과: `codex-render-merge-result.md`, 10줄 요약: `codex-render-merge-result-o.txt`.
