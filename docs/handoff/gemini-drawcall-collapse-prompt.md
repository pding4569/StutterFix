# Claude 작업 지시서 — 투명 스프라이트 Draw Call 1/20 이하 감축 (2,500회 → 수십 회) 및 260~400+ FPS 돌파

너는 Unity 6000.3 (`6000.3.21f1`, Mono, Built-in Render Pipeline, D3D11) 및 C++/HLSL 렌더링 최적화 엔지니어다.
현재 `E:\StutterFixWork\wt-gpu`에서 GPU Compute로 장식 23,547~28,523개의 정점을 계산하고 있으나, **가시 `MeshRenderer` 약 1,880개가 프레임당 약 2,500회의 엔진 Draw 호출을 일으켜 무거운 구간(`260~270초`) FPS가 `168 FPS`에 묶여 있다**(장식 그리기를 통째로 빼면 `168 → 261 FPS`).

아래의 **실패 원인 분석**과 **2단계 아키텍처 설계**를 숙지하고, **제약 조건을 100% 준수하면서 Draw 호출을 1/10 ~ 1/50 이하로 줄이는 구현과 검증**을 수행하라.

---

## 1. 절대 위반 금지 제약 조건

1. **페인터 알고리즘(그리기 순서) 100% 보존**:
   - Unity Built-in RP의 투명 정렬 규칙(`sortingLayerID` → `sortingOrder` → `renderQueue` → `cameraDistance` → `materialInstanceID` 오름차순)이 바뀌면 안 된다.
   - 소유 장식들 사이에 게임의 비소유 렌더러(타일, 행성, 마스크, 필터, 비소유 장식 등)가 섞여 있으므로, **실제로 화면에 보이는 비소유 렌더러(정렬 장벽, Barrier)와의 앞뒤 순서**가 절대 깨지면 안 된다.
2. **화면 픽셀 동일성**:
   - 알파 블렌딩이므로 OIT(순서 무관 투명도)나 `ZWrite On` 알파 컷아웃으로 바꾸지 마라.
   - 원본 대비 픽셀 최대 차이 `maxdiff <= 3/255`(목표 `0`)를 유지하라.
3. **런타임 Harmony 패치 + Unity 6000.3.10f1 번들만 사용**:
   - 엔진 소스 수정이나 URP 전환 불가.
   - `Dev` / `Player` / `PlayerAuto` 3종 빌드와 `LoadCheck`(`FieldRefAccess` 개수 유지, 일반 `Player`에 연구용 타입 유출 0)를 반드시 통과하라.

---

## 2. 왜 이전 "정렬 키별 69개 메시 병합"이 서브메시 1,478개 / Draw 3,815회(`166 → 152 FPS` 하락)로 실패했는가?

같은 실수를 절대 반복하지 않도록 아래 3가지 원인을 먼저 확인하라:

1. **원인 1 (`materialInstanceID` 정렬 키 인터리빙으로 인한 텍스처 지그재그 파편화)**:
   - 얼불춤은 장식마다 `spriteRenderer.material`에 접근해 **개별 `Material` 인스턴스(28,829개)**를 만든다.
   - 정렬 키의 마지막 기준이 `materialInstanceID` 오름차순이다 보니, 동일한 `(sortingLayerID, sortingOrder, Z)` 안에서도 `텍스처 A(matID=1001)` → `텍스처 B(matID=1002)` → `텍스처 A(matID=1005)`처럼 텍스처가 계속 번갈아 나타난다.
   - 이를 단일 텍스처 재질 기반 SubMesh로 묶으면 텍스처가 바뀔 때마다 SubMesh가 쪼개져 **1,478개의 SubMesh**가 생성된다.
2. **원인 2 (Unity Built-in RP의 다중 SubMesh + `sharedMaterials[]` 배열 오버헤드)**:
   - Unity Built-in RP에서 하나의 `MeshRenderer`에 수십~수백 개의 SubMesh와 `sharedMaterials[]` 배열을 할당하면, 엔진 내부 `RenderNodeQueue`가 서브메시마다 별도의 렌더 노드를 만들고 동일 정점 버퍼에 대한 재질 상태 전환을 직렬화하여 **단일 SubMesh 렌더러보다 CPU/드라이버 제출 비용이 오히려 더 비싸진다.**
   - **철칙**: 병합된 대표 `MeshRenderer`는 **항상 `subMeshCount == 1`(단일 SubMesh, 단일 공유 `Material`)** 이어야 한다!
3. **원인 3 (`CameraEvent.BeforeForwardAlpha`의 한계와 Z-버퍼 충돌)**:
   - `CameraEvent.BeforeForwardAlpha` / `AfterForwardAlpha`는 투명 큐(`2501..5000`) 전체의 맨 앞이나 맨 뒤에만 끼어들 수 있어, 중간에 낀 타일/행성/비소유 장식과의 정렬 순서를 맞출 수 없다.
   - 또한 반투명(`0 < alpha < 1`) 스프라이트에 `ZWrite On` + `ZTest LEqual`을 켜면 앞쪽 반투명 스프라이트가 뒤쪽 스프라이트를 잘라내어 화면이 깨진다. 따라서 **반드시 `ZWrite Off`를 유지한 채, Built-in RP의 네이티브 정렬 큐 안에 "장벽 구간별 단일-SubMesh 대표(Proxy) `MeshRenderer`"를 배치하고, 구간 내부의 페인터 순서는 정점/인덱스 버퍼의 삼각형 나열 순서(D3D11 In-Order Rasterization)로 보존**해야 한다.

---

## 3. 구현할 아키텍처: "가시 장벽(Active Barrier) 구간 분할 + `Texture2DArray` 단일-SubMesh 대표 렌더러"

단계별로 검증하기 위해 **Step A (셰이더 변경 없는 무장벽 동일-텍스처 단일-SubMesh 병합)** 와 **Step B (`Texture2DArray` 기반 장벽 구간당 단일 Draw 통합)** 순서로 진행하라.

### Step A. 비소유 가시 장벽(Barrier) 필터링 + 단일-SubMesh(`subMeshCount = 1`) 대표 렌더러 병합

1. **유령 장벽(Ghost Barrier) 제거**:
   - 전체 렌더러를 페인터 순서로 정렬했을 때, 사이에 끼어 있는 비소유(`!gpuOwned`) 렌더러 중 **`forceRenderingOff == true`이거나 `!enabled`이거나 `color.a * opacity == 0`인 투명 비소유 렌더러는 화면에 아무 픽셀도 그리지 않으므로 정렬 장벽(Barrier)이 아니다!**
   - 실제로 화면에 픽셀을 그리는 **가시 비소유 렌더러(타일, 행성, 가시 비소유 장식, SpriteMask)** 만 진짜 장벽(Barrier)으로 취급하여 소유 장식 구간(Span)을 나눠라.
2. **동일 `(sortingLayerID, sortingOrder, Z)` 내 텍스처별 재그룹화 여부 판정**:
   - 동일한 `(sortingLayerID, sortingOrder, Z)` 버킷 안의 소유 장식들(예: 한 타일에 묶인 `nevRay` 56개 세트, `sortingOrder = 775`의 `nesFires` 3,231개, `sortingOrder = 777`의 `nesFires2` 1,731개)은 이미 **같은 텍스처**를 공유하거나 화면상에서 서로 다른 텍스처와 겹치지 않는다.
   - 특히 같은 텍스처(`TextureId`)와 같은 재질 서명(`_PointSampling`, `blendMode`)을 쓰는 연속 구간(또는 동일 `(Layer, Order, Z)` 버킷 내 동일 `TextureId` 그룹)은 **`subMeshCount = 1`인 단일 `MeshRenderer` + 단일 공유 `Material`** 로 묶어라(절대 하나의 `Mesh`에 수십 개 `subMeshCount`를 만들지 마라).

### Step B. `Texture2DArray` + 장벽 구간(Barrier Span)당 단 1개의 Draw로 완전 통합 (핵심 목표)

서로 다른 `sortingOrder`와 서로 다른 텍스처(수십 종)를 가진 소유 장식들이 연속으로 이어질 때, **가시 비소유 장벽(Barrier)과 만날 때만 끊어서 구간(Span) 전체를 단 1개의 `MeshRenderer`(`subMeshCount = 1`, Draw 1회)로 그리는 구조**를 구현하라.

#### B-1. 런타임 무손실 `Texture2DArray` 구축 (`Graphics.Blit` + `Graphics.CopyTexture`)
- 이미 로드된 스프라이트 텍스처(수십 종, `Arche` 기준 활성 약 100여 종 이하)는 해상도나 포맷(`DXT5`, `RGBA32`)이 다르고 `isReadable == false`일 수 있다.
- 맵 준비 시점에 소유 장식들이 참조하는 고유 `Texture2D` 목록을 수집하여 해상도 티어별(또는 최대 해상도 `2048×2048` 단일/2티어) `Texture2DArray`(`TextureFormat.RGBA32`, `mipChain: false`, `linear: false`)를 생성하라:
  1. 각 원본 `Texture2D`(`w × h`)를 임시 `RenderTexture`(`w × h`, `RenderTextureFormat.ARGB32`, `sRGB`)에 `Graphics.Blit`으로 1:1 복사한다(가장자리 밖 바이리니어 블리딩 방지를 위해 우측/상단 1px 클램프 여백 포함 가능).
  2. `Graphics.CopyTexture(tempRt, 0, 0, 0, 0, w, h, texArray, sliceIndex, 0, 0, 0)`를 호출해 `Texture2DArray`의 `sliceIndex` 슬라이스 좌하단 `(0, 0, w, h)`에 **리샘플링 없이 비트 그대로** 복사한다.
  3. 각 스프라이트의 정규화 UV `(u, v)`를 슬라이스 내 실제 영역 `(u * (w / (float)arrayW), v * (h / (float)arrayH))`로 변환하고, 정점의 추가 속성(예: `TEXCOORD0.z = sliceIndex`, `TEXCOORD0.w = flags (pointSampling | flip)`)에 슬라이스 번호와 샘플러 플래그를 기록한다.

#### B-2. `Sprites/Tile` 호환 `Texture2DArray` 셰이더 (`GpuDecoBatch.shader`)
- `tools/gpudeco-lab` 번들에 Built-in RP 투명 스프라이트 호환 셰이더 1개를 추가하라:
  - `Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }`
  - `Cull Off`, `Lighting Off`, `ZWrite Off`, `Blend One OneMinusSrcAlpha` (원본 `Sprites/Tile`의 프리멀티플라이드/표준 블렌드 모드와 100% 일치시킬 것)
  - `UNITY_DECLARE_TEX2DARRAY(_MainTexArr);`와 함께 `SamplerState sampler_point_clamp;`, `SamplerState sampler_linear_clamp;`를 선언하고, 정점 플래그(`pointSampling`)에 따라 `_MainTexArr.Sample(sampler_point_clamp, float3(uv, slice))`와 `_MainTexArr.Sample(sampler_linear_clamp, float3(uv, slice))`를 분기하라.
  - 정점 색상은 원본 `SpriteRenderer`의 정점 컬러 배칭과 100% 동일하게 Compute Shader에서 half-up `UNorm8` 양자화(`floor(saturate(c) * 255.0 + 0.5) / 255.0`)를 거친 값을 입력받아 곱한다.

#### B-3. 장벽 구간(Barrier Span)별 대표 `MeshRenderer` + In-Order 삼각형 배치
1. **장벽 구간(Span) 생성**:
   - 전체 렌더러의 페인터 정렬 순서에서, 가시 비소유 장벽(Barrier $B_m$)과 다음 가시 비소유 장벽($B_{m+1}$) 사이에 놓인 모든 소유 장식 $[i_{\text{start}} \dots i_{\text{end}}]$를 하나의 **Span $m$**으로 묶는다.
   - Span $m$ 안의 소유 장식들은 **서로 다른 `sortingOrder`와 서로 다른 텍스처를 가지고 있어도 분리하지 않는다!**
   - Span $m$을 그리는 대표 `MeshRenderer` 1개에는 $B_m$ 바로 뒤, $B_{m+1}$ 바로 앞에 정렬되도록 구간 첫 장식의 `(sortingLayerID, sortingOrder, 월드 Z)`를 부여한다.
2. **Span 내부의 페인터 정렬 보장**:
   - Span $m$의 정점 버퍼(`Raw` UAV)와 고정 쿼드 인덱스 버퍼(`0,1,2, 2,3,0, 4,5,6, ...`)에서, 각 장식 쿼드의 슬롯 순서 $0, 1, \dots, K_m - 1$을 **원래의 `(sortingLayerID, sortingOrder, renderQueue, distance, materialInstanceID, rendererOrder)` 오름차순 그대로** 배치한다.
   - D3D11 파이프라인은 단일 `DrawIndexed` 안에서 인덱스 버퍼 순서대로 삼각형을 래스터라이즈하고 블렌딩하므로, Span $m$ 내부의 `sortingOrder`가 달라도 원본 1,880개 개별 렌더러와 **픽셀 단위로 100% 동일한 순서**로 합성된다.
3. **꺼진(비가시) 장식의 오버드로우 및 정점 복사 제거**:
   - **방법 1 (Compute 퇴화 삼각형 — 가장 단순하고 빠름)**: `Evaluate` 커널(또는 Span 정점 기록 커널)에서 현재 프레임에 꺼진 장식(`alpha == 0` 또는 `forceRenderingOff`)의 정점 4개 위치를 모두 `(0, 0, 0)`(면적 0 퇴화 삼각형)으로 출력한다. GPU 래스터라이저는 면적 0 삼각형을 정점 단계 직후 1클럭에 버리므로 픽셀 셰이더 실행과 오버드로우는 **0**이다.
   - **방법 2 (Span 단위 단일 정점 버퍼 직접 기록 — 복사 Dispatch 자체 제거!)**:
     - 기존에는 개별 `Mesh`가 23,547개여서 D3D11 UAV 8개 제한 때문에 2,944번 복사 Dispatch를 해야 했다.
     - 이제 Span 대표 `Mesh`가 **전체 수십 개(또는 슬라이스당 10~30개)** 로 줄어들므로, **`Evaluate` 커널이 아예 Span 대표 `Mesh`의 Raw 정점 버퍼(`Mesh.GetNativeVertexBufferPtr(0)` 또는 `GraphicsBuffer`)에 직접 정점을 쓰거나, Span당 단 1회의 선형 복사(`CopySubresourceRegion` 또는 1회 `CopyRun`)** 만 수행하면 된다! 즉 2,944회의 8-UAV 복사 Dispatch도 동시에 사라진다.

---

## 4. 실행 및 검증 순서 (단계별 게이트)

1. **진단 1 (`Barrier Span` 개수 실측 로그)**:
   - `Arche`의 `260~270초` 구간에서 현재 가시 소유 장식(~1,880개) 사이에 끼어 있는 **"실제 가시 비소유 렌더러(Active Barrier)"**가 몇 번 정렬 순서를 끊는지 세어 로그로 출력하라(`total_owned_visible`, `active_barrier_spans`, `same_tex_runs`).
2. **구현 및 픽셀 동일성 검증 (`G1` / `256s` / `262s` / `270s` / `276s`)**:
   - 정지 프레임에서 원본 `SpriteRenderer` 대비 `maxdiff <= 3/255`(목표 `0`), `different == 0%`를 확인하라.
   - 특히 `Texture2DArray` 슬라이스 복사 시 `sRGB` 감마 변환이나 바이리니어 경계 블리딩으로 인한 오차가 `0`인지 격리 픽셀 프로브(`ImagePixelProbe`)로 먼저 검증하라.
3. **성능 대조 (`260~270초` 무거운 구간 실측)**:
   - 캡처 없는(no-capture) 실측 조건에서 다음 지표를 비교 보고하라:
     - 프레임당 엔진 Draw 호출 수 (`기존 ~2,500회` → `목표 30~150회 이하`)
     - 프레임당 D3D11 정점 복사 횟수 (`기존 수백~2,944회` → `Span 수만큼 축소 또는 직접 기록`)
     - `260~270초` 진짜 평균 FPS (`기존 168 FPS` → `목표 250~400+ FPS`) 및 30ms 초과 스파이크 `0회`
   - 3빌드(`Dev`, `Player`, `PlayerAuto`) + `LoadCheck` 통과 및 종료 후 설치 원본 `9/9` 복원을 확인하라.
