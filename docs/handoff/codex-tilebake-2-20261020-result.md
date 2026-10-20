# tilebake-2 결과: 타일 크기 지원과 오프라인 후보 비율

2026-10-20, `E:\StutterFixWork\wt-tilebake`, `codex/night-tilebake`.

크기 X/Y 트랙을 구현했다. 그림자 모드와 기존 실행 경로를 유지했다. 게임 실행·설치·커밋은 하지 않았다. 새 Harmony 패치와 `Module.ResolveSignature` 사용은 없다. Player 컴파일 제외를 유지했다.

크기 효과라는 이유만으로 전체 타일을 제외하던 `scale-vector-startup`을 제거했다. 실제 지원률·화면 오차·531.8초의 시작 비용 개선은 측정하지 않았다. **파일 기준 후보 상한은 Lost Requiem 21.092%, HELLO 8.410%이며 실제 지원 확정값이 아니다.**

## 타일 코드로 확정한 의미

장식 구현을 근거로 타일 의미를 추정하지 않았다. 이 체크아웃에는 참고용 `codex-bake-3-20261020.md` 임무 문서만 있고 해당 `-result.md`는 없다. 대신 아래 타일 구현과 설치된 게임 DLL을 직접 읽었다. 디컴파일 발췌는 [증거 폴더](tilebake-2-evidence/)에 보존했다.

| 항목 | 확정한 의미와 근거 |
|---|---|
| 목표 | `ffxMoveFloorPlus.Decode`: `targetScaleV2 = scale / 100f`. 위치·회전과 달리 시작 배치에 더하지 않는 절대 localScale 목표다. `scaleUsed=false`이면 미사용이며 각 축 NaN은 그 축의 Kill/생성/쓰기를 모두 생략한다. |
| 초기 크기 | `scnGame.MakeLevel`이 설정한 `startScale`, `scrFloor.ResetToLevelStart`의 `transform.localScale=startScale`가 초기 배치다. 자유 이동 생성 타일은 Reset 예외가 있으므로 기존 제외를 유지한다. Snapshot은 실제 pre-scrub localScale X/Y를 읽는다. |
| 읽기 시점 | `StartEffect.TweenFloor`/`FloorAnim.TweenFloor`는 크기 처리 진입 시 XY 벡터를 한 번 저장한다. 실제 양의 길이 시작값은 `FloorAnim.Startup`의 첫 Step, 또는 첫 Step 전 `Complete`가 호출하는 Startup에서 `Transform.localScale`을 읽는다. `VEnd`는 생성 시 확정한다. |
| 축별 순서 | 저장한 XY → ScaleX `Kill(true)` → 저장 벡터 `WithX(targetX)`와 **현재** XY 비교 → X 생성/즉시 쓰기 → ScaleY `Kill(true)` → 처음 저장한 벡터 `WithY(targetY)`와 현재 XY 비교 → Y 생성/즉시 쓰기. |
| 비교/쓰기 | `RDUtils.ApproximatelyXY`는 X와 Y 각각 `Mathf.Approximately`의 AND다. `ExtensionMethods.WithX/WithY`는 지정 축만 바꾸고 나머지 축을 보존한다. `FloorAnim.Apply`/Vector3Plugin 축 제한은 getter의 현재 벡터 중 **자기 축만** `start + change * ease`로 바꾼다. |
| 교체 | 같은 축의 이전 트랙을 먼저 완료한다. `FloorAnim.Prev → Complete → Startup(미시작일 때) → Apply(duration)`. 이후 비교·새 시작값은 완료 이징 계산값이며 단순 목표 대입으로 바꾸지 않았다. 반대 축의 완료는 해당 축의 시작/출력을 바꾸지 않는다. |
| 길이 0 | `FloorAnim.ZeroScale`은 당시 getter를 시작으로 읽고 자기 축만 `start + (target-start)*ZeroTween.EaseEnd`로 즉시 쓴다. X 즉시 쓰기는 뒤의 Y 생성 여부에도 영향을 준다. 불투명도 길이 0의 직접 목표 대입 규칙은 별도로 유지한다. |

예를 들어 X=1,Y=1에서 길이 0으로 X=2,Y=1.0000005를 주면, X 쓰기 후 현재 X=2와 저장 X=1이 달라 Y도 생성된다. Y만 비교하면 생략할 작은 차이도 실제로 적용된다. 양의 길이 X는 생성 직후 쓰지 않으므로 같은 조건에서 Y가 생략된다. fixture에서 두 경우를 구분했다.

## 구현 범위와 제외

`FloorBake.Core.cs`는 기존 6채널의 크기 슬롯 3/4에 세그먼트를 생성한다. 곡 시각 기반 무상태 평가, 같은 T0의 입력 순서, `elapsed=(songTime-T0)/pitch*timeScale`, 이징 함수·이진 탐색 재사용을 유지했다. 다른 writer가 없는 축의 지연 Startup을 이전 완료값으로 미리 계산하는 기존 시간 계약을 사용한다. 실제 프레임 누적값과 비트 동일하다고 주장하지 않는다.

Y가 완료 시작값과 **Approximately이지만 정확히 같지는 않고**, 같은 효과가 X도 지정하며 이전 양의 길이 X 트랙이 있으면, 저장 X의 마지막 프레임 값에 따라 Y 생성 여부가 달라질 수 있다. 시각만으로 결정하지 않고 `scale-cross-axis-frame-dependent-skip`으로 타일 전체를 제외한다. 예정상 X가 끝났어도 최종 Step이 아직 적용되지 않았을 가능성을 보수적으로 포함한다. Y가 정확히 같으면 강제 생성 여부가 유한 이징의 값에 영향을 주지 않으므로 지원한다. 선행 양의 길이 X가 없을 때의 X 길이 0 쓰기와 Y 비교는 직접 모델링했다.

크기 변화량/길이 0 결과가 비유한 경우 `scale-arithmetic-nonfinite`로 전체 타일을 제외한다. 컴파일 중 뒤늦게 제외한 타일도 모든 이전 채널 트랙을 비우고 최종 세그먼트 수를 다시 센다. legacy `Start`의 `targetScale` 덮어쓰기, 다른 writer, 조건/수동, 자유 이동, 회전 wrap 등 기존 제외를 유지한다.

`tilebake verify`에는 크기 X/Y 절대 오차에 더해 상대 오차 `abs(baked-Rec)/max(abs(Rec),1e-6)`의 n/max/p99/상위8 타일을 추가했다. 분모가 1e-6 미만인 표본 수도 출력한다. 타일/스프라이트 원래 치수·부모 변환을 수집하지 않으므로 크기 픽셀 환산은 **미정**으로 명시했다. localScale 오차에 위치용 pixels/world만 곱해 픽셀 오차로 가장하지 않는다. 표본은 기존 `tileIndex%4==0` Rec 출력이며 정적 타일·길이 0 직접 쓰기·모든 화면 픽셀을 검증하지 않는다. n=0은 통과가 아니다.

## 오프라인 검증

[fixture 로그](tilebake-2-evidence/fixture.log): DOTween 35개 이징 × 중간 시각 99개 × 크기 2축 = **6,930개**, 실제 DOTween EaseManager와 Vector3Plugin 축 산식의 float 결과를 비트 단위로 비교해 차이 0. 기존 위치 중간값 **3,465개**도 차이 0.

크기 X/Y 각각 교체, 한 축 NaN/양 축 NaN, 첫 Step 전 같은 T0 교체, 임의 seek/rewind, 길이 0과 비표준 ZeroK, XY 결합 비교의 생성/생략 차이, 프레임 의존 제외 시 이전 트랙 전부 제거, 변화량 overflow를 확인했다. 531.8초·1초 길이·6채널 기준값 복귀의 **합성 8,171타일**은 49,026세그먼트를 생성하고 모든 타일의 크기 중간/끝값을 확인했다. 이는 실맵 지원 타일 8,171개라는 증거가 아니다.

[추정 도구](../../tools/optimization/tilebake-support-estimate.py)는 파일을 읽고 stdout만 출력한다. 설치·게임 실행·맵 파일 수정은 없다. IDFromTile의 ThisTile/Start/End·clamp, 역범위/양끝 포함/gap, floor별 AnimateTrack 상속과 생략 필드 유지, Floor 반복의 `executeOnCurrentFloor` 대상 원점 이동, 조건 태그와 고유 타일 합집합을 self-test로 확인했다. Beat 반복은 범위를 늘리지 않으며 시간은 복원하지 않는다. 미지의 범위는 전체 타일 미정으로 처리한다. 100,000타일/80MB 상한을 두었다.

```powershell
py -3 tools/optimization/tilebake-support-estimate.py --self-test
py -3 tools/optimization/tilebake-support-estimate.py
dotnet run --project tools/tilebake-fixture -c Release
```

## 오프라인 지원률 표

정확한 지정 파일 두 개만 읽었다. 파일 SHA256·전체 사유별 표·대형 되돌리기 목록은 [support-estimate.md](tilebake-2-evidence/support-estimate.md)에 있다. normal custom / High / `remakeFloors=true` 가정이다. 사유별 타일 집합은 중복되므로 행을 더하지 않는다. geometry 항목 수에 시작 타일 1개를 더한 전체 listFloors 규모다.

| 구분 | Lost Requiem (15,001개) | HELLO (14,721개) |
|---|---:|---:|
| FloorAppear writer 제외 추정 | 10,785 / 71.895% | 13,455 / 91.400% |
| FloorDisappear writer 제외 추정 | 11,400 / 75.995% | 5,913 / 40.167% |
| 제외 합집합 | 11,837 / 78.908% | 13,483 / 91.590% |
| **후보 상한** | **3,164 / 21.092%** | **1,238 / 8.410%** |
| 크기 채널 노출 (제외 사유 아님) | 11,032 / 73.542% | 6,128 / 41.628% |
| 크기 XY 프레임 의존 근접 비교 미정 | 11,032 / 73.542% | 5,423 / 36.839% |
| 회전 wrap/초기 Euler 미정 | 8,790 / 58.596% | 239 / 1.624% |
| hitbox 수동 태그 결합 미정 | 411 / 2.740% | 5,428 / 36.872% |
| 파일에서 찾은 조건/시각설정/미지 ease/잘못된 길이 제외 | 각각 0 | 각각 0 |
| 파일상 자유 이동 이벤트 | 0 | 0 |
| 초기값·기존 tween·씬·외부 패치·clock 미정 | 15,001 / 100% | 14,721 / 100% |
| 확정 지원률 | **미정** | **미정** |

FloorAppear/Disappear 추정 근거는 `scnGame.MakeLevel`의 animationType 상속 → `changeTrack.animationType/animationType2` → `ffxChangeTrack.PrepFloor`의 None 이외 AddComponent → `FloorBake.ExcludeWriter`의 연결 타일 전체 제외다. FloorAppear가 단순 Fade라도 현재 코드는 제외한다. 파일의 AnimateTrack 자체를 임의 타일 이동 이벤트로 취급하지 않았다.

회전은 `startRot.z + targetRot`와 초기 Transform Euler/tweenRot 비교가 필요하므로 회전 목표 0도 파일만으로 지원을 확정하지 않는다. HELLO의 239개에는 한 바퀴 밖 rotationOffset 노출이 있지만 최종 rotation-wrap 제외 수와 동일시하지 않는다. 크기 미정 집합도 **근접 비교 조건 발생 수가 아니라**, 두 축 목표가 있어 확인이 필요한 보수적 노출 집합이다. tag가 있다고 모두 수동 제외하지 않고, 실제 hitbox 태그 결합은 미정으로 뒀다. 파일의 자유 이동 이벤트 0도 런타임 generated/free-roam 참조 부재의 증명은 아니다.

### 531.8초 대형 되돌리기 형태

지정 Lost Requiem 파일에서 floor **14926**, 범위 **6830(Start)..15000(End)**, gap 0의 이벤트를 찾았다. 범위는 정확히 **8,171개**이고 위치 (0,0), 회전 0, 크기 (100,100)%, OutElastic이다. **파일의 duration은 32비트, opacity는 245%(목표 2.45)**다. 1초는 Decode의 `32*crotchet` 결과일 수 있으나 이 도구는 tempo/entryTime을 복원하지 않으므로 531.8초 시각·1초 길이를 새로 확인했다고 쓰지 않는다. 불투명도 목표 1이라는 인계 설명도 이 파일 값과 다르다.

크기 목표 자체는 이제 지원한다. 그러나 이 이벤트가 닿는 타일은 다른 전체 타일 제외와 합쳐 판단하므로 **파일 기준 후보는 2,202/8,171개(26.949%)**다. 나머지 5,969개는 다른 writer 때문에 제외 추정이다. 2,202개도 런타임 미정 조건을 통과해야 한다. 따라서 “531.8초 효과 전체가 굽기 대상”이라는 결론은 아직 성립하지 않는다. HELLO에는 같은 위치0/회전0/크기100% 형태가 없었다.

### 다음에 풀 사유 상위 5개

지원 확대 순서는 제안만 하며 이번에 기존 경로를 바꾸지 않았다. 단독 증가 수는 다른 **파일상 제외**를 고정한 후보 상한 증가량이며 실지원 증가량은 아니다.

| 우선 | 사유와 코드 근거 | Lost Requiem | HELLO |
|---:|---|---:|---:|
| 1 | 나타나기 writer 소유권/채널 충돌 모델: `FloorBake.ExcludeWriter`, `ffxChangeTrack.PrepFloor`, `FloorAnim.RealOverlap` | 노출 10,785, 단독 +437 | 노출 13,455, 단독 +7,570 |
| 2 | 사라지기 writer 모델: 같은 제외 함수와 `ffxFloorDisappearPlus` | 노출 11,400, 단독 +1,052 | 노출 5,913, 단독 +28 |
| 3 | 회전 Euler wrap 모델: `FloorBake.Snapshot`, `FloorBakeCore.Compile`의 rotation-wrap | 미정 노출 8,790 | 미정 노출 239 |
| 4 | hitbox/조건 수동 실행 순서: `scnGame.ApplyEvent`, `FloorBake.Conditional`, 비예약 효과 검사 | 미정 노출 411 | 미정 노출 5,428 |
| 5 | 크기 근접 Y의 XY 결합 비교: `FloorAnim.TweenFloor`, `FloorBakeCore.Compile`의 새 제외 | 미정 노출 11,032 | 미정 노출 5,423 |

두 writer를 함께 풀면 현재 파일상 제외 합집합 11,837/13,483개를 재검토할 수 있지만, 모든 타일이 지원된다는 뜻은 아니다. 3~5번은 미정 집합이므로 증가량을 숫자로 확정하지 않았다. 초기값·씬·외부 패치 등 전역 런타임 검증도 별도로 필요하다.

## 빌드와 LoadCheck

| 구성 | Release 빌드 | FloorBake/Core | LoadCheck |
|---|---|---|---|
| 기본 DEV | 오류 0, 경고 39 | 있음/있음 | FieldRef 266곳, 틀림 0, 기존 후크 각각 1 |
| `-p:Edition=Player` | 오류 0, 경고 4 | 없음/없음 | FieldRef 229곳, 틀림 0 |
| `-p:Edition=Player -p:AutoTestBuild=1` | 오류 0, 경고 39 | 있음/있음 | FieldRef 266곳, 틀림 0, 기존 후크 각각 1 |

로그는 [tilebake-2-evidence](tilebake-2-evidence/)에 있다. 기존 어셈블리 충돌·obsolete·미사용 경고가 남는다. LoadCheck는 정적 형식/서명 검사이며 Harmony 설치·해제의 실게임 실행 증명이 아니다. 이름을 실행 중 결정하는 기존 FieldRef 5곳도 실게임 확인 전이다. `git diff --check` 통과.

## 내가 돌릴 순서

아래는 사용자가 이후 실행할 순서이며 이번 작업에서는 실행하지 않았다. 기존 tilebake 순서를 유지했다.

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

준비 로그에서 지원 타일 수와 `scale-cross-axis-frame-dependent-skip`을 포함한 사유별 제외 수부터 확인한다. verify는 stop 전에 실행한다. 같은 세대·동일 타일 ID·고정 pitch/timeScale 테이프인지 확인하고, 위치 world/px·회전·불투명도·시작 위상에 더해 **크기 X/Y 절대·상대 오차 n/max/p99/top8 및 nearZero**를 기록한다. 크기px는 이번에 미정이다. 표본 0·전체 제외는 정확성 통과로 취급하지 않는다. 기존 위치 제안 기준 <=0.01px, 시작 차이 절댓값 <=1프레임을 유지하며 크기 합격 기준은 새로 지어내지 않았다.

그림자 모드이므로 기존 FloorAnim과 시작 비용은 그대로 남는다. 22~25ms 제거·FPS 향상·전체 곡 정확성·제품 채택을 이번 결과로 주장하지 않는다.
