# LiveDeco 결과 — 2026-10-21

**게이트 1 센서스 구현·실행 JSON·오프라인 검증을 완료했다. 실제 게이트 1은 미측정이며 여기서 중단한다. 게이트 2는 구현하지 않았다.** 게임 실행·제어·설치·커밋은 하지 않았다. 기본 꺼짐이며 일반 Player에서 제외한다. PlayerAuto에는 Claude의 `--probe` 시험용 센서스만 들어간다.

## 읽은 근거와 중단 이유

작업 지시, `docs/optimization-36.md` 끝 절들, matshare/gpusplit-3/framecmp 1~4 결과를 읽었다. `docs/ROADMAP-EXTREME-20261021.md`는 이 worktree에 없어서 `E:\StutterFixWork\StutterFix\docs\ROADMAP-EXTREME-20261021.md`를 읽기 전용으로 확인했다. 시작 시 worktree는 깨끗했다.

게임을 실행할 수 없으므로 Arche 262·270초의 소유 후보 비율과 런 수를 확보할 수 없다. 기존 matshare의 이미지끼리만 센 Run이나 과거 decorender/decopipe 결과로 이번 게이트 통과를 대체하지 않았다. **미측정은 FAIL 실측도 PASS 실측도 아니다.** 두 시점의 새 로그가 다음 필수 관측이다.

## 센서스

```text
livedeco census
livedeco state
livedeco off
```

`census`는 자연 재생 중 요청하고 **다음 본 카메라 preCull**에서 한 번 실행한다. AutoTest는 실행 완료를 기다린다. 본 카메라 탐색은 `GpuSplit.FindMain()`을 재사용하고 중복 카메라·장식 manager 없음·reset·다른 GPU 진단·matshare 활성·framecmp 정지를 거절한다. `livedeco on`은 게이트 미통과와 프로토타입 미구현을 명시하며 오류로 거절한다.

`Resources.FindObjectsOfTypeAll<Renderer>()`의 유효한 scene 렌더러를 대상으로 enabled·activeInHierarchy·forceRenderingOff=false를 live로 정의한다. 이미지의 소유자는 `scrVisualDecoration.spriteRenderer`로 연결하고 중복 소유는 오류다. **알파와 frustum으로 걸러내지 않는다.** 이전 bounds 센서스 오류를 피하고 화면 밖 대상도 보수적인 장벽으로 남긴다. live 이미지 분모는 전체 scene 이미지이며, 본 카메라 layer 밖 이미지는 제외 사유로 남긴다. 비소유 장벽은 본 카메라 cullingMask 안의 전체 live 렌더러다. 이는 실제 카메라 draw 측정이 아니다.

후보는 정확한 scrVisualDecoration, 반복 1×1, SpriteDrawMode.Simple, 단일 pass·queue 3000 `Sprites/Tile`, 키워드 없음이다. 마스크·히트박스·켜진 mesh·SortingGroup·split alpha·blend effect·텍스처 없음·여러 재질 슬롯을 제외한다. **태그가 있다는 이유만으로 제외하지 않는다.** 게이트 2에서 live 전환을 처리해야 할 움직이는 대상이 이번 조사 대상이다.

MPB는 별도로 보수적으로 제외한다(`property-block-unverified`). 기존 조사상 SpriteRenderer의 엔진 소유 MPB도 존재하므로 이것이 모두 특수 효과라는 뜻은 아니다. `basic_candidates`는 이 제외 직전의 수이고, `owned_candidates`만 게이트 비율에 쓴다. Unity MPB API는 임의 속성을 열거하지 못하므로 내용을 모르는 상태로 출력 재현 가능성을 선언하지 않았다. MPB 때문에 비율이 낮으면 기본 렌더러 방식 자체를 폐기했다는 결론을 내리지 말고, 해당 shader의 MPB 속성과 엔진 기본값/게임 쓰기 경로를 다음에 관측해야 한다.

각 제외 live 이미지는 **첫 번째 사유 하나**에만 집계한다. `owned_candidates + remaining_live = live`, 사유별 합이 remaining_live라는 분할 검사를 수행한다. 외부 렌더러는 타입별 재질 슬롯 수로도 집계한다.

## 정렬과 런 수

`SceneRenderOrder.Key`로 matshare와 LiveDeco의 정렬 구현을 공유한다. sortingLayer 값 → sortingOrder → queue → 거리 내림차순 → **signed material ID 오름차순**이며, 거리 계산은 기존 matshare의 카메라/전역 sort mode·custom axis·sprite pivot/center를 그대로 사용한다. NaN/Infinity는 거절한다. renderer ID/slot은 진단 출력을 결정적으로 만들기 위한 마지막 fallback이며 Unity의 동률 규칙으로 주장하지 않는다.

소유·비소유가 같은 키와 같은 재질 ID로 묶이면 `ambiguous_material_ties`를 기록하고 게이트를 INCONCLUSIVE로 둔다. SortingGroup, opaque queue 슬롯, 누락 재질이 있으면 이번 평면 정렬 모델로 전체 순서를 확정하지 않고 역시 INCONCLUSIVE로 남긴다. 이 조건을 로그 없이 우회해 PASS로 바꾸지 않는다. 외부 multipass/특수 렌더러의 실제 패스 구성까지 증명하는 도구는 아니다.

- `barrier_runs`: 소유 후보의 연속 구간 수. 비소유 렌더러가 끼는 횟수에 따른 이상적 하한이다. 첫 후보 구간도 1개로 센다.
- `mesh_runs`: 위 장벽 외에 sortingLayer/order/queue/거리, 아틀라스 치환 뒤 재질 내용 서명, GameObject layer가 바뀌면 나눈 수다. 서로 다른 렌더 상태를 메시 하나로 처리한다고 가정하지 않는 보수적인 설계 수다.
- `key_splits`, `signature_splits`, `unity_layer_splits`: 추가 분리 원인이다. 후보 재질 서명은 기존 matshare의 전체 선언 속성을 사용하되 `_MainTex`의 texture ID만 아틀라스로 정규화하고 scale/offset·다른 texture·색·stencil/상태를 유지한다. 텍스처 종류 자체는 런을 나누지 않는다.

이 구분 때문에 barrier_runs≤200만으로 통과시키지 않는다. **확정 가능한 순서에서 mesh_runs≤200, owned_candidates/live≥80%일 때만 해당 snapshot의 GATE1=PASS**다. 빈 입력이나 순서 미확정은 INCONCLUSIVE다. atlas 구현 가능성, 실제 draw/SetPass, 화면 동일성은 이 PASS의 증명 범위 밖이다. 262·270초 두 snapshot을 모두 확인해야 한다.

로그에는 frame/song/camera ID/mask, 전체 이미지/live/후보/비율/제외 수, 두 런 수, 원인별 분류, 상위 10개 후보 서명, 처음 12개 런의 멤버 수·첫 Renderer·빌릴 재질 ID·정렬 키, GATE1와 센서스 CPU ms를 남긴다. snapshot 사이 변화를 연속 추적하지는 않는다. 센서스 CPU ms는 계측 오버헤드이며 성능 절감량이 아니다.

## Claude 실행용 steps

[생성기](../../tools/optimization/livedeco-steps.py)는 JSON만 만들며 sfmeasure import·게임 실행·설치를 하지 않는다. [예제](livedeco-example-steps.json)는 가상 맵 경로이므로 실제 경로로 다시 생성해야 한다.

```powershell
py -3 tools/optimization/livedeco-steps.py --chart '<Arche FULL 실제 경로.adofai>' --output docs/handoff/livedeco-steps.json
# 아래 실행은 Claude 담당이며 이번 작업에서는 수행하지 않았다.
py -3 E:\StutterFixWork\run_json.py docs/handoff/livedeco-steps.json wt-livedeco livedeco-gate1 --probe --keys livedeco,archegpu
```

25단계, timeout_min=15다. 두 시점 모두 `stop → seek 262 12 → waitsongpos <262 또는 270> 40 → stamp → census → state → stop`으로 별도 재생한다. 첫 센서스의 정지 비용이 두 번째 시각을 밀지 않게 했다. 실제 표본 시각은 census의 song/frame 로그로 확인한다. 일시정지·프레임 캡처·성능 창·게이트 2 활성 명령은 넣지 않았다.

| 항목 | 262초 | 270초 |
|---|---|---|
| live / owned_candidates / 비율 | 미측정 | 미측정 |
| barrier_runs / mesh_runs | 미측정 | 미측정 |
| remaining_reason / 순서 미확정 사유 | 미측정 | 미측정 |
| GATE1 | 미측정 | 미측정 |

## 게이트 2 후속 범위

두 snapshot의 게이트가 통과하기 전 아틀라스·셰이더·MeshRenderer·원본 숨김은 만들지 않는다. 현재 코드는 원본 Renderer/Material/Texture/Transform 값을 쓰지 않으며 새 Harmony 패치도 없다. 일회성 preCull 구독은 완료·off·AutoTest 종료/오류·SceneReset·모드 unload에서 해제한다.

통과 뒤 첫 멤버의 원래 재질을 빌려 정렬 위치를 점유하고, 런 안 삼각형은 원래 순서대로 넣는 설계를 검증해야 한다. 이번 fixture는 이 정렬 주장만 지정된 규칙의 모델에서 확인했다. **새 커스텀 shader를 원본의 빌린 material에 어떻게 적용하고 그 material을 공유하는 비소유 대상을 보호할지, 원래 material ID를 보존하면서 atlas texture/상태를 어떻게 바꿀지는 미구현**이다.

압축 아틀라스도 아직 구현 가능성을 확정하지 않았다. DXT 블록 4px 정렬 CopyTexture는 블록 내용을 보존해도 UV 정밀도·clamp·이중선형·mip·색 공간까지 원본 샘플과 같음을 보장하지 않는다. 압축 사본과 RGBA32 대안은 게이트 통과 후 원본 Sprites/Tile 식과 함께 시험해야 한다. 이번에 atlas VRAM/픽셀 차이/CPU 메시 비용/동적 전환 분할/2.5ms 절감은 측정하지 않았다.

게이트 통과 후에도 264초 pause에서 A→on→B→off→A2와 중앙 20% 제외 framecmp 대조를 먼저 수행하고, 화면 동일성이 확인된 뒤 별도 재생 ABBA 262→276초 fuse 창을 측정해야 한다. 두 실행을 이번 census JSON에 미리 섞지 않았다.

## 오프라인 검증

| 검사 | 결과 | 로그 |
|---|---|---|
| Release DEV | 오류 0 / 경고 38 | [DEV](livedeco-build-dev.log) |
| Release Player | 오류 0 / 경고 36 | [Player](livedeco-build-player.log) |
| Release PlayerAuto | 오류 0 / 경고 38 | [PlayerAuto](livedeco-build-auto.log) |
| LoadCheck DEV / Player / PlayerAuto | 266 / 229 / 266곳, 각각 틀림 0 | [DEV](livedeco-loadcheck-dev.log), [Player](livedeco-loadcheck-player.log), [Auto](livedeco-loadcheck-auto.log) |
| LiveDeco 정책·정렬 앵커 fixture | 22,917 checks 통과 | [fixture](livedeco-fixture.log) |
| edition 메타데이터 | 10 checks 통과, 일반 Player 타입/참조 없음 | 같은 fixture 로그 |
| 기존 MatShare 정책 fixture | 25,401 checks 통과 | [회귀 검사](livedeco-matshare-fixture.log) |
| Python steps | 25단계 구조, 독립 표본, 한글·공백 경로 CLI roundtrip 통과 | [steps](livedeco-steps-check.log) |
| `git diff --check` | 통과 | 최종 확인 |

정렬 fixture는 signed ID 극값, layer/order/queue/distance 우선순위, 비소유 끼어들기, 키/서명/layer 분리, 소유/비소유 같은 material tie 거부, 80%·200런 경계, 1,000개 무작위 배열의 첫 멤버 앵커 치환 후 원래 외부 순서를 검사한다. Unity 콜백·카메라별 실제 draw·픽셀을 검사한 것이 아니다. LoadCheck는 IL 필드 참조 검사이며 동적 이름 5곳은 실행 중 확인 영역이다. 기존 참조 버전 충돌·deprecated·미사용 필드 경고가 남는다.

```powershell
dotnet run --project tools/optimization/LiveDecoCheck -c Release -- bin/Player/StutterFix.dll bin/Release/StutterFix.dll bin/PlayerAuto/StutterFix.dll
dotnet run --project tools/optimization/MatShareCheck -c Release
py -3 tools/optimization/test-livedeco-steps.py
```

## 작업 관찰

task-observer v3.5.0과 기존 사용자 범위 관찰 헤더·활성 원칙을 읽었다. 해당 저장소는 허용된 쓰기 범위 밖이라 변경하지 않았다. 작업 관찰은 이 인계에 남긴다: 비소유 장벽만 센 런 하한과 실제 렌더 상태를 반영한 메시 수를 구분하고, 엔진 기본 MPB 존재를 재현 가능성으로 오인하지 않아야 한다. 별도 스킬·메모리는 수정하지 않았다.
