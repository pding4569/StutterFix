# 안개 Tight 메시 독립 D3D11 시험 결과 — 2026-10-18

**면적을 줄인 bbox·격자·Unity Tight 세 방식 모두 0 byte 조건에서 탈락했다. 제품 적용 후보 없음.** 최종 채택한 비교 990조건 중 불일치 832조건, 같은 상태 반복 렌더 불일치 0조건이다. Bilinear의 최대 채널값 차이는 1, Point까지 포함하면 19다. 1 차이도 허용하지 않는 지시대로 폐기한다. 다른 메시 인코딩까지 수학적으로 불가능하다는 증명은 아니다.

게임 실행·게임 제어·Mods 설치·커밋·푸시를 하지 않았다. 제품 C#·csproj를 수정하지 않았다. 추가한 C#은 제품 Compile 목록 밖의 `tools/gpuprobe/Gpu4Tight.cs` 하나이며 `#if UNITY_EDITOR`로 감쌌다. 독립 임시 Unity Editor만 실행했다.

## 시험 환경과 실제 완료 범위

- Unity Editor 6000.3.10f1 / RTX 4060 Ti / Direct3D11 / Gamma / Built-in `Sprites/Default`.
- ARGB32 linear RT, 3440×1440 및 1720×720, MSAA·HDR 끔. RGBA32로 동기 읽어 **RGBA 네 채널의 byte 전부**를 비교했다. 성능 측정 중 픽셀을 읽은 것은 아니다. 이번에는 성능 측정 자체를 진행하지 않았다.
- 원본 PNG 4종, cap1024 RGBA32, 실제 변환에 성공한 cap1024 DXT5 2종. mip 없음/있음, Bilinear/Point, 팽창 2/3/4/8 최종 텍셀, 단독/3장 겹침. 같은 비교 쌍에서 texture 객체·rect·pivot·ppu·색·재질·Transform·명시적 sortingOrder를 유지한다. 전체 rect의 동일한 텍스처 공간 좌표를 정점 위치와 UV에 사용한다. 텍스처 crop/재패킹/RGB 제거는 없다.
- 고정 ppu=100인 독립 비교다. 축소할 때 게임 `ImagePrefetch`가 ppu까지 보정하는 실제 월드 크기의 재현은 아니다. 카메라 크기를 텍스처 크기에 비례시켰다. wrap=Clamp이며, census의 large fog에는 Repeat도 있다.
- 각 **입력/메시/팽창/mip/filter/RT** 조건에서 처음 1 byte 차이가 나오면 그 조합을 즉시 폐기한다. 다른 지오메트리 방식도 확인했다. 같은 방식의 나머지 입력·팽창은 실패 범위를 조사하는 독립 조건으로 기록했고, 성능 후보로 올리지 않았다.
- 계획에는 회전 0/17/63°, scale .7/1.3/3, x 반전, pivot (.5,.5)/(.23,.71)가 있다. **조기 탈락으로 실제 방문한 회전은 전부 0°, pivot은 전부 (.5,.5)**다. scale .7 983조건, 1.3 7조건, x 반전 40조건이다. 회전 17/63°, scale 3, 다른 pivot, 2장 겹침은 미검증이다. 미방문 조건을 통과로 세지 않는다. 앞선 필수 조건의 반례만으로 후보를 폐기했으므로 이 뒤로 확대하지 않았다.
- 실제 RT별 3440: 487조건 / 1720: 503조건. mip 없음 466 / 있음 524. Bilinear 472 / Point 518. 1장 860 / 3장 130조건이다.

`Shader.Find("Sprites/Tile")`은 이 독립 Editor에서 null이다. 이전 GPU3 자료가 확인한 게임 엔진 버전은 6000.3.21f1이므로 Editor와 버전도 다르다. 게임 Tile 셰이더의 통과·실패 또는 실제 게임 FPS 결과로 바꾸어 쓰지 않는다.

## 입력 알파와 DXT 보정

`alpha>0`만 마스크에 넣는다. `alpha<=8` 수치는 참고 통계이며 **제거 기준으로 사용하지 않았다**. 최종 크기에서 정사각형 반경 2/3/4/8 텍셀로 팽창한다. 원본 4종의 Unity RGBA 데이터 SHA256은 Pillow로 원본 PNG를 RGBA 변환하고 상하를 뒤집은 데이터와 모두 일치했다. 파일 SHA256·알파 전체 히스토그램은 `codex-tight-20261018-summary.json`에 있다.

|입력|크기|alpha=0 픽셀|alpha<=8 픽셀|
|---|---:|---:|---:|
|fog / black fog 원본·cap1024, 각각|682×682|95,248|270,506|
|large fog 원본|1489×1489|1,093,615|1,625,654|
|large fog cap1024 RGBA32|1024×1024|516,817|768,592|
|large fog cap1024 DXT5, GPU 조회|1024×1024|516,817|768,592|
|nev fog 원본·cap1024|100×100|6,707|6,939|
|nev fog DXT5, GPU 조회|100×100|6,739|6,943|

large fog의 축소는 `PngDecoder.ManagedDownscale`과 같은 크기 반올림·알파 가중 상자 합계·0.5 반올림을 독립 fixture로 옮겼다. GPU Blit으로 이미지 축소하지 않았다. PNG 해독 이후의 이 계산을 사용한 것이며, 실행 중인 게임의 상주 텍스처를 읽은 것은 아니다.

`Texture2D.Compress(false)` 뒤 실제 format을 검사했다. 682×682의 fog/black fog는 RGBA32로 남아 DXT 시험에서 제외했다. large 1024×1024와 nev 100×100만 DXT5였다. 이것이 게임의 native TexCompress와 동일한 압축 byte라는 주장은 하지 않는다.

**DXT의 CPU 조회만 신뢰하면 안 되는 반례가 나왔다.** nev DXT5는 `GetPixels32` 알파와 같은 크기 Point Blit 후 GPU 알파가 494픽셀 달랐다. 최종 DXT 시험은 GPU 알파로 마스크를 다시 만들었다. large DXT5의 CPU/GPU 알파 차이는 0이다. 원본 픽셀을 읽는 GPU Blit은 압축 알파 조사용이며, 비교에 쓰는 DXT 텍스처를 RGBA로 교체하지 않는다.

초기 `fixture-final` 안의 DXT 비교 241행은 최종 근거에서 제외했다. 이를 `fixture-dxtgpu`의 GPU 알파 기준 241행으로 대체했다. 최종 990 = PNG·cap1024 **749** + 보정 DXT **241**이며, 두 DXT 실행을 더하지 않았다.

## 지오메트리와 API 검증

`Sprite.OverrideGeometry`를 FullRect 및 Tight 런타임 Sprite에 시도했지만 이 Editor에서는 `Not allowed to override geometry`를 기록하고 원래 메시를 유지했다. Play 모드에서도 이 경로의 거부를 확인했다. 이 무변경 상태를 0차이 통과로 인정하지 않았다. 최소 삼각형/사각형 API 시험 결과는 `codex-tight-20261018-api*/api-probe.txt`에 있다.

대체 경로는 `SpriteDataAccessExtensions.SetVertexCount/SetVertexAttribute(Position, TexCoord0)/SetIndices`다. 쓴 정점 수·좌표·인덱스를 다시 읽어 검사한 뒤에만 비교했다. Sprite 정점 채널이 Position·TexCoord0뿐인 것도 분리 시험으로 확인했다. 정점 색상 채널의 초기화 누락으로 차이가 생겼다고 가정하지 않았다.

OverrideGeometry의 입력은 rect의 픽셀 좌표이고 pivot/ppu 변환은 API가 수행한다. 직접 정점 API에는 변환한 로컬 좌표와 UV를 넣는다. 이 좌표 계약은 [Unity 공식 문서](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/sprite/overridegeometry)와 로컬 API XML로 확인했다. 거부된 초기 실행과 좌표 수정 전 탐색 결과는 아래 수치에 포함하지 않는다.

|원본 RGBA32, 팽창 2px|bbox 면적비 · 정점/삼각형|grid16 면적비 · 정점/삼각형|Unity Tight 면적비 · 정점/삼각형|
|---|---|---|---|
|fog|1.000000 · 4/2|0.842236 · 1627/3080|0.815764 · 139/137|
|black fog|1.000000 · 4/2|0.842236 · 1627/3080|0.815764 · 139/137|
|large fog|0.641398 · 4/2|0.527675 · 4723/9140|0.515072 · 443/441|
|nev fog|0.469200 · 4/2|0.614400 · 36/48|0.498100 · 9/7|

large fog 1024 RGBA32와 실제 DXT5는 같은 2px 조건에서 bbox 0.644382(4/2), grid16 0.536621(2304/4396), Tight 0.519711(207/205)이었다.

- bbox는 팽창 마스크 전체의 최소 직사각형이다. fog/black fog는 전체 rect가 되어 면적 감소가 없으므로 렌더링 후보로 올리지 않았다.
- grid16은 전역 16texel 격자다. 남기는 셀의 경계 정점을 사전으로 공유한다. 셀 병합이나 인접 변의 부분 분할을 하지 않아 T접합을 만들지 않는다. 이번 최대 크기는 4,835정점/9,364삼각형이다. 구현은 정점 65,530개 미만에서 제한한다. 이 상한은 ushort 인덱스 보호용이며 성능상 허용 상한은 아니다.
- Tight는 팽창 마스크를 0/255의 별도 텍스처로 만들고 `Sprite.Create(..., Tight)`의 형상을 가져와, 원본 텍스처 Sprite에 위치·UV를 설정했다. 약한 알파를 임계값으로 제거하지 않는다. 공개 Sprite.Create에는 alphaTolerance 인자가 없어 엔진 기본 윤곽 생성을 사용했다. alphaTolerance 수치별 시험이나 윤곽 단순화 뒤 모든 texel의 포함 보장은 수행하지 않았다. 이 후보도 실제 렌더링에서 탈락했다.

240개 지오메트리 행의 면적·정점·삼각형·bounds·UV 오차는 summary와 각 geometry.tsv에 있다. 면적비는 메시 로컬 면적/FullRect 면적이며, 화면에서 잘린 뒤의 실제 픽셀 면적은 아니다.

## 0 byte 판정

|방법|실제 비교한 조건|불일치|최대 채널값 차이|
|---|---:|---:|---:|
|bbox(면적 감소 있음)|221|192|19|
|grid16|428|320|15|
|Unity Tight 형상|341|320|19|
|합계|990|832|19|

- 같은 원본을 두 번 그린 차이: **0/990**. 그림이 전혀 보이지 않은 조건: **0/990**.
- alpha=1인 흰 텍스처 세 장을 의도적으로 통째로 제거하는 양성 대조: **368,743픽셀 / 1,106,229 byte** 차이 검출, 최대 차이 3. PNG·DXT 보정 실행 모두 같은 결과다. 초기의 어두운 조건에서는 출력 양자화에 가려 양성 대조를 검출하지 못했으므로 그 실행은 무효로 하고 배경·색을 보정했다.
- bbox의 첫 반례: large fog 원본, mip 없음, Bilinear, pad2, 3440×1440, 회전0/scale .7/중앙 pivot/1장에서 **78픽셀 / 80 byte, 최대 1**.
- grid의 첫 반례: fog 원본, 같은 조건에서 **127픽셀 / 136 byte, 최대 1**.
- Tight의 첫 반례: fog 원본, 같은 조건에서 **130픽셀 / 139 byte, 최대 1**.

**투명 경계에서만 달라진 것은 아니다.** fog/grid 반례의 RT 좌표 (1639,557), 원본 텍스처 좌표 (234,192)에서 RGBA가 **(54,73,98,255) → (54,72,98,255)**로 달랐다. 최근접 알파 29, 9×9 이웃의 최소 알파 27로, alpha=0 경계에서 떨어진 비투명 내부에서도 1 차이가 났다. 다른 예인 (1555,569)는 **(50,70,96,255) → (50,70,95,255)**, 이웃 최소 알파 9다. 이 fog 사례를 완전 불투명한 alpha255 내부라고 부르지는 않는다.

이상적인 같은 아핀 UV 식에 대해, 읽어온 정점·UV로 재계산한 최대 오차는 약 8.43×10^-8이었다. 이것과 삼각형 분할·보간·래스터 반올림이 1 차이로 이어졌을 가능성은 **추정**이다. GPU 명령 단위로 원인을 확정하지 않았다. Point에서는 texel 경계에서 다른 픽셀을 선택했을 가능성도 있다(추정). Bilinear 전체의 최대 차이는 1, Point 포함 최대 차이는 19다.

이미지별 비교/불일치는 fog 148/128, black fog 150/128, large fog 404/288, nev fog 288/288이다. 158조건의 국소적인 0차이나 면적비 1인 bbox를 방식 전체의 합격으로 보지 않는다.

## GPU 시간·census·절약 추정

**0 byte를 만족하는 면적 감소 후보가 없어, 100장 겹침 D3D11 timestamp 성능 시험은 실시하지 않았다. FullRect/후보 GPU 시간·차이 ms·FPS 개선은 미측정(null)이다.** 불합격 형상의 면적만으로 절약 ms를 숫자로 제시하지 않는다.

지정한 census를 오프라인으로 집계했다. 본 카메라 Camera#94428의 전체 후보 AABB 면적은 459,483,967.364 pixel, 화면 92.758개 분량이다. 이는 **전체 후보**의 면적 합계이며, 이번 안개 4종만의 값은 아니다.

|본 카메라 대상 이미지|draw 행 수|group의 aabb_pixels 합|화면 비율|
|---|---:|---:|---:|
|fog|110|6,261,370.097|1.264|
|large fog|94|162,623,250.042|32.830|
|black fog / nev fog|0|0|0|
|대상 합계|204|168,884,620.139|34.093|

같은 이미지의 group과 draw를 중복 합산하지 않았다. census에서 fog는 renderer 110개가 같은 texture ID, large fog는 94개가 같은 texture ID였다. 텍스처 공유는 Sprite 공유 증명과 구분한다.

이 census의 large fog는 **1489×1489 RGBA32·mip1·Repeat**였으며, 1024/DXT 상주 텍스처가 아니었다. 축소/DXT 조건은 요구된 별도 입력 시험이다. renderer 변환 행렬이 없어 화면에서 잘린 뒤의 메시 면적은 복원하지 않았다.

향후 합격 후보가 생겼을 때의 식만 남긴다. 이미지 j의 면적비 r_j와 census의 잘린 AABB 면적 A_j를 쓰면, 대상 내 모델 감소 비율은 `Σ A_j(1-r_j) / Σ A_j`다. 전체 후보 AABB 기준 모델은 분모를 459,483,967.364로 바꾼다. 제공된 픽셀 비례 GPU 시간 2.9ms에 그에 대응하는 픽셀 비용 감소 비율의 상한을 곱한다. **잘린 AABB×전체 Sprite 면적비는 실제 가시 픽셀 수가 아니고, 엄밀한 실행 시간 상한도 아니다.** 깊이·겹침·shader·대역폭·추가 정점 비용을 무시한 가정이다. 이번에는 합격 후보가 없어 r_j를 대입한 절약 값을 채택하지 않는다. 3.7ms/2.9ms 자체도 이번에 다시 측정한 값이 아니다.

## 게임 적용에 필요한 설계 조사(구현하지 않음)

이번에 저장한 `texture-il.txt`, `customsprite-il.txt`와 GPU3의 `consumers.tsv`/`scrVisualDecoration-il.txt`, 현행 소스를 대조했다.

|위치·소비자|설계상 필요한 것|
|---|---|
|TextureManager.GetOrAddSprite / CustomSprite..ctor|customSprites 사전의 기존 값을 재사용한다. 생성자는 texture 폭/높이·rect·pivot·ppu로 Sprite.Create하고 sprite 필드에 저장한다. 향후에는 실제 texture/ppu 확정 뒤 생성자 postfix, 사전 공유 전이 후보 지점이다. renderer만 교체하면 다른 참조와 어긋난다|
|같은 이미지 공유|사전의 같은 키·유효한 수정 시각 경로는 같은 CustomSprite를 반환하므로 그 경로에서는 이미지당 한 번 계산하면 된다. 다른 경로 표기·내부/Bundle·재읽기·다른 인스턴스까지 반드시 Sprite 하나라고 보장하지 않는다|
|ImagePrefetch|여기서 직접 Sprite.Create한다고 가정하지 않는다. 현행 코드는 CustomSprite 생성자 prefix에서 AdjustPixelsPerUnit을 불러 축소 비율을 ppu에 반영한다. 최종 texture·크기·ppu를 사용하고 원본 PNG 크기 마스크를 재사용하지 않는다|
|TexCompress|Reinitialize/LoadRawTextureData와 직접 DXT 경로가 있다. 압축 전 RGBA 마스크를 사용하면 안 된다. 최종 format 확정 뒤 알파로 만들고 texture/크기/format/mip/갱신 세대가 바뀌면 무효화한다. 이번 CPU/GPU 알파 차이도 고려한다|
|ResizeCollider / spriteUnscaledSize|ResizeCollider는 sprite.bounds.size를 BoxCollider2D.size에 쓴다. spriteUnscaledSize가 zero일 때 크기도 초기화한다. bounds 축소는 초기 크기와 편집 클릭 판정을 바꾼다|
|GetDecorationWorldSize|sprite.bounds.size×pivotTrans.localScale을 사용한다. rect/pivot만 유지해서는 충분하지 않다|
|SpriteMask / SpriteAlphaMask|SetSprite는 주 renderer 외에도 sprite를 전달한다. 마스크 윤곽/캐시·alpha cutoff·반전 마스크 영향은 미검증이다. 주 renderer의 0차이만으로 마스크도 통과했다고 볼 수 없다|
|LazySprite|투명할 때 원래 CustomSprite를 pending에 보관하고 ResizeCollider/크기 읽기 등에서 flush한다. 공유 sprite를 유지하며 한 번 계산하는 설계는 pending과 충돌할 가능성이 낮다(추정). renderer만 교체하면 pending이 덮어쓴다. 첫 비zero 크기 초기화는 건너뛰면 안 된다|
|타일링·sampling·shader 변경|SetTile은 repeatX/repeatY와 material 값을 바꾸고, SetSprite는 sampling variant와 wrap도 다룬다. 일반 스프라이트 시험 한 번으로 repeat/마스크/다른 blend까지 안전하다고 보지 않는다. 현재는 전부 불채택이므로 게임 패치를 추가하지 않았다|
|동적 batch/컬링|격자는 최대 9,364삼각형으로 Sprite batching이나 정점 비용이 달라질 수 있다. 실제 게임의 batch 상한·draw 수는 미측정이다. bounds 축소 시 화면 가장자리 컬링도 달라질 수 있어 기존의 보수적 bounds 보존이 필요하다|

**이번 bounds 관찰:** 직접 SpriteDataAccess로 정점을 설정한 240예 모두 `Sprite.bounds`가 원래 FullRect와 같았다. 원래 rect 양 끝의 미참조 정점을 넣은 버전도 같았다. 이는 이 Editor의 이 API 경로에서 관찰한 결과이며, OverrideGeometry나 다른 버전의 보장이 아니다. 미참조 정점 덕분에 bounds가 유지됐다는 인과도 따로 증명하지 않았다. 향후에는 원래 크기를 나타내는 참조된 축퇴 삼각형이나 원래 bounds/크기의 명시적 보존을 검토할 수 있지만, 실제 Sprite.bounds·Renderer.bounds·가장자리 컬링·collider·mask를 먼저 검증해야 한다. **이것이 이번 픽셀 불일치를 해결한다는 뜻은 아니다.**

`Sprites/Tile`의 .shader 소스는 작업 트리/게임 Data 파일 검색에서 찾지 못했고 Editor에도 없었다. IL로 확인한 것은 repeatX/repeatY 등의 material 설정, sampling variant, shader 전환 경로까지다. 네이티브 shader의 정확한 UV/경계/clip/블렌드 식은 미확인이다. Sprites/Default와 같은 식이라고 단정하지 않는다. GPU3에서 확인한 투명 동률 정렬·재질 공유 불합격을 뒤집는 변경도 없다.

## 재현·빌드·산출물

독립 프로젝트는 `%TEMP%/stutterfix-tight-20261018/UnityFixture`다. 제품 빌드/설치 훅을 실행할 필요가 없다.

```powershell
Copy-Item tools/gpuprobe/Gpu4Tight.cs "$env:TEMP/stutterfix-tight-20261018/UnityFixture/Assets/Editor/Gpu4Tight.cs"
$env:SF_GPU4_OUT = 'C:/Users/Public/StutterFixTrace/tight-repeat-new'
Remove-Item Env:SF_GPU4_DXTONLY -ErrorAction SilentlyContinue
& 'E:/Unity/Editor/6000.3.10f1/Editor/Unity.exe' -batchmode -quit -force-d3d11 `
  -projectPath "$env:TEMP/stutterfix-tight-20261018/UnityFixture" `
  -executeMethod Gpu4Tight.Run -logFile "$env:TEMP/tight-repeat.log"
# DXT만 다시 시험할 때는 SF_GPU4_DXTONLY=1. 새로운 출력 경로를 지정한다.
# done.txt와 Editor 종료 로그를 모두 확인한다. 실패 실행의 남은 TSV를 합격으로 세지 않는다.
py -3 tools/gpuprobe/gpu4_summarize.py
```

제품 로컬 Debug 빌드만 실행했다. Developer: 경고35/오류0, Player: 19/0, PlayerAuto: 4/0(증분 빌드). build-install.sh/ship/pack을 호출하지 않았고, Mods로 복사하는 명령도 실행하지 않았다. 다른 작업의 변경은 되돌리지 않았다.

**보존 검사에 대한 제한:** 시작 때 제품 C# 172개와 Mods 파일 299개, 총 471개를 해시로 기록했다. 종료 비교에서 5개가 달랐다: `AutoTest.cs`, Mods의 `StutterFix.dll`, `StutterFix.dll.53667.cache`, `session.lock`, `autotest.done`. 사용자가 알린 동시 작업이 진행되는 환경이며, 이번 작업에서는 이 파일들을 쓰거나 게임에 명령을 보내지 않았다. 어느 프로세스가 썼는지 추적하지 않았으므로 변경 주체는 확정하지 않는다. **작업 전체 동안 제품/Mods가 바이트 단위로 불변이었다고 주장하지 않는다.** 기존 상태로 강제 복구하면 동시 작업을 덮어쓰므로 복구하지 않았다. 나머지 466개는 시작 해시와 같았다.

근거 파일(이 문서와 같은 폴더 기준):

- `codex-tight-20261018-summary.json`: 최종 집계, 원본 해시, geometry 240행, census, 내부 차이 예시.
- `codex-tight-20261018-fixture-final/{validation,geometry,alpha,differences}.tsv`: PNG 쪽. **같은 폴더의 cap1024_dxt 행은 제외한다**.
- `codex-tight-20261018-fixture-dxtgpu/{validation,geometry,alpha,differences}.tsv`: 최종 DXT 쪽. CPU/GPU 알파 차이도 기록했다.
- 두 폴더의 `positive.txt`/`environment.txt`/`done.txt`. `no-saving.txt`는 면적을 줄이지 않는 후보 기록이다.
- `codex-tight-20261018-{png,dxtgpu}-editor.txt`, `*-{dev,player,playerauto}-build.txt`, `*-{texture,customsprite}-il.txt`.
- `codex-tight-20261018-preservation-{before,after}.json`, `preservation-check.txt`: 제품 C#·Mods 기존 파일 해시 대조와 위 5개 차이 기록.

초기 API 거부·양성 대조 실패의 `fixture`/`fixture-v3`/`fixture-v5` 등은 무효인 탐색 기록이며 최종 수치에 넣지 않았다. 실행한 PNG 버전 소스는 temp의 `Gpu4Tight-png-tested.cs`에 보존했다. 현행 fixture에는 이후 GPU 알파 읽기를 추가했고, DXT 보정 시험은 추가 후 버전으로 실행했다. 마지막 소스 변경은 ApiProbe의 정점 채널 출력뿐으로, 렌더링 판정 로직은 바꾸지 않았다.

조기 탈락 뒤의 변환 조건, 실게임 Tile·상주 텍스처, SpriteMask/편집 조작/컬링/배칭의 실게임 동작, GPU 절약 시간은 미검증이다. **이를 채우려고 이번에 금지된 게임 실행이나 제품 변경을 수행하지 않았다.**
