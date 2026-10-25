DRAWCOLLAPSE=FAIL reason=GATE1_NOT_MEASURED owned_visible=NA barrier_visible=NA active_barrier_spans=NA same_tex_runs=NA textures_in_owned=NA array_mem_mb_dxt=NA

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
