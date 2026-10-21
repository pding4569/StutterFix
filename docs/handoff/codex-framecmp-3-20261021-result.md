# framecmp-3 결과 — 2026-10-21

게임 실행·설치·커밋 없이 구현과 오프라인 검증을 마쳤다. 영역 제외, 잡음 비중단 보고, 행성 갱신·파티클 정지/복구, 29단계 생성기와 최종 RESULT 출력을 추가했다. **새 DLL의 실제 잡음·A/A2·A/B·양성 대조 값과 게임 완주는 미검증이다.**

## 영역 제외와 명령

기본값은 `center`다. 화면 중앙 가로 20% × 세로 20%, 즉 면적 4%를 제외한다. 2560×1440에서는 왼쪽 아래 (1024,576), 크기 512×288이다. 설정은 비교기 인스턴스에 유지하며, `stop` 후 새 인스턴스는 기본값으로 시작한다.

```text
framecmp mask center
framecmp mask none
framecmp diff A B [0..255] [png] [mask=x,y,w,h ...] [maskpct=cx,cy,w,h ...]
framecmp noise N1 N2 [mask=x,y,w,h ...] [maskpct=cx,cy,w,h ...]
framecmp assertzero A A2 [png]
framecmp result
```

- `mask=`는 왼쪽 아래 원점의 픽셀 단위 사각형이다. `maskpct=`는 **0~1 비율**로 중심 좌표 cx,cy와 너비·높이를 받는다. `maskpct=0.5,0.5,0.2,0.2`가 center와 같다. 50,50,20,20 표기는 거부한다.
- 명령별 사각형은 현재 preset에 더한다. 사용자 사각형만 쓰려면 먼저 `framecmp mask none`을 실행한다. 여러 사각형은 합집합이며 중복 픽셀은 한 번만 센다. 명령별 사각형은 다음 명령에 저장하지 않는다.
- 픽셀 중심이 사각형 안에 있는지를 검사한다. 왼쪽·아래 경계는 포함하고 오른쪽·위 경계는 제외하며 화면 밖 부분은 실제 픽셀 순회로 잘린다. NaN/무한대/0 이하 크기/잘못된 비율은 거부한다.
- `changed`, `maxRGBA`, 비율, PSNR, top32는 제외 영역 **밖**으로 계산한다. 비율·PSNR의 분모도 밖의 픽셀 수다. `mask inside_changed=.../... outside_changed=.../...`로 안팎 변경 픽셀 수와 면적을 함께 출력한다.
- 화면 전체를 제외해 밖의 픽셀이 0이면 비교를 거부한다. 아무것도 검사하지 않은 결과를 동일성 성공으로 만들지 않는다.
- `assertzero`와 양성 대조의 내부 비교에도 현재 preset을 적용한다. A/A2와 control 복원은 밖의 픽셀 0뿐 아니라 기존 캡처 상태 안정성도 요구한다. 마스크는 Renderer 상태 검사를 없애지 않는다.

예: 기본 center와 추가 왼쪽 아래 사각형을 함께 제외한다.

```text
framecmp diff A B 0 mask=0,0,64,64 maskpct=0.1,0.1,0.05,0.05
framecmp noise N1 N2 mask=0,0,64,64
```

## 잡음 바닥과 최종 요약

유효한 N1/N2 사이의 픽셀 차이와 상태 변화는 경고·진단으로 보고하고 계속 진행한다. 밖의 차이가 없으면 `noise outside=0`을 출력한다. 비영 밖 잡음 또는 상태 불안정은 `warning=True`이며 안쪽 변경 수도 별도 기록한다. 제출 중 상태 변화, GPU 오류, 잘못된 입력·해상도·캡처 옵션, 전체 화면 제외 등은 여전히 실제 오류다.

양성 대조마다 별도로 잡음 바닥을 측정한다. 그 바닥의 밖 픽셀이 비영이거나 상태가 불안정하면 해당 대조는 `INCONCLUSIVE`로 보고하고 다음 단계로 간다. 잡음을 변경 효과 검출로 잘못 주장하지 않는다. 안정된 바닥에서 시도한 대조는 기존대로 복원 0/상태 안정성을 요구하며, 실제 변경 픽셀이 양수일 때만 유효하다. 후보 부족, 16회 검출 실패, 복원 실패는 계속 오류다.

`framecmp-steps.py`의 기본/PNG/사용자 변경 명령과 예제 JSON을 29단계로 갱신했다.

```text
pause → filters off → ui exclude → mask center → noise N1 N2
→ grab A → gpusplit hide deco-image:small=1 → wait → grab B
→ gpusplit off → wait → grab A2 → assertzero A A2 → diff A B
→ control order → control hide → filters on → resume → framecmp result → stop → quit
```

앞부분의 timeout/auto/open/진단 끄기/seek/waitsongpos는 유지했다. `assertzero`가 내부에서 diff를 출력하고 밖 0을 검증한다. 잡음 비영이라는 이유만으로 noise 단계가 실패하던 경로는 제거했다. 다른 실제 실패까지 성공 처리하거나 완주를 보장하지 않는다.

마지막 framecmp 결과 줄 형식은 다음과 같다. 아래는 **형식 설명이며 실측값이 아니다.**

```text
framecmp RESULT noise_out=<밖 변경 수> aa2_out=<밖 변경 수> ab_out=<밖 변경 수> ctl_order=<양성 변경 수/INCONCLUSIVE/NA> ctl_hide=<양성 변경 수/INCONCLUSIVE/NA>
```

완료 시 resume 다음에 출력한다. 중간 오류로 stop/unload 정리로 들어가면 그때까지의 결과와 미실행 `NA`를 출력한다. 결과 중복 출력은 막는다. 양성 대조의 내부 noise는 최초 noise_out 값을 덮어쓰지 않으며, shots를 지워도 요약값은 유지된다. 명시적인 새 noise 명령은 이전 요약을 초기화한다.

## 중앙 잡음의 코드상 후보와 고정

`framecmp-run2-evidence.txt`의 두 캡처는 유효하고 1563/3,686,400픽셀이 달랐다. 주어진 상위 다섯 격자는 center 안에 들어가지만, 상위 격자 목록만으로 나머지 차이까지 모두 안에 있다고 증명할 수 없다. 원본 픽셀을 새 마스크로 재비교하지 않았으므로 기존 실행의 outside=0을 주장하지 않는다.

설치된 `Assembly-CSharp.dll`을 실행하지 않고 ILScan으로 읽었다. 근거는 `framecmp-3-planet-il.txt`다.

- `PlanetRenderer.LateUpdate`는 `Time.unscaledTime`을 읽는 경로가 있다. face 회전, rainbow 색과 ring/tail/core/face 색 갱신도 포함한다. 따라서 timeScale=0만으로 모든 행성 표시 갱신이 정지한다고 볼 수 없다. unscaledTime 경로는 missCooldownActive 등을 읽는 분기와 연결되며, 기존 실측에서 이 분기가 활성화됐는지는 모른다.
- `PlanetRenderer`는 tail/core/co-op 파티클 참조와 Play/Stop/Clear 경로를 가진다. 파티클 시뮬레이션과 셰이더·업로드 상태까지 기존 Renderer 스냅샷이 모두 담지는 않는다.

진단용 pause에 다음을 추가했다.

1. 소유 Harmony ID `StutterFix.framecmp.pause`로 `PlanetRenderer.LateUpdate`에 pause 동안만 거짓을 반환하는 prefix를 설치한다. resume에서 그 ID의 패치만 제거한다. PlanetRenderer.enabled 자체는 변경하지 않는다.
2. 유효한 Scene의 PlanetRenderer 하위 파티클과 tailParticles/tailParticlesCoop/coreParticles 필드가 ParticleSystem인 참조를 찾는다. **재생 중이던 시스템만** 집합에 저장해 `Pause(false)`한다. 이미 정지·일시정지 상태인 시스템은 소유하지 않는다.
3. resume에서 저장한 시스템이 살아 있고 여전히 paused이면 `Play(false)`한다. Clear/Stop/재시뮬레이션은 하지 않는다. pause 설정 실패 시에도 Resume 경로로 복구를 시도한다. 기존 stop/unload/error 정리와 시간·오디오·컨트롤러 복구의 finally를 유지했다.

이는 정지 중 바뀔 수 있는 코드 경로에 대한 조치다. **기존 1563픽셀의 실제 원인 확정이나 수정 후 잡음 제거의 실측 증거가 아니다.** Harmony prefix 실행, 실제 파티클 고정·재개, 다른 모드와의 상호작용은 게임 미실행으로 검증하지 않았다. pause 이후 새로 생성·재생되는 파티클을 지속 감시하는 기능도 추가하지 않았다.

## 오프라인 검증

| 검사 | 최종 결과 | 로그 |
|---|---|---|
| Developer Release 빌드 | 오류 0, 경고 38 | `framecmp-3-build-dev.log` |
| Player Release 빌드 | 오류 0, 경고 4 | `framecmp-3-build-player.log` |
| PlayerAuto Release 빌드 | 오류 0, 경고 4 | `framecmp-3-build-auto.log` |
| LoadCheck Developer | 266곳, 틀림 0 | `framecmp-3-loadcheck-dev.log` |
| LoadCheck Player | 229곳, 틀림 0 | `framecmp-3-loadcheck-player.log` |
| LoadCheck PlayerAuto | 266곳, 틀림 0 | `framecmp-3-loadcheck-auto.log` |
| RGBA8 / 마스크 / 상태 fixture | 11 / 15 / 14개 통과 | `framecmp-3-fixture.log` |
| edition 메타데이터 | 10개 통과 | 같은 로그 |
| 생성기·예제 JSON | 24개 통과 | `framecmp-3-steps-check.log` |
| `git diff --check` | 통과 | 최종 작업 시 확인 |

위 경고 수는 최종 빌드 로그 기준이며 Player/PlayerAuto는 증분 빌드다. 경고 전체가 없다는 뜻은 아니다. LoadCheck의 동적 필드 이름 5곳은 각 edition 모두 실행 중 확인 대상으로 남아 있다. 최초 행성 고정 코드 빌드에서 설치 Harmony에 `UnpatchSelf`가 없어 오류 1개를 발견했고, 지원되는 `UnpatchAll(소유 ID)`로 고친 뒤 최종 3종을 재검증했다.

마스크 fixture는 안팎 집계·밖만의 max/PSNR/분모, 중복 합집합, 음수 원점 클리핑, threshold, 왼쪽 아래 경계, 전체 제외 시 NaN, 잘못된 입력을 검사한다. Player DLL에는 기존 세 framecmp 타입 정의와 framecmp 타입 참조가 없고 Developer/PlayerAuto에는 세 타입이 있다. Unity 실행 순서·실제 GPU 캡처를 검사하는 fixture는 아니다.

재현 명령은 기존 인계와 같은 로컬 빌드/검사 명령이다. `framecmp-steps.py --output ...`은 JSON만 만들고 게임 실행이나 설치를 하지 않는다.

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

`task-observer` 3.5.0을 읽고 전역 관찰 5건의 헤더와 활성 원칙을 확인했다. 전역 저장소는 허용 쓰기 범위 밖이므로 수정하지 않았다. 이 작업에서 별도 관찰 파일·스킬 변경은 작성하지 않았고, 구현·빌드 문제와 검증 한계는 이 결과 문서에 남겼다.
