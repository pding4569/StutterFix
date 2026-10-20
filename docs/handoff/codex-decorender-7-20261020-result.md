# S2h decorender 결과 — 재질 대여와 연속 구간 메시

`DecoRender.cs`에 구현했다. 기존 `#if DEV || AUTOTEST`와 조건부 Compile을 유지했다. 커밋·게임 실행·설치는 하지 않았다. **실제 run 수, 화면 동일성, FPS 이득은 미측정**이다.

## 구현

- 기존 가장자리 블록 선택·그룹 재질 생성·재질 ID 재시도·키별 단일 그룹 제한을 제거했다. 명령은 `decorender on [20] [floatcolor]`, `compare`, `state`, `off`이며 기존 `edge=...`, `matid=...` 옵션은 제거했다.
- 관찰 기간에 행렬·색·flip 변화가 있었던 적격 장식을 관리 후보로 남긴다. 보임 여부는 후보 조건에 넣지 않는다. 기존 Sprites/Tile·Sprites/Default, RepeatX/Y=1, Clamp, 최대 2048, 마스크/히트박스·점샘플링 제외 조건을 유지한다.
- 로드된 씬의 모든 Renderer를 Resources로 조사한다. 비활성·disabled·forceRenderingOff·알파 0·DontSave 렌더러도 포함한다. 각 `(sortingLayerID, sortingOrder)` 안에서 **큐 오름차순 → 정렬 거리 내림차순 → 부호 있는 재질 인스턴스 ID 오름차순**으로 정렬한다.
- 그 줄에서 후보가 최대한 이어지는 구간을 구성한다. 비관리 렌더러는 숨었어도 구간을 끊는다. 아틀라스 페이지·Unity layer·실효 재질/MPB 상태·월드 z·정렬 거리가 달라도 구간을 나눈다. 따라서 같은 키에 여러 구간이 있어도 키 전체를 제외하지 않는다.
- 구간 첫 장식의 **동일한 원본 Material 객체**를 MeshRenderer.sharedMaterial로 빌린다. 새 Material을 만들지 않는다. 모든 씬 렌더러의 sharedMaterials 사용 수를 확인하여 다른 렌더러와 공유된 재질은 `shared-material`로 해당 장식만 제외한다. 자체 그룹 렌더러와 지연 Destroy 중인 자체 렌더러는 이 집계에서 제외한다.
- 대여는 렌더 동안에만 적용한다. 원본 SpriteRenderer들을 forceRenderingOff로 숨긴 뒤 원래 mainTexture를 저장하고 아틀라스로 바꾼다. 그 밖의 원본 재질 속성은 변경하지 않는다. RepeatX/Y=1은 후보 조건으로 보장하고, 그룹 MPB에 원본 MPB·아틀라스 `_MainTex`·흰 `_RendererColor`·단위 `_Flip`을 넣는다. 정점에 원본 색·flip·월드 좌표·아틀라스 UV를 적용한다.
- 카메라 PostRender, compare 원본 촬영, 구간 해제, off/실패 정리에서 원본 mainTexture와 forceRenderingOff를 복원한다. 원본 재질을 Destroy하지 않는다. 원래 forceOff였던 장식은 그대로 숨긴다.
- sprite/material/sortingOrder/sortingLayer/mask 등 변화 감지는 **해당 장식만** 후보 목록에서 제거한다. 관찰 중에도 키 전체를 탈락시키지 않는다. 복원한 장식은 이후 정렬 줄의 비관리 장벽이 된다. 영향을 받은 구간만 새 메시로 바꾸고, 구성과 상태가 같은 다른 구간의 메시·재질은 재사용한다. 재질 제공자가 빠지면 남은 구간의 첫 멤버가 새 제공자가 된다.
- 각 카메라 PreCull과 PreRender에서 정렬 줄을 재조사하므로 외부 렌더러 생성·정렬/거리 변화도 다음 렌더에 반영한다. 실제로 안 보이는 관리 장식은 구간에는 남기되 해당 프레임 정점·인덱스를 내보내지 않는다. 첫 컬링 이전 초기 토폴로지와 원본 경계를 제공한다.

## 보수적 제한과 비용

- 원근 정렬 또는 z축 이외 방향으로 거리 정렬하는 카메라는 장식 하나씩 구간을 만든다. 합친 메시의 경계 중심이 원본 정렬 거리를 바꿀 수 있기 때문이다. 일반적인 z축 직교 정렬에서 구간 병합을 사용한다.
- SpriteSortPoint.Center만 받는다. Pivot으로 바뀌면 해당 장식을 복원한다.
- 키에 재질이 없는 Renderer, 복수 재질/서브메시 Renderer, SortingGroup이 있으면 `unsupported-order`로 그 키의 후보들을 복원한다. SortingGroup 자식의 키뿐 아니라 조상 그룹 자체의 정렬 키도 확인한다. 이 경우는 일반적인 비관리 단일 재질 렌더러 장벽과 구별한다.
- 전수 조사·정렬·구간 비교는 카메라마다 실행된다. 그룹 재사용 비교도 포함하므로 **메시 수가 줄어도 전체 프레임 이득을 보장하지 않는다**. 실제 CPU 비용은 state의 build-ms와 별도 프레임 측정으로 확인해야 한다. build-ms에는 PreCull 조사 비용이 포함되지 않는다.
- 아틀라스 예산 초과 시 기존 키 단위 예산 제외 방식은 유지했다. 사용하지 않게 된 페이지도 세션 종료까지 보유할 수 있으며 atlas-bytes는 할당량이다.

## 방향 근거와 확인 한계

방향은 **부호 있는 material ID 오름차순 = 뒤→앞**으로 코드와 on/order-audit 로그에 넣었다. 저장소의 [S2f 결과](codex-decorender-5-20261020-result.md)에 기록된 CSV 직접 분석은 materials n=2 50/50, n=3 49/50, n=6 50/50이었다. 동일 재질을 공유하는 후보는 이번 구현에서 허용하지 않는다.

다만 원본 `D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\Mods\StutterFix\sortprobe\20261020-012144-430-3127f337\trials.csv`는 현재 없다. 해당 설치 폴더와 E:\StutterFixWork의 작업 폴더들에서도 trials.csv를 찾지 못했다. **이번 작업에서 before_back_to_front와 mat 값을 다시 대조하지 못했고, 이전 결과 문서의 분석을 근거로 사용했다.** 로그에도 이 제한을 표시한다. 원본을 확보하면 before 열의 재질 대응을 다시 검산해야 한다. 98~100%의 관찰값을 Unity의 일반적 정렬 보장으로 확대하지 않는다.

## 새 state/on 항목

`state`의 `run-state={...}`와 선택 완료 `on` 로그에 다음을 출력한다. off 뒤에는 종료 직전 스냅샷을 보존한다.

| 항목 | 의미 |
|---|---|
| moving-candidates | 관찰을 마친 뒤 움직임이 확인된 적격 장식 수. 예산·공유 재질 등 추가 제외 전 |
| runs | 현재 살아 있는 그룹 메시 수 |
| taken | 현재 관리 장식 수. 관찰 중 0 |
| average-run | 현재 관리 장식 수 / run 수. run이 없으면 0 |
| run-sizes | 크기 1, 2~5, 6~20, 21+ 구간의 개수 |
| unmanaged-breaks | 한 키의 후보 구간 사이에 비관리 렌더러가 끼어 끊긴 횟수. 연속된 비관리 여러 개는 한 번 |
| keys | 현재 관리 장식이 있는 키 수 |
| observation-change-fields | 관찰 중 제거를 유발한 sprite/material/sortingOrder/sortingLayer/mask/other 필드별 건수 |

관찰 변화 카운터는 한 장식에서 여러 필드가 바뀌면 중복 집계한다. `other`는 material-properties, renderer-owner, unityLayer, sortingGroup, drawMode, repeat, wrap, 파괴, 정렬 기준점 등을 합친다. 기존 `changes`에는 이 세부 분류도 남긴다. `changed-during-observation`과 `restored-items`는 실제 제거된 장식 수이며 더 이상 키 전체의 연쇄 제외 수가 아니다. `selected`는 최초 선택 스냅샷, `taken`/`managed`는 현재값이다. 기존 edge/matid 통계는 제거했다.

성공 판단에 필요한 **실제 moving-candidates/runs/taken/average-run/분포 값은 게임 실행 뒤 채워야 한다**. 이번 구현 작업에서 추정값을 넣지 않았다.

## 검증

- Release DEV / PlayerAuto / Player 빌드: 모두 오류 0개. 기존 참조 버전 충돌·obsolete·미사용 필드 경고는 남는다.
- MSBuild Compile 평가: DecoRender.cs는 Player=0, PlayerAuto=1.
- 실제 소스의 RebuildRuns, SortDistance, Borrow, ReleaseGroup, SourceOff/SourceVisible/HideSource/RestoreSource를 추출한 오프라인 검사 26건 통과. 음수 ID 방향, 큐/거리 우선순위, 숨은 외부 장벽, 숨은 후보 유지, 페이지 분리, 구간 재사용, 멤버/제공자 제거, 공유 재질 제외, 새 외부 렌더러, 원근 singleton, 복수 재질 제외, 텍스처/forceOff 복원, 원본 재질 비파괴를 확인했다. 씬·렌더러·메시 생성 API는 모형이다. **Unity 콜백/컬링/GPU/픽셀 검사와 PruneChanged 전체 실행 검증은 아니다.**
- `py -3 tools/check-ui-encoding.py`: SFENC001: clean.
- `git diff --check`: 통과.

빌드 로그: `obj/decorender-7-build-{dev,autotest,player}.log`. 검사 생성기·프로젝트: `obj/decorender-7-check/`, 결과: `obj/decorender-7-check.log`. Compile 평가: `obj/decorender-7-{player,auto}-compile.json`. obj 아래 검증 산출물은 git 추적하지 않는다.

## 게임에서 돌릴 순서

사용자가 빌드를 설치한 뒤 기존 재현 구간에서 실행한다. 이번 작업에서는 설치하거나 게임을 켜지 않았다.

```text
decorender on 20
decorender compare
decorender compare
decorender compare
decorender state
decorender off
decorender state
```

compare는 필요한 N회만큼 반복한다. AutoTest에서는 on이 관찰 20프레임을 끝낼 때까지 재시도하고, compare는 카메라 PostRender 결과가 나올 때까지 같은 step을 재시도한다. 각 compare의 song/frame, snapshotStable, max/임계값별 픽셀 비율과 원본/그룹/diff 이미지를 확인한다. state의 run 수가 taken보다 충분히 작아지는지와 관찰 변화 필드 분포를 먼저 기록한다. off 후 원본 텍스처·보임 복구도 확인한다. clone camera compare의 기존 한계(이미지 효과·명령 버퍼·다른 카메라 미복제)는 유지된다.

