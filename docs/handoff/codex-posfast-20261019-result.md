# PosFast 작업 결과 — 2026-10-19

## 결론

기본 끔 실험 경로, 장식별 구조체 슬롯, 단일 축·위치 쌍 연결, 표본 검증·통계·벤치 명령을 구현했다. **단, 인계 문서의 핵심 가정이 실제 IL과 달랐다. `UpdateScreenClamp()`는 화면 고정이 꺼져 있어도 `screenRelativePos`를 항상 쓴다.** 따라서 “`pivotPosVec`만 갱신”하면서 요청된 상태 비트 동일성도 보존하려면 추가 조건이 필요하다.

현재 구현은 계산될 `screenRelativePos`가 이미 저장된 값과 비트 동일할 때만 호출을 생략한다. 나머지는 원래 호출을 수행하고 `screen-change`로 센다. 실제 위치 이동 대부분은 이 조건에서 폴백할 것으로 **추정**한다. 성능 개선 후보로 채택하거나 기본값을 켜지 않았다. 화면 상태까지 같은 식으로 갱신하는 범위 변경은 사용자에게 질문했으며, 이 문서를 쓸 때까지 답변은 없었다. 그 변경은 적용하지 않았다.

게임 실행 0회, Mods 설치 0회, 커밋 0회. 마이크로 벤치·곡 전체 검증·실제 A/B는 실행하지 않았다. 게임 실행 금지 지시 때문에 µs 실측이나 실제 이득 수치를 만들지 않았다.

## 상태 불일치 반례와 IL 근거

새로 저장한 원시 IL: [codex-posfast-20261019-il.txt](codex-posfast-20261019-il.txt). 게임 DLL을 별도 .NET 프로세스에서 리플렉션으로 읽었고 게임 메서드는 실행하지 않았다.

1. **화면 고정이 꺼져 있어도 화면 상대 좌표는 바뀐다.** `UpdateScreenClamp`의 `IL_001b`는 `clampToScreen`을 저장한다. 이후 `IL_0020`부터 위치와 오프셋을 더하고, `CameraAspect`일 때만 X에 화면 비율을 적용한다. `IL_0053` 이후는 공통 경로로 `(pivotPosVec + pivotOffsetVec) / 20f + (0.5f, 0.5f)`를 `IL_0078`에서 `screenRelativePos`에 저장한다. clamp가 false일 때 반환하는 분기가 없다. 예: 오프셋 0, 위치 (0,0)→(20,0)이면 화면 상대 좌표 (0.5,0.5)→(1.5,0.5)이다. 위치 필드만 쓰면 이전 화면 상대 좌표가 남는다. 화면에 쓰이는지와 별개로 요청된 비트 비교에서 명백히 다르다.
2. **잘린 이미지 피벗 변경.** 기존 [decoration IL](codex-updatecost-20261018-decoration-il.txt)의 `Setup`은 `croppedPivotOffset`을 쓰고 `SetPosition`을 호출한다. [효과 IL 발췌](codex-posfast-20261019-move-il.txt)에서도 `ffxMoveDecorationsPlus`가 `SetTextureScaleMultiplier`를 호출하고 `targetCropPivotOffset`을 `croppedPivotOffset`에 쓴 뒤 `SetPosition`을 호출함을 확인했다. 이 값은 자식 위치 `(pivotOffsetVec + croppedPivotOffset) / trueScaleMultiplier`에 포함된다. 슬롯은 효과 시작마다 기준을 무효화하고 첫 완전 호출 뒤에만 다시 유효해진다.
3. **배율 변경.** `get_trueScaleMultiplier`의 실제 IL은 `scaleMultiplier * textureScaleMultiplier`이다. `Setup`의 배율 쓰기와 `scrVisualDecoration.SetTextureScaleMultiplier`의 `textureScaleMultiplier` 쓰기를 확인했다. 이전 값으로 구한 자식 위치를 계속 쓰면 안 된다. 계산된 배율 벡터를 비트 비교한다.
4. **첫 완전 호출 전 상태.** 위치·배율 필드가 같다는 이유만으로 자식 Transform이 올바르다고 가정할 수 없다. 새 슬롯은 유효하지 않으며, 폴백 후 실제 자식 위치가 계산값과 비트 동일한 경우에만 기준을 저장한다. `MoveApply.SetLocal`의 epsilon 생략 등으로 실제 쓰기가 완료되지 않은 경우도 기준을 만들지 않는다.
5. **배치·시차 변경.** `SetParallax`는 placement를 쓰고 `UpdateScreenClamp`를 직접 부른다. 현재 clamp가 false여도 placement가 Camera/CameraAspect면 원래 호출이 clamp를 켤 수 있으므로 둘 다 제외한다. 자식 Transform·parallax 객체 자체가 교체된 경우도 제외한다.

## 구현

- `PosFast.cs`: 장식→슬롯 Dictionary는 `NewRec`에서 한 번 조회한다. 프레임별 스텝은 `Rec.PosSlot/PosGeneration`으로 `Entry[]` 구조체를 바로 읽는다. offset·crop·계산된 배율의 float 비트, placement·clamp, 자식·parallax 객체를 기록한다. 같은 장식의 X/Y 기록은 같은 슬롯을 공유한다.
- 정확히 `scrVisualDecoration`, 살아 있는 parallax/child, 비 Camera placement, clamp 꺼짐, editor 없음, 외부 패치 없음, 유효한 기준과 같은 입력 필드, 같은 화면 상대 좌표, 기존 `MoveApply.SkipForLate` 성공 때만 `pivotPosVec`를 쓴다. `SkipForLate`의 기존 등록·통계는 그대로 수행한다. 양수 color alpha도 요구해 `InvisibleSkip.LazyPrefix`가 완전 호출을 생략한 결과를 기준으로 삼지 않는다.
- `DecoAnim.OnUpdate`의 위치 키 1/2와 `StepPair`에 연결했다. 단일 축 폴백은 원래 `SetPositionX/Y`를 호출하며 기존 예외 경계를 유지한다. `OnComplete`는 원래 setter를 계속 호출하고 통계·기준만 갱신한다. 기본 끔일 때 단일 축은 기존 setter 분기로 바로 간다.
- `Dormancy.ForgetLevel`에서 슬롯·장식 참조를 놓고 세대를 바꾼다. `Main.Unload`에서 끄고 참조를 놓는다. 오래된 Rec의 세대가 다르면 Dictionary 재조회 없이 원래 경로로 간다. 추가 Harmony 패치·이벤트·PlayerLoop 등록은 없다.
- Harmony 소유자는 첫 위치 효과 바인딩 때 검사하고 맵 해제·재초기화 때 무효화한다. 검사 대상은 SetPosition/SetPositionX/Y/UpdateScreenClamp와 배율 getter다. 자기 모드의 정확한 ID만 허용한다. **검사 후 다른 모드가 동적으로 패치를 추가하는 경우는 자동 감지하지 않는다.** 새 프로세스에서 모드 초기화를 마친 상태로 비교해야 한다.
- 별도 `groupstep-on` 실험의 위치 쌍은 자체 setter 경로를 쓰므로 이번 `StepPair` 연결을 타지 않는다. PosFast A/B에서는 groupstep을 끈다.

## 명령·검증·진단 비용

모드 초기화 때 모드 폴더의 `posfast-on` 파일이 있어야 켜진다. 파일을 실행 도중 바꾸는 것만으로 토글되지 않는다. 이번 작업에서는 파일을 만들지 않았다.

개발자/PlayerAuto 명령:

- `posfast state`: enabled, 슬롯 수, 생략·원래 호출 시도 수, 이유별 수, 검사·불일치·첫 예, 호환성 상태.
- `posfast verify` / `posfast verify off`: 생략한 호출의 1/64를 같은 프레임에 실제 `SetPosition`으로 다시 수행한다. `pivotPosVec`, `pivotOffsetVec`, `childTransform.localPosition`, `screenRelativePos`, `clampToScreen`을 비트 비교한다. 불일치 장식은 맵 해제 전까지 제외하며 효과를 새로 만들어도 제외는 유지된다. 첫 불일치만 즉시 로그를 쓰고 이후 개수는 state에서 확인해 로그 폭주를 피한다.
- `posfast bench`: 재생 중에는 거부한다. 메뉴에서 더미 장식 2,048개, 준비 5회, 측정 256회로 성공 경로의 메모 비교·screen 비교·필드 쓰기·실제와 공유하는 HashSet/List 등록 코드를 잰다. 반복마다 별도 측정용 집합을 비운다. 실제 게임 목록·통계를 건드리지 않는다.

동일 입력 재호출의 근거: 원래 SetPosition은 parallax가 있으면 입력으로 자식 위치·피벗 필드·화면 상대 좌표를 다시 정한다. 적용 조건에서 editor 부작용이 없고, 기존 UpdatePosition은 SkipForLate로 다시 미뤄진다. 화면 상대 좌표는 추가 guard로 이미 원래 계산값이며 자식 위치는 기준 호출에서 확인했다. 외부 setter/clamp/getter 패치는 검사에서 제외한다. 이 조건 아래 열거한 게임 상태는 재호출로 같아야 한다. StutterFix 자체의 호출 횟수·표본 진단 수는 늘 수 있다. 표본 재실행은 성능 A/B에서 끈다.

통계는 PosFast가 연결된 위치 호출 시도와 완료 호출의 누계다. 게임 전체의 모든 SetPosition 호출 수가 아니다. 표본 재실행은 checked로 별도 집계한다. 이유 배열 순서는 `unbound/type/null/clamp/editor/foreign/alpha/rejected/cold/identity/fields/late/complete/screen-change`이다. 맵이 바뀌어도 수치·첫 불일치 문자열은 세션 누계로 남고 슬롯·장식 참조는 해제된다.

스텝마다 Stopwatch나 Harmony 패치 목록 조회를 하지 않는다. 슬롯 배열 확장·Dictionary 추가는 효과 생성 시점에만 한다. 정상 스텝에는 로그·문자열 생성이 없다. 표본 검증의 Transform 읽기·실제 setter 재실행은 verify를 켠 경우에만 추가된다. 벤치는 Unity native 포인터가 없는 더미이므로 실제 scene/liveness/GetVisible 조건과 실게임 SkipForLate의 전체 비용을 측정하지 못한다. 출력은 대체 성공 경로 비용이며 **0.15µs 이하가 나와도 실제 전체 경로 예산 통과를 증명하지 않는다.** 현재 구현의 screen-change 폴백 추가 비용도 별도 A/B가 필요하다.

## 이번 검증 결과

| 검사 | 결과 | 근거 |
|---|---|---|
| 개발자 빌드 | 오류 0, 경고 35 | [dev-build](codex-posfast-20261019-dev-build.txt) |
| Player 빌드 | 오류 0, 경고 28 | [player-build](codex-posfast-20261019-player-build.txt) |
| PlayerAuto 빌드 | 오류 0, 경고 35 | [playerauto-build](codex-posfast-20261019-playerauto-build.txt) |
| 실제 PosFast.cs를 연결한 관리 코드 대체 검사 | assertion 176개 통과 | [check](codex-posfast-20261019-check.txt), `tools/PosFastCheck` |
| 게임 DLL 원시 IL 읽기 | 완료 | [IL](codex-posfast-20261019-il.txt) |
| Unity/Mono µs 벤치, 실제 프레임 동일성, ABBA | 미실행 | 게임 실행 금지 |

대체 검사는 기본 끔·파일 opt-in·첫 완전 호출·offset/crop/배율·signed zero·NaN 비트·배치/clamp·editor·자식/parallax 교체·Late 조건·외부 패치·세대 무효화·1/64 샘플·불일치 장식 격리·해제 동작을 확인한다. 화면 고정이 꺼진 실제 위치 변경도 screen-change로 폴백하는 회귀 사례를 포함한다. 출력의 mismatch 1은 자식 위치를 의도적으로 훼손한 시험이며 실게임 불일치가 아니다. Unity/Harmony와 게임 setter의 관리 코드 대체물이므로 실제 엔진 동등성이나 성능을 증명하지 않는다.

재현 명령(모두 게임 실행·설치 없음):

```powershell
dotnet build StutterFix.csproj -v q --nologo
dotnet build StutterFix.csproj -p:Edition=Player -v q --nologo
dotnet build StutterFix.csproj -p:Edition=Player -p:AutoTestBuild=1 -v q --nologo
dotnet run --project tools/PosFastCheck/PosFastCheck.csproj -c Release
dotnet run --project tools/PosFastCheck/PosFastCheck.csproj -c Release -- 'D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\A Dance of Fire and Ice_Data\Managed\Assembly-CSharp.dll'
```

## 예상 이득과 Claude 후속 순서

인계에서 제시한 체인 비용 **0.5µs/호출을 가정**하면, 전부 생략될 때 `위치 스텝 수 N × (0.5 − 생략 경로 b) / 1000 ms`이다. N=890, b=예산값 0.15를 넣은 가정값은 **0.3115ms/프레임**이다. b는 이번 실측값이 아니다. 현재는 screen-change guard가 추가되어 실제 생략 비율 q를 곱해야 하고, 원래 호출로 가는 나머지의 조건 검사·기준 재저장 비용도 빼야 한다. q=0이면 호출 절감은 0이며 추가 검사만 남는다. 현재 코드의 이득이 양수라는 근거는 없다.

1. 먼저 이 IL 반례와 범위를 검토한다. 보이는 위치 이동을 실제로 빠르게 하려면 `screenRelativePos`까지 같은 식으로 갱신하는 변경이 필요하다. 그 변경을 채택하기 전에는 현재 코드를 기본 끔으로 유지한다.
2. 메뉴에서 `posfast bench`를 실행해 µs 출력과 `registered/frame=2048`, `sink=0`을 남긴다. 이 수치를 실제 전체 경로 비용으로 확대 해석하지 않는다.
3. 현재 보수적 구현을 시험한다면 PlayerAuto, 같은 해상도·주사율·빌드·설정, groupstep 끔에서 `posfast-on` 없음(A)/있음(B)을 판마다 **새 프로세스로 ABBA** 실행한다. Arche 257.8~278.8초 창에서 `fuse begin/end`의 DecoAnim 갱신 ms와 프레임 수를 비교한다. 성능 판에서는 verify를 끄고 `posfast state`의 skipped/full 및 screen-change 비율도 함께 남긴다.
4. 별도 새 프로세스 B판에서 곡 시작 전에 `posfast verify`를 켜고 곡 전체를 돌린다. 끝에 state의 checked·mismatch·첫 예를 저장한다. 생략·검사 수가 0이면 동일성 검증 통과로 세지 않는다.
5. A/B 각각 곡 끝의 같은 시점에 `decosig`를 남겨 대조한다. 실제 원시 메모 상태와 화면/Transform의 검증 범위가 다르므로 verify와 함께 본다. 전체 곡·재시작·효과 겹침·crop/배율 변경·외부 모드 조건은 이번 오프라인 검사로 완료 처리하지 않는다.

## 산출물과 보존

변경: `PosFast.cs`, `DecoAnim.cs`, `MoveApply.cs`, `AutoTest.cs`, `StutterFix.cs`, `StutterFix.csproj`, `Dormancy.cs`, `tools/PosFastCheck/*`, 이 결과 문서와 검사·빌드·IL 로그. 기존 `.codex/config.toml` 및 기존 미추적 파일은 수정하지 않았다. 빌드 산출물은 저장소 bin/obj에만 만들었다. 설치 훅을 호출하지 않았다.

이 문서는 apply_patch로 첫 문자 U+FEFF를 포함해 작성했다. 작성 후 바이트 선두 EF BB BF, 엄격한 UTF-8 디코딩, 한글 핵심 문장 보존 및 U+FFFD 부재를 확인했다.
