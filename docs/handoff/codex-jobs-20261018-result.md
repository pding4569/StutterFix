# 장식 Job 그림자 비교 도구 결과 — 2026-10-18

1단계 코드와 오프라인 검증을 완료했다. 제품의 장식 갱신은 그대로 실행하며, 새 코드는 결과를 따로 계산해 읽기 비교만 한다. **게임 실행 0회, Mods 설치 0회, 커밋 0회.** 실제 비트 동일성·비용·병렬 실행 여부는 게임 실행 금지 때문에 미측정이다. 아래의 설계 수식은 추정이며 실측 개선값이 아니다.

## 변경 범위

- `DecoJobShadow.cs`: DEV/AUTOTEST 전용. `decojob shadow begin|end`, LateUpdate postfix, 순차/잡/실제 값 비교, 비용 보고, 자원 정리.
- `AutoTest.cs`: 명령 연결 1줄.
- `StutterFix.csproj`: FuseProbe/CpuBreak/OffscreenProbe와 동일한 Compile 조건 1줄.
- `StutterFix.cs`: DEV/AUTOTEST의 UninstallAll에 Stop 연결 1줄. 모드 비활성화·Unload 경로에서 함께 해제된다.
- 설정·UI·MoveApply·Dormancy·DecoAnim·원래 게임 함수는 변경하지 않았다. 기존 작업 파일은 유지했다.

## A. 설치된 Unity API와 제약

현재 Player.log의 엔진 표기는 `6000.3.21f1 (c02631ffc030)`다. DLL 해시는 [dll-hashes](codex-jobs-20261018-dll-hashes.txt)에 보존했다. Assembly-CSharp SHA256은 이전 FuseLate IL 근거와 같은 `6BA2CFB8D260962D3C8A3E4409F60E8E98293CA9B2D2ECA4875096BD02D40230`이다.

| 조사 항목 | 로컬 DLL에서 확인한 결과 |
|---|---|
| TransformAccessArray 생성 | `Transform[]`, `int capacity`, `NativeArray<TransformHandle>` 생성자. `desiredJobCount=-1` 기본값. capacity 생성 후 Add, SetTransforms, RemoveAtSwapBack, Dispose 가능 |
| 배열 유지 비용 | 생성/추가/제거/전체 교체는 네이티브 바인딩이다. 관리 IL만으로 시간이나 네이티브 복잡도를 확정할 수 없다. 실제 비용은 도구가 보고 |
| desiredJobCount | 네이티브 Create로 넘기는 작업 분할 요청값. 읽기 전용/쓰기 정책을 선택하는 값이 아니다. 자동값 -1만 사용하며 작업 스레드 수를 변경하지 않음 |
| TransformAccess | position/rotation/localPosition/localRotation/localScale 모두 읽기·쓰기 API 존재. localToWorldMatrix/worldToLocalMatrix는 읽기. SetPositionAndRotation/SetLocalPositionAndRotation도 존재 |
| 스레드 안전 | TransformAccess의 해당 네이티브 접근자에 IsThreadSafe=true. 일반 Transform을 작업 스레드에서 직접 쓰는 것과 구별해야 함 |
| 스케줄 | Schedule(TAA, dependency), ScheduleReadOnly(TAA, batchSize, dependency), 각각 ByRef 버전 존재. 읽기 전용 잡에서 Transform 쓰기는 계약 위반 |
| 비-Burst 실행 | IJobParallelForExtensions는 T의 reflection data와 관리 Execute delegate를 JobsUtility.CreateJobReflectionData에 등록한 뒤 네이티브 ScheduleParallelFor를 호출. Burst 없이 호출 가능한 코드 경로가 존재 |
| 모드 참조 | 사용한 NativeArray/ReadOnly/WriteOnly/Unsafe 속성/IJobParallelFor/JobHandle/JobsUtility/TransformAccessArray가 기존 `UnityEngine.CoreModule.dll` 참조만으로 netstandard2.1 세 빌드에서 컴파일됨. 추가 Reference 및 배포 DLL 불필요 |
| Unity.Collections/Burst | Managed 폴더에 각각 DLL이 실제 존재. 이번 도구는 별도 패키지 기능을 사용하지 않으며 BurstCompile도 없음 |
| 스레드 수 | JobWorkerCount 읽기 API 확인. 6코어라고 작업 스레드 5개 또는 6개라고 가정하지 않음. begin/end에서 실제 설정값과 실행된 ThreadIndex 및 mainIndex 보고 |

[API 추출](codex-jobs-20261018-api.txt), [JobsUtility](codex-jobs-20261018-jobsutility.txt), [NativeArray](codex-jobs-20261018-nativearray.txt)에 설치된 DLL의 디컴파일 근거를 남겼다.

잡에는 관리 객체 참조를 넣지 않았다. Input/Output은 Vector2/Vector3/Quaternion/float 값만, 잡 본체는 NativeArray 핸들·Vector3·int만 포함한다. Transform·장식 객체·List·delegate·문자열을 잡에 전달하지 않는다. 각 Execute는 64개 묶음의 서로 겹치지 않는 출력 구간만 쓴다. 메인에서 n과 배열 capacity를 직접 보장하며, 완료 전 입력/출력 읽기·재할당·Dispose를 하지 않는다.

릴리스의 안전 검사는 **실제 DLL 본문에서 생략된 것**을 확인했다. NativeArray 인덱서는 UnsafeUtility.ReadArrayElement/WriteArrayElement를 직접 호출하며, Allocate에서 CheckAllocateArguments/IsUnmanagedAndThrow를 부르지 않는다. TransformAccess.CheckWriteAccess와 MarkReadOnly 본문도 비어 있다. Conditional 속성의 이름만 보고 판단한 것이 아니다. 따라서 안전 검사가 잘못된 인덱스·읽기 전용 쓰기를 막아 줄 것이라고 기대해서는 안 된다. Unity 네이티브의 모든 검사가 꺼졌다는 의미는 아니다.

`Quaternion.Euler(0f,0f,z)`는 기존과 같은 함수를 쓴다. 설치 DLL에서 Internal_FromEulerRad의 네이티브 EulerToQuaternion 바인딩은 IsThreadSafe=true다. [Quaternion 근거](codex-jobs-20261018-quaternion.txt), [Vector2](codex-jobs-20261018-vector2.txt), [Vector3](codex-jobs-20261018-vector3.txt)에서 벡터 연산자는 성분 산술과 값 생성으로 확인했다. Unity.Mathematics는 사용하지 않았다. EulerXYZ와의 비트 동일성은 미검증이며 대체할 근거가 없다. 같은 함수도 Mono 메인/작업 스레드의 부동소수점 처리 및 Transform 네이티브 변환까지 동일하다는 보증은 없으므로 실제 값을 별도로 비교한다.

Burst는 Editor에서 JIT, Player에서는 빌드 시 AOT로 처리된다. 이미 빌드된 게임에 새로 넣는 모드 DLL은 게임의 기존 Burst AOT 대상에 포함되어 있지 않다. **추정:** DLL이 있다는 이유만으로 새 모드 잡이 Burst 컴파일되지는 않는다. 이번 경로는 명시적으로 비-Burst이며 Burst 성능을 전제로 하지 않는다. [Unity Burst 설명](https://docs.unity3d.com/jp/current/Manual/script-compilation-burst.html)

**2단계의 큰 제약:** 쓰기 가능한 IJobParallelForTransform은 같은 Transform 계층을 단일 작업 스레드에서 처리한다. 관리자의 공통 부모 아래 장식들을 넣으면 다중 코어 쓰기 이득이 제한될 가능성이 있다. ScheduleReadOnly는 계층을 넘어 나눌 수 있어도 Transform 쓰기를 해서는 안 된다. desiredJobCount를 늘려 이 제약을 없앨 수 있다고 판단하지 않았다. 공식 설명은 다른 6.x 버전이므로 설치 DLL의 실제 스케줄 성능은 별도 확인 대상이다. [Unity 공식 설명](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/jobs/ijobparallelfortransform), [Unity 원본 스케줄 코드](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Jobs/Managed/IJobParallelForTransform.cs/)

## B. 그림자 비교의 범위와 계산

begin에서만 전용 Harmony ID `StutterFix.decojobshadow`로 관리자 LateUpdate에 Priority.Last postfix 하나를 설치한다. 대량 함수 패치나 장식별 패치는 없다. 명령을 부르기 전에는 진단 패치·카메라 콜백·배열 할당이 없다. 순수 Player에서는 클래스·명령 연결·Stop 연결이 컴파일되지 않는다.

선택 조건:

- 정확히 scrVisualDecoration이며 GetVisible()이 true인 장식. 원본 LogicUpdate는 보이는 장식만 UpdatePosition을 실행하므로 숨은 장식은 비교하지 않는다.
- followPlanet의 관리 참조가 null이고 stickToFloor/lockRotation이 false.
- parallax가 유효하고 inited/isDecoration이 true, decoration이 자기 자신, cachedTransform이 pivotTrans와 같은 관리 참조.
- cameraTransform이 현재 controller.camy.transform과 같음. 카메라 위치는 관리자/프레임당 한 번만 읽음.
- clampToScreen/dontAlterX/dontAlterY가 false. customLevel이 없고 parallax가 꺼진 갈래는 제외.
- pivot 부모가 없거나 world position=(0,0,0), rotation=(0,0,0,1), lossyScale=(1,1,1)을 성분별 정확한 ==로 만족. Unity의 근사 Vector/Quaternion ==를 사용하지 않음.

Dormancy.List를 다시 부르면 상태 준비·카운터 등에 손댈 수 있으므로 호출하지 않는다. 원래 manager.allDecorations에서 읽기만 하며, 별도의 selection 시간에 전체 스캔 비용을 포함한다. 따라서 selection은 제품 경로에 그대로 권장하는 순회 구현이 아니다. 이 보수적 스캔이 비싸면 gather만 보고 전체 이득이라고 해석하면 안 된다.

이 체크아웃에서 `git log --all -- FuseLate.cs` 결과는 없었다. 삭제된 소스를 복구했다고 주장하지 않는다. 남아 있는 [FuseLate 결과](codex-fuselate-20261018-result.md)와 [원본 IL](codex-fuselate-20261018-il.txt), 같은 SHA256의 현재 DLL을 근거로 계산했다.

계산 순서는 다음을 유지했다.

1. `P = pivotPosVec + Vector2.zero`, Vector2→Vector3 변환. 원본의 SetNewStartPosition/posCamAtStart는 매번 이 P로 덮어쓰므로 이전 값을 입력으로 읽지 않는다.
2. `Vector3(camera - P)` → Vector2 변환 → multiplier 성분별 곱 → P 더하기.
3. `parallaxOffset * camScaleMultiplier / textureScaleMultiplier` 순서대로 계산하고 2의 결과에 더하기.
4. 허용한 부모 조건에서 예상 position.z=0. 원본은 먼저 localPosition.z=0을 쓰고 SetTrans에서 world z를 읽는다. 부모 shear, 부호 있는 0, 네이티브 변환 차이는 실제 비교로 드러나며, 동일하다고 미리 인정하지 않는다.
5. rotation=`Quaternion.Euler(0,0,startRot+rotAngle)`.
6. scale=`scaleVec * camScaleMultiplier * (Vector2)Vector3.one`, z=1.

입력을 NativeArray에 모아 동일 계산 함수를 메인 순차 루프와 IJobParallelFor 양쪽에서 실행한다. 잡은 64개 단위로 계산하지만 장식별 출력은 따로 저장한다. Complete 직후 실제 pivotTrans.position/rotation/localScale을 읽어 세 쌍(seq/job, seq/actual, job/actual)을 비교한다. 비교 대상은 float 10성분이며 SingleToInt32Bits로 +0/-0 및 NaN 비트도 구별한다. 각 mismatch 카운터 단위는 '장식 한 개의 출력 레코드 한 쌍'이다. 처음 다른 성분·장식 이름·곡 시각·프레임·양쪽 출력 전체를 R 형식과 16진 비트로 보고한다. 표본 0개는 NO SAMPLES로 명시한다.

제품의 parallax.startPosition/posCamAtStart/endPosition, rotAngle/scaleVec 또는 실제 Transform은 쓰지 않는다. UpdatePosition을 대조용으로 다시 실행하지도 않는다. 다른 모드 postfix가 같은 Priority.Last로 이후 값을 바꾸거나 루프 도중 카메라를 옮기면 관측이 달라질 수 있다. 이 도구의 0 mismatch가 임의 모드 조합까지 보증하지는 않는다.

## C. 비용 보고와 측정 해석

end는 실제 표본 프레임 수를 분모로 사용하며 다음을 보고한다.

| 출력 | 의미 |
|---|---|
| selection | 원본 전체 목록 스캔·조건 검사·부모 확인·카메라 위치 읽기 |
| gather, gather-us/deco | 선정된 장식의 관리 필드/getter → NativeArray 입력 복사. camScaleMultiplier getter의 메인 스레드 비용도 포함 |
| seq | 같은 입력의 메인 순차 계산 및 결과 배열 쓰기 |
| Schedule | 잡 reflection 초기화/스케줄 호출을 포함. 첫 프레임 cold 비용도 포함 |
| Complete | 기다림과 메인 스레드가 직접 맡은 작업을 포함. 즉시 Complete하므로 비동기 겹치기 이득을 가정하지 않음 |
| chunks-sum / worker-chunks / main-chunks | 64개당 타이머 2회로 잰 실행 경과 시간 합계. ThreadIndex로 메인 참여분을 분리. CPU 사용 시간 자체는 아니며 Schedule/Complete에 더하면 중복 계산 |
| actual-read, compare | 실제 Transform 읽기와 비트 비교 비용. 각각 루프 전체를 재며 장식마다 타이머를 읽지 않음 |
| TAA-sync | HashSet/Dictionary 유지·현재 목록 확인·실제 Add/RemoveAtSwapBack 포함 |
| TAA changed/stable | 추가/제거가 있는 프레임과 없는 프레임의 평균 유지 비용, 해당 프레임 수·추가/제거 수 |
| allocation, resizes | NativeArray 최초 생성·용량 증가. 일반 프레임에서는 capacity 재사용 |
| TAA-one-shot | 실제 살아 있는 Transform으로 capacity 생성+Add 및 SetTransforms 측정. 2,150개 이상이면 정확히 2,150개, 미만이면 있는 수를 n으로 표시 |

TAA는 진단 전용 목록으로만 생성·갱신하며 Transform 잡을 스케줄하지 않는다. 실제 게임 오브젝트는 읽기 참조로만 저장한다. 시작 직후 작은 집합을 쟀다가 2,150개에 도달하면 해당 일회 측정값을 2,150개 측정으로 갱신한다. n<2150이면 2150개 실측으로 보고하면 안 된다. 선택 순서와 TAA swap-back 순서는 다를 수 있으나 이번 계산 잡은 TAA를 사용하지 않는다. 실제 쓰기 단계에서는 이 인덱스 매핑을 반드시 설계해야 한다.

초기 JIT·배열 생성·첫 TAA 구성은 켠 판에 실제 부하를 추가한다. 일정 크기 이후에는 배열과 컬렉션 capacity를 재사용하지만 새 최대 크기에서는 확장이 발생할 수 있다. **이 판의 FPS/최악 프레임은 성능 A/B 근거로 사용하지 않는다.** 첫 프레임을 자동 제거하지 않았으며 begin/end에 포함 여부를 적었다. 설치/해제 패치 비용은 프레임당 수치에 포함되지 않는다.

게임 미실행으로 다음 값은 모두 미측정이다: 비교 표본 수/불일치 수, 입력 복사 µs, 스케줄/완료 ms, TAA 2150개 ms, 실제 JobWorkerCount, 작업 스레드 실행. observedThreadIndices가 mainIndex 외 값을 포함하면 Mono 잡이 작업 스레드에서 실행되었다는 관측이다. 여러 스레드 인덱스 관측만으로 그들이 동시에 실행됐다는 것까지 증명하지는 않는다.

**추정 모델(실측 이득 아님):** G=gather, S=Schedule, C=Complete, T=TAA-sync, E=selection, W=아직 측정하지 않은 실제 쓰기·동기화 비용(ms/frame)라 두면,

- 입력 수집+스케줄 하한 = G+S. 실제로 남는 메인 스레드 비용의 완성값이 아니다.
- 현재 동기 그림자 모델 = E+G+S+C+T. 결과 읽기/비교는 제품에 넣지 않으므로 제외하되 실제 Transform 쓰기 W는 아직 빠져 있음.
- 원래 1.9~2.4ms 전체를 대체한다고 가정한 최대 절감 추정 = `[1.9,2.4] - (E+G+S+C+T+W)`. 선정되지 않은 장식·Shader·Hitbox 비용이 남으므로 이 식은 낙관적 상한이다.
- 2,150개 × 입력 복사 1µs = 2.15ms. 이 경우 스케줄/쓰기 전부터 대부분의 예산을 써 이득을 기대하기 어렵다.
- 2,150개 × 0.1µs = 0.215ms라는 가상 입력 비용에서는 다른 비용 전의 예산이 1.685~2.185ms다. 0.1µs는 관측값이 아니며 기대 성능으로 인용하면 안 된다.

따라서 B의 세 mismatch가 모두 0이고 충분한 표본을 얻은 뒤, C의 실제 수치로 2단계를 판단해야 한다. 현재는 성능 개선 채택을 보류한다.

## D. 실제 쓰기 2단계 설계 판단 — 구현하지 않음

1. `scrDecorationManager.LateUpdate`는 타일링·마스크 캐시 갱신 뒤 LogicUpdate 순회, 마지막에 카메라 프레임레이트 화면 갱신이다. 기존 MoveApply.ManagerLateTranspiler가 allDecorations를 Dormancy.List로, LogicUpdate를 LogicMaybe로 바꾼다. LogicMaybe를 통째로 생략하면 UpdateShader/UpdateHitboxState까지 빠진다. **대상 장식의 UpdatePosition만 대체**하고 나머지 작업은 원래 순서로 보존하는 별도 경계를 설계해야 한다. [관리자](codex-jobs-20261018-manager.txt), [장식](codex-jobs-20261018-decoration.txt)
2. DecoAnim은 Update에서 입력 속성을 바꾸고 MoveApply.DeferTransform은 특정 회전·크기 쓰기를 LateUpdate에 맡긴다. 잡 입력은 해당 Update가 끝난 뒤, 원래 UpdatePosition이 읽었어야 할 시점에 수집해야 한다. 생략한 장식이 반드시 잡에 들어가는지와 예외 시 원래 경로로 복귀하는지 보장해야 한다.
3. `GetDecorationWorldSize`는 pivotTrans.localScale을 읽고 기즈모 LateUpdate/Drag가 호출한다([호출 목록](codex-jobs-20261018-worldsize-callers.txt), [Visual 구현](codex-jobs-20261018-visual.txt)). UpdateShader 마스크는 다른 장식의 Transform을 읽을 수 있고, 타일링/마스크 캐시는 순회 앞에 있다. hitbox·기즈모·마스크 등 의존자가 값을 읽기 전 Complete해야 한다. 렌더 직전 Camera.onPreCull만 완료점으로 잡으면 이미 늦을 수 있다. 초기 설계는 관리자 안에서 의존 경계 앞에 Complete하고, 그 뒤에만 더 늦은 PlayerLoop 완료점의 안전성을 조사해야 한다.
4. PACL2를 포함한 외부 모드의 Harmony 패치·Transform 접근은 현재 설치 폴더에서 확인하지 못했다(PACL2 DLL 검색 결과 없음). 예전 사용 환경만으로 호환성을 보증하지 않는다. 대상 외부 모드가 설치된 환경에서 같은 Update/LateUpdate 도중 읽기·쓰기 경로 및 패치 순서를 별도로 확인해야 한다.
5. Dormancy 잠듦/깨움, LazySprite 지연 생성, 새 장식, 파괴·재부모화에서 TAA와 입력/출력 인덱스 매핑을 함께 갱신한다. swap-back으로 옮긴 항목의 매핑도 고쳐야 한다. 실행 중 잡이 참조하는 배열/TAA는 바꾸지 않는다.
6. 곡 끝·실패·다시 시작·편집 복귀·새 맵·비활성화·Unload에서 Complete → 진단/제품 잡 사용 중지 → TAA/NativeArray Dispose → 관리 참조 해제 순서가 필요하다. 실제 쓰기 경로에서는 실패/재시작에 scene change가 없는 경우도 포함해 기존 종료 훅과 연결해야 한다.
7. 같은 계층은 쓰기 잡이 한 스레드에 몰릴 수 있으므로, IJobParallelFor 그림자 결과가 빠르더라도 Transform 쓰기까지 선형 병렬화된다는 추정은 하지 않는다. 계층을 바꾸면 좌표계·순회·외부 모드 의미가 바뀌므로 1단계에 포함하지 않았다.

이번 진단 도구 자체는 매 프레임 즉시 Complete하므로 프레임 사이 실행 중 잡이 남지 않는다. end, scene change, 렌더 전 곡 종료/관리자 교체 감지, 모드 해제에서 전용 패치와 두 이벤트를 해제하고 모든 네이티브 배열/TAA 및 장식 참조를 비운다. 진단 예외 시에도 중지하며 오류를 한 번 기록한다. begin은 재생 중에만 허용한다. 게임을 실제로 돌리지 않았으므로 수명 경로의 런타임 검증은 남아 있다.

## 오프라인 검증

| 명령 | 결과 |
|---|---|
| `dotnet build -p:Edition=Player -p:AutoTestBuild=1 -v q --nologo` | 오류 0, 경고 35 |
| `dotnet build -p:Edition=Player -v q --nologo` | 오류 0, 경고 4 (증분 빌드) |
| `dotnet build -v q --nologo` | 오류 0, 경고 35 |
| LoadCheck PlayerAuto / Dev | FieldRefAccess 241곳, 틀림 0 |
| LoadCheck Player | FieldRefAccess 215곳, 틀림 0 |
| Player 타입 검사 | DecoJobShadow 0개 형식 |
| PlayerAuto / Dev 타입 검사 | DecoJobShadow 및 값 형식/잡 포함 |

경고는 기존 어셈블리 버전 충돌·미사용 멤버 등이다. 최종 추가 코드의 새 경고는 없다. 빌드 로그는 `codex-jobs-20261018-{playerauto,player,dev}-build.txt`, FieldRef 근거는 [loadcheck](codex-jobs-20261018-loadcheck.txt), 컴파일된 클래스 IL은 [shadow-il](codex-jobs-20261018-shadow-il.txt)에 있다. ILSpy의 -il은 전체 어셈블리를 출력하므로 클래스 시작~끝을 추출해 확인했다. 해당 클래스에는 Transform setter/SetPositionAndRotation 또는 게임 UpdatePosition 호출이 없다.

LoadCheck는 .NET 오프라인 필드 검사이며 Unity 네이티브 잡 실행 성공을 증명하지 않는다. 빌드 출력은 저장소 bin 안에만 생성했다. 설치 훅은 호출하지 않았다. Mods/StutterFix 하위 파일 전체의 해시 목록은 작업 전후 동일했다. HEAD는 작업 중 외부 커밋 `6101ae2`로 바뀌었다(별도 지시서 `codex-updatecost-20261018.md`만 추가, 이번 수정 파일과 겹침 없음). 이 작업에서는 커밋 명령을 실행하지 않았고 외부 변경은 보존했다. [보존 검증](codex-jobs-20261018-preservation.txt), [출력 DLL 해시](codex-jobs-20261018-output-hashes.txt)에 근거를 남겼다.

## Claude가 게임에서 실행할 순서

아래는 후속 실행 담당자를 위한 명령이다. **이번 작업에서는 설치·게임 실행·명령 전달을 수행하지 않았다.** DLL 교체는 별도 권한이 있는 후속 작업에서만 한다. fusestate는 필요 없다. 정확한 Arche .adofai 경로는 해당 실행 환경에서 선택한다.

게임 화면에서 원래 판을 재현하는 경우(설정·화면 조건은 기존 기준과 동일):

```text
auto on
game <Arche .adofai 전체 경로>
press
wait 120
wait 120
wait 15
decojob shadow begin
wait 25
decojob shadow end
```

wait는 실시간이므로 로딩·재생 지연에 따라 실제 시작/끝 곡 시각이 255/280과 다를 수 있다. begin/end 출력 song 및 Player.log를 확인해 목표 255~280초 범위인지 판단한다. 255초를 한 번의 wait로 보내지 않는다(자동 시험 단계 제한). game이 키 대기 상태가 아닐 때에는 press와 자동 재생 시작 시각을 먼저 확인한다.

빠르게 편집기 중간 타일부터 확인하는 별도 방법:

```text
open <Arche .adofai 전체 경로>
auto on
seek 255 8
wait 8
decojob shadow begin
wait 25
decojob shadow end
stop
```

seek는 편집기 전용이고 해당 타일 entryTime 기준으로 시작하므로 이것도 출력 곡 시각으로 보정한다. 게임 화면 경로와 다른 조건이므로 두 경로의 비용을 섞지 않는다.

후속 확인 순서: frames>0/compared>0 → 세 mismatch 모두 0 확인 → first 예제 확인 → G+S와 전체 E+G+S+C+T 확인 → n=2150의 TAA 일회 비용 확인 → changed/stable 구분 → workerCount 및 작업 스레드 참여 확인. 비교가 한 개라도 다르면 2단계로 넘어가지 않는다. 별도 재생에서 end 후 패치/이벤트/배열 정리, 다시 begin, 곡 종료·재시작·편집 복귀·맵 교체·모드 해제도 확인한다. 순수 Player에는 명령이 없어야 한다.
