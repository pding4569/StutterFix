# 판정 오차 막대 눈금을 트윈 없이 직접 돌리기 (Codex 구현 지시서)

작업 폴더 E:\StutterFixWork\wt-meter (브랜치 claude/hitmeter-direct). 게임 실행·DLL 설치·커밋·푸시는 하지 않는다(Claude 가 게임으로 확인한다).
한국어로 보고한다. 기존 코드 스타일(주석 밀도·이름)을 맞춘다.

## 왜
표본 프로파일(9만 타일 합성 맵, 자동 플레이, 메인 스레드): 저 BPM(곡 60초)에서 `scrHitErrorMeter.AddHit` 0.9% → 고 BPM(곡 160초) 9.9%.
스택 포함 비율: `DOTween.Kill`→`TweenManager.FilteredOperation` 6.8%, 문자열 비교(`string.Equals`/`SequenceEqual`) 약 3%, 새 트윈 만들기(`DOTween.To`/`AddActiveTween`) 약 2.4%, `ReorganizeActiveTweens` 2.3%.
타격마다 문자열 id 로 활성 트윈 전체를 훑는 Kill 이 2번 + 트윈 3개를 만들기 때문이다. 초당 타격이 많을수록(고 BPM) 프레임이 깎이는 원인의 큰 부분.

## 원래 게임 코드 (Assembly-CSharp, 디컴파일)
```csharp
// scrHitErrorMeter.AddHit(float angleDiff, HitMargin hitMargin, float marginScale = 1f, scrPlanet planet = null, scrFloor hitFloor = null)
... (각도 계산, averageAngle = Lerp(averageAngle, angleDiff, sensitivity)) ...
if (meterShape == ErrorMeterShape.Curved)
    handImage.rectTransform.DORotateQuaternion(Quaternion.Euler(0f, 0f, averageAngle), 0.25f).SetEase(Ease.OutCubic).SetId(TWEEN_ID);
else if (meterShape == ErrorMeterShape.Straight)
    handImage.rectTransform.DOAnchorPos(new Vector2((0f - averageAngle) * 2.5f, -62f), 0.25f).SetEase(Ease.OutCubic).SetId(TWEEN_ID);
... DrawCurvedTick / DrawStraightTick -> ErrorMeterTick.Show(hitMargin, overrideColor, tweenId, tweenDuration)

// ADOFAI.ErrorMeterTick.Show(HitMargin hitMargin, Color? overrideColor, string tweenId, float tweenDuration)
string text = tweenId + "o";
DOTween.Kill(tweenId);
DOTween.Kill(text);
tick.color = overrideColor ?? GetFallbackColor();
tickOutline.color = (flag ? Color.white : WhiteClear);
tick.DOColor(tick.color.WithAlpha(0f), tweenDuration).SetEase(Ease.InQuad).SetId(tweenId).OnKill(() => tick.color = Color.clear);
if (flag) tickOutline.DOColor(WhiteClear, tweenDuration).SetEase(Ease.InQuad).SetId(text).OnKill(() => tickOutline.color = WhiteClear);
```
`scrHitErrorMeter.Reset` 도 `DOTween.Kill` 을 부른다(tweenId 들). 이 Reset 에서 눈금이 지워지는 동작(OnKill 이 색을 지움)을 그대로 유지해야 한다.
이미 `HitMeterFade.cs` 가 `Show` 의 DOColor 를 가벼운 `Fade`(CanvasRenderer 투명도만)로 바꿔 놓았다. 그 위에 얹는다.

## 할 일
`HitMeterFade.cs` 와 같은 방식(Harmony 트랜스파일러로 호출 하나씩 바꾸기, 다른 모드의 AddHit/Show 패치와 충돌하지 않게 전체 prefix 대체는 금지)으로:
1. **`Show` 의 `DOTween.Kill(string)` 2개** — 문자열 id 로 활성 트윈 전체를 훑지 않게. 이 눈금(Tick)이 지금 걸고 있는 트윈 핸들을 눈금마다 기억해 두었다가 `Kill()` 을 그 핸들에 직접 부른다(진짜 DOTween 객체를 계속 쓰므로 OnKill 동작이 같다). 핸들이 이미 끝났으면(`!IsActive()`) 아무것도 안 한다. 다른 모드가 같은 id 로 만든 트윈은 이 길이 못 죽이므로, 기억한 핸들이 없는 눈금만 원래 `DOTween.Kill(id)` 로 돌아간다.
2. **손(핸들) 트윈** — `DORotateQuaternion`/`DOAnchorPos` + `SetEase(OutCubic)` + `SetId(TWEEN_ID)` 3줄 체인. 손은 하나뿐이고 새 타격이 오면 전에 걸린 트윈은 의미가 없다(마지막이 매 프레임 마지막에 쓰므로). 손마다 마지막 트윈 핸들을 기억해 새로 걸기 전에 `Kill(false)`(콜백 없이)로 끈다. 트윈 시작값 규칙(첫 업데이트에서 현재값을 읽음)이 같게 유지되는지 확인하고, 다르면 시작값을 우리가 직접 읽어 줘야 한다.
3. 위 두 개로 `FilteredOperation`(활성 트윈 전체 훑기)이 AddHit 경로에서 사라졌는지 호출 수를 센다(정적 카운터 `HitMeterFade.KillFast/KillSlow`).

더 줄일 수 있으면(트윈을 아예 만들지 않고 모드가 매 프레임 직접 값을 쓰는 풀) 시도해도 되지만 **동작이 한 비트도 다르면 안 된다**. 우선 1·2 로 끝내고, 3 의 결과를 보고한다.

## 정책
`DrawCollapsePolicy.cs`(또는 기존 HitMeterFade.Enabled 방식)에 `HitMeterDirectKill` 플래그, 기본 **끔**. 게임 안에서 `set` 명령으로 켜고 끌 수 있게(기존 자동 시험 패턴 따라). 끈 상태는 지금과 완전히 같아야 한다. Main.Unload 에 되돌리는 코드(패치 제거, 정적 상태 비우기).

## 검증 (코드로)
- 헤드리스 검사 `tools/optimization/HitMeterDirectCheck/` (기존 `EventAdmissionCheck` 형식 참고): DOTween.dll 을 참조해 가짜 눈금/손 객체로, 원래 코드 경로와 새 경로가 같은 입력 순서(타격 시각·간격·id 재사용·Reset 중간 끼움·같은 id 를 다른 모드가 만든 경우)에서 프레임마다 같은 최종 값(손 위치/회전, 눈금 색·투명도)을 내는지. 최소 2,000개 조건.
- Player 빌드 두 종류(`-p:Edition=Player`, `-p:Edition=Player -p:AutoTestBuild=1`) 오류 0.
- 기존 검사 중 관련된 것(HitMeterFade 관련) 다시 통과.

## 보고에 꼭 넣을 것
무엇을 바꿨나, 어떤 검사가 몇 개 통과했나, **게임에서 안 돌려 본 것**(실제 FPS·화면은 Claude 가 잰다), 원래 코드와 다를 수 있는 점(있다면 정확히).
