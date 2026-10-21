
## 회차 1
- Codex 요약: 변형 B 구현과 오프라인 검증을 완료했습니다.

- 빌드 3종 오류 0, LoadCheck·fixture 통과.
- 자동 시험 144단계 JSON 생성.
- 픽셀·FPS는 미측정이므로 GATE2 통과 판정은 보류했습니다.
- 게임 실행·설치·커밋·푸시는 하지 않았습니다.

[결과 문서](E:/StutterFixWork/wt-ld2b/docs/handoff/codex-gate2-B-result.md) · [10줄 요약](E:/StutterFixWork/wt-ld2b/docs/handoff/codex-gate2-B-o.txt) · [시험 JSON](E:/StutterFixWork/wt-ld2b/docs/handoff/gate2-B-steps.json)


- 시험 핵심 줄:
  - [StutterFix] [livedeco2] GATE2=ERROR reason=BadImageFormatException:Method has no body final_on=False

## 회차 2 — 추상 Harmony 대상 제거 (2026-10-21)
- 회차 1의 최초 on 실패를 분석했다. 기존 구조 setter 수집은 scrDecoration.SetVisible/SetDepth의 추상 선언 2개(RVA=0)를 포함했다. 디컴파일·ilscan·실제 DLL PE/reflection으로 확인했다.
- 새 LiveDecoPatchPlan은 실제 본문 18개만 포함하고, 두 추상 선언의 scrVisualDecoration 재정의가 같은 virtual slot인지 검사한다. 구조 변경 시 소유 해제와 SetSprite 두 오버로드는 유지했다.
- 전체 계획/외부 훅을 아틀라스 할당·패치 적용 전에 검증한다. 설치 실패는 대상 이름을 포함한다. 식별 로그: patch_plan version=2 targets=18 abstract_declarations_skipped=2.
- 생산 계획 공유 실제 DLL 검사 38 checks 통과. 빌드 Dev/Player/Auto 오류 0/0/0; LoadCheck 274/229/274곳 틀림 0; LiveDeco/아틀라스/edition/MatShare/FrameCompare/steps 통과.
- 회차 1 전달 프로파일은 samples=960572, set_pct=70.130186. 표본 시간 내 비율이며 DecoAnim 전체/native 쓰기 비율은 미입증.
- 일반 Player 제외·기본 꺼짐·기존 144 steps·엄격 PASS 조건 유지. 변경된 DLL로 다음 외부 시험을 인계하며, 같은 코드의 실패 시험을 반복하지 않았다.
- 현재 GATE2=PASS 미확인. 실제 Harmony 설치 성공·소유/런 수·픽셀·복원·FPS는 미측정. 설계 한계가 확정된 상태가 아니라 재현 가능한 대상 선정 결함을 수정한 상태다.
- 게임 실행·설치·커밋·푸시·config 변경 없음. 결과: codex-gate2-B-result.md / codex-gate2-B-o.txt. 근거: gate2-B-run2-* 로그와 artifacts.txt.

## 회차 1
- Codex 요약: 본문 없는 추상 메서드 2개를 Harmony로 패치하던 결함을 수정했습니다.

- 빌드 3종 오류 0, LoadCheck·회귀 검사 통과.
- 게임 실행·설치·커밋은 하지 않았습니다.
- 실제 픽셀·FPS와 `GATE2=PASS`는 미확인입니다.

[결과 문서](/E:/StutterFixWork/wt-ld2b/docs/handoff/codex-gate2-B-result.md) · [10줄 요약](/E:/StutterFixWork/wt-ld2b/docs/handoff/codex-gate2-B-o.txt)


- 시험 핵심 줄:
  - [StutterFix] [livedeco2] GATE2=ERROR reason=InvalidOperationException:zero-owned-see-exclusions final_on=False

## 회차 3 — 진단 정지 소유 판정, 중단 작업 이어하기 (2026-10-21)
- 최신 zero-owned 증거와 기존 수정 4파일을 보존해 이어갔다. 추상 대상 실패 수정이나 같은 DLL의 게임 시험은 반복하지 않았다.
- 디컴파일 ADOBase.isEditingLevel은 편집기에서 controller.paused를 반환한다. ilscan도 getter 호출을 확인했다. framecmp pause가 이 값을 true로 만들어 기존 전역 편집 독자 제외가 전체 후보를 거절하는 결함을 확인했다.
- FrameCompare 정지 출처/동일 controller/정지 유지 조건을 검증해 진단 재생 정지만 구분한다. 실제 선택·테두리·히트박스·활성 기즈모 currentDec 독자는 계속 제외한다. 기즈모 LateUpdate의 캐시 Transform 읽기는 디컴파일/IL로 확인했다.
- 첫 Tick에도 cost/vertexcheck/remaining_reason을 남긴다. 새 로그 admission version=3, 기존 patch_plan version=2 유지. 이전 로그에는 제외 분포가 없어 전체 소유 0의 유일 원인이나 수정 후 소유 수는 미확인이다.
- 빌드 Dev/Player/Auto 오류 0/0/0(경고 38/4/38), LoadCheck 275/229/275곳 틀림 0; 패치 계획 38, LiveDeco 22984, 아틀라스 1052924, edition 19, MatShare 25401, FrameCompare control 49 및 edition/steps 통과.
- 최신 전달 ProfSet 비율은 962183표본/70.352910%; 전체 DecoAnim/native 쓰기 몫은 미입증. 일반 Player DLL 해시는 run2와 동일하다.
- GATE2=PASS·실제 소유/정점·픽셀/복원·GC/VRAM·FPS는 미확인. 설계 한계 확정이 아니라 정지/분류 결함을 수정한 상태다. 변경된 DLL과 기존 144 steps를 인계하며 다음 관측은 새 admission 및 최초 제외 분포다.
- 게임 실행·설치·커밋·푸시·config 변경 없음. 결과 codex-gate2-B-result.md / codex-gate2-B-o.txt; 증거 gate2-B-run3-* 로그와 artifacts.txt.

## 회차 1
- Codex 요약: 진단용 `pause`가 편집 상태로 판정되어 소유 후보 전체를 제외하는 경로를 수정했습니다. 최초 실패의 제외 사유 로그도 보강했습니다.

- 3종 빌드 오류 0, LoadCheck·회귀 검사 통과.
- 게임 실행·설치·커밋은 하지 않았습니다.
- 실제 픽셀·FPS와 `GATE2=PASS`는 미확인입니다.

[결과 문서](/E:/StutterFixWork/wt-ld2b/docs/handoff/codex-gate2-B-result.md) · [10줄 요약](/E:/StutterFixWork/wt-ld2b/docs/handoff/codex-gate2-B-o.txt)


- 시험 핵심 줄:
  - [StutterFix] [livedeco] cost frame=22930 vertex_ms=8.7111 upload_ms=0.8408 runs_ms=28.8660 runs=0 owned=0 live=2074 new_runs=0 reused_runs=0 heap_delta=0 stable_alloc_bytes=0 transition_alloc_bytes=0 
  - [StutterFix] [livedeco2] GATE2=ERROR reason=InvalidOperationException:zero-owned-see-exclusions final_on=False
