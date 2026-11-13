# Present GPU 시간 계측 구현 결과 — 2026-11-14

요청한 `presentpath on gputime`을 구현하고 오프라인 검증을 완료했다. **게임 실행·설치 DLL 교체·커밋은 하지 않았다. 실게임 GPU 시간, Present 지연 원인, FPS 이득은 아직 미측정이다.** 원래 인계문의 2.83ms 등은 Claude의 선행 측정이며 이번 작업의 측정값이 아니다.

- 작업 위치: `E:\StutterFixWork\wt-v14prod`
- 브랜치/기준 HEAD: `claude/adofai-renderer` / `20a6422ef6ee59efb42dfcbef3e8177eaafbdc71`
- 시작 상태: tracked 수정 없음. 기존 인계문과 다수의 untracked 연구 산출물은 유지했다.
- 기존 Present/버퍼 계측을 확장했다. 제품 기본 설정이나 공용 `bench_v14.py`는 바꾸지 않았다.

## 구현

[PresentPath.cs](../../PresentPath.cs)에 AUTOTEST 전용 `on gputime`(native mode 9)을 추가했다. 기존 Unity 버전·D3D11·UnityPlayer SHA256 검사와 스왑체인 동일성·개별 vtable 소유권 검사, framegen 동시 사용 거부를 유지한다. 새 export는 없다.

[presentpath.h](../../native/sfnative/presentpath.h)는 기존 개별 스왑체인 Present 후크를 그대로 사용한다. gputime은 Present 인자·버퍼 수·latency·화면 크기를 변경하지 않는다. 새 [present_gputime.h](../../native/sfnative/present_gputime.h)가 쿼리 수명과 통계를 관리한다.

| 지점 | 실제 기록 위치 |
|---|---|
| a | `Camera.onPreRender`에서 `targetTexture == null`인 화면 카메라의 재사용 CommandBuffer를 실행하고, 첫 native 렌더 이벤트에서 GPU timestamp 기록. 같은 Present까지 중복 카메라 이벤트는 무시 |
| b | 원래 `Present` 호출 앞에서 GPU timestamp 및 EVENT 쿼리 `End` |
| wall | 원래 `Present` 함수만 전후 QPC로 감쌈. 쿼리 발행·회수·통계 처리는 이 wall 값 바깥 |
| c | Present 반환 QPC 뒤 첫 GPU 명령으로 timestamp 기록 |
| EVENT | c 발행 뒤 즉시 `GetData(...DONOTFLUSH)`로 Present 전 EVENT의 완료 관측 여부 확인 |

16칸 링이며 **한 칸에 두 프레임**을 넣는다. 하나의 Disjoint Begin/End가 두 프레임의 a/b/c를 모두 감싸므로, 같은 유효 GPU 시계 구간 안에서 b₀→b₁을 계산한다. a→b와 b→c는 두 프레임 모두 회수하며, b→b는 `(0,1), (2,3), …`의 **인접 간격 절반**만 표본화한다. 서로 다른 disjoint 구간의 timestamp를 빼지 않는다. 중간에 카메라 시작 이벤트가 없는 Present가 끼면 해당 b→b 표본은 제외한다. disjoint 또는 frequency=0인 쿼리 구간은 두 프레임 모두 제외한다.

프레임당 timestamp 3개와 EVENT 1개, 두 프레임당 disjoint 1개를 사용한다. 초기화 때 총 144개 쿼리 객체를 만든 뒤 재사용한다. Begin/End 발행은 평균 프레임당 5회다. a/b/c와 EVENT를 모두 유지하기 위한 비용이며, 실제 계측 오버헤드는 미측정이다.

회수는 순서대로, 한 번에 최대 16칸을 확인한다. `S_FALSE`이면 즉시 그 회수를 끝낸다. 링이 가득 차면 새 표본을 건너뛰고 `ringDrops`를 올린다. 대기 루프·Sleep·Flush·미완료 쿼리 덮어쓰기와 자체 프레임당 힙 할당은 없다. 카메라 delegate와 CommandBuffer도 한 번 만들어 재사용한다. 드라이버/Unity 내부 할당이나 드라이버 COM 호출 자체의 지연까지 검증한 것은 아니다.

`off`/모드 비활성화/Unload의 기존 `PresentPath.Stop()`에서 관리 카메라 콜백을 해제하고 버퍼를 Release한다. native 원자 mode=0으로 새 계측을 중지한 뒤, 기존 렌더 이벤트에서 열린 disjoint를 닫고 보류 표본을 버리며 모든 쿼리·context·device 참조를 해제한다. 그 뒤 기존 vtable 복원 ACK를 사용한다. 원래 후크의 overlay 소유권 거부/상태 보존 정책은 유지한다. 실제 게임 Unload·복원은 미검증이다.

## 상태 줄과 유효성

`presentpath state`와 `assert`의 render-thread ACK가 통계를 snapshot으로 공개한다. 누적 통계는 새 `on`에서 초기화되며 `off` 뒤에도 마지막 통계를 읽을 수 있다.

| 필드 | 의미 |
|---|---|
| `recovered`, `renderMeanUs/P50Us/P95Us/MaxUs` | 유효한 a→b 회수 프레임 수와 GPU timestamp 구간 µs |
| `bbSamples`, `bbMeanUs/P50Us/P95Us/MaxUs` | 같은 disjoint 안에서 측정한 인접 b→b 표본 |
| `presentSamples`, `presentMeanUs/P95Us` | TEST가 아닌 원래 Present의 QPC wall µs. 회수 여부와 독립적 |
| `eventChecks`, `eventNotObserved`, `eventNotObservedPct` | 반환 뒤 조회에서 EVENT 완료를 관측하지 못한 횟수와 비율. 조회 HRESULT 실패는 분모에서 제외 |
| `disjoint`, `queryFailures` | 제외한 disjoint/zero-frequency **두 프레임 구간 수**, 생성·조회 실패/잘못된 timestamp 순서 횟수 |
| `ringDrops`, `pending`, `missingBegin`, `discardedOnStop` | 포화로 누락한 프레임, 회수 대기 프레임, 시작 이벤트 없는 Present, 종료 시 버린 프레임 |
| `bcMeanUs/P95Us`, `ready` | b→c GPU timestamp 구간과 native 쿼리 준비 여부 |

p50/p95는 누적 고정 histogram의 **8µs 버킷 상한**이다. 65536µs 이상 overflow 버킷에 percentile이 걸리면 관측 최대값을 상한으로 출력한다. 평균/최대는 µs 정수 반올림이다. per-frame raw trace나 정밀한 실수 percentile은 저장하지 않는다.

`assert`는 기존 조건에 더해 `recovered > 0`, `queryFailures == 0`, `ready == 1`을 요구한다. `ringDrops`/`missingBegin`은 로그에서 별도 확인해야 한다. 누락이 많거나 두 시점 사이 `recovered`가 증가하지 않으면 그 실행을 원인 판정에 쓰지 않는다. 첫/마지막 snapshot의 차이를 이용해 준비/워밍업 구간이 섞였음을 확인할 수 있지만, histogram percentile 자체는 lease 전체 누적값이다.

**EVENT 비율은 “Present 반환 순간 GPU 미완료의 정확한 비율”이 아니다.** 조회는 반환 뒤에 이뤄지므로 그 사이 완료될 수 있고, `DONOTFLUSH`의 `S_FALSE`는 완료를 아직 조회로 확인하지 못했다는 뜻이다. 이를 `eventNotObservedPct`라는 관측 이름으로 출력한다. 다음 프레임의 조회만으로 과거 반환 순간의 완료 여부를 소급 판정하지 않는다. CPU QPC와 GPU timestamp의 절대 시각을 보정하지 않으므로 “GPU 완료 QPC 시각”도 만들지 않는다. [Microsoft EVENT/Timestamp 쿼리 정의](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/ne-d3d11-d3d11_query), [Disjoint 유효 조건](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/ns-d3d11-d3d11_query_data_timestamp_disjoint).

## 검증 결과

| 검사 | 이번 결과 / 근거 |
|---|---|
| native 빌드 | 성공, 오류 0. C++ `/W4 /WX` 통과. 기존 ISPC performance 경고 유지. [로그](../../out/present-gputime/build-native.txt) |
| AUTOTEST / Player / DEV | 모두 오류 0. 경고 각 63 / 63 / 84. [AUTOTEST](../../out/present-gputime/build-autotest.txt), [Player](../../out/present-gputime/build-player.txt), [DEV](../../out/present-gputime/build-dev.txt) |
| 실제 native Tracker + 가짜 GPU backend | PASS. 16쌍/32프레임 포화·덮어쓰기/대기 없음·같은 disjoint b→b·중복 시작·누락/disjoint 제외·EVENT 미관측·조회 실패·부분 생성 실패·열린 구간 종료·percentile overflow. [로그](../../out/present-gputime/fixture.txt) |
| 실제 관리 메서드 추출 fixture | PASS. Step/Stop/State/GpuCamera를 컴파일·실행. 켜기/끄기 3회, 화면 외 카메라 제외, 100회 callback에서 버퍼 1개 재사용, ACK, query failure 거부, Unload가 쓰는 Stop 진입점 정리, observe 격리. [로그](../../out/present-gputime/managed-fixture.txt) |
| 기존 native COM / BufferResize / ScreenPrefs | 모두 PASS. D3D11 device 없는 gputime 진입도 후크/COM 참조 누수 없이 거부. [COM](../../out/present-gputime/present-path-fixture.txt), [Pump](../../out/present-gputime/pump-fixture.txt), [Prefs](../../out/present-gputime/prefs-fixture.txt) |
| export / 내장 native / 관리 코드 격리 | HEAD export 60개의 이름·ordinal 전부 보존, 추가 export 없음. 세 관리 DLL 모두 현재 native 내장. PresentPath/BufferResize/ScreenPrefs는 AUTOTEST만 포함. [JSON](../../out/present-gputime/artifact-check.json) |
| diff | `git diff --check` 통과 |

fixture는 실제 GPU/Unity 게임/디스플레이를 사용하지 않는다. 가짜 timestamp 값은 구현 검사용 입력이며 GPU 성능 측정값으로 보고하지 않는다. 실제 D3D 드라이버의 쿼리 지원·진행, 카메라 이벤트의 게임 내 순서/범위, 계측 비용·픽셀·실제 off/Unload는 Claude의 실행으로 확인해야 한다.

최종 [SHA256](../../out/present-gputime/hashes.json):

```text
native/sfnative.dll            5d0bf447afe4657b11a8a25ee1e95ffe2908ae93ad5fcea6d1d2aa5aa55b5f40
bin/PlayerAuto/StutterFix.dll  59219655536faa4e2b2929ba1622104c1bd022c335e3fea4cdc7c6ac7949988c
bin/Player/StutterFix.dll      7afdbf98c54e0bea4e9b04c7f503bc1c0fbe8d86a9ecbf9e981eb5d296dd9175
bin/Release/StutterFix.dll     9cce82073037056f6acb9e43d12e7da860ceb6191760bcbf71d398175024c7b5
```

## Claude 측정용 ppGpu 예시 — 미실행

공용 `E:/StutterFixWork/bench_v14.py`의 기존 CFG2/CFG_MID/CFG_POST 초기화 뒤에 아래를 넣는다. 기존 `ppObs`와 같은 패턴이며 이번 작업에서 그 공용 파일은 수정하지 않았다.

```python
CFG2["ppGpu"] = (dict(CFG2["rlNR"][0]), list(CFG2["rlNR"][1]))
CFG_MID["ppGpu"] = ["presentpath on gputime", "wait 1",
                    "presentpath state", "presentpath assert"]
CFG_POST["ppGpu"] = ["presentpath state", "presentpath assert",
                     "presentpath off", "presentpath state"]
```

```powershell
$env:BENCH_WT = 'wt-v14prod'
$env:BENCH_RT = '3440x1440'
py -3.12 E:/StutterFixWork/bench_v14.py pp-gpu hl50 ppObs,ppGpu,ppGpu,ppObs --probe
# 같은 조건의 Exit(sh)도 비교
py -3.12 E:/StutterFixWork/bench_v14.py pp-gpu-exit sh ppObs,ppGpu,ppGpu,ppObs --probe
```

실행 조건은 인계문에 지정한 HELLO 50초, 3440 렌더, 2560×1440 FullScreenWindow 및 기존 WaitSkip/RunLod/overlay 상태를 유지한다. Exit도 동일 조건을 맞춘다. 현재 bench parser는 PresentPath 실패를 자동 배제하지 않으므로 요약 TSV만으로 성공 판정하지 않는다.

양 끝에서 `accepted gputime`, `queryFailures=0`, 증가하는 `recovered`, 실패 Present 0, 동일 체인/`ownsVtable=1`을 확인한다. `off` 뒤 `status=0 mode=0 ready=0 pending=0`을 확인한다. 후크 비용은 ppObs와 ppGpu의 FPS/CPU/wall 차이로 먼저 확인한다. 준비/상태 조회 구간과 실제 측정 구간을 로그에서 구분한다.

## 해석과 다음 후보

1. a→b가 Present wall과 비슷한 규모이고, b→b도 전체 프레임 간격과 비슷하며, HELLO에서 GPU 계측값이 Exit보다 큰 경우 GPU 작업량/드라이버 큐 대기 가설을 우선 조사한다. 같은 프레임의 GPU 작업 축소와 필터 blit/RenderTexture 패스 정리를 다음 후보로 삼는다. 두 평균의 수치가 비슷하다는 사실만으로 원인을 확정하지 않는다.
2. a→b가 작고 Present wall이 크면 화면 카메라 범위 밖 작업·이전 프레임 backlog를 먼저 확인한다. 이들이 작다는 추가 증거가 있어야 DWM/OS pacing/드라이버 동기화 가설의 우선순위를 올릴 수 있다. DWM이라고 확정하는 계측은 아니다.
3. b→c에는 Present 뒤 timestamp 제출 전의 CPU 지연, GPU 큐 진행/유휴가 섞인다. GPU busy나 Present 함수 자체의 GPU 비용으로 읽지 않는다. b→b도 처리 간격이며 busy time과 다르다.
4. a는 첫 **화면 카메라 이벤트**다. 그보다 먼저 제출된 화면 외 카메라, compute, texture 업로드와 이전 프레임 작업은 a→b에서 빠질 수 있다. a→b를 GPU 프레임 전체라고 단정하지 않는다. `missingBegin`이 크거나 recovered가 없으면 카메라/렌더 이벤트 경로부터 확인한다.
5. Present 앞당기기는 이번 계측에 따른 실험 후보일 뿐이다. 아직 제출되지 않은 화면 작업을 건너뛰지 않는지, 렌더 순서·픽셀·자원 수명·원복을 별도로 검증한 뒤에만 실험한다. 이번 변경은 Present 위치를 바꾸지 않는다.

## 재현 명령 — 빌드/오프라인 검사만

```powershell
& native/build-sfnative.bat 'C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.44.35207' 'C:\SFBundle\winsdk\microsoft.windows.sdk.cpp\c\Include\10.0.26100.0' 'C:\SFBundle\winsdk\microsoft.windows.sdk.cpp.x64\c' 'C:\SFBundle\tools\ispc-v1.31.0-windows\bin'
dotnet build StutterFix.csproj -c Release -p:Edition=Player -p:AutoTestBuild=1
dotnet build StutterFix.csproj -c Release -p:Edition=Player
dotnet build StutterFix.csproj -c Release
& tools/analysis/build-present-gputime-fixture.ps1
py -3.12 tools/analysis/check-present-gputime-managed.py
& tools/analysis/build-present-path-fixture.ps1
py -3.12 tools/analysis/check-present-buffer-pump.py
py -3.12 tools/analysis/check-present-buffer-prefs.py
py -3.12 tools/analysis/check-present-path-artifacts.py
git diff --check
```

작업 절차 관찰: 기존 관찰 목록을 확인했고, 이번 세션에서 새로 기록할 일반화 항목은 없었다. 위 결과는 이 체크아웃의 빌드/오프라인 증거에 한정한다.
