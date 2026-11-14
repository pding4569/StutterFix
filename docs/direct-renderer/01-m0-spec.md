# 코덱스 작업: 직통 렌더러 M0 — 뼈대와 스트레스 측정 (2026-11-14)

작업 폴더 `E:\StutterFixWork\wt-v14prod` (브랜치 claude/adofai-renderer). 커밋·게임 실행·설치 DLL 교체 금지(Claude 가 한다). 한국어로 보고.
먼저 `docs/direct-renderer/00-design.md` 를 읽어라(왜 새로 만드는지, 과거 실패 표). 결과: `docs/direct-renderer/01-m0-result.md`.

## 목표
게임 안에서 **Unity 객체 없이** 네이티브가 계산하고 네이티브가 직접 그리는 경로를 세우고, 그 경로의 비용을 합성 부하로 잰다. 이번 단계는 실제 장식을 다루지 않는다(M1). AUTOTEST/DEV 전용, 기본 꺼짐, 제품 동작 변화 0.

## 만들 것
1. **네이티브 모듈** `native/sfnative/direct_renderer.{h,cpp}` (기존 framegen/effects_gpu 의 D3D11 렌더 스레드 이벤트·상태 저장/복원·셰이더 바이트코드 포함 방식을 재사용):
   - 인스턴스 버퍼(동적 또는 링 구조, 프레임당 Map 1회 또는 UpdateSubresource 1회): 인스턴스당 2x3 행렬(월드), 색 RGBA, UV 사각형, 텍스처 층 번호, 시차/카메라 모드 플래그.
   - 정점 셰이더: SV_VertexID 로 사각형 꼭짓점 생성, 카메라 상수(뷰·투영·카메라 위치)와 시차 계수로 화면 위치 계산 — 카메라가 움직여도 CPU 쓰기 없음.
   - 픽셀 셰이더: Texture2DArray 샘플 × 색, 출력 선곱(rgb*a, a), 블렌드 One/OneMinusSrcAlpha (Sprites/Tile 과 같은 블렌드).
   - 대상: 메인 카메라 camRT. C# 이 `RenderTexture.GetNativeTexturePtr()`(또는 colorBuffer 네이티브 포인터)를 넘기고 네이티브가 RTV 를 만들어 캐시(크기 바뀌면 다시).
   - 그리기: 정렬된 인스턴스 배열을 같은 (텍스처 배열, 블렌드) 연속 구간마다 DrawInstanced 1회.
2. **네이티브 트랙 평가기** `native/sfnative/direct_tracks.{h,cpp}`:
   - 트랙 = 인스턴스 × 채널(posX, posY, rot, scaleX, scaleY, r, g, b, a) 의 시간순 구간 배열(시작 t0, 길이, 시작값, 끝값, 이징 번호). 이징은 DOTween 기본 이징 함수와 같은 식(최소 Linear, InQuad/OutQuad/InOutQuad, InSine/OutSine/InOutSine, OutBack, OutExpo — 표로 늘릴 수 있게).
   - 채널별 커서(시간이 앞으로 가면 O(1) 전진, 뒤로 가면(seek) 이진 탐색으로 재설정).
   - **활성 집합**: 지금 움직이는 트랙만 평가(구간 시작/끝 시각을 이벤트 큐로). 멈춘 인스턴스는 지난 값 그대로.
   - 작업 스레드 풀(영구 스레드, 코어 수-2 이하, 기본 3)로 나눠 평가 → 인스턴스 버퍼 스테이징에 직접 씀. 화면 밖·알파 0 인스턴스는 그리기 목록에서 뺀다(압축은 스레드별 구간 후 합치기).
   - 메인 스레드 비용 = 시작 신호(곡 시각·카메라 넘김) + 렌더 이벤트 전 완료 확인뿐. 시작은 PlayerLoop 앞쪽(예: Update 직후), 렌더 이벤트에서 완료 대기(대기 시간 측정).
3. **C# 쪽** `DirectRenderer.cs`(AUTOTEST/DEV): Texture2DArray 생성(합성 텍스처 M 장, 같은 크기), 메인 카메라 CommandBuffer(CameraEvent.AfterForwardAlpha)에 플러그인 이벤트 1개, PlayerLoop 시작 지점 등록, Main.Unload·맵 바뀜·자동 시험 종료 때 전부 해제.
4. **자동 시험 명령** `dr bench <N> <M> [anim%=100]`: 화면 안에 무작위 사각형 N 개(크기 다양, 일부 화면 밖·알파 0), 텍스처 M 장, 60초짜리 무작위 트랙(구간 0.2~2초 반복) 생성 후 켬. `dr state`: 프레임 수, 평균/p95 — 메인 스레드 시작·확인 µs, 작업 스레드 평가 µs(스레드 합/벽시계), 렌더 이벤트 µs(대기 포함/제외), 그린 인스턴스 수, draw 호출 수, 활성 트랙 수. `dr off`.
   - 그림 확인용 `dr shot <이름>`: 기존 framecmp grab 을 쓰면 되도록 상태 줄만. 사각형이 보이는지는 Claude 가 캡처로 확인.
5. bench_v14 예시(결과 문서에 적기, 공용 파일 수정 금지):
   `CFG2["drB"] = (dict(CFG2["rlNR"][0]), list(CFG2["rlNR"][1]))`, `CFG_MID["drB"] = ["dr bench 30000 64", "wait 2", "dr state"]`, `CFG_POST["drB"] = ["dr state", "dr off"]`, 비교 대상 rlNR. N 은 3000/30000/100000 세 가지를 Claude 가 돌린다.

## 합격선 (Claude 가 게임에서 잼)
N=30000, 70% 움직임: 메인 스레드 추가 < 0.1ms/프레임, 렌더 이벤트 < 0.4ms, draw 호출 = 텍스처 구간 수(합성 부하에서는 1~수 개). GPU 시간은 따로 보고(덧칠 크기에 따라 달라짐).

## 검증 (코덱스)
- 4종 빌드(Native, Player, PlayerAuto, Dev) 오류 0, 기존 native export 이름·순서 보존(새 export 는 뒤에 추가), 일반 Player 빌드에 dr 문자열 없음.
- 트랙 평가 오프라인 fixture: 이징별 값이 C# DOTween 식과 같음(ULP 차이 보고), seek 앞뒤, 활성 집합 시작/끝 경계, 스레드 수 1/3 결과 같음.
- 셰이더: 오프라인 D3D11 장치(WARP 가능)로 사각형 4개 그려 예상 픽셀 대조.
- 할당: 켠 뒤 매 프레임 관리 힙 할당 0(코드 근거).

## 막히면
틀에 얽매이지 말 것: 인스턴스 버퍼 대신 StructuredBuffer+SV_InstanceID, 컴퓨트로 평가(GPU 평가는 deco-v2 체인 실패가 있으니 CPU 우선), Unity 의 `Graphics.RenderPrimitives` 로 그리기(네이티브 그리기가 막힐 때 대안, 단 계산은 네이티브). 무엇을 확인했고 무엇이 미확인인지 결과에 구분.
