# M4g 구현·오프라인 검증 결과

2026-11-15. `E:\StutterFixWork\wt-m4`, 브랜치 `claude/drfull`, 기준 HEAD `1ff5493140e15e0b3ac3f71b5859f0b9ea1a2c0a`.

**변경 슬롯 업로드, 순서와 분리된 지오메트리, 실제 순서가 같은 경우 명령 재기록 생략, 원본 DXBC와 같은 변환 연산 순서, named Grab의 프레임별 캐시와 GPU 계측을 구현했다. Exit 직통 켬이 원본 335 FPS보다 빠르다는 실게임 목표는 미측정이다. 게임 실행·조작, 설치 DLL 교체, 커밋은 하지 않았다.**

진행 중 명세에 추가된 DD GPU 35,346µs·빨강 줄 최대 43 차이와 HELLO 상수 단서도 읽고 반영했다. 해당 추가 문구는 외부 작업자의 변경으로 보존했다. 기존 untracked 산출물도 보존했다.

## 업로드와 GPU 사전 변환

M4f 코드에는 다음 비용이 있었다.

- `listChanged`가 지오메트리까지 dirty로 만들어 정렬마다 로컬 정점 스트림을 다시 만들고 전송했다.
- 포즈 하나가 달라져도 `gpuPoses` 전체를 `WRITE_DISCARD`로 전송했다. 배열 크기는 활성 변경 수가 아니라 native ID의 최고 슬롯에 따른다.
- draw의 색·flip 입력도 전체 배열 단위로 dirty였다.
- 같은 프로그램의 상수 버퍼를 모든 draw가 덮어썼다. 각 draw의 값이 이전 프레임과 같아도, 직전 다른 draw의 값과 다르면 다시 업로드했다.

이번에는 native draw ID별 로컬 정점 주소를 고정했다. 실제 새 draw가 생길 때만 정점을 추가하고, 그리기 순서는 별도 R32_UINT 인덱스 버퍼에 둔다. **목록 순서 변경만으로 지오메트리를 dirty로 만들지 않는다.** 버퍼 용량이 커지는 경우에는 새 자원의 전체 초기화가 필요하며, 이를 일반 재정렬과 구분한다.

포즈·draw 입력은 변경 슬롯을 중복 없이 모아 정렬하고, 인접 슬롯을 합친다. 동적 버퍼에 변경 데이터만 기록한 뒤 persistent GPU 버퍼의 해당 범위에 복사한다. 전 슬롯이 바뀌는 dense 프레임은 동적 업로드 버퍼를 SRV로 직접 사용한다. dense에서 sparse로 바뀔 때는 이전 데이터의 GPU 내부 복사로 캐시를 동기화하고, CPU에서는 변경 슬롯만 전송한다. deferred context의 boxed `UpdateSubresource` 소스 오프셋 문제도 피했다.

compute는 매 프레임 모든 정점을 다시 계산하지 않는다. 변경 포즈·색·flip·Live에 걸친 **64정점 블록**만 작업 목록으로 보내고, 이미 만든 나머지 월드 정점은 재사용한다. 같은 블록 안의 변경되지 않은 이웃 정점은 함께 재계산할 수 있다. 지오메트리 확장·출력 버퍼 재생성 때는 초기 전체 계산을 수행한다.

상수 버퍼는 동일한 재질 내용과 프레임 값은 공유하고, 개별 오브젝트 행렬은 포즈별로 분리한다. 배치용 단위행렬 상수는 개별 draw용과 별도 저장한다. 실제 재질/MPB 값이 달라진 binding은 처음에 전용 버퍼로 분리하고 이후 재사용한다. Sprite 색 VB도 draw별로 보존한다. **바뀐 binding만 업로드하며, 그 binding 내부는 D3D11 상수 버퍼 전체를 갱신한다. DWORD 단위 부분 CB 갱신이라고 주장하지 않는다.** 최초 재질 분리나 새 자원 생성에는 일회성 할당이 남아 있다.

`state`의 누적 바이트는 다음 8개로 나눴다. fixture에서 합계가 기존 `rt_upload_bytes`와 같은지도 검사한다. 초기 immutable 자원 생성과 GPU 내부 복사는 이 CPU 업로드 합계에 포함하지 않는다.

| state 필드 | 대상 |
|---|---|
| `upload_geometry_bytes` | 새 로컬 정점 |
| `upload_pose_bytes`, `upload_pose_ranges` | 변경 포즈·Live와 연속 범위 수 |
| `upload_draw_bytes`, `upload_draw_ranges` | 변경 색·flip 등 draw 입력 |
| `upload_index_bytes`, `upload_index_calls` | 변경 그리기 목록 |
| `upload_constant_bytes`, `upload_constant_calls` | 변경 상수 binding |
| `upload_color_bytes` | 개별 sprite 색 VB |
| `upload_cpu_batch_bytes` | FL11 미지원 장치의 CPU 배치 VB |
| `upload_work_bytes`, `baked_vertices` | compute 작업 목록·제어 값, 실행 정점 lane 수 |

### 전송량 fixture

| 조건 | 검증 결과 |
|---|---|
| 7,000소스 중 131포즈 변경, 256프레임 | **포즈 10,480B/프레임**, 연속 범위 2개, 정점·draw·상수 업로드 0 |
| 같은 sparse 시험의 compute | **896정점 lane/프레임**, 작업 목록 56B와 최초 제어 값 16B; 평균 56.0625B/프레임 |
| 7,000소스 전체 반전 정렬, 64프레임 | **지오메트리 0B, 포즈 0B, compute dispatch 0**, 인덱스 168,000B/프레임 |
| native ID 2의 포즈 하나 변경 | 포즈 **80B**, 개별 원본 DXBC와 픽셀 동일 |
| sprite tint 하나 변경 | draw 입력 **32B**, 지오메트리 0B, 개별 원본 DXBC와 픽셀 동일 |
| 서로 다른 개별 draw를 안정 상태로 반복 | 추가 CB·sprite 색 업로드 **0B** |

합성 7,000포즈 전체는 560,000B다. 같은 fixture에서 131개 변경은 그 대비 약 98.13% 적은 포즈 전송이다. **실게임의 프레임당 12.2MB가 정확히 얼마로 줄었는지는 아직 측정하지 않았다.** 기존 state에는 항목별 바이트가 없어 그 12.2MB의 실제 구성비를 역산해 확정할 수 없다.

## 불필요한 재정렬과 스파이크

변경 키를 기존 증분 트리에 반영한 뒤, 실제 Entry 순서가 이전 명령의 순서와 같은지 비교한다. 같으면 CommandBuffer 초기화·Canvas run 재기록·draw 목록 전송을 건너뛴다. opaque/transparent phase, Unity explicit 항목의 Live, Canvas 격리 방식이 달라지는 경우에는 같은 ID 순서여도 재기록한다. 키가 바뀐 노드의 트리 갱신 자체는 남아 있다.

`key-distance`와 `key-bucket`으로 거리만 달라진 통보와 층·순서·큐·셰이더·재질 등의 변경을 구분한다. `avoided_order_replans`, `key_only_frames`는 순서를 재평가했지만 명령을 유지한 횟수다. 이로써 단순 거리 변화만으로 기존의 비싼 재기록까지 이어지는 경로를 없앴다.

정렬 fixture는 기존 7,000회 무작위 변경의 전체 정렬 oracle 대조를 유지하며, 공통 키 이동은 생략, 실제 교차와 제거는 재계산, 안정 검사 10,000회의 관리 할당 0을 추가로 확인한다.

개별 draw의 원인은 `single_unverified`, `single_geometry`, `single_program`, `single_constants`, `single_textures`, `single_segment`, `single_limit`로 분류한다. 합계가 `single_draws`와 같은지 검사한다. 이는 실제 제출된 개별 draw의 분류이며, 컬링된 항목은 포함하지 않는다. 미검증/geometry 조건을 먼저 판정하고, 인접 후보의 프로그램·상수·텍스처 경계를 판정한다.

64KB였던 managed 상수 패킷은 준비 때 소스 규모에 맞춰 예약한다. `main_max` 프레임의 collect/pose/sort/Canvas/packet 시간과 GC collection 차이를 저장한다. 최대 commit에는 포즈 수·dirty draw 수·바이트·GC·배열 확장 횟수를 남기고, 전체 확장 횟수와 할당 크기도 센다.

**기존 76ms main / 4.6ms commit 스파이크의 실게임 원인은 확정하지 않았다.** 배열 확장 경로는 코드에서 확인해 줄였지만, 그 과거 프레임이 실제 확장·GC·job 대기·Canvas 렌더링 중 무엇 때문이었는지는 옛 합계 값만으로 증명할 수 없다. 다음 실게임 state에서 `max_*`, `commit_peak_*`, `commit_growth*`로 구분할 수 있다.

## 변환 반올림

원본 `Sprites/Tile`, `ADOFAI/FloorMesh`, `Sprites/Default` DXBC를 확인했다. ObjectToWorld는 Y항 `mul`, X항 `mad`, Z항 `mad`, translation `add` 순서다. 기존 compute의 HLSL `mul(world, float4(...))`는 실제 disassembly에서 행별 **`dp4`**로 컴파일됐다. `IEEE_STRICTNESS`만으로 원본의 연산 순서를 보존하지 못한 것이다.

compute를 원본과 같은 Y→X→Z→translation 순서의 `precise`/명시적 `mad`로 바꿨다. 수정 전·후 어셈블리와 원본 VS 목록은 `out/direct-renderer/m4g/pretransform-{before,after}.asm`, `precision-audit.json`에 있다. 변환 이후에도 원래 VS/PS DXBC를 그대로 사용한다. sRGB 변환을 임의로 추가하거나 채널 값을 보정하지 않았다.

기존 배치 56조건 외에, 가는 geometry와 비대칭·shear 행렬, 카메라 변화, PixelSnap을 포함한 **56×24=1,344개 변환**을 같은 원본 DXBC 개별 draw와 비교한다. 실제 coverage가 생기는지도 확인하며, 다른 채널 수는 0이다. 재정렬·희소 포즈·tint 변화도 픽셀 동일이다.

**DD 캡처 y 793~806의 빨강 최대 43과 Exit 11.3% 픽셀의 ±1~2가 이 수정으로 0이 됐다는 실게임 증명은 없다.** 연산 순서 차이는 코드와 어셈블리로 확인했으나, 실제 Unity의 동적 배칭 정점과 하드웨어 GPU·색수차 필터를 포함한 최종 화면은 별도 대조가 필요하다. 기존 독립 산술 oracle의 48조건 중 28픽셀/최대 1 차이는 유지됐으며, 위의 원본 DXBC끼리의 정확한 비교와 별개다.

## GrabPass와 GPU 비용

실제 게임 asset에서 다음을 확인했다. `_GrabTexture`라는 sampler 이름만으로 named Grab이라고 판단하면 안 된다.

| Shader 계열 | 원본 GrabPass `m_TextureName` | 구현 |
|---|---|---|
| `.../Grab`, `.../GrabMasked`, `VisualDeco/Grab` | 빈 문자열 | draw마다 복사 유지 |
| `.../UnifiedGrab`, `.../UnifiedGrabMasked` | `_BLENDMODES_UnifiedGrabTexture` | 프레임당 해당 이름의 첫 사용에서만 복사 |

shader resource를 schema 2로 확장해 실제 Grab 이름을 보존했다. 기존 DXBC는 변경하지 않았다. 로더는 schema 1도 읽는다. native `grab=1`과 sampler `srgb=-1`의 기존 unnamed 동작도 보존하고, 이름별 ID를 추가했다.

각 named Grab은 별도 texture/SRV를 가진다. 같은 native 프레임의 plugin segment 사이에서도 캐시를 유지하며, 다음 프레임에는 다시 캡처한다. 다른 이름은 서로 다른 시점에 독립 캡처한다. 같은 이름을 native와 Unity explicit 양쪽에서 소유하면 서로 다른 스냅샷이 생길 수 있어 해당 구성은 원본 경로로 복귀한다.

MULTIPLY/DIFFERENCE의 원본 DXBC로 unnamed 8조건과 실제 UnifiedGrab 8조건을 검사했다. 두 draw일 때 unnamed는 2회, named는 1회 복사하며, 독립 산술 oracle과 픽셀 차이 0이다. 다음 프레임 초기화, segment 간 유지, 이름 두 개의 독립성도 검사한다.

배치에도 보수적인 월드 AABB 합집합 컬링을 추가했다. 모든 항목이 화면 밖인 배치는 draw를 제출하지 않는다. 경계는 float 오차 여유를 더하고, 신뢰할 bounds가 없으면 유지한다. 정지 상태의 run bounds는 재사용하고, 카메라 변화는 현재 VP로 판정한다. 부분적으로 보이는 배치 안의 소스별 완전 컬링까지 구현한 것은 아니다.

`state`에는 `grab_copies`, `grab_bytes`, `named_grab_hits`, `culled_draws`, `culled_sources`를 추가했다. 복사 바이트는 지원하는 camRT의 32/64/128비트 색 형식을 기준으로 센다.

`gpu_event_us`, `gpu_event_max_us`, `gpu_samples`, `gpu_pending`, `gpu_disjoint`는 64프레임 간격으로 수집한다. 타임스탬프는 deferred GPU 명령 목록 안의 compute·복사·draw 전후에 기록하고, disjoint/frequency를 검사한다. `GetData(DONOTFLUSH)`로 준비된 결과만 수거하며 기다리지 않는다. **이 시간은 native plugin event 범위이며, Unity 카메라 전체의 presentpath 시간이나 GPU busy와 같지 않다.** 초기 지오메트리 생성·CPU 명령 기록 시간도 이 GPU 구간과 구분한다.

**DD의 9배 GPU 시간이 Grab 복사 때문이었다고 확정하지 않는다.** 실제 DD에 named/unnamed가 각각 몇 번 그려졌는지는 새 state와 카메라 GPU 측정이 필요하다. unnamed Grab을 임의로 프레임당 1회로 줄이는 변경은 하지 않았다.

## 추가 HELLO 단서: 상수 값은 0이 아니었다

명세에 지목된 기존 `drfull-colors-20261115-112501-433.tsv`를 직접 읽었다. `nativeCB`는 개별 CB의 시작이 아니라 **선두 sprite 색 16B + VS의 여러 CB + PS의 여러 CB를 합친 패킷 전체**다. 원본 metadata로 stage·CB 크기를 누적해 해독하면 다음과 같다.

| 변수 | stage / CB 내부 오프셋 | 실제 패킷 오프셋 | 저장된 값 |
|---|---|---:|---|
| `_MainTex_ST` | VS `$Globals` / 48 | **64** | **(1,1,0,0)** |
| `_TintColor` | PS `$Globals` / 32 | **672** | **(0.5,0.5,0.5,0.5)** |

패킷 크기는 720B다. raw floats[8..11], floats[12..15]는 이 두 변수의 실제 위치가 아니다. 따라서 제공된 TSV는 `Value()`가 이 배경의 `_TintColor`·ST를 0으로 채웠다는 근거가 되지 않는다. 값을 임의로 기본값으로 덮어쓰는 변경도 하지 않았다. **HELLO 배경색의 실제 원인은 여전히 미확정이다.**

`tools/direct-renderer/decode-colors.py`를 추가해 기존 TSV의 **native pass 슬롯 2,044개, 변수 29,768개**를 전수 해독했다. 변형/배치 모호성 0, 해석 불가 0이다. 결과는 `out/direct-renderer/m4g/decoded-colors.tsv`와 동명 JSON이다. 옛 캡처에는 `_TintColor` 같은 모든 Unity 재질 값이 없으므로, 이 전수 해독만으로 모든 값의 Unity 일치를 선언하지 않는다.

`drfull colors`는 이제 기존 원본 TSV와 함께 `.constants.tsv`도 만든다. 모든 native 슬롯·pass·stage·CB·변수·component에 대해 CB 내부 오프셋과 실제 패킷 오프셋, material/MPB 존재 여부, typed Unity API로 독립 조회한 기대 값, 실제 바이트 값, match/MISMATCH를 기록한다. 기대 값 조회에서 `Value/Float/Integer/Fill`을 재호출하지 않는다. Color/Vector는 실행 중 실제 Unity shader property type으로 구분하고, material slot의 MPB 우선순위와 renderer 색·flip·mask cutoff도 구분한다. native에서 나중에 패치하는 matrix/time, engine texture 크기 등은 미비교로 명시한다. 이 새 Unity 전수 대조 명령은 구현·빌드했지만 **게임 안에서 실행하지 않았다.**

```powershell
py -3.12 tools/direct-renderer/decode-colors.py 'D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice/Mods/StutterFix/drfull-colors-20261115-112501-433.tsv'
```

## 검증과 재현

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/direct-renderer/check-m4g.ps1
py -3.12 tools/direct-renderer/audit-m4g-precision.py
powershell -NoProfile -ExecutionPolicy Bypass -File tools/direct-renderer/check-m4g-baseline.ps1
```

마지막 명령은 HEAD의 M4f native와 fixture를 별도 출력 폴더에서 빌드하는 참고 대조다. checkout·설치 DLL을 되돌리지 않는다. 모든 명령은 로컬 빌드/소프트웨어 WARP이며 게임을 실행하지 않는다.

| 검사 | 결과 |
|---|---|
| native / DEV / Player / PlayerAuto | 4종 빌드 오류 0; 기존 managed 경고는 남음 |
| export name / ordinal / hint | 기존 **74/74 보존** |
| ABI / resource / Player 경계 | 기존 Frame 408, Pose 128 및 기존 opcode 유지; 새 query는 끝에 추가; named metadata와 DXBC roundtrip 통과 |
| 원본 DXBC 배치 비교 | Tile/Floor 48 + Default 8 = **56조건, 다른 픽셀 0** |
| 가는 선·변환 비교 | **1,344변환, 다른 채널 0**, coverage 확인 |
| 업로드·정렬·개별 draw 분류 | sparse 80B/32B, 131포즈, 7,000소스 반전, 바이트 합계·원인 합계 검사 통과 |
| GPU query | WARP에서 비동기 timestamp 표본 수집 통과; 하드웨어/게임 측정 아님 |
| 배치 컬링 | 56조건의 완전 화면 밖 배치 draw 0 |
| Grab | unnamed 8 + named 8, 다른 픽셀 0; 복사 횟수·프레임·segment·이름 범위 통과 |
| 수명·준비·복원 | 기존 25 lifecycle, 10,000 mask, 안정 검사 100,000회 및 준비 pump 회귀 통과 |
| Canvas·stencil | 기존 합성 24·공유 stencil 9 회귀 통과 |
| packet·포즈 | SPSC 128, sparse native ID 5, packed/world-only ABI 회귀 통과 |
| 정렬 | 기존 7,000회 oracle 대조, 공통 이동/교차/제거, 안정 identity 검사 10,000회 할당 0 |
| FrameCompare | metrics 19 + masks 15 + state 15 + controls 17 회귀 통과 |
| 설치 DLL | 시작 대비 **native 동일, managed 외부 변경 감지**; 이 작업에서 설치·복원하지 않음 |

최종 로그와 감사 파일은 `out/direct-renderer/m4g/check-release-final.log`, `m4g-audit.json`, `final-hashes.json`, `validation/`에 있다. 변경된 로컬 native DLL을 세 managed 산출물에 포함했으며, 설치 경로에는 쓰지 않았다.

설치 managed DLL은 검증 시작의 `19EBA3B7…`에서 외부 작업 중 `B0EFE68D…`로 바뀐 것을 관측했다. native 설치 DLL은 `CDEE1098…`로 동일했다. 원래 스냅샷을 유지했고, 변경을 덮어쓰거나 복원하지 않았다. 따라서 설치 파일 2/2 보존 성공이라고 보고하지 않는다. `validation/installed-{before,after}.json` 및 `installed-result.txt`가 증거다.

## 실게임 계획과 남은 게이트

`tools/direct-renderer/plan-m4g.py`로 `out/direct-renderer/m4g/plans/`에 **4맵×6=24개** 계획을 만들고 감사했다. `m4g-{hl50,wf,dd,sh}-{plan,pixel,A1,B1,B2,A2}`다. hl50 50초, WF 258초, DD 50초, Exit 88초, 3440×1440 렌더다. 픽셀 후 ABBA 순서는 A1/B1/B2/A2, 각 측정 12초다.

B와 pixel만 warm/on/wait를 사용하고, A에는 warm observer를 설치하지 않는다. on 준비는 pause 안에서 끝내며 off/cold로 정리한다. 픽셀은 off/off 0을 먼저 요구한다. 새 `drfull assert clean`이 A/B 측정 전후와 pixel 준비에서 `safe=0`을 확인하고, 안전 모드면 결과를 거부한다. 안전 모드를 강제로 끄거나 설정 파일을 바꾸지 않는다.

계획은 **생성만 했고 실행하지 않았다.** 남은 실게임 게이트는 Exit >335 FPS, DD GPU 병목 귀속과 개선, DD 빨강 줄/Exit ±1~2의 실제 픽셀 0, main_max <2ms 및 준비 완료 시간이다. 오프라인 WARP의 전송량·픽셀 통과를 이 게이트의 달성으로 바꾸어 보고하지 않는다.
