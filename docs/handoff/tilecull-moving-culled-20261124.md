# TileCull: 움직이는 화면 밖 타일을 바로 켜지 않기 (2026-11-24, Codex 작업 문서)

작업 위치 `E:\StutterFixWork\wt-v14prod`, 브랜치 `claude/v3-282port`, 시작 HEAD 는 `git log -1` 로 확인.
**게임 실행·설치 DLL 교체·커밋·푸시·`git add -A` 금지.** 게임 대조·측정은 Claude 가 한다. 디컴파일 게임 코드는 `E:\StutterFixWork\decompiled-20261124` 에서 읽기만.

## 문제 (측정)

타일 이동 효과(FloorAnim 이 처리, `ffxMoveFloorPlus`)가 많은 타일을 움직이면 `FloorAnim.StartPrefix → TileCull.PinArg → TileCull.Pin → TileCull.Uncull` 이 움직이는 타일마다 화면 밖이라도 렌더러를 켠다(`r.enabled = true`, 엔진 `RendererScene::AddRenderer`). 한 프레임에 수천 개면 길어진다.
- PLUM MEGAMIX 596.17초(결과 화면, 타일 4,892개 대상) 39ms 프레임, 표본 대부분 TileCull.Uncull/Pin.
- Lost Requiem 528.39초 25ms, PLUM 530.75초 20ms 프레임에도 같은 경로.
원자료: `E:\StutterFixWork\night-results\dropscan8\{plum,lost}\profsamp-frames.txt`(심볼: `E:\StutterFixWork\tools-sym\symb_lib.py`).

## 목표

`TileCull.Pin(i)` 에서 타일이 꺼져(culled, st==1) 있으면 바로 켜지 말고 "꺼진 채 움직이는" 상태로 두었다가, **그 타일이 실제로 화면(+여유) 안에 들어올 수 있을 때만** 켠다. 그림은 지금과 같아야 한다: 카메라에 보일 수 있는 타일은 그리기 전에 반드시 켜져 있어야 한다.

## 할 일

1. `TileCull.cs` 전체 상태 기계(st 0/1/2/3, cover/quiet, 격자 등록, Move/Step/Tick, PinArg/PinComp/PinGo/ScriptPrefix, RecolorDefer.FlushFloor, glow 렌더러, StopAll/되돌리기)를 읽고 문서에 짧게 정리한다.
2. 새 상태를 설계한다. 요구:
   - 꺼진 채 움직이는 타일은 매 프레임(그리기 전, 기존 Step 시점이 렌더 전인지 확인) 현재 월드 위치·크기로 보수적 경계를 계산해 카메라 범위(기존 격자 여유와 같거나 더 넓게)와 겹치면 기존 Uncull(색 미룬 것 먼저 적용 포함)을 한다. 회전·큰 크기·부모 변환·카메라 회전/확대를 고려한 **보수적** 판정(애매하면 켠다).
   - 움직임이 멈추면(기존 pinned 의 quiet 처리와 같은 방식) 다시 격자에 넣어 원래 관리로 돌아간다.
   - 스크립트가 도는 경우(ScriptPrefix), 색 바꾸기, 다른 이유로 Pin 되는 경우, TileCull 정지(StopAll)·장면 바뀜·곡 끝/결과 화면(Hitch.AfterSong) 등 모든 경로에서 상태가 새지 않게. 꺼진 채 남는 타일이 보이면 안 된다(정지 때는 기존처럼 전부 원래대로).
   - 프레임당 비용: 꺼진 채 움직이는 타일 수 N 에 대해 O(N) 의 가벼운 계산만(엔진 호출 최소: transform 읽기는 필요한 만큼).
3. 정적 스위치 `TileCull.LazyMovingUncull`(기본 true 로 둘지 판단은 Claude 측정 후; 일단 코드상 기본값은 true 로 하고 문서에 명시)와 AUTOTEST 명령 `tilecull lazymove on|off|status`(카운트: 미룬 수, 나중에 켠 수, 끝까지 안 켠 수).
4. 시험 빌드 대조: 기존 `culltest`(`TileCull.RenderCompare`: 꺼둔 타일을 켰을 때 타일 층 그림 비교, 대조군 포함)가 새 상태의 타일도 포함해 비교하는지 확인하고, 필요하면 포함되게 고친다. 추가로 AUTOTEST 전용 "보이는데 꺼져 있음" 검사(카메라 frustum/화면 사각형 대비 실제 렌더러 bounds 로, 꺼진 채 움직이는 타일 중 화면에 걸치는 것 수 = 0 이어야 함)를 프레임마다 켤 수 있게.
5. 세 빌드 오류 0: `dotnet build StutterFix.csproj -c Release -p:Edition=Player`, `-p:Edition=Debug`, `-p:Edition=Player -p:AutoTestBuild=1`.
6. 결과 문서 `docs/handoff/tilecull-moving-culled-20261124-result.md`: 바꾼 파일, 상태 기계 정리, 같은 그림 근거, 남은 위험, Claude 가 돌릴 명령(PLUM 530·596초, Lost 528초 근처에서 culltest·새 검사·프레임 측정).

## 하지 말 것
- 그림이 달라질 수 있는 근사(여유 축소, 보이는 타일 늦게 켜기).
- 게임 실행, 설치 폴더 쓰기, git commit/push.
- 막히면 같은 시도 반복 말고 다른 각도로(예: 효과 시작 때 대상 타일 범위의 화면 겹침을 한 번에 판정).
