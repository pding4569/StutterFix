# GPU 장면 그룹 분리 진단 — 2026-10-20

요청한 `gpusplit` 명령, 오프라인 로그 요약기, 검사와 후속 측정 steps를 만들었다. 게임 실행·설치·게임 명령 전송·커밋은 하지 않았다. 실측 GPU 몫과 절약 효과는 아직 미검증이다. 인계 문서가 가리킨 `docs/handoff/archegpu-evidence/`는 이 작업 트리에 없으므로 배경의 4.24/4.06/3.4ms를 새 근거로 재확인하지 않았다.

## 명령과 분류

`DEV || AUTOTEST` 전용이며 기본 꺼짐이다. 일반 Player에는 소스와 명령이 포함되지 않는다. 새 설정·Harmony 패치·네이티브 코드·설치 파일을 추가하지 않았다.

```text
gpusplit hide deco-image
gpusplit hide deco-text
gpusplit hide deco-particle
gpusplit hide tiles
gpusplit hide planets
gpusplit hide other
gpusplit hide deco-text+deco-particle
gpusplit hide all
gpusplit refresh
gpusplit state
gpusplit hide none
gpusplit off
```

`GpuSplit.cs`는 유효한 scene의 렌더러를 본 카메라(`Camera`라는 이름의 활성 GameObject에 있는 유일한 카메라)의 cullingMask로 제한한다. Canvas 아래 렌더러와 본 카메라 mask 밖의 UI 전용 렌더러는 제외한다. 카메라가 없거나 모호하면 중단하고 복원한다. UI와 본 카메라가 같은 layer를 공유하는 Canvas 밖 특수 렌더러의 귀속은 실제 화면에서 확인해야 한다. 프러스텀·현재 활성 여부로 cohort를 줄이지 않아, 현재 비활성이던 장식도 이후 켜지면 숨겨진다.

분류 우선순위는 다음과 같다. 한 렌더러는 정확히 한 그룹에 들어간다.

| 그룹 | 소유 관계 / 렌더러 기준 |
|---|---|
| deco-particle | `scrDecoration` 아래 `ParticleSystemRenderer` |
| deco-text | `scrDecoration` 아래 TMP_Text/TextMesh 소유 렌더러 |
| deco-image | `scrDecoration` 아래 나머지 SpriteRenderer |
| tiles | `scrFloor` 아래 렌더러 및 `floorRenderer.renderer`/`topGlow` 직접 참조; 아이콘 포함 |
| planets | `scrPlanet`/`PlanetRenderer` 아래 렌더러 및 PlanetRenderer의 tailParticles, tailParticlesCoop, coreParticles, sparks, deathExplosion 참조 아래 렌더러 |
| other | 위에 속하지 않는 본 카메라 scene 렌더러 |

기존 `GpuCensus.IsDecoration`/`DecoCensus`의 `scrDecoration` 소유 관계와 `TileCull`의 floorRenderer/topGlow 기준을 재사용했다. `all`은 UI를 제외한 여섯 그룹의 합집합이다. `other`에 장식 소유의 미분류 렌더러가 포함될 수 있다. 현재 scene에서 그룹별 census 수를 hide/refresh 로그에 남긴다. 이 수는 실제 draw 수가 아니다.

## 숨김과 복원

`forceRenderingOff`만 변경한다. `Renderer.enabled`, GameObject 활성, 스크립트·판정·충돌 상태는 변경하지 않는다. 최초 값을 저장하므로 원래부터 숨겨진 렌더러는 복원 후에도 숨김을 유지한다. 같은 그룹을 다시 hide하거나 refresh할 때 최초 값을 덮어쓰지 않고 새 렌더러만 추가한다. 그룹을 바꾸면 이전 그룹을 복원한 뒤 새 그룹을 선택한다. refresh는 기존 cohort를 유지하며 새 후보를 추가하는 동작이다.

hide/refresh에서만 scene 탐색·분류 목록을 만든다. 숨김 중 Camera.onPreCull에서 기존 목록을 프레임당 한 번 순회해 외부 코드의 해제를 다시 숨긴다. 이 경로에는 새 목록·LINQ·문자열·프레임당 할당이 없다. `external_unhide_writes`로 재적용 횟수를 기록한다. `gpusplit off`, `hide none`, `gpuprobe off`, Main.Unload, 진단 오브젝트 OnDisable/OnDestroy 및 scan 실패 경로에서 살아 있는 렌더러의 최초 값을 복원한다. 파괴된 렌더러는 건너뛴다. 꺼진 뒤 callback과 cohort를 해제한다.

`gpuhide`/`gpucensus`와 동시 실행은 거부한다. 다른 화면 변경 진단은 사용하지 않는 판을 권한다. 숨김 중 외부 모듈이 같은 플래그를 수정했다면 복원은 명령 시작값을 우선한다. 게임이 그 사이 의도한 최신 값을 재구성하지는 않는다.

## 후속 `sf_run` steps — 이번 작업에서 실행하지 않음

담당자가 별도로 허용된 환경에 PlayerAuto와 기존 독립 gpuprobe DLL을 준비한 뒤 실행한다. 아래는 새 프로세스 한 번의 곡 재생에서 4초 창 8개를 모으는 절차다. 창 시작은 262, 268, 274, 280, 286, 292, 298, 304초이고 마지막 종료는 308초다. 시작 전후에 scan/워밍업 여유를 두었다. 258~278초만의 측정이 아니므로 그 구간에만 일반화하지 않는다. 로딩을 포함한 외부 제한은 약 5분이다.

```python
from pathlib import Path
chart = str(next(Path('D:/얼불춤 맵 파일/Arche').glob('*FULL.adofai')))
groups = ['none', 'deco-image', 'tiles', 'planets',
          'deco-text+deco-particle', 'other', 'all', 'none']
steps = ['timeout 180', 'auto on', 'open ' + chart,
         'archegpu on', 'gpuprobe on', 'seek 262 8',
         'waitsongpos 260 60']
for i, group in enumerate(groups):
    start = 262 + 6 * i
    steps += ['gpusplit hide ' + group, f'waitsongpos {start - 1} 20']
    if group != 'none':
        steps += ['gpusplit refresh']
    steps += [f'waitsongpos {start} 20', 'archegpu stamp', 'gpuprobe reset',
              f'waitsongpos {start + 4} 20', 'archegpu stamp',
              'gpuprobe state', 'gpusplit state', 'gpusplit hide none']
steps += ['gpusplit off', 'gpuprobe off', 'archegpu off', 'stop', 'quit']
sf_run(steps=steps, timeout_min=5, tag='gpusplit-arche-8windows')
```

각 4초 창의 시작·종료 stamp와 reset/state는 서로 다른 프레임에서 처리된다. 실제 경계는 로그로 확인하고 steps의 초 지정만으로 정확히 4초라고 단정하지 않는다. 시작 직전 refresh 이후에 새로 생성된 렌더러는 다음 hide/refresh까지 추가되지 않는다.

## 로그 해석

```text
py -3 tools/optimization/gpusplit-summary.py <이 판의 Player.log> -o <요약.md>
```

요약기는 reset으로 창을 나누고 앞뒤 stamp, completed, 본 카메라 ID/active_frames, `mean_frame_ms`를 결합한다. 여덟 창 순서가 다르거나 여러 판이 섞인 로그는 거부하므로 판 하나의 로그를 전달한다. 창마다 본 카메라 표본이 없거나 모호한 경우, ID가 달라진 경우, active_frames와 completed가 다른 경우, ready/계측 오류, 측정 중 hide/refresh, stamp 누락 또는 3.5~4.5초 범위 이탈도 거부한다. snapshot busy이면 다음 그룹으로 진행하지 말고 해당 창의 state를 재시도하거나 판을 폐기한다.

기준값은 앞뒤 기준 창 평균이다. 표의 `기준−숨김`은 해당 그룹 제거 차이 추정치이며 음수를 그대로 보존한다. 앞뒤 기준 차이와 그 절댓값을 드리프트 오차 지표로 표기한다. 통계적 신뢰구간은 아니다. 다섯 개 개별/복합 그룹 몫의 합, 전체 숨김 차이, `그룹 합−전체 숨김 차이`도 출력한다. invalid/ring_full/not_ready 카운터는 별도 표시한다. 실패·미회수·워밍업·드리프트가 큰 판은 인과 추정에서 제외한다.

본 카메라 시간에는 필터가 포함된다. 필터 합을 다시 더하지 않는다. 숨김이 컬링 등 CPU 작업도 바꾸지만 이 값은 CPU Camera.Render 타이머가 아닌 GPU timestamp다. 다만 GPU **경과 시간**에는 제출 간격의 유휴가 섞일 수 있어 CPU 영향과 완전히 무관하다고 단정하지 않는다. 목록 재적용 CPU 비용, 시간에 따른 내용 변화, 공유 패스와 오버드로 변화가 있어 순수 그룹 shader 시간이나 MsGPUBusy의 증거로 부르지 않는다. 앞뒤 두 기준이 같아도 중간 내용의 변화가 없다는 증명은 아니다.

## 오프라인 검증

- Release 개발자 / Player / PlayerAuto 빌드 모두 오류 0. 경고는 각 build 로그에 보존했다.
- LoadCheck: 개발자 266곳, Player 229곳, PlayerAuto 266곳, FieldRefAccess 불일치 모두 0. 동적 이름 5곳은 실행 시 확인 대상이다.
- ILScan: 일반 Player에서 GpuSplit 진단 형식 제외 확인. 개발자/PlayerAuto에는 포함된다. `*-types.txt` 참조.
- `dotnet run --project tools/optimization/GpuSplitCheck -c Release`: 1,040개 검사, 오류 0. 512개 조건 조합의 분류/배타성, 원래 false/true 복원, 중복 hide/refresh, 외부 해제 재적용, 파괴된 객체, 중복 restore, enabled/active 보존과 Enforce 10,000회 할당 0을 확인했다.
- `py -3 tools/optimization/test-gpusplit-summary.py`: 합성 로그 검사 4개 통과. 기준/드리프트/불일치 수치, UI 카메라 제외, 누락/오류/카메라 표본 불일치/잘못된 창/판 혼합/창 중간 변경 거부를 확인했다.
- Python py_compile 및 `git diff --check` 통과.

검사는 Unity-free 정책과 저장 ledger를 .NET 8에서 직접 실행한 범위다. Unity/Mono의 실제 소유 관계·새 장식 생성·게임 내 복원·할당·GPU 시간은 아직 검증하지 않았다. 게임을 실행하지 않은 조건에서 측정 결과를 만들지 않았다. 증거는 `codex-gpusplit-20261020-*-build.txt`, `*-loadcheck.txt`, `*-types.txt`, `*-fixture.txt`, `*-summary-check.txt`에 남겼다. 10줄 요약은 `codex-gpusplit-20261020-o.txt`다.
