# 프로젝트 저장공간 이동 (2026-10-09)

개발·시험의 실제 경로는 `E:\StutterFixWork\StutterFix`다. 전체 접근 허용 뒤 하나씩 복사하고
파일 수·길이·SHA256을 전부 비교했다. C: 프로젝트 원본은 앱의 폴더 사용 때문에 아직 보존 중이다.
C: 원본에서 새 작업을 하지 않는다. main/태그/릴리스/새ZIP0이다.

| 자료 | E: 목적지 | GiB | 검증 파일 수 | C: 원본 제거 |
|---|---|---:|---:|---:|
| StutterFix 전체 | E:\StutterFixWork\StutterFix |14.877|32151|보류|
| 측정 기록 | E:\StutterFixWork\StutterFixTrace |19.120|2183|완료|
| PerfView 임시 분석 캐시 | E:\StutterFixWork\PerfViewCache |2.959|2|완료|

합계36.956GiB·34336파일 검증. 측정 기록·캐시의 옛 C: 경로는 검증한 E: 목적지의 junction이다.
제거 뒤 C: 여유33.041GiB(35,477,209,088바이트)다. 실제 제거한 자료는22.079GiB다.
원시 해시 목록은 외부 `E:\StutterFixWork\migration-verified.json`에 보존하며
개인 자료 이름이 있어 Git에 넣지 않는다. 작은 검증 요약은 `storage-validation.json`이다.
`C:\SFBundle`은 원래부터 `E:\SFBundle` 연결이다. 공유 Codex 런타임·설정, D: 게임·맵,
E: 기존 자료는 변경하지 않았다.

## 남은 이전

복사 완료 뒤 원본을 다시 해시 검증했으나 C: 프로젝트 루트 이름 변경은 실패했다.
Claude 종료 뒤에도 실패했다. Windows 폴더 사용 프로세스 조회에서 ChatGPT/Codex와
연결 도구 Python/Node/uv 프로세스가 확인됐다. 이 프로세스를 강제 종료하지 않았다.
프로젝트 C: 원본 제거와 루트 호환 연결은 앱이 폴더를 놓은 뒤 남은 작업이다.
E:에서 진행한 최신 수정·커밋을 C:의 옛 파일로 덮어쓰면 안 된다.

`tools/move-project-storage.ps1`은 기본 미리보기, 명시적 실행 때만 단일 robocopy→길이/SHA256
검증→전체 원본 재검증→원본 이름 변경/호환 연결→검증된 원본 제거 순서다. 기존 목적지를
덮어쓰지 않는다. 프로세스의 OS 현재 디렉터리도 E:로 바꾸지만 다른 앱의 폴더 핸들까지 풀지는 못한다.
부모 폴더를 다른 앱이 사용 중이면 이동 중단 원인과 원본 보존을 기록한다.

## 창 사라짐 기록

Windows17:12:47 가상 메모리 부족→17:12:50 DWM 추가 메모리 할당 실패·종료,
Git 스택 보호 페이지 생성 실패→17:13:02 Git NULL 쓰기 오류→17:13:12 DWM 종료 기록이다.
사용자가 당시 C:230MB를 보고했고 재부팅 뒤 읽은 값은11.24GiB다. 가상 메모리 부족을
일으킨 단일 프로세스·Git 압축의 최고 메모리·Claude 인과는 확인 안 됨이다.
Git 명령과 E: 저장소 로컬 설정에 `gc.auto=0`, `pack.threads=1`, `pack.windowMemory=128m`,
`pack.deltaCacheSize=64m`을 적용했다. 이 설정이 사건 원인을 입증하지는 않는다.
E: Git 연결 검사 종료0(기존 dangling 객체는 보존), PlayerAuto 빌드 오류0·LoadCheck220/오류0이다.

셰이더14장까지 완료한 `16ee49a`의 시험은 반복하지 않는다. 강한 네온후보6의 캡처 없는
FX끔/후보/끔 일반4배 대조를 이어간다. 실제 표시 지연은 별도 PresentMon 근거가 필요하다.
