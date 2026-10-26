# 네이티브 트윈 여러 채널 GPU 이어받기 구현 메모

2026-10-26. `docs/handoff/native-tween-adopt-multi.md`에 따라 PositionX 단독 구현을 PositionX/PositionY/Rotation/ScaleX/ScaleY/Color 묶음으로 확장했다. 기본값 `DrawCollapsePolicy.AdoptNativeTweens=false`와 `FastMove.CanAnim`의 InCatchUp 제한을 유지한다. 게임 실행·DLL 설치·커밋은 하지 않았다.

## 확인한 DLL과 IL

현재 게임 `Assembly-CSharp.dll`의 SHA-256을 다시 읽어 기존 추출물과 같은 `F692210F9A342F7EED4BE40AAD463286AB40BF046599959823942EA02F4CA3FF`임을 확인했다. 게임 MVID는 `c0baa18f-e115-4186-bf86-1b2288496232`, DOTween Core 버전은 `1.2.765`다.

- 원본 IL: `tools/adopt-lab/decompiled/ffxMoveDecorationsPlus.il`의 StartEffect와 중첩 클로저.
- 토큰/필드 목록: `tools/adopt-lab/probe-result.json`의 callbackMethods. 현재 DLL 해시가 추출물의 manifest와 일치한다.
- 이번 현재 DLL에서 다시 읽은 호출/필드 IL: `tools/adopt-lab/multi-channel-ilscan.txt`.
- 플러그인 근거: 같은 decompiled 폴더의 `DG.Tweening.Plugins.FloatPlugin.decompiled.cs`, `Vector2Plugin.decompiled.cs`, `ColorPlugin.decompiled.cs`.

재현 명령(PowerShell, 게임 실행 없이 DLL 읽기):

```powershell
$game = 'D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\A Dance of Fire and Ice_Data\Managed\Assembly-CSharp.dll'
Get-FileHash -LiteralPath $game -Algorithm SHA256
$env:METHOD = 'ffxMoveDecorationsPlus'
$env:FIELDS = '1'
dotnet run --project tools/ilscan/ilscan.csproj -c Release -- $game ZZZ
foreach ($suffix in @('0','1','2','3','8','9')) {
    $env:METHOD = 'ffxMoveDecorationsPlus+<>c__DisplayClass47_' + $suffix
    dotnet tools/ilscan/bin/Release/net8.0/ILScan.dll $game ZZZ
}
Remove-Item Env:METHOD
Remove-Item Env:FIELDS
```

StartEffect 토큰은 `0x06001048`이다. 다음 토큰은 같은 MVID에서만 허용한다. 이름이 비슷한 다른 게임 경로·DLL·클로저는 허용하지 않는다. 표에서 0x 접두사는 생략했다.

| 채널 / 게임 Key → GPU 슬롯 | 플러그인과 값 형식 | getter / setter / onUpdate / onComplete 토큰 |
|---|---|---|
| PositionX / 1 → 0 | FloatPlugin, float | `06002EEA / 06002EEB / 06002EEC / 06002EE1` |
| PositionY / 2 → 1 | FloatPlugin, float | `06002EEE / 06002EEF / 06002EF0 / 06002EE2` |
| Rotation / 5 → 4 | FloatPlugin, float | `06002F02 / 06002F03 / 06002F04 / 06002ED8` |
| ScaleX / 7 → 5 | Vector2Plugin, Vector2, AxisConstraint.X | `06002ED9 / 06002EDA / 없음 / 없음` |
| ScaleY / 8 → 6 | Vector2Plugin, Vector2, AxisConstraint.Y | `06002EDB / 06002EDC / 없음 / 없음` |
| Color / 9 → 7,8,9,10 | ColorPlugin, Color, alphaOnly=false | `06002F06 / 06002F07 / 06002F08 / 06002EDD` |

모든 delegate는 단일 호출이며 실제 대상 형식이 메서드 선언 형식과 정확히 같아야 한다. getter/setter/onUpdate는 동일 클로저 객체, onComplete는 아래의 해당 부모 객체여야 한다. `47_N`은 `ffxMoveDecorationsPlus+<>c__DisplayClass47_N`의 약칭이다.

| 채널 | IL에서 확인한 클로저 필드와 참조 경로 | 목표값과 현재값 |
|---|---|---|
| PositionX | `47_2.newPos.x`, `47_2.CS$<>8__locals2` → `47_1`, `47_1.CS$<>8__locals1` → `47_0` | 끝값 `47_1.startPos.x + fx.targetPos.x`; 현재 `newPos.x == dec.pivotPosVec.x` |
| PositionY | `47_3.newPos.y`, `47_3.CS$<>8__locals3` → `47_1`, 이후 위와 동일 | 끝값 `startPos.y + fx.targetPos.y`; 현재 `newPos.y == dec.pivotPosVec.y` |
| Rotation | `47_8.newRot`, `47_8.CS$<>8__locals8` → `47_0` | 끝값 `fx.targetRot`; 현재 `newRot == dec.rotAngle` |
| ScaleX/Y | getter/setter 대상 자체가 `47_0`; getter는 `dec.scaleVec`, setter는 `dec.SetScale(v)` | 생성 Vector2 끝값 `fx.targetScaleV2`의 해당 축; 현재 `dec.scaleVec.x/y` |
| Color | `47_9.newColor`, `47_9.CS$<>8__locals9` → `47_0` | 끝값 `fx.targetColor`; 현재 newColor의 RGBA 모두 dec.color와 비교 |

모든 루트 47_0의 dec가 대상 장식과 참조가 같고, `<>4__this`가 살아 있는 ffxMoveDecorationsPlus여야 한다. PositionX/Y의 onComplete는 47_1, Rotation/Color의 onComplete는 47_0을 대상으로 한다. ScaleX/Y의 onUpdate/onComplete는 둘 다 null이어야 한다.

모두 일반 절대 To 트윈이다. Position의 LastPosition 모드는 DOTween isRelative가 아니라 게임에서 캡처한 startPos + targetPos로 절대 목표가 해결된 경우다. Rotation은 최단각 플러그인이 아니다. Scale은 Decode에서 이미 /100으로 변환한 targetScaleV2를 그대로 쓴다. isRelative=true는 모두 거절한다.

Vector2Plugin은 축 제한 없는 쪽 change를 +0f로 만들고 매 갱신 getter에서 읽은 다른 축을 보존한다. 따라서 해당 축만 GPU에 등록한다. 미사용 끝값 축은 한 축만 지정한 이벤트에서 NaN일 수 있어 검사 대상에서 제외한다. 사용 축 start/end/change/current는 모두 유한해야 하고, 미사용 change는 정확히 +0f여야 한다.

## 묶음과 활성화 경계

1. TryReadNativeChannels는 eventTweens의 모든 active 트윈과 Running Rec를 확인한다. 실행 중 항목이 모두 위 6종의 검증된 네이티브일 때만 NativeAdoptSpec 한 묶음을 반환한다. 하나라도 실패하거나 미지원 키·Running Rec가 섞이면 부분 후보를 반환하지 않고 장식 전체를 기존 CPU 경로에 둔다. 활성 항목 없는 장식은 거절 수에 더하지 않는다.
2. 기존 PositionX와 같은 공통 안전 조건을 유지한다. 초기화 완료·재생 중·정방향·Normal/scaled/timeScale=1·지연 없음·Restart 한 번·autoKill, from/blend/speed/sequence/link/customEase/추가 식별자 없음 등을 요구한다. snapping, Color.alphaOnly, 잘못된 scale 축 옵션도 거절한다. 콜백/플러그인/Despawn의 Harmony 패치, 다른 소유자의 Update/Kill 패치, ForeignSetter/QuartzOptimizer도 거절한다. 필드/구조 읽기 실패도 CPU 유지다.
3. 분류 → 캡처 직전 → commit 직전에 묶음을 다시 읽어 사전·모든 트윈·클로저·Fx 참조와 Key/start/end/change/Pos/Dur/Ease/Over/Period/current를 비교한다. Key 순서로 정렬하므로 사전 열거 순서에 의존하지 않는다.
4. 원래 전체 Dur와 현재 Pos를 보존해 네이티브 트윈별 독립 소스를 준비한다. 서로 다른 채널의 시계가 같아 보여도 합치지 않는다. Color만 RGBA 4슬롯을 같은 소스에 등록한다. 현재값이나 남은 시간으로 새 곡선을 만들지 않는다.
5. 클로저 현재값·장식 현재값·기존 master evaluator 예상값을 비트 비교한다. start/end/change, SetMasterSlotAnimated 결과 change와 캡처/commit 시 master 현재값도 비트 비교한다. Scale 완료 슬롯은 기존 Vector 경로처럼 start + change * Eval(Dur)이고, Position/Rotation/Color는 원래 onComplete와 같은 목표값이다.
6. 캡처에서는 inactive proxy와 GPU 상태만 준비한다. 원본 사전/트윈은 건드리지 않는다. 첫 디스패치 및 CommitGpuActivationUndo의 전체 묶음 검증이 성공한 뒤 CommitNativeAdoptions가 모든 채널에 Kill(false)를 호출하고 각각의 원래 Key에 proxy를 넣는다. commit 전 예외는 기존 rollback으로 GPU 준비 상태를 폐기하며 원본 native 참조·진행·active·재생 상태를 유지한다.
7. commit 이후 예상 밖의 Kill 실패는 기존 GPU→CPU 복구로 돌아간다. NativeStillOwns를 모든 채널 Key로 넓혀 살아 있는 원본에는 Rec를 중복 생성하지 않는다. 제거된 채널은 일반 GpuResumeSpec으로 복원한다. 죽인 원래 DOTween 객체를 부활시키지는 않는다. 채택/교체는 묶음 전체를 대상으로 하며 commit 전 부분 채택은 없다. 비가역 Kill 도중 예외까지 원본 객체 동일성으로 되돌린다는 보장은 하지 않는다.
8. opt-in Update prefix/finalizer와 첫 프레임 보정, 꺼짐/Shutdown 때 후크 제거는 기존 방식이다. 후보 참조는 commit/rollback 후 정리한다.

복원 Key는 BuildGpuResumePlan의 first<=4?first+1:... 표와 맞췄다. 특히 Color first=7은 **Key=9**, ScaleY first=6은 **Key=8**이다. Color는 한 GpuResumeSpec의 CStart/CEnd/CChange, ScaleY는 기존 복원 코드의 벡터 성분 1로 전달된다. 원래 네이티브 생성 순서는 관측하지 않았으므로 OrderObserved=false를 유지한다.

## 진단과 변경 파일

AUTOTEST 명령은 GPU 소유권이 꺼진 상태에서 `gpudeco light adoptnative on|off`다. 상태 로그의 PositionX 단독 표기를 6종 채널/장식 묶음 표기로 고쳤다.

`[GPU 장식] 이어받기: N개(장식), 채널 M개, 값 불일치 K개, 거절 …`

N은 장식 수, M은 native 트윈 채널 수다(Color는 1개, GPU 슬롯은 4개). K는 commit 검증에서 master와 다른 스칼라 성분 수다. 읽기 단계 값 불일치는 거절 15번으로 CPU에 남는다. nativeCombos/NativeRejectSummary는 실패한 묶음 전체를 Key 순서로 정렬하고 (nat)/(rec)를 붙여 집계한다. 다중 채널 자체는 이제 거절 사유가 아니다.

거절 코드: 1 갱신 경계, 2 미지원 키/Rec 혼합, 3 타입/옵션/기본 상태, 4 호환/버전/패치, 5 from 등 플래그, 6 수명/추가 콜백, 7 link/manager 갱신 중, 8 플러그인/제거 경로 패치, 9 delegate 토큰/대상, 10 완료 클로저, 11 장식 참조, 12 Fx, 13 비유한 값, 14 끝값/change/시간/ease/scale 미사용 change, 15 현재값 비트 불일치, 16 읽기 예외.

- GpuDecoNativeAdopt.cs: 장식별 채널 묶음, 6종 타입·토큰·클로저 검사, 채널별 소스/Color 묶음 캡처, 전체 검증/commit, 다중 Key 복구 보호, 거절 조합/채널 수 로그.
- GpuDecoActivation.cs: PositionX 전용 읽기 대신 묶음 분류 함수를 호출한다.
- GpuDecoExport.cs: 장식별 묶음을 한 번에 캡처하고 각 native Key/참조가 해당 묶음에 포함되는지 확인한다. 설정 상태 로그도 갱신했다.
- GpuDecoResume.cs의 기존 일반 Key/성분 변환을 사용한다. 이번에는 이 파일에 추가 수정하지 않았다.

## 남은 위험과 게임 시험 순서

실제 이어받기 수·픽셀·이후 프레임의 Unity Mono/GPU 산술·FPS는 확인하지 않았다. 현재값 비트 일치가 미래 dt/이징·GPU 반올림·scale의 다른 축 보존·복원 생성 순서까지 증명하지는 않는다. Fx/클로저 변경은 commit 직전까지만 검사하며 Harmony 외 detour는 검출을 보장하지 않는다. 채널별 소스 증가로 기존 용량에 닿으면 commit 전 활성화 자체가 rollback될 수 있다.

Claude 측 게임 시험:

1. 같은 Arche 254.4초 시작에서 adoptnative off/on을 비교한다. N/M, 거절 번호/조합, CPU_fallback 감소량을 기록한다. PositionX+PositionY+Rotation, 여기에 Color가 붙은 조합, Color 단독, ScaleX+ScaleY 및 한 축만 지정한 scale을 확인한다. 여러 곡 중간 지점에서도 반복한다. 인계의 247개가 모두 채택된다고 가정하지 않는다.
2. 미지원 키·Running Rec 혼합·잘못된 콜백/옵션·값 불일치가 포함된 장식은 전체가 CPU에 남고 원래 모든 native entry가 그대로인지 확인한다.
3. 같은 일시정지 장면에서 GPU on 화면을 저장하고 재생 없이 gpudeco live off 후 픽셀/논리값을 비교한다. X/Y/회전/크기/RGBA를 포함하고 Color source 혼합이 없는지와 Key=9, ScaleX=7/ScaleY=8의 복원을 확인한다. HUD 영향 0.7% 이내와 장식 픽셀 차이를 분리한다.
4. activation_after_record, activation_after_capture, activation_after_dispatch 실패 주입으로 모든 원본 native entry 참조·active·Elapsed·IsPlaying과 renderer 복원을 확인한다. 기존 activation fixture는 모든 Key를 순회한다. 한 묶음에 3~4개 이상 채널이 있는 대상을 포함한다.
5. 일시정지/재개와 native Update 전/후 활성화 첫 프레임에서 누락/이중 진행을 확인한다. 재생 중 live off 후 모든 채널의 원래 Pos/Dur/Start/Change가 이어지는지, 곡 끝·포털·편집 정지·재시도·scene reset의 표시 복원과 source/Rec 잔량을 확인한다. commit 도중 제거 실패의 복구도 별도 확인한다.

## 빌드

최종 소스 기준 요청된 두 구성과 공백 검사를 확인한다. 실제 게임 시험은 수행하지 않는다.

| 명령 | 결과 | 로그 |
|---|---|---|
| dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1 | 성공, 오류 0, 경고 46 | tools/adopt-lab/multi-build-playerauto.txt |
| dotnet build -c Release -p:Edition=Player | 성공, 오류 0, 경고 46 | tools/adopt-lab/multi-build-player.txt |
| git diff --check | 통과 | 공백 오류 없음 |

GpuDecoNativeAdopt.cs의 새 빌드 경고는 없다. 참조 버전 충돌·사용 중단 API·미사용 필드 등의 경고는 남아 있다. 산출물은 bin/PlayerAuto/StutterFix.dll, bin/Player/StutterFix.dll이며 게임 폴더에 복사하지 않았다. 기존 작업 트리의 다른 변경을 되돌리거나 커밋하지 않았다.

## 속도

2026-10-26. `docs/handoff/native-tween-adopt-speed.md`에 따라 분류·캡처·commit에서 반복하던 전체 읽기를 제거했다. 장식별 `NativeAdoptSpec`/채널/값 배열은 분류 때 한 번 만들고 활성화가 끝날 때까지 재사용한다.

- 활성화 캐시: QuartzOptimizer, ForeignSetter, DOTween.Version, Update 패치 허용 여부, manager의 `_TweenLinks` 참조와 `isUpdateLoop` 상태를 시작 때 한 번 읽는다. 플러그인 3종의 EvaluateAndApply, Despawn, Kill 및 실제 callback 메서드의 Harmony 패치 결과는 메서드와 자기 패치 허용 여부별로 한 번만 조회한다. 이 결과는 다음 활성화로 넘기지 않는다. `_TweenLinks.Contains(tween)`은 각 트윈에서 계속 확인한다.
- 정적 캐시: TweenManager/DOTweenComponent 형식, 게임 Module/MVID 일치 여부, 메서드 조회 결과, 형식·필드명·반환형별 필드 읽기 델리게이트를 보관한다. 최초 읽기에서 FieldInfo를 찾고 DynamicMethod의 ldfld/ldsfld로 델리게이트를 만든다. emit을 지원하지 않는 런타임에서는 캐시한 FieldInfo.GetValue로 읽는다. 필드 누락·형식 불일치·읽기 실패는 기존처럼 CPU 유지 또는 commit 전 rollback으로 처리한다. 읽기 델리게이트는 대상 객체를 캡처하지 않는다.
- 캡처/commit 재확인: 전체 사전의 실행 항목 수·Key·트윈 참조, Rec 혼합 여부, 트윈의 기본 상태/옵션/시간, 저장한 delegate의 동등성, 클로저 링크·dec·Fx 참조와 목표값을 확인한다. Start/End/Change/Elapsed/Duration/ease 인수 및 클로저·장식 현재값은 저장한 값과 비트 비교한다. 새 묶음·배열·호출 목록을 만들지 않으며 메타데이터와 Harmony 패치 정보를 다시 조회하지 않는다. commit 직전 GPU master 현재값의 비트 비교도 유지한다.
- 수명: 성공·rollback 및 분류 단계 예외에서도 후보 묶음과 활성화 패치/manager 캐시를 비운다. MVID+MetadataToken 고정, 장식 전체 채택, 모든 묶음 검증 뒤 Kill(false), 기본 꺼짐과 첫 프레임 보정은 유지한다.
- AUTOTEST 시간 로그: `[GPU 장식] 이어받기 검사 Xms / 캡처 Yms / commit Zms`. 검사는 활성화 공통 검사와 분류 읽기, 캡처는 소스 ID 준비·가벼운 재확인·proxy/source/채널 준비, commit은 최종 검증과 Kill/proxy 교체를 포함한다. 실패 시에도 진행한 단계의 누적 시간을 남긴다.

최종 소스의 빌드/정적 검증 결과:

| 명령 | 결과 | 로그 |
|---|---|---|
| dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1 | 성공, 오류 0, 경고 46 | tools/adopt-lab/speed-build-playerauto.txt |
| dotnet build -c Release -p:Edition=Player | 성공, 오류 0, 경고 46 | tools/adopt-lab/speed-build-player.txt |
| git diff --check | 통과 | 공백 오류 없음 |

GpuDecoNativeAdopt.cs의 빌드 경고는 없다. 게임 실행·DLL 설치·커밋 및 실행 시험은 하지 않았다. **247장식/707채널 합계 20ms 이하 달성 여부와 게임 내 정확성은 이번 빌드만으로 판정하지 않는다.** 다음 허용된 게임 측정에서 세 시간의 합과 기존 채택 수·값 불일치·복구 동작을 함께 확인해야 한다.

## 남는 거절

2026-10-26. `docs/handoff/native-tween-adopt-leftover.md`에 따라 기존 `NR(3)`의 일반 상태·Float/Vector/Color 옵션을 분리했다. 다른 복합 거절도 상태·콜백·플러그인·시간·값별 `NativeReject` 코드로 나눴다. 과거 번호 2~15는 아래 새 번호로 대체되므로 이전 로그의 3·14·15와 직접 비교하지 않는다. 읽기 예외 16과 갱신 중 1은 유지한다.

AUTOTEST 로그는 `거절 35=2(Uninitialized) ... [PositionX(nat)+Color(nat)+ code=35/Uninitialized channel=Color]=2`처럼 코드별 장식 수와 전체 실행 채널 조합, 실제 실패 채널을 함께 표시한다. 이 예시는 형식 설명이며 게임 측정값이 아니다. 장식마다 사전 검사 순서상 **처음 실패한 조건 하나**만 집계한다. 캡처·commit 재검사는 이 수를 늘리지 않는다. 활성화 시작 시 집계를 초기화하고, 요약 조회는 집계를 지우지 않아 분류 로그와 commit 로그가 같은 내용을 볼 수 있다. 분류 직후에도 `이어받기 분류: 후보 ... 거절 ...`를 출력하므로, 이후 정렬 장벽이나 렌더러 검증에서 실패해도 원인을 읽을 수 있다. 분류 후보 수는 실제 commit 성공 수가 아니다.

### 코드별 의미와 유지 사유

아래 조건은 모두 계속 거절한다. 유일한 허용 확대는 표 다음의 **이미 완료된 지연**이다. 비트 비교를 허용 오차 비교로 바꾸거나, 미지원 장식을 부분적으로 이어받지 않는다.

| 코드 / 로그 이름 | 정확한 조건 | 계속 거절하는 이유 |
|---|---|---|
| 1 UpdateRunning / 20 HookFrame | native Update 실행 중 / 재생 중 후크 설치 프레임 | 이중·누락 갱신 방지 |
| 16 ReadException | 필드·메타데이터·상태 읽기 예외 | 확인 못 한 구조를 추정하지 않음. `ex=`에 예외 표시 |
| 21 RunningRec / 22 UnsupportedKey | 실행 Rec 혼합 / 지원하지 않는 Key | 장식 전체 소유권·채널 계약 유지 |
| 30 Inactive | `!active` | 살아 있는 원본만 채택 |
| 31 Id / 32 Target / 33 StringId / 34 IntId | id·target·stringId가 null 아님 / intId가 -999 아님 | 외부 식별·제어 동작을 제거할 수 있음 |
| 35 Uninitialized | `!IsInitialized()` | 아직 getter로 확정하지 않은 start/change를 사용하면 안 됨 |
| 36 NotPlaying / 37 Complete | `!IsPlaying()` / `IsComplete()` | GPU 시계는 개별 정지·완료 객체를 표현하지 않음 |
| 38 Relative / 39 Backwards / 40 TimeScale | isRelative / isBackwards / timeScale 비트가 1f와 다름 | 절대값·단일 전진 시계 계약 |
| 41 DelayNonfinite / 42 DelayNegative | Delay가 NaN·무한 / 음수 | 잘못된 지연 상태 |
| 43 ElapsedDelayNonfinite / 44 ElapsedDelayNegative | ElapsedDelay가 NaN·무한 / 음수 | 잘못된 경과 상태 |
| 45 DelayRemaining | ElapsedDelay < Delay | GPU와 CPU 복원에 남은 지연 표현이 없음 |
| 46 DelayElapsedMismatch | ElapsedDelay와 Delay 비트 불일치 | 경과가 지연을 초과하거나 부호 비트까지 다른 비정상 상태 제외 |
| 47 Loops / 48 CompletedLoops | Loops != 1 / CompletedLoops != 0 | 단일 루프만 지원 |
| 50 Inverted / 51 IndependentUpdate / 52 From | isInverted / isIndependentUpdate / isFrom | 시간·곡선 계약 다름 |
| 53 Blendable / 54 SpeedBased / 55 Sequenced | isBlendable / isSpeedBased / isSequenced | 합성·속도·시퀀스 의미 미지원 |
| 60 UpdateType / 61 LoopType | Normal 아님 / Restart 아님 | 갱신·루프 계약 다름 |
| 62 DelayIncomplete | `!delayComplete` | 정확히 지연 끝에 도달해도 DOTween이 아직 시작하지 않았을 수 있음 |
| 63 AutoKill / 64 SequenceParent / 65 SpecialStartup | autoKill=false / 부모 sequence 존재 / specialStartupMode != 0 | 수명·시작 동작 보존 불가 |
| 66 CustomEase | customEase 존재 | GPU·복원 evaluator에서 같은 함수를 보장 못 함 |
| 67 OnStart / 68 OnKill / 69 OnPlay / 70 OnPause | 해당 추가 callback 존재 | Kill/교체가 callback 의미를 삭제함 |
| 71 OnRewind / 72 OnStepComplete / 73 OnWaypointChange | 해당 추가 callback 존재 | 지원하지 않는 callback 의미 |
| 80 LinksUnavailable / 81 Linked / 82 ManagerUpdating | link 사전 없음 / 해당 tween link 존재 / manager 갱신 중 | 외부 수명·갱신 경계 확인 불가 |
| 83 PluginType / 84 PluginPatched | 플러그인 실제 형식 다름 / EvaluateAndApply 패치 존재 | 원래 산술·setter 계약 불명 |
| 85 DespawnPatched / 86 KillPatched | Despawn / 외부 소유자 Kill 패치 존재 | 제거 동작 보장 불가 |
| 90 ScaleType / 91 ScaleSnapping / 92 ScaleAxis | 정확한 Vector2 TweenerCore 형식 아님 / snapping / 예상 X·Y 축 아님 | 지원한 scale 플러그인 옵션과 다름 |
| 93 ColorType / 94 ColorAlphaOnly | 정확한 Color TweenerCore 형식 아님 / alphaOnly | RGBA 네 슬롯과 alpha 전용 getter/setter 동작을 혼동하지 않음 |
| 95 FloatType / 96 FloatSnapping | 정확한 float TweenerCore 형식 아님 / snapping | 지원한 float 산술과 다름 |
| 100 Getter / 101 Setter | 고정 MVID·토큰·단일 delegate·대상 참조·패치 검사 실패 | 알려진 클로저만 읽음 |
| 102 ScaleOnUpdate / 103 ScaleOnComplete | scale에 예상하지 않은 해당 callback 존재 | 원래 지원 경로는 둘 다 null |
| 104 OnUpdate / 105 OnComplete / 106 CompleteClosure | 해당 delegate 검증 실패 / 완료 클로저 참조 다름 | callback·대상 계약 불일치 |
| 110 Decoration / 111 Fx | 루트 dec가 대상과 다름 / Fx가 살아 있지 않음 | 다른 장식이나 죽은 효과의 값을 사용하지 않음 |
| 120 PositionNonfinite / 121 DurationNonfinite | Pos / Dur 비유한 | 잘못된 시계 |
| 122 OvershootNonfinite / 123 PeriodNonfinite | Over / Period 비유한 | 잘못된 이징 인수 |
| 124 StartNonfinite / 125 EndNonfinite / 126 ChangeNonfinite | 해당 성분 비유한 | 잘못된 곡선 |
| 127 CurrentNonfinite / 128 ExpectedNonfinite / 129 LogicalNonfinite | 클로저 현재값 / Fx 목표값 / 장식 현재값 비유한 | 비교·캡처 불가 |
| 130 Duration / 131 PositionNegative / 132 PositionAtEnd | Dur <= 0 / Pos < 0 / Pos >= Dur | 유효한 진행 구간 밖 |
| 133 EaseUnset / 134 EaseUnsupported | Unset / GPU Supported 밖 | 기존 evaluator의 의미 유지 |
| 135 ScaleUnusedChange | scale 미사용 축 change가 +0f 비트 아님 | 반대 축까지 변하는 곡선은 지원하지 않음 |
| 140 EndMismatch / 141 ChangeMismatch | Fx 목표와 tween 끝값 비트 불일치 / End-Start와 Change 비트 불일치 | 원래 곡선을 추정·보정하지 않음 |
| 150 ClosureValueMismatch / 151 LogicalValueMismatch | 클로저 / 장식 현재값이 원래 곡선 예상값과 비트 불일치 | 기존 현재값 일치 검사 그대로 유지 |
| 160 QuartzOptimizer / 161 ForeignSetter / 162 Version / 163 UpdatePatched | 해당 호환 모드 / 외부 setter / DOTween 버전 다름 / 외부 Update 패치 | 확인한 원본 동작만 채택 |

### 안전하게 푼 조건: 완료된 지연만 허용

기존 `Delay()!=0 || ElapsedDelay()!=0`는 **지연이 과거에 끝난 진행 tween**까지 거절했다. 이제 둘 다 유한·0 이상이고 두 값의 비트가 같으며 `delayComplete=true`인 경우만 허용한다. 초기화·재생·정방향·1회 루프 및 모든 기존 값·콜백 검사는 그대로 통과해야 한다. 분류 시 Delay/ElapsedDelay도 저장하고 캡처·commit 직전에 비트로 재확인한다. 남은 시간으로 duration을 바꾸거나 elapsed에 지연을 더하지 않는다.

현재 게임 DLL 해시는 기존 메모와 같은 `F692210F9A342F7EED4BE40AAD463286AB40BF046599959823942EA02F4CA3FF`로 다시 확인했다. 설치된 DOTween DLL을 읽어 다음을 확인했다(게임 실행 없음).

- `TweenManager.Update`: `!delayComplete`일 때만 UpdateDelay를 실행하며, 이후 position에 dt를 더한다.
- `Tweener.DoUpdateDelay`: elapsed > delay일 때 elapsedDelay=delay, delayComplete=true로 만들고 초과 시간만 반환한다. **elapsed == delay는 아직 완료가 아니다.** 따라서 두 float가 같다는 이유만으로 62번을 풀지 않았다.
- `TweenManager.Goto`: delayComplete=true, elapsedDelay=delay를 설정한다. 완료된 지연은 이 경로에서도 생길 수 있다.
- `GpuLiveSource`와 `DecoAnim.GpuResumeSpec`은 Dur/Pos/Ease/Over/Period를 보존하지만 지연·지연 경과 필드가 없다. GPU 갱신은 `Pos + dt`이며 Over/Period는 이징 인수다. 남은 지연을 Over/Period나 음수 Pos로 대신 표현하지 않는다.

읽은 코드: `out/adopt-leftover/TweenManager.cs`, `Tweener.cs`, `TweenExtensions.cs`, `ffxMoveDecorationsPlus.cs`. 저장소 측 근거는 `GpuDecoExport.cs`의 GpuLiveSource/AllocateGpuLiveSource/시계 갱신, `GpuDecoGpu.cs`의 Supported, `GpuDecoResume.cs`, `DecoAnim.GpuResume.cs`다.

### 남은 1~5개에 대한 추정과 한계

현재 `ffxMoveDecorationsPlus.StartEffect`의 Color는 일반 `DOTween.To(Color)`이고 alphaOnly 옵션을 설정하지 않는다. Opacity는 별도 float 채널이다. 이 경로 자체에는 SetDelay/SetLoops/SetRelative도 없으며 `Done()`은 양수 duration을 그대로 반환한다. 따라서 **완료된 지연 허용이 기존 1~5개를 해결했다고 주장할 근거는 없다.** 외부 설정·패치 여부는 실제 객체 검사가 결정한다.

코드에서 가능한 후보는 새 tween이 첫 native Update 전이라 startupDone=false인 경우(35), 개별 pause 또는 기본 autoPlay 변경으로 IsPlaying=false인 경우(36), 실제 형식·옵션 차이(93/94/95/96), 식별자(31~34)다. 특히 중간 시작 직후 일부 이벤트만 생성된 경계라면 35가 가능하다. 새 로그로 확인해야 하며 실제 원인은 미확정이다. 미초기화 tween은 DOTween Startup이 getter에서 시작값을 정하는 시점이 아직 오지 않았으므로 임의 초기화하거나 0으로 추정하지 않았다. 정지 tween은 GPU의 공통 전진 시계에 그대로 실을 수 없다. alphaOnly와 미지원 easing도 같은 이유로 유지한다. easing은 기존 NR(14)이므로 **기존 NR(3)으로 보고된 건의 직접 원인은 아니다.**

장식 전체 채택, 기본 꺼짐, InCatchUp 제한, commit 전 현재값 비트 비교, 원래 start/change/Pos/Dur 보존과 Kill(false) 순서는 유지한다. 정렬 tie 예외를 무시하거나 강제로 장식을 소유하지 않는다. **거절 0개·픽셀·복원·FPS는 이번 작업에서 확인하지 않았다.** 다음 허용된 게임 측정에서 분류 로그의 코드/채널로 판단해야 한다.

### 검증

| 검사 | 결과 | 로그 |
|---|---|---|
| `dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1` | 성공, 오류 0, 경고 46 | `out/adopt-leftover/build-playerauto.txt` |
| `dotnet build -c Release -p:Edition=Player` | 성공, 오류 0, 경고 46 | `out/adopt-leftover/build-player.txt` |
| `py -3 out/adopt-leftover/check-contract.py` | 127개 오프라인 검사 통과 | `out/adopt-leftover/contract-check.txt` |
| `git diff --check` | 통과 | 공백 오류 없음 |

오프라인 검사는 실제 소스에서 enum·NativeTweenContract·필드 reader·거절 집계 메서드를 추출해 현재 DOTween DLL과 함께 실행한다. Unity 객체 생성·DOTween 초기화·게임 업데이트 없이 DOTween manager에 등록하지 않은 메모리 객체에 상태를 설정한다. 완료된 양수 지연, 남은 지연, 지연 끝이지만 미완료인 경계, NaN/음수·경과 불일치, 미초기화·개별 정지·루프·갱신 옵션·callback 거절, Color 완료 지연, 코드/조합/실패 채널 집계와 초기화, 번호 중복·범위를 검사한다. Harmony 패치 검사는 fixture에서 대역을 사용하므로 호환성·실게임·GPU·미래 프레임 일치 시험은 아니다. Delay/ElapsedDelay 스냅샷 및 기존 비트 검사 유지도 정적으로 확인한다.

게임 실행·DLL 설치·커밋은 하지 않았다. 기존 작업 트리 변경은 유지했다.
