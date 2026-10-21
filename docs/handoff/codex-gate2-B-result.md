# Gate 2 변형 B 결과 — 2026-10-21

**2회차: 직전 자동 시험의 `Method has no body`를 일으키는 추상 메서드 패치 경로를 수정했다. 수정 후 3종 빌드·LoadCheck·회귀 검사는 통과했다. 실제 Harmony 설치·픽셀·FPS는 재시험하지 않았으므로 GATE2 통과는 미확인이다.** 게임 실행·설치·커밋·푸시 및 `.codex/config.toml` 변경은 하지 않았다.

## 2회차 오류 분석과 수정

장부와 [직전 자동 시험](gb-autoloop-run1-evidence.txt)을 먼저 확인했다. 최초 `livedeco on`이 `BadImageFormatException:Method has no body`로 끝났고 `final_on=False`였다. 세 시점 픽셀·ABBA FPS까지 진행하지 못했다. 따라서 이 로그는 아틀라스의 픽셀 실패나 이득 없음의 근거가 아니다.

디컴파일 `E:/StutterFixWork/decompiled/Assembly-CSharp/scrDecoration.cs:189,193`은 `SetVisible(bool)`·`SetDepth(int)`가 추상 선언임을 보여 준다. 기존 Runtime.Begin은 구조 setter를 이름으로 모으면서 추상 메서드도 포함해 총 **20개**를 Harmony에 넘기려 했다. `tools/ilscan`의 [메서드 목록](gate2-B-run2-patch-il.log), 실제 DLL의 reflection·PE 메타데이터를 사용하는 [새 회귀 검사](gate2-B-run2-patch-check.log)로 두 선언의 **Abstract=true, RVA=0**을 확인했다. `scrVisualDecoration.cs:327,431`의 재정의에는 실제 본문이 있다. 게임 DLL SHA256은 `F692210F9A342F7EED4BE40AAD463286AB40BF046599959823942EA02F4CA3FF`다. 전달 로그에 스택은 없지만, 기존 대상 목록에 본문 없는 메서드가 포함된 코드 결함은 확정했다.

- `LiveDecoPatchPlan.cs`에서 **18개 실제 구현 대상**과 **제외한 추상 선언 2개**를 구분한다. 추상 선언을 제외할 때 실제 visual 재정의가 존재하고 같은 virtual slot인지 검증하므로, SetDepth/SetVisible의 소유 해제 훅은 유지한다. SetSprite 두 오버로드와 나머지 구조 변경 훅도 유지했다.
- 이름·정확한 인자 타입·인스턴스 호출 규약·void 반환·유일성·관리 본문을 모두 확인한다. 필수 대상이 추상/본문 없음이면 이름을 포함해 실패한다. 다른 모드의 Harmony 소유자 검사도 유지한다. 전체 계획을 아틀라스 할당과 패치 적용 **전에** 검증한다.
- 설치가 다른 원인으로 실패하면 `patch-install <타입>.<메서드>:<예외>:<내용>`을 남겨 다음 실패 지점을 식별한다. 기존 StopRuntime/UnpatchAll/렌더러 복원 경로는 유지했다.
- 새 빌드 식별 로그는 `[livedeco] patch_plan version=2 targets=18 abstract_declarations_skipped=2`다. 일반 Player에서는 새 계획 타입도 제외한다. 기본 꺼짐·판정 조건·아틀라스/정점 계산·144-step 시험 입력은 유지했다. 게이트 3은 구현하지 않았다.

새 검사는 생산 코드와 같은 계획 생성기를 링크하고 설치 DLL을 읽는다. **38 checks**로 추상 선언/실제 PE RVA, 본문 존재, 중복 없음, 8개 상태 쓰기 훅, 구조 setter 보존, 두 SetSprite 오버로드, 필수 추상 대상 거절을 확인했다. 게임 메서드·Unity native·Harmony detour는 실행하지 않았다. 명령은 `dotnet run --project tools/optimization/LiveDecoPatchCheck -c Release -- "<게임 Managed>/Assembly-CSharp.dll"`이다.

이는 본문 없는 대상을 배제하는 오프라인 검증이며, 게임 Mono에서 Harmony 설치가 성공했음을 증명하지 않는다. 다음 외부 자동 시험은 **변경된 DLL**로 기존 144 steps를 사용하고 위 version=2 로그부터 확인해야 한다. 이후 owned/runs·vertexcheck·atlas 메모리·GC·픽셀·복원·ABBA FPS를 관측한다. 같은 코드의 실패 시험을 반복하지 않았다. 새 빌드 해시는 [산출물 목록](gate2-B-run2-artifacts.txt)에 기록한다.

공통 명세의 로드맵은 이 워크트리에 없어 `E:/StutterFixWork/StutterFix/docs/ROADMAP-EXTREME-20261021.md`를 읽었다. freeze 결과 문서는 이 워크트리와 canonical 문서 경로 모두에 없었다. 시작 시 이미 변경된 `codex-al-gb-2.md`와 중단 전에 남긴 run2 로그는 이어받았고, 사용자 인계 변경은 보존했다.

공통 명세가 게이트 1을 통과로 결정했으므로 기존 MPB 전체 이름 열거 문제를 다시 중단 조건으로 삼지 않았다. 알려진 커스텀 MPB는 제외하고 미지의 속성 문제는 실제 최종 화면 비교에 남겼다. 작업 중 공통 문서에 추가된 DecoAnim 채널 쓰기 차단 지시도 반영했다. 공통 문서의 추가 3줄은 외부에서 들어온 변경이며 이 작업에서 작성하거나 되돌리지 않았다.

## 구현

- `livedeco on|off|state`는 Dev/AutoTest에서만 제공한다. 설정·로드 시 자동 활성화하지 않는다. 기존 `census`도 유지했다.
- `LiveDecoRuntime.cs`가 원래 scene 렌더러의 정렬 키와 재질 슬롯을 모은다. 소유 장식이 연속인 구간에서 첫 멤버의 **원래 재질 인스턴스**, sortingLayer/order, z를 빌린 MeshRenderer로 그린다. 원본 재질을 변경하거나 복제하지 않고 메시의 MPB에만 아틀라스를 바인딩한다. 텍스처 자체는 구간을 나누지 않으며 실제 아틀라스 페이지·재질 서명·Unity layer·정렬 bucket이 다르면 분리한다. 비소유 장벽과 동률 불확실성을 유지한다.
- 원래 `Sprites/Tile` 셰이더와 재질을 그대로 사용한다. 새 셰이더·번들·키워드는 추가하지 않았다. 재질의 색·블렌드·출력 식은 원래 경로에 맡긴다. 텍스처 ST가 identity가 아니거나 RepeatX/Y가 1이 아니면 제외한다. 타일링·특수 블렌드·mask·split-alpha·비사각 sprite도 원래 경로에 남긴다.
- 정점은 게임의 논리 필드에서 직접 계산한다. `pivotPosVec`, `pivotOffsetVec`, `croppedPivotOffset`, PPU/피벗을 포함한 원래 sprite.vertices/uv/triangles, flip, child 위치·회전·크기, parallax, 화면 고정, followPlanet, 카메라 회전/배율 잠금을 처리한다. 타일에 붙은 장식과 시차 축의 이전 위치에 의존하는 장식은 제외한다. 소유 직전 실제 SpriteRenderer의 월드 정점 4개와 비교하며 최대 오차가 1e-4를 넘거나 비유한 값이면 제외한다.
- `scrDecoration.UpdatePosition` Harmony Prefix와 `TightLate.Process`의 직접 Transform 경로를 모두 차단한다. `DecoAnim`의 일반·완료·XY 합침 setter delegate도 명시적으로 감싸, 위치·피벗·회전·크기·색·불투명도·시차 offset의 논리 필드만 갱신한다. `MoveApply.DeferTransform/UpdateNow`, `ColorFast`의 직접 색 쓰기, `InvisibleSkip.After`의 우회도 처리했다. 논리 rendererColor를 계산하므로 다음 효과의 시작값과 GetVisible/GetAlpha 입력을 보존한다. 게이트 3 평가기·워커 분할은 만들지 않았다.
- 소유 중 원본 SpriteRenderer에는 `forceRenderingOff`를 적용한다. 해제할 때 현재 논리 색·child offset·UpdatePosition을 원래 렌더러에 반영한다. 장식의 구조 setter는 먼저 소유를 해제한다. `SceneReset`, `ExitFix.LoadLevelPrefix`, scene 변경, 기존 `Main.Unload`/AutoTest 정리가 Stop으로 이어진다. 이 수명 동작의 실제 게임 검증은 남아 있다.
- 모든 구간이 **하나의 재사용 인터리브 Vertex 배열**(Vector3 + Color32 + Vector2)을 공유한다. 각 mesh에 필요한 부분만 SetVertexBufferData로 올린다. 지정한 DontValidateIndices/DontRecalculateBounds/DontNotifyMeshUsers 플래그와 고정 bounds를 쓴다. 변하지 않은 구간의 메시·멤버 배열·재질은 재사용하며, 바뀐 구간만 생성/폐기한다.
- `cost`는 정점 계산·업로드·정렬/구간 갱신 시간과 new/reused runs, live/owned, atlas pages/textures/bytes, GC thread allocation delta를 기록한다. 전환 프레임과 120프레임마다 출력하며 프레임 카운터는 매 프레임 집계한다. 정상 프레임의 실제 0B 할당은 아직 입증하지 않았다. 편입·페이지 추가·구간 변경은 전환 할당으로 분리한다.

## 아틀라스 선택과 제한

`LiveDecoAtlas.cs`는 RGBA32 시험 경로다. 원본이 readable RGBA32이면 그대로 읽고, 나머지는 동일 크기의 GPU 출력→RGBA32 readback을 사용한다. RT active와 GL.sRGBWrite는 finally에서 복원한다. 이 변환의 sRGB·양자화 동일성은 게임 픽셀 비교 대상이다.

압축 내부 영역을 4px CopyTexture로 복사하는 것만으로는 임의 DXT 경계 블록의 바이트 보존과 실제 가장자리 색으로 만든 2px extrusion을 함께 보장하지 못한다. 현재 구현은 재압축을 넣지 않고 허용된 RGBA32로 대체한다. **DXT 바이트 그대로 복사하는 최적화는 구현하지 않았다.**

각 원본 텍스처 전체를 최소 2px의 실제 가장자리/모서리 색으로 확장한다. 배치 사각형은 4px 단위, 내용 원점은 짝수 픽셀이다. 원래 sprite UV를 double의 uMin/uMax로 변환하고 마지막에 float로 한 번만 내린다. 자동 리사이즈·텍스처 축소·재압축은 하지 않는다. filter/sRGB/aniso가 다른 소스는 별도 페이지로 나누고, mip chain 및 non-Clamp wrap은 제외 사유별 개수를 남긴다. 페이지는 최소 2048이고 필요하면 더 크며 GPU RGBA32 예산은 최대 512MiB이다. CPU readable 사본 메모리는 별도로 든다. 페이지가 여러 장이면 런 수도 증가한다.

| 경로 | 텍스처/UV 특성 | 남는 위험·비용 |
|---|---|---|
| 현재 RGBA32 아틀라스 | 크기 변경 없이 2px extrusion, UV 한 번 변환 | readback/sRGB, UV 보간, Color32 양자화; 실제 페이지 수/VRAM/런 수 미측정 |
| 같은 크기끼리 Texture2DArray 대안 | 원본 UV.xy를 유지하고 uv.z=slice, 호환 포맷이면 slice별 CopyTexture로 압축 바이트 유지 가능 | 크기·포맷·filter·mip·wrap별 배열 분리로 런 증가 가능; 배열 셰이더와 원래 재질 ID 앵커 보존 설계 필요 |

아틀라스에서 최대 채널 차이 1/255가 관측되면 `[livedeco2] PIXEL_NEAR`로 표시하고 PASS로 세지 않는다. 원인이 UV/샘플링이면 배열 대안을 비교한다. 배열은 Color32 색 양자화나 잘못된 정점을 해결하지 않으므로 위치별 차이·maxdiff와 함께 원인을 구분해야 한다. 자동으로 배열 경로로 전환하거나 동일 시험을 코드 변경 없이 반복하지 않는다.

## Transform 읽기 감사

읽기 전용 디컴파일을 현재 설치 게임 DLL의 IL과 교차 확인했다. [메서드·필드 IL](gate2-B-il.log), [전체 지정 필드/호출 읽기 목록](gate2-B-transform-readers.log). Assembly-CSharp SHA256은 `F692210F9A342F7EED4BE40AAD463286AB40BF046599959823942EA02F4CA3FF`이며 지정 읽기 목록은 52행이다. 도구는 PE 메타데이터를 읽었으며 게임 코드를 실행하지 않았다.

- scrDecoration.UpdatePosition은 localPosition→parallax start/posCamAtStart→SetTrans→SetRotation→SetScale 순서다. SetRotation은 startRot+rotAngle에 floor 또는 camera 회전을 더하고, SetScale은 scaleVec*camScaleMultiplier*floor scale이다. 지원 범위는 같은 연산 순서로 계산하고 floor 부착은 제외한다.
- scrParallax.SetTrans의 화면 고정/카메라 시차·parallaxOffset, private startPosition/endPosition을 확인했다. start/end 상태도 렌더 직전에 논리적으로 갱신한다. dontAlterX/Y의 이전 Transform 위치 의존은 제외한다.
- `GetDecorationWorldSize`는 pivot.localScale을 읽는다. `DecoTransformGizmoHolder`의 gizmo 경로와 drag는 편집기 경로다. 편집 상태·활성 선택/히트박스 테두리를 소유에서 제외한다.
- `SpriteAlphaMaskUtils`는 obj.transform과 SpriteAlphaMask.pivotTrans/childTrans를 읽는다. mask·다른 메시 출력·mask interaction·사용 중인 per-decoration filter는 제외한다.
- 히트박스/활성 collider 및 floor 부착·부모 계층은 제외한다. 소유하려는 child는 SpriteRenderer 자신의 Transform이며 pivot 바로 아래여야 한다. pivot 부모의 world matrix가 identity여야 한다.
- 이동 효과 StartEffect/클로저는 pivotPosVec/offset/rotAngle/scaleVec의 논리 필드를 읽는다. 이 값들은 계속 갱신한다. manager tiling/wrap 갱신은 소스를 변경하므로 sampler 변경을 재검사하고 소유를 해제한다.

이 감사는 해당 게임 코드의 지정 참조 목록과 디컴파일 검토다. 다른 모드의 reflection·Transform alias 사용 전체를 증명하지 않는다. 외부 Harmony hook은 편입 시 거절한다. 고정 scene snapshot 이후의 임의 외부 renderer 생성은 지원하지 않으며, manager 목록 크기 변경과 scene/reset은 정리 후 종료한다. 실제 live 집합의 켜짐·꺼짐·깊이·이미지 변경은 원래 목록 안에서 재분할한다. 원근/비스듬한 카메라/custom sort axis, 외부 SortingGroup, 불확실한 재질 동률도 ERROR로 복원한다.

## DecoAnim 설정 비용

직전 자동 시험이 전달한 표본은 **960,572개, set_pct=70.130186%**다. 채널별 apply_us/set_us는 위치 X 0.072866/0.562924, 위치 Y 0.073281/0.216343, 회전 0.073331/0.129199, 크기 X 0.142760/0.027674, 크기 Y 0.134887/0.030378, 색 0.073243/0.262872, 불투명도 0.079391/0.575806이다. 이는 표본 Step/Apply/OnUpdate 시간에서 ProfSet 비율이며, DecoAnim 전체 1.9ms의 70.13% 또는 순수 native 쓰기 비율이라는 뜻은 아니다. 특히 크기 setter는 ProfApply에 포함된다. 게임을 새로 실행하지 않고 전달 로그에서 인용한 수치다.

`livedeco profile begin|end`를 추가했다. 기존 ProfN/ProfApply/ProfSet을 차분하여 채널별 apply_us/set_us와 총 표본 시간의 set_pct를 출력한다. PlayerAuto에서도 이 별도 구간 동안만 샘플링을 켜고 원래 상태로 복원한다. 생성 steps는 다른 시험보다 먼저 livedeco가 꺼진 262→276초에서 이를 측정한다. ProfApply에는 ApplyE의 크기 setter가 포함되므로, 출력의 set_pct를 모든 native 쓰기 비중이나 DecoAnim 전체 비용 비중으로 해석하면 안 된다. 프로파일 창은 ABBA FPS 근거에 포함하지 않는다.

## 자동 시험 입력과 판정

- 생성기: [livedeco2-steps.py](../../tools/optimization/livedeco2-steps.py)
- 생성 완료 입력: [gate2-B-steps.json](gate2-B-steps.json), 기존 Arche chart 인자를 재사용한 **144 steps**, 실행하지 않음.
- 프로파일 창 다음에 262/270/276초 독립 seek→pause→A→on→B→off→A2를 수행한다. 최종 카메라 체인·원래 필터는 켠 채, UI 제외/중앙 20% 마스크를 사용한다. PNG와 outside 차이 수·위치·maxdiff, A2 복원 차이를 남긴다. 각 시점 후 캡처 버퍼를 비운다.
- 다음은 자연 재생의 off/on/on/off 4개 262→276초 창이다. 켬 준비는 251초부터 수행하여 측정 시작 전 여유를 준다. 각 창에 fuse begin/end를 넣고 실제 Stopwatch 경과 시간·Unity frame 차이로 FPS/ms를 기록한다. pause·캡처·setter 프로파일은 포함하지 않는다. 준비가 늦어 262초 허용 창을 넘으면 유효 측정으로 인정하지 않는다.
- 기록된 A/B/A2의 모드·정지 song time·복원 상태, 세 시점 완비, ABBA 순서·자연 재생·동일 runtime 생존·on 소유 수를 확인한다. 세 시점 모두 ab_out=0, 복원 성공, on 평균 FPS가 off 평균보다 15% 이상일 때만 GATE2=PASS다. 그 밖에는 PIXEL_FAIL/NO_GAIN/ERROR와 이유 코드를 출력한다. 마지막에는 반드시 off다.

## 오프라인 검증

| 검사 | 이번 결과 | 근거 |
|---|---|---|
| 실제 게임 DLL 패치 계획 | 38 checks, 본문 있는 18개/추상 제외 2개 | gate2-B-run2-patch-check.log |
| Release Dev / Player / PlayerAuto | 오류 0/0/0, 경고 38/36/38 | gate2-B-run2-build-dev/player/auto.log |
| LoadCheck Dev / Player / Auto | 274/229/274곳, 틀림 0/0/0 | gate2-B-run2-loadcheck-dev/player/auto.log |
| 기존 LiveDeco 정렬·앵커 정책 | 22,952 checks 통과 | gate2-B-run2-fixture.log |
| 신규 아틀라스·판정 정책 | 1,052,924 checks 통과 | 같은 로그 |
| LiveDeco edition 메타데이터 | 19 checks, 일반 Player 타입/참조 없음 | 같은 로그 |
| 기존 MatShare 정책 | 25,401 checks, 오류 0 | gate2-B-run2-matshare-fixture.log |
| 기존 FrameCompare fixture | RGBA 11 + mask 15 + state 14 + control 17 통과 | gate2-B-run2-framecmp-fixture.log |
| Gate1 / Gate2 steps | 25 / 144 steps 검증, UTF-8 CLI 통과 | gate2-B-run2-gate1-steps-check.log / gate2-B-run2-steps-check.log |
| 기존 진단 edition 검사 | 진단 타입 32/32 분리, 런타임 6/6 유지, 폐기 타입 5/5 양쪽 부재 | gate2-B-run2-edition-check.log |
| 최종 diff/인코딩 | git diff --check 및 UTF-8 문서 검사 통과 | 로컬 검사 |

진단 edition 검사는 처음에 `MaterialPoolFixture`의 Dev 존재를 요구해 실패했다. HEAD 소스 목록·현 csproj·타입 정의에서 재질 풀/크롭 연구 코드가 이미 제거된 것을 확인했다. 검사기의 폐기 타입 5개를 양쪽 빌드 모두 부재여야 하는 조건으로 수정했다. 살아 있는 진단 타입의 Player 제외 요구와 필수 런타임 타입 유지 요구는 유지했다. 실패를 숨기거나 없는 타입을 다시 추가하지 않았다.

LoadCheck의 동적 필드 이름 5곳은 기존처럼 실행 중 확인 대상이다. 빌드의 기존 어셈블리 충돌/폐기 API/미사용 경고는 남아 있다. 순수 fixture·정적 IL·메타데이터 검사는 Unity 정점/픽셀·실제 복원·VRAM·GC·FPS 증명이 아니다.

## 남은 실제 관측

실제 on의 live/owned/coverage, sampler 제외 분포, atlas 페이지 수/VRAM, 실제 런 수, vertexcheck, 전환 비용, stable_alloc_bytes, 세 시점 ab_out/maxdiff/aa2_out, ABBA FPS가 필요하다. ProfApply/ProfSet은 위 전달 표본만 확보했다. 전달된 센서스의 71~78구간을 이 구현의 실제 구간 수로 대체하지 않는다. 게임 실행 금지를 지키기 위해 새 게임 관측은 수행하지 않았고, 자동 루프에 변경된 빌드와 기존 입력을 인계한다.

## 작업 관찰 반환

task-observer v3.5.0을 읽고 기존 사용자 범위 관찰 헤더·활성 원칙을 확인했다. 저장소는 현재 쓰기 허용 범위 밖이므로 수정하지 않고 이 문서로 반환한다. 문제: 게임 메서드 Prefix만 추가하면 이미 바인딩된 setter delegate와 모드의 직접 쓰기 경로가 남을 수 있다. 개선: 논리 필드를 만드는 채널→모드 빠른 길→게임 setter→native 쓰기를 함께 감사하고 모두 같은 소유 검사로 통과시킨다. 원칙: 대체 렌더러의 원본 억제는 표시 플래그뿐 아니라 실제 상태 쓰기 경로 전체를 검증한다. 유형은 프로젝트 내부 구현/검증 관찰이다. 스킬·메모리는 수정하지 않았다.

2회차 관찰도 동일한 report-back 방식으로 반환한다. 문제: LoadCheck는 FieldRefAccess를 검증하고 기존 fixture는 순수 정책/edition을 검증하므로, 런타임에서 이름으로 수집한 추상 Harmony 대상은 둘 다 통과할 수 있다. 개선: 생산 코드의 패치 계획 생성기를 오프라인 검사와 공유하고, 실제 DLL의 본문·추상 여부·구체 override를 패치 전에 검사한다. 원칙: 런타임에 구성하는 훅 목록은 실제 구현의 존재까지 검증해야 한다. 유형은 프로젝트 내부 검증 관찰이며 별도 전역 관찰 파일·스킬·메모리는 수정하지 않았다.
