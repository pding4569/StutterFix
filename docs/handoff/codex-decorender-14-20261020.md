# Codex 과제 S8: decorender — 프레임마다 도는 "안전 검사"가 메인 스레드의 40% (프로파일)

같은 규칙(E:\StutterFixWork\wt-decorender2, 커밋·게임 실행·설치 금지, `#if DEV || AUTOTEST`, 사용량 절약). 결과 문서 `docs/handoff/codex-decorender-14-20261020-result.md`.

## 게임 결과 (S7 빌드, `decorender on 0 scope=all atlas=128`, Arche 곡 262~270초, 메인 스레드 표본 프로파일 8,056개, UnityPlayer 심볼 해석)
단계 계측의 빌드(collect 0.79 + 정점 0.16 + 업로드 0.64 + topology 0.15 ms)는 정상으로 내려왔지만 **프레임 전체는 아직 느리다**(6초 `fuse` 209프레임, 끈 판은 약 760). 프로파일(스택 어디든, 비율은 메인 스레드 표본):
- **`DecoRender:PreCull` 46.3%** ← `Camera.FireOnPreCull`. 그 안: **`PrepareFrame` 42.1%**:
  - **`PruneChanged` 16.7%**
  - `Build` 12.1%
  - **`AuditLateBarriers` 10.6%**
  - **`UniformsUnchanged` 10.3%** (리프에 `Material::GetTextureScaleAndOffset`, `ShaderPropertySheet::FindPropertyIndex`, `Renderer_CUSTOM_GetSharedMaterial`, 문자열 변환 `convert_to_utf8`, `base_hash_map<char const*,...>::find` — 즉 재질 속성 이름으로 읽기)
  - `Takeover` 5.9%
  - 기타: `UnityEngine.Object:IsNativeObjectAlive`/`CompareBaseObjects`, `Renderer.get_enabled/forceRenderingOff/GetSharedMaterial`, `Component.get_gameObject`(3.7%), `GameObject::IsActive`, `Dictionary<Renderer, RenderOrder>.FindEntry`, `HashSet.Contains` — 전부 **렌더러·재질의 상태를 매 프레임 반복 읽는 안전 검사**의 흔적.
- 합쳐서 **약 40%(대략 3ms 이상)가 DecoRender 의 검사 코드**다. 이 단계들은 아직 단계 계측 표에 없다(`PruneChanged`, `AuditLateBarriers`, `UniformsUnchanged`, `Takeover` 를 계측에 추가해서 합이 맞게 하라).

## 고칠 것 (목표: 이 검사들의 합 ≤0.3ms/프레임)
1. **`PruneChanged`**: 무엇을 전수 훑는지(가져간 28k 장식의 sprite/material/sorting/mask/렌더러 소유 확인)를 확인하고, **이벤트 기반 + 시간 분할**로 바꾼다. 이벤트: 게임에서 `sortingOrder`/`sprite`/`sharedMaterial`/마스크가 바뀌는 곳을 IL 스캔(`tools/ilscan`, CLAUDE.md 참고: 예 `scrVisualDecoration.SetSprite`, `scrDecoration.SetDepth`, `SetMaskingType`, `SetMaskingDepth`, `EnableMeshRenderer` …)으로 찾아 Harmony postfix 로 "그 장식 dirty" 표시(새 패치는 `Main.Unload` 에서 해제하고, `decorender off`/모드 언로드 때 되돌린다). 훅이 못 잡는 변화 대비 안전망은 프레임마다 **전체의 1/N(예 1/128)만 라운드로빈**으로 확인(전수 금지).
2. **`AuditLateBarriers`**: 폴링 대상이 ~1,600개로 줄었다는데도 10%다. 무엇이 비싼지(`enabled`+`forceRenderingOff`+`activeInHierarchy` 3번 읽기×1,600 = 네이티브 호출 4,800번?) 확인해, 매 프레임이 아니라 (a) 장벽 렌더러를 끄고 켜는 코드(TileCull 등 이 모드가 직접 바꾸는 곳)에 훅으로 이벤트화 가능한 건 이벤트로, (b) 나머지는 N프레임마다(예 8프레임)·라운드로빈으로. 켜지는 순간 한두 프레임 늦는 것은 허용하되 늦을 때 그 키를 원본 렌더러로 되돌리는 안전 규칙은 유지.
3. **`UniformsUnchanged`**: 구간(run)마다 빌린 재질의 속성(`_MainTex` ST, Repeat, PixelSnap 등)이 안 바뀌었는지 이름 문자열로 읽는다. 속성 이름은 `Shader.PropertyToID` 로 한 번만 정수화하고, 확인은 on 때 + 이벤트(우리가 재질을 바꿀 때) + N프레임마다 소수만. 문자열 API 금지.
4. **`Takeover` 5.9%**: `on` 이후에도 프레임마다 무엇을 하는지 확인하고(재시도/재개? 대기 장식 승격?) 매 프레임이 아니게.
5. **`Build` 12.1%**: 계측상 1.7ms 인데 프로파일 비율이 큰 이유를 확인(`IsNativeObjectAlive`/`Dictionary<Renderer, RenderOrder>.FindEntry`/`Component.get_gameObject` 호출이 안 잡힌 것일 수 있음). 장식 객체당 Unity 객체 호출을 줄이도록 캐시(예: 활성 확인은 이벤트 기반).
6. 단계 계측에 위 모든 항목 추가, `unaccounted-ms` ≈ 0 유지. 목표: 프레임당 DecoRender 총합 ≤0.5ms, `fuse` 창 프레임이 끈 판 이상.
7. 정확성: `compare` 최대 ≤2/255 유지, 변화 훅 지연으로 이상치가 생기는지 `state` 에 "늦게 잡은 변화 수" 카운터.

## 결과 문서에
각 검사의 원인·바꾼 방식, 새 훅 목록(파일·함수·해제 위치), 계측 항목, 빌드 결과, 게임에서 돌릴 순서(`decorender on 0 scope=all atlas=128`, `state`, `fuse begin/end`, `compare`×N, `profsamp` 도).
