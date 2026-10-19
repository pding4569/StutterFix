# Codex 작업 M: DecoEngine — 영구 소유(알파 전환 때도 안 빼기)와 dirty 플래그 기반 안정성 검사, 색 직접 처리 — 게임 실행·Mods 설치·커밋 금지

결과: `docs/handoff/codex-decoengine4-20261019-result.md` (**apply_patch 로 UTF-8 BOM, 쓴 뒤 `Get-Content -Encoding UTF8` 로 한글 확인**), 한국어, 추측은 "추정". 빌드 셋(Dev/Player/PlayerAuto) 오류 0, `tools/DecoEngineCheck` 통과. 기본 끔 유지. 기준 커밋 16ca13e (Codex L 반영).

## 실측 (Claude, Arche 곡 252.0~273.0초, 새 프로세스, 검증 끔, 곡 시각 맞춤)
| 판 | 모드 애니메이션 갱신 | 장식 관리자 LateUpdate | 합 | 창 프레임 |
|---|---:|---:|---:|---:|
| A 엔진 끔 | 1.90ms | 1.14ms | 3.04ms | 3,030 |
| B2 엔진 켬 (fb4fe3f) | 2.25 | 1.19 | 3.44 | 2,861 |
| **B3 엔진 켬 (16ca13e, Codex L)** | **1.83** | **1.19** | **3.02** | **3,031** |
→ **본전(±0)**. 소유율 81%인데 이득이 없다. 창 구간 누적(전곡 아님: B3 곡 273초 시점 − 252초 시점): 입장 33.6k, 복귀(restore=fallback) 16.3k 중 **알파 전환 복귀(alpha-fallback) 16.2k**, `compact` 15.6k, child-reads 232k(L 전 대비 감소), `hidden-admission-records` 53k. 즉 남은 큰 비용은 (1) 알파 0 전환 때 장식을 엔진에서 빼고 다시 넣는 churn(해시·쓰기 장벽·재입장 `Admit`), (2) 레거시 색(키 9/10) 사슬, (3) 엔진 자체의 장식당 Advance/Late.
이론 한계 추정: 엔진이 장식당 0.6µs(Advance 0.3 + Late 쓰기 0.3)이면 ~2,100 장식 1.3ms + 색 0.5ms = 1.8ms 합, 즉 최대 −1.2ms. 지금 3.02 → 그 쪽으로 가려면 아래를 해야 한다.

## 할 일 (사용자가 추가 제안한 두 가지를 1·2번으로, 효과가 큰 순)
1. **영구 소유 (permanent ownership)**: 입장 자격이 한 번 확인된 장식은 곡이 끝나거나 장면이 리셋될 때까지 고정 정수 슬롯(`active` 배열 인덱스 안정)으로 **계속 소유**하고, 알파 0↔양수 전환은 장식의 내부 `visible` 비트(또는 `awake` 비트마스크)만 토글한다. 출입(`Release`→`Admit`)에 쓰던 해시 조회(`owned`/`candidates`/`lateSkippedSet` Dictionary·HashSet), 쓰기 장벽, 슬롯 재할당을 알파 전환 경로에서 0으로. 핵심은 **알파 0 이 되는 프레임들의 기존 경로 의미를 엔진 안에서 재현**하는 것: 기존 경로에서 보이지 않는 장식은 `MoveApply.DeferTransform/SkipForLate/UpdateNow` 가 "즉시 쓰기"를 하므로(또는 Late 가 건너뛰므로) 최종 Transform/필드가 어떻게 되는지 `docs/handoff/decoengine2-evidence/*.md` 의 IL 조사를 따라 표로 확정하고, 엔진 안에서 `visible=false` 일 때 (a) 필드만 계속 진행·게시 (b) Late Transform 쓰기를 기존 경로가 하던 대로 건너뛰거나 수행하는 두 분기를 구현. 알파 0 을 지나는 프레임의 순서 의존(색 기록이 위치 기록보다 앞/뒤)은 기존 `AlphaSafe`/`AlphaWillSet` 로직을 확장해 안전한 경우만 영구 소유, 불확실한 장식은 이유를 세어 기존 경로로 보낸다(그 장식은 재입장하지 않게 `PermanentlyExcluded` 비트).
   - 목표 카운터: `restores`/`alpha-fallback` 이 창 구간 16k → 100 미만, `Admit` 호출·해시 조회 수 급감.
2. **`Transform.get_localPosition` 폴링 제거를 완결**: 지금 child-reads 가 줄었지만 0 이 아니다(입장·16프레임 주기 등). 외부에서 자식 localPosition 이 바뀌는 경로(`scrDecoration.SetSprite/SetPivotOffset/SetScale/SetRotation`, 에디터 기즈모, 다른 모드)를 Harmony 훅 또는 모드 쪽 setter 지점에서 **dirty 플래그만** 세우게 하고 평상시 네이티브 읽기를 0 으로. 전수 읽기 검증은 Dev/verify 에서만. 훅 개수·비용·다른 모드 충돌과 hook 이 놓칠 수 있는 경로(IL 목록)를 문서화.
3. **색(키 9·10) 직접 처리**: 영구 소유 장식의 색 기록을 레거시 `DecoAnim.Step`→`OnUpdate`→`Action<scrDecoration,Color>`→`SetColor`→`ApplyColor_Patch2` 로 돌리는 대신 엔진 Advance 안에서 값 계산(기존 `Apply` 와 비트 동일)하고 `ApplyColor` 를 직접 호출. 알파가 변하지 않은 프레임(색만 바뀐 프레임)은 `ApplyColor` 호출을 건너뛸 수 있는지(결과 `rendererColor` 가 비트 동일한 경우만; 같은 값 재설정이 부수효과를 가지는지 IL 로 확인). 위험이 크면 구현 대신 설계로 남겨라.
4. **`compact` 15.6k/창**: 엔진이 소유한 기록이 레거시 목록에서 빠지는 `Compact`/`EngineDirty` 비용이 매 프레임 일어나는지 확인하고 줄여라.
5. 효과가 이어 오는 장식(Arche 장식 하나가 8~188개 이벤트를 받음)이 영구 소유 상태에서 새 위치/회전/크기 기록을 **슬롯 교체**로 받는 경로(2단계-a 에서 일부 구현됨)가 해시 없이 O(1) 인지 점검.

## 규칙
- 모든 변경은 `decoengine verify` 불일치 0, `tools/DecoEngineCheck` 확장 통과. 알파 전환·순서 경계 사례(0 을 지나는 프레임, 같은 프레임 색+위치, 효과 재시작, 보임→숨김→보임)를 오프라인 검사에 추가.
- 불확실한 경우 보수적으로 기존 경로로(이유 카운터). 비트 동일이 깨질 위험이 있으면 구현하지 말고 문서에 이유.
- 예상 이득을 항목별로 µs/프레임 추정으로 적어라(채택은 Claude 가 실게임 ABBA 로).
- 결과 문서 끝에 Claude 가 돌릴 순서(`waitsongpos 252`, `fuse`, `decoengine state` 앞뒤 차이, `profsamp begin 1000`, 새 프로세스 ABBA, 곡 끝 `decosig`)를 적어라.
