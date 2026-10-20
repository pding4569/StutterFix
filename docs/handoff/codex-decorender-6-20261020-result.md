# S2g decorender 결과 — 2026-10-20

`DecoRender.cs`와 `SortProbe.cs`에 요청한 실험을 구현했다. 변경은 `#if DEV || AUTOTEST` 안에 있으며 일반 Player에서는 두 소스가 컴파일 대상에서 빠진다. 커밋·게임 실행·설치는 하지 않았다. 새 GPU 시험 결과와 픽셀 동일성·성능 결론은 없다.

## 선택 방식과 인수

```text
decorender on [frames=20,1..600] [floatcolor] [edge=low|high] [matid=above|below|none]
decorender state
decorender compare
decorender off
```

기본값은 `edge=high`, `matid=none`이다. `decorender on edge=low`처럼 프레임 수를 생략해도 된다. 프레임 수 다음 옵션의 순서는 자유다. 기존 `floatcolor`도 함께 사용할 수 있다.

- 관찰 완료 후, 그룹을 만들기 전에 `Resources.FindObjectsOfTypeAll<Renderer>()`로 조사하고 유효한 로드된 씬의 렌더러만 남긴다. 비활성·disabled·forceRenderingOff·alpha 0·DontSave 렌더러도 포함한다. 프리팹/에셋 렌더러는 제외한다. 비관리 장식·타일·다른 Renderer 종류도 같은 키의 장벽이다.
- 각 `(sortingLayerID, sortingOrder)`에서 원본 `sharedMaterial.GetInstanceID()`를 부호 있는 정수 오름차순으로 정렬한다. 재질 없는 렌더러가 있으면 `missing-material`로 그 키를 제외한다.
- low는 최저 ID 끝부터, high는 최고 ID 끝부터 연속된 적격 후보만 가져간다. 첫 비관리/부적격 렌더러에서 멈춘다. 같은 재질 ID 묶음에 비관리가 하나라도 있으면 그 묶음 전체에서 멈춘다. 재질 동률 안의 Renderer 순서를 가정해 장벽을 건너가지 않는다.
- 블록 밖 적격 후보는 `not-edge-block`으로 제외한다. 키 일부가 부적격이라는 이유만으로 키 전체를 제외하던 `key-not-fully-eligible`와 외부 렌더러가 있다는 이유만의 `shared-order` 제외를 대체했다.
- 기존 셰이더/MPB/샘플링/반복/마스크 제한은 유지한다. 선택 블록에 움직임이 전혀 없으면 `static`, 아틀라스 예산을 넘으면 해당 키의 블록 전체를 `atlas-budget`으로 제외한다. 블록을 복수 그룹으로 나눠야 하면 `split-order`로 그 키 전체를 제외한다. 이 단계에서 블록 일부만 남기거나 다른 블록으로 재선택하지 않는다.
- 메시 내부 순서는 원본 재질 ID 오름차순, 동률은 원본 Renderer ID 오름차순으로 고정한다. 기존 보임 판정·원본 forceOff 복구·변화 시 키 전체 복귀·compare는 유지한다. 관찰 중 구조 변화도 기존처럼 키 전체를 제외한다.

`matid`는 그룹마다 `new Material(source)`의 실제 ID를 블록의 최소/최대 ID와 비교한다. above는 최대보다 큼, below는 최소보다 작음의 엄격한 조건이다. none은 한 번만 생성한다. above/below는 반대 방향이면 자기 재질만 즉시 버리고 최대 64회 생성한다. 새 ID가 늘거나 줄 것이라고 가정하지 않는다.

`group-material` 로그에 `requested`, `attempts`, `matched`, `new-Material(source)-id`, `block-min`, `block-max`, `position=below|inside|above`를 남긴다. 64회 실패하면 `matched=False`, `placement FAILED`를 기록하고 마지막 재질로 실험을 계속한다. 이 표본을 요청한 방향의 성공 표본으로 세면 안 된다. 비교 범위는 **블록 원본 재질**이며 외부 비관리 재질과의 상대 위치는 별개다.

선택은 한 시점의 조사다. 조사에 이미 들어온 비관리 렌더러의 보임 전환은 블록 규칙에 반영된다. 이후 외부 렌더러 생성·재질 교체·정렬 키 이동을 계속 감시하지는 않는다. 이 변화가 있으면 off/on으로 재선택한다. 실제 혼합 타입의 그리기 순서는 아래 sortprobe로 확인해야 한다.

## state와 카운터

`key-state`에 다음을 기록하며 off 뒤에도 종료 직전 스냅샷을 보존한다.

| 필드 | 의미 |
| --- | --- |
| `edge`, `matid` | 이번 세션의 옵션 |
| `managed-keys` | 현재 관리 중인 키 수, 관찰 중 0 |
| `managed-visible`, `managed-hidden` | 실제 SpriteRenderer 상태에 따른 관리 장식 수, 관찰 중 0 |
| `keys=[key:candidates=...,renderers=...,edge-block=...,managed=...,reason=...,reasons={...}]` | 키별 최초 적격 후보 수, 조사한 렌더러 수, 가장자리 블록 크기, 현재 관리 수, 키 전체 제외 이유, 세부 제외 이유 |
| `excluded-key-reasons` | 키 전체가 제외된 첫 사유별 키 개수 |

`edge-block`은 가장자리 선택 직후 크기다. 이후 static/예산/split/changed로 관리 수가 0이 되어도 유지한다. `candidates`는 최초 적격 후보 수이며 관찰 중 탈락도 포함한다. `renderers`는 선택 시 조사값이다. 관찰 중 키 전체가 먼저 탈락한 경우에는 0일 수 있다. 기존 `selected`는 최초 최종 선택 수, `managed`는 현재 남은 수다.

`excluded` 장식 카운터는 초기 부적격 장식을 해당 이유로 세고, 이후 각 단계에서 실제 제거된 후보를 센다. 이전 S2f의 키 전체 장식 수 카운터와 의미가 달라졌다. 부분 블록이 살아 있으면 `reason=managed`와 `reasons={not-edge-block=...}`가 함께 나올 수 있다. 실행 중 복귀는 기존 `changes/restored-keys`, `changes/restored-items`로 확인한다.

모든 장식 키의 `key-selection` 로그에 후보/렌더러/블록 크기, 재질 최소/최대, 제외 이유를 남긴다. `state`의 키 상세도 20개 제한 없이 모두 출력한다.

## sortprobe mesh-material

```text
sortprobe mesh-material [repeats=50,50..200] [seed=20261020]
```

기본 150회이며 기존 `sortprobe all`도 유지했다. 편집기를 열고 재생을 정지한 상태에서만 실행한다. 기존 합성 Sprites/Tile 재질, 같은 큐·정렬 키·거리, 자기 전용 카메라/오브젝트/빈 레이어를 사용한다.

| 케이스 | 구성 | 기본 반복 |
| --- | --- | --- |
| `mesh-material-below` | 메시 1 + 스프라이트 1, 메시 재질 ID < 스프라이트 재질 ID | 50 |
| `mesh-material-above` | 메시 1 + 스프라이트 1, 메시 재질 ID > 스프라이트 재질 ID | 50 |
| `mesh-material-six` | 메시 3 + 스프라이트 3, 서로 다른 재질을 무작위 label 순서로 생성·배정 | 50 |

n=2의 방향은 실제 ID를 비교하고, 안 맞으면 양쪽을 번갈아 새 재질로 교체해 최대 64개 배치를 확인한다. `details`에 각 배치의 메시/스프라이트 재질 ID와 일치 여부, 최종 모든 label의 재질 ID를 기록한다. 실패 표본은 `valid=False`다. n=6은 생성/렌더러 타입과 재질 ID 배정의 상관을 섞으며, 모든 최종 ID를 기록한다.

기존 `material-id` 전체 순서 가설과 `mesh-before-sprite` 가설을 함께 집계한다. 후자는 같은 타입 안의 순서를 강제하지 않고 모든 메시가 모든 스프라이트보다 뒤인지 검사한다. **n=2 above가 두 가설을 직접 구별하는 표본**이다. below에서는 두 가설 모두 `mesh, sprite`를 예측하므로 그것만으로 구별할 수 없다. invalid/불안정/무변경 순서 변화가 있으면 보고서에 규칙 미확정으로 남긴다. 같은 타입만 있는 기존 케이스의 타입 가설 일치는 혼합 타입 근거가 아니다.

새 `report.md`는 무효 표본당 한 줄씩 단계별 원인을 기록한다. solo 색 구별 실패, 픽셀 디코드 모호성, pairwise 비추이성, full-group top 불일치, peeling 불일치, visibility 토글 뒤 top 변화, 반복 순서 변화, 재질 ID 배치 실패를 구별한다. `details`의 `invalid-reasons`에도 보존한다. 보고서 줄에는 최종 label/Renderer 종류/재질 ID도 남긴다.

기존 실행 `20261020-012144-430-3127f337/trials.csv`는 읽기 전용으로 분석했다. 총 2,550행 중 무효 1,550행을 [기존 무효 표본 report.md](codex-decorender-6-invalid/report.md)에 한 줄씩 정리했다. 원본 CSV는 변경하지 않았다. 기존 자료에 내부 검사 단계가 없으면 원인을 확정하지 않고, 기록된 top·before/after·stable와 details의 재질/큐/거리 동률만 설명했다.

## 검증

| 확인 | 결과 |
| --- | --- |
| Release DEV | 성공, 오류 0 |
| Release PlayerAuto | 성공, 오류 0 |
| Release Player | 성공, 오류 0 |
| Player MSBuild Compile 항목 | DecoRender.cs / SortProbe.cs 합계 0 |
| 실제 소스 메서드 추출 오프라인 검사 | 36개 assertion 통과 |
| 기존 무효 표본 분석 | 1,550/1,550행 보고서 기록 |
| UI 인코딩 검사 | `SFENC001: clean` |
| `git diff --check` | 통과 |

검사 생성기 `obj/decorender-6-check.py`, 프로젝트 `obj/decorender-6-check/`, 로그 `obj/decorender-6-check.log`. 실제 선택/키 제외/보임/재질 재시도/인수 처리와 sortprobe 타입 가설·무효 사유 함수를 추출하고 가짜 Unity API로 실행했다. 양 방향, 중간 장벽, 동일 재질 혼합 장벽, 음수 ID, 재질 없음, 숨은 렌더러, 에셋/미로드 씬 제외, 예산/split/static 제외, 재질 배치 성공/64회 실패/해제 수, 옵션 기본값과 범위를 확인했다. Unity 콜백·GPU 시험의 대용이 아니다.

빌드 로그는 `obj/decorender-6-build-{dev,autotest,player}.log`, 기존 표본 분석 생성기는 `obj/decorender-6-historical.py`다. 기존 참조 버전 충돌·obsolete/미사용 필드 경고는 남아 있다.

## 게임에서 돌릴 순서

이번 작업에서는 실행하지 않았다. 빌드를 따로 설치한 뒤 다음 순서로 확인한다. 자동 시험에서는 충분한 단계 제한을 두고 `sortprobe`를 한 단계로 끝까지 기다린다.

```text
keep
timeout 600
open <Arche 경로>
sortprobe mesh-material
```

1. 먼저 정지된 편집기에서 sortprobe를 완료한다. 세 케이스 total/valid/stable, 실제 ID 방향, 두 가설 일치율, 무효 사유를 확인한다. 고정 seed 재실행 및 다른 seed/새 프로세스의 재현성도 별도로 판단한다.
2. 기존 재현 구간으로 이동해 아래 블록을 한 번씩 반복한다. 모든 조합은 같은 구간에서 새로 시작한다.

```text
auto on
seek 262 8
waitsongpos 264
decorender on 20 edge=low matid=none
decorender state
decorender compare
decorender compare
decorender compare
decorender compare
decorender compare
decorender state
decorender off
decorender state
```

반복할 on 인수는 다음 6개다.

```text
decorender on 20 edge=low matid=none
decorender on 20 edge=high matid=none
decorender on 20 edge=low matid=above
decorender on 20 edge=low matid=below
decorender on 20 edge=high matid=above
decorender on 20 edge=high matid=below
```

3. `selected>0`인지 먼저 확인한다. 키별 후보/블록/관리 수, `not-edge-block`, `split-order`, 예산 제외, managed-visible/hidden을 비교한다. `matched=False` 조합은 요청한 재질 방향의 유효 표본으로 세지 않는다.
4. compare의 실제 song 시각이 264~273초, 특히 기존 이상치 267.7초 전후를 포함하는지 확인한다. 기존 252초 구간과 숨은 렌더러가 보이게 되는 구간도 확인한다. `snapshotStable`, 전체/선택 bounds 안팎 차이, >0/>1/>2/>4/>8 픽셀 수와 max를 기록한다. 필요하면 같은 인수에 `floatcolor`를 추가해 대조한다.
5. 변화 복귀와 off 뒤 원본 화면을 확인한다. 캡처/진단을 켠 판으로 FPS 개선을 결론내리지 않는다. 새 실험에서 타입 규칙과 재질 ID 규칙을 확인하기 전에는 가장자리 선택 자체를 정렬 동일성 보장으로 해석하지 않는다.

task-observer v3.5.0의 기존 공유 로그 헤더와 활성 원칙을 확인했다. 공유 로그는 이번 쓰기 허용 범위 밖이므로 변경하지 않았으며 스킬 수정도 하지 않았다.
