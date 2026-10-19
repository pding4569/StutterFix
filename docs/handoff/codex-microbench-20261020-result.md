# 게임 안 마이크로벤치 구현 결과 (2026-10-20)

## 범위와 검증 결과

- 작업 폴더/브랜치: `E:\StutterFixWork\wt-animbench`, `codex/night-animbench`.
- `MicroBench.cs` 하나 추가, `AutoTest.cs` 명령 2개 및 종료 시 취소 연결, `StutterFix.csproj` 조건부 Compile 항목 1개 추가.
- 새 C# 코드는 모두 `#if DEV || AUTOTEST` 안에 있다. 새 Harmony 패치 및 곡 재생 로직 연결 없음.
- `dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1` 성공: **오류 0개, 경고 36개**. 출력 `bin/PlayerAuto/StutterFix.dll`.
- `dotnet build -c Release -p:Edition=Player` 성공: **오류 0개, 경고 36개**. 출력 `bin/Player/StutterFix.dll`.
- 경고는 참조 버전 충돌(MSB3277), 기존 API 사용 중단 및 사용하지 않는 필드/변수 등이며 `MicroBench.cs` 경고 없음.
- 저장소 `tools/ilscan`으로 DLL 형식 정의 확인: AutoTest에는 `StutterFix.MicroBench` 포함, 일반 Player에는 해당 형식 0개. 일반 Player의 다른 형식 목록도 읽어서 빈 검색 결과의 유효성을 확인했다.
- **게임 실행·설치·커밋은 하지 않았다. 성능 수치 및 런타임 정상 동작은 아직 미검증이다.**

## 실행 방법

사용자가 AutoTest DLL을 설치한 뒤, 카메라가 켜진 장면에서 곡을 멈춘 상태로 기존 `autotest.txt` 명령을 사용한다. 각 명령은 시험과 정리가 끝날 때까지 다음 명령 진행을 기다린다. 예:

```text
keep
timeout 600
animbench 2000 300 main
animbench 2000 300 mainsep
animbench 2000 300 job
animbench 2000 300 jobrot
animbench 2000 300 anim
matbench 20000 300
```

`keep`은 기존 AutoTest의 묶음 완료 후 게임 종료를 막는 명령이다. `timeout 600`은 느린 장면에서 matbench의 9개 방식이 기존 단계 제한 180초에 걸리는 것을 피하기 위한 예시다. 더 긴 측정에는 제한도 맞춰 늘린다. 이 문서의 명령 파일을 실제 게임 폴더에 생성하지 않았다.

- `animbench [N=2000] [프레임수=300] [방식=main]`
- `matbench [N=20000] [프레임수=300]`
- 프레임수는 워밍업을 제외한 **방식별 측정 프레임수**다. 워밍업은 각 방식 60프레임.
- 양의 정수만 허용: animbench N 최대 50000, matbench N 최대 1000000, 프레임수 최대 10000. 잘못된 인수는 이유를 로그로 남기고 실행하지 않는다.
- 시작 시 `Hitch.Playing`이면 거부한다. 도중 재생 시작/활성 장면 변경 시 중단하며, animbench는 활성 카메라가 없거나 사라져도 거부/중단한다.
- 정상 종료, 실행 중 예외, 컴포넌트 비활성화/파괴, AutoTest 종료/타임아웃 시 job을 완료하고 graph/NativeArray/TransformAccessArray/Material/Sprite/Texture 및 임시 부모와 자식을 정리한다. `Destroy` 다음 프레임부터 다음 명령으로 진행한다.

## animbench 구현과 로그 해석

현재 장면에 원점·회전 없음·크기 1인 임시 부모 하나와 SpriteRenderer 자식 N개를 만든다. 1×1 흰 텍스처/스프라이트를 공유하며, 캐시된 Transform 배열에 결정적인 x/y 위치, z 회전, x/y 크기를 프레임마다 쓴다. 실제 게임 장식은 건드리지 않는다.

| 방식 | 작업 |
| --- | --- |
| `main` | 메인에서 `localPosition`, `localRotation`, `localScale` 쓰기 |
| `mainsep` | 메인에서 월드 `position`, `rotation`, `localScale` 쓰기 |
| `job` | 메인에서 NativeArray 값과 Quaternion 준비, Update에서 Transform job 예약·flush, LateUpdate에서 Complete |
| `jobrot` | 위와 같되 Quaternion.Euler 계산을 job 안에서 수행, Burst 없음 |
| `anim` | Animator.BindStreamTransform + AnimationScriptPlayable/IAnimationJob, 수동 graph.Evaluate를 LateUpdate에서 수행 |

모든 방식은 같은 결정적 입력을 사용한다. 부모가 identity이므로 main/mainsep의 로컬/월드 좌표 조건이 같다. 준비 경로도 NativeArray를 사용하므로 main의 쓰기 비용에는 배열 읽기 비용이 포함된다. 자식이 하나의 부모 트리를 공유하는 조건은 실제 게임의 여러 부모 트리와 병렬성이 다를 수 있다.

로그 접두사는 `[마이크로벤치]`. 출력의 시간 쌍은 **평균/p99**이며 p99는 정렬 후 nearest-rank 방식이다.

- `쓰기 ... ms/프레임`: main/mainsep은 세터 루프, job/jobrot은 Schedule와 ScheduleBatchedJobs 구간. anim의 실제 쓰기는 Evaluate에 포함된다.
- `준비`: 위치·크기·각도 입력 채우기 및 해당 방식의 메인 Quaternion 계산.
- `Complete/Evaluate`: LateUpdate에서 실제 대기/수동 애니메이션 평가에 쓴 시간.
- `메인 합계`: **프레임별 준비 + 쓰기 + Complete/Evaluate**의 평균/p99. job이 메인 비용을 줄이는지는 이 값으로 비교한다. 각 항목의 p99를 더한 값이 아니다.
- `전체 프레임`: 해당 Update 시작부터 다음 Update 시작까지 Stopwatch 실측. 렌더·대기·다른 게임 작업도 포함하며 GPU 실행 시간이나 job worker CPU 시간을 따로 재는 지표가 아니다.

job/jobrot은 첫 워밍업에서 Complete 전 `targets[0].localPosition` 읽기를 1회 시험한다. 읽기 전후 IsCompleted, 읽기 시간, 이후 Complete 대기, 읽힌 값/예외, 목표 값, 완료 후 값을 한 줄로 남긴다. 시험/로그 시간은 측정 표본에 들어가지 않는다. 이미 job이 끝났거나 너무 짧은 표본이라면 강제 동기화 여부를 단정할 수 없다. 실제 출력으로 해석해야 한다.

카메라는 존재 여부만 확인하며 위치·투영·culling mask는 변경하지 않는다. 모든 스프라이트가 실제 화면에 보이는 조건을 원하면 사용자가 카메라를 배치해야 한다. 비교할 때 장면/카메라/FPS 제한/VSync/외부 부하를 동일하게 둔다.

## matbench 구현과 로그 해석

`Sprites/Default`, 없으면 `Hidden/Internal-Colored`로 Material 하나를 만든다. 셰이더 메타데이터에서 실제 선언된 Float/Range와 Color 프로퍼티를 하나씩 찾고 이름/ID를 캐시한다. 셰이더나 적절한 프로퍼티가 없으면 이유를 남기고 정리한다.

9개 방식을 순서대로 각각 워밍업 60프레임 + 요청 프레임수 동안 측정한다:

1. SetFloat(string), 같은 값 연속
2. SetFloat(id), 같은 값 연속
3. SetFloat(string), 호출마다 변하는 값
4. SetFloat(id), 호출마다 변하는 값
5. SetColor(string), 같은 값 연속
6. SetColor(id), 같은 값 연속
7. SetColor(string), 호출마다 변하는 값
8. SetColor(id), 호출마다 변하는 값
9. SetFloat(string), 없는 프로퍼티에 변하는 값

변하는 값은 1024개 값의 주기로 반복한다. 없는 프로퍼티는 `HasProperty`로 부재를 확인한 `_StutterFixMicroBenchMissing`이다. 루프 안 delegate/프로퍼티 탐색/관리 힙 할당은 없다. 값 생성과 루프 자체의 비용은 포함된다.

출력 `µs/호출`은 프레임별 **N회 배치 시간/N**의 평균/p99다. 개별 호출 지연의 p99는 아니다. 같은 값 조건끼리 또는 변하는 값 조건끼리 string−id 차이를 비교하면 문자열 처리 추가 비용의 근삿값을 볼 수 있다. id 경로에도 관리/네이티브 경계 및 루프 비용이 있어 순수 네이티브 쓰기 비용만 분리했다고 볼 수는 없다. 없는 프로퍼티 호출의 경고/네이티브 동작도 런타임 확인 대상이다. Material은 렌더러에 연결하지 않아 이 벤치는 Material CPU API 호출 비용을 잰다.

## API 확인 및 남은 제한

- 로컬 게임의 `UnityEngine.CoreModule.dll`/`UnityEngine.AnimationModule.dll` 기존 참조로 NativeArray, Unity.Jobs, TransformAccessArray, IJobParallelForTransform 및 AnimationScriptPlayable/TransformStreamHandle이 모두 컴파일됐다. 새 어셈블리 참조는 필요하지 않았다.
- Managed 폴더에 `Unity.Collections.dll`도 존재한다. 이번 코드의 NativeArray API는 기존 프로젝트 참조만으로 해석된다.
- API 부재로 생략한 방식은 없다. 다만 Animator가 이 임시 계층에서 실제로 값을 쓰는지는 런타임 문제이므로 `anim` 첫 워밍업에 모든 자식의 위치/회전/크기를 읽어 허용 오차 내 결과를 검사한다. 실패/예외면 `지원 불가` 이유를 로그에 남기고 해당 명령을 종료한다.
- 컴파일 성공은 job 스케줄링, Animator 바인딩, 실제 카메라 렌더, 성능 개선, 동기화 동작의 게임 내 검증을 대신하지 않는다. 이 항목들은 사용자의 실행 결과가 필요하다.

빌드 원문 로그는 무시되는 `obj/microbench-autotest-build.log`, `obj/microbench-player-build.log`에 있다.
