# Codex 작업 Y: Quartz 모드가 StutterFix 와 함께 있을 때 프레임이 오르는 이유 분석 (정적 분석만) — 게임 실행·Mods 설치·소스 변경·커밋 금지

결과: `docs/quartz-analysis-20261019.md` (+ 검토 메모 `docs/handoff/codex-quartzanalysis-20261019-result.md`). **UTF-8 BOM, apply_patch**, 한글 확인. 이 저장소에는 다른 모드의 코드를 저장·인용하지 않는다(`docs/release.md`): **이름·동작 설명·호출 관계만** 적고 소스는 옮기지 않는다. `Quartz/UserData` 의 `Discord.*`/`*credentials*`/`*.key`/`*secret*` 파일은 절대 읽거나 적지 말 것(인증 정보).

## 사실 (Claude 실측, 일반 게임 버전 Unity 6000.3.10f1, HELLO 2026 전곡, 3440×1440, 판마다 새 프로세스)
- StutterFix 정식 2.8.0 단독: 평균 194 FPS, 1% low 99, 0.1% low 68, 12ms 초과 89개.
- 같은 조건 + Quartz 켬: **평균 224, 1% low 118, 0.1% low 77, 12ms 초과 64개** (평균 +15%, 1% low +19%). 사용자 말: "Quartz 모듈이 우리 모드와 시너지가 있는 것 같다 / 병 주고 약 주고 인지 판단해야 한다."
- Quartz 저장 설정(`UserData/*.json`)에서 켜진 것: Accuracy(ShowHitText), AutoDeafen, ChatterBlocker, Combo, Countdown, Judgement, **JudgementPopupHider**, MainMenu.DisableMenuMusic, PlanetColors, SongTitle, Tweaks(DisableAutoPause, BlockMouseWheelScrollWhilePlaying), **VisualTweaks(RemoveAllCheckpoints, RemoveBallCoreParticles)**, Overlay/OverlayPanels, **Optimizer(LightTextShadows=true, RenderAllHitSounds=true; 나머지 SmoothGC/LeakGuard/CollectOnLevelLoad/BoostProcessPriority/RunInBackground/LossyTextureCompression/FastBloom/SkipNoOpScreenFilters/CacheScreenScale/SkipIdleParticles/PauseOffscreenParticles=false)**. `EffectRemover.On=false`.
- 이미 배제: StutterFix 의 "판정 글자 전부 숨기기"만 켜기는 효과 없음(187 FPS). Wallpaper Engine 일시 정지 효과 없음.
- StutterFix 로그의 "다른 모드가 같은 게임 함수를 고치는 곳"(Quartz 켠 판): `scnEditor.SwitchToEditMode`, `ffxCheckpoint.StartEffect`, `ffxMenuPlanetSpeedChange.StartEffect`, `ffxBloomPlus/FlashPlus/HallOfMirrorsPlus/MoveFloorPlus/ScreenScrollPlus/ScreenTilePlus/SetFilterAdvancedPlus/ShakeScreenPlus.StartEffect`(effectremover), `scrFloor.SetToRandomColor`, `scrParticleDecoration.Update/ResetParticle`(optimizer), `scrCamera.SetCustomFrameRate`(optimizer), `scnEditor.Play`, `LevelData.Decode`(effectremover), `TextureManager.LoadTexture`(optimizer), `scnGame.ResetScene`, `scnGame.ApplyEventsToFloors`(visualtweaks), `scnGame.ApplyEvent`(keylimiter), `scrConductor.PlayHitTimes`(optimizer), `scrPlanet.MoveToNextFloor`(uihider, Quartz), `scrController.FailAction/Restart`(optimizer), `OnLandOnPortal`(uihider), `QuitToMainMenu`(tuf), `scnGame.Play`(keylimiter, Quartz, autodeafen, combo, judgement), `scnEditor.OpenLevelCo`, `scrFloor.LightUp`(visualtweaks).

## 입력물
`D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\ModsOff\Quartz\` (지금 꺼 둔 위치): `Runtime\versions\2.0.0\QuartzUmm.dll`(약 1.9MB, 모듈 포함), `QuartzUmm.Bootstrap.dll`, `Info.json`, `UserData\*.json`(설정; 인증 파일 제외). 게임 DLL: 같은 게임 폴더 `A Dance of Fire and Ice_Data\Managed\`. 도구: `tools/ilscan`(`ILScan.exe <dll> <호출이름>`, `TYPES=`, `METHOD=`, `FIELDS=1`), ilspycmd(디컴파일/원시 IL, 이전 작업에서 사용한 방식), Python. StutterFix 쪽 코드는 이 저장소.

## 알고 싶은 것
1. **Quartz 가 로드될 때 전역으로 바꾸는 값**: `QualitySettings`, `Application.targetFrameRate/runInBackground`, `Time.*`(fixedDeltaTime·maximumDeltaTime·timeScale), `Physics/Physics2D`(autoSimulation·layer 충돌), `AudioSettings`/AudioMixer/DSP·`AudioListener`, 스레드·프로세스 우선순위, `GC`(`GarbageCollector.GCMode`, `GC.Collect`), `Resources.UnloadUnusedAssets`, `Screen`/해상도/vSync, `Camera` 설정, `Canvas`/`TMP` 전역, `Shader.SetGlobal*`, 게임 옵션 저장값(`Persistence`)이나 `RDC.*`/`GCS.*` 정적 필드 쓰기. 활성 모듈 설정과 **무관하게 항상 실행되는 것**(Optimizer 옵션이 전부 false 여도 도는 코드 경로)을 구분.
2. **각 패치(위 목록)의 동작**: 프리픽스/포스트픽스/트랜스파일러/피널라이저, 무엇을 건너뛰거나 대체하나(반환값 `false` 로 원래 함수를 끊는지), 호출 빈도(곡 중 프레임마다/타일마다/한 번), 설정에 따라 켜지는 조건. 특히 곡 중 반복 호출되는 것: `scrPlanet.MoveToNextFloor`, `scrParticleDecoration.Update`, `scrConductor.PlayHitTimes`, `scnGame.ApplyEvent`, `scrFloor.LightUp`, `scrCamera.SetCustomFrameRate`.
3. **프레임을 올릴 수 있는 후보를 순위로**: 위 정적 분석으로 "HELLO(타일 14.7k·필터 많음·자동 플레이)에서 메인 스레드 시간을 줄일" 가능성이 높은 동작을 근거와 함께 3~5개로 압축(예: 공 중심 파티클 제거, 체크포인트 제거, 판정 팝업 숨기기가 아닌 다른 것, 히트음 미리 렌더(`RenderAllHitSounds`), `PlayHitTimes` 대체, 오디오 DSP/우선순위, `Time.*` 설정 등). 각 후보가 StutterFix 에 이미 있는 기능과 겹치는지(`docs/feature-audit-2.8.0.md`, 소스) 대조.
4. **StutterFix 와 충돌·시너지**: 같은 게임 함수에 서로 패치가 걸린 곳에서 순서·우선순위·반환 형태가 어떤 영향을 주는지(예: Quartz 가 `scnGame.ApplyEvent`/`LevelData.Decode`/`scrFloor.LightUp`/`scrParticleDecoration.Update` 를 바꿔서 StutterFix 의 효과 감싸기·타일 끄기·파티클 건너뛰기·장식 최적화가 더 잘/덜 작동하는 경로). 특히 **Quartz 가 있으면 StutterFix 의 최적화가 달라 보이는 경로**(우리 패치가 걸린 함수를 Quartz 가 먼저/나중에 감싸는지).
5. **게임 업데이트(Unity 6000.3.25f1, 3.4 알파)에서 Quartz 가 깨지는 이유**: `RDInput.GetMainPressKeys` 반환형 변경 외에 같은 계열 위험이 있는 호출(예: 입력 API) 목록(이건 참고용).
6. 산출물 맨 위에 "한 줄 결론"(가장 유력한 원인 후보 1~2개와 확신도)과, StutterFix 에 **우리 방식으로 구현할** 기능 후보 목록(이름·한 줄 설명·예상 위험·Quartz 와 같이 쓸 때 우리 것을 우선 활성화하는 방법: `Compat`/Harmony `GetPatchInfo` 로 감지해 양보시킬 항목)을 둔다.

## 규칙
- 코드 변경·게임 실행·설치·커밋 금지. 모르면 "미확인". 추측은 "추정".
