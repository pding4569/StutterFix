# bake-2 결과: 곡 시각 기반 그림자 굽기 프로토타입

2026-10-20, `E:\StutterFixWork\wt-bake`, `codex/night-bake`.

게임 실행·설치·커밋은 하지 않았다. 기본 꺼짐인 개발 전용 컴파일러, 평가기, `decobake` 명령과 오프라인 검사를 추가했다. **제품 채택·렌더 변경·소유권 이전은 하지 않았다.** 실게임 오차·지원율·Arche 비용은 미측정이다.

## 구현과 수집 계약

- `DecoBake.Core.cs`: Unity 참조 없는 입력 스냅샷, 장식×16개 scalar 채널의 구간 표, 이진 탐색 평가기. 원본 실행 순서를 재정렬하지 않는다. 같은 T0에서는 마지막 인스턴스가 이긴다. 다음 구간의 T0가 이전 구간의 배타적 cutoff 역할을 한다. 이전 duration은 보존한다.
- `DecoBake.cs`: `decobake on/off/ready/verify/bench`, 초기값 수집, 최종 목록 수집, worker 컴파일, 오차 통계. `#if DEV || AUTOTEST`와 csproj 조건으로 일반 Player에서 두 타입 모두 제외한다. 설정 파일에 활성 상태를 저장하지 않는다.
- 새 Harmony 대상은 **2곳**이다. `scrDecoration.Setup(LevelEvent, out bool, bool)` postfix에서 각 장식의 생성/재설정 완료 직후 값을 읽고, static `scnGame.PrepVfx(List<scrFloor>, int, List<LevelEvent>, bool)` postfix에서 최종 목록 수집을 예약한다. `PrepVfx` 내부 `ScrubToTime(0)` 이후 값을 초기값으로 재사용하지 않는다.
- 선언 타입, 이름, 인자 수와 형식, 반환형, static/instance, `Standard`/`HasThis` 호출 규약으로 각각 정확히 하나를 찾은 뒤 패치한다. 둘 중 하나가 없거나 모호하면 패치를 시작하지 않는다. 패치 도중 실패하면 전용 Harmony ID 전체를 해제한다. Mono 경로에 metadata-token 기반 메서드/서명 복원을 추가하지 않았다.
- 실제 시작 프레임은 **이미 있는 `FastMove.Prefix`**에서 관찰한다. 자체 검증의 재진입/bypass는 제외한다. 별도 `StartEffect` 패치는 없다. 관찰 함수는 원본 실행 여부나 반환값을 바꾸지 않는다.
- 새 맵 초기화는 기존 `FastMove.ForgetLevel` 경로를 재사용한다. `Main.UninstallAll`에서 `DecoBake.Shutdown`, `DecoSim.Shutdown`을 호출한다. worker 취소, 스냅샷 참조 해제, 캡처 종료, 전용 패치 제거를 수행한다. worker 입력에는 Unity 객체가 없다.

`ffxMoveDecorationsPlus` 인스턴스에서 이미 확정된 `startTime`, `startEffectOffset`, `duration`, `degreeOffset`(angleOffset/반복 오프셋 반영 결과), ease와 매개변수, 목표값과 배치 기준을 읽는다. 대상은 실제 `GetTaggedDecorations(IEnumerable<string>)`를 열거한다. 빈 태그·중복·태그 순서를 자체 구현하지 않는다. `scrVfxPlus.instance.effects`의 최종 순서를 유지하고, 반복 전개된 객체마다 instance ID를 보존한다. `sourceLevelEvent`로 합치지 않는다.

조건/수동/타격 효과는 예약 목록 밖에 있으므로 `floor.plusEffects`도 조사하여 가능한 대상 장식에 제외 사유를 붙인다. 먼저 모든 제외 사유를 전파한 뒤 컴파일하므로, 나중의 미지원 효과 하나도 그 장식의 앞부분까지 전부 제외한다. 사유별 장식 수는 각 사유 내 고유 개수이며, 서로 다른 사유끼리는 중복될 수 있다.

## 지원과 보수적 제외

현재 지원은 초기 배치가 **Global**인 이미지 장식의 위치 X/Y, 피벗 X/Y, 회전, 색 RGBA, 불투명도, 시차 배율 X/Y, 시차 오프셋 X/Y다. 목표 좌표/비율은 Decode가 변환한 값을 그대로 사용한다. 위치 목표는 Global의 `startPos + targetPos` 또는 LastPosition의 `직전현재값 + targetPos`다. 이동 고정 플래그가 있으면 기존과 같이 위치/회전/크기/피벗/시차 오프셋 블록을 건너뛰고 색·불투명도·시차 배율은 별도로 처리한다.

지원 안 함 사유:

- 크기 변경: 첫 Step의 지연 Startup과 X/Y getter 순서를 이번 프로토타입에서 정의하지 않았다. 해당 장식 전체를 제외한다. 정적 초기 크기는 평가기에 보관한다.
- 초기 Global 이외 배치, Global/LastPosition 이외 배치 변경, 행성/바닥 추종, 회전/크기 고정.
- 이미지·원래 크기·크롭·smoothing 변경, 마스크 생성/변경/대상, 가시성/깊이 변경.
- 이미지 이외 장식, 히트박스/충돌/조건 연결, 조건·수동·타격 실행, 시각 효과 설정 조건.
- 사용자/미확인 이징, 비유한 초기값/목표값, 음수/비유한 길이, 시차 부품 없음, 축 고정/화면 고정.
- 다른 장식 효과 또는 임의 `CallMethod`/`AddComponent`가 발견되면 영향 범위를 단정하지 않고 전 장식에 제외 사유를 붙인다. 외부 소유자의 장식 setter 패치도 전 장식 제외다.

외부 모드가 Update 등에서 필드를 직접 쓰는 경우를 완전히 탐지하지는 못한다. 지원율은 이 스냅샷 분류의 비율이며 외부 쓰기 부재의 증명이 아니다. 일반 커스텀 맵·High 시각 설정만 받는다. 지원 밖 객체도 게임의 기존 경로로 계속 실행되며 그림자 쪽은 값을 쓰지 않는다.

## 교체와 시간 의미

게임 DLL을 실행하지 않고 현재 설치본을 ILSpy로 읽었다. 핵심은 `scnGame.ApplyEvent`, `ffxMoveDecorationsPlus.Decode/StartEffect`, `ffxPlusBase.ScrubToTime/AdjustDurationForHardbake`, `scrVfxPlus.Update`, `scrConductor.Update`, `scrDecoration.Setup/SetPlacementType`, `scrParallax.SetTrans`다. 이 작업의 기준 DLL 해시는 [정적 발췌](bake-evidence/bake2-clock-read-sites.txt)에 남겼다.

1. `scnGame.ApplyEvent`의 `crotchet = 60 / (bpm * pitch * floor.speed)`, MoveDecorations의 `duration = 이벤트 duration * crotchet`을 확인했다. 이미 pitch가 반영된 duration을 다시 나누지 않는다. 비커스텀의 `AdjustDurationForHardbake`는 별도 의미여서 제외한다.
2. 기존 Rec는 `(useSmoothDeltaTime ? Time.smoothDeltaTime : Time.deltaTime) * DOTween.timeScale`을 float로 누적한다. 이는 실제 시작 프레임, Unity 시간 배율, smoothing, float 덧셈 이력에 의존한다.
3. 현재 기본 conductor는 `songposition_minusi = (double)((float)(dspTime - dspTimeSong - calibration_i) * song.pitch) - addoffset`이다. 고정 pitch에서 곡 시각 차이/pitch는 DSP 시간 차이에 대응한다(캐스팅 정밀도 차이는 남는다). 입력 보정은 이미 곡 시각 안에 있다.
4. 새 의미는 **T0 = startTime - startEffectOffset**, `elapsed = (songTime - T0) / frozenPitch * frozenDOTweenTimeScale`이다. elapsed<=0은 시작값, duration 도달은 목표값, duration=0은 T0에서 즉시 목표값이다. calibration_i를 한 번 더 빼지 않는다. ScrubToTime의 `(t-startTime)/pitch-calibration_i` 공식을 일반 재생의 동치식으로 사용하지 않았다.
5. `scrVfxPlus.Update`는 paused이면 실행하지 않는다. conductor는 AudioListener.pause 동안 DSP fallback을 진행하지 않고, 기존 Rec는 Time.deltaTime 경로를 따른다. 새 평가기는 pause 플래그나 dt를 읽지 않고 전달된 곡 시각만 사용한다. 동일 표·장식·속성·곡 시각이면 호출 순서나 seek/되감기와 무관하다. DSP/Unity 시계의 실제 프레임 위상 동치는 주장하지 않는다.
6. pitch, DOTween.timeScale, 이징 기본 매개변수는 준비 시 고정한다. 이후 변경을 감지하면 현재 준비 상태를 무효화한다. 캡처 중 발생한 계약 실패는 테이프의 굽기 검증도 거부한다. 구형/WebGL conductor의 다른 공식, 동적 배속·smoothing·비표준 Time.timeScale 이력까지 기존 Rec와 같다고 주장하지 않는다.

교체는 이전 구간을 T0에서 평가한 **완료 전 현재값**으로 상대 목표를 먼저 계산한다. 이전 구간을 `Kill(true)` 완료한 목표값이 새 시작값이다. 예: 이전 현재 4, 이전 끝 10, 상대 +2이면 새 시작 10/새 끝 6이다. 모든 구간을 이어 붙이는 방식으로 현재 4를 새 시작값으로 쓰지 않는다.

이징은 `DecoAnim`과 동일한 `ZeroTween.Eval` → 게임 DOTween EaseManager 함수를 재사용한다. overshoot/period는 DOTween 기본값이며 Flash 계열 overshoot 정수화도 SetEase와 맞췄다. 공유 ZeroTween proxy를 수정하지 않고 매개변수만 읽는다.

## verify와 bench 출력

DecoSim 테이프에 프레임별 곡 시각(double), pitch, DOTween 배율, Unity frame 번호, 직교 카메라 월드→픽셀 배율을 추가했다. 장식 instance ID와 캡처 당시 완성된 굽기 표도 묶어 보관한다. 따라서 stop의 장식 재설정 뒤에도 과거 표로 대조한다. off/Unload는 이 참조까지 해제한다.

`decobake verify`:

- 기존 DecoSim의 장식 인덱스%4==0 **실제 Rec.Step 출력**과 해당 프레임 곡 시각의 굽기 출력을 비교한다. 속성별 n/최대 절대 오차/p99/상위 장식 8개를 기록한다. 색은 RGBA를 각각 집계한다.
- 교체가 발생한 장식의 n/최대/p99를 별도로 기록한다. 여기의 교체 분류는 맵 전체에서 실제 진행 중 교체 구간이 있는 장식이며, 교체 순간만의 표본은 아니다.
- 실제 StartEffect UnityFrame에서 테이프가 T0를 처음 통과한 UnityFrame을 뺀 차이와 차이별 빈도를 기록한다. 캡처 시작 경계/범위 밖은 미대응으로 센다. 비단조 곡 시각이면 위상만 NA로 거부하고 값 평가는 계속 무상태로 수행한다.
- 미지원 사유별 장식 수·비율과 전체 지원율을 기록한다. 표본 없음은 NA/NaN이며 오차 0의 증거로 쓰지 않는다.
- 위치는 Global `pivotPosVec`의 월드 원단위 오차와 `오차 * 1440 / (2 * orthographicSize)`로 환산한 3440×1440 기준 픽셀 오차를 기록한다. 직교가 아니면 픽셀 표본을 만들지 않는다. **이 환산은 시차·피벗·회전까지 합성한 최종 렌더 픽셀 차이가 아니다.** 제안 기준 0.01px를 최종 화면 승인 기준으로 채택하려면 합성/렌더 검증이 추가로 필요하다.

Rec 샘플이 없는 길이 0 효과는 라이브 Step 통계의 범위 밖이다. 중간 Kill 완료 샘플은 같은 프레임 최종 출력과 섞지 않는다. 해당 경계와 길이 0은 오프라인 fixture가 검사한다. 따라서 이 명령은 전 장식/전 이벤트 화면 검증 완료를 의미하지 않는다.

`decobake bench`는 곡/캡처 종료 후에만 실행한다. 마지막 완성 표의 worker 굽기 시간, 수집 CPU 합계, 관측 최대 메인 작업 시간, Prep 이후 완료 wall 시간, 표 payload 추정량, 프로세스 heap 변화, 지원 장식×16채널 평가 중앙/p99와 checksum을 낸다. 첫 평가 pass는 제외하고 32 pass를 측정한다. 장식 수·이동효과 인스턴스 수·전체 예약효과 수를 따로 출력한다. 원본 맵 이벤트 95,211과 반복 전개 이후 이동효과 수를 같은 집계로 취급하지 않는다.

메인에서는 Setup postfix를 장식별로 읽고, 최종 목록/대상 열거를 약 4ms마다 양보한다. 순수 구간 컴파일은 Task worker에서 수행한다. 목록/대상 상한은 장식 100,000, 예약효과 300,000, 대상 참조 4,000,000이다. 하나의 원자적 Unity 호출·LINQ MoveNext·할당/GC를 선점할 수 없으므로 **최대 메인 작업<=16ms는 실게임 로그로 확인해야 한다.** 수집 CPU에는 같은 맵의 Setup 관찰 누적 비용도 포함한다. heap 변화는 Prep 이전 초기 스냅샷 메모리를 제외하고 다른 게임 할당을 포함하며, payload에는 객체 header/임시 목록/두 번째 스냅샷 사본이 빠진다. 전체 상주 메모리의 정확한 값으로 해석하지 않는다.

## 오프라인 검증 결과

| 검사 | 결과 | 증거 |
|---|---|---|
| 개발 Release | 오류 0, 경고 38 | [로그](bake-evidence/bake2-build-dev.txt) |
| 일반 Player Release | 오류 0, 경고 36 | [로그](bake-evidence/bake2-build-player.txt) |
| Player+AutoTest Release | 오류 0, 경고 38 | [로그](bake-evidence/bake2-build-playerauto.txt) |
| LoadCheck 개발/Player/PlayerAuto | FieldRef 266/229/266, 틀림 0, 동적 이름 5곳 | [개발](bake-evidence/bake2-loadcheck-dev.txt), [Player](bake-evidence/bake2-loadcheck-player.txt), [AutoTest](bake-evidence/bake2-loadcheck-playerauto.txt) |
| 개발 타입 포함/Player 제외 | 개발·AutoTest 포함, 일반 Player 제외 | 같은 LoadCheck 로그 |
| 새 후크의 정적 계약 | Setup/PrepVfx 각각 정확 일치 1 | 개발·AutoTest LoadCheck 로그 |
| 실제 컴파일러 fixture | 교체, cutoff, 동시각 순서, 길이 0, 반복, 되감기, pitch/배율, 전체 제외, 잘못된 정렬 거부, 취소 통과 | [fixture](bake-evidence/bake2-contract-check.txt) |
| 실제 DOTween 이징 | 3,465개 중간값 및 각 완료 목표값 통과 | 같은 fixture 로그 |

fixture는 production `DecoBake.Core.cs`를 직접 링크한다. 게임 DOTween의 순수 EaseManager 함수를 호출했으며 게임 프로세스나 Unity 장면을 실행한 것이 아니다. LoadCheck는 .NET의 메타데이터 검사이지 Mono의 실제 Harmony 설치 성공 증명이 아니다. 기존 경고를 오류 0과 혼동하지 않았다.

별도 **희소 합성 fixture**: 장식 28,828개, 이벤트 95,211개, 이벤트당 대상 하나, 구간 95,211개. .NET 8에서 굽기 **34.644ms**, payload **9,804,664B**, GC 전 heap 차이 **18,145,440B**, 전체 16채널 평가 중앙 **2.218ms**, p99 **4.541ms**였다. 이 자료는 **Arche 이벤트/태그 fanout이 아니며** 실제 Arche 수집·Mono 성능·16ms 메인 예산·2초 로딩 목표·0.01px 정확도를 증명하지 않는다.

## 내가 돌릴 순서

이번에는 실행하지 않았다. 나중에 사용자가 승인하여 개발/AutoTest 빌드를 사용할 때의 순서다. 대상 파일은 디렉터리에서 확인한 다음 **하나**다.

`D:\얼불춤 맵 파일\Arche\[𝐃𝐎 𝐍𝐎𝐓 𝐎𝐏𝐄𝐍 𝐓𝐇𝐈𝐒 𝐀𝐓 𝐇𝐎𝐌𝐄] FULL.adofai`

기존 기본 DecoAnim 경로와 High 시각 설정에서 다음 AutoTest 명령을 순서대로 사용한다. open 전에 on을 해야 초기 Setup 스냅샷을 얻는다. play는 PrepVfx를 다시 만들 수 있으므로 **재생 후 ready를 기다린 다음** 캡처한다.

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

`ready`는 완성될 때까지 같은 단계에서 기다리고, 수집 실패면 예외로 시험을 중단한다. 먼저 준비 로그의 장식/효과/지원 수를 확인한다. 12초 표본은 교체나 중후반 효과를 포함한다는 보장이 없으므로 필요한 구간별 캡처를 따로 해야 한다. 초기 짧은 표본에서 오차가 작아도 전곡 결론으로 확장하지 않는다.

확인할 지표는 속성별 표본 수/최대/p99/상위8, 시작 위상 프레임 빈도, 교체 장식 오차, 사유별 제외율, 픽셀 환산<=0.01px 제안 기준, 실제 장식/예약효과 수, 굽기 worker+수집 비용, 최대 메인 작업<=16ms, 메모리, 평가 중앙/p99다. 현재 이 라이브 지표들은 전부 미측정이며 제품 채택이나 릴리스 승인은 남아 있다.
