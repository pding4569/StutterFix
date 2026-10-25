# 이미지 소유 작업 장부

## 2026-10-25 — 코드·오프라인 검증 1회차

`IMAGEOWN=FAIL fps_before=NA fps_after=NA gain_pct=NA pixel_max=NA fallback=NA spikes_30ms=NA reason=GAME_NOT_RUN_AND_COLLAPSE_OWNER_HOOKS_PENDING`

사용자 직접 지시와 `codex-imageown-20261025.md`를 적용했다. 게임·설치·커밋·푸시·자동 장치 호출·설정/제어 변경 0회다. 기존 사용자/Claude 변경을 유지했다.

1. 진단 (a)/(b): UpdatePosition 선택과 지속 CPU TransformPoint VB 주입, GPUIMG_DIAG 숫자 로그 및 잡음/복원 계획을 구현했다. **실제 진단 로그 없음**. 게임 금지로 원인 확정은 보류했다.
2. 원인 수정: 두 후보의 실제 차이가 없어 어느 수식도 확정 수정하지 않았다. 이미지 matrix/refreshpose는 명시 선택이며 새 pose 경로 기본 꺼짐을 유지했다. 픽셀 max<=3은 NA다.
3. 통합 준비: 소유 수 입구·32바이트 부분 갱신·호출과 알파 더티 감시를 구현했다. `GpuDecoDrawCollapseRenderer.cs` 수정 금지로 RegisterGpuImageCollapseVariants/RememberGpuImageCollapseAddresses 두 호출은 소유자 요청으로 남겼다. 연결되지 않은 후보를 통합 완료로 표시하지 않는다.
4. 수용 시험: 기본 diag1 156단계와 선행 조건이 붙은 abba1 320단계를 생성만 했다. 이전 템플릿/계획을 유지했다. ABBA는 이미지 소유 off/on 비교이며 기존 독립/병합 ABBA를 그대로 쓰지 않는다.
5. 검증: 최종 3종 빌드 오류0(경고48/36/40), LoadCheck279/229/281곳 틀림0, 실제 큐4495개·생성기5개 통과. 정적 대기4476개의 정상 프레임 감시0은 합성 상태 기계 결과이며 실FPS가 아니다.

상세 구현·소유자 요청·외부 실행 선행 조건·한계는 `codex-imageown-result.md`, 10줄 요약은 `codex-imageown-summary.txt`를 따른다. 같은 미완성 통합 계획을 반복 실행하지 말고, 먼저 기본 정지 진단 근거를 받아 원인별로 진행한다. 자동 장치를 직접 시작하거나 중지하지 않았다.
