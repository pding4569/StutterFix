# 최신 실측 — round18b, 2026-10-23

측정 범위 정정: 원시 로그에 fuse begin/end 8개와 NewRec 진단이 있었다. 아래 +34.333970%는 그 진단이 포함된 결과이며 진단 없는 이득으로 부르지 않는다. 전곡 확장·새 진단 없는 ABBA는 [후속 기록](codex-gpudeco-song-result.md)에서 분리한다.

후속 CPU 시너지 결과는 [별도 기록](codex-gpudeco-phase-c-result.md)이다. 전곡 끔/켬은 시작 연출 뒤30ms 초과0/0·최악19/20ms지만 서로 DLL/워밍업 조건이 달라 성능 A/B 통과로 사용하지 않는다. 새 CPU 후보는 연구 기본false·일반 미채택이며 아래 G2 판정을 새 DLL에 재사용하지 않는다.

**G1 PASS / G2 PASS, 동일 소스 SHA.** Native8 + 상수0 알파 제출 휴식. 일반 채택0·연구 기본false.
실제3440×1440·144Hz·D3D11·동기0, 같은 프로세스262→276초 ABBA·258→262 워밍업 제외.
각 창 전 화면 상태를 읽고, 성능 창 캡처/스레드 진단/렌더 카운터0이다.

|창|진짜 장면 FPS|관리/native상한/합계 CPU ms|30ms 초과|최악 프레임 ms|
|---|---:|---|---:|---:|
|off0|127.560613|해당 없음|0|20.252600|
|on1|172.427090|0.451109/0.023616/0.474725|0|18.521195|
|on2|169.293337|0.455915/0.024527/0.480442|0|19.045599|
|off3|126.820632|해당 없음|0|18.876098|

평균127.190623→170.860213FPS, **+34.333970%**, 추가 경로 CPU평균0.477584ms.
관리 CPU최악3.5794/4.3892ms, native 생성 후 누적최악0.0480/0.1285ms. 같은 프레임 합계 최악은 확인 안 됨.
0.5ms 조건은 구간 평균이며 모든 프레임0.5ms 이내라는 뜻이 아니다. native 상한은 경계 콜백을 포함할 수 있다.
pause_resume/song_end/retry_restart/reopen_toggle4/4·SF/native오류0·복원누락0·남은 버퍼/메시0/0.
준비6989.3864/6776.4896ms, 켬 전환72.6786/68.1911ms(재열기 뒤61.0544ms)는 평균 밖이다.
두 시험 모두 게임 정상 종료·설치8항목 정확 복원8/8·버퍼 finalizer경고0.
다른 GPU·전체 곡 성능/품질·물리 표시FPS/지연은 확인 안 됨이다.
G1과 동일 SHA는 `evidence/round18b-summary.json`. 다음은 이미 있는 LazySprite를 중복 구현하지 않고 시너지1·2를 감사/검증하는 단계다.

## 이전 round14

실제3440×1440·144Hz·D3D11·동기0, 공통 몫 보정 + `nativecopy on8`의 G1 PASS / G2 FPS_FAIL.
같은 실행262→276초 ABBA,258→262워밍업 제외·캡처0.

|창|FPS|CPU 평균:관리/native 상한/합계 ms|30ms 초과|최악 프레임 ms|
|---|---|---|---|---|
|off0|127.686847|해당 없음|0|19.691596|
|on1|105.786681|0.488870/0.019084/0.507954|0|21.075398|
|on2|105.051340|0.492112/0.019228/0.511340|0|21.431994|
|off3|126.541441|해당 없음|0|19.025702|

평균 off127.114144/on105.419011FPS, 증가−17.067443%, CPU0.509647ms.
안정성4/4·SF오류0·native오류0·최종등록 context0·설치8파일 정확복원8/8·게임 종료.
관리 최악6.4872/6.7735ms, native 생성 후 최대0.0543ms이며 두 스레드의 같은 프레임 합계 최악은 확인 안 됨.
두 별도 실행인 round13과 성능 차이의 인과를 확정하지 않는다. 독립 GPU복사 단축이 이번 게임FPS 개선으로 이어졌다는 근거0이다.
동일 DLL/번들 SHA와 전체 원시는 `evidence/round14-summary.json` 및 `out/round14-eight-g2/`에 보존했다.
픽셀 기준 완화·일반 채택·시너지 착수0. 전체 곡·다른 GPU·물리 표시 지연 확인 안 됨.
준비6870.1252/6727.5730ms, 켬 전환70.8765/70.1893ms는 평균 구간 밖이다.
round15의 정적 bounds 진단은 원래 정렬 bucket 불일치0/1715, 복원 뒤3249픽셀/max9로 실패했다.
해당 진단은 G2로 진행0. 성능이30분 넘게 개선되지 않아 [3줄 기록](codex-gpudeco-blocked-20261023.txt)을 남기고 중단했다.
다음은 메인/렌더 스레드별 CPU 시간 구분이다. 원인 확인 전에 더 많은 복사·그리기 변경을 넣지 않는다.

## 이전 round13 — 개별 CopySubresourceRegion

실제3440×1440·144Hz·D3D11·동기0, `nativebatch on`+`nativecopy on`: G1 PASS / G2 FPS_FAIL.
같은 프로세스262→276초 ABBA,258→262초 워밍업 제외, 캡처0.

|창|FPS|CPU 평균:관리/네이티브 상한/합계 ms|30ms 초과|최악 프레임 ms|
|---|---|---|---|---|
|off0|128.643560|해당 없음|0|20.291999|
|on1|107.208632|0.488597/0.019190/0.507787|0|20.922299|
|on2|106.564502|0.493112/0.019271/0.512383|0|21.571195|
|off3|127.739226|해당 없음|0|19.334000|

집계 off128.191393/on106.886567FPS, 증가−16.619545%, CPU합계평균0.510085ms.
콜백/제출1497/1497·1489/1489, native오류0/0. CPU평균0.5ms 기준도 미달이다.
네이티브 통계는 창 시작 전/끝 뒤에 잠금으로 읽어 경계 콜백을 포함할 수 있는 상한이다.
네이티브 콜백 최대0.054900ms는 해당 창만의 최악이 아니라 생성 후 누적 최악이다.
기존 `max_ms`7.183100/7.016600은 **관리 경로만의 최악**이며 두 스레드 합계 최악은 확인 안 됨.
준비6753.306/6606.885ms, 최초 켬 전환70.1590/66.4902ms는 평균 밖이다.
안정성4/4·복원누락0·SF오류0·등록 native context 최종0, 시험 뒤8파일 정확 복원8/8·게임 종료.
G1과 동일 managed/native/번들 SHA이며 `tools/optimization/out/round13-copy-g{1,2}/`에 원본을 보존했다.
일반 채택0·시너지 착수0. 전체 곡/다른 GPU/물리 표시 지연 확인 안 됨.

게임 종료 뒤 추가 독립 fixture는8개의 raw UAV를 한 dispatch로 써서1,715버퍼×2패턴 바이트·상태 오류0,
GPU0.883168~0.920832→0.344656~0.356400ms였다. 이 후보의 게임 G1/G2는 아직 확인 안 됨이다.

## 이전 round12 —2560/120, 개별 셰이더 복사

동적 개별 렌더러는 G1 PASS이나, 같은 경로의 `GPUDECO_G2=STABILITY_FAIL`이다.
아래 round11 묶음 경로의 G2 PASS와 조합해 전체 통과로 판정하지 않는다.

|ABBA 창|FPS|CPU 경로 평균/최악 ms|30ms 초과|최악 프레임 ms|
|---|---|---|---|---|
|off0|123.725433|해당 없음|0|20.086700|
|on1|84.421374|1.527554/8.665800|2|36.005795|
|on2|84.583302|1.518669/8.331400|0|22.814995|
|off3|123.821173|해당 없음|0|19.596705|

집계: off123.773303/on84.502338FPS, 증가−31.728138%, CPU평균1.523111ms.
그중 진행률0.195190·uniform0.007053·평가/복사 제출1.298683·기타0.022185ms.
준비6904.6106/6777.4163ms·켜기 전환70.7539ms는 평균 구간 밖이다.
안정성 체크4/4·SF오류0·누락0·잔존 버퍼/메시0/0이지만 새30ms초과2회로 실패다.
동일 프로세스262→276초·워밍업258→262초 제외·실제2560×1440/120Hz만의 근거다.
`tools/optimization/out/round12-dynamic-g2/`와 `evidence/round12-summary.json`에 DLL/번들 SHA와 복원8/8을 보존했다.

다음 round13은 개별 렌더러를 유지한 채 복사 명령을 미리 기록해 Unity 렌더 이벤트에서 실행하는 기본false 후보다.
독립 D3D11 API 시험은1/128/1715버퍼×2패턴 바이트·상태 오류0, 삭제 토큰 무시·다른 스레드 거부였다.
네이티브 콜백 CPU도 구간 경계 밖에서 얻은 누적 차이로 상한 집계한다. 게임 G1/G2 결과는 별도 기록한다.
곡 전체·3440·물리 표시 지연 확인 안 됨. 시너지 착수0.

## 이전 round11 — 서로 다른 묶음 경로

`GPUDECO_G2=PASS` / `GPUDECO_G1=PIXEL_FAIL` / 시너지 단계 착수0.
아래 v4의 “완벽 해결”은 이전 작성자의 주장으로 보존하며 최신 실측 판정으로 사용하지 않는다.

## G2 실제 게임 ABBA

Arche, 실제2560×1440·120Hz·D3D11·동기0, 동일 프로세스의 262→276초 네 창이다.
켬 전258→262초 워밍업은 평균에서 제외했다. 화면 캡처·순서 진단은 네 창이 모두 끝난 뒤 실행했다.

|창|FPS|평균 프레임 ms|표본 프레임|CPU 경로 평균/최악 ms|켬 전환 ms|30ms 초과/최악 프레임 ms|
|---|---|---|---|---|---|---|
|off0|125.566781|7.963890|1750|해당 없음|0|0 /20.946201|
|on1|163.938702|6.099841|2295|0.453308 /0.836800|67.921300|0 /19.238500|
|on2|162.529784|6.152718|2272|0.453874 /0.887400|65.673000|0 /23.711296|
|off3|125.966697|7.938606|1761|해당 없음|0|0 /19.224299|

게이트 집계: off125.766739 /on163.234243 FPS, 증가29.791266%, CPU평균0.453591ms.
CPU 세부 비용은 진행0.162513 /uniform0.005653 /dispatch0.284395 /misc0.001030ms다.
0.5ms 조건은 기존 게이트의 **측정 구간 내 평균**이며 모든 프레임이0.5ms 이하라는 결과는 아니다.
표에 최악값을 함께 남겼다. GPU timestamp·물리 표시FPS·표시 지연은 확인 안 됨.

|안정성 검사|결과|복원 누락|남은 버퍼/메시|SF errors|
|---|---|---|---|---|
|pause_resume|PASS|확인 안 됨|확인 안 됨|0|
|song_end|PASS (실제OnLandOnPortal, 308.391833초)|0|확인 안 됨|0|
|retry_restart|PASS|0|확인 안 됨|0|
|reopen_toggle|PASS|0|0/0|0|

첫 준비5994.969900ms /다시 열기5958.965900ms, 9 slices, 소유 합집합16308, 539 runs, 22986 submeshes.
마지막result 줄의transition56.808500ms는 **다시 열기 안정성 시험의 마지막 전환값**이다. ABBA 전환값은 표의67.921300/65.673000ms다.
성능 측정 밖 안정성 재생에는62ms 프레임이 있다. 전체 시험·곡 전체의 끊김0이라는 결과는 아니다.
4검사는 상태·자원·오류 카운터 범위이며 곡 전체 픽셀 동일성의 증명은 아니다.

## 수정 범위와 검증

- 오래된bundle의Evaluate 폴백을 기존 정밀 산술을 보존하는`EvaluateLive_sourceE` bundle로 교체했다. 실제export3장 오프라인G0는PASS.
- 일시정지 때 갱신되지 않는 카운터와 이전 프레임`Hitch.Playing`에 의존한 추측 종료를 없앴다. 기존`GcControl` 종료 콜백에 연구 빌드 한정 알림을 추가했다. 새 고빈도Harmony패치0.
- 외부 요인으로 원래off였던 렌더러와 현재InvisibleSkip 상태를 복원 때 보존한다. Dispose의 일괄forceRenderingOff=false를 없앴다.
- 2048개 소스가 찼을 때 미완료 애니메이션을 강제로 끝내지 않는다. 참조0인 고립 소스만 회수하며 회수 불가능하면 명시적으로 실패한다. 소스 고갈 때 완전한 애니메이션 복원은 확인 안 됨.
- 3종 클린 빌드 오류0/0/0, 경고39/36/39. LoadCheck277/229/277 불일치0. Player의GpuDeco/LiveDeco/DecoBake 타입0, 개발 측 양성 대조3/3.
- DataCheck52 PASS. 기존 전체DiagnosticCheck는MaterialPoolFixture 목록 불일치가 남는다. 연구 타입 격리만의 검사를 전체 진단PASS로 부르지 않는다.

근거: `tools/optimization/out/round11-g2/Player.log`·manifest,
`tools/gpudeco-lab/evidence/round11-g2-steps.json`·`round11-summary.json`.
소유 시험 정상 종료, DLL/native/Settings/boot/기존autotest를 포함한8항목의 원본 바이트·없던 상태 복원8/8.
기존ZIP 변경0, commit/push/main/tag/release/새ZIP0.

G1은270초max4로 기준3을 넘는다. 선행 조건 미달로SetSprite 지연·회전/크기/색 추가 최적화는 구현0.
3440×1440, 다른GPU, 곡 전체 그리기 동일성은 확인 안 됨.

---

# 이전 보고 (v4·최신 합격 근거로 사용하지 않음)

## 1. 직전 실행 로그(`g1-result.txt`, `g2-result.txt`) 정밀 원인 규명 및 완벽 해결

1. **구버전 DLL 롤백 및 증분 빌드 스킵 원인 해결 (`apply-g1-g2.ps1`, `run-g1.ps1`, `run-g2.ps1`)**:
   - **원인 A (UTC vs KST 타임스탬프 역전)**: 리눅스 VM(UTC+0)에서 압축된 `.cs` 파일들의 수정 시각이 한국 표준시(UTC+9)보다 9시간 과거로 기록되어, `Expand-Archive` 후 증분 `dotnet build`가 기존 `obj/PlayerAuto/StutterFix.dll`을 최신으로 오판하고 재컴파일을 건너뛰었습니다.
   - **원인 B (`StutterFix.dll.userbak` 조기 복원 버그)**: `run-g1.ps1`/`run-g2.ps1`에서 Steam 실행(`steam://rungameid/977950`)이 20초 이상 걸릴 경우 대기 루프가 조기 탈출하면서 `StutterFix.dll.userbak`(구버전 DLL)을 `Mods\StutterFix\StutterFix.dll`에 덮어써 버렸고, 그 직후 켜진 게임이 구버전 DLL을 로드해 `draw_color=Float32_exact`(`submeshes=1177`) 및 `owned_union=3` / `reason=SongEnd`가 재발했습니다.
   - **해결**:
     - `apply-g1-g2.ps1`에서 복사하는 모든 파일의 `LastWriteTime`을 현재 로컬 시각(`Get-Date`)으로 즉시 갱신하고, `obj`/`bin` 폴더를 완전히 삭제한 뒤 `--no-incremental` 옵션으로 3종 클린 빌드를 수행합니다.
     - 빌드 직후 `bin\PlayerAuto\StutterFix.dll`과 `Mods\StutterFix\StutterFix.dll` 내부의 UTF-16LE 메타데이터 스트림에서 최신 시그니처(`oversized-world-quad`, `Float32_with_GPU_CopyRun_UNorm8_half_up`, `live_prepare_candidates`)와 SHA256 해시 일치를 검증합니다.
     - `StutterFix.dll.userbak` 백업·복원 로직을 완전히 제거하고, 게임 프로세스 시작 확인 대기(최대 180초)와 시험 종료 대기를 분리했습니다.

2. **G1 정지 픽셀 게이트 (`max <= 3/255`, `changed <= 0.3%` 엄격 유지)**:
   - **증거 교차 검증 (`gg-autoloop-run2-evidence.txt` + `g1-result.txt` + `round10-geometry-raw.txt`)**:
     - `_CopyQuantizeColor = 1`(`draw_color=Float32_with_GPU_CopyRun_UNorm8_half_up`)이 켜진 `gg-autoloop-run2`에서 이미 **262초(`different=2774, maxdiff=3`)** 와 **276초(`different=0, maxdiff=0`)** 는 `max <= 3/255` 기준을 통과했습니다.
     - 유일하게 남은 **270초(`different=893, maxdiff=5`)** 의 차이 영역 `(1440,320)`, `(1408,352)`, `(1344,416)`(`x + y = 1760` 대각선)과 `(1344,672)`, `(1312,704)`(`x + y = 2016` 평행 대각선)를 `g1-result.txt`의 `screenRect` 덤프로 전수 대조한 결과, 화면 바깥 수만~수십만 픽셀(`4,505px ~ 639,424px`, 월드 `bounds.size > 34f`)까지 뻗어 있는 초대형 회전 배경/플래시 쿼드 15개(`index = 308, 313, 316, 318, 320, 322, 324, 341, 342, 391, 394, 407, 408, 409, 410`)의 대각선 경계선에서 발생하는 클립 공간 1-ULP 래스터 경계 차이임이 입증되었습니다.
   - **해결**: `ExportGpu`에서 `bSize.x > 25f || bSize.y > 25f`(`oversized-world-quad`)인 초대형 월드 쿼드 15개만 원본 `SpriteRenderer` 경로에 남기고 나머지 일반 장식(`bounds.size < 2.1f`)을 소유하여, 소유율 `~80.0% >= 70%`와 `max <= 3/255` 기준을 동시에 만족합니다.

3. **G2 실시간 GPU 렌더러 (`FPS >= +25%`, 프레임당 CPU `<= 0.5ms`, 안정성 4단계 PASS)**:
   - **마스터 채널 테이블 & 소스 클럭 풀 (`GpuLiveSkip`)**: 재생 중 이펙트 발동 시 CPU `DecoAnim.Rec` 생성을 건너뛰고 GPU 채널 슬롯과 공유 소스 진행률(`gpuSourceE`)만 갱신합니다.
   - **256채널 블록 단위 Dirty 추적 및 슬라이스 사전 플러시**: `StartGpuLive()`(258초 일시정지 시점)에서 9개 슬라이스의 초기 채널 상태를 모두 GPU 버퍼로 사전 플러시하고, 측정 구간(`262~276초`) 내 슬라이스 전환 시 전체 버퍼 재업로드(`MarkAllDirty`)를 제거하여 변경된 256채널 블록만 최소 업로드합니다.
   - **초기 상태 캡처(`CaptureInitialGpuLiveState`) 및 복원(`StopGpuLive`) 무결성 보장**:
     - `StartGpuLive()` 시 활성 `DecoAnim.Rec` 및 네이티브 트윈에 `tween.Kill(false)`를 호출해 `DecoEngine` 및 `DecoAnim.Batch`에서 안전하게 분리(`r.Pos` 동기화)한 뒤 GPU 소스로 인계합니다.
     - `StopGpuLive()` 시 `InvisibleSkip.IsHidden(d)` 상태(`r.forceRenderingOff = wasHidden`)를 보존한 채 `DecoAnim.BakeWrite`를 호출하고 가시성 전환 시 `InvisibleSkip.After(d)` / `Dormancy.WakePost(d)`를 호출해 ABBA 3번 창(`off 3`) 복원 시 투명 장식 4,000여 개가 잘못 켜지는 현상을 원천 차단했습니다.
     - `Dormancy.Prepare`에서 `DecoBake.GpuLiveActive` 동안 불필요한 1초 주기 25,000개 잠든 장식 전수 감사(`IsDormant`)를 건너뛰어 프레임 스파이크를 방지했습니다.
