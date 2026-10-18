# E1 결과 — 알파 0 조각 discard (2026-10-19)

**판정: 본 카메라의 절약 추정 0.0815~0.2571ms로, 인계 문서의 0.3ms 기준에서는 이득 없음. 4번 게임 적용 설계·AssetBundle 구현은 하지 않았다.** 독립 fixture에서는 유효한 GPU 절약이 있다. 예를 들어 large fog 100장·3440×1440·면적 0.5배는 1.8562→1.4636ms(-0.3926ms, 21.15%)다. 이 수치를 실제 Arche 본 카메라 절약으로 직접 사용하면 안 된다.

게임 실행·게임 제어·Mods 설치·커밋·푸시 0회. 제품 C# 179개 SHA256 변경 0개, csproj 변경 0개. 추가한 시험 C#은 `tools/gpuprobe/Gpu5Discard.cs` 하나이며 `#if UNITY_EDITOR`와 명시적 Compile 목록 제외를 확인했다. 제품 빌드·설치 훅은 호출하지 않았다. 기존 작업 트리 변경은 보존했다.

## 1. 실제 Tile 셰이더 확인

근거는 기존 `codex-gpufree-20261019-evidence-shaders.txt`, `codex-gpufree-20261019-evidence.json` 및 기존 오프라인 추출본 `%TEMP%/stutterfix-gpufree-audit/Sprites_Tile-23.json`이다. 기존 요약 JSON에는 Blend/Cull이 빠져 있어 직렬화된 원본 상태도 확인했고, 이번 증거의 `offline.json`에 필요한 상태를 보존했다.

|상태|실제 값|
|---|---|
|RGB/Alpha Blend|둘 다 One / OneMinusSrcAlpha, Add|
|ColorMask|RGBA(15)|
|Cull / ZWrite / ZTest|Off / Off / LEqual|
|Stencil|Always, Pass/Fail/ZFail 모두 Keep, Ref 0|
|AlphaToMask|Off|

8개 D3D11 프로그램은 VS 0~3과 PS 4~7이다. PS 4와 6은 바이트 해시가 같고, PS 5와 7도 같다. 실제 비스테레오 조합은 `PIXELSNAP_ON` × `USE_POINT_SAMPLING`이다.

- VS 0/1: 모델·카메라 변환, 정점 색×Tint, UV×반복 배수. UV offset은 이 어셈블리에 없다.
- VS 2/3: 위 변환 후 화면 반 크기를 사용해 clip 좌표를 round-to-nearest-even으로 픽셀에 맞춘다.
- PS 4/6: _MainTex 한 번 sample → 보간된 색과 RGBA 곱 → RGB에 최종 alpha를 한 번 더 곱 → 출력.
- PS 5/7: sample 전에 UV를 `(floor(uv * textureSize) + 0.5) / textureSize`로 맞춘다. 나머지는 동일하다. PS 추가 입력은 _MainTex_TexelSize의 zw이고, 다른 텍스처·AlphaTex sample·후처리는 없다.

따라서 핵심 계산은 다음과 같다. 실제 PS는 float 연산이다.

```hlsl
float4 c = tex2D(_MainTex, uv) * interpolatedColor;
c.rgb *= c.a;
if (c.a == 0 && all(c.rgb == 0)) discard; // 후보에만 추가
return c;
```

유한한 PNG/색 입력에서 c.a=0이면 premultiply 뒤 c.rgb도 0이다. 실제 Blend는 dest×(1-0)+0이므로 이 출력은 목적지를 보존한다. 깊이 쓰기·스텐실 변경·AlphaToMask도 없어 해당 상태에서는 버려도 출력 부작용이 없다. NaN/Inf나 다른 BlendOp/깊이 쓰기/스텐실 쓰기 상태까지 일반화하지 않는다.

fixture 셰이더는 `Gpu5Discard.Prepare`가 임시 프로젝트에 생성한다. Point 키워드 이름은 시험용 `POINT_SAMPLE`이며 실제 `USE_POINT_SAMPLING`과 위 수식을 대응시켰다. VS는 제어 가능한 clip 공간 quad 변환을 쓰므로 게임 카메라·SpriteRenderer 경로 전체의 동치 증명이 아니다. PS 계산·Point UV·PixelSnap·렌더 상태를 재현한 두 변형 사이의 비교다. Repeat 배수는 1, 스테레오는 시험하지 않았다.

## 2. RGBA byte 동일성

Unity Editor 6000.3.10f1, RTX 4060 Ti, D3D11, Gamma, ARGB32 linear RT, depth 24, MSAA 1, mip 없음, Clamp에서 수행했다. 카메라/게임은 실행하지 않고 CommandBuffer.DrawMesh로 quad를 순서대로 겹쳐 그렸다. 배경은 RGB뿐 아니라 alpha도 0이 아닌 값이다.

실제 Arche PNG 3종 × 원본/긴 변 1024 한도 RGBA32 × RT 3440×1440/1720×720/513×287 × Bilinear/Point × Point/PixelSnap 4변형 × 변환 2종 × 1/3/100장 × 색 alpha 일반/0 = **1,728조건**이다. 변환은 0°·면적 0.5배·정방향 및 17°·면적 3배·X 반전이며 층마다 위치를 조금 달리했다. 일반 색은 정점 alpha .57×Tint alpha .63으로 1보다 작다.

1024 입력은 기존 Gpu4 방식과 같은 계열의 alpha 가중 box 축소를 임시 fixture에서 만들었다. 정수 합계·반올림을 썼으며, 게임 native 축소 결과와 바이트 동치라고 검증한 것은 아니다. 작은 두 PNG는 원래 682×682여서 한도 입력도 같은 크기다.

|입력|크기|alpha=0 픽셀 / 전체|
|---|---|---:|
|fog / black fog 원본·한도|682×682|95,248 / 465,124 (20.48%)|
|large fog 원본|1489×1489|1,093,615 / 2,217,121 (49.33%)|
|large fog 한도|1024×1024|516,817 / 1,048,576 (49.29%)|

|검사|불일치 조건|
|---|---:|
|최종 RGBA 모두 0일 때만 discard|0 / 1,728|
|texture alpha=0이고 최종 RGBA도 0일 때만 discard|0 / 1,728|
|원래 셰이더 반복 캡처|0 / 1,728|
|일반 색인데 무장식 배경과 동일하여 검출 불능|0 / 864|
|느슨한 alpha≤8/255 양성 대조가 차이를 검출|864 / 864 일반 색 조건|

모든 비교는 RGBA 4채널 전체 byte를 검사했다. 따라서 위 안전 조건의 불일치 byte 합과 최대 채널 차이는 모두 0이다. color alpha=0인 나머지 864조건도 최종 알파 판정과 텍스처 알파 판정 둘 다 출력이 같았다. 전자는 전체 조각을 버릴 수 있지만 후자는 텍스처 alpha=0인 조각만 버린다는 차이가 있다.

실제 세 PNG의 투명 texel RGB는 모두 0이었다. 이를 그대로 두고 **투명 RGB가 0이 아닌 합성 RGBA 입력 72조건**을 추가했다. 8×8에서 RGB가 있는 alpha 0/64 texel을 섞고, RT 3종·필터 2종·Point 변형 2종·색 alpha 일반/0·1/3/100장·63°·X 반전으로 그렸다. premultiply 뒤 최종 RGBA 조건 및 texture alpha 조건 모두 **불일치 0/72**였다.

별도 비프리멀티플라이 합성 입력(모든 texel RGB≠0, alpha=0)의 blend 대조:

|Blend|안전 조건 불일치 byte|alpha만 보고 버릴 때 불일치 byte|
|---|---:|---:|
|One One|0|229,251|
|SrcAlpha OneMinusSrcAlpha|0|0|
|One OneMinusSrcAlpha|0|229,251|

인계의 일반화 한 가지는 바로잡는다. 통상 Add 연산의 **SrcAlpha OneMinusSrcAlpha는 source alpha가 정확히 0이면 RGB가 있어도 목적지가 그대로**다. 위험이 검출된 것은 source factor가 One인 두 비프리멀티플라이 대조다. 모든 blend에서 RGBA=0이면 무조건 안전하다는 주장도 하지 않는다. dest factor가 0이 되는 다른 상태에서는 별도 검증이 필요하다.

이 결과는 시험한 유한 입력에서의 동치 검증이며, 실제 게임 상주 텍스처·351개 재질·압축·SpriteMask·모든 프로퍼티/플랫폼을 포괄하는 증명은 아니다.

## 3. D3D11 GPU 타이밍

기존 `tools/gpuprobe/query.cpp`의 변경 없는 `gpuprobe.dll`을 임시 Editor 프로젝트의 Plugins/x86_64에만 복사했다. RT에서 D3D11 장치를 얻고 disjoint+timestamp query를 사용했다. scope는 draw 제출 전체를 감싸고 RT clear·CPU byte 비교는 바깥이다. 완주 뒤 지연 query를 회수했다. CPU Stopwatch나 Editor 프레임 시간을 GPU 시간으로 사용하지 않았다.

3이미지 × RT 2종 × 투영 전 면적 0.5/3배 × N 50/100/200 = **36조건**이다. 정사각 quad가 viewport 밖으로 나가는 부분은 잘리므로 면적 배수는 실제 래스터 픽셀 합과 다르다. 특히 확대하면 투명 가장자리가 잘려 나가 텍스처 전체 alpha=0 비율만큼 절약되지 않는다.

조건마다 ABBA를 2회, 블록마다 예열 8회·측정 32회로 실행했다. bilinear, Point/PixelSnap 끔, 축소 한도 RGBA32 입력이다. 이 표의 “원래”는 위 PS를 재현한 **discard 없는 대체 셰이더**이며 실제 게임의 draw 측정이 아니다.

각 이미지의 첫 조건(3440, 면적 .5, N50)은 초기 첫 ABBA와 두 번째의 절약량이 각각 1.8701/0.0823ms, 1.8460/0.0889ms, 2.0188/0.2314ms로 불안정했다. 초기 표본은 보존하되 이 세 조건 전체를 최종 표에서 제외했다. 원인은 미확인이고 첫 사용/클록/제출 상태 영향은 추정이다. 해당 세 조건만 timestamp를 포함한 예열 64회·측정 64회·ABBA 2회로 재측정했다. 재측정의 두 cycle 절약량은 fog 0.1006/0.0722ms, black 0.0661/0.0646ms, large 0.1953/0.2039ms다. fog의 잔여 드리프트 때문에 본 카메라 모델에는 안정된 N100/200만 사용했다.

초기 측정 9,216회+재측정 1,536회 중 최종 채택은 **9,984회**다. 채택 블록 288개 모두 bad/disjoint·ring full·native error 0. 원래/후보는 조건당 각각 128표본이며, 위 재측정 세 조건은 각각 256표본이다. 각 블록은 집계 평균만 보존하므로 개별 프레임 분산/신뢰구간은 산출하지 않았다. 반복 제출/동기 readback을 쓰는 독립 throughput 실험이며 실제 프레임의 GPU busy·FPS·끊김 시간과 같지 않다.

아래는 블록 표본 수가 같은 원래/후보 평균이다. 음수 절약은 느려진 방향이다.

|안개|RT 너비|면적 배수|N|원래 ms|discard ms|절약 ms|절약 %|
|---|---:|---:|---:|---:|---:|---:|---:|
|fog.png|3440|0.5|50|0.9514|0.8650|0.0864|9.08|
|fog.png|3440|0.5|100|1.8672|1.7406|0.1266|6.78|
|fog.png|3440|0.5|200|3.7499|3.4667|0.2832|7.55|
|fog.png|3440|3|50|2.0605|2.0583|0.0022|0.11|
|fog.png|3440|3|100|4.1063|4.1159|-0.0096|-0.23|
|fog.png|3440|3|200|8.2347|8.2380|-0.0033|-0.04|
|fog.png|1720|0.5|50|0.2376|0.2228|0.0149|6.26|
|fog.png|1720|0.5|100|0.4750|0.4456|0.0295|6.20|
|fog.png|1720|0.5|200|0.9446|0.8905|0.0541|5.73|
|fog.png|1720|3|50|0.5308|0.5308|-0.0000|-0.00|
|fog.png|1720|3|100|1.0542|1.0588|-0.0045|-0.43|
|fog.png|1720|3|200|2.1245|2.1143|0.0103|0.48|
|nes_black_fog.png|3440|0.5|50|0.9321|0.8668|0.0654|7.01|
|nes_black_fog.png|3440|0.5|100|1.8617|1.7249|0.1367|7.34|
|nes_black_fog.png|3440|0.5|200|3.7481|3.4726|0.2755|7.35|
|nes_black_fog.png|3440|3|50|2.0565|2.0476|0.0089|0.43|
|nes_black_fog.png|3440|3|100|4.1133|4.1157|-0.0024|-0.06|
|nes_black_fog.png|3440|3|200|8.2929|8.3020|-0.0092|-0.11|
|nes_black_fog.png|1720|0.5|50|0.2365|0.2221|0.0145|6.12|
|nes_black_fog.png|1720|0.5|100|0.4752|0.4484|0.0268|5.64|
|nes_black_fog.png|1720|0.5|200|0.9661|0.8901|0.0760|7.87|
|nes_black_fog.png|1720|3|50|0.5334|0.5411|-0.0077|-1.44|
|nes_black_fog.png|1720|3|100|1.0743|1.0755|-0.0012|-0.11|
|nes_black_fog.png|1720|3|200|2.1534|2.1436|0.0098|0.46|
|nes_large_fog.png|3440|0.5|50|0.9315|0.7319|0.1996|21.43|
|nes_large_fog.png|3440|0.5|100|1.8562|1.4636|0.3926|21.15|
|nes_large_fog.png|3440|0.5|200|3.7372|2.9391|0.7982|21.36|
|nes_large_fog.png|3440|3|50|2.0445|1.9084|0.1361|6.66|
|nes_large_fog.png|3440|3|100|4.0870|3.8073|0.2797|6.84|
|nes_large_fog.png|3440|3|200|8.1883|7.6447|0.5436|6.64|
|nes_large_fog.png|1720|0.5|50|0.2369|0.1873|0.0496|20.93|
|nes_large_fog.png|1720|0.5|100|0.4775|0.3739|0.1036|21.70|
|nes_large_fog.png|1720|0.5|200|0.9566|0.7526|0.2040|21.33|
|nes_large_fog.png|1720|3|50|0.5113|0.4836|0.0276|5.41|
|nes_large_fog.png|1720|3|100|1.0207|0.9524|0.0683|6.69|
|nes_large_fog.png|1720|3|200|2.0457|1.9230|0.1226|6.00|

## 4. Arche 본 카메라 환산과 판정

기존 census 6개 중 Tile 그룹이 있는 본 카메라 5개를 오프라인 집계했다. 전체 안개 4종의 aabb_pixels 합 / Sprites/Tile 합은 약 **37.97~43.22%**다. 최신 `gpu-census-20261018-223220-954.tsv`의 Camera#94428은 다음과 같다.

|항목|aabb_pixels|Tile 대비|
|---|---:|---:|
|Tile 전체|486,925,579.544|100%|
|fog.png|5,590,869.320|1.1482%|
|nes_large_fog.png|199,895,243.916|41.0525%|
|nev_fog.png|4,953,600.000|1.0173%|
|세 안개 합|210,439,713.236|43.2180%|
|nes_black_fog.png|0|0%|

**이는 AABB 후보 면적 합이지 실제 draw·fragment·GPU 비용 비중이 아니다.** census는 large fog의 GPU 실제 크기를 1489×1489, wrap Repeat로 기록했다. 인계의 1024 설명과 다르며, 이번 성능 표의 1024·Clamp 입력을 당시 게임 입력 그대로라고 부르지 않는다. 촬영 시각도 이번 실험에서 새로 검증하지 않았다.

본 카메라 `C(s)=A+B*s²`, B≈2.9ms는 인계의 기존 측정값을 사용했다. 이번에 게임에서 다시 잰 수치가 아니다. fixture에서는 `B_fixture=(C(1)-C(.5))/.75`, 픽셀 비용 절약률은 `r=(Δ(1)-Δ(.5))/(C(1)-C(.5))`로 계산했다. 같은 면적·N·이미지의 두 RT 평균을 짝지었다.

**추정 가정:** 카메라 B 전체가 Tile 계열에 속하고, 그 안의 각 이미지 GPU 픽셀 비용 비중이 위 AABB 비중과 같으며, fixture의 절약률이 게임에도 옮겨진다. nev_fog와 기타 draw는 절약 0으로 놓았다. 이는 비용을 직접 분리한 측정이 아니다.

`예상 절약 = 2.9 * (0.01148198*r_fog + 0.41052525*r_large)`

|면적 배수|N|fog 픽셀 비용 절약률|large fog 픽셀 비용 절약률|본 카메라 절약 추정 ms|
|---|---:|---:|---:|---:|
|0.5|100|6.98%|20.96%|0.2518|
|0.5|200|8.17%|21.37%|0.2571|
|3|100|-0.17%|6.89%|0.0820|
|3|200|-0.22%|6.85%|0.0815|

예를 들어 면적 .5·N100에서 fog의 A/B는 0.0110/1.8563ms, large는 0.0179/1.8383ms다. 위 픽셀 비용 절약률을 적용하면 본 카메라 약 **3.7→3.4482ms(추정)**다. 면적 3배의 large A는 -0.0013ms로 거의 0이며, 음수 값을 물리적 고정 비용으로 해석하지 않는다. 두 해상도만으로 맞춘 모델의 측정 오차다.

시나리오 추정 범위는 **0.0815~0.2571ms**, 본 카메라 3.7ms 대비 약 **2.2~6.9%**다. 미측정 nev_fog의 해당 비용을 100% 없앤다고 가정해도 위 모델의 가장 큰 추정에 +0.0295ms로 **0.2866ms**다. 이것도 수학적/통계적 상한은 아니다. 실제 면적·표본화·개별 draw 비용이 AABB 가정과 다르면 달라진다.

인계의 **실제 Arche 카메라 예상 절약 0.3ms**를 진행 기준으로 해석하면, 이번 근거로는 **이득 없음(추정)**이다. standalone N200에서 0.7982ms를 절약한 사례와 이 판정을 혼동하지 않는다. **4번 AssetBundle·재질 교체·게임 검증 명령 설계는 진행하지 않았다.** 게임 내 실제 절약/FPS/끊김 개선은 미측정이다.

## 5. 재현·보존·검증

- 임시 프로젝트: `%TEMP%/stutterfix-alphadiscard-20261019/UnityFixture`.
- 증거 폴더: `C:/Users/Public/StutterFixTrace/alphadiscard-20261019/`.
- 원자료: `validation.tsv`, `synthetic.tsv`, `positive.tsv`, `alpha.tsv`, `timing.tsv`, `timing-retest.tsv`.
- 집계: `summary.json`, `summary.tsv`, `offline.json`. `summary.tsv`는 재측정 세 조건을 교체한 최종 36조건이다.
- 재현/추적: `analyze.py`, `summarize.py`, `input-hashes.json`, `manifest.json`, `scope-checks.json`, `Gpu5Tile.shader`, 본 시험에 실제 사용한 `Gpu5Discard-main-run.cs`, 최종 코드 `Gpu5Discard-final.cs`.
- Editor 로그: `prepare.log`, `run.log`, `retest.log`. 본 시험·보강 시험 모두 완료 표식이 있고 종료 코드 0을 확인했다.
- 최초 게임 프로세스 확인 결과 0개여서 대기 없이 진행했다. 종료 확인에서도 게임 프로세스 0개다. 작업이 실행한 두 측정 Editor는 모두 종료했다.

기존 임시 프로젝트의 ProjectSettings/Packages만 복사해 새 프로젝트를 만들고, 최종 시험 소스를 Assets/Editor에, 기존 gpuprobe.dll을 Assets/Plugins/x86_64에 둔다. 환경 변수 `SF_GPU5_OUT`은 새 출력 폴더로 지정한다. 다음 세 Editor 호출은 순차 실행하며 완료 후 다음 호출을 시작한다.

```powershell
# $fixture = 위 임시 UnityFixture의 절대 경로
# $env:SF_GPU5_OUT = 새 증거 폴더
& 'E:/Unity/Editor/6000.3.10f1/Editor/Unity.exe' -batchmode -quit -force-d3d11 -projectPath $fixture -executeMethod Gpu5Discard.Prepare -logFile "$env:TEMP/gpu5-prepare.log"
& 'E:/Unity/Editor/6000.3.10f1/Editor/Unity.exe' -batchmode -quit -force-d3d11 -projectPath $fixture -executeMethod Gpu5Discard.Run -logFile "$env:TEMP/gpu5-run.log"
& 'E:/Unity/Editor/6000.3.10f1/Editor/Unity.exe' -batchmode -quit -force-d3d11 -projectPath $fixture -executeMethod Gpu5Discard.Retest -logFile "$env:TEMP/gpu5-retest.log"
```

fixture는 원본 폴더에서 세 이름의 PNG가 정확히 하나씩 발견되는지 검사하고, byte 차이·검출 불능·query 누락/오류가 나오면 실패시킨다. 보존한 집계 스크립트는 이번 증거 폴더를 절대 경로로 사용하므로 재현 출력 경로를 바꿀 때 함께 조정해야 한다.

제품 빌드 셋은 `EnableDefaultCompileItems=false`와 fixture Compile 항목 0개를 확인했다. 제품 변경이 없으므로 빌드를 다시 하지 않았다. 결과 문서는 apply_patch로 선두 U+FEFF를 포함해 작성했으며, 작성 후 BOM 바이트 EF BB BF·엄격한 UTF-8 디코딩·한글 대표 문구 및 `Get-Content -Encoding UTF8` 출력을 확인했다. BOM은 선두에 1개, 대체 문자 U+FFFD는 0개였다.

