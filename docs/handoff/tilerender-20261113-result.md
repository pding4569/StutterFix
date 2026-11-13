# 타일 렌더 조사 도구와 정지 화면 시제품 결과 (2026-11-13)

## 판정

**전체 요청 완료가 아니다.** `tilerender census`, 조건부 정지 화면 직접 그리기, 네 맵의 검증 계획과 오프라인 검사를 구현했다. 요청한 **실시간 인스턴싱·쓰기 지점 기반 dirty 갱신·실시간 무할당·게임 픽셀 및 FPS 검증은 미완료/미확인**이다. 현재 구현을 그 경로의 완성품으로 사용하면 안 된다.

이번 지시에서 게임 실행·설치 DLL 교체·커밋·푸시는 금지다. 이를 수행하지 않았다. 실제 맵의 census 결과가 없으므로 어느 맵의 정렬 구간이 안전한지도 판정하지 않았다. Unity Editor도 실행하지 않았다. 설치 상태/해상도를 바꾸는 명령은 실행하지 않았다.

작업 위치: `E:\StutterFixWork\wt-v14prod`, 현재 기준 HEAD `ac27d02`, 브랜치 `claude/adofai-renderer`. 기존 `out/`와 인계 원문 등 미추적 자료는 유지했다.

## 구현 내용

### 1. `tilerender census`

`TileRender.cs`를 DEV/AUTOTEST에만 포함했다. 명령은 `AutoTest`의 bool Step 프로토콜을 따라 30프레임 표본이 끝날 때까지 다음 단계로 넘어가지 않는다. 첫 스냅샷은 프레임 끝에서 수집한다.

- 소유 판별은 이름 추측이 아닌 `scrFloor.floorRenderer.renderer`다. 기존 `TileCull.Build`에서도 같은 wrapper를 사용한다. MeshRenderer와 SpriteRenderer를 모두 조사하며, glow/행성/장식은 독립적인 비타일 렌더러다.
- 전체 소유 수, enabled/active/main-camera layer mask 통과 수, frustum AABB 통과 수, material slot 수, 서로 다른 sharedMesh 수와 상위 10개, 셰이더/정렬 큐/키워드 분포를 로그로 남긴다.
- 서로 다른 재질 instance 수 및 내용이 같은 재질 묶음 수를 따로 센다. 셰이더 ID·정렬 큐·정렬된 키워드·pass 활성화·GI/instancing 상태·선언된 텍스처/scale/offset/색/벡터/숫자 속성을 canonical 문자열로 비교한다. FNV64는 표시용이며 묶음 판정은 문자열 전체로 하므로 해시 충돌로 합치지 않는다. 상세 재질 로그는 최대 30그룹, 전체 그룹 수에는 제한이 없다.
- 정렬 키는 기존 `GpuDecoScene.SortKey`를 재사용한다. 타일 sorting layer value/order 범위, 모델상의 연속 구간 수, 타일 사이 비타일 slot/renderer 수, 동점 bucket 수, SortingGroup 존재를 기록한다. 끼어드는 renderer 예시는 최대 20개다.
- 재생 중 연속 30프레임에 대해 원소별 행렬 bit 변화와 재질/선언된 MPB 속성 변화의 타일 수 평균을 낸다. renderer 및 slot MPB를 모두 본다. 소유 집합·활성 집합·컨트롤러가 달라지거나 pause/프레임 단절이 있으면 평균을 유효하게 내지 않고 오류로 끝낸다.
- 이미 정지했거나 곡 재생 중이 아니면 정적 스냅샷만 내고 변화량은 `UNMEASURED`로 명시한다. 0으로 꾸미지 않는다.

한계: frustum AABB 통과는 엔진의 occlusion/LOD 이후 정확한 main-camera visible 수가 아니다. 정렬 결과는 CPU 모델이며 native/CommandBuffer draw 전체를 복원하지 않는다. MPB의 비선언 uniform·배열·버퍼 등은 전부 열거할 수 없어 변화량의 범위 밖이다. census는 진단용 할당과 전체 열거를 포함하므로 FPS 측정과 분리한다.

### 2. `tilerender on|off|state|assertactive`

현재 backend는 **`frozen-original-draw`**다. 원본 Mesh·Matrix·Material·shader pass를 그대로 CommandBuffer.DrawMesh에 넣고 해당 renderer만 `forceRenderingOff=true`로 바꾼다. 원본 draw 수는 그대로이므로 **renderer 관리 비용을 제거하는 상한 후보**이지 draw-call 감소나 인스턴싱 결과가 아니다.

허용 조건은 매우 좁다.

- `FrameCompare.Paused` 상태여야 한다. 재생 중 `on`은 오류다.
- Built-in Forward/default camera sorting이며 camera occlusion culling이 꺼져 있어야 한다. 도구가 이를 임의로 끄지는 않는다.
- 타일 전체가 투명 draw 목록의 **맨 앞 연속 구간**이어야 한다. 중간 구간은 `BeforeForwardAlpha`로 재현할 수 없어 거부한다. 타일 관련 동일 layer/order/queue/distance bucket도 거부한다.
- SortingGroup이 있는 가시 장면, 타일 LODGroup, SpriteRenderer 타일, MPB, 여러 material/submesh, shadow caster, `Sprites/Tile` 이외 또는 여러 pass의 재질은 거부한다.
- 타일 layer를 소비하는 다른 활성 카메라, 기존 `BeforeForwardAlpha` CommandBuffer가 있으면 거부한다.

`off`는 저장한 forceRenderingOff 값을 되돌리고 camera callback/CommandBuffer를 해제한다. resume, 모드 끄기/unload, 장면 reset, 자동 시험 종료/실패에도 정리한다. 카메라/기존 가시 소스의 행렬·정렬·메시 등이 달라지면 후보를 해제한다. 이 검사는 정지 스냅샷 검사용이며 dirty 기반 실시간 구현이 아니다. 새로 생성되는 renderer와 모든 외부 native draw까지 감시한다는 보장은 없다.

실제 on 성공 및 픽셀 일치는 미검증이다. 보수적인 조건 때문에 네 맵 모두 거부될 수 있다. **거부는 성능/그림 실패도 성공도 아니며 실험 불성립이다.** `on`이 성공했어도 새 셰이더나 실시간 경로 승인이 아니다.

### 3. 검증 준비

외부 파일:

- `E:\StutterFixWork\tilerender_run.py`: 기본은 JSON 계획 작성뿐이다. `--run`을 준 경우에만 기존 `run_locked.py`를 호출한다. 이번에는 호출하지 않았다. 기존 파일 덮어쓰기를 거부한다.
- `E:\StutterFixWork\bench_v14.py`: `CFG2["trOn"]`을 `rlNR`의 독립 복사본으로 추가했다. 설정별 `CFG_MID`를 seek/waitsongpos 뒤, FPS 측정 전에 적용한다. 환경 변수 BENCH_MID와 같은 중간 명령 위치를 쓰되 trOn만의 명령이 baseline에 새지 않는다. **`TR_LIVE_READY=False`일 때 trOn 요청은 runner 실행 전에 거부한다.** 이 플래그만 바꿔서는 실시간 구현이 생기지 않는다.

계획은 HELLO 50초, Exit 88초, Windflower 260초, 7777 120초다. 공통 순서는 다음과 같다.

1. `CFG2["rlNR"]`로 맵 열기/seek/waitsongpos.
2. 재생 중 `tilerender census`가 30프레임 끝날 때까지 기다림.
3. pause 후 필터 유지/마스크 없음으로 a·a2 캡처, `assertzero a a2`.
4. on → state → wait 1 → assertactive → b 캡처 → assertactive.
5. `framecmp diff a b 0 png` 및 `framecmp assertpixels a b`.
6. off → restored 캡처 → `framecmp assertzero a restored png` → resume/quit.

`FrameCompare`에 `assertpixels A B`를 추가했다. 후보 비교는 forceRenderingOff가 의도적으로 바뀌므로 픽셀만 0인지 검사한다. 기존 `assertzero`는 픽셀과 상태 동일성 검사를 그대로 유지하며 noise/복구 대조에 사용한다. 각 캡처 내부의 안정성 검사도 유지한다. 비교 앞뒤 assertactive는 후보가 자동 해제돼 원본끼리 비교한 결과를 성공으로 오인하는 일을 막는다.

준비된 JSON은 `out/tilerender-plans/{hl,sh,wf,s7}.json`. 이 파일들과 외부 Python 수정은 자동으로 커밋되지 않으므로 Claude가 보존/커밋 범위를 별도로 확인해야 한다.

## 오프라인 검증

| 검사 | 결과 | 근거 |
|---|---|---|
| DEV Release build | 오류 0, 경고 84 | `out/tilerender-dev-build.txt` |
| Player Release build | 오류 0, 경고 63 | `out/tilerender-player-build.txt` |
| Player AUTOTEST Release build | 오류 0, 경고 57 | `out/tilerender-auto-build.txt` |
| 네 맵 계획·시간·설정 복사·명령 순서·활성 검사·픽셀/복구 구분·중간 명령 위치·실시간 거부·Python 구문 | PASS | `out/tilerender-plan-check.txt` |
| `git diff --check` | 오류 없음 | LF→CRLF 안내만 있음 |
| 게임 census/원본↔후보 픽셀/복구/실제 GC 할당/FPS ABBA | 미실행 | 이번 작업의 금지 범위 |

재현 명령(게임을 실행하지 않음):

```powershell
dotnet build StutterFix.csproj -c Release
dotnet build StutterFix.csproj -c Release -p:Edition=Player
dotnet build StutterFix.csproj -c Release -p:Edition=Player -p:AutoTestBuild=1
py -3.12 tools/analysis/check-tilerender-plan.py
```

## Claude의 다음 작업

1. 먼저 네 맵 census 로그를 확보한다. 계획의 on이 거부되면 앞선 census 로그까지 조사 자료로 쓰되 그 실행을 픽셀 성공으로 집계하지 않는다. 그림 비교가 성립하려면 `on backend=...`, 두 assertactive, noise/후보/복구 검사 모두 확인해야 한다.
2. 실제 순서가 투명 prefix가 아니라면 **현재 snapshot backend의 조건을 느슨하게 풀지 않는다.** 연속 구간 사이의 정확한 엔진 삽입 지점이나 정렬을 보존하는 별도 설계가 먼저다. 카메라 이벤트 하나가 임의의 sortingOrder 사이를 나누지는 못한다.
3. 현재 자료 `out/original-tile-shader/state.json` 및 `program-*.txt`에는 원본 Sprites/Tile 상태와 D3D 명령이 있다. vertex 행렬/색/RepeatX·RepeatY, premultiplied fragment, texel 중심 sampling variant가 있으므로 단순한 sprite shader로 대체하지 않는다. **인스턴싱 shader 사본/번들 및 원본 대조 fixture는 아직 작성하지 않았다.** 이 경로를 진행할 경우 기존 번들 방식과 실제 픽셀 대조가 필요하다.
4. 실시간 구현은 FloorAnim뿐 아니라 native FloorMesh 갱신, 색/style/opacity, 부모 transform, renderer 활성 상태/카메라/정렬/다른 모드 쓰기 등에도 무효화 경계를 마련해야 한다. 현재는 FloorAnim 및 색 쓰기 지점을 변경하지 않았다. 30프레임 평균 변화가 적다는 사실만으로 이후 변화가 없다고 가정하지 않는다.
5. 실시간 동작·무할당·새 대상/재질/메시·정렬 변화 시 복귀 검사를 구현/검증한 뒤에만 `TR_LIVE_READY`를 올리고 `rlNR,trOn,trOn,rlNR` ABBA를 진행한다. 현재 플래그는 의도적으로 닫혀 있다. 아직 성능 향상 수치는 없다.

전체 요청의 남은 실시간 구현을 이번 코드가 완성했다고 인계하면 안 된다. 현재 산출물은 그 판단에 필요한 census와 좁은 정지 진단/검증 도구다.
