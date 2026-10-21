# framecmp-4 결과 — 2026-10-21

합성 순서·숨김 대조군과 최종 PASS/FAIL 판정을 구현하고 오프라인 검증을 마쳤다. **게임 실행·설치·커밋은 하지 않았다. 새 DLL의 실제 검출 픽셀 수, 본 카메라 체인에서의 표시, 제한 시간 내 완주는 미검증이다.**

## hide가 NA였던 원인

제공된 `framecmp-run3-evidence.txt`는 다섯 번째 순서 대조 도중 RESULT를 출력하고 끝난다. 기존 로컬 `Player.log`를 읽어 같은 재질·Renderer ID와 RESULT를 확인했고, 직후에 다음 종료 사유가 있었다.

```text
[StutterFix] [자동 시험] 시간 초과 (framecmp control order) - 게임을 끕니다
```

발췌는 [framecmp-4-timeout-evidence.txt](framecmp-4-timeout-evidence.txt)에 보존했다. 이는 이번 실행 로그가 아니라 기존 3회차 로그다. 생성기의 `timeout 180`은 단계별 제한이고, 기존 순서 대조는 후보를 최대 16회 재시도했다. 첫 네 쌍은 차이가 0이었고 다섯 번째 시도 중 순서 단계의 180초 제한에 걸렸다. 따라서 **hide 자체가 실패한 것이 아니라 hide 명령에 도달하지 못했다.** 시간 초과가 재시도 상한보다 먼저 발생했다.

## 구현

- `framecmp control order`와 `framecmp control hide` 모두 실제 장식 후보 검색 대신 우리 소유의 임시 SpriteRenderer 두 개를 사용한다. 실제 장식 재시도 경로는 제거했다.
- `GpuSplit.FindMain()`의 기존 본 카메라를 사용한다. 보조 카메라 생성, `Camera.Render`, 카메라 체인·cullingMask 수정은 없다. 활성 카메라와 display 0, 지원되는 `Sprites/Tile` 셰이더, 비 UI 가시 레이어를 요구한다.
- 본 카메라 viewport의 중심 `(0.18,0.18)`, 크기 `(0.12,0.12)`에 두 사각형을 완전히 겹친다. 카메라 회전과 나란한 면으로 놓고 같은 위치·z·스케일, 같은 최상위 sortingLayer와 sortingOrder 32767을 사용한다. 기본 center 제외 범위 `(0.4..0.6,0.4..0.6)` 밖이다. 일반적인 전체 화면 합성 체인을 전제로 한 배치이며, 특수한 RT 재배치·후속 덮어쓰기까지 정적으로 보장한 것은 아니다.
- 흰색 FullRect 스프라이트, 서로 다른 재질 두 개, 불투명 빨강·파랑 Renderer 색을 사용한다. 재질은 모두 renderQueue 3000, 지원되는 `_Color`·`_RendererColor`는 white다.
- 생성된 재질 ID를 **부호 있는 int로 비교**해 작은 ID를 빨강, 큰 ID를 파랑에 배정한다. 따라서 생성 순서나 ID 증가 방향과 무관하게 기준 프레임은 작은 ID가 뒤, 큰 ID가 앞이라는 SortProbe 규칙을 만족한다. 재생성 루프는 필요 없다.
- order는 재질 두 개만 서로 교환한다. Renderer 색·sortingLayer·sortingOrder·transform은 유지하므로 순서 변화에 따라 겹친 영역의 색이 달라져야 한다. hide는 기준 상태에서 앞쪽 파랑의 `forceRenderingOff=true`로 뒤의 빨강을 드러낸다.
- 각 대조는 **최대 1시도·4캡처**다: 합성 객체가 있는 A/N 잡음 바닥 → 변경 B → 복원 A2. 기존 GPU readback별 15초 제한은 유지한다. 단계별 180초 제한을 늘리는 대신 반복 캡처를 없앴다. 시작·정리 로그에 캡처 상한과 실제 경과 시간을 남긴다.
- 잡음 바닥은 밖 픽셀 0과 상태 안정성을 요구한다. 비영 잡음·상태 불안정은 `INCONCLUSIVE`와 최종 FAIL로 남기고 다음 명령으로 진행한다. 변경 검출 0 또는 복원 실패도 숫자와 FAIL로 보고하고 hide 대조까지 진행한다. 실제 캡처·GPU·설정 예외는 기존 오류 경로를 유지한다.
- 정상·부분 생성 실패·캡처 오류·stop·unload 시 소유 객체 정리를 등록했다. Renderer를 즉시 끄고 GameObject를 비활성화한 뒤 객체·재질·Sprite·Texture를 모두 Destroy한다. 정상 완료 시 한 프레임을 양보해 지연 파괴가 끝나게 한다. A/A2 복원 검사는 합성 쌍이 살아 있는 상태의 픽셀 0과 Renderer 상태 동일성을 확인한다.

## RESULT와 VERDICT

명령 형식과 29단계 JSON은 유지한다. 마지막 결과 형식은 다음과 같다. **아래는 형식 설명이며 이번 실측값이 아니다.**

```text
framecmp RESULT noise_out=<픽셀 수> aa2_out=<픽셀 수> ab_out=<픽셀 수> ctl_order=<픽셀 수> ctl_hide=<픽셀 수>
framecmp VERDICT noise=<PASS/FAIL> aa2=<PASS/FAIL> order_ctl=<PASS/FAIL> hide_ctl=<PASS/FAIL>
```

noise·aa2는 밖 픽셀 0과 상태 안정성일 때 PASS다. order_ctl·hide_ctl은 안정된 잡음 바닥, 변경 픽셀 양수, 복원 픽셀 0, 복원 상태 동일성을 모두 만족해야 PASS다. 0인 양성 대조는 FAIL이다. 미실행 `NA`, 잡음 때문에 판정 불가인 `INCONCLUSIVE`도 FAIL이다. 정상적으로 비교가 완료되면 ctl 값은 실제 변경 픽셀 수를 숫자로 기록한다. 예외·중단 때 값을 만들어 채우지 않는다. 기존 RESULT 중복 방지와 최초 noise 결과 보존은 유지했다.

## 오프라인 검증

| 검사 | 결과 | 근거 |
|---|---|---|
| Developer Release | 오류 0, 경고 38 | `framecmp-4-build-dev.log` |
| Player Release | 오류 0, 경고 36 | `framecmp-4-build-player.log` |
| PlayerAuto Release | 오류 0, 경고 38 | `framecmp-4-build-auto.log` |
| LoadCheck Developer | 266곳, 틀림 0 | `framecmp-4-loadcheck-dev.log` |
| LoadCheck Player | 229곳, 틀림 0 | `framecmp-4-loadcheck-player.log` |
| LoadCheck PlayerAuto | 266곳, 틀림 0 | `framecmp-4-loadcheck-auto.log` |
| RGBA8 / 마스크 / 상태 fixture | 11 / 15 / 14개 통과 | `framecmp-4-fixture.log` |
| 새 대조군·판정 fixture | 17개 통과 | 같은 로그 |
| edition 메타데이터 | 13개 통과 | 같은 로그 |
| 생성기·예제 JSON | 24개 통과 | `framecmp-4-steps-check.log` |
| `git diff --check` | 통과 | 최종 확인 |

새 fixture는 음수 ID·역방향 생성·int 경계, 누락·잘못된 결과의 FAIL, 음성 0·양성 양수와 상태 유효성 조건을 검사한다. 부호 있는 ID 정렬 규칙을 적용한 불투명 색 겹침 모델에서 재질 교환·앞쪽 숨김 각각 144픽셀 변경, 복원 0을 확인한다. **이 144는 분석 모델의 fixture 값이며 Unity 실측이 아니다.** Unity가 본 카메라 체인에서 그 규칙대로 렌더링하는지, 실제 색·가시성·파괴 타이밍까지 증명하지 않는다.

Player DLL에는 FrameCompare·FrameCompareMetrics·FrameCompareState·FrameCompareControlPolicy 네 타입 정의와 framecmp 타입 참조가 없고 Developer/PlayerAuto에는 네 타입이 있다. LoadCheck는 각 edition에서 동적 필드 이름 5곳을 실행 중 확인 대상으로 남긴다. 빌드 경고는 0이 아니다.

```powershell
dotnet build -c Release
dotnet build -c Release -p:Edition=Player
dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1
dotnet run --project tools/LoadCheck -c Release -- bin/Release/StutterFix.dll
dotnet run --project tools/LoadCheck -c Release --no-build -- bin/Player/StutterFix.dll
dotnet run --project tools/LoadCheck -c Release --no-build -- bin/PlayerAuto/StutterFix.dll
dotnet run --project tools/optimization/FrameCompareCheck -c Release -- bin/Player/StutterFix.dll bin/Release/StutterFix.dll bin/PlayerAuto/StutterFix.dll
py -3 tools/optimization/check-framecmp-steps.py
git diff --check
```

`task-observer` 3.5.0과 기존 전역 관찰 헤더·활성 원칙을 읽었다. 전역 관찰 저장소는 허용 쓰기 범위 밖이므로 수정하지 않았다. 별도 스킬·메모리 변경 없이 이번 원인·수정·검증 한계를 이 결과에 기록했다.
