1. 결론: 소유 비율은 성능 지표가 아니다. Exit의 원래 CPU 경로는 휴면·동일값 생략을 사용하므로 Arche의 절감률을 그대로 옮길 수 없다.
2. 가장 먼저 재생 중 그리기/복사/렌더러 등록을 각각 제거해 비용을 나눈다. 약 0.6ms는 측정된 단일 비용이 아니라 반사실 가정의 잔차다.
3. 코드상 우선 후보는 22개 대체 항목 때문에 실행하는 전체 집합 재평가와, 242개 대표 렌더러 및 텍스처 경로의 고정비다. 기여 시간은 미측정이다.
4. 개선 방향은 이득인 묶음만 소유하고, 대체 항목만 평가하며, 카메라·채널이 변하지 않은 항목을 재사용하는 것이다. 정점 셰이더 평가는 그 다음 비교 후보다.
5. 이번 작업은 코드·기존 로그 조사와 미실행 측정 JSON 작성까지다. 게임·빌드·설치·푸시를 하지 않았으며 G1 픽셀 불합격은 계속 유효하다.

2026-11-03, `claude/gpudeco-partial`. 프로토타입 없음, 런타임/제품 기본값 변경 없음.

## 근거와 수치의 경계

요청 파일: `E:\StutterFixWork\codex-cheap-prompt.md`. 기존 결과 [g1.md](g1.md), 구조 [g1-code-structure.md](g1-code-structure.md), [동작 명세](../00-deco-spec.md)를 먼저 확인했다. 기존 G1의 Arche 262초 통합 그리기는 최대 채널 차이 4로 허용치 3을 넘었다. 아래는 개선 채택이나 성능 합격 보고가 아니다.

| 구분 | 값과 의미 |
|---|---|
| 지시서의 기존 실측 | Exit 원래 경로 386/382 FPS, G1 364/366 FPS, 장식 제거 535 FPS. 7777 370→342 FPS, Arche 무거운 구간 103→299 FPS. 서로 다른 맵/창의 수치를 같은 비용으로 합치지 않는다. |
| 위 값으로 계산 | 원래 경로 평균 프레임 시간 `(1000/386+1000/382)/2=2.60424ms`, G1 `2.73975ms`, 순증 `0.13551ms`. 장식 제거 `1.86916ms`, 차이 `0.73508ms`. 반올림 FPS의 역수이며 원시 프레임 분포의 평균을 재계산한 값은 아니다. |
| 가정에서 나온 잔차 | 절감 기대를 0.5ms로 놓을 때 `0.13551+0.5=0.63551ms`. 실제로 그만큼 절감 가능한 작업이 Exit에도 있었다는 가정이 필요하다. 따라서 “숨은 0.6ms” 전부가 신규 오버헤드라는 결론은 아직 성립하지 않는다. |
| 직접 확인한 로그 | `night-results/g1ab-sh-1-partial.log:1411`: spans=242, included=2977, arrays=3, bytes=646709248, copy_calls=187, texture_fallback_items=22, texture_fallback_dispatch=True. 646.7MB(십진)/616.75MiB다. |
| 직접 확인한 CPU 계측 | 같은 로그:1680, 89,157프레임. total=0.086435ms, validate=0.057507ms, dispatch=0.014010ms, whole_frame=0.093487ms. total은 `DispatchGpuLiveSlice` 범위이고 whole_frame에는 `CheckPlayerFrame`·clock이 더 들어간다. validate는 uniform과 중첩되고 fb_eval/copy는 dispatch 안에 있으므로 합산 금지. |
| 직접 확인한 동적 변화 | 같은 로그:1649–1652에 동적 장벽 때문에 독립 렌더러로 전환한 구간이 누적 18개까지 보인다. 준비 시 22개 대체 항목·187개 복사 호출이 곡 내내 고정이라는 가정은 틀릴 수 있다. |
| 비교 시 주의 | g1.md의 Exit 소유 2,989개와 지시서 ABBA의 2,999개는 다른 실행이다. 이번 기준은 해당 로그/빌드에 귀속한다. 로그의 비정상적으로 큰 효과 시간/음수 나머지는 비용 근거에서 제외한다. |

CPU 계측에는 엔진 API가 호출 스레드를 기다리게 한 시간이 포함될 수 있지만, 비동기 GPU 실행·렌더 스레드 처리 전체를 재지는 않는다. 0.086ms만으로 GPU/드라이버를 범인으로 확정할 수 없다. 준비 로그의 복사 수는 실행 프레임당 native copy 횟수가 아니다. 배열 바이트도 실제 추가 상주 VRAM/페이지 폴트/대역폭의 측정이 아니다.

## 원래 경로와 새 경로의 손익

게임 원본 `decompiled/Assembly-CSharp/scrDecorationManager.cs:LateUpdate`는 모든 장식의 `LogicUpdate`를 호출한다. `scrDecoration.cs:LogicUpdate`는 `GetVisible()`일 때만 `UpdatePosition`을 부른다. 그러나 비교 대상은 이 원본을 그대로 실행하는 게임이 아니라 StutterFix가 켜진 원래 경로다. `MoveApply.cs:LogicMaybe`와 `Dormancy.cs`의 휴면 목록, `TightLate.cs:Apply`의 입력/변환 캐시가 이미 일을 줄인다. 움직이지 않아도 시차·잠금·카메라 입력이 바뀌면 위치 비용이 생기므로 “정적 이벤트=항상 공짜”도 아니다.

GPU 소유 시 `GpuDecoExport.cs:SkipGpuLate`와 `TightLate.cs:List/Process`가 CPU 위치 처리를 생략하고, `FastMove`의 GPU 분기가 원래 트윈을 대체한다. 대신 `DispatchGpuLiveSlice`가 source 진행·dirty 업로드·카메라·검증·통합 평가를 한다. `GpuDecoDrawCollapseRenderer.cs:DispatchDrawCollapse`는 대체 항목이 남으면 `source.Gpu.DispatchEvaluate()`를 추가한다. `GpuDecoGpu.cs:DispatchEvaluate`의 dispatch 크기는 대체 22개가 아니라 `data.Items.Length` 전체다. 복사 청크 마스크는 이 평가 범위를 줄이지 않는다. 다만 알파 0 항목은 셰이더가 먼저 탈출한다.

Arche의 대량 동시 트윈/위치 갱신을 제거하는 이득과 Exit의 휴면 경로를 제거하는 이득은 다를 수 있다. Exit의 태그별 활성 트윈·카메라 의존·가시 면적 분포, 7777의 실제 구간/텍스처 분포는 이번에 전수 분석하지 않았으므로 **미확인**이다. 작은 소유 집합에서도 전체 준비·검증·대표 렌더러 비용을 지불할 수 있다는 설명은 코드와 양립하지만, 맵별 손실 원인의 실측 증명은 아니다.

## 가설과 구현 후보

아래 예상 이득은 모두 추정식/구조적 상한이다. ms 보장은 없다. 구현 크기는 작은 실험 S(수 파일), 중간 M(소유 계획/커널 수정), 큰 L(렌더 경로/안전 계약 재설계)이다.

| 내용 | 코드 근거 (`파일:함수`) | 예상 이득/판별식 | 깨질 수 있는 동작 | 크기 |
|---|---|---|---|---|
| 대체 항목 전용 compact 평가 | `GpuDecoDrawCollapseRenderer.cs:DispatchDrawCollapse/ReorderCopyForFallback`; `GpuDecoGpu.cs:DispatchEvaluate`; `GpuDeco.compute:EvaluateLive` | `T_eval(N)-T_eval(F)`; 초기 N=2999, F=22라 호출 슬롯은 약 136배 차이. GPU 시간 136배 절감이라는 뜻은 아니다. 동적 제외도 F에 포함. | master/source/정점 인덱스, 이미지 교체, 복사 청크 재배치, CPU 복원 | M, 최우선 코드 후보 |
| 대체 장식만 CPU에 남겨 두 번째 평가 자체 제거 | `GpuDecoPerItemPrepare.cs`; `GpuDecoDrawCollapseRenderer.cs:ExcludeCpuFallbackFromCollapse/DispatchDrawCollapse`; `GpuDecoExport.cs:PrepareGpuLive` | `T_extra_eval+T_copy - T_CPU(F) - T_new_barriers`; 22개를 되돌려도 더 비싸지면 채택하지 않음 | CPU 장식이 새 장벽이 되어 구간 증가, 이벤트 대상 순서·혼합 소유·복원 | M |
| 최대 소유 대신 이득인 연속 묶음만 소유 | `GpuDecoExport.cs:PrepareGpuLive/VisibleInWindow`; `DrawCollapsePolicy.cs:Plan`; `GpuDecoPlayerPolicy.cs:MinimumOwned`; `Dormancy.cs` | 묶음별 `B=절약한 CPU 트윈·위치·렌더러 비용 - 평가·업로드·대표·배열·검증 증분`. `B>여유값`만 허용. 절대 개수/비율 기준만으로 결정 금지 | 제외한 정적 항목이 장벽이 되어 조각 증가, 시차/잠금 카메라 변화, hidden→visible | M/L |
| 곡 전체 또는 긴 구간에서 GPU를 아예 사용하지 않음 | `GpuDecoPlayer.cs:Prepared/BeforeControllerUpdate`; `GpuDecoPlayerPolicy.cs`; `DrawCollapsePolicy.cs:Plan/SpanDecision` | Exit 기존 순증 약 0.136ms 회피 가능성. 전체 소유 이득이 음수면 GPU 경로를 만들지 않는 것이 최선일 수 있음. 새 기준판으로 확인 필요 | 준비 후 취소/재시도, 과도한 전환 정지. 초기에는 곡 전체 고정 결정, 프레임별 ownership 진동 금지 | S/M |
| 전역 계산은 유지, Unity Renderer 없는 순서 보존 제출 | `GpuDecoDrawCollapseRenderer.cs:BuildDrawCollapseSpans/DispatchDrawCollapse`; `DrawCollapsePolicy.cs:Plan`; `GpuDecoBatch.shader:vert` | `T_engine_renderer(242)-T_explicit_submit(242)`; 컬링/정렬 객체는 줄어도 242 draw/재질 경계는 남을 수 있음 | 카메라별 층/정렬/투명 블렌드/필터 실행 시점. 하나의 카메라 버퍼 끝에 몰아 그리면 장벽 앞뒤 순서 파괴 | L |
| 안전한 인접 구간만 병합 | `DrawCollapsePolicy.cs:Plan/SameSortKey`; `GpuDecoDrawCollapseRenderer.cs:DynamicOrderTick` | `Δ구간수 × 제출 고정비`에서 더 넓은 bounds·overdraw를 뺌. 장벽을 넘는 병합은 금지 | 깊이 변경, SortingLayer/order/queue, 알파 블렌딩 순서 | M/L |
| 정점 셰이더에서 live 채널/pose 평가 | `GpuDeco.compute:EvaluateCollapse/WriteVerticesAt`; `GpuDecoBatch.shader:vert`; `GpuDecoGpu.cs:UploadSourceE` | 통합용 compute/UAV 쓰기·읽기 의존성 제거. 현재도 global compute 1회이므로 “242 compute 제거”가 아님. 채널 계산이 정점 4개/여러 카메라에서 반복되어 오히려 늘 수 있음 | precise double→float 산술, 색 UNorm8, 이미지 행렬, 버퍼 인덱스, CPU readback 진단 | L |
| 정점 셰이더화의 완전성 조건 | `GpuDecoDrawCollapseRenderer.cs:DispatchDrawCollapse`; `GpuDecoCopy.cs`; `GpuDecoBatch.shader:vert` | 통합만 VS로 바꾸면 **187 복사 경로와 대체 전체 dispatch는 남는다**. 독립 대체 렌더러도 같은 evaluator 사용 또는 CPU 환원해야 제거 가능 | 대체 텍스처 바인딩 상한 48, 정렬/재질 경계, fallback복귀 | L |
| 변하지 않은 항목/구간 재사용 | `GpuDecoExport.cs:AdvanceGpuLiveSources/FlushDirty`; `GpuDecoGpu.cs:UploadSourceE/DispatchCollapseMinimal`; `GpuDeco.compute:EvaluateCollapse` | `T_eval(N)-T_eval(dirty∪active∪camera-dependent)`; 이미 알파 early-out가 있어 단순 hidden 제거의 추가 이득은 제한 가능 | source 완료/중단/Kill, 이미지·visible·alpha·map 변화, 카메라 이동·lockScale/Rotation·시차, seek/재시작 | M/L |
| source 업로드 범위/버퍼 순환 개선 | `GpuDecoExport.cs:DispatchGpuLiveSlice/AllocateGpuLiveSource`; `GpuDecoGpu.cs:UploadSourceE/UploadDirtyLiveChannels` | 매 프레임 sourceHighWater×4B, 채널 dirty range×16B. 호출 감소와 재사용 버퍼 hazard 감소 가능. 바이트가 작다고 동기화 비용 0은 아님 | 버퍼 수명/이벤트 직후 값, GPU가 읽는 버퍼 덮어쓰기 | M, costprobe로 우선 확인 |
| 텍스처 배열 대신 원본 빌려 그리기/작은 재질 묶음 | `GpuDecoDrawCollapseRenderer.cs:PrepareDrawCollapse/BuildDrawCollapseSpans`; `DrawCollapsePolicy.cs:AdmitArrayGroups/TextureBytes`; `GpuDecoBatch.shader:frag` | 약 616.75MiB 배열 중 제거분; 시간은 `상주/캐시 이득 - 증가한 SetPass·draw`. 원본과 배열이 모두 살아 있는 중복도 고려 | 패딩 UV/필터/압축/순서, direct 48개 제한, 텍스처별 분할 증가 | M/L |
| owned 오브젝트의 남은 작업·전수 검증 구분 | `TightLate.cs:ManagerEnd/List`; `GpuDecoPlayer.cs:CheckPlayerFrame`; `GpuDecoDrawCollapseRenderer.cs:CheckDrawCollapseBeforeDispatch/ValidateDrawCollapse` | validate 0.0575ms를 전부 없애도 잔차 0.6355ms의 약 9%에 불과. 원래 렌더러 enabled=false이나 오브젝트/다른 컴포넌트는 존속 | 감시 제거 시 외부 변경/장벽 활성화/순서 역전 누락. guard off를 제품 최적화로 쓰지 않음 | S 계측, M 안전한 증분화 |

일부 함수는 partial 클래스의 다른 파일에 걸쳐 있다. 대표 메시·재질·렌더러 생성 경로는 `GpuDecoDrawCollapseRenderer.cs:BuildDrawCollapseSpans`다.

## 측정 순서 — 사용자 복귀 후에만

모든 JSON은 **미실행**, 현재 게임 실행 금지와 `user_playing.flag` 보존 지시가 우선이다. 이 문서는 실행 예약이 아니다. `run_locked.py`를 이번 작업에서 호출하지 않는다. 이후에도 사용자 게임 중에는 빌드/분석/게임을 시작하지 않는다.

### 준비·빌드 대상

- 동일 소스의 Release **PlayerAuto**: `dotnet build StutterFix.csproj -c Release -p:Edition=Player -p:AutoTestBuild=1 --nologo` → `bin/PlayerAuto/StutterFix.dll`. `phasewindow`, ablation, fpsmark가 AUTOTEST 전용이라 일반 Player DLL로는 안 된다. 기존 아티팩트가 동일 소스임을 해시/커밋으로 확인할 수 있으면 재빌드하지 않는다.
- GPU 타이밍에는 별도 `tools/gpuprobe/build.ps1`의 `gpuprobe.dll`이 필요하다. [도구 설명](../../../tools/gpuprobe/README.md)을 따른다. DLL을 제공하는 측정 환경은 이후 담당자가 준비하며 이번에는 Mods를 건드리지 않았다. 누락 시 gpuprobe 결과는 미확인으로 남긴다.
- 현재 내장 GpuDeco compute/배치 번들과 native copy를 그대로 사용한다. 셰이더/Unity 빌드는 필요 없다. 프로토타입을 만들 때만 `claude/gpudeco-cheap`에서 기본 꺼짐 옵션으로 개발하고, 셰이더 변경 시 managed/native/bundle 해시를 모두 다시 기록한다.
- 각 프로세스의 settings 외 모든 설정·해상도 2560×1440·VSync 0·FPS 제한·필터·카메라·다른 모드·map hash를 고정한다. `fpscap 0`은 VSync 0/target 10000을 명시한다. FrameGenOutside=0, FrameGenRefresh=false를 JSON에 고정했다. 재생 도중 빌드/캡처 분석 금지.
- 아래 JSON의 절대 맵 경로는 기존 Exit 측정 명세에서 가져왔다. 파일 존재를 정적 검사했다. settings 적용/실제 준비 완료·GPU 소유 수·전체 복귀 0은 실행 로그로 반드시 확인한다. gpuprobe는 맵을 연 뒤 **press 전에** 설치하고 준비 시간을 창에서 뺀다.

### 1. 가장 먼저: 신규 경로의 비용 분리 (12판)

`cheap-steps/01-01-*.json`부터 `01-12-*.json`까지 manifest 순서로 **각각 새 프로세스**에서 실행한다. 순서는 `N K D DK R RK RK R DK D K N`, 조건당 2판이다. 동일 소유 상태를 유지한 90–100초 창을 `phasewindow`, `gpuprobe`, `fpsmark`, `gpudeco cost`로 함께 본다. 88초에 조건을 적용해 2초를 버리고, 100초 이후 복구·종료한다. 해당 창은 기존 로그에서 Exit가 낮은 FPS를 보이는 구간이며 전체 곡 판정을 대신하지 않는다.

| 조건 | 명령 | 남는 것 / 제거되는 것 |
|---|---|---|
| N | `gpudeco ablate off`, hideproxy off | 정상 GPU 경로 |
| K | `gpudeco ablate copy` | source/평가/그리기 유지, **독립 목적지 복사만** 제거. 통합 UAV 쓰기는 남음. 정점이 낡아짐 |
| D | `gpudeco ablate draw` + `gpudeco drawcollapse hideproxy on` | 독립 forceRenderingOff와 통합 대표 숨김을 함께 적용. compute/업로드/복사 유지 |
| DK | `gpudeco ablate both` + hideproxy on | D에 독립 목적지 복사 제거 추가 |
| R | `gpudeco ablate root` | 교체 root 비활성. 대표+독립 렌더러의 활성 계층 제거, compute/업로드/복사 유지 |
| RK | `gpudeco ablate rootcopy` | R에 독립 목적지 복사 제거 추가 |

**범위 함정:** `ablate draw` 단독은 `slice.Renderers`의 독립 렌더러만 숨긴다. 통합 대표는 별도 `gpuCollapseSpans`에 있으므로 hideproxy도 필요하다. D는 대표의 enabled 변경까지 포함해 순수 GPU raster만 제거하는 시험이 아니다. R은 원래 소유 오브젝트를 끄지 않는다. R/D의 카메라 출력 변화로 뒤 필터 비용도 달라질 수 있다. 제거한 그림은 시각 합격 대조로 쓰지 않는다.

판독은 FPS 퍼센트보다 `1000/FPS` ms 차이를 먼저 쓴다. `N-K`는 복사 경로+그리기 stale 부작용, `D-DK`와 `R-RK`는 그림이 없는 상태의 독립 복사 기여, `DK-RK`는 교체 renderer 계층 등록/컬링 후보다. `N-D`는 그리기/일부 enabled 처리/후속 필터를 합친 효과다. 각 차이는 시스템 병목·동기화와 상호작용하므로 합쳐 0.6ms에 맞추지 않는다. gpuprobe whole-frame draw/SetPass도 함께 확인해 D/R에서 교체 draw가 실제로 감소했는지 검증한다.

모든 조건에서 `gpudeco live state`의 active/owned/대체·복귀 상태와 `gpudeco ablate state`의 오류·native 제출/실행을 기록한다. guard 복귀, 조건 무효, 타이밍 disjoint/busy 지속, 표본 없음, 창의 곡 시각 이탈이면 그 판은 무효다. 12판에서 차이가 반복되지 않으면 그 조건만 같은 순서로 2회 추가하고 성급히 원인을 정하지 않는다.

`gpudeco cost reset`은 관리 cost 구간만 초기화하며 costprobe는 초기화하지 않는다. costprobe는 재생 중 reset이 금지되어 JSON에 창 전후 report를 넣었다. 각 평균의 창 값은 `(종료평균×종료frames - 시작평균×시작frames)/(종료frames-시작frames)`로 구하고, 누적 작업 수는 차분한다. frames 차가 0이면 미측정이다. 관리 cost reset/phasewindow/fpsmark 명령 사이 몇 프레임 경계 오차와 probe reset 비동기 회수 지연도 기록한다. 엄밀히 동일한 프레임 구간의 직접 GPU 시간이라고 부르지 않는다.

### 2. 원래 경로와 동일 DLL 비교 (4판, 전체 곡)

`02-01-cpu.json`, `02-02-gpu.json`, `02-03-gpu.json`, `02-04-cpu.json`의 ABBA. settings의 GpuDeco만 0/1로 바꾼다. 90–100초 및 180–190초에 위와 같은 계측, 이후 `waitend 300`으로 전체 곡 요약을 남긴다. 종료 타임아웃은 완주가 아니므로 portal/종료 로그와 곡 프레임 수를 확인한다. 새 동일 DLL 기준으로 0.1355ms 순손실이 반복되는지, 원래 경로가 이미 휴면으로 싼지 확인한다. 준비/카운트다운/첫 활성화 프레임은 창 밖이다. CPU 모드의 gpudeco cost=0은 다른 CPU 장식 코드 전체 비용이 0이라는 의미가 아니다.

### 3. 정지 장면 compute 제거 (1판, 짧은 창 8개)

`03-frozen-compute.json`: 92초에 일시정지하고 정상/`gpudeco drawcollapse policy skipcompute on`을 N S S N N S S N으로 비교한다. `gpuprobe`, fpsmark만 사용한다. **phasewindow는 사용하지 않는다:** `PhaseWatch.Record`는 유효한 SongBucket에서만 songTicks를 쌓으므로 정지 창이 0으로 보일 수 있다. 소유/카메라가 그대로인지 확인하고, 정책 원복 뒤 RESTORED 화면을 비교한다.

`SkipPausedCompute`는 `DispatchDrawCollapse` 입구에서 반환해 통합과 대체 compute/복사를 함께 생략하지만 그 앞 `AdvanceGpuLiveSources/FlushDirty/UploadSourceE` 및 검증은 남긴다. 첫 span Renderer.enabled가 참일 때만 동작한다. 장면이 같지 않거나 해당 조건을 확인하지 못하면 효과 없음으로 결론내리지 말고 **무효/미확인**으로 표시한다. 이 시험은 재생 중 source 변화·변경률의 대체 증거가 아니다.

### 4. API/렌더 스레드 대기와 실제 GPU 구간 구별

gpuprobe의 GPU frame 범위는 첫 카메라 preCull→end-of-frame이다. `GpuLiveFrame`은 manager LateUpdate에서 먼저 제출하므로 compute 일부가 범위 **앞에** 실행될 수 있다. camera-sum/효과는 중첩되어 합산 금지. 따라서 S에서 FPS는 늘고 GPU camera 시간이 그대로여도 compute가 공짜라고 결론낼 수 없다.

1–3단계 뒤 귀속이 남으면 같은 N/RK 창을 ETW ThreadTime 및 GPU queue 타임라인으로 각 2판 추가한다. main/render 스레드 실행, 대기 스택, compute 제출→첫 카메라 전 GPU 작업, UAV→SRV/vertex 소비 지점을 본다. phasewindow는 경과 시간(대기 포함)이며 GPU/CPU 실행 시간을 가르는 도구가 아니다. 기존 명령만으로 compute GPU 시작/끝을 직접 재는 기능은 **미확인/미구현**이다. 필요하면 기본 꺼짐 GPU timestamp 계측을 추가하고 “추가 계측 필요”를 결과로 남긴다. source upload buffer ring/no-upload 대조도 현재 명령에는 없어 임의 명령을 JSON에 넣지 않았다.

### 5. 분리 결과에 따른 구현 순서와 검증 판 수

1. S가 크면 대체 compact kernel/CPU 환원부터 각각 비교. DK-RK가 크면 Renderer 없는 제출을 먼저 설계. D까지도 차이가 작고 CPU 기준이 이미 싸면 소유 축소/전체 비활성화 판단을 먼저 한다.
2. 선택 규칙은 준비 시 event/카메라 의존성과 묶음별 항목·장벽·배열 증분으로 보수적으로 예측한다. 숨김 장식도 나중에 보일 수 있으므로 현재 프레임 가시성만으로 소유권을 바꾸지 않는다. 실제 활성 source 수/가시 항목/대표 renderer 수/복사 실행 수를 같은 창에 기록할 추가 계측이 필요하다.
3. compact 평가 또는 reuse는 각각 하나씩 기본 꺼짐 후보로 만들고, CPU 복원/seek/이미지/visible/재시작 경계를 먼저 검사한다. VS 평가는 precise 산술·색 반올림·4정점/카메라 중복 비용을 별도로 비교한다.
4. 성능 측정 전에 G1의 픽셀 차단부터 해결한다. 기존 CPU 원본 독립 실행과 정상 GPU/후보/원복, noise 대조, 가시성 전환·이미지 변경·다시 하기 및 최소 3지점(Arche 262초 포함)을 검사한다. 허용차 최대 3 초과면 성능 개선 채택 중단. ablation 화면을 합격 증거로 쓰지 않는다.
5. 채택 후보는 Exit·7777·Arche·Windflower 각 전체곡 ABBA 4판(총 16판), Arche 무거운 구간도 별도 동일 창으로 보고한다. 전체 평균/최악 10초/긴 프레임/복귀/소유 수를 모두 비교한다. 이번 작성한 JSON은 비용 귀속용 Exit 17판까지이며 나머지 후보별 JSON은 구현/픽셀 통과 뒤 만든다.

## 결과물과 확인 범위

- [cheap-steps/manifest.json](cheap-steps/manifest.json): 실행 순서, 조건, 판 수. manifest 자체는 steps 실행 파일이 아니다.
- `cheap-steps/01-*.json` 12개, `02-*.json` 4개, `03-frozen-compute.json` 1개. 빌드가 필요한 새로운 진단 명령은 추가하지 않았다.
- 이번 확인: UTF-8/JSON 파싱, steps/timeout/경로·명령 대응의 정적 점검. 실게임 실행 결과·GPU 시간·픽셀 합격·개선율은 모두 미측정이다.
