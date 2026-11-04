# 얼불춤 전체 구조 분석 (진행 중)

표시: **확인됨**(실제 실행과 코드가 일치) / **코드**(코드·에셋에서만 읽음) / **미확인**.
재현: `tools/rendersys/scan_assets.py`, `scan_code.py`. 표는 `data/`.

## 분석 범위 (코드)

- 에셋: 파일 63개, 객체 231,942개, GameObject 47,171개, 카메라 144개, 재질 237개, 셰이더 427개, 스크립트 482종.
- 코드: `Assembly-CSharp`와 `firstpass` 파일 2,529개, 296,881줄. Update류를 가진 파일 708개, 렌더 콜백을 가진 파일 378개(대부분 CameraFilterPack).
- 곡 전체를 읽은 것은 아직 아니다. 먼저 목록을 만들고, 곡 중에 실제로 도는 코드를 런타임 자료로 추린 뒤 그 부분을 읽는다.

## 2026-11-04 게임 코드 변경 (코드)

`Assembly-CSharp.dll` sha `f692210f`→`ec0d7c38`. 파일 15개가 달라졌다. 입력(비동기 키), 판정 이름 정리, 메뉴, `TextureManager` 해제 전 확인, `scnGame` 여백 추적을 편집기에서만 초기화, `ffxRecolorFloorPlus` 기본값 정도다. 렌더링 구조 변경은 없다. 엔진은 6000.3.25f1 그대로.
`HitMeterFade`가 `ErrorMeterTick`을 찾는데 이 클래스의 `Start`가 `Awake`/`Start`로 나뉘었다. 동작 확인 필요(미확인).

## 장면 (코드)

`scnSplash, scnLevelSelect, scnCalibration, scnLoading, scnGame, scnEditor, scnCLS, scnMinesweeper`와 공식 레벨 장면들. 커스텀 맵 플레이는 `scnGame`(게임 화면) 또는 `scnEditor`(편집기)에서 돈다.

### scnGame 최상위 (level4, 객체 684개)

`Controller`(scrController), `scnGame`, `Conductor`(scrConductor, AudioSource 여러 개), `Vfx`(scrVfx, scrVfxPlus), `Decoration Container`(scrDecorationManager), `Floors`, `Level Maker`(scrLevelMaker), `PlayerManager`(scrPlayer 4개, PlanetarySystem), `CamParent/Camera`(scrCamera), `GUI`(Canvas, CanvasOverlay, CanvasMiddle), `CustomBackgroundSprite`, `VideoBG`(VideoPlayer), `WorldCanvas`.

## 카메라와 그리는 순서

| 카메라 | depth | 지움 | 그리는 레이어 |
|---|---:|---|---|
| BGStaticCam | -1.63 | 색(2) | BG Static |
| OverlayCam | -1.63 | 색(2) | RenderTexture(화면 전체 Quad) |
| BGMovingCam | -1.11 | 깊이(3) | BG |
| Camera | -1 | 안 지움(4) | Default, Planet, Floor, Camera, Flash, TopGlow, FloorTemp 등 |
| PausePlanets | 0 | 색(2) | Planet |

값은 장면 파일 기준(**코드**). 실행 중 `scrCamera`가 바꿀 수 있다.

**RT 모드(코드, 실행 측정과 일부 일치):** `SetupRTCam`이 BG 두 대와 본 카메라의 대상을 화면 크기 `camRT`로 돌리고, `OverlayCam`이 그 그림을 입힌 Quad를 그린다. `useRTCam`이 언제 켜지는지, 필터가 각 카메라에 어떤 순서로 걸리는지는 RenderDoc으로 확인한다.

## 정렬 레이어 (코드)

`Bg3, Bg2(Lights), Bg, Floor, FloorCover, FloorTop, Ring, Planet, Default, Petals, FgFlash, ForegroundUI`. GraphicsSettings 투명 정렬 모드 0, 품질 설정 vSync 0, MSAA 값 8(실제 RT는 측정에서 MSAA=1, **확인됨**: census).
정렬 동점 규칙은 shader ID → material ID (메모리 unity-tie-sort-rule, 측정).

## 남은 일

1. 게임 실행: RenderDoc 캡처 6맵, 런타임 객체·활성 Update 목록, PlayerLoop 단계별 비용.
2. 곡 중 실제로 도는 코드 읽기: 컨트롤러·컨덕터·공·타일·장식·효과·홀드·판정 글자·UI.
3. 값을 읽는 쪽 목록: 게임 코드와 다른 모드(Quartz, JALib, AdofaiGenBridge, TUFHelper, YouTubeStream, PACL2).
4. 항목별 비용과 대체 가능성 표.
