# 자동 반복 2회차

직전 게임 시험 결과(자동 수집): `docs/handoff/gg-autoloop-run1-evidence.txt`. 먼저 `docs/handoff/gg-autoloop-ledger.md` 장부를 읽고, 이미 실패한 접근은 되풀이하지 말고 결과에서 배운 점으로 다른/더 나은 접근을 택해라(같은 코드를 다시 돌리는 것은 금지). 게임 코드는 `E:/StutterFixWork/decompiled/Assembly-CSharp/<타입>.cs` 디컴파일본으로 읽어라(근사, ilscan 으로 교차확인). 성공 조건(정규식) `GPUDECO_G1=PASS` 이 아직 안 나왔다. 결과를 분석해 원인을 코드로 찾아 고치고, 안 되는 이유가 설계 한계면 결과 문서에 구체적으로 적고 멈춰라. 아래는 원래 임무다.


---
# 자동 반복 7회차

직전 게임 시험 결과(자동 수집): `docs/handoff/gg-autoloop-run6-evidence.txt`. 먼저 `docs/handoff/gg-autoloop-ledger.md` 장부를 읽고, 이미 실패한 접근은 되풀이하지 말고 결과에서 배운 점으로 다른/더 나은 접근을 택해라(같은 코드를 다시 돌리는 것은 금지). 게임 코드는 `E:/StutterFixWork/decompiled/Assembly-CSharp/<타입>.cs` 디컴파일본으로 읽어라(근사, ilscan 으로 교차확인). 성공 조건(정규식) `GPUDECO_G1=PASS` 이 아직 안 나왔다. 결과를 분석해 원인을 코드로 찾아 고치고, 안 되는 이유가 설계 한계면 결과 문서에 구체적으로 적고 멈춰라. 아래는 원래 임무다.


---
# 자동 반복 2회차

직전 게임 시험 결과(자동 수집): `docs/handoff/gg-autoloop-run1-evidence.txt`. 먼저 `docs/handoff/gg-autoloop-ledger.md` 장부를 읽고, 이미 실패한 접근은 되풀이하지 말고 결과에서 배운 점으로 다른/더 나은 접근을 택해라(같은 코드를 다시 돌리는 것은 금지). 게임 코드는 `E:/StutterFixWork/decompiled/Assembly-CSharp/<타입>.cs` 디컴파일본으로 읽어라(근사, ilscan 으로 교차확인). 성공 조건(정규식) `GPUDECO_G1=PASS` 이 아직 안 나왔다. 결과를 분석해 원인을 코드로 찾아 고치고, 안 되는 이유가 설계 한계면 결과 문서에 구체적으로 적고 멈춰라. 아래는 원래 임무다.


---
# 자동 반복 2회차

직전 게임 시험 결과(자동 수집): `docs/handoff/gg-autoloop-run1-evidence.txt`. 먼저 `docs/handoff/gg-autoloop-ledger.md` 장부를 읽고, 이미 실패한 접근은 되풀이하지 말고 결과에서 배운 점으로 다른/더 나은 접근을 택해라(같은 코드를 다시 돌리는 것은 금지). 게임 코드는 `E:/StutterFixWork/decompiled/Assembly-CSharp/<타입>.cs` 디컴파일본으로 읽어라(근사, ilscan 으로 교차확인). 성공 조건(정규식) `GPUDECO_G1=PASS` 이 아직 안 나왔다. 결과를 분석해 원인을 코드로 찾아 고치고, 안 되는 이유가 설계 한계면 결과 문서에 구체적으로 적고 멈춰라. 아래는 원래 임무다.


---
# gpudeco 1단계: 곡 시각 구운 표 + GPU 계산으로 활성 장식의 CPU 비용을 0 에 가깝게 (G0·G1)

게임 실행·설치·커밋·푸시 금지(자동 루프가 한다). `.codex/config.toml` 수정 금지. 장식·맵 이름은 "Arche" 만. 한국어로.
먼저 읽을 것(순서대로, 필요한 부분만): `docs/ROADMAP-EXTREME-20261021.md` · `docs/optimization-36.md` 끝 절들(특히 "livedeco gate 2 결과와 싼 대안 검토", bake 절) · `docs/handoff/codex-bake-9-20261021-result.md`(구운 표·`DecoBakeCore`·`BakeLive`·소유 규칙) · `docs/handoff/codex-gate2-A-result.md`, `docs/handoff/ga-autoloop-ledger.md`(실패 이력) · `docs/handoff/decovis-evidence.txt`. 게임 코드는 `E:/StutterFixWork/decompiled/Assembly-CSharp/<타입>.cs`(근사, `tools/ilscan` 교차확인). 이 워크트리는 livedeco(ld2a: Dormancy 이벤트 소유·구간·재질 빌리기) + bake(`DecoBake*.cs`)를 합친 것이다.

## 왜 이것인가 (측정된 사실)
Arche 262→276초 프레임 8.0~8.4ms(124 FPS). 장식 정지 맵 2.27ms. 활성 이미지 장식 약 2,000개(85~95% 가 화면에 보임). 장식 몫 약 5.7ms: 모드 장식 애니메이션 1.9ms(그중 설정 함수가 약 70%) + 관리자 LateUpdate 1.2ms + **엔진이 움직이는 Transform 을 처리하는 비용 약 2.5~3.3ms**(`decofreeze`: Transform 쓰기를 전부 막으면 −3.3ms; 쓰기 호출 자체는 호출당 0.09µs 라 압축해도 안 줄어든다 — PosFast·SkipSame 이 +0.2% 였던 이유).
지난 시도(gate 2 A: SpriteRenderer 를 CPU 정점 메시로 대체)는 **CPU 정점 계산 2.5ms + 업로드 0.9ms + 변경 감지(쓰기 훅) 4~6ms** 로 순손실(켬 52~56 FPS)이었고 픽셀도 43만 개 다름(최대 6/255). 결론: **장식 값의 계산·변경 감지·정점 생성을 CPU 에서 하면 안 된다.** 이번 방향은 장식 애니메이션이 이벤트(타일 시각)에서 시작하는 **시간 함수**라는 점을 이용한다: 맵 열 때 구운 표(`DecoBake`) 를 GPU 버퍼에 올리고, **GPU 가 곡 시각 t 에서 모든 소유 장식의 위치·회전·크기·색을 직접 평가해 구간 메시의 정점 버퍼에 쓴다.** CPU 는 프레임당 곡 시각·카메라 값 몇 개와 디스패치 1번만 한다(목표 ≤0.3ms). Transform 은 안 건드리고, 변경 감지·정점 업로드가 없다.

## 시간 의미 (사용자가 승인한 허용 오차)
곡 시각 기반 시계(`DecoBakeCore.SongClock`: 곡 시계 위에 원본 delta 누적과 프레임당 1% 끌어당김, 50ms 넘게 어긋나면 재동기)를 쓴다. 원본은 float dt 를 누적하므로 비트 동일은 불가. 허용: 이펙트 시작 위상 ≤1프레임, 위치 ≤0.01 화면 픽셀, 색 ≤1/255. **구현 뒤 측정한 실제 오차를 반드시 보여라**(채널별 최대·평균). 이 허용 안에서 통과/불통과를 판단한다.

## 설계 지침 (구현 선택은 근거를 문서에 적고 바꿔도 됨)
1. **GPU 가 정점 버퍼를 쓰고 게임 셰이더로 그린다(권장):** 구간(run)마다 MeshRenderer 1개(ld2a 의 구간·재질 빌리기·정렬 키 규칙 재사용: 첫 멤버의 sortingLayerID/order/z 와 재질 인스턴스; 같은 키 안에서 재질 인스턴스 ID 순서가 앞뒤를 정함). 메시의 정점 버퍼를 `mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw` 로 열고 `mesh.GetVertexBuffer(0)` 의 GraphicsBuffer 를 컴퓨트 셰이더가 직접 쓴다 → **게임의 `Sprites/Tile` 셰이더를 그대로 쓰므로 색 곱셈·알파·정렬 재질 ID 문제가 없다**(gate 2 에서 이미 반영: 정점 색 Float32×4, `_RendererColor`=white, `_Flip`=(1,1,1,1)). 정점 레이아웃은 ld2a 의 `LiveDecoRenderer` 와 같게(Position Float32×3, Color Float32×4, TexCoord0 Float32×2). `mesh.bounds` 는 큰 고정 값으로(중심 z 는 첫 멤버 월드 z 와 같게). 멤버 SpriteRenderer 는 `forceRenderingOff`(끄기·언로드·곡 끝·다시 하기·맵 다시 열기에서 복원 보장), 소유 장식의 원본 트윈 평가/Transform 쓰기/색 쓰기는 `BakeLive` 의 소유 규칙으로 막는다(켜기 전에 이미 진행 중인 트윈은 끝값 처리 후 인계).
2. **컴퓨트 셰이더/번들을 어떻게 얻는가를 먼저 조사하라:** 이 프로젝트에 이미 셰이더/컴퓨트를 게임에 넣는 방법이 있다(`fsr/`, `tools/effects`, `docs/shader-research.md`, FrameGen·`sfnative.dll`/D3DCompile 경로). 런타임에 `ComputeShader` 를 소스로 만들 수 없으면(Unity 에디터 없이) ① 기존 번들 파이프라인을 쓰거나 ② `sfnative.dll` 에서 D3D11 컴퓨트를 HLSL 소스 런타임 컴파일(D3DCompile)로 돌려 Unity 메시의 네이티브 버퍼(`GetNativeVertexBufferPtr`)에 쓰는 방법을 고른다. 선택과 근거(왜 가능한지 확인한 코드·로그)를 문서 맨 앞에 적어라. 어느 쪽도 안 되면 즉시 근거와 함께 멈춰라(G0 불가).
3. **구운 표 → GPU 버퍼:** `DecoBakeCore` 의 트랙(장식별 채널 16개, 구간 `Table.Eval` 은 무상태 함수)을 평탄화: 장식 헤더(채널별 구간 시작 인덱스·개수), 구간 배열 `{t0, t1, v0, v1, easeId, overshoot, period}`(시간 정렬). 평가 = 채널별 이진 탐색 → 이징 → 값. 이징은 DOTween `EaseManager.Evaluate` 전부(실제 표에 나오는 종류만 먼저, 나머지는 CPU 대체 경로) HLSL 로 이식하고 **GPU 결과 ↔ CPU `Table.Eval`/`ZeroTween.Eval` 을 수만 샘플 대조**(G0). 위치·회전·크기·색·불투명도에서 장식 월드 정점(피벗·`childLocalOffset`·시차 `scrParallax.SetTrans`·카메라 회전/배율 잠금·타일 상대·스프라이트 코너/UV/PPU/flip/타일링)으로 가는 식은 gate 2 의 CPU 구현(`LiveDecoRenderer.cs` 의 정점 계산)과 `scrDecoration.UpdatePosition` 디컴파일을 근거로 이식하고, 같은 입력에서 CPU 정점과 GPU 정점의 최대 오차를 대조(`vertexcheck` 방식).
4. **소유 범위:** bake 지원 장식 ∩ ld2a 의 소유 판정(기본형 이미지, 비 parent-follow 등). 나머지는 원래 경로로 두고 구간이 끊긴다. 소유 집합 변화는 `Dormancy` 이벤트(깨움/잠듦)로만(프레임당 전체 스캔·쓰기 훅 금지). 시험 중 소유 수가 800 미만이면 즉시 `GPUDECO_G1=OWNED_LOW` 로 종료.
5. **카메라/시계 입력:** 프레임당 1회 읽은 카메라 위치·회전·배율·시차 기준값, 곡 시각 `tb`. 일시정지(`controller.paused`/`Time.timeScale 0`)에서는 `tb` 가 멈춰야 하고 `framecmp pause` 와 호환돼야 한다.

## 단계와 통과 기준
- **G0 (수치 충실도, 게임 안 일시정지 시점 3곳 262·270·276초):** GPU 가 평가한 소유 장식 속성(위치/회전/크기/색)과 같은 순간 실제 `scrDecoration` 논리 필드의 차이(채널별 최대·평균·초과 개수, 화면 픽셀 환산)를 로그 `[gpudeco] G0 t=… ch=pos|rot|scale|color max=… mean=… over_tol=…`. 허용(위 시간 의미) 안이면 G0 통과.
- **G1 (픽셀):** 같은 3곳에서 `framecmp`(끔→켬→끔, 기준 안정)로 다른 픽셀 수·최대 채널 차이·차이 구역. **통과: 세 시점 모두 최대 채널 차이 ≤3/255 이고 다른 픽셀 ≤ 화면의 0.3%(2560×1440 기준 약 11,000개)** — 이 기준은 임시이며 최종은 사용자가 정한다. 차이가 나면 구역별(장식 ID·텍스처·알파·z·블렌드)로 원인을 로그로 추적. `GPUDECO_G1=PASS` 줄을 steps 마지막 요약으로 출력(조건 불충족이면 `PIXEL_FAIL`/`G0_FAIL`/`OWNED_LOW`/`ERROR` + 이유).
- (G2 성능은 이번 라운드 범위 밖. 다만 `[gpudeco] cost` 에 프레임당 CPU(ms: 시계·유니폼·디스패치·기타)와 GPU 타임스탬프가 있으면 남겨라.)

## 시험 steps 생성기 `tools/optimization/gpudeco-steps.py --chart <Arche *FULL.adofai> --output x.json`
`tools/optimization/livedeco2-steps.py`·`framecmp-steps.py` 를 참고. 한 게임에서: `open` → 262·270·276초로 `seek`/일시정지 → G0 로그 → framecmp 끔/켬/끔 → 요약. 요약 줄에 `GPUDECO_G1=…`. 맵을 여는 시간(약 1.5분)과 켜는 준비 시간(소유·표·메시 만들기)도 `[gpudeco] prepare_ms` 로 남겨라(준비는 재생 시작 전 한 번이어야 하며 곡 도중 수 초 멈추는 설계는 쓰지 않는다 — gate 2 의 켜는 데 13초 문제).

## 규칙
- 빌드 3종 오류 0(`-c Release` Dev / `-p:Edition=Player` / `-p:Edition=Player -p:AutoTestBuild=1`), LoadCheck·기존 fixture 통과, 일반 Player 에는 이 코드가 없어야 함(기본 꺼짐, `gpudeco on|off|state` 개발 전용).
- 모든 판단은 로그 수치로. 같은 시험을 코드 변경 없이 되풀이 요청하지 마라. 설계 한계(번들/컴퓨트 불가, 소유 70% 미만 고정, G0 오차가 구조적으로 큼)가 수치로 확인되면 결과 문서에 적고 멈춰라.
- 결과 `docs/handoff/codex-gpudeco-1-result.md` + `-o`(10줄).

## 추가 지시: 독립 Unity 프로젝트(랩)를 먼저 쓴다 (사용자 제안 반영, 2회차부터 적용)
이 PC 에 **Unity 에디터 6000.3.10f1** 이 있다(`unity run <프로젝트> --editor-version 6000.3.10f1 -- -executeMethod …`, 사용 예: `fsr/BuildFsr.cs.txt`, 독립 Editor D3D11 fixture: `tools/gpuprobe/Gpu3Fixture.cs`, `docs/handoff/codex-gpu3-20261018-result.md`). **더 새 에디터로 만든 번들은 게임이 거부**하므로 반드시 .10f1 로 굽는다(게임은 .21f1 이라 더 낮은 에디터 번들은 읽는다). 게임 실행은 맵 열기에 1.5분+시험 5분이라 느리고 도구가 제한되므로 다음 순서로 간다:
1. **게임에서 한 번 내보내기** `gpudeco export <경로>`(AutoTest 전용): 소유 장식의 구운 표 구간 배열, 장식 정적 데이터(스프라이트 코너·UV·피벗·PPU·flip·타일링·시차·배치 종류·잠금), 카메라 값, 그리고 일시정지한 262·270·276초 시점의 **실제 장식 논리 필드와 실제 SpriteRenderer 월드 정점**을 파일로. 이후 대조의 기준(정답)이 된다.
2. **`tools/gpudeco-lab/`(독립 Unity 프로젝트, 게임과 분리, 제품 빌드에 안 들어감):** 위 파일을 읽어 컴퓨트 셰이더(이징 전부·이진 탐색 평가·정점 식)로 같은 시점의 속성/정점을 계산해 정답과 대조(G0 오프라인 전체: 채널별 최대·평균 오차, 정점 오차), 메시 정점 버퍼(`GraphicsBuffer.Target.Raw`)에 쓰고 그리는 경로와 장식 2,000개·구간 1,000여 개 규모의 디스패치·GPU 시간 비용 측정, 번들 빌드(컴퓨트 셰이더 포함)까지. 랩 결과(오차·비용)가 허용 안이고 번들이 만들어지면 3 으로.
3. **게임 연결:** 랩의 번들·로직을 모드에 넣고 G1(픽셀)·G2(FPS)를 게임에서 확인(위 단계 정의 그대로).
랩 시험은 steps 생성기와 별개로 Codex 가 직접 `unity run`(배치 모드)으로 돌려도 된다(게임 실행이 아니므로 허용, 단 오래 걸리면 타임아웃 주의, 한 번에 5분 안쪽으로 쪼갤 것). 랩 결과는 `docs/handoff/codex-gpudeco-lab-result.md` 에 수치로.

## 2회차 추가 사항 (Claude, 1회차 증거 반영)
1. **게임 시험이 `decobake ready` 대기에서 시간 초과**(명령별 제한 180초; 게임을 끔)로 export 가 한 번도 안 만들어졌다. steps 에서 첫 줄 `timeout 420` 으로 올리고, bake 준비 대기를 `wait 60` 여러 번과 `decobake ready` 확인으로 나눠라(60초 이하 wait 여러 번 방식은 CLAUDE.md/지시서에 이미 기록된 관례). 준비에 걸린 시간을 `[gpudeco] prepare_ms` 로 남겨라. 맵 열기 약 90초 + bake 시간을 합쳐 첫 export 까지 5분 안쪽이어야 한다.
2. **Unity 라이선스(exit 198)**: `unity license` 상태는 "Unity Personal (Assigned)·활성"이지만 `unity auth status` 는 "로그인되어 있지 않음"이다. 사용자가 `unity auth login` 으로 로그인할 예정이다(제3자 계정 정보는 우리가 다루지 않는다). 로그인 전에는 랩을 돌리지 말고, 로그인 뒤에도 같은 오류면 `unity` 가 쓴 Editor 로그(`%LOCALAPPDATA%\Unity\Editor\Editor.log` 와 배치 모드 `-logFile`)의 실제 메시지를 결과 문서에 적어라. **로그인을 기다리는 동안 게임 쪽 export 와 오프라인 검증(CPU 쪽 fixture, 데이터 계약, steps 가 실제로 export 를 만드는지)을 먼저 끝내라.**
3. 대안 경로: Editor 가 끝내 안 되면 sfnative.dll 에서 D3D11 컴퓨트를 HLSL 런타임 컴파일(D3DCompile)로 돌리는 경로(지시서의 ②)를 설계 비교로 적어라(구현은 사용자 결정 뒤).

## 중요 정정 (Claude 가 직접 확인): Unity 에디터는 로그인 없이 직접 실행하면 된다
`unity run` 래퍼는 exit 198(라이선스 확인 실패)이 나지만, **에디터를 직접 실행하면 라이선스가 정상 처리되고 exit 0** 이다(로그에 `Successfully resolved entitlement details`, 배치 모드 확인 완료). 에디터 경로: `E:\Unity\Editor\6000.3.10f1\Editor\Unity.exe`. 사용 예:
`"E:/Unity/Editor/6000.3.10f1/Editor/Unity.exe" -batchmode -nographics -quit -projectPath <랩 프로젝트 절대경로> -executeMethod <클래스.메서드> -logFile <로그경로>` (`-quit` 없이 `-executeMethod` 안에서 `EditorApplication.Exit(0)` 로 끝내도 됨). 사용자는 `unity auth login` 을 할 필요가 없고(자격 증명 보관소 오류가 나는 PC 라 로그인은 실패한다) 하지 말 것. **랩 시험·번들 제작을 위 직접 실행으로 지금 진행하라.** Compute 셰이더 컴파일(D3D11), `Mesh` Raw 정점 버퍼 쓰기, 배치 모드 GPU 실행(`-nographics` 는 GPU 가 없으므로 컴퓨트 시험은 `-nographics` 를 빼고 D3D11 로: `-force-d3d11`)을 확인하고, 안 되면 정확한 로그 줄을 결과 문서에 적어라.

## 설계 개선 제안 (Claude, 랩 결과 "1,000구간 제출 2.724ms 목표 0.3ms 초과" 반영) — 다음 회차에서 검토·구현
랩이 보여 준 것: 한 Raw 메시(장식 2,000개, 표 2만 구간)에 평가하는 CPU 제출은 **0.0057ms** 로 거의 공짜인데, 구간마다 메시/버퍼가 따로라서 생기는 **버퍼 나눠 쓰기(1,001 dispatch)가 2.72ms**. 비용은 계산이 아니라 **메시 개수**다. gate 2 A 안의 구간은 "같은 텍스처인 연속 소유 장식" 이라 1,000여 개지만, **정렬상 끊기는 구간(비소유 렌더러가 끼는 경계)은 센서스에서 71~78개뿐**이다(아틀라스 가정).
→ **구간(렌더러)은 정렬 경계(71~78개)로만 나누고, 한 렌더러 안의 텍스처 구간은 서브메시(submesh)로 둔다:** MeshRenderer 1개 + `Mesh` 1개(정점 버퍼 1개) + `subMeshCount = 그 렌더러 안의 텍스처 구간 수` + `sharedMaterials[i]` = i 번째 텍스처 구간의 첫 멤버 재질 인스턴스(게임 셰이더 그대로, 재질 새로 만들기 없음). 서브메시는 인덱스 순서(= 원래 그리기 순서)대로 그려지고 한 렌더러는 한 정렬 키를 가지므로 **비소유 렌더러와의 순서는 경계에서만 바뀐다(그대로 유지)**. 그러면 GPU 에서 평가해 쓰는 정점 버퍼가 렌더러당 1개라 나눠 쓰기가 **약 80 dispatch(≈0.2ms)** 로 준다. 엔진의 서브메시 draw call 수(약 1,200)는 지금 원본의 SpriteRenderer draw 수와 같아 CPU 렌더 스레드 비용은 늘지 않아야 한다(실측으로 확인할 것).
확인할 위험: ① 렌더러 한 개의 정렬 키가 첫 서브메시 재질의 인스턴스 ID 를 쓰는지, ② 한 렌더러에 `sharedMaterials` 수백 개를 줄 때의 엔진 비용과 제한, ③ 서브메시마다 `_MainTex` 가 다른 재질 인스턴스가 그대로 쓰이는지(MaterialPropertyBlock `_RendererColor`/`_Flip` 은 렌더러 단위라 한 번만), ④ 랩에서 "렌더러 80개 × 서브메시 수십 개" 규모로 제출 비용·픽셀 동일(정렬 장난감 fixture)을 먼저 측정. 안 되면 아틀라스 B 안(구간 ≤80개)을 대안으로 근거와 함께 보고.


## 판정 규칙 정정 (Claude): OWNED_LOW 는 "깨어 있는 장식이 충분한데 소유가 적을 때"만
2회차 시험으로 실제 export 가 처음 만들어졌다: 262초 owned=1730/awake=2134(81%, 거절 bake-unsupported 375·repeat-tile 22), 270초 owned=1875/awake=2270(83%). 그런데 **276초는 awake=24, owned=17** 이라 `OWNED_LOW` 로 시험이 중단됐다 — 276초는 장식이 원래 거의 없는 시점이라(예전 census 도 17) 중단 사유가 아니다. 규칙을 바꿔라: `OWNED_LOW` 는 **awake ≥ 800 이고 owned/awake < 70%** 일 때만. awake 가 800 미만인 시점(276초)은 G0/G1 수치는 계속 재되 "장식 수 적음"으로 표시하고 통과 판정은 262·270초 두 시점으로 한다(276초는 보조). 소유 81~83% 의 거절 사유(bake 미지원 17%)는 사유별로 로그하되 목표는 70%↑ 유지.


## 설계 방향 정정 (Claude, 3회차 실제 export G0 실패 분석): 곡 시각이 아니라 "효과(소스)별 실제 시작·진행값"을 GPU 에 준다
3회차 증거: 262초 위치 Y 최대 81.5px / 평균 1.87px, 270초 65.6 / 2.73px, 초과 장식 1,470·1,697개. 곡 시각 전역 보정(−50~+50ms, 201 후보)의 최선도 262초 최대 14.5px / 평균 1.28px 라서 **전역 시계 이동으로는 못 맞춘다.** 원인은 계산식이 아니라 **시간 의미**다: 원본 DecoAnim 은 효과마다 시작 프레임이 다르고 `Pos += td`(float dt 누적)로 진행하므로 효과별 위상이 곡 시각 이상값과 프레임 단위로 어긋나고, 빠른 장식은 1프레임(8ms) 위상차가 수십 px 다. 사용자가 승인한 허용("시작 위상 ≤1프레임")을 px 로 환산하면 이 정도다 — 그래서 G0 를 px 가 아니라 **시간 영역**으로 정의하고(아래), 설계는 시작·진행값을 실제 값으로 앵커링한다.
1. **bake-9 의 앵커링을 GPU 로 가져온다**(`docs/handoff/codex-bake-9-20261021-result.md` "트리거 앵커 시계": 실제 `FastMove.Prefix` 관찰로 소스별 **float 진행값**을 공유, 같은 소스의 여러 채널은 프레임당 한 번 같은 dt 를 더함). GPU 에는 곡 시각 `t` 대신 **소스별 진행값 배열 `progress[sourceId]`**(float, 프레임당 CPU 가 활성 소스 수백~수천 개만 갱신해 한 번의 `SetData` 로 올림, 비용 µs 단위)를 준다. 채널 평가: 장식·채널의 구간 표에서 곡 시각으로 "지금 활성인 구간"을 이진 탐색해 소스를 정하고, 그 소스의 `progress` 로 이징 입력(`progress/duration`)을 계산. 아직 시작 관찰이 없는 소스는 이상 시각으로 대체(대체 사용 횟수를 로그).
2. **G0 판정을 시간 영역으로:** 채널별 오차를 px 와 함께 **위상 오차(ms)** 로 환산해 보고(`phase_ms = 값 오차 / 그 순간 속도`), 통과 기준: ① 앵커링 후 시작·진행 위상 오차 ≤ 1프레임(활성 소스의 99% 이상, 프레임 시간은 그 곡 구간 평균), ② 정지 상태(속도≈0)와 종료 구간의 값 오차 ≤ 0.01px·색 ≤1/255, ③ 구간 안 속도가 큰 장식은 px 오차가 아니라 위상 오차로 판단. 앵커링 전·후 비교표(최대/평균/초과 개수 px·ms)를 결과 문서에.
3. G1(픽셀)은 일시정지 시점에서 같은 진행값을 쓰므로 앵커링 뒤에는 위치가 거의 일치해야 한다(최대 채널 차이 ≤3/255, 다른 픽셀 ≤0.3% 임시 기준 유지). 앵커링으로도 위치 오차가 큰 장식 집단이 남으면 소유에서 제외하는 규칙(사유 로그)을 두고 소유율 70%↑를 지켜라.
4. 랩에서 먼저: 게임에서 내보낸 export 에 소스별 **실제 진행값**과 시작 프레임을 포함하도록 export 형식을 확장(필요하면 `gpudeco export` 가 해당 시점의 활성 `DecoAnim` 레코드의 진행값을 함께 기록)하고, 랩에서 `progress` 기반 평가 대 실제 값을 대조한 뒤 게임에 연결.
