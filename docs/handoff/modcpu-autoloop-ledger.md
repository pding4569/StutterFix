# modcpu 자동 시험 장부

## 3회차 — 외부 latecache1 탈락 / 단일 제출 후보 준비 (2026-10-25)

지정 run2 증거 76줄 중 75줄은 원시 `E:/StutterFixWork/night-results/al-mc-2.log`와 완전히 같고, 첫 시작 줄은 157행의 앞 1500자다. 최초 기준선 평균 168.003 FPS는 보존하며 이번 ABBA와 구분한다.

| 항목 | 값 |
|---|---|
| 화면 / 소유 / native 오류 | 3440×1440@164.999 / 네 창 모두 23547 / 모두 0 |
| A0/B1/B2/A3 FPS (micro_end 행) | 167.994 (419) / 169.166 (494) / 168.853 (569) / 168.609 (643) |
| A/B 평균 FPS / 증가율 | 168.301500 / 169.009500 / +0.420674% |
| 최초 168.003 대비 B 증가율 / A 양끝 차이 | +0.599096% / +0.366085% |
| 이번 수용 하한 / 판정 | 185.131650 FPS / latecache1 목표 미달 |
| phasewindow 합 ms (행) | 5.95 (421) / 5.91 (496) / 5.92 (571) / 5.93 (645) |
| onupdate ms (행) | 0.056 (423) / 0.056 (498) / 0.055 (573) / 0.056 (647) |
| fuse 관리자/효과시작/애니메이션 ms (행) | 0.49/0.23/0.51 (425), 0.48/0.23/0.51 (500), 0.48/0.23/0.54 (575), 0.49/0.23/0.53 (649) |
| B 캐시 재구축 / 끝 CPU 대상 / GPU 제외 | 1074·1077회 / 각각 395 / 각각 37 |
| 복원 (433/508/583/657행) | 네 창 모두 originals_restored=True, 10854/10854, unobserved_order=0 |
| 직전 픽셀 / 끊김 임계치 / 전체 수명 안정성 | NA / NA / NA |
| 새 후보 | (2) 평가→정규화→native 복사를 한 ExecuteCommandBuffer로 제출 |
| 새 옵션 / 기본값 / 일반 Player | submitsingle / false / 제외 |
| 새 계획 / tag / 단계 수 | tools/optimization/out/modcpu-submitsingle2-steps.json / modcpu-260-270-abba-submitsingle2 / 119 |
| 새 후보 FPS / 픽셀 / 안정성 | NA / NA / NA |
| 오프라인 검사 | 실제 제출·기록 코드+managed adapter 35개, 생성기 5개 통과 |
| 빌드 DEV/Player/PlayerAuto 오류 / 경고 | 0/0/0 / 46/4/39 (Player 증분) |

기존 목록 캐시는 계속 바뀌는 CPU 목록에서 약 64% 프레임마다 재구축되며 GPU 소유 23547개 중 awake에서 제외한 수는 37개였다. 변경 처리 생략은 화면 의미를 바꾸므로 같은 캐시 방식을 재시험하지 않는다. 이 결과는 모든 managed 최적화의 설계 한계가 아니다.

기존 복사 CommandBuffer는 이미 준비 때 캐시된다. 새 방식은 평가 즉시 Dispatch와 복사 제출을 합치는 **다른 코드**다. 명령 객체는 재사용하지만 동적 카메라·시각을 위해 매 프레임 재기록하며 이 비용으로 더 느릴 수 있다. 기본 off, CPU 처리·소스 진행·dirty 업로드·기존 fallback 유지. 각 창 양끝에서 환경·소유·실제 후보 실행·fallback 0·copy 오류 0을 검증한다.

다음 수용식은 `B평균 >= max(183,168.003*1.10,새 A평균*1.10)`이다. 계획에 픽셀/수명 전체 시험이 없으므로 속도만으로 PASS가 아니다. 최초 66단계/기존 latecache1 118단계 계획의 객체 전체 호환성은 확인했으며 반복 시험 요청은 아니다. 게임 실행·설치·커밋·푸시·자동 장치 호출·설정/제어 파일 변경 0회. 상세와 정확한 실행 구간은 `codex-modcpu-result.md`를 따른다.

## 2회차 — 외부 기준선 확인 / 후보 1 준비 (2026-10-25)

원문 `mc-autoloop-run1-evidence.txt`의 38줄을 원시 `E:/StutterFixWork/night-results/al-mc-1.log`와 대조했다. 첫 회전의 준비 상태와 실제 외부 기준선 완료를 구분한다.

| 항목 | 값 |
|---|---|
| 화면 / 소유 | 3440×1440@165Hz / 두 창 모두 23547 |
| micro_end 원시 행 | 415 / 484 |
| 기준선 창 1 / 창 2 / 평균 FPS | 167.918 / 168.088 / 168.003 |
| 경과초 / 프레임 | 9.945318 / 1670, 9.959054 / 1674 |
| onupdate 원시 행 / 평균 ms | 419 / 0.055, 488 / 0.056 |
| fuse 원시 행 / 관리자·효과시작·애니메이션 ms | 421 / 0.51·0.22·0.52, 490 / 0.51·0.22·0.53 |
| phasewindow 원시 행 / 합 ms | 417 / 5.95, 486 / 5.95 |
| native 오류 / CPU 복원 | 0·0 / 두 창 originals_restored=True, 10854/10854 |
| 이번 후보 | (1) TightLate GPU 소유 제외 배열 재사용 + 변경 때 Dormancy 압축 |
| 기본값 / 일반 Player | false / 제외 |
| 후보 계획 / tag / 단계 수 | tools/optimization/out/modcpu-latecache1-steps.json / modcpu-260-270-abba-latecache1 / 118 |
| 비교 | 같은 DLL, latecache off/on/on/off, 각 260~270초 |
| fps_after / gain_pct / 픽셀 / 안정성 | NA / NA / NA / NA |
| 기준선 +10% 및 183 이상 수용 하한 | 184.8033 FPS |
| 이번 판정 | MODCPU=FAIL fps_before=168.003 fps_after=NA gain_pct=NA reason=CANDIDATE_NOT_RUN |
| 오프라인 검증 | 빌드 DEV/Player/PlayerAuto 오류 0/0/0, 경고 46/36/39; Dormancy+캐시 87개, 생성기 4개 통과 |

캐시 키는 awake 참조/실제 List 버전/개수/소유 세대이며, 변경 없는 프레임만 재사용한다. CPU fallback 순서·원래 ManagerEnd/GpuLiveFrame·전환 복원을 유지했다. 캐시가 준비되지 않은 B, 소유 부족 및 화면 불일치는 각 창 양끝에서 거부한다. 기존 기준선 JSON과 `--mode baseline` 출력의 객체 전체 동일성도 확인했다. 근거·구현 상세는 `codex-modcpu-result.md`, 원문 발췌·IL·빌드 로그는 `tools/optimization/out/modcpu-*`다.

속도 수용에는 `B평균 >= max(183, 168.003*1.10, A평균*1.10)`를 적용한다. 메타데이터로 일반 Player 제외를 확인했다. 전역 인코딩 검사는 기존 UTF-16 `out/image-matrix-candidate/scrDecoration.cs`에서 중단됐으며, 수정/추가 C# 9개 별도 검사는 통과했다. 기존 산출물/검사기는 유지했다.

게임 실행·설치·커밋·푸시·자동 장치 호출·제어 파일/설정 변경 0회다. 후보 계획은 생성만 했고 실제 FPS·픽셀·끊김·retry/reopen/Unload는 미검증이다. 계획에 픽셀/안정성 단계가 없으므로 속도 개선만으로 PASS를 내지 않는다. 현재 한계 판정 근거는 없으며 같은 기준선을 다시 요청하지 않는다. 아래 첫 회전 수치는 과거 준비 시점이다.

## 첫 회전 — 기준선 준비 (2026-10-25)

지시: `codex-modcpu-20261025.md`. 첫 회전 코드 변경 금지에 따라 기존 GPU 경로 그대로 기준선을 준비했다. 자동 장치는 호출하지 않았다.

| 항목 | 값 |
|---|---|
| 소스 | codex/night-gpu / 4fd3a01 |
| 생성기 | tools/optimization/modcpu-steps.py, 수정 없음 |
| 계획 | tools/optimization/out/modcpu-baseline-steps.json |
| tag / 명령 수 | modcpu-260-270 / 66 |
| 요구 환경 | 3440×1440@165Hz |
| 요구 소유 | 두 창 모두 23547 |
| 기준선 창 1 / 창 2 FPS | NA / NA |
| fps_before / fps_after / gain_pct | NA / NA / NA |
| 실제 환경 / 소유 | NA / NA |
| onupdate / fuse / phasewindow | NA / NA / NA |
| 새 픽셀 불일치 / 안정성 | NA / NA |
| modcpu 원시 로그 | 없음 |
| 판정 | MODCPU=FAIL reason=BASELINE_NOT_RUN |

오프라인 계획 계약을 확인했다. 이번 새 코드·게임 실행·설치·커밋·푸시·자동 장치 호출·제어 파일 변경은 0회다. 기존 render-merge 장부와 문서는 유지한다. 실제 결과가 들어오면 원문·파일·행과 두 창 개별 FPS를 남기고 모집단/환경을 확인한 뒤 평균을 계산한다. 다음 후보는 (1) TightLate 순회 감소이며 기준선 수집 전에 적용하지 않는다. PASS에는 +10%와 183 FPS 이상, 기존 G1/G2 기준 유지 및 새 픽셀 불일치 0의 증거가 모두 필요하다.
