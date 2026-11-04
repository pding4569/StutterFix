# 무엇이 어떻게 그려지나

표시: **확인됨** / **코드** / **미확인**. 실제 순서는 RenderDoc 캡처로 확인해야 한다.

## 카메라 구조

재생 시작 때 `scnGame.Play`가 `SetupRTCam(true)`를 부른다(**코드**, 측정 census의 RT와 일치). BG 두 대와 본 카메라의 대상이 화면 크기 `camRT`가 되고, `OverlayCam`이 `camRT`를 입힌 Quad를 그린다.

```
BGStaticCam(BG Static) ┐
BGMovingCam(BG)        ┼ camRT → Quad(OverlayCam) → 백 버퍼 → 위에 UI 캔버스
Camera(본 화면)         ┘
```

- 본 카메라는 레이어 `Default, TransparentFX, Ignore Raycast, Water, Planet, Floor, Camera, Flash, TopGlow, FloorTemp, Review, Handles`에 `Start`에서 Foreground(값 128)를 더한다. 측정 mask 652183과 맞다.
- `enableCustomFPS`(맵 효과)면 별도 RT로 주기마다 세 카메라를 직접 `Render()`한다.
- 필터는 카메라에 붙은 컴포넌트다. `scrVfxPlus.filterToComp`가 `Filter` 값과 컴포넌트를 잇고, 효과가 `enabled`를 켠다. 각 필터의 `OnRenderImage`가 전체 화면 Blit이다. 측정에서 필터는 `Camera`와 `BGMovingCam` 양쪽에 있었다(**확인됨**: gpuprobe 범위 이름).

## 그리는 순서

정렬 레이어 순서: `Bg3, Bg2(Lights), Bg, Floor, FloorCover, FloorTop, Ring, Planet, Default, Petals, FgFlash, ForegroundUI`.

| 물건 | 정렬 레이어 | Unity 레이어 | 근거(코드) |
|---|---|---|---|
| 깊이 ≥ 0 장식(이미지·글자·입자·오브젝트) | **Bg** | 9(Floor) | `SetDepth` |
| 타일 | Floor (지나온 타일 일부 FloorTop) | Floor | `FloorMeshRenderer`, `scrFloor:1218` |
| 홀드 | FloorCover, 끝 원 Floor | | `scrHoldRenderer` |
| 공, 꼬리, 링 | 기본 Planet(`SetLayer`로 바뀜) | Planet | `PlanetRenderer` |
| 깊이 < 0 장식 | **Default** | 7(Foreground) | `SetDepth` |
| 플래시 | Default 또는 FgFlash | | `ffxFlashPlus` |

**핵심:** 일반 장식은 정렬 레이어 `Bg`(타일 아래) 또는 `Default`(공 위)에만 들어간다. 타일과 공 사이에 끼는 건 `syncFloorDepth` 장식과 공 `SetLayer` 정도다. 장식과 타일이 뒤섞이는 경우는 드물다. 어려운 순서 문제는 **같은 레이어 안의 동점과 마스크**다.

레이어 안에서는 `sortingOrder`(장식은 `-depth`)가 먼저이고, 동점이면 측정으로 찾은 규칙(셰이더 ID → 재질 ID, 메모리 unity-tie-sort-rule)을 따른다. 타일 글로우는 타일 정렬값에 맞춘다.

## 물건별

- **타일**: `FloorMesh`가 각도·폭·길이로 메시를 계산하고 문자열 키로 공유한다. 재질은 타일별(`floorMeshRenderer.material`). 스타일 변경은 재질 값 7개와 메시 재계산(`SetTrackStyle`).
- **이미지 장식**: 프리팹 하나에 `SpriteRenderer`와 `MeshRenderer`가 같이 있다. 일반은 스프라이트로, 장식 필터가 있으면 메시와 RT로 그린다(`UpdateShader`). 셰이더는 `tileShader`, 메시는 `visualDecoShader`.
- **마스크**: `SpriteMask`(스텐실)와 `maskInteraction`. `disableV15Features`가 아니면 `SpriteAlphaMask` 방식으로 RT에 합성한다. Exit에서 렌더 노드 재할당 비용 측정.
- **블렌드**: `BlendModeEffect`가 재질을 바꾼다. 게임은 전부 `Hidden/BlendModes/SpritesDefault/Grab`(그릴 때마다 화면 복사). 모드의 `FastBlend`가 더하기만 복사 없이 바꾼다.
- **글자 장식**: `scrTextDecoration`, Canvas. `canvas.sortingLayerName` 사용.
- **입자 장식**: `scrParticleDecoration`, `ParticleSystemRenderer`.
- **오브젝트 장식**: `scrObjectDecoration`, 내부에 타일·공 표시를 쓴다.
- **프리팹 장식**: `Resources.Load("PublicPrefabs/...")`.
- **공**: `scrPlanet`(움직임) + `PlanetRenderer`/`PlanetSprite`(표시), 꼬리·코어·불꽃 입자.
- **판정 글자**: `scrHitTextManager`, `scrHitTextMesh` 풀. TextMeshPro. 1.25초 뒤 끔.
- **배경**: `scrCustomBackgroundSprite`, 영상은 `VideoBG`(VideoPlayer).
- **UI**: `GUI` 아래 Canvas 3개, `WorldCanvas`.
- **규모**: 에셋 재질 237개, 셰이더 427개(필터 다수). 표는 `data/materials.tsv`, `shaders.tsv`.

## 필터 (코드)

`CameraFilterPack` 337파일. 대부분 `OnRenderImage`에서 `material.SetFloat`을 문자열 이름으로 여러 번 부르고 `Graphics.Blit`한다. `FastFilter>1`이면 작은 임시 RT를 거친다. 측정 비용은 `Blur_Movie` 약 0.96ms(Windflower), `SuperComputer` 약 0.68ms(7777)가 눈에 띈다.

## 실제로 봐야 할 것

필터 적용 순서, 화면 복사 횟수, 실제 배칭, 클리어 횟수, MSAA 적용 여부, UI가 어느 대상에 그려지는지, 렌더 스레드 상태 변경 수.
