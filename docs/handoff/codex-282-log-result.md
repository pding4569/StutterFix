# StutterFix 2.8.2 로그 작업 결과

작업 위치: `E:\StutterFixWork\wt-282log`  
브랜치: `claude/282-log`, 시작/종료 HEAD: `356e37d` (지시서 커밋, 부모 `d099b46`). 커밋·푸시·게임 실행은 하지 않았다.

## 구현

- `Lg.cs`를 추가하고 프로젝트에 포함했다. `Lg.I/W/E`가 정보·경고·오류 출력을 한 곳에서 처리한다.
- Player의 `VerboseLog` 기본값은 false다. 필수 메시지 목록과 진단 예외 목록은 `Lg.cs` 한 곳에 있다. 오류·경고 호출은 본문에 특정 단어가 없어도 통과한다.
- DEV/AUTOTEST는 `Verbose`에 false를 대입해도 항상 true이며 필터와 제한을 우회한다. 기존 자동 시험 로그의 본문·심각도·줄 수를 유지하기 위한 처리다.
- 정식 Player는 자세히 모드에서도 같은 키를 10초에 3회, 전체를 고정 1초 창당 100줄로 제한한다. 생략 요약도 전체 한도에 포함한다.
- 숫자를 제외한 첫 60글자를 문자열 생성 없이 해시·비교한다. 1,024개 슬롯과 최대 4회 탐색으로 메모리와 탐색량을 제한한다. 슬롯 경합·충돌·포화 시 같은 줄 제한은 더 출력하는 쪽으로 완화하되 전체 제한은 원자적 CAS로 유지한다. 잠금은 쓰지 않는다.
- 출력이 잠잠해져도 System.Threading.Timer가 1초마다 만료 창을 확인한다. 같은 줄 요약은 10초 만료 후 다음 확인 또는 다음 호출 때, 전체 초과 요약은 다음 초의 확인 또는 다음 호출 때 기록된다. 타이머 스케줄링 지연은 가능하다. 언로드 시 타이머를 정리한다. 새 게임 API 호출은 추가하지 않았다.
- `Main.ApplyConfig`와 초기 설정 불러오기에 VerboseLog를 반영했다. 설정창의 문제 보고/로그 내보내기 카드에 기존 Option/T 패턴의 한영 토글과 설명을 추가했다. 변경 즉시 적용·저장된다.
- LogExport의 설정 목록에는 저장된 VerboseLog와 실제 Verbose 상태를 함께 넣었다.
- 모드 소스 132개 파일의 호출 726곳(Log 671, Warning 1, Error 54)을 기계적으로 바꿨다. 형식 문자열과 인자는 유지했다. 이 중 StutterFix.cs/SettingsWindow.cs 외 130개 파일은 호출 치환만 변경됐음을 원본과 대조했다.

## 오프라인 전곡 로그 집계

대상: `tools/ReleaseCheck/out/full-game-2.8.2.log`의 `[StutterFix]` 시작 줄. 실제 `Lg.IsEssential`을 포함한 LogCheck로 계산했다.

| 구분 | 줄 수 |
| --- | ---: |
| 기존 전체 | 108 |
| 필수 필터에 맞는 줄(UMM 버전/불러오기 1줄 포함) | 12 |
| 필터 밖이지만 UMM이 직접 쓰는 Active./Loading time | 2 |
| 실제 예상 잔여량(위 두 항목 합계) | 14 |
| 줄어드는 양 | 94 (약 87.0%) |

모드가 직접 쓴 105줄 중 11줄이 남고, UMM 자체의 3줄은 그대로 남는 계산이다. 25줄 안팎 목표보다 적지만 목표 개수에 맞추려고 설치·상태 로그를 추가하지 않았다. 게임 개조 표시, 기능 설치 완료 요약, D3D/렌더링 방식, 프레임 통계 상태, 캐시 공간 부족 2줄, 멈춘 창 진단 2줄, 곡 시작 화면 설정, 곡 요약을 남긴다.

`[이미지] 해독 시간` 1줄은 본문에 `못 한 것 0`, `못 푼 것 0`이 있어 지시서의 문자 포함 규칙(`못 `)에 따라 남는다. 실패 수가 0인 경우도 규칙을 임의로 예외 처리하지 않았다. 그 외 이미지 단계 시간은 자세히 모드에서만 보인다.

원본 로그에는 시각이 없어 이 집계에는 시간 제한을 적용하지 않았다. 위 14줄은 필터 기준 예상량이며, 실제 반복/초당 제한의 결과나 생략 요약 개수는 실시간 호출 간격에 따라 달라진다. 실제 게임에서 측정한 결과가 아니다.

## 게임 없이 수행한 검증

| 검증 | 결과 |
| --- | --- |
| `dotnet build StutterFix.csproj -c Release -p:Edition=Player` | 오류 0, 경고 36 |
| `dotnet build StutterFix.csproj -c Release -p:Edition=Player -p:AutoTestBuild=1` | 오류 0, 경고 40 |
| `dotnet build StutterFix.csproj` (Debug/DEV) | 오류 0, 경고 38 |
| `dotnet build tools/LoadCheck -c Release -o tools/LoadCheck/bin` | 오류 0, 경고 0 |
| `tools/LoadCheck/bin/LoadCheck.exe bin/Player/StutterFix.dll` | FieldRefAccess 229곳, 틀림 0, 동적 이름 6곳 |
| `tools/LoadCheck/bin/LoadCheck.exe bin/Debug/StutterFix.dll` | FieldRefAccess 266곳, 틀림 0, 동적 이름 6곳 |
| LogCheck Player | 27개 통과, 실패 0 |
| LogCheck DEV | 27개 통과, 실패 0 |
| LogCheck AUTOTEST | 27개 통과, 실패 0 |
| 모드 범위 직접 Logger 호출 검색(Lg 제외, tools/native 제외) | 잔여 0 |
| 기존 파일 인코딩/줄 끝 바이트 검사 | 변경 134개 모두 UTF-8/BOM 보존, CRLF |
| `git diff --check` | 통과 |

LogCheck는 Lg.cs를 직접 포함하며 Unity나 게임 어셈블리에 의존하지 않는다. 주입한 시계와 출력 함수로 3회/10초 경계, 100회/초 경계, 숫자/유니코드 숫자 제거, 첫 60글자 키, 정보 필터, 경고·오류, 자세히 모드 제한, DEV/AUTOTEST 원문/무제한 출력, 다음 호출/유휴 만료 요약, 요약의 전체 한도, 지연된 시각, 동시 호출/동시 만료, 재진입을 검사했다. 이미 만들어진 문자열을 전달하는 반복 억제 10,000회와 허용 경로 10,000회에서 검사 코드의 추가 할당은 각각 0바이트였다. 호출자가 미리 만드는 문자열과 실제 출력/생략 요약의 할당까지 없다는 뜻은 아니다.

재현 명령:

```powershell
dotnet run --project tools/LogCheck -c Release -- tools/ReleaseCheck/out/full-game-2.8.2.log
dotnet run --project tools/LogCheck -c Release -p:LogEdition=Dev
dotnet run --project tools/LogCheck -c Release -p:LogEdition=AutoTest
```

세 LogCheck 명령은 같은 출력 폴더를 사용하므로 순서대로 실행한다. 최종 재검증 도중 빌드를 겹쳐 실행해 DEV 한 번이 실패했으며, 다른 실행 종료 후 단독 재실행으로 27개 통과를 확인했다. Player 최초 빌드는 게임의 Timer 타입과 이름이 겹쳐 실패했으나 System.Threading.Timer로 명시해 수정한 뒤 세 빌드를 모두 통과했다.

실행 증거는 `tools/LogCheck/out/{player,playerauto,debug}-build.txt`와 `{player,dev,autotest}-tests.txt`에 있다. out/은 해당 프로젝트의 .gitignore로 제외했다. 빌드 경고에는 게임 참조 어셈블리 버전 충돌과 사용하지 않는 필드 등이 포함된다.

## 미수행 및 범위 제한

- 지시대로 게임은 실행하지 않았다. 실제 UMM 로그 파일, 설정창 배치·클릭·저장 재실행, Unity에서의 타이머 출력, 실제 크래시 감소는 확인하지 않았다.
- LoadCheck는 IL/필드 참조 정적 검사다. 게임에서 모드를 초기화한 검증이 아니며 동적 필드 이름 6곳은 실행 중 확인 대상이다.
- `file`은 두 번 시도했으나 Git 제공 실행 파일이 `CreateFileMapping ... Win32 error 5`로 시작에 실패했다. 대신 원본 대비 BOM과 UTF-8 디코딩, CRLF/LF 바이트 수를 검사했다. 새 파일도 UTF-8/BOM 없음/CRLF로 저장했다.
- 지시서의 tools/native 제외 규칙 때문에, 무조건적인 저장소 전체 `git grep`에는 tools/ 별도 프로젝트의 기존 Logger 호출 15줄이 남는다. 해당 프로젝트들은 수정하지 않았다. 모드 본체는 Lg.cs 내부의 출력 3곳만 남는다.
- DEV/AUTOTEST에서 제한까지 적용하면 기존 자동 시험 로그의 줄 수가 바뀌므로 이 빌드들은 기존 출력을 유지했다. 정식 Player의 보통/자세히 경로에는 모두 제한을 적용했다.
- 커밋·푸시·게임 실행·설치 DLL 교체는 하지 않았다.

## 바뀐 파일

직접 구현/설정 연결: `Lg.cs`, `StutterFix.cs`, `SettingsWindow.cs`, `LogExport.cs`, `StutterFix.csproj`.

새 시험 프로젝트: `tools/LogCheck/LogCheck.csproj`, `tools/LogCheck/Program.cs`, `tools/LogCheck/.gitignore`.

결과 문서: `docs/handoff/codex-282-log-result.md`.

기존 변경 파일 전체(134개, 대부분 호출 치환):

```
AbTest.cs
AllocFix.cs
AllocProbe.cs
AllocScan.cs
AnimationAudit.cs
AssetDiskCache.cs
AudioTap.cs
AutoTest.cs
BeatFix.cs
BigLevel.cs
BlendProbe.cs
BootConfig.cs
Compat.cs
DecoAnim.cs
DecoBatch.cs
DecoDiag.cs
DecoEngine.cs
DecoGpu.cs
DecoJobShadow.cs
DecoRender.cs
DecoSim.cs
DecodeFix.cs
DecorationAudit.cs
Dormancy.cs
EditorPick.cs
EffectBudget.cs
EffectScan.cs
EffectsGpuProbe.cs
ExitFix.cs
FastBlend.cs
FastJson.cs
FastMove.cs
FfxReuse.cs
FilterGpuProbe.cs
FilterHalfPerf.cs
FilterHalfProbe.cs
FilterIdentityProbe.cs
FilterTrace.cs
FloorAnim.cs
FloorLateFix.cs
FloorProf.cs
FrameGen.cs
FrameParts.cs
FrameRateScreenWatch.cs
FrameScan.cs
FreeFpsProbe.cs
Fsr.cs
FxSlots.cs
GameScreenButton.cs
GcControl.cs
GpuCensus.cs
GpuHide.cs
GpuProbe.cs
HalfRender.cs
HitMeterFade.cs
HitTextFade.cs
Hitch.cs
HitchHeatmap.cs
ImagePrefetch.cs
InstBench.cs
InstantMove.cs
InvisibleSkip.cs
JitWarm.cs
LazySprite.cs
LeakGuard.cs
LoadFix.cs
LogExport.cs
LoopProfiler.cs
LowEnd.cs
MatReuse.cs
MemoryBudget.cs
MergeProbe.cs
MeshBench.cs
MeshWarm.cs
MicroBench.cs
ModWatch.cs
MoveApply.cs
MoveProf.cs
NativeMotion.cs
OffscreenProbe.cs
OptimizationModules.cs
ParallelDecode.cs
ParticleFix.cs
ParticleTextWatch.cs
PatchHot.cs
PcTune.cs
PerfOverlay.cs
PhaseWatch.cs
PhysicsPause.cs
PlayTweaks.cs
PosFast.cs
Precheck.cs
PresentWatch.cs
Profiler.cs
RecolorDefer.cs
RecolorSplit.cs
RenderCallbackScan.cs
RenderVerify.cs
RenderWatch.cs
Resilience.cs
RestartAdvisor.cs
SamplerWatch.cs
SceneReset.cs
SceneScan.cs
ScreenEffects.cs
ScriptProbe.cs
SettingsWindow.cs
ShaderWarm.cs
SlowScan.cs
SortProbe.cs
SoundWarm.cs
StartProbe.cs
StutterFix.cs
StutterFix.csproj
TagIndex.cs
TexCompress.cs
TextFix.cs
TextureCensus.cs
ThumbCam.cs
TightLate.cs
TileCost.cs
TileCull.cs
TileSim.cs
TimeProbe.cs
TransitionFix.cs
TweenFix.cs
TypeCache.cs
UiProf.cs
Updater.cs
UploadBudget.cs
VramGuard.cs
WasteProbe.cs
WindowGhost.cs
ZeroTween.cs
```

CODEX_282_LOG_DONE
