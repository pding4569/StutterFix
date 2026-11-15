# 코덱스 작업: M4i — VRAM 초과 붕괴, 렌더 스레드 비용, HELLO 색 (2026-11-15 밤)

작업 폴더 `E:\StutterFixWork\wt-m4` (브랜치 claude/drfull). 커밋·게임 실행·설치 DLL 교체 금지. 한국어 보고. 결과: `docs/direct-renderer/14-m4i-result.md`.
앞 문서: `12-m4h-spec.md`, `12-m4h-result.md`, `13-hello-color-notes.md`. Claude 수정(커밋됨): 확인 안 된 explicit LightMode(Exit 의 `Particles/Standard Unlit` GRABPASS)는 예외 대신 모든 패스(-1)로(`explicitUnknownPasses`).

## M4h 실게임 (3440 렌더, JALib 끔, 안전 모드 0)
| 맵 | A1 | B1 | B2 | A2 |
|---|---|---|---|---|
| Exit 88초 | 369.9 | **279.6** | **280.7** | 374.8 |
| DD 50초 | 278.1 | 24.5 | (진행 중) | |
| DD skip explicit | | 24.3 | | |

### Exit: GPU 는 원본과 같아졌다, 남은 것은 CPU(렌더 스레드)
- 메인 카메라 GPU B 2,423~2,433µs = A 2,398~2,405µs. main_us 548, **render_us 1,655**, replans 517(order_key_changes 3,624), main_max 12.7ms.
- 렌더 스레드: rt_constants 526, rt_apply 332, rt_cull 175, rt_draw 125, rt_execute 132, rt_batch(표본) 1,134µs.
- 업로드 총 2.04GB/약 3,600프레임 중 **upload_index_bytes 1.85GB(프레임당 ~0.5MB)** — 인덱스를 매 프레임 다시 쓴다. 고정 인덱스 + 간접 인자/draw 범위만 바꾸게.
- **single_draws 1,335,374(프레임당 ~345)**: single_program 666,701, single_constants 455,949, single_unverified 155,797, single_textures 56,927. 재질 상수가 달라 못 묶는 것은 인스턴스별 상수 버퍼(StructuredBuffer + SV_InstanceID/정점 속성)로 옮겨 원래 DXBC 대신 원래 셰이더와 같은 식의 우리 셰이더로? — **원래 DXBC 유지가 원칙**이므로 대안: 같은 프로그램·텍스처 연속 구간은 상수 변화를 정점 속성(색)로 흡수 가능한 경우만 묶기, 나머지는 cbuffer 링(한 번 Map + VSSetConstantBuffers1 오프셋)으로 draw 당 비용 최소화.
- 목표: Exit B ≥ A (렌더 스레드 < 0.6ms, main < 0.3ms, replans 대폭 감소).

### DD: VRAM 초과 상태에서 우리 그리기만 붕괴
- **켜기 전부터 VRAM 사용 14.1GB / 예산 7.53GB(6.8GB 초과)** — 원본은 이 상태로도 278 FPS. 우리 GPU 자원은 32MB(최대 184MB 준비 중).
- B1: 메인 카메라 GPU 31~41ms 중 **gpu_native_frame_us 40,512**(우리 native), explicit 74, 그 밖 994. skip explicit 도 24.3 FPS → explicit 무관. M4g 에선 같은 DD B2 가 212 FPS 로 정상인 적이 있음(간헐).
- 가설: 우리 그리기를 **deferred context 명령 목록 하나로 ExecuteCommandList** 하면 드라이버가 목록이 참조하는 모든 자원의 상주를 한꺼번에 요구 → 초과 상태에서 페이징 폭주. Unity 는 즉시 컨텍스트로 조금씩. 시험: 즉시 컨텍스트 경로(렌더 스레드 이벤트 안에서 immediate context 에 직접, 상태 저장/복원), 또는 목록을 작게 나누기. Grab 텍스처 20MB 상주 영향도. 원인을 timeline 으로 증명하고 고쳐라. (VRAM 이 넘치지 않는 조건 비교도 도구로: 예 이미지 최대 변 줄이는 설정으로 같은 판.)

### HELLO 색: `13-hello-color-notes.md`
- 파티클이 빠진 색의 약 절반. **같은 세션 안에서** on 그림 vs off+`hidepath` 숨김 그림 비교로 이분 탐색해 "우리 그림에 빠진 렌더러"를 특정하는 진단 계획을 만들고(drfull on 상태에서 grab 한 그림을 기준으로), 파티클 explicit DrawRenderer 가 실제로 그리는지 GPU timeline/픽셀로 확인. 고쳐라.

### 반올림 ±1~2 (Exit 11%, M4d 부터 같음), DD 가는 선 빨강 41, WF 대각선 34: M4h 결과의 합성 원인을 실제 장식에 귀속.
- 4종 빌드 오류 0, export 보존, fixture. 계획 m4i 접두사(pixel+ABBA 4맵, HELLO 이분 진단판, DD 즉시 컨텍스트 A/B).
