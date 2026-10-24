# Claude 인계 — 장식 트레이스 이미지 재생, 2026-10-24 20:21 KST

## 먼저 읽을 상태

- 사용자가 **“잠깐 멈추고 클로드한테 줄 인계문서 만들어”**라고 요청해 개발과 시험을 중지했다. 자동 재개하지 않는다. 이 문서를 받은 뒤 사용자가 이어가라고 하면 진행한다.
- 실제 작업 폴더: `E:\StutterFixWork\wt-gpu`, 브랜치 `codex/night-gpu`, HEAD `1649c8725546cd9e53b5cd35dac87f4fe58beb74`.
- dirty 파일 **614개**. 기존 Claude 변경/시험이 대량 포함되어 있다. 일괄 checkout/reset/clean 금지. 커밋·푸시·main·태그·릴리스·ZIP은 이번 작업에서 하지 않았다.
- 중지 시 owned job 없음, 게임/Unity Editor 프로세스 없음. 최신 게임 시험 105/107/109/110 모두 정상 종료, 설치 원본 **9/9 상태 복원** 확인.
- **400 FPS 미달, 완성형 승인 전.** 새 이미지 GPU 소유권은 실제로 동작하지만 실제 게임 픽셀 차이가 남는다. 새 후보의 성능 시험은 아직 실행하지 않았다.
- **현재 소스는 최신 빌드와 다르다.** 마지막 검증된 managed DLL은 round109 빌드다. 그 이후 작성한 격리 픽셀 진단은 미완성이다. 아래 “중단 순간 초안”부터 처리해야 한다.
- 사용자 선호: 한정 G2는 이미 끝났으므로 반복하지 말 것. 미세 CPU 0.5ms보다 소유 장식 CPU 트윈/Transform 작업 제거, 그림·정렬·충돌체 보존, 무거운 구간 실 FPS 400과 끊김 제거를 우선한다. 인계 요청이 없으면 MD 갱신/보고 때문에 작업을 끊지 말고 구현을 계속하라고 했다.

## 바로 이어갈 작업

1. `GpuDecoImageIsolatedFixture.cs`와 `GpuDecoImagePlayback.cs`의 `images isolated` 호출은 **방금 쓴 초안**이다. csproj 포함도 아직 없다. 현 상태를 빌드 통과로 간주하지 않는다.
2. 목적은 원본 SpriteRenderer와 교체 MeshRenderer를 **각각 단독으로**, 실제 카메라 행렬/재질/텍스처/현재 GPU VB로 그려 비교하는 것이다. 단독부터 다르면 기하/셰이더 경로, 단독은 같고 합친 화면만 다르면 정렬·겹침·카메라/필터 경로를 조사할 근거가 된다. 아직 어느 원인도 확정하지 않았다.
3. 새 shader `ImagePixelProbe`는 현재 **픽셀마다 여러 InterlockedAdd**를 한다. 그대로 실행하면 진단 자체가 GPU 정체를 만들 수 있다. 64스레드 그룹 내 축약 후 그룹당 합계만 원자 연산하도록 먼저 줄인다. 경계 밖 스레드도 그룹 barrier에 참여해야 한다. 명시적 정지 진단만 사용하며 성능판과 분리한다.
4. fixture의 양쪽 비어 있음/양성·음성 대조를 점검한다. 현재 samples>0 강제와 의도적 차이 검출 대조는 아직 없다. DrawRenderer가 원본 forceRenderingOff 상태에서 실제로 그리는지도 확인해야 한다. 빈 그림 둘을 픽셀 통과로 세지 않는다.
5. csproj에 AUTOTEST 전용 포함 → 3빌드/LoadCheck → 새 bundle 별도 폴더 → 짧은 owned 경계 시험. **기존 bundle 또는 Claude 근거를 덮어쓰지 않는다.**
6. 실제 픽셀 차이를 해결한 뒤에만 아래 준비된 캡처 없는 성능 대조를 진행한다. 아직 못 잰 수치를 400 FPS로 추정하지 않는다.

## 이번에 실제 구현한 이미지 GPU 재생

연구용 AUTOTEST에만 들어간다. 일반 Player에는 포함하지 않는다. 기본 `gpuImageCandidate=false`, `gpuImageMatrix=false`.

- `GpuDecoImagePlayback.cs`: 준비된 이미지 이벤트·실제 대상 참조·asset ID·quad topology를 검증하고 GPU 소유권을 확장한다. imageFilename만 바꾸는 이벤트 대상에 한한다. 원래 크기/스무딩/crop 변경, 마스크, hitbox, 미지원 상태는 기존 CPU 경로다.
- 전체 영향 장식 5,087개 중 5,028개 eligible, 59개 null 이미지 인자는 실제 의미를 가정하지 않고 CPU에 남긴다. 렌더러 조건까지 적용하면 새 GPU 소유 **4,976개**, 기존 23,547 → **28,523개**.
- 이미지 이벤트는 GPU 스칼라 효과를 먼저 commit한 뒤, 원래 `FastMove.ImageBlock`을 **한 번만** 실행한다. 게임 원래 LazySprite 지연 대입을 유지한다.
- 원본 논리 필드 16개를 현재 GPU master 상태로 동기화하되 Transform을 매 프레임 쓰지 않는다. `DecoAnim.CopyGpuImageFields`, `InvisibleSkip.SyncGpuImageColor`가 보조한다. 후자는 해당 인스턴스만 잠시 허용하고 forceRenderingOff를 복원한다.
- 실제 이미지가 바뀌거나 LazySprite가 flush될 때만 shape/UV와 texture MPB를 갱신한다. 기존 mesh/index/VB는 교체하지 않는다. GPU 입력 쓰기 여러 개는 원자적이지 않으므로 실패하면 CPU 복귀한다.
- 숨은 장식 pending 목록만 확인하고 alpha가 양수가 될 때 원래 LazySprite flush를 호출한다. 전체 장식 Transform 갱신 루프를 새로 만들지 않았다.
- 오류 단계 구분: 논리 동기화 실패 전이면 CPU 복귀 뒤 이미지 블록 1회; 원본 setter 자체 예외면 복귀 후 재던짐; GPU 업로드 실패면 이미 실행한 스칼라/이미지를 다시 실행하지 않고 CPU 복귀한다.
- CPU 복귀 때 이미지 장식은 `d.UpdatePosition()`을 명시적으로 호출한다. 논리 필드가 같아도 원본 Transform은 GPU 재생 중 오래된 값일 수 있어서 단순 epsilon/같은 값 스킵으로는 복원되지 않는다.
- `Shutdown`은 `DisposeGpuLive()` 뒤에 이미지 candidate/fault/matrix 플래그를 지운다. 복원 전에 플래그를 지우면 이미지 경로를 놓친다.
- 이미지 이벤트는 현재 compiled packet 일괄 경로에서 제외한다. 개별 원래 대상 순서와 이미지 부작용을 유지한다. 순서 재정렬/중복 제거하지 않았다.

주요 변경 파일: `GpuDecoImagePlayback.cs`, `GpuDecoImagePlan.cs`, `GpuDecoImageVariants.cs`, `GpuDecoImageGeometryFixture.cs`, `GpuDecoExport.cs`, `GpuDecoActivation.cs`, `GpuDecoPackets.cs`, `DecoBake.cs`, `DecoAnim.Bake.cs`, `FastMove.cs`, `InvisibleSkip.cs`, `LazySprite.cs`, `GpuDecoGpu.cs`, `LiveDecoGeometry.cs`, `StutterFix.csproj`.

명령(모두 `gpudeco ` 접두어):

| 명령 | 조건/역할 |
|---|---|
| `images playback on` | 맵 snapshot/prepare 전에 선택 |
| `images compile` | 준비된 이미지 표/asset 메타데이터 수집 |
| `images own on/off` | 정지한 CPU 상태에서 새 4,976개 소유 여부 비교 |
| `images matrix on/off` | 정지한 CPU 상태에서 새 이미지 행렬 경로 선택 |
| `images playback state` | setter/upload/flush/pending/recovery 카운터 |
| `images audit` | 활성화 직후 CPU Transform vs GPU 정점/색/UV/재질 감사 |
| `images locate` | 정지 경계의 지정 화면 영역에 겹치는 장식 숫자 상태 로깅 |
| `images requireactive` | 실제 setter 실행, GPU 유지, recovery/native 오류 0 확인 |
| `images failupload` | 다음 이미지 geometry 업로드 뒤 오류 주입 |
| `images requirefallback` | 이미지 오류 뒤 GPU 해제/CPU 복원 완료 확인 |
| `images isolated` | **미완성 초안. 아직 호출하지 말 것** |

`images audit`의 CPU Transform은 **활성화 직후만** 비교 기준이다. GPU로 계속 움직인 뒤의 오래된 원본 Transform을 진짜 정답으로 쓰지 않는다.

## 완료 시험과 한계 — 재실행 전 새 가설부터

공통 실제 화면은 **2560×1440 / 120Hz / D3D11 / FullScreenWindow / 동기 0**, 모니터 꺼진 조건이다. 3440 결과로 확대하지 않는다. 아래 round 번호 경로는 `tools/optimization/out/` 아래다.

| 시험 | 결과 | 제한 |
|---|---|---|
| round99/100/101(직전 단위) | 이미지 대입 6,052회(즉시1,137/지연4,915) 관측 불일치0; 103asset×2슬롯 GPU 부분 갱신 불일치0; 격리 흰 quad 103이미지 픽셀0 | 기존 ledger 첫 절에 있음. 실제 맵 컬러/회전/정렬/연속 재생 증명은 아님 |
| round102/103 | 실제 256초 구간 기존 GPU vs 새 이미지 소유권 픽셀 차이 2,963 / 2,848; 각각 보존 | 103 old/old, new/new 반복 차이는0. 색8/UV/재질texture 감사0, 일부 위치 오차 있음 |
| round104 | 중간 seek 뒤 재개 즉시 기존 FastMove Take catch-up 가드로 CPU 복귀 | 보호를 끄지 않았다. GPU 유지 시험에서 제외 |
| **round105-image-natural** | 0초 자연 시작, GPU 28,523 유지; 260초 직전 setters6,734 / uploads6,256 / flushes1,075 / pending4,476 / recoveries0. 오류주입 뒤 CPU Rec11,831/11,831 복원·미관측순서0·pending0·정상 종료·원본9/9 | 260.022초 주입 복귀 35.479ms. 실제 장치 고장 시험/전곡 끝/픽셀/FPS 통과 아님. uploads에는 활성화4,976회가 포함됨 |
| round107-image-matrix | 행렬을 먼저 합성하는 v1: old/new **58/3,686,400픽셀**, maxRGBA4,1,25,0; CPU/new도 같은58. new 반복0 | 픽셀 미통과 |
| round109-image-locate | v1 새 표본 **42픽셀**, max3,2,20,0; 반복0; 88개 실제 숫자 상태 저장 | 시각이 다른 독립 실행이라 58→42를 개선률로 쓰지 않는다 |
| round110-image-native-matrix | native 정규화 반영 v2: **121픽셀**, max5,2,10,0; 반복0 | 정규화 중간값은 맞아졌지만 픽셀 미통과. 다른 표본이라 42→121만으로 회귀 크기를 정하지 않는다 |

위 105/107/109/110 manifest의 `PASSED`는 계획 실행/종료/복원 검사 범위다. `framecmp diff`는 nonzero라도 자체 실패하지 않는다. **manifest PASSED를 픽셀 승인으로 부르지 않는다.**

round105의 `recovery ... injected=False`는 공용 복구 테스트 플래그 값이다. 계획의 `images failupload`로 `injected_image_upload`를 낸 의도적 실패였다. 실제 자연 장치 오류로 오해하지 않는다.

최신 후보의 실 FPS/최악 프레임/전곡 끝/FG/Unload 호환성: **확인 안 됨**. 옛 packet 후보 round74/75의 무거운 260~270초 167.760/168.822FPS를 새 이미지 후보 성능으로 쓰지 않는다.

## 행렬 후보와 찾은 수치 근거

**Canonical shader는 `tools/gpudeco-lab/GpuDeco.compute`다.** `tools/gpudeco-lab/Assets/Shaders/`는 오래된 사본일 수 있다. `prepare.ps1`가 canonical→ignored `project/Assets/Shaders/`로 복사한다.

- 기존 shader는 이미 double 채널 평가, CpuQuotient, 반각 회전 다항식, UNorm8 반올림 보정을 갖고 있었다. 기존 수정 다시 만들지 않는다.
- v1 새 이미지 전용 `Flags & 64`: 고정 자식 행렬과 pivot TRS 열을 먼저 합성하고 원래 Sprite local corner에 곱한다. 옛 경로는 이미 변환한 shape 점을 scale/rotate했다.
- `GpuDecoGpu`는 `EvaluateImageLive` marker kernel이 있는 bundle만 optional `_ImageShape` 버퍼(8float4/item)를 할당/바인딩한다. 새 이미지 geometry 갱신 시 여기에 행렬/원래 corner를 넣는다. native export/mesh VB 크기 변경 없음.
- 지원 chain guard는 현재 직접 pivot→child→renderer / renderer identity / above identity만 허용한다. **이 guard의 Vector/Quaternion/Matrix `==`는 Unity 근사 비교일 수 있으니 채택 전 `.Equals` 등 정확 비교로 점검할 것.**
- Unity Editor 6000.3.10f1의 v1/v2 별도 픽셀 fixture32조건(큰 원점4·회전4·카메라각2): 픽셀 차이0. 하지만 CPU TransformPoint 정점 오차는 최대0.0078125world였고 실제 게임 픽셀은 nonzero다. 숫자 정점/32조건을 전체 시각 승인으로 확대하지 않는다.
- `GpuDecoNativeMatrixProbe.cs`: 실제 구간 87각도+균일2,049각도 =2,136조건. 기존 GPU 회전 basis가 native Transform과382조건 달랐다.
- 원인 중 하나: native Transform 정규화는 **곱 두 개를 float로 반올림한 dot→sqrt→각 성분 나눗셈**이다. Mono 관리 `Quaternion.Normalize`는 더 넓은 중간값으로 275조건 달랐다. 강제 float dot 모형은 native quaternion과 **0/2,136 불일치**.
- v2는 새 이미지 경로에만 `ImageRotation`을 쓰고 component 나눗셈을 `CpuQuotient`로 보정했다. GPU basis 불일치 **6/2,136**, 실제 구간87각도는0. 옛 `Rotation` 경로는 유지했다. **6조건 잔여와 실제 게임 픽셀 문제는 미해결**.
- native TransformPoint 산술 조사17,088성분: 분리 float 왼쪽 합산 불일치0, 오른쪽 합산4,302. 이 역시 Sprite 실제 raster의 동일성을 증명하지 않는다.
- v3(점 계산을 명시적 mad로 변경)는 fixture32 중 **19조건 / 658,066픽셀 / 최대252**로 실패했다. **미채택·게임 시험0**. canonical은 v2 수식으로 되돌렸다. v3 bundle/로그는 실패 근거로만 보존한다.

근거 파일:

- `out/image-matrix-candidate/bundle-v1/`, `bundle-v2/` — 각각 보존된 bundle.
- `out/round106-build/editor.log` — v1 bundle/32조건.
- `out/round109-build/matrix-v2-editor.log` — v2 bundle/32조건0.
- `out/round109-build/matrix-v3-editor.log` — v3 실패.
- `tools/gpudeco-lab/evidence/image-matrix.txt`는 **마지막 v3 실패로 덮인 fixture 출력**이다. v2 성공 파일이라고 읽지 않는다.
- `tools/gpudeco-lab/evidence/native-matrix.txt`는 v2 회전/정규화/점 산술 최신 결과.
- `out/image-matrix-candidate/native-matrix-v1.txt`, `locate109.json`(숫자 기록88), `angles109.txt`(중복 제거87).
- 게임 원본/새 비교 PNG는 설치 Mods의 `framecmp/` 아래 OLD107/NEW107A/CPU107, OLD109/NEW109A, OLD110/NEW110A 등. 이전 캡처를 새 결과로 올리지 않는다.

## 중단 순간 초안 / 빌드 상태

- 마지막 **3빌드 + LoadCheck 성공**: `out/round109-build/{dev,player,auto}.log`, `*-load.log`. PlayerAuto FieldRef280곳/틀림0. 일반 Player 연구 제외를 유지했다.
- 검증된 DLL `bin/PlayerAuto/StutterFix.dll` SHA256:
  `58006bca4cb08fa415a4f73e4e6a105dbc47ce8f593b6fda487532e4f787e5c5`
- native는 이번 단위에서 수정/재빌드하지 않았다. `native/sfnative.dll` SHA256:
  `39bc2aca032214ce48f2a869b9f9c3b1e3ada1eff4bffa3a85d9a9d48e94d45a`
- v2 bundle SHA256:
  `330b4a0cfd8b4212b3d88e942229c75957806599ca5603c2e7b77cdcdd55fb21`
- **현재 source≠DLL**: `GpuDecoImagePlayback.cs`의 `images isolated`가 새 파일 메서드를 부르는데 `GpuDecoImageIsolatedFixture.cs`는 아직 csproj에 포함되지 않았다. 따라서 그대로 Auto 빌드하면 누락 메서드 오류가 예상된다. 미검증 상태를 숨기지 말고 포함/초안 수정을 마친 뒤 빌드한다.
- canonical compute에는 v2 수식에 추가로 미컴파일 `ImagePixelProbe` kernel이 붙어 있다. **bundle-v2에는 이 새 kernel이 없다**. 새 bundle을 만들기 전에는 `images isolated`를 실행할 수 없다.
- 새 `GpuDecoImageMatrixProbe.cs`, `GpuDecoNativeMatrixProbe.cs`는 Editor 전용 도구다. `prepare.ps1`는 ImageMatrixProbe만 자동 복사한다. NativeMatrixProbe는 현재 수동으로 project/Assets/Editor에 복사되어 있다.
- `out/apply-image-matrix-candidate.py`는 이미 적용한 일회성 스크립트다. **재실행 금지**. 과거 문자열을 대상으로 하므로 현재 소스에 맞지 않는다.
- `out/image-matrix-candidate/run-pixel-then-performance.py`는 round110 픽셀0일 때만111/112를 시작하도록 한 controller였다. 픽셀121이라 assert로 종료했다. **111/112는 시작하지 않았다.** 재실행해도 같은 nonzero 조건에 걸리며, 가드를 지우고 돌리지 않는다.
- 준비만 된 성능 계획: `tools/optimization/out/round108-{image,old}-steps.json`. 자연0초→250~260/260~270 두 micro 경계, 캡처 없음. **아직 bundle-v1 경로이므로 채택 후보로 바꾼 뒤 새 run 이름으로 사용할 것.** `micro begin/end`는 경계의 frame/QPC만 사용하고 per-frame probe를 추가하지 않는다.

## 재개 시 운용 / 금지

- 기존 `tools/sfmeasure` trace API1 저장 계획/start/status로 호출 수를 아낀다. 오래된 MCP 연결이면 marker 확인, 또는 설정된 C: helper에 Python import로 접근한다. 실제 수정 대상 source는 E:다.
- helper `trace_runs.active_job()`부터 확인한다. 사용자 게임은 끄지 않는다. owned runner만 설치/실행/정상quit/원본9상태 byte 복원을 수행한다. 시험 중 source 빌드/DLL/native/bundle 교체 금지.
- manifest `bundle` SHA는 기본 고정 bundle 경로를 해시하는 경우가 있다. 이번 새 bundle은 명령에 별도 경로를 넘겼으므로 **manifest 기본 bundle SHA만 보고 실제 사용 bundle을 판단하지 않는다**. 계획의 명령 경로+해당 파일SHA+게임로그를 함께 확인한다.
- 3빌드: `dotnet build StutterFix.csproj -c Release`; `-p:Edition=Player`; `-p:Edition=Player -p:AutoTestBuild=1`. `tools/LoadCheck/bin/Release/net8.0/LoadCheck.dll`로 각각 검사, 일반 Player에는 `--no-bake`. native 수정 시 native→managed 순서.
- Unity 설치: `D:\StutterFixTools\UnityEditors\6000.3.10f1\Editor\Unity.exe`. 게임은 승인받은 현재6000.3.21f1alpha. Editor lab은 정확히6000.3.10f1. `tools/gpudeco-lab/prepare.ps1` 후 별도 output bundle, `-batchmode -force-d3d11 -projectPath ...project -executeMethod ... -logFile ...` 사용. PowerShell은 GUI exe 직접 호출만 하면 기다리지 않을 수 있으므로 `Start-Process -PassThru -Wait -WindowStyle Hidden`로 실제 exit를 확인한다.
- PowerShell→Python 파이프로 한글 literal을 보낼 때 `?`로 깨진 전례가 있다. 파일 읽기/ASCII escape/apply_patch를 사용한다. 최근 PowerShell profile이 읽기 명령을 지연시킨 적이 있어 단순 도구는 `login:false`가 빠르다.
- MinHook/vendor/전역 draw 후킹/Steam 바이트 우회/두 번째 swapchain 금지. 원래 swapchain/일반 Present/정렬/충돌체 보존. 기본 꺼짐·일반 Player 연구 제외.
- 자동화는 사용자 요청으로 삭제한 상태다. 새 예약/UAC/권한창을 만들지 않는다. 사용 한도 우회/모델 변경 금지. 사용자 중지 상태를 자동으로 풀지 않는다.
- 기존 완료 G2/round99~101/전곡을 이유 없이 반복하지 않는다. 한 문제30분 진전 없으면 이유와 남은 작업을3줄로 남기고 우회를 누적하지 않는다. 원인 가설과 확정 사실을 구분한다.
