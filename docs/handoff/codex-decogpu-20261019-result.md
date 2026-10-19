# DecoGpu 1단계 결과 — 설계와 진단 전용 프로토타입

2026-10-19. **DEV/AUTOTEST 전용 프로토타입과 독립 Editor 렌더 검증까지 구현했다. 실제 게임 성능·Arche 252초 구성·게임 픽셀 동일성은 미측정이다. 2단계 채택 판정은 보류한다.**

게임 실행·제어, Mods 설치, 커밋·푸시는 하지 않았다. 독립 임시 Unity Editor 프로젝트만 실행했다. 기존 `.codex/config.toml`과 미추적 작업 파일은 보존했다. 문서는 `apply_patch`로 UTF-8 BOM을 넣어 작성했다.

## 1. 전용 구조 설계

### 데이터와 지원 범위

새 효과를 쓰는 장식만 명시적으로 등록한다. 기존 MoveDecorations의 전체 장식에 자동 적용하지 않는다. 장식별 고정 ID와 연속 배열을 두고 `position0/1`, `rotation0/1`, `scale0/1`, `color0/1`, `songStart`, `duration`, `easeCode`, `parallaxXY`, `tileAnchor`, `pivotOffset`, `spriteId`, `sortBand`를 저장한다. GPU 경로라면 C#/HLSL의 float4 단위 레이아웃·stride를 명시하고, bool 대신 uint를 사용한다. 변경된 슬롯만 업로드한다. 시작/완료/충돌한 효과의 쓰기 순서도 ID와 함께 기록한다.

첫 지원 이징은 Linear, In/Out/InOut Quad·Cubic·Quart·Quint로 한정한다. 원본 시간 경계·완료 시 끝값 대입과 다항식 계산 순서를 명시하고 `ZeroTween.Eval`을 기준으로 수치 대조한다. 현재 `ZeroTween.Eval`은 DOTween 이징 평가 호출을 감싼 함수다. GPU에 그대로 호출할 수 있는 함수가 아니다. Sine/Expo/Circ/Elastic/Back/Bounce, 커스텀 곡선·콜백, 지원하지 않는 중첩은 원래 경로로 돌린다. GPU 부동소수점 결과의 비트 동일성을 가정하지 않는다.

### 계산 위치 비교

|방식|프레임 CPU 작업|GPU 작업·제약|판단|
|---|---|---|---|
|A. 구조화 버퍼 + 셰이더 이징|곡 시각·카메라 상수와 바뀐 효과 슬롯 업로드, 배치 제출|정점별 이징·변환. 같은 스프라이트의 정점마다 계산이 반복된다. 투명 픽셀 비용은 남는다|2단계 후보. CPU 애니메이션 자체를 옮길 수 있지만 별도 HLSL·번들·동기화 필요|
|B. C#/네이티브 일괄 계산 + 인스턴싱|최종 행렬·색 배열 계산과 업로드, 배치 제출|정점 변환·스프라이트 샘플링. GPU 이징 없음|이번 프로토타입. DecoAnim은 그대로 진행|
|C. 정점 배열 갱신 + 기존 셰이더|모든 스프라이트 정점·색과 인덱스 업로드|기존 셰이더가 정점 색을 소비하는지 확인 필요|Editor 없는 대안. 인스턴싱보다 업로드량이 커질 수 있음(추정)|

Unity 6000.3 Mono/netstandard2.1 참조에 대해 빌드했다. 기존 `scrCamera`/OnRenderImage 기반 후처리 코드는 Built-in RP를 시사하지만 현재 게임을 켜 확인한 값은 아니다. `decogpu begin`은 `GraphicsSettings.currentRenderPipeline == null`, 하드웨어 인스턴싱 지원, 실제 게임 카메라의 Forward 경로를 검사하며 다르면 거부한다. 게임 버전은 실행 시 `Application.unityVersion`으로 조사 로그에 남긴다. 과거 GPU3 문서의 게임 버전 기록과 이번 Editor 버전을 같다고 가정하지 않는다.

### 제출·텍스처·정렬

이번 구현은 게임 카메라의 `BeforeForwardAlpha` CommandBuffer에 `DrawMeshInstanced`를 기록한다. 매 프레임 SpriteRenderer별 Transform/색 setter를 호출하는 대신 행렬과 인스턴스 색 배열을 넘긴다. 배치는 실제 Sprite의 vertices/uv/triangles를 복제한다. 텍스처 리사이즈·아틀라스·Tight 메시 재생성은 하지 않는다. 스프라이트별 원본 텍스처를 그대로 샘플링한다.

`Graphics.RenderMeshInstanced`는 새 API지만 SpriteRenderer의 sortingLayer/order 및 다른 투명 렌더러 사이의 순서를 자동으로 복원해 주는 API가 아니다. 이번에는 측정 대상 카메라와 명령 순서를 명확히 하기 위해 CommandBuffer 방식을 썼다. Unity의 인스턴싱 제출 계약은 [CommandBuffer.DrawMeshInstanced 공식 문서](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/rendering/commandbuffer/drawmeshinstanced)와 [공식 C# 바인딩](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Graphics/RenderingCommandBuffer.cs)을 참고했고 실제 참조 DLL로 컴파일했다. 이 문서 링크의 최신 버전을 6000.3 실행 검증으로 취급하지 않는다.

정렬층의 실제 값 → sortingOrder → InstanceID 순으로 정렬한다. **InstanceID는 원본 생성 순서나 네이티브 tie-sort의 증명이 아니다.** 그 정렬에서 인접한 같은 Sprite·재질 전체 속성 서명·층·order만 묶고 1,023개에서 나눈다. 따라서 실제 배치 수는 단순한 텍스처 종류 수보다 많다. 같은 텍스처라도 Sprite 메시/UV가 다르면 다른 배치다. order가 모두 다르면 거의 장식 수만큼 제출할 수 있다. `state`의 draw 수는 명령 제출 수이며 드라이버나 Frame Debugger의 실제 draw 수를 측정한 값이 아니다.

**현재 프로토타입은 기존 투명 장식·타일·행성과의 전역 정렬을 보존하지 못한다.** CommandBuffer 한 구간은 원본 투명 오브젝트 사이에 개별 장식을 끼워 넣지 않는다. 선택 장식끼리도 원본 동률 정렬을 보장하지 않는다. 성능 상한을 조사하고 차이를 드러내는 전용 경로이며 제품 채택 구현이 아니다.

2단계에서는 새 효과의 장식을 명시적인 렌더 밴드에 한정하고 그 밴드의 원본/대체를 함께 제어해야 한다. 일반 장식과 임의 교차가 필요하면 전체 투명 순서의 구간마다 draw를 나누거나 기존 SpriteRenderer로 돌려야 한다. 배치를 위해 비인접 장식을 무작정 텍스처순으로 재정렬하지 않는다. 텍스처 배열도 크기·포맷·밉·압축이 맞아야 하며 서로 다른 원본 입력을 변환하면 샘플링 차이가 생긴다. 아틀라스/텍스처 배열은 이번 구현에 없다.

### 색·블렌드·시차·타일

새 셰이더는 `tex2D(originalTexture, originalUV) × rendererColor × material._Color` 뒤 RGB에 알파를 곱하고 `Blend One OneMinusSrcAlpha`로 합성한다. 마스크·특수 블렌드·점 샘플링 변형·외부 알파 텍스처는 지원하지 않는다. 원본 Sprites/Tile의 모든 내부 분기나 정밀도를 재현했다고 주장하지 않는다. `_Color` 외의 임의 재질 속성은 조사 서명에 포함되지만 새 셰이더의 동작에 자동 이식되지 않는다.

시차 위치·회전·크기는 `TightLate.Apply` 계산식에서 가져왔다. `pivotPosVec`, `rotAngle`, `scaleVec`, `scaleMultiplier`, `textureScaleMultiplier`, `parallaxOffset`, 카메라 위치를 읽고 parallax의 start/end/posCamAtStart 필드도 갱신한다. 렌더러가 pivot 아래에 가진 고정 변환은 begin 시 상대 행렬로 저장한다. SpriteRenderer의 flipX/flipY는 최종 행렬에 적용한다. pivotOffset/croppedPivotOffset·자식 참조·textureScaleMultiplier·scaleMultiplier가 바뀌면 원본으로 복원한다.

현재는 `stickToFloor`, 행성 따라가기, 카메라 회전/크기 잠금, 화면/축 고정, 비항등 부모를 기존 TightLate 자격 판정으로 제외한다. 2단계의 타일 기준점은 명시적인 타일 ID·고정 오프셋으로 두고 타일 이동 결과를 별도 기준점 배열에 올린다. 임의 게임 Transform 참조를 GPU에서 역참조하지 않는다. 타일 투명도가 색에 관여하는 경우는 타일 상태까지 동기화하거나 원본 경로로 돌려야 한다.

### 복원 계약

곡 중 애니메이션은 계속되므로 end에서 begin 시점 위치로 되감으면 안 된다. begin 시 변경한 렌더 소유 상태(`forceRenderingOff`)는 저장값으로 되돌리고, Transform/색은 **end 시점의 최신 scrDecoration 필드**로 원래 `UpdatePosition`을 호출해 반영한다. sprite/material/enabled는 프로토타입이 바꾸지 않는다. parallax 필드도 최신 원본 계산 상태로 남긴다.

명시적 end, 곡 종료·일시정지·장면 정리·카메라 변경·진단 전제 변경, 유효성 상실, Setup/SetSprite/SetBlendMode/SetMaskingType/SetTile/SetVisible/EnableMeshRenderer 경계를 만나면 전체 세션을 해제한다. 일반적인 색·위치·회전·크기 애니메이션은 이번 측정에서 계속 진행한다. 2단계 새 효과에서는 일반 MoveDecorations가 같은 장식을 쓰기 전에 최신 GPU 결과를 필드와 엔진에 동기화한 뒤 소유권을 반환해야 한다. 그 GPU→필드 동기화는 이번 범위에서 구현하지 않았다.

Dormancy의 가시성 원천인 rendererColor/rendererEnabled를 유지한다. 원본 ApplyColor 뒤 InvisibleSkip이 forceRenderingOff를 바꿀 수 있으므로 선택 장식은 본 카메라 pre-cull에서 다시 숨긴다. alpha가 0이 되거나 Dormancy 목록에서 빠져 자격을 잃으면 세션 전체를 복원한다. 원래 숨겨진 장식은 begin 대상이 아니다.

`Main.UninstallAll`에서 `DecoGpu.Stop`을 먼저 부른다. Stop은 전용 Harmony ID·Camera 콜백·CommandBuffer·메시·재질·AssetBundle·진단 GameObject와 관리 참조를 해제한다. 원본 소유 Sprite/Texture/Material은 파괴하지 않는다. `restore-mismatch`는 원본 위치 평가의 두 번 호출 결과·필드·색·enabled·재질 유지와 forceRenderingOff 복원 검사를 집계한다. Transform 비교는 Unity 벡터/회전 비교이므로 바이트 동일 검사라고 부르지 않는다. 런타임 누수 0과 복원 불일치 0은 후속 게임 검증이 필요하다.

## 2. 구현 파일과 명령

|파일|역할|
|---|---|
|`DecoGpu.cs`|명령, 조사, 선정, 인스턴싱, 같은 프레임 픽셀 비교, 복원|
|`TightLate.cs`|DEV/AUTOTEST에서 소유 장식의 Late Transform 쓰기 우회, 캐시 무효화|
|`ColorFast.cs`|DEV/AUTOTEST에서 소유 장식의 색 필드만 진행하고 renderer setter 우회|
|`FuseProbe.cs`|동일 fuse 창에 DecoGpu 계산·제출 CPU ms 표시|
|`AutoTest.cs`, `StutterFix.cs`|명령 1개와 해제 연결 1개|
|`StutterFix.csproj`|새 C#와 새 번들을 FuseProbe와 동일 조건으로 포함|
|`effects/DecoGpu.shader`, `effects/stutterfix_decogpu`|진단용 인스턴싱 셰이더와 제작된 번들|
|`tools/effects/build_decogpu.ps1`, `BuildDecoGpu.cs.txt`|독립 임시 Editor 프로젝트에서 번들 제작|
|`tools/effects/DecoGpuFixture.cs.txt`|게임과 독립된 기본 스프라이트 대조 시험|

`decogpu begin [상한]`은 기본 500, 최대 10,000이다. 전부를 시도하려면 `decogpu begin 10000`을 쓴다. 켜려면 legacy 맵 재생 중, TightLate/ColorFast 켬, DecoEngine 끔, Built-in Forward/인스턴싱 지원이 필요하다. 외부 Harmony 훅이 확인된 주요 색·위치/LogicUpdate 경로는 거부한다. 명령은 제품 Player에는 없다. 기본 꺼짐이며 자동 활성화 설정도 없다.

`decogpu state`는 관리 장식/배치/최근 및 누적 제출 수/렌더 횟수/계산·제출 평균 CPU ms/누적 복원·불일치를 보고한다. `decogpu end`로 해제하고 같은 검사를 읽는다. `fuse`의 DecoGpu 값은 그 fuse 창 동안의 일괄 계산·제출 누적 tick을 Unity 프레임 수로 나눈 값이다. 기존 LateUpdate와 중복 합산하지 않도록 렌더 pre-cull에서 실행한다. CPU 측정이며 GPU 시간이나 전체 진단 오버헤드와 같지 않다.

`decogpu compare`는 같은 명령 처리 프레임에서 원본과 인스턴싱을 512×288 RT로 각각 Camera.Render하고, RGBA 최대 채널 차이와 차이 픽셀 비율을 보고한다. 선택 장식의 현재 원본 Transform/색을 반영한 뒤 A를 찍고, 원본을 숨겨 B를 찍으며 finally에서 RT·임시 텍스처·Transform/색/숨김을 돌려놓는다. 비교는 동기 readback이며 성능 판에 섞지 않는다. 카메라 콜백·후처리 이력·시간 기반 셰이더가 두 Render 사이 변할 수 있어 완벽한 고정 상태 비교는 아니다. 512×288은 원래 3440×1440의 샘플링 검증을 대신하지 않는다. 별도 원본 A/A 캡처의 차이도 대조해야 한다.

## 3. 조사 데이터와 선정 기준

이번에는 Arche를 실행하지 않았다. 약 2,100개·252~273초·CPU/GPU 기준값은 인계 문서의 Claude 실측이며 이번 재측정이 아니다. 현재 시점의 스프라이트/텍스처 종류 수, 마스크 비율, 정렬층/order, 배치 가능 수를 새 값으로 제시할 근거는 없다.

begin은 `DecoAnim.RunningDecorations()`를 한 번 조사하고 다음 로그를 남긴다. **이 모집단은 애니메이션 중인 장식이며 Dormancy의 모든 깨어 있는 장식과 정확히 같지 않다.** 정지해 있지만 보이는 장식은 포함하지 않는다.

- 전체 애니메이션 장식/이미지/현재 보임/마스크/히트박스/메시 수와 sprite·texture·material ID 종류 수.
- 텍스처, sortingLayer 이름·ID, sortingOrder, 셰이더, 키워드, queue, 색·벡터·float·텍스처 속성 전체 서명별 그룹 크기. 이는 실제 제출 가능한 연속 그룹 수와 다르다.
- 원본 renderer.bounds의 8개 모서리를 투영한 긴 변의 픽셀 분포: `<128`, `128~511`, `512~1023`, `1024~2047`, `2048 이상`. 화면 밖 영역을 클리핑하지 않은 bounds 근사이며 실제 채워진 안개 픽셀 면적이 아니다.
- 실제 Unity 버전과 RP. 원본 데이터는 `[decogpu] survey` 및 `group` 줄로 Player.log에 남는다. 이 한 번의 조사·문자열/속성 열거·셰이더/패치 설치는 측정 창 밖에서 한다.

깨끗한 이미지의 현재 기준은 정확한 scrVisualDecoration 타입 + 기존 TightLate 자격 + 켜진 SpriteRenderer + Simple draw + repeatX/Y=1 + 마스크/SortingGroup/MaterialPropertyBlock 없음 + 단일 pass Sprites/Default 또는 Sprites/Tile + 키워드 없음 + 외부 알파 없음이다. 같은 manager와 본 카메라 cullingMask에 속해야 한다. 바뀌는 pivot 기준 필드는 자동 복원 대상이다. 외부 모드가 Harmony를 통하지 않고 재질 속성·자식 Transform을 직접 바꾸는 경우까지 감지하지 못하므로, 그런 소비자가 있는 장식은 전용 경로의 적합 대상으로 인정하면 안 된다.

## 4. 실제 확인한 범위

### 셰이더 번들과 Editor 렌더

기존 effects 방식처럼 **Unity Editor가 있어야** 새 HLSL/ShaderLab을 플랫폼 셰이더로 컴파일하고 AssetBundle을 만들 수 있다. 런타임 Shader.Find는 이미 Player에 들어 있는 셰이더만 찾는다. enableInstancing을 켠 것만으로 기존 Sprite 셰이더에 새 인스턴스 색 속성이 생기지 않는다. 현재 PC의 `D:/StutterFixTools/UnityEditors/6000.3.10f1/Editor/Unity.exe`를 사용해 Editor 설치 없이 제작했다. 결과는 D3D11 전용이며 다른 그래픽 API는 미검증이다.

제작 명령은 `powershell -File tools/effects/build_decogpu.ps1`이다. 기본 임시 프로젝트는 `%TEMP%/StutterFix-DecoGpu-6000.3.10f1`. 게임 Mods에는 복사하지 않는다. 처음 프로젝트를 만들 때 `com.unity.modules.assetbundle`·`imageconversion`을 manifest에 넣고, instancing stripping을 KeepAll로 저장하고 ForceRebuildAssetBundle로 제작한다. 기존에 같은 이름의 불완전한 임시 프로젝트가 있다면 manifest의 모듈 항목도 확인해야 한다.

초기 빈 manifest는 로드할 수 없는 번들을 만들었고, StripUnused는 인스턴싱 변형 2개 중 1개를 제거했다. 두 문제를 수정했다. 최종 로그에서 vertex/fragment 각각 stripping 후 **2/2개 보존**, 독립 Editor에서 **최종 번들 로드 성공**을 확인했다.

최종 번들 SHA256: `790486639AB1393D1CCA39B6F93A99160610BA7FE6F9F527C8FF9A952B423452`.

독립 시험은 Unity Editor 6000.3.10f1, Direct3D11, Gamma, 128×128 ARGB32 linear RT, 합성용 RGBA 텍스처, 겹치는 스프라이트 2개, 명시적 sortingOrder 0/1을 사용했다. 회전·비균등 크기·flipX/Y 4조건에서 원본 Sprites/Default와 번들의 인스턴싱 렌더를 대조했다.

|조건|다른 RGBA 픽셀 / 16,384|최대 채널 차이 / 255|
|---|---:|---:|
|0|858|2|
|1|992|2|
|2|993|2|
|3|1,062|1|

원자료 요약: [editor-fixture.txt](decogpu-evidence/editor-fixture.txt). **0차이가 아니다.** 기본 셰이더와 새 float 기반 셰이더의 정밀도/합성 차이가 원인일 가능성은 추정이며 분리 검증하지 않았다. 게임의 Tile 셰이더·원래 재질·DXT·안개·동률 정렬·필터를 검증한 결과가 아니다.

시험을 재현하려면 fixture 파일을 임시 프로젝트의 `Assets/Editor/DecoGpuFixture.cs`로 복사하고 같은 Editor에 `-batchmode -quit -force-d3d11 -projectPath <임시 프로젝트> -executeMethod DecoGpuFixture.Run -logFile <로그>`를 준다. 이 렌더 시험에는 `-nographics`를 넣지 않는다. 출력은 `DecoGpuOutput/fixture.txt`다. Editor exit 0과 새 출력 시각을 함께 확인한다.

### 빌드·정적 검증

세 구성 모두 `dotnet build StutterFix.csproj -c Release -v q --nologo`에 아래 속성을 붙여 로컬 빌드했다. 설치 스크립트는 호출하지 않았다.

|구성|속성|결과|
|---|---|---|
|Dev|기본|오류 0|
|Player|`-p:Edition=Player`|오류 0|
|PlayerAuto|`-p:Edition=Player -p:AutoTestBuild=1`|오류 0|

기존 참조 버전 충돌·사용되지 않는 필드·구형 API 경고는 남아 있다. ILScan으로 Player DLL의 DecoGpu 타입 **0개**, PlayerAuto의 DecoGpu 타입 **1개**를 확인했다. 새 Compile/EmbeddedResource와 기존 파일의 새 분기는 DEV/AUTOTEST 조건으로 제한했다. 제품 Player에 새 렌더 경로나 새 셰이더를 넣지 않는다.

## 5. 예상 효과 — 모두 추정, 개선 실측 아님

다음 범위는 배치 수가 적고 대부분의 2,100개가 선정되는 가정이다. 실제 Sprite/order 분포 때문에 수천 배치가 되면 오히려 느려질 수 있다.

|항목|인계의 기존값|이번 프로토타입 후 추정|
|---|---:|---:|
|DecoAnim 애니메이션 갱신|약 1.80ms|약 1.80ms 유지. GPU 이징은 구현하지 않음|
|관리자 LateUpdate|약 1.17ms|선정 비율에 따라 감소. 자격 검사·목록 순회는 남음|
|추가 일괄 계산·제출|없음|2,100개·적은 배치 가정 0.3~1.0ms. Mono 실측 필요|
|개별 color setter|약 0.3ms|선정 장식만 우회. 전체 0.3ms 절약을 가정하지 않음|
|FinishFrameRendering 등|약 1.7~2.3ms|0.8~2.3ms 가정. 감소 폭 미확인, profsamp로 확인|
|GPU 총시간|약 4.7~6ms|약 4.7~6.5ms 가정. 안개 픽셀 병목은 계속되며 악화 가능|
|전체 메인 스레드|약 7~10ms|측정 전 새 총 ms를 확정할 수 없음|

500/2,100개만 고르면 단순 비례로 Transform+색의 절약 상한은 `(1.17+0.3)×500/2100 ≈ 0.35ms`다. 거기에서 계산·제출 비용을 빼야 한다. 이 숫자는 위치/색 비용이 균일하고 두 시간이 독립이라는 낙관적인 추정이다. 실제로 color 비용은 애니메이션 갱신 안에 포함될 수 있으므로 합산으로 총 CPU 시간을 만들어서는 안 된다. 기본 500개 판은 기능·복원 확인용이다. >1.5ms 판정은 가능하면 전체 선정 판과 FinishFrameRendering 변화까지 확인해야 한다.

CPU 진단 오버헤드도 존재한다. begin/compare/end는 할당·동기 렌더·패치 설치·복원 때문에 측정 창 밖으로 뺀다. 정상 프레임에서는 전체 재질 속성 열거/문자열 생성/프레임 로그/장식마다 Stopwatch를 쓰지 않는다. 고정 배열을 재사용하며 배치 단위로만 시계를 읽는다. 다만 매 장식 자격 검사·Unity getter·소유권 lookup과 매 배치 색 배열 업로드는 남는다. 이 비용이 절약을 지울 수 있으며 전체 프레임 A/B가 최종 근거다.

## 6. Claude 후속 실행 순서

다음은 **후속 실행자가 별도 승인된 세션에서** 수행할 절차다. 이번 작업에서는 실행하지 않았다.

1. 같은 DLL/설정·3440×1440/같은 Hz·D3D11·수직동기·다른 모드·이미지 한도·해상도를 고정한다. 현재 적용 DLL을 별도로 확인한다. 최초에는 새 프로세스에서 Arche를 열고 실제 곡 252초 선정/조사 판을 한 번 돌린다. begin의 조사·번들 로드·Harmony 패치 설치·첫 셰이더 사용은 성능 창 밖으로 뺀다.
2. 별도 정확도 판에서 `waitsongpos 252 400`, `decogpu begin 500`, `decogpu state`, `decogpu compare`, `decogpu end`, `decogpu state` 순서로 실행한다. compare는 성능 판과 분리한다. 원본 A/A 통제와 실제 해상도의 shot도 확인한다. 작은 RT 0차이만으로 채택하지 않는다.
3. 중간 end, 일시정지, 다시 하기, 편집 복귀, 일반 sprite/mask/blend 변경, alpha 0, 모드 내리기를 각각 시험하고 `active=False`, 관리/배치/제출 잔존 0과 `restore-mismatch` 증가 0을 확인한다. 종료 후 메모리/Unity 객체·패치/콜백 잔존도 검사한다. destroyed 장식은 실존 객체의 복원 성공 수와 구분한다.
4. N=500 검증 뒤 N=10000을 시도해 실제 선정 수·배치 수를 기록한다. 2,100개가 전부 선정된다고 가정하지 않는다. `decogpu state`에서 세션이 경계 때문에 자동 종료됐으면 그 판은 성능 판에서 제외하고 이유를 기록한다.
5. 새 프로세스 **A/B/B/A**로 각각 같은 곡 창을 잰다. A는 decogpu 없음, B는 켬이다. 설정 토글을 위한 제품 설정은 추가하지 않았으므로 `sf_ab`의 setting 인자에 가상의 DecoGpu 설정을 넘기지 않는다. `sf_run`을 네 번, A/B 명령 목록만 달리 실행한다. 게임 재사용 A/B나 같은 DLL reload는 쓰지 않는다.

정확한 252~273초 창의 명령 예시는 다음과 같다. 실제 맵 여는 명령과 재생/press는 기존 Arche 시험 절차 앞부분을 사용한다.

```text
# A: fuse 패치는 미리 설치하고 버린다. gpuprobe도 재생 전에 설치한다.
fuse begin
fuse end
waitsongpos 252 400
fuse begin
profsamp begin 1000
waitsongpos 273 400
profsamp end
fuse end

# B: 먼저 실제 252초에서 조사/선정하는 기능 판을 끝낸 뒤 측정한다.
# 정확한 252~273 창에서는 250초에 준비하고 252초까지 워밍업한다.
fuse begin
fuse end
waitsongpos 250 400
decogpu begin 10000
waitsongpos 252 400
decogpu state
fuse begin
profsamp begin 1000
waitsongpos 273 400
profsamp end
fuse end
decogpu state
decogpu end
decogpu state
```

250초와 252초의 선정 모집단은 다를 수 있다. 원래 요구대로 252초에 begin한 판은 `waitsongpos 252 → decogpu begin → 워밍업 → fuse/profsamp begin`으로 기록하되, 설치·초기화가 끝난 실제 곡 시각을 남겨 A도 같은 시작 시각에 맞춘다. 이를 정확한 252~273초 고정 창으로 부르지 않는다. `waitsongpos`의 외부 자동 시험 step timeout도 400초 이상으로 맞춘다.

GPU 시간은 별도 새 프로세스 ABBA에서 `gpuprobe on`을 재생 전에 설치하고 같은 창의 reset/state로 비교한다. census·survey·compare·shot을 성능 창에 섞지 않는다. `profsamp`는 샘플 비율이므로 FinishFrameRendering의 절대 ms 감소를 단독 확정하지 않는다. 표본 포함 비율·전체 메인 스레드 시간·GPU probe/PresentMon을 함께 해석한다. 계측 없는 프레임 시간도 별도로 확인한다.

**판정 기준:** 같은 입력 구간에서 Update+LateUpdate+FinishFrameRendering을 포함한 메인 스레드 시간이 >1.5ms 감소하고, GPU 시간이 늘지 않으며, 정렬·색 차이가 설명되고 허용된 경우에만 새 효과 구현으로 진행한다. 현재는 전역 겹침 순서와 실제 게임 픽셀/성능이 미확인이고 독립 시험도 0차이가 아니므로 그 조건을 통과하지 않았다.
