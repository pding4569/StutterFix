# Codex 작업 I: DecoEngine — 보이는 이미지 장식의 위치·회전·크기 애니메이션 + Transform 갱신을 "장식 하나 = 한 번의 계산"으로 완전 대체 (설계 먼저, 그다음 1단계 구현) — 게임 실행·Mods 설치·커밋 금지

결과: `docs/handoff/codex-decoengine-20261019-result.md` (**apply_patch 로 UTF-8 BOM, 쓴 뒤 `Get-Content -Encoding UTF8` 로 한글 확인**), 한국어, 추측은 "추정". 빌드 셋 오류 0. 켜기: 모드 폴더 `decoengine-on` 파일(기본 끔). 검증 명령 `decoengine verify|state|bench`. **화면·게임 결과는 현재 경로와 비트 동일.**

## 왜 이 구조인가 (모든 수치는 Claude 실측, Arche 곡 257.8~278.8초, 2560×1440 120Hz, 판마다 새 프로세스 A/B 와 표본 프로파일 `profsamp`)

현재 구조(켠 상태 기준)에서 프레임당 비용: `DecoAnim.UpdatePrefix` 1.87~1.91ms(위치 쌍 0.71 + 회전 0.22 + 크기 X·Y 0.29 + 색 0.46 + `Compact` 0.25 + 뼈대 0.30), `scrDecorationManager.LateUpdate` 1.03~1.06ms(`TightLate`: 장식마다 `parallax.SetTrans` 상당 계산 후 Transform setter 3개; 더미 벤치 장식당 0.29µs). 호출 사슬(`Step`→`Apply`→`OnUpdate`→`DeferTransform`→`SkipForLate`→`RegisterLate` 해시 등)이 기록마다 반복된다.
**지난 시도들의 교훈**(`docs/optimization-36.md` 끝부분): (1) 순수 계산을 상주 배열로 일괄 처리하는 그림자(`DecoBatchShadow`)는 기록당 0.035µs(실제 Mono, 값 비트 동일 3.9M번 확인)로 아주 쌌다. (2) 그러나 이것을 **기존 경로와 병합해서**(순서 번호로 섞어 가며 `StepPair`·`setPos` 체인·`DeferTransform` 판정·필드 쓰기를 그대로 호출) 제품에 통합한 `DecoAnim.Batch.cs` 는 창 구간 기록당 0.52µs 로 15배가 됐고 전체가 2.44 vs 1.84ms 로 느려졌다. (3) 기록 묶음(GroupStep v1·v2)·`SetPosition` 생략(PosFast)·판정 캐시/인라인(FastLane)·해시 조회 줄이기도 이득이 없거나 오차 이내였다. → **부분 치환은 소용없다. 기록 단위 사슬과 장식 단위 사슬을 둘 다 없애고 한 장식을 한 번에 처리해야 한다.**
`TightLate`(성공, +8%)가 보여 준 방식: 정적 정보 캐시 + 얇은 루프, 비교 도구로 원래 경로와 비트 비교, 장식별 제외 정책.
Mono float 의미(`floatsem` 실측): 한 식으로 쓴 float 식은 double 정밀도로 계산되고 저장 시점(지역 변수·필드·반환)에만 float 로 반올림된다(`s + c*e` 한 식 = `(float)((double)s + (double)c*(double)e)`). 식을 쪼개면 결과가 달라진다. .NET 8/C++ strict 계산은 이 게임에서 비트가 다르다 → 값 계산은 **현재 코드(`ApplyE`, `TightLate.Apply`)와 같은 식 모양·같은 `ZeroTween.Eval` 호출**로. 네이티브 계산 금지.

## 목표 구조: 장식 단위 엔진

대상 장식(eligible): 정확히 `scrVisualDecoration`, 마스크 아님, 히트박스 없음, 메시 렌더러 꺼짐, 보임(`rendererEnabled && rendererColor.a != 0`), `followPlanet`/`stickToFloor`/`lockRotation`/`lockScale` 없음, 시차 `clampToScreen`/`dontAlterX/Y` 없음, 부모 단위 변환(= `TightLate.Eligible` 조건과 같음; 그 코드를 재사용/공유). 대상 기록: 키 1·2·5·7·8. 색(9)·불투명도(10)·피벗(3·4)·시차(11~13)는 기존 `recs` 경로로 둔다(장식당 기록 중 일부만 새 엔진이어도 되도록: 한 장식의 키 1·2·5·7·8 기록은 **전부 새 엔진**, 나머지 키는 기존 경로 — 같은 장식에서 둘이 서로의 값을 읽는지(예: 색 단계가 알파를 바꿔 보임 판정이 변함) 확인).

새 엔진이 한 프레임에 장식 하나에 대해 하는 일(순서는 현재 한 프레임의 효과와 같아야 함):
1. 그 장식의 활성 기록(최대 5개: PosX, PosY, Rot, ScaleX, ScaleY; 서로 다른 (Pos, Dur, Ease)일 수 있음)을 진행: `Pos += td`, 끝 판정, `e = ZeroTween.Eval(...)`(같은 (Pos,Dur,Ease,Over,Period)면 재사용), 값 = `ApplyE` 와 같은 식.
2. 끝난 기록은 현재 `OnComplete` 와 같은 결과(끝값 `FEnd`, `Running=false`, `Completed++`) 후 슬롯 해제.
3. 장식의 최종 상태 `pivotPosVec`(X,Y 축별), `rotAngle`, `scaleVec` 를 **프레임 상태 구조체**에 갱신하고(필드 쓰기는 하지 않음 — 아래 게으른 동기화), 그 값으로 `TightLate.Apply` 와 같은 식(시차·`parallaxOffset`·`camScale`)으로 Transform 위치·회전·크기를 계산해 setter 3개(`pivot.position`, `pivot.rotation`, `pivot.localScale` — `SetPositionAndRotation` 으로 합치지 않음: 비트 동일 확인된 방식 그대로)로 쓰고 `parallax.startPosition/posCamAtStart/endPosition` 필드도 같은 값으로.
   → 지금처럼 DecoAnim(Update 단계)에서 필드에 쓰고 LateUpdate 에서 읽어 다시 계산하는 두 단계를 **한 번의 계산·한 번의 Transform 쓰기**로.
   카메라 위치는 `scrDecorationManager.LateUpdate` 가 한 번 읽는 값이므로, 엔진의 Transform 쓰기는 **LateUpdate 단계**에서(TightLate 자리) 하고, 기록 진행(값·끝 판정)은 **Update 단계**(`DecoAnim.UpdatePrefix` 자리)에서 한다 — 즉 두 단계로 나뉘되 둘 다 장식당 호출 사슬 없는 얇은 루프이고, 장식 상태 구조체를 거쳐 필드 읽기/쓰기·`DeferTransform`·`SkipForLate`·`lateSkippedSet`·`Dormancy.Wake` 해시를 **하지 않는다**. (이 사슬이 표본 프로파일의 비용 대부분: HashSet Add/Remove/Contains 합 7%, 유니티 null 검사 2%, GC 쓰기 장벽 3%, `List.set_Item`/`stelemref`.)

**게으른 필드 동기화(핵심 설계)**: 새 엔진이 소유한 장식은 `pivotPosVec`/`rotAngle`/`scaleVec`/`parallax` 필드를 프레임마다 쓰지 않는다. 대신 그 필드를 읽는 코드가 실행되기 직전에 동기화한다. 읽는 코드를 IL 전수로 목록화하라(`tools/ilscan` `FIELDS=1`, 모든 어셈블리와 Mods 의 DLL: `scrDecoration.pivotPosVec`/`pivotOffsetVec`/`rotAngle`/`scaleVec`/`parallax.startPosition`/`posCamAtStart`/`endPosition`, `scrParallax.GetPositionWithParallax`, `GetDecorationWorldSize`, 편집기 기즈모, PACL2 `FollowDecoration`/`OrbitDecoration`, 모드 `MoveApply`/`InstantMove`/`FastMove`(효과 시작 `Startup` 이 현재 값 읽음)/`DecoSig`/`TileSig`/`DecoAnim.Startup`, 그리고 `Kill(true)`/`Complete` 경로). 이 읽기들에 대해 (a) 읽기 직전 동기화 훅(Harmony prefix 를 읽는 메서드마다 — 개수가 적으면) 또는 (b) 읽는 쪽이 이미 모드 코드(FastMove 등)면 직접 호출, (c) 외부 모드가 읽는 경우(PACL2 등)는 호환성 검사(`Harmony.GetPatchInfo`/IL 목록으로 해당 모드가 로드돼 있으면 그 장식군은 새 엔진에서 제외 — `PairAllowed()` 방식)로 처리. 동기화 대신 "프레임 끝에 필드를 쓰되 해시 없이 직접 쓰기"(= 지금 `DeferTransform` 의 필드 쓰기 비용만 남김, 해시·판정 사슬은 제거)를 **1단계의 단순 대안**으로 먼저 구현·측정해라(게으른 동기화는 2단계). 두 대안의 프레임당 비용과 위험을 설계 문서에 비교하라.

수명·정합성: `KillPrefix`/`CompletePrefix`/`KillAllPrefix`/`NewRec` 재사용/`Complete`/`Compact`/`DropAll`/`SceneReset`/`ForgetLevel`/`Main.Unload`/곡 끝(`Hitch.SongEnded`)/Dormancy(깨움·재움)/`InvisibleSkip`(알파 0)/`LazySprite`/`ExitFix` 와의 상호작용, 그리고 효과 시작 시 새 `Rec` 가 생길 때(`FastMove.Run`/`NewRec`/`Startup`) 새 엔진 슬롯으로의 입장 시점(첫 `Startup` 후), 장식이 도중에 eligible 에서 빠지면(알파 0, 마스크, 메시 켜짐 등) 기존 경로로 안전하게 되돌리는 방법(상태 동기화 후 `recs` 로 복귀)을 설계 문서에 정리해라.

## 이번 작업 범위

1. **설계 문서**(결과 문서의 앞부분): 위 구조의 상세 설계. 장식 상태 구조체 필드, 슬롯/장식 인덱스 관리, 프레임 루프 두 개(Update 단계 진행, LateUpdate 단계 Transform), 게으른 동기화 대 프레임 필드 쓰기 비교, 읽는 코드 IL 전수 표, 수명 이벤트 표, eligible 판정과 이탈, 검증 계획, 단계 구분(1단계: 필드 프레임 쓰기 방식으로 완전 대체 구현 + 검증 + 벤치, 2단계: 게으른 동기화 — 이번에는 설계만), 예상 비용(프레임당 µs 예산: 기록 진행 장식당, Transform 계산 장식당) 대비 현재(위치 쌍 0.71 + 회전 0.22 + 크기 0.29 + `Compact` 일부 + TightLate 0.65 = 약 1.9ms/프레임)와 이득 추정.
2. **1단계 구현**(기본 끔): 새 `DecoEngine.cs`. 새 엔진이 소유한 기록은 `recs` 에서 뺀다(그래서 `Compact`·뼈대도 줄어든다). 기존 경로와 **순서 병합하지 않는다**(지난 DecoBatch 실패 원인): 한 장식의 키 1·2·5·7·8 은 전부 새 엔진이 소유하고, 같은 프레임에 기존 경로가 만지는 같은 장식의 다른 키(색 등)는 서로 독립임을 코드로 증명(필드 읽기 의존: 알파 → 보임 판정). 진행은 `UpdatePrefix` 에서 기존 루프 **앞**에서 일괄, Transform 은 `TightLate` 의 같은 매니저 루프 안에서(대상 장식은 `TightLate.Apply` 대신 엔진의 상태로 계산: 코드 재사용).
3. **검증 도구**: `decoengine verify`: 표본(1/16)의 장식에서 같은 프레임 같은 입력으로 **기존 경로(DecoAnim.Step→필드→TightLate/UpdatePosition)** 가 만들 값(키별 값, `pivotPosVec`/`rotAngle`/`scaleVec`, Transform position/rotation/localScale, parallax 필드)을 순수 계산으로 구해 비트 비교(필드 쓰기 없이 값끼리). 그리고 기존 `late-verify`/`tightlate verify`/`decosig` 가 새 엔진이 켜진 상태에서도 불일치 0. 통계: 소유 장식 수/프레임, 소유 기록 수/프레임, eligible 이탈 이유, 동기화·폴백 횟수.
4. **마이크로 벤치**: `decoengine bench`(더미 장식 2,150개 + 더미 `Rec`·Transform, `TightLate.Bench`/`GroupStep` 벤치 방식)로 Update 단계 진행 + LateUpdate 단계 Transform 합의 장식당 µs. 지난 두 번의 교훈: **벤치만 믿지 말 것** — 결과 문서에 실전 A/B 판단 기준을 적어라(현재 `fuse` 의 "모드 애니메이션 갱신" 1.87ms + "장식 관리자 LateUpdate" 1.04ms = 2.9ms 를 기준으로 새 엔진이 합 1.8ms 이하면 채택 가치).
5. 계측 확장: `fuse`(FuseProbe)의 "모드 애니메이션 갱신" 과 "장식 관리자 LateUpdate" 에 새 엔진 시간이 포함되게(`DecoAnim.UpdateMs`, 매니저 prefix/postfix 범위 확인), `decosamp`·`profsamp` 로 새 엔진 함수가 표본에 이름으로 보이도록(별도 비인라인 메서드로, `[MethodImpl(NoInlining)]` 로 프로파일 가시성 확보).

## 하지 말 것 / 주의
- 네이티브(C++) 계산 금지. `Quaternion.Euler`/Transform setter 는 지금 `TightLate.Apply` 가 쓰는 호출 그대로(비트 동일 검증됨).
- 기존 최적화(TightLate, PairXY/StepPair, DeferTransform, LazySprite, Dormancy, InvisibleSkip)는 켜진 채 호환. 새 엔진이 켜진 장식은 그 최적화 경로를 타지 않을 뿐 결과는 같다.
- 어떤 조건이든 불확실하면 그 장식/기록은 기존 경로(제외). 예외는 그 장식만 기존 경로로 풀어 처리.
- 결과 문서에 Claude 가 돌릴 순서(`decoengine-on` 파일 켬/끔 새 프로세스 ABBA + `fuse begin/end` + `profsamp` + `decoengine verify` + 곡 끝 `decosig` 대조 + HELLO/Arche 전곡)를 적어라.
