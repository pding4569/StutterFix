# instbench 구현 결과 (2026-10-20)

## 완료 범위

- `InstBench.cs` 추가, `AutoTest.cs` 명령 분기 및 취소 처리 연결, csproj 조건부 Compile 등록.
- 모든 새 코드는 `#if DEV || AUTOTEST`. 일반 Player의 Compile 목록에서 InstBench/MicroBench/AutoTest 제외 확인.
- 커밋·게임 실행·설치 하지 않음. 실제 프레임 시간, GPU 비용, 시각 동등성은 미측정/미검증.
- **정렬 순서 미반영**. 단일 정렬 슬롯 실험이며 실제 장식의 투명 정렬을 대체하지 않음.

## 사용법

기존 AutoTest 명령 묶음에서 사용:

```text
instbench [N=2000] [측정프레임=300] [renderers|drawinst|renderparams|drawindirect|texturearray] [K=1]
```

N: 1..50000, 측정 프레임: 1..10000, K: 1..64. 생략 시 `instbench 2000 300 renderers 1`.
각 명령은 워밍업 60프레임 후 지정 프레임을 측정한다. 같은 K에서 renderers를 먼저 실행해야 자동 차이 로그가 나온다.
K는 자동 순회하지 않으며 1, 4, 16, 64 각각 아래 묶음을 반복한다.

```text
instbench 2000 300 renderers 1
instbench 2000 300 drawinst 1
instbench 2000 300 renderparams 1
```

나머지 K에서는 마지막 인수를 각각 4, 16, 64로 바꾼다. 기준선은 마지막으로 완료한 renderers 한 건만 보관한다.
선택 경로 진단은 `instbench 2000 300 drawindirect 1`, `instbench 2000 300 texturearray 1`.
기본 AutoTest 단계 제한은 180초이며 저프레임/긴 측정에서는 앞에 `timeout 600` 등 충분한 제한을 지정한다.

곡 재생 중 시작을 거부하고 활성 카메라가 필요하다. Built-in RP 전용이다.
Camera.main(없으면 첫 활성 카메라)을 기준으로 화면 안에 격자를 배치한다. 해당 카메라가 Default 레이어를 보아야 한다.
SpriteRenderer와 두 Graphics 경로 모두 벤치 레이어를 보는 전체 카메라에 렌더링한다. 로그에 활성 카메라 수를 기록한다.
시험 중 곡 재생 시작, 장면 변경, 기준 카메라 이동/투영 변경·비활성화, 카메라 수 변경,
해상도/vSync/targetFrameRate/화질 단계 변경 시 중단하고 정리한다.
AutoTest 취소/시간 초과, 컴포넌트 비활성화/파괴, 초기화·측정 예외에도 정리한다.
부모 없는 렌더러 GameObject, Sprite, Texture, Mesh, Material, 로드한 Shader/AssetBundle만 정리한다.
정상 완료 시 마지막 프레임을 수집하고 Destroy 처리 다음 프레임까지 기다린 뒤 AutoTest를 진행한다.

## 방식별 상태와 근거

| 방식 | 구현/가능성 | 근거와 제한 |
|---|---|---|
| renderers | 구현, 빌드 통과 | 부모 없는 1x1 SpriteRenderer N개. 매 프레임 위치/회전/비균일 크기/색 rgba 쓰기. 실제 렌더링 검증은 남음. |
| drawinst | 구현, 빌드 통과 | 설치된 CoreModule에 Graphics.DrawMeshInstanced 존재. 텍스처별 최대 1023개 배치, Matrix4x4 배열과 MPB `_InstanceTint` Vector4 배열 제출. 장치 instancing 지원·번들 로드·shader.isSupported 검사. |
| renderparams | 구현, 빌드 통과 | 설치된 CoreModule에 RenderParams와 Graphics.RenderMeshInstanced<T> 존재. 같은 행렬/색 데이터 사용. 비균일 크기의 worldToObject 저장 여유를 두어 보수적으로 최대 511개씩 제출. |
| drawindirect/컴퓨트 | 사유 로그 후 건너뜀 | RenderMeshIndirect, DrawMeshInstancedIndirect(ComputeBuffer), ComputeBuffer API 존재. 기존 셰이더는 StructuredBuffer/간접 인스턴스 인덱스 경로가 없어 동등한 데이터 업로드+그리기 시험 불가. 컴퓨트 dispatch 및 버퍼 업로드 수치 없음. |
| texturearray | 사유 로그 후 건너뜀 | Texture2DArray API는 존재하지만 기존 셰이더는 sampler2D 전용이며 배열 샘플링·인스턴스별 슬라이스 경로 없음. |

기존 `effects/DecoGpu.shader`와 내장 리소스 `StutterFix.decogpu`를 재사용한다.
소스에 `#pragma multi_compile_instancing`, `UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceTint)`와 실제 색상 읽기 경로가 있다.
일반 Shader.Find 성공이나 Material.enableInstancing 값만으로 인스턴스 색상 지원을 추정하지 않는다.
Sprites/Default 등의 인스턴싱 변형은 이번에 런타임 확인하지 않았으므로 대체 경로로 사용하지 않는다.
번들 부재/중복 로드/셰이더 미지원/제출 예외 시 이유를 남기고 종료한다. 셰이더 번들 재빌드는 하지 않았다.
간접/배열 진단 로그는 실제 실행 장치의 graphicsDeviceType, supportsInstancing, supportsComputeShaders,
supports2DArrayTextures도 기록하지만 해당 플래그만으로 그리기 성공을 주장하지 않는다.

텍스처별 인스턴스 수는 균등 분배하고 나머지를 앞쪽 텍스처에 하나씩 추가한다.
호출 수는 각 텍스처의 `ceil(실제 개수/배치한도)` 합이며 빈 텍스처는 제출하지 않는다.
N이 K로 나누어떨어지면 drawinst는 요청의 `K × ceil((N/K)/1023)`과 같다.

| N=2000 | K=1 | K=4 | K=16 | K=64 |
|---|---:|---:|---:|---:|
| drawinst 제출 호출/프레임 | 2 | 4 | 16 | 64 |
| renderparams 제출 호출/프레임 (한도 511) | 4 | 4 | 16 | 64 |

이 수치는 CPU API 제출 횟수이며 내부 GPU draw 수나 실측 성능 수치가 아니다.

## 측정과 해석

- 동일한 프레임 번호/인스턴스 번호의 결정적 함수로 위치, 회전 z, 크기 x/y, 색 rgba를 계산한다.
  K개의 1x1 텍스처를 만들고 같은 인스턴스는 모든 방식에서 같은 텍스처와 값을 사용한다.
- 메인 구간: 값 계산 + Transform/SpriteRenderer 쓰기 또는 행렬/색 배열 채우기 + MPB 업로드 + Graphics 제출.
  Stopwatch 실측이며 준비·정리·보고·배열 정렬은 측정 구간 밖이다. 정상 측정 루프에는 명시적 관리 힙 할당이 없다.
- 전체 프레임: 한 Update의 작업 시작부터 다음 Update의 수집 시점까지의 Stopwatch 간격.
  Time.deltaTime만으로 측정하지 않는다. CPU 구간과 전체 프레임 모두 평균/p99(ms), 표본 수를 기록한다.
- 일치하는 기준선(N/K/표본 수/장면/기준 카메라/기준 카메라 행렬/투영/카메라 수/해상도/vSync/FPS 제한/화질)이 있으면
  renderers 대비 메인 평균, 전체 평균, 전체 p99의 차이를 한 줄에 기록한다. 음수는 해당 시간이 감소했음을 뜻한다.
- GPU 병목 해석은 전체 프레임 차이를 참고한다. GPU 직접 타이머가 아니므로 GPU 병목을 단정하지 않는다.
  vSync/FPS 제한, 렌더 스레드, 장면 부하, 다른 카메라의 동작, 외부 부하 등은 영향을 준다.
  표시 설정이 같아도 장면 내용과 다른 카메라 상태가 완전히 동일하다는 보장은 없으므로 정적인 장면에서 비교한다.
- 로그 접두사: `[마이크로벤치] instbench ...`.

## 검증 결과와 남은 항목

요청한 빌드:

```text
dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1
```

빌드 성공, 오류 0개·경고 36개. InstBench.cs 발생 경고 없음. 산출물: `bin/PlayerAuto/StutterFix.dll`.
기존 참조 버전 충돌(MSB3277), 폐기 예정 API 및 미사용 필드 등의 경고는 남는다.
`git diff --check` 통과. MSBuild Compile 항목 조회로 일반 Player에서 실험 코드 제외 확인.
게임 DLL의 공개 메타데이터만 읽어 위 API들의 존재를 확인했으며 Unity/게임/에디터를 실행하지 않았다.

남은 검증은 실제 D3D11 장치에서 셰이더/번들 로드, 두 인스턴싱 경로의 색·행렬 및 시각 동등성,
K=1/4/16/64별 측정과 종료·취소 후 자원 정리 확인이다.
간접 그리기와 Texture2DArray는 대응 셰이더 및 번들 제작이 필요하다.
이번 결과로 Arche 270초 구간의 개선량이나 GPU 병목 여부를 결론내릴 수 없다.
