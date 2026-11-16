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

## 6. 정정 (저녁): 보임 판정 버그 → 낮의 FPS 이득 일부는 가짜
- 켜자마자 멈춘 픽셀 대조만 했던 탓에 못 봤다. **재생 8초 뒤 멈춘 대조**(gx-plans/pp-*-st.json)에서 Exit PSNR 24.8, HELLO 약 35.
- 원인: 유니티 카메라가 맵 레이어를 안 그려 scrFloor/ToggleVisible/Mawaru_Sprite 의 OnBecameVisible/Invisible, scrVisualDecoration.UpdateShader 등의 renderer.isVisible 이 "안 보임"으로 굳음 → 타일 Update(아이콘·색)·필터 장식 갱신 정지. 끄고 다시 켜면 맞음(Exit d0 대 d2 63.2)이 단서. 포즈·상수·순서·캐시·캔버스·파티클 컬링은 모두 강제 갱신해도 그대로라 배제.
- 고침: DirectFull.Visibility.cs — 메인 카메라 CullingGroup(경계 구)로 보임 대행, 위 콜백은 우리 판정으로만 호출, isVisible 호출 4곳 바꿔치기. 결과 Exit 63.2(최대 2), HELLO 43.4.
- 정직한 FPS: HELLO 119.9(146~152 는 가짜) → 재질 값 빠른 경로 135.3, Exit 141 → 190. 원본 HELLO 약 204, Exit 348.
- 재질 값 빠른 경로: Material.SetColor/SetFloat/SetVector/SetInt/set_color 는 바뀐 속성 상수만(속성 기반 상태가 바뀌면 일반 경로). DOTween.dll 도 감시(재질 트윈 람다). 검증 192,819번 불일치 0.

## 7. 엔진 내부 프로파일(HELLO, 기호 풀기: E:\StutterFixWork\tools-sym\symb.py)
| | 원본 | 직통 |
|---|---|---|
| 카메라 그리기(Camera::CustomRender) | 약 1.5ms | 약 0.7ms |
| 출력 대기(WaitForPendingPresent) | 0.82ms | 0.12ms |
| 우리 PreCull | 0 | 1.9ms |
| 스크립트 Update+LateUpdate | 약 2.0ms | 약 3.0ms |
- 우리 비용이 0 이어도 렌더러 교체만으로 HELLO 상한 약 +10%. 압도적(2배)은 장식·타일·효과·DOTween 스크립트(약 0.85ms)를 대체하고 그 결과를 유니티 오브젝트 대신 GPU 로 직접 보내야(감시 비용도 사라짐) 가능: 추정 약 2.45ms.
- PreCull 1.9ms 내역: Refresh 28%(FillConstants·Bind·UpdateKey), ReplanCanvases 21%(LiveGraphic·CanvasGraphicVisible — 캔버스 SetAlpha 마다), RebuildFullOrder 12%(TryNativeInsert 의 O(n) 번호 갱신), PromoteSources 10%.

## 8. 밤 (11-16 밤~11-17): 렌더러 쪽 최적화와 다섯 맵 비교
- 효과 있음: 캔버스 SetAlpha 는 알파만 다시 판정(+2.5%), 감시 알림 경량화(문자열 만들기·이름 세기·중복 살아있음 확인 제거, +4%), 끼워 넣기 번호 프레임 끝 한 번(+3%), 명시 그리기 보임은 캔버스 시야 평면 재사용(카메라 행렬 읽기가 포즈 작업을 기다리게 했음).
- 효과 없음: 포즈 자리 유지(손해, 기본 끔), 입력 레이아웃 전역 캐시, 참조 비교자.
- HELLO 재생 뒤 대조의 흔들림은 게임 자체(원본 두 장도 PSNR 75 로 다름). 계획 `pp-hl-st2` 는 대조 실패로 멈추지 않는다.
- 정직한 FPS(3440, GpuDeco 끈 원본 대비): HELLO 155/200, Exit 219/343, WF 148~161/221, 7777 177/327, Arche 무거운 구간 59/150(다시 읽기 1394번/프레임).
- 원본 HELLO 감시 비용 내역(프로파일): 프레임의 7.7%, 출처 1위 DOTween UI 페이드(CanvasRenderer.SetAlpha), 2위 타일 재질 SetColor/SetFloat. 장식 쓰기는 작다.
- GpuDeco 켬 대 끔(같은 빌드): HELLO 189/200, WF 119.5/221(메인 카메라 GPU 4.61 → 8.27ms), Exit·7777·Arche 같거나 약간 낮음. 메인 스레드는 GpuDeco 가 줄인다(HELLO 4.03 → 3.75, WF 3.81 → 3.20). 게임·GpuDeco 모두 장식 텍스처 밉맵 없음(TextureManager.LoadTexture mipChain:false) → 밉맵 가설 기각. GPU 원인 미확인(RenderDoc 필요).
- 다음: 타일 전용 통로(FloorMeshRenderer.set_color 등 → 미리 연결한 그리기 상수에 직접), UI 페이드 쓰기는 추적 안 하는 대상이면 감시 경로 앞에서 바로 버리기.
