# FrameGenLab — 게임 밖 프레임 생성 시험

11장은 9장 **1a와 0단계**, 12장은 수직동기 0과 **1b 상태·UI 조사**다. 실제 게임의 프레임 생성 설정은 아직 추가하지 않았다.
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
