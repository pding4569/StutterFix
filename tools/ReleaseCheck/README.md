# ReleaseCheck

정식 ZIP의 DLL 바이트를 바꾸지 않고 완주를 확인하는 임시 UMM 모드다.
자동 시험 빌드와 정식 DLL 검증을 구분하기 위해 게임 제어만 별도 모드로 둔다.
배포 ZIP에는 포함하지 않는다. 성능 A/B용 도구가 아니다.

`dotnet build -c Release` 후 임시 모드 폴더에 ReleaseCheck.dll과 UMM Info.json,
시험 맵 경로 한 줄의 map.txt를 둔다. 폴더가 이미 있으면 덮어쓰지 않는다.
원래 입력 확인을 한 번만 통과시키고 자동 플레이로 완주한 뒤 정상 종료한다.
실패·시간 초과도 로그를 남기고 정상 종료를 요청한다. Unload에서 자동 플레이와
이 도구의 Harmony 패치를 되돌린다. 시험 뒤 임시 모드 폴더를 제거하고
게임 설정 원본을 복원한다. 개인 경로가 들어가는 map.txt는 커밋하지 않는다.

통과 근거는 실제 설치 ZIP/DLL SHA, `COMPLETE OnLandOnPortal`, 게임의 곡 요약,
모드 설치 오류 검사, 엔진 Shutdown이다. 단순 프로세스 종료는 통과가 아니다.
