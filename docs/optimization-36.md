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
|08|설계 제한 확인, 구현 보류|타일 부모·시차·좌표계 읽기 의존. 단순 평탄화·워커 Transform 쓰기 적용0|
|09|직선·스칼라 SIMD 후보 구현|네이티브 빌드·두 Release 오류0. 실제 Mono 1,048,576개 다름0. 다른 이징 미구현|
|10|정수 태그 색인 후보 구현·검증 중|순서/중복/null/변경 목록 6조건 오류0. 로딩 시 이벤트 제거 미구현|
|11|기존 휴면 목록·직접 트위너 유지|일괄 Behaviour 비활성화·모든 클로저 제거 미구현|
|12|크롭 설계 검토, 구현 미완료|공유 텍스처의 원래 크기·UV·피벗·마스크·반복 샘플링 동일성 미확인|
|13|입자 PauseAndCatchup 기본 꺼짐 후보·격자 범위 overflow 방지|두 Release 오류0. 실제 적용 대상0이라 입자 복귀 품질/비용 미확인|
|14|문자열 재질 호출45곳을 런타임 ID30개로 전환|두 Release 오류0. 정적 메시 병합·색 양자화 미구현|
|15|기존 DRS GPU 예산80%·계측 실패 폴백·Recorder 수명 정리|정책12조건/두 Release 오류0. 실제 GPU응답·게임 필터 통합 미확인|
|16|설계 검토, 구현 미완료|폰트/언어/스타일·동시 표시 수·Canvas 정렬 경계 보존 미확인|
|17|순수 진단22타입 일반 Player 컴파일 제외|세 빌드 오류0·필수 런타임6타입 유지. 전체 핫패치 병합 미구현|
|18|SHA-256·설치 트랜잭션·신규 모듈 수명 공통화|정책25조건·실제 엔진 파일4조건/수명3조건 오류0. 기존 전체 모듈 이관 미완료|
|19|맵별 화면 프로필·상위5타일·Markdown 복사 후보|프로필13조건/타일 집계6조건·실제 맵 전환6상태 오류0. 실제 완주 히트맵·UI 클릭은 미확인|
|20|메인 스레드 우선순위/MMCSS·P코어 후보|CPU 집합11조건·별도 워커 적용/복구·실제 MMCSS 적용/해제 오류0. 렌더 스레드 미구현·FPS 미측정|
|21~36|사전 감사 완료, 구현 미완료|번호별 구현·동일성·성능 검증을 계속 진행|

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
