# Codex 작업 V: 2.8.0 기능 정리 — 실험 탭 만들기·기본값 재정리·죽은 연구 플래그 제품에서 빼기 — 게임 실행·Mods 설치·커밋 금지

결과: 코드 변경 + `docs/handoff/codex-cleanup280-20261019-result.md`(**apply_patch UTF-8 BOM**, 한글 확인). 빌드 셋(Dev/Player/PlayerAuto) 오류 0. `tools/check-ui-encoding.py` 통과. `tools/LoadCheck` 통과(`./pack.sh` 의 LoadCheck 단계와 같은 검사; 직접 `dotnet build tools/LoadCheck` 후 실행해도 됨).

근거: `docs/feature-audit-2.8.0.md`(특히 "먼저 볼 요약"·"측정 빌드와 배포 빌드 차이"·"설정창 정리 제안"). 사용자 지시(2026-10-19): "실험 기능도 정리해야 함. **켤 건 켜고, 필요 없는 건 실험 탭에 박거나, 없애거나.**" 권고를 아래처럼 확정했다.

## 해야 할 일
### 1. 설정 창에 "실험" 탭 (새 페이지 아님)
감사 문서가 지적한 대로 새 페이지는 Dock/PageNames/DrawContents/DockOrder/아이콘/`subSel` 길이가 얽혀 위험하다. 대신 **플레이 페이지에 5번째 탭 "실험"** 을 추가한다(현재 플레이 탭: 기본 / 미리 준비 / 그리기 / 장식 이동, `SubDefs` 와 `PagePlay`, 탭 클램프·`subSel` 개수·`ui <페이지>.<갈래>` 번호 연동을 전부 맞출 것). 이 탭 맨 위에 한 줄 경고("기본으로 꺼져 있는 검증 중인 기능입니다. 문제가 생기면 끄세요."), 항목마다 현재처럼 이름+한 줄 설명(펼치면 자세히).
옮길 항목(= 지금 기본 꺼짐이거나 단독 이득 근거가 약한 것; 이미 다른 페이지에 **고유한 자리**가 있는 사용자-화면 항목(FrameGen·FSR·쉐이더·저사양 HalfRender)은 옮기지 않는다):
- 플레이 기본 탭에 섞여 있는 실험 후보들: 메모리 묶음 중 단독 근거 없는 것(01·31·36), native 관련(`NativeMotion`, arena, pin, `TagIndexNativePlans`), `ThreadSchedulingMode`/`ThreadRenderScheduling`은 **한 기능처럼 표시하되 기본 켬 유지**(옮기지 말고 제목만 합쳐 보이게), MMCSS=2·코어 제한(`CpuSet` 정책)·`PhysicsPause`·`TimerHalfMs`, `MapProfiles`(맵별 화면 설정 저장), `HitchHeatmap`, `FilterIdentity`, `ParticleCatchup`, `FrameGenConstantRing`.
- 감사의 "기본 켬 재검토" 5개 중 4개: `JsonStringPool`, `AsyncUploadBudget`, `TagIndex`(정수 태그 색인), `FastLane Compact` — 개별 이득 근거가 약하다(전부 "기본 켬으로 묶어 전곡 오류 0, 열기 시간 동일"). **새 설정의 선언 기본값을 false 로 바꾸고 실험 탭에 둔다.** 기존 사용자가 이미 저장한 Settings.xml 값은 유지(XML 이 새 선언 기본값보다 우선 — 감사 문서 "기준" 절) — 이 3개는 2.7.3 정식에 없던 브랜치 신규 기능이라 정식 사용자에게 바뀌는 것은 없다(코드/`git log v2.7.3..HEAD` 로 확인). `FrameStats` 는 GPU 모니터 값이 걸려 있으니 옮기지 말고 현 상태 유지.
- **기본 켬 유지**: 감사의 핵심(GcPause, DecoAnim, FloorAnim, ImagePrefetch, SkipAssetUnload, LoadCache, LeakFix, ShaderWarm, MeshWarm, SoundWarm, JitWarm, …), TightLate·EulerMemo·ColorFast·메인+렌더 스레드 우선순위, `AssetDiskCache`(감사 권고: 재방문 실측으로 기본 켬 유지, 첫 열기 쓰기 비용 한 줄 안내).
- 기본 탭의 오래된 문구("검증 중인 후보입니다. 기본으로 꺼져 있습니다")는 실제 기본값과 맞게 고친다: 기본 탭엔 기본 켬 기능만 두고 그 문구는 실험 탭으로 옮긴다.

### 2. 죽은 연구 플래그를 제품(Player)에서 빼기
`GroupStep`(`groupstep-on`), 상주 DecoAnim Batch(`decobatch-on`), `DecoEngine`(`decoengine-on`), `PosFast`(`posfast-on`), `TightLate.SkipSame`(`tightskip-on`), `UpdateEvalMemo`(`evalmemo-on`)는 측정에서 이득이 없거나 느려서 미채택이다(문서: `docs/optimization-36.md`). **정식 Player 빌드에서는 이 플래그 파일을 읽지 않고 항상 꺼진 상태**가 되게 한다: 파일 읽는 줄을 `#if DEV || AUTOTEST` 로 감싸거나(`Edition.Dev`), 정적 기본값을 false 로 고정. 코드 자체는 지우지 말고(연구 보존, 개발 빌드에서 계속 가능) **제품 동작 변경 0**임을 확인: 현재 정식 Player 기본이 이미 꺼짐이므로, 변경 후에도 같은 코드 경로가 돌아야 한다(차이는 "플래그 파일이 있어도 무시"뿐). `DecoBatch`(맵 열 때 이미지 장식 프리팹 복제)·`TextGenerator 재사용` 등 **다른 기능과 이름이 같은 로딩 쪽 기능은 건드리지 않는다.**

### 3. 검증 자료
결과 문서에: 옮긴/바꾼 항목 표(이름, 이전 → 이후 위치·기본값), 제품 동작이 바뀌지 않았다는 근거(새 설정 기본값 대비 저장값 우선, 정식 2.7.3 에 없던 기능), 인코딩/LoadCheck 결과, Claude 가 게임에서 확인할 순서(`ui 1.4`(플레이 실험 탭) + `shot`, 정식 Player 빌드 로그에 설치 오류 0, HELLO 완주).

## 규칙
- 기능 동작·성능은 건드리지 않는다(설정 위치·기본 선언·플래그 읽는 조건만). 새 코드 최소.
- 한글 문자열은 `T("한글","English")` 패턴, 설정 창 디자인 규칙 유지(`CLAUDE.md`), 깨짐 검사 필수.
- 애매하면 보수적으로 현 상태 유지하고 결과 문서에 이유를 적는다.
