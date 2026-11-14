# 직통 렌더러 M0 결과 — 2026-11-14

구현과 코덱스 담당 오프라인 검증 완료. **게임 안 합격선은 미측정**이다. 작업 위치는 `E:\StutterFixWork\wt-v14prod`, 브랜치는 `claude/adofai-renderer`, 작업 시작 HEAD는 `29ac02e`다. 시작 시 추적 파일 수정은 없었고 M0 소스도 없었다. 기존 `out/` 미추적 실험 자료는 보존했다.

커밋·게임 실행·설치판 DLL 교체·화면 설정 변경은 하지 않았다. 빌드로 작업 폴더의 `native/sfnative.dll`과 `bin/` 산출물만 갱신했다. 제품에 켜지는 설정이나 실제 맵 장식 소유권 변경은 없다.

## 구현

- [direct_tracks.h](../../native/sfnative/direct_tracks.h), [direct_tracks.cpp](../../native/sfnative/direct_tracks.cpp): 9채널 구간 트랙, 전진 커서, 역방향 seek의 이진 탐색, 시작/끝 이벤트와 활성 목록, 영구 작업 스레드. 기본 3개이며 논리 프로세서 수−2 이하로 제한한다(작은 시스템은 최소 1개). 정지한 객체의 행렬·색은 재작성하지 않는다. 각 작업자가 자신의 연속 인스턴스 구간을 평가하고 가시 목록을 만든다. 렌더 쪽에서 작업자 순서대로 합쳐 정렬 순서를 유지한다.
- [direct_renderer.h](../../native/sfnative/direct_renderer.h), [direct_renderer.cpp](../../native/sfnative/direct_renderer.cpp): 인스턴스당 72바이트(2×3 행렬, RGBA, UV 사각형, 층, 플래그, 시차). 기본 버퍼 `StructuredBuffer`에 인스턴스 데이터 `UpdateSubresource` 1회, 카메라 상수 업데이트 1회. CPU 쓰기 가능한 structured buffer의 호환성 문제를 피했다.
- [direct_renderer.hlsl](../../native/sfnative/direct_renderer.hlsl): `SV_VertexID`로 6정점 생성, `SV_InstanceID`로 인스턴스 읽기, 카메라 VP·위치와 시차 적용, `Texture2DArray × color`, 선곱 출력, `One/InvSrcAlpha`. 합성 부하는 텍스처 배열 1개·블렌드 1개라 가시 인스턴스가 있으면 draw 1회, 없으면 0회다. 층 번호가 달라도 draw를 나누지 않는다.
- [direct_shader_bytes.h](../../native/sfnative/direct_shader_bytes.h): FXC가 만든 내장 바이트코드. 게임 렌더 이벤트에서 셰이더 컴파일하지 않는다.
- camRT native texture를 COM 참조로 보관하고 RTV를 캐시한다. 포인터·크기·포맷 변경 시 다시 만든다. typeless RGBA/BGRA 및 부동소수 포맷, sRGB 쓰기, MSAA RTV 차원을 처리한다. 텍스처 배열은 M=1일 때도 명시적으로 `TEXTURE2DARRAY` SRV를 만든다.
- D3D11 deferred context에서 명령을 기록하고 `ExecuteCommandList(..., TRUE)`로 실행해 Unity immediate context 상태를 복원한다. 기존 framegen의 Present/발견 경로는 사용하지 않는다.
- 메인→렌더 사이 8칸 고정 큐에 시각·카메라·대상 텍스처를 프레임별 복사한다. 제출은 짧은 큐 잠금과 시작 신호만 사용하며 렌더 작업 전체를 기다리는 잠금은 잡지 않는다. 첫 작업은 PlayerLoop에서 시작하고, 이미 다음 프레임이 큐에 있으면 이전 렌더 이벤트 끝에서 다음 작업을 시작한다. 렌더 이벤트는 해당 작업 완료를 기다린다. 큐 초과는 `skipped`에 남긴다.
- [DirectRenderer.cs](../../DirectRenderer.cs): DEV/AUTOTEST에서만 컴파일. Update의 `ScriptRunBehaviourUpdate` 직후에 PlayerLoop 표식을 추가하고 메인 camRT 카메라 `AfterForwardAlpha`에 이벤트 1개를 붙인다. `dr off`, `Main.Unload`/제거, 자동 시험 정상·오류 종료, 카메라/맵 데이터/대상 RT 변경 시 해제한다. RT의 동일 객체 내 크기 변경은 native pointer를 다시 읽어 처리한다. PlayerLoop 제거는 현재 트리에서 자기 표식만 지운다.
- 정리 시 카메라 CommandBuffer를 제거하고 네이티브 작업을 완료·종료한 뒤 텍스처를 해제한다. lease마다 다른 정수 이벤트 token을 사용하므로 이전 판에서 남은 콜백은 새 판을 그리지 않는다.

합성 장면은 고정 난수 seed로 만든다. `anim%`에 해당하는 객체마다 9채널 중 하나를 움직이며, 9종 채널이 전체에 분산된다. 즉 3만 개·70%에서는 활성 트랙 21,000개다. 각 트랙은 0.2~2초의 연속 구간으로 60초를 채우고 곡 시각을 60초로 감아 반복한다. 연속 구간 사이에는 활성 상태가 유지되므로 이벤트는 트랙 시작·끝만 있으면 된다. 틈이 있는 트랙의 구간별 시작·끝 이벤트도 fixture에서 확인했다. 일부 객체는 alpha=0 또는 화면 밖이다. 합성 객체는 카메라 위치를 따라가는 플래그를 사용하므로 맵의 절대 좌표에 종속되지 않는다. 시차·월드 모드 계산도 CPU cull과 shader에 함께 구현했다. 카메라 위치를 옮겨도 정지 인스턴스 버퍼 자체는 바뀌지 않음을 확인했다.

## 빌드·분리 검증

| 대상 | 결과 | 근거 |
|---|---|---|
| Native | 오류 0, C++ `/W4 /WX` 통과 | [최종 네이티브·fixture 로그](../../out/direct-renderer/check-log-final.txt) |
| Player | 오류 0, 기존 경고 63개 | [로그](../../out/direct-renderer/build-player.txt) |
| PlayerAuto | 오류 0, 기존 경고 63개 | [로그](../../out/direct-renderer/build-playerauto.txt) |
| Dev (`bin/Debug`) | 오류 0, 기존 경고 84개 | [로그](../../out/direct-renderer/build-dev.txt) |
| Player 분리 | 타입 없음, 관련 user string 0개 | [메타데이터 검사](../../out/direct-renderer/ease-editions.txt) |
| 기존 native ABI | 기존 60개 이름·ordinal 전부 유지, 새 5개 @61~65 | [검사](../../out/direct-renderer/exports-result.txt), [기존](../../out/direct-renderer/exports-before.txt), [변경 후](../../out/direct-renderer/exports-after.txt) |

ISPC의 기존 shadow/gather/scatter 경고는 남아 있다. 빌드 실패로 계산하지 않았다. 새 모듈은 일반 DLL에 포함되지만 `sf_dr_create` 호출 전에는 스레드·D3D 자원·콜백 등록이 생기지 않는다. 일반 Player에서는 호출할 C# 코드와 명령이 제거된다.

최종 Native SHA256: `2D9735EDC1B08DD7A4DD7E26086A36481EED01CF0B6403799BECB0ABFA524BDC`.

## 트랙·셰이더 검증

[fixture 소스](../../tools/direct-renderer/check.cpp), [실행 도구](../../tools/direct-renderer/check.ps1), [이징 비교](../../tools/direct-renderer/ease/Program.cs).

- 3,000개 객체, 14개 시각(전진, 여러 구간 건너뛰기, 60초 끝, 끝 이후, 역방향 seek)에서 작업자 1개/3개 인스턴스 결과 바이트 동일, 활성 트랙 수 동일.
- 별도 불연속 트랙 12개 시각에서 시작·끝·틈·seek 경계 값과 활성 집합 확인. 시작 시각은 활성, 끝 시각은 마지막 값을 쓴 뒤 비활성이다.
- 설치된 **실제 DOTween.dll의 `EaseManager.Evaluate`**와 9종×1,001점=9,009개 비교. Unity나 게임은 실행하지 않고 .NET 8에서 DLL을 읽었다. 게임 Mono의 수학 함수 결과까지 검증한 것은 아니다.

| 이징 | 최대 ULP 차이 | 최대 절대 차이 |
|---|---:|---:|
| Linear / InQuad / OutQuad / InOutQuad / OutBack / OutExpo | 0 | 0 |
| InSine | 2 | 5.960464477539063e-8 |
| OutSine | 1 | 5.960464477539063e-8 |
| InOutSine | 2 | 2.9802322387695312e-8 |

DOTween SHA256: `8E90FC43AB564A5A51424AF56531AB074A5A2E9CAB271B2D653D287954237B83`.

- WARP D3D11, 16×16 RT, 4개 사각형·4개 텍스처 층: 예상 RGBA 1,024채널 대조에서 최대 차이 **0**, 평균 **0**, 다른 채널 **0**. 반투명 흰색의 선곱 출력도 포함한다.
- 비기본 IA topology·viewport·RTV를 설정한 뒤 이벤트 실행 후 동일하게 복원됨을 확인했다.
- 투명/화면 밖 압축, RT 16→8 크기·포인터 재바인딩, 큐 8프레임 처리·초과 제출, 해제 후 오래된 콜백, 재시작 token 분리를 확인했다.
- typeless sRGB texture/RT, 배열 길이 1, 회색 128의 sample/write 왕복(허용 차이 1), 카메라 이동 시 정지 인스턴스 무수정도 통과했다.

이것은 합성 셰이더 검증이다. 실제 Sprites/Tile·장식·맵 순서·전체 곡 framecmp 증거는 아니다.

## 오프라인 CPU 합성 부하

같은 fixture에서 30프레임 워밍업 후 120프레임, 작업자 3개, M=64, 움직임 70%. 시각은 `frame/60`이고 프레임 사이에 실제 시간 대기는 없다. 표는 **게임·Unity·GPU를 포함하지 않는 native evaluator 측정**이다. 시작은 C++ `Tracks.start` 호출만 잰다. C# 카메라 읽기·P/Invoke·큐 제출의 전체 비용은 게임의 `main_us_*`로 따로 확인해야 한다.

| N | 활성 트랙 | 가시 객체(마지막 표본) | 시작 평균/p95 µs | 작업 벽시계 평균/p95 µs | 작업 스레드 합 평균/p95 µs |
|---:|---:|---:|---:|---:|---:|
| 3,000 | 2,100 | 2,812 | 1.587 / 2.300 | 94.893 / 115.000 | 209.621 / 221.300 |
| 30,000 | 21,000 | 28,010 | 2.579 / 3.500 | 1,112.497 / 1,387.800 | 3,226.573 / 4,090.400 |
| 100,000 | 70,000 | 93,297 | 2.933 / 4.100 | 4,483.695 / 4,996.300 | 13,148.108 / 14,376.800 |

작업 스레드 합은 병렬 작업량이며 벽시계와 더하지 않는다. 벽시계가 렌더 이벤트 대기와 같다는 뜻도 아니다. 앞에서 시작한 평가와 Unity 작업이 얼마나 겹치는지는 게임 측정이 필요하다. 특히 10만 개에서는 평가만 약 4.48ms이므로 게임 렌더 대기 목표를 만족한다고 볼 수 없다.

## 매 프레임 관리 힙 할당 0의 코드 근거

`DirectRenderer.Tick`은 카메라/맵 동일성 확인, 캐시한 camRT 필드 delegate, 기존 native pointer, 값 형식 `Matrix4x4`·`Vector4`·80바이트 blittable `CameraData`, P/Invoke, 미리 만든 4,096칸 `long[]`만 사용한다. 정상 경로에 LINQ·새 배열·boxing·문자열 조합·delegate 생성·reflection 조회·CommandBuffer 재작성은 없다. native ring도 고정 8칸이고 workers의 active/visible 및 staging 저장소는 켤 때 확보한다.

로그/정렬용 복사 배열은 명시적 `dr state`/`dr shot`, 설정 배열과 PlayerLoop 트리 재작성은 켜기/끄기 때만 생긴다. 오류·맵 전환에 의한 종료는 정상 프레임 비용에 포함하지 않는다. Mono 프로파일러로 실측한 할당 0 증거는 아직 없으며, 위 결론은 요청된 코드 근거 수준이다. D3D 드라이버/command list의 native 내부 할당까지 0이라는 주장은 하지 않는다.

## Claude 게임 측정 절차

공용 `bench_v14` 파일은 수정하지 않았다. 명세의 예시는 다음과 같다.

```python
CFG2["drB"] = (dict(CFG2["rlNR"][0]), list(CFG2["rlNR"][1]))
CFG_MID["drB"] = ["dr bench 30000 64", "wait 2", "dr state"]
CFG_POST["drB"] = ["dr state", "dr off"]
```

기본 움직임은 100%다. **합격선 측정에는 `dr bench 30000 64 70` 또는 `dr bench 30000 64 anim%=70`으로 바꾼다.** N=3000/30000/100000을 각각 새 판에서 rlNR 기준과 ABBA로 비교한다. `[곡 시작] 화면`·rtsize를 함께 남긴다. 설치는 Claude 담당이며 PlayerAuto/Dev C#과 새 native DLL을 짝지어 사용해야 한다.

명령:

```text
dr bench 30000 64 70
wait 2
dr state
dr shot dr-m0
pause
framecmp grab dr-m0
resume
dr off
```

`dr shot` 자체는 상태와 기존 `framecmp grab` 안내만 적는다. 캡처 중에는 framecmp의 기존 일시정지 요구를 따른다. 화면 확인은 성능 구간과 분리한다.

`dr state`는 프레임 수, 마지막 drawn/draws/active_tracks, skipped/errors, C# 전체 main 시작 비용 평균/p95, native submit·작업 합·작업 벽시계·render_with_wait·render_without_wait·render_wait 평균/p95를 µs로 기록한다. 평균은 켠 이후 전체, p95는 최근 최대 4,096개 표본이다. render_with_wait에는 가시 목록 합치기, 업로드, RTV 재바인딩, D3D 기록/실행, 다음 큐 작업 시작이 포함된다. GPU 실행 시간은 이 CPU 타이머에 포함되지 않는다.

합격 판단: N=30000/70%, 정상 카메라 렌더에서 **skipped=0, errors=0**, 충분한 실제 프레임 표본을 확인한 뒤 main <100µs, render_with_wait <400µs, 가시 객체가 있을 때 draws=1을 확인한다. 큐 초과가 있다면 빠른 제출 시간만으로 통과시켜서는 안 된다. 첫 RTV 생성 표본과 워밍업 영향도 로그에 남긴다. GPU 시간은 별도 기존 GPU 도구로 측정해야 하며 현재 로그에는 `GPU_ms=unmeasured`라고 명시한다.

미확인: 게임 안 보임/카메라 이동/RT resize/맵 전환·Unload, 실제 Mono 할당·이징 ULP, 전체 C# 메인 비용, 렌더 이벤트 예산, GPU 시간, 실제 맵 그림·순서. 카메라 상수는 Update 직후의 snapshot이므로 이후 LateUpdate 카메라 변경과의 프레임 일치는 게임에서 확인해야 한다. 실제 장식 컴파일과 소유권 전환은 M1 범위다. 다중 texture-array/blend 구간 입력 API는 M1에서 일반화할 지점이며 M0는 명세의 합성 배열 1개/블렌드 1개를 검증한다.

## 재현

```powershell
# 오프라인 트랙·WARP·DOTween + Native 빌드 (게임이 떠 있으면 거절)
& tools/direct-renderer/check.ps1 -NativeBuild

dotnet build -p:Edition=Player
dotnet build -p:Edition=Player -p:AutoTestBuild=1
dotnet build

# 실제 DOTween ULP + 세 C# 산출물의 타입/user string 분리 검사
dotnet run --project tools/direct-renderer/ease/EaseCheck.csproj -- out/direct-renderer/native-ease.csv $PWD

# dumpbin /exports로 만든 이전/이후 표의 이름·ordinal 검사
py -3.12 tools/direct-renderer/verify-exports.py out/direct-renderer/exports-before.txt out/direct-renderer/exports-after.txt
```

`exports-before.txt`의 기준은 작업 시작 `29ac02e`의 체크인 native DLL이다. `git show 29ac02e:native/sfnative.dll`의 바이너리를 Python `subprocess.check_output`으로 그대로 저장해 dumpbin으로 읽었다(PowerShell 문자열 리다이렉션으로 바이너리를 저장하지 않는다). FXC/SDK 경로와 MSVC 탐색은 `check.ps1`에 있다.

## M0b — 실게임 지적 반영 (2026-11-14)

이어 작업한 기준 HEAD는 `517ca6b01c334bb2bc6f58b8aaddb10bc393d6bc`이다. 작업 시작 때 있던 `AutoTest.cs` 수정과 작업 중 외부에서 추가된 `JitOpt.cs` 수정은 보존한다. 커밋·게임 실행·설치 DLL 교체는 하지 않는다. 아래 내용은 위 M0 결과를 대체하는 부분만 기술한다.

### 구조 변경

- 활성 구간의 `t0`, 역길이(double), 시작값, 차이값, 다음 경계 시각을 이징별 SoA에 캐시한다. 매 프레임에는 구간 벡터를 따라가지 않고 같은 이징의 연속 배열을 평가한다. 구간 경계에서만 원본 구간과 커서를 읽고 해당 슬롯을 교체한다. seek와 활성 집합 변경은 캐시를 재구축한다. 저장소는 생성 때 확보한다.
- 위치·색은 바뀐 필드만 기록한다. scale은 캐시한 sin/cos를 사용하며 rotation이 바뀔 때만 삼각함수를 다시 계산한다. 정지 객체의 GPU 필드는 그대로 둔다.
- C# 합성 bench의 기본 작업자는 3→1이다. 시작 지점은 기존 `ScriptRunBehaviourUpdate` 직후를 유지한다. 실제 Unity와 겹치는 시간은 게임에서 별도 확인해야 한다.
- 기존 합성 좌표는 XY만 카메라를 따라가면서 월드 Z=0을 사용했다. 따라서 카메라 깊이·자세에 따라 전체가 clip volume에서 제외될 수 있었다. 이를 입증하는 오프라인 재현을 추가했다. 실제 Exit 카메라 행렬은 이번 작업에서 수집하지 않았으므로 실게임 원인을 특정 행렬 값으로 단정하지 않는다.
- 합성 bench만 별도 flag=2의 화면 좌표 평면(X ±12, Y ±7, clip Z=0.5, W=1)을 사용한다. CPU cull과 VS가 같은 범위를 사용하므로 창 크기·RT 크기·카메라 행렬과 무관하게 화면에 표시된다. 기존 월드/시차 모드 flag=0/1은 유지하며 기존 픽셀 fixture에서도 계속 검증한다. 이 합성 경로는 실제 장식의 월드 카메라 일치 검증을 대신하지 않는다.
- 마지막 4개 객체는 중앙의 큰 고정 사각형이다. N=3000/30000/100000의 끝 4개는 원래 70% 활성 집합 밖이어서 활성 트랙 수 2100/21000/70000은 유지한다. 다른 N·anim%에서는 표시용 4개가 애니메이션에서 제외된다.
- `dr state`에 마지막 완료 프레임의 `cull_outside`, `cull_alpha_zero`, `cull_other`(비정상 alpha)를 추가한다. 기존 stat 0~17과 export는 변경하지 않고 stat 18~20만 사용한다. `drawn + 세 cull 합 = N`으로 확인할 수 있다.

### 이번 이어 작업의 추가 최적화

- SoA를 이징 × 채널의 81개 그룹으로 나눴다. 채널 쓰기도 템플릿으로 특수화하여 매 트랙의 채널 분기를 없앴다. 구간 교체 때만 원본 Track/Segment에 접근한다. 각 그룹은 해당 채널의 전체 트랙 수만큼 미리 확보하므로 합성 장면의 구간 교체에 재할당이 필요하지 않다.
- SSE2로 네 트랙의 double 정규화, float 보간 및 Linear/Quad/Back 다항식을 평가한다. FMA나 fast-math는 사용하지 않는다. sin/cos/pow는 기존 스칼라 함수를 유지한다. SSE2는 Windows x64 기본 명령 집합이어서 AVX2 전용 실행 조건을 추가하지 않는다.
- 화면 좌표 AABB도 SSE2로 판정한다. 비정상 alpha는 IEEE float exponent 비트로 검사하며 NaN/무한대는 `cull_other`로 센다. 가시 저장소는 최대 크기로 한 번 생성하고 별도 `visibleCount`만 갱신한다. 순서는 인스턴스 원래 순서를 유지한다.
- GPU 배열 분리와 작은 공간 블록별 평가도 시험했으나 30k 단일 스레드의 확실한 개선이 없어 제외했다. 중간 실험 로그는 `out/direct-renderer/m0b-*.txt`에 보존했다.

### 오프라인 최종 측정과 합격 판정

**정확성 검증과 4종 빌드는 통과했다. 성능 합격선은 미달이다.** 30k/70%/단일 스레드에서 작업 합 평균 **590.047µs**, 벽시계 평균 **615.965µs**다. 요구한 작업 합 300µs 이하 및 트랙당 10ns 이하를 달성했다고 보고하지 않는다. 3스레드 벽시계 평균이 263.317µs여도 단일 스레드 합격을 대신하지 않는다.

실제 i5-9400F에서 게임 프로세스가 없는 상태로 순차 실행했다. M=64, 움직임 70%, 30프레임 워밍업 뒤 120프레임, 시각 간격 1/60초, 실제 프레임 대기는 없다. [최종 로그](../../out/direct-renderer/m0b-final.txt). 아래 작업 합은 평가·컬링·가시 데이터 복사를 모두 포함한다. ns/트랙은 작업 합을 활성 트랙 수로 나눈 값이다.

| N | 작업자 | 활성 트랙 | 가시 수 | 작업 합 평균 / p95 µs | 벽시계 평균 / p95 µs | ns/트랙 | 평가 / 컬링·복사 평균 µs |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 3,000 | 1 | 2,100 | 2,808 | 47.071 / 53.600 | 50.449 / 83.900 | 22.415 | 29.877 / 17.147 |
| 3,000 | 2 | 2,100 | 2,808 | 49.585 / 51.800 | 36.050 / 61.100 | 23.612 | 32.508 / 16.968 |
| 3,000 | 3 | 2,100 | 2,808 | 52.030 / 58.500 | 24.600 / 43.200 | 24.776 | 35.842 / 16.052 |
| 30,000 | 1 | 21,000 | 27,977 | 590.047 / 802.200 | 615.965 / 815.100 | 28.097 | 353.816 / 236.166 |
| 30,000 | 2 | 21,000 | 27,977 | 623.287 / 973.700 | 346.591 / 563.500 | 29.680 | 369.743 / 253.427 |
| 30,000 | 3 | 21,000 | 27,977 | 669.168 / 1,013.100 | 263.317 / 396.100 | 31.865 | 396.066 / 272.921 |
| 100,000 | 1 | 70,000 | 93,192 | 3,169.667 / 4,186.700 | 3,176.212 / 4,219.700 | 45.281 | 1,871.744 / 1,297.765 |
| 100,000 | 2 | 70,000 | 93,192 | 4,819.118 / 6,037.200 | 2,444.054 / 3,076.000 | 68.845 | 2,479.483 / 2,339.424 |
| 100,000 | 3 | 70,000 | 93,192 | 7,284.035 / 9,455.000 | 2,482.449 / 3,265.700 | 104.058 | 3,631.857 / 3,651.820 |

같은 CPU에서 이전 M0 실행 파일을 다시 실행한 [기준 로그](../../out/direct-renderer/m0b-baseline-rerun.txt)는 30k 단일 스레드 작업 합 3,322.717µs였다. 이번 값은 약 82.2% 감소(5.63배)다. 단, 기준과 새 합성 장면은 화면 좌표 표시와 마지막 4개 고정 사각형이 다르며 가시 수도 28,010→27,977로 바뀌었다. 이는 전체 변경 전후 오프라인 비교이며 평가 커널만의 엄격한 동일 부하 A/B나 실게임 FPS 향상 증거가 아니다. 이번 작업 시작 때의 중간 SoA 구현은 [첫 실행](../../out/direct-renderer/m0b-soa-before.txt)에서 30k 단일 작업 합 1,077.267µs였다.

남은 비용은 30k에서 평가 353.816µs, 컬링·복사 236.166µs로 양쪽에 있다. 스레드를 늘리면 벽시계는 줄지만 작업 합은 증가했다. 기본 작업자는 1개로 유지한다. 현재 구조에는 Object/GPU 쓰기 산포, 구간 경계의 원본 벡터 접근, 스칼라 삼각·지수 함수가 남아 있다. 각각의 독립 비용을 계측한 것은 아니므로 어느 하나가 잔여 비용 전부라고 단정하지 않는다. **M0b 성능 목표는 미완료이며 추가 구조 최적화가 필요하다.**

### 정확성·빌드·ABI 결과

- 작업자 1/2/3, 14개 전진·역방향·끝 시각의 GPU 인스턴스 바이트 동일. 1/3 작업자의 순서대로 합친 가시 버퍼도 동일하며 `visible + outside + alpha + other = N`을 확인했다. 불연속 트랙의 12개 경계·seek 사례 통과.
- 실제 최적화된 SSE 평가 경로에서 이징 9종 × 1,001점이 스칼라 값과 동일하다. 그 경로가 출력한 CSV를 설치된 실제 DOTween `EaseManager.Evaluate`와 비교했다. 최대 ULP는 기존과 같은 InSine=2, OutSine=1, InOutSine=2, 나머지=0. 게임 Mono는 실행하지 않았다.
- 기존 WARP 4사각형은 1,024개 RGBA 채널에서 최대 차이 0, 다른 채널 0. 월드 Z=0을 깊이 컬링하는 카메라로 기존 경로가 사라지는 조건을 재현했다. 새 화면 경로는 16×16, 2560×1440, 3440×1440에서 네 사분면의 RGBA 표본이 정확히 일치하고 `drawn=4, draws=1`이었다. 큰 RT 두 개의 전 픽셀 비교를 했다는 뜻은 아니다.
- D3D 상태 복원, 크기·포인터 재바인딩, 투명/화면 밖/NaN alpha 사유 집계, 큐 8개·초과 제출, stale token, 해제, typeless sRGB·배열 길이 1·카메라 이동 fixture 통과.

| 대상 | 결과 | 로그 |
|---|---|---|
| Native | 오류 0, C++ `/W4 /WX /fp:strict`, 기존 ISPC 경고 유지 | [m0b-final.txt](../../out/direct-renderer/m0b-final.txt) |
| Player | 오류 0, 경고 63개 | [m0b-player.txt](../../out/direct-renderer/m0b-player.txt) |
| PlayerAuto | 오류 0, 경고 63개 | [m0b-playerauto.txt](../../out/direct-renderer/m0b-playerauto.txt) |
| Dev | 오류 0, 경고 84개 | [m0b-dev.txt](../../out/direct-renderer/m0b-dev.txt) |
| C# 분리·DOTween | Player 타입 없음·dr 문자열 0, PlayerAuto/Dev 타입 있음, ULP 통과 | [m0b-ease-editions.txt](../../out/direct-renderer/m0b-ease-editions.txt) |
| Native ABI | 기존 65개 export 이름·ordinal 모두 보존, 추가 0개 | [m0b-exports-result.txt](../../out/direct-renderer/m0b-exports-result.txt) |

최종 native SHA256: `7828EB6C8C95A7287A7732CCA2708C42424C6600EEF70671FC0B69336E141203`.

기존 `drB3k/drB30k/drB100k` 설정은 수정하지 않았다. 작업 폴더의 DLL/빌드 산출물만 갱신했다. 커밋·게임 실행·설치 DLL 교체·화면 설정 변경·외부 게임 종료는 하지 않았다. 실게임의 drawn/FPS/render_wait/GPU 비용과 Unity 작업 경쟁 개선은 **미검증**이다. 오프라인 표시 수정이 실게임 출력까지 확인되었다고 해석하면 안 된다.

재현은 위 M0의 명령과 동일하다. 게임이 없을 때 `tools/direct-renderer/check.ps1 -NativeBuild`를 실행하면 9행 CPU 표, SSE 이징 CSV, WARP 검증과 native 빌드를 순서대로 수행한다. 이후 Player/PlayerAuto/Dev를 순차 빌드하고 `dotnet run --project tools/direct-renderer/ease/EaseCheck.csproj -- out/direct-renderer/native-ease.csv $PWD`로 분리를 확인한다.
