# 인계: GPU 장식 소유 + draw 통합을 플레이어 빌드로 이식 (2026-10-26)

작업 트리: `E:\StutterFixWork\wt-port` (브랜치 `claude/player-port`, `codex/night-gpu` 최신을 합친 상태). **너(Codex)는 코드만 짠다.** 게임·Unity·sfmeasure 실행, 커밋, 푸시는 하지 않는다(Claude 가 한다). 빌드(`dotnet build`)와 정적 점검은 해도 된다.
한국어로 보고한다. 설정창·로그 문구도 한국어(기존 문체를 따른다).

## 0. 목표

지금 GPU 장식 소유(`gpudeco live`)와 draw 통합(`drawcollapse`)은 **개발용 자동 시험(AUTOTEST 빌드)에서만, 시험 명령으로만** 돌고, Arche 맵 260~270초 창에 하드코딩돼 있다. 이걸 **일반 플레이어 빌드(`-p:Edition=Player`, AUTOTEST 없음)에서 설정 하나로 켜지는 기능**으로 만든다.
- 측정 효과: Arche 260~270초 실제 재생 FPS 약 165(모드 끔) → 274(켬), 끊김 0. 곡 전체·다른 맵으로 일반화하는 것이 목적이다.
- **기본값은 꺼짐.** 켜지 않은 사용자에게는 런타임 초기화·패치·메모리 사용이 0이어야 한다(`runtime_initialized=False` 수준으로).

## 1. 배경 지식 (코드는 직접 읽어라, 아래는 지도)

- 소유: `GpuDecoExport.cs`(`PrepareGpuLive`/`StartGpuLive`/`StopGpuLive`, 소유 시 원본 렌더러 `forceRenderingOff`), `GpuDecoGpu.cs`(컴퓨트 버퍼·디스패치), `tools/gpudeco-lab/GpuDeco.compute`(`EvaluateCollapse` 커널).
- 통합: `GpuDecoDrawCollapseRenderer.cs`(`PrepareDrawCollapse`, `BuildDrawCollapseSpans`, `RebuildDrawCollapse`, `ValidateDrawCollapse`, `DispatchDrawCollapse`), `DrawCollapsePolicy.cs`(정책·상수), `GpuDecoDrawCollapse.cs`(명령), 셰이더 `GpuDecoBatch.shader`(`generate-drawcollapse-shader.py` 로 생성), 번들 `tools/gpudeco-lab/evidence/drawcollapse5/bundle/stutterfix_gpudeco`(Unity 에디터로만 만들 수 있음, **너는 번들을 다시 빌드할 수 없다**. 현재 번들 그대로 쓴다).
- 이미지 장식: `GpuDecoImage*.cs`(`images own on` 경로), `GpuDecoTrim.cs`. 이미지 소유는 이 구간에서 GPU 병목 때문에 이득이 작거나 느려질 수 있었다(이미지 끔 240 → 켬 276 FPS, 개발용 시험 기준). **기본 정책은 이미지 소유 켬**(최신 측정에서 이득), 단 설정으로 끌 수 있게 한다.
- 시험 명령 진입은 `DecoBake.GpuStep`(AutoTest.cs 의 `case "gpudeco"`)이다. 이 명령 경로(AUTOTEST)는 **그대로 동작해야 한다**(Claude 가 계속 시험에 쓴다).
- `#if DEV || AUTOTEST` 로 감싸진 파일이 대부분이다(csproj 에서도 `AutoTestBuild==1` 조건 파일 16개). 플레이어 빌드에 필요한 최소 집합만 `#if` 를 풀거나 새 조건(예: `GPUDECO`)으로 컴파일에 포함한다. 진단·프로브·픽셀 비교(`FrameCompare`, `GpuHide`, `GpuDecoSnapshot`, 각종 `*Fixture`, `*Probe`, `GpuCensus`, `GpuProbe`)는 플레이어 빌드에 **넣지 않는다**.
- 하드코딩된 창: `PrepareDrawCollapse` 의 텍스처 등록 창 `[257.5, 270.25]`(`VisibleInWindow(table,item,257.5,270.25)`), `DispatchDrawCollapse` 의 `outside_prepared_260..270_window`, `GpuDecoDrawCollapse.cs` 의 `song>=259.75 && song<=270.25`, 게이트의 `Math.Abs(MergeSong-260)` 등. 이들은 **시험용 게이트**와 **제품 동작**을 분리해서, 제품 동작은 곡 전체(또는 곡 시간 창을 일반화한 값)를 쓰게 한다.

## 2. 해야 할 일 (단계별, 앞 단계가 되면 다음)

### 단계 A — 플레이어 빌드에 컴파일되게 (필수)
1. 플레이어 빌드에 필요한 최소 파일 집합을 정한다: 소유(`GpuDecoExport` 의 live 부분·`GpuDecoGpu`·`GpuDecoData`·`GpuDecoPackets`·`GpuDecoRecovery`·`GpuDecoActivation` 등), 통합(`GpuDecoDrawCollapse*`, `DrawCollapsePolicy`), 이미지(`GpuDecoImagePlayback/Plan/Timeline/Variants`, `GpuDecoTrim`), `Dormancy`/`DecoBake*`/`TightLate`/`DecoAnim` 중 의존하는 것. 의존이 시험 전용 심볼(`FrameCompare.Paused`, `Hitch.Playing`, `GpuProbe`, `MergeSong` 의 시험용 게이트 등)을 부르면 제품 경로에서는 안전한 대체(예: `scrController`·`scrConductor` 로 현재 곡 시간 계산)로 바꾼다.
2. `dotnet build -c Release -p:Edition=Player`(AUTOTEST 없음)와 `-p:Edition=Player -p:AutoTestBuild=1` **둘 다** 오류 0, 경고 증가 최소.
3. 플레이어 빌드 결과물에 AUTOTEST/DEV 전용 타입(예: `GpuHide`, `FrameCompare`, `AutoTest`)이 **없는지** 확인한다(리플렉션 없이 `ilscan` 이나 이름 검색으로).

### 단계 B — 제품 진입점 (필수)
1. 설정 `Config.GpuDeco`(정수 0/1, 기본 0)와 `Config.GpuDecoImages`(기본 1)를 추가한다(기존 `ExpFullscreen` 같은 설정 저장/로드 방식을 따른다. `Settings.xml` 에 값이 없을 때 기본이 유지되어야 한다).
2. 설정창(`SettingsWindow.cs`)에 토글 한 줄을 넣는다. 위치는 "플레이" 계열 페이지의 적절한 곳, 문구는 한국어로 짧게, 자세한 설명은 펼침 설명(`SplitDesc`)으로: "GPU 장식 처리(실험): 장식 이동을 GPU가 계산해 CPU 비용을 줄입니다. 켜면 첫 곡 시작이 약간 느려질 수 있습니다." 같은 식. **기본 꺼짐**. 디자인 가이드는 `CLAUDE.md` 의 "설정 창 디자인" 절을 따른다.
3. 번들을 **임베디드 리소스**로 넣는다(`StutterFix.decogpu` 가 이미 `effects/stutterfix_decogpu` 로 AUTOTEST 빌드에서 조건부 포함된 것을 확인하라. 같은 번들을 플레이어 빌드에도 포함하고, 로드는 `AssetBundle.LoadFromMemory`/기존 추출 방식 중 안전한 쪽). 파일 경로 기반(`gpudeco bundle <path>`)은 시험용으로 남긴다.
4. 자동 수명 주기: 설정이 켜져 있고(그리고 D3D11·컴퓨트 셰이더 지원·직교 카메라 등 `RequireLive` 조건이 맞으면) **곡 재생 준비 때 한 번** 준비(`prepare`)하고, 재생 시작 때 켜고(`on`), 곡 끝·죽음·다시 하기·편집기 진입·`Main.Unload` 때 **반드시 되돌린다**(`StopGpuLive` 경로 재사용). `CLAUDE.md` 의 규칙: 게임에 새로 거는 것(Harmony ID, 정적 이벤트, PlayerLoop)은 `Main.Unload` 에 되돌리는 코드를 넣는다. 켜지지 않는 조건이면 **조용히 꺼 둔 채** 이유만 `[StutterFix]` 로그 한 줄(`[GPU 장식] 건너뜀: <이유>`).
5. 실패하면 원본 동작으로 복귀한다(기존 `recovery`/`fallback` 로직). 사용자 곡이 깨지거나 장식이 사라지면 안 된다. 폴백 후에는 그 곡이 끝날 때까지 다시 켜지 않는다.

### 단계 C — 곡 전체로 일반화 (핵심, 가능한 만큼)
1. 시험용 창(260~270)을 제품 경로에서 풀어, 곡 전체에 대해 소유·통합이 돌게 한다. 소유(`gpuScope.WholeSong`)는 이미 `song_union` 범위가 있다.
2. **문제**: draw 통합의 텍스처 은행(`ArrayBindings=4`, `DirectBindings=48`, 합 ≤ 52)은 한 창에서 쓰는 텍스처가 52개 이하일 때만 맞다. Arche 곡 전체의 텍스처 합집합은 약 185개다. 해결 방향(너의 판단으로 가장 단순한 것을 골라 구현하라):
   (a) 곡 시간을 창으로 나누고 창 경계(곡 시간)에서 **재계획 없이** 은행 바인딩만 바꾸는 방식(창마다 쓰는 텍스처를 미리 계산해 두고, 창 경계 직전 프레임들에서 `_Tex0..3`/`_Big0..47` 셰이더 전역 바인딩과 슬롯 테이블 버퍼만 교체). 또는
   (b) 창 경계에서 구간(span)을 새로 계획하는 방식(재계획 비용은 개발용 측정으로 약 75~110ms 이므로 곡 중 한 번 끊김. **끊김이 생기면 안 된다**: 재생 시작 전에 모든 창을 미리 계획해 두고 경계에서는 준비된 것으로 교체만). 또는
   (c) 한 번에 52개를 넘는 곡은 통합을 켜지 않고 **소유(GPU 계산)만** 쓰는 안전 모드로 둔다.
   어떤 방식이든 **곡 중 재계획으로 인한 프레임 끊김이 없어야 한다**(측정은 Claude 가 한다). 구현이 큰 (a)/(b)가 어렵다면 (c)로 먼저 안전하게 내고 그 사실과 이유를 보고서에 적어라.
3. 안전장치 방침(사용자 결정): 곡 중 순서 점검 위반은 **로그만** 남기고 폴백은 심각한 실패(컴퓨트/버퍼 오류, 텍스처 슬롯 미스, 소유 장식 소실 등)에서만 한다. 기본 정책값은 `DrawCollapsePolicy` 의 현재 기본(`KeepAfterBarrier=true`, `SkipOrderSweep=true`, `GlobalVerts=true`, `TileComposite=false`, `DisableOwnedRenderers=false`, `DirectDispatch=false`, `TightAllOwned=false`, `TightLargeQuads=true`)을 그대로 쓴다. 이 값들은 측정으로 정해졌다. 바꾸지 마라.

### 단계 D — 맵·상황별 안전 조건 (필수)
- 장식이 적거나 없는 맵, 컴퓨트 미지원, 해상도·카메라가 특이한 경우, 필터·마스크·SortingGroup·SpriteMask 활성 등 기존 `RequireLive`/`Reason` 이 거절하는 경우 → 조용히 끈다.
- 켜진 상태에서 곡을 다시 하기/죽고 다시 하기/편집기 왕복을 반복해도 메모리·버퍼·렌더러 누수가 없어야 한다(`DisposeDrawCollapse`/`DisposeGpuLive` 가 호출되는 경로 확인, `LeakGuard` 와 충돌 확인).
- 다른 모드(Quartz, AdofaiTweaks, XPerfect 등)와 `forceRenderingOff`/`enabled` 쓰기가 겹치는 기존 처리(`LiveDecoChanges`)는 유지.

## 3. 건드리지 말 것

- 번들·셰이더·컴퓨트 소스(`tools/gpudeco-lab/*`)는 읽기만(수정하면 번들 재빌드가 필요한데 너는 못 한다). 필요하면 보고서에 "번들 변경 필요: <이유>" 로 적는다.
- 측정으로 기각된 것(타일 합성 기본 켬, 원본 렌더러 enabled 끄기, 직접 디스패치, 일반 소유 장식 트림, `ImageMaxSide` 낮추기, 독점 전체 화면)을 기본값으로 켜지 마라.
- 시험 명령 경로의 동작과 로그 문자열(`DRAWCOLLAPSE_*`, `IMAGEOWN_*`, `TILECOMP` 등)은 바꾸지 마라(Claude 의 시험 계획이 파싱한다).

## 4. 검증 (너는 정적·빌드 검증만, 나머지는 Claude)

네가 할 것: 두 빌드 오류 0, AUTOTEST 심볼 미포함 확인, 변경 파일 목록과 각 이유, 단계별로 어디까지 했는지(A/B/C/D) 표.
Claude 가 할 것(참고): 플레이어 DLL 로 Arche 켬/끔 ABBA FPS, 곡 전체 완주·끊김, 폴백 로그, 반복 재시작 누수, 설정창 확인.

## 5. 산출

- 코드 변경(커밋은 하지 마라). 마지막에 `docs/handoff/codex-player-port-20261026-result.md` 를 써라: 단계별 완료 여부, 새 설정 이름과 기본값, 곡 전체 방식 (a)/(b)/(c) 중 무엇을 골랐는지와 이유, 알려진 한계, Claude 가 게임에서 확인해야 할 항목 목록.
