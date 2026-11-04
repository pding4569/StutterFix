# 한 프레임의 흐름

표시: **확인됨** / **코드** / **미확인**. 코드 위치는 `decompiled/Assembly-CSharp`.
Unity 스크립트끼리의 실행 순서는 실제 수집 전이라 **미확인**이다. 아래는 단계별로 묶은 것이다.

## 단계별 비용 (확인됨, 2560x1440, GpuDeco=0, 판 하나씩)

| 단계 | DDONGSSADA | Windflower | 비고 |
|---|---:|---:|---|
| 전체 | 7.57ms | 4.63ms | |
| 지난 화면 대기 `WaitForLastPresentation` | 4.32 | 0.58 | DD 판은 압축이 늦은 판 |
| Update `ScriptRunBehaviourUpdate` | 1.05 | 1.35 | |
| LateUpdate | 0.67 | 0.96 | |
| 그리기 `FinishFrameRendering` | 0.59 | 0.82 | 메인 쪽만. 렌더 스레드 시간은 별도 |
| 캔버스 `PlayerUpdateCanvases` | 0.24 | 0.28 | |
| `UpdateAllRenderers` | 0.12 | 0.16 | |
| `SendMouseEvents` | 0.11 | 0.03 | 맵에 따라 다름 |
| `UpdateAudio` | 0.11 | 0.11 | |

## Update (코드)

- **`scrConductor`**: 곡 시계의 기준. `AudioSettings.dspTime`이 안 변한 프레임은 실시간으로 보간한다. `songposition_minusi`가 효과와 판정의 시계다. 소리는 5초 앞까지 예약하며 `"snd"+이름` 문자열을 만든다. `getSpectrum`이면 매 프레임 `GetSpectrumData`(FMOD FFT).
- **`scrController`**: 상태 기계(`StateEngine`)로 `PlayerControl_Update`를 부른다. Update 본문은 치트코드·키 조합 확인 등 곡과 상관없는 검사가 많다. 플레이어마다 `Simulated_PlayerControl_Update`(비동기 입력이 꺼졌을 때).
- **`scrPlanet`**(플레이어당 2개 이상): `hittable`이면 **매 프레임 `Physics2D.OverlapCircleAll`**로 히트박스 검사(배열 할당), 크기 쓰기, 각도 갱신(`Update_RefreshAngles`), 입자 방출 쓰기. 홀드 타일이면 위치 계산과 `holdRenderer.UpdateColor`.
- **`scrCamera`**: 위치·회전·색 보간 후 `transform` 쓰기. 크기 트윈이면 `UpdateSize`.
- **`scrVfxPlus`**: `effects` 목록을 시각 순서로 보며 시간이 된 효과의 `StartEffect`. 맵 효과는 전부 여기서 시작한다. `ffx` 파일 84개, 그중 39개가 DOTween 사용.
- **`scrDecorationManager.Update`**: 편집 중이 아니면 **장식 전부 `CheckHitboxHit`**.
- **`scrFloor.Update`**: 켜진 타일만. 색 종류가 Glow/Rainbow/Blink/Switch/Volume이면 매 프레임 색 쓰기. Volume은 프레임마다 DOTween `Sequence`를 만든다.
- **`scrHoldRenderer.Update`**: 홀드마다 `UpdateMesh`, `UpdateColor`.
- **`AudioManager.Update`**: 끝난 `AudioSource`를 재사용 큐로.
- **`scnGame.Update`**: 화면 효과 판 4개의 크기를 매 프레임 쓴다.
- HUD: `scrHUDText`, `scrUIController` 등 소수.

## LateUpdate (코드)

- **`scrDecorationManager.LateUpdate`**: **장식 전부 `LogicUpdate`**. 보이는 장식은 `UpdatePosition`(pivot `localPosition`, 시차 `SetTrans`로 `position`, 회전, 크기)을 **값이 안 바뀌어도 매 프레임 쓴다**. `customLevel`이면 `UpdateShader`. 모드의 `Dormancy`·`TightLate`가 이 경로를 줄인다.
- **`scrVisualDecoration.UpdateShader`**: `disableV15Features`가 아니면 장식마다 `meshRenderer.material`을 건드리고, 장식 필터가 있으면 프레임마다 `Graphics.Blit`과 리플렉션 `OnRenderImage` 호출.
- **`scrFloor.LateUpdate`**: 켜진 타일만. 위치·크기·투명도 변화 비교, 변했으면 재질·글로우 쓰기.
- **`scrController.LateUpdate`**: 음량, `mutedImage.SetActive`, **`FloorMesh.UpdateAllRequired`**(필요한 타일 메시 생성, 문자열 키 캐시).
- **`scrParallax.LateUpdate`**: 배경 등 시차.
- **`PlanetRenderer`, `PlanetSprite`, `scrLockToCamera`**: 공 표시와 카메라 고정.

## 게임이 이미 하는 화면 밖 처리 (코드)

`scrFloor.OnBecameVisible/Invisible`이 타일 `enabled`를 켜고 끈다. 화면 밖 타일은 Update/LateUpdate가 안 돈다(글로우도 안 보일 때). 장식에는 이런 처리가 없고, 보이는지는 렌더러 활성과 알파로만 판단한다(`GetVisible`).

## 재질 복제 (코드)

`spriteRenderer.material`, `floorMeshRenderer.material`은 접근 시 재질을 복제한다. 그래서 **장식과 타일이 대체로 자기 재질을 가진다**. SetPass가 그리기 호출에 가까운 이유로 보인다(DD 295/358, 수치는 **확인됨**, 인과는 **미확인**).

타일 정렬값은 `(100 + 남은 타일 수) * 5`다. 앞 타일이 위에 그려진다.
