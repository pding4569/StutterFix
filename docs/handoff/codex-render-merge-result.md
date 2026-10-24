# render-merge 구현 결과

`codex-render-merge-20261025.md`와 마스터 인계에 따른 코드·시험 계획 준비 결과다. **실제 게임 시험은 실행하지 않았다.** 게임 실행·설치·커밋·푸시·설정 파일 수정 0회. FPS·픽셀·라이브 자원 복원 게이트는 미측정이며 채택/PASS를 주장하지 않는다.

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

현재 판정: `RENDERMERGE=ERROR reason=GAME_NOT_RUN fps=NA pixels=NA renderers=NA` — 코드 준비 상태이며 실험 실패 수치가 아니다.
