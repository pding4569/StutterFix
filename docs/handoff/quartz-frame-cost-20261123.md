# Codex 작업: Quartz 3.0.0-alpha-4 매 프레임 비용 줄이기 (2026-11-23)

작업 폴더 `E:\StutterFixWork\wt-v14prod`, 브랜치 `claude/v3-282port`. **게임 실행·종료, 설치 DLL 교체, git 커밋/푸시는 하지 않는다.** 게임 측정은 Claude 가 한다.
작업 트리에 Claude 의 커밋 안 된 변경(`HitMeterFade.cs` ProbeNoAlpha, `AutoTest.cs` meternoalpha/canvaslist/canvasoff, `EngineCensus.cs`)이 있다. 덮거나 되돌리지 말 것.

## 배경
StutterFix 는 다른 모드의 쓸데없는 매 프레임 일을 이미 줄인 적이 있다: PACL2 가 매 프레임 같은 글자를 다시 넣는 것을 `TextFix` 가 같은 글자면 건너뛴다(`Compat.cs`, `TextFix.cs` 참고). 다른 모드 감싸기는 `Compat.cs` 의 방식(소유자 이름 확인, 없으면 아무것도 안 함, `Main.Unload` 에서 되돌림)을 따른다.

사용자 PC 의 Quartz 는 `D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\Mods\Quartz\Runtime\versions\3.0.0-alpha-4\QuartzUmm.dll` (Runtime/state.json 의 Current). 설정은 `Mods\Quartz\UserData\*.json`.

## 측정 근거 (HELLO 2026 게임 화면 자동 플레이, 메인 스레드 1kHz 표본, 표본 비율 = 메인 스레드 시간 비율)
- `Quartz.Features.VisualTweaks.VisualTweaks:ApplyBallCoreParticlesTweak` 0.77%
- `Quartz.Features.VisualTweaks.VisualTweaks:ApplyPlanetParticleTweak` 0.63%
- `Quartz.Features.Panels.PanelsOverlay/Updater:Update` 0.51% (`UpdatePanel` 0.47%)
- `Quartz.Core.RuntimeTicks:Tick` 0.47%, `Quartz.LoaderUmm:OnUpdate` 0.51%, `FeatureService/TickAdapter:Tick` 0.35%
- `Quartz.Features.PlanetColors.PlanetColors/PlanetRendererLateUpdatePatch:Postfix` 0.45%, `PlanetColors:ApplyPlanetRing` 0.43%, `Compat.Game.GameApi:SetRingColor` 0.34%
- `KeyViewerOverlay/Updater:Update` 0.31%, `KeyViewerOverlay:UpdateDmNote` 0.24%, `PollUncoveredKeys` 0.12%
- `SongTitleOverlay/HideGameTitlePatch:Postfix` 0.25%, `JudgementPopupHider/HitTextShowPatch:Postfix` 0.20%, `ComboOverlay/Updater:Update` 0.13%, `SongTitleOverlay/Updater:Update` 0.13%, `VisualTweaks:GetPlanetParticles` 0.13%
- 메인 스레드 약 3.1ms/프레임이라 1% ≈ 0.03ms. Quartz 합계 약 4%.

## 할 일
1. QuartzUmm.dll 을 ILSpy CLI/MCP 또는 `tools/ilscan` 으로 읽고 위 함수들이 매 프레임 무엇을 하는지 밝힌다(어떤 엔진 속성을 쓰는지, 값이 실제로 매 프레임 바뀌는지, 문자열·할당이 있는지). **디컴파일한 Quartz 코드를 저장소 파일로 저장하거나 커밋 대상에 넣지 않는다**(공개 저장소, 남의 코드).
2. 화면·동작이 원래와 **같은 채로** 줄일 수 있는 것을 고른다. 예: 매 프레임 같은 ParticleSystem 모듈 값을 다시 쓰는 것 → 마지막으로 쓴 값과 같으면 쓰기 건너뛰기(단, 게임·다른 코드가 그사이 같은 값을 바꿀 수 있으면 안 됨: 예전 FloorFix 실패 사례 CLAUDE.md "지운 것" 참고. 엔진에서 현재 값을 읽어 비교하는 방식이 더 안전하다). 값이 실제로 바뀌는 매 프레임 일은 건드리지 않는다.
3. 설정 `QuartzFrameTrim`(기본 **꺼짐**), 진단 AUTOTEST 명령(예: `quartztrim state` 로 건너뛴 횟수/실제로 쓴 횟수)을 넣는다. Quartz 가 없거나 형식/메서드 이름이 다르면 아무것도 걸지 않고 로그 한 줄만 남긴다. Harmony ID 는 전용, `Main.Unload` 에서 해제. 매 프레임 할당 0.
4. 검증 방법을 적는다: 켬/끔 같은 곡 시각의 실제 화면 비교는 `shot`(framecmp 는 ScreenSpaceOverlay 캔버스를 못 찍는다), 그리고 각 건너뛰기마다 "쓰려던 값 == 엔진의 현재 값" 대조 카운터(verify 모드)로 불일치 0 을 확인할 수 있게.
5. 빌드: `dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1`, `dotnet build -c Debug -p:Edition=Player`, `dotnet build -c Release -p:Edition=Player`, LoadCheck 오류 0.

막히면 다른 방법을 시도한다(예: Quartz 의 Tick 빈도 자체, 보이지 않는 패널의 갱신 건너뛰기 등). 효과가 없을 것 같으면 그렇다고 근거와 함께 적는다.

## 결과물
- `docs/handoff/quartz-frame-cost-20261123-result.md`(한국어): 함수별 분석(코드 인용 없이 동작 요약), 고른 방법과 같음의 근거, 변경 파일 목록(명시적 경로), Claude 가 돌릴 시험 단계(HELLO ABBA: `ab_steps.py` 변형 또는 설정 A/B, verify 단계).
