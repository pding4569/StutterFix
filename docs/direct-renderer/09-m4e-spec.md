# 코덱스 작업: M4e — HELLO 색 불일치, 잡음 대조, 켜기·재계획 비용 (2026-11-15 저녁)

작업 폴더 `E:\StutterFixWork\wt-m4` (브랜치 claude/drfull). 커밋·게임 실행·설치 DLL 교체 금지. 한국어 보고. 결과: `docs/direct-renderer/09-m4e-result.md`.
앞 문서: `08-m4d-spec.md`, `08-m4d-result.md`. Claude 가 고친 것(이미 커밋 예정): native Create 용량 한도 100000→4000000, ring bytes 예약 min(capacity,8192)*1KB, 관리 capacity = 렌더러 + 캔버스 수 + 64 (HELLO 는 캔버스 14,924×4 로 한도를 넘어 `DRF native Create hr=0x80070057`).

## M4d 실게임 결과 (3440 렌더 요청, 캡처는 2560x1600, pause 상태 d1=on, d0=off, d00=off 다시)
| 맵 | FULL ON | 픽셀 (UI 왼쪽 가운데 칸 = FPS 숫자 제외) | 비고 |
|---|---|---|---|
| DD 50초 | native 11871, unityExplicit 48, sources 48014, setup 27.1초 | **차이 0 (비트 동일)** | aux RT 2, 살아있는 캔버스 그래픽 5 |
| Exit 88초 | native 3353, explicit 23, sources 30317, setup 17.7초 | 최대 채널 2, >3 인 픽셀 0 | 11% 픽셀에서 ±1~2 (반올림) |
| Windflower 258초 | native 3123, explicit 104, sources 75173, setup 20.3초 | 8.9% (잡음 0.49%) — 그림 미확인, 다시 잼 | 고치기 전 native 로 잰 판 |
| **HELLO 50초** | native 2047, explicit 68, sources 47695, setup 24.3초 | **65.8% 다름, 배경 전체 색이 다름** | 아래 |

### HELLO 색 불일치
- 그림: `E:\StutterFixWork\night-results\m4d-img\hl50-d0.png`(off), `hl50-d1.png`(on), `hl50-cmp.png`(위 off, 아래 on, 상하 뒤집힘), `hl50-diffmask.png`.
- 타일·행성·UI·밝게 포화된 가운데는 같고, **배경 장식 층 전체 색이 다르다**: off 는 초록빛, on 은 붉은/보라빛. 영역 평균 RGB: 왼쪽 off (45,70,30) on (66,46,39) / 오른쪽 off (0,70,7) on (13,39,35). 오른쪽 끝에 on 에만 파란 띠.
- 후보: SpriteRenderer.color/재질 _Color 또는 MPB 색이 안 들어감(색 정점 버퍼 slot7 기본값), 블렌드 모드(Hidden/BlendModes/*·Grab 계열, Multiply/Overlay/Screen 등)의 Grab 원본이 틀림(직전 색 타깃 복사 시점), 정렬 순서가 바뀐 큰 배경 장식, 셰이더 키워드 변형 선택 오류, 감마/sRGB 뷰 포맷. HELLO 50초 화면에 있는 장식들을 census 해서 원인을 **증명**하고 고쳐라. 오프라인 재현이 가능하면 그 장식 하나를 WARP 로 Unity 값과 대조.
- 이것이 고쳐질 때까지 HELLO 는 사용 불가.

### 잡음 대조(d0 vs d00) 실패
- 모든 맵에서 off/off 가 0.34~0.88% 다르고 위치가 늘 왼쪽 가운데(framecmp 좌표 bottom-left (32..352, 736..832)). 게임 FPS 표시 숫자/UI 로 보인다(HELLO 는 on 캡처에서 FPS 숫자가 0). framecmp 의 `assertzero d0 d00` 가 그 때문에 매번 게임을 끈다. 그 UI 요소를 찾아 캡처 동안 고정하거나 framecmp 가 그 요소만 제외(명시적 이름/RectTransform, 위치 하드코딩 금지)하게 하라. 다른 맵에서 잡음 0 을 유지해야 함.

## 성능 (지금이 핵심 목표: 원본보다 압도적으로 빠르게)
- setup 17~27초: 재생 중 켜기로는 쓸 수 없다. 목표: 맵 불러오기/재생 준비 안에서 < 1초, 또는 여러 프레임에 나눔. 무엇이 오래 걸리는지 단계별로 재고(sources 수만 개를 FindObjectsOfTypeAll·GetComponentInParent·AddComponent(수명 컴포넌트) 하는 것, 셰이더/입력 layout 생성, 정렬) 줄여라.
- **sources 30k~75k 인데 native 2k~12k**: 꺼진 렌더러(inactive:watch 26,953 등)에 수명 컴포넌트·변환 배열·포즈를 모두 만든다. 꺼진 것은 가벼운 감시만(켜질 때 승격), TransformAccessArray/포즈는 활성 대상만.
- pause 중 첫 3프레임 main_us 9~57ms(최대 140ms), replan_max 6~86ms, commit_max 4~22ms, render_us 4~16ms. 매 프레임 평소 비용 목표: main < 0.3ms, render 스레드 < 1ms(원래 Unity 그리기보다 싸야 의미가 있다). 무엇이 매 프레임 비싼지 state 에 단계별로(변경 수집, 포즈 업로드, 정렬, 패킷, 렌더 스레드 상수 쓰기/드로우) 나눠 기록하라.
- Claude 가 지금 DD·Exit ABBA(FPS)를 재는 중. 결과는 `E:\StutterFixWork\night-results\m4d-{dd,sh}-{A1,B1,B2,A2}.txt` 에 생긴다(끝나면 읽어서 반영).

## 검증
- 4종 빌드 오류 0, export 보존, 기존 fixture 통과.
- 결과 문서에 Claude 가 돌릴 계획(HELLO/WF 픽셀 다시, 4맵 ABBA) 경로.
- 막히면 비정상적인 아이디어도 빠르게 시도. 실측 미확인 구분.

## 추가: DD·Exit ABBA 결과 (Claude, 이 문서 작성 뒤)
| 맵 | A1 | B1 | B2 | A2 |
|---|---|---|---|---|
| DD 50초 | 248.5 | **22.2** | **28.5** | 242.0 |
| Exit 88초 | 327.9 | **68.5** | **41.1** | 326.4 |
- DD B1 state: frames=278 **replans=279(매 프레임 재계획)**, main_us 평균 9,072 / 최대 134,939, replan_max 75,538, render_us 1,840, commit_us 60, native=1945(켤 때 11871 에서 줄어듦). 매 프레임 재계획이 1순위 원인. 무엇이 재계획을 일으키는지(어떤 변경 알림이 매 프레임 오는지) 찾아 없애라.
