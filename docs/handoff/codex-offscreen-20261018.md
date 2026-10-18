# Codex 작업 5: 화면 밖 장식 애니메이션·갱신 건너뛰기 가능성 조사 + 측정 도구 — 게임 실행·Mods 설치·커밋 금지

선행: `codex-tight` 작업이 끝난 뒤 시작한다(Unity Editor 시험과 게임 측정이 겹치지 않게). 결과: `docs/handoff/codex-offscreen-20261018-result.md`(한국어, 추측은 "추정"). 새 측정 코드는 `OffscreenProbe.cs`(DEV/AUTOTEST 전용, FuseProbe.cs/CpuBreak.cs 와 같은 Compile 조건), AutoTest.cs·csproj 에는 최소 줄. 제품 동작은 바꾸지 않는다(설계만).

## 배경 (Claude 실측, Arche 곡 270초 구간, 95 FPS, 깨끗한 판)

메인 스레드 프레임당 10.47ms(PhaseWatch): Update 스크립트 4.64ms + LateUpdate 스크립트 2.71ms + FinishFrameRendering 1.70 + UpdateAllRenderers 0.49 + 캔버스 0.4 + 기타. GPU 는 4.7ms 라 이 구간은 CPU(메인) 병목.
스크립트 안(cpubreak, 254~269초 평균): `scrDecorationManager.LateUpdate` 1.92~2.4ms(깨어 있는 장식 약 2,150개 각각 `LogicUpdate`→`UpdatePosition`), 모드 `DecoAnim` 장식 애니메이션 갱신 2.4~3.45ms(도는 애니메이션 약 11,700개: 위치X·Y·회전·크기X·Y·색 = 장식당 5~6개, 장식 약 2,170개), `scrVfxPlus.Update` 0.31, 타일 스크립트 0.22, 지휘자 0.12.
같은 시각 census(gpucensus): 본 카메라가 실제로 그리는 후보는 390~1,159개(장식 슬롯 275개 + 타일/행성 등). 즉 깨어 있는 장식 2,150개 중 상당수(추정 절반 이상)가 **카메라 화면 밖**이다. 그런데 DecoAnim 도 LateUpdate 도 화면 밖 장식까지 매 프레임 계산·Transform 쓰기를 한다.
이미 있는 것: `InvisibleSkip`(알파 0 → forceRenderingOff), `Dormancy`(안 보이는=알파 0 장식은 LateUpdate 순회에서 뺌), `DeferTransform`(보이는 장식 회전·크기는 값만 저장), `PairXY`, `LazySprite`. 화면 밖(알파>0 이지만 카메라 밖) 장식은 아직 아무 처리가 없다.

## 아이디어 (검증 대상)

화면 밖 장식은 곡 시간의 닫힌 식(시작값 + 변화량 × 이징(진행도))으로 값이 정해지므로(DecoAnim 은 이미 DOTween 대신 모드가 같은 식으로 돌림), 화면에 들어오기 전 프레임에 한꺼번에 값을 계산해 적용하면 도중 프레임은 건너뛸 수 있다. 위치 X·Y 는 싸게 계산해 화면 밖 판정에 쓰고, 회전·크기·색·엔진 Transform 쓰기·`UpdatePosition`(LateUpdate)을 건너뛴다.
위험: 시차(parallax) 배율이 있는 장식은 화면 위치가 카메라에 따라 매 프레임 변함(census: 시차 배율 1.0 인 것 28개, 0 인 것 12개, 그 밖 1,930개), 회전·크기로 경계 상자가 커짐(큰 이미지 3840×3840 스프라이트), 카메라 흔들림·회전·확대, 마스크/히트박스/편집기/종료 시점 상태(곡 끝 `decosig` 대조), 다른 모드가 읽는 값, 다시 시작·건너뛰기(seek).

## 할 일

1. **측정 도구 `offscreenstat begin|end`**: 곡 시간 구간 동안 프레임마다(또는 4프레임에 1번 표본) 애니메이션이 도는 각 장식에 대해 실제 렌더러 bounds(SpriteRenderer.bounds 또는 월드 bounds; 회전·크기·시차 반영된 지금 값) 와 본 카메라 가시 영역(cullingMatrix 또는 orthographicSize/aspect/회전 포함)의 거리를 구한다. 보고: 프레임당 평균 (a) 화면 안, (b) 화면 밖 1배 이내(마진 25%/50%/100% 세 가지 기준), (c) 1배 넘게 먼 것, (d) 알파 0(이미 처리됨) — 장식 수와 그 장식들이 가진 애니메이션 수(`DecoAnim` 의 활성 기록: 접근자는 `DecoAnim.RunningDecorations()`/recs 참고 — 필요하면 DEV/AUTOTEST 전용 읽기 접근자 추가). 또 (c)/(b)에서 시간이 지나 (a)로 바뀌는 사건의 빈도와 "화면 밖에 머무는 연속 프레임 길이" 분포(1·2~5·6~20·20 초과 프레임). 시차 배율 별(0, 1, 그 밖) 분류. 다음 프레임 위치를 닫힌 식으로 미리 알 수 있는 비율(위치X·Y 기록이 모두 있는 장식).
   표본 비용 때문에 FPS 가 바뀌어도 되지만 측정 값 자체(카운트)가 정확해야 한다. 켜지 않으면 비용 0.
2. **절약 상한 계산 근거**: 위 분포로, "마진 100% 밖이면 그 프레임의 DecoAnim 계산(회전·크기·색, 위치는 판정용으로만)과 LateUpdate UpdatePosition 을 건너뛴다"고 가정했을 때 줄일 수 있는 비율 → 270초 구간 DecoAnim 3.3ms·LateUpdate 2.4ms 에 곱한 상한 ms. 판정에 드는 비용(위치 판정 장식당 몇 µs 가정)을 빼서 순이득 추정(식을 적을 것).
3. **IL/코드로 안전성 조건 조사**(구현 금지): 화면 밖 장식의 Transform/SpriteRenderer 값을 읽는 게임·모드 코드 전수(`scrDecoration` 의 `GetDecorationWorldSize`/`UpdateShader`/마스크/`CheckHitboxHit`/편집기/`scrDecorationManager`, 모드의 `InvisibleSkip`/`Dormancy`/`FastMove`/`MoveApply`), 곡 끝·다시 시작·편집으로 돌아가기·모드 끄기에서 건너뛴 값을 되돌려 놓아야 하는 지점, 카메라 `scrCamera` 가 장식을 읽는 경우(필터·`ffxSetFilter`·`scrScreenTile` 등). 시차 장식의 화면 위치 공식(`scrParallax.SetTrans`, `posCamAtStart`, `multiplier`, `startPosition`)을 써서 "카메라 위치만 알면 화면 위치를 구하는" 닫힌 식 가능 여부.
4. 결론: 구현 가치(예상 순이득)와 가장 안전한 첫 단계(예: 시차 배율 0 인 장식만, 마진 크게, 값 계산은 하되 Transform 쓰기만 건너뛰기 등)를 순서대로 제안하고, Claude 가 게임에서 돌릴 명령 순서(Arche 255~280초, `offscreenstat begin/end`, `decostruct`)를 적어라.
