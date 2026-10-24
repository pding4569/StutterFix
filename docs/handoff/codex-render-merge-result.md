# render-merge 구현 결과

## 현재 회차 3: run2 실제 ABBA 실패 분석과 map5 수정 (2026-10-25)

**외부 run2 판정은 `RENDERMERGE=ERROR`, 이번 map5는 `RENDERMERGE=ERROR reason=GAME_NOT_RUN`이다.** 게임 실행·설치·커밋·푸시·자동 장치 호출·설정 변경은 하지 않았다. 지정 문서의 05:45 보충과 최신 `gm-autoloop-run2-evidence.txt`를 우선했다. 아래 과거 환경 오류·소유 26개 설명을 이번 중단 이유로 사용하지 않는다.

### run2 실측: 소유 범위는 회복했지만 병합은 실패

3440×1440@164.999, GPU 소유/할당 23,547개·CPU fallback 0개로 ABBA 네 창이 모두 진행됐다. A 직전 독립 enabled/visible은 1,880개다. 요약의 실제 결과 11줄(창 4·픽셀 2·안정성 3·자원 mark·최종 판정)을 현재 원시 `E:/StutterFixWork/night-results/al-gm-2.log`와 대조해 전부 일치함을 확인했다.

| 창 | FPS | 할당 병합 렌더러 최대 | 엔진 Draw Calls 최대 | 30ms 초과 | 최악 ms |
|---|---:|---:|---:|---:|---:|
| A0 | 166.844797 | 해당 없음 | 2479 | 0 | 15.757298 |
| B1 | 77.992570 | 4875 | 2489 | 3 | 75.452797 |
| B2 | 77.943161 | 4875 | 2527 | 4 | 112.754099 |
| A3 | 161.787018 | 해당 없음 | 2524 | 0 | 27.100297 |

이전 코드의 `renderers` 계측은 활성/화면 가시 수가 아니라 구간에 할당한 `RunCount` 최대였다. B의 비어 있지 않은 교체 서브메시 최대는 1,980개다. 준비 로그는 모든 구간에서 `items=renderers=submeshes`로, 병합이 실제로 한 항목당 한 렌더러로 쪼개졌다. 예: 260초 구간 4,580개, 262.5초 4,632개, 270초 4,875개. 소유 26개였던 run1과 달리 이번은 유효한 목표 모집단의 **실패**다.

262초 원본↔독립 차이는 0, 원본↔병합/독립↔병합은 **439,069픽셀·최대 33/255**다. 270초는 원본↔독립 0, 병합 차이 **3,207픽셀·최대 2/255**다. 두 시각 복원/노이즈는 0이다. 따라서 픽셀 증가 금지 게이트도 실패한다. retry/song_end/reopen은 각각 pass, 마지막 Shutdown 자원 검사 읽기에서 ERROR로 끝나 **stability=3/4**, 전체 unload/reload는 미측정이다. ERROR가 성능·픽셀·끊김 실패를 없애 주지는 않는다.

### 코드에서 찾은 문제와 수정

1. **휴면 보조 렌더러도 비소유 경계로 취급.** `GpuDecoExport`의 merge용 스캔은 나중에 깨어날 CPU 객체를 보존하려고 비활성 렌더러까지 수집했다. 이때 지원 판정을 받은 장식의 `meshRenderer`·`bordersRenderer`·`hitboxRenderer`도 일반 경계가 됐다. 게임 근사 디컴파일 `scrVisualDecoration.UpdateShader/EnableMeshRenderer/SetDepth`와 설치 DLL ILScan을 읽어, mesh 전환이 Renderer.enabled가 아닌 GameObject.SetActive로 동작하고 sprite/mesh가 같은 sortingOrder를 받는 호출 관계를 확인했다. 기존 LiveGpuReason은 mesh-mode·filter·mask·editor-borders를 거절하므로, **이미 소유가 허용된 단순 스프라이트의 휴면 보조 객체만** 경계에서 제외한다. CPU fallback/알 수 없는 비활성 객체는 그대로 보존한다. 사전 압축 때 이 보조 행과 실제 경계를 합쳐 버리지 않도록 구분하며, ready/on에서 보조 객체가 실제로 켜졌다면 거부한다. 제외 개수와 실제 경계·bucket/layer/material별 분할 개수를 준비 로그에 추가했다. **run2에 분할 사유별 로그는 없어, 모든 singleton의 원인이 이 하나라고 확정하지 않는다.**
2. **기록된 정점 버퍼를 얻은 뒤 CPU 메시 수정.** 기존 `RefreshGpuMergeGroup`은 알파 이벤트·구간 진입마다 Mesh.SetIndices를 호출했다. Unity는 CPU 메시 데이터를 수정하면 GPU 정점 버퍼가 재생성될 수 있고 GetVertexBuffer를 다시 받아야 한다고 명시한다([Unity Mesh.GetVertexBuffer](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Mesh.GetVertexBuffer.html)). 이 경로는 미리 기록한 CopyMappedRun의 목적지 참조를 보장하지 못한다. 정점·인덱스 topology와 command buffer를 준비 때 고정하고, 별도 StructuredBuffer 복사표만 변경하도록 수정했다. 숨은 장식은 네 정점을 같은 소스 첫 정점으로 복사해 삼각형을 퇴화시킨다. 보이는 장식은 기존 네 모서리·UV·색·순서를 유지한다. 보이는 개수가 같고 멤버만 바뀌어도 복사표를 갱신한다. 전부 숨은 run의 Renderer는 끄고 활성 run 수를 이벤트마다 집계한다. **run2에서 버퍼 재생성 자체를 계측한 것은 아니므로, 픽셀 실패의 전체 원인 귀속은 아직 가설이다.** 셰이더·번들 변경은 없다.
3. **자원 mark의 쓰기/읽기 상한 불일치.** run2는 Unity 객체 **111,382개**를 정상 기록했지만 reader는 100,000개를 넘으면 거부했다. 이는 이 로그의 마지막 ERROR를 설명하는 확정 코드 경로이며 누수 검출 결과가 아니다. 기존 1MiB 파일 상한은 유지하고 객체 수×4+GUID/플래그 17바이트가 실제 남은 파일 길이와 정확히 같은지 검사한다. writer도 같은 파일 상한·token 상한을 검사한다. 111,382개, 잘린 파일, 뒤에 붙은 데이터, 비어 있거나 과대한 수를 오프라인 검사했다. 실제 `unity_alive=0/native_alive=0`은 아직 확인되지 않았다.

새 코드는 연구용 기본 꺼짐이다. 재생 중 메시 할당 금지·정렬 bucket/엄격 재질 ID 순서·실제 알파에 의한 누락 검사·GPU 복구/소유 기준은 유지한다. 알 수 없는 렌더러를 일괄 제외하거나 FPS/픽셀 게이트를 낮추지 않았다. 보조 렌더러가 지원 계약 밖의 외부 변경으로 재생 중 켜지는 경우까지 이번 오프라인 검사로 증명한 것은 아니다.

### 생성물과 검증 범위

- 생성기 tag `rendermerge-260-270-abba-map5`, 새 계획 **`tools/optimization/out/render-merge-map5-steps.json`(333단계)**. run2가 사용한 실제 Arche 파일 존재를 확인해 생성했다. 최초 곡 시작 준비 후 정지, 측정은 seek 260 기반·ABBA 진입 pause 없음, 262/270초 픽셀·retry/song_end/reopen/Shutdown 절차를 유지했다. 생성만 했고 기존 JSON을 덮어쓰거나 자동 장치를 호출하지 않았다.
- Dev / Player / PlayerAuto 빌드 **오류 0**, 경고 **46/36/39**. 생성기 계약 **11개**, RenderMergeCheck **74개** 통과(게이트·심볼·보조 경계·복사표·파일 길이 68 + 실제 3종 DLL 격리 6). hidden/visible 왕복·동일 개수 멤버 교체·비연속 source·이웃 그룹 보존·퇴화 삼각형·CPU fallback/미확인 경계 보존을 검사했다. 빌드·검사 로그와 ILScan은 `tools/optimization/out/render-merge-map5-*`, `render-merge-run3-ilscan.txt`에 있다.
- 변경 파일 13개 UTF-8 읽기·리터럴 내 대체문자 부재, 변경 C# 5개의 기존 인코딩 검사기 `SFENC001: clean`, 요약 정확히 10줄, 문서의 새 계획 경로 일치, `git diff --check`를 확인했다. 과거 UTF-16 산출물을 포함한 전체 인코딩 검사는 반복하지 않았다. 기존 빌드 경고는 남아 있다.
- 이번 수정은 실제 정렬·픽셀·GPU 버퍼 수명·실시간 FPS·끊김·수명 복원을 증명하지 않는다. map5의 해당 수치는 전부 **NA**이며 PASS가 아니다. run2로 설계 전체의 불가능을 확정하지 않으며 같은 코드의 반복 시험도 요청하지 않는다. 후속 외부 결과는 `omitted_admitted_auxiliaries/split_*`와 **할당 renderer 및 활성 renderer를 구별해** 읽어야 한다. 새 `rendermerge_window renderers`는 알파상 하나 이상 살아 있는 run의 최대이며, 준비 로그 `rendermerge_prepared renderers`는 할당 run 수다. 실제 Draw Calls 게이트는 계속 엔진 계측값을 쓴다. 퇴화 서브메시도 제출될 수 있으므로 `replacement_submeshes`는 살아 있는 그룹 수이지 실제 Draw Calls가 아니다.

T2 기존 `monosyms`·`ETW_MONO_SYMS`와 이름 붙인 기록을 보존했다. 기존 메인 포함 3,846표본: PlayerRender 36.9%, CallUpdateMethod 48.3%, GpuLiveFrame 7.7%, TightLate.List 4.4%, TightLate.Process 4.3%, FastMove.Prefix 3.7%(포함 비율 중첩, 합산 금지). 과거 **2560 환경**의 6.1→4.545ms(220 FPS)는 −1.555ms, →2.5ms(400 FPS)는 −3.6ms; 교체 그리기 제거 3.83ms에서도 −1.33ms다. 이번 3440 run2와 섞지 않으며 map5 개선량은 NA다.

task-observer 3.5.0과 기존 사용자 범위 관찰 헤더를 확인했다. 이번 작업은 허용된 worktree 안에서만 기록하며 사용자 범위 스킬/메모리 파일은 수정하지 않았다. 아래는 이전 회차다.

## 이전 회차 2: run1 소유 범위 오류 수정, 당시 수정판 실측 없음 (2026-10-25)

**기존 외부 run1은 `RENDERMERGE=ERROR`, 새 schedule4 수정판은 `RENDERMERGE=ERROR reason=GAME_NOT_RUN`이다.** 게임 실행·설치·커밋·푸시·자동 장치 호출은 하지 않았다. 05:45 추가 인계의 곡 처음 준비 절차를 반영했다. 아래 이전 회차의 환경 불일치·실측 없음 설명은 과거 상태다.

### 지정된 run1에서 실제로 수집된 수치

증거: `gm-autoloop-run1-evidence.txt`(3440×1440@164.999). ABBA 4창·픽셀 2점은 실제로 수집됐다. 이 결과를 예전 2560 환경이나 소유 23,547개 기준과 섞지 않는다.

| 창 | FPS | 소유 / 독립 enabled | 합친 렌더러 | 엔진 Draw Calls 최대 | 30ms 초과 |
|---|---:|---:|---:|---:|---:|
| A0 | 104.609374 | 26 / 0 | 해당 없음 | 2336 | 0 |
| B1 | 104.533949 | 26 / 0 | 6 | 2307 | 0 |
| B2 | 104.176331 | 26 / 0 | 6 | 2318 | 0 |
| A3 | 104.259526 | 26 / 0 | 해당 없음 | 2305 | 0 |

모든 census에서 소유 26개 전부 `proven_constant_zero_alpha=26`이었다. 262/270초의 원본↔독립↔병합·복원·노이즈 픽셀 차이는 모두 0이지만, 보이는 GPU 교체 대상이 0이므로 목표 장면의 픽셀 동등성 증거로 수용하지 않는다. retry만 통과(stability=1/4); song_end용 `seek 300 12` 후 activation에서 GPU 24/CPU fallback 2가 되어 cached ready가 거부됐다. **B1/B2의 측정 전환은 통과했으며 실패는 곡 끝 안정성 준비 단계다.** reopen/Unload 결과는 수집되지 않았다. 이는 렌더 병합 설계 한계나 220 FPS 불가능의 증거가 아니다.

### 원인과 수정

- `GpuDecoExport.cs`에서 `gpuScope.WholeSong`은 가시성 시간표 필터를 건너뛴다. 따라서 `window_visible=34`를 단순히 화면 안 장식 34개라고 해석하면 안 된다. 코드상 먼저 굽기 거절 사유가 없는 항목만 남긴 뒤 세는 후보 수다.
- `scnGame.PrepVfx`는 정렬된 예약 목록을 만든다. 이후 `scrVfxPlus.ScrubToTime`은 시작된 효과를 같은 목록에서 제거한다. 기존 `DecoBake.Collect`는 그 목록을 yield하며 읽고, 개수 변경 시 재시작해 줄어든 목록으로 기준을 만들 수 있었다. 과거 효과가 `ordered`에서 빠지면 조건/수동 제외 대상이 되어 소유 후보가 줄어드는 코드 경로다. 디컴파일본을 읽고 설치 게임 DLL의 ILScan으로 PrepVfx→Reset/OrderBy/ThenBy/ToList 및 ScrubToTime→효과 ScrubToTime/List.Remove 호출을 교차 확인했다. 개별 장식별 거절 집계까지 이 로그에 없으므로 26개의 모든 원인을 확정한 것은 아니다.
- `DecoBake.cs`/`DecoBake.Core.cs`: PrepVfx postfix에서 예약 순서·참조를 즉시 복사해 수집기가 읽는다. 재수집은 이 스냅샷을 유지하며 잘린 live 목록을 새 기준으로 삼지 않는다. 새 PrepVfx는 새 스냅샷으로 교체하고 ForgetLevel/Shutdown에서 해제한다. 실제 live 예약 목록은 adopt/packet 검증용으로 그대로 둔다. 조건/태그/정렬·소유 거절 정책을 완화하지 않았다.
- 생성기: 05:45 지시대로 `select 0 → play → scheduled → playbackready → pause → decobake ready → scope song → rendermerge prepare/live prepare → resume → stop`으로 최초 준비한다. 이후 측정은 전부 `seek 260 12` 기반이며 ABBA 진입 pause/resume은 없다. 곡 0초에서 260초까지 자연 재생하지 않는다.
- ABBA 전에 별도 seek로 256초 activation을 만들고 pause→ready로 fallback 경계를 준비한다. live off로 CPU 소유를 복원한 뒤 resume/stop한다. 이 준비 절차는 측정 창 밖이다. 픽셀/곡 끝 검사 후 256초 retry/reopen으로 돌아갈 때와 reopen/선택 T2 준비에도 같은 순서를 적용한다. 픽셀은 독립 경로로 진입하고 해당 시각에 멈춘 뒤에만 ready를 검사한다. 재생 중 메시 할당 제한과 복구 가드는 유지한다.
- song_end의 activation은 실제 296초 fallback을 반영하도록 live on 직후 pause→ready→on→resume한다. 이 경계의 pause/resume 연속성은 실게임 미검증이며, FastMove 복구나 새 fallback 불일치가 생기면 그대로 실패한다.
- 생산 수용 게이트: 각 ABBA 창은 GPU 소유 **20,000개 이상**, A의 독립 enabled **1개 이상**, 네 창의 allocated/owned/CPU fallback 개수 일치를 요구한다. 모집단 증거가 없으면 PASS 불가다. 이는 26개·전부 투명인 무효 실험을 조기에 거부하기 위한 추가 조건이며, 기존 FPS ≥220·렌더러 ≤150·Draw Calls ≤600·픽셀·끊김·복원 기준도 모두 유지한다. enabled 검사는 화면 안 가시성 보장과 같지 않으므로 census/visible 수 역시 함께 검토해야 한다.

새 계획: **`tools/optimization/out/render-merge-schedule4-steps.json`**, tag `rendermerge-260-270-abba-schedule4`, 333단계. 생성만 했으며 기존 계획·제어 파일·`.codex/config.toml`은 변경하지 않았다. 다른 작업에서 추가된 인계문서 05:45 보충 2개도 보존했다.

### 오프라인 검증과 남은 항목

Dev/Player/PlayerAuto 빌드 오류 0(최종 로그 경고 4/36/39; 증분 빌드 수치). 생성기 계약 **11개**, RenderMergeCheck **54개**(정책/정확 주소 심볼 48 + DLL 격리 6), 실제 굽기 코어 계약 검사 전체 통과(예약 목록 제거·순서 교체·재수집·상한 관련 5개 추가). 로그는 `tools/optimization/out/render-merge-schedule4-{dev,player,playerauto,contract}.log`다. `git diff --check` 확인. 별도 전체 UI 인코딩 검사는 기존 `out/image-matrix-candidate/scrDecoration.cs`의 UTF-16 파일 때문에 예외 종료했다. 변경한 C# 소스 6개로 범위를 제한해 동일 검사기를 실행한 결과 UTF-8 읽기·리터럴 검사 `SFENC001: clean`을 확인했다. 해당 외부 산출물은 수정하지 않았다.

**수정판 소유 수·FPS·픽셀·실제 정렬·새 끊김·retry/song_end/reopen/Shutdown 복원은 모두 NA다.** 특히 full population에서 cached fallback이 재현 가능하게 일치하는지와 296초 resume을 확인해야 한다. UMM 전체 unload/reload도 미측정이다. 같은 코드의 반복 실행을 요청하지 않는다.

T2 기존 `monosyms`/`ETW_MONO_SYMS`/정확 주소 매칭과 이름 붙은 기록은 보존했다. 메인 포함 3,846표본: PlayerRender 36.9%, CallUpdateMethod 48.3%, GpuLiveFrame 7.7%, TightLate.List 4.4%, TightLate.Process 4.3%, FastMove.Prefix 3.7%(포함 비율은 겹치므로 합산 금지). 기존 2560 환경의 6.1ms에서 220 FPS까지 −1.555ms, 400 FPS까지 −3.6ms; 교체 그리기 제거 3.83ms에서도 400 FPS까지 −1.33ms가 남는다. 이는 새 후보의 개선량이 아니다.

스킬 관찰: task-observer 3.5.0을 읽고 기존 사용자 범위 관찰 로그를 확인했다. 작업 가능한 위치가 이 worktree로 한정되어 사용자 범위 로그를 새로 쓰지 않았다. 별도 스킬 수정/메모리 갱신은 하지 않았고, 이번 작업의 수집 경쟁·무효 모집단 방지 절차는 위 결과에 보존했다.

## 이전 회차: 재생 중 전환 준비 완료, 실측 없음 (2026-10-25)

**`RENDERMERGE=ERROR reason=GAME_NOT_RUN` — 이번 수정판의 FPS 4창·픽셀 2점·렌더러/Draw Calls·끊김·복원은 모두 NA다.** 사용자 지시대로 게임 실행·설치·커밋·푸시·자동 장치 호출은 하지 않았다. 아래 환경 불일치 반복 기록은 과거이며, 05:40 추가 인계와 현재 코드가 우선한다. 2560×1440@120과 3440×1440@165가 이미 허용되어 있으므로 과거 환경 오류를 이번 중단 이유로 사용하지 않는다.

### 수정과 판정 범위

- `render-merge-steps.py`: A/B/B/A 모두 `seek 260 12` → 음악 시작 확인 → `decobake ready` → `waitsongpos 256 40` → packets compiled → live on 순서다. 측정 진입에 pause/resume이 없고, B만 cached ready/on을 추가한다. 준비·픽셀 진단의 pause는 유지한다. 4개 창은 모두 260~270초이며, 픽셀 262/270초와 retry/song_end/reopen/Shutdown 복원 절차도 보존했다.
- `GpuDecoRenderMerge.cs`: 재생 중 ready는 22개 사전 생성 layout의 CPU fallback 경계가 현재 소유와 같은지만 검사한다. 같으면 재생 중 on/off 가능; 다르면 재생 중 재생성 없이 거부한다. 일시정지 상태에서는 기존 재생성 경로를 유지한다. 정렬·재질·알파·누락 항목 검사와 GPU 소유/CPU 복원 구현은 그대로다. 측정 창 안의 ready/모드 변경은 거부한다.
- `GpuDecoRenderMergeGate.cs`: 한 ABBA 안의 해상도/주사율 변경을 거부한다. 각 창 시작에 allocated/owned/independent_enabled/CPU_fallback을 기록하고, 측정 직전 live census로 독립 렌더러의 enabled/visible 수를 남긴다. B의 구간 렌더러/서브메시 수와 실제 엔진 Draw Calls는 기존 창 종료 계측이 기록한다. 렌더러/호출 수·FPS·픽셀·안정성 수용 기준을 낮추지 않았다.
- 생성기 tag는 `rendermerge-260-270-abba-live-toggle3`. 새 JSON은 `tools/optimization/out/render-merge-live-toggle3-steps.json`이며 실행하지 않았다. 과거 계획과 자동 장치 제어 파일은 수정하지 않았다. 동일 코드 반복 시험도 요청하지 않는다.

### 기존 실제 로그에서 추가 확인한 한계

원시 `E:/StutterFixWork/night-results/rm-abba2.log`를 읽었다. 3440×1440@165 환경·음악 시작 대기를 통과한 뒤, **458~469행**에서 `running=True` 중첩 FastMove → `FastMoveOriginal` 복구(곡 256.062623초) → micro begin 실패가 확인된다. 이번 수정은 이 pause/resume 진입 경로를 제거한다. 복구 가드를 해제하거나 예외를 숨기지 않았다. 수정판에서 실제 소유가 유지되는지는 미검증이다.

또한 **344~389행**에서 준비 지원 수 34, GPU admitted/owned union **26**, 시간 layout의 renderer **0~9**가 확인된다. 이는 23,547개 소유·보이는 약 1,900개였던 기존 GPU 경로와 다른 모집단이다. 목록/세대 변경 후 재수집 로그와, `DecoBake.Collect`의 예약 목록 밖 효과 제외 코드는 확인했다. seek 시 예약 목록 차이가 원인일 가능성은 있으나 정확한 원인은 이 기록만으로 확정하지 않는다. 굽기 정책을 임의로 완화하지 않았다. **다음 외부 결과는 census/owned부터 확인해야 하며, 26개 소유 결과를 목표 장면의 성능 개선·설계 한계로 판정하면 안 된다.** 이 준비 문제와 재생 중 cached ready의 fallback 불일치는 남은 검증/수정 항목이다.

### 이번 오프라인 검증

Dev / Player / PlayerAuto 빌드 모두 오류 0(경고 각각 46/4/39). 생성기 계약 8개 통과, RenderMergeCheck 44개 통과(정책·정확 주소 심볼 38개 + 실제 3종 DLL 격리 6개). `git diff --check` 확인. 빌드 후 UI 인코딩 검사 실패 문자열은 이번 빌드 로그에서 발견되지 않았다. 게임 픽셀·FPS·실제 정렬·재생 끊김·수명 복원 검증을 대신하지 않는다. 기존 문서 이동/삭제 상태는 사용자 작업으로 보존했다. 별도 스킬 관찰 항목은 추가하지 않았다.

T2는 기존 `MonoDump.Syms`, `ETW_MONO_SYMS`/INC·LEAF 구현과 `pv-260-268-on-named.txt`를 확인했으며 새 게임/기록으로 반복하지 않았다. 원시 메인 포함 표본은 **3,846개**다(과거 성능 문서의 3,844는 2개 차이). PlayerRender 36.9%, CallUpdateMethod 48.3%, GpuLiveFrame 7.7%, TightLate.List 4.4%, TightLate.Process 4.3%, FastMove.Prefix 3.7%다. 포함 표본 비율은 서로 겹쳐 합산하지 않는다. 남은 목표 차이는 기존 2560 환경의 6.1ms → 220 FPS에 −1.555ms, →400 FPS에 −3.6ms, 교체 그리기 제거 3.83ms에서도 →400 FPS에 −1.33ms이며 이번 후보 개선량은 NA다.

이하 기록은 과거 이력이다.

## `codex-al-gm-9.md` 재지정 확인 (2026-10-25)

지정된 `gm-autoloop-run8-evidence.txt`의 실패·요약 3줄은 현재 원시 `E:/StutterFixWork/night-results/al-gm-8.log` **230~232행**과 일치한다. 이미 분석한 외부 run8의 재지정이며 새 게임 측정이 아니다. 요구 `2560x1440@120`, 실제 `3440x1440@164.999`로 맵 open 전 환경 검사에서 중단됐다. 판정은 **`RENDERMERGE=ERROR reason=ENVIRONMENT_MISMATCH`**다.

현재 보존 로그 1~8번도 각 요약의 오류 3줄과 일치하며 같은 289단계 계획이다. 시작 줄 첫 ` - ` 뒤 명령 문자열을 줄바꿈 없이 UTF-8로 해시한 SHA-256은 모두 `37a0478012c29f92e00ac8f694b62f3f73849ba974ef7527f77f6749c20fe3a4`다. 아래 과거 기록의 다른 해시는 추출 규약이 명시되지 않았으므로 이번 대조에 사용하지 않는다. 각 로그의 실제 `rendermerge_window_begin`·`rendermerge_window`·`rendermerge_pixel`·`rendermerge_stability` 결과 줄은 모두 0개다. FPS 4창·픽셀 2점·렌더러·Draw Calls·30ms 초과·복원 4종은 **NA**이며 오류 요약의 0은 초기값이다.

`GpuDecoRenderMergeGate.cs`의 `RequireGpuMergeEnvironment` 조건·오류 출력, `RenderMergeGatePolicy.cs`의 초기값·누락 증거 거부, 생성기의 open 전 검사 순서를 확인했다. 게임 `ADOStartup.cs` 근사 디컴파일을 읽고 기존 ILScan으로 설치 DLL 메타데이터의 Startup→FixResolution/ForceResolutionOnLofi→Screen 호출 관계를 교차 확인했다. 게임의 최소 크기·Steam Deck·lofi 처리는 목표 시험 환경을 보장하지 않는다. 검사는 정상이며 화면 불일치의 운영상 원인·후보 개선량·렌더 병합 설계 한계는 미판정이다. 렌더러를 수정할 새 근거가 없다.

**동일 환경·동일 코드·동일 계획 재시도 중단 필요.** 코드·검사·설정·기존 JSON/tag·제어 파일을 유지하고 새 계획·시험 요청 없이 분석을 마친다. 자동 장치를 직접 중지하지 않았다. 후속 시험 검토에는 목표 환경 확보의 새 근거가 필요하며 동일 코드 반복 금지 규칙도 유지한다. T2 기존 결과와 목표 차이(220 FPS까지 −1.555ms, 400 FPS까지 −3.6ms, 교체 그리기 제거 상태에서도 −1.33ms)는 보존했다.

문서 5개만 갱신한다. 검증 범위는 원시/요약 대조·8개 계획 동일성·실제 결과 부재·문서 정합성·10줄 요약·`git diff --check`다. 코드 변경이 없어 빌드·기존 성능 검사를 반복하지 않는다. 게임 실행·설치·커밋·푸시·설정 변경 0회, 크레딧 사용량은 제공되지 않아 NA다. **PASS 아님, T1 실게임 판정 미완료.** 아래는 기존 기록이다.

## `codex-al-gm-8.md` 재지정 확인 (2026-10-25)

지정된 외부 7·8회차 증거를 다시 확인했다. `gm-autoloop-run7-evidence.txt`·`gm-autoloop-run8-evidence.txt`의 실패·요약 3줄은 각각 현재 원시 `E:/StutterFixWork/night-results/al-gm-7.log`·`al-gm-8.log` **230~232행**과 일치한다. 기존 재시작 8·9회차에서 분석한 증거이며 새 게임 측정이 아니다. 지시서에 중첩된 9회차 문구도 별도 실행으로 세지 않는다.

판정은 `RENDERMERGE=ERROR reason=ENVIRONMENT_MISMATCH expected=2560x1440@120 actual=3440x1440@164.999`다. 생성기는 맵 open 전에 환경을 검사하며, `GpuDecoRenderMergeGate.cs`의 `RequireGpuMergeEnvironment`가 화면 크기와 주사율을 조건대로 거부했다. `RenderMergeGatePolicy.cs`의 배열·집계 초기값과 오류 출력 경로를 확인했다. 로그의 FPS·픽셀·렌더러 0과 stability=0/4는 측정 결과가 아니다. ABBA 4창·픽셀 2점·렌더러·Draw Calls·30ms 초과·복원 4종은 모두 **NA**다.

보존 로그 1~8번도 각 요약의 오류 3줄과 일치한다. 각 시작 줄의 첫 ` - ` 뒤 명령 문자열을 줄바꿈 없이 UTF-8로 해시한 SHA-256은 모두 `37a0478012c29f92e00ac8f694b62f3f73849ba974ef7527f77f6749c20fe3a4`다. 실제 `rendermerge_window_begin/window`·`rendermerge_pixel`·`rendermerge_stability` 결과 줄은 각각 0개다. 시작 줄에 나열된 예정 명령은 실행 증거가 아니다.

게임 `ADOStartup.cs` 근사 디컴파일의 `FixResolution`·`ForceResolutionOnLofi`를 읽고 기존 ILScan으로 설치 DLL의 Startup→해상도 처리→Screen 호출 관계를 교차 확인했다(메타데이터 읽기만 수행). 최소 크기·Steam Deck·lofi 처리는 목표 환경을 보장하지 않는다. 화면 불일치의 운영상 원인은 이 증거로 특정할 수 없다. 후보 개선량·렌더 병합 설계 한계도 미판정이므로 환경 오류를 근거로 렌더러를 수정하지 않는다.

**동일 환경·동일 코드·동일 계획 재시도 중단 필요.** 코드·검사·설정·기존 JSON/tag·제어 파일을 유지하고 새 계획·시험 요청 없이 이번 분석을 마친다. 자동 장치를 직접 중지한 것은 아니다. 후속 시험에는 목표 환경 확보의 새 근거가 필요하며 동일 코드 반복 금지 규칙도 유지한다. 기존 T2 결과와 220 FPS까지 −1.555ms, 400 FPS까지 −3.6ms, 교체 그리기 제거 상태에서도 −1.33ms라는 목표 차이를 보존했다.

문서 5개만 갱신했다. 원시/요약 대조·8개 계획 동일성·실제 결과 부재를 확인했고 문서 정합성·10줄 요약·`git diff --check`로 마무리 검증한다. 코드 변경이 없어 빌드·기존 성능 검사는 반복하지 않는다. 게임 실행·설치·커밋·푸시·설정 변경 0회, 크레딧 사용량은 제공되지 않아 NA다. **PASS 아님, T1 실게임 판정 미완료.** 아래는 기존 기록이다.

## `codex-al-gm-7.md` 재지정 확인 (2026-10-25)

최상단이 지정한 run6을 우선하고 중첩된 과거 run8과 구분했다. `gm-autoloop-run6-evidence.txt`의 오류 3줄은 현재 원시 `E:/StutterFixWork/night-results/al-gm-6.log` **230~232행**과 일치한다. 기존 재시작 7회차와 같은 증거이며 새 게임 측정은 없다. 판정은 `RENDERMERGE=ERROR reason=ENVIRONMENT_MISMATCH expected=2560x1440@120 actual=3440x1440@164.999`다.

보존 1~8번 모두 각 요약의 오류 3줄과 일치한다. 첫 ` - ` 뒤 명령 문자열을 줄바꿈 없이 UTF-8로 해시한 SHA-256은 모두 `37a0478012c29f92e00ac8f694b62f3f73849ba974ef7527f77f6749c20fe3a4`다. 실제 `rendermerge_window_begin/window`·`rendermerge_pixel`·`rendermerge_stability` 결과 줄은 각각 0개다. ABBA 4창·픽셀 2점·렌더러·Draw Calls·끊김·복원 4종은 **NA**이며 오류 요약의 0은 초기값이다.

`GpuDecoRenderMergeGate.cs`의 화면 조건·오류 출력, `RenderMergeGatePolicy.cs`의 초기값·누락 증거 거부, 생성기의 open 전 검사 순서를 확인했다. 게임 `ADOStartup.cs` 근사 디컴파일을 읽고 기존 ILScan으로 설치 DLL의 Startup→FixResolution/ForceResolutionOnLofi→Screen 호출 관계를 교차 확인했다(메타데이터 읽기만 수행). 최소 크기·Steam Deck·lofi 처리는 목표 화면을 보장하지 않는다. 검사는 정상이며 화면 불일치의 운영상 원인·후보 개선량·렌더 병합 설계 한계는 미판정이다. 환경 오류를 근거로 렌더러를 수정할 수 없다.

**동일 조건 재시도 중단 필요.** 코드·설정·기존 JSON/tag·제어 파일 유지, 새 계획·시험 요청 없이 여기서 중단한다. 자동 장치를 직접 중지하지 않았다. 기존 T2와 220/400 FPS 목표 차이를 보존했다. 문서 5개만 갱신하고 원시 대조·계획 동일성·문서 정합성·10줄 요약·`git diff --check`로 검증한다. 빌드·기존 성능 검사 반복 및 게임 실행·설치·커밋·푸시·설정 변경 0회, 크레딧 사용량 NA다. 아래는 기존 기록이다.

## `codex-al-gm-6.md` 재지정 확인 (2026-10-25)

최상단이 지정한 run5를 우선하고 중첩된 과거 run8과 구분했다. `gm-autoloop-run5-evidence.txt`의 오류 3줄은 현재 원시 `E:/StutterFixWork/night-results/al-gm-5.log` **230~232행**과 일치한다. 기존 재시작 6회차와 같은 증거이며 새 게임 측정은 없다. 판정은 `RENDERMERGE=ERROR reason=ENVIRONMENT_MISMATCH expected=2560x1440@120 actual=3440x1440@164.999`다.

보존 1~8번 모두 각 요약의 오류 3줄과 일치한다. 첫 ` - ` 뒤 명령 문자열을 줄바꿈 없이 UTF-8로 해시한 SHA-256은 모두 `37a0478012c29f92e00ac8f694b62f3f73849ba974ef7527f77f6749c20fe3a4`다. 실제 `rendermerge_window_begin/window`·`rendermerge_pixel`·`rendermerge_stability` 결과 줄은 각각 0개다. ABBA 4창·픽셀 2점·렌더러·Draw Calls·끊김·복원 4종은 **NA**이며 오류 요약의 0은 초기값이다.

`GpuDecoRenderMergeGate.cs`의 화면 조건·오류 출력, `RenderMergeGatePolicy.cs`의 초기값·누락 증거 거부, 생성기의 open 전 검사 순서를 확인했다. 게임 `ADOStartup.cs` 근사 디컴파일을 읽고 기존 ILScan으로 설치 DLL의 Startup→FixResolution/ForceResolutionOnLofi→Screen 호출 관계를 교차 확인했다(메타데이터 읽기만 수행). 최소 크기·Steam Deck·lofi 처리는 목표 화면을 보장하지 않는다. 검사는 정상이며 화면 불일치의 운영상 원인·후보 개선량·렌더 병합 설계 한계는 미판정이다. 환경 오류를 근거로 렌더러를 수정할 수 없다.

**동일 조건 재시도 중단 필요.** 코드·설정·기존 JSON/tag·제어 파일 유지, 새 계획·시험 요청 없이 여기서 중단한다. 자동 장치를 직접 중지하지 않았다. 기존 T2와 220/400 FPS 목표 차이를 보존했다. 문서 5개만 갱신하며 원시 대조·계획 동일성·문서 정합성·10줄 요약·`git diff --check`로 검증한다. 빌드·기존 성능 검사 반복 및 게임 실행·설치·커밋·푸시·설정 변경 0회, 크레딧 사용량 NA다. 아래는 기존 기록이다.

## `codex-al-gm-5.md` 재지정 확인 (2026-10-25)

최상단이 지정한 run4를 우선하고 중첩된 과거 run8과 구분했다. `gm-autoloop-run4-evidence.txt`의 오류 3줄은 현재 원시 `E:/StutterFixWork/night-results/al-gm-4.log` **230~232행**과 일치한다. 기존 재시작 5회차와 같은 증거이며 새 게임 측정은 없다. 판정은 `RENDERMERGE=ERROR reason=ENVIRONMENT_MISMATCH expected=2560x1440@120 actual=3440x1440@164.999`다.

보존 1~8번 모두 각 요약의 오류 3줄과 일치한다. 첫 ` - ` 뒤 명령 문자열을 줄바꿈 없이 UTF-8로 해시한 SHA-256은 모두 `37a0478012c29f92e00ac8f694b62f3f73849ba974ef7527f77f6749c20fe3a4`다. 실제 `rendermerge_window_begin/window`·`rendermerge_pixel`·`rendermerge_stability` 결과 줄은 각각 0개다. ABBA 4창·픽셀 2점·렌더러·Draw Calls·끊김·복원 4종은 **NA**이며 오류 요약의 0은 초기값이다.

`GpuDecoRenderMergeGate.cs`의 화면 조건·오류 출력, `RenderMergeGatePolicy.cs`의 초기값·누락 증거 거부, 생성기의 open 전 검사 순서를 확인했다. 게임 `ADOStartup.cs` 근사 디컴파일과 기존 ILScan의 설치 DLL 메타데이터 분석으로 Startup→FixResolution/ForceResolutionOnLofi→Screen 호출 관계를 교차 확인했다. 최소 크기·Steam Deck·lofi 처리는 목표 화면을 보장하지 않는다. 검사는 정상이며 화면 불일치의 운영상 원인·후보 개선량·렌더 병합 설계 한계는 미판정이다. 환경 오류를 근거로 렌더러를 수정할 수 없다.

**동일 조건 재시도 중단 필요.** 코드·설정·기존 JSON/tag·제어 파일 유지, 새 계획·시험 요청 없이 여기서 중단한다. 자동 장치를 직접 중지하지 않았다. 기존 T2와 220/400 FPS 목표 차이는 보존했다. 문서 5개만 갱신하며 원시 대조·계획 동일성·문서 정합성·10줄 요약·`git diff --check`로 검증한다. 빌드·기존 성능 검사 반복 및 게임 실행·설치·커밋·푸시·설정 변경 0회, 크레딧 사용량 NA다. 아래는 기존 기록이다.

## `codex-al-gm-4.md` 재지정 확인 (2026-10-25)

최상단이 지정한 run3를 우선하고 중첩된 과거 run8과 구분했다. `gm-autoloop-run3-evidence.txt`의 오류 3줄은 현재 원시 `E:/StutterFixWork/night-results/al-gm-3.log` **230~232행**과 일치한다. 기존 재시작 4회차와 같은 증거이며 새 게임 측정은 없다. 판정은 `RENDERMERGE=ERROR reason=ENVIRONMENT_MISMATCH expected=2560x1440@120 actual=3440x1440@164.999`다.

보존 1~8번 모두 각 요약의 오류 3줄과 일치한다. 157행 첫 ` - ` 뒤 명령 문자열을 줄바꿈 없이 UTF-8로 해시한 SHA-256은 모두 `37a0478012c29f92e00ac8f694b62f3f73849ba974ef7527f77f6749c20fe3a4`다. 실제 `rendermerge_window_begin/window`·`rendermerge_pixel`·`rendermerge_stability` 결과 줄은 각각 0개다. ABBA 4창·픽셀 2점·렌더러·Draw Calls·끊김·복원 4종은 **NA**이며 오류 요약의 0은 초기값이다.

`GpuDecoRenderMergeGate.cs`의 화면 조건, `RenderMergeGatePolicy.cs`의 초기값·누락 증거 거부, 생성기의 open 전 검사 순서를 확인했다. 게임 `ADOStartup.cs` 근사 디컴파일과 기존 ILScan의 설치 DLL 메타데이터 분석으로 Startup→FixResolution/ForceResolutionOnLofi→Screen 호출 관계를 교차 확인했다. 최소 크기·Steam Deck·lofi 처리는 목표 화면을 보장하지 않는다. 검사는 정상이며 화면 불일치의 운영상 원인·후보 개선량·렌더 병합 설계 한계는 미판정이다. 환경 오류를 근거로 렌더러를 수정할 수 없다.

**동일 조건 재시도 중단 필요.** 코드·설정·기존 JSON/tag·제어 파일 유지, 새 계획·시험 요청 없이 여기서 중단한다. 자동 장치를 직접 중지하지 않았다. 기존 T2와 220/400 FPS 목표 차이는 보존했다. 문서 5개만 갱신하며 원시 대조·계획 동일성·문서 정합성·10줄 요약·`git diff --check`로 검증한다. 빌드·기존 성능 검사 반복 및 게임 실행·설치·커밋·푸시·설정 변경 0회, 크레딧 사용량 NA다. 아래는 기존 기록이다.

## `codex-al-gm-3.md` 재지정 확인 (2026-10-25)

최상단이 지정한 run2를 우선하고 중첩된 과거 run8과 구분했다. `gm-autoloop-run2-evidence.txt`의 오류 3줄은 현재 보존 원시 `E:/StutterFixWork/night-results/al-gm-2.log` **230~232행**과 일치한다. 새 게임 시험은 없으며, 기존 재시작 3회차와 같은 증거다. 판정은 `RENDERMERGE=ERROR reason=ENVIRONMENT_MISMATCH expected=2560x1440@120 actual=3440x1440@164.999`다.

보존 1~8번 모두 각 요약의 오류 3줄과 일치한다. 157행 첫 ` - ` 뒤 명령 문자열을 줄바꿈 없이 UTF-8로 해시한 SHA-256은 모두 `37a0478012c29f92e00ac8f694b62f3f73849ba974ef7527f77f6749c20fe3a4`이며 실제 `rendermerge_window_begin/window`·`rendermerge_pixel`·`rendermerge_stability` 결과 줄은 각각 0개다. ABBA 4창·픽셀 2점·렌더러·Draw Calls·끊김·복원 4종은 **NA**다. 오류 요약의 0은 초기값이다.

`GpuDecoRenderMergeGate.cs`의 화면 조건, `RenderMergeGatePolicy.cs`의 초기값·누락 증거 거부, 생성기의 open 전 검사 순서를 확인했다. 게임 `ADOStartup.cs` 근사 디컴파일과 설치 DLL을 기존 ILScan으로 메타데이터만 읽어 Startup→FixResolution/ForceResolutionOnLofi→Screen 호출 관계를 교차 확인했다. 최소 크기·Steam Deck·lofi 처리는 목표 화면을 보장하지 않는다. 검사는 정상이며 화면 불일치의 운영상 원인·후보 개선량·렌더 병합 설계 한계는 미판정이다.

**동일 조건 재시도 중단 필요.** 코드·설정·기존 JSON/tag·제어 파일은 유지하고 새 계획·시험 요청 없이 여기서 중단한다. 자동 장치를 직접 중지하지 않았다. 기존 T2와 220/400 FPS 목표 차이는 보존했다. 문서 5개만 갱신하고 원시 대조·계획 동일성·문서 정합성·10줄 요약·`git diff --check`로 검증한다. 빌드·기존 성능 검사 반복 및 게임 실행·설치·커밋·푸시·설정 변경 0회, 크레딧 사용량 NA다. 아래는 기존 기록이다.

## `codex-al-gm-2.md` 재지정 확인 (2026-10-25)

이번 요청은 최상단의 `gm-autoloop-run1-evidence.txt`를 우선하고, 중첩된 과거 run8 지시와 구분했다. 현재 run1의 실패·요약 3줄은 원시 `E:/StutterFixWork/night-results/al-gm-1.log` **230~232행**과 일치한다. 과거 최초 run1의 준비 계약 오류(339~352행)와는 다른 보존 파일이다. 새 게임 시험은 수행하지 않았다.

판정은 `RENDERMERGE=ERROR reason=ENVIRONMENT_MISMATCH expected=2560x1440@120 actual=3440x1440@164.999`다. 생성기의 open 전 환경 검사가 코드대로 거부했다. `GpuDecoRenderMergeGate.cs`의 `RequireGpuMergeEnvironment`, `RenderMergeGatePolicy.cs`의 초기값·증거 누락 거부를 확인했다. FPS 4창·픽셀 2점·렌더러·Draw Calls·끊김·복원 4종은 **NA**이며 오류 요약의 0을 측정값으로 해석하지 않는다.

보존 로그 1~8번 모두 요약의 오류 3줄과 일치하고 실제 `rendermerge_window_begin/window`·`rendermerge_pixel`·`rendermerge_stability` 결과가 각각 0개다. 157행에서 첫 ` - ` 뒤의 명령 문자열을 추출해 줄바꿈 없이 UTF-8로 해시하면 SHA-256은 모두 `37a0478012c29f92e00ac8f694b62f3f73849ba974ef7527f77f6749c20fe3a4`다. 아래 과거 기록의 해시는 추출 규약이 없어 이번 검증에 사용하지 않았다. 동일 계획이라는 결론은 원시 문자열 대조로 확인했다.

게임 `ADOStartup.cs` 근사 디컴파일의 `FixResolution`·`ForceResolutionOnLofi`를 읽고 기존 ILScan 실행 파일로 설치 DLL의 Startup→해상도 처리→Screen 호출 관계를 교차 확인했다(메타데이터 읽기만 수행). 이 코드는 최소 크기·Steam Deck·lofi 처리를 하며 목표 시험 환경을 보장하지 않는다. 화면 불일치의 운영상 원인은 미확정이고, 렌더 병합의 설계 한계나 개선량도 미판정이다.

**동일 환경·동일 코드·동일 계획 재시도 중단 필요.** 코드·설정·기존 JSON/tag·제어 파일을 유지했고 새 계획·시험 요청은 만들지 않았다. 자동 장치를 직접 중지하지 않았다. 게임 실행·설치·커밋·푸시·설정 변경·빌드 반복은 0회다. T2 기존 결과와 220/400 FPS 목표 차이는 보존했다. 검증은 원시/요약 대조·계획 동일성·결과 부재·문서 정합성·10줄 요약·`git diff --check`로 한정한다. 크레딧 사용량은 제공되지 않아 NA다. 아래는 기존 분석 기록이다.

직전 분석은 `codex-al-gm-9.md`에 따른 **재시작 9회차: 기존 외부 8회차 증거 재지정, 환경 차단 유지**다. 새 게임 측정은 없다. 게임 실행·설치·커밋·푸시·설정 변경 없이 증거를 분석했다. FPS·픽셀·라이브 자원 복원 게이트는 미측정이다. 아래 기존 회차는 과거 기록이며, 자동 장치 번호 재시작으로 현재 보존 로그를 과거 최초 실행과 동일시하지 않는다.

## 재시작 9회차: 기존 run8 재지정, 새 측정 없이 환경 차단 유지 (2026-10-25)

- 마스터 인계·장부·기존 성능 문서를 읽고 지정된 `gm-autoloop-run8-evidence.txt`를 원시 `E:/StutterFixWork/night-results/al-gm-8.log`와 대조했다. 실패·요약 3줄은 **230~232행**과 일치한다. 요구 `2560x1440@120`, 실제 `3440x1440@164.999`로 맵 open 전 중단됐다. 이 증거는 재시작 8회차에서 이미 분석한 run8이며, 별도 새 시험으로 세지 않는다.
- 현재 보존 로그 1~8번도 각각 요약 증거와 일치한다. 모두 157행의 같은 289단계 계획이며 명령 문자열 SHA-256은 `1baeabe6850d46abf39d1c2b631a42c79ba433e64daf722343d859db70b9eecc`다. 각 로그의 실제 `rendermerge_window_begin/window`·`rendermerge_pixel`·`rendermerge_stability` 결과 줄은 0개다. 시작 줄의 예정 명령 목록은 실행 결과가 아니다.
- `RequireGpuMergeEnvironment`의 화면 크기·주사율 조건, 생성기의 open 전 검사 순서, `RenderMergeGatePolicy` 초기값과 오류 출력 경로를 확인했다. 게임 `ADOStartup.cs` 근사 디컴파일의 `FixResolution`·`ForceResolutionOnLofi`를 읽고 기존 ILScan으로 설치 DLL의 호출 관계를 교차 확인했다(메타데이터 읽기만 수행). 최소 크기·Steam Deck·lofi 처리는 목표 시험 환경을 보장하지 않는다. 검사는 정상이며 실제 화면 불일치의 운영상 원인은 미확정이다.
- FPS 4창·픽셀 2점·렌더러·Draw Calls·30ms 초과·복원 4종은 **NA**다. 오류 요약의 0은 초기값이다. 후보 개선량·음악 시작 대기 수정·렌더 병합 설계 한계는 미판정이다. T2 기존 결과 및 220 FPS까지 −1.555ms, 400 FPS까지 −3.6ms, 교체 그리기 제거 상태에서도 −1.33ms라는 목표 차이를 보존했다.
- **자동 장치의 동일 환경·동일 코드·동일 계획 재시도 중단 필요.** 수정 근거가 없어 코드·검사·설정·기존 JSON/tag·제어 파일은 유지했다. 새 계획·재시험 요청 없이 여기서 중단하며 자동 장치를 직접 중지하지 않았다. 후속 시험 검토에는 목표 환경 확보의 새 근거가 필요하다.
- 문서 5개만 갱신했다. 원시/요약 대조·8개 계획 동일성·실제 결과 부재·문서 정합성·10줄 요약·`git diff --check`로 검증한다. 코드 변경이 없어 빌드·기존 검사를 반복하지 않는다. 게임 실행·설치·커밋·푸시·설정 변경 0회다. 크레딧 사용량은 제공되지 않아 NA다.

문서용 판정: `RENDERMERGE=ERROR reason=ENVIRONMENT_MISMATCH expected=2560x1440@120 actual=3440x1440@164.999 fps=NA,NA,NA,NA pixels=NA renderers=NA engine_draws_max=NA stability=NA`. reason/NA는 원시 오류의 해석이며 새 게임 로그가 아니다. **PASS 아님, T1 실게임 판정 미완료.**

## 재시작 8회차: 지정된 외부 7·8회차 모두 환경 검사에서 중단 (2026-10-25)

- 지시서의 두 증거 `gm-autoloop-run7-evidence.txt`·`gm-autoloop-run8-evidence.txt`를 각각 원시 `E:/StutterFixWork/night-results/al-gm-7.log`·`al-gm-8.log`와 대조했다. 실패·요약 3줄은 각각 **230~232행**과 일치한다. 두 증거가 같은 실패를 보여 어느 쪽을 기준으로 해도 판정은 같다.
- 현재 보존 로그 1~8번 모두 157행의 같은 289단계 계획이다. 명령 문자열 SHA-256은 `1baeabe6850d46abf39d1c2b631a42c79ba433e64daf722343d859db70b9eecc`이며, 각 로그의 실제 `rendermerge_window_begin/window`·`rendermerge_pixel`·`rendermerge_stability` 결과 줄은 0개다. 요구 `2560x1440@120`, 실제 `3440x1440@164.999`로 맵 open 전에 거부됐다.
- 원인은 `GpuDecoRenderMergeGate.cs`의 `RequireGpuMergeEnvironment` 조건 불충족이다. 생성기는 open보다 먼저 이 검사를 수행한다. `RenderMergeGatePolicy.cs` 초기값과 결과 출력 경로를 확인했으며, 오류 요약의 FPS·픽셀·렌더러·Draw Calls 0은 측정값이 아니다. 게임 `ADOStartup.cs` 근사 디컴파일을 읽고 기존 ILScan으로 설치 DLL 메타데이터의 Startup→FixResolution/ForceResolutionOnLofi→Screen 호출을 교차 확인했다. 이 시작 코드는 목표 시험 환경을 보장하지 않는다. 실제 화면 불일치의 운영상 원인은 미확정이다.
- FPS 4창·픽셀 2점·렌더러·Draw Calls·30ms 초과·복원 4종은 모두 **NA**다. 렌더러 결함이나 설계 한계를 판단할 새 증거가 없어 코드·검사·설정·기존 JSON/tag·제어 파일은 수정하지 않았다. **자동 장치의 동일 환경·동일 코드·동일 계획 재시도 중단 필요.** 새 시험 계획·요청 없이 여기서 중단하며 자동 장치를 직접 중지하지는 않았다.
- 문서 5개를 갱신했다. 원시/요약 대조·8개 계획 동일성·실제 결과 부재·문서 정합성·10줄 요약·`git diff --check`로 검증한다. 빌드·기존 검사는 반복하지 않는다. 게임 실행·설치·커밋·푸시·설정 변경 0회다. 기존 T2와 220/400 FPS 목표 차이는 보존했으며 이번 후보 개선량·음악 시작 대기 수정은 미측정이다. 크레딧 사용량은 제공되지 않아 NA다.

문서용 판정: `RENDERMERGE=ERROR reason=ENVIRONMENT_MISMATCH expected=2560x1440@120 actual=3440x1440@164.999 fps=NA,NA,NA,NA pixels=NA renderers=NA engine_draws_max=NA stability=NA`. reason/NA는 원시 오류의 해석이며 새 게임 로그가 아니다. **PASS 아님, T1 실게임 판정 미완료.**

## 재시작 7회차: 최신 외부 6회차도 환경 검사에서 중단 (2026-10-25)

- 장부를 읽고 최상단 지시의 `gm-autoloop-run6-evidence.txt`를 우선했다. 중첩된 과거 run8은 최신 시험으로 취급하지 않았다. 실패·요약 3줄은 원시 `E:/StutterFixWork/night-results/al-gm-6.log` **230~232행**과 일치한다. 요구 `2560x1440@120`, 실제 `3440x1440@164.999`로 맵 open 전 중단됐다.
- 현재 보존 로그 1~8번 각각의 요약 증거도 원시와 일치한다. 모두 157행의 같은 289단계 계획이며 명령 문자열 SHA-256은 `1baeabe6850d46abf39d1c2b631a42c79ba433e64daf722343d859db70b9eecc`다. 실제 `rendermerge_window_begin/window`·`rendermerge_pixel`·`rendermerge_stability` 결과 줄은 각 0개다. 시작 목록은 후속 명령의 실행 증거가 아니다.
- `GpuDecoRenderMergeGate.cs`의 화면 조건·오류 요약, `RenderMergeGatePolicy.cs`의 초기값·증거 누락 거부, 생성기의 open 전 검사 순서를 확인했다. 게임 `ADOStartup.cs` 근사 디컴파일과 기존 `tools/ilscan` 실행 파일의 설치 DLL 메타데이터 분석으로 Startup→FixResolution/ForceResolutionOnLofi→Screen 호출을 교차 확인했다. 최소 크기·Steam Deck·lofi 처리는 목표 환경을 보장하지 않는다. 검사는 정상이며 화면 불일치의 운영상 원인은 이 증거만으로 특정하지 않는다.
- FPS 4창·픽셀 2점·렌더러·Draw Calls·30ms 초과·복원 4종 모두 **NA**다. 오류 요약의 0은 초기값이다. 음악 시작 대기 수정·후보 개선량·렌더 병합 설계 한계는 미판정이다. 기존 T2 결과와 목표 차이(220 FPS까지 −1.555ms, 400 FPS까지 −3.6ms, 교체 그리기 제거 상태에서도 −1.33ms)는 보존했다.
- **자동 장치의 동일 환경·동일 코드·동일 계획 재시도 중단 필요.** 새 렌더러 수정 가설을 뒷받침할 결과가 없어 코드·검사·설정·기존 JSON/tag·제어 파일을 유지했다. 새 계획·시험 요청 없이 여기서 중단한다. 자동 장치를 직접 중지한 것은 아니다. 목표 환경 확보의 새 근거와 동일 코드 반복 금지 규칙을 충족하기 전에는 후속 시험을 요청하지 않는다.
- 문서 5개만 갱신했다. 원시/요약 대조·8개 계획 동일성·실제 결과 부재·문서 정합성·10줄 요약·`git diff --check`로 검증한다. 코드 변경이 없어 빌드·기존 검사는 반복하지 않는다. 게임 실행·설치·커밋·푸시·설정 변경 0회, 크레딧 사용량은 제공되지 않아 NA다.

문서용 판정: `RENDERMERGE=ERROR reason=ENVIRONMENT_MISMATCH expected=2560x1440@120 actual=3440x1440@164.999 fps=NA,NA,NA,NA pixels=NA renderers=NA engine_draws_max=NA stability=NA`. reason/NA는 원시 오류의 해석이며 새 게임 로그가 아니다. **PASS 아님, T1 실게임 판정 미완료.**

## 재시작 6회차: 최신 외부 5회차도 환경 검사에서 중단 (2026-10-25)

- 장부와 최상단 지시의 `gm-autoloop-run5-evidence.txt`를 읽었다. 실패·요약 3줄은 원시 `E:/StutterFixWork/night-results/al-gm-5.log` **230~232행**과 일치한다. 요구 `2560x1440@120`, 실제 `3440x1440@164.999`로 맵 open 전 중단됐다. 중첩된 과거 run8보다 최신 run5를 우선했다.
- 보존 로그 1~8번과 각 요약 증거도 대조했다. 모두 같은 289단계 계획이며 명령 문자열 SHA-256은 `1baeabe6850d46abf39d1c2b631a42c79ba433e64daf722343d859db70b9eecc`다. 각 로그의 실제 `rendermerge_window_begin/window`·`rendermerge_pixel`·`rendermerge_stability` 결과 줄은 0개다. 시작 목록에 적힌 후속 명령은 실행 결과가 아니다.
- `GpuDecoRenderMergeGate.cs`의 환경 조건·오류 출력, `RenderMergeGatePolicy.cs`의 초기값·누락 증거 거부, 생성기의 open 전 검사 순서를 확인했다. 게임 `ADOStartup.cs` 근사 디컴파일과 기존 `tools/ilscan`의 설치 DLL 메타데이터를 대조해 Startup→FixResolution/ForceResolutionOnLofi→Screen 호출을 확인했다. 최소 크기·Steam Deck·lofi 처리는 목표 환경을 보장하지 않는다. 검사는 정상이며 화면 불일치의 운영상 원인은 이 증거만으로 특정하지 않는다.
- FPS 4창·픽셀 2점·렌더러·Draw Calls·30ms 초과·복원 4종 모두 **NA**다. 오류 요약의 0은 초기값이다. 음악 시작 대기 수정·후보 개선량·렌더 병합 설계 한계는 미판정이다. 기존 T2 결과와 220 FPS까지 −1.555ms, 400 FPS까지 −3.6ms, 교체 그리기 제거 상태에서도 −1.33ms라는 목표 차이를 보존했다.
- **자동 장치의 동일 환경·동일 코드·동일 계획 재시도 중단 필요.** 새 렌더러 수정 가설을 뒷받침할 결과가 없어 코드·검사·설정·기존 JSON/tag·제어 파일을 유지했다. 새 계획·시험 요청 없이 여기서 중단한다. 자동 장치를 직접 중지한 것은 아니다. 목표 환경 확보의 새 근거와 동일 코드 반복 금지 규칙을 충족하기 전에는 후속 시험을 요청하지 않는다.
- 문서 5개만 갱신했다. 원시/요약 대조·8개 계획 동일성·실제 결과 부재·문서 정합성·10줄 요약·`git diff --check`로 검증한다. 코드 변경이 없어 빌드·기존 검사는 반복하지 않는다. 게임 실행·설치·커밋·푸시·설정 변경 0회, 크레딧 사용량은 제공되지 않아 NA다.

문서용 판정: `RENDERMERGE=ERROR reason=ENVIRONMENT_MISMATCH expected=2560x1440@120 actual=3440x1440@164.999 fps=NA,NA,NA,NA pixels=NA renderers=NA engine_draws_max=NA stability=NA`. reason/NA는 원시 오류의 해석이며 새 게임 로그가 아니다. **PASS 아님, T1 실게임 판정 미완료.**

## 재시작 5회차: 최신 외부 4회차도 환경 검사에서 중단 (2026-10-25)

- 장부를 읽고 최상단 지시의 `gm-autoloop-run4-evidence.txt`를 우선했다. 실패·요약 3줄은 원시 `E:/StutterFixWork/night-results/al-gm-4.log` **230~232행**과 일치한다. 맵 open 전 요구 `2560x1440@120`, 실제 `3440x1440@164.999`로 거부됐다. 중첩된 과거 9회차의 run8은 최신 시험으로 취급하지 않았다.
- 보존 로그 1~8번을 각각 요약 증거와 대조했다. 모두 157행의 같은 289단계 계획이며 명령 문자열 SHA-256은 `1baeabe6850d46abf39d1c2b631a42c79ba433e64daf722343d859db70b9eecc`다. 각 로그의 실제 `rendermerge_window_begin/window`·`rendermerge_pixel`·`rendermerge_stability` 결과 줄은 0개다. 시작 목록은 후속 단계 실행 증거가 아니다.
- `GpuDecoRenderMergeGate.cs:48`의 화면 조건과 생성기의 open 전 검사 순서는 정상이다. 게임 `ADOStartup.cs` 근사 디컴파일을 읽고 기존 `tools/ilscan`으로 설치 DLL 메타데이터의 Startup→FixResolution/ForceResolutionOnLofi→Screen 호출을 교차 확인했다. 최소 크기·Steam Deck·lofi 처리는 목표 환경을 보장하지 않는다. 실제 화면이 달라진 운영상 원인은 이 증거만으로 특정하지 않는다.
- `RenderMergeGatePolicy.cs`와 오류 요약 경로도 확인했다. 환경 검사에서 중단되면 FPS 배열·픽셀·렌더러·Draw Calls 값은 초기값 0이며 측정 결과가 아니다. FPS 4창·픽셀 2점·렌더러·Draw Calls·30ms 초과·복원 4종 모두 **NA**다. 음악 시작 대기 수정·후보 개선량·렌더 병합 설계 한계는 미판정이다.
- **자동 장치의 동일 환경·동일 코드·동일 계획 재시도 중단 필요.** 렌더러 수정 가설을 뒷받침할 새 결과가 없어 코드·검사·설정·기존 JSON/tag·제어 파일을 유지했다. 새 계획·시험 요청 없이 여기서 중단하며 자동 장치를 직접 중지한 것은 아니다. 목표 환경 확보의 새 근거와 동일 코드 반복 금지 규칙을 충족하기 전에는 후속 시험을 요청하지 않는다.
- 문서 5개만 갱신했다. 원시/요약 대조·8개 계획 동일성·실제 결과 부재·문서 정합성·10줄 요약·`git diff --check`로 검증한다. 코드 변경이 없어 빌드·기존 검사는 반복하지 않는다. 게임 실행·설치·커밋·푸시·설정 변경 0회, 크레딧 사용량은 제공되지 않아 NA다. T2와 기존 목표 차이(220 FPS까지 −1.555ms, 400 FPS까지 −3.6ms, 교체 그리기 제거 상태에서도 −1.33ms)를 보존했다.

문서용 판정: `RENDERMERGE=ERROR reason=ENVIRONMENT_MISMATCH expected=2560x1440@120 actual=3440x1440@164.999 fps=NA,NA,NA,NA pixels=NA renderers=NA engine_draws_max=NA stability=NA`. reason/NA는 원시 오류의 해석이며 새 게임 로그가 아니다. **PASS 아님, T1 실게임 판정 미완료.**

## 재시작 4회차: 최신 외부 3회차도 환경 검사에서 중단 (2026-10-25)

- 장부와 `gm-autoloop-run3-evidence.txt`를 읽고 원시 `E:/StutterFixWork/night-results/al-gm-3.log` **230~232행**의 실패·요약 3줄과 대조했다. 모두 일치하며 맵 open 전 `gpudeco rendermerge environment`에서 요구 `2560x1440@120`, 실제 `3440x1440@164.999`로 중단됐다. 지시서에 중첩된 과거 9회차의 run8보다 최상단의 최신 run3을 우선했다.
- 보존 로그 1~8번 각각의 요약 증거가 원시와 일치한다. 모두 157행의 같은 289단계 계획이며 ` - timeout 600 / ` 뒤 문자열 SHA-256은 `1baeabe6850d46abf39d1c2b631a42c79ba433e64daf722343d859db70b9eecc`다. 각 로그의 실제 `rendermerge_window_begin/window`·`rendermerge_pixel`·`rendermerge_stability` 결과 줄은 0개다. 시작 목록에 나열된 명령은 실행 증거가 아니다.
- `GpuDecoRenderMergeGate.cs:48`의 화면 크기·주사율 조건과 `render-merge-steps.py`의 open 전 검사 순서를 확인했다. 게임 `ADOStartup.cs` 근사 디컴파일을 읽고 기존 `tools/ilscan`으로 설치 DLL의 Startup→FixResolution/ForceResolutionOnLofi→Screen 호출 관계를 교차 확인했다(메타데이터 읽기만 수행). 최소 크기·Steam Deck·lofi 분기는 목표 환경을 보장하지 않는다. 검사는 정상이며 실제 화면이 달라진 운영상 원인은 이 로그만으로 특정하지 않는다.
- FPS 4창·픽셀 2점·렌더러·Draw Calls·30ms 초과·복원 4종 모두 **NA**다. 오류 요약의 0은 초기값이다. 음악 시작 대기 수정·후보 개선량·렌더 병합 설계 한계는 미판정이다. 기존 T2와 220 FPS까지 −1.555ms, 400 FPS까지 −3.6ms, 교체 그리기 제거 상태에서도 −1.33ms라는 목표 차이를 보존했다.
- **자동 장치의 동일 환경·동일 코드·동일 계획 재시도 중단 필요.** 코드 수정 근거가 없어 코드·검사·설정·기존 JSON/tag·제어 파일을 유지했다. 새 계획·시험 요청 없이 여기서 중단하며 자동 장치를 직접 중지한 것은 아니다. 목표 환경 확보의 새 근거와 동일 코드 반복 금지 규칙을 충족하기 전에는 후속 시험을 요청하지 않는다.
- 문서 5개만 갱신했다. 원시/요약 대조·8개 계획 동일성·실제 결과 부재·문서 정합성·10줄 요약·`git diff --check`로 검증한다. 코드 변경이 없어 빌드·기존 검사는 반복하지 않는다. 게임 실행·설치·커밋·푸시·설정 변경 0회, 크레딧 사용량은 제공되지 않아 NA다.

문서용 판정: `RENDERMERGE=ERROR reason=ENVIRONMENT_MISMATCH expected=2560x1440@120 actual=3440x1440@164.999 fps=NA,NA,NA,NA pixels=NA renderers=NA engine_draws_max=NA stability=NA`. reason/NA는 원시 오류의 해석이며 새 게임 로그가 아니다. **PASS 아님, T1 실게임 판정 미완료.**

## 재시작 3회차: 최신 외부 2회차도 환경 검사에서 중단 (2026-10-25)

- 장부를 읽고 `gm-autoloop-run2-evidence.txt`의 실패·요약 3줄을 원시 `E:/StutterFixWork/night-results/al-gm-2.log` **230~232행**과 대조해 모두 일치함을 확인했다. 현재 2번 로그는 과거 문서의 231~233행이 아닌 230~232행이다. 맵 open 전 `gpudeco rendermerge environment`에서 요구 `2560x1440@120`, 실제 `3440x1440@164.999`로 중단됐다.
- 보존 로그 1~8번과 각각의 요약 증거도 대조했다. 8개 모두 같은 환경 거부이며, 157행의 289단계 목록에서 ` - timeout 600 / ` 뒤 문자열 SHA-256은 모두 `1baeabe6850d46abf39d1c2b631a42c79ba433e64daf722343d859db70b9eecc`다. 각 로그의 실제 `rendermerge_window_begin/window`·`rendermerge_pixel`·`rendermerge_stability` 결과 줄은 0개다. 시작 목록은 후속 명령 실행 증거가 아니다.
- `GpuDecoRenderMergeGate.cs:48`의 화면 조건과 생성기의 open 전 검사 순서를 확인했다. 게임 `ADOStartup.cs` 근사 디컴파일을 읽고 설치 DLL을 기존 `tools/ilscan`으로 메타데이터만 분석해 Startup→FixResolution/ForceResolutionOnLofi→Screen 호출 관계를 교차 확인했다. 최소 크기·Steam Deck·lofi 처리는 목표 환경을 보장하지 않는다. 검사는 정상이며 화면 불일치의 운영상 원인은 특정하지 않는다.
- FPS 4창·픽셀 2점·렌더러·Draw Calls·30ms 초과·복원 4종은 모두 **NA**다. 오류 요약의 0은 초기값이다. 음악 시작 대기 수정·렌더 병합 개선량·설계 한계는 계속 미판정이다. T2 기존 결과와 220/400 FPS 목표 차이를 보존했다.
- **자동 장치의 동일 환경·동일 코드·동일 계획 재시도 중단 필요.** 후보 측정이 없어 렌더러 수정 근거가 없으며 코드·검사·설정·기존 JSON/tag·제어 파일을 유지했다. 새 계획·재시험 요청은 만들지 않았고 자동 장치를 직접 중지하지 않았다. 후속 검토에는 목표 환경 확보의 새 근거가 필요하며 동일 코드 반복 금지 규칙도 유지한다.
- 문서 5개만 갱신했다. 원시/요약 대조·8개 계획 동일성·문서 정합성·10줄 요약·`git diff --check`로 검증하며 코드 변경이 없어 빌드·기존 검사는 반복하지 않는다. 게임 실행·설치·커밋·푸시·설정 변경 0회, 크레딧 사용량은 제공되지 않아 NA다.

문서용 판정: `RENDERMERGE=ERROR reason=ENVIRONMENT_MISMATCH expected=2560x1440@120 actual=3440x1440@164.999 fps=NA,NA,NA,NA pixels=NA renderers=NA engine_draws_max=NA stability=NA`. reason/NA는 원시 오류의 해석이며 새 게임 로그가 아니다. **PASS 아님, T1 실게임 판정 미완료.**

## 재시작 2회차: 최신 외부 1회차도 환경 검사에서 중단 (2026-10-25)

- 장부와 최신 `gm-autoloop-run1-evidence.txt`를 읽고 원시 `E:/StutterFixWork/night-results/al-gm-1.log` **230~232행**의 실패·요약 3줄이 일치함을 확인했다. `gpudeco rendermerge environment`에서 요구 `2560x1440@120`, 실제 `3440x1440@164.999`로 맵 open 전에 중단됐다.
- 현재 보존된 `al-gm-1.log` 및 `al-gm-2.log`~`al-gm-8.log` **8개 모두** 같은 환경 거부다. 289단계 시작 목록의 ` - timeout 600 / ` 뒤 문자열 SHA-256은 전부 `1baeabe6850d46abf39d1c2b631a42c79ba433e64daf722343d859db70b9eecc`이며, 각 로그의 실제 `rendermerge_window_begin/window`·`rendermerge_pixel`·`rendermerge_stability` 결과 줄은 0개다. 시작 목록은 실행 완료 증거가 아니다.
- FPS 4창·픽셀 2점·렌더러·Draw Calls·30ms 초과·복원 4종 모두 **NA**다. 오류 요약의 0은 초기값이다. 음악 시작 대기 수정과 렌더 병합 개선량은 계속 실게임 미검증이며, 설계 한계도 미판정이다.
- `GpuDecoRenderMergeGate.cs:48`의 화면 조건과 생성기의 open 전 검사 순서를 확인했다. 게임 `ADOStartup.cs` 근사 디컴파일의 최소 크기·Steam Deck·lofi 분기를 읽고 기존 `tools/ilscan`으로 설치 DLL의 Startup→FixResolution/ForceResolutionOnLofi→Screen 호출 관계를 메타데이터만 읽어 교차 확인했다. 시작 처리는 목표 시험 환경을 보장하지 않으며 검사 조건은 정상이다. 화면 불일치의 운영상 원인은 특정하지 않는다.
- **동일 조건 재시도 중단 필요.** 코드 수정의 근거가 될 후보 측정이 없으므로 코드·검사·설정·기존 JSON/tag·자동 장치 제어 파일을 유지했다. 새 계획·재시험 요청은 만들지 않았고 자동 장치를 직접 중지하지 않았다. 목표 환경 확보의 새 근거가 필요하며 동일 코드 반복 금지 규칙도 유지한다.
- 문서 5개만 갱신하고 원시/요약 대조·8개 계획 동일성·문서 정합성·10줄 요약·`git diff --check`를 확인했다. 코드 변경이 없어 빌드·기존 검사는 반복하지 않았다. T2와 기존 220/400 FPS 목표 차이를 보존했다. 게임 실행·설치·커밋·푸시·설정 변경 0회, 크레딧 사용량은 제공되지 않아 NA다.

문서용 판정: `RENDERMERGE=ERROR reason=ENVIRONMENT_MISMATCH expected=2560x1440@120 actual=3440x1440@164.999 fps=NA,NA,NA,NA pixels=NA renderers=NA engine_draws_max=NA stability=NA`. reason/NA는 원시 오류의 해석이며 새 게임 로그가 아니다. **PASS 아님, T1 실게임 판정 미완료.**

## 9회차: 외부 8회차도 동일 환경 차단, 새 시험 요청 없이 중단 (2026-10-25)

- 장부와 `gm-autoloop-run8-evidence.txt`를 읽고 원시 `E:/StutterFixWork/night-results/al-gm-8.log` **230~232행**의 실패·요약 3줄과 대조했다. 모두 일치하며 맵 open 전 요구 `2560x1440@120`, 실제 `3440x1440@164.999`로 거부됐다.
- 외부 **2~8회차 원시 157행의 289단계 명령 목록은 모두 완전히 같다**. ` - timeout 600 / ` 뒤 명령 문자열 SHA-256은 `1baeabe6850d46abf39d1c2b631a42c79ba433e64daf722343d859db70b9eecc`다. 일곱 로그 모두 실제 `rendermerge_window_begin/window`·`rendermerge_pixel`·`rendermerge_stability` 결과 줄은 0개이며 동일 환경 차단이 **7회 연속**이다.
- FPS 4창·픽셀 2점·렌더러·Draw Calls·30ms 초과·복원 4종은 모두 **NA**다. 오류 요약의 0은 초기값이며 음악 시작 대기 수정도 실게임 미검증이다. 시작 목록에 적힌 후속 명령을 실행 증거로 세지 않았다.
- `GpuDecoRenderMergeGate.cs:48`의 화면 크기·주사율 조건과 `render-merge-steps.py`의 open 전 검사 순서를 확인했다. 게임 근사 디컴파일 `ADOStartup.cs`를 읽고 설치 DLL을 기존 `tools/ilscan`으로 메타데이터만 분석해 Startup→FixResolution/ForceResolutionOnLofi 및 Screen 호출 관계를 교차 확인했다. 시작 처리는 목표 환경을 보장하지 않으며 검사는 정상이다. 운영상 화면 불일치의 원인은 이 증거로 특정하지 않는다.
- **차단 원인은 외부 시험 환경 불일치이며 렌더 병합의 설계 한계는 미판정이다.** 코드 수정 가설을 뒷받침할 새 후보 측정이 없어 코드·검사·설정·기존 JSON/tag·자동 장치 제어 파일을 유지했다. 새 계획이나 후속 시험을 요청하지 않았다.
- **외부 자동 장치는 동일 조건 재시도를 중단해야 한다.** 이번 문서 갱신은 자동 장치를 직접 중지한 것이 아니다. 목표 환경 확보의 새 근거 없이 후속 시험을 검토하지 않으며 동일 코드 반복 금지 규칙도 유지한다.
- 검증: 원시/요약 3줄 일치, 7회 계획 동일성·실제 결과 누락, 검사·게임 디컴파일/IL, 문서 정합성 및 `git diff --check` 확인. 문서만 변경하므로 빌드·기존 오프라인 검사를 반복하지 않았다. 게임 실행·설치·커밋·푸시·설정 변경 0회다. T2와 기존 220/400 FPS 목표 차이를 보존했다. 이번 회차 크레딧 사용량은 세션에 제공되지 않아 NA다.

문서용 판정: `RENDERMERGE=ERROR reason=ENVIRONMENT_MISMATCH expected=2560x1440@120 actual=3440x1440@164.999 fps=NA,NA,NA,NA pixels=NA renderers=NA engine_draws_max=NA stability=NA`. reason/NA는 원시 오류의 해석이며 새 게임 로그가 아니다. **PASS 아님, T1 실게임 판정 미완료.**

## 8회차: 외부 7회차도 동일 환경 차단, 후보 측정 없이 중단 (2026-10-25)

- 장부와 `gm-autoloop-run7-evidence.txt`를 읽고 원시 `E:/StutterFixWork/night-results/al-gm-7.log` **230~232행**의 실패·요약 3줄을 대조했다. 세 줄 모두 일치하며 맵 open 전 요구 `2560x1440@120`, 실제 `3440x1440@164.999`로 거부됐다.
- 외부 **2~7회차 원시 157행의 289단계 명령 목록은 모두 완전히 같다**. ` - timeout 600 / ` 뒤의 명령 문자열 SHA-256은 여섯 로그 모두 `1baeabe6850d46abf39d1c2b631a42c79ba433e64daf722343d859db70b9eecc`다. 실제 `rendermerge_window_begin/window`·`rendermerge_pixel`·`rendermerge_stability` 결과 줄은 각 로그 0개다. 동일 환경 차단은 **6회 연속**이다.
- FPS 4창·픽셀 2점·렌더러·Draw Calls·30ms 초과·복원 4종은 모두 **NA**다. 오류 요약의 0은 초기값이다. 시작 목록은 실행 결과가 아니며 음악 시작 대기 수정도 실게임 미검증이다.
- `GpuDecoRenderMergeGate.cs:48`의 `Screen.width==2560 && Screen.height==1440 && Math.Abs(hz-120)<.5`와 생성기의 open 전 검사 순서를 확인했다. 게임 근사 디컴파일 `ADOStartup.cs`의 최소 크기·Steam Deck·lofi 분기를 읽고 설치 DLL을 기존 `tools/ilscan`으로 읽기 전용 분석해 Startup→FixResolution/ForceResolutionOnLofi 및 Screen 호출 관계를 교차 확인했다. 게임의 시작 처리는 목표 환경을 보장하지 않으며 검사는 정상이다. 화면 불일치의 운영상 원인은 특정할 수 없다.
- **차단 원인은 외부 시험 환경 불일치다. 렌더 병합의 설계 한계는 미판정이다.** 코드 수정 가설을 뒷받침할 새 후보 측정이 없어 코드·검사·설정·기존 JSON/tag·자동 장치 제어 파일을 유지했다. 새 계획이나 후속 시험을 요청하지 않았다.
- **외부 자동 장치는 동일 조건 재시도를 중단해야 한다.** 이 문서 갱신은 자동 장치를 직접 중지한 것이 아니다. 목표 환경 확보의 새 근거 없이 후속 시험을 검토하지 않으며 동일 코드 반복 금지 규칙도 유지한다.
- 검증: 원시/요약 3줄 일치, 6회 계획 동일성·실제 결과 누락, 검사·게임 디컴파일/IL 확인, 문서 정합성 및 `git diff --check`. 문서만 변경하므로 빌드·기존 오프라인 검사는 반복하지 않았다. 게임 실행·설치·커밋·푸시·설정 변경 0회다. T2와 기존 220/400 FPS 목표 차이를 보존했다. 이번 회차 크레딧 사용량은 세션에 제공되지 않아 NA다.

문서용 판정: `RENDERMERGE=ERROR reason=ENVIRONMENT_MISMATCH expected=2560x1440@120 actual=3440x1440@164.999 fps=NA,NA,NA,NA pixels=NA renderers=NA engine_draws_max=NA stability=NA`. reason/NA는 원시 오류의 해석이며 새 게임 로그가 아니다. **PASS 아님, T1 실게임 판정 미완료.**

## 7회차: 외부 6회차도 동일 환경 차단, 측정 근거 추가 없음 (2026-10-25)

- 장부를 먼저 읽고 `gm-autoloop-run6-evidence.txt`의 실패·요약 3줄을 원시 `E:/StutterFixWork/night-results/al-gm-6.log` **230~232행**과 대조했다. 모두 일치하며 맵 open 전 요구 `2560x1440@120`, 실제 `3440x1440@164.999`로 거부됐다.
- 외부 **2~6회차 원시 157행의 289단계 명령 목록은 모두 완전히 같다**. 각 로그의 실제 `rendermerge_window_begin/window`·`rendermerge_pixel`·`rendermerge_stability` 결과 줄은 0개다. 동일 환경 차단이 **5회 연속**이며 시작 목록은 후속 단계 실행 증거가 아니다.
- FPS 4창·픽셀 2점·렌더러·Draw Calls·30ms 초과·복원 4종은 모두 **NA**다. 오류 요약의 0은 초기값이며 음악 시작 대기 수정도 실게임 미검증이다. 성능 실패나 렌더 병합의 설계 한계를 판정할 수 없다.
- `GpuDecoRenderMergeGate.cs`의 너비·높이·주사율 조건과 생성기의 open 전 검사 순서를 확인했다. 게임 근사 디컴파일 `ADOStartup.cs`의 최소 크기·Steam Deck·lofi 분기를 읽고 설치 DLL을 기존 `tools/ilscan` 실행 파일로 읽기 전용 분석해 Startup→FixResolution/ForceResolutionOnLofi 및 Screen 호출 관계를 교차 확인했다. 목표 환경을 보장하는 처리가 아니며 검사는 정상이다. 운영상 화면 불일치 원인은 이 증거로 특정하지 않는다.
- **외부 자동 장치의 동일 조건 재시도 중단 필요.** 코드·검사·설정·기존 JSON/tag·자동 장치 제어 파일을 유지했고 새 계획이나 후속 시험을 요청하지 않았다. 이 기록은 자동 장치 중지 실행이 아니다. 목표 환경 확보의 새 근거 없이 후속 시험을 검토하지 않으며 동일 코드 반복 금지 규칙도 유지한다.
- 검증: 원시/요약 3줄 대조, 5회 계획 동일성·실제 결과 누락 확인, 검사·게임 디컴파일/IL 확인, 문서 정합성 및 `git diff --check`. 문서만 변경하므로 빌드·기존 오프라인 검사는 반복하지 않았다. 게임 실행·설치·커밋·푸시·설정 변경 0회다. T2와 기존 220/400 FPS 목표 차이는 보존했다. 이번 회차 크레딧 사용량은 이 세션에 제공되지 않아 NA다.

문서용 판정: `RENDERMERGE=ERROR reason=ENVIRONMENT_MISMATCH expected=2560x1440@120 actual=3440x1440@164.999 fps=NA,NA,NA,NA pixels=NA renderers=NA engine_draws_max=NA stability=NA`. reason/NA는 원시 오류의 해석이며 새 게임 로그가 아니다. **PASS 아님, T1 실게임 판정 미완료.**

## 6회차: 외부 5회차도 동일 환경 차단, 측정 근거 추가 없음 (2026-10-25)

- 장부를 먼저 읽고 `gm-autoloop-run5-evidence.txt`의 실패 줄을 원시 `E:/StutterFixWork/night-results/al-gm-5.log` **230~232행**과 대조했다. 요약과 원시는 일치하며 맵 open 전 요구 `2560x1440@120`, 실제 `3440x1440@164.999`로 거부됐다.
- 외부 **2·3·4·5회차 원시 157행의 289단계 명령 목록은 완전히 같다**. 각 로그의 실제 `rendermerge_window_begin/window`·`rendermerge_pixel`·`rendermerge_stability` 결과 줄은 0개다. 같은 환경 차단이 **4회 연속**이며 시작 목록은 실행 완료 증거가 아니다.
- FPS 4창·픽셀 2점·렌더러·Draw Calls·30ms 초과·복원 4종은 모두 **NA**다. 오류 요약의 0은 초기값이며 음악 시작 대기 수정도 실게임 미검증이다. PASS나 성능 실패를 판정할 수 없다.
- `GpuDecoRenderMergeGate.cs`의 너비·높이·주사율 조건과 생성기의 open 전 검사 순서를 확인했다. 게임 근사 디컴파일 `ADOStartup.cs`의 최소 크기·Steam Deck·lofi 분기를 읽고 설치 DLL을 `tools/ilscan`으로 읽기 전용 분석해 Startup→FixResolution/ForceResolutionOnLofi 및 Screen 호출 관계를 교차 확인했다. 이 시작 처리는 목표 화면 확보를 보장하지 않으며 검사는 정상이다. 화면 불일치의 운영상 원인은 이 증거로 특정하지 않는다.
- **외부 시험 환경 불일치가 차단 원인이다. 렌더 병합의 설계 한계는 미판정이다.** 원인과 관계없는 코드 수정·검사 완화·화면 설정 변경을 하지 않았다. 코드·설정·기존 JSON/tag·자동 장치 제어 파일을 유지했고 새 계획이나 후속 시험을 요청하지 않았다.
- **외부 자동 장치의 동일 조건 재시도는 중단해야 한다.** 이번 문서 갱신은 자동 장치 중지 실행이나 다음 시험 승인이 아니다. 후속 검토에는 목표 환경 확보의 새 근거가 필요하며 동일 코드 반복 금지 규칙을 유지한다.
- 검증: 요약/원시 실패 줄 대조, 4회 계획 동일성·실제 결과 누락 확인, 검사·게임 디컴파일/IL 확인, 문서 정합성 및 `git diff --check`. 문서만 변경하므로 빌드·기존 오프라인 검사를 반복하지 않았다. 게임 실행·설치·커밋·푸시·설정 변경 0회다. T2와 기존 220/400 FPS 목표 차이는 보존했다.

문서용 판정: `RENDERMERGE=ERROR reason=ENVIRONMENT_MISMATCH expected=2560x1440@120 actual=3440x1440@164.999 fps=NA,NA,NA,NA pixels=NA renderers=NA engine_draws_max=NA stability=NA`. reason/NA는 원시 오류의 해석이며 새 게임 로그가 아니다. **PASS 아님, T1 실게임 판정 미완료.**

## 5회차: 외부 4회차도 동일 환경 차단, 후속 시험 요청 없음 (2026-10-25)

- `gm-autoloop-ledger.md`를 먼저 읽고 `gm-autoloop-run4-evidence.txt`의 실패 줄을 원시 `E:/StutterFixWork/night-results/al-gm-4.log`와 대조했다. 원시 **230~232행**에서 맵 open 전 `gpudeco rendermerge environment`가 요구 `2560x1440@120`, 실제 `3440x1440@164.999`로 거부됐다. 요약 증거의 실패 줄은 원시와 일치한다.
- 외부 **2·3·4회차의 289단계 명령 목록은 완전히 같다**. 세 로그 모두 실제 `rendermerge_window_begin/window`·`rendermerge_pixel`·`rendermerge_stability` 결과 줄은 0개다. 같은 환경 차단이 **3회 연속**이며 시작 목록은 후속 단계의 실행 증거가 아니다.
- FPS 4창·픽셀 2점·렌더러·Draw Calls·30ms 초과·복원 4종은 모두 **NA**다. 오류 요약의 0은 초기값이며 새 성능·픽셀·안정성 결과로 취급하지 않는다. 음악 시작 대기 수정 역시 실게임 미검증이다.
- `GpuDecoRenderMergeGate.cs:48`의 화면 조건과 생성기의 open 전 검사 순서를 확인했다. `ADOStartup.cs` 디컴파일의 최소 크기·Steam Deck·lofi 분기를 읽고 설치 DLL의 Startup→FixResolution/ForceResolutionOnLofi 및 Screen 호출 관계를 `tools/ilscan`의 읽기 전용 메타데이터 분석으로 교차 확인했다. 이 시작 처리는 목표 화면 확보를 보장하지 않으며 검사는 정상이다.
- **원인은 외부 시험 환경 불일치다. 렌더 병합의 설계 한계나 성능 실패는 입증되지 않았다.** 새로운 렌더러 수정 가설을 뒷받침하는 결과가 없으므로 코드·검사·설정·기존 JSON/tag·자동 장치 제어 파일을 유지했다. 새 계획이나 재시험 요청을 만들지 않았다.
- **외부 자동 장치는 같은 조건의 반복 시험을 중단해야 한다.** 목표 화면 확보의 새 근거와 동일 코드 반복 금지 규칙에 맞는 후속 시험 근거가 있어야 재검토할 수 있다. 이 문서 갱신은 자동 장치 중지 실행이나 후속 시험 승인이 아니다.
- 검증: 원시/요약 실패 줄 대조, 3회 명령 목록 동일성·실제 결과 누락 확인, 검사·게임 코드/IL 확인, 문서 정합성 및 `git diff --check`. 문서만 변경하므로 빌드와 기존 오프라인 검사를 반복하지 않았다. 게임 실행·설치·커밋·푸시·설정 변경 0회다. T2와 220/400 FPS까지 남은 기존 수치를 보존했다.

문서용 판정: `RENDERMERGE=ERROR reason=ENVIRONMENT_MISMATCH expected=2560x1440@120 actual=3440x1440@164.999 fps=NA,NA,NA,NA pixels=NA renderers=NA engine_draws_max=NA stability=NA`. reason/NA는 원시 오류의 해석이며 새 게임 로그가 아니다. **PASS 아님, T1 실게임 판정 미완료.**

## 4회차: 동일 환경 차단 재발, 반복 시험 중단 (2026-10-25)

- `gm-autoloop-ledger.md`·`gm-autoloop-run3-evidence.txt`와 원시 `E:/StutterFixWork/night-results/al-gm-3.log`를 확인했다. 최초 실패는 원시 **230~232행**의 `gpudeco rendermerge environment`: 요구 `2560x1440@120`, 실제 `3440x1440@164.999`다. 외부 2회차와 동일한 환경 거부가 연속 2회 발생했다.
- 외부 2·3회차 원시 157행에서 시작 목록 접두사를 제거해 비교한 **289단계 명령 목록이 완전히 같다**. 새 구현의 시험 결과가 아니다. 시작 목록에 적힌 open·seek·ABBA·픽셀·복원은 실행 증거가 아니며, 3회차에도 실제 측정 결과 줄은 없다.
- FPS 4창·픽셀 2점·렌더러·엔진 Draw Calls·30ms 초과 프레임·복원 4종은 모두 **NA**다. 오류 요약의 0은 초기값이며 픽셀 동일성·끊김 0·복원 성공을 의미하지 않는다. 2회차의 음악 시작 동기화 수정도 여전히 미검증이다.
- `GpuDecoRenderMergeGate.cs:48`의 너비·높이·주사율 조건과 `render-merge-steps.py`의 open 전 검사 순서를 다시 읽었다. 검사는 의도대로 거부했다. 게임 `ADOStartup.cs`의 `FixResolution`·`ForceResolutionOnLofi`도 다시 읽고 설치 DLL 메타데이터를 `tools/ilscan`으로 교차 확인했다. 게임 시작 시 해상도 처리는 목표 환경을 보장하지 않는다. 게임 코드 실행은 없었다.
- **원인은 외부 시험 환경 불일치이며 렌더 병합의 설계 한계가 입증된 것은 아니다.** 이 근거로 검사 완화·3440×1440 결과 환산·임의 화면 변경·렌더러 코드 수정을 하지 않는다. 기존 T2 결과와 220/400 FPS까지 남은 수치는 보존한다.
- **외부 자동 장치는 이 실패의 재시도를 중단해야 한다.** 같은 환경·같은 코드·같은 계획 재실행을 요청하지 않는다. 새 JSON/tag는 만들지 않았고 기존 JSON·코드·설정·자동 장치 제어 파일을 유지했다. 외부 운영 단계에서 목표 환경 확보 근거가 생겨야 후속 시험을 검토할 수 있으며, 환경 확보만으로 동일 코드 반복 금지 규칙을 해제하지 않는다.
- 이번 검증은 원시 증거와 요약 대조, 2·3회차 명령 목록 동일성, 검사 코드·게임 IL 확인, 문서 정합성 및 `git diff --check`다. 문서만 바꾸므로 빌드·기존 오프라인 검사는 재실행하지 않았다. 게임 실행·설치·커밋·푸시·설정 변경 0회다.

문서용 판정: `RENDERMERGE=ERROR reason=ENVIRONMENT_MISMATCH expected=2560x1440@120 actual=3440x1440@164.999 fps=NA,NA,NA,NA pixels=NA renderers=NA engine_draws_max=NA stability=NA`. NA와 reason은 원시 오류의 해석이며 새 게임 로그가 아니다. **PASS 아님, T1 실게임 판정 미완료.**

## 3회차: 환경 불일치 확인 후 중단 (2026-10-25)

### 외부 2회차에서 실제로 실행된 범위

- `gm-autoloop-ledger.md`, `gm-autoloop-run2-evidence.txt`, 원시 `E:/StutterFixWork/night-results/al-gm-2.log`를 확인했다. 원시 231~233행의 최초 실패는 `gpudeco rendermerge environment`: 요구 `2560x1440@120`, 실제 `3440x1440@164.999`다.
- 실패는 계획의 `open`·`seek`·준비·ABBA보다 앞이다. 원시 157행의 긴 시작 줄은 **실행 예정 명령 목록**이며 뒤쪽 명령의 실행 증거가 아니다. `rendermerge_window_begin/window`, `rendermerge_pixel`, `rendermerge_stability`의 실제 결과 줄은 없다.
- 따라서 FPS 4창, 픽셀 2점, 렌더러/엔진 Draw Calls, 30ms 초과 프레임, 복원 4종은 모두 **NA**다. 오류 요약의 `0.000`·`pixels=0`·`renderers=0`·`stability=0/4`는 초기값이며 관측값이 아니다. 2회차 시작 동기화 수정도 아직 게임에서 검증되지 않았다.

### 코드에서 확인한 원인과 변경하지 않은 이유

`GpuDecoRenderMergeGate.cs`의 `RequireGpuMergeEnvironment`는 `Screen.width==2560 && Screen.height==1440 && Math.Abs(hz-120)<.5`를 요구한다. 생성기는 reset 뒤, 맵을 열기 전에 이 검사를 호출하며 각 ABBA 창 시작에서도 재검사한다. 실제 화면은 너비가 880px 크고 주사율은 약 44.999Hz 높다. 이는 조건을 정확히 거부한 결과이며 렌더 병합 알고리즘 오류의 증거가 아니다.

게임 근사 디컴파일 `E:/StutterFixWork/decompiled/Assembly-CSharp/ADOStartup.cs`의 `Startup`·`FixResolution`·`ForceResolutionOnLofi`도 읽었다. `FixResolution`은 최소 크기 및 Steam Deck 처리, `ForceResolutionOnLofi`는 별도 lofi 조건 처리이며 목표 시험 환경을 설정하지 않는다. 설치된 `Assembly-CSharp.dll`을 `tools/ilscan`으로 **메타데이터만 읽어** Startup→해당 로컬 함수 및 `Screen.SetResolution` 호출 관계를 교차 확인했다. 외부 로그의 `FixResolution` 실행만으로 목표 화면에 맞춰졌다고 볼 수 없다. 화면 불일치가 생긴 운영상의 원인(모니터/원격 세션/저장 설정)은 이 증거로 특정하지 않는다.

**이번 차단은 외부 시험 환경 제약이다. 렌더 병합의 설계 한계가 수치로 확인된 것은 아니다.** 검사 제거·완화, 3440×1440 수용 범위 확대, FPS 환산, 임의 화면 설정 변경은 하지 않았다. 원인과 관계없는 렌더러 코드 수정도 하지 않았다. T2의 기존 이름 붙은 결과와 아래 220/400 FPS 차이는 보존한다.

### 반복 시험 중단 및 인계

- 문서용 판정 요약: `RENDERMERGE=ERROR reason=ENVIRONMENT_MISMATCH expected=2560x1440@120 actual=3440x1440@164.999 fps=NA,NA,NA,NA pixels=NA renderers=NA engine_draws_max=NA stability=NA`. `reason`과 NA는 외부 원시 오류를 해석한 표기이며 새 게임 로그가 아니다.
- **자동 장치는 같은 환경에서 같은 코드·같은 계획을 다시 실행하지 말 것.** 이번에는 새 steps JSON/tag를 만들지 않았고 재시험을 요청하지 않는다. 기존 2회차 JSON은 보존 자료이며 새 시험 승인이나 실행 요청이 아니다.
- 후속 시험에는 외부 운영 단계에서 목표 화면 환경이 실제로 확보됐다는 새 근거가 필요하다. Codex는 게임·화면 설정·자동 장치 제어 파일을 변경하지 않았다. 환경 확보 뒤에도 동일 코드 반복 금지 규칙은 유지하며, 운영자가 다음 시험 여부를 결정한다.
- 오프라인 확인은 원시 로그/생성기/환경 검사 및 게임 IL 호출 관계 확인, 문서 정합성, `git diff --check`다. 코드 변경이 없으므로 이전 빌드·정책 검사를 재실행하지 않았다. **PASS 아님, T1 실게임 판정 미완료.**

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

최신 판정은 문서 최상단의 재지정 확인을 따른다. 과거 최초 외부 1회차는 준비 오류였으며, 현재 보존 로그 1~8번은 동일 환경 불일치다. 시작 동기화 수정과 렌더 병합의 개선량은 미측정이다. 동일 조건 재시도는 중단한다.
