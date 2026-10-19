# Codex 작업 AC: 메인 스레드에서 매 프레임 반복되는 엔진 호출 전수 조사 — 히트음처럼 "미리 처리/건너뛰기"로 줄일 수 있는 후보 찾기 (조사·설계만) — 소스 변경·게임 실행·설치·커밋 금지

결과: `docs/engine-calls-20261020.md` (+ 검토 메모 `docs/handoff/codex-enginecalls-20261020-result.md`). **UTF-8 BOM, apply_patch**, 한글 확인. 소스(.cs)는 건드리지 않는다. 게임 DLL 디컴파일·IL 본문은 저장소에 넣지 않는다(`%TEMP%` 사용). 다른 모드 코드 인용·저장 금지.

## 배경
Quartz 의 `RenderAllHitSounds`(타일마다 개별 `AudioManager.Play` 예약 → 미리 합쳐 소수의 음원으로 예약)가 HELLO 전곡 평균 +15%/1% low +19% 의 원천임을 확인했다(`docs/quartz-analysis-20261019.md`, 우리 쪽 독립 구현 `HitSoundBatch.cs`). 사용자 요구: "**이런 것(매 프레임 반복되는 엔진 호출 중 미리 처리하거나 건너뛸 수 있는 것)을 다 찾아보자.**" 장식 애니메이션 쪽은 계산이 아니라 장식별 엔진 쓰기가 비용이라 합칠 수 없다는 것이 확인됐다(`docs/optimization-36.md`). 그래서 다른 곳을 찾는다.

Gemini 의 가설(검증 필요): HELLO 후반 메인 스레드의 `Material.SetFloat` 5.8%·`SetColor` 1.6% 는 `CameraFilterPack_*::OnRenderImage` 가 매 프레임 문자열 이름(`"_Value"`, `"_Value2"` 등)으로 값을 덮어쓰는 비용이며, 문자열→ID 변환을 캐시하고 이전 프레임과 값이 같으면 호출을 건너뛰면 화면 변화 없이 ~5% 이득. 우리 쪽 우려: (a) 건너뛰기는 그 머티리얼을 다른 코드가 중간에 바꾸지 않을 때만 안전(필터가 `new Material(shader)` 로 자기 머티리얼을 갖는지 확인), (b) 값이 박자마다 DOTween 으로 바뀌는 구간에서는 건너뛸 게 별로 없음, (c) 같은 값 쓰기를 건너뛰어도 엔진이 실제로 시간을 아끼는지는 미측정(문자열 해시 비용 vs 네이티브 쓰기 비용 분리 필요), (d) 과거 `FloorFix`("인자가 같으면 결과도 같다" 가정 실패, 타일 색 이상·60~75ms 멈춤)와 같은 실수를 반복하지 말 것.

## 입력 (저장소)
- `docs/handoff/profiles/prof-h50.txt`(HELLO 곡 46~61초), `prof-h170.txt`(HELLO 168~183초), `prof-a262.txt`(Arche 262~277초): 알파 Unity 6000.3.25f1, 3440×1440, 자동 플레이, Quartz 없음, 기본 설정, `profsamp` 1000Hz 15초 표본 프로파일. 형식: 네이티브 모듈별 리프 / 리프 상위 / **관리 함수 포함(스택 어디든)** 목록(포함 시간 %) / 리프 ← 호출자. (메인 스레드 기준. 퍼센트는 15초 표본 중 비율.)
- 게임 코드: 같은 게임 폴더 `A Dance of Fire and Ice_Data\Managed\Assembly-CSharp.dll`(현재 알파). 도구: `tools/ilscan`(`ILScan.exe <dll> <호출이름>`: 그 이름을 부르는 곳 전부, `TYPES=`, `METHOD=`, `FIELDS=1`), ilspycmd(디컴파일/원시 IL, 이전 작업에서 사용한 방식).
- StutterFix 소스(이 저장소)와 `docs/feature-audit-2.8.0.md`(이미 있는 기능 목록), `docs/quartz-analysis-20261019.md`.

## 해야 할 일
1. 세 프로파일의 "관리 함수 포함" 목록에서 **우리 모드/Harmony 래퍼가 아닌, 게임 스크립트와 유니티 엔진 호출**(예: `Material.SetFloatImpl/SetColorImpl/SetVector…`, `CameraFilterPack_*`, `FloorMeshRenderer.set_color`, `scrFloor.Update/LateUpdate`, `scrController.UpdateInput/ProcessKeyInputs`, `Rewired.*`, TMP/UI, `Canvas`, `scrConductor.Update`, `scrCamera.*`, 입자 `scrParticleDecoration.*`, `DOTween` 등) 중 포함 시간이 큰 것을 표로 정리(세 프로파일 비교: 초반/후반/Arche 차이).
2. 각 후보에 대해 IL 로 **호출 구조**를 파악: 누가(어느 게임 클래스·메서드가) 매 프레임 몇 번, 어떤 인자(문자열 이름? 정수 ID? 같은 값 반복?)로 부르는지. 특히 `Material.SetFloat/SetColor/SetVector/SetTexture(string, …)` 를 부르는 곳 전부(`ILScan Material::SetFloat` 등)를 분류: `CameraFilterPack_*`(필터 수), 타일/장식 머티리얼, UI. HELLO 에서 실제로 활성인 필터 수와 필터별 호출 속성 수 추정(맵 이벤트 `SetFilter`/`SetFilterAdvanced` 분포는 `tools/decotrack-research/out/*.json` 또는 맵 파일에서).
3. **줄일 수 있는 방법을 후보별로 설계**(우리 방식, 안전성 우선): (a) 정수 속성 ID 캐시(`Shader.PropertyToID` 호출 제거; 문자열 해시 비용이 얼마인지 근거), (b) 같은 값 건너뛰기(안전 조건: 그 머티리얼이 그 스크립트 전용, 다른 쓰기 경로 없음, 머티리얼 교체·씬 전환·`OnDisable` 시 캐시 무효화, `_TimeX` 처럼 매 프레임 바뀌는 값은 비교만 하고 지나감), (c) 계산을 한 번에/미리(히트음 방식), (d) 호출 자체를 없애는 조건(필터가 화면에 영향 없는 값일 때 — `SkipNoOpScreenFilters` 같은 기존 기능과의 중복·차이는 `FilterIdentity`/`feature-audit` 확인), (e) 기대하는 이득(프로파일 퍼센트를 메인 스레드 ms 로 환산: HELLO 후반 약 6ms 프레임 기준), 위험(화면 변화·다른 모드 충돌), 우리 구현 난이도, 필요한 검증(곡 끝 상태 대조·화면 대조·`decosig` 류·픽셀 비교)과 측정 방법.
4. 같은 방식(히트음 예약처럼)으로 **새로 찾을 수 있는 다른 반복 비용**도 제안: 프로파일에 보이지만 아직 모르는 항목(예: `Rewired.InputManager_Base.DoUpdate`·`ReInput` 1.3%·1.0%, 난독화된 이름의 호출 `YzAwSQu…` 0.9%, `UnityEngine.GUIStyle.GetPreferredSize/GetDimensions`(우리 모니터/설정 창 OnGUI 인지 확인), `IsNativeObjectAlive`, `DOTween.TweenManager.Update`, `scrConductor.Update`, 입력 폴링)은 누구 것이고 우리가 어떻게 할 수 있는지.
5. 산출물 맨 위에 "한 줄 결론"(가장 큰 후보 1~3개와 예상 이득/확신도)과 **우선순위 표**(이득 큰 순·안전한 순)를 둔다. 각 항목에 "우리가 이미 가진 기능과 겹침/차이"를 표시.

## 규칙
- 근거 있는 것만 사실로. 추측은 "추정", 모르면 "미확인". 구현·설치·게임 실행 금지.
- 인계 문서 끝에 Claude 가 게임에서 돌릴 확인 순서(측정으로 가설을 확인하는 최소 실험 — 예: 해당 `SetFloat` 호출 횟수를 구간 단위로 세는 방법(고빈도 Stopwatch 패치 금지), 건너뛰기 켬/끔 A/B)를 적는다.
