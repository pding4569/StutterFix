# 다음 작업 설계 (2.4.7 이후)

클라우드에서 쓴 계획서다. 빌드·측정은 로컬에서 한다. 각 항목은 **먼저 잴 것 → 고칠 방법 → 같은 결과인지 확인하는 법** 순서로 적었다.
원칙은 그대로다: 효과·판정·소리는 원래 게임과 같게. 측정 코드가 끊김을 만들지 않게(호출이 많은 곳에 Stopwatch 금지, 차단 A/B·할당량으로 잰다).

---

## 1. 큰 맵 편집 화면에서 카메라를 끌 때 남는 작은 튐

**지금 아는 것**: 누르는 순간의 61ms(`ObjectsAtMouse`의 모든 타일 거리 재기)는 `EditorPick`이 없앴다. 끄는 동안 남는 작은 튐은 아직 원인을 모른다.

**먼저 잴 것** (개발자 명령 `campan` 을 넓힌다)
1. `campan` 은 카메라를 직접 옮긴다. 실제 드래그 경로(`scnEditor.Update` 의 마우스 처리)는 거치지 않으므로 두 가지를 따로 재서 비교한다.
   - A: `campan` (카메라만 이동) — 튐이 있으면 원인은 그리기 쪽(타일 보임 변화, 컬링, `TileCull` 켜고 끄기).
   - B: 가짜 마우스 드래그 — `Input` 을 흉내 낼 수 없으므로 `scnEditor` 의 드래그 처리 함수(IL 스캐너로 `Update` 안에서 `ObjectsAtMouse`·`camera` 쓰는 곳을 찾아 이름 확인)를 직접 매 프레임 부른다.
2. 튄 프레임마다 다음을 한 줄로 남긴다: 프레임 시간(Stopwatch, 프레임 경계에서만), 그 프레임의 힙 증가량, `EditorPick.Calls/Fast` 증가, `TileCull` 이 켜고 끈 타일 수, `OnBecameVisible` 류 이벤트 수.
3. A 에서 튀지 않고 B 에서만 튀면 드래그 경로. 둘 다 튀면 그리기 쪽.

**후보와 고칠 방법**
- 드래그 중에도 `ObjectsAtMouse` 가 불리는 경우(끌기 판정·호버): `EditorPick` 이 받는지 `Fast` 로 확인. 빠진 호출 경로가 있으면 같은 방식으로 감싼다.
- 드래그 중 `TransformAccessArray` 재생성: 타일 목록 변경 감지가 드래그 중에 참이 되는지 확인. 되면 감지 조건을 고친다.
- 타일 보임 변화가 한 프레임에 몰림(`TileCull`): 한 프레임에 켜는 타일 수 상한을 두고 나머지를 다음 프레임으로 넘긴다. 단 **편집 화면에서만**(곡 중 보임은 건드리지 않는다).

**확인**: 같은 맵·같은 이동 속도로 끔/켬 각각 10초 × 3번, 16ms 넘는 프레임 수와 최악값 비교.

---

## 2. 고BPM 에서 `ShowHitText` 의 `DOTween.Kill` 전체 훑기

**가설**: 판정 글자를 띄울 때마다 `DOTween.Kill(target)` 이 불린다. DOTween 의 `Kill(target)` 은 활성 트윈 전부를 훑는다(O(활성 트윈 수)). 효과가 많은 맵에서 활성 트윈이 수천 개면 판정마다 수천 번 비교.

**먼저 잴 것**
1. IL 스캐너로 `ShowHitText` (또는 판정 글자를 띄우는 함수, `scrHitTextMesh` 쪽)가 부르는 `DOTween::Kill` / `ShortcutExtensions::DOKill` 확인.
2. 차단 A/B: 개발자 설정으로 그 한 호출만 건너뛰는 판을 만들어(글자가 겹쳐 보여도 측정용), 같은 구간 프레임 시간 비교. Stopwatch 로 감싸지 않는다.
3. 판정 때 활성 트윈 수(`DOTween.TotalActiveTweens()`, 판마다 한 번 최댓값만)를 기록.

**고칠 방법 (결과가 같아야 함)**
- 판정 글자 오브젝트마다 그 글자에 건 트윈을 모드가 기억해 두고, `Kill(target)` 대신 기억한 트윈만 `Kill()` 한다.
  - 트윈을 거는 함수를 가로채 `target == 판정 글자` 인 트윈을 목록에 넣는다(`DecoAnim` 이 쓰는 방식과 같다).
  - 목록 밖에서 같은 target 으로 걸린 트윈이 있을 수 있으므로, 처음 도입 때는 개발자용 대조를 켠다: 원래 `Kill` 이 죽였을 트윈 수(훑어서 셈)와 기억한 수가 다르면 기록.
- `TweenFix`(`isUpdateLoop`)와 겹치는지 확인: `Kill` 이 업데이트 루프 안에서 불리면 즉시 제거가 아니라 표시만 하므로 비용 모양이 다르다.

**확인**: 대조 불일치 0, 고BPM 맵 곡 중 최악 프레임·평균 비교.

---

## 3. 곡 중 쓰레기 `FinishFrameRendering` 약 0.47MB/s

**지금 아는 것**: 곡 중 GC 가 멈춰 있어서 힙 증가량 = 할당량. `AllocScan`/`HeapPath` 로 C# 쪽 할당 대부분은 찾았고 남은 것이 그리기 마무리 단계에 붙어 있다.

**먼저 잴 것**
1. 그 단계에서 불리는 C# 콜백을 찾는다: `Camera.onPreRender/onPostRender`, `RenderPipelineManager`, `OnWillRenderObject`, `OnRenderObject`, `OnBecameVisible/Invisible`, UI `Canvas.willRenderCanvases`. `RenderCallbackScan` 결과를 다시 본다.
2. 콜백별 차단 A/B 대신 **힙 증가량 나누기**: 각 콜백 앞뒤에서 `GC.GetTotalMemory(false)` 차이만 누적(가벼움, Stopwatch 아님). 다른 모드의 콜백도 포함해 이름별로 모은다.
3. 다른 모드를 전부 끈 판과 비교해 모드 탓인지 가른다.

**후보**: `OnBecameVisible` 에서 문자열 만들기, 글자 장식의 메시 다시 만들기(`TextFix` 가 막지 못한 경로), `Canvas` 재구성 시 리스트 할당.
다른 모드 탓이면 고치지 않고 README "다른 모드와 함께"에 적는다(다른 모드 코드·설정은 바꾸지 않는다).

---

## 4. 효과 재사용의 게임 조회 가로채기(`FfxReuse.Arrange`) 부담 줄이기

**지금**: 남겨 둔 컴포넌트(`pending`)나 붙인 순서 기록(`logicalIndex`)이 하나라도 있으면 `GoGet` 이 `GetComponent` 대신 `GetComponents` 로 **배열을 만들고**, 원소마다 해시 조회 1~2번을 한다. 곡 중 타일마다 불리므로 할당과 조회가 타일 수만큼 생긴다.

**고칠 방법**
- 모드가 컴포넌트를 남겨 두거나 순서를 기록한 **게임오브젝트 집합**(`touched`)을 따로 둔다. `pending.Add`/`logicalIndex[...] =` 하는 곳에서 `touched.Add(c.gameObject)`, 비울 때 같이 비운다.
- `GoGet`/`CGet`: `!touched.Contains(go)` 이면 `go.GetComponent<T>()` 를 바로 돌려준다(원래 게임과 같은 호출, 할당 없음).
- `GoGets`/`CGets`: 같은 조건이면 배열을 그대로 돌려준다(원래도 배열 반환).
- `CKids`(자식 포함)는 자식 중 하나라도 touched 일 수 있으므로 그대로 둔다. 필요하면 부모 쪽으로 "자손이 touched" 표시를 올려 둔다(2단계).

**주의**: 지워진 오브젝트가 `touched` 에 남아도 결과는 같다(느린 길로 갈 뿐). `pending.RemoveWhere(null)` 하는 곳에서 `touched.RemoveWhere(go => go == null)` 도 같이.

**확인**: 기존 개발자용 대조(`ffxreuse` 명령)로 원래 방식과 같은 컴포넌트·같은 순서인지. 곡 중 할당량(MB/s)을 전후 비교.

---

## 정리 계획 (빌드가 필요한 것, 로컬에서)

- `AutoTest.cs` 명령 43개: 조사가 끝난 것(`whoholds`, `whoholds2`, `whoholdsmat`, `matrefs`, `newmats`, `countmats`, `orphaninfo`, `orphanrend`, `killorphans`, `dotclear`, `floordump`, `cullexp`, `thumbtest`)은 그 조사를 한 커밋을 주석에 남기고 지운다. 자동 시험에 쓰는 것(`open`, `select`, `play`, `stop`, `quit`, `wait`, `retry`, `auto`, `log`, `mark`, `pick`, `ffxreuse`, `beatfix`, `tilecull`, `campan`)은 남긴다. 지우기 전에 로컬 자동 시험 스크립트가 어떤 명령을 부르는지 확인한다.
- 개발자 전용 진단(`HeapPath`, `FrameScan`, `TimeProbe`, `SlowScan`): player 빌드에서 이미 빠지는지 `Edition`/csproj 조건을 확인하고, 안 빠지면 dev 전용으로 묶는다.
- 큰 파일(`GcControl` 1042줄, `FfxReuse`, `TileCull`, `BeatFix`, `EditorPick`): 동작은 두고, 파일 머리 주석을 "무엇을·왜·측정값·확인 방법" 순서로 맞추고 지난 버전의 고친 기록은 CHANGELOG 로 옮긴다.
