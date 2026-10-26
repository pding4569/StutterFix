# 다시 하기(게임 화면) 준비 줄이기 — 수집·표 만들기 건너뛰기 (Codex 작업 지시)

작업 폴더: E:\StutterFixWork\wt-rc (브랜치 claude/release-3.0.0-rc). 같은 폴더를 Claude 도 건드릴 수 있으니 **GpuDecoPlayer.cs 의 `Prepared()` 와 GcControl.cs 는 건드리지 말고**, 필요한 변경은 DecoBake 쪽 `PreparePlayer`/`EndRoundPlayerKeep`/`BeginPrepCollection` 근처로 한정한다.

## 문제 (게임 측정, Arche, 게임 화면 경로: game -> press -> 죽음 -> retry)
죽고 다시 하면 GPU 장식 때문에 로딩이 길어진다(GPU 장식 끈 판은 retry 가 5~8ms).
로그: `[GPU 장식] 준비 1173ms (이전 준비 결과 재사용): 수집 617, 표 만들기 대기 251, 이미지 계획 70, GPU 준비 234`
- 수집 617ms + 표 만들기 251ms = 868ms 가 다시 하기마다 같은 내용을 다시 만든다(레벨 데이터·장식이 안 바뀌었는데).
- 그때 관리 힙에 약 300MB 쓰레기도 남는다(수집 120~146MB + 표 만들기 180MB). 쓰레기가 쌓이면 몇 판 뒤 GC 가 0.8초 멈춘다.
- 목표: 같은 레벨 다시 하기에서 준비를 **300ms 이하**, 힙 증가 **50MB 이하**.

## 하고 싶은 것
`EndRoundPlayerKeep`(GpuDecoPlayer.cs, 읽기만) 이 라운드 끝마다 `baselineObjects/Ids/Decorations/Events`, `table`, `effectList`, `prepEffectSnapshot`, `prepFloors` 를 null 로 버린다.
다시 하기에서 `PreparePlayer` 가 `BeginPrepCollection(floors, true)` 로 전부 다시 모은다. 같은 레벨이 확실할 때는 이것들을 **버리지 않고 재사용**해 수집·표 만들기를 건너뛴다.

재사용 조건(전부 맞아야 하고, 하나라도 아니면 지금처럼 전체 수집 — 안전 쪽):
- 게임 화면 경로(에디터 아님: `scnEditor.instance` 없음), 같은 `scrDecorationManager.instance`, 장식 개수·객체 참조가 같음(`SameBaselineObjects` 가 이미 있음).
- 레벨 데이터가 같음: 같은 `scnGame`/levelData 참조와 이벤트 개수, 타일(scrFloor) 개수가 같음. 값싼 지문(개수 + 몇 개 필드 해시)을 쓰고, 에디터에서 편집 중이면 재사용 금지.
- 이전 라운드에서 GPU 장식이 정상 종료(복원 성공)했음(`gpuLastRestoreComplete`), 막힘(Blocked) 아님.
- 장식의 "처음 Setup 기록"(`initial`)은 이미 남아 있음(재사용 대상). 시작 상태 읽기는 활성화 때 현재 값을 다시 읽으므로 영향 없음.

## 검증(필수)
1. AUTOTEST 명령 `gpudeco light prepreuse on|off|verify` 추가(`GpuDecoExport.cs` 의 `light` 명령들 옆). `verify` 는 재사용 경로를 쓰면서도 전체 수집을 한 번 더 돌려
   `PlayerSetupHash()`·baselineObjects 순서·table.Decorations 개수/Initial 값 해시를 비교해 `[GPU 장식] 준비 재사용 검증: 같음/다름 N` 로그를 남긴다.
2. 다르면 재사용을 끄고 전체 수집으로 돌아가는 폴백이 반드시 있어야 한다.
3. 두 빌드(`-c Release -p:Edition=Player -p:AutoTestBuild=1`, `-c Release -p:Edition=Player`) 오류 0. 게임 실행·커밋·DLL 설치 금지.
4. 끝나면 `docs/research/retry-prepare-reuse-notes.md` 에 무엇을 보존/재사용하는지, 재사용 조건, 남은 위험을 적는다.
