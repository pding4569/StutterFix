# tilebake 결과: 타일 이동 그림자 굽기 프로토타입

2026-10-20, `E:\StutterFixWork\wt-tilebake`, `codex/night-tilebake`.

게임 실행·설치·커밋을 하지 않았다. 기본 꺼짐인 개발 전용 수집기/평가기와 오프라인 검증을 구현했다. **크기 변경·회전 wrap·다른 쓰기 주체가 있는 타일은 전체 제외하는 부분 지원 프로토타입이다.** Lost Requiem 실게임 지원율, 화면 오차, 시작 위상, 프레임 스파이크 개선은 이번에 측정하지 않았다.

## 변경과 시간 계약

- `FloorBake.Core.cs`: Unity 객체가 없는 타일×6채널 표. `Eval(tileIndex, channel, songTime)`은 과거 평가 순서와 무관하다. 채널은 위치 X/Y, 회전, 크기 X/Y, 불투명도다. 크기 채널의 정적 초기값은 보관하지만 크기 변경 효과는 제외한다.
- `DecoBakeCore.Segment`, `Find`, `Value`, `EaseFn`을 재사용한다. 타일 전용 컴파일러가 필요한 이유는 장식의 Global/LastPosition과 다른 위치·회전 기준, `Mathf.Approximately` 생성 생략, 불투명도 길이 0 직접 대입, `FloorAnim.Apply(duration)` 완료 계산이다. 기존 장식 컴파일러와 평가기는 수정하지 않았다.
- `T0 = startTime - startEffectOffset`, `elapsed = (songTime - T0) / frozenPitch * frozenDOTweenTimeScale`. 보정값을 다시 빼지 않는다. pitch/DOTween 배율/이징 기본값이 바뀌면 중단한다. 같은 T0는 게임 최종 목록의 마지막 인스턴스가 이긴다. 정렬이 틀리면 재정렬해 숨기지 않고 거부한다.
- 위치 목표는 **게임 Decode가 확정한** `startPos + targetPos`, 회전 목표는 `startRot.z + targetRot`이다. 타일 이동에는 장식 LastPosition 같은 현재값 상대 모드가 없다. `startTile/endTile` 기준·오프셋을 다시 해석하지 않고 이미 계산된 `start/end/gapLength`를 읽는다. 역방향 범위는 뒤집고 양끝을 포함해 `1 + gapLength` 간격으로 열거한다. 반복 전개된 객체별 InstanceID를 유지하며 원본 이벤트로 합치지 않는다.
- 교체 시 이전 기록의 완료 이징값을 먼저 계산하여 새 시작값으로 삼는다. 위치·회전 길이 0은 `ZeroTween.EaseEnd` 계산, 불투명도 길이 0은 목표 직접 대입이다. NaN 위치/크기 축은 미사용이다. 회전/불투명도 NaN 목표는 제외한다. OpaOnly는 불투명도 트랙만 생성한다.
- `FloorAnim`과 달리 시작값을 첫 실제 Step까지 지연해 읽지 않는다. 다른 쓰기 주체를 제외한 뒤 승인된 곡 시각 의미로 미리 계산한다. 프레임 누적 float와 순수 곡 시각의 차이를 비트 동일성으로 주장하지 않는다.

## 수집과 패치

새 Harmony 대상은 2곳이다.

1. static `scnGame.PrepVfx(List<scrFloor>, int, List<LevelEvent>, bool)` prefix/postfix: 준비 상태를 추적하고 최종 정렬 목록 수집을 시작한다.
2. instance `scrVfxPlus.ScrubToTime(float)` prefix/postfix: PrepVfx 안의 최초 0초 scrub **직전** 타일 초기값을 읽는다. 이 시점은 `changeTrack.PrepFloor`, `startPos` 설정, `opacityVal` 적용, 효과별 PrepVfx가 끝난 뒤다. 그 밖의 임의 시각 이동은 이전 캡처를 무효화한다. 게임 scrub은 실행한 효과를 예약 목록에서 제거하므로, 수집 시작 때 목록 배열을 동결하고 postfix에서 그 정상적인 개수 변화를 반영한다. 이를 맵 편집으로 잘못 판정하지 않는다.

선언 타입·이름·인자 수/형식·반환형·static/instance·Standard/HasThis 호출 규약으로 각각 정확히 1개를 고른 뒤 설치한다. 누락/모호 시 설치하지 않는다. 패치 설치 및 `on` 초기화 실패 시 전용 소유자 ID 전체를 해제한다. `Module.ResolveSignature`를 쓰지 않았다. LoadCheck가 두 대상의 유일성을 확인했다.

실제 시작 프레임은 기존 `FloorAnim.StartPrefix`에 읽기 관찰 호출만 추가했다. 반환값, 원본 실행 여부, Rec 생성·진행·Kill·setter를 바꾸지 않는다. 타일 Transform/불투명도/렌더에 새 쓰기는 없다.

`#if DEV || AUTOTEST` 및 csproj 조건으로 일반 Player에서 두 타입을 제외한다. `tilebake off`와 `Main.UninstallAll`에서 취소 토큰, 표/기준 스냅샷/수집 참조, 캡처의 굽기 참조, 전용 패치를 정리한다. 기존 맵 정리 경로도 무효화한다. worker에는 순수 스냅샷만 넘기고 취소된 worker의 늦은 예외도 관찰한다.

`open → on`은 이미 0초 scrub이 끝났을 수 있다. 정지 상태에서 현재값을 읽되 **T0<=0 이동 효과가 닿는 타일과 기존 활성 tween 타일을 제외**한다. 이미 scrub이 목록에서 제거한 이동 효과는 floor.plusEffects의 비예약 효과 조사로 찾아 타일 전체를 제외한다. 초기 재생 위치를 보장하려면 먼저 맵을 새로 열어야 한다. 타일 0부터 한 번 준비한 뒤에는 pre-scrub 기준 스냅샷을 얻는다. 선택 타일에서 재생할 때는 같은 타일 InstanceID 목록의 기준 스냅샷을 복제해 재사용한다. 기준이 없거나 타일 객체가 바뀌면 거부한다. 알려진 scrub 이외에 이벤트 목록 객체/개수가 바뀌어도 준비를 중단한다.

초기 스냅샷은 메인 스레드에서 한 번 읽는다. 이벤트 수집은 대략 4ms 경계에서 양보하고 순수 컴파일은 worker에서 한다. 초기 읽기와 큰 단일 범위 열거는 아직 세분화하지 않았으므로 16ms 이내라고 보장하지 않는다. `bench`의 `maxSliceMs`로 실제 확인해야 한다.

## 제외와 T3 쓰기 주체

현재 형상의 T3 **53개** 목록을 [t3-writer-inventory.txt](tilebake-evidence/t3-writer-inventory.txt)에 보존했다. 서명 문자열은 증거용이며 런타임 서명 복원에 사용하지 않는다.

- `ffxFloorAppearPlus`/`ffxFloorDisappearPlus`는 연결된 `floor` 전체를 제외한다. 나타나기 효과는 위치가 같아 보이는 경우에도 실제 tween을 만들므로 생략하지 않는다. 연결 타일을 찾지 못하면 전체 맵을 제외한다.
- 그 밖의 Floor 계열, LightBridge, CallMethod/AddComponent는 영향 범위를 단정하지 않고 전체 맵을 제외한다. 예약 목록 밖의 조건/수동/타격 이동 효과도 범위를 열거해 해당 타일 전체를 제외한다.
- T3의 특수 연출 MonoBehaviour 타입이 씬에 있으면 비활성 객체까지 검사해 전체 맵을 제외한다. 씬 밖 자산이나 향후 추가될 임의 모드 writer까지 검출한다는 뜻은 아니다. scrFloor/MoveFloor 메서드의 다른 소유자 Harmony 패치도 전체 제외 사유다.
- Reset/MakeLevel/PrepVfx/초기 배치 쓰기는 기준 스냅샷 생명주기로 처리한다. scrFloor.LateUpdate의 Transform 쓰기는 자식 multiplanetLine 위치 동기화임을 정적으로 확인했다. 모든 타일을 무조건 제외하는 근거로 사용하지 않았다. 근거는 [static-game-contract.txt](tilebake-evidence/static-game-contract.txt)다.
- 크기 변경은 지연 vector Startup과 ApproximatelyXY의 축 간 결합 때문에 제외한다. 회전 목표가 `[0,360)` 밖이거나 초기 tweenRot/Transform Euler가 다르면 제외한다.
- 조건/수동/타격, 시각 설정 조건, legacy `Start` scale 설정, 사용자/미확인 ease, 비유한 초기값/원점/목표, 음수·비유한 길이는 제외한다. 범위·gap이 잘못되어 안전한 대상 집합을 정할 수 없으면 전체 준비를 중단한다.
- 자유 이동 착지 목록에 일반 listFloors 밖 객체가 들어갈 수 있다. 해당 분기는 구현하지 않고 범위가 자유 이동 구역을 만나면 전체 맵을 제외한다. 임의로 일반 타일만 굽지 않는다.

모든 사유를 먼저 전파한 뒤 컴파일한다. 나중의 미지원 효과도 타일의 모든 이전 트랙을 제외한다. 사유별 고유 타일 수/전체 타일 비율을 로그로 내고, 서로 다른 사유는 중복될 수 있다. 다른 writer/크기 효과가 많으면 지원율이 0%일 수 있다. 지원율은 실게임 확인 전이다.

## 명령과 검증 범위

`tilebake on/off/ready/verify/bench`. `ready`는 완료까지 AutoTest 단계를 유지하고 실패 상태면 예외로 중단한다. 상태를 설정 파일에 저장하지 않는다.

TileSim 테이프에 곡 시각, 실제 Unity 프레임, pitch, DOTween 배율, 카메라 viewport 높이에 따른 pixels/world, 타일 InstanceID, 굽기 표/세대, 시작 인스턴스를 기록한다. 1440 픽셀로 고정하지 않는다. 새 세대/seek/clock 역행/설정 변경은 이전 비교를 무효화하며 다른 타일 객체의 테이프와 비교하지 않는다.

`verify`는 완료된 같은 표의 프레임별 Rec 출력과 순수 평가를 비교한다. 속성별 표본 수/최대/p99/상위 8개 타일(리스트 인덱스), 위치의 world 및 px 추정값, 시작 위상 프레임 차이 빈도, 관측 불가 시작 수, 제외 사유별 비율을 출력한다. p99는 nearest-rank다. 시작 위상은 T0를 처음 지난 캡처 프레임과 실제 StartPrefix 관측 프레임의 차이다. 캡처 이전 시작은 판정하지 않는다.

표본은 기존 TileSim의 `tileIndex % 4 == 0` Rec 갱신 출력이다. 정적 타일·길이 0 직접 쓰기·진짜 DOTween 중첩·색/스타일·모든 화면 픽셀을 검증하지 않는다. 시작 위상도 실제 첫 표시 프레임과 같다는 보장은 없다. 표본이 0개이면 통과가 아니다. 직교 카메라의 위치 축 오차×pixels/world는 근사 환산이며 원근/비정상 카메라는 환산하지 않는다. 카메라 변환·두 축 합성 오차·후처리를 포함한 실제 화면 비교는 남아 있다.

제안 기준은 **위치 0.01px 이하, 시작 차이 절댓값 1프레임 이하**다. p99와 최대를 함께 보고, 드문 큰 오차를 p99만으로 감추지 않는다. 회전·크기·불투명도에는 이번에 임의 합격 기준을 만들지 않았다.

## 오프라인 검사와 숫자

`dotnet run --project tools/tilebake-fixture -c Release -- --bench`는 실제 Core와 게임 DOTween EaseManager를 링크한다. 교체, 원점 상대 오프셋, 되감기, 동일 T0 반복 순서/인스턴스, 길이 0/불투명도 직접 대입, NaN 미사용 축, Approximately, 늦은 writer 전체 제외, 크기/wrap 제외, 잘못된 길이/목표/정렬, 취소, pitch/scale 계약, 이징 중간값 **3,465개**를 검사했다.

기존 `tools/tilesim-fixture`도 새 메타데이터에 맞춰 갱신했다. lifecycle/고의 mismatch 탐지/경로 무효화와 새 시계·viewport·ID/세대·시작 stamp·역행 검사를 포함해 **17개 검사 통과**다. Unity 대신 stub을 쓰는 캡처·리플레이 계약 검사다.

이번 .NET 8 합성 1회 결과(워밍업 1회 + 전체 6채널 평가 32회, 다른 빌드 작업과 겹친 표본):

| 타일 | 효과 | 대상 참조/구간 | 굽기 ms | 입력+굽기 ms | 표 payload B | GC 전 heap 증가 B | 평가 중앙 ms | 평가 p99 ms |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 8,171 | 4,097 | 139,243 | 127.423 | 133.821 | 6,288,768 | 18,795,976 | 2.479 | 11.262 |
| 90,000 | 4,097 | 221,072 | 199.087 | 308.671 | 16,762,880 | 55,712,848 | 9.562 | 33.430 |

1개 효과는 전체 타일을 대상으로 하고 나머지 4,096개는 각각 32개 타일을 대상으로 한다. 수천 효과가 모두 9만 타일에 닿는 최악 fanout이 아니다. payload는 구간/숫자/참조의 대략적인 합으로 객체 header·임시 list·중복 기준 스냅샷·GC 비용을 포함하지 않는다. heap 변화는 합성 입력/임시 할당도 포함하며 상주 메모리나 peak 측정이 아니다. 런타임에는 타일 10만/효과 30만/대상참조 400만/구간 400만 상한을 둔다. 상한은 OOM 방지의 보장이 아니다. 100만 타일 맵은 열지 않았다.

이 수치는 Lost Requiem 측정도 Unity Mono 처리량도 아니다. 32개 평가의 p99는 사실상 표본 최대이며 비교 실험이나 안정적인 성능 결론이 아니다. [fixture.txt](tilebake-evidence/fixture.txt)에 원문과 checksum을 보존했다. `tilebake bench`도 동일 합성을 별도 표시하고, 실수집 표의 굽기/수집/메모리/평가 숫자와 섞지 않는다.

빌드/LoadCheck:

| 구성 | Release 빌드 | 새 타입 | LoadCheck FieldRef 오류 | 새 후크 정확일치 |
|---|---|---|---:|---|
| 기본 DEV | 오류 0, 경고 39 | 있음 | 0/266 | 각각 1 |
| Edition=Player | 오류 0, 경고 4 | 없음 | 0/229 | 설치 경로 없음 |
| Player + AutoTestBuild=1 | 오류 0, 경고 39 | 있음 | 0/266 | 각각 1 |

기존 어셈블리 충돌/obsolete/미사용 경고가 있으며, 새 코드에도 Object.FindObjectsOfType obsolete 경고 1개가 있다. LoadCheck는 정적 형식/서명 검사이며 Mono에서 Harmony 설치·해제의 실행 증명은 아니다. 이름을 실행 중 결정하는 FieldRef 5곳은 기존 도구의 실행 중 확인 대상으로 남는다. 로그는 [tilebake-evidence](tilebake-evidence/)에 있다.

## 효과 시작 비용에 대한 근거와 한계

기존 531.8초의 8,171개 타일 시작 비용은 인계/기존 optimization-36 기록이다. 이번 실측으로 재현하지 않았다.

순수 평가기는 맵 준비 때 모든 시작/목표를 굽고, 각 타일×채널을 곡 시각으로 이진 탐색한다. 효과 시작 순간에 Rec·클로저·delegate·타일 범위 열거를 새로 만들 필요가 없는 구조다. 현재 구현은 전역 이벤트 인덱스 하나를 진행시키는 런타임을 추가한 것이 아니라 **시작 콜백 자체가 필요 없는 조회 표**다. 전체 평가 비용은 선택된 타일×채널 수와 트랙 길이에 비례하며 공짜가 아니다.

그림자 모드에서는 기존 FloorAnim이 그대로 실행되므로 시작 비용은 사라지지 않는다. 관찰/수집/검증 비용도 더해진다. 소유권 이전, Transform 쓰기, 활성 타일 집합 관리, 기존 경로 우회는 구현하지 않았다. 따라서 22~25ms 제거·FPS 향상·제품 채택을 주장할 수 없다.

## 사용자가 실행할 순서

이번에는 실행하지 않았다. 기존 `seek 524 8`은 약 516초 타일을 선택한다. 바로 4초 캡처하면 531.8초가 빠질 수 있고, 이미 재생 중이면 seek는 재생 위치를 옮기지 않는다. AutoTest 코드 확인에 따른 보완 순서:

```text
open D:\얼불춤 맵 파일\Lost Requiem\Ludicin-Lost-Requiem.adofai
tilebake on
auto on
play
tilebake ready
stop
seek 524 8
tilebake ready
waitsongpos 530 40
tilesim capture 4
wait 4.5
tilebake verify
stop
tilebake bench
tilebake off
```

타일 0 최초 준비 → 정지 → 선택 타일 재생의 기준 스냅샷 재사용 경로는 정적으로 구현했으나 게임에서 검증하지 않았다. 첫 준비/재생이 0초 기준을 제공하지 않으면 명시적으로 중단한다. 준비 로그에서 지원 타일 수/비율을 먼저 확인한다. 전체 제외나 표본 부족이면 캡처 결과로 정확성을 결론 내리지 않는다. 다시 맵을 열거나 타일 객체가 재생성되면 같은 ID 검증 때문에 기준을 새로 얻어야 한다. 두 번째 stop이 재준비를 유발하면 bench는 그 재준비된 현재 표를 평가하며, verify는 stop 전에 완료해야 한다.

## 에디터 미리보기 API 초안 — 설계만

```csharp
// 제안 인터페이스. 이 이름의 제품/에디터 API는 구현하지 않았다.
float Eval(int tileIndex, TileChannel channel, double songTime);
void EvalRange(int firstTile, int count, double songTime,
               Span<TilePose> destination, out PreviewCoverage coverage);
```

표·세대·고정 pitch/timeScale·초기값을 하나의 불변 PreviewSnapshot으로 묶는다. 동일 snapshot/time 입력에는 같은 값을 반환하고 임의 점프/되감기에서 상태 누적을 하지 않는다. 제외 타일은 초기값을 정상 미리보기로 가장하지 않고 coverage/사유로 반환한다. EvalRange는 여러 채널을 같은 시각/세대에서 읽으며 쓰기·렌더 반영은 별도의 에디터 계층이 맡는다.

이벤트 편집 시 InstanceID 대신 에디터의 안정적인 event ID + 반복 인스턴스 키를 사용한다. 목표/길이/이징/시간 편집은 옛·새 대상 범위 합집합의 채널을 무효화하고, 교체 체인 때문에 해당 타일의 첫 변경 이벤트부터 뒤쪽 트랙을 다시 굽는다. 삭제/같은 T0 순서 변경도 후속 시작값에 전파한다. 제외 사유가 추가·제거되면 타일의 모든 채널을 재검토한다.

타일 추가/삭제, 상대 범위 기준 변경, tempo/entryTime, 반복 전개, 초기 배치, pitch/timeScale, writer·조건 변경은 의존 범위를 다시 계산하며 안전한 범위를 모르면 전체 재굽기한다. worker는 Unity 참조 없는 스냅샷만 받고 완성된 세대를 원자적으로 교체한다. 오래된 작업은 취소하며 미리보기는 서로 다른 세대의 결과를 섞지 않는다. 부분 재굽기·에디터 연결·미리보기 setter는 이번에 구현하지 않았다.
