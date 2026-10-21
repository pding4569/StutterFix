
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

## 회차 4 — 실제 renderer 계층 합성 (2026-10-21)
- 시작 git status/diff는 깨끗했다. 최신 전달 live=2074 전부 child-hierarchy, owned/runs=0, vertexcheck count=0을 분석했다. 기존 추상 대상/진단 정지 수정은 보존했다.
- 기존 이유는 누락/renderer!=child/child.parent!=pivot을 합쳐 기록하므로 실제 실패 조건은 미확인이다. 디컴파일·ilscan에서 Image 프리팹 생성, child localPosition offset과 pivot 변환 쓰기를 대조했다. 프리팹 직렬화 계층을 확인했다는 주장은 하지 않는다.
- renderer→pivot 실제 local TRS 경로(최대 16노드, child 포함)를 생산/fixture 공유 코드로 합성하고 child 위치만 논리 ChildPosition으로 치환한다. 초기 1e-4 실제 정점 대조와 기존 독자 제외/복원은 유지했다.
- admission version=4, 초기 hierarchy 관계/깊이/분포와 세부 제외 사유를 추가했다. 형제 renderer·누락·pivot 밖·과도한 깊이는 계속 제외한다.
- 빌드 Dev/Player/Auto 오류 0/0/0(경고 38/36/38); LoadCheck 275/229/275곳 틀림 0; 패치 계획 38, 계층 25개 포함 LiveDeco 23009, 아틀라스 1052924, edition 22, MatShare 25401, FrameCompare 및 25/144 steps 통과. Player DLL/전달 JSON 해시 유지.
- 최신 전달 ProfSet 967476표본/69.881109%; 전체 DecoAnim/native 쓰기 몫은 미입증이다.
- 실제 계층·소유/런·정점·픽셀/복원·GC/VRAM·FPS·GATE2=PASS는 미확인이다. 설계 한계도 미확정이다. 변경된 DLL의 다음 관측은 admission version=4와 hierarchy/vertexcheck/remaining_reason이다.
- 게임 실행·설치·커밋·푸시·config 변경 없음. 결과 codex-gate2-B-result.md / codex-gate2-B-o.txt; 근거 gate2-B-run4-* 로그와 artifacts.txt.

## 회차 2
- Codex 요약: `child-hierarchy` 전원 제외를 분석하고, 실제 렌더러 계층을 합성하도록 수정했습니다.

- 3종 빌드 오류 0, LoadCheck·회귀 검사 통과.
- 결과 문서·장부·10줄 요약 갱신.
- 게임 실행·설치·커밋 없음.
- 실제 픽셀·FPS와 `GATE2=PASS`는 미확인입니다.

[결과 문서](/E:/StutterFixWork/wt-ld2b/docs/handoff/codex-gate2-B-result.md)


- 시험 핵심 줄:
  - [StutterFix] [livedeco2] GATE2=ERROR reason=InvalidOperationException:atlas-vram-budget-512MiB final_on=False

## 회차 5 — sampler별 아틀라스 계획과 총 예산 (2026-10-21)
- 시작 git status/diff는 깨끗했다. 최신 run2 전달은 admission version=4, renderer!=child/child.parent=pivot/depth=2/path=None 2127개를 지나 atlas-vram-budget-512MiB로 중단했다. 실제 페이지 크기·개수·원본 분포는 없어 단일 거대 페이지/그룹별 중복/내용 자체 초과 중 어느 경로인지 미확인이다.
- 기존 Plan은 sampler를 합친 한 장으로 preferredSize를 정하고 Get은 그룹별로 그 크기의 페이지를 따로 만들며 계획 좌표도 버렸다. 전역 크기·실제 분리·편입 순서가 일치하지 않는 결함을 수정했다. 이전 추상/정지/계층 수정은 보존했다.
- 생산/fixture 공유 Plan이 sampler별 정사각 Shelf 후보를 만들고 총 512MiB 안에서 페이지 수 최소·동수면 바이트 최소 조합을 선택한다. 실제 Shelf·좌표 예약도 Get과 공유하고, 신규 live 입력은 예약을 침범하지 않는다. GPU 페이지는 필요할 때 할당하며 초기 미할당 예약도 성장 예산에 포함한다.
- atlas_plan version=5, 원본 제외/sampler별 수·최대 크기·padded_bytes, 페이지 크기·reserved/allocated bytes·실패 로그를 추가했다. 크기/aniso/sRGB 변화 검사도 보강했다. 기존 2px 패딩·4px 정렬·UV·엄격 판정·기본 꺼짐·Player 제외 유지.
- 게임 디컴파일 SetSprite·tiling의 wrap 변경을 실제 DLL ilscan으로 대조했다. 합성 회귀 17개 2044²는 단일 1024MiB 대신 2장 512MiB, sampler 3그룹 각 5개는 768MiB 대신 5장 512MiB; 실제 Arche 결과는 아니다. 내용 자체가 예산 초과인 입력은 계속 거절한다.
- 빌드 Dev/Player/Auto 오류 0/0/0(경고 38/4/38), LoadCheck 275/229/275곳 틀림 0; 새 계획 118278, 기존 LiveDeco 23009, 아틀라스 1052924, edition 22, 패치 38, MatShare 25401, FrameCompare 및 25/144 steps 통과. 첫 fixture의 최소 페이지 예산 오설정은 시험에서 수정하고 실패 로그를 보존했다.
- 최신 전달 ProfSet 971762표본/69.830267%; 전체 DecoAnim/native 쓰기 몫은 미입증. 일반 Player DLL/전달 JSON 해시는 run4와 동일하다.
- GATE2=PASS·실제 페이지/소유/런·정점·픽셀/복원·GC/VRAM·FPS는 미확인이다. 현재는 설계 한계 확정이 아니라 계획/할당 불일치를 수정한 상태다. 변경된 DLL과 기존 144 steps 인계; 다음 관측은 atlas_plan version=5와 sampler/페이지/예산 로그다.
- 게임 실행·설치·커밋·푸시·config 변경 없음. 결과 codex-gate2-B-result.md / codex-gate2-B-o.txt; 근거 gate2-B-run5-* 로그와 artifacts.txt.

## 회차 3
- Codex 요약: 아틀라스 계획과 실제 할당의 불일치를 수정했습니다. sampler별 배치와 총 512MiB 예산을 함께 계산합니다.

- 3종 빌드 오류 0, LoadCheck·회귀 검사 통과.
- 결과 문서·장부·10줄 요약 갱신.
- 게임 실행·설치·커밋 없음.
- 실제 픽셀·FPS와 `GATE2=PASS`는 미확인입니다.

[결과 문서](/E:/StutterFixWork/wt-ld2b/docs/handoff/codex-gate2-B-result.md)
- 시험 핵심 줄:
  - [StutterFix] [livedeco] cost frame=22468 vertex_ms=13648.0586 upload_ms=6.9079 runs_ms=6513.1056 runs=81 owned=1808 live=2127 new_runs=81 reused_runs=0 heap_delta=0 stable_alloc_bytes=0 transition_all

## 회차 6 — 초기 비용 분리와 더티 런 업로드 (2026-10-21)
- 시작 git status/diff는 깨끗했다. run3 전달은 live=2127·owned=1808·runs=81, 25원본/8192² 두 페이지/512MiB까지 도달했다. 이전 추상 대상·정지·계층·계획 수정은 보존했다.
- 첫 cost 합 20168.0721ms는 vertex_ms에 편입/원본 readback·extrusion·쓰기, runs_ms에 전체 아틀라스 업로드가 섞였다. 정상 프레임 정점 비용이 아니다. 전달에는 PIXEL/FPS/최종 판정·자동 시험 실패 예외가 없어 실제 중단의 유일 원인은 미확정이다.
- 새 접근: 내부 픽셀 행 단위 복사, 편입 시 UV/중심/계층 캐시, local 변경 때만 TRS 재생성, 각도 변경 때만 회전 생성, 정확한 논리 입력/색 변경 추적, 더티 런만 업로드. 공유 CPU 배열에서 위치가 달라진 런은 변경 없는 이웃까지 전체를 다시 써서 업로드한다. 검증/정렬 native 읽기는 매 프레임 유지한다.
- update/startup version=6과 atlas_copy/atlas_upload·prepare/vertex/runs/atlas_upload/upload/cpu_ms·changed_members/uploaded_runs·transition/stable을 기록한다. 정점/업로드 수치만으로 전체 이득을 판단하지 않는다.
- 기존 성능 on 준비는 251→262초의 11곡초보다 동기 준비가 길면 시작 창을 놓칠 수 있다. 성능 준비만 진단 pause/on/resume으로 감싼 148 steps로 생성기·전달 JSON을 함께 변경했다. 자연 재생 ABBA 측정 창·픽셀 3시점·엄격 PASS 조건은 유지했다. 이것이 전달의 실제 실패 예외였다는 주장은 하지 않는다.
- 디컴파일 scrDecoration의 배율/회전/크기/UpdatePosition 및 scrParallax.SetTrans를 실제 DLL IL과 대조했다. 전달 ProfSet 969360표본/69.856397%는 전체 DecoAnim/native 쓰기 몫 증명이 아니다.
- 빌드 Dev/Player/Auto 오류 0/0/0(경고 38/4/38), LoadCheck 275/229/275곳 틀림 0. 계층31·더티604(공유 생산 함수/무작위200·.NET 워밍업 할당0B), LiveDeco23619·아틀라스1052924·계획118278·edition22·패치38·MatShare25401·FrameCompare 및25/148 steps 통과. 게임 Mono/native/GPU 증거는 아니다.
- GATE2=PASS·변경 후 픽셀/복원·FPS·정점≤0.5ms/업로드≤0.4ms·전체 cpu·Mono GC/VRAM 피크는 미확인이다. 설계 한계 확정이 아니라 반복 계산/업로드와 준비 시간 여유 문제를 수정한 상태다. 새 DLL과148 steps의 다음 관측은 version6 및 stable cost 전체 분해다.
- 게임 실행·설치·커밋·푸시·config 변경 없음. 결과 codex-gate2-B-result.md / codex-gate2-B-o.txt; 근거 gate2-B-run6-* 로그와 artifacts.txt.

## 회차 4
- Codex 요약: 반복 계산·업로드를 줄이고, 초기 비용 계측과 시험 준비 절차를 수정했습니다.

- 3종 빌드 오류 0, LoadCheck·회귀 검사 통과.
- 결과 문서·장부·10줄 요약·148단계 시험 JSON 갱신.
- 게임 실행·설치·커밋·푸시 없음.
- 실제 픽셀·FPS·비용 목표와 `GATE2=PASS`는 미확인입니다.

[결과 문서](/E:/StutterFixWork/wt-ld2b/docs/handoff/codex-gate2-B-result.md)


- 시험 핵심 줄:
  - [StutterFix] [livedeco] atlas_copy width=5000 height=5000 read_ms=2418.7092 extrude_ms=1175.1421 write_ms=14.7505
  - [StutterFix] [livedeco] atlas_copy width=3840 height=3840 read_ms=2491.6093 extrude_ms=792.9080 write_ms=6.4395
  - [StutterFix] [livedeco] atlas_copy width=2500 height=2500 read_ms=1096.0453 extrude_ms=340.7590 write_ms=3.3324
  - [StutterFix] [livedeco] atlas_copy width=2048 height=2048 read_ms=752.5428 extrude_ms=287.3155 write_ms=2.3613
  - [StutterFix] [livedeco] atlas_copy width=2048 height=2048 read_ms=774.7975 extrude_ms=261.1022 write_ms=2.1070
  - [StutterFix] [livedeco] atlas_copy width=2048 height=2048 read_ms=851.9842 extrude_ms=256.9039 write_ms=1.8191
  - [StutterFix] [livedeco] atlas_copy width=2048 height=2048 read_ms=761.7281 extrude_ms=238.6527 write_ms=2.0482
  - [StutterFix] [livedeco] atlas_copy width=2048 height=2048 read_ms=756.9934 extrude_ms=280.7523 write_ms=1.7922
  - [StutterFix] [livedeco] atlas_copy width=2880 height=1620 read_ms=890.6300 extrude_ms=266.5175 write_ms=2.0088
  - [StutterFix] [livedeco] atlas_copy width=960 height=720 read_ms=140.0023 extrude_ms=53.0922 write_ms=0.3528
  - [StutterFix] [livedeco] atlas_copy width=682 height=682 read_ms=72.1882 extrude_ms=25.7022 write_ms=0.2760
  - [StutterFix] [livedeco] atlas_copy width=512 height=512 read_ms=22.8486 extrude_ms=16.9438 write_ms=0.1486
