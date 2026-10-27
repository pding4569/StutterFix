# 빛난 타일의 박자 알림(OnBeat) 줄이기 — 화면이 똑같음을 보장하는 건너뛰기 (Codex 구현 지시서)

작업 폴더 E:\StutterFixWork\wt-beat (브랜치 claude/beat-diag, 진단 스위치 `beatskip` 가 이미 있음). 게임 실행·DLL 설치·커밋·푸시 금지(Claude 가 게임으로 확인). 한국어로 보고.

## 측정 근거 (게임에서 확인)
고정 BPM 64,000 시험 맵(9만 타일, BPM 변경 이벤트 없음), 자동 플레이, 5초 구간 6개:
- 모드 기본값(`BeatFix` 켬): 곡 4초 400 FPS → 곡 30초 약 300 FPS (계속 하락). 표본 프로파일 포함 비율 `BeatFix.PropPrefix` 5.8% → 29.8%, `BeatFix.FastFloors` 5.2% → 27.6%, `scrFloor.OnBeat` 1.6% → 9.2%.
- `beatfix off`(게임 원래 방식): 약 230 FPS 로 일정(원래 방식은 모든 타일을 부르므로 더 느림).
- `beatskip on`(진단: 빛난 타일 OnBeat 를 아예 안 부름): 460~507 FPS 로 일정.
원인: `BeatFix.FastFloors`(BeatFix.cs)가 "빛난 타일(hasLit) 목록 전체"에 박자마다 `OnBeat()` 를 부른다. 고 BPM 에서는 매 프레임 불리고(게임은 프레임당 한 박자씩 따라감), 빛난 타일은 곡이 진행될수록 쌓여서 비용이 타일 수에 비례해 늘어난다.

## 게임 코드 (디컴파일)
```csharp
// scrFloor.OnBeat
if (!dontChangeMySprite) {
    if (vfx.overrideTileSprites && !hasLit) floorRenderer.sprite = vfx.unlitTile;
    if (hasLit) SetToRandomColor();
}
// scrFloor.SetToRandomColor (빛난 타일에서 박자마다)
if (dontChangeMySprite) return;
switch tileFlashStyle:
  Rando:     floorRenderer.color = arrTileFlashColours[Random...].WithAlpha(floorRenderer.color.a); topGlow.color = Color.clear; bottomGlow?.color = topGlow.color;
  Christmas: topGlow.color = arrColorsBottomGlow[(conductor.beatNumber + seqID) % 2].WithAlpha(0.6f*glowMultiplier); bottomGlow?...
  PureWhite: topGlow.color = white; topGlow.color = topGlow.color.WithAlpha(glowMultiplier); bottomGlow?...
  OnlyTopWhite && controller.gameworld: topGlow.color = white.WithAlpha(0.8f*glowMultiplier);
  (그 밖: 색을 안 바꿈)
num/num2 = (customSpriteRange ? min/maxLitSprite : 0 / (overrideStyle==Checkerboard ? 1 : arrLitTiles.Length-1))
if overrideStyle == RandomChoice  : randomNumberForLitAndGlow = Floor(Random.Range(num,num2))
elif overrideStyle == Checkerboard: randomNumberForLitAndGlow = (Abs(Round(x)+Round(y)+conductor.beatNumber) % 2 == 1) ? num : num2
else                              : randomNumberForLitAndGlow = 0
if vfx.overrideTileSprites : floorRenderer.sprite = vfx.arrLitTiles[randomNumberForLitAndGlow]
if vfx.overrideGlowSprites : topGlow.sprite = ...; bottomGlow.sprite = topGlow.sprite
if (bottomGlow) bottomGlow.color = bottomGlow.color.WithAlpha(Min(bottomGlow.color.a, floorRenderer.color.a));
topGlow.color = topGlow.color.WithAlpha(Min(topGlow.color.a, floorRenderer.color.a));
```
핵심: **tileFlashStyle 가 색을 안 바꾸는 값이고, overrideStyle 이 None 이고(RandomChoice·Checkerboard 아님), overrideTileSprites·overrideGlowSprites 가 꺼져 있으면**, 호출이 하는 일은 마지막 두 줄(글로우 투명도를 타일 투명도 이하로 맞추기)과 `randomNumberForLitAndGlow = 0` 뿐이다. 글로우 투명도가 이미 0(또는 타일 투명도 이하)이고 그 값을 올리는 사건이 없으면 호출은 **아무것도 안 바꾼다**.

## 할 일
`BeatFix.cs` 의 FastFloors(빛난 타일 목록 순회)를, **호출해도 결과가 완전히 같은 타일은 안 부르게** 줄인다. 순서·난수 소비도 같아야 한다(난수를 쓰는 스타일/경로는 원래대로 전부 부른다).
1. "정착(settled)" 상태 정의: 아래가 모두 참인 빛난 타일은 OnBeat 가 아무것도 안 하므로 목록에서 뺀다.
   - 타일 종류 scrFloor 그 자체, `dontChangeMySprite` 가 거짓이면서 vfx 의 tileFlashStyle 이 색을 안 바꾸는 값(위 목록에 없는 값), overrideStyle 이 RandomChoice/Checkerboard 가 아님, overrideTileSprites·overrideGlowSprites 거짓.
   - topGlow(그리고 있으면 bottomGlow) 알파가 타일(floorRenderer) 알파 이하(가장 흔한 경우 0). `randomNumberForLitAndGlow` 가 이미 0.
   조건을 읽는 비용이 호출보다 싸야 한다(유니티 네이티브 호출을 타일마다 박자마다 부르면 소용없다). 정착 판단은 **빛남이 켜진 뒤 한 번 호출해 본 결과**(호출 전후 상태 비교)로 하고, 그 뒤에는 낮은 빈도로만 다시 본다.
2. 정착 타일을 다시 목록에 넣는 사건을 모두 잡는다: `LightUp`·`Reset`(이미 LitChanged 훅), 글로우/타일 알파를 올리거나 바꾸는 게임 코드(IL 스캔으로 `topGlow`·`bottomGlow` 의 color 를 쓰는 모든 곳을 찾아 표로 낼 것, `tools/ilscan` 사용법은 CLAUDE.md), vfx 설정 변경(tileFlashStyle·overrideStyle 이 바뀌는 곳), 다른 모드가 글로우를 건드릴 가능성(Foreign() 검사 확장 가능하면).
3. 안전장치: 의심스러우면 원래 길(전부 호출)로. 정책 플래그 `BeatSettledSkip` 기본 **끔**(`Config` + 자동 시험 명령 `beatsettled on|off`). 끈 상태는 지금과 바이트까지 같아야 한다. Main.Unload 에 정적 상태 비우기 포함.
4. 개발자 검증(`beat-verify` 확장): 정착으로 뺀 타일을 가끔(예: 64박자마다 일부) 실제로 OnBeat 호출 전후로 비교해 topGlow·bottomGlow·floorRenderer 의 color/sprite, randomNumberForLitAndGlow 가 안 바뀌었음을 센다. 바뀌면 불일치 카운터 증가 + 그 타일을 목록에 복귀.
5. 헤드리스 검사(`tools/optimization/BeatSettledCheck/`, 기존 `EventAdmissionCheck` 형식): 위 상태기계(정착 판단·복귀 사건)를 가짜 타일 상태로 최소 2,000개 조건에서 "원래 전부 호출한 결과 == 새 방식 결과" 비교(스타일 조합, 글로우 알파 올림, Reset, 투명도 변경, 순서/난수 소비 포함).

## 검증
- 두 Player 빌드(`-p:Edition=Player`, `-p:Edition=Player -p:AutoTestBuild=1`) 오류 0.
- 기존 관련 검사 통과.
## 보고
바꾼 것, 글로우를 쓰는 게임 코드 위치 표, 통과한 검사 수, 게임에서 안 돌려 본 것, 원래와 다를 수 있는 점(있다면 정확히).
