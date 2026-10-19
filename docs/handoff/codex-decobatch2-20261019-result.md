# DecoBatch 2단계 구현 결과 — 2026-10-19

기본 끔인 관리 코드 상주 SoA 경로를 추가했다. 게임 실행 0회, Mods 설치 0회, 커밋 0회다. 네이티브 계산은 추가하거나 호출하지 않는다. 실제 Unity Mono 비트 동일성·화면 동일성·성능 이득은 아직 미측정이며, 기본값을 켤 근거로 삼으면 안 된다.

## 구현 파일

- `DecoAnim.Batch.cs`: 슬롯·장식 인덱스·생성 순서 관리, SoA 계산과 필드 쓰기, 기존 경로와의 순서 병합, 검증·상태·벤치 명령.
- `DecoAnim.cs`: 시작 시 플래그 읽기, Rec 슬롯과 순서 번호, UpdatePrefix 분기, 강제 완료·Kill·PlayingTweens·KillAll 연결, 진단 뷰, decosamp 계측.
- `Dormancy.cs`, `Hitch.cs`, `StutterFix.cs`: 새 맵·곡 종료·Unload 때 기록과 참조 정리. SceneReset은 기존 DropAll → KillAll 경로로 슬롯도 정리한다.
- `AutoTest.cs`, `StutterFix.csproj`: 명령 라우팅과 모든 빌드의 제품 파일 포함.
- `tools/GroupStepCheck/Program.cs`, `GroupStepCheck.csproj`: 실제 제품 Batch 파일과 UpdatePrefix를 연결하는 오프라인 회귀 확장.

기존 `.codex/config.toml` 변경과 기존 미추적 폴더·파일은 건드리지 않았다. 설치 훅은 실행하지 않았다.

## 켜기와 호환 조건

모드 폴더의 `decobatch-on` 파일을 설치 시점에 한 번 확인한다. 파일이 없으면 기존 Step/StepPair/GroupStep 순회를 사용하며 프레임마다 파일을 확인하지 않는다. 켠 경우에만 SoA 배열을 할당한다. 슬롯은 최대 65,536개이며 용량 초과 기록은 기존 경로로 처리한다.

Dev/Prof, Main.MeasureBuild, NativeMotion, GroupStep, 그림자 측정, decoablate, UpdateCostProbe 측정창에서는 제품 일괄 처리를 하지 않는다. 진행 중 이런 조건이 켜지면 기존 recs로 복원한 뒤 처리한다. 실제 비교는 Main.MeasureBuild=false인 PlayerAuto 빌드로 할 것. PosFast, PairXY, TightLate, LazySprite, Dormancy는 유지한다. 미지원 Ease 필터는 없고 Over/Period를 포함해 ZeroTween.Eval을 그대로 호출한다.

대상은 키 1·2·5·7·8이고, 정확히 scrVisualDecoration이며 마스크·숨김·알파 0이 아니고 MoveApply.DeferTransform이 허용하는 기록이다. 색·불투명도·피벗·시차 등 다른 키는 기존 경로로 간다. 추가 안전 조건으로 색 alpha와 opacity가 모두 양수여야 한다.

## 순서와 수명

첫 갱신은 기존 Step/StepPair로 수행한다. 따라서 Startup의 시작값·변화량 계산, 특히 크기 Y가 앞선 크기 X 처리 뒤 현재 scaleVec를 읽는 순서를 보존한다. 성공적으로 진행한 기록만 슬롯에 넣고 그 프레임 끝 Compact에서 recs에서 뺀다. 다음 프레임부터 슬롯의 Pos와 값이 주 상태다. 시작·변화·위치·길이·Ease·Over·Period·키·장식 인덱스·Rec 참조를 상주 배열로 유지한다. 장식 참조는 별도 인덱스 표와 참조 수로 관리하고 마지막 슬롯이 해제되면 놓는다.

기존 recs와 슬롯 스케줄을 BatchOrder로 병합해 원래 생성 순서대로 처리한다. 일괄 루프를 색 단계 앞이나 뒤로 몰아 이동시키지 않는다. 새 등록은 pending 목록에 담고 프레임 끝에 정렬하여 현재 순회를 바꾸지 않는다. 해제는 빈 자리로 표시하고 순서를 유지하여 정리한다. 그 정리가 끝난 뒤에만 슬롯 번호를 재사용하므로 같은 프레임에 새 기록을 두 번 진행하지 않는다. 안정된 프레임에서는 정렬하지 않는다.

DeferTransform 판정은 연속된 같은 장식의 상주 필드 쓰기 구간에서 재사용한다. 색·불투명도 등 기존 setter를 거치면 판정 캐시를 무효화한다. 따라서 같은 장식이 비연속으로 등장하거나 중간 setter가 있으면 한 프레임에 여러 번 판정할 수 있다. 이는 인계의 ‘장식당 한 번’을 안전하게 완화한 부분이다. 프레임 전체 캐시는 알파가 바뀐 뒤의 잘못된 판정을 재사용할 수 있어 사용하지 않았다. 실제 판정 횟수는 state의 decisions로 확인한다. SkipForLate의 등록은 기존 HashSet.Add 성공 때만 List에 넣으므로 중복 호출에도 장식은 한 번만 등록된다.

인접한 X·Y는 기존 PairAllowed 조건에서 한 번의 PosFast.Try/setPos로 처리한다. 완료 축은 FEnd, 진행 중 축은 FLast를 넣는다. 단독 위치 축은 기존 OnUpdate/OnComplete setter를 유지한다. 회전은 진행 값과 완료 시 FEnd를 필드에 넣고, 크기는 현재 다른 축을 읽어 자기 축만 계산한다. 이 경우 기존 OnComplete에는 크기 setter가 없다.

Rec는 Proxy.id의 정체성을 유지한다. InList는 ‘recs 또는 상주 슬롯이 소유함’을 뜻하며 상주 중에는 true다. Complete, Kill, KillAll, PlayingTweens에서 슬롯 Pos/FLast/VLast를 동기화한 뒤 기존 로직으로 들어간다. 자연 완료는 값 적용 후 Running=false, Completed 증가, 슬롯 해제와 InList=false 순서다. 재사용은 소유권이 해제된 기록에만 허용한다. 진단용 ProbeRecords·animstat·이동 장식 수집도 상주 기록을 포함한다.

새 맵, SceneReset, Hitch의 공통 SongEnded, UninstallAll/Main.Unload에서 참조를 놓는다. 새 Harmony 패치·정적 이벤트·PlayerLoop 등록은 없다. 개별 admission/계산/setter 예외는 해당 기록을 기존 경로로 복구해 기존 안전 경계로 처리한다. 위치 쌍은 두 축의 진행 상태를 복구한 뒤 각각 기존 Step으로 처리한다.

## 값과 필드 검증

`decobatch verify`는 검증을 토글한다. 반드시 `decobatch state`로 enabled=true, allowed=true, slots>0, checks 증가를 확인해야 한다. Dev에서는 명령이 있어도 제품 루프가 돌지 않으므로 검증 완료로 해석하지 말 것.

1/16 표본에서 먼저 슬롯의 이전 상태를 Rec로 동기화하고, 기존 GroupExpected 순수 계산기로 같은 입력의 기대값을 만든다. 이 계산기는 ApplyE/Step의 연산 모양을 따른다. 별도의 ZeroTween.Eval 호출로 Pos/done/FLast/VLast를 비교하며 장식 setter를 호출하지 않는다. SoA 값은 `start + change * e`, 크기도 기존 ApplyE의 한 식 형태를 유지한다. .NET 8의 계산 결과를 Mono의 증거로 대신하지 않는다.

쓰기 전 계산 비교 뒤, 실제 필드 쓰기 직후 pivotPosVec/rotAngle/scaleVec도 비교한다. 위치 쌍은 최종 setPos가 쓴 X와 Y를 각각 기존 경로의 기대 축 값과 비교한다. 변경하지 않은 크기 축은 VLast의 두 성분 비교에 포함된다. 기대·실제 비트와 키·Ease·성분의 첫 불일치를 기록한다. checks는 계산 비교와 필드 비교를 각각 세므로 표본 기록 수와 같지 않다.

계산 불일치는 쓰기 전에 예외 경계로 보내 이전 진행 상태를 복구하고 기존 Step을 실행한다. 불일치가 한 번이라도 생기면 이후 프레임은 상주 경로를 차단하고 기존 recs로 복원한다. 필드 비교는 쓰기 후 검출이므로 최초 실패 프레임까지 화면 동일성을 보장했다는 뜻은 아니다. 불일치가 있으면 채택하지 말 것.

`decobatch state`는 상주 슬롯·장식 수, 진행 프레임 수, 프레임당 처리 기록, 병합·유지 포함 시간, 판정 횟수, 완료·동기화·검증 횟수와 첫 불일치를 표시한다. fallback 배열은 순서대로 type/mask/alpha/hidden/defer/key/capacity/error이며 Ease 미지원은 0이다. fallback은 판정/시도 횟수이고 고유 기록 수가 아니다. 통계는 해당 실행의 누적값이다.

## 계측과 벤치

`decosamp begin` / `decosamp end`의 전체 루프 시간과 기록 수에 상주 경로를 포함했다. 별도 batch 항목은 병합·슬롯 유지·Compact를 포함한 BatchFrame 전체 시간이며 전체 루프에 이미 들어 있다. 이 항목을 다시 더하면 이중 계산이다. 상주 프레임의 기존 키별 호출 표는 비용 전체가 아니다. 상주 내부 Compact는 batch 항목에 포함되며 바깥 Compact 항목에는 포함되지 않는다.

UpdatePrefix의 t0부터 LastFrameMs/UpdateMs를 확정하는 범위 안에서 BatchFrame이 실행된다. 따라서 기존 fuse의 ‘모드 애니메이션 갱신’에도 포함된다. 정상 처리에서 기록별 Stopwatch는 추가하지 않았다. 상태 시간은 BatchFrame 전체를 한 번 재며, 별도 검증은 명시적으로 켰을 때만 수행한다. 판정·정렬·해제 비용도 실제 비교에서 숨기지 않는다.

`decobatch bench`는 애니메이션이 전부 멈춘 PlayerAuto에서 실행한다. Dev/Prof/그림자 또는 활성 기록이 있으면 거부한다. 더미 장식에 2,150/4,500개 기록을 만들고 기존 StepPair+Step과 실제 BatchStep을 비교해 필드 쓰기를 포함한 us/record를 출력한다. 워밍업 10회, 측정 200회다. setter는 필드만 쓰는 대역이다. 실제 DeferTransform 판정·스케줄 병합·등록/정리·게임 setPos 연쇄·LateUpdate를 제외하므로 FPS 이득으로 해석하지 않는다. 이번 작업에서는 게임 실행 금지로 이 명령을 실행하지 않았다.

등록 때 배열 복사와 장식 사전 접근, 슬롯 변화 때 순서 보존 정리, 새 등록 때 스케줄 정렬 비용이 있다. 실전에서 기록이 자주 교체되면 이 유지 비용이 이득을 상쇄할 수 있다. 약 1.3ms 절약/FPS +13%는 인계 문서의 추정이며 이번 결과가 아니다.

## 오프라인 검증 결과

- Developer / Player / PlayerAuto: 오류 각각 0개. 경고 각각 35/33/35개. 빌드는 Mods 복사 명령이나 설치 훅 없이 `dotnet build`로만 수행했다.
- `py -3 tools/GroupStepCheck/run.py`: 실제 Batch 파일과 추출한 UpdatePrefix를 연결한 .NET 8 스텁 검증 통과.
- 새 비교 900개 시나리오 × 7프레임: 키 순서 6종, Ease 6종, dt 5종, 숨김·알파 변화·Complete·NativeMotion 복귀 포함. 비교 실패 0개.
- 수명 40회: 등록·슬롯 구멍·재사용·기존 경로 복귀·완료 후 슬롯/장식 참조 누수 0개.
- 안정 상태 10,000프레임: 해당 스레드 관리 할당 0바이트.
- 계산 배열 오류 주입: 같은 프레임 기존 값 복구 및 이후 상주 경로 차단 확인.
- 기존 회귀: scalar 3,240개, pair 300개, FastLane 1,440개, Compact 256개, 실제 UpdatePrefix 80개와 setter 콜백·목록 추가·앞선 기록 Complete 검증 통과.

이 검증은 Unity 객체·실제 MoveApply·게임 setter·Mono JIT·실제 화면을 대체하지 않는다. 새 장식 필드 비교가 Unity에서 통과했다는 의미도 아니다. 결과 문서는 apply_patch로 UTF-8 BOM을 포함해 작성하고 엄격 UTF-8 디코딩과 한글 검사를 수행한다.

## Claude가 실행할 순서

1. 별도 허가된 세션에서 PlayerAuto를 설치하고 Main.MeasureBuild=false를 유지한다. NativeMotion·GroupStep·그림자·decoablate·UpdateCostProbe는 끈다. 기존 설정과 설치 파일을 백업한다. decobatch-on의 유무는 새 프로세스마다 적용한다.
2. 먼저 B(켬)에서 `decobatch verify`, `decobatch state`를 사용해 실제 슬롯과 checks 증가를 확인한다. `late-verify`, `tightlate verify`, `animstat`도 사용한다. Arche 240~279초에서 계산/필드 mismatch=0을 확인하고, 완료·Kill(true/false)·재시작·새 맵·알파/숨김/마스크 전환도 확인한다. 검증 계측이 켜진 판은 성능 비교에서 분리한다.
3. 별도의 유휴 상태에서 `decobatch bench`를 실행한다. 위에서 명시한 제외 비용과 함께 결과를 보관한다. 벤치 숫자만으로 채택하지 않는다.
4. A(끔) → B(켬) → B(켬) → A(끔), 매 판 새 프로세스로 같은 해상도·주사율·설정·구간을 돌린다. 같은 DLL을 reload하지 않는다. Arche 240~279초에서 `fuse begin`/`fuse end`와 `decosamp begin`/`decosamp end`로 전체 애니메이션 비용을 비교한다. 검증은 끈다. state의 슬롯·처리량·판정·fallback·동기화도 함께 저장한다.
5. 판단 기준: 양쪽 B에서 전체 ‘모드 애니메이션 갱신’ 시간이 A보다 일관되게 감소하고, 재현 가능한 끊김/최악 프레임이 나빠지지 않아야 한다. decosamp 키별 표가 작아진 것만으로 개선이라고 하지 않는다. 유지·정리·병합·LateUpdate를 포함해 비용이 다른 곳으로 옮겨갔는지 확인한다. 편차보다 작은 차이는 미확정으로 남긴다.
6. HELLO/Arche 전곡을 켬/끔 대조한다. 같은 종료·스냅샷 조건에서 곡 끝 `decosig` 결과의 비트를 비교하고, `late-verify`/`tightlate verify`도 확인한다. 화면·필드·검증 불일치가 하나라도 있으면 식 모양·저장 지점·순서부터 수정한다. 기준이 충족되기 전 기본값을 바꾸지 않는다.

실제 Mono의 mismatch 수, 실제 A/B ms·FPS·끊김 수, 곡 끝 decosig 동일 여부는 Claude의 후속 측정 항목이며 이번 작업에서는 미측정이다.
