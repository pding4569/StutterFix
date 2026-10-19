# DecoEngine 설계 및 1단계 구현 결과 — 2026-10-19

## 1. 설계: 소유권과 두 단계 루프

목표는 키 1·2·5·7·8의 기록별 setter 사슬을 장식별 계산으로 바꾸는 것이다. 이번 구현은 **기본 끔인 실험 경로**다. 필드를 매 Update에 게시하는 1단계를 구현했으며, 게으른 필드 동기화는 설계만 했다. 실게임의 화면·최종 상태 비트 동일성과 성능 개선은 아직 검증하지 않았다. 게임 실행·Mods 설치·커밋은 모두 0회다.

`DecoEngine.State`는 풀에서 재사용하는 장식별 소유 객체다. 필드는 장식 D, TightLate.Entry 참조, 최대 5개의 Rec 참조 배열, 개수 N, 활성 배열 인덱스 Index, 키 마스크 Mask, 예외 복구용 Advanced 마스크, 프레임 위치 Position·회전 Angle·크기 Scale, 자식 Transform과 오프셋·배율·placement·자식 위치 기준값, 표본 여부 Sample이다. 순수 대조 입력·출력은 값 형식 Expected/RecordExpected에 보관한다. 즉 실제 저장은 연속적인 거대 struct 배열 대신 **풀링한 소유 객체 + 활성 List + 고정 크기 슬롯 배열**이다. 슬롯마다 새 클로저나 Tween을 만들지 않는다.

장식→State 사전은 입장·퇴장·새 효과 생성 때만 조회한다. 뜨거운 루프는 active 배열과 State.Slots를 순회한다. TightLate.Entry.Engine에 소유 참조를 직접 연결하므로 LateUpdate에서 다시 장식 사전을 조회하지 않는다. 엔진이 소유한 기록은 recs에서 실제로 제거한다. 슬롯 순서는 기존 recs의 생성 순서다. 축별 시계·Dur·Ease가 달라도 각 기록의 값을 따로 진행한다.

### Update

1. 기존 td 계산과 약 1e-6 정지 판정을 통과한 뒤, 기존 recs 루프 앞에서 `DecoEngine.Update(td)`를 부른다.
2. TightLate와 공유하는 Eligible 검사 및 자식 상태 검사를 한다. 실패하면 진행 전에 기존 recs로 돌려보내 그 프레임을 원래 루프가 처리한다.
3. 장식의 현재 위치·회전·크기를 한 번 읽는다. 엔진 입장 뒤 즉시 효과나 다른 Update가 쓴 값도 반영하기 위해서다.
4. 최대 5개의 시계를 원래 `Pos + td` 식으로 진행한다. 같은 연속 슬롯의 (Pos, Dur, Ease, Over, Period)가 비트 동일하면 이징 값을 재사용한다. 이징 함수는 기존 ZeroTween.Eval이고, 값 계산은 ApplyE와 같은 식 모양이다. C++/네이티브 계산은 추가하지 않았다.
5. 위치·회전의 완료는 FLast 계산 후 최종 필드에 FEnd를 사용한다. 크기는 기존 OnComplete에 별도 끝값 setter가 없으므로 플러그인 계산값을 유지한다. Running·Stepped·Completed와 InList를 갱신한다.
6. 장식당 pivotPosVec·rotAngle·scaleVec를 한 번씩 게시한다. 대상 기록에서 SetPosition/DeferTransform/SkipForLate/RegisterLate/Dormancy.Wake를 호출하지 않는다.

입장은 기존 첫 Step/Startup이 끝난 뒤, 같은 Update의 기존 루프 다음·Compact 전에 수행한다. 크기 Startup이 앞선 축 setter의 결과를 읽는 순서를 그대로 보존하려는 선택이다. 따라서 새 효과의 첫 프레임은 기존 경로이며, 다음 진행부터 엔진이 소유한다. NewRec가 생긴 프레임만 입장 후보를 다시 모으므로 매 프레임 전체 recs를 별도 분류하지 않는다. 다만 효과가 매 프레임 생기는 맵에서는 이 분류 비용도 매 프레임 생긴다.

### LateUpdate

TightLate.List의 기존 매니저 순서 안에서 Engine 참조가 있는 Entry는 DecoEngine.Late를 부른다. 카메라 위치는 기존과 같이 매니저 호출당 한 번 읽은 값이다. 필드 게시 뒤에 다른 효과가 값을 바꿨을 수 있어 Late에서 현재 필드를 다시 읽는다. 기존 TightLate.Apply 계산 코드를 인자화해 공유하며, parallax.startPosition/posCamAtStart/endPosition과 Transform.position/rotation/localScale을 기존 식·호출로 쓴다. SetPositionAndRotation으로 합치지 않았다. 기존 SkipSame 옵션도 유지한다.

이는 1단계의 의도적인 필드 읽기·쓰기 비용이다. 필드 왕복까지 제거하는 2단계는 아직 구현하지 않았다. 완료되어 소유 기록이 0개가 된 장식은 즉시 엔진에서 빠지며 그 뒤 Late는 보통 TightLate가 처리한다.

## 2. 대상 조건, 다른 키와의 독립성, 이탈

기본 조건은 TightLate.Eligible을 직접 재사용한다. 정확히 scrVisualDecoration, 히트박스·마스크·활성 메시 없음, rendererEnabled 및 rendererColor.a != 0, followPlanet·stickToFloor·lockRotation·lockScale 없음, 정상 시차 부품과 카메라/피벗 관계, clamp/dontAlterX/Y 없음, 캐시된 부모 관계와 단위 변환 조건을 따른다. 직전 매니저 프레임에 실제 처리된 Entry만 받는다.

추가로 편집기, Camera/CameraAspect placement, 자식 Transform 없음, 비정상 시간, 진짜 DOTween Shadow가 붙은 대상 기록, 중복 키, 5개 초과 슬롯은 제외한다. 입장 때 자식 localPosition이 원래 `(pivotOffsetVec + croppedPivotOffset) / trueScaleMultiplier`와 비트 동일한지 확인한다. 매 진행 때 오프셋·배율·placement·자식 참조와 실제 자식 localPosition도 검사한다. 마지막 Transform getter는 외부 코드가 자식만 바꾼 경우를 놓치지 않으려는 비용이며, 벤치에 포함했다.

동일 장식에서 기존 루프와 함께 돌 수 있는 키는 다음처럼 제한했다.

| 기존 루프에 남는 키 | 새 엔진과 동시 소유 조건 | 근거 |
|---|---|---|
| 색 9 | Startup 완료, CStart.a와 CEnd.a 비트 동일, CChange.a == 0, 알파 0.001~1 | SetColor는 color를 쓰고 ApplyColor를 부른다. scrVisualDecoration.ApplyColor는 color·opacity·stickToFloor·parentFloor만 읽으며 위치·회전·크기를 읽지 않는다. stickToFloor는 제외되어 있다. RGB 변화 중 가시성은 유지된다. |
| 불투명도 10 | Startup 완료, FStart와 FEnd 비트 동일, FChange == 0, 값 0.001~100 | 같은 ApplyColor 경로이며 가시성을 바꾸지 않는 상수 기록만 허용한다. |
| 알파가 변하는 색/불투명도 | 장식 전체 기존 경로 | 중간에 GetVisible이 달라지면 기존 위치/회전 setter의 즉시 쓰기 여부가 달라질 수 있다. 순서 독립성을 증명하지 않았다. |
| 피벗 3·4, 시차 11~13 및 그 밖 | 장식 전체 기존 경로 | 자식 위치·시차와의 의존성을 보수적으로 제외했다. |

따라서 **임의의 혼합 키를 전부 지원한 것은 아니다.** 알파 일정 RGB 기록과 변환 기록의 동시 소유는 구현했으며, 나머지는 불확실하면 기존 경로라는 지시에 따라 제외했다. 효과 생성 시 NewRec가 해당 장식의 소유를 먼저 돌려놓으므로 새 키가 기존 슬롯과 몰래 섞이지 않는다.

일반 이탈은 현재 필드가 이미 게시된 상태에서 남은 Rec를 돌려놓고 EngineOrder로 한 번 정렬한다. 이것은 소유권 경계 작업이며 프레임별 순서 병합이 아니다. Update 진행 전 이탈은 같은 프레임에 기존 루프가 진행한다. Late에서 가시성 등이 바뀌면 기존 LogicMaybe 분기로 복귀한다. 예외로 일부 기록만 진행한 경우 그 기록만 현재 프레임 재진행을 막고, 아직 진행하지 않은 기록은 원래 예외 경계를 포함한 기존 루프에 맡긴다. 예외 장식은 그 Entry 수명 동안 재입장을 막는다. 자식 Transform getter 등 입장/진행 전 검사 자체가 예외를 던지는 경우도 장식별로 복귀한다. 편집기 여부 확인은 엔진 Update당 한 번으로 올렸다.

외부 setter 패치는 초기 Configure에서 소유자를 확인해 제외한다. PACL이라는 이름을 가진 로드 어셈블리가 있으면 엔진 전체 소유를 보류한다. 이번 설치에는 PACL2가 없어 FollowDecoration/OrbitDecoration의 실제 IL을 확인하지 못했다. 실행 후 동적으로 추가되는 패치·어셈블리까지 계속 감시하지 않는다. 외부 모드 구성이 달라지면 아래 IL 조사와 새 프로세스 검증이 필요하다.

## 3. 필드 읽는 코드 조사와 2단계 동기화 설계

`tools/ilscan`에 `DECO_READERS=1` 모드를 추가했다. 모든 메서드의 ldfld/ldflda 및 대상 메서드 호출을 조사한다. FieldRef처럼 직접 필드 토큰이 없는 경우를 보완하기 위해 대상 이름 ldstr도 StringReference로 별도 기록한다. **문자열 참조는 읽는 메서드를 확정한 결과가 아니다.** 소스 호출 경로와 함께 검토해야 한다. 런타임 생성 DynamicMethod·계산해서 만든 문자열·아직 설치하지 않은 DLL까지 IL 정적 조사로 증명할 수는 없다.

실행: `dotnet build tools/ilscan/ILScan.csproj -c Release`, 이어서 `py -3 tools/ilscan/deco-readers.py`.

게임 Managed 하위와 Mods 하위 DLL 및 새 개발자 DLL까지 총 244개 조사: 240개 정상 파싱, 4개 비대상/오류 기록. 결과 153행 = 직접 필드 읽기 93 + 주소 읽기 1 + 호출 5 + 문자열 참조 54. 게임 Assembly-CSharp 41행, 설치된 기존 StutterFix 39행, 새 개발자 StutterFix 73행이다. 설치된 DLL은 읽기만 했다. 각 파일 SHA-256과 오류 원문은 `decoengine-evidence/readers-inventory.json`, 메서드별 전수 표는 `decoengine-evidence/readers.tsv`에 있다.

4개는 libdeflate/sfnative/turbojpeg 네이티브 DLL(관리 메타데이터 없음)과 YouTubeStream의 macOS 메타 파일 `._YouTubeStream.dll`(정상 PE가 아님)이다. 이를 관리 소비자 0개로 잘못 집계하지 않았다. 다른 설치 모드에서는 이번 대상의 직접 IL/이름 문자열 참조가 발견되지 않았다.

| 소비자/경로 | 조사 결과 및 읽는 상태 | 1단계 | 2단계 동기화 위치(설계만) |
|---|---|---|---|
| scrDecoration.SetPositionX/Y, SetPivotX/Y | pivotPosVec·pivotOffsetVec | 매 Update 필드 게시 | setter 진입 직전에 해당 장식 Sync |
| scrDecoration.UpdateScreenClamp, UpdatePosition | 위치·오프셋, 회전·크기 | 게시된 값 사용 | 원래 경로 진입 전에 Sync; 엔진 자체 Late는 소유 상태 사용 |
| ffxMoveDecorationsPlus.StartEffect 및 DisplayClass 콜백 | 위치·오프셋·회전, 크기 getter | 기존 첫 Step/Startup 유지 | 효과 시작 전에 해당 대상들 Sync, Startup에서도 Sync |
| scrParallax.SetTrans/GetPositionWithParallax | startPosition·posCamAtStart·endPosition | 기존 Late 식으로 게시 | parallax→소유 장식으로 찾아 Sync; 참조 반환/주소 읽기도 포함 |
| scrVisualDecoration.UpdateShader | scaleVec | 필드 게시, 마스크 소유 제외 | 호출 전 대상과 참조 마스크 Sync 또는 해당 군 제외 |
| scrDecoration.GetDecorationWorldSize | 기본 구현은 zero, 실제 가상 호출 소비자도 조사 | 필드 게시 | 오버라이드별 확인 후 getter prefix |
| DecoTransformGizmoHolder.LateUpdate/DragStart/Drag | pivotPosVec·pivotOffsetVec, GetPositionWithParallax/GetDecorationWorldSize | 편집기 소유 제외 | 편집기 계속 제외하는 것이 단순함 |
| scnEditor.DragDecorationsStart/DragDecorations/GoToDecoration/MoveCameraToDecoration | posCamAtStart·pivotOffsetVec | 편집기 제외 | 동일 |
| ADOFAI.DecorationPivot.UpdatePivotCrossImage | pivotPosVec | 편집기 제외 | 동일 |
| scrDecorationManager.UpdateBordersSizes/FinishUpdateDecorations | scaleVec·pivotOffsetVec | 게시된 값 | 매니저 읽기 직전 대상 Sync |
| scrCustomBackgroundSprite.LateUpdate | posCamAtStart | 해당 타입 소유 제외 | 계속 제외 |
| PACL2 FollowDecoration/OrbitDecoration | DLL 미설치로 IL 확인 불가 | PACL 이름 로드 시 전체 제외 | 해당 장식군 제외를 우선; 실제 DLL 확보 후 reader prefix 검토 |
| FastMove/InstantMove/DecoAnim.Pos/Rot/Scale/Startup | FieldRef를 통한 위치·회전·크기 및 효과 시작값 | 이미 게시된 값, NewRec 앞 소유 해제 | 모드 코드 직접 Sync 호출을 넣을 위치 |
| MoveApply, InvisibleSkip/Precheck/ZeroTween.Snap | setter·미룬 값·Snap/FieldRef 경로 | 게시된 값, 숨김 이탈 | 해당 작업 진입 때 Sync 또는 소유 해제 |
| DecoSig(TileSig.cs)·LoadFix 검증 | FieldRef 초기화 문자열 + 모드 소스 확인 | 게시된 값으로 검사 | 측정 직전 SyncAll; 해시 계산 자체는 그대로 |
| ExitFix.Read/Same/LightOne 계열 | 직접 위치·오프셋·회전·크기 | reset/경계 처리 후 사용 | reset snapshot/복원 전 SyncAll + 소유 해제 |
| TightLate/DecoJobShadow/PosFast | 직접 필드/FieldRef | 공유 입력, 독립 진단 옵션 유지 | 진단 시작 전에 Sync 또는 엔진 소유 입력 지원 |
| Kill(true), Complete, PlayingTweens, KillAll | 활성 표·완료 입력 | 원래 Rec 경로로 먼저 복귀 | Sync 후 원래 완료 처리; 누락하면 끝값/활성 목록이 깨짐 |

### 프레임 필드 게시 대 게으른 동기화

| 방식 | 매 프레임 비용 | 위험/추가 작업 | 이번 상태 |
|---|---|---|---|
| 1단계 필드 게시 | 장식당 Vector2 두 개+float 쓰기, Update/Late 재읽기. 실제 µs는 미측정 | 기존 소비자는 최신 필드를 읽음. 순서 의존 키·외부 패치 제외 필요 | 구현 |
| 2단계 게으른 동기화 | 소비자가 없으면 해당 필드 왕복 절약 가능. reader 호출이 잦으면 Sync 조회/훅 비용이 절약보다 커질 수 있음 | 위 표의 직접·간접·가상·FieldRef·외부 모드 소비자 모두 처리, 재진입/예외/수명 관리 필요 | 설계만 |

게으른 동기화 채택 전에는 Sync 호출/프레임, Sync된 장식 수, 훅 수·설치 시간, 추가 할당, 외부 모드별 제외율을 기록해야 한다. 비용 절약은 **추정**이며 이번 작업에서 µs 수치를 만들지 않았다. 수십 개 reader를 Harmony로 감싸는 것보다 모드 코드 직접 호출과 외부 모드 제외를 우선한다.

## 4. 수명 이벤트와 정합성

| 사건 | 처리 |
|---|---|
| NewRec/이전 키 Kill | 소유 장식의 남은 기록을 먼저 원래 목록에 돌려놓는다. 재사용은 기존 InList 조건을 따른다. |
| 첫 Startup | 원래 Step에서 수행한 뒤 입장한다. Shadow가 붙은 대상 기록은 제외한다. |
| 자연 완료 | Running=false, Completed 증가, Engine/InList 해제. 마지막 기록이면 State도 풀로 반환한다. |
| Kill(false)/Kill(true)/Complete | 장식의 기록을 복귀시킨 뒤 기존 Kill/Complete를 실행한다. 완료 콜백의 원래 setter도 그대로다. |
| PlayingTweens | 전체 소유를 복귀시켜 기존 AddRunning 경로에 누락 없이 포함한다. |
| KillAll/DropAll/SceneReset | 먼저 전체 복귀, 기존 완료 또는 폐기 처리, 엔진 참조 정리. |
| Compact | 엔진 소유는 recs에서 빠져 있다. 안전망으로 Engine 참조가 남은 항목도 재진행 없이 제외한다. |
| ForgetLevel/Hitch.SongEnded | DropLevel을 연결했다. 활성 소유가 있으면 DropAll로 폐기하고 풀·사전 참조를 비운다. 엔진을 켠 실험의 경계 처리다. |
| TightLate.Forget/매니저 교체 | 먼저 Rec 소유를 복귀시킨 뒤 Entry 캐시를 비운다. |
| Main.Unload | 기존 FinishAll 뒤 DecoEngine.Uninstall, 참조와 Enabled/verify 해제. 새 Harmony ID·이벤트·PlayerLoop 등록 없음. |
| Dormancy | 직전 매니저에서 처리된 가시 장식만 입장. 소유 중 Wake 해시 호출 없음. 숨겨지거나 매니저에서 누락되면 복귀한다. |
| InvisibleSkip/알파 0 | Eligible에서 제외. 가시성을 바꾸는 활성 색/불투명도는 처음부터 장식 전체 제외. |
| LazySprite/즉시 효과 | 게시된 필드 유지. Late에서 현재 필드를 다시 읽어 Update 뒤 변경을 반영한다. |
| ExitFix | 새 맵/리셋 경계에서 위 정리가 연결되며 필드 게시 방식이라 snapshot에 늦은 값이 남지 않는다. |
| 예외/검증 불일치 | 장식별 원래 소유로 복귀, 이미 진행한 기록은 같은 Update에서 중복 진행하지 않음. 해당 Entry 재입장 금지. |

## 5. 검증·벤치·계측

모드 초기화 시 모드 폴더에 `decoengine-on` 파일이 있을 때만 켜진다. 이번 작업에서는 그 파일을 Mods에 만들지 않았다. 기본 끔 상태는 기존 경로이며, 기존 실험 옵션과 충돌하면 소유를 보류한다. GroupStep/DecoBatch/NativeMotion 동시 소유는 제외한다. TightLate·PairXY·DeferTransform·Dormancy·InvisibleSkip·LazySprite의 기존 경로는 계속 살아 있다.

개발자용/PlayerAuto 자동 시험 명령:

- `decoengine state`: enabled/compatibility/현재 소유 장식 수, 엔진 진행 프레임당 소유 장식·기록, admissions, 필드 게시 sync, 소유 복귀 restores, fallback, 이유 배열, checks/mismatch/첫 불일치. 이 통계는 세션 누계이며 deco/frame·rec/frame의 분모는 엔진이 실제로 진행한 Update 수다. fuse의 전체 프레임 분모와 다르다.
- 이유 배열 0~9: 정상 자리 / 타입 / 히트박스·마스크·메시 / 숨김 / 따라가기·고정 / 시차 / 부모 / 관리자·수명·거부 / 입장 제외 / 자식·오프셋 상태 변경. 8번은 혼합 키·Shadow·첫 프레임·자식 기준 등 여러 입장 제외를 합친 값이다.
- `decoengine verify` / `decoengine verify off`: 입장 순서 기준 장식 1/16 표본. 같은 입력으로 별도 시계를 진행하고 ZeroTween.Eval을 독립 호출하는 순수 참조 계산을 수행한다. 키별 Pos/FLast/VLast/Running/Stepped와 게시된 위치·회전·크기를 비교한다. Late에서는 기존 필드 기준 식으로 만든 parallax 상태 및 Transform setter 입력을 비트 비교한다. 참조 계산은 필드·Transform을 쓰지 않는다.
- **순수 대조의 Transform.rotation은 setter 입력 대조다.** Unity가 rotation setter 안에서 정규화한 뒤 getter로 돌려주는 결과까지 순수 C# 계산으로 증명하지는 못한다. 실제 getter 결과와 원래 UpdatePosition 경로 대조는 `tightlate verify` 및 `late-verify`를 함께 써야 한다. 기존 late-verify도 엔진 소유 장식을 표본으로 비교하도록 TightLate.Process에 연결했다.
- `decoengine bench`: 메뉴에서만 허용한다. 실제 Unity Transform을 가진 더미 2,150개, 장식당 Rec 5개, 준비 5회·측정 200회다. Update 구간은 Eligible·자식 guard·진행·필드 게시, Late 구간은 Eligible·공유 TightLate 식·Transform 쓰기를 포함한다. update/late/합 µs/장식 및 ms/프레임을 출력한다. 입장 분류·폴백 정렬·매니저 목록 동기화·실제 맵 구성은 제외한다. 게임 재생 중에는 거부한다. 실제 장식 목록이나 소유 사전에 더미를 넣지 않고, 사용하는 통계는 finally에서 복원한다. **이번에는 실행하지 않았다.**

FuseProbe는 UpdateMs 누계와 매니저 LateUpdate prefix/postfix를 잰다. 엔진 Update/입장 분류는 기존 t0~UpdateMs 누계 안, 엔진 Late는 매니저 범위 안에 있다. 따라서 비교 시 엔진 시간을 별도로 빼면 안 된다. `decosamp`의 전체 루프 시간에도 포함되며 개별 키 시간에는 빠지므로 엔진 state를 출력에 붙였다. 입장 분류를 Compact 시간으로 잘못 세지 않게 Compact 타이머 시작 위치를 뒤로 옮겼다. `Update/Advance/Late`는 NoInlining으로 profsamp 이름 식별을 돕는다. 스텝마다 Stopwatch를 추가하지 않았다.

진단을 끈 정상 진행에서는 로그/문자열을 만들지 않는다. 배열 확장·풀 확장·사전 작업은 입장/퇴장 시 생긴다. 검증을 켜면 독립 Eval·필드 비교가 추가되며, 벤치는 한 번에 많은 Unity 오브젝트를 만든다. 성능 A/B에는 둘 다 섞지 않는다. 자식 위치 getter, 두 차례 Eligible, 효과가 잦을 때의 재입장/복귀 정렬은 남아 있는 실제 비용이므로 아래 A/B에서 반드시 함께 판단한다.

### 실행한 오프라인 확인

| 항목 | 결과/증거 |
|---|---|
| 개발자 빌드 | 오류 0, 경고 35. `decoengine-evidence/build-dev.log` |
| Player 빌드 | 오류 0, 경고 33. `decoengine-evidence/build-player.log` |
| PlayerAuto 빌드 | 오류 0, 경고 35. `decoengine-evidence/build-playerauto.log` |
| 실제 DecoEngine.cs 연결 대체 환경 검사 | 358,430 assertions 통과. 독점 소유, 축·시계별 진행, 정확한 끝값, 알파 일정 RGB 공존, 변하는 알파 제외, Shadow/Startup 제외, 순서 복원, 자식/오프셋 변경, 예외 주입·부분 진행 중복 방지, 네이티브 guard 예외 격리, 경계 폐기, 해제를 검사. `decoengine-evidence/ownership-check.log` |
| 기존 갱신 경로 회귀 | 기존 GroupStepCheck가 실제 Rec/Step/StepPair/Compact/Complete/UpdatePrefix를 추출해 검사. 80개 Update 사례, 콜백 append·앞선 Complete·InList 타이밍 및 기존 묶음/예외/정리 검사 통과. `decoengine-evidence/groupstep-regression.log` |
| 텍스트/공백 | SFENC001 clean, git diff --check 통과. 결과 문서 EF BB BF BOM·엄격 UTF-8 디코딩·대체 문자 0개 확인. Get-Content -Encoding UTF8로 한글 직접 재확인. `decoengine-evidence/result-encoding.json` |

대체 환경 검사는 .NET 8의 관리 스텁이다. **Unity Mono float 의미·실제 Transform·Harmony 호출 순서·실게임 성능의 증거가 아니다.** 회귀 도구가 부수적으로 출력하는 .NET 벤치 숫자도 실게임 채택 근거에 포함하지 않는다. 실게임 verify/bench/fuse/profsamp/decosig 실행은 전부 미실시다.

## 6. 비용 예산과 Claude 실전 확인 순서

인계에서 받은 실측 기준은 모드 애니메이션 1.87~1.91ms + 관리자 LateUpdate 1.03~1.06ms, 합 약 2.9ms/프레임이다. 위치 쌍 0.71 + 회전 0.22 + 크기 0.29 + Compact 일부 + TightLate 대상 부분 약 0.65가 이번에 겨냥한 약 1.9ms다. 이 수치는 이번 실행으로 새로 측정한 값이 아니다.

설계 예산은 진행·필드 게시·guard 약 0.20µs/소유 장식, Transform 계산·가시성 판정·setter 약 0.30µs/대상 장식이다. 합 0.50µs를 더미 2,150개에 적용하면 1.075ms다. 이는 목표 예산이지 달성 수치가 아니다. 실제로는 움직이는 장식 수와 Late 대상 장식 수가 다르고, 색 비용·폴백·입장 분류가 더해진다. 잔여 0.5~0.8ms를 가정하면 합 1.6~1.9ms가 될 수 있다는 정도의 **추정**만 가능하다. 보수적 제외 때문에 소유율이 낮거나 새 효과가 자주 와서 매번 풀리면 이득이 없을 수도 있다.

채택 기준은 같은 구간 fuse 두 항목 합 **1.8ms 이하**, 비트 검증 불일치 0, 장식 상태/화면 변화 없음, 최악 프레임/하위 백분위 악화 없음이다. 마이크로 벤치만 통과한 것은 채택이 아니다. 이번 작업의 결론은 기본 끔 후보 구현이며 채택 판정은 보류다.

아래는 Claude가 별도 권한 범위에서 수행할 후속 순서다. 이 문서 자체가 이번 작업의 게임 실행·설치 금지를 해제하지는 않는다.

1. 새 DLL을 사용할 준비를 한 뒤 다른 옵션·화면 해상도/주사율·동시 모드를 고정한다. GroupStep/DecoBatch/NativeMotion은 끄고 TightLate 및 기존 기본 최적화를 유지한다. 실행 중 파일만 바꾸거나 같은 DLL reload로 A/B하지 않는다.
2. 별도 정확성 판에서 decoengine-on 켬, 새 프로세스로 Arche를 연다. `decoengine state`의 compatibility와 owned/admissions가 실제로 증가하는지 확인한다. 소유 0/검사 0의 mismatch=0은 통과가 아니다.
3. `decoengine verify`, `tightlate verify`, `late-verify`를 켜고 Arche 257.8~278.8초와 알파 변화·피벗·시차 효과 전환 구간을 확인한다. 장식별 제외 이유·복귀 횟수도 기록한다. reset/retry/중간 시작/곡 끝/맵 전환에서 표가 남지 않는지 확인한다.
4. 메뉴에서 `decoengine bench`를 한 번 실행한다. 실전 성능 판과 분리한다. guard 실패 출력은 측정 실패이며 숫자를 채택하지 않는다.
5. 정확성 판과 분리한 새 프로세스 ABBA: A=파일 없음, B=파일 있음, 순서 A→B→B→A. 매 판 같은 Arche 257.8~278.8초에 `fuse begin/end`. 검증 옵션은 끈다. 두 시간 합, 구간 프레임 수, 소유율, 입장·복귀 횟수, 최악 프레임과 1%/0.1% low를 기록한다.
6. 별도 표본 판에서 `profsamp begin 1000`/`profsamp end`, 필요하면 decosamp를 쓴다. DecoEngine.Update/Advance/Late 및 입장 분류·정렬·guard가 어디에 비용을 옮겼는지 확인한다. 표본 수집 판을 깨끗한 성능 판과 혼동하지 않는다.
7. 파일 켬/끔 각각 새 프로세스로 Arche·HELLO 전곡. `decoengine verify` 및 기존 tightlate/late 검증을 켠 정확성 판과 검증을 끈 성능 판을 구분한다. 곡 끝 `decosig` 대조는 기존 카메라/계속 도는 회전 차이와 새 차이를 장식별로 구분한다. 새 차이를 기존 잡음으로 자동 무시하지 않는다.
8. 동등성과 1.8ms 기준을 모두 만족할 때만 채택을 검토한다. 미달이면 기본 끔 유지. 2단계 게으른 동기화나 더 넓은 혼합 키 지원은 reader/호환성 증거를 보강한 별도 작업으로 진행한다.

기존 작업자의 `.codex/config.toml` 변경과 세션 시작 때 존재하던 미추적 디렉터리는 수정하지 않았다. 결과 파일은 apply_patch로 UTF-8 BOM을 포함해 작성했다.
