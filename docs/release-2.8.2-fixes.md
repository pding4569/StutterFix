# 2.8.2 버그 수정 정리 (2026-11-18, 릴리스 전 — main·태그·GitHub 릴리스 없음)

브랜치 `release/2.8.2` (= v2.8.1 `d6fb971` 위에 올린 수정만). 3.0.0 계열(`claude/adofai-renderer` 등)에서 2.8.1 코드에 실제로 있는 버그만 골라 옮겼고, 옮길 때마다 2.8.1 에서 재현되는지 먼저 확인했다.
최종 확인은 정식 ZIP(`dist/StutterFix-2.8.2-player.zip`) 으로 HELLO 2026 전곡: 평균 165 FPS(모니터 꺼진 2560×1600 144Hz 조건), 끊김 0번, sf_errors 0, 정상 종료(`tools/ReleaseCheck/out/full-game-2.8.2.json`, 시험 동안만 Quartz 꺼 둠).

## 들어간 것

| # | 수정 | 2.8.1 에서의 증상 | 확인 | 커밋 |
|---|---|---|---|---|
| 1 | 히트음 예약 묶기 조각 이음매 '틱' | 0.5초 조각이 이어지는 자리에서 파형이 끊겨 딱 하는 잡음 | 녹음 분석(곡 음량 0, HELLO 이음매 47곳 × 2판): 이음매 파형 급변 중앙값 비율 **7.3배(79%가 상위 5% 초과) → 0.8~0.9배(4%, 우연 수준)**. 같은 입력에서 2.8.1 두 판 결과가 비트 단위로 같음(결정적). HitSoundCheck 4종 통과(겹침 합 오차 1.2e-7, 절대 DSP 시각 오차 1e-9 샘플, 할당 0) | `7eaafa8` |
| 2 | 죽은 직후 히트음 보호 보강 | `OnPlayerDied`·`Fail2Action` 에서 묶기가 안 멈추고, 실패 상태에서도 예약이 남을 수 있음 | AutoTest `audioplaying`(HELLO): 곡 중 죽음 52개 → 1개 → 0개, 처음에 죽음 1 → 0(남는 1개는 게임 자체 `SfxParentSrc`, 히트음 아님). 2026-11-22 실제 출력 녹음(audiotap, 곡 음량 0): HELLO 10초에서 자동 플레이 중 `auto off` 로 죽음 → 죽은 뒤 0.25초 단위 최대 진폭이 2.8.1·2.8.2·묶기 끔(게임 원래) 세 가지 모두 같은 모양(죽음 소리 0.034~0.074 → 1.25초 안에 0), 각 2판. 이 시나리오에서는 2.8.1 에도 죽은 뒤 히트음이 새어 나오지 않았고 2.8.2 도 원래 게임과 같다 | `7eaafa8` |
| 3 | Silentroom - 1KXYZØ 5225번 타일 근처 큰 타일 투명(에디터 재생, 제보) | FloorAnim 이 TileCull 에 타일 이동을 안 알려 꺼진 채 남음 | 2.8.1 + 제보자 Settings.xml 222개로 에디터 재생 3번 재현, TileCull 끔·FloorAnim 끔·수정 각 3번 정상 | `042211b` |
| 4 | `메모리 정리 미루기`(GcPause) 끄면 곡 시작/끝 처리 누락 | `GcControl.Tick` 이 꺼져 있으면 `Hitch.Tick` 전에 반환 → 셰이더 예열·물리 중지·저사양 등 곡 수명 처리가 통째로 안 돎. eLadia 제보(켜면 이펙트가 달라짐)의 직접 원인은 이것이 아니라 #8(곡 수명 처리에 묶여 있던 파티클 멈추기)이었다 | GcPause=false 로 HELLO 시작: 2.8.1 은 `[물리]`·`[저사양] 그래픽` 로그 0줄, 수정 뒤 GcPause 켠 판과 같은 로그. **2026-11-22 화면 비교**(메모리 정리 미루기 끔 사용자에게 곡 수명 처리가 새로 도는 영향): Arche 곡 40.0초(`waitsongpos`로 시각 정렬, 각 2판) 검은 영역 끔 80.71·80.58% / 켬 80.38·79.90%로 같음, HELLO·Windflower 는 끔/켬 차이가 같은 설정끼리 판 사이 차이와 같은 크기(평균 절대 차이 HELLO 25~33 대 24~31, Windflower 5.0~5.5 대 5.4~5.7). 단 `wait 42`처럼 시간으로만 맞추면 설정마다 곡 시각이 달라 Arche 에서 86.6% 대 89.0% 로 다르게 보이는 가짜 차이가 난다(처음에 이걸 물결 왜곡 버그로 오해했다가 시각 정렬로 아님을 확인). 이 비교는 세 곡 한 시점의 화면이며 모든 맵·시점 증명이 아님 | `ae7ef21` |
| 5 | 윈도우가 아닌 OS 에서 불러오기 오류 | 리눅스·맥 원래 유니티: `GetCurrentThreadId` EntryPointNotFoundException 으로 Load 가 죽음 | 윈도우에서 정상 불러오기만 확인(리눅스·맥 실기 시험 못 함, Wine/Proton 은 WindowsPlayer 라 그대로) | `cce00de` |
| 6 | 로그 `This game is modded (StutterFix 버전)` 한 줄 | 7th Beat Games 모딩 가이드라인 | 정식 ZIP 전곡 로그에서 확인 | `cce00de` |
| 7 | LoadCheck 거짓 경보 제거 | 2026-11-04 게임 업데이트로 `RDInputType_AsyncKeyboard.mainKeys` 가 없어져 `pack.sh` 의 LoadCheck 가 "틀림 1" 로 멈춤(실제로는 앞선 `GetSpecialInput` 검사에서 이미 꺼져 동작 변화 없음) | `pack.sh` 통과(Debug 266·Player 229곳 틀림 0), CompatCheck 설치된 게임 302개 중 못 찾은 것 0 | `c2ed40d` |
| 8 | 화면 밖 파티클 멈추기(LowPauseParticles) 기본 꺼짐 | 2.8.0 부터 기본 켬. 일부 맵에서 화면 안 파티클을 통째로 멈춰 없앰(eLadia 곡 27초 타일 주변 검은 효과). 곡 중 처리(Hitch.Playing)가 있을 때만 일해서 GC 멈춤 켬/끔에 따라 달라 보임(제보 "메모리 정리 미루기 켜면 이펙트 달라짐") | eLadia 에디터 재생, 곡 27초 화면의 검은 영역 비율: 최적화 전부 끔 13.3%/13.9%(GC 켬/끔)=원래 모습, 2.8.1 기본 켬 4.9%(사라짐)/끔 13.2~13.4%. 31개 기능 이분 탐색으로 이 기능 하나로 좁힘: 이 기능만 끄면 13.66% 복구, 파티클 따라잡기 옵션은 4.98% 로 해결 못 함. 2.8.2 기본값+2.8.1 식 설정(세대 1)에서 GC 켬/끔 13.91%/13.78% | `d099b46` |
| 9 | 자세한 로그 토글(VerboseLog)과 로그 폭주 막기(Lg) | 로그가 너무 많이 쌓여 다른 모드 로그를 읽기 힘들고, 로그가 폭주하면 크래시가 난다는 제보 | 정식 빌드 기본(보통): 필수 줄만. HELLO 전곡 모드 로그 108줄 → 14줄(정식 ZIP 실제 곡 한 판 측정: 보통 14줄, 자세히 110줄). 같은 줄 10초 3번·전체 초당 100줄 제한. LogCheck 27개 x 3 에디션, 빌드 3종·LoadCheck 통과 | `097e597` |
| 10 | 타이머 정밀도 0.5ms 요청 기본 켬 | 기본값 변경(버그 수정 아님). 예전 설정 파일에는 false 가 적혀 있어 설정 세대 3 으로 한 번만 덮어씀, 설정창 플레이 기본 탭으로 이동 | 사용자 결정(2026-11-22 "넣자"). 시험하던 PC 는 ISLC 가 이미 0.5ms 로 고정해 효과를 측정하지 못했다. 설정 세대 1(2.8.1 사용자)·TimerHalfMs=false 로 시작한 AutoTest 빌드에서 `[저사양] 타이머 0.5ms 요청 켬`·`nt_owned=True`, `set TimerHalfMs false` 로 `요청 해제`·`nt_owned=False`, 다시 켬 확인(이 PC 는 ISLC 가 이미 0.5ms 라 실제 해상도 변화는 측정 못 함: current 4959x100ns) | `7265d74`(원본 `7a257a5`) |

시험 전용(Player ZIP 에는 안 들어감): `AudioTap.cs`(`audiotap begin/end`, 새 AudioListener 로 게임 소리 출력을 녹음), `tools/analysis/tap_analyze.py`, 결과 `tools/ReleaseCheck/out/hitsound-tick-2.8.2.json`. 한계: 모드 안 녹음 분석이라 귀로 비교한 것은 아니며, 이음매에 우연히 히트음 시작이 겹치면 값이 커질 수 있다.

## 넣지 않은 것과 이유

- **JitInline float32**(`1fee535`, 외부에서 AI로 찾아 준 KreScent 보고서), **WaitSkip 해상도 변경 오류**(`e8e6726`), **RunLod 검은 대각선**(`20a6422`): 3.0.0 계열에만 있던 기능의 버그다. 2.8.1 소스에는 `JitInline`·`WaitSkip`·`RunLod` 코드가 없음을 확인했다(해당 없음).
- **GPU 장식(GpuDeco)·직통 렌더러·화면 효과·프레임 늘리기 등**: 새 기능이라 3.0.0 에서.
- `eb32901`(편집 화면 자동 GC 막기) 등 3.0.0 전용 동작 변경, `ae4fc86` 의 미검증 초안 나머지.
- eLadia GcPause 효과 차이: 원인 확정 — 위 #8(화면 밖 파티클 멈추기)이며 이번에 고쳤다.

## 릴리스 순서 (사용자가 "올려"라고 한 뒤)

`docs/release.md` 따라: (선택) 테스터 ZIP `./pack.sh test 1` → `release/2.8.2` 를 main 에 fast-forward(현재 main = v2.8.1 `d6fb971` 의 자손) → 태그 `v2.8.2` → GitHub 릴리스에 `dist/StutterFix-2.8.2-player.zip`·`developer.zip` → 디스코드 알림(`docs/announcement-2.8.2.md`). `dist/` 는 `pack.sh` 가 만든 현재 ZIP 이다(sha256 `d60a8efc…`).
