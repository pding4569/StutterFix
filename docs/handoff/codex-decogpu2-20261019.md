# Codex 작업 Q: DecoGpu 프로토타입 실게임 디버깅 — begin 은 되는데 renders=0, 세션이 compare 전에 스스로 멈춤 — 게임 실행·Mods 설치·커밋 금지

결과: `docs/handoff/codex-decogpu2-20261019-result.md` (**apply_patch 로 UTF-8 BOM, 쓴 뒤 `Get-Content -Encoding UTF8` 로 한글 확인**), 한국어. 빌드 셋 오류 0. 기준 커밋 7bf2892 (아래 Claude 의 실게임 수정 반영: 후보를 `TightLate.AllDecorations()` 전체로, 탈락 이유 카운터, `HasPropertyBlock()` 거절 제거, 추상 메서드 패치 제외).

## Claude 가 실게임(Arche, 곡 250초, 3440x1440)에서 확인한 사실
1. **원래 코드는 실게임에서 한 번도 선정에 성공하지 못했다**: `HasPropertyBlock()` 이 SpriteRenderer 에서는 거의 항상 true(스프라이트 텍스처 내부 블록)라 273개 후보가 전부 탈락 → 그 조건을 뺐다. 그 조건이 안전장치로 의도한 것(외부 MaterialPropertyBlock)은 `GetPropertyBlock` 으로 알려진 속성을 읽어 확인하는 식으로 다시 설계해라(어떤 속성이 실제로 쓰이는지 Arche 에서 샘플).
2. 그 다음 `harmony.Patch(method)` 가 추상 메서드에 걸려 `Method has no body` → `!IsAbstract && GetMethodBody() != null` 로 해결.
3. 현재 상태(7bf2892): `decogpu begin 500` 성공: **managed=241, batches=40**, `active=True`. 그러나 begin 3초 뒤 `decogpu state`: **renders=0 draws-total=0 compute-ms/render=0** → 한 번도 그리지 않음(원본은 `forceRenderingOff=true` 라 장식이 사라졌을 것). `decogpu compare` 이후 state: `active=False status=stopped (lifecycle/eligibility/unload)` restore=241(복원은 성공, mismatch 0). 즉 (a) `Camera.onPreCull += PreCull` → 그리기가 실제로 안 불린다, (b) compare 전에(또는 compare 안에서) 세션이 "lifecycle/eligibility" 로 멈춘다.
   - 재현 순서: `waitsongpos 250`, `decogpu begin 500`, `wait 3`, `decogpu state`, `decogpu compare`, `decogpu state`, `decogpu end`. 곡 시각 250초 부근 Arche(곡 252초 부근에서 깨어 있는 보이는 장식 약 270개 / 전체 TightLate 후보 28,835개 중 대부분 숨김 `Eligible` reason 3).
   - 게임 카메라는 `camera.actualRenderingPath == Forward`, 파이프라인 Built-in, Unity 6000.3.21f1. 장식은 `Sprites/Tile` 셰이더(대부분) + 일부 `Sprites/Default`, 한 장식마다 sortingOrder 가 거의 유일(`groups` 로그: 대부분 count=1) — 그래서 배치 40개는 연속 같은 상태끼리 합친 결과.

## 할 일
1. **왜 renders=0 인지 코드로 찾아라**: `PreCull` 이 호출되는 조건(`Camera.onPreCull` 은 카메라별로 모든 카메라에서 불림 — `GameCamera(camy)` 와 비교하는 부분, 게임이 카메라를 `Camera.Render()` 로 직접 그리거나 `enabled=false` 로 두는지, 커맨드 버퍼 `BeforeForwardAlpha` 가 카메라가 실제 렌더링하는 이벤트인지(Forward vs 게임이 쓰는 렌더 타깃/후처리 경로, `camRT` 에 그리는 별도 카메라?)). 게임은 장면을 `camRT` 로 그리고 최종 카메라가 그 RT 를 화면에 그린다는 정황이 있다(CLAUDE.md/FrameGen 연구: "camRT", "최종 카메라"). **장식을 그리는 실제 카메라와 `GameCamera(...)` 가 같은지, `CameraEvent.BeforeForwardAlpha` 가 그 카메라의 투명 패스 이전인지** 검증(IL/`FrameGen*.cs` 의 카메라 탐색 코드 참고)하고 틀렸으면 수정.
2. **세션이 멈추는 경계**를 진단 가능하게: 멈출 때마다 이유(어느 `Stop(reason)` 호출 지점/조건, 장식·값)를 `status` 에 남기고 `state` 에 첫 중단 이유를 영구 표시. `compare` 가 `Camera.Render` 를 두 번 부르면서 자기 자신의 경계 훅(`BoundaryPrefix`: `SetVisible`/`Setup` 등)이나 `lifecycle` 검사에 걸려 멈추는지 확인.
3. **프로토타입이 실게임에서 실제로 그리게 되도록** 고쳐라. 불확실한 부분은 개발 진단(`decogpu probe`: 카메라 목록, 각 카메라의 `actualRenderingPath`·`targetTexture`·`enabled`·`depth`·`cullingMask`·`GetCommandBuffers` 와 장식 `layer` 가 어떤 카메라에 보이는지, `Camera.onPreCull` 호출 카메라 이름 로그)를 추가해 다음 라운드에 쓸 수 있게 한다.
4. `FuseProbe` 의 DecoGpu 줄이 제대로 나오는지 확인.
5. 결과 문서에 원인 분석, 수정, Claude 가 돌릴 순서(위 재현 순서 + `decogpu probe`, 성공 시 `compare` 결과 해석, 그다음 `waitsongpos 252` + `decogpu begin 10000` + `fuse`/`profsamp` ABBA)를 적어라.

## 규칙
- 제품 코드 경로는 건드리지 않는다. DEV/AUTOTEST 전용 유지. 새로 건 것은 `end`/`Unload` 에서 전부 해제(실게임에서 begin 실패 시에도 부분 설치가 남지 않게 begin 전체를 try/catch 로 롤백).
