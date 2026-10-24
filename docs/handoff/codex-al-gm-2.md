# 자동 반복 2회차

먼저 `docs/handoff/codex-master-20261022.md`(마스터 인계문서)를 읽어라.

직전 게임 시험 결과(자동 수집): `docs/handoff/gm-autoloop-run1-evidence.txt`. 먼저 `docs/handoff/gm-autoloop-ledger.md` 장부를 읽고, 이미 실패한 접근은 되풀이하지 말고 결과에서 배운 점으로 다른/더 나은 접근을 택해라(같은 코드를 다시 돌리는 것은 금지). 게임 코드는 `E:/StutterFixWork/decompiled/Assembly-CSharp/<타입>.cs` 디컴파일본으로 읽어라(근사, ilscan 으로 교차확인). 성공 조건(정규식) `RENDERMERGE=PASS` 이 아직 안 나왔다. 결과를 분석해 원인을 코드로 찾아 고치고, 안 되는 이유가 설계 한계면 결과 문서에 구체적으로 적고 멈춰라. 아래는 원래 임무다.


---
# 장식 렌더링 비용 줄이기 (render-merge) — Claude 인계, 2026-10-24 밤

먼저 `docs/handoff/codex-master-20261022.md`(전체 상황·규칙·도구)와 `docs/handoff/claude-perf-260-270-20261024.md`(이번 측정)를 읽어라. 게임 실행·설치·커밋·푸시 금지(자동 루프가 한다). 시험은 **`seek 260`으로 무거운 구간(260~270초)만** 돌려라(0초부터 자연 재생은 이미지 재생 계획 외엔 금지). 한 판에서 여러 측정을 이어서 하도록 steps 를 짜라.

## 확정된 사실 (Arche 260~270초, 2560x1440@120, 같은 설정)
- GPU 경로 끔 107 FPS(9.3ms) → 기존 GPU 경로 164~167 FPS(6.1ms) → 교체 렌더러·복사 끔 261 FPS(3.83ms). 이미지 GPU(소유 28,523)는 161 FPS(이득 없음, 보류). 렌더러별 MPB 제거(`gpudeco nombp on`)는 163.7 FPS(효과 없음).
- 6.1ms = 렌더링 2.2 + Update 1.8 + LateUpdate 1.3 + 기타 0.75. 우리 모드 OnUpdate 0.06ms, 다른 모드 <0.01ms.
- PerfView(심볼 있음, `docs/handoff/pv-260-268-{on,off}-inclusive.txt`): 켬−끔 메인 스레드 2.04ms 중 PlayerRender +1.34 (Camera::CustomRender +1.18: DoRenderLoop +0.66, ExecuteRenderQueue +0.56, GetOrCreateSharedRendererScene +0.51, PrepareMeshRenderNodes +0.38, BuildRenderObjectData +0.36, DoForwardShaderRenderLoop +0.37, CanFlattenSharedMaterialData +0.22) + CallUpdateMethod +0.57. 렌더 스레드 78%(끔 26%). → 비용은 **보이는 교체 MeshRenderer 약 1,880~1,930개의 렌더 노드 준비·그리기 제출 수**에 비례.
- 끔 상태에서도 스크립트 CallUpdateMethod 2.22ms 전부가 managed(mono) 안이고 엔진 쪽 별도 항목은 없다. 그런데 모든 어셈블리 Update/LateUpdate/코루틴 감싸기(`cpubreak`)로 설명되는 건 1.64ms(켬 기준)뿐 → **이름 없는 managed 시간이 약 0.8~1ms**.

## 과제
**T1 (핵심) 보이는 렌더 노드 수 줄이기 — 목표 보이는 그리기 객체를 1,900 → ≤150 렌더러(그리기 호출 ≤600), 260~270초 ≥ 220 FPS.**
정렬 규칙(같은 sortingLayer/order/큐/거리 안에서 재질 인스턴스 ID 오름차순이 뒤→앞)을 지켜야 하므로, 현재 독립 렌더러(장식 1개=렌더러 1개)를 **정렬 경계(비소유 렌더러가 끼는 곳)로만 나눈 구간 렌더러 + 텍스처별 서브메시**로 바꾼다(`GpuDecoSnapshot/GpuDecoExport` 의 run/submesh 구성은 일시정지 G1 진단에서 이미 있다: runs≈109, submeshes≈1,773, 픽셀은 기존 GPU 경로와 같은 수준). 어려운 점은 **라이브에서 보이는 장식이 시간에 따라 바뀐다**는 것(소유 23,547 중 보이는 건 약 1,900): 시간 슬라이스(예: 2.5초)별로 그 슬라이스에 보일 수 있는 장식만 구간으로 구성하고 슬라이스 경계에서 구간을 미리 준비(곡 도중 멈춤 금지, 준비는 재생 시작 전 또는 이전 슬라이스 동안 유휴 시간에). 알파 0 장식은 구간에서 제외(꺼진 렌더러 대신 인덱스에서 빠짐) 또는 퇴화 사각형. 서브메시 재질은 서브메시 i 가 "그 텍스처 구간 첫 멤버의 기존 재질 인스턴스"이고 `sharedMaterials` 인스턴스 ID 가 구간 안에서 엄격 오름차순인지 검사(안 맞으면 렌더러 분할). `bounds.center.z`·정렬 키는 구간 첫 멤버와 같게. 정점은 기존 컴퓨트가 렌더러별 버퍼에 쓴다(렌더러 수가 줄어 CopyRun dispatch 도 준다).
검증 게이트: ① 일시정지 262·270초 `framecmp` 끔/켬/끔에서 기존 GPU 경로(독립 렌더러)와의 픽셀 차이가 **늘지 않을 것**(비교 대상은 원본 SpriteRenderer, 현재 기록: 이미지 후보 제외한 기존 경로) ② 재생 중 `fuse`/`micro` 창 FPS ≥ 220, 곡 중 새 끊김(30ms↑) 0 ③ 곡 끝·다시 하기·맵 다시 열기·Unload 복원 오류 0.
**T2 이름 없는 managed 시간 찾기.** PerfView 기록의 managed 프레임은 `!0x1a…` 주소다. Mono JIT 주소를 이름으로 바꾸는 방법: 모드에서 `mono-2.0-bdwgc.dll` 의 `mono_pmip(void* ip)` / `mono_jit_info_table_find` 를 P/Invoke 로 불러(Mono 가 JIT 한 메서드 이름 문자열 반환) 주소 목록 파일(`addr.txt`, 한 줄에 16진 주소)을 이름으로 바꾸는 AutoTest 명령 `monosyms <입력> <출력>` 을 만들고, `tools/etwstall` 에 `ETW_MONO_SYMS=<파일>` 로 INC/LEAF 출력의 `!0x…` 를 치환하는 옵션을 추가하라(같은 게임 실행 안에서 JIT 주소가 유효하므로 기록 직후 같은 게임 프로세스에서 돌려야 한다: 기록 → `monosyms` 로 해당 주소 해석을 한 번의 시험 안에서). 결과로 260~270초 managed 시간 상위 메서드(우리 Harmony 래퍼 포함)를 이름으로 보고.
**T3** T1·T2 결과로 `docs/handoff/claude-perf-260-270-20261024.md` 를 갱신하고, 남은 항목(400 FPS 까지)을 수치로 정리.

## steps 생성기와 성공 표시
`tools/optimization/render-merge-steps.py --chart <Arche *FULL.adofai> --output x.json` 을 만들어라: `seek 260` 기반, 한 게임에서 기존 경로·구간 경로를 번갈아(ABBA) 측정하고 마지막 요약 한 줄 `RENDERMERGE=PASS|FPS_FAIL|PIXEL_FAIL|STABILITY_FAIL|ERROR` + FPS·픽셀·렌더러 수. PASS = 위 게이트 ①②③ 모두. 같은 시험을 코드 변경 없이 되풀이 요청하지 마라, 설계 한계가 수치로 확인되면 문서에 쓰고 멈춰라. 결과 `docs/handoff/codex-render-merge-result.md` + `-o`(10줄).

## 시작을 빠르게 해 주는 사실 확인 (Claude, 2026-10-24 23:15)
- `mono-2.0-bdwgc.dll` 에 `mono_pmip`, `mono_jit_info_table_find`, `mono_jit_info_get_method`, `mono_method_full_name`, `mono_get_root_domain` export 가 모두 **있다**(게임 폴더 `MonoBleedingEdge\EmbedRuntime`). T2 는 `mono_pmip(IntPtr)` 한 번 호출로 시작하면 된다.
- steps 뼈대는 `tools/optimization/out/round119-diag4-steps.json`(seek 260 기준 두 창: 기존 경로 / 교체 렌더러·복사 끔)과 `round121-pv-steps.json`(PerfView 기록 `trace <이름> <초>` 두 번)을 복사해서 수정하라. 실행·상태 조회는 사용자 쪽 자동 장치가 `tools/sfmeasure/trace_runs.py` 의 `start`/`status` 로 한다(`plan` 은 워크트리 안 상대 경로, 실행 이름은 새 이름).
- 시험 한 판은 3~4분. 게임을 여러 번 켜지 않도록 한 판 안에 측정을 이어서 넣어라.

## 2026-10-25 05:40 Claude 보충: 밤새 25회전이 헛돈 이유와 지금 상태 (먼저 읽을 것)
1. 헛돈 이유: `RequireGpuMergeEnvironment` 가 2560x1440@120 만 허용했는데 사용자 모니터는 3440x1440@165 였다. **고침**(둘 다 허용, 같은 실행 안의 ABBA 끼리만 비교하므로 문제 없음). 이 환경 검사 때문에 "측정 불가"로 멈추는 일은 이제 없다. 환경 불일치를 이유로 시험을 건너뛰지 말 것.
2. 고침: `gpudeco rendermerge playbackready` 가 false(아직)를 돌려주면 GpuStep 가 사용법 예외로 떨어졌다 → `rendermerge ` 접두어는 GpuRenderMergeCommand 의 bool 을 그대로 돌려주게 `GpuDecoExport.cs` 수정.
3. 실제 게임 시험(`rm-abba2`, 3440x1440@165)에서 확인된 것: 환경 통과, 맵 열림, `playbackready` 통과, `rendermerge prepare` 가 구간별 `items/renderers/submeshes`(2~9개)를 보고. **A 팔의 `gpudeco micro begin` 에서 실패**: "micro window requires playing GPU ownership".
4. 원인(로그): `pause`→`packets compiled`→`live on`→`resume` 직후 곡 256.06초에 `FastMove.Orig` 가 불려 `RecoverGpuLive("FastMoveOriginal")` 로 GPU 소유가 꺼짐(`original_guard bypass=False running=True ... hitch_playing=True resume_continuity=False`). `running=True` = FastMove 실행 중에 중첩 호출이 들어옴. 증명된 다른 계획들(round122 등)은 **pause/resume 없이** `seek 260 12`→`decobake ready`→`waitsongpos 256 40`→`gpudeco packets compiled`→`gpudeco live on`→`waitsongpos 260 40`→`gpudeco micro begin` 순서로 소유가 유지된다.
5. 할 일(우선순위): (a) A 팔(합치기 끔)은 증명된 위 순서(pause 없음)로 바꿔 바로 돌려 기준선을 얻는다. (b) B 팔은 `rendermerge ready/on` 이 Paused 를 요구하므로, 가능하면 요구를 풀어 재생 중 토글하게 하거나(렌더 합치기는 sort/draw만 바꾸고 소유 상태는 안 건드림), pause 위치를 256.0 이 아니라 이동 효과가 없는 조용한 곡 위치(`gpudeco live state`/로그로 찾기)로 옮긴다. 둘 다 안 되면 원인(중첩 호출을 일으키는 효과)을 `original_guard_stack` (`GpuOriginalTrace`) 로 잡아라. (c) 이번 라운드의 목표는 **ABBA 결과 숫자 한 번 얻기**다(FPS 4창, 렌더러 수, 엔진 draw 수, 픽셀 2점). 코드 설계를 더 갈아엎기 전에 숫자부터.
6. 시험 실행: `cd E:\StutterFixWork && python run_json.py wt-gpu/tools/optimization/out/<plan>.json wt-gpu <이름> --keys RENDERMERGE,rendermerge --tail 60` (자동 루프가 하는 것과 같음). 모니터는 지금 3440x1440@165.

## 2026-10-25 05:45 Claude 보충 2: 1회전 게임 시험 결과와 진짜 원인 (가장 중요)
시험 `al-gm-1`(3440x1440@165, pause 없는 새 계획) 결과: A·B 네 창 모두 **FPS 104.2~104.6**(합친 B 창 104.18, 안 합친 A 창 104.26 — 차이 없음), `owned=26`, `visible_renderers=0`(소유한 26개는 이미 알파 0), `engine_draws_max≈2,305~2,318`, 합친 렌더러 수 6. B 팔 두 번째 전환은 `rendermerge fallback layout changed; prepare while paused before next playback` 로 중단.
**진짜 원인은 병합 코드가 아니라 소유 범위다.** 이 계획은 `seek 260` 뒤에 `live prepare` 를 해서 그 창에서 보이는 장식 34개 중 26개만 소유한다(`live_prepare_candidates window_visible=34 admitted=26`). 이전에 FPS 가 높았던(추정 160+) 측정과 `gpu_owned_peak=23,547` 은 **곡 처음(`select 0`→`play`→`gpudeco scheduled`→`pause`→`decobake ready`→`gpudeco live scope song`→`gpudeco live prepare`→`resume`→`stop`)에서 한 번 준비하고, 그다음 `seek 260 12`→`decobake ready`→`waitsongpos 256 40`→`gpudeco packets compiled`→`gpudeco live on`** 하는 순서였다(참고 계획 `tools/optimization/out/round121-pv-steps.json`, `round122-nombp-steps.json`). 렌더 노드 1,900개 문제는 **소유 23,547개(스프라이트→GPU 메시로 바뀐 것)** 상태에서 보이는 렌더러 수다. 소유 26개 상태에서는 병합할 대상 자체가 없다.
할 일: `render-merge-steps.py` 의 준비 단계를 위의 증명된 순서(곡 처음에서 준비 → 정지 → seek 260 → live on)로 바꾸고, `owned` 가 20,000 이상인지 `micro_end` 줄에서 확인하라. 그 상태에서 비로소 렌더러 수(`visible_renderers`)와 FPS 의 A/B 차이가 의미 있다. B 팔 전환 시 'fallback layout changed' 는 병합 준비가 live on 시점 레이아웃과 안 맞는다는 뜻이니, 준비를 재생 중 또는 live on 직후로 맞춰라.
