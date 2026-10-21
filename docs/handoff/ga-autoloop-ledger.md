
## codex-al-ga-4 수정 — 2026-10-21 (검증 로그 run5)
- 시작 HEAD 3b8bdd6, 작업 트리 깨끗함. 기존 계층·ToggleVisible·바운드 검사 유지.
- run3 전달 실측: 소유/구간 1,472/1,100, ab_out=2,278 / maxdiff=5 / aa2_out=769 / stable=False, GATE2=ERROR unstable_baseline. Prof samples=972,702 / set_pct=69.249446% / valid=True(기존 Step 표본 한계 유지). FPS 미도달.
- 기존 night-results/al-ga-3.log에서 소유 전 잡음 818(262초)/1,194(270초) 추가 확인. 818개 잡음과 A/A2 차이의 상위 영역이 중첩된다. Renderer 상태는 같았으므로 A/B 차이 전부를 메시 오류로 단정하지 않는다. 출처 해시·발췌 보존.
- 디컴파일 PlanetSprite.LateUpdate는 Time.unscaledTime * 12 % 11을 재질 _Frame에 쓴다. 기존 정지는 PlanetRenderer.LateUpdate만 막고 상태 검사도 이 uniform을 읽지 않았다. 설치 게임 IL·동일 DLL 해시로 교차확인. 게임 메서드 실행 없음.
- 두 타입의 instance void LateUpdate/0을 유일 선택해 같은 !Paused Prefix 적용. Resume/오류/종료의 기존 Harmony 해제 경로 유지. 마지막 값을 보존하고 재질을 강제로 변경하지 않는다.
- FrameCompare 상태에 sharedMaterial의 planetFrame, GatePixel에 잡음/복원 필드 차이 로그 추가. 생성기는 132단계로 각 noise 뒤 assertzero N1 N2를 수행해 불안정 기준을 ERROR로 중단. 필터 켬·픽셀 0·중앙 20%·FPS +15% 유지.
- 3종 빌드 오류 0/0/0, LoadCheck 틀림 0/0/0. FrameCompare 11/15/15/17/19/2, 계층 2,729·바운드 482·스크립트 15·구간 106,460·판정 18·edition 31·패치 17·MatShare 25,401 및 24/25/132 생성기 검사 통과.
- 게임 실행·설치·커밋·푸시·config 수정 없음. 동일 코드 게임 시험 반복 없음. 잡음 전체 기여량·수정 후 소유/픽셀/FPS/GC/수명은 미측정. 재생 중 scan 비용을 고쳤다는 주장은 하지 않는다. GATE2=PASS 미달성.
- 결과 문서 최신 run5 절·10줄 요약·gate2-A-run5-* 검증 로그 갱신. 자동 루프는 변경된 132단계로 잡음 0/상태 안정성부터 확인한다.

## codex-al-ga-3 수정 — 2026-10-21 (검증 로그 run4)
- 시작 HEAD 4e59c35, 작업 트리 깨끗함. 계층·프로파일·ToggleVisible 수정 유지.
- run2 전달 실측: 소유/구간 0/0, 정점 검사 2,087개 전부 제외, max_err=0.100000374. Prof samples=987,093 / set_pct=69.311975% / valid=True. 픽셀/FPS 미도달.
- 정점 검사와 평면 정점 AABB 대 컬링 AABB 차이를 한 max_err에 합친 코드 오류를 찾았다. 두께 0.2의 컬링 상자는 동일 정점이어도 옛 비교에서 0.1 오차를 냄을 합성 재현했다. 실게임 localBounds 두께·구성별 오차가 없어 전체 실패 원인의 확정은 보류.
- 정점 직접 비교를 유지하고 실제 localBounds 8모서리를 같은 논리 계층/피벗 변환으로 계산해 bounds와 비교한다. 두 검사 모두 1e-4 유지, bounds-error 분리. 추가 계산은 입장 검사에만 수행.
- renderer_version=4, vertex_max_err/bounds_max_err/quad_bounds_max_err, 첫 최대 3개 vertexdetail의 local_bounds_z 로그 추가. 고정 0.1 보정·성공 기준 완화 없음.
- 지정 디컴파일·설치 게임 장식/시차 IL·CoreModule bounds/localBounds API 교차확인. 게임 DLL 해시 기존과 동일. 게임 메서드 실행 없음.
- 3종 빌드 오류 0/0/0, LoadCheck 틀림 0/0/0, 생산 계층 2,729·새 바운드 482·스크립트 15건 통과. 구간 106,460·판정 18·edition 31·패치 대상 17개 및 기존 MatShare/FrameCompare·25/129단계 생성기 통과.
- 게임 실행·설치·커밋·푸시·config 수정 없음. 소유/실픽셀/FPS/GC/수명은 수정 후 미측정. GATE2=PASS 미달성. 동일 코드 게임 시험 반복 없음.
- 결과 문서 최신 run4 절·10줄 요약 갱신. 자동 루프는 수정 코드의 분리 오차·실제 bounds 두께·소유/구간 후 기존 픽셀/ABBA를 확인한다.

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

## codex-al-ga-2 최신 전달 후 수정 — 2026-10-21 (검증 로그 run3)
- 시작 HEAD 863096b, 작업 트리는 깨끗했다. 이전 계층/프로파일 수정을 유지하고 새 원인을 수정했다.
- 최신 전달 시험은 foreign-child-script 2,088개·repeat-tile 38개·keywords 1개 제외, 소유/구간 0/0에서 오류 종료. 픽셀/FPS는 단계 미도달.
- Prof 표본은 957,795개, set_pct=68.852079%, valid=True. Step 표본 범위이며 PairXY/GroupStep 억제·StepPair/OnComplete/후속 엔진 비용 제외 한계는 그대로다.
- 현재 설치 sharedassets4.assets 직접 감사: 이미지 프리팹 490의 ToggleVisible 471은 자기 scrVisualDecoration 490에 연결되어 enabled만 설정한다. 디컴파일·IL·관리자 직접 LogicUpdate 호출·기반 타입의 콜백 부재를 교차확인했다. 프리팹 1개의 결과로 2,088개 전체 연결을 단정하지 않는다.
- 정확한 게임 ToggleVisible 타입 + 자기 sprite Transform + 자기 장식 참조만 허용. 외부/동명/상속/잘못된 연결은 계속 거부하고 캐시된 스크립트의 활성/대상 변경은 소유 중 재검사한다. 콜백/대상 enabled를 강제 변경하지 않는다.
- renderer_version=3 visibility_policy=bound-self, visibility_admitted 및 구체적 제외 타입/연결 사유 로그 추가. 외부 Harmony 패치 거부에 ToggleVisible 포함. 기본 꺼짐·Player 제외·오차/픽셀/FPS 기준 유지.
- 3종 빌드 오류 0/0/0, LoadCheck 틀림 0/0/0, 계층 2,729건·스크립트 15건·구간 106,460건·판정 18건·edition 31건·게임 패치 대상 17개 통과. 기존 MatShare/FrameCompare·25/129단계 생성기 통과.
- 게임 실행·설치·커밋·푸시·config 수정 없음. 동일 게임 시험 재실행 없음. 새 소유/구간/정점/픽셀/FPS/GC/수명은 미측정, GATE2=PASS 미달성.
- 결과: codex-gate2-A-result.md 최신 절 / codex-gate2-A-o.txt(10줄). 새 도구 livedeco-prefab-audit.py는 표준 라이브러리만 사용하고 에셋을 읽기만 한다. 수정한 코드로 기존 129단계 프로파일→픽셀 3시점→ABBA를 확인할 준비를 마쳤다.

## 회차 2
- Codex 요약: `ToggleVisible` 오분류를 수정하고 오프라인 검증을 완료했습니다.

- 3종 빌드·LoadCheck 오류 0, 기존·신규 검사 통과.
- 결과 문서·장부·10줄 요약 갱신.
- 게임 실행·설치·커밋은 하지 않았습니다.

수정 후 실픽셀·FPS는 미측정이므로 **Gate 2 PASS는 아직 아닙니다.**

[결과 문서](E:/StutterFixWork/wt-ld2a/docs/handoff/codex-gate2-A-result.md)


- 시험 핵심 줄:
  - [StutterFix] [livedeco] transition frame=23871 owned=0 runs=0 created=0 reused=0 destroyed=0 reconcile_ms=0.331300
  - [StutterFix] [livedeco] cost per_frame=1 samples=1 owned=0 runs=0 vertex_ms=0.791000 upload_ms=0.766600 reconcile_ms=0.331300 scan_ms=842.840100 upload_calls=0.000 allocated_bytes=0 steady_allocated_b
  - [StutterFix] [livedeco2] GATE2=ERROR reason=autotest_aborted:오류로 그만둠 variant=A on=False

## 회차 3
- Codex 요약: 인계대로 정점·컬링 바운드 비교 오류를 수정했습니다.

- 3종 빌드·LoadCheck 오류 0.
- 새 바운드 검사 482건과 기존 fixture 통과.
- 결과 문서·장부·10줄 요약 갱신.
- 게임 실행·설치·커밋 없음.

실게임 원인 확정과 수정 후 픽셀·FPS는 미측정이므로 **Gate 2 PASS는 아직 아닙니다.**

[결과 문서](/E:/StutterFixWork/wt-ld2a/docs/handoff/codex-gate2-A-result.md)


- 시험 핵심 줄:
  - [StutterFix] [livedeco] transition frame=23975 owned=1472 runs=1100 created=1100 reused=0 destroyed=0 reconcile_ms=36.642200
  - [StutterFix] [livedeco] cost per_frame=1 samples=1 owned=1472 runs=1100 vertex_ms=2.619300 upload_ms=2.535200 reconcile_ms=36.642200 scan_ms=841.307000 upload_calls=1100.000 allocated_bytes=0 steady_a
  - [StutterFix] [livedeco] cost per_frame=1 samples=9 owned=1472 runs=1100 vertex_ms=2.567067 upload_ms=1.363356 reconcile_ms=0.000000 scan_ms=34.416711 upload_calls=1100.000 allocated_bytes=0 steady_all
  - [StutterFix] [livedeco2] GATE2=ERROR reason=unstable_baseline variant=A on=False

## 회차 4
- Codex 요약: `PlanetSprite` 재질 애니메이션의 일시정지 누락을 수정했습니다.

- 3종 빌드·LoadCheck 오류 0, 회귀 검사 통과.
- 결과 문서·장부·10줄 요약 갱신.
- 게임 실행·설치·커밋 없음.

수정 후 실픽셀·FPS는 미측정이므로 **Gate 2 PASS는 아직 아닙니다.**

[결과 문서](E:/StutterFixWork/wt-ld2a/docs/handoff/codex-gate2-A-result.md)


- 시험 핵심 줄:
  - [StutterFix] [livedeco] transition frame=23165 owned=1478 runs=1114 created=1114 reused=0 destroyed=0 reconcile_ms=21.626100
  - [StutterFix] [livedeco] cost per_frame=1 samples=1 owned=1478 runs=1114 vertex_ms=2.775600 upload_ms=2.702700 reconcile_ms=21.626100 scan_ms=290.741500 upload_calls=1114.000 allocated_bytes=0 steady_a
  - [StutterFix] [livedeco] cost per_frame=1 samples=8 owned=1478 runs=1114 vertex_ms=2.753813 upload_ms=1.437425 reconcile_ms=0.000000 scan_ms=35.655163 upload_calls=1114.000 allocated_bytes=0 steady_all
  - [StutterFix] [livedeco] transition frame=24488 owned=1577 runs=1230 created=1230 reused=0 destroyed=0 reconcile_ms=88.958400
  - [StutterFix] [livedeco] cost per_frame=1 samples=1 owned=1577 runs=1230 vertex_ms=2.679800 upload_ms=2.200600 reconcile_ms=88.958400 scan_ms=351.548500 upload_calls=1230.000 allocated_bytes=0 steady_a
  - [StutterFix] [livedeco] cost per_frame=1 samples=5 owned=1577 runs=1230 vertex_ms=2.727280 upload_ms=1.510000 reconcile_ms=0.000000 scan_ms=34.950140 upload_calls=1230.000 allocated_bytes=0 steady_all
  - [StutterFix] [livedeco2] GATE2=ERROR reason=autotest_aborted:오류로 그만둠 variant=A on=False
