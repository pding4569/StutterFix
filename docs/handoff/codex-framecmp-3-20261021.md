# Codex 임무 framecmp-3: 잡음 바닥이 화면 중앙 공 주변에 국한 → 영역 제외·비중단 보고로 끝까지 돌게

작업 폴더 `E:\StutterFixWork\wt-framecmp`. 게임 실행·설치·커밋 금지. 결과 `docs/handoff/codex-framecmp-3-20261021-result.md` + `-o`(10줄). 근거: `docs/handoff/framecmp-run2-evidence.txt`.

## 실측(2회차) — 캡처 성공!
Arche 264초 정지 후 `framecmp noise N1 N2`: 두 캡처 모두 `valid=1`(2560×1440, 같은 렌더러 목록 해시), **차이 `maxRGBA=4,3,2,0 changed=1563/3,686,400 (0.042%) PSNR 82.7dB`, 차이 위치 상위 32×32 격자 = (1280,704)·(1248,704)·(1152,704)·(1152,736)·(1184,704) = 화면 중앙(공/행성/꼬리/빛) 주변**. 즉 정지 중에도 중앙의 행성·파티클·셰이더 시간(`shader_Time` 은 같음)이 아닌 무엇인가가 미세하게 변한다. 이 잡음 때문에 `noise` 단계가 시험 전체를 중단시켰다(`단계 실패`).

## 해야 할 것
1. **영역 제외**: `framecmp diff <A> <B> [임계] [mask=x,y,w,h ...]` 및 `framecmp noise ...` 에 하나 이상의 제외 사각형(왼쪽 아래 좌표, 화면 해상도 비율로도 지정 가능 `maskpct=cx,cy,w,h`)을 추가해 제외 영역 밖의 차이만 센다. 기본 제외 영역 `center`(화면 중앙 가로세로 각 20%)를 제공하고 `framecmp mask center|none` 으로 설정.
2. **잡음 바닥 비중단**: `noise` 가 0 이 아니어도 시험 단계를 실패시키지 말고 "잡음 바닥 = 값(제외 영역 안/밖 따로)" 으로 보고하고 계속 진행(밖의 잡음이 0 이 아니면 경고). 제외 영역 밖 잡음이 0 이면 `noise outside=0` 으로 명확히 표시.
3. 차이가 중앙 주변의 무엇 때문인지 가능하면 원인(행성 `PlanetRenderer`, 꼬리 파티클 `tailParticles`, 정지 중에도 갱신되는 `Update` 등)을 코드로 찾아 알려 주고, 쉽게 정지시킬 수 있으면 pause 때 같이 고정(복구 포함).
4. `framecmp-steps.py` 의 시험이 끝까지 가도록: noise(제외 영역 적용) → grab A → (변경 예: `gpusplit hide deco-image:small=1` 같은 기존 진단 한 가지) → grab B → 복원 → grab A2 → diff A A2(제외 밖 0 이어야) → diff A B → control order/hide(양성) → resume. 각 단계 결과 요약 줄 `framecmp RESULT noise_out=.. aa2_out=.. ab_out=.. ctl_order=.. ctl_hide=..` 을 마지막에 한 줄로.
5. 빌드 3종 오류 0 + LoadCheck + fixture. Player 제외 유지.
