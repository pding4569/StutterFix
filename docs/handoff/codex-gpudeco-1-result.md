# gpudeco-1 결과 — 자동 반복 2회차

**기존 Unity 6000.3.10f1 번들 경로로 GPU 수치 대조·Raw 메시 그리기·번들 생성/재로드를 완료했다. export 대기의 시간 초과와 일시정지 시 bake 폐기 경로를 수정했다. 실제 Arche export는 아직 0개이며, 합성 1,000구간 제출 2.723905ms는 목표 0.3ms를 초과한다. 게임 G0/G1은 미검증이므로 GPU 소유/게임 렌더러 연결로 진행하지 않았다.**

게임 실행·설치·커밋·푸시 **0회**, .codex/config.toml 수정 **0회**다. 독립 에디터와 읽기 전용 참조/오프라인 도구만 실행했다. 일반 Player에서는 GPU/bake 진단 코드가 제외된다.

## 방식 선택과 실제 에디터 실행

기존 fsr/BuildFsr.cs.txt와 docs/shader-research.md의 AssetBundle 방식을 재사용했다. [랩 결과](codex-gpudeco-lab-result.md)에 실패/수정/채널별 수치/한계를 모았다.

지시서의 정정대로 Unity.exe를 직접 호출했다. 첫 호출은 IPC 또는 라이선스 exit 198이었지만, **-batchmode -quit -force-d3d11 직접 호출**에서 6000.3.10f1·RTX 4060 Ti·Direct3D11 fixture가 실제 실행됐다. 로그인/활성화는 하지 않았다. -nographics 경로는 Null Device라 GPU 검사에 쓰지 않았다. 성공한 전체 명령은 [README](../../tools/gpudeco-lab/README.md)에 있다.

최초 번들은 생성됐어도 재로드가 실패했다. 버전 오류만 보고 에디터 호환성 문제로 결론내리지 않고, 로그의 **AssetBundle 내장 모듈 비활성화**를 확인했다. 독립 프로젝트 manifest 참조를 수정하고 강제 재생성해 **35,399바이트 번들 재로드 및 101,475개 이징 대조**를 통과했다. 게임에서의 적재는 미검증이다. sfnative.dll은 변경하지 않았다.

## export를 막는 코드 경로 수정

1. **실패 이력**: gg 장부/회차 1 증거에는 timeout 180 뒤 decobake ready 대기에서 export가 만들어지지 않았다. 같은 steps를 반복하지 않았다. 새 생성기는 첫 줄 **timeout 420**, open 뒤 **wait 60 → decobake state → wait 60 → decobake ready**로 나눈다. state는 실제 ready/세대/collector/worker를 로그한다.
2. **5분 예산**: gpudeco prepare를 open 전에 시작한다. bake Tick/ready/export에서 **300초**를 확인하고 넘으면 prepare_ms·상태·ERROR를 기록한다. 첫 export 로그는 open+bake+seek+파일 쓰기 전체와 bake wall을 분리하고, export 자체 비용도 남긴다. gpudeco off/reset 및 bake 종료에서 추적을 해제한다. 이는 시간 제한 구현이며 **실제 준비가 5분 안에 끝났다는 실측은 없다**.
3. **일시정지 결함**: AutoTest pause는 FrameCompare.Pause를 호출해 DOTween.timeScale을 0으로 만든다. 같은 Update의 뒤쪽 DecoBake.Tick은 frozenScale 변경으로 이를 실패 처리하여 표를 버릴 수 있었다. FrameCompare가 소유한 동일 controller·paused·Time=0·Tween=0·audio pause가 온전하고 **정지 전 oldTween이 frozenScale과 같을 때만** 일시정지로 인정한다. 외부 배율 변경·다른 controller·손상된 정지는 기존 거부를 유지한다. 게임에서의 실제 일시정지/export 성공은 미검증이다.
4. **소유권 유지**: export는 실제 논리값·정점·구운 표를 읽는 AUTOTEST 전용이다. 소유권 변경·원본 트윈 종료·Renderer 숨김·매 프레임 전체 스캔/쓰기 훅을 추가하지 않았다. gpudeco on은 실제 데이터 검증 전 계속 거부한다. steps는 export 전용이며 G1 PASS를 출력하지 않는다.

게임 수식은 scrDecoration.UpdatePosition·scrParallax.SetTrans 디컴파일과 기존 tools/ilscan 증거를 읽어 확인했다. game callback의 실제 서명은 이번 FrameCompare fixture도 설치 DLL에서 읽기 전용으로 교차 확인했다. 수식 자체의 실제 게임 충실도는 export가 필요하다.

## GPU에서 확인한 수치

근거: [smoke-r2.txt](../../tools/gpudeco-lab/evidence/smoke-r2.txt), [최종 합성 대조](../../tools/gpudeco-lab/evidence/runs.txt), [번들 대조](../../tools/gpudeco-lab/evidence/bundle.txt).

|항목|실측|
|---|---|
|수정 전 GPU 이징 101,475개|최대 0.00165259838104 / 평균 4.02213904572e-7 / 2e-5 초과 526|
|수정 후 같은 규모|최대 **1.97160989046e-6** / 평균 **2.09635552387e-8** / 초과 **0**|
|합성 정점 8,000개|최대 **0.000477268022223px** / 평균 **0.0000223719397063px** / 0.01px 초과 **0**|
|합성 최종 색 32,000개|최대 **6.55651092529e-7** / 평균 **1.30608677864e-8** / 1/255 초과 **0**|
|1,000개 독립 Raw 메시|복사 8,000정점 불일치 **0**, 단일/분할 RGBA 이미지 차이 **0픽셀 / 최대 0**|
|단일 메시 제출|**0.005729ms**, dispatch 1회|
|1,000메시 제출|**2.723905ms**, dispatch **1,001회**|

이징 오류는 Circ 경계와 Elastic asin 위상 근사였다. 끝점 분기·유리식·precise로 고쳤다. 독립 메시의 초기 빈 화면은 같은 대조군의 UInt32 인덱스를 적용한 뒤 해결됐다. 전체 이력과 16채널별 최대/평균/초과는 랩 결과에 있다. **합성 대조를 실제 Arche G0나 게임 G1로 바꾸어 표시하지 않는다.**

## 설계상 현재 중단 지점

2,000장식·20,000표 구간·1,000개 독립 메시에서는, 한 통합 버퍼의 평가 뒤 미리 기록한 CommandBuffer로 각 메시 Raw 버퍼에 복사한다. CPU 정점 업로드/Transform 쓰기/프레임당 명령 재작성은 없지만, copy dispatch **1,000회**가 추가된다. **제출 2.723905ms는 0.3ms 목표의 약 9.08배**이며 단일 dispatch 조건도 충족하지 않는다. 이 경로를 게임에 그대로 연결하지 않는다.

이는 한 합성 실행의 에디터 제출 수치이며 GPU 실행 시간·실게임 FPS가 아니다. 드라이버 대기/밀어내기가 섞일 수 있고 렌더러 1,000개의 매 프레임 엔진 비용은 포함하지 않는다. 모든 GPU 방식이 불가능하다는 결론도 아니다. 네이티브 D3DCompile 경로는 출력 분배·동기화·수명을 별도 해결해야 하므로 비교만 남기고 구현하지 않았다. 공유 버퍼/커스텀 그리기 전환은 게임 셰이더·재질·정렬 검증을 새로 해야 한다.

**실제 export 0 / 실제 소유 수·비율·prepare_ms·시작 위상·실제 채널 오차·게임 픽셀·FPS 모두 미측정**이다. 실제 export와 비용을 통과하기 전에 게임 연결/소유 복구/G1 steps를 구현한 것으로 표시하지 않는다. 같은 코드를 다시 게임 시험하도록 요청하지 않았다.

## 오프라인 검증

근거는 tools/gpudeco-lab/evidence/round2-*.txt다.

|검사|결과|
|---|---|
|Release Dev / Player / PlayerAuto|각 **오류 0**, 경고 38 / 4 / 38|
|LoadCheck Dev / PlayerAuto|FieldRef 277곳, 불일치 **0**; export는 AUTOTEST만|
|LoadCheck Player|FieldRef 229곳, 불일치 **0**; bake/GPU 타입·명령·export 제외|
|랩 C# / HLSL|오류·경고 **0/0**, 3커널 SM5 컴파일 통과|
|데이터 계약 / DecoSim / 서명|**14 / 12 / 10 통과**|
|bake/live 계약·복구|통과|
|framecmp 수치·마스크·상태·대조 / 빌드·pause 메타데이터 / 설치 게임 서명|**58 / 21 / 2 통과**|
|ld2a 기하·컬링·스크립트·pose·upload|**2729 / 482 / 15 / 40 / 32 통과**|
|ld2a policy·G2 policy / activation / write·queue|**106479·18 / 29 / 157·14 통과**|
|ld2a delta / window|**5,918,737검사·6,000변경 / 58 통과**|
|gpudeco steps / framecmp steps|시점 3곳·open 1회·최대 wait 60·G1 미통과 계약 / **24검사 통과**|

기존 어셈블리 충돌·구식 API·미사용 필드 경고는 남는다. LoadCheck의 동적 참조 5곳은 정적 검사의 한계다. FrameCompareCheck의 첫 --no-restore 호출은 assets 파일 부재로 실패했고, 로컬 SDK 복원 후 최종 검사에서 통과했다. 랩 비용 검사는 대규모 회귀 빌드와 겹치지 않게 수행했다.

task-observer **3.5.0**을 적용했다. 제한된 worktree에서 사용자 범위 관찰·스킬·메모리 파일은 수정하지 않고 이 결과로 전달한다. 관찰: 버퍼 readback 일치만으로 렌더러 출력 성공을 대신하지 않으며, 번들 생성만으로 재로드 성공을 대신하지 않는다.

GPUDECO_G1=ERROR stage=G0-real-export reason=real_export_missing_and_1000_run_submit_over_budget
