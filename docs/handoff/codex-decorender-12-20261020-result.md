# DecoRender S6 결과 — 2026-10-20

## 수행 범위

`E:\StutterFixWork\wt-decorender2`에서 소스 수정과 로컬 빌드·회귀 검사만 수행했다. 커밋·게임 실행·설치는 하지 않았다. 새 코드도 `#if DEV || AUTOTEST`와 csproj의 동일 조건 안에 있다.

실제 topology 평균 ≤0.1ms, collect ≤1ms, build 평균 ≤0.5ms, 6~12초 수백 프레임, compare 최대 ≤2/255는 모두 **미측정**이다. 기존 783.8ms/1001.4ms/약 209ms는 새 빌드의 수치가 아니다.

## topology 증분 방식

- `DecoRender.Topology.cs`에 키별 정렬된 Renderer 목록, 렌더 순서 값(queue/distance/material/id), 관리 후보 매핑, 기존 구간 목록을 캐시한다. 초기 on/구조 재발견 때 waiting 장벽까지 포함해 한 번 정렬한다. 기존 활성 목록의 중복 정렬을 제거했다.
- AuditLateBarriers가 변화 키를 topologyKeys에 모은다. enabled/forceRenderingOff/active만 바뀌면 순서를 유지한다. waiting 동안 comparator 스냅샷도 유지하므로 재활성화 자체는 재정렬·재발견을 일으키지 않는다. waiting 동안 이동했다면 활성화 시 실제 값 비교로 해당 키만 재정렬한다.
- 변경 키의 캐시 순서에서 활성 비관리 장벽만 구간을 끊는다. 첫 항목/개수/구성원/GroupKey가 동일한 구간은 메시를 재사용한다. 변화 없는 키는 계획·생성·해제를 하지 않는다. 거리 비교도 캐시값을 사용한다.
- 장벽 거리/큐 변경은 그 키만 재정렬한다. 키 이동·재질 인스턴스 변경, 지원하지 않는 submesh/SortingGroup 장벽 활성화, setup/mesh 소유 관계 변경, 관리 항목 제거/z 변경, 카메라 merge 조건 변경에는 보수적인 전체 발견 경로를 유지한다.
- state에 topology-changed-keys-last, 누적 total, key-rebuilds, full-rebuilds를 추가했다. stats reset으로 초기 비용을 제외한다.
- 실제 구간 변경 시 ResetLayout은 아직 전역 슬롯/NativeArray 재배치를 수행한다. 따라서 **layout까지 변경 키 수에만 비례한다고 주장하지 않는다**. 다음 측정에서 layout이 지배하면 이 경로가 남은 대상이다.
- 기존 break/waiting 이유 집계는 마지막 전체 발견 시점의 스냅샷이다. 증분 변화량은 새 카운터로 판단한다.

## SoA 원인과 수정

확인한 결함: TightLate.Apply → DecoRender.Capture는 매니저 LateUpdate에서 실행되지만, 다음 PreCull의 ResetLayout이 배열을 재할당하고 모든 PoseFrame을 -1로 바꿨다. 훅이 호출돼도 같은 프레임 캡처를 수집 직전에 잃을 수 있었다. 현재 프레임 pose/color를 Item 기준으로 보존하고 새 슬롯에 복원했다. 최초/주기 검증 및 불일치 시 native fallback은 유지했다. float/byte 양쪽 회귀 검사를 통과했다.

이 결함이 기존 Arche의 soa-hits=0 전부를 설명하는지는 게임 증거가 없다. TightLate는 설치/활성/Playing/MoveApply/Dormancy/disableV15Features/compatibility 조건을 통과한 경로에서만 Capture를 호출한다. 항목별 type, mask/mesh, follow/stick/lock, parallax, parent, rejected 조건도 있다. 조건을 임의로 풀거나 Transform 읽기를 SoA 적중으로 세지 않았다.

| 새 진단값 | 의미 |
|---|---|
| capture-calls / mapped | 세션 중 훅 호출 / resident 매핑 성공 누계 |
| nonresident / cold | 관리 대상이 아닌 캡처 / 레이아웃 준비 전 캡처 |
| stale-pose | 현재 프레임 캡처가 없어 native를 읽은 누계 |
| rejected-pose | 구조 또는 검증 거부로 native를 읽은 누계 |
| local-invalidations | RecordLocal의 pivot 변경으로 캡처를 무효화한 누계 |
| tightlate-context | 기존 TightLate state: 설치/활성, 처리 수, 조건별 제외, compatibility |

soa-hits/transform-fallbacks는 마지막 빌드 값이고 새 카운터는 stats reset 이후 누계다. capture-calls=0이면 TightLate 조건, nonresident가 크면 대상 교집합, mapped가 있는데 stale-pose가 크면 시점/무효화, rejected-pose/soa-mismatches가 크면 검증·계층 제약부터 확인한다.

GatherWork도 수정했다. Dormancy가 전체 목록을 반환할 때 이미 기록됐고 계속 숨겨진 항목은 managed rendererEnabled/alpha로 걸러 수집 작업과 다음 previousAwake 목록에 넣지 않는다. visible→hidden 마지막 갱신은 직전 awake 목록으로 처리한다. Notify와 최초/구조 변경 전체 갱신도 유지한다. collect 7ms→1ms 달성 여부는 미측정이다.

## 약 209ms 추적용 계측

기존 상위 단계 audit, dirty+prune, change-compare, layout, collect, job-schedule, job-wait, upload+bounds, topology, other 및 unaccounted-ms를 유지했다. 새 행도 mean/p99/max/n/calls 형식, 최대 2048개 창이다.

| 새 행 | 범위 / 중복 관계 |
|---|---|
| camera-scan/subset-other | 카메라 발견, 정렬 모드/축, 집합 검사. other의 일부 |
| borrow-restore/subset-other | 빌드 전 대여 재질 복원. other의 일부 |
| prune/subset-dirty | renderer/sprite/material/MPB 안전성 검사. dirty+prune의 일부 |
| barrier-poll/subset-audit | 검사 집합 생성, 상태 읽기, 변경 키 등록. audit의 일부 |
| owner-events/subset-audit | 소유자 mesh 처리와 알림 집합 비우기. audit의 일부 |
| build-orchestration/subset-other | Build 진입~종료에서 내부 기계측 단계들을 뺀 signed 잔여. other에 포함 |
| pre-cull/inclusive | ValidSession, PrepareFrame, Takeover를 포함한 활성 콜백 전체 |
| pre-render/inclusive | ValidSession, 카메라 집계/최초 로그, Takeover를 포함한 활성 콜백 전체 |

구간 on/off, 원본 숨김/복원, 그릴 때의 재질 대여는 기존 draw-on/off에 포함된다. inclusive 행은 build/draw와 겹치므로 더하지 않는다. subset도 상위 행과 더하지 않는다. build-orchestration은 잔여의 별도 표시이며 원인을 특정한 독립 단계 이름은 아니다.

unaccounted-ms는 PrepareFrame total에서 상위 단계 합을 뺀 signed 차이를 유지하며 0으로 강제하거나 clamp하지 않는다. 기존 약 209ms가 어느 새 행에 속하는지는 **미확정**이다. GPU 시간과 다른 모드의 시간도 이 CPU 표로 설명할 수 없다.

## 검증 결과

| 검사 | 결과 |
|---|---|
| DEV dotnet build -v q --nologo | 오류 0 / 경고 39 |
| Player dotnet build -p:Edition=Player -v q --nologo | 오류 0 / 경고 36 |
| AUTOTEST dotnet build -p:Edition=Player -p:AutoTestBuild=1 -v q --nologo | 오류 0 / 경고 4 (최종 증분; 앞선 전체 빌드는 경고 39) |
| DecoRenderCheck | 검사 90개 / 실패 0 |
| DLL 경계 | DEV/AUTOTEST에 DecoRender·VerticesJob 존재 / Player에는 타입·참조 없음 |
| LoadCheck DEV | FieldRefAccess 268곳 / 틀림 0 / 동적 이름 5곳은 런타임 확인 대상 |
| git diff --check | 통과 |

테스트는 실제 Incremental/Topology 소스를 링크한다. 장벽 분할/병합, 다른 키 구간 보존, 무변경 프레임 0 재구성, 거리 정렬, enabled 전환의 0 재정렬/0 전체발견, comparator 스냅샷 보존, 지원하지 않는 장벽의 보수적 경로, 현재 프레임 SoA 보존, 숨김 목록 생략/깨움, 기존 업로드와 검증 fallback을 포함한다. 가짜 Unity/Jobs 검사이므로 네이티브 실행 시간·GPU 픽셀·FPS 증거는 아니다.

최종 로그: bin/decorender12-{dev,player,autotest}-build.log, bin/decorender12-fixture-{build,final}.log, bin/decorender12-loadcheck.log. 초기 fixture는 기존 전체 topology 플래그를 기대하던 단언이 실패했고, 새 변경 키 동작에 맞춰 갱신했다. 초기 실패 프로세스는 이 작업이 띄운 것만 종료했다. 최종 검사는 DLL 직접 실행으로 완료했다. 최종 통과 로그와 초기 시도 로그를 구분한다. 산출 DLL은 bin/Debug, bin/Player, bin/PlayerAuto에 있으며 설치하지 않았다.

## 게임에서 돌릴 순서

동일 Arche 구간·해상도·카메라 설정을 사용하고 정확성과 성능은 별도 재생으로 측정한다.

1. `decorender on 0 scope=all atlas=128`
2. `decorender state` — 초기 선택/키/구간과 tightlate-context 확인.
3. 정확성 판: `decorender compare` × 8회 이상. 장벽 켜짐/꺼짐, mesh 전환, 잠듦/깨움 및 서로 다른 프레임을 포함한다. 모든 유효 결과 최대 ≤2/255를 확인하고 무효/실패를 통과로 세지 않는다.
4. `decorender off` 후 같은 구간에서 성능 판 재생. 같은 on 명령 실행. `decorender verify off`.
5. 초기 준비 후 `decorender stats reset`. 예: 252초 `fuse begin`, 262초 `fuse end` (10초). 창 안에서 compare/state/PNG 저장을 하지 않는다. 6~12초 중 같은 길이로 끔/켬/끔을 맞춘다.
6. end 직후 `decorender state`. 프레임 수, build total, topology/layout/collect/dirty/audit/other와 subset, unaccounted-ms, draw/callback 시간을 기록한다. 변경 키·재구성·전체발견·layout-resets 및 capture 진단도 기록한다.
7. 변화 없는 키의 key-rebuilds가 증가하지 않는지 확인한다. full-rebuilds가 계속 증가하면 setup/mesh/재질/키/z/카메라 구조 변화 경로를 조사한다.
8. 동일 길이의 끔/켬/끔을 반복하고 마지막에 `decorender off`.

통과 목표: topology 평균 ≤0.1ms, collect ≤1ms, build 평균 ≤0.5ms, 창 프레임 수 수백 이상, compare 최대 ≤2/255. 현재 달성 판정을 하지 않는다. 미달이면 같은 창에서 가장 큰 단계의 평균을 먼저 보고한다. p99/최대끼리는 프레임이 다를 수 있어 합산하지 않는다. layout 지배 시 전역 재배치, dirty+prune이면 안전성 검사, other이면 새 subset과 build-orchestration, audit이면 폴링/소유자 처리, draw이면 다중 카메라 토글/원본 복구가 다음 확인 대상이다.

기존 감시 범위 제한(색인 때 비활성이던 비장식 객체의 직접 활성화, 알림을 우회한 장식 자식 직접 변경, 외부 Renderer 신규 생성)은 유지된다. 전수 재발견을 매 프레임 수행하지 않으므로 이 시나리오도 별도 정확성 검증이 필요하다.
