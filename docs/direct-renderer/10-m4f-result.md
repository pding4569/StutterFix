# M4f 구현·오프라인 검증 결과

2026-11-15. 작업 폴더 `E:\StutterFixWork\wt-m4`, 브랜치 `claude/drfull`, 기준 HEAD `1f1c5cfcb39c4440b4293745b1beb2e3a5ed7a73`.

**증분 정렬, 고정 포즈 슬롯, GPU 월드 정점 사전 변환 + 원본 DXBC 배치, 준비 경로 개선, 실제 StutterFix FPS 오버레이 동결을 구현했다. native / DEV / Player / PlayerAuto 빌드 오류는 모두 0이고, 기존 export 74/74와 기존 fixture를 보존했다. 새 배치 56조건은 개별 그리기와 픽셀 차이 0이다. 다만 실게임 전체 직통 그리기 <0.95ms, 목표 0.3ms대, 준비 완료 <1초, main_max <2ms, off/off 잡음 0은 아직 실측하지 않았다. 이 성능·실게임 픽셀 게이트의 달성을 선언하지 않는다.**

커밋, 게임 실행·조작, 설치 DLL 교체를 하지 않았다. `native/sfnative.dll`과 세 managed DLL은 worktree의 로컬 빌드 산출물로 갱신했다. 검증 구간의 설치 DLL 해시는 2/2 일치했다. 기존 작업의 untracked 산출물은 보존했다.

## 비용 예산에 맞춘 구조 변경

### 1. 증분 정렬과 Live 비트

`RebuildFullOrder`라는 함수 이름은 유지하지만, 내부의 전체 수집 후 `ordered.Sort(CompareFull)`는 제거했다. Entry가 자신의 재사용 가능한 treap 노드를 소유한다. 순서 키·그룹 경로·패스 구조가 바뀐 Source의 옛 노드만 제거하고 새 키로 삽입한다. 일반적인 변경 비용은 항목당 기대 O(log n)이며, 노드 재삽입은 관리 할당을 만들지 않는다.

변경 노드를 모두 분리한 다음 공유 그룹 키를 갱신하므로, 트리에 연결된 노드의 비교 키가 중간에 변하지 않는다. 부모 변경 때는 옛 부모·재질 인덱스에서 Source를 빼고 새 조상·SortingGroup을 다시 등록한다. Renderer 계층 변경을 쓰기 전부터 전체 rescan으로 바꾸던 경로도 제거했다. Canvas 구조 변경의 기존 보수적 재준비는 유지한다.

native 항목의 단순 enabled/SetActive/force-off 변화는 정렬 대신 패킷의 Live 비트로 보낸다. 꺼진 항목도 원래 위치를 유지하며, 다시 켤 때 정렬 키가 실제로 달라진 경우에만 재삽입한다. Unity explicit 항목과 Canvas의 가시성 변경은 필요한 명령 기록을 갱신한다.

실제 키 변경 후에는 정렬된 트리를 선형으로 펼치고 CommandBuffer/segment를 다시 기록한다. **이 선형 기록 비용까지 없앴다는 뜻은 아니다.** 카메라 정렬 축처럼 많은 키가 실제로 바뀌면 그 항목들은 모두 갱신 대상이다.

`drfull why`는 기존 `타입 + writer` 통계에 `order reason=...`을 더한다. `key`, `pass-key`, `group-distance`, `group-path`, `group-signal`, `mask-range`, `draw-rebind`, `structure`, `hierarchy`, `destroy`, `camera-sort` 등을 구분한다. 그룹 거리 통보도 실제 그룹 키가 같은 경우에는 재삽입하지 않는다.

### 2. 포즈는 고정 용량, 활성 슬롯만 추적

준비 때 TransformAccessArray와 NativeArray를 한 번 확보한다. Live 변화는 슬롯 추가 또는 `RemoveAtSwapBack`과 대응 포즈·중심·거리·역행렬 필요 비트의 swap-remove만 수행한다. native Source ID와 조밀한 포즈 슬롯은 계속 분리돼 있다. Live 변화로 `haveNativeFrame=false`를 세우거나 모든 포즈를 다시 업로드하지 않는다.

작업 결과 통보용 NativeQueue도 고정 byte 배열로 바꿨다. 행렬 비교는 16번의 인덱서 순회를 정확한 `Matrix4x4.Equals` 비교로 바꿨고, 활성 패스가 역행렬을 요구할 때만 `worldToLocalMatrix`를 읽는다.

활성 native 패스에 역행렬 요구가 없으면 `CommitActiveWorld`로 월드 행렬 64바이트만 조밀하게 전송한다. 필요한 패스가 하나라도 있으면 기존 128바이트 Pose 경로를 사용한다. 패스가 새로 역행렬을 요구하면 해당 포즈를 강제로 다시 포착한다. 기존 Frame/Pose 구조 크기와 Commit/CommitPacked의 의미는 보존했다.

용량은 초기 scene Renderer/Canvas 수에 여유를 더해 예약한다. 예약을 초과하면 원본 경로로 복귀한다. dormant 대상의 Source/포즈를 미리 생성하는 방식은 사용하지 않는다.

### 3. GPU에서 정점을 준비하고 원래 DXBC로 배치

처음에는 CPU에서 정점을 변환하는 배치를 구현했지만, 7,000소스가 모두 움직이는 WARP 시험에서 native만 약 1.65ms여서 예산을 만족하지 못했다. 최종 D3D11 FL11 경로는 **compute shader로 월드 정점을 만들고, 원래 vertex/pixel DXBC로 그린다.**

- 정렬 목록/geometry가 바뀔 때만 로컬 정점·UV·색·draw ID 스트림을 갱신한다.
- 포즈·색·Live가 바뀌면 조밀한 GPU 입력을 갱신하고 compute를 실행한다. 정지 프레임은 이미 만든 월드 정점 버퍼를 재사용한다.
- 원본 DXBC의 ObjectToWorld/WorldToObject는 단위행렬, MVP는 현재 VP로 패치한다. 원래 blend/depth/stencil/rasterizer, texture/sampler와 순서는 유지한다.
- 연속하고 호환되는 항목만 합친다. 프로그램/상태, 텍스처/샘플러, 배치로 옮기지 않은 CB·MPB 값이 다르면 경계를 만든다. 해시가 같아도 실제 바이트를 비교하므로 해시 충돌만으로 합치지 않는다. Plugin segment 경계를 넘어 그리지 않는다.
- `SpriteRenderer.color`용 정점 색과 `Sprites/Default`의 `_RendererColor`, `_Flip`은 사전 변환 입력에 넣고, 배치 CB에서는 해당 값을 흰색/단위 뒤집기로 정규화한다. 끈 소스의 삼각형은 모두 같은 점으로 접어 coverage를 없앤다.
- 검증한 대상은 `Sprites/Tile`, `ADOFAI/FloorMesh`, `Sprites/Default`의 일반/PixelSnap 변형이다. Default의 ETC1 외부 알파 변형, Grab, 마스크, 다른 미검증 셰이더는 기존 개별 native/Unity explicit 경로를 유지한다.
- 타일 메시의 읽을 수 있는 CPU 데이터는 원래 외부 Unity VB/IB 경로에 보충한다. 원래 format/submesh/baseVertex 경로를 단일 draw 대조군으로 보존한다. 읽을 수 없거나 요구 semantic이 없는 메시를 임의의 색/UV로 대체하지 않는다.

FL11 compute를 사용할 수 없는 장치에는 CPU 사전 변환 경로가 남아 있다. 최종 새 픽셀·시간 수치는 FL11 WARP의 GPU 사전 변환 경로에 대한 것이다.

상수 버퍼는 소스마다 생성하지 않고 프로그램·stage·slot·크기별로 공유한다. 업로드 비교 상태도 공유 버퍼 자체에 두어, 다른 draw의 갱신 후 오래된 per-draw 캐시가 업로드를 잘못 생략하지 않게 했다. 셰이더/상태, input layout, SRV/sampler는 해당 native 세션에서 재사용한다. compute bytecode와 원본 shader catalog도 캐시한다.

### 4. 준비 시간과 계측

꺼진 Canvas는 하위 트리까지 훑지 않고 목록으로 보관하며, 켤 때 카메라 mask 포함 여부를 확인하고 승격한다. managed write-site 설치는 raw IL의 call/callvirt를 먼저 검사해, 관련 호출이 없는 모든 메서드에 Harmony CodeInstruction을 만드는 비용을 피한다.

새 `drfull warm`은 **원본 경로가 살아 있는 맵 준비 구간**에 observer 설치와 원본 shader catalog 로드를 먼저 수행한다. 이후 `drfull on`은 이를 재사용한다. `drfull off`는 렌더 자원을 복원하며, `drfull cold`는 warm observer까지 해제한다. warm은 명시적 명령이며 자동으로 게임 시작에 설치하지 않는다.

생성한 B/픽셀 계획은 seek 전에 warm을 수행한다. A는 warm을 설치하지 않아 원본 Unity 기준에 observer 호출 비용을 섞지 않는다. 모든 계획은 종료 전에 off/cold로 정리한다.

state의 준비 값은 다음을 구분한다.

| 필드 | 의미 |
|---|---|
| `warm_ms`, `warm_cached` | 따로 지불한 observer/catalog 준비 |
| `setup_ms` | on부터 publish까지 wall time |
| `ready_ms` | 첫 native frame 완료를 관측하기까지의 wall time; 준비 1초 게이트에는 이것도 확인 |
| `setup_hooks_ms`, `setup_discover_ms`, `setup_canvas_ms`, `setup_shader_ms`, `setup_native_ms`, `setup_bind_ms`, `setup_packets_ms`, `setup_publish_ms` | iterator 양보 시간과 분리한 단계별 작업 시간 |
| `prepare_slices`, `prepare_max_ms`, `prepare_work_ms` | 협력적 준비 분할 횟수·최대·총 작업 |

**차가운 on이나 warm 이후 전체 맵 준비가 1초 이내임을 실측한 것은 아니다.** WARP의 native 준비 수치는 Harmony·Unity 수집·컴포넌트·프레임 대기를 포함하지 않는다. 실제 계획에서는 `drfull wait`가 첫 native frame까지 기다린 뒤 검사를 진행한다.

### 5. 실제 FPS UI 동결

기존 `hl50-d0.png`의 좌중앙에는 StutterFix의 FPS/CPU/VRAM 오버레이가 보인다. 종전 동결은 `ADOFAI.FPSCounter.Update`만 막았고, `PerfOverlay`는 unscaled delta time으로 숫자·그래프·점멸·toast와 uGUI geometry를 계속 바꿨다.

FrameCompare pause 동안 `PerfOverlay.Update`, `LateUpdate`, 입력 OnGUI가 현재 표시를 그대로 보존하도록 했다. pause 로그는 실제 singleton의 component ID·이름·mode·표시 FPS를 남긴다. 게임 FPSCounter 동결도 유지한다. Resume/실패 정리는 기존 pause lease를 해제하고 오버레이의 정상 갱신을 다시 허용한다. 화면 좌표 제외나 UI 통째 숨기기는 추가하지 않았다.

**실게임 off/off 0은 아직 미측정이다.** 다른 unscaled 필터/장면 요소의 잡음까지 사라졌다고 확대하지 않는다.

## 최종 오프라인 검증

재현 명령은 다음과 같다. 게임 실행·설치 동작은 없다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/direct-renderer/check-m4f.ps1
```

| 검사 | 결과 |
|---|---|
| native / DEV / Player / PlayerAuto | 4종 오류 0; 기존 경고는 남음 |
| export name / ordinal / hint | 기존 74/74 보존 |
| 원본 ABI / shader resource / Player 경계 | 통과; 기존 Frame 408, Pose 128; 새 payload 크기와 opcode 번호도 검사 |
| 기존 원본 DXBC 대 독립 산술 WARP | 48/48, 최대 채널 1, 다른 픽셀 합계 28, 음성 대조 5; 종전 범위 보존 |
| 새 배치 대 원래 개별 DXBC draw | Tile/Floor 48 + Default 8 = **56/56, 다른 픽셀 0, 최대 채널 0**, 소스 3개 → draw 1회 |
| 배치 변화 | 서로 다른 포즈·색·X/Y flip·PixelSnap, Live off/on, 실제 이동+VP 갱신, 변경 MPB 경계의 픽셀 대조 통과 |
| packed pose | 기존 sparse native ID 5 통과; 새 world-only 64바이트 행렬도 ID 5에서 픽셀 동일 |
| 동시 제출 | 128개 교대 색 SPSC 패킷, 픽셀 동일, errors=0, dropped=0 |
| 안정 상수 / layout | 추가 상수 업로드 0 및 layout 재사용 통과 |
| 증분 정렬 | 7,000항목, 무작위 7,000회 키 변경을 전체 정렬 oracle과 대조; 별도 10,000회 갱신 관리 할당 0, 약 0.243µs/갱신 |
| 층·그룹 정책 / 수명·준비 pump | 기존 20,115 정책 검사, 25종 lease 검사, 임의 mask 10,000, 안정 검사 100,000회 할당 0; 준비 완료·실패·취소 등 통과 |
| Canvas / 공유 stencil | 합성 24, FLOAT 최대 1.19209e-7, RGBA8 최대 2, naive 음성 대조 3, 공유 stencil 9 |
| Grab 원본 DXBC | MULTIPLY/DIFFERENCE 8/8, 다른 픽셀 0, 최대 채널 0 |
| FrameCompare 정책 | metrics 19 + masks 15 + state 15 + controls 17 |
| 계획 | 4맵 × 분류·픽셀·ABBA = 24개 생성·감사; 실행하지 않음 |
| 설치 파일 | 검증 구간 StutterFix.dll / sfnative.dll 해시 2/2 일치 |

새 배치 fixture의 픽셀 차이 0과, 기존 독립 산술 oracle의 ±1 차이는 서로 다른 비교다. 위 수치를 합쳐서 실제 Unity 화면 전체가 비트 동일하다고 해석하지 않는다. HELLO의 과거 실게임 색 불일치 원인 증명과 새 실게임 픽셀 게이트도 여전히 필요하다.

### 7,000소스 제출 시험의 범위

최종 `validation/warp.log`의 같은 원본 Floor DXBC, 매우 작은 geometry, 단일 호환 run, 256회 반복 수치다. 첫 준비 프레임은 평균에서 제외한다.

| 조건 | render 평균 | Commit 평균 | 배치 준비 표본 평균 | draw/프레임 | 관측 C++ heap 호출 |
|---|---:|---:|---:|---:|---:|
| 정지, 월드 정점 재사용 | 54.040µs | 별도 집계 안 함 | 0.025µs | 1 | 0 |
| 매 프레임 7,000개 월드 행렬 변경 | 770.044µs | 144.439µs | 52.475µs | 1 | 0 |

native 자원 준비만은 31ms, 공유 CB 생성은 4개였다. 정지 256프레임 동안 추가 compute dispatch가 없고, 이동 256프레임에서는 프레임마다 dispatch하며, 로컬 geometry 업로드는 전체에서 한 번뿐임도 검사한다.

`render_us`는 native event 전체 wall time이고 WARP의 소프트웨어 GPU 실행까지 포함한다. `rt_batch_us` 등 세부 표본은 64프레임마다 한 번의 작업 구간이며 전체 프레임 평균과 분모가 다르다. **이들을 더해 임계 경로/GPU busy를 계산하지 않는다.** 0 heap은 fixture가 계측한 C++ new 호출과 별도 관리 정렬 갱신에 관한 것으로, 게임 전체 GC/Unity/드라이버 할당 0의 증거가 아니다.

이 시험에는 Unity 포즈 포착, 실제 3440 렌더, 여러 재질 run, 실제 overdraw·필터·Canvas가 없다. 따라서 이동 표본의 약 0.77ms를 실게임 전체 0.95ms 게이트 통과나 목표 0.3ms 달성으로 발표하지 않는다.

## Claude 실행 계획과 남은 게이트

계획은 모두 `E:\StutterFixWork\wt-m4\out\direct-renderer\m4f\plans\` 아래에 있다. `manifest.json`은 실행 목록이며 `launch=false`다.

| 맵 / 곡 시각 | 분류 | 픽셀 | ABBA |
|---|---|---|---|
| HELLO / 50초 | `m4f-hl50-plan.json` | `m4f-hl50-pixel.json` | `m4f-hl50-{A1,B1,B2,A2}.json` |
| Windflower / 258초 | `m4f-wf-plan.json` | `m4f-wf-pixel.json` | `m4f-wf-{A1,B1,B2,A2}.json` |
| DD / 50초 | `m4f-dd-plan.json` | `m4f-dd-pixel.json` | `m4f-dd-{A1,B1,B2,A2}.json` |
| Exit / 88초 | `m4f-sh-plan.json` | `m4f-sh-pixel.json` | `m4f-sh-{A1,B1,B2,A2}.json` |

A/B 모두 측정 위치에서 pause → on/off 및 필요한 준비 대기 → resume → 동일 wait 2 → 12초 측정이다. B/픽셀만 seek 전 warm을 수행하며, 모든 후보는 `drfull wait`와 전후 `assert on`을 확인한다. 색/MPB 진단과 `drfull why`도 남긴다.

픽셀 계획의 off/off 0, 후보 다른 픽셀 <0.1%·최대 채널 ≤3, 실제 복원 확인을 먼저 통과시킨다. 이후 ABBA FPS와 `main_us`, `main_max_us`, `render_us`, `collect_us`, `pose_us`, `sort_us`, `packet_us`, 준비 단계 시간을 확인한다. `pose_world_only`, `inverse_poses`, `pose_adds/removes`, `incremental_keys`, `live_toggles`, `gpu_batch`, `gpu_dispatches`, `gpu_pose_uploads`, `gpu_geometry_uploads`, `batch_runs/sources/vertices`, `single_draws`, `cb_creates`로 실제 배치 비율과 비용 원인을 확인할 수 있다.

자동 OFF/재준비, errors/dropped, uncertainCanvas, 복원 실패가 있으면 표본은 무효다. 실제 임계 경로를 확인해 전체 <0.95ms와 목표 0.3ms대 여부를 판단하고, 곡 중 main_max <2ms 및 관리 프레임 할당 0도 별도로 측정해야 한다. WARP 수치와 기존 M4d/M4e FPS를 새 실게임 성능으로 대체하지 않는다.

최종 근거: `out/direct-renderer/m4f/check-final.log`, `validation/`, `default-warp/`, `grab/`, `m4f-audit.json`, `final-hashes.json`. 소스와 빌드 산출물 해시는 마지막 파일에 있다.
