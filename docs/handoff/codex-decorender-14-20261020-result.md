# DecoRender S8 결과 — 안전 검사를 이벤트와 분할 검사로 변경

2026-10-20. 작업 경로 `E:\StutterFixWork\wt-decorender2`. 소스 수정과 로컬 검증 완료. 커밋·게임 실행·설치는 하지 않았다. 실험은 `DEV || AUTOTEST` 전용이며 기본 꺼짐이다.

**검사 합 ≤0.3ms, DecoRender 총합 ≤0.5ms, 끔 이상 fuse 프레임 수, compare 최대 ≤2/255는 게임 미실행으로 미검증**이다. 인계 문서의 S7 수치를 새 코드 측정값으로 재사용하지 않았다.

## 원인과 변경

| 대상 | 확인한 반복 비용 | 변경 |
|---|---|---|
| PruneChanged | 움직임 work마다 소유자·sprite·material·sorting·Unity layer·mask·SortingGroup·draw mode·repeat와 전체 유니폼 검사 | 움직임과 구조 dirty 분리. 구조 이벤트 대상 검사 + 프레임당 ceil(장식 수/128) 순환 안전망. 검사 목록 재사용 |
| UniformsUnchanged | work마다 shader/queue/keywords/MPB 및 선언 속성 전체 읽기 | on/레이아웃 생성 때 스냅샷, 구조 이벤트 때 검증, 정기 검사 최대4개/프레임. 공통 ID 정적 초기화, 선언 속성 ID는 스냅샷 저장. Uniforms/MaterialState 문자열 속성 API 제거. Borrow도 MainTexId 사용 |
| AuditLateBarriers | 일반 대상 약1,600개마다 enabled/active/force-off와 활성 상태의 정렬·재질·거리 읽기. bool마다 생존 검사와 gameObject 취득 | 일반 대상 ceil(수/8) + 전체 장벽 ceil(전체 수/128) + 이벤트 대상의 합집합. GameObject 저장, 생존 판정 공유. 초기 비활성/장식 소유 장벽도 느린 안전망에 포함 |
| Takeover | PreCull/PreRender에서 같은 VisibleItems 중복 순회. 카메라 뒤 force-off와 빌린 재질 복구 | 이미 적용했고 fallback 키가 늘지 않았으면 중복 Takeover 생략. PostRender 복구 유지. Hidden/Borrowed를 native 생존 검사보다 먼저 확인 |
| Build | resident Dictionary, Unity 생존 검사, enabled/gameObject/active/force-off, 정렬 Dictionary와 native 거리 읽기 | Resident/가시성/GameObject/정렬 키·기준 거리 저장. 상태는 이벤트/안전망에서 갱신. 정렬 Dictionary 제거. 포즈·색·flip이 달라질 때만 기존 native SortDistance 사용. 재계획 때 기준 거리 갱신 |

28,835개가 모두 남으면 정기 구조 검사는 최대226개/프레임, 유니폼은 최대4개다. 일반 장벽1,600개면 빠른 검사200개에 전체 장벽의1/128 검사가 추가된다. 호출 수 제한이며 시간 측정값은 아니다. 대량 이벤트의 dirty 처리는 제한하지 않는다.

색/이동 Notify는 geometry를 깨우되 장벽 소유자를 다시 검사하지 않는다. 구조·가시성 이벤트만 소유자 검사를 예약한다. EnableMeshRenderer는 UpdateShader에서 반복 호출되므로 실제 상태 전환만 구조 변경으로 처리한다. 기존 Dormancy의 ObserveMesh/NotifySetup 경로도 유지한다.

## IL 스캔과 새 훅

설치된 게임의 Assembly-CSharp.dll을 tools/ilscan으로 읽었다. 로그: `bin/decorender14-ilscan-{visual,callers}.log`.

- SetSprite 두 오버로드: sprite, point sampling 재질, 텍스처 wrap 변경.
- scrVisualDecoration.SetDepth: sorting layer/order와 GameObject layer 변경. 기반 scrDecoration.SetDepth만으로 override 경로를 설명할 수 없다.
- SetMaskingDepth 두 오버로드, SetMaskingType, SetMaskingTarget: 마스크 변경.
- SetTile: RepeatX/Y 재질 변경. SetBlendMode: material/shader 교체. sharedMaterial setter 외에 material setter도 확인했다.
- SetVisible: sprite renderer enabled 변경. EnableMeshRenderer: 자식 GameObject 활성화.

세션 전용 Harmony ID: `StutterFix.DecoRender.Safety`. 등록: DecoRender.cs 초기화 → DecoRender.Safety.cs:InstallChangeHooks. 선언된 오버로드 전체를 찾아 postfix를 등록한다.

| 게임 함수 | 새 postfix — DecoRender.Safety.cs |
|---|---|
| SetSprite ×2, SetDepth, SetMaskingDepth ×2, SetMaskingType, SetMaskingTarget, SetTile, SetBlendMode | StructurePost |
| EnableMeshRenderer | MeshChangePost → 실제 전환 때 StructurePost/ObserveMesh |
| SetVisible | VisibilityPost → Notify + 가시성 캐시 갱신 |

현재 DLL의 예상 postfix 수는11개다. 실제 적용 수는 state의 hooks로 확인한다. fixture의 Harmony는 가짜이므로 게임에서의 패치 적용 성공을 증명하지 않는다.

TileCull.cs:SetRenderEnabled에서 NotifyRenderer를 직접 호출한다. SetEnabled/Cull/Uncull/StopAll 및 비교용 임시 enabled 전환이 이 함수를 쓴다. 추가 Harmony 패치는 없으며 알림 호출만 DEV/AUTOTEST 안에 있다. off에서는 세션이 없어 즉시 반환한다.

해제: decorender off/씬 전환/실패 → DecoRender.Stop → RemoveChangeHooks → 해당 ID UnpatchAll과 이벤트 인덱스 정리. 모드 언로드도 기존 Main.Unload의 UninstallAll → Try(DecoRender.Stop)으로 같은 해제를 수행한다. 원본 forceRenderingOff, 빌린 재질 텍스처, 카메라 이벤트, 버퍼 및 소유 메시/아틀라스 복구·해제를 유지했다.

## 계측과 늦게 발견한 변화

- PruneChanged+budget/subset-dirty: 이벤트와 분할 검사 전체. dirty+prune의 부분집합.
- UniformsUnchanged/subset-prune: 유니폼/텍스처 sampler 검사. 예외 경로도 finally로 포함.
- audit/AuditLateBarriers, barrier-poll/subset-audit, owner-events/subset-audit.
- Takeover/draw-on/callback, draw-off/callback: 적용/복구 비용.
- checks+takeover+restore/completed-frame: prune+audit+프레임 내 모든 카메라 적용/복구 합계. 검사 합 ≤0.3ms 판정용.
- callback-orchestration/outside-prepare: PreCull/PreRender의 ValidSession 등 나머지 비용. frame CPU에 포함하며 PrepareFrame과 중복 계산하지 않음.
- build+draw/completed-frame: PrepareFrame+적용/복구+콜백 나머지. 진행 중 마지막 프레임은 다음 PrepareFrame에서 확정.
- safety: 구조 이벤트 수, 정기 구조/유니폼 검사 수, 훅 수. barrier-poll은 중복 제거한 정기 대상 수, barrier-event는 추가 이벤트 대상 수. barrier-slow-population은 빠른 목록 밖 대상 수.
- late-changes={structure,visibility,barrier}: 이벤트가 아닌 안전망에서 발견한 변경 수. stats reset으로 지우지 않는 정확성 누계. 지연 프레임 수/고유 장식 수를 의미하지 않음.

PrepareFrame의 단계 합산식은 유지했다. audit/dirty/layout/collect/change/schedule/wait/upload/topology/other는 상위 단계이며 unaccounted-ms로 남은 차이를 표시한다. prune/uniform/camera/borrow 등의 subset을 다시 더하지 않는다. draw는 카메라별 호출 수가 달라 평균을 단순 합하지 않고 completed-frame을 본다.

Capture/Notify와 Harmony 이벤트 진입의 외부 호출 비용까지 completed-frame에 전부 포함되지는 않는다. 전체 DecoRender ≤0.5ms는 fuse와 profsamp도 함께 확인한다. 새 코드의 실제 unaccounted≈0도 게임 미검증이다.

## 오프라인 검증

- DEV/Player/AUTOTEST: 모두 오류0. 경고 각각39/36/39개. 참조 버전 충돌·기존 obsolete/미사용 경고 포함.
- DecoRenderCheck: **505개 검사, 실패0**. 실제 Incremental/Topology/Safety 소스를 링크한다. 기존104개에1,280개 장식의128프레임 순환·프레임당10+4 예산·중복 카메라 생략·구조 이벤트 우선 처리·가시성 누락 계수/복귀·장벽8프레임 순환·이벤트 추가·off 인덱스 정리를 추가했다.
- fixture의 Unity/Jobs/Harmony와 PruneChanged는 가짜다. 실제 구조 검사 본체·native 비용·패치 설치·픽셀·FPS를 검증한 것은 아니다.
- DLL 경계: DEV/AUTOTEST에는 DecoRender·VerticesJob 존재. Player에는 해당 타입·참조 없음.
- LoadCheck DEV: FieldRefAccess 이름268곳, 틀림0, 동적 이름5곳은 게임 확인 대상.
- git diff --check 통과. 게임 실행·설치·커밋 없음.

로그: `bin/decorender14-{dev,player,autotest}-build.log`, `bin/decorender14-fixture-build.log`, `bin/decorender14-boundary.log`, `bin/decorender14-loadcheck.log`.

DLL: `bin/Debug/StutterFix.dll`, `bin/Player/StutterFix.dll`, `bin/PlayerAuto/StutterFix.dll`. 설치하지 않았다.

## 게임에서 돌릴 순서

Arche의 같은 구간262~270초, 같은 해상도/설정/시작 조건을 사용한다. 끔/켬/끔 fuse 길이를 일치시킨다. 인계의6초209/약760프레임은 같은 길이로 재측정하기 전 참고값이다.

1. 정확성 재생: `decorender on 0 scope=all atlas=128` → `decorender state`. hooks=11, full-rebuilds=1, selected/managed/groups, slow population 기록.
2. `decorender compare` ×8 이상. sprite/depth/material/repeat/mask/mesh, TileCull 활성화, 잠듦/깨움 전후에 분산. 모든 유효 결과 max≤2/255·snapshotStable=True 확인. 이상치와 late-changes 증가 시점 기록. 검사/PNG 구간은 성능 창에서 제외.
3. `decorender off`. 다시 on/off와 모드 재로드 후 별도 ID 패치·카메라 콜백·원본 renderer/재질이 남지 않는지 확인.
4. 별도 성능 재생: `decorender on 0 scope=all atlas=128` → `decorender verify off` → 준비 뒤 `decorender stats reset`.
5. 262초 `fuse begin`, 270초 `fuse end`. 창 안에서 compare/state/PNG 저장 금지. end 뒤 `decorender state` 저장.
6. 같은 길이 끔/켬/끔 반복. checks+takeover+restore 평균≤0.3ms, 전체 비용≤0.5ms 및 fuse 프레임 수가 끔 이상인지 확인. managed/retired-keys 전후를 기록해 대부분 원본으로 복귀해서 빨라 보이는 경우를 구분.
7. 별도 표본 프로파일 재생: `profsamp begin 1000` → 동일 창 → `profsamp end` 또는 `profsamp end frames`. PruneChanged/AuditLateBarriers/UniformsUnchanged/Takeover/Build와 새 훅·Capture 비용 확인. 프로파일러를 켠 판과 끈 fuse 판을 구분.
8. 마지막 `decorender off`.

안정된 목록에서 정기 구조/비활성 장벽 검사는 약128프레임, 일반 장벽은 약8프레임, 직접 유니폼 변경은 ceil(관리 장식 수/4)프레임까지 늦을 수 있다. 28,835개면 유니폼 안전망 한 바퀴는 약7,209프레임이다. 게임 함수 경로 변경은 이벤트 검사로 앞당긴다.

외부 코드의 훅을 우회한 직접 변경·새 renderer 생성·부모 활성화는 발견 전에 화면 차이를 낼 수 있다. 발견 시 영향 키를 원본으로 복귀시키지만 발견 전 픽셀 일치까지 보장하지 않는다. compare와 변경 훅 게임 시험 전에는 정확성/성능 목표 달성을 선언하지 않는다.
