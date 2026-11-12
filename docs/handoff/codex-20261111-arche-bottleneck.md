# StutterFix Arche 병목 인계 — 2026-11-11

## 작업 위치와 현재 상태

- 작업 폴더는 `E:\StutterFixWork\wt-v14prod`, 브랜치는 `claude/adofai-renderer`, 기준 HEAD는 `63968c1`이다. Claude의 기존 작업과 Codex 후속 수정이 워킹 트리에 함께 있다. C:의 예전 체크아웃을 사용하거나 기존 변경을 덮어쓰지 않는다.
- 마지막 시험까지 정상 종료했다. 18:31 KST 확인에서 설치 원본 **10/10**, 화면 레지스트리 **17/17**이 일치했고 게임 프로세스, `game.lock`, `user_playing.flag`가 없었다. 이어서 작업할 때는 상태를 다시 확인한다.
- 이번 턴 커밋·푸시·배포는 0회다. 설치판은 시험 전 바이트로 돌아갔다. 후보는 명시적 시험 빌드나 연구 명령에만 있다.
- 사용자는 실제 병목부터 찾으라고 요청했다. Arche 무거운 구간의 **Unity 원본 400 FPS**와 순간 끊김 감소가 목표다. 그림·정렬·충돌체·효과 의미와 복구를 보존한다. 최종 확인 전 실험을 일반 Player에 넣지 않는다.

## 지금까지 확인한 병목

실측 환경은 r150, Unity **6000.3.25f1**, D3D11, 창 모드, 바탕 화면 **2560×1440 120Hz**, 게임 창 **2560×1417**, 수직동기 0, 목표 FPS 10000, 최대 렌더 대기열 4다. 시험마다 다른 UMM 모드를 잠시 끄고 복원했다. 이전 3440 결과와 합산하지 않는다.

### Arche 자연 재생 262–274초

| 범위 | 실측 | 근거 |
|---|---|---|
| 프레임과 스레드 CPU | 296.3 FPS, 메인 **3.2341ms/프레임**, 렌더 **1.8481ms/프레임**. 400 FPS의 메인 예산 2.5ms까지 **0.7341ms**가 남는다. | [구간 요약](../../out/arche-bottleneck-262-274.json) |
| 메인 스레드 CPU 사이클 | `Update` **25.80%**, `FinishFrameRendering` **25.27%**, `LateUpdate` **18.78%**, `PlayerUpdateCanvases` **8.80%**. 완전한 PlayerLoop 213표본, 순서 거절·조회 오류 0. | [CPU/GPU 원시 로그](../../out/continue-20261111-130800-ar-reuse-gpuonly-map-on-game-cycle-profile/Player.log) |
| Update 콜백 | `scrVfxPlus.Update`는 Update 사이클의 **19.80%**, DOTween **17.67%**, conductor **8.46%**. 계측한 콜백 합계 **48.96%**다. | [콜백 원시 로그](../../out/continue-20261111-131917-ar-reuse-gpuonly-map-on-game-callback-profile/Player.log) |
| 장식 관리자 | 209표본에서 `DecoBake.GpuLiveFrame`은 LateUpdate 사이클의 **55.80%**, `TightLate.List` 자체 **9.26%**, `Dormancy.List` **0.27%**. | [관리자 분해](../../out/continue-20261111-132912-ar-reuse-gpuonly-map-on-game-manager-profile/Player.log) |
| GpuDeco 내부 | 212표본에서 `AdvanceGpuLiveSources`는 LateUpdate 사이클의 **26.75%**, 전체 메인 사이클의 **5.27%**. `UploadSourceE`는 LateUpdate의 **0.55%**, `DispatchDrawCollapse` **12.67%**, 그리기 전 검사 **6.43%**. | [GpuDeco 분해](../../out/continue-20261111-133628-ar-reuse-gpuonly-map-on-game-gpudeco-profile/Player.log) |
| GPU 타임스탬프 | 첫 카메라→프레임 끝 평균 **2.9711ms**, 메인 카메라 **2.8502ms**, 완료 444/요청 445, 오류 0. | [CPU/GPU 원시 로그](../../out/continue-20261111-130800-ar-reuse-gpuonly-map-on-game-cycle-profile/Player.log) |

사이클은 CPU 일의 상대 분포다. 시간으로 환산하지 않는다. GPU 타임스탬프는 두 지점의 경과 시간이고, 물리적 표시 FPS·입력 지연·GPU busy·Present 대기 원인은 확인 안 됨이다. 최초 계측에서 렌더 프레임마다 FixedUpdate가 한 번이라는 가정을 써 35표본만 통과했다. 수정 뒤 213표본을 사용했다. 모니터 로그의 `272.1~272.3초`는 곡 시계로 258초 직후 계측을 설치한 구간이다. 그때의 78~320ms를 262–274초 순수 재생 결과에 넣지 않는다. 곡 시계 219.60초의 GpuDeco 진행·이미지 **5.0~5.7ms**는 여러 자연 판에서 관측됐고 내부 원인은 확인 안 됨이다.

### Arche 262초 정지 장면

| 장식 표시 조건 | Unity FPS | 첫 카메라→프레임 끝 GPU 경과 |
|---|---:|---:|
| 전부 표시, 앞·뒤 대조 | 303.1 / 302.0 | 3.0725 / 3.0985ms |
| GpuDeco 프록시만 숨김 | 413.7 | 2.1463ms |
| 원래 렌더러에 남은 장식 43개만 숨김 | 397.8 | 2.2598ms |
| 두 그룹 모두 숨김 | 415.8 | 1.0531ms |

[동일 장면 원시 로그](../../out/continue-20261111-135922-ar-reuse-gpuonly-map-on-render-split/Player.log)

장식을 실제로 숨긴 진단이다. 프록시와 원래 장식 양쪽의 비용이 크며 숨김 수치를 400 FPS 달성으로 쓰지 않는다. 정렬 묶음 대조의 전체 표시 **3.0648 / 3.0742ms**에서 풀·그림자 8개를 숨기면 **2.9023ms**, 도시 배경 5개 **2.9577ms**, 별 4개씩의 두 묶음 **2.9897 / 2.9970ms**였다. 첫 세분화 판은 스프라이트 이름으로 고른 대상이 0개여서 제외했다. 실제 정렬 키와 숨김 개수를 확인한 판을 사용한다. [유효한 정렬 묶음 로그](../../out/continue-20261111-140552-ar-reuse-gpuonly-map-on-render-textures/Player.log)

## 구현과 채택 판단

| 후보 | 검증 | 상태 |
|---|---|---|
| 소스 시계 값의 CPU/GPU 업로드 배열 공유, `ClockArraysBuild=1` | Arche 정지 274초 픽셀 차이 **0**, 이징 재계산 **1,835/1,835 일치**, CPU 애니메이션 복귀 **10,015/10,015**. 캡처 없는 에디터 seek 262–274초 ABBA **295.3 / 297.9 / 299.0 / 296.5 FPS**. 기준 평균 295.9→후보 298.45 **(+0.862%)**, 메인 CPU **3.2325→3.2115ms**, 진행 구간 경과 **0.2257→0.2008ms**. | 연구 빌드 유지. 일반 채택·자연 전곡·다른 맵은 확인 안 됨. [ABBA](../../out/clockarrays-arche-abba.json) · [픽셀](../../out/continue-20261111-134753-ar-reuse-gpuonly-map-on-clockarrays-on-visual/pixels.json) |
| 투명 줄에서 원본 텍스처 샘플 생략, `spritechannels prepare rows` | Unity **6000.3.10f1** D3D11 fixture 120조건 차이 **0**, 의도 변경 대조 3/3. 첫 fixture는 128×128 타깃을 64×64만 읽어 대조가 2/3이었고 타깃 전체를 읽도록 고쳤다. 게임 262초의 28개 렌더러·10개 실제 텍스처 적용에서 픽셀 차이 **0**, 양성 대조 **2,177,015픽셀**, 복원 차이 **0**, 콜백 등록/해제 **56/56**. | **보류.** 정지 A/B/A **302.0 / 302.8 / 302.8 FPS**, GPU 경과 **3.0947 / 3.0777 / 3.0653ms**. 후보가 앞뒤 대조 사이이고 GPU 이득이 확인되지 않았다. 같은 시험을 반복할 이유가 없다. [fixture](../../out/singlearray-lab/sprite-row-result.json) · [픽셀](../../out/continue-20261111-141606-ar-reuse-gpuonly-map-on-sprite-rows/pixels.json) · [비용](../../out/continue-20261111-142010-ar-reuse-gpuonly-map-on-sprite-rows-cost/Player.log) |

앞선 이징 해시/공유 이징 후보는 각각 **−0.20% / +0.14%**, 단일 텍스처 경로와 픽셀 마스크도 실측 이득이 대조 변동과 비슷했다. 장식 목록 캐시, 단일 텍스처, 같은 알파 마스크를 근거 없이 다시 시험하지 않는다.

## 파일과 재현

- `PhaseWatch.cs`, `PhaseCycleProbe.cs`, `PhaseCallbackProbe.cs`, `StutterFix.csproj`에 AUTOTEST의 PlayerLoop 사이클·콜백 계측을 연결했다. `phasewindow callbacks on|manager|gpudeco`를 선택한 뒤 `phasewindow cpu begin/end`로 구간을 잡는다. 중첩 비용은 한 번만 집계하며 해제 때 별도 Harmony ID와 PlayerLoop를 복구한다. 일반 Player 타입 포함 0.
- `GpuDecoExport.cs`와 `StutterFix.csproj`의 `CLOCK_ARRAYS`는 `-p:Edition=Player -p:AutoTestBuild=1 -p:ClockArraysBuild=1`에서만 만들어지며 산출물은 `bin/PlayerAutoClockArrays/StutterFix.dll`이다. 기존 계산 순서, 이징, 소스 슬롯, 완료·복구를 유지한다.
- `SpriteChannelResearch.cs`, `tools/analysis/SpriteChannels.shader`, `SpriteChannels.compute`, `SpriteRowFixture.cs`는 정지 장면 연구 후보다. GPU로 실제 텍스처의 알파 유효 줄을 구하고 텍스처 자체는 바꾸지 않는다. `out/singlearray-lab/row-bundle/stutterfix_sprite_rows`는 별도 연구 번들이다. 마지막 PlayerAuto SHA-256은 `6DD997850DAC15B53FDC78BD0D22F8E28F8147699E848836D176436CCB318EB1`, row 번들은 `A91741B2DBFCC4E9511B1AEFB723B60F1F102B9BE45DF52D3D07F321AACDB573`이다.
- 마지막 DEV / Player / PlayerAuto 빌드 오류 **0/0/0**, LoadCheck 필드 이름 검사 **299/246/299**, 오류 **0/0/0**. 새 네이티브 변경은 없다. `git diff --check`에 기존 `StutterFix.cs` 끝 빈 줄이 남아 있다.
- 실행은 `py -3.12 tools/analysis/continue-runlod.py ar <kind> --windowed-startup --isolated-mods`. 지금까지 쓴 종류는 `reuse-gpuonly-map-on-game-cycle-profile`, `-game-callback-profile`, `-game-manager-profile`, `-game-gpudeco-profile`, `-clockarrays-{off,on}-perf`, `-render-split`, `-render-textures`, `-sprite-rows`, `-sprite-rows-cost`이며 앞부분 `reuse-gpuonly-map-on`은 공통이다. 출력 폴더마다 `plan.json`, 실제 DLL, `Player.log`, 원본과 복원 검사 파일이 있다.

## 이어서 할 일

1. **Update 미분해 구간과 렌더 마무리를 우선한다.** 다음 자연 262–274초에서는 DOTween 콜백 안의 게임 `TweenManager.Update`와 모드 `DecoAnim.UpdatePrefix`·`FloorAnim.UpdatePrefix`를 한 창에서 나눈다. `scrVfxPlus.Update`에서는 `FastMove.Run` 같은 효과 단위 경계부터 본다. 장식별 Stopwatch를 달지 않는다.
2. `FinishFrameRendering`의 CPU 실행과 대기는 현 자료로 구분 안 됨이다. 관리자 PerfView 에이전트가 다시 사용 가능할 때만 **같은 자연 구간**에서 ThreadTime을 찍는다. 그전에도 관리 코드 분해는 진행할 수 있다. 오래된 ETL이나 정지 장면의 GPU 수치로 현재 드라이버 병목을 확정하지 않는다.
3. 유효한 최적화는 픽셀·순서·복귀 대조 뒤 캡처 없는 자연 재생에서 확인한다. 긴 프레임은 conductor 곡 시각에 맞추고 진단 설치와 해제·CPU 복귀 프레임을 구분한다. 마지막 전체 자연 판은 400 FPS에 미달했다.

게임은 기존 `GameLock` 소유 러너만 사용한다. 사용자 게임이 있으면 새 실행·강제 종료를 하지 않는다. 게임 시험 중 빌드·DLL 교체를 하지 않고 정상 종료 뒤 설치 10개와 화면 값 17개를 복원한다. 현재 관리자 에이전트는 종료 상태다. 새 UAC·관리자 승인 우회·Steam 우회·전역 그리기 후킹·두 번째 스왑체인은 사용하지 않는다.
