# Claude 인계 — 2026-10-17

사용자가 주간 한도/유료 크레딧 때문에 새 개발·시험을 멈추고 인계 정리를 요청했다.
이 문서부터 읽고, 필요한 번호의 `docs/optimization-36.md` 최신 절만 확인한다. 긴 대화/연구 전체 재독·완료 시험 반복 금지.

## 위치·규칙

- 실제 저장소 `E:\StutterFixWork\StutterFix`, 브랜치 `codex/optimization-36`. C: 옛 복사본에서 작업하지 않는다.
- 현재 설치 Unity6000.3.21f1 알파 기준으로 계속하도록 사용자 승인받았다.
- **36개 전체 완료 아님.** 감사표/실행 상태는 `docs/optimization-36.md` 앞부분에 있다. 미구현·실패 후보를 통과로 바꾸지 않는다.
- 각 단위 Developer/Player Release 빌드, native 수정 시 `native/build-sfnative.bat`. 새 연결은 Main.Unload 복구 필수.
- 기본 꺼짐 실험. main/태그/릴리스/새 ZIP 금지. MinHook/vendor/전체 그리기 가로채기/Steam 함수 바이트 우회 금지.
- 사용자 모니터 꺼짐. 이번 실제 게임은2560×1440 120Hz. 3440 검증으로 대체하지 않는다.
- 사용자 `.codex/config.toml`9줄 변경과 무관한 untracked 파일 보존. 시험 뒤 설치3파일+boot2파일 원본 바이트 복원.

## 이번에 완료·푸시한 단위

1. `1a620c9`: 태그 계획/히트맵 두 곡 포탈 완주, 빈 다음 판에 옛 타일 표시하던 오류 수정, WinMM1ms 소유/복원 수정.
   - HELLO5,869계획/7,676값, Arche97,694계획/98,121값 시작·완료 대조 불일치0. SF오류0·포탈2/2.
   - 검사 자체79/142ms 때문에 이 완주 판을 성능 근거로 쓰면 안 된다.
   - 태그 숫자 저장 Arche끔/켬/끔390.478/437.435/435.686FPS. 대조변동11.5776%라 개선 확정0, 기본false.
   - 빈 포탈 수명8조건0, 타이머18조건0, 실제 끔→켬→중복켬→끔 소유 일치. NT0.5ms 구현 아님.
2. `7645054`: 기본false `ThreadRenderScheduling`. 네이티브 이벤트가 소유한 실제 스레드 HANDLE을 넘기고, 세대 번호로 취소된 콜백 차단.
   - 독립539조건0·수명10조건0. 실제 렌더mask63→62→63,priority0→15→0. 기존 코어 정책85/실제워커6조건0.
   - 렌더 MMCSS는 미구현: AvRevert는 등록한 스레드에서만 가능. 우선순위/코어만 렌더에 적용하며 UI에 명시.
3. `3bffe0e`: 서로 다른 MVID의 실제 활성 DLL reload. 새 lease가 재적용 전 읽은 값0·63으로 복원 확인.
   - 마지막 외부 관측0·63은 새 모듈 명시적 Shutdown 뒤 값이다. Unload와 재적용 사이 외부 샘플이라고 하지 않는다.
4. **이 인계 문서와 같은 최종 커밋**: 35번 보간 상수 버퍼 링. 아래 참고.

## 35번 최종 상태

- `FrameGenConstantRing=false`; 설정 창에 실험 선택. `constant_ring.h`, `framegen_block.h`에 소유 CB3칸(각1,760B)만 적용.
- D3D11.1 기능 확인, immediate context NO_OVERWRITE. 실행 뒤 event query, DONOTFLUSH 완료 확인 후만 재사용. 미지원/미완료/Map 실패는 원래 DEFAULT 업로드.
- Unity 버퍼/그림3장 정책/보간 수학/잠금/원래 스왑체인 변경0. `capture` bit28, Packet ABI168바이트 유지.
- 독립 색60조건61,440픽셀 차이0. 실제 보간 경로와60조건14,745,600픽셀 바이트 차이0.
- 기존 움직임 판정12/60실패는 별도 보존. 링끔도 같은 첫 실패16,648픽셀. 움직임 품질 통과로 부르지 않는다.
- D3D debug layer 없음(`debug_errors=-1`), 확인 안 됨.
- HELLO 실제 기능: 링 쓰기11,513/폴백396, 끈 뒤지원0·FG끔 연결0·SF오류0. 원본5/5복원.
- 캡처 없는 HELLO FG4배의 링끔/켬/끔 **227.3552/235.6381/237.5897FPS**.
  차이−0.8214~+3.6431%, 대조변동4.5015%. 활성1표본, 개선 채택0·기본false 유지.
- 켬 누적 쓰기54,242/폴백2,627은 곡 창 미분리·폴백 원인별 미분리.
- native/Developer/Player/PlayerAuto 빌드 오류0; 이미지600조건/계산4,194,304개/렌더 핸들539조건 회귀 오류0; LoadCheck212곳0; 일반 진단36타입 제외/필수6타입 유지.
- 증거 `tools/optimization/validation/35-ring*.json`, `35-constant-ring-game.json`. 이미 끝난3성능 판 반복 금지.
- 마지막 시험 DLL SHA `0ddef79a54d58bef2e5f3673e07a382e5f0b6b71c36466a235b14350defcc61b`, native `489f022171ed961fc42bc3c3a55b164a650244c022ccce08d8dc8bb97c304824`.

## 남은 작업·차단 이유

- 35 Arche 성능/전체 곡/실제3440/표시 지연은 확인 안 됨. 성능 판 동안 빌드·벤치·캡처 병행 금지.
- 21 재질/COW는 동률 정렬36조건 중30조건 불일치로 **일반 차단 유지**. 보조57,656렌더러는 이미 비활성, 추가 끄기0.
- 12 크롭은 실제GPU16조건 중12조건·171픽셀 불일치로 **채택 금지**. 경계 SIMD만 구현. 새 크롭/희소 텍스처 대안은 구현하지 않았다.
- 08 Arche부모깊이 이미1. Transform Jobs 미구현; 시차/읽기/콜백 순서 보존 근거 필요.
- 10 이벤트 제거는 Kill(true)/콜백/상대 쓰기 순서를 바꿀 수 있어 미구현.
- 11/14/16 일괄 컴포넌트 비활성/색 양자화/판정16슬롯 교체는 동일성 미검증. 활성124형식의 완전히 빈Update0, 판정풀1,400개.
- 18 신규 모듈만 공통 수명화. 기존 전체 모듈 이관 미완료. Harmony ID/다른 모드 사이 순서를 임의 변경하지 않는다.
- 20 렌더 MMCSS/FPS 이득,22 실제 물리 키 입력,24 DSP64/128·위상 제어 미검증.
- 26 SwapEffect는 기존 swapchain의 ResizeBuffers로 변경 불가. 원래 swapchain만 사용. 기존큐1은FPS/지연 악화 근거 있어 재도입0.
- 27 Unity프레임 끝 fence/queue0 미구현. 생성그림/상수버퍼 query와 혼동하지 않는다.
- 28 Collider/타 모드 FixedUpdate 보존 때문에 전역 물리/FixedDelta100 미적용.
- 29 실제 Boehm이므로 SGen nursery 옵션 작동 안 함.30 NT0.5ms 미구현.31 소유 arena만 lock, 전체 관리힙 보장 아님.
- 32 NT store/prefetch,25 FP16,33 전체 키워드 변형 예열 미구현/미검증.36 정수 계획만 비관리이며 Unity객체 GC루트 제거 아님.

## 이어받을 때 최소 확인

1. E:에서 git status/log. 사용자 설정 변경 보존. 새 게임/시험은 사용자가 Claude에서 재개할 때만.
2. 이 문서+감사표로 번호 선택 후 관련 파일만 읽기. 미확인/차단 조건을 무시한 강제 구현 금지.
3. 의미 있는 단위만 한국어 커밋·현재 작업 브랜치 푸시. 배포하지 않는다.

도구: `tools/optimization/build-step.ps1 -Step 고유이름 -AutoTest`; 완료 로그를 덮지 않는다.
실행 스크립트/전체 로그는 ignored `tools/optimization/out/`, 공개 요약은 `validation/`에 있다.
Python은 `C:\Users\나로컴\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe`.
