# GPU3 결과 — 재질 공유·아틀라스 동일성 검증 (2026-10-18)

**D 프로토타입은 구현하지 않았다. A 정렬과 B 표본화가 모두 불합격했고, C에서 텍스처 전체 크기에 의존하는 소비자도 확인했다.** `atlasprobe` 명령은 존재하지 않는다. 실제 Arche 258·270초 화면, 본 카메라 GPU 시간, 258~274초 FPS 개선은 이번에 측정하지 않았다.

얼불춤 실행 명령·Mods 설치·커밋·푸시 0회. 기존 실행 중인 게임 프로세스는 제어하지 않았다. 게임과 분리된 임시 **Unity Editor** 프로젝트에서 D3D11 GPU 시험을 수행했다. gpu2의 코드와 설정 파일 7개는 작업 전후 SHA256이 모두 같다. 제품 C#·AutoTest.cs·csproj 변경 0줄.

## 환경·산출물·채택 범위

- GPU 시험: Unity Editor **6000.3.10f1**, RTX 4060 Ti, Direct3D11, Gamma, Built-in `Sprites/Default`, 128×128 ARGB32 linear RT, MSAA/HDR 끔. 얼불춤 Player나 Frame Debugger 측정이 아니다.
- 현재 게임의 `globalgamemanagers`에는 **6000.3.21f1**이 기록되어 있다. Editor 버전·실제 게임 Tile 셰이더·게임 상주 텍스처와 같다고 주장하지 않는다.
- 확장 픽스처: `tools/gpuprobe/Gpu3Fixture.cs`. `#if UNITY_EDITOR`이며 StutterFix의 명시적 Compile 목록에 넣지 않았다. 프로젝트/제품에서 자동 실행하는 연결도 없다.
- IL·맵 분석/집계: `tools/gpuprobe/gpu3_offline.py`, `gpu3_map_audit.py`, `gpu3_summarize.py`.
- 옛 `MaterialPoolFixture.cs`, `DecorationMaterialPool.cs`, `AlphaCropFixture.cs`는 `git show 5d42c57^:<파일>`로 **작업 트리 밖** `%TEMP%/stutterfix-gpu3-20261018/`에 복원했다. 원래 fixture는 게임의 Main/Hitch/재질 풀에 의존한다. 이번 독립 fixture는 이 의존성을 제거하고 직접 sharedMaterial을 바꾸어 정렬 문제를 분리했다. 옛 COW 구현을 제품에 되살리지 않았다.
- 아래 근거 경로는 모두 이 결과 문서가 있는 `docs/handoff/` 기준이다.

## A. 동률 정렬

과거 실제 게임 Tile fixture의 **36조건 공유 적용 36/36, 픽셀 불일치 30/36**은 그대로다. 원자료: `tools/optimization/validation/21b22b-sorting-delta-game.json`. 이 시험을 재실행하지 않았다.

새 독립 시험은 재질 생성 순서 6 × 렌더러 생성 순서 6 × 텍스처 생성 방향 2 × 정렬층 구성 2 = **144조건**이다. 마지막 구성은 서로 다른 sortingLayerID가 아니라 **같은 sortingLayer에서 order 0/1의 두 층**이다. 빨강/초록/파랑 alpha .55 스프라이트 3장이 같은 위치에 겹친다. 모든 재질의 셰이더/프로퍼티는 같게 했다. 원래 별도 재질·별도 흰색 텍스처 → 공유 재질·별도 텍스처 → 공유 재질·동일 아틀라스 텍스처를 비교했다.

|검사|최종 결과|
|---|---:|
|같은 원본 상태로 두 번 렌더한 불일치|0/144|
|공유 재질이 실제로 적용됨|144/144|
|명시적 order를 뒤집는 양성 대조가 차이를 검출|144/144|
|원본 vs 재질 공유 픽셀 불일치|99/144|
|원본 vs 공유+아틀라스 픽셀 불일치|96/144|
|아틀라스 상태에서 Transform root sibling 순서를 뒤집어 생긴 변화|0/144|

최종 `sorting-v2`는 원본/공유/아틀라스 캡처를 **명시적 order로 기준 그림 6개를 만들기 전에** 수행한다. 기준 그림 생성 과정이 비교 전 동률 내부 순서를 건드리는 교란을 제거했다. 초기 탐색본은 최종 근거로 사용하지 않는다.

동률이 3장 모두에 걸리는 72조건만 따로 보면:

|순서 가설|원본 순서와 일치|공유+아틀라스 순서와 일치|
|---|---:|---:|
|재질 GetInstanceID 숫자 오름차순|72/72|12/72|
|재질 생성 역순|72/72|12/72|
|렌더러 생성 순서|12/72|72/72|
|렌더러 GetInstanceID 숫자 내림차순|12/72|72/72|

**이 표본에서 원본은 재질 ID 순서, 공유+동일 텍스처는 렌더러 생성 순서와 일치했다. 두 경로는 같은 규칙이 아니었다.** 생성 순서와 ID가 연동되므로 ID 자체가 엔진의 비교 키라는 인과 증명은 아니다. 공유 재질·서로 다른 텍스처의 순서는 이 단순 가설들로 설명되지 않았다. native ID/배치 내부 키라는 해석은 **추정**이다. 다른 버전/재생 재시작/오브젝트 재사용에서도 이 순서가 보장된다고 사용해서는 안 된다.

근거: `codex-gpu3-20261018-sorting-v2/sorting.tsv`, `sorting-summary.json`, `sorting-v2-editor.txt`.

### Arche 광선 주장 검증

원본 `[𝐃𝐎 𝐍𝐎𝐓 𝐎𝐏𝐄𝐍 𝐓𝐇𝐈𝐒 𝐀𝐓 𝐇𝐎𝐌𝐄] FULL.adofai`의 이벤트 줄 **137,452개**, 개별 객체 파싱 오류 **0개**를 읽었다. 파일 SHA256은 `map.json`에 있다. 전체 파일은 trailing comma와 최상위 배열 뒤 누락된 comma 때문에 표준 JSON 파서가 거절하므로, 원본을 수정하지 않고 각 이벤트 객체 줄을 파싱했다.

- floor 3400~3699: 광선 AddDecoration **15,000개 = 300타일 × 50개**.
- depth -21..28: 각 depth에 **300개**. `tile=[1,1]`, imageSmoothing Enabled 각각 15,000개다.
- 이 범위의 실제 ray 이미지는 **6종**이다. 맵 전체 ray 이미지는 **8종**, ray AddDecoration 22,960개다.
- 예시 실제 데이터: **3440=90°, 3441=90°, 3442=180°, 3443=30°** 각 50개. 인수인계의 3440→3443 예시와 정확히 같지는 않다.
- fog 계열은 맵 전체에서 4종(`fog`, `nes_black_fog`, `nes_large_fog`, `nev_fog`)을 발견했다. 제안한 “3종”을 어느 구간의 어떤 파일로 제한한 것인지는 미확인이다.

**2,002개 가시 장식, 953묶음, 수 묶음이라는 수치는 이번에 확인하지 못했다.** 맵 파일만으로 258·270초의 누적 애니메이션·현재 alpha·카메라 bounds/가시성·동률 거리를 재현하지 않았다. 정적 15,000개를 화면 후보 수로 바꾸어 쓰지 않는다. 근거: `codex-gpu3-20261018-map.json`, `map-audit.txt`.

### 동률 제외 정책과 남는 수의 한계

안전한 후보 정책은 다음과 같다. 같은 카메라의 queue/정렬층/order/투명 거리/SortingGroup 등 상위 정렬 조건이 동률인 **전체 렌더러**를 먼저 모은다. 공유 대상끼리만 비교해서는 안 된다. 대상과 동률인 원본 비대상 렌더러도 순서가 바뀔 수 있다. screen-space bounds가 겹치는 모든 위험 쌍의 양 끝을 제외하고, 남긴 대상의 생성 순서/재질 순서 역전 가능성까지 확인한다. AABB 중첩은 실제 불투명 픽셀 중첩의 보수적 상한이다. 임의 shader 변형/SortingGroup/다중 pass/마스크/불명확한 거리도 제외한다.

맵의 변환·색·이미지·카메라가 움직이므로 **258초 한 스냅샷에서 안전했다는 이유로 258~274초 전체를 공유하면 안 된다.** 구간 전체 swept bounds나 모든 관련 상태 전환의 안전성을 검증하지 못하면 제외한다. CPU마다 프레임 O(N²) 검사를 새로 넣지 않는다.

실제 bounds를 읽지 않았으므로 이 정책의 **258초/270초 잔존 수는 미측정**이다. 대신 “같은 depth/order가 둘 이상이면 무조건 제외”하는 더 보수적인 정적 정책은 위 15,000개 광선을 **전부 제외(남는 ray 0개)**한다. 이는 실제 화면에서 안전하게 남길 수 있는 수가 0이라는 뜻은 아니다. 현재 근거로는 광선 2,002개를 수 묶음으로 만든다는 기대를 채택할 수 없다.

## B. 아틀라스 표본화

실제 Arche PNG **18종**(nev_travel_* 14종 + fog 4종)을 사용했다. 자르기/회전 패킹 없이 전체 width×height를 보존했고, Sprite FullRect·pixelsPerUnit·pivot을 쌍마다 같게 했다. padding 1/4/16, POT/NPOT page, mip 없음/있음, 회전 0/17/63°, scale .7 및 x축 반전 -1.3, pivot (.5,.5)/(.23,.71) 조합이다. filter Bilinear, wrap Clamp, aniso 1.

원본과 atlas 모두 같은 SpriteRenderer·재질·색으로 연속 렌더한다. atlas는 가장자리 **RGBA 전체**를 복사하고 남은 page 전체까지 Clamp 값으로 채웠다. 투명 texel의 RGB를 지우거나 premultiply하는 전처리는 없다. 다른 그림이 이웃에 없도록 해 이웃 오염을 제거한 조건이다. 검은 fog도 양성 검출되도록 회색 계열 배경과 무장식 기준 그림을 사용했다.

|최종 PNG 입력 검사|조건|불일치 조건|최대 채널 byte 차이|
|---|---:|---:|---:|
|mip 없음, padding 1|432|187|1|
|mip 없음, padding 4|432|213|1|
|mip 없음, padding 16|432|216|2|
|mip 있음, 세 padding 전체|1,296|1,166|57|
|전체|2,592|1,782|57|

그림을 검출하지 못한 조건 **0**, 같은 상태 반복 캡처 불일치 **0/2,592**, 입력 텍스처 byte 변경 **0**이다. 초기 검은 배경 탐색본은 black fog를 양성 검출하지 못했으므로 최종 수치에서 폐기했다.

**n=16까지 늘려도 mip 없는 조건에서 0차이가 아니다.** 이웃 그림이 없는 조건에서도 차이가 났으므로 padding만 늘려 해결됐다고 볼 수 없다. 원본 texel→atlas UV의 정규화/보간·래스터 반올림 차이는 원인 **추정**이며, 이 실험만으로 특정 GPU 연산 하나로 확정하지 않았다. mip는 개별 텍스처와 atlas 전체의 레벨 생성/크기/LOD가 달라지는 위험도 더한다. 기본 mip 없는 게임 경로와 이 스트레스 조건을 구분한다.

이는 과거 AlphaCrop의 Bilinear 차이(8조건 각 9~31픽셀)를 투명 여백 보존만으로 해결했다고 주장할 수 없다는 새 반례다. 이번 조건에서도 **보장 가능한 padding/UV 조건을 찾지 못했다.** 실제 Tile 셰이더, 게임의 Tight mesh/OverrideGeometry, 다른 색 공간·RT·압축 조합까지 통과한 것은 아니다.

### DXT·축소 입력은 별도 판정

별도 Editor Texture2D.Compress(false) 시험은 반환 format을 확인했다. 18종 중 **실제 DXT5가 된 nev_fog.png(100×100) 1종만** 유효 압축 시험으로 채택했다. 이 DXT 입력을 GetPixels32로 풀어 RGBA atlas에 넣은 72조건은 **72조건 모두 불일치**, 최대 byte 차이 **1**, 반복 캡처 차이/원본 변경/빈 그림 **각 0**이었다.

나머지 17종은 Compress 뒤에도 ARGB32/RGBA32여서 DXT 통과/실패 수에 넣지 않았다. 다수는 171×171 같은 4의 배수가 아닌 크기라 Editor 경고가 났다. 1489×1489 large fog는 이 참고 경로의 GPU Blit 축소 결과가 1023×1023이라 역시 DXT가 아니었다. 이 GPU 축소는 게임 native 줄이기와 동일한 결과가 아니다. `compression-skips.txt`에 실제 크기/형식을 기록했다.

**게임이 이미 1024 한도/DXT로 올린 실제 상주 텍스처는 읽지 못했다.** PNG로 Editor에서 만든 DXT가 게임 TexCompress의 결과와 같다고 주장하지 않는다. 게임 입력의 검증은 미완료다. 향후 블록 복사 방식이라도 x/y/복사 영역의 4×4 정렬, NPOT 끝 블록의 Clamp, 각 mip, sRGB 해석을 따로 확인해야 한다. 압축을 풀어 재압축하는 방법은 원래 decoded texel 보존을 가정할 수 없다.

근거: `codex-gpu3-20261018-fixture/sampling.tsv`, `fixture-dxt/sampling.tsv`, `fixture-dxt/compression-skips.txt`, `summary.json`, `sampling-v2-editor.txt`, `dxt-v2-editor.txt`.

## C. 소비자 IL 조사와 배치 설정

게임 Managed와 설치 Mods의 DLL 파일 **244개** 중 관리 어셈블리 **239개**를 스캔했다. native 4개(gpuprobe/libdeflate/sfnative/turbojpeg)와 macOS `._YouTubeStream.dll` 보조 파일 1개는 IL 대상에서 제외했다. 메서드별 call/field 참조 행 **1,155개**를 보존했다. 게임·모드의 직접 Sprite/SpriteRenderer/재질 getter/setter, wrap/filter/압축·업로드, scrVisualDecoration와 CustomSprite 필드 경로를 포함한다. reflection으로 만들어진 호출·네이티브 구현·나중에 로드될 DLL의 완전성까지 증명하지 않는다.

|소비자|확인한 경로와 영향|
|---|---|
|scrVisualDecoration.SetSprite 두 오버로드|_sprite/주 렌더러/SpriteMask/SpriteAlphaMask의 sprite를 갱신. material getter→SetPointSamplingVariant, sprite.texture.wrapMode 쓰기. Renderer.sprite만 갈아 끼우는 대체는 이 상태들과 어긋날 수 있음|
|ResizeCollider / spriteUnscaledSize / GetDecorationWorldSize|sprite.bounds 및 BoxCollider2D.size를 사용. spriteUnscaledSize는 backing field. rect·pivot뿐 아니라 mesh bounds와 기존 캐시 초기화 시점을 보존해야 함|
|**GetDecorationPixelsSizeInUnits**|**sprite.texture.width/height ÷ pixelsPerUnit**. sprite.rect 크기를 읽지 않음. atlas 크기를 원본 크기로 취급하는 추가 차단 요인|
|**SetTextureScaleMultiplier**|**sprite.texture.width/height**를 읽음. sizeUsed 효과만 처음 제외해도 이후 Setup/재시작/다른 호출 경로를 보호하지 않으면 깨질 수 있음|
|SetTile / UpdateDecorationTilingLateUpdate|Renderer.material 접근. repeatX/repeatY 및 texture.wrapModeU/V 쓰기. atlas page를 공유하면 다른 장식에도 sampler 변경이 전파될 수 있음|
|RDUtils.SetPointSamplingVariant|EnableKeyword/DisableKeyword. smoothing이 같아도 이후 변화 때 공유 재질 변경/재복제를 처리해야 함|
|SetBlendMode / UpdateShader / 마스크|shader·별도 mesh 재질·RT/Blit·texture 크기·mask cache를 사용. 시작 때 blend/mask None만 검사하면 이후 변화에 취약|
|FastMove.ImageBlock|이미지 사전의 CustomSprite로 SetSprite. size/smoothing/mask 변화도 처리. 기존에 갈아 끼운 atlas Sprite만으로 이후 교체를 유지할 수 없음|
|LazySprite|투명할 때 원래 CustomSprite를 pending에 보관. Defer도 원래 텍스처 wrapMode=Clamp를 쓰며 보이기 전에 SetSprite를 flush. pending과 Renderer.sprite만 대체한 상태가 불일치할 수 있음|
|InvisibleSkip|알파에 따라 forceRenderingOff 변경 및 LazySprite flush. 정적 가시 집합을 구간 전체로 가정할 수 없음|
|ExitFix.LightOne|원래 Sprite 참조 일치 확인 뒤 sr.material.SetPointSamplingVariant와 spr.texture.wrapMode=Clamp를 다시 실행. 재시작에서도 공유가 풀리거나 공유 상태 쓰기가 일어날 수 있음|
|ImagePrefetch|텍스처 Reinitialize/LoadRawTextureData, 줄인 비율에 따른 Sprite.Create pixelsPerUnit 조정. PNG 원본 치수 대신 **최종 Sprite의 실제 ppu/rect/geometry**를 사용해야 함|
|TexCompress|크기/형식/읽기 가능 조건 확인 뒤 DXT1/5 Reinitialize/LoadRawTextureData. DirectAllowed 경로는 원래 RGBA 업로드도 생략. atlas 입력은 이 최종 경로 이후여야 하며 실제 format을 확인해야 함|
|AssetDiskCache|PersistentBlobCache의 byte 저장소. ImagePrefetch가 캐시 블록을 다시 사용함. atlas sprite/UV로 캐시 키/원본 텍스처 수명을 바꾸지 않음|
|기타 Sprite.texture 소비자|scnEditor.MakeThumbnail, SpriteAlphaMaskUtils, Quartz SpriteRegistry.Destroy 등도 발견. 전역 CustomSprite/리소스 사전을 atlas 객체로 바꾸면 원본 소유·파괴 경로까지 검토 필요|

근거: `codex-gpu3-20261018-consumers.tsv`, `scan-manifest.json`, `scrVisualDecoration-il.txt`, `scrDecorationManager-il.txt`, `TextureManager-il.txt`, `RDUtils-il.txt`. 모드 소스의 FastMove.ImageBlock/LazySprite.Defer/ExitFix.LightOne/ImagePrefetch.LoadImage/TexCompress.Substitute도 대조했다.

### 실제 Player batching 활성 여부: **미확인**

오프라인 확인 수치: globalgamemanagers serialized version **22**, Unity **6000.3.21f1**, enableTypeTree **0**, serialized type **20개**. Editor LoadSerializedFileAndForget는 객체 **0개**를 반환했다. 따라서 PlayerSettings.dynamicBatching 값을 신뢰할 수 있게 읽지 못했다. 현재 프로젝트의 Editor 설정은 이 게임의 빌드 설정 증거로 쓰지 않았다.

실제 릴리스 Player의 **Batches Count / SetPass Calls Count / Draw Calls Count는 이번 조회 0회, 값은 null(미측정)**이다. 이 셋을 0으로 측정했다는 뜻이 아니다. 실행 중인 게임에 명령을 보내는 일도 하지 않았다. `Camera.onPreCull` 이벤트 존재, 재질/텍스처 개수, census의 추정 run 수만으로 batching 활성/실제 draw 수를 확정하지 않는다. Sprite 전용 배치와 일반 Mesh 동적 배치도 같은 기능이라고 가정하지 않는다.

**C의 “실제 Player 배치 활성 여부를 숫자로 확인” 요건은 미완료**다. `codex-gpu3-20261018-batching.json`에 미측정과 실패 근거를 명시했다. A/B가 실패했으므로 이를 우회해 프로토타입이나 성능 주장을 진행하지 않았다.

## D. 판정·대안·Claude 후속 절차

A/B/C의 통과 조건을 충족하지 않아 **DecoAtlasProbe, atlasprobe on/off/state, 공유 재질·아틀라스 제품 적용 모두 추가하지 않았다.** 원복·수명·게임 픽셀·GPU/FPS 시험도 수행한 것으로 쓰지 않는다.

- **재질 공유만**: 새 fixture 99/144 불일치라 대안으로 바로 채택할 수 없음.
- **아틀라스 없이 배치 키만 같게 하기**: 내부 투명 순서 보존이 증명되지 않으며, 서로 다른 _MainTex를 단순 키 변경만으로 같은 텍스처 draw로 만들 수 없음.
- **동률 제외**: 위 보수적 정책/구간 전체 검증으로 남는 실제 수부터 확인. 광선은 정적 완전 제외 시 잔존 0개이므로 큰 절약은 현재 보장할 수 없음.
- **일반 채택**: 정렬이나 UV를 강제로 바꾸어 근사하거나 미세한 픽셀 차이를 허용하는 방식은 이번 0차이 조건을 바꾸므로 도입하지 않음.

설치·게임 실행 권한이 있는 후속 담당자가 할 순서다. 이번 세션에서는 실행하지 않았다.

1. PlayerAuto + 기존 gpuprobe를 준비한다. 해상도/주사율/D3D11/LowRenderScale/vSync/프레임 제한/보간/FuseLate를 고정하고, 기존 실행 중인 판과 섞지 않는다.
2. 정렬 판은 실제 게임 **Tile 셰이더**로 같은 fixture를 옮겨 테스트하되 제품 재질 풀은 켜지 않는다. 실제 renderer/material/texture ID, 반복 캡처 0차이, 명시적 order 양성 대조를 같이 보관한다. 위 Editor ID 규칙을 게임의 고정 규약으로 가정하지 않는다.
3. 258초와 270초 각각 별도 새 판에서 다음 기존 명령만 실행한다. shotat은 해당 시간을 **처음 넘긴 프레임**이며 정확한 고정 시각이 아니다. 실제 request_song_s/unity_frame 로그를 남긴다.

```text
auto on
game D:/얼불춤 맵 파일/Arche/[𝐃𝐎 𝐍𝐎𝐓 𝐎𝐏𝐄𝐍 𝐓𝐇𝐈𝐒 𝐀𝐓 𝐇𝐎𝐌𝐄] FULL.adofai
gpuprobe on
press
shotat 258 gpu3-baseline-258
gpucensus
gpuprobe state
gpuprobe off
```

270초 판은 `shotat 270 gpu3-baseline-270`으로 바꾼다. census는 다음 렌더 콜백의 일회성 snapshot이고 캡처보다 늦을 수 있다. **258/270 정확한 동일 상태의 제외 후 수를 얻으려면** 후속 일회성 fixture가 한 번의 고정된 카메라/곡 상태에서 bounds·현재 sprite/material/모든 동률 렌더러를 함께 읽어야 한다. 현재 두 명령을 같은 프레임이라고 간주하지 않는다.

4. 배치 카운터는 릴리스 Player에서 실제 recorder 핸들 Valid/표본 수/값을 기록하는 일회성 진단으로 먼저 확인한다. 안 읽히면 안 읽힌다고 기록하고 Frame Debugger/동일 Sprite 쌍의 실제 draw 관찰 등 다른 직접 근거가 필요하다. ID run 추정으로 대체하지 않는다.
5. 258~274초 **기준 GPU 판**은 census/캡처와 분리한다. 아래 기다리기는 실시간이어서 정확한 곡 창은 실제 songposition 로그로 확인해야 한다. 필요하면 기존 자동 시험의 시간 대기 조건을 후속에서 보완한다. 곡 중 shader/Harmony 설치는 하지 않는다.

```text
auto on
game D:/얼불춤 맵 파일/Arche/[𝐃𝐎 𝐍𝐎𝐓 𝐎𝐏𝐄𝐍 𝐓𝐇𝐈𝐒 𝐀𝐓 𝐇𝐎𝐌𝐄] FULL.adofai
gpuprobe on
press
wait 150
wait 108
gpuprobe reset
wait 16
gpuprobe state
gpuprobe off
```

이 판의 FPS를 측정 오버헤드 없는 FPS로 부르지 않는다. FPS는 probe를 끈 별도 새 판에서 같은 실제 곡 창으로 비교한다. 기존 본 카메라 **6.21ms는 인계 수치**이며 이번 재측정 값이 아니다.

6. 현재는 대체 화면이 없으므로 “원본 vs atlas” shot 쌍/절약량은 만들 수 없다. 나중에 A/B/C가 통과할 때만, **한 고정 상태 안에서 시간 진행 없이** 원본과 대체를 순차 렌더하고 alpha 포함 RGBA byte를 비교하는 캡처를 구현한다. 별도 판 shotat 두 장은 곡 시각·카메라·애니메이션 입력/위상이 같다는 증명이 없으므로 0차이 채택 근거로 쓰지 않는다. 동일성 통과 뒤에만 새 판 ABBA GPU 및 별도 무계측 FPS를 258~274초에서 측정한다.

## 재현·빌드·보존

임시 프로젝트는 `%TEMP%/stutterfix-gpu3-20261018/UnityFixture`에 남겼다. `Assets/Editor/Gpu3Fixture.cs`를 저장소 도구 파일에서 복사한 뒤, 아래처럼 **Editor만** 실행한다. -nographics를 넣으면 GPU 시험이 아니다. 출력 폴더는 새 위치로 지정하는 것을 권한다.

```powershell
$env:SF_GPU3_OUT = 'C:/Users/Public/StutterFixTrace/gpu3-repeat'
$env:SF_GPU3_SORTONLY = '1'       # 정렬만. sampling은 이 변수를 제거하고 SAMPLEONLY=1
& 'E:/Unity/Editor/6000.3.10f1/Editor/Unity.exe' -batchmode -quit -force-d3d11 -projectPath "$env:TEMP/stutterfix-gpu3-20261018/UnityFixture" -executeMethod Gpu3Fixture.Run -logFile "$env:TEMP/gpu3-repeat.log"
# PNG: SORTONLY 제거, SF_GPU3_SAMPLEONLY=1
# DXT: SORTONLY/SAMPLEONLY 제거, SF_GPU3_COMPRESSED=1 (실제 format 확인, 미압축 결과 제외)
```

각 실행은 `done.txt`와 Editor 로그의 성공 종료를 함께 확인한다. 이전 run 출력 파일만 남은 실패 판을 성공으로 취급하지 않는다. 집계는 `py -3 tools/gpuprobe/gpu3_summarize.py`. 입력 절대 경로는 연구용 도구에 명시되어 있다. 게임/Player와 같은 셰이더나 환경을 자동으로 재현하는 범용 시험기가 아니다.

|검사|결과|
|---|---:|
|Developer Release 로컬 빌드|오류 0 / 기존 경고 35|
|Player Release 로컬 빌드|오류 0 / 기존 경고 19|
|PlayerAuto Release 로컬 빌드|오류 0 / 기존 경고 35|
|Python 연구 스크립트 py_compile|오류 0|
|git diff --check|오류 0 (.codex/config.toml 기존 LF/CRLF 경고)|
|gpu2 코드·프로젝트·설정 7파일 SHA256|전부 동일|
|설치 DLL·Settings.xml·boot.config·globalgamemanagers|동일|

빌드 로그는 `codex-gpu3-20261018-{dev,player,playerauto}-build.txt`. 빌드 산출물은 bin/obj뿐이며 자동 build-install 훅은 호출하지 않았다. 새 Editor 파일은 제품 컴파일 목록에 없고 제품 프레임 루프/이벤트/Harmony 설치 변경 0이다.

보존 검사 대상은 **308파일**이다. 변경이 관찰된 것은 기존 실행/외부 작업의 `Mods/StutterFix/autotest.done`, `session.lock`, `tilecull-scan.txt` **3개 기록 파일**이다. 이 세 파일에 쓰기 명령을 보내지 않았으며 과거 값으로 되돌리지 않았다. 따라서 “Mods 전체 byte 불변”이라고 쓰지는 않는다. 근거: `codex-gpu3-20261018-preservation-{before,after}.json`.

**남은 미완료:** 실제 게임 Tile/최종 DXT·1024 입력 동일성, 구간별 bounds 제외 후 수, 릴리스 실제 batching 수, Arche 258·270초 동일 상태 픽셀, GPU/FPS. 현재 결과는 공유/아틀라스 채택을 차단하는 근거이며, 게임 성능 개선의 근거가 아니다.
