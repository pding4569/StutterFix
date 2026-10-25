DRAWCOLLAPSE=FAIL reason=GATE1_INCOMPLETE_AFTER_RECOVERY candidate_spans=17 confirmed_spans=NA samples=1/6 gate=HOLD

# DrawCollapse 2회차 — 외부 run1 분석과 독립 표본 계획 수정 (2026-10-25)

`codex-al-dc-2.md`·마스터·장부·지정 run1 증거·Gemini 설계안을 확인했다. **이번 Codex의 게임 실행·설치·커밋·푸시·자동 장치 호출·설정/제어 파일 변경은 모두 0회**다. 아래 실측은 외부 장치가 이미 수행한 run1이며 이번에 게임을 실행한 결과가 아니다.

## 외부 증거와 판정

`dc-autoloop-run1-evidence.txt`의 비어 있지 않은 **68줄 전부**를 `E:/StutterFixWork/night-results/al-dc-1.log`와 순서·중복까지 대조했다. 원시 458행의 첫 진단만 완료됐고, 460행 재개 후 463~467행에서 `FastMoveOriginal` 복귀, 484행의 다음 validate에서 독립 GPU 소유 조건 실패가 발생했다. 262/264/266/268/270초 진단은 없다.

| 항목 | 외부 run1 결과와 범위 |
|---|---|
| 화면 환경 | **2560×1440@164.999**, 기존 3440×1440 또는 120Hz 성능 근거와 혼합 금지 |
| 첫 진단 | song=**260.008990**, frame=17860 |
| 할당 소유 / 실제 GPU 담당 / fallback | **23547 / 23547 / 0** |
| 가시 후보 소유 / 사이 장벽 / 구간 / 같은 텍스처 run | **1628 / 316 / 17 / 1177** |
| 미확인 비소유 / SortingGroup / tie / SpriteMask | **318 / 0 / 0 / 0** |
| 실제 GPU 담당 원본의 고유 텍스처 | **81종**, 맵 전체 301종과 범위가 다름 |
| 압축 배열 예상 추가량 / 비압축 포함 총량 | **221,238,096바이트(210.989090MiB) / 644,880,732바이트(615.006191MiB)** |
| 배열 구성 / 기능 / 장치 VRAM | **35티어·계획 배열 35개 / copy_support=True / 7949MiB** |
| 실제 배열 할당·복사 / 여유 VRAM 증명 | **0 / 0 / 없음**, feasibility=UNVERIFIED |
| 보호 복귀 | song=**260.066747**, CPU Rec **10252/10252**, unobserved_order=0, restore_ms=**23.988800** |
| FPS / 엔진 draw / 원본 대비 픽셀 / 30ms 이상 끊김 / 전체 수명 안정성 | 모두 **NA** |

35개 tier의 bytes·slices를 원시에서 합산해 총량과 81종을 검산했고, DXT tier만 별도 합산해 압축 합계와 대조했다. 615MiB가 7949MiB보다 작다는 사실은 실제 여유 메모리나 할당·복사 성공을 뜻하지 않는다. `driver_allocated_mb=0.000`도 여유 VRAM 실측으로 쓰지 않는다. **장벽 수 316은 소유 후보 사이만, unknown 318은 필터를 통과한 전체 미확인 비소유 renderer라 모집단이 다르다.**

로그는 `visibility=conservative_bounds_alpha_candidates pixels_proven=False ordering_proven=False gate=HOLD timing_contaminated=True`다. **17은 실제 픽셀 기반 확정 S가 아니다.** 알파·bounds·GPU quad로 텍스처 투명 영역·마스크·실제 픽셀 기여나 엔진 순서를 증명하지 못한다. S≤150 진입, S>400 실패, 전 구간 최대치 모두 미판정이다. 복귀 Rec 수의 일치는 이번 픽셀/재개 연속성 시험 통과가 아니다. raw의 전체 곡 평균 254/89 FPS·진단 비용 189.728ms·CPU 범위 평균 0.347974ms도 260~270초 성능 결과로 전용하지 않는다.

## 코드로 확인한 실패 경로와 수정

`AutoTest.cs`의 pause/resume는 `FrameCompare.Pause/Resume`를 호출한다. `GcControl.IsPlayingFast`는 controller.paused를 반영하고, `Hitch.Tick`은 정지→플레이 전이에 `Begin` → `SongStarted`를 호출한다. `Hitch.SongStarted`의 `EffectBudget.Suspend(3f)`는 **현재+60프레임**까지 `InCatchUp`을 다시 켠다. `FastMove.Take`는 길이 있는 효과에서 `CanAnim`이 거짓이면 원본으로 넘기고, `CanAnim`은 catch-up 중 거짓이다. `Orig`는 GPU 소유 중 원본 실행 전에 `RecoverGpuLive("FastMoveOriginal",false)`로 복귀시킨다. 같은 프레임용 `FrameCompare.PlaybackContinuity`는 이 60프레임 보호를 해제하지 않는다.

run1은 재개 한 프레임 tracking=True 다음 hitch_playing=True, resume_continuity=False 상태에서 복귀했다. **이번 판에는 `take_refusal reason=...` 추적이 없으므로 정확한 Take 분기 실행을 직접 실측했다고 단정하지 않는다.** `running=True`도 Prefix가 Take 전에 세우는 값이므로 그 값만으로 재진입/메모리 손상을 주장하지 않는다. `gg-autoloop-ledger.md`의 기존 53~55회차에도 동일 재개 보호 문제가 기록돼 있다.

읽기 전용 근사 디컴파일 `ffxMoveDecorationsPlus.cs`의 `StartEffect`가 원본 tween 사전·DOTween 콜백·SetPosition 등을 변경하는 것을 확인했다. **설치된 게임 Assembly-CSharp.dll을 ILScan으로 읽어** duration·GetTaggedDecorations·DOTween.To 및 setter 호출을 교차확인했다. 따라서 원본을 GPU 소유 중 강제로 허용하거나 catch-up 보호를 없애지 않는다. IL은 호출 계약 근거이며 이번 런타임 분기 추적의 대용은 아니다.

수정한 것은 `tools/optimization/drawcollapse-steps.py`의 **진단 계획 생성 코드**다. 각 260/262/264/266/268/270초 표본을 **새 seek 260(12초 lead-in) → 256초 대기 → packets compiled → live on → 해당 시점 대기 → pause → validate → diag → live off → resume → stop**으로 분리한다. 원본 재개 전에 소유를 내려놓아 run1의 실패 전제를 제거한다. 런타임·셰이더·native·정렬·보호 조건은 수정하지 않았다. 표본 간 seek로 프레임 스케줄이 달라질 수 있어 연속 10초 최대 장벽 수 증명도 아니다.

새 기본 tag는 `drawcollapse-gate1-260-270-diag2-isolated`, **103단계**, `tools/optimization/out/drawcollapse-gate1-diag2-isolated-steps.json`에 **생성만** 했다. 기존 68단계 diag1 파일은 SHA256까지 보존했고 baseline/abba/single 명시 모드의 전체 JSON 호환도 검사했다. 자동 장치에 전달·예약·실행하지 않았다. 새 계획의 게임 성공은 **미검증**이다.

## 오프라인 검증과 남은 관문

| 검사 | 이번 결과 |
|---|---|
| DEV / Player / PlayerAuto 빌드 오류 | **0 / 0 / 0** |
| 빌드 경고 | **46 / 36 / 4**, PlayerAuto 증분 빌드 |
| LoadCheck FieldRefAccess(이름) | **279 / 229 / 280**, 각각 오류 0·동적 이름 5 |
| 일반 Player DrawCollapse 정책/명령 | **False / False** |
| DrawCollapseCheck | **27개 통과**, 기존 정책 소스 링크 |
| 생성기 회귀검사 | **6개 통과**, 소유 중 resume/seek/stop/quit 금지·6회 독립 진입/해제·기존 명시 모드 보존 포함 |
| 원시 대조 / 메모리 산술 | **68줄 순서 일치 / 35티어·81 slices 합계 일치** |

최초 LoadCheck 실행파일에는 새 DrawCollapse 정책/명령 검사 출력이 없어 최종 감사가 거부했다. 도구를 현재 소스로 다시 빌드(오류/경고 0/0)한 뒤 3종을 재검사했고, 위 수치는 이 최종 출력이다. DEV/PlayerAuto의 True/True·일반 Player의 False/False를 실제 로그에서 확인했다.

검사·IL·구조화 원시 대조 로그는 `tools/optimization/out/drawcollapse-run2-*`에 보존했다. 빌드는 기존 저장소 인코딩 경고를 포함할 수 있으며 빌드 exit 0을 저장소 전역 인코딩 통과로 바꾸지 않는다. 이번 변경 텍스트와 10줄 요약은 별도 확인한다. 설치하지 않았으므로 설치 원본 복원 시험은 NA다.

**1관문 HOLD·2관문 미구현**이다. 확정 S≤150 전까지 병합 렌더러·배열 셰이더를 추가하지 않는다. 151~400 보류, 확정 S>400 중단 기준과 maxdiff≤3·draw≤300·FPS≥220·30ms 이상 0·3빌드/LoadCheck 기준을 유지한다. 이번 결과는 이 설계 전체의 불가능을 확정한 것이 아니다. 남은 것은 새 계획 외부 결과와 실제 픽셀 기여/정렬 증명이며 같은 diag1 계획 재시도는 하지 않는다.

task-observer **3.5.0**은 기존 사용자 범위 로그·원칙을 읽고 report-back으로 적용했다. 외부 관찰/스킬/메모리 수정은 없으며 새 일반화 관찰도 없다.

---

## 이전 1회차 기록 (외부 run1 수신 전)

당시 판정: `DRAWCOLLAPSE=FAIL reason=GATE1_NOT_MEASURED owned_visible=NA barrier_visible=NA active_barrier_spans=NA same_tex_runs=NA textures_in_owned=NA array_mem_mb_dxt=NA`.

# DrawCollapse 1관문 준비 결과 (2026-10-25)

사용자 지정 `codex-drawcollapse-20261025.md`와 그 문서가 지정한 마스터·Gemini 설계안·이전 render-merge 결과를 읽었다. **게임 실행·설치·커밋 금지가 우선**이므로 이번에는 진단 코드·계획·오프라인 검사만 준비했다. 위 FAIL은 수용 증거가 없다는 뜻이며 `too_many_barriers`나 새 설계의 성능 실패 판정이 아니다. **1관문은 미완료이고 2관문 병합 구현은 하지 않았다.**

## 기준과 현재 수치

- 인계의 기준은 GPU 소유 상태 **168 FPS(약 6.0ms)**, 장식 이동 이벤트를 뺀 같은 구간 **492~500 FPS(약 2.0ms)**다. 차이 약 4ms를 출발점으로 보존하며 Gemini 원문의 261 FPS를 한계값으로 쓰지 않는다. 이번에 재측정한 수치가 아니다.
- 텍스처 **301종**은 인계의 이전 실측이다. 이번 실제 소유 텍스처 수·포맷·치수·추가 배열 메모리는 **NA**다. 301장을 모두 2048² RGBA32 단일 밉으로 만들면 산술상 **5,049,942,016바이트(4,816MiB)**이며, 이것을 실제 맵의 배열 크기로 보고하지 않는다.
- 합격 조건은 원본 대비 **maxdiff≤3**, **엔진 draw≤300**, 260~270초 **FPS≥220**, **30ms 이상 끊김 0**, 3빌드·LoadCheck 통과·설치 원본 복원이다. 이번 픽셀·엔진 draw·FPS·끊김·게임 수명 안정성은 모두 **NA**, 설치를 하지 않아 복원 시험도 **NA**다.

## 준비한 코드

`GpuDecoDrawCollapse.cs`에 명시 호출 전용 `gpudeco drawcollapse validate`와 `gpudeco drawcollapse diag`를 추가했다. DEV/PlayerAuto에만 들어가며 기본 Player에는 정책·명령 모두 없다. 자동 호출·프레임 훅·설정 옵션은 추가하지 않았다.

`validate`는 전곡 scope·독립 GPU 렌더러·할당 소유 **23547**·실제 GPU 담당 **20000 이상**·native 오류 0과 기존 기준선 조건을 확인한다. 별도 submitsingle·latecache·costprobe 후보는 제외한다. 실제 화면 크기·주사율·fallback 수를 로그로 남기며 해상도를 강제로 바꾸지 않는다.

`diag`는 260~270초의 **일시정지 상태**, G2 측정 창 밖에서만 동기 GPU 정점 readback을 한다. 기존 독립 renderer의 bounds는 2,000,000 크기의 정렬용 경계이므로 `isVisible`이나 동결된 원본 Transform으로 소유 가시성을 판단하지 않는다. 실제 GPU quad의 유한 정점·삼각형 면적·양수 알파·카메라 frustum을 검사하고, 현재 독립 renderer의 `MatShare.SortKey`와 기존 `SceneRenderOrder.Key.CompareTo`로 정렬한다. 텍스처 기준 재그룹화는 없다.

비소유는 enabled·activeInHierarchy·!forceRenderingOff·카메라 layer·bounds를 검사한다. 확인된 Sprites/Tile SpriteRenderer는 자체 알파 0을 제외하되, 임의 셰이더·MPB·메시·마스크의 알파를 추측해서 제외하지 않는다. 알 수 없는 항목·SortingGroup·SpriteMask·재질 tie를 별도 계수한다. 구간은 소유 후보 사이에 끼는 비소유 행에서만 끊고, 선행/후행 장벽과 같은 renderer의 여러 재질 슬롯은 장벽 수에 중복 반영하지 않는다. `same_tex_runs`는 텍스처뿐 아니라 재질 서명·기존 정렬 버킷·game layer가 같은 연속 구간 수다.

요청된 필드의 로그 형식은 다음과 같다. **아래는 형식이며 실제 실측 로그가 아니다.**

```text
DRAWCOLLAPSE_DIAG owned_visible=N barrier_visible=M active_barrier_spans=S same_tex_runs=R textures_in_owned=T array_mem_mb_dxt=X ... visibility=conservative_bounds_alpha_candidates pixels_proven=False ordering_proven=False gate=HOLD timing_contaminated=True
```

**중요한 미완료점:** bounds·알파·면적만으로는 투명한 텍스처 영역, 화면 가림, 마스크의 실제 픽셀 기여를 증명할 수 없다. 따라서 이 로그의 N/M/S/R은 **가시 후보 통계**이며 요청된 실제 픽셀 기반 장벽 수의 확정값으로 쓰면 안 된다. SortingGroup/마스크 정렬도 미증명이다. 후보 계수를 실제 계수로 둔갑시켜 S>400 실패나 S≤150 구현 진입을 선언하지 않도록 항상 `gate=HOLD`를 쓴다. 같은 재질 tie도 진단용 renderer ID로만 결정하며 엔진 순서가 증명됐다고 하지 않는다.

## 배열 메모리 사전 계산

Texture2DArray 생성·CopyTexture·Blit 호출은 **0개**다. 현재 실제 GPU 담당 원본의 고유 텍스처를 수집하고 다음 기준으로 `DRAWCOLLAPSE_ARRAY` 및 `DRAWCOLLAPSE_ARRAY_TOTAL` 로그를 준비했다.

- **실제 graphicsFormat·정확한 가로/세로 치수·mipmapCount·filter·wrap·aniso별 tier**로 묶는다. 2048² 패딩·RGBA32 강제 변환·압축 해제·CPU ReadPixels·읽기 가능 설정 변경은 없다.
- Unity의 실제 graphicsFormat 블록 가로/세로/바이트 수를 읽고 각 밉의 올림 블록 수를 합산한다. 논리적 TextureFormat의 RGB24 등을 그대로 바이트 수로 오인하지 않는다. DXT/BC 합계는 `array_mem_mb_dxt`, 비압축까지 포함한 총량은 `total_mb`다. 로그의 mb 단위는 **MiB(2²⁰바이트)**다. BC 이외 압축·알 수 없는 형식은 unsupported/NA로 남긴다.
- tier별 slice 수·바이트·장치 slice 한도와 분할 시 필요한 배열 수를 남긴다. CopyTexture의 서로 다른 텍스처 타입 간 복사 지원·장치 VRAM·driver 할당 추정도 기록한다. 복사 기능이 없거나 알려진 추가 배열만으로 장치 VRAM 전체를 넘으면 `IMPOSSIBLE_COPY_SUPPORT`/`IMPOSSIBLE_VRAM_LOWER_BOUND`, 그 외는 **UNVERIFIED**다. driver 할당 추정은 여유 VRAM의 증명이 아니다.
- 실제 할당 정렬·driver overhead·밉 제한/스트리밍·sRGB·압축 복사 동일성·실제 여유 VRAM은 아직 검증하지 않았다. tier가 여러 개인 경우 한 span의 텍스처 전체를 한 draw에서 바인딩할 수 있다는 증명도 없다.

API 의미는 [Unity GetBlockSize](https://docs.unity3d.com/cn/6000.0/ScriptReference/Experimental.Rendering.GraphicsFormatUtility.GetBlockSize.html), [GetBlockWidth](https://docs.unity3d.com/ja/6000.0/ScriptReference/Experimental.Rendering.GraphicsFormatUtility.GetBlockWidth.html), [GetBlockHeight](https://docs.unity3d.com/cn/6000.0/ScriptReference/Experimental.Rendering.GraphicsFormatUtility.GetBlockHeight.html), [CopyTexture](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/graphics/copytexture) 문서로 대조했다. 문서는 API 계약 참고이며 게임 Unity 6000.3.21f1에서의 픽셀/복사 성공 증거가 아니다. 이번 번들 생성도 없다.

## 계획과 오프라인 검증

`tools/optimization/drawcollapse-steps.py`의 기본 모드는 이제 **diag**, tag는 `drawcollapse-gate1-260-270-diag1`이다. 기존 기준선의 곡 처음 준비→seek 260(12초 lead-in)→256초부터 live on 순서를 재사용하고, **260/262/264/266/268/270초**에 일시정지·validate·diag·resume를 수행한다. 10초는 곡 시각 구간이며 일시정지/readback이 있으므로 FPS 창이 아니다. 별도 fuse/micro 성능 측정은 없다. 전곡이나 표본 사이 프레임의 최대 S도 증명하지 않는다.

`tools/optimization/out/drawcollapse-gate1-diag1-steps.json` **68단계**는 기존 로컬 Arche 기준선의 chart 경로로 **생성만** 했다. 실행기·자동 장치에 전달하지 않았다. 기존 baseline/abba/single **명시 모드**의 JSON 객체는 기존 modcpu 생성기와 tag 차이까지 대조해 보존했다. 기본 모드 변경은 명시적으로 기록하며 modcpu 생성기·기존 JSON은 수정하지 않았다.

| 검사 | 결과 |
|---|---|
| DEV / Player / PlayerAuto 빌드 오류 | 0 / 0 / 0 |
| 빌드 경고 | 46 / 36 / 39 |
| LoadCheck FieldRefAccess(이름) | 279 / 229 / 280, 각각 오류 0·동적 이름 5 |
| 일반 Player 정책·명령 유출 | False / False |
| DrawCollapseCheck | **27개 통과**, 실사용 정책 소스 링크 |
| 계획 생성기 | **4개 통과**, 기존 명시 모드 객체 전체 비교 포함 |

정책 검사는 signed 재질 ID·순서 유지·중복 슬롯·선행/후행 tie·23547개 소유 항목·150/151/400/401 경계·비정사각 밉·압축 꼬리 밉·잘못된 치수·64비트 overflow를 확인한다. 실제 Unity 렌더링이나 픽셀 검증은 아니다. LoadCheck는 메타데이터/IL/FieldRef 계약 검사이며 게임을 켜는 도구가 아니다. 최종 로그는 `tools/optimization/out/drawcollapse-*`에 보존한다. 기존 저장소 전역 인코딩 검사의 과거 상태를 이번 통과로 바꾸지 않고, 이번 변경 파일만 UTF-8·텍스트 무결성을 별도 확인한다.

## 이어갈 때의 경계

1. 이번 사용자 금지 조건을 유지한다. 게임 실행·설치·커밋·푸시·자동 장치 호출·설정/제어 파일 변경 **0회**다.
2. 실제 픽셀 기여 및 정렬 미확인 항목을 해결하고 허용된 외부 실측을 받기 전까지 **1관문 HOLD**다. S와 실제 301종 분포/추가 메모리를 기존 69 renderer·1478 submesh 결과에서 추정하지 않는다.
3. 확정 S≤150일 때만 2관문을 검토한다. **151~400은 인계에 진입 지시가 없으므로 보류**다. 확정 S>400이면 `DRAWCOLLAPSE=FAIL reason=too_many_barriers`로 중단한다. 후보 상한처럼 보이는 숫자만으로 이 실패를 선언하지 않는다.
4. 현재 병합 렌더러·새 Texture2DArray 셰이더·subMesh 변경·색 양자화 변경·ABBA 후보 구현은 없다. 향후 구현은 **subMeshCount==1·재질 1개**와 인계의 2관문 조건을 모두 만족해야 한다.

task-observer **3.5.0**과 기존 사용자 범위 관찰 로그/원칙을 읽었다. 안정적인 로그 경로는 작업 폴더 밖이므로 report-back 방식으로 처리했으며 스킬·메모리·외부 관찰 파일은 수정하지 않았다. 이번 새 일반화 관찰은 없다.
