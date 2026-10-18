# Arche GPU 분해 도구와 오프라인 조사 결과 — 2026-10-18

요청한 `gpuprobe on|state|off`와 `gpucensus`를 구현했다. 구간 집계를 위해 `gpuprobe reset`도 추가했다. **게임 실행·Mods 설치·커밋은 하지 않았다.** GPU 절약을 입증할 게임 측정은 아직 없으며, 렌더링 최적화는 추가 적용하지 않았다. FuseLate 변경은 보존했다.

## 변경 파일과 산출물

- `GpuProbe.cs`: 선택적으로 켜는 D3D11 카메라/OnRenderImage timestamp 세션.
- `GpuCensus.cs`: 카메라별 렌더 후보·재질·셰이더·텍스처·정렬 층의 일회성 TSV 조사.
- `tools/gpuprobe/query.cpp`, `build.ps1`, `check.cpp`, `README.md`, `.gitignore`: 독립 진단 DLL과 WARP 검사기. 기존 `tools/filtergpu/query.cpp`의 D3D11 device 획득/쿼리 패턴을 참고했다. sfnative와 기존 filtergpu 코드는 변경하지 않았다.
- `AutoTest.cs`: 명령 두 줄만 추가. `StutterFix.csproj`: DEV/AUTOTEST 조건부 Compile 두 줄만 추가. `StutterFix.cs`: DEV/AUTOTEST 해제 경로에 `Try(GpuProbe.Stop)` 한 줄만 추가.
- 로컬 빌드: `bin/PlayerAuto/StutterFix.dll`, `bin/Player/StutterFix.dll`, `bin/Debug/StutterFix.dll`, `tools/gpuprobe/gpuprobe.dll`, `tools/gpuprobe/check.exe`. 네이티브 산출물은 도구 폴더 .gitignore로 제외했다. 내장 리소스·제품 패키지에 넣지 않는다.
- 근거 파일: 이 문서와 같은 디렉터리의 `codex-gpu-20261018-{playerauto,player,dev}-build.txt`, `native-build.txt`, `native-check.txt`, `preservation.txt`, `camera-il.txt`, `filter-il.txt`, `vfx-il.txt`.

## 확인한 원래 코드와 IL

1. `FilterGpuProbe.cs`는 이름이 `CameraFilterPack_`로 시작하는 형식만 계측한다. 반면 `ffxSetFilterPlus.SetFilter`는 **CameraFilterPackLegacy_*** 필드와 `UnityStandardAssets.ImageEffects.CameraMotionBlur.velocityScale`을 바꾼다(`filter-il.txt`). 기존 Arche 전체 곡 결과의 CameraFilterPack GPU 0.000ms는 이 구형 필터들의 비용 0을 뜻하지 않는다. 새 도구는 접두어 대신 카메라에 붙은 OnRenderImage 메서드를 찾아 감싼다. 지금은 실제 활성 필터 목록/비용 미측정이다.
2. `scrCamera.SetupRTCam`은 BGStatic/BG/본 카메라의 targetTexture를 설정하고, `Update`는 화면 크기 변경 때 RT를 다시 만든다. `UpdateCustomFrameRateScreen`에는 Camera.Render와 Graphics.Blit 경로도 있다(`camera-il.txt`). **카메라 disabled만으로 렌더 비용 0이라 할 수 없다.** census는 활성 GameObject의 disabled 카메라도 수동 Render 후보로 표시한다. `BGStaticCam`이라는 이름만으로 RT 재사용을 허용할 수 없다. RT의 실제 공유 여부는 census의 텍스처 instance ID로 확인해야 한다.
3. 알파 0 생략은 이미 `InvisibleSkip`에 있다. ApplyColor 뒤 forceRenderingOff를 설정하며, Sprites/·Hidden/BlendModes/·지정 Additive 셰이더만 허용한다. census는 forceRenderingOff인 대상을 제외한다. 남는 알파 0 후보가 생겨도 임의 셰이더의 알파 의미를 일반화해서는 안 된다.
4. 기존 `docs/handoff/filters-findings.md`의 반해상도 연구는 5개 후보 모두 화질 차이로 미채택이다. 예를 들어 AAA_SuperComputer의 해당 20초 ABBA 구간 GPU 평균은 4.60→3.90ms였지만 최대 픽셀 차이는 9/255였다. 이는 **과거 다른 구간의 근거**이며 Arche 270초 절약량이 아니다. WideScreen은 2.10→2.20ms로 악화된 사례도 있다.

## gpuprobe의 측정 범위와 비용 제한

- on은 **맵 로딩 후 press 전에** 실행한다. 카메라 32개/OnRenderImage 인스턴스 477개까지 등록한다. disabled 필터도 등록하므로 이후 enabled 전환을 추적한다. 신규 카메라/컴포넌트 생성 후에는 다시 on해야 한다. 한도를 넘거나 설치가 실패하면 정리 후 오류를 낸다. 패치 설치 자체의 시간은 측정에서 제외한다.
- off에서는 객체·코루틴·카메라 정적 이벤트·카메라 명령 버퍼·Harmony 패치·D3D 쿼리를 만들지 않는다. Player 빌드에는 두 C# 파일/명령/해제 호출을 넣지 않는다. on에서만 자원을 만든다.
- **8프레임 중 하나**를 측정한다. 8개 순환 슬롯, 표본당 96개 카메라/효과 호출 상한, 전체 쿼리 1,560개를 on에서 만든다. 곡 중 CreateQuery하지 않는다. 동일 카메라의 여러 Render 호출은 그 프레임 안에서 합산한다.
- 3프레임 이상 지난 표본 중 가장 오래된 것 하나만 조회한다. 모든 GetData는 DONOTFLUSH이고, 미준비면 즉시 그 프레임의 조회를 끝낸다. 슬롯이 차면 덮어쓰지 않고 표본을 건너뛴다. stat snapshot과 경합할 때 렌더 스레드 poll도 기다리지 않고 다음 프레임으로 넘긴다. 게임 경로에는 Flush/ReadPixels/픽셀 복사/대기 루프가 없다.
- EOF 코루틴은 on 동안만 돌며, 카메라 AfterEverything 종료 마커는 on 동안 매 렌더에 제출된다. 이 측정 도구 자체의 실제 게임 CPU/GPU 비용은 **미측정**이다. 계측 판 FPS를 최적화 성능으로 쓰지 않는다. 1/8 샘플링이므로 희귀 끊김이나 전체 최악 프레임도 보장하지 않는다.
- 프레임 전체는 **첫 카메라 onPreCull→WaitForEndOfFrame**의 GPU timestamp 경과 시간이다. 카메라는 **onPreRender→CameraEvent.AfterEverything**, 효과는 해당 OnRenderImage prefix→finalizer다. [Unity CameraEvent 문서](https://docs.unity.cn/6000.2/Documentation/ScriptReference/Rendering.CameraEvent.html)의 AfterEverything 경계를 사용한다.
- 카메라 평균의 합과 동일 완료 표본의 전체 평균을 비교하고 signed residual을 출력한다. `effect_outside_camera=0`일 때 효과는 카메라 안쪽 구간이므로 **카메라+효과를 더하지 않는다**. 이 카운터가 증가하면 경계/명령 제출 순서를 실제 게임에서 먼저 확인한다. nested Camera.Render는 카메라 합이 중복될 수 있다. 음수 residual을 0으로 숨기지 않는다.
- timestamp는 **GPU 경과 시간**이다. CPU 제출 간격의 GPU 유휴·다른 context 스케줄링이 섞일 수 있으며 FrameTiming GPU 또는 PresentMon GPU busy와 같은 지표가 아니다. 카메라 시작 전 작업, Present, 출력 지연도 전체 범위에 들어가지 않는다. 인계의 GPU 6.5ms와 수치가 다르다고 바로 오류라 판단하지 않는다.
- state 출력: `active_frames`, `calls`, `mean_frame_ms`(전체 완료 표본 분모, 미호출 프레임 0 포함), `mean_active_ms`, `max_frame_ms`(그 카메라/효과의 해당 프레임 호출 합 최대), `camera-sum`, residual. 필터 최대/카메라 최대는 서로 다른 프레임일 수 있으므로 최대값끼리 더하지 않는다.
- 품질 카운터: ready, completed/requested, invalid, ring_full, overflow, not_ready, errors, effect_outside_camera, unknown_camera/effect. state 시점 마지막 3프레임 이상은 아직 미회수일 수 있다. not_ready는 기다렸다는 뜻이 아니다. invalid/overflow/error/unknown이 있으면 그 수와 표본 손실을 남긴다.
- reset은 패치·쿼리를 재설치하지 않고 통계 epoch만 바꾼다. 이전 표본은 완료 후 버리며 미완료 쿼리를 재사용하지 않는다. state/off는 측정 창을 닫기 전에 실행하고, off는 자동 report하지 않는다. Main.Unload에서도 구독/패치/명령 버퍼/코루틴을 제거하고 렌더 스레드 자원 해제를 예약한다.

## gpucensus 출력과 해석

파일은 `%LOCALAPPDATA%/StutterFix/gpu-census-yyyyMMdd-HHmmss-fff.tsv`에 생성된다. Mods 폴더에는 쓰지 않는다. 카메라별 요약만 Player.log에 남긴다.

반영 조건은 Renderer enabled, forceRenderingOff, activeInHierarchy, 카메라 cullingMask, 프러스텀, ShadowsOnly다. 월드 AABB를 near/far에 자르고 viewport와 겹친 면적을 구한다. 렌더러 수와 material slot 수를 따로 표시한다. 다중 재질의 AABB를 material slot마다 중복 합산한 값도 별도로 둔다.

그룹별로 개수/면적/알파 0 후보/PropertyBlock 수, 재질 instance ID, 셰이더/키워드/renderQueue, 정렬 층/순서, 모든 텍스처 프로퍼티를 기록한다. SpriteRenderer는 실제 sprite.texture와 MaterialPropertyBlock의 텍스처·색 override를 고려한다. per-material block이 있으면 renderer-wide block 대신 사용한다. Unity 텍스처 크기·형식·mip/streaming loaded level 외에, **D3D11 QueryInterface/GetDesc로 실제 Texture2D GPU 리소스 크기·DXGI 형식·mip/array/MSAA 수**를 읽는다. 네이티브 DLL/대응 리소스가 없으면 GPUdesc=unavailable로 표시한다. RT resolve 텍스처의 GetDesc sample 수와 Unity RT MSAA 설정은 다를 수 있어 둘 다 기록한다.

실제 GPU descriptor 읽기는 GetNativeTexturePtr의 렌더 스레드 동기화를 일으킬 수 있다. 고유 텍스처당 한 번만 읽어 캐시하며, 이 캐시는 명령 종료/예외 때 버린다. **census 판과 직후 회복 구간을 타이밍 근거에서 제외한다.** 매 프레임 진단으로 사용하지 않는다.

한계: AABB 덮임은 실제 색/알파 픽셀·depth/stencil·가려짐을 반영하지 않은 상한 추정이다. SortingGroup, LODGroup, sprite sort point, 오클루전, 셰이더 pass/배치 내부 정렬을 재현하지 않는다. 정렬 순서별 재질/주 텍스처 전환은 renderQueue→sortingLayer→sortingOrder→거리로 **추정**하고, 실제 draw-call 수로 부르지 않는다. 알파 0도 일반 셰이더에서는 완전 투명 증명이 아니다. census는 장면 상태의 한 프레임 스냅샷이며 GPU가 현재 어느 프레임을 실행하는지까지 일치시키지 않는다.

## GPU 절약 후보와 채택 조건

새 게임 수치가 없으므로 아래 예상 절약은 모두 **미측정/추정**이다. 인계의 6.5ms는 기준 예산이며, 카메라별 실측값으로 나눠야 한다. 3440×1440은 4,953,600픽셀이다. 보이는 이미지 1,900개만으로 실제 겹침 배수나 절약 ms를 계산할 수 없다.

| 후보 | 같은 그림 근거/추가 확인 | 예상 절약의 수치 근거와 한계 | 위험/현재 결정 |
|---|---|---|---|
| 알파 0 생략 확대 | InvisibleSkip 허용 셰이더와 동일한 의미임을 확인하고 실제 출력 비교 필요 | census의 남은 alpha0 픽셀 합 / 전체 slot 픽셀 합을 비율 r로 삼을 수 있으나, r×카메라 GPU 시간은 균일 fill 비용 가정의 추정일 뿐 | 이미 처리한 forceRenderingOff 대상은 절약에 다시 더하지 않음. 미확인 셰이더는 적용 안 함 |
| 화면 밖 대상 생략 | Unity 프러스텀 컬링은 이미 수행. 추가 생략은 셰이더 변형/트레일/다른 카메라·그림자까지 동일해야 함 | outside 카운트는 이미 걸러진 후보여서 GPU 절약을 그 수에 비례해 계산할 수 없음. 현재 추가 절약 확인 0ms | 임의 forceRenderingOff 추가 안 함 |
| 불투명 물체에 완전히 가려진 대상 | 실제 깊이 쓰기·블렌드·스텐실·필터 입력까지 확인 필요 | AABB만으로 가려진 실제 픽셀을 알 수 없어 절약 미산정 | 투명 스프라이트/오클루전 오판 위험. 후보만 |
| BG RT 재렌더 생략 | 모든 입력·카메라 행렬·색·필터·시간/난수·RT clear/공유 의존성이 같은 프레임임을 증명해야 함 | 생략 가능한 해당 카메라 측정 시간 C가 이론 상한. RT 3장이 ARGB32 전체 화면이라고 가정하면 색 평면 합 59,443,200바이트지만 실제 대역폭/절약 시간 아님 | 이름 Static만으로 불변 아님. 공유 RT에서 clear/합성 손실 가능. 후보만 |
| 같은 재질·텍스처로 배치 증가 | 원래 순서를 유지하는 병합만 검토. 투명 순서 교환은 일반적으로 그림이 달라짐 | census의 전환/run 추정으로 후보 선정. draw/CPU 제출 비용과 GPU 시간 구별 필요, 현재 ms 미산정 | 재질이 같아도 MPB/키워드/SortingGroup/스텐실 차이 가능. 구현 안 함 |
| 강도 0 등 항등 필터 생략 | 기존 FilterIdentity 인증 구조처럼 같은 설정·형식·색 공간의 실제 원본/대체 픽셀 대조 필요. 구형 필터는 별도 증명 | 실제 활성 구형 필터 GPU 시간 F 이하가 상한, 대체 Blit 비용 차감. 기존 고급 필터 0ms로 Legacy 0ms라 하지 않음 | time/난수/상태 갱신 및 원본과 같은 듯한 색 변환 위험. 신규 인증/생략 안 함 |
| mipmap | 화질 변화 후보 | 완전 mip chain은 해당 텍스처 메모리를 대략 1/3 더 요구. 모든 7.85GiB가 대상이라는 가정이면 +2.62GiB로 VRAM 악화 가능. 실제 대상 바이트는 별도 산정 | aliasing/선명도/VRAM 영향. 옵션 후보, 제품 코드 추가 없음 |
| 긴 변 1024→768 | 화질 변화 후보 | 해당 텍스처 면적/기본 레벨 메모리는 약 0.5625배(43.75% 감소). 화면에서 덮는 픽셀 수는 그대로이므로 GPU ms 43.75% 감소를 뜻하지 않음 | 이미 VRAM 한계 맵이지만 흐림 사용자 판단 필요. 기본 변경 없음 |
| 카메라 RT 가로세로 1/2 | 화질 변화 후보 | 순수 픽셀 작업만 지배하는 카메라 비용 C라면 0.75C에서 축소/확대 Blit 비용을 뺀 값이 낙관적 추정. 6.5ms 전체에 바로 적용하면 안 됨 | 텍스트/가는 선/스프라이트 필터 품질·합성 순서 영향. 후보만 |
| FilterHalf | 화질 변화 후보 | 과거 20초 구간 GPU 변화 -0.70ms~+0.10ms. 이 Arche 구간에서는 미측정 | 기존 연구 5종 모두 화질 차이로 미채택. 기본 끔 연구 경로 유지 |

우선순위는 Legacy를 포함한 카메라/효과 분해→270초 census와 240/290초 비교→실측으로 상위 비용 후보 선정이다. 지금 새로운 최적화를 안전하다고 승인할 수 있는 픽셀/게임 근거는 없다.

## Claude 실행 순서

현재 이 작업에서는 설치하지 않았다. 실제 게임 담당자가 설치가 허용된 측정 환경에 PlayerAuto와 독립 gpuprobe.dll을 준비해야 한다. sfnative 교체는 필요 없다. D3D11/3440×1440/165Hz, vSync·프레임 제한·보간·FuseLate 등 설정을 판 사이에 고정한다. filtergpu/filterhalf/filterhalfperf/픽셀 캡처 진단은 함께 켜지 않는다. gpuprobe on은 많은 Harmony 패치를 설치할 수 있으므로 **press 전에** 한 번 실행한다.

다음은 270초 타이밍 전용 판이다. 각 줄은 자동 시험 명령 하나다. `wait`는 모두 180초 이하이며, press 후 대기는 실시간 기준이므로 로그에서 실제 곡 시각/시작 지연을 확인한다. reset은 분석할 구간을 비울 뿐 카메라나 패치를 다시 설치하지 않는다.

```text
auto on
game D:/얼불춤 맵 파일/Arche/[𝐃𝐎 𝐍𝐎𝐓 𝐎𝐏𝐄𝐍 𝐓𝐇𝐈𝐒 𝐀𝐓 𝐇𝐎𝐌𝐄] FULL.adofai
gpuprobe on
wait 5
press
wait 150
wait 115
gpuprobe reset
wait 5
gpuprobe state
gpuprobe off
```

비교 지점도 새 게임 판에서 같은 절차를 쓰되, press 뒤 `wait 150` 다음의 두 번째 wait만 바꾼다. 240초는 `wait 85`→reset→wait 5→state, 290초는 `wait 135`→reset→wait 5→state다. 이 창은 각각 대략 235~240/265~270/285~290초이며 인계의 10초 평균과 같은 길이가 아니다. 10초 창이 필요하면 두 번째 대기를 각각 80/110/130으로 바꾸고 reset 뒤 wait 10으로 통일한다. 각 판 state는 **회수 완료된** 프레임까지만 포함한다. 실제 곡 위치를 로그와 대조한 뒤 비교한다.

census는 타이밍과 별도 판에서 다음처럼 한다. 240/290초는 마지막 wait를 각각 90/140으로 바꾼다. 가능하면 빠른 두 지점도 보관하여 셰이더·재질·Legacy 활성 상태·RT가 함께 바뀌는지 비교한다.

```text
auto on
game D:/얼불춤 맵 파일/Arche/[𝐃𝐎 𝐍𝐎𝐓 𝐎𝐏𝐄𝐍 𝐓𝐇𝐈𝐒 𝐀𝐓 𝐇𝐎𝐌𝐄] FULL.adofai
press
wait 150
wait 120
gpucensus
```

즉석으로 한 판에서만 먼저 확인해야 하면 타이밍 판의 state→off **뒤**에 census를 실행해도 되지만, 이 census는 270초보다 조금 늦다. census 이후 프레임이나 그 판 전체 평균을 성능 수치로 쓰지 않는다. `shot`도 별도 화질 판에서만 쓴다.

수집할 값: ready=1, completed 충분 여부, invalid/overflow/errors/unknown/effect_outside_camera, 카메라 mean/max, 효과 mean/max, 카메라 합/전체/residual, 활성 OnRenderImage와 RT ID, GPU 실제 크기/mip/형식, census 후보 수/overdraw/alpha0/run 추정. 계측 자체 오버헤드는 캡처 없는 별도 off/on/off 판으로 확인하고, 현재 구현의 빌드/WARP 통과를 실제 게임 오버헤드 통과로 바꾸어 쓰지 않는다. 1/8 고정 위상 표본은 주기성 작업을 놓칠 수 있으므로 그 한계도 남긴다.

## 검증 및 남은 범위

- 지정한 PlayerAuto/Player/DEV 오프라인 빌드: 각각 오류 0개(최종 빌드 경고 35/4/35개). 새 GPU 소스의 컴파일 경고는 없다. 전체 로그 파일 참조. 일반 Player에서 GpuProbe/GpuCensus 형식 각각 0개, PlayerAuto에서 GpuProbe 포함도 IL로 검사했다(`player-il.txt`, `playerauto-il.txt`).
- native build와 WARP check exit 0. 첫 정상 집계는 frame/camera/effect 각각 40개, ring_full/invalid/errors 0. 중첩 timestamp 관계, 반복 카메라의 프레임 내 합산, 호출 없는 프레임 분모, 미닫힘/overflow 표본 폐기, epoch reset의 미회수 표본 격리, 미완료 슬롯 재사용 방지, 종료/재초기화/null 초기화, Texture2D 실제 descriptor 읽기를 검사했다. WARP는 Unity 이벤트 순서/실제 게임 부하·화질·오버헤드 검증을 대신하지 않는다.
- 기존 `.codex/config.toml`, ExitFix/FuseProbe/MoveApply/PerfOverlay/SettingsWindow/FuseLate는 작업 전 사본과 SHA256 동일. 공유 3개 파일은 이번 삽입 줄만 빼면 작업 전 내용과 동일(줄바꿈 정규화 후 비교). `preservation.txt` 참조.
- 게임 내 명령 실행·실제 카메라 경계·census 실제 출력·그래픽 상태/출력 동일성·Arche GPU 개선량은 **미검증**. 렌더러/필터를 끄거나 RT·텍스처·정렬을 바꾸는 제품 동작은 추가하지 않았다.
