# 진행 중인 네이티브 트윈을 켜는 순간 GPU 소스로 이어받기 — 타당성 조사 (Codex 명세)

작업 폴더 `E:\StutterFixWork\wt-adopt` (브랜치 `claude/native-adopt-research`, 기준 5e8ea58). **기존 소스 수정·게임 실행·커밋 금지.** 새 문서 `docs/research/native-tween-adopt.md` 와 필요하면 `tools/adopt-lab/` 만 만든다. 한국어, 결론 먼저(5줄).

## 문제
얼불춤(Unity 6000.3 Mono) 모드 StutterFix 의 "GPU 장식"은 장식 2.8만 개의 이동을 컴퓨트 셰이더가 계산한다. 곡 **중간부터 시작**(에디터에서 타일을 골라 재생)하면, 게임이 시작 지점까지의 효과를 시작 직후 60프레임(`EffectBudget.InCatchUp`) 동안 몰아서 적용한다. 이때 `FastMove.CanAnim`(FastMove.cs:631)이 `InCatchUp` 이면 길이 있는 효과를 모드 애니메이터(`DecoAnim`)가 맡지 못하게 막아서(`return false`), 게임 **원래 코드(`ffxMoveDecorationsPlus.StartEffect`)가 장식에 네이티브 DOTween 트윈을 만든다.**
켜는 순간(`DecoBake.StartPlayer` → `StartGpuLive` → `ClassifyGpuActivationOwnership`, GpuDecoActivation.cs:20) 그 트윈이 아직 진행 중인 장식은 "CPU 처리(fallback)"로 남아 GPU가 못 가져간다. 실측(Arche 곡 254초 시작): CPU 처리 227~231개, 전부 "네이티브 트윈 진행 중"(키: PositionX 197, Color 22~27, ScaleX 8). 그중 큰 이미지 52개(19200x10800 등 화면 전체 배경)가 GPU를 0.64ms 쓰고 이것을 숨기면 무거운 구간이 +17~21%다.
시도해서 안 된 것: `CanAnim` 의 InCatchUp 조건을 풀기 → GPU 장식이 아예 안 켜지고 화면이 44% 달라짐. 따라잡기 중 직접 처리가 막혀 있던 이유가 실제로 있다.

## 해 줄 일 (근거·줄 번호·재현 명령 필수, 모르면 "확인 못 함")
1. **원래 코드가 만드는 트윈의 정체**: 게임 DLL(`D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\A Dance of Fire and Ice_Data\Managed\Assembly-CSharp.dll`, `DOTween*.dll`)을 `tools/ilscan`(CLAUDE.md 참고) 또는 ilspy MCP 로 읽어 `ffxMoveDecorationsPlus.StartEffect`, `scrDecoration` 의 `eventTweens`(키 `TweenType`), 트윈을 만드는 메서드(`DOTween.To`/`DOTween.To(getter,setter,end,dur)`, `SetEase`, `SetDelay`, `SetRelative`, `OnComplete` 등)를 정리. 트윈 종류별(PositionX/Y, Color, ScaleX/Y, Rotation, Opacity, ParallaxOffset…)로 어떤 값에 무엇을 쓰는지.
2. **런타임에 읽을 수 있는 상태**: DOTween `Tween` 객체에서 시작값/끝값/변화값(`TweenerCore<float,float,FloatOptions>` 의 `startValue`/`endValue`/`changeValue`), `Elapsed()`, `Duration()`, `easeType`/`customEase`/overshoot/period, `delay`/`elapsedDelay`, `isRelative`, loops, `isBackwards`, `onUpdate`/`onComplete` 콜백 유무를 **리플렉션/공개 API 로 정확히** 읽는 방법. 이 게임이 쓰는 DOTween 버전(Pro/Free)과 필드 접근자.
3. **따라잡기 의미론**: 게임이 시작 지점까지의 효과를 어떻게 적용하는지(`scrVfxPlus`/`scrController` 의 재생 시작 경로). 과거 이벤트의 트윈은 경과 시간만큼 앞서 만들어지는지(Goto/Complete/남은 시간 조정), 아니면 처음부터 시작하는지. 위의 "InCatchUp 풀기" 시험이 화면 44% 달라진 이유 가설.
4. **이어받기 설계**: 켜는 순간(`ClassifyGpuActivationOwnership` 를 `CaptureInitialGpuLiveState`, GpuDecoExport.cs 1405~ 근처, 이미 진행 중인 `DecoAnim.Rec` 를 `GpuLiveSource` 로 바꾸는 코드와 비교) 네이티브 트윈을 GPU 소스로 바꾸는 방법: TweenType→채널(16채널) 매핑, 소스의 `Pos/Dur/Ease/Over/Period`, 채널 `start/change/end`, 원래 트윈을 **콜백 없이** 죽이는 법(`Kill(false)`), 활성화 실패 시 되돌리기(`RollbackGpuActivation`, GpuDecoActivation.cs ~), 곡 끝/정지 때 GPU→CPU 복원(`BuildGpuResumePlan`)이 네이티브 트윈 출신 소스를 어떻게 다시 CPU 쪽으로 돌려줄지. 비트 동일성(DOTween 이징과 `ZeroTween.Eval`)이 유지되는 조건.
5. **위험·검증**: 이어받기 대상이 안전하지 않은 경우(상대 이동, 지연, 반복, 콜백 있음, 이징 미지원)를 가르는 기준표. 어떤 경우는 계속 CPU 처리로 남겨도 되는지. 검증 방법: 같은 일시정지 장면에서 켠 채/`gpudeco live off` 한 채 픽셀 비교(HUD 0.7% 이내), CPU 처리 장식 수, 곡 중간 시작 2~3지점.
6. **결론**: 구현 가능한가, 위험, 대략의 작업량, 먼저 할 가장 작은 실험(예: PositionX 만 이어받기).

## 규칙
- 비공개 "헤르츠" 맵은 열지 말 것.
- 새 파일 이외 수정 금지. 진단 로그 필드는 `GpuDecoActivation.cs:59` 의 "CPU 처리 이유" 줄(AUTOTEST) 참고.
