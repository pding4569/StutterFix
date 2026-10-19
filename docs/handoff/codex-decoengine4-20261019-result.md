# DecoEngine 4 결과 — 2026-10-19

영구 소유, 알파 전환의 순서 보존 경로, resident 슬롯의 효과 교체를 구현했다. 인계 항목 1·4·5를 반영했다. **2번 자식 위치 폴링의 완전 제거와 3번 색 직접 처리는 안전 근거가 부족하여 보류했다.** 게임 실행·Mods 설치·커밋은 하지 않았다. 기본 끔(`decoengine-on` 파일로만 켬)을 유지했다.

작업 시작과 종료 HEAD는 `752bb2c`이다. `16ca13e`의 이전 작업은 이미 커밋된 기준으로 취급했다. 기존 `.codex/config.toml` 변경과 미추적 파일은 그대로 두었다. 설치 훅을 호출하지 않았고, 아래 빌드는 저장소의 `bin` 출력만 만들었다.

## 검증 결과와 한계

| 검사 | 결과 | 근거 |
|---|---|---|
| Dev | 종료 0, 오류 0, 경고 35 | `decoengine4-evidence/build-dev.log` |
| Player | 종료 0, 오류 0, 경고 33 | `decoengine4-evidence/build-player.log` |
| PlayerAuto | 종료 0, 오류 0, 경고 35 | `decoengine4-evidence/build-playerauto.log` |
| `py -3 tools/DecoEngineCheck/run.py` | PASS, 단언 359,107개, 내부 비교 7,111개, mismatch=0 | `decoengine4-evidence/ownership-check.log` |
| 변경분 공백 검사 | `git diff --check` 통과 | 기존 설정 파일의 CRLF 안내는 별도 |
| 결과 문서 인코딩 | apply_patch 작성, UTF-8 BOM `EF BB BF`, 엄격 해석 성공, 대체문자 0, 한글 제목 일치 | Get-Content -Encoding UTF8 및 `decoengine4-evidence/result-encoding.json` |
| 게임 DLL | SHA256 `6BA2CFB8D260962D3C8A3E4409F60E8E98293CA9B2D2ECA4875096BD02D40230` | 이번 IL 스캔 대상 |
| 설치된 StutterFix DLL | 작업 중 확인한 두 SHA256 동일: `DC9F974BAB9D4CF335755D91953465B0DF3BCD9690F6DF1501C75128F1F5F5BF` | 빌드 전·후 읽기 전용 확인 |

빌드 경고에는 참조 버전 충돌, 사용 중단 API, 미사용 필드가 있다. 경고 0이라고 해석하면 안 된다. 검사 로그의 `fallback InvalidOperationException`은 주입한 Eval 예외를 회수하는 회귀 사례다.

오프라인 검사는 실제 `DecoEngine.cs`를 연결한다. 이번에는 실제 `Step`, `StepPair`, `Startup`, `ApplyE`, `OnUpdate`, `OnComplete`, `PositionUpdate`, replay 래퍼와 `EngineReason`도 소스에서 추출해 연결했다. 게임/native setter·이징·일부 eligibility 의존성은 대역이다. 따라서 위 mismatch=0은 **.NET 8 오프라인 대역 환경**의 결과이며 Unity Mono 반올림, SpriteRenderer/Transform native 부수효과, Harmony 실행 순서, 화면 동일성 또는 성능을 입증하지 않는다. 실게임 `decoengine verify`와 전곡 `decosig`는 실행 금지 조건에 따라 미실행이다.

추가 사례: 알파 0 종점, 같은 프레임 색→위치/위치→색, OnUpdate의 0 뒤 OnComplete의 양수, opacity 0, 보임→숨김→보임, 숨김 상태의 새 색·크기 Startup, 같은 Rec 200회 교체, X/Y 짝과 색 경계, 다른 슬롯 해제 후 인덱스 유지, 비유한 값의 영구 제외, manager heartbeat 중단, replay 예외 후 context 복구·중복 진행 방지. 기존 128개 무작위 5채널·400프레임 대조도 유지했다.

## 1. 영구 소유와 숨김 순서

- 한 번 입장한 `State`는 마지막 transform Rec가 완료되어도 유지한다. `active` 인덱스는 유지하며, 예외·지원하지 않는 변경으로 빠진 자리는 null로 남긴다. 다른 장식을 끝 슬롯으로 옮기지 않는다. `owned` 카운터는 살아 있는 State 수이고 `slots`는 빈 자리까지 포함한 길이다.
- 알파 전환은 `Release`/`Admit`로 처리하지 않는다. 알파가 현재 프레임 내내 안전한 경우는 기존 Advance를 사용한다. 알파가 0을 지나거나 새 resident 기록의 Startup이 필요한 경우는 최대 5개 transform 슬롯과 색·opacity를 **EngineOrder의 세 정렬 스트림**으로 병합해 기존 Step을 한 번씩 호출한다. scratch 배열·참조 정렬·새 List 할당은 없다.
- 색·opacity는 레거시 목록에 남아 있다. replay에서 진행한 기록에는 `EngineSkipFrame`을 적어 이어지는 레거시 루프가 다시 진행하지 않게 한다. 숨김 상태에도 시계와 필드는 계속 진행한다. 원래 setter가 하던 즉시 쓰기·lazy 저장·보임 복귀 시 flush를 그대로 사용한다.
- replay 중 보이는 장식의 `MoveApply.SkipForLate`는 현재 State를 참조해 `EngineDefer`의 프레임 정수로 등록한다. 해당 경로에서는 `lateSkippedSet` 해시 등록이 없다. `Replaying` context는 `finally`에서 이전 값으로 복구한다. 기존 InvisibleSkip의 lazy/hidden 집합 등 **게임·모드 전체의 모든 해시를 없앤 것은 아니다.**
- `TightLate`의 숨김 reason 3도 State를 반환하지 않게 했다. 숨김을 허용한 구조 검사를 다시 수행하므로 hitbox·mask·mesh·follow·lock·parallax·부모 조건은 유지한다. Dormancy가 숨김 장식을 순회 목록에서 빼더라도 유지되도록, Update에서는 entry의 마지막 보임 프레임 대신 manager의 마지막 완료 프레임을 확인한다.
- 구조 변경, 비유한 알파, shadow/batch 충돌, 예외 등 불확실한 상태는 `Entry.EngineRejected`를 영구 제외 비트로 사용한다. 이 값은 레벨 entry 표가 초기화될 때까지 유지된다. 안전 조건이 나중에 회복되어도 해당 entry는 재입장하지 않는다. `permanent-excluded`, `alpha-fallback`, `reasons`로 확인한다.

### IL과 기존 모드 경로로 확정한 처리 표

범위는 정확히 `scrVisualDecoration`, mask/hitbox/follow/stick/lock 제외, 일반 배치·고정 offset·유효 parallax이다. `GetVisible`은 이 범위에서 rendererEnabled와 rendererColor.a의 0 여부로 결정된다. 음수 알파와 NaN의 의미를 단순히 `>0`으로 대체하지 않았다.

| 실행 시점 | 필드/Transform 처리 | 이번 구현 |
|---|---|---|
| 보이는 상태의 위치 setter | `SetPosition`이 child 위치·pivot 필드·screen clamp 값을 처리. `UpdateNow`는 SkipForLate에 등록하면 최종 위치를 Late로 미룸 | replay에서는 원래 setter 사용, 등록만 State의 정수 프레임으로 대체 |
| 보이는 상태의 회전·크기 | `DeferTransform`이 true이면 필드만 저장, Late의 UpdatePosition 상당 계산이 최종 Transform을 씀 | 원래 Step/DeferTransform 유지 |
| 숨김 상태의 위치 | SkipForLate는 false. InvisibleSkip의 LazyPrefix가 적용되면 pivot 필드·lazy 표시만 저장하고 child/UpdatePosition은 미룸. lazy 자격이 없으면 원래 SetPosition/UpdateNow가 즉시 처리 | 원래 setter·LazyPrefix를 그대로 실행. 숨김을 무조건 필드 저장만 하는 것으로 치환하지 않음 |
| 숨김 상태의 회전·크기 | DeferTransform false이므로 원래 SetRotation/SetScale이 즉시 Transform을 씀 | 원래 Step에서 그대로 실행 |
| 위치 실행 당시 보였고 이후 색이 0이 됨 | manager의 LogicUpdate는 최종 숨김 장식의 UpdatePosition을 생략. 앞서 등록한 deferred 위치는 manager postfix가 한 번 보정 | 기존 EngineLatePostfix를 유지 |
| 색이 먼저 0이 되고 이후 위치 실행 | 그 위치는 숨김 setter/lazy 분기를 탐 | 생성 순서 병합으로 보존 |
| 최종 상태가 보임 | manager Late에서 최종 필드로 UpdatePosition 상당 계산과 Transform 쓰기 | 기존 TightLate 공통 Apply 사용 |

근거: 기존 `decoengine2-evidence/GetVisible-il.md`, `game-methods-il.md`, `UpdatePosition-il.md`, `ApplyColor-il.md`와 현재 `MoveApply.cs`, `InvisibleSkip.cs`, `DecoAnim.cs`. 이번 DLL에서도 `guards-game.txt`, `decoration-methods.txt`, `visual-methods.txt`를 다시 추출했다.

전역 생성 ID가 연속인 X/Y는 기존 `PairXY`, `Prof`, `PairAllowed` 조건으로 실제 StepPair를 실행한다. 색·opacity·다른 장식 기록을 가로질러 짝짓지 않는다. 중간 기록이 이미 완료·삭제되어 현재 레거시 목록에서는 붙어 있지만 생성 ID가 연속이 아닌 경우는 개별 Step이다. 이 경우 최종 값 동일성은 기존 SetPosition의 마지막 인자 결정성과 offset 불변 조건에 의존하며, setter 횟수는 늘 수 있다. native 부수효과와 비용은 실게임 확인 대상이다.

## 2. child localPosition dirty 전환: 보류

요구한 **평상시 native 읽기 0은 달성하지 않았다.** 입장 때의 실제 위치 검사, 매 프레임 관리 필드 비교, 일반 모드의 16프레임 간격 child 검사, verify의 매 프레임 검사를 유지했다. 따라서 기존 native-only 변경의 최대 15프레임 감지 지연도 남아 있다. 새로 해결했다고 판단하면 안 된다.

이번 `DECO_GUARDS=1` 및 메서드 목록의 결과:

| 쓰기 경로 | 확인 결과 | dirty 전환 판단 |
|---|---|---|
| SetPosition, SetPositionX/Y, SetPivotX/Y | SetPosition에서 childTransform.localPosition 쓰기. X/Y·pivot setter가 이곳으로 연결 | 해당 managed setter 훅은 직접 경로만 포착 |
| SetRotation / SetScale | pivot의 회전/크기를 쓰며 child.localPosition 쓰기는 없음 | 여기에 훅을 붙이면 고빈도 호출 비용만 증가할 가능성 |
| SetSprite 두 오버로드 | sprite 갱신과 ResizeCollider, 직접 child 위치 쓰기는 없음 | 이 함수 훅만으로 alias writer를 포착할 수 없음 |
| CreateDecoration, SetType | childTransform 참조 저장 | 관리 참조 비교로 감지. 정확한 visual 외 타입은 기존 제외 |
| SetTextureScaleMultiplier / MoveDecorationsPlus | textureScaleMultiplier / croppedPivotOffset 변경 | 기존 관리 값 비교로 감지 |
| 에디터 기즈모 | 일반 Transform 함수와 다른 target 참조를 사용 | 엔진은 편집기 진입 시 RestoreAll. 기즈모 훅 추가하지 않음 |
| 일반 Transform helper, DOTween 내부 writer, 다른 모드의 alias·캐시·리플렉션 | 장식 setter를 거친다는 보장 없음. 정적 IL 직접 참조 목록은 객체 별칭의 증명이 아님 | 장식 setter dirty만으로 전수 포착 불가 |

`SetPivotOffset`라는 이름의 장식 setter를 가정하지 않았다. 현재 DLL의 관련 경로는 `SetPivotX/Y → SetPosition`이다. 기존 Mods IL 감사(`decoengine-evidence/guards-mods-20261019.txt`)는 참고 자료이며 이번에 모든 외부 모드를 다시 감사한 결과가 아니다.

**추가 Harmony 훅 0개**, 추가 setter Dictionary 조회 0개. 새 ID·이벤트·PlayerLoop 설치가 없어 추가 해제 훅도 없다. 기존 `Main.Unload → DecoEngine.Uninstall`과 `SceneReset → TightLate.Forget / DecoAnim.DropAll`에서 영구 슬롯과 entry를 정리한다. 외부 setter 패치/PACL 제외 조건은 유지했다. 임의 alias 쓰기를 완전히 감지할 근거가 생기기 전에는 주기 검사를 삭제하지 않는 쪽으로 남겼다.

## 3. 색 직접 처리: 설계만, 구현 보류

IL상 `scrDecoration.SetOpacity`는 opacity 저장 후 가상 ApplyColor로 간다. visual ApplyColor는 `rendererColor = color.WithAlpha(color.a * opacity * floorFactor)`와 SpriteRenderer.color 저장을 수행한다. 이번 자격에서는 stickToFloor=false이므로 floorFactor는 1이지만, **곱셈 순서와 성분별 보간 식**까지 유지해야 한다. 완료 프레임에는 보간값 OnUpdate 뒤 정확한 CEnd/FEnd OnComplete가 각각 적용된다.

엔진에서 색·opacity를 소유하려면 위 두 적용을 현재 최대 7기록 병합 순서에 포함하고, `CStart`의 성분별 `+= CChange * e`, scalar 식, 시작·완료·Kill(true) 의미를 각각 검증해야 한다. rendererColor가 같아도 기존 `ApplyColor` 패치가 InvisibleSkip/LazySprite의 숨김·lazy flush 등을 수행하며 SpriteRenderer native setter 부수효과까지 동등하다는 근거가 없다. 따라서 SetColor/SetOpacity→ApplyColor 사슬 및 같은 값의 ApplyColor 호출을 건너뛰지 않았다.

현재 replay는 이 사슬의 **순서와 한 번 진행**을 보존하는 경로다. 색 직접 처리로 부를 수 없다. 특히 새 OnUpdate에서 색이 0이었다가 OnComplete에서 다시 양수가 되는 경우를 한 번의 최종 색 저장으로 합치면 안 된다.

## 4·5. Compact와 효과 교체

- 기존 state의 `compact`는 레거시 전체 Compact 호출 횟수가 아니라 **장식 내부의 최대 5개 슬롯 압축 횟수**다. 실제 완료/중단 슬롯이 있을 때만 실행하도록 기존 Advance 조건을 유지했고, replay도 같은 조건으로 했다. 정상 프레임에는 슬롯 참조 재저장·Array.Clear·Mask 재계산을 하지 않는다. 실제 이벤트 완료에 의한 compact까지 0으로 만들지는 않았다.
- Admit가 실제로 레거시 목록에서 resident 기록을 뺀 경우에만 EngineDirty를 세운다. 기존의 무조건 EngineDirty를 제거했다. `admission-scans`는 Admit 본문 진입 횟수, `list-compactions`는 그곳의 실제 목록 제거 횟수다. 레거시 Step의 완료·Kill·오류 자체가 세우는 fastDirty는 유지한다.
- NewRec의 기존 tween Dictionary 읽기에서 얻은 이전 Rec의 `OwnerHint`를 검증해 State를 찾는다. 유효한 힌트이면 추가 owned 조회 0이며 `boundary-hint/hash`의 앞 수가 증가한다. 힌트가 없거나 무효하면 owned 조회 한 번이 뒤 수에 쌓인다. 이미 있던 tween Dictionary 자체의 접근까지 없앤 것은 아니다.
- BindNew는 최대 5슬롯 안에서 생성 순서대로 새 transform 기록을 바로 넣는다. 장식 수와 무관한 O(1) 상한이며 State 인덱스는 유지한다. 첫 Startup은 기존 슬롯·색·opacity와 순서 병합해 실행한다. 기존 scale 시작값을 미리 고정하지 않는다.
- 힌트는 pooled State가 다른 장식 또는 아직 입장하지 않은 후보로 재활용된 경우도 검사한다. 효과 교체 회귀 검사에서 발견한 후보의 LateEntry=null 경우를 보강했다.
- **Admit의 전체 레거시 목록 스캔은 남아 있다.** OwnerHint는 해당 resident 기록 조회를 줄이지만, 새 장식 발견·다른 기록 검사·목록 제거의 전체 비용을 모두 O(1)로 바꾼 것은 아니다. 영구 슬롯의 누적 크기와 idle 슬롯 순회 비용도 새 성능 위험이다.

계측은 카운터만 추가했다. 장식별 Stopwatch는 없다. 검토에서 지적된 verify 성공 검사당 `"key=" + r.Key` 문자열 생성을 불일치 때만 수행하도록 바꿨다. verify off의 이득으로 계산하면 안 된다.

## 예상 이득 — 모두 추정, 실측 아님

기준 창은 인계 문서의 3,031프레임이다. alpha-fallback 16.2k는 약 5.34회/프레임, child-reads 232k는 약 76.5회/프레임, compact 15.6k는 약 5.15회/프레임이다. 각각의 단가는 이번 작업에서 측정하지 않았다. 다음 수치는 **명시한 단가를 가정한 예산**이며 예상 성능 보장이 아니다.

| 항목 | 가정과 µs/프레임 추정 | 이번 반영 |
|---|---|---|
| alpha exit/re-admit 제거 | 한 왕복 비용을 10~40µs로 가정하면 5.34×단가 = 약 53~214µs 절감. replay의 기존 setter 비용과 별도 신규 idle 순회 비용을 빼기 전 | 구현, 실게임 목표 alpha-fallback <100/창은 미확인 |
| child 읽기 완전 제거 | 한 native 읽기 0.1~0.3µs 가정 시 약 8~23µs. 입장 감소에 따른 부수 감소와 분리 불가 | 보류, 직접 절감 예산 0 |
| 색 직접 처리 | 색 기록 2,000회/프레임, 사슬 절약 0.05~0.15µs/회 가정 시 100~300µs | 보류, 직접 절감 예산 0 |
| 불필요한 EngineDirty/전체 정리 | 제거되는 전체 스캔 1회/프레임당 1~10µs 가정이면 1~10µs. 실제 제거 빈도에 곱해야 함. 5.15회/프레임의 슬롯 compact와 혼동 금지 | 일부 구현, 완료 기록 정리는 유지 |
| 효과 경계 owned 힌트 | 기존 입장 빈도 33.6k/3,031≈11.1회/프레임을 대용하고 추가 해시 0.03~0.1µs 가정 시 약 0.3~1.1µs | 일부 구현, 새 입장·전체 Admit 스캔 제외 |

상쇄 비용의 추정: 추가 idle 슬롯 10,000개를 훑는 비용을 슬롯당 0.005~0.02µs로 가정하면 50~200µs/프레임이다. 숨김 replay가 자주 실행되면 원래 setter·lazy 처리 비용도 지속된다. 생성 ID가 연속이 아닌 X/Y의 개별 Step은 호출 수가 늘 수 있다. **항목별 예산을 더해서 3.02ms→특정 수치로 개선됐다고 결론 내릴 수 없다.** 인계의 이론적 −1.2ms도 이번 결과가 아니다.

## Claude가 실행할 순서

1. 별도 새 프로세스 정확성 판에서 엔진을 켜고 `decoengine verify`, Arche·HELLO 전곡을 실행한다. mismatch=0, 완료·중단·Kill(true)·효과 재시작·알파 0 경계와 숨김 복귀를 확인한다. 곡 끝 `decosig`를 기준판과 비교한다. 이 판은 성능 수치에서 제외한다. 이번 Codex 작업에서는 이 절차를 실행하지 않았다.
2. 검증이 맞을 때만 동일 빌드·설정·해상도·주사율로 **매 판 새 프로세스 ABBA**를 실행한다. A는 엔진 끔, B는 켬. verify와 stats는 off. DLL reload/같은 실행 재활용으로 대체하지 않는다.
3. 각 판에서 `waitsongpos 252` → `decoengine state` → `fuse begin` → `waitsongpos 273` → `fuse end` → `decoengine state`. 명령 응답의 실제 곡 시각도 남긴다. 긴 대기는 명령 제한 안으로 나눈다.
4. 두 state의 차이로 admissions, restores, alpha-fallback, permanent-excluded, retained-alpha-frames, startup-replay-frames, replay-steps, admission-scans, list-compactions, boundary-hint/hash, child-reads, compact, late-fixup을 비교한다. `owned`와 `slots`는 누적 카운터가 아닌 현재값이며, `owned`에는 완료 뒤 남은 idle State도 포함된다. 원래의 ownership%만으로 성공을 판단하지 않는다.
5. 목표는 해당 창 alpha-fallback <100, mismatch=0, decosig 동일, 모드 Update+manager Late 합과 끊김 악화 없음이다. permanent-excluded가 늘어서 수치만 줄어든 경우는 성공으로 세지 않는다. ReplayFrame/StepPair·lazy 경로와 커진 idle 슬롯 비용도 같이 확인한다.
6. 별도 진단 판에서 `decoengine stats on` → `waitsongpos 252` → `decoengine state` → `fuse begin` → `profsamp begin 1000` → `waitsongpos 273` → `profsamp end` → `fuse end` → `decoengine state` → `decoengine stats off`. ReplayFrame, Advance, Admit, Stable, setter/lazy 분포를 확인한다. 표본 수집·stats 비용이 섞이므로 깨끗한 ABBA 결과와 분리한다.
7. B가 유리하더라도 전곡·숨김 재등장·중도 재시작까지 확인한 뒤 Claude가 채택을 판단한다. 그 전까지 기본 끔을 유지한다.
