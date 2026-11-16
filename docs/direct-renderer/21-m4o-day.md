# M4o (Claude, 2026-11-16 낮) — 처음 켜짐 전체 재계획, GPU 거품, 감시 장치 비용

브랜치 `claude/drfull-m4n-wip`. 3440x1440 렌더, 12초 fpsmark, 판마다 새로 켬. A = 원본, B = drfull on.
"cam" = presentpath gputime 의 메인 카메라(Camera) GPU 평균 µs.

## 1. 전체 재계획의 원인 = 처음 켜지는 장식·타일
- HELLO B(미리 만들기 없음): 1,289프레임 중 전체 재계획 784번. 렌더 스레드 plan 2.06 + record 3.22ms/프레임.
- order 사유 `passes`(첫 패스 만들기) ~3,000번/12초. 꺼져 있던 렌더러가 켜지면 그리기 목록 길이가 바뀌고(절대 위치) 전부 다시 짠다.
- 미리 만들기(`drfull-prebuild.txt`, 꺼진 단순 스프라이트, 큐 ≥2501, 지원 셰이더): HELLO 전체 재계획 784→152, render 6.3→2.8ms, **84.8→106.2 FPS**. 픽셀 PSNR 42.9(전과 같음).
- 남은 원인(`layout_why`, 새 계수): `promote:MeshRenderer:ADOFAI/FloorMesh` 3,496번 = **타일**. `layout_size`127 / `layout_segcount`31 / `layout_segbounds`0 / `layout_same`10. → 메시도 미리 만들기(`drfull-prebuild-mesh.txt`) 측정 중.

## 2. 꺼진 그리기도 GPU 비용(간접 그리기)
- 미리 만들기 판 Exit 일시정지: indirect 479개/프레임(전 48), 그리기 단계 GPU +1.75ms → **간접 그리기 하나 약 4µs**(D3D11 NVIDIA).
- `drfull cache direct`: 단독(배치 아닌) 그리기는 직접 DrawIndexed 로 기록하고, 꺼진 것은 기록에서 뺀다. 켜짐/꺼짐은 그 위치만 표시해 그 조각만 다시 기록(전체 재계획 없음). GPU 프러스텀 컬링은 단독 그리기에서 없어지지만 화면 밖 그리기는 잘려서 픽셀 같음.
- 결과: Exit 231.8→244.1, DD 189.0→212.8(이전 최고 207.8), HELLO 106.2→108.7. HELLO 픽셀 PSNR 42.8.

## 3. 카메라 안 GPU 거품 = 렌더 스레드 준비 시간
- 메인 카메라 GPU 가 원본보다 길었다: HELLO 4.18→6.53ms, WF 4.60→6.73, DD 3.95→4.57, Exit 2.48→2.80.
- 가설: 카메라 안에서 우리 렌더 스레드가 apply/plan/record(HELLO 2.7ms) 하는 동안 GPU 가 논다(timestamp 는 벽시계).
- 고침: 메인 스레드 PreCull 끝(CommitFull 뒤)에 `GL.IssuePluginEvent(..., 0xFFFE)` → 렌더 스레드가 카메라 전에 packet 적용·plan·상수·컬링·바뀐 조각 미리 기록(`prepareCached`, `drfull-early.txt`). 카메라 안에서는 목록 실행만.
- 결과 HELLO: 카메라 안 render_us 2.72→**0.31ms**, cam 6.53→6.17, 현재대기 2.63→0.78ms, FPS 108.4(메인 스레드가 병목으로 바뀜). Exit 244.1→248.9. HELLO 픽셀 PSNR 43.5.
- 전체 재계획 때도 같은 내용 조각은 명령 목록 다시 씀(`cacheReuse`, 첫 위치 기준).

## 4. 메인 스레드 (HELLO, phase_window, ms/프레임)
| 단계 | A | B(early) |
|---|---|---|
| 합계 | 4.87 | 9.22 |
| FinishFrameRendering | 1.90 | 3.80 (우리 PreCull 1.93 포함) |
| ScriptRunBehaviourUpdate | 1.31 | **3.04** |
| ScriptRunBehaviourLateUpdate | 0.71 | 0.78 |
- Update +1.73ms 는 게임 스크립트 안 감시 장치(IL 호출 바꿔치기) 비용으로 추정. Transform 쓰기 2,427번/프레임이 PoseNotify 우회로 여전히 바꿔치기 경로를 탔다.
- 고침(측정 중): `PoseNotify=false` 면 Transform 쓰기는 IL 을 아예 안 바꾼다(`Kind` → -1). 쓰기마다 (형식, 이름) 사전 세기는 `drfull-lean.txt` 로 끈다.
- 확인 판: 원본 + 감시 장치만(`gw-hl-warm`) 대 원본(`gw-hl-A`) 대 lean(`gl-hl-warm`).
- B 의 FinishFrameRendering 에서 우리 PreCull 을 빼면 1.87 = A 1.90 → **원본 Unity 의 맵 그리기 메인 스레드 비용은 거의 없다**. 렌더러 교체만으로 줄일 수 있는 상한은 작고(Exit 상한 +28%), 압도적이려면 게임 스크립트(Update/LateUpdate 2.0ms) 대체가 필요하다.

## 5. 기타
- DD 픽셀: 미리 만들기 판 431픽셀 최대 4(전에는 비트 동일). 원인 미확인.
- WF: A 220.7/218.3, B 194.8/174.8.
- `gx-cacheoff`/`gx-graboff` 는 계획의 `drfull assert cache` 로 실패 → `*2.json` 으로 다시.
