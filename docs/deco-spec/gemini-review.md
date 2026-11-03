# 얼불춤(ADOFAI) 커스텀 맵 장식 GpuDeco 최대 최적화 설계 및 `00-deco-spec` / `01-implementation-order` 종합 검토 보고서 (v2)

## 결론 (5줄 이내)

1. **현행 GpuDeco(298.8 FPS)가 빠른 진짜 이유:** GPU에서 구간을 이진 탐색하는 구조가 아니라, `FastMove.Run` → `DecoBake.GpuLiveSkip`이 바뀐 채널만 갱신하고 `AdvanceGpuLiveSources`가 CPU에서 활성 효과(Source)당 이징을 1회만 계산한 뒤 GPU(`EvaluateCollapse`)는 탐색·이징 없이 산술만 수행하기 때문입니다.
2. **저소유율 맵(7777 등)에서 FPS가 떨어진 원인:** 비소유 장벽(`1 - r`)이 그리기 구간(`CollapseSpan`)을 잘게 쪼개고 동점 장식을 독립 렌더러(`gpuCollapseTextureFallback`)로 강등시켜 **이중 컴퓨트 디스패치(`DispatchEvaluate`) + `DynamicOrderTick` 감시 비용(고정+장벽 비례 약 0.25~0.40ms)**이 절감액보다 커지기 때문이며, 손익분기점은 **유효 소유 비율 약 35~50% 이상(소형 맵은 55% 이상) 및 구간당 평균 합침 수 4개 이상**입니다.
3. **`01-implementation-order.md`(G1→G5) 순서안 평가:** G1(무변화 이벤트 무시 + `visible` 분리 + 사전 맵 판정 + `FastMove.Orig` 격리)만으로 116개 맵 상한이 **62.5% → 69.6%(Exit 27.4% → 85.8%, Windflower 2.8% → 48.6%)**로 뛰므로 G1 우선 전략은 타당하나, **G1 시험 대상인 HELLO(31.7%)는 이미 셰이더에 구현된 `lockRotation`/`lockScale` 거부 해제(56.9%)까지 함께 넣어야 손익분기점을 넘깁니다.**
4. **끊김·복귀 0 달성(Q5 & G1-4/G1-5):** `FastMove.Orig`의 무조건적 `RecoverGpuLive`를 **소유 장식 포함 여부 확인 + 개별 장식 강등(`gpuCollapseMapCpu = uint.MaxValue`)**으로 바꾸고, `PreparePlayer` 전에 가벼운 사전 판정을 두며 실패 시에도 `GcControl.CleanAfterPrepare`를 실행하면 Exit 복귀(1회)와 HELLO(20회)·DDONGSSADA(9회) 끊김이 즉시 0이 됩니다.
5. **깊이 11만 건(Q2)·마스크(Q3)·오버드로(Q6):** 깊이는 **정적 버킷 슬롯 풀 + `_CollapseMap` 인덱스 스왑**으로 재계획 0회로 풀고, 마스크는 **상태 변경 4개 지점의 이벤트 푸시**로 전수 검사를 없애며, 오버드로는 **`WriteVerticesAt`의 앞쪽 불투명 가리개 AABB 접기**로 픽셀 오차 0을 유지하며 줄입니다.

---

## 0. 전제 교정 및 `00-deco-spec.md` 대조에서 발견한 핵심 코드 불일치

### 0-1. 질문 전제 교정 2가지
1. **전제 교정 1 — "현재 GpuDeco는 맵 열 때 구운 구간 배열(`Tracks`)을 GPU에서 평가한다"는 전제:**
   - **코드 근거:** `src/GpuDecoExport.cs:PrepareGpuLive` (938행: `for (int ch = 0; ch < 16; ch++) item.Tracks[ch] = Array.Empty<DecoBakeCore.Segment>();`).
   - 실제 제품(`GpuDeco`)은 라이브 모드에서 `Tracks` 배열을 GPU로 전혀 업로드하지 않습니다. 표(`DecoBakeCore.Table`)는 준비 시점에 **지원 여부 판정과 가시 구간(`VisibleInWindow`) 계산**에만 쓰이고, 곡 중에는 `FastMove.cs:Run`(723행) → `GpuDecoExport.cs:GpuLiveSkip`(313행) → `ApplyGpuLiveEffectToTarget`(662행)이 이벤트 시작 시점에 `LiveChannel { start, change, end, sourceIndex }`(16B)를 갱신합니다.
   - 매 프레임 `GpuDecoExport.cs:AdvanceGpuLiveSources`(616행)가 **활성 효과(Source) 단위로 CPU에서 `ZeroTween.Eval`을 딱 1번씩만 계산**해 `_SourceE`에 올리고, GPU `GpuDeco.compute:EvaluateCollapse`(347~375행)는 **이진 탐색 0회, GPU 이징 0회, 알파 조기 종료(`!(v[10] * v[11] > 0)`)**만 수행합니다.
2. **전제 교정 2 — "`lockRotation`, `lockScale`, `dontAlterX`, `dontAlterY`는 새로 GPU 셰이더를 짜야 한다"는 전제:**
   - **코드 근거:** `src/GpuDecoExport.cs:PrepareGpuLive`(867행 `camScale`, 932행 `Flags = (d.lockScale ? 1 : 0) | (d.lockRotation ? 2 : 0) | (d.parallax.dontAlterX ? 4 : 0) | (d.parallax.dontAlterY ? 8 : 0) ...`), `src/GpuDeco.compute:WriteVerticesAt`(246행 `flags & 4`, `flags & 8`, 251~252행 `flags & 1`, 256행 `flags & 2`), `src/GpuDecoDrawCollapseRenderer.cs:DispatchDrawCollapse`(1338~1348행 `new Vector4(ctm.position.x, ctm.position.y, ctm.eulerAngles.z, camScaleM)`).
   - 프레임당 카메라 유니폼 전달과 컴퓨트 셰이더 정점 계산이 **이미 100% 구현**되어 있으나, `src/DecoBake.cs:SetupPostfix`(253행)와 `src/DecoBake.cs:Collect`(405행)에 초기 거부 조건만 남아 있어 막혀 있습니다.

### 0-2. `docs/00-deco-spec.md` 명세와 현재 코드(`src/`) 사이의 숨은 불일치 3가지
1. **시차(`Parallax`, 채널 12·13) 끝점 계산 불일치 (`00-deco-spec.md` 4절 5번 대조):**
   - 명세 4절 5번과 `game/game-excerpts.cs:StartEffect`(484~487행)에서確認되듯, `TweenType.Parallax`는 `ScaleX/Y`와 마찬가지로 **`OnComplete` 콜백이 없습니다.**
   - 라이브 경로인 `src/GpuDecoExport.cs:ApplyGpuLiveEffectToTarget`(696행: `bool isScaleOrPar = c == 5 || c == 6 || c == 12 || c == 13;`)과 `SetMasterSlotStatic`(555행)은 채널 12·13을 포함해 `startVal + (target - startVal) * easeDurEnd`로 계산하지만, **표 컴파일러인 `src/DecoBake.Core.cs:Compile`(368행: `bool isScale = c == ScaleX || c == ScaleY;`)과 `src/GpuDecoExport.cs:PrepareGpuLive`(1093행: `IsScale = (ch == 5 || ch == 6)`)는 채널 12·13을 누락**하여 `end = target`으로 굽고 있습니다.
   - `Flash`/`InFlash`/`OutFlash` 등 끝점 이징값이 1이 아닌 시차 트윈이 끝난 뒤 `LastPosition`이나 가시성 판정, 곡 중간 진입(`AdoptState`)이 실행되면 표와 실제 게임 값이 어긋납니다. `DecoBake.Core.cs:368`도 `c == 5 || c == 6 || c == 12 || c == 13`으로 통일해야 합니다.
2. **`SetMaskingType(MaskingType.None)`의 은밀한 부수효과 (`00-deco-spec.md` 4절 1번 대조):**
   - `game/game-excerpts.cs:SetMaskingType`(608~611행)은 `visible`(`rendererEnabled`)을 확인하지 않고 무조건 `spriteRenderer.enabled = !isMask()`를 실행합니다. 또한 `StartEffect`(491행 `SetVisible`, 526행 `SetMaskingType`)에서 마스크 설정이 `visible`보다 **뒤에** 실행됩니다.
   - 따라서 G1에서 "값이 안 바뀌는 이벤트 무시"를 구현할 때, 원래 `maskingType == MaskingType.None`인 장식에 `maskingTypeUsed = true, targetMaskingType = MaskingType.None`인 이벤트가 오더라도 **그 시점에 `rendererEnabled == false`(숨겨진 상태)이거나 같은 이벤트에서 `visible = false`를 줬다면 "값 안 바뀌는 무효 이벤트"로 버리면 안 됩니다.** 이 경우 `spriteRenderer.enabled`가 `true`로 켜지면서 `rendererEnabled`는 `false`로 남아 `LateUpdate`(`UpdatePosition`)는 멈춘 채 화면에 그려집니다.
3. **`LastPosition`이 진행 중 트윈을 끊을 때의 처리 (`00-deco-spec.md` 4절 2번 및 `01-implementation-order.md` G1-6 대조):**
   - `game/game-excerpts.cs:StartEffect`(261~269행)는 `startPos = dec.pivotPosVec`을 `value.Kill(complete: true)` **앞에서** 읽고, 트윈 시작값 `newPos = dec.pivotPosVec`은 `Kill(complete: true)` **뒤에서**(직전 목표값으로 완료된 상태) 읽습니다.
   - 확인 결과, 현재 `src/DecoBake.Core.cs:Compile`(361~367행: `current`를 `target`에 더하고 `start`는 `list[list.Count - 1].End` 사용)과 `src/GpuDecoExport.cs:ApplyGpuLiveEffectToTarget`(688~705행: `currentBeforeKill = EvalMasterSlotCurrent`를 `origin`으로 쓰고 `startVal = afterKill` 사용), 그리고 `src/FastMove.cs:Run`(744행, 765행)은 **이미 명세 4절 2번과 정확히 일치하게 구현**되어 있습니다(체인 합성 반례 `15→105`는 `src/DecoV2NominalChain.cs`만의 버그였음).
   - 다만 `DecoBake.Core.cs:Compile`(361행)은 명목 시각 `e.T0`에서 `Value()`를 평가하는 반면 실제 게임은 프레임 시각에서 끊기므로, 진행 중인 트윈을 `LastPosition`으로 끊는 장식(`replacement && e.RelativePosition`)에서 명목 시간 체인 오차가 발생할 수 있습니다. GpuDeco 라이브 경로는 실제 프레임 시점의 `EvalMasterSlotCurrent`를 쓰므로 오차가 `0`입니다.

---

## 추가 질문 및 `01-implementation-order.md` 검토: 저소유율 FPS 역전 원인과 손익분기점

### 1. 소유 비율이 낮으면 FPS가 오히려 떨어지는 원인 (코드 근거)

`01-MEASUREMENTS.md` 9절 시제품 2차에서 **`7777` 맵(전체 498개 중 88개 소유 = 17.7%)이 복귀 없이 완주했는데도 373 FPS(2.681ms) → 346 FPS(2.890ms)로 +0.209ms/frame 느려진 원인**은 아래 3가지 코드 경로의 합산입니다.

1. **비소유 장벽(`1 - r`)에 의한 구간(`CollapseSpan`) 파편화 및 동점 강등으로 인한 이중 컴퓨트 디스패치:**
   - `src/DrawCollapsePolicy.cs:Plan`(153~165행)은 정렬 순서상 비소유 장벽(`!row.Owned`)이 끼어들 때마다 `CollapseSpan`을 끊습니다. 소유율이 `r = 17.7%`이고 비소유 장벽이 `82.3%`(410개)이면, 소유 장식 88개가 수십 개의 자잘한 `CollapseSpan` 렌더러로 쪼개져 드로우콜 통합 이득이 사라집니다.
   - 더 치명적인 것은 `src/GpuDecoDrawCollapseRenderer.cs:BuildDrawCollapseSpans`(888~907행) 및 `ReclassifyBucket`(650~665행)입니다. 비소유 장벽과 같은 `(Layer, Order, Queue, Distance)` 묶음에 걸려 동점 분리된 소유 장식이 **단 1개라도** `gpuCollapseTextureFallback[i] = true`(`gpuCollapseTextureFallbackCount > 0`)로 강등되면, `src/GpuDecoDrawCollapseRenderer.cs:DispatchDrawCollapse`(1397~1409행)가 매 프레임 실행됩니다:
     ```csharp
     if(gpuCollapseTextureFallbackCount>0 && ((source.CopyMask?.Low??ulong.MaxValue)|(source.CopyMask?.High??ulong.MaxValue))!=0)
     {
         source.Gpu.DispatchEvaluate(); // <-- 소유 장식 전체에 대해 두 번째 컴퓨트 커널 디스패치!
         ...
         if(source.NativeCopy==null || !source.NativeCopy.Execute(low,high))Graphics.ExecuteCommandBuffer(source.Commands);
     }
     ```
     즉, `EvaluateCollapse` 커널 디스패치(1347행)에 더해 **독립 렌더러 복사용 `DispatchEvaluate()` + `NativeCopy.Execute()`가 한 번 더 돌아가며** CPU/GPU 제출 비용이 2배로 뜁니다.
2. **비소유 장식 수(`(1 - r) * N`)에 비례하는 `DynamicOrderTick` 네이티브 속성 조회 비용:**
   - `src/GpuDecoDrawCollapseRenderer.cs:BuildBarrierIndex`(573~579행)는 **`gpuLiveMasterByDeco`에 속하지 않은 모든 비소유 시각 장식(`!gpuLiveMasterByDeco.ContainsKey(vd)`)을 `gpuWatchRows`에 등록**합니다.
   - 소유율이 낮을수록 `gpuWatchRows.Count`가 커지며(`7777`은 약 410개, `DDONGSSADA` 시제품 2차는 약 5,833개), `DynamicOrderTick`(608~648행)이 8프레임마다 모든 `gpuWatchRows`를 돌며 `r.enabled`, `r.gameObject.activeInHierarchy`, `r.forceRenderingOff`, `r.sortingOrder`, `r.transform.position.z`, `r.sortingLayerID`(Mono→C++ P/Invoke 호출)를 읽습니다.
3. **소유 개수와 무관한 GpuDeco 프레임당 고정 오버헤드(`GpuLiveFrame`):**
   - `src/GpuDecoExport.cs:GpuLiveFrame`(1381~1394행) → `CheckPlayerFrame`(4프레임마다 네이티브 검사) → `AdvanceGpuLiveSources` → `FlushDirty` → `UploadSourceE` → `CheckDrawCollapseBeforeDispatch`(8프레임마다 `ValidateDrawCollapse(false)`) → `DispatchCollapseMinimal` → `ApplySpanCulling`은 소유 장식이 88개든 28,000개든 매 프레임 고정 비용을 지불합니다.

### 2. 몇 % 이상이어야 이득인가? (수식 및 실측 기반 손익분기점)

- **비용·이득 계산식 (`추정`, `01-MEASUREMENTS.md` 1절·3절 실측치 기반):**
  - 맵 전체 장식의 원래 프레임 비용을 $\Delta T_{\text{all}} = T_{\text{원래}} - T_{\text{장식없음}}$이라 두면(`01-MEASUREMENTS.md` 3절 곡 전체 평균 기준):
    - `7777` (498개): $\Delta T_{\text{all}} = \frac{1000}{374} - \frac{1000}{470} = 2.674 - 2.128 = \mathbf{0.546\text{ ms}}$ (최악 10초: $3.831 - 3.077 = \mathbf{0.754\text{ ms}}$)
    - `Exit` (3,524개): $\Delta T_{\text{all}} = \frac{1000}{386} - \frac{1000}{535} = 2.591 - 1.869 = \mathbf{0.722\text{ ms}}$ (최악 10초: $3.448 - 2.353 = \mathbf{1.095\text{ ms}}$)
    - `Windflower` (6,290개): $\Delta T_{\text{all}} = \frac{1000}{308} - \frac{1000}{542} = 3.247 - 1.845 = \mathbf{1.402\text{ ms}}$ (최악 10초: $5.051 - 2.625 = \mathbf{2.426\text{ ms}}$)
    - `HELLO 2026` (2,531개): $\Delta T_{\text{all}} = \frac{1000}{262} - \frac{1000}{435} = 3.817 - 2.299 = \mathbf{1.518\text{ ms}}$ (최악 10초: $4.651 - 2.778 = \mathbf{1.873\text{ ms}}$)
    - `DDONGSSADA` (6,314개): $\Delta T_{\text{all}} = \frac{1000}{340} - \frac{1000}{608} = 2.941 - 1.645 = \mathbf{1.296\text{ ms}}$ (최악 10초: $5.348 - 2.045 = \mathbf{3.303\text{ ms}}$)
  - GpuDeco가 비율 $r \in [0, 1]$의 장식을 소유할 때:
    - **총 절감 시간:** $S(r) \approx \eta \cdot r \cdot \Delta T_{\text{all}}$ (여기서 $\eta \approx 0.80$은 소유 장식당 비용 절감률; Arche 실측 $(7.72\text{ms} - 1.34\text{ms}) / 7.72\text{ms} = 82.6\%$).
    - **GpuDeco 추가 비용:** $C_{\text{gpu}}(r) = C_{\text{fixed}} + C_{\text{fallback}}(r) + C_{\text{watch}}(1-r)N \approx 0.15\text{ ms} + 0.13\text{ ms} \approx \mathbf{0.28\text{ ms/frame}}$ (단, 동점 폴백이 0이고 구간 합침이 잘 되면 $\approx 0.16\text{ ms/frame}$).
  - **`7777` ($r = 88/498 = 17.7\%$) 대입 검증 (`추정`):**
    $$S(0.177) - C_{\text{gpu}}(0.177) = (0.80 \times 0.177 \times 0.546\text{ ms}) - 0.28\text{ ms} = 0.077\text{ ms} - 0.280\text{ ms} = \mathbf{-0.203\text{ ms/frame}}$$
    실측치인 **$-0.209\text{ ms/frame}$ ($373 \to 346\text{ FPS}$)**와 정확히 일치합니다.
- **손익분기점 소유 비율 ($S(r) > C_{\text{gpu}}(r)$):**
  1. **중·대형 맵 ($\Delta T_{\text{all}} \ge 1.0\text{ ms}$, 장식 2,000개 이상: Exit, Windflower, HELLO, DDONGSSADA):**
     $$r_{\text{break-even}} = \frac{C_{\text{gpu}}}{\eta \cdot \Delta T_{\text{all}}} \approx \frac{0.25}{0.80 \times (0.72 \sim 1.50)} \approx \mathbf{26\% \sim 43\%}$$
     안전 마진을 포함한 실용 기준은 **전체 시각 장식의 `45% ~ 50%` 이상**입니다.
  2. **소형·경량 맵 ($\Delta T_{\text{all}} \approx 0.55\text{ ms}$, 장식 500개 내외: `7777`):**
     $$r_{\text{break-even}} = \frac{0.25}{0.80 \times 0.546} \approx \mathbf{57\%}$$
     따라서 `7777`은 G1(29.9%)이나 G2(37.6%)에서는 켜면 오히려 손해이며, **G3(`+ 반복·이미지 부가`, 소유 상한 `64.5%`)에 도달했을 때 비로소 이득**이 됩니다.
  3. **코드에 넣을 일반 활성화 게이트 규칙 (`GpuDecoPlayerPolicy` / `GpuDecoPlayer.cs:548, 594`):**
     - 맵 이름 예외 없이 모든 맵에서 역전을 막으려면, 단순 `MinimumOwned = 64` 대신 아래 **3중 일반 조건**을 만족할 때만 활성화해야 합니다:
       - `ownedCount >= 64`
       - `ownedCount >= totalVisualCount * 0.45` (또는 `ownedCount - tieExcludedCount >= 300`)
       - **구간 평균 압축비:** `includedCount >= spanCount * 3` (장벽 파편화로 구간당 평균 합침 장식 수가 3개 미만이면 드로우콜 절감이 없으므로 거부)

### 3. `docs/01-implementation-order.md` (G1 → G5) 순서안 검토 및 보완 제안

`01-MEASUREMENTS.md` 9절의 단계별 상한표를 보면 `01-implementation-order.md`가 설계한 **G1(무변화 무시 + `visible`) → G2(카메라·행성) → G3(반복·이미지 부가) → G4(깊이) → G5(마스크)** 순서는 전체 116개 맵 기준 **62.5% → 63.3% → 69.6% → 73.1% → 78.6% → 82.7% → 86.2%**로 점진적 검증에 매우 잘 짜여 있습니다. 다만 **2가지 보완**이 필요합니다.

1. **보완 1 — G1 합격 시험 맵에서 `HELLO`의 위치 또는 `lockRotation`/`lockScale`의 G1 편입:**
   - `01-implementation-order.md` 27행은 G1 합격 기준 시험 맵으로 `(Exit, Windflower, HELLO, 회귀 확인 Arche)`를 지정했습니다.
   - 그런데 `01-MEASUREMENTS.md` 9절 표를 보면 G1(`+ 가시성`) 단계에서 `Exit`는 **85.8%**, `Windflower`는 **48.6%**가 되지만, **`HELLO`는 31.7%에 불과**합니다(`HELLO`의 최대 걸림돌은 `회전·크기 잠금` 1,185개 = 46.8%이기 때문).
   - 더욱이 `lockRotation`, `lockScale`, `dontAlterX`, `dontAlterY`는 **새 셰이더 작업 없이 `DecoBake.cs:253, 405`의 거부 문자열 2줄 제거 + `StopGpuLive` 복원 보강만으로 즉시 동작**합니다.
   - **권고:** (A) G1은 `Exit`(85.8%)와 `Windflower`(48.6%)로 FPS 상승을 검증하고 `HELLO`는 G1에서 "끊김 0·안전 거부(소유율 45% 미만 게이트 작동)"만 확인하든지, (B) 이미 셰이더에 구현된 `lockRotation`/`lockScale`/`dontAlterX`/`dontAlterY` 거부 해제를 G1에 포함시켜 `HELLO`까지 G1에서 **56.9%**로 올려 한 번에 검증하는 것이 훨씬 효율적입니다.
2. **보완 2 — G1-1(무변화 이벤트 무시)에서 `SetMaskingType(None)` 부수효과 방어:**
   - 앞서 0-2절에서 지적한 대로, `maskingTypeUsed`가 `MaskingType.None`을 쓰더라도 대상 장식이 그 시점에 `visible == false`(`rendererEnabled == false`)일 가능성이 있으면 `spriteRenderer.enabled = true`로 켜지는 부수효과(명세 4절 1번)가 있으므로, 해당 장식의 `visible` 트랙이 항상 `true`일 때만 `maskingTypeUsed(None)`을 무변화로 무시해야 합니다.

---

## Q1. 범위 확장 설계 (우선순위·처리 방식·코드 근거)

`01-MEASUREMENTS.md` 2절·9절과 `docs/00-deco-spec.md` 2~5절을 결합한 최적 해결 순서와 각 사유별 처리 방식(**표에 굽기 / 프레임당 값 넘기기 / 원래대로 두기**)은 다음과 같습니다.

| 순위 | 거부 사유 | 선택 방식 | 코드 근거 (`파일:함수`) | 116개 맵 누적 상한 (핵심 수혜 맵) |
|---|---|---|---|---|
| **1 (G1)** | **값이 안 바뀌는 이벤트 필드** (`depth`, `mask`, `originalSize`, `smoothing`, `crop` 등) | **표에 굽기 (거부 사유 제외)** | `src/DecoBake.cs:Reasons`(318행), `src/DecoBake.Core.cs:Compile`(339행) | **62.5% → 63.3%** (`Exit` **27.4% → 59.4%**) |
| **2 (G1)** | **가시성 변경 (`visibleUsed`)** (`depthUsed`와 분리) | **표에 굽기 (계단 채널 + 알파/가시성 게이트)** | `src/DecoBake.cs:Reasons`(329행), `src/FastMove.cs:Run`(755, 795행), `src/GpuDecoExport.cs:HiddenAlpha`(74행) | **→ 69.6%** (`Exit` **85.8%**, `Windflower` **48.6%**, `HELLO` **31.7%**) |
| **3 (G1.5 / G2)** | **카메라 회전·크기 잠금 (`lockRotation`, `lockScale`) 및 시차 축 고정 (`dontAlterX/Y`)** | **프레임당 값 넘기기 (이미 셰이더 구현됨, 거부만 해제)** | `src/DecoBake.cs:SetupPostfix`(253행), `src/DecoBake.cs:Collect`(405행), `src/GpuDeco.compute:WriteVerticesAt`(246~256행) | **→ 73.1%** (`HELLO` **56.9%**, `Exit` **89.8%**, `7777` **37.6%**) |
| **4 (G2)** | **행성 추종 (`followPlanet`) 및 `Camera`·`CameraAspect` 배치** | **프레임당 값 넘기기 (`_Planets`, `_CameraRect` 유니폼)** | `src/DecoBake.cs:SetupPostfix`(251~255행), `src/DecoBake.Core.cs:PlacementReason`(311행), `docs/00-deco-spec.md` 2절 | (위 73.1%에 포함, `HELLO` 836개·`Arche` 114개 해결) |
| **5 (G3)** | **반복(`tile`)·원래 크기(`originalSize`)·크롭 피벗(`cropPivotOffset`)** | **계단 표 + 텍스처 랩 분리** | `src/GpuDecoPerItemPrepare.cs:PerItemPrepareReason`(22행), `src/DecoBake.cs:Reasons`(323행), `docs/00-deco-spec.md` 4절 9번 | **→ 82.7%** (`7777` **39.4% → 64.5%**, `HELLO` **66.0%**) |
| **6 (G4)** | **깊이 변경 (`depthUsed`)** | **계단 표 + 정적 버킷 슬롯 풀(`_CollapseMap` 스왑)** | `src/GpuDecoDrawCollapseRenderer.cs:BuildDrawCollapseSpans`(1016행), `src/GpuDeco.compute:EvaluateCollapse`(349행) | **78.6% (G3 합산 시 82.7%)** (`DDONGSSADA` **21.0% → 66.7%**) |
| **7 (G5)** | **마스크 관련 / 파티클·오브젝트·글자·혼합 모드·히트박스** | **원래대로 두기 (단, 전수 검사를 이벤트 푸시로 교체)** | `src/DecoBake.cs:SetupPostfix`(247~249행), `src/GpuDecoDrawCollapseRenderer.cs:DynamicOrderTick`(608행) | 원래 방식 유지 (렌더링·충돌 규칙 상이) |

### 제안별 상세 설계 (Q1)

#### 제안 Q1-A (G1-1): 값이 안 바뀌는 이벤트 필드 거부 제외
- **무엇을 바꾸는지:**
  - 현재 `src/DecoBake.cs:CopyEvent`(341행)는 대상 장식을 모르는 상태에서 `Reasons(f)`로 이벤트 단위 거부 사유를 만들고, `src/DecoBake.Core.cs:Compile`(339~343행)이 모든 대상 장식 `decorations[d].Reasons`에 일괄 복사합니다.
  - 이를 수정하여 `DecoBake.cs:SetupPostfix`에서 장식별 초기 상태(`initDepth`, `initMaskType`, `initMaskTarget`, `initUseMaskDepth`, `initMaskFront`, `initMaskBack`, `initOrigSize`, `initSmoothing`, `initCropOffset`, `initPlacement`, `initVisible`)를 기록하고, `Collect`/`Compile`에서 시간순으로 장식 `d`의 현재 상태를 추적하며 **실제 값이 달라지거나 부수효과가 발생할 때만** `decorations[d].Reasons`에 사유를 추가합니다.
  - **주의(명세 4절 1번·6번):**
    - `maskingTypeUsed`가 `MaskingType.None`을 쓸 때: 대상 장식이 그 시점에 `curVisible[d] == false`이면 `spriteRenderer.enabled = true`로 켜는 부수효과가 있으므로 무변화로 무시하지 않고 거부(또는 `drawEnabled` 별도 비트로 처리)합니다.
    - `depthUsed`: `!d.syncFloorDepth && f.targetDepth == curDepth[d]`일 때만 무변화로 무시합니다(`syncFloorDepth`는 타일 깊이를 복사하므로 제외).
    - `movementTypeUsed`: `f.movementType == curPlacement[d] && d.followPlanet == null`이고 `startPos`가 불변일 때만 무변화로 무시합니다.
- **예상 이득과 근거:**
  - **사실(`01-MEASUREMENTS.md` 9절):** `Exit from SHiP`의 소유 가능 상한이 **27.4% → 59.4%(+32.0%p)**로 급증하며, 런타임 추가 비용은 **0.00ms**입니다.
- **합격/불합격 판정 측정 기준:**
  - `Exit`에서 준비 로그의 지원 장식 수가 59% 이상으로 증가하고, 정지 프레임 3지점 픽셀 오차 `> 3/255` 픽셀 수 = `0`.
- **깨질 수 있는 동작과 방어책:**
  - 숨겨진 장식(`visible=false`)에 `SetMaskingType(None)`이 호출되어 다시 켜지는 현상(명세 4절 1번) → 위 조건(`curVisible[d] == true`일 때만 `MaskingType.None` 무시)으로 100% 방어됩니다.

#### 제안 Q1-B (G1-2 & G1-3): `visibleUsed` 분리 및 가시성 계단 처리
- **무엇을 바꾸는지:**
  1. `src/DecoBake.cs:Reasons`(329행): `if (f.visibleUsed || f.depthUsed)`에서 `f.visibleUsed`를 빼고 `if (f.depthUsed) r.Add("깊이 변경");`만 남깁니다.
  2. `src/GpuDecoExport.cs`: `bool[] gpuMasterVisible` 배열(초기값 `d.rendererEnabled`)을 두고, `FastMove.cs:Run`(723행 `SkipGpuOrAdopt`)에서 GPU 소유 장식에 `visUsed(fx)`가 있으면 **원본 `d.SetVisible`을 호출하지 않고**(원본 `spriteRenderer.enabled`를 건드리면 `CheckPlayerFrame` 740행과 충돌함) `d.rendererEnabled = visV && !d.forceHide;` 및 `gpuMasterVisible[masterIdx] = d.rendererEnabled;`만 갱신합니다.
  3. 가시성 반영: `gpuMasterVisible[masterIdx] == false`이면 슬라이스의 알파/가시성 판정(`GpuLiveSlice.HiddenAlpha`, 74행)을 `true`로 만들고, GPU로 올라가는 채널 11(`opacity`)의 부호 또는 `LiveChannel` 가시성 플래그를 0으로 마스킹하여 `GpuDeco.compute:EvaluateCollapse`(358행 `!(v[10] * v[11] > 0)`)가 정점 4개를 `(0,0,0)`으로 접고 `CollapseSpanAlpha`(482행)가 유휴 구간 렌더러를 끄게 합니다.
  4. `StopGpuLive`(1688행): 복원 시 `d.SetVisible(gpuMasterVisible[i])`를 호출해 CPU 상태를 일치시킵니다.
- **예상 이득과 근거:**
  - **사실(`01-MEASUREMENTS.md` 2절·9절):** `Exit`에서 단독 912개 중 ~898개가 풀려 상한이 **59.4% → 85.8%**로 뛰고, `Windflower`는 **3.7% → 48.6%**, `HELLO`는 **2.6% → 31.7%**로 상승합니다.
- **합격/불합격 판정 측정 기준:**
  - `Exit`, `Windflower`에서 `CheckPlayerFrame`("다른 처리에서 소유 렌더러 상태 변경") 예외 0회, 가시성 전환 직후 정지 프레임 픽셀 오차 `> 3/255` = `0`.
- **깨질 수 있는 동작과 방어책:**
  - 명세 4절 4번: 숨겨진 동안(`!rendererEnabled`)에는 원래 `LateUpdate`가 `UpdatePosition`을 건너뛰나 어차피 화면에 그려지지 않으므로 픽셀 차이는 `0`입니다(단, `SetMaskingType`을 쓰는 장식은 이미 마스크 사유로 제외되어 있으므로 명세 4절 1번과 겹치지 않음).

#### 제안 Q1-C (G2): `lockRotation`, `lockScale`, `dontAlterX/Y` 즉시 해제 + `followPlanet`, `Camera`/`CameraAspect` 프레임 유니폼 추가
- **무엇을 바꾸는지:**
  1. **즉시 해제 (셰이더 수정 없음):** `src/DecoBake.cs:SetupPostfix`(253행 `if (d.lockRotation || d.lockScale)` 제거) 및 `src/DecoBake.cs:Collect`(405행 `dontAlterX || dontAlterY` 제거, `clampToScreen`만 유지). `src/GpuDecoExport.cs:StopGpuLive`(1711~1713행)에서 `(Flags & 3) != 0`인 장식은 복원 시 회전·크기 마스크(`mask |= (1 << 4) | (1 << 5) | (1 << 6)`)를 강제 켜서 카메라 회전·줌 반영분을 복원합니다.
  2. **`followPlanet` 및 `Camera`/`CameraAspect` 배치 (`docs/00-deco-spec.md` 2절 수식 그대로):**
     - `GpuDeco.compute`에 `float4 _Planets` (불/얼음/3번째 행성 월드 XY 및 일시정지 여부)와 `float4 _CameraScreen` (`orthographicSize`, `aspect`, `cam.pixelRect` 보정값) 유니폼을 추가합니다.
     - `WriteVerticesAt`(240~254행)에서:
       - `followPlanet` 플래그가 켜져 있고 `!paused`이면 `P0 = pivotPos + planetPos`.
       - `Camera`/`CameraAspect` 배치이면 명세 2절의 수식(`screenRel = (pivotPosVec + pivotOffsetVec) / 20 + 0.5`, `CameraAspect`는 `x *= screenH / screenW`, 직교 카메라 월드 투영 `W = camPos + (screenRel - 0.5) * 2 * orthoSize * float2(aspect, 1)`)으로 `W`를 구합니다.
     - `clampToScreen`이 켜진 장식이거나 곡 중 `SetPlacementType(Tile)`이 호출되는 장식은 `DecoBake`에서 계속 거부하거나 이벤트 시점 타일 위치를 `OriginalX/Y`에 기록합니다.
- **예상 이득과 근거:**
  - **사실(`01-MEASUREMENTS.md` 9절):** `HELLO 2026` 상한이 **31.7% → 56.9%(+25.2%p)**, `7777`이 **29.9% → 37.6%**, `Exit`가 **85.8% → 89.8%**로 상승합니다. 프레임당 유니폼 2개(`Vector4`) 추가 비용은 **0.005ms 미만(`추정`)**입니다.
- **합격/불합격 판정 측정 기준:**
  - `HELLO 2026` 소유율 50% 이상 달성, 전곡 완주 시 복귀 0회 및 평균/최악 10초 FPS 상승, 카메라 회전·줌·행성 추종 구간 정지 프레임 픽셀 오차 `> 3/255` = `0`.
- **깨질 수 있는 동작과 방어책:**
  - 일시정지(`ADOBase.controller.paused == true`) 순간 원래 게임의 `UpdatePosition`(`game/game-excerpts.cs:197`)은 `(!ADOBase.controller.paused && followPlanet != null) ? followPlanet.transform.position : Vector2.zero`로 **일시정지 중에는 행성 위치를 0으로 더하는 버그(원본 동작)**가 있습니다! 따라서 정지 프레임 대조(`paused == true`)에서 픽셀 오차 0을 맞추려면, `paused`일 때 행성 위치를 더하지 않는 원본 식인지, 아니면 정지 직전 마지막 `LateUpdate` 값을 유지하는지 확인해야 합니다. (원본은 일시정지 중 `scrDecorationManager.LateUpdate` 자체는 돌지만 `UpdatePosition`은 트윈이나 카메라가 안 바뀌어도 매 프레임 호출되므로 `paused`가 되는 첫 프레임에 `vector = Vector2.zero`로 점프합니다. GPU 유니폼도 `paused ? Vector2.zero : planet.position`을 넘기면 정지 프레임 대조까지 100% 일치합니다!)

#### 제안 Q1-D (G3): `tile`(반복) 텍스처 랩 공유 분리 및 `originalSize`·`cropPivotOffset` 계단 지원
- **무엇을 바꾸는지:**
  1. **텍스처 랩 공유 규칙(`00-deco-spec.md` 4절 9번) 해결:** 현재 `src/GpuDecoPerItemPrepare.cs:PerItemPrepareReason`(22행)과 `src/GpuDecoDrawCollapseRenderer.cs:PrepareDrawCollapse`(159행)는 `t.wrapModeU != TextureWrapMode.Clamp`이면 무조건 거부합니다. 하지만 같은 텍스처를 쓰는 다른 장식 하나가 `tile`을 켜면 텍스처 전체가 `Repeat`가 됩니다.
     - 장식 자체가 반복(`d.tiled`)이 아니면 UV가 `[0, 1]` 안에 있으므로 `t.wrapMode == Repeat`여도 `Texture2DArray`에 복사해 그릴 때 픽셀이 100% 동일합니다. 따라서 **비반복 장식에 대해서는 `t.wrapMode != Clamp` 거부를 즉시 제거**합니다.
     - 실제 반복(`d.tiled == true`) 장식은 `GpuDecoBatch.shader`에서 텍스처 배열 샘플링 전 `uv = frac(uv) * uvScale`을 수행하거나 `SamplerState sampler_LinearRepeat` 분기를 두어 지원합니다.
  2. **`originalSize`(`textureScaleMultiplier`) 및 `cropPivotOffset` 계단 지원:**
     - `GpuDeco.compute:WriteVerticesAt`(236~253행)는 이미 `c0 = _Constants[baseC + 0]`(`scaleMultiplier * textureScaleMultiplier`, `textureScaleMultiplier`)과 `c1.yz`(`croppedPivotOffset`)를 읽어 정점을 계산합니다!
     - 따라서 `FastMove.cs:Run`에서 해당 이벤트 발생 시 `_Constants` 버퍼의 해당 장식 슬롯(8개 float, 32B)만 갱신해 주면 계단 이벤트로 즉시 지원됩니다.
- **예상 이득과 근거:**
  - **사실(`01-MEASUREMENTS.md` 9절):** `7777`의 소유 상한이 **39.4% → 64.5%(+25.1%p)**로 뛰어 손익분기점(57%)을 돌파하고, `HELLO`가 **57.0% → 66.0%**, 116개 맵 합계가 **78.6% → 82.7%**로 올라갑니다.

---

## Q2. 깊이·가시성 변경 (DDONGSSADA 110,581건 재계획 없는 처리 설계)

### 1. 왜 `SetDepth` 때마다 `RebuildDrawCollapse()`를 부르면 안 되는가?
- **코드 근거:** `src/GpuDecoDrawCollapseRenderer.cs:RebuildDrawCollapse`(1060~1084행)는 모든 `CollapseSpan`의 `Mesh`, `GameObject`, `MeshRenderer`를 파괴하고(`Dispose`) 새로 생성하므로 1회 호출에 **약 150~220ms**(`DrawCollapsePolicy.cs:123` 주석 및 `GpuDecoPlayer.cs:567` 주석)가 걸립니다. `DDONGSSADA`의 깊이 이벤트 110,581건에서 이를 호출하면 게임이 멈춥니다.

### 2. 핵심 불변식 발견 (`game/game-excerpts.cs:SetDepth` + `01-MEASUREMENTS.md` 5절 동점 규칙)
- `game/game-excerpts.cs:SetDepth`(629~647행) 및 `docs/00-deco-spec.md` 4절 7번:
  ```csharp
  string sortingLayerName = ((depth >= 0) ? "Bg" : "Default");
  int layer = ((depth >= 0) ? 9 : 7);
  int sortingOrder = -depth;
  ```
- **불변식:**
  1. `SetDepth(depth)`는 오직 `(sortingLayerID, sortingOrder, gameObject.layer)`만 바꾸며, **`Distance(Z)`, `renderQueue`, `Shader ID`, `Material ID`(스프라이트 생성 시 할당된 인스턴스 ID)는 곡 내내 절대 바뀌지 않습니다!**
  2. `01-MEASUREMENTS.md` 5절 실측 동점 규칙에 의해, 같은 `(sortingLayerID, sortingOrder, renderQueue, Distance)` 버킷 안에 모인 장식들 사이의 그리기 순서는 **언제나 `Material ID` 오름차순(초기 정적 순서)**으로 완전히 결정됩니다.
  3. 현재 `src/GpuDeco.compute:EvaluateCollapse`(349~352행)는 이미 간접 매핑 버퍼 `_CollapseMap[slot]`을 지원합니다:
     ```hlsl
     uint item = _CollapseMap[slot];
     if (item >= _Count) { WriteCollapsedAt(outputItem); return; } // 0xFFFFFFFF이면 면적 0 삼각형 출력!
     ```

### 3. 해결 설계: "정적 깊이 버킷 슬롯 풀(Static Depth-Bucket Slot Pool) + `_CollapseMap` O(1) 스왑"
- **무엇을 바꾸는지:**
  1. **맵 열 때(준비 시점):**
     - 맵의 모든 이벤트(`baselineEvents`)를 스캔해 각 장식 $i$가 곡 중에 가질 수 있는 깊이 값 집합 $D(i) = \{ \text{초기 depth}, \text{이벤트 targetDepth들} \}$을 수집합니다.
     - 정적 장식($|D(i)| = 1$)은 기존과 동일하게 고정 슬롯 1개를 배정합니다.
     - 동적 깊이 장식($|D(i)| > 1$)에 대해서는 도달 가능한 각 깊이 버킷 $b = (\text{Layer}(d), -\text{depth}, \text{GameLayer}(d))$마다, 그 버킷 안에서 **`(Distance, MaterialId)` 오름차순 위치에 예비 슬롯 `slot(i, b)`**를 미리 잡아 둡니다.
     - 초기 깊이 $b_0$에 해당하는 `gpuCollapseMapCpu[slot(i, b_0)] = (uint)i`로 켜 두고, 나머지 $b \neq b_0$ 슬롯은 `gpuCollapseMapCpu[slot(i, b)] = 0xFFFFFFFFu`(접힌 투명 삼각형)로 초기화합니다.
  2. **곡 중 `SetDepth(newDepth)` 발생 시 (`FastMove.cs:Run`):**
     - `Mesh`나 `MeshRenderer`를 재생성하지 않고, CPU 배열에서 단 2개의 `uint`만 바꿉니다:
       ```csharp
       gpuCollapseMapCpu[oldSlot] = uint.MaxValue; // 이전 깊이 버킷에서 비활성화
       gpuCollapseMapCpu[newSlot] = (uint)item;    // 새 깊이 버킷의 정확한 동점 순서 위치에서 활성화
       ```
     - 이전 버킷 구간과 새 버킷 구간의 가시 카운트(`gpuSpanVisible`)를 `-1`, `+1` 갱신하고, 프레임 끝에 바뀐 `_CollapseMap` 구간만 `SetData`로 업로드합니다.
  3. **슬롯 풀 상한 초과 시 안전장치:**
     - 만약 특정 장식이 수백 개의 서로 다른 `targetDepth` 값으로 연속 스윕하여 총 예약 슬롯 수 $\sum_i |D(i)|$가 예산(예: `65,536` 슬롯 = 정점 버퍼 약 15MB)을 넘는 장식만 선별적으로 `gpuCollapseTextureFallback`(독립 렌더러)으로 두어 그 장식의 `Renderer.sortingOrder`만 직접 갱신합니다. (`DDONGSSADA`는 타일당 같은 시각에 최대 2,680개가 유한한 깊이 층들 사이를 오가므로 대다수 버킷 풀 안에서 해결됩니다.)
- **예상 이득과 근거:**
  - **사실(`01-MEASUREMENTS.md` 9절):** `DDONGSSADA`의 소유 상한이 **21.0% → 66.7%(+45.7%p)**로 급증합니다.
  - **비용(`추정`):** 재계획(`RebuildDrawCollapse`, ~200ms) **0회**. 이벤트 1건당 CPU 비용은 배열 인덱스 대입 2회(`~20ns`), 타일당 2,680건 동시 발생 프레임에서도 $2,680 \times 20\text{ns} \approx \mathbf{0.054\text{ ms}}$(`추정`)로 끊김이 전혀 없습니다.
- **합격/불합격 판정 측정 기준:**
  - `DDONGSSADA` 전곡 완주 시 `RebuildDrawCollapse` 호출 0회, 30ms 초과 프레임 스파이크 0회, 깊이 변경 직후 정지 프레임 픽셀 차이 `> 3/255` 픽셀 수 = `0`.
- **깨질 수 있는 동작과 방어책:**
  - `depth >= 0`(레이어 9, `Bg`)과 `depth < 0`(레이어 7, `Default`) 사이는 `gameObject.layer`도 바뀌므로(`game/game-excerpts.cs:639`), 버킷 키에 반드시 `gameObject.layer`를 포함해 `CollapseSpan`의 `go.layer`(`GpuDecoDrawCollapseRenderer.cs:1006`)가 카메라 컬링 마스크와 일치하게 해야 합니다.

---

## Q3. 마스크 처리 전략 및 매 디스패치 전수 검사의 이벤트 기반 대체

### 1. 마스크 장식: 원래 방식 유지 vs 직접 그리기(스텐실/별도 텍스처)
- **결론:** **마스크 제공자(`Mask`)와 소비자(`VisibleInsideMask`, `VisibleOutsideMask`)는 원래 방식(Unity `SpriteMask` + `SpriteRenderer`)으로 남기는 것이 맞습니다.** 단, 명세 4절 1번을 반영하여 **"실제로 마스크가 켜지지 않는 무변화 이벤트"**만 G1에서 먼저 걸러내야 합니다.
- **코드 근거:**
  1. `docs/00-deco-spec.md` 1절에서 확인되었듯 `scrController.disableV15Features`는 항상 `true`이므로 렌더 텍스처 경로(`SpriteAlphaMask`)는 죽은 코드이고, 모든 마스크는 **Unity 네이티브 스텐실(`SpriteMask`)**로 동작합니다.
  2. Unity 2D 렌더러는 `SpriteMask.frontSortingOrder = -front + 1`부터 `backSortingOrder = -back` 범위(`00-deco-spec.md` 4절 11번)의 경계에서 스텐실 값을 쓰고 지우는 패스를 자동 삽입합니다.
  3. 이를 `GpuDecoBatch.shader`에서 직접 그리려면 스텐실 참조값·비교 함수(`Stencil Comp Equal / NotEqual`)별로 재질과 `CollapseSpan`을 어차피 전부 쪼개야 하고, 비소유 장식(파티클·타일·텍스트)이 같은 마스크를 쓸 때 Unity의 내부 스텐실 비트 할당과 충돌할 위험이 큽니다.
  4. 반면 `01-MEASUREMENTS.md` 9절에서 보듯 **마스크를 직접 처리하지 않고도 G1~G4만으로 `Arche` 99.2%, `Exit` 92.9%, `DDONGSSADA` 70.5%, `HELLO` 66.0%, `7777` 64.5%, `Windflower` 55.7% (116개 맵 합계 82.7%)**를 소유할 수 있습니다.

### 2. 과거 마스크 이식(3~7 FPS)의 원인인 "매 디스패치 전수 검사"를 이벤트 기반으로 대체하는 방법
- **현재 코드에 남아 있는 폴링 루프 (`src/GpuDecoDrawCollapseRenderer.cs`):**
  1. `DynamicOrderTick()` (608~648행): 8프레임마다 `gpuWatchRows` 전체를 순회하며 `DrawCollapseBarrierActive(row)` (`r.enabled`, `r.gameObject.activeInHierarchy`, `r.forceRenderingOff`, `sprite.color.a`)와 `r.sortingOrder`, `r.transform.position.z`, `r.sortingLayerID`를 네이티브 호출로 읽습니다.
  2. `ValidateDrawCollapse(false)` (1173~1280행): 8프레임마다 `CollapseBarriersToCheck` + `OwnedChecksPerFrame`에 대해 `GpuDecoScene.SortKey`(네이티브 `r.bounds`, `r.sortingOrder`)를 읽습니다.
  - 과거 마스크 이식에서 이 검사를 매 디스패치마다 수천~수만 개 렌더러에 전수 실행했을 때 Mono→C++ 네이티브 브릿지 호출 수십만 번으로 프레임당 140~300ms(3~7 FPS)가 걸렸던 것입니다.
- **이벤트 기반(Push-Only Dirty Queue) 대체 설계:**
  - **코드 근거:** `docs/00-deco-spec.md` 2~3절 및 `src/FastMove.cs:Run`(755~798행), `ImageBlock`(565~608행)을 보면, 곡 중에 비소유 장벽(마스크 포함)의 정렬 키나 활성 상태를 바꿀 수 있는 진입점은 **오직 아래 5곳뿐**입니다:
    1. `scrVisualDecoration.SetDepth` (이미 `GpuDecoPlayer.cs:52`에 후크 설치됨 → `DecoBake.GpuDepthChanged`)
    2. `scrDecoration.SetVisible` (`FastMove.cs:755, 795` 및 Harmony 후크)
    3. `scrVisualDecoration.SetMaskingType` / `SetMaskingTarget` / `SetMaskingDepth` (`FastMove.cs:ImageBlock` 603~607행 및 Harmony 후크)
    4. `scrVisualDecoration.ApplyColor` (알파가 `0 ↔ 양수`로 바뀌어 `DrawCollapseBarrierActive`가 변하는 순간)
    5. 비소유 장식의 Z 위치 변경(얼불춤의 2D 장식 이동 `SetPosition`은 XY만 바꾸므로 Z는 불변; 3D 오브젝트/파티클 장벽만 해당)
  - **무엇을 바꾸는지:**
    - `gpuWatchRows`에 번호 `w`를 부여하고 `HashSet<int> gpuDirtyWatchIndices` 큐를 만듭니다.
    - 위 상태 변경 후크가 호출될 때만 `gpuDirtyWatchIndices.Add(w)`로 더티 인덱스를 푸시합니다.
    - `DynamicOrderTick()`(608행)에서 `for (int w = 0; w < gpuWatchRows.Count; w++)` 전수 루프를 삭제하고, **`if (gpuDirtyWatchIndices.Count == 0) return;`으로 즉시 종료**한 뒤 더티 큐에 들어온 `w`만 검사하여 `ReclassifyBucket`을 호출합니다.
- **예상 이득과 근거:**
  - 상태 변화가 없는 프레임(곡의 95% 이상)에서 장벽 감시 CPU 비용이 **`0.000ms` (`O(1)` 빈 큐 확인)**가 되며, 저소유율 맵에서 비소유 장벽 수천 개가 있어도 감시 비용이 0이 됩니다.
- **합격/불합격 판정 측정 기준:**
  - `AUTOTEST`에서 60프레임마다 기존 전수 검사를 대조 실행했을 때 이벤트 기반 `gpuBucketMembers` 상태와 불일치 `0건`, 전곡 FPS 및 최악 프레임 시간 개선.

---

## Q4. 뽑기 방식 비교 (현행 GpuDeco 298.8 FPS vs 배열 160.7 FPS vs 체인 118.7 FPS)

### 1. 왜 배열 방식(160.7 FPS)과 명목 체인 방식(118.7 FPS)이 GpuDeco(298.8 FPS)보다 느렸는가?

`01-MEASUREMENTS.md` 1절·4절과 `docs/chain.md`, `docs/chain-code-structure.md`, `src/DecoV2NominalChain.cs`, `src/GpuDecoExport.cs`를 대조하면 원인이 명확히 드러납니다.

| 비교 항목 | 현행 `GpuDeco` (298.8 FPS) | `deco-v2` 배열 구간 (160.7 FPS) | `deco-v2` 명목 체인 (118.7 FPS) |
|---|---|---|---|
| **그리기 호출 (Draws)** | **346회** | **345회** | **790회 (2.28배 폭증)** |
| **본 카메라 GPU 시간** | **2.91ms** | **3.47ms** | **7.53ms (2.59배 폭증)** |
| **프레임 시간 (1000/FPS)** | **3.34ms** | **6.22ms (CPU 병목)** | **8.42ms (GPU+드로우콜 병목)** |
| **가시성/유휴 구간 컬링** | `CollapseSpanAlpha`로 유휴 구간 `Renderer.enabled=false` + 알파 0 조기 종료 | 구간 컬링은 동작했으나 CPU 패킷 갱신 과부하 | **`docs/chain-code-structure.md` 6행: CPU 가시성 판정 생략 → 790개 구간 전부 매 프레임 드로우!** |
| **이징(Easing) 계산 위치** | **CPU에서 활성 효과(Source)당 1회** (`AdvanceGpuLiveSources` 654행) | CPU/GPU 슬롯별 처리 | **GPU 스레드마다 무거운 `Ease()` 분기·초월함수 직접 계산** |
| **구간 탐색 방식** | **탐색 0회** (`LiveChannel` 직접 참조) | 패킷/배열 갱신 | **`DecoV2NominalChain.cs:Value`(54행): 체인 끝(`count-1`)에서 역순 선형 탐색 (`while (i > 0 && time < seg.T0) i--`)** |

1. **체인 방식(118.7 FPS)이 무너진 3가지 코드 원인:**
   - **원인 A (`docs/chain-code-structure.md` 6행):** *"CPU는 매 프레임 카메라와 곡 시각만 넘기고 가시성·활성 판정을 하지 않는다."* 이 때문에 알파가 0인 구간까지 포함해 **790개 대표 렌더러가 매 프레임 전부 제출**되어 드로우콜이 `346 → 790`으로 2.3배 늘었습니다.
   - **원인 B (`src/DecoV2NominalChain.cs:Value` 54행):** 체인 끝에서부터 `while (i > 0 && time < seg.T0) i--;`로 **역순 선형 탐색**을 수행합니다. 곡 초·중반에는 미래 구간의 사슬 끝에서부터 수십~수백 번 글로벌 메모리(`_Segments`)를 거꾸로 읽으며 캐시 스래싱이 발생합니다.
   - **원인 C (GPU 이징 중복 계산):** 현행 `GpuDeco`는 타일 하나가 장식 104개를 동시에 움직일 때(`01-MEASUREMENTS.md` 7절 Arche) CPU에서 `ZeroTween.Eval`을 **딱 1번**만 계산해 `_SourceE[slot]`를 공유하지만, 체인 방식은 GPU에서 $104 \times \text{채널 수}$만큼 `Ease()`(삼각함수·지수함수·분기문)를 중복 실행합니다.
2. **배열 방식(160.7 FPS)이 느렸던 원인:**
   - GPU 시간은 `3.47ms`(드로우 345회)로 선방했으나 전체 프레임이 `6.22ms`(160.7 FPS)였던 것은, `Arche` 14초 구간에 쏟아지는 168,316개 애니메이션 기록(`01-MEASUREMENTS.md` 7절)을 **장식×채널 슬롯 단위로 시작·완료 패킷 처리**하면서 CPU 메인 스레드가 약 5~6ms를 썼기 때문입니다.

### 2. 질문하신 "시간순 커서로 바뀐 항목의 현재 구간 번호만 GPU 버퍼에 쓰는 방식"은 현행 GpuDeco보다 나은가?

- **판단:** **단순히 `DecoBakeCore.ActiveEvaluator`(178~221행)처럼 `(장식×채널)` 슬롯 단위로 커서를 전진시키며 GPU에서 구간 이징을 평가하면 배열/체인 방식과 똑같은 함정에 빠져 현행 GpuDeco(298.8 FPS)보다 느려집니다.**
- **그러나 현행 GpuDeco의 장점(`_SourceE` CPU 공유 평가 + `CollapseSpanAlpha` 구간 컬링)을 유지하면서 "시작/완료 시 버퍼 쓰기"만 구간 인덱스 방식으로 결합하면 현행 GpuDeco보다도 더 빨라집니다:**
  1. **현재 `GpuDeco`의 남은 CPU 병목 (`src/GpuDecoExport.cs:CompleteGpuLiveSource` 585~613행):**
     - 현재 `GpuDeco`는 트윈이 **시작할 때**만 `LiveChannel`(16바이트: `start, change, end, sourceIndex`)을 더티 블록으로 업로드하는 것이 아니라, **트윈이 끝날 때(`CompleteGpuLiveSource`)도 모든 대상 슬롯을 돌며 `sourceIndex = -1, start = endVal`로 덮어쓰고 `MarkDirty`를 호출해 16바이트씩 GPU로 재업로드**합니다!
  2. **개선안 (Zero-Completion-Upload LiveChannel):**
     - 트윈이 끝나면 `CompleteGpuLiveSource`(591행)는 이미 `gpuSourceE[src.Slot] = 1f;`를 기록하고 `_SourceE` 버퍼(최대 8,192 float = 32KB)를 프레임당 1회 업로드합니다.
     - GPU 셰이더(`GpuDeco.compute:LiveValue`)에서 `_SourceE[src.Slot] == 1.0f`(또는 완료 비트)이면 `ch.end`를 반환하도록 하고, `src.Slot`을 풀에서 재사용하기 전까지는 **`CompleteGpuLiveSource`에서 `PropagateMasterSlotToSlices` / `MarkDirty`를 부르지 않으면** 트윈 완료 시점의 CPU 루프와 GPU 버퍼 업로드량이 **절반(50%)으로 줄어듭니다!**
  3. **곡 중간 시작과 다시 하기 복원:**
     - **다시 하기(0초 시작):** 모든 슬롯을 초기 상태(`Initial`)로 한 번 리셋하고 `_SourceE` 풀만 비우면 준비된 구간·텍스처를 100% 재사용합니다(`TryReusePlayerPreparation`).
     - **곡 중간 시작(에디터 재생):** `table.Tracks`에 대해 준비된 이진 탐색(`DecoBakeCore.Find`, 288행)을 슬라이스 진입 시 1회 실행해 각 채널의 현재 구간(`Start, Target, End, Source`)을 채우면 `ScrubToTime` 직후 프레임부터 끊김 없이 이어받습니다.

---

## Q5. 안전 복귀 (Exit의 `FastMoveOriginal` 복귀 해결 및 거부된 맵의 끊김 제거)

### 1. 왜 Exit와 DDONGSSADA가 곡 중에 `FastMoveOriginal`로 전체 복귀했는가?

- **코드 근거 (`src/FastMove.cs:Prefix` 131~158행 및 `Orig` 212~244행):**
  ```csharp
  running = true; // 131행: Prefix 진입 시 true로 설정!
  ...
  if (!Take(__instance)) { ... return Orig(__instance); } // 158행
  ```
  - 로그에 찍힌 `original_guard running=True`는 재진입이 아니라, 131행에서 `running = true`를 켠 뒤 **158행에서 `Take(__instance)`가 `false`를 반환해 `Orig(__instance)`로 들어갔기 때문**입니다!
  - `Take(fx)`(`src/FastMove.cs:375~429행`)가 `false`를 반환하는 원인은 4가지입니다:
    1. **384행 `if (CropUsed(fx)) return No(3);`**: 알파 3.4에서 추가된 `cropPivotOffsetUsed`가 켜진 효과는 `FastMove`가 무조건 거부해 `Orig`로 보냅니다!
    2. **385행 `if (AnyImg(fx) && !ImgSafe(fx)) return No(3);`**: `ImgSafe`(620행)에서 `imageFilenameUsed`의 파일 이름이 `customSprites` 사전에 없거나 빈 문자열이 아닌 미등록 이름일 때 `Orig`로 보냅니다.
    3. **380행 `if (durRef(fx) > 0f && !CanAnim(fx)) return No(0);`**: `DecoAnim`이 지원하지 않는 이징(`INTERNAL_Custom`, `Unset` 등)이 있는 길이 > 0 효과.
    4. **428행 `return ParallaxOk(fx) || No(0);`**: `parallaxUsed`가 켜진 효과인데 대상 장식 중 `parallax == null`인 장식이 섞여 있을 때.
  - **결정적 문제 (`src/FastMove.cs:Orig` 235행):**
    위 사유로 `Take(fx)`가 거부된 효과가 **GPU가 소유하지 않은 비소유 장식(예: 마스크 장식이나 이미 제외된 장식)만 건드리는 효과여도**, `Orig(fx)` 235행은 대상 장식을 확인하지 않고 무조건 `DecoBake.RecoverGpuLive("FastMoveOriginal", false)`를 호출해 **맵 전체의 GpuDeco를 꺼버립니다!**

### 2. 복귀를 전체가 아닌 장식 단위로 줄이는 설계 (G1-5)
- **무엇을 바꾸는지:**
  1. **`FastMove.cs`에서 `CropUsed(fx)` 직접 지원:**
     - `game/game-excerpts.cs:StartEffect`(514~518행)와 똑같이 `FastMove.cs:ImageBlock`(601행 뒤)에 아래 2줄을 추가하고, `Take`(384행)의 `if (CropUsed(fx)) return No(3);`를 제거합니다:
       ```csharp
       if (CropUsed(fx)) {
           vis.croppedPivotOffset = fx.targetCropPivotOffset / 100f;
           vis.SetPosition(vis.pivotPosVec, vis.pivotOffsetVec);
       }
       ```
  2. **`FastMove.Orig(fx)`에서 비소유 대상 무시 + 소유 대상 개별 강등(`EvictSingleGpuOwnedDecoration`):**
     - `Orig(fx)` 진입 시 `fx`의 대상 장식(`decManager.GetTaggedDecorations(fx.targetTags)`) 중 `DecoBake.GpuLiveOwns(dec)`인 장식이 있는지 먼저 확인합니다.
     - **소유 장식이 0개이면:** `RecoverGpuLive`를 절대 부르지 않고 그대로 `return true;`하여 원본 `StartEffect`가 비소유 장식만 처리하게 합니다.
     - **소유 장식이 포함되어 있으면:** 전체 복귀 대신 해당 소유 장식 `masterIdx`만 `EvictSingleGpuOwnedDecoration(masterIdx)`로 개별 강등합니다:
       - 해당 장식의 16채널 현재값을 `EvalMasterSlotCurrent`로 읽어 `DecoAnim.BakeWrite`로 CPU `scrVisualDecoration`에 기록하고 진행 중인 트윈을 CPU `DecoAnim`으로 이관합니다.
       - `gpuCollapseMapCpu[gpuCollapseSlotOfItem[item]] = uint.MaxValue`로 설정해 GPU 통합 구간에서 즉시 숨기고, 원본 `SpriteRenderer`의 `enabled = true`, `forceRenderingOff = false`를 복원한 뒤 `gpuCollapseBarriers`에 추가합니다.

### 3. 왜 준비가 거부된 맵(HELLO 0→20번, DDONGSSADA 1→9번)에서 곡 중 끊김이 폭증했는가? (G1-4)

`src/GpuDecoPlayer.cs:Prepared`(74~124행), `PreparePlayer`(491~605행), `src/GpuDecoExport.cs:PrepareGpuLive`(825~1182행)를 추적하면 **정확히 3가지 원인**이 겹쳐 있습니다:

1. **원인 1 (`GpuDecoPlayer.cs:Prepared` 109행 vs 121행 — 예외 발생 시 GC 청소 누락):**
   ```csharp
   long grown = (GC.GetTotalMemory(false) >> 20) - heapBeforePrep;
   lifetime.Ready();
   GcControl.CleanAfterPrepare(grown); // <-- 109행: 성공했을 때만 실행됨!
   ...
   catch (Exception e)
   {
       Skip(e.Message); DecoBake.ReleasePlayerLevel(); cleanupPending=false; // <-- 121행: GcControl.CleanAfterPrepare 누락!
   }
   ```
   - `HELLO 2026`(이벤트 2,867개)과 `DDONGSSADA`(이벤트 **146,540개**)에서 `PreparePlayer`는 먼저 `BeginPrepCollection`과 `DecoBakeCore.Compile`(517~524행)을 끝까지 돌려 **수십 MB의 관리 힙(`Event[]`, `List<Segment>`, `Segment[][]`)을 할당**합니다.
   - 그 직후 548행(`table.Supported < 64`, `HELLO` 38개) 또는 594행(`gpuLiveOwnedIndices.Length < 64`, `Windflower` 33개) 또는 시제품 1차의 `PrepareDrawCollapse`(158행 `mip/wrap` 예외)에서 예외가 던져지면, **109행의 `GcControl.CleanAfterPrepare(grown)`을 건너뛰고 `catch` 블록으로 빠집니다!**
   - `ReleasePlayerLevel()`이 참조만 `null`로 밀어버린 수십 MB의 가비지가 청소되지 않은 채 곡이 시작되면서, 플레이 도중 Mono Boehm GC가 9~20번 터진 것입니다!
2. **원인 2 (`GpuDecoExport.cs:PrepareGpuLive` 840~841행 — 거부될 맵에서 `LazySprite`와 `InvisibleSkip` 지연분 강제 해제):**
   ```csharp
   LazySprite.FlushAll();PrepMark("스프라이트 지연분");
   InvisibleSkip.ApplyAllLazy();PrepMark("안 보이는 지연분");
   ```
   - `PrepareGpuLive` 맨 첫머리(840~841행)에서 아직 이 맵이 `AcceptGpuOwnedCount`(1026행)나 `PrepareDrawCollapse`(1154행)를 통과할지 모르는 상태에서 **`LazySprite.FlushAll()`과 `InvisibleSkip.ApplyAllLazy()`를 먼저 호출해 기존 모드의 CPU 최적화 상태를 날려버립니다!**
3. **원인 3 (`GpuDecoPlayer.cs:ReleasePlayerLevel` 707~720행 — 거부된 뒤에도 `DecoBake.enabled = true` 유지):**
   - 맵이 거부되어 `lifetime.Fail()`이 된 뒤에도 `DecoBake.enabled`가 `true`로 남아 있어, 곡 중 매 이벤트마다 `FastMove.cs:Prefix`(120행 `DecoBake.ObserveStart`)와 `FastMove.cs:Take`(390행 `DecoBake.TryPacketTargets`), `FastMove.cs:Run`(167행 `BeginGpuSource` / `EndGpuSource`, 723행 `SkipGpuOrAdopt`)가 불필요하게 호출됩니다.
- **해결책 (G1-4):**
  1. **수집 전 고속 사전 판정(`CanAdmitMapFast`):** `PreparePlayer`(514행)에서 무거운 `BeginPrepCollection` / `Task.Run(Compile)`을 돌리기 전에, `initial`과 `scrVfxPlus.instance.effects`를 배열 할당 없이 1회 스캔해 지원 가능 장식 상한이 `MinimumOwned`(64개 및 소유율 45%) 미만이면 **0바이트 할당으로 즉시 `Skip`**합니다.
  2. **`LazySprite.FlushAll()` / `InvisibleSkip.ApplyAllLazy()` 위치 이동:** `PrepareGpuLive` 840~841행에서 빼고, **모든 준비 검사와 `AcceptGpuOwnedCount`가 통과한 뒤(또는 소유 장식에 대해서만)** 실행합니다.
  3. **`Prepared`의 `catch` 블록에서도 `GcControl.CleanAfterPrepare(grown)` 강제 호출** 및 거부된 라운드 동안 `DecoBake` 후크 단락(`if (lifetime.Blocked) return`).
- **합격/불합격 판정 측정 기준:**
  - 준비가 거부된 맵(`HELLO`, `Windflower` 등)에서 곡 중 끊김 횟수가 기존 후보(GpuDeco 끔)와 동일(`HELLO` 20 → 0번, `DDONGSSADA` 9 → 1번 이하).

---

## Q6. 칠하는 양 (Overdraw / Fill-Rate 절감의 현실성과 픽셀 오차 0 기법)

### 1. 앞쪽 불투명 영역 깊이 기록(Front-to-Back + Early-Z)이 비현실적인 이유
- **코드 근거 (`src/GpuDecoBatch.shader` 14행, 85행 및 `01-MEASUREMENTS.md` 6절):**
  1. `GpuDecoBatch.shader`는 `ZWrite Off`, `ZTest Always`, `Blend [_SrcBlend] [_DstBlend]`(기본 프리멀티플라이드 알파 `One OneMinusSrcAlpha`)로 뒤에서 앞으로(Back-to-Front) 블렌딩합니다.
  2. 앞쪽부터 그리며 `ZWrite On`을 쓰려면 픽셀이 완전 불투명(`color.a == 1.0`)이어야 하는데, BC3(DXT5) 압축 텍스처의 쌍선형 필터링(`sampler_LinearClamp`) 경계 픽셀은 알파가 `0 < a < 1`입니다. `clip(a - 0.999)`로 자르면 경계선 블렌딩 순서가 뒤집혀 **`> 3/255` 픽셀 오차 0 조건을 즉시 위반**합니다(`01-MEASUREMENTS.md` 4절에서 투명 여백 트림이 정지 프레임 최대 `14/255` 오차를 낸 것과 같은 맥락).
  3. 또한 특수 혼합 모드(`BlendModeEffect`)나 스텐실(`SpriteMask`)과도 충돌합니다.

### 2. 이 파이프라인에서 현실적이고 픽셀 오차가 0인 기법: "컴퓨트 셰이더 전면 불투명 가리개(Occluder AABB) 컬링"
- **왜 현실적인가 (`01-MEASUREMENTS.md` 6절 실측 근거):**
  - `01-MEASUREMENTS.md` 6절 프레임 덤프를 보면:
    - `Arche 264초`: 사각형 전체 `84.1배` → 알파>0 `47.5배` → **불투명 가리개 뒤 남음 `15.6배` (-67.2% 감소!)**
    - `HELLO 140초`: 알파>0 `25.5배` → **불투명 가리개 뒤 남음 `12.3배` (-51.8% 감소!)**
    - `Exit 95초`: 알파>0 `12.2배` → **불투명 가리개 뒤 남음 `7.0배` (-42.6% 감소!)**
  - 커스텀 맵에서 오버드로가 40~80배로 치솟는 주원인은 **화면을 덮는 대형 불투명 배경/가리개 스프라이트 앞(그리기 순서상 뒤)에 깔린 수천 개의 이전 연출 장식들이 아직 `alpha > 0`인 채로 가려져 있는 것**입니다.
- **무엇을 바꾸는지 (`src/GpuDeco.compute:WriteVerticesAt` 310~325행 확장):**
  - 현재 `GpuDeco.compute:WriteVerticesAt`(310~325행)는 이미 화면 밖 쿼드를 `WriteCollapsedAt(outputItem)`(정점 4개를 `0`으로 써서 면적 0 삼각형으로 만듦 → 래스터라이저가 픽셀 0개로 즉시 폐기)으로 접고 있습니다:
    ```hlsl
    if (_CameraClip.z > 0.0f && (maxX < -_CameraClip.z || minX > _CameraClip.z || maxY < -_CameraClip.w || minY > _CameraClip.w))
    {
        WriteCollapsedAt(outputItem);
        return;
    }
    ```
  - 준비 시점에 텍스처 중앙 영역(`TightMarginTexels = 4` 안쪽)의 모든 텍셀이 `alpha == 255`인 스프라이트를 `IsSolidOccluder`로 태그합니다.
  - 매 프레임(또는 컴퓨트 1패스)에서 현재 `v[10] * v[11] >= 1.0`(완전 불투명)이고 회전이 $0^\circ/90^\circ/180^\circ/270^\circ$이며 기본 혼합 모드인 가장 앞쪽(그리기 순서 `outputItem`이 큰) 대형 가리개 최대 4개의 **안쪽 월드 AABB `(occMinX, occMinY, occMaxX, occMaxY)`와 그리기 순서 `occOrder`**를 유니폼(`_Occluders[4]`)으로 넘깁니다.
  - `WriteVerticesAt`에서 `outputItem < occOrder`(가리개보다 먼저 그려져 가리개 밑에 깔리는 장식)이고 쿼드의 4개 정점 `(minX, minY, maxX, maxY)`가 가리개 안쪽 AABB에 **완전히 포함**되면 `WriteCollapsedAt(outputItem)`으로 접습니다!
- **예상 이득과 근거 (`추정`, `01-MEASUREMENTS.md` 6절 회귀식 적용):**
  - `01-MEASUREMENTS.md` 6절 오프라인 벤치 회귀식:
    $$\text{상대 GPU 시간} \approx 0.133 + 0.0112 \times \text{칠하는 배수}$$
  - `Arche 264초`에서 불투명 가리개 컬링으로 칠하는 배수가 `47.5배 → 15.6배`로 줄어들 때(보수적으로 대형 축정렬 가리개만 적용해 `47.5배 → 25배`로만 줄어도):
    $$\text{상대 GPU 시간 비율} = \frac{0.133 + 0.0112 \times 25}{0.133 + 0.0112 \times 47.5} = \frac{0.413}{0.665} \approx \mathbf{0.621\ (-37.9\%\text{ 감소, 추정})}$$
  - 본 카메라 GPU 시간 `2.91ms`가 **약 `1.81ms`로 `-1.10ms` 단축(`추정`)**됩니다.
- **합격/불합격 판정 측정 기준:**
  - `Arche 264초`, `HELLO 140초`, `Exit 95초` 정지 프레임에서 채널 차이 `> 3/255` 픽셀 수 = `0`, 본 카메라 GPU 시간 유의미한 감소 확인.
- **깨질 수 있는 동작과 방어책:**
  - 가리개 스프라이트 가장자리의 쌍선형 필터링 반투명 픽셀(1~2텍셀)이 비치는 문제 → 가리개 AABB를 월드 공간에서 `4 텍셀` 안쪽으로 축소(Inset)하여, 100% 불투명한 내부 영역에 완전히 들어간 뒤쪽 쿼드만 접으면 픽셀 오차가 수학적으로 **0**입니다.

---

## 요약: 단계별 실행 체크리스트 (`01-implementation-order.md` 보완판)

| 단계 | 작업 내용 (`파일:함수`) | 목표 소유 상한 (116개 맵) | 합격 기준 |
|---|---|---|---|
| **G1 (보완)** | 1. 무변화 이벤트 필드 제외 (`DecoBake.cs:Reasons`, `Compile` — 단, `visible=false`일 때 `SetMaskingType(None)` 부수효과 보존)<br>2. `visibleUsed` 분리 및 계단 가시성 반영 (`DecoBake.cs:329`, `FastMove.cs:723`, `GpuDecoExport.cs:HiddenAlpha`)<br>3. `PreparePlayer` 사전 고속 판정(`owned >= 64` & `소유율 >= 45%` & `구간당 평균 >= 3개`) + `Prepared` `catch` 블록 `GcControl.CleanAfterPrepare` 추가 + `LazySprite.FlushAll` 지연<br>4. `FastMove.Orig` 비소유 무시 + 소유 장식 개별 강등 + `CropUsed` 직접 처리<br>5. `DecoBake.Core.cs:368` 시차(채널 12·13) 끝점 수식 통일<br>6. *(권장 편입)* 이미 구현된 `lockRotation`/`lockScale`/`dontAlterX/Y` 거부 해제 (`DecoBake.cs:253, 405`) | **69.6%** *(잠금 해제 편입 시 **~72%**)* | - `Exit`, `Windflower`(잠금 편입 시 `HELLO` 포함) 전체 복귀 `0회`<br>- 거부된 맵 끊김 증가 `0회`<br>- 정지 프레임 3지점 + 가시성 전환 + 다시 하기 뒤 `>3/255` 픽셀 `0개`<br>- `Exit` ABBA FPS 상승 |
| **G2** | `followPlanet`, `Camera`·`CameraAspect` 배치 유니폼 추가 (`GpuDeco.compute:WriteVerticesAt`, `docs/00-deco-spec.md` 2절 수식 및 일시정지 행성 위치 규칙 재현) | **73.1%** | `HELLO` 56.9%+, `Exit` 89.8%+ 달성 및 픽셀 오차 `0개` |
| **G3** | 비반복 장식의 `t.wrapMode != Clamp` 거부 해제(`GpuDecoPerItemPrepare.cs:22`) + 반복(`tile`) 셰이더 UV 래핑 + `originalSize`·`cropPivotOffset` `_Constants` 부분 갱신 | **82.7%** (단, 깊이 제외 시 ~77%) | `7777` 소유율 64.5% 달성 및 FPS 역전(373→346) 해소 |
| **G4** | 동적 깊이(`SetDepth`) 정적 버킷 슬롯 풀 + `_CollapseMap` `uint` 스왑 (`GpuDecoDrawCollapseRenderer.cs`) + 장벽 감시 `DynamicOrderTick` 이벤트 푸시화 | **82.7%** (`DDONGSSADA` **66.7%**) | `DDONGSSADA` 11만 건 깊이 변경에서 재계획 `0회`, 끊김 `0회`, 픽셀 오차 `0개` |
| **G5 / Q6** | `GpuDeco.compute:WriteVerticesAt` 전면 불투명 가리개(Occluder AABB) 컬링 추가 | **82.7%~86.2%** | `Arche 264초`, `HELLO 140초` 본 카메라 GPU 시간 단축 및 픽셀 오차 `0개` |
