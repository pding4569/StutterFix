# Codex 임무 livedeco-2: 게이트 1 FAIL 이 분류 오류로 보임 — 진단·수정

작업 폴더 `E:\StutterFixWork\wt-livedeco`. 게임 실행·설치·커밋 금지. 결과 `docs/handoff/codex-livedeco-2-20261021-result.md` + `-o`(10줄). 근거 `docs/handoff/livedeco-run1-evidence.txt`.
## 실측(Arche 262·270초)
`images_all=28828 live=2078/2266 basic_candidates=0 owned_candidates=0 remaining_reason=mesh-enabled count=2078(전부) GATE1=FAIL`. **live 장식 전부가 `mesh-enabled` 로 탈락**, `basic_candidates=0`. 그런데 재질 서명은 99.4%가 기본형이고, `scrVisualDecoration.SetBlendMode`(IL)는 `blendMode>0` 일 때만 sprite/mesh BlendModeEffect 를 켠다. 비소유 장벽 렌더러(MeshRenderer 233·SpriteRenderer 124·Particle 25·Line 2 슬롯)는 따로 센다. → "mesh-enabled" 판정이 잘못되었을 가능성이 높다(예: 장식 프리팹의 `meshRenderer`/`meshRendererMat` 컴포넌트가 항상 존재하거나, `enabled` 가 아니라 다른 상태를 읽음, 또는 `Renderer.enabled` 대신 activeSelf).
## 해야 할 것
1. `mesh-enabled` 탈락 조건을 코드로 확인하고(어떤 필드를 읽는지), 장식 프리팹의 meshRenderer 의 실제 상태(존재/enabled/`forceRenderingOff`/`sharedMaterial` 유무/`BlendModeEffect` 활성)를 센서스에 **분포로** 로그(몇 개가 어떤 조합인지). 진짜 메시 모드로 그려지는 장식이 있는지, 아니면 비활성 메시 컴포넌트를 오판한 것인지 확정.
2. 오판이면 분류를 고치고 `owned_candidates`/`coverage_pct`/`mesh_runs`(비소유 렌더러가 끼는 횟수)가 의미 있게 나오게 한다. 진짜 메시 장식이면 소유 불가 이유를 정확히 분류(blend 모드 종류 등)하고 그 비율을 보고.
3. 고친 뒤 같은 steps(`tools/optimization/livedeco-steps.py`)로 게이트 1 숫자가 나오게(내가 `run_json.py` 로 돌림). 통과 기준 그대로(런 수 ≤200, 소유 후보 ≥80%). 빌드 3종 오류 0 + LoadCheck + fixture. Player 제외 유지.
