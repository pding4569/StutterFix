# Gate 2: 활성 이미지 장식을 구간 메시로 그리기 (정지 화면 픽셀 동일 + 재생 중 이득 측정) — 개발 전용, 기본 꺼짐

게임 실행·설치·커밋·푸시 금지(자동 루프가 빌드·시험·커밋한다). `.codex/config.toml` 수정 금지. 장식·맵 이름은 "Arche" 만 쓴다.
먼저 읽을 것: `docs/ROADMAP-EXTREME-20261021.md`, `docs/optimization-36.md` 끝 절들, `docs/handoff/codex-livedeco-20261021.md`(원래 2단계 설계), `docs/handoff/codex-livedeco-2-*`·`codex-livedeco-3-*` 결과, `docs/handoff/ld-autoloop-ledger.md`, `docs/handoff/codex-freeze-20261021-result.md`(있으면: 변환 쓰기 vs 애니메이션 비용 분해). 게임 코드는 `E:/StutterFixWork/decompiled/Assembly-CSharp/<타입>.cs`(근사; `tools/ilscan` 로 교차확인).

## 게이트 1 결과 (결정됨: 통과로 진행)
Arche 262초/270초: live 이미지 장식 2,128/2,265개, 소유 후보 2,089/2,222개(98.1%), 연속 구간(런) 수 78/71(≤200), 키 분할 64/60, 재질 서명 분할 0, 모호한 재질 동률 0. MaterialPropertyBlock 은 알려진 이름으로는 `_MainTex` 텍스처만 있음(이름 전체 열거는 불가라 완전 증명 못 함 → **미지의 속성이 있으면 아래 픽셀 비교에서 걸린다**). 위 수치는 "아틀라스로 합쳤다고 가정한" 런 수다.

## 배경 (Gemini 검토 + 내 측정에서 나온 가설. 가설은 아래 시험으로 확인)
- Arche 262→276초 프레임 8.0ms. 장식 정지 2.27ms, 장식 0개 1.82ms. 활성 장식 몫 5.7ms = 모드 처리 약 3.2ms(DecoAnim 1.9 + 매니저 LateUpdate 1.16 + 시작 0.13) + 엔진 안 약 2.5ms.
- **가설**: 엔진 안 2.5ms 는 장식마다 `scrDecoration.UpdatePosition` 이 `pivotTrans` 에 localPosition/position/rotation/localScale 을 4~5번 쓰는 Transform 더티·바운드 비용이다(렌더러를 다 꺼도 +6% 뿐이었던 것과 맞음). 그렇다면 **소유 장식의 Transform 쓰기를 없애고 C# 에서 정점을 직접 계산해 구간마다 MeshRenderer 1개로 그리면** 아틀라스 없이도 CPU 이득의 대부분을 얻는다. (freeze 실험 결과가 있으면 그 수치를 우선 믿어라.)
- 정렬 규칙(SortProbe 실측): 같은 (sortingLayer, sortingOrder, 큐, 거리) 안에서는 재질 인스턴스 ID 부호 있는 오름차순이 뒤→앞. `rendererPriority` 무관. 구간(런)마다 MeshRenderer 1개가 **첫 멤버의 sortingLayerID/sortingOrder/z 와 재질 인스턴스(빌린 재질)** 를 쓰면 비소유 렌더러(타일·행성·텍스트·파티클 등)와의 순서가 유지된다. 멤버 SpriteRenderer 는 `forceRenderingOff`(끄기·언로드 때 복원 보장).
- `Graphics.DrawMesh*`/`DrawMeshInstanced` 는 sortingLayer/order 를 못 줘서 쓰지 않는다(씬 MeshRenderer 사용). 렌더 파이프라인은 Built-in(SRP Batcher/BRG 불가): `GraphicsSettings.currentRenderPipeline == null` 을 시작 로그에 확인.

## 구현 요구 (`livedeco on|off|state`, AutoTest/Dev 전용)
1. 정점 직접 계산: 소유 장식마다 월드 정점 4개를 `scrDecoration`/`scrParallax`/`scrDecorationManager` 의 실제 수식(디컴파일 소스 + ilscan 확인; 피벗·childLocalOffset·패럴랙스·회전(카메라 회전 잠금 포함)·스케일(카메라 배율 잠금 포함)·스프라이트 코너/피벗/PPU·flip·tile/RepeatX/Y·sortingOrder/z)로 구한다. **검증 모드**: 켜기 전에 각 소유 장식의 계산 정점을 실제 SpriteRenderer 로 만든 월드 정점/바운드와 비교해 최대 오차를 로그(`[livedeco] vertexcheck max_err=... count=...`)하고, 오차가 크면(>1e-4 월드 단위) 그 장식은 소유에서 제외한다.
2. 소유 장식은 `UpdatePosition`(Transform 쓰기)을 건너뛴다(Harmony; 끌 때 되돌림, Main.Unload 해제 포함). 건너뛴 동안 이 Transform 을 읽는 다른 게임 코드(히트박스·마스크·부모-자식·장식 이동 효과가 읽는 현재값)가 있는지 디컴파일에서 찾아 결과 문서에 적고, 있으면 그 장식은 소유 제외.
3. 색(알파 포함 `Sprites/Tile`·`Sprites/Default` 출력 식, premultiplied 처리)·스프라이트 UV·flip·타일링·블렌드 모드(기본형만 소유; 비기본은 제외)를 정점에 굽는다. 소유 판정은 이미 센서스(`LiveDeco*.cs`)에 있다.
4. 정점 갱신은 매 프레임 구조체 배열 하나에 쓰고 `mesh.SetVertexBufferData(arr, 0, 0, n, 0, MeshUpdateFlags.DontValidateIndices | DontRecalculateBounds | DontNotifyMeshUsers)`. `mesh.bounds` 는 시작 때 큰 고정 값으로 박는다. 정점 레이아웃 인터리브(위치·Color32·uv). 인덱스 순서 = 원래 그리기 순서(구간 안 앞뒤 유지). GC 할당 0 이 되게(로그로 힙 증가 확인).
5. 장식이 live 에서 빠지고 들어올 때(활성 집합 변화)·소유 변경: 구간 재계산은 증분으로, 전환 프레임 비용을 로그. 곡 재시작/맵 다시 열기/죽고 다시 하기에서 오류·누수 없이 정리(`LeakGuard`·`ExitFix` 패턴 참고; Main.Unload 에도).
6. 로그: 구간 수, 소유 수, 프레임당 `[livedeco] cost` (정점 계산 ms, 메시 업로드 ms, 구간 재계산 ms), `vertexcheck`.

## 검증 steps 생성기 `tools/optimization/livedeco2-steps.py --chart <Arche *FULL.adofai> --output x.json` (자동 루프가 이걸로 시험한다)
`tools/optimization/livedeco-steps.py` 와 `framecmp`(`framecmp-steps.py`)·`fuse` 사용법을 참고해 한 게임에서 순서대로:
A. **픽셀**: `seek` 로 262초 근방 → `pause` → `framecmp grab A`(livedeco 끔) → `livedeco on` → `grab B` → `diff A B` → `off` → `grab A2` → `diff A A2`(복원 확인). 262·270·276초 근방 세 시점에서. 중앙 20% 제외 영역 밖 다름 픽셀 수(`ab_out`)와 위치·최대 채널 차이를 `[livedeco2] PIXEL t=... ab_out=... maxdiff=...` 로.
B. **성능**: 262→276초 재생(`pause` 없이) 창을 `fuse begin/end` 로 재되 **끔·켬·켬·끔** (같은 곡 구간 되돌려 `seek`, 안정화 여유) 한 게임 안에서. `[livedeco2] FPS mode=on/off fps=... ms=...`.
C. 마지막에 요약 한 줄: `[livedeco2] GATE2=PASS` 는 **세 시점 모두 ab_out=0**(눈에 안 띄는 ≤1/255 극소수는 별도 `PIXEL_NEAR` 로 보고하고 PASS 로 세지 않는다) **이고** 켬이 끔보다 평균 FPS ≥ +15% 일 때만. 아니면 `GATE2=PIXEL_FAIL`/`GATE2=NO_GAIN`/`GATE2=ERROR` + 이유 코드. **되돌림 후에도 기본 상태(끔)로 끝나야 함.**

## 규칙·종료
- 빌드 3종 오류 0(`-c Release` Dev / `-p:Edition=Player` / `-p:Edition=Player -p:AutoTestBuild=1`), LoadCheck·기존 fixture 통과, 일반 Player 에는 이 코드가 없어야 함.
- 모든 판단은 로그 수치로. 안 되는 이유가 설계 한계면(예: Transform 읽는 코드가 많아 소유 제외 비율이 큼, 픽셀이 구조적으로 다름) 결과 문서에 구체 수치로 적고 멈춰라. 같은 시험을 코드 변경 없이 되풀이 요청하지 마라.
- 결과 `docs/handoff/codex-gate2-<변형>-result.md` + `-o`(10줄).

## 추가 지시 (Gemini 검토 + DecoAnim.cs 확인, 라운드 도중 추가됨)
`DecoAnim.cs` 의 채널 갱신(`Apply`/`OnUpdate`/`OnComplete`, 약 380~430행)은 매 채널마다 `setPosX/Y`(→SetPosition), `setRot`, `setScale`, `setCol/ColorFast.TryColor`, `setOpa/ColorFast.TryOpacity` 를 불러 **Transform·SpriteRenderer.color 에 쓴다**(일부는 `MoveApply.DeferTransform` 으로 나중에 한꺼번에). 즉 `UpdatePosition` 만 건너뛰어서는 Transform/렌더러 쓰기가 남는다. **소유 장식은 이 갱신 경로의 Transform·SpriteRenderer(color 포함) 쓰기를 전부 막고**, 논리 필드(`pivotPosVec`, `rotation`, `scaleVec`, 색·불투명도 등 `scrDecoration` 의 C# 필드: 다음 효과가 "현재값"에서 시작하므로 계속 갱신돼야 함)만 갱신하게 하라. 정점은 이 논리 필드에서 계산한다. `DecoAnim.ProfApply/ProfSet`(속성별 "계산+설정" µs, 로그 줄 870행 부근)로 `설정` 비중을 먼저 확인해 결과 문서에 적어라(Gemini 추정: DecoAnim 1.9ms 의 60% 이상이 설정). 게이트 3 후보(평면 `DecoState[]` + 채널 평가 단일 스레드, 필요시 sfnative 1회 호출)는 gate 2 가 통과한 뒤에 한다 — 지금은 만들지 마라. 워커 스레드 분할은 쓰지 않는다(6코어 HT 없음, 스파이크 위험).

## 성능 지침 (Claude 가 A 안 4회차 증거에서 추가: 소유 1,478개, 구간 1,114개)
`[livedeco] cost` 가 **vertex_ms=2.75 + upload_ms=1.4~2.7 = 약 4.2ms/프레임** 이다. freeze 실험에서 Transform·설정 쓰기를 없애면 3.69ms 가 줄었으므로(끔 8.95→5.25ms), 메시 쪽 비용이 이 정도면 **순이득이 거의 0** 이다. 픽셀 동일성 작업과 별개로 다음 목표를 반드시 달성해라(수치로 보고): **정점 계산 ≤0.5ms, 업로드 ≤0.4ms (소유 약 1,500개 기준)**.
1. 정점 계산 1.9µs/장식은 너무 느리다. 프레임 루프 안에서 Unity 프로퍼티(Transform/Sprite/Renderer 읽기, `sprite.rect/pivot/bounds`, `TransformPoint` 등)를 **부르지 마라** — 맵/소유 시작 때 한 번 구해 둔 불변 데이터(`DecoStatic`: 스프라이트 로컬 코너·UV·피벗·PPU·flip·타일링·정렬 키)를 구조체 배열에 두고, 프레임당에는 `scrDecoration` 의 **논리 C# 필드**(위치/회전/크기/색)와 카메라 값(프레임당 1회 읽기)만 읽는다. 구조체 배열/`unsafe` 포인터/경계 검사 제거, 삼각함수는 각도가 안 바뀌면 캐시.
2. **더티 추적**: 논리 필드가 안 바뀐 장식은 정점을 다시 계산하지 않는다(채널 갱신 경로·`DecoAnim` 설정 함수 대체 지점에서 더티 비트를 세움). 카메라 값이 안 바뀌면(정지·평행 이동만 등) 카메라 영향 항도 건너뛴다. 더티가 없는 구간은 업로드도 건너뛴다.
3. **업로드 호출 수**: A 안은 구간이 1,100개 이상이라 `SetVertexBufferData` 가 프레임당 1,100번이다. 더티 구간만 올리고, 가능하면 구간 여러 개를 한 `Mesh` 로 합치는 방법이 정렬 규칙(같은 키 안 재질 인스턴스 ID 순서)을 깨지 않는 범위에서 있는지 검토해라(깨면 쓰지 마라). B 안은 구간이 ≤80개라 이 문제가 작다.
4. 위 비용 줄이기를 하는 동안 **`GATE2` 시험(픽셀·FPS)이 끝까지 도는 것**이 우선이다. 현재 `autotest_aborted:오류로 그만둠` 이 나는 단계의 원인도 증거 로그에서 찾아 고쳐라. 픽셀: 4회차 262초 `ab_out=674 maxdiff=2`(기준 안정, aa2_out=0)까지 왔다 — 0 으로 만드는 것이 목표지만, 차이가 몇 안 되고 최대 2/255 이면 `PIXEL_NEAR` 로 따로 보고(이 경우 PASS 판정은 사용자가 정한다).

## 긴급 (A 안 5회차 시험 결과, Claude 추가)
첫 재생 FPS 시험이 끝까지 돌았다: **켬 14.7 FPS(67.9ms/프레임), 끔 120.3 FPS(8.31ms)** → gain −87.9%, 판정 `PIXEL_FAIL reason=outside_pixels_changed`. 즉 지금 켜면 프레임이 8배 느려진다. 증거: `docs/handoff/ga-autoloop-run*-evidence.txt` 최신, `cost` 줄에 `scan_ms≈29.7`(프레임당? 전체 장식 스캔으로 보임), `owned=18 runs=16`(재생 중에 소유 수가 1,478→18로 줄었다 — 이유 미확인), `dirty_images=124`, `upload_calls=113`. 해야 할 일, 우선순위순:
1. **프레임당 스캔 제거**: `scan_ms`(약 30ms)가 매 프레임 도는지, 장식 28,828개를 전부 도는지 확인하고, 활성 집합이 바뀔 때(전환)만 증분으로 하게 해라. 프레임당 비용은 정점·업로드 외에 `scan`/`reconcile`/레지스트리 갱신/Harmony 패치 호출 오버헤드까지 합쳐 **켬/끔 프레임 시간 차이를 구간 단위로 쪼개 보고**하라(`Stopwatch` 를 호출이 많은 작은 함수에 감싸 측정 비용이 덮는 일이 없게 — 구간 합과 FPS 시험의 차이로 교차확인).
2. **소유 수 감소 원인**: 시험 도중 `owned` 가 1,478→18 이 된 이유(재생 중 소유 해제 규칙? 전환 때 일괄 제외?)를 로그로 찾아 고쳐라. 소유가 이렇게 줄면 이득은 0 이고 오버헤드만 남는다.
3. 그다음에 `outside_pixels_changed`(켬/끔 픽셀 차이) 를 분석하라.
`GATE2=PASS` 는 아직 멀었다. 한 번에 다 고치려 하지 말고 1→2→3 순서로, 각 단계 수치(켬 FPS)를 증거로 남겨라.
