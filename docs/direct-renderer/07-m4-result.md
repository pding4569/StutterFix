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
