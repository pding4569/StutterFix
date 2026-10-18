# Codex 작업: 장식 위치 갱신 합치기(FuseLate) 구현 — 게임 실행·설치·커밋 금지

Claude 가 게임 측정을 맡는다. 너는 코드 조사·구현·오프라인 빌드만 한다. Mods 폴더에 복사하지 말고, git commit 하지 말 것.
빌드: `dotnet build -c Debug -p:Edition=Player -p:AutoTestBuild=1` (bin/PlayerAuto), `dotnet build -c Debug -p:Edition=Player` (bin/Player), `dotnet build -c Debug` (개발자용). 세 개 모두 오류 0 이어야 한다.
결과 보고는 `docs/handoff/codex-fuselate-20261018-result.md` (한국어): 바꾼 파일·줄, 동일성 근거(IL), 남은 위험, Claude 가 게임에서 확인할 것.

## 배경 (측정, Arche 곡 260~276초, 프레임당)

새 자동 시험 명령 `fuse begin/end`, `decostruct` (FuseProbe.cs) 결과:
- 메인 10.2ms 중 모드 장식 애니메이션 갱신 2.78ms + `scrDecorationManager.LateUpdate` 2.10ms. 장식 이동 효과 시작은 0.20ms 뿐(같은 장식을 계속 다시 시작하지만 싸다).
- 애니메이션 도는 장식 1,970개: 전부 scrVisualDecoration, pivotTrans == 자기 Transform == parallax.transform, 시차 컴포넌트 enabled=false, 화면 고정 15, 행성 따라감/타일 붙음/회전 카메라 고정 0, 부모 원점·회전 없음·크기 1. 1,550개가 위치X+Y+회전+크기X+Y+색 동시.

장식 하나의 LateUpdate (scrDecoration.LogicUpdate → UpdatePosition) 는 엔진 호출이 6번 이상이다:
```
UpdatePosition(): vector = (!paused && followPlanet != null) ? followPlanet.transform.position : zero
  vector2 = pivotPosVec + vector; if stickToFloor ...
  pivotTrans.localPosition = vector2;            // 엔진 쓰기 (곧 아래에서 덮임)
  parallax.SetNewStartPosition(vector2); parallax.posCamAtStart = vector2;
  if (customLevel != null || parallax.enabled) parallax.SetTrans();
     // SetTrans: Init(); position = cachedTransform.position (엔진 읽기); (clamp 아니면) v = (cameraTransform.position(엔진 읽기) - posCamAtStart) * multiplier + startPosition;
     //  dontAlterX/Y; vector3 = isDecoration ? parallaxOffset * camScaleMultiplier / textureScaleMultiplier : 0; endPosition = v + vector3;
     //  cachedTransform.position = (end.x, end.y, position.z)   (엔진 쓰기)
  SetRotation(rotAngle): num = startRot + angle (+stick/lock 갈래); pivotTrans.rotation = Euler(0,0,num) (엔진 쓰기)
  SetScale(scaleVec): scaleVec = scale; pivotTrans.localScale = (scale * camScaleMultiplier * (stick ? floor scale : one)).WithZ(1) (엔진 쓰기)
  if stickToFloor ApplyColor()
```
(전체 원문은 ILSpy 로 직접 확인: scrDecoration.UpdatePosition/SetRotation/SetScale, scrParallax.SetTrans/Init/SetNewStartPosition. scrVisualDecoration 이 SetScale 을 override 하는지 반드시 확인.)

## 할 일: 같은 결과를 엔진 호출 2번(+프레임당 카메라 위치 1번)으로

`scrDecoration.UpdatePosition()` 앞에 prefix (새 파일 `FuseLate.cs`, 설정 `FuseLate` 기본 true, 개발자용/AutoTest 에서 모드 폴더 `fuselate-off` 파일로 끔 — StutterFix.cs 의 `DevPresentFlags` 의 다른 `has("...")` 줄들처럼). 조건이 모두 맞을 때만 빠른 길, 아니면 원래 함수:
- `d.GetType()` 이 scrVisualDecoration 정확히 (SetScale override 여부에 맞춰 원래와 같은 식으로)
- `ReferenceEquals(followPlanet, null)`, `!stickToFloor`, `!lockRotation` (또는 lock 은 카메라 회전을 프레임당 한 번 읽어 원래 식 그대로 — 선택)
- parallax 가 null 아님, `inited`, `cachedTransform` 이 pivotTrans 와 같은 참조, `!clampToScreen`, `!dontAlterX && !dontAlterY`, `isDecoration` 이고 `decoration` 이 d
- 부모가 정확히 원점·회전 없음·크기 (1,1,1): 장식 Transform 의 부모 참조를 장식별로 기억(처음 한 번 transform.parent), 부모마다 프레임당 한 번 position/rotation/lossyScale 을 읽어 확인. 곡 중에 장식을 다른 부모로 옮기는 게임 코드가 있는지 IL 전수로 확인하고 결과를 보고(내가 찾은 것: ffxDamageRat 만 SetParent. 장식과 관계 있는지 확인).
빠른 길에서는 원래와 같은 관리 필드 쓰기(parallax.startPosition, posCamAtStart, endPosition, d.rotAngle, d.scaleVec)와 같은 float 연산 순서(같은 C# 식을 같은 형식으로 써서 같은 IL 이 나오게)로 끝값을 계산하고:
`pivotTrans.SetPositionAndRotation(new Vector3(end.x, end.y, z), Quaternion.Euler(0f, 0f, num))` + `pivotTrans.localScale = ...` 만 한다. z 는 원래의 `position.z` (localPosition z=0 을 쓴 뒤 읽은 월드 z) — 부모가 원점이면 0 이다.
`cameraTransform.position` 은 같은 프레임 안에서 같은 카메라면 한 번만 읽어 둔다(프레임 번호 + 카메라 참조로 캐시). LateUpdate 도중 카메라가 움직이는 코드가 있는지(장식 관리자 LateUpdate 루프 안) 확인할 것.
`ADOBase.customLevel != null || parallax.enabled` 조건: customLevel 이 null 이고 parallax 가 꺼졌으면 원래는 SetTrans 를 안 부른다 → 그 경우는 원래 함수로.

## 같이 볼 것
- MoveApply.cs 의 기존 UpdatePosition 관련 가로채기(UpdateNow, SkipForLate, ManagerLatePostfix 의 개발자용 대조 `VerifyLate`)와 충돌하지 않게. VerifyLate 대조가 빠른 길 결과에도 그대로 돌게 한다.
- 개발자용(Edition.Dev 또는 모드 폴더 `fuselate-verify` 파일): 64개 중 1개는 빠른 길 뒤 원래 함수를 다시 불러 localPosition/rotation/localScale·parallax 필드를 비트 비교해 다르면 세고 첫 예를 로그. (원래 함수를 다시 부르면 결과가 같아야 하므로 부작용 없음 — 그걸 근거로 써라. 아니면 반대 순서로.)
- `Main.Unload`/UninstallAll 에서 정리(정적 캐시 비우기). Harmony 는 메인 ID 로 설치하면 기존 해제에 포함된다.
- csproj 는 Compile 목록이 명시적이다. 새 파일을 넣을 것. SettingsWindow 에 줄을 넣을 필요는 없다(실험 설정).
- `FuseProbe.Struct`/AutoTest 에 빠른 길 횟수·원래 길로 간 이유별 횟수를 출력하는 명령 `fusestate` 를 추가.

추측은 "추정"이라고 표시. 끝나면 결과 파일과 빌드 결과를 남겨라.
