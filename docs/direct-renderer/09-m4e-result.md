# M4e 구현·오프라인 검증 결과

2026-11-15. `E:\StutterFixWork\wt-m4`, `claude/drfull`, 관찰 HEAD `1ea68f6975fd5e90728c4255d2a9a6d527e77422`.

**전체 완료 판정은 보류다. FPS UI 동결, 단계별 준비, 비활성 대상 분리, 불필요한 재계획 제거, 제출 잠금 분리와 리소스 재사용을 구현했다. native / DEV / Player / PlayerAuto 빌드는 모두 오류 0, 기존 export 74/74와 fixture를 보존했다. 그러나 HELLO의 실제 색 불일치 원인은 증명하지 못했고, 그 문제의 수정 완료도 주장하지 않는다. HELLO는 계속 사용 불가다.**

커밋, 게임 실행·조작, 설치 DLL 교체를 하지 않았다. 모든 실행 검증은 로컬 빌드·메타데이터 읽기·소프트웨어 WARP다. `native/sfnative.dll`은 이 worktree의 빌드 산출물로 갱신했다.

## HELLO: 확인한 사실과 남은 증거

기존 `night-results/m4d-img/hl50-cmp.png`를 확인했다. 기존 실게임 census와 plan 파일도 읽었다.

- `drfull-census-20261115-073231-778-renderers.tsv`에서 켜져 있고 force-off가 아닌 Grab 대상은 두 개다. `-1356302`는 `BLENDMODES_MODE_DIFFERENCE`, 정렬 순서 10000, `VisibleOutsideMask`이며, `-1212500`은 `BLENDMODES_MODE_MULTIPLY`, 정렬 순서 100, mask=None이다. 둘 다 MPB가 있다.
- 같은 자료의 활성 SpriteMask는 `-1356404`, `-1188584`, `-1188582` 세 개다. plan의 groups는 모두 0이고, 로그의 customRange는 모두 True다.
- HELLO 픽셀판의 Canvas 실제 그리기 후보는 0이다. 기록은 canvasTotal=14924, canvasActive=34, canvasGraphics=23393, canvasPotentialGraphics=0, auxRT=0이다. 따라서 이 표본의 큰 색 차이를 M4d 보조 Canvas 합성의 양자화 오차로 설명할 수 없다.
- 기존 로그에 main RT는 ARGB32, 3440×1440, MSAA=1로 기록돼 있다. BGMovingCam과 메인 Camera가 이 RT를 공유한다. 화면 캡처는 2560×1600이다.

`make-m4e-grab-cases.py`와 확장한 WARP fixture는 **실제 추출된 Grab DXBC**의 MULTIPLY·DIFFERENCE를 독립 HLSL 산술과 비교한다. non-white tint, material color, point/bilinear texture sampling, 1회/연속 2회 Grab을 포함한 **8/8에서 다른 픽셀 0, 최대 채널 0**이다. 기존 tile/floor 48조건도 그대로 통과한다.

이것은 해당 입력에서 slot 7 색·원본 DXBC·복사 시점·블렌드 산술이 맞다는 증거다. **HELLO 런타임의 실제 색·MPB·상수 버퍼·정렬을 재생한 결과가 아니다.** 기존 census는 MPB의 존재만 기록하고 값을 기록하지 않았다. 이 자료만으로 색, 키워드, Grab, 정렬, SpriteMask 중 하나를 범인으로 확정할 수 없다.

잘못된 마스크 범위 수정을 피하기 위해 설치 게임의 `scrVisualDecoration.SetMaskingDepth` IL도 읽었다. 게임은 chart depth를 front=`-depth+1`, back=`-depth`로 변환한다. chart의 front/back depth가 같다는 이유로 native의 실제 front/back sorting order도 같다고 간주하지 않았다. 근거는 `out/direct-renderer/m4e/mask-depth-il-final.txt`다. [Unity SpriteMask 문서](https://docs.unity3d.com/kr/current/ScriptReference/SpriteMask.html)는 정렬 범위와 SortingGroup 지역성을 설명하지만, 설치 엔진의 실제 stencil 명령을 입증하는 자료는 아니다.

다음 진단을 구현했다.

- `drfull colors`: 원본 카메라의 활성 Renderer마다 계층 경로·셰이더·키워드·정렬·maskInteraction·Sprite.color·재질 _Color·Renderer/slot MPB의 _Color와 _RendererColor·bounds를 TSV로 기록한다. FULL ON에서는 생산 코드가 만든 패스별 상수 바이트도 Base64로 기록한다. 파일은 실행 시 모드 폴더의 `drfull-colors-<UTC>.tsv`에 생긴다. Unity가 실제 바인딩한 GPU 상수 버퍼를 읽었다고 표시하지 않는다.
- `drfull grab unity` / `drfull grab native`: OFF일 때만 바꾸는 진단 선택이다. unity는 BlendModes Renderer를 원래 재질의 `DrawRenderer` 경로로 보낸다. 기본값과 정규 계획은 native다. 이 대조가 성공해야 한다고 미리 가정하지 않는다.
- 정규 HELLO 픽셀판은 OFF/ON의 실제 색 값을 모두 수집한다. 추가 Unity-blend 진단판도 준비했다.

**남은 필수 작업은 새 HELLO 픽셀판과 진단판에서 실제 색/MPB/순서 값을 확보해 같은 장식을 재생하고, 원인 변경만으로 오류가 사라지는 것을 확인하는 것이다.** 이번 작업에서는 게임 실행 금지 조건을 유지했으므로 이 검증과 수정 확정은 수행하지 않았다.

## FPS UI 잡음

설치 게임 DLL의 `ADOFAI.FPSCounter.Update`는 `unscaledDeltaTime`, `realtimeSinceStartupAsDouble`을 읽고 `TMP_Text.text`와 RectTransform을 쓴다. timeScale=0으로는 멈추지 않는다.

`FrameCompare.FreezePlanets()`의 pause lease에 이 정확한 타입의 Update prefix를 추가했다. 현재 표시된 글자와 위치를 그대로 유지하고, 해당 컴포넌트 ID·이름을 로그에 남긴다. resume/실패 정리의 기존 `RestorePlanets()`가 같은 Harmony owner를 해제한다. 화면 좌표 제외, 다른 UI 비활성화, 글자 강제 대입은 하지 않는다.

**실게임 off/off 잡음 0은 미측정이다.** 코드는 확인된 FPS 갱신 경로를 동결하지만, 기존 캡처 사각형에 다른 변동 요소가 없는지까지 오프라인으로 확정하지 않는다.

## 켜기 준비와 비활성 대상

`drfull on`은 이제 준비를 시작하고 즉시 `mode=preparing`을 반환한다. 준비 driver가 약 4ms의 협력적 예산으로 작업을 나눈다. Harmony 메서드 스캔·개별 패치, Canvas 단위 수집, Renderer 단위 바인딩 사이에 양보한다. **4ms는 개별 Unity/Harmony 호출을 선점하는 상한이 아니다.** 차가운 리소스 로드, 단일 패치, 최종 publish는 더 오래 걸릴 수 있고 `prepare_max_ms`에 드러난다. 총 준비 <1초도 아직 실측하지 않았다.

준비 동안 메인 카메라의 원래 cullingMask를 유지한다. Canvas 층 이동도 publish 이후에 시작한다. 준비 실패·off·driver 파괴는 iterator와 부분 생성 자원을 정리한다. 완료 직전 계층 변동으로 snapshot이 무효가 되면 원본 경로로 복귀한다. 활성 경로의 계층 전체 재수집 역시 원본을 반환하고 준비를 다시 시작하므로, 그 구간은 정상 ON 벤치로 세면 안 된다.

`drfull wait`는 AutoTest용 대기 단계다. 준비 종료뿐 아니라 첫 native frame까지 기다리고 `assert on`을 검사한다. 종전의 `on → wait 2`만으로 준비 완료를 가정하는 계획을 바꿨다.

- 초기 비활성/disabled/force-off Renderer는 Renderer 참조의 HashSet 감시만 만든다. Source·MPB·부모 인덱스·수명 컴포넌트·pose를 만들지 않는다. 관찰 중인 managed enabled/force-off/SetActive 쓰기로 켜지면 다음 PreCull에 승격한다. SetActive는 바뀐 하위 트리만 확인한다.
- 비활성 Canvas는 Canvas 참조만 보관하고 하위 트리 수집·그래픽 콜백·개별 수명 컴포넌트를 설치하지 않는다. 활성 Canvas의 수명 컴포넌트도 root에만 둔다. Graphic과 관리 setter 관찰은 유지한다.
- TransformAccessArray, pose/center/distance와 업로드 배열은 현재 Live source 수에 맞춘 조밀한 배열이다. 비활성화 때 추적 목록에서 빠진다. native draw의 안정된 source ID와 조밀한 pose 위치를 분리하기 위해 같은 export 뒤에 `CommitPacked` opcode를 추가했다. 원래 Commit 의미와 ABI 구조체 크기는 보존했다.
- 부모·공유 재질 인덱스는 Source별 등록 집합으로 중복을 확인한다. 큰 공통 부모 목록에 `List.Contains`를 반복하던 이차 비용을 없앴다.

감시는 기존 managed write-site 관찰 범위에 의존한다. 외부 미관측 native/Animator 코드가 Renderer.enabled나 계층을 직접 바꾸는 경우의 같은 프레임 승격은 실게임 검증 대상이다. 이미 승격된 Source와 D3D draw 설정은 재사용을 위해 보관하며, 이후 꺼진 Source를 매 프레임 transform job에 넣지는 않는다.

## 매 프레임 재계획과 제출

M4d에서 실제 정렬 변화 없이 `orderDirty`를 세우던 다음 경로를 제거했다.

1. 구조가 같은 SpriteMask의 일반 Refresh가 매번 재정렬했다. 이제 custom range의 실제 필드 튜플이 바뀔 때만 범위 재계획을 요청한다. 마스크 pose 이동은 일반 Sprite 정렬 키 변경으로 취급하지 않는다. SortingGroup의 실제 외부 키 변화는 유지한다.
2. sprite가 없는 SpriteRenderer의 반복 Refresh가 빈 패스 배열을 다시 만들며 매번 재정렬했다. 기존 draw가 실제로 없어질 때만 순서 변경으로 센다.
3. Canvas 조상 Transform의 모든 pose 통보가 가시성과 무관하게 전체 정렬을 요청했다. 통보는 Canvas 후보를 재검사하고, 실제 가시성 또는 전체 정렬 키가 달라졌을 때만 정렬한다. Canvas 거리 키 변경도 비교한다.

`drfull why`는 `타입 + writer 이름`별 통보 횟수와 재계획 관련 누적값을 출력한다. state에 `avoided_mask_replans`, `avoided_empty_replans`, `canvas_pose_signals`, `order_key_changes`, `draw_rebinds`를 추가했다. **세 경로의 코드상 문제는 확인했지만, 기존 DD의 매 프레임 재계획을 각 경로가 몇 번씩 일으켰는지는 기존 로그에 없으므로 미확정이다.**

Sprite.color만 변한 통보는 texture/layout/재질 구조 전체 재검사 대신 해당 색 상수만 갱신한다. 그 외 통보와 섞이면 전체 Refresh를 사용한다.

native 변경은 다음과 같다.

- Stats와 Commit이 렌더 명령 기록 mutex를 기다리던 구조를 분리했다. Stats는 발행된 atomic 누적값을 읽고, 단일 생산자 Commit과 단일 소비자 render는 release/acquire로 8칸 ring의 소유권을 넘긴다. Program/Geometry/Draw 등 자원 변경과 render의 잠금은 유지한다.
- 같은 geometry/program/sprite-layout의 input layout, 같은 texture/view, sampler 설정을 재사용한다.
- 색과 CB의 이전 업로드 바이트가 같으면 `UpdateSubresource`를 생략한다. 초기 비교 버퍼는 Draw 생성 때 확보한다.
- native pose 저장은 capacity 전체를 처음부터 초기화하지 않고 실제 사용하는 ID까지 확장한다.

단계별 state는 `collect_us`, `pose_us`, `sort_us`, `packet_us`, `rt_apply_us`, `rt_cull_us`, `rt_constants_us`, `rt_draw_us`, `rt_execute_us`, 업로드 수·바이트, layout hit와 준비 slice 수·최대·작업 합계를 포함한다. native 세부 시간은 64프레임마다 한 프레임을 계측하고 `rt_sample_frames`로 분모를 공개한다. `render_us`는 전체 event wall time의 기존 집계다. 이 시간들을 GPU busy나 Present 대기로 해석하거나 서로 겹치는 값을 더하면 안 된다.

**평소 main <0.3ms, render <1ms, 관리 전체 프레임 할당 0, 실게임 FPS 개선은 미측정이다.** 오프라인에서 안정된 단일 draw의 추가 상수 업로드 0은 확인했다.

## 기존 DD·Exit ABBA 반영

작업 도중 사양에 추가된 표와 `night-results/m4d-{dd,sh}-{A1,B1,B2,A2}.txt`를 읽었다. 다음은 **기존 M4d 실행의 관찰값**이며 새 M4e 성능이 아니다.

| 맵 | A1 | B1 | B2 | A2 |
|---|---:|---:|---:|---:|
| DD 50초 | 248.5 | 22.2 | 28.5 | 242.0 |
| Exit 88초 | 327.9 | 68.5 | 41.1 | 326.4 |

DD B1의 frames=278/replans=279, main 평균 9072µs·최대 134939µs, replan 최대 75538µs가 기록됐다. Exit B2에도 frames=621/replans=520과 main 평균 14357µs가 있다. 기존 구현이 실전에서 매우 느렸다는 관찰을 그대로 보존한다.

기존 B 계획은 seek 이후 재생 중 긴 동기 setup을 수행했다. 이 동안 곡 시각이 움직였는지와 A/B 측정 시작의 실제 장면 일치는 확인하지 못했다. 따라서 위 표를 같은 workload의 정확한 개선율로 확정하지 않는다. 새 ABBA 계획은 **A/B 모두 setup 구간을 pause → on/off → 준비 대기 → resume로 감싸고 동일한 wait 2 뒤 12초를 측정**한다. 기존 seek/waitsongpos 값은 유지했다.

## 검증과 재현

최종 증거는 `out/direct-renderer/m4e/check-final.log`, `validation/`, `grab/`, `m4e-audit.json`, `final-hashes.json`에 있다.

| 검사 | 결과 |
|---|---|
| native / DEV / Player / PlayerAuto | 4종 모두 오류 0 |
| 기존 export 이름·ordinal·hint | `sf_z_drfull_v1` 포함 74/74 보존 |
| 내장 native 및 Player 경계 | 최신 native 3/3 일치, 일반 Player 진단·원본 셰이더 제외 |
| 원본 셰이더/ABI roundtrip | 21 shaders / 1012 variants / 2024 stages / 6594 variables / 2407 textures |
| 기존 원본 DXBC WARP | 48/48, 최대 채널 1, 다른 픽셀 합계 28, 기존 control 5 |
| 기존 Canvas·스텐실 WARP | 합성 24, FLOAT 최대 1.19209e-7, RGBA8 최대 2, naive 음성 대조 3, 공유 stencil 9 |
| 기존 층 저널·그룹 정책 | 20115 검사, stable layer 관리 할당 0 |
| 기존 카메라 lease | 25종, 임의 마스크 10000, 안정 검사 100000회 관리 할당 0 |
| 준비 pump | 완료/실패/예산 양보/취소/옛 driver/카메라 변경, 원래 mask 보존; Unity stub 범위 |
| 새 packed pose | dense 1개 + native ID 5, 픽셀 동일, 기존 Commit 보존 |
| 새 동시 제출 | 128개 교대 색 패킷 전부 픽셀 동일, errors=0, dropped=0 |
| 안정 상수·layout | 같은 프레임 추가 업로드 0, 픽셀 동일, layout cache hit |
| Grab 원본 DXBC | 8/8, 다른 픽셀 0, 최대 채널 0; 실제 HELLO 값 재생 아님 |
| 픽셀 정책 fixture | metrics 19 + masks 15 + state 15 + controls 17 |
| 계획 감사 | 정규 24개 + 별도 HELLO Unity-blend 진단판 1개 생성, 실행 없음 |

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/direct-renderer/check-m4e.ps1
```

위 명령은 설치나 게임 실행을 하지 않는다. 초기 로컬 검증의 설치 DLL 해시 관찰에서는 1/2가 달라졌다. 이 작업에서 설치/복원 쓰기는 하지 않았고 변경 주체를 단정하지 않는다. 그 최초 관찰은 `out/direct-renderer/m4e/installed-{before,after}.json`에 보존했다. 최종 validation 구간의 두 DLL 해시는 2/2 일치했다. 후자의 일치를 세션 전체 불변으로 확대하지 않는다.

## Claude가 실행할 계획

경로는 모두 `E:\StutterFixWork\wt-m4\out\direct-renderer\m4e\plans\` 아래다. 생성·감사만 했고 여기서는 실행하지 않았다.

| 맵·곡 시각 | 분류 | 픽셀 | ABBA 접두사 |
|---|---|---|---|
| HELLO 50초 | `m4e-hl50-plan.json` | `m4e-hl50-pixel.json` | `m4e-hl50-{A1,B1,B2,A2}.json` |
| Windflower 258초 | `m4e-wf-plan.json` | `m4e-wf-pixel.json` | `m4e-wf-{A1,B1,B2,A2}.json` |
| DD 50초 | `m4e-dd-plan.json` | `m4e-dd-pixel.json` | `m4e-dd-{A1,B1,B2,A2}.json` |
| Exit 88초 | `m4e-sh-plan.json` | `m4e-sh-pixel.json` | `m4e-sh-{A1,B1,B2,A2}.json` |

추가 진단은 `m4e-hl50-unityblend-pixel.json`이다. native 픽셀판과 실제 색/MPB TSV를 먼저 보존하고 이 진단판과 비교한다. 이는 원인 분리용이며 ABBA 이득으로 집계하지 않는다.

정규 manifest는 `manifest.json`이다. 모든 후보는 `drfull wait` 후 native frame과 `assert on`을 확인한다. HELLO/WF 픽셀을 먼저 재검사하고, off/off 잡음 0 및 후보의 다른 픽셀 <0.1%·최대 채널 ≤3과 실제 복원 확인을 통과한 맵만 FPS를 해석한다. 자동 OFF, 계층 재준비, errors/dropped, uncertainCanvas, 복원 실패가 있으면 표본을 무효로 한다. HELLO는 위 원인 증명·픽셀 수정 검증이 끝날 때까지 사용 가능 판정을 내리지 않는다.
