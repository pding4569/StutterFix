# Codex 작업: 오늘(2026-10-18) 변경의 동일성 검토 (게임 실행 금지)

Claude 가 지금 게임으로 측정 중이다. **게임을 켜거나, Mods 폴더에 설치하거나, sf_run 등을 부르지 말 것.** 코드 읽기·IL 확인(tools/ilscan, ILSpy)·오프라인 빌드(`dotnet build -c Debug -p:Edition=Player` 출력은 bin/ 에만)만 한다. git commit 은 하지 말고 결과를 파일로 남긴다.

## 대상 커밋 (codex/optimization-36, 오늘 것)
`git log --oneline 83facdf..HEAD` 로 본다. 특히:
1. `RecolorDefer.cs` + `TileCull.cs` 연결: 화면 밖 타일의 UpdateAngle/SetTrackStyle/ColorFloor 엔진 호출 미루기, 드레인, Uncull/Pin/StopAll 적용, PinComp 예외, ScriptPrefix(Update/LateUpdate 에서 관리 빼기 대신 미룬 것만 적용).
2. `TileCull.cs` 발광(topGlow) 렌더러도 끄기 (culled 사전에 -(i+1)).
3. `FloorLateFix.cs`: scrFloor.LateUpdate 의 isTweening 용 위치·크기 읽기 4곳을 multiplanetLine == null 이면 기본값.
4. `FloorAnim.cs` OpaOnly 경로 (길이 0/길이 있음).
5. `LazySprite.cs` + `FastMove.ImageBlock` + `InvisibleSkip`: 투명 장식 SetSprite 미루기, 먼저 적용하는 15곳, 옛 방식 타일링 갱신 예외.
6. `DecoAnim.cs`: 보이는 이미지 장식의 회전·크기 값만 저장(MoveApply.DeferTransform), 위치 X·Y 짝 합치기(StepPair).
7. `MoveApply.cs` HashSet 비우기.

## 할 일
각 변경마다 "원래 게임 코드와 결과가 달라질 수 있는 경로"를 찾아라. 예:
- 미룬 상태(타일 색·스타일, 장식 스프라이트)를 그 사이에 읽는 게임 코드나 다른 모드 코드가 있는가 (IL 로 호출처 전수). 빠진 함수가 있으면 이름과 근거.
- 순서가 바뀌어 값이 달라지는 경우 (예: StepPair 에서 X 와 Y 사이에 다른 코드가 pivotPosVec 을 읽는 경우, Startup/완료 처리 순서, 예외 처리 차이).
- 다시 하기/곡 끝/모드 끄기/편집기 전환에서 미룬 것이 남는 경로.
- Unity 객체 null/파괴 처리, 스레드 문제.
찾은 것마다: 파일·줄, 게임 메서드(IL 위치), 실제로 일어날 수 있는 조건, 심각도(화면이 달라짐 / 상태만 다름 / 이론상), 고치는 방법. 문제 없음이면 왜 없는지 근거.

결과는 `docs/handoff/codex-review-20261018-result.md` 에 한국어로. 추측은 "추정"이라고 표시.
