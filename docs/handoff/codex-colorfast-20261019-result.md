# 색 처리 직통 경로 작업 결과 — 2026-10-19

## 완료 범위와 판정

`ColorFast.cs`를 추가하고 `DecoAnim.OnUpdate`와 `OnComplete`의 키 9/10에 연결했다. 기본 끔이며 로드 때 `colorfast-on` 파일을 한 번 읽는다. `ColorFast`는 `DecoEngine`을 참조하지 않는다. 기존 `AlphaWillSet` 호출은 유지했고, 그 함수는 엔진이 꺼져 있으면 무동작이다. TightLate의 계산·쓰기 정책은 바꾸지 않았다.

게임 실행, Mods 설치, 커밋은 하지 않았다. 실게임 성능·화면 동일성·곡 끝 decosig는 미검증이다. 지금 판정은 **기본 끔 실험 구현 및 오프라인 검증 완료, 실게임 채택 보류**다.

| 검증 | 결과 |
|---|---|
| Dev | 빌드 오류 0, 경고 35 |
| Player | 빌드 오류 0, 경고 33 |
| PlayerAuto | 빌드 오류 0, 경고 35 |
| ColorFastCheck | 확인 41,010개 통과; 직통 호출 16,384건, 순수 계산 표본 512건 |
| 소스 인코딩 검사 | `SFENC001: clean` |
| 게임 안 colorfast / tightlate / late-verify / decosig | 실행하지 않음 |
| colorfast bench | 구현했으나 실행하지 않음; 호출당 실측 µs 없음 |

빌드는 `dotnet build StutterFix.csproj`에 Edition/AutoTestBuild 속성만 달아 실행했다. 설치 스크립트는 호출하지 않았다. 빌드 로그는 `%TEMP%/colorfast-{dev,player,playerauto}-final-build.log`다. 경고는 참조 버전 충돌·기존 obsolete API·미사용 필드 등이며, 새 코드 경고 유무도 마지막 로그에서 확인했다.

## 1. 원본 IL과 비트 동일성의 범위

시작점인 `decoengine2-evidence/ApplyColor-il.md`를 전부 읽고, 현재 참조 DLL에서도 색·가시성·위치 관련 메서드를 직접 IL로 읽었다. 현재 `Assembly-CSharp.dll`의 MVID는 `d4aeee2e-422b-4bba-92d1-a78531be3227`이다.

중요한 정정: 이 DLL의 `scrVisualDecoration.ApplyColor`에는 **UpdateRendererEnable 호출이 없다**. 82바이트 전체는 다음 동작뿐이다.

1. `f = stickToFloor ? parentFloor.opacity : 1f`.
2. `rendererColor = color.WithAlpha(color.a * opacity * f)`; 곱셈 순서는 IL `0033 mul`, `0035 mul`이다.
3. IL `004C`에서 `spriteRenderer.color = rendererColor`를 한 번 쓴다.

`SetColor`와 `SetOpacity`는 해당 필드 하나를 저장한 뒤 가상 `ApplyColor`만 부른다. `WithAlpha(Color,float)`의 전체 IL도 읽었다. RGB를 순서대로 읽고 입력 알파와 함께 `new Color(r,g,b,a)`를 만든다. 직통 경로가 쓰는 생성자와 같다.

`ApplyColor`에는 enabled, rendererEnabled, 콜라이더, 마스크, Transform, lateSkipped 등의 쓰기나 관련 호출이 없다. `SetVisible`이 별도로 rendererEnabled/spriteRenderer.enabled/spriteMask.enabled를 쓴다. `GetAlpha`는 rendererColor.a 반환뿐이다. 정확한 visual 타입에서 rendererEnabled가 true이고 알파가 양수이면 `GetVisible`은 마스크·히트박스 분기 전에 true를 반환한다.

추가로 전체 게임 DLL의 `scrDecoration.color/opacity/rendererColor/rendererEnabled`에 대한 `stfld` 및 `ldflda`를 검색했다. visual 타입에 적용되는 쓰기는 생성자, SetColor, SetOpacity, ApplyColor, SetVisible뿐이다. 나머지 rendererColor/rendererEnabled 쓰기는 다른 object/text 타입 메서드다. 주소 읽기는 ApplyColor의 color 및 GetAlpha의 rendererColor 등이다.

오프라인 확인 명령:

```powershell
dotnet run --project tools/ColorFastCheck/ColorFastCheck.csproj -c Release
dotnet run --project tools/ColorFastCheck/ColorFastCheck.csproj -c Release --no-build -- --il '<Managed>/Assembly-CSharp.dll'
dotnet run --project tools/ColorFastCheck/ColorFastCheck.csproj -c Release --no-build -- --writers '<Managed>/Assembly-CSharp.dll'
```

### 직통 허용 조건

- 정확히 `scrVisualDecoration`; 상속 타입은 제외.
- `!stickToFloor`; 타일 알파 경로는 통째로 제외.
- rendererEnabled가 true이고 직전 rendererColor.a가 `> 0f`.
- InvisibleSkip/Dormancy가 켜져 있고 `Precheck.Active == 0`.
- 원래 순서의 `c.a * o * 1f`가 `> 0f`; 0·음수·NaN은 제외.
- SpriteRenderer가 살아 있고 forceRenderingOff가 false.
- 로드 때 검사한 Harmony 패치가 허용 목록에 해당하며 이전 검증 실패가 없음.

색 기록은 color만, 불투명도 기록은 opacity만 저장한다. rendererColor와 spriteRenderer.color는 원래와 같은 순서로 저장한다. 같은 Color라도 **SpriteRenderer.color 쓰기는 생략하지 않는다**. 엔진의 동일값 dirty 처리·알림 부수효과를 추정하여 삭제하지 않았다. OnComplete도 연결해 마지막 입력 및 Kill(true)의 완료 처리를 같은 정책으로 처리한다.

### InvisibleSkip / Dormancy를 생략하는 근거와 경계

`InvisibleSkip.Work`는 양수 알파일 때 hidden 목록에서 꺼내는 일만 한다. 정상 상태에서 hidden 등록은 알파 `<=0`에서 forceRenderingOff=true와 함께 이루어지고, 양수 전환은 기존 ApplyColor가 hidden 제거·Precheck.Touch·LazySprite.Flush·위치 보정·forceRenderingOff 해제를 처리한다. 따라서 직전 양수이고 forceRenderingOff=false인 보임 유지 호출에서는 그 작업이 없다. Precheck가 하나라도 활동 중이면 보수적으로 기존 경로를 사용한다.

`Dormancy.Sleep`은 `IsDormant`가 참일 때만 호출된다. 정확한 visual에서 rendererEnabled=true·알파 양수이면 GetVisible=true이므로 잠들 수 없다. 잠든 뒤 알파가 양수로 바뀌는 첫 호출은 직전 알파 조건에 걸려 원래 ApplyColor/WakePost로 간다. rendererEnabled가 false인 채 색만 양수가 된 경우도 직통에 들어오지 않으며, SetVisible(true)는 원래 WakePost를 유지한다. 따라서 정상 게임 필드 쓰기와 기존 모드 훅을 거친 **깨어 있는 상태의 유지**만 우회한다. 깨워야 할 전환은 원래 경로다.

이 근거는 현재 DLL과 확인한 모드 코드의 상태 전이에 관한 것이다. 외부 모드가 필드를 직접 덮어쓰거나 숨김 목록과 forceRenderingOff의 관계를 깨는 특수 진단은 이 증명의 범위 밖이다. gpuhide/cull 등 화면을 바꾸는 진단과 함께 성능·동일성을 판정하지 않는다.

### 호환성과 수명

로드 시 SetColor/SetOpacity/ApplyColor/GetAlpha/GetVisible/SetVisible 및 생략하는 WithAlpha/InvisibleSkip.After/Dormancy.WakePost 자체의 패치 메서드를 검사한다. ApplyColor의 InvisibleSkip.After·Dormancy.WakePost, SetVisible의 Dormancy.WakePost, 색 setter의 FrameParts 진단 패치만 허용한다. 그 외 prefix/postfix 및 모든 transpiler/finalizer는 원래 경로로 돌린다. 이름이나 소유자 문자열만으로 넓게 허용하지 않는다.

패치 검사는 로드 시의 스냅샷이다. 나중에 설치되는 다른 계측 패치를 자동으로 발견하지는 않는다. 이번 비교에서는 updatecost/SlowScan 같은 setter 계측을 뒤늦게 추가하지 말고 동일 프로세스 구성으로 측정한다. 게임 DLL이 바뀌면 IL 검토도 다시 해야 한다.

새 Harmony 패치·이벤트·PlayerLoop·장식 참조 목록은 없다. `Main.Unload`에서 `ColorFast.Uninstall`이 Enabled/검증/호환 상태를 해제한다. 기존 다른 변경과 미추적 파일은 보존했다.

## 2. 검증 명령과 오버헤드

`colorfast state`는 enabled, compatibility, direct, verify, checked, mismatch, failed, first와 다음 fallback 배열을 출력한다.

| 인덱스 | 사유 |
|---|---|
| 0 | 호환성 미확인/거절 또는 검증 실패 |
| 1 | null 또는 정확한 visual 타입 아님 |
| 2 | stickToFloor |
| 3 | rendererEnabled=false 또는 직전 알파가 양수 아님 |
| 4 | InvisibleSkip/Dormancy 꺼짐 또는 Precheck 활동 중 |
| 5 | 새 알파가 양수 아님 |
| 6 | SpriteRenderer 없음/파괴 또는 forceRenderingOff |

기본 끔일 때 DecoAnim에서는 Enabled 분기만 추가된다. 켰을 때도 매 호출 할당·시계·새 해시 조회는 없다. renderer null/forceRenderingOff 확인에는 Unity 호출 비용이 있어 이득을 상쇄할 수 있다. 실제 결과로 판단해야 한다.

Dev/PlayerAuto에서 `colorfast verify`를 실행하면 직통 32회 중 1회에 순수 원본 계산을 한다. color/opacity/rendererColor/spriteRenderer.color를 비트 비교하고, forceRenderingOff=false, enabled의 호출 전후 동일, rendererEnabled=true를 확인한다. 기존 ApplyColor를 라이브 장식에 재호출하지 않는다. 불일치가 하나라도 있으면 그 로드 동안 직통을 중지하고 이후 호출을 기존 경로로 돌린다. `verify off`는 실패 상태를 지우지 않는다.

이 검증은 순수 계산과 공개 렌더 상태의 비교다. hidden/dormant/lazy 내부 목록을 추가 해시 조회로 검사하지 않는다. 그 목록은 위 상태 전이 근거와 후속 게임 검증으로 확인해야 한다. Unity의 네이티브 dirty 내부 상태를 읽거나 증명한 것은 아니다. 원래의 네이티브 쓰기를 유지한 이유다.

`tools/ColorFastCheck`는 **실제 ColorFast.cs를 링크**하고 관리 코드 Unity/Harmony 대체물로 검사한다. 임의 RGB 비트 패턴, 색·불투명도 각각의 원본 setter 대조, 0/-0/음수/NaN 알파, 타입·stick·숨김·Precheck·renderer 조건, 외부 패치 거절, 정확히 한 번의 색 쓰기, 1/32 표본, 불일치 후 중지, 로드/해제를 확인했다. 41,010개 확인이 통과했다. 이것을 Unity/Mono 실행 검증이나 성능 증거로 확대하지 않는다.

`colorfast bench`는 맵과 편집기를 닫은 메뉴에서만 허용한다. 게임 Awake/OnDestroy를 부르지 않는 관리 필드용 더미 visual과 실제 SpriteRenderer를 쓰며, 원본 delegate와 직통을 워밍업 후 A/B/B/A로 비교한다. 각 라운드는 16,384회씩 색·불투명도 둘 다 호출한다. 호출별 Stopwatch 없이 라운드 전체만 잰다. 끝에 타이밍 밖에서 원래 setter 재호출과 512쌍의 상태를 대조하고, 불일치 시 직통을 중지한다. 생성한 GameObject는 finally에서 Destroy한다.

벤치 원본/직통 µs 차이를 Δ라고 하면 예상 절감은 `1800 × Δ / 1000 ms/frame`이다. 하나의 더미와 비어 있는 메뉴의 해시/캐시/렌더 상태는 실제 1,800개 장식과 다르다. 벤치만으로 채택하지 않는다. FrameParts setter 통계는 직통 호출을 포함하지 않으므로 해당 수치의 감소도 이득으로 해석하지 않는다. 모드 애니메이션 전체 시간으로 비교한다.

## 3. 예상 절감 — 모두 미실측 추정

7ms 메인 스레드 프레임 기준으로 인계 프로파일의 약 12%는 0.84ms다. 이 12% 전체가 사라지는 것은 아니다. Step 6.2%=0.434ms, OnUpdate 5.2%=0.364ms, delegate 4.4%=0.308ms, SetColor 4.3%=0.301ms, ApplyColor 4.2%=0.294ms는 중첩된 호출 사슬의 비율이므로 합산하면 안 된다.

| 항목 | 7ms 기준 판단 |
|---|---|
| 색 delegate→setter→Harmony 래퍼·postfix 우회 | 해당 inclusive 영역 4.4%≈0.308ms 안의 일부가 후보. 엔진 색 쓰기 유지·guard 비용 때문에 그 전부가 절감될 수 없음 |
| easing/Step/OnUpdate의 나머지 | 계산·순서를 유지하므로 별도 절감 0으로 계획 |
| 같은 SpriteRenderer.color 쓰기 삭제 | 구현하지 않음, 절감 0 |
| Δ=0.05µs라는 가정 | 0.09ms≈1.29%; 예시 추정이며 측정값 아님 |
| Δ=0.10µs라는 가정 | 0.18ms≈2.57%; 예시 추정이며 측정값 아님 |
| 추가 가드·fallback·네이티브 읽기 | 손해 가능. 양수 절감의 하한은 보장할 수 없음 |

직통 허용 비율이 낮으면 실제 N은 1,800보다 작다. 특히 Precheck 활동 중에는 전체 fallback이므로 state에서 direct/fallback 증분을 함께 봐야 한다.

## 4. LateUpdate 동일값 쓰기 재평가

### 실제 기본값과 기존 구현

TightLate.cs의 `SkipSame = true`와 `tightskip-off` 주석은 선언 시점 설명이다. 실제 로드에서는 StutterFix.cs가 **`File.Exists("tightskip-on")`으로 덮어쓴다**. 따라서 기본은 false이고 tightskip-off는 로드 코드에서 읽지 않는다. TightLate 자체는 tightlate-off가 없으면 켜진다.

현재 TightLate.Apply는 마지막으로 엔진에 쓴 position/Quaternion/localScale 입력 비트를 저장한다. SkipSame을 켰을 때 각각 같으면 setter를 생략한다. 경로를 빠졌거나 프레임이 끊기면 Has를 무효화한다. 카메라 위치는 매니저 LateUpdate 호출당 한 번 읽어 Process/Apply에 전달한다. 제안한 직전 값 비교와 카메라 위치 캐시는 이미 있다. 이 작업에서 같은 기능을 추가하지 않았다.

인계의 건너뜀 비율 위치 1%·회전 9%·크기 34%를 같은 세 setter 분모로 보면 생략 가능한 호출은 약 `(1+9+34)/3=14.7%`다. 위치·회전 대부분은 여전히 써야 하며 비교 비용도 남는다. 이 수치는 이번 실측이 아니라 인계 수치다.

**카메라 Z 회전 원인 설명은 적용 대상을 구분해야 한다.** TightLate 대상은 lockRotation=false, stickToFloor=false다. 회전은 `Quaternion.Euler(0,0,startRot+rotAngle)`이며 카메라 Z를 더하지 않는다. 따라서 대상의 회전 91%가 달랐다는 사실을 카메라 Z 변화 탓으로 확정할 수 없다. rotAngle 애니메이션 등이 원인일 수 있다는 추정까지만 가능하다. 카메라 Z는 제외된 lockRotation 장식의 원본 SetRotation에서 더한다. 위치는 카메라 위치와 parallax 계산이 매 프레임 달라질 수 있다.

### FinishFrameRendering과 dirty 가설

원본 IL은 setter를 호출한다는 사실까지만 보여 준다. 같은 값을 쓸 때 네이티브가 조기 종료하는지, dirty를 언제 합치는지, FinishFrameRendering의 1.7ms 중 Transform 의존 작업이 얼마인지는 모드 C#으로 확인할 수 없다. 스프라이트 수·정렬·배칭·컬링·재질·렌더 스레드 대기도 후보 설명이다. **쓰기 횟수에 비례한다는 결론은 미확인**이다.

모든 렌더 비용이 세 Transform 쓰기에 선형 비례한다고 가정해도 `1.7ms×14.7%≈0.249ms`, 7ms의 약 3.56%라는 계산일 뿐이다. 이는 보장된 상한도 실측 절감도 아니다. 기존 시험에서 이득이 작아 기본 끔으로 둔 결정을 바꿀 근거는 없다. 이번 추가 구현의 절감은 0이며, 남은 이득은 다음 최소 실험으로 판정한다.

1. colorfast 끔, decoengine 끔, TightLate 켬을 고정하고 다른 설정/DLL/해상도/주사율/창 모드도 동일하게 둔다.
2. `tightskip-on` 없음/있음/있음/없음의 새 프로세스 ABBA. A/B 모두 같은 곡 252~273초를 측정한다.
3. `tightlate state`를 창 앞뒤에 기록한다. `ΔskipPos/Δwrites`, `ΔskipRot/Δwrites`, `ΔskipScale/Δwrites`를 구한다. writes는 setter 세 개 묶음의 후보 횟수다.
4. 같은 창에서 fuse와 profsamp를 비교하고 PresentMon CPU/GPU busy·wait·present 구성을 보조로 쓴다. PresentMon 자체가 FinishFrameRendering 함수 시간을 직접 주지는 않는다. profsamp의 해당 스택 표본 비율과 프레임/CPU 시간을 함께 본다.
5. 별도 검증 판에서 tightlate verify와 late-verify, decosig를 확인한다. setter별 Stopwatch 래퍼는 추가하지 않는다.

### 제외된 장식과 이중 위치 쓰기

TightLate.Eligible 제외 이유는 type, hitbox/mask/mesh, hidden, follow/stick/lock, parallax, parent, rejected다. state의 reasons 배열은 **0번 예약 칸을 포함한 8칸**이며 이름에 대응하는 값은 1~7이다. 누적 호출 횟수이며 고유 장식 수가 아니다. 새 게임 수치를 얻지 않았으므로 실제 제외 비중은 미상이다. 엔진을 끈 상태에서 동일 창의 `Δreason[i] / (Δprocessed + ΣΔreason[1..7])`로 순회 대상 내 비중을 구할 수 있다. Dormancy가 미리 뺀 잠든 장식은 이 분모 밖이며 Dormancy 통계로 별도 기록한다.

제외된 장식이 MoveApply.LogicMaybe에서 원본 LogicUpdate→UpdatePosition까지 가면 다음 IL이 남는다.

- UpdatePosition `007F`: pivotTrans.localPosition 쓰기.
- UpdatePosition `00C6`: SetTrans 호출(커스텀 맵 또는 parallax.enabled일 때).
- SetTrans `000C`: 방금 위치의 world position 읽기.
- SetTrans `0162`: cachedTransform.position 쓰기. 이전 world Z를 보존하며 dontAlterX/Y이면 이전 world X/Y도 사용한다.

따라서 이중 쓰기는 존재하지만 **제외 전부가 매 프레임 두 번 쓰는 것은 아니다**. hidden은 원본 GetVisible/LogicMaybe에서 UpdatePosition 자체가 생략될 수 있고, parallax.enabled 조건·실제 하위 타입에 따라서도 다르다. excluded 카운터만으로 이중 쓰기의 횟수를 확정할 수 없다.

TightLate 대상은 이미 최종 위치 한 번만 쓴다. 나머지에서 첫 localPosition을 삭제하면 SetTrans가 읽는 world Z/축 유지값, parent 변환, Init·clampToScreen·카메라 변환, 파생 타입·다른 Harmony 관찰값이 달라질 수 있다. 별도 제한 경로를 만들려면 이 중간 입력을 같은 연산 순서로 재현하고 IL/게임 대조를 다시 해야 한다. 이번에는 삭제·합치기를 구현하지 않았다. 알려진 제외 비중과 시간 없이 예상 절감 %를 만들어 내지 않는다.

## 5. Claude의 후속 실행 순서 — 이 문서에서는 실행하지 않음

### 정확성 판

1. 승인된 후속 세션에서 동일 PlayerAuto DLL을 준비한다. decoengine-on은 두지 않는다. 기존 posfast/tightskip 등 비교 변수를 고정하고 `late-verify` 파일을 둔다. baseline과 colorfast를 모두 새 프로세스로 연다.
2. colorfast 켠 판에서 `colorfast state`가 enabled=true/compatibility=ok인지 확인한다. 거절이면 patch 사유부터 확인하고 강제로 우회하지 않는다.
3. 재생 전에 `colorfast verify`, `tightlate verify`를 켠다. 전체 곡을 돌린다. colorfast state의 direct>0, checked>0, mismatch=0 및 tightlate/late-verify의 불일치 0을 확인한다. checked=0은 검증 성공이 아니다.
4. 곡 끝 같은 시점의 `decosig colorfast-A1`/`decosig colorfast-B1` 등을 저장해 기존 decosig 비교 방식으로 새 차이가 없는지 확인한다. 입력/색/Transform 순서를 바꾸지 않았더라도 이 검사를 생략하지 않는다.

### 성능 ABBA

`colorfast-on` 없음(A1) → 있음(B1) → 있음(B2) → 없음(A2), 매 판 새 프로세스. 파일을 실행 중에 바꿔도 해당 로드에는 적용되지 않는다. 검증 판과 성능 판을 분리하고 성능 판에서는 colorfast verify off, tightlate verify off, late-verify 파일 없음으로 양쪽을 맞춘다. 게임 시작·맵 열기·자동 재생 절차는 기존 sf_ab 방식을 따른다.

각 판 같은 위치에서 아래 순서를 사용한다.

```text
waitsongpos 252
colorfast state
tightlate state
fuse begin
profsamp begin 1000
waitsongpos 273
profsamp end
fuse end
colorfast state
tightlate state
animstat
```

`waitsongpos`는 곡의 실제 시계를 사용한다. fuse/profsamp 구간과 창 설정을 판마다 동일하게 유지한다. 필요하면 별도 메뉴에서 colorfast bench를 먼저 보되, 벤치 결과를 ABBA의 대용으로 쓰지 않는다.

**채택 조건:** 같은 252~273초 창에서 모드 애니메이션 갱신 시간이 ABBA 양방향으로 감소하고, 유효한 colorfast 표본에서 불일치 0이며, tightlate/late-verify가 계속 통과하고, 곡 끝 decosig에 새 차이가 없어야 한다. 직통 적용 건수·fallback 분포와 원본 전체 프레임 시간도 함께 기록한다. 이 조건을 확인하기 전에는 기본 끔을 유지한다.
