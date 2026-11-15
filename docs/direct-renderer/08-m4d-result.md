# M4d 구현 및 오프라인 검증 결과

2026-11-15. 작업 폴더 `E:\StutterFixWork\wt-m4`, 브랜치 `claude/drfull`, 시작 HEAD `c4862746249c367ec269b3b19ac19567b52b79a6`.

**M4d의 개별 오브젝트 보존·보조 카메라 합성·명시적 Renderer 재생·그룹 정렬·분류 명령과 실게임 시험 계획을 구현했다. 실게임 픽셀/FPS 기준을 통과했다고 주장하지 않는다.** 커밋, 게임 실행·종료·조작, 설치 DLL 교체를 하지 않았다. 빌드한 native DLL은 이 작업 폴더의 `native/sfnative.dll`이다. 설치 경로는 해시 읽기만 했다.

## M4c의 층/섬 거부 제거

- `DirectFull.Render.cs`는 원래 메인 카메라 마스크 전체를 소유한다. 메인 카메라의 맵 층은 항상 빠지고, 원래 보이던 Renderer를 개별적으로 수집한다. 층 0 등의 SpriteMask와 Flash 같은 추가 층의 Renderer도 같은 목록에 들어간다. `retainedMask=0`이며 Canvas나 SortingGroup 때문에 어떤 층 전체를 되살리지 않는다. 필터와 메인 카메라의 원래 RT·투영은 유지한다.
- `retained Unity masks cannot share native stencil`, `order intersects retained Unity interval` 검사와 retained 구간 예약 자료를 제거했다. SortingGroup이 있다는 이유로 시작을 거부하지 않는다.
- 네이티브로 바인딩할 수 없는 Renderer/재질 슬롯은 그 자리의 `CommandBuffer.DrawRenderer(..., shaderPass=-1)`로 재생한다. 파티클, TMP MeshRenderer, 미지원 셰이더/변형, static batch, 추가 vertex stream, non-simple Sprite, native 바인딩 실패를 사유별로 센다. 한 슬롯의 실패가 뒤의 슬롯/Renderer 분류를 중단하지 않는다. 원래 큐가 opaque인 항목은 `BeforeForwardOpaque`, 투명 항목은 `BeforeForwardAlpha`에 기록한다.
- SortingGroup은 외부 그룹 키 → 중첩 그룹 키 → 내부 Renderer 키로 평탄화한다. 내부 거리 대신 그룹 루트의 외부 거리를 사용하고, 지원하는 Unity에서는 `sortAtRoot`도 읽는다. `DirectFullOrderPath`가 실제 비교에 사용된다. 엔진의 완전 동점 규칙과 실제 중첩 마스크 범위는 여전히 픽셀판 검증 대상이다.

## Canvas 보존과 수명

`DirectFull.Canvas.cs`는 root/override-sorting Canvas를 정렬 단위로 만든다. 중첩 override Canvas의 하위 트리는 별도 단위로 분리한다. Canvas 아래의 일반 Renderer도 명시적 Renderer 경로에 남기며, 보조 카메라에 중복해서 그려지지 않게 한다.

- GameObject/Graphic/Canvas의 활성 상태, CanvasRenderer의 cull·재질 수·상속 알파, Graphic 색 알파, 실제 메시의 정점 수를 확인한다. 메시 bounds의 월드 AABB와 카메라 frustum으로 화면 밖 항목도 제외한다. 아무 Canvas도 그릴 후보가 아니면 보조 `Camera.Render()` 호출은 0회다. 최초부터 0개이면 보조 카메라와 RT를 만들지 않는다.
- 비활성 Canvas는 층을 옮기거나 전체 정렬 구간을 예약하지 않는다. 수명 컴포넌트의 OnEnable/OnDisable/OnDestroy, Graphic의 정점/재질/layout/cull 알림, 관리 쓰기 지점 알림으로 해당 Canvas만 큐에 넣는다. 카메라의 view/projection이 바뀌면 그래픽이 실제 존재하는 후보 Canvas만 가시성을 다시 검사한다. 안정 프레임에 `Resources.FindObjectsOfTypeAll`이나 모든 비활성 Canvas 전수 검사를 하지 않는다.
- 다른 활성 카메라도 사용하지 않는 빈 unnamed 층 두 개를 찾는다. 그릴 Canvas의 개별 GameObject와 자식만 대기 층으로 옮기고, 해당 정렬 구간을 렌더할 때만 보조 카메라 층으로 옮긴다. 일반 네이티브 Renderer의 층을 별도로 바꾸지 않는다.
- 옮길 트리에 Collider/Collider2D, 부모 Rigidbody/Rigidbody2D, Joint/Joint2D가 있으면 층을 옮기지 않는다. 빈 층 두 개를 찾지 못한 경우도 같다. 이때 그 개체는 원래 층에 두고, 보조 렌더 동안 다른 Renderer의 `forceRenderingOff`와 다른 CanvasRenderer의 `cull`만 잠시 보관·변경·원복해 격리한다. 메인 카메라에서 층 전체를 보존하는 우회는 없다. 이 예외 경로는 공유 층의 Renderer 수에 비례한 비용이 들 수 있다.
- 실제 층 기록은 생산 `DirectFullObjectLayers<T>` 저널이 보관한다. 반복 이동으로 원래 층을 덮어쓰지 않고, 부분 이동 실패와 파괴된 개체를 처리한다. off, 기존 재시작/맵 전환/편집 복귀/Unload/활성화 실패 경로에서 층·Canvas.worldCamera·임시 force/cull 상태·메인 마스크·보조 리소스를 반환한다.
- 새로운 계층/Renderer/Canvas의 생성·파괴·부모 변경은 다음 메인 PreCull에서 내용을 다시 수집한다. 기존 Harmony 관찰 후크를 다시 설치하지 않는다. 보통의 재질·정점 변경은 Unity가 보조 렌더에서 그대로 읽으므로 전체 Renderer 재수집을 하지 않는다.

## 합성 방식과 수학적 범위

인접한 Canvas 정렬 단위는 하나의 run으로 묶는다. 중간에 Renderer나 SpriteMask 명령이 있으면 run을 나눈다. 보조 카메라 한 대가 메인 PreCull 안에서 run마다 두 번 렌더한다. 위치·view·projection·viewport·RT 크기는 메인과 같고, ScreenSpaceCamera Canvas의 worldCamera만 호출 동안 바꾼 뒤 즉시 복구한다. 필터 컴포넌트를 보조 카메라에 복제하지 않는다.

일반 `Blend SrcAlpha OneMinusSrcAlpha`는 투명 RT의 알파에 `a²`를 누적한다. 따라서 그 RT의 알파를 coverage로 취급해 단순히 `One/OneMinusSrcAlpha` 합성하는 방식은 원본과 다르다. 이번 구현은 **검정과 흰 배경의 ARGBFloat RT 두 개**를 사용한다.

```
K = 검정 배경에 그린 결과
W = 흰 배경에 그린 결과
T = W - K
최종 결과 = K + 원래 camRT * T     // RGBA 각각
```

native의 새 `Composite` opcode가 full-screen triangle과 D3D11 dual-source blend(`One`, `Src1Color/Src1Alpha`)로 이 식을 적용한다. 기본 SrcAlpha 방식의 alpha-squared 누적과 선곱 알파 방식 모두 처리한다. 보조 RT의 typeless D3D11 포맷도 명시적 FLOAT SRV로 받는다. 기존 ABI 구조체와 export는 바꾸지 않았다.

이 식은 fragment 출력이 배경에 의존하지 않는 source-over 계열에서 성립한다. UI/Default 계열과 명시한 TMP 계열을 허용하고, 모르는 Canvas 셰이더는 `unverified-canvas-blend:<shader>`를 기록한다. 전체 분류와 on 자체는 진행하지만 `uncertainCanvas != 0`이면 `assert on`이 실패하여 벤치 성공으로 집계되지 않는다. framebuffer 읽기/비선형 custom Canvas를 검증된 합성으로 표시하지 않는다.

**8비트 중간 반올림까지 동일하다는 뜻은 아니다.** 원래 camRT에 매 객체를 그릴 때의 RGBA8 양자화와, FLOAT 보조 RT를 거쳐 마지막에 양자화하는 과정은 다르다. 아래 fixture에서는 최대 채널 2의 차이가 생겼다. Canvas가 있는 실제 화면에서 다른 픽셀 비율 < 0.1%를 만족하는지는 별도 픽셀판으로 판정해야 한다. 한 run의 두 FLOAT 컬러 RT는 3440×1440에서 약 151.2 MiB이며 두 depth 버퍼 비용도 추가된다. run 수가 많으면 비용이 크다. 기존 run의 RT는 재계획 때 재사용하고 off에서 해제한다. `auxRT`는 보유 수, `auxActiveRT`는 현재 사용하는 수다.

## 마스크 공유

네이티브 마스크와 명시적 Unity Renderer 재생 사이에 camRT의 depth/stencil을 바꾸지 않는다. 네이티브 event는 Unity의 현재 RTV/DSV를 받아 사용하고, command-list 실행 후 기존 D3D 상태를 반환한다. 시작에서 stencil을 0으로 초기화하고 다음 규칙을 사용한다.

| 종류 | Ref | Read / Write mask | 비교 | 통과 연산 |
|---|---:|---|---|---|
| SpriteMask 범위 시작 | 0 | 255 / 255 | Always | IncrementSaturate |
| SpriteMask 범위 끝 | 0 | 255 / 255 | Always | DecrementSaturate |
| VisibleInsideMask 네이티브 Sprite | 0 | 255 / 0 | NotEqual | Keep |
| VisibleOutsideMask 네이티브 Sprite | 0 | 255 / 0 | Equal | Keep |

기존 추출물 `out/direct-renderer/m4bc/shaders/bindings.json`의 **Sprites/Default와 Sprites/Mask 자체의 저장된 stencil 상태는 Ref=0, Comp=Always, Pass=Keep**다. SpriteMask interaction의 실제 변경은 Unity 렌더러 쪽에서 주입된다. DXBC에는 D3D pipeline stencil state가 들어 있지 않으므로, 저장된 셰이더만 읽어서 그 런타임 주입까지 입증했다고 할 수 없다. 이번 WARP는 위 프로토콜의 native 증감과 독립적인 Unity 방식 대상 비교가 같은 DSV에서 맞물리는 것을 검증한다. **실제 게임의 DrawRenderer가 주입하는 상태와 모든 마스크 조합의 일치는 미측정**이며 4맵 픽셀판이 필수다. 이 제한을 새 거부 예외로 되돌리지는 않았다.

## `drfull plan`, census와 state

`drfull plan`은 렌더러를 켜거나 층·카메라 마스크·렌더러 상태를 바꾸지 않는다. 모든 Renderer의 재질 슬롯과 Canvas/CanvasRenderer를 분류하고 사유별 수를 로그에 남긴다. 전체 표는 모드 폴더의 `drfull-plan-<UTC 시각>.tsv`다. Shader variant/mesh signature의 사전 분류를 하되 D3D 리소스 생성은 on 시점에 한다. 실제 바인딩 실패는 그때 개별 DrawRenderer 사유에 추가된다.

`drfull census`에도 Canvas 요약을 추가했다. `canvasBounds`, `canvasActive`, `canvasInactive`, `canvasGraphics`, `graphicHierarchyActive/Inactive`, `liveCanvas`, `liveGraphics`, `nonDrawingGraphics`, `culled`, `zeroAlpha`, `emptyMesh`, `offscreen`을 구분한다. 따라서 HELLO의 과거 `canvasBounds=14923`이 몇 개의 활성 Canvas와 실제 그래픽 후보로 나뉘는지 다음 분류판에서 숫자로 확인할 수 있다. **여기서는 게임을 실행하지 않았으므로 HELLO의 실제 활성/비활성 개수는 측정하지 않았다.** 가시성 수는 GPU 완료 draw 수가 아닌 후보 수다.

state는 `mode=M4d-original`이며 기존 native/Unity explicit 수와 함께 다음을 기록한다.

- `replans`, `rescans`, `commit_max_us`, `replan_max_us`, `main_us`, `main_max_us`.
- Canvas 전체·활성·그래픽·잠재 그래픽·현재 화면 그래픽 수.
- `auxRT`, `auxActiveRT`, `auxRuns`, `auxDraws`, `aux_us`, `aux_max_us`, `uncertainCanvas`.

main 시간에는 통보에 따른 전체 재수집도 포함한다. commit은 native packet 제출의 관리 wall time, replan은 순서/Canvas 재계획 및 전체 재수집의 최대 wall time, aux는 `Camera.Render()` 호출 구간의 wall time이다. GPU busy나 Present 대기 시간으로 해석하거나 겹치는 구간을 더하지 않는다. 실게임의 할당 0, main < 0.3ms, 곡 중 commit/replan < 2ms 목표는 미측정이다.

## 로컬 검증

증거 폴더: [`out/direct-renderer/m4d`](../../out/direct-renderer/m4d).

| 검사 | 결과 |
|---|---|
| native / DEV / Player / PlayerAuto | 4종 빌드 오류 0 |
| 기존 export 이름·ordinal·hint | `sf_z_drfull_v1` 포함 **74/74 보존** |
| 관리 DLL에 내장된 native | 3/3 최신 산출물과 일치 |
| 원래 DXBC 리소스 / 일반 Player 경계 | DEV·PlayerAuto 포함, 일반 Player 제외 |
| 생산 셰이더 로더/ABI | 21 shaders, 1,012 variants, 2,024 stages, 6,594 variables, 2,407 textures roundtrip |
| 기존 원래 DXBC WARP | 48/48, 최대 채널 1, 다른 픽셀 합계 28, 추가 control 5종 |
| 생산 합성 WARP | **24/24**, FLOAT 최대 오차 **1.19209e-7**, typeless 보조 RT 포함 |
| RGBA8 양자화 대조 | 최대 채널 **2**, 다른 픽셀 합계 **24,642 / 73,728**. 이 fixture 자체는 < 0.1% 비율을 만족하지 않음 |
| 잘못된 단순 알파 합성 음성 대조 | 3/3 오류 검출 |
| 공유 DSV의 native 마스크 ↔ 독립 대상 | 9/9. 초기 inside/outside, 2중 증가, 1회 감소 뒤 유지, 마지막 감소 뒤 복구 |
| 생산 개별 층 저널·중첩 그룹 순서 | **20,115 검사 통과**. 부분 실패, 파괴, 반복 복원, 부호 비트 포함 |
| 저널의 안정 이동/복원 할당 | 10,000회에서 관리 **0바이트**. Unity 엔진 전체 프레임의 측정은 아님 |
| 기존 생산 카메라 lease 수명 | 25종 + 임의 마스크 10,000개, 안정 검사 할당 0 |
| 픽셀 기준 판정/기존 캡처 정책 | metrics 19 + masks 15 + state 15 + controls 17 검사 통과 |
| 계획 JSON 감사 | 분류 4 + 픽셀 4 + ABBA 16 = **24개**, manifest 별도 |
| 설치 DLL 관찰 | 시작/종료 SHA-256 2/2 일치. 설치·복원 쓰기 없음 |

재현 명령은 게임을 실행하거나 설치하지 않는다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/direct-renderer/check-m4d.ps1
py -3.12 tools/direct-renderer/plan-m4c.py
py -3.12 tools/direct-renderer/check-m4d-artifacts.py out/direct-renderer/m4d
```

`check-m4d.ps1`은 로컬 빌드와 소프트웨어 WARP를 BelowNormal 우선순위로 수행한다. 게임 프로세스나 공유 측정 lock을 조작하지 않는다. 이것이 실행되는 동안 별도의 성능 측정을 병행하면 그 성능 표본은 여기서 유효하다고 보장하지 않는다.

## Claude가 실행할 계획

**다음 JSON은 생성·감사만 했고 실행하지 않았다.** 기존 bench의 `ppGpuCam`, `BENCH_WT=wt-m4`, RT 3440×1440, GpuDeco=0, RunLod=false, dr2 off, fpscap=0을 사용한다. seek/waitsongpos 및 12초 fpsmark 구간 생성 규칙을 유지한다. 분류판과 픽셀판에는 FPS 구간을 만들지 않는다.

기준 manifest: [`out/direct-renderer/m4d/plans/manifest.json`](../../out/direct-renderer/m4d/plans/manifest.json).

| 맵·곡 시각 | 분류판 | 픽셀판 | ABBA 파일 접두사 |
|---|---|---|---|
| HELLO 50초 | `m4d-hl50-plan.json` | `m4d-hl50-pixel.json` | `m4d-hl50-{A1,B1,B2,A2}.json` |
| Windflower 258초 | `m4d-wf-plan.json` | `m4d-wf-pixel.json` | `m4d-wf-{A1,B1,B2,A2}.json` |
| DD 50초 | `m4d-dd-plan.json` | `m4d-dd-pixel.json` | `m4d-dd-{A1,B1,B2,A2}.json` |
| Exit(sh) 88초 | `m4d-sh-plan.json` | `m4d-sh-pixel.json` | `m4d-sh-{A1,B1,B2,A2}.json` |

위 파일은 모두 `E:\StutterFixWork\wt-m4\out\direct-renderer\m4d\plans\` 아래에 있다. 예를 들어 Claude의 기존 실행기에 넘기는 경로는 다음과 같다. 이 문서 작성 과정에서는 실행하지 않았다.

```powershell
# E:\StutterFixWork 에서, 설치/동시 플레이/측정 잠금은 Claude가 관리
py -3.12 run_locked.py E:/StutterFixWork/wt-m4/out/direct-renderer/m4d/plans/m4d-hl50-plan.json wt-m4 m4d-hl50-plan --keys "[drfull],[framecmp],[presentpath,단계 실패,오류로,FPS 구간" --tail 4
```

순서는 맵마다 분류판 → 픽셀판 → 새 실행 A1→B1→B2→A2다. 분류판은 `drfull plan`, `drfull census`, `assert off`를 수행한다. 픽셀판은 pause → on d1 → off d0 → off d00 순으로 캡처한다. `framecmp assertlimit d0 d1 0.1 3 png`는 다른 픽셀 비율 **엄격히 < 0.1%**, RGBA 최대 차이 **≤ 3**을 검사한다. `framecmp assertzero d0 d00`은 off/off 픽셀과 캡처 상태가 모두 0이어야 통과한다. 후보와 원본의 카메라 마스크가 의도적으로 다르므로 후보 비교에 엄격한 상태 동일성 검사를 잘못 적용하지 않는다.

ABBA의 B는 `drfull on`, A는 원래 Unity다. 측정 전후 `assert on/off`, `state`, `cpuwindow begin/end`를 확인하고 종료 전에 off를 반환한다. `uncertainCanvas`, native 오류/포화/정체, 중도 자동 off, 픽셀 gate 실패, 복원 불일치가 있으면 FPS를 유효 후보 결과로 집계하지 않는다.

추가 실게임 확인 대상은 재시작·맵 전환·편집 복귀·Unload의 정확 복원, 이후 활성화되는 Canvas와 새 계층, 카메라 이동 시 출입, 실제 SpriteMask/DrawRenderer interaction, nested SortingGroup, 필터·UI·물리/히트박스다. 실게임 픽셀 일치, 압도적 FPS 이득, 전체 곡 안전성은 이 오프라인 결과로 확정하지 않는다.

정렬 정책 참고: [Unity Sorting Groups](https://docs.unity3d.com/kr/current/ScriptReference/Rendering.SortingGroup.html), [Unity 2D Sorting](https://docs.unity3d.com/cn/2018.3/Manual/2DSorting.html). 이 링크는 그룹의 외부/내부 정렬 구조 참고이며 설치 게임의 실제 stencil 주입을 입증하는 자료가 아니다.
