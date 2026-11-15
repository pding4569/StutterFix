# 코덱스 작업: M4j — 상수 링 회귀, DD VRAM 붕괴의 진짜 원인, 남은 픽셀 차이 (2026-11-16 새벽)

작업 폴더 `E:\StutterFixWork\wt-m4` (브랜치 claude/drfull). 커밋·게임 실행·설치 DLL 교체 금지. 한국어 보고. 결과: `docs/direct-renderer/15-m4j-result.md`.
앞 문서: `14-m4i-spec.md`, `14-m4i-result.md`, `13-hello-color-notes.md`.

## Claude 가 고친 것 (커밋됨): ColorWriteMask 비트 순서
- native 가 Unity 직렬화 colMask(Unity ColorWriteMask: A=1 B=2 G=4 R=8)를 그대로 D3D11 RenderTargetWriteMask(R=1 G=2 B=4 A=8)로 썼다 → `ColorMask RGB`(14)가 "G+B+A" 가 되어 빨강을 안 쓰고 알파를 썼다. HELLO 배경색 불일치의 주원인. 변환을 넣음(drfull.cpp).
- 결과(일시정지 픽셀, 같은 세션 off/on): **HELLO PSNR 19.7 → 43.6**, 최대 (148,61,173) → (82,73,32), >3 15.5%, >10 0.65%, >30 0.07%. DD 여전히 비트 동일, Exit 최대 2(11%), WF 최대 36(대각선 빛 가장자리, 이 수정과 무관).
- 이 종류의 "Unity 열거형 ↔ D3D11 열거형" 변환을 **모두** 다시 검사하라(블렌드 인자·연산, 비교 함수, 스텐실 연산, 컬링, 깊이 쓰기, 텍스처 주소/필터, 정점 형식). WARP fixture 에 ColorMask RGB/A/0 같은 비대칭 경우를 넣어 재발 방지.

## M4i 실게임
| 맵 | A1 | B1 | B2 | A2 |
|---|---|---|---|---|
| Exit | 372.6 | **250.7** | **247.5** | 374.0 |  (M4h 279.6/280.7 보다 나빠짐)
- Exit B1: main 849(collect 451 ← M4h 148), render 2,024(**rt_constants 1,407 ← 526**), upload_index 0(좋음), **upload_constant_bytes 6.30GB ← 82MB** — 상수 링이 매 프레임 전부를 다시 올린다. 바뀐 상수만 링에 쓰고 나머지는 이전 오프셋 재사용. collect 회귀 원인도.
- DD: deferred/immediate 차이 없음(21.6~24.6 FPS, gpu_native_frame 41~47ms) → **명령 목록 가설 기각**. `pressure mip 2` 는 VRAM 14.2GB 그대로(장식 텍스처에 mip 없음) → 판정 불가. 원본은 같은 VRAM 초과(14.1/7.53GB)에서 3.5~3.7ms.
  - 다음: (a) **그리기별 GPU 시간**(구간을 잘게: 텍스처별/draw 묶음별 timestamp)으로 41ms 가 어느 draw 들인지 특정, (b) 그 draw 의 텍스처·SRV·샘플러(우리가 만든 SRV 포맷/MostDetailedMip, sRGB 뷰, 샘플러 aniso/MaxLOD)가 Unity 가 쓰는 것과 다른지, (c) VRAM 이 넘치지 않는 대조: 이미지 최대 변 설정(StutterFix `ImageMaxSide`, 예 1024)으로 A/B 같은 조건 계획을 만들어라. Unity 는 같은 텍스처를 그리는데 왜 우리만 느린지가 핵심.
- HELLO 이분 진단은 결정적이지 않았다(숨김 차이 2,705,203 → 2,705,314) — 전역 규칙 문제(위 마스크)였다. 파티클 explicit vs baked 0.19% 차이 → 파티클은 그려진다.

## 남은 픽셀
- HELLO 0.65% (>10): 위치 히스토그램이 화면 가운데~오른쪽 넓게. 남은 비대칭 상태 변환(위 전수 검사)부터.
- WF 대각선 빛 가장자리 36, Exit ±2(11%): 원인 귀속.
- 4종 빌드 오류 0, export 보존, fixture. 계획 m4j 접두사(4맵 pixel+ABBA, DD 그리기별 GPU 진단, DD ImageMaxSide 1024 A/B).
