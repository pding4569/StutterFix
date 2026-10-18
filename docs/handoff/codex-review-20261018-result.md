# 2026-10-18 변경 동일성 검토 결과

검토 기준: `codex/optimization-36`, `83facdf..6e41c5660f88139e2f5c103c58f9c5f0fb15a238`.
검토일: 2026-10-18. 소스와 설치된 게임 DLL의 정적 분석 결과다. 아래 조건을 만족하는 실제 맵을 재생해 확인한 결과는 아니다.

## 결론

**전체 변경을 원본과 항상 동일하다고 승인할 수 없다.** 우선 수정할 것은 F1의 무지개색 전환 시 취소 순서 역전이다. F2의 첫 유효 이미지 크기 손실과 F4의 대기열 참조 잔존도 코드에서 확인된다. F3은 화면 밖에서 타일을 밟는 조건에서의 외부 색 소비자 누락이다. F5는 기존 TileCull 해제 누락으로, 이번 발광 렌더러 추가에도 그대로 영향을 준다.

FloorLateFix, FloorAnim.OpaOnly, MoveApply의 HashSet 정리는 조사한 정상 게임 경로에서 새 화면 차이의 근거를 찾지 못했다. StepPair의 정상 수치 계산도 동일하지만 예외 처리와 다른 Harmony 패치의 관찰 가능성까지 동일한 것은 아니다. 마스크 관련 F6은 현재 게임의 기본 설정에서는 활성화되지 않는 **조건부·추정 위험**으로 별도 분류했다.

게임 실행, sfmeasure 호출, Mods 설치, 커밋·푸시를 하지 않았다. 구현 코드는 수정하지 않았다. 실행한 빌드는 `dotnet build -c Debug -p:Edition=Player --nologo -v quiet`뿐이며 **성공, 오류 0개·경고 19개**, 출력은 `bin/Player`와 `obj/Player`다. 참조 버전 충돌 MSB3277 및 미사용·폐기 API 등의 경고가 있다. 빌드 성공은 동작 동일성의 증거가 아니다.

## 조사 범위와 근거 읽는 법

- `git log --oneline 83facdf..HEAD`와 일곱 대상 파일군의 변경을 확인했다.
- `Assembly-CSharp.dll`과 `Assembly-CSharp-firstpass.dll` 전체를 ILSpy CLI 9.1로 IL 출력하고, 메서드 끝 표식으로 호출자를 묶어 모든 해당 호출·필드 접근을 검색했다. 상세 조건은 대상 형식의 디컴파일 결과와 IL을 함께 읽었다. `IL_XXXX`는 **수정 전 DLL의 해당 메서드 내 오프셋**이다.
- Mods 아래의 모드·부트스트랩 DLL 19개도 전체 IL 검색했다. AdofaiGenBridge, JALib와 부트스트랩, Quartz의 현재·보관 버전 및 UpdateEngine, TUFHelperLite, YouTubeStream을 포함한다. StutterFix 자체, `lib` 아래 시스템 의존 DLL, 네이티브 DLL, macOS 메타 파일은 이 19개에서 제외했다. 보관 버전이 실제 게임에 로드됐다는 뜻은 아니다.
- 위 19개에서 `scrFloor.isTweening/lastPos/lastScale`, `scrVisualDecoration._sprite/spriteRenderer/spriteMask/spriteAlphaMask`, `scrDecoration.pivotTrans/pivotPosVec`, `FloorRenderer.color/cachedColor`의 직접 IL 접근은 발견하지 못했다. 리플렉션·문자열 기반 Harmony 패치·동적 로드 코드까지 없다고 증명한 것은 아니다.
- 게임 DLL SHA-256:
  - Assembly-CSharp: `6BA2CFB8D260962D3C8A3E4409F60E8E98293CA9B2D2ECA4875096BD02D40230`
  - Assembly-CSharp-firstpass: `7ADDEF3C50C8C45A10275967F46CBCE2EBA058FFE7E6051C8460F03338F64741`
- 재확인용 임시 자료: `%TEMP%\sf-codex-review-20261018\`. 전체 IL, 대상 형식 C#, `mod-manifest.txt`, `floor-callers.txt`, `all-consumers.txt` 및 메서드/필드 검색 스크립트가 있다. 임시 폴더이므로 이 문서에 핵심 근거를 독립적으로 남긴다.

## 발견 사항

### F1. [P1 / 화면이 달라짐] 무지개색으로 전환할 때 취소한 자리에 이전 색 트윈이 다시 생긴다

- 위치: `RecolorDefer.cs:121`, `RecolorDefer.cs:199`, `RecolorDefer.cs:248`.
- 관련 게임 IL:
  - `ffxRecolorFloorPlus.StartEffect`: `IL_0078` UpdateAngle(false), `IL_010D` SetTrackStyle, `IL_0124` tweenTypes.ForEach(기존 트윈 취소), **그 뒤** `IL_0226` ColorFloor.
  - `ffxRecolorFloorPlus.<>c__DisplayClass28_0.<StartEffect>b__0`: `IL_000E` TryGetValue, `IL_0017` Kill(true).
  - `scrFloor.ColorFloor`: Single은 `IL_0080` TweenColor, Stripes는 `IL_00D7` TweenColor. Rainbow는 원래 색의 알파를 읽어 즉시 색을 쓰며 이전 트윈을 별도로 취소하지 않는다.
- 발생 조건: 화면 밖 같은 타일에 길이가 남은 Single/Stripes 요청 A가 pending에 있고, drain·ScriptPrefix·Uncull보다 먼저 Rainbow 요청 B가 온다. 같은 프레임에 연속된 효과면 가능한 순서다. B의 UpdateAngle/SetTrackStyle은 여전히 미룰 수 있어 A를 적용하지 않는다.
- 차이:
  1. 원본은 B의 ForEach에서 A를 Kill(true)하고 Rainbow를 적용한다.
  2. 변경 코드는 ForEach 시점에 A가 아직 트윈 사전에 없다. 이어 ColorFloorPrefix가 Rainbow를 지원하지 않는다고 판단해 FlushIf를 호출한다.
  3. Apply가 A를 마지막 요청으로 재생하며 `Tween()`은 남은 길이가 있으면 살아 있는 트윈을 만든다. 그 뒤 원래 Rainbow가 실행된다.
  4. 새로 만든 A는 B의 취소 지점을 이미 지나쳤다. 이후 DOTween의 OnUpdate가 Rainbow 색을 다시 덮어쓸 수 있다. 화면 밖에서 시작해도 카메라가 트윈 종료 전에 돌아오면 보인다.
- 수정: B의 대상 타일별 **기존 트윈 취소 전에** pending을 처리해야 한다. 예를 들어 지원 불가 요청임을 효과 입구에서 판단해 그 대상의 pending을 먼저 적용하거나, 원본의 타일별 취소 지점에 flush를 넣는다. 단순히 ColorFloorPrefix에서 FlushIf만 부르는 현재 방식으로는 늦다. 새로 생성한 A에 B의 Kill(true)를 다시 적용하는 방법도 가능하나 Glow를 포함한 원본 취소 순서를 보존해야 한다.
- 확실성: 위 조건에서의 호출 순서와 살아 있는 트윈 재생성은 코드로 확인했다. 특정 맵에서의 발생 빈도는 미측정이다.

### F2. [P2 / 상태가 달라짐, 편집 조작에 영향 가능] 마지막 이미지만 남기면 첫 유효 이미지의 크기 캐시를 잃는다

- 위치: `LazySprite.cs:85`의 `pending[v] = cs`, `LazySprite.cs:97` 및 `LazySprite.cs:110`.
- 관련 게임 IL:
  - `scrVisualDecoration.SetSprite(CustomSprite,bool)`: `IL_005A` ResizeCollider.
  - `scrVisualDecoration.ResizeCollider`: `IL_0001` editorCollider, `IL_003B` collider 크기 쓰기, `IL_0041` spriteUnscaledSize 읽기, `IL_004B` zero 비교, `IL_0059` 최초 크기 저장.
  - 이후 `DecoTransformGizmoHolder.DragStart IL_007E`, `Drag IL_02F3`이 spriteUnscaledSize를 읽는다.
- 발생 조건: 처음에는 이미지가 없거나 로드 실패한 투명 이미지 장식으로, editorCollider는 존재하고 spriteUnscaledSize는 0이다. 보이기 전 유효 이미지 A, 이어 크기가 다른 유효 이미지 B로 변경되며 중간에 읽기/flush가 없다.
- 차이: 원본은 A에서 spriteUnscaledSize를 최초 설정하고 B에서는 유지한다. 지연 코드는 A를 버리고 B만 SetSprite하므로 B의 크기가 최초 크기로 남는다. 최종 sprite·collider만 비교하거나 초기 이미지가 정상인 장식만 비교하면 놓친다.
- 수정: spriteUnscaledSize가 0인 장식의 첫 유효 SetSprite는 즉시 수행하고 이후부터 미루는 방법이 가장 단순하다. 또는 원본 조건에 맞춰 최초 크기 부작용을 별도 기록·보존해야 한다.
- 확실성: 초기 조건을 만족할 때의 상태 차이는 확인된다. 편집기 드래그 결과가 얼마나 달라지는지는 미측정이다.

### F3. [P2 / 화면이 달라질 수 있음] ScriptPrefix 외에도 타일 색을 읽고 다른 상태에 남기는 함수가 있다

- 위치: `RecolorDefer.cs:59` Install의 세 함수 패치 목록, `TileCull.cs:464` ScriptPrefix.
- 누락 함수와 IL:
  - `scrPlanet.MoveToNextFloor IL_0BC6/IL_0DD2` → LightUp, `IL_0BD1/IL_0DD9` → SetToRandomColor.
  - `scrFloor.LightUp IL_006B` → SetToRandomColor.
  - `scrFloor.OnBeat IL_003D` → SetToRandomColor.
  - `scrFloor.SetToRandomColor IL_0050`은 floorRenderer.color 알파를 읽고, `IL_0382/IL_03C2`는 그 알파로 bottomGlow/topGlow 알파를 제한한다.
  - `scrFloor.TweenColor IL_001D`도 시작 색을 읽지만 RecolorDefer의 직접 패치 대상이 아니다.
- 발생 조건: pending 상태인 타일을 카메라 밖에서 밟는 등, flush보다 먼저 LightUp/SetToRandomColor가 호출된다. 커스텀 카메라가 현재 타일과 멀리 떨어질 수 있으므로 '밟을 타일은 언제나 uncull 상태'라는 보장은 없다.
- 구체적 차이: 알파 0인 즉시 Single 색 변경이 pending이고 엔진의 이전 색 알파가 1이면, Christmas 등의 발광 설정에서 SetToRandomColor가 이전 알파 1을 사용한다. 이후 지연 Single을 적용해 타일 색은 바로잡아도 이미 계산한 topGlow.color까지 복구하지는 않는다. ScriptPrefix는 Update/LateUpdate 진입만 보호하므로 직접 호출을 막지 못한다.
- 수정: SetToRandomColor와 외부 TweenColor 등의 실제 색 소비자 진입 전에 해당 타일을 flush한다. 내부 재생에는 bypass를 유지한다. OnBeat 또는 LightUp 전체를 무조건 계측할 필요는 없으며, 공통 소비 함수의 pending 확인으로 처리할 수 있다.
- 확실성: 누락 호출·알파 의존성은 확인했다. 해당 순서를 만드는 맵 재현은 하지 않았다. DivineIntervention 등 기본 스테이지 전용 TweenColor 호출은 커스텀 맵 TileCull 대상과 겹침을 확인하지 못했으므로 별도 확정 결함으로 세지 않았다.

### F4. [P2 / 상태만 다름·메모리 유지] FlushAll 후에도 order가 지난 맵의 타일을 붙잡는다

- 위치: `RecolorDefer.cs:92`, `RecolorDefer.cs:166`, `RecolorDefer.cs:177`, `RecolorDefer.cs:257`; `TileCull.cs:515`.
- 발생 조건: pending이 있는 상태에서 편집기로 전환하거나 StopAll/Shutdown한다.
- 차이: FlushAll은 pending을 비우지만 order/drainAt은 정리하지 않는다. order.Clear는 AfterStep/Drain 안에만 있다. StopAll 후에는 Step이 want=false에서 반환할 수 있어 AfterStep이 다시 돌기 전까지 order가 scrFloor 참조를 계속 보유한다. Shutdown도 같은 상태다.
- 게임 연결: `scnGame.ResetScene`/`scnEditor.SwitchToEditMode` 앞의 TileCull pause/stop 패치가 이 경로를 만든다. 이 항목은 잘못된 게임 IL 연산이 아니라 모드가 추가한 정적 컨테이너의 수명 문제이므로 게임 IL 오프셋은 해당 없음이다.
- 수정: FlushAll 완료 후 pending이 비었으면 order.Clear와 drainAt=0을 수행한다. Shutdown/장면 정리에서도 보장한다. 풀의 Pending에는 색 목록과 옵션을 이미 비우므로 핵심 잔존 참조는 order다.
- 범위: 관리 참조 잔존은 확인했다. 유지되는 실제 힙·VRAM 용량은 측정하지 않았다.

### F5. [P1 / 모드 끈 뒤에도 상태 변경 / 기존 결함] TileCull의 PlayerLoop·렌더러 복원이 UninstallAll에 연결되지 않는다

- 위치: `TileCull.cs:273` InstallLoop, `TileCull.cs:291` Step, `TileCull.cs:515` StopAll; `StutterFix.cs:449` UninstallAll.
- 발생 조건: TileCull이 Ready/active인 곡에서 모드를 끄거나 DLL을 다시 불러온다.
- 차이: UninstallAll은 RecolorDefer.Shutdown 및 InvisibleSkip.Uninstall을 부르지만 TileCull.StopAll/Shutdown을 부르지 않는다. TileCullStep을 PlayerLoop에서 제거하는 구현도 없다. Step은 Main의 installed를 검사하지 않아 active 상태로 계속 돌 수 있다. Harmony를 푼 뒤에는 렌더러 enabled의 가상 상태 처리만 사라지므로 원상 복구 계약도 깨진다.
- 오늘 변경과 관계: `83facdf`에도 있던 해제 누락이다. 오늘 새로 생긴 결함이라고 주장하지 않는다. 다만 오늘 추가한 **topGlow의 꺼진 상태와 새 참조 배열에도 동일하게 적용**된다.
- 수정: TileCull.Shutdown을 추가해 Enabled/Ready 또는 종료 표식을 내려 재진입을 막고, pending 적용·StopAll·소유한 TileCullStep 제거·VerifyPreCull 이벤트 해제를 수행한다. 백그라운드 Scan 결과가 해제 뒤 설치에 사용되지 않도록 세대/종료 검사도 필요하다. Main.UninstallAll에서 Harmony 해제 전에 호출한다.
- 게임 IL: 해당 없음. 모드가 설치한 PlayerLoop·Camera 이벤트의 소유권 문제다. FrameGen의 RemoveFrameStart는 자기 표식만 제거하며 TileCull을 대신 정리하지 않는다.

### F6. [P2 조건부 / 화면 차이 추정] V15를 활성화하면 마스크가 다른 장식보다 늦게 갱신될 수 있다

- 위치: `MoveApply.cs:245` DeferTransform와 `MoveApply.cs:254` SkipForLate; `DecoAnim.cs:298`, `DecoAnim.cs:313`; `LazySprite.cs:43`, `FastMove.cs:485`.
- 중요한 제한: 현재 DLL에서 disableV15Features에 쓰는 곳은 `scrController..ctor IL_00C9`뿐이고 true를 넣는다. 조사한 다른 모드 IL에서도 이 필드 쓰기는 찾지 못했다. 따라서 **아래 V15 렌더텍스처 경로를 현재 기본 실행의 확정 결함으로 세면 안 된다.** 외부 코드가 공개 필드를 false로 바꾸는 경우의 조건부 호환성 위험이다.
- 회전·크기 지연: DeferTransform은 정확히 scrVisualDecoration이고 hitbox가 없으며 GetVisible이면 허용한다. isMask 제외 조건이 없다. 마스크는 GetVisible에서 알파 0이어도 true가 될 수 있다(`scrVisualDecoration.GetVisible IL_0016`).
- 소비자: `scrDecorationManager.LateUpdate IL_005E`는 장식을 목록 순서대로 LogicUpdate한다. `scrDecoration.LogicUpdate IL_0009` UpdatePosition 다음 `IL_001C` UpdateShader를 실행한다. 마스크 대상 장식이 마스크보다 먼저면, 대상의 UpdateShader가 마스크의 이전 Transform을 읽는다.
- IL: `scrVisualDecoration.UpdateShader IL_02E9` → SpriteAlphaMaskUtils.ApplyAlphaMaskToRT. 그 함수 `IL_018E/IL_0197`은 다른 장식 마스크의 pivotTrans/childTrans, `IL_01BA`는 회전을 읽고, `SpriteAlphaMask.get_maskSize IL_002E`는 localScale을 읽는다.
- 이미지 지연에도 같은 읽기 구멍이 있다. 투명 일반 장식의 이미지를 pending으로 만든 뒤 **이미지 변경 없이 마스크 유형만 변경하는 다음 효과**는 FastMove.ImageBlock에서 SetMaskingType으로 바로 간다. 'others이면 이미지 변경을 미루지 않는다'는 조건은 이전 효과의 pending을 비우지 않는다. SetMaskingType/SetMaskingTarget은 LazySprite의 먼저 적용하는 목록에 없고, ApplyAlphaMaskToRT는 `SpriteAlphaMask.sprite`를 직접 읽는다(`IL_0267`, `IL_0358` 등). 대상 장식이 먼저 처리되면 이전 이미지로 렌더텍스처를 만들 수 있다.
- 수정: 회전·크기 지연은 마스크를 제외하거나 모든 마스크의 pending Transform을 대상의 UpdateShader 전에 적용한다. 이미지 pending은 SetMaskingType 등 역할 전환 전에 해당 장식을 flush하고, 외부 SpriteAlphaMask 소비 경로도 보호한다.
- 기본 옛 방식에서는 자신의 LogicUpdate→UpdateShader 진입 prefix가 이미지를 flush하고 SpriteMask의 실제 렌더는 이후에 이루어진다. 그 경로에서 위 순서만으로 한 프레임 차이가 난다고 단정하지 않는다.

### F7. [이론상 / 화면 차이 추정] 컬링 경계가 최신 메시·발광 경계를 포함한다는 보장은 없다

- 위치: `TileCull.cs:350` Register, `TileCull.cs:342` glowR 등록, `TileCull.cs:470` PinComp 예외.
- Register는 본체 r.bounds만 저장한다. 새 topGlow.bounds의 합집합을 쓰지 않는다. 또한 InMeshUpdate 중 culled 타일은 메시가 바뀌어도 PinComp가 반환해 기존 bounds/cells를 유지한다.
- 관련 게임: `scrFloor.SetTrackStyle`은 스타일에 따라 FloorMesh의 길이·폭·sprite 여부를 바꾼다. UpdateMesh가 최종 meshFilter.mesh를 바꾸고, 발광 스프라이트는 SetToRandomColor의 overrideGlowSprites 경로에서도 바뀐다.
- 조건: 새 메시 또는 glow의 화면 쪽 가장자리가 과거 본체 경계보다 커서, 과거 경계는 컬링 영역 밖인데 새 경계 일부는 실제 화면 안에 들어온다. 현재 25%+3의 여유와 8단위 셀은 완화책이며 모든 크기에 대한 증명은 아니다.
- 판단: 실제 게임 자산/맵에서 이 조건을 충족하는 크기·카메라 조합은 확인하지 않았다. 기존 culltest 3곳의 0픽셀 차이를 반박하는 측정도 아니다. **이론상으로만 기록한다.**
- 수정 방향: 본체·glow의 보수적 합집합으로 등록하고, deferred 스타일 변경 시 bounds/cells 갱신 또는 영향 있는 타일만 Pin한다. 매 프레임 전체 bounds를 다시 읽을 필요는 없다.

### F8. [P2 / 진단 상태·계측 비용 잔존] 새 matprobe Harmony ID가 해제되지 않는다

- 위치: `StutterFix.cs:93`, `StutterFix.cs:437`, `StutterFix.cs:525`; `ParticleDump.cs:14`~`24`.
- 발생 조건: DEV/AUTOTEST에서 `matprobe` 파일로 계측을 설치한 뒤 모드 끄기/다시 불러오기.
- 차이: `StutterFix.matprobe`로 Material(Shader) 생성자와 Shader.Find에 prefix/postfix를 설치하지만 HarmonyIds에는 그 ID가 없고 MatProbe.Shutdown도 없다. 전체 코드 검색에서 별도 해제 경로를 찾지 못했다. VerifyPatches 역시 HarmonyIds로 필터링하므로 이 옛 패치를 잡지 못한다.
- 영향: 원래 게임에는 없는 계측이 해제 뒤에도 남는다. Pre는 Hitch.Playing 검사 전 매 호출마다 Stopwatch.GetTimestamp를 실행한다. 실제 비용/끊김은 미측정이며, '이 패치가 실제 끊김을 만들었다'고 단정하지 않는다. 새 DLL과 옛 계측 코드가 함께 남을 수 있다는 해제 계약 위반은 확인된다.
- 게임 IL: 해당 없음. Unity 외부 호출 메서드에 모드가 건 Harmony 패치의 소유권 문제다.
- 수정: 정확한 ID를 HarmonyIds에 추가하거나 MatProbe.Shutdown에서 해당 ID만 UnpatchAll하고 UninstallAll에 연결한다. 같은 진단을 다시 켤 때 중복 설치 방지도 함께 둔다.

## 일곱 변경별 나머지 판단

| 대상 | 판단과 근거 |
|---|---|
| RecolorDefer + TileCull 연결 | F1/F3/F4 수정 필요. Uncull은 렌더러를 켜기 전에 flush하고 Pin도 Uncull을 거치므로 그 순서는 맞다. StopAll도 활성 상태에서 flush한다. ScriptPrefix는 Update/LateUpdate에 대한 보호는 되지만 모든 소비자의 보호는 아니다. |
| topGlow 컬링·음수 인덱스 | SetEnabled/GetEnabled/StopAll은 음수 인덱스를 `-i-1`로 되돌리고, 검증의 본체 배열 접근에도 음수 제외가 있다. 조사한 변환에서 음수 배열 접근 오류는 찾지 못했다. Cull 당시 꺼져 있던 glow는 사전에 넣지 않으므로 나중에 켜지면 컬링 최적화를 빠져나갈 수 있으나, 이를 화면 불일치로 세지는 않았다. F5/F7은 남는다. |
| FloorLateFix | 아래 상세 근거상 정상 경로의 화면 차이를 찾지 못했다. 내부 lastPos/lastScale/isTweening 값 자체는 일부러 원본과 달라진다. |
| FloorAnim OpaOnly | 길이 0/길이 있음 모두 기존 opacity 갈래와 같은 비교·Kill·등록 순서다. 아래 상세 근거상 새 결과 차이를 찾지 못했다. |
| LazySprite + FastMove + InvisibleSkip | F2 확정 조건 및 F6 조건부 누락. 기존 정상 이미지의 최종 상태 비교는 첫 유효 이미지 초기화나 외부 마스크 소비 순서를 증명하지 않는다. |
| DecoAnim DeferTransform / StepPair | 일반 이미지의 최종 LateUpdate 값은 게임 UpdatePosition→SetRotation/SetScale로 재구성된다. 마스크 외부 소비는 F6. StepPair의 정상 산술은 동일하며 아래 예외 차이는 조건부다. |
| MoveApply HashSet 정리 | 새 화면·상태 차이의 근거 없음. 작은 dirty는 원소별 Remove, 남은 것이 있으면 Clear하는 안전망까지 있어 함수 종료 시 집합이 빈다는 불변식은 유지된다. |

### FloorLateFix 상세

- `scrFloor.LateUpdate`의 후보 네 곳은 `IL_005C`, `IL_0079`, `IL_035A`, `IL_0370`이다. 본체·다중 행성 선을 움직이는 `IL_0349`의 position 읽기는 대상이 아니다.
- 두 게임 어셈블리 및 조사한 모드 DLL 전체에서 lastPos/lastScale/isTweening의 직접 외부 소비는 없었다. 게임의 유일한 isTweening 읽기 `IL_0331`은 multiplanetLine != null 갈래 안이다.
- 처음 LineRenderer가 붙은 프레임에는 이전 lastPos/lastScale이 0이라 isTweening이 원본보다 true가 되기 쉽다. 추가로 선 위치를 현재 타일 위치에 쓰는 결과이며, 이후 프레임에는 실제 값을 저장한다. 위치가 모두 0인 특수 경우를 포함해 외부에 잘못된 값을 누적하는 경로는 찾지 못했다.
- `ReferenceEquals`는 파괴된 Unity 객체를 null로 간주하지 않는다. 파괴됐지만 CLR 참조가 남은 선은 빠른 길 대신 원래 Transform 읽기를 수행하므로 안전한 쪽으로 빠진다. 원본의 Unity null 검사는 다중 행성 갈래에 그대로 있다.
- 원본 Transform이 이미 파괴된 비정상 상태에서는 예외 발생 여부가 달라질 수 있다. 정상 타일 생명주기의 확정 화면 결함으로 세지 않았다.

### FloorAnim.OpaOnly 상세

- 활성화 조건은 opacity 사용 + 위치 미사용/양축 NaN + 회전 미사용 + 크기 미사용/양축 NaN이다(`FloorAnim.cs:168`~`171`).
- 공통 경로와 마찬가지로 material 읽기 → 이전 opacity 트윈 완료 → Mathf.Approximately 비교 → 길이 0 직접 쓰기 또는 OPA Rec 생성 → dontChangeMySprite 순서다. mt.Count==0이면 Prev를 생략해도 같은 null 결과다.
- OPA에만 전달하는 null Transform은 Startup(`FloorAnim.cs:621`), Apply(`FloorAnim.cs:635`), 개발자 shadow에서 사용하지 않는다. Record/Proxy 재사용은 같은 floor·key만 허용하므로 다른 키의 Transform 필요 기록으로 바뀌는 경로도 없다.
- 제거한 startPos/startRot 계산과 불필요한 Transform getter는 정상 객체에서 결과 부작용이 없다. 이미 파괴된 객체, 또는 다른 모드가 Prev의 완료 콜백 안에서 타일을 파괴하는 경우의 예외 시점까지 같다고 보장하지는 않는다.

### StepPair 상세

- `scrDecoration.SetPositionX/Y`는 pivotPosVec의 한 축을 교체해 SetPosition에 넘긴다. SetPosition은 parallax가 null이면 반환하며, 그 외에는 최종 인자에 따라 pivotPosVec·childTransform·pivotOffsetVec를 쓰고 마무리 작업을 한다. X 값 계산과 Y 값 계산 사이에 다른 Rec를 실행하지 않는다.
- `DecoAnim.Startup`의 위치 키는 저장해 둔 시작값에서 FChange만 계산한다. Y Startup을 X 적용보다 앞으로 옮겨도 Y가 Transform이나 X 중간값을 읽지 않는다. 두 Apply도 위치 키에서는 FLast 계산뿐이다.
- 완료 프레임에 FLast 대신 FEnd를 택하므로 원래 OnUpdate 후 OnComplete가 넣는 최종 축값과 같다. X/Y의 길이·진행도가 다르더라도 각각 계산하므로, 짝 생성 효과가 같아야만 성립하는 산술은 아니다. 재생 중인 이웃 X/Y와 동일 장식인지 검사한다.
- 단, 새 catch(`DecoAnim.cs:477`)는 한 축 계산/SetPosition에서 예외가 나면 **두 축을 함께 종료**한다. 원래는 해당 Rec만 종료하고 다음 Rec를 시도했다. 이는 **이론상 예외 처리 차이**다. 예를 들어 다른 Harmony 패치가 X setter에만 예외를 내거나 자체 부작용을 붙이면 합치기로 호출 관찰 자체가 달라진다. 조사한 모드 DLL에서 그 특정 패치를 확인하지 못했고, 정상 게임 코드의 유효 객체에서 한 축만 실패하는 구체적 사례도 찾지 못했다.
- 호환성까지 엄격히 지키려면 SetPositionX/Y/SetPosition에 외부 패치가 있는 경우 pair를 끄거나, 예외 경로에서 기존 단일 Step 방식의 복구 규칙을 명시해야 한다. 기본 게임의 정상 산술 때문에 PairXY를 전부 제거할 근거는 찾지 못했다.
- `!Prof` 조건 때문에 개발자/Measure 빌드에서는 이 경로가 꺼진다. 이번 소스의 Main.MeasureBuild는 false이고 AUTOTEST만으로 Prof가 true가 되지는 않는다. 후속 검증에서 PairSteps 증가 여부를 확인해야 한다.

## 초기화·종료·스레드 검토

- 재시작/편집 복귀: SceneReset.Prefix의 InvisibleSkip.ApplyAllLazy가 LazySprite.FlushAll을 먼저 실행한다. TileCull은 ResetScene/MakeLevel에 PauseHook, SwitchToEditMode에 StopHook이 있다. 게임이 값을 되돌린 뒤 pending 스프라이트를 다시 쓰는 일반 경로는 찾지 못했다. Recolor의 order 잔존은 F4다.
- 곡 끝: Hitch의 종료 처리에서 InvisibleSkip.ApplyAllLazy를 부르므로 스프라이트 pending이 정리된다. TileCull은 곡 끝에도 카메라를 따라 계속 동작하도록 의도되어 있어 Recolor는 drain/uncull로 처리된다. '곡 끝 즉시 pending==0' 계약은 아니다.
- 모드 끄기: LazySprite는 InvisibleSkip.Uninstall→RestoreAll에서 비운다. RecolorDefer.Shutdown은 pending을 적용한다. TileCull 해제는 F5가 남는다. MoveApply의 lateSkipped는 ManagerLatePostfix에서만 비우므로, manager가 비활성화/파괴된 Update~LateUpdate 사이에 모드가 내려가는 경우의 미적용 Transform·참조 잔존도 **추정 위험**이다. 새 DeferTransform이 이 기존 구조를 추가로 이용한다. 독립 Flush/Clear를 종료·씬 전환에 연결하는 것이 안전하다.
- Unity 파괴 객체: LazySprite.Flush/FlushAll 및 Recolor Apply는 Unity null 검사를 하여 파괴된 객체 적용을 피한다. 두 구현 모두 pending을 먼저 제거하고 적용 예외를 기록·삼키므로, 예외가 나면 미적용 상태를 재시도하지 않는다. 정상 자산에서 확인된 새 예외는 없으며, 파괴/외부 패치 예외의 원본 동등성은 보장되지 않는다.
- 스레드: pending/order/recs/dirty/HashSet에는 잠금이 없다. 조사한 경로는 효과 실행·DOTween Update·게임 Update/LateUpdate·PlayerLoop 등 메인 스레드에서 호출된다. TileCull의 백그라운드 Scan은 메서드 목록만 found로 전달하며 pending 컨테이너를 직접 건드리지 않는다. 현재 확인한 정상 호출에서 새 데이터 경쟁의 증거는 없다. 외부 모드의 작업 스레드에서 Unity 함수를 호출하는 경우는 이 판단 범위 밖이다.

## 같은 커밋 범위의 나머지 변경

일곱 중점 항목 외의 런타임 변경도 diff와 구현을 읽었다. 다음은 별도 게임 검증 없이 내릴 수 있는 판단의 한계다.

| 변경 | 동일성·복원 판단 |
|---|---|
| AssetDiskCache / PersistentBlobCache / ImagePrefetch | UnpackDxt의 새 offset은 헤더 뒤 payload 위치이며, ImagePrefetch는 `cachedData.Length-cachedOffset`만 할당·복사·BlockSize·pendingBytes에 사용한다(`ImagePrefetch.cs:724`~`736`). 헤더가 GPU 블록에 섞이는 길이/오프셋 오류는 찾지 못했다. 캐시 Magic이 v1→v2로 바뀌어 옛 SHA-256 헤더를 새 검사합으로 해석하지 않는다. 읽기 실패는 원래 디코딩으로 돌아간다. 정상 payload의 결과 바이트를 바꾸는 경로는 찾지 못했다. 새 64비트 검사합은 SHA-256과 같은 충돌 내성이 아니므로 모든 가능한 손상에 대한 수학적 동등성은 주장하지 않는다. known 용량은 외부 프로세스가 파일을 추가하면 틀릴 수 있으나 이미지 내용 동일성과 별개인 quota 관리 위험이다. 원본 파일 식별이 길이·mtime에 의존하는 기존 한계는 이번 변경의 새 결함으로 세지 않았다. |
| 캐시 위치·기본값 | 기본 경로 LocalAppData, 상한 3072MB, 기본 켬으로 바뀐다. 저장소·I/O 상태는 의도적으로 달라진다. 원래 파일 자체를 수정하는 경로는 없고, 빈 공간 확인 실패는 예외를 삼킨 뒤 캐시를 시도한다. 초기 quota+4GB 여유 검사만 있으므로 실행 중 다른 프로그램이 디스크를 채우는 것까지 막는 보장은 아니다. |
| PhysicsPause | 기본 끔이므로 기본 게임 경로는 유지된다. 켜면 물리 simulationMode를 Script(2)로 바꾸는 **의도적인 비동일 옵션**이다. Rigidbody가 없어도 trigger/collision callback에 의존하는 다른 모드는 달라질 수 있다. 실제 게임 `scrDecoration.CheckHitboxHit`는 `Collider2D.Overlap` 질의이고 AutoSync2D 조건을 검사한다. saved/changed별 복원, 이미 Script면 미소유, 다른 코드가 다른 모드로 바꿨으면 덮지 않는 방어가 있다(`PhysicsPause.cs:26`~`63`). 자동 종료 후 복원은 Hitch·Shutdown에 연결된다. 재생 도중 옵션을 켜면 다음 SongStarted 전에는 활성화되지 않는 동작도 있다. 기본 끔을 근거로 모든 옵션 ON의 게임 동일성을 보장하면 안 된다. |
| 0.5ms 타이머 요청 | 기본 끔. 성공 시에만 ntOwned를 바꾸고 동일 5000 단위 요청을 해제하며 Shutdown에서도 요청을 내린다(`LowEnd.cs:21`~`30`, `LowEnd.cs:326`). 게임 값의 결과를 직접 바꾸는 경로는 찾지 못했다. API 실패 시 소유 상태 유지·후속 재시도는 있지만, Shutdown의 마지막 해제 실패 후 모드가 완전히 내려가면 재시도를 보장할 호출은 없다. 이는 조건부 외부 자원 해제 한계이며 실패를 실행해 재현하지 않았다. |
| 메인·렌더 스레드 우선순위 기본 켬 | 실제 스케줄링 구현 자체의 변경은 없고 Config 기본값과 안내가 바뀐다. 새 설정에 대해서만 기본값이며 기존 저장된 false/0이 자동으로 true/1로 마이그레이션된다고 볼 수 없다. ThreadScheduling.Shutdown/RenderThreadScheduling.Shutdown은 연결돼 있고 소유한 현재 상태를 확인한 뒤 복원한다. 스레드 실행 순서·오디오/다른 프로세스와의 경쟁은 의도적으로 달라지므로 화면/입력 타이밍까지 원본과 엄밀히 같다는 주장은 하지 않는다. 이번 정적 검토로 성능 수치를 다시 검증하지 않았다. |
| 제거한 재질 공유·알파 여백 크롭 | DecorationMaterialPool의 설치/설정/빌드 항목과 개발용 fixture/probe를 제거했다. 네이티브 소스 변경은 alpha_bounds.h include 및 해당 헤더·export 제거다. 정상 이미지 축소/필터/압축 알고리즘을 바꾸는 소스 diff는 없다. sfnative.dll 바이너리 재빌드의 모든 함수 동등성을 별도 디스어셈블 비교한 것은 아니다. 이번 변경에 14,710장 PNG 대조를 수행했다고 주장하지 않는다. |
| 진단·자동 시험 추가 | TileSig/ParticleDump, Dormancy.Stat, AutoTest 명령 및 기존 probe 대상 확장·문서·ILScan의 `*` 지원은 일반 플레이어 경로의 게임 값 변경으로 이어지는 새 경로를 찾지 못했다. `matprobe` 해제는 F8이다. RecolorSplit probe는 파일로 켠 DEV/AUTOTEST에서만 설치되며, 예외 시 postfix가 빠져 probeDepth가 틀어질 수 있으나 해당 값은 로그용이다. 많은 함수를 감싸는 probe를 켠 판을 무계측 성능으로 취급하면 안 된다. |
| PerfOverlay 통계 | AUTOTEST에만 0.1ms 구간 히스토그램을 추가한다. `1% low`는 느린 1% 프레임의 평균 FPS가 아니라 **99백분위 프레임 시간의 역수**를 구간 중앙값으로 근사한다(`PerfOverlay.cs:594`). 0.1%도 같은 방식이다. `12ms 초과`는 실제로 12.0ms 이상 bin을 포함하고 199.9ms 초과는 마지막 bin으로 합친다. 게임 결과 변경은 아니지만 측정값을 다른 정의의 low와 비교할 때 이 차이를 명시해야 한다. |

## 후속 검증 우선순위 (이 검토에서는 실행하지 않음)

1. 같은 culled 타일에 Single/Stripes(길이 있음) → Rainbow를 같은 프레임에 실행. RecolorDefer ON/OFF로 남은 Color tween과 도중 색을 비교한다(F1).
2. 초기 이미지 없음 + 투명 장식에 크기가 다른 A→B를 연속 지정. 최종 sprite뿐 아니라 spriteUnscaledSize 및 편집기 크기 조작을 비교한다(F2).
3. 카메라를 현재 타일에서 멀리 두고 즉시 알파 색 변경 후 타일 착지. 타일 색과 topGlow/bottomGlow를 함께 비교한다(F3).
4. pending이 남아 있을 때 편집 복귀/모드 끄기 후 pending뿐 아니라 order·PlayerLoop·꺼진 렌더러·보유 참조를 검사한다(F4/F5).
5. V15를 별도로 활성화하는 지원 범위가 있다면, 목록상 대상 장식→마스크 순으로 배치해 마스크 회전·크기와 일반 장식→마스크 전환 직전의 이미지 변경을 프레임 단위로 비교한다(F6).

곡 끝 상태 해시만으로 F1/F3/F6의 도중 순서 차이를 판정하지 말아야 한다. 수정은 이 문서에 제안만 했으며 적용하지 않았다.

