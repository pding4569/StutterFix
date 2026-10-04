# 할 일 (PC 세션에서 순서대로)

클라우드 세션에서 만든 것(브랜치 `main-gpsl7k`)을 PC 에서 빌드·시험한다. 클라우드에서는 빌드도 게임 시험도 못 했다.
main 에 올리기·태그·릴리스는 사용자가 "올려"라고 할 때만.

## 1. MCP 설치 (사용자)

```bash
git fetch origin main-gpsl7k && git checkout main-gpsl7k
# 파이썬은 설치돼 있음 (python --version 만 확인)
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

- 됨 (2026-10-03). `dotnet build` (개발자용, 자동 시험 포함) → 훅이 Mods 에 설치.
- `tools/LoadCheck` 로 불러오기 확인.

## 3. sfmeasure 첫 시험 (Claude, 사용자는 맵 경로만 알려 줌)

- `sf_status`: DLL 자동 시험 "있음", boot.config 상태.
- 짧은 `sf_run`: `["open <맵>", "auto on", "play", "wait 30", "stop"]`. 게임이 저절로 켜지고 꺼지는지, 요약이 나오는지.
- 안 되면 `tools/sfmeasure/server.py` 를 고친다(경로, 프로세스 이름, 로그 형식).
- 됨 (2026-10-03): `D:/얼불춤 맵 파일/2022/level.adofai` 로 켜고 끄기·요약 정상 (곡 평균 415 FPS, 연출 뒤 최대 8ms).

## 4. (지움) 메모리 정리만 쓰기(GcOnly)

- 넣지 않기로 함 (2026-10-03): TUFReplay 충돌의 정확한 원인이 나오지 않았는데 다른 기능을 다 끄는 설정을 둘 이유가 없다. 커밋 ab35428·0b48ce6 되돌림.

## 5. sfnative.dll 확인 (Claude)

- Arche 열기 시간과 C# 대조 결과. 괜찮으면 다음 버전 후보 (MAIJEUN 님 크레딧).
- 됨 (2026-10-03): Arche 열기 43.0초(이미지 301장 21.0초), 네이티브 C# 대조 1996번 중 다름 0, 곡 평균 338 FPS·연출 뒤 최대 13ms.

## 6. 테스터 zip (Claude → 사용자가 디스코드로 전달)

- `./pack.sh test 1` → `dist/StutterFix-2.4.7.1-tester.zip`. UMM 목록에 "테스터 2.4.7.1" 로 보이는지 확인.
- 됨 (2026-10-04): 2.4.7.1 테스터 zip (TypeCache, 다시 하기 가볍게, MeshWarm, SoundWarm, FloorAnim, 타일 이동 나눠 처리). 불러오기 확인 FieldRefAccess 200곳 틀림 0.

## 7. 디컴파일로 최적화 후보 찾기 (Claude, ilspy)

측정으로 무거운 곳을 먼저 짚고 → 그 게임 함수를 C# 으로 읽어 원인을 찾고 → 고치고 → 원래와 같은지 대조한다.
코드만 보고 고치지 않는다(호출 횟수·실제 비용은 sf_ab 로 확인). 디컴파일한 코드는 저장소에 넣지 않는다.

- 측정으로 이미 짚인 곳부터: `scrVfxPlus.Update`/`StartEffect`(효과별 시작 비용), `scnGame.UpdateDecorationObjects`,
  `LevelData.LoadLevel`(맵 파일 읽기 6.4초, 멈춘 네이티브 JSON 작업의 다른 길), `ShowHitText`, `scrFloor.Update`, 박자 알림(`scrConductor`).
- 매 프레임 도는 게임 함수를 훑어 흔한 낭비 찾기: Update 안 `GetComponent`·`FindObjectsOfType`·`Camera.main`, 문자열 합치기, LINQ·람다 할당,
  리스트 전체 훑기(O(n²)), 같은 값 다시 넣기. 찾은 것은 목록으로 남기고 할당량·A/B 로 실제로 큰 것만 고친다.

## 8. 측정·구현 (`docs/next-design.md`, sfmeasure 로)

1. 효과 재사용 조회 줄이기 (`FfxReuse.Arrange`): 대조 + 곡 중 할당량. → 됨 (2026-10-04): 되돌려 쓴 적 없는 오브젝트는 GetComponent 그대로, 대조 3,520번 다름 0.
2. 판정 글자 `DOTween.Kill` 비용: ILSpy 로 `ShowHitText` 확인 → `sf_ab`. → 저절로 해결: `DOKill` 3번이 살아 있는 트윈을 다 훑는데, FloorAnim·DecoAnim 뒤로 살아 있는 진짜 트윈이 120~300개뿐.
3. 편집 화면 카메라 끌기 튐 (`campan`), 곡 중 쓰레기 0.47MB/s. → 확인 (HELLO 2026): 95% 8.2ms, 최대 13ms (측정 명령 자체의 설치 5초 프레임 빼고). 할 것 없음.

## 9. 화면 출력 실험 (`sf_presentmon`, `sf_ab`)

- 지금 출력 방식(Flip/합성)과 찢어짐 허용 확인 → GPU 우선순위, 대기 프레임 수 1. 효과가 있을 때만 넣는다.
- 됨 (2026-10-04): 지금은 Composed: Flip(합성). 대기 프레임 1 은 FPS 209→128·지연 늘어 버림. 독점 전체 화면은 Independent Flip, 지연 15.8→7.9ms → 실험 옵션(기본 끔). GPU 우선순위는 다른 프로그램이 GPU 를 같이 쓸 때만 의미가 있어 미룸.

## 10. 오늘(2026-10-04) 나온 것

- HELLO 2026 곡 시작 5.1초: 됨 — 처음 쓰는 효과의 JIT 였다(`JitWarm`), 42~43ms → 끊김 없음.
- UI 캔버스 23~28ms: 판정 글자 TMP Rebuild 22.7ms 까지 좁힘(CLAUDE.md). 재현이 들쭉날쭉해 원인 함수는 아직.
- PLUM MEGAMIX 520~570초: 소리 불러오기 아님. 대부분 GPU 과부하(519.9초 GPU 20ms, 528.5초 27ms, 맵 그리기 자체). 567초는 메인 스레드가 다른 스레드를 17ms 기다림(PerfView 샘플 유실로 더 못 봄).
- 남은 타일 이동 비용(타일당 약 2.5us, transform 엔진 호출): 저사양 나눠 처리로만 덮는다.

## 보류

- **TUFReplay 충돌**: 그 모드 제작자가 분석할 때까지 기다린다. 제보자에게 Player.log 를 요청해 둠.
- 맵 확인 도구(보기만), AutoTest 명령 정리: 8번 뒤에.
