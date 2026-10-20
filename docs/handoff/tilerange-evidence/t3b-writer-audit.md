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
