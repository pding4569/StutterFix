# Codex 작업 H: DecoBatch 2단계 — 상주 배열(SoA) 일괄 처리의 제품 구현(기본 끔) — 게임 실행·Mods 설치·커밋 금지

선행 결과: `docs/handoff/codex-decobatch-20261019-result.md`(그림자 `DecoBatchShadow.cs`). 결과: `docs/handoff/codex-decobatch2-20261019-result.md` (**apply_patch 로 UTF-8 BOM, 쓴 뒤 한글 확인**), 한국어, 추측은 "추정". 빌드 셋 오류 0. 켜기: 모드 폴더 `decobatch-on` 파일(기본 끔, Claude 가 A/B 로 이득과 비트 동일을 확인한 뒤 기본값을 바꾼다). 검증 명령 `decobatch verify|state`. **화면·게임 결과는 현재 경로와 비트 동일해야 한다.**

## 1단계 그림자 실측 (Claude, Arche 곡 240~279초, 실제 Mono)

- 상주 SoA 일괄 계산이 매우 싸다: 관리 코드 한 루프 0.125ms/프레임(전체 기록: 슬롯 약 700~14,000), 네이티브 0.032ms, 유지(등록/해제) 0.049ms, 읽기 전용 필드 접근 0.11ms. 지금 같은 일을 하는 모드 코드(`decosamp` 프로덕션 표본): 위치 쌍 0.71ms + 회전 0.22ms + 크기 X·Y 0.29ms + `Compact` 0.25ms + 루프 뼈대 0.30ms = 약 1.8ms/프레임. → 이 일을 일괄 처리로 대체하면 프레임당 약 1.3ms 절약 가능(추정, 예상 FPS +13%).
- **하지만 그림자의 값이 실제와 비트가 다른 경우가 있었다**: 관리 코드 2.7%(631,530/23.5M), 네이티브 15%(3.56M/23.5M). 첫 예: key=1 Linear, Pos/Dur 같음, 기대 −3310.1228 (0xC54EE1F7) vs −3310.12256 (…F6): 1 ulp.
- **원인(Claude 가 게임 안에서 `floatsem` 명령으로 확인)**: 이 게임의 Mono 는 float 식을 한 식으로 쓰면 **double 정밀도로 계산하고 지역 변수·필드·반환 등 저장 시점에만 float 로 반올림**한다. `s + c*e` 한 식은 `(float)((double)s + (double)c*(double)e)`(곱셈을 반올림하지 않는 FMA 와 같은 결과)와 2,000,000/2,000,000 일치, 엄격 단정밀도(곱셈을 float 로 확정한 뒤 더함)와는 78%만 일치, 식을 `float m = c*e; return s+m;` 로 쪼개면 엄격과 같아진다(78% 일치만 식 한 줄과 같음). 나눗셈 `t/d` 는 일반=엄격 100%, OutCubic(`t=t/d-1; t*t*t+1`) 일반 vs 엄격 70%만 일치. 즉 **.NET 8/MSVC /fp:strict 계산은 이 Mono 와 다르다**. 그림자의 오프라인 검증(1,300만 번 불일치 0)은 .NET 8 이었다. 네이티브 C++ 는 이 Mono 의 저장 시점별 정밀도 규칙을 정확히 재현하려면 DOTween.dll IL 의 저장 지점을 하나하나 맞춰야 해서 위험하다.
- **결론/설계 원칙**: 값 계산은 **현재 경로와 같은 코드·같은 식 모양**을 써야 비트가 같다: 이징 값 `e` 는 `ZeroTween.Eval(ease, pos, dur, over, period)`(DOTween 의 `EaseManager.Evaluate` 델리게이트를 그대로 호출 — `ZeroTween.cs`) 를 그대로 부르고(호출당 약 0.03~0.05µs), 값은 `ApplyE` 와 같은 모양의 **한 식**(`r.FStart + r.FChange * e`, 크기 `v.x = VStart.x + VChange.x * e`, 색 `c.r += CChange.r * e` 등. `DecoAnim.cs` `ApplyE` 의 연산자 모양을 글자 그대로)으로 쓴다. SoA 배열 원소를 쓰더라도 식의 구조가 같으면 같은 JIT 규칙. 네이티브 계산은 이번 단계에서 쓰지 않는다. 이 원칙이 맞는지는 `decobatch verify`(아래)가 증명해야 한다 — 불일치가 생기면 식 모양을 `ApplyE` 와 완전히 같게 맞추는 방법을 먼저 시도.

## 목표 구조 (기본 끔, 플래그/기록 단위 하이브리드)

일괄 처리 대상 기록(**eligible**): 키 1·2(위치 X·Y: 한 장식의 쌍은 기존 `StepPair` 와 같은 한 번의 `setPos`/`PosFast.Try` 호출로 마무리), 5(회전), 7·8(크기)이면서 **그 프레임 그 장식이 `MoveApply.DeferTransform` 이 참**(보이는 이미지 장식, LateUpdate/TightLate 에 맡김 → 필드만 쓰면 되는 경우), 이징은 `ZeroTween.Eval` 이 처리하는 모든 Ease(지원 제한 없이 Eval 을 부르므로 Linear/InQuad/OutCubic 외 Bounce/Elastic 등도 가능하지만 Over/Period 필요 — 우선 `Over`/`Period` 를 배열에 담아 Eval 에 그대로 전달). 색(9)·불투명도(10)·피벗(3·4)·시차(11~13) 및 숨김·투명·마스크 장식·Dev/Prof/`Shadow`·decoablate 는 **기존 경로**(`recs` 목록 그대로).
- **상주 배열**: 기록 시작(`Startup` 이후 — 시작값/변화량이 확정된 시점)에 슬롯을 할당해 키, Ease, Over, Period, Dur, Pos, FStart/FChange(또는 VStart/VChange 성분), 장식 인덱스, 원 `Rec` 참조(관리 객체 배열)를 담고, **그 기록은 `recs` 목록에서 빼서 프레임 루프(`UpdatePrefix`)의 일반 순회(`Step`)를 타지 않게** 한다(그래야 `Compact`·루프 뼈대 비용도 줄어든다). 장식이 도중에 `DeferTransform == false` 가 되면(알파 0, 마스크 등) 그 장식의 일괄 기록을 `recs` 로 되돌린다(상태 일관성: 슬롯의 Pos/값을 `Rec` 에 동기화). `Rec` 가 여전히 정체성(`Proxy.id`)이므로 `KillPrefix`/`CompletePrefix`/`KillAllPrefix`/`NewRec` 재사용/`Complete` 경로는 `Rec` 로 들어오고, 일괄 슬롯 기록이면 먼저 슬롯 상태를 `Rec` 로 동기화(`Rec.Pos`, `FLast`/`VLast` 등)한 뒤 기존 `Complete(r)` 로직을 그대로 쓰고 슬롯을 해제.
- **프레임 처리**: `UpdatePrefix` 에서 기존 루프 앞/뒤에서 일괄 루프 한 번: 기록 슬롯을 **만든 순서**대로 순회. 순서 의존: 같은 장식의 위치 X·Y 쌍과 크기 X→Y(Y 의 시작값은 X 설정 뒤 `scaleVec` 에서 읽음)가 기존 순서와 같게 — 슬롯 순서를 `recs` 순서와 같게 유지(슬롯 삽입은 만든 순서 번호로 정렬 유지, 스왑 제거로 순서가 깨지면 안 됨: 필요하면 순서 보존 제거(앞으로 당기기)나 순서 번호 정렬 사용). 장식 필드(`pivotPosVec`/`rotAngle`/`scaleVec`)는 **프레임마다** 이 루프에서 쓴다(지금 `DeferTransform` 이 하는 것과 같게: 다른 코드·TightLate 가 같은 프레임 LateUpdate 에서 읽는다). `MoveApply` 가 하는 부수 효과(`SkipForLate` 의 `lateSkippedSet.Add`/`lateSkipped.Add`)는 장식당 한 번이면 되는지(같은 프레임 중복 등록이 무해한지 확인) 확인하고 장식당 한 번만.
- **완료**: 끝 판정(`Pos+td >= Dur`)에서 기존 `Step` 과 같은 순서: 값 계산 → 필드 쓰기 → `Running=false; Completed++; OnComplete(r)`(끝값 `FEnd` 쓰기, 위치 쌍은 `x = done ? FEnd : FLast`). 완료 기록은 슬롯 해제와 `InList=false`(`NewRec` 재사용 의미 유지: 목록에서 빠진 것만 재사용 — 일괄 기록은 처음부터 `recs` 에 없으므로 `InList` 의미를 일괄 슬롯 소유 여부로 대체하는 방식을 명확히 설계).
- **판정 호출 최소화**: `DeferTransform(d)` 는 장식당 한 번(그 프레임 그 장식의 모든 기록에 재사용). 단 같은 프레임 안에서 색·불투명도 기록(기존 경로)이 알파를 바꿔 판정이 뒤집힐 수 있으므로, 일괄 루프를 색 단계 **뒤**/앞 어느 쪽에 두는지에 따른 원래 순서(`recs` 순서: 보통 위치·회전·크기 뒤에 색)를 보존하는지 확인(원래: 한 이벤트의 기록은 [PosX, PosY, ..., Rot, ScaleX, ScaleY, ..., 색 …] 연속, 장식마다 색은 뒤). 판정 변화가 일괄·기존 경계에서 비트 차이를 만들지 않는 설계(예: 색 단계가 알파를 바꾸는 프레임은 해당 장식을 기존 경로로 처리) 를 정하라.
- 장식 인덱스 → `scrDecoration` 참조 배열, 장식 목록이 바뀌면(새 맵/재시작) 전부 무효화(`Dormancy.ForgetLevel`, `SceneReset`, `Hitch` 곡 끝, `UninstallAll`/`Main.Unload`).

## 검증 (필수)

1. `decobatch verify`: 일괄 루프가 계산한 각 기록의 값(FLast/VLast, 새 Pos, done)을 **같은 프레임 같은 입력으로 기존 `Apply` 경로**(순수 계산: `ApplyE` 와 같은 식을 `Rec` 필드로)로도 계산해 비트 비교하는 것을 1/16 표본으로, 불일치 개수·첫 예(키, Ease, 성분, 기대/실제 비트). 이 비교는 부수 효과 없이(필드 쓰기 없음) 순수 함수끼리 비교. 그 위에 (b) 장식 필드 결과(`pivotPosVec`/`rotAngle`/`scaleVec`)와 `setPos` 입력이 기존 경로 값과 같은지: 표본 장식에서 기존 `DeferTransform` 경로를 같은 입력으로 실행해 전/후 필드 비트 비교하는 방법(필드를 일괄이 쓴 직후 값 저장 → 같은 입력으로 기존 경로가 쓸 값을 계산해 비교).
2. 기존 도구가 계속 의미 있게: `late-verify`, `tightlate verify`, 곡 끝 `decosig` 켬/끔 대조(Claude 가 실행), `animstat`. 끈 상태(`decobatch-on` 없음)는 현재와 비트·성능 동일(플래그 확인은 시작 때 한 번).
3. 마이크로 벤치 `decobatch bench`: 더미 장식(`TightLate.Bench`·`GroupStep` 벤치 방식)과 더미 기록 2,150~4,500개로 일괄 루프의 기록당 µs(필드 쓰기 포함, 기존 `Step` 대비). 단, 지난 두 번 벤치는 좋았는데 실전에서 느려진 전례(GroupStep v2 의 실전 DecoAnim 1.88 → 2.07ms)가 있으니 **실전 A/B 에서 확인할 판단 기준**을 결과 문서에 적어라(`decosamp` 로 표에 안 잡히는 일괄 시간은 `decosamp` 에 일괄 루프 시간도 더하도록 계측 확장, `fuse` 의 "모드 애니메이션 갱신" 에 일괄 루프 시간이 포함되게 `LastFrameMs` 범위 확인).
4. 통계: 일괄 기록 수/프레임, 슬롯 수, 기존 경로로 돌아간 이유별(마스크/숨김/알파/Ease 미지원 없음/키 밖), 장식 판정 수, 완료 수, 동기화 횟수.

## 하지 말 것 / 주의
- 네이티브(C++) 계산 금지(이번 단계). 식 모양을 `ApplyE` 와 다르게 쓰면 Mono 규칙으로 비트가 달라진다(위 실측).
- 기존 최적화(DeferTransform, PairXY/StepPair, PosFast(기본 끔), TightLate, LazySprite, Dormancy)와 호환. NativeMotion(Linear 일부 네이티브)은 기본 끔: 켜져 있으면 일괄 처리를 끄고 기존 경로로.
- 안전 우선: 불확실한 조건은 기존 경로로. 예외는 그 기록만 기존 경로로 풀어 처리(묶음 전체가 아니라).
- 결과 문서에 Claude 가 돌릴 순서(`decobatch-on` 파일 켬/끔 새 프로세스 ABBA, `fuse begin/end`, `decosamp`, `decobatch verify`, 곡 끝 `decosig` 대조, 전곡 HELLO/Arche)를 적어라.
