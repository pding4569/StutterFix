# 이미 닫힌 실험과 지금 열린 후보

같은 시험을 반복하지 않으려고 모았다. 수치와 판단은 당시 기록이며 맵과 빌드가 달랐다. 근거는 메모리, `docs/optimization-36.md`, `docs/deco-spec/results/`.

## 닫힌 것

| 실험 | 결과 | 비고 |
|---|---|---|
| 일반 스프라이트 투명 영역 메시 | 픽셀 차이: 2026-11-04 DD 약 3,600~4,000픽셀(최대 3/255), 이전 Arche 832/990 | 여유를 늘려도 그대로. FPS는 미측정 |
| 장식 계산 전용 엔진 `DecoEngine` | Arche 3.04→3.02ms | 계산이 아니라 쓰기가 비용 |
| 표로 장식 움직이기(adopt) | +0.45ms 느려짐 | 같은 이유 |
| `IJobParallelForTransform` | 쓰기 약 0.2ms/2,150개라 이득 없음 | 메인 스레드 측정 |
| 화면 밖 장식 건너뛰기 | Arche 움직이는 장식 77%가 화면 안 | 다른 맵은 미측정 |
| Late 등록 기억·Compact 건너뛰기(N1) | 잡음 수준 | 표본 4.7% 예측이 실제 0.05~0.1ms |
| 재질 공유(이상적 묶음) | 그리기 58~62%만 남고 GPU -1.3ms 수준 | 동점 정렬 문제 |
| 타일 굽기 | 0/15,001 지원 | 보류 |
| 일 없는 타일 Update 생략 | -0.023ms(잡음 안) | 게임이 이미 화면 밖을 끔 |
| 홀드 재질 조회만 줄이기 | -0.003ms | 홀드 자체 건너뛰기는 다른 시험 |
| 활성화 하한 1만 | 되돌림 | 사용자 결정 |
| 첫 열기 압축 대기 | 되돌림 | FPS 이득 없이 DD 첫 열기 25.6초 증가 |
| `AudioListener`·`Physics2D` 큰 비용 가설 | 표본에서 작음 | `Physics2D` 상위 500 밖 |

## 측정으로 확인된 이득, 아직 안 넣음

| 후보 | 결과 | 남은 확인 |
|---|---|---|
| JIT `aggressive-inlining` | Windflower 216→224 FPS(+3.7%), `all`은 Exit +3~6% | 곡 전체 끊김, 크래시, 켜는 시점 |
| 증분 GC 끄기(`gc-max-time-slice` 제거) | Exit +1.1%, Windflower +2.1% | 쓰기 장벽(`wbarrier` 표본 0.6~3.0%)과 맞음. 곡 밖 GC 멈춤 시간 |
| DXVK | DD +9.5%, HELLO +7.2%, 나머지 0~1.5%, GPU 장식 Arche -8% | 끊김, 픽셀, 호환성, 3440 |

## 대기 중인 시험 (`deep-queue.py`, `deep-queue2.py`~`deep-queue9.py`, 시작은 `start-deep.sh`)

- 계층 나누기(`flatten`), `Dormancy` 거르기, 보통 맵 12개.
- 런타임 전수 조사(`rtcensus`), 렌더 스레드·작업 스레드·메인 네이티브 표본.
- 빈 배경 카메라 끄기(`camskip`).
- 투명 플래시 판 끄기(`flashskip`), 마스크 노드 배열 엔진 패치(`maskgrow`).
- 곡 중 Rewired 끄기(`rewiredoff`): 키보드는 Unity Input을 쓰고 Rewired는 `RDInputType_Joystick`·`PlayerSelect`·`ADOStartup` 등에서만 쓴다. 표본 1.2~2.3%. 조이스틱 사용자는 제외해야 한다.
- 마우스 이벤트 단계 빼기(`mousedrop`): `OnMouse*`는 `ObjectRotator`, `TransformGizmo`, `XRHare`, `DisableRendererOnMouseDown`에만 있다. 단계 비용 0.03~0.11ms.
- 일 없는 홀드 건너뛰기(`holdskip`): 값 대조 뒤 ABBA. 표본 Exit 5.0%, Windflower 2.5%.
- 숨은 부팅 옵션 8종 HELLO 고르기: `force-d3d11-singlethreaded`, `force-d3d11-nothreads`, `job-worker-count` 3·8, `job-system-wait-spin-time-ns` 0·20만, `no-main-thread-job-stealing`, 임시 할당기 크기.
- 렌더러 플래그 끄기(`rflags`): 타일 프리팹은 그림자 On, 빛·반사 프로브 On, 모션 벡터 On이다. 조명이 없어 그림은 같아야 한다.
- 타일 색 MPB(`tilempb`): [Unity 문서](https://docs.unity3d.com/kr/560/ScriptReference/Renderer.SetPropertyBlock.html)가 멀티스레드 렌더링에서 재질 직접 쓰기보다 효율적이라고 한다. 오래된 문서라 측정으로 확인한다. 화면 밖 타일 색은 이미 `RecolorDefer`가 처리한다.
- RenderDoc 캡처(`rd-run.py`, 처음 쓰는 경로라 실패할 수 있다).

## 작아서 시험 전인 것

판정 눈금 페이드를 트윈 없이 한 반복문으로(표본 0.3~1.1%), `scrController.UpdateInput`의 키 전체 순회 줄이기(표본 0.8~1.9%), 장식 프리팹의 편집기 전용 자식 줄이기(맵 열기, Transform 수), Quartz 오버레이 끈 비교(표본 1.4~3.2%, TMP 별도).

## 사용자 PC 설정에서 나온 변수

레지스트리: HAGS 켜짐(`HwSchMode=2`), MPO 꺼짐(`OverlayTestMode=5`). 전원 구성은 Atlas. 지금까지의 측정은 전부 이 상태다.

- NVIDIA 전원 관리: 부하가 낮으면 P 상태가 내려간다(측정 시점 P3, 435MHz). 같은 맵이 76~236 FPS로 갈린 판의 후보지만 확인 전이다. 게임별 "최대 성능 선호"와 HAGS 끔을 사용자 전환으로 비교할 수 있다.
- DXVK 선택 항목은 픽셀을 안 바꾸는 쪽이 `dxgi.maxFrameLatency`, `dxvk.tearFree` 등이며 효과는 재 봐야 한다. `d3d11.relaxedBarriers`는 쓰지 않는다.

## 인터넷에서 확인한 것

- [Unity 카메라 최적화 글](https://unity.com/blog/games/optimize-game-performance-with-camera-usage): 카메라마다 고정 비용이 있고 불필요한 카메라를 없애라고 권한다. `camskip` 시험의 근거.
- `job-worker-count`는 기본값보다 크면 [무시된다](https://docs.unity3d.com/2023.2/Documentation/ScriptReference/Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerMaximumCount.html). 시험을 3·4로 바꿨다.
- [Unity 명령줄 문서](https://docs.unity.com/en-us/engine/6000.3/manual/unity-editor/command-line-arguments/player): `-force-d3d11-singlethreaded`, `-log-memory-performance-stats`. 엔진 코드에서 단일 스레드 플래그와 스레드 최적화 금지 플래그(`nothreads`)가 실제로 쓰이는 것을 확인했다. 기본값은 단일 스레드가 아니다.
- 증분 GC를 끄면 GC가 도는 순간 멈춤이 길어진다는 [합성 시험](https://gist.github.com/jechter/2730225240163a806fcc15c44c5ac2d6)이 있다. 곡 중에는 GC를 멈추지만 곡 밖 멈춤은 재야 한다.
- Mono·할당기 교체는 도움이 된다는 근거를 못 찾았다.
