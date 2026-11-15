# 코덱스 작업: M4h — DD GPU 31ms 의 정체 + Exit 남은 CPU 비용 (2026-11-15 밤)

작업 폴더 `E:\StutterFixWork\wt-m4` (브랜치 claude/drfull). 커밋·게임 실행·설치 DLL 교체 금지. 한국어 보고. 결과: `docs/direct-renderer/12-m4h-result.md`.
앞 문서: `11-m4g-spec.md`, `11-m4g-result.md`. 측정 환경 변경: 이제 측정 판은 JALib 을 끄고 돈다(JALib 이 Harmony 를 자주 깨뜨림). A/B 는 같은 조건.

## M4g 실게임 (3440 렌더, 안전 모드 0)
| 맵 | A1 | B1 | B2 | A2 |
|---|---|---|---|---|
| Exit 88초 | 376.6 | 233.4 | 234.5 | 372.7 |
| DD 50초 | 267.3 (JALib 켜짐) | **24.3** | | |
- Exit B1: main_us 994, main_max 8,373, render_us 2,068, replans 518/약 2,800프레임, rt_upload 약 0.7MB/프레임(12MB 에서 줄어듦), grab_copies 9,181(이름 없는 Grab, 프레임당 약 3), named_grab_hits 0, 메인 카메라 GPU 3,252µs (A 2,393~2,402). 픽셀(일시정지): 잡음 0, on/off 최대 2·11.3% 픽셀 ±1~2 (M4d 부터 계속 같음).
- **DD B1**: frames 356, main_us 1,538, render_us 1,888 (rt_constants 1,056, rt_execute 4,362 표본), replans 273, grab_copies 3,915(프레임당 11, grab_bytes 77.6GB = 19.8MB/회), **메인 카메라 GPU 31,469µs (A1 3,567)**, 그런데 우리 native 이벤트 GPU(`gpu_event_us`) 평균 1,087µs(최대 18,047). → **28ms 가 우리 native 이벤트 바깥**(같은 메인 카메라 안)에 있다. unityExplicit=48(파티클·LineRenderer·미지원 셰이더 DrawRenderer), 보조 카메라(캔버스), 필터, 깊이/스텐실 상태, Unity 가 우리 뒤에 다시 하는 일 등.

## 할 일
1. **메인 카메라 GPU 를 구간별로 재라**: 우리 CommandBuffer 의 각 segment 사이(명시적 DrawRenderer 묶음 앞뒤, 보조 카메라 합성, opaque/before-alpha 경계)에 플러그인 이벤트로 GPU timestamp 를 찍어 "native 그리기 / Unity 명시 DrawRenderer / 합성 / 그 밖" 으로 나눠 state 에. 진단 스위치 `drfull skip explicit`(명시 DrawRenderer 를 건너뜀 — 픽셀은 달라져도 됨, 비용 원인 확인용)도.
2. 원인을 고쳐라. 후보: 명시 DrawRenderer 로 그리는 파티클이 Unity 원래 경로와 달리 컬링/배칭/LOD 없이 그려짐(Unity 는 화면 밖 파티클을 안 그림), DrawRenderer 가 모든 패스(-1) 로 그려 패스가 늘어남, 재질 인스턴스화, 스텐실/깊이 상태가 Unity 의 조기 거부를 막음, 이름 없는 Grab 의 복사 대상 크기.
3. Exit: main 0.99ms·render 2.07ms·GPU +0.86ms 를 예산(전체 < 0.95ms) 안으로. replans 518 원인(order_key_changes)·rt_constants·grab 3회/프레임(이름 없는 Grab 은 Unity 도 매번 복사하지만 Unity 는 화면 밖이면 안 그린다 — 화면 안 블렌드 장식만 복사하는지), main_max 8ms 스파이크.
4. 반올림 ±1~2(Exit 11%): 원인 한 가지를 증명(어느 셰이더/장식에서, 정점 변환·보간 정밀도·블렌드 순서 중 무엇).
- 4종 빌드 오류 0, export 보존, fixture 통과. 계획은 m4h 접두사(4맵 pixel + ABBA + DD 진단판 `skip explicit`).
