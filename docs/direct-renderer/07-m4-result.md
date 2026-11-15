# 직통 렌더러 M4 결과 — M4a 상한 진단 인계

2026-11-15. 작업 폴더 `E:\StutterFixWork\wt-m4`, 브랜치 `claude/drfull`, 시작 HEAD `52592e34eb358dd29d920b6d4a8b5b4a9b67019b`.

**이번 산출물은 M4a(`drfull ceil` / `drfull census`)다. M4 전체 렌더러를 완성한 결과가 아니다.** 설계서 마지막 순서의 “M4a 먼저”, “Claude가 상한 FPS를 먼저 본다”에 해당하는 첫 인계 지점까지 구현·오프라인 검증했다. 게임 실행 금지에 따라 그 FPS를 여기서 측정하지 않았다. M4b의 원래 셰이더 로더·WARP/Unity 대조, M4c의 전체 네이티브 그리기, M4d의 보조 카메라 합성은 **미구현**이다. `drfull on`은 설명과 함께 예외로 거부한다.

커밋·게임 실행·설치 DLL 교체를 수행하지 않았다. native 빌드는 작업 폴더의 `native/sfnative.dll`만 갱신했다. M3 코드·장식 채널 수학·`ShapeCellsCpu`·`SceneRenderOrder`·기존 native 소스와 export 정의는 유지했다.

## M4a 구현

- [DirectFull.cs](../../DirectFull.cs): `drfull ceil`을 호출한 현재 메인 카메라 `Camera`의 맵 내용 층만 cullingMask에서 뺀다. **대체 그리기는 0회**다. 필터·타깃 RT·투영·다른 카메라 설정을 바꾸지 않는다. 게임 Renderer의 enabled/forceRenderingOff, GameObject.layer, 콜라이더, 게임 스크립트 실행 상태는 바꾸지 않는다. 이 진단의 빈 맵 화면은 의도된 결과다.
- 카메라 층 이름 `Foreground`, `Planet`, `Floor`, `BG`, `BG Static`, `TopGlow`, `FloorTemp`, `textDeco`를 런타임에 번호로 바꾼다. 설치 게임의 `globalgamemanagers` TagManager를 읽어서 각각 **7, 8, 9, 10, 11, 14, 15, 18**임을 확인했다. 특히 장식 정렬층 `Bg`는 GameObject 층 `Floor=9`이고, 앞 장식 정렬층 `Default`는 GameObject 층 `Foreground=7`이다. `scrVisualDecoration.SetDepth`의 9/7 및 타일 따라감 경로도 읽었다. 제거 대상은 항상 **원래 메인 카메라 마스크와 이 집합의 교집합**이다. UI=5, Flash=13 등은 제거 집합에 넣지 않았다.
- [DirectFull.MaskLease.cs](../../DirectFull.MaskLease.cs): 원래 마스크를 한 번만 보관한다. 반복 ceil이 이미 줄어든 마스크를 원래 값으로 덮어쓰지 않는다. 현재 마스크가 0인 경우와 부호 비트가 있는 마스크도 복원한다. 원래 마스크에 제거할 층이 전혀 없으면 변경 없이 거부한다.
- `off`, 재시작, 커스텀 레벨 초기화, 메뉴 복귀, 편집 복귀/재생, ResetScene, PrepVfx, 활성 화면 변경·화면 unload, 카메라 비활성/파괴, 진단 객체 비활성/파괴, AutoTest 정상/오류 종료, Main.Unload에 반환 경로를 연결했다. 파괴된 카메라는 Unity 생존 검사 후 건너뛴다. 이전 진단 객체의 지연 파괴 콜백은 객체 identity로 걸러 같은 프레임의 새 ceil을 중단하지 않게 했다. off는 카메라가 살아 있으면 `originalMask`와 실제 다시 읽은 `restoredMask`를 함께 기록한다.
- 다른 코드가 cullingMask를 바꾸거나 GpuDeco/RunLod/DR2가 켜지면 표본 무효 사유를 남기고 원래 마스크로 복원한다. DR2/DRX/GpuDeco/RunLod가 켜진 상태와 기존 DR CommandBuffer가 있는 상태의 시작은 거부한다. DRX도 ceiling 중 시작할 수 없다. 다른 모드의 임의 native 직접 제출까지 모두 식별하는 보장은 없다.
- 진단 중의 Update와 메인 Camera.onPreCull은 카메라/모드 상태만 확인한다. 매 프레임 Renderer 전수 검사·정렬·자료 생성은 없다. off 때 이벤트·Harmony 후크·진단 객체를 제거한다. 활성화 비용과 로그/문자열 생성은 측정 구간 밖으로 뺀다.
- 기본 꺼짐이며 활성 설정을 저장하지 않는다. DEV/PlayerAuto에만 명령·진단 코드·셰이더 목록을 넣었고 일반 Player에는 모두 제외했다.

## census와 원본 프로그램 조사

[DirectFull.Census.cs](../../DirectFull.Census.cs)는 장면의 활성/비활성 Renderer를 재질 슬롯별로 분류한다. 카메라에 원래 들어오던 항목과 맵 관련 항목을 포함한다. ceiling 중에도 **원래 마스크**를 기준으로 센다. 종류·GameObject 층·정렬층/순서·큐·셰이더·정렬한 키워드·메시의 정점 attribute/형식/차원/stream·인덱스 형식·submesh 수·Sprite drawMode/maskInteraction·property block 유무를 기록한다. Native buffer pointer를 요구하거나 메시 CPU 읽기를 강제로 켜지 않는다.

명령 한 번에 모드 폴더에 `drfull-census-<UTC 시각>-{renderers,groups,shaders}.tsv`를 쓴다. 모든 장면 카메라의 마스크/RT, Canvas, SpriteMask, 실제 층별 수도 로그에 남긴다. 그룹 수는 **재질 슬롯 수**, 최종 renderers/affected는 **Renderer 수**다. candidate는 종류만 보고 분류한 미검증 후보이며 네이티브 소유 수가 아니다. `knownMapOutsideRemovedLayers`가 0이 아니면 빠진 층을 먼저 조사해야 한다. 같은 층에 UI/다른 내용이 섞인 경우도 상세 TSV에서 확인해야 한다.

[audit-original-shaders.py](../../tools/direct-renderer/audit-original-shaders.py)는 설치 게임의 assets를 읽기만 한다. 기존 다른 checkout의 UnityPy 1.25.4 로컬 의존성을 `--deps`로 재사용했다. `.assets`의 D3D11 플랫폼 블록을 LZ4 해제한 뒤 DXBC 컨테이너/청크/길이/VS·PS 단계를 검사하고 실제 D3DDisassemble로 받아들여지는지 확인했다. **422개 셰이더 이름, 프로그램 3,454개, 오류 0개**다. 같은 이름이 여러 에셋에 있으면 `YES_MULTIPLE_ASSETS`와 각 에셋 identity를 보존하며 임의로 하나를 고르지 않는다.

증거는 [shader-audit.log](../../out/direct-renderer/m4/shader-audit.log), [inventory.json](../../out/direct-renderer/m4/shaders/inventory.json), [내장 census 표](../../effects/direct-full/shader-census.tsv)다. 주요 셰이더는 pass/keyword index/cbuffer offset 등이 든 Unity parsed metadata JSON도 따로 보관했다. **내장한 것은 census 표이며 원본 DXBC 로더/바이트코드 리소스가 아니다.**

| 셰이더 | DXBC 추출 가능 | VS / PS 프로그램 수 | RDEF 프로그램 수 |
|---|---|---:|---:|
| Sprites/Tile | 예 | 4 / 4 | 0 |
| ADOFAI/FloorMesh | 예 | 2 / 2 | 0 |
| Legacy Shaders/Particles/Additive | 예 | 2 / 2 | 0 |
| ADOFAI/Frames | 예 | 4 / 4 | 0 |
| ADOFAI/TiledSpriteWithGap | 예 | 1 / 1 | 0 |
| ADOFAI/HoldShader | 예 | 1 / 1 | 0 |
| Hidden/BlendModes/VisualDeco/Grab | 예 | 26 / 26 | 0 |
| Hidden/BlendModes/SpritesDefault/Grab | 예 | 200 / 200 | 0 |
| Hidden/BlendModes/SpritesDefault/Framebuffer | 직접 VS/PS 없음 | 0 / 0 | 0 |
| Sprites/Default | 이번 assets 목록에서 찾지 못함 | 미확인 | 미확인 |

수는 에셋 블록의 프로그램 수다. 독립 변형 수나 정확한 VS/PS 짝 수를 뜻하지 않는다. 실행 시 census에서 표에 없는 셰이더는 UNKNOWN으로 남긴다. Built-in 셰이더/UsePass 참조 등의 해석은 아직 하지 않았다.

**M4b에서 해결할 실제 제약:** 위 주요 프로그램에는 cbuffer 이름/변수 배치가 들어 있는 `RDEF` 청크가 없다. 따라서 DXBC 반영 정보만으로 이름·오프셋을 전부 얻을 수 있다는 설계 가정은 이 에셋에서 성립하지 않는다. 보관한 Unity pass/subprogram 메타데이터에서 cbuffer 슬롯·오프셋·keyword/프로그램 연결을 읽고 바이트코드 선언·입력 signature와 교차 검증해야 한다. 현재의 바이트 패턴 검색 순서를 VS/PS 변형 짝으로 간주하면 안 된다. 그 다음 입력 layout, 행렬 배치, 재질/글로벌/MPB, Sprite 추가 상수, 실제 pass 렌더 상태까지 맞춰야 픽셀 비교를 시작할 수 있다. 원본 DXBC를 그대로 쓴다는 사실만으로 픽셀 일치를 선언하지 않는다.

## 로컬 검증

증거 폴더: [out/direct-renderer/m4](../../out/direct-renderer/m4).

| 검사 | 결과 |
|---|---|
| DEV / Player / PlayerAuto | 오류 0. 최종 로그 경고 84 / 4(증분 빌드) / 63개 |
| native/build-sfnative.bat | 통과. 기존 빌드 옵션 유지 |
| native export 이름/ordinal/hint | 73/73 보존 |
| 관리 DLL 내장 native | 최신 작업 산출물과 3/3 일치 |
| DEV/PlayerAuto census 리소스 | 현재 표와 2/2 일치 |
| 일반 Player | DirectFull 타입과 census 리소스 모두 제외 |
| 생산 DirectFull.cs 수명 fixture | 25종 통과: 반복 on/off, 현재 마스크 0, 빈 원래 마스크 거부, 재시작/편집/화면 전환, 카메라 비활성/파괴, 외부 마스크 쓰기, 활성화 도중 실패 8지점, 경합 거부 등 |
| 임의 마스크 복원 | 10,000개 일치. 부호 비트 포함 |
| 안정 검사 할당 | fixture의 100,000회 CheckLease+PreCull에서 관리 할당 0바이트 |
| 실제 설치 게임 메타데이터 | 수명 후크 대상 8개 존재 및 PrepVfx 인자 수 확인. 게임 메서드 실행 없음 |
| git diff --check | 통과 |

fixture는 실제 생산 수명 코드를 가짜 Unity/Harmony 경계로 컴파일해 돌린 것이다. **Unity 엔진의 실제 callback/Destroy 타이밍·실제 후크 설치·실게임 복원을 검증한 결과가 아니다.** 0바이트도 이 관리 fixture의 결과이며 Unity 실행 중 GC 측정을 대신하지 않는다. 게임 FPS·CPU/GPU 시간·물리/히트박스·필터·픽셀·메인 0.3ms/렌더 1ms 목표는 전부 미측정이다. Unity 캡처, WARP 원본 셰이더 fixture도 이번에는 실행하지 않았다.

### 설치 파일 관찰

이 세션은 설치 경로에 쓰기·교체·복원 명령을 수행하지 않았다. 시작/종료 SHA 대조에서는 **native만 일치하고 managed는 작업 도중 달라졌다(1/2)**. 파일 변경 주체를 조사하지 않았으므로 추정하지 않는다. 변경된 설치본을 덮어쓰거나 시작 버전으로 되돌리지 않았다. 검증기는 이를 PASS로 숨기지 않고 HOLD로 기록한다.

| 설치 파일 | 시작 SHA-256 | 후속 관찰 SHA-256 |
|---|---|---|
| StutterFix.dll | `70E9873719427E8435BF59E6D1371083DA344D4F86C277B5AE5A7654703BB1DA` | `B0EFE68D6CCCE3217310DA0AD65CBA4048C19D2D25F738C97F14D7822898C795` |
| sfnative.dll | `E7A7CF6026892C3082224C2CB2C45D3DC8B2D35F9045DEBE81E1241590ACE05A` | 동일 |

[installed-before.json](../../out/direct-renderer/m4/installed-before.json), [installed-after.json](../../out/direct-renderer/m4/installed-after.json), [installed-result.txt](../../out/direct-renderer/m4/installed-result.txt)에 보존했다. 이후 다른 작업에서 다시 바뀔 수 있으므로 실게임 인계 시 설치본을 따로 식별해야 한다.

## 재현 — 게임 실행 없음

```powershell
py -3.12 tools/direct-renderer/audit-original-shaders.py --deps E:/StutterFixWork/wt-v14prod/out/asset-reader-deps --game-data "D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice/A Dance of Fire and Ice_Data" --output out/direct-renderer/m4/shaders --catalog effects/direct-full/shader-census.tsv
powershell -NoProfile -ExecutionPolicy Bypass -File tools/direct-renderer/check-m4.ps1
py -3.12 tools/direct-renderer/plan-m4a.py
```

`check-m4.ps1`은 native를 먼저 `native/build-sfnative.bat`으로 빌드하고 아래 관리 3종, 수명 fixture, 실제 어셈블리 메타데이터·리소스·해시 검사를 한다. 설치 경로는 해시 읽기만 한다. 기존 `installed-before.json`은 덮어쓰지 않는다. 별도 작업의 새 시작 기준선은 새 OutputDirectory로 보관한다.

```powershell
dotnet build StutterFix.csproj -c Release
dotnet build StutterFix.csproj -c Release -p:Edition=Player
dotnet build StutterFix.csproj -c Release -p:Edition=Player -p:AutoTestBuild=1
dotnet run --project tools/direct-renderer/m4-check -c Release -- .
py -3.12 tools/direct-renderer/check-m4-artifacts.py
```

native 로그는 `build-native.log`, 관리 로그는 `build-{dev,player,playerauto}.log`, 나머지는 `lifecycle.log`, `exports-result.txt`, `artifacts.json`에 있다.

## Claude의 M4a 실게임 측정 명령과 bench 설정

**이 절은 실행하지 않은 인계 계획이다.** [plan-m4a.py](../../tools/direct-renderer/plan-m4a.py)는 계획 JSON만 만들며 설치·게임 실행 기능이 없다. 기존 `E:\StutterFixWork\bench_v14.py`의 현재 `ppGpuCam` 설정·명령을 읽어 재사용한다. `BENCH_WT=wt-m4`, RT `3440x1440`, GpuDeco=0, RunLod=false, DR2 off, fpscap=0이다. 숨김 진단이나 scriptskip을 추가하지 않는다. 최종 전체 settings/명령은 각 JSON에 저장된다.

- 맵/목표 곡 시각: HELLO `hl50` 50초, Windflower `wf` 258초, DD `dd` 50초, Exit `sh` 88초.
- 순서: 판마다 새 실행으로 A1→B1→B2→A2. A는 원래 Unity, B는 `drfull ceil`. 각 판 FPS 구간 12초. 활성화/카메라 계측 준비 뒤 1초 여유를 두며 실제 측정 곡 시각은 로그의 경계를 쓴다.
- A/B 모두 기존 `presentpath on gputime cameras`와 assert를 사용한다. CPU는 같은 `cpuwindow begin/end` 경계로 함께 읽는다. CPU 누적과 GPU 구간은 서로 더하지 않는다.
- census는 각 맵 별도 판이다. 파일 생성·전수 조사·로그가 들어가므로 `census` 판의 FPS는 ABBA에 합치지 않는다.
- 예상 준비 파일: [plans/manifest.json](../../out/direct-renderer/m4/plans/manifest.json) 및 맵당 `m4a-<맵>-{A1,B1,B2,A2,census}.json`, 총 20개. 기존 `run_locked`가 읽는 steps/settings/timeout_min/tag를 담았다. 자동 실행은 하지 않았다.

핵심 AUTOTEST 순서는 다음과 같다(실제 open/seek/계측 설정까지 들어간 전체본은 JSON 참조).

```text
dr2 off
drfull off
drfull assert off
# open/seek 후 기준 또는 후보 선택
drfull ceil
wait 1
drfull state
drfull assert ceil
cpuwindow begin
phasewindow begin
fpsmark start
wait 12
fpsmark end m4a-B
phasewindow end
cpuwindow end
drfull assert ceil
drfull state
drfull off
drfull assert off
drfull state
```

A판은 ceil 대신 off/assert off를 쓴다. census판에서는 곡 시각에 도달한 뒤 `drfull census`를 별도로 실행한다. `state`의 mean_fps는 ceil 이후 준비 구간까지 포함하므로 ABBA 수치에는 fpsmark 구간을 사용한다. B판에 `mode=M4a-ceiling active=True draws=0` 및 `assert ceil PASS`가 없거나 `sample invalid`/자동 복원이 발생하면 그 판은 무효다. 종료 로그의 `originalMask == restoredMask`와 off 상태도 확인한다.

상한 측정 뒤 off/restart/다른 맵/편집 복귀/Unload에서 실제 카메라 마스크와 화면·충돌 보존을 확인한다. ceiling 화면은 의도적으로 맵을 그리지 않으므로 원본과의 픽셀 일치나 완성 렌더러 성능으로 해석하지 않는다.

## 다음 단계의 미완료 범위

1. Claude의 M4a 실게임 결과: A/B FPS·메인/렌더/GPU·최장 프레임·층/렌더러 census·실제 원복. 현 상태는 미측정.
2. M4b: Unity metadata로 정확한 pass/키워드/VS·PS 및 cbuffer 바인딩을 구성하고 원본 DXBC 리소스·native 로더를 구현한다. Sprites/Tile/FloorMesh의 비어 있지 않은 WARP 결과를 같은 상태의 Unity 캡처와 대조한다. RDEF 부재와 Sprites/Default의 위치를 해결해야 한다.
3. M4c: native mesh 버퍼/input layout과 Sprite 정적 geometry, 게임 상태의 변경 알림, 원래 정렬 전체 목록, SpriteMask를 구현한다. 아직 Unity와 같은 재질의 완전 동점 규칙을 확정한 것으로 취급하지 않는다. 부분 잔여의 순서 안전성도 검사해야 한다. 현재 `drfull on`을 활성 경로로 오해하지 않게 거부한다.
4. M4d: 지원 못 하는 Renderer를 정렬 연속 구간별 보조 RT에 그려 제자리에 합성한다. 콜라이더 GameObject의 layer 이동 없이 수명·스텐실·합성 알파를 검증한다. 현 상태에는 보조 카메라/RT/합성이 없다.

M4b~d가 구현되지 않았으므로 해당 단계의 AUTOTEST/bench 성공 설정이나 수치는 제시하지 않는다. 후속 구현에서도 각 단계 4종 빌드와 픽셀·실게임 비용·원복 검증을 각각 추가해야 한다.

---

## 2026-11-15 후속 작업 — M4b 원래 셰이더 로더·M4c `drfull on`

작업 폴더 `E:\StutterFixWork\wt-m4`, 브랜치 `claude/drfull`, 시작 HEAD `5d9b563`. 위 M4a 기록은 당시 상태로 남겼다. **이번에는 M4b와 M4c의 실행 경로 및 오프라인 fixture까지 구현했다. `drfull on`이 원래 DXBC로 타일·스프라이트를 제출하며, 실게임 AUTOTEST가 가능한 DEV/PlayerAuto 산출물을 만들었다.** 게임에서 켜서 검증했다는 뜻은 아니다. M4d 보조 카메라/RT 합성은 포함하지 않는다.

커밋·게임 실행·설치 DLL 교체를 수행하지 않았다. Claude의 게임 프로세스가 살아 있는 동안에는 소스 작업을 진행했고, 종료를 확인한 뒤 로컬 빌드와 WARP fixture를 실행했다. 기존 M3는 유지했다. 직접 수정한 native DLL은 작업 폴더의 `native/sfnative.dll`뿐이다.

### M4b — RDEF 없이 원래 프로그램과 바인딩 연결

- [export-original-shaders.py](../../tools/direct-renderer/export-original-shaders.py)는 Unity의 압축 블록 **entry table과 segment 번호**를 읽고, 각 stage의 `m_BlobIndex` 및 `m_ParameterBlobIndices`로 프로그램/추가 파라미터 블롭을 정확히 찾는다. 이전의 DXBC 바이트 패턴 발견 순서로 VS/PS를 짝짓지 않는다.
- 보관했던 parsed form의 `m_NameIndices`, common/partial cbuffer, texture/sampler, 키워드 인덱스를 추가 파라미터 블롭과 합친다. 슬롯·바이트 오프셋·타입·행렬 크기를 복구한다. 키워드 이름이 프로그램 블롭과 같은지, VS/PS 키워드 집합이 같은지 검사한다.
- DXBC의 `dcl_constantbuffer` 슬롯/크기와 실제 명령이 참조하는 DWORD 성분을 대조한다. 읽는 cbuffer 성분에 대응하는 이름/범위가 없으면 추출을 거부한다. texture/sampler 선언, VS 입력 signature, VS 출력↔PS 입력 semantic/type/mask도 교차 검사한다. 지원하지 않는 buffer/UAV 선언은 거부한다.
- `Sprites/Default`와 `Sprites/Mask`는 **`Resources/unity_builtin_extra`**에서 찾았다. 원래 assets와 built-in 파일의 SHA-256, 연결한 바인딩, 각 DXBC 해시·disassembly를 [shaders/bindings.json](../../out/direct-renderer/m4bc/shaders/bindings.json) 및 같은 폴더에 남겼다.
- 결과 리소스 [original-shaders.bin](../../effects/direct-full/original-shaders.bin)은 **3,044,920바이트, 셰이더 이름 21개, 패스별 변형 레코드 1,012개, stage 참조 2,024개, 중복을 뺀 DXBC 260개**다. 이 수는 이전 M4a의 assets 블록 프로그램 수와 집계 단위가 다르다. SHA-256: `701e4473c68c303258a5cc234baeb7cfc23358167cbf514b405800f038cc0a51`.
- `Sprites/Tile` 4변형, `ADOFAI/FloorMesh` 2변형, `Sprites/Default`/`Sprites/Mask`/`ADOFAI/Frames` 각 4변형, HoldShader·TiledSpriteWithGap·Legacy Additive, Grab/Overlay 계열을 포함한다. GrabPass는 직전 색 타깃을 복사하고 원래 PS에 공급한다. named Grab/UsePass만 있고 실행 프로그램이 없는 Framebuffer 계열 4개는 명시적인 fallback 목록으로 남긴다. instancing·soft particles·fog의 미지원 변형을 임의의 기본 변형으로 대신하지 않는다.

[DirectFull.Shaders.cs](../../DirectFull.Shaders.cs)가 검증 리소스를 읽고 재질/글로벌 키워드와 일치하는 패스 변형을 선택한다. 매 프레임 JSON 파싱·추출·D3DCompile은 없다. 게임에서는 원래 DXBC를 `CreateVertexShader`/`CreatePixelShader`에 그대로 넣는다. fixture의 참조 HLSL은 게임 렌더러에서 사용하지 않는다.

상수는 재질별 MPB가 비어 있지 않으면 그 블록을 선택하고, 그렇지 않으면 renderer MPB를 선택한다. 선택한 블록의 속성 → 재질 값/셰이더 기본값 → 해당 글로벌 값 순으로 채운다. 재질 블록과 renderer 블록의 속성을 임의로 합치지 않는다. float로 보관된 기존 정수 속성과 `SetInteger`의 실제 정수 보관을 구분하며, 실제 정수는 float를 경유하지 않고 cbuffer에 쓴다. `_MainTex_ST`·texel size, 스프라이트 텍스처/색/flip/외부 alpha, object/world/view/projection/VP 등도 별도로 공급한다. 행렬은 Unity의 열 순서와 바이트코드의 열 접근에 맞춘다.

패스 상태 29항목을 리소스에 보관해 blend RGB/alpha, 연산, color write mask, cull, depth clip/test/write, offset, alpha-to-coverage, stencil reference/read/write 및 앞/뒤 연산을 만든다. D3D11 역 Z에 맞게 depth 비교를 바꾼다. **현재 실게임 진입은 mono orthographic D3D11·Gamma·비 MSAA camRT·투명 큐로 제한한다.** Linear 색 전송, opaque 단계, 외부 MPB constant-buffer lease 등을 검증한 것처럼 켜지 않는다.

참고한 API 규칙: [Unity Renderer.SetPropertyBlock](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/renderer/setpropertyblock), [MPB.GetInteger](https://docs.unity3d.com/cn/current/ScriptReference/MaterialPropertyBlock.GetInteger.html), [Material.GetInt의 float 별칭 규칙](https://docs.unity3d.com/cn/current/ScriptReference/Material.GetInt.html). 색 공간을 다시 변환해 중복 적용하지 않도록 Gamma에서 먼저 검증하는 경계도 두었다.

### M4c — 전체 목록·네이티브 그리기·원복

주요 구현은 [DirectFull.Render.cs](../../DirectFull.Render.cs), [DirectFull.Native.cs](../../DirectFull.Native.cs), [drfull.cpp](../../native/sfnative/drfull.cpp), [drfull.h](../../native/sfnative/drfull.h)다.

- `drfull on`이 **현재 준비된 장면에서 즉시 활성화**한다. 원래 셰이더를 만들고 geometry/constant/texture/state를 준비한 뒤 메인 카메라 마스크를 lease로 줄인다. 기본 꺼짐이고 켜진 상태를 설정에 저장하지 않는다.
- MeshRenderer는 `GetNativeVertexBufferPtr`/`GetNativeIndexBufferPtr`, 실제 vertex attributes/stride/offset, index format, submesh `indexStart/indexCount/baseVertex`를 사용한다. 여러 stream을 지원하고 signature에 필요한 속성이 없으면 거부한다. Sprite는 `vertices/uv/triangles`로 캐시한 정적 버퍼를 만든다. 색은 별도 정점 입력 또는 원래 `_RendererColor`, flip은 해당 원래 상수 또는 geometry에 반영한다.
- TransformAccessArray의 read-only 작업이 부모를 포함한 최종 world/inverse 행렬과 정렬 거리를 읽는다. 변경된 행렬/거리만 NativeQueue로 받는다. Unity의 `Transform.hasChanged`를 지우지 않는다. 일반적인 XY 이동만으로 전체 순서 목록을 매 프레임 다시 만들지 않는다.
- 기존 `DirectSequenceChanges`의 관리 코드 쓰기 지점 관찰을 연결했다. renderer/material/MPB/키워드/mesh/일부 texture 상태 변화와 enabled/active 수명을 통지한다. native/internal-call setter 자체에 Harmony를 붙이지 않는다. 장식의 논리 갱신·Transform 쓰기·게임 스크립트는 계속 실행한다.
- 순서는 sorting layer→order→queue→distance→shader→material→renderer/slot로 잡고, 동일 renderer의 패스 순서도 보존한다. 변경 때만 목록/명령 버퍼를 재구성한다. native는 8개 패킷 큐로 변경 상수와 pose를 복사받고, 그리기 때 보수적으로 clip-volume 밖의 bounds를 제외한다.
- 원래 `Sprites/Mask` DXBC로 cutoff를 그리고 범위 시작/끝 stencil 증감과 inside/outside 비교를 수행한다. 잘못된 범위·포화·지원하지 않는 depth/stencil 타깃은 거부한다. Unity에 남긴 구간을 마스크가 가로지르면 M4d 필요 사유로 거부한다.
- particle/skinned/text 및 지원 못 하는 단일 패스 renderer는 원래 Unity `DrawRenderer`를 **정렬된 같은 자리**에 넣을 수 있다. 이는 네이티브 소유 그리기 수와 별도의 `unityExplicit` 수로 보고한다. 마스크가 있는 미지원 fallback, 모르는 다중 패스는 거부한다.
- Canvas와 SortingGroup이 있는 층은 Unity 메인 카메라에 남긴다. 잔여 renderer/Canvas/그룹의 정렬 구간을 보수적으로 예약하고, 그 앞/뒤를 BeforeForwardAlpha/AfterForwardAlpha로 나눈다. native 목록이 잔여 구간 안에 끼거나 마스크가 가로지르면 **이유를 로그에 쓰고 켜지 않는다**. 충돌 GameObject의 layer를 옮기지 않는다. 이 경계 때문에 모든 맵/시각에 `on`이 허용된다고 보장하지 않는다.
- 카메라 이벤트에서 현재 camRT의 RTV/DSV/viewport를 사용한다. deferred context의 명령 목록을 실행한 뒤 `ExecuteCommandList(..., TRUE)`로 Unity의 즉시 컨텍스트 상태를 복구한다. 이전 token의 이벤트는 off 뒤에 도착해도 무효다.
- off·맵/장면 변경·재시작·편집 복귀·Unload는 공유 lease의 원래 카메라 마스크로 복원하고, 명령 버퍼·관찰 후크·job 배열·native COM 참조·추가 수명 컴포넌트를 해제한다. 동적 생성/삭제·재부모화로 목록 계약이 깨지면 복원하고 재활성화가 필요하다고 로그에 남긴다. 렌더 오류/큐 포화/타깃 교체/경합 모드도 자동 복원한다.

`state`에는 `mode=M4c-original`, `native`, `unityExplicit`, 소스 수, 제거/유지 마스크, 완료 프레임/그리기, 오류/포화, `main_us`, `main_max_us`, `render_us`, `commit_us`, `replans`가 나온다. `assert on`은 실제 완료된 native 그리기가 없거나 카메라/제출이 오래 정체되거나 오류/포화가 있으면 실패한다. 단순히 켜짐 플래그만 검사하지 않는다. 시간은 각 경로의 wall time이며 GPU busy나 Present 대기를 뜻하지 않는다.

### 오프라인 WARP 대조 및 검증 결과

증거 폴더: [out/direct-renderer/m4bc](../../out/direct-renderer/m4bc).

| 검사 | 결과 |
|---|---|
| DEV / Player / PlayerAuto / native | 모두 빌드 오류 0. 관리 빌드 경고는 기존 항목 포함 84 / 63 / 63개 |
| native C++ | `/W4 /WX /fp:strict` 통과 |
| 기존 native export | 이름·ordinal·hint **73/73 보존**, `sf_z_drfull_v1 @74` 한 개를 이름순 끝에 추가 |
| 관리 DLL의 내장 native | 작업 폴더 최종 native와 **3/3 일치** |
| 원래 shader 리소스 경계 | DEV/PlayerAuto **2/2 내장**, 일반 Player에는 DirectFull 코드/리소스 제외 |
| 생산 리소스 로더 계약 검사 | 21 shader / 1,012 variant / 2,024 stage / 6,594 변수 / 2,407 texture 참조의 해시·CB·패스·키워드 왕복 일치 |
| C#↔native ABI | Create 16 / Program 144 / Geometry 200 / Draw 104 / Frame 408 / Stats 72 / Pose 128바이트 일치 |
| 공유 mask lease fixture | 25종·임의 마스크 10,000개 통과, 안정 CheckLease+PreCull 100,000회 관리 할당 0바이트 |
| WARP 원래 DXBC ↔ 독립 식 | **48/48 통과**, 전체 1,179,648픽셀 중 다른 픽셀 합계 **28**, 최대 채널 차이 **1/255**, 전부 FloorMesh의 계산/양자화 차이 |
| WARP Tile | 32/32 사례에서 모든 채널 차이 0 |
| WARP 비어 있지 않은 출력 | 각 사례 원래 그리기 완료, 배경 외 픽셀 3,668~8,510개 확인 |
| WARP 추가 대조 5종 | 행렬 전치 오류 검출, 즉시 컨텍스트 shader 상태 보존, 변경 상수 적용/정확 복원, clip-volume 밖 native culling, unified stencil의 불일치 거부/일치 허용 |
| 설치 DLL 관찰 | 앞선 검사 2/2 일치 → 중간 관찰 HOLD 0/2 → 마지막 snapshot HOLD 1/2. 이 세션의 설치/교체/복원 명령 없음 |

WARP 도구는 [drfull-warp.cpp](../../tools/direct-renderer/drfull-warp.cpp)가 **생산 `drfull.cpp`**를 함께 빌드해 실행한다. [make-warp-cases.py](../../tools/direct-renderer/make-warp-cases.py)가 원래 두 셰이더의 검증된 슬롯/오프셋으로 입력을 만들고, [drfull-reference.hlsl](../../tools/direct-renderer/drfull-reference.hlsl)은 별도의 이름 있는 상수 배치로 우리 계산식을 수행한다. Tile 4변형/FloorMesh 2변형 × 4상태 × point/bilinear sampler = 48사례다. 비대칭 world/VP, PixelSnap, 반복/point sampling, premultiplied alpha, FloorMesh의 shadow/inset/perlin/icon/flip/sprite/active 분기, 외부 mesh VB·다중 stream·baseVertex/indexStart, sprite 색 입력 및 중첩 그리기를 포함한다.

[warp-results.tsv](../../out/direct-renderer/m4bc/warp/warp-results.tsv), [warp-summary.json](../../out/direct-renderer/m4bc/warp/warp-summary.json), [contract.log](../../out/direct-renderer/m4bc/contract.log), [lifecycle.log](../../out/direct-renderer/m4bc/lifecycle.log), [exports-result.txt](../../out/direct-renderer/m4bc/exports-result.txt), [artifacts.json](../../out/direct-renderer/m4bc/artifacts.json)에 수치/해시가 있다. 설치본 관찰은 [installed-before.json](../../out/direct-renderer/m4bc/installed-before.json)과 [installed-after.json](../../out/direct-renderer/m4bc/installed-after.json)이다. 검증 스크립트의 PowerShell JSON 배열 래핑 때문에 동일 해시를 HOLD로 오인하던 검사도 고쳤다. 그 수정 뒤 앞선 검사들은 2/2 일치했으나, 12:51의 최초 snapshot과 13:27 이후 관찰 사이에는 실제 설치 해시가 달라졌다. 변경 주체를 추정하지 않고 보존한다.

| 설치 파일 | 12:51 최초 관찰 SHA-256 | 13:27 중간 관찰 SHA-256 |
|---|---|---|
| StutterFix.dll | `B0EFE68D6CCCE3217310DA0AD65CBA4048C19D2D25F738C97F14D7822898C795` | `AD6A3F2DDD6DCE1A3B1904513B313EB08E30490D0227DBFF27B88A3216D3EB7D` |
| sfnative.dll | `613450E651FDC1D442E5AA59F7912566C913E3EFB4DCEB95F93EB856ABD07E43` | `E7A7CF6026892C3082224C2CB2C45D3DC8B2D35F9045DEBE81E1241590ACE05A` |

그 뒤 마지막 artifact 검사의 `installed-after.json`에는 managed가 최초의 `B0EFE68D...`로 다시 관찰됐고 native는 `E7A7CF60...`라 1/2 일치다. 위 표는 중간 관찰 기록이며 현재 설치 상태를 고정해서 보장하는 표가 아니다. 서로 다른 시점의 설치본을 작업 폴더의 새 M4c 산출물로 혼동하지 않는다.

WARP fixture의 허용 상한은 채널 2/255이며 실제 최대는 1/255다. 실게임 픽셀판은 아래처럼 차이 0을 요구한다.

**WARP 대조는 Unity 캡처와의 대조가 아니다.** 이 결과로 실게임 픽셀 일치·전체 곡 안전성·충돌 보존·FPS 이득·메인 0.3ms/렌더 1ms 달성을 주장하지 않는다. 원래 셰이더를 사용해도 SpriteRenderer의 실제 입력, 같은 재질 완전 동점, 실제 stencil/필터/잔여 구간은 Unity에서 픽셀 검증해야 한다. 원래 shader의 재구성 식조차 FloorMesh에서 28픽셀의 1단계 차이가 있으므로 이것을 “오프라인 완전 픽셀 일치”라고 부르지 않는다.

안정 상태에서는 알림이 없는 상수를 전수 검사하지 않으며 pose는 job으로 갱신한다. **Full 경로 전체의 실제 GC 0은 미측정**이다. geometry/variant/재질 구조가 바뀔 때의 재바인딩에는 배열/리소스 생성이 있고, 구조 변경이 계속되는 맵에서는 비용/누적 descriptor 한도를 확인해야 한다. 일반 이미지/타일의 색·행렬 변경과 구조 변경을 같은 검증으로 취급하지 않는다. 외부 모드/엔진 안에서 관찰하지 못한 상태 변경, Linear/MSAA/opaque·보조 합성은 추가 검증 범위다.

### 재현 명령 — 게임 실행 없음

```powershell
py -3.12 tools/direct-renderer/export-original-shaders.py
py -3.12 tools/direct-renderer/make-warp-cases.py
powershell -ExecutionPolicy Bypass -File tools/direct-renderer/check-m4.ps1 -OutputDirectory out/direct-renderer/m4bc
py -3.12 tools/direct-renderer/plan-m4c.py --maps hl50,wf,dd,sh --rt 3440x1440
```

추출기는 `--deps`, `--game-data`, `--output`, `--resource`를 받을 수 있다. 기본 deps 경로는 이전에 설치해 둔 `E:/StutterFixWork/wt-v14prod/out/asset-reader-deps`를 읽기만 한다. 빌드기는 라이브 게임 프로세스/사용자 플레이 flag가 있으면 시작을 거부하며 설치본을 바꾸지 않는다. native 중간 산출물은 이 작업 폴더의 `out/direct-renderer/m4bc/native-build`로 분리했다.

### Claude 실게임 AUTOTEST — 픽셀 먼저, 이어서 ABBA

새 JSON은 [plan-m4c.py](../../tools/direct-renderer/plan-m4c.py)로 만들었다. [plans/manifest.json](../../out/direct-renderer/m4bc/plans/manifest.json)과 `m4c-{hl50,wf,dd,sh}-{pixel,A1,B1,B2,A2}.json` **20개**다. `tag/steps/settings/timeout_min` 형식이 M4a와 같다. 생성만 했고 실행하지 않았다. 실제 게임에서는 새 managed/native 빌드 쌍이 로드된 새 프로세스를 사용해야 한다.

HELLO 50초 픽셀판의 핵심 단계는 다음과 같다. JSON 앞부분은 `auto on`, fpscap 0, 지정 RT, 맵 open, GpuDeco=0/RunLod=false, dr2/off, seek 50/8 및 waitsongpos 50/400으로 구성되어 있다.

```text
framecmp ui include
framecmp mask none
pause
wait 1
drfull on
wait 2
drfull assert on
drfull state
framecmp grab d1
drfull assert on
drfull off
drfull assert off
wait 1
framecmp grab d0
framecmp diff d0 d1 0 png
framecmp save d1
framecmp save d0
framecmp grab d00
framecmp diff d0 d00 0
framecmp assertzero d0 d00
framecmp assertzero d0 d1 png
resume
drfull assert off
```

요청한 drPix 순서 **pause → on의 d1 → off의 d0 → diff**다. `ui include`는 FrameCompare의 별도 카메라 mask lease가 M4의 mask를 덮지 않게 한다. UI를 포함하므로 off/off `d0↔d00` 잡음도 검사한다. 픽셀판에는 중심 제외 마스크와 허용 threshold가 없다. **off/off 잡음 0, d0/d1 차이 0, d1 앞뒤 assert on 통과**가 전제다. 실패/자동 off/경계 거부를 d0/d0의 거짓 성공으로 취급하지 않는다.

bench는 각 맵에서 **A1(Unity) → B1(drfull on) → B2(drfull on) → A2(Unity)**, 실제 fpsmark 구간 **12초**다. B의 mid는 `drfull on; wait 2; assert on; state; cpuwindow begin`, post는 `cpuwindow end; assert on; state; off; assert off; state`다. A는 같은 위치에서 off/assert off를 쓴다. activation/첫 job 준비/검증은 측정 구간 밖이다. `ppGpuCam`의 동일 설정으로 phasewindow·cpuwindow·presentpath GPU camera 수치를 함께 남긴다. `state` 평균은 enable 이후 전체이며 ABBA FPS에는 fpsmark 구간을 쓴다.

무효 조건: 픽셀 gate 실패, `on` 거부, 중도 FULL OFF/sample invalid, 오류/포화/정체, native 완료 그리기 증가 없음, 마스크 복원 불일치, 동시 빌드/다른 GPU 부하. `native`와 `unityExplicit`, 제거/유지 층 및 예약 구간도 함께 보고해 맡은 범위가 줄어든 수치를 전체 native 성능으로 포장하지 않는다. M4d 경계로 거부된 시각은 실패 사유와 census를 보존하고, 성공한 M4c 벤치로 집계하지 않는다.

### 남은 실게임 확인

이번 인계의 완료 범위는 **M4b 로더 + M4c 시험 가능한 on/off/검증 경로 + WARP fixture + 4종 빌드**다. Claude가 위 픽셀판과 ABBA, 재시작/다른 맵/편집 복귀/Unload 복원, 타일 충돌·UI·필터·마스크를 실제 게임에서 확인해야 한다. M4d 보조 합성, Unity와의 완전 동점 규칙 확정, 모든 맵의 admission, 실게임 성능 목표는 완료로 표시하지 않는다.
