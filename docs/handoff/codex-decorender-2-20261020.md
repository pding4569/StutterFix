# Codex 과제 S2b: decorender 보완 — 선택 범위 좁히기·변화 장식은 빼기·이유 로그

앞 과제(`docs/handoff/codex-decorender-20261020.md`, 결과 `codex-decorender-20261020-result.md`)의 연속. 같은 규칙(작업 폴더 E:\StutterFixWork\wt-decorender, 커밋·게임 실행·설치 금지, `#if DEV || AUTOTEST`, 사용량 절약). 결과 문서는 `docs/handoff/codex-decorender-2-20261020-result.md`.

## 게임에서 돌려 본 결과 (Arche 곡 264초, `decorender on`)
- `selected=2172 excluded=26663 source textures=40`: 보이는 장식을 전부 골라서 **정적인 큰 배경 장식**(5000x5000, 3491x1586 Repeat, 2885x1080 Repeat, 2500x2500 vignette 등)까지 들어가 아틀라스가 19장(8192x8192 3장 포함)이 됐다. 우리가 노리는 건 **프레임마다 값이 바뀌는 움직이는 장식**(실측: 약 1,100개, 텍스처 10종, 순서값 42개, 대부분 512x512 이하 또는 2048 이하 DXT5 Clamp)뿐이다.
- 그룹 중에 셰이더 `Sprites/Tile` 이 나온다(타일링 장식). Repeat wrap 텍스처도 있다. 둘 다 아틀라스로 못 간다.
- 첫 카메라(BGStaticCam) PreRender 에서 `InvalidOperationException: owned renderer destroyed or sprite/material/sorting/mask changed` 로 전체가 중단됐다(어떤 장식이 어떻게 바뀌었는지 로그 없음).

## 고칠 것
1. **선택 기준**: (a) 정확히 `scrVisualDecoration`, 보임 등 지금 조건 + (b) 셰이더가 `Sprites/Default` 계열로 아틀라스 가능(타일링 `Sprites/Tile`·Repeat/Mirror wrap·특수 키워드 제외), (c) 텍스처 한 변 ≤ 2048, (d) **최근 움직이는 장식만**: `on` 시점에 N프레임(기본 20, 인수로) 동안 관찰해(카메라 PreRender 마다 월드 행렬 또는 `spriteRenderer.color`/Transform 변화 확인) 값이 바뀐 장식만 선택. 정적 장식은 원래 SpriteRenderer 로 둔다. 선택/제외 이유별 개수(타일링 셰이더, wrap, 큰 텍스처, 정적, 마스크/히트박스, 기타)를 로그. 아틀라스 페이지는 총 VRAM 이 작게(목표: 선택된 텍스처 합이 4096x4096 한 장 이하) — 넘으면 선택 장식 중 큰 것부터 제외하고 로그.
2. **변화 감지 시 장식 하나만 되돌리기**: Build 중 어떤 장식의 sprite/material/sorting/mask/렌더러 소유가 바뀌었거나 파괴됐으면 **전체 중단하지 말고 그 장식만** 관리에서 빼 원래 SpriteRenderer(`forceRenderingOff=false`)로 돌려주고 그 이유(어떤 항목이 바뀌었는지: sprite/material/sortingOrder/sortingLayer/mask/destroyed)를 개수별로 `state` 에 남긴다. 전체 중단은 카메라 구성이 바뀌는 등 복구 불가 조건에만.
3. **로그**: 처음 N개 변화 이벤트는 장식 인덱스·항목·이전/이후 값을 자세히.
4. 나머지(compare, off, state, 자동 복구)는 그대로. compare 는 선택된 움직이는 장식 집합에 대해 같은 방식으로 동작하고, 가져간 집합 안/밖 픽셀 차이를 구분할 수 있으면 더 좋다(가능하면: 집합에 속한 장식의 화면 영역 바운딩 박스 안의 픽셀만 따로 집계).

## 결과 문서에
바꾼 선택 기준, 새 `state` 항목, 빌드 결과, 게임에서 돌릴 순서(앞과 같음 + `decorender on 20`).
