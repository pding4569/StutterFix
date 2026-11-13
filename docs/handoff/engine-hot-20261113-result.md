# 엔진 메인 스레드 조사 결과 — 2026-11-13

요청한 오프라인 분석·AUTOTEST 명령·벤치 설정·Claude 실행 계획을 준비했다. 새 성능 이득, 픽셀 일치, 실행 중 복원은 **미측정**이다. 게임 실행·종료, 설치 DLL 교체, 커밋·푸시는 하지 않았다. 기존 ScriptPath 작업을 보존했다.

작업 폴더 `E:\StutterFixWork\wt-v14prod`, 브랜치 `claude/adofai-renderer`, 조사 시작 HEAD `34c0f8ae57d37331400d2d3d5b250245c50c701d`. 기존 게임 프로세스 PID 28784가 확인됐으므로 이 세션이 게임을 실행하지 않았다는 말은 게임이 꺼져 있다는 뜻이 아니다.

## 근거와 재현

먼저 [엔진 내부 조사](../render-system/06-engine-internals.md)와 [기존 engine-extreme 결과](../deco-spec/results/engine-extreme.md)를 읽었다. WaitSkip, MaskGrow, 계층 스캔 8,192개 분할은 기존 발견이며 새 발견으로 세지 않는다. 과거 DD presentation 대기와 이번 job 대기는 서로 다른 경로다.

- `py -3.12 tools/analysis/engine-hot.py`: 기존 `E:\StutterFixWork\night-results\nat-hl.resolved.txt`, `nat-sh.resolved.txt`만 읽는다. HL 12,025개, Exit 12,021개 스택. [전체 자식·호출자·리프 분포](../../out/engine-hot/samples.md), [원시 집계·짧은 호출 경로·입력 SHA256](../../out/engine-hot/samples.json).
- `py -3.12 tools/analysis/engine-hot-disasm.py`: 설치된 UnityPlayer를 **파일로만** 읽는다. [디스어셈블](../../out/engine-hot/disasm.txt). `out/engine-hot/deps`의 capstone/pefile 사용. 필요한 경우 `py -3.12 -m pip install --target out/engine-hot/deps capstone pefile`.
- PE의 RSDS GUID `0BD6084081424C169079D97BFE021469`, age `1`이 제공된 PDB 디렉터리 키와 일치함을 검사한다. 불일치 시 중단. UnityPlayer SHA256 `7e362b4c6ecee6fb493b1872cda5a5a0caf44f3b131b3c4bad132f0d1309f436`.
- 함수 이름은 제공된 `night-results/unityplayer-syms.tsv`의 RVA를 사용한다. 새 PDB 전수 추출은 하지 않았다. 함수 범위는 다음 심볼까지로 제한하며, 레지스터·구조체 필드 의미는 추정이다. E8 전수 검색은 opcode 후보이므로 UI/물리 초기화 함수는 별도 디코딩으로 경계를 확인했다.

**표본은 호출 횟수·작업 개수·렌더러 개수가 아니다.** 메인 스레드를 샘플링한 결과라 워커 CPU 전체나 커널 대기 시간을 알 수 없다. 포함 비율끼리는 중첩되므로 합산하지 않는다. 아래 첫 표는 기존 stacktree와 맞춰 `runtime-invoke`가 있는 스택을 제외한 비율이다. 자식 분포 설명은 관리 콜백까지 포함한 전체 스택 기준이며 분모를 별도로 적었다.

## 함수별 분해

| 함수 | HL 스크립트 밖 포함 % | Exit 같은 기준 % | 전체 스택에서 확인한 자식/역할 |
|---|---:|---:|---|
| PlayerRender | 22.170 | 21.055 | Offscreen 3,222/3,722(HL), 2,496/3,328(Exit). 그 밖에 UI overlay, RenderCameras, low-level overlay |
| RenderOffscreenCameras | 18.620 | 15.165 | CustomRender 2,188/3,222, CustomCull 976/3,222(HL); Exit 1,894/2,496, 547/2,496 |
| Camera::CustomRender | 15.734 | 11.613 | HL: DoRenderLoop 893, 공유 renderer scene 577, 이미지 필터 456, depth texture 76 / 2,262. Exit: 필터 712, 공유 scene 635, DoRenderLoop 335, depth 110 / 2,016 |
| ujob_wait_for | 10.894 | 9.176 | 렌더 노드 추출·렌더러 갱신·컬링·SyncFence 등, 아래 표 |
| ujob_execute_job | 8.266 | 7.454 | HL: Transform 스캔 439, ExecuteRenderQueue 402, ClearJob 64, CullDynamicObjectsJob 48 / 995. Exit: 렌더 큐 435, Transform 202, 동적 컬링 196 / 899 |
| GetOrCreateSharedRendererScene | 4.798 | 5.199 | 추출 559/577(HL), 616/635(Exit). 호출자 모두 CustomRender |
| ExtractSceneRenderNodeQueue | 4.649 | 5.049 | wait 481/559, 메인 객체 통합 55/559(HL); Exit 506/616, 82/616 |
| RendererUpdateManager::UpdateAll | 4.565 | 2.562 | HL wait 274, Mesh Finalize 245, 변경 그룹 예약 21 / 549; Exit 153, 120, 28 / 309 |
| RenderManager::UpdateAllRenderers | 4.516 | 2.604 | RendererUpdateManager 536/543(HL), 309/314(Exit) |
| TransformChangeDispatch::ScanAndHandleChangesJob | 3.651 | 1.672 | 스캔 자체 396/439(HL), 151/202(Exit); 나머지는 실제 렌더러·파티클 콜백 |
| Camera::CustomCull | 3.501 | 4.925 | 관리 콜백 포함 HL 1,037개 중 OnPreCull 전달 736, CullScene 223; Exit 669개 중 CullScene 480, OnPreCull 101 |
| MeshRenderer::FinalizeUpdate | 2.037 | 0.998 | 전체 245/245(HL), 120/120(Exit)가 SyncFence 아래 |
| SyncFence | 2.037 | 0.998 | 전체 245/245, 120/120가 ujob_wait_for 아래 |

### ujob_wait_for는 대부분 직접 일을 한다

| 바로 바깥 호출자 | HL 표본 / 1,314 | Exit 표본 / 1,121 |
|---|---:|---:|
| ExtractSceneRenderNodeQueue | 481 (36.61%) | 506 (45.14%) |
| RendererUpdateManager::UpdateAll | 274 (20.85%) | 153 (13.65%) |
| SyncFence → MeshRenderer::FinalizeUpdate | 245 (18.65%) | 120 (10.70%) |
| CullScene | 93 (7.08%) | 267 (23.82%) |
| GfxDevice::EndAsyncJobFrame | 71 (5.40%) | 1 (0.09%) |
| TransformHandle::GetLocalToWorldMatrixInternal | 67 (5.10%) | 12 (1.07%) |

HL은 wait 바로 안쪽이 execute인 855개 외에 lane_guts를 거쳐 execute로 들어가는 140개도 있다. **총 995/1,314=75.72%**가 직접 job 실행을 포함한다. Exit는 827+72=899/1,121=80.20%다. 이 안에서 HL은 Transform 스캔 439, 렌더 큐 402, ClearJob 64, 동적 컬링 48개를 수행했다. 따라서 `ujob_wait_for 10.9%`를 전부 없앨 수 있는 휴면 시간으로 해석하면 안 된다.

디스어셈블의 `ujob_wait_for`(0x570880)에서 `lane_guts` 호출 0x570B10/0x570CB0, **ujob_execute_job 직접 호출 0x570C2B**를 확인했다. 의존 작업을 찾아 메인이 수행하는 경로가 있다. OS 대기 분기도 존재하지만 해당 대기의 실제 지속 시간·깨운 워커·critical path는 ETW ThreadTime 없이 미확정이다.

### 카메라와 반복 컬링

에셋의 level4(에디터)에는 Camera(mask 652055), BGMovingCam(1024), BGStaticCam(2048), PausePlanets(256), OverlayCam(1048576)이 있다. 이는 정적 구성 5개이며 **HL/Exit 실행 중 실제 그리는 카메라 수가 5라는 증거는 아니다**. 기존 turbo는 PausePlanets를 끈다. level1 등의 주 카메라 mask도 다르므로 에셋 숫자를 런타임에 하드코딩하지 않았다.

`RenderOffscreenCameras`의 0x37E8BB에서 CustomCull 호출을 확인했고 카메라 스택을 순회한다. `CustomRender → GetOrCreateSharedRendererScene → ExtractSceneRenderNodeQueue`도 표본에 반복 등장한다. 추출 함수는 `BeginRenderQueueExtraction`(0x3820B6), job kick(0x3820CF), wait(0x3820FC), 메인 객체 통합(0x382111)을 수행한다. 다만 카메라 포인터가 없는 샘플 스택으로는 **몇 카메라가 같은 프레임에 scene을 재사용했는지, 매 카메라가 반드시 재추출하는지** 판정할 수 없다.

`scrCamera.cs:618`의 `UpdateCustomFrameRateScreen`은 Bgcamstatic.Render, BGcam.Render, camobj.Render를 명시적으로 부른다. 이는 사용자가 custom FPS 기능을 켰을 때의 추가 경로이며 이번 표본에서 기능이 활성인지 미확인이다. 카메라를 단순히 enabled=false로 만들어도 명시적 Render 호출 제거를 보장하지 않는다.

실제 카메라별 호출 횟수는 새 `engknob observe on`으로 12초 기록하고 assert/state에서 이름별 preCull 횟수와 경과 프레임을 읽는다. `engknob census`는 ID·mask·target texture·오클루전·해당 layer의 활성 Renderer 후보 수를 출력한다. 후보 수는 frustum/마스크/스키닝/동적 draw를 반영한 최종 visible 수가 아니다. census의 전체 리소스 스캔은 **성능 측정 구간 밖**에서 실행한다. 비활성 카메라를 명시적으로 Render하는 경로는 `Camera.allCameras`에서 빠질 수 있으므로 observe 결과도 전수 Render 호출 계측은 아니다.

### Transform 변경 구독

`ScanAndHandleChangesJob` 0x675A50의 0x675B2A는 `start + 0x2000`을 상한으로 잡는다. 스캔 루프에서 변경 비트와 관심 마스크를 대조하고 콜백을 호출한다. 비용은 전체 renderer 개수 하나보다 **더러운 계층의 크기·관심 마스크·변경 대상 수·분할 수**에 달린다. 작업 개수/계층별 실제 길이는 이번 표본만으로 알 수 없다.

- 실제 자식: HL은 ParticleSystemRenderer bounds 20, MeshRenderer dispatch 15, SpriteRenderer 변경 7 / 439. Exit는 Mesh 25, Sprite 12, Particle bounds 7, SpriteMask 변경 7 / 202. 렌더러 갱신을 통째로 생략하면 위치·bounds·컬링·마스크가 깨질 수 있다.
- UI::RectTransform::InitializeClass는 0x66DBA2, 0x66DBD1에서 permanent-interest 등록. UI도 구독 시스템이라는 코드 근거다. 이번 스캔의 큰 비용이 UI 때문이라는 증거는 아니다.
- PhysicsManager::InitializeClass의 0x6CA265에서 RegisterSystem 호출. 이는 **3D 물리 관리자**의 등록 근거이며 Physics2D 전체나 콜라이더 없는 장식에 대한 개별 구독 증거가 아니다.
- Transform 초기화, ObjectDispatcher transform tracking, SpeedTree도 직접 등록 참조가 있다. 등록 참조 존재와 해당 맵에서 사용됨을 구분한다. 간접 등록/구독 인스턴스 목록은 복원하지 않았다.

콜라이더가 없는 장식에도 렌더러·파티클은 Transform 변경을 필요로 한다. 부모 이동이 자식 collider·UI에 미치는 효과도 있어 전역 물리/UI 구독 제거를 의미 보존 후보로 채택하지 않았다.

## 후보 우선순위와 비용 규모

아래 ms는 **동일 프레임 4.35ms를 가정한 표본 비율 환산**이다. 이번 실행의 프레임 시간, 예상 실측 이득, 달성 가능한 확정 상한이 아니다. 중첩되어 합산 금지. 개선 기대값은 실험 전 미확정이며 0 또는 악화도 가능하다.

| 순서 / 후보 | 불변 조건과 위험 | HL에서 관련 비용 규모 | 준비 상태 |
|---|---|---|---|
| API 1: jobs1/2/4/8 | 같은 의존 job을 모두 완료하고 출력 보존. 워커 감소로 직렬화·tail 악화, 증가로 경합 가능 | wait 포함 예산 약 0.474ms; 직접 작업까지 포함하므로 전량 절감 불가 | 개별 시험 구현, 원래 수 복원. 최대 수 초과/현재 수와 같으면 INVALID |
| API 2: useOcclusionCulling=false | baked occluder가 기여하지 않는 카메라에서만 결과 보존 기대. 더 많은 draw/GPU 비용 가능 | 스크립트 밖 CustomCull 약 0.152ms 전체 중 일부 | occ 구현. turbo lossless가 이미 껐으면 INVALID로 중복 시험 배제 |
| API 3: BG 카메라 mask 축소 | 렌더러·명령 버퍼·필터·배경 clear·향후 생성 layer까지 계약 필요 | Offscreen 전체 약 0.810ms 중 배경 몫 미확정; 추출 전체 약 0.202ms | bgmask는 BG 두 카메라 mask=0 **화면 변경 진단**. 제품 적용 아님 |
| API 4: Physics2D.autoSyncTransforms=false | 쿼리 이전의 명시적 SyncTransforms와 충돌/트리거 결과 보존 필요 | 관련 2D 동기화 표본을 분리하지 못해 ms 미확정 | sync2d 진단 구현. 원래 false면 INVALID. OPT의 PhysicsPause와 중복 가능 |
| API 보류: renderer static flags/계층 변경 | 이동·재질 변경이 있는 장식을 static으로 표시하면 잘못된 bounds/배칭 가능. 계층 분할은 local 좌표·장면 정리 보존 필요 | Transform 스캔 포함 약 0.159ms, 자체 리프 약 0.143ms | 기존 flatten 조사 참조, 임의 static 적용 없음 |
| native 후순위: Transform 스캔 분할 크기 | 예약 측 시작 번호/블록 수와 소비 측 크기를 함께 바꿔 누락·중복 없이 한 번씩 처리해야 함 | 위 0.159ms 예산; 워커 몫 미확정 | 0x675B2A의 0x2000 확인. 이 한 곳만 0x4000/0x800으로 바꾸면 겹침/누락 위험. 패치 미구현 |
| native 후순위: renderer scene/추출 재사용 | view/projection/cullingMask/sorting/mask/command buffer/프레임 내 변경 epoch가 일치할 때만 재사용 | 공유 scene 약 0.209ms, 추출 약 0.202ms로 중첩 | ExtractSceneRenderNodeQueue 0x381FD0 후보, 소유·무효화 계약 미복원. 무조건 early return 금지 |
| native 보류: wait 폴링/stealing 변경 | 의존 job 완료와 fence memory ordering 유지 필요. 기존 WaitSkip와 다른 함수 | 전체 wait 예산 0.474ms, 대다수가 유효한 일 | wait 반환/execute 생략 패치 없음. ETW·워커 표본 선행 |

기존 turbo lossless는 PausePlanets 비활성, depth/occlusion 조정, renderer probe/shadow/motion 플래그를 이미 건드린다. 기존 rlNR 설정/시작 명령을 복제하고, 적용 가능한 대상이 없으면 성공으로 세지 않는다. 기본 꺼짐 설정을 새 제품 옵션으로 추가하지 않았다. 작업/렌더 스레드 우선순위는 OPT의 ThreadSchedulingMode=2·ThreadRenderScheduling과도 겹쳐 새 우선순위 기능은 만들지 않았다.

## 명령·벤치 사용

`EngineKnob.cs`는 AutoTestBuild=1에서만 컴파일한다. DEV/일반 Player에는 포함하지 않는다. 설정 저장·기본 hook이 없으며 명시적 on에서만 활성화한다. 한 번에 한 항목만 빌린다.

```
engknob census
engknob observe on
engknob assert observe
engknob off
engknob jobs1 on       # jobs2 / jobs4 / jobs8
engknob assert jobs1
engknob off
engknob occ on         # bgmask / sync2d
engknob state
engknob assert occ
engknob off
```

on과 assert 사이에는 프레임이 지나야 한다. 카메라 시험은 대상마다 preCull 호출 >0, 대상 생존, 종료 시 설정 일치를 요구한다. worker 수와 2D sync도 종료 시 값을 검증한다. 프레임 사이의 모든 외부 쓰기를 감시하지는 않으므로 assert는 전 구간 불변성 증명이 아니다. 복원은 off·AutoTest 종료/keep·실패·SceneReset·모드 비활성/언로드에서 시도하고 readback 실패를 기록한다. 동적 신규 카메라는 자동으로 건드리지 않는다.

카메라 시험과 observe에는 작은 preCull 카운터 비용이 있다. `ekObserve`와 rlNR을 먼저 비교하고, 필요하면 같은 카메라 집합을 센 대조군으로 맞춘다. observe는 모든 활성 카메라, occ/bgmask는 변경한 카메라만 세므로 세 항목의 감시 비용이 정확히 같다고 가정하지 않는다. 이 진단 비용을 보정하지 않은 차이를 제품 이득으로 해석하지 않는다.

외부 `E:\StutterFixWork\bench_v14.py`에 **rlNR 복제** 항목 `ekObserve`, `ekJ1`, `ekJ2`, `ekJ4`, `ekJ8`, `ekOcc`, `ekBg`, `ekSync2d`를 추가했다. 기존 CFG_MID에서 seek/waitsongpos 뒤에 켜며, CFG_POST에서 assert 후 off한다. KEYS에 `[engknob]`를 추가했다. accepted와 `restored ... errors=0`가 둘 다 있어야 FPS를 인정하며, assert 실패/복원 실패/FPS 누락은 INVALID다. 기존 JALib/Harmony 실패 거르기도 유지했다.

Claude 실행 예시(이 세션에서는 **실행하지 않음**):

```powershell
$env:BENCH_WT = 'wt-v14prod'
$env:BENCH_RT = '3440x1440'
py -3.12 E:\StutterFixWork\bench_v14.py engine-hot-j2 hl rlNR,ekJ2,ekJ2,rlNR
```

설치·빌드 선택은 기존 run_locked 흐름대로 Claude가 담당한다. 한 프로세스씩 순차 실행하며 현재 사용자/다른 에이전트의 게임과 겹치지 않아야 한다. `ekOcc`가 no eligible camera면 기존 turbo가 이미 적용한 것인지 먼저 census로 확인한다. `ekJ2`가 현재 2면 no-op이므로 다른 수를 택한다. `ekBg`/`ekSync2d`를 의미 보존 성능 후보와 섞지 않는다.

`py -3.12 tools/analysis/prepare-engine-hot.py`는 [계획 디렉터리](../../out/engine-hot/plans)에 HL/Exit/Arche 각 1개 census와 5개 frozen pixel 계획(occ, jobs1/2/4/8), 총 18개 JSON을 만든다. baseline 재캡처 일치 → 변경 → 차이 0 → 원복 → 복원 차이 0 순서다. fixed screenshot 통과 후에도 live 재생, 카메라 움직임, 마스크/정렬/물리 상태, 맵 전체 검증은 별도로 필요하다. sync2d의 충돌 동등성은 이 이미지 계획으로 검증하지 않는다.

## 검증과 남은 일

- 기존 입력 집계와 stacktree의 스크립트 밖 포함 비율 재현. 호출 횟수와 표본 수 혼동 없이 기록했다.
- AUTOTEST/Player/DEV 빌드 오류 0. [AUTOTEST 로그](../../out/engine-hot/build-auto.txt), [Player 로그](../../out/engine-hot/build-player.txt), [DEV 로그](../../out/engine-hot/build-dev.txt). 경고는 남아 있으며 빌드 성공이 런타임 API·복원 성공을 증명하지 않는다.
- `py -3.12 tools/analysis/check-engine-hot-plan.py`: 8개 CFG의 rlNR 동일성, seek 뒤 활성화, FPS 뒤 assert/복원, accepted/복원 누락·복원 오류 거부를 오프라인 검사했다.
- 빌드 DLL의 타입 이름 검사에서 EngineKnob는 AUTOTEST에만 존재하고 Player/DEV에는 없었다. 계획 18개 JSON 파싱과 결과 문서의 모든 상대 링크 대상 존재도 확인했다. `git diff --check` 오류 없음.
- **미확인:** 실제 카메라 수/호출 수, 계층·job 개수, 워커 부하·ETW 대기 시간, 각 API 실제 적용/복원, 픽셀·순서·충돌 동등성, ABBA 이득, 다른 맵/전체 곡 효과.
- Claude의 첫 순서: census → 현재와 다른 worker 수 1개 ABBA → 픽셀/복원 → 다른 맵 재현. occ/sync2d는 no-op인지 먼저 확인. native 분할 패치는 예약/스캔 양쪽 계약 복원 전 실행하지 않는다.

새 제품 최적화 채택은 없다. 이 작업은 실행을 Claude에 맡기는 인계 요청의 오프라인 준비 완료다.
