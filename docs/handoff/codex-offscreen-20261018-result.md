# 화면 밖 장식 갱신 조사 결과 — 2026-10-18

측정 전용 `offscreenstat begin|end`를 추가했다. 제품의 애니메이션·Transform·LateUpdate 동작은 바꾸지 않았다. **게임 실행·게임 제어·Mods 설치·커밋·푸시를 하지 않았다.** 선행 `codex-tight-20261018-result.md` 완료 및 실행 중인 Unity Editor/게임 없음(잔류 CrashHandler만 있음)을 확인한 뒤 시작했다.

실제 Arche 가시성 분포와 성능 순이득은 **미측정**이다. 아래 ms는 인계서의 3.3ms·2.4ms를 입력으로 한 **추정**이며, 게임 측정 결과로 읽으면 안 된다. 전 속성 계산 생략을 바로 제품화하기보다는, 논리 상태 계산을 유지하며 제한된 장식의 엔진 쓰기만 미루는 설계부터 검증할 가치가 있다.

## 변경 범위와 실행 비용

- `OffscreenProbe.cs`: DEV/AUTOTEST 전용 통계. Harmony 패치·PlayerLoop·GameObject를 추가하지 않는다.
- `DecoAnim.cs`: 기존 `#if DEV || AUTOTEST` 안에 `ProbeRecords` 읽기 뷰 1개. probe는 Rec를 수정하거나 보관하지 않는다.
- `AutoTest.cs`: 명령 분기 1줄. `StutterFix.csproj`: FuseProbe/CpuBreak와 동일한 Compile 조건 1줄.
- `StutterFix.cs`: 기존 DEV/AUTOTEST UninstallAll 경로에 `Try(OffscreenProbe.Stop)` 1줄. Main.Unload와 모드 끄기 모두 이 경로를 탄다.

`begin`에서만 `Camera.onPreRender`를 구독하고 `end`/Stop/예외/모드 내림에서 해제한다. 미사용 상태에서는 프레임 콜백·목록 순회·측정용 Harmony 패치가 **0개**다. 플레이 중 미사용 상태에서는 probe의 필드 접근자도 생성하지 않는다(모드 내림의 Stop 호출은 일회 초기화를 할 수 있다). 종료 시 장식·렌더러 참조 목록을 비운다. begin을 다시 부르면 지난 구간을 버리고 새로 센다.

활성 중에는 매 표본 활성 Rec 목록 1회 + 장식별 렌더러 열거/실제 bounds 읽기 + 최대 4회 AABB/frustum 검사를 한다. 카메라 콜백 전체에 Stopwatch 1쌍만 사용한다. 자주 불리는 게임 함수나 속성마다 Stopwatch를 붙이지 않았다. `probe-ms/sample`은 이 계측의 관측 비용이며 게임의 원래 장식 갱신 시간으로 해석하지 않는다. 이 판의 FPS를 최적화 성능 A/B에 쓰면 안 된다. 새 State 생성·Dictionary 성장과 엔진 조회 비용은 켜진 판에 포함된다.

## 카운트 정의와 읽는 법

표본 시점은 본 카메라 `scrCamera.instance.camobj`의 `onPreRender`이다. LateUpdate가 적용한 지금 프레임의 카메라·시차·회전·크기를 읽는다. 카메라 이름/Camera.main/Renderer.isVisible에는 의존하지 않는다. 같은 Unity frame에서 재렌더하면 첫 콜백만 세고 `duplicate-callbacks`를 남긴다. 따라서 커스텀 FPS로 본 카메라가 매 4번째 프레임에만 그려지면 표본 수도 줄어든다. `samples`와 `rendered-frame-span`이 다르면 전체 Update 프레임에 대한 평균으로 확대하지 않는다.

모집단은 **표본 시점에 Running=true인 DecoAnim Rec가 한 개 이상 있는 장식**이다. 완료되어 목록에서 빠진 Rec와 진짜 DOTween만 쓰는 장식은 이 모집단에 없다. 장식은 한 번만 세고, 그 장식의 모든 활성 Rec를 애니메이션 수로 센다. SpriteMask처럼 Renderer가 없는 대상도 분모에서는 사라지지 않고 unknown으로 남는다.

각 행의 `decos/frame`, `anims/frame`, `rotScaleColorOpacity/frame`은 표본 합계/samples다. 마지막 것은 key 5/7/8/9/10(회전·크기X/Y·색·불투명도)만 센다. 위치1/2, 피벗3/4, 시차11, 시차 오프셋12/13은 절약 대상에서 제외한다.

시차 배율은 그 표본의 multiplier_x/y가 둘 다 0, 둘 다 1, 그 밖, 시차 부품 없음 네 갈래다. 부품 enabled 상태나 카메라 고정 여부의 안전성까지 뜻하지는 않는다.

거리 단위는 **화면 전체 폭/높이의 비율을 각 변 바깥에 더한 마진**이다. 25%는 화면 폭이 1.5배, 50%는 2배, 100%는 3배가 되는 영역이다. cullingMatrix의 clip X/Y 행을 각각 1/(1+2m)배 하고 Z/W는 유지하여 여섯 평면을 만든다. 회전·확대·시차가 반영된 실제 Renderer.bounds로 검사한다. 월드 중심점만 보고 작은 물체로 가정하지 않는다.

|출력 bucket|의미|
|---|---|
|inside|마진 0의 본 카메라 AABB 후보|
|outside_0_25|원래 화면 밖, 25% 확장 영역에는 걸침|
|outside_25_50|25% 밖, 50%에는 걸침|
|outside_50_100|50% 밖, 100%에는 걸침|
|outside_gt100|100% 확장 영역에도 걸치지 않음|
|alpha0|적용된 rendererColor.a<=0, 위치 검사 이전에 별도 집계|
|unknown|유효한 본체 bounds 없음/0 크기/비정상 값/본 카메라 깊이·레이어 등에서 제외|

(b)의 25%/50%/100% 누적 수는 각각 첫 근접 행, 첫 두 근접 행 합, 세 근접 행 합이다. (c)는 outside_gt100이다. 각 시차 갈래에서 일곱 bucket을 합하면 그 표본의 장식 수/Rec 수가 된다.

이미지 장식은 spriteRenderer와 meshRenderer 본체만 검사한다. 편집 선택 테두리·히트박스 보조 Renderer는 포함하지 않는다. 다른 장식 유형은 자신이 소유한 자식 Renderer를 검사한다. 여러 본체가 있으면 가장 가까운 bucket을 쓴다. 유효하지 않은 본체가 섞이면, inside인 본체가 확인된 경우 외에는 unknown으로 보수적으로 남긴다. `renderer-excluded`와 `depth-excluded`는 **렌더러 검사 건수**이며 장식 수가 아니다. alpha0는 논리 적용 알파이며 특수 셰이더의 최종 픽셀 가시성을 증명하지 않는다.

inside는 bounds 기반 **컬링 후보**다. 가려짐·마스크·투명 픽셀·다른 카메라까지 포함한 실제 픽셀 기여를 뜻하지 않는다. AABB는 회전된 스프라이트에 대해 보수적이며, 이를 정확한 스프라이트 외곽 거리나 원본 PNG의 비투명 외곽으로 부르지 않는다.

연속 바깥 체류는 outside 4개 bucket을 합쳐 센다. inside→outside→inside를 모두 본 경우만 completed이며 길이는 1 / 2~5 / 6~20 / 20초과 **Unity 연속 프레임**이다. 구간 시작/끝, 애니메이션 종료·파괴, 렌더 공백, alpha0/unknown 전환은 censored-observed-lower-bound로 분리한다. 양 끝이 잘린 길이를 완전한 체류 분포에 섞지 않는다. `entries-far`는 그 체류 중 한 번이라도 >100%였던 장식의 inside 재진입, `entries-near`는 근접 bucket만 거친 재진입이다. 근접을 거쳐 들어온 먼 장식도 far 사건으로 센다. 사건/1000표본과 사건/곡 초를 함께 보고한다.

`XY-record-pair`와 `farXY`는 활성 위치X·Y Rec가 **모두 있는 비율의 분자/분모**다. 다음 프레임 위치 예측의 필요조건에 대한 coverage이며, 다음 dt·카메라·효과까지 알려진 비율은 아니다. 장식 시작/끝/교체 순서까지 동일한 정확한 예측 가능 비율은 이 값보다 작을 수 있다.

곡 종료·Resetting·관측한 곡 시간 역행은 ABORTED로 기록하고 콜백을 해제한다. 예외가 난 구간은 부분 카운트가 남아도 **구간 전체를 폐기**한다. 표본이 없으면 NO_SAMPLES이며, 0개 가시 장식이라는 결과로 쓰지 않는다. 프레임 사이에 끝났다 같은 시각으로 재시작한 것까지 검출하는 수명 검증기는 아니므로 측정 중 stop/retry/seek를 섞지 않는다.

## 절약 상한과 비용 모델

인계서의 3.3ms(DecoAnim) + 2.4ms(관리자 LateUpdate) = **5.7ms**가 해당 두 단계에서의 절대 절약 한계다. 위치 계산·판정·복원과 남는 관리 비용 때문에 실제로는 이에 못 미친다. 카운트만으로 속성별 원래 비용을 알 수 없어 아래 비례식은 엄밀한 시간 상한이 아니라 **동일 단가를 가정한 추정**이다.

- F: 표본 수, N: 전체 활성 장식 합/F.
- A: 전체 활성 Rec 합, A_far: >100% 장식의 key 5/7/8/9/10 Rec 합.
- D_far: >100% 장식 합/F, L: 같은 구간 LateUpdate 실제 순회 장식 평균.
- q=A_far/A, p=D_far/L.
- 예상 gross = 3.3q + 2.4p ms/frame.
- 예상 net = gross − N×c/1000 − h ms/frame. c는 위치·가시성 판정 비용(µs/장식), h는 복원·재진입·호환성 확인 비용이다.

출력의 pLateProxy는 인계서의 L=2150을 고정 사용하고 1에서 제한한다. q와 far/animated-decos도 함께 출력한다. **활성 애니메이션 장식 수와 LateUpdate 전체 장식 수를 같은 분모로 취급하지 않았다.** 실제 계산 때에는 동일 구간 animstat/Dormancy.Stat/기존 cpubreak 근거로 L을 교체해야 한다. 관리자의 상수 비용이나 다른 장식 비용까지 전부 없앨 수 있다고 가정하면 과대평가다.

예시(모두 추정): N=L=2150, 절약할 속성 Rec 비중 60%, 그중 멀리 있는 비율이 장식/속성에서 모두 f, h=0이면 gross=4.38f ms다.

|f|gross|c=0.1µs 순이득|c=0.5µs 순이득|c=1µs 순이득|
|---:|---:|---:|---:|---:|
|25%|1.095ms|0.880ms|0.020ms|-1.055ms|
|50%|2.190ms|1.975ms|1.115ms|0.040ms|
|75%|3.285ms|3.070ms|2.210ms|1.135ms|

f=50%면 판정 단가 손익분기는 약 **1.019µs/장식**(h=0)이다. Transform 쓰기만 미루며 DecoAnim 논리 계산은 유지하면 3.3q 부분 대부분이 남는다. LateUpdate만 절반을 줄인다는 더 작은 가정에서는 gross=1.2ms이고 c=0.5µs에서 net=0.125ms−h다. 기존 DeferTransform/PairXY가 이미 없앤 쓰기는 다시 절약량에 더하면 안 된다.

>100% 체류 분포에서 completed가 대부분 1프레임이면 진입 복원 비용이 커지고, 20초과 체류가 많을수록 장기 생략 가능성이 높다. censored는 전체 길이를 모르므로 장기 체류 확정 건수에 더하지 않는다. probe-ms/sample 자체에는 비싼 자식 열거와 통계 작성도 있어 제품 판정 단가 c의 실측 대용으로 쓰지 않는다.

## IL/코드로 확인한 안전 조건

근거는 `codex-offscreen-20261018-il.txt`(기존 ILScan), `...-raw-il.txt`(오프라인 산술 IL), `...-readers-il.txt`(메타데이터 전수 참조)다. 원래 ILScan 소스는 수정하지 않았다. `%TEMP%/sf-offscreen-metadata-20261018`의 임시 확장으로 모든 메서드의 scrDecoration/scrVisualDecoration/scrParallax 직접 필드 읽기·주소 읽기·메서드 참조를 찾았다. Assembly-CSharp 461개 참조, firstpass 0개를 수집했다. 숫자는 비용/소비자 개수가 아니라 메서드 내 중복 제거 참조 수다.

Mods 아래 모든 DLL과 **ModsOff/PACL2 및 assembly_cache**도 검사했다. 네이티브 libdeflate/sfnative/turbojpeg 및 macOS `._` 부스러기는 관리 메타데이터가 없어 제외했다(`...-audit-errors.txt`). PACL2는 비활성 폴더에 있어도 별도 호환성 소비자로 조사했다. PACL2 본체와 cache는 각각 144개 참조이며 같은 기능의 두 복사본을 두 개 활성 모드로 세지 않는다. 로컬 StutterFix와 설치본도 별도 스냅샷으로 기록했다.

이 조사는 **직접 IL 참조 전수**다. reflection 이름 조회, Harmony가 삽입할 코드, 범용 GetComponent<Transform>, 패키지 내부/실행 중 내려받는 미열거 DLL, Unity 네이티브 렌더/물리 소비자를 모두 증명한 것은 아니다. 따라서 다른 모드 호환성이 안전하다는 결론은 내리지 않는다. reflection 기반 자체 모드 사용은 소스 검색으로 보완했다.

|소비자|읽는 상태와 조건|
|---|---|
|scrDecoration.UpdatePosition|pivotPosVec + 행성 위치 + 붙은 타일 이동량, parallax 시작 상태, rotAngle/scaleVec를 엔진에 적용. stickToFloor면 ApplyColor도 수행|
|LogicUpdate|GetVisible면 UpdatePosition, 이어 UpdateShader와 히트박스 상태 갱신. **LogicUpdate 전체 생략과 UpdatePosition만 생략은 다르다**|
|scrVisualDecoration.GetDecorationWorldSize|sprite.bounds와 pivotTrans.localScale. 편집 Gizmo LateUpdate/Drag가 소비|
|scrVisualDecoration.UpdateShader|sprite/mesh, 마스크와 대상 목록, scaleVec, repeat, 색/불투명도, cfpCache 및 렌더 상태. 마스크 Transform은 다른 장식 셰이더가 읽으므로 화면 밖이라고 생략 불가|
|scrDecorationManager|Update에서 Collider2D.Overlap 기반 CheckHitboxHit, LateUpdate에서 LogicUpdate. tiling/선택 테두리/히트박스 크기 갱신도 SpriteRenderer 상태 사용|
|CheckHitboxHit / UpdateHitbox|물리 Collider의 현재 위치/크기에 의존. 화면 밖 히트박스도 행성·다른 장식과 충돌할 수 있어 제외 필요|
|DecoTransformGizmoHolder|GetDecorationWorldSize/GetPositionWithParallax. 편집으로 돌아가기 전에 endPosition/Transform 동기화 필요|
|InvisibleSkip / LazySprite / Precheck|rendererColor·forceRenderingOff·지연 Sprite와 Transform 상태를 함께 관리. 기존 투명 상태 지연과 새 화면 밖 지연을 중첩하면 깨움/검증 순서 필요|
|Dormancy / MoveApply|투명·무히트박스 등 이미 확정된 조건만 순회 제외. DeferTransform은 정확한 scrVisualDecoration/마스크 제외 등으로 제한하고 LateUpdate 쓰기를 전제. 새 LateUpdate 생략과 결합하면 필드만 갱신하고 엔진 쓰기는 영구 누락될 수 있음|
|FastMove / InstantMove / DecoAnim|새 효과 시작·Kill(true)/OnComplete·Scale Startup이 현재 pivotPosVec/rotAngle/scaleVec/color/opacity를 읽음. 논리 값을 건너뛰면 다음 효과 시작값부터 달라짐|
|ExitFix / SceneReset / DecoSig|ResetScene의 기준 상태, 가벼운 재설정, Transform·sprite·색의 검증에 의존. 끝난 뒤에만 복원하면 ResetScene 안의 관측에 늦음|
|PACL2 FollowDecoration / OrbitDecoration|pivotPosVec/pivotOffsetVec/rotAngle/scaleVec/시차 배율 등을 읽어 다른 장식 애니메이션의 입력으로 사용. 화면 밖이어도 논리 계산 생략 불가|
|PACL2 PreviewerMoveDecorationCore|SpriteRenderer/scaleVec 등 편집 미리보기 상태 소비|

scrCamera·ffxSetFilter 계열에서 이 세 장식 타입에 대한 직접 참조는 전수 검색에 없었다. scrCamera는 카메라 Transform/투영/수동 Camera.Render/RT 경로를 바꾼다. 현재 실제 타입 이름은 `scrScreenTile`이 아니라 **ScreenTile**(Assembly-CSharp)이며 OnRenderImage는 material/Graphics.Blit 경로다. 장식 직접 참조 없음이 렌더 패스/마스크를 통해 장식 픽셀을 쓰지 않는다는 뜻은 아니다. 화면 밖 판정에는 그 프레임의 본 카메라 행렬과, 향후 별도 카메라/RT/셰이더 의존을 함께 고려해야 한다.

## 시차의 닫힌 식과 한계

`raw-il`의 scrParallax.SetTrans 0097~00C8, 00C9~00EE, 00F3~0136에서 확인한 비고정 분기는 다음과 같다(⊙/⊘는 성분별 곱/나눗셈).

```
U = startPosition + (cameraTransform.position.xy - posCamAtStart.xy) ⊙ multiplier
U.x/y = 기존 cachedTransform.position.x/y  [dontAlterX/Y인 축]
O = isDecoration ? parallaxOffset ⊙ camScaleMultiplier ⊘ textureScaleMultiplier : (0,0)
endPosition = U + O
cachedTransform.position = (endPosition.x, endPosition.y, 기존 z)
```

`clampToScreen` 분기는 pixelRect의 원점 + screenRelativePos⊙pixelRect 크기를 ScreenToWorldPoint에 넣어 U로 사용한다. dontAlterX/Y 복원은 비고정 분기에 있다.

중요하게도 UpdatePosition은 **매번** P를 계산해 SetNewStartPosition(P), posCamAtStart=P를 쓴다. Init 때 카메라 위치 하나를 영구 보관하는 구조로 가정하면 틀린다.

```
P = pivotPosVec + (paused 또는 followPlanet 있음 ? 행성 위치 : 0)
    + (stickToFloor ? parentFloor.position.xy - startPos : 0)
```

paused 분기의 행성 접근과 null 조건도 원래 분기 그대로 보존해야 한다. 단순한 경우 startPosition=posCamAtStart.xy=P이므로 `world.xy=(1-m)⊙P + m⊙C + O`, 카메라 기준 위치는 `(1-m)⊙(P-C)+O`로 정리할 수 있다. 실제 화면 좌표는 여기에 카메라 worldToCamera/projection을 적용한다. m=0은 월드에 남고 m=1은 카메라 이동을 따른다.

카메라 위치 **하나만**으로 모든 장식의 화면 위치를 구할 수는 없다. P, 시차/오프셋, lockScale의 카메라 배율, 부모/자식 계층, floor/followPlanet, 축 고정의 기존 Transform, camera 회전·줌·흔들림이 필요하다. 회전/크기/피벗/이미지 교체로 다음 bounds도 커질 수 있다. m=0에서도 카메라 회전·줌으로 갑자기 화면에 들어올 수 있다. 마진 100%는 안전 증명이나 다음 프레임 예측을 대신하지 않는다.

DecoAnim은 `r.Pos += (smoothDeltaTime 또는 deltaTime) × DOTween.timeScale`의 **float 누적**을 사용한다. Scale 시작값은 첫 갱신 시 읽는다. `start + change × ease(songTime-startTime)`로 건너뛴 시간을 한 번에 합치면 float 반올림·시작 시점·중간 Kill/새 효과 순서가 달라질 수 있다. 기존 위치/진행도/논리 상태 계산은 유지하고 엔진 반영만 미루는 쪽이 첫 검증 범위로 적합하다.

## 복원/무효화가 필요한 경계 — 설계만

- 화면 재진입: 렌더 전에 위치·회전·크기·색·Sprite/마스크/Collider를 필요한 순서로 반영. 렌더 콜백에서 처음 발견하면 culling 이후라 이미 늦을 수 있어 제품 판단/flush는 culling **이전**에 필요하다. 이 probe의 onPreRender는 관측용이다.
- 새 효과 시작/속성 Kill(true)/OnComplete/다른 소비자 읽기 전: 논리 상태는 항상 최신이어야 한다. 속성 하나씩 flush하면 순서 상호작용도 유지해야 한다.
- 완주 OnLandOnPortal/실패 FailAction/나가기: Hitch.SongEnded의 ApplyAllLazy와 맞추되, 마지막 효과 예산 큐는 원래처럼 다음 프레임에도 진행한다. 끝났다고 전부 버리거나 끝값으로 강제 완료하면 안 된다.
- retry/편집 복귀: SceneReset.Prefix의 DropAll/ApplyAllLazy 및 Resetting 전에 현재 지연 상태를 정리하고, 뒤따르는 ResetDecorations/Setup이 덮는 새 상태를 지연시키지 않는다. FinishCustomLevelLoading에서 오래된 값을 복원하면 늦다.
- seek/ScrubToTime/새 맵/카메라 변경: 이전 가시성/시간/bounds 캐시를 무효화하고 원래 재생 준비 경로로 복원한다. seek 초반에는 DecoAnim 대신 DOTween인 경우가 있으므로 probe 모집단 감소를 전 장식 감소로 해석하지 않는다.
- 설정 끄기/모드 끄기/Main.Unload: 미뤄진 엔진 값을 현재 논리 상태에 맞춘 뒤 기존 FinishAll·ApplyAllLazy·unpatch 순서를 존중한다. 이번 probe는 상태를 미루지 않으므로 Stop만 필요하다.

## 제안 순서와 Claude의 후속 명령

1. 이번 probe로 >100% 비중·XY coverage·완결/검열된 체류·진입 빈도를 확보한다. 그 전에 제품 구현 가치를 확정하지 않는다.
2. 첫 제품 실험은 정확한 scrVisualDecoration, 무히트박스·무마스크·특수 shader/mesh 없음·floor/planet 추종 없음·축/화면/회전/크기 잠금 없음·부모 변환 통제·다른 소비자 없음으로 제한한다. **논리 값과 r.Pos는 계속 계산**하고 엔진 쓰기만 미룬다. m=0부터 검증하면 공식은 단순하지만 인계서의 12개/2150개라 동일 단가 전체 5.7ms 비례 추정도 약 0.032ms뿐이다. 효과 입증용 작은 시작이지 큰 성능 후보는 아니다.
3. 지원 가능한 시차 배율을 확장하고, 카메라 급변/이미지·크기·회전 변화 시 보수적으로 즉시 flush한다. 마진과 장기 체류가 실제로 이득인지 측정한다. 비트 동일 논리 상태/엔진 상태 및 별도 카메라 출력까지 검증한 뒤 회전·크기·색 계산 생략을 별도 설계한다.

아래는 **Claude가 나중에 허가된 게임 세션에서** 실행할 순서이며, 이번 작업에서는 실행하지 않았다. 새 DEV 또는 PlayerAuto DLL의 설치/새 프로세스 시작은 Claude 쪽 별도 작업이다. 경로는 실제 Arche 맵 파일로 치환한다.

```text
open <Arche .adofai 절대경로>
auto on
play
wait 255
offscreenstat begin
wait 25
offscreenstat end
decostruct
animstat
keep
```

wait는 실시간 기준이므로 실제 구간은 begin/end가 출력한 **song 시작·끝**으로 판정한다. 정확한 255~280초 경계를 맞춰야 하면 별도 준비 판에서 `shotat 255 offscreen-pre`로 song 도달을 확인하고 begin한다. 표본 중 캡처는 부하를 섞으므로 끝 경계에 shotat을 쓰지 말고 출력 song 시간으로 확인한다. decostruct/gpucensus/전체 로그 작성은 end **뒤**에 둔다. cpubreak on은 곡 중간에 설치하지 않는다. 270초 구간의 깨끗한 시간 기준은 기존 인계 측정값을 쓰거나, probe를 끈 별도 판에서 재측정한다.

빠른 탐색은 `seek 255 12` 뒤 시작 근처를 버리고 같은 명령을 쓸 수 있지만, float 누적/Startup과 DOTween 모집단이 다른 탐색 판이다. 마지막 판단은 위의 처음부터 재생한 판으로 한다. 제품 구현 후에는 무최적화/최적화 새 프로세스 쌍으로 같은 song 시점 decosig(진입 직전/직후·곡 끝), 전체 곡 출력, retry/stop/모드 끄기·다시 켜기를 대조해야 한다.

## 검증과 보존

DEV·Player·PlayerAuto 빌드 모두 오류 0. Player 메타데이터에 OffscreenProbe 타입 0개. DEV/PlayerAuto LoadCheck에서 FieldRefAccess 이름 고정 238곳 중 틀림 0(동적 이름 5곳은 기존 실행 중 확인 대상). 관련 로그는 `...-dev-build.txt`, `...-player-build.txt`, `...-playerauto-build.txt`, `...-loadcheck-*.txt`, `...-player-types.txt`다.

실제 소스에서 State/Close/연속 체류 전이 코드를 그대로 추출한 독립 .NET 검증에서 **13개 경계 사례 통과**: 1프레임, far→near→inside, 왼쪽 검열, 렌더 공백, alpha0/unknown, 종료 중복 정리, 1/2/5/6/20/21 길이. `...-counter-check.txt`에 기록했다. 이 시험은 Unity bounds/frustum/native callback을 실행한 시험이 아니다. 실제 게임에서 카메라 선택·가시성 카운트·렌더 순서가 맞는지 확인하는 단계는 남아 있다.

`...-preservation-before.json`은 시작 당시 루트 C#·csproj·기존 .codex/config.toml 및 Mods/StutterFix 모든 파일의 SHA256이다. 종료 비교는 `...-preservation-check.txt`에 남긴다. 지정한 연결 파일 4개 외 기존 C# 파일과 기존 config/설치 파일은 바꾸지 않는다. 다른 작업의 기존 미추적 파일도 덮지 않았다. 게임 실행·Mods 파일 쓰기·git commit/push 명령은 사용하지 않았다.

종료 보존 확인: 시작 해시 **472개** 중 Mods/StutterFix **298개**, 나머지 기존 소스·config **170개**가 모두 동일하다. 지정 연결 파일 4개는 추가한 코드만 제거하면 시작 SHA256과 정확히 일치하며, 원래 BOM/줄바꿈을 유지했다. 기존 `.codex/config.toml`의 다른 작업 변경도 그대로다. 최종 git diff --check 통과. PlayerAuto에는 OffscreenProbe 포함, Player에는 없음도 메타데이터로 확인했다.
