# 2.6.0 정식 릴리스 전 확인 (2026-10-10)

사용자 “다음 정식 버전 2.6.0으로 올려” 승인에 따라 docs/release.md를 진행했다.

| 항목 | 결과 |
|---|---|
| 플레이어/개발자 LoadCheck |219/220곳 · 틀림0/0|
| 실제 플레이어 ZIP 기본값/기존 저장 배율/리소스 검사 |오류0|
| 일반 MeasureBuild/AutoTest/Dev/합성카운터훅 |0/0/0/0|
| native·effects·FSR 내장 리소스 |원본SHA 일치3/3|
| ZIP CRC/구성 |2종 · 오류0|
| 실제 일반 ZIP HELLO 2026 전체 |OnLandOnPortal1·Won1·정상Shutdown1|
| SF집계오류/native실패 |0/0|
| 곡 요약 진짜FPS/초반 포함 최악 |136 / 1060ms|
| 시작 연출 뒤 최악/곡 중 끊김 집계 |32ms / 0회|
| 실제 화면/프레임 생성 |3440×1440·165Hz·D3D11·동기0·Exclusive / 4배|
| 기존 설정 원본바이트/임시도구 제거 |1/1|
| 기존 테스터1/2/3·FrameGenLab ZIP 보존 |4/4|
| 새 성능 A/B·PresentMon·전체 픽셀 품질 |확인 안 됨|

정식 ZIP의 플레이어 DLL 바이트를 그대로 설치했다. DLL 자체의 AutoTest는 false다.
별도 임시 UMM ReleaseCheck가 맵 열기·자동 플레이·포탈 완료 감지·정상 종료만 제어했다.
성능 측정 판이 아니며 테스트 도구는 배포 ZIP에 넣지 않았다. 종료 뒤 임시 모드 폴더를 제거하고
UMM Params.xml·모드 Settings.xml 원본을 복원했다. 일반2.6.0은 사용자 게임에 유지했다.
기존 DOTween775개 집계는 SF오류0과 구분한다. 초반1060ms가 있어 전체 무끊김으로 확대하지 않는다.
완주1판을 모든PC/모든맵의 안정성·품질 증명으로 쓰지 않는다.

플레이어 managed SHA는 테스터3과 같다. 이번 버전 준비의 런타임/셰이더/프리셋 변경0이다.
기본값·독점 전환·표시 대기의 앞선 검증은 framegen-research31~33장, 화면 효과는
shader-research14~16장에 있다. 전체화면500Hz 카운터 정지 원인은 여전히 확인 안 됨이다.

공개 검사자료: tools/ReleaseCheck/validation-2.6.0. 개인 경로와 전체 Player.log는 out에만 둔다.
릴리스 본문은 docs/patch-notes-2.6.0.md와 README의2.6.0 절이며,
플레이어에게 보낼 짧은 공지는 docs/announcement-2.6.0.md다.
