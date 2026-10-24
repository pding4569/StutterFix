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
