# Codex 작업 F: DecoAnim 빠른 길(FastLane) — 호출당 중복 판정·정리 패스 제거 — 게임 실행·Mods 설치·커밋 금지

결과: `docs/handoff/codex-fastlane-20261019-result.md` (**apply_patch 로 UTF-8 BOM, 쓴 뒤 `Get-Content -Encoding UTF8` 로 한글 확인**), 한국어, 추측은 "추정". 빌드 셋 오류 0. 켜기/끄기: 기본 **켬**, 모드 폴더 `fastlane-off` 파일로 끔(로드 때 한 번 읽음). 검증 명령 `fastlane verify|state`. 제품 동작(화면·게임 결과)은 **비트 동일**해야 한다.

## 실측 (Claude, 프로덕션 경로, 16프레임에 1번 표본 `decosamp`, Arche 곡 257.8~278.8초 21초 창 평균, 2560×1440 120Hz)

`DecoAnim.UpdatePrefix`(DecoAnim.cs) = 프레임당 1.91ms(`fuse`). 표본 프레임 기준 루프 전체 2.216ms(기록 6,417개 목록):
| 항목 | 프레임당 호출 수 | 호출당(Stopwatch 쌍 약 0.05µs 포함) | 프레임당 |
|---|---:|---:|---:|
| 위치 쌍(`StepPair`: X+Y) | 1,174 | 0.60µs | 0.708ms |
| 색(Key 9) `Step` | 1,064 | 0.43µs | 0.455ms |
| 회전(Key 5) `Step` | 1,182 | 0.18µs | 0.216ms |
| 크기 X(Key 7) `Step` | 943 | 0.16µs | 0.146ms |
| 크기 Y(Key 8) `Step` | 943 | 0.15µs | 0.146ms |
| 호출 합 | | | 1.673ms |
| `Compact()`(완료 기록 걷어 내기, 매 프레임 목록 전체 한 패스) | | | **0.246ms** |
| 루프 뼈대(위 둘을 뺀 나머지: 목록 순회, `!r.Running` 건너뛰기, `PrepareNative`, `GroupFrameStart` 등) | | | 약 0.30ms |
지난 시도 교훈(모두 `docs/optimization-36.md` 끝부분): GroupStep(속성 5개를 한 단계)·PosFast(`SetPosition` 체인 생략)는 비트는 같았지만 A/B 에서 이득이 없거나(−0.08ms) 오히려 느렸다. **엔진 호출이 아니라 호출당 관리 코드(판정·조회·검사)가 비용**이고, 한 장식 기록 5개에 대해 같은 판정을 반복한다. 화면 밖 건너뛰기·작업 스레드·셰이더 변경 등은 이미 기각됨.

## 할 일 (각각 독립으로 켜고 끌 수 있게 내부 플래그를 나누고, 효과가 없으면 코드를 되돌릴 수 있게)

1. **정리 패스 합치기**: `Compact()` 의 별도 패스를 없앤다. 메인 루프(`UpdatePrefix`)에서 기록을 진행하면서 같은 패스에서 `recs[w++]`(Running 인 것만 유지, 아니면 `InList=false`)로 앞쪽으로 당긴다. 루프 중 `recs` 에 항목이 추가될 수 있는지(효과 시작이 `NewRec` 로 `recs.Add`, 설정 함수 안에서 게임 코드가 호출되는 경로: `OnComplete`→setter→게임 `SetPosition`/`SetColor` 에서 다른 효과를 시작하는지) 코드/IL 로 확인하고, 추가될 수 있으면 `w` 와 `i` 를 안전하게 처리(루프가 끝난 뒤 남은 `i..Count` 를 `w` 뒤로 이동). 순서(만든 순서)는 보존. `Edition.Dev` 의 `Shadow` 유지 조건·GroupStep/`AblateMask` 등 다른 `continue`/`i += n` 경로는 기존 `Compact` 로 폴백하거나 같이 처리. `InList`/`NewRec` 재사용 의미는 그대로(완료된 기록이 이번 프레임 안에 목록에서 빠지는 시점이 같아야 한다: 원래는 프레임 끝의 `Compact` 에서 빠짐 — 같은 프레임 안 다른 기록의 `OnComplete`/`Kill` 이 `r.InList` 를 읽는 경로가 있는지 확인).
2. **장식당 판정 캐시**: 한 프레임 안에서 같은 장식(`r.D`)의 연속 기록(위치 쌍, 회전, 크기 X·Y — `FastMove` 가 한 이벤트로 같은 장식에 만든 기록들은 `recs` 에 연속)이 매번 `MoveApply.DeferTransform(d)`(→ `DeferRotScale`, 정확한 `scrVisualDecoration` 확인, `InvisibleSkip.MaskOrUnknown`(delegate 호출), `SkipForLate`: 상태 검사 여러 개, `managerLateFrame`, `d.hitbox`, `GetVisible()`, `lateSkippedSet.Add`+`lateSkipped.Add`(해시셋)) 와 위치 쪽 `UpdateNow`→`SkipForLate` 를 반복한다. 같은 프레임·같은 장식의 판정을 한 번만 하고 재사용: 루프 로컬에 `lastDeco`/`lastDeferResult` 를 두고 `Apply`/`StepPair` 에 전달하거나, `MoveApply` 쪽에 프레임 번호+장식 참조 한 칸 캐시. **판정이 같은 프레임 안에서 바뀔 수 있는 경우**(색·불투명도 기록 단계가 알파를 바꿔 `GetVisible` 이 뒤집힘, 효과 시작/끝, `InvisibleSkip` 의 숨김 변경, `Dormancy` 깨움)를 코드로 전수해 캐시를 무효화하는 지점을 정확히 정하라(색/불투명도 `Step` 실행 시 무효화 등). `lateSkippedSet.Add` 의 의미(그 프레임 안에서 이미 등록됐으면 재등록이 무해)도 확인.
3. **회전·크기 기록의 짧은 길**: 호출당 관리 오버헤드(`Started` 확인, `Pos`/끝 판정, `Apply` 의 키 `switch`, `ZeroTween.Eval`, setter 분기)를 줄이기 위해, 보이는 이미지 장식(판정 캐시가 "LateUpdate 에 맡김" 이고 해당 키가 5/7/8)의 단계를 인라인 코드로 처리(같은 float 연산 순서·같은 필드 쓰기, 크기 Y 시작값은 크기 X 설정 뒤에 읽는 순서 유지). 가능하면 `Eval` 결과 재사용(같은 프레임·같은 (Pos, Dur, Ease, Over, Period) 비트면 직전 e 재사용: `ZeroTween.Eval` 은 결정적, 지난 GroupStep 작업에서 IL 로 정적 상태 없음을 확인함).
4. **마이크로 벤치(필수, 먼저)**: `TightLate.Bench`/`GroupStep` 벤치 방식(`GetUninitializedObject` 더미 장식·더미 Transform 2,150개 + 더미 `Rec`)으로 `fastlane bench`: 위 표의 각 항목(회전·크기·위치쌍·루프 뼈대·정리)의 호출당 µs 를 FastLane 켬/끔(또는 원래 함수 직접 호출)으로 재서 결과에 적는다. 목표: 회전·크기 호출당 0.16~0.18 → 0.08µs 이하(Stopwatch 쌍 제외 기준), `Compact` 0.25 → 0 에 가깝게(합쳐진 패스의 추가 비용만), 위치 쌍은 판정 캐시로 0.60 → 0.45µs 이하. 벤치로 못 미치면 원인을 찾아 줄이고, 그래도 안 되면 그 항목은 넣지 말 것(지난 두 번 벤치는 좋은데 실전에서 느려진 전례가 있으니 벤치 결과를 과신하지 말고 실전 A/B 에서 확인할 판단 기준을 적어라).
5. **검증**: (a) `fastlane verify`: 표본(1/64) 장식에서 캐시된 판정과 즉석 계산 판정을 비교해 불일치 수 보고. 회전·크기 짧은 길의 결과 값은 원래 `Step` 경로와 같은 입력으로 계산한 값을 비트 비교(순수 계산 함수 공유). (b) 기존 `late-verify`/`tightlate verify`/`decosig` 가 계속 의미 있게(모두 켜진 채 불일치 0). (c) 통계: 인라인 처리 수/폴백 수와 이유, 캐시 적중·무효화 수.
6. 결과 문서에 예상 이득(식)과 Claude 가 돌릴 순서(`fastlane-off` 켬/끔 새 프로세스 ABBA + `fuse begin/end` + **`decosamp begin/end`**(표본 프레임 호출별 비용; 켠 판에서 항목별 µs 가 줄었는지), `fastlane verify`, 곡 끝 `decosig` 대조)를 적어라. `decosamp` 는 `DecoAnim.cs` 의 `#if AUTOTEST` 표본 계측이며 `Step`/`StepPair` 호출 바깥을 재므로 인라인 처리한 기록이 이 표에 안 나오면 그 이유를 적고 계측도 같이 확장하라(`sampTicks[k]` 에 인라인 경로 시간도 더하기).
