# GPU 무손실 절약 점검 결과 — 2026-10-19

## 결론

**현재 증거로 비트 동일을 보장하면서 기본 켬으로 넣을 새 GPU 최적화는 없음. 이번 제품 적용은 불가다.** 제품 C#·프로젝트·설정 변경 0, 새 Harmony/렌더 콜백/프레임 루프 0, 승인된 추가 절약 0ms다. 이는 모든 무손실 GPU 최적화가 원리적으로 불가능하다는 결론은 아니다. 남은 후보와 필요한 증명은 아래에 분리했다.

게임 실행·게임 명령 전송·Mods 설치·커밋·푸시를 하지 않았다. 독립 Unity Editor도 실행하지 않았다. 앞 작업 A·B가 반영된 HEAD `211a6d85bc5066a5b8453a49c78ae54f09a4d08e` 위에서 읽기 전용 조사와 로컬 빌드만 했다. 기존 `.codex/config.toml` 변경 및 사용자 미추적 파일은 보존했다.

새 런타임 기능이 없으므로 `gpufree-off` 토글과 `gpufree verify` 명령도 만들지 않았다. 존재하지 않는 명령으로 A/B를 시키지 않는다. 향후 후보를 구현한다면 설치 시 `Main.Entry.Path/gpufree-off`를 확인하고, 모든 새 패치·자원·상태를 `Main.Unload`에서 되돌리는 조건이 필요하다.

## 근거와 수치 해석

이번에 직접 확인한 자료:

- 기존 `codex-gpu-20261018-{camera-il,result}`, `codex-gpu2-20261018-result`, `codex-gpu3-20261018-result` 및 관련 소비자 IL. 실패한 정렬·아틀라스·Tight 시험은 반복하지 않았다.
- `%LOCALAPPDATA%/StutterFix/gpu-census-*.tsv` **6개**. 최신 것은 `gpu-census-20261018-223220-954.tsv`, frame 101526, 3440×1440, RTX 4060 Ti, D3D11이다. 이 snapshot의 정확한 곡 시각은 TSV만으로 확정하지 않았다.
- 현재 게임 에셋의 셰이더 객체 **428개**, 카메라 객체 **731개**, 읽기 오류 **0개**. 그중 게임 관련 셰이더 366개와 모든 카메라의 원시 트리는 임시 폴더에 추출했다. 같은 이름/용도의 에셋도 개별 객체로 센 수다.
- Legacy 셰이더 **37개**와 `Sprites/Tile`, `ADOFAI/FloorMesh`, `ADOFAI/zWriteUI`, `Hidden/VideoBloom`의 D3D11 프로그램 **95개**를 `d3dcompiler_47!D3DDisassemble`로 읽었다. 현재 활성 8종 Legacy의 픽셀 연산과 C# `OnRenderImage`, VideoBloom IL을 자세히 대조했다. 비활성 29종까지 모든 파라미터 조합의 중립성을 검증한 것은 아니다.
- 지정된 Arche 안개 PNG **4개**의 모든 RGBA 픽셀을 검사했다. 게임에서 줄이기·압축한 상주 텍스처의 readback은 하지 않았다.

근거 파일은 [evidence.json](codex-gpufree-20261019-evidence.json), [실제 셰이더 어셈블리](codex-gpufree-20261019-evidence-shaders.txt), [실제 필터·카메라 IL](codex-gpufree-20261019-evidence-il.txt)이다. JSON에 PNG/census SHA256, 카메라 직렬화 설정, 셰이더 렌더 상태·프로그램별 해시를 보존했다.

GPU 시간은 이번 실측이 아니라 인계 문서의 Claude 실측이다. 본 카메라 `C(1)=3.74`, `C(.5)=1.58ms`에서 `B=(3.74-1.58)/.75=2.88ms`, `A=.86ms`다. 따라서 `C(s)≈.86+2.88s²`. 이 B는 픽셀 수에 따라 변한 전체 비용이며 텍스처 대역폭·블렌딩·깊이 비용을 각각 분리한 측정이 아니다. AABB 화면 93배도 실제 셰이딩 픽셀 수가 아닌 상한이다.

인계에는 전체 GPU 약 4.7ms와 probe 프레임 전체 3.9ms가 함께 있다. 동일 표본/동일 계측 범위라는 근거 없이 합치거나, 본 카메라 3.7에 필터 0.8을 다시 더해 새 측정치로 만들지 않는다. 아래 절약 범위는 모두 **추정**, 각 항목을 더할 수 없다.

|항목|조건부 절약 추정|이번 채택/승인 절약|핵심 제약|
|---|---:|---:|---|
|깊이·스텐실 제거/축소|0~0.2ms 수준의 탐색 가설, 직접 분리 미측정|0ms|24를 이미 요청, 공유 RT·ZTest·마스크 필요성 미증명|
|RT 형식·clear·discard|MSAA 추가 절약 0ms; clear/discard 0~0.2ms 탐색 가설|0ms|색 보존·배경 초기화·카메라 누적 내용 필요|
|필터 중립 경로/복사 감소|중립 필터당 0~0.15ms, 필터 합 약 0.8ms 범위 안|0ms|현재 값 미확인, 알파·중간 양자화·동일성 미검증|
|안개 R8+상수 RGB|백분율 상한 가설 2.88×.75=2.16ms, 실제 기대값으로 사용 불가|0ms|상수 RGB 조건 4개 모두 실패; 대역폭 병목 비중도 미측정|
|출력/플립|GPU 렌더 절약 미측정; 지연은 과거 15.8→7.9ms|0ms|지연과 렌더 비용 구분, 과거 FPS 209→187|
|클럭·전력·PCIe|수치 산정 불가|0ms|모드에서 무손실·무부작용 제어 보장 불가|
|그 밖의 새 후보|0ms 보장, 양의 절약 미측정|0ms|기존 최적화 중복 및 반투명 순서 의존|

깊이/clear 범위는 모델에서 도출된 예측치가 아니다. 예컨대 3440×1440×4byte 한 번의 표면 접근은 19.8MB이며, 유효 대역폭을 임의로 100~200GB/s라 가정하면 약 0.10~0.20ms다. fast clear·캐시·압축·실제 접근량에 따라 0에 가까울 수도 있다. 이 가정을 RTX 4060 Ti의 측정 대역폭으로 쓰지 않는다.

## 1. 깊이·스텐실: 제거 불가, 24 재요청은 새 최적화가 아님

`scrCamera.SetupRTCam`은 RT를 생성하지 않는다. `useRTCam`에 따라 `camRT` 또는 null을 BGStatic/BGMoving/본 카메라의 `targetTexture` 셋에 함께 넣고 Overlay와 quad를 활성화한다. 실제 생성은 `scrCamera.Update`의 화면 크기 변경 분기다: 기존 RT Release → `Screen.width`, `Screen.height`, **24** → `new RenderTexture(...)` → SetupRTCam(true). 최신 census는 이 요청의 결과를 **depth=32**로 보고한다. 실제 깊이/스텐실 DXGI 포맷은 census에 없으므로 24→32의 구체적인 포맷 선택 원인은 미확정이다.

최신 snapshot에서 BGStatic/BGMoving/Camera는 모두 RT `#-2763642`를 공유한다. Overlay는 backbuffer다. 따라서 카메라마다 깊이를 독립적으로 빼는 구조가 아니다.

|실제 셰이더|ZWrite|ZTest|직렬화 stencil|
|---|---|---|---|
|Sprites/Tile|Off|LEqual(4)|Always(8), Keep(0)|
|ADOFAI/FloorMesh|Off|LEqual(4)|Always, Keep|
|ADOFAI/zWriteUI|On|Less(2)|`_StencilComp`, `_StencilOp` 파라미터|

Tile/FloorMesh가 깊이를 **쓰지 않는 것**은 확인했다. 하지만 **검사하지 않는 것**은 아니다. 앞 카메라/다른 렌더러가 쓴 깊이를 소비할 가능성이 있으며, 카메라 후보 전부가 이 두 셰이더라는 증거도 없다. zWriteUI 에셋의 존재는 위험의 구체적 예이며, 그 에셋이 이번 Arche snapshot에서 실제로 그려졌다는 증거로 쓰지는 않는다.

게임 `scrVisualDecoration`은 SpriteMask의 sprite·정렬 범위와 SpriteRenderer.maskInteraction을 바꾼다. SpriteMask의 네이티브 그리기 상태를 Tile의 정적 stencil Always만으로 배제할 수 없다. Unity 공식 문서도 sprite mask 사용 시 depth/stencil buffer 필요성을 설명한다([Sprite mask](https://docs.unity.com/en-us/engine/6000.7/manual/unity2d/sprite/mask/hide-reveal-parts-sprite-mask), 이 페이지는 URP 설명이므로 현재 Built-in Player의 실제 draw 판정을 대신하지 않음).

**16bit/0depth 변경은 승인 불가.** 정밀도 변경·스텐실 상실에 대한 비트 동일 증거가 없다. 24로 다시 설정하는 구현은 게임이 이미 하는 요청과 같다.

후보 구현 지점은 RT 생성 자체를 바꾸는 `scrCamera.Update`의 좁은 transpiler 또는 생성 직후 SetupRTCam prefix다. postfix에서 이미 카메라 셋에 연결한 RT의 descriptor를 뒤늦게 바꾸는 방식은 피해야 한다. 향후 구현 시 HalfRender/FrameGen의 RT 교체, 화면 크기 변경, custom FPS, unload 때 원래 RT/카메라 연결/quad 재질 복구까지 필요하다. 이번에는 패치하지 않았다.

검증하려면 같은 고정 장면에서 깊이/스텐실 원본과 대체를 별도 RT에 렌더하여 alpha 포함 RGBA byte를 비교해야 한다. 겹친 서로 다른 z, 타일/행성/텍스트/파티클, SpriteMask 안/밖, 배경 카메라 누적, 화면 회전·확대·재시작을 포함한다. 해당 구간 한 장의 성공은 모든 맵의 기본 켬 근거가 아니다.

## 2. RT 형식·clear·discard

현재 게임 `resources.assets`와 게임 카메라 구성을 담은 `level1`에서 다음 설정이 같다. 이는 **직렬화 기본값**이며 현재 재생 중 값의 readback이 아니다. 최신 census는 clearFlags/backgroundColor를 출력하지 않는다.

|카메라|clearFlags 기본값|의미|현재 snapshot 출력|
|---|---:|---|---|
|BGStaticCam|2|SolidColor, level1 배경 RGBA≈(.007843,.007843,.011765,1)|공유 RT, HDR=False, allowMSAA=False|
|BGMovingCam|3|Depth, 이전 색 유지|같은 RT, HDR=False, allowMSAA=False|
|Camera|4|Nothing, 앞 카메라 결과 유지|같은 RT, HDR=False, allowMSAA=False|
|OverlayCam|2|SolidColor, black alpha1|backbuffer, HDR=False, allowMSAA=False|

색 RT는 최신 census에서 `R8G8B8A8_UNorm/ARGB32`, sRGB=False, MSAA=1, mip=1, GPU samples=1이다. backbuffer의 정확한 포맷은 미확인이고 Quality MSAA는 0이다. **MSAA를 끄는 추가 이득은 없다.** RGB565/반정밀도/색 공간 변경은 값·알파·중간 반올림을 보존하지 못한다. RGB24도 같은 GPU 저장 형식으로 처리될 가능성이 있고 알파 소비자를 배제하지 못해 승인 불가다.

BGMoving/Camera 앞에서 discard/DontCare를 쓰면 BGStatic/이전 카메라의 누적 색을 잃을 수 있다. BGStatic 또는 Overlay의 clear를 없애려면 그 뒤 첫 그리기가 viewport 전체 RGBA를 항상 덮는다는 증명이 필요하다. 투명 안개와 backgroundColor alpha1만으로는 충분하지 않다. clear를 사각형 draw로 대체하면 shader·블렌딩·깊이·scissor 조건과 추가 draw 비용까지 생긴다.

Unity의 GetTemporary는 이미 크기/형식별 풀을 쓰며 내부적으로 DiscardContents를 사용한다. 이 이점은 특히 타일 기반 모바일·멀티 GPU에서 설명된다([GetTemporary](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/rendertexture/gettemporary)). 새 clear/discard 호출로 데스크톱 NVIDIA에서 같은 이득을 기대할 근거가 없다. **풀을 없애거나 매번 별도 영구 RT로 바꾸지 않는다.** 같은 descriptor라도 사용 중인 두 RT를 하나로 합치면 read/write alias가 생길 수 있다.

후보 위치는 카메라 clear 직전과 실제 첫 draw 범위지만, 확인 없는 렌더 콜백을 추가하지 않았다. 동일성 검증은 첫 프레임·배경 투명·불완전 viewport·누적 렌더·custom FPS 반복 프레임까지 포함해야 한다.

## 3. 블릿 체인과 필터별 중립성

최신 census의 본 카메라 활성 OnRenderImage는 **9개 = Legacy 8개 + VideoBloom 1개**다. 배경 카메라의 나열된 기본 효과는 비활성이다. “필터 약 10개”를 정확히 10회 복사라고 해석하지 않는다.

Legacy 8개 각각의 OnRenderImage에는 shader Blit와 shader가 없을 때의 fallback Blit가 **서로 다른 분기**에 있다. 둘을 합쳐 프레임당 두 번으로 세면 틀린다. 정상 shader 경로는 각 **1회**, 합 **8회**이며 이 8종 C# 코드 안에는 GetTemporary가 없다. Unity가 OnRenderImage 사이에 제공하는 중간 RT는 별개다.

VideoBloom은 추가 다중 pass다. `MasterAmount==0` 또는 `(MediumAmount==0 && LargeAmount==0)`이면 **원래 게임부터 단일 Graphics.Blit 후 반환**한다. 이 조건을 다시 구현해도 GPU 추가 절약은 0이다. 활성 bloom은 높이 약 198에 맞춘 내부 해상도, 임시 RT, downsample/threshold, BloomBlit의 여러 scale·반복 경로와 마지막 composite를 쓴다. 실제 Medium/Large/Kernel/Blend 값이 census에 없어 이번 구간의 정확한 동적 Blit 수는 **미확인**이다. 모든 pass를 전체 화면 크기 복사로 세지 않는다.

마지막 출력은 SetupRTCam의 **Overlay quad draw** 경로도 있다. 이를 Graphics.Blit 1회와 같은 호출로 세면 안 된다. custom FPS가 켜졌을 때의 화면 보관/표시, 다른 모드의 출력, Unity 내부 copy까지 포함한 프레임 전체 정확한 Blit 횟수는 이번 자료로 확정하지 않았다. 같은 RT ID·크기라는 이유로 두 출력 단계 중 하나를 지울 수 없다.

아래는 **실제 게임 DXBC 픽셀 셰이더 + C# 필드 매핑**을 보고 평가한 내용이다. 활성=True는 파라미터가 중립이라는 뜻이 아니다.

|Legacy 필터|픽셀 sample 명령 수|연산·중립 후보|비트 동일 판정|
|---|---:|---|---|
|Distortion_FishEye|1|Distortion **0.5**에서 분기가 원래 UV를 선택. 0을 중립으로 보지 말 것|RGB는 후보, 출력 alpha를 1로 고정. 범용 RGBA identity 아님|
|Color_GrayScale|1|`RGB + Intensity*(dot(RGB,[.222,.707,.071])-RGB)`; Intensity=0 후보|alpha=1 고정. 입력 alpha1 불변식 없이 우회 불가|
|Atmosphere_Rain|8|noise 샘플·왜곡·flash·두 번 Fade 보간. Fade=0 및 유한 입력의 Intensity=0이 후보|텍스처·유한 값·왜곡/flash 경로 포함 검증 필요. 파라미터 이름만으로 중립 승인 불가|
|Color_Chromatic_Aberration|3|Offset=0이면 세 sample 좌표가 같고 RGBA 채널을 재조합|가장 좁은 추가 검증 후보. 실제 RT·UV·알파·sRGB·부호0 포함 byte 대조 미수행|
|FX_Hexagon_Black|1|좌표 양자화·육각 경계 mask. Value가 제수에도 들어감|Value=0은 일반 identity가 아니며 0나누기 위험. 추가하지 않음|
|Sharpen_Sharpen|5|Value=0이면 center×1−평균×0 뒤 이웃 min/max clamp|유한 입력에서 유력 후보. Value2=0만으로 같은 결과라 단정하지 않음. 실제 byte 대조 미수행|
|Color_Contrast|1|`(RGB-.5)*Contrast+.5`, Contrast=1 후보|alpha=1 고정 및 산술/양자화 검증 필요|
|Light_Water2|1|세 개 8회 루프에서 파형 차→UV 이동·밝기 곱. Intensity=0 후보|Speed=0과 identity를 혼동하면 안 됨. 초월함수·유한값·UV 계산 포함 byte 대조 필요|
|VideoBloom|다중 pass|Master/Medium/Large 중립이면 게임이 이미 Blit|중립 경로 추가 최적화 0, 활성 효과는 병합 승인 불가|

sample 수는 DXBC 정적 명령 수이며 cache miss·GPU 메모리 읽기 횟수의 실측이 아니다.

`FilterIdentity.cs`는 **Legacy가 아닌** CameraFilterPack의 Chromatic_Aberration, Colors_Brightness, Distortion_Dream2, TV_Chromatical, Color_BrightContrastSaturation, Color_Contrast **6종**만 등록한다. RT 크기/형식/sRGB/MSAA와 환경이 맞는 외부 승인 기록이 있어야 원래 shader 대신 **Graphics.Blit을 한 번** 한다. 필터 컴포넌트 자체나 Unity 중간 RT/패스를 없애는 기능이 아니다. 이 환경의 승인 파일 내용은 이번에 평가하지 않았고 승인하지도 않았다.

따라서 **Legacy Chromatic Offset=0·Sharpen Value=0**은 기존 목록에 빠진 좁은 후속 검증 후보다. 단순히 Legacy 접두어만 붙여 기존 승인/중립값을 재사용하지 않는다. FishEye의 중립값과 강제 alpha처럼 구현이 다른 예가 실제로 있다. on/off를 위해 Behaviour.enabled를 바꾸면 OnRenderImage의 TimeX 증가와 재개 위상이 바뀔 수 있으므로 이 방법도 자동 채택하지 않는다.

필터를 한 shader로 합치면 이전 RT의 UNorm 저장 반올림/필터링 경계가 사라진다. 같은 실수 수식이라도 `Q(f2(Q(f1(x))))`와 `Q(f2(f1(x)))`는 일반적으로 다르다. 중간 좌표·format·sRGB·alpha·sampler·이펙트 시간 상태까지 같고, 제거할 복사 자체가 byte identity라는 증명 없이는 **블릿 병합 불가**다.

추가 구현 위치는 로드 때 정확한 Legacy OnRenderImage를 패치하는 별도 GpuFree 모듈이 적합하나 이번에는 구현하지 않았다. 우선 독립 D3D11 fixture에서 실제 shader/정확한 바인딩으로 원본 필터와 대체 Blit를 같은 입력에 실행해야 한다. 0..255 RGBA ramp·투명 픽셀의 비영 RGB·checker/경계·NPOT·flip·실제 크기/format/sRGB/sample 수를 포함하고 모든 byte 차이 0을 요구한다. 임의 Sprites/Default fixture로 Legacy shader를 증명할 수 없다. 같은 상태 반복 렌더 0차이와 효과가 있는 값의 양성 대조도 필요하다. 통과 뒤에도 비유한 파라미터/지원하지 않는 RT는 원래 경로로 돌린다.

## 4. 텍스처 샘플러와 R8

|PNG|크기|전체 RGB 종류|alpha>0인 픽셀의 RGB 종류|알파 범위|alpha=0 픽셀|
|---|---:|---:|---:|---:|---:|
|fog.png|682×682|33|33|0..112|95,248|
|nes_large_fog.png|1489×1489|35|35|0..112|1,093,615|
|nev_fog.png|100×100|2|1|0..255|6,707|
|nes_black_fog.png|682×682|2|2|0..112|95,248|

**4종 모두 모든 픽셀 RGB 상수 조건 실패.** nev_fog는 보이는 픽셀만 보면 한 색이지만 alpha0 texel의 RGB가 다르다. Tile은 texture RGBA를 먼저 표본화하고 tint를 곱한 뒤 RGB에 alpha를 곱한다. bilinear에서는 투명 texel의 RGB도 보간에 들어가므로 “alpha0 RGB는 버려도 같다”는 가정은 성립하지 않는다. 검은 안개도 RGB=0..1의 실제 차이가 있다.

census의 mip=1/aniso=1은 추가 밉/이방성 비용이 없다는 근거다. Bilinear→Point는 그림을 바꾸며, 게임 SetPointSamplingVariant와 Tile의 좌표 floor/texel-center 변형도 보존해야 한다. DXT는 손실이라 제외한다.

R8+상수색은 원본 값부터 표현하지 못하므로 **제안대로 구현 불가**다. grayscale+alpha 두 채널(RG8)이나 별도 복원은 다른 후보지만, 표본화·채널 재조합·색 공간·premultiply의 byte 동일성과 게임 Sprite.texture 크기/압축/수명 소비자가 추가 검증 대상이다. 이번에 RG8의 성립을 증명하지 않았다.

가정상 byte 전송량 4→1은 75% 감소지만 fragment 비용 75% 감소가 아니다. 위 2.16ms는 모든 픽셀 비례 비용이 해당 텍스처 전송으로만 이루어졌다는 비현실적 상한 산식이다. 보장 절약 0ms. 검증은 원본 PNG뿐 아니라 게임의 최종 축소/DXT 텍스처와 회전·확대·Point/Bilinear·tint·알파·양자화 경계의 픽셀 비교가 필요하다.

## 5. 제출·플립 모드

기존 화면 출력 실험의 Independent Flip 지연 15.8→7.9ms, FPS 209→187을 참고만 한다. CPU 제한 구간의 제출 지연·표시 지연 개선을 본 카메라 GPU 절약으로 바꿔 쓰지 않는다. GPU 제한 구간에서도 compositor 비용·GPU query 구간·present 대기를 분리한 새 측정 없이는 절약 ms를 산정할 수 없다. exclusive fullscreen이나 MaxQueuedFrames는 바꾸지 않았다. 비트 동일 검증도 렌더 캡처뿐 아니라 해상도/색 공간/출력 변환을 고정해야 한다.

## 6. 클럭·전력·PCIe·VRAM

**모드 내부의 무손실 GPU 렌더 최적화로 분류하면 불가.** 전력 제한/강제 성능 상태/드라이버 프로필/PCIe 링크 상태를 변경하지 않았다. 클럭 상승이 shader byte 동일을 뜻하더라도 시스템·전력 부작용 없는 기본 켬이라는 근거가 아니다. 실제 클럭·전력 throttling·링크 폭·PageIn/Evict는 이번에 측정하지 않았다.

VRAM 이동은 기존 VramGuard·ETW 조사 범위다. 이미지 한도를 낮추는 것은 그림이 바뀌어 이번 0byte 조건 밖이다. 추가 texture preload도 VRAM 압박을 키울 수 있어 “무료 절약”으로 넣지 않는다. 여기서는 새 GPU 시간 수치를 만들지 않았다.

## 7. 그 밖의 후보와 중단 기준

- InvisibleSkip/TileCull/ShaderWarm/FilterIdentity를 다시 구현하지 않는다. 색 alpha0도 별도 블렌드·마스크·깊이 side effect를 포함한 기존 조건을 무시하고 일괄 확대하지 않는다.
- 반투명 장식의 앞뒤 그리기·재질 공유·atlas·mesh 변경은 기존 0byte 반례가 있어 재시험하지 않았다. 중간 색을 오프스크린에 묶어 cache하는 것도 현재 애니메이션·정렬·중간 UNorm 반올림을 보존해야 한다.
- census는 실제 draw·shader 비용 카운터가 아니다. 같은 셰이더/MPB 1개/화면 overdraw 상한만으로 새 절약량을 만들지 않는다.
- **밤새 탐색할 새 제품 경로는 승인하지 않음.** 추가 진행 가치가 있는 것은 Legacy Chromatic/Sharpen의 정확한 중립 입력이 이 구간에 실제 존재하는지 확인하고, 존재할 때만 실제 shader의 독립 byte fixture를 만드는 좁은 후속이다. 깊이 제거·R8 상수색·합성 shader 병합은 현 근거로 추진하지 않는다.

## Claude 후속 실행 순서 — 이번에는 실행하지 않음

현재 새 GpuFree 기능이 없으므로 `gpufree-off` 생성/삭제 A/B를 해도 제품 동작 차이가 없다. **지금 켬/끔 성능 판을 반복하지 말 것.** 아래 절차는 위 후보가 독립 fixture를 통과하고, 실제 기능·gpufree-off가 구현된 뒤에만 수행한다.

1. 원래 DLL/Settings/boot 파일과 해상도 3440×1440·D3D11·주사율·프레임 제한·vSync·보간·LowRenderScale·다른 모드를 고정한다. 토글 파일은 로드 시 읽는 규약으로 한다. 켬은 파일 없음, 끔은 Mods/StutterFix/gpufree-off 존재. 설치/실행 권한이 있는 후속 담당자만 준비한다.
2. 픽셀 전용 **새 프로세스**에서 `auto on`, `game <Arche FULL.adofai>`, `press`를 쓰고 **목표 시각 전에** `shotat 258 gpufree-on-258`을 예약한다. 268초도 별도 예약/판으로 보관한다. 끔도 새 프로세스에서 같은 시각을 예약한다. 실제 request_song_s·Unity frame·캡처 프레임/곡 위치를 함께 보관한다. 현재 shotat은 목표를 처음 넘는 프레임이므로 다른 프로세스 두 장이 정확한 같은 입력 상태라는 보장은 없다.
3. 먼저 같은 설정의 baseline↔baseline 반복 두 장으로 자체 변동을 확인하고, RGBA byte 차이 픽셀 수/최대 채널 차이를 계산한다. on/off가 다르면 카메라·애니메이션·시간·랜덤/입력 위상을 조사한다. **별도 판 불일치를 곧바로 기능 실패로 단정하지 말고**, 같은 고정 상태에서 원본/대체를 순차 렌더하는 fixture로 구분한다. 별도 판 두 장의 0차이도 모든 상태의 증명이 아니다.
4. 픽셀 비교와 분리한 성능 전용 새 프로세스에서 `gpuprobe on`을 **press 전에** 실행한다. 캡처/census/filteridentity readback을 이 판에서 함께 실행하지 않는다. 재생 후 `wait 150` → `wait 108` → `gpuprobe reset` → `wait 10` → `gpuprobe state` → `gpuprobe off`. wait는 실시간이므로 실제 곡 위치를 확인하여 258~268초에서 벗어난 표본은 비교에서 제외한다. state는 회수 완료된 query까지 포함하며 끝 지연을 확인한다. 정확한 곡 창이 필요하면 기존 자동 시험의 곡 시각 대기/표본 라벨을 후속 보완한다.
5. 새 프로세스 기준 on/off/off/on(ABBA), 최소 두 쌍. 패치 설치·맵 열기·진단 요청 프레임은 창에서 제외한다. 본 카메라/필터/전체 GPU mean·분포·최악과 ready/completed/invalid/errors/overflow를 보관한다. 프레임 전체와 중첩된 필터 시간을 단순 합하지 않는다. FPS/끊김은 probe까지 끈 별도 판에서 확인한다.
6. byte 차이 1이라도 기능 자체에서 재현되면 기본 켬 불가. GPU 차이가 표본 변동보다 작거나 CPU 끊김이 늘면 채택하지 않는다. 원래 DLL/설정/토글 파일을 복구하고 정상 종료한다. 승인 기록·커밋·설치는 이번 문서가 자동 허가하는 작업이 아니다.

## 빌드·재현·보존

|검사|결과|
|---|---|
|Developer Release|오류 0, 경고 35|
|Player Release|오류 0, 경고 25|
|PlayerAuto Release|오류 0, 경고 35|
|오프라인 ILDetails 도구|오류 0, 경고 0|
|연구 Python 2파일 py_compile|통과|
|git diff --check|통과, 기존 .codex/config.toml LF/CRLF 안내만|
|설치 StutterFix.dll·Settings.xml·boot.config SHA256|비교한 3개 모두 동일|

빌드는 `dotnet build StutterFix.csproj -c Release --nologo -v q`에 각각 기본 DEV, `-p:Edition=Player`, `-p:Edition=Player -p:AutoTestBuild=1`을 사용했다. build-install/ship 훅은 호출하지 않았다. 빌드 로그는 `codex-gpufree-20261019-{dev,player,playerauto}-build.txt`, 보존 근거는 [preservation.json](codex-gpufree-20261019-preservation.json)이다. 보존 해시는 오프라인 감사 뒤·제품 빌드 전에 채취하여 빌드 뒤와 비교했다. Mods 전체 파일의 byte 불변 검사는 아니며 설치/변경 명령을 보낸 적은 없다.

새 연구 도구는 [gpufree_audit.py](../../tools/gpuprobe/gpufree_audit.py), [gpufree_details.py](../../tools/gpuprobe/gpufree_details.py)다. StutterFix Compile 목록에 없으며 자동 실행 연결도 없다. Python 3.12 + UnityPy/Pillow/lz4를 사용한다. Python 3.15 패키지 설치는 네이티브 wheel 빌드에 실패하여 3.12 바이너리 패키지를 TEMP에만 설치했다. 제품 빌드 실패와 무관하다.

```powershell
$env:PYTHONPATH = "$env:TEMP/stutterfix-gpufree-pylib312"
py -3.12 tools/gpuprobe/gpufree_audit.py --game-data 'D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice/A Dance of Fire and Ice_Data' --map 'D:/얼불춤 맵 파일/Arche' --census-dir "$env:LOCALAPPDATA/StutterFix" --out "$env:TEMP/stutterfix-gpufree-audit"
py -3.12 tools/gpuprobe/gpufree_details.py "$env:TEMP/stutterfix-gpufree-audit" docs/handoff/codex-gpufree-20261019-evidence
```

도구는 게임을 실행하지 않고 원본을 읽으며 TEMP·지정한 결과 파일만 쓴다. UnityPy가 생성한 Shader.export 텍스트는 이 게임의 PlayerSubPrograms를 완전하게 출력하지 않아, 압축 blob에서 DXBC를 추출한 실제 디스어셈블 결과를 근거로 사용했다. 원래 HLSL을 복원했다는 뜻은 아니다. 임시 원시 트리·전체 IL·ASM은 `%TEMP%/stutterfix-gpufree-audit`에 남겼고 핵심 자료는 저장소 evidence에 보존했다.

이 문서는 apply_patch로 첫 문자 U+FEFF를 포함해 작성했다. 최종 검사에서 파일 시작 byte `EF BB BF`, strict UTF-8 decode, 한글 제목·핵심 문장 유지 및 U+FFFD 없음 여부를 확인했다. 실제 게임 픽셀 동일성/성능 A/B는 미실행이고, 이번 결론은 제품 변경을 승인하지 않는 오프라인 판단이다.
