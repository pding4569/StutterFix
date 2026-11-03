1. 앞선 조사·측정을 이어 받아 실패한 CPU 생략·조회 재사용·장벽 알림 실험을 삭제했다.
2. Player와 PlayerAuto 빌드는 오류 0, GpuDecoPlayerCheck 455개 검사는 통과했다(1차 정리 기준).
3. GpuDeco 기본값 0과 기존 제품 렌더링·복귀 검사를 유지한다.
4. 이번 작업의 게임 실행·전곡·픽셀 검증은 0회이며, 대기열 검증에 맡긴다.
5. 푸시·릴리스하지 않았고 잔여 픽셀 차이는 해결됐다고 주장하지 않는다.

2026-11-03, `E:\StutterFixWork\wt-partial`, `claude/gpudeco-cheap`.
시작 커밋 `932545e`. 지침: `E:\StutterFixWork\codex-clean-prompt.md`.
기존 simplify-research/cpu-extreme/engine-extreme 문서를 먼저 확인했다.

## 최종 표

| 정리 | 호출 경로·근거 | 결과 |
|---|---|---|
| IdleFloorUpdate | 일반 Player 제외. DEV/AUTOTEST 설치·명령만 존재. cpu-extreme ABBA -0.023404ms로 판 간 변동 0.088744ms보다 작음 | 파일·설치·명령·컴파일 항목 삭제 |
| HoldMaterialReads | AUTOTEST에서만 명령으로 후크 설치. ABBA -0.002616ms로 조건 내 변동보다 작음 | 파일·명령·컴파일 항목 삭제 |
| NotifyBarrierWatch | Player에서 false, AUTOTEST 명령만 활성화. 관리 경과 0.077812→0.139485ms로 악화 | 후크·인덱스·pending·oracle·플래그 삭제. 기존 8프레임 장벽 검사 유지 |

현재 켜진 기능은 기존 제품 설정을 따른다. GpuDeco는 기본 0이며 새 성능 기능을 켜지 않았다.
GpuDeco를 명시적으로 켰을 때 Flash CPU 라이브 이징, 기존 장벽 검사·독립 렌더러 복귀는 유지한다.
측정 도구 phasewindow/gpuprobe/profsamp/framecmp/gpudeco cost는 보존한다.

## 검증과 한계

1차 정리: Player 오류 0/경고 49, PlayerAuto 오류 0/경고 47,
GpuDecoPlayerCheck 455개/오류 0. 삭제된 CPU 실험이 두 DLL 모두에 없는지 검사하도록 갱신했다.
빌드 로그와 검사 출력은 [evidence/night-final](evidence/night-final)에 보존한다.
빌드·메타데이터 검사는 화면 동등성이나 성능 향상 증명이 아니다.

## 사용자 결정 항목

- `GpuDeco` 기본값 변경 여부: 현재 0 유지.
- 릴리스 여부: 현재 로컬 정리만 수행, 푸시·릴리스 없음.
- 남은 픽셀 차이 처리: 기존 Arche 262초 최대 RGBA 4,4,4,0 및 threshold 3 초과 8픽셀을 미해결로 유지.
  이번 정리는 새 픽셀 결과를 만들지 않았으며 대기열의 별도 결과로 판단해야 한다.
