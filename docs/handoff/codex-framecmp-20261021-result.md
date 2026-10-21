# framecmp 구현 결과 — 2026-10-21

게임 실행·설치·커밋 없이 코드, 빌드, 오프라인 검증까지만 수행했다. **실제 카메라 체인 캡처 성공, 잡음 바닥 0, 양성 대조 검출은 아직 미확인이다.** 이 결과로 화면 동일성이나 성능 개선을 채택하면 안 된다.

## 구현

- `FrameCompare.cs`, `FrameCompareMetrics.cs`: `DEV || AUTOTEST` 전용, 일반 Player 컴파일 제외. 설정 변경이나 자동 시작 없음. 명령을 처음 사용할 때만 진단 오브젝트를 만든다.
- AutoTest `pause` / `resume`: `scrController.paused`, `Time.timeScale`, `DOTween.timeScale`, `AudioListener.pause`를 저장·정지·복원한다. `TogglePauseGame`과 UI 메뉴는 호출하지 않는다. 현재 게임 DLL의 `set_paused`는 `_paused`, 입력 매핑, 오류 막대 상태를 갱신하며 시간 배율은 바꾸지 않는다. 그래서 시간·트윈·오디오를 각각 정지한다. 확인 근거는 `framecmp-controller-il.txt`.
- `DecoAnim` / `FloorAnim`의 기존 Update prefix/postfix에 개발 빌드 정지 가드를 추가했다. 원래 DOTween은 전역 배율 0으로 정지한다. unscaled 애니메이션·커스텀 셰이더·다른 모드까지 완전히 정지됐다고 가정하지 않고 잡음 바닥 검사로 실패 처리한다.
- `WaitForEndOfFrame` 뒤 `ScreenCapture.CaptureScreenshotIntoRenderTexture`로 **화면 전체 크기**의 ARGB32/비-MSAA RT에 복사하고 `AsyncGPUReadback` RGBA32로 회수한다. 단독 카메라 입력, 깊이, camRT 형식을 복제하지 않으므로 기존 `clear=Nothing` 제한을 피하는 경로다. 추가 `Camera.Render()` 호출은 없다.
- 캡처 옵션 적용 후 자연 프레임 1회를 기다리고 다음 프레임 끝을 캡처한다. AutoTest는 readback이 끝날 때까지 다음 명령으로 넘어가지 않는다. readback 제한은 실시간 15초, 프레임 끝에 도달하지 못한 경우 AutoTest 단계 제한이 작동한다.
- 카메라 이벤트는 캡처 중의 옵션 유지에만 사용한다. 새 Harmony 패치와 `Module.ResolveSignature` 사용은 없다.

## 명령

| 명령 | 동작 |
|---|---|
| `pause` / `resume` | 장면 정지 / 저장한 값 복원. 이미 정지 중이면 중복 저장하지 않음 |
| `framecmp filters off` | 캡처 동안 필터를 끔. CameraFilterPack 계열, Bloom 이름, `OnRenderImage` 2인자 컴포넌트 탐색 |
| `framecmp filters on` | 원래 enabled 상태로 캡처. 강제로 모든 필터를 켜는 명령은 아님 |
| `framecmp ui exclude` | 기본값. Canvas를 끄고, 안전할 때 카메라 마스크에서 UI 레이어 제외 |
| `framecmp ui include` | UI 포함 |
| `framecmp grab A` | 정지 상태에서 최종 RGBA8 캡처를 A로 저장 |
| `framecmp diff A B [0..255] [png]` | 기본 임계 0. 채널별 최대 차이, 다른 픽셀 수/비율, PSNR, 32px 격자 상위 5곳 |
| `framecmp noise N1 N2` | 같은 정지 상태에서 두 장을 차례로 캡처. 픽셀 차이 또는 Renderer 상태 변화가 있으면 실패 |
| `framecmp control hide` | 실제 장식 한 개 forceRenderingOff 후 검출 및 복원 검사 |
| `framecmp control order` | 실제 겹치는 장식 두 개의 재질 ID 동률 순서 반대 방향으로 sortingOrder를 강제한 뒤 검출 및 복원 검사 |
| `framecmp save A` | 개발 모드 폴더 아래 `framecmp/A.png` 저장 |
| `framecmp clear` | 메모리 캡처 해제 |

필터와 Canvas의 원래 enabled 값, 카메라 cullingMask는 **각 캡처 완료 직후** 되돌린다. 캡처 중에는 preCull 이벤트로 유지하고, 렌더 끝에 옵션이 덮어써졌으면 실패한다. 최종 합성에 필요할 수 있는 OverlayCam 자체는 끄지 않는다. UI 레이어에 Canvas 밖의 활성 Renderer가 있으면 해당 레이어를 제거하지 않는다. 이 경우 Canvas 밖 HUD가 남을 수 있으므로 PNG를 확인해야 한다. 필터 탐색에 잡히지 않는 CommandBuffer/SRP 효과도 남을 수 있다.

차이 판정은 어느 한 RGBA 채널이라도 **임계보다 큰** 픽셀을 한 번 센다. 최대 차이와 PSNR은 임계와 무관한 원시 값이며 PSNR은 RGBA 4채널 평균 제곱 오차로 계산한다. 완전 일치는 Infinity. 영역은 원시 readback 배열의 좌하단 기준 `x,y,width,height`와 채널 절대 차이 합(L1)으로 표시한다. 실제 API/그래픽스 백엔드에서 PNG 상하 방향은 최초 실기 확인 대상이다.

캡처는 최대 8장, 이름은 64자 이내 문자/숫자/`_`/`-`. 해상도나 캡처 옵션이 다른 두 장은 비교하지 않는다. `control`은 기존 캡처를 지우므로 필요한 A/B/A2 PNG는 먼저 저장한다. 3440×1440은 고정 제한 없이 지원하도록 구현했으며 실제 검증한 해상도는 아직 없다.

## 대조군과 정지 상태 검증

`control`은 자체적으로 변경 없는 두 장의 잡음 바닥 0을 먼저 요구한다. 최대 2,048개의 화면 내 장식 후보에서 16회까지 시험하며, 가려진 후보나 순서에 둔감한 후보의 차이 0은 통과시키지 않는다. 정렬 대조는 같은 layer/order/queue, 다른 재질 ID, 같은 카메라 깊이, 겹치는 화면 경계를 요구한다. SortingGroup은 제외하고 orthographic 기본 정렬만 지원한다. 재질 자체를 복제·교체하지 않고 sortingOrder ±1로 재질 ID 순서의 반대를 강제한다. 다른 같은 키 장식과의 상대 순서도 바뀔 수 있어 두 장식만의 독립 정렬 실험은 아니다.

**통과 조건은 잡음 0 + 의도한 변경 >0 + 복원 후 0 + Renderer 상태 복원이다.** 후보 부족, 16개 후보 모두 차이 0, 복원 불일치는 명령 실패다. 실제 대조군은 게임 미실행으로 아직 0회다.

Renderer 상태 서명은 인스턴스 ID, 활성/표시, 행렬, sorting layer/order, 재질 ID 배열, Sprite ID/색/뒤집기를 순서대로 해시한다. readback 사이 서명이 바뀌면 캡처를 거부하고 noise/복원 캡처 사이도 확인한다. 이 해시는 충돌 가능성이 있으며 모든 재질 속성, shader global, 메시 변형, 텍스처 업로드, 카메라 행렬까지 증명하지 않는다. 최종 픽셀 음성·양성 대조가 필수다.

## 복원과 수명

AutoTest 오류·시간 초과·정상 종료·keep 종료·reload 경로에서 `FrameCompare.Stop`, `Main.UninstallAll`에서 `FrameCompare.Unload`를 호출한다. 두 경로 모두 대조군, 필터, UI, 시간·트윈·오디오를 복원하고 캡처 메모리를 비운다. 기존 SceneReset이나 다른 연구 모듈은 수정하지 않았다.

정상 readback은 비동기 폴링이다. GPU readback 시간 초과에서는 RT를 조기 파괴하지 않고 소유 오브젝트가 완료까지 보관하며, 완료 후 해제한다. 그동안 새 framecmp 요청은 거부한다. 모드 언로드는 남은 소유 요청을 `WaitForCompletion`으로 배출한 뒤 RT를 해제한다. 따라서 드라이버가 멎은 상황에서 언로드 배출까지 시간 상한을 보장하지는 않는다. 일반 캡처와 AutoTest Stop 경로는 이 동기 배출을 호출하지 않는다.

## 사용 예

```powershell
py -3 tools/optimization/framecmp-steps.py --chart '실제 Arche 맵 경로.adofai' --png --output framecmp-steps.json
# 재질 공유 대조는 이 옵션으로 생성:
py -3 tools/optimization/framecmp-steps.py --chart '실제 Arche 맵 경로.adofai' --apply 'matshare on' --restore 'matshare off' --png
```

생성기는 JSON만 만든다. 설치·실행·sfmeasure import를 하지 않는다. `framecmp-example-steps.json`은 경로를 `Arche.adofai`로 둔 27단계 예다. 실제 실행자는 경로를 바꿔 생성해야 한다. open → seek 262 8 → waitsongpos 264 → pause → noise → A → 변경 → B → 복원 → A2 → 비교 → 두 양성 대조 → resume 순서다. 원래 A/A2 diff는 수치를 보고하는 명령이므로 실행자가 0을 확인해야 한다. controls는 자체 pass/fail을 강제한다. 실패 시 AutoTest가 원복하고 중단하므로 원인을 고친 뒤 새 묶음으로 다시 실행한다.

**성능 측정 판에서는 사용하지 않는다.** 이 도구는 전체 오브젝트 탐색, 정렬/해시, GPU 복사/readback과 PNG 저장 비용을 만든다. matshare는 기존 구현상 DEV에서만 실제 켜질 수 있으므로 PlayerAuto에서는 기본 gpusplit 예를 사용한다.

## 오프라인 확인

| 확인 | 결과 |
|---|---|
| `dotnet build -c Release` | 오류 0 |
| `dotnet build -c Release -p:Edition=Player` | 오류 0 |
| `dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1` | 오류 0 |
| LoadCheck Developer / Player / PlayerAuto | 266 / 229 / 266곳, 틀림 각각 0. 동적 이름 5곳은 기존대로 실행 중 확인 필요 |
| FrameCompareMetrics fixture | 11개 통과: 동일/전체 차이/alpha 단독/채널 최댓값/임계/PSNR/비율/가장자리 격자/정렬/입력 거부 |
| framecmp 타입 메타데이터 | 7개 통과: Player 타입·참조 없음, Developer/PlayerAuto에 두 타입 있음 |
| Python 생성기 | 27단계 JSON roundtrip/명령 순서/timeout/기존 pixeldiff 미사용 확인 |
| `git diff --check` | 통과 |

빌드에는 기존 참조 버전 충돌·deprecated API·미사용 필드 경고가 남는다. 경고 0이나 실제 Unity 로딩 성공으로 보고하지 않는다. 로그는 `framecmp-build-*.log`, `framecmp-loadcheck-*.log`, `framecmp-offline-check.log`.

기존 전체 `DiagnosticCheck`도 시도했으나 새 도구와 무관한 `MaterialPoolFixture`의 edition 기대값에서 실패했다. 이 검사기를 고치거나 전체 격리 통과로 기록하지 않았다. 새 두 타입은 전용 fixture의 실제 DLL 메타데이터로 확인했다. task-observer는 3.5.0을 읽고 관찰했으며 이번 작업에서 추가 재사용 스킬 후보는 없다. 전역 관찰 저장소는 이 worktree의 쓰기 범위 밖이므로 변경하지 않았다.

## 실기에서 막힐 경우

- `supportsAsyncGPUReadback=false`, ARGB32 RT 생성 실패, ARGB32→RGBA32 readback 오류는 장치/해상도와 함께 실패한다. 사용 중인 후처리 원본 RT의 형식이 다르다는 이유만으로 통과 처리하지 않는다.
- 프레임 끝이 오지 않음/최소화/렌더 중단, 해상도 변경, 정지 값 덮어쓰기, Renderer 서명 변화도 성공으로 처리하지 않는다.
- noise가 0이 아니면 필터 이름·비활성 개수, UI 잔존, 시간 의존 shader/global, MSAA resolve, 비동기 텍스처 갱신, 다른 모드 갱신을 확인해야 한다. 원인을 찾기 전에 허용 임계로 덮지 않는다.
- 후보 대안은 프레임 끝 `ReadPixels` 동기 회수(호환성 확인용이나 stall 비용 큼), `CaptureScreenshotAsTexture`(동기 비용·CPU 복사), 외부 화면 캡처(커서/오버레이/색 보정/프레임 선택 차이 통제 필요)다. 어느 대안도 아직 구현·검증하지 않았다.
- 실제 정지 화면 PNG, 잡음 0, 두 양성 대조 >0, 복원 0, 옵션·언로드 복원을 확인한 후에만 해당 장면·해상도의 증거로 사용한다. 전체 곡/모든 GPU/모든 해상도 동일성으로 일반화하지 않는다.
