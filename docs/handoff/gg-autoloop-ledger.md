
## codex-al-gg-2 후속 — 2026-10-21 (최신)

- 직전 증거의 300001.072ms / `새 맵 스냅샷 대기`는 단순 굽기 지연이 아니었다. 편집 open은 PrepVfx를 호출하지 않는다. 원본 `scnEditor.OpenLevelCo`·`scrController.WaitForStartCo` 디컴파일과 설치 DLL IL 호출 목록으로 교차확인했다. 기존 `open → ready → seek` 순서는 첫 재생 준비를 일으키기 전에 기다렸다.
- 수정: `select 0 → play → gpudeco scheduled(실제 collector/worker/ready 관찰) → pause → wait 60 → state → wait 60 → ready → resume → stop`, 그 다음 시점 3곳 seek/export. timeout 420·첫 export 300초 예산 유지. 새 steps는 `tools/gpudeco-lab/evidence/round3-export-steps.json`. 생성기/계약 검사는 통과했고 실제 게임 export 성공은 미검증이다.
- **되풀이하지 않을 접근:** 편집 open 뒤 재생 준비 없이 ready 대기, 또는 1,000개 독립 메시/1,001 dispatch를 성능 성공으로 간주하는 경로. 80개 Raw 메시·메시당 15서브메시를 새로 구현해 검증했다.
- Unity 6000.3.10f1 직접 D3D11 실행 성공. 최초 샌드박스 IPC 실패(exit1)·기존 라이선스 클라이언트 entitlement 실패(exit198)를 보존했다. 해당 클라이언트 종료 후 새 직접 실행은 정상 동작했으며 로그인·설치·라이선스 변경은 없었다.
- 합성 2,000장식/20,000표 구간/80메시/1,200서브메시/81 dispatch. 제출 평균 **0.1522106ms**, 5묶음 평균 최소/최대 **0.147147/0.157122ms**. 8,000정점 복사 불일치 **0**. GPU timestamp 미제공.
- 대규모 128×128 분리/통합 그리기 **0픽셀/최대0**. SpriteRenderer 원본 대조 및 같은 키·중간 비소유 경계·생성 순서 6종 모두 **0픽셀/최대0**. 재질 역전 음성 대조는 각 **2,304픽셀/최대93~110**. 게임 Sprites/Tile와 실제 경계는 미검증이다.
- 첫 랩 픽셀 실패 1,991픽셀/최대117은 비용 측정에서 t가 270.0499로 진행된 GPU와 t=270 정답을 혼합한 시험 결함이었다. 시각과 복사 버퍼를 정답 시각으로 복원한 후 통과했다. 실패/중간/최종 로그를 보존했다.
- Camera.Render wall(에디터128×128, 각100회)은 분리1,200렌더러 **4.237392ms** / 80렌더러·1,200서브메시 **3.068427ms**. 컬링·제출·드라이버 대기를 포함할 수 있으며 순수 엔진 CPU/GPU/게임 FPS 수치가 아니다. 수백 재질/렌더러·게임 수명/복구는 미검증이다.
- 번들 **35,399B** 재생성·재로드·101,475이징 대조 통과. 3종 Release 오류0(기존경고38/4/38), LoadCheck 불일치0·일반 Player 제외, bake/live·데이터·DecoSim·서명·ld2a·framecmp·steps·HLSL fixture 통과. 최신 근거는 `tools/gpudeco-lab/evidence/round3-*` 및 `submeshes.txt`다.
- 게임 실행·설치·커밋·푸시·config 수정 0회. 실제 export **0개**, 실제 준비 시간/소유율/채널 오차/시작 위상/G0/G1/G2 미측정. 실제 export 검증 전 게임 소유/렌더러 연결은 계속 거부한다. 결과 문서·랩 결과·10줄 요약을 최신화했다.
- GPUDECO_G1=ERROR stage=G0-real-export reason=real_export_missing

---

## 회차 1
- Codex 요약: export·독립 랩·steps를 추가했고, 3종 빌드·LoadCheck·오프라인 fixture가 통과했습니다.

**Unity 라이선스 오류(exit 198)로 GPU 검증이 막혀 G0/G1은 미완료**입니다. 인계 기준에 따라 게임 연결 전에 중단했습니다.

게임 실행·설치·커밋·푸시·설정 변경은 하지 않았습니다.

[결과 문서](E:/StutterFixWork/wt-gpu/docs/handoff/codex-gpudeco-1-result.md) · [10줄 요약](E:/StutterFixWork/wt-gpu/docs/handoff/codex-gpudeco-1-result-o.txt)


- 시험 핵심 줄:

## 회차 2

- 첫 회차와 같은 게임 시험을 반복하지 않았다. 게임 실행·설치·커밋·푸시·설정 변경 0회.
- timeout 420 / wait 60 두 번 / 실제 state / ready, open부터 첫 export까지 300초 예산·전체 prepare_ms 기록을 반영했다. 실게임 5분 달성은 미측정이다.
- 코드상 추가 차단: framecmp pause의 DOTween.timeScale=0이 bake의 frozenScale 변경 거부를 유발한다. 동일 controller의 온전한 framecmp 소유 정지만 인정하도록 수정했다. 실게임 export 성공은 미검증이다.
- Unity 6000.3.10f1 직접 호출(-batchmode -quit -force-d3d11, 파이프로 종료 대기)은 실제 실행 성공. 로그인/설치 없이 RTX 4060 Ti에서 검증했다. -nographics는 Null Device이므로 GPU 시험용이 아니다.
- GPU 최초 이징 101,475개에서 최대 0.00165259838104 / 초과 526. Circ 경계·Elastic asin 위상 근사를 수정한 뒤 최대 1.97160989046e-6 / 평균 2.09635552387e-8 / 초과 0.
- 합성 정점 8,000개 최대 0.000477268022223px / 평균 0.0000223719397063px / 초과 0. 실제 Arche G0 결과가 아니다.
- 1,000개 독립 Raw 메시 복사 차이 0이어도 UInt16 조합은 빈 출력이었다. CPU 사본 폐기·바운드·동기 컴파일만으로 해결되지 않았고 UInt32로 맞춘 뒤 1,573픽셀을 그렸다. 단일/분할 128×128 RGBA 비교 차이 0. 이를 일반적인 Unity UInt16 결함으로 단정하지 않는다.
- 번들 최초 재로드 실패는 generic 버전 오류와 함께 AssetBundle module disabled 로그가 있었다. 독립 프로젝트의 기존 내장 모듈 참조 수정 후 **35,399B 생성·재로드·수치 대조 통과**, 게임 적재는 미검증.
- **그대로 되풀이하지 않을 경로:** 통합 평가 1회 + 1,000개 CopyRun을 미리 기록한 CommandBuffer. 합성 2,000장식 / 20,000표 구간 / 1,000메시에서 제출 **2.723905ms**, dispatch **1,001회**. 0.3ms 목표의 약 9.08배라 이 경로의 성능 성공으로 게임 연결하지 않는다. GPU timestamp/게임 FPS/매 프레임 renderer 엔진 비용은 미측정이다. 네이티브 대안은 비교만 했고 구현하지 않았다.
- 3종 빌드 오류 0·LoadCheck·bake/live·데이터·DecoSim·서명·ld2a·framecmp·steps fixture 통과. 실제 export 0파일·실제 소유율/prepare_ms/시작 위상/게임 G0/G1은 미검증.
- 결과: [본 결과](codex-gpudeco-1-result.md), [랩 수치](codex-gpudeco-lab-result.md), [10줄 요약](codex-gpudeco-1-result-o.txt). fixture 종료 코드와 수치 로그는 tools/gpudeco-lab/evidence/에 있다.
- GPUDECO_G1=ERROR stage=G0-real-export reason=real_export_missing_and_1000_run_submit_over_budget


## 회차 1
- Codex 요약: 인계대로 수정·검증했습니다. 게임 실행·설치·커밋은 하지 않았습니다.

- export 대기와 일시정지 시 bake 폐기 문제 수정
- GPU 수치·번들 재로드, 3종 빌드·LoadCheck·기존 fixture 통과
- 1,000구간 제출 **2.724ms**로 목표 **0.3ms 초과**
- 실제 export가 없어 게임 G0·G1은 미검증이며, 게임 연결은 중단했습니다.

[결과 문서](/E:/StutterFixWork/wt-gpu/docs/handoff/codex-gpudeco-1-result.md) · [10줄 요약](/E:/StutterFixWork/wt-gpu/docs/handoff/codex-gpudeco-1-result-o.txt)


- 시험 핵심 줄:
  - [StutterFix] [gpudeco] prepare_begin budget_ms=300000 includes=open,bake,seek,export
  - [StutterFix] [gpudeco] GPUDECO_G1=ERROR stage=prepare prepare_ms=300001.072000 reason=first_export_budget_300s status=새 맵 스냅샷 대기

## 회차 2
- Codex 요약: 준비 대기 순서를 수정하고 서브메시 랩을 검증했습니다.

- CPU 제출 평균 **0.1522ms**, 합성 픽셀 차이 **0**
- 3종 빌드·LoadCheck·기존 fixture·번들 재로드 통과
- 실제 export가 없어 **게임 G0/G1은 미검증**

게임 실행·설치·커밋은 하지 않았습니다.

[결과 문서](E:/StutterFixWork/wt-gpu/docs/handoff/codex-gpudeco-1-result.md) · [10줄 요약](E:/StutterFixWork/wt-gpu/docs/handoff/codex-gpudeco-1-result-o.txt)


- 시험 핵심 줄:
  - [StutterFix] [gpudeco] prepare_begin budget_ms=300000 includes=open,bake,seek,export
  - [StutterFix] [gpudeco] prepare_ms=182998.126800 phase=first_export includes=open,bake,seek,export bake_wall_ms=514.999100
  - [StutterFix] [gpudeco] prepare_ms=4623.995800 phase=export owned=1730 awake=2134 raw=262.01982366340235 tb=262.00703288420073 path=E:\StutterFixWork\wt-gpu\tools\gpudeco-lab\exports\262.gpudeco
  - [StutterFix] [gpudeco] prepare_ms=4590.392900 phase=export owned=1875 awake=2270 raw=270.01799260871485 tb=270.0076849285594 path=E:\StutterFixWork\wt-gpu\tools\gpudeco-lab\exports\270.gpudeco
  - [StutterFix] [gpudeco] prepare_ms=4386.468400 phase=export owned=17 awake=24 raw=276.00734197394922 tb=276.0021744821363 path=E:\StutterFixWork\wt-gpu\tools\gpudeco-lab\exports\276.gpudeco
