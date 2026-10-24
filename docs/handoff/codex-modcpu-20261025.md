# Codex 지시서: 모드 쪽 managed CPU 시간 줄이기 (Arche 260~270초, GPU 소유 상태)
먼저 `docs/handoff/codex-master-20261022.md`(마스터)와 `docs/handoff/claude-perf-260-270-20261024.md`(성능 분해, 맨 아래 'T2 결과')를 읽어라.

## 배경 (측정으로 확정)
- 렌더 합치기(render-merge)는 **접었다**: 렌더러 1,880→69 로 줄여도 엔진 draw 가 2,530→3,815 로 늘어 FPS 166.9→152.6(-8.6%), 픽셀 불일치. 비용은 렌더러 노드 수가 아니라 draw 호출 수이고, 정렬 키가 장식마다 달라 draw 수를 못 줄인다. `docs/handoff/codex-render-merge-result.md` 참고. 다시 시도하지 말 것.
- 이름이 붙은 메인 스레드 비용(PerfView 4초 창, `docs/handoff/pv-260-268-on-named.txt`): 렌더 36.9%(엔진), 스크립트 48.3% 중 `scrDecorationManager.LateUpdate` 패치 14.7%(= `TightLate.ManagerEnd`/`DecoBake.GpuLiveFrame` 7.7% 안에 `DispatchGpuLiveSlice`→`GpuDecoCopy.Execute`→`Graphics.ExecuteCommandBuffer` 4.0%, `TightLate.List` 4.4%, `TightLate.Process` 4.3%), DOTween 업데이트 10.0%, `scrVfxPlus.Update_Patch` 4.7%, `ffxPlusBase.StartEffect` 4.6%(`FastMove.Prefix`→`DecoAnim.UpdatePrefix/StepPair` 3.7%). 잎 비용: `HashSet.Contains` 1.2%, `Object.IsNativeObjectAlive` 1.1%.
- 현재 기준선: 3440x1440@165, 260~270초, GPU 소유 23,547개에서 **약 166 FPS**(6.0ms/프레임). 모드 자신이 만든 managed 비용(TightLate+GpuLiveFrame+FastMove/DecoAnim+ copy 제출)이 합쳐 약 20% = 1.2ms. 다 없애도 약 208 FPS 가 상한이다.

## 목표
이 구간에서 **FPS 를 기준선 대비 +10% 이상(≥183)** 올린다. 화면 결과는 지금과 동일해야 한다(원본 대비 픽셀 비교 기준은 기존 G1/G2 게이트와 같게, 새 불일치 0).
후보(효과 큰 순서로 시도, 한 번에 하나씩 ABBA 로 확인): (1) `TightLate.List/Process/ManagerEnd` 를 매 프레임 훑지 않고 변경 있을 때만(이미 GPU 소유인 장식 목록은 한 번 만든 배열을 재사용), (2) `DispatchGpuLiveSlice` 의 CommandBuffer 를 매 프레임 새로 채우지 않고 재사용/한 번에 제출, (3) `FastMove.Prefix`/`DecoAnim` 에서 소유 장식의 효과는 일찍 빠져나가기, (4) `HashSet.Contains`·`IsNativeObjectAlive` 호출 횟수 줄이기. 게임 자체 비용(DOTween, scrVfxPlus)은 건드리지 않는다.

## 시험
`tools/optimization/modcpu-steps.py` 가 생성하는 계획(곡 처음에서 준비 → seek 260 → live on → 10초 창 ×2)을 쓴다. 판정 줄은 `gpudeco micro_end ... real_fps=` 와 `onupdate`/`fuse` 요약이다. 변경 전 기준선(첫 회전에서 코드 수정 없이 한 번)과 변경 후를 같은 환경(3440x1440@165)에서 비교해 장부에 숫자로 남겨라. 실행/설치/커밋은 자동 장치가 한다. 소유 수 `owned=23547` 인지 매번 확인(작으면 계획이 잘못된 것).
성공 조건 로그 줄: 직접 `MODCPU=PASS fps_before=... fps_after=...` 를 `GpuLog` 로 찍는 판정은 필요 없다. 대신 결과 문서 첫 줄에 `MODCPU=PASS` 또는 `MODCPU=FAIL` 과 숫자를 적어라.
