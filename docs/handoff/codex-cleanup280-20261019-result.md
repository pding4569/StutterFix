# 2.8.0 기능 정리 결과 — 2026-10-19

인계: `codex-cleanup280-20261019.md`. 코드 변경과 오프라인 검증 완료. 게임 실행·Mods 설치·커밋·푸시·패키징은 하지 않았다. 자동 build-install 훅과 `pack.sh`도 실행하지 않았다. 시작 시 존재한 `.codex/config.toml`, README, CHANGELOG 및 미추적 파일은 보존했다.

## 설정 창 변경

플레이 페이지에 다섯 번째 **실험** 탭을 추가했다. 자동 시험 주소는 `ui 1.4`다. `SubDefs`의 제목/아이콘은 각각 5개, `PagePlay` 분기는 0~4이며 `ShowForTest`도 해당 페이지의 실제 탭 개수로 제한한다. 페이지 수는 여전히 9개이므로 `subSel[9]`, Dock, PageNames, DrawContents와 페이지 번호는 그대로 유지했다.

실험 탭 첫 문구는 `기본으로 꺼져 있는 검증 중인 기능입니다. 문제가 생기면 끄세요.`다. 기존 Option의 이름·한 줄 설명·펼치기·종속 안내 방식을 사용하며 새 한글은 `T("한글", "English")`로 작성했다.

아래 기본값은 **저장 XML에 필드가 없는 경우**다. 저장된 true/false는 우선한다.

| 항목 / 필드 | 이전 위치·기본값 | 이후 위치·기본값 |
|---|---|---|
| 메모리 한도 자동 조절 / GcDynamicBudget (01) | 플레이 기본 / false | 플레이 실험 / false |
| 맵 문자열 재사용 / JsonStringPool | 플레이 기본 / true | 플레이 실험 / **false** |
| 로딩 업로드 예산 / AsyncUploadBudget | 플레이 기본 / true | 플레이 실험 / **false** |
| 미세 위치 쓰기 생략 / MoveEpsilon | 플레이 기본 / false | 플레이 실험 / false |
| 장식 숫자 계산 묶기 / NativeMotion | 플레이 기본 / false | 플레이 실험 / false |
| 비관리 계산 버퍼 / NativeMotionArena | 플레이 기본 / false | 플레이 실험 / false |
| 계산 버퍼 RAM 고정 / NativeMotionPinArena (31) | 플레이 기본 / false | 플레이 실험 / false |
| 장식 태그 색인 / TagIndex | 플레이 기본 / true | 플레이 실험 / **false** |
| 비관리 태그 번호 / TagIndexNativePlans (36) | 플레이 기본 / false | 플레이 실험 / false |
| 입력 프레임 대기 보정 / FrameQueueDelay | 플레이 기본 / false | 플레이 실험 / false |
| 입자 복귀 따라잡기 / ParticleCatchup | 플레이 기본 / false | 플레이 실험 / false |
| MMCSS / ThreadSchedulingMode=2 | 기본 탭의 3단 선택 / 기본 mode=1 | 실험 탭의 오디오 방식 스위치 / 기본 꺼짐; 끄면 mode=1 |
| 고성능 코어만 / ThreadPerformanceCores | 플레이 기본 / false | 플레이 실험 / false |
| 0번 논리 코어 제외 / ThreadAvoidLogicalZero | 플레이 기본 / false | 플레이 실험 / false |
| 물리 시뮬레이션 중지 / PhysicsPause | 플레이 기본 / false | 플레이 실험 / false |
| 맵별 화면 설정 / MapProfilesEnabled | 플레이 기본 / false | 플레이 실험 / false |
| 끊긴 타일 기록 / HitchHeatmap | 플레이 기본 / false | 플레이 실험 / false |
| 중립 필터 건너뛰기 / FilterIdentity | 플레이 미리 준비 / false | 플레이 실험 / false |
| 타이머 0.5ms / TimerHalfMs | 저사양 컴퓨터 / false | 플레이 실험 / false |
| 보간 업로드 버퍼 / FrameGenConstantRing | 그래픽의 FrameGen 하위 / false | 플레이 실험 / false |
| FastLane Compact 생략 / **DecoFastCompact** | 사용자 UI·Settings 필드 없음 / 정적 FastCompact=true | 플레이 실험 / **새 XML 필드 false** |
| 메인·렌더 우선순위 / ThreadSchedulingMode, ThreadRenderScheduling | 플레이 기본 / 1, true | 플레이 기본의 공통 제목 아래 유지 / **1, true 유지** |
| 디스크 캐시 / AssetDiskCache | 플레이 기본의 실험 묶음 / true | 플레이 기본 / **true 유지**, 실험 표기 제거·첫 열기 쓰기 비용 안내 추가 |

기본 탭의 메인·렌더 우선순위 제목을 한 기능으로 묶었다. 일반 선택에는 끔/우선순위만 남겼고 MMCSS=2 선택은 실험 탭으로 옮겼다. 저장된 mode=2는 탭을 보기만 해서는 변경하지 않는다. 메인·렌더의 개별 저장 필드와 적용·복구 로직은 유지했다.

FastLane은 기존 `fastlane-off` 차단을 유지하면서 `DecoFastCompact`를 기존 `FastCompact`에 연결했다. 후보 cache/inline 경로와 Compact 알고리즘은 바꾸지 않았다. 기존 Compact는 XML 필드가 없었으므로 이전 정적 true가 새 false 기본값으로 전환되는 것은 요청된 명시적 변경이다.

핵심 기능(GcPause, DecoAnim, FloorAnim, ImagePrefetch, SkipAssetUnload, LoadCache, LeakFix, ShaderWarm, MeshWarm, SoundWarm, JitWarm 등), TightLate, EulerMemo, ColorFast, FrameStats는 유지했다. FrameGen 본체·FSR·쉐이더·저사양 HalfRender의 고유 위치도 유지했다. 01·31·36은 감사 표의 필드 번호가 아니라 `optimization-36.md`의 연구 항목 번호로 해석했다. 따라서 근거가 있는 AssetDiskCache·RecolorDefer는 옮기지 않았다.

## 연구 플래그의 Player 격리

아래 플래그 읽기는 `#if DEV || AUTOTEST` 안으로 제한했다. 일반 Player에서는 해당 활성 상태를 false로 초기화한다. UpdateEvalMemo는 false 초기화 뒤 활성화 검사 블록 전체를 제외했다. TightLate.SkipSame의 정적 선언도 false로 맞췄다.

| 연구 기능 | 제외한 플래그 | 수정 파일 |
|---|---|---|
| GroupStep | groupstep-on | DecoAnim.GroupStep.cs |
| 상주 애니메이션 Batch | decobatch-on | DecoAnim.Batch.cs |
| DecoEngine | decoengine-on | DecoEngine.cs |
| PosFast | posfast-on | PosFast.cs |
| TightLate.SkipSame | tightskip-on | StutterFix.cs, TightLate.cs |
| UpdateEvalMemo | evalmemo-on / evalmemo-off | UpdateEvalMemo.cs |

일반 Player의 6개 연구 활성 플래그 문자열은 최종 DLL 검사에서 **0개**, Dev/PlayerAuto는 각각 **6개**였다. 연구 코드는 삭제하지 않았다. 별도 파일 `DecoBatch.cs`의 맵 로딩 프리팹 복제, TextFix의 TextGenerator 재사용은 변경하지 않았다.

제품 동작 동일성 주장은 **이 6개 연구 경로의 기존 기본 꺼짐 상태**에 한정한다. 기존 Player도 파일이 없으면 false였고, 이제 파일이 있어도 false다. 기능 알고리즘·패치 설치 방식·프레임별 처리에는 변경이 없다. 위 표의 기본값 4개 변경과 연구 파일 무시는 의도된 정책 변경이므로 모든 설정에서 완전히 동작이 같다고 주장하지 않는다.

## 저장값과 2.7.3 확인 근거

- 시작 시 `ModSettings.Load<Settings>`로 읽고, 세 기존 필드(JsonStringPool·AsyncUploadBudget·TagIndex)를 새 기본값으로 덮어쓰는 마이그레이션은 추가하지 않았다.
- 최종 Player DLL의 실제 Settings 타입을 생성하고 XmlSerializer로 검사했다. 네 필드의 새 선언 기본값 false **4/4**, XML에 저장한 false/true 우선 **8/8**, 누락 XML 필드의 false 기본값 **4/4** 통과했다. 실제 사용자 Settings.xml에는 접근하거나 쓰지 않았다.
- `git show v2.7.3:StutterFix.cs`에서 세 기존 필드 선언이 모두 없음을 확인했다. `git log v2.7.3..HEAD -S`에서 문자열 풀·업로드 예산 추가는 `91571e7`, 태그 관련 필드는 `de6e5ac`·`1f9fd92` 이력으로 확인했다.
- `git ls-tree -r --name-only v2.7.3`에도 `DecoAnim.FastLane.cs`가 없다. 기존 정식 2.7.3에 있던 세 설정을 강제로 끄는 변경이 아니다. 브랜치 사용자에게 이미 저장된 세 필드 값은 보존된다.

## 검증 결과

| 검사 | 결과 |
|---|---|
| Dev / `dotnet build StutterFix.csproj -v q --nologo -c Debug` | 오류 **0**, 경고 35 |
| Player / 위 명령 + `-p:Edition=Player` | 오류 **0**, 경고 33 |
| PlayerAuto / 위 명령 + `-p:Edition=Player -p:AutoTestBuild=1` | 오류 **0**, 경고 35 |
| `dotnet build tools/LoadCheck -v q --nologo -c Release -o tools/LoadCheck/bin` | 오류 **0**, 경고 0 |
| LoadCheck Dev | 이름 참조 **259곳**, 틀림 **0**, 동적 이름 5곳 |
| LoadCheck Player | 이름 참조 **227곳**, 틀림 **0**, 동적 이름 5곳 |
| LoadCheck PlayerAuto | 이름 참조 **259곳**, 틀림 **0**, 동적 이름 5곳 |
| 실제 DLL 연구 플래그 검사 | Dev **6/6**, Player **0/6**, PlayerAuto **6/6** |
| 실제 Settings 타입/XML 검사 | 기본값 **4/4**, 저장값 우선 **8/8**, 누락 기본값 **4/4** |
| UI 소스 연결 검사 | 실험 제어 **21개** 모두 1.4에 한 번씩 존재, 기본 제어 3개 유지, 제목/아이콘 각 5개 |
| `py -3 tools/check-ui-encoding.py` | **SFENC001: clean** |
| `git diff --check` | 통과 |

빌드 경고에는 System.IO.Compression 참조 버전 충돌과 미사용 필드 등이 포함된다. LoadCheck의 동적 이름 5곳은 해당 검사로 확정할 수 없어 게임 확인이 필요하다. 게임 성능·전체 곡 동일성·설치 오류 0을 이번 작업에서 새로 입증한 것은 아니다.

최종 빌드 로그는 `%TEMP%/cleanup280-Dev-final.log`, `cleanup280-Player-final.log`, `cleanup280-PlayerAuto-final.log`에 있다. 일회성 XML/메타데이터 검사 도구는 `%TEMP%/cleanup280-check`에 두었으며 제품 프로젝트에는 추가하지 않았다.

이 결과 문서는 `apply_patch`로 UTF-8 BOM을 포함해 작성했다. 시작 바이트 `EF BB BF`, 엄격한 UTF-8 디코딩 성공, 대체 문자 U+FFFD 0개, 주요 한글 문구 **5/5 일치**를 확인했다.

## Claude의 후속 게임 확인 순서

1. 별도 권한 범위에서 새 PlayerAuto를 설치하고 게임을 실행한다. `ui 1.4`와 `shot cleanup280-play-experimental`로 실험 탭을 연다. 스크롤하며 한국어/영어 이름·설명 펼치기·native/태그 종속 안내를 확인한다. 기본·미리 준비·그리기·장식 이동 탭도 기존 번호 1.0~1.3으로 접근한다.
2. 새 설정에서 실험 기본 꺼짐, 메인·렌더 우선순위와 디스크 캐시 기본 켬을 확인한다. 기존 XML의 세 필드 true 및 mode=2가 탭 열기만으로 변경되지 않는지 확인한다. 실험 옵션 변경 후 저장·재실행·복구를 확인한다.
3. 일반 정식 Player 빌드로 새로 실행해 `[StutterFix]` 설치 오류 **0**을 확인한다. 연구 on 플래그가 남아 있어도 위 6개 경로는 비활성인지 확인한다. FrameStats/GPU 모니터도 유지되는지 본다.
4. HELLO 2026을 완주해 오류·끊김 로그를 확인한다. 정확한 비교가 필요하면 같은 기존 XML과 화면 조건으로 검사하고, 이번 오프라인 성공을 성능 향상으로 해석하지 않는다.
