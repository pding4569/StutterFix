# gpusplit-3 결과 — 2026-10-21

게임 실행·제어·설치·커밋은 하지 않았다. 작업 위치는 `E:\StutterFixWork\wt-archegpu`다. 기존 미커밋 gpusplit-2 변경과 파일을 유지했다. 제품 최적화와 메시 변경은 추가하지 않았다.

**진단 구현과 오프라인 검증 완료. `onscreen=0`의 런타임 원인 확정, 약 1,300개 복구 확인, 픽셀 동일 표본 및 새 GPU 절감 실측은 미완료다.** 게임을 실행하지 않았으므로 이 항목을 성공으로 처리하지 않는다. 인계서의 약 5.7ms/이미지 전체 숨김 약 3.3ms 및 draw/SetPass 수는 제공된 이전 실측이다. 이번 변경으로 같은 화면에서 그만큼 절감했다고 주장하지 않는다.

## 1. 분류 진단과 수정 범위

기존 `GpuSplit.Scan`은 자동 시험 명령을 실행하는 시점의 카메라와 native `Renderer.bounds` 하나로 멤버십을 결정했다. 이후 `Enforce`는 어떤 카메라든 첫 callback에서 스냅샷 목록을 다시 숨겼다. 선택 당시 알파가 0인 장식은 이후 페이드 인해도 목록에서 빠지지 않았다. **스냅샷 유지 문제와 본 카메라 이외 callback 사용은 코드에서 확인한 결함이다. 이것만으로 이전 모든 bounds가 frustum 밖이었던 이유까지 입증한 것은 아니다.**

수정 사항:

- `gpusplit census`는 다음 본 카메라 `onPreCull`에서 실행한다. 정확한 이름 `Camera`인 활성 scene 카메라가 유일해야 하며, 모호하면 실패한다.
- 판정 평면은 `camera.cullingMatrix`로 계산한다. 기존 Camera overload 및 `projectionMatrix * worldToCameraMatrix`와의 차이도 센다. 직교 여부만으로 별도 임의 화면 사각형을 만들지 않는다.
- bounds는 native 월드 AABB와 `localBounds`를 `localToWorldMatrix`로 변환한 월드 AABB의 합집합이다. native bounds가 뒤처져도 로컬 bounds가 화면과 겹치면 invisible로 숨기지 않도록 보수적으로 잡는다. sliced/tiled 크기는 sprite 원본 크기 대신 renderer localBounds를 사용한다. Unity의 bounds 자체를 수정하지 않는다.
- 첫 이미지 5개와 별도로 enabled·active·unforced 이미지 첫 5개의 native/local/conservative center·extents, native/conservative/projection 교차 결과, viewport center, 현재 알파를 로그로 남긴다.
- 카메라 위치, ortho/size/aspect, near/far, RT 크기, view/projection/culling 행렬, 6개 frustum 평면 normal·distance를 기록한다.
- `image_live_onscreen_unforced count=N`, `bounds_recovered`, `culling_vs_projection_disagree`, `legacy_vs_culling_disagree`를 추가했다. raw onscreen 수에는 비활성/이미 강제 숨김 장식도 포함되므로 1,377과 비교할 값은 **unforced count**다. 카메라 AABB 후보 수는 실제 draw 수가 아니다.

원인 분리 방법:

| 새 로그 | 해석 가능한 범위 |
|---|---|
| preCull native도 정상이고 이전 snapshot만 전부 outside | 명령 시점과 렌더 시점 차이의 근거. 이전 snapshot 행렬이 없어 과거 원인 완전 재구성은 불가 |
| native 밖, conservative 안, `bounds_recovered>0` | native/local bounds 불일치의 근거 |
| culling와 projection 평면 결과 불일치 | 별도 culling matrix 사용 여부를 조사할 근거. projection 판정을 임의 정답으로 바꾸지 않음 |
| 새 preCull에서도 unforced onscreen 0 | 해결 확인 실패. bounds의 viewport z, near/far 및 행렬 로그로 후속 조사해야 함 |

게임 DLL을 읽기 전용 ILScan으로 조사했다. `scrCamera.LateUpdate`가 `SetPosition`을 호출하고 `UpdateSize`가 본 카메라 orthographicSize를 쓰는 경로는 확인했다. 이것은 snapshot 시점 민감성을 뒷받침하지만 실패 프레임의 값을 재현하지는 않는다. 근거는 [camera-il.txt](codex-gpusplit-3-20261021-camera-il.txt)다. 새 Harmony 패치는 없다.

## 2. 동적 필터

```text
gpusplit hide live
gpusplit hide live:visible
gpusplit hide live:invisible
gpusplit hide live:tiny
gpusplit hide live:invisible+tiny
gpusplit census
gpusplit reset
gpusplit state
gpusplit off
```

모든 live 필터의 공통 집합은 scene 이미지 장식 중 enabled·active이며 현재 `forceRenderingOff=false`인 SpriteRenderer다. 본 카메라 cullingMask 밖과 Canvas/text 소유 렌더러를 제외한다.

| 필터 | 매번 본 카메라 preCull에서 평가하는 조건 |
|---|---|
| `live` / `live:all` | 공통 집합 전체 |
| `live:visible` | frustum 교차 AND 현재 color alpha > 0 |
| `live:invisible` | frustum 밖 OR 현재 color alpha <= 0 |
| `live:tiny` | 투영 AABB 면적 < 화면의 0.01% |
| `live:invisible+tiny` | invisible OR tiny |

알파 임계는 0, tiny 임계는 0.01 screen percent로 고정했다. 2560×1440에서 0.01%는 약 368.64 픽셀 면적에 해당하나 실제 피복 픽셀 수가 아니다. 기존 `small=1`의 1%와 다르다. 면적은 화면 밖을 자르지 않은 보수적 AABB 투영이다. near plane을 가로지르는 bounds는 무한 면적으로 처리해 tiny에서 제외하며, 완전히 near 뒤인 것은 0이다. tiny도 화면 변화가 가능한 숨김 진단이다.

실제 멤버십은 매 preCull 현재 enabled/active/forced/알파/위치/크기로 재판정한다. 숨긴 대상은 해당 본 카메라 postRender에서 원래 force 값으로 복원한다. 다음 프레임 LateUpdate에도 복원 안전망을 둔다. off/disable/destroy에서도 복원한다. 다른 카메라에 스냅샷 숨김을 강제하지 않는다. 기존에 강제 숨겨진 대상은 건드리지 않는다.

**발견 목록은 활성화/refresh/census 시 캐시한다.** 그 뒤 생성되거나 새로 이미지 소유가 된 렌더러는 `refresh` 또는 재활성화가 필요하다. 기존 캐시의 페이드/이동/활성화는 매번 반영한다. 매 프레임 전체 Resources 검색이나 GetComponent 재분류를 하지 않는다. 픽셀 비교는 독립적으로 최신 Resources 목록을 검색하므로, 실행 중 렌더러가 생성되면 timing 집합과 비교 집합을 맞추기 위해 성능 창 전에 refresh해야 한다.

`state`는 선택 수의 마지막 값·최소·최대, 본 카메라 렌더 횟수, 분류+숨김+복원 평균/최악 CPU ms를 표시한다. `reset`으로 262초 측정 시작 전에 통계를 비운다. 최악값은 preCull와 postRender의 합이며 callback가 중첩되는 특수 렌더 경로는 별도 검증 대상이다.

## 3. 같은 프레임 RGBA 비교

```text
gpusplit off
gpusplit pixeldiff live:invisible
gpusplit pixeldiff live:tiny
gpusplit pixeldiff live:invisible+tiny
gpusplit pixeldiff live:visible
```

명령 하나당 자연 본 카메라 렌더 직전 preCull에서 **입력 RT**를 보관하고 같은 프레임 EndOfFrame에서 본 카메라만 수동 렌더한다. UI/Overlay 카메라는 렌더하지 않는다. 이미 그려진 출력 RT를 입력으로 쓰면 숨긴 장식이 바탕에 남아 거짓 0이 나올 수 있으므로 그렇게 하지 않는다.

정확히 같은 RT descriptor와 입력 seed로 `A0(숨김 없음) → A1(없음) → B(현재 필터 숨김) → A2(없음)`를 렌더한다. 요청한 A/B 두 렌더 외 두 대조군을 추가한 것이다. 다음을 비교한다.

1. 자연 렌더 결과와 A0: baseline 재현성.
2. A0/A1 및 A1/A2: 재렌더 callback/시간성 효과의 불안정 여부.
3. A1/B: `rgba_max`(0..255), `different_pixels`(RGBA 중 하나라도 다르면 1픽셀).

로그 예시 형식(아래 값은 실측이 아님):

```text
[gpusplit] pixeldiff filter=... frame=... camera_id=... size=... selected=...
rgba_max=... different_pixels=... control_max=... control_pixels=...
natural_max=... natural_pixels=... valid=... sample_equal=...
scope=main-camera-RT-RGBA8-only timing_contaminated=1
```

같은 frame, 선택 수 > 0, natural/control 차이 모두 0인 경우만 `valid=1`이다. 여기에 후보 RGBA 차이도 0이어야 `sample_equal=1`이다. 선택 0개, 로그 누락, 예외는 통과가 아니다. 자연 main render가 실행되지 않는 프레임도 무효다. 본 카메라 target이 ARGB32·비MSAA·2D가 아니거나 viewport가 전체가 아니면 거절한다. depth 보존이 필요한 Nothing clear 모드도 지원하지 않는다. 본 카메라에 world/camera Canvas UI가 섞이면 거절한다. 미지원 입력을 저해상도나 다른 포맷으로 바꿔 통과시키지 않는다.

RT/active RT/force 플래그는 finally에서 복원하고 임시 RT/CPU texture는 해제한다. gpusplit/gpuhide/gpucensus의 동시 진단도 막았다. 이 비교는 GPU 동기 readback, 반복 렌더와 전체 픽셀 CPU 비교가 매우 비싸므로 **해당 실행 전체를 성능 창과 분리**한다. 일반 렌더 callback의 게임 내부 부작용까지 되돌리는 기능은 아니므로 대조군 실패를 무효로 남긴다. 한 프레임의 RGBA8 결과는 전체 곡·HDR·다른 카메라·물리 지연 증명이 아니다.

## 4. Claude 실행용 sf_run steps

[생성기](../../tools/optimization/gpusplit3-steps.py)는 JSON을 출력/저장할 뿐 sfmeasure를 import하거나 게임을 실행하지 않는다.

```powershell
py -3 tools/optimization/gpusplit3-steps.py --chart '<Arche FULL.adofai 실제 경로>' --output tools/optimization/out/gpusplit3-steps.json
```

출력 JSON의 `steps`, `timeout_min=30`, `tag=gpusplit3-same-262-266`을 sf_run에 전달한다. [steps-example.json](codex-gpusplit-3-20261021-steps-example.json)은 형식 확인용 가상 경로를 넣은 예시이며 바로 실행하면 안 된다. 실제 경로로 생성해야 한다. 이번에는 sf_run을 호출하지 않았다.

255단계 구성:

- 성능: `A → invisible → A → tiny → A → invisible+tiny → A → visible → A`, 총 9창. 매번 동일 seek와 258초 준비 뒤 262~266초를 측정한다. 262초에 gpuprobe 및 gpusplit 통계를 reset한다. 양끝 archegpu stamp, gpuprobe state, gpusplit state를 남긴다.
- 픽셀: gpuprobe를 끈 별도 4회 반복에서 필터마다 262.1/264/265.8초에 3표본씩, 총 12명령을 실행한다. 실제 frame/song 시각은 직전 stamp와 pixeldiff frame 로그로 확인한다. 각 명령 후 0.2초 대기를 두지만 장치가 느려 목표 시각을 넘긴 표본은 같은 시각 표본으로 간주하지 않는다.
- census는 기준 창의 258초 준비 시점에 실행하며 262~266초 성능 구간에 넣지 않는다. visible 비교는 화면을 바꾸는 양성 대조군이며 동등성 절감 후보가 아니다.

판정표(새 실측을 채우기 전까지 모두 미측정):

| 후보 | 앞/뒤 기준 main GPU ms | 후보 GPU ms | 전체 draw / SetPass 차이 | CPU 순회 평균/최악 ms | valid pixel 표본/최대 차이 | 채택 |
|---|---|---|---|---|---|---|
| live:invisible | 미측정 | 미측정 | 미측정 | 미측정 | 0개 / 미측정 | 보류 |
| live:tiny | 미측정 | 미측정 | 미측정 | 미측정 | 0개 / 미측정 | 보류 |
| live:invisible+tiny | 미측정 | 미측정 | 미측정 | 미측정 | 0개 / 미측정 | 보류 |
| live:visible | 미측정 | 미측정 | 미측정 | 미측정 | 0개 / 미측정 | 비교용 |

draw/SetPass는 계속 **전체 프레임 카운터**다. SpriteRenderer 수를 draw 수로 바꾸지 않았다. gpuprobe의 카메라별 GPU 행에서 본 카메라 외 시간이 앞뒤 기준과 후보에서 안정적인지 확인한다. 전체 카운터만으로 외부 카메라 draw가 안정적임을 엄밀히 분리 증명할 수는 없다. 본 카메라만 숨기고 postRender 복원하는 구조와 앞뒤 전체 프레임 기준 안정성을 보조 근거로 사용하고, 외부 draw를 직접 분리하지 못한 한계는 유지한다.

## 5. 화면 동일 절감 후보 설계와 기존 처리

픽셀 동일 표본이 없어서 자동 최적화 구현은 하지 않았다. 진단 자체는 명령 전에는 꺼져 있고 Player에는 들어가지 않는다.

`Dormancy.IsDormant`는 disableShader, hitbox 없음, 정확한 scrVisualDecoration 타입, mesh 꺼짐, `GetVisible()==false` 조건에서 CPU LogicUpdate 목록을 줄인다. frustum 밖인지로 GPU draw를 생략하는 기능은 아니다. `CountOffscreen`의 `isVisible` 집계도 진단 카운트일 뿐 숨김 구현이 아니다.

`InvisibleSkip.Work`는 ApplyColor 뒤 내부 `rendererColor.a<=0`와 허용 shader를 확인해 강제 숨기고, 다시 알파가 양수가 되면 즉시 푼다. shader 거절 캐시와 hidden 목록도 유지한다. 이미 숨긴 약 27,451개는 이번 live 공통 집합에서 제외하므로 중복 이득으로 세지 않는다. 남은 약 1,377개가 모두 이 기능의 누락이라는 결론도 불가하다. 양수 알파 장식은 의도대로 남고, 알파 0 대상은 shader 거절·색 적용 시점·내부 rendererColor와 현재 SpriteRenderer.color의 차이를 확인해야 한다. 이번 census는 현재 color alpha를 남기지만 각 거절 원인별 추적까지 하지 않는다.

통과 필터가 생긴 뒤의 구현 후보는 기존 InvisibleSkip 소유권과 통합하는 기본 꺼짐 옵션이다. 알파 경로는 검증된 shader에서 기존 ApplyColor 사건에 얹고, 카메라 밖 경로는 본 카메라 렌더에만 한정해 복원한다. 다른 카메라에서도 보이지 않는다는 검증 없이 전역 강제 숨김으로 승격하지 않는다. tiny는 실제 픽셀 손실이 가능한 조건이므로 표본 0만으로 전체 구간 안전 규칙으로 채택하지 않는다. 목록 생성·소유권 변경·페이드 인·맵 전환 복원은 함께 검증해야 한다.

비용 계산 방법:

- `C = traversal_mean_ms`, `ΔG = 앞뒤 기준 GPU 평균 − 후보 GPU`.
- 인계의 draw당 1.3~1.5µs를 임시 추정에만 쓰면 순회 C ms를 상쇄할 draw 절감은 대략 `ceil(1000*C/1.5)`~`ceil(1000*C/1.3)`개다. C=0.1ms이면 약 67~77개, C=1ms이면 약 667~770개다. 이 값은 새 실측이 아니다.
- 실제 판단은 측정된 ΔG·draw·SetPass 및 CPU frame 증가를 함께 사용한다. CPU/GPU는 병렬이므로 ΔG−C를 그대로 FPS 이득으로 보고하지 않는다. 모든 캐시 이미지 순회가 86,484개라면 1,377개만 훑는 비용으로 가정할 수 없다. frame time/p95가 개선되고 앞뒤 기준이 안정적이어야 한다.
- 이번 새 C와 ΔG가 없으므로 손익 계산의 실측 결론은 **산출 불가/채택 보류**다.

## 6. 오프라인 검증

| 검사 | 결과 | 근거 |
|---|---|---|
| `dotnet build -c Release` | 오류 0, 경고 38 | [DEV 로그](codex-gpusplit-3-20261021-build-dev.log) |
| `dotnet build -c Release -p:Edition=Player` | 오류 0, 경고 36 | [Player 로그](codex-gpusplit-3-20261021-build-player.log) |
| `dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1` | 오류 0, 경고 38 | [PlayerAuto 로그](codex-gpusplit-3-20261021-build-playerauto.log) |
| LoadCheck DEV / Player / PlayerAuto | 266 / 229 / 266곳, 각각 틀림 0; 동적 이름 5곳은 런타임 확인 영역 | [DEV](codex-gpusplit-3-20261021-loadcheck-dev.log), [Player](codex-gpusplit-3-20261021-loadcheck-player.log), [PlayerAuto](codex-gpusplit-3-20261021-loadcheck-playerauto.log) |
| GpuSplitCheck | 3,965 checks, errors=0 | [fixture 로그](codex-gpusplit-3-20261021-fixture.log) |
| steps 구조 검사 | 255 steps, 9 timing windows, 12 isolated captures | `py -3 tools/optimization/test-gpusplit3-steps.py` |
| Player 타입 제외 | GpuSplit / Policy / PixelDiff 모두 0; DEV/AUTOTEST 각각 존재 | [타입 로그](codex-gpusplit-3-20261021-player-types.log) |
| `git diff --check` | 오류 없음 | 줄바꿈 정규화 안내만 출력 |

fixture는 기존 512가지 소유권 분류와 원상복원/중복 숨김/파괴/강제 숨김/할당 검사를 유지하고, live 각 조합·페이드 인 이탈·화면 밖 진입·임계 경계·NaN/Infinity·RGBA alpha-only 차이·near crossing·동적 소유 복원을 추가한다. Unity의 실제 bounds/카메라/RT 동작은 fixture 검증 범위 밖이다. 빌드 경고에는 기존 assembly 버전 충돌/obsolete/미사용 필드 등이 남아 있다.

수정 소스: `GpuSplit.cs`, `GpuSplitPolicy.cs`, 새 `GpuSplitPixelDiff.cs`, `StutterFix.csproj`, 동시 진단 차단만 추가한 `GpuHide.cs`/`GpuCensus.cs`, `tools/optimization/GpuSplitCheck/Program.cs`, 새 steps 생성기와 구조 검사. `GpuProbe.cs` 및 기존 gpusplit-2 산출물은 이번 작업 시작 전부터 변경된 파일로 유지했다.

## 관찰 보고

task-observer의 기존 사용자 범위 로그는 읽었지만 허용된 쓰기 경로 밖이므로 수정하지 않았다. 관찰은 이 인계 문서로 보고한다: A/B 렌더 검증에서 정상 출력 이미지를 재렌더 seed로 쓰면 제거한 물체가 바탕에 남아 거짓 동일 판정이 가능하다. 이번 진단에는 자연 렌더 입력 캡처와 자연 출력 재현 검사를 넣었다. 후속 실험 스킬 후보 `sfmeasure-experiments`에서 검증 입력과 출력의 구분을 다룰 수 있다. 설치된 스킬이나 메모리는 변경하지 않았다.
