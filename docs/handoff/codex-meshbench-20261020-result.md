# meshbench 구현 결과 — 2026-10-20

## 완료 범위

`MeshBench.cs`를 추가하고 AutoTest 명령/취소 경로 및 개발자·AutoTest 전용 컴파일 항목에 연결했다. `#if DEV || AUTOTEST`와 프로젝트의 조건부 Compile을 함께 적용한다. 일반 Player 구성에서는 포함되지 않는다.

게임 실행·설치·커밋은 하지 않았다. 아래 내용은 구현·컴파일·오프라인 불변식 점검 결과이며 실제 프레임 시간 측정 결과가 아니다. 목표인 전체 프레임 평균 -1.5ms 이상 달성 여부는 미측정이다.

## 사용법

```text
meshbench <N=2000> <측정프레임=300> <G=42> <T=10>
```

허용 범위: N 1..50000, 측정프레임 1..10000, G 1..32768, T 1..64. 곡 재생 중에는 거부하며 Built-in RP와 Default 레이어를 보는 활성 카메라가 필요하다. 매 방식마다 60프레임 워밍업 후 지정한 프레임 수를 수집한다.

한 명령이 `renderers → a-buffer → b-lists → c-job` 순서로 실행된다. 각 방식의 자원을 Destroy한 뒤 정리 프레임을 두고 다음 방식을 시작한다. 기준선은 해당 명령의 renderers 결과만 사용하며 다른 실행이나 기존 instbench 결과와 섞지 않는다. 시작 실패/미지원/실행 예외가 발생하면 이유를 기록하고 해당 명령의 나머지 방식을 중단한다.

후속 게임 측정 시 AutoTest 단계 예시(이번 작업에서는 실행하지 않음):

```text
timeout 600
meshbench 2000 300 42 10
meshbench 1100 300 42 10
meshbench 2000 300 1 1
meshbench 2000 300 400 10
```

각 meshbench 명령은 기본값에서 총 1,440프레임과 생성·정리 시간이 필요하다. 600초는 예시이며 낮은 FPS나 큰 표본 수에서는 늘려야 한다. vSync와 targetFrameRate는 벤치가 바꾸지 않으므로 제한 영향을 확인하고 측정해야 한다.

| N | G | T | 생성 그룹 | 총 정점 | 이번 실측 |
|---:|---:|---:|---:|---:|---|
| 2000 | 42 | 10 | 420 | 8000 | 미실행 |
| 1100 | 42 | 10 | 420 | 4400 | 미실행 |
| 2000 | 1 | 1 | 1 | 8000 | 미실행 |
| 2000 | 400 | 10 | 2000 | 8000 | 미실행 |

장식 i는 `texture=i%T`, `sortingOrder=(i/T)%G`로 분배한다. 존재하는 조합만 생성하므로 마지막 설정은 가능한 4,000조합 중 2,000그룹을 생성하며 실제 사용 sortingOrder는 0..199다. 빈 그룹을 추가하지 않는다. 모든 Renderer의 sortingLayerID는 0이다.

## 구현 방식

| 방식 | 구현 / 컴파일 | 런타임 확인 |
|---|---|---|
| renderers | 개별 SpriteRenderer의 position/rotation/localScale/color 갱신 | 미실행 |
| a-buffer | NativeArray<Vertex> CPU 채우기 + SetVertexBufferData | 미실행 |
| b-lists | MarkDynamic + 재사용 List<Vector3>/List<Color> 직접 채우기 + SetVertices/SetColors | 미실행 |
| c-job | IJobParallelFor에서 a와 같은 NativeArray 채우기, Complete 후 메인에서 메시 업로드 | 미실행 |

세 메시 방식 모두 그룹마다 원점·항등 Transform의 GameObject(MeshFilter+MeshRenderer)를 만들고 `Sprites/Default` 복제 재질과 텍스처를 사용한다. 재질은 텍스처별로 공유한다. 각 메시 정점은 CPU TRS로 계산한 월드 좌표이며 쿼드당 4정점, 6인덱스다. 인덱스와 큰 고정 bounds는 초기화 시 설정한다. 모든 메시를 MarkDynamic으로 표시한다.

a/c 정점은 Position float3 + Color float4 + UV float2, stride 36바이트다. 업로드에는 `DontRecalculateBounds|DontValidateIndices|DontNotifyMeshUsers`를 적용한다. b는 고정 UV를 초기화 때만 넣고 위치·색 리스트를 직접 갱신한다. b가 NativeArray에서 리스트로 복사하는 추가 비용을 지불하지 않도록 했다. 반대로 a/c는 interleaved 정점의 UV도 매 프레임 채우고 업로드한다. 이 데이터 경로 차이까지 포함한 비교다.

c는 Burst를 사용하지 않으며 프레임당 한 번 Schedule(N,64)/Complete한다. 작업자는 메시 API나 GameObject를 만지지 않는다. NativeDisableParallelForRestriction은 장식마다 서로 겹치지 않는 4정점 영역에 쓰기 위해 적용했다. 메인 구간에는 Schedule/Complete 대기와 업로드 호출까지 포함되므로 별도 작업자 CPU 시간이나 순수 메인 실행 시간으로 해석하면 안 된다.

기존 InstBench의 결정적 값 계산을 `InstBench.Layout.Value`로 추출해 두 벤치가 공유한다. 카메라 선택·화면 내 그리드·위치·회전·비균등 크기·RGBA 공식은 기존 instbench와 같다. meshbench의 기준선에는 메시 방식과 같은 텍스처/그리기 순서 분배를 적용하므로 기존 instbench의 모든 sortingOrder=0 기준선과는 구별해야 한다.

## 로그와 보호·정리

로그 접두사는 `[마이크로벤치] meshbench`다. 방식별 N/G/T, 비어 있지 않은 그룹 수, 총 정점, 메인 구간 및 전체 프레임의 평균/p99와 표본 수를 기록한다. a/b/c는 renderers 대비 Δ메인 평균/p99, Δ전체평균, Δ전체p99를 기록한다. 음수는 기준선보다 짧은 시간이다. 메인 구간은 값 계산·정점 채우기·메시 업로드 호출(또는 Renderer 설정)이며 전체 프레임은 해당 구간 시작부터 다음 Update까지의 간격이다. GPU 직접 시간은 측정하지 않는다.

재생 시작, 장면 변경, 선택 카메라 소실/비활성화/행렬/투영/cullingMask 변경, 활성 카메라 수, 해상도, vSync, targetFrameRate, 화질 변경 시 중단한다. 방식 간에도 기준 조건 키를 비교한다. 임의의 장면 콘텐츠 변화나 다른 카메라의 세부 속성 변화까지 모두 감시하지는 않는다.

정상 종료·예외·취소·OnDisable·OnDestroy 시 이 벤치가 만든 GameObject, Mesh, Material, Sprite, Texture와 NativeArray를 정리한다. 초기화 도중 실패해도 생성된 배열·그룹 범위만 정리한다. AutoTest Finish의 MeshBench.Cancel 연결로 시간 초과 시에도 정리한다. 게임 장식과 기존 재질·메시·카메라 설정은 변경하지 않는다.

## 검증 결과

요청한 빌드:

```text
dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1
```

성공: 오류 0개, 경고 36개, 경과 5.07초. 출력 `bin/PlayerAuto/StutterFix.dll`. 경고는 참조 어셈블리 버전 충돌(MSB3277), 기존 API 사용 중단/미사용 필드·변수 등이며 새 MeshBench.cs 경고는 없었다.

- `git diff --check` 통과.
- 일반 Player의 MSBuild Compile 목록에서 MeshBench/InstBench/AutoTest 제외 확인.
- 필수 네 설정과 경계 세 설정 `(1,32768,64)`, `(50000,1,1)`, `(50000,32768,64)`에 대해 빈 그룹 없음, N개 장식의 중복 없는 완전 분배, Renderer와 메시의 텍스처/순서 일치, 4N개 정점 영역의 중복 없는 완전 커버를 오프라인에서 점검했고 모두 통과했다. 이는 Unity 렌더링이나 작업 스레드 실행 검증이 아니다.

## 막힌 것과 해석 제한

인계 문서의 게임 실행 금지에 따라 네 설정의 실제 평균/p99/Δ 수치는 수집하지 않았다. a/b/c의 API 참조와 컴파일은 모두 가능했지만 장치별 지원, Unity Mono 작업 실행, 실제 업로드·렌더링·정리, 픽셀 동등성은 런타임 확인이 남아 있다.

그룹의 sortingLayerID/sortingOrder는 적용했지만 실제 장식의 SpriteMask/stencil, SortingGroup, Sprite 정렬 기준점, 같은 sortingOrder 내 거리·투명 정렬, 다른 SpriteRenderer와의 내부 교차 순서, atlas/비직사각형 Sprite/뒤집기/게임 전용 재질은 재현하지 않는다. 특히 같은 순서의 다른 텍스처를 별도 MeshRenderer로 묶으면 개별 SpriteRenderer의 정렬과 시각적으로 같다고 보장할 수 없다. 이 시험은 실게임 렌더러 교체 구현이 아니다.

전체 프레임 결과에는 vSync/FPS 제한, 렌더 스레드, GPU, 장면 부하와 방식 고정 실행 순서의 영향이 포함된다. 후속 실측 전에는 -1.5ms 목표 달성이나 실게임 개선을 결론 내릴 수 없다.
