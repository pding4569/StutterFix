# T1 TileSim 결과 — 2026-10-20

## 상태

작업 폴더 `E:\StutterFixWork\wt-tilesim`, 브랜치 `codex/night-tilesim`. 커밋·게임 실행·설치를 하지 않았다. 새 런타임 기능은 `#if DEV || AUTOTEST`와 프로젝트 Compile 조건으로 제한했다.

FloorAnim 6개 키의 읽기 전용 캡처·SoA 비트 검증·단일/Parallel.For 벤치, 타일 비용 샘플 계측, IL 기반 렌더 구조 분석을 구현했다. **실제 맵 검증과 성능 측정은 미실행**이다. **타일 색 계산의 캡처/비트 재생은 미구현**이다. FloorAnim에 색 Rec가 없고 별도 DOTween/게임 Update 입력이 필요함을 확인했다. 색 경로는 비용 계측과 설계 조사에 포함했다.

## 명령

AutoTest 명령으로 사용한다. 기존 autotest/sfmeasure 전달 경로를 사용하며 새로운 서버나 게임 프로세스를 만들지 않는다.

| 명령 | 조건·출력 |
|---|---|
| `tilesim capture 4` | 곡 재생 중, FloorAnim/이징 사용 가능. 0초 초과~120초. 초기 스냅샷, 생성/Startup/완료/취소, dt, 타일 인덱스%4==0 표본을 메모리 테이프에 저장 |
| `tilesim verify` | 완료된 유효 테이프. 단일/병렬 각각 key별 비교 수·mismatch와 최초 8개 차이의 frame/tile/key/token/진행값/상태/float bit를 출력 |
| `tilesim bench` | 곡 정지 후. JIT/워커 warmup·Reset 제외, 최소 300프레임분 반복. eval/events/starts(부분집합)/total/unaccounted의 mean·p99와 checksum, 병렬 worker 경과합 출력 |
| `tilesim selftest` | 곡·캡처·tilecost 종료 후. 합성 생명주기와 완료 산술 시험 |
| `tilecost begin` | 곡 중 캡처와 분리. 측정용 Harmony 훅을 그때 설치, 16프레임마다 한 프레임 표본화 |
| `tilecost end` | 단계별 QPC ticks·호출 수·ms/선택 프레임·root-ms·unaccounted-ms·원인/폴백 수를 출력하고 자체 훅 해제 |

verify는 **실제 FloorAnim Rec의 애니메이션 축 값·진행 위치·Running/token**만 비교한다. 한 축의 scale 기록에서 다른 축은 후속 기록에 의해 바뀔 수 있으므로 비교 대상에 넣지 않는다. Transform 최종값·렌더 이미지·진짜 DOTween·길이0 즉시 경로·타일 색은 비교 범위 밖이다. 표본 0인 키는 '통과' 근거가 아니며 전체 0 표본은 판정불가로 출력한다.

테이프는 세션 메모리에만 있으며 새 capture와 Unload에서 폐기한다. 512MiB 보수적 예약, 100000타일, 200000슬롯, 12000프레임 상한을 둔다. 식별 실패·슬롯 중복·분할 경로 전환·원본 오류·갱신 중 비정상 재진입 등은 reason-count를 남기고 무효화한다. 제외된 zero/real-overlap/original-effects 수도 끝에 표시한다. 같은 슬롯을 정상적으로 완료하고 다시 시작하는 것은 거부하지 않는다.

## 비용 항목과 해석

| 계측 이름 | 포함 범위 |
|---|---|
| floor-update-other | FloorAnim.UpdatePrefix 중 자식 구간을 뺀 관리·Startup·순회 등 |
| ease-eval | FloorAnim.Apply에서 ZeroTween.Eval 호출 경계 |
| floor-apply/read/write | Apply에서 ease-eval을 뺀 축 산술·Transform 읽기/쓰기·opacity 필드 저장; 순수 native setter 비용과 같지는 않음 |
| floor-start/zero-other | StartPrefix, 즉시 위치/회전/크기 처리 중 자식 시간 제외 |
| mesh-update | FloorMesh.UpdateAllRequired 전체(캐시 조회·새 메시 생성·할당 포함) |
| color/style/opacity | ColorFloor/SetTrackStyle/SetColor/TweenColor/TweenOpacity와 두 구체 FloorRenderer 색 setter; 중첩은 exclusive |
| tilecull | TileCull.Tick/Step 중 자식 제외 |
| recolor-defer | AfterStep/Apply/색·스타일 Prefix 중 자식 제외 |
| game-floor-update/late-other | scrFloor.Update/LateUpdate 중 위 자식 제외. opacity의 실제 Material.SetFloat, glow·icon 갱신 등이 여기 남을 수 있음 |

모든 단계는 실제 경계에서 Stopwatch를 사용한다. 작은 값도 raw ticks/6자리 ms를 출력한다. `root-ms - sum(exclusive)`가 unaccounted-ms이며 **관찰한 호출 루트 내부**의 회계 잔차다. 프레임 전체 CPU/GPU·미계측 게임 함수의 잔차가 아니다. 보고서의 0을 프레임 비용이 모두 설명됐다는 근거로 쓰면 안 된다.

표본 프레임에서는 Rec별 Apply/ease 경계와 Harmony 호출 비용이 발생한다. 고정 1/16 표본은 위상 편향이 있을 수 있으므로 효과 시점을 옮겨 재측정한다. ms/sampled-frame의 분모는 호출이 없던 선택 프레임도 포함한다. QPC 단일 호출 추정치도 표시하지만 자동으로 뺄셈 보정하지 않는다. 이 계측을 켠 상태의 FPS를 A/B 성능 결과로 쓰지 않는다. Harmony 등록 실패/누락 메서드는 원인별 로그와 실패 수로 남는다. 예외를 삼키지 않고 원래 예외를 반환한다.

## 검증

재현 명령:

```powershell
dotnet build -v q --nologo
dotnet build -p:Edition=Player -v q --nologo
dotnet build -p:Edition=Player -p:AutoTestBuild=1 -v q --nologo
dotnet run --project tools/tilesim-fixture/TileSimFixture.csproj
dotnet run --project tools/tilecost-fixture/TileCostFixture.csproj
```

빌드 결과와 최종 검증 수는 아래 최종 확인에 기록한다.

TileSim fixture는 구현 파일을 링크하고, 관찰 대상은 독립적인 Rec 산술로 구한다. 초기의 시작된/미시작 기록, 6개 키, 0/미소/음수 dt, 자연 완료, Kill(true), 같은 슬롯 대체, Rec 재사용, Kill(false), 경로 변경의 무효화·거부를 검사한다. 정상 테이프는 단일·병렬 각각 24표본 mismatch=0이다. 의도적으로 틀린 값 1개는 두 모드 모두 mismatch=1로 검출했다.

TileCost fixture는 실제 계측 콜백을 호출해 중첩 exclusive 시간의 합산, 1/16 표본화, 예외 동일성·회복과 Unpatch 요청을 검사한다. **Harmony 등록은 스텁**이므로 게임 내 패치 성공을 보장하지 않는다. 게임에 포함된 구형 Harmony/MonoMod를 .NET8에서 직접 구동하는 시도는 초기화 호환성 오류로 실패해, 이후 스텁으로 시간 집계만 검사했다. 게임의 Mono 환경 확인은 남아 있다. fixture의 시간값은 게임 성능 근거가 아니다.

## 게임에서 나중에 실행할 순서

이번 작업에서는 아래 순서를 실행하지 않았다. 인계받는 측이 허용된 게임 세션에서 실행한다.

1. Lost Requiem을 열고 재생, `seek 531` 근처에서 `tilesim capture 4`. 캡처 종료 로그를 기다리고 `tilesim verify`. 양 모드 key별 표본과 mismatch를 확인한다.
2. 재생을 `stop`한 뒤 `tilesim bench`. eval뿐 아니라 events+eval total/p99와 checksum을 함께 본다.
3. 별도 재생에서 같은 구간 직전 `tilecost begin`, 구간이 끝나면 `tilecost end`. patch-failures, root/unaccounted, floor-error와 zero/overlap/fallback 수부터 확인한다.
4. HELLO의 125.6초 전후도 같은 순서로 실행한다. 길이0 효과에서는 Rec 표본이 적거나 없을 수 있으므로 excluded-zero-writes와 start/zero 계측을 확인한다. 0 표본을 비트 동일 판정으로 쓰지 않는다.
5. 스타일 변경 구간은 mesh-update와 color/style, opacity 구간은 game-floor-update/late-other도 함께 본다. 필요하면 여러 시작 위상으로 tilecost를 반복한다.
6. 원인 해결 후 계측을 모두 끄고 실제 교체 실험의 FPS/프레임 비용을 별도로 측정한다. 현재 기능은 교체가 아니므로 FPS 개선을 기대하는 A/B 대상이 아니다.

`open`에는 기존 자동 시험 규칙에 맞는 실제 맵 경로를 넣는다. `capture`는 즉시 반환하므로 자동 스크립트에서는 최소 4초 이상의 대기와 완료 로그 확인을 둔다. Unload는 캡처를 중단·폐기하고 TileCost 소유 Harmony ID를 해제한다.

## 설계 결론

[설계 문서](codex-tilesim-20261020-design.md)에 렌더러·재질·스타일·정렬·TileCull/RecolorDefer/MeshWarm 관계와 증거를 정리했다. 표 평가만 대체하면 Transform·재질 반영·스타일/메시 갱신은 줄지 않는다. FloorRenderer.Awake의 renderer.material 경로 때문에 타일이 공유 재질이라는 가정도 성립하지 않는다. 그룹 메시의 가능성은 있으나 실제 재질 ID/정렬 구간·추가 렌더러·Collider/Transform 읽기·동적 스타일 적격 판정과 픽셀 검증이 먼저 필요하다. 게임 측정 수치 없이 예상 절감 ms를 만들지 않았다.

## 최종 확인

- DEV 빌드: 오류 0, 경고 38. Player 빌드(증분): 오류 0, 경고 4. Player+AUTOTEST 빌드: 오류 0, 경고 38. 기존 참조 버전 충돌·미사용/구형 API 경고가 남아 있다.
- `tilesim selftest`: 합성 단일/병렬 검사 합계 12개 통과. 완료 시 End로 스냅하면 틀리는 큰 float 상쇄 사례도 포함했다.
- TileSim 외부 fixture: 12개 상위 검사 통과. 정상 테이프 단일/병렬 각각 24비교, 불일치 0. 오류 주입은 각각 1비교/1불일치. 정지 후 bench 양 모드의 checksum 일치, 재생 중 bench 거부도 확인했다.
- TileCost 회계 fixture: 중첩·표본화·예외 동일성/회복·해제 요청 통과. 스텁 등록 22개, 관찰 루트 회계 `unaccounted-ms=0.000000`. 실제 Harmony/Mono 적용은 미검증이다.
- ILScan 형식 확인: DEV/AUTOTEST에 TileSim·TileCost 각각 존재, Player에는 각각 0개. 첫 확인 스크립트는 ILScan의 `(0개 형식)` 요약행을 형식으로 잘못 세었고, `== ` 형식 헤더로 판정하도록 고쳐 제외를 확인했다.
- `git diff --check` 통과. 커밋·게임 실행·설치 없음.
- 로컬 빌드·fixture 로그: `tools/optimization/out/tilesim-20261020/`에 보관한다(기존 gitignore 적용). 실패한 구형 Harmony 직접 구동 로그도 함께 보존한다.

다음 판정에 필요한 것은 실제 Mono의 `tilesim verify` 결과, 같은 구간의 tilecost 단계값, 그리고 색을 포함하려면 별도 색 소유권·시간 입력 캡처 명세다. 이번 결과만으로 FPS 개선이나 전체 타일 파이프라인 완료를 주장하지 않는다.
