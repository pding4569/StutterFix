# DecoEngine 2단계-a 결과 — 2026-10-19

변하는 색 알파·불투명도를 가진 장식의 **변환 기록 소유를 허용**했다. 색 9·불투명도 10은 기존 목록에서 기존 순서로 실행한다. 가시성이 유지되는 프레임만 변환을 먼저 계산하고, 0 전환·불확실한 프레임은 진행 전에 기존 경로로 복귀한다. 같은 변환 키를 다시 시작할 때 장식 전체를 복귀시키는 처리는 슬롯 교체로 바꿨다.

**기본 끔 유지. 실게임 채택 판정은 미실시다.** 게임 실행·Mods 설치·커밋은 하지 않았다. Dev/Player/PlayerAuto 오류 0개, 관리 스텁 검사는 358,854개 통과했다. 실제 Unity Mono의 비트 동일성, Arche 소유율 70% 및 두 시간 합 1.8ms 달성은 아직 확인하지 않았다.

작업 시작 HEAD는 지시서 커밋 `08a10c6`이며 구현 기준 `467184c`는 그 부모다. 기존 `.codex/config.toml` 변경과 미추적 파일들은 보존했다. 제품 소스 변경은 `DecoEngine.cs`, `DecoAnim.cs`, `TightLate.cs` 세 파일이다. AutoTest는 기존 `decoengine` 명령 전달을 그대로 사용한다. 검사 코드와 이 문서·근거 파일을 추가/갱신했다.

## 1. 가시성·쓰기 순서 조사

실제 게임 `Assembly-CSharp.dll`의 IL과 `DecoAnim`, `MoveApply`, `Dormancy`, `InvisibleSkip`, `PosFast`, `TightLate` 소스를 따라 확인했다. IL 원문은 [근거 디렉터리](decoengine2-evidence/)의 `ApplyColor-il.md`, `GetVisible-il.md`, `UpdatePosition-il.md`, `game-methods-il.md`에 있다. 도구가 이름이 같은 다른 타입의 메서드도 함께 반환하므로 **end of method의 타입 이름까지 확인**했다.

- 비마스크·히트박스 없는 정확한 `scrVisualDecoration` 타입에서 `GetAlpha`는 `rendererColor.a`, `GetVisible`은 `rendererEnabled && alpha != 0`이다. 음수·NaN도 단순한 `!= 0`에서는 보일 수 있으므로 이를 양수와 같게 취급하지 않았다.
- `ApplyColor`는 `color.a * opacity * floorOpacity`를 rendererColor에 저장한다. 바닥 붙음은 기존 소유 조건에서 제외하므로 마지막 인자는 1이다. 위치·회전·크기 필드를 읽지 않는다.
- `SetPosition`은 위치 필드·자식 위치·피벗 오프셋·화면 고정 상태를 쓰고 `UpdatePosition`으로 이어진다. MoveApply의 `UpdateNow`는 호출 순간 `SkipForLate`를 확인한다. PosFast도 같은 가시성 확인을 거친다.
- `SetRotation`/`SetScale`은 필드와 Transform을 쓴다. DecoAnim은 호출 순간 `DeferTransform`이 참이면 필드만 쓴다. `UpdatePosition`은 위치·시차 계산 뒤 **현재 필드의 회전·크기도 다시 적용**한다.
- `DecoAnim.UpdatePrefix`의 기존 목록은 생성 순서다. `ApplyE → OnUpdate → 완료이면 OnComplete`이며 색은 완료 프레임에 보간값과 정확한 끝값을 차례로 쓴다. 크기는 Startup 때 현재 scaleVec를 읽는다. 위치 X/Y 인접 쌍 처리도 유지한다.
- 숨김 위치는 InvisibleSkip이 필드만 저장하고, 다시 보이는 ApplyColor에서 미룬 위치를 적용할 수 있다. Dormancy는 ApplyColor 뒤 깨우며, 숨김 LogicUpdate의 위치 갱신을 생략한다. 앞에서 Late에 맡겼다가 뒤에서 숨겨졌다면 MoveApply.ManagerLatePostfix가 원래 UpdatePosition을 보충한다.

| 같은 프레임의 상태/순서 | 기존 경로의 필드·Transform 및 시점 | 엔진 처리 / 관찰 가능성 |
|---|---|---|
| 모든 중간 알파가 양수 | 각 변환의 필드는 Update에서 바뀌고, 가시 장식의 최종 Transform은 Late에서 최신 필드로 덮인다. 색 적용은 변환 필드를 읽지 않는다. | 변환 필드를 먼저 게시하고 색은 원래 순서로 실행해도 최종 입력은 동일하다. 생략한 중간 Transform 쓰기는 지원하는 소비자 범위에서 관찰되지 않는다. |
| 0→양수 | 색 앞의 위치는 Lazy에 남을 수 있고, 색 적용이 이를 flush한다. 뒤의 변환은 Late로 미뤄진다. 최종 가시 Late는 최신 필드를 쓴다. | 현재 숨김이면 진행 전 복귀. 원래 레코드 순서와 Lazy/깨우기/Startup을 그대로 실행한다. 보이기 시작하면 재입장을 요청한다. |
| 양수→0, 앞에 미룬 변환 있음 | 색 앞 변환은 Late 등록, 색 뒤 회전·크기는 즉시 쓸 수 있다. 숨김 LogicUpdate가 위치를 생략해도 postfix 보충이 최종 필드로 전체 변환을 다시 쓴다. | 다음 알파의 보간값·완료값을 사전 검사하고 **어떤 엔진 슬롯도 진행하기 전에** 전부 복귀한다. 원래 보충 등록도 실행된다. |
| 양수→0, 앞에 미룬 변환 없음 | 숨김 위치는 Lazy만 남고, 회전·크기는 즉시 쓰일 수 있다. 보충 등록이 없으면 pivot 위치가 이번 필드 위치와 달라도 정상이다. | 무조건 숨김 Transform을 전부 쓰는 설계는 최종 결과를 바꿀 수 있다. 전환 프레임은 기존 경로로 처리한다. 이 차이는 관찰 가능하다. |
| 한 프레임에 양수→0→양수, 또는 보간값 0 후 완료값 양수 | 중간 GetVisible·Lazy flush·즉시 쓰기 판단이 달라진다. 최종 알파만 보는 것으로는 안전하지 않다. | 각 색/불투명도 레코드의 OnUpdate 값과 OnComplete 끝값을 모두 검사한다. 한 값이라도 안전 범위 밖이면 복귀한다. |
| 계속 0 또는 음수·비정상 값·극소 값 | Lazy·Dormancy 및 `!= 0`과 `<= 0`의 서로 다른 판단이 작동할 수 있다. | 기존 경로. 음수끼리 곱해 양수가 되는 경우도 이번에는 소유하지 않는다. |
| 엔진 Update 뒤 즉시 효과가 숨김 처리 | 원래 변환이 등록한 Late 보충은 소유 레코드의 자연 완료 여부와 관계없이 남는다. | TightLate Entry를 프레임당 한 번 별도 목록에 보관하고, 관리자 postfix에서 아직 숨김이면 원래 UpdatePosition을 실행한다. State가 이미 재활용됐어도 보충한다. |

위 동등성 논증은 기존의 타입·부모·시차·자식 안정성 및 외부 패치 제외 조건 아래에서다. 죽은 `screenRelativePos` 쓰기를 생략하는 기존 1단계 범위도 유지했다. 이 필드를 포함한 모든 내부 상태나 임의 외부 모드의 중간 관찰까지 동일하다는 주장이 아니다. 실제 Unity Transform getter 결과·Mono 산술·Harmony 실행 순서는 아래 실게임 대조가 필요하다.

## 2. 구현과 비용

`State.Color`/`Opacity`는 원래 목록에 남은 두 레코드의 직접 참조다. `AlphaSafe`는 현재 color.a·opacity와 다음 Step의 알파 인자들을 확인한다. 보간 위치는 기존과 같은 `Pos + td` 및 Dur 제한, 이징은 같은 Eval, 완료 시 정확한 끝값도 별도로 확인한다. 첫 Startup 전 색/불투명도는 end-start로 변화를 예측한다. 일정한 양수 인자는 Eval을 생략한다.

각 인자를 `[0.000001, 100]`으로 제한해 곱이 0으로 언더플로하거나 무한대로 넘는 경우를 피했다. 이 범위를 벗어나면 실제로 계속 보이더라도 보수적으로 복귀한다. Back/Elastic 등도 이름만으로 허용하지 않고 다음 실제 평가값을 검사한다. 추가 비용은 장식당 최대 두 번의 Eval이다. 실전 비용 감소는 **추정**이며 아직 측정하지 않았다.

`AlphaWillSet`은 원래 색 setter 직전에 안전 범위 밖→안으로 들어오는 경우 Dirty를 설정한다. 해시 조회 없이 재입장을 요청하므로, fade-in 뒤 새 효과가 없어도 소유 기회를 다시 얻는다. 완전히 숨겨진 프레임마다 입장 전체 검사를 강제하지 않는다.

입장 시 EngineEntry의 직전 LastFrame 조건을 완화했다. 원래 첫 Step 뒤 현재 가시성·구조 조건을 통과했다면 숨김에서 깨어난 Entry도 입장시킨다. 오래된 Transform 캐시의 Has를 반드시 지워 첫 Late가 실제로 쓰게 한다. 이후 엔진 Update는 여전히 관리자 LastFrame·구조·예외 거부를 검사한다. Entry 자체가 없는 첫 만남은 한 번 더 재시도하며, 목록에 영원히 없는 장식이 전 프레임 전체 입장 검사를 유발하지 않게 연속 재시도를 제한했다.

같은 키 교체 경로:

1. KillPrefix/Complete의 `DetachRecord`가 해당 슬롯만 뺀다. 다른 슬롯과 State는 유지한다. 최대 슬롯 수가 5라 탐색·이동은 상수 한도다.
2. 기존 Complete 본문의 Startup/Apply/OnUpdate/OnComplete는 그대로 실행한다. 따라서 **Kill(true)의 끝값을 먼저 쓴 뒤** Pos/Rot 등의 기존 코드가 FStart를 현재 필드에서 읽는다. Scale의 시작값은 기존 첫 Startup까지 미룬다.
3. NewRec은 효과 생성 경계에서만 소유 사전을 조회한다. 지원 키는 Pending으로 State를 유지한다. 마지막 슬롯을 끊은 경우도 새 레코드의 첫 Step 전까지 빈 State를 유지한다.
4. 새 변환 레코드는 원래 목록에서 첫 Startup/Step을 실행한 뒤 기존 State에 붙는다. 슬롯은 EngineOrder 순서를 유지한다. 피벗/시차 등 지원 밖 새 키는 생성 전에 장식 전체를 돌려보낸다. Shadow·비정상 레코드 등은 기존 안전 조건으로 복귀한다.
5. 엔진 소유 레코드를 원래 목록에서 뺄 때 InList=false로 명시하고, 복귀 때 이미 원래 목록에 있는 새 레코드를 중복 추가하지 않는다. 소유/목록 중복과 같은 Rec 재사용 200회 반복을 검사했다.

정상 엔진 진행·알파 예측·슬롯 분리·Late 보충 등록에는 해시 조회가 없다. 필드 게시와 Late 등록은 장식당 프레임당 각각 최대 한 번이다. 효과 생성/입장/실제 복귀 경계의 사전 조회와 복귀 시 전역 정렬은 남아 있다. 따라서 입장·복귀 자체 전체를 O(1)로 만든 것은 아니다. **매 효과마다 복귀시키던 경로를 제거**했고, 남은 전환 프레임 복귀 비용은 실전 카운터로 확인해야 한다.

TightLate의 별도 Entry 목록은 정수 프레임 표시로 중복을 막는다. 크기가 안정된 뒤에는 목록 용량을 재사용하지만 최초 확장 때 할당할 수 있다. 정상 프레임에도 목록 순회와 GetVisible 한 번이 추가된다. 무거운 맵에서 이 비용이 1.8ms 예산 안에 드는지 확인해야 한다.

보충 갱신 postfix는 기존 모드 Harmony ID에 설치하며 `before = StutterFix.fuseprobe`, Low 우선순위로 fuse의 마지막 postfix보다 먼저 실행한다. 새 Harmony ID/정적 이벤트/PlayerLoop는 없다. 기존 Main.Unload의 ID별 UnpatchAll 및 TightLate.Uninstall/Forget으로 패치와 목록 참조를 정리한다. 예외 시 기존 finalizer가 남은 보충 목록을 정리한다. 정상 성능 판의 보충 순회 비용은 fuse 관리자 시간 안에 포함된다. LateSkip 또는 DeferRotScale을 끄면 엔진 소유를 보류한다.

## 3. state와 bench

`decoengine state`는 세션 누계다. 이번 `frames` 분모는 엔진이 소유한 프레임만이 아니라 DecoAnim에 활성 작업이 있어 진입한 Update 전체다. 이전 결과의 분모와 다르다.

| 출력 | 뜻 |
|---|---|
| owned / owned-rec | 지금 소유 State 수 / 변환 슬롯 수. 색·불투명도는 소유 기록에 포함하지 않는다. |
| frames / deco-frames / deco/frame | 측정 Update 수 / 엔진에서 갱신한 장식 횟수 / 그 평균 |
| owned-steps / rec/frame | 엔진에서 처리한 변환 레코드 누계 / Update당 평균 |
| all-steps / ownership% | 해당 프레임들의 DecoAnim.Steps 증가량 누계 / owned-steps÷all-steps×100. 색·불투명도 및 기타 원래 키도 분모에 포함한다. |
| admissions / restores / sync | 신규 State 입장 / 장식 전체 복귀 / 필드 게시 횟수 |
| detach / attach | 기존 슬롯 분리 / 원래 첫 Step을 마친 새 슬롯의 기존 State 부착. 전체 복귀와 다르다. |
| alpha-fallback / late-fixup | 다음 알파가 불확실해 진행 전 복귀 / Update 뒤 숨김으로 원래 UpdatePosition 보충 |
| alpha-events / zero-endpoints | 실제 양의 길이 Rec 생성 시 색/불투명도 횟수와, 그 시작·끝 중 하나가 정확히 0인 횟수. 소스 이벤트 수가 아니라 대상 장식별 실행 횟수다. 길이 0 즉시 효과는 제외한다. |
| checks / mismatch / first | 기존 독립 참조 계산의 검사 수 / 불일치 / 첫 차이 |

`rej` 인덱스 1~8: 색 기록 무효 / 불투명도 기록 무효 / 다른 키 / 미시작·Shadow·Batch·비정상 시계 / 중복 키 / 변환 슬롯 없음 / Entry 입장 불가 / 자식 기준 불가. 0은 빈 자리다. 이제 변하는 알파 자체를 rej 1로 세지 않는다. 기존 `reasons`의 구조·숨김·수명·자식 변경 분해도 유지한다.

`missing` 인덱스: 0 빈 자리, 1 관리자·설정·수명 조건 불가, 2 사전에 Entry 없음, 3 EngineRejected, 4 현재 숨김, 5 기타 Eligible 구조 불가, **6 LastFrame이 오래됐지만 입장 회복 성공**, **7 같은 프레임 Entry로 입장 성공**. 6·7은 거부 수가 아니다. 진행 중 LastFrame 단절은 여전히 reasons 7로 복귀한다.

구간 소유율은 반올림된 누계 ownership%를 빼지 말고 **100×Δowned-steps/Δall-steps**로 구한다. Δall-steps=0이면 측정 실패다. Δdeco-frames/Δframes와 Δowned-steps/Δframes로 창 구간 평균을 구한다. probe·verify 예외가 있는 판은 정확한 성능 표본으로 쓰지 않는다.

`decoengine bench [0..100]`에 알파 전환을 흉내 낸 장식 비율을 추가했다. 예: `bench 25`. 2,150개·변환 기록 5개·200회 조건을 유지하며 선택된 장식은 다음 알파가 0이 되는 별도 색 레코드를 갖는다. `transition-checks`를 표시한다. **전환 사전 검사만 흉내 내고 동일 작업량을 위해 Advance는 계속 수행한다. 실제 복귀 정렬·기존 setter/Lazy·재입장 비용을 재현하는 벤치가 아니다.** deferred 목록과 실제 매니저 목록 비용도 제외한다. 출력에 이 한계를 명시했다. bench는 이번에 실행하지 않았다.

## 4. 실제 Arche 원본 이벤트 집계

`py -3 tools/DecoEngineCheck/alpha_events.py`로 실제 FULL 맵을 읽기만 했다. SHA-256 `026873b89f05ea94b86b83634a938ac053593ebea6cb832d8134e4a8084f8064`, actions 108,617개, decorations 28,835개다. 근거: [arche-alpha.json](decoengine2-evidence/arche-alpha.json).

| 항목 | 색 | 불투명도 |
|---|---:|---:|
| 활성 MoveDecorations 중 해당 속성 소스 이벤트 | 14,928 | 908 |
| 종료 알파/불투명도 0 | 7,405 (49.60%) | 348 (38.33%) |
| 양의 길이 이벤트 / 그중 종료 0 | 13,601 / 6,179 | 345 / 218 |
| 태그 대상별 정적 바인딩 | 132,274 | 20,218 |
| 직전 목표값을 시작으로 보는 모델의 시작 또는 끝 0 | 109,035 (82.43%) | 13,341 (65.99%) |
| 같은 모델의 0↔비0 경계 | 58,154 | 13,044 |

종료 0 수는 원본의 직접 집계다. **시작값 모델은 추정**이다. Kill(true)가 앞 기록의 목표값을 적용한다는 성질을 사용했지만 RepeatEvents 13개 확장, 체크포인트, 다른 속성 작성자, 실제 시간·효과 순서를 재현하지 않았다. 따라서 이 표로 252.8~273.8초 창의 전환 프레임 빈도나 소유율을 계산할 수 없다. 실제 생성 시작/끝은 위 alpha-events/zero-endpoints, 실제 소유 중 복귀는 alpha-fallback의 창 구간 차이로 확인한다. 순간 전환이 드물어 이득이 클 것이라는 판단 역시 현재는 추정이다.

## 5. 실행한 검증과 한계

| 확인 | 결과 / 근거 |
|---|---|
| Dev | 오류 0, 경고 35. `decoengine2-evidence/build-dev.log` |
| Player | 오류 0, 경고 33. `decoengine2-evidence/build-player.log` |
| PlayerAuto | 오류 0, 경고 35. `decoengine2-evidence/build-playerauto.log` |
| 소유권·알파·교체·Late 보충 | 358,854 assertions 통과, 참조 mismatch=0. `ownership-check.log` |
| 기존 DecoAnim 회귀 | 실제 UpdatePrefix 80사례, 묶음 900사례, scalar 3,240·pair 300사례 등 통과. `groupstep-regression.log` |
| 텍스트·공백 | 결과 문서 EF BB BF, 엄격 UTF-8 디코딩, U+FFFD 0개, 한글 문구 왕복 확인. Get-Content -Encoding UTF8 직접 확인 및 git diff --check 통과. `result-encoding.json` |

재현 명령은 `py -3 tools/DecoEngineCheck/run.py`, `py -3 tools/GroupStepCheck/run.py`다. 전자는 실제 DecoEngine.cs와 추출한 실제 TightLate.EngineDefer/EngineLatePostfix를 연결한다. 페이드 내부 소유·0 끝값 이전 복귀·불투명도·비정상 값·완료 직전 중간 0·원래 순서 복원·재입장 요청·다른 키 추가·지원 밖 키 복귀·중복 목록 방지·같은 Rec 200회 교체·마지막 슬롯 완료 뒤 Late 보충·등록 중복·보충 예외를 검사했다. 로그의 injected InvalidOperationException은 의도한 예외 격리 검사다.

이 검사는 .NET 8 관리 스텁이다. 게임 Setter/Transform/Harmony/Mono 실행 증거가 아니다. Kill(true)의 콜백 본문은 기존 소스대로 유지했지만, 엔진 교체 스텁의 완료·첫 Step 일부는 모델로 처리한다. 기존 실제 Complete/Startup/Step의 별도 회귀도 수행했다. 실제 게임에서 두 경로가 결합되는 검증은 남아 있다. 검사 도구가 출력한 .NET 벤치 수치를 실게임 성능 근거로 사용하지 않았다.

## 6. Claude 후속 실게임 순서

이 절은 다음 작업자를 위한 절차다. 이번 작업에서는 실행하거나 Mods를 변경하지 않았다.

1. 별도 승인 범위에서 새 PlayerAuto DLL을 준비한다. 기본 옵션과 해상도·주사율·다른 모드를 고정한다. TightLate/LateSkip/DeferRotScale은 켜고 GroupStep/Batch/NativeMotion은 끈다. PACL 로드나 외부 setter 패치로 compatibility가 제외되면 소유 0을 검증 성공으로 세지 않는다. 같은 DLL reload A/B는 하지 않는다.
2. 정확성 전용 새 프로세스 B에서 Arche FULL을 게임 화면 경로로 연다. `decoengine verify`, `tightlate verify`와 기존 late-verify를 켠다. 첫 화면·0→양수·양수→0·동시 색/불투명도·새 효과 교체·retry/중간 시작/곡 끝을 확인한다. checks 증가와 mismatch=0, 실제 슬롯 소유 증가가 모두 필요하다. 숨김 보충·부모/자식 변경·미지원 키 복귀 카운터도 확인한다.
3. **252.8~273.8초 창** 바로 앞에 `decoengine state`, `fuse begin`, 창 끝에 `fuse end`, `decoengine state`를 찍는다. seek를 쓴다면 앞 여유를 포함해 실제 로그의 곡 시각으로 창을 맞춘다. 긴 wait는 명령당 180초 미만으로 나눈다. 정확성 판은 성능 판에서 제외한다.
4. 메뉴에서 `decoengine bench 0`, `bench 25`, `bench 100`을 각각 수행해 추가 사전 검사 비용만 비교한다. 본곡 성능 판과 분리한다. 이 벤치로 폴백 포함 1.8ms 달성을 판정하지 않는다.
5. 검증을 끈 **새 프로세스 ABBA**: A=decoengine-on 없음, B=있음, A→B→B→A. 매 판 같은 Arche 창에서 fuse와 state 앞뒤를 수집한다. 애니메이션 갱신+관리자 LateUpdate 합, 100×Δowned-steps/Δall-steps, Δdeco-frames/Δframes, Δadmissions/restores/attach/detach/sync/alpha-fallback/late-fixup/rej/missing을 기록한다. A의 엔진 카운터가 비활성인 것은 정상이다. B에서 all-steps·checks·owned가 0인 자료는 유효한 동등성 근거가 아니다.
6. 별도 정확성 판으로 A/B 각각 Arche·HELLO 전곡 및 곡 끝 `decosig`를 대조한다. 새 차이는 장식별로 조사한다. 기존 카메라/계속 도는 회전 잡음으로 자동 무시하지 않는다. verify의 Transform 대조는 setter 입력 비교이므로 실제 getter 결과를 확인하는 tightlate/late 대조도 함께 본다.
7. **채택 기준:** 같은 창의 두 시간 합 ≤1.8ms, verify 불일치 0, 의미 있는 소유율(목표 70% 이상), 새 decosig/화면 차이 없음, 최악 프레임·하위 백분위 악화 없음. 새 알파 Eval·입장 검사·보충 목록이 절약분을 먹는지 필요하면 별도 profsamp 판으로 확인한다. 미달이면 기본 끔을 유지한다.

현재 결론은 기본 끔 구현 후보와 오프라인 검증 완료다. 실게임 소유율·비트 동일성·1.8ms 성능 채택은 후속 확인 대상이다.
