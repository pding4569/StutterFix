# 게임 코드 밖 FPS 후보 조사

2026-10-15. 조사 및 복원 완료. 새 플레이어용 최적화 채택은 **0개**다. 정지 배경 비용이 구현 기준에 못 미쳤고, 작은 RT 후보는 화면이 달라지거나 실제로 ARGB32로 대체됐다. 6개 후보군을 조사했다. 카메라 성능 탐색 16판은 제한을 기록하고 채택 근거에서 제외했으며, 교정 ABBA 16판(HELLO 12/Windflower 4)을 별도로 남겼다. 픽셀 대조는 6시점 중 반복 대조군 차이 0인 5시점만 유효하게 사용했다. HELLO 필터 조사 판은 완주했다.

성능 표본은 약 첫 20초(HELLO)/30초(Windflower)의 구간 시험이며 전체 곡 FPS 개선 수치가 아니다. Arche/h90000 추가 성능 판은 하지 않았다. 화면을 그대로 유지하는 채택 후보가 없으므로 확대 A/B를 진행하지 않았다. 모든 가능한 그래픽 기법을 불가능하다고 결론 내리지 않는다.

## 범위와 판정

FrameGen 및 셰이더 소스는 수정하지 않는다. 커밋·푸시·릴리스 없음. 자동 시험 전용 `FreeFpsProbe`는 명령이 실행될 때만 동작하며 매 프레임 reflection/Stopwatch/렌더 대조를 하지 않는다. 상태는 명시적인 restore 및 Main.Unload에서 복구한다. 성능 판과 ReadPixels 대조 판은 분리한다.

공통 화면 환경: D3D11, FullScreenWindow, 2560×1440 120Hz, vSync 0, targetFrameRate 10000(fpscap 0). FrameGenOutside=0을 각 묶음 전체에 적용. 기존 설치 DLL SHA-256 CB634D735758EBE0E38A1E45320F0DD7D2919C7F7408E6BA0520814C3AD46858와 Settings.xml을 free-fps-evidence에 보존했다.

## 주의: 탐색 판과 유효 측정 구분

최초 enabled=false 탐색에서 scrCamera가 camobj.enabled를 다시 쓰는 것을 IL로 확인했다(camera-fields-il.txt). 본 카메라 enabled 탐색 결과는 비중 계산에서 제외한다. 최초 탐색의 A/B wait 단계는 play 및 off 명령의 길이 차이로 관측 시간이 달라졌다. 채택 근거로 사용하지 않는다. 후속 측정은 cullingMask=0 및 공통 명령 뒤 같은 길이 wait로 교정한다. 카메라 제거는 화면이 다르며 비용 분리만을 위한 것이다. GPU 평균은 게임 로그의 반올림된 값이므로 0.1ms 차이로 확정적인 인과를 주장하지 않는다.

## 첫 세션 현재 값

| 카메라 | HDR | MSAA | 동적 해상도 | 오클루전 | forceIntoRT | targetTexture |
|---|---|---|---|---|---|---|
| BGStaticCam | false | false | false | false | false | null |
| BGMovingCam | false | false | false | false | false | null |
| Camera | false | false | false | false | false | null |
| Thumbnail Maker | true | false | false | true | false | RT / enabled=false |

softParticles=false, particleRaycastBudget=256, anisotropicFiltering=Disable, masterTextureLimit=0. 사용하지 않는 썸네일 카메라는 기존 ThumbCam으로 이미 꺼져 있다. HDR/MSAA/occlusion/소프트 파티클/이방성 필터를 끄는 후보는 현재 경로에서 이미 적용된 값이라 새로운 이득이 없다. 파티클 레이캐스트 예산을 줄이면 충돌 결과가 달라질 수 있다. 텍스처 mip 제한은 화질을 낮추므로 바이트 동일 조건의 최적화가 아니다. forceIntoRT는 직접 그리기를 추가 중간 타깃 경로로 바꾸므로 비용 제거 플래그로 취급하지 않는다.

## 렌더 구조

scrCamera.SetupRTCam/Update의 IL은 camRT 생성·targetTexture 연결·quad 재질 연결 경로를 확인했다. 그러나 실제 측정에서 세 주요 카메라 targetTexture=null이며 OverlayCam은 활성 카메라 목록에 없다. 첫 세션에서 camRT 깊이만 바꾸는 것은 직접 화면 경로에 영향을 주지 않는다. 재시작 뒤 두 번째 세션에서는 세 카메라 targetTexture가 RT이고 OverlayCam이 활성화돼 있다. 최종 cullingMask ABBA는 이 두 번째 세션에서 진행하며 세션 간 수치는 섞지 않는다. 검증은 세 카메라를 동일한 크기의 임시 타깃에 순서대로 그려 같은 프레임에서 바이트 비교한다. 반복 ARGB32/24 대조군 자체에 차이가 있으면 그 순간은 포맷 후보를 판정하지 않는다. 이 대조는 활성 직접 화면 전체의 동일성 증명도, 실제 camRT 경로에서의 성능 시험도 아니다.

## 배치와 필터

MatReuse는 메모리 누수를 정리하는 코드이며 타일마다 다른 재질을 전부 공유하는 최적화가 아니다. FloorRenderer의 재질은 색·스타일 변경으로 서로 달라질 수 있으므로 동일 셰이더/재질 이름만으로 합치지 않는다. Renderer.material 대신 sharedMaterials로 재질 수를 조사한다. MaterialPropertyBlock은 차이가 있는 오브젝트 속성을 전달하는 목적이므로 존재 자체를 불필요한 배치 파괴로 판정하지 않는다. 기존 BlendModes 최적화는 반복 구현하지 않는다.

필터 합치기는 설계만 허용한다. 밝기·대비·채도는 affine RGB 연산으로 조건부 합성이 가능하지만 원래 각 패스의 ARGB32 양자화·클램프·sRGB 변환·alpha 처리가 중간마다 일어난다. 한 번의 연산으로 합치면 바이트 동일을 보장할 수 없다. 노이즈는 좌표·시간·난수·연산 순서와 원래 pass별 반올림을 보존해야 한다. 따라서 후보를 정한 뒤 원래 연속 패스와 한 패스 출력을 같은 입력과 프레임에서 전체 RGBA 바이트로 비교해야 한다. 임의의 3개 필터가 아니라, 합칠 수 있는 연속된 색 필터 3개 이상의 실제 비율을 센다.

## VRS / 렌더 스레드 / 업스케일러

- boot.config는 force-gfx-jobs=legacy, gfx-threading-mode=6. 실제 D3D11 경로. 기존 AGENTS의 D3D12/native 및 scratch 옵션 실패 실험은 반복하지 않는다. 멀티스레드 렌더링은 이미 쓰는 엔진 기능이며 임의로 Unity 렌더 API를 C# 작업 스레드에서 호출하는 방식으로 대체할 수 없다.
- VRS는 최신 Unity에서 API가 존재하는 것과 이 게임의 D3D11 경로에 동일 화면으로 연결할 수 있다는 것이 다르다. shading rate를 낮추면 픽셀당 원래 shading 결과를 일반적으로 보존하지 못하며, 이번 셰이더 수정 금지·바이트 동일 조건에서는 채택할 근거가 없다. 엔진 전체에서 절대 불가능하다고 주장하지 않는다.
- UnityEngine.NVIDIAModule.dll은 실제로 설치되어 있으며 DLSSContext API도 있다(nvidia-il.txt). 그러므로 Mono이므로 DLSS 자체가 불가능하다고 판단하지 않는다. 다만 HDRP/Universal 패키지 DLL은 찾지 못했고 이 게임의 기본 2D 렌더 경로에는 DLSS를 바로 켜는 설정이 없다. 적절한 깊이·모션 벡터·저해상도 입력과 native 실행 통합이 필요하다. 업스케일 결과는 원본과 바이트 동일하지 않으며 이번 조건에서 제외한다. 기존 Fsr.cs에는 FSR1 경로가 이미 있다.
- [Unity 6000.3 렌더 파이프라인 비교](https://docs.unity.com/en-us/engine/6000.3/manual/render-pipelines/choose-a-render-pipeline/feature-comparison), [Unity 동적 해상도 지원 플랫폼](https://docs.unity.com/en-us/engine/6000.5/manual/cameras/resolution-scale/dynamic-resolution/introduction): Windows 동적 해상도는 D3D12 지원 조건이므로 D3D11 allowDynamicResolution만 켜서는 해결되지 않는다.

## 교정된 카메라 ABBA (HELLO, 약 첫 20초)

A=원래 cullingMask, B=해당 카메라 cullingMask=0. 각 후보 ABBA 4판(A 2/B 2). 숫자는 두 판 평균이며 끊김 개수는 두 판 합계다. 시작 연출 제외 지표는 게임의 [곡]/[임무30]을 사용한다. GPU 비중은 (A GPU−B GPU)/A GPU로 계산한다. mask=0도 카메라의 clear와 OnRenderImage 필터는 남으므로 전체 카메라 제거 비용이 아니라 내용 그리기의 절감 비중이다. B 중간 상태 로그에서 mask=0 유지 확인. 30ms 초과 판은 임의로 버리지 않았다.

| 후보 | FPS A→B | GPU ms A→B | 내용 그리기 GPU 비중 | >30ms A→B | 판 수 | 화면 동일 | 판단 |
|---|---:|---:|---:|---:|---:|---|---|
| BGStaticCam | 272→276.5 | 1.95→1.95 | 0% | 1→0 | 4 | 아니오 | 캐싱 구현 기준인 10% 절감 근거 없음 |
| BGMovingCam | 269.5→276.5 | 2.0→1.9 | 5% | 2→0 | 4 | 아니오 | 움직이는 화면 삭제는 최적화로 채택 불가 |
| Camera | 275→353.5 | 2.0→0.75 | 62.5% | 0→0 | 4 | 아니오 | 주 비용은 실제 게임 화면. 내용 삭제 채택 불가 |
| Windflower BGStaticCam | 294→296 | 1.7→1.7 | 0% | 0→0 | 4 | 아니오 | 두 번째 맵에서도 캐싱 기준 미달 |

## 후보별 판단표

| 후보 | 시도 / 판 수 | 전→후 수치 | 화면 동일 여부 | 채택/기각 이유 |
|---|---|---|---|---|
| 정지 배경 캐싱 | HELLO/Windflower 카메라 ABBA 각 4판 | 위 표 | 비용 분리 B는 다름 | GPU 평균 차이 0, 10% 구현 기준 미달로 구현하지 않음 |
| RT depth 24→0/16 | 5개 유효 시점의 같은 프레임 24 반복 대조 후 비교 | 대조 0; 각각 1,692,230바이트 차이 | 다름 | 스텐실·깊이 제거 후보 기각. FPS A/B로 확대하지 않음 |
| RT RGB565 | 5개 유효 시점의 같은 프레임 비교 | 337,075바이트 차이 | 다름 | 색 정밀도 감소 후보 기각 |
| RT ARGB4444 | 5개 유효 시점 | 차이 0; 실제 ARGB32로 대체 | 유효 표본에서는 같음 | 더 가벼운 버퍼가 아님. 기각 |
| HDR/MSAA/occlusion/softParticles/aniso 끄기 | 메뉴 및 맵 현재 값 확인 | 이미 꺼짐→동일값 | 같음(변경 없음) | 변경 없는 후보, 추가 ABBA의 실익 없음 |
| 동적 해상도 | 현재 D3D11, flag=false | 성능 표본 없음 | 미검증 | API 조건 부적합; 해상도 감소는 바이트 동일 조건 위반 |
| 미사용 오버레이 카메라 제거 | 카메라 목록 및 targetTexture 확인 | Thumbnail Maker 이미 꺼짐; 활성 OverlayCam은 RT 출력 | 사용하지 않는 카메라 변경 없음 | 출력용 OverlayCam은 제거하면 화면 달라짐 |
| 파티클 raycast 256→축소 / mip 0→1 | 기능 의미·현재 설정 확인 | 성능 표본 없음 | 일반적으로 달라짐 | 충돌 결과·텍스처 화질 변경 조건이라 채택하지 않음 |
| 재질 공유/MPB 삭제 | Renderer.sharedMaterials/HasPropertyBlock 조사 | 15,540 enabled renderer / 15,239 material ID / 804 MPB | 미검증 | 개별 색·스타일 상태 공유로 원래 화면 보장 못함. 임의 통합하지 않음 |
| 단순 색 필터 합치기 | 2초 filterstate 조사 | 완주 전 92표본: 3개 동시 4(4.3%), 연속 3개 0 | 구현·대조 없음 | 30% 빈도 기준 미달, 연속 조합 없음. 일반적인 동일성 설계 조건만 기록, 구현 금지 준수 |
| VRS / DLSS / 멀티스레드 렌더 | 엔진 설정·모듈·IL·공식 문서 확인 | 성능 판 없음 | 미검증 | 위 항목의 적용 조건과 제약 기록; 기존 실패 옵션 반복 없음 |


## 필터 최종 표본

HELLO는 [임무완주] OnLandOnPortal로 완주를 확인했다. 포털 이전 2초 간격 92표본(마지막 184.5초)만 사용하며 포털 이후 계속 증가한 songposition 표본은 제외한다. Color_Noise + Color_BrightContrastSaturation + Colors_Brightness가 함께 켜진 표본은 58/60/62/64초 4개(4.35%). 그러나 TV_WideScreenVertical/TV_Chromatical2 등의 왜곡 패스가 사이에 있어 **연속된 단순 색 필터 3개**는 0/92다. 초기 중간 집계의 0회는 Colors_Brightness 이름 분류 누락과 포털 이후 표본 포함이 있었으므로 폐기했다. 원시 표본은 filter-final.json에 저장했다. 이 값은 모든 프레임을 센 정확한 프레임 비율이 아니라 2초 스냅샷 비율이다.


## 실제 형식과 추가 픽셀 근거

요청한 24비트 깊이는 이 PC에서 실제 depth=32, depthStencilFormat=D32_SFloat_S8_UInt로 할당됐다. depth=16은 D16_UNorm, depth=0은 None이었다. RGB565는 실제 B5G6R5_UNormPack16. **ARGB4444는 실제 format=ARGB32 / graphicsFormat=R8G8B8A8_UNorm**으로 대체됐으므로 바이트가 같아도 저장량·대역폭 절약이 없다.

HELLO 두 유효 editor 시점에서 깊이 0/16은 각각 1,692,230 및 10,923,487바이트가 달랐다. 추가 실행의 editor 시점도 1,692,264바이트가 달랐다. Windflower 첫 시점과 HELLO 약 14초 시점에서는 깊이 0/16 차이 0이었지만 맵/순간에 따라 달라지므로 일반 적용하지 않는다. RGB565는 유효 5시점 모두 차이가 났다(236,200 / 337,075 / 2,672,958 / 338,300 / 8,381,020바이트). 마지막 HELLO 약 30초 시점은 24 반복 대조군 자체에 1,007바이트 차이가 있어 후보 대조 전체를 폐기했다. 모든 입력·마스크를 검증했다고 주장하지 않는다.

## 렌더 카운터와 배치

처음 Recorder.Get의 valid=True/sampleBlockCount=0은 드로 콜 0이라는 뜻이 아니었다. 별도 진단 실행에서 Unity.Profiling.ProfilerRecorder를 2초 동안 켜고 값을 읽은 뒤 Dispose했다. 정식 빌드에는 들어가지 않는다.

| 상태 | 주요 렌더 카메라 | Draw calls | SetPass | Batches | 표본 |
|---|---:|---:|---:|---:|---|
| HELLO editor | RT 경로의 scene 3 + Overlay 1 | 232 | 126 | 232 | 2초 수집 뒤 마지막 한 프레임 |
| HELLO 재생 약 14초 | scene 3 + Overlay 1 | 423 | 273 | 409 | 2초 수집 뒤 마지막 한 프레임 |

이 숫자는 평균/곡 전체 최대치가 아니다. HELLO snapshot은 enabled Renderer 15,540개, 고유 material ID 15,239개, MPB 사용 804개. Windflower snapshot은 각각 7,640 / 7,168 / 5,594개. 많은 material ID는 바꿀 여지가 있을 수 있으나 현재 게임은 타일별 색·스타일을 바꾸므로 공유로 원래 결과를 보장하지 못한다. 실제로 동시에 그려지는 renderer 수·draw calls와 전체 material 수를 혼동하지 않는다. 안전한 공유 대체나 불필요한 호출이라는 근거를 확보하지 못해 새 공유 패치를 넣지 않았다.

## 빌드와 종료·복원

- PlayerAuto: 오류 0, 경고 30. Player: 오류 0, 경고 25. 빌드 로그는 free-fps-evidence/build-auto.log 및 build-player.log. 경고는 참조 버전 충돌·deprecated API·미사용 필드 등이다.
- FreeFpsProbe는 #if DEV || AUTOTEST 안에만 있다. 새 Harmony/정적 이벤트는 설치하지 않으며, 카메라 mask는 restore/Main.Unload에서 복원하고 네이티브 카운터도 Dispose한다. 일시적인 targetTexture/RenderTexture.active 변경은 finally에서 되돌린다. 매 프레임 managed 진단 코드를 추가하지 않았다.
- 최종 게임 프로세스 없음 확인. 모든 게임 종료는 자동 시험 quit로 정상 종료; taskkill 사용 없음. 마지막 sfmeasure 종료 대기 중 발생할 수 있는 '묶음 도중 게임이 꺼짐'은 실제 프로세스 종료를 확인해 정상 종료로 처리했다.
- dotnet build -c Debug -p:Edition=Player 결과 bin/Player/StutterFix.dll을 Mods에 복원했다. 설치 DLL과 빌드 DLL 및 원본의 SHA-256은 모두 CB634D735758EBE0E38A1E45320F0DD7D2919C7F7408E6BA0520814C3AD46858.
- Settings.xml 원본 복원 확인: 원본/복원본 SHA-256 모두 A73F5C606FB838641BA3B6268BD7C88A3DC1BD9E01B2A399559CC836F6DDCE4A.
- 커밋·푸시·릴리스 없음. FrameGen*·framegen*.h 및 셰이더 소스 수정 없음. 기존 .codex/config.toml 등 사용자의 변경은 건드리지 않았다.

원시 로그와 실행 스크립트, camera-final.json(각 판 화면 조건 포함), filter-final.json, IL 결과, 원본 DLL·설정은 docs/handoff/free-fps-evidence/에 있다. 이번 결과는 새 FPS 개선의 성공 보고가 아니라, 화면을 유지하며 적용할 수 있는 후보가 없음을 확인한 조사 결과다.
