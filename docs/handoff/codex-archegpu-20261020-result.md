# Arche GPU 구간 연구 결과 — 2026-10-20

게임 실행·명령 전송·설치·커밋·푸시 없이 로컬 조사와 빌드만 수행했다. **새로 입증한 GPU 절약은 0ms, 제품 최적화 채택은 0개**다. 이번 구현은 기존 GPU 계측과 연결하는 기본 꺼짐 진단이다. 실제 258~278초의 활성 패스와 MsGPUBusy 차는 Claude의 후속 측정으로 확정해야 한다.

## 확인한 근거와 중복 제외

- `filters-findings.md`, `filter-identity.md`, `codex-gpu{,2,3}-20261018-result.md`, `codex-gpufree-20261019-result.md`, `codex-decogpu{,2}-20261019-result.md`, `optimization-36.md` 후반과 `roadmap-deco-pipeline-20261020.md`를 참고했다. 반해상도·재질 공유·아틀라스·장식 GPU 이동 시험을 반복하지 않았다.
- 과거 CameraFilterPack 0.000ms는 Legacy 비용 0의 증거가 아니다. 기존 census의 본 카메라 후보는 Legacy 8개와 VideoBloom이다. 이것이 이번 구간에서 매 프레임 호출된다는 의미도 아니다.
- 본 카메라 후보: `CameraFilterPackLegacy_` 접두어의 Distortion_FishEye, Color_GrayScale, Atmosphere_Rain, Color_Chromatic_Aberration, FX_Hexagon_Black, Sharpen_Sharpen, Color_Contrast, Light_Water2 및 VideoBloom. 실제 형식 이름·활성·콜백 수는 새 `archegpu on/state` 로그로 확인한다.
- 과거 `gpufree` 자료의 본 카메라 해상도 시험은 3.74→1.58ms, 모델은 C(s)≈0.86+2.88s²다. **과거 조건에서 픽셀 수에 비례한 부분의 근사**이며 이번 구간의 5~6.6ms 전체에 대입하지 않는다. 필터는 카메라 내부 비용이므로 더하지 않는다.
- `scrCamera.SetupRTCam`은 BGStatic/BGMoving/본 카메라를 공유 RT에 연결한다. 과거 snapshot은 ARGB32, MSAA=1, Quality MSAA=0, BGStatic 색 초기화→BGMoving 깊이 초기화→본 카메라 누적이었다. 현재 값은 on의 카메라 목록으로 다시 확인한다.
- `VideoBloom`은 강도 0이면 이미 단일 Blit로 돌아간다. Legacy 8종은 정상 경로 각각 단일 shader Blit이며, Bloom은 저해상도 내부 다중 패스다. 정확한 반복 횟수는 동적 파라미터가 필요하다. 단순히 9×전체 화면 복사로 계산하지 않는다.

## 기존 계측을 재사용한 이유

`GpuProbe.cs`와 `tools/gpuprobe`는 이미 D3D11 비동기 timestamp로 카메라와 Legacy를 포함한 OnRenderImage를 잰다. 1/8 프레임 샘플링, DONOTFLUSH, 대기 루프 없음이다. 새 네이티브 계측을 만들지 않았다. 이 도구를 쓰는 판에는 담당자가 기존 별도 `gpuprobe.dll`을 준비해야 한다. 이번 작업에서는 네이티브 DLL을 빌드하거나 설치하지 않았다.

`PhaseWatch`의 `[엔진 단계]`는 PlayerLoop의 CPU 경과 시간이다. `FrameScan`의 카메라 전후 시간도 CPU다. `GpuClock`은 NVML 클럭·이용률·P-state이며 패스별 시간계가 아니다. `PerfOverlay`/`RenderWatch`는 FrameTimingManager 및 GPU Frame Time Recorder를 사용하지만 프레임 전체값이다. `FreeFpsProbe`의 draw/SetPass Recorder는 카운터다. 어느 것도 Camera.Render 앞뒤 CPU 타이머를 GPU 패스 시간으로 바꾸지 못한다. GL.Flush/강제 동기화는 추가하지 않았다.

gpuprobe timestamp는 GPU **경과 시간**이며 CPU 제출 간격의 GPU 유휴 등이 포함될 수 있다. 목표인 PresentMon **MsGPUBusy**와 구분한다. timestamp 판은 비용 순위 탐색, 별도 무쿼리 A/B 판의 MsGPUBusy는 프레임 GPU 바쁨 변화 확인에 쓴다. A/B는 효과 제거 후의 종속 효과·복사·스케줄링 변화까지 포함하므로 해당 shader의 순수 시간으로 부르지 않는다.

## 새 구현과 복원

- `ArcheGpuResearch.cs`: `archegpu on|state|stamp|timeline|off`, `fxoff <effect-id|유일한 형식 이름>|none`.
- `on`은 재생 전에만 허용한다. scene 카메라의 ID·clear·target RT·MSAA·HDR·mask와 이미지 효과 ID/형식을 기록한다. disabled 카메라도 수동 Render될 수 있어 포함한다. GameObject/카메라 이름과 맵 원문 문자열은 새 목록에 출력하지 않는다.
- 각 선언층의 OnRenderImage를 열거해 **정확히 1개**, 인스턴스 void(RenderTexture,RenderTexture), 비제네릭·비가변 인자인 경우만 패치한다. 오버로드가 있으면 실패 후 정리한다. 이름만의 AccessTools.DeclaredMethod나 ResolveSignature는 쓰지 않는다.
- `state`는 실제 재생 중 콜백 수, 첫 콜백의 RT 크기·format·sRGB·MSAA, 현재 공개 숫자·enum·Color 필드를 출력한다. 첫 콜백 RT는 이후 해상도 변화까지 추적한 기록이 아니다. state는 한 시점의 파라미터이지 구간 전체 이력은 아니다.
- `fxoff`는 선택한 인스턴스의 원본 OnRenderImage 대신 **Graphics.Blit 1회**를 한다. 일반적인 중간 RT/카메라 연결을 유지하고 Behaviour.enabled는 변경하지 않는다. 효과가 원래 비활성이면 bypassed=0이며 절약 근거가 없다. null 입력/동일 입력·출력은 원본으로 보낸다.
- 이는 **화면을 바꾸는 진단**이다. 필터 내부 TimeX·난수·이력 갱신을 실행하지 않으므로 같은 재생에서 none으로 되돌린 후를 원본 대조군으로 쓰지 않는다. A/B 각각 새 프로세스로 시작한다. 다른 Harmony 패치가 원본 호출을 건너뛰는 경우도 있어 bypassed>0만으로 최종 출력 경로를 완전히 증명할 수는 없다.
- `Settings.ArcheGpuResearch=false` 기본값. 명시적 on에서만 활성화하며 저장 값 true만으로 설치되지 않는다. `off`/`Main.Unload`는 먼저 우회 게이트를 닫고 선택·카운터·목록·설정값을 지운 뒤 전용 Harmony ID를 해제한다. 원래 컴포넌트 필드는 수정하지 않는다. 실게임에서의 해제·재시작 복원은 미검증이다.
- 새 소스·명령·설정 필드·Unload 호출은 `DEV || AUTOTEST` 및 조건부 Compile로 제한했다. Player에는 연구 형식이 없다. off 동안 새 프레임 루프·이벤트·쿼리·네이티브 호출은 없다. on 기준판에는 관측 prefix 비용이 있으므로 순수 무계측 기준판도 별도로 둔다.

## 맵 이벤트: 확정한 것과 시각 미확정

`tools/optimization/archegpu-audit.py`는 기존 읽기 전용 parser를 재사용한다. 원본을 수정하지 않는다. 산출물은 `codex-archegpu-20261020-events.json`이며 원본 SHA256은 `026873b89f05ea94b86b83634a938ac053593ebea6cb832d8134e4a8084f8064`다.

전체 원문: SetFilter **1538**, Bloom **103**, MoveCamera **1537**, ShakeScreen **168**. 시간 제어는 Twirl **966**, SetSpeed **113**, Pause **8**, RepeatEvents **13**이다. JSON에는 관련 이벤트의 원문 필드·floor·source_index를 모두 보존했다. 이를 그대로 258~278초의 개수라고 부르지 않는다.

**이번에 정확한 초 단위 구간 목록은 확정하지 못했다.** 게임의 entryTime을 이번 작업에서 생성하지 않았고, angleOffset·속도·Pause·Twirl·Repeat 및 구간 이전 필터 상태를 해석하지 않은 floor 범위를 곡 초로 간주하면 틀릴 수 있다. 따라서 임의 타이밍 모델로 목록을 만들어내지 않았다.

담당자는 맵을 연 편집 상태에서 `archegpu timeline`을 실행한다. `%LOCALAPPDATA%/StutterFix/archegpu-floors-*.tsv`와 원본 해시 `.sha256`를 생성한다. 아래 결합은 원본 해시가 일치해야 성공한다.

```powershell
$chart = Get-ChildItem -LiteralPath 'D:\얼불춤 맵 파일\Arche' -Filter '*FULL.adofai' | Select-Object -First 1
py -3 tools/optimization/archegpu-audit.py $chart.FullName --timeline '<실제 TSV 경로>' -o docs/handoff/codex-archegpu-20261020-events-timed.json
```

출력 `floor_anchor_window`는 게임 타일 진입시각이 258~278초인 관련 이벤트, `preceding_source_candidates`는 그 이전 이벤트 후보다. **실제 발화시각/활성 필터 목록이 아니다.** angleOffset·duration·RepeatEvents·disableOthers와 이전 상태를 반영한 최종 해석이 남는다. 정확한 활성 패스는 구간 gpuprobe/콜백 수로 교차 확인한다.

## 후보와 조건부 예상 절약

아래 값은 새 실측이 아니다. 합산하지 않으며, 화면 동일성 시험 통과 전에는 제품 구현 대상이 아니다.

|후보|수치 근거·조건부 예상|판정/다음 증거|
|---|---|---|
|Legacy Chromatic Offset=0, Sharpen Value=0|각 효과 원본 시간 F에서 대체 Blit B를 뺀 F−B. 과거 탐색 가설 0~0.15ms/개, 현재는 미산정|기존 FilterIdentity 6종과 다른 Legacy 후보. 실제 값·RT에서 같은 입력 A/A 및 A/Blit RGBA byte 비교가 필요. 구현 안 함|
|Bloom 반복·커널/중간 RT 감소|활성 Bloom 측정시간 F가 상한. 단순히 반복 수 절반=시간 절반이라 하지 않음|원래 중립 경로는 추가 절약 0ms. 활성 경로 반복 순서·반올림 변경의 비트 동일 증거 없음. fxoff로 비용부터 확인|
|불필요한 복사/전체 화면 RT 수 감소|제거 가능한 복사 1회의 실측 비용이 상한. 3440×1440×4B=19.81MB/색 평면, read+write 최소 39.63MB라는 크기 산식만 가능|공유 RT의 누적 색·알파·sRGB·양자화·Overlay quad 의존성 때문에 곧바로 제거 불가|
|MSAA·depth·stencil·clear|과거 MSAA=1이므로 MSAA 추가 절약 0ms. clear/depth 0~0.2ms는 과거 대역폭 가정에 의한 탐색 가설|현재 descriptor 재확인. 24bit 요청은 이미 게임 동작. ZTest·SpriteMask와 누적 색 때문에 0depth/discard 적용 안 함|
|투명/화면 밖 sprite 추가 컬링|추가 승인 절약 0ms, 실제 새 대상 수 미확정|InvisibleSkip·Unity frustum 컬링과 중복 제외. AABB 면적은 실제 채워진 픽셀 수 아님. 과거 장식 전체 숨김 +6%도 이번 GPU ms 절감률로 환산 불가|
|쿼드·간단 shader·배칭|현재 정량 추정 불가|Sprite 윤곽→쿼드는 fill 증가 가능. 반투명 정렬 변경·재질 공유는 과거 픽셀 실패. 장식 파이프라인 중단 결론 유지|

새 화질 최적화는 추가하지 않았으므로 이번 변경에 대한 비트 동일/1~2/255 통과 주장은 없다. `RenderVerify`는 저해상도 scale/half 경로 검사이고, 기존 `FilterIdentityProbe`/`FilterHalfProbe`는 같은 입력 반복 원본을 함께 비교하는 패턴을 제공한다. Legacy 신규 최적화를 채택하려면 실제 shader/바인딩·원본 해상도에서 이를 확장하고, A/A 차이 0인 표본만 사용하며 RGBA 최대 차이·차이 픽셀 수·PSNR을 기록해야 한다. 이 연구의 `fxoff` 자체를 화질 통과 후보로 승인하지 않는다.

## Claude용 실행 절차 — 이번 작업에서는 실행하지 않음

담당자가 허용된 환경에 PlayerAuto DLL을 준비한다. D3D11·해상도·Hz·vSync·프레임 제한·보간·다른 모드·이미지 한도·클럭 조건을 판 사이에 고정한다. gpuprobe가 필요한 판만 기존 독립 DLL도 준비한다. 정상 Player는 sf_run 명령을 실행하지 못한다.

### 1. 타임라인·초기 목록 (성능 근거에서 제외)

```python
from pathlib import Path
chart = str(next(Path('D:/얼불춤 맵 파일/Arche').glob('*FULL.adofai')))
sf_run(steps=['timeout 180', 'auto on', 'open ' + chart,
              'archegpu timeline', 'archegpu on', 'archegpu state',
              'archegpu off', 'quit'], timeout_min=5, tag='archegpu-inventory')
```

timeline과 원문을 위 스크립트로 결합한다. inventory만으로 콜백 활성 여부를 확정하지 않는다.

### 2. 원본 패스별 GPU 시간 (계측 판)

```python
sf_run(steps=['timeout 180', 'auto on', 'open ' + chart,
              'archegpu on', 'gpuprobe on', 'seek 262 8',
              'waitsongpos 256 60', 'archegpu state',
              'waitsongpos 258 30', 'archegpu stamp', 'gpuprobe reset',
              'waitsongpos 278 45', 'archegpu stamp', 'gpuprobe state',
              'archegpu state', 'gpuprobe off', 'archegpu off', 'stop', 'quit'],
       timeout_min=5, tag='archegpu-pass-original')
```

on의 패치 설치·첫 RT 관측·state는 측정 창 전에 끝낸다. 시작·종료 명령은 다른 프레임에 처리되므로 stamp/reset 경계를 로그로 확인한다. 끝의 미회수 표본을 강제로 기다리지 않는다. completed/requested·invalid·overflow·errors·unknown·effect_outside_camera와 residual을 기록하고 카메라 내부 필터 시간을 중복 합산하지 않는다. 이 판의 FPS를 무계측 성능으로 보고하지 않는다.

### 3. 필터 하나씩 새 프로세스 A/B/B/A (PresentMon 판정)

```python
def steps(target):
    return ['timeout 180', 'auto on', 'open ' + chart, 'archegpu on',
            'fxoff ' + target, 'seek 262 8',
            'waitsongpos 256 60', 'archegpu state',
            'waitsongpos 258 30', 'archegpu stamp', 'log ARCHEGPU_BEGIN',
            'waitsongpos 278 45', 'archegpu stamp', 'log ARCHEGPU_END',
            'archegpu state', 'fxoff none', 'archegpu off', 'stop', 'quit']

# inventory에 나오며 인스턴스가 하나뿐인 형식 이름을 지정한다. 복수라면 매 판 ID를 확인한다.
target = 'CameraFilterPackLegacy_Color_Chromatic_Aberration'
for i, selection in enumerate(['none', target, target, 'none']):
    sf_run(steps=steps(selection), timeout_min=5,
           tag=f'archegpu-chromatic-{i}',
           presentmon={'start_after_s': 0, 'seconds': 180})
```

같은 절차를 Sharpen·VideoBloom·실제로 호출된 다른 Legacy에 하나씩 적용한다. 형식 이름은 실제 로그를 우선한다. B는 bypassed>0, A는 selected=0을 확인한다. 각 B는 시작부터 대상을 선택한 새 프로세스다. 상태가 있는 필터이므로 같은 재생에서 B 직후 none으로 되돌린 결과를 A로 쓰지 않는다.

sf_run의 PresentMon 시작은 프로세스 감지 후 실시간 기준이며 곡 258초가 아니다. 180초 CSV에는 실행·로딩·구간 밖도 들어간다. 전체 CSV 평균이나 sf_run 요약 GPU 평균을 258~278초 결과로 쓰지 않는다. stamp의 song/QPC/frequency/UTC와 CSV 시각 열을 대조해 곡 창이 포함되었는지 확인하고 같은 곡 구간만 추출한다. CPUStartQPC 등이 없는 버전은 시각 단위·원점을 확인하고, 대응하지 못하면 해당 판을 제외해 시각 대응을 갖춰 재수집한다. 실행이 늦어 CSV가 구간을 덮지 못한 판도 제외한다. 경계 명령 영향을 피한 258.5~277.5초를 주 분석, 258~278초를 참고로 쓰고 제외 폭은 모든 판에서 같게 한다.

보고할 값은 판별 유효 프레임 수, MsGPUBusy 평균·중앙값·p95, A끼리 차이, A평균−B평균, 클럭·온도·P-state·실패/누락 수다. MsGPUTime 등 다른 열로 대신하지 않는다. 두 A의 드리프트 안이면 개선 미확정이다. 유망하면 담당자가 같은 조건으로 ABBA를 추가한다. RenderVerify/ReadPixels/PNG/census/쿼리 계측을 이 판에 섞지 않는다.

마지막으로 archegpu on도 하지 않는 순수 원본 판을 같은 곡 창에서 한 번 채집해 연구 prefix 자체 영향을 확인한다. 진단 비용이 프레임 병목을 바꾼다면 A/B 결과를 일반화하지 않는다.

## 빌드·검사

최종 소스에서 다음을 실행해 세 구성 모두 오류 0을 확인했다. 로그는 같은 디렉터리의 codex-archegpu-20261020-{dev,player,playerauto}-build.txt다.

```text
dotnet build StutterFix.csproj -c Release
dotnet build StutterFix.csproj -c Release -p:Edition=Player
dotnet build StutterFix.csproj -c Release -p:Edition=Player -p:AutoTestBuild=1
```

LoadCheck: 개발자 266곳/Player 229곳/PlayerAuto 266곳, 모두 불일치 0이다. 동적 이름 5곳은 실행 시 확인이 필요하다. 실제 Harmony 패치 성공이나 Mono 실행을 증명하는 검사는 아니다. *-loadcheck.txt 참조.

ILScan: Player의 ArcheGpuResearch는 0개 형식, 개발자/PlayerAuto에는 존재한다. *-types.txt 참조. Python py_compile과 git diff --check도 성공했다. 게임 내 선택·실제 호출·복원·픽셀·MsGPUBusy는 이번에 미검증이다. 설치 경로 쓰기나 게임 실행 없이 로컬 bin/obj와 이 작업 트리에만 산출물을 만들었다.

이벤트 도구의 합성 입력 검사도 통과했다: 구간 타일 진입시각 결합, 이전 이벤트의 angleOffset 보존, 다른 원본 해시 거부. `codex-archegpu-20261020-audit-check.txt` 참조. 이 검사는 실제 곡의 발화시각 해석을 증명하지 않는다.
