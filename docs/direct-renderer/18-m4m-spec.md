# 코덱스 작업: M4m — 재생 중 캐시가 무너진다 (2026-11-16 새벽)

작업 폴더 `E:\StutterFixWork\wt-m4` (브랜치 claude/drfull). 커밋·게임 실행·설치 DLL 교체 금지. 한국어 보고. 결과: `docs/direct-renderer/18-m4m-result.md`.
앞 문서: `17-m4l-plan.md`, `17-m4l-result.md`.

## M4l 실게임 (Claude)
- 일시정지(픽셀판): 캐시가 잘 된다 — Exit render_us 334(0.85ms 에서), replays 67,204 / records 106, plan_builds 1. DD render 403, HELLO 362. 픽셀: **DD 비트 동일**, Exit 최대 2(그대로), **HELLO PSNR 40.06 (최대 150,71,166) ← 42.8 아래로 회귀, d0/d00 잡음 대조도 실패**.
- **재생 중(Exit B1)은 오히려 나빠졌다: 159.6 FPS (A1 332.1, M4k B 233)**. phase 합계 6.26ms, FinishFrameRendering **3.37ms**(A 0.86).
  state(2,269 프레임): main_us 1,112, **render_us 4,212**, replans 521, order_key_changes 3,622,
  **cache_plan_builds 2,245(거의 매 프레임)**, cache_records 17,193, replays 259,120,
  **cache_constant_evaluations 31,042,759(프레임당 ~13,700) · cache_constant_bytes 11.6GB(프레임당 ~5MB) · cache_constants_us 2,800**, cache_apply 439, cache_plan 301, cache_pose 157,
  메인 카메라 GPU 5,575(native 4,190 ← M4k 1,485), grab_copies 6,180.
- 즉 재생 중에는 (1) 계획을 매 프레임 다시 만들고, (2) 모든 draw 의 상수를 매 프레임 다시 평가·업로드한다. 상수 중 무엇이 매 프레임 바뀌는지(카메라 행렬·_Time·unity_ObjectToWorld 가 아직 draw 상수에 남았나? 글로벌을 draw 마다 복사?) 변수 이름별로 세어 state 에 넣고, **draw 별 상수에는 그 draw 의 재질 값만**, 카메라/시간/글로벌은 프레임 상수 하나로 분리하라. 원래 DXBC 가 한 cbuffer 에 섞어 읽으면(예 $Globals 에 _Time 류) 그 cbuffer 만 프레임마다 한 번 만들고 draw 끼리 공유(같은 내용이면 같은 버퍼).
- plan_builds 원인(order_key_changes 3,622 / replans 521)을 고쳐라: 거리 키 변화가 실제 상대 순서를 바꿀 때만.
- GPU native 4.19ms(M4k 1.49): 간접 draw 수(7.9M/2,269 = 프레임당 3,477) vs 이전 single_draws ~345 — 간접 draw 로 쪼개면서 GPU 비용이 늘었다. 묶을 수 있는 건 묶고, 화면 밖 draw 의 간접 인자 0 처리 비용 확인.
- HELLO 회귀(PSNR 40.06, 잡음 대조 실패)와 Grab 사각형(Claude 수정이 HELLO 를 39.8 로 만든 것) — 확실하지 않은 Grab draw 는 전체 복사.
- 4종 빌드 오류 0, export 보존, fixture. 계획 m4m 접두사. 남은 M4l 측정 결과는 `E:\StutterFixWork\night-results\m4l-*.txt/.log`.

## 추가 (Claude, M4l 나머지 실측)
- **DD 원래 크기(VRAM 14.6GB 초과) B1/B2 = 179.8/180.1 FPS (A 242.6/243.0)** — Grab 을 목록 밖 즉시 복사로 바꾼 M4l 이 VRAM 초과 붕괴를 해소했다(grab_copies 27,025 실제 복사, native GPU 4.38ms; 이전 24~68ms). 이 Grab 경로는 유지할 것.
- DD 1024: A 237.1/242.7, B 178.4/202.8. DD 재생 중 cache_constants_us 0.78~0.91ms 가 렌더 스레드의 대부분.
- **Exit cache off B1 = 205.7 FPS** (cache on 159.6/159.1, M4k 233). 즉 지금 캐시는 재생 중 손해이고, cache off 도 M4k 보다 느리다 → M4l 의 다른 변경(간접 draw 분할 프레임당 ~3,477개, Grab 경로 등)도 비용을 늘렸다. cache off: main 1,064, render 1,402, native GPU 2,089.
