# 매 프레임 엔진 호출 조사 — 2026-10-20

**한 줄 결론: 1순위는 전용 필터의 문자열→ID 치환과 제한된 동일 값 쓰기 생략(구조 확신 높음·속도 이득 미측정), 2순위는 지휘자의 추가 tick/hold 음원 예약 묶기(새 후보·이득 미확인), 3순위는 UI 측정 호출의 소유자 확인이다. HELLO 후반 재질 쓰기 표본 7.4%는 약 0.444ms/6ms 프레임의 조사 예산이며, 필터만으로 5% 향상한다는 근거는 없다.**

## 우선순위

아래 순서는 확인된 속도 이득 순위가 아니라, 관측된 비용과 제거 가능성을 함께 본 **조사 순위**다. 이득이 미측정인 후보끼리 실제 크기 순서를 확정할 수 없다.

|조사 순위|후보 / 기존 기능과의 차이|6ms 기준 관련 표본 예산|안전 순위·난이도|확신 / 판정|
|---|---|---:|---|---|
|1a|필터 상수 문자열을 정수 ID로 치환. TypeCache는 필터 타입 검색 캐시라 별개|문자열 변환 비용 미분리. SetFloatImpl 0.342ms를 그대로 절감액으로 쓰면 안 됨|1·중간. 값·횟수·순서 보존|구조 높음, 성능 낮음. 먼저 단독 A/B|
|1b|허용한 전용 필터의 동일 값 setter 생략. FilterIdentity의 중립 필터 전체 우회와 별개|FloatImpl+ColorImpl 합 0.444ms의 일부. 타일·장식·vector 포함, 필터 몫 미확인|3·중상. 외부 쓰기·다중 패스·재질 수명 보호 필요|정적 후보 높음, 절감률 낮음. 소수 필터만|
|2|extraTicksCountdown / holdSoundsData 예약 묶기. HitSoundBatch가 맡는 hitSoundsData 외 영역|Conductor 래퍼 전체 약 0.090ms의 일부. 별도 AudioManager 비용 포함률 미확인|4·높음. 오디오 시각·취소·피치 보존|구조 높음, 대상 호출량·이득 미확인|
|3|GUIStyle 크기 측정 캐시/비활성화 범위 확인. UiDraw.Width 캐시와 별개|h170 0.8% ≈ 0.048ms, GetDimensions와 중복|2·낮음~중간. 소유자가 우리 UI일 때만|소유자 미확인. 먼저 UI 상태 대조|
|4|특수 타일 색 Switch/Volume의 반복 쓰기 좁히기. FloorLateFix와 별개|FloorMeshRenderer.set_color 2.0% ≈ 0.120ms의 일부|5·높음. 공유 재질·다른 쓰기 경로 존재|인과 중간. 일반 색 캐시 금지|
|5|scrParallax.SetTrans의 공통 카메라 읽기/변화 없는 위치 쓰기 줄이기|h170 1.2% ≈ 0.072ms의 일부|4·높음. TightLate/MoveApply와 크게 중복|새 독립 영역 미확인, 후순위|

안전 순위는 1이 가장 안전하다. 중립 필터 전체 우회는 기존 FilterIdentity의 인증 확장으로만 검토한다. 입력 갱신 생략과 Unity Object 생존 검사 전역 교체는 권하지 않는다.

## 1. 범위와 증거 해석

조사만 수행했다. 소스(.cs/.csproj) 변경, 빌드, 게임 실행, Mods 설치, 커밋은 하지 않았다. 게임 디컴파일 본문과 IL 호출 자료는 저장소 밖 TEMP의 `sf-enginecalls-20261020`에만 두었다. 다른 모드 구현을 이번 조사에 읽거나 복사하지 않았다. Quartz 동작 설명은 기존 저장소 문서의 배경으로만 사용했다.

입력은 [h50](handoff/profiles/prof-h50.txt), [h170](handoff/profiles/prof-h170.txt), [a262](handoff/profiles/prof-a262.txt)다. 각각 HELLO 46~61초 / 168~183초 / Arche 262~277초의 15초 표본이며, 인계 기준 알파 Unity 6000.3.25f1·3440×1440·자동 플레이·Quartz 없음이다. 실행 중 버전/옵션은 재확인하지 않았다.

- 포함 시간은 같은 스택의 부모·자식을 중복 포함한다. SetColor와 SetColorImpl, Rewired의 여러 단계, Transform과 장식 래퍼를 합산하지 않는다.
- 표에 없는 항목은 **목록 밖(—)** 이다. 0%, 호출 없음, 동일한 고정 검출 하한이라는 뜻이 아니다.
- 호출 스택의 관리 프레임이 빠지는 경우가 있다. h170 SetFloatImpl 리프 묶음은 호출자가 “없음”으로 보고되어 필터별 기여를 배분할 수 없다.
- 6ms × 표본비율은 **가정한 프레임에서의 상당 시간**이다. 이번에 실제 CPU 프레임 시간을 재지 않았고, 대기/표본 편향·GPU 병목·패치 비용을 포함한 실제 절감량이나 FPS 예측이 아니다. Arche에 6ms를 대입하지 않는다.
- h170 FloatImpl 5.7% + ColorImpl 1.7% = 7.4%, 상당 시간 0.444ms. 전부 없어진다고 가정해도 프레임 시간 7.4% 감소/처리율 약 8.0% 증가에 해당할 뿐, 현실적 필터 이득은 이보다 작거나 패치 비용 때문에 음수일 수 있다.
- 5% 프레임 시간 감소는 0.300ms다. 그것만으로도 재질 예산의 약 67.6%를 제거해야 한다. “약 5% 이득”은 검증할 가설로 남긴다.

### 주요 관리 함수 비교

우리 모드 함수와 Harmony 래퍼는 주 표에서 제외했다. 래퍼만 남은 게임 Update는 다음 표에 따로 표시한다.

|원본 게임/엔진/라이브러리 함수|h50 %|h170 %|a262 %|h170 6ms 상당 시간|읽을 수 있는 차이|
|---|---:|---:|---:|---:|---|
|Material.SetFloatImpl|0.9|5.7|—|0.342ms|후반 증가. 필터/타일 분해 필요|
|Material.SetFloat|1.0|—|—|—|문자열·정수 오버로드 구분 안 됨|
|Material.SetColorImpl|1.0|1.7|—|0.102ms|SetVector도 같은 내부 함수로 감|
|Material.SetColor|1.0|1.7|—|0.102ms|위 항목과 합산 금지|
|FloorMeshRenderer.set_color|0.9|2.0|—|0.120ms|타일 원본 경로가 실제 후보|
|scrDecoration.LogicUpdate|2.2|3.3|—|0.198ms|TightLate/MoveApply와 중첩|
|DG.Tweening.Core.TweenManager.Update|1.6|1.6|—|0.096ms|모드가 맡지 않은 트윈 포함|
|DG.Tweening.Tween.DoGoto|1.2|1.3|—|0.078ms|위 항목과 중첩|
|Rewired.InputManager_Base.DoUpdate|1.3|1.4|—|0.084ms|후반에도 거의 일정|
|Rewired.InputManager_Base.Update|1.2|1.3|—|0.078ms|DoUpdate와 중첩|
|Rewired.ReInput.akQfnDbPZywVjhOOFyyXxeKyxzCb|1.0|1.1|—|0.066ms|난독화 루틴의 상위 경로|
|YzAwSQuwclVncgAOlFEYukVTLseR.HJLvPqWZjXiyepqQTSvDgxaDdDsV|0.9|0.9|—|0.054ms|Rewired_Core.dll 내부임을 IL로 확인|
|scrController.UpdateInput|1.2|1.3|—|0.078ms|ProcessKeyInputs 포함|
|scrController.ProcessKeyInputs|1.1|1.2|—|0.072ms|자동 플레이에서도 진행/판정 수행|
|scrPlayer.Simulated_PlayerControl_Update|1.1|1.2|—|0.072ms|아래 Hit 등과 중첩|
|scrPlayer.Hit / OttoHoldHit (각각)|0.9|1.0|—|각 0.060ms|입력을 안 눌러도 필요한 자동 진행|
|scrParallax.SetTrans|—|1.2|—|0.072ms|카메라/위치 읽기·position 쓰기|
|Object.IsNativeObjectAlive|—|1.0|2.3|0.060ms|게임과 우리 경로 모두 포함|
|Object.op_Equality|—|1.0|2.6|0.060ms|생존 검사와 중첩|
|GUIStyle.GetPreferredSize / GetDimensions (각각)|—|0.8|—|각 0.048ms|IMGUITextHandle 0.8%와 중첩|
|Transform.set_position|—|—|3.6|—|Arche의 남은 엔진 쓰기|
|Transform.get_localPosition|—|—|2.6|—|장식 애니메이션과 중첩|

|래퍼로만 관측된 원본 진입점(예산 참고용)|h50 %|h170 %|a262 %|
|---|---:|---:|---:|
|scrFloor.Update_Patch1|1.8|3.1|—|
|scrFloor.LateUpdate_Patch1|1.8|1.0|—|
|scrConductor.Update_Patch1|1.4|1.5|—|
|scrVfxPlus.Update_Patch3|1.6|—|2.2|
|DOTweenComponent.Update_Patch6|4.1|4.1|32.9|

CameraFilterPack 개별 OnRenderImage, Material.SetTextureImpl, TMP 재생성, Canvas, scrCamera, scrParticleDecoration은 제공된 관리 포함 목록에 독립 항목이 없다. 비용이 없다는 판단은 하지 않는다. Arche는 DecoAnim.UpdatePrefix 31.4%, TightLate.List 17.0%가 보이지만 요청 범위상 새 엔진 후보와 분리했다. 32.9% DOTween 래퍼를 원본 DOTween 비용으로 읽으면 모드의 직접 애니메이션 비용을 잘못 세게 된다.

## 2. IL 전수 호출 분류

현재 DLL을 기존 ILScan으로 읽었다. **이 빌드의 FindCallers는 메서드 짧은 이름을 완전 일치로 비교한다.** `Material::SetFloat`는 0곳을 반환했다. 아래처럼 `SetFloat`로 검색한 뒤 목적지가 `UnityEngine.Material::SetFloat`인 줄만 골랐다. PlayerPrefs/AudioMixer/MaterialPropertyBlock/게임 SetColor는 제외했다.

재현 명령(읽기 전용, 기존 실행 파일 사용):

```powershell
$managed = 'D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\A Dance of Fire and Ice_Data\Managed'
& tools/ilscan/bin/Release/net8.0/ILScan.exe "$managed\Assembly-CSharp.dll" SetFloat
& tools/ilscan/bin/Release/net8.0/ILScan.exe "$managed\Assembly-CSharp-firstpass.dll" SetFloat
# SetColor / SetVector / SetTexture도 같은 방식
```

|어셈블리 / 호출|필터 계열|나머지|합계|
|---|---:|---:|---:|
|Assembly-CSharp / SetFloat|60|44|104|
|Assembly-CSharp / SetColor|17|32|49|
|Assembly-CSharp / SetVector|37|19|56|
|Assembly-CSharp / SetTexture|32|23|55|
|firstpass / SetFloat|276|27|303|
|firstpass / SetColor|14|4|18|
|firstpass / SetVector|226|34|260|
|firstpass / SetTexture|62|35|97|
|두 어셈블리 / SetFloat|336|71|407|
|두 어셈블리 / SetColor|31|36|67|
|두 어셈블리 / SetVector|263|53|316|
|두 어셈블리 / SetTexture|94|58|152|

단위는 **호출을 가진 메서드 정의와 목적지 이름의 조합**이다. 한 메서드 안 여러 속성 쓰기는 1줄로 합쳐지며, 오버로드된 호출자 메서드는 같은 이름으로 여러 줄 나올 수 있다. 실행 횟수·고유 속성 수·string 오버로드 수가 아니다. 네 API 합집합의 필터 클래스는 CSharp 61종, firstpass 276종이다(현재/Legacy 접두어 모두 포함). 설치된 컴포넌트 수와 다르다.

전수 결과의 비필터 영역은 다음과 같이 분류했다. 모든 호출자를 실행 중 핫 패스로 취급하지 않는다.

|영역|해당 호출자 계열 / 역할|반복성·인자|
|---|---|---|
|타일·hold·행성|scrFloor(SetIconAngle/Scale/Color/Sprite/OutlineSprite, UpdateCommentGlow, Start, LateUpdate, SetTrackStyle, UpdateMultitapTexture), scrHoldRenderer(CreateMesh/Start/UpdateFoolDir/UpdateMesh/UpdateColor), FloorRenderer.SetFlash, FloorMeshRenderer.set_color, PlanetSprite(LateUpdate, sprite/alphaMask setter), scrRing.UpdateRingSegments|색/알파는 ID 경로 확인. 아이콘 텍스처는 문자열. 초기화·이벤트·매 프레임이 섞임|
|장식·마스크·블렌드|scrVisualDecoration.UpdateShader/SetTile, SpriteAlphaMaskUtils.ApplyAlphaMaskToRT, BlendModes.BlendModeEffect.SetMask/OverlayMaterialProperties, ShaderProperty.ApplyToMaterial, ShaderUtilities.TransferMaterialProperty/SetProperty|공유 재질·RT 및 동적 속성. 상수 문자열만 치환 가능, 동일 값 전역 캐시 부적합|
|화면 효과|VideoBloom.BloomBlit/OnRenderImage, scrBlur.UpdateTexture/BlurTexture, ColorSuite.OnRenderImage, TNOFilter, TNONebula, PolarScreen, ScreenScroll, ScreenTile|문자열/RT/패스 내부 변경 포함. 필터별 따로 조사|
|표준/제공 효과 라이브러리|UnityStandardAssets.ImageEffects의 Antialiasing, Bloom 계열, BlurOptimized, CameraMotionBlur, ColorCorrection 계열, Contrast 계열, CreaseShading, DepthOfFieldDeprecated, EdgeDetection, Fisheye, GlobalFog, Grayscale, ImageEffects.RenderDistortion, MotionBlur, Noise 계열, PostEffectsHelper, ScreenOverlay, SSAO 계열, SunShafts, TiltShift, Tonemapping, VignetteAndChromaticAberration; Kirnu.MarvelousBloom/ScreenTextureBlend; CustomLightingManager/DirectionalLightManager.updateMaterials|정적 호출 존재. HELLO에서 사용/활성 여부 미확인. RT·조명 갱신까지 한꺼번에 생략하면 안 됨|
|초기화·UI·편집기|scrController.Awake, scnEditor.Start, scrLevelMaker.DrawHolds, RDString.SetLocalizedFont, PracticeTimeline.Init/UpdatePositions, PauseLevel.Init, PauseMedals.Init, Rewired.Glyphs.UnityUI.UnityUITextMeshProGlyphHelper의 GetOrCreateAsset/RemoveUnusedAssets, TakeScreenshot의 GetBlurredScreenshot|곡 중 반복 비용이라는 증거 없음. 로컬 UI/폰트 재질 공유 주의|
|공식 맵 전용·기타|scrPAPPart1BG/3BG, ffxDamageRat, ffxLightBridge, PortalQuad, Mawaru 및 Bar/WarpTile/Sprite 계열, scrNeoCosmosEntrance, BarFade, TaroBGScript.EnableStuff, BBManager.DoFancyEffect, SingSing, TMPro.Examples.ShaderPropAnimator, DG.Tweening.DOTweenModuleUnityVersion.DOGradientColor|현재 세 표본의 원인으로 연결되지 않음. 마지막 항목은 트윈용 확장 경로|

호출 목록은 TEMP의 `Assembly-CSharp[-firstpass]-Set*.txt`에 있다. 본 문서는 메타데이터 이름과 분석 요약만 담으며 게임 구현 본문을 재배포하지 않는다.

### 문자열 ID 비용에 대한 확인

로컬 UnityEngine.CoreModule.dll의 Material 오버로드를 확인했다. string SetFloat/Color/Vector/Texture는 Shader.PropertyToID를 거쳐 내부 setter로 들어가고, int 오버로드는 그 변환을 건너뛴다. SetVector도 SetColorImpl을 사용한다. 따라서 ColorImpl 1.7%를 순수 Color 값 쓰기로 한정할 수 없다.

SetFloatImpl은 두 오버로드의 공통 하위 호출이다. **ID 치환이 없애는 것은 그 앞의 변환이며 Impl 비용 전체가 아니다.** PropertyToID의 해시/조회 시간, 엔진 내부 같은 값 최적화 여부, 패치 도입 후 순이득은 현재 자료에서 수치로 분리되지 않는다. 이를 확인하기 위해 인터넷의 다른 Unity 버전 수치를 가져오지 않았다.

## 3. HELLO 필터와 호출 속성

기존 [맵 분석 요약](../tools/decotrack-research/out/summary.json)의 H 경로로 원본 맵을 읽고 SHA256 동일성을 확인했다. 원본은 수정하지 않았다.

- 기본 SetFilter 1,014개, 필터 이름 31종, enabled=true 786 / false 228, disableOthers=true 36개.
- SetFilterAdvanced 830개, 이름 70종, enabled=true 544 / false 286, disableOthers=true 55개.
- 고급 대상은 Camera Foreground 742개 / Background 88개이며 Decoration 대상은 0개다. 기본 31종+고급 70종은 이벤트 이름 기준이며 중복 구현/Legacy 매핑을 합친 고유 활성 인스턴스 수가 아니다.
- 고급 70종 모두 정적 본문에서 private SCMaterial, new Material(SCShader), OnDisable 정리 패턴을 확인했다. 보통 인스턴스 전용이지만 외부 반사·Harmony 패치·다른 재질 변경 API까지 배제한 증거는 아니다.
- **실제 h50/h170 동시 활성 수는 미확인.** 정적 보조 추정은 원본 이벤트를 floor+angleOffset/180 순서(동률은 원본 순서)로 훑고 카메라 plane별 enabled 집합/disableOthers 초기화를 적용했을 때 고급 합 최대 12개(타일 3706, 전경 10+배경 2)다. RepeatEvents 재실행·실제 beat/초 변환·기본 필터와 상호작용·초기 상태를 재현하지 않았으므로 실게임 최대치/엄밀한 상한/170초의 값이 아니다. 미래 측정 구간의 필터 작업량을 12로 고정하지 않는다.

|고급 필터 / 이벤트 수|OnRenderImage 평가 1회당 setter 정적 경로|대략 생략 가능한 영역|
|---|---|---|
|FX_Glitch2 / 91|float 2 + vector 1: _TimeX, _Glitch, _ScreenResolution|시간 이외 값이 고정이면 최대 2/3 호출. 실제 비용률 아님|
|TV_WideScreenHorizontal / 86|float 5 + vector 1: _TimeX, _Value~4, _ScreenResolution|전환 중이 아니면 최대 5/6. 실제 tween 중 중복률 미확인|
|TV_VHS / 68|float 5 + vector 1|동일한 호출 수 구조. 값 의미는 개별 확인|
|Glow_Glow / 57|일반 경로 float 5 + vector 1, FastFilter>1 경로 float 추가 1 + texture 1|_Amount가 패스 사이 바뀜. 두 쓰기를 호출 위치별로 캐시하면 오동작|
|AAA_SuperComputer / 38|float 9 + color 1 + vector 1|좌표·색·크기·해상도. 정적 사이트 수 11, 시간은 계속 갱신|
|Color_GrayScale / 36|float 2 + vector 1|_Fade·해상도 고정 구간|
|TV_WideScreenVertical / 32|float 5 + vector 1|Horizontal과 같은 수, 개별 인스턴스 필요|
|TV_Chromatical2 / 29|float 5 + vector 1|_TimeX와 Fade/ZoomFade/ZoomSpeed 등|
|TV_VHS_Rewind / 26|float 5 + vector 1|시간 외 값 고정 구간|
|FX_Glitch1 / 21|float 2 + vector 1|시간 외 값 고정 구간|
|Real_VHS / 21|float 6 + texture 2|6개 제어값과 VHS/VHS2. RT 내용은 참조 동일만으로 고정 아님|
|Color_BrightContrastSaturation / 21|float 4 + vector 1|색 조절 3개·해상도, _TimeX 유지|
|Color_Contrast / 4|float 2 + vector 1|_Contrast·해상도 고정 구간, _TimeX 유지|

호출 수는 보통 렌더 평가 한 번당 수이며 프레임당 횟수는 카메라/타깃/수동 Invoke 횟수에 따라 달라진다. UpdateShader의 장식별 수동 필터 실행까지 포함한 전역 N/frame은 런타임 계수가 필요하다.

### 안전한 구현 설계

1. **ID 치환**: 허용한 게임 메서드의 상수 속성 이름에만 준비 단계에서 프로세스별 ID를 만들고 int 오버로드로 치환한다. ID를 디스크에 고정 저장하지 않는다. 명령 순서·setter 수·Graphics.Blit·시간 누적은 그대로 둔다. IL 패턴/호출 수 검사가 실패하면 그 메서드를 원본으로 둔다.
2. **동일 값 생략**: float부터 작은 필터 목록으로 시작한다. 값 비교는 epsilon을 쓰지 않고 정확 비교로 한다. NaN 등 판정이 애매한 값은 원래 setter를 실행한다. vector/color는 성분별 비교, 텍스처는 우선 제외한다. _TimeX 같은 변화 값은 원래 경로로 두고 시간 누적을 건드리지 않는다.
3. **캐시 단위**: (소유 컴포넌트, 실제 Material 객체, property ID, 값 형식)의 **가장 최근 실제 쓰기 값**을 기록한다. 호출 위치별 “지난 프레임 값” 캐시는 금지한다. Glow의 A→2A→다음 프레임 A를 모두 보존해야 한다. 다중 패스는 최초 허용 목록에서 제외하는 편이 안전하다.
4. **무효화**: 재질/셰이더 교체, OnEnable/OnDisable/OnDestroy, 맵/씬 전환, 설정 끔, 모드 reload/Unload 때 지운다. 해상도·색공간·품질 변경 시 관련 값/인증도 폐기한다. 파괴된 Unity 객체는 관리 참조가 남을 수 있으므로 객체 참조 비교만으로 생존을 판단하지 않는다.
5. **다른 쓰기 경로**: 같은 재질의 SetFloat/Color/Vector/Int, CopyPropertiesFromMaterial, 셰이더 변경 및 외부 필터 패치 등을 조사해야 한다. 전체 게임 Material API에 캐시 패치를 붙이는 방식은 핫 패스/다른 모드 충돌 비용 때문에 첫 구현에서 피한다. 소유권을 확정할 수 없거나 외부 패치가 있으면 양보한다. 완전 탐지가 어렵다면 ID 치환만 제공한다.
6. **선제 계산의 한계**: 필터 시간·DOTween 필드·카메라/RT 크기는 재생 중 바뀐다. 입력 값 계산을 준비 단계에서 한 번만 하는 것은 부적합하다. 미리 할 수 있는 것은 ID, 타입/필드 접근자, 불변 테이블 준비다. 임의로 필터 셰이더를 합치는 것은 렌더 순서·색공간·RT 정밀도를 바꾸므로 별도 과제다.

FilterIdentity는 환경/RT 형식·크기·sRGB·AA와 중립값이 인증된 6종만 단순 Blit으로 우회한다. 기능 감사에서는 기본 끔·이득 ≤0.03ms로 분류되어 있다. 이번 안은 비중립 필터에서도 **같은 값을 넣는 호출만** 줄인다는 차이가 있다. Quartz의 SkipNoOpScreenFilters와 유사한 전체 우회는 새 구현으로 중복시키지 않는다. 이번에는 그 모드 코드를 조사하지 않았다.

## 4. 필터 외 반복 후보와 보류 이유

### 4.1 타일 색·알파: “같은 인자” 전역 생략 금지

FloorMeshRenderer.color setter는 scrFloor.ShaderProperty_Color 정수 ID로 쓰고 cachedColor도 갱신한다. ID 변환 제거 이득은 없다. FloorRenderer는 renderer.material을 보관하고 SetMaterial로 교체할 수 있어 외부 쓰기 가능성을 배제할 수 없다.

scrFloor.Update는 Glow/Rainbow/Blink/Switch/Volume에서 색을 쓴다. Switch는 구간 내 같은 두 색 중 하나를 반복 쓰므로 **한정 후보**다. Rainbow는 재질에서 읽은 이전 색의 HSV/alpha를 사용하므로 계산을 불변값으로 바꾸면 안 된다. Volume은 매 Update에서 Sequence를 만들고 pulse 조건에 따라 지연 콜백을 예약하는 경로도 있다. 값만 보는 캐시가 이벤트 순서를 바꾸지 않도록 주의한다.

scrFloor.LateUpdate의 알파 SetFloat는 이미 isFading(지난 opacity와 다름)일 때만 실행한다. 이를 “매 프레임 무조건 같은 알파를 쓰는 곳”으로 세지 않는다. 아이콘/외곽선 sprite setter도 lastIconSprite/lastIconOutlineSprite 비교 후 조기 반환한다. 새 동일 sprite 캐시는 중복이다.

새 실험은 Switch 타일만의 반복 비율과 재질 소유·쓰기 경로를 먼저 확인한다. cachedColor를 신뢰한 무조건 생략, SetTrackStyle 전체 생략은 금지한다. 과거 FloorFix 실패와 같은 전제를 재사용하지 않는다. 기존 FloorLateFix는 multiplanetLine 없는 타일의 불필요한 transform 읽기 4곳을 줄이는 기능이므로 이 후보와 다르다. 검증은 타일별 색/알파/아이콘/flash·렌더러 교체 시점과 전곡 최종 상태·화면을 함께 비교한다.

### 4.2 scrConductor: 히트음 외 예약을 묶을 여지

Update는 프레임마다 DSP/Unity 시계와 pitch를 읽고 곡 위치·입력·박자 알림을 갱신한다. 전체 Update를 준비 단계로 옮기거나 자동 플레이 때 생략할 수 없다.

게임은 약 5초 앞까지 extraTicksCountdown(sndHat), holdSoundsData(시작/종료 있는 음원 포함), hitSoundsData를 각각 개별 예약한다. 현재 HitSoundBatch는 마지막 hitSoundsData만 맡으며 SoundWarm은 PCM 믹싱이 아니라 최초 로드 예열이다. **추가 tick과 hold 예약 묶기**가 히트음 방식의 새 적용 후보다.

추가 tick의 동종 PCM/음량을 먼저 묶고 hold는 따로 검토한다. hold 종료 시각, 반복 음원, pitch, 재생 속도 변경, 일시정지/실패/중간 시작·재예약, 믹서 그룹과 효과음 설정을 보존해야 한다. Unity API/PCM 업로드는 메인 스레드, 불변 PCM 합산만 작업 스레드로 보낸다. 예산 초과/자료 불완전 시 원래 커서를 보존하고 원래 예약으로 되돌아가야 한다. 실제 예약량이 낮으면 작업 스레드·PCM 메모리·업로드 비용이 더 크므로 채택하지 않는다. 전곡 평균보다 예약 몰림 프레임의 최악 시간 감소 여부를 우선 본다.

### 4.3 Rewired·입력 폴링: 중첩을 합산하지 말 것

IL 경로는 InputManager_Base.Update → DoUpdate(UpdateLoopType.Update, UpdateLoopSetting.Update) → ReInput.akQfn… → YzAwSQu….HJLv…다. 난독화 클래스는 Rewired_Core.dll에 있고 controller.enabled와 currentFrame 등을 참조한다. 다른 모드의 정체불명 루틴으로 보지 않는다. 내부 모든 장치 처리 의미를 완전히 복원한 것은 아니다.

DoUpdate는 설정된 Update/FixedUpdate/OnGUI 루프 비트를 검사한다. 등록된 루프가 중복인지 런타임 확인할 수 있지만, Update와 FixedUpdate를 무조건 하나로 합치면 소비자별 입력 시점이 달라진다. DoUpdate 1.4% + ReInput 1.1% + 난독화 0.9%를 3.4%로 합치지 않는다.

scrController.UpdateInput은 비동기 키 큐를 시각 순으로 꺼내고 시각 그룹별 ProcessKeyInputs를 부른 뒤 프레임 끝에도 한 번 호출한다. ProcessKeyInputs는 PlayerControl 상태에서 플레이어의 Simulated_PlayerControl_Update를 수행한다. **키 큐가 비어도 자동 진행·hold·판정이 필요**하므로 “키 없음 → return”은 부적합하다. 입력 결과를 프레임 캐시하는 경우에도 시각 그룹과 key down/up, 다중 플레이어, 장치 교체, 포커스를 보존해야 한다. 낮은 기대 이득에 비해 위험이 크므로 보류한다.

### 4.4 GUIStyle/TMP/Canvas

h170 GUIStyle.GetPreferredSize/GetDimensions/IMGUITextHandle 0.8%는 같은 체인이다. 제공 표본에는 소유자 OnGUI가 없어 우리 설정창, UMM, 게임/라이브러리 UI 중 누구인지 확정하지 못했다.

정적 확인: SettingsWindow는 CalcSize 호출이 여러 곳 있으나 재생 중 닫힌 창은 비활성화하는 경로가 있다. PerfOverlay.OnGUI는 마우스 입력용이고 실제 그리기는 LateUpdate/uGUI이며, UiDraw.Width는 TextGenerator.GetPreferredWidth 결과를 이미 캐시한다. 따라서 이 0.8%를 “우리 모니터 OnGUI 글자 재기”로 단정하는 설명은 맞지 않는다.

최소 확인은 설정창/UMM 닫기, 모니터 0/1 조건과 해당 관리 호출 스택 재수집이다. 소유자가 우리 UI로 확인되면 (문자열, 폰트 객체/크기/스타일, 스킨, DPI/배율, 언어, 너비 제약) 키로 크기 캐시를 검토한다. TMP/Canvas의 드문 rebuild 끊김은 이 표본에 없어 미확인이다. TextFix/HitTextFade/HitMeterFade를 새 후보로 중복 제안하지 않는다. 해당 끊김이 재현될 때 별도 추적한다.

### 4.5 Object 생존 검사·DOTween·카메라/시차·입자

|영역|호출 구조 / 줄일 여지|기존 중복·위험·확인|
|---|---|---|
|IsNativeObjectAlive|op_Equality/Inequality/Implicit 아래에서 호출. h170 리프는 Equality 0.8%, Inequality 0.2%에서 옴|ReferenceEquals로 전역 교체하면 파괴된 Unity 객체를 정상으로 오인. 수명 보장 구간에서 같은 객체의 검사 한 번을 재사용하는 정도만 검토. Mod/게임 기여 분리 필요|
|DOTween|Update→TweenManager.Update→DoGoto→플러그인/콜백/엔진 쓰기. 트윈 수에 비례|DecoAnim/FloorAnim/TweenFix/ZeroTween/PairXY 등과 중복. 색/필터의 남은 트윈을 식별하기 전 전체 교체 금지. 종료·Kill·콜백·ease·독립 시간 대조 필요|
|scrParallax.SetTrans|Init 후 cachedTransform.position 읽기, 카메라 위치 또는 pixelRect/ScreenToWorldPoint 계산, endPosition 저장, position 쓰기. LateUpdate에서도 호출|동일 카메라 입력은 묶을 수 있으나 부모/타일·dontAlterX/Y·z·시차 offset을 보존해야 함. TightLate가 이미 카메라 읽기와 장식 적용을 묶고 있어 잔여 대상을 따로 계수|
|scrCamera|세 표본에 독립 포함 항목 없음|Get/Set/투영 행렬을 프레임 하나로 캐시하면 실행 순서·다중 카메라·렌더 패스 차이 위험. 새로운 비용 수치 없음|
|scrParticleDecoration.Update|autoPlay·재생 상태 처리 뒤 shape.scale, simulationSpeed×song.pitch 쓰기, 편집기 gizmo 처리|ParticleFix.SkipIdle가 두 값의 동일 쓰기를 이미 생략. LowPauseParticles/ParticleCatchup은 화면/시뮬레이션 정책 변경. 새 후보 아님|
|장식 UpdateShader|활성 cfpCache를 순회해 임시 RT/Blit, OnRenderImage 반사 Invoke, 마스크 재질 입력 쓰기|Camera 고급 이벤트만 있는 HELLO에는 자동으로 귀속할 수 없음. 장식 필터가 실제 있는 맵에서 typed delegate·스크래치 인자 재사용 후보. 예외 래핑/재진입·RT 수명 보존, 수치 미확인|

Arche의 position 쓰기 3.6%는 개별 장식의 최종 변환을 실제로 적용하는 비용을 포함한다. 히트음처럼 PCM으로 합산해 소수의 음원으로 줄이는 방식과 달리, 서로 다른 Transform의 최종 상태를 하나의 setter로 합칠 수 없다. 기존 optimization-36의 제한을 그대로 유지한다.

## 5. 검증 설계와 중단 기준

가장 먼저 “어느 호출을 얼마나 없앨 수 있나”를 계수하고, 실제 순이득은 계수기를 끈 별도 판에서 잰다. 고빈도 함수에 Stopwatch/Harmony 전역 패치를 겹쳐 원래 비용을 바꾸지 않는다.

- 허용 필터 몇 개의 원래 호출 위치에서 고정 배열의 정수 카운터만 증가시킨다. 호출자 식별용 StackTrace, reflection, 문자열 사전 키 생성, boxing, 프레임별 로그를 쓰지 않는다. 결과는 구간 끝 한 번 출력한다.
- Float/Color/Vector의 string/int 호출, 필터/타일/장식, 실제 쓰기/동일 값/변경 값/무효화를 나눈다. 활성 인스턴스 수는 구간 전후 스냅샷과 프레임별 최소/최대만 저장한다. 모든 필터에 수백 개 패치를 곡 시작에 설치하지 않는다.
- ID만 치환 / ID+제한 생략을 별도 설정으로 분리한다. 먼저 계수만 켬/끔에서 계수 자체의 오버헤드부터 확인한다. 메인 스레드 전체 프레임 표본과 PresentMon으로 CPU/GPU 병목을 구분한다.
- 같은 DLL/설정/해상도/맵/다른 모드 상태로 매 판 새 프로세스 ABBA를 최소 2묶음 비교한다. h50/h170/a262의 같은 구간과 전곡의 최악/p99/p99.9·끊김 횟수를 함께 본다. 평균 FPS가 올라도 순간 멈춤이 늘면 승격하지 않는다.
- 정확성은 필터 필드뿐 아니라 렌더 직전 Material 값·셰이더·RT 문맥·블렌드/패스 순서, 타일/장식 상태, 곡 끝 상태를 비교한다. decosig만으로 필터 화면 동일성을 증명할 수 없다.
- 결정적 시각/seed/해상도에서 원본과 같은 곡 시점의 화면 픽셀 차이를 비교한다. 노이즈/시간 필터는 같은 입력 시각·seed 조건의 오프라인 렌더 또는 기준 반복 변동 범위를 확보한다. 일시정지 시 화면이 같다는 것만으로 전곡을 인증하지 않는다.
- 재질 교체·OnDisable/Enable·씬 이동·실패/재시도·seek·해상도·속도 변경·모드 끔/reload/Unload·다른 모드 병용에서 캐시 복구를 검증한다. 패치/이벤트/정적 캐시는 Unload에서 되돌릴 수 있어야 한다.
- FloatImpl의 실제 호출자 귀속이 다르거나 동일 값 비율이 낮으면 필터 생략 가설을 내린다. 낮은 이득이 패치/비교/캐시 비용을 못 덮으면 ID 치환만 남기거나 모두 채택하지 않는다. 이번 문서는 어느 기능도 채택하지 않는다.

## 6. 입력 지문과 남은 불확실성

|입력|SHA256|
|---|---|
|Assembly-CSharp.dll|F692210F9A342F7EED4BE40AAD463286AB40BF046599959823942EA02F4CA3FF|
|Assembly-CSharp-firstpass.dll|CD7646B60F24AE46FA08794D4EC0B6BFEFEBA37DC2BD873419F2DF399C84677D|
|HELLO 원본 level.adofai|11945ECA2D39E1760A3D4B80325FD2497F6BC1C95666E4A8D2EC14D0E12B4BD8|
|prof-h50.txt|D7D118B20A9B1D2C3EECEE8DDDCAA1F920C59B11ABCD4F169843D2B78031C6EB|
|prof-h170.txt|05087740AF82531AB353AAE7B87266F03DA39B16262546A68527F6D37C621004|
|prof-a262.txt|6305FD74A02ECABF095E5EC645096F19AC63B2246DD2F13EA4527324782B3358|

현재 DLL 지문과 프로파일 당시 DLL의 일치 여부는 당시 지문이 없어 미확인이다. 정적 결과는 위 현재 DLL 기준이다. 여전히 모르는 핵심은 h170 FloatImpl 호출자별 기여, 실제 활성 필터/평가 횟수, setter 동일 값 비율, 문자열 변환의 독립 시간, 엔진 내부 동일 값 처리, 다른 모드의 재질 쓰기, GUIStyle 소유자다.

참고한 우리 구현: [FilterIdentity](../FilterIdentity.cs), [FloorLateFix](../FloorLateFix.cs), [ParticleFix](../ParticleFix.cs), [HitSoundBatch](../HitSoundBatch.cs), [TightLate](../TightLate.cs), [MoveApply](../MoveApply.cs), [SettingsWindow](../SettingsWindow.cs), [PerfOverlay](../PerfOverlay.cs), [UiDraw](../UiDraw.cs). 기존 범위와 제한은 [기능 감사](feature-audit-2.8.0.md), [optimization-36](optimization-36.md), [기존 Quartz 분석](quartz-analysis-20261019.md)을 대조했다.
