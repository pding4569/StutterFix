# S2f decorender 결과 — 2026-10-20

`DecoRender.cs`의 선택·복귀 단위를 `(sortingLayerID, sortingOrder)` 키 전체로 바꾸고, 메시 내부 순서를 원본 재질 인스턴스 ID 오름차순으로 고정했다. 변경은 기존 `#if DEV || AUTOTEST` 안에 있다. **커밋·게임 실행·설치는 하지 않았다. 픽셀 동일성과 성능은 이번 작업에서 측정하지 않았다.**

## 변경 사항

- `manager.allDecorations`와 비활성 오브젝트를 포함한 씬의 `scrVisualDecoration`을 합쳐 중복 없이 조사한다. enabled/active/forceOff/alpha는 아틀라스 적격성에서 분리했다. 안 보이는 장식도 같은 키의 적격성·그룹 구성에 포함한다.
- 기존 셰이더·재질/MPB·Clamp·크기·반복·마스크/히트박스·점샘플링 제한을 유지한다. 한 장식이라도 부적격이면 `key-not-fully-eligible`로 키 전체를 제외한다. SpriteRenderer가 없는 장식은 자식 Renderer의 키를 부적격으로 등록한다. Renderer 자체가 전혀 없으면 정렬 키를 식별할 수 없다.
- 관찰 기간에 기존 움직임 판정(행렬·색·flip 변화)을 만족한 장식이 하나라도 있는 키만 남긴다. 그 키의 정적 형제와 숨은 형제는 함께 관리한다. 모든 장식이 정적인 키는 `static`으로 제외한다.
- 예산 초과로 큰 텍스처를 제거할 때도 그 텍스처를 사용하는 키 전체를 제거한다. 키 일부만 남기지 않는다.
- 선택 완료 직전, 비활성 오브젝트까지 포함한 씬 Renderer를 한 번 조사한다. 전체 후보의 SpriteRenderer를 관리 대상으로 계산하므로 같은 키의 숨은 형제는 비관리 충돌로 세지 않는다. 타일·행성·다른 Renderer 등이 같은 키에 있으면 `shared-order`로 키 전체를 제외한다.
- 키에 예정 그룹이 둘 이상이면 `split-order`로 전체 제외한다. 아틀라스 페이지뿐 아니라 기존 Unity layer·재질/MPB 실효 속성 차이로 생기는 그룹 분리도 보수적으로 제외한다. 서로 다른 원본 재질 인스턴스라도 실효 속성이 같으면 같은 그룹이다.
- 그룹 생성 시 한 번 `sharedMaterial.GetInstanceID()` 오름차순, 동률이면 원본 Renderer instanceID 오름차순으로 정렬한다. 부호 있는 ID를 그대로 비교한다. 이후 Build에서 거리나 보임에 따라 재정렬하지 않는다.
- 매 카메라 PreRender의 Build에서 모든 관리 장식의 SpriteRenderer enabled·activeInHierarchy·forceRenderingOff·color.a를 읽는다. 안 보이면 그 프레임의 정점/인덱스를 내보내지 않는다. 다시 보일 때도 그룹 내 고정 위치를 유지한다. 이전의 장식 내부 rendererEnabled/rendererColor 필드 대신 실제 SpriteRenderer 상태를 따른다.
- 원본을 잠시 숨긴 상태는 `Hidden`으로 구분한다. 자신의 forceRenderingOff를 게임의 숨김 상태로 오인하지 않도록 숨기기 직전 값을 보관하고, 매 카메라 PostRender에서 복구하며 그룹 렌더러도 끈다. 다음 카메라 PreCull/PreRender가 그룹을 다시 준비한다. compare의 원본 촬영·off·변화 복귀도 같은 복구 함수를 쓴다. 원래 forceOff였던 장식을 강제로 보이게 하지 않는다.
- 원본 재질 객체 교체를 포함한 기존 changed 검사에서 문제가 생기면 해당 키 전체를 복귀시킨다. 메시를 즉시 비우고 끄므로 복귀한 원본과 이전 메시가 같이 그려지지 않는다. 정렬 키가 다른 관리 키로 이동했다면 목적지 키도 복귀한다. `changes`에 `restored-keys`를 추가했다.

키마다 `[decorender] key-selection key=... total=... eligible=... reason=... eligibility-reasons={...}`를 남긴다. 상세 충돌 예시의 20개 제한과 별개로 **장식 키별 선택 로그는 전부** 남긴다. 적격성 사유는 그 키 안 부적격 장식의 사유별 수이며, 전체 키 제외 카운터와 구별한다.

## 순서 방향 근거

기존 런타임 자료를 읽기 전용으로 확인했다.

```text
D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\Mods\StutterFix\sortprobe\20261020-012144-430-3127f337\report.md
D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\Mods\StutterFix\sortprobe\20261020-012144-430-3127f337\trials.csv
```

보고서에는 `Status: complete`와 `Order = back to front`가 명시되어 있다. `SortProbe.Predict`의 material-id 가설은 `a.R.sharedMaterial.GetInstanceID().CompareTo(b.R.sharedMaterial.GetInstanceID())`이고, 동률은 Renderer ID 오름차순이다. CSV의 유효한 `materials` 행에서 `details`의 label→mat 대응을 읽어 `after_back_to_front`를 직접 재계산한 결과:

| 장식 수 | 재질 ID 오름차순과 일치 |
| --- | --- |
| n=2 | 50/50 (100%) |
| n=3 | 49/50 (98%) |
| n=6 | 50/50 (100%) |

따라서 이번 로그와 구현의 방향은 **재질 ID 오름차순 = 뒤→앞**이다. n=3의 예외 1건과 동일 재질 시험의 valid=0은 남아 있다. 동일 재질의 Renderer ID 동률 해소는 요청한 실험 규칙이며 일반적인 Unity 보장으로 주장하지 않는다. 이번 요청대로 고정 재질 ID 순서를 사용하므로 z/카메라 거리 변화에 따른 메시 내부 재정렬은 하지 않는다. 서로 다른 z가 있는 실제 곡에서의 동일성은 compare로 확인해야 한다. 큐/실효 재질 상태가 다르면 기존 그룹 분리로 키가 제외된다.

## state 항목

기존 필드를 유지하고 `key-state={...}`를 추가했다. off 뒤에도 마지막 스냅샷을 보존한다. off 뒤의 수치는 종료 직전 기록이며 현재 관리 중이라는 뜻은 아니다.

| 항목 | 의미 |
| --- | --- |
| `managed-keys` | 현재 남아 있는 관리 키 수. 관찰 중에는 0 |
| `managed` | 현재 관리 장식 수. 관찰 중에는 기존처럼 후보 수 |
| `managed-visible`, `managed-hidden` | 실제 SpriteRenderer 상태 기준 보이는/안 보이는 관리 장식 수. 관찰 중에는 0 |
| `excluded-keys` | 제외 키 합계. 실행 중 changed로 복귀한 키 포함 |
| `excluded-key-reasons={...}` | 키별 최초 제외 이유의 개수. `key-not-fully-eligible`, `static`, `atlas-budget`, `shared-order`, `split-order`, `changed-during-observation`, `changed` 등 |
| `vertices` | 현재 관리 장식 전체의 원본 정점 합계(보이지 않는 장식 포함) |
| `emitted-vertices` | 마지막으로 구성한 그룹 메시의 정점 수. 숨은 장식은 실제 Build에서 제외. 첫 Build 전에는 초기 토폴로지 수 |
| `changes/restored-keys`, `changes/restored-items` | 변화로 복귀/관찰 탈락한 키 수와 장식 수 |

기존 `excluded={...}`는 선택 단계에서 해당 이유로 제외한 **키 전체의 장식 수**를 센다. 예를 들어 키 전체 10개 중 1개가 부적격이면 `key-not-fully-eligible=10`, 키 카운터는 1이며, 구체적 부적격 사유 1건은 키별 `eligibility-reasons`에 나온다. 실행 중 복귀는 `changes`와 키 제외 사유 `changed`로 확인한다. `selected`는 최초 선택 장식 수이며 복귀 후 줄어드는 `managed`와 구별한다. 기존 `groups`는 할당한 그룹 수라서 복귀한 빈 그룹도 포함한다.

## 검증

| 확인 | 결과 |
| --- | --- |
| Release DEV 빌드 | 성공, 오류 0개 |
| Release PlayerAuto 빌드 | 성공, 오류 0개 |
| Release Player 빌드 | 성공, 오류 0개 |
| MSBuild Compile 항목 | DecoRender.cs: Player=0, PlayerAuto=1 |
| 실제 소스 메서드를 추출한 오프라인 검사 | 24개 시나리오 통과 |
| 실제 trials.csv 재계산 | 위 material-id 50/50·49/50·50/50 확인 |
| `py -3 tools/check-ui-encoding.py` | `SFENC001: clean` |
| `git diff --check` | 통과 |

빌드 로그는 `obj/decorender-5-build-{dev,autotest,player}.log`, 검사 생성기/프로젝트/로그는 `obj/decorender-5-check.py`, `obj/decorender-5-check/`, `obj/decorender-5-check.log`다. 오프라인 검사는 실제 선택·정렬·보임/숨김·키 전체 복귀 메서드를 추출해 가짜 Renderer/씬/메시 API로 실행했다. 전체 부적격 키 제거, 정적 형제 포함, 예산 키 제거, 외부 충돌, 그룹 분리, 음수 재질 ID 및 Renderer 동률, disabled/alpha0/inactive/forceOff, 숨김 값 복구와 키 전체 메시 제거를 확인했다. **Unity 렌더 콜백·GPU·픽셀 동일성 검증은 아니다.** FindObjectsOfType의 Unity 6 obsolete 경고가 추가로 발생하며 참조 버전 충돌 등 기존 경고도 남는다.

## 게임에서 돌릴 순서

이번 작업에서는 실행하지 않았다. 사용자가 빌드를 설치한 뒤 기존 재현 구간에서 다음 순서로 실행한다. compare 횟수와 실행 시각은 실제 로그의 song 값으로 확인한다.

```text
keep
open <Arche 경로>
auto on
seek 262 8
waitsongpos 264
decorender on 20
decorender compare
decorender compare
decorender compare
decorender compare
decorender compare
decorender state
decorender off
decorender state
```

1. `key-selection`의 total/eligible/사유, 최종 managed-keys/managed-visible/managed-hidden와 제외 키 개수를 확인한다. selected=0은 대체 화면 검증 표본으로 세지 않는다.
2. 곡 264~273초, 특히 267.7초 전후가 실제 compare에 포함되도록 추가 촬영한다. 기존 252초 구간도 확인한다. 숨은 형제가 보였다가 사라져도 순서가 유지되는지 원본/group/diff16을 확인한다.
3. `snapshotStable=True`, 전체/선택 bounds 안/밖 차이와 >0/>1/>2/>4/>8 픽셀 수를 확인한다. 필요하면 동일 구간을 `decorender on 20 floatcolor`로 반복한다. 기존 compare와 floatcolor 구현은 유지했다.
4. 재질/스프라이트/정렬 등 변화가 생기면 restored-keys와 restored-items, managed 감소, 원본 복귀를 확인한다. off 뒤 active=False와 원본 화면 복원도 확인한다.

외부 렌더러 충돌 조사는 선택 시점의 스냅샷이다. 이후 새 외부 Renderer 생성이나 비관리 Renderer의 키 이동을 계속 재조사하지는 않는다. 그런 변화 후에는 off/on으로 다시 선택한다. FindObjectsOfType(true)는 inactive 씬 오브젝트를 포함하지만 Unity의 DontSave 제외 규칙은 남는다. compare의 복제 카메라·후처리·시간 콜백 한계와 성능 미측정 상태도 그대로다.

task-observer v3.5.0의 기존 공유 로그 헤더와 활성 원칙을 읽었다. 공유 로그는 이번 쓰기 허용 범위 밖이므로 변경하지 않았고 스킬 생성/수정도 하지 않았다.
