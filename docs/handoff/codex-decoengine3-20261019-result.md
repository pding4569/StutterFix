# DecoEngine 3 결과 — 2026-10-19

장식당 갱신 비용을 줄이는 후보를 구현했다. 게임 실행·Mods 설치·커밋은 하지 않았다. 엔진 기본 끔(`decoengine-on` 파일로만 켬)을 유지했다. 실제 작업 시작 HEAD는 `5f428eb`이며, 인계 문서의 성능 기준 `fb4fe3f` 수치는 그대로 참고했다. 기존 커밋이나 사용자의 설정 변경은 되돌리지 않았다.

실게임 성능은 미측정이다. 목표인 Update+Late 합 ≤1.8ms 달성을 주장하지 않는다. 이번 변경의 추정 절감은 약 0.20~0.31ms/프레임으로, 기준 3.44ms에서 약 3.13~3.24ms 수준이다. 아래 수치는 모두 추정이며 Claude의 새 프로세스 ABBA가 채택 기준이다.

## 구현한 변경

### 1. Stable의 네이티브 읽기 분산

- 입장 때 기존 `StableChild`와 위치 스냅샷 검사를 유지한다.
- 자식 참조, `pivotOffsetVec`, `croppedPivotOffset`, `trueScaleMultiplier`, `placementType`는 계속 매 프레임 비교한다. 달라지면 즉시 기존 경로로 복귀한다. 새 값을 임의로 받아들이지 않는다.
- 관리 값이 같으면 `childTransform.localPosition`은 16프레임마다 읽는다. 최초 검사는 입장 순서로 1~16프레임 뒤에 배분한다. 검사 시점에도 값이 다르거나 읽기 예외가 나면 해당 장식만 복귀한다.
- `decoengine verify`에서는 모든 소유 장식을 매 프레임 읽는다. 기존 setter 입력 대조의 1/16 장식 표본과 별개다.
- `child-reads`는 Stable에서 수행한 읽기 횟수다. 입장 검사 읽기는 포함하지 않는다.

**정확성 한계:** 임의의 다른 코드가 관리 필드를 바꾸지 않고 자식 Transform만 쓰면 최대 15프레임 동안 놓칠 수 있다. 120fps에서는 약 125ms, 낮은 FPS에서는 더 길다. 이 동안 기존 setter가 복구했을 자식 위치가 남을 수 있다. 자식이 파괴돼도 다음 검사까지 발견이 늦을 수 있다. 게임·다른 모드 전체에 대한 관리 필드 불변 증명은 성립하지 않는다. 인계 문서가 허용한 주기 검사 후보로만 구현했으며, 검증 모드 통과가 이 지연 위험까지 없애 주지는 않는다.

### 2. Advance의 무조건 슬롯 정리 제거, 기존 Eval 공유 계측

- 완료/중단 슬롯이 없는 프레임에는 슬롯 복사, `Array.Clear`, Mask 재계산을 생략한다. 기존에는 살아 있는 Rec 참조를 매번 같은 배열에 다시 썼다.
- 슬롯이 끝난 프레임에만 한 번 압축하며 같은 순회에서 Mask도 만든다. 순서, 시계 덧셈·종료 clamp, float 식, FEnd 처리, VLast 처리와 Publish는 유지했다.
- `(Pos, Dur, Ease, Over, Period)`가 비트 동일한 **인접** 슬롯의 Eval 공유는 이미 있었다. 이를 유지했다. 비인접 슬롯까지 검색하는 캐시는 추가하지 않았다. 최대 5슬롯에서 추가 비교 비용의 이득을 아직 입증하지 못했다.
- `decoengine stats on/off`를 추가했다. 기본 off이며 켜면 `ease-hit/miss`가 공유 적중/실제 Eval 횟수를 센다. 적중률은 구간 전후 차이의 hit/(hit+miss)로 계산한다. 정확성 oracle과 AlphaRecordSafe의 Eval은 분모에 포함하지 않는다.
- `compact`는 슬롯 압축을 실행한 횟수다. 계측용 장식별 Stopwatch, Harmony 패치는 추가하지 않았다. stats off에도 슬롯당 조건 분기는 남으므로 실게임에서 순이득 확인이 필요하다.

### 5. 숨겨진 장식의 후보 생성 생략과 실패 사유 세분화

- 비소유 Rec는 후보 Dictionary/State 구성 전에 관리 필드 `rendererEnabled && rendererColor.a != 0`을 검사한다. 숨겨졌으면 원래 recs에 그대로 둔다. 알파가 양수가 될 때 기존 `AlphaWillSet`의 Dirty 경로가 재입장을 요청한다.
- 이 검사는 전체 eligibility 통과를 뜻하지 않는다. 보이는 장식은 기존 `EngineEntry`와 자식 위치 검사를 모두 통과해야 입장한다.
- 기존 `missing[4]`는 Eligible의 reason 3, 즉 렌더러 비활성 또는 최종 렌더러 알파 0이었다. 다른 원인과 혼합된 항목이 아니었다. 이제 이 경우의 상당수는 `hidden-admission-records`로 옮겨진다. **이 카운터는 장식 수가 아닌 후보 스캔에서 건너뛴 Rec 수**다. 따라서 기존 missing[4]와 수치를 직접 비교하면 안 된다.
- `missing-eligible=0/...`를 추가했다. EngineEntry의 실제 Eligible 실패를 reason 인덱스별로 센다: 1 타입/없음, 2 hitbox·mask·mesh, 3 숨김, 4 follow/stick/lock, 5 parallax 상태·참조, 6 부모, 7 Rejected. 기존 missing[1..7] 분류도 유지한다.
- 사전 조건 실패(missing 1), 엔트리 미생성(2), EngineRejected(3), stale(6), 같은 프레임(7)은 기존 missing 카운터를 본다. `missing-eligible`는 이 항목들을 중복 집계하지 않는다. 새 실게임 원인별 수치는 아직 없다.

## IL 조사와 유지한 경로

`tools/ilscan`에 `DECO_GUARDS=1` 모드를 추가했다. 전체 메서드의 관련 `stfld`/`ldflda`와 localPosition 쓰기 호출을 기록한다. 네이티브 PE는 명시적으로 건너뛴다. 다음 파일에 이번 디스크 감사 결과를 남겼다.

- `docs/handoff/decoengine-evidence/guards-game-20261019.txt`: 현재 Assembly-CSharp 전체 메서드.
- `docs/handoff/decoengine-evidence/guards-mods-20261019.txt`: Mods 하위 DLL 30개 경로와 SHA256. 관리 DLL 26개를 읽었고 네이티브 3개, macOS 리소스 파일 1개를 제외했다. 설치된 StutterFix는 이번 빌드 이전 DLL이다.
- 기존 `scrDecoration-il.txt`, `scrVisualDecoration-il.txt`의 메서드별 필드·호출 목록도 함께 확인했다.

| 경로 | IL에서 확인한 사실 | 판단 |
|---|---|---|
| SetPosition, SetPositionX/Y, SetPivotX/Y | SetPosition이 childTransform.localPosition을 쓰고 pivotOffsetVec도 저장한다. X/Y·pivot 변경은 SetPosition으로 연결된다. | 관리 offset이 달라지면 즉시 복귀. 같은 값을 저장하는 호출은 값 비교만으로 감지할 수 없다. |
| SetScale | scaleVec·pivotTrans.localScale을 쓰며 자식 localPosition 쓰기는 없다. | scaleVec는 매 Advance에서 읽는 기존 동작 유지. |
| SetSprite 두 오버로드 | sprite 갱신과 ResizeCollider 호출. 메서드 목록에 자식 localPosition 쓰기가 없다. | 이것만으로 모든 호출자·다른 모드까지 안전하다고 확대하지 않는다. |
| SetTextureScaleMultiplier | textureScaleMultiplier 저장. | trueScaleMultiplier 관리 비교 유지. |
| MoveDecorationsPlus.StartEffect | croppedPivotOffset 저장. | 매 프레임 관리 비교로 복귀. |
| CreateDecoration / ObjectDecoration.SetType | childTransform 참조 저장. | 참조가 달라지면 즉시 복귀. ObjectDecoration은 원래 타입 제외 대상. |
| Setup / SetPlacementType / SetParallax | hitbox, stickToFloor 주소, lockRotation/Scale, scaleMultiplier, croppedPivotOffset, placementType, followPlanet 등 변경 경로가 있다. | 입장 당시 값을 영구 신뢰할 수 없다. |
| SetMaskingType / EnableMeshRenderer | maskingType / meshRendererEnabled 저장. | 마스크·메시 검사 유지. |
| scrParallax.Init / UpdateScreenClamp | cachedTransform, cameraTransform, decoration, inited, isDecoration / clampToScreen 변경. | 참조와 상태 검사 유지. |
| 에디터 | 기존 StableChild/Update에서 편집기 배제. | 에디터에서 엔진 진행하지 않음. |
| 다른 모드 / 공용 Transform 함수 | 이번 Mods 관리 DLL 감사에서는 벤치 등 StutterFix 코드 외에 관련 장식 필드 저장이 나오지 않았다. 게임에는 RDExtensions.LocalMove*/Translate* 등 임의 Transform 쓰기가 있다. | 정적 직접 참조 감사일 뿐 alias, 리플렉션, DOTween의 내부 쓰기, 외부 캐시·동적 어셈블리는 증명하지 못함. |

스캐너의 `[child-ref + local-write]` 표시는 같은 메서드에 두 참조가 있다는 뜻이다. 실제 객체 별칭이나 분기 관계를 증명하는 표시는 아니다. `ldflda`도 쓰기 가능성을 보수적으로 표시하며 실제 쓰기를 뜻한다고 단정하지 않는다.

### 3. Late eligibility 축소는 보류

`EngineReason`과 Late의 `Eligible` 재검사를 유지했다. 타입 자체는 변하지 않지만 hitbox·mask·mesh·follow/stick/lock·parallax는 위와 같이 관리 상태 변경 경로가 존재한다. Setup의 epoch 무효화만으로 그 밖의 SetMaskingType/EnableMeshRenderer/Init 등을 모두 포괄한다고 증명할 수 없다. Update 이후 Late 전의 변경도 확인해야 한다. 보임 판정·부모 정체만 남기는 구현에는 새 setter 무효화 또는 해당 필드 스냅샷 감시가 추가로 필요하다. 이번에는 검사 삭제로 정확성을 약화하지 않았다. 이 항목의 예상 절감은 0이다.

### 4. 알파 전환의 retained active 플래그 방식은 미구현

단순히 active에 남기고 한 프레임 플래그만 세우는 방식은 현재 코드에서 성립하지 않는다.

1. 소유 transform Rec는 recs에서 빠져 있고 색·불투명도 Rec는 원래 recs 순서로 돈다. active 루프에서 transform setter를 호출하면 알파 OnUpdate/OnComplete와의 EngineOrder 순서가 달라진다. 중간 알파 0은 최종 알파가 양수여도 중요하다.
2. recs에 임시로 노출해도 `CompactRecords`는 `Engine != null` Rec를 제거한다. 반대로 Engine을 null로만 바꾸면 Kill/Complete의 `DetachRecord`, 재사용 시 `InList`, 새 Rec의 PrepareNew/Admit가 남겨진 State를 일관되게 관리하지 못한다.
3. 숨김 상태는 여러 프레임 지속할 수 있고 Late의 reason 3도 소유권을 돌려준다. 한 프레임 표지만으로 해결되지 않는다.

가능한 후속 설계는 State에 legacy 체류 상태를 두고, 원래 recs에 EngineOrder대로 Rec를 노출하며, Compact·Kill·재사용·완료·재개 시 이중 진행 방지를 함께 수정하는 것이다. 임시 목록 노출에는 여전히 삽입 비용이 든다. 현재 오프라인 검사는 실제 게임 setter/알파 중간 상태의 호출 순서까지 실행하지 않으므로 이 구조를 안전하게 검증할 기반도 부족하다. 따라서 alpha-fallback의 기존 복귀를 유지했다. 이 항목의 예상 절감은 0이며, 16k 복귀가 사라졌다고 주장하지 않는다.

### 6. 색 직접 처리 미구현

원래 Step→OnUpdate→SetColor/SetOpacity 경로와 알파 중간/완료 값 저장 순서를 유지했다. 직접 호출 최적화의 실게임 비트 동일성은 이번에 검증하지 않았다. 예상 절감 0.

## 예상 이득 — 실측 아님

인계 문서의 프레임 7ms, 소유 장식 약 1,050개/프레임을 기준으로 환산했다. 포함 시간끼리는 중첩될 수 있어 더하지 않는다.

| 항목 | 관련 프로파일과 계산 | 추정 절감 µs/장식·프레임 | 추정 절감 ms/프레임 |
|---|---|---:|---:|
| Stable 주기 읽기 | localPosition 2.8%×7ms×15/16 = 0.184ms. 여기에 스케줄·계수 비용 고려. verify off 기준, 입장 읽기 제외. | 0.15~0.18 | 0.16~0.19 |
| 완료 때만 슬롯 정리 | Advance 리프 6.1%×7=0.427ms 및 쓰기 장벽 표본 일부 감소 예상. 그중 8~21%가 해당 루프였다고 가정한 추정. | 0.03~0.09 | 0.03~0.09 |
| 숨김 후보 미생성 | Admit 1.9%×7=0.133ms 중 일부. 전체 hash 표본을 모두 절감하는 변경은 아님. | 0.01~0.03 | 0.01~0.03 |
| 기존 Eval 공유 | 알고리즘 유지. stats on의 추가 비용은 미측정. | 0 | 0 |
| eligibility·알파 체류·색 직접 처리 | 기존 경로 유지. | 0 | 0 |

합계 약 0.20~0.31ms는 거친 추정이다. 현 목표와 차이가 크므로 이 결과만으로 엔진을 기본 켬으로 바꾸면 안 된다. `Stable` 전체 4.9%나 `Advance` 전체 8.8%가 통째로 없어지는 변경도 아니다.

## 검증 결과

- `dotnet build StutterFix.csproj -v q --nologo`: Dev 오류 0, 경고 35.
- `dotnet build StutterFix.csproj -p:Edition=Player -v q --nologo`: Player 오류 0, 경고 33.
- `dotnet build StutterFix.csproj -p:Edition=Player -p:AutoTestBuild=1 -v q --nologo`: PlayerAuto 오류 0, 경고 35.
- `dotnet build tools/ilscan -c Release -v q --nologo`: 오류 0, 경고 0.
- `py -3 tools/DecoEngineCheck/run.py`: **PASS assertions=358869**, `checks=7482 mismatch=0 first=none`. 검사 하네스의 미사용 필드 경고 1개가 있었다.
- 기존 무작위 128회×400프레임 시계·스칼라·역순 scale·완료 대조와 예외/재사용/수명 검사를 유지했다. 추가 검사는 native-only 변경의 지연·기한 내 복귀, 관리 필드 즉시 복귀, 16프레임 주기, 샘플 읽기 예외, 공유 Eval 5채널 1회, signed-zero 파라미터의 공유 금지, 중단 슬롯 Mask 갱신, 숨김/재입장을 확인했다.
- 이 검사는 production DecoEngine.cs와 TightLate의 deferred-Late 메서드를 연결한 net8 대리 환경이다. 실제 Unity 네이티브 동작·Mono float 반올림·Harmony 순서·실제 TightLate eligibility 전체를 증명하지 않는다. 검증 중 출력되는 InvalidOperationException fallback 한 줄은 의도적 예외 주입이다.
- `git diff --check` 통과. 게임 실행·Mods 설치·커밋·기본 켬 파일 생성은 수행하지 않았다.

## Claude가 이어서 돌릴 순서

1. 별도 새 프로세스 정확성 판: 엔진 켬, `decoengine verify`, Arche와 HELLO 전곡. mismatch=0, 완료/중단/알파 전환·숨김 복귀·장식 상태를 확인하고 곡 끝 decosig도 대조한다. 이 판은 성능 판에서 제외한다. verify는 자식 guard가 매 프레임 실행되므로 정상 성능과 다르다.
2. 별도 검증 끔 판에서도 자식 native-only 변경·파괴·숨김 전환을 확인한다. 주기 검사 위험의 수용 여부는 Claude가 판단한다. 필요하면 매 프레임 검사로 되돌린다.
3. 깨끗한 성능 판은 **새 프로세스 A→B→B→A**로 실행한다. A는 decoengine-on 없음, B는 있음. 같은 DLL·맵·타일 시작 경로·2560x1440 120Hz·다른 모드·출력 조건을 유지한다. 모두 `decoengine verify off`, `decoengine stats off`로 하고 profsamp는 끈다. 재로드 A/B는 사용하지 않는다.
4. 각 판에서 `waitsongpos 252` → `decoengine state` → `fuse begin` → `waitsongpos 273` → `fuse end` → `decoengine state`. 호출 지연을 포함한 실제 창 시작·끝 곡 시각도 기록한다. 긴 대기는 자동 시험의 명령별 제한 안으로 나눈다.
5. Update+Late 합, 창 프레임 수, 최악 프레임·low, 소유율과 admissions/restores/alpha-fallback, hidden-admission-records, missing/missing-eligible, child-reads, compact의 구간 차이를 비교한다. 카운터는 누적이며 자동 초기화되지 않는다. 기존 B2와의 변경 효과 비교는 가능하면 fb4fe3f DLL과 이번 DLL을 각각 새 프로세스로 추가 비교한다.
6. 별도 진단 판에서 `decoengine stats on` → `waitsongpos 252` → `decoengine state` → `fuse begin` → `profsamp begin 1000` → `waitsongpos 273` → `profsamp end` → `fuse end` → `decoengine state` → `decoengine stats off`. Eval 적중률은 ease-hit/miss 구간 차이로 계산한다. Stable/localPosition, Advance, Admit 및 eligibility의 실제 표본 비중을 확인한다. 표본 수집과 stats의 비용이 섞이므로 이 판은 깨끗한 ABBA 수치와 분리한다.
