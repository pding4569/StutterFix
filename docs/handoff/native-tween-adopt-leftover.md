# 네이티브 트윈 이어받기 — 남는 몇 개 없애기 (Codex 작업 지시)

작업 폴더: E:\StutterFixWork\wt-port (브랜치 claude/player-port). `GpuDecoNativeAdopt.cs` 가 이미 있다(이어받기 구현, 기본 꺼짐 `DrawCollapsePolicy.AdoptNativeTweens`).

## 게임 측정 (Arche 에디터 곡 254.4초 중간 시작, 이어받기 켬)
거의 다 이어받지만(230개 장식) **1~5개가 계속 거절**된다. 거절 조합:
- `[PositionX+PositionY+Rotation+Color (모두 네이티브)]` 2~5개
- `[Color(네이티브)]` 1~2개
(로그 형식: `이어받기: N개(장식), 채널 M개, 값 불일치 0개, 거절 3=5 [조합]=개수`; 코드 3 = `NativeAdopt` 안의 일반 조건/Color 조건/위치 조건 중 어느 `return NR(3)`)

이 몇 개가 CPU 처리로 남으면 그 장식의 원래 렌더러가 "정렬 장벽"이 되어, 같은 정렬 키(층 -6, 순서 0, 큐 3000, 거리 0, 같은 머티리얼)인 소유 장식과 겹치면
`drawcollapse ambiguous material tie` 예외로 GPU 장식이 통째로 꺼진다(FPS 400 -> 268). 그래서 **거절 0개**가 목표다.

## 할 일
1. 거절마다 **정확히 어떤 조건**인지 알 수 있게 NR 코드를 더 잘게 나눈다(같은 번호 `NR(3)` 가 여러 곳). 예: 일반 트윈 조건(257행 부근)은 조건 하나하나(Delay, IsPlaying, ElapsedDelay, Loops 등)로 코드 분리.
   Color 조건(`alphaOnly` 등)도 코드 분리. AUTOTEST 로그 `거절` 줄에 코드별 개수와, 가능하면 조합별 코드.
2. 코드를 분리한 뒤 어떤 조건에 걸리는지 IL/코드를 읽고 추측해 보라(예: 지연(Delay)이 남은 트윈, Color 트윈이 alphaOnly(불투명도), 이징 미지원, 시작 전 트윈(`!IsInitialized`) 등).
   **안전하게 이어받을 수 있는 조건만** 풀어라. 지연(Delay)이 남은 트윈은 GPU 쪽에 지연 표현이 있는지 `DecoAnim`/`GpuDecoGpu` 의 지연 처리(`GpuResumeSpec`, Over/Period/지연 필드)를 확인하고, 없으면 풀지 말고 보고만 한다.
3. 이어받은 것의 현재 값 일치 검사(비트)는 그대로 유지.
4. 두 빌드(`-c Release -p:Edition=Player -p:AutoTestBuild=1`, `-c Release -p:Edition=Player`) 오류 0. 게임 실행·커밋·DLL 설치 금지.
5. 끝나면 `docs/research/native-tween-adopt-impl-notes.md` 에 "남는 거절" 절을 추가: 코드별 의미, 풀었는지, 못 푼 이유.
