# 제보 버그: Silentroom - 1KXYZØ 에서 큰 타일이 투명해짐 (2026-11-15)

작업 폴더 `E:\StutterFixWork\wt-v14prod` (브랜치 claude/adofai-renderer). 커밋·게임 실행·설치 DLL 교체 금지(Claude 가 한다). 한국어 보고.
결과: `docs/handoff/silentroom-tiles-20261115-result.md`.

## 제보
- 테스터(v2.8.1 플레이어용, 1920x1080, RTX 4060 Ti): "이 부분에 저 큰 타일들이 투명해집니다. 켜져 있는 게 너무 많아서 어떤 게 문제인지 모르겠다." 판마다 3~10초 만에 죽고 다시 함 → **곡 시작 직후**에 보임.
- 맵: `D:\얼불춤 맵 파일\[노가다 크루] Silentroom - 1KXYZO\level.adofai` (타일 8,052, BPM 276, MoveTrack 5,621 / PositionTrack 746 / RecolorTrack 522 / AnimateTrack 64 / Hide 22).
- 곡 시작 부근 이벤트(일부): floor 70 `PositionTrack scale 0`(그다음 editorOnly scale 100 opacity 50), floor 70 `MoveTrack startTile [330,Start]~[565,Start] duration 0 opacity 0 angleOffset -9999`, floor 0/1/70 RecolorTrack(Neon/Standard). floor 565 `MoveTrack scale [0,0]` 뒤 `[100,100]`(duration 0) 등. 크기 바꾸는 MoveTrack 4,130개.
- 테스터 설정: v2.8.1 기본값 대부분 켬(FloorAnim, ZeroTween, RecolorDefer, RecolorSplit, TweenGuard, EffectSplit, MeshWarm, SkipSame, FastLoop, MoveFinish, DormantSkip, TileCull(타일 3000개 이상이면 자동) 등). 로그: Claude 스크래치패드 nogada/Player.log.

## Claude 가 이미 확인한 것
- TileCull 은 FloorAnim 이 직접 돌리는 타일 이동을 몰랐다(TweenFloor 후크만 있음) → FloorAnim.TweenFloor 에서 `TileCull.PinArg` 를 부르도록 고쳤다(미커밋, 같은 작업 트리). 하지만 곡 2~42초 `culltest`(꺼 둔 타일을 모두 켜고 다시 그려 비교) 20번 모두 차이 0 → **이번 증상의 원인은 TileCull 이 아님**.
- 지금 게임에서 처음부터 1~14초 화면을 기본 설정 vs 타일 최적화 7개 끔으로 저장하는 대조를 돌리는 중(결과는 Claude 가 이 문서에 덧붙임).

## 할 일
1. 코드로 원인 후보 좁히기: 곡 시작(따라잡기/ScrubToTime·angleOffset -9999·"이미 지난 효과"를 한 번에 적용하는 경로)에서 **타일 불투명도·크기·위치**를 다르게 만들 수 있는 모드 경로를 전부 찾아라. 특히
   - FloorAnim(OpaOnly/FastOpa, Zero 경로, RunSplit, Start 지연값), ZeroTween, TweenGuard, MoveFinish, EffectSplit/EffectBudget(효과 미루기·나누기 순서), RecolorDefer/RecolorSplit(색·스타일과 불투명도 상호작용, `dontChangeMySprite`), MeshWarm, PositionTrack scale 0 과 opacity 의 결합,
   - 원래 게임 `ffxMoveFloorPlus.StartEffect/TweenFloor/ScrubToTime`, `scrFloor.opacity` setter, `PositionTrack` 적용(scrLevelMaker) 과 모드 경로의 차이(디컴파일 `E:\StutterFixWork\decompiled\Assembly-CSharp\`).
   - 같은 타일에 duration 0 이동과 opacity 가 같은 프레임 여러 번 오는 경우(순서·덮어쓰기), 이전 트윈 Kill 의미.
2. 맵 파일을 오프라인으로 분석해 곡 0~15초에 실제로 실행되는 타일 효과 목록(시각, 대상 범위, 값)을 뽑고, 각 후보 경로가 그 효과들에서 원래 결과와 달라질 수 있는지 판정하라.
3. 확실한 버그면 고쳐라(제품 코드 허용, 기본값 변경 금지). 고칠 때는 원래 게임 코드와 같은 결과가 되는 근거(IL/디컴파일 줄)를 적어라.
4. Claude 가 게임에서 확인할 AUTOTEST 명령 순서(bench_v14 설정 예시: 기능별 끔 대조, `BENCH_GAME=1` 로 곡 처음부터 `waitsongpos t` + `framecmp grab/save`)를 결과에 적어라.
5. 4종 빌드 오류 0.
