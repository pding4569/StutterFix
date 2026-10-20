# DecoRender S5 결과 — 2026-10-20

## 수행 범위

`E:\StutterFixWork\wt-decorender2`에서 코드 수정과 로컬 빌드·회귀 검사만 수행했다. 커밋·게임 실행·설치하지 않았다. 변경은 기존 `#if DEV || AUTOTEST` 경계 안에 있다. 게임의 12초 프레임 수, 평균 0.5ms 이하, compare 최대 ≤2/255는 **미측정**이다.

## 구현 및 원인 분석

- `IndexBarriers`: 장식 소유 렌더러는 전부 이벤트 전용으로 분리했다. 활성 장식 자식도 매 프레임 폴링하지 않는다. 비장식은 색인 시 `activeInHierarchy=true`인 객체만 폴링 목록에 넣는다. 이 목록에는 현재 enabled/force-off 때문에 기다리는 타일 메시도 포함한다. 그 밖의 비활성 waiting 객체는 한 번 분류하고 이후 폴링하지 않는다. 실제 약 1,600개로 줄었는지는 `barrier-poll`로 확인해야 한다. 숫자를 임의로 잘라 정확성을 희생하는 상한은 넣지 않았다.
- `AuditLateBarriers`: 첫 카메라의 `PrepareFrame`에서만 호출하고 `auditFrame`으로 중복도 차단한다. 폴링 대상의 enabled/forceRenderingOff/activeInHierarchy를 읽으며, 그리는 대상에만 재질·정렬 거리 검사를 수행한다. waiting의 이동은 재구성을 유발하지 않는다. 스냅샷은 검사 후 갱신한다. 그리기 전환이나 그리는 장벽의 정렬 변화는 해당 정렬키를 무효화한다.
- `Dormancy.WakePost`: `EnableMeshRenderer` 등을 기존에 받던 위치에서 `meshRendererEnabled` 현재값을 `ObserveMesh`에 전달한다. 값 전환만 topology를 무효화한다. 비활성 자식 검사는 통지받은 소유자에 한해서 수행한다. `IsDormant`에서 이미 읽은 값의 전달도 유지한다.
- 기존 코드는 비장식의 비활성 waiting까지 감시 목록에 포함했고, 그리지 않는 장벽도 거리 변화로 topology를 무효화했다. 또한 재구성 때마다 `ResetLayout`으로 모든 `Written=false`가 되어 다음 빌드가 모든 항목을 다시 업로드했다. 이것이 `changed-items`가 전체 개수에 가까워질 수 있는 코드 경로다. **이전 게임의 27,447개가 실제로 이 경로 때문이었다는 실행 증거는 없으므로 원인 확정은 하지 않는다.**
- `RebuildRuns`는 구간 구성이 실제 달라졌는지 반환한다. 구성이 같으면 레이아웃을 초기화하지 않는다. 구성이 달라져도 유지 구간의 정점 출력·Written·보임 상태를 보존하고 새 구간 구성원만 재기록한다. 기존 구간 재사용 판단으로 영향 없는 구간의 메시 재생성/재업로드를 피한다. 변화가 발생한 topology 경로의 전역 scene 발견·순서 계획은 여전히 남아 있으며, 이 비용은 `topology`에 표시한다.
- 직교 정렬 거리에서 카메라 위치의 공통 감산항을 제거했다. 카메라 평행 이동만으로 모든 장벽의 스냅샷 거리가 달라지는 것을 막는다. 원근 거리 계산은 유지한다.
- 매 프레임 모든 구간의 첫 장식을 작업 집합에 넣던 경로를 제거했다. 정상 프레임은 Dormancy의 현재 awake와 직전 awake, 변경 통지를 처리한다. 직전 awake는 잠드는 순간 정점을 숨기는 데 필요하다. 초기/구조 변경 때의 준비 순회는 별도다.
- dirty는 실제 matrix/color/flip 비트값 차이 또는 visibility 전환, 최초 미기록일 때만 발생한다. 단순 Notify/Capture 호출만으로 dirty가 되지 않는다. 오차 허용 비교로 바꾸지 않았다. 처음 10빌드에 frame/changed/awake/unwritten/pose/visibility/layout-resets를 로그로 남기며 이후 평균은 state에 유지한다. `stats reset`으로 이 로그와 평균을 다시 시작할 수 있다.

## 단계별 계측

`Stopwatch.GetTimestamp()`/`Stopwatch.Frequency`를 사용한다. `state`의 `stage-ms/window2048` 표 각 행은 mean/p99/max/n/calls다. n은 최근 최대 2,048개, calls는 reset 이후 해당 단계 기록 횟수다. 0건 작업인 프레임도 단계 진입 1회로 기록한다.

| 행 | 포함 비용 |
|---|---|
| audit | 제한된 장벽 폴링, 소유자 이벤트 처리 |
| dirty+prune | awake/통지 작업 집합, sprite/material/MPB 등 안전성 검사 |
| change-compare | 이전 값 대조, dirty 판정, 보임 집합 및 geometry 입력 갱신 |
| layout | 슬롯·SoA·NativeArray 준비, 유지 구간 출력 보존 |
| collect | 보임 값, transform/SoA, color/flip 수집 |
| job-schedule | 병렬 job Schedule |
| job-wait | Complete 대기 |
| upload+bounds | 범위 정렬·병합, 정점 업로드, 구간 bounds |
| topology | scene 재발견·순서 계획·변경 구간 메시 생성/이전 구간 해제 |
| other | 카메라 확인, 대여 재질 복원, 가시 구간 통계, dirty 로그 |
| unaccounted-ms | 빌드 total에서 위의 서로 겹치지 않는 단계 합을 뺀 잔여 |
| draw-on/callback | 카메라별 Takeover, 원본 숨김, 구간 켬 |
| draw-off/callback | PostRender의 재질/원본 복구, 구간 끔 |
| build+draw/completed-frame | 한 프레임 build와 카메라별 draw-on/off CPU 합 |

`unaccounted-ms`에 draw-on/off를 빼지 않는다. 이 콜백들은 PrepareFrame 빌드 밖에서 실행된다. 대신 `build+draw/completed-frame`이 카메라 여러 개의 비용을 합산한다. 다음 프레임 첫 카메라에서 직전 프레임을 확정하므로 마지막 진행 프레임은 표본에 포함되지 않는다. GPU 시간이나 전체 게임 프레임 시간은 아니다. 수집 루프의 값 비교는 별도 QPC 구간으로 분리했으며 계측 자체에도 비용이 있다.

기존 `build-cpu-ms/window2048`의 total/collect/job+wait/upload/topology도 유지한다. 세부 표와 중복되는 요약 행을 다시 더하지 않는다. 합산 검증에는 세부 표의 첫 10단계만 사용한다. p99/최대끼리는 같은 프레임의 값이 아닐 수 있어 합산하지 않는다.

## 검증 결과

| 검사 | 결과 |
|---|---|
| DEV `dotnet build -v q --nologo` | 오류 0, 경고 39 |
| Player `dotnet build -p:Edition=Player -v q --nologo` | 오류 0, 경고 4 (증분 빌드) |
| AUTOTEST `dotnet build -p:Edition=Player -p:AutoTestBuild=1 -v q --nologo` | 오류 0, 경고 39 |
| `tools/DecoRenderCheck` | 프로덕션 collector/job/upload/audit를 링크한 제어 흐름 검사 66개, 실패 0 |
| DLL 경계 | DEV/AUTOTEST에 DecoRender/VerticesJob 존재, Player에는 타입과 DecoRender 참조 없음 |
| `tools/LoadCheck` DEV | FieldRefAccess 268곳, 틀림 0, 동적 이름 5곳은 실행 중 확인 대상 |
| `git diff --check` | 통과 |

회귀 검사는 안정 프레임 0 업로드, 잠든 장식 미방문, 같은 값 캡처/통지의 0 dirty, 잠듦/깨움, 새 구간 추가 시 유지 구간 보존(float/byte), 감사 프레임당 1회, 소유자 이벤트 전용 자식 검사, waiting 이동 무효화 방지, drawing 전환 및 이동 감지, 통계 최대/호출수/reset을 포함한다. 가짜 Unity/Jobs이므로 실제 네이티브 실행 시간이나 GPU 픽셀 증거는 아니다.

로그: `bin/decorender11-{dev,player,autotest}-build.log`, `bin/decorender11-fixture.log`, `bin/decorender11-loadcheck.log`. 산출 DLL은 `bin/Debug/StutterFix.dll`, `bin/Player/StutterFix.dll`, `bin/PlayerAuto/StutterFix.dll`이며 설치하지 않았다.

## 게임에서 실행할 순서

정확성과 성능은 별도 재생으로 확인한다. 같은 해상도·카메라·재생 설정으로 Arche 250초 부근에서 시작한다.

1. `decorender on 0 scope=all atlas=128`
2. `decorender state` — barrier-poll, barrier-event, barrier-ignored, changed-items, layout-resets 확인.
3. 정확성 판에서 `decorender compare` × 8회 이상. 다른 프레임과 보임/mesh 전환을 포함하고 각 유효 결과의 최대 ≤2/255를 확인한다. 무효/실패 결과를 통과로 계산하지 않는다.
4. `decorender off` 후 별도 성능 판 재생. 같은 on 명령으로 초기 준비를 끝낸다. `decorender verify off`.
5. 264초 직전 `decorender stats reset`. 264초 `fuse begin`, 276초 `fuse end`. 창 안에서 compare/PNG 저장/state 출력을 하지 않는다.
6. end 뒤 `decorender state`. 12초 프레임 수, 빌드 평균, 단계별 평균/p99/최대, unaccounted-ms, build+draw, dirty 평균/awake 평균, layout-resets와 barrier-poll을 기록한다. 처음 10프레임 dirty 로그도 함께 확인한다.
7. 동일 구간 끔/켬/끔으로 반복하고 마지막에 `decorender off`.

목표는 12초에 수백 프레임 이상, build 평균 ≤0.5ms, 초기 구성을 제외한 changed 약 1,000~2,000, compare 최대 ≤2/255다. 모두 현재 미측정이다. 미달 시 가장 큰 단계와 unaccounted를 함께 보고한다. 특히 topology/layout 반복, awake가 전체 목록으로 반환되는 상태, collect/prune의 네이티브 읽기, 다중 카메라 draw-on/off가 다음 측정에서 확인할 후보 비용이다. 기존 1,029.7ms/71.6ms/2.59ms/11.18ms 수치는 이전 빌드 결과이며 이번 수정의 성능으로 인용하지 않는다.

색인 때 비활성이었던 비장식 객체의 직접 활성화, Dormancy 알림을 우회한 장식 자식 직접 변경, 새 외부 Renderer 생성은 전수 폴링으로 즉시 발견하지 않는다. 이번 감시 축소 범위에 따른 제한이며 해당 변화 시나리오의 compare가 필요하다. 게임 금지 조건을 지켰으므로 새로운 화면 동등성이나 성능 달성을 주장하지 않는다.
