# 맵 열기, 그리고 Unity 객체를 없앨 때 깨지는 곳

표시: **확인됨** / **코드** / **미확인**.

## 맵 열기 (코드)

```
scnGame.Update (프레임 조건) → LoadAndPlayLevel
  LoadLevel: FlushUnusedMemory, UnloadUnusedAssets, levelData.LoadLevel (파일 → LevelEvent)
  RemakePath → scrLevelMaker.MakeLevel: 타일 Instantiate, 정렬값, 시작 위치
  ReloadAssets
  UpdateDecorationObjects: 이벤트마다 CreateDecoration (Instantiate + Setup 391줄), 이동 효과의 이미지까지 미리 로드
  ApplyEventsToFloors (1,031줄): 이벤트 → 타일별 ffx 컴포넌트 생성
  FinishUpdateDecorations, DrawMultiPlanet, DrawHolds
  Play: 카메라·지휘자·컨트롤러 되감기, SetupRTCam(true), FinishCustomLevelLoading
    ResetDecorations, UpdateVideo, PrepVfx, 시작 코루틴
```

효과는 타일에 붙는 컴포넌트다(`ffxPlusBase`). 곡 중에는 `scrVfxPlus`가 시간 순으로 부른다.

측정(두 번째 열기, 대기 2초 포함, 지금 되돌린 압축 대기 빌드): DD 26.2초 중 이미지 5.5초, Arche FULL 22.2초 중 7.2초, Windflower 8.8초 중 4.1초. 예전 Arche 측정은 이벤트 변환 3.85초, 장식 2만 8,835개 8.8초(Setup 약 2초, 복제 약 4.2초).

모드가 끼어드는 곳: `ParallelDecode`, `ImagePrefetch`, `DecoBatch`, `MeshWarm`, `SoundWarm`, `ExitFix`, `JitWarm`.

## 값을 읽는 곳 (코드, 단순 검색)

호출 빈도는 측정 전이다.

- **타일 위치**: 곡 중에 관련되는 파일은 `scrPlanet`, `scrPlayer`, `scrController`, `scnGame`, `scrLevelMaker`, `scrDecoration`, `ffxCameraPlus`, `ffxFloorAppearPlus`, `ffxFloorDisappearPlus`, `scrPortalParticles`. 공은 `stickToFloor`면 매 프레임 읽는다. 장식은 `stickToFloor`면 위치를 읽고 색·정렬도 따른다.
- **`floorRenderer` 직접 사용**: `scrObjectDecoration` 8, `scrHoldRenderer` 6, `scnEditor` 6, `scrLevelMaker` 5, `scnGame` 3, `scrVisualDecoration` 2, `scrFloor` 2, `scnCLS` 2, `ffxLightBridge` 2, `scrPlanet` 1.
- **장식 찾기**: `ffxMoveDecorationsPlus`, `ffxSetObjectPlus`, `ffxSetTextPlus`, `ffxSetParticlePlus`, `ffxEmitParticlePlus`, `ffxSetFilterAdvancedPlus`, `Level`, `scnEditor`, 편집기 패널들.
- **히트박스**: `scrPlanet.Update`의 `OverlapCircleAll`과 `scrDecoration.CheckHitboxHit`이 콜라이더를 쓴다. 히트박스 장식은 객체를 유지하거나 판정을 대체해야 한다.

## 다른 모드

설치: Quartz(부트스트랩으로 받아 온다), JALib, AdofaiGenBridge, TUFHelperLite, YouTubeStream. 로그에 같은 게임 함수를 고치는 곳 30개가 보인다(`jf-hl-1.log`). 패치 전체 목록은 게임을 켜서 뽑아야 한다.

## 항목별 비용과 빼기 쉬운 정도

비용은 지금까지의 측정이며 맵과 판마다 다르다. 난이도는 코드 분석에 따른 판단이다.

| 항목 | 측정 비용 | 뺄 때 어려움 | 판단 |
|---|---|---|---|
| 이미지 장식 위치 쓰기 | Arche 약 3.7ms(`decofreeze`) | 시차·스크린 고정·타일 부착·외부 읽기 | 가장 큰 후보, GpuDeco 범위 |
| 이미지 장식 그리기 GPU | 맵마다 1.3~6.4ms(숨김, 판 달라 근사) | 같은 레이어 동점·마스크·필터 | GpuDeco, 동점 규칙은 측정됨 |
| 장식 전부 순회 | Arche 1.3~1.7ms(`Dormancy` 전) | 낮음 | 이미 줄임 |
| 타일 Update/Late | 게임이 화면 밖을 끔. 후보 ABBA는 잡음 안 | 공·장식이 위치를 읽음 | 효과 없음 확인 |
| 타일 그리기 GPU | HELLO·7777·Windflower 작음, DD는 판 간 차이 큼 | 레이어가 분리되어 비교적 쉬움 | 낮은 우선순위 |
| 필터 | 7777 1.68ms, Windflower `Blur_Movie` 0.96ms | 픽셀 동일이 어려움 | GPU 병목 맵에서 후보 |
| 판정 글자·UI 캔버스 | 약 0.25~0.3ms | UI는 그대로 둠 | 개별 최적화 |
| 공 히트박스 물리 | 미측정 | 히트박스 없으면 건너뛸 수 있어 보임 | **새 후보** |
| 소리 | `UpdateAudio` 0.11ms, FFT 1.3%(`LowNoFft`) | | 일부 처리됨 |
| 렌더 스레드 | 미측정 | | RenderDoc·프로파일 필요 |

## 구조적 결론 (코드)

1. 일반 장식은 정렬 레이어 `Bg`와 `Default`에만 들어간다. 타일·공 앞뒤로 나뉠 뿐 뒤섞이지 않아서, 장식 묶음을 정해진 두 위치에 끼우는 구조가 가능해 보인다. 예외: `syncFloorDepth`, 공 `SetLayer`, 마스크.
2. 어려운 동점은 같은 레이어 안이다. 재질이 장식마다 복제되므로 재질 ID가 사실상 생성 순서를 반영할 가능성이 있다(**미확인**, 측정 필요).
3. 타일은 게임이 화면 밖을 끄므로 추가 이득이 작다. 장식에는 그런 처리가 없다.
4. 게임은 많은 값을 안 바뀌어도 쓴다. 값 비교로 줄이는 방향은 일부 적용됐다(`TightLate`, `Dormancy`).

## 게임을 켜야 답이 나오는 것

- 실제 스크립트 실행 순서, 활성 객체 수, 렌더 스레드 비용.
- 곡 중 다른 모드 패치 전체 목록.
- `Physics2D.OverlapCircleAll`의 실제 비용.
- 필터·클리어·화면 복사의 실제 순서(RenderDoc).
- `ErrorMeterTick` 변경 뒤 `HitMeterFade` 동작.
