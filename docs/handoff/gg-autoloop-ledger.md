
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
