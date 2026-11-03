1. 검증 전체 제거는 불가: `ValidateDrawCollapse(false)`에는 로그뿐 아니라 예외·GPU 복귀를 유발하는 안전 검사가 있다.
2. `NotifyBarrierWatch=false` 뒤에 변경 알림 프로토타입을 구현했다. 일반 Player의 기본 동작·검증 주기는 유지한다.
3. 알림은 다음 dispatch에서 처리하고, 관측 불가능한 직접 변경은 64프레임 감사로 남긴다. 모든 행에 외부 쓰기 가능성이 있어 완전한 무순회는 아직 증명되지 않았다.
4. AUTOTEST에 같은 dispatch의 기존 서명 대조·불일치 누적·실패 시 중단 명령을 추가했다. 게임 실행·픽셀·성능 측정은 하지 않았다.
5. 제품 비활성 코드·정책 필드를 조사만 했다. 삭제·Mods 수정·잠금 해제·푸시는 없으며, 기존 G1 픽셀 불합격은 그대로다.

2026-11-03. 요청: `E:\StutterFixWork\codex-simplify-prompt.md`.
작업 트리 `E:\StutterFixWork\wt-partial`, 브랜치 `claude/gpudeco-cheap`.
기존 [cheap-research.md](cheap-research.md)와 `cheap-steps/` 17판 계획은 먼저
`claude/gpudeco-partial`의 `2effec8`에 보존했다. [G1 구조](g1-code-structure.md)를 이어받았다.

## 근거와 범위

| 기존 실측 (이번에 재실행하지 않음) | 값 | 해석 |
|---|---|---|
| Exit `night-results/g1ab-sh-1-partial.log` | total 0.086ms, validate 0.0575ms, dispatch 0.014ms, progress 0.0088ms, check 0.0065ms, 감시 946개 | 곡 평균 관리 코드 계측. validate 안의 세 함수 비중은 아직 분리되지 않았다. |
| DDONGSSADA `evidence/g1/g1-dd-missingfix.excerpt.txt` | total 0.427ms, validate 0.396ms | 0.396ms 전부를 장벽 순회 절감량으로 간주할 수 없다. |
| 이번 후보 | FPS/ms/픽셀/실제 후크 설치 성공 미측정 | 아래 비용·주기 계산은 설계값이다. |

조사 범위는 저장소의 GpuDeco 호출·정책 쓰기, Dormancy/TileCull/FastMove/MoveApply/TightLate,
`E:\StutterFixWork\decompiled\Assembly-CSharp`의 `scrDecoration`, `scrVisualDecoration`,
`scrParticleDecoration` 및 정렬 키 계약이다. 임의 외부 DLL의 모든 쓰기를 전수 증명한 것은 아니다.
카메라별 OnPreCull 이후 변경과 나중에 생성되는 임의 렌더러는 알림만으로 완전 감시할 수 없다.

## A. 장벽 입력·변경 경로 전수표

`DrawCollapseBarrierActive`: enabled, activeInHierarchy, forceRenderingOff, SpriteRenderer.sprite/color.a.
`GpuDecoScene.SortKey/Distance`: sorting layer/order, Material.renderQueue/ID, 슬롯, bounds.center
(SpriteSortPoint.Pivot이면 transform.position), 카메라 위치/forward/정렬 모드.
기존 `DynamicOrderTick`의 캐시 서명은 **order, transform.z, material ID, layer ID뿐**이다.
따라서 renderQueue 제자리 수정·bounds만 변한 파티클 등의 기존 감시 공백을 후보 대조 0으로 증명할 수 없다.

| 변경 경로 (`파일:함수`) | 바뀌는 입력 | 기존 알림 | 후보 처리 / 잡지 못하는 부분 |
|---|---|---|---|
| `scrDecoration:Setup/Awake`; `Dormancy:WakeBase` | 생성·초기값·새 sprite/mesh renderer | `CollapseMarkNew`는 GPUDECO에도 있음 | 기존 재계획 목록 유지. 감시 인덱스 재작성 때 초기 전수 감사. 임의 Renderer 생성은 별도 잔여 경로. |
| `scrVisualDecoration:SetDepth`; `GpuDecoPlayer:DepthChanged` | layer/order | 제품 후크는 `GpuDepthChanged`만 호출 | 제품 후크에서 후보 알림도 전달. 기존 barrierWatch와 dirtyIds는 소비하지 않는다. |
| `scrDecoration:SetColor/SetOpacity` → `scrVisualDecoration:ApplyColor` | sprite alpha | Dormancy WakePost의 dirty 알림은 DEV/AUTOTEST 한정 | 후보 소유 Harmony가 ApplyColor 후 알림. floor opacity의 후속 ApplyColor도 포함. |
| `scrVisualDecoration:SetVisible` | renderer.enabled | 같은 제한 | 후보 독립 후크. 논리 visible과 원본 억제 구분은 G1 경로 유지. |
| `scrVisualDecoration:SetMaskingType/EnableMeshRenderer` | enabled, mesh object 활성, 마스크 | 같은 제한 | 후보 독립 후크. 보조 렌더러 검사는 기존 Validate에서 유지. |
| `scrVisualDecoration:SetSprite` 두 overload; LazySprite/FastMove 이미지 적용 | sprite 존재·재질·기하/bounds | 기존 dirty 알림만으로 완전하지 않음 | 구체 메서드 모든 overload 후크. 직접 sprite 대입은 잔여. |
| `scrDecoration:SetPosition* / SetPivot* / SetParallax* / SetPlacementType` → `UpdatePosition`; `LogicUpdate` | 위치·시차·카메라 잠금 변환 | 기존 Dormancy 색/visible 후크로는 부족 | UpdatePosition 후크. 빠른 MoveApply/TightLate의 직접 transform 대입은 우회할 수 있어 잔여. |
| `scrDecoration:SetRotation/SetScale`; Visual SetTile/SetTextureScaleMultiplier; geometry 갱신 | 회전·크기·bounds·재질 속성 | 전용 완전 알림 없음 | 잔여 감사. 기존 4필드 서명이 놓치는 bounds/queue만의 변경은 그대로 한계. |
| `scrVisualDecoration:SetDepth`의 stickToFloor + 부모 타일 이동/opacity | 부모 기준 order·위치·alpha | setter를 거친 부분만 | setter 후크 + 부모/직접 transform 변경은 잔여. |
| `scrParticleDecoration:SetDepth/SetVisible/ResetParticle/Update`; ParticleSystem 시뮬레이션 | particle renderer order/활성/bounds 중심 | Visual 후크에 포함되지 않음 | 파티클 행은 잔여. 거리 변경은 기존 서명 자체가 불완전하므로 독립 픽셀 대조 필수. |
| 타일·텍스트·행성·기타 MeshRenderer/SpriteRenderer | order·active·alpha·bounds | Visual로 연결된 부분 외 없음 | 비장식 행은 잔여. SortingGroup/카메라 레이어 제외는 기존 입장 규칙 유지. |
| `TileCull:SetEnabled/Cull/Restore/Shutdown`; 외부 모드 renderer 제어 | enabled 및 카메라별 일시 변경 | 알림 없음 | 잔여. preCull에서 바꾸고 되돌리는 변경은 dispatch 감사 시점과 다를 수 있다. |
| MatShare/재질 교체·외부 `sharedMaterial(s)`/`renderQueue` 수정 | 재질 ID·queue·슬롯 | 일반 Unity setter 전역 후크 없음 | 모든 행에 직접 쓰기 가능성이 있으므로 잔여로 둔다. queue만의 변화는 기존 서명보다 강한 oracle이 필요. |
| `GameObject.SetActive`, 부모 계층 활성, Destroy, 외부 transform/bounds 대입 | activeInHierarchy·생존·위치/거리 | 알림 없음 | 모든 행 잔여. 파괴된 행을 건너뛰는 기존 동작도 유지하며 그 잔여 캐시 문제를 해결했다고 주장하지 않는다. |
| 카메라 이동·회전·sort mode/axis·cullingMask | 정렬 거리·가시 집합 | 장식 setter 알림과 독립 | 기존 Validate의 카메라 축/모드 검사 유지. 단순 직교 이동은 기존 shift 보정. 임의 새 카메라/레이어 변경까지 지원 확장하지 않음. |
| `GpuDecoExport:G1 visible/image`, 동적 제외/재포함 | 통합 슬롯·복사 마스크·독립 alpha | G1 및 CollapseSpanAlpha/RefreshItem | 기존 동작 유지. `ExcludedVisibilityTick`의 매 dispatch 평가는 제거하지 않음. |

### 프로토타입

- `DrawCollapsePolicy.NotifyBarrierWatch=false`. Player에서 켜는 설정 UI/파일 입력은 추가하지 않았다.
  AUTOTEST 명령 또는 소스 실험으로만 켠다. 기본 꺼짐 상태에는 추가 Harmony 후크가 없다.
- 켜질 때 `GpuDecoPlayer.ConfigureBarrierNotifications`가 자체 Harmony owner
  `StutterFix.GpuDeco.Player.BarrierWatch`로 구체 메서드만 패치한다. abstract 메서드는 제외한다.
  실패 시 부분 패치를 되돌리고 예외를 전달한다. off/Shutdown에서 owner의 패치를 해제한다.
- 준비 인덱스 `renderer ID → watch row`와 중복 제거 pending 집합을 사용한다.
  통상 dispatch에서는 pending 행만 방문한다. 기존 dirtyIds는 재계획용이므로 그대로 둔다.
  pending을 먼저 소비해 API 호출 중 재진입한 알림은 다음 dispatch까지 보존한다.
- 첫 dispatch/재시작/재계획 때 전수 감사. 이후 관측 불가능한 쓰기를 허용하는 행만 64프레임 감사한다.
  현재 코드·외부 호환 계약에서는 **모든 감시 행이 이 잔여 집합**이다. 닫힌 writer 집합이 증명되면
  그때만 해당 행을 감사에서 뺄 수 있다. 알림만으로 안전하다고 분류한 행은 0개다.
- 기존 모드는 그대로 8프레임 주기다. 알림 없는 정적 구간의 속성 읽기 감사 횟수는
  대략 1/8로 줄어들 수 있지만, 후크 비용/HashSet/움직이는 장식의 알림 비용이 추가된다.
  실제 절감률이 아니며, 잔여 변경의 최악 탐지 지연은 64프레임으로 늘어난다. 제품 채택 근거가 없다.
- 재분류와 Exclude/Include/대표 bounds 갱신 코드는 공유한다. 전수 안전 검사, pending fallback,
  CPU 복귀, G1 논리 visible 및 `ExcludedVisibilityTick`은 유지했다. 프로토타입이 검증 비용을 0으로 만들지는 않는다.

### 같은 프레임 AUTOTEST 명령 (미실행)

GPU 소유 비활성 상태, Product가 설정된 PlayerAuto에서 실행한다.
`gpudeco drawcollapse notify check`는 준비/활성화 **전에** 켠다.
on/check/off 전환은 active ownership이면 거부한다.

| 명령 | 의미 |
|---|---|
| `gpudeco drawcollapse notify check` | 후보 + 기존 8프레임 시점의 서명 oracle. 누적 카운터 초기화. |
| `gpudeco drawcollapse notify state` | enabled/oracle/checks/comparisons/mismatches 출력. |
| `gpudeco drawcollapse notify verify` | 후보/oracle 켜짐, 실제 생략 행 comparisons>0, mismatches=0 필수. 무표본은 실패. |
| `gpudeco drawcollapse notify on` | 대조 없는 비용 측정 모드. correctness 합격 근거로 사용 금지. |
| `gpudeco drawcollapse notify off` | 기존 8프레임 방식 복원, 후크 해제. 비활성 상태에서만. |

oracle은 기존 검사 예정인 같은 dispatch에서 후보가 생략할 행의 active/4필드 서명을 캐시와 비교한다.
누락이면 mismatches를 누적하고 dispatch 전에 예외로 기존 복귀 경로에 넘긴다.
후보가 선택한 행은 같은 기존 처리문을 실행하며, 별도의 독립 렌더링 모델을 실행하는 시험은 아니다.
공유한 기존 서명 자체의 누락·GPU 그림 차이·후크 설치 여부는 이 카운터만으로 증명하지 못한다.
check의 전수 읽기/목록 검색 비용은 성능 시험에 포함하지 않는다.

### Validate 제거 판단

`DrawCollapseGuard.Check`는 제품 콜백이 있어도 **proxy/barrier order changed**와
**sort key changed** 두 종류만 `OrderViolation` 로그로 전환한다.
나머지는 collect=false에서 예외다. `CheckDrawCollapseBeforeDispatch`는 제품이면 다시 throw하고,
상위 dispatch 오류 처리·복귀가 동작한다.

| 검사 | false 경로 동작 | 제거 판단 |
|---|---|---|
| RequireDrawCollapse: 소유/준비/whole-song/slice/merge 조건 | 예외 | 유지 |
| prepared texture/spans/included, CPU fallback 계약 | 예외 | 유지 |
| 카메라 생존·직교·축·정렬 모드 | 예외 | 유지 |
| 보조 renderer가 다시 보이는지 순환 점검 | 예외 | 유지 |
| owned row 생존/재질 소실 | 예외 | 유지 |
| owned sort key 변화, proxy/barrier 순서 | 일부 로그 전용 | 분리 제거 후보이나 외부 변경의 진단을 잃음. 이번에는 유지 |
| 대표 renderer/material 생존 전수 | 예외 | 유지 |
| full 전용 included/전수 order 검사 | 진입/재계획 계약 | 유지. false 비용 절감과 별개 |

`SkipOrderSweep=true`여도 앞의 계약 검사·대표 순회가 남는다.
`ExcludedVisibilityTick`은 연속 alpha 채널을 평가하여 독립 렌더러 `RefreshItem`을 바꾼다.
실제 화면/그리기 비용을 바꾸므로 로그라고 취급하지 않는다.

## B. Player 비활성 코드 조사 (삭제 없음)

일반 Player, AUTOTEST 없음, GPUDECO 정의, Product=true 및 정상 프로그램 호출 기준이다.
리플렉션으로 private 필드를 강제로 바꾸는 외부 코드는 고정값 증명의 범위 밖이다.
[source-inventory.json](evidence/simplify/source-inventory.json)은 csproj 조건·#if를 해석한 정적 목록이다.
185개 Compile 항목 포함, 95개 제외. 전처리 결과는 C# 컴파일러의 전체 호출 그래프 증명이 아니다.
`GpuStep`/진단 진입점 제거와 실제 쓰기 호출자를 추가로 추적했다.

줄 수는 원문 메서드/파일의 물리 행 수이며 주석·중괄호를 포함한다.
서로 중첩되므로 합산하지 않는다. 예상 삭제 LOC나 최종 DLL 바이트 감소 실측이 아니다.

| 항목 / 근거 (`파일:함수`, 켜는 곳) | 줄 수 / 범위 | 제품에서 실행되지 않는 내용 | 영향·삭제 시 깨질 것·위험 |
|---|---|---|---|
| `GpuDecoDrawCollapseRenderer:DispatchDrawCollapse` | 함수 93행; 직접 경로 1408–1420, recorded 이하 1441–1482 | GlobalVerts=true/DirectDispatch=false/MinimalDispatch=true/Product=true이므로 직접·recorded·span 제출은 선택되지 않음 | 분기 확인 비용 일부 후보, 주 이득은 유지보수. AUTOTEST policy direct/minimal/globalverts, 기존 진단·independent 복귀와 혼동하면 위험 높음. 함수 끝 fallback evaluate/copy는 **살아 있음**. |
| `BuildBarrierOrderClones` | 함수 71행, early return 뒤 본체 | CloneBarrierMaterials=false. 쓰기 지점은 AUTOTEST gate만 | 호출/return은 남음. 머티리얼 순서 실험·복원 관리가 깨짐. 유지보수 위주, 중간 위험. |
| `BuildDrawCollapseSpans`의 TieAllBarriers/CloneBarrierMaterials/TightAllOwned 반대 분기 | 함수 295행; 조건 위치 925–926/별도 Trim:58 | 고정 false 조건의 본체 | 준비 비용/코드량 후보. 기존 픽셀 비교 실험을 잃음. 중간~높음. 함수 전체 제거 불가. |
| KeyCache=true/FullScanOnReplan=false | 같은 295행 함수의 859/881 조건 | 강제 매 재계획 full scan 선택만 비활성 | `!gpuCollapseScanned` 최초 스캔과 캐시 무효 fallback은 **실행됨**. 잘못 삭제하면 새 장벽 누락. 높은 위험, 준비 시 비용만. |
| `GpuDecoRenderMerge` | 파일 320행, Player 비공백 299행; Prepare 92행/Dispatch 22행/command 49행 | prepare=true/on=true 쓰기는 `GpuRenderMergeCommand`뿐. Player에서 그 호출자인 `GpuStep` 제거. prepare/on 기본 false·Shutdown false | 본체 비활성이나 `MergeHidden`/자원 해제/공유 참조·정적 List 생성은 남음. 파일째 제거하면 빌드와 연구 gate 깨짐. 유지보수/초기화 메모리 위주, 높은 위험. |
| `GpuDecoPackets` | 파일 415행, Player 비공백 410행; Compile 69행/command 29행/TryGpuPacketRun 61행 | mode=0, nonzero 쓰기는 command뿐이며 Player 호출자 제거. Prepare의 Compile 조건 false, TryGpuPacket 먼저 거절 | FastMove 호출·초기화/정리와 배열/사전 선언은 살아 있음. `packets shadow/static/native/all/replay/compiled/compiledcheck`, rollback 진단 깨짐. 호출/branch 비용 일부 + 유지보수, 높은 위험. |
| `GpuDecoPacketTargets` | 파일 142행, Player 비공백 139행 | packets mode 6/7 fast targets/oracle는 위와 같이 진입 불가 | 공유 타입·호출 정리 필요. compiled target 시험 깨짐. 높은 위험, 주로 유지보수. |
| `GpuDecoDrawCollapse:GpuDrawCollapseCommand`, `GpuDecoExport:GpuStep` | 전자 파일 215행 중 Player 비공백 43행만 남음; 후자 파일 2974행 중 1695행 | DEV/AUTOTEST 진단 진입점은 이미 전처리 제외 | 제품 DLL 축소 이득 없음. 연구 명령/측정 흐름 삭제 위험 높음. 제외 행 수를 새 삭제 가능량으로 계산하지 않음. |
| `GpuDecoDrawCollapseGate`, `GpuDecoRenderMergeGate`, `GpuDecoCostProbe`, image fixtures, ablation/storage/snapshot/order probe | csproj 제외 95항목 전체 목록은 inventory; 아래 GPU 파일 표 | 컴파일 항목 자체가 Player에서 제외 | 제품 런타임/바이너리 이득 없음. AUTOTEST·DEV·오프라인 시험을 깨뜨림. 삭제하지 않음. |
| HideProxies=false/SkipPausedCompute=false/ClipScale=1 | Dispatch 93행 등, 필드별 참조 행 수는 아래 표 | 숨김/정지 compute 생략/축소 실험 | 1회 분기·연산 수준 후보이나 Mono 최적화 여부 미측정. ablation 및 frozen compute 비용 분리가 깨짐. 중간 위험. |
| PartialMaskAdmit/SplitByOrder/CullIdleSpans/DisableOwnedRenderers/ClipOffscreen/ExactBigArray 등 고정 true | 아래 전체 필드 표, 참조 지점은 JSON | 반대 선택 분기만 후보; 참인 본체는 제품 실행 | 실제 마스크/정렬/복사/렌더러 복구 계약과 결합. 단순 필드 삭제보다 호출 계약 단순화가 필요. 높은 위험, 절감 시간 미측정. |
| PreStartActivate/DeferEndOnResult/ActivateInCatchUp/MidReusePlan/KeepBeforeBarrier/NudgeTieSpans/KeepAfterBarrier | 아래 표; Player 활성화·종료/BuildDrawCollapseSpans | false 설정 분기는 진단 외 없음 | 재시작·중간 시작·CPU 복귀까지 시험해야 함. 높은 위험. KeepAfterBarrier의 기존 픽셀 문제를 삭제로 해결했다고 볼 수 없음. |
| `AdoptNativeTweens` | 필드+제품 writer 1곳, 참조 7행 | **고정값 아님**. `SetNativeAdoption`이 켬/Shutdown 해제에서 값을 씀 | EnsureNativeAdoptionHook 및 GPU 재개·네이티브 트윈 인수에 사용. 죽은 플래그로 분류 금지. |
| `NotifyBarrierWatch` | 새 필드, 기본 false | 일반 Player에서는 켜는 명령 없음; 새 연구 경로만 비활성 | 후보 prototype/후크/대조 명령을 잃음. 이번 조사 결과를 근거로 지우지 않음. |

### 정책 필드 전체표

초기 35개 가변 정적 필드 + 신규 bool 1개 = 36개다(여러 선언의 int 두 개를 각각 셈).
상수 ArrayBindings/DirectBindings/OwnedChecksPerFrame/TieNudge 등과 Guard의 진단 플래그는 별도다.
아래 쓰기는 전처리상 남은 대입이며, `AdoptNativeTweens`의 실제 호출도 확인했다.
참조 행 수는 조건·선언을 포함한 소스 규모 지표이지 죽은 본체의 삭제 LOC가 아니다.

| 필드 | Player 초기값 | Player 쓰기 지점 | 읽기/참조 행 수 |
|---|---|---|---|
| `AdoptNativeTweens` | `true` | GpuDecoNativeAdopt.cs:196 | 8 |
| `PartialMaskAdmit` | `true` | 없음 (고정) | 4 |
| `SplitByOrder` | `true` | 없음 (고정) | 2 |
| `RecordedDispatch` | `true` | 없음 (고정) | 3 |
| `MinimalDispatch` | `true` | 없음 (고정) | 2 |
| `TieAllBarriers` | `false` | 없음 (고정) | 2 |
| `CloneBarrierMaterials` | `false` | 없음 (고정) | 3 |
| `CullIdleSpans` | `true` | 없음 (고정) | 2 |
| `HideProxies` | `false` | 없음 (고정) | 7 |
| `ClipScale` | `1f` | 없음 (고정) | 2 |
| `GlobalVerts` | `true` | 없음 (고정) | 12 |
| `DirectDispatch` | `false` | 없음 (고정) | 2 |
| `DisableOwnedRenderers` | `true` | 없음 (고정) | 4 |
| `TileComposite` | `false` | 없음 (고정) | 1 |
| `TileSingleMaxArea` | `.98f` | 없음 (고정) | 1 |
| `TileMerge` | `true` | 없음 (고정) | 1 |
| `ClipOffscreen` | `true` | 없음 (고정) | 4 |
| `SkipOrderSweep` | `true` | 없음 (고정) | 2 |
| `NotifyBarrierWatch` | `false` | 없음 (고정) | 5 |
| `SkipPausedCompute` | `false` | 없음 (고정) | 2 |
| `PreStartActivate` | `true` | 없음 (고정) | 2 |
| `DeferEndOnResult` | `true` | 없음 (고정) | 3 |
| `ActivateInCatchUp` | `true` | 없음 (고정) | 3 |
| `MidReusePlan` | `true` | 없음 (고정) | 2 |
| `TightLargeQuads` | `true` | 없음 (고정) | 3 |
| `TightAllOwned` | `false` | 없음 (고정) | 2 |
| `TightMinSide` | `1025` | 없음 (고정) | 4 |
| `TightMarginTexels` | `4` | 없음 (고정) | 3 |
| `TightMaxAreaRatio` | `.8f` | 없음 (고정) | 2 |
| `ExactBigArray` | `true` | 없음 (고정) | 2 |
| `KeyCache` | `true` | 없음 (고정) | 3 |
| `FullScanOnReplan` | `false` | 없음 (고정) | 2 |
| `KeepBeforeBarrier` | `true` | 없음 (고정) | 2 |
| `NudgeTieSpans` | `true` | 없음 (고정) | 2 |
| `KeepAfterBarrier` | `true` | 없음 (고정) | 2 |
| `MaxSpans` | `700` | 없음 (고정) | 2 |

### 이미 Player 컴파일에서 제외되는 GPU 파일

| Player excluded GPU source | Physical lines |
|---|---|
| `GpuDecoDrawCollapseGate.cs` | 175 |
| `GpuDecoThreadCpu.cs` | 115 |
| `GpuDecoRenderMetrics.cs` | 53 |
| `GpuDecoCostProbe.cs` | 58 |
| `GpuDecoPacketFixture.cs` | 260 |
| `GpuDecoAblation.cs` | 82 |
| `GpuDecoStorage.cs` | 93 |
| `GpuTileComposite.cs` | 352 |
| `GpuDecoImageGeometryFixture.cs` | 467 |
| `GpuDecoImageTextureFixture.cs` | 111 |
| `GpuDecoSnapshot.cs` | 497 |
| `GpuDecoOrderProbe.cs` | 228 |
| `GpuDecoRenderMergeGate.cs` | 202 |

## 사용자 복귀 뒤 측정 순서 (예약·실행하지 않음)

1. 사용자 게임 종료와 실행 허가를 확인한 다음에만 별도 실행 담당자가 설치/잠금 정책을 따른다.
   이번 작업에서 `user_playing.flag`, `run_locked.py`, 게임/Mods는 변경·호출하지 않는다.
2. 동일 소스 PlayerAuto/내장 bundle/native/map/settings 해시를 기록한다.
   정지된 준비 단계에서 `notify check`를 켜고 Product=true인 실제 경로로 준비/활성화한다.
   깊이·visible·알파 0 경계·이미지 Missing/복귀·mask·비소유 이동·타일·파티클·재시작/seek를 실행한다.
   같은 시점 oracle comparisons>0/mismatches=0과 후크 설치·해제/CPU 복귀를 확인한다.
   불일치·무표본·GPU 복귀 발생 시 그 판은 성능 후보 합격으로 쓰지 않는다.
3. 기존 서명보다 강한 화면 대조를 한다. CPU 원본 독립 실행, GPU 정상, 후보, 복원 및 noise를
   Arche 262초 포함 여러 시점에서 비교한다. 기존 G1 최대 채널 차이 4(허용 3 초과)를 먼저 해결해야 한다.
4. 정확성 통과 뒤 새 프로세스별 off/on/on/off로 Exit와 DDONGSSADA부터 비교한다.
   check는 끄고 notify on으로 측정한다. total/validate/dispatch/progress/check, watch 수,
   알림/검사 건수, 같은 무거운 창 FPS·프레임 시간·긴 프레임·실제 fallback 수를 함께 기록한다.
   0.0575/0.396ms 전체가 절감 가능하다는 가정을 검증하지 않고 개선율로 보고하지 않는다.
5. ExcludedVisibilityTick/Validate/동적 감시를 분리 계측해야 남은 비용을 귀속할 수 있다.
   이번에는 추가 타이밍 계측을 넣지 않았다. 개선이 재현될 때만 전체곡/다른 맵/복귀 범위를 확대한다.
   모든 측정이 끝날 때까지 기본 false를 유지한다.

## 로컬 검증과 한계

- `py -3 tools/optimization/simplify-audit.py`: Player 전처리 목록·필드 표 생성 성공.
  저장소 소스만 읽는 작은 분석이며 게임 DLL 스캔/디컴파일/성능 분석은 실행하지 않았다.
- `git diff --check`: 통과. 죽은 제품 코드 삭제는 하지 않았다.
- Player / PlayerAuto 최종 빌드 결과는 아래 기록한다. 게임 안 oracle·성능·픽셀은 미실행이다.

<!-- BUILD_RESULTS -->
