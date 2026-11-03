# 지시서: 명세 기준으로 GpuDeco 맡는 범위 넓히기 — G1

작업 트리 `E:\StutterFixWork\wt-partial` (branch `claude/gpudeco-partial`). 먼저 `docs/deco-spec/00-deco-spec.md`를 끝까지 읽는다. 명세와 코드가 다르면 게임 코드(`E:\StutterFixWork\decompiled\Assembly-CSharp`)를 확인하고 명세를 고친다. `docs/deco-spec/gemini-review.md`는 외부 검토 의견이다. 아래 "검토 의견 검증" 표에서 틀렸다고 한 내용은 쓰지 않는다.

## 규칙

- 맵 이름·곡 시각·장식 개수에 맞춘 상수와 예외 금지. 외부 굽기 금지.
- 기본값 변경 금지(`GpuDeco` 기본 0). 푸시 금지. 단계마다 로컬 커밋. 안전 검사를 근거 없이 빼지 않는다.
- 빌드: `dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1` → `bin/PlayerAuto`.
- 게임 시험은 `python E:\StutterFixWork\run_locked.py <steps.json> wt-partial <이름> [--keys ...] [--tail N]`로만. 예시 명세: `E:\StutterFixWork\clean-hl-partial.json`. 작업 폴더는 `E:\StutterFixWork`.
- **게임이 도는 동안 빌드·대량 파일 읽기·분석 스크립트를 함께 돌리지 않는다.** 2026-11-03에 이것 때문에 없는 끊김을 잰 적이 있다(HELLO 끊김 20번 → 깨끗한 재측정 0번).
- 시험이 끝날 때마다 설치 DLL이 `E:\StutterFixWork\restore\StutterFix-rc-player.dll`과 같은지, `gpuprobe.dll`이 없는지, `Settings.xml`의 `GpuDeco`가 0인지 확인한다.
- 측정 없는 통과 금지. 추정은 추정이라고 쓴다. 비교는 같은 조건의 끔/켬으로 하고 판 수를 적는다. 화면 크기를 로그에서 확인해 함께 적는다.
- 결과는 `docs/deco-spec/results/g1.md`, 코드 구조는 `docs/deco-spec/results/g1-code-structure.md`(파일별 역할, 실행 흐름, 플래그, 안전장치, 한계)에 쓴다.

## 현재 측정 (깨끗한 재측정, GpuDeco 설정 켬, 2560x1440, 맵마다 1판씩)

| 맵 | 기준 빌드 `wt-cand` (거부 상태) | 시제품 `wt-partial` | 결과 |
|---|---|---|---|
| HELLO | 271 FPS, 끊김 0, 최악 10초 217 | 272 FPS, 끊김 0, 최악 10초 216 | 지원 38개로 거부. 거부 경로는 해가 없음 |
| 7777 | 370 FPS, 끊김 0, 최악 10초 260 | 342 FPS, 끊김 0, 최악 10초 252 | 소유 88/498개로 켜짐. 손해 재현(앞선 판 346) |
| DDONGSSADA | 342 FPS, 끊김 1, 최악 10초 208 | 250 FPS, 끊김 25, 최악 10초 28 | 소유 455개. 21.6초에 전체 복귀(594ms 프레임), 그 뒤에도 80초까지 28~54 FPS |
| Exit (앞선 판, 다른 작업과 겹침) | 384 FPS, 끊김 0 | 362 FPS, 끊김 1 | 소유 949개, 곡 중 전체 복귀 |
| Arche (앞선 판) | 469 FPS, 끊김 0 | 464 FPS, 끊김 0 | 맡는 범위 같음, 오차 범위로 보임 |

맵 파일 기준 단계별 상한(값 안 바뀜 + 가시성 / + 잠금 / + 잠금·카메라·행성): HELLO 31.7 / 39.0 / 56.9%, Windflower 48.6 / 49.8 / 50.7%, Exit 85.8 / 89.8 / 89.8%, 7777 29.9 / 37.6 / 37.6%, DDONGSSADA 20.0 / 20.8 / 21.0%, Arche 98.5 / 98.5 / 98.6%. 실제 소유 수는 이보다 적을 수 있다(Windflower 계산 176개, 실제 33개).

## 검토 의견 검증

| 내용 | 판정 |
|---|---|
| 곡 중 GPU가 표를 안 읽음(`GpuLiveSkip`→`ApplyGpuLiveEffectToTarget`→`AdvanceGpuLiveSources`→`EvaluateCollapse`) | 코드 확인 |
| 잠금·`dontAlterX/Y`는 셰이더와 카메라 값이 이미 구현됨 | 코드 확인 |
| `DecoBake.Core.cs:368`이 시차 채널(12, 13)을 끝값 계산에서 빠뜨림 | 코드 확인 |
| 대체 장식이 있으면 디스패치가 2번, 장벽 감시가 8프레임마다 전수 순회 | 코드 확인, 비용(ms)은 미측정 |
| 잠금만 풀면 HELLO 56.9% | 틀림. 39.0% |
| 전체 복귀의 대표 원인이 크롭 이벤트 | 틀림. 6개 맵 크롭 이벤트 0개 |
| HELLO 끊김이 GC 때문 | 해당 없음. 끊김은 측정 오염이었다 |
| 축정렬 가리개 컬링 -37.9% | 틀림. 6개 프레임 시험에서 접히는 양 0% |
| 손익 분기 45%, 7777 계산이 실측과 일치 | 비용값을 맞춰 넣은 추정. 기준값으로 믿지 않는다 |

## G1 작업

1. **거부 규칙을 값 기준으로 바꾼다.** `DecoBake.Reasons`가 필드 사용만 보고 거부하는 것을, 대상 장식별로 실제 값이 바뀌는지 보도록 고친다. 예외: 명세 4절 1번. `maskingType` 설정은 값이 같아도 숨긴 장식을 다시 켠다. 해당 장식의 visible이 곡 내내 참일 때만 무시한다.
2. **`가시성/깊이 변경` 사유를 둘로 나누고 가시성을 처리한다.** `rendererEnabled`와 실제 그리기 켜짐을 구분한다. `SetVisible`과 `CheckPlayerFrame`의 소유 렌더러 검사가 충돌하지 않게 하고, 복원 때 CPU 상태를 맞춘다.
3. **잠금과 시차 축 고정 거부를 푼다.** `lockRotation`, `lockScale`, `dontAlterX/Y`. 복원 경로(`StopGpuLive`)에서 카메라가 반영된 회전·크기가 원본과 맞는지 확인한다. Camera·행성 배치는 G2다.
4. **전체 복귀를 고친다.** 복귀 원인은 없는 이미지 파일을 가리키는 이벤트로 추정한다(`FastMove.ImgSafe` 거부 → `Orig`). 없는 파일 이벤트가 Exit 1건, DDONGSSADA 41건, 복귀 없는 7777 0건이다. `gpudeco` 명령의 `original_guard` 추적(`SetGpuOriginalTrace`, `take_refusal`)으로 먼저 확정한다.
   - 대상에 소유 장식이 없으면 복귀하지 않는다.
   - 없는 파일은 게임이 스프라이트를 비우는 것과 같게 소유 장식에서 처리한다.
   - 그 밖의 거부는 전체 복귀가 남아도 되지만 횟수와 사유를 로그로 남긴다.
5. **전체 복귀 뒤의 성능 붕괴 원인을 찾는다.** DDONGSSADA는 복귀 뒤 수십 초 동안 28~54 FPS였고, 복귀 전 0~10초도 175 FPS(기준 361)였다. 원인을 찾아 고치거나, 못 고치면 근거를 보고한다.
6. **시차 끝값을 통일한다.** `DecoBake.Core.cs:368`.
7. **낮은 소유율에서 켜지 않는 일반 활성화 조건을 넣는다.** 맵 이름이 아닌 소유 비율과 구간당 평균 장식 수로 판단한다. 기준값은 측정으로 정하고 근거를 적는다. 조건 미달 맵은 기준 빌드와 FPS·끊김이 같아야 한다.

크롭, `originalSize`, 반복, 깊이 변경, 마스크 직접 처리, Camera·행성 배치, 칠하는 양은 이번 범위가 아니다.

## G1 합격 기준

- **맡는 범위:** Exit, Windflower의 지원 수·실제 소유 수를 로그로 남기고, 맵 파일 상한과의 차이를 사유별로 설명한다.
- **복귀:** Exit, Windflower, DDONGSSADA, 7777, HELLO, Arche 곡 전체에서 전체 복귀 0번. 남으면 사유를 적는다.
- **픽셀:** Exit, Windflower, Arche에서 정지 프레임 3지점 이상 + 가시성 전환 직후 1지점 이상 + 다시 하기 뒤 1지점. 채널 차이 `>3/255` 픽셀 0. 기존 도구: `FrameCompare`, `framecmp`, `run_locked.py`.
- **끊김:** 곡 전체 끊김이 기준 빌드보다 늘지 않음.
- **FPS:** Exit, Windflower 곡 평균과 최악 10초를 기준 빌드와 ABBA 4판으로 비교. Arche 회귀 확인 ABBA 4판.

**G1이 끝나면 멈추고 보고한다.** 다음 층으로 가지 않는다. Exit에서 FPS가 오르지 않으면 어느 단계 비용인지 재서 보고한다. 막히면 시도와 근거를 `docs/deco-spec/results/g1.md`에 쓰고 멈춘다. 마지막 답에는 합격 항목, 불합격 항목, 측정 표, 설치 복원 확인 결과를 쓴다.
