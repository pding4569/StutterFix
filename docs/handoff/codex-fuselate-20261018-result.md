# FuseLate 구현 결과 (2026-10-18)

요청 범위인 코드 조사·구현·오프라인 빌드를 완료했다. 게임 실행, Mods 설치/복사, 커밋, 푸시는 하지 않았다. 실제 성능과 Unity 네이티브 결과의 비트 동일성은 아직 측정하지 않았다.

## 변경 파일·위치

| 파일:줄 | 변경 |
|---|---|
| `FuseLate.cs:10` | 새 모듈. `UpdatePosition` prefix, 조건 검사, 두 번의 Transform 쓰기, 부모/카메라 캐시, 64회당 1회 비트 비교, 통계, 정리 |
| `StutterFix.cs:78` | Dev/AutoTest `fuselate-off`, `fuselate-verify` 파일 처리 |
| `StutterFix.cs:218` | 메인 Harmony ID로 설치 |
| `StutterFix.cs:533` | 모드 해제의 애니메이션 마무리와 Harmony 해제 뒤 캐시·delegate 정리 |
| `StutterFix.cs:879` | 설정 적용. off 파일은 후속 ApplyConfig에도 유지 |
| `StutterFix.cs:1203` | 저장 설정 `FuseLate = true`. 설정창 줄은 추가하지 않음 |
| `StutterFix.csproj:185` | 명시 Compile 목록에 추가 |
| `MoveApply.cs:290` | 기존 VerifyLate 표본은 `FuseLate.RunOriginal`로 재실행하여 빠른 길끼리 비교하는 것을 방지 |
| `ExitFix.cs:133` | 새 맵 LoadLevel 진입에서 부모·카메라 캐시 해제 |
| `AutoTest.cs:1066` | `fusestate` 명령 |
| `FuseProbe.cs:107` | `decostruct` 출력에도 FuseLate 상태 포함 |

기존 `.codex/config.toml` 및 작업 시작 전 존재하던 미추적 파일은 수정하지 않았다.

## 빠른 길의 범위와 비용

- 정확히 `scrVisualDecoration`, followPlanet이 관리 참조 null, stickToFloor/lockRotation이 false일 때만 적용한다.
- parallax가 살아 있고 inited이며 cachedTransform과 pivotTrans가 같은 참조, isDecoration이고 decoration이 같은 장식인지 검사한다.
- clampToScreen, dontAlterX/Y는 모두 false여야 한다. `customLevel == null && !parallax.enabled`이면 원래 함수를 실행한다.
- 부모의 position=(0,0,0), rotation=(0,0,0,1), lossyScale=(1,1,1)을 성분별 정확한 `==`로 검사한다. Unity Vector/Quaternion의 근사 `==`는 쓰지 않았다. 부모가 없으면 단위 변환으로 취급한다.
- 조건을 통과하면 `SetPositionAndRotation` 1회와 `localScale` 쓰기 1회다. startPosition/posCamAtStart/endPosition/rotAngle/scaleVec도 원래 계산 순서의 식으로 저장한다.
- 카메라 position은 관리자 LateUpdate 한 번의 실행 안에서 프레임 번호+카메라 참조로 공유한다. 관리자 밖 UpdatePosition에서는 매번 읽어, Update에서 얻은 카메라 값을 LateUpdate까지 재사용하지 않는다. 여러 관리자가 같은 프레임에 돌면 각 실행에서 새로 읽는다.
- 부모 참조는 pivot Transform별 ConditionalWeakTable에 처음 한 번 저장하고, 부모 변환은 관리자 안에서는 부모별 프레임당 한 번 확인한다. 관리자 밖에서는 매번 변환을 확인한다.
- 두 번이라는 수는 Transform **쓰기** 횟수다. 조건 검사, 부모 첫 조회/프레임 확인, 첫 카메라 조회, `lockScale`일 때 camScaleMultiplier 내부 orthographicSize 조회 등은 별도다.

## 원본 IL과 계산 동일성의 근거

[원본 IL 발췌](codex-fuselate-20261018-il.txt)에 참조 DLL SHA256과 7개 메서드를 보존했다. [컴파일된 prefix IL](codex-fuselate-20261018-prefix-il.txt)도 첨부했다. 원본은 ILSpy IL 출력에서 해당 메서드 전체를 발췌했다.

1. `scrVisualDecoration`의 선언 메서드와 디컴파일 결과에 SetScale override가 없다. 따라서 베이스 `scrDecoration.SetScale` 식을 사용한다.
2. `UpdatePosition` IL_0037~0043은 `pivotPosVec + Vector2.zero`다. 값을 그대로 대입하도록 단순화하지 않았다. IL_0073 이후에는 Vector2→Vector3 변환으로 localPosition.z=0을 먼저 쓴다. 허용한 단위 부모에서 뒤이어 읽는 world position.z는 0이라는 전제를 사용한다.
3. `SetNewStartPosition`은 Vector3 인수를 Vector2로 변환하여 private startPosition에 저장할 뿐이다. 빠른 길도 이 Vector2→Vector3→Vector2 변환을 유지했다.
4. `SetTrans` IL_0097~00c8은 **Vector3 뺄셈 → Vector2 변환 → Vector2 성분별 곱 → Vector2 덧셈**이다. 카메라/시작 위치를 먼저 Vector2로 바꾸거나 곱셈을 분배하지 않았다. IL_0102~0136의 offset은 `parallaxOffset * camScaleMultiplier / textureScaleMultiplier` 순서다.
5. `SetScale` IL_002d~0049는 **Vector2 곱 → Vector3.one을 Vector2로 변환 → Vector2 곱 → WithZ(1)**이다. 빠른 길도 똑같은 연산자 서명을 사용한다. Quaternion은 원본과 같이 `Euler(0f, 0f, startRot + rotAngle)`이다.
6. 컴파일된 prefix IL에서 Vector3 뺄셈, Vector2 나눗셈, Vector2 곱셈 4개, WithZ, SetPositionAndRotation을 오프라인 assertion으로 확인했다. 이는 연산 순서 검토이며 게임 내 비트 일치나 성능 검증을 대신하지 않는다.

## 부모 변경·카메라 순서 조사

`tools/ilscan`으로 현재 `Assembly-CSharp.dll` 전체의 직접 호출을 검색했다. `Transform.SetParent` 55곳, `Transform.set_parent` 13곳이다. "ffxDamageRat만 있다"는 전제는 전체 DLL 기준으로는 맞지 않는다.

- `ffxDamageRat.StartEffect`: `ADOBase.lm.listFloors`의 scrFloor Transform을 target 아래로 옮긴다. 이미지 장식 자체의 부모를 바꾸지 않는다.
- `ffxTractorbeamFloors.AbductFloor`의 지연 콜백도 효과 오브젝트를 tractorbeamSource 아래로 옮긴다. 이미지 장식 순회와 별개다.
- `scrDecorationManager.CreateDecoration`: 이미지 장식은 Instantiate(prefab, manager.transform)로 만든다. Prefab 장식의 별도 pivot/child 구조는 정확한 타입 검사에서 제외된다.
- **편집기 `PropertyControl_DecorationsList.OnItemDropSides`는 실제 장식을 SetParent(null)했다가 다시 manager 아래로 붙인다.** 이 메서드 전후에 부모 캐시를 비우는 prefix/finalizer를 추가했다. 예외 경로도 캐시를 비운다. 해당 패치 역시 메인 Harmony ID로 해제된다.
- 나머지 호출 목록에는 타일 생성, UI 풀/메뉴, 모바일 화면, Rewired/TMP 예제 등이 있다. 정적 직접 호출 검색에서는 곡 중 이미지 장식을 다른 부모로 옮기는 경로를 찾지 못했다. 리플렉션, 네이티브 코드, 다른 모드의 동작까지 증명한 결과는 아니다.
- `scrDecorationManager.LateUpdate`는 사전 타일링/마스크 작업 뒤 `LogicUpdate`를 순회하고, 순회가 끝난 후 `CheckUpdateFrameRateScreen`을 호출한다. `LogicUpdate`의 UpdatePosition→UpdateShader→UpdateHitboxState 경로에서 카메라 Transform을 옮기는 직접 코드는 확인되지 않았다. 장식 타입별 UpdateShader 선언도 확인했다. 다만 visual의 필터 OnRenderImage 호출은 리플렉션 경로이므로 외부 필터/모드 전체의 부작용까지 보장하지 않는다.

## 검증 방식과 남은 위험

Dev는 자동으로, PlayerAuto는 `fuselate-verify` 파일이 있을 때 fast 누적 횟수 64회마다 한 번 비교한다. 순수 Player에는 표본 비교 본문이 컴파일되지 않는다.

빠른 길 직후 localPosition/rotation/localScale, parallax의 startPosition/posCamAtStart/endPosition, 장식의 rotAngle/scaleVec를 값 형식 스냅샷으로 저장한다. 그 뒤 bypass 플래그로 **이 prefix만 우회**하여 원래 UpdatePosition을 다시 실행하고 모든 float 성분을 `SingleToInt32Bits`로 비교한다. 첫 불일치는 이름과 전후 값(R 형식)을 기록한다. checked/mismatch는 `fusestate`에서 확인한다.

허용한 갈래의 원본은 이전 endPosition을 읽지 않고, startPosition/posCamAtStart를 먼저 덮어쓴다. dontAlterX/Y가 false라 이전 position.x/y도 최종 계산에 사용하지 않는다. 같은 입력에서 위치·회전·크기와 비교 대상 필드를 다시 쓰는 구조이므로 표본 재실행 뒤에도 원래 결과가 남는다. 기존 MoveApply의 다른 Harmony 처리와 다른 모드 패치는 그대로 통과한다. 네이티브 Transform dirty 상태, 다른 패치의 부작용까지 "없다"고 검증한 것은 아니다.

남은 위험:

- Unity 네이티브 `SetPositionAndRotation`과 개별 setter의 정규화/좌표 변환 결과가 비트까지 같은지는 미확인이다. 부모의 숨은 shear, +0/-0, 비정상 float 등도 게임 대조로 확인해야 한다. 관리 연산자 순서만 같다고 동일성이 확정되지 않는다.
- 다른 모드가 임의로 장식을 reparent하거나 관리자 루프 도중 카메라/부모를 이동하면 캐시가 낡을 수 있다. 알려진 편집기 재배치와 새 맵 진입은 무효화했지만 Unity Transform 전체를 패치하지는 않았다.
- 캐시는 장식 첫 진입에 ParentInfo/ConditionalWeakTable 기록을 할당하고, 처음 보는 부모/카메라에서 Dictionary가 늘 수 있다. 정상 반복 경로에는 새 스냅샷 객체·문자열·Stopwatch·리플렉션 조회가 없다. 실제 Mono에서의 할당 및 lookup 비용은 미측정이다.
- 샘플 대조는 1/64이며 모든 장식의 모든 프레임을 보증하지 않는다. 대조가 켜진 판의 시간은 원본 재실행 비용을 포함한다. 성능 A/B는 대조를 끈 PlayerAuto로 해야 한다.
- 빠른 길은 기본 true지만 게임 검증 전 코드다. 성능 개선 수치는 아직 없으며 출시 적합성도 판단하지 않았다.

## 오프라인 빌드

세 명령 모두 최종 코드로 성공했다. 출력 경로는 프로젝트 설정의 bin 아래이며 Mods에는 쓰지 않았다. 빌드 요약·출력 DLL 해시는 [빌드 근거](codex-fuselate-20261018-build.txt)에 기록했다. 경고는 버전 충돌(MSB3277)과 기존 미사용 필드 등이다.

| 명령 | 출력 | 오류 |
|---|---|---|
| `dotnet build -c Debug -p:Edition=Player -p:AutoTestBuild=1` | `bin/PlayerAuto/StutterFix.dll` | 0 |
| `dotnet build -c Debug -p:Edition=Player` | `bin/Player/StutterFix.dll` | 0 |
| `dotnet build -c Debug` | `bin/Debug/StutterFix.dll` | 0 |

`git diff --check`も確認。ゲーム 실행·설치용 자동 훅은 호출하지 않고 shell에서 소스 작성과 dotnet build만 수행했다.

## Claude가 게임에서 확인할 것

1. 검증용 PlayerAuto에 `fuselate-verify`와 기존 `late-verify`를 켜고 Arche 260~276초에서 `fusestate`, `animstat`, `decostruct`를 전후 수집한다. fast가 증가하고 checked>0, FuseLate mismatch=0, 기존 LateMismatch=0인지 확인한다. 첫 불일치가 있으면 해당 로그·장식 조건부터 조사한다.
2. 화면 고정 15개가 clamp 사유로 원래 경로를 타는지, follow/stick/lock/축 고정/텍스트·파티클·Prefab/부모 비단위 변환은 원래 경로로 가는지 확인한다. noSetTrans 갈래도 별도로 확인한다. fallback 순서는 `fusestate` 출력에 나온 순서이며 init에는 null camera도 포함된다.
3. 편집기에서 장식 순서 변경, 재생/재시도, 맵 교체, UMM 해제/재활성화에서 화면과 캐시 수명, 예외가 없는지 확인한다. 부모·카메라 이동이 있는 맵도 대조한다.
4. 대조를 끄고 같은 DLL·같은 화면 조건으로 `FuseLate=true/false` 또는 `fuselate-off` 파일 유무 A/B를 한다. 파일은 모드 Load 시 읽으므로 판마다 새 프로세스를 사용한다. flag 강제 off는 설정 재적용 후에도 유지된다. `fuse begin/end`와 fusestate의 전후 차이로 관리자 시간·fast/fallback 비율을 함께 기록한다.
5. Arche 해당 구간 외에 HELLO/Windflower와 실제 곡 전체에서도 최악 프레임·끊김 수를 확인한다. 단기 평균 시간 차이를 곡 전체 끊김 제거로 확대 해석하지 않는다.
