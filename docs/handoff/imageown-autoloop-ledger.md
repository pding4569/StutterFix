# 이미지 소유 작업 장부

## 2026-10-25 — 코드·오프라인 검증 2회차

`IMAGEOWN=FAIL fps_before=NA fps_after=NA gain_pct=NA pixel_max=NA fallback=NA spikes_30ms=NA reason=DIAG2_NOT_RUN`

외부 run1 직접 증거42줄을 원시 로그에 대조했다.3440×1440@164.999·GPU28,523·활성화 CPU fallback0, 정점주입286개·world delta0.00013647876, refreshpose 호출0/4,976을 관측했다. O/O2·G/N·D/DN·G/G2 반복/복원 차이는0이나 원본 O/G·O/D·O/U 비교는 **없다**.

중단 원인은 첫 시점 아홉 번째 캡처 UN의 **8장 상한 위반**이다. 원시580행 실패 뒤583행 pause 복원·585행 원본 보호이므로 보호 복귀를 선행 원인으로 오인하지 않는다. 기존 diag1은 반복하지 않는다.

기본 diag를165단계 diag2로 변경했다. CPU 반복 대조/저장 후 O2 재사용으로 최대8장, 후보별 원본 diff/PNG 즉시 기록, bounded GPUIMG_DIAG의 네 반복/복원 대조를 추가했다. 명시 diag1/abba 전체 출력8사례 동일·기존 JSON 해시 보존. 새 JSON은 생성만 했다.

SetSprite/UpdatePosition 디컴파일 및 설치 DLL ILScan 대조. renderer 연결2곳은 현재 이미 존재하며 수정하지 않았다. 픽셀 원인 미확정·ABBA 선행 조건 미충족, 소유 방식 불가능 판정은 아니다.

3빌드 오류0/0/0·경고48/36/40, LoadCheck279/229/281곳 틀림0·일반 Player 연구 제외, 생성기7검사 통과. 빌드 격리·큐 재검사 생략. 게임·설치·커밋·푸시·장치 호출·Editor·설정/제어 변경0회, 타 작업자 bundle 근거 변경 유지. 상세 결과와10줄 요약을 따른다.

## 2026-10-25 — 코드·오프라인 검증 1회차

`IMAGEOWN=FAIL fps_before=NA fps_after=NA gain_pct=NA pixel_max=NA fallback=NA spikes_30ms=NA reason=GAME_NOT_RUN_AND_COLLAPSE_OWNER_HOOKS_PENDING`

사용자 직접 지시와 `codex-imageown-20261025.md`를 적용했다. 게임·설치·커밋·푸시·자동 장치 호출·설정/제어 변경 0회다. 기존 사용자/Claude 변경을 유지했다.

1. 진단 (a)/(b): UpdatePosition 선택과 지속 CPU TransformPoint VB 주입, GPUIMG_DIAG 숫자 로그 및 잡음/복원 계획을 구현했다. **실제 진단 로그 없음**. 게임 금지로 원인 확정은 보류했다.
2. 원인 수정: 두 후보의 실제 차이가 없어 어느 수식도 확정 수정하지 않았다. 이미지 matrix/refreshpose는 명시 선택이며 새 pose 경로 기본 꺼짐을 유지했다. 픽셀 max<=3은 NA다.
3. 통합 준비: 소유 수 입구·32바이트 부분 갱신·호출과 알파 더티 감시를 구현했다. `GpuDecoDrawCollapseRenderer.cs` 수정 금지로 RegisterGpuImageCollapseVariants/RememberGpuImageCollapseAddresses 두 호출은 소유자 요청으로 남겼다. 연결되지 않은 후보를 통합 완료로 표시하지 않는다.
4. 수용 시험: 기본 diag1 156단계와 선행 조건이 붙은 abba1 320단계를 생성만 했다. 이전 템플릿/계획을 유지했다. ABBA는 이미지 소유 off/on 비교이며 기존 독립/병합 ABBA를 그대로 쓰지 않는다.
5. 검증: 최종 3종 빌드 오류0(경고48/36/40), LoadCheck279/229/281곳 틀림0, 실제 큐4495개·생성기5개 통과. 정적 대기4476개의 정상 프레임 감시0은 합성 상태 기계 결과이며 실FPS가 아니다.

상세 구현·소유자 요청·외부 실행 선행 조건·한계는 `codex-imageown-result.md`, 10줄 요약은 `codex-imageown-summary.txt`를 따른다. 같은 미완성 통합 계획을 반복 실행하지 말고, 먼저 기본 정지 진단 근거를 받아 원인별로 진행한다. 자동 장치를 직접 시작하거나 중지하지 않았다.
