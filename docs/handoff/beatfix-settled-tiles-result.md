# BeatSkipNoChange 1차 구현 결과

2026-10-27, `E:\StutterFixWork\wt-beat`.
원 지시서: [beatfix-settled-tiles.md](beatfix-settled-tiles.md).

## 구현

- `Config.BeatSkipNoChange = false`가 기본값이다. `beatsettled on|off`는 이 설정을
  저장하고 다음 박자부터 목록을 재구축한다. 명령 출력과 `pan` 결과에 적용 여부,
  재검사/목록 불일치/검증 상태 일치/검증 상태 불일치 누계를 표시한다.
- 켰을 때 `lit`은 `hasLit && !dontChangeMySprite`인 일반 타일만 번호순으로 유지한다.
  기존 LightUp/Reset 훅에 더해 TweenOpacity postfix와
  ffxLightBridge.StartEffect postfix를 추가했다. 후자는 실제 `brigeFloors` 전체를 갱신한다.
  첫 번째 onBeats 반복에 중복 등록된 같은 타일도 함께 갱신한다.
- 훅/대상 필드가 확인되지 않으면 새 정책을 허용하지 않는다. 기존 외부 OnBeat 패치,
  PropagateOnBeat 패치, 파생 타일, vfx 부재/불일치, overrideTileSprites 폴백을 유지한다.
- 매 256번째 게임 내 타일 박자에 **제외된 타일까지 전체**의 관리 필드를 재검사한다.
  첫 onBeats 반복 전에 복구하므로 그 박자의 중복 listener도 함께 복구된다.
  정상 재검사에는 Unity 네이티브 속성 읽기가 없다. 누락 발견 시 누계와 로그를 남긴다.
- `beat-verify` + 개발자/PlayerAuto 빌드에서는 64박자마다 제외된 타일 한 장에 OnBeat를
  실제 호출하고 topGlow/bottomGlow/floorRenderer의 색·스프라이트와
  randomNumberForLitAndGlow를 정확 비교한다. 불일치/예외이면 누계를 올리고,
  새 정책을 unload까지 중단하고 기존 lit 목록으로 복귀한다.
  일반 Player에는 이 스냅샷 코드가 포함되지 않는다.
- `Main.UninstallAll`에서 `BeatFix.Shutdown`을 호출한다. 새 정책/검증 플래그,
  맵 참조/번호표/버퍼, 새 누계를 해제·초기화하며 Harmony 훅은 기존 ID 해제 경로를 따른다.

## 게임 IL 조사

대상: 설치된 `Managed/Assembly-CSharp.dll`을 **읽기만** 했다.
SHA256: `F692210F9A342F7EED4BE40AAD463286AB40BF046599959823942EA02F4CA3FF`.
`tools/ilscan`을 빌드하고 `METHOD='*' FIELDS=1`로 게임 전체를 스캔했다.
원문은 `out/beatsettled/game-fields.txt`, 글로우 관련 후보 블록은
`out/beatsettled/glow-writers.txt`에 보존했다.

`dontChangeMySprite` 쓰기는 다음 6곳이다.

| 위치 | 처리 |
|---|---|
| scrFloor.Reset | 기존 LitChanged postfix |
| scrFloor.TweenOpacity | 새 OpacityChanged postfix |
| ffxLightBridge.StartEffect | 새 BridgeChanged postfix, brigeFloors 전체 |
| LevelSelectIsland.Awake | 목록 초기 작성 시 현재 값 반영 |
| scrColorPlanet.Awake | 목록 초기 작성 시 현재 값 반영 |
| scrSetEmojiMode.Awake | 목록 초기 작성 시 현재 값 반영 |

직접 글로우 필드 접근과 color setter/tween 호출을 함께 가진 게임 메서드는 아래와 같다.
ILScan은 메서드의 호출/필드 이름을 중복 제거해 보여 주므로, 같은 메서드에 등장했다는
사실만으로 다른 렌더러의 color 쓰기를 글로우 쓰기로 단정하지 않았다.

| 게임 코드 위치 | IL에서 확인한 글로우 관련 동작 | 2차 설계에서의 의미 |
|---|---|---|
| LevelSelectIsland.Awake | topGlow/bottomGlow와 Color.clear, SpriteRenderer.set_color | 초기 상태 반영 |
| scrFloor.LateUpdate | topGlow, opacity/glowMultiplier 변경, SpriteRenderer.set_color | 매 프레임 알파 재상승 가능 경로 |
| scrFloor.SetToRandomColor | topGlow/bottomGlow 색·스프라이트, WithAlpha/Min, 난수번호 | OnBeat의 실제 변경 경로 |
| scrObjectDecoration.SetFloorGlowColor | topGlow 색, opacity, WithAlpha, floorGlowColor 기록 | 장식에 의한 글로우 재설정 |
| ffxLightBridge.StartEffect | topGlow 색 읽기와 DOColor, dontChangeMySprite 쓰기 | tween 진행 중의 글로우 변경 |
| CoopPainter.OnHit | topGlow에 Color.clear, SpriteRenderer.set_color | 타격 이벤트에 의한 변경 |
| scrPlanet.SwitchChosen (동시 등장 후보) | topGlow/bottomGlow 접근, SetActive 및 legacyFloorSpriteRenderer의 DOColor | 글로우 color 직접 쓰기로 확정하지 않음 |

2차는 구현하지 않았다. 위 경로와 tween/외부 모드 쓰기를 모두 무효화 사건으로 잡고,
색을 바꾸지 않는 스타일·비난수 override·sprite override 꺼짐에서 첫 OnBeat 결과가
안정된 타일만 정착 대상으로 삼는 설계다. 특히 LateUpdate의 알파 갱신과 장식의
SetFloorGlowColor를 재현하는 검증 맵이 필요하다. 이번 작업에는 그 맵이 없으므로
`BeatSettledSkip`이나 추가 정착 판정을 넣지 않았다.

## 검증 결과

| 검사 | 결과 |
|---|---|
| BeatSettledCheck | 4,096조건, 131,099 assertions 통과 |
| 정상 훅 이벤트 1,024박자 | 재검사 4회, 목록 불일치 0 |
| 미통지 플래그 변경 주입 | 256번째 박자에 불일치 1건 수리, 중복 listener/타일 호출 복귀 |
| 제외 타일만 있는 정상/재검사 박자 | 렌더러 속성 읽기 0 |
| 상태 변화 주입 | 검증 불일치 1건 검출, 새 정책 중단 |
| Player 빌드 | 오류 0, 경고 46 |
| Player + AutoTestBuild=1 빌드 | 오류 0, 경고 47 |
| git diff --check | 통과 (csproj의 LF→CRLF 안내만 있음) |

실행 명령과 결과 로그는 `tools/optimization/BeatSettledCheck/README.md`,
`out/beatsettled/{headless,build-player,build-player-auto}.log`를 참조한다.

추가로 기존 DormancyCheck를 실행했으나 기존 어댑터가 현재 Dormancy.cs의
GpuPlaybackState/GpuDecoPlayer와 DecoBake.SkipGpuLate/CollapseMarkDirty/
CollapseMarkNew/GpuLateOwnershipEpoch 인터페이스를 제공하지 않아 **컴파일 오류 18개**로
실행되지 않았다. 이 작업에서 Dormancy.cs와 해당 검사 소스는 변경하지 않았으며
`git diff --exit-code -- Dormancy.cs tools/optimization/DormancyCheck`로 확인했다.
이 검사를 통과했다고 계산하지 않았다. 로그: `out/beatsettled/dormancy.log`.
기존 BeatFix 전용 헤드리스 검사는 없어 이번에 프로덕션 소스를 연결한 검사를 추가했다.

## 검증 범위와 남은 게임 확인

- 게임 실행, DLL 설치, 커밋, 푸시는 수행하지 않았다. 실제 픽셀 동일성, FPS,
  곡 전체 동작과 실제 Harmony postfix 연결은 Claude의 게임 검증 대상이다.
- 정책 끔에서는 기존 hasLit 목록과 호출 결과를 유지한다. 헤드리스 끔/켬 모두
  원래 전체 호출 결과와 상태·유효 호출 순서·난수 상태가 일치했다.
  이는 바이너리 파일 자체가 변경 전과 같다는 뜻은 아니다.
- 현재 확인한 게임 쓰기 경로는 즉시 반영한다. 미래 게임/다른 모드가 훅 밖에서
  플래그를 직접 바꾸면 재검사 전 최대 255박자 동안 목록이 낡을 수 있다.
  특히 true→false 누락은 그 사이 실제 OnBeat를 놓칠 수 있다. 기본 꺼짐을 유지하고,
  이런 호환성까지 무조건 동일하다고 주장하지 않는다.
- 검증 모드는 의도적으로 추가 OnBeat를 호출한다. 예상과 달리 그 호출이 상태나
  난수를 바꾸면 감지 후 중단하지만, 이미 발생한 검증 호출의 부작용을 되감지는 않는다.
- 일반 overrideTileSprites 경로는 기존 폴백을 유지하므로 이번 절감 대상에서 제외된다.

게임 확인 시 `beatskip off`, `beatfix on`, `beatsettled on`으로 새 정책을 사용한다.
`beatsettled on`을 다시 보내면 누계를 출력한다. `beatsettled off`로 기존 방식에 복귀한다.
상태 비교는 PlayerAuto 빌드와 기존 `beat-verify` 파일을 사용한다.
