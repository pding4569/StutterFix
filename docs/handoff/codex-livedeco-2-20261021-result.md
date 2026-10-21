# LiveDeco-2 결과 — 2026-10-21

**`mesh-enabled` 조건의 구조적 오판을 확인하고 수정했다. 게임 재측정은 하지 않았으며, 수정 후 GATE1은 미측정이다.** 게임 실행·설치·커밋을 하지 않았다. 일반 Player 제외를 유지했다.

## 확인한 원인

기존 `LiveDeco.Reason()`은 `d.meshRenderer && d.meshRenderer.enabled`만 읽었다. activeSelf, activeInHierarchy, forceRenderingOff는 확인하지 않았다. 이는 캐시 필드나 activeSelf를 잘못 읽은 문제가 아니라, 실제 `Renderer.enabled`를 읽으면서 오브젝트 활성 조건을 빠뜨린 문제다.

현재 설치된 게임 DLL을 읽기 전용으로 분석했다. `scrVisualDecoration.EnableMeshRenderer(bool)`은 `meshRendererObj.SetActive(bool)`로 전환하고 private `meshRendererEnabled`에 값을 기록한다. `Renderer.enabled`를 끄는 방식이 아니다. 따라서 **enabled=true인 비활성 메시 자식도 기존 분류에서는 제외되는 오류**가 확정된다. 해당 상태가 Arche 262·270초 각각 몇 개인지는 기존 로그에 없으므로 전부가 이 원인이라고 단정하지 않는다.

`UpdateShader()`의 메시 전환 경로에서는 spriteRenderer를 끄고 메시 오브젝트를 켠다. `SetBlendMode(None)`도 메시용 재질을 생성·할당할 수 있으므로 메시 컴포넌트/재질의 존재 자체가 메시 모드의 증거는 아니다. blend 효과는 None 이외 모드일 때 켠다. 메시 사용과 blend 사용도 같은 조건이 아니다.

근거: [게임 메서드 발췌와 DLL SHA256](livedeco-2-static-evidence.txt), [IL 필드·호출 목록](livedeco-2-il-members.log), [기존 실측](livedeco-run1-evidence.txt). BlendModeEffect 단독 타입을 네임스페이스 없이 조회한 보조 시도는 실패했으며 결론 근거로 쓰지 않았다. 게임 DLL의 Unity 메서드는 호출하지 않았다.

## 수정과 다음 로그

- 메시 제외 조건을 다른 렌더러와 같은 `존재 && 유효 scene && enabled && activeInHierarchy && !forceRenderingOff`로 바꿨다. GameObject나 Renderer 상태는 변경하지 않는다.
- 실제 live 메시와 sprite가 동시에 있는 장식은 `remaining_reason=mesh-live:blend=<이름>(<숫자>)`로 보수적으로 제외한다. 나머지 mask/hitbox/repeat/blend/MPB/재질 검사는 유지했다. `basic_candidates`는 MPB 제외 직전, `owned_candidates`는 최종 후보다.
- `classifier_version=2`로 새 코드 사용 여부를 확인한다.
- 전체 scene 이미지에 `mesh_state` 조합별 분포를 기록한다. sprite live, mesh 존재, 게임의 private mesh 모드, mesh enabled/activeSelf/activeInHierarchy/forceRenderingOff/scene 유효/live, 카메라 layer 포함, sharedMaterial 유무와 shader, 게임 blend 모드, sprite/mesh BlendModeEffect 각각의 enabled/isActiveAndEnabled를 포함한다. `sharedMaterial`만 읽으며 `material(s)`를 만들지 않는다.
- 각 조합에 `count`, 전체 이미지 대비 `images_pct`, sprite-live 분모 대비 `sprite_live_pct`, sprite 또는 mesh-live 합집합 대비 `union_live_pct`를 기록한다. 해당 분모에 속하지 않는 조합의 live 비율은 0이다. 비율은 반올림되어 합이 정확히 100이 아닐 수 있다.
- `mesh_diagnostics`에 `legacy_mesh_false_rejects`, `mesh_only_live`, `mesh_and_sprite_live`, `image_union_live`, `distribution_total`을 남긴다. `legacy_mesh_false_rejects`는 sprite-live이면서 enabled 메시가 실제 live가 아닌 수로, 앞선 mask/hitbox 등 다른 제외 검사까지 통과한다는 뜻은 아니다. 분포 합계=전체 이미지 검사를 추가했다.
- 메시 전용 장식은 게이트 후보로 승격하지 않는다. 연결된 live 메시 장벽은 `barrier_type=image-mesh:blend=<이름>(<숫자>)`로 분리하고 외부 장벽으로 유지한다. 다른 비소유 렌더러도 그대로 유지한다.

기존 게이트 분모는 **sprite-live**로 유지했다. 메시 전용 장식까지 포함한 모든 장식의 소유율이라는 의미는 아니다. 별도 합집합 분포로 메시 전용 비율을 확인할 수 있다. mesh-live도 frustum·알파·geometry·실제 draw를 증명하는 값은 아니며, 상태상 live인 보수적 장벽이다.

`barrier_runs`는 비소유 슬롯이 후보 사이에 끼면서 나뉘는 구간 수(첫 구간 포함)다. `mesh_runs`는 거기에 정렬 키·재질 내용 서명·Unity layer 분리까지 더한 수다. 게이트는 여전히 **mesh_runs≤200, owned_candidates/live≥80%**이며 순서 불확실성은 INCONCLUSIVE다. 수치를 좋게 만들기 위해 MPB나 정렬 장벽을 우회하지 않았다.

## 실행 준비

기존 `tools/optimization/livedeco-steps.py`와 25단계 순서를 변경하지 않았다. 기존 실측에 기록된 실제 Arche 경로로 [재실행 JSON](livedeco-2-steps.json)을 생성했다. JSON 생성만 수행했으며 아래 명령은 **사용자 실행용이고 이 작업에서 실행하지 않았다.**

```powershell
py -3 E:\StutterFixWork\run_json.py docs/handoff/livedeco-2-steps.json wt-livedeco livedeco-gate1 --probe --keys livedeco,archegpu
```

262·270초 모두 `classifier_version=2`, 상태 분포, legacy 오판 수, 실제 mesh-only 수/모드/비율, `basic_candidates`, `owned_candidates`, `coverage_pct`, `mesh_runs`, GATE1을 확인한다. 기존 2078/2266개 전부가 mesh-enabled였다는 로그로 수정 후 후보 수를 역산하지 않는다. 다음에 MPB 등이 주된 제외 사유가 되더라도 이번 수정이 그것을 해결했다는 뜻은 아니다.

| 항목 | 262초 | 270초 |
|---|---|---|
| 수정 후 상태 분포·오판 수·메시 모드 비율 | 미측정 | 미측정 |
| basic / owned / coverage / mesh_runs | 미측정 | 미측정 |
| 수정 후 GATE1 | 미측정 | 미측정 |

## 오프라인 검증

| 검사 | 결과 | 로그 |
|---|---|---|
| Release DEV 빌드 | 오류 0, 경고 38 | [DEV](livedeco-2-build-dev.log) |
| Player 빌드 | 오류 0, 경고 4 | [Player](livedeco-2-build-player.log) |
| PlayerAuto 빌드 | 오류 0, 경고 38 | [Auto](livedeco-2-build-auto.log) |
| LoadCheck DEV / Player / Auto | 268 / 229 / 268곳, 틀림 모두 0 | [DEV](livedeco-2-loadcheck-dev.log), [Player](livedeco-2-loadcheck-player.log), [Auto](livedeco-2-loadcheck-auto.log) |
| LiveDeco fixture | 22,950 checks 통과 | [fixture](livedeco-2-fixture.log) |
| edition 메타데이터 | 10 checks, 일반 Player 타입/참조 없음 | 같은 fixture 로그 |
| MatShare 회귀 fixture | 25,401 checks, 오류 0 | [MatShare](livedeco-2-matshare-fixture.log) |
| Python steps | 25단계·독립 시점·UTF-8 CLI 통과 | [steps](livedeco-2-steps-check.log) |
| git diff --check | 통과 | 최종 로컬 검사 |

추가 fixture는 렌더러 상태 32개 조합과 비활성 메시 후보 사이에 실제 live 메시 장벽이 끼는 경우를 검사한다. Unity 실제 상태/콜백/픽셀/성능 검사는 아니다. LoadCheck에서 private mesh 모드와 blend 모드 필드 참조도 통과했지만 기존 동적 이름 5곳은 실행 중 확인 대상이다. 기존 어셈블리 충돌·deprecated·미사용 필드 경고는 남는다.

## 작업 관찰

task-observer v3.5.0과 사용자 범위 관찰 헤더·활성 원칙을 읽었다. 사용자 범위 관찰 저장소는 쓰기 허용 범위 밖이므로 변경하지 않았다. 이번 작업의 관찰: 렌더러 enabled 단독 판정은 오브젝트로 모드를 전환하는 게임에서 오판을 만든다. 실측 오류를 수정할 때 기존 snapshot에서 빠진 필드를 추정값으로 채우지 않고 다음 센서스에서 조합별로 기록해야 한다. 별도 스킬·메모리는 수정하지 않았다.
