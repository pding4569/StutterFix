# decofreeze 구현 결과 (2026-10-21)

작업 위치: `E:\StutterFixWork\wt-freeze`, 브랜치 `codex/night-freeze`.
구현·오프라인 검증 완료. **게임 실행·설치·커밋·푸시 0회. 실제 FPS·절감 ms는 미측정**이다.

## 읽은 근거와 변경 범위

- `docs/handoff/codex-freeze-20261021.md`, `docs/optimization-36.md`의 비용 분해 및 끝 절들을 읽었다.
- 이 워크트리에는 `docs/ROADMAP-EXTREME-20261021.md`가 없어, `E:\StutterFixWork\StutterFix\docs\ROADMAP-EXTREME-20261021.md`를 읽기만 했다.
- 기존 약 8.0ms/활성 장식 5.7ms/엔진 잔여 약 2.5ms는 인계된 배경 수치다. 이번 작업으로 다시 측정하거나 확정하지 않았다.
- 새 파일: `DecoFreeze.cs`, `tools/optimization/decofreeze-steps.py`, `tools/optimization/test-decofreeze-steps.py`, `tools/decofreeze-fixture/{DecoFreezeFixture.csproj,Program.cs}`.
- 연결부: `AutoTest.cs`, `DecoAnim.cs`, `TightLate.cs`, `StutterFix.cs`, `StutterFix.csproj`. `.codex/config.toml`은 수정하지 않았다.

## 명령과 정지 범위

`decofreeze off|pos|anim|both|state`, 추가 집계 명령 `reset|summary`.
기본은 off이며 설정·표식 파일로 켜지지 않는다. Harmony 소유자는 `StutterFix.decofreeze`다.

| 모드 | 동작 |
|---|---|
| off | 원래 경로. 설치된 진단 Prefix는 통과한다. 모든 표본에서 동일한 패치 체인을 유지한다. |
| pos | 정확히 `scrVisualDecoration`인 이미지 장식의 UpdatePosition 및 아래 쓰기 경로를 생략. 위치·피벗·회전·크기 값 저장은 유지한다. 렌더러 활성·색·그리기 설정은 바꾸지 않는다. |
| anim | `DecoAnim.UpdatePrefix` 진입에서 돌아가 해당 프레임의 장식 채널 평가를 생략한다. batch/group/native/DecoEngine 분기 전에 적용된다. LateUpdate/UpdatePosition은 그대로 돈다. |
| both | pos와 anim을 함께 적용. |
| state | 모드, 패치 설치 여부, 측정 여부, 누적 생략 수와 프레임당 수를 기록. |
| reset | off로 전환, 패치 설치, 표본 초기화, 다음 fuse 창의 집계를 활성화. |
| summary | 유효 표본만 모드별 평균으로 한 줄 출력한 뒤 off로 전환하고 집계를 종료. |

재생 중이며 `!SceneReset.Resetting`일 때만 정지한다. 편집/되감기 초기화 중에는 원래 경로를 통과한다.
pos는 다른 장식 형식과 알 수 없는 파생 형식을 통과시킨다. anim은 DecoAnim의 장식 갱신 전체를 멈춘다.
효과 시작 함수, 타일/FloorAnim, 행성은 패치하지 않는다. 효과 시작·Kill/Complete는 계속 실행되므로 anim 중에도 새 효과의 시작·완료가 값을 바꿀 수 있다.

## 호출 경로: 디컴파일 + 설치 DLL의 IL 교차확인

게임을 켜지 않고 `tools/ilscan`으로 설치된 Managed/Assembly-CSharp.dll의 `scrDecoration`, `scrParallax`를 읽었다.
원시 기록은 `tools/optimization/out/decofreeze/out-freeze-game-decoration-il.txt`와 `out-freeze-game-parallax-il.txt`에 있다(로컬 출력, Git 제외).

1. `scrDecorationManager.LateUpdate → LogicUpdate → scrDecoration.UpdatePosition`.
2. `UpdatePosition → pivotTrans.localPosition`, `scrParallax.SetTrans → cachedTransform.position`, `SetRotation → pivotTrans.rotation`, `SetScale → pivotTrans.localScale`.
3. `SetPositionX/Y`, `SetPivotX/Y → SetPosition → childTransform.localPosition / 편집기 선택 테두리 위치 → UpdateScreenClamp → UpdatePosition`.
4. `SetParallaxOffsetX/Y → scrParallax.SetTrans`; `scrParallax.LateUpdate → SetTrans`도 별도로 존재한다.
5. 기본 켬 `TightLate.List → Process → Apply`는 UpdatePosition을 우회해 position/rotation/localScale을 직접 쓴다. 적격 판정 뒤 Apply/DecoEngine.Late 직전에 생략 지점을 넣었다. 부적격 장식은 기존 LogicUpdate를 지나 UpdatePosition Prefix에 걸린다.

따라서 UpdatePosition Prefix 하나만으로는 pos 시험이 성립하지 않는다. 다음 5개를 이름·정확한 인자 형식·인스턴스 void 서명으로 선택했다:

- `scrDecoration.UpdatePosition()`
- `scrDecoration.SetPosition(Vector2, Vector2)`: 값 2개와 UpdateScreenClamp만 반영하고 Transform/편집기 피벗 쓰기 생략.
- `scrDecoration.SetRotation(float)`: rotAngle만 저장.
- `scrDecoration.SetScale(Vector2)`: scaleVec만 저장.
- `scrParallax.SetTrans()`: 소유 장식이 대상 이미지일 때만 생략.

SetPosition의 UpdateScreenClamp는 화면 고정의 논리 상태를 유지하기 위한 것이다. IL상 Transform 쓰기가 없다.
SetRotation/SetScale를 직접 부르는 채널·즉시 효과도 같은 Prefix로 막는다. 기존 MoveApply의 값만 저장하는 경로는 유지한다.
TightLate의 캐시는 모드 변경 때 무효화하며, 생략한 슬롯의 Has도 지운다.

부분 패치 설치 실패 시 이 소유자의 패치를 되돌린다. Main.Unload → UninstallAll에서 Shutdown을 부르며, AutoTest의 정상/오류/시간초과 종료에서도 Shutdown한다. Shutdown은 정지 해제·독립 Harmony 패치 제거·로그 구독 해제를 수행한다.

## 단계 생성과 측정 방식

다음은 **JSON만 만드는** 명령이다. 차트 인자는 실제 Arche FULL 경로로 바꾼다.

```powershell
py -3 tools/optimization/decofreeze-steps.py --chart 'E:\차트폴더\Arche FULL.adofai' --output tools/optimization/out/decofreeze/steps.json
```

출력: 104단계, timeout_min=20. 한 번 open한 같은 게임에서 다음 9개 창을 반복한다:

`off, pos, anim, both, off, both, anim, pos, off`

- 측정 전에 `fuse begin/end`로 기존 fuse 패치 설치 비용을 치른 뒤 `decofreeze reset`한다. 이 준비 호출은 RESULT로 집계하지 않는다.
- 각 창: `decofreeze off → stop → seek 258 8 → waitsongpos 258 40 → decofreeze <mode> → waitsongpos 262 40 → fuse begin → waitsongpos 276 40 → fuse end → decofreeze state`.
- 기존 seek는 재생 중이면 재생을 다시 시작하지 않으므로 stop이 필수다. `seek 258 8`은 250초 근처 타일에서 재생하며, 258초에 모드를 적용해 262초까지 약 4초 안정화한다.
- 마지막: off → stop → summary → quit. 생성만 했으며 이 단계들을 실제로 실행하지 않았다.
- fuse begin/end와 같은 명령에서 측정 시계·프레임 카운터를 읽는다. FPS=`Time.frameCount 차이 / Stopwatch 경과 초`, ms=`1000 / FPS`. 실제 시작·끝 곡 시각도 남긴다. PresentMon 표시 FPS가 아니라 Unity 프레임 카운터다.
- RESULT 한 줄에 mode/fps/ms/seconds/valid, 오류 수, 생략 수·프레임당 수, 해당 fuse 결과 전체를 붙인다.
- 생략 카운터 순서는 UpdatePosition / 세 setter 합 / SetTrans / TightLate 직접 적용 / DecoAnim.UpdatePrefix다. `skip_anim_update`는 채널 개수가 아닌 프레임 갱신 진입 횟수다.
- SUMMARY는 창별 ms의 산술평균, 그 역수의 FPS, `off 평균 ms - 해당 모드 평균 ms`를 기록한다. 유효 표본 수가 off=3, 나머지=2씩일 때만 complete=True다. 표본이 없으면 NaN이다.

## 오류와 유효 표본

시작은 곡 262~263초 미만, 종료는 276~277초 미만이어야 한다. DecoAnim이 활성·설치되어 있지 않으면 시작을 거부한다.
0프레임, 0시간, 재생 중단, 필요한 생략 카운터 0, 오류가 있는 창은 평균에서 제외한다.

- `decoanim_errors`: 기존 `DecoAnim.Errors` 증가분. 내부에 잡혀 로그로 나오지 않던 오류도 구분한다.
- `unity_errors`: 측정 창 안 Unity Error/Exception/Assert 메시지 수. Warning은 세지 않는다.
- 오류가 있으면 별도의 `[decofreeze] [Error] invalid_window ...` 한 줄을 남긴다. 실제 `tools/sfmeasure/server.py`의 RE_ERR를 데이터로 읽어 이 줄이 sf_errors 집계에 걸리고 정상 RESULT는 걸리지 않음을 오프라인 확인했다. 원본 오류 로그와 진단 오류 줄이 함께 잡힐 수 있으므로 sf_errors의 숫자를 고유 예외 개수로 해석하지 않는다.
- **성능 시험은 PlayerAuto DLL을 사용한다.** Dev는 기존 DOTween 검증용 shadow 계산이 남으므로 동작 점검만 가능하고, 모든 RESULT를 valid=False로 처리한다.

## 검증 결과

| 검사 | 결과 |
|---|---|
| `dotnet build -c Release` | 오류 0, 경고 38 |
| `dotnet build -c Release -p:Edition=Player` | 오류 0, 경고 36 |
| `dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1` | 오류 0, 경고 38 |
| LoadCheck Dev / Player / PlayerAuto | FieldRefAccess 268 / 229 / 268곳, 틀림 모두 0. 실행 중 결정 이름 5곳씩은 기존 검사 한계 |
| 단계 생성기 오프라인 검사 | 104단계, 9회 대칭, stop/seek/안정화/fuse 쌍, timeout, UTF-8 CLI 왕복 통과 |
| 콜백·수명·메타데이터 fixture | 30개 assertion 통과 |
| 일반 Player 메타데이터 | DecoFreeze 타입 정의·타입 참조·관련 멤버 참조·decofreeze 명령 문자열 없음 |
| Dev / PlayerAuto 메타데이터 | DecoFreeze 타입 존재 |
| `git diff --check` | 통과 |

재현:

```powershell
py -3 tools/optimization/test-decofreeze-steps.py
dotnet run --project tools/decofreeze-fixture/DecoFreezeFixture.csproj -c Release -- bin/Release/StutterFix.dll bin/Player/StutterFix.dll bin/PlayerAuto/StutterFix.dll
dotnet run --project tools/LoadCheck/LoadCheck.csproj -c Release -- bin/Player/StutterFix.dll
```

fixture는 실제 DecoFreeze.cs를 링크하여 기본 off, 이미지 한정, 값 보존, 비이미지/외부 시차 통과, 초기화/편집 통과, anim/both 분리, 잘못된 창/측정 중 변경 거부, 오류 표본 제외, 9표본 집계, Dev 제외, 해제·부분 패치 실패 복구를 검사했다. **Harmony 패치 등록과 Unity는 대체물**이다. 실제 Mono detour·델리게이트·게임 예외·그리기 상태는 아직 검증하지 않았다.

최종 DLL SHA-256:

- Dev: `DEB7536253432ED8671AF4EBBF56C1B9E1C5640D5E3883F5703F689A3D9B0CE6`
- Player: `763803356181CBA9911FD0E52E87A9002CC69A283FC8BEA712393A9CB5E055B7`
- PlayerAuto: `7F5F38519C487D39D35FF0B6A179FC48F19AA22E3C4B945A3031EA53A33A4705`

## 해석 한계와 다음 실행 담당자에게

pos는 Transform 설정자뿐 아니라 UpdatePosition의 관리 계산 및 TightLate.Apply 계산도 제거한다. off-pos 차이를 순수 엔진 비용이라고 단정하면 안 된다. anim은 채널 계산과 그에 딸린 setter/색 적용을 함께 제거하며, 시작/완료 처리는 남는다. 두 차이를 독립 항목처럼 단순 합산하지 않는다.

화면은 의도적으로 달라진다. 렌더러 활성·forceRenderingOff를 변경하지 않지만, 자세·겹침·가려짐·색이 달라진 결과로 GPU/컬링 부담도 바뀔 수 있다. 효과 시작이 계속되므로 시작 순간의 모든 상태를 완전히 고정하는 도구도 아니다. 임의 효과·플러그인의 다른 Transform 쓰기까지 전역 차단하지 않는다.

다음 담당자는 PlayerAuto로 생성 JSON을 실행하고 9개의 RESULT와 complete=True인 SUMMARY, 필요한 생략 수, sf_errors를 함께 확인해야 한다. 유효하지 않은 창은 원인을 보존하고 재시험한다. **현재는 성능 가설의 참/거짓이나 livedeco 이득을 판단할 실게임 수치가 없다.**
