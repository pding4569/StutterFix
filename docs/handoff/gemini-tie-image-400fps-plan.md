# StutterFix 400 FPS 달성 설계서: 동점 정렬 묶음 픽셀 오류 원인·수정 및 이미지 장식 4,976개 GPU/DrawCollapse 통합

## 1. 현황 및 400 FPS(2.50ms)까지 남은 비용 분해 (측정 근거)

맵 `"Arche"` 260~270초 구간(`3440x1440@165`, 소유 장식 `23,547`개, 가시 구간 통합 `CollapseSpan` ON 기준)에서 확보된 측정치와 남은 격차는 다음과 같다.

| 상태 / 항목 | 프레임 시간 (ms) | FPS | 엔진 Draw 수 | 픽셀 오차 (495만 픽셀 중) | 비고 |
|---|---:|---:|---:|---|---|
| 정지 장식 맵 (이동 이벤트 제거) | 2.00 | ~500 | - | 기준선 | 목표 상한선 |
| GPU Live 단독 (독립 렌더러 1,880개) | 6.06 | ~165 | ~2,500 | 0 | 렌더 노드 준비 병목 (+1.4ms) |
| DrawCollapse 안전 모드 (`KeepBeforeBarrier=false`) | 4.94 | ~202 (+22.7%) | 767 | 110~207 px (maxdiff 1) | 동점 제외 `506`개 폴백 복사 `0.23ms` 잔존 |
| DrawCollapse `KeepBeforeBarrier=true` (현재 코드) | 4.81 | ~208 (+23.0%) | ~630 | 283,838~430,103 px (maxdiff 3~7) | 폴백 `506 → 14` 감소, **픽셀 실패** |

`docs/perfview_현재상태_이름붙음.txt` (메인 스레드 3,310 샘플 / 4,000ms 창) 기준 현재 안전 모드(`4.94ms`)의 남은 두 덩어리는 다음과 같다:

1. **덩어리 1 — 동점 정렬 묶음 폴백(`DRAWCOLLAPSE_TIE_EXCLUDED = 506`개)**:
   - `DecoBake.DispatchDrawCollapse → GpuDecoCopy.Execute → Graphics.ExecuteCommandBuffer`가 메인 스레드의 **5.6% (약 0.23ms/프레임)** 와 추가 엔진 draw 약 **150회**를 소비한다(`src/GpuDecoDrawCollapseRenderer.cs` 538~547행).
   - 텍스처 오버플로는 `0`개이므로, 동점 묶음 제외만 해결하면 `gpuCollapseTextureFallbackCount == 0`이 되어 **독립 경로의 `DispatchEvaluate()` + `ExecuteCommandBuffer` 복사를 통째로 0회로 건너뛸 수 있다.**
2. **덩어리 2 — 비소유 이미지 장식 약 5,000개(`nesFires` 3,231 + `nesFires2` 1,731 등)의 CPU 이동·관리 비용**:
   - `TightLate.List` (**7.3%, ~0.30ms**) + `TightLate.Process/Apply` (**7.1%, ~0.29ms**) + `DecoAnim.UpdatePrefix/StepPair/scrDecoration.SetPosition` (**5.3%, ~0.22ms**) + `FastMove.Prefix` (**3.8%, ~0.16ms**) = **합계 약 0.97~1.05ms/프레임**, 그리고 워커 스레드의 `TransformChangeDispatch::ScanAndHandleChangesJob` (316 샘플) 및 `SpriteRendererTransformChangedJob` (69 샘플).
   - 이 4,976개 이미지 장식을 GPU 소유(`23,547 → 28,523`) 및 `DrawCollapse` 구간에 편입하면 `Dormancy.EvictGpuOwnedFromAwake()`(`src/Dormancy.cs` 82~95행)에 의해 `awake` 목록에서도 자동 퇴거되어 위 ~1.0ms와 나머지 장벽/드로우가 한꺼번에 사라진다.

## 2. 질문 1: `DrawCollapsePolicy.KeepBeforeBarrier`가 42만 픽셀을 틀리게 만든 정확한 원인 (줄 단위 코드 대조)

`src/DrawCollapsePolicy.cs`의 `Plan`(76~98행)과 `src/GpuDecoDrawCollapseRenderer.cs`의 `BuildDrawCollapseSpans`(230~360행)를 대조한 결과, `KeepBeforeBarrier = true`가 폴백을 `506 → 14`로 줄이면서 260초에 즉시 `428,697` 픽셀(maxdiff 4~7)을 틀리게 만든 원인은 **단일 버그가 아니라 아래 3가지 메커니즘의 연쇄 작용**이다.

### 2.1 결정적 원인 1 (가설 c + 공유 재질 동점 역전): 같은 정렬 묶음 안의 '비활성 장벽'이 492개 유지 장식을 여러 구간(`CollapseSpan`)으로 쪼개고, 모든 구간에 동일한 공유 재질(`gpuCollapseSharedMats`)을 부여해 그리기 순서가 역전됨

- **(a) 원인 / 메커니즘**:
  1. `src/GpuDecoDrawCollapseRenderer.cs` 258~265행에서 `activeBucketMinMat`은 **그 순간 활성인 장벽(`DrawCollapseBarrierActive(b) == true`)만** 스캔하여 버킷별 최소 재질 ID(`minBarrierMat`, 예: `-832,720`)를 기록한다:
     ```csharp
     // src/GpuDecoDrawCollapseRenderer.cs 258~265행
     foreach(var b in gpuCollapseBarriers)
     {
         if(!DrawCollapseBarrierActive(b))continue;
         var bm=DrawCollapseMaterial(b);if(!bm)continue;
         var bk=MatShare.SortKey(b.R,bm,gpuLiveCamera,b.Key.Slot);
         var bkey=(bk.Layer,bk.Order,bk.Queue,bk.Distance);
         if(!activeBucketMinMat.TryGetValue(bkey,out int curMin) || bk.Material<curMin)activeBucketMinMat[bkey]=bk.Material;
     }
     ```
  2. `KeepBeforeBarrier = false`(안전 모드)일 때는 활성 장벽이 존재하는 묶음 `B = (Layer, Order, Queue, Distance)`의 소유 장식 506개가 **전부** `gpuCollapseIncluded[i] = false`로 제외된다(274행). 따라서 그 묶음 `B`에는 `Owned = true` 행이 **0개**가 되어 `CollapseSpan`이 아예 만들어지지 않고, 506개 모두 원래 재질 ID(`-2,706,580 ~ -598,272`)를 가진 독립 렌더러(`source.Renderers[i]`)로 그려진다.
  3. 반면 `KeepBeforeBarrier = true`(273행)를 켜면, 묶음 `B` 안에서 `ok.Material < minBarrierMat`(`-2,706,580 ~ -832,722`)인 **492개 소유 장식이 `gpuCollapseIncluded[i] = true`로 남는다.**
  4. **문제는 그 직후인 287~293행에서 발생한다**:
     ```csharp
     // src/GpuDecoDrawCollapseRenderer.cs 287~293행
     foreach(var row in gpuCollapseBarriers)
     {
         var material=DrawCollapseMaterial(row);
         RequireLive(row.R && material,"drawcollapse replan barrier lost");
         row.M=material;row.Key=MatShare.SortKey(row.R,material,gpuLiveCamera,row.Key.Slot);
         rows.Add(new DrawCollapsePolicy.Row(row.Key,false,row.R.GetInstanceID()));
     }
     ```
     287행은 `DrawCollapseBarrierActive(b)` 여부에 상관없이 **`gpuCollapseBarriers`에 들어 있는 비활성 장벽 6만여 개를 전부 `Owned = false`로 `rows`에 넣는다!**
     묶음 `B`(안개 장벽 `nesFires`/`nesFires2` 등이 속한 묶음) 안에는 재질 ID가 `-2,706,580 ~ -832,722` 사이사이에 끼어 있는 **비활성 장벽들(`color.a == 0` 또는 `!enabled`)** 이 다수 존재한다.
  5. 이어서 `DrawCollapsePolicy.Plan(rows)`(`src/DrawCollapsePolicy.cs` 84~96행)가 호출되면, `Plan`은 장벽의 활성 여부를 모른 채 `row.Owned == false`(비활성 장벽)를 만날 때마다 무조건 구간을 끊는다(93행 `if(items.Count>0){spans.Add(items.ToArray());items.Clear();}`).
     그 결과 **같은 `(Layer, Order, Queue, Distance)` 묶음 `B` 안의 492개 소유 장식이 단일 구간이 아니라 여러 개의 하위 구간 $S_1, S_2, \dots, S_k$로 쪼개진다!**
  6. 이제 324~358행에서 $S_1, S_2, \dots, S_k$의 대표 렌더러와 재질을 만들 때:
     ```csharp
     // src/GpuDecoDrawCollapseRenderer.cs 324~333행
     string matKey=first.M.renderQueue+"/"+blend[0]+"/"+blend[1]+"/"+blend[2];
     if(!gpuCollapseSharedMats.TryGetValue(matKey,out var shared) || !shared) { ... }
     s.Material=shared;s.SharedMat=true;
     ```
     **같은 묶음 `B` 안의 모든 하위 구간 $S_1, S_2, \dots, S_k$가 단 하나의 공유 재질 `shared`(ID `-3,442,812`)를 똑같이 부여받는다!**
  7. 결과적으로 $S_1, S_2, \dots, S_k$는 `(Layer, Order, Queue, Distance, Material)` 5개 정렬 키가 **100% 동점(`SameMaterialTie`)** 이 된다.
     Unity 런타임에서 `new GameObject` / `AddComponent<MeshRenderer>()`(351~353행)로 생성되는 객체의 InstanceID는 **생성 순서대로 더 작은 음수**(`S_1.Renderer = -3,442,820 > S_2.Renderer = -3,442,830 > ... > S_k.Renderer = -3,442,900`)를 받는다.
     따라서 `SceneRenderOrder.Key.CompareTo`(`src/SceneRenderOrder.cs` 28행 `Renderer.CompareTo(b.Renderer)`) 및 Unity 투명 정렬에서 **나중에 생성된 $S_k$가 $S_1$보다 먼저 그려져, 같은 묶음 내 반투명 장식 492개의 페인터 순서가 구간 단위로 뒤집힌다!**
  8. 더구나 `ValidateDrawCollapse`(470~494행)는 `barrier` 대 `span`(`key.CompareTo(proxy)`)만 비교할 뿐, **같은 묶음 내 `span` 대 `span`의 동점/역전은 전혀 검사하지 않으므로** 가드 실패(폴백)는 0건인데 화면 픽셀 42만 개만 틀어지게 된다.
     *(참고: 안전 모드에서도 활성 장벽이 없는 묶음 안에 비활성 장벽이 끼어 구간이 2개로 쪼개지면 동일한 공유 재질 동점이 발생하며, 이것이 안전 모드의 잔여 110~207픽셀 오차(maxdiff 1)의 원인이기도 하다.)*

### 2.2 결정적 원인 2 (가설 c + 동적 장벽 기상): 묶음 내 비활성 장벽이 260~270초 재생 중 깨어날 때(`color.a > 0`), 그 장벽 뒤에 있어야 할 구간이 대표 재질 ID(`-3,442,812`) 때문에 장벽 앞으로 점프함

- **(a) 원인 / 메커니즘**:
  1. `RebuildDrawCollapse()`(260초) 시점에는 알파가 `0`이어서 `DrawCollapseBarrierActive(b) == false`였던 장벽 $b_{\text{wake}}$(예: 재질 ID `-1,500,000`)는 260행에서 무시되어 `minBarrierMat`이 `-832,720`(당시 활성 장벽)으로 잡힌다.
  2. 따라서 원래 페인터 순서상 **$b_{\text{wake}}(-1,500,000)$보다 뒤에 그려져야 하는** 소유 장식들(재질 ID `-1,499,998 ~ -832,722`)이 `KeepBeforeBarrier`에 의해 구간 $S_2$로 편입된다.
  3. 그러나 구간 $S_2$의 대표 재질 ID는 `-3,442,812`로 모든 기존 재질보다 작기 때문에, **같은 `(Layer, Order, Queue, Distance)` 묶음 안에서는 어떤 `CollapseSpan`도 장벽 $b_{\text{wake}}(-1,500,000)$ 뒤에 위치하는 것이 수학적으로 불가능**하다(`-3,442,812 < -1,500,000`).
  4. 재생 중 $b_{\text{wake}}$의 알파가 양수가 되는 순간, 원래 순서(`S_1 → b_wake → S_2`)가 아니라 **`S_1, S_2(-3,442,812) → b_wake(-1,500,000)`** 순서로 그려져 $S_2$와 $b_{\text{wake}}$의 앞뒤가 역전된다.
  5. 재생 중 `ValidateDrawCollapse(false)`는 장벽 6만 3천 개를 프레임당 `CollapseBarrierSweepPerFrame = 32`개씩만 순환 점검(35행, 전수 순회에 약 1,970프레임 = 10초 이상 소요)하므로, 260~270초 사이에 깨어난 $b_{\text{wake}}$를 즉시 감지하지 못한다.

### 2.3 결정적 원인 3 (가설 a, b, d 대조): `PrepareDrawCollapse` → `RebuildDrawCollapse` 간 상태 오염 및 `SplitByOrder`의 `Distance` 누락

사용자께서 제시하신 가설 (a), (b), (d)를 코드와 대조한 결과는 다음과 같다:

- **가설 (a) & (b) 검증 — `PrepareDrawCollapse`(0초)에서 변경된 `gpuCollapseIncluded` / `gpuCollapseBarriers`가 `RebuildDrawCollapse`(260초)에서 초기화되지 않는 상태 오염 + 독립 렌더러 키 차이**:
  - `PrepareDrawCollapse()`(213행)는 곡 시작 전(0초)에 `BuildDrawCollapseSpans()`를 1회 호출한다. 이때 274~281행이 실행되면:
    ```csharp
    gpuCollapseIncluded[i]=false;
    if(!gpuCollapseTextureFallback[i]){gpuCollapseTextureFallback[i]=true;gpuCollapseTextureFallbackCount++;}
    var ir=source.Renderers[i];
    if(ir)newBarriers.Add(new GpuLiveRow{R=ir,M=ir.sharedMaterial,Item=-1,AuxiliaryOwner=-1,Key=MatShare.SortKey(ir,ir.sharedMaterial,gpuLiveCamera)});
    ...
    gpuCollapseBarriers.AddRange(newBarriers);
    ```
  - 이후 260초에 `drawcollapse ready`로 `RebuildDrawCollapse()`(361~381행)를 부르면, `gpuCollapseSpans`만 비울 뿐 **`gpuCollapseIncluded`, `gpuCollapseTextureFallback`, `gpuCollapseBarriers`에 추가된 `newBarriers`(`source.Renderers[i]`)를 원래 상태로 되돌리지 않는다!**
  - 게다가 `RebuildDrawCollapse()`가 실행될 때는 `gpuLiveActive == true && gpuCollapseOn == false` 상태이므로, 0초에 `newBarriers`로 들어갔던 `source.Renderers[i]`(`MeshRenderer`)는 260초에 `enabled == true`이다. 그 결과 260초의 `BuildDrawCollapseSpans()` 260행에서 **우리 모드가 만든 독립 렌더러 `source.Renderers[i]`가 `DrawCollapseBarrierActive == true`인 '활성 장벽'으로 오인**되어 `activeBucketMinMat`을 오염시킨다!
  - 또한 가설 (b)대로 271행은 `orow.R`(`SpriteRenderer`)의 현재 `bounds.center`로 키를 구하는 반면, 277행은 `ir = source.Renderers[i]`(0초 `BuildLiveRun` 시점의 `first.R.bounds.center`로 구워진 `MeshRenderer`)의 키를 구한다. 만약 GPU 활성화(256초) 이전에 CPU에서 위치/깊이가 바뀐 장식이 있다면 `orow.R`과 `ir`의 `bounds.center`가 달라질 수 있다.
- **가설 (d) 검증 — `DrawCollapsePolicy.Plan`의 `SplitByOrder`가 `Distance`를 검사하지 않음**:
  - `src/DrawCollapsePolicy.cs` 89행:
    ```csharp
    if(SplitByOrder && haveBucket && items.Count>0 && (row.Key.Layer!=bl || row.Key.Order!=bo || row.Key.Queue!=bq)){spans.Add(items.ToArray());items.Clear();}
    ```
  - `(Layer, Order, Queue)`만 같으면 `row.Key.Distance`(`z` 깊이)가 달라도 한 구간(`CollapseSpan`)으로 합쳐지며, 대표 렌더러는 `first.R`의 `Distance`(345행) 하나만 사용한다. 같은 `(Layer, Order, Queue)` 안에서 `Distance`가 다른 소유 장식과 장벽이 섞여 있으면 대표 렌더러의 `Distance`와 개별 장식의 `Distance`가 어긋난다.

### 2.4 질문 1의 안전한 수정안 (파일·함수·줄 수준)

- **(b) 깨질 수 있는 시나리오**:
  - 260~270초 창 안에서 비활성 장벽(`color.a == 0`)이 깨어나 같은 버킷의 유지 장식보다 앞에 그려져야 하는 경우, 또는 한 버킷이 비활성 장벽 때문에 여러 구간으로 쪼개져 동일 재질 ID 동점이 되는 경우, 또는 `drawcollapse ready`를 여러 번 호출할 때 이전 호출의 `newBarriers`가 누적되는 경우.
- **(c) 코드 수정 지점**:
  1. **`src/DrawCollapsePolicy.cs` 83~91행 (`Plan`)**:
     - `SplitByOrder`가 `(Layer, Order, Queue)`뿐 아니라 **`Distance`까지(`!row.Key.SameBucket(prevKey)`) 바뀔 때마다 구간을 끊도록** 수정한다.
     - 또한 **같은 `(Layer, Order, Queue, Distance)` 버킷 안에서는** 대표 재질 ID가 모든 장벽보다 작으므로 장벽 뒤에 오는 구간을 만들 수 없다. 따라서 같은 버킷 안에서 이미 `KeepBeforeBarrier` 필터를 통과해 살아남은 소유 장식들 사이에 비활성 장벽(`!row.Owned`)이 끼어 있더라도 **같은 버킷 내부에서는 구간을 쪼개지 않고 단일 구간으로 묶거나**, 만약 같은 버킷에 복수 구간이 생기면 아래 3번처럼 구간별로 오름차순 재질 ID를 부여한다.
  2. **`src/GpuDecoDrawCollapseRenderer.cs` 99~124행 & 230~293행 (`PrepareDrawCollapse` & `BuildDrawCollapseSpans`)**:
     - `PrepareDrawCollapse`에서 초기 `gpuCollapseWindowIncluded`(`bool[]`), `gpuCollapseBaseTexFallback`(`bool[]`), `gpuCollapseBaseBarrierCount`(`int`)를 별도 필드에 저장하고, `BuildDrawCollapseSpans()` 시작 시점에 이 3가지를 항상 초기 상태로 복원한 뒤 `newBarriers`를 계산한다(재계획 시 상태 오염 방지).
     - **핵심**: 258~265행에서 `activeBucketMinMat`을 구할 때, 현재 프레임에 `DrawCollapseBarrierActive(b)`인 장벽뿐 아니라 **260~270초 창 안에서 활성화될 수 있는 후보 장벽**(비소유 `scrVisualDecoration` 중 `VisibleInWindow`이거나 `enabled && gameObject.activeInHierarchy`인 장벽)의 최소 재질 ID까지 포함해 `minBarrierMat`을 구한다. 그리고 287~293행에서 `rows`에 장벽을 넣을 때, **같은 버킷 안에서 `minBarrierMat`보다 앞에 있는(즉 260~270초 동안 절대 켜지지 않는) 비활성 장벽은 `rows`에 넣지 않는다!**
       이렇게 하면 같은 버킷 안의 유지 장식(`ok.Material < minBarrierMat`)은 비활성 장벽에 의해 쪼개지지 않고 **단 하나의 `CollapseSpan`** 으로 만들어져, 공유 재질 동점 역전과 비활성 장벽 기상 문제가 동시에 사라진다!
  3. **`src/GpuDecoDrawCollapseRenderer.cs` 324~358행 (동일 버킷 다중 구간 방어)**:
     - 설령 어떤 이유로든 같은 `(Layer, Order, Queue, Distance)` 버킷에 2개 이상의 `CollapseSpan`이 생성될 경우, `MatShare.cs` 169행(`MatSharePolicy.MaterialOrder`)과 동일하게 해당 버킷의 구간 수만큼 `Material`을 복제한 뒤 **`Material.GetInstanceID()` 오름차순으로 구간 0, 1, 2...에 배정**하여 같은 버킷 내 구간끼리 절대 같은 재질 ID를 공유하지 않게 보장한다.
- **(d) 가장 싼 검증 방법**:
  - `BuildDrawCollapseSpans()` 끝에 로그 한 줄을 추가해 `same_bucket_multi_spans` 개수(같은 `(Layer, Order, Queue, Distance)`를 갖는 `CollapseSpan` 쌍의 수)를 찍는다.
  - 수정 전에는 `KeepBeforeBarrier = true`일 때 `same_bucket_multi_spans > 0`이 찍히며, 위 수정 후 `same_bucket_multi_spans == 0`이 되는 즉시 260/262/270초 픽셀 오차가 `428,697 → 110~207 (maxdiff 1)`(또는 0)으로 복원되는지 확인한다.

## 3. 질문 2: 이미지 장식 약 5,000개(`4,976`개)의 GPU 소유 및 DrawCollapse 편입

### 3.1 질문 2(a): `images own on`에서 남은 픽셀 차이(42~121픽셀)의 원인 후보 3가지 (가능성 순) 및 가장 싼 진단법

`docs/claude-gpudeco-image-playback-20261024.md`(round 102~110)와 `src/GpuDecoImagePlayback.cs`, `src/GpuDecoImageGeometryFixture.cs`를 대조한 가능성 순 원인 3가지는 다음과 같다.

#### 가능성 1위: 이미지 교체/Flush 시 `d.UpdatePosition()` 미호출로 인해 `LiveDecoGeometry.Capture`가 고정된(stale) CPU Transform(`d.pivotTrans` / `d.childTransform`)을 읽는 문제

- **(a) 원인 / 메커니즘**:
  - `src/GpuDecoImagePlayback.cs` 136~140행(`RunGpuImageEffect`) 및 168~170행(`FlushGpuImagesBeforeVisible`)을 보면:
    ```csharp
    SyncGpuImageFields(master,d);
    stage=1;FastMove.RunGpuImageBlock(fx,d);gpuImageSetters++;stage=2;
    if(!gpuLiveActive)return;
    if(LazySprite.GpuPeekPending(d,out _))gpuImagePending.Add(master);
    else {gpuImagePending.Remove(master);RefreshGpuImageGeometry(master,d);}
    ```
  - `SyncGpuImageFields(master, d)`(70~79행)는 GPU 마스터 채널 16개의 논리 값(`pivotPosVec`, `rotAngle`, `scaleVec` 등)만 `d`의 필드에 복사하고, **비용을 아끼기 위해 `d.UpdatePosition()`은 호출하지 않는다**(따라서 `d.pivotTrans`와 `d.childTransform`의 실제 `UnityEngine.Transform`은 GPU 활성화 시점의 옛날 위치·회전·스케일에 멈춰 있다).
  - 이어서 `FastMove.RunGpuImageBlock` → `d.SetSprite(cs, false)`(또는 `LazySprite.Flush` → `d.SetSprite`)가 실행된 직후 `RefreshGpuImageGeometry(master, d)`(80~115행)가 호출된다.
  - 그런데 `RefreshGpuImageGeometry` 86행 → `ImageFixtureItem`(`src/GpuDecoImageGeometryFixture.cs` 14행)을 보면:
    ```csharp
    g=LiveDecoGeometry.Capture(d.pivotTrans,d.childTransform,r.transform,v.Vertices,r.flipX,r.flipY);
    ```
    **`LiveDecoGeometry.Capture`가 `d.pivotTrans`, `d.childTransform`, `r.transform`의 현재 Transform 행렬/속성을 직접 읽는다!**
  - 만약 `d.SetSprite`가 새 스프라이트 크기에 맞춰 `d.scaleMultiplier`를 변경했거나 GPU 재생 도중 `d.rotAngle`/`d.scaleVec`/`d.pivotOffsetVec`이 바뀌었더라도, `d.UpdatePosition()`이 호출되지 않았기 때문에 `d.pivotTrans`와 `d.childTransform`의 `Transform`에는 **새 `scaleMultiplier`나 현재 논리 상태가 반영되어 있지 않거나 활성화 직전의 낡은 Transform 값이 남아 있다!**
- **(d) 가장 싼 진단 방법**:
  - `src/GpuDecoImagePlayback.cs` 82행(`RefreshGpuImageGeometry` 시작 지점, 즉 이미지가 실제로 바뀌거나 Flush될 때만 실행되는 경로)에서 **`d.UpdatePosition();`을 단 1줄 호출**한 뒤 `ImageFixtureItem`을 캡처하게 하고 `round109/110`의 정지 경계 픽셀 비교(`framecmp diff`)를 실행한다.

#### 가능성 2위: `GpuDeco.compute`의 월드 정점 좌표 계산(1~2 ULP 오차, 최대 `0.0078125` world error)이 반투명 안개(`nesFires`) 가장자리에서 래스터라이저 1픽셀 경계를 넘기는 문제

- **(a) 원인 / 메커니즘**:
  - `docs/claude-gpudeco-image-playback-20261024.md`의 `round107~110` 측정치를 보면, 전체 `3,686,400` 픽셀 중 오차 픽셀이 **42~121개**뿐이고 `maxRGBA`가 `(3, 2, 20, 0)` ~ `(5, 2, 10, 0)`으로 블루 채널만 10~25 수준이다.
  - 또한 `images audit`(203행)에서 `color8_mismatch=0`, `UV_mismatch=0`, `material_texture_mismatch=0`인데 **`pose_error_gt_1e5`만 0이 아니며(`max_world_error = 0.0078125`)**, `GpuDecoNativeMatrixProbe`에서 2,136개 각도 중 6개 각도의 회전 basis 불일치 및 `TransformPoint` float 덧셈 결합 순서 차이(4,302/17,088 성분)가 확인되었다.
- **(d) 가장 싼 진단 방법 (`GpuDecoImageIsolatedFixture` 작성 없이 5줄로 즉시 판별)**:
  - 미완성 초안인 `GpuDecoImageIsolatedFixture.cs`나 `ImagePixelProbe` 셰이더를 빌드할 필요가 없다!
  - `LocateGpuImageDifference`(`src/GpuDecoImagePlayback.cs` 210~248행)에서 일시정지 경계 시점에 모든 가시 이미지 장식 `i`에 대해, **CPU의 실제 `r.transform.TransformPoint(v)` 네 모서리 좌표를 `points[i*4+k].position`에 덮어쓴 뒤 `slice.Gpu.VertexBuffer.SetData(points)`와 `Graphics.ExecuteCommandBuffer(slice.Commands)`를 1회 실행**하여 화면을 캡처해 본다.
  - **판정**:
    - 만약 CPU `TransformPoint` 정점을 GPU VB에 넣었을 때 **픽셀 차이가 0**이 되면 → 재질/텍스처/정렬/블렌딩은 100% 일치하며 **오직 `GpuDeco.compute`의 정점 좌표 수식 오차**가 유일한 원인임이 확정된다.
    - 반대로 CPU `TransformPoint` 정점을 넣었는데도 **똑같이 42~121픽셀 차이가 나면** → 정점 수식 문제가 아니라 아래 **가능성 3위(정렬 키 / `wrapMode` / `LazySprite` 타이밍)** 가 원인임이 100% 확정된다!

#### 가능성 3위: `PrepareGpuLive`에서 이미지 장식 4,976개 편입 시 숨겨진 장식의 정렬 거리(`CandidateSortKey`의 `d.pivotTrans.position` vs `BuildLiveRun`의 `first.R.bounds.center`) 또는 `LazySprite` Flush 시점 차이

- **(a) 원인 / 메커니즘**:
  - `src/GpuDecoExport.cs` 928행(`PrepareGpuLive`)은 `r.forceRenderingOff == true`인 장식의 정렬 키를 구할 때 `CandidateSortKey`(734행 `Vector3 pos = d.pivotTrans.position;`)를 쓰지만, 1142행(`BuildLiveRun`)에서 독립 `MeshRenderer`의 `mesh.bounds` 중심을 설정할 때는 `first.R.bounds.center`를 쓴다.
  - 또한 `FlushGpuImagesBeforeVisible()`(`src/GpuDecoImagePlayback.cs` 165행)은 `LateUpdate` 끝(`DispatchGpuLiveSlice`)에서 알파(`ch10 * ch11 > 0`)를 보고 `LazySprite.Flush(d)`를 호출하는 반면, CPU 경로는 `FastMove` / `DecoAnim` / `TightLate` 도중 `InvisibleSkip`에서 Flush한다. 만약 같은 프레임에 다른 장식이 같은 공유 텍스처의 `wrapMode`나 상태를 읽는다면 미세한 타이밍 차이가 날 수 있다.
- **(d) 가장 싼 진단 방법**:
  - 정지 비교 직전 콘솔에서 `LazySprite.FlushAll()`을 먼저 실행한 뒤 캡처하거나, 위 가능성 2위의 CPU `TransformPoint` 주입 테스트 결과(0인지 비-0인지)로 즉시 분리한다.

### 3.2 질문 2(b): 이미지 장식을 DrawCollapse 구간(`CollapseSpan`)에 넣을 때 필요한 구조

현재 `GpuDecoBatch.shader`와 `EvaluateCollapse` 커널은 **정점 버퍼 레이아웃(60B stride)을 전혀 바꿀 필요 없이** `_CollapseTexture` 버퍼(`gpuCollapseTexture`, 아이템당 `Vector4` 2개 = 32B)에서 텍스처 배열 슬라이스와 UV 스케일을 읽도록 이미 설계되어 있다! 따라서 아래 3가지만 연결하면 된다:

1. **준비 단계(`PrepareDrawCollapse`, `src/GpuDecoDrawCollapseRenderer.cs` 125~203행)에서 `gpuImageVariants`의 모든 `ReadyQuad` 텍스처를 배열/슬롯에 사전 등록**:
   - 현재 127~141행은 각 장식의 **초기 스프라이트 텍스처**만 `textures` 사전에 넣는다.
   - 여기에 `if (gpuImageCandidate && gpuImageVariants != null)`일 때 `gpuImageVariants` 중 `Status == GpuDecoImageVariant.Kind.ReadyQuad`인 모든 변형의 `v.Source.sprite.texture`를 `textures` 사전에 함께 넣는다 (`round99`에서 확인된 유효 quad 변형은 103개이며 상당수가 동일한 안개 텍스처들을 공유하므로 기존 4 DXT5 Arrays + 32 Direct 슬롯 예산 안에 들어간다).
   - 184행·191행에서 채운 `addresses` 사전(`Dictionary<int, Vector4>`, 키: `Texture2D.GetInstanceID()`, 값: `(arrayOrSlot, slice, uScale, vScale)`)을 정적 필드 `gpuCollapseAddresses`에 보존한다.
2. **런타임 이미지 교체/Flush 시(`RefreshGpuImageGeometry`, `src/GpuDecoImagePlayback.cs` 105행) `_CollapseTexture` 버퍼의 해당 아이템 슬롯만 32B 부분 갱신**:
   - 정점 로컬 좌표(`Shape[0..3]`)와 스프라이트 UV(`UV[0..3]`)는 이미 `RefreshGpuImageGeometry`의 `slice.Gpu.UpdateImageGeometry(item, next)`(105행)가 `slice.Gpu`의 `_Static` / `_UV` / `_ImageShape` 버퍼에 갱신하고 있다.
   - 여기에 추가로, `gpuCollapseTexture != null`일 때 새 텍스처 `t = source.sprite.texture`의 주소를 `gpuCollapseAddresses`에서 찾아 **`gpuCollapseTexture` 버퍼의 `item * 2`번째부터 `Vector4` 2개(32바이트)만 부분 업로드**한다:
     ```csharp
     // src/GpuDecoDrawCollapseRenderer.cs 에 헬퍼 추가
     static readonly Vector4[] gpuCollapseTexScratch = new Vector4[2];
     internal static void UpdateDrawCollapseTextureSlot(int item, Texture2D t, Material m)
     {
         if (gpuCollapseTexture == null || gpuCollapseAddresses == null) return;
         if (!t || !gpuCollapseAddresses.TryGetValue(t.GetInstanceID(), out var address))
         {
             if (!gpuCollapseTextureFallback[item])
             {
                 gpuCollapseTextureFallback[item] = true;
                 gpuCollapseTextureFallbackCount++;
                 gpuCollapseIncluded[item] = false;
                 gpuCollapsePendingFallback = "image_texture_not_in_collapse_arrays item=" + item;
             }
             return;
         }
         gpuCollapseTexScratch[0] = new Vector4(address.x + (m.IsKeywordEnabled("USE_POINT_SAMPLING") ? 128 : 0), address.y, 0, 0);
         gpuCollapseTexScratch[1] = new Vector4(address.z, address.w, address.z / t.width * .5f, address.w / t.height * .5f);
         gpuCollapseTexture.SetData(gpuCollapseTexScratch, 0, item * 2, 2);
     }
     ```
   - 또한 `gpuCollapseOn && gpuCollapseIncluded[item]`인 동안에는 독립 렌더러(`slice.Renderers[item]`)가 꺼져 있으므로, `RefreshGpuImageGeometry` 108~112행의 `renderer.GetPropertyBlock` / `SetPropertyBlock` 호출을 건너뛰어(또는 `gpuCollapseOff` 복귀 시에만 일괄 반영) 엔진 MPB 갱신 비용을 아낀다.
3. **`GpuDeco.compute`의 `EvaluateCollapse` 커널에 `_ImageShape`(`Flags & 64`) 지원 확인**:
   - 만약 이미지 장식에 `images matrix on`(`Flags & 64`, `_ImageShape` 버퍼) 경로를 사용한다면, `slice.Gpu.DispatchCollapse`에서 `EvaluateCollapse` 커널에도 `_ImageShape` 버퍼를 바인딩하고 `Flags & 64`일 때 `EvaluateImageLive`와 동일한 행렬 곱으로 정점을 계산하도록 맞춰야 한다.
   - 정렬 순서 보존: `PrepareGpuImageAdmission`(`src/GpuDecoImagePlayback.cs` 48~49행)에서 이미 삼각형 인덱스(`Triangles[0..5]`)가 불변인 쿼드만 허용하고, `RefreshGpuImageGeometry`(93~96행)에서 `sortingLayerID`, `sortingOrder`, `renderQueue`, `sharedMaterial`이 불변임을 검증하므로 **구간 내 페인터 정렬 순서와 인덱스 버퍼는 100% 보존된다.**

### 3.3 질문 2(c): 이미지 장식 편입 시 바꿔야 할 `GpuDecoDrawCollapse*.cs` 및 `GpuDecoImagePlayback.cs`의 정확한 지점

| 파일 | 함수 및 줄 번호 | 현재 코드 (차단 원인) | 변경 방법 |
|---|---|---|---|
| `src/GpuDecoDrawCollapse.cs` | `RequireDrawCollapse` (19~20행) | `checks.Check(GpuLiveOwned==23547 && ...)` — `23547` 하드코딩으로 인해 이미지 편입(`28523`) 시 즉시 예외 발생 | `int expected = (gpuImageCandidate && gpuImageOwn) ? 28523 : 23547; checks.Check((GpuLiveOwned==23547 \|\| GpuLiveOwned==expected) && ...)` |
| `src/GpuDecoDrawCollapseRenderer.cs` | `PrepareDrawCollapse` (137~139행) | `RequireLive(t && t.GetInstanceID()==source.Data.Items[i].TextureId && t.mipmapCount==1 && t.wrapModeU==TextureWrapMode.Clamp && t.wrapModeV==TextureWrapMode.Clamp, ...)` — `round99`에서 확인됐듯 이미지 변형 텍스처는 초기 `wrapMode`가 `Repeat`인 표본(4,180회)이 존재함 | `gpuImageVariants`의 `ReadyQuad` 텍스처들을 `textures` 사전에 추가하고, V15 활성 맵(`!ADOBase.controller.disableV15Features`)에서는 `LazySprite.Defer`/`SetSprite`가 원래 `Clamp`로 바꾸므로 이미지 변형 텍스처의 초기 `wrapMode`를 `Clamp`로 맞추거나 검사에서 허용 |
| `src/GpuDecoDrawCollapse.cs` | `GpuDrawCollapseCommand` ("drawcollapse diag", 86~87행) | `RequireLive(texture && texture.GetInstanceID()==slice.Data.Items[i].TextureId, "drawcollapse texture changed since preparation");` | `UpdateImageGeometry`에서 `slice.Data.Items[item].TextureId = next.TextureId`로 갱신하거나, `GpuImageActor`인 경우 현재 스프라이트 텍스처가 `gpuCollapseAddresses`에 있는지 검사 |
| `src/GpuDecoImagePlayback.cs` | `RefreshGpuImageGeometry` (105행) | `slice.Gpu.UpdateImageGeometry(item, next);`만 호출하고 `_CollapseTexture`는 갱신하지 않음 | 직후에 `UpdateDrawCollapseTextureSlot(item, source.sprite.texture, source.sharedMaterial);` 호출 추가 |
| `src/GpuDecoImagePlayback.cs` | `FlushGpuImagesBeforeVisible` (156~172행) | 매 프레임 `gpuImagePending` **4,476개 전체**를 순회하며 `Dictionary.TryGetValue` + `EvalMasterSlotCurrent` 8,952회 호출 (**성능 병목!**) | **핵심 최적화**: 매 프레임 4,476개를 전수 순회하지 말고, `GpuLiveSlice.MarkDirty(int slot)`(`src/GpuDecoExport.cs` 119행)에서 알파 채널(`(slot & 15) == 10 \|\| (slot & 15) == 11`)이 더티될 때만 `gpuImagePending` 여부를 확인해 깨우도록 변경! (프레임당 4,476회 순회 → **0회**) |

> **특별 주의 (`FlushGpuImagesBeforeVisible`의 숨은 프레임당 비용)**:
> `docs/claude-gpudeco-image-playback-20261024.md`의 `round105` 로그에서 260초 시점 `pending`이 **4,476개**였다. 현재 `FlushGpuImagesBeforeVisible()`(`src/GpuDecoImagePlayback.cs` 156~172행)은 **매 프레임** `gpuImagePending`(4,476개)을 리스트로 복사하고 루프를 돌며 `LazySprite.GpuPeekPending`(Dictionary 조회 4,476회)과 `EvalMasterSlotCurrent`(8,952회)를 실행한다!
> 알파 채널(10, 11)은 이미 `GpuLiveSlice.MarkDirty(slot)`(`src/GpuDecoExport.cs` 119~123행)가 `pendingAlpha` 목록으로 정확히 추적하고 있으므로, **`FlushDirty()`의 `pendingAlpha` 루프 안에서만 `gpuImagePending.Contains(master)`를 검사**하면 평상시 프레임당 4,476개 순회 비용이 **완전히 0**이 된다!

## 4. 질문 3: `FastMove.Prefix` (`GpuLiveSkip`)의 `SaveGpuTarget` 생략 안전성 검증

이전 묶음의 `src/GpuDecoExport.cs`(282~313행, 623~675행) 및 `src/GpuDecoRecovery.cs`(12~91행)를 줄 단위로 대조한 결과, **`!GpuRecoveryTesting`일 때 `SaveGpuTarget`을 생략하는 것은 100% 안전하다(확인 완료).**

- **(a) 원인 / 메커니즘 (안전한 근거)**:
  1. `GpuLiveSkip`(`src/GpuDecoExport.cs` 282~313행)의 호출 순서를 보면:
     ```csharp
     PreflightGpuImageTarget(fx,d,masterIdx);
     PrepareGpuEffect(fx);
     SaveGpuTarget(masterIdx,d);
     captured=true;
     ApplyGpuLiveEffectToTarget(fx, masterIdx, d);
     ```
  2. 예외가 발생할 수 있는 모든 사전 검증과 할당 — 즉 이미지 이벤트 유효성 검사(`PreflightGpuImageTarget`), 이징/채널 지원 여부 검사(`PrepareGpuEffect` 57~60행), 그리고 소스 클럭 슬롯 할당(`AllocateGpuLiveSource` 61행, 풀 고갈 시 `InvalidOperationException`) — 은 **모두 `SaveGpuTarget`이 호출되기 전(`captured == false`)에 이미 끝난다!**
  3. `captured = true` 이후에 실행되는 `ApplyGpuLiveEffectToTarget(fx, masterIdx, d)`(`src/GpuDecoExport.cs` 623~675행) 내부를 전수 확인하면:
     - 625행 `PrepareGpuEffect(fx)`는 이미 `gpuCurrentFxParsed == true`이므로 첫 줄(54행)에서 즉시 반환한다.
     - 626~668행 및 673~674행은 사전 할당된 고정 배열(`gpuMasterChannels`, `gpuMasterAnchors`, `gpuMasterSlotSource`, `slice.Channels`, `slice.DirtyBlocks`)에 대한 순수 float 대입과 `ZeroTween.Eval` 산술뿐이므로 예외를 던질 수 없다.
     - 유일하게 예외를 던질 수 있는 코드는 **669행 `GpuRecoveryFault("target_after_channel");`** 단 한 줄뿐이며, 이 함수는 오직 `RunGpuRecoveryFault` 테스트 중(`GpuRecoveryTesting == true`)에만 예외를 던진다(`src/GpuDecoRecovery.cs` 197~202행).
- **(b) 깨질 수 있는 시나리오**:
  - `RunGpuRecoveryFault("target_after_channel")` 자동화 테스트를 돌릴 때만 `RollbackGpuTarget()`이 필요하므로, `if (GpuRecoveryTesting)` 조건을 유지하면 자동화 복구 테스트와 실제 게임 재생 양쪽 모두 깨지는 시나리오가 **전혀 없다**.
  - 만약 `PreflightGpuImageTarget`이나 `PrepareGpuEffect`에서 예외가 나더라도 그때는 원래 `captured == false`여서 `RollbackGpuTarget()`을 부르지 않고 곧바로 `RecoverGpuLive`로 가므로 동작이 100% 동일하다.
- **(c) 코드 수정 지점 (`src/GpuDecoExport.cs` 291~312행)**:
  ```csharp
  // src/GpuDecoExport.cs 291~312행
  PrepareGpuEffect(fx);
  if (GpuRecoveryTesting) { SaveGpuTarget(masterIdx, d); captured = true; }
  ApplyGpuLiveEffectToTarget(fx, masterIdx, d);
  gpuLiveSkippedRecs += keys; return true;
  ```
  그리고 `finally` 블록(312행)도 `if (captured) { gpuTargetUndoCount = 0; gpuTargetUndoDecoration = null; }`로 감싼다.
- **(d) 가장 싼 검증 방법**:
  - 수정 후 기존 복구 단위 시험(`RunGpuRecoveryFault("target_after_channel")` 및 `"preflight"`)이 그대로 PASS하는지 확인하고, 260~270초 재생에서 `FastMove.Prefix` CPU 샘플(기존 0.16ms) 감소를 확인한다(추정 절감량: **약 0.05~0.08ms/프레임**).

## 5. 종합: 202 FPS(4.94ms) → 400 FPS(2.50ms) 달성 단계별 예상 절감표 (추정)

| 단계 | 적용 항목 | 제거되는 메인/렌더 스레드 작업 | 예상 절감 (추정) | 누적 프레임 시간 (추정) |
|---|---|---|---:|---:|
| 현재 기준 | DrawCollapse 안전 모드 (`23,547` 소유) | - | - | **4.94 ms (~202 FPS)** |
| **Step 1** | 질문 1 수정: `Plan`의 `SameBucket` 분리 + 비활성 장벽 분할 차단 + `KeepBeforeBarrier` 활성화 (+ 잔여 14개 후방 구간 `z - 1e-4f` 분리) | `tieExcluded` `506 → 0`개, `DispatchDrawCollapse` 내 독립 폴백 `DispatchEvaluate` + `ExecuteCommandBuffer` 복사(5.6%) 및 엔진 draw ~150회 제거 | **-0.25 ~ -0.35 ms** | **~4.60 ms (~217 FPS)** |
| **Step 2** | 질문 2 수정: 이미지 장식 4,976개 픽셀 오차 수정(`d.UpdatePosition()` 1회 호출 등) 후 GPU 소유(`28,523`개) + `DrawCollapse` 편입 + `FlushGpuImagesBeforeVisible` 더티 구동화 | 비소유 안개 4,976개의 `DecoAnim.StepPair/SetPosition`(0.22ms), `TightLate.List/Process/Apply`(0.59ms), `FastMove`(0.16ms), 워커 스레드 `TransformChangeDispatch` 및 남은 장벽/드로우 제거 | **-1.10 ~ -1.40 ms** | **~3.25 ms (~308 FPS)** |
| **Step 3** | 질문 3 수정 (`SaveGpuTarget` 생략) + `CheckDrawCollapseBeforeDispatch` 순환 점검 주기 완화(`OwnedChecksPerFrame` 16→4, `CollapseBarrierSweepPerFrame` 32→8) + `scrDecorationManager.LateUpdate` 빈 목록 조기 탈출 | `GpuLiveFrame` 검증 오버헤드 및 `LateUpdate` 루프 껍데기 제거 | **-0.45 ~ -0.65 ms** | **~2.60 ~ 2.80 ms (~360~385 FPS)** |
