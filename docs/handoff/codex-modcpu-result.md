MODCPU=FAIL fps_before=168.003 fps_after=NA gain_pct=NA reason=CANDIDATE_NOT_RUN

# 2회차 — 기준선 확정과 TightLate 목록 캐시 후보 (2026-10-25)

`codex-al-mc-2.md`의 최신 `mc-autoloop-run1-evidence.txt`를 우선했다. 요약의 원문 38줄이 현재 원시 `E:/StutterFixWork/night-results/al-mc-1.log`와 일치한다. 첫 회전은 실패 후보가 아니라 코드 변경 없는 기준선이었다. 아래 `BASELINE_NOT_RUN` 기록은 과거 준비 시점이다. 렌더 병합은 재시도하지 않았다.

## 확정된 기준선

| 항목 | 창 1 | 창 2 |
|---|---|---|
| 원시 micro_end 행 | 415 | 484 |
| 실제 곡 구간 | 260.062335→270.014330초 | 260.048297→270.010516초 |
| 경과 / 프레임 | 9.945318초 / 1670 | 9.959054초 / 1674 |
| real_fps | 167.918 | 168.088 |
| GPU 소유 / native 오류 | 23547 / 0 | 23547 / 0 |
| phasewindow (417/486행) | 1672프레임, 합 5.95ms | 1676프레임, 합 5.95ms |
| FinishFrameRendering / Update / LateUpdate | 2.22 / 1.66 / 1.28ms | 2.22 / 1.68 / 1.28ms |
| onupdate (419/488행) | 1674프레임, 0.055ms | 1678프레임, 0.056ms |
| fuse (421/490행): 장식 관리자 / 효과 시작 / 애니메이션 | 0.51 / 0.22 / 0.52ms | 0.51 / 0.22 / 0.53ms |
| live off 복원 (425/494행) | originals_restored=True, 10854/10854 | originals_restored=True, 10854/10854 |

화면 3440×1440@165Hz는 원시 334·362·387·456행, 소유 23547·visible renderer 1880은 404·474행에서 확인했다. FPS 산술평균은 **168.003**, 평균 FPS의 역수는 **5.952275ms**다. 기존 목표의 +10%와 183 FPS를 모두 적용하면 이번 수용 하한은 **184.8033 FPS(5.411159ms)**다. 약 **0.541116ms** 감소가 필요하며 이는 후보의 예상 이득이 아니다. 2560 환경이나 과거 render-merge 결과와 합치지 않는다. fuse/phasewindow 계측을 포함한 두 창이며 진단 없는 일반 플레이·전곡·물리 지연 근거가 아니다.

## 원인과 첫 후보

`TightLate.List`는 이미 GPU 소유인 장식도 매 프레임 읽고 Entry 슬롯을 맞춘 다음 `Process → SkipGpuLate → HashSet.Contains`로 건너뛰었다. `Dormancy.Prepare`도 dormant가 하나라도 있으면 변경이 없어도 awake 전체를 다시 쓰며 `List._version`을 증가시켰다. 기존 구조에서는 목록 버전을 키로 하는 필터 캐시가 매 프레임 무효화된다. 두 부분을 같은 **후보 (1)** 범위에서 수정했다.

- 연구 옵션 `gpudeco latecache on/off`, 기본 false, 일반 Player 제외. A는 원래 순회, B는 CPU 처리 대상 배열 재사용이다. GPU 평가·복사 제출과 게임 DOTween/scrVfxPlus는 유지했다.
- B에서 GPU 활성일 때만 새 Sleep 이벤트가 있는 경우 dormant 제거 순회를 수행한다. Wake 추가·원본 목록 교체/버전/개수 변경·감사 검사는 기존 경로대로 반영된다. A 및 일반 Player의 순회 조건은 기존과 같다.
- `GpuLateListCache`는 awake 참조·실제 `_version`·개수·소유권 세대를 키로 한다. CPU fallback/null/중복의 원래 순서를 보존한다. 제거한 소유 장식의 기존 `Entry.Has`는 false로 하여 복원 첫 프레임에 값 쓰기가 생략되지 않게 했다.
- 소유권 재분류·activation rollback·live off·Dispose·Dormancy reset·TightLate Forget에서 캐시 참조를 지운다. `_version`을 못 읽거나 추적이 불가능하면 원래 순회를 쓴다. B 측정의 validate는 캐시가 준비되지 않은 fallback을 거부한다.
- `ManagerEnd/GpuLiveFrame`는 매 프레임 그대로 실행한다. 효과 진행과 카메라가 움직이므로 이 부분을 변경 이벤트에만 묶으면 화면 의미가 바뀐다. CPU 나머지 장식도 매 프레임 기존 Process를 거친다.

게임 `scrDecorationManager.LateUpdate` 근사 디컴파일을 읽고 설치 DLL을 ILScan으로 교차 확인했다(`tools/optimization/out/modcpu-manager-il.txt` 81~97행). 타일링·마스크 준비 → allDecorations의 LogicUpdate → `CheckUpdateFrameRateScreen` 순서다. 기존 allDecorations 대체 지점만 유지했고 앞뒤 동작을 생략하지 않았다. 독립 IL 읽기이며 게임을 실행하지 않았다.

## 준비한 시험과 검증

`tools/optimization/modcpu-steps.py` 기본 출력은 **118단계 A/B/B/A**, tag `modcpu-260-270-abba-latecache1`이다. 기존 `--chart`·`--output` 인자를 유지하며 `--mode baseline`은 보존된 최초 66단계 JSON과 객체 전체가 같다. 곡 처음 준비 → 각 창 seek 260 → 256초 compiled/live on → 260~270초 측정 순서를 유지했다. 각 창 시작/끝 `latecache validate`는 소유 23547·3440×1440@165를 검사하고, micro 로그에 옵션·캐시 준비·CPU/제외 대상 수·재구축 수를 남긴다. 후보 간격 안에 다른 최적화를 켜지 않는다.

생성만 한 계획: `tools/optimization/out/modcpu-latecache1-steps.json`. 자동 장치는 호출하지 않았다. 다음 외부 결과에서 A0/B1/B2/A3 개별 수치·환경·소유·계측 요약을 장부에 남기고 A 평균과 B 평균을 비교해야 한다. 속도 수용 조건은 `B평균 >= max(183, 168.003*1.10, A평균*1.10)`이다. 이 계획에는 픽셀 캡처·retry/reopen/Unload 검증이 없으므로 FPS만으로 `MODCPU=PASS`를 내지 않는다. 기존 G1/G2 기준과 새 픽셀 불일치 0, 복원·끊김 근거도 필요하다.

오프라인: DEV/Player/PlayerAuto 빌드 오류 **0/0/0**(경고 **46/36/39**), 실제 Dormancy 소스+캐시 수명/변경 검사 **87개**, 생성기 검사 **4개** 통과. 기준선 원문 38줄 대조·기존 JSON 호환성·diff check 확인. 검사는 managed adapter이며 실제 Unity 픽셀·프레임 비용·수명 증명이 아니다. 빌드/검사 로그와 원시 발췌는 `tools/optimization/out/modcpu-*`에 둔다.

최종 DLL의 ILScan 메타데이터에서 캐시 타입이 DEV/PlayerAuto에만 있고 Player에는 없음을 확인했다(`modcpu-metadata-check.txt`). 전역 `check-ui-encoding.py`는 기존 `out/image-matrix-candidate/scrDecoration.cs`의 UTF-16 BOM을 UTF-8로 읽다가 중단됐다. 해당 기존 파일과 검사기는 수정하지 않았다. 이번 수정/추가 C# **9개**의 UTF-8 해독·동일 SFENC001 문자열 검사는 별도로 통과했다. 전역 검사 전체가 통과했다고 주장하지 않는다.

후보 FPS·개선율·픽셀·실게임 안정성은 **NA**다. 설계 한계가 확인된 상태가 아니므로 중단 사유를 한계로 꾸미지 않는다. 이번 게임 실행·설치·커밋·푸시·자동 장치 호출·제어 파일 및 설정 변경은 **0회**다. 다음 단계는 이 새 후보의 외부 결과 분석이며 같은 기준선 코드를 다시 돌리지 않는다.

---

# 모드 managed CPU 최적화 — 첫 회전 기준선 준비 (2026-10-25)

`codex-modcpu-20261025.md`, 마스터 인계와 성능 분해의 T2 결과를 읽었다. 지시의 **“첫 회전에서 코드 수정 없이 한 번”**을 적용한다. 이번에는 런타임·생성기·설정을 수정하지 않고 기준선 계획만 생성했다. FAIL은 목표 미입증이며 성능 저하를 측정했다는 뜻이 아니다.

## 생성한 기준선 계획

- 기준 소스: `codex/night-gpu`, HEAD `4fd3a01`. 기존 수정 문서 5개는 보존한다.
- 생성기: `tools/optimization/modcpu-steps.py` 원본 그대로.
- 산출물: `tools/optimization/out/modcpu-baseline-steps.json` (로컬 산출물로 Git 제외).
- tag: `modcpu-260-270`, 명령 66개, `timeout_min=25`.
- SHA256: `d27253a3538822ac2bff29240e1507490a388d540ad76624ab217c0fb4f84165`.
- Arche 경로는 기존 map5 계획의 `open` 인자에서 읽고 파일 존재만 확인했다. 게임이나 맵을 실행하지 않았다.
- 곡 처음에서 예약·굽기·song scope 소유 준비 후 정지하고, 각 창은 `seek 260 12` → `decobake ready` → 256초 대기 → compiled packets → live on → 260초 census → micro/onupdate/fuse/phasewindow → 270초 종료 순서다. 동일 10초 창을 두 번 측정한다. ABBA는 후보 구현 이후 단계다.

## 자동 장치의 결과 수용 기준

1. 같은 게임·설정의 **3440×1440@165Hz**(164.999 표기는 허용) 증거를 보존한다. 2560×1440@120Hz 결과와 섞지 않는다. 지정 생성기 자체에는 환경·소유 수를 강제하는 게이트가 없으므로 실행 결과에서 별도로 확인해야 한다.
2. 두 창 모두 `gpudeco live census`의 소유 수가 **23547**이어야 한다. 작은 모집단, 환경 불일치, 준비 실패 또는 결과 누락은 기준선으로 채택하지 않는다.
3. 두 `gpudeco micro_end ... real_fps=` 원문과 `onupdate`·`fuse`·`phasewindow` 요약, 로그 경로·행을 장부에 보존한다. 계획의 대기 명령만으로 실제 260~270초 측정 완료를 주장하지 않는다.
4. 환경과 모집단을 통과한 기준선 두 FPS의 산술평균을 `fps_before`로 기록한다. 과거 render-merge A 평균 166.846331 FPS는 참고값이며 이번 기준선을 대신하지 않는다.
5. 기준선 확인 후 후보 **(1) TightLate 목록/처리/ManagerEnd 순회 줄이기**부터 하나씩 구현한다. CPU fallback·소유 변경·복원·맵 전환을 보존하고, 같은 환경에서 A/B/B/A로 확인한다. 이후 후보 (2) copy 제출, (3) 소유 효과 조기 종료, (4) 조회 횟수 감소 순서를 따른다.
6. 최종 통과에는 기준선 대비 **+10% 이상과 183 FPS 이상을 모두** 요구한다. 즉 `fps_after >= max(183, fps_before * 1.10)`이다. 화면 결과는 기존 G1/G2 기준을 유지하고 새 픽셀 불일치가 0이어야 한다. 이 두 창 계획에는 새 픽셀 비교 단계가 없으므로 FPS 로그만으로 화면 동일성까지 통과 처리하지 않는다.

렌더 병합은 재시도하지 않는다. 게임 자체 DOTween/scrVfxPlus 비용은 수정하지 않는다. 로그에 별도 `MODCPU=PASS` 판정 코드를 추가하지 않는다.

## 이번 검증과 남은 일

오프라인에서 원본 생성기를 실행해 JSON을 생성하고, 초기 준비 순서·두 창의 전체 명령·측정 시작/종료 짝·설정·tag·명령 수를 대조했다. 런타임과 생성기의 Git 차이는 0이다. 런타임 수정이 없어 빌드는 반복하지 않았다.

게임 실행·설치·커밋·푸시·자동 장치 호출·제어 파일 변경은 모두 0회다. 새 modcpu 원시 로그는 아직 없고 실제 FPS·픽셀·끊김·복원은 미측정이다. **다음 단계는 자동 장치의 코드 변경 없는 기준선 결과를 확인하는 것**이다. 그 전에는 후보 코드나 성능 개선 수치를 확정하지 않는다.
