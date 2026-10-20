# decorender S4 구현 결과 — 2026-10-20

지정 worktree `E:\StutterFixWork\wt-decorender2`에서 구현했다. 게임 실행·설치·커밋은 하지 않았다. 새 렌더 코드와 기존 코드의 추가 훅은 모두 `DEV || AUTOTEST`에 한정한다.

**구현·오프라인 검증 완료, 실게임 성능/화면 관문은 미검증이다.** Arche 264~276초의 켬/끔 프레임 수, `fuse`, 빌드 평균/p99, `compare` 최대 ≤2/255를 이번 작업에서 측정했다고 해석하면 안 된다. 인계 문서의 1,003ms/82FPS/199FPS와 8회 compare 수치는 이전 구현의 참고값이다.

## 구현

- 매 프레임 전체 장식과 전체 Renderer를 다시 조사하던 `PruneChanged`/`RebuildRuns` 호출을 없앴다. 최초 선택 또는 topology dirty 때만 전체 구간을 재구성한다.
- 매 프레임 작업 집합은 Dormancy가 이미 준비한 awake 목록, 직전 프레임 awake 목록, 변화 알림의 합집합이다. 직전 목록을 한 프레임 유지하여 막 잠든 장식의 기존 정점을 퇴화시킨다. **추가로 구간당 재질을 빌려주는 첫 원본 하나는 검사한다.** 그 원본이 잠들어도 보이는 다른 멤버가 그 재질을 사용하므로 변화 감지에 필요하다. 따라서 `collect-items`가 Dormancy awake 수와 정확히 같을 필요는 없다.
- 정점은 awake이고 `rendererEnabled && alpha > 0`, 실제 renderer enabled/active, 원본 force-off가 아닌 장식만 만든다. 숨김 전환 때 위치/색을 0으로 써 퇴화시키고, 안정적으로 잠든 항목은 재계산/재업로드하지 않는다. 인덱스와 원본 정점 오프셋은 유지한다.
- 비활성 장식 자식 렌더러는 owner별로 색인한다. 매 프레임 2.9만 자식 목록을 훑지 않는다. 기존 Dormancy 알림과, Dormancy가 이미 읽는 `meshRendererEnabled` 값의 전환으로 해당 owner만 재검사한다. 기존 활성 barrier와 장식에 속하지 않는 barrier는 계속 검사한다.
- `TightLate.Apply`의 최종 position/quaternion/scale 및 관리 색을 SoA에 기록한다. 기존 Transform/SpriteRenderer 쓰기는 유지했다. 직접 자식의 local translation은 `MoveApply.SetLocal` 쓰기 이후 기록한다. 캡처가 없는 프레임, 지원하지 않는 부모 관계, 검증에서 탈락한 항목은 해당 장식의 Transform을 읽는다.
- SoA를 처음 채택할 때 실제 pivot position/rotation/localScale, 렌더러 행렬과 색을 비트 대조한다. `decorender verify` 동안 추가로 64후보당 1개를 대조한다. 불일치는 누적하고 해당 항목을 세션 동안 Transform 폴백으로 전환한다. 실제 Unity의 quaternion/행렬 반올림으로 폴백이 많아질 가능성은 실게임에서 확인해야 한다.
- dirty 장식별로 NativeArray 입력을 채우고 `IJobParallelFor`를 한 번 schedule/complete한다. Burst는 사용하지 않는다. 정점 위치·색 생성은 job에서 수행하며 Unity Renderer/Mesh 접근은 메인 스레드에 남긴다. 정적 local vertices/atlas UV는 최초/재구성 때만 준비한다.
- dirty 범위를 run 내 인접 범위끼리 병합하여 `SetVertexBufferData`로 부분 업로드한다. 바뀐 run만 보이는 멤버의 캐시 bounds/최종 행렬로 bounds를 갱신한다. `HasVisible=false`인 run은 그리지 않는다.
- 첫 카메라 `onPreCull`에서, LateUpdate 이후 한 번 준비한다. 새 bounds가 culling 전에 필요하므로 이 시점을 유지했고 `preparedFrame`으로 다른 카메라가 같은 메시를 재사용하게 했다. 카메라별 원본 숨김/복구는 실제 숨긴 원본만 순회한다.
- awake 및 알림 대상의 sprite/material/order/layer/mask/repeat/MPB/재질 속성 변화 검사는 유지한다. 재질의 매 프레임 문자열 직렬화는 숫자/참조 스냅샷 대조로 바꿨다. 변경 항목은 원본으로 돌리고 구간을 재구성한다. z 순서 변경 및 빌드 이후 알림은 해당 정렬키의 원본으로 폴백한다. off/실패 시 원본 force-off와 빌린 재질 텍스처를 복원하고 메시·아틀라스·NativeArray를 해제한다.

## 새 훅 및 파일

| 파일 / 함수 | 역할 |
|---|---|
| `Dormancy.cs`: `RenderAwake` | 준비된 목록을 읽기만 한다. `List` 재호출로 통계나 준비 단계를 진행하지 않는다. |
| `Dormancy.cs`: `WakePost`, `WakeBase`, `Sleep` | 색·보임·마스크·mesh enable·Setup/Awake·잠듦 변화 알림. Setup/Awake는 구조도 무효화한다. |
| `Dormancy.cs`: `IsDormant` → `DecoRender.ObserveMesh` | 이미 읽은 mesh enabled 값의 전환을 전달한다. 별도 자식 폴링 없음. |
| `TightLate.cs`: `Apply` → `DecoRender.Capture` | 최종 계산값을 SoA에 기록한다. SkipSame 분기에도 적용한다. |
| `MoveApply.cs`: `SetLocal` → `DecoRender.RecordLocal` | 실제 setter를 수행한 뒤 자식 translation 갱신 또는 pivot 캡처 무효화. |
| `DecoRender.Incremental.cs` | 작업 집합·SoA·job·부분 업로드·barrier 색인·타이밍 통계·버퍼 수명. |
| `DecoRender.Uniforms.cs` | 재질/MPB의 문자열 없는 유효값 대조. |
| `DecoRender.cs`: `PrepareFrame`, `PreCull`, `Takeover`, `PostRender`, `Stop` | 한 프레임 한 번 준비, 변경 시 재구성, 카메라별 숨김/복구, 정리. |

## state / 명령

`decorender state`에 다음을 표시한다. 타이밍 단위는 CPU ms다.

- 기존 `build-ms/last-frame`, `build-ms/mean-frame`, `compute-ms`, `upload-ms` 유지.
- `build-cpu-ms/window2048`: 최근 최대 2,048빌드의 **total / collect / vertices-job+wait / upload+bounds / topology**, 각각 mean·p99·n.
- collect는 카메라/작업 집합 수집, 후보 변화 검사, barrier 검사, SoA/폴백 읽기를 포함한다. topology는 전체 재구성/배열 재준비다. job 시간은 schedule 및 Complete 대기까지, upload는 범위 정렬·부분 업로드·bounds 갱신까지 포함한다. 카메라별 takeover/복구와 LateUpdate 안의 Capture 비용은 이 빌드 타이머 밖이다. 최종 이득은 전체 프레임과 fuse로 판단해야 한다.
- `changed-items`, `uploaded-vertices`, `collect-items`, `drawing-runs`, `soa-hits`, `transform-fallbacks`, `soa-verify`, `soa-checks`, `soa-mismatches`.
- `decorender verify` / `decorender verify off`: 추가 샘플 대조 켬/끔. 최초 채택 대조는 항상 수행한다.
- `decorender stats reset`: 빌드 누적/창 통계만 초기화한다. 메시·선택·누적 대조 실패 수는 유지한다.

`mean-frame`은 reset 이후 전체 평균이고, window2048 평균/p99는 마지막 2,048프레임으로 제한된다. 12초 동안 2,048프레임을 넘으면 p99가 전체 12초의 값은 아니다. 초기 선택·on 자체의 일회성 준비 시간도 매 프레임 통계에 포함하지 않는다. 활성 세션 도중 topology 재구성 비용은 포함한다.

## 검증

| 검사 | 결과 |
|---|---|
| `dotnet build -v q --nologo` | DEV: 오류 0, 경고 39 |
| `dotnet build -p:Edition=Player -v q --nologo` | Player: 오류 0, 경고 36 |
| `dotnet build -p:Edition=Player -p:AutoTestBuild=1 -v q --nologo` | AUTOTEST: 오류 0, 경고 39 |
| `tools/DecoRenderCheck` | production 증분 코드에 대한 제어 흐름 검사 39개, 실패 0 |
| DLL 메타데이터 | DEV/AUTOTEST에 DecoRender와 VerticesJob 존재, Player에는 타입과 참조 없음 |
| `tools/LoadCheck` DEV | FieldRefAccess 268곳, 틀림 0. 동적 이름 5곳은 별도 런타임 확인 대상 |
| `git diff --check` | 통과 |

로그는 `bin/decorender10-{dev,player,autotest}-build.log`, `bin/decorender10-fixture.log`, `bin/decorender10-loadcheck.log`다. bin은 추적하지 않는다.

fixture는 실제 `DecoRender.Incremental.cs`를 링크하며, 잠듦/깨움·안정 프레임 무업로드·인접 범위 병합·정확한 메시 오프셋·float/byte 숨김 색·SoA 채택/거부·직접 자식 SetLocal·늦은 알림 폴백·mesh 값 변화·버퍼 해제를 검사한다. **가짜 Unity/Jobs를 사용하므로 실제 Unity job 실행, 스레드 배치, 네이티브 반올림, GPU 화면 및 성능을 검증한 것은 아니다.**

## 다음 게임 측정 순서

정확성과 성능은 별도 재생으로 검사한다. 같은 해상도·카메라·재생 설정을 사용한다.

1. Arche 약 250초에서 `decorender on 0 scope=all`.
2. 정확성 판: `decorender verify`, `decorender compare`를 여러 다른 프레임에서 8회 이상. `decorender state`로 각 compare max ≤2/255, 변화 픽셀 비율, SoA 검사/불일치/폴백 수, 그리는 구간을 기록한다. 중간 보임·투명·mesh 재활성/정렬 변경과 off 복원도 확인한다.
3. `decorender off`. 캡처/PNG 쓰기와 비교 검사가 있는 판을 FPS 증거로 사용하지 않는다.
4. 별도 성능 판을 **끔 / 켬 / 끔**으로 반복한다. 켠 판은 250초 부근에서 on하여 초기 준비를 측정창 전에 마친다. `verify off`로 추가 샘플 검사를 끈다.
5. 켠 판 264초 직전 `decorender stats reset`, 264초에 `fuse begin`, 276초에 `fuse end`. state 출력은 end 뒤에 수행한다. 끈 판도 같은 264~276초 `fuse begin/end`를 사용한다.
6. 각 판의 12초 프레임 수·모드 갱신 ms·Late ms와, 켠 판 build total/collect/job/upload/topology 평균/p99/n을 기록한다. 마지막에 `decorender off`.

관문은 켠 판이 끈 판과 같거나 더 나은 전체 프레임 시간, 빌드 평균 0.3~0.5ms 이하, compare 최대 ≤2/255다. 현재 이 값들은 **미측정**이다. 수집/계산/업로드 중 실제 지배 항목도 아직 특정하지 않았다. 수집에는 awake뿐 아니라 재질 대여자와 안전성 검사가 남아 있고, 많은 SoA 폴백·잦은 topology 재구성·부분 업로드 호출 수가 목표를 막을 수 있으므로 각 분리 통계를 확인해야 한다.

전체 scene renderer 재검색은 초기/변화에 따른 재구성 때만 한다. Dormancy 알림을 거치지 않는 외부 코드의 비활성 자식 직접 활성화나 새로운 비장식 Renderer 생성까지 매 프레임 즉시 발견하는 보장은 이번 증분 경로에 없다. 실제 compare와 해당 변화 시나리오를 통과하기 전에는 이전 단계의 화면 동등성 결과를 새 구현의 결과로 승계하지 않는다.
