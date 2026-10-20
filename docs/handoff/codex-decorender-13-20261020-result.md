# Codex S7 decorender 결과 — 2026-10-20

## 수행 범위와 결론

작업 위치 `E:\StutterFixWork\wt-decorender2`. 커밋·게임 실행·설치 없이 코드 수정, 로컬 빌드, 오프라인 회귀 검사를 수행했다. 신규 런타임 코드는 `#if DEV || AUTOTEST` 안에 있다. Player에는 DecoRender 타입과 참조가 없음을 DLL 메타데이터로 확인했다.

전체 장면 탐색 `RebuildRuns` 호출을 `on` 초기화 한 곳으로 제한했다. 이후에는 변경 키만 계획하고, 지원하지 않는 구조 변경은 해당 키를 세션 동안 원본 렌더러로 복귀시킨다. 카메라 목록 변경은 기존처럼 세션을 중단한다. 다시 `on` 해야 하며, 실행 중 목록 변경을 무시하지 않는다.

**게임 성능과 픽셀 목표 달성은 미검증이다.** topology ≤0.1ms, collect ≤1ms, 총 빌드 ≤0.5ms, compare 최대 ≤2/255를 새 빌드에서 측정하지 않았다. 기존 S6 수치는 입력 자료이며 이번 수정 결과가 아니다.

## 확인한 코드 원인

- 기존 `DecoRender.Incremental.cs:342`: 장식이 이미 기록됐고 보이는 상태에서 `Matrix.m23` 비트가 달라지면 `topologyDirty=true`. 다음 PrepareFrame은 `RebuildRuns(mainCamera)`를 호출해 `Resources.FindObjectsOfTypeAll<Renderer>()`, 전체 재질 사용 횟수·SortingGroup·장벽 색인을 다시 계산했다. 단일 장식의 z 잡음이 전역 탐색을 유발할 수 있는 확정된 코드 경로다. S6에서 실제로 가장 많이 발동했는지는 실행 전 원인 계측이 없으므로 확정하지 않는다.
- setup, mesh 상태 전환, watch의 key/material/unsupported 변경, mesh-owner 알림, prune도 같은 전체 경로에 연결돼 있었다. watch 거리만 변해도 정렬 작업과 함께 다른 조건이 전역 플래그를 세울 수 있었다.
- 기존 `preparedSortZ`는 **mainCamera일 때만** 갱신됐다. 다른 카메라 4개가 공유 값을 순회마다 덮어쓴다는 가설은 코드와 맞지 않았다. 다만 주 카메라 z축 변화가 전역 플래그로 이어졌으며 다른 카메라의 정렬 설정 변화는 충분히 표현하지 못했다.
- SoA는 `residents[d] -> Item.Slot`에 기록하고 같은 `Item.Slot`에서 읽는다. 정적 검사에서 별도의 키/인덱스 체계 불일치는 발견하지 못했다. 슬롯 재배치 시 현재 프레임 scalar 캡처 보존도 기존 코드에 이미 있었다.
- SoA 적중에는 현재 프레임 `PoseFrame`과 `!NativeFallback`이 모두 필요하다. 첫 위치/회전/로컬 스케일/합성 행렬/색 비트 검증 중 하나만 실패해도 `PoseRejected`가 설정되고 레이아웃 재생성 뒤에도 유지됐다. 훅 호출·매핑이 많아도 적중 0이 가능한 경로다. 실제 S6의 실패 항목은 새 reject 카운터로 확인해야 한다.

## 수정 내용

### topology

- `DecoRender.Incremental.cs`의 Build에서 raw `m23` 변화는 원인 계측만 한다. 재정렬 판단은 초기 정렬과 같은 `SortDistance(renderer, mainCamera)`와 `orderedState.Distance`를 비트 비교한다. 기존 비교기의 bounds.center/axis/perspective 계산을 유지했다.
- 실제 거리가 바뀌면 `topologyKeys`·`reorderKeys`에 해당 키만 추가한다. 이미 cull이 시작된 현재 프레임은 그 키의 원본을 사용하고 다음 프레임에 키 계획을 갱신한다. 정확도를 위해 z epsilon이나 거리 허용오차를 도입하지 않았다.
- `GroupKey`에서 world-z 문자열을 제거했다. z가 고정된 그룹 문자열에 남는 대신 키 계획의 실제 거리 비교로 구간을 나눈다.
- `DecoRender.cs`의 PrepareFrame에서 카메라 ID별 정렬 mode/orthographic/axis 및 필요한 perspective position을 저장한다. 문자열을 매 프레임 생성하지 않는다. 변경 시 기존 색인에 키별 재정렬을 요청하며 전체 장면 탐색은 하지 않는다. mergeCameras 변화도 같은 방식이다.
- watch key 이동은 기존 목록에서 제거하고 새 키 목록에 추가한다. 단순 가시성 전환은 기존 비교기 스냅샷을 유지한다.
- setup/mesh/unsupported watch/prune처럼 재질 대여·하위 메시·SortingGroup 안전성을 확신할 수 없는 구조 변경은 `RetireKey`로 **그 키 전체를 원본으로 복귀**시킨다. 새 재질이 다른 관리 키와 공유되면 그 재질 사용자 키도 복귀시킨다. 복귀한 키는 이번 세션에서 다시 승격하지 않는다. 단순 z/거리 변경은 이 경로를 사용하지 않는다.
- 나머지 키의 구간과 출력은 유지한다. `retired-keys`와 현재 관리 장식/구간 수를 반드시 함께 비교해야 한다. 관리 대상을 대량 복귀시킨 결과만으로 최적화 성공을 판정하면 안 된다.
- `full-rebuilds`는 세션 누계로 바꿔 `stats reset`이 0으로 숨기지 않게 했다. 정상 세션은 초기 발견 1을 유지한다. 카메라 목록 변경 뒤 새 on은 별도 세션이다.

### 캡처/SoA

- `TightLate.cs`의 Capture를 native position/rotation/localScale setter 적용 **뒤**로 옮겼다. SkipSame 분기와 일반 분기 모두 포함한다.
- 엄격한 TRS 검증을 통과한 장식은 기존 scalar SoA를 사용한다. 검증을 완화하거나 `PoseRejected`를 무조건 지우지 않았다.
- 거부됐거나 부모 구조상 native 행렬이 필요한 장식은 Capture에서 실제 renderer 행렬을 프레임 캐시하고 Build에서 재사용한다. 이후 RecordLocal이 오면 이 캐시를 무효화한다. 레이아웃 슬롯 이동은 Item 소유 캐시를 잃지 않는다.
- `soa-hits`는 기존 TRS 경로 의미를 유지한다. `pose-cache-hits-total`은 마지막 Build의 TRS+정확한 행렬 캐시 적중, `exact-pose-hits`는 정확한 캐시 누계, `capture-matrix-reads`는 캡처 단계 native 읽기 누계다. **exact 캐시 적중이 늘어도 soa-hits는 0일 수 있다.**
- 이 fallback 캐시는 native 읽기를 없앤 것이 아니라 캡처 시점으로 옮긴다. collect 숫자만 작아졌다고 전체 성능 향상으로 해석하지 않는다. fuse와 전체 프레임 측정을 함께 본다. 순수 SoA 적중 및 collect 1ms 달성은 미확정이다.

## 새 계측

`decorender state`에 다음을 추가했다.

- `topology-causes`: on/world-z/sort-distance/sort-axis/merge-cameras/mesh-active/setup/watch/prune/other/camera-list 및 추가 안전 복귀 원인별 이벤트 수.
- `last`, `last-build`, `pending`: 마지막 원인, 마지막 키 계획 직전까지의 원인, 현재 다음 계획 대기 원인. Build에서 발견한 거리 변경은 다음 프레임 재계획 원인으로 표시된다.
- `cause-items/events`: 원인별 영향 장식 이벤트 수. 서로 다른 프레임·원인에 같은 장식이 중복될 수 있으며 고유 장식 수가 아니다. 비장식 watch는 원인 이벤트에 포함되고 장식 수에는 포함되지 않을 수 있다.
- `z-items`, `max-abs-dz`, `first10`: stats 구간의 z 변경 장식 이벤트 수, 최대 절댓값 변화, 처음 10개 `(장식 Index, 이전 z, 새 z)`. z와 최대 변화는 round-trip 형식으로 작은 잡음도 보존한다.
- `retired-keys`, 정확한 캡처 캐시 카운터, `soa-reject-reasons={position,rotation,scale,matrix,color}`. reject 카운터는 기존 soa-mismatches처럼 정확성 누계이며 stats reset으로 지우지 않는다.

세션의 처음 20개 topology 계획에 `[decorender] topology-cause build=... mode=full/on|keys|stable` 로그를 낸다. 원인별 이벤트·영향 장식 이벤트·변경 키 수·해당 계획 전까지의 z 변경 수/최대/처음 10개를 기록한다. stats reset은 이 20개 로그 예산을 다시 열지 않는다. state의 원인 계측은 계속 유지된다.

## 검증

- DEV/Player/AUTOTEST 로컬 빌드: 모두 오류 0. 최종 경고 수는 각각 빌드 로그에 기록했다.
- DecoRenderCheck: **104개 검사, 실패 0**. 실제 Incremental/Topology 소스를 링크한다. 기존 90개에서 z 잡음과 실제 정렬 거리 변경의 구분, 세션 full 누계 유지, 부모 합성 불일치의 strict fallback, post-setter 정확한 캐시 적중·무효화, 한 키 복귀 시 다른 키 구간·출력 보존 등을 추가했다.
- DLL 경계: DEV/AUTOTEST의 DecoRender·VerticesJob 존재, Player의 타입·참조 없음.
- LoadCheck DEV: FieldRefAccess 이름 268곳, 틀림 0, 동적 이름 5곳은 런타임 확인 대상.
- `git diff --check` 통과. `RebuildRuns`의 호출 지점은 초기 on 1곳이다.

검사는 가짜 Unity/Jobs 기반이므로 엔진 내부 부동소수 처리·native 비용·실제 카메라 동작·GPU 픽셀·FPS를 증명하지 않는다. 카메라별 스냅샷과 실제 TightLate setter 순서는 소스 검토 및 실제 어셈블리 컴파일로 확인했으며 fixture가 게임 엔진을 실행한 것은 아니다.

로그: `bin/decorender13-{dev,player,autotest}-build.log`, `bin/decorender13-fixture-build.log`, `bin/decorender13-boundary.log`, `bin/decorender13-loadcheck.log`. DLL: `bin/Debug/StutterFix.dll`, `bin/Player/StutterFix.dll`, `bin/PlayerAuto/StutterFix.dll`. 설치하지 않았다.

## 게임에서 돌릴 순서

동일 Arche 구간(252~262초), 해상도, 카메라 설정을 유지한다. 정확성과 성능은 별도 재생으로 측정한다.

1. `decorender on 0 scope=all atlas=128`
2. `decorender state`: 초기 `full-rebuilds=1`, 선택 수/관리 장식 수/구간 수, tightlate 상태를 기록한다. 처음 20개 topology-cause 로그를 보존한다.
3. 정확성 재생에서 `decorender compare` ×8회 이상. z 이동, 장벽 켜짐/꺼짐, mesh/setup 변경, 잠듦/깨움을 포함한다. **모든 유효 compare 최대 ≤2/255**인지 확인한다. 무효/실패는 통과로 세지 않는다.
4. `decorender off` 후 같은 구간을 성능용으로 재생한다. 같은 on 명령을 실행하고 `decorender verify off`.
5. 초기 준비 뒤 `decorender stats reset`. 252초 `fuse begin`, 262초 `fuse end`. 측정 창 안에서 state/compare/PNG 저장을 하지 않는다.
6. end 직후 `decorender state`. full-rebuilds가 여전히 1인지, 실제 거리/장벽 변경 때 key-rebuilds가 증가하는지, topology-changed-keys-last와 원인별 이벤트를 확인한다. 변화 없는 프레임은 키 재구성이 0이어야 한다.
7. total/topology/collect/layout/dirty/audit/other 및 subset, unaccounted, draw/callback, 프레임 수와 함께 soa-hits/pose-cache-hits-total/exact-pose-hits/capture-matrix-reads/stale/rejected/reject-reasons를 기록한다. 관리 장식·구간 수와 retired-keys를 전후 비교한다.
8. 같은 길이로 끔/켬/끔을 반복하고 마지막에 `decorender off`.

판정 목표: topology 평균 ≤0.1ms, collect 평균 ≤1ms, 빌드 평균 ≤0.5ms, full-rebuilds=1 유지, 실제 변경에서 키 경로 사용, 유효 compare 최대 ≤2/255. 이번 오프라인 결과로 달성을 선언하지 않는다. scalar soa-hits가 계속 0이면 reject-reasons와 local-invalidations/stale을 먼저 확인하고, 정확한 캐시만 적중한다면 캡처 native 비용을 포함한 전체 결과로 평가한다.

기존 감시 범위 제한도 남는다. 색인 때 비활성이던 비장식 객체의 직접 활성화, 알림을 우회한 직접 변경, 외부 Renderer 신규 생성 등은 별도 게임 정확성 검증 대상이다. 키 복귀는 새 전역 발견 없이 안전성을 보존하려는 보수적 처리이며, 실제 게임 전체의 정확성 입증을 대신하지 않는다.
