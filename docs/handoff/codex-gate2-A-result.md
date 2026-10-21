# Gate 2 A 자동 반복 5회차 — 재개 프레임의 오래된 재생 상태로 인한 소유 해제 수정 (2026-10-21, r11)

**재개 직후 `Dormancy-reset`으로 재생 시험이 중단되는 코드 경로를 수정하고 오프라인 검증을 완료했다. 새 실게임 측정은 없으며 `GATE2=PASS`는 미달성이다.** 시작 HEAD `02a3e2f`, 시작 작업 트리는 깨끗했다. 게임 실행·설치·커밋·푸시·`.codex/config.toml` 변경은 하지 않았다.

## 읽은 증거와 단계별 상태

지정 인계·장부·공통 지시(성능·긴급 포함)·기존 결과와 필수 배경 문서를 확인했다. 최신 전달은 수정 시각 기준 **run4(19:53)**다. 기존 전체 `E:/StutterFixWork/night-results/al-ga-4.log`와 게임 디컴파일은 읽기만 했다. 워크트리에 없는 로드맵·freeze 결과는 정식 checkout/`wt-freeze`에서 읽었다. [원본 SHA256·행 번호·집계](gate2-A-r11-prior-evidence.txt)를 보존했다. 아래 표는 모두 **수정 전 run4**다.

| 기존 정지 시점 | live 이미지 / 소유 | 소유율 | 구간 | ab_out / maxdiff | aa2_out / stable |
|---|---:|---:|---:|---:|---|
| 262초 | 2,127 / 2,022 | 95.063470% | 1,246 | 427,364 / 6 | 0 / True |
| 270초 | 2,262 / 2,152 | 95.137047% | 1,439 | 133,259 / 5 | 0 / True |
| 276초 | 17 / 17 | 100% | 15 | 0 / 0 | 0 / True |

1. **전체 스캔 제거**: 기존 `scan_ms=0.000100~0.000900`이다. Dormancy 이벤트·정적 캐시·증분 큐·1초 감사 구조를 유지했다. 새 프레임당 전체 순회는 없다. 정상 재생에서는 `tracking || PlaybackContinuity`가 단락 평가되어 새 프레임 번호·컨트롤러 검사를 호출하지 않는다.
2. **소유 유지**: 262/270초 정지는 95% 이상이며 276초는 live 분모도 17개다. 켬 준비의 258.017840초는 1,294/1,349개(95.922906%, 857구간)였다. 이어 재개 직후 초기화로 0개가 된다. 이를 276초의 17개나 가시성 임계 문제와 혼동하지 않았다. 정지/재개 경계의 해제 원인을 아래와 같이 수정했다. 재생 중 전체 소유율 유지는 아직 미측정이다. 0.01px·히스토그램·제외 사유·OWNED_LOW 기준을 유지한다.
3. **비용·FPS**: 첫 끔만 **123.331064 FPS / 8.108257ms**, 1,719프레임·13.938094초로 완료됐다. 켬·켬·마지막 끔은 미완료이므로 이득률을 계산하지 않는다. Prof는 **993,464표본 / set_pct=68.721148% / valid=True**이며 Step 표본·PairXY/GroupStep 억제·OnComplete/후속 엔진 비용 제외 한계를 유지한다. 이번 수정은 재생 시작 차단을 고친 것이며 비용 개선 수치는 없다.
4. **픽셀**: 1~3의 재생 확인이 끝나지 않아 색/정점/UV·비교 제외 영역을 바꾸지 않았다. 기존 427,364/133,259개 차이는 PIXEL_NEAR가 아니다. 상위 구역은 262초 `(1344,416), (736,768), (992,896), (672,384), (1376,256)`, 270초 `(544,384), (992,352), (928,352), (544,416), (960,352)`다. 장식 ID·텍스처·알파·블렌드와 연결된 증거가 없어 특정 장식이나 소유 밖 파티클/행성/텍스트 탓으로 확정하지 않는다. 제외 추가·기준 완화는 없다.

| 기존 정지 steady 표본 | 프레임 수 | scan_ms | reconcile_ms (delta 포함) | vertex_ms | upload_ms |
|---|---:|---:|---:|---:|---:|
| 262초, 소유 2,022 | 7 | 0.000172 | 3.036400 | 0.569571 | 0.070329 |
| 270초, 소유 2,152 | 7 | 0.000214 | 3.167571 | 0.632514 | 0.079429 |
| 276초, 소유 17 | 7 | 0.000143 | 2.693200 | 0.020557 | 0.000800 |

표는 로그의 samples로 가중한 기존 평균이다. 정지 중에도 dirty·delta 비용이 남고 heap 증가도 있다. 정점≤0.5ms·업로드≤0.4ms 동시 목표, 무할당, 재생 FPS+15% 달성을 주장하지 않는다. 첫 262초 enable은 33,718.803700ms였고, 재생 켬 준비는 setup 12,883.520200ms(register 1,244.352700 / patch 11,636.744600), first_render 218.925300ms, transition 13,102.445500ms였다. 준비 비용을 줄였다는 주장은 없다.

## 원인과 수정

전체 로그 **930~950행**: 258초에서 paused/ready 준비 완료 → resume → **`lifecycle Dormancy-reset restore=1`** → waitsongpos **11,088.033ms** → fuse 곡 **269.7초** → Begin **269.857836초, active=False, ready=False, owned=0** → `GATE2=ERROR`다.

관리 코드의 실행 순서가 원인이다. `Main.OnUpdateBody`는 **GcControl.Tick → AutoTest.Tick** 순서다. GcControl은 실제 `controller.paused`를 읽어 `Hitch.Tick`의 캐시 `wasPlaying`을 갱신한다. 같은 Update의 후반에서 AutoTest가 FrameCompare.Resume으로 pause를 해제해도 `Hitch.Playing`은 그 프레임 동안 False다. `Dormancy.List`는 `Hitch.Playing || FrameCompare.Paused`가 둘 다 False라 ResetState를 호출하고, 활성 소유를 Stop한다. 렌더러의 RenderFrame에도 같은 오래된 조건이 있어 Dormancy만 고치면 카메라에서 다시 중단된다.

게임 디컴파일 `scrDecorationManager.LateUpdate` 173~195행은 장식 목록/LogicUpdate를 처리한다. [설치 DLL IL](gate2-A-r11-manager-il.log)에서도 이 호출과 `allDecorations` 읽기를 교차확인했다. `scrConductor`의 DSP 기준 갱신은 기존 r10 분석과 같다. ResetState → Stop → 동기 UnpatchAll 동안 오디오가 진행할 수 있어 시각 건너뜀과 일치하지만, **기존 로그에 Stop/UnpatchAll 단독 시간이 없어 11.088초 전부를 패치 해제 비용으로 확정하지 않는다.** 게임 메서드를 실행하지 않았다.

- FrameCompare에 **성공한 Resume의 해당 프레임에서만 유효한 `PlaybackContinuity`**를 추가했다. 같은 컨트롤러이고 실제 controller/audio/timeScale/DOTween이 재개된 상태일 때만 유지한다. 다음 프레임에는 만료되며 새 Hitch 상태를 따른다. 같은 프레임의 중복 Resume은 유효 상태를 지우지 않는다.
- Dormancy와 RenderFrame의 기존 pause 유지 조건에 동일한 연속성 판정을 사용한다. 정상 재생 경로는 단락 평가한다. `renderer_version=10`, resume 로그에 `continuity_frame`과 `tracking`을 남긴다. 프레임당 로그는 추가하지 않았다.
- 원래부터 정지된 controller/audio/scale, 교체된 controller, 복원 예외에는 유지가 성립하지 않는다. 프레임 번호만으로 긴 유예 시간을 주거나 소유가 활성이라는 이유로 실제 정지를 무시하지 않는다. scene generation/reset/manager 검사, ForgetLevel, 명시적 종료 복원은 유지했다. FrameCompare.Stop/Unload/OnDestroy에서는 재개 참조·프레임을 해제한다.
- **144단계 생성 JSON·258초 양모드 pause/ready 준비·262→276초 측정·최소 3초 워밍업·OWNED_LOW·픽셀0·FPS+15% 기준은 그대로다.** Gate 3로 진행하지 않았다.

## 검증과 한계

확장한 [DormancyCheck](../../tools/optimization/DormancyCheck/README.md)는 실제 Dormancy.cs에 더해 **생산 FrameCompare의 필드/Pause/Resume과 생산 RenderFrame 수명 조건을 그대로 추출**해 연결한다. 재개 전 Hitch=False → 같은 프레임 manager/camera → 다음 프레임 Hitch 갱신 순서를 검사한다. **41건 통과**: pause/wake/sleep, 재개 유지·만료·중복, 실제 정지, 새 scene 목록/맵 해제, 설정 차단, 컨트롤러 교체, 기존 pause/음소거/scale 정지, 복원 예외, scene reset/generation/manager 부재. [수정본](gate2-A-r11-dormancy-fixture.log).

**세 음성 대조 모두 컴파일 성공 후 의도한 실행 검사에서 exit 1**: [Dormancy만 기존 pause 조건](gate2-A-r11-negative-dormancy.log), [렌더러만 기존 수명 조건](gate2-A-r11-negative-renderer.log), [Resume 연속성 설정 제거](gate2-A-r11-negative-resume.log). 마지막에는 실제 소스를 다시 추출해 41건을 통과했다. Unity/Harmony/DSP/행성·파티클 복원은 managed adapter이므로 실게임 수명·FPS·픽셀 증명은 아니다.

- Release DEV / Player / PlayerAuto: 오류 **0/0/0**, 경고 **38/36/38**. LoadCheck **276/229/276곳**, 틀림 **0/0/0**. 동적 필드명 5곳은 실제 실행 확인 대상이다.
- 시작/정렬 **29**·소스계약 **5**, Gate2 창 **58**, 정책 **106,479**·gate **18**·edition **42**·게임 hook metadata **22** 통과. 일반 Player에는 실험 코드가 없다.
- 계층 **2,729**·바운드 **482**·스크립트 **15**·캐시 **40**·업로드 **32**, 델타 **5,918,737 / 6,000회 변경**·쓰기 통지 **133**, MatShare **25,401** 통과.
- FrameCompare **11/15/15/17/21/2**, 생성기 **24/25/144단계** 검사 통과. 로그는 `gate2-A-r11-*`, 최종 검사와 해시는 [검증 기록](gate2-A-r11-validation.txt)에 보존한다.
- 검사 도구 자동 추출을 MSBuild Exec에 연결하려던 첫 시도는 cmd에서 py를 찾지 못해 9009로 실패했다. 해당 연결을 제거하고 기존 fixture 방식대로 명시적 추출을 사용했다. 초기 음성 대조의 미처리 예외가 검사 프로세스를 남겨 후속 빌드가 파일 잠금으로 실패했다. 경로 확인 후 이 작업 소유 DormancyCheck 프로세스 하나만 종료하고, 최상위 예외 처리를 추가했다. 세 음성 대조와 수정본을 정상 재검증했으며 컴파일 실패를 회귀 검출로 세지 않았다. 게임 프로세스에는 접근하지 않았다.

**새 소유 시계열·비용·전환 시간·3시점 픽셀·ABBA FPS·실게임 복원은 미측정이다.** 단계 3의 재개 차단 수정 상태로 종료한다. 동일 코드 게임 시험을 실행하거나 요청하지 않았다. 작업 관찰 스킬 v3.5.0은 적용했고 사용자 범위 저장소는 읽기만 했다. 이번 검사 도구의 오류는 위에 원인·수정·재검증을 기록했으며 메모리·스킬 파일은 변경하지 않았다.

---

# Gate 2 A 자동 반복 4회차 — 긴 준비가 곡 시각을 넘기는 ABBA 시작 오류 수정 (2026-10-21, r10)

**재생 시험의 시작 차단 원인을 수정하고 오프라인 검증을 완료했다. 수정 후 실게임 측정은 없으며 GATE2=PASS는 미달성이다.** 시작 HEAD `4d53c77`, 작업 트리는 깨끗했다. 게임 실행·설치·커밋·푸시·`.codex/config.toml` 변경은 하지 않았다.

## 기존 증거와 단계별 상태

지정 인계·장부·공통 지시의 성능/긴급 절·기존 결과와 필수 배경을 읽었다. 수정 시각 기준 최신 전달은 **run3(19:28)**이며 run6(13:57)보다 늦다. 기존 전체 로그 `E:/StutterFixWork/night-results/al-ga-3.log`도 읽기만 했다. 워크트리에 없는 로드맵·freeze 결과는 정식 checkout/`wt-freeze`에서 확인했다. [원본 SHA256·행 번호·집계](gate2-A-r10-prior-evidence.txt)를 보존했다. 아래는 모두 **수정 전 run3** 수치다.

| 기존 시점 | live 이미지 / 소유 | 소유율 | 구간 | ab_out / maxdiff | aa2_out / stable |
|---|---:|---:|---:|---:|---|
| 262초 | 2,127 / 2,025 | 95.204513% | 1,244 | 431,703 / 6 | 0 / True |
| 270초 | 2,209 / 2,123 | 96.106836% | 1,375 | 124,277 / 5 | 0 / True |
| 276초 | 17 / 17 | 100% | 15 | 0 / 0 | 0 / True |

1. **전체 스캔**: 전달 cost 표본의 `scan_ms=0.000100~0.001100`이다. 기존 Dormancy 이벤트·캐시·델타 구조를 유지했고 프레임당 전체 스캔을 추가하지 않았다. 명시적인 Begin의 registry 157,254개 등록은 전환 비용이다.
2. **소유 일관성**: 정지 262/270초는 95% 이상이다. 276초는 live 분모 자체가 17개다. 첫 켬의 252초는 219/361, 오류 뒤 266.817828초는 643/835(2프레임 cost 표본)다. 긴 정지 뒤 갱신을 따라잡는 구간이며 정상 재생의 소유율 유지·누적 해제 오류 여부를 확정할 수 없다. 0.01px 임계·히스토그램·제외/해제 사유·3~5초 OWNED_LOW 조건을 유지했다. 262/270초 bounds-error-px는 63/44개, 기타 repeat-tile 38/41개와 keywords 각 1개다.
3. **비용·재생 시험**: 첫 끔만 **125.723528 FPS / 7.953961ms**, 1,749프레임·13.911477초로 완료됐다. 켬·켬·마지막 끔은 미완료이므로 이득률을 계산하지 않는다. 과거 −87%를 새 켬 FPS로 쓰지 않는다. Prof는 **1,002,697표본 / set_pct=69.326192% / valid=True**이며 Step 표본·PairXY/GroupStep 억제·OnComplete/후속 엔진 비용 제외 한계가 있다.
4. **픽셀**: 1~3의 재생 검증이 끝나지 않아 색/정점 수식이나 제외 영역을 바꾸지 않았다. 현재 431,703/124,277개 차이는 PIXEL_NEAR가 아니다. 상위 구역은 262초 `(1280,320), (896,352), (736,768), (1856,512), (1344,416)`, 270초 `(1056,320), (960,352), (1024,320), (1024,352), (992,352)`다. 장식 ID·텍스처·알파·블렌드와 연결된 증거가 없어 파티클/행성/텍스트 또는 특정 장식으로 원인을 확정하지 않는다. 비교 제외 추가는 하지 않았다.

| 기존 정지 steady 표본 | 프레임 수 | scan_ms | reconcile_ms (delta 포함) | vertex_ms | upload_ms |
|---|---:|---:|---:|---:|---:|
| 262초, 소유 2,025 | 7 | 0.000171 | 3.025286 | 0.592586 | 0.070386 |
| 270초, 소유 2,123 | 7 | 0.000214 | 3.015743 | 0.597672 | 0.078586 |
| 276초, 소유 17 | 7 | 0.000186 | 2.098871 | 0.017786 | 0.000800 |

표는 로그의 samples로 가중한 기존 표본 평균이다. 실제 켬 재생 FPS나 전체 프레임 비용이 아니다. 262초 첫 렌더에는 reconcile 346.1732ms·vertex 1.0148ms·upload 2.7908ms가 들었다. 정지 중에도 dirty·delta 비용이 남고, allocated_bytes=0과 별개로 heap 증가가 기록된다. **정점≤0.5ms·업로드≤0.4ms·GC 무할당·FPS+15% 달성을 주장하지 않는다.**

## 원인과 수정

전체 로그 **890~904행**에서 첫 켬 setup **14,519.818300ms**(register **1,248.790700**, patch **13,268.672200**), 첫 렌더 **80.511600ms**, enable_transition **14,600.329900ms**를 확인했다. first PreCull의 owned_ts는 아직 **252.005694초**지만 다음 waitsongpos는 **141.726ms** 만에 끝나고 fuse는 **266.7초**에서 시작한다. 이후 `invalid ABBA window start`로 중단한다. 기존 로그는 개별 실패 조건을 표시하지 않았지만, 시작 범위 262±0.5초를 넘긴 것은 직접 확인된다. 첫 소유 뒤 약 0.2초만 지났으므로 3초 워밍업도 충족하지 못한다. 다른 조건이 동시에 실패했을 가능성은 배제하지 않는다.

디컴파일 `scrConductor.Update` 800~812행은 AudioListener.pause와 DSP 시계를 읽고, 876행은 DSP 기준으로 songposition_minusi를 갱신한다. `AutoTest.Update`의 동기 Begin 동안 오디오가 진행하고 다음 프레임에 곡 시계가 따라잡는다. `waitsongpos`는 `>=` 검사라 이미 넘긴 262초를 복구하지 못한다. 설치 DLL의 해당 호출·필드를 [ilscan 기록](gate2-A-r10-conductor-il.log)으로 교차확인했다. 게임 메서드는 실행하지 않았다. 기존 정지 픽셀 시험이 13~14초 준비 뒤에도 같은 곡 시각을 유지한 것도 준비 중 pause를 사용하는 근거다.

- ABBA 네 구간 모두 `seek 262 12 → waitsongpos 258 40 → pause → livedeco on/off → livedeco2 ready on/off → resume → waitsongpos 262 40`로 준비한다. 긴 패치/등록 비용은 곡 시계가 멈춘 구간에 둔다. 끔에도 같은 pause/resume 준비를 적용한다. **FPS 측정은 재개 후 262→276초이며 측정 중 pause는 없다.**
- 새 ready 명령은 비교용 pause·모드 일치·258±0.5초·유한한 곡 시각을 확인한다. 켬은 선택한 카메라의 첫 PreCull 완료와 소유>0을 기다린다. 대기 중 프레임별 로그를 쓰지 않고 완료 시 song/ready/transition을 남긴다. renderer_version=**9**, 기존 첫-cull 정렬 보호는 유지한다.
- 258→262초를 실제 재생해 워밍업한다. Begin의 **최소 3초** 검사, 소유 800개 미만 조기 중단, 262/276초 범위·ABBA 순서·픽셀0·FPS+15% 기준을 유지한다. 준비 시간을 없애거나 줄였다고 주장하지 않는다.
- Begin 거절 시 index/mode/song/paused/active/ready/owned/warmup/transition 값을 남긴다. `LiveDecoGate2.Step`은 전체 `ex.ToString()`을 `[livedeco2] ERROR`로 남긴다. 렌더러의 기존 `[livedeco] ERROR` 전체 스택도 유지한다. 승인된 Begin만 window/mode/songStart에 반영해 잘못된 시작이 열린 창 상태를 남기지 않게 했다.
- 생성기는 **132→144단계**다. 기존 픽셀 3시점·필터 켬·중앙 20% 영역·복원 검사·ABBA 구간은 유지했다. OFF에서는 기존 패치 해제 경로를 사용한다.

## 오프라인 검증과 한계

새 [LiveDecoWindowCheck](../../tools/optimization/LiveDecoWindowCheck/README.md)는 **실제 LiveDecoGate2.cs·판정 정책을 연결**하고 긴 setup/first-cull, 시계·오디오·렌더러를 managed adapter로 재현한다. **58건** 통과: 준비 완료 대기·모드/시각/NaN/무한대·워밍업·실패 후 창 상태·전체 스택·OWNED_LOW, 실제 생성 JSON의 ABBA 네 구간 완료와 마지막 복원, 픽셀 증거 누락의 ERROR를 검사한다. 이전 HEAD 생성 JSON을 넣은 대조본은 **song=266.632330 / warmup_s=0.016 / ready=True**의 시작 거절로 **exit 1**, 수정 JSON은 **exit 0**이다. [대조](gate2-A-r10-window-negative.log), [수정본](gate2-A-r10-window-fixture.log). 실제 Unity/DSP 정지·콜백·수명·FPS의 증명은 아니다.

- Release DEV/Player/PlayerAuto: 오류 **0/0/0**, 경고 **38/4/38**. LoadCheck **276/229/276곳**, 틀림 **0/0/0**.
- 시작/정렬 **29**·소스 계약 **5**, Dormancy 수명 **20**, 계층 **2,729**·바운드 **482**·스크립트 **15**·정점 캐시 **40**·업로드 **32** 통과.
- 정책 **106,479**·gate **18**·edition **42**(일반 Player 제외)·게임 hook metadata **22**, 델타 **5,918,737 / 6,000회 변경**·쓰기 통지 **133**, MatShare **25,401** 통과.
- FrameCompare **11/15/15/17/21/2**, 생성기 **24/25/144단계** 검사 통과. 빌드·LoadCheck·회귀 로그는 `gate2-A-r10-*`, 최종 해시/검사는 [검증 기록](gate2-A-r10-validation.txt)에 보존한다.
- 첫 IL 조회는 추정한 Data 폴더 이름이 틀려 실패했다. csproj의 실제 GameManaged 경로로 고쳐 성공했고 실패 기록도 보존했다. 관측용 프로세스 목록 조회는 권한 거부였으나 해당 검사 프로세스는 도구 세션에서 정상 종료 0을 확인했다. 남은 차단 작업은 없다.

**새 소유 시계열·scan/reconcile/vertex/upload·준비 시간·3시점 픽셀·ABBA FPS·실게임 복원은 미측정이다.** 단계 3의 시작 차단을 고친 상태로 종료하며 단계 4나 Gate 3로 진행하지 않았다. 동일 코드 게임 시험을 반복 실행하거나 요청하지 않았다. 작업 관찰 스킬 v3.5.0은 적용했고 이번 발견은 구현/검증 범위여서 새 스킬 개선 관찰은 없다. 기존 사용자 범위 관찰은 읽기만 했으며 메모리·스킬 파일 변경은 없다.

---

# Gate 2 A 자동 반복 3회차 — 첫 소유를 카메라 PreCull로 이동 (2026-10-21, r9)

**재생 FPS 시험을 막은 카메라 실행 순서 오류를 수정하고 오프라인 검증을 완료했다. 수정 후 실게임 수치는 없으며 Gate2 통과는 미확인이다.** 시작 HEAD `12b62cc`, 시작 작업 트리는 깨끗했다. 게임 실행·설치·커밋·푸시·`.codex/config.toml` 변경은 하지 않았다. 기존 Dormancy 이벤트·캐시·델타 구조를 유지했다.

## 이번에 읽은 증거

지정 인계 → 장부 → 공통 지시(끝 성능·긴급 절 포함) → 기존 결과와 필수 배경 문서를 읽었다. 최신 전달은 **run2(19:05)**이며 run6(13:57)보다 늦다. 기존 전체 로그 `E:/StutterFixWork/night-results/al-ga-2.log`를 읽기만 했고, SHA256·행 번호·원인 호출 경로를 [r9 기존 증거](gate2-A-r9-prior-evidence.txt)에 보존했다. 워크트리에 없는 로드맵과 freeze 결과는 정식 checkout/`wt-freeze`에서 읽기만 했다.

아래 수치는 모두 **수정 전 run2**다. 새 코드 결과로 전용하지 않는다.

| 기존 시점 | live 이미지 / 소유 | 소유율 | 구간 | ab_out / maxdiff | aa2_out / stable |
|---|---:|---:|---:|---:|---|
| 262초 | 2,127 / 2,026 | 95.251528% | 1,234 | 439,553 / 6 | 0 / True |
| 270초 | 2,263 / 2,165 | 95.669465% | 1,428 | 130,006 / 5 | 0 / True |
| 276초 | 17 / 17 | 100% | 15 | 0 / 0 | 0 / True |

- Prof **974,326표본 / set_pct=69.655914% / valid=True**. 기존 Step 표본·PairXY/GroupStep 억제·OnComplete/후속 엔진 비용 제외 한계는 유지한다.
- 첫 끔 FPS만 완료: **124.133396 FPS / 8.055850ms**, 1,731프레임, 13.944676초. 켬·켬·마지막 끔 창은 미완료이므로 이득률은 계산할 수 없다. 과거 −87%를 이번 코드의 켬 FPS로 쓰지 않는다.
- 첫 켬은 곡 **252.005862초**, 소유 236개로 시작했다가 첫 PreCull에서 **`sort-context-changed camera_z=0->-10`**으로 복원·중단했다. 이후 `invalid ABBA window start`, 최종 `GATE2=ERROR / on=False`다. 스택은 SortSupported → RenderFrame → PreCull이다(전체 로그 880~897행).

## 1. 전체 스캔과 남은 비용

기존 run2의 `scan_ms`는 **0.000100~0.000900ms**로 큐 확인 수준이다. 첫 등록의 `dirty=157254`는 Begin에서 큐에 넣은 레지스트리를 첫 프레임에 처리한 수이며, 프레임당 전체 스캔을 다시 수행했다는 뜻은 아니다. 이번 수정도 전체 스캔·정적 재판정을 프레임 루프에 추가하지 않는다.

| 기존 262초 정지 표본 | scan_ms | reconcile_ms (delta 포함) | vertex_ms | upload_ms |
|---|---:|---:|---:|---:|
| 첫 프레임 1개 | 0.000900 | 1,682.110000 | 5.638800 | 4.825300 |
| 이후 정지 표본 9프레임의 로그별 평균 범위 | 0.000100~0.000467 | 2.690533~4.895575 | 0.586400~0.599567 | 0.068400~0.072100 |

첫 enable은 **39,243.351600ms**, 이후 14,632.802600 / 10,106.456600 / 9,897.177300ms였다. 초기 등록/색인/패치별 시간은 기존 로그에 없어 어느 단계가 대부분인지는 확정하지 않는다. 정지 프레임에도 dirty 984개와 delta 약 2.7ms가 남는다. scan 값만 작다고 전체 비용 목표나 FPS 이득을 달성한 것으로 적지 않는다. `allocated_bytes=0`에도 heap 증가가 있어 실제 Mono GC 무할당을 증명하지 못한다.

## 2. 소유 일관성·제외 기준

262·270초 정지 표본은 live 이미지의 **95% 이상**을 소유한다. 276초는 분모 자체가 17개여서 그 시점의 소유 17개를 누적 오제외로 해석하지 않는다. **262→276초 재생 중의 소유율 유지 증거는 없다.** FPS 시작 전 252초의 234/361 역시 다른 시점의 수치다.

0.01px 오차 정책을 유지했다. 기존 262초는 검사 2,088개 중 bounds-error-px 62개, 270초는 2,220개 중 55개를 제외했다. 두 시점의 여섯 구간 히스토그램은 각각 `1103,352,571,62,0,0` / `1197,383,585,55,0,0`이다. 기타 repeat-tile 38/42개, keywords 각 1개다. 임계나 가시성을 완화하지 않았다.

## 3. 재생 시작 오류 수정과 전환 계측

디컴파일 `scrCamera.Update` 376행은 **Vector2인 `pos + shake`를 localPosition에 대입**한다(Z=0). `LateUpdate` 399~402행은 `SetPosition(transform.position)`을 호출하며, 그 메서드 580~583행은 Z를 **−10**으로 고정한다. 설치 DLL의 IL에서 Vector2 변환·Transform 쓰기와 LateUpdate → SetPosition 호출을 교차확인했다. [카메라 IL](gate2-A-r9-camera-il.log). 게임 메서드를 실행하지 않았다.

기존 `Begin()`은 자동시험 Update 도중 동기 `RenderFrame()`으로 소유·정점 검증·정렬 키를 먼저 만들었다. 당시 Z=0을 기준으로 고정하고 같은 프레임의 정상 PreCull Z=−10을 문맥 변경으로 판정했다. 단순히 카메라 Z 검사를 지우면 이전 키와 새 델타 키에 서로 다른 거리 원점이 섞일 수 있으므로 실행 순서를 고쳤다.

- Begin은 목록 준비·등록·후킹 후 `starting` 상태에서 반환한다. 이때 **소유·숨김·정점 검증·정렬 키 생성은 없다.** `state`의 `ready=False`와 `activation pending=1`로 구별한다.
- 선택한 메인 카메라의 **첫 PreCull**에서만 최초 RenderFrame을 실행한다. 그때의 카메라 문맥으로 모든 초기 키·정점·메시를 만든다. 시작 완료 후에는 기존 Z/축/모드/마스크 변경 중단 조건을 유지한다. NaN/무한대 카메라 Z도 거절한다.
- 소유 0개·초기 실패는 기존 PreCull 전체 스택 기록·Gate2 오류·Stop 복원으로 처리한다. 대기 중 off/맵 해제/수명 종료도 기존 Stop을 통해 callback과 시작 상태를 해제한다. 실제 Unity 수명은 미측정이다.
- `EnabledSeconds`와 3초 워밍업은 최초 소유 렌더가 끝난 뒤부터 센다. setup/admission 지연을 워밍업으로 세거나 시작 프레임에서 OWNED_LOW를 판정하지 않는다. 이후 3~5초 소유 800개 미만 검사와 ABBA의 3초 검사 기준은 유지한다.
- renderer_version=**8**. `prepare_ms`(목록 준비·외부 훅 검사), `register_ms`(발견·색인), `patch_ms`, `first_render_ms`, `wait_first_cull_ms`를 남긴다. enable_transition_ms는 setup+첫 렌더 작업 시간이며 callback 대기 시간은 별도다. 초기 cost/owned_ts는 **phase=activation**으로 따로 배출해 steady 평균에서 뺀다.

아직 새 타이밍은 없다. **시작 39초나 paused delta 2.7ms를 줄였다고 주장하지 않는다.** 새 계측은 그 비용의 위치를 확인하기 위한 것이다. 정점≤0.5ms·업로드≤0.4ms·FPS+15%와 전체 켬/끔 비용 분해는 실게임 미검증이다.

## 4. 픽셀

단계 1~3의 재생 수치가 완성되지 않아 픽셀 수식·색 형식·비소유 렌더러·비교 제외 영역을 변경하지 않았다. 현재 픽셀 오차는 262초 439,553개/최대6, 270초 130,006개/최대5로 극소 차이 단계가 아니다.

상위 32×32 구역은 262초 `(896,352), (1664,576), (1536,224), (1536,608), (1312,512)`, 270초 `(544,416), (896,352), (1024,320), (1856,512), (544,384)`다. 뒤에 붙은 값은 채널 오차 합이다. 장식 ID·텍스처·알파·블렌드와 연결된 자료가 없어 소유 메시/파티클/행성/텍스트 원인 분류를 확정하지 않는다. 추가 비교 제외를 승인된 것으로 간주하지 않는다.

## 오프라인 검증·종료

새 [LiveDecoActivationCheck](../../tools/optimization/LiveDecoActivationCheck/README.md)는 실제 StartRendering/PreCull/SortSupported/EnabledSeconds를 수정 없이 추출하고 카메라·시계·렌더·복원을 managed adapter로 바꾼다. **시작/정렬 29건 + Begin/워밍업/정리 소스 계약 5건** 통과. 첫-cull 예외 조건의 `!starting`만 없앤 대조본은 첫 Z=0→−10 승인 검사에서 **exit 1**, 수정본은 통과했다. [대조](gate2-A-r9-activation-negative.log), [수정본](gate2-A-r9-activation-fixture.log), [소스 계약](gate2-A-r9-activation-extract.log). 실제 Unity callback 순서·렌더링·패치 설치·복원 성공을 증명하는 fixture는 아니다.

- Release DEV/Player/PlayerAuto 빌드 오류 **0/0/0**, 경고 **38/4/38**(증분 빌드 출력). LoadCheck **276/229/276곳**, 틀림 **0/0/0**.
- 소유·정렬 정책 **106,479**, gate **18**, edition metadata **42**(일반 Player 제외), 설치 게임 hook metadata **22** 통과.
- Dormancy 수명 **20**, 계층 **2,729**, 바운드 **482**, 스크립트 **15**, 정점 캐시 **40**, 업로드 **32** 통과.
- 생산 델타 **5,918,737건 / 6,000회 변경**, 쓰기 통지 **133**, MatShare **25,401** 통과.
- FrameCompare **11/15/15/17/21/2**, 생성기 **24/25/132단계** 검사 통과. 기존 steps는 변경하지 않았다.
- `gate2-A-r9-*`에 빌드·LoadCheck·fixture·기존 증거를 보존하고 [검증 기록](gate2-A-r9-validation.txt)에 최종 해시와 차이 검사를 기록했다.

최신 실패와 같은 코드를 다시 게임에서 실행하거나 동일 코드 시험을 요청하지 않았다. 새 코드의 소유율·비용·3시점 픽셀·FPS·실게임 수명은 미측정이며 **GATE2=PASS 미달성**으로 종료한다. 작업 관찰 스킬 v3.5.0을 확인했으며 이번 발견은 구현·검증 범위여서 새 스킬 개선 관찰은 기록하지 않았다. 메모리·스킬 파일 변경 없음.

---

# Gate 2 A 자동 반복 2회차 — 비교용 일시정지의 Dormancy 초기화 실패 수정 (2026-10-21, r8)

**최신 시험의 시작 차단 원인을 수정하고 오프라인 검증을 완료했다. 수정 후 게임 측정은 없으며 Gate2 통과는 미확인이다.** 게임 실행·설치·커밋·푸시·`.codex/config.toml` 수정은 하지 않았다. 시작 HEAD는 `31e44fe`, 작업 트리는 깨끗했다. 기존 r7의 이벤트 기반 델타·캐시·부분 구간 갱신을 유지했다.

## 최신 증거와 원인

인계 `codex-al-ga-2.md`, 공통 지시의 끝 절들, 장부, 기존 결과와 전달 증거를 읽었다. 파일 번호 대신 수정 시각을 확인했다. 최신은 **ga-autoloop-run1-evidence.txt**(18:44)이며 run6(13:57)은 과거다. 기존 전체 로그 `E:/StutterFixWork/night-results/al-ga-1.log`도 읽기만 했다.

- 기존 실측: Prof **979,110표본 / set_pct=69.121312% / valid=True**. 기존 Step 표본·PairXY/GroupStep 억제·OnComplete/후속 엔진 비용 제외 한계는 그대로다.
- 로그 364행에서 비교용 pause, 366행에서 상태가 일시정지→정지로 바뀌고, 435~438행에서 **Dormancy live tracking is not ready → GATE2=ERROR / autotest_aborted**가 기록된다.
- 소유 시작 전에 중단되었다. **새 소유 수·scan/reconcile/vertex/upload 비용·픽셀·FPS 수치는 없다.** 과거의 18개나 FPS −87%를 이 코드의 측정값으로 쓰지 않는다.

`FrameCompare.Pause()`는 Paused를 먼저 켠 뒤 controller.paused를 켠다. `GcControl.IsPlayingFast/Slow`는 paused를 재생 제외 조건으로 쓰고 그 결과를 `Hitch.Tick`에 전달한다. 그런데 기존 `Dormancy.List()`는 **!Hitch.Playing이면 ResetState()**를 호출해 source와 awake 목록을 지웠다. `Begin()`은 비교용 일시정지를 허용하면서도 source가 있어야 하는 LiveTrackingReady를 요구하므로 두 조건이 충돌했다. 로그의 Enabled/broken/LogicSkip 개별 값은 없으므로 다른 차단 조건까지 전부 실측했다고 주장하지 않는다.

게임 디컴파일 `scrDecorationManager.LateUpdate`의 장식 목록 순회·LogicUpdate 호출을 확인하고, 설치 DLL의 IL로 교차확인했다. scrController.paused setter의 _paused 저장도 디컴파일에서 확인했다. 게임 메서드는 실행하지 않았다. [원본 해시·행 번호·호출 경로](gate2-A-r8-prior-evidence.txt), [관리자 IL](gate2-A-r8-manager-il.log).

## 수정 범위

1. DEV/AutoTest의 비교용 **FrameCompare.Paused** 동안 Dormancy 목록과 깨움/잠듦 추적을 유지한다. 일반 정지·메뉴·설정 꺼짐·오류·맵 해제의 초기화 경로는 유지한다. 일반 Player에는 비교용 분기가 컴파일되지 않는다.
2. 명시적 `livedeco on`에서 원본을 숨기기 전에 `PrepareLiveTracking(manager)`로 현재 관리자 목록을 한 번 준비한다. seek 직후 첫 관리자 LateUpdate 전이나 차가운 일시정지 상태에서도 초기화할 수 있다. 초기화 비용은 enable_transition_ms/enable_allocated_bytes 안에 포함한다. **RenderFrame의 전체 스캔을 다시 넣지 않았다.**
3. 성공 시 `live_tracking ready=1 playing=... capture_paused=... awake=...`를 남기고 실패 시 Enabled/broken/LogicSkip/playing/capturePaused/source를 예외에 포함한다. 기존 Begin의 `[livedeco] ERROR` 전체 스택·복원 경로를 사용한다.

## 오프라인 검증

새 DormancyCheck는 **실제 Dormancy.cs**를 연결한다. managed adapter로 10프레임 일시정지 중 소유·목록 유지, 잠듦/깨움, 재개, 일반 정지, 첫 LateUpdate 전 초기화, 관리자 목록 교체, unload, 설정 꺼짐과 null source를 **20건** 검사했다. 이전 playing-only 조건만 복원한 대조본은 `capture pause preserves live owner and readiness`에서 실패(exit 1)하고 수정본은 20건 통과했다. Unity의 실제 Harmony 설치·프레임 비용·게임 수명 시험의 대체 증거는 아니다. [대조본](gate2-A-r8-dormancy-negative.log), [수정본](gate2-A-r8-dormancy-fixture.log), [검사 설명](../../tools/optimization/DormancyCheck/README.md).

- Release DEV/Player/PlayerAuto 빌드 오류 **0/0/0**, 경고 **38/36/38**. LoadCheck **276/229/276곳**, 틀림 **0/0/0**.
- 소유·정렬 정책 **106,479건**, gate 정책 **18건**, edition metadata **42건**(새 준비 메서드의 일반 Player 제외 포함), 게임 hook metadata **22개** 통과.
- 계층 **2,729**, 바운드 **482**, 스크립트 **15**, 정점 캐시 **40**, 업로드 **32**, 생산 델타 **5,918,737**, 쓰기 통지 **133건** 통과.
- FrameCompare **11/15/15/17/21/2**, 기존 **132단계** 생성기 검사 통과. steps·픽셀 수식·비교 제외 영역·성공 기준은 변경하지 않았다.
- 빌드/LoadCheck/fixture 로그는 `gate2-A-r8-*`, 최종 해시·차이 검사는 [검증 기록](gate2-A-r8-validation.txt)에 보존했다.

지시의 1→2→3→4 순서를 유지한다. 이번 증거는 단계 1의 소유 시작 전 차단 오류여서 이를 수정했다. 기존 구현의 **소유 90%·scan≤0.05ms·정점≤0.5ms·업로드≤0.4ms·FPS+15%·3시점 픽셀0·실게임 복원**은 신규 측정 없이 통과로 적지 않는다. 픽셀 단계는 1~3의 새 수치가 없어 보류했다. 동일 코드 게임 재시험을 실행하거나 요청하지 않았다.

---

# Gate 2 A 7회차 — Dormancy 이벤트·정적 캐시·부분 구간 갱신 (2026-10-21)

**소스 수정과 오프라인 검증 완료. 신규 게임 측정이 없으므로 Gate 2 통과·소유 90%·scan ≤0.05ms·정점 ≤0.5ms·업로드 ≤0.4ms·FPS +15%는 미확인이다.** 게임 실행·설치·커밋·푸시·`.codex/config.toml` 변경을 하지 않았다. 시작 시 이미 수정된 `Dormancy.cs`의 `LiveTrackingReady`, Changed/Rebuilt/Audit/Reset 통지를 보존하고 연결했다. 아래 과거 결과도 보존했다.

공통 인계·r7 지시·장부·직전 결과·최신 run6 전달 증거를 읽었다. 전달 파일이 후반 발췌라 기존 `E:/StutterFixWork/night-results/al-ga-6.log`에서 세 픽셀/ABBA 결과를 보완했다. 워크트리에 없는 로드맵과 freeze 결과는 정식 checkout/`wt-freeze`에서 읽기만 했다. [기존 로그 SHA256와 판정](gate2-A-r7-prior-evidence.txt)은 새 실험 결과가 아니다.

## 1. 전체 스캔 제거

- 전체 scene 발견 `Register()`는 명시적인 Begin에서 한 번만 한다. RenderFrame의 Scan/레지스트리 재등록을 제거했다. 빈 프레임은 델타 큐 개수만 확인한다.
- Dormancy 깨움·잠듦의 논리 가시성 변화만 큐에 넣는다. 매 채널 ApplyColor마다 재판정하지 않는다. 숨김·재표시는 승인/거절 캐시를 유지하고 실제 스프라이트·재질·계층·스크립트 조건 변경은 해당 후보만 무효화한다.
- 비소유 변경도 추적하도록 게임/모드 어셈블리의 관리 쓰기 지점에 typed delegate 통지를 넣었다. Renderer 정렬/재질/활성/스프라이트, Material 속성, Transform 이동/회전/계층, GameObject 활성/레이어, 스크립트 활성, Instantiate/Destroy/AddComponent를 관측한다. Unity native setter 자체는 Harmony로 패치하지 않는다.
- 렌더러·재질·Transform 조상·종속 렌더러·자식 스크립트에서 source로 가는 색인을 유지한다. 생성/재부모화는 변경 하위 트리만 등록하고 재색인에서 오래된 연결을 제거한다. 실패는 전체 스택을 `[livedeco] ERROR`로 남기고 복원한다.
- 전체 정렬 목록/구간을 다시 훑는 Reconcile을 없앴다. 변경 행의 기존 구간과 인접 연결 구간만 재구성한다. 공유 정점 배열의 해제 영역을 병합·재사용하며 영향 없는 구간의 offset/내용은 유지한다.
- 소유 중 기존 Dormancy 감사는 1초 주기다. 놓친 깨움과 awake 가시성 누락을 세며 `audit_ms`는 dormant 감사와 awake 검사 시간을 포함한다. 카메라 정렬 문맥/마스크가 바뀌면 전체 재스캔 대신 소유를 복원하고 중단한다.

오프라인 수치: 생산 정렬/부분 재구성/정점 영역 관리 메서드를 그대로 추출해 독립적인 전체 분할 oracle과 **6,000회 변경 / 5,918,737건** 대조를 통과했다. 구간 병합/분할, 버퍼 중첩·내용 보존, 정적 프레임의 구간/offset 보존, 동률 해제 통지, 마지막 잠듦 후 전체 반납을 확인했다. [델타 fixture](gate2-A-r7-delta-fixture.log). 실제 프레임 시간의 대리 수치가 아니다.

## 2. 소유 일관성·픽셀 기준 오차

GetVisible의 논리 가시성과 원본을 숨기는 소유 비트를 분리했다. 소유자가 켠 forceRenderingOff는 잠듦으로 취급하지 않으며, 외부에서 숨긴 비소유 렌더러는 계속 제외한다. InvisibleSkip이 소유 중인 원본의 비트를 덮지 않도록 했다. **기존 Live도 소유자의 비트를 예외 처리했으므로, 이것만으로 기존 18개의 원인을 확정하지 않는다.**

owned_ts에 `awake`(현재 live 이미지 sprite 분모), owned/runs/dirty와 비용을 1초마다 남긴다. `dormancy_awake`는 mesh/기타를 포함하는 원래 목록 크기로 별도 기록한다. 해제는 destroyed/renderer-disabled/inactive-hierarchy/logical-invisible/camera-layer/경계 쓰기 등으로 집계한다. 실제 live 감소와 판정 손실을 구분하기 위한 로그이며, 곡 끝 소유 수를 강제로 늘리지 않았다. **새 시계열·90% 소유율·기존 18개 원인의 실증은 미측정이다.**

정점/바운드 오차에 실제 렌더 직교 카메라의 `max(width/(2*size*aspect), height/(2*size))`를 곱해 **엄격히 0.01px 미만**만 통과시킨다. 두 오차를 각각 검사하고 NaN/무한대/유효하지 않은 배율은 거절한다. 0.00027 월드 오차는 배율 36에서 0.00972px로 통과하고 배율 40에서 0.0108px로 제외된다. 실제 Arche 배율/분포로 주장하지 않는다. 히스토그램은 `<0.001 / <0.005 / <0.01 / <0.05 / ≥0.05px / nonfinite` 여섯 구간이며 제외 사유별 개수를 유지한다.

생산 쓰기 통지 **133건** 검사(인자 0~4개, generic/non-generic 생성·파괴·컴포넌트 추가, 반환값/레이블/예외 경계/원래 예외 보존)가 통과했다. 워밍업 후 typed wrapper 10,000회는 관리 adapter에서 추가 할당 0이었다. [쓰기 fixture](gate2-A-r7-changes-fixture.log). 설치 Harmony의 GetOriginalInstructions overload도 [IL metadata](gate2-A-r7-harmony-metadata.log)로 확인했다. **실제 Unity Mono 패치 설치 성공·콜백 비용·GC 무할당 증명이 아니다.**

## 3. 비용·조기 중단·수명

cost에 scan/reconcile/vertex/upload/awake/owned/dirty를 남긴다. 델타 비용은 reconcile_ms에 포함하고 delta_ms로도 구분한다. scan_ms는 큐 확인 비용이다. 초기 발견/색인/패치/메시 준비는 enable_transition_ms·enable_allocated_bytes·watched_objects에 남긴다. 감사는 audit_ms, callback 부하는 write_callbacks/watcher_visits 횟수로 기록한다. 작은 setter마다 Stopwatch를 넣지 않았다. **callback 자체와 게임/엔진을 포함한 정확한 켬/끔 차이 분해는 미측정이다.**

기존 ABBA 생성기는 약 252초에 켜고 262초부터 집계하므로 3초보다 긴 여유가 이미 있다. 132단계를 유지했고 실제 enabled 경과가 3초 미만이면 집계를 거절하며 경과/transition_ms를 기록한다. 자연 재생에서 켠 뒤 **3~5초 사이 owned<800**이면 GATE2=OWNED_LOW를 남기고 자동 시험을 즉시 종료·복원한다. 276초 정지 픽셀 표본에는 조기 중단을 적용하지 않는다.

OnLandOnPortal/FailAction/Restart/QuitToMainMenu/scnGame.OnDestroy 진입 복원과 기존 ResetScene·Dormancy reset/rebuild·맵 다시 열기·Main.Unload·root 파괴 경로를 연결했다. 구간·원본 숨김·Harmony 소유자·watcher/cache/queue/정점 배열을 반납한다. **실제 게임의 실패/재시작/종료 수명 시험은 미실행이다.**

## 4. 픽셀 단계

1~3의 신규 게임 수치를 얻을 수 없는 세션이므로 지시한 순서에 따라 픽셀 수식 수정과 제외 영역 변경은 진행하지 않았다. Float32 색/UV/flip/블렌드/기존 중앙 20% 마스크와 PASS 기준을 유지했다. 최신 기존 증거는 다음과 같다.

| 기존 run6 | 262초 | 270초 | 276초 |
|---|---:|---:|---:|
| ab_out | 376,530 | 124,332 | 0 |
| maxdiff | 6 | 5 | 0 |
| aa2_out / stable | 0 / True | 0 / True | 0 / True |

기존 ABBA FPS는 **off 122.358015 / on 15.168761 / on 15.062440 / off 123.147474**, 이득 **−87.686140%**, 최종 GATE2=PIXEL_FAIL이다. Prof는 **986,285표본 / set_pct=69.182709% / valid=True**이며 기존 Step 표본·PairXY/GroupStep 억제·OnComplete/후속 엔진 비용 제외 한계를 유지한다.

상위 32×32 구역은 262초 `(896,352), (1856,512), (864,352), (736,768), (1536,224)`, 270초 `(544,384), (960,352), (896,352), (960,320), (992,320)`다. 구역 뒤 로그 숫자는 채널 절대 차이 합이며 변경 픽셀 수가 아니다. 장식 ID/텍스처/알파/블렌드와 연결한 자료가 없어 소유 메시/파티클/행성/텍스트 중 어느 것이 원인인지 확정할 수 없다. **비소유 렌더러 변경이나 추가 비교 제외는 하지 않았다.**

## 검증과 종료

- Release DEV/Player/PlayerAuto 빌드 오류 **0/0/0**, LoadCheck **276/229/276곳 검사, 틀림 0/0/0**.
- 구간·anchor·화면 오차·소유 가시성 정책 **106,479건**, gate 정책 **18건**, edition metadata **39건**(일반 Player 제외), 게임 hook metadata **22개** 통과.
- 계층 **2,729**, 바운드 **482**, 스크립트 **15**, 정점 캐시 **40**, 업로드 **32**, MatShare **25,401**, FrameCompare **11/15/15/17/21/2**, 생성기 **25/132단계** 검사 통과.
- 생산 델타 **5,918,737건**, 쓰기 통지 **133건**, [소스 계약 감사·최종 해시](gate2-A-r7-validation.txt), git diff --check 통과.

정적 변경 감지는 두 관리 어셈블리의 관측 가능한 쓰기 범위다. 다른 어셈블리/네이티브·반사 호출·열린 generic 경로까지 포괄한다고 주장하지 않는다. 초기 등록/색인/패치 비용, 실제 callback 부하, 재생 중 구간 생성/할당, 실소유율, 픽셀 0·성능 목표는 새 실행 증거가 필요하다. 금지된 게임 시험을 실행하거나 동일 코드 반복 시험을 요청하지 않았다. **새 측정 없이 PASS로 승격하지 않고 종료한다.**

작업 관찰 스킬 v3.5.0을 확인했다. 이번 발견은 구현·검증 범위이며 새 스킬 개선 관찰 기록은 없다. 메모리/스킬 파일은 변경하지 않았다.

---

# Gate 2 A 6회차 분석 — 전체 장면 폴링 설계의 비용 확인 후 중단 (2026-10-21, 감사 run7)

**현재 구현의 전체 장면 폴링·반복 안전 검사 설계에서 중단한다. 실측 최종 판정은 `GATE2=PIXEL_FAIL`, FPS 이득 −87.875467%다. `GATE2=PASS`는 미달성이다.** 인계의 “안 되는 이유가 설계 한계면 결과 문서에 구체적으로 적고 멈춰라”를 적용했다. 이번에는 소스·빌드 산출물·steps를 변경하지 않았다. 스캔 제거와 성능 목표 달성은 완료하지 못했다. 동일 코드 게임 재시험을 실행하거나 요청하지 않는다.

`codex-al-ga-6.md` → 장부 → run5 전달 증거 → 기존 전체 로그 순으로 읽었다. 시작 HEAD는 `ad65eab`이고 시작 작업 트리는 깨끗했다. 작업 중 외부에서 추가된 `gate2-common.md`의 긴급 지시도 읽고 그 diff를 보존했다. 게임 실행·설치·커밋·푸시·설정 변경은 하지 않았다. 게임 수치는 기존 `E:/StutterFixWork/night-results/al-ga-5.log`의 재분석이다. 전달 파일은 후반 발췌이므로 세 픽셀 표본과 첫 세 FPS 표본을 전체 로그에서 보완했다. [원본 SHA256·행 번호·발췌·집계·현재 소스 해시](gate2-A-run7-analysis.txt)를 남겼다.

## 끝까지 완료된 시험의 실제 결과

| 시점 | 소유 / 구간 | A↔B 변경 픽셀 / 최대 차이 | 잡음 / A↔A2 | stable |
|---|---:|---:|---:|---|
| 262초 | 1,490 / 1,113 | 391,798 / 6 | 0 / 0 | True |
| 270초 | 1,518 / 1,153 | 121,812 / 7 | 0 / 0 | True |
| 276초 | 17 / 15 | 0 / 0 | 0 / 0 | True |

| 262→276초 ABBA | FPS | ms/프레임 | 프레임 수 |
|---|---:|---:|---:|
| off 0 | 122.331735 | 8.174494 | 1,705 |
| on 1 | 14.682848 | 68.106680 | 204 |
| on 2 | 14.738163 | 67.851063 | 204 |
| off 3 | 120.325125 | 8.310816 | 1,683 |

정책과 같은 산술평균 FPS는 off **121.328430**, on **14.7105055**, 이득 **−87.875467028%**다. +15% 목표는 **139.5276945 FPS**, 그 역수에 해당하는 전체 프레임 예산은 **7.167035932ms**다. 예산 환산값을 독립 측정 프레임 시간으로 쓰지 않는다. 이번에는 132단계가 끝까지 진행됐고 마지막에 `variant=A on=False`를 남겼다. 이전 `GPU readback exceeded 15s`는 이번 실행의 중단 원인이 아니다.

Prof는 **873,985표본 / set_pct=70.049131% / valid=True**다. DecoAnim.Step의 Apply/OnUpdate 표본이며, PairXY/GroupStep 억제·StepPair/OnComplete·setter 이후 엔진 비용 제외 한계는 유지한다.

## 긴급 1: 스캔 범위와 비용, 증분 전환 설계의 결손

- `LiveDecoRenderer.Register`(197행)는 `Resources.FindObjectsOfTypeAll<Renderer>()`에서 scene 렌더러를 등록한다. 로그의 registry는 **157,254개**다. FindObjects를 매 프레임 반복하는 코드는 아니다.
- `RenderFrame`(631~634행)은 **매 프레임 Scan**을 부른다. Scan(477~539행)은 sources 전체를 순회하고 live 소스의 재질 슬롯·정렬 키·소유 상태를 갱신한다. 장식 28,828개에 한정된 순회가 아니다.
- `GeometryReason`(233행)은 소유 계층·자식 Transform·스크립트·렌더러 상태를 읽는다. `SkipLate`(398행)는 TightLate·UpdatePosition·회전/크기 setter·Parallax 경로에서 같은 검사를 다시 부른다. PreCull의 cost 밖에도 안전 검사와 Harmony 호출 비용이 남는다.
- Reconcile은 topology 변경 때만 돈다. 측정 창 두 개에서 transition은 **각 204회**, created 합계 **10,027 / 9,861**, destroyed 합계 **11,061 / 10,888**이다. 누적 생성·파괴 횟수이며 동시 존재 개수가 아니다. 멤버·버전·키·소유 변화별 기여량은 로그에 없다.

정지 8프레임 표본의 scan은 262초 **36.045113ms**, 270초 **38.227325ms**, 276초 **27.876138ms**다. 276초에는 소유 17개, vertex **0.019550ms**, upload **0.000850ms**, 업로드 호출 **0**, reconcile **0**이었다. 메시 작업이 거의 없어도 전 장면 스캔은 남는다. 이 관측 스캔 시간은 목표 전체 프레임 예산 7.167ms를 넘는다. 정점·업로드 최적화만으로 현재 설계의 목표를 달성할 근거가 없다. 이를 다른 구현의 물리적 하한으로 일반화하지 않는다.

재생 창 시작과 FPS 종료 사이에 출력된 **samples=1인 cost 표본만** 별도로 집계했다. on 1은 172건, on 2는 171건이다. 전체 204프레임을 완전히 추적한 자료는 아니다.

| 출력 표본 평균 ms | on 1 (172건) | on 2 (171건) |
|---|---:|---:|
| scan | 46.211451 | 46.172319 |
| reconcile | 4.241067 | 4.206554 |
| vertex | 1.656350 | 1.649179 |
| upload | 0.676295 | 0.682586 |
| 네 구간 합계 | 52.785163 | 52.710638 |
| 같은 창 전체 FPS 측정 프레임 시간 | 68.106680 | 67.851063 |

등록 갱신은 scan에 포함되어 별도 값이 없다. setter/SkipLate/Harmony·원래 게임·엔진·로그·대기 시간도 분리되지 않았다. 표본 범위도 달라 표의 차이를 Harmony 비용으로 간주할 수 없다. 정확한 켬/끔 차이 분해는 **미측정**이다. 호출마다 Stopwatch를 넣거나 추정 잔여를 확정 비용으로 기록하지 않았다.

소유 1,400~1,600개인 단일 프레임 표본은 **109 / 115건**이다. vertex 중앙값 **1.689700 / 1.678800ms**, upload 중앙값 **0.678900 / 0.674800ms**, 두 목표(≤0.5/≤0.4ms) 동시 충족은 **0/109, 0/115**다. 이 표본에서 dirty_images=owned, upload_calls=runs였다. 정지 화면의 더티 생략은 실제 움직이는 이 집합의 갱신을 없애지 못했다.

**스캔 삭제를 구현하지 않은 이유:** 현재 BoundaryPrefix는 장식 Setup/SetSprite/SetTile/SetBlendMode/SetVisible/SetMaskingType/SetTextureScaleMultiplier/SetDepth 일부만 감시한다. Source에는 비소유 타일·행성·기타 렌더러도 있다. 이들의 활성·enabled·forceRenderingOff·재질 슬롯·정렬·거리·계층 변경을 모두 알리는 dirty 등록 경로가 없다. 설치 CoreModule의 [Renderer API 목록](gate2-A-run7-renderer-api.log)에도 이 변경을 통합 통지하는 add_/remove_ 이벤트가 없다. 이 목록을 모든 다른 Unity 타입·콜백 기능의 부재 증명으로 확대하지 않는다.

현재 훅만으로 전환 시 Scan을 돌리면 비소유 장벽 변경을 놓칠 수 있다. GameObject 활성 콜백으로도 Renderer.enabled·재질·정렬 변경을 전부 포괄하지 못한다. 소유 장식 dirty만 추적하거나 검사 주기를 늘리는 것으로 변경 프레임의 정렬 안전성이 확보되지 않는다. 필요한 작업은 **맵/scene 입장에서 변경 작성 경로를 분류하고 소유·비소유 렌더러 모두의 등록/dirty/파괴/reset 프로토콜을 만드는 설계 변경**이다. 이를 구현한 것으로 기록하지 않는다. 중단 범위는 현재 전 장면 폴링·반복 검사 구현이다. 변형 A의 모든 가능한 설계가 불가능하다고 결론 내리지 않는다.

steady GC도 통과로 기록하지 않는다. 단일 프레임 표본의 heap_delta 중앙값은 **935,936 / 921,600바이트**이고, 코드의 CreateRun/멤버·인덱스 배열 생성이 계속 발생한다. heap_delta는 다른 관리 할당·GC도 섞인 힙 변화다. allocated_bytes=0만으로 이 전환 경로의 무할당을 증명할 수 없다.

## 긴급 2: 소유 18개의 원인을 판단할 수 있는 범위

276초 정지 비교는 **새 Begin부터 소유 17개**였고(원본 755~758행), 입장 검사도 **count=17 / excluded=0**이다. 이후 정지 8프레임도 17개다. 끝 시점의 작은 소유 수를 누적 vertex/bounds 오제외 1,400개로 설명할 수 없다. 재생 끝의 18개와 262·270초 약 1,500개는 서로 다른 시점이다.

소유 해제 경로는 Scan의 !Live·GeometryReason/스프라이트/재질 변경, BoundaryPrefix, TryLogicalColor의 alpha≤0이다. Live는 원본 enabled/active/layer/forceRenderingOff를 보고 InvisibleSkip도 알파에 따라 forceRenderingOff를 바꾼다. 기존 로그에는 !Live 세부 사유별 해제 수와 시점별 전체 live 후보 분모가 없다. **17~18개가 곡의 실제 표시 집합인지, 상호작용에 의한 과소 소유인지의 완전한 원인 분리는 미확정**이다. 사유별 관측 없이 가시성·알파 제외를 없애거나 소유 1,500개를 강제하지 않았다.

## 긴급 3: 픽셀 회귀와 코드/IL 확인

이번 262초 **391,798픽셀/최대6**은 이전 run4의 **674/최대2**보다 크다. 시각·소유 집합이 달라 엄밀한 한 변수 대조는 아니다. 5회차에는 Float32 색과 카메라 캐시/계산 등이 함께 바뀌었다. Float32이면 원본 최종 정점/셰이더 입력까지 같다는 이전 가설은 이번 결과로 지지되지 않는다. 관리 SpriteRenderer.color setter가 Float32인 사실만으로 내부 렌더링 정밀도까지 확정할 수 없다.

세 시점 noise=0·restored_fields=0·aa2_out=0이므로 기존 잡음 정지나 GPU 제한 시간을 다시 수정할 근거는 없다. 최대 차이 6/7은 PIXEL_NEAR(최대2)도 넘는다. 색·카메라 기저 계산·CPU 월드 정점과 원본 GPU 변환 반올림·정렬의 기여량은 분리되지 않았다. 이미 실패한 Color32 코드로 단순 복귀하거나 비교 임계·마스크를 늘리지 않았다. 픽셀 0 기준을 유지한다.

지정 디컴파일의 `scrDecorationManager.LateUpdate → LogicUpdate → UpdatePosition`, SetPosition/SetRotation/SetScale, scrParallax.SetTrans, scrVisualDecoration.ApplyColor를 설치 DLL IL과 새로 대조했다. [manager](gate2-A-run7-manager-il.log) · [decoration](gate2-A-run7-decoration-il.log) · [parallax](gate2-A-run7-parallax-il.log) · [visual](gate2-A-run7-visual-il.log). [게임 SHA256](gate2-A-run7-game-hash.txt)는 기존 **F692210F9A342F7EED4BE40AAD463286AB40BF046599959823942EA02F4CA3FF**와 같다. ilscan은 메타데이터만 읽었고 게임 메서드를 실행하지 않았다. scrVisualDecoration이 Renderer.enabled·SetActive·sortingOrder를 직접 쓰는 것도 확인했다.

## 검증 상태와 종료

이번 변경은 결과 문서·장부·10줄 요약과 감사 기록이다. 소스·생성기·바이너리를 바꾸지 않아 같은 빌드/fixture를 반복하지 않았다. 기존 run6의 DEV/Player/PlayerAuto 빌드 **오류 0/0/0**, LoadCheck **틀림 0/0/0**, Player 제외 metadata **36건**, 패치 대상 **17개**, 계층 **2,729**·바운드 **482**·스크립트 **15**·캐시 **40**·업로드 **32**, 구간 **106,460**·정책 **18**, FrameCompare **11/15/15/17/21/2**, MatShare **25,401**, 생성기 **24/25/132** 통과 로그를 확인했다. 이들은 이전 오프라인 결과다. 이번 신규 검증은 로그의 판정/산술 재계산·설치 DLL IL 감사·git diff --check다. [감사 검증 25건](gate2-A-run7-validation.txt)은 픽셀 전사·ABBA 산술·소유 표본·목표 미충족·생산 소스/생성기/프로젝트의 HEAD 동일성·요약 정확히 10줄을 확인했다.

워크트리에 없는 로드맵과 freeze 결과는 `E:/StutterFixWork/StutterFix/docs/ROADMAP-EXTREME-20261021.md`, `E:/StutterFixWork/wt-freeze/docs/handoff/codex-freeze-20261021-result.md`에서 읽었다. freeze 문서 자체에는 실측이 없으며 공통 인계의 3.69ms 배경값을 신규 측정으로 쓰지 않았다.

**현재 설계 한계 중단. 스캔 제거·소유 감소 세부 원인 수정·실픽셀 0·FPS +15%·수명 검증은 완료하지 못했다.** 자동 루프 재실행과 동일 코드 재시험 권고는 하지 않는다. 기본 꺼짐·Player 제외·판정 기준·기존 안전 검사를 유지했다. 메모리·스킬 파일은 변경하지 않았다.

---

# Gate 2 A 5회차 수정 — 캡처 제한 시간 분리·정점 캐시·더티 업로드 (2026-10-21, 로그 run6)

**코드 수정과 오프라인 검증 완료. GATE2=PASS 미달성. 수정 후 실픽셀·FPS·정점 0.5ms/업로드 0.4ms 목표는 미측정이다.** `codex-al-ga-5.md` → 장부 → run4 증거 순으로 읽었다. 시작 HEAD는 `ba29aa8`이며 처음 확인한 작업 트리는 깨끗했다. 기존 계층·ToggleVisible·바운드·PlanetSprite 정지를 유지했다. 게임 실행·설치·커밋·푸시・`.codex/config.toml` 수정은 하지 않았다.

워크트리에 없던 로드맵과 freeze 결과는 각각 `E:/StutterFixWork/StutterFix/docs/ROADMAP-EXTREME-20261021.md`, `E:/StutterFixWork/wt-freeze/docs/handoff/codex-freeze-20261021-result.md`에서 읽기만 했다. freeze 결과 문서 자체에는 신규 실측이 없으며, 8.95→5.25ms/3.69ms 절감은 추가된 공통 인계의 배경 수치로 취급한다. 작업 중 발견한 `gate2-common.md`의 성능 지침 추가 diff는 외부 변경이며 보존했다. 이 세션에서 해당 파일을 편집하지 않았다.

## 전달 결과와 실제 중단 지점

출처는 [run4 전달 증거](ga-autoloop-run4-evidence.txt)와 기존 `E:/StutterFixWork/night-results/al-ga-4.log`다. 신규 게임 측정이 아니다. [원본 SHA256·줄 번호·발췌](gate2-A-run6-prior-capture-evidence.txt)를 보존했다.

| 항목 | 262초 | 270초 |
|---|---:|---:|
| 소유 / 구간 | 1,478 / 1,114 | 1,577 / 1,230 |
| N1↔N2 변경 픽셀 / 상태 변화 | 0 / 0 | 0 / 0 |
| A↔B / 최대 차이 | 674 / 2 | B 캡처 실패 |
| A↔A2 / stable | 0 / True | 미도달 |
| 정상 상태 표본 수 | 8 | 5 |
| vertex_ms / upload_ms | 2.753813 / 1.437425 | 2.727280 / 1.510000 |
| scan_ms / upload_calls | 35.655163 / 1,114 | 34.950140 / 1,230 |
| steady_allocated_bytes | 0 | 0 |

최종 중단은 원본 614행의 **`framecmp grab B`: GPU readback exceeded 15s**다. 270초 noise/assertzero는 통과했다. 이전의 잡음·계층·소유 0 실패를 다시 고칠 상황이 아니다. 276초 픽셀·ABBA FPS는 미도달이며 최종 GATE2=ERROR다. Prof는 **samples=953,430 / set_pct=69.633873% / valid=True**다. PairXY/GroupStep을 억제한 Step Apply/OnUpdate 표본이며 StepPair·OnComplete·setter 이후 엔진 비용은 제외한다.

## 코드 수정

1. **GPU 제한 시간에서 동기 상태 감사를 분리했다.** 기존 Grab은 Request와 시작 시각 저장 직후 전체 State()와 비교를 실행했다. 157,254개 렌더러의 CPU 감사 동안 PlayerLoop에 양보하지 않은 시간도 GPU 대기 15초로 셌다. 기존 성공 캡처 elapsedMs도 10,960.029~24,006.205ms여서 GPU 단독 지연으로 해석할 수 없다. 같은 EndOfFrame 안의 두 상태 수집·동일성 검사를 먼저 끝내고 Request→시작 시각→최소 1회 yield→완료/15초 검사를 수행한다. pending RT 수명·실패 정리·읽기 오류 검사는 유지한다. elapsedMs는 요청 대기, submissionAuditMs/completionAuditMs는 CPU 감사로 분리했다. **시간 혼합은 코드로 확인했지만, 기존 실패의 실제 GPU 지연 기여량은 분리 측정이 없어 확정하지 않는다.**
2. **정점 색의 추가 양자화를 제거했다.** 지정 디컴파일 scrVisualDecoration.ApplyColor와 설치 게임 IL의 경로는 Float32 색을 rendererColor/SpriteRenderer.color에 쓴다. 기존 메시 경로는 이를 Color32로 다시 양자화했다. 0.5도 8비트 UNorm으로 정확히 표현할 수 없으므로 임의의 원본 색을 보존하지 못한다. **공통 문서 초기 Color32 레이아웃은 엄격한 동일 목표와 충돌하므로, 동일 목표를 우선해 색만 Float32x4로 변경했다.** 위치·색·UV 인터리브와 SetVertexBufferData는 유지한다. stride는 24→36바이트, 1,500개×4정점 전량은 144,000→216,000바이트다. 실제 버퍼 stride/색 형식을 입장 때 검사한다. 이 변경이 674픽셀 전부를 해결한다는 주장은 하지 않는다. 정점 변환·래스터화 차이도 가능하다.
3. **Compute의 장식별 Unity 프로퍼티 읽기를 제거했다.** 카메라 위치·회전·배율·화면 기저를 프레임마다 한 번 캡처한다. 소유 시작 때 구한 로컬 코너·UV·flip·고정 계층 Shape를 재사용하고, Compute는 논리 C# 필드와 스냅샷만 읽는다. camScaleMultiplier의 원래 식과 연산 순서를 유지한다. 카메라 배치형은 직교 화면 기저로 계산하며 입장 때 기존 실제 정점/바운드 1e-4 검사를 그대로 받는다. 이 경로의 실게임 정밀도·소유 비율은 미검증이다.
4. **정확 비교 더티 추적을 추가했다.** 설정 대체 지점의 위치·피벗·회전·크기·색 변화가 dirty를 세운다. 계산된 월드 위치·크기·피벗·각도도 매 프레임 비교해 카메라 변화와 직접 논리 필드 변경을 검출한다. 각도가 같으면 쿼터니언을 재사용한다. pose가 같으면 4정점 계산을, 색도 같으면 버퍼 채움을 생략한다. 허용 오차·색 반올림·작은 움직임 무시는 없다.
5. **더티 구간만 업로드한다.** 새 구간·버퍼 오프셋 이동·깊이 변화는 모든 멤버를 채운다. 기존 구간에서 멤버 하나가 바뀌면 그 멤버만 채우고 구간당 업로드 1회를 예약한다. 변하지 않은 다음 프레임은 이전 dirty를 물려받지 않는다. images 사전 전체 순회 대신 현재 소유 구간 멤버를 계산/숨김 처리한다. cost에 dirty_images/skipped_uploads/vertex_target_ms=0.5/upload_target_ms=0.4/mesh_targets_met를 추가했다. vertex_ms에는 공통 카메라 캡처와 정점 계산·채움이 포함된다.
6. **PIXEL_NEAR는 최대 차이 2까지 별도 기록한다.** pass=0이며 세 시점 ab_out=0·안정성·복원·ABBA 평균 FPS +15%의 PASS 조건은 그대로다. 다른 구간을 한 Mesh로 합치면 단일 Renderer의 layer/order/distance와 첫 재질 ID 정렬을 독립적으로 유지한다는 근거가 없어 적용하지 않았다. 재질 복제·아틀라스·커스텀 셰이더·워커·Gate 3는 추가하지 않았다.

## 오프라인 검증

| 검사 | 결과 | 로그 |
|---|---|---|
| Release DEV / Player / PlayerAuto | 오류 0/0/0, 경고 38/4/38 | gate2-A-run6-build-dev.log / -player.log / -auto.log |
| LoadCheck DEV / Player / PlayerAuto | 276/229/276곳, 틀림 0/0/0 | gate2-A-run6-loadcheck-dev.log / -player.log / -auto.log |
| 생산 계층 / 바운드 / 스크립트 | 2,729 / 482 / 15 통과 | [geometry](gate2-A-run6-geometry-fixture.log) |
| 신규 생산 pose 캐시 / 업로드 무효화 | 40 / 32 통과 | 같은 로그 |
| 구간·앵커 / Gate 2 판정 / edition / 패치 대상 | 106,460 / 18 / 36 / 17 통과 | [fixture](gate2-A-run6-fixture.log) |
| FrameCompare RGBA / 마스크 / 상태 / 대조 | 11 / 15 / 15 / 17 통과 | [framecmp](gate2-A-run6-framecmp-fixture.log) |
| FrameCompare edition·정지·실제 Grab IL 순서 / 게임 정지 콜백 | 21 / 2 통과 | 같은 로그 |
| MatShare / 기존 FrameCompare steps | 25,401 / 24 통과 | gate2-A-run6-matshare-fixture.log / -framecmp-steps-fixture.log |
| Gate 1 / Gate 2 steps | 25 / 132단계, UTF-8 CLI 통과 | gate2-A-run6-gate1-steps-fixture.log / -steps-fixture.log |

캐시 fixture는 생산 메서드를 관리 수학 어댑터로 컴파일해 독립적인 leaf→pivot→world 연산과 대조한다. 각 필드별 변화·0/음수 크기·1e-6 이동·반복 프레임 비트 보존·재입장 강제 검사를 포함한다. 업로드 fixture는 새/재배치 버퍼·pose-only/color-only 변화·변경 없는 이웃·다음 정지 프레임을 검사한다. 메타데이터 검사는 DEV/PlayerAuto 정점의 Float32 Color, 두 동기 상태 감사가 Request 앞에 있음, 일반 Player의 실험 타입·참조 부재를 확인한다.

지정 디컴파일과 [scrDecoration IL](gate2-A-run6-decoration-il.log)·[scrParallax IL](gate2-A-run6-parallax-il.log)을 대조했다. [게임 DLL 해시](gate2-A-run6-game-hash.txt)는 기존과 같은 F692210F9A342F7EED4BE40AAD463286AB40BF046599959823942EA02F4CA3FF다. scrVisualDecoration.ApplyColor는 gate2-A-run6-visual-il.log에서 rendererColor 저장과 SpriteRenderer.color 쓰기를 추가 확인했다. 이 검사들은 Unity/Mono의 실제 렌더링·Harmony 실행·픽셀·성능 증명이 아니다. LoadCheck 동적 이름 5곳과 기존 빌드 경고는 남는다. 보조 검사에서 잘못 지정한 test-framecmp-steps.py는 실제 check-framecmp-steps.py로 정정했고 최종 로그는 24건 통과다.

## 남은 수치와 한계

| 항목 | 이번 수정 후 |
|---|---|
| 정점 ≤0.5ms / 업로드 ≤0.4ms (약 1,500개) | 미측정 / 미측정, 달성 주장 안 함 |
| 정지 프레임 계산·업로드 생략 | 관리 fixture만 통과, Unity 실제 호출 수는 미측정 |
| 262/270/276초 실픽셀·A/A2 | 미측정 |
| ABBA 평균 FPS +15% | 미측정 |
| Unity/Mono steady GC / 재시작·사망·맵 재열기·언로드 | 미측정 |

**전체 scene 안전 스캔은 남아 있다.** 직전 정상 표본 scan_ms=35.655163/34.950140은 정점·업로드보다 훨씬 크다. 이번 변경은 Compute 읽기를 줄였지만 전체 렌더러의 live/외부 장벽/재질·계층 검사는 삭제하지 않았다. SkipLate 반복 안전 검사도 남아 있다. 따라서 메시 목표를 충족해도 전체 FPS +15%를 보장하지 않는다. 서로 다른 구간을 합치거나 안전 검사를 생략해 수치를 맞추지 않았다.

생성기는 132단계를 유지한다. renderer_version=5·Float32x4·분리한 감사 시간·dirty/skip 로그로 변경 코드의 가설을 판별할 수 있다. 실제 GPU 대기 15초 초과는 계속 ERROR다. 동일 코드 재시험을 반복 실행/요청하지 않았으며 게임 실행 없이 여기서 종료한다. 이전 674/2를 PIXEL_NEAR나 PASS로 소급 변경하지 않았다.

task-observer 관찰 반환(사용자 범위 저장소는 읽기만 함, 스킬·메모리 쓰기 없음): 문제 — 비동기 요청 직후 큰 동기 감사를 대기 제한에 포함하면 CPU 지연을 GPU 실패로 오인한다. 개선 — 요청 전 상태 검증과 요청 후 대기를 분리하고 각각 기록한다. 원칙 — 제한 시간은 해당 자원이 진행할 기회를 받은 구간에서 측정하며 전체 작업 시간과 구별한다.

---

# Gate 2 A 최신 수정 — 행성 재질 애니메이션 정지 누락 (2026-10-21, 로그 run5)

**일시정지 중에도 진행하던 PlanetSprite의 재질 애니메이션을 막고 오프라인 검증을 완료했다. 수정 후 실픽셀·FPS는 미측정이며 Gate 2 PASS를 선언하지 않는다.** codex-al-ga-4.md → 장부 → run3 전달 증거 순서로 확인했고, 시작 HEAD는 `3b8bdd6`, 작업 트리는 깨끗했다. 기존 계층·ToggleVisible·바운드 검사는 유지했다. 게임 실행·설치·커밋·푸시·`.codex/config.toml` 수정은 하지 않았다.

## 전달 시험과 추가로 찾은 원본 로그

[run3 전달 증거](ga-autoloop-run3-evidence.txt)의 262초 결과는 소유 1,472개 / 구간 1,100개, 정점 검사 2,087개 중 615개 제외(bounds-error 611 / vertex-error 4)다. vertex_max_err=0.00013647876, bounds_max_err=0.000272957521, local_bounds_z=0.2가 실제로 기록됐다. 소유된 장식의 오차는 기존 1e-4 이하다. Prof는 samples=972,702 / set_pct=69.249446% / valid=True이며, 앞서 기록한 Step 표본·PairXY/GroupStep 억제·후속 엔진 비용 제외 한계는 유지한다.

| 전달 실측 | 값 |
|---|---:|
| 262초 A↔B 변경 / 최대 채널 차이 | 2,278 / 5 |
| 262초 A↔A2 변경 | 769 |
| 안정성 / 최종 판정 | False / GATE2=ERROR, unstable_baseline |
| 정상 상태 표본 9프레임의 vertex / upload / scan ms | 2.567067 / 1.363356 / 34.416711 |
| 같은 9프레임의 upload_calls / steady_allocated_bytes | 1,100 / 0 |
| 입장 프레임 reconcile / scan ms | 36.642200 / 841.307000 |
| ABBA FPS | 미도달 |

워크트리 밖 **기존** `E:/StutterFixWork/night-results/al-ga-3.log`를 읽기만 해, 요약에서 빠졌던 잡음 대조를 찾았다. [출처 해시와 발췌](gate2-A-run5-prior-capture-evidence.txt)에 보존했다. 이번에 게임을 실행해 수집한 자료가 아니다.

- 렌더러를 켜기 전 262초 N1↔N2부터 **818픽셀**, 최대 RGBA 4/3/4/0이었다. 당시 rendererStateEqual=True였으므로 기존 상태 검사가 보지 못하는 출력 변화가 있었다.
- 잡음의 상위 영역은 (1376,864), (1344,864), (1312,864) 등이다. A↔A2의 상위 영역도 같은 위치에 집중된다. 이후 270초 잡음도 1,194픽셀이다. 위치 중첩은 원인 추적의 단서이며 기여량의 증명은 아니다.
- 따라서 A↔B 2,278개를 모두 메시 오류로 단정할 수 없다. 잡음을 빼서 차이를 0으로 만들거나 마스크·임계를 늘리지 않았다. 9프레임의 할당 0 역시 전체 곡·성능·수명 검증으로 확대하지 않는다.

## 코드 원인과 수정

지정 디컴파일 `E:/StutterFixWork/decompiled/Assembly-CSharp/PlanetSprite.cs:82–84`의 `LateUpdate`는 매 프레임 `meshRenderer.material.SetFloat("_Frame", Time.unscaledTime * 12f % 11f)`를 실행한다. 기존 진단 정지는 **PlanetRenderer.LateUpdate만** 차단했다. Time.timeScale=0 / DOTween.timeScale=0 / controller.paused로는 PlanetSprite의 unscaledTime 재질 갱신을 막지 못한다. 기존 상태 서명은 행렬·Sprite ID·color·재질 ID를 비교하지만 이 재질 값은 포함하지 않았다.

[설치 게임 PlanetSprite IL](gate2-A-run5-planetsprite-il.log)에서 get_material → get_unscaledTime → Material.SetFloat 호출을, [PlanetRenderer IL](gate2-A-run5-planetrenderer-il.log)에서 별도 LateUpdate 경로를 교차확인했다. [게임 DLL 해시](gate2-A-run5-game-hash.txt)는 기존 F692210F9A342F7EED4BE40AAD463286AB40BF046599959823942EA02F4CA3FF와 같다. ilscan은 메타데이터만 읽고 게임 메서드를 실행하지 않았다.

1. `FrameCompare.FreezePlanets`가 두 타입의 선언된 instance void LateUpdate/0을 정확히 하나씩 선택해 **같은 !Paused Prefix**를 건다. 마지막 표시 값을 유지하며 재질 값을 강제로 쓰지 않는다. 부분 패치 실패도 기존 Pause→Resume 정리에서 같은 Harmony ID로 해제된다. 정상 resume/stop/error/unload의 복원 경로도 그대로 사용한다.
2. 캡처 상태에 PlanetSprite의 `planetFrame`을 추가한다. 기존 meshRenderer 참조를 이용하고 **sharedMaterial**로 읽어 감사 과정에서 재질을 복제하지 않는다. 숨김 상태 구분과 모든 기존 안정성 조건은 유지한다. 이 값은 모든 셰이더 속성의 완전한 감사가 아니다.
3. `GatePixel`에 `gate2-restore`의 실제 필드 차이와 `gate2-baseline noise_valid/noise_out/noise_state_stable/restored_fields/aa2_out`을 추가했다. 다음 오류에서 잡음과 복원 상태 변경을 구분할 수 있다. 최종 판정식은 바꾸지 않았다.
4. Gate 2 생성기는 세 시점 각각 `noise N1 N2` 바로 뒤에 **assertzero N1 N2**를 수행한다(129→132단계). 잡음 픽셀 0과 상태 동일을 확인한 뒤 A→on→B→off→A2로 진행한다. 불안정하면 ERROR로 끝내며 재시도·유리한 표본 선택·잡음 상쇄는 하지 않는다. filters on·중앙 20% 제외·픽셀 임계 0·프로파일→3시점→ABBA 순서는 유지한다.

이 정지 누락은 코드·IL로 확인했지만, **818/769개 전체가 이 경로 때문인지, A↔B에 독립적인 메시 차이가 얼마나 남는지는 미확정**이다. 필터 비활성화나 렌더러 방식 변경으로 우회하지 않았다. renderer_version=4와 소유/바운드 정책도 그대로다.

## 오프라인 검증

| 검사 | 결과 | 근거 |
|---|---|---|
| Release DEV / Player / PlayerAuto | 오류 0/0/0, 경고 38/4/38 | gate2-A-run5-build-dev.log / -player.log / -auto.log |
| LoadCheck DEV / Player / PlayerAuto | 276/229/276곳, 틀림 0/0/0 | gate2-A-run5-loadcheck-dev.log / -player.log / -auto.log |
| FrameCompare RGBA / 마스크 / 상태 / 대조 | 11 / 15 / 15 / 17 checks 통과 | [framecmp](gate2-A-run5-framecmp-fixture.log) |
| FrameCompare edition·생산 정지 IL / 설치 게임 두 콜백 서명 | 19 / 2 checks 통과 | 같은 로그 |
| 생산 계층 / 컬링 바운드 / 스크립트 판정 | 2,729 / 482 / 15 checks 통과 | [geometry](gate2-A-run5-geometry-fixture.log) |
| 구간·앵커 / Gate 2 판정 / edition / 패치 대상 | 106,460 / 18 / 31 / 17 checks 통과 | [fixture](gate2-A-run5-fixture.log) |
| MatShare / FrameCompare 기존 steps | 25,401 / 24 checks 통과 | gate2-A-run5-matshare-fixture.log / -framecmp-steps-fixture.log |
| Gate 1 / Gate 2 생성기 | 25 / 132단계, UTF-8 CLI 통과 | gate2-A-run5-gate1-steps-fixture.log / -steps-fixture.log |
| git diff --check | 통과 | 최종 로컬 검사 |

새 fixture는 같은 행렬/재질 ID여도 planetFrame 변경이 상태 차이로 검출되는지와, 실제 DEV/PlayerAuto IL에 두 타입을 대상으로 한 정지 설치 호출이 있는지 검사한다. 설치 게임의 두 메서드도 instance void LateUpdate/0 서명으로 각각 유일함을 확인했다. 일반 Player의 FrameCompare·LiveDeco 타입/참조 부재를 확인했다. **실제 Mono Harmony 실행·Unity 네이티브 렌더링·픽셀·FPS·수명 복원을 검증한 것은 아니다.** LoadCheck 동적 이름 5곳과 기존 빌드 경고는 남는다.

## 인계와 한계

자동 루프는 변경된 **132단계 생성기**를 사용한다. 새 pause 로그의 PlanetSprite._Frame, N1↔N2 `planetFrame` 안정성과 변경 픽셀 0을 먼저 확인한다. 그 뒤 262/270/276초 A↔B=0, A↔A2=0·상태 동일, off/on/on/off 262→276초 평균 FPS +15%를 모두 만족해야 PASS다. 잡음이 남으면 기존 ERROR를 유지하며 새 로그로 다른 갱신원을 판별한다.

수정 후 소유/구간·실픽셀·FPS·GC·재시작/사망/맵 재열기/언로드 복원은 미측정이다. 이번 변경은 진단 일시정지와 검사에 한정되므로 재생 중 scan_ms 등 성능 문제를 해결했다고 주장하지 않는다. 새로운 설계 한계로 확정할 증거는 아직 없고, 코드에서 확인한 정지 누락을 수정했다. 동일 코드 게임 시험 반복은 하지 않았다.

task-observer v3.5.0 관찰 반환(사용자 범위 스킬·메모리 쓰기 없음): 문제 — 행성 표시를 멈출 때 부모의 콜백만 막으면 자식이 쓰는 재질 애니메이션은 진행하고 Renderer 서명은 같게 남는다. 개선 — 별도 콜백의 시간원·재질 쓰기를 추적하고 해당 값을 상태 검사에 포함한다. 원칙 — 기준 화면의 잡음과 복원 차이를 먼저 분리한 뒤 구현 차이를 해석한다.

---

아래는 보존한 이전 수정 결과다.

# Gate 2 A 최신 수정 — 정점과 컬링 바운드 비교 오류 (2026-10-21, 로그 run4)

**검증 코드의 서로 다른 도형 비교를 수정하고 오프라인 검증을 완료했다. 실게임 원인 확정·소유·픽셀·FPS는 미측정이며 Gate 2 PASS를 선언하지 않는다.** codex-al-ga-3.md·장부·run2 증거를 먼저 읽었다. 시작 HEAD는 `4e59c35`, 작업 트리는 깨끗했다. 기존 계층·프로파일·ToggleVisible 수정은 유지했다. 게임 실행·설치·커밋·푸시·config 수정은 하지 않았다.

## 전달 실측

출처는 [ga-autoloop-run2-evidence.txt](ga-autoloop-run2-evidence.txt)이며 이번 세션의 게임 측정값이 아니다. 소유/구간 0/0, vertexcheck count=2,087 / excluded=2,087 / max_err=0.100000374. 제외는 vertex-error 2,087개 / repeat-tile 38개 / keywords 1개다. no eligible images로 GATE2=ERROR, 픽셀/FPS 미도달이다. scan_ms=842.840100은 입장 실패 프레임 비용이며 정상 프레임 비용으로 쓰지 않는다.

프로파일은 samples=987,093 / set_pct=69.311975% / valid=True다. PairXY/GroupStep 억제 상태의 Step 표본 Apply/OnUpdate 설정 비율이며 StepPair·OnComplete·setter 뒤 엔진 작업은 포함하지 않는다. 앞서 실패한 계층/스크립트 허용 방식을 다시 바꾸거나 같은 게임 시험을 반복하지 않았다.

## 원인과 수정

기존 Admit은 실제 정점 네 개와 계산 정점을 비교한 뒤, 계산 정점으로 만든 **두께 0인 AABB**와 **Renderer의 컬링 AABB**인 bounds의 min/max 차이도 같은 max_err에 넣었다. 이 둘은 같은 도형이 아니다. 컬링 상자의 Z 두께가 0.2이면 정확히 같은 정점도 이 비교에서 0.1 오차를 만든다. 새 회귀 검사에서 이 잘못된 탈락을 재현했다.

**실게임 로그에는 정점/바운드별 오차와 실제 localBounds 두께가 없으므로, 2,087개 모두의 오차가 Z 두께 때문이라고 단정할 수 없다.** 0.100000374는 이 코드 오류와 부합한다. 고정값 0.1을 빼거나 허용 오차를 늘리지 않았다.

- 실제 월드 정점 네 개는 기존처럼 TransformPoint와 직접 비교한다. `1e-4` 초과/비유한 값은 vertex-error로 거부한다.
- 실제 **Renderer.localBounds의 8개 모서리**를 캐시한 고정 계층·논리 child 오프셋·논리 피벗 위치/회전/크기로 변환해 월드 AABB를 만든다. 이를 Renderer.bounds와 비교해 `1e-4` 초과/비유한 값은 별도 bounds-error로 거부한다. 로컬 바운드의 flip을 재차 적용하지 않는다. 렌더러/바운드 값을 변경하지 않는다.
- 추가 계산은 소유 입장 검증에서만 수행한다. 정상 프레임의 정점/업로드 루프에 추가 바운드 계산/배열 할당은 없다. Transform·색 쓰기 차단과 off/해제 복원은 유지한다.
- 시작 표시는 `renderer_version=4 bounds_policy=renderer-local-aabb`. vertexcheck에 vertex_max_err / bounds_max_err / quad_bounds_max_err / bounds_source를 추가한다. max_err는 같은 도형끼리의 두 검사 중 최댓값이며 옛 평면 상자 비교 값은 quad_bounds_max_err로만 보고한다.
- 검사 프레임의 첫 최대 3개에 vertexdetail의 vertex_err / bounds_err / quad_bounds_err / local_bounds_z를 기록한다. 전체 탈락 때도 로그가 남는다. 기존 제외 정책과 픽셀/FPS 기준은 유지했다.

지정 디컴파일 scrDecoration.cs의 SetPosition/SetRotation/SetScale/UpdatePosition과 scrParallax.cs의 SetTrans를 읽고 [장식 IL](gate2-A-run4-decoration-il.log)·[시차 IL](gate2-A-run4-parallax-il.log)과 대조했다. child 오프셋은 `(pivotOffset + croppedPivotOffset) / trueScaleMultiplier`, 피벗 위치는 Vector2 localPosition 설정 후 시차 적용, 회전은 startRot+rotAngle 및 카메라 잠금, 스케일 Z는 1이다. 기존 논리 수식을 바꿀 증거는 없었다. Transform 소비자 감사/제외는 아래 기존 결과 범위를 유지한다.

[CoreModule IL](gate2-A-run4-renderer-api-il.log)에서 get_localBounds와 get_bounds가 서로 다른 native injected getter임을 확인했다. 관리 IL로 실제 네이티브 상자 두께를 증명하지 않는다. [게임 DLL SHA256](gate2-A-run4-game-hash.txt)은 `F692210F9A342F7EED4BE40AAD463286AB40BF046599959823942EA02F4CA3FF`로 기존과 같다. ilscan은 메타데이터만 읽었다. 없는 로드맵은 정식 체크아웃에서, freeze 결과는 wt-freeze에서 읽기만 했다. freeze의 새 실게임 수치는 없었다.

## 오프라인 검증

| 검사 | 결과 | 로그 |
|---|---|---|
| Release DEV / Player / PlayerAuto | 오류 0/0/0, 경고 38/4/38 | gate2-A-run4-build-dev.log / -player.log / -auto.log |
| LoadCheck DEV / Player / PlayerAuto | 276/229/276곳, 틀림 0/0/0 | gate2-A-run4-loadcheck-dev.log / -player.log / -auto.log |
| 생산 계층 / 새 생산 바운드 / 스크립트 판정 | 2,729 / 482 / 15 checks 통과 | [geometry](gate2-A-run4-geometry-fixture.log) |
| 구간·앵커 / Gate 2 판정 / edition / 패치 대상 | 106,460 / 18 / 31 / 17 checks 통과 | [fixture](gate2-A-run4-fixture.log) |
| MatShare / FrameCompare | 25,401 / 11+15+14+17+13 checks 통과 | gate2-A-run4-matshare-fixture.log / -framecmp-fixture.log |
| Gate 1 / Gate 2 생성기 | 25 / 129단계, UTF-8 CLI 통과 | gate2-A-run4-gate1-steps-fixture.log / -steps-fixture.log |
| git diff --check | 통과 | 최종 로컬 검사 |

새 검사는 생산 WorldBounds를 직접 연결해 두께 0.2의 옛 0.1 오탈락 재현·올바른 바운드 일치·비대칭 flip 바운드·중간 계층·음수/0 크기·Z 크기·회전·논리 오프셋과 독립 8모서리 순차 변환의 일치·실제 0.001 위치 차이 검출을 검사한다. System.Numerics 어댑터이므로 Unity native bounds·Mono float·실픽셀·성능 검증은 아니다. 일반 Player에는 실험 타입/참조가 없다. LoadCheck 동적 이름 5곳과 Harmony 실제 실행은 런타임 확인 대상이다.

## 자동 루프 인계와 미측정

기존 129단계 프로파일→262/270/276초 픽셀·복원→off/on/on/off 262→276초 FPS 생성기는 유지한다. 변경된 코드에서 vertexdetail의 local_bounds_z 및 세 오차, vertexcheck의 분리 최댓값과 제외 사유, owned/runs·visibility_admitted·비용을 확인한다. 옛 quad 오차만 약 0.1이고 vertex/bounds 오차는 1e-4 이하라면 비교 도형 차이가 직접 확인된다. 실제 정점/바운드 오차가 남으면 해당 사유로 계속 거부한다.

**수정 후 소유·구간·실정점·픽셀·FPS·GC·재시작/사망/맵 재열기/언로드 복원은 모두 미측정**이다. 약 1,200구간은 예상치다. 세 시점 ab_out=0, 복원/기준 상태 안정성과 ABBA 켬 평균 FPS +15%를 모두 만족해야 PASS다. 설계 한계로 확정할 근거는 아직 없고 비교 코드의 수정 가능한 오류를 고쳤다. 코드 변경 없는 같은 게임 시험의 재실행은 하지 않았다.

task-observer v3.5.0 관찰 반환(사용자 범위 쓰기 없음): 문제 — 정점과 컬링 AABB처럼 다른 양을 한 최댓값으로 합치면 실제 변환 오류를 구별하기 어렵다. 개선 — 같은 도형끼리 비교하고 구성별 오차·원본 바운드 두께를 기록한다. 원칙 — 합성 재현으로 확인한 코드 오류와 실게임 원인 추정/미측정을 구분한다.

---

아래는 보존한 이전 수정 결과다.

# Gate 2 A 최신 수정 — ToggleVisible 오분류 (2026-10-21, 로그 run3)

**표준 프리팹의 ToggleVisible을 외부 자식 스크립트로 거부하는 원인을 수정했다. 오프라인 검증은 통과했고 수정 후 소유 수·픽셀·FPS는 미측정이다. Gate 2 PASS를 선언하지 않는다.** codex-al-ga-2.md의 장부와 최신 증거부터 읽고 기존 계층/프로파일 수정을 보존했다. 시작 시 작업 트리는 깨끗했고 HEAD는 863096b였다. 게임 실행·설치·커밋·푸시·config 수정은 하지 않았다.

## 최신 전달 시험

[자동 수집 증거](ga-autoloop-run1-evidence.txt)는 이전 계층/프로파일 수정 이후의 결과다. 이번 세션에서 게임을 측정한 값이 아니다.

| 항목 | 전달 실측 |
|---|---|
| 소유 / 구간 | 0 / 0 |
| 제외 | foreign-child-script 2,088개, repeat-tile 38개, keywords 1개 |
| 프로파일 | enabled=True, samples=957,795, valid=True |
| 설정 비율 | set_pct=68.852079% |
| 실패 | livedeco on: no eligible images, GATE2=ERROR / autotest_aborted |
| 픽셀 / FPS | 단계 미도달 |

프로파일은 이전 수정으로 표본이 생겼다. **68.852079%는 Step에서 표본 수집한 Apply/OnUpdate의 설정 비중**이다. Prof의 PairXY/GroupStep 억제, StepPair·OnComplete·setter 이후 엔진 작업 제외 한계가 있어 평상시 전체 애니메이션 비용의 비율로 확대하지 않는다. 실패 입장 프레임 scan_ms=478.261100도 정상 재생 프레임 비용이나 FPS 근거가 아니다.

## 원인과 확인 근거

기존 Admit은 자기 장식·parallax·blend·mask 참조 이외의 활성 MonoBehaviour를 모두 foreign-child-script로 거부했다. 게임 표준 이미지 프리팹의 sprite 노드에는 활성 ToggleVisible이 들어 있으므로 이 프리팹은 그 조건에 걸린다.

외부 패키지 설치 없이 표준 라이브러리만 사용하는 [에셋 감사 도구](../../tools/optimization/livedeco-prefab-audit.py)를 추가했다. 현재 설치 sharedassets4.assets의 프리팹 490 → ToggleVisible 471 → **자기 scrVisualDecoration 490** 연결, ignoreIfDisabled=False를 직접 확인했다([프리팹/해시](gate2-A-run3-prefab.log)). 프리팹은 1개 확인했으며 이것만으로 전달된 2,088개 인스턴스가 모두 같은 연결이었다고 단정하지 않는다. 도구는 Unity v22 직렬화 형식만 지원하고 다른 버전은 거부한다.

디컴파일 ToggleVisible.cs와 [설치 DLL IL](gate2-A-run3-visibility-il.log)을 대조했다. OnBecameVisible/Invisible은 scriptToToggle.enabled만 설정하며 invisible 분기에서 자신의 gameObject.activeSelf를 확인한다. Transform·SpriteRenderer.color·장식 논리 값은 읽거나 쓰지 않는다. [관리자 IL](gate2-A-run3-manager-il.log)의 LateUpdate는 enabled 검사 없이 null이 아닌 항목의 LogicUpdate를 직접 부른다. scrVisualDecoration → scrDecoration → ADOBase → RDTools.RDBaseDll의 메서드 목록에 Update/LateUpdate/OnEnable/OnDisable이 없음을 확인했다([visual](gate2-A-run3-visual-types.log), [decoration](gate2-A-run3-decoration-types.log), [ADOBase](gate2-A-run3-adobase-types.log), [RDBaseDll](gate2-A-run3-base-il.log)). 이 연결에서 enabled 변화로 장식 갱신이 중단되거나 Transform 소비 콜백이 추가 실행되는 관리 경로는 확인되지 않았다.

게임 DLL SHA256은 F692210F9A342F7EED4BE40AAD463286AB40BF046599959823942EA02F4CA3FF로 이전과 같다([해시](gate2-A-run3-game-hash.txt)). 감사는 파일만 읽고 게임 메서드는 실행하지 않았다. 워크트리에 없는 로드맵은 정식 체크아웃 docs/ROADMAP-EXTREME-20261021.md에서, freeze 결과는 wt-freeze/docs/handoff/codex-freeze-20261021-result.md에서 읽었다. freeze 문서는 오프라인 구현 결과여서 새 실게임 절감 수치로 쓰지 않았다.

## 수정

- 새 LiveDecoScripts.Reason은 **정확한 게임 ToggleVisible 타입 + 자기 SpriteRenderer와 같은 Transform + 자기 장식 대상 참조**일 때만 추가 허용한다. 동명 외부 타입·상속 타입·잘못된/누락된 대상·다른 노드는 거부한다. 콜백이나 enabled를 강제로 변경하지 않는다.
- 입장 당시 캐시한 스크립트의 활성 상태와 연결을 소유 중 재검사한다. 캐시된 비활성 외부 스크립트가 켜지거나 ToggleVisible 대상이 바뀌면 기존 Release로 복원한다. **입장 후 임의 외부 AddComponent 전부를 실시간 탐지하는 기능은 아니다.** 기존 Setup/경계 사건 재입장 검사를 유지한다.
- 제외 이유는 foreign-child-script:<전체 타입명>, visibility-node, visibility-target:<타입명 또는 missing>으로 세분화했다. 시작에 renderer_version=3 visibility_policy=bound-self, 정점 검사에 실제 허용된 콜백 수 visibility_admitted를 남긴다. 정점 오차 1e-4 및 마스크·히트박스·반복·특수 블렌드·정렬·텍스처 조건을 유지했다.
- 외부 Harmony 패치 거부 검사에 ToggleVisible을 포함했다. 새 콜백 패치는 설치하지 않는다. 논리 필드·엔진 쓰기 차단·off/종료 복원은 기존 경로를 유지한다. 일반 Player 제외·기본 꺼짐을 유지한다.
- 기존 129단계 생성기는 유지했다. 변경된 코드의 프로파일→3시점 픽셀/복원→ABBA 검증을 준비했고 같은 게임 시험은 재실행하지 않았다. 아틀라스·게이트 3·워커 분할은 추가하지 않았다.

## 오프라인 검증

| 검사 | 결과 | 로그 |
|---|---|---|
| Release DEV / Player / PlayerAuto | 오류 0/0/0, 경고 38/36/38 | gate2-A-run3-build-dev.log / -player.log / -auto.log |
| LoadCheck DEV / Player / PlayerAuto | 276/229/276곳, 틀림 0/0/0 | gate2-A-run3-loadcheck-dev.log / -player.log / -auto.log |
| 생산 계층 / 새 생산 스크립트 판정 | 2,729 / 15 checks 통과 | [geometry](gate2-A-run3-geometry-fixture.log) |
| 구간/첫 멤버 앵커 / Gate 2 판정 | 106,460 / 18 checks 통과 | [fixture](gate2-A-run3-fixture.log) |
| edition / 게임 패치 대상 | 31 / 17 checks 통과 | 같은 로그 |
| MatShare / FrameCompare | 25,401 / 11+15+14+17+13 checks 통과 | gate2-A-run3-matshare-fixture.log / -framecmp-fixture.log |
| Gate 1 / Gate 2 생성기 | 25 / 129단계, UTF-8 CLI 통과 | gate2-A-run3-gate1-steps-fixture.log / -steps-fixture.log |
| 현재 설치 프리팹 | 프리팹 1개, 자기 장식 대상 ToggleVisible 1개 | gate2-A-run3-prefab.log |
| git diff --check | 통과 | 최종 로컬 검사 |

새 fixture는 실제 생산 정책 소스를 연결해 정상 바인딩·자기 장식 비활성 상태·잘못된/누락된 대상·다른 노드·동명 외부 타입·상속 타입·입장 후 대상 변경·캐시된 외부 스크립트 활성화를 검사한다. 관리 어댑터 검사이며 Unity 콜백·렌더링·Mono Harmony 실행·성능 증명이 아니다. LoadCheck 동적 이름 5곳은 런타임 확인 대상이다.

## 자동 시험 인계와 한계

renderer_version=3, visibility_admitted, nested_admitted, vertexcheck max_err/count/excluded, 제외 타입 분포, owned/runs 및 비용을 확인한다. **수정 후 이 값들과 실픽셀·FPS·GC·재시작/사망/맵 재열기/언로드 복원은 미측정**이다. 약 1,200구간은 예상치다. 세 시점 ab_out=0, 복원·기준 상태 안정성, ABBA 켬 평균 FPS ≥ +15%를 모두 만족해야 PASS다. 현재 증거에는 수정 가능한 코드 원인이 있으므로 설계 한계로 확정하지 않았고 성공 조건도 완화하지 않았다.

task-observer v3.5.0 관찰 반환(사용자 범위 스킬/메모리 쓰기 없음): 문제 — 스크립트를 한 제외 이유로 집계하면 표준 프리팹의 보조 컴포넌트와 외부 Transform 소비자를 구별할 수 없다. 개선 — 현재 직렬화 연결과 콜백 IL을 대조한 뒤 정확한 타입·역할·대상 참조로 판정하고 거부 타입을 로그에 남긴다. 원칙 — 증명한 바인딩 범위만 허용하고 실게임 미측정을 구분한다.

---

아래는 보존한 이전 수정 결과다.


# Gate 2 A 자동 반복 2회차 결과 — 2026-10-21

**소유 0개로 중단한 계층 판정과 프로파일 표본 0개의 원인을 수정하고 오프라인 검증을 완료했다. 수정 후 게임 측정은 없으며 Gate 2 PASS를 선언하지 않는다.** 중단된 2회차의 미커밋 수정본을 이어서 검토·보완했다. 현재 HEAD는 `7758ce9`이며 커밋하지 않았다. 게임 실행·설치·푸시와 `.codex/config.toml` 수정도 하지 않았다. 아래 1회차 결과는 이력으로 보존한다.

## 전달된 실패와 원인

장부 [ga-autoloop-ledger.md](ga-autoloop-ledger.md)와 [run1 증거](ga-autoloop-run1-evidence.txt)를 먼저 읽었다. 직전 게임 측정은 다음 단계에서 끝났으며 픽셀/FPS 판정까지 도달하지 않았다.

| 전달 항목 | run1 실측 |
|---|---|
| 소유 / 구간 | 0 / 0 |
| 제외 이유 | child-parent 2,089개, repeat-tile 38개 |
| 설정 비용 프로파일 | enabled=False, samples=0, valid=False |
| 자동 시험 실패 | livedeco on: no eligible images |
| 마지막 판정 | GATE2=ERROR, autotest_aborted |

기존 `GeometryReason`의 `child-parent`는 피벗 부모, child의 직계 부모, renderer와 child의 동일성 조건을 하나로 합쳤다. SpriteRenderer가 논리 오프셋의 대상인 `childTransform`과 같아야 한다는 가정 때문에, 그 사이에 고정 변환이 있는 계층을 처리할 수 없었다. **기존 집계만으로 세 하위 조건별 실패 개수나 실제 계층 깊이를 역산할 수는 없다.** 새 구현은 피벗 부모 제약을 유지하면서 나머지를 실제 계층 합성으로 대체하고 실패 이유를 분리한다. 따라서 수정 후 2,089개가 전부 소유된다고 예측하지 않는다.

디컴파일 `scrDecoration.SetPosition`은 `(pivotOffset + croppedPivotOffset) / trueScaleMultiplier`를 **childTransform.localPosition**에 쓰고, `UpdatePosition`은 pivotTrans 및 parallax·회전·스케일을 갱신한다. `scrDecorationManager.CreateDecoration`은 이미지 프리팹을 관리자 아래 생성하고 scrVisualDecoration을 얻는다. 이 관리 코드는 프리팹 내부 renderer와 child가 같은 객체라고 보장하지 않는다. 설치 게임 IL과 교차확인했다: [장식 IL](gate2-A-run2-decoration-il.log), [시차 IL](gate2-A-run2-parallax-il.log), [관리자 IL](gate2-A-run2-manager-il.log). [DLL 해시](gate2-A-run2-game-hash.txt)는 기존 `F692210F9A342F7EED4BE40AAD463286AB40BF046599959823942EA02F4CA3FF`와 같다. IL 도구는 메타데이터만 읽었고 게임 메서드는 실행하지 않았다.

프로파일은 `DecoAnim.Prof`가 PlayerAuto 기본 상태에서 false인 채 읽히는 것이 직접 원인이었다. 기존 `profilebegin`은 카운터만 저장하고 활성화하지 않아 표본이 생기지 않았다.

## 이어서 완성한 수정

- 새 `LiveDecoGeometry.cs`는 renderer→child 아래 구간, child의 회전·스케일, child→pivot 위 구간을 고정 로컬 변환으로 합성한다. 매 프레임 논리 child XY 오프셋만 올바른 위치에 넣는다. flip은 renderer 로컬 정점에 먼저 적용하며 월드 행렬 역변환은 사용하지 않아 크기 0도 처리한다.
- 평면이며 pivot까지 연결되는 최대 16개 로컬 구간만 지원한다. 끊긴 계층·순환·과도한 깊이·비평면·고정 구간의 런타임 변경은 거부하거나 원본으로 돌린다. 피벗 부모 항등성, 마스크·히트박스·반복·특수 블렌드·외부 스크립트 제외는 유지한다. 소유 진입 전 원본 정점/바운드와 **1e-4** 검사를 그대로 수행한다. `renderer_version=2`, `nested_admitted`로 새 경로 사용 여부를 확인할 수 있다.
- 소유 중 정점은 계속 논리 필드에서 계산하고, 기존 TightLate·setter·ColorFast의 Transform/색 쓰기 차단과 off 복원을 유지한다. 아틀라스·새 셰이더·게이트 3·워커 분할은 추가하지 않았다. 구간별 업로드 비용과 호출 수를 분리하는 기존 `cost`도 유지했다.
- `profilebegin`은 DEV/AutoTest에서만 Prof를 켜고 원래 값을 보관한다. profileend/reset/abort/result/SceneReset/Main.Unload에서 복원한다. samples=0이면 `profile_no_samples`로 최종 ERROR를 남겨 무효한 프로파일을 숨기지 않는다. 일반 Player에서는 Prof의 readonly 속성까지 그대로 유지한다.
- **프로파일 해석 제한:** 기존 Prof 경로는 PairXY/GroupStep을 억제하고 Step 표본을 사용한다. `pairxy_suppressed=1 groupstep_suppressed=1 baseline_path_unchanged=0`을 명시해 이 비율을 최적화된 평상시 경로 전체의 설정 비율로 오해하지 않게 했다. StepPair·OnComplete·setter 이후 엔진 작업도 측정 범위 밖이다. 실제 set_pct는 아직 없으므로 “설정 60% 이상”은 확인되지 않은 가설이다. 프로파일 재생은 픽셀/FPS 창과 분리하며 원래 Prof 값을 복원한다.

## 이번 오프라인 검증

최종 소스에 대해 아래를 실행했다. 빌드 경고는 기존 어셈블리 충돌·deprecated·미사용 필드 등이며 오류는 없다.

| 검사 | 결과 | 로그 |
|---|---|---|
| Release DEV / Player / PlayerAuto | 오류 0/0/0, 경고 38/4/38 | gate2-A-run2-build-dev.log / -player.log / -auto.log |
| LoadCheck DEV / Player / PlayerAuto | 276/229/276곳, 틀림 0/0/0 | gate2-A-run2-loadcheck-dev.log / -player.log / -auto.log |
| 실제 LiveDecoGeometry 소스 연결 fixture | 2,729 checks 통과 | [geometry](gate2-A-run2-geometry-fixture.log) |
| 구간·첫 멤버 앵커 모델 / Gate 2 판정 | 106,460 / 18 checks 통과 | [fixture](gate2-A-run2-fixture.log) |
| edition 메타데이터 | 28 checks 통과: 일반 Player 실험 타입/참조 없음, Prof는 DEV/AutoTest에서만 변경 가능 | 같은 fixture 로그 |
| 설치 게임 Harmony 대상 유일성 | 17개 통과 | 같은 fixture 로그 |
| MatShare / FrameCompare 기존 fixture | 25,401 / 11+15+14+17 checks 통과 | gate2-A-run2-matshare-fixture.log / -framecmp-fixture.log |
| 기존 Gate 1 / Gate 2 생성기 | 25 / 129단계, UTF-8 CLI 통과 | gate2-A-run2-gate1-steps-fixture.log / -steps-fixture.log |
| git diff --check | 통과 | 최종 로컬 검사 |

계층 fixture는 0~여러 개 중간 구간, child 위/아래 변환, 음수·0 스케일, flip, 논리 오프셋 변화, 고정 구간의 위치·회전·크기 변경, 재부모화·파괴·순환, 깊이 16/17 경계를 검사한다. 생산 알고리즘을 연결했지만 수학 어댑터는 System.Numerics이며 실제 Unity 부동소수점·SpriteRenderer·픽셀 검증은 아니다. LoadCheck 동적 이름 5곳과 Mono Harmony 실행도 런타임 확인 대상이다.

## 자동 루프 인계와 현재 한계

생성기 `tools/optimization/livedeco2-steps.py`의 129단계는 유지했다. 수정된 코드로 프로파일 → 262/270/276초 픽셀·복원 → off/on/on/off 262→276초 FPS를 수집하도록 되어 있다. 이번에 같은 게임 시험을 다시 실행하지 않았다. 설계 한계로 확정할 새 수치는 없고, 직전 오류에는 코드상 수정 가능한 원인이 있었다.

다음 자동 시험에서는 `PROFILE enabled=True`, samples>0, `renderer_version=2`, vertexcheck 및 `nested_admitted`, 제외 이유별 개수, owned/runs와 cost를 확인해야 한다. 약 1,200구간은 예상이며 실측으로 쓰지 않는다. 세 시점 `ab_out=0`과 복원 성공, ABBA 평균 FPS +15%를 모두 만족해야 PASS다. **수정 후 소유 수·정점 오차·픽셀·FPS·GC·실제 수명 복원은 모두 미측정**이다. 이 조건들을 완화하거나 성공 로그를 미리 만들지 않았다.

task-observer 관찰은 결과 문서로 반환하며 사용자 범위 스킬·메모리는 수정하지 않았다. 문제: 여러 계층 제약을 하나의 제외 이유로 합치면 집계만으로 실제 실패 조건을 구별할 수 없다. 개선: 실패 조건을 분리하고 새 허용 경로의 진입 수를 함께 남긴다. 원칙: 전달된 실패 수치와 코드상 추론, 수정 후 미측정을 구분한다.

---

아래는 보존한 1회차 결과다.

# Gate 2 A 결과 — 2026-10-21

변형 A의 개발 전용·기본 꺼짐 프로토타입과 자동 검증 생성기를 구현했다. **실게임 Gate 2는 미측정이며 PASS를 선언하지 않는다.** 게임 실행·설치·커밋·푸시는 하지 않았다. 기준 HEAD는 `5bf7413`이다. 작업 중 공통 인계에 추가된 논리 필드 전용 갱신 지시도 반영했다. 게이트 3나 워커 분할은 만들지 않았다.

## 구현

- `livedeco on|off|state`는 `LiveDecoRenderer.cs`를 호출한다. 기존 `census`와 Gate 1 정책은 유지했다. 다른 소유 렌더러·장식 엔진과의 동시 실행, SRP, 원근/비-Z 정렬, 비항등 관리자 부모는 거부한다. 시작 로그에 `currentRenderPipeline=null`을 남긴다.
- 모든 live 렌더러/재질 슬롯을 장벽으로 포함한다. 정렬된 행에서 소유 장식이 연속이고 정렬 버킷·Unity layer·재질 서명·실제 텍스처가 같을 때만 같은 구간이다. 떨어진 같은 텍스처는 합치지 않는다. **재질 동률은 소유끼리라도 검증되지 않은 순서를 만들지 않고 제외한다.** 첫 멤버의 실제 재질 인스턴스, sortingLayerID, sortingOrder, Z를 빌린 씬 MeshRenderer를 쓴다. 재질·텍스처 복제와 UV 재배치는 없다.
- 디컴파일 `scrDecoration.UpdatePosition/SetPosition/SetRotation/SetScale/camScaleMultiplier`, `scrParallax.SetTrans`와 설치 DLL의 IL을 대조했다. 논리 위치·피벗/크롭 오프셋·PPU가 반영된 실제 sprite 정점·child 회전/스케일·시차·카메라 회전/배율 잠금·화면 고정·축 고정·flip을 계산한다. 바닥 부착·행성 따라가기·비평면 자식·반복/타일·비쿼드·특수 블렌드·마스크·히트박스·활성 종속 렌더러/외부 자식 스크립트는 제외한다.
- 소유 진입 전 원본을 갱신한 뒤 계산 정점 4개와 `TransformPoint` 정점 및 SpriteRenderer.bounds의 min/max를 비교한다. `[livedeco] vertexcheck max_err=... count=... excluded=... threshold=0.0001`을 남기고, 비유한 값이나 1e-4 초과는 제외한다. 이 검사는 진입 시 검사이며 모든 미래 프레임의 동일성 증명이 아니다.
- `TightLate.Process`의 직통 Transform 경로와 Harmony `UpdatePosition`을 모두 막는다. `SetPosition`은 논리 위치/피벗과 screen clamp 필드만, 회전/크기는 논리 필드만 갱신한다. 색/불투명도 setter·ApplyColor·`ColorFast.TrySet`도 소유 중 엔진 색 쓰기를 생략하고 `rendererColor`까지 갱신한다. `DecoAnim.Apply/OnUpdate/OnComplete`의 다음 효과 시작값을 유지한다. 알파가 0 이하로 넘어가면 원본에 돌려 숨김/깨우기 bookkeeping을 처리한다.
- 색은 `color.WithAlpha(color.a * opacity * 1f)`의 기존 순서로 보존하고 Color32 정점에 넣는다. 원래 `Sprites/Tile` 셰이더를 그대로 빌려 텍스처 샘플·색 곱·프리멀티플라이를 셰이더에서 한 번 수행한다. 반복은 1인 경우만 소유한다. `Sprites/Default` 등 센서스 기본형 밖 셰이더는 제외한다. Color32 양자화, Quaternion 정규화 차이, SpriteRenderer와 메시의 내부 처리 차이는 실픽셀 검증 대상이다.
- 위치/Color32/UV를 인터리브한 공용 상주 구조체 배열 하나에 모든 구간을 채운 뒤 구간별 source offset으로 `SetVertexBufferData` 및 지정된 3개 플래그를 쓴다. 인덱스는 원본 sprite 인덱스와 구간 순서를 유지한다. 큰 고정 bounds를 쓰고 범위 밖 정점은 오류로 중단한다. 같은 Z, 활성 상태, forceRenderingOff를 매 프레임 다시 쓰지 않는다.
- 행의 진입/이탈/키 변경은 이진 삽입/제거, 구간은 기존 첫 멤버·멤버 버전 대조로 **바뀐 메시만 교체**한다. `transition`에 생성/재사용/폐기 수와 시간을 기록한다. 순서 장벽을 추적하는 캐시된 전체 렌더러 스캔과 구간 설명의 선형 순회는 남는다. 장식 Setup/경계 변경 시 레지스트리를 보충한다. 임의 외부 코드가 별도로 생성하는 비장식 렌더러를 전부 실시간 발견한다는 보장은 없다.
- `cost`는 최대 120프레임 묶음의 **프레임당 평균** 정점 계산/업로드/재구성/스캔 ms, upload_calls를 분리한다. 약 1,200구간이라는 예상치를 실측으로 쓰지 않는다. 메인 스레드 allocated_bytes, steady_allocated_bytes/steady_samples, 힙 증감을 남긴다. 비용·정점 로그 자체의 할당은 카운터 밖이고 전환 할당은 따로 포함된다. **실게임 GC=0은 아직 확인하지 못했다.**
- off, AutoTest 종료/오류, SceneReset, Main.Unload, 루트 파괴에서 원본 forceRenderingOff·현재 논리 상태의 child Transform/위치/색을 복원하고 패치·메시·오브젝트를 정리한다. 빌린 재질/텍스처는 파괴하지 않는다. 복원 오류는 최종 ERROR에 반영한다. 실제 재시작/사망/맵 재열기/언로드의 누수 검사는 미실행이다.

## Transform 소비자 감사

게임 DLL SHA256은 `F692210F9A342F7EED4BE40AAD463286AB40BF046599959823942EA02F4CA3FF`이다. [해시](gate2-A-game-hash.txt), [장식 IL](gate2-A-decoration-il.log), [시차 IL](gate2-A-parallax-il.log), [읽기 IL](gate2-A-readers-il.log), [쓰기 IL](gate2-A-guards-il.log)을 남겼다. IL 도구는 메타데이터만 읽었고 게임 메서드를 실행하지 않았다.

직접 Transform 필드/크기/시차 위치 관련 읽기 참조 16행을 수집했다. `GetDecorationWorldSize` 호출자는 gizmo LateUpdate/Drag 2곳, `GetPositionWithParallax` 호출자는 gizmo LateUpdate/DragStart/Drag 3곳이다. gizmo LateUpdate는 `isEditingLevel`에서만 실행한다. 활성 선택/히트박스 테두리, 히트박스, 마스크, 바닥/부모 의존 장식은 소유에서 제외한다. `SpriteAlphaMask.maskSize`와 `SpriteAlphaMaskUtils.ApplyAlphaMaskToRT`/closure는 마스크 Transform을 읽으므로 마스크 및 마스킹된 이미지를 제외한다. 관리자 테두리/히트박스 크기 계산도 디컴파일에서 확인했다.

`ffxMoveDecorationsPlus`의 시작값은 pivotPosVec, pivotOffsetVec, rotAngle, scaleVec, color, opacity 등 논리 상태다. 해당 필드는 계속 갱신한다. 텍스처 전체 크기를 읽는 함수는 텍스처를 바꾸지 않아 유지된다. 이 감사는 설치 게임 관리 코드의 직접 참조를 조사한 것이며 네이티브 내부·반사·임의 외부 모드의 alias 소비자 전체를 증명하지 않는다.

## ProfApply / ProfSet와 자동 검증

**설정 비중은 미측정이다.** 현재 저장 근거에서 새 구간의 ProfApply/ProfSet 수치가 없어 Gemini의 “60% 이상”을 결과로 쓰지 않았다. 생성기의 첫 별도 재생에서 262→276초 `livedeco2 profilebegin/profileend`로 누적 카운터 차이를 구한다. `[livedeco2] PROFILE key=... apply_us=... set_us=...` 및 표본 가중 `set_pct`를 출력한다. 이 카운터는 Step의 Apply/OnUpdate 표본이며 StepPair·OnComplete·setter 이후 엔진 처리 전체를 포함하지 않는다. Prof가 꺼져 있으면 samples=0/valid=False로 표시한다. 프로파일 재생은 픽셀/FPS 측정 창 밖이다.

```powershell
py -3 tools/optimization/livedeco2-steps.py --chart '<Arche FULL.adofai 절대 경로>' --output x.json
```

이 명령은 JSON만 만든다. 생성기 [livedeco2-steps.py](../../tools/optimization/livedeco2-steps.py)는 총 129단계다. 262/270/276초 각각 독립 seek → pause → 잡음 바닥 → A → on → B → off → A2를 캡처한다. 중앙 가로/세로 각 20% 제외 영역 밖 픽셀 수·최대 채널 차이·상위 영역 좌표·복원·상태 안정성을 `PIXEL`에 남긴다. 1/255 차이도 PIXEL_NEAR로만 보고한다.

그 뒤 같은 게임에서 네 번 독립 seek, 252초 모드 설정, 262초 측정 시작, 276초 측정 종료로 **off/on/on/off** 순서를 지킨다. pause 없이 fuse begin/end와 FPS·ms·실제 시작/끝 곡 시각·프레임 수를 기록한다. 두 on FPS 평균이 두 off 평균보다 15% 이상이고 세 픽셀 표본이 정확히 0일 때만 PASS다. 누락/중복/순서 오류·불안정한 기준/복원 실패·창 중 렌더러 중단은 ERROR다. 정상 결과 및 AutoTest 오류 종료 모두 끔으로 끝난다.

| 실게임 항목 | 결과 |
|---|---|
| vertexcheck 최대 오차·소유 수·구간 수 | 미측정 |
| 262/270/276초 ab_out / aa2_out / maxdiff | 미측정 |
| ABBA 네 창 FPS / 향상률 | 미측정 |
| 정점/업로드/구간 재구성/스캔 ms 및 GC | 미측정 |
| ProfApply/ProfSet 설정 비중 | 미측정 |
| Gate 2 | 미측정, PASS 아님 |

## 오프라인 검증

| 검사 | 결과 | 근거 |
|---|---|---|
| Release DEV / Player / PlayerAuto 빌드 | 오류 0 / 0 / 0 | gate2-A-build-dev.log / gate2-A-build-player.log / gate2-A-build-auto.log |
| LoadCheck DEV / Player / PlayerAuto | 276 / 229 / 276곳, 틀림 0 / 0 / 0 | gate2-A-loadcheck-*.log |
| LiveDeco 구간·첫 멤버 앵커 모델 | 106,460 checks 통과 | gate2-A-fixture.log |
| Gate 2 판정 경계 fixture | 18 checks 통과 | 같은 로그 |
| edition 메타데이터 | 22 checks 통과, 일반 Player 실험 타입/참조 없음 | 같은 로그 |
| 설치 게임 패치 대상 유일성 | 17개 통과 | 같은 로그 |
| MatShare 기존 fixture | 25,401 checks, errors=0 | gate2-A-matshare-fixture.log |
| FrameCompare 기존 fixture | 11+15+14+17 checks 통과 | gate2-A-framecmp-fixture.log |
| Gate 1 기존 steps / 새 Gate 2 steps | 25단계 / 129단계, UTF-8 CLI 포함 통과 | gate2-A-gate1-steps-fixture.log / gate2-A-steps-fixture.log |

LoadCheck의 동적 이름 5곳과 Harmony의 실제 Mono 패치 실행은 런타임 확인 대상이다. 모델 fixture·메타데이터 검사로 Unity 렌더링·부동소수점·픽셀·성능·누수 검증을 대체하지 않는다. 기존 deprecated/미사용/어셈블리 참조 경고는 남는다. `.codex/config.toml`은 수정하지 않았다. 공통 인계 파일에 외부에서 추가된 지시는 보존했다.

## 작업 관찰 반환

task-observer v3.5.0의 사용자 범위 헤더와 원칙을 읽었다. 저장소의 스킬·메모리는 수정하지 않고 여기로 반환한다. 문제: 일반 setter Harmony 패치만으로는 TightLate/ColorFast의 직통 엔진 쓰기가 남는다. 개선: 논리 setter와 직통 경로를 함께 추적하고 렌더러 소유권으로 같은 차단 조건을 적용한다. 원칙: 우회 경로를 포함해 엔진 쓰기의 실제 종착점을 감사한다. steps CLI의 임시 파일 검증은 제한된 외부 Temp 대신 워크스페이스의 고유 파일로 실행하고 즉시 지운다.
