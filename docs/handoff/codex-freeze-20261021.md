# 임무 freeze: Arche 활성 장식 5.7ms 를 "변환 쓰기" / "애니메이션 계산"으로 쪼개는 개발 전용 측정 (게임 안 시각은 망가져도 됨)

작업 폴더 `E:\StutterFixWork\wt-freeze` (브랜치 codex/night-freeze). 게임 실행·설치·커밋·푸시 금지(Claude 가 한다). 결과 `docs/handoff/codex-freeze-20261021-result.md` + `-o`(10줄 요약).
먼저 `docs/optimization-36.md` 끝 절들("비용 분해")과 `docs/ROADMAP-EXTREME-20261021.md` 를 읽을 것. 게임 코드는 `E:/StutterFixWork/decompiled/Assembly-CSharp/<타입>.cs`(근사, ilscan 으로 교차확인).

## 배경 (Gemini 검토에서 나온 가설을 검증하려는 것)
Arche 262→276초 프레임 8.0ms(125 FPS). 장식 정지 맵 2.27ms, 장식 0개 1.82ms → 활성 장식 몫 5.7ms. 모드가 직접 재는 처리 약 3.2ms(DecoAnim 갱신 1.9 + 매니저 LateUpdate 1.16 + 효과 시작 0.13), 나머지 약 2.5ms 는 "엔진 안" 비용.
**가설**: 그 2.5ms 는 살아 있는 렌더러가 있어서가 아니라, 장식마다 `scrDecoration.UpdatePosition` 이 `pivotTrans.localPosition/position/rotation/localScale` 을 매 프레임 써서 엔진이 Transform 계층 행렬·바운드를 다시 계산하는 비용이다. (근거: 렌더러를 전부 `forceRenderingOff` 해도 FPS 가 +6% 뿐이었다 = 그리기 제출 자체는 싸다.) 이게 맞으면 "변환 쓰기를 없애고 메시로 그리는" 설계(livedeco)의 이득은 약 3.5ms 이상이고, 틀리면 훨씬 작다. 지금 확인한다.

## 만들 것: AutoTest 명령 `decofreeze <off|pos|anim|both|state>` (개발/AutoTest 빌드 전용, 일반 Player 빌드에 타입·참조가 없어야 함, 기본 꺼짐, Main.Unload 에서 패치 해제)
- `pos`: 이미지 장식(scrVisualDecoration)의 `UpdatePosition`(및 그 안에서 Transform 에 쓰는 SetPosition/SetRotation/SetScale 호출 경로)을 **건너뜀**. 장식은 켜지는 순간의 자세로 굳지만 렌더러는 그대로 켜져 그려짐. DecoAnim 계산은 그대로 돈다(값만 쌓음).
- `anim`: DecoAnim 의 장식 갱신(채널 평가)을 건너뜀. UpdatePosition 은 그대로(굳은 값을 계속 씀).
- `both`: 둘 다 건너뜀. `off`: 해제. `state`: 모드와 건너뛴 호출 수 로그.
- 건너뛴 호출 수·프레임당 호출 수를 `[decofreeze]` 로그로. 어느 클래스·메서드를 막았는지 근거(호출 경로 확인)를 결과 문서에.
- 주의: 막는 것은 "측정 목적"이다. 효과 시작/타일/행성 등 다른 것은 건드리지 마라. 게임 상태가 깨져 예외가 나면 모드(`sf_errors`) 집계에 어떻게 나오는지 로그로 구분.

## 시험 steps 생성기
`tools/optimization/decofreeze-steps.py --chart <Arche *FULL.adofai> --output x.json` (기존 `livedeco-steps.py`, `decobake`/`fuse` 단계 구성 참고: `open` → `seek 258` 근방 → `waitsongpos 262` → `fuse begin` → 곡 276 → `fuse end`, 모드마다 한 번씩, 모드 순서 **off, pos, anim, both, off, both, anim, pos, off** 식 대칭(ABBA 확장)으로 한 게임에서 여러 구간을 재되 같은 곡 구간 262→276 을 매번 `seek` 로 되풀이하고 앞 여유를 둬 안정화). 각 구간 FPS(프레임 수/시간)·`fuse` 결과를 `[decofreeze] RESULT mode=... fps=... ms=...` 로 한 줄씩. 마지막에 모드별 평균과 off 대비 절감 ms 를 `[decofreeze] SUMMARY` 한 줄로. 곡 중간 되돌려 `seek` 하는 방식이 이미 있는지(`seek <곡 초> [앞 여유]`) 확인하고 쓸 것.

## 빌드/규칙
빌드 3종(`-c Release` Dev/Player(`-p:Edition=Player`)/PlayerAuto(`-p:Edition=Player -p:AutoTestBuild=1`)) 오류 0, 일반 Player 에 이 코드가 없음을 LoadCheck 류로 확인. `.codex/config.toml` 수정 금지. 장식·맵 이름은 "Arche" 만 사용, 비공개 사항 언급 금지.
설계 한계로 막히면 결과 문서에 구체적으로 적고 멈춰라.
