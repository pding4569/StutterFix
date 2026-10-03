# 할 일 (PC 세션에서 순서대로)

클라우드 세션에서 만든 것(브랜치 `main-gpsl7k`)을 PC 에서 빌드·시험한다. 클라우드에서는 빌드도 게임 시험도 못 했다.
main 에 올리기·태그·릴리스는 사용자가 "올려"라고 할 때만.

## 1. MCP 설치 (사용자)

```bash
git fetch origin main-gpsl7k && git checkout main-gpsl7k
winget install Python.Python.3.12                     # sfmeasure 용. 새 터미널에서 python --version 확인
winget install astral-sh.uv                           # uvx (Serena, Windows-MCP 용)

# 꼭: 게임 DLL 디컴파일 (필요한 것은 mcilspy 안내를 따른다)
pip install mcilspy
claude mcp add ilspy -s user -- python -m mcilspy.server

# 권장: 필요한 함수 본문·부르는 곳만 찾아 읽기 (C#). 저장소 루트에서
claude mcp add serena -s local -- uvx --from git+https://github.com/oraios/serena serena start-mcp-server --context ide-assistant --project .

# 권장: 세션이 넘어가도 측정값·해 본 것 기억 (플러그인. 설치 방법은 claude-mem README 를 따른다)
#   CLAUDE.md 가 기준이고, 기억이 CLAUDE.md 와 어긋나면 CLAUDE.md 를 따른다

# 선택: 화면 조작 (UMM 창, 설정 창 눈으로 확인). 게임 켜고 끄기·측정은 sfmeasure 가 하므로 없어도 된다
claude mcp add windows-mcp -s user -- uvx windows-mcp serve
```
- `sfmeasure` 는 저장소 `.mcp.json` 에 이미 등록돼 있어 따로 설치하지 않는다(파이썬만 있으면 됨).
- 나중에: Discord MCP(제보 읽기, 아직 안 넣음), graphify·task observer(효과 작음).
- 명령은 각 도구의 안내(README)로 한 번 확인한다. 클라우드에서는 직접 설치해 보지 못했다.
- PresentMon 을 쓰려면 환경 변수 `SF_PRESENTMON` 에 PresentMon.exe 경로 (관리자 권한 필요).
- **Claude Code 를 저장소에서 다시 켠다** (MCP 는 세션 시작 때 붙는다) → `/mcp` 에서 `sfmeasure`, `ilspy`, `serena`(와 깐 것) 승인.
- 디컴파일한 게임 코드는 보기만 하고 저장소·커밋·주석에 넣지 않는다.

## 2. 빌드·설치 (Claude)

- `dotnet build` (개발자용, 자동 시험 포함) → 훅이 Mods 에 설치. 클라우드에서 쓴 코드의 첫 컴파일이다: `StutterFix.cs`, `SettingsWindow.cs` 의 메모리 정리만 쓰기(GcOnly).
- `tools/LoadCheck` 로 불러오기 확인.

## 3. sfmeasure 첫 시험 (Claude, 사용자는 맵 경로만 알려 줌)

- `sf_status`: DLL 자동 시험 "있음", boot.config 상태.
- 짧은 `sf_run`: `["open <맵>", "auto on", "play", "wait 30", "stop"]`. 게임이 저절로 켜지고 꺼지는지, 요약이 나오는지.
- 안 되면 `tools/sfmeasure/server.py` 를 고친다(경로, 프로세스 이름, 로그 형식).

## 4. 메모리 정리만 쓰기(GcOnly) 시험 (Claude)

- `sf_run` 에 `settings: {"GcOnly": true}`: 로그에 "켜짐: 메모리 정리만", 그 밖의 패치·boot.config 수정 없음, 곡 중 GC 멈춤은 동작(`GC 재개` 줄).
- 끄고(`false`) 다시 돌려 원래 기능이 전부 돌아오는지. 설정 창 플레이 페이지에 항목이 보이는지(사용자 눈으로).

## 5. sfnative.dll 확인 (Claude)

- Arche 열기 시간과 C# 대조 결과. 괜찮으면 다음 버전 후보 (MAIJEUN 님 크레딧).

## 6. 테스터 zip (Claude → 사용자가 디스코드로 전달)

- `./pack.sh test 1` → `dist/StutterFix-2.4.7.1-tester.zip`. UMM 목록에 "테스터 2.4.7.1" 로 보이는지 확인.

## 7. 측정·구현 (`docs/next-design.md`, sfmeasure 로)

1. 효과 재사용 조회 줄이기 (`FfxReuse.Arrange`): 대조 + 곡 중 할당량.
2. 판정 글자 `DOTween.Kill` 비용: ILSpy 로 `ShowHitText` 확인 → `sf_ab`.
3. 편집 화면 카메라 끌기 튐 (`campan`), 곡 중 쓰레기 0.47MB/s.

## 8. 화면 출력 실험 (`sf_presentmon`, `sf_ab`)

- 지금 출력 방식(Flip/합성)과 찢어짐 허용 확인 → GPU 우선순위, 대기 프레임 수 1. 효과가 있을 때만 넣는다.

## 보류

- **TUFReplay 충돌**: 그 모드 제작자가 분석할 때까지 기다린다. 제보자에게 Player.log 를 요청해 둠. 그동안은 4번의 "메모리 정리만 쓰기"가 임시 해결책.
- 맵 확인 도구(보기만), AutoTest 명령 정리: 7번 뒤에.
