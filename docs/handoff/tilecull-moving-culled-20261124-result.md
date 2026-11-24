# TileCull: 꺼진 채 이동하는 타일 구현 결과 (2026-11-24)

요청 문서: [tilecull-moving-culled-20261124.md](tilecull-moving-culled-20261124.md).

구현과 오프라인 검증을 완료했다. **실제 게임의 픽셀 일치·끊김 감소는 아직 검증하지 않았다.** 코드의 `TileCull.LazyMovingUncull` 기본값은 요청대로 `true`이며, 배포 기본값 채택 여부는 Claude의 게임 대조·측정 후 결정해야 한다.

작업 위치는 `E:\StutterFixWork\wt-v14prod`, 브랜치는 `claude/v3-282port`, 시작/종료 HEAD는 `a53062846bf067812fca57382b230911ad8a8b03`이다. 시작 시 추적 파일 수정은 없었고, 기존의 많은 미추적 자료는 유지했다. 게임 실행, 설치 폴더 쓰기, 설치 DLL 교체, 커밋, 푸시, 스테이징을 하지 않았다. 디컴파일 자료는 `E:\StutterFixWork\decompiled-20261124\Assembly-CSharp`에서 읽기만 했다.

## 바꾼 파일

| 파일 | 변경 |
|---|---|
| [TileCull.cs](../../TileCull.cs) | 상태 4, 이동 Pin의 지연 복원, 매 프레임/메인 카메라 컬링 직전 검사, 정지 후 격자 복귀, 복원/색 적용 재진입 처리, 계수, AUTOTEST 가시성 검사, culltest 복원 보강 |
| [TileCullMovingBounds.cs](../../TileCullMovingBounds.cs) | 현재 local bounds와 전체 부모 변환을 이용하는 보수적 월드 AABB 계산 |
| [AutoTest.cs](../../AutoTest.cs) | `tilecull lazymove on\|off\|status`, `tilecull movingverify on\|off\|status` |
| [StutterFix.csproj](../../StutterFix.csproj) | 새 경계 계산 파일을 세 빌드에 포함 |
| [TileCullMovingCheck.csproj](../../tools/tilecull-moving-check/TileCullMovingCheck.csproj), [Program.cs](../../tools/tilecull-moving-check/Program.cs), [Stubs.cs](../../tools/tilecull-moving-check/Stubs.cs) | 실제 TileCull 소스를 링크한 오프라인 상태/경계 회귀 시험. Unity/Harmony는 대체 객체 |

## 상태 기계와 기존 경로 확인

| st | 의미 | 격자 등록 | `culled` 사전 |
|---|---|---|---|
| 0 | 기존 관리 대상, 렌더러를 TileCull이 끄지 않은 상태 | 있음 | 없음 |
| 1 | 기존 화면 밖 정지 타일 | 있음 | 본체 및 TileCull이 끈 발광 |
| 2 | 관리 밖: 움직임/형상 변경 또는 지나치게 큰 타일 | 없음 | 없음 |
| 3 | 대상 아님/소멸 | 없음 | 없음 |
| **4** | **화면 밖에서 꺼진 채 움직이는 타일** | **없음** | **상태 1 때의 본체/발광 항목 유지** |

`cover`는 켜진 격자 칸이 해당 타일을 덮는 횟수다. `Move`는 새로 들어오는 칸을 먼저 켜고 나가는 칸을 끈다. 타일은 겹치는 여러 칸에 등록되며 `cover == 0`일 때만 끈다. `st > 1`은 기존 `Move` 처리에서 제외된다.

`PinArg`는 `ffxMoveFloorPlus.TweenFloor*`와 `FloorAnim`의 위치/회전/크기 변경 알림이다. 여기서만 `Pin(i, moving: true)`를 호출한다. 상태 1이면 격자에서 빼고 상태 4로 옮기며, 렌더러를 켜거나 밀린 색을 일괄 적용하지 않는다. 이미 상태 4인 중복 이동 알림은 `quiet`만 초기화한다. 이미 보이거나 pinned인 타일은 기존처럼 처리한다.

`PinSelf`(직접 회전·리셋), `PinComp`(메시 생성/갱신), `PinGo`, `PinFx`(등장·사라짐/이전 타일)는 즉시 복원하는 기존 Pin 경로를 유지한다. 상태 4에 이런 알림이 오면 바로 켜고 상태 2로 옮긴다. 이동 효과가 모양이 아닌 transform만 바꾼다는 범위에서 지연을 허용한다.

`ScriptPrefix`는 기본적으로 밀린 색을 먼저 적용한다. `scrFloor.Update/LateUpdate`를 읽어 색·불투명도·아이콘·발광 갱신 및 다중 행성 선 추적을 확인했다. 기본 스크립트 경로에서 매번 본체를 강제 복원하지 않는다. 기존 비교용 `PinOnScript=true`이면 상태 4도 즉시 복원한다. `IsCulled`는 상태 1과 4를 모두 반환하므로 `RecolorDefer`의 미루기/소비자 Flush가 계속 작동한다.

`Uncull`은 본체를 켜기 전에 `RecolorDefer.FlushFloor`를 호출하고, `want/wantGlow`에 저장된 게임의 enabled 의도를 복원한다. Flush 중 메시 Pin이 재진입하면 다시 Flush하지 않고 별도 목록에 기록한다. 격자 순회가 끝난 뒤 그 Pin을 처리해 형상이 바뀐 타일을 기존 격자에서 빼며, 순회 중 컬렉션 변경도 피한다. `RecolorDefer.InMeshUpdate`에서 상태 1을 유지하는 기존 예외는 그대로지만, 상태 4의 메시 변경은 즉시 복원한다.

`Requalify`는 기존과 같이 **30프레임마다 3회 연속** 변화가 없으면 재등록한다. 이는 프레임 기준이며 1초 고정 타이머가 아니다. 상태 4는 현재 본체+발광 경계로 렌더러를 켰다가 다시 끄는 과정 없이 상태 1로 복귀한다. 부모/자식 발광 경계가 바뀌어도 정지 횟수를 초기화한다. 너무 크거나 격자 정수 범위를 벗어난 경계는 켜고 상태 2로 남긴다.

`Tick`의 PlayerLoop 누락 감지는 기존처럼 전부 복원하고 중지한다. `StopAll`, `PauseHook/ResumeHook`(MakeLevel/ResetScene), 편집 복귀, 장면 전환, 비활성화/Shutdown은 상태 4의 사전 항목도 복원·정리한다. 새 컬링 직전 이벤트는 Shutdown에서 해제한다. 곡이 끝나도 같은 목록이면 기존 Step이 계속 돌기 때문에 `Hitch.AfterSong` 결과 화면의 FloorAnim 이동도 검사한다. 곡 끝에서 대량 복원하던 지연을 다시 도입하지 않았다.

## 같은 그림을 지키는 방식과 근거 범위

1. `InstallLoop`는 기존과 같이 `PostLateUpdate` 첫 항목에 Step을 삽입한다. 따라서 일반 LateUpdate 뒤이며, 기존 그리기 단계 앞이다. Step은 `Move → 이동 경계 검사 → 밀린 메시 처리 → 정지 재등록` 순서다. 나머지 PostLateUpdate 단계에서 생긴 변경도 잡기 위해 **메인 카메라 `Camera.onPreCull`에서 다시 검사**한다. 이 배치는 소스에서 확인했으며 실제 게임 실행으로 PlayerLoop 순서를 추적하지는 않았다.
2. 꺼진 렌더러의 이전 월드 `bounds`만 사용하지 않는다. 매 검사에서 `Renderer.localBounds`와 캐시한 Transform의 **현재 `localToWorldMatrix`**를 읽고, `월드 중심 = M × 로컬 중심`, `월드 extents = abs(M의 선형부) × 로컬 extents`로 계산한다. 회전·음수/큰/비균일 scale·부모 변환·shear를 포함한다. 작은 바깥쪽 오차 여유도 더한다.
3. TileCull이 끈 자식 발광도 별도의 현재 변환/크기로 계산해 본체 경계와 합친다. 발광 알파나 게임의 현재 enabled 의도 때문에 경계를 줄이지 않는다.
4. 기존 반경 `abs(orthographicSize) × sqrt(1+aspect²) × 1.25 + 3`의 격자 전체 칸을 포함한다. 현재 카메라의 같은 여유 범위도 합친다. 여기에 실제 카메라 frustum과 8 월드 단위씩 넓힌 타일 AABB가 겹치는 경우도 복원한다. 따라서 회전·기울임·perspective/비표준 projection은 기존 XY 사각형 밖이어도 추가로 보호한다.
5. NaN/무한대, 잘못된 frustum, 소멸한 renderer/transform, 불확실한 경계는 숨긴 채 두지 않고 복원한다. 초대형 좌표의 격자 정수 변환/루프 overflow도 복원한다.
6. 게임이 의도적으로 끈 enabled 값은 계속 false로 보존한다. 새 상태는 타일/발광의 색·순서·충돌을 바꾸지 않는다.

위는 구현 근거와 오프라인 회귀 증거다. 실제 Unity의 disabled-renderer local bounds 갱신, 가시성 콜백의 타이밍, 제3자 모드의 더 늦은 카메라/transform 변경, 추가 카메라에 대해서는 게임 검증이 필요하다. 기존 격자 컬링 전체의 미확인 동작을 이번 변경으로 검증했다고 주장하지 않는다.

## 계수와 검사 명령

AUTOTEST 빌드에서:

```text
tilecull lazymove on
tilecull lazymove off
tilecull lazymove status
tilecull movingverify on
tilecull movingverify status
tilecull movingverify off
culltest
```

`lazymove` 상태의 의미:

- `pending`: 현재 상태 4인 타일 수.
- `deferred`: 상태 1→4 진입 횟수(고유 타일 수가 아니라 이동 관리 구간 수).
- `laterUnculled`: 상태 4에서 화면 접근/불확실성/다른 Pin/off/StopAll 때문에 복원한 횟수. 실제 enabled=true 쓰기를 하지 않은 `want=false` 복원도 포함한다.
- `endedHidden`: 이동 정지가 확인되어 **켜지 않은 채 격자로 돌아간** 구간 수. 그 뒤 일반 격자 관리에서 켜지는 것은 이 계수를 바꾸지 않는다.
- 새 Build에서 초기화되며 StopAll 뒤에는 마지막 수치를 남긴다. `deferred = laterUnculled + endedHidden + pending`을 오프라인 시험에서 확인했다. `off`는 pending을 즉시 기존 pinned 상태로 복원한다.

`movingverify on`은 계수를 초기화하고 메인 카메라 그리기 직전마다 production 경계 계산과 별개로 **실제 `Renderer.bounds`와 카메라 frustum**을 비교한다. 게임이 켜려는 본체/발광 중 실제로 꺼져 있고 frustum과 겹치는 항목을 센다. `frames`는 검사 콜백 횟수, `checkedTiles`는 검사한 상태 4 타일의 누계, `last`는 직전 본체+발광 위반 수, `badFrames/tileHits/glowHits`는 누적 위반이다. 위반 첫 10회는 프레임 번호와 함께 로그를 남긴다. `off`는 계수를 보존한다. **frames > 0, checkedTiles > 0이고 위반 계수 전부 0**이어야 유효한 음성 결과다. 검사 비용은 성능 측정에서 제외한다.

`culltest`는 상태 4도 `culled` 사전에 유지되므로 본체와 발광을 대조군에서 함께 켠다. 로그에 실제 포함된 `꺼진 채 이동 타일 N개`를 추가했다. 양쪽 캡처 전에 밀린 색/메시를 맞추며, 필터를 끄고 타일/발광 층을 비교한다. 비교 중에는 자동 복원/검사 콜백을 잠깐 건너뛰어 같은 상태를 유지한다. 캡처가 실패해도 임시 enabled, 카메라 마스크/targetTexture, 이전 RenderTexture.active를 finally에서 되돌린다. **픽셀 차이 0, 같은 상태 대조군 차이 0, 이동 타일 N > 0**을 함께 확인해야 새 집단의 유효한 검사다. 색 적용이 메시 Pin을 유발하면 일부 타일이 먼저 복원될 수 있으므로 N=0 결과를 성공 증거로 쓰면 안 된다.

## 오프라인 검증 결과

| 명령 | 결과 | 로그 |
|---|---|---|
| `dotnet build StutterFix.csproj -c Release -p:Edition=Player` | 오류 0, 경고 69 | [player-build.txt](../../out/tilecull-moving-20261124/player-build.txt) |
| `dotnet build StutterFix.csproj -c Release -p:Edition=Debug` | 오류 0, 경고 89 | [debug-build.txt](../../out/tilecull-moving-20261124/debug-build.txt) |
| `dotnet build StutterFix.csproj -c Release -p:Edition=Player -p:AutoTestBuild=1` | 오류 0, 경고 66 | [autotest-build.txt](../../out/tilecull-moving-20261124/autotest-build.txt) |
| `dotnet run --project tools/tilecull-moving-check/TileCullMovingCheck.csproj -c Release` | **18,352개 확인 통과**, 무작위 affine 변환 2,000개 | [offline-check.txt](../../out/tilecull-moving-20261124/offline-check.txt) |
| LoadCheck Player / Debug / AUTOTEST | FieldRefAccess 고정 이름 258 / 320 / 321곳, 틀림 전부 0 | [Player](../../out/tilecull-moving-20261124/player-loadcheck.txt), [Debug](../../out/tilecull-moving-20261124/debug-loadcheck.txt), [AUTOTEST](../../out/tilecull-moving-20261124/autotest-loadcheck.txt) |
| `git diff --check` | 공백 오류 없음 | 최종 점검 |

LoadCheck의 실행 중 이름을 결정하는 7 / 7 / 8곳은 여전히 런타임 확인 대상이다. 빌드는 경고 0이 아니다. 오프라인 시험은 실제 소스의 Step/Pin/Requalify/StopAll/RenderCompare를 실행하지만 Unity/Harmony를 대체하므로 실제 렌더링과 native 호출 비용은 검증하지 않는다. 시험의 가짜 픽셀은 픽셀 일치 증거가 아니다.

저장소 전체 `py -3 tools/check-ui-encoding.py`는 기존 미추적 자료 `out/runlod-f-curve-api.cs`의 UTF-16 내용에서 UTF-8 디코딩 오류로 중단된다. csproj는 이 후처리를 `IgnoreExitCode=true`로 실행하므로 세 빌드의 오류 0과 별개의 제한이다. 기존 자료를 고치지 않았으며, 이번 변경 파일 8개의 UTF-8 디코딩/대체문자/SFENC001 문자열 검사는 별도로 통과했다: [검사 결과](../../out/tilecull-moving-20261124/changed-encoding-check.txt), [재현 스크립트](../../out/tilecull-moving-20261124/validate-text.py).

최종 AUTOTEST DLL의 SHA-256은 `6F049D5F20721D272D1DF7D21651BB09BF0B2828D6366D3030F6CC3DED3DE79F`이다. 빌드 산출물은 워크트리의 bin에만 만들었다.

상태 시험은 본체/발광 지연·화면 접근 복원, enabled 의도 보존, 색 Flush 순서, 재진입 메시 Pin, 중복 이동 및 목록 교환 제거, 스위치 off, 정지 후 꺼진 격자 복귀, 큰 경계/극단 좌표/NaN, 발광만 화면 접근, 오래된 world bounds, 결과 화면, 장면/타일 목록 교체, Pause/Stop/루프 누락, Step 뒤 이동의 컬링 직전 복원, 가시성 검사 양성 대조, culltest 예외 복원을 포함한다. affine 시험은 실제 경계 helper가 무작위 3×4 변환을 적용한 로컬 상자의 꼭짓점 8개를 모두 감싸는지 검사했다.

## Claude가 실행할 게임 검증

이 절의 명령은 **작성만 했으며 실행하지 않았다.** AUTOTEST DLL은 `bin/PlayerAuto/StutterFix.dll`이다. 설치·게임 운영·기존 원본 복원은 Claude의 측정 절차로 수행한다.

PLUM 530.75초 근처의 에디터 시각 대조 예시(`open`에는 실제 차트 경로 사용):

```text
open <PLUM MEGAMIX 차트 경로>
auto on
tilecull on
tilecull lazymove on
tilecull movingverify on
seek 530 12
waitsongpos 530.8 60
pause
tilecull lazymove status
culltest
tilecull movingverify status
resume
waitsongpos 534 30
tilecull movingverify status
tilecull movingverify off
stop
```

Lost Requiem은 같은 절차에서 차트와 `seek 528 12`, `waitsongpos 528.45 60`, 마지막 `waitsongpos 532 30`으로 바꾼다. 효과 직전/직후, 이동 도중, 카메라 접근, scale/회전 최대 지점의 culltest를 추가한다. 대조군/후보와 같은 시각·장면을 사용한다. seek는 상태 재현이 자연 진행과 같다는 보장이 없으므로 원래 dropscan 게임 모드에서도 확인해야 한다.

PLUM 596.17초는 **결과 화면** 재현을 별도로 해야 한다. 에디터 seek만으로 대체하지 않는다. 기존 `game <차트 경로>` / `auto on` / `press` 재현 절차로 게임 모드 588초 이전까지 진행한 뒤 다음 구간을 사용한다:

```text
tilecull lazymove on
tilecull movingverify on
waitsongpos 590 30
wait 7
pause
tilecull lazymove status
culltest
tilecull movingverify status
resume
wait 3
tilecull movingverify status
tilecull movingverify off
```

실제 로그 시각이 596.17초 결과 화면 이동을 포함했는지 확인한다. 결과 화면에서는 Hitch.Playing이 false일 수 있으므로 곡 끝 이후에 `waitsongpos`를 추가하지 않는다. 시간 정렬이 다르면 590초 이후 wait만 조정한다. 처음부터 진행할 때 하나의 wait에 600초를 넣지 말고 기존 단계 제한 안에서 여러 시각 대기를 사용한다. 이 창에서 retry/다른 맵/장면 전환/나가기도 수행해 숨은 렌더러가 남지 않는지 확인한다.

성능은 위 시각 대조와 별도 판으로 **A=lazy off, B=lazy on, B, A** 순서로 측정한다. TileCull 자체는 네 판 모두 on이다. 스위치는 각 판 진입 전에 바꾸고, off 명령의 즉시 복원 프레임과 준비 비용은 측정 창에서 제외한다. PLUM 530초와 Lost 528초는 효과 전후를 포함한 약 10초 창, PLUM 결과 화면은 590초~결과 연출 뒤의 같은 창을 사용한다. 예시:

```text
tilecull movingverify off
tilecull lazymove off
# 기존 재현 절차로 효과 전 측정 창에 진입
dropscan begin
wait 10
dropscan end
tilecull lazymove status
```

B에서는 `lazymove on`으로 바꾸고 같은 재현을 반복한다. `dropscan-frames.csv`와 meta/log를 판별 이름으로 각각 보존한다(다음 end가 덮어쓴다). 평균뿐 아니라 대상 효과 프레임, p95/p99/최대, 16.7/25/33ms 이상 프레임 수를 비교한다. renderer 활성화가 줄어도 두 번의 O(N) 검사 비용이 더 크면 채택 근거가 없다.

병목 확인이 필요하면 별도 진단 판에서 `profsamp begin 1000 native ring`과 `dropscan begin`을 함께 시작하고 같은 창 뒤 `dropscan end`, `profsamp end frames`로 보관한다. `TileCull.Uncull/Pin`, `RendererScene::AddRenderer` 표본 감소와 새 `CheckMoving/CurrentMovingBounds` 비용을 비교한다. 스레드 정지 샘플러가 개입한 진단 판을 무계측 성능으로 해석하지 않는다.

최종 채택 전에는 색 변경과 겹친 이동, 큰/음수 scale·회전·부모 변환, 카메라 회전/음수 줌/줌아웃, 재시작·실패·완주·결과 화면·모드 해제도 확인한다. 가시성 위반이나 유효한 culltest 픽셀 차이가 하나라도 있으면 통과로 처리하지 않는다.

## 남은 위험과 다음 판단

상태 4 검사 자체는 조밀한 목록에 대해 O(N)이며 프레임마다 목록·frustum 배열을 만들지 않는다. Transform은 Build에서 캐시하고, 타일당 각 검사에서 본체 local bounds/행렬을 읽으며 꺼둔 발광이 있으면 그것도 읽는다. 일반적으로 Step과 메인 카메라 컬링 직전 두 번 검사하므로 **실제 native 읽기 비용과 결과 화면에서의 수천 타일 유지 비용은 미측정**이다. Requalify의 기존 전체 타일 순회는 30프레임마다 유지한다.

보수적인 여유/발광 경계 때문에 예상보다 일찍 켜지거나 절약이 작을 수 있다. 실게임에서 local bounds가 예상대로 최신인지와 가시성 콜백에 의존한 스크립트의 차이도 확인해야 한다. culltest는 필터를 끈 순간의 타일/발광 층 검증이며 자연 전곡의 모든 효과를 증명하지 않는다. 현재 결과는 **구현·세 빌드·오프라인 회귀 통과**, 게임 그림/성능/배포 채택은 **미검증**이다.

## Claude 게임 확인 (2026-11-24)
- 대조(PLUM 529~533초, 곡 끝 결과 화면): culltest 픽셀 차이 0(꺼진 채 이동 3,768·4,865개 포함), movingverify badFrames 0(3,242프레임, 타일 검사 83만 번).
- ABBA(끔/켬/켬/끔, PLUM 525초~끝): 530.75초 19.1/40.1/34.3/18.4ms, 곡 끝 36.6/59.5/46.4/34.0ms, 평균 프레임 2.923/3.155/3.125/2.904ms → 켬이 약 7% 느리고 끊김도 커짐(매 프레임 수천 타일 경계 재계산 비용이 렌더러 켜기보다 큼).
- 판정: 기각. 코드는 patches/tilecull-lazymove-loss 에 보관, 작업 트리에서 되돌림.
