# Codex 작업: 판정 오차 막대 눈금 투명도를 셰이더로 (2026-11-23)

작업 폴더 `E:\StutterFixWork\wt-v14prod`, 브랜치 `claude/v3-282port`. **게임 실행·종료, 설치 DLL 교체, git 커밋/푸시는 하지 않는다.** 게임 측정은 Claude 가 한다. 작업 트리의 다른 커밋 안 된 변경(Claude 진단, 앞선 Codex Quartz 작업)을 덮거나 되돌리지 않는다.

## 측정 근거 (Claude, 2026-11-23, HELLO 2026 게임 화면 자동 플레이 12~52초, 판마다 새로 켠 ABBA)
- 막대 캔버스 전체 끔: +3.3% (11-22).
- `HitMeterFade.ProbeNoAlpha`(= `HitMeterFade.Fade` 의 DOTween 설정 함수에서 `cr.SetAlpha` 만 막음, 화면은 달라짐): base 312.5/319.5 → noalpha 325.2/327.4 FPS, **+3.3%**. 즉 막대 비용은 거의 전부 보이는 눈금(채움 60 + 외곽선 60)의 CanvasRenderer 투명도를 매 프레임 바꾸는 데서 나온다(캔버스 다시 묶기 `UI::Canvas::UpdateBatches`, `CanvasRenderer.SetAlpha` 호출, 설정 람다).
- HELLO 는 판정이 초당 20번 넘게 와서 3초 수명의 60눈금이 늘 다 보인다. 앞선 후보(다 사라진 눈금 숨기기)는 숨긴 것 0 이라 접었다(`hitmeter-batch-20261123-result.md` 맨 위).
- 막대는 `ScreenSpaceOverlay` 캔버스라 framecmp 캡처에 안 찍힌다. 실제 화면 비교는 AutoTest `shot`.

## 목표
화면 픽셀이 **바이트까지 같은 채로**, 매 프레임 눈금마다 `CanvasRenderer.SetAlpha` 를 부르지 않게 한다.

## 설계 (권장, 더 나은 방법이 있으면 근거와 함께 바꿔도 된다)
1. `HitMeterFade.Fade` 의 빠른 길(이미 있음: 정점 색은 시작 색 그대로, 렌더러 투명도만 바꿈)에서 설정 함수가 `cr.SetAlpha(x.a / a0)` 대신 눈금 슬롯의 float 배열 값만 쓴다. DOTween 트윈·완료·Kill 은 그대로 둔다(값이 원래와 같은 DOTween 계산에서 나온다).
2. 프레임당 한 번(캔버스 그리기 전: `Canvas.willRenderCanvases` 또는 LateUpdate 끝) `Shader.SetGlobalFloatArray`(또는 전용 재질 `SetFloatArray`) 로 올린다. 배열 크기 고정(눈금 슬롯 수), 매 프레임 할당 0.
3. 눈금 Image(Body/Outline)에 전용 재질을 준다: UI/Default 와 같은 계산 + 슬롯 투명도 곱. 슬롯 번호는 정점 데이터(예: UV1, `IMeshModifier`/`BaseMeshEffect` 또는 `Image` 대체 없이 메시 수정)에 한 번 넣는다. 전용 재질이 모든 눈금에서 같으면 묶음 수·그리기 순서가 원래와 같아야 한다(확인 항목).
4. **바이트 같음의 핵심**: 원래 경로에서 화면 알파 = 엔진이 정점 색(Color32) × 렌더러 투명도(float)를 묶을 때 만드는 값(양자화·반올림 방식 미확인). 셰이더가 같은 바이트를 만들어야 한다. 그러므로 먼저 **AutoTest 탐침 명령**(예: `alphaprobe`)을 만든다:
   - `ScreenSpaceCamera` 캔버스 + 전용 카메라 → 작은 RenderTexture(ARGB32, 검은 바탕).
   - 흰 스프라이트(Body 와 같은 스프라이트 사용 권장) Image 들을 격자로 두고, 정점 알파 바이트 v(0..255 중 표본, 실제로는 Graphic.color.a 를 v/255 로)와 렌더러 투명도 c(실제 DOTween 페이드가 내는 값 범위의 많은 표본, 예: 0..1 4096개 + 경계 값) 조합으로 그린다.
   - 같은 장면을 (a) 원래 방식 `cr.SetAlpha(c)` (b) 새 재질 + 배열 c 로 그려 ReadPixels 로 바이트 비교. 불일치 수·첫 사례를 로그에 남긴다. 셰이더 반올림식은 (a)의 결과에 맞춰 정한다(예: `round(v*c)`, `floor(v*c+0.5)`, float 곱 그대로 등 후보를 모두 시험).
   - 탐침은 카메라/캔버스/RT 를 만들고 끝나면 지운다.
5. 셰이더는 Unity 6000.3.10f1 로 번들 빌드가 필요하면 기존 방식(`out/hl-singlearray-lab` 등 Unity batch 빌드, 저장소의 셰이더 번들 빌드 절차)을 따른다. 기존 번들 로딩 코드(GpuDeco 등)를 참고한다.
6. 설정 `HitMeterShaderAlpha`(기본 **꺼짐**), AutoTest `meteralpha on|off|state|probe`. 다른 모드가 눈금 재질·색을 바꾸면(시작·끝 RGB 다름 등 이미 있는 느린 길 조건, 재질이 기본이 아님) 그 눈금은 원래 길. 예외 시 원래 길로 돌아가고 재질을 원래대로. `Main.Unload`·맵 전환에서 원래 재질·렌더러 투명도로 되돌린다.
7. 켬→끔 전환 때 남은 눈금의 렌더러 투명도를 배열 값으로 맞춰 둔다(화면이 튀지 않게).

## 확인 (Claude 가 게임에서)
- `alphaprobe`: (a)/(b) 불일치 0.
- 실제 화면: 게임 화면 HELLO 20초, `pause` 뒤 `shot` 켬/끔 비교(우리 표시판·끊김 알림은 `OverlayMode 0` 으로 끈다). 막대 영역 픽셀 차이 0.
- 성능: `ab_steps.py`/설정 ABBA, HELLO·Windflower 12~52초.

## 결과물
- `docs/handoff/hitmeter-shaderalpha-20261123-result.md`(한국어): 설계·근거, 변경 파일(명시적 경로), 시험 명령 줄 목록, 남은 위험.
- 빌드: `dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1`, `-c Debug -p:Edition=Player`, `-c Release -p:Edition=Player`, LoadCheck 오류 0.
