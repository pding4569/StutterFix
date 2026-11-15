# 코덱스 작업: M4f — 비용 예산 안으로 (2026-11-15 밤)

작업 폴더 `E:\StutterFixWork\wt-m4` (브랜치 claude/drfull). 커밋·게임 실행·설치 DLL 교체 금지. 한국어 보고. 결과: `docs/direct-renderer/10-m4f-result.md`.
앞 문서: `09-m4e-spec.md`, `09-m4e-result.md`. Claude 가 이번에 고친 것(커밋됨): 꺼진 캔버스가 켜지면 그 캔버스만 승격(`PromoteCanvases`), 파괴된 대상은 그 대상만 빼기, 새로 생긴 렌더러/캔버스·부모 변경은 dormant 추가/재정렬만(전체 재스캔 대신), `MarkRescan` 사유 로그. (원래는 일시정지 중 캔버스 "B", 재생 중 "Error Meter(Clone)" SetActive 로 매번 RecreateFull → 실패.)

## 실게임 (3440 렌더, Claude)
| 맵 | 원본 FPS | 상한(맵 그리기 뺌) | M4d | M4e+증분 |
|---|---|---|---|---|
| Exit 88초 | 327.9 / 326.4 | 477.4 / 475.0 | 68.5 / 41.1 | **111.6** |
| DD 50초 | 248.5 / 242.0 | 364.8 | 22.2 / 28.5 | (재측정 필요) |
- Exit B1 state: sources 6976(dormant 23341), frames 1543, **main_us 3,063 (sort_us 2,112, pose_us 510, collect_us 405, packet 35)**, main_max 40,572, **replans 338 / 1543 프레임**, replan_max 11,149, **render_us 2,881** (rt_execute 462, rt_draw 158), setup_ms 29,070.
- DD pixel(일시정지): sources 1035, dormant 46979, main_us 1,255, render_us 2,430, rt_execute 4,077(첫 프레임 포함), setup_ms 30,088. 픽셀: UI 칸 밖은 시간으로 움직이는 필터 가로줄 하나(잡음 d0/d00 1.2% 와 같은 성격)만 다름 → 그림은 맞다. 잡음 대조(FPS UI 동결)는 여전히 실패: off/off 가 왼쪽 가운데 칸에서 다름.

## 예산 (가장 중요)
Exit: 원본 3.06ms/프레임, 맵 그리기를 뺀 상한 2.10ms → **우리 직통 그리기 전체(메인 + 렌더 스레드 임계 경로)가 0.95ms 미만이어야 원본보다 빠르고, "압도적"이려면 0.3ms 대**. 지금 ~6ms. Unity 는 같은 렌더러 ~7000개를 대략 0.14µs/개로 처리한다(동적 배칭). 이 목표에 맞게 구조를 바꿔라:
1. **정렬은 증분으로**: 전체 재정렬(RebuildFullOrder, O(n log n)) 금지. 키가 바뀐/켜진/꺼진 항목만 제거·삽입(정렬된 배열에서 이진 탐색, 또는 정렬 구간 단위 버킷). 무엇이 orderDirty 를 일으키는지 `drfull why` 로 세고(Exit 338/1543), 불필요한 것은 없앤다(Live 토글은 키 순서를 바꾸지 않는다 → 정렬 대신 켜짐 비트).
2. **포즈 추적 재할당 금지**: Live 가 바뀔 때마다 `BuildPoseTracking` 이 모든 NativeArray·TransformAccessArray 를 다시 만들고 haveNativeFrame=false(전체 업로드). 고정 슬롯 + 켜짐 비트로, 추가/제거는 swap-remove.
3. **렌더 스레드 2.9ms**: 소스마다 상수 버퍼 쓰기·드로우 호출. 같은 셰이더 변형·텍스처·상태가 정렬 순서상 연속인 스프라이트/타일은 **한 번에 그려라**: GPU(또는 렌더 스레드)에서 정점을 월드 공간으로 미리 변환한 큰 정점 버퍼 + unity_ObjectToWorld=단위행렬로 원래 DXBC 를 그대로 한 번 Draw(Unity 동적 배칭과 같은 원리, 셰이더는 원래 것). 스프라이트 색(_RendererColor/정점 색)·MPB 가 다른 것은 배치 경계 또는 정점 색으로. 픽셀이 같은지 WARP fixture 로 증명.
4. **켜기 준비 29~30초**: 이유를 단계별로 재고 1초 안으로(또는 맵 열 때 미리). 셰이더/입력 layout 은 캐시, dormant 4만 개는 목록만.
5. 매 프레임 할당 0, 곡 중 main_max < 2ms.
6. FPS UI 동결(잡음 대조)도 고쳐라: 동결 대상이 실제로 그 UI 인지 확인(off/off 0 이어야 함).

## 검증
- 4종 빌드 오류 0, export 보존, fixture 통과. state 에 단계별 시간 유지.
- Claude 가 돌릴 계획(m4f 접두사, 4맵 pixel + ABBA)과 결과 문서.
