# 36개 최적화 감사·진행 기록 (2026-10-16)

기준: codex/shader 2a267f3 / 2.7.3. 작업: codex/optimization-36.
사용자 .codex/config.toml 및 기존 untracked 파일은 보존한다. 새 릴리스 승인은 없다.

## 2.7.3까지 변경

2.7.0: 시작 멈춤·저장 휴식 깜빡임·출력 시간표, 조밀한 광학 흐름·공 합성.
2.7.1: 실제 그림 시각을 감싸는 원본 프레임 쌍 선택.
2.7.2: 제한 없음 FPS 하락 수정. 쌍 선택은 원본 간격 10ms 이상에서만 사용.
2.7.3: 설정 창 한글 복구·정식 ZIP 완주.
사이에 셰이더 20종·공유·탭 변경, 내장 GPU RAM 예산, 필터 항등 검사가 추가됐다.
최근 JSON 읽기·정지 배경 캐싱·camRT·카메라·필터 합치기 조사의 새 성능 수정 채택은 0개다.

## 사전 감사

|번호|상태|근거·차이|
|---|---|---|
|01|부분|GcControl: RAM 40% 제한 있음. 가용 RAM·Working Set 없음|
|02|구현|ImagePrefetch.Predict/ReadDims: 헤더 추정·첫 로드 단계 축소. RGBA 보수 추정|
|03|부분|게임 LoadTexture 최종 Apply(false,true). 요청 모듈에 UploadMeshData 호출 없음|
|04|미구현|FastJsonParser: 짧은 문자열·버퍼 풀 없음|
|05|미구현|ImagePrefetch/ParallelDecode: 메모리 캐시만 있음|
|06|미구현|LoadFix/TransitionFix: 업로드 예산 구간 변경·복원 없음|
|07|부분|MoveApply.SetLocal: 동일 위치 생략. 게임 SetRotation은 별도 함수의 world 회전 쓰기|
|08|미구현|FastMove/MoveApply: 계층 평탄화·Transform Job 없음|
|09|미구현|native/sfnative/sfdxt.ispc: ISPC는 DXT 압축용|
|10|부분|FastMove.ShapeInfo/ZeroTween: 태그 대상 캐시·즉시 이동. 정수 ID·로드 시 병합 없음|
|11|부분|Dormancy/FloorAnim: 휴면 목록·직접 트위너. 컴포넌트 일괄 비활성화 아님|
|12|미구현|PngDecoder/ImagePrefetch: 알파 경계 크롭·피벗 보정 없음|
|13|부분|TileCull/ParticleFix: 고정 8단위 셀·Pause. Catchup 아님|
|14|부분|MatReuse/RecolorSplit: 재질 재사용. 병합·양자화·Property ID 전수화 없음|
|15|부분|LowEnd.AutoTick/Fsr: GPU EMA 85% 자동 해상도. 필터 통합 없음|
|16|부분|HitTextFade/HitMeterFade: 페이드 최적화. 링버퍼·Canvas 분리 없음|
|17|부분|csproj/Edition: 런타임 차단·일부 Compile 제외. 대부분 진단 타입 포함|
|18|부분|UninstallAll/Updater: 개별 정리·DLL 백업. SHA·원자 복구·공통 생명주기 없음|
|19|부분|Compat/ModWatch/LogExport: 패치 감지·끊김 요약. 맵 프로필·타일 히트맵 없음|
|20|부분|LowEnd/PcTune: 프로세스 High·절전 제한 해제. MMCSS·코어 고정 없음|
|21|미구현|native/sfnative: indirect 장식 렌더링 없음|
|22|미구현|PlayTweaks: RawInput QPC 판정 없음. 판정 변경을 별도 검증해야 함|
|23|부분|GcControl: Unity GC 동결. NoGCRegion·아레나 없음|
|24|미구현|SoundWarm: 소리 사전 읽기. DSP·위상 제어 없음|
|25|부분|build-sfnative: SSE2/SSE4.1/AVX2 멀티타깃. 전체 정렬·FP16 없음|
|26|부분|BootConfig/framegen: Flip·원래 스왑체인 연결. latency 1 강제 없음|
|27|부분|PresentWatch/framegen: 생성 GPU 완료 확인. CPU 시작 페이싱 없음|
|28|부분|LoadFix.RetryColliders: 에디터 충돌체만 제거. 물리 전체 중단 없음|
|29|미구현|GcControl/BootConfig: Unity 실제 GC 종류 확인 후 SGen 옵션 지원 판단 필요|
|30|부분|LowEnd: 1ms 타이머. NT 0.5ms 없음|
|31|미구현|native/sfnative: VirtualLock 없음. 관리 힙 전체 영구 고정은 보장 불가|
|32|부분|libdeflate/common_defs: 라이브러리 prefetch만. 자체 GPU NT store 없음|
|33|부분|ShaderWarm.WarmFilters: 이미 16×16 RT 실제 필터 패스 예열. 모든 키워드 변형은 아님|
|34|미구현|LowEnd/native: CPU 토폴로지·인터럽트 분포 미측정|
|35|부분|framegen: 그림·쿼리 링. 동적 NO_OVERWRITE 버퍼 링 없음|
|36|미구현|ParallelDecode/FastMove: 관리 객체 조회. 관리 객체 주소만 IntPtr에 보관하면 GC 안전하지 않음|

## 실행 상태

새 기능은 기본 꺼짐이며 기존 설정 값을 유지한다. 성능 채택과 후보 구현·기능 검증을 구분한다.

|번호|진행|확인|
|---|---|---|
|01|동적 예산 후보 구현|정책 55조건·두 Release 빌드 오류 0. 실제 게임 API 동작 확인|
|02|기존 구현 확인·중복 변경 0|두 Release 빌드 오류 0. RGBA 4B 보수 추정 유지|
|03|기존 CPU 픽셀 해제 확인·중복 변경 0|게임 IL·두 Release 빌드 오류 0. 게임 소유 메시 강제 변경 0|
|04|해석 단위 문자열 풀·ArrayPool 후보 구현|20,017조건 오류 0. 반복 이벤트 할당 7,107,400 → 4,611,752바이트|
|05|DXT·JSON DOM 영구 캐시 후보 구현|22조건 오류 0·두 Release 빌드 오류 0·실제 첫 로드/재방문 정상 종료|
|06|구간 업로드 예산 후보 구현|복원 9조건 오류 0·두 Release 빌드 오류 0. 실제 16ms/64MB 적용 → 2ms/4MB 복원|
|07|미세 위치 쓰기 생략 후보 구현|100,006조건·두 Release 오류0. 호출 통합·실제 화면/성능 미확인|
|08|실제 계층 추가 감사·Job 갱신 미구현|Arche현재 장식28,835개 부모 깊이1·타일/장식 조상0. 이 표본은 이미 평평하며 더 평탄화0. 시차/월드 읽기 의존은 유지|
|09|직선·스칼라 SIMD 후보 구현|네이티브 빌드·두 Release 오류0. 실제 Mono 1,048,576개 다름0. 다른 이징 미구현|
|10|정수 태그 색인 후보 구현·검증 중|순서/중복/null/변경 목록 6조건 오류0. 로딩 시 이벤트 제거 미구현|
|11|기존 휴면 목록·직접 트위너 유지|현재 준비된 Arche의 활성124형식 중 완전히 빈 Update0. 추가 비활성화0·모든 클로저 제거 미구현|
|12|SIMD 경계 탐색·피벗 보정 연구 구현, 크롭 채택 불가|독립23,080조건 오류0. 실제 GPU16조건 중12조건에서 합계171픽셀 차이. 일반 managed 경로 제외·원본 텍스처 변경0|
|13|입자 PauseAndCatchup 기본 꺼짐 후보·격자 범위 overflow 방지|두 Release 오류0. 실제 적용 대상0이라 입자 복귀 품질/비용 미확인|
|14|문자열 재질 호출45곳을 런타임 ID30개로 전환|두 Release 오류0. 정적 메시 병합·색 양자화 미구현|
|15|기존 DRS GPU 예산80%·계측 실패 폴백·Recorder 수명 정리|정책12조건/두 Release 오류0. 실제 GPU응답·게임 필터 통합 미확인|
|16|실제 풀 구조 추가 감사·구현 미완료|Arche준비 표본 판정 글자1,400개·활성0·문구 미준비. 7종/16슬롯 교체·Canvas 재부모화0|
|17|순수 진단36타입 일반 Player 컴파일 제외|세 빌드 오류0·필수 런타임6타입 유지·일반 DLL 시작/정상 종료 확인. 전체 핫패치 병합 미구현|
|18|SHA-256·설치 트랜잭션·신규 모듈 수명 공통화|정책25조건·실제 엔진 파일4조건/수명3조건 오류0. 기존 전체 모듈 이관 미완료|
|19|맵별 화면 프로필·상위5타일·Markdown 복사 후보|프로필13조건/타일 집계6조건·맵 전환6상태 오류0. 두 곡 포탈 도착/히트맵 완료2/2, 빈 다음 판 복구8조건0, 클립보드 API 대조/복원1/1. 물리 UI 클릭은 확인 안 됨|
|20|메인 우선순위/MMCSS·렌더 우선순위/코어 후보|렌더 핸들/복원539조건·수명10조건0, 실제 렌더 mask3F→3E→3F/priority0→15→0. 렌더 MMCSS·FPS 개선은 확인 안 됨|
|21|재질 공유/COW 연구 구현, 일반 채택 불가|동률 반투명 겹침36조건 중30조건 픽셀 불일치. 일반 Player 선택/설치 차단. 아래18재질·단일 장면0차이는 통과 근거 아님|
|22|기본 꺼짐 WM_INPUT 프레임 대기 후보|소프트웨어 수신→폴링만 보정. 기존 비동기 입력에는 추가 보정0. 실제 물리 키 판정은 확인 안 됨|
|23|숫자 계산 비관리 영역 후보·NoGCRegion 지원 확인|영역13조건·실제 Mono1048576개 차이0/중간 재사용. NoGCRegion 미지원·전체 맵 버퍼 전환 미구현|
|24|오디오 API·생명주기 검토, 구현 미완료|64/128 강제·위상 변경0. 오디오 리셋의 클립/재생 상태 복구 미검증|
|25|숫자 버퍼64정렬·선택적AVX-512 타깃 후보|네이티브/두 Release 오류0. 오프라인4194304개/실제Mono1048576개 차이0. AVX-512 하드웨어·FP16 미검증|
|26|기존 Flip 옵션 유지·강제 변경 미구현|모드 로드 전 만들어진 원래 swapchain의 SwapEffect를 ResizeBuffers로 바꿀 수 없음. 지연1 재도입0|
|27|CPU/GPU 대기 경계 검토, 구현 미완료|생성 worker 완료 query와 CPU 프레임 시작 fence는 별개. 새 CPU 대기0·queue0 보장0|
|28|실제 물리 사용 감사·전역 중단 미구현|히트박스 Collider2D.Overlap/타 모드 FixedUpdate 보존. fixedDeltaTime100 적용0|
|29|현재 엔진 비호환 확인·적용0|실제 bdwgc·NoGCRegion 미지원. SGen nursery128MB 런처를 작동하는 Unity 기능으로 만들지 않음|
|30|NT 타이머 현재 범위 읽기·강제 요청 미구현|지원 실패는 확인 안 됨. 기존1ms 요청 유지·새 NtSet 요청0|
|31|자기 소유1.25MiB만 선택적 페이지 고정|정책16조건·실제 Windows 고정/원복·Mono 계산1,048,576개 차이0. 관리 힙 전체 고정/페이지 폴트 감소는 확인 안 됨|
|32|실제 쓰기/읽기 경로 감사·NT store 적용 보류|숫자 출력은 바로 CPU가 읽고 GPU 쪽은 DEFAULT 상수 버퍼 UpdateSubresource다. 자체 CPU-write staging 스트리밍 경로0. 성능 개선 확인 안 됨|
|33|기존 예열의 실패 재시도·자원 복구 수정|독립 소유권/실패 주입56조건 오류0·두 Release 빌드 오류0. 전체 키워드/드라이버 변형·실제 GPU 성능은 확인 안 됨|
|34|0번 논리 코어 제외 기본 꺼짐 후보|순수 정책85조건·자기 워커 Windows 적용/복구6조건 오류0. 게임 IRQ 분포/성능·렌더 스레드 적용 확인 안 됨|
|35|보간 소유 상수 버퍼3칸 후보 구현·기능 검증|완료 event 후 NO_OVERWRITE·기존 경로 폴백.60조건14,745,600픽셀 추가 차이0. FPS차이−0.8214~+3.6431%·대조변동4.5015%라 성능 채택0|
|36|정수 태그 계획만 비관리 영역 후보 구현|독립10,028조건 오류0. 실제 두 맵105,797번호 대조 불일치0·해제/오래된 계획 차단2/2. Unity 객체 참조 유지·GC/FPS 이득 확인 안 됨|

05 실제 HELLO 두 새 실행: 첫 로드 DXT 저장 215·JSON 저장 1, 재방문 DXT 적중 84·JSON 적중 1.
512MB LRU라 전체 이미지가 상주한다는 뜻은 아니다. 양 판 SF 오류 0·정상 종료, 설치 3파일 원본 복원.
JSON 스냅샷은 순수 DOM이며 Unity 객체가 들어갈 수 있는 LevelEvent 디코딩 자체를 생략하지 않는다.
실제 로딩 시간·FPS 개선·OOM 방지 효과·전체 픽셀 동일성·모든 OS/GPU 검증은 확인 안 됨이다.
기준/번호별 빌드와 비공개 로그는 tools/optimization/out, 공개 요약은 tools/optimization/validation.

한글은 PowerShell 기본 ASCII stdin으로 한 차례 손상돼 복구했다. 파이프 입력은 UTF-8을 명시한다.
tools/optimization/check-text.ps1이 UTF-8 오류·대체 문자·물음표 연속 문자열을 차단한다.
안전 조건: 손상 캐시·권한 실패는 원래 길로 돌아간다. epsilon·양자화·크롭·물리·판정·오디오·저정밀도는 별도 동일성 검증이 필요하다.
GC·Working Set·Flip·타이머·코어 고정만으로 OOM·페이지 폴트·지연 0을 보장하지 않는다.

2026-10-16 사용자 확인에 따라 이후 대상은 현재 설치된 Unity 6000.3.21f1 알파다.
기존 6000.3.10f1 호환성은 이번 실행에서 확인 안 됨이다.
06 실제 메뉴 시험: 신규 설정 기본 꺼짐 4/4, 캐시 카운터 0, SF 오류 0, 정상 종료 1/1.
앞선 로딩 시험은 적용/복원 완료 후 도구의 숫자 로그 검사에서 실패했다. 게임 크래시로 세지 않는다.
설치 DLL/native/Settings 원본 SHA-256 3/3 복원했다. 설정 창 한글·잘림 경고 0을 확인했다.

07: UpdatePosition은 local 위치 쓰기 → 시차가 같은 Transform의 world 위치 읽기/쓰기 → world 회전 쓰기 순서다.
SetLocalPositionAndRotation으로 치환하는 것은 동등하지 않다. 추가 후보는 실제 마지막 위치와 축별 1e-6 미만 비교뿐이며 기본 false다.
08: 부모 타일 이동·카메라 시차·회전 고정·피벗 및 다른 모드가 읽는 Transform 계층을 보존해야 한다.
현재 경로는 순수 최종 포즈 표가 없으므로 TransformAccessArray에 바로 넣을 독립 입력이 없다.
부모를 없앤 뒤 같은 프레임 보정값을 다시 계산하는 방식의 이득·동일성은 확인 안 됨. 평탄화 완료로 세지 않는다.

09: 직선 이징의 스칼라 좌표·회전·불투명도만 SoA 5배열/SSE2·SSE4.1·AVX2 자동 선택으로 계산한다.
512개 미만·다른 이징·최초 초기화·바뀐 애니메이션은 기존 계산이다. 색·크기·시차 벡터는 이번 후보 대상이 아니다.
.NET8 엄격 float 시험 2,097,152개 다름0 뒤 실제 Mono 1,048,576개에서 250,471개가 달랐다.
게임 Mono의 중간 곱/합 double 반올림을 명시한 뒤, 실제 같은 입력 1,048,576개 다름0.
새 네이티브 두 반올림 경로는 오프라인 합계4,194,304개 다름0. SSE2/SSE4.1 각각 강제 실행은 확인 안 됨.
실행 시 엔진 이징과 1,024개 대조해 맞는 반올림 경로만 고르고, 불일치/ABI 부재는 기존 계산으로 돌아간다.
선택 과정은 처음 1회이며 매 장식 Stopwatch 추가0. SIMD 버퍼는 기본 꺼짐이며 켰을 때만 할당한다.
HELLO/Arche 50초 개발자 판의 실제 SIMD 묶음0/0. HELLO 미세 위치 생략58, Arche0.
첫 HELLO의 마지막 편집기용 stop 명령은 게임 화면에서 실패했다. 정상 종료/복원 후 로그를 보존했고 기능 통과로 세지 않는다.
Arche 집계 오류1은 UI 측정의 Error Meter 오브젝트 이름 오인이다. 오류 분류기를 수정하고 6조건 대조했다.
원시 로그는 변경하지 않았다. 수정 분류로 SF 오류0·정상 종료1/1이며, 실제 SIMD 사용0을 통과로 확대하지 않는다.
FPS 개선·전체 움직임 동일성은 확인 안 됨.

10: 준비 시 정수 태그표, 효과별 번호 계획, 원래 List 참조와 순서/중복 보존. FastMove.Take에만 적용한다.
Dictionary 값 교체/삭제/Clear 및 List 원소 교체의 버전 변화를 실제 런타임에서 확인한 경우만 켠다.
태그표 변경 시 그 판의 색인을 버리고 기존 조회로 돌아간다. 설정·Unload·새 맵에서 참조를 놓는다.
효과별 번호 계획은 FinishCustomLevelLoading 끝에서 준비한다. 빠진 효과·변경된 태그 목록은 첫 사용에 보완한다.
현재 Arche 준비 태그32,035·효과97,694, 재생35초 조회229·추가 계획0·표 무효화0·SF오류0·정상종료1/1.
전체 효과 사전 컴파일 보장이나 duration0 이벤트 병합 완료가 아니다. 성능 손익은 확인 안 됨.
duration0이라도 이전 Tween 완료 콜백·다른 모드의 중간 읽기·히트박스가 관찰할 수 있어 일괄 삭제를 넣지 않았다.
11: 장식 갱신은 관리자에서 LogicUpdate를 직접 부르므로 Behaviour.enabled만 끄면 그 순회가 사라지는 구조가 아니다.
기존 Dormancy/DecoAnim/FloorAnim은 유지하고, 원래 DOTween으로 돌아가는 지원 밖 경로와 개발자 Shadow의 클로저를 제거했다고 하지 않는다.
12: 같은 그림이 여러 Sprite·필터·이미지 변경 효과에서 공유된다. 텍스처 크기만 줄이면 originalSize·UV·pivot·마스크 경계가 바뀐다.
현재 게임의 CustomSprite 생성 경로를 확인했으며, 전체 보정과 그림 대조가 없는 자동 크롭은 미구현으로 남긴다.
13: 기존 TileCull은 카메라 orthographicSize로 화면 범위를 매번 조절하고 너무 큰 범위는 기존 그리기로 돌아간다.
줌마다 전체 격자를 다시 만드는 변경은 넣지 않았다. 1px 장식 제거와 격자 크기 자동 조절은 미구현이다.
PauseAndCatchup 후보는 기존 화면 밖 입자 멈춤 설정과 함께 켰을 때만 적용하고 원래 모드를 복원한다.
다른 모드가 바꾼 모드는 덮어 복구하지 않는다. Arche 상태3회 모두 owned0이라 복귀 효과/성능 확인 안 됨.
14: FastBlend/FrameScan/Fsr/LowEnd/ScreenEffects의 literal 재질 호출45곳을 같은 문자열의 Shader.PropertyToID 캐시로 바꿨다.
ID는 실행마다 계산하며 직렬화하지 않는다. 페이드 양자화는 값 변경이며 정적 병합은 sorting/mask/필터 경계를 보존해야 하므로 미구현이다.
실제 엔진 ID30/다름0, GPU 화면 효과 fixture 꺼짐57,600픽셀/다름0·원본변경0, 무변환LUT 최대 바이트오차0.
15: 기존 자동 해상도 토글을 유지하고 GPU EMA80% 초과 때10% 하향,55% 미만 때 상향·기존 쿨다운 유지.
GPU Recorder가 없거나 예외를 내면 해당 재생에서 재설치를 반복하지 않고 기존 해상도를 유지한다. 끄기/곡 끝/Unload에 Dispose한다.
게임 필터는 서로 다른 샘플 위치/임시 RT/재질 상태를 쓰므로 임의 Uber 패스로 동일하다고 하지 않는다. 기존 자체 색감 통합 패스는 유지한다.
16: 게임 판정 풀의 폰트·언어·스타일·동시 활성 수를 확인하지 않고16개로 덮으면 글자가 사라질 수 있다.
고정 그림 굽기와 Canvas 분리는 실제 UI 픽셀/판정·타 모드 Canvas 순서 검증이 남아 있으며 구현 완료로 세지 않는다.
17: 순수 진단21타입과 새 UpdateFixture를 MSBuild 조건으로 일반 Player에서 제외한다. Developer/PlayerAuto에는 포함한다.
모니터의 PresentWatch/GfxProbe 등 런타임6타입은 유지한다. 타입 이름만 보고 필요한 기능까지 지우지 않는다.
진단 호출36곳은 조건부 컴파일이며 테스트 메서드3개도 일반 Player에 없다. FSR/저사양 요약과 VRAM 오류 보고는 유지한다.
EffectScan은 EffectBudget을 구동하는 기능 패치라 제외하지 않았다. 의미가 다른 Prefix/Postfix를 일괄 합쳤다고 하지 않는다.
18: GitHub 해당 자산의 digest와 ZIP SHA-256을 비교하고, 누락/불일치는 자동 설치를 하지 않는다.
임시 DLL/Info 바이트 해시·관리 어셈블리 이름을 확인한 뒤 원자 교체/백업하고, 중간 오류/취소는 둘 다 복원한다.
참고: [GitHub release asset digest](https://docs.github.com/en/rest/releases/assets?apiVersion=2022-11-28).
가짜 저장소25조건 오류0(DLL/Info 교체 뒤 오류 포함), 실제 Mono의 격리 폴더에서 해시 거부/설치·백업/교체 뒤 오류 복구/임시 파일 정리4조건 오류0.
실제 배포 파일·릴리스 변경0, 테스트 ZIP은 메모리에만 만들었다. 전원 손실 중 복구와 실제 인터넷 자동 업데이트는 확인 안 됨.
IOptimizationModule의 독립 Harmony/예외 격리/역순 해제를 새 태그 모듈에 적용했다. 기존 모든 모듈을 이관한 것은 아니다.
끄기→켜기에서 새 후보 설정을 다시 적용하며 실제 소유자1→0→1/Native활성 true→false→true를 확인했다.
15 실제 HELLO12초 GPU Recorder 유효true·EMA2.917483ms, 끈 뒤 핸들 유효false/EMA0. FPS 이득/80% 전환 응답은 확인 안 됨.
18 시험 뒤 설치3파일·boot/백업2파일 원본 바이트5/5를 복원하고 정상 종료했다.
공개 validation 로그는 원시 출력 내용을 유지하며 UTF-8로 변환했다. 원시 인코딩/실행 로그는 out에 보존했다.
settings-experiments-3440.png는 새 설정 창/시작 화면 표본이며 곡 성능·움직임 검증이 아니다.

19 (2026-10-17): 맵 내용 SHA-256별 색감/해상도/프레임 생성 값만 저장한다. 입력/전체 화면/OS 옵션은 전역이다.
별도 파일의 해시/맵 키/형식/128KiB 상한을 검사하고 손상 시 검증된 백업 또는 전역 설정을 쓴다.
저장할 때 잠시 전역 값을 직렬화한 뒤 런타임의 맵별 값을 finally로 복구한다. 새 파일은 원자 교체한다.
오프라인13조건 오류0. 실제 HELLO→Arche→HELLO 값0.11→0.75 /0.11→0.25 /0.75, 끈 뒤0.11, 전역XML0.11·프로필2개.
사전 시험은 FinishCustomLevelLoading이 맵 열기 때 불릴 것으로 가정해 적용0이었다. 제외 JSON/원시 로그를 보존했다.
실제 호출은 재생 시작 경로였다. scnGame.LoadLevel 성공 직후로 바꾼 뒤6상태 확인했다. FPS/파일 변경 중 경쟁 검증은 확인 안 됨.
타일 히트맵은 기존 모니터가 분류한 순간 끊김만 기록한다. 최대1024타일/Top5, 순위는 최악 시간이며 동일 시간은 타일 번호순이다.
6조건 오류0. 타일 번호는 끊김 감지 시점의 currFloor.seqID이며 원인 타일을 증명하지 않는다. 포탈 도착과 중도 기록을 구분한다.
일시정지에서 지우지 않고 ResetScene에서 다음 시도를 준비한다. 매 프레임 새 Stopwatch/목록 정렬0, 기본 꺼짐이다.
완주 이벤트·실제 끊김 타일의 정확도·모니터끔 비용·설정 버튼 클릭은 확인 안 됨이다.
기존 Harmony 공유 패치 확인은 곡 밖에서만1회 수행하도록 옮겼다. 같은 함수의 공유는 충돌 확정이 아니다.
Discord 요약은 사용자 버튼으로 클립보드에만 복사하며 네트워크 전송0. 개인정보 마스킹은 기존 경로를 재사용한다.

20: 메인 스레드 예약은0끔/1TIME_CRITICAL/2MMCSS Pro Audio 중 하나다. 기본0, P코어 제한도 기본false.
곡 밖/끄기/Unload에 자기 스레드의 우선순위/코어마스크/MMCSS를 복구한다. 다른 코드가 바꾼 우선순위/마스크는 덮지 않는다.
지원 API 실패는 기존 스케줄링으로 돌아가고, 복구 실패는1초 간격으로만 재시도한다. 레지스트리/권한/OS 설정 변경0.
CPU_SET_INFORMATION의 레코드 Size·프로세스/기존 스레드 마스크·할당/주차 상태·단일 프로세서 그룹을 검사한다.
EfficiencyClass 구분이 없으면 코어 제한0. CPU0 제외나 특정 CPU 모델의 P/E 번호 추정0.
공식 규약: [MMCSS](https://learn.microsoft.com/en-us/windows/win32/api/avrt/nf-avrt-avsetmmthreadcharacteristicsw), [CPU 집합](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-system_cpu_set_information).
정책11조건 오류0. 별도 짧은 워커의TIME_CRITICAL 설정/복원True, MMCSS 설정/복원True.
실제 HELLO12초 메인 MMCSS=True, P코어 homogeneous-or-unknown, 옵션 해제 뒤꺼짐·SF오류0·정상 종료1/1·설치5파일 복원.
게임 메인의TIME_CRITICAL·실제 이종 CPU·렌더 스레드·입력/오디오 경쟁·FPS/최악 프레임 개선은 확인 안 됨이다.

21: 현재 scrDecoration은 Transform·SpriteRenderer·Collider2D.Overlap을 함께 쓴다. GameObject를 없애면 그리기 외 게임 기능도 대체해야 한다.
DrawMeshInstancedIndirect는 호출에 지정한 Mesh/Material을 공유하므로, 서로 다른 그림/블렌드/정렬/마스크를 현재 재질 그대로 한 번에 묶는 변경은 아니다.
[Unity API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Graphics.DrawMeshInstancedIndirect.html).
연구용 indirectaudit은 실제 맵의 공유 재질·그림·정렬 키를 한 번만 센다. 일반 Player에는 포함0, 렌더러 교체0.
실제 Arche: 장식28,835·SpriteRenderer86,484·공유 재질28,829·텍스처218·정렬 키554·마스크0·누락 재질0.
Instancing/Compute 지원True/True·SF오류0·정상 종료·설치5파일 복원. 서로 다른 재질을 동일한 것으로 가정하지 않는다.
GPU 렌더러/ComputeShader/텍스처 atlas/동적 정렬/충돌체 대체·동일 픽셀·성능은 미구현/확인 안 됨이다.

22: [RAWKEYBOARD](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawkeyboard)의 필드는 스캔 코드/상태/가상 키/메시지/추가 정보이며 표준 하드웨어 QPC 시각 필드는 없다.
WM_INPUT을 받은 스레드의 QPC는 소프트웨어 수신 시각이다. 폴링1000Hz와 키가 눌린 시각의 정확도는 같지 않다.
현재 판정·오디오 오프셋·다른 모드의 입력 제한에 연결한 역산 동일성 검증이 없어 실제 판정은 변경0.
RawInput 수신/링버퍼·장치 시각 보정·새 판정 경로는 미구현이다. 서브밀리초 입력/입력→광자 개선은 확인 안 됨이다.

23: 현재 Unity Mono는 mono-2.0-bdwgc.dll이며 실제 TryStartNoGCRegion(16MiB)은 NotImplementedException이다.
지원 확인에서 잡은 예외1건을 일반 오류 정규식이 오인했다. 원시 로그를 보존하고 해당 정확한 지원 확인 줄만 별도 구분해 재집계했다. 재실행0.
기존 GcControl의 Unity GCMode 제어를 유지하며 SGen/너서리 옵션을 작동하는 기능으로 표시하지 않는다.
[Unity Mono GC](https://docs.unity.com/en-us/engine/6000.3/manual/scripting/compilation-and-code-reload/script-compilation/backends/mono).
NativeMotion의5개 스칼라 버퍼만 64바이트 정렬 선형 영역1,310,720바이트로 선택 전환한다. 옵션은 기본false.
할당 실패는 기존 관리 배열, 범위 초과는 거부, ResetScene에서 커서 재사용·Unload에서 정확히 해제한다. Unity 객체/LevelEvent를 주소만 저장하지 않는다.
오프라인 정렬/경계/재사용/해제13조건 오류0. 실제5열 주소alignment OR0, Mono1048576계산 차이0,8번째 묶음 뒤 영역 재사용 포함.
옵션 해제 뒤activeFalse, SF오류0(예상된 지원 예외 별도1)·정상 종료·설치5파일 복원. 전체 임시 버퍼 전환·GC/FPS 이득은 확인 안 됨.

24: AudioSettings.Reset은 지원하는 가장 가까운 설정을 쓰며64/128을 보장하지 않는다. 전체 오디오 객체/재생 상태를 재구성하고 스크립트 생성 클립은 재생성해야 한다.
로드 중 main thread stall 가능성이 명시되어 있다. [Reset](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AudioSettings.Reset.html), [오디오 설정 수명](https://docs.unity3d.com/6000.3/Documentation/Manual/class-AudioSettings.html).
SoundWarm의 사전 읽기와 판정 타이머를 보존하고, 복구하지 못할 클립/박자 상태를 try-catch만으로 안전하다고 하지 않는다.
개발자 명령은 현재 DSP/버퍼 수/샘플레이트만 읽는다. 오디오 Reset/위상 보정0, 실제 오디오 지연·64/128 동작·위상 잠금은 확인 안 됨이다.
실제 메뉴:48,000Hz·DSP1,024샘플·4버퍼·real255/virtual4095·로드 클립79. SF오류0/정상 종료/설치5파일 복원. 이것은 스피커까지의 지연 측정이 아니다.

25: NativeMotion 기본 꺼짐 경로에만 SSE2/SSE4.1/AVX2/AVX512SKX 선택 빌드를 추가했다. 이미지 DXT의 타깃은 이전3개 그대로다.
자기 소유5열 SoA만 _aligned_malloc(64)/_aligned_free로 할당한다. 없거나 실패하면 이전64정렬 관리 래퍼 할당으로 돌아간다.
양쪽 새 export 존재를 확인한 뒤 사용하며, 기존 이미지/게임/드라이버 소유 메모리를 새 free에 넘기지 않는다.
처음 실제 시작에서 ApplyConfig가 SfNative.Init보다 먼저여서 정렬 래퍼 폴백이었다. 폴백 실행도1048576개 차이0이지만 native_allocatorFalse였다.
선택적 영역을 만들기 전에 기존 네이티브 초기화를 수행하도록 의존 순서를 고친 뒤, 실제 native_allocatorTrue·5열 정렬OR0·1048576개 차이0·해제activeFalse를 확인했다.
사전 폴백 JSON은 별도 보존했다. 두 시험 SF오류0·정상 종료/원본5파일 복원. 아레나 helper16조건·실제 native할당10조건 오류0.
변경 DLL의 오프라인 두 반올림 경로4,194,304개 차이0·기존 이미지600조건 차이0(현재 CPU 선택 경로).
AVX-512 CPU 실제 실행·SSE2/SSE4.1 강제·전체 네이티브 버퍼 전환·FP16·게임 FPS 이득은 확인 안 됨/미구현이다.
FP16은 원래 float 값과 같지 않고 AVX512SKX 지원이 AVX512-FP16 지원을 뜻하지 않으므로 정밀도 변경을 자동 적용하지 않는다.
네이티브 빌드 성공은cmd 자체 종료0으로 확인했다. 첫 PowerShell stderr의 ISPC 경고 ErrorRecord와 잘못된 cmd 인용 실행은 원시 로그로 보존했다.

26: 모드 시작 때 게임 D3D11 장치는 이미 만들어져 있다. 현재 연결은 그 원래 SwapChain의 Present를 보존한다.
ResizeBuffers 인자는 개수/크기/형식/flags이며 SwapEffect 변경 인자가 없다. FLIP_DISCARD 강제는 생성 전 엔진 설정 또는 소유 체인의 재생성이 필요하다.
기존 BootConfig Flip 옵션/다음 실행 적용을 유지한다. Unity 엔진의 원래 swapchain 포인터 교체/두 번째 출력 체인 생성0.
[ResizeBuffers](https://learn.microsoft.com/en-us/windows/win32/api/dxgi/nf-dxgi-idxgiswapchain-resizebuffers), [DXGI Flip Model](https://devblogs.microsoft.com/directx/dxgi-flip-model/).
Flip이 DirectFlip/MPO/DWM 우회100%를 보장하지 않으며 창·다른 오버레이·드라이버 조건에 따른다.
기존 maxQueuedFrames2→1의209→128FPS/시작→표시15.8→19.1ms 악화 기록을 유지한다. 새 SetMaximumFrameLatency(1) 강제·이득 측정0.

27: 기존 native의 생성 그림 GPU 완료 query를 메인 스레드에서 직접 기다리는 것은 Unity 프레임 끝 fence와 같지 않다.
같은 immediate context의 렌더 스레드/생성 worker/CPU 대기 수명과 장치 재생성에 따른 취소를 함께 보존해야 한다.
SystemMonitor GPU3D는 다른 프로세스가 포함된 사용률이고 PresentWatch 대기 시간도 GPU 작업량95%의 증명이 아니다.
장치 안전한 프레임 끝 fence/CPU 시작 페이싱·GPU95% 부하 A/B·queue depth0·지연 개선은 미구현/확인 안 됨이다.
새 기다림·Flush·잠금 확장0. 단순 폴링이나 일정 Sleep을 queue0 최적화로 추가하지 않았다.

28: scrDecoration의 히트박스 경로는 Collider2D.Overlap을 호출한다. Script 모드와 FixedUpdate 중단은 같은 기능이 아니다.
Time.fixedDeltaTime100은 다른 모드와 게임의 FixedUpdate 호출에도 영향을 주며, 물리 Transform 동기화를 없애도 안전하다는 근거가 없다.
연구 명령은 활성 Rigidbody/Collider/FixedUpdate 대상·현재 모드만 센다. 물리/고정 시간 값 변경0.
물리100% 중단·히트박스 동일성·고정 갱신 제거 효과는 미구현/확인 안 됨이다.

29: 23번 실제 런타임 확인과 공식 Unity Mono 문서에 따라 현재 수집기는 Boehm이며 SGen이 아니다.
MONO_GC_PARAMS의SGen nursery-size128m는 현재 게임의 Gen0 너서리 크기를 바꾸는 지원 경로가 아니다.
환경 변수/Steam 실행 옵션/런처 변경0. SGen 런타임용 별도 도구도 현재 게임 최적화로 포장하지 않았다.
Minor GC 차단/128MB 효과/다른 Unity 버전 호환성은 확인 안 됨이다. 기존 Unity GC 제어는 유지한다.

30: NtQueryTimerResolution을 명시적 연구 명령에서만 호출한다. 없거나 실패하면 확인 안 됨이며 일반 Player에는 타입 자체를 포함하지 않는다.
반환 단위100ns를 그대로 적고, 최소 지원 간격과 현재 간격·게임 프레임 시작 정확도를 구분한다.
새 NtSetTimerResolution 요청은 하지 않았다. 기존 엔진/WinMM/다른 모드의 같은 프로세스 요청 수명을 확인하지 않고 FALSE를 호출해 모두 원복된다고 가정하지 않는다.
기존 LowNoThrottle의 timeBeginPeriod(1)/timeEndPeriod(1) 쌍은 유지한다. 새0.5ms 강제·소유 요청 복구·끊김/FPS 효과는 미구현/확인 안 됨이다.

## 2026-10-17 후속: 28·30·31 실제 지원 확인

28: HELLO10초 Rigidbody2D/3D0/0, Collider2D14,769, 활성 FixedUpdate3(DOTween/MonsterLove.StateEngine/UMM.UI 각1).
fixedDeltaTime0.0199999921, Physics2D FixedUpdate/Physics3D Script, autoSync2D true. 변경0·SF집계오류0·정상 종료/설치5파일 복원.
30: NtQueryTimerResolution의100ns 단위 값은 coarsest156250/finest5000/current4959. 새 요청0. 실제 프레임 시작 정확도는 확인 안 됨.
31: 관리 힙을 잠그지 않고 NativeMotion의 별도 VirtualAlloc 페이지1,310,720바이트만 고정한다. 기본false, 여유512MiB+요청4배 미만이면 이전 할당.
작업 집합을 소유한 값으로 변경하고 VirtualLock 성공 시에만 사용한다. 끄기/Unload에 VirtualUnlock/VirtualFree 후 자신이 설정한 작업 집합만 되돌린다.
다른 코드가 변경한 값은 덮지 않고 해제/복구 실패는 소유 기록을 남겨 재시도한다. 영구 RAM 고정이나 전체 페이지 폴트 제거 기능이 아니다.
정책/실패 복구16조건 오류0. 실제 Windows min/max/flags204800/1413120/0→1581056/1646592/1→204800/1413120/0.
pinned true→false, 5열64정렬OR0, 실제 Mono 계산1,048,576개 차이0, SF집계오류0·정상 종료·설치5파일 복원.
종료 뒤 Quartz Discord RPC의 ThreadAbort 경고1건은 원시 로그에 보존했다. 전체 프로세스 예외0이라고 하지 않는다.
자료: validation/28-physics-game.json, 30-timer-game.json, 31-page-lock-check.txt, 31-pinned-game.json.

## 2026-10-17 후속: 21 대체·보조 렌더러·22 보정

**채택 결과: 재질 공유는 불합격이다.** 후속 동률 정렬 GPU fixture에서36조건 중30조건이 원본과 달랐다.
일반 Player에서는 실제 공유 구현을 컴파일하지 않고 거부하는 stub만 둔다. 설정 행도 숨기며 수동 XML true로도 설치되지 않는다.
아래18재질/선택 장면0픽셀은 연구 자료이며 정식 무손실 통합 성공을 뜻하지 않는다. 동률 제외 설계 또는 보류 여부를 사용자에게 질문했다.

실제 화면은2560x1440·120Hz다. 이번 검증을3440 시험으로 대체하지 않는다.

|Arche 구조|수|
|---|---:|
|장식|28,835|
|서로 다른 SpriteRenderer|86,484|
|주 그림|28,828|
|선택 테두리 / 히트박스 표시|28,828 /28,828|
|각 보조 역할의 activeInHierarchy / 실제 visible|0 /0|
|중복 순회 / 추가 비활성화 / 충돌체 변경|0 /0 /0|
|원본 재질 / CRC 후보 묶음|28,829 /19|

GameObject 비활성은 이미 컴포넌트를 비활성화한다([Unity](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/GameObject.SetActive.html)).
enabled=true라는 필드만 보고 이미 비활성인 보조 렌더러를 비용으로 합산하지 않는다. 실제 엔진 컬링 시간/그리기 호출 수는 확인 안 됨이다.

재질 공유는 기본false이며 새 맵 준비부터 적용한다. 생성/복제 출처가 같은 기본 Sprites/Tile 셰이더만 다룬다.
다른 템플릿은 읽을 수 없는 태그/내부 상태가 같다고 가정하지 않아 서로 합치지 않는다. 템플릿 계보와5개 속성의 float 비트·텍스처/UV·키워드·패스·큐·GI/instancing 상태로 비교한다.
CRC는 감사용 후보 수일 뿐 실제 공유의 동일성 키로 사용하지 않는다. 알 수 없는 태그/버퍼/배열 쓰기는 대상에서 제외하며 마스크/네이티브 애니메이션/다중 재질 렌더러도 제외한다.
공개 Material 수정·Renderer 재질 읽기/쓰기 경로를 보호한다. 캐시된 원본 수정 전에 그 원본을 쓰던 렌더러를 복구하며, 읽기에서도 먼저 분리해 개별 참조를 보존한다.
Unity 내부의 get_material 복제는 공개 생성자를 거치지 않으므로, 원래 getter 실행/실제 렌더러 참조/중간 setter 변경 여부를 확인해 같은 템플릿 계보를 전달한다.
보호할 수 없는 API가 있으면 설치 전체를 되돌린다. 새 MinHook/네이티브 그리기 가로채기0. 끄기/Unload/맵 전환 때 원본 참조로 복구하고 자기 복제 재질만 지운다.
캐시 참조를 보존하려고 원본 재질도 유지한다. 렌더러가 쓰는 공유 수 감소이며 전체 Material RAM 감소를 뜻하지 않는다.
뜨거운 SetFloat/SetColor 경로는 typed Prefix라 __args 배열/인자 boxing0. 초기 묶기는 준비 단계로 옮기고 게임 중 설치 요청은 곡 밖까지 미룬다.

초기 LoadLevel 훅의 실제 대상0 판은 제외 자료21b-cow-game.json에 보존했다. FinishCustomLevelLoading 이후에는28,828개를 찾았다.
다음 사전 판은 공개 생성자만 추적해 Unity 내부 복제의 출처를 못 얻어 공유0이었다(21b-cow-ready-game.json). 출처 확인을 빼지 않고 내부 복제 계보를 추가했다.
이후 실제 주 렌더러28,828개가 공유18개. 합성 fixture14조건 오류0, 비어 있지 않은 그림/원본 참조/캐시 수정 분리/알 수 없는 태그 제외 확인.
같은 Unity 프레임 월드 카메라 대조3,686,400픽셀 중 차이0/비검정74,413픽셀. 후처리와 전체 곡의 모든 출력 동일성은 확인 안 됨이다.
fixture 전용 재질2개가 뒤 집계에 포함돼20으로 보이는 것은 실제 Arche 공유18과 구분한다.

캡처 없는 새 실행3판(꺼짐→공유→꺼짐), 모니터 누적10..50초 창의 결과다. 프레임1500ms 이상을 제외하는 기존 모니터 집계를 사용한다.
세 판 모두 시작 연출 뒤30ms 초과0, 공통 시험 DLL SHA256 C8CD58663F96851A90B4773A13C138A2A373CB57E4E291A6E6CF1776BAD54534.

|설정|누적 프레임|누적 ms|진짜 FPS|
|---|---:|---:|---:|
|꺼짐 앞|17,426|40,001.5201|435.6334|
|공유|17,314|39,999.3767|432.8567|
|꺼짐 뒤|17,455|39,999.4223|436.3813|

공유의 원본 FPS 손실0.64~0.81%, 대조 차이0.17%. 공유 수 감소가 이 구간의 FPS 향상을 증명하지 않는다. draw call·물리 표시 FPS·GPU 배칭 시간은 확인 안 됨.
각 판 SF집계오류0, 기존 DOTween startup 경고757개씩. 정상 종료3/3·설치5파일 원본 복원3/3.
이3판 뒤 초기 묶기만 준비 단계로 옮겼다. 그 후 최종판의 동일한 성능3판 반복은 하지 않았으며 최종 성능 통과라고 확대하지 않는다.

입력 후보도 기본false. WM_INPUT 전용 메시지 스레드의 수신 QPC와 게임 폴링 QPC 차이만 뺀다. AudioSettings의 새 시각으로 위상을 바꾸지 않고 게임의 폴링 DSP 시각을 기준으로 쓴다.
기존 비동기 입력/이미 시각이 붙은 입력은 추가 보정0. 포커스 없음·곡 밖·다중/알 수 없는 키·홀드·250ms 초과·기존 RawInput 등록·다중/식별 불가 장치에서는 기존 경로다.
원래 SpriteRenderer/정렬/충돌체를 그대로 두며 입력을 새로 주입하지 않는다. 원래 게임의 AsyncRefreshAngles만 선택적으로 사용하고 호출 범위의 targetSongTick을 복구한다.
한 VK의 짧은 down/up은 수신 시각을 유지하지만 폴링 전 두 번 눌린 경우는 모호한 것으로 버린다. 중복 폴링은 한 프레임에 한 번만 보정한다.
[RawInput 등록](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerrawinputdevices)은 프로세스의 같은 장치 클래스를 덮을 수 있어 기존 키보드 등록을 발견하면 등록하지 않는다.
독립 정책/짧은 입력/중복 입력/등록 충돌/정리26조건 오류0. 현재 설치 게임은 이미 비동기 입력 true이고 추가 실제 보정0이다.
물리 키 수신→폴링 분포·수동 판정 변화·입력→광자·다른 키보드 배치/OS 호환성은 확인 안 됨이다.

이번 신규 후보 두 개를 기본 켜짐으로 바꾸지 않는다. 기존 기능·미리 설정·정식 버전/ZIP/릴리스 변경0.

초기 묶기를 준비 단계로 옮긴 최종 후보의 새2판:

|맵 / 실제 곡 초|주 그림 공유 /추적|실제 맵 공유 재질(합성 fixture 전)|월드 차이 픽셀 /전체|게임 각도 경로 차이 /조건|
|---|---:|---:|---:|---:|
|Arche7.6079928|28,828 /28,828|18|0 /3,686,400|0 /21|
|HELLO8.3543334|2,036 /2,426|57|0 /3,686,400|0 /21|

두 판 각각 합성14조건 오류0. 입력 각도 대조는 같은 DSP tick을 원래 게임 함수/후보 경로에 넣고 angle/targetSongTick을 비교한42조건이며 실제 키를 누르거나 판정을 호출한 시험이 아니다.
끝에 angle/targetSongTick/진단 카운터를 원복했다. 실제 추가 보정0, 끈 뒤 공유/추적/연결0, SF집계오류0·정상 종료2/2·설치5파일 원본 복원2/2.
자료: validation/21b22b-final-{arche,hello}-game.json. 전체 곡/후처리 포함/모든 장치 호환성/물리 키 개선은 확인 안 됨이다.
일반 Player는 진단25타입/연구용 픽셀 fixture/입력 각도 시험 메서드 제외, 개발자용에는 포함. LoadCheck212곳 잘못된 참조0.

최종 안전성 후속(21b22b-sorting-delta-game.json): 합성19조건 오류0. 원래 동일 재질을 함께 쓰던 렌더러들에 대한 get_material의 원래 복제 동작도 보존하도록 모든 원래 별칭을 먼저 복구했다.
Resources 등으로 공유 복제본을 찾아 직접 고치는 경우는 원본 렌더러 전체를 복구하고 후보를 중지한다. 쓰기 도중 복제본을 지우지 않고 일반 Uninstall 때 정리한다. 실패 주입1조건 오류0.
이 보호와 별개로 동률 정렬은 실패했다. 반투명3장이 동일 layer/order/distance에 겹치며 재질/렌더러 생성6x6순서를 바꾼36조건 모두 실제로 공유됐고30조건에서 픽셀이 달랐다.
명시적 order를 뒤집는 양성 대조는 픽셀 변화를 검출했다. 따라서 빈 그림/공유 미적용 때문에 나온 결과가 아니다.
[Unity 정렬 규약](https://docs.unity3d.com/6000.3/Documentation/Manual/sprite/sort-sprites/sort-sprites.html)은 상위 정렬 기준이 같을 때 셰이더/재질 묶음과 내부 순서를 보장하지 않는다.
정렬554키의 숫자를 보존해도 이 내부 순서가 같은 것은 아니다. 정렬 키를 새로 쓰거나 그리기 함수를 가로채는 우회는 추가하지 않았다.

입력은 기존 위상을 유지한 상대 보정으로 좁혔다. 원래 게임 함수로 폴링/수신 시각의 각도 차이만 구해 기존 angle에 적용하며, 대기0이면 값도 바꾸지 않는다.
기존 위상/누적 회전 offset을 넣은 새42조건 차이0, 실제 입력 주입/판정 호출0. 이전21조건 절대 시각 대조와 구분한다.
포커스 변경/설정 창/키 지정 중에는 대기를 지우며, 다른 모드가 각도 함수를 수정한 경우 새 후보 설치를 거부한다.
실제 WM_INPUT→폴링 지연 분포와 물리 키 판정 개선은 여전히 확인 안 됨이다.

## 32~36 후속: 모니터 꺼짐 중 독립 작업 (2026-10-17)

사용자가 모니터를 껐다고 알렸다. 이번 묶음의 게임 실행0, 화면 조건 변경0, 3440 시험0이다.
모니터 꺼짐은 모든 개발 중단 요청이 아니다. 이미 완료한 Arche 성능3판/정렬36조건은 다시 실행하지 않았다.
36개 전체 완료가 아니며 위 진행 표의 미구현/보류/미확인은 그대로 남는다.

|번호|새 변경 / 확인|숫자|남은 확인|
|---|---|---:|---|
|32|NativeMotion.Invoke→Output CPU 읽기, framegen DEFAULT 상수 버퍼 UpdateSubresource 경로 확인. NT store/prefetch 추가0|자체 CPU-write GPU staging Map 경로0|대상 버퍼의 CPU/GPU 소비 패턴·캐시 이득 확인 안 됨|
|33|성공한 패스만 완료로 기록. 실패한 셰이더는 다음 준비 때 재시도. RT/Material/active RT를 finally에서 정리. 누락 자원 재시도·Unload 기록 해제|독립 실패 주입/소유권/캐시56조건 오류0|실제 GPU 키워드/블렌드 변형 전체·프레임 비용·완주 확인 안 됨|
|34|ThreadAvoidLogicalZero=false. 곡 중 메인 스레드만 논리0 제외, 비어 있는 마스크/여러 그룹/P코어 미확인은 기존 배치. 곡 밖/끔/Unload 복원, 외부 변경 유지|정책85조건 오류0·Windows 자기 워커6조건 오류0|IRQ/DPC 분포·게임 비용·렌더 스레드·실제 GUI 확인 안 됨|
|35|DEFAULT 상수 버퍼/그림 링/READ staging을 구분. NO_OVERWRITE 변경0|새 동적 버퍼0·새 fence/Flush/대기0|기능 지원·GPU 참조 범위·슬롯 반환 시점·성능 확인 안 됨|
|36|CompiledTagTable.Rows의 List<scrDecoration> 참조와 Plan.Ids의 int를 구분. Unity 객체 주소를 IntPtr에 감추는 변경0|새 비관리 객체 색인0|실제 GC 스캔 이득·객체 수명 안전한 대체 색인 확인 안 됨|

33 독립 시험은 실제 ShaderWarm.cs와 ShaderWarmPasses.cs를 연결하고 Unity/API 실패를 가짜 구현으로 주입했다.
첫/둘째 RT 할당 실패, Material 생성 실패, 둘째 패스 실패, 패스0, Clear 실패, null RT, 미지원 셰이더,
성공 뒤 반복 생략, 실패 뒤 재시도, 자원 중복 보관 방지, 누락 Shader/Resource의 나중 준비, 끔/플레이 중 AfterLoad 생략, Unload 뒤 재준비를 확인했다.
셰이더를 성공 목록에 먼저 넣거나 Blit 예외에 Material/active RT를 남기는 기존 경로를 고쳤다.
예열용 입력 RT를 명시적으로 비우며 마지막에는 호출자의 active RT를 복원한다.
새 전역 이벤트/Harmony 패치/매 프레임 계측0. 기존 ShaderWarm 토글과 기본값은 유지한다.
[Unity 예열 문서](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Shader.WarmupAllShaders.html)는 메모리에 있는 셰이더 변형의 DX11/OpenGL 예열 지원과 DX12/Vulkan/Metal의 vertex layout/RT 차이 제한을 구분한다.
이번 독립 가짜 API 시험은 실제 예열 범위를 측정하지 않았으므로 모든 키워드/드라이버 캐시가100% 준비됐다고 부르지 않는다.

34 실제 Windows 시험은 게임이 아닌 자기 프로세스의 짧은 워커만 사용했다.
허용 마스크63→62→63, 외부 대체 마스크2는 Unload에서도2 유지, 도구가 마지막에 원본63으로 복구했다.
끔·끔으로 전환·곡 밖·Unload·외부 변경 보존6조건이다. 우선순위/MMCSS 기본값0, 새 제외 옵션false다.
P코어 선택과 함께 켜면 확인된 P코어 집합의0번만 제외하며, 구분할 수 없으면 P코어라고 추정하지 않는다.
기존 예약 Tick의 상태 키 계산에 bool 한 개만 더하며, 상태가 같으면 반환한다. 새 매 프레임 Win32 조회/할당0.
복구 실패 뒤 동일 상태 키로 영구 건너뛰던 예외 경로도1초 후 재시도가 가능하게 했다.
[Windows affinity 문서](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-setthreadaffinitymask)의 프로세스 허용 마스크 제한을 유지한다.
CPU0이 실제 인터럽트 집중 코어라는 측정도, 이 제외가 성능을 높였다는 측정도 없다.

35 [D3D11_MAP 규약](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/ne-d3d11-d3d11_map)에 따라
NO_OVERWRITE는 GPU가 읽는 범위를 덮지 않겠다는 소유권 보장이 필요하다. D3D11.1 상수 버퍼 기능 지원 확인도 필요하다.
원래 DEFAULT 상수 버퍼를 flag만 바꾸거나3프레임이 지났다는 이유로 슬롯을 덮는 구현을 넣지 않았다.
이 보류는 GPU 링 자체가 불가능하다는 뜻이 아니며, 현재 안전성/비용 근거가 없는 상태다.

36 숫자 배열을 비관리로 옮기는 것과 Unity 객체를 GC의 도달 가능 대상에서 빼는 것은 다르다.
객체 주소만 숨기면 수명 안전성이 없어지고 GCHandle로 지키면 그 객체는 여전히 루트다.
기존 게임 사전도 같은 장식들을 보관하므로 새 표만 옮겨 GC 스캔이 사라졌다고 할 수 없다.

32/33/34/35/36 각 Developer·Player Release 빌드는 오류0. 네이티브 코드/내장 DLL 변경0.
실제 GPU/화면/물리 입력 검증이 필요한 항목은 모니터 복귀 후 별도 조건에서 진행한다.
재질 공유의 일반 채택0·정렬 실패30/36은 유지한다. 정렬 동률 제외 후보 추가 설계는 사용자 답변 대기다.
main/태그/릴리스/새 ZIP0. 자료: tools/optimization/validation/32-36-offline-summary.json.

## 미완료 항목 재개: 08/11/12/16/36 (2026-10-17)

사용자가 전체 미완료 작업을 계속하도록 다시 지시했다. 모니터 꺼짐 상태에서3440 시험은0이다.
연구/기능 검증과 성능 채택을 계속 구분한다.

### 08/11/16 실제 범위 확인

Arche 편집 준비에서 현재 scrDecorationManager.allDecorations 목록을 직접 사용한 결과:

|항목|값|변경|
|---|---:|---:|
|현재 장식|28,835|0|
|부모 최대 깊이 / 타일 조상 / 장식 조상|1 /0 /0|부모 변경0·Job 적용0|
|활성 MonoBehaviour 형식 / 완전히 빈 Update / 본문 불명|124 /0 /0|추가 비활성화0|
|판정 글자 풀 / 활성 표본|1,400 /0|16슬롯 교체0|
|판정 글자 폰트 /재질 /준비된 서로 다른 문자열 /문자 수 최대|1 /1 /1 /0|Canvas 변경0|

이미 부모 깊이1인 장식은 더 평탄화해서 줄일 계층이 없다. 이 준비 상태의 결과를 모든 맵/재생 구간의 독립성 증명으로 확대하지 않는다.
완전히 빈 Update는 실제 IL이 nop*+ret인 경우만 셌다. 다른 상태에서는 비용이 없다는 추정으로 컴포넌트를 끄지 않았다.
판정 글자는 아직 문구가 준비되지 않은 표본이다. 실제 동시 표시 수/문구·언어·스타일 동일성은 확인 안 됨이다.
새 AnimationAudit는 명시적 연구 명령뿐이며 일반 Player에 포함0·매 프레임 호출0이다.

사전 첫 판은 game 명령 뒤 Hitch.Playing 보호 조건에 걸려 감사0이었다. 도구가 정지 상태로 옮긴 다음 판은 활성 Scene만 골라 장식1개만 보았다.
실제 게임은 장식을 다른 로드된 Scene에 둘 수 있어 이1개 표본을 Arche 감사로 채택하지 않았다.
범위를 현재 관리자 목록/로드된 Scene으로 고친 최종판만 위 표에 사용했다. 두 사전판은 삭제하지 않았다.
3판 모두 정상 종료·설치3+boot2파일 정확한 원본 복원. 첫 판의 도구 단계 오류는 크래시가 아니다.
자료: validation/12-dependencies-manager-game.json, 사전12-alpha-dependencies-game.json/12-dependencies-ready-game.json.

### 12 투명 여백 연구 후보

sf_alpha_bounds_v1은 RGBA/ARGB의4바이트 픽셀을 SSE2로 읽어 Alpha>0의 경계만 구한다.
입력/줄 padding을 바꾸지 않고, null/짧은 길이/stride/크기 overflow를 거절한다. 실제 픽셀을 자르거나 그림을 바꾸는 로딩 hook은0이다.
피벗 계획은 원본 픽셀 위치를 유지하는 수식이며1픽셀 투명 테두리를 포함한다. 반복/미프맵/마스크/알 수 없는 셰이더/DXT/원본 크기 불명은 제외한다.

|검증|조건 /검사량|불일치|
|---|---:|---:|
|네이티브 경계·입력 보존·피벗/조건 정책|23,080조건·1,706,991픽셀|0|
|실제 Unity Sprites/Default GPU,128×128,피벗2×필터2×회전2×배율2|16조건·조건당16,384픽셀·그림 존재 양성 대조16/16|12조건·픽셀 합계171|
|Point·회전0|4조건|0|
|Point·회전17°|4조건|각1~3픽셀|
|Bilinear|8조건|각9~31픽셀|

기하의 수학적 위치가 같아도 원래/자른 Texture의 UV·부동소수점 표본화/래스터 결과를 바이트까지 같다고 할 수 없다.
그 중 어느 단계가171픽셀 각각을 만든 정확한 원인인지는 확인 안 됨이다. 크롭은 일반 기능에 채택0이다.
같은 수식이나 일부 Point4조건 통과로 실패12조건을 무시하지 않는다. AlphaCropPlan/Probe/Fixture는 일반 Player 컴파일 제외다.
네이티브 DLL은 새 무설정 export를 포함하지만 일반 로딩에서 호출0이다.

기존 네이티브 회귀: PNG 줄 필터/내부 포맷0/1/2·완전4×4블록 DXT600조건 불일치0, 숫자 계산4,194,304개 불일치0.
최초 오프라인 회귀 도구는 TextureFormat enum과 네이티브 내부 layout번호를 혼동하고 패딩되지 않은 블록을 넣어101/600불일치였다.
잘못된 도구 입력은 제외했고 ABI에 맞춘 위600조건만 채택한다. 제품 계산/압축 알고리즘 수정0이다.
실제 전체 PNG 맵/다른 CPU 디스패치/전체 렌더링 동일성은 이번 새export 검증에서 확인 안 됨이다.

### 36 정수 태그 계획만 비관리 영역에

TagIndexNativePlans=false 기본값. 기존 태그 색인이 켜진 경우에만 다음 재생 준비에서 작동한다.
현재 효과들의 태그 수에 맞춘 최대8MiB 정수 영역을 한 번 만들고 연속으로 저장한다. 영역을 reset해서 오래된 slice에 새 값을 덮지 않는다.
배열/영역이 가득 차거나 할당이 실패하면 원래 int[] 계획이다. 해제/잘못된 스레드/범위 밖 slice는 포인터를 읽지 않고 원래 사전 조회다.
Unity 객체/문자열/장식 목록은 관리 참조로 유지한다. 객체 주소/GCHandle를 숫자 메모리에 숨기는 변경0이다.
새 맵은 기존 FastMove.ForgetLevel→TagIndex.Forget 경로로 표와 영역을 놓는다. 메뉴 복귀/설정 해제/모듈 Unload에서도 정리하며 새 메뉴 복귀 패치는 기존 독립 tags Harmony 소유자와 함께 해제한다.

|맵 /최종 기능 판|효과 /비관리 계획|번호 /영역 용량(정수)|원래 사전 행 참조 불일치|재생 중 실제 색인 적중|해제 뒤 오래된 slice 읽기|
|---|---:|---:|---:|---:|---|
|Arche|97,694 /97,694|98,121 /98,121|0|27|False·자기 영역 해제True|
|HELLO|5,869 /5,869|7,676 /7,676|0|209|False·자기 영역 해제True|

영역 payload는392,484/30,704바이트다. 관리 Plan/문자열/객체 참조가 사라지는 것은 아니다.
독립 수명·범위·원래 태그 순서·중복·null·작은 용량·외부 스레드·해제 뒤 읽기10,028조건 오류0.
실제 최종2판105,797번호 대조 불일치0·SF집계오류0·정상 종료2/2·설치5원본 복원2/2.
DOTween 종료 경고는 두 판 각각 startup757+missing target/field18로 보존하며 프로세스 전체 오류0이라고 하지 않는다.
Arche 앞선8MiB 고정 할당판도 기능 대조는 통과했지만 최종판은 실제 필요한 수에 맞춰 다시 확인했다. 반복 FPS 표본이 아니다.
실제 GC 스캔 시간·원본 FPS·순간 끊김·전체 곡·다른 모드 전체 호환성은 확인 안 됨이다.
일반 Player/Developer/PlayerAuto 빌드 오류0·LoadCheck212잘못된 참조0·일반 진단29타입 제외/필수6타입 유지.
자료: validation/36-primitive-final-{arche,hello}-game.json,12-36-followup-summary.json.

## 17 후속: 일반 빌드의 순수 진단 분리 (2026-10-17)

AllocScan/SlowScan/PhaseWatch/RenderWatch/SamplerWatch/LoopProfiler/DecoDiag7타입을 추가로 일반 Player에서 컴파일 제외했다.
해당 설치/해제/매 프레임 진단 호출/개발자 GUI 참조도 DEV||AUTOTEST 범위로 제한했다.
플레이어 모니터의 빈 엔진 단계 설명은 빈 문자열 그대로다. 사용자 Insert 설정 창/효과 분산/프레임 구성 숫자/FPS·출력 대기 표시는 유지한다.
EffectScan은 이름과 달리 EffectBudget.Enter/Exit와 실제 사용자 모니터의 효과 시간 집계에 필요해 제외하지 않았다.
FrameParts/ModCost/PresentWatch/FrameGen/GC/화면 효과 등 런타임 기능을 진단이라는 이유로 지우지 않는다.

|확인|값|
|---|---:|
|일반 Player 진단 타입 제외 /Developer 포함|36/36 ·36/36|
|기존 필수 런타임 타입 유지|6/6|
|일반 Player LoadCheck 참조 /잘못된 참조|212 /0|
|Developer/Player/PlayerAuto 빌드 오류|0 /0 /0|
|자동 시험 코드 없는 일반 Player 실제 시작 준비 /정상 창 닫기 /정상 종료|True /True /True|
|일반 Player 시작 시험 SF집계오류 /게임 설치3+boot2원본 복원|0 /5/5|

첫 Player빌드는 개발자 flags 함수의 PhaseWatch 참조1개가 남아 컴파일 실패했다. 보호 구간에 포함한 뒤 위 최종 빌드/시작을 확인했고 실패 로그는 보존했다.
일반 시작 판은 곡 재생/성능/화면 품질 시험이 아니다. 실제 자동 시험 DLL을 일반 Player로 간주하지 않았다.
한 게임 함수에 걸린 다른 모드와의 Prefix/Postfix 순서를 바꾸지 않았다. 전체 핫패치 병합은 아직 구현/검증하지 않았다.
자료: validation/17-normal-player-smoke.json. 버전/릴리스/ZIP 변경0.

## 19·36 후속: 포탈 도착·히트맵 수명·원본 태그 대조 (2026-10-17)

실제 2560×1440 120Hz · FullScreenWindow · 동기0 · 앞서 준비2. 사용자가 모니터를 껐으므로 3440 시험으로 합산하지 않는다.
태그 숫자 저장 후보와 히트맵을 켠 두 판을 각각 새 실행으로 포탈까지 진행했다. 그림 캡처0, 강제 효과/해상도 변경0.

|확인|HELLO 2026|Arche|
|---|---:|---:|
|포탈 도착 /SF집계오류 /정상 종료|1 /0 /1|1 /0 /1|
|시작·완료 원본 대조 계획 수(각각)|5,869|97,694|
|시작·완료 원본 대조 값 수(각각) /불일치|7,676 /0|98,121 /0|
|완료까지 숫자 계획 적중 /무효화|5,840 /0|97,692 /0|
|후보 끈 뒤 소유 int 수 /설치·boot 원본 복원|0 /5/5|0 /5/5|
|히트맵 최악 기록 타일 /시간|523 /81.17ms|79 /143.31ms|

위 최악 프레임에는 명시적 원본 대조 검사가 각각79.487ms·142.284ms 들어갔다. 끝 대조도90.578ms·148.925ms다.
두 판은 기능/수명 시험이며 성능 대조가 아니다. 곡 전체 그림 동일성·GC 스캔 시간·3440 성능은 확인 안 됨.
HELLO 실행 도중 이후 빌드가 바뀌어 종료 시 bin SHA를 사용한 첫 stamp를 수정했다. 종료 전 실제 설치 DLL을 보존한 SHA는
b07297ce506c5c3e2db4a51077affd3263de9b80c7cf7e9e5016b4d41a61aac9이며,
끝 bin SHA ca40214ca16ffaf4660893f98ad29919e9a8a2d24829098d420164c4a03e02e2와 구분한다.
Arche부터 runner가 실행 전 DLL SHA를 저장한다. HELLO 판을 이후 빈 포탈 수정 검증으로 쓰지 않는다.

이전 판에 기록이 있고 새 판에는 기록이 없을 때 포탈 도착 후에도 옛 타일을 현재 판처럼 표시하는 오류를 발견했다.
현재 완료0건은 명시적으로0건, 새 진행 중 이전 기록은 '이전 판 기록'으로 표시한다.
연결 소스 수명 fixture8조건 오류0(완료 멱등/완료 뒤 기록 금지/Unload 포함). 실제 빈 포탈 판은 확인 안 됨.

Markdown 복사 API는 실제 게임에서 생성문과 클립보드 일치True, 이전 클립보드 원본 복원True,466글자, 외부 메시지0.
물리 마우스의 복사 버튼 클릭은 확인 안 됨. 기존 태그 정리는 FastMove.ForgetLevel→TagIndex.Forget 경로를 재사용하며 중복 LoadLevel 패치를 추가하지 않는다.
자료: validation/19-36-full-{hello,arche}-game.json,19-30-integration-game.json; HeatmapLifetimeCheck.

## 30 후속: 기존 WinMM 1ms 요청의 소유·복원 (2026-10-17)

새 NT0.5ms 요청0. 기존 NoThrottle 기본false와1ms 요청 값을 유지했다.
begin 성공 때만 소유하고 성공한 begin과 같은 period로 end를 요청한다. end 실패/없는 API는 소유 상태를 유지해 재시도한다.
별도 절전 API가 실패해도 타이머 해제 시도는 실행한다. 중복 켜기/끄기는 새 요청0이며 기존 Main.Unload→LowEnd.Shutdown에서 해제한다.

|확인|값|
|---|---:|
|순수 정책/실제 별도 프로세스 WinMM 조건 /오류|18 /0|
|실제 게임 요청·소유 상태|끔·끔→켬·켬→켬·켬→끔·끔|
|게임 SF집계오류 /정상 종료 /원본 복원|0 /1 /5/5|
|새 NT0.5ms 요청 /추가 매 프레임 API 계측|0 /0|
|실제 표시 지연·FPS 효과|확인 안 됨|

[Windows 타이머 규약](https://learn.microsoft.com/en-us/windows/win32/multimedia/timer-resolution)에 맞춘 요청/해제 소유 수정이다.
NT API의 전역 해상도를 임의의 옛 값으로 덮는 복구는 넣지 않았다.
자료: TimerPeriodCheck, validation/19-30-integration-game.json. 두 Release/PlayerAuto 빌드 오류0.

### 36 캡처·검사 없는 원본 FPS 대조

Arche 새 실행 끔→켬→끔3판, 같은 managed/native SHA ·2560×1440 120Hz ·프레임 생성0.
기존 모니터 경과10~50초 누적40초 구간이며 정확한 native 곡5~45초 창과 같다고 가정하지 않는다.
기존 모니터의1500ms 이상 프레임 제외 규칙을 유지한다. 게임 실행 중 빌드/독립 벤치/캡처/전체 태그 대조0.

|판|원본 프레임 수|누적ms|원본 FPS|
|---|---:|---:|---:|
|끔 앞|15,627|40,020.159|390.4782|
|숫자 영역 켬|17,497|39,999.096|437.4349|
|끔 뒤|17,427|39,998.961|435.6863|

켬의 대조 대비 차이+0.4013~+12.0254%, 끔 앞→뒤 차이+11.5776%.
활성1표본·대조 변동 때문에 성능 개선 채택0. GC 스캔 시간·물리 표시 FPS·3440 성능은 확인 안 됨.
SF집계오류0/0/0·정상 종료3/3·원본 복원5/5×3. 기존 DOTween NULL target startup 경고와 Discord 초기화 오류는 별도 게임 로그에 남아 있다.
일반 기능 오류 전체0이나 다른 모드 오류0이라는 뜻이 아니다. 후보 기본false 유지.
자료: validation/36-perf-{off-before,on,off-after,summary}.json.

## 20 후속: 렌더 스레드의 소유 핸들·예약 복구 (2026-10-17)

기존 메인 스레드 예약과 별도로 ThreadRenderScheduling 기본false를 추가했다.
새 선택은 기존 우선순위/코어 설정을 렌더 스레드에도 적용한다. 우선순위/코어가 모두 꺼지면 캡처도0.
게임의 원래 렌더 스레드는 네이티브 Unity 플러그인 이벤트1개로 실제 커널 핸들을 복제해 잡는다.
스레드 번호만 저장/다시 OpenThread하지 않아 번호 재사용으로 다른 스레드를 변경하지 않는다.
큐에 남은 취소/옛 세대 이벤트는 무시한다. 네이티브 콜백은 관리 함수/객체 주소를 보관하지 않는다.
원래 D3D 장치/화면/Present 연결과 GPU 상태는 변경0이다.

|확인|값|
|---|---:|
|독립 실제 워커 핸들·취소·복원 조건 /오류|539 /0|
|가짜 Unity+실제 워커 수명 조건 /오류|10 /0|
|기존 실제 코어 복구 /CPU 집합 정책 오류|6조건0 /85조건0|
|실제 HELLO 렌더 코어 마스크|3F→3E→3F|
|실제 HELLO 렌더 우선순위|0→15→0|
|실제 게임 SF집계오류 /정상 종료 /원본 복원|0 /1 /5/5|
|네이티브 이미지/DXT 회귀 /비트 계산 대조|600조건0 /4,194,304개0|
|Developer/Player/PlayerAuto 빌드 오류 /LoadCheck 잘못된 참조|0/0/0 /0(212곳)|
|일반 진단 제외 /필수 런타임 유지|36/36 /6/6|
|FPS·끊김·실제 표시 지연 개선 /3440 시험|확인 안 됨 /확인 안 됨|

실제 판은2560×1440 120Hz의 짧은 기능 시험이며 전체 곡/성능 대조가 아니다.
기존 설치 DLL·native·설정·boot2파일을 바이트까지 복원했다.
오래된 콜백, 중복 take/event,512번 미소비 핸들 취소, 이미 종료된 스레드, 부분 적용 뒤 복구,
다른 소유자의 서로 다른 priority/affinity 보존을 독립 시험했다.
외부 변경 fixture의 첫 시도는 관측 핸들에 QUERY 권한을 빠뜨려 실패했다. 도구 권한을0x60으로 수정한539조건 결과이며 제품 실패로 세지 않는다.

매 프레임 Win32/토폴로지 조회0. 기본 꺼짐은 조건 검사 후 즉시 반환한다.
전환 시에만 플러그인 이벤트를 내고 연결 대기 중 최대3초 동안50ms 간격으로 핸들 완료를 확인한다. GPU 완료 대기0/새 Harmony 패치0.
실패하면 원래 예약을 유지/복구한다. 다른 값으로 바뀐 priority/affinity는 덮어쓰지 않는다.
설정 끔/곡 밖/Main.Unload에 같은 복구 함수를 연결했다. 실제 새 DLL reload 후속 확인은 아래와 같다.

[Unity 플러그인 이벤트](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/GL.IssuePluginEvent.html)는 렌더 스레드에서 실행한다.
[MMCSS 해제 규약](https://learn.microsoft.com/en-us/windows/win32/api/avrt/nf-avrt-avrevertmmthreadcharacteristics)은 등록한 스레드에서 직접 해제해야 한다.
따라서 렌더 MMCSS는 넣지 않았으며 UI에 오디오 방식은 메인에만 적용한다고 명시한다. 일반 HANDLE 복구를 MMCSS 복구라고 부르지 않는다.
자료: RenderThreadCheck,RenderSchedulingCheck,validation/20-render-game.json. 첫 게임 판의 no-eligible 상태 문자열은 값이 적용된 뒤에도 남는 표시 오류였고 최종 빌드에서 수정했다.

### 활성 상태의 실제 새 DLL reload

옛 자동 시험의 일반 reload는 묶음 끝에 자동 플레이를 되돌려 곡이 먼저 실패할 수 있다.
따라서 명시적 연구 명령 reloadactivefixture는 임시 set이0개·자동 플레이·새 DLL을 요구하고, 실행 중 자동 플레이를 유지해 바로 실제 UMM Reload를 호출한다.
일반 reload 동작은 변경0. 전체 시험은 소유한 새 게임에서 정상 quit로 끝내고 설치5파일 원본을 복원한다.

|확인|값|
|---|---:|
|Unload 직전 활성 소유 /pending|True /0|
|Unload 직전 실제 렌더 priority /mask|15 /62|
|원래 값 /새 모듈이 재적용 전에 읽은 실제 값|0·63 /0·63|
|옛·새 실행 MVID의 서로 다른 수|2|
|SF집계오류 /정상 종료 /원본 복원|0 /1 /5/5|

UMM은 옛 설정을 저장해 새 모듈도 같은 예약을 다시 켰다. 새 모듈의 before 값은 실제 새 lease의 적용 전 Win32 조회이며 원래0·63과 일치한다.
외부 관측 핸들로 읽은 마지막0·63은 새 모듈의 명시적 Shutdown 뒤 값이다. 이를 Unload와 재적용 사이 외부 시점 측정으로 부르지 않는다.
직접적인 그 사이 외부 표본·FPS/표시 지연 효과는 확인 안 됨.
자료: validation/20-render-active-reload.json(두 managed SHA·MVID·활성 상태·각 관측 범위 보존). 두 Release/PlayerAuto 빌드 오류0.

## 35 후속: GPU 완료를 확인하는 소유 상수 버퍼 링 (2026-10-17)

FrameGenConstantRing 기본false. 보간의 BlockFlow가 소유한 상수 버퍼3개만 DYNAMIC으로 만들고,
D3D11.1 MapNoOverwriteOnDynamicConstantBuffer 지원을 확인한 뒤 immediate context에서 NO_OVERWRITE로 쓴다.
게임/Unity 소유 vertex/index 버퍼 변경0, 렌더 상태/픽셀 계산/보간 방식/스왑체인 변경0.
상수 Data의 페이로드는1,760바이트×3=5,280바이트이며 드라이버 실제 할당/VRAM 사용량을 뜻하지 않는다.
다른 보간 계산(DenseFlow)의 자체 버퍼와 모든 게임 업로드를 전환한 것은 아니다.

각 command list 실행 뒤 소유 event query를 End한다. 다음 사용은 GetData(DONOTFLUSH)가S_OK·True일 때만 허용한다.
미지원/미완료/이미 같은 미실행 목록에서 예약한 칸/Map 실패는 기존 DEFAULT UpdateSubresource로 돌아간다.
GPU 완료 대기 루프·Flush·3프레임만 지난 뒤 무조건 덮어쓰기0. Map과 드라이버 자체의 블로킹 시간은 아직 측정하지 않았다.
공유 immediate context의 기존 보호 범위를 그대로 사용하며 소스 그림 완료 query나 잠금 정책을 바꾸지 않는다.
기본 꺼짐은 새 버퍼/쿼리 생성0·기존 업로드 경로다. 네이티브 사용 횟수는 수명과 별개인 정적 atomic으로 읽어 engine 해제 뒤 객체를 역참조하지 않는다.

|확인|값|
|---|---:|
|독립 상수 업로드 색/알파 양성대조 조건 /픽셀 /차이|60 /61,440 /0|
|미실행3칸 예약 뒤4번째 쓰기 /deferred context Map 허용|거절 /거절|
|독립 실제 feature 지원 /D3D debug layer|1 /확인 안 됨|
|실제 보간 상수 업로드의 기존 경로와 바이트 대조 조건 /픽셀 /추가 차이|60 /14,745,600 /0|
|같은 보간 fixture의 기존 움직임 판정 실패 조건|12/60|
|실제 HELLO 링 쓰기 /기존 경로 폴백(켬 표본)|11,513 /396|
|링 끈 뒤 지원 상태 /FG 끈 뒤 원래Present 연결|0 /0|
|실제 SF집계오류 /정상 종료 /설치5원본 복원|0 /1 /5/5|
|네이티브/Developer/Player/PlayerAuto 빌드 오류|0 /0 /0 /0|
|이미지/DXT 회귀 /비트 계산 /렌더 핸들 수명 회귀 오류|600조건0 /4,194,304개0 /539조건0|
|성능·전체 곡·실제3440|확인 안 됨|

독립 색 fixture 사전판은 SDK winrt include 누락으로 빌드 실패했고, 첫 draw는 기본 컬링 때문에 양성대조 alpha에 실패해 제외했다.
현재 도구는 CullNone을 명시하고 모든 원본 픽셀의 색/alpha도 검증한다. 게임 raster 상태 변경0.
D3D 디버그 레이어가 없어 debug_errors=-1로 적으며0오류 통과라고 부르지 않는다.
보간 기존 움직임 판정은 새 링 끔/켬 모두 첫 비정지 조건에서16,648픽셀 실패였다.
새 링 대조 모드는 전체60조건에서 기존 움직임 실패12개를 별도로 보존하고, 업로드 경로 사이 전체RGBA 바이트 동일성만 검증한다.
기존 비링 움직임 fixture의 실패 조건/반환은 그대로다. 보간 움직임 품질 통과나12개 실패 해결이 아니다.
실제 게임 기능 판은2560×1440 120Hz·4배의 짧은 전환 시험이며 캡처/성능 대조가 아니다.

자료: ConstantRingLab,BlockFlowLab constant-ring 모드,validation/35-constant-ring-game.json.
규약: [D3D11_MAP](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/ne-d3d11-d3d11_map),
[Map](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-map),
[GetData](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-getdata).

### 35 캡처·검사 없는 HELLO 원본 FPS

같은 managed/native SHA·실제2560×1440 120Hz·FG고정4배, 새 실행 끔→켬→끔3판.
기존 모니터 경과10~50초 누적40초 창이며1500ms 이상 제외 규칙을 유지한다. 정확한 native 곡5~45초 창과 같다고 가정하지 않는다.
시험 중 빌드/벤치/GPU캡처/전체 대조0. 출력 카운터는 곡 창으로 자르지 않았으므로 이 표의 원본 FPS와 배율을 계산하지 않는다.

|판|원본 프레임 수|누적ms|원본 FPS|
|---|---:|---:|---:|
|링 끔 앞|9,094|39,999.076|227.3552|
|링 켬|9,426|40,002.022|235.6381|
|링 끔 뒤|9,504|40,001.740|237.5897|

켬의 대조 대비 차이−0.8214~+3.6431%, 끔 앞→뒤 차이+4.5015%. 활성1표본·개선 채택0·기본false 유지.
켬 누적 링 쓰기54,242·전체 폴백2,627(곡 창 미분리, 원인별 분리 확인 안 됨).
SF집계오류0/0/0·정상 종료3/3·원본 복원5/5×3. Arche 성능·전체 곡·실제3440·물리 표시 지연은 확인 안 됨.
자료: validation/35-ring-perf-{off-before,on,off-after}.json,35-ring-summary.json.

## 2026-10-18 Claude 마무리 (codex/optimization-36)

1단계: 12(투명 여백 크롭)·21(재질 공유)·29(SGen 환경)·NoGCRegion 시험 코드와 `sf_alpha_bounds_v1` 내보내기 제거, sfnative 다시 빌드(오류0).
3단계: 기본 켬 = 04 JSON 문자열 풀, 06 업로드 예산, 10 정수 태그 색인(HELLO 전곡 2판 SF 오류 0, 맵 열기 11.9초 동일). 05 디스크 캐시는 USB SSD(D:)에서 열기 11.9→16.4초라 **기본 끔**. 14·17·18은 무조건 적용.
01·07·09·13·15·19·20·22·25·31·34·35·36은 설정 창 토글(기본 끔)로 유지, 설정 창 7페이지 열기 오류 0.
2단계에서 이번에 넣은 것: 30 NtSetTimerResolution 0.5ms(기본 끔, 우리가 올린 요청만 소유·해제, 이 PC는 이미 0.4959ms라 변화 없음), 28 곡 중 물리 중지(기본 끔, Physics2D만 Script로, 곡 끝/끔/Unload에 우리가 바꾼 값만 복구, HELLO 전곡 ABBA 226.5 vs 226.5 FPS = 이득 없음).
넣지 않은 것과 이유: 24 DSP 버퍼(변경 시 오디오 지연이 바뀌어 사용자 입력 오프셋 보정이 어긋남), 26·27 CPU 프레임 시작 페이싱(SwapEffect 변경 불가, `MaxQueuedFrames` 2→1은 이미 FPS 209→128로 악화 실측), 23 전체 맵 버퍼 전환·25·32 NT store(자체 CPU 쓰기 스트리밍 경로 0), 33 전체 키워드 변형(검증 불가).

## 2026-10-18 후속 3건 (실측)

1. 곡 중 30ms 넘는 프레임(HELLO 전곡 한 판 12~20개, 시각 무작위, CPU 4ms·GPU 3ms인데 프레임만 30~44ms = 메인 스레드가 윈도우에 밀림). 메인 스레드 우선순위(ThreadSchedulingMode=1) 단독 14개, 0번 코어 제외 단독 14개, 메모리 묶음(01·31·36) 단독 11개로 효과 없음. **메인+렌더 스레드 우선순위를 같이 켜야** 줄어듦: HELLO 12·20개 → 0·1·2개, 1% low 106~108 → 113~116, 0.1% low 48~51 → 62~66, 평균 214~216 → 220 FPS. Arche 11 → 3개(1% low 92→94, 0.1% low 71→75). 기본 켬으로 반영. 07·09·13(CPU 묶음)은 10·12개로 효과 없어 기본 끔 유지.
2. 디스크 캐시 느림의 원인: 상한 512MB가 Windflower(1.6GB)를 계속 밀어냄(적중 40%), 읽기마다 관리 SHA-256, 쓰기마다 폴더 훑기·정렬, 읽기마다 수정시각 쓰기, 블록 이중 복사. 8바이트 단위 64비트 검사합·총량 한 번 계산·복사 제거·저장 위치 C: LocalAppData·상한 3GB로 고침. 두 번째 열기 Windflower 11.8 → 9.7초, HELLO 12.9 → 11.6초(첫 열기는 +0.1~1.2초). 남은 공간이 상한+4GB보다 적으면 스스로 끔. 기본 켬.
3. 39ms 한 번: 맵의 특정 지점이 아니라 위 1번의 무작위 윈도우 스케줄링 지연. 해결은 1번.

## 2026-10-18 바닐라 병목 7곳 실측 (HELLO 전곡, 개발자용 time-probe/alloc-probe)

|번호|대상|프레임당 시간(측정 비용 포함)|할당|판단|
|---|---|---:|---:|---|
|1|ffxSetFilterAdvancedPlus.StartEffect|0.001ms (0.1번/프레임, 한 번 14us)|목록 20위 밖|효과 종류별 시간도 0ms. 패치 안 함|
|2|CameraFilterPack OnRenderImage 문자열 Set*|-|목록 밖|371개 필터를 Harmony 로 바꾸면 곡 시작 패치만 수 초(앞서 측정: 수백 패치 4초). 이득 없음, 안 함|
|3|scrVisualDecoration.UpdateShader|0.028ms (69번, 한 번 0.4us)|목록 밖|안 함|
|4|scrPlanet.MoveToNextFloor|0.024ms|0.009MB/s (120B x 78/s)|안 함|
|5|scrPlanet.Update (OverlapCircleAll)|0.086ms (24번, 한 번 3.6us)|0.016MB/s (평균 3B)|할당이 거의 없음(빈 배열 재사용), 안 함|
|6|scrConductor.Update 안 히트사운드 문자열|전체 0.164ms|0.006MB/s|안 함|
|7|scrHitTextManager.ShowHitText|0.001ms|목록 밖|안 함|
일곱 개를 합쳐도 프레임당 0.14ms(프레임 5.4ms 의 2.6%, 측정 비용 포함 상한)라 FPS 220 vs 218 같은 잡음 안이다. 곡 중 큰 비용은 ffxRecolorFloorPlus(한 번 7~10ms, 42KB), scrFloor.Update(0.22ms)다.

## 2026-10-18 타일 색 바꾸기(ffxRecolorFloorPlus) 미루기와 작업 스레드 실험

원인(HELLO 실측): 타일 하나 6.5us(SetTrackStyle 3.2 / ColorFloor 1.5 / UpdateAngle 1.1), 한 효과가 타일 4,800개를 칠해 한 번에 7~10ms. 기존 구간 나누기(RecolorSplit)는 조각 하나 2.6ms가 프레임마다 1~2개씩 몇 프레임 이어졌다.
방법(`RecolorDefer.cs`, 기본 켬, 설정창 "화면 밖 타일 색 바꾸기 미루기"): TileCull 이 렌더러를 꺼 둔 타일에서 UpdateAngle / SetTrackStyle / ColorFloor 의 엔진 호출만 기록하고(관리 값 styleNum·special* 필드·specialTimeOffset 은 원래 식 그대로 즉시), 타일이 켜지기 직전(Uncull)·움직여 관리에서 뺄 때·전부 켤 때 적용. 색은 같은 DOTween 을 만들어 끝내거나(앞의 것) 지난 시간만큼 Goto(마지막). 프레임마다 0.6ms 는 먼저 들어온 순서로 미리 적용해 쌓인 양을 줄인다(타일 이동 효과가 한꺼번에 움직여 밀린 것을 적용하는 프레임이 8ms 가 되던 것).
검증: 곡 전체 끝 타일 상태 대조(`tilesig`, 타일 14,721개 x 스타일·메시·재질 8값·색·special 필드): HELLO·Arche 모두 비색 필드 차이 0, 단색(Single) 타일 색 차이 0(처음에는 SetColor(끝값)이 DOTween 끝값과 마지막 비트가 달라 13개가 달랐고 같은 DOTween 을 쓰게 고쳐 0). 움직이는 색(Glow·Blink·Rainbow)은 끄고 켠 두 판 사이뿐 아니라 끈 판 두 번 사이에도 같은 만큼 다르다(곡 끝 시각 차이). `culltest` 5곳(미룬 타일 2~4천 개 있는 때) 다른 픽셀 0. 다시 하기 2번 오류 0.
HELLO PlayerAuto ABBA: 끔 12ms 초과 102·102 / 16ms 초과 40·34 / 1% low 113 / 0.1% low 63·65 → 켬 66·70 / 30·30 / 116 / 69·71. 곡 중 효과가 가장 무거운 프레임 26~32ms → 17~22ms(137초·165초 색 바꾸기 프레임이 상위 8개에서 사라짐). Arche 12ms 초과 358 → 288, 16ms 초과 29 → 22.
파티클: HELLO 곡 중 켜진 파티클 시스템 47개, 재생 19~21개, 입자 약 9천 개 중 안 보이는 것 3% — 모두 보이는 행성 꼬리(Planet/Pivot/Tail, 입자 약 470개, 방출 501/s)다. 끌 대상이 없고, ParticleSystem*UpdateAll 11~18ms 는 작업 스레드를 기다리는 구간(무작위 시각)이다.
작업 스레드(Job.Worker 0~4, Background 16개) 우선순위를 곡 중 올려 봤으나 16ms 초과 38 → 67, 20ms 초과 15 → 33, 0.1% low 63 → 52 로 나빠져 지웠다.

## 2026-10-18 타일 이동(ffxMoveFloorPlus) 불투명도 전용 효과 · 장식 이동 쪼개 보기

- HELLO 8,933칸 길이 0 효과: 8.6~10.1ms. 로그로 확인하니 "위치를 쓴다"가 켜져 있지만 값이 (NaN, NaN)이라 원래 TweenFloor 도 위치는 아무것도 안 한다(타일 이동 효과 17개가 전부 위치·회전·크기를 안 쓴다는 분류는 일부 틀렸다: 위치 표시는 켜져 있음). 위치·크기 값이 모두 NaN 이거나 안 쓰고 회전을 안 쓰고 불투명도만 바꾸는 길이 0 효과는 transform 읽기·startPos 계산을 빼고 나머지(material 읽기, 이전 애니메이션 정리, 값 비교·쓰기)는 그대로 하는 경로(`FloorAnim.OpaZero`)로 보낸다. 9.2~10.1ms → 4.4ms. 곡 끝 타일 상태 대조(14,721개) 차이 0.
- 화면 밖 타일 불투명도 미루기는 하지 않았다: 남은 4.4ms 는 material 읽기·이전 애니메이션 정리·쓰기이고, 불투명도는 장식·홀드가 읽고 scrFloor.LateUpdate 의 페이드가 읽으므로 미룰 때 맞춰야 할 곳이 많은데 이득은 한 프레임 4ms 이하다.
- 타일 12363의 장식 225개(길이 0, visible False, 크기 0,0, 이미지 "null") 이벤트: 합계 1.11ms (크기 0.21, 보이기 0.11, 깊이·이미지 0.73, 나머지 0.05). 로그에서 5ms 로 보인 것은 그 프레임 장식 이동 전체(2.4ms) 또는 곡 5초대 33개 효과 합이다. 이 이벤트는 곡 전체에서 1ms 를 넘는 길이 0 장식 이동 중 유일했다. 패치하지 않았다.

## 2026-10-18 Gemini IL 분석 후보 검증 (HELLO ABBA, 판마다 새로 켬)

| 후보 | 검증 | 결과 |
|---|---|---|
| 1-a 미룬 색 적용의 메시 갱신이 화면 밖 타일을 켜고 관리에서 뺌 | 맞음(PinComp). 그 갱신 중 화면 밖(st=1) 타일은 그대로 둠 | HELLO 관리에서 뺌 92,162 → 40,154회 |
| 1-b 타일 스크립트 Update/LateUpdate 마다 관리에서 뺌 | Update/LateUpdate 는 위치·회전·크기·메시를 안 바꿈(IL). 미룬 색만 먼저 적용하는 가벼운 Prefix 로 교체 | 위와 합쳐 |
| 3 길이 있는 불투명도 전용 타일 이동도 transform 없이 | OPA 기록은 Transform 을 안 씀(시작·적용·검증 모두 r.F) | 위와 합쳐: 평균 218 → 224 FPS, 1% low 114 → 118, 0.1% low 75~78 → 81~82, 12ms 초과 53~59 → 46~47 |
| 2 화면 밖 타일의 발광(topGlow) 렌더러도 끄기 | HELLO 130초에 발광 렌더러 14,721개 모두 켜짐, 9,712개 활성, 보이는 것 64개 | 평균 224 → 227~229 FPS, 1% low 118 → 120~121, culltest 3곳 다른 픽셀 0 |
| 3 scrFloor.LateUpdate 의 isTweening 용 위치·크기 읽기 | isTweening·lastPos·lastScale 은 두 어셈블리 전체에서 LateUpdate 의 multiplanetLine 갈래만 읽음(IL 전수, 설치 모드 DLL 문자열에도 없음). multiplanetLine 이 null 이면 4곳 읽기 생략 | 평균 227~228 → 232~233 FPS, 1% low 120~121 → 126 |
| 5 필터 OnDisable 재질 파괴·RT 예열 | 곡 전체 new Material 823번 10.5ms, Shader.Find 78번 9.1ms, 가장 긴 한 번 0.52ms, 곡 처음 8초 합 1.9ms. RT 풀은 몇 프레임 안 쓰면 지워져 예열이 남지 않음. 재질을 남기면 Start 에서만 넣는 값이 달라질 수 있음 | 하지 않음 |
| 6 시작 유예 5초가 5.13초 효과 묶음보다 먼저 끝남 | 유예 중에는 나누지 않는다(바로 실행). 5.13초 묶음은 유예 밖이라 나누기 대상이지만 합 10ms 가 기본 예산 10ms 안이라 한 프레임에 돈다 | 동작이 맞음, 바꾸지 않음 |

모든 변경은 곡 끝 타일 상태 대조(HELLO 14,721개, Arche 4,139개) 비색 필드 차이 0, 단색 색 차이 0. Arche 판은 12ms 초과가 310 ↔ 349 로 한 판씩이라 판단 보류(1% low 96 ↔ 94).
오늘 합계(HELLO, 같은 PC·같은 설정 판끼리): 평균 약 218 → 233 FPS, 1% low 114 → 126.

## 2026-10-18 Gemini 2차 후보(모듈 간 연결) 검증 — Arche

먼저 지금 빌드에서 Arche 를 다시 쟀다: Gemini 가 짚은 219.6초 44~66ms 스파이크는 없다(그 효과 4,962개는 이미지를 안 바꾸고 3.3ms). 이미지를 바꾸는 큰 장식 이동은 곡 248.7초(1,731개)와 258.0초(3,231개 + 1,731개)였고, 258초 두 효과가 15ms(그중 SetSprite 10ms, 그 프레임 22ms)였다.

| 후보 | 결과 |
|---|---|
| 1 투명 장식 SetSprite 미루기 | 구현(`LazySprite`). 조건: 투명·히트박스 없음·마스크 아님(InvisibleSkip 이 안 그리는 목록) + 그 효과가 이미지 말고 크기·부드럽게·마스크를 안 바꿈. 텍스처 감싸기 모드(텍스처 전체 값)는 원래 자리에서 바로 넣고, 스프라이트를 읽는 게임 함수 15곳(IL 로 찾음: SetSprite, UpdateShader, UpdateHitbox, SetTextureScaleMultiplier, ResizeCollider, spriteUnscaledSize 등)과 다시 보일 때·곡 끝에 먼저 적용. 이 맵들은 옛 방식(V15 기능 꺼짐)이라 매 프레임 도는 타일링 갱신은 바꿀 텍스처가 있을 때만 먼저 적용. Arche 258초 15ms → 6.1ms, 248.7초 5.5 → 1.5ms. 곡 끝 장식 상태(Arche 28,828개, HELLO 2,426개 x 스프라이트·마스크 2개·_sprite·원래 크기·충돌 상자·재질 키워드·감싸기 모드) 차이 0 |
| 2·3 투명 장식의 회전·크기·색 애니메이션 미루기 | 측정: Arche 곡 전체에서 1ms 넘는 길이 0 장식 이동은 6개뿐이고 회전·크기를 쓰는 것은 없다. 매 프레임 순회는 Dormancy 로 이미 평균 93개(28,835개 중), 깨움 89,822번(초당 약 290) — 하지 않음 |
| 4 SetTrans·SetRotation·SetScale 같은 값 쓰기 생략 | 깨어 있는 장식이 프레임당 약 93개라 엔진 호출 약 280번(0.03~0.06ms 추정, 0.1ms 미만) — 하지 않음 |
| 5 FloorAnim position·localScale 중복 읽기 | 위치·크기를 쓰는 큰 타일 이동 효과 시작 몇 번의 타일당 1~2번 — 효과 시작 시간 기록에서 3ms 넘는 타일 이동이 없어 하지 않음 |
남은 Arche 효과 프레임: 곡 274.8초 장식 5,091개 색 바꾸기 7ms(색 4ms, 앞부분 2ms), 279.6초 효과 85개 14ms. 나머지 20~30ms 프레임은 효과가 0개인 엔진·OS 쪽이다.
