# 코덱스 작업: M4n — 재계획을 국소로, 메인 수집 비용 (2026-11-16 새벽)

작업 폴더 `E:\StutterFixWork\wt-m4` (브랜치 claude/drfull). 커밋·게임 실행·설치 DLL 교체 금지. 한국어 보고. 결과: `docs/direct-renderer/19-m4n-result.md`.
앞 문서: `18-m4m-spec.md`, `18-m4m-result.md`.

## M4m 실게임 (Exit 88초, 3440, JALib 끔, 안전 모드 0)
- A1 329.9 / **B1 224.5 / B2 217.3** / A2 329.7 (67%). M4l 159 에서 회복, M4k 233 수준.
- 상수 고침 확인: cache_constants_us 41(2,800 에서), constant_evaluations 프레임당 ~60.
- phase 합계 B 4.45ms vs A 3.03: FinishFrameRendering 1.98(A 0.86), LateUpdate 0.62(A 0.41 근처).
- **렌더 스레드 1,018µs = rt_cache_plan 435 + record 180 + apply 122 + pose 118 + execute 56 + constants 41 + cull 14**. cache_plan_builds 509 / 3,157 프레임(replans 519, order_key_changes ~3,600) → **재계획 한 번이 약 2.7ms(전체 계획 재작성)**. 
- **메인 931µs = collect 482 + pose 246 + sort 174 + packet 28**, main_max 20ms, max_pose 9.7ms, max_sort 3.5ms.
- GPU: 메인 카메라 4,512(A ~2,400), native 3,219(B2 1,837), gpu_alpha 3,324, effects 1,188.

## 할 일 (목표: Exit B > A)
1. **국소 재계획**: 순서가 바뀐 항목이 속한 segment(또는 batch run)만 다시 계획·기록한다. 나머지 segment 의 명령 목록·계획은 그대로. 재계획 1회 비용 < 50µs. order_key_changes 의 대부분이 실제 상대 순서를 바꾸는지 확인(거리 키만 바뀌고 이웃과의 순서는 같으면 재계획 불필요).
2. **메인 collect 482µs**: 무엇을 매 프레임 순회하는지(전체 sources? 후크 큐?) 계측하고 변경된 것만 O(변경 수)로. pose 246 은 TransformAccessArray 잡의 Complete 대기인지, max_pose 9.7ms 스파이크(배열 재할당?) 제거. sort 174 는 증분이어야 한다.
3. GPU native 1.8~3.2ms(M4k 1.49): 간접 draw/배치 방식이 GPU 를 더 쓰는지 cache-off 대조로 확인하고 더 싼 쪽.
4. HELLO 픽셀 회귀(M4l PSNR 40.06, 기준 42.8) — 남은 측정 결과 `E:\StutterFixWork\night-results\m4m-*.txt/.log` 확인.
- 4종 빌드 오류 0, export 보존, fixture. 계획 m4n 접두사. 막히면 비정상적인 아이디어도 빠르게 시도.

## 추가 (Claude, M4m 나머지 실측)
- Exit cache off B1 = **238.1 FPS** (cache on 224.5/217.3, cache indirect 217.9). cache off: main 925, render 1,107, native GPU 1,678. → 지금 캐시는 재계획 비용 + GPU 추가 비용 때문에 손해. 국소 재계획 후 다시 비교.
- **DD 원래 크기 A1 243.3 / B1 160.2** (M4l 179.8). DD B1: main 912(collect 575!), render 799(plan 176, apply 158, pose 154, constants 106, record 75, execute 59), grab_copies 프레임당 ~11, **메인 카메라 GPU 5,482 / native 5,010**(A 카메라 전체 ~3,500), phase 합계 6.24ms(A 4.11). DD 는 **GPU 병목** — 우리 native 그리기 GPU 를 원본 수준(맵 내용 몫 ~1.5~2ms)으로 내려야 한다. 후보: Grab 복사 횟수/크기(원본 Unity 와 같은 장면에서 Grab draw 수 비교), 사전 변환 compute, 간접 인자/컬링 dispatch, 오버드로(화면 밖·완전 투명 draw), 상태 전환. GPU timeline 으로 항목별 µs 를 state 에.
