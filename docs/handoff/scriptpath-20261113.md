# 코덱스 작업: 메인 스레드 게임 스크립트 경로 분석 + 상한 측정 플래그 (2026-11-13)

작업 폴더 `E:\StutterFixWork\wt-v14prod` (claude/adofai-renderer). 커밋·푸시·게임 실행·설치 DLL 교체 금지. 한국어 보고. 결과 `docs/handoff/scriptpath-20261113-result.md`.

## 배경
3440 렌더에서 HELLO·Exit·Arche 는 메인 스레드 병목. 렌더링 쪽 새 방식 세 가지(픽셀 깊이 가림 0%, 겹친 층 압축 = 8비트에서 손실, 타일 인스턴싱 = 타일마다 메시·재질이 달라 불가)는 오늘 닫혔다.
메인 스레드 1000Hz 스택 표본 집계: `docs/handoff/hl-stacktree-20261113.txt`, `docs/handoff/sh-stacktree-20261113.txt` (원본 E:\StutterFixWork\night-results\nat-hl.resolved.txt, nat-sh.resolved.txt, 집계기 tools/analysis/stacktree.py·decopath.py). 이 표본은 RunLod 켠 설정이라 Camera.FireOnPreCull(RunLod) 은 제품 기본(꺼짐)에서는 없다.
**규칙(사용자):** 코드 이해에서 멈추지 말고, 각 함수가 어떤 경로로 매 프레임 실행되고 무엇과 얽히는지(다른 우리 패치·게임 코드·다른 모드와의 시너지) 추적한 뒤, 싼 상한 측정으로 확인하고 구현한다. 지난 시도(메모/문서: BeatSkipNoChange, HoldSkip, HitMeterDirectKill, TileCull, MouseEventsSkip, turbo safe2, FloorFix 삭제 사유)를 먼저 확인해 반복하지 않는다. 화면·판정·소리는 원본과 같아야 한다(비트 동일 검증).

## 할 일
1. 표본 상위 진입점 중 장식 경로를 뺀 것 — `scrFloor.Update`(HL 5.3%), `scrConductor.Update`(4.2%), `Canvas.SendWillRenderCanvases`(3.4%, TMP), `scrFloor.LateUpdate`(2.7%), `scrVfxPlus.Update`(1.8%), `Rewired.InputManager_Base.Update`(1.2%), UMM `UI.Update`(1.16%), `EventSystem.Update`(1.1%), `scrHUDText.Update`, `PlanetSprite.LateUpdate`, `SteamController.Update`, `scnEditor.Update`(게임 화면인데 왜 도는지), `StutterFix.PerfOverlay.LateUpdate`(우리 것, 0.62%) — 마다:
   - 설치된 게임 DLL(`D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\A Dance of Fire and Ice_Data\Managed\Assembly-CSharp.dll`)을 디컴파일해(ilspy 또는 tools/ilscan) 매 프레임 하는 일을 적고, 표본의 내부 분포(stacktree 의 소유자·안쪽 호출)와 맞춘다.
   - 결과가 프레임마다 같은데 다시 계산하는 일, 화면 밖/꺼진 대상에 하는 일, 이미 우리 패치가 덮는 부분과 겹치는 일을 찾는다. 각 후보에 "결과가 같다"는 불변 조건과 깨지는 경우를 적는다.
   - 표로: 진입점, 프레임당 추정 ms(표본 % × 프레임 4.35ms), 후보, 불변 조건, 위험, 기대 이득.
2. 상위 3개 후보에 AUTOTEST 전용 상한 플래그를 만든다(예: `scriptskip <이름> on|off` 로 그 일을 통째로 건너뛰기 — 화면이 달라져도 되는 상한 측정용). bench_v14.py CFG2 에 해당 항목 추가(rlNR 기반). 실행은 Claude.
3. 상한이 큰 후보 하나는 정확한 버전(같은 결과 보장)까지 구현하고, 결과 대조 명령(원래 함수와 새 함수의 결과를 같은 프레임에 비교, 불일치 수 로그)을 붙인다. 제품 설정은 기본 꺼짐.

## 막히면
틀에 얽매이지 말고 다른 길도 시도(예: 게임 Update 를 우리 루프 하나로 합치기, 이벤트 기반으로 바꾸기, Unity 메시지 호출 비용 자체 줄이기). 확인/미확인을 구분해 기록.
