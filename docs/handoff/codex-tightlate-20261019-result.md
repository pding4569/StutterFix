# TightLate 작업 결과 — 2026-10-19

구현과 오프라인 검사를 마쳤다. **게임 실행·Mods 설치·커밋·푸시는 하지 않았다.** Unity 안에서의 비트 일치와 장식당 비용은 미측정이다. 따라서 목표인 LateUpdate 1ms 이하 또는 장식당 0.5µs 이하를 달성했다고 판단하지 않는다. `tightlate bench`를 구현했지만 실행에는 Unity 런타임이 필요하므로 이번 실행 금지 범위에서는 수치를 만들지 않았다.

## 변경 파일

| 파일 | 변경 |
|---|---|
| `TightLate.cs` | 순서 보존 루프, 관리 필드 판정, 부모 캐시, 계산·쓰기, 표본 대조, 장식별 제외, 통계·더미 벤치 |
| `MoveApply.cs` | 매니저 LateUpdate의 두 allDecorations 읽기를 `TightLate.List`로 연결 |
| `Dormancy.cs` | 새 맵 정리에서 TightLate 참조 해제 |
| `SceneReset.cs` | 재시작·편집기 복귀 시 참조 해제 |
| `Hitch.cs` | 곡 종료 시 참조 해제 |
| `StutterFix.cs` | 설치, 모든 빌드의 `tightlate-off` 파일 처리, UninstallAll 정리 |
| `StutterFix.csproj` | 명시 Compile 항목 추가 |
| `AutoTest.cs` | `tightlate state`, `verify`, `verify off`, `bench` 명령 |

작업 전부터 있던 `.codex/config.toml` 변경과 미추적 파일은 유지했다. 빌드·설치 훅과 `/ship`은 호출하지 않았다. 빌드는 프로젝트의 bin 디렉터리에만 출력했다.

## 이전 실패를 반영한 구조

FuseLate 결과 문서와 원본 IL, jobs 결과 문서와 현재 DecoJobShadow.WriteTest를 읽었다. 이 체크아웃의 `git log --all -- FuseLate.cs`에는 결과가 없어 `git show <commit>:FuseLate.cs`로 삭제 소스를 확인할 커밋을 찾지 못했다. 과거 소스를 복구했다고 주장하지 않는다. 인계 문서에 제시된 FuseLate의 실측 악화 및 Transform 쓰기 비용은 이번 측정값이 아니다.

새 장식별 UpdatePosition prefix를 추가하지 않았다. 안정된 순회 슬롯은 배열 인덱스로 Entry를 찾고, Dictionary는 슬롯의 장식 참조가 달라졌을 때만 사용한다. 부모 관계만 캐시하고, 바뀔 수 있는 관리 필드는 현재 값을 직접 읽는다. 프레임마다 장식별 ConditionalWeakTable 조회, 카메라·프레임 캐시 검색, Transform 상태 읽기를 하지 않는다.

별도 prefix에서 배치를 먼저 처리한 뒤 나머지를 순회하는 방식은 쓰지 않았다. 원본 매니저는 타일링·알파 마스크 캐시 갱신 **뒤에** allDecorations를 읽는다. 그 지점에서 `Dormancy.List`를 받아 한 루프 안에서 빠른 처리 또는 기존 `MoveApply.LogicMaybe`를 원래 순서대로 실행한 뒤 빈 목록을 반환한다. 원래 Count가 0이 되어 뒤의 중복 순회가 사라지고, 매니저 끝의 CheckUpdateFrameRateScreen은 유지된다. 이 방식은 장식 사이 순서를 바꾸지 않으므로 다른 장식 Transform을 읽는 갈래와의 순서 독립성을 가정할 필요가 없다.

매니저 prefix는 타이머와 호출별 완료 표시 초기화만 한다. 같은 프레임에 매니저가 두 번 호출되어도 각각 처리한다. Dormancy.NewFrame과 기존 MoveApply postfix는 유지했다. 이번 루프가 회전·크기까지 반영하므로 DeferTransform이 값만 저장한 장식도 처리된다. 제외 장식은 원래 LogicMaybe가 처리하고, 보이지 않게 된 lateSkipped 장식은 기존 postfix가 보정한다. 기존 late-verify와 decosig는 변경하지 않았다.

## 허용 조건과 보임 판정

빠른 경로는 재생 중이며 SceneReset 밖이고, MoveApply·Dormancy가 켜져 있고, 커스텀 맵과 disableV15Features가 있을 때만 사용한다.

- 정확히 scrVisualDecoration, 히트박스 없음, 마스크 아님, meshRendererEnabled=false.
- rendererEnabled=true이고 rendererColor.a가 정확히 0이 아님.
- followPlanet 관리 참조 null, stickToFloor/lockRotation/lockScale=false.
- parallax/pivot 관리 참조가 있고 inited/isDecoration=true. decoration이 자기 자신, cachedTransform이 pivotTrans, cameraTransform이 현재 카메라와 같은 참조.
- clampToScreen/dontAlterX/dontAlterY=false.
- pivot 부모가 현재 매니저 Transform이고 그 부모의 위치·회전·크기가 성분별 정확히 원점·단위 변환.

**인계 문서의 GetVisible 설명과 현재 DLL은 다르다.** 현재 scrVisualDecoration.GetVisible은 rendererEnabled를 읽고, GetAlpha()==0 및 isMask/useHitbox를 분기한다. GetAlpha는 rendererColor.a다. 이 타입의 현재 함수에는 spriteRenderer.enabled나 activeInHierarchy 조회가 없다. 위 비마스크·히트박스 없음 조건에서 `rendererEnabled && rendererColor.a != 0f`와 동치다. Dormancy의 awake를 곧 visible로 가정하지 않았다. 막 깨어난 숨은 장식과 메시가 켜진 장식도 원래 경로로 보낸다.

disableShader=true이고 meshRendererEnabled=false인 visual의 UpdateShader는 EnableMeshRenderer(false)에서 변경할 상태가 없다. 히트박스도 없으므로 이 갈래의 LogicUpdate에서 남는 작업은 UpdatePosition이다. 마스크와 메시가 켜진 장식은 이 생략을 적용하지 않는다.

## IL 조사·캐시 수명

[현재 게임 IL 근거](codex-tightlate-20261019-il.txt)는 ILScan `METHOD=* FIELDS=1`의 전체 Assembly-CSharp 스캔에서 27개 필드의 쓰기 목록, 관련 타입 메서드, 직접 부모 변경 호출자를 추린 것이다. 검사 DLL SHA256:

`6ba2cfb8d260962d3c8a3e4409f60e8e98293ca9b2d2eca4875096bd02d40230`

- textureScaleMultiplier는 생성자·SetTextureScaleMultiplier, 시차 배율은 SetParallax/set_multiplier 등, clamp는 UpdateScreenClamp에서 바뀐다. Setup은 startRot/scaleMultiplier/lockRotation/lockScale 등을 쓴다. 이 값들은 캐시하지 않아서 SetSprite/ResizeCollider/이동 효과 뒤에도 다음 처리에서 현재 값을 읽는다.
- rendererEnabled/rendererColor, maskingType, meshRendererEnabled도 각각 SetVisible/ApplyColor/SetMaskingType/EnableMeshRenderer의 현재 관리 필드로 판정한다.
- pivotTrans/parallax는 CreateDecoration에서 정한다. scrPrefabDecoration의 별도 pivot 구조는 타입 조건으로 제외한다. pivot 참조가 바뀌면 부모 캐시를 다시 확인한다.
- 직접 SetParent/set_parent 호출 전체 목록에는 실제 장식의 부모를 바꾸는 편집기 OnItemDropSides가 있다. 정확한 타입 `ADOFAI.LevelEditor.Controls.PropertyControl_DecorationsList`의 전후에 무효화를 연결했다. Setup도 무효화한다. ffxMoveDecorationsPlus에는 직접 부모 변경 호출이 없다.
- 매니저 및 일반 이미지 장식의 조사한 직접 게임 경로에서 곡 중 매니저 부모 변환을 옮기는 경로는 찾지 못했다. 초기 매니저·Setup·편집기 재배치·카메라 변경 때 부모 단위 변환을 검사한다. **이 정적 호출 조사는 리플렉션·네이티브 코드·다른 모드의 임의 Transform 조작까지 증명하지 않는다.** 그 경우 캐시가 낡을 위험이 남는다.
- 새 맵, SceneReset, 곡 종료, UninstallAll에서 Dictionary·슬롯의 장식/Transform 참조를 놓는다. 진단 누계는 유지한다. 새 Harmony 패치는 메인 모드 ID 소유이므로 기존 UnpatchAll로 해제한다.

우회 대상 LogicUpdate/UpdatePosition/SetRotation/SetScale, 배율 getter, visual 보임·마스크·셰이더 함수 및 parallax 함수에 메인 모드 ID 이외의 Harmony 소유자가 있으면 기존 순회를 유지하고 해당 함수·소유자를 기록한다. Enabled를 바꾸지 않으며 state의 compatibility로 원인을 확인할 수 있다. 검사는 초기화·캐시 무효화 시 수행한다. 그 뒤 곡 중 동적으로 설치된 외부 패치는 즉시 감지하지 못한다.

## 계산·비트 대조

원본의 `pivotPosVec + Vector2.zero`, Vector2→Vector3→Vector2 변환, Vector3 카메라 차→Vector2 변환→성분 곱→덧셈 순서를 유지했다. offset은 `parallaxOffset * camScale / textureScaleMultiplier`, 크기는 `scaleVec * camScale * (Vector2)Vector3.one` 순서다. lockScale은 제외했으므로 camScale은 원본 getter의 `1f * (scaleMultiplier * textureScaleMultiplier)`다. startPosition/posCamAtStart/endPosition도 저장한다. rotAngle/scaleVec는 입력의 비트가 그대로 남는다.

position·rotation·localScale 설정자를 각각 사용한다. SetPositionAndRotation으로 합치지 않았다. 회전은 원본과 같은 Quaternion.Euler와 rotation 설정자를 거친다. 원본의 중간 localPosition 쓰기는 단위 부모 조건에서 최종 world position 계산으로 대체했다. 네이티브 좌표 변환·부호 있는 0·숨은 shear에서의 비트 일치는 실제 게임 대조 전에는 확정할 수 없다.

`tightlate verify`를 실행하면 처리 누계 64개마다 하나를 같은 프레임에 원래 UpdatePosition으로 재실행한다. TightLate에는 UpdatePosition prefix가 없으므로 별도 bypass가 필요 없으며 기존 패치 체인을 통과한다. position/rotation/localScale, parallax의 세 필드, rotAngle/scaleVec의 모든 float를 SingleToInt32Bits로 비교한다. +0/-0도 구분한다. 불일치 개수와 첫 장식·필드별 일치 여부·전후 값을 로그에 남기고 **그 장식만** 이번 맵의 원래 경로로 보낸다. 전역 Enabled는 유지한다.

허용 갈래는 매 호출에서 startPosition과 posCamAtStart를 먼저 덮어쓰며, 이전 endPosition 또는 position.x/y를 최종 계산 입력으로 쓰지 않는다. 따라서 동일 입력으로 최종 상태를 다시 쓰는 검증 방식이다. 네이티브 dirty 플래그까지 부작용이 없다고 증명한 것은 아니다. 비교가 켜진 판은 재실행·Transform 읽기 비용이 추가되므로 성능 판으로 사용하지 않는다. `tightlate verify off`로 끈다. 순수 Player에는 비교·벤치 본문과 AutoTest 명령이 없다.

## 비용 예산과 미측정 벤치

설계 예산은 장식당 약 0.3µs, 허용 상한 0.5µs다. 2,150개 기준 각각 약 0.645ms/1.075ms이므로 0.5µs를 통과해도 전체 매니저 1ms 이하는 별도로 확인해야 한다.

정상 빠른 경로의 네이티브 Transform 호출은 장식당 쓰기 3회, 카메라 position 읽기는 매니저 호출당 1회다. 부모 조회와 단위 변환 읽기는 초기화·무효화 때만 한다. 컴파일된 Eligible+Apply에는 정적 IL 기준 ldfld 34개와 FieldRef 호출 9개가 있으며 후자는 읽기 7개·쓰기 2개다. isMask의 관리 필드 읽기 등을 포함한 예산은 약 42개 이상의 관리 필드 읽기다. 슬롯·카운터 접근은 별도다. 이는 시간 실측이 아니다.

`tightlate bench`는 실제 맵 장식을 건드리지 않고 더미 Transform 2,150개를 만든다. 관리 장식/parallax 입력은 GetUninitializedObject로 만든 필드 컨테이너라 AddComponent/Awake를 실행하지 않는다. 제품과 같은 Process(Eligible+Apply)를 5회 예열하고 200회 반복한다. 반복마다 카메라 X를 바꾼다. 출력은 us/deco, ms/frame, 0.5µs 통과 여부다. finally에서 더미 부모와 자식을 Destroy한다. 슬롯 동기화·초기 부모 조회·fallback·verify 비용은 제외하므로 전체 루프 시간과 함께 봐야 한다. 이 명령의 Unity 내 실행 자체도 아직 검증하지 않았다.

**이번 벤치 결과: 미측정.** 게임 실행 금지로 µs 수치를 얻지 않았다. 0.5µs 초과 여부와 그 원인 조사는 Claude의 실행 후 남은 작업이다. 첫 진입의 Entry/Dictionary/배열 할당, 새로 깨어난 장식의 캐시 추가 비용도 실전에서 확인해야 한다.

`tightlate state`는 Enabled/설치 상태, 캐시 수, 마지막 처리 수, 누적 처리·프레임 수, 무효화 수, 제외 이유별 누계, 검증 수/불일치/첫 예, compatibility를 출력한다. loop-ms/frame은 슬롯 동기화·조건 검사·fallback·대조를 포함하는 순회 구간의 Stopwatch 1쌍이다. manager-ms/frame은 prefix/finalizer 1쌍으로 잰 전체 매니저 시간이며 off 판에서도 잰다. **on 판의 manager 시간은 새 루프를 포함한다.** 구간 성능은 상태 전후의 누계 차이로 확인하거나 별도 fuse begin/end로 잰다. 원본 처리만 따로 빼서 측정한 시간으로 해석하지 않는다.

## 오프라인 확인

| 최종 명령 | 오류 | 경고 | FieldRefAccess 검사 |
|---|---:|---:|---|
| `dotnet build -c Debug -p:Edition=Player -p:AutoTestBuild=1` | 0 | 35 | 249곳, 타입 불일치 0 |
| `dotnet build -c Debug -p:Edition=Player` | 0 | 25 | 223곳, 타입 불일치 0 |
| `dotnet build -c Debug` | 0 | 35 | 249곳, 타입 불일치 0 |

로그: [PlayerAuto](codex-tightlate-20261019-playerauto-build.txt), [Player](codex-tightlate-20261019-player-build.txt), [Dev](codex-tightlate-20261019-dev-build.txt). LoadCheck의 동적 이름 5곳은 실행 중 확인 대상으로 남는다. 경고에는 기존 어셈블리 버전 충돌·미사용 필드 등이 있다.

컴파일된 IL에서 Apply의 Transform setter가 position/rotation/localScale 정확히 3개이고 Transform getter는 0개임을 assertion으로 확인했다. Eligible의 유일한 Transform 호출은 캐시 갱신 분기 안의 get_parent이며, GetVisible 또는 Unity Object 비교 호출은 없다. 원본 매니저는 allDecorations 읽기 2곳과 LogicUpdate 1곳이다. `git diff --check`도 통과했다. 이 검사는 Harmony의 실제 설치·Unity 네이티브 결과 검증을 대체하지 않는다.

## Claude 실행 순서

1. 사용자 허용을 받은 별도 게임 검증 단계에서 동일 PlayerAuto DLL을 준비한다. 이번 작업에서는 설치하지 않았다. `tightlate-off`는 모드 폴더에서 Load 때 한 번 읽으며 모든 빌드에 적용된다. 파일 없으면 코드 기본 켬, 파일 있으면 끔이다. 설정창·Config 항목은 추가하지 않았다.
2. 첫 프로세스에서 `tightlate state`로 installed=true와 compatibility=ok를 확인한다. `tightlate bench`를 실행해 출력과 더미 정리·예외 유무를 남긴다. 0.5µs를 넘으면 결과와 loop/manager 시간을 바탕으로 원인을 줄인 뒤 재검증한다. 실행 전에 개선됐다고 간주하지 않는다.
3. 검증용 새 프로세스에서 기존 `late-verify`를 켜고 Arche 250~278초를 재생한다. `tightlate verify`, 구간 전후 `tightlate state`와 `animstat`를 수집한다. 처리 수>0, checked>0, mismatch=0, LateMismatch=0을 확인한다. 마스크/메시/숨김/lockScale/follow/비단위 부모가 각 제외 이유로 원래 경로를 타는지 확인한다.
4. 동일한 타일·곡 시점에서 `decosig`를 저장한다. off 파일이 있는 새 프로세스에서도 같은 조건으로 저장·대조한다. 곡 끝 대조도 수행한다. decosig는 기존 AutoTest 명령의 저장/비교 인수 형식을 따른다. 불일치가 있으면 원인을 확인하고, 해당 장식의 첫 로그·parallax·rotAngle/scaleVec를 보존한다.
5. 성능 A/B는 **매 판 새 프로세스**, 동일 DLL·화면·설정, verify off와 기존 late-verify off로 한다. Arche 250~278초를 off/on/on/off 등으로 반복하고 fuse begin/end, 상태 전후 차이, 프레임 시간·최악 프레임·끊김 수를 남긴다. 최초 캐시 생성과 깨어남이 몰리는 프레임도 따로 확인한다.
6. HELLO 전곡의 on/off decosig와 끊김을 확인하고, 편집기 재배치·재시작·맵 교체·UMM 해제/재활성화를 시험한다. 부모나 카메라를 다른 모드가 움직이는 환경에서는 verify를 켜 캐시 전제를 확인한다. 짧은 Arche 구간의 평균 개선만으로 전곡 끊김 제거를 결론 내리지 않는다.

결과 문서는 apply_patch로 UTF-8 BOM을 포함해 작성했다. 첫 세 바이트 EF BB BF와 엄격 UTF-8 디코딩, 한글 구절 보존을 검사했고 Get-Content -Encoding UTF8로 본문 한글을 확인했다. 첫 검사 스크립트는 PowerShell 파이프가 스크립트의 한글 비교 문자열을 물음표로 바꿔 assertion이 실패했다. 문서 본문은 정상으로 남아 있었고, 비교 문자열을 ASCII Unicode escape로 바꿔 다시 검사했다.
