# FrameGenLab — 게임 밖 프레임 생성 시험

11장은 9장 **1a와 0단계**, 12장은 수직동기 0과 **1b 상태·UI 조사**다. 17장에서 사용자 승인에 따라 일반 빌드에도 **프레임 늘리기 (실험)**를 기본 꺼짐으로 추가했다.
게임 그림·게임 DLL·다른 모드 코드는 포함하지 않는다. 타일 길·움직이는 장식·행성 둘을 직접 만든 장면이다.

12장 추가 실험은 배포하지 않는다. 기존 테스터 ZIP을 보존하며, `pack.ps1`은 새 GPU 계측 바이너리를 거부한다.

```powershell
.\build.ps1
py -3 .\run_unsynced.py
# 게임을 정상 종료한 상태에서만; 설치 DLL·설정을 디스크에 백업하고 끝에 복원
.\build_measure.ps1 -Probe Capture
py -3 .\capture_game.py --map '<맵.adofai>' --seconds 15 --ui --out results\capture-new
```

독립 시험은 3440×1440, Present 동기 0, 원본 400/200/100 × 끔/2/3/4, 조건마다 15초다.
출력 FPS는 성공한 Present/s이며 물리적 화면 표시 수는 확인 안 됨. GPU 값은 D3D11 timestamp/disjoint다.
`--gpu-timing 0`으로 계측을 끈 대조를 할 수 있다. CSV·로그·게임 캡처는 `results/`에만 둔다.
게임 캡처는 WaitForEndOfFrame 숫자 버퍼를 종료 때 CSV로 쓰고, 재생 진입 뒤 8초에 camRT/화면을 한 번 읽는다.
`--ui`는 ScreenSpaceOverlay 루트만 임시 UI 카메라로 옮겨 투명 RT에 한 번 그린 뒤 상태를 복원한다.
Canvas·layer 복원은 확인했지만 RectTransform의 미세한 반올림 차이가 남았다. 이 방법을 그대로 정식 기능에 쓰지 않는다.
WorldSpace 표시 요소·원래 화면과의 정확한 합성·매 프레임 비용은 확인 안 됨. 캡처/PNG 저장 프레임은 성능 표본으로 쓰지 않는다.

## 테스터가 실행할 것

ZIP을 풀고 `2x.cmd`, `3x.cmd`, `4x.cmd` 중 하나를 실행한다. 각각 60초 동안 출력하고 ESC로 끝낸다.
기본 진짜 프레임 속도는 **현재 모니터 주사율 / 배수**다. 예: 144Hz에서는 72→144, 48→144, 36→144 FPS.
비교용 `60fps-2x.cmd`는 진짜 60 FPS에서 2배를 요청한다. 144Hz 고정 주사율에서는 120 FPS를 일정한 주사율 칸마다 낼 수 없어 1칸/2칸 간격이 섞인다.
테스터는 카메라 이동·회전·확대, 행성 회전, 가장자리 검은 틈, 화면이 가려진 뒤의 동작을 확인하고 `results/`의 CSV와 콘솔 결과를 전달한다.
장식은 진짜 프레임에서만 움직이고 행성은 출력 시각으로 다시 그린다. 소리·판정·입력은 이 시험 프로그램에 없다.

직접 비교:

```powershell
.\FrameGenLab.exe --mode none --base 60 --multiplier 2 --seconds 30 --csv results/none.csv
.\FrameGenLab.exe --mode reproj --base 60 --multiplier 2 --seconds 30 --csv results/reproj.csv
.\FrameGenLab.exe --mode hybrid --base 60 --multiplier 2 --seconds 30 --csv results/hybrid.csv
```

`none`은 진짜 프레임만, `reproj`는 행성도 포함한 지난 그림을 옮기기, `hybrid`는 행성 없는 그림을 옮기고 지금 행성을 그리기다.
UI를 흉내 낸 윗줄은 합성 뒤에 고정해서 그린다. **실제 Unity UI를 별도로 얻는 문제를 해결한 것은 아니다.**

## 빌드와 표시 간격 측정

MSVC x64, Windows SDK가 필요하다. `build.ps1 -MsvcRoot ... -SdkInclude ... -SdkLib ...`로 다른 설치 경로를 지정할 수 있다.
기본 SDK 경로는 이 개발 PC의 `C:\SFBundle\winsdk`다. 프로그램은 정적 CRT로 빌드하므로 테스터에게 컴파일러가 필요 없다.

```powershell
.\build.ps1
py -3 .\run_suite.py --presentmon C:\SFBundle\PresentMon.exe --seconds 20 --base 0
# PresentMon 관리자 권한 필요. 한글 출력 경로가 지원되지 않는 빌드는 --pm-out <ASCII 경로>.
# DXGI 표시 통계로 별도 검증; PresentMon 성공으로 취급하지 않는다.
py -3 .\run_suite.py --native-only --seconds 20 --base 0
# 60 FPS에서 주사율 한도와 2배의 간격 양자화 확인
py -3 .\run_suite.py --native-only --seconds 20 --base 60
# 관리자 PowerShell: GPU + DWM/Win32k까지 WPR로 기록한 뒤 PresentMon 오프라인 분석
.\capture_wpr.ps1 -Seconds 8 -TraceDir C:\Users\Public\StutterFixTrace\framegenlab\new-trace
```

FlipDiscard, 백 버퍼 2개, 프레임 대기 객체, 최대 대기 1개, `Present(1,0)`를 쓴다.
고해상도 대기 타이머 + 마지막 0.5ms 이내 대기를 쓰며, 놓친 슬롯은 건너뛰고 오래된 그림을 몰아서 내지 않는다.
출력마다 RTV를 다시 묶고, 원본 텍스처를 다시 그릴 때 SRV를 해제해 동시에 읽고 쓰지 않는다.
이 설정은 시험 프로그램의 것이다. 유니티의 기존 큐·Present(0)·여러 카메라에 넣었을 때는 다시 검증해야 한다.
WPR의 GPU 프로필만으로는 표시 추적에 필요한 DWM/Win32k 이벤트가 빠지므로 DesktopComposition도 함께 기록한다.
이미 다른 기록이 있으면 시작 실패로 멈추며 기존 기록을 취소하지 않는다. 자기 기록의 종료가 실패할 때만 그 기록을 취소한다.
이 개발 PC에서는 두 프로필을 함께 기록해도 PresentMon 표시·GPU CSV가 나오지 않았다. DXGI 표시 간격 검증과 PresentMon 검증은 구분한다.

매 실행 전에 GPU 셰이더의 이동·회전·확대를 독립 CPU 계산과 512점 대조한다(허용 오차 1/255).
이는 좌표 계산 검증이며 색 정확도·행성의 실제 게임 상태 복원·입력 지연 검증을 대신하지 않는다.

CSV의 `cpu_submit_ms`는 GPU 시간이 아니다. `real`은 합성 장면의 원본 갱신이며 실제 게임의 로직 프레임이 아니다.
DXGI 통계에서는 실제 표시된 PresentCount/주사율 칸/QPC를 기록한다. 연속 PresentCount 쌍만 간격에 넣고 관찰에서 빠진 번호 수를 따로 적는다.
빠진 번호가 모두 드롭된 프레임이라고 가정하지 않는다. 시작·끝 1초는 제외한다. DXGI의 FPS는 관찰한 연속 표시 간격 평균의 역수이며 완전한 표시 횟수 집계가 아니다.
`output_start_to_display_ms`는 시험 프로그램의 출력 시작 QPC→DXGI 표시 시각이며 게임 입력 지연이 아니다.

## 0단계: 무거운 맵

```powershell
.\build_measure.ps1
py -3 .\measure_maps.py --map '<맵.adofai>' --label hello --seconds 45 --out results/hello
py -3 .\measure_maps.py --map '<맵.adofai>' --label hello-cpu --seconds 45 --affinity 0x6 --cpu-duty 0.5 --cpu-workers 2 --cpu-thread-priority 2 --out results/hello-cpu
py -3 .\measure_maps.py --map '<맵.adofai>' --label hello-reference --seconds 45 --plain --out results/hello-reference
```

게임은 닫힌 상태에서 시작한다. 측정 DLL과 설정은 실행 중에만 바꾸고 끝나면 되돌린다.
`--plain`은 같은 측정 빌드에 프레임 경계 2곳만 두는 비용 비교 기준이다.
`--affinity`로 게임 코어를 제한하고, `--cpu-duty`·`--cpu-workers`로 해당 코어에 같은 프로세스 우선순위의 시험 부하를 준다.
`--cpu-thread-priority`는 부하 스레드의 상대 우선순위(0/1/2)다. 게임 우선순위는 바꾸지 않는다. 부하 프로세스는 시험 시간+30초에 스스로 끝난다.
원본 DLL·설정의 복구용 사본도 해당 결과 폴더에 먼저 저장한다. 일반 종료·예외에서는 자동 복원하고 강제 중단 뒤에는 이 사본으로 복원한다.
이는 느린 CPU를 정확하게 흉내 내는 벤치마크가 아니라 로직/그리기 비율과 출력이 낮아진 조건을 살피는 시험이다.

측정 빌드는 원래 소스를 바꾸지 않고 `out/measure/`에 Main·AutoTest 사본을 만들어 계측을 연결한다.
`MeasureBuild=false`를 유지하며 자주 호출되는 설정 함수·스크립트를 Harmony로 감싸지 않는다.
곡 시작 5초를 제외하고, 단계마다 타임스탬프와 숫자 배열만 갱신한다. 종료할 때만 로그를 만든다.
곡 중 큰 프레임도 평균에 포함한다. 프레임 전체와 단계 합계가 1% 이상 어긋나면 결과를 거절한다.
해제는 자신이 추가한 PlayerLoop Marker만 제거한다. 이 DLL은 테스터 ZIP이나 정식 배포에 포함하지 않는다.

스레드·GPU는 동시에 일하므로 CPU 단계와 GPU 시간을 더해 100%로 만들지 않는다.
스크립트 단계에는 LateUpdate의 그리기 준비도 섞일 수 있다. 전체를 건너뛸 수 있는 순수 게임 로직으로 읽지 않는다.

원본 로그는 `results/`에만 남는다(커밋 제외). 공개할 요약은 사적인 맵 이름·경로를 제거하고 `docs/framegen-research.md`에 적는다.

API 근거: [DXGI Flip model](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/for-best-performance--use-dxgi-flip-model),
[프레임 대기 객체](https://learn.microsoft.com/en-us/windows/uwp/gaming/reduce-latency-with-dxgi-1-3-swap-chains),
[PresentMon 지표 정의](https://github.com/GameTechDev/PresentMon/blob/main/README-ConsoleApplication.md).

## 13장 게임 안 프레임 생성 (연구용, 배포 금지)

```powershell
.\build_ingame_native.ps1
.\build_measure.ps1 -Probe InGame
py -3 .\measure_ingame.py --map '<맵.adofai>' --label hello-2x --mode 2 --flip --out results/ingame-hello-2x
```

`--mode 0/2/4`, 기본 실험 설정 `FrameGenExperiment=0`. 연구 DLL의 UMM 설정창 단추나 F8로 끔/2배/4배를 바꾼다.
`--flip`은 이 PC에서 확인한 D3D11 RT의 세로 방향을 맞춘다. 연구 DLL의 설정 기본값은 true다.
입력·판정·카메라·행성 로직은 매 출력 프레임 실행한다. 무거운 장면 카메라는 2/4프레임마다 렌더링하며,
그 사이 저장한 행성 없는 화면을 현재 카메라로 재투영하고 현재 행성 렌더링과 진짜 프레임 UI 픽셀을 합친다.
따라서 2배/4배는 장면 렌더링 횟수에 대한 출력 비율이다. 끔 대비 전체 출력률이 2/4배 된다는 뜻은 아니다.
메인 스레드가 멈추면 생성 출력도 멈춘다.

네이티브 연구 코드는 별도 sfnative 사본에만 연결한다. 정식 프로젝트의 Compile 목록·원래 native DLL을 바꾸지 않는다.
러너는 설치된 관리 DLL·sfnative.dll·Settings.xml의 원본을 디스크에 먼저 저장하고, 정상 종료 후 세 파일을 정확히 복원한다.
게임이 켜져 있거나 이전 진단 폴더가 있으면 실행을 거부한다. 강제 중단 뒤에는 게임을 정상 종료하고 결과 폴더의 `.original` 세 파일로 복원한다.
`build_measure.ps1 -Install -Probe InGame`은 이 복구 절차를 생략하므로 거부한다.

표본은 마지막 음악 재생 구간의 곡 5~45초다. 메뉴/카운트다운의 이전 곡 시계를 버리고 연속 프레임 번호를 확인한다.
출력은 성공한 Present(0) 호출 수이며 물리적 표시 FPS는 확인 안 됨.
GPU 시간은 끼운 프레임의 행성 검정/흰 HDR 바탕 두 패스 + 네이티브 합성에 D3D11 timestamp/disjoint를 둬 잰다.
곡 중 파일 쓰기·GPU 결과 대기·이미지 읽기는 하지 않는다. `--capture`는 시각 확인용이며 성능 표에서 제외한다.
이 빌드와 코드를 정식 DLL·테스터 ZIP에 넣지 않는다.

## 17장: 공 포함 프레임 늘리기 (실험)

`build_outside.ps1 -Game` 다음 `build_measure.ps1 -Probe Outside`로 연구 빌드를 만든다.
연구 설정 창의 그래픽 페이지에 **프레임 늘리기 (실험)** 스위치가 있다. 기본 꺼짐이며,
켜면 정수 2~8배를 선택한다. F8을 가로채지 않는다. 일반 빌드는 같은 `FrameGen.cs`와 `native/sfnative/framegen.*` 구현을 쓰되 연구 계측·캡처를 켜지 않는다. GPU 시간 쿼리와 큰 기록 배열도 만들지 않는다.
꺼져 있을 때는 런타임 초기화·카메라 콜백·Present 연결·작업 스레드를 만들지 않는다. 실제 3440 게임 검증은 화면 조건이 돌아온 뒤에 이어가며, 릴리스·기존 테스터 ZIP은 그대로 보류한다.
배율은 성공한 출력 제출 수 / 현재 진짜 프레임 수의 목표다. 물리적 표시 FPS나 입력 처리 속도를 뜻하지 않는다.
생성 주기는 `원본 주기 / (N-1)`이므로 다른 정수에도 같은 식을 쓸 수 있으나 GPU·CPU·Present 비용에 한계가 있다.

```powershell
py -3 .\repeat_outside.py --map '<맵.adofai>' --label ratios --out results/ratios --modes '0,2,4,3,5,8,0'
py -3 .\summarize_repeat.py results/ratios --output results/ratios/pooled.json
```

`--width 3440 --height 1440`을 지정하면 실제 게임 창 크기가 다를 때 비교를 중단한다.
해상도가 맞지 않는 판을 3440 결과로 해석하지 않는다. `measure_outside.py --settings <JSON>`은
기존 저사양·FSR 등의 설정을 이번 판에만 바꾸며 종료 후 설치 세 파일을 원본 바이트로 복원한다.
`--switch-smoke --mode 0`은 배율 전환과 잘못된 값(1·9)이 생성 꺼짐으로 처리되는지 보는 별도 시험이다.

`normal_smoke.py`는 `FRAMEGEN_RESEARCH` 없는 Player+AutoTest 빌드로 기본 꺼짐·2~8 전환·해제·반만 그리기 중단/재개·완주를 확인한다.
`normal_repeat.py`는 꺼짐→2배→4배→꺼짐의 각 35초 구간을 비교한다. 원본은 명시적 상태 명령 사이의
Unity 프레임 번호/Stopwatch 차이이며 정확한 장면 카메라 완료 수가 아니다. 활성 출력은 native의 성공한
Present 누적 수, 꺼짐 출력은 Unity 수에서 추정한다. 꺼짐을 재기 위해 관찰 연결을 설치하지 않는다.
같은 바이너리·실제 해상도·주사율인지 확인하고 달라지면 합산을 중단한다. GPU 시간·최악 간격은 연구 실행에서만 잰다.
`compat_outside.py`는 연구 계측을 켜고 FSR·반만 그리기·배율 전환·설정창을 각각 새 실행으로 확인한다.
모든 실행은 설치 DLL·네이티브 DLL·설정을 바이트로 백업하고 정상 종료 후 복원한다.

## 18장: 출력 FPS·흔들림 비교

일반 모니터 항목 탭의 기본값은 자동이다. 프레임 늘리기 활성 시 출력 FPS, 꺼짐에는 원본 FPS를 보이며
원본/출력/둘 다를 직접 고른 선택은 유지한다. 출력은 실제 성공 Present 누적 수로 계산하며,
프레임 시간·끊김·곡 통계는 계속 원본 기준이다. `normal_smoke.py --monitor-smoke --mode 4 --no-shot`은
새 기본 자동, 세 수동 선택, 자동 복귀·생성 해제 뒤 원본 표시를 확인하고 아이콘·미니·상세·설정창을 각각 캡처한다.

`measure_outside.py --camera-blend`는 **연구 전용** 지연 보간 후보다. 지난 진짜 그림·UI를 약 원본 한 프레임
늦추고 두 실제 카메라 사이만 보간한다. 일반 native 빌드에는 후보 선택을 허용하지 않는다.
`--scene-pair`는 곡20초의 같은 원본 world/screen과 생성 그림을 읽는 별도 진단이며 성능에서 제외한다.
`--clip`의20~23초와 별도로 `analyze_camera_transition.py`는 읽기 전5~19초의 카메라 상수에서
shake·줌·회전·방향 전환 없는 이동의 되돌림만 센다. 화면 전체 품질을 증명하지 않는다.
`preview_camera.py <비교 폴더> --prediction-run <성공한 예측 판 이름> --output <webp>`는
off/지정한예측/blend4의 실제 GPU 표본으로 나란한 미리보기를 만든다. 실패 판은 보존하며 자동으로 다른 판을 고르지 않는다.

`analyze_full.py <완주 폴더>`는 배율을 summary에서 읽어5초~곡 끝의 집계와 긴 간격 목록을 저장한다.
`analyze_border.py <capture 폴더> --out <json>`과 `analyze_edge_runs.py <시각 비교 폴더> --out <json>`은
NumPy·Pillow가 있는 Python으로 실행하는 오프라인 픽셀 분석이다. 지연 모드의 같은 그림 대조는
실제 지난 슬롯을 저장한 `pair-pose.json`이 필요하다. 연구 `--scene-pair`는 기하 정보를 한 번 읽으며
시작 전 곡 시간과 되감기를 구분한다. 전체 품질 승인·물리적 표시 FPS로 쓰지 않는다.

`measure_outside.py --filter-pair`는 WideScreenHV의 실제 입력/출력을 곡20초 한 프레임에서 복사한다.
한 managed 효과에만 연구용 연결을 설치하고 native 종료 뒤 PNG를 읽는다. 원래 설정은 유지한다.
`analyze_filter_pair.py <capture 폴더> --out <json> [--preview <png>]`는 NumPy·Pillow로
테두리와 흰 그림 마스크×입력 모델을 비교한다. 성능 측정·필터 수정 시험과 구분한다.

`--screen-border --camera-blend`는 연구 전용 화면 고정 테두리 후보다. 원래 WideScreenHV가
한 번 실행된 뒤 그 입력을 출력에 다시 넣어, 뒤따르는 게임 필터도 빠짐없이 한 번씩 처리한다.
생성기는 그 그림을 옮긴 후 원래 material로 얻은 검은 마스크를 화면 좌표에 적용하고 UI를 덮는다.
**WideScreenHV와 뒤 필터의 적용 순서는 의도적으로 달라진다.** 전체 움직임·모든 필터 조합의
원본 동일성 승인이 아니다. Smooth0·StretchX/Y1·화면 크기 입력만 허용하며 다른 조건이면 중단한다.
마스크는 Size가 바뀔 때 다시 그리고 native는 이미지 슬롯마다 자기 복사본을 보관한다.
셰이더·세 번째 슬롯·설정·managed 효과 연결은 연구에만 있고 기본false다. 일반 native는 선택을 막는다.
원래 필터 입출력 증거인 `--filter-pair`와 함께 사용하지 않는다. `--scene-pair`의 별도 시각 판은
정확한 마스크와 source/display 카메라도 저장하며 GPU 읽기로 생긴 간격은 성능에서 제외한다.


`--layer-probe --clip`은 연구용 실제 장식 상태/원본 그림 진단이다. 다섯 시각에서 연속 원본
두 프레임의 중앙 장식128개까지 현재 시차·placement·위치/bounds를 `deco-state.csv`에 기록한다.
기본false·일반 미포함이며 GPU 읽기와 함께 성능 집계에서 제외한다.
`analyze_image_motion.py <capture> --mode 0 --out <json>`은 비압축 GPU 그림의 중앙60% 위상 상관을
재며 PSR/그림 변화량과 거절 표본도 보존한다. 원본 두 그림에는 카메라 중심 예상 이동과
실제 그림 이동·반복/카메라 warp 회색 MAE도 따로 기록한다. 저장 step6 픽셀과 실제 픽셀을
구분하고 압축 미리보기나 카메라 상수만으로 품질을 승인하지 않는다.

`--image-gate --camera-blend`는 연구용 조건부 반복 후보다(기본false, 일반 빌드 미포함).
최근 두 원본 장면의 중앙60%를9개 영역으로 나눠 각각256점에서 원본 반복과 카메라 warp의
밝기 정규화 상관을 GPU로 비교한다. 판단이 불확실하거나 고정 그림과 어긋나는 영역이 있으면
반복한다. 전체 원본 구간의 변환 상한16실제px 또는 급가속을 넘으면 그 구간 전체를 반복한다.
카메라 두 상태 사이만 보간하며 미래로 외삽하지 않는다. 경계 후보는 `--screen-border`로 함께 쓴다.
396바이트 GPU 결과는 EVENT 완료 뒤 DONOTFLUSH/DO_NOT_WAIT로만 읽으며, 미완료·혼잡은 기다리지
않고 반복한다. 캡처의 전체 텍스처 Map과 구분한다. `motion-gate.csv`는 성공 생성 Present를
재투영/반복으로 나눠 세며 원인0미완료·1불확실·2불일치·3일치·4이전 그림 없음·5상한·6급변·7혼잡이다.
반복을 보간 성공으로 해석하지 않는다. 추가 GPU 비교 비용은 원본 FPS에 포함되며 생성 그리기
timestamp에는 포함되지 않는다. `MotionGateLab.exe`는 별도 스왑체인 없이 고정/이동/밝기/무늬 없음과
위아래 반전의10가지 GPU 판별 및 상태 복원을 확인한다. 게임 이미지 검증과 별개다.

연구 `--clip`은 이제 GPU에서 step6로 줄인 출력·같은 이전 원본·다음 진짜 원본을 함께 저장한다.
EVENT가 준비된 작은 staging 그림만 DO_NOT_WAIT로 읽고, PPM 저장은 worker 종료 뒤에 한다.
전체 크기 GPU 복사·작은 RAM 복사 비용은 남으므로 성능 판과 분리한다. 최대256표본이며
대기열이 차면 기다리지 않고 skipped_busy에 센다. 이미지 게이트에서 실제로 옮긴 source마다
첫 생성도 추가 표본으로 잡고 `clip.csv`의 gate_reprojected에 구분한다.
`analyze_paired_motion.py <capture> --mode 4 --out <json>`은 같은 source/UI/최종 마스크의
identity 대조와 출력 왕복을 비교하며, 정지 원본의 추가 이동·반대 방향·끝값 초과도 확인한다.
이 대조는 별도 끔 판의 전체 필터 동일성이나 물리적 표시 품질을 뜻하지 않는다.
`check_motion_lab.py <fixture 폴더>`는 CPU 기준으로 GPU 축소·이진 마스크·UI·상하 반전을 바이트 대조한다.
연구 native를 다시 만들면 build_measure도 다시 실행한다. runner는 두 바이너리 SHA와 실제 설치된
native 및 삼중 캡처 파일을 확인하며, 내장 native가 오래된 빌드는 설치 전에 거부한다.

`summarize_repeat.py`의 손실 범위는 두 종류다. `scene_fps_decrease_bounds_percent`는 시간 가중 끔
기준의 카메라 콜백 누락 범위이고, `scene_loss_across_off_controls_percent`는 독립한 양끝 끔 판의
변동까지 포함한 범위다. 기준이10% 근처면 가중 값만 골라 통과시키지 않고 각 끔 값도 확인한다.

`--block-flow`는24장의 연구용 실제 그림 보간이다(기본false, 일반 managed/native 미포함).
두 알려진 원본의1/8 밝기 그림에서8×8 블록을 양방향으로 맞춘다. 전체 coarse 후보를 비교한 뒤
세밀하게 맞추며 최대7축소px(약56실제px) 밖은 예측하지 않는다. 역방향 일치·오차·가림을
통과한 블록만 중간 시점으로 옮겨 합친다. 무지·불확실·가림은 가까운 진짜 프레임을 쓴다.
UI 차이와 두 행성의 Renderer bounds(+32px) 영역도 가까운 원본을 유지한다. 늦게 생기는
Renderer·화면 필터가 크게 번지는 효과의 완전한 보호는 별도 확인이 필요하다.
원래 필터가 적용된 두 그림을 쓰며 종전 image-gate/screen-border 후보와 함께 켜지 않는다.
다음 진짜 그림을 기다려 한 원본 구간만큼 늦게 보인다. `timeline_to_submit_ms`는 보간 시점에서
Present 호출 완료까지의 소프트웨어 지연이며, 물리적 화면 지연은 PresentMon 등 별도 근거가 필요하다.
GPU 누적 카운터는256점에서 양쪽 원본과 각각4점 이상(3/255 초과) 다른 생성 그림을 세고,
움직이는 점도4개 이상 요구한다. 전체 픽셀 새 그림 비율과 같다고 단정하지 않는다. 읽기는
worker 종료 뒤에만 한다. 성능의 생성 GPU timestamp는 합성 그리기이며 원본별 블록 탐색과
계측 dispatch 비용은 제외한다(이 비용은 원본 FPS에는 포함된다).

`BlockFlowLab.exe <이미 존재하는 출력 폴더>`는 두 원본 사이16px 이동·정지 그림·UI·행성 보호와
위아래 반전·상태 복원을 독립 GPU로 검사한다. `--clip`의 `block_phase`는 같은 원본 두 장 사이
표시 시점이며 삼중 PPM으로 실제 새 그림과 움직임을 추가 확인한다. `analyze_paired_motion.py`의
새 그림 기준은 양쪽 원본과 각각1.5625% 이상 픽셀이3/255를 넘는 것이다. GPU256점 집계와
정의/표본이 다르므로 따로 보고한다. `analyze_block_regions.py`는 중앙9영역의 추가 진단이며
여러 층·가림·효과에서 위상 상관이 실제 움직임과 달라질 수 있어 거절 표본도 보존한다.
