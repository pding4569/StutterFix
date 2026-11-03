# 얼불춤 이미지 장식 명세 (게임 코드 기준)

2026-11-03. 게임 디컴파일을 끝까지 읽고 정리했다. 읽은 파일: `scrDecoration`, `scrVisualDecoration`, `scrParallax`, `scrDecorationManager`, `ffxMoveDecorationsPlus`, `SpriteAlphaMaskUtils`, `ffxPlusBase`·`scrVfxPlus`의 실행 부분. 아래에서 "미확인"이 아닌 동작은 이 코드에 근거한다. 성능과 픽셀 동일성은 이 문서로 증명되지 않는다.

## 1. 전제

- `scrController.disableV15Features`는 필드 초기값 `true`이고 게임 어디서도 쓰지 않는다. 맵 파일의 같은 이름 값은 `LevelData` 쪽 필드일 뿐이다. 그래서 `UpdateShader`의 메시 경로와 `SpriteAlphaMask` 렌더 텍스처 경로는 **죽은 코드**다.
- 따라서 이미지 장식은 언제나 `SpriteRenderer`로 그린다. 마스크는 언제나 유니티 `SpriteMask`(스텐실)다.
- 레벨 설정 `stickToFloors`는 타일 설정이고 장식의 `stickToFloor`와 무관하다.
- 효과 실행: 맵을 열 때 `startTime - startEffectOffset`, `floor.seqID` 순으로 정렬. `scrVfxPlus.Update`의 커서가 곡 시간까지 전진하며 `StartEffect` 호출. 시작 시각은 `entryTime + 각도 오프셋 / π × 60 / (bpm × speed)`.

## 2. 매 프레임 최종 값

`scrDecorationManager.LateUpdate`가 모든 장식에 `LogicUpdate`를 부르고, `GetVisible()`이 참일 때만 `UpdatePosition`이 돈다.

`GetVisible = rendererEnabled && (alpha != 0 || isMask || (hitbox && !hitOnce))`

**피벗 위치**

```
P0 = pivotPosVec
   + (멈춤 아님 && followPlanet ? 행성 위치 : 0)
   + (stickToFloor ? parentFloor 위치 - startPos : 0)

일반:    W = (카메라 위치 - P0) * 시차배율 + P0
화면 고정(Camera, CameraAspect):
         screenRel = (pivotPosVec + pivotOffsetVec) / 20 + 0.5   (CameraAspect는 x에 화면높이/화면너비를 먼저 곱함)
         W = camera.ScreenToWorldPoint(pixelRect.xy + pixelRect.wh * screenRel)

최종 = W + parallaxOffset * camScale / textureScaleMultiplier
```

**회전**: `startRot + rotAngle + (stickToFloor ? 타일 z각 : lockRotation ? 카메라 z각 : 0)`

**크기**: `scaleVec * camScale * (stickToFloor ? 타일 localScale : 1)`

- `trueScale = scaleMultiplier * textureScaleMultiplier`
- `camScale = (lockScale ? orthographicSize * 0.2 / (camZoom / 100) : 1) * trueScale`

**자식 위치**: `(pivotOffsetVec + croppedPivotOffset) / trueScale`

**색**: `RGB = color.rgb`, `A = color.a * opacity * (stickToFloor ? 타일 opacity : 1)`

**프레임 입력은 이것뿐이다**: 카메라 위치·z각·orthographicSize, 행성 위치, (stickToFloor일 때) 부모 타일의 위치·z각·크기·opacity. 나머지는 곡 시간만의 함수다.

## 3. 이벤트 필드 분류 (`ffxMoveDecorationsPlus`)

| 필드 | 게임 동작 | 값 종류 | 분류 |
|---|---|---|---|
| positionOffset | X, Y 트윈 | 연속 | 표 (지원) |
| pivotOffset | X, Y 트윈 | 연속 | 표 (지원) |
| rotationOffset | 트윈 | 연속 | 표 (지원) |
| scale | X, Y 트윈, 축 제한 | 연속 | 표 (지원) |
| color, opacity | 트윈 | 연속 | 표 (지원) |
| parallax, parallaxOffset | 트윈 | 연속 | 표 (지원) |
| decorationImage | 즉시 스프라이트 교체 | 계단 | 표 (이미지만일 때 지원) |
| visible | `SetVisible` 즉시 | 계단(1비트) | **표로 가능, 지금은 거부** |
| depth | 정렬 층·순서 즉시 | 계단, 정렬 영향 | 표 + 구간 재배치, 어려움 |
| relativeTo | `SetPlacementType` 즉시 | 계단 | Global·LastPosition 지원, 나머지 프레임 값 |
| originalSize, cropPivotOffset | 즉시 | 계단 | 표로 가능, 지금은 거부 |
| imageSmoothing | 재질 키워드 변경 | 계단 | 재질 구분 필요 |
| maskingType, Target, 깊이 범위 | 즉시 | 계단, 스텐실 영향 | 원래 방식 또는 직접 처리 |
| 장식의 `tile`(반복) | 재질 값 + 텍스처 랩 | 초기값 | 텍스처 공유 규칙 주의 |
| 장식의 `blendMode` | `BlendModeEffect`, 화면 복사 셰이더 | 초기값 | 원래 방식 |

## 4. 놓치기 쉬운 동작

1. **`SetMaskingType`이 숨긴 장식을 다시 켠다.** `spriteRenderer.enabled = !isMask()`로 쓰고 `visible`을 보지 않는다. 한 이벤트 안에서 `visible` 다음에 마스크가 실행되므로 마스크가 이긴다. 이때 `rendererEnabled`는 거짓이라 `LateUpdate` 위치 갱신은 멈춘 채 그려진다. 그래서 표에는 "그리기 켜짐"과 "rendererEnabled"를 따로 둬야 한다.
2. **`LastPosition`의 끝값은 끊긴 순간의 현재값 기준이다.** `startPos = pivotPosVec`을 `Kill(true)` **앞에서** 읽고, 시작값은 `Kill(true)` **뒤에서** 읽는다. 진행 중에 끊기면 이전 목표로 튀었다가 `끊긴 순간 값 + 오프셋`으로 간다. 끊긴 값은 프레임 시각에 좌우되므로, 표는 이벤트 시각의 값으로 근사할 수밖에 없다. 직전 목표값을 쓰면 이 경우 틀린다(체인 합성 반례 15→105).
3. **`Kill(true)`는 이전 트윈의 완료 콜백을 그 순간 실행한다.** 그래서 축별 시작값은 직전 목표값이 된다(LastPosition의 끝값만 예외).
4. **숨겨지거나 알파 0이면 `LateUpdate` 갱신을 건너뛴다.** 그동안 시차·잠금 값은 낡지만 트윈이 돌면 setter가 `UpdatePosition`을 부른다. 보이지 않으니 대부분 차이가 없고, 1번과 겹칠 때만 드러난다.
5. **시차 트윈은 시작값 클로저가 고정이고 완료 콜백이 없다.** 값 적용은 다음 `LateUpdate`다. 목표는 `target / 100f`, 초기값은 `0.01f * value`로 float 계산이 다르다.
6. **`SetPlacementType`은 `startPos`만 바꾼다.** `positionOffset` 없이 `relativeTo`만 쓰면 `pivotPosVec`은 그대로다. `followPlanet`은 먼저 지워진다. Tile은 그 시점의 타일 위치를 읽는다. `clampToScreen`은 다음 `SetPosition`이나 `SetParallax` 때 갱신된다.
7. **깊이 규칙:** `depth >= 0`이면 `Bg` 층·게임 레이어 9, 음수면 `Default` 층·레이어 7이고, 순서는 `-depth`다. `syncFloorDepth`면 타일의 값을 **호출 시점에** 복사한다. 레이어는 카메라 컬링에도 영향을 준다.
8. **`stickToFloor`와 `lockRotation`은 배타적이다.** 회전은 `stickToFloor`가 우선한다. 크기의 `lockScale`은 함께 적용된다.
9. **반복(`tile`)의 텍스처 랩은 텍스처 단위다.** 같은 이미지를 쓰는 장식 하나라도 반복이면 그 텍스처 전체가 `Repeat`가 된다.
10. **대상 찾기:** 태그는 공백으로 나누고 없으면 `NO TAG`다. `Distinct`로 중복을 제거한다. 태그 표는 `Setup` 때 만들어진다.
11. **마스크 제공자**는 `spriteRenderer`가 꺼지고 `spriteMask`가 켜진다. 범위는 `front = -앞 + 1`, `back = -뒤`이며 앞뒤가 뒤집히면 교환한다.

## 5. 구현 분류

| 분류 | 대상 | 곡 중 비용 |
|---|---|---|
| 연속 표 | 위치, 피벗, 회전, 크기, 색, 투명도, 시차 | GPU 평가 (지금 경로) |
| 계단 표 | visible, 이미지, 원래 크기, 크롭 | 변경 시점에만 |
| 프레임 값 | 잠금, 행성 추종, Camera 배치 | 프레임당 값 몇 개 |
| 정렬 변경 | depth | 구간 처리 필요, 설계 미정 |
| 스텐실 | 마스크 제공자·소비자 | 원래 방식 또는 직접 처리, 게임 검증 안 됨 |
| 원래 방식 유지 | 글자, 오브젝트, 파티클, prefab, 사용자 컴포넌트, 히트박스, 혼합 모드, 조건 이벤트 | 지금과 같음, 구간을 끊는 장벽 |

## 6. 현재 코드의 규칙과 어긋나는 점

- `DecoBake.Reasons`는 **필드를 쓰면** 거부한다. 값이 안 바뀌어도 대상 장식 전체가 빠진다.
- `visibleUsed`와 `depthUsed`를 한 사유로 묶어, 1비트짜리 가시성까지 거부한다.
- `FastMove.Orig`는 효과 하나라도 원래 경로로 가면 `RecoverGpuLive("FastMoveOriginal")`로 **맵 전체를** 되돌린다. Exit, DDONGSSADA가 곡 중에 이렇게 꺼졌다. 대상이 소유 장식인지 먼저 봐야 한다. 정확한 트리거는 아직 안 봤다.

## 7. 아직 안 읽은 것 (미확인)

- `scnGame`의 장식 생성과 `RepeatEvents`, `SetConditionalEvents` 전개 전체.
- `scrTextDecoration`, `scrObjectDecoration`, `scrParticleDecoration`, `scrPrefabDecoration`. 지금은 원래 방식으로 남기므로 범위 밖이다.
- DOTween `AxisConstraint`의 내부 동작(기존 비트 대조로 간접 확인).
- 다른 모드가 장식에 쓰는 경로(PACL2 `FollowDecoration` 등).
- `sortingOrder`의 범위를 넘는 depth 값(32767, 32768)의 실제 동작.
