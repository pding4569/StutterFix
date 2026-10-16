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
|07~36|사전 감사 완료, 구현 미완료|07의 local 위치·world 회전을 무조건 합치지 않음|

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
