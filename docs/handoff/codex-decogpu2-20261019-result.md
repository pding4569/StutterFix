# DecoGpu 2 작업 결과 (2026-10-19)

## 범위와 결론

- 현재 HEAD `86010bd`에서 작업했다. `7bf2892` 이후 커밋된 후보 선정·추상 메서드 제외 수정은 유지했다.
- 수정 파일: `DecoGpu.cs`, `FuseProbe.cs`, 이 결과 문서. 시작부터 있던 `.codex/config.toml` 수정과 미추적 파일은 건드리지 않았다.
- 게임 실행, Mods 설치, 커밋, 푸시는 하지 않았다. 빌드는 저장소의 bin/obj에만 만들었다.
- 첫 렌더 전에 중단될 수 있는 확정적인 코드 결함을 고쳤다. 실제 Arche의 렌더 성공·후보 수·픽셀 동일성·성능 개선은 이번 작업에서 측정하지 않았다.

## 원인 분석

### renders=0과 자동 중단을 함께 설명하는 남아 있던 검사

기존 Begin은 `HasPropertyBlock()` 거절을 제거했지만, 매 프레임 사용하는 `Valid(Item)`에는 `!i.R.HasPropertyBlock()`이 남아 있었다. 호출 경로는 `TightLate.Step → DecoGpu.SkipLate → Valid → Stop`이다. 따라서 스프라이트 내부 블록이 있는 선정 장식은 다음 LateUpdate에서 PreCull보다 먼저 세션을 끝낼 수 있다. Compare 안의 Build도 같은 검사에 걸릴 수 있었다. 카메라 콜백 미호출을 먼저 단정할 수 없는 이유다.

인계서의 관측 로그에는 중단 지점이 없어, 그 실행에서 정확히 이 분기로 중단됐다고 확정하지는 않는다. 수정 후 first-stop과 콜백 횟수로 구분한다.

### 카메라와 RT 경로

로컬 게임 `Assembly-CSharp.dll`을 `tools/ilscan`으로 읽었다.

- `scrCamera.Awake`: Camera 컴포넌트를 `camobj`에 보관한다.
- `scrCamera.SetupRTCam`: `Bgcamstatic`, `BGcam`, `camobj`의 targetTexture를 `camRT`로 설정하는 경로가 있고 `Overlaycam`, quad도 다룬다.
- `scrCamera.UpdateCustomFrameRateScreen`: `Bgcamstatic`, `BGcam`, `camobj`에 대한 Camera.Render 호출 경로와 Graphics.Blit이 있다.
- `FrameGen.cs`도 장면 추적에는 camobj, 별도 합성 시점에는 Overlaycam을 구분한다.

즉 camobj가 화면에 직접 출력하지 않는다는 이유만으로 잘못된 카메라는 아니다. enabled=false 역시 수동 Render 경로에서는 미호출 증거가 아니다. 근거 없이 최종 합성 카메라로 바꾸지 않았다. Built-in/Forward 제한과 `BeforeForwardAlpha`를 유지했다. 실제 Arche에서 그 카메라가 해당 장식 레이어를 그리고 해당 이벤트를 실행하는지는 다음 실게임 probe와 compare로 확인해야 한다. IL 호출 목록만으로 투명 패스 실행·픽셀 출력을 검증했다고 주장하지 않는다.

## 수정 내용

### 중단 진단과 복원

- `Valid`를 실패 이유가 있는 `InvalidReason`으로 바꿨다. TightLate.EngineReason 번호, 장식 ID, sprite/material/sorting/layer/renderer 상태, pivot/scale 변경을 구분한다.
- BoundaryPrefix는 실제 원본 메서드명과 장식 ID, comparing 여부를 남긴다. 경계 훅을 무시해서 세션을 억지로 유지하지 않는다.
- Update의 재생 종료, 리셋, 일시정지, timeScale, manager/camera/controller 소실, camobj 교체, 설정 변경을 각각 기록한다.
- `first-stop`은 첫 중단 이유와 프레임을 보존한다. 이후 state/end/Unload가 덮어쓰지 않으며, 다음 begin 시 새 세션 기록으로 초기화한다.
- 종료 뒤에도 stopped-renders/stopped-draws와 콜백 횟수를 state에 보존한다. 기존 restore/restore-mismatch는 누적 카운터다.
- GameObject/컴포넌트 생성부터 Begin 전체를 try/catch로 감쌌다. 실패 시 소유한 원본 복원, 전용 Harmony ID 해제, PreCull/PostRender 해제, 커맨드 버퍼 제거·해제, 생성 Mesh/Material/번들 정리를 수행한다. 기존 Main.Unload의 DecoGpu.Stop 연결을 유지했다.
- 커맨드 버퍼 제거·해제 실패를 개별 처리하여 원본 복원까지 건너뛰지 않도록 했다.

### 렌더 시작과 compare

- 첫 Build가 전 배치 명령을 성공적으로 기록하기 전에는 원본을 숨기거나 TightLate/ApplyColor를 건너뛰지 않는다.
- 선택 카메라의 첫 PreCull에서 Build에 성공하면 원본을 숨긴다. 콜백이 120프레임 동안 오지 않으면 원본을 유지한 채 명시적 이유로 중단한다. 이는 제출 전 안전장치이며 GPU 실행 완료 보장은 아니다.
- PreCull 전체/선택 카메라 횟수와 선택 카메라 PostRender 횟수를 별도로 센다.
- Compare는 실제 일반 경로의 제출 및 PostRender가 한 번도 없으면 거절한다. 두 번의 동기 렌더를 실게임 렌더 성공처럼 집계하지 않는다.
- Compare의 Build와 종료 후 명령 재작성은 `Build(false)`로 수행하여 TotalTicks, 렌더 수, 제출 시간 합계에 넣지 않는다.
- Compare 중 경계 훅 등으로 중단되면 첫 이유를 유지하고 예외로 알린다. finally에서 이미 복원한 원본을 다시 숨기지 않는다. 카메라 targetTexture, RenderTexture.active 및 임시 픽셀 자원도 정리한다.

### MaterialPropertyBlock

- 존재 여부만으로 거절하지 않는다. 재사용 블록에 GetPropertyBlock을 읽고 알려진 속성의 지원 여부를 확인한다.
- `_MainTex`는 현재 sprite.texture와 같아야 한다. 외부 알파, 별도 알파 텍스처, 지원하지 않는 색 또는 flip 재정의는 거절한다. 기본 흰색과 renderer 색에 대응하는 표준 내부 값은 허용한다.
- 게임 IL에서 `BlendModes.BlendModeEffect.SetMaterialProperties/SetMaskMaterialProperties`가 쓰는 외부 속성을 확인했다. `ShaderUtilities`의 OverlayColor/OverlayTexture/OverlayTextureST/BlendStencilComp/NormalStencilComp/StencilId PropertyId 6개를 begin/probe 때 한 번 읽어 캐시한다. 그 재정의가 블록에 있으면 지원하지 않는 경로로 거절한다. 필드 누락 시 begin 실패로 복구한다.
- 후보 선정과 각 Build에서 검사한다. SkipLate에는 블록 전체 읽기를 중복해서 넣지 않았다.
- MPB 임의 속성 전체를 열거할 수는 없다. 알 수 없는 속성, 직접 재질 변경, 스프라이트 셰이더 세부 차이, 다른 투명 물체와의 정렬은 여전히 픽셀 대조 대상이다.
- Arche의 실제 속성 샘플은 게임 실행 금지로 채취하지 않았다. probe가 다음 라운드에 최대 16개 보이는 장식의 표준/BlendModes 블록 값을 출력한다. 추가 안전 검사 때문에 선정 수가 기존 241개와 달라질 수 있다.

### decogpu probe / FuseProbe

- probe: 로드된 씬의 Camera 이름/ID, 선택 여부, actualRenderingPath, enabled/active, depth, cullingMask, RT 이름/ID/크기, 카메라별 PreCull 관측 수를 출력한다.
- 카메라별 후보 장식 layer와 개수, 모든 CameraEvent의 GetCommandBuffers 이름·크기, 보이는 장식 MPB 샘플을 출력한다. 레이어 개수는 프러스텀·최종 가시성을 뜻하지 않는다.
- 카메라 호출 관측은 활성 세션에서만 하며 최대 32개 ID를 보관한다. 매 프레임 이름 로그나 장면 전체 검색을 하지 않는다. 종료 후 probe의 카메라별 관측 수는 새로 살아 있는 세션의 수가 아니므로 종료 state의 보존 합계를 함께 본다.
- FuseProbe.End의 DecoGpu 계산·제출 줄을 유지하고 compare 제외 설명 및 DecoGpu.State를 덧붙였다. 자동 중단된 세션을 0ms 성공 결과로 읽지 않도록 한다.
- 측정값은 Build의 CPU 계산·명령 기록 비용이다. MPB 검사도 여기에 들어간다. 전체 CPU 비용이나 GPU 시간은 아니며, SkipLate 검사·PreCull 카운터 등 Build 바깥 비용은 profsamp 등과 함께 판단해야 한다.

## 검증

| 항목 | 결과 |
|---|---|
| DEV: dotnet build StutterFix.csproj -v q --nologo | 종료 0, 오류 0, 경고 35 |
| Player: 위 명령 + -p:Edition=Player | 종료 0, 오류 0, 경고 4 (증분 빌드) |
| PlayerAuto: 위 명령 + -p:Edition=Player -p:AutoTestBuild=1 | 종료 0, 오류 0, 경고 35 |
| ILScan TYPES=StutterFix.DecoGpu | DEV 1 / Player 0 / PlayerAuto 1 형식 |
| git diff --check (작업 파일) | 오류 없음 |
| 결과 문서 | apply_patch로 작성, UTF-8 BOM 및 한글 재확인 |

최종 빌드 로그: `%TEMP%\stutterfix-decogpu2-20261019\Dev.log`, `Player.log`, `PlayerAuto.log`. 경고에는 참조 어셈블리 버전 충돌과 기존 unused/obsolete 경고가 포함된다. 일반 Player는 DecoGpu와 FuseProbe 소스가 제외되는 기존 조건을 유지한다. 실게임, Unity 픽셀 fixture, 성능 측정은 실행하지 않았다.

## Claude의 다음 실게임 순서

### 1. 기존 재현 + 카메라/블록 확인

```text
waitsongpos 250
decogpu probe
decogpu begin 500
wait 3
decogpu state
decogpu probe
decogpu compare
decogpu state
decogpu end
decogpu state
decogpu probe
```

- begin 직후 originals-hidden=False는 정상이다. 첫 성공 PreCull 이후 True, renders/draws-total/preCull-selected/postRender-selected가 증가해야 한다.
- active=False라면 first-stop부터 읽는다. `SkipLate ... TightLate.EngineReason=3`은 실제 숨김 전환일 수 있다. `boundary ... comparing=True`면 compare 중 발생한 게임 setter 경계다. `PreCull ... MPB ...`이면 출력된 샘플과 지원 조건을 대조한다.
- preCull-all만 늘고 selected가 0이면 probe의 카메라 ID·선택·RT·레이어를 비교한다. 선택 카메라에서 StutterFix.DecoGpu 버퍼가 BeforeForwardAlpha에 붙었는지도 확인한다. 종료 후에는 해당 버퍼가 사라져야 한다.
- 성공한 compare는 `max-channel=0`, `changed=0/147456`이 512x288의 그 두 렌더에서 같다는 뜻이다. 화면에 대상 장식이 실제로 보였는지 함께 확인해야 한다. 이 수치만으로 원래 해상도·곡 전체·정렬 전체의 동일성을 주장하지 않는다.
- 차이가 있으면 정렬/셰이더/MPB와 시간 의존 필터를 구분한다. 동기 Camera.Render 두 번 사이에도 렌더 콜백은 실행되므로 차이 하나로 특정 원인을 단정하지 않는다. compare 거절/예외/자동 중단이면 성능 단계로 넘어가지 않는다.
- end 전후 restore-mismatch 증가가 0인지 확인한다. restore는 실제 복원한 살아 있는 렌더러 수의 누적 값이므로 begin의 managed와 해당 구간 증가분을 비교한다. 다시 begin할 수 있는지도 별도로 확인한다.

### 2. 동일 구간에서 fuse / profsamp ABBA

픽셀 대조와 복원 확인 뒤, 각 판을 같은 시작 조건과 같은 곡 시각으로 다시 맞춘다. 연속 재생 중 A→B로 넘어가 다른 내용을 재지 않는다. 필요하면 판마다 새 프로세스를 사용한다.

A는 decogpu end 상태, B는 다음과 같이 시작한다.

```text
waitsongpos 252
decogpu begin 10000
wait 3
decogpu state
```

B의 첫 제출·PostRender를 확인한 뒤 모든 판에서 같은 곡 시각에 같은 길이로 측정한다. A에도 동일한 준비 대기를 적용한다. 각 구간의 예시는 다음과 같다.

```text
fuse begin
profsamp begin 1000
wait 10
profsamp end
fuse end
decogpu state
```

순서는 A → B → B → A. B는 매 판 새 begin으로 선정하고 끝나면 end한다. probe/compare/begin/end 및 패치 설치 시간은 측정 구간 바깥에 둔다. 한 B라도 중간에 멈추면 그 판을 유효한 B 성능으로 쓰지 않는다. managed/batches/renders와 같은 시각의 맵 상태를 함께 남긴다.

profsamp 자체가 스레드를 주기적으로 정지시키므로 무계측 프레임/끊김 확인도 별도로 해야 한다. 10000은 선정 상한이며 실제 managed=10000을 뜻하지 않는다. 투명 정렬·원본 화면 동일성이 확보되기 전에는 성능 수치가 좋아도 제품 적용 근거로 쓰지 않는다.
