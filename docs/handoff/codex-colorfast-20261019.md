# Codex 작업 N: 색 처리 사슬 직통 경로 (보임→보임) + LateUpdate 동일값 쓰기 재검토 — 게임 실행·Mods 설치·커밋 금지

결과: `docs/handoff/codex-colorfast-20261019-result.md` (**apply_patch 로 UTF-8 BOM, 쓴 뒤 `Get-Content -Encoding UTF8` 로 한글 확인**), 한국어, 추측은 "추정". 빌드 셋(Dev/Player/PlayerAuto) 오류 0. 새 기능은 기본 **끔**(`colorfast-on` 파일로 켬; 로드 때 한 번 읽음), 검증 `colorfast verify|state`. 화면 결과는 **비트 동일**해야 한다. DecoEngine(`DecoEngine.cs`)은 실게임에서 이득이 없어 기본 끔으로 둔다 — 이 작업은 엔진을 쓰지 않고 **기존 경로 안에서** 사슬을 줄인다.

## 배경 (실측)
Arche 곡 252~273초 프로파일(메인 스레드, 표본 21k, `profsamp`): 색 처리 사슬 약 12% = `DecoAnim.Step`(키 9/10) 6.2% → `OnUpdate` 5.2% → `Action<scrDecoration,Color>` 델리게이트 4.4% → `scrDecoration.SetColor` 4.3% → `scrVisualDecoration.ApplyColor`(Harmony 패치 `ApplyColor_Patch2`) 4.2% + 그 Postfix `InvisibleSkip.After`(`hidden` 해시), `Dormancy.WakePost`(`dormant` 해시). 한 프레임 도는 색 기록 약 1,800개(`animstat`: 색 1,833 / 위치X 1,786 / 회전 1,848 / 크기 1,610). 모드가 이미 `투명→투명`일 때만 `ApplyColor` 를 건너뛴다(`InstantMove.HiddenStay`, 길이 0 이동용 `HidSet`; 길이 있는 트윈은 `DecoAnim` 키 9/10 `OnUpdate`→`setCol`/`setOpa` 델리게이트로 전부 `ApplyColor` 호출).

## 할 일 1: `보임(oldA>0) → 보임(newA>0)` 직통 경로
- 대상: 정확히 `scrVisualDecoration`, `!stickToFloor`(또는 타일 알파가 있으면 `ApplyColor` 와 같은 식·순서 `c.a * o * f` 로 계산), 직전 `rendererColor.a > 0f` 이고 새 그리기 색 `a > 0f`.
- 먼저 `ApplyColor` IL 을 전수 읽어라(`docs/handoff/decoengine2-evidence/ApplyColor-il.md` 가 시작점; 필요하면 `tools/ilscan`, ilspycmd): 필드 쓰기(`color`/`opacity`/`rendererColor`), `spriteRenderer.color` 쓰기, `UpdateRendererEnable()`(→ `rendererEnabled`, `forceRenderingOff`, `GetVisible()`), 기타 부수효과(콜라이더·마스크·`lateSkipped` 등). **보임→보임이고 가시성 상태가 안 바뀌는 프레임**에 `ApplyColor` 를 부르지 않고 같은 필드·같은 엔진 호출만 하면 결과가 비트 동일한지 증명한다(`UpdateRendererEnable` 결과가 같은 입력에서 같은지, `Color` 비트 비교 후 같으면 `spriteRenderer.color` 쓰기 생략이 허용되는지: 같은 값을 다시 써도 엔진이 dirty 를 세우는 부수효과가 렌더에 영향 없는지 — 불확실하면 쓰기는 유지하고 사슬 호출만 줄인다).
- `InvisibleSkip.After`/`Dormancy.WakePost` 는 이 상황에서 상태가 바뀌지 않는다는 근거(hidden 아님→보임 유지, 깨어 있는 장식→Wake 무변화)를 코드로 확인하고, 경계(`<=0 ↔ >0`, 깨움이 필요한 잠든 장식, `Precheck`/`TouchDeco` 가 하는 일)는 기존 `ApplyColor` 를 그대로 부르게 한다.
- 구현 위치 후보: `DecoAnim.OnUpdate` 의 키 9/10 처리에서 `setCol` 델리게이트 호출 대신 직통(델리게이트·Harmony 래퍼 우회; `AccessTools.FieldRef`/미리 만든 DynamicMethod 로 필드 쓰기). `DecoEngine` 의존 금지(`AlphaWillSet` 같은 엔진 훅은 엔진이 꺼져 있으면 무동작이어야 함).
- 예상 이득 식: 프레임당 색 기록 N(≈1,800) × (현재 호출당 µs − 직통 호출당 µs). 마이크로 벤치(`colorfast bench`: 더미 장식·`SpriteRenderer` 가 필요하면 실제 객체를 쓰는 메뉴 전용 시험)로 호출당 µs 를 재고, 벤치만 믿지 말라는 지난 교훈을 문서화.
- 검증: `colorfast verify`: 표본(1/32) 직통 호출 직후 같은 입력으로 **원래 경로를 복제 객체가 아닌 순수 계산으로**(또는 별도 시계로) 구한 `color`/`opacity`/`rendererColor`/`spriteRenderer.color`/`forceRenderingOff`/`enabled`/`rendererEnabled` 와 비트 비교. 가능하면 같은 프레임에 원래 `ApplyColor` 를 호출하는 개발자용(Dev) 대조 모드(직통 후 원래 경로를 돌려 같은 결과인지 확인; 값이 같은 재호출이 무해한 경우만). 기존 `decosig` 곡 끝 대조·`tightlate verify`·`late-verify` 가 계속 통과.

## 할 일 2: LateUpdate 의 `SetRotation`/`SetScale`/`SetTrans` 동일값 쓰기 (평가만, 구현은 필요하면 최소)
사용자의 제안: `scrParallax.SetTrans`(`cachedTransform.position`), `scrDecoration.SetRotation`(`pivotTrans.rotation`), `SetScale`(`pivotTrans.localScale`)이 값이 같아도 매 프레임 쓰고, 유니티가 같은 값이어도 dirty 를 세워 `FinishFrameRendering`(약 1.7ms)을 키운다는 가설. **이미 해 본 것**: `TightLate.cs` 가 이 세 setter 를 장식당 한 번씩만 쓰고(`SkipSame`: 직전에 쓴 비트값과 같으면 생략; 곡 252초 부근 건너뛴 비율 위치 1%·회전 9%·크기 34%, 이득 작아 기본 끔 `tightskip-on`; 소스상 `SkipSame` 기본값이 true 처럼 보이니 실제 기본값과 `tightskip-*` 파일 처리부터 확인해 문서에 적어라), 카메라 값은 매니저 LateUpdate 시작에 한 번 읽는다(`TightLate.Process`). 따라서:
- (a) 같은 값 쓰기를 막아도 이득이 작았던 이유(회전은 카메라 Z 회전이 매 프레임 변해 대부분 값이 다름 등)를 `SkipSame` 통계로 설명하고, 제안(직전 프레임 비트값 비교, 카메라 캐시)이 **현재 TightLate 와 어떻게 다르며 남은 이득이 있는지** 판단.
- (b) `FinishFrameRendering` 비용이 Transform 쓰기 횟수(dirty)에 비례하는지: 모드 코드로 확인 가능한 범위(엔진 소스 접근 불가)에서 가능한 설명/증거를 정리하고, 실게임으로 확인할 최소 실험(예: `tightskip-on` ABBA 로 `FinishFrameRendering` 시간 `fuse`/`PresentMon`/`profsamp` 비교)을 설계만 해라.
- (c) TightLate 를 타지 않는 장식(제외 사유 분포: hitbox/mask/mesh, follow/stick/lock, parallax, hidden)이 있는지, 그들의 `UpdatePosition`→`SetTrans` 이중 쓰기(`localPosition` 후 `position`)가 남아 있는지(사용자 제안 3): 있다면 비중(`tightlate state` excluded 카운터)과, 합칠 수 있는지 IL 로 판단. TightLate 대상은 이미 한 번 쓴다.

## 규칙
- 비트 동일이 증명되지 않거나 불확실한 경우는 기존 경로로(이유를 세는 카운터). 새 해시 조회를 추가하지 말 것. 새로 걸린 Harmony/이벤트는 `Main.Unload` 에서 해제.
- 결과 문서에 항목별 예상 절감을 프로파일 퍼센트(메인 스레드 프레임 약 7ms 기준)로 적고, Claude 가 돌릴 순서(`colorfast-on` 켬/끔 새 프로세스 ABBA, `waitsongpos 252` + `fuse begin/end` + `profsamp begin 1000/end`, `colorfast verify`, 곡 끝 `decosig` 대조)를 적어라. 채택 기준: 같은 창에서 모드 애니메이션 갱신 시간 감소, verify 불일치 0, 곡 끝 decosig 새 차이 없음.
