# gpusplit-2 결과 — 2026-10-21

게임 실행·제어·설치·커밋 0회. 작업 위치 `E:\StutterFixWork\wt-archegpu`. Tight 메시 전환은 구현하지 않았다. 기존 2560×1440·120Hz의 5.7257ms / 이미지 숨김 차이 2.9327ms는 인계 실측이며, 이번 작업에서 다시 측정한 값이 아니다.

필터와 창 단위 그리기 카운터를 추가하고 오프라인 검증을 마쳤다. **카운터는 전체 프레임 값이다. 본 카메라 전용 draw/SetPass/정점/삼각형 수 요구는 충족하지 못한다.** Unity의 이 `ProfilerRecorder` 카운터는 카메라별 통계를 제공하지 않는다. 전체 프레임 수를 본 카메라 수로 바꾸어 표시하거나 SpriteRenderer 수를 실제 draw call 수로 간주하지 않았다. 게임 실행을 하지 않아 (a)~(e)의 실제 비용 분해와 화면 동일 절감의 채택은 아직 없다.

## 추가 명령과 분류 기준

```text
gpusplit hide deco-image:onscreen
gpusplit hide deco-image:offscreen
gpusplit hide deco-image:big
gpusplit hide deco-image:small=1
gpusplit hide deco-image:onscreen,big=100,alpha
gpusplit hide deco-image:transparent
gpusplit hide deco-image:additive
gpusplit hide deco-image:alpha
gpusplit census
gpusplit state
gpusplit off
```

- 필터끼리는 `,`로 AND 결합한다. `big=N` / `small=N`의 N은 화면 면적 백분율, 기본 1이다. big은 `>N`, small은 `<=N`이다. `N=100`은 화면 한 장 면적이다. 기존 `deco-text+deco-particle` 같은 그룹 합집합도 유지했다. 필터와 다른 그룹의 혼합은 거절한다.
- onscreen/offscreen은 정확히 이름이 `Camera`인 본 카메라의 frustum과 renderer 월드 AABB 교차 여부다. `isVisible`을 사용하지 않는다. main cullingMask와 기존 UI 제외 규칙을 먼저 적용한다.
- 면적은 월드 AABB의 8꼭짓점을 viewport에 투영한 직사각형이다. 화면 밖 영역을 자르지 않으며 실제 스프라이트 윤곽·겹침·래스터 픽셀 수가 아니다. near plane을 가로지르면 보수적으로 Infinity, 완전히 뒤면 0이다. 회전한 스프라이트에서는 과대평가한다.
- transparent는 `renderer.enabled && SpriteRenderer.color.a <= 0`이다. 텍스처·재질·MPB 알파를 모두 곱한 결과라는 뜻은 아니다. inactive 오브젝트의 enabled도 따로 잡힐 수 있어 `enabled_active`와 `matched_enabled_active`를 같이 기록한다.
- additive/alpha는 `_SrcBlend`/`_DstBlend`와 존재하는 `_BlendOp`를 우선 확인한다. Add에서 One 또는 SrcAlpha → One은 additive, One 또는 SrcAlpha → OneMinusSrcAlpha는 alpha다. BlendModeEffect의 활성 LinearDodge는 수학적 additive로 분류한다(GrabPass 유무와는 별개).
- 기본 `Sprites/Tile`은 이전 추출본의 실제 pass가 RGB/alpha 모두 One/OneMinusSrcAlpha·Add임을 확인했다. 알려진 `PIXELSNAP_ON`/`USE_POINT_SAMPLING` 키워드 조합만 alpha로 받는다. 나머지는 unknown으로 남긴다. `blend_evidence`에 shader·keywords·세 blend 속성의 실제 값/부재를 기록한다. 재질 복제를 일으키는 `renderer.material`은 읽지 않는다.

**분류는 hide/refresh/census 시점의 스냅샷이다.** hide는 현재 선택된 렌더러 집합을 유지하고, refresh는 새로 맞는 대상을 합집합으로 추가한다. 창 중간에 refresh하지 않는다. 이후 카메라 이동·색 변경으로 분류가 바뀌어도 집합은 바뀌지 않는다. 따라서 offscreen/transparent 숨김 결과만으로 4초 내내 화면이 같다고 판정할 수 없다. 매 프레임 전체 렌더러를 재분류하는 진단 부하는 넣지 않았다.

최초 `forceRenderingOff` 값을 기존 ledger에 저장하고 off/그룹 변경/예외/비활성/파괴/Unload에서 복원한다. enabled·activeSelf는 변경하지 않는다. 재차 hide/refresh해도 최초 값을 덮어쓰지 않는다. 기존에 off였던 대상은 off로 복원된다.

census에는 다음을 남긴다.

1. 기존 6그룹 수와 필터별 전체/활성 수(onscreen, offscreen, big, small, additive, alpha, transparent, unknown, unforced_live).
2. 이번 선택 수·활성 선택 수·이미 forceRenderingOff였던 수.
3. 활성·onscreen·unforced 이미지의 투영 AABB 면적 합/최대와 sprite geometry 정점/삼각형 합. 이것은 제출/래스터 통계가 아니다.

## gpuprobe 창 카운터

`gpuprobe on` 때만 Render category의 Draw Calls Count, SetPass Calls Count, Batches Count, Triangles Count, Vertices Count recorder를 시작한다. `reset`에서 누적을 비우고 직전 프레임 혼입을 피하려고 경계 프레임을 제외한다. `state`에 `render_counter=... scope=whole-frame valid=... frames=... mean=...`을 함께 출력한다. 지원되지 않거나 표본이 없으면 unavailable이며 0으로 채우지 않는다. off/시작 실패/Unload에서 Dispose한다. 신규 Harmony 패치 0개, `Module.ResolveSignature` 사용 0개다.

카운터는 마지막 완료 프레임을 매 Update에 수집하며 GPU timestamp는 기존 1/8 표본이다. 같은 reset/state 창에 속하지만 표본 수와 경계가 완전히 같은 통계가 아니다. 본 카메라 외 UI·배경·다른 카메라·필터의 draw도 포함한다. 앞뒤 기준 카운터가 안정적이고 다른 카메라 GPU 시간도 안정적인 경우에 한해 전체 프레임 감소량을 보조 근거로 쓴다. 정확한 본 카메라 draw 통계에는 후속 Frame Debugger/그래픽 캡처에서 해당 카메라 범위의 draw·상태·정점·삼각형을 직접 세는 별도 측정이 필요하다. 그 캡처의 시간은 무캡처 GPU 시간과 섞지 않는다.

## 스프라이트 생성 경로 확인

이번에 설치된 `Assembly-CSharp.dll`을 읽어 IL과 서명을 저장했다. SHA256 `F692210F9A342F7EED4BE40AAD463286AB40BF046599959823942EA02F4CA3FF`.

- `scrDecoration.Setup` → `TextureManager.GetOrAddSprite`가 실제 호출 경로다. [호출자 증거](codex-gpusplit-2-20261021-callers.txt).
- `GetOrAddSprite` IL 0084에서 ppu=100, 0089에서 meshType=0을 명시해 `CustomSprite` 생성자를 부른다. 현재 Unity enum은 FullRect=0 / Tight=1이다.
- `CustomSprite` 생성자는 rect=(0,0,texture.width,texture.height), pivot=Vector2.one/2, 전달받은 ppu, extrude=0, 전달받은 meshType으로 6인수 `Sprite.Create`를 부른다.
- `TextureManager.LoadNewSprite`도 같은 rect·중앙 pivot·extrude=0을 쓰되 meshType은 호출 인수를 따른다. Unity의 5인수 overload 기본이 Tight라고 해서 위의 명시적 FullRect 경로가 Tight가 되지는 않는다.
- 모드의 `ImagePrefetch` 등은 축소 후 ppu를 보정할 수 있다. 여기의 원본 이미지 면적은 실제 상주 텍스처나 최종 화면 크기를 대신하지 않는다.

[현재 DLL IL·enum·빌드별 타입 증거](codex-gpusplit-2-20261021-il.txt), [기존 Tile 추출본의 pass 상태 보존](codex-gpusplit-2-20261021-shader.json). 후자는 10월 19일의 추출본을 이번에 재독한 자료이며 게임 assets를 이번에 다시 추출한 증거는 아니다.

## 읽기 전용 알파 조사

`tools/optimization/sprite-alpha-audit.py`는 지정한 차트를 메모리에서만 파싱하고 `decorationImage`로 실제 파일을 찾는다. Pillow로 alpha>0 경계 상자, nonzero 픽셀 수, 픽셀 사각형 볼록 껍질 근사를 구한다. 원본 맵·이미지는 쓰지 않으며 읽기 전후 SHA256을 확인한다. 결과 파일만 지정 경로에 쓴다.

Arche FULL 차트 SHA256 `026873b89f05ea94b86b83634a938ac053593ebea6cb832d8134e4a8084f8064`. 이미지 참조 **301종**, 초기 AddDecoration **28,828개**, 읽기 실패 **0**, 미측정 인스턴스 **0**. 이미지 변경 이벤트 308개는 별도로 세고 인스턴스 수에 중복 가산하지 않았다. [이미지별 원본 크기·bbox·비율·가중치·SHA256 전체](codex-gpusplit-2-20261021-alpha.json).

| 추정 모델 | bbox로 줄어드는 면적 | 볼록 윤곽 근사로 줄어드는 면적 |
|---|---:|---:|
| 각 이미지의 면적비를 인스턴스 수로 가중 | 26.39285% | 35.59799% |
| 원본 텍셀 면적 × 인스턴스 수로 가중 | 16.91674% | 28.48316% |

첫 행 수식은 `100 × (1 − Σ(instance × candidate_area/full_area) / Σinstance)`이다. 이는 인스턴스별 같은 쿼드 면적을 가정한 추정이다. 두 번째 행은 원본 텍셀 면적을 가중한다. **어느 쪽도 262~266초의 화면 투영 면적 가중치가 아니다.** 가시성·scale·반복 UV·이미지 변경·카메라 이동·압축·다운스케일·클리핑을 반영하지 않았다. 2.9327ms에 이 비율을 곱해 절감 ms로 쓰지 않는다.

| 이미지 | 초기 인스턴스 | bbox/전체 | 볼록 윤곽/전체 | 윤곽 정점 근사 |
|---|---:|---:|---:|---:|
| nev_travel_90.png | 7,100 | 57.760% | 54.800% | 5 |
| nev_travel_180.png | 5,890 | 100.000% | 100.000% | 4 |
| nev_travel_30.png | 5,090 | 53.021% | 39.703% | 25 |
| fog.png | 4,964 | 100.000% | 79.769% | 184 |
| nev_travel_150.png | 3,330 | 63.319% | 46.777% | 17 |

전체 인스턴스의 FullRect 모델은 정점 **115,312** / 삼각형 **57,656**이다. 볼록 윤곽 근사는 정점 **1,206,513(10.463배)** / 삼각형 **1,148,869(19.926배)**다. bbox는 빈 이미지 제외 기본 4정점/2삼각형으로 유지할 수 있지만 윤곽 Tight보다 절감 면적이 작다. 이 수는 **Unity Tight의 실제 출력 정점 수가 아니다.** Unity의 알파 임계·팽창·윤곽 단순화·삼각분할은 재현하지 않았으며 실제 배칭/정렬 영향은 미측정이다. `nev_text_void.png` 초기 6인스턴스는 파일 alpha가 전부 0이지만, 해당 창에 살아 있는지나 다른 이미지로 바뀌는지는 모른다.

재현(게임 실행 없음):

```powershell
$chart = Get-ChildItem -LiteralPath 'D:\얼불춤 맵 파일\Arche' -Filter '*FULL.adofai' | Select-Object -First 1
py -3 tools/optimization/sprite-alpha-audit.py $chart.FullName --output docs/handoff/codex-gpusplit-2-20261021-alpha.json
py -3 tools/optimization/test-sprite-alpha-audit.py
```

## 화면 동일 절감 후보와 필요한 판별

| 후보 | 현재 근거/제약 | 다음 숨김 A/B |
|---|---|---|
| color alpha=0 렌더러 제출 생략 | 새 transparent/활성/이미-off census로 구분 가능. 실제 제출 여부는 아직 모름. additive·특수 셰이더에서 alpha=0만으로 무효 출력을 단정하지 않음 | `deco-image:transparent`, `deco-image:onscreen,transparent`; draw/SetPass/GPU 차이와 동일 시각 RGBA 비교 |
| 화면 밖 제출 생략 | frustum AABB 기준 offscreen 분리. Unity가 이미 컬링했다면 draw/GPU 차이가 0일 수 있음 | `deco-image:offscreen`; 다른 카메라에 보이는 대상도 숨겨지므로 모든 카메라 GPU 로그와 실제 캡처 함께 확인 |
| 큰 투명 쿼드/겹침 면적 감소 | FullRect 확인, bbox/hull 추정. 하드웨어는 화면 밖 픽셀을 이미 클리핑하므로 거대 쿼드의 면적 전체가 실제 fill은 아님 | `deco-image:big=1`, `small=1`, `onscreen,big=100`; 면적 합·정점/삼각형·draw 감소 대비 GPU 감소 비교 |
| 블렌드/GrabPass 비용 | Tile은 premultiplied alpha, LinearDodge 별도. unknown을 alpha로 합치지 않음 | `deco-image:additive`, `deco-image:alpha`; shader별 수와 effect 카메라 시간을 함께 확인 |
| 윤곽 Tight / bbox | 원본 투명 여백 수치 확보. **기존 독립 Tight 시험에서 픽셀 불일치가 있어 화면 동일 후보로 채택 불가** | 이번에는 구현/재시험 안 함. 구현 검토 시 아래 순서로 별도 동일성 관문부터 |

draw/SetPass가 크게 줄고 면적이 작은 그룹에서도 GPU가 줄면 (a)의 근거가 된다. draw가 비슷한 비교에서 면적 감소와 GPU 감소가 맞으면 (c)/(d)의 근거가 된다. 정점/삼각형 감소와 함께 봐야 (b)를 구분할 수 있다. 그룹마다 이미지 수·면적·셰이더가 동시에 달라지므로 이 상관만으로 단일 원인을 확정하지 않는다. offscreen/transparent의 활성·unforced 수가 양수여도 실제 제출 증명은 아니다.

Tight 구현 계획만: 원래 texture/rect/pivot/ppu/extrude를 유지하고 sprite 캐시 키에 meshType을 분리 → 반복 UV·특수 셰이더·마스크·압축 텍스처 조건을 먼저 제외/분리 → 같은 프레임 원본/후보 RT를 RGBA로 비교(회전, Point/Bilinear, 경계, 실제 Tile, DXT/축소 포함) → **픽셀 관문 통과 시에만** 실제 geometry 수·batches·정렬·GPU 시간 측정 → 원본 복원. 위치/UV가 수학적으로 같다는 것만으로 통과시키지 않는다. 기존 [Tight 결과](codex-tight-20261018-result.md)는 990조건 중 832조건 불일치였으며, 이를 다시 성공으로 계산하지 않았다. 그 과거의 다른 해상도 수치를 이번 2560×1440 성능 수치로 전용하지 않는다.

## 후속 sf_run steps — 생성만 검증, 실행하지 않음

담당자가 허용된 PlayerAuto·gpuprobe 환경에서 아래 Python 코드를 sf_run에 넘긴다. **모든 창에서 stop → seek를 다시 한다.** 재생 중 seek만 부르면 기존 AutoTest는 이미 재생 중이라고 반환해 위치가 바뀌지 않는다. 카메라 ID/효과 구성이 바뀔 수 있어 창마다 gpuprobe를 다시 준비한다.

```python
import importlib.util
from pathlib import Path
spec = importlib.util.spec_from_file_location('gpu2steps', 'tools/optimization/gpusplit2-steps.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
chart = next(Path('D:/얼불춤 맵 파일/Arche').glob('*FULL.adofai'))
args = module.build(chart, module.FILTERS)
sf_run(**args)  # 이 줄은 이번 작업에서 실행하지 않았다.
```

생성기 자체는 sfmeasure를 import하거나 게임을 실행하지 않는다. JSON 생성만 하려면 `py -3 tools/optimization/gpusplit2-steps.py --output tools/optimization/out/gpusplit2/steps.json`이다. 10종 B 창과 앞뒤 A 창 총 21창(기준 11창), 외부 제한 30분이다. 짧게 나누려면 `module.build(chart, ['deco-image:transparent'])`처럼 3창 묶음을 사용한다. 설정·해상도·헤르츠는 기존 기준과 같게 유지한다.

각 창의 명령은 다음과 같다. none 창은 hide 대신 census를 쓴다.

```text
gpusplit off
gpuprobe off
stop
seek 262 12
waitsongpos 258 40
gpuprobe on
archegpu stamp
gpusplit hide deco-image:<필터>    # 기준에서는 gpusplit census
waitsongpos 262 20
archegpu stamp
gpuprobe reset
waitsongpos 266 20
archegpu stamp
gpuprobe state
gpusplit state
gpusplit off
gpuprobe off
stop
```

필터 멤버십은 약 258초 스냅샷이며 실제 측정 창은 262~266초다. 첫 stamp와 측정 시작 stamp의 차이로 분류의 나이를 기록한다. on/scan이 늦어 시작이 262초를 넘으면 그 창은 버리고 준비 시점을 앞당긴다. 카메라 ID가 변하면 다른 카메라로 바뀐 원인을 확인한 뒤 비교한다. 멤버십의 시간 변화가 결과를 바꿀 가능성이 있으면 더 짧은 창·동일 시각 정지 캡처로 먼저 검사하며 현재 결과를 실시간 offscreen/transparent 정책의 성능으로 부르지 않는다.

각 B에 대해 바로 앞뒤 A의 평균에서 B를 빼고, A끼리의 차이를 드리프트 지표로 함께 보존한다. 모든 창 시작/종료 stamp가 같은 곡 구간인지 확인한다. GPU completed/active_frames/calls와 errors/overflow/unknown/effect_outside_camera를 확인하고 표본 누락/경계 이탈은 제외한다. counter의 valid=0 또는 frames=0은 결측이다. GPU 시간, draw, SetPass, batches, triangles, vertices의 감소량을 같은 행에 기록하되 counter scope는 whole-frame으로 유지한다. 기존 `gpusplit-summary.py`는 옛 8창 고정 순서이므로 이 21창 분석에 사용하지 않는다.

## 검증과 남은 범위

| 검사 | 결과 |
|---|---|
| `dotnet build -c Release` | 오류 0, 경고 38 |
| `dotnet build -c Release -p:Edition=Player` | 오류 0, 경고 4 |
| `dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1` | 오류 0, 경고 38 |
| LoadCheck Developer / Player / PlayerAuto | 266 / 229 / 266곳, 틀림 전부 0, 동적 이름 각 5곳은 실행 중 확인 대상 |
| 기존 GpuSplitCheck + 필터 경계/잘못된 입력/복원 확장 | 1,062조건, 오류 0 |
| 알파 마스크 합성 검사 | 1검사 통과(빈 이미지·alpha=1 유지·bbox·볼록 면적·정점) |
| 기존 gpusplit-summary 회귀 | 4검사 통과 |
| steps 생성 검사 | 21회 seek, 21개 동일 시작/종료 창, 기준 census 11개; 실행 0 |
| 메타데이터 타입 검사 | GpuSplit/GpuSplitPolicy/GpuSplitGroup/GpuSplitLedger/GpuProbe: Developer·PlayerAuto 포함, Player 제외 |
| `git diff --check` | 통과 |

빌드·LoadCheck 로그는 작업트리의 `tools/optimization/out/gpusplit2/`에 있다(ignored). 경고에는 기존 System.IO.Compression 참조 충돌과 미사용 필드 등이 포함된다. 임시 디렉터리의 합성 이미지 쓰기는 접근 오류가 나서 검사를 작업트리의 ignored 출력 경로로 옮겨 재검증했다. 원본 맵·이미지 쓰기는 없었다.

게임 런타임 분류·알파 필터 전환의 실제 화면·Recorder 유효성/표본 동작·본 카메라 전용 draw 통계·신규 GPU 절감 수치는 미검증이다. 일반 Player에 기능을 켜거나 제품 최적화로 채택하지 않았다. 공유 task-observer 로그는 허용된 작업트리 밖이므로 수정하지 않았으며 스킬 변경은 하지 않았다.
