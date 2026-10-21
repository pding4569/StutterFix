# Codex 임무 framecmp-4: 순서 양성 대조군이 계속 0 → 합성 스프라이트로 보장되는 대조군으로

작업 폴더 `E:\StutterFixWork\wt-framecmp`. 게임 실행·설치·커밋 금지. 결과 `docs/handoff/codex-framecmp-4-20261021-result.md` + `-o`(10줄). 근거 `docs/handoff/framecmp-run3-evidence.txt`.

## 3회차 실측
`framecmp RESULT noise_out=0 aa2_out=0 ab_out=194184 ctl_order=NA ctl_hide=NA`. 잡음 바닥(영역 밖) 0 ✓, A vs A2 영역 밖 0 ✓, 변경(숨김 진단) 검출 ✓(ab_out=194,184 픽셀). 그러나 **순서 대조군(`control order`)은 실제 장식 쌍 5개를 시도해도 모두 "차이 0 (가려졌거나 교환 가능)"로 끝났고 `ctl_hide` 도 NA** 로 끝났다. 순서에 민감하지 않은 "차이 0"은 증거가 못 되므로 순서 양성 대조군이 반드시 필요하다.
## 해야 할 것
1. **합성 대조군**: 실제 장식 쌍을 찾는 대신, 정지 상태에서 **본 카메라 체인에 보이는 위치**(제외 영역 밖, 화면 가장자리 쪽, 예: 화면 왼쪽 아래 사분면)에 우리 소유의 SpriteRenderer 2개를 임시로 만든다: 서로 겹치고, 서로 다른 불투명 색(예: 빨강/파랑, alpha 1), 같은 sortingLayer/sortingOrder/z, 서로 다른 재질(게임 장식과 같은 `Sprites/Tile` 셰이더) — 재질 인스턴스 ID 순서로 그리기 순서가 정해지는 규칙(`SortProbe` 결과: 부호 있는 ID 오름차순이 뒤→앞)을 이용해 **ID 순서를 보장**(재질을 만들고 ID 를 읽어 필요하면 다시 만들어 원하는 순서로). A 캡처 후 두 재질을 서로 바꿔 (또는 한쪽을 다시 만들어 ID 순서를 뒤집어) B 캡처 → 겹친 영역의 색이 바뀌므로 `diff` 가 >0 이어야 한다. 대조군 끝나면 임시 객체를 모두 파괴. 이 합성 대조군을 `framecmp control order` 의 기본으로(실제 장식 쌍은 보조).
2. **`control hide` 가 NA 로 끝난 이유**를 찾아 고친다(앞 단계 재시도 후 시간/횟수 상한에 걸렸는지). 합성 스프라이트 하나를 `forceRenderingOff` 해서 확실히 검출되게.
3. `framecmp RESULT` 줄에 `ctl_order=<다른 픽셀 수>`, `ctl_hide=<다른 픽셀 수>` 가 숫자로 나오고, 음성 대조(`변경 없음`)는 0 이어야 한다는 판정(`PASS/FAIL`)을 한 줄 더: `framecmp VERDICT noise=PASS aa2=PASS order_ctl=PASS hide_ctl=PASS`.
4. 빌드 3종 오류 0 + LoadCheck + fixture. Player 제외 유지.
