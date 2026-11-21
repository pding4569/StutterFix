# Codex 작업: StutterFix 2.8.2 — 자세한 로그 끄고 켜기 + 로그 폭주 막기

작업 폴더 `E:\StutterFixWork\wt-282log` (브랜치 claude/282-log, release/2.8.2 의 d099b46 에서 갈라짐). **커밋·푸시·게임 실행은 하지 않는다**(Claude 가 한다). 빌드는 해도 된다.

## 배경 (제보)
"로그가 너무 많이 쌓여 다른 모드 로그를 읽을 때 힘들고, 가끔 로그가 폭주로 나오면 크래시가 나는 경우가 종종 생기는 것 같아요. 자세한 로그를 껐다 켤 수 있는 토글 기능이 있으면 좋겠어요."
모드는 `Main.Entry.Logger.Log(...)`(UMM ModLogger, UMM Log.txt 와 Unity Player.log 둘 다에 쓴다)을 수천 곳에서 쓴다. 정식(Player) 빌드 HELLO 전곡 한 판의 우리 줄은 약 108줄(`tools/ReleaseCheck/out/full-game-2.8.2.log` 에서 `[StutterFix]` 로 시작하는 줄)이다.

## 해야 할 것
1. **`Lg.cs`(새 파일, csproj 에 Compile Include 추가)**: 모든 모드 로그가 거치는 한 곳.
   - `Lg.I(string)` 정보 줄, 기존 `Logger.Warning/Error` 호출도 같은 한도(2번)를 거치게 `Lg.W/E` 제공.
   - `Lg.Verbose`(정적 bool). **개발자용(DEV)·자동 시험(AUTOTEST) 빌드에서는 항상 true**(자동 시험이 로그 줄 모양에 기대므로 그 빌드의 로그 내용·모양을 바꾸지 말 것).
   - 정식(Player) 빌드: 설정 `VerboseLog`(기본 **false**)가 true 일 때만 자세히. false(보통)일 때는 **필수 줄만** 통과:
     오류·경고·실패(메시지에 `실패|오류|예외|경고|못 |Error|error|fail|Exception` 포함), `This game is modded`, `[안정성]`(안전 모드·비정상 종료), `[곡]`(곡 요약 한 줄), 버전·불러오기 한 줄, 기능 요약 한 줄.
     나머지(설치 줄, [이미지] 단계 시간, [상태], [로딩] 등)는 자세히 모드에서만. 규칙은 `Lg.cs` 한 곳의 목록으로 두고, `tools/ReleaseCheck/out/full-game-2.8.2.log` 로 "보통 모드에서 몇 줄이 남는지"를 오프라인으로 세어 보고(목표 25줄 안팎, 끊김 진단에 꼭 필요한 줄은 남길 것) 결과를 `docs/handoff/codex-282-log-result.md` 에 적는다.
2. **폭주 막기(모든 에디션의 정식 경로, 자세히 모드에서도)**: 같은 줄(숫자를 지운 첫 60자 키)은 10초에 3번만 쓰고 그 뒤는 세기만 한다. 창이 끝나면 "(같은 로그 N번 생략: <키>)" 한 줄만 남긴다. 전체는 초당 100줄 넘으면 넘치는 줄을 세기만 하고 1초 뒤 "(로그 초과 N줄 생략)" 한 줄. 매 프레임·훅 안에서 불려도 되게 **할당 없이 싸게**(문자열 만들기 전에 막을 수 있으면 `Lg.I(Func)`/조건부 오버로드는 필수 아님, 단 키 계산에서 새 문자열을 만들지 말 것: 첫 60자 해시로).
   스레드 안전(다른 스레드에서도 불림: 잠금 대신 Interlocked/ThreadStatic 으로 가볍게, 틀려도 줄이 더 나오는 쪽으로).
3. **치환**: 저장소 모든 `.cs` 의 `Main.Entry.Logger.Log(` · `Entry.Logger.Log(` · `modEntry.Logger.Log(` · `Main.Entry.Logger.Warning(` · `...Error(` 를 `Lg.I(`/`Lg.W(`/`Lg.E(` 로 기계적으로 바꾼다(자기 자신 `Lg.cs` 안 내부 호출은 제외). 형식 문자열·인자는 그대로. `tools/` 아래 별도 프로젝트와 `native/` 는 건드리지 않는다.
4. **설정**: `Settings` 에 `public bool VerboseLog = false;` 추가, `Main.ApplyConfig`(StutterFix.cs)에서 `Lg.Verbose = Edition.Dev || ... || Config.VerboseLog` 처리. **설정창**(SettingsWindow.cs)에 토글 하나("자세한 로그 / Verbose log", 설명: 켜면 모드가 자세한 진단 줄을 남기고 끄면 필수 줄만 남깁니다. 문제를 제보할 때 켜고 재현해 주세요) — 기존 항목들이 있는 "문제 보고" 근처(로그 내보내기 단추 가까이)에 둔다. 기존 `Option(...)` 패턴과 한글·영문 T() 쌍을 따른다. LogExport(로그 내보내기)가 쓰는 설정 목록 출력에 VerboseLog 상태도 넣는다.
5. 규칙: UI 문자열은 한국어 기본·영어 병기(`T("한글","English")`). 줄 끝은 CRLF·BOM 유지(파일마다 원래대로: 편집 후 `file` 로 확인). 새 게임 API 호출 금지. 개발자용·자동 시험 로그 내용 불변.

## 검증 (게임 없이)
- `dotnet build StutterFix.csproj -c Release -p:Edition=Player`, `-p:Edition=Player -p:AutoTestBuild=1`, 기본(Debug, 개발자용) 세 빌드가 모두 오류 0.
- `tools/LoadCheck` 로 Player·Debug DLL 불러오기 확인(`dotnet build tools/LoadCheck -c Release -o tools/LoadCheck/bin` 뒤 `tools/LoadCheck/bin/LoadCheck.exe bin/Player/StutterFix.dll`, 틀림 0).
- `tools/LogCheck`(새 콘솔 프로젝트)로 `Lg` 의 한도 로직(같은 줄 3번/10초, 초당 100줄, 필수 줄 통과, 자세히 모드 통과)을 단위 시험(`Lg.cs` 를 파일로 포함, Unity 의존 없이 시험 가능하게 시계와 출력 대리자를 주입).
- 치환 뒤 `git grep -n "Logger\.\(Log\|Warning\|Error\)" -- "*.cs"` 가 `Lg.cs` 외에 남지 않음.

끝나면 `docs/handoff/codex-282-log-result.md` 에 바뀐 파일, 보통 모드 줄 수(오프라인 계산), 시험 결과, 못 한 것을 적고 마지막 줄에 `CODEX_282_LOG_DONE`.
