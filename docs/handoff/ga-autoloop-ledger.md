## codex-al-ga-4 자동 반복 4회차 — 2026-10-21 (r10, 긴 준비가 곡 시각을 넘기는 ABBA 오류 수정)
- 시작 HEAD 4d53c77, 작업 트리 깨끗함. 게임 실행·설치·커밋·푸시·config 변경 없음.
- 최신 run3(19:28)와 기존 전체 al-ga-3.log를 읽기 전용 감사. 원본 SHA256·행 번호·표본 집계·conductor 디컴파일/IL 교차확인 보존.
- 기존 262/270/276초 live2127/2209/17, owned2025/2123/17(95.204513%/96.106836%/100%). ab_out431703/124277/0, maxdiff6/5/0, aa2_out0·stableTrue. 재생 유지 증거 없음.
- 기존 scan0.000100~0.001100ms. 정지262/270 각7프레임 가중평균 reconcile3.025286/3.015743·vertex0.592586/0.597672·upload0.070386/0.078586ms. 목표·무할당·FPS 이득 달성 아님.
- 기존 off0만125.723528FPS/7.953961ms 완료. on 준비14.519818초(patch13.268672초), 첫 렌더0.080512초. 곡252초에서 동기 준비 뒤 fuse266.7초로 넘어가 시작 범위/3초 warmup 실패, GATE2=ERROR. 켬FPS/이득률 미측정.
- ABBA 모두258초에 pause→on/off→ready(첫 PreCull 완료 대기)→resume→262초 재생. 측정262→276초에는 pause 없음. 144단계, renderer_version9. 시간/픽셀/이득/OWNED_LOW 기준 유지, 정렬 보호/스캔 제거 유지.
- 시작 거절에 song/active/ready/owned/warmup/transition 및 Gate2 전체 예외 스택 기록. 승인된 Begin만 창 상태 반영. 준비 시간을 줄였다는 주장 없음.
- 실제 Gate2 코드·현재 JSON 검사58건 통과. 이전 HEAD JSON은 song266.632330/warmup0.016/readyTrue에서 예상 exit1, 수정 JSON exit0. 실제 Unity/DSP/수명/성능 증명 아님.
- 빌드 오류0/0/0·LoadCheck 틀림0/0/0, 시작29/계약5/정책106479/gate18/edition42/hook22, Dormancy20/계층2729/바운드482/스크립트15/캐시40/업로드32, 델타5918737/쓰기133/MatShare25401/FrameCompare/생성기24·25·144 통과.
- 결과·10줄요약·r10 증거/검증 갱신. 수정 후 소유율·비용·픽셀·FPS·실게임 복원 미측정, GATE2=PASS 미달성. 동일 코드 게임 시험 반복 실행/요청 없음.

## codex-al-ga-3 자동 반복 3회차 — 2026-10-21 (r9, 첫 소유를 메인 카메라 PreCull로 이동)
- 시작 HEAD 12b62cc, 작업 트리 깨끗함. 게임 실행·설치·커밋·푸시·config 변경 없음.
- 최신 전달 run2(19:05)와 기존 전체 al-ga-2.log를 읽기 전용 감사. 원본 SHA256·행 번호·스택·카메라 디컴파일/IL 교차확인 보존.
- 기존 262/270초 live2127/2263, owned2026/2165(95.251528%/95.669465%), 276초17/17. 재생 중 유지 증거는 없음. scan0.000100~0.000900ms로 전체 스캔 제거 효과는 관측됐으나 paused delta약2.7ms·vertex약0.59ms·첫enable39.243초가 남음.
- 기존 PIXEL262/270/276=439553/130006/0, maxdiff6/5/0, aa2_out0·stableTrue. off0=124.133396FPS/8.055850ms만 완료. 켬 창 앞 camera_z0→-10 오류로 GATE2=ERROR. 과거−87%를 새 켬 FPS로 전용하지 않음.
- 게임 Update의 Vector2 localPosition(Z=0)→LateUpdate.SetPosition(Z=-10)과 Begin 동기 RenderFrame이 충돌. 첫 소유/검증/키 생성을 메인 카메라 첫 PreCull로 이동, 이후 Z/축/모드/마스크 변경 거절 유지. 초기 실패·취소 복원과 ready상태 구별.
- renderer_version8, prepare/register/patch/first_render/wait_first_cull 계측과 activation/steady 비용 분리. 3초 warmup/OWNED_LOW 시계는 첫 소유 렌더 완료 후 시작. 전체 스캔 재도입·임계 완화·steps/픽셀/제외 영역 변경 없음.
- 생산 시작/정렬29건·소스계약5건 통과. !starting 예외를 없앤 대조본 exit1, 수정본 통과. 실제 Unity callback/수명/성능 증명은 아님.
- 빌드 오류0/0/0·LoadCheck 틀림0/0/0, 정책106479/gate18/edition42/hook22, Dormancy20/계층2729/바운드482/스크립트15/캐시40/업로드32, 델타5918737/쓰기133/MatShare25401/FrameCompare/생성기24·25·132 통과.
- 결과·10줄요약·r9 증거/검증 기록 갱신. 수정 후 소유율·비용·픽셀·FPS·실게임 수명 미측정, GATE2=PASS 미달성. 동일 코드 게임 시험 반복 실행/요청 없음.

## codex-al-ga-2 자동 반복 2회차 — 2026-10-21 (r8, 비교용 pause 초기화 수정)
- 시작 HEAD 31e44fe, 작업 트리 깨끗함. 게임 실행·설치·커밋·푸시·config 변경 없음.
- 파일 수정 시각으로 최신 run1(18:44)을 선택했다. Prof979110표본/set_pct69.121312%/valid=True 뒤 첫 livedeco on이 Dormancy live tracking is not ready로 중단, GATE2=ERROR. 소유/픽셀/FPS는 미도달이다.
- 기존 전체 al-ga-1.log 364행 pause, 366행 정지 전환, 435~438행 오류·판정을 확인하고 원본 해시/발췌를 보존했다.
- FrameCompare는 pause를 허용하지만 Dormancy.List의 !Hitch.Playing이 source/awake를 지워 Begin의 readiness와 충돌한다. 관리 코드·게임 디컴파일·설치 관리자 IL을 대조했다.
- DEV/AutoTest의 비교용 pause 동안 목록/델타를 유지하고, 명시적 Begin에서 소유 전에 현재 목록을 한 번 준비한다. 설정/오류 차단과 일반 정지/맵해제 복원은 유지, 실패 플래그 진단 추가. 프레임당 전체 스캔 재도입 없음.
- 실제 Dormancy.cs 수명 fixture20건 통과. playing-only 조건의 대조본은 pause-owner 검사에서 exit1, 수정본은 통과. 실제 Unity 수명/성능 증명은 아니다.
- 빌드 오류0/0/0·LoadCheck 틀림0/0/0, 정책106479/gate18/edition42/hook22, 계층2729/바운드482/스크립트15/캐시40/업로드32, 델타5918737/쓰기133, FrameCompare 및132단계 통과.
- 결과/10줄요약/r8 증거 갱신. 새 소유율·비용·픽셀·FPS는 미측정. 과거18개·FPS를 새 코드 결과로 전용하지 않음. Gate2 통과 미확인. 동일 코드 게임 시험 반복 없음.

## codex-al-ga-6 분석 — 2026-10-21 (감사 run7, 현재 설계 한계로 중단)
- 시작 HEAD ad65eab, 처음 작업 트리 깨끗함. 작업 중 외부 추가된 gate2-common.md 긴급 지시를 읽고 diff 보존. 게임 실행·설치·커밋·푸시·config·소스·steps·바이너리 변경 없음.
- run5 전달은 전체 로그 후반 발췌. 기존 al-ga-5.log를 보완 감사하고 원본 SHA256·행 번호·집계·소스 해시를 gate2-A-run7-analysis.txt에 보존. 신규 게임 측정 없음.
- 262/270/276초 ab_out=391798/121812/0, maxdiff=6/7/0, noise=0·aa2_out=0·stable=True 모두 확인. Float32/캐시 동시 변경으로 한 변수 원인 확정 불가. 이전674/2보다 회귀했지만 시각/소유 집합도 다름. Color32 단순 복귀·기준 완화 없음.
- ABBA FPS=122.331735/14.682848/14.738163/120.325125, 평균 off121.328430/on14.7105055, gain=-87.875467028%. 최종 GATE2=PIXEL_FAIL/outside_pixels_changed/on=False. 132단계 완료, 이전 GPU 타임아웃 재수정 근거 없음.
- Prof873985표본/set_pct70.049131%/valid=True. 기존 Step 표본·PairXY/GroupStep 억제·후속 엔진 제외 한계 유지.
- 매 프레임 Scan 대상 registry157254개. FindObjects는 Register에서만. 소유17개 정지8프레임도 scan27.876138ms, vertex0.019550/upload0.000850/호출0. 목표FPS139.5276945의 프레임 예산7.167035932ms보다 관측 스캔만 큼.
- on창 samples=1 출력172/171건 평균 scan46.211451/46.172319, reconcile4.241067/4.206554, vertex1.656350/1.649179, upload0.676295/0.682586ms. 각204프레임 전체와 표본 다름. 등록은 scan 포함, Harmony/SkipLate/원래게임/엔진/로그/대기 정확 분해 미측정.
- 소유1400~1600 표본109/115건, vertex중앙1.689700/1.678800, upload중앙0.678900/0.674800ms, 목표 동시충족0/109·0/115. transition각204회, created합10027/9861·destroyed합11061/10888. 전환 생성과 힙변화가 있어 allocated_bytes=0만으로 GC통과 주장 안 함.
- 276초 새 Begin부터 owned17, vertexcheck count17/excluded0. 끝시점18개를 262초1500개의 누적오제외로 단정 불가. !Live/alpha/boundary/geometry 세부해제사유와 live분모 없어 과소소유 여부 미확정. 가시성 강제 소유 수정 없음.
- BoundaryPrefix는 장식 일부 메서드만 감시. 비소유 활성/재질/정렬/계층 변경 dirty 프로토콜 없어 Scan 단순삭제·주기연장으로 안전성 유지 불가. 필요한 것은 입장시 변경작성경로 분류와 scene 등록/dirty/reset 설계. 현재 폴링·반복검사 구현 한계로 중단하며 변형A 전체 불가능으로 일반화 안 함.
- 지정 디컴파일·설치DLL IL(scrDecoration/manager/parallax/visual), Renderer API와 게임해시를 새로 감사. 게임메서드 실행 없음. 소스/산출물 변경 없어 빌드/fixture 재실행 없이 기존 run6 오류0·fixture통과 로그 확인.
- 결과 최신6회차 절·10줄요약·run7감사 갱신. 스캔제거·소유감소원인수정·픽셀0·FPS+15%·수명검증 미완료. GATE2=PASS 미달성. 동일 코드 재시험 실행/요청 없음.

## codex-al-ga-5 수정 — 2026-10-21 (검증 로그 run6)
- 시작 HEAD ba29aa8, 처음 작업 트리는 깨끗함. 기존 계층·ToggleVisible·바운드·PlanetSprite 정지 유지. gate2-common.md 성능 지침 추가는 외부 변경으로 보존.
- run4 전달: 소유/구간 1,478/1,114, 262초 ab_out=674/maxdiff=2/aa2_out=0/stable=True. Prof samples=953,430 / set_pct=69.633873% / valid=True(기존 Step 표본 한계 유지).
- 기존 al-ga-4.log 614행에서 270초 framecmp grab B GPU readback 15초 타임아웃 확인. 두 시점 noise=0/상태 동일. 전체 로그 출처 해시·발췌 보존. FPS 미도달.
- Request 직후 전체 scene 동기 상태 감사가 GPU 제한 시간에 포함되는 구조 수정. 동일 EndOfFrame의 상태 검사를 먼저 끝내고 Request→시각→1회 yield→완료/15초 검사. CPU 감사/대기 로그 분리, RT 수명 유지. 기존 GPU 지연의 실제 기여량은 미확정.
- 정점 색의 Color32 추가 양자화 제거: 엄격한 동일 목표를 우선해 Float32x4/stride36 사용(초기 Color32 명세와의 차이 명시). 재질·텍스처·UV 유지, 레이아웃 검증. 기존 674픽셀 전체 해결 주장은 안 함.
- Compute의 장식별 Unity 프로퍼티 읽기를 카메라 공통 스냅샷으로 대체. 캐시 Shape/UV·정확한 pose/색 dirty·각도 재사용·현재 구간 멤버 순회·더티 구간만 업로드. 새/재배치 버퍼는 전량 채움.
- renderer_version=5, dirty_images/skipped_uploads/mesh_targets_met 및 목표 0.5/0.4ms 추가. PIXEL_NEAR 최대2/pass=0, 최종 픽셀0·FPS+15% 기준 유지. 정렬 근거 없이 다른 구간을 병합하지 않음.
- 빌드 오류 0/0/0, LoadCheck 틀림 0/0/0. 계층2,729·바운드482·스크립트15·캐시40·업로드32·구간106,460·판정18·edition36·패치17·FrameCompare11/15/15/17/21/2·MatShare25,401·steps24/25/132 통과.
- 게임 실행·설치·커밋·푸시·config 수정 없음. 동일 코드 게임 시험 반복 없음. 수정 후 실픽셀/FPS/GC/수명·정점≤0.5ms/업로드≤0.4ms는 미측정. 기존 scan35.655163/34.950140ms와 SkipLate 반복 안전 검사는 남는다. GATE2=PASS 미달성.
- 결과 문서 최신 run6 절·10줄 요약·gate2-A-run6-* 로그 갱신. 생성기는132단계 유지, 변경된 코드의 감사/대기 분리·Float32·dirty/skip 수치로 다음 판별 가능. 이 세션은 게임 실행 없이 종료.

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

## 회차 5
- Codex 요약: 타임아웃 계산, 색 정밀도, 정점 캐시·더티 업로드를 수정했습니다.

- 3종 빌드·LoadCheck 오류 0, 회귀 검사 통과.
- 결과 문서·장부·10줄 요약 갱신.
- 게임 실행·설치·커밋·푸시 없음.

실픽셀·FPS·성능 목표는 미측정이므로 **GATE2=PASS는 미달성**입니다.

[결과 문서](E:/StutterFixWork/wt-ld2a/docs/handoff/codex-gate2-A-result.md)


- 시험 핵심 줄:
  - [StutterFix] [livedeco] transition frame=23845 owned=1490 runs=1113 created=1113 reused=0 destroyed=0 reconcile_ms=21.075300
  - [StutterFix] [livedeco] cost per_frame=1 samples=1 owned=1490 runs=1113 vertex_ms=0.859000 upload_ms=1.700100 reconcile_ms=21.075300 scan_ms=288.621600 upload_calls=1113.000 dirty_images=1490.000 skip
  - [StutterFix] [livedeco] cost per_frame=1 samples=8 owned=1490 runs=1113 vertex_ms=0.694400 upload_ms=0.069988 reconcile_ms=0.000000 scan_ms=36.045113 upload_calls=0.000 dirty_images=0.000 skipped_uplo
  - [StutterFix] [livedeco] transition frame=25156 owned=1518 runs=1153 created=1153 reused=0 destroyed=0 reconcile_ms=22.093600
  - [StutterFix] [livedeco] cost per_frame=1 samples=1 owned=1518 runs=1153 vertex_ms=1.105100 upload_ms=1.852800 reconcile_ms=22.093600 scan_ms=300.260700 upload_calls=1153.000 dirty_images=1518.000 skip
  - [StutterFix] [livedeco] cost per_frame=1 samples=8 owned=1518 runs=1153 vertex_ms=0.835125 upload_ms=0.105613 reconcile_ms=0.000000 scan_ms=38.227325 upload_calls=0.000 dirty_images=0.000 skipped_uplo
  - [StutterFix] [livedeco] transition frame=26655 owned=17 runs=15 created=15 reused=0 destroyed=0 reconcile_ms=0.470800
  - [StutterFix] [livedeco] cost per_frame=1 samples=1 owned=17 runs=15 vertex_ms=0.022300 upload_ms=0.016500 reconcile_ms=0.470800 scan_ms=31.181300 upload_calls=15.000 dirty_images=17.000 skipped_upload
  - [StutterFix] [livedeco] cost per_frame=1 samples=8 owned=17 runs=15 vertex_ms=0.019550 upload_ms=0.000850 reconcile_ms=0.000000 scan_ms=27.876138 upload_calls=0.000 dirty_images=0.000 skipped_uploads=
  - [StutterFix] [livedeco2] FPS mode=off index=0 fps=122.331735 ms=8.174494 frames=1705 elapsed_s=13.937512 song_begin=262.060504 song_end=276.004290 valid=True
  - [StutterFix] [livedeco] transition frame=31367 owned=164 runs=97 created=97 reused=0 destroyed=0 reconcile_ms=1.729900
  - [StutterFix] [livedeco] cost per_frame=1 samples=1 owned=164 runs=97 vertex_ms=0.100500 upload_ms=0.096200 reconcile_ms=1.729900 scan_ms=65.037800 upload_calls=97.000 dirty_images=164.000 skipped_uplo

## 회차 6
- Codex 요약: 인계의 **설계 한계 중단 조건**을 적용했습니다. 코드 수정 없이 결과·장부·10줄 요약을 갱신했습니다.

- 기존 시험: `GATE2=PIXEL_FAIL`, FPS 이득 **−87.875467%**.
- 전체 렌더러 157,254개 스캔 비용이 핵심 병목입니다.
- 안전한 증분 갱신에 필요한 변경 감지 구조는 미구현입니다.
- 오프라인 감사 25건·`git diff --check` 통과.
- 게임 실행·설치·커밋 없음.

[상세 결과](E:/StutterFixWork/wt-ld2a/docs/handoff/codex-gate2-A-result.md)


- 시험 핵심 줄:
  - [StutterFix] [livedeco] transition frame=23981 owned=1483 runs=1119 created=1119 reused=0 destroyed=0 reconcile_ms=21.219000
  - [StutterFix] [livedeco] cost per_frame=1 samples=1 owned=1483 runs=1119 vertex_ms=0.991800 upload_ms=1.839800 reconcile_ms=21.219000 scan_ms=280.456900 upload_calls=1119.000 dirty_images=1483.000 skip
  - [StutterFix] [livedeco] cost per_frame=1 samples=8 owned=1483 runs=1119 vertex_ms=0.774550 upload_ms=0.089675 reconcile_ms=0.000000 scan_ms=36.990338 upload_calls=0.000 dirty_images=0.000 skipped_uplo
  - [StutterFix] [livedeco] transition frame=25295 owned=1611 runs=1255 created=1255 reused=0 destroyed=0 reconcile_ms=20.112100
  - [StutterFix] [livedeco] cost per_frame=1 samples=1 owned=1611 runs=1255 vertex_ms=0.996900 upload_ms=1.432200 reconcile_ms=20.112100 scan_ms=271.613900 upload_calls=1255.000 dirty_images=1611.000 skip
  - [StutterFix] [livedeco] cost per_frame=1 samples=8 owned=1611 runs=1255 vertex_ms=0.778138 upload_ms=0.078288 reconcile_ms=0.000000 scan_ms=36.725763 upload_calls=0.000 dirty_images=0.000 skipped_uplo
  - [StutterFix] [livedeco] transition frame=26804 owned=17 runs=15 created=15 reused=0 destroyed=0 reconcile_ms=0.340000
  - [StutterFix] [livedeco] cost per_frame=1 samples=1 owned=17 runs=15 vertex_ms=0.018000 upload_ms=0.014600 reconcile_ms=0.340000 scan_ms=30.233400 upload_calls=15.000 dirty_images=17.000 skipped_upload
  - [StutterFix] [livedeco] cost per_frame=1 samples=8 owned=17 runs=15 vertex_ms=0.020550 upload_ms=0.000825 reconcile_ms=0.000000 scan_ms=27.677563 upload_calls=0.000 dirty_images=0.000 skipped_uploads=
  - [StutterFix] [livedeco2] FPS mode=off index=0 fps=122.358015 ms=8.172738 frames=1706 elapsed_s=13.942691 song_begin=262.060504 song_end=276.004443 valid=True
  - [StutterFix] [livedeco] transition frame=31503 owned=167 runs=95 created=95 reused=0 destroyed=0 reconcile_ms=1.735800
  - [StutterFix] [livedeco] cost per_frame=1 samples=1 owned=167 runs=95 vertex_ms=0.103700 upload_ms=0.100800 reconcile_ms=1.735800 scan_ms=64.339800 upload_calls=95.000 dirty_images=167.000 skipped_uplo

## 회차 7 — Dormancy 이벤트 기반 구조 변경

- 시작부터 있던 Dormancy 변경을 보존하고 Changed/Rebuilt/Audit/Reset을 구현에 연결했다. 게임 실행·설치·커밋·푸시·config 변경 없음.
- RenderFrame의 전체 renderer Scan/재등록을 제거했다. 깨움/잠듦 델타·정적 승인/거절 캐시·게임/모드 관리 쓰기 통지·색인·부분 구간 재구성·공유 정점 영역 재사용을 구현했다.
- 논리 가시성과 소유 숨김 비트 분리·InvisibleSkip 충돌 방지·해제 사유와 live 분모 시계열·0.01px 오차/히스토그램·초당 비용/감사·3초 warmup·OWNED_LOW 조기 중단·수명 복원을 추가했다.
- 새 게임 측정은 없다. 소유 90%·scan≤0.05ms·vertex≤0.5ms·upload≤0.4ms·FPS+15%·픽셀0·실게임 수명은 미검증이다. 픽셀 수정은 1~3의 새 수치 전까지 보류했다.
- 오프라인: 3종 빌드 오류0/0/0, LoadCheck 틀림0/0/0, 생산 델타5,918,737건·쓰기133건·정책106,479건·gate18건·edition39건·hook22개 및 기존 fixture/132단계 검사 통과.
- 최신 기존 al-ga-6.log는 262/270/276초 ab_out376530/124332/0, maxdiff6/5/0, FPS gain−87.686140%, GATE2=PIXEL_FAIL이다. 수정 후 측정으로 전용하지 않는다.
- 결과 문서·10줄 요약·r7 빌드/fixture/기존 증거/소스 감사 갱신. 실제 Mono patch 설치·초기 색인/patch 비용·callback 부하·다른 어셈블리/네이티브/반사/generic 경로의 포괄성은 증명하지 않았다. GATE2=PASS 미달성.

## 회차 1
- Codex 요약: Dormancy 기반 델타·캐시·부분 구간 갱신을 구현했습니다. 3종 빌드·LoadCheck 오류 0, 오프라인 검증 통과입니다.

게임 실행·설치·커밋은 하지 않았습니다. 실픽셀·FPS는 미측정이므로 **Gate 2 PASS는 미달성**입니다.

[상세 결과](/E:/StutterFixWork/wt-ld2a/docs/handoff/codex-gate2-A-result.md)


- 시험 핵심 줄:
  - [StutterFix] [livedeco2] GATE2=ERROR reason=autotest_aborted:오류로 그만둠 variant=A on=False

## 회차 2
- Codex 요약: 일시정지 때 Dormancy 목록이 지워져 소유 시작이 실패하던 오류를 수정했습니다.

- 3종 빌드·LoadCheck 오류 0, 회귀 검사 통과.
- 결과 문서·장부·10줄 요약 갱신.
- 게임 실행·설치·커밋·푸시 없음.

실게임 픽셀·FPS는 미측정이므로 **Gate 2 통과는 미확인**입니다.

[상세 결과](E:/StutterFixWork/wt-ld2a/docs/handoff/codex-gate2-A-result.md)


- 시험 핵심 줄:
  - [StutterFix] [livedeco] cost per_frame=1 samples=1 owned=2026 runs=1234 vertex_ms=5.638800 upload_ms=4.825300 reconcile_ms=1682.110000 delta_ms=1500.903300 scan_ms=0.000900 audit_ms=0.000000 awake=212
  - [StutterFix] [livedeco] owned_ts t=262.017840 awake=2127 owned=2026 runs=1234 dirty=157254 scan_ms=0.000900 vertex_ms=5.638800 upload_ms=4.825300 transition_ms=181.206700 audits=0 missed=0
  - [StutterFix] [livedeco] begin variant=A renderer_version=7 color_format=Float32x4 vertex_stride=36 live_source=Dormancy-events change_methods=1723 change_sites=4039 enable_transition_ms=39243.351600 e
  - [StutterFix] [livedeco] cost per_frame=1 samples=4 owned=2026 runs=1234 vertex_ms=0.589925 upload_ms=0.071225 reconcile_ms=4.895575 delta_ms=4.895575 scan_ms=0.000250 audit_ms=1.193950 awake=2127 dorm
  - [StutterFix] [livedeco] owned_ts t=262.017840 awake=2127 owned=2026 runs=1234 dirty=5609 scan_ms=0.000250 vertex_ms=0.589925 upload_ms=0.071225 transition_ms=39243.351600 audits=2 missed=0
  - [StutterFix] [livedeco] cost per_frame=1 samples=1 owned=2026 runs=1234 vertex_ms=0.596000 upload_ms=0.072100 reconcile_ms=2.795300 delta_ms=2.795300 scan_ms=0.000100 audit_ms=2.361100 awake=2127 dorm
  - [StutterFix] [livedeco] owned_ts t=262.017840 awake=2127 owned=2026 runs=1234 dirty=984 scan_ms=0.000100 vertex_ms=0.596000 upload_ms=0.072100 transition_ms=39243.351600 audits=3 missed=0
  - [StutterFix] [livedeco] cost per_frame=1 samples=3 owned=2026 runs=1234 vertex_ms=0.599567 upload_ms=0.069867 reconcile_ms=2.690533 delta_ms=2.690533 scan_ms=0.000467 audit_ms=0.766233 awake=2127 dorm
  - [StutterFix] [livedeco] owned_ts t=262.017840 awake=2127 owned=2026 runs=1234 dirty=2952 scan_ms=0.000467 vertex_ms=0.599567 upload_ms=0.069867 transition_ms=39243.351600 audits=4 missed=0
  - [StutterFix] [livedeco] cost per_frame=1 samples=1 owned=2026 runs=1234 vertex_ms=0.586400 upload_ms=0.068400 reconcile_ms=2.732000 delta_ms=2.732000 scan_ms=0.000300 audit_ms=0.000000 awake=2127 dorm
  - [StutterFix] [livedeco] owned_ts t=262.017840 awake=2127 owned=2026 runs=1234 dirty=984 scan_ms=0.000300 vertex_ms=0.586400 upload_ms=0.068400 transition_ms=39243.351600 audits=4 missed=0
  - [StutterFix] [livedeco] begin variant=A renderer_version=7 color_format=Float32x4 vertex_stride=36 live_source=Dormancy-events change_methods=1723 change_sites=4039 enable_transition_ms=14632.802600 e

## 회차 3
- Codex 요약: 카메라 Z 전환(0→−10) 때문에 시험이 중단되던 경로를 수정했습니다. 첫 소유·검증을 `PreCull`로 옮겼습니다.

- 3종 빌드·LoadCheck 오류 0, 신규 29건·기존 회귀 검사 통과.
- 게임 실행·설치·커밋 없음.
- 수정 후 실게임 픽셀·FPS는 미측정으로, **Gate 2 통과는 미확인**입니다.

[결과 문서](E:/StutterFixWork/wt-ld2a/docs/handoff/codex-gate2-A-result.md)에 근거와 남은 한계를 기록했습니다.
- 시험 핵심 줄:
  - [StutterFix] [livedeco] activation pending=1 first_render=main-camera-PreCull setup_ms=13354.685000 prepare_ms=5.101600 register_ms=1318.962600 patch_ms=12030.008700
  - [StutterFix] [livedeco] begin variant=A renderer_version=8 color_format=Float32x4 vertex_stride=36 live_source=Dormancy-events change_methods=1723 change_sites=4039 enable_transition_ms=13704.717400 p
  - [StutterFix] [livedeco] cost per_frame=1 samples=1 phase=activation owned=2025 runs=1244 vertex_ms=1.014800 upload_ms=2.790800 reconcile_ms=346.173200 delta_ms=314.929100 scan_ms=0.001100 audit_ms=2.7
  - [StutterFix] [livedeco] owned_ts t=262.017840 awake=2127 owned=2025 runs=1244 dirty=157254 phase=activation scan_ms=0.001100 vertex_ms=1.014800 upload_ms=2.790800 transition_ms=13704.717400 audits=1 m
  - [StutterFix] [livedeco] cost per_frame=1 samples=4 phase=steady owned=2025 runs=1244 vertex_ms=0.602650 upload_ms=0.070525 reconcile_ms=3.363150 delta_ms=3.363150 scan_ms=0.000200 audit_ms=0.590900 aw
  - [StutterFix] [livedeco] owned_ts t=262.017840 awake=2127 owned=2025 runs=1244 dirty=5661 phase=steady scan_ms=0.000200 vertex_ms=0.602650 upload_ms=0.070525 transition_ms=13704.717400 audits=2 missed=
  - [StutterFix] [livedeco] cost per_frame=1 samples=2 phase=steady owned=2025 runs=1244 vertex_ms=0.580850 upload_ms=0.071050 reconcile_ms=2.572900 delta_ms=2.572900 scan_ms=0.000150 audit_ms=1.103650 aw
  - [StutterFix] [livedeco] owned_ts t=262.017840 awake=2127 owned=2025 runs=1244 dirty=1976 phase=steady scan_ms=0.000150 vertex_ms=0.580850 upload_ms=0.071050 transition_ms=13704.717400 audits=3 missed=
  - [StutterFix] [livedeco] cost per_frame=1 samples=1 phase=steady owned=2025 runs=1244 vertex_ms=0.575800 upload_ms=0.068500 reconcile_ms=2.578600 delta_ms=2.578600 scan_ms=0.000100 audit_ms=0.000000 aw
  - [StutterFix] [livedeco] owned_ts t=262.017840 awake=2127 owned=2025 runs=1244 dirty=988 phase=steady scan_ms=0.000100 vertex_ms=0.575800 upload_ms=0.068500 transition_ms=13704.717400 audits=3 missed=0
  - [StutterFix] [livedeco] activation pending=1 first_render=main-camera-PreCull setup_ms=13666.814500 prepare_ms=2.538600 register_ms=1248.603700 patch_ms=12415.634700
  - [StutterFix] [livedeco] begin variant=A renderer_version=8 color_format=Float32x4 vertex_stride=36 live_source=Dormancy-events change_methods=1723 change_sites=4039 enable_transition_ms=14006.710500 p

## 회차 4
- Codex 요약: 약 14.5초의 준비 중 곡 시각이 넘어가던 ABBA 시작 오류를 수정했습니다.

- 3종 빌드·LoadCheck 오류 0, 신규 검사 58건과 기존 회귀 검사 통과.
- 결과 문서·장부·10줄 요약 갱신.
- 게임 실행·설치·커밋·푸시는 하지 않았습니다.

수정 후 실픽셀·FPS는 미측정이므로 **GATE2는 아직 미통과**입니다.

[결과 문서](E:/StutterFixWork/wt-ld2a/docs/handoff/codex-gate2-A-result.md) · [10줄 요약](E:/StutterFixWork/wt-ld2a/docs/handoff/codex-gate2-A-o.txt)


- 시험 핵심 줄:
  - [StutterFix] [livedeco] activation pending=1 first_render=main-camera-PreCull setup_ms=32610.453800 prepare_ms=2.961400 register_ms=1396.917100 patch_ms=31210.014000
  - [StutterFix] [livedeco] begin variant=A renderer_version=9 color_format=Float32x4 vertex_stride=36 live_source=Dormancy-events change_methods=1723 change_sites=4039 enable_transition_ms=33718.803700 p
  - [StutterFix] [livedeco] cost per_frame=1 samples=1 phase=activation owned=2022 runs=1246 vertex_ms=0.910200 upload_ms=2.137300 reconcile_ms=1105.249000 delta_ms=1042.275400 scan_ms=0.000900 audit_ms=2
  - [StutterFix] [livedeco] owned_ts t=262.017840 awake=2127 owned=2022 runs=1246 dirty=157254 phase=activation scan_ms=0.000900 vertex_ms=0.910200 upload_ms=2.137300 transition_ms=33718.803700 audits=1 m
  - [StutterFix] [livedeco] cost per_frame=1 samples=1 phase=steady owned=2022 runs=1246 vertex_ms=0.563200 upload_ms=0.069700 reconcile_ms=5.557900 delta_ms=5.557900 scan_ms=0.000200 audit_ms=2.364800 aw
  - [StutterFix] [livedeco] owned_ts t=262.017840 awake=2127 owned=2022 runs=1246 dirty=2713 phase=steady scan_ms=0.000200 vertex_ms=0.563200 upload_ms=0.069700 transition_ms=33718.803700 audits=2 missed=
  - [StutterFix] [livedeco] cost per_frame=1 samples=3 phase=steady owned=2022 runs=1246 vertex_ms=0.560300 upload_ms=0.070200 reconcile_ms=2.537100 delta_ms=2.537100 scan_ms=0.000167 audit_ms=0.730533 aw
  - [StutterFix] [livedeco] owned_ts t=262.017840 awake=2127 owned=2022 runs=1246 dirty=3009 phase=steady scan_ms=0.000167 vertex_ms=0.560300 upload_ms=0.070200 transition_ms=33718.803700 audits=3 missed=
  - [StutterFix] [livedeco] cost per_frame=1 samples=2 phase=steady owned=2022 runs=1246 vertex_ms=0.587350 upload_ms=0.070800 reconcile_ms=2.771700 delta_ms=2.771700 scan_ms=0.000150 audit_ms=1.071650 aw
  - [StutterFix] [livedeco] owned_ts t=262.017840 awake=2127 owned=2022 runs=1246 dirty=2006 phase=steady scan_ms=0.000150 vertex_ms=0.587350 upload_ms=0.070800 transition_ms=33718.803700 audits=4 missed=
  - [StutterFix] [livedeco] cost per_frame=1 samples=1 phase=steady owned=2022 runs=1246 vertex_ms=0.568200 upload_ms=0.070400 reconcile_ms=2.542200 delta_ms=2.542200 scan_ms=0.000200 audit_ms=0.000000 aw
  - [StutterFix] [livedeco] owned_ts t=262.017840 awake=2127 owned=2022 runs=1246 dirty=1003 phase=steady scan_ms=0.000200 vertex_ms=0.568200 upload_ms=0.070400 transition_ms=33718.803700 audits=4 missed=
