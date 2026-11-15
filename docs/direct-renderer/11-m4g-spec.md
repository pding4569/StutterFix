# 코덱스 작업: M4g — 렌더 스레드 업로드 폭주 없애기 (2026-11-15 밤)

작업 폴더 `E:\StutterFixWork\wt-m4` (브랜치 claude/drfull). 커밋·게임 실행·설치 DLL 교체 금지. 한국어 보고. 결과: `docs/direct-renderer/11-m4g-result.md`.
앞 문서: `10-m4f-spec.md`, `10-m4f-result.md`.

## 주의: 지난 측정 일부가 안전 모드로 오염됐었다
측정 도구가 JALib 깨진 실행을 강제로 끄면 StutterFix 가 비정상 종료로 세어 안전 모드가 켜졌다(m4a-dd-B2 153.6, m4d-sh-B2 41.1, m4e-sh-B1 111.6 은 안전 모드 판). 고쳤고, 아래는 깨끗한 판이다(safe=0 확인).

## M4f 실게임 (Exit 88초, 3440 렌더, 깨끗한 판)
- A1 원본 335.5 FPS, **B1 M4f 132.9 FPS**. 상한(맵 그리기 뺌) 약 477.
- 일시정지 픽셀: off/off 잡음 0(FPS UI 동결 성공), on/off 최대 채널 2, 11.3% 픽셀이 ±1~2 (top 칸 bottom-left (800..832, 608..672)). 준비 0.39초(30초에서 줄어듦, 좋음).
- B1 state (1858 프레임): main_us 1,172 (collect 551, pose 447, sort 146, packet 27), main_max 75,982, replans 504, replan_max 1,230, commit_max 4,591,
  **render_us 4,872**: rt_apply 290, rt_cull 317, rt_constants 418, rt_draw 103, rt_execute 288, **rt_batch_us 5,767(표본)**,
  **rt_uploads 1,261,080 (프레임당 679), rt_upload_bytes 22,652,411,680 (프레임당 약 12.2MB)**, gpu_geometry_uploads 504(= replans 수), gpu_pose_uploads 1857(매 프레임), batch_runs 15,961, batch_sources 46,025, batch_vertices 276,150, single_draws 673,516(프레임당 362), order_key_changes 3,622, layouts 378.

## 할 일 (목표: Exit B 가 원본 335 보다 빠르게, 상한 477 에 가깝게)
1. **업로드는 바뀐 것만**: 프레임당 12MB 는 말이 안 된다. 무엇이 매 프레임 크게 올라가는지(포즈 전체? 정점 전체? 상수 전체?) 항목별 바이트를 state 에 나눠 기록하고, 바뀐 포즈/상수만 부분 업로드(Map NO_OVERWRITE 링 버퍼 또는 UpdateSubresource box). 포즈는 바뀐 슬롯만(pose_uploads=131 인데 gpu_pose_uploads 가 매 프레임 전체일 가능성).
2. **재정렬 때 지오메트리 다시 올리기 금지**(gpu_geometry_uploads = replans). 정렬 순서가 바뀌어도 정점 데이터는 그대로, 그리기 목록(인덱스/간접 인자)만 바뀌게.
3. replans 504/1858 이 여전히 많다: order_key_changes 3,622 의 원인(거리 키가 카메라 이동마다 바뀌는 것? 동점 아닌 곳의 거리 변화는 순서에 영향 없음) — 실제로 상대 순서가 바뀐 경우만 재정렬.
4. single_draws 프레임당 362: 배치로 못 묶는 이유별 개수를 state 에.
5. main_max 76ms / commit_max 4.6ms 스파이크 원인.
6. 반올림 ±1~2 (11% 픽셀)가 어디서 오는지(배치 정점 변환 정밀도? sRGB?), 가능하면 0 으로. 원래 Unity 는 같은 셰이더에 오브젝트 행렬을 곱한다 — 정점 미리 변환 순서·float 정밀도를 Unity 와 같게(mul 순서, fma).
- 4종 빌드 오류 0, export 보존, fixture 통과. 계획 m4g 접두사(4맵 pixel + ABBA).

## 추가 (Claude, 명세 작성 뒤)
- Exit ABBA(깨끗): A1 335.5 / B1 132.9 / B2 127.9 / A2 336.1.
- DD 픽셀(M4f, 잡음 0): on/off 0.12% 다름, 최대 차이는 **빨강 채널만 43** (예: off (33,127,127) on (76,127,127)), 위치는 캡처 y 793~806 의 가는 가로줄 하나(이전 M4d 에선 이 줄이 비트 동일). 가는 선 장식의 부분 픽셀 위치가 미리 변환 정밀도로 달라지고 색수차 필터(TV_Chromatical 류)가 빨강만 옮겨 커진 것으로 추정. 6번 항목(Unity 와 같은 mul 순서·정밀도)으로 0 을 만들어라. 그림: `E:\StutterFixWork\night-results\m4f-img\dd-pixel-d{0,1}.png`.
- **GPU 가 진짜 병목(DD)**: presentpath 메인 카메라("Camera", camRT) GPU 시간 — DD 원본 3,832µs → M4f **35,346µs(9배)**, Exit 2,525 → 4,751µs. DD B1 22.2 FPS(A1 253.3), main 1.4ms·render 2.5ms 라 CPU 가 아니다(bbMeanUs 4,146 → 39,812). 가설: Grab(블렌드 모드) 그리기마다 camRT 전체(3440x1440) 복사 — Unity 는 이름 있는 GrabPass(`_GrabTexture` 등)를 프레임당 이름별 한 번만 복사한다(이름 없는 GrabPass 만 매번). 원래 셰이더의 GrabPass 이름을 보고 Unity 규칙대로 복사 횟수를 맞추고, 복사 수·바이트·GPU 시간(우리 패스 timestamp)을 state 에. 다른 후보: 사전 변환 dispatch 가 전체 정점을 매 프레임 처리, 오버드로/잘못된 컬링(Unity 는 화면 밖 렌더러를 안 그린다 — 우리가 화면 밖까지 그리는지 확인), 블렌드/깊이 상태. 원인을 GPU 시간으로 증명하고 고쳐라.
- **HELLO 배경색 단서(Claude)**: `drfull grab unity`(BlendModes 를 Unity DrawRenderer 로) 진단판도 64.7% 다름 → BlendModes 원인 아님. `drfull colors` TSV(`Mods/StutterFix/drfull-colors-20261115-112501-433.tsv`)에서 `pt2_11145955-space-art-5626853_1920.jpg/child/sprite` 의 **Legacy Shaders/Particles/Additive** 슬롯 nativeCB 가 floats[8..11]=0, floats[12..15]=0 — 이 셰이더 cbuffer 는 `_TintColor` offset 32, `_MainTex_ST` offset 48(bindings.json) 이므로 **_TintColor=0, _MainTex_ST=0** 으로 채워졌다(셰이더 기본 _TintColor 0.5, _MainTex_ST (1,1,0,0)). Additive 는 2*color*_TintColor*tex 라 이 큰 배경 이미지(초록 성분 추정)가 사라지거나 한 텍셀 색이 된다 → 원본 초록, 우리 붉은/보라(같은 자리 pt2_lights 주황·보라만 남음)와 맞는다. `Value()` 의 조회 순서(MPB HasProperty → 텍스처 ST → shader Properties 기본값/재질)에서 왜 0 이 나오는지 고쳐라(MPB 가 HasProperty 를 참으로 주는데 값이 0? 재질 null? 다중 cbuffer 오프셋 섞임?). 같은 문제가 다른 셰이더(재질 속성이 Sprites/Default 와 다른 모든 셰이더)에도 있는지 전수 검사(TSV 의 모든 슬롯에서 Unity 재질 값과 우리가 채운 바이트 비교 도구).
