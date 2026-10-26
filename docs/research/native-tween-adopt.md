진행 중인 네이티브 트윈의 GPU 이어받기는 조건부로 구현 가능하다. PositionX 한 종류부터 검증할 수 있다.
`InCatchUp` 제한은 유지해야 한다. 원래 따라잡기는 `Goto`로 시간을 보정하지만 현재 DecoAnim 대역에는 이 처리가 없다.
핵심 위험은 원래 콜백의 의미, 활성화 실패 시 원본 복구, 첫 갱신 프레임과 복원 순서이며, 현재 코드는 이 경로를 지원하지 않는다.
큰 이미지 52개를 숨겼을 때의 +17~21%를 이어받기의 예상 이득으로 사용할 수 없다. 같은 이미지를 계속 그리면 픽셀 처리 비용은 남는다.
최소 실험은 검증된 원래 PositionX 트윈 1개를 같은 일시정지 경계에서 캡처·왕복하는 것이다. 이번에는 게임·구현·커밋 없이 정적 조사와 오프라인 검사만 했다.

# 네이티브 트윈 GPU 이어받기 타당성 조사

조사일: 2026-10-26. 작업 기준: `claude/native-adopt-research`, HEAD `5e8ea58c54ff1fa574f0262958792345720824a9`.
요구사항: `docs/handoff/native-tween-adopt.md:3–20`. 기존 소스 수정, 게임 실행, 커밋, 비공개 헤르츠 맵 열기는 하지 않았다. 새 파일은 이 문서와 `tools/adopt-lab/` 아래뿐이다.

## 1. 근거와 재현 범위

이 문서에서 `D/`는 `tools/adopt-lab/decompiled/`를 뜻한다. `파일:줄–줄`은 이번 체크아웃 또는 보존한 디컴파일 파일의 줄 번호다. DLL의 C#은 ILSpy가 복원한 표현이고 원본 개발 소스의 줄 번호가 아니다. API와 콜백을 DLL에서 직접 확인했으며, 실제 실행 중인 트윈의 값과 다른 모드의 패치 상태는 확인 못 함.

| 대상 | 확인 결과 | 근거 |
|---|---|---|
| 게임 DLL | Assembly-CSharp, 파일 버전 0.4.3.0, 2,706,432바이트 | `tools/adopt-lab/assembly-manifest.json:2–7` |
| DOTween Core | **1.2.765**, `DOTween.dll` 175,616바이트 | `D/DG.Tweening.DOTween.decompiled.cs:13`, manifest:9–14 |
| DOTween Pro | **1.0.381**, `DOTweenPro.dll` 16,384바이트가 함께 설치됨 | `D/DG.Tweening.DOTweenPro.decompiled.cs:3–6`, manifest:16–21 |
| 도구 | ILSpy/Decompiler 9.1.0.7988 | `tools/adopt-lab/ilspy-version.txt:1–2` |
| 게임 모듈 MVID | `c0baa18f-e115-4186-bf86-1b2288496232` | `tools/adopt-lab/probe-result.json`, `gameMvid` |

두 DOTween DLL의 파일 버전은 모두 1.0.0.0이므로 제품 버전으로 사용하면 안 된다. 위 제품 버전은 각각 DLL 내부 `Version` 필드의 값이다. 장식 트윈 생성은 Pro의 경로나 애니메이션 컴포넌트가 아니라 Core의 `DOTween.To`를 사용한다. Unity 6000.3/Mono라는 환경은 인계 문서·`CLAUDE.md:18`의 전제이며 이번에 게임 프로세스로 재확인하지 않았다.

SHA-256은 manifest에 전체 보존했다. 게임 DLL은 `F692210F9A342F7EED4BE40AAD463286AB40BF046599959823942EA02F4CA3FF`, Core는 `8E90FC43AB564A5A51424AF56531AB074A5A2E9CAB271B2D653D287954237B83`, Pro는 `DB5225F6DB87969A03B48A56E2DC3828B38384ADCA44DA0F89248A2BB483B056`이다.

### 실행한 검사

- ILSpy MCP로 관련 형식 21개의 C#을 추출했다. 게임 코드 실행 없이 DLL 파일을 읽는 작업이다.
- `collect-evidence.ps1 -SkipDecompile -IncludeIl`로 해시·도구 버전과 MoveDecorations의 IL을 수집했다. ILSpy 9.1의 `--ilcode -t`는 여기서 전체 어셈블리를 출력하므로 스크립트가 대상 형식과 중첩 클로저만 남긴다.
- 별도 .NET 8 콘솔은 `DOTween.Init/To`, Unity 객체 생성, 게임 어셈블리 로드를 하지 않는다. 게임 DLL은 `PEReader`로 읽는다. 네이티브 엔진 호출 없이, 초기화되지 않은 합성 `TweenerCore<float,float,FloatOptions>`의 필드를 넣어 API 계약을 검사했다.
- 결과: Tween 필드 **35개**의 존재·접근 수준, 제네릭 공개 필드 **6개**, 상태 API **7개** 확인. 이징 표본 **20개**의 값·비트를 기록했고, MoveDecorations의 `StartEffect`와 클로저 메서드 **43개**의 토큰·IL 해시를 추출했다. 빌드는 경고 0·오류 0, 검사 프로세스 종료 코드 0.
- 이것은 필드 접근과 순수 계산의 .NET 8 증거다. Unity Mono의 JIT, 실행 중 콜백 출처, GPU 계산·픽셀·FPS, 실제 활성화·복구 성공은 **확인 못 함**.

### 재현 명령 — 게임을 실행하지 않음

작업 폴더에서 PowerShell로 실행한다. 첫 명령은 DLL C# 전체를 다시 추출하며 시간이 걸린다. 이번 최초 C# 추출은 동일 버전의 ILSpy MCP로 했고, 아래 스크립트의 C# 일괄 재추출 분기는 아직 일괄 실행하지 않았다. `-SkipDecompile -IncludeIl` 분기와 아래 빌드·검사는 실행했다.

```powershell
& tools/adopt-lab/collect-evidence.ps1 -IncludeIl
# 기존 C# 추출물을 두고 manifest/IL만 재수집하려면:
& tools/adopt-lab/collect-evidence.ps1 -SkipDecompile -IncludeIl

dotnet build tools/adopt-lab/probe/AdoptProbe.csproj -c Release --nologo -v q --ignore-failed-sources
if ($LASTEXITCODE -ne 0) { throw 'build failed' }
dotnet tools/adopt-lab/probe/bin/Release/net8.0/AdoptProbe.dll |
    Set-Content -LiteralPath tools/adopt-lab/probe-result.json -Encoding UTF8
if ($LASTEXITCODE -ne 0) { throw 'probe failed' }

# 특정 근거의 줄 번호를 확인하는 예:
rg -n 'ScrubToTime|Goto|Kill|num2' tools/adopt-lab/decompiled/ffxPlusBase.decompiled.cs
rg -n 'ClassifyGpuActivationOwnership|whyNative|RollbackGpuActivation' GpuDecoActivation.cs
git diff --name-only
git status --short
```

DLL 경로가 다르면 수집 스크립트의 `-Managed`, 프로젝트의 `-p:ManagedDir=...`, 검사 실행 시 첫 인자(게임 DLL 경로)를 각각 지정한다. 게임 설치 폴더에 쓰는 명령은 없다. 모드 본체를 빌드하거나 설치하는 명령도 없다.

## 2. 원래 장식 트윈의 정체

`scrDecoration.eventTweens`는 공개 `Dictionary<global::TweenType,DG.Tweening.Tween>`다(`D/scrDecoration.decompiled.cs:67`). `TweenType`은 장식 키 열거형이며 DOTween 내부의 동명 열거형과 구분해야 한다(`D/TweenType.decompiled.cs:1–28`).

`ffxMoveDecorationsPlus.StartEffect(scrPlanet)`는 다음 순서다(`D/ffxMoveDecorationsPlus.decompiled.cs:118–378`).

1. 레거시 단일 크기값을 Vector2로 확장하고 `AdjustDurationForHardbake()`를 호출한다.
2. 태그 대상마다 `eventTweens`를 얻고, 필요하면 배치 타입을 바꾼다.
3. 위치 X/Y → 시차 오프셋 X/Y → 피벗 X/Y → 회전 → 크기 X/Y → 색 → 불투명도 → 시차 배율 순으로 처리한다.
4. 같은 키의 이전 트윈에 **`Kill(true)`**를 호출한 뒤 새 `DOTween.To(...).SetEase(ease)...Done()`을 저장한다. 위치 기준점은 이전 트윈 완료 **전**, 실제 새 시작값은 완료 **후** 캡처한다. 이 차이는 상대 위치에서 중요하다.
5. 보이기·깊이·이미지 등은 별도 즉시 처리다. 모두 GPU 채널로 바꿀 수 있다는 뜻은 아니다.

### 종류별 값·콜백·채널

`F = TweenerCore<float,float,FloatOptions>`, `V = TweenerCore<Vector2,Vector2,VectorOptions>`, `C = TweenerCore<Color,Color,ColorOptions>`다. 다음 채널은 `CaptureInitialGpuLiveState`의 실제 16채널 배열과 일치한다(`GpuDecoExport.cs:1417–1432,1475–1497`).

| TweenType 값 / GPU 채널 | 실제 형식 / getter·setter | 목표값과 콜백 | 생성 근거 |
|---|---|---|---|
| PositionX=1 → 0 / PositionY=2 → 1 | F, 클로저 `newPos.x/y`를 읽고 그 축만 클로저에 쓴다 | `startPos.axis + targetPos.axis`. OnUpdate는 `SetPositionX/Y(newPos.axis, dec.pivotOffsetVec)`, OnComplete는 목표를 재대입 | D/ffxMoveDecorationsPlus:148–188 |
| PivotX=3 → 2 / PivotY=4 → 3 | F, 캡처한 `pivotOffsetVec` 축 | `targetPivot.axis`. OnUpdate/OnComplete에서 `SetPivotX/Y` | 같은 파일:233–274 |
| Rotation=5 → 4 | F, 캡처한 `rotAngle` | `targetRot`. OnUpdate/OnComplete에서 `SetRotation`. 최단각 회전 플러그인이 아니다 | 같은 파일:276–295 |
| ScaleX=7 → 5 / ScaleY=8 → 6 | **V**, getter는 매 호출 시 `dec.scaleVec`, setter는 `dec.SetScale(v)` | 목표 Vector2는 `targetScaleV2` 그대로. `SetOptions(AxisConstraint.X/Y)`. **OnUpdate/OnComplete 없음** | 같은 파일:297–322 |
| Color=9 → 7,8,9,10 | C, 캡처한 `dec.color` | `targetColor`. OnUpdate/OnComplete에서 `SetColor`, RGBA 네 성분 | 같은 파일:324–342 |
| Opacity=10 → 11 | F, 캡처한 `dec.opacity` | `targetOpacity`. OnUpdate/OnComplete에서 `SetOpacity`. 색 alpha와 별도 채널 | 같은 파일:344–362 |
| Parallax=11 → 12,13 | V, 캡처한 `dec.parallax.multiplier` | `targetParallax / 100f`. setter가 multiplier 전체에 직접 쓴다. 콜백 없음 | 같은 파일:364–374 |
| ParallaxOffsetX=12 → 14 / Y=13 → 15 | F, 캡처한 `dec.parallaxOffset` 축 | `targetParallaxOffset.axis`. OnUpdate/OnComplete에서 `SetParallaxOffsetX/Y` | 같은 파일:190–230 |
| Position=0 / Scale=6 / Glow | 이 StartEffect에서는 생성하지 않음 | 이름만 보고 0/6을 X/Y 묶음으로 처리하면 안 됨. 이번 대상 밖이며 CPU 유지 | D/TweenType:1–28, 위 StartEffect 전체 |

표의 `D/ffxMoveDecorationsPlus:...`는 확장자 `.decompiled.cs`를 생략한 것이다. 색 setter는 `color` 저장 후 `ApplyColor`, 불투명도는 `opacity` 저장 후 `ApplyColor`다. 위치 setter는 피벗·screen clamp·Transform 갱신까지 수행한다. 단순 필드 대입과 같은 부작용이 아니다(`D/scrDecoration.decompiled.cs:229–279,298–325`).

**이 StartEffect에는 `SetDelay`, `SetRelative`, `SetLoops`, 사용자 `OnKill` 등록이 없다.** 그러나 런타임 기본값이나 다른 모드가 바꿀 수 있어 객체 검사 없이 기본 상태를 확정하면 안 된다. `Tweener.Setup`은 `DOTween.defaultAutoKill/defaultRecyclable/defaultEase*/defaultLoopType/defaultAutoPlay`를 복사한다(`D/DG.Tweening.Tweener.decompiled.cs:30–59`).

`movementType == LastPosition`의 상대 이동은 **DOTween.isRelative가 아니다.** 게임이 `dec.pivotPosVec`를 기준점으로 골라 절대 목표를 계산한 뒤 일반 To를 만든다. 나머지 위치는 `dec.startPos`가 기준이다(`D/ffxMoveDecorationsPlus.decompiled.cs:140–159`). 따라서 이미 초기화된 트윈의 절대 start/end/change를 읽으면 LastPosition을 재해석할 필요가 없다.

`Done()`은 이 게임에 들어 있는 Core 확장의 메서드다. `duration <= 0`일 때만 즉시 Complete하고, 양수이면 그대로 반환한다. 남은 시간으로 바꾸거나 delay를 추가하지 않는다(`D/DG.Tweening.TweenExtensions.decompiled.cs:45–76`). 이벤트 duration의 원래 단위는 `evnt.GetFloat("duration") * crotchet`로 초가 되고, hardbake 조정도 있으므로 활성화 때 원래 이벤트 duration을 다시 계산하면 안 된다(`D/ffxMoveDecorationsPlus.decompiled.cs:435`, `D/ffxPlusBase.decompiled.cs:179–184`).

## 3. 런타임 상태를 정확히 읽는 방법

다음은 설계 예시이며 기존 소스에는 적용하지 않았다. 공개 필드와 API를 우선하고 내부 필드는 선언 형식을 고정해서 읽는다. 필드가 없거나 형식이 다르면 CPU 처리로 남긴다.

```csharp
const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
static T Read<T>(Tween t, string name) =>
    (T)(typeof(Tween).GetField(name, F)
        ?? throw new MissingFieldException(typeof(Tween).FullName, name)).GetValue(t);

// active인 상태에서 Kill 전에 읽는다. 읽기 위해 ForceInit/Goto를 호출하지 않는다.
float pos = t.Elapsed(false);       // 한 루프의 position, 지연시간 제외
float dur = t.Duration(false);      // 한 루프 duration
float delay = t.Delay(), elapsedDelay = t.ElapsedDelay();
int loops = t.Loops(), completedLoops = t.CompletedLoops();
bool initialized = t.IsInitialized(), playing = t.IsPlaying();
Ease ease = Read<Ease>(t, "easeType");
EaseFunction custom = Read<EaseFunction>(t, "customEase");
float over = t.easeOvershootOrAmplitude, period = t.easePeriod;
bool relative = t.isRelative, backwards = t.isBackwards;
// onStart는 내부 기반 클래스에 선언되어 있다.
var onStart = typeof(ABSSequentiable).GetField("onStart", F).GetValue(t);

if (t is TweenerCore<float,float,FloatOptions> f) {
    float start = f.startValue, end = f.endValue, change = f.changeValue;
    bool snapping = f.plugOptions.snapping;
}
```

| 상태 | 정확한 접근자 / 형식 | 근거 |
|---|---|---|
| start/end/change, getter/setter/options | 앞의 F/V/C 형식으로 cast. `startValue/endValue/changeValue/plugOptions/getter/setter`는 모두 public | D/DG.Tweening.Core.TweenerCore`3.decompiled.cs:10–23 |
| 경과·길이 | `Elapsed(false)` / `Duration(false)`. 인자 생략 시 includeLoops=true | D/DG.Tweening.TweenExtensions.decompiled.cs:655–695 |
| delay / elapsedDelay | 공개 `Delay()` / `ElapsedDelay()`, 같은 이름의 내부 float 필드도 존재 | 같은 파일:629–653 |
| ease/custom | Tween 내부 `easeType:Ease`, `customEase:EaseFunction`. Instance/NonPublic 반사 또는 기존 `AccessTools.FieldRefAccess<Tween,T>` 사용 | D/DG.Tweening.Tween.decompiled.cs:67–71, ZeroTween.cs:109–112 |
| overshoot / period | public float `easeOvershootOrAmplitude`, `easePeriod`. 해당 객체에서 읽고 기본값으로 재생성하지 않음 | Tween:73–75 |
| relative/backwards/timeScale | public `isRelative` getter / `isBackwards:bool` / `timeScale:float` | Tween:10–12,119 |
| loops/completedLoops | 공개 `Loops()` / `CompletedLoops()`. `loopType:LoopType`은 내부 필드 | TweenExtensions:616–627,814–825, Tween:61–63 |
| 초기화·재생·완료 | `IsInitialized()`, `IsPlaying()`, `IsComplete()`, 내부 `startupDone/isPlaying/isComplete` | TweenExtensions:775–812, Tween:101–113 |
| 갱신 방식 | 내부 `updateType:UpdateType`, `isIndependentUpdate:bool`. 개별 timeScale과 함께 읽음 | Tween:24–26, D/DG.Tweening.Core.TweenManager.decompiled.cs:514–548 |
| 기타 형태 | 내부 `isSequenced`, `sequenceParent`, `isFrom`, `isBlendable`, `isInverted`, `isSpeedBased`, `specialStartupMode`, `autoKill`, `isRecyclable` | Tween:14,46–57,85–97 |
| 콜백 | public `onPlay/onPause/onRewind/onUpdate/onStepComplete/onComplete/onKill/onWaypointChange`. `onStart`는 `typeof(DG.Tweening.Core.ABSSequentiable)`의 내부 필드 | Tween:28–44, D/DG.Tweening.Core.ABSSequentiable.decompiled.cs:11 |

표의 Tween/TweenExtensions는 `D/DG.Tweening.이름.decompiled.cs`를 줄인 것이다. `active/position/isRelative/playedOnce`는 public getter와 비공개 setter를 가진 속성이다. 읽기에는 반사가 필요 없다. `"active"`라는 필드를 찾으면 안 된다. 합성 검사에서 사용한 `<active>k__BackingField` 쓰기는 본래 트윈을 읽는 구현에 사용하지 않는다.

### 초기화 전·지연 중 상태

`startValue/changeValue`는 생성 직후 미확정일 수 있다. `Tweener.DoStartup`이 getter를 호출하고 relative 종점 보정과 `SetChangeValue`를 실행한다(`D/DG.Tweening.Tweener.decompiled.cs:75–128`). 특히 Scale은 그 시점의 scaleVec가 시작값이다. `ForceInit()`을 읽기 작업처럼 호출하면 시작값 확정 시점이 바뀐다. 첫 대상은 **IsInitialized()==true**로 한정하고, 기존 Scale Rec의 Started 보정을 네이티브에 그대로 적용하지 않는다.

많은 상태 API는 inactive일 때 0/false를 반환한다. **Kill 전에 전부 저장**한다. Elapsed에 delay를 더하거나 Duration에서 Elapsed를 빼 새 곡선을 만들지 않는다. Goto는 delay를 완료 상태로 바꾸므로 delay/elapsedDelay/내부 delayComplete도 확인한다(`D/DG.Tweening.Core.TweenManager.decompiled.cs:852–858`).

### 콜백은 유무만으로 판정할 수 없음

Position/Color 등의 To setter는 클로저 변수만 바꾸며, **OnUpdate가 실제 장식에 반영하는 본체**다. OnComplete는 목표값을 다시 대입한다. 콜백을 모두 금지하면 주요 후보를 가져올 수 없고, 이름만 보고 허용하면 다른 모드의 부작용을 버릴 수 있다.

허용 후보는 해당 DLL에서 확인한 **getter/setter/OnUpdate/OnComplete를 한 묶음으로 식별**한 경우다.

- `Delegate.GetInvocationList()` 길이, `Method.Module.ModuleVersionId`, `MetadataToken`, 필요하면 IL 해시를 대조한다. 이름에 StartEffect가 들어가거나 `t.target==d`라는 이유만으로 허용하지 않는다. 이 To에는 SetTarget이 없어 target이 null일 수 있다.
- 생성 클로저 형식과 알려진 필드의 참조 사슬을 따라 마지막 `dec`가 사전 소유자와 같은 객체인지 확인한다. 임의 속성이나 delegate는 호출하지 않는다. Fx 참조와 캡처 기준점도 대조한다.
- 추가된 다중 delegate, 알 수 없는 getter/setter, 다른 수명주기 콜백이 있으면 CPU 유지다. `onKill/onPause/onPlay/onStart`를 임의로 null로 바꿔 통과시키지 않는다.
- 제네릭 형식이 같더라도 내부 `tweenPlugin`이 원래 FloatPlugin/Vector2Plugin/ColorPlugin인지 별도로 확인한다. 사용자 플러그인은 계산·Reset 의미가 다를 수 있다(`D/DG.Tweening.Core.TweenerCore`3.decompiled.cs:25,160–170`, `D/DG.Tweening.Tweener.decompiled.cs:30–47`).
- 일부 OnComplete는 변경 가능한 Fx 목표 필드를 다시 읽는다. PositionX라면 캡처 startPos.x와 현재 Fx.targetPos.x의 합이 추출 endValue와 비트까지 일치하는지 확인한다. 달라졌다면 종점 스냅샷만으로 같은 완료를 재현할 수 없어 제외한다.

이번 설치판의 PositionX 식별 정보:

| 용도 | 생성 형식 / 메서드 | MethodDef token |
|---|---|---|
| getter | `ffxMoveDecorationsPlus+<>c__DisplayClass47_2.<StartEffect>b__0` | `0x06002EEA` |
| setter | 같은 형식의 `<StartEffect>b__1` | `0x06002EEB` |
| OnUpdate | 같은 형식의 `<StartEffect>b__2` | `0x06002EEC` |
| OnComplete | `ffxMoveDecorationsPlus+<>c__DisplayClass47_1.<StartEffect>b__3` | `0x06002EE1` |

getter/setter/update의 Target은 같은 47_2 객체이고 `CS$<>8__locals2`→47_1→`CS$<>8__locals1`→47_0의 dec로 연결된다. Complete의 Target은 대응하는 47_1이다. IL 근거: `D/ffxMoveDecorationsPlus.il:315–340,507–560`. 토큰·IL 해시는 `probe-result.json`의 callbackMethods에 보존했다. 이는 이 DLL판의 식별 후보이며 실행 중 delegate를 대조한 결과는 아니다.

## 4. 중간 시작과 따라잡기 의미론

### 게임의 실제 경로

```text
scrController.Start_Rewind
  → conductor.StartMusic(..., OnMusicScheduled)
  → checkpointNum != 0: OnMusicScheduled → Scrub(checkpointNum, ...)
  → 시작 타일과 4박 전/카운트다운을 고려한 시각 num4 계산
  → conductor.ScrubMusicToTime(num4)
  → scrVfxPlus.ScrubToTime((float)num4)
  → 미실행 효과별 ffxPlusBase.ScrubToTime(t)
  → StartEffect() → Kill(true) 또는 Goto(경과, andPlay:true)
```

근거: `D/scrController.decompiled.cs:1496–1594,2478–2536`, `D/scrVfxPlus.decompiled.cs:260–282`.

`ffxPlusBase.ScrubToTime(t)`의 동작(`D/ffxPlusBase.decompiled.cs:123–165`):

- `t < startTime`이면 아무것도 하지 않는다.
- 그 외에는 먼저 StartEffect(), 이어 triggered=true로 설정한다.
- `t >= startTime+duration`이거나 그 끝과 Mathf.Approximately이면 eventTweens를 Kill(true)로 완료한다.
- 아직 진행 중이면 **`num2=(t-(float)startTime)/cond.song.pitch - scrConductor.calibration_i`**를 계산하고 active 대상에 `Goto(num2,true)`를 호출한다. num2가 거의 0이 아니면 pausedTweens에도 추가한다.
- Goto는 내부 ForceInit 후 값을 적용한다. 일반 Goto도 OnUpdate를 호출한다. withCallbacks=false는 모든 콜백을 금지한다는 뜻이 아니다(`D/DG.Tweening.TweenExtensions.decompiled.cs:145–180`, `D/DG.Tweening.Tween.decompiled.cs:267–302`).
- PlayerControl_Enter는 pausedTweens의 active 트윈을 Play하고 목록을 지운다. 목록 이름만으로 개별 isPlaying=false라고 단정할 수 없다(`D/scrController.decompiled.cs:511–529`).

따라서 진행 중인 과거 효과는 **원래 duration을 유지하고 중간 위치로 Goto한 트윈**이다. 남은 길이로 새 곡선을 만드는 방식이 아니다. 평상시 scrVfxPlus.Update는 도달한 효과에 StartEffect를 호출하므로 Scrub과 구분해야 한다(`D/scrVfxPlus.decompiled.cs:182–232`).

### 60프레임은 모드의 보호 기간

`EffectBudget.Suspend(3f)`는 실시간 유예와 `graceFrame=Time.frameCount+60`을 설정하고 InCatchUp은 후자만 본다(`EffectBudget.cs:53–60`). 재생 준비와 SongStarted에서 Suspend가 호출된다(`GcControl.cs:342–343`, `Hitch.cs:249`). 경계는 `<=`다. 이는 **모드의 보호 기간**이며 게임이 반드시 60프레임에 나눠 Scrub한다는 증거가 아니다. 게임 ScrubToTime은 효과 목록을 동기적으로 순회한다.

제품 GPU 기동도 곡 위치 0.5초 이후에는 InCatchUp이 끝날 때까지 기다린다(`GpuDecoPlayer.cs:120–136`). 이 뒤에도 긴 네이티브 트윈이 남을 수 있어 이번 문제가 발생한다.

### InCatchUp 해제 후 화면 44% 차이의 가설

강한 가설은 과거의 양수 길이 효과가 DecoAnim으로 바뀌면 사전에 **active=false인 Proxy**가 들어가기 때문이다. 게임 Scrub은 active를 검사하고 Goto하므로 이 Proxy에는 Goto하지 않는다. 반면 Rec는 Pos=0에서 모드 목록으로 진행한다. 이 버전 DecoAnim.Install은 Kill/Complete/PlayingTweens를 다루지만 **Goto는 패치하지 않는다**. 과거 효과가 같은 장식에 겹치면 뒤 효과의 시작값도 바뀐다(`FastMove.cs:378–380,630–639`, `DecoAnim.cs:130–177,219–253,546–569`).

이미 끝난 효과의 Kill(true)는 DecoAnim에도 연결되므로 특히 시작점을 가로질러 계속되는 효과의 시계가 달라진다고 설명할 수 있다. **44% 이미지 차이 재현, GPU가 켜지지 않은 예외의 특정, 인과관계 확정은 확인 못 함.** 숫자는 인계 문서:8의 기존 보고다. InCatchUp 조건은 유지하는 설계를 제안한다.

## 5. 기존 GPU 인계와의 차이 및 설계

### 현재 코드의 경계

ClassifyGpuActivationOwnership은 active인 비Rec 트윈을 하나라도 찾으면 **장식 전체를 CPU fallback**으로 남긴다. 독립 renderer가 필요하고 GPU owned set에서 제외한다(`GpuDecoActivation.cs:20–64`). CaptureInitialGpuLiveState는 실제 장식의 16채널을 static 값으로 초기화한 뒤 fallback을 건너뛴다. 안전한 Rec만 undo 저장→Kill(false)→원래 Pos/Dur/Ease 등의 GPU 소스로 바꾼다. 여기서 native가 발견되면 예외다(`GpuDecoExport.cs:1405–1499`).

분류만 풀면 Capture의 예외에 걸린다. Capture만 늘리면 rollback은 native를 저장하지 않는다. **분류·추출·undo·복원·진단을 함께 설계**해야 한다. 기존 undo는 Rec의 GpuResumeSpec과 Started를 보관해 Rec를 재생성할 뿐 원래 native 객체를 복구하지 않는다(`GpuDecoActivation.cs:13–16,67–110`).

진단 whyNative와 키별 건수는 **장식마다 첫 거절에서 break**한다. **227–231은 장식 수이며 전체 트윈 수가 아니다. PositionX=197도 PositionX만 가진 장식 197개라는 뜻이 아니다.** 같은 장식에 Color나 미지원 키가 남으면 PositionX를 가져와도 GPU 소유가 될 수 없다. 대형 이미지 52개 중 PositionX만으로 가져올 수 있는 수는 확인 못 함.

### A. 읽기·판정 계획을 먼저 작성

1. 기존 Rec / 안전 후보 native / CPU 유지로 분류한다. **해당 장식의 모든 진행 중 entry**를 검사하고 하나라도 처리할 수 없으면 장식 전체를 CPU에 남긴다. 기존 Rec의 불일치·미지원도 확인한다.
2. 임시 NativeAdoptSpec에 D/Key, 원래 Tween·사전 참조, 구체 형식, 원래 start/end/**change**, Pos/Dur/Ease/Over/Period, options, playing, 업데이트 단계, 콜백 출처, 순서 관측 여부를 저장한다. 이벤트 시각으로 추측하지 않는다.
3. 사용 성분의 유한성, 0<=Pos<Dur, duration>0, 현재값과 추출 곡선의 일치를 검사한다. `InvisibleSkip.ApplyAllLazy()` 이후 경계를 이용한다. 다르면 현재값을 새 start로 꾸미지 말고 제외한다.
4. source 수·채널·복원 spec·사전 대체 객체를 미리 확보한다. 용량은 8192이며 참조 중 source를 재사용하면 안 된다(`GpuDecoExport.cs:204,453–469`).

분류와 캡처 사이에도 Tween 참조·active·세대가 같은지 확인한다. 외부 변경이 보이면 소유권을 옮기기 전에 거절한다.

### B. 소스와 채널의 값

```text
source.Pos    = native.Elapsed(false)
source.Dur    = native.Duration(false)        # 원래 전체 duration
source.Ease   = native.easeType
source.Over   = native.easeOvershootOrAmplitude
source.Period = native.easePeriod
channel.start = native.startValue[성분]
channel.change= native.changeValue[성분]      # float로 저장된 변화값
channel.end   = 원래 코드가 완료 시 남기는 값
anchor.target = native.endValue[성분]
```

F/Color는 OnComplete가 목표를 강제하므로 end는 완료 목표다. Scale/Parallax에는 OnComplete가 없으므로 **원래 플러그인과 같은 산술의 `start+change*Eval(E,Dur,Dur,Over,Period)`**가 end다. 이징 끝이 항상 1이라고 가정하지 않는다. 축 Scale은 그 축만, Color 4성분과 Parallax 2성분은 같은 source에 둔다. Position에 startPos를 다시 더하거나 Parallax를 다시 100으로 나누지 않는다.

현재 SetMasterSlotAnimated는 change를 target-start로 계산한다(`GpuDecoExport.cs:561–581`). 첫 구현에서는 추출 change와 재계산 change의 비트 일치를 요구하거나 명시적인 change 입력 경로를 새로 설계해야 한다. 불일치를 무시하지 않는다. 현재값을 static으로만 복사하면 이후 애니메이션이 멎는다.

처음에는 **native Tween 하나당 source 하나**가 안전하다. Ease/Duration/Elapsed가 같다는 이유만으로 공유하지 않는다. native에는 원래 이벤트 ID가 없어 기존 Rec의 GpuSource 묶음을 추정할 수 없다. SourceId는 Rec/이벤트 ID와 충돌하지 않게 배정하고 StartSong은 캡처 시점 참고 정보로 둔다. 공유 최적화는 추출 검증 후 별도 작업이다.

### C. 첫 업데이트 시점

DOTween은 Component.Update에서 smooth/raw deltaTime×DOTween.timeScale을 계산하고 TweenManager에서 개별 Tween.timeScale을 곱한다. 현재 GPU live는 공통 dt로 Pos를 진행하며 개별 timeScale을 저장하지 않는다(`D/DG.Tweening.Core.DOTweenComponent.decompiled.cs:61–79`, `D/DG.Tweening.Core.TweenManager.decompiled.cs:514–548`, `GpuDecoExport.cs:615–652`).

첫 대상은 Normal/scaled/timeScale=1/전진 1회로 제한한다. 곡 시각과 startTime의 차를 다시 계산하면 pitch/calibration/Goto와 dt 누적 이력을 잃는다.

**시작 프레임을 무조건 현재 프레임으로 쓰면 안 된다.** 이미 native가 갱신된 프레임이면 dt를 다시 더하지 않고, 갱신 전이면 그 프레임의 dt를 한 번 이어받아야 한다. 현재 StartGpuLive의 `gpuLiveLastSourceStepFrame=Time.frameCount`는 일시정지 ON 경계를 보호하지만 자동 기동의 native 시점까지 증명하지 않는다(`GpuDecoExport.cs:1549–1557`). GpuCpuUpdateFrame은 DOTween.Update의 **prefix**에서 설정된다(`DecoAnim.cs:660–676`).

최소 실험은 일시정지 경계로 제한한다. 재개 프레임부터 정확히 한 번씩 진행하는지 확인한다. 자동 기동을 구현할 때는 prefix 내부/본체 이후/late/pre-cull 단계를 구분해 검증한다. prefix의 플래그만으로 native 본체 갱신 완료를 추정하지 않는다.

### D. 콜백 없이 원래 트윈 제거

**Kill(false)는 OnComplete를 실행하지 않지만 onKill은 실행할 수 있다.** 업데이트 루프 밖이면 Despawn이 onKill을 부르고 루프 중이면 active=false 후 나중 정리에서 호출될 수 있다(`D/DG.Tweening.TweenExtensions.decompiled.cs:183–215`, `D/DG.Tweening.Core.TweenManager.decompiled.cs:275–282`).

첫 조건은 onKill==null이며 알 수 없는 link/destroy 관리도 제외한다. 설치판의 link 여부는 Tween 어셈블리에서 `DG.Tweening.Core.TweenManager` 형식을 얻고, 정적 비공개 `_TweenLinks` 사전을 읽어 `System.Collections.IDictionary.Contains(t)`로 확인할 수 있다. 접근 실패도 미확인으로 제외한다(`D/DG.Tweening.Core.TweenManager.decompiled.cs:78,404–410,432–434`). 알려진 OnUpdate/OnComplete는 호출하지 않고 그 의미를 start/change/end에 옮긴다. Kill(true)/Complete/Goto(Dur)는 완료 위치로 뛰거나 콜백을 실행하므로 인계 수단으로 쓰지 않는다. 삭제 지연이나 재활용이 있으므로 죽인 Tween을 되살릴 객체로 사용하지 않는다.

### E. 활성화 실패 시 복구

**먼저 Kill하고 active=true만 되돌려 원본을 복구하는 설계는 불가**다. Despawn은 Reset/풀 재사용과 manager 목록 변경을 동반한다. 원래 참조와 시작값만 저장해도 부활하지 않는다. 기존 RollbackGpuActivation에도 native 부활 경로가 없다.

첫 방식은 일시정지한 동기 경계에서의 두 단계 처리로 제안한다.

1. native 사전 entry와 객체를 유지한 채 읽기·검사·GPU 준비·기존 Rec undo·첫 dispatch까지 수행한다. 도중에 yield하지 않는다. 일시정지 중이라면 native에 Pause를 다시 호출할 필요도 없다. 재생 중 경계까지 일반화하려면 onPause/onPlay가 null임을 확인한 정지·재개와 정확한 업데이트 단계 관리가 추가로 필요하다.
2. 여기서 실패하면 GPU를 해제하고 renderer와 기존 Rec를 undo한다. native는 아직 Kill하지 않았으므로 원래 참조·사전·Pos·playing을 유지할 수 있다. 따로 Pause했다면 원래 playing만 복구한다.
3. **실패할 수 있는 준비를 마친 뒤** 검증한 native를 Kill(false)한다. 재활용 후 다른 Tween을 가리킬 수 있으므로 죽인 실객체를 사전에 오래 두지 않고 미리 만든 inactive 전용 대체 객체로 교체한다. 재진입/다른 모드의 Kill 패치를 허용하면 이 단계도 실패할 수 있으므로 부분 commit 대상도 미리 만든 CPU 연속 재생 spec으로 넘길 수 있어야 한다.
4. commit 후 예외는 원래 객체의 엄밀한 rollback과 구분하여 GPU→CPU 연속 재생 복원으로 처리한다. **원래 Tween 참조 동일성과 궤적 연속성은 별도 합격 조건**이다. 기존 activationCommitted/RecoverGpuLive 경계에 native 자료를 통합해야 한다(`GpuDecoExport.cs:1537–1589`).

현재 Capture는 중간에 Rec를 detach한다. 같은 위치에 native Kill을 덧붙이는 것만으로는 2번을 보장하지 못한다. 별도 undo 목록과 commit 절차가 필요하다. 알 수 없는 DOTween 패치 환경은 우선 CPU에 남긴다. 위 절차는 설계이며 실패 주입 검증 결과가 아니다.

### F. GPU→CPU 복원과 곡 종료

BuildGpuResumePlan은 source/channel에서 키별 GpuResumeSpec을 만들며 Color 4성분·Parallax 2성분의 source 일치를 검사한다. ResumeGpuRecord는 Pos/Duration/Over/Period/Start/Target/**Change**를 가진 DecoAnim.Rec를 만든다(`GpuDecoResume.cs:10–62`, `DecoAnim.GpuResume.cs:17–89`).

알려진 콜백 의미를 DecoAnim으로 재현할 수 있는 native라면 **원래 DOTween을 다시 시작하지 않고 Rec로 이어갈 수 있다.** 원래 start/change와 그 시점의 Pos를 유지한다. 남은 duration으로 Ease를 재시작하지 않는다. 알 수 없는 콜백을 가진 native는 이 경로에 넣지 않는다.

현재 ResumeSpec에는 native 출신 표시와 임의 콜백 보관이 없다. 출처/키/추출 schema/완료 방식/순서 관측 여부를 보존해 복원 전에 허용한 의미인지 재검사해야 한다.

native에는 Rec의 GpuOrder/EngineOrder가 없다. 기존 ResumePlan은 미관측 순서를 표시하고 대체 순서를 만들지만 **원래 갱신 순서의 증거가 아니다**(`GpuDecoResume.cs:34–43`). 사전 순서·장식 index를 생성 순서로 쓰지 않는다. 향후 manager active 목록의 실제 순서를 읽거나 생성 시 추적이 필요할 수 있다. activeId만으로 다른 Rec와의 전체 순서는 알 수 없다. 첫 실험은 독립 PositionX로 좁히고 Pivot/Color/Opacity 등 순서 의존성이 있는 혼합 장식을 제외할 수 있다. 이후 실제 복귀·화면 비교가 필요하다.

| 경계 | 처리 요구 | 현재 근거 |
|---|---|---|
| 진행 중 live off / GPU 오류 | 현재값을 CPU에 쓰고 미완료 native 출신도 Rec로 계속 진행. 다음 프레임 이중 갱신 방지 | GpuDecoExport.cs:1620–1729, GpuDecoRecovery.cs:92–121 |
| 포털·곡 끝 | gpuLivePortalObserved에 따라 계속 진행 조건이 갈림. 모든 경계에서 ResumePlan이 생긴다고 가정하지 않음 | GpuDecoExport.cs:385–390,1630–1643 |
| 재시도·편집 정지·scene reset | CPU 복귀와 게임 Kill/reset 순서를 유지. 끝난 효과를 나중에 Rec로 부활시키지 않음 | GpuDecoPlayer.cs:52–64,155–169,380–395, GpuDecoExport.cs:1630–1636 |
| unload·앱 종료·syncState=false | 계속 진행하지 않는 경로의 native/대체 entry/저장 참조와 GPU 자원 해제 | GpuDecoExport.cs:1734–1738,1759–1817 |

## 6. 비트 동일성 및 위험 판정표

### 이징 일치의 필요조건

ZeroTween.Eval은 설치 Core의 `EaseManager.Evaluate(Ease,EaseFunction,float,float,float,float)`에 delegate를 연결하고 custom=null로 호출한다(`ZeroTween.cs:45–49,102–112,137–146`). **Ease/Pos/Dur/Over/Period가 같으면 같은 Mono 프로세스에서 같은 이징 함수를 쓸 수 있다.** customEase가 있으면 현재 Eval로 보존할 수 없다. Flash의 overshoot는 SetEase에서 정수화하므로 전역 기본값으로 EaseParams를 다시 구하지 않는다(`D/DG.Tweening.TweenSettingsExtensions.decompiled.cs:156–168`).

다음 조건은 별도로 필요하다.

- FloatPlugin은 저장된 changeValue×Ease를 start에 더한다. Color는 성분별로 더하고 축 Vector2는 getter에서 현재 반대 축을 읽어 유지한다(`D/DG.Tweening.Plugins.FloatPlugin.decompiled.cs:45–75`, `D/DG.Tweening.Plugins.ColorPlugin.decompiled.cs:63–99`, `D/DG.Tweening.Plugins.Vector2Plugin.decompiled.cs:118–153`).
- GPU live는 CPU에서 계산한 Ease와 `(float)((double)start+(double)change*(double)e)`를 쓴다(`tools/gpudeco-lab/Assets/Shaders/GpuDeco.compute:228–255`, `GpuDecoExport.cs:615–652`). Mono 중간 정밀도·cast 경계와의 일치는 실기 비교가 필요하다. 이번 .NET 8 검사는 Mono 동일성 증거가 아니다.
- 완료 시 F/Color의 목표 강제와 V의 eased endpoint를 구분한다. 다음 이벤트가 옛 트윈을 Kill(true)할 때도 같은 end를 사용해야 한다. 현재 GPU 교체 코드도 end를 읽는다(`GpuDecoExport.cs:684–707`).
- 갱신 순서·dt의 float 누적·color alpha×opacity·lazy flush·Transform·셰이더·그리기 순서도 같아야 한다. 논리값 동일과 화면 동일은 별도 검증이다.

오프라인 반례: InQuad, 원래 duration=2, 캡처 Pos=0.75, 이후 0.5초. 0→1의 원래 곡선은 **0.390625**, 현재값부터 남은 1.25초로 다시 InQuad를 돌리면 **0.278125**다. 원래 start/change와 진행 1.25/2를 유지해야 한다(`probe-result.json:306–309`).

### 첫 구현 대상 판정

‘후보’는 향후 구현 조건이며 이번에 구현·검증했다는 뜻이 아니다.

| 상태 | 첫 처리 | 이유·확장 조건 |
|---|---|---|
| 검증한 PositionX, active/초기화 완료, 0<=Pos<Dur | 후보 | 형식·모든 delegate·Target 참조·현재값·완료값 대조 |
| LastPosition | 후보 | 이미 절대화된 start/end/change 사용. 상대 목표를 다시 계산하지 않음 |
| DOTween.isRelative=true | CPU 유지 | 원래 StartEffect에는 SetRelative 없음. 초기화 후 절대값과 완료 콜백 일치를 입증한 뒤 확장 |
| delay!=0 / 지연 미완료 / 초기화 전 | CPU 유지 | GPU source에 delay 상태가 없고 시작값 확정 시점이 다름. 완료된 delay도 첫 단계에서는 제외 |
| loops!=1 / completedLoops!=0 / Yoyo·Incremental / backwards·inverted | CPU 유지 | 현재 source는 단일 전진 곡선 |
| Normal 외 / independent / 개별 timeScale!=1 / 개별 Pause | CPU 유지 | 현재 공통 clock과 다름 |
| 알려진 단일 OnUpdate/OnComplete | 의미를 추출할 수 있을 때 후보 | 콜백 유무만으로 제외하면 PositionX도 가져오지 못함 |
| onKill/onPlay/onPause/onStart 등 / 알 수 없는·추가 delegate | CPU 유지 | 콜백 삭제는 동작 삭제. Kill(false)도 onKill 호출 가능 |
| customEase / 지원 외 ease | CPU 유지 | Supported는 0–31 또는36. Flash=32–35, INTERNAL_Custom=37 제외. Unset=0도 첫 후보에서 제외 가능 |
| from/blendable/speedBased/sequence/special startup/link | CPU 유지 | 원래 단순 To와 계약이 다름. link 존재를 검사할 수 없으면 미확인으로 제외 |
| ScaleX/Y | 다음 단계 | Vector2 형식, 정확한 axisConstraint, snapping=false. 사용하지 않는 축의 NaN 처리 주의 |
| Color | 다음 단계 | alphaOnly=false, 네 성분 한 clock, 알려진 콜백, Opacity와의 순서 확인 |
| Parallax/Pivot/Offset/Rotation/Opacity | 다음 단계 | 값 추출 외에 setter 부작용과 다른 채널의 순서 검증 필요 |
| 사용 성분 NaN/Infinity / change·종점 불일치 / 사전 변경 | CPU 유지 | 추정이나 임의 보정으로 통과시키지 않음 |
| 같은 장식에 하나라도 안전하지 않은 진행 entry 존재 | 장식 전체 CPU 유지 | 소유권은 장식 단위. native가 오래된 CPU Transform에 쓰는 혼합 방지 |

이징 번호 근거: `GpuDecoGpu.cs:25`, `D/DG.Tweening.Ease.decompiled.cs:3–43`. Scale의 쓰지 않는 축이 NaN이라고 전체 Vector2를 무조건 거절하지 않는다. 사용하는 성분과 Options를 검증하고 반대 축 유지 방식을 확인한다.

안전하지 않은 대상은 기존 fallback renderer로 CPU에 남겨도 된다. 일부 native가 나중에 끝나더라도 현재 분류는 시작 경계에서 한 번이므로 자동 재승격을 보장하지 않는다.

## 7. 검증 계획·작업량·미확인 사항

### 기존 숫자의 해석

CPU fallback 227–231, PositionX=197, Color=22–27, ScaleX=8, 대형 이미지 52개, 0.64ms, 숨겼을 때 +17–21%, InCatchUp 해제 후 44% 이미지 차이는 **인계 문서:7–8의 기존 보고**다. 이번에 원본 로그나 화면을 다시 얻지 않았다. 재측정 수치, 52개와 키별 중복, 안전 조건을 통과하는 native 수는 **확인 못 함**.

화면을 덮는 이미지를 숨기면 그리기 비용 자체가 줄어든다. GPU 소스로 옮겨도 같은 이미지를 같은 순서로 그리면 texture 읽기·overdraw·blend는 남는다. 직접 줄일 수 있는 것은 native update/CPU setter/Transform 등이다. 그리기 경로 차이의 GPU 이득은 별도 측정해야 한다. **0.64ms 전부나 +17–21%를 이어받기의 예상 성능 이득으로 사용하지 않는다.**

### 가장 작은 다음 실험 — 이번에는 수행하지 않음

1. 안전 조건을 통과하는 PositionX **1개**를 고른다. 같은 장식의 다른 진행 entry도 모두 안전한지 확인한다. 초기화 완료·지연 없음·전진 1회·Normal/scaled/timeScale=1·알려진 delegate로 한정한다.
2. 같은 일시정지 프레임에서 원래 트윈의 형식·start/end/change·Pos/Dur·ease·options·콜백 서명·현재값을 저장하고 GPU 최초값과 비트 비교한다. 그 전에 읽기 전용 전체 native census부터 수행해도 된다.
3. 같은 경계의 ON→OFF, 재개 후 각 프레임, 완료 프레임, 다음 효과의 Kill(true)를 독립 원래 native 대조와 비교한다. 최종 SetPositionX와 renderer 상태까지 확인한다.
4. Rec 탈착 후/캡처 후/첫 dispatch 후/부분 commit 실패를 주입한다. precommit 원래 참조·사전·Pos·playing 유지와 postcommit CPU 연속 재생을 나눠 판정한다. 현재 native identity fixture는 native를 fallback으로 남기는 시험이므로 새 adopt 경로의 증거가 아니다(`GpuDecoActivation.cs:248–273`).
5. 통과 후 PositionX 전체, 이어 Color/ScaleX로 확장한다. InCatchUp 조건은 유지한다.

### 중간 시작 2~3지점과 픽셀·복원 비교

아래는 향후 검증 절차이며 이번 게임 실행을 뜻하지 않는다.

| 시험 | 방법·기록 | 합격 조건 |
|---|---|---|
| 중간 시작 | Arche 254초와 전후 두 지점. 240/270초는 후보이며 실제 진행 native가 없으면 다른 지점 선택. 비공개 맵 사용 금지 | native 전체 entry census, 후보·거절·복합 키·대형 이미지 대응 기록 |
| 같은 일시정지 장면 ON/OFF | 같은 카메라·해상도·시각·scale·이미지로 ON과 `gpudeco live off` 후 비교. 여러 번 왕복하고 별도 원래 CPU 대조 확보 | HUD 차이는 전체 픽셀의 0.7% 이내. HUD 영역을 명시하고 영역 밖 신규 차이는 0 목표. 왕복끼리만 같은 것을 원래 native와 같다고 판정하지 않음 |
| 계속 진행 | pause/resume, 자연 완료, 다음 효과 교체, 서로 다른 dt, CPU 업데이트 전후 기동 | 단계별 이중 step·누락 없음. 값·완료 프레임·복원 후 미래 궤적 일치 |
| 복구·종료 | live off, dispatch 오류, 용량 부족, retry, 편집 정지, 곡 끝, unload | originals_restored, resume_expected/resumed, 미복구 수, native/Rec 잔수를 대응. 객체 동일성과 연속 재생을 분리 |
| 성능 | 픽셀·진행·복구 검증 후 동일 조건 대조/후보 교차 실행 | CPU fallback 장식 수, native/CPU Rec 수, setter/Transform 횟수, 실제 FPS, GPU 시간을 각각 기록. 이미지 숨기기 시험과 구분 |

0.7%는 HUD 차이 상한이며 장식 차이를 0.7%까지 허용한다는 뜻이 아니다. ON에서 틀린 값을 만들고 OFF에도 같은 틀린 값을 복원하면 왕복 비교가 이를 놓치므로 독립 native 대조가 필요하다.

현재 AUTOTEST ‘CPU 처리 이유’는 장식마다 첫 거절 하나만 센다(`GpuDecoActivation.cs:25–59`). 향후 진단은 다음을 분리해야 한다. 이번 소스에는 추가하지 않았다.

- native 총 entry 수, native 보유 장식 수, 키별 전체 entry 수, 장식의 복합 키, 후보/채택/거절 장식 수.
- 미초기화·콜백 미확인·onKill·delay·loops·갱신 방식·ease·용량·현재값 불일치·순서 미관측 등 거절 이유.
- 대형 이미지52개 중 실제 GPU로 옮긴 수. PositionX=197만으로 계산하지 않음.
- precommit 원래 참조 유지, postcommit CPU 연속 재생, 갱신 단계, 미관측 순서, 복원 실패. 곡 시각 추정으로 채우고 inferred_source_clocks=False라고 보고하지 않음.

### 대략적인 작업량

현 구조에 익숙한 개발자 1명 기준의 설계 추정이며 실제 개발 시간 측정이 아니다.

| 범위 | 예상 |
|---|---|
| 전체 native 읽기 전용 census·PositionX 서명 대조 | 0.5~1일 |
| PositionX 추출·commit/undo·Rec 연속 재생·실패 주입 | 2~4일 |
| 2~3지점 화면·복원·업데이트 단계 검증, Color/Scale 확장 | 추가 2~4일 |
| 전체 채널 및 일반 loop/delay/customEase 지원 | 범위 밖. 필요성 측정 후 별도 설계 |

구현 후보는 **초기화된 원래 PositionX를 InCatchUp 이후 경계에서 원래 시계 그대로 가져오는 범위**로 좁힐 수 있다. 공개 API나 필드 접근의 부재는 장애가 아니다. 남은 판단 근거는 실제 안전 후보 수, 실행 delegate의 출처, 기동 프레임의 갱신 단계, native/Rec 복원 순서, 실패 후 연속 재생, 원래 화면과의 픽셀 비교, FPS/GPU 시간이다. 현재 조사만으로 채택이나 성능 개선을 확정할 수 없다.
