# M4i 구현·오프라인 검증 결과

2026-11-15. `E:\StutterFixWork\wt-m4`, 브랜치 `claude/drfull`, 기준 HEAD `70ac32e62dc68e9b3775305ceb5fe369f80daffd`.

**immediate 제출, CPU 인덱스 업로드 제거, 상수 링과 상태 캐시, 같은 세션의 렌더러·Canvas 그래픽 이분 진단을 구현했다. 실게임 DD의 residency 가설 확정, Exit render <600µs/main <300µs·FPS 역전, HELLO 누락 렌더러의 실제 이름, 실제 장식별 반올림 귀속은 미측정이다.** 게임 실행 금지 상태에서 이 네 가지를 증명했다고 보고하지 않는다.

`14-m4i-spec.md`, `12-m4h-spec.md`, `12-m4h-result.md`, `13-hello-color-notes.md`를 기준으로 작업했다. 시작 시 존재한 `StutterFix.csproj`의 Diagnostics/ExplicitPolicy 포함 변경과 기존 산출물을 보존했다. Claude의 unknown LightMode → `-1` 정책도 유지했다. 커밋·게임 실행/조작·설치 DLL 교체/복원은 하지 않았다. 아래 검증 수치는 이번 작업에서 실행한 오프라인 시험이며, 명세의 게임 수치를 재측정한 값이 아니다.

## DD: deferred 목록을 거치지 않는 제출 경로

기존 native segment는 모든 그리기·상수·Grab 복사를 deferred context에 기록한 뒤 `FinishCommandList`와 `ExecuteCommandList(TRUE)` 한 번으로 실행했다. 새 기본 후보는 **Unity render-thread plugin event 안에서 immediate context에 직접 제출**한다. 이 경로에는 `FinishCommandList`/`ExecuteCommandList` 호출이 없다. Unity API/Present hook을 native에 넣지 않았다.

원본 RT/DSV/viewport를 읽고 D3D11.1 `SwapDeviceContextState`로 전용 상태를 선택한 뒤 그린다. draw/compute가 끝나면 이전 전체 상태를 복원한다. 전용 상태는 매 사용 전에 `ClearState`하여 이전 Unity stage/UAV/predication이 새 그리기에 섞이지 않게 한다. 정상·실패 경로 모두 RAII로 복원하고, 복원 시간도 render 통계에 포함한다. 빈 segment는 packet 소비 후 상태 교환 없이 반환한다.

기능을 지원하지 않는 장치에서는 기존 deferred 경로로 돌아간다. `state`의 `submit_requested`, `submit_immediate_available`, `submit_immediate_segments`, `submit_deferred_segments`, `submit_state_restores`를 함께 확인해야 한다. 요청 문자열만으로 immediate 실행을 판정하지 않는다. 모드 변경은 off에서만 가능하며 native도 첫 event 뒤 configuration 변경을 거부한다.

```text
drfull submit immediate
drfull submit deferred
drfull constants ring
drfull constants legacy
```

기존 GPU camera/native/explicit/composite/other timeline과 같은 표본의 DXGI usage/budget/non-local 기록을 유지했다. immediate 경로는 실제 명령 제출 앞뒤를 native 구간으로 표시한다. `submit_state_us_total`, `submit_record_us_total`은 누적 CPU 시간이고 GPU timestamp·busy·page-fault 시간과 같지 않다.

명세의 **원본도 이미 14.1GB/7.53GB인 상황**은 단순히 우리 자원 크기를 줄이는 것만으로 원인을 설명하기 어렵다. 이번 구현은 명령 목록 제출 가설을 분리하는 대조 수단과 우회 후보다. D3D 문서가 “목록의 모든 자원을 동시에 상주시킨다”는 특정 드라이버 동작을 보장하는 것은 아니다. **WARP에서 동일한 픽셀을 얻었다는 사실로 하드웨어 residency 폭주를 증명할 수 없다.**

`m4i-dd-submit-crossover-{loaded,mip2}`는 같은 프로세스·pause된 장면에서 deferred → immediate → immediate → deferred를 측정한다. 모드마다 native/GPU/메모리 state와 그림을 남기고 그림 일치를 검사한다. 일반 노래 진행 ABBA와 구분하는 정지 장면 진단이다. 별도의 DD immediate/deferred B1/B2·pixel 계획도 있다.

`drfull pressure mip 2`는 원래 `QualitySettings.globalTextureMipmapLimit`를 저장한 뒤 진단 중만 변경한다. `drfull pressure restore`와 batch 종료가 원래 값을 복원한다. 이는 mip을 사용하는 이미지에 대한 도구이며, mip 없는 이미지 등은 줄지 않을 수 있다. **mip2라는 이름만으로 VRAM 예산 아래라고 가정하지 않는다.** 실제 DXGI usage/budget을 검사하고, 품질이 다른 그림을 정상 pixel/FPS 판정에 섞지 않는다. 실제 page migration의 확정은 필요하면 VidMm/ETW 증거와 함께 해야 한다.

## Exit: 인덱스와 단일 draw 비용

**CPU가 완성된 인덱스 스트림을 다시 쓰는 경로를 제거했다.** 안정적인 source geometry/world-VB 주소는 유지하고, 순서 변경 시 소스별 `{source, destination, count}` 12바이트 범위만 보낸다. compute가 ordered index buffer를 GPU에서 만든다. 정렬 순서·배치 경계·원본 VS/PS DXBC는 유지한다. 같은 list를 다시 통보하면 인덱스 생성도 하지 않는다.

명세의 “고정 인덱스 + draw 범위”를 문자 그대로 적용한 구현은 아니다. 원본 DXBC와 단일 batched draw를 유지하면서 임의 painter order를 바꾸려면 순서 표현이 필요하다. 소스별 draw로 분해하면 draw 수가 다시 늘기 때문에 **GPU에서 순서 인덱스를 재생성**하는 방식을 택했다. `upload_index_bytes=0`은 호스트 인덱스 전송이 0이라는 뜻이며, 순서 자료 전송이나 GPU 작업 전체가 0이라는 뜻은 아니다.

7,000소스를 매 프레임 반전하는 64프레임 fixture에서 기존 168,000B/frame 인덱스 대신 **84,000B/frame 범위 자료**, 인덱스 호스트 업로드 0, geometry/pose 업로드 0, 정점 bake 재실행 0을 검사했다. 순서 인덱스 compute는 별도 실행되며 `order_gpu_dispatches`로 드러난다. 같은 목록 재통보 16회는 범위 전송·인덱스 compute 모두 0이다. 반투명 3소스의 실제 reverse-order 그림을 single-draw 원본과 대조하는 검사도 유지했다.

단일 draw 경로 변경:

- D3D11.1 상수 오프셋 기능이 있으면 필요한 원본 CB 내용을 256B 정렬로 모아 **segment당 최대 한 번 Map/Unmap**하고 `VS/PSSetConstantBuffers1`으로 범위를 지정한다. 모든 draw가 같으면 재업로드도 생략한다. 원본 셰이더를 새 식으로 번역하거나 uniform을 임의로 vertex color로 바꾸지 않았다.
- deferred 대조와 기능 미지원 장치는 기존 CB 바인딩을 사용한다. deferred의 offset-only 변경 관련 호환성 문제를 대조 경로에 들이지 않았다. ring 모드에서는 사용하지 않는 개별 GPU CB를 만들지 않고 legacy 모드에서 실제 필요한 것만 지연 생성한다.
- 상수의 material/pose/camera/time/renderer color·flip 입력 버전을 추적하여 변하지 않은 입력은 patch/행렬 계산을 반복하지 않는다. material detach와 희소 변경, camera-only 변경, 원본 inverse 행렬 변경도 구분한다.
- geometry/layout, VS/PS texture·sampler, CB storage 바인딩을 재사용한다. Grab이 입력 SRV를 해제할 때와 composite가 슬롯을 바꿀 때 캐시도 무효화한다. frustum plane은 프레임당 한 번 만들고 draw마다 다시 계산하지 않는다.

`rt_constants`에는 링 준비 비용도 포함한다. `order_descriptor_bytes`, `order_gpu_dispatches`, `cb_ring_maps/bytes/bindings`, `constant_evaluations/reuses`를 기존 render/apply/cull/draw/execute 계측과 함께 읽는다.

최종 합성 microbenchmark 수치는 `out/direct-renderer/m4i/m4i-audit.json`의 `microbench`에 있다. 같은 BelowNormal 우선순위, 345개 개별 draw, 128개 이동 프레임, 작은 합성 geometry, software WARP에서 각 모드를 별도 프로세스로 한 번씩 실행했다.

| 모드 | render µs/frame | 128프레임 ring Map |
|---|---:|---:|
| immediate + ring | 3,475.52 | 128 |
| deferred + legacy CB | 7,278.37 | 0 |
| immediate + legacy CB | 7,093.72 | 0 |

세 모드 모두 측정 구간 C++ heap 추가 할당 0, frame당 draw 345를 확인했다. 링의 바인딩은 128프레임 합계 88,576회였다. 이는 반복 ABBA·신뢰구간을 갖춘 성능 시험이 아니며, WARP 드라이버의 CPU 렌더 비용도 포함한다. 7,000소스 fixture의 최종 논리 GPU 보유량은 6,529,896B, 그중 index+범위 upload buffer는 284,416B였다. `constant_evaluations=4`, `constant_reuses=3328`도 확인했다. 모두 합성 자료다.

**실제 Exit 렌더 스레드 1,655µs → 600µs 미만의 실측 결과가 아니다.** main <300µs, replan 517 감소, B FPS ≥ A 역시 이번 작업에서는 검증할 수 없다. 실제 상대 순서를 바꾸는 managed order 정책을 완화해 replan 수를 줄이지 않았다.

## HELLO 및 실제 장식 귀속 진단

`drfull probe list`는 on에서 **현재 실제로 native/explicit로 분류한 renderer**와 Canvas 개별 `Graphic`을 저장한다. TSV에 session-local index, instance ID, 경로, material slot·shader·native ID/explicit 경로 또는 Canvas run·shader를 기록한다. 별도 세션의 ID·그림을 재사용하지 않는다.

off에서 다음 명령을 사용할 수 있다.

```text
drfull probe hide <first> <count>
drfull probe particles
drfull probe restore
drfull probe clear
```

매 hide는 이전 숨김을 원래 값으로 복원하고 새 범위만 적용한다. Renderer는 원래 `forceRenderingOff`, UI Graphic은 원래 `enabled`를 보존한다. 숨김 중 정상 renderer를 다시 켜는 동작은 거부한다. batch 종료는 숨김과 압박 진단을 복구한다. 기존 `hidepath`의 비가역적인 누적 숨김에 의존하지 않는다.

`drfull probe bisect`는 AutoTest에서 여러 프레임에 걸쳐 진행한다. **pause/on에서 잡은 `onref`를 고정**하고 off의 `offref/offnoise` 잡음·상태 동일성을 먼저 검사한다. 이후 같은 프로세스에서 왼쪽/오른쪽 범위를 각각 복원→숨김→GPU readback하여 onref와의 PSNR/changed/max-channel을 비교하고 더 가까운 쪽을 내려간다. leaf의 instance/path/route, 전체 trial TSV, 숨김 그림을 저장하고 복원한다. `improved_over_full_off`도 기록한다.

이는 **한 후보를 찾는 greedy 탐색**이다. 여러 누락 층, 상쇄되는 alpha/가림, 겹치는 UI가 있으면 전체 원인을 한 번에 열거하지 못한다. 개선 없는 leaf를 원인으로 확정하지 않는다. leaf를 개별 hide하고 off 복원 잡음 0, on 기준의 해당 색 층 감소·픽셀 위치를 대조해야 실제 귀속이다. 4맵 모두 이 진단 계획을 만들었으므로 Exit ±1~2, DD 빨강 가는 선 41, WF 대각선 34를 renderer 또는 Canvas Graphic으로 좁힐 수 있다. **해당 실제 장식 이름은 이번 작업에서 관측하지 않았다.** M4h의 합성 UNORM8 양자화 결과로 실제 장식 귀속을 대신하지 않았다.

파티클에는 `drfull particles baked` 비교 경로를 추가했다. 매 프레임 명시적 카메라로 `BakeMesh/BakeTrailsMesh`를 갱신하고 재사용 mesh를 원래 material/pass와 MPB로 `DrawMesh`한다. 기존 body/trail material entry를 구분하고 bake 수·정점 수·빈 mesh 수를 state에 남긴다. `drfull particles explicit`이 기존 경로다. GPU-instanced mesh particles는 검증 없이 허용하지 않는다.

**baked는 기본 off인 진단 후보다.** Unity의 실제 particle simulation space/stream/trail material 배치와 보간·shader keyword까지 픽셀 검증한 결과가 아니다. `m4i-hl50-particles-pixel`은 같은 세션에서 explicit/baked/original을 캡처하고 noise=0 및 세 쌍의 차이, explicit GPU 시간을 기록한다. DrawRenderer 호출 수나 bake 정점 수만으로 실제 fragment가 나왔다고 판정하지 않는다. HELLO 빠진 색이 복구됐다는 실게임 주장은 하지 않는다.

## 빌드·fixture·계획

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/direct-renderer/check-m4i.ps1
```

로컬 build, WARP, managed fixture, JSON 계획 생성·감사만 수행한다. 게임 시작·설치 쓰기·커밋 기능은 없다. 설치 DLL은 읽어서 hash만 비교한다.

| 검사 | 판정 |
|---|---|
| native / DEV / Player / PlayerAuto | 4종 오류 0; 기존 managed/ISPC 경고는 별도 |
| export | name/ordinal/hint 74/74 보존 |
| ABI | 기존 opcode/payload 보존; Configure=22(8B), SubmissionPerf=23(96B) 추가 |
| 패키지 | managed 3종에 현재 native 포함; Player 원본 shader 진단 resource 제외 |
| 배치 / 가는 정점 | 3개 제출·상수 모드 합산 배치 정확 일치 72조건, 가는 정점 1,728변환 정확 일치 |
| Grab | unnamed/named × 3모드, 48조건 픽셀 차이 0; 수명·idle 회수·재생성 검사 |
| 일반 original/reference | 48조건 max 1·다른 픽셀 28; 독립 reference 산술과의 기존 차이이며 배치 정확 일치와 구분 |
| 상태 복원 | IA 8개 VB, 6 shader stage, SRV 384·sampler 48·CB/range 42·CS UAV 8·RT 8 및 depth/blend/raster/viewport/scissor/predication 조회 대조; 비어 있지 않은 CS/GS/UAV·CB 오프셋·predicate 대조 포함 |
| 오류 중 복원 | 잘못된 segment 및 상태 교환 후 MSAA Grab 거부를 주입; pipeline 동일·오류 1 확인. 오류 표본은 정상 성능 자료에서 제외 |
| 수명/순서/packet | 기존 lifecycle 25, masks 10,000, 안정 100,000, oracle edits 7,000, SPSC 128 및 pixel metrics 회귀 통과 |
| timeline / 메모리 | 같은 표본의 범주·단계 합계, serial, 무효 표본, 논리 바이트 중복 제거 유지 |
| 합성 | 기존 composite 24, stencil 9, 공유 depth 6, 양자화 대조 6 유지 |
| 계획 | 정상 24 + 진단 19 생성·감사; 실행하지 않음 |
| 설치 파일 | 검증 구간 snapshot의 managed/native 2/2 SHA256 동일 |

정상 계획은 `m4i-{hl50,wf,dd,sh}-{plan,pixel,A1,B1,B2,A2}`다. 3440×1440, 시작 50/258/50/88초, A1/B1/B2/A2, 12초 측정을 유지한다. 준비 중 pause와 `drfull wait`, safe=0, native 오류/drop=0, 원래 mask 복원, pixel의 off/off 잡음 0 및 changed <0.1%/max ≤3 gate를 포함한다. 진단 모드는 각 정상 계획 시작에서 초기화한다. 8장 제한을 지키도록 진단 후 불필요해진 capture만 `framecmp drop`으로 해제하며, 생성된 계획의 최대 capture 수를 감사한다.

진단 19개는 DD 제출 B1/B2/pixel 6개, DD 같은 세션 crossover 2개, 4맵 이분 진단 4개, HELLO particle pixel 1개, Exit/DD/WF local·Unity Grab 대조 6개다. `--probe-first/--probe-count`로 수동 범위를 바꿀 수 있으며, 자동 이분 탐색은 해당 실행에서 생성한 목록 전체를 사용한다.

최종 근거: `out/direct-renderer/m4i/check-final.log`, `validation/`, `default.log`, `deferred-default.log`, `immediate-legacy-default.log`, `grab*.log`, `composite/rounding.tsv`, `plans/manifest.json`, `m4i-audit.json`, `final-hashes.json`.

API 근거: [SwapDeviceContextState](https://learn.microsoft.com/en-us/windows/win32/api/d3d11_1/nf-d3d11_1-id3d11devicecontext1-swapdevicecontextstate), [상수 범위 바인딩](https://learn.microsoft.com/en-us/windows/win32/api/d3d11_1/nf-d3d11_1-id3d11devicecontext1-pssetconstantbuffers1), [ParticleSystemRenderer.BakeMesh](https://docs.unity3d.com/kr/current/ScriptReference/ParticleSystemRenderer.BakeMesh.html). 이 API 설명은 DD 드라이버 residency 가설이나 실제 맵의 픽셀 검증을 대신하지 않는다.
