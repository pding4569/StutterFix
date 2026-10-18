# Codex 작업 B: 같은 이벤트의 속성 묶음(GroupStep) — DecoAnim 한 단계에서 한 장식의 위치 X·Y·회전·크기 X·Y 를 같이 — 게임 실행·Mods 설치·커밋 금지

선행: 작업 A(TightLate)가 끝나 있다. 작업 트리에 A 의 변경이 있으니 덮지 말 것(`git status`/`git diff` 확인). 결과: `docs/handoff/codex-groupstep-20261019-result.md` (**apply_patch 로 UTF-8 BOM 으로 쓰고 한글이 남아 있는지 `Get-Content -Encoding UTF8` 로 확인**), 한국어, 추측은 "추정". 빌드 셋 오류 0. 새 동작은 모드 폴더 `groupstep-off` 파일로 끌 수 있고 `groupstep verify` 자동 시험 명령을 가진다.

## 배경 (Claude 실측)

Arche 곡 252~278초: `DecoAnim` 갱신이 프레임당 약 3.3ms(관측 도구로 재면 8.2ms 로 부풀려짐). 프레임당 `Step` 약 2,785번 + `StepPair`(위치 X·Y 한 쌍) 약 880번(=기록 4,545개), 장식 약 920개 → 장식당 기록 약 5개(위치X·위치Y·회전·크기X·크기Y). 같은 장식의 기록 중 (Pos 비트, Dur 비트, Ease)가 모두 같은 묶음(`updatecost` 의 group sizes, 표본): 5개짜리 54,950묶음·3개짜리 24,106·1개짜리 67,736·2개짜리 2,891 → **기록의 약 65% 가 5개 묶음**. Ease 는 Linear 33%, OutCubic 46%, InCubic 21%. `ZeroTween.Eval` 은 호출당 0.055µs 로 싸다. `Apply`(값 계산) 0.47µs, `OnUpdate`(엔진 설정) 0.62µs, `Step` 전체 약 1.66µs, `StepPair` 2.9µs (모두 관측 도구 포함이라 부풀림, 보정 계수 약 0.4).
지금 `DecoAnim.UpdatePrefix`(DecoAnim.cs, `recs` 순회)는 기록마다 `Step`: Started 확인, `r.Pos += td` + 끝 판정, `Apply`(값), `OnUpdate`(설정 함수 호출: 위치는 `SetPositionX/Y`(`StepPair` 가 X·Y 를 한 번의 `SetPosition` 으로), 회전은 `SetRotation`(보이는 장식은 `MoveApply.DeferTransform` 으로 필드만 저장), 크기는 `SetScale`(같이 DeferTransform)), try/catch, 완료 처리. 지난 PairXY 가 X·Y 두 기록을 하나로 합쳐 장식당 약 0.4µs(270초 구간 +2 FPS) 줄인 것이 보정 기준. **5개를 한 번의 `StepGroup` 으로** 처리해 기록 순회·판정·`Apply` 분기·예외 처리 오버헤드를 줄이는 것이 목표. 비트 동일해야 한다.

## 할 일

1. **묶음 판정**: `recs` 는 만든 순서. 한 이벤트가 한 장식에 시작한 기록들은 `recs` 에서 연속인지(`StepPair` 가 인접 X·Y 만 확인했음 — `DecoAnim.cs` `StepPair`/`UpdatePrefix`)를 코드와 측정으로 확인(`updatecost` 의 group sizes 는 장식·Pos·Dur·Ease 가 같은 것을 센 것이므로 연속 여부와 순서(키 순서)를 따로 센다). 연속이 아니면 장식별 소규모 인덱스로 묶음을 찾는 방법. 한 묶음 안 각 기록의 `Started`, `Pos`, `Dur`, `Ease`, `Over`, `Period`, `Running` 이 **런타임에 비트 동일**임을 프레임마다 확인(싸게: 두 번째 기록부터 첫 기록과 정수 비교), 다르면 그 묶음은 기존 개별 `Step` 으로.
2. **StepGroup**: 한 번의 `dt` 진행·끝 판정, **`e = Eval` 한 번**(결과 재사용은 결정적 함수라 비트 동일 — 근거: `ZeroTween.Eval` 코드/IL 에 전역 상태 없음), 각 키별 값 계산(`Apply` 의 키별 식 그대로: `FStart + FChange * e`, 크기 `VStart + VChange * e` 등 — 같은 연산 순서/같은 float), 설정 호출 순서는 **원래 기록 순서와 같게**(예: 위치 쌍 → 회전 → 크기X → 크기Y 또는 recs 순서) 해서 부수 효과 순서도 동일. 보이는 장식(`DeferTransform`)이면 필드 쓰기만, 아니면 기존 설정 함수. 끝나는 프레임의 `OnComplete`·`Complete`·`Compact`·`Completed++`·`Running=false` 처리도 개별 `Step` 과 같게(키별 FEnd 사용 등 `StepPair` 구현 참고). 예외가 나면 묶음 전체가 아니라 **개별 `Step` 처럼 해당 기록만 끝내고 나머지는 계속**(지난 StepPair 검토에서 "한 축 예외 때 두 축이 같이 끝남" 이 지적됨: 예외 시 묶음을 기록 단위로 풀어 개별 처리하도록 폴백).
3. **`groupstep verify`**: 묶음 처리된 것의 1/64 를 같은 프레임에 개별 `Step` 경로로도 계산해(상태 복사: Pos, FLast/VLast/CLast, 해당 장식의 pivotPosVec/rotAngle/scaleVec 필드를 쓰기 전에 스냅샷하고 두 경로가 같은 입력에서 같은 결과를 내는지 — 장식 필드를 두 번 쓰지 않도록 설계: 예를 들어 값만 계산하는 순수 함수 `Compute(r, pos)` 를 개별/묶음이 공유하고 비교는 `Compute` 결과(키별 값)와 최종 필드 값을 비트 비교) 비교하고 불일치 수·첫 예를 보고. `updatecost` 의 `PosX/Y/Rot/Scale` 그룹 통계 코드(`UpdateCostProbe.cs`)를 재사용 가능.
4. **측정 코드**: 묶음 처리 수/프레임, 묶음 크기 분포, 개별 폴백 이유별 개수, StepGroup 루프 시간(프레임당 Stopwatch 1쌍, 기존 `LastFrameMs` 와 같은 범위). 켜지 않으면 비용 0.
5. 기존 장치와의 결합: `NativeMotion`(Linear 일부는 `Apply` 에서 네이티브 값을 쓴다: `nativeStep`), `PairXY`/`StepPair`, `MoveApply.DeferTransform`/`SkipForLate`, `InstantMove`, `Dormancy`, `UpdateEvalMemo`(Codex 가 만든 기본 끔 메모, 필요 없으면 건드리지 말 것), 개발자용 `Edition.Dev` 의 `Shadow`(진짜 DOTween 짝) 검증 경로(`Prof`/`Dev` 에서는 묶음을 끄는 현재 StepPair 방식과 같게). `DecoAnim.Stat()`/`Summary()` 에 묶음 통계 추가.
6. **예상 이득 식**을 결과에 적어라: 줄어드는 호출 수(프레임당 기록 4,545 → 묶음 수)와 기록당 오버헤드 가정(0.2~0.4µs)으로. PairXY 의 실측(장식당 약 0.4µs)을 기준으로.

Claude 가 게임에서 돌릴 순서(Arche 250~278초 `groupstep-off` 파일 켬/끔 새 프로세스 A/B, `groupstep verify`, 곡 끝 `decosig` 대조, HELLO 전곡)를 결과에 적어라.
