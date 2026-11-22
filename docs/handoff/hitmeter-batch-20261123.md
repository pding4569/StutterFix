# Codex 작업: 판정 오차 막대(scrHitErrorMeter) 메인 스레드 비용 줄이기 (2026-11-23)

작업 폴더 `E:\StutterFixWork\wt-v14prod`, 브랜치 `claude/v3-282port`. **게임 실행·종료, 설치 DLL 교체, git 커밋/푸시는 하지 않는다.** 게임 측정은 Claude 가 한다.

## 측정 근거 (Claude, 2026-11-23)
- HELLO 2026 곡 12~52초, 판마다 새로 켠 ABBA: 막대 캔버스(`Error Meter(Clone)`, ScreenSpaceOverlay 캔버스 2개, Graphic 244개)를 통째로 끄면 299.3/301.4 → 311.0/309.5 FPS(+3.3%), 메인 스레드 3.24 → 3.14ms. 즉 막대가 프레임당 약 0.1ms 를 쓴다. 이 값이 줄일 수 있는 상한이다.
- 같은 맵 메인 스레드 1kHz 프로파일(`E:\StutterFixWork\night-results\profH-gate-frames.txt`, 심볼 풀기는 `C:\Temp\claude\c76b1956af29c992\18ada627-573c-4620-ae83-dda0aee45efa\scratchpad\symlib2.py <파일> <정규식>`): `UnityEngine.Canvas:SendWillRenderCanvases` 1.9%, `UI::Canvas::UpdateBatches` 2.1%, `CanvasUpdateRegistry:PerformUpdate` 1.3%, `Graphic:Rebuild`/`DoMeshGeneration`/`UpdateGeometry` 각 0.8%, `HitMeterFade` 람다 0.8%, `UI::CanvasManager::WillRenderCanvases` 4.8%(모든 캔버스 합).
- 기존 최적화: `HitMeterFade.cs`(눈금 사라질 때 Image.DOColor 대신 CanvasRenderer 투명도만, 바늘 트윈 대체). 눈금 하위 캔버스는 게임이 이미 붙여 둔다(11-12 확인, 이득 0).

## 할 일
1. 게임 코드(`scrHitErrorMeter`: AddHit, DrawCurvedTick, DrawStraightTick, Reset, UpdateLayout, 필드 cachedTicks/tickCacheSize/tickLife 등, 그리고 tickPrefab 의 구성 요소)와 `HitMeterFade.cs` 를 읽고, 0.1ms 가 어디서 나오는지 밝힌다. 디컴파일은 ILSpy MCP 나 `tools/ilscan`(CLAUDE.md 참고)으로 본다. **디컴파일한 게임 코드를 저장소 파일로 저장하거나 커밋 대상에 넣지 않는다**(공개 저장소).
2. 화면이 픽셀까지 같은 채로 그 비용을 줄이는 방법을 구현한다. 우선 후보:
   - (A) 눈금 전부를 하나의 MaskableGraphic(한 메시)로 그리기. 원래 눈금은 그리기만 막고(상태·트윈·게임 로직은 유지), 같은 스프라이트·재질·정점 색·UV·순서를 한 메시에 옮긴다. 바뀐 프레임에만 메시를 다시 만든다.
   - (B) 매 프레임 캔버스 전체가 다시 묶이는(UpdateBatches) 원인이 투명도 변경이라면, 투명도가 실제로 바뀐 눈금만 갱신하거나 다 사라진 눈금을 그리기 목록에서 빼는 방법.
   - 다른 방법이 더 싸고 정확하면 그것을 써도 된다. 막히면 다른 방법을 시도한다.
3. 설정 `HitMeterBatch`(기본 **꺼짐**)와 AUTOTEST 명령(예: `meterbatch on|off|state|verify`)을 추가한다. verify 는 켬/끔 같은 프레임 화면을 비교할 수 있게 기존 캡처 도구(`shot`, framecmp 등 AutoTest.cs 의 기존 명령)를 활용하는 절차를 명시한다.
4. 게임에 건 것(Harmony 패치, 만든 GameObject)은 `Main.Unload` 와 맵 전환에서 되돌린다. 매 프레임 할당 0, 예외 시 원래 경로로 돌아가기.
5. 빌드 확인: `dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1`, `dotnet build -c Debug -p:Edition=Player`, LoadCheck(`tools/LoadCheck`) 오류 0.

## 결과물
- 변경 파일 목록(명시적 경로), 설계·근거, 남은 위험.
- Claude 가 돌릴 시험 계획: 픽셀 비교 단계와 HELLO ABBA(켬/끔, 곡 12~52초) 명령.
- 결과 문서: `docs/handoff/hitmeter-batch-20261123-result.md`.
