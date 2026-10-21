
## 회차 1
- Codex 요약: 변형 A 구현과 오프라인 검증을 완료했습니다.

- 3종 빌드 오류 0, LoadCheck·기존 fixture 통과.
- 129단계 자동 검증 생성기 추가.
- 게임 실행·설치·커밋·푸시는 하지 않았습니다.
- 실픽셀·FPS·GC는 미측정이므로 Gate 2 PASS는 아직 아닙니다.

[결과 문서](/E:/StutterFixWork/wt-ld2a/docs/handoff/codex-gate2-A-result.md) · [10줄 요약](/E:/StutterFixWork/wt-ld2a/docs/handoff/codex-gate2-A-o.txt)
- 시험 핵심 줄:
  - [StutterFix] [livedeco] transition frame=24217 owned=0 runs=0 created=0 reused=0 destroyed=0 reconcile_ms=0.291900
  - [StutterFix] [livedeco] cost per_frame=1 samples=1 owned=0 runs=0 vertex_ms=0.841500 upload_ms=0.776800 reconcile_ms=0.291900 scan_ms=42.526200 upload_calls=0.000 allocated_bytes=0 steady_allocated_by
  - [StutterFix] [livedeco2] GATE2=ERROR reason=autotest_aborted:오류로 그만둠 variant=A on=False

## 회차 2 — 중단 수정본 이어서 완료 (2026-10-21)
- run1은 소유 0 / 구간 0, child-parent 2,089개·repeat-tile 38개 제외, PROFILE enabled=False/samples=0에서 오류 종료했다. 픽셀·FPS 결과는 없다.
- 기존 child-parent의 renderer=child 및 직계 부모 가정을 실제 평면 계층 합성으로 대체했다. 최대 깊이 16, 변경/비평면/끊긴 계층은 제외·복원한다. renderer_version=2, nested_admitted 로그로 새 경로를 확인한다. 기존 집계로 세 하위 조건별 실패 수를 단정하지 않는다.
- profilebegin에서 DEV/AutoTest Prof를 일시 활성화하고 profileend/reset/abort/result/SceneReset/Unload에서 원래 값을 복원한다. 표본 0개는 최종 ERROR. Prof가 PairXY/GroupStep을 억제한다는 측정 한계를 로그에 명시했다.
- 최종 DEV/Player/PlayerAuto 빌드 오류 0/0/0, LoadCheck 틀림 0/0/0, 계층 fixture 2,729건, 구간 106,460건, 판정 18건, edition 28건, 패치 대상 17개 통과. 기존 MatShare/FrameCompare·25/129단계 생성기 fixture도 통과.
- 게임 실행·설치·커밋·푸시·config 수정 없음. 수정 후 실픽셀·FPS·GC·소유·수명은 미측정이며 GATE2=PASS 미달성. 동일 게임 시험을 반복 실행하지 않았다.
- 결과: codex-gate2-A-result.md 최신 2회차 절 / codex-gate2-A-o.txt(10줄). 검증 근거: gate2-A-run2-* 로그. 자동 루프는 변경된 코드로 기존 생성기의 프로파일→3시점 픽셀→ABBA를 검증한다.

## 회차 1
- Codex 요약: 중단된 수정본을 이어서 완료했습니다.

- 계층 판정과 프로파일 표본 0개 원인을 수정했습니다.
- 3종 빌드 오류 0, LoadCheck 틀림 0, 계층 검사 2,729건과 기존 fixture 통과.
- 결과 문서·장부·10줄 요약을 갱신했습니다.
- 게임 실행·설치·커밋은 하지 않았습니다.

실픽셀·FPS는 미측정이므로 **Gate 2 PASS는 아직 아닙니다.**

[결과 문서](E:/StutterFixWork/wt-ld2a/docs/handoff/codex-gate2-A-result.md)


- 시험 핵심 줄:
  - [StutterFix] [livedeco] transition frame=24057 owned=0 runs=0 created=0 reused=0 destroyed=0 reconcile_ms=0.311200
  - [StutterFix] [livedeco] cost per_frame=1 samples=1 owned=0 runs=0 vertex_ms=0.680900 upload_ms=0.725900 reconcile_ms=0.311200 scan_ms=478.261100 upload_calls=0.000 allocated_bytes=0 steady_allocated_b
  - [StutterFix] [livedeco2] GATE2=ERROR reason=autotest_aborted:오류로 그만둠 variant=A on=False
