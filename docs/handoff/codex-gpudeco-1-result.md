# gpudeco-1 결과 — 2026-10-21

**선택: 기존 Unity 6000.3.10f1 AssetBundle 파이프라인을 재사용한다. Editor 라이선스 오류(exit 198)와 실제 export 부재로 랩 G0 검증 단계에서 중단했다. 게임 GPU 렌더러 연결·G1 통과는 미달성이다.**

근거: `fsr/BuildFsr.cs.txt`의 번들 빌드 코드, `docs/shader-research.md`의 기존 번들 사용 구조, 이번 [Editor 로그](../../tools/gpudeco-lab/evidence/smoke-editor.txt)의 **6000.3.10f1** 버전과 라이선스 종료. 네이티브 컴파일 대안으로 방향을 바꿔 승인되지 않은 계측을 진행하지 않았다.

게임 실행·설치·커밋·푸시 **0회**. `.codex/config.toml` 수정 **0회**. 설치된 게임 DLL·설정·맵을 변경하지 않았다. 독립 Editor만 실행했으며 fixture 진입 전 종료됐다.

## 반영

1. **AUTOTEST 전용 `gpudeco export <새 파일 경로>`**: bake ready/기준 세대·인스턴스/일시정지/직교 카메라/Dormancy 조건을 검사한다. bake 지원 ∩ ld2a 입장 규칙 ∩ 현재 awake를 읽어, 16채널 구간·실제 논리값·실제 SpriteRenderer 정점/색·정적 데이터/카메라/정렬 식별자를 저장한다. export 한 번의 후보 순회이며 매 프레임 전체 스캔·쓰기 훅·Transform 쓰기·소유권 전환은 추가하지 않았다. 원본 트윈을 끝내거나 숨기지 않는다.
2. **ld2a 판정 재사용**: `LiveDecoRenderer.ExportReason`은 기존 GeometryReason/재질/스크립트 검사를 쓰되 manager를 명시적으로 받는다. 실제 재질·정렬 복원, Dormancy 이벤트 소유를 검증한 GPU 렌더러는 아직 없다. export 수는 **소유 후보 수**다. 800개 미만이면 `OWNED_LOW` 로그 후 AutoTest 오류로 즉시 중단한다.
3. **독립 랩**: compute 이징·표 이진 탐색·정점 식·Raw 메시 쓰기·CPU/실제 export 대조·번들 제작 진입점을 작성했다. [랩 결과](codex-gpudeco-lab-result.md)에 입력 규모와 미측정 항목을 분리했다. Sprite 피벗/PPU/flip은 실제 로컬 코너와 캡처된 기저에 반영하며, 반복 타일·마스크·부모 추종 등은 원본 후보로 남긴다.
4. **steps 생성기**: [gpudeco-steps.py](../../tools/optimization/gpudeco-steps.py)는 Arche 파일을 한 번 열고 262·270·276초 정지 export를 얻는 JSON을 만든다. 실게임 실행은 하지 않았다. 추가 지시의 단계 순서를 따라 현재 생성기는 **export 단계 전용**이다. 미구현인 GPU on/framecmp G1 단계를 실행하거나 PASS로 요약하지 않는다. `gpudeco state/off/reset/result`는 개발용, `on`은 실제 랩 G0/번들 검증 전 거부한다.
5. **제품 제외 검사**: 일반 Player에 `GpuDecoData`, `GpuStep`, `ExportGpu`가 없음을 LoadCheck로 확인한다. Dev에는 export가 없고 AUTOTEST에만 export가 있다. 기본 GPU 소유 수 **0**, 새 Harmony 패치 **0개**다.

`scrDecoration.UpdatePosition`/`scrParallax.SetTrans` 디컴파일과 현재 DLL의 `tools/ilscan` 출력을 교차 확인했다. 실제 식은 `trueScale = scaleMultiplier × textureScaleMultiplier(XY)`, `camScale = lockScale배율 × trueScale`, `world = (cam - pos) × parallax + pos + parallaxOffset × camScale / textureScaleMultiplier`, 자식 오프셋은 `(pivotOffset + croppedPivot) / trueScale`다. z는 실제 pivot z를 별도 저장한다. IL 근거: [decoration-il.txt](../../tools/gpudeco-lab/evidence/decoration-il.txt), [parallax-il.txt](../../tools/gpudeco-lab/evidence/parallax-il.txt). GPU가 이 식을 허용 오차 안에서 실행하는지는 **미측정**이다.

## 검증

근거: [evidence](../../tools/gpudeco-lab/evidence/). 아래는 오프라인 결과다.

|검사|결과|
|---|---|
|Release Dev / Player / PlayerAuto|각 **오류 0**|
|LoadCheck Dev / PlayerAuto|FieldRef **277**, 불일치 **0**; Setup/PrepVfx 정적 계약 각각 **1개**|
|LoadCheck Player|FieldRef **229**, 불일치 **0**; bake/GPU 타입·명령·export 제외|
|랩 C# / HLSL|C# 오류/경고 **0/0**, HLSL 커널 **2/2** 컴파일 통과|
|새 파일 계약 / export steps|**14건 통과** / 3정지 시점·1회 open·G1 미통과 계약 통과|
|기존 bake/live 구동|계약·복구 fixture 통과|
|기존 DecoSim / 서명|**12 / 10건 통과**|
|ld2a 기하/컬링/스크립트/pose/upload|**2,729 / 482 / 15 / 40 / 32건 통과**|
|ld2a policy / G2 policy|**106,479 / 18건 통과**|
|ld2a delta|**5,918,737검사**, 독립 전체 분할 정답 대비 **6,000변경** 통과|
|ld2a activation / changes / Dormancy / window|**34 / 171 / 41 / 58건 통과**|
|기존 steps|livedeco **144단계**, framecmp **24검사** 통과|

빌드의 기존 어셈블리 충돌·구식 API·미사용 필드 경고는 남는다. LoadCheck의 실행 중 결정되는 5곳은 정적 검사 한계다. `window` 첫 실행은 필수 JSON 인자를 빠뜨려 실패했으며, 생성한 실제 livedeco steps를 전달한 최종 실행이 58건 통과했다. 새 진단 fixture의 초기 예외 필터와 HLSL 예약어 충돌도 수정한 뒤 최종 검사를 통과했다. 동일 코드로 실게임 시험을 되풀이하지 않았다.

## 미달성과 재개 지점

현재 **실제 export 0파일**, **GPU 수치 표본 0개**, **생성 완료 번들 0개**, **실제 소유율·prepare_ms·CPU/GPU 비용·시작 위상·G1 픽셀·FPS 전부 미측정**이다. 오차 최대·평균을 0으로 채우지 않았다. G0 충실도나 CPU ≤0.3ms 목표 달성을 판단할 근거가 없다.

유효한 Editor 라이선스가 준비되고 별도 권한을 가진 실행자가 실제 export를 제공하면, README의 랩 Verify로 채널/정점 오차·최소 800개·70% 이상 커버리지를 확인하고 번들 제작/로드를 검증한다. 실제 약 1,000구간 부하도 추가 검증해야 한다. 이 선행 조건을 통과한 뒤에만 GPU 소유/원본 트윈 인계/복구·카메라 시계·실제 게임 재질/구간 그리기를 연결하고 G1 OFF→ON→OFF를 구현·측정한다. 이 작업은 이번 결과에서 완료한 것으로 표시하지 않는다.

task-observer **3.5.0**을 적용했다. 이 worktree에는 영구 관찰 저장소를 새로 만들지 않고 이 결과로 전달한다. 신규 영구 스킬/메모리 변경은 없다. 라이선스/실제 데이터 부재를 GPU 충실도 통과로 대신하지 않는다는 판단과 미측정 구분을 남겼다.

`GPUDECO_G1=ERROR stage=G0-lab reason=editor_license_exit_198_and_real_export_missing`
