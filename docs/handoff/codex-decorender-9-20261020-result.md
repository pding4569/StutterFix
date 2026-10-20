# S2j decorender 결과 — 대기 장벽 제외와 보이는 run 통계

`DecoRender.cs`에 구현했다. `#if DEV || AUTOTEST`와 조건부 Compile을 유지했다. 커밋·게임 실행·설치는 하지 않았다. 실제 Arche의 run 수·화면 동일성·FPS 이득은 이번 작업에서 측정하지 않았다.

## 구현

- 비관리 렌더러는 `enabled && gameObject.activeInHierarchy && !forceRenderingOff`일 때만 정렬 장벽이다. 꺼진 장식 자식 MeshRenderer와 비장식 렌더러는 정렬 comparator와 run 분할에서 제외한다. 관리 SpriteRenderer는 숨겨져도 후보 순서를 유지한다.
- 꺼진 비관리 렌더러의 잘못된 재질 수나 SortingGroup 때문에 같은 키 전체를 제외하지 않도록, 대기 렌더러 필터를 정렬 안전성 검사보다 먼저 적용했다. 재질 대여를 위한 공유 여부 검사는 꺼진 렌더러도 계속 포함한다.
- 기존 프레임당 한 번의 `PruneChanged → RebuildRuns → Build` 경로가 대기 렌더러의 활성화·비활성화·정렬 키 변경을 반영한다. 기존 run과 멤버 순서가 같으면 메시와 재질을 재사용한다.
- 실제 그리는 자식 MeshRenderer가 있으면 그 소유 장식은 `decoration-child-mesh-active`로 원본 복원·관리 제외한다. `meshRendererEnabled` 필드는 기존 PruneChanged 감시와 매 카메라 PreRender 감시를 함께 사용한다. Dormancy.cs도 같은 필드를 읽고 EnableMeshRenderer를 변경 지점으로 취급하는 것을 확인했다. Dormancy에는 변경을 가하지 않았다.
- PreCull 감사에서 비관리 렌더러의 enabled·activeInHierarchy·forceRenderingOff·정렬 키와 소유 장식을 저장하고, 매 PreRender에서 다시 확인한다. PreCull 이후 바뀌면 이전/현재 키와 소유 장식의 SpriteRenderer 키를 원본으로 복귀시킨다. 해당 키의 run은 비활성화하고 재질 대여와 원본 force-off를 해제한다. 다른 키는 계속 관리한다.
- 컬링이 끝난 PreRender에서 새 메시를 생성하지 않는다. 늦은 변화의 영향을 받은 키는 그 프레임 남은 카메라에서도 원본으로 유지하고 다음 프레임 PreCull에서 기존 재구성 경로로 다시 계획한다. 따라서 늦은 변화 프레임의 `runs`는 아직 이전 계획일 수 있고, `fallback-keys`와 `drawing-runs`로 실제 관리 중단을 구분한다. Unity 컬링과 원본 복원 시점의 실제 화면 결과는 게임에서 확인해야 한다.
- 현재 보이는 장식이 정점을 내보내는 run만 `HasVisible=true`로 판단한다. 숨김 또는 정점 0개인 run은 takeover 때도 MeshRenderer.enabled=false다. 숨은 장식의 고정 토폴로지·퇴화 정점 방식은 유지했다.
- 초기 on 로그 전에 Build와 가시 run 표본을 준비하므로 시작 로그에서도 보이는 run 수를 확인할 수 있다. scope·atlas 예산·증분 버퍼·재질 대여·compare 캡처 방식은 유지했다. 원본 복귀 키가 있는 프레임의 compare는 기존 snapshot 검사 경로로 실패 처리해 유효한 동일성 표본에 섞이지 않게 했다.

## state / on 로그

아래 항목은 `run-state={...}` 및 선택 완료 `on` 로그에 나온다. off 이후 마지막 스냅샷도 보존한다.

| 항목 | 의미 |
|---|---|
| candidates / taken | 최초 적격 후보 / 현재 관리 장식 수 |
| runs | 활성 장벽만 사용해 계획한 현재 전체 run 수 |
| run-mean / run-max | 최근 최대 120개 프레임 표본의 전체 run 수 평균 / 최대 |
| visible-runs / drawing-runs | 보이는 장식의 정점이 있고 원본 복귀 키가 아닌 현재 run 수. 두 항목은 같은 값 |
| visible-run-mean / visible-run-max | 같은 프레임 창에서 drawing-runs 평균 / 최대 |
| visible-run-samples | 현재 표본 수 / 창 크기 120 |
| average-run / max-run | 현재 관리 장식 수÷전체 run 수 / 최대 멤버 수 |
| run-sizes | 멤버 수 1, 2~5, 6~20, 21~100, 101+의 run 개수 |
| active-barriers / active-barrier-renderers | 관련 키의 현재 그리는 비관리 장벽 총수 / 종류·제외 이유별 수 |
| waiting-barriers / waiting-barrier-renderers | 관련 키의 꺼진 비관리 렌더러 총수 / 종류·제외 이유별 수 |
| unmanaged-breaks | 후보 사이에 활성 비관리 장벽이 있어 끊긴 간격 수 |
| fallback-keys | PreCull 이후 변화로 원본에 복귀한 키 수 |
| keys | 현재 관리 장식이 남아 있는 정렬 키 수 |

이전 `barrier-renderers`는 `active-barrier-renderers`와 `waiting-barrier-renderers`로 나눴다. 장벽 수는 매 프레임 재구성 시점의 스냅샷이며 관련 키 앞/뒤 장벽도 포함하므로 unmanaged-breaks와 합계가 같지 않다. 보이는 run 수는 **소스 가시 상태와 정점 방출 기준**이다. 카메라 frustum·occlusion·cullingMask를 반영한 실제 GPU draw call 측정값은 아니다. PostRender에서 다음 카메라를 위해 렌더러를 끄더라도 state에는 준비된 가시 run 수를 보여 준다.

표본은 Time.frameCount당 한 개다. 같은 프레임의 다음 카메라가 원본 복귀를 일으키면 마지막 값을 교체한다. state 조회와 compare로 표본을 추가하지 않는다. 최근 창의 평균/최대는 run 개수이며, average-run/max-run과 분포는 현재 멤버 수 통계다. 기존 compute/upload-ms에는 매 PreRender의 추가 감시 비용이 포함되지 않는다.

## 검증

- Release DEV: 오류 0, 경고 39.
- Release PlayerAuto (`Edition=Player`, `AutoTestBuild=1`): 오류 0, 경고 39.
- Release Player: 오류 0, 경고 4. 기존 참조 충돌·obsolete·미사용 필드 경고가 남아 있다.
- MSBuild Compile 평가: DecoRender.cs는 Player=0, PlayerAuto=1.
- 실제 소스 추출 run/장벽/통계 검사: **50건 통과**. 꺼짐·force-off·비활성 장벽 무시, 활성화 후 분할, 꺼진 다중 재질 장벽, 자식 메시의 실제 enabled와 필드 감시, 늦은 변화의 원본 복귀, 영향 없는 키 유지, 빈 run 비활성화, 같은 프레임 표본 교체, 120프레임 창, 크기 분포를 포함한다.
- 실제 Build/PrepareFrame/ValidSession 소스 추출 검사: **39건 통과**. 기존 증분 slice·다중 카메라 준비 1회·두 색 버퍼·숨김/재등장·씬 전환 회귀 검사와 정점 0개 run을 확인했다. 이 fixture의 정렬 재구성과 표본 수집은 stub이며 해당 로직은 위 50건에서 별도 검사한다.
- `py -3 tools/check-ui-encoding.py`: clean. `git diff --check`: 통과.

검사는 Unity 렌더러·Mesh·콜백을 모형으로 대체한다. 실제 Unity 컬링, GPU 순서, 픽셀 동일성, Arche에서 수만 개 run이 실제로 얼마나 줄어드는지는 입증하지 않는다. 새 렌더러가 PreCull 이후 생성되는 경우는 다음 프레임의 전수 감사에서 발견한다.

로그: `obj/decorender-9-build-{dev,autotest,player}.log`, `obj/decorender-9-check.log`, `obj/decorender-9-incremental.log`. 생성기: `obj/decorender-9-check/generate.py`, `obj/decorender-9-incremental.py`. obj 산출물은 git 비추적이다.

## 게임에서 돌릴 순서

사용자가 빌드를 설치한 뒤 Arche의 같은 재현 구간에서 실행한다.

```text
decorender on 0 scope=all
decorender compare
decorender compare
decorender compare
decorender state
decorender off
decorender state
```

compare를 필요한 N회 반복한다. 최근 120프레임 통계가 필요하면 120개 렌더 프레임 이상 진행한 뒤 state를 기록한다. candidates/taken 대비 runs, drawing-runs, run-mean/max, visible-run-mean/max, run-sizes, 활성/대기 decoration-child:MeshRenderer 수를 확인한다. compare의 snapshotStable·픽셀 차이·원본/그룹/diff 이미지를 기록한다. 자식 메시나 비장식 렌더러가 켜지는 구간에서는 원본 복귀와 다음 프레임 run 분할, off 후 원본 보임·재질 복원을 확인한다. 실제 감소량과 화면 동일성을 확인하기 전 FPS 개선을 결론내리지 않는다.
