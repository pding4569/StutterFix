# bake-3 결과: 크기 채널 지원과 Arche 오프라인 지원률

2026-10-20. `E:\StutterFixWork\wt-bake`, `codex/night-bake`, 시작 HEAD `481d334`(bake-3 지시서). 게임 실행·설치·커밋은 하지 않았다. 기존 게임/모드 경로를 수정하지 않고 작업 폴더 안에서만 구현·빌드·오프라인 검사했다. 그림자 모드/기본 꺼짐을 유지한다.

크기 X/Y를 지원하도록 연결했고 3종 빌드 오류 0, LoadCheck 통과, 일반 Player 제외를 확인했다. 다만 **파일상 지원 후보는 bake-2 4개에서 bake-3 5개(0.0173%)**다. 가장 큰 제한은 초기 Global 이외 배치 28,830개(99.9827%)다. 실제 게임 지원률·화면 오차·성능은 미측정이다.

## 크기 의미와 코드 근거

설치 DLL을 ILSpy로 정적 역컴파일했다. DLL SHA256과 관련 메서드 발췌는 [크기 의미 근거](bake-evidence/bake3-scale-read-sites.txt)에 있다. 게임 프로세스나 Unity 장면은 실행하지 않았다.

| 질문 | 확인한 의미 | 근거 |
|---|---|---|
| 목표와 상대/절대 | `LevelEvent.Decode`는 숫자를 양축으로 복제하고, 배열의 null 축을 NaN으로 만든다. `ffxMoveDecorationsPlus.Decode`는 Vector2 scale을 100으로 나눈다. `StartEffect`의 비NaN legacy `targetScale`은 양축 목표에 우선한다. `relativeTo=LastPosition`도 크기는 절대 비율이다. 위치와 달리 상대 덧셈이나 `SetRelative` 호출이 없다. | `LevelEvent.Decode`, `ffxMoveDecorationsPlus.Decode/StartEffect`, `FastMove.Run` |
| 시작값 시점 | `DecoAnim.Scale`은 해당 축 `KillKey` 후 레코드를 만들고 목표만 저장한다. 첫 `Step`의 `Startup`에서 그때의 `scaleVec`를 읽는다. 첫 Step 전 완료도 `Complete`가 Startup부터 한다. StartEffect 시점의 현재값을 즉시 캡처하는 방식이 아니다. | `DecoAnim.cs`: `Scale`, `Startup`, `Step`, `Complete` |
| X/Y 순서 | 원본 StartEffect와 FastMove는 X Kill→생성, Y Kill→생성 순서다. DOTween Vector2Plugin은 Startup에서 시작 벡터를 읽고, 매 적용 시 getter로 현재 벡터를 다시 읽어 제한 축만 바꾼다. DecoAnim도 X/Y 각각 현재 벡터의 자기 축만 바꾼다. Y가 X 갱신 후 벡터를 읽어도 Y의 시작 성분은 변하지 않는다. | `Vector2Plugin.SetChangeValue/EvaluateAndApply`, `DecoAnim.ApplyE` 7/8 |
| 교체 | 이전 동일 축 효과를 완료한 **실제 산술 완료값**이 새 시작값이다. 반대 축은 유지된다. 위치의 상대 목표는 Kill 이전 값에서 잡지만 크기에는 상대 목표 계산이 없다. | `Scale`→`KillKey`→`Kill(true)`→`Complete` |
| 동일 프레임 여러 효과 | 앞 효과가 아직 Startup 전이어도 다음 동일 축 효과의 Kill(true)가 시작·완료시킨다. X만 바꾸는 효과는 Y 트랙을 끊지 않는다. 동일 T0에서는 최종 목록의 순서대로 처리한다. 서로 다른 T0가 실제로 한 프레임에 실행되는 시점 차이는 기존 bake-2와 같이 곡 시각/실행 프레임의 위상 오차로 남는다. | 원본 StartEffect의 축별 dictionary 키, `DecoAnim.Complete`, `DecoBakeCore.Find` |
| 길이 0와 완료 | 크기는 OnComplete 목표값 재대입이 없다. 길이 0은 기존 ZeroTween처럼 ease(1,1), 양의 길이는 ease(duration,duration)을 이용한 `시작 + (목표-시작)*이징`을 보존한다. float 상쇄로 완료값이 목표와 달라질 수도 있다. | `InstantMove.CScale`, `ZeroTween.EaseAtEnd/Calc`, `DecoAnim.ApplyE/OnComplete` |

지연 Startup을 단순화할 수 있는 범위는 **기존 지원 조건에서 scaleVec의 자기 축 작성자가 해당 축 효과뿐인 경우**다. 그동안 다른 축 갱신·피벗·위치·회전·시차 설정은 자기 scale 성분을 쓰지 않는다. 최초 트랙의 시작값은 Setup 종료 시 초기 scaleVec, 후속 트랙의 시작값은 이전 트랙의 완료값이다. 이를 곡 시각 기반 무상태 평가에 사용했다. 프레임 누적 Step과 같은 시계라고 주장하지 않는다.

현재 설치본 `scrDecoration`에는 `realScale`/`tilingScale`이라는 멤버가 없다. 실제 필드/속성 관계는 `scaleVec`→`SetScale`→`pivotTrans.localScale = scale * camScaleMultiplier * 부모배율`이다. `camScaleMultiplier`에는 `scaleMultiplier * textureScaleMultiplier`와 lockScale의 카메라 배율이 들어간다. stickToFloor는 부모 바닥 배율을 곱한다. 원래 크기 설정은 `SetTextureScaleMultiplier`로 별도 배율을 바꾼다. tiling은 `scrVisualDecoration.repeatX/Y`, 텍스처/mesh/material 경로에서 처리하며 scaleVec과 같은 값으로 취급하지 않는다.

`SetPosition`의 피벗 자식 위치는 `(pivotOffset + croppedPivotOffset) / trueScaleMultiplier`다. 크기는 그 자식의 부모 변환에 적용되므로 최종 피벗 이동량과 화면 크기에는 영향을 준다. `SetScale` 자체는 scaleVec만 보관하고 부모 localScale을 설정하며, 피벗/시차 상태를 새로 읽어 자기 scaleVec을 되쓰지 않는다. `UpdatePosition`과 시차의 화면 합성은 scalar 크기 평가 검증과 별도다.

초기 Tile/Camera/행성 배치, 추종·lockScale·stickToFloor, 마스크, 이미지/원래크기/크롭 변경, 조건/히트박스, 외부 setter 패치, 사용자 이징 등은 기존의 **서로 구분된 제외 사유로 장식 전체를 계속 제외**한다. 크기 변화량/완료 산술이 비유한이면 새 사유 `크기 산술 비유한`을 붙이고 이미 만들어진 다른 속성 트랙도 모두 제거한다. 해석하지 못한 갈래를 새로 허용하지 않았다.

## 구현과 verify

- `DecoBake.Core.cs`: 기존 16채널 중 보관만 하던 5/6번을 크기 X/Y로 활성화했다. `CopyScale`이 scalar 우선·NaN 축 미사용을 처리한다. 위치의 상대 플래그는 크기에 적용하지 않는다. 구간에 보간 목표 `Target`과 산술 완료 `End`를 구분하고 이징은 기존 함수를 그대로 쓴다. 곡 시각 식, 동일 T0 순서, 되감기 무상태 계약은 유지했다.
- `DecoBake.cs`: 기존의 크기 일괄 제외를 제거하고 `CopyEvent`에서 목표를 복사한다. `forceDontTweenMovement`이면 복사하지 않는다. Setup에서 가능한 경우 스프라이트 bounds와 고정 배율만 읽어 명목 크기 환산 계수를 보관한다. 새 Harmony 패치는 없다. 기존 두 후크와 FastMove 시작 관찰을 재사용하며 metadata-token 서명 해석을 추가하지 않았다.
- `decobake verify`: 크기 X/Y 절대 오차 n/최대/p99/상위8와 교체 장식 통계에 더해 상대 오차 `|예측-실제|/|실제|`의 n/최대/p99·교체 n/최대/p99를 출력한다. 실제값 0은 상대 오차 NA 개수로 분리하고 절대 오차에는 포함한다.
- 가능하면 `|scale오차| * 초기 sprite.bounds 해당축 * |trueScaleMultiplier 해당축| * 프레임 PixelsPerWorld`를 명목 축길이 픽셀 오차로 출력한다. 스프라이트 지연 로딩/크기0/직교 배율 없음은 NA다. 회전·시차·피벗·타일링을 합성한 최종 렌더 오차가 아니며 이를 0.01px 화면 승인으로 해석하지 않는다. 위치 환산 로그의 해상도 표기는 실제 계산에 쓰는 높이 1440으로 정리했다.
- payload 추정은 새 구간 필드와 크기 계수를 반영했다. 메모리 수치는 여전히 객체 header/임시 수집 목록/GC를 제외한 추정이다. 새로운 실맵 비용 측정은 하지 않았다.

## 오프라인 지원률

도구: [bake-support-estimate.py](../../tools/optimization/bake-support-estimate.py). 명시한 단일 파일만 읽고 stdout으로 Markdown을 출력한다. 이미지·다른 맵·게임을 열거나 원본을 수정하지 않는다. 정확한 파일은 다음과 같다.

`D:\얼불춤 맵 파일\Arche\[𝐃𝐎 𝐍𝐎𝐓 𝐎𝐏𝐄𝐍 𝐓𝐇𝐈𝐒 𝐀𝐓 𝐇𝐎𝐌𝐄] FULL.adofai`

SHA256: `026873b89f05ea94b86b83634a938ac053593ebea6cb832d8134e4a8084f8064`. 집계 후 해시도 같았다. 원본의 후행 쉼표 102,058곳과 decorations 앞 구분 쉼표 누락 1곳만 문자열 밖에서 메모리 보정했다. 셀프테스트로 문자열 안 `,}` 보존을 확인했다.

분모는 **소스 장식 행 28,835개**(이미지 AddDecoration 28,828 + AddText 7)다. 같은 이미지/태그를 공유해도 서로 다른 장식 행을 합치지 않는다. 태그는 실제 manager처럼 OR 합집합/Distinct로 대응했다. MoveDecorations 95,211개, scale 포함 원본 이벤트 74,259개, 대상 없는 이벤트 0개다. RepeatEvents 13개는 같은 태그의 고유 대상 집합을 늘리지 않으므로 반복 인스턴스별 비용/시점은 확장하지 않았다. 실제 manager의 생성 인스턴스 수와는 구분한다.

| 제외 사유(사유 안 고유 장식, 사유 간 중복 가능) | bake-2 | bake-3 |
|---|---:|---:|
| 초기 Global 이외 배치 | 28,830 (99.9827%) | 28,830 (99.9827%) |
| 크기 지연 Startup/축 순서 | 28,129 (97.5516%) | 0 (0%) |
| 이미지/원래크기/크롭/wrap | 5,087 (17.6418%) | 5,087 (17.6418%) |
| 가시성/깊이 변경 | 22 (0.0763%) | 22 (0.0763%) |
| 이미지 이외 장식 | 7 (0.0243%) | 7 (0.0243%) |
| 파일상 초기/이벤트 마스크、히트박스、조건/수동/타격、시각 조건、사용자 이징、다른 장식 효과 | 각 0 | 각 0 |
| 파일상 추종/고정、Global 이외 배치 변경、비유한 초기/목표、음수/비유한 길이 | 각 0 | 각 0 |
| 파일상 제외 없음(런타임 조건부 후보) | **4 (0.0139%)** | **5 (0.0173%)** |
| 파일상 제외 있음 | 28,831 (99.9861%) | 28,830 (99.9827%) |
| components 영향 추가 미정(위 제외와 중복) | 228 (0.7907%) | 228 (0.7907%) |
| 최종 런타임 지원 판정 미정 | 28,835 (100%) | 28,835 (100%) |

0건은 해당 키/원본 이벤트로 확인되는 사유의 수다. 런타임에서 해당 사유가 없다는 증명이 아니다. 모든 장식에서 시차 부품/축 고정/화면 고정, 외부 모드 setter 패치·직접 쓰기, High/커스텀 일반 맵 조건, 고정 시계, 스키마 버전 변환·disabled 기본값, 예약 목록 밖 효과·히트박스 연결, 실제 초기값/이미지 로딩이 **미정**이다. 원본 components가 비어 있지 않은 228개는 추가 미정으로 분리했다. 이를 지원으로 세지 않는다. 위 가정 아래 실제 지원률의 조건부 범위는 bake-2 0~0.0139%, bake-3 0~0.0173%다. 자세한 전체 사유 표와 미정 표는 [도구 출력](bake-evidence/bake3-support-estimate.md)에 있다.

### 지원 확장 상위 5개 제안

순위는 앞 순위를 누적 해제했을 때 새로 늘어나는 **파일상 후보**가 가장 많은 순서다. 초기 배치는 서로 다른 구현 계약인 Tile과 Camera 계열로 나눴다. 모르는 갈래를 이번 코드에서 풀지는 않았다.

| 순위·갈래 | 해당 장식 | 이것만 해제할 때 순증 | 앞 순위까지 누적 해제 시 추가 후보 | 코드 근거/필요 확인 |
|---|---:|---:|---:|---|
| 1. Tile 초기 배치 | 28,716 (99.5873%) | 23,522 (81.5745%) | 23,522 (81.5745%) | SetupPostfix의 Global 검사, SetPlacementType의 floor 기반 startPos와 이후 배치 변경/시차 계약 |
| 2. 이미지/원래크기/크롭/wrap | 5,087 (17.6418%) | 0 | 5,053 (17.5238%) | Reasons의 이미지 관련 Used 플래그; 실제 이 맵은 decorationImage 이벤트가 원인. sprite/texture 배율과 피벗 재계산·비동기 로딩 필요 |
| 3. 가시성/깊이 | 22 (0.0763%) | 0 | 14 (0.0486%) | Reasons의 visibleUsed/depthUsed; 실제 이 맵은 depth 이벤트. 스냅샷 채널/렌더 순서 의미 확인 필요 |
| 4. 이미지 이외(AddText) | 7 (0.0243%) | 0 | 7 (0.0243%) | SetupPostfix의 scrVisualDecoration 검사. 텍스트 setter/SetText 및 크기 측정 의미 필요 |
| 5. Camera/CameraAspect 초기 배치 | 114 (0.3954%) | 4 (0.0139%) | 6 (0.0208%) | SetPlacementType의 좌표 단위 변환, 카메라 종속 배치·시차 계약 필요 |

단독 순증과 누적 추가 후보는 중복 때문에 다르다. 위 5개를 모두 해제해도 components 미정 등은 남는다. 초기 Global 검사 전체만 해제하는 반사실에서는 23,526개(81.5883%)가 추가 후보이나, 이는 Tile/Camera 구현 완료나 실제 런타임 지원 보장이 아니다.

재현 명령(출력 리다이렉션 대상만 작업 폴더):

```powershell
$env:PYTHONIOENCODING = 'utf-8'
py -3 tools/optimization/bake-support-estimate.py --self-test
py -3 tools/optimization/bake-support-estimate.py 'D:\얼불춤 맵 파일\Arche\[𝐃𝐎 𝐍𝐎𝐓 𝐎𝐏𝐄𝐍 𝐓𝐇𝐈𝐒 𝐀𝐓 𝐇𝐎𝐌𝐄] FULL.adofai'
```

## 오프라인 검사 결과

| 검사 | 결과 | 증거 |
|---|---|---|
| `dotnet build StutterFix.csproj -c Release` | 오류 0, 경고 38 | [개발](bake-evidence/bake3-build-dev.txt) |
| 위 명령 + `-p:Edition=Player` | 오류 0, 경고 4 | [Player](bake-evidence/bake3-build-player.txt) |
| 위 명령 + `-p:Edition=Player -p:AutoTestBuild=1` | 오류 0, 경고 38 | [AutoTest](bake-evidence/bake3-build-playerauto.txt) |
| LoadCheck 개발/Player/AutoTest | FieldRefAccess 266/229/266, 틀림 모두 0, 동적 이름 각 5곳 | [개발](bake-evidence/bake3-loadcheck-dev.txt), [Player](bake-evidence/bake3-loadcheck-player.txt), [AutoTest](bake-evidence/bake3-loadcheck-playerauto.txt) |
| 타입/후크 계약 | 개발·AutoTest 굽기 타입 포함, 일반 Player 제외. 기존 Setup/PrepVfx 정확 일치 각각 1 | 같은 LoadCheck 로그 |
| production Core 직접 링크 fixture | 크기 교체/LastPosition에서도 절대 크기/NaN 축/동일 T0 미시작 교체/길이0/음수 크기/legacy scalar/되감기/완료 산술/overflow 전체 제외 통과 | [fixture](bake-evidence/bake3-contract-check.txt) |
| 이징 | 크기 3,600개 중간·완료 산술 비교, 기존 위치 이징 3,465개 및 기존 경계 계약 통과 | 같은 fixture 로그 |
| 추정 도구 셀프테스트 | JSON 보정의 문자열 보존, 태그 중복 고유화, scale NaN 축 형태, 나중 제외 전파 통과 | [도구 테스트](bake-evidence/bake3-estimator-selftest.txt) |
| 변경 공백 검사 | `git diff --check` 통과 | 로컬 검사 |

Player는 이번 호출의 증분 빌드 결과라 이전 bake-2의 전체 빌드 경고 수와 직접 비교하지 않는다. .NET 8 fixture는 게임 DOTween의 순수 EaseManager만 사용했다. LoadCheck는 메타데이터 검사이며 Mono Harmony 설치/실제 게임 정확도를 증명하지 않는다. 첫 fixture의 INTERNAL_Zero 끝점을 0으로 가정한 테스트가 실패하여, 실제 EaseManager 반환값을 기준으로 수정하고 별도의 float 상쇄 사례로 강제 목표 대입과의 차이를 검증했다. 최종 로그는 수정 후 전체 통과 결과다.

## 내가 돌릴 순서

**이번에는 아래를 실행하지 않았다.** 기존 bake-2 순서에 크기 지표를 추가한 향후 사용자 승인 후 실행 계획이다. 기존 기본 DecoAnim 경로, High 시각 설정에서 open 전에 on을 한다. play가 PrepVfx를 재구성할 수 있으므로 재생 후 ready를 기다린 다음 캡처한다.

```text
decobake on
open D:\얼불춤 맵 파일\Arche\[𝐃𝐎 𝐍𝐎𝐓 𝐎𝐏𝐄𝐍 𝐓𝐇𝐈𝐒 𝐀𝐓 𝐇𝐎𝐌𝐄] FULL.adofai
select 0
play
wait 1
decobake ready
decosim capture 12
wait 13
stop
decobake verify
decobake bench
decobake off
```

먼저 준비 로그에서 실제 장식/효과/지원 수를 확인한다. 파일상 후보가 5개뿐이라는 추정과 실제 수집·제외 사유를 대조하고, 크기 표본 n=0을 오차 0으로 해석하지 않는다. 짧은 구간에 교체/크기 이벤트가 없다면 해당 구간을 별도로 캡처해야 한다.

기존 위치/색/회전/불투명도/시차 절대 오차와 시작 위상 프레임 분포, 교체 장식 오차, 사유별 지원률에 다음 크기 지표를 추가한다: X/Y 절대 n/최대/p99/상위8, 상대 n/최대/p99·교체 통계, 실제0 상대 NA 수, 명목 축길이 px n/최대/p99·환산불가 수. 길이0/중간 Kill 완료는 기존 테이프 Step 표본 범위 밖이며 fixture 증거로 구분한다.

bench에서 실제 지원 수와 함께 worker 굽기 시간/수집 CPU/최대 메인 slice/완료 wall 시간/메모리/평가 중앙·p99를 확인한다. 최대 메인 작업 16ms, 로딩 2초, 최종 화면 0.01px 등은 아직 통과 판정할 자료가 없다. 제품 채택·렌더 변경·릴리스는 수행하지 않았다.
