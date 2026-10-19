# Codex 작업 U: 쉐이더 "내 프리셋 저장·불러오기" + 한 번에 껐다 켜기 — 게임 실행·Mods 설치·커밋 금지

결과: 코드 변경 + `docs/handoff/codex-fxslots-20261019-result.md`(**apply_patch UTF-8 BOM**, 한글 확인). 빌드 셋(Dev/Player/PlayerAuto) 오류 0. 인코딩 검사 `tools/check-ui-encoding.py` 통과(한글 `???` 깨짐 방지). 기본 동작은 그대로(새 기능은 눌러야만 작동).

## 사용자 요구 (2026-10-19)
"쉐이더 저장 기능 필요함, 이거 일일이 껐다 키는 거 ㅈㄴ 에바임." 지금은 쉐이더(화면 효과 20종 + 기본 효과)를 맵마다 하나씩 켜고 끄고 값을 맞춰야 한다. 있는 것: 모양 4종(선명/또렷/네온/직접 조절, `ScreenEffects.Preset`), `모두 끄기` 단추(켜짐 탭), `설정 공유`(코드 한 줄 복사/붙여넣기: `FxShare.Export(c)`/`FxShare.Import(code, c, out msg)`), 실험 `맵별 화면 설정 저장`(`c.MapProfilesEnabled`, 맵 내용 해시별 자동 저장). 없는 것: **이름 붙여 여러 개 저장해 두고 한 번에 불러오기**, **끈 쉐이더를 한 번에 다시 켜기**.

## 만들 것
1. **내 프리셋 슬롯**: 쉐이더 페이지 "켜짐" 탭(또는 사용자가 자주 쓰는 곳; `SettingsWindow.cs` 의 쉐이더 페이지 구조 읽고 가장 자연스러운 자리 선택, 현재 `설정 공유` 근처도 가능)에 "내 프리셋" 섹션: 이름 입력 + `지금 설정 저장` 단추, 저장된 목록(최대 12개)마다 `적용`(한 번 클릭)·`덮어쓰기`·`이름 바꾸기`(또는 삭제 후 다시 저장으로 대체 가능)·`삭제`. 저장 내용 = `FxShare.Export(c)` 와 같은 코드 문자열(이미 쉐이더 값 전체를 담는다 — 어떤 필드가 담기는지/LUT 파일 제외 등 한계를 확인해 문서에 적고, 부족하면 `FxShare` 에 필드 추가가 안전한지(코드 호환·버전)). 불러오면 `ScreenEffects.Apply()` 후 저장, 켜진 쉐이더 수·메시지 표시. 오류(깨진 코드)는 인라인 메시지.
2. **영구 저장**: `Settings` 에 `List<string>`(이름, 코드) 두 목록 또는 작은 직렬화 클래스 목록을 추가(UMM `UnityModManager.ModSettings` XmlSerializer 호환 — 기본 생성자, public 필드; 기존 Settings.xml 이 새 필드 없이도 읽혀야 한다: 마이그레이션 0, 기본 빈 목록). 이름은 중복·빈 문자열·너무 긴 값 처리.
3. **한 번에 껐다 켜기**: `모두 끄기` 를 누르면 지금 켜 둔 쉐이더 집합(코드)을 `Settings` 에 한 칸 기억(`FxLastOn`)하고, 그 자리에 `다시 켜기`(되돌리기) 단추를 보여 준다. 끄기→켜기 반복이 한 클릭이어야 한다. (같은 기능을 다른 이름으로 이미 쓰는 곳이 있으면 재사용.)
4. 한국어/영어 문자열 모두(`T("한글","English")` 패턴). 설정 창 디자인 규칙은 `CLAUDE.md` 의 "설정 창 디자인" 절(Linear/Raycast, 상자 최소, 줄 목록, 작은 스위치) 따름: 새 섹션은 `SubHeading` + `BeginGroup/EndGroup` + 기존 `Btn`/`Option` 스타일을 재사용, 색 규칙 유지, 글자 많이 늘리지 말 것. IMGUI `TextField` 의 포커스·키 입력이 게임 단축키와 충돌하지 않는지 확인(다른 텍스트 입력 칸 `숫자 편집`과 같은 처리: `GUI.SetNextControlName`, 포커스 중 `GUIUtility.keyboardControl`).
5. **검증 도구**: 개발자용(`DEV`/`AUTOTEST`) 자동 시험 명령 `fxslot save <이름>`, `fxslot apply <이름>`, `fxslot list`, `fxslot delete <이름>` 와 `fxslot selftest`(저장→모두 끄기→적용→모든 `Fx*` 필드가 저장 전과 같은지 비트 비교, 되돌리기, XML 왕복) — `AutoTest.cs` 에 한 줄씩. 게임 안 UI 확인은 기존 `ui <페이지>.<갈래>` + `shot <이름>` 으로 Claude 가 한다.
6. 결과 문서에: 어디에 무엇을 넣었는지, 저장 형식, 알려진 한계(LUT 파일 등), Claude 가 돌릴 확인 순서(빌드 → 설정 창 열기 `ui` + `shot` → `fxslot selftest` → 실제 슬롯 저장·재시작 후 유지).

## 규칙
- 기본값·기존 설정은 바꾸지 않는다. 새 필드는 기본 빈 값. 제품 Player 빌드에도 UI 는 들어가고, `fxslot` 명령만 DEV/AUTOTEST.
- 버튼 배치는 한 줄 폭을 넘지 않게(좁은 창에서도).
