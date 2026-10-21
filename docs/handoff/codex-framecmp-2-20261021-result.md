# framecmp-2 결과 — 2026-10-21

게임 실행·설치·커밋 없이 코드 수정과 오프라인 검증을 수행했다. **실제 첫 캡처 성공, 잡음 바닥 0, 양성 대조 검출, 음성 대조 0은 아직 미검증이다.** 요청 2번의 실제 게임 시험 완주는 실행 금지 조건 때문에 수행하지 않았다.

## 기존 실패에서 확정한 것

기존 `Grab`의 실패 조건은 다음 OR 조건 하나였다.

```csharp
w != Screen.width || h != Screen.height || state != State()
```

첫 `state`는 `WaitForEndOfFrame` 이후, 두 번째는 `AsyncGPUReadback` 대기가 끝난 코루틴 재개 시점에 수집했다. 요청이 바로 끝나지 않으면 `yield return null`을 거쳐 다음 Update 단계에서 검사한다. 따라서 서로 다른 프레임·렌더링 단계의 상태를 비교할 수 있었다. 기존 `State()`에는 화면 밖·비활성·표시 금지 Renderer까지 포함됐고 ID/표시 플래그/행렬/정렬/재질/Sprite 상태 중 하나만 달라도 같은 오류가 났다. 카메라 상태와 RT 크기는 이 오류 분기에 직접 검사되지 않았다.

`framecmp-run1-evidence.txt`와 전체 `E:\StutterFixWork\night-results\framecmp.log`에는 분기별 값이나 전후 해시가 없다. **첫 실행에서 실제로 달라진 필드가 해상도인지, 어느 Renderer의 어떤 값인지 소급 확정할 수 없다.** 첫 캡처가 실패했으므로 두 캡처 사이 필터 복원/재비활성만으로 첫 실패를 설명할 수도 없다.

코드에서 `Dormancy`는 장식 순회 목록을 관리하며 Renderer의 enabled/forceRenderingOff를 직접 쓰지 않는다. `InvisibleSkip`은 갱신 콜백에서 forceRenderingOff를 바꾸고 `TileCull.Step`은 PostLateUpdate에 enabled를 갱신할 수 있다. 그러나 기존 로그는 이 둘 중 어느 것도 실제 원인으로 특정하지 않는다. 초기 명령에는 `gpusplit off`가 있어 live hide도 첫 잡음 캡처의 확정 원인으로 볼 수 없다. 기존 실행에서 readback 동안 몇 프레임이 지났는지도 기록되지 않았다.

## 수정한 판정과 로그

- `FrameCompareState.cs`: Unity와 분리한 필드별 스냅샷 비교기. `rawHash`는 진단용이며 통과 여부는 해시 동등성이 아닌 필드 비교로 판단한다.
- 캡처 제출 직전과 screenshot/readback 요청 직후를 같은 프레임 끝에서 비교한다. 화면/카메라/표시 가능한 Renderer 상태가 제출 중 바뀌면 실패한다.
- 제출 후 대기 중 장면 변화는 `phase=readback-wait-advisory`로 기록한다. 캡처마다 별도 RT를 만들고 완료까지 다시 쓰거나 해제하지 않으므로, 나중 장면 변화만으로 이미 제출한 RT의 readback을 거부하지 않는다. RT 생성 상태·크기, readback 크기·RGBA 바이트 수와 GPU 오류는 계속 검사한다.
- `submission`, `readback-wait-advisory`, `noise`, `negative-control`, `control-restore` 단계마다 변경 필드 수/무시한 숨김 상태 필드 수/최대 12개 전후 값을 기록한다. 표시 관련 변경을 먼저 출력한다.
- 화면 해상도/전체 화면 모드/MSAA/색 공간, Renderer 개수/ID 목록 해시/원시 상태 해시, enabled/forceRenderingOff/active/행렬/layer/정렬/재질/Sprite 정보를 남긴다.
- 카메라별 enabled/active/마스크/깊이/view/projection/culling 행렬/viewport/clear/HDR/MSAA/display/transparency sort/target RT ID·크기·형식·MSAA·생성 상태를 기록한다.
- source/completion frame과 경과 프레임·실시간 ms, 캡처 RT와 현재 화면 크기를 기록한다. `Time.time`, unscaled/realtime, `Shader.GetGlobalVector`로 읽은 `_Time`/`unity_DeltaTime`도 남긴다. 이 API 값은 GPU에서 사용한 모든 셰이더 상수를 증명하지 않는다.

두 스냅샷 모두에서 disabled/forceRenderingOff/비활성에 해당하는 Renderer의 내부 변화·추가·제거는 숨김 변화로만 센다. 어느 한쪽에서라도 표시 가능하면 변화는 실패 대상이다. 카메라도 어느 한쪽에서 활성화돼 있으면 변화는 실패 대상이다. 화면 밖·알파 0·마스크 밖·Canvas 하위라는 추측으로 활성 Renderer의 변화를 제외하지는 않는다.

`noise`, `assertzero`, control 복원은 모두 **캡처 시점끼리의 상태 안정성 + RGBA 임계 0**을 계속 요구한다. 대기 중 변화의 진단 분리는 서로 다른 캡처 사이의 안정성 검사를 완화하지 않는다.

## 반복 토글과 시험 순서

필터·Canvas·카메라 마스크는 첫 캡처부터 같은 옵션 묶음 동안 유지한다. 매 캡처 완료 때 복원하고 다시 끄는 동작을 없앴다. 새 컴포넌트는 다음 캡처 적용 때 탐색하고 preCull에서 옵션을 유지한다. 옵션 변경, resume, 오류, stop, unload에서 원래 값을 복원한다. preCull 구독은 중복되지 않게 한다. resume의 캡처 복원 중 예외가 나도 시간·트윈·오디오·컨트롤러 정지 값의 복원을 시도하도록 finally를 유지했다.

`Dormancy`/`InvisibleSkip`/`TileCull` 자체에는 새 정지 가드를 넣지 않았다. 제출 후의 장면 상태와 이미 캡처한 RT의 수명을 구분하고, 숨김 상태 변화를 분리하는 방식으로 수정했다. 기존 `DecoAnim`/`FloorAnim` 정지 가드와 DOTween/게임 시간 배율 0은 유지했다.

`framecmp assertzero A B [png]`를 추가했다. 픽셀 차이나 유효 상태 변화가 있으면 AutoTest 오류로 중단한다. `framecmp-steps.py`의 A/A2 비교를 이 명령으로 바꿔, 수치만 출력하고 음성 대조 실패를 지나가던 문제를 막았다. 27단계 구조와 order/hide 양성 대조는 유지했다. 예제 JSON도 갱신했다. 실제 사용자 명령/장면에 따라 양성 후보 부족이나 비영 잡음이 나오면 계속 실패해야 하며 성공을 보장하는 우회는 없다.

## 오프라인 검증

| 검사 | 결과 | 로그 |
|---|---|---|
| Developer Release 빌드 | 오류 0, 경고 38 | `framecmp-2-build-dev.log` |
| Player Release 빌드 | 오류 0, 경고 36 | `framecmp-2-build-player.log` |
| PlayerAuto Release 빌드 | 오류 0, 경고 38 | `framecmp-2-build-auto.log` |
| LoadCheck Developer / Player / PlayerAuto | 266 / 229 / 266곳, 틀림 각각 0 | `framecmp-2-loadcheck-{dev,player,auto}.log` |
| RGBA8 fixture | 11개 통과 | `framecmp-2-fixture.log` |
| 필드 스냅샷 fixture | 14개 통과 | 같은 로그 |
| edition 메타데이터 | 10개 통과 | 같은 로그 |
| Python 생성기 | 21개 통과, 기본/PNG/matshare와 예제 JSON | `framecmp-2-steps-check.log` |
| `git diff --check` | 통과 | 최종 작업 시 확인 |

스냅샷 fixture는 동일 상태, 표시↔숨김 전환, 숨긴 행렬 변경의 분리, Renderer 추가/삭제, 해상도/RT/projection/forceRenderingOff/정렬/재질 변경의 필드 진단을 검사한다. Unity의 실제 콜백 순서나 GPU readback은 검사하지 않는다. Player DLL에는 FrameCompare/FrameCompareMetrics/FrameCompareState 정의와 framecmp 타입 참조가 없고 Developer/PlayerAuto에는 세 타입이 있다. LoadCheck의 동적 이름 5곳은 각 빌드 모두 기존대로 실행 중 확인 대상이다.

재현 명령은 다음과 같다. 모두 로컬 빌드/반사 검사/JSON 생성뿐이다.

```powershell
dotnet build -c Release
dotnet build -c Release -p:Edition=Player
dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1
dotnet run --project tools/LoadCheck -c Release -- bin/Release/StutterFix.dll
dotnet run --project tools/LoadCheck -c Release -- bin/Player/StutterFix.dll
dotnet run --project tools/LoadCheck -c Release -- bin/PlayerAuto/StutterFix.dll
dotnet run --project tools/optimization/FrameCompareCheck -c Release -- bin/Player/StutterFix.dll bin/Release/StutterFix.dll bin/PlayerAuto/StutterFix.dll
py -3 tools/optimization/check-framecmp-steps.py
```

## 다음 실제 실행에서 확인할 것

실행이 별도로 허용된 뒤 새 DLL과 새 생성 JSON으로 확인해야 한다. 기존 실행 결과를 수정본의 성공 증거로 재사용하지 않는다.

1. N1/N2의 `noise zero=True`, `rendererStateEqual=True`와 submission changedFields=0.
2. A/A2 `assertzero`의 changed=0, 유효 상태 변화 0.
3. order/hide 각각 `valid=1 restored=0 positive=>0`.
4. 완료 또는 오류 후 필터/UI/마스크/시간·트윈·오디오 복원.
5. 잡음이 비영이면 `phase=noise`의 필드, capture-context 시간, 카메라 RT/MSAA와 PNG를 확인한다. 스냅샷이 같아도 시간 의존 커스텀 셰이더/재질 속성/입자·메시 변형/텍스처 업로드는 남을 수 있다. 새 실측 없이 이 원인을 확정하거나 제거했다고 보고할 수 없다.

필드 스냅샷은 전체 Renderer 탐색과 문자열 저장 비용을 늘린다. 8장 상한은 유지하지만 실제 메모리/소요 시간은 미측정이며 성능 시험에 섞으면 안 된다. 화면 동일성·성능 개선·다른 해상도 또는 전체 곡으로 일반화할 증거는 없다.

`task-observer` 3.5.0을 읽고 기존 전역 관찰/원칙을 확인했다. 전역 저장소는 허용된 쓰기 범위 밖이므로 변경하지 않았다. 별도 스킬 수정은 하지 않았다.
