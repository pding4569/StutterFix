# GPU exact 조사 결과 (2026-11-13)

요청한 독립 실험실, AUTOTEST 센서스 구현, [합성기 설계](../render-system/10-gpu-exact-compositor.md)를 완료했다. **현재 시험한 산술식 10개 중 모든 입력에서 ROP와 일치하는 식은 없었다. 실제 ROP로 만든 경험적 전이 LUT는 시험 범위의 반복 합성과 일치했다.** 제품 렌더러를 바꾸거나 성능 향상을 확인한 결과는 아니다.

## 이어받은 상태와 변경 범위

- 작업 경로 `E:\StutterFixWork\wt-v14prod`, 시작 HEAD `9ddc247`, 브랜치 `claude/adofai-renderer`.
- 요청 문서는 미추적 상태였고 이번 세 산출물은 없었다. 기존 ZCull·SceneRenderOrder·TileRender 재질 직렬화·native 도구 빌드 방식을 재사용했다. 기존 다수 out/증거와 이전 작업은 보존했다.
- 게임 실행·게임 조작·설치 DLL 교체·커밋·푸시는 하지 않았다. 기존 프로세스와 표시 설정도 조작하지 않았다. 설치/표시 상태의 전후 해시 검증은 수행하지 않았다.
- 독립 D3D11 GPU 정확도 실험은 실행했다. GPU 부하가 생기므로 동시에 진행 중인 다른 성능 측정의 유효성을 이 결과로 보장하지 않는다. 이번 실험에서는 GPU 시간·게임 FPS를 측정하지 않았다.
- 제품 기본값과 기능은 바꾸지 않았다. 새 C# 파일은 프로젝트 항목과 파일 전처리 모두 AUTOTEST 전용이다. DEV/Player에는 새 센서스 타입/명령이 없다.

## A. 독립 블렌드 실험실

구현: [tools/blendlab](../../tools/blendlab/README.md). Windows D3D11 하드웨어 디바이스, **NVIDIA GeForce RTX 4060 Ti 한 대**, vendor 4318/device 10243, feature level 11_0, RGBA8_UNORM Linear, MSAA 1. 다른 GPU·드라이버·sRGB·MSAA로 일반화하지 않는다. 드라이버 버전은 이번 도구에서 수집하지 않았다.

```powershell
cmd /c tools\blendlab\build.bat
& tools/blendlab/blendlab.exe "$((Get-Location).Path)\tools\blendlab\lab.hlsl" > tools/blendlab/result.txt
py -3.12 tools/blendlab/analyze.py
py -3.12 tools/blendlab/inspect-assets.py
```

최종 native 빌드·실행·분석은 종료 코드 0. readback/API 실패와 반복 LUT 불일치는 실패 종료하도록 했다. 후보식 불일치는 표로 남긴다. 원시 [result.txt](../../tools/blendlab/result.txt), [불일치 200조건 CSV](../../tools/blendlab/mismatches.csv), [집계 JSON](../../tools/blendlab/summary.json), [게임 에셋 블렌드 상태](../../tools/blendlab/asset-states.json).

### 실제 경로 확인

게임 파일을 UnityPy로 **읽기만** 했다. 이번에 직접 읽은 상태는 asset-states.json에 보존했다.

| 경로 | 확인한 블렌드·쓰기 | 알파 처리 |
|---|---|---|
| Sprites/Tile (`sharedassets4.assets`) | One / OneMinusSrcAlpha, Add, RGBA | a + dstA·(1-a) |
| Sprites/Default (`Resources/unity_builtin_extra`) | One / OneMinusSrcAlpha, Add, RGBA | a + dstA·(1-a) |
| Legacy Shaders/Particles/Additive (`resources.assets`) | SrcAlpha / One, Add, **RGB만 쓰기** | dstA 유지 |
| Hidden/BlendModes/SpritesDefault/Grab (`resources.assets`) | 복수 패스: One/Zero 및 One/OneMinusSrcAlpha | 단일 블렌드 식만으로 재현 불가 |
| GpuDecoBatch (`tools/gpudeco-lab/GpuDecoBatch.shader`) | 기본 재질 인자 One / OneMinusSrcAlpha, 상태는 재질로 가변 | 기본 `(rgb*a,a)`, LinearDodge `(rgb*a,0)` |

타일 프리멀티플라이는 기존 `out/original-tile-shader/program-4.txt:29-30`의 `mul o0.xyz, r0.wwww, r0.xyzx; mov o0.w, r0.w`에서도 확인했다. GpuDeco 소스는 기본 463행, 가산 462행이다. straight SrcAlpha/InvSrcAlpha와 One/One은 추가 비교군이며 원본 타일이 이 경로라는 뜻이 아니다. Legacy RGB-only와 GpuDeco 가산은 실수 RGB 식이 같아도 중간 정밀도는 다를 수 있다.

### 입력·후보와 불일치

모드별 dst 256 × source color 256 × alpha 256 = **16,777,216입력**을 실제 PS+ROP로 그리고 같은 입력의 compute 후보와 비교했다. 추가 float 알파는 `5.1f/255`(실제 float 0.0199999996), `0.1234567f`, `0.5001f`; 각각 65,536입력이다. 첫 값은 인계 문서의 HELLO 예시를 재현한 상수이며 이번에 게임에서 채집한 값은 아니다. 나머지 둘은 임의 float 경계 탐색용이다. alpha byte 전수와 float 대표 3종의 범위를 구분한다.

PS는 sourceColor/255를 PMA 경로에서 alpha와 곱해 출력한다. compute는 float round-even / +0.5 floor, 255·256·512·1024단계 소스·인자 양자화, 8비트 정수 곱합/255, 8비트 인자 확장/256, 곱항별 반올림 등 **10개 식**을 검사한다. 정확한 식은 lab.hlsl에 있다.

| 모드 | float round-even: byte 입력 RGB 불일치 | A 불일치 | 8비트 정수 곱합/255: RGB 불일치 |
|---|---:|---:|---:|
| 0 PMA 기본 | 4,146,696 | 0 | 527,360 |
| 1 straight 비교군 | 0 | 0 | 0 |
| 2 Legacy 가산 RGB-only | 0 | 0 | 0 |
| 3 GpuDeco 가산 `(s*a,0)` | 397,084 | 0 | 397,084 |
| 4 One/One 비교군 | 0 | 0 | 0 |

byte 전수만 맞는 모드도 추가 float까지 포함하면 10개 후보 모두 적어도 한 입력에서 실패했다. 특히 모드0 a≈0.02의 단순 float 식은 RGB **15,748/65,536**, A **3,584/65,536** 불일치다. 모드0 byte 입력의 첫 float RGB 실패 인덱스 65920은 dst=128, color=1, alpha=1(각 /255)이다. CSV에는 채널별 첫 실패 입력·불일치 수·최대 바이트 오차를 넣었다. 일부 후보는 최대 2/255이고 표의 단순 float 예는 최대 1/255다.

이는 후보군을 기각하는 결과이며 정확한 일반 산술식이 존재하지 않는다는 증명은 아니다. 동적 float 전부를 검증한 것도 아니다. 소스 PS fixed/half, 텍스처 필터링, 독립 RGBA 패턴은 별도다.

### 경험적 f^k LUT와 고정점

각 모드·float 알파·source color마다 ROP의 dst 0..255 전이표를 실제 readback으로 만들었다. 같은 층을 실제 ROP로 반복 그리고 CPU에서 전이표를 반복 적용한 RGB/A와 대조했다.

- 5모드 × float 알파 3종 × 65,536픽셀, k=1..255.
- **k=1은 전이표 정의 자체**여서 독립 검증에 포함하지 않는다.
- k=2..255의 독립 RGB/A 비교 **499,384,320회, 불일치 0**.
- 이 표는 GPU 측정에 기반한 경험적 표다. GPU compute에서 표를 생성·조회하는 성능이나 모든 byte alpha의 k=255 반복을 측정한 것은 아니다.

모드0, a=5.1f/255의 RGB 고정점:

| source color | f(d)=d인 dst 바이트 | 개수 |
|---|---|---:|
| 0/255 | 0..25 | 26 |
| 128/255 | 128..178 | 51 |
| 255/255 | 230..255 | 26 |

다른 모드·알파의 목록도 원시 로그에 있다. 한 층의 고정점만으로 다른 색인 나머지 모든 층을 생략할 수는 없다. 실제 적용 시 사용자 GPU에서 소스 float 비트와 ROP 상태별 표 생성/반복 검사를 하거나, 일반식 발견 후 자체 검사를 통과해야 한다.

## B. AUTOTEST 전용 `gpuexact census N`

[GpuExactCensus.cs](../../GpuExactCensus.cs). 명령 `gpuexact census 120`, 중단 `gpuexact stop`. 시작 시 원본 renderer 설정(GpuDeco=0, RunLod=false), mono orthographic camera, 다른 GPU 진단과의 충돌을 검사한다. 주 카메라 onPreRender에서 프레임당 한 번 관찰하며 AutoTest는 완료까지 같은 명령을 poll한다. 장면 리셋·모드 unload·AutoTest batch 종료에서 이벤트를 해제한다. renderer 숨김·재질/MPB 변경은 없다.

서명은 화면 행렬 **VP×localToWorld의 float 비트**, sprite/geometry/UV/flip, texture와 sampler/updateCount, 모든 재질 property/keyword/pass 상태, 알려진 MPB 색/텍스처, 활성 상태, 정렬 키다. 미추적 MPB는 일치 판정에서 제외한다. 카메라 clear/배경·RT·viewport·클리핑 조건도 비교한다. 카메라 이동은 각 항목의 화면 행렬에 반영하며, 카메라에 붙어 화면상 고정된 물체까지 일괄 무효화하지 않는다. 최초 프레임은 시간 비교에서 제외한다.

마지막 한 번의 `[gpuexact]` 줄에 다음 mean/min/p10/p50/p90/max가 나온다.

- `prefix`: 맨 아래부터 같은 순서로 이어지는 변화 없는 연속 층 비율. clear/background 변화나 해당 층의 화면 행렬 변화는 prefix를 무효화한다.
- `unchanged_all`: 중간 변화와 무관하게 변화 없는 층 전체의 참고 비율.
- `runs`: 인접한 같은 텍스처·같은 색/알파·같은 재질 상태의 run(2층 이상) 비율.
- `runs_texel_alpha1_estimate`: run 면적에 sprite texture 알파 1 내부 비율을 가중한 추정치. point는 alpha=255 texel, bilinear는 오른쪽/위/대각 이웃까지 255인 영역을 사용한다. packed/mips/trilinear/aniso는 unknown이다.

**모든 비율의 조각 분모는 clipped projected AABB 면적 추정**이다. ZCull과 같은 visible renderer/material-slot 열거 및 SceneRenderOrder 정렬 모델을 쓰지만 ZCull의 별도 GPU coverage pass는 실행하지 않는다. 회전·얇은 sprite·겹친 triangles·particles에서는 실제 fragment와 다르다. 히스토그램도 실제 UV/화면 필터 발자국 측정이 아니다. run의 alpha=1은 RGB 상수까지 보장하지 않는다. 이 수치는 채택/성능 증거가 아닌 후속 실험 대상 상한의 proxy다. unknown shader·SortingGroup·mask·particle·같은 재질까지 동점은 보수적 장벽이다.

### bench_v14.py 연결 예시 — 실행하지 않음

공용 `E:\StutterFixWork\bench_v14.py`는 다른 작업과 공유하므로 이번에는 수정하지 않았다. `CFG_MID` 정의 이후 넣을 정확한 항목:

```python
CFG2["gxCensus"] = (dict(CFG2["rlNR"][0], GpuDeco=0, RunLod=False),
                    list(CFG2["rlNR"][1]))
CFG_MID["gxCensus"] = ["gpuexact census 120"]
# KEYS가 정의된 뒤 추가: 결과 요약에 마지막 센서스 한 줄도 보존
KEYS += ",[gpuexact]"
```

나중에 게임 사용이 허용된 단독 진단 시간에만:

```powershell
$env:BENCH_WT='wt-v14prod'
py -3.12 E:\StutterFixWork\bench_v14.py gx-hello hl50 gxCensus --build
py -3.12 E:\StutterFixWork\bench_v14.py gx-arche ar gxCensus
```

CFG_MID는 seek/waitsongpos 이후 실행된다. 명령은 120프레임 완료까지 기다린 뒤 다음 FPS 구간으로 넘어가므로 센서스 관찰 구간과 후속 FPS 구간을 혼동하지 않는다. 이 설정의 자동 FPS 출력은 이번 최적화의 성능 결과로 쓰지 않는다. 실패 시 AutoTest가 실패해야 하며 출력이 없다고 성공으로 취급하지 않는다. **위 게임 명령은 이번 작업에서 한 번도 실행하지 않았다.**

## C. 설계·검증과 다음 진행점

[10-gpu-exact-compositor.md](../render-system/10-gpu-exact-compositor.md)에 반복 LUT·고정점·prefix 캐시·타일 목록 합성, 사용자 GPU 자체 검사, 원본 fallback, 셰이더 ID→재질 ID 동점 규칙, 미확정 동점 장벽, 마스크/필터/파티클 범위, 센서스 입력용 시간/면적 상한 계산식을 정리했다.

검증 로그:

| 검사 | 결과 |
|---|---|
| 독립 native build + run + analyze | 종료 0, 반복 LUT 불일치 0 |
| AUTOTEST Release | 오류 0 (`out/gpuexact-auto-build.txt`) |
| Player Release | 오류 0 (`out/gpuexact-player-build.txt`) |
| DEV Release | 오류 0 (`out/gpuexact-dev-build.txt`) |
| 빌드 DLL의 센서스 타입 포함 여부 | AUTOTEST에만 존재, Player/DEV에는 없음 (메타데이터 문자열 검사) |
| 결과·설계·README 상대 링크 | 10개 모두 존재 |
| `git diff --check` | 통과 |
| 실제 게임 센서스/전체 곡 픽셀 비교/성능 | **미실행·미측정** |

기존 참조 버전 충돌·미사용 필드 경고는 남아 있다. 새 센서스 runtime lifecycle/수치의 게임 검증은 하지 않았으며 컴파일 통과로 대신 주장하지 않는다.

다음 우선순위는 Claude가 게임 사용을 끝낸 뒤 위 센서스로 prefix/run proxy를 먼저 수집하는 것이다. 이후 실제 coverage로 분모를 교정하고 RGB 상수 내부 비율을 측정한다. 이득 가능성이 있으면 경험적 ROP 전이표의 생성·메모리·GPU 조회·원본 가장자리 경계 비용을 포함한 독립 벤치를 만든다. 현재 일반 Q식이 실패했으므로 임의 가장자리 전체를 compute에서 비트 동일하게 처리할 수 있다고 가정하지 않는다.
