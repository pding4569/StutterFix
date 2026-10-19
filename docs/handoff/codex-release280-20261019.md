# Codex 작업 S: StutterFix 2.8.0 릴리스 문서 초안 (패치노트·README 절·디스코드 공지) — 게임 실행·Mods 설치·커밋·태그·푸시 금지

결과: `docs/patch-notes-2.8.0.md`, `docs/announcement-2.8.0.md`, README.md 맨 위 "2.8.0 에서 바뀐 것" 절(+목차 줄), 오래된 절을 `CHANGELOG.md` 로 옮기기, 그리고 `docs/handoff/codex-release280-20261019-result.md`(검토 메모: 근거·확인 못 한 것). 모든 파일 **UTF-8 BOM, apply_patch**, 쓴 뒤 한글 확인. `Info.json`/`repository.json` 버전은 건드리지 않는다(Claude 가 올림).

## 기준
- `docs/release.md` 의 "1. 버전 올리기"와 "쓰면 안 되는 것"을 지킨다: 게임 DLL·디컴파일 코드·개인 시험 맵 이름(특히 헤르츠 맵 이름은 절대 쓰지 않는다)·사용자 이메일·다른 모드 코드 금지. 맵 이름은 2.7.0 공지가 쓴 공개 맵 수준(Arche·HELLO)까지만, 되도록 "무거운 커스텀 맵"처럼 일반화. 문체는 `docs/patch-notes-2.7.x.md`, `docs/announcement-2.7.0.md`, README 의 지난 절을 따라(쉬운 한국어, 측정값은 사용자가 이해할 말로, 과장 금지, "추정" 은 추정이라고).
- 범위: **v2.7.3 이후 사용자에게 영향이 있는 변화** (`git log v2.7.3..HEAD`, `docs/optimization-36.md`, 코드의 실제 기본값). 각 항목은 **코드에서 기본 켬/끔을 확인**하고 적어라(예: `StutterFix.cs` 의 파일 플래그 `-off`/`-on`, `Settings` 기본값, `SettingsWindow` 토글). 문서와 코드가 다르면 코드가 맞다(예: `TightLate.SkipSame` 선언은 true 지만 로드 때 `tightskip-on` 파일이 있어야 켜짐 → 실제 기본 꺼짐).
- 측정값은 `docs/optimization-36.md`·`docs/handoff/*` 에 있는 것만 인용(수치와 맵·조건 함께). 새로 만든 숫자 금지. 개발용·측정 전용 도구(profsamp, fuse, decosamp, decogpu, DecoEngine 등 DEV/AUTOTEST/기본 꺼짐 연구)는 노트에 넣지 않거나 "개발자용" 한 줄.
- 반드시 포함할 후보(코드로 기본값 확인 후 사실이면): 장식 LateUpdate 빠른 길(TightLate, Arche 곡 260~278초 +8%), 색 직통 경로(ColorFast), 같은 각도 회전 계산 재사용(eulermemo), 위치 X·Y 짝 합치기, 보이는 장식 회전·크기 LateUpdate 위임, 투명 장식 SetSprite 미루기(LazySprite), 타일 색/이동 최적화(RecolorDefer, 불투명도 전용 빠른 경로), 화면 밖 타일 발광 렌더러 끄기, 메인·렌더 스레드 우선순위 기본 켬(곡 중 무작위 30ms+ 프레임), JSON 문자열 풀·로딩 업로드 예산·정수 태그 색인(맵 열기), 게임 업데이트(Unity 6000.3.25f1) 대응(`AllocFix` 입력 후킹 모양이 다르면 끔), 설정 창/토글 변화, 새로 생긴 기본 꺼짐 토글들 중 설정 창에 보이는 것(이름·한 줄 설명).
- 알려진 제한/주의도 짧게: 이번 게임 업데이트 뒤 다른 모드와의 호환은 모드별로 다름(코드·로그로 확인된 것만, 예: 입력 API 변경으로 일부 모드가 예외를 낼 수 있음 — 우리 문제가 아님을 사실대로).
- `announcement-2.8.0.md` 는 2.7.0 공지처럼 짧게(디스코드용, 7~9줄) + 자동 업데이트 안내 + 로그 내보내기 안내 + 릴리스 링크(`https://github.com/pding4569/StutterFix/releases/tag/v2.8.0`).

## 검토 메모에 적을 것
- 각 항목의 근거 파일/커밋과 코드상 기본값 확인 위치, 문서와 코드가 달랐던 곳, 근거가 약해 뺀 항목과 이유, 사용자가 결정해야 하는 것(예: 어떤 기본값을 노트에 강조할지).
