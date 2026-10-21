# gpudeco-1 최신 결과 — codex-al-gg-5 (2026-10-22)

**Unity 6000.3.10f1 직접 실행·D3D11·기존 AssetBundle 방식을 유지했다. 이번 변경은 소스별 실제 `Rec.Pos`를 받는 export v2와 독립 랩 평가다. 합성 검증은 통과했지만 실제 v2 export가 없어 새 방식의 게임 G0/G1은 미측정이다. 게임 연결·새 번들 제작은 진행하지 않았다.**

## 원인과 이번 선택

먼저 장부와 `gg-autoloop-run4-evidence.txt`를 읽었다. 직전 시험의 첫 export는 184257.5118ms, bake worker는 511.2941ms였고 3파일을 생성했다. 따라서 대기 순서를 되돌리거나 준비 제한을 늘리지 않았다. 최신 실제 입력은 `exports/run-bd81aca8d8c149fa963add1c4d6ba68d/`의 v1 파일이다. 262초 소유1730/awake2134, 270초1875/2268, 276초17/24를 현재 데이터 리더로 확인했다. **아래 과거 절의 81.50/65.62px는 이전 입력의 측정이며 이 최신 입력을 새로 계산한 값이 아니다.**

`scrVfxPlus.Update`는 실제 Update에서 효과를 시작하고, `ffxMoveDecorationsPlus.StartEffect`는 상대 목표의 기준을 읽고 이전 트윈을 `Kill(true)`한 뒤 새 시작값을 정한다. 현재 `DecoAnim.Step`은 `float Pos + dt`를 저장한다. 곡 시각 하나로는 효과별 진행과 교체 시작값을 함께 복원할 수 없다. 디컴파일본을 읽고 설치 DLL의 `StartEffect` 호출을 [IL 로그](../../tools/gpudeco-lab/evidence/round6-move-il.txt)로 교차확인했다. 전역 시계 201후보 탐색은 다시 하지 않았다.

이번에는 **현재 채널의 실제 Rec가 구간을 고르고 그 소스의 진행값이 이징 입력이 된다.** 곡 시각으로 먼저 구간을 고르면 경계에서 아직 시작하지 않은 다음 효과를 선택할 수 있고, 교체 후 Start/Target도 다를 수 있기 때문이다. 실제 Rec가 없는 채널은 기존 이진 탐색 표를 사용하고 폴백 수를 남긴다. 이는 인계의 소스 앵커링을 현재 효과 식별까지 확장한 선택이다.

## 구현 범위

- 기존 `FastMove.Run` 호출 범위에서만 소스 ID·실제 트리거 프레임·곡 시각을 Rec에 기록한다. 새 Harmony 패치·Transform/색 쓰기 훅·프레임당 장식 순회는 없다. 관찰은 `gpudeco prepare`에서 켜고 `off`에서 끈다. 세대와 capture epoch가 다른 기록은 거부한다.
- export 때만 각 장식의 현재 `eventTweens`를 읽는다. 실행 중인 Rec의 실제 float Pos·Start/Target·duration·ease를 내보낸다. 같은 `(source ID, start frame)`의 진행값이 다르면 평균내지 않고 파일 생성 전에 오류로 중단한다. 완료 Rec는 원본 OnComplete/scale 플러그인 산술의 끝값을 보존한다. 새 기록이 없는 native·direct·미시작 채널은 표 폴백이며 실제 값을 복사해 정답을 만드는 보정은 없다.
- v2에는 소스 진행값 배열, 채널별 현재 구간, 표의 Source/Replacement, 최근 최대256개의 실제 프레임 간격과 동일 프레임의 원본 tween dt 평균이 들어간다. 프레임당 새 관찰은 두 float와 합계의 고정 크기 링 갱신뿐이다. export 전체 순회는 진단 스냅샷에서만 실행한다.
- GPU는 `progress[clock]`로 구간을 평가하고 Raw 정점을 쓴다. 소스 진행값은 사전 할당 배열에서 한 번 `SetData`한다. `Actual`·`World`·렌더러 정답 색은 GPU 입력 버퍼에 넣지 않는다. 게임의 소유·원본 평가 생략·렌더러 연결은 실제 G0 전이라 계속 차단한다.
- G0 로그는 채널별 최대/평균·px·국소 `값 오차/속도` ms를 전후로 기록한다. 빠른 채널의 소스별 최악 위상은 관찰 평균 1프레임 이내가 99% 이상이어야 한다. 1프레임의 예상 이동이 0.01px/색1/255 이내인 채널과 종료/폴백 채널은 절대 오차 기준을 유지한다. 실제 필드→Sprite 및 GPU 필드→GPU 정점 식도 별도로 0.01px를 검사한다. 국소 시간 환산은 실제 시작 이력이나 비단조 이징의 전역 역함수 증명이 아니다.
- v1은 읽되 v2로 다시 쓰지 못한다. 실제 v1을 새 G0 시험에 주면 수치 계산 전에 `v2_real_anchor_export_required`로 거부한다. `OWNED_LOW`는 awake≥800이고 소유율<70%일 때만이며 276초는 계속 보조다.

## 이번 독립 랩의 수치

최종 [GPU/게이트 로그](../../tools/gpudeco-lab/evidence/round6-source-gate.txt), [Editor 로그](../../tools/gpudeco-lab/evidence/round6-source-gate-editor.txt), [채널별 표](codex-gpudeco-lab-result.md)를 본다. Unity6000.3.10f1 / RTX4060Ti / D3D11, 최종 exit0이다.

|합성 시험|결과|
|---|---|
|v2 파일 왕복|2,923,234B, 소스257, 장식2,000, 채널32,000|
|8,000정점 최대/평균|0.0000309467 / 0.0000032826px, 0.01px 초과0|
|최종 RGBA 32,000개 최대/평균|0.0000002384 / 0.000000006754, 1/255 초과0|
|GPU 필드→정점 식 최대|0.0000191923px, 초과0|
|일시정지/소스 분리|곡 시각+30초에도 앵커 출력 동일, 소스37만 +7ms 후112채널 변화·다른 소스 변화0|
|정답 입력 독립성|Actual 변경 후 GPU 출력 변화0|
|곡 시각 경로 음성 대조|동일 합성 정답에서 정점 최대524.2243px·평균116.2252px·초과7936/8000으로 의도대로 실패|
|257소스 업로드+1 dispatch CPU 제출|1,028B, 500회 평균0.0075288ms|
|2,000소스 업로드+1 dispatch CPU 제출|8,000B, 500회 평균0.0132740ms|

**이 표는 합성 입력이다. Arche의 실제 앵커링 전후 개선량·실제 프레임 비용·G1 픽셀 통과가 아니다.** 비용에는 소스 관찰·게임 평가·80메시 복사·그리기가 없고 GPU timestamp도 없다. 이전 80메시 합성 비용을 더해 게임 비용을 추정하지 않았다. 2,000소스 비용 시험은 257개의 진행값을 반복한 업로드 규모 검사다.

## 검증과 환경

3종 최종 Release 오류0/0/0, 경고38/4/38. LoadCheck277/229/277곳 불일치0. 일반 Player에서 GpuDecoData/GpuStep/BakeWrite와 새 Rec의 GPU 메타데이터가 없다. 데이터 계약41건, bake/live 계약, DecoSim12, framecmp, ld2a 정책·geometry·delta, steps/출력 충돌, SM5 3커널이 통과했다. 최종 랩 C#도 오류·경고0이다. 근거는 `tools/gpudeco-lab/evidence/round6-*`와 manifest에 있다.

첫 독립 Editor는 UPM IPC 연결 30초 실패(exit1), 다음 직접 호출은 `com.unity.editor.headless was not found`·entitlement404(exit198)로 시험 진입 전에 종료됐다. 로그를 보존했고 클라이언트 자연 종료 확인 뒤 새 직접 호출이 성공했다. 로그인·라이선스 변경·설치·강제 종료는 하지 않았다. 성공한 변경별 랩 실행만 각기 보존했으며 초기 실패 뒤 남은 파일을 현재 결과로 복사하지 않았다.

워크트리에 지시된 ROADMAP 파일이 없어 현재 기본 체크아웃의 동명 파일과 최신 optimization-36 bake/livedeco 절을 읽었다. 수정은 이 워크트리 안에서만 했다. 게임 실행·설치·커밋·푸시·`.codex/config.toml` 수정0회이고 기존 export 6개는 보존했다.

## 남은 경계

이번 실제 v2 export는 **0개**다. 현재 실제 v1에는 소스 Pos/Start가 없고 이를 정답에서 역산해 채우지 않았다. 실제 세 시점의 채널별 px/ms·위상99%·정점·소유율 유지·준비 시간 변화는 미측정이다. 실제 G0가 없으므로 새 번들 제작·게임 소유권 이전·G1/G2를 진행하지 않았다. 이것을 새 설계의 불가능 판정으로 바꾸지 않는다.

현재 구현은 관찰/export/독립 랩 단계다. 게임에서 소유 Rec 생성을 생략한 뒤에도 BakeLive식 소스 시계를 계속 갱신하는 구동기, 실제 GPU 렌더러 연결 및 수명/복구 경로는 아직 구현하지 않았다. 랩의 SetData 비용만으로 이 부분의 예산이나 정확성을 보장할 수 없다.

다음 외부 자동 루프가 사용할 준비물은 [v2 export steps](../../tools/gpudeco-lab/evidence/round6-export-steps.json)이다. export 코드와 데이터 계약이 달라진 시험이며 기존 게임 시험을 그대로 반복하도록 요청하지 않는다. 게임 실행 금지를 유지한 채 준비 파일만 생성했다. 실제 v2 세 파일이 생기면 README의 `GpuDecoLab.Verify -gpudecoInput <새 폴더>`로 먼저 전후 비교한다. 소스 진행값 충돌·폴백 오차·GPU 정점 오차가 남으면 해당 숫자와 소유율을 근거로 다음 판단을 해야 한다.

`GPUDECO_G1=ERROR G0=UNMEASURED stage=source-clock reason=v2_real_anchor_export_required integration=BLOCKED`

---

# 이전 gpudeco-1 결과 — codex-al-gg-4 (2026-10-22)

**Unity 6000.3.10f1 직접 실행·D3D11·기존 AssetBundle 방식을 유지한다. 파일 충돌과 OWNED_LOW 판정을 수정했고 실제 export 3개를 모두 측정했다. 276초는 보조 측정으로 통과했지만 262·270초 G0는 실패하므로, 곡 시각 표 설계의 한계를 기록하고 게임 연결을 중단한다.** `GPUDECO_G1=PASS`는 달성하지 못했다. 게임 실행·설치·커밋·푸시·config 수정·로그인은 0회다.

## 이번 수정과 실제 수치

- 직전 [run3](gg-autoloop-run3-evidence.txt)는 262초 export가 이미 존재해 `FileMode.CreateNew`에서 실패했다. 기존 정답 파일은 보존한다. steps 생성기의 기본 출력은 매번 고유한 `exports/run-<UUID>/`이며, 명시한 폴더에 기존 `.gpudeco`가 있으면 **게임 준비 이전인 생성 단계에서 거부**한다. 런타임도 정적 데이터 수집 전에 충돌을 설명한다. 경쟁 조건에 대한 CreateNew 보호는 유지한다. 이전 고정 경로 JSON은 자동으로 수정되지 않으며 재사용 시 계속 거부된다. 이번에는 새 게임 시험을 요청하지 않는다.
- 최신 정정대로 exporter와 lab이 공통 `GpuDecoCoverage`를 쓴다. **awake ≥800이고 owned/awake <70%일 때만 OWNED_LOW**다. GPU 미지원 이징 제외 후에도 같은 검사를 한다. awake <800이면 보조 측정을 끝까지 진행하며 주 판정은 고밀도 2곳에 한정한다. 허용 오차·소유율·시점을 추가 완화하지 않았다.
- [이번 실제 GPU 로그](../../tools/gpudeco-lab/evidence/round5-verify.txt)는 `measured=3/3 primary=2/2 auxiliary=PASS`다. 아래 수치는 기존 정답 파일을 이번 수정본으로 평가한 결과다. 원본 3파일 SHA256은 이전 manifest와 일치한다.

|시점|소유/awake|정점 최대 / 평균 px|0.01px 초과|최종 RGBA 최대 / 평균|1/255 초과|
|---|---|---|---|---|---|
|262 주 판정|1730/2134 (81.0684%)|81.501349211 / 1.920398382|5936/6920|0.001122171059 / 0.000043840684|0/27680|
|270 주 판정|1875/2270 (82.5991%)|65.618709400 / 2.890432067|6796/7500|0.006678126752 / 0.000108363689|72/30000|
|276 보조|17/24 (70.8333%)|0 / 0|0/68|0 / 0|0/272|

16슬롯별 최대·평균·화면 환산·초과 수는 이번 로그의 `[gpudeco] G0` 48줄에 보존했다. 276초는 모든 슬롯의 오차가 0이다. 별도의 실제 이벤트 시작 위상·게임 픽셀·G2 FPS·GPU timestamp는 **UNMEASURED**다.

## 중단 근거와 검증

시간 의미의 차이는 판정 오류와 별개다. `scrVfxPlus.Update` 디컴파일의 `songposition_minusi >= startTime-startEffectOffset` 뒤 `StartEffect` 호출을 [설치 DLL IL](../../tools/gpudeco-lab/evidence/round5-vfx-il.txt)로 다시 확인했다. `DecoAnim.Step/StepPair`는 실제 시작 후 float 진행값에 dt를 누적하지만 `DecoBakeCore.Value`는 예약 T0와 tb의 차를 평가한다. v1 export는 소스별 실제 시작·Rec.Pos·dt 이력을 담지 않는다. 실제 필드로 만든 CPU 정점은 최대 0.004081598/0.007796900px·초과0인데 CPU 표부터 실제 필드와 다르다. 전역 시계 201후보에서도 최소 최대오차는 14.535930/16.227838px다. 이번에는 시간 보정·정답 역산을 제품에 적용하지 않았고 기존 실패 경로를 성능 성공으로 반복하지 않았다. 실제 트리거/진행 상태를 GPU 입력으로 바꾸는 구현은 현재 무상태 곡 시각 표의 범위를 넘으므로 진행하지 않았다.

- 3종 Release Dev/Player/PlayerAuto **오류0/0/0**, 기존 경고38/4/38. LoadCheck **277/229/277곳·불일치0**, 일반 Player의 bake/GpuDecoData/GpuStep 제외 확인.
- 랩 C# 오류0·경고0, SM5 3커널 컴파일 통과. 데이터·소유율 경계 **21건**, 새 출력 폴더/기존 파일 보존/기존 steps 순서 검사, bake/live 계약·DecoSim12·framecmp·ld2a 정책/geometry/delta·생성기 회귀 통과. 현재 수정과 관계없는 합성 성능·번들 시험은 다시 돌리지 않았다.
- 최초 샌드박스 실행은 UPM IPC 30초 실패(exit1), 다음 직접 호출은 기존 클라이언트 entitlement 실패(exit198)였다. 해당 클라이언트가 자연 종료한 뒤 새 직접 실행에서 RTX4060Ti/D3D11로 **의도한 G0 실패 exit1**을 확인했다. 강제 종료·로그인·설치·라이선스 수정은 하지 않았다. 이전 verify 파일이 남아 있던 최초 복사본은 `round5-stale-verify-not-current.txt`로 격리했고 이번 근거로 사용하지 않는다. 최종 에디터 로그의 `primary=2/2`와 새 결과를 함께 확인했다.

[최종 에디터 로그](../../tools/gpudeco-lab/evidence/round5-verify-fresh-editor.txt) · [입력/소스/검증 해시](../../tools/gpudeco-lab/evidence/round5-manifest.json) · [랩 결과](codex-gpudeco-lab-result.md) · [10줄 요약](codex-gpudeco-1-result-o.txt).

**GPUDECO_G1=G0_FAIL G0=FAIL game_pixels=UNMEASURED reason=real_export_gate_failed integration=BLOCKED**

아래는 당시 기록이다. 특히 gg-3의 “276초 절대800개 미달로 중단” 판정은 이번 최신 정정으로 대체한다.

---

# 이전 gpudeco-1 결과 — codex-al-gg-3 (2026-10-22)

**선택: Unity 6000.3.10f1 직접 실행·D3D11·기존 AssetBundle 경로를 유지했다. 실제 Arche export 3개가 확보됐지만, 262·270초 G0가 실패하고 276초 소유가 17개로 800개 조건을 충족하지 못했다. 현재 곡 시각 표 설계의 한계를 수치로 확인했으므로 게임 연결을 진행하지 않고 중단한다.** 게임 G1 픽셀·G2 성능은 미측정이며 `GPUDECO_G1=PASS`는 달성하지 못했다.

이번 작업의 게임 실행·설치·커밋·푸시·`.codex/config.toml` 수정은 모두 **0회**다. 기존 외부 자동 시험이 만든 파일을 읽고 독립 Unity 에디터와 오프라인 fixture만 실행했다. 이전 기록의 “export 0개”는 당시 상태이며 아래 최신 결과로 갱신한다.

## 실제 export와 소유 조건

근거: [직전 자동 시험](gg-autoloop-run2-evidence.txt), [최종 실제 GPU 대조](../../tools/gpudeco-lab/evidence/round4-verify-final.txt), [에디터 로그](../../tools/gpudeco-lab/evidence/round4-verify-final-editor.txt). export 원본은 덮어쓰거나 보정하지 않았다. [manifest](../../tools/gpudeco-lab/evidence/round4-manifest.json)에 원본·소스·실제 판정 로그의 SHA256을 기록했다.

|요청 시점|실제 raw / tb (초)|소유 / awake|비율|파일 크기|
|---|---|---|---|---|
|262|262.01982366340235 / 262.00703288420073|1,730 / 2,134|81.0684%|3,234,752 B|
|270|270.01799260871485 / 270.0076849285594|1,875 / 2,270|82.5991%|3,507,369 B|
|276|276.00734197394922 / 276.0021744821363|17 / 24|70.8333%|18,284 B|

외부 시험의 open부터 첫 export까지 **182,998.1268ms**로 300초 예산을 충족했다. worker wall은 **514.9991ms**이고 각 export 자체는 **4,623.9958 / 4,590.3929 / 4,386.4684ms**였다. 이 export 진단 비용을 매 프레임 성능으로 해석하지 않는다.

`ExportGpu`는 `Dormancy.AwakeList`에 있는 장식만 후보로 수집한다. `Dormancy.IsDormant`는 이미지 장식·히트박스 없음·메시 꺼짐·`GetVisible()==false`를 판정하며, 원본 `scrVisualDecoration.GetVisible/GetAlpha/ApplyColor`도 rendererEnabled와 실제 최종 알파를 사용한다. 디컴파일과 [설치 DLL IL](../../tools/gpudeco-lab/evidence/round4-visual-il.txt)을 교차 확인했다. **276초에는 현재 awake 집합을 100% 지원해도 상한이 24개라 절대 최소 800개에 못 미친다.** 제외 7개를 추가 지원하는 것으로 해결할 수 없다. 잠든 장식을 소유 수에 더하거나 시점·기준을 바꾸지 않았다. 감소가 맵 연출인지 Dormancy 추적 누락인지는 이번 export만으로 확정하지 않는다. 기존 gate2 증거에도 같은 시점 소유 17개가 기록돼 있어 새 GPU 실행 때문에 감소했다고 볼 근거는 없다.

## G0 실제 정답 대조

아래는 **수정 후** GPU가 `tb`에서 평가한 결과와 실제 SpriteRenderer 월드 정점·최종 색의 차이다. 월드 거리×export의 PixelsPerWorld로 화면 단위를 환산했다. 276초는 OWNED_LOW 즉시 중단 규칙에 따라 GPU 평가하지 않았다.

|시점|정점 수|정점 최대 / 평균 px|0.01px 초과|최종 RGBA 최대 / 평균|1/255 초과|
|---|---|---|---|---|---|
|262|6,920|81.501349211 / 1.920398382|5,936|0.001122171059 / 0.000043840684|0 / 27,680|
|270|7,500|65.618709400 / 2.890432067|6,796|0.006678126752 / 0.000108363689|72 / 30,000|

전체 16슬롯의 원시값 최대·평균·화면 환산·초과 개수는 최종 로그의 `[gpudeco] G0` 32줄에 있다. 주요 채널은 다음과 같다. 위치/크기/회전의 단위가 다른 원시값을 하나의 품질 수치로 합산하지 않았다.

|시점·채널|원시값 최대 / 평균|화면 최대 / 평균 px|초과 장식|
|---|---|---|---|
|262 위치 X|0.04156494 / 0.00392142|7.587691 / 0.274529|1,291|
|262 위치 Y|12251.40625 / 7.10778526|81.501349 / 1.870377|1,470|
|262 회전|0.68208504 / 0.05935108 도|7.682162 / 0.079005|1,118|
|262 크기 X / Y|각 0.04778016 / 0.00292422|2.732407 / 0.152949, 2.736437 / 0.136586|1,160 / 1,123|
|262 논리 color.a|0.00926147 / 0.00351304|해당 없음|650|
|270 위치 X|0.07879639 / 0.01096029|13.738138 / 0.732738|1,527|
|270 위치 Y|10311.40625 / 5.53972917|65.618709 / 2.730208|1,697|
|270 회전|0.66737175 / 0.09293105 도|7.501837 / 0.124303|1,378|
|270 크기 X / Y|각 0.07061148 / 0.00480101|3.385417 / 0.248347, 3.095133 / 0.214548|1,462 / 1,407|
|270 논리 color.a|0.05141610 / 0.00697301|해당 없음|1,300|

피벗 XY·RGB·opacity·시차 배율/오프셋의 논리 오차는 두 시점 모두 0이었다. 논리 color.a와 최종 renderer RGBA는 opacity 곱 때문에 서로 다른 분포다. 논리 알파 실패를 최종 색의 통과로 덮지 않았다.

## 원인을 코드로 분리한 결과와 수정

1. **실제 필드 → 정점 수식:** `CpuVertices(actual)`와 실제 SpriteRenderer 정점 차이는 262초 최대/평균 **0.004081598 / 0.000001180px**, 270초 **0.007796900 / 0.000003119px**, 초과 **0/0**이었다. 초기 포즈·시차·크기 식 전체가 수십 px 틀렸다는 가설과 구분했다.
2. **GPU 정점 산술:** 큰 좌표의 곱·덧셈 결합을 제한하도록 HLSL 정점 식에 `precise`를 추가했다. CPU 표로 만든 정점과 GPU 정점의 최대 차이는 **0.481697728→0.032652784px**, **0.124750398→0.007796900px**로 줄었다. 수정 후에도 262초 **4정점**이 0.01px를 넘는다. GPU 논리값 자체를 CPU 수식에 넣은 대조도 같은 4정점에서 실패하므로 잔여 포즈 산술 오차가 있다. 이 수정을 완전한 정확성 해결로 취급하지 않는다.
3. **곡 시각 표 ↔ 실제 필드:** CPU `Table.Eval`도 실제 필드와 큰 차이가 난다. 예를 들어 262초 index 391의 위치 Y는 실제 **−135387.3125**, CPU 표 **−147638.765625**다. 해당 트랙은 t0=261.825002344·길이0.225초·240693.0625→−239306.9375 선형 구간이다. 이벤트 시작 양자화·진행 시간 차이가 고속 트랙에서 큰 값 차이로 증폭된다. `scrVfxPlus.Update`는 곡 시각이 예약 시각을 지난 Update에서 StartEffect를 호출하고, `DecoAnim.Step/StepPair`는 각 Rec.Pos에 float dt를 누적한다. `Table.Value`는 `(tb−예약T0)/pitch×scale`로 바로 평가한다. 시작 프레임·과거 dt 이력은 이 무상태 표에 없다. 원본 디컴파일과 [IL](../../tools/gpudeco-lab/evidence/round4-vfx-il.txt)로 시작 경로를 교차 확인했다.
4. **전역 시계 보정의 한계:** 읽기 전용 진단으로 −50~+50ms를 0.5ms 간격 201개 후보에서 대조했다. 최대 정점 오차가 가장 작은 후보도 262초 **−6ms에서 최대14.535930 / 평균1.277428px / 초과6,125정점**, 270초 **−6ms에서 최대16.227838 / 평균1.434866px / 초과6,761정점**이었다. 평균이 가장 작은 후보도 각각 **1.129953 / 1.408870px**다. 연속 최적해를 증명하는 검사는 아니지만 이 범위의 단일 시계 보정으로 허용치를 달성한 후보는 없었다. 채널별 역산 시각은 위치 X에서 **−19.530~+8.010ms / −14.251~+10.701ms**로 서로 달랐다. 역산은 단조·동일 구간만 사용한 진단이며 실제 시작 위상을 측정한 값은 아니다.
5. **랩 판정 결함:** 기존 Verify는 OWNED_LOW를 로그한 뒤 정상 return해 프로세스가 exit 0이었다. 원인 분해를 공용 `GpuDecoAudit.cs`로 옮기고 Verify/Audit 모두 동일 경로를 사용한다. 실제 G0·소유 실패는 exit **1**로 반환하고, 마지막 G1 판정 한 줄에 **OWNED_LOW와 G0=FAIL**을 함께 보존한다. 기존 하드코딩 `요청시각±1/120초` 검사는 실제 이벤트 시작 위상을 측정하지 못하므로 제거했고, 요청 시점 도달 지연을 시작 위상 통과로 간주하지 않는다. 시작 위상은 계속 UNMEASURED다.

현재 v1 export에는 실제 이벤트 시작 프레임·Rec.Pos·dt 이력과 Source/Replacement가 저장되지 않는다. 실제 필드 스냅샷을 GPU 정답으로 덮어쓰거나 정답에서 역산한 시각을 제품 입력으로 쓰지 않았다. 소스별 실제 시작/진행 상태를 입력으로 바꾸는 방식은 현재 “곡 시각 몇 개만 CPU에서 전송” 설계와 다른 작업이며 이번에는 구현하지 않았다. 아틀라스·네이티브 경로도 이 시계 의미 차이를 자동으로 해결하지 않는다.

## 검증과 중단 상태

- Release Dev / Player / PlayerAuto **오류0/0/0**, 이번 증분 빌드의 기존 경고 **4/4/4**. LoadCheck **277/229/277곳**, 불일치 **0/0/0**. 일반 Player에는 GpuDecoData/GpuStep/export/BakeLive가 없다. 제품 런타임 소스·게임 연결·소유 쓰기 경로는 변경하지 않았다.
- 랩 C# **오류0·경고0**, SM5 커널3개 컴파일 통과. 수정 후 합성 smoke: 이징 **101,475개** 최대 **1.97160989046e−6**, 평균 **2.09635552387e−8**, 초과0; 정점8,000개 최대 **0.000477268px**, 평균 **0.000022239px**, 초과0; RGBA32,000개 최대 **6.55651092529e−7**, 평균 **1.30608677864e−8**, 초과0. 이 합성 통과는 실제 G0 실패를 대체하지 않는다.
- bake/live 계약, 데이터14, DecoSim12, 서명10, 정책106,479+18, geometry2,729·bounds482·scripts15·pose40·upload32, delta5,918,737/6,000변경, activation29, write/queue157+14, Dormancy41, framecmp·steps fixture 통과. WindowCheck 첫 호출은 생성 JSON 인자를 빠뜨려 exit1이었고, 올바른 생성 JSON을 전달한 검사는 **58건 통과**했다. 실패 로그도 보존했다. 전부 managed/오프라인 검사로 실제 게임 픽셀·FPS를 뜻하지 않는다.
- Unity 첫 샌드박스 실행은 Package Manager IPC 30초 오류(exit1), 직후 기존 클라이언트는 entitlement 오류(exit198)였다. 클라이언트 부재를 확인한 새 직접 실행에서 실제 D3D11 대조에 진입했다. 프로세스 강제 종료·로그인·설치·라이선스 파일 변경은 하지 않았다. 각 독립 실행은 5분 미만이다.
- 이번 HLSL 변경 후 번들을 새로 배포·연결하지 않았다. `evidence/bundle`과 이전 80메시 비용/픽셀 수치는 이전 합성 증거다. 실제 G0가 실패한 상태에서 번들·게임 연결 성공을 주장하지 않는다. 게임 G1/G2·GPU timestamp·실제 이벤트 시작 위상은 미측정이다.

재현: README의 `GpuDecoLab.Verify -gpudecoInput <exports 폴더>` 직접 실행. 정상적인 현 상태 판정은 에디터 exit1이며, 마지막 줄은 **`GPUDECO_G1=OWNED_LOW G0=FAIL game_pixels=UNMEASURED reason=real_export_gate_failed integration=BLOCKED`**다. 같은 게임 시험 반복·임계 완화·시점 변경을 요청하지 않는다. task-observer 지침은 읽기 전용 적용했고 사용자 메모리·스킬·관찰 저장소는 수정하지 않았다.

---

# 이전 gpudeco-1 결과 — codex-al-gg-2 후속 (2026-10-21)

**선택: Unity 6000.3.10f1 직접 실행 / D3D11 / 기존 AssetBundle 경로를 유지한다. 80개 Raw 메시 + 1,200개 서브메시가 제출 평균 0.1522106ms, 합성 픽셀 차이 0으로 랩을 통과했다. 게임 export를 막던 준비 순서를 수정했다. 실제 Arche export는 0개이므로 게임 연결과 G0/G1은 미완료다.**

게임 실행·설치·커밋·푸시·`.codex/config.toml` 수정은 하지 않았다. Unity 독립 랩만 실행했다. 근거는 [랩 결과](codex-gpudeco-lab-result.md), [최종 GPU 로그](../../tools/gpudeco-lab/evidence/submeshes.txt), [번들 로그](../../tools/gpudeco-lab/evidence/bundle.txt), `tools/gpudeco-lab/evidence/round3-*`다. 아래 이전 2회차 기록은 당시 결과이며 최신 제출 비용과 구분한다.

## 이번 실패 원인과 수정

- `gg-autoloop-run1-evidence.txt`는 300001.072ms에 `status=새 맵 스냅샷 대기`로 끝났다. 이 상태에서는 collector/worker가 아직 시작되지 않는다. 시간 상한을 늘리는 것으로 해결하지 않았다.
- `scnEditor.OpenLevelCo`는 LoadLevel → RemakePath → ReloadAssets → UpdateDecorationObjects까지만 수행한다. 실제 `scnGame.PrepVfx`는 재생 준비의 `scrController.WaitForStartCo`에서 호출된다. 기존 생성기는 `open → wait → decobake ready → seek`였으므로 **최초 PrepVfx를 일으킬 재생 전에 그 결과를 기다렸다.** 디컴파일과 [설치 DLL IL 호출 목록](../../tools/gpudeco-lab/evidence/round3-prepvfx-callers.txt)을 함께 확인했다.
- 새 순서는 `open → select 0 → play → gpudeco scheduled → pause → wait 60 → state → wait 60 → ready → resume → stop`이다. `scheduled`는 collector/worker/ready를 확인하고 실제 PrepVfx 관찰 세대·상태를 로그한다. 편집 화면 open만으로 준비 완료를 가정하거나 PrepVfx 관찰 전에 DOTween 배율을 0으로 고정하지 않는다. 타일 0의 첫 전체 시간표를 수집한 뒤 기존 262·270·276초 seek/export를 수행한다.
- timeout 420·wait 최대 60·첫 export 총예산 300초·기존 pause 배율 보호를 유지했다. [새 steps JSON](../../tools/gpudeco-lab/evidence/round3-export-steps.json)은 생성만 했으며 게임에서 실행하지 않았다. 실제 준비 시간·export 성공은 미검증이다.

## 서브메시 경로 검증

`GpuDecoSubmeshes.cs`는 합성 2,000장식·20,000표 구간을 한 번 평가한 뒤 **80개 메시 Raw 버퍼**에 CopyRun을 기록한다. 각 메시는 25장식·15서브메시로 구성하고 재질 인스턴스를 그대로 빌린다. 평가 1 + 복사 80 = **81 dispatch/프레임**이다. 정점 쓰기는 GPU에서만 수행하고 비용 창 안에는 readback·그리기·메시/명령 재생성이 없다.

|항목|이번 최종 실측|
|---|---|
|CPU 제출 500회 / 100회씩 5묶음|평균 **0.1522106ms**, 묶음 평균 최소 **0.147147ms**, 최대 **0.157122ms**; 0.3ms 예산 통과|
|Raw 버퍼 80개 / 정점 8,000개|위치·색·UV 불일치 **0**|
|1,200개 분리 렌더러 ↔ 80렌더러×15서브메시|128×128, 기준 비검정 1,573픽셀, **다른 픽셀 0 / 최대 채널 차이 0**|
|같은 정렬 키·반투명 겹침·비소유 경계 / 생성 순서 6종|원본 SpriteRenderer ↔ 개별 메시 6종 및 개별 ↔ 서브메시 6종 모두 **0픽셀 / 최대 0**|
|재질 순서 역전 음성 대조 6종|각 **2,304픽셀** 차이, 최대 채널 차이 **93~110**; 대조군이 실제 순서 오류를 검출함|
|Camera.Render CPU wall / 각 100회|분리 1,200렌더러 **4.237392ms** → 80렌더러/1,200서브메시 **3.068427ms**|

기존 1,001 dispatch / 2.723905ms 경로를 그대로 반복하지 않았다. 위 Camera.Render 값은 컬링·제출·드라이버 대기가 섞인 에디터 128×128 장면의 wall time이다. **순수 엔진 CPU 비용·GPU timestamp·실게임 FPS를 뜻하지 않는다.** `Sprites/Default`의 3종 텍스처와 재질을 썼으며 실제 게임 `Sprites/Tile`, 실제 정렬 경계 71~78개, 한 렌더러에 수백 재질, 게임 복구 수명은 아직 검증하지 않았다. 소수 fixture가 Unity 전체의 정렬 규칙을 증명하지도 않는다. 이번 합성 범위에서 서브메시 제안을 배제할 근거가 없으므로 아틀라스/네이티브 방식으로 바꾸지 않았다.

합성 정점은 최대 **0.000477268022223px**, 평균 **0.0000223719397063px**, 0.01px 초과 0이다. 최종 색은 최대 **6.55651092529e-7**, 평균 **1.30608677864e-8**, 1/255 초과 0이다. 실제 시작 위상·장식 채널별 오차의 대용이 아니다. 전체 16채널은 최종 GPU 로그에 있다.

## 환경·검증·남은 단계

첫 샌드박스 에디터는 Package Manager IPC 30초 실패(exit 1), 직후 권한 검토 실행은 기존 라이선스 클라이언트에서 `Found 0 entitlement groups` / `com.unity.editor.headless was not found`(exit 198)였다. 해당 클라이언트가 종료된 것을 확인한 **새 직접 실행은 라이선스 처리와 RTX 4060 Ti D3D11 fixture에 정상 진입**했다. 로그인·설치·라이선스 파일 변경은 하지 않았다. 성공/실패 로그를 모두 보존했다. 초기 픽셀 시험의 1,991픽셀 차이는 비용 측정에서 이동한 시각을 원래대로 복원하지 않은 랩 결함이었다. 같은 시각으로 복원한 뒤 통과했고 실패 로그는 `round3-submeshes-r1.txt`에 남겼다.

- Release Dev / Player / PlayerAuto: 오류 **0/0/0**, 기존 경고 **38/4/38**. LoadCheck도 각 불일치 **0**이며 일반 Player의 bake/GPU 타입·명령·export 제외를 확인했다.
- bake/live 계약·복구, 데이터 계약 14, DecoSim 12, 서명 10, ld2a policy 106,479+18, geometry 2,729·culling 482·script 15·pose 40·upload 32, activation 29, write/queue 157+14, delta 5,918,737검사/6,000변경, framecmp 58+21+2, steps·HLSL 검사가 통과했다. 로그는 `round3-*`다.
- 번들을 다시 만들고 같은 에디터에서 재로드한 컴퓨트의 101,475개 이징을 대조했다. 게임 적재는 미검증이다. 랩의 C# 변경은 제품 빌드에 들어가지 않는다.
- 실제 export **0개**. 다음 선행 조건은 수정된 steps가 만든 실제 262·270·276초 파일을 `GpuDecoLab.Verify`로 대조하는 것이다. 이 세 파일과 실제 소유율·채널/정점 오차가 없으므로 GPU 소유/게임 렌더러 연결을 진행하지 않았다. 게임 시험 반복 요청이나 G1 PASS 표시는 하지 않는다.

task-observer v3.5.0은 읽기 전용 지침으로 적용했다. 사용자 범위 스킬·메모리·관찰 파일은 수정하지 않았다. 관찰: 준비 완료 대기는 실제 준비를 시작시키는 호출 뒤에 두어야 하며, 비용 측정에서 바뀐 시각은 픽셀 정답 대조 전에 복원해야 한다.

최신 판정: **GPUDECO_G1=ERROR stage=G0-real-export reason=real_export_missing**

---

# 이전 gpudeco-1 결과 — 자동 반복 2회차

**기존 Unity 6000.3.10f1 번들 경로로 GPU 수치 대조·Raw 메시 그리기·번들 생성/재로드를 완료했다. export 대기의 시간 초과와 일시정지 시 bake 폐기 경로를 수정했다. 실제 Arche export는 아직 0개이며, 합성 1,000구간 제출 2.723905ms는 목표 0.3ms를 초과한다. 게임 G0/G1은 미검증이므로 GPU 소유/게임 렌더러 연결로 진행하지 않았다.**

게임 실행·설치·커밋·푸시 **0회**, .codex/config.toml 수정 **0회**다. 독립 에디터와 읽기 전용 참조/오프라인 도구만 실행했다. 일반 Player에서는 GPU/bake 진단 코드가 제외된다.

## 방식 선택과 실제 에디터 실행

기존 fsr/BuildFsr.cs.txt와 docs/shader-research.md의 AssetBundle 방식을 재사용했다. [랩 결과](codex-gpudeco-lab-result.md)에 실패/수정/채널별 수치/한계를 모았다.

지시서의 정정대로 Unity.exe를 직접 호출했다. 첫 호출은 IPC 또는 라이선스 exit 198이었지만, **-batchmode -quit -force-d3d11 직접 호출**에서 6000.3.10f1·RTX 4060 Ti·Direct3D11 fixture가 실제 실행됐다. 로그인/활성화는 하지 않았다. -nographics 경로는 Null Device라 GPU 검사에 쓰지 않았다. 성공한 전체 명령은 [README](../../tools/gpudeco-lab/README.md)에 있다.

최초 번들은 생성됐어도 재로드가 실패했다. 버전 오류만 보고 에디터 호환성 문제로 결론내리지 않고, 로그의 **AssetBundle 내장 모듈 비활성화**를 확인했다. 독립 프로젝트 manifest 참조를 수정하고 강제 재생성해 **35,399바이트 번들 재로드 및 101,475개 이징 대조**를 통과했다. 게임에서의 적재는 미검증이다. sfnative.dll은 변경하지 않았다.

## export를 막는 코드 경로 수정

1. **실패 이력**: gg 장부/회차 1 증거에는 timeout 180 뒤 decobake ready 대기에서 export가 만들어지지 않았다. 같은 steps를 반복하지 않았다. 새 생성기는 첫 줄 **timeout 420**, open 뒤 **wait 60 → decobake state → wait 60 → decobake ready**로 나눈다. state는 실제 ready/세대/collector/worker를 로그한다.
2. **5분 예산**: gpudeco prepare를 open 전에 시작한다. bake Tick/ready/export에서 **300초**를 확인하고 넘으면 prepare_ms·상태·ERROR를 기록한다. 첫 export 로그는 open+bake+seek+파일 쓰기 전체와 bake wall을 분리하고, export 자체 비용도 남긴다. gpudeco off/reset 및 bake 종료에서 추적을 해제한다. 이는 시간 제한 구현이며 **실제 준비가 5분 안에 끝났다는 실측은 없다**.
3. **일시정지 결함**: AutoTest pause는 FrameCompare.Pause를 호출해 DOTween.timeScale을 0으로 만든다. 같은 Update의 뒤쪽 DecoBake.Tick은 frozenScale 변경으로 이를 실패 처리하여 표를 버릴 수 있었다. FrameCompare가 소유한 동일 controller·paused·Time=0·Tween=0·audio pause가 온전하고 **정지 전 oldTween이 frozenScale과 같을 때만** 일시정지로 인정한다. 외부 배율 변경·다른 controller·손상된 정지는 기존 거부를 유지한다. 게임에서의 실제 일시정지/export 성공은 미검증이다.
4. **소유권 유지**: export는 실제 논리값·정점·구운 표를 읽는 AUTOTEST 전용이다. 소유권 변경·원본 트윈 종료·Renderer 숨김·매 프레임 전체 스캔/쓰기 훅을 추가하지 않았다. gpudeco on은 실제 데이터 검증 전 계속 거부한다. steps는 export 전용이며 G1 PASS를 출력하지 않는다.

게임 수식은 scrDecoration.UpdatePosition·scrParallax.SetTrans 디컴파일과 기존 tools/ilscan 증거를 읽어 확인했다. game callback의 실제 서명은 이번 FrameCompare fixture도 설치 DLL에서 읽기 전용으로 교차 확인했다. 수식 자체의 실제 게임 충실도는 export가 필요하다.

## GPU에서 확인한 수치

근거: [smoke-r2.txt](../../tools/gpudeco-lab/evidence/smoke-r2.txt), [최종 합성 대조](../../tools/gpudeco-lab/evidence/runs.txt), [번들 대조](../../tools/gpudeco-lab/evidence/bundle.txt).

|항목|실측|
|---|---|
|수정 전 GPU 이징 101,475개|최대 0.00165259838104 / 평균 4.02213904572e-7 / 2e-5 초과 526|
|수정 후 같은 규모|최대 **1.97160989046e-6** / 평균 **2.09635552387e-8** / 초과 **0**|
|합성 정점 8,000개|최대 **0.000477268022223px** / 평균 **0.0000223719397063px** / 0.01px 초과 **0**|
|합성 최종 색 32,000개|최대 **6.55651092529e-7** / 평균 **1.30608677864e-8** / 1/255 초과 **0**|
|1,000개 독립 Raw 메시|복사 8,000정점 불일치 **0**, 단일/분할 RGBA 이미지 차이 **0픽셀 / 최대 0**|
|단일 메시 제출|**0.005729ms**, dispatch 1회|
|1,000메시 제출|**2.723905ms**, dispatch **1,001회**|

이징 오류는 Circ 경계와 Elastic asin 위상 근사였다. 끝점 분기·유리식·precise로 고쳤다. 독립 메시의 초기 빈 화면은 같은 대조군의 UInt32 인덱스를 적용한 뒤 해결됐다. 전체 이력과 16채널별 최대/평균/초과는 랩 결과에 있다. **합성 대조를 실제 Arche G0나 게임 G1로 바꾸어 표시하지 않는다.**

## 설계상 현재 중단 지점

2,000장식·20,000표 구간·1,000개 독립 메시에서는, 한 통합 버퍼의 평가 뒤 미리 기록한 CommandBuffer로 각 메시 Raw 버퍼에 복사한다. CPU 정점 업로드/Transform 쓰기/프레임당 명령 재작성은 없지만, copy dispatch **1,000회**가 추가된다. **제출 2.723905ms는 0.3ms 목표의 약 9.08배**이며 단일 dispatch 조건도 충족하지 않는다. 이 경로를 게임에 그대로 연결하지 않는다.

이는 한 합성 실행의 에디터 제출 수치이며 GPU 실행 시간·실게임 FPS가 아니다. 드라이버 대기/밀어내기가 섞일 수 있고 렌더러 1,000개의 매 프레임 엔진 비용은 포함하지 않는다. 모든 GPU 방식이 불가능하다는 결론도 아니다. 네이티브 D3DCompile 경로는 출력 분배·동기화·수명을 별도 해결해야 하므로 비교만 남기고 구현하지 않았다. 공유 버퍼/커스텀 그리기 전환은 게임 셰이더·재질·정렬 검증을 새로 해야 한다.

**실제 export 0 / 실제 소유 수·비율·prepare_ms·시작 위상·실제 채널 오차·게임 픽셀·FPS 모두 미측정**이다. 실제 export와 비용을 통과하기 전에 게임 연결/소유 복구/G1 steps를 구현한 것으로 표시하지 않는다. 같은 코드를 다시 게임 시험하도록 요청하지 않았다.

## 오프라인 검증

근거는 tools/gpudeco-lab/evidence/round2-*.txt다.

|검사|결과|
|---|---|
|Release Dev / Player / PlayerAuto|각 **오류 0**, 경고 38 / 4 / 38|
|LoadCheck Dev / PlayerAuto|FieldRef 277곳, 불일치 **0**; export는 AUTOTEST만|
|LoadCheck Player|FieldRef 229곳, 불일치 **0**; bake/GPU 타입·명령·export 제외|
|랩 C# / HLSL|오류·경고 **0/0**, 3커널 SM5 컴파일 통과|
|데이터 계약 / DecoSim / 서명|**14 / 12 / 10 통과**|
|bake/live 계약·복구|통과|
|framecmp 수치·마스크·상태·대조 / 빌드·pause 메타데이터 / 설치 게임 서명|**58 / 21 / 2 통과**|
|ld2a 기하·컬링·스크립트·pose·upload|**2729 / 482 / 15 / 40 / 32 통과**|
|ld2a policy·G2 policy / activation / write·queue|**106479·18 / 29 / 157·14 통과**|
|ld2a delta / window|**5,918,737검사·6,000변경 / 58 통과**|
|gpudeco steps / framecmp steps|시점 3곳·open 1회·최대 wait 60·G1 미통과 계약 / **24검사 통과**|

기존 어셈블리 충돌·구식 API·미사용 필드 경고는 남는다. LoadCheck의 동적 참조 5곳은 정적 검사의 한계다. FrameCompareCheck의 첫 --no-restore 호출은 assets 파일 부재로 실패했고, 로컬 SDK 복원 후 최종 검사에서 통과했다. 랩 비용 검사는 대규모 회귀 빌드와 겹치지 않게 수행했다.

task-observer **3.5.0**을 적용했다. 제한된 worktree에서 사용자 범위 관찰·스킬·메모리 파일은 수정하지 않고 이 결과로 전달한다. 관찰: 버퍼 readback 일치만으로 렌더러 출력 성공을 대신하지 않으며, 번들 생성만으로 재로드 성공을 대신하지 않는다.

이전 2회차 판정: real_export_missing_and_1000_run_submit_over_budget. 위 최신 서브메시 결과가 제출 예산 문제를 해소했고, 실제 export 부재는 남아 있다.

GPUDECO_G1=ERROR stage=G0-real-export reason=real_export_missing
