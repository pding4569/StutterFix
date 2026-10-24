# render-merge 구현 결과

`codex-al-gm-2.md`에 따른 2회차 결과다. **이번 Codex 세션은 게임을 실행하지 않았다.** 외부 자동 장치의 1회차 실패 로그를 분석해 시작 동기화와 오류 기록을 수정했다. 게임 실행·설치·커밋·푸시·설정 파일 수정 0회. FPS·픽셀·라이브 자원 복원 게이트는 여전히 미측정이며 PASS를 주장하지 않는다.

## 2회차: 측정 전 준비 중단 분석과 수정 (2026-10-25)

### 확인한 증거

- `gm-autoloop-run1-evidence.txt`, `gm-autoloop-ledger.md`와 원시 `E:/StutterFixWork/night-results/al-gm-1.log`를 읽었다. 요약 증거에 생략된 구체적인 실패는 원시 로그 339~352행에 있다.
- `seek 260 12` → `gpudeco scheduled`(스냅샷 수집 중) → `pause` → 곡 시작 연출 종료 → `decobake ready` 실패 순서였다. 사유는 `수집 후 pitch/DOTween 배율/이징 기본값 변경: 고정 계약에서 제외`. ABBA 첫 창도 열리지 않았다. FPS 0·pixels 0·renderers 0·stability 0/4는 **측정 누락**이며 성능 0이나 픽셀 동일성을 뜻하지 않는다.
- 실제 화면은 원시 로그 340행에서 3440×1440@165로 확인됐다. 목표인 2560×1440@120과 달라 준비 오류를 해결해도 기존 `begin`에서 거부된다. 이 환경의 성능 결론을 내리지 않았고 화면 설정도 변경하지 않았다.

### 코드 원인과 수정 범위

`gpudeco scheduled`는 `collector != null || job != null || ready`만 검사한다. 이는 **PrepVfx 관찰**이지 음악 시작 완료가 아니다. 게임 근사 디컴파일 `scrController.Start_Rewind`는 `StartMusic(..., OnMusicScheduled)`를 예약하고 `hasSongStarted=false`로 둔다. 이후 `scrConductor.StartMusicCo`가 콜백을 호출한다. 체크포인트 시작의 `OnMusicScheduled`는 `hasSongStarted=true`와 `Scrub`을 수행하고, `Scrub`은 `AudioListener.pause=false`를 쓴다. 그 전에 진단 pause를 걸면 오디오 일시정지 상태가 뒤에서 깨질 수 있다. 그러면 DOTween 0을 한정 허용하는 `FrameCompare.PreservesBakeTweenScale`도 false가 되어 고정 계약 검사가 실패한다.

설치된 게임 DLL의 메타데이터를 `tools/ilscan`으로 읽어 `OnMusicScheduled → Scrub`, `Start_Rewind → StartMusic`, `Scrub → AudioListener.set_pause` 호출을 교차 확인했다. 게임 함수는 호출하지 않았다. **기존 로그는 필드별 값을 남기지 않아 오디오가 변한 순간 자체는 직접 계측되지 않았다.** 로그 순서와 실제 호출 코드가 뒷받침하는 경쟁을 제거했으며 실게임 해결 확인은 남았다.

- AutoTest 전용 `gpudeco rendermerge playbackready`: 실제 `hasSongStarted`, 편집기·컨트롤러·Hitch 재생, 오디오·시간 진행을 기다린다. 40초 상한이며 성공과 reset에서 대기 시계를 초기화한다.
- 생성기의 **모든 seek 뒤** 이 확인을 추가했다. 최초 준비·맵 재열기·선택 Mono 준비는 250초 도달 뒤 pause한다. 0초 재생 없이 ABBA 창은 계속 260~270초다. 렌더 병합 본체·정렬·픽셀·220 FPS·150렌더러·600 Draw Calls 기준은 유지한다.
- `gpudeco rendermerge environment`를 open 전에 추가했다. 2560×1440@120을 읽기 전용으로 검사하며 불일치 시 actual 값과 함께 ERROR를 낸다. begin에서도 같은 검사를 유지한다. 자동 장치 실행 환경이 목표와 맞아야 측정으로 진행한다.
- `DecoBake.Tick`의 거부 조건을 완화하지 않고 실제/고정 pitch·DOTween 배율·overshoot·period, pause 온전성·오디오·timeScale·음악 시작 상태를 실패 사유에 추가했다. `AutoTest`도 구체적인 단계 실패를 Finish에 넘겨 마지막 RENDERMERGE 오류에 보존한다.
- 생성만 한 새 JSON: `tools/optimization/out/render-merge-round2-steps.json`. tag=`rendermerge-260-270-abba-startsync2`. 자동 장치는 수정 생성기와 새 실행 이름을 사용해야 한다. 기존 코드·기존 계획의 반복 시험은 요청하지 않는다.

### 오프라인 검증과 남은 판정

Dev / Player / PlayerAuto 빌드 오류 각 0개(경고 46/4/39개, 기존 경고 포함), RenderMergeCheck 44개·FrameCompareCheck 81개·생성기 7개 통과. 생성기 검사는 맵 로드 전 환경 확인과 seek 후 실제 음악 시작 대기를 추가로 검증한다. 이전 `scheduled → pause` 순서는 거부한다. 새 JSON 생성과 `git diff --check`도 통과했다. T2 기존 이름 붙은 기록은 보존했으며 반복 측정하지 않았다.

현재 판정: `RENDERMERGE=ERROR reason=ROUND1_PREP_CONTRACT; ROUND2_GAME_NOT_RUN fps=NA pixels=NA renderers=NA`. 수정 후 FPS·픽셀·끊김·복원은 미측정이다. 이 증거로 렌더 병합의 설계 한계나 목표 달성 여부를 판정할 수 없다.

아래는 기존 구현 범위와 기존 측정에 근거한 남은 목표다.

## T1: 구간 렌더러 후보

- `GpuDecoRenderMerge.cs`: 기존 song-scope 소유권·소스 시계·컴퓨트·이벤트 packet을 유지하고 그리기 목적지만 바꾼다. 독립 렌더러와 구간 메시를 같은 준비 자원에서 전환한다. 기본 꺼짐, 일반 Player 제외.
- 정렬된 행에서 비소유 렌더러, sorting layer/order/queue/거리, Unity layer를 경계로 삼는다. 비소유 장식은 현재 숨겨져 있어도 경계에 포함한다. 소유권의 적격 조건은 기존 독립 경로를 유지한다. 모호한 동일 재질 tie는 경계 압축 전에 거부한다.
- 텍스처·재질 속성 서명별 연속 구간이 서브메시다. 재질은 해당 구간 첫 멤버의 기존 인스턴스다. 서브메시 재질 ID의 엄격한 증가와 첫 멤버의 정렬 bucket/bounds를 검사한다.
- `CopyMappedRun`으로 전체 소유 버퍼의 희소 멤버를 구간 정점 버퍼에 복사한다. 기존 번들의 커널을 사용하므로 새 셰이더 번들 생성은 필요 없다. 후보 모드에서는 독립 렌더러·native 목적지 복사를 제출하지 않는다.
- 2.5초 단위로 **250~280초 및 290~315초, 총 22개 layout**을 일시정지 준비 단계에서 만든다. 첫 범위는 260~270초와 픽셀 시험, 뒤 범위는 Arche 포탈 종료 시험용이다. 280~290초 및 이 범위 밖은 지원하지 않고 CPU 복구로 처리한다. 전곡 지원 완료가 아니다.
- 시간표는 후보 배치 힌트다. slice 진입 시 전체 실제 채널, 이후 실제 알파 변경 시 제외 멤버를 검증한다. 제외된 멤버가 표시될 가능성이 생기면 `visible_item_outside_prepared_window`로 실패하고 기존 복구 경로로 돌아간다. 따라서 시간표 위상 오차로 장식이 조용히 사라진 상태를 통과시키지 않는다.
- 두 알파 인자가 유한한 상수이며 하나가 정확히 0일 때 인덱스에서 제외한다. 이벤트별 변경 서브메시를 중복 제거하고, 멤버 마스크가 바뀔 때만 인덱스를 갱신한다. 경계에서는 미리 만든 메시를 전환하며 새 메시·GPU 버퍼를 생성하지 않는다. 이때 채널 검사와 인덱스 갱신의 실제 프레임 비용은 아직 미측정이다.
- CPU fallback은 일시정지 상태의 `rendermerge ready`에서 확인해 정렬 경계로 나눈다. 이후 fallback 구성이 달라지면 실패한다. 기존 GPU→CPU 복구 로직을 유지한다.

## 시험 생성기와 판정

```powershell
py -3 tools/optimization/render-merge-steps.py --chart '<Arche FULL.adofai 절대 경로>' --output tools/optimization/out/render-merge-steps.json
```

이 명령은 JSON만 만든다. 실행은 사용자 측 자동 장치의 새 시험 이름과 워크트리 상대 plan 경로로 한다. 이번 세션에서 `start`/게임 도구를 호출하지 않았다.

한 프로세스에서 `seek 260 12`를 사용해 A(독립)→B(구간)→B→A를 측정한다. 두 모드 모두 256초에서 일시정지 준비 후 재개하고 260~270초를 fuse/micro와 함께 측정한다. 처음 준비할 때도 0초 자연 재생을 하지 않는다.

그 뒤 새 seek로 262·270초에서 원본 O, 독립 A/N, 구간 B, 복원 A2를 캡처한다. 전체 화면 마스크 없음, A/B 픽셀 차이 0을 요구하고 O/A 대비 O/B의 변경 픽셀·최대 채널 오차 증가도 거부한다. A/N 노이즈, A/A2 픽셀·렌더 상태 복원을 함께 검사한다. 점마다 캡처를 저장하고 clear한다.

실제 retry, 맵 reopen, 300초 부근 seek 뒤 포탈 종료, 활성 소유권에서 `decobake off`를 수행한다. 마지막 항목은 모드 Unload에서도 호출하는 **DecoBake.Shutdown 경로** 검사다. UMM 전체 모드 다시 불러오기는 별도로 미측정이다. `resources mark/clean`은 구간 메시·GameObject까지 포함하며 Unity 객체 잔존 및 native token 생존을 검사한다.

`RENDERMERGE=PASS|FPS_FAIL|PIXEL_FAIL|STABILITY_FAIL|ERROR` 한 줄에 FPS 4개·픽셀·최대 렌더러·실제 엔진 Draw Calls 최대값·복원 4개 결과를 쓴다. 중도 AutoTest 오류도 ERROR 요약과 자원 해제를 시도한다.

PASS에는 2560×1440@120, 유효 ABBA 4창, 두 B창 각각 ≥220 FPS, 구간 렌더러 최대 ≤150, **ProfilerRecorder의 전체 엔진 Draw Calls 최대 ≤600**, B창 30ms 초과 0, 프레임 관측 누락 없음, 두 픽셀 검사와 복원 4개가 모두 필요하다. 누락된 증거는 ERROR다. 숫자를 인자로 주어 픽셀/성능 검사를 대체할 수 없다. 창 밖 전곡 끊김이나 물리 표시 지연까지 증명하지 않는다.

## T2: 기존 결과 보존과 도구 보완

인계 시 이미 `MonoDump.Syms`/AutoTest `monosyms`와 이름 붙은 원시 기록 `pv-260-268-on-named.txt`가 있었다. 기존 T2의 600주소 해석·상위 메서드 결과는 재측정하지 않고 보존했다. 현재 워크트리의 etwstall에는 이름 치환과 INC/LEAF 집계 코드가 없어 추가했다.

- `ETW_MONO_SYMS`: 정확한 명령 주소로만 이름을 치환한다. 근접 주소 추정 없음, 미해결 주소 보존, 스택·INC·LEAF 공통 적용.
- `ETW_MONO_ADDRS`: 분석 창의 미해결 주소 목록을 파일로 출력한다. 외부 자동 장치가 완성 파일을 원자적으로 제공해야 한다.
- `monosyms`: `0x`/`!0x` 지원, 잘못된 주소 거부, 공백 경로의 큰따옴표 지원, 게임 프로세스 시작보다 오래된 입력 거부.
- 선택 `--mono-dir <시험 전용 새 폴더>`는 기록→외부 주소 추출 대기→같은 게임에서 해석하는 단계를 추가한다. 이전 게임의 주소/심볼 파일 재사용 금지. 별도 외부 추출 장치가 없으면 이 옵션을 넣지 않는다. 운영 방법은 `tools/etwstall/README.md`에 기록했다.

## 오프라인 검증

| 검사 | 결과 |
| --- | --- |
| Dev / Player / PlayerAuto `dotnet build --no-restore` | 각각 오류 0, 경고 있음 |
| etwstall `dotnet build --no-restore` | 오류 0, 경고 있음 |
| RenderMergeCheck | 44개 통과: 정책·심볼 로더 38개, 실제 DLL의 Player/Dev/AutoTest 격리 6개 |
| 생성기 계약 검사 | 5개 통과: ABBA·픽셀·시각·일시정지 준비/전환·선택 Mono 순서 |
| `git diff --check` | 통과 |
| 기존 전체 UI 인코딩 검사 | `out/image-matrix-candidate/scrDecoration.cs`의 기존 UTF-16 파일에서 UTF-8 decode 오류. 해당 파일 수정 안 함 |

오프라인 정책 검사는 실제 게임의 FPS·픽셀·Unity 정렬·인덱스 갱신 비용·D3D 장치 복구를 증명하지 않는다. 렌더러/호출 수 목표가 실제로 안 나오면 원시 수치를 남기고 같은 코드의 반복 시험은 요청하지 않는다.

## 400 FPS까지 남은 수치

기존 같은 설정의 6.1ms(약164 FPS)에서 220 FPS는 ≤4.545ms: **최소 1.555ms, 25.5% 감소**가 필요하다. 400 FPS는 2.5ms: **3.6ms, 59.0% 감소**가 필요하다. 이번 후보의 감소량은 미측정이다.

교체 그리기를 제거한 3.83ms도 2.5ms보다 **1.33ms, 34.7%** 더 줄어야 한다. 이는 그림을 정상적으로 표시하는 구현의 성능 예상값이 아니다. 기존 named trace의 포함 표본은 PlayerRender 36.9%, CallUpdateMethod 48.3%, DOTween 10.0%, GpuLiveFrame 경로 7.7%, TightLate.List 4.4%, TightLate.Process 4.3%다. 포함 스택 비율은 겹치므로 합산하지 않는다. 구간 렌더러의 실제 결과 전에는 이 비율을 새 FPS 개선량으로 바꾸지 않는다.

최신 판정은 위 2회차 항목을 따른다. 1회차 외부 시험은 준비 오류로 중단됐으며 이번 시작 동기화 수정의 개선량은 미측정이다.
