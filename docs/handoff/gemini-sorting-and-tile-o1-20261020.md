# StutterFix 설계 제안: (1) 동일 정렬값 내 재질 ID 정렬 우회 & (2) 8,000+ 타일 효과 O(1) 시작 설계

아래 두 가지 설계는 **화면(픽셀)·판정·입력·그리기 순서 100% 동일성**을 유지하는 전제로 작성된 구현 명세다. 현재 작업 중인 장식 트레이싱/그룹 메시 파이프라인 및 `FloorAnim` / `TileCull` 코드와 대조해 검증·적용해 달라.

---

## 1. 같은 정렬값(`sortingLayer` + `sortingOrder` + `Z`) 안에서 재질 ID 정렬 우회 설계

### 원인 요약 (`CLAUDE.md` 6x6 반례 30/36 실패 원인)
Unity C++ `ForwardShaderRenderLoop`는 반투명 `SpriteRenderer` 정렬 시 `(sortingLayerID, sortingOrder, distanceZ)`가 모두 같으면 **`Material Sort Key`(재질 생성/InstanceID 순서)** 로 tie-break를 수행한다.
따라서 원본 순서가 `R0(상태A, Mat#100) → R1(상태B, Mat#101) → R2(상태A, Mat#102)`일 때 전역 재질 풀(`SharedMat_A`, `SharedMat_B`)로 묶으면 `R0(SharedMat_A) → R2(SharedMat_A) → R1(SharedMat_B)`로 묶여 **중간에 낀 `R1`과 `R2`의 그리기 순서가 역전**된다.

이를 **픽셀 차이 0으로 우회하는 3가지 방법**은 다음과 같다.

| 방식 | 적용 대상 | 핵심 원리 | 픽셀·정렬 동일성 보장 조건 |
| :--- | :--- | :--- | :--- |
| **방식 1: 연속 구간(Run-Length) 재질 공유** | `SpriteRenderer` 유지 시 (`MatReuse` 대체) | 전역 18개 공유 대신, 같은 `(layer, order)` 버킷 안에서 **원본 그리기 순서(`원본 Material InstanceID` → `Renderer 순번`)로 정렬한 뒤 인접한 동일 재질 상태 구간(Run)끼리만** 공유 재질 1개를 부여 | 각 Run의 공유 재질을 **Run 순서대로 생성(`new Material` / `Instantiate`)** 해 `InstanceID` 단조 증가(`Mat_Run0 < Mat_Run1 < Mat_Run2`)를 보장하면, 서로 떨어진 `A-B-A`가 `A-A-B`로 합쳐지지 않으면서 연속된 수백 개 안개·광선은 1드로콜로 배칭됨 |
| **방식 2: 그룹 메시(Group Mesh) 정점 순서 고정** | 현재 실험 중인 트레이싱 + 그룹 메시 | Unity `SpriteRenderer` 정렬기를 거치지 않고, C#에서 원래 정렬 규칙(`layer` → `order` → `원본 재질 ID` → `생성 순번`) 그대로 정렬된 연속 Run마다 하나의 `Mesh` 정점/인덱스 버퍼에 순서대로 기록 | 단일 `Mesh` 내부에서는 인덱스 버퍼(`0,1,2, 2,3,0 ...`) 순서대로 GPU가 무조건 그리므로 재질 ID 정렬 문제가 원천 소멸함 |
| **방식 3: `sortingOrder` 경계 재번호 매기기** | `SpriteRenderer` 유지 + 전역 재질 공유 시 | Unity Sprite 동적 배칭은 **`sortingOrder`가 달라도 정렬 후 인접한 두 렌더러의 재질·텍스처가 같으면 배칭을 끊지 않음**(`sortingOrder`는 정렬 키일 뿐 배칭 분리 조건이 아님) | 같은 `sortingLayer` 내에서 원본 최종 정렬 순서대로 줄을 세운 뒤, 재질 상태가 바뀌는 경계(Run)마다 `sortingOrder`를 `-32768..32767` 범위에서 단조 증가하도록 재할당하면 `sortingOrder` 단계에서 순서가 확정되어 `Material Sort Key` tie-breaker까지 내려가지 않음 |

### 권장 선택
* **트레이싱 + 그룹 메시 경로**로 갈 경우 **방식 2**를 기본으로 삼아, 같은 `(sortingLayer, sortingOrder)` 내에서도 **원본 재질 상태 + 스프라이트가 연속된 Run 단위**로 그룹 메시를 묶는다.
* 그룹 메시에서 제외되는 일반 `SpriteRenderer` 장식에 재질 공유를 켤 때는 전역 공유가 아닌 **방식 1(Run-Length 재질 공유 + Run 순서대로 재질 인스턴스 발급)** 을 사용해 6x6 정렬 fixture 36/36 통과 여부를 먼저 검증한다.

---

## 2. 8,000개 넘는 타일 효과(`ffxMoveFloorPlus` / `MoveTrack`) 시작 스파이크 O(1) 설계

### 현재 병목 (`FloorAnim.cs:146..318`)
* `Lost Requiem` 531.8초(8,171타일), `HELLO` 125.6초(8,933타일), `Arche` 타일 990/2467/3703(2,485~3,704타일) 등 대형 `MoveTrack` 시작 시 `FloorAnim.Run`이 `start..end` 타일을 즉시 순회하며 타일마다 `Prev` 사전 조회 + `tt.position` / `tt.localScale` 네이티브 읽기 + `Rec` 생성/갱신을 수행해 **효과 1개 시작에 22~26ms**가 소요된다.
* 그러나 하나의 `ffxMoveFloorPlus` 효과 안에 속한 모든 타일은 **`Dur`, `Ease`, `Pos`( 진행 시각), 목표 오프셋(`c.Pos`, `c.Rot`, `c.Scale`, `c.Opacity`)이 100% 동일**하다:
  * 목표 위치: `target.startPos + c.Pos` (`FloorAnim.cs:254`)
  * 목표 회전: `(target.startRot + c.Rot).z` (`FloorAnim.cs:255`)
  * 목표 크기: `c.Scale` (`FloorAnim.cs:296, 307`)
  * 목표 불투명도: `c.Opacity` (`FloorAnim.cs:316`)

### 핵심 설계: 구간 기술자(`RangeEffect`) + 64타일 청크(`FloorChunk`) 지연 평가

#### 1) 자료구조 (타일별 `Rec` 생성 제거 → 효과당 `RangeEffect` 1개)
* 전체 `listFloors`(`N`개)를 64타일 단위 청크(`chunkCount = (N + 63) >> 6`, 8,192타일 기준 **128개 청크**)로 나눈다.
* `ffxMoveFloorPlus.StartEffect` 발동 시(`step == 1`이고 `freeroamArea`가 없는 일반 구간, 또는 `freeroamArea` 타일만 별도 처리):
  1. 8,000개 타일을 돌지 않고 **`RangeEffect` 구조체/객체 1개**만 할당한다 (`id, start, end, dur, ease, over, period, posOffset, rotOffset, scaleTarget, opaTarget, channelMask, elapsed, easeFactor`).
  2. 매 프레임 `UpdatePrefix`에서는 활성 `RangeEffect`마다 이징 비율 `k = ZeroTween.Eval(e.Ease, e.Elapsed, e.Dur, e.Over, e.Period)`를 **프레임당 딱 1번**만 계산한다.
  3. 시작 프레임에는 `[start .. end]`가 완전히 덮는 내부 청크(`c0 .. c1`, 최대 128개)에 `chunk.PushEffect(effectId)`만 기록한다 (**8,000타일 루프 대신 128개 청크 정수 기록만 수행하므로 시작 비용 < 0.02ms, 사실상 O(1)**). 양 끝 경계 청크(최대 2개, 128타일 미만)만 개별 타일 스탬프를 찍는다.

#### 2) 중간 덮어쓰기(Kill & Overwrite) 시 시작값(`FStart`) 100% 비트 동일성 보장
* 원래 코드에서 새 `MoveTrack`($E_2$)이 시각 $t_0$에 이전 `MoveTrack`($E_1$)을 끊고 시작할 때:
  * 만약 $E_1$이 `dur > 0`으로 진행 중이었다면, 원래 코드는 `Prev`에서 `Complete(r)`(`Kill(true)`)를 호출한다!
  * **매우 중요한 사실 (`FloorAnim.cs:238, 250`, DOTween `Kill(true)` 동작)**:
    * `ffxMoveFloorPlus`의 `Prev`는 `old.Kill(true)`(`complete = true`)를 부른다!
    * 즉, 이전 트윈 $E_1$이 중간에 끊기더라도 **중간 위치에서 멈추는 것이 아니라 $E_1$의 최종 목표값(`FEnd` / `VEnd`)으로 즉시 완주(Complete)한 뒤**, 그 최종 목표값을 새 트윈 $E_2$의 시작값(`FStart`)으로 삼는다!
    * 검증 근거: `FloorAnim.cs:238` `if (r.Running) { Killed++; Complete(r); }` 및 `250행` `if (old.active) old.Kill(true);`.
  * **이 성질 덕분에 O(1)화가 훨씬 단순하고 완벽해진다**:
    * 타일 $i$의 어떤 채널(PX, PY, ROT, SX, SY, OPA)이든, 새 효과 $E_2$가 시작되는 순간의 시작값(`FStart`)은 **직전 효과 $E_1$의 진행률과 무관하게 무조건 직전 효과 $E_1$의 목표값(`target.startPos + E1.Pos` 등)** 이다!
    * 따라서 각 청크(또는 타일)는 체인을 길게 들고 있을 필요조차 없이, 채널별로 **[직전 확정 오프셋(`prevOffset`) + 현재 진행 중인 `RangeEffect` ID(`currEffectId`)] 딱 두 값**만 기억하면 된다!
    * 새 효과 $E_2$가 청크에 들어올 때: 만약 그 청크에 이전 효과 $E_1$이 걸려 있었다면 `prevOffset = E1.TargetOffset; currEffectId = E2.Id;`로 덮어쓰기만 하면 끝난다(완전한 $O(1)$, 체인 깊이 항상 1).

#### 3) `TileCull`(화면 밖 타일 컬링)과 연동한 가시 청크 전용 갱신
* 각 64타일 청크마다 타일들의 초기 바운드 합집합(`baseBounds`)을 맵 로드 시 1회 구해 둔다.
* 청크의 현재 월드 바운드는 `baseBounds`를 현재 활성 효과의 위치 오프셋(`lerp(prevPosOffset, currPosOffset, k)`)만큼 평행이동한 사각형이다.
* 매 프레임 `FloorAnim.UpdatePrefix`에서:
  * **가시 청크(현재 카메라 뷰 사각형과 바운드가 겹치는 청크 + 플레이어 현재 타일 `currFloor.seqID` 주변 청크)**: 청크 안의 64개 타일에 대해 `FStart = target.startPos + prevOffset`, `FEnd = target.startPos + currOffset`과 이미 계산된 `k`로 `Transform` / `opacity`를 갱신한다.
  * **비가시 청크(카메라 화면 밖 청크)**: 매 프레임 `Transform`을 건드리지 않고 건너뛴다 (`dirty = true`만 유지).
  * **동기화 지점(Sync Hook)**:
    1. 카메라가 이동해 비가시 청크가 가시 범위로 들어오는 순간,
    2. 또는 `ffxFloorAppearPlus` / `ffxFloorDisappearPlus` / `Kill` / `ScrubToTime` / `RealOverlap` 등 외부 코드가 특정 타일의 `Transform`이나 `opacity`를 건드리기 직전(`FlushPrefix`),
    해당 청크(또는 타일)를 현재 `k`로 즉시 반영(Materialize)한다.

### 기대 효과 및 검증 방법
* **시작 스파이크**: 8,000~14,721타일 `MoveTrack` 시작 시 루프 횟수가 `8,933타일 × 네이티브 호출` → `~140개 청크 정수 대입`으로 줄어들어 **22~26ms 스파이크가 0.05ms 이하로 소멸**한다.
* **매 프레임 갱신 비용**: 이징 계산이 타일당 1회에서 `RangeEffect`당 1회로 줄고, 화면 밖 청크의 `Transform` 쓰기가 생략된다.
* **동일성 검증**: 기존 `FloorAnim`의 1/64 샘플 검증(`MaybeShadow`)과 동일하게, 임의 타일 표본 및 가시 타일 전수에 대해 기존 `FloorAnim` 결과와 매 프레임 `position` / `eulerAngles` / `localScale` / `opacity`를 비트 단위로 비교해 불일치 0건을 확인한다.
