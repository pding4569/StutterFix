# T3b 결과 — 장부 제외 명시, 쓰기 사이트 진단, 오프라인 검증

2026-10-20. 작업 폴더 `E:\StutterFixWork\wt-tilerange`. 게임 실행·설치·커밋은 하지 않았다.

사이트 진단과 오프라인 검증을 구현했다. **198만 호출의 원인 제거와 평균 0.15ms/프레임 달성은 아직 확인하지 못했다.** 조사한 현재 소스의 `Rewrite`는 이미 `opacity/startPos/startRot/tweenRot`만 추적했다. 지목된 장부 필드 4개는 변경 전부터 이 목록 밖이었다. 따라서 이번 명시적 제외가 기존 dirty 호출을 줄였다고 주장할 수 없다. 게임에서 상위 사이트를 확인해야 다음 원인 판단이 가능하다.

## 구현

- `FloorRangeWriters.Rewrite`가 만드는 각 field-write/field-address/transform-setter 호출에 고정 정수 슬롯을 넣었다. 메서드 형식·서명, **원본 IL 오프셋**, 종류와 필드/접근자 이름을 저장한다.
- 카운터는 기본 꺼짐. `tilerange sites on`은 사이트 카운터만 0으로 만들고 켠다. `tilerange sites off`는 계수를 멈추고 기존 수치를 보존한다. `tilerange off`도 계수를 끈다. `tilerange status`는 비영 호출 수 상위 12개를 내림차순으로 출력한다. 동률 순서는 보장하지 않는다. 계수 경로는 배열 슬롯에 대한 조건 검사와 `Interlocked.Increment`이며 문자열 생성·해시 조회·동적 슬롯 탐색은 없다. 정렬/스냅샷 할당은 status 때만 한다.
- 기존 prefix 방식 대상에는 같은 Patch 호출의 진입 계수 transpiler를 함께 지정했다. `prefix-mark`는 메서드 진입 횟수이며 EffectPrefix가 표시하는 현재/인접 타일 수와 같지는 않다. ResetBody에는 reset 진입 슬롯이 있다. 진입 계수는 다른 prefix가 원본 실행을 막으면 증가하지 않을 수 있다.
- `FloorAnim`의 TweenFloor/New/Step/Complete/KillPrefix/CompletePrefix 직접 표시에도 별도 정적 슬롯을 붙였다. 기존 `hook-calls`에는 이 경로도 들어가므로 53개 게임 훅만 계수해서는 원인을 놓칠 수 있다. 기존 실제 표시 조건은 변경하지 않았다.
- 원본 오프셋은 Harmony의 원본 instruction reader로 준비 때 읽고 쓰기 종류/대상별 출현 순서로 대응한다. FloorLateFix가 앞에서 getter 명령 크기를 바꿔도 오프셋이 밀리지 않는다. 원본에 대응하지 않는 새 쓰기가 들어오면 `writer-site-original-mismatch`로 설치를 실패시킨다. 다른 모드가 같은 종류의 쓰기를 제거·재배열하면 대응이 달라질 수 있어 절대적인 소스 매핑 보장은 아니다.
- framework `ILGenerator` 형식을 netstandard에서 직접 참조할 수 없어 Harmony의 기존 `GetOriginalInstructions` 공개 overload를 **준비 때 reflection으로** 호출한다. 새 `Module.ResolveSignature`/`ResolveMethod(MetadataToken)` 호출은 추가하지 않았다. 기존 이름+인자 수+호출 규약 선택, 중복 거부, 실패 시 연구 Harmony 전부 해제를 유지했다. 대상은 기존 53개이며 새 게임 메서드 패치 대상을 추가하지 않았다.

## 장부와 LateUpdate의 IL 근거

전체 `Assembly-CSharp.dll` 메서드의 IL을 오프라인으로 조사했다. [장부 읽기 목록](tilerange-evidence/t3b-bookkeeping-readers.txt)에는 아래 네 읽기만 있다. 다른 메서드에서 해당 필드를 타일 상태로 읽는 경로는 이 어셈블리에서 발견하지 못했다. 다른 모드나 reflection 읽기까지 증명하는 것은 아니다.

| 필드 | LateUpdate 읽기 → 쓰기 | 판단 |
|---|---|---|
| lastPos | IL_0051 → IL_035F | 현재 thisTransform.position과 비교하는 이전 프레임 관측값; 표시 제외 |
| lastScale | IL_0069 → IL_0375 | 현재 floorRenderer.transform.localScale과 비교하는 이전 프레임 관측값; 표시 제외 |
| opacityLastFrame | IL_008D → IL_0381 | opacity와 비교해 isFading 계산; 표시 제외 |
| glowMultiplierLastFrame | IL_00A4 → IL_038D | glowMultiplier와 비교해 isChangingGlowMult 계산; 표시 제외 |

장부 쓰기와 읽기는 원본 IL 그대로 남긴다. `IsBookkeeping`으로 제외를 명시했고 stfld와 ldflda 모두 dirty를 만들지 않는다. Reset 같은 수명 메서드의 진입 표시 자체는 유지한다.

LateUpdate의 유일한 Transform setter는 **IL_034E `set_position`**이다. IL_0246의 `multiplanetLine` 존재 검사와 IL_0331의 `isTweening`, IL_0336 `brfalse.s IL_0353` 안쪽에 있다. 대상은 IL_0339 `multiplanetLine` → IL_033E `get_transform`; 값은 IL_0344 타일 transform → IL_0349 `get_position`이다. 타일 자체를 매 프레임 다시 쓰는 호출이 아니다. 기존 조건 안쪽 wrapper를 그대로 유지하며 새 getter 비교는 추가하지 않았다. wrapper의 기존 Transform 참조 사전 검사는 준비한 실제 타일 Transform일 때만 dirty를 표시한다.

`scrFloor::Update` 본체는 조사한 DLL에 정의돼 있지 않았다. LateUpdate의 `holdLength`(0018), `isTweening`(0086), `isFading`(009D), `isChangingGlowMult`(00B4) 기록도 인증 필드가 아니므로 표시하지 않는다. 다른 효과/특수 연출의 타이머·박자·캐시 필드 역시 현재 scrFloor 인증 필드 추적 대상이 아니었다. 추가로 제외할 **추적 중인 무조건 장부 쓰기**는 발견하지 못했다. 시간으로 회전시키는 Transform 쓰기는 장부로 분류하지 않았다.

아래 53개 표의 각 원본 본체는 [원본 IL](tilerange-evidence/t3b-game-raw-il.txt)에 있고, 후보 목록은 새로 생성한 [TILE_WRITERS 출력](tilerange-evidence/t3b-game-writers.txt)에 있다. 표는 직접 쓰기와 주소 노출을 열거한다. 매 프레임 전체 호출 횟수나 Transform 별칭을 정적으로 증명한 표는 아니다. 예를 들어 PuzzleRoom의 startPos 주소는 바로 Vector3.y를 읽는 경로지만, 종전 보수적 주소 훅을 유지했다.

| 대상 | IL 근거 (원본 오프셋) | 판단 |
|---|---|---|
| `scnGame::ApplyEventsToFloors` | 직접 인증 필드/Transform 쓰기 없음 | 변환할 직접 쓰기 없음; 기존 대상 유지 |
| `scnGame::PrepVfx` | 직접 인증 필드/Transform 쓰기 없음 | 변환할 직접 쓰기 없음; 기존 대상 유지 |
| `scnGame::ResetScene` | IL_0059 callvirt set_localPosition<br>IL_0069 callvirt set_rotation<br>IL_007E callvirt set_position | 맵 인증 폐기 유지 |
| `scrLevelMaker::MakeLevel` | IL_0085 stfld startPos<br>IL_009E stfld startRot<br>IL_00AA stfld tweenRot | 맵 인증 폐기 유지 |
| `scrLevelMaker::ResetFloor` | IL_0013 callvirt SetPositionAndRotation<br>IL_001D callvirt set_localScale | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `scrLevelMaker::MakeFreeroamGrid` | IL_0303 callvirt set_localScale<br>IL_0381 stfld startPos<br>IL_039D stfld startRot<br>IL_03AB stfld tweenRot | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `FreeroamArea::.ctor` | 직접 인증 필드/Transform 쓰기 없음 | freeroam 소유 타일 표시 유지 |
| `scrFloor::Awake` | IL_005D stfld startPos<br>IL_0076 stfld startRot<br>IL_0082 stfld tweenRot | 수명/명시적 상태 변경 진입 표시 유지 |
| `scrFloor::Reset` | IL_0259 stfld opacity<br>IL_0276 stfld startPos<br>IL_0281 stfld startRot<br>IL_028C stfld tweenRot | 수명/명시적 상태 변경 진입 표시 유지 |
| `scrFloor::LateUpdate` | IL_034E callvirt set_position<br>장부 stfld: 035F/0375/0381/038D; 분기 0336 → 0353 | 장부 4개 제외를 명시; isTweening 조건부 선 Transform 쓰기 유지 |
| `scrFloor::TweenRotation` | 직접 인증 필드/Transform 쓰기 없음 | 수명/명시적 상태 변경 진입 표시 유지 |
| `scrFloor::TweenOpacity` | IL_0034 stfld opacity | 수명/명시적 상태 변경 진입 표시 유지 |
| `scrFloor::MoveToBack` | 직접 인증 필드/Transform 쓰기 없음 | 수명/명시적 상태 변경 진입 표시 유지 |
| `scrFloor::MoveBackBy` | 직접 인증 필드/Transform 쓰기 없음 | 수명/명시적 상태 변경 진입 표시 유지 |
| `scrFloor::SetRotation` | IL_001C stfld tweenRot<br>IL_002D callvirt set_eulerAngles | 수명/명시적 상태 변경 진입 표시 유지 |
| `scrFloor::ResetToLevelStart` | IL_000E ldflda startRot<br>IL_002C callvirt set_position<br>IL_0033 callvirt set_eulerAngles<br>IL_003A stfld tweenRot<br>IL_0046 stfld opacity<br>IL_005A callvirt set_localScale | 수명/명시적 상태 변경 진입 표시 유지 |
| `scrFloor::<TweenRotation>b__203_1` | IL_0001 ldflda tweenRot | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `scrFloor::<TweenRotation>b__203_2` | IL_000C callvirt set_eulerAngles | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `scrFloor::<TweenOpacity>b__204_1` | IL_0002 stfld opacity | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `ffxLightBridge::Start` | IL_008F stfld opacity | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `ffxLightBridge::StartEffect` | 직접 인증 필드/Transform 쓰기 없음 | 변환할 직접 쓰기 없음; 기존 대상 유지 |
| `ffxFloorAppearPlus::StartEffect` | IL_0047 callvirt set_position<br>IL_0062 callvirt set_eulerAngles<br>IL_0078 stfld tweenRot | 현재/인접 타일 효과 진입 표시 유지 |
| `ffxFloorAppearPlus::FloorSetup` | IL_00CC callvirt set_eulerAngles<br>IL_00F6 stfld tweenRot<br>IL_014E callvirt set_position<br>IL_0179 callvirt set_eulerAngles<br>IL_019F stfld tweenRot<br>IL_01FA callvirt set_position<br>IL_0226 callvirt set_eulerAngles<br>IL_024D stfld tweenRot<br>IL_0295 callvirt set_position<br>IL_02DF callvirt set_position | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `ffxFloorAppearPlus::<StartEffect>b__7_1` | 직접 인증 필드/Transform 쓰기 없음 | 변환할 직접 쓰기 없음; 기존 대상 유지 |
| `ffxFloorDisappearPlus::StartEffect` | 직접 인증 필드/Transform 쓰기 없음 | 현재/인접 타일 효과 진입 표시 유지 |
| `ffxMoveFloorPlus::StartEffect` | 직접 인증 필드/Transform 쓰기 없음 | 기존 효과 실행 전 인증 폐기 유지 |
| `DivineIntervention::Awake` | IL_01C9 callvirt set_localScale<br>IL_026D callvirt set_localScale<br>IL_02DE callvirt set_localPosition<br>IL_04A7 callvirt set_position<br>IL_0620 callvirt set_localScale | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `DivineIntervention::Update` | IL_02F3 callvirt set_position<br>IL_041D callvirt set_position<br>IL_045A callvirt set_localScale<br>IL_0652 callvirt set_localScale<br>IL_0676 callvirt set_position<br>IL_07C3 callvirt set_localScale<br>IL_07E7 callvirt set_position<br>IL_0A59 callvirt set_localEulerAngles<br>IL_0A96 callvirt set_localEulerAngles<br>IL_0AD3 callvirt set_localEulerAngles<br>IL_0B86 callvirt set_eulerAngles<br>IL_0CD8 callvirt set_eulerAngles<br>IL_0DA2 callvirt set_localPosition<br>IL_0DF9 callvirt set_localEulerAngles<br>IL_0ED4 callvirt set_localPosition<br>IL_0F2B callvirt set_eulerAngles | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `LaserSpawner::Update` | IL_06B6 callvirt set_localScale<br>IL_0737 callvirt set_localScale<br>IL_07B4 callvirt set_localScale<br>IL_084C callvirt set_localPosition | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `Mawaru::SoccerUpdate` | IL_0105 callvirt set_position<br>IL_0136 callvirt set_position | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `Mawaru::HighFiveUpdate` | IL_00F1 callvirt set_position<br>IL_0151 callvirt set_position | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `Mawaru::RandomizeTiles` | IL_0106 callvirt set_position<br>IL_014B callvirt set_position<br>IL_0190 callvirt set_position<br>IL_01C4 callvirt set_position<br>IL_01F8 callvirt set_position<br>IL_02B8 callvirt set_localScale<br>IL_032D callvirt set_localScale<br>IL_037F callvirt set_position<br>IL_03D1 callvirt set_position<br>IL_0423 callvirt set_position | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `Mawaru::DefendUpdate` | IL_003F callvirt set_eulerAngles<br>IL_00A2 callvirt set_position<br>IL_00F8 callvirt set_eulerAngles<br>IL_0131 callvirt set_eulerAngles<br>IL_0184 callvirt set_position<br>IL_0234 callvirt set_position<br>IL_0381 callvirt set_localPosition<br>IL_03F6 callvirt set_localPosition<br>IL_0436 callvirt set_localPosition<br>IL_05D9 callvirt set_eulerAngles<br>IL_069B callvirt set_eulerAngles<br>IL_06FD callvirt set_localPosition<br>IL_093B callvirt set_localScale<br>IL_0B1E callvirt set_localScale<br>IL_0BCC callvirt set_localScale | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `Mawaru::NishiGoatUpdate` | IL_00AD callvirt set_position<br>IL_0161 callvirt set_eulerAngles | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `Mawaru::Defend2Update` | IL_003F callvirt set_eulerAngles<br>IL_00A2 callvirt set_position<br>IL_00F8 callvirt set_eulerAngles<br>IL_0131 callvirt set_eulerAngles<br>IL_0184 callvirt set_position<br>IL_0234 callvirt set_position<br>IL_0379 callvirt set_localPosition<br>IL_03B9 callvirt set_localPosition<br>IL_055C callvirt set_eulerAngles<br>IL_061E callvirt set_eulerAngles<br>IL_0680 callvirt set_localPosition<br>IL_08BE callvirt set_localScale<br>IL_0AA1 callvirt set_localScale<br>IL_0B4F callvirt set_localScale | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `Mawaru::FinaleUpdate` | IL_019C callvirt set_position<br>IL_01D6 callvirt set_localPosition<br>IL_0205 callvirt set_localScale | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `SingSing_Notefield::Update` | IL_02A4 callvirt set_localPosition<br>IL_02CE callvirt set_localScale<br>IL_0323 callvirt set_localScale<br>IL_0348 callvirt set_localPosition<br>IL_03B4 callvirt set_localScale<br>IL_0402 callvirt set_localPosition<br>IL_043B callvirt set_localScale<br>IL_0460 callvirt set_localPosition<br>IL_04D6 callvirt set_localScale<br>IL_052A callvirt set_localPosition<br>IL_0559 callvirt set_localScale<br>IL_057E callvirt set_localPosition | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `TaroBGScript::FloorResetPosition` | IL_000C callvirt set_position<br>IL_001D callvirt set_eulerAngles | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `TaroBGScript::FloorBeatVibe` | IL_0180 callvirt set_position<br>IL_01A6 callvirt set_eulerAngles | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `TaroBGScript::FloorDrunkVibe` | IL_00C4 callvirt set_position | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `TaroBGScript::FloorDrunkVibeXPos` | IL_00AA callvirt set_position | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `ThirdSun::Update` | IL_0043 callvirt set_eulerAngles | 0043은 vortex 시각 연출 회전(001F ldfld vortex); 장부 아님, 타일 참조 검사 유지 |
| `scnMinesweeper::ProcessTile` | IL_011A callvirt set_localScale | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `scnMinesweeper::Fail` | IL_00C5 callvirt set_localScale | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `scnMinesweeper::EndingUpdate` | IL_01CE callvirt set_localScale | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `Cutscene1::Awake` | IL_00DF callvirt set_localScale<br>IL_00F4 callvirt set_localScale<br>IL_0109 callvirt set_localScale | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `Cutscene1::CreateNameEntry` | IL_004F callvirt set_position<br>IL_0085 callvirt set_localScale<br>IL_00CC callvirt set_localPosition<br>IL_01FC callvirt set_localScale | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `Cutscene1::SkewNameEntry` | IL_0031 callvirt set_localPosition<br>IL_004B callvirt set_rotation<br>IL_0064 callvirt set_localScale | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `PuzzleRoom::Update` | IL_0373 ldflda startPos<br>IL_04E9 callvirt set_localPosition<br>IL_050B callvirt set_localScale | 0373 주소 다음 0378 ldfld Vector3.y는 읽기이나 기존 보수적 표시 유지; 장부 아님 |
| `scnTaroMenu2::Start` | IL_009A callvirt set_localScale | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `ffxLightBridge+<>c__DisplayClass9_0::<StartEffect>b__1` | IL_0007 stfld opacity | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `ffxMoveFloorPlus+<>c__DisplayClass17_1::<StartEffect>b__6` | IL_0006 ldflda tweenRot | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |
| `ffxMoveFloorPlus+<>c__DisplayClass17_1::<StartEffect>b__7` | IL_0011 callvirt set_eulerAngles | 실제 상태 쓰기/주소 노출 훅 유지; 장부 추가 제외 없음 |

## 안전망 비용 계산

8,171타일은 `ceil(8171/64)=128`청크다. 매 프레임 `ceil(128/32)=4`청크 × 최대 4타일 = **최대 16표본/프레임**이다. clean 청크도 순환 검사하며 64타일 청크의 완전 재인증에는 16회 방문, 약 512프레임의 안정 구간이 필요할 수 있다.

인계 수치로 계산한 표본당 관측 평균은 `147ms / 57373 = 2.56218µs`다. 이 평균을 그대로 가정하면 `16 × 2.56218 = 약 40.995µs/프레임`이다. **새 DLL에서 측정한 값이 아니라 기존 누적 표본 평균을 이용한 추정**이다. dirty 상태·활성 트윈·엔진 getter 비용에 따라 달라진다.

240FPS × 8초 = 1,920프레임이면 총 예산은 정확히 **288ms**(약 290ms)다. 위 추정 안전망은 78.711ms/8초이며 훅에 남는 예산은 약 109.005µs/프레임, 209.289ms/8초다. 이전 합계는 147+173.8=320.8ms로, 같은 1,920프레임 구간이었다고 가정하면 0.167083ms/프레임이다. 이 가정에서 목표 초과는 32.8ms, 약 11.39%다.

단, 기존 status의 hooks/타일 수 비율은 약 242회인데 safety-samples는 명목 16표본/프레임과 같은 구간으로 맞지 않는다. 기존 누적 카운터는 on/shadow마다 초기화되지 않는다. 따라서 이 비율로 실제 FPS를 확정하지 않는다. 새 측정은 시작/끝 status의 **차이**와 실제 `Time.frameCount` 차이를 사용해야 한다. `pipeline-frame`을 함께 기록한다. `sites on`은 hook/safety 누적 시간까지 지우지 않는다. 진단 카운터 비용은 기존 hook-ms 측정 바깥이므로 성능 판은 반드시 sites off로 측정한다.

## 오프라인 검증

| 검증 | 결과 |
|---|---|
| DEV / Player / AutoTest 빌드 | 모두 오류 0; 경고 각각 38 / 36 / 38 |
| LoadCheck DEV / Player / AutoTest | 틀림 모두 0; 이름 기반 검사 266 / 229 / 266곳, 동적 이름 각 5곳은 실행 중 확인 대상 |
| 기존 coordinator fixture | 70 → **76검사 통과**; 사이트 기본 꺼짐·정렬·상위 12·재설정·중지 추가 |
| 새 writer rewrite fixture | **16검사 통과**; 실제 production transpiler, 장부 4개 stfld를 생성 IL로 실행해 값 보존 및 dirty 0 확인; 실제 상태 쓰기/주소/Transform 보존·label/예외 진입·원본 오프셋/불일치 거부 확인 |
| 실제 게임 DLL 메타데이터 | 53개 대상 각각 유일, 본체/필드 검증 통과 |
| 에디션 타입 검사 | DEV/AutoTest에 FloorRange 및 Writers 존재; Player에는 둘 다 없음; Ledger는 전부 없음 |

게임 메서드나 엔진은 실행하지 않았다. fixture의 엔진/Harmony 모델과 메타데이터 검사는 Mono detour 성공을 증명하지 않는다. 새 원본 instruction reader를 포함한 실제 패치는 Claude의 다음 게임 실행에서 확인해야 한다. 빌드 로그와 SHA-256, fixture 결과는 [검증 요약](tilerange-evidence/t3b-validation-summary.json)에 있다. 기존 t3 증거는 덮어쓰지 않았다.

재현 명령은 [fixture README](../../tools/tilerange-fixture/README.md)에 있다. 새 IL 오프셋 출력은 `tools/ilscan`의 `RAW_IL` 모드이며 `audit-t3b.py`가 전체 IL에서 대상 53개와 장부 독자만 골라 저장한다. task-observer는 이번 제한된 worktree에서 보고 방식으로 적용했으며 전역 관찰 저장소를 변경하지 않았다.

## 내가 돌릴 순서 (Claude)

1. Claude가 별도로 설치하고 게임을 시작한다. 재생 전 정지 상태에서 `tilerange shadow`를 실행해 `patched=53 patch-fail=0 ready=True`를 확인한다. 실패하면 writer-fail 원인을 먼저 확인한다. 이 작업에서는 설치·실행하지 않았다.
2. `tilerange sites on` → 시작 `tilerange status`를 저장한 뒤 **Lost Requiem 531.8초 shadow**를 실행한다. 이벤트 직후 `tilerange status`의 상위 12개 `site[id] calls=...`와 mismatch/late-changes, 시작·끝 pipeline-frame 및 safety/hook 누적값을 저장한다. 먼저 메서드/IL 오프셋으로 매 프레임 호출 원인을 확인한다. direct-mark가 상위면 해당 FloorAnim 경로도 확인한다.
3. 같은 start 로그에서 **skipped-chunks가 늘었는지**, dirty-chunks/tiles-processed가 줄었는지 확인한다. 이번 제외 명시만으로 늘어난다고 가정하지 않는다. mismatch=0과 mismatch-latched=False도 확인한다.
4. `tilerange sites off`. shadow 진단 판을 종료하고 독립 프로세스에서 **끔/켬/켬/끔** 조건으로 시작 시간을 비교한다. 끔은 `tilerange off`, 켬은 shadow 없는 `tilerange on`이며 사이트 진단을 끈다. 같은 맵·시각·설정에서 기존 효과 시작 로그와 켬의 start-ms를 비교한다. original-ms-estimate를 끔 실측으로 대체하지 않는다.
5. 켬 판의 동일 구간 status 차이로 `(Δsafety-ms+Δhook-ms)/ΔframeCount ≤ 0.15ms`를 판정한다. 1,920프레임을 실제 확보했을 때만 288ms 예산을 적용한다. 첫 status 직후의 status/콘솔 작업 프레임은 성능 구간에서 분리한다. 실제 구간 프레임 수와 사이트 진단 상태를 함께 기록한다. 원인 제거·생략률·성능 목표 통과는 이 실행 뒤 판단한다.
