# gpudeco 최신 결과 — codex-al-gg-2 후속, round9 (2026-10-22)

**Unity 6000.3.10f1 직접 실행·D3D11 AssetBundle 경로를 유지했다. 최신 run1의 270초 G0 실패를 보간 산술에서 재현하고 수정했다. 새 번들 재로드로 최신 두 export의 G0를 확인했으며, 수정 후 게임 G1은 미측정이다.** 게임 실행·설치·커밋·푸시·config 변경은 하지 않았다.

## 최신 증거와 원인

장부 최신 항목은 gg-7이지만 전달된 `gg-autoloop-run1-evidence.txt`가 run6보다 나중에 갱신됐다. run1 첫 export는 **183571.3017ms**, bake는 **514.6238ms**다. 262초 G1은 **3768/3686400픽셀(약 0.1022%)·최대9/255**, A/A2=0이었다. 픽셀 개수 조건은 충족하지만 최대 채널 차이 조건은 실패다. 이어 270초 G0가 최대 **0.498907268047px·4정점 초과**로 중단돼 276초 export는 없다. 두 파일을 세 시점 전체 통과로 대체하지 않았다.

[원인 진단](../../tools/gpudeco-lab/evidence/round9-residual.txt)은 장식 index=391의 위치 Y 하나를 지목했다. 시작240693.0625, 목표−239306.9375, duration0.225000008941, 실제 progress0.0693048983812에서 CPU/실제 값은 **92842.6171875**, 기존 GPU는 **92842.625**다. 회전·정점 식 자체는 같은 GPU 필드를 주면 최대0.007795426px로 허용 안이다. 따라서 회전 근사나 전역 시계 보정을 반복하지 않았다. 원본 `scrDecoration.SetRotation/SetScale/UpdatePosition` 디컴파일과 [설치 DLL IL](../../tools/gpudeco-lab/evidence/round9-decoration-il.txt), DOTween 호출은 [IL](../../tools/gpudeco-lab/evidence/round9-ease-il.txt)로 확인했다.

첫 가설인 선형 나눗셈 보정만으로는 큰 끝값 보간이 **그대로 실패**했다([중간 실패](../../tools/gpudeco-lab/evidence/round9-division-only-fail.txt)). 최종 변경은 다음 두 가지다.

- Linear의 SM5 근사 나눗셈을 double 잔차 보정 뒤 float로 반올림한다. 65,537개 표본에서 원래 GPU 나눗셈은 CPU와 **19,567개 불일치**, 수정 경로는 **0개**다. 이 표본은 duration 1e-4~100과 실제 실패 입력을 포함하며 모든 실수 입력의 증명은 아니다.
- `start + change × ease`에서 강제 분리했던 float 중간 곱을 `mad`로 바꿨다. 이 PC의 CPU 런타임 표현식과 같은 한 번 반올림 결과를 얻는다. 같은 실패 입력의 CPU/GPU가 모두 **92842.6171875**다. 다른 GPU/런타임의 산술과 비용은 미검증이다.

정답 Actual/World를 GPU 입력에 넣거나 원본 값을 수정하지 않았다. 소유 집합·시간·판정 기준·정렬·색 UNorm8 변환은 유지했다. `Residual`은 게임 중단으로 남은 일부 파일을 진단하는 새 랩 명령이며 `snapshots=2 full_gate=NOT_EVALUATED`를 출력한다. 한 스냅샷이라도 G0 실패하면 exit1이다.

## 수정 후 실제 export 대조

새 **56,364B** 번들을 생성·재로드했고, **그 번들의 ComputeShader**로 최신 두 원본 export를 평가했다([번들 수치](../../tools/gpudeco-lab/evidence/round9-bundle-snapshots.txt)).

|시점|소유/awake|수정 전 정점 최대/평균px·초과|수정 후 정점 최대/평균px·초과|수정 후 RGBA 최대/평균·초과|
|---|---|---|---|---|
|262|1680/2081 (80.7304%)|0.00401119562 /0.00000596904·0|0.00401119562 /0.00000596904·0|1.86264515e-9 /8.15080490e-12·0|
|270|1873/2270 (82.5110%)|0.49890726805 /0.00027365150·4|0.00779542606 /0.00000728350·0|3.72529030e-9 /3.91238108e-11·0|

전체 16채널의 최대·평균·화면 환산·국소위상은 같은 로그에 있다. 빠른 소스1568/1568·1771/1771이 실제 평균프레임9.216351/11.049486ms 안이다. 실제 시작위상 전체 이력은 미측정이다. 276초는 이번 시험에서 수집되지 않았으며 이전 파일로 채우지 않았다.

별도로 **이전 run6의 완전한 세 파일**을 새 셰이더로 회귀 대조했다([로그](../../tools/gpudeco-lab/evidence/round9-verify.txt)). 262/270/276초 정점 최대 **0.00817265734/0.00779688125/0px**, 초과0/0/0, `GPUDECO_G0_OFFLINE=PASS measured=3/3 primary=2/2 auxiliary=PASS`다. 이는 이전 입력의 회귀 결과이며 최신 run1의 누락 시점을 측정했다는 뜻이 아니다. 전역 시계201후보·1,000메시 성능 시험은 반복하지 않았다.

## 검증과 준비물

- [번들 재로드](../../tools/gpudeco-lab/evidence/round9-bundle.txt): 이징101,475개 초과0, 선형65,537개 불일치0, 회전8,200각도 및 대형 경계 사례 통과. 색36조건·중간값6격자 모두0픽셀. 실제 두 파일의 그리기 색26,880/29,968성분 바이트 불일치0이다. 합성 셰이더는 Sprites/Default다.
- Release Dev/Player/PlayerAuto 오류 **0/0/0**(이번 증분 로그 경고4/4/4). LoadCheck **277/229/277곳 불일치0**, 일반Player 제외 확인. 랩 C# 오류/경고0, SM5 5커널 통과.
- 데이터51·bake/live·DecoSim12·framecmp·ld2a 정책/geometry/delta·steps 검사 통과. framecmp 경로 오기의 exit2는 보존하고 실제 `check-framecmp-steps.py`의 24검사 통과로 정정했다.
- [새93단계 JSON](../../tools/gpudeco-lab/evidence/round9-g1-steps.json)을 생성만 했다. 새 고유 export 폴더·새 번들, timeout420·wait60·첫 export300초 예산·전체화면 A/B/A2·262/270 주/276 보조·원래 픽셀 임계값을 유지한다.

최초 샌드박스 실행은 UPM IPC exit1, 직후 실행은 기존 라이선스 클라이언트 entitlement exit198이었다. 클라이언트 자연 종료 후 직접 실행은 성공했다. 로그인·라이선스 변경·강제 종료는 없다. 초기 wrapper가 IPC 실패 뒤 예전 verify.txt를 복사한 파일은 **STALE EXCLUDED**로 명시하고 근거에서 제외했다. [근거·해시 장부](../../tools/gpudeco-lab/evidence/round9-manifest.json)에 구분했다.

**최신 두 시점 G0 실패는 해결됐지만, 기존 262초의 최대9/255 픽셀 차이가 이번 변경으로 해결됐는지는 미측정이다.** 게임 G1 PASS·G2·GPU timestamp·실시간 소유/원본 평가 및 Transform 쓰기 생략·재생전1회 준비는 완료로 선언하지 않는다. 이번 변경은 정지 G1 진단의 보간 정확성을 고친 범위다.

`GPUDECO_G1=ERROR G0_LATEST_PARTIAL=PASS snapshots=2/3 game_pixels=UNMEASURED reason=post_fix_runtime_pixels_required prior_run1=G0_FAIL`

---

# 이전 gpudeco 결과 — codex-al-gg-7 (2026-10-22)

**기존 Unity 6000.3.10f1/D3D11 AssetBundle 경로를 유지했다. 정지 G1의 그리기 색을 네이티브 SpriteRenderer와 같은 8비트 변환으로 수정했다. 독립 랩은 통과했으며 수정 후 게임 G1은 미측정이다.** 새 번들은 55,748B이며 생성·재로드·실제 GPU 실행을 확인했다. 게임 실행·설치·커밋·푸시·config 수정은 0회다.

## 직전 실패와 원인 확인

장부와 run6 증거·PNG를 읽었다. 262초 **418,684픽셀/최대7**, 270초 **136,779/최대5**, 276초 보조 **0/0**이었다. A/A2는 모두0·안정 기준이었다. 차이의 대부분은1~2/255이며 히스토그램은 [원본 PNG 감사](../../tools/gpudeco-lab/evidence/round8-prior-pixels.txt)에 보존했다. 원본 `scrVisualDecoration.ApplyColor` 디컴파일과 설치DLL IL의 `SpriteRenderer.set_color` 호출을 [교차확인](../../tools/gpudeco-lab/evidence/round8-color-il.txt)했다.

이전 합성 SpriteRenderer 대조는 정점 색이 모두 흰색이라 이 차이를 검출하지 못했다. 독립 랩에서 실제 로그의 저알파값을 포함한 비흰색·겹침을 주면 기존 Float32 그리기 색이 다른 픽셀을 만들었다. 네이티브 색은 포화 후 **`floor(float(color*255)+0.5)/255`**와 일치했다. C# `Color32`와 HLSL `round`의 짝수 반올림은 중간값에서 틀렸다(예:0.5/255→네이티브1, managed0). 중간 실패도 `round8-colors-midpoint-fail.txt`와 Editor 로그에 보존했다.

따라서 **GPU Evaluate의 속성·논리 색·G0는 Float32 그대로**, GPU `CopyRun`의 그리기용 색만 명시적 `_CopyQuantizeColor=1`로 변환한다. 곱셈을 `precise`로 분리해 FMA에 따른 중간값 변경을 막는다. 36B Float32 정점 레이아웃, 원본 재질·텍스처·셰이더·정렬·소유 정책은 유지했다. CPU helper는 readback 검사 전용이며 정점 업로드나 정답 보정에 쓰지 않는다. 원본 Float32 복사 fixture는 flag0을 명시한다.

## 검증 수치

Unity6000.3.10f1 / RTX4060Ti / D3D11의 [최종 색 랩](../../tools/gpudeco-lab/evidence/round8-colors-final-guard.txt)과 [새 번들 재로드](../../tools/gpudeco-lab/evidence/round8-bundle.txt)가 exit0이다.

|검사|결과|
|---|---|
|비흰색·저알파·범위 밖·중간값 6색 × 겹침1/32/128 × CPU/GPU|36조건 모두 다른픽셀0·최대0|
|모든256개 중간값과 ±1e-7, RGB/알파|6격자 모두 다른픽셀0·최대0|
|기존 Float32 음성 대조|18조건 중14조건에서 차이 검출|
|실제262/270/276초 export의 GPU 그리기 색|27,680/28,368/272성분, 바이트 불일치0/0/0, float 정규화 최대오차5.96046448e-8|
|새 번들 생성·재로드|55,748B, 이징101,475개 초과0·회전 PASS·위 색 검사 재통과|
|Release Dev / Player / PlayerAuto|오류0/0/0, 기존경고38/4/38|
|LoadCheck / 일반Player제외|277/229/277곳 불일치0, 일반Player GPU 타입 없음|
|기존 fixture|데이터51·bake/live·DecoSim12·framecmp·ld2a 정책/geometry/delta·steps·SM5 통과|

색 랩의 픽셀 셰이더는 `Sprites/Default`다. 실제 export 대조는 게임에서 수집한 정답 색의 네이티브 변환과 GPU 버퍼를 비교한 것으로 **게임 `Sprites/Tile`의 최종 화면 대조를 대신하지 않는다.** 기존 게임 실패 전체의 원인을 색 하나로 확정하지 않으며 수정 후 G1 개선량도 추정하지 않는다.

## 최신 export의 G0

직전 run6의 원본 v2 세 파일을 읽기만 했다. [오프라인 GPU 대조](../../tools/gpudeco-lab/evidence/round8-verify.txt)는 실제 입력이 바뀐 이번 결과이며 이전 회차 수치를 재사용하지 않았다. 전후 채널별 최대·평균·px/ms·초과는 로그에 모두 있다.

|시점|소유/awake|곡시계 정점 최대/평균px·초과|앵커 정점 최대/평균px·초과|앵커 RGBA 최대/평균·초과|
|---|---|---|---|---|
|262|1730/2134 (81.0684%)|46.2278860 /1.90658232·5978|0.00817265734 /0.00000472407939·0|7.45058060e-9 /1.18695630e-10·0|
|270|1773/2217 (79.9729%)|63.6225510 /2.25441362·6569|0.00779688125 /0.00000439756416·0|7.45058060e-9 /1.80873002e-10·0|
|276 보조|17/24|0/0·0|0/0·0|0/0·0|

빠른 앵커 소스1628/1628·1719/1719가 실제 평균프레임9.252877/9.399858ms 안이었다. 이는 현재 값의 국소위상 검사이며 실제 시작위상 전체이력 증명은 아니다. 소유70%·276보조·0.01px·색1/255·픽셀0.3%/최대3 기준은 바꾸지 않았다. 전역 시계201후보나 실패한1,000메시 성능 경로는 반복하지 않았다.

## 준비물과 한계

[새93단계 JSON](../../tools/gpudeco-lab/evidence/round8-g1-steps.json)은 새 고유 export 폴더와 새 번들을 지정한다. 생성만 했으며 실행하지 않았다. open/play/bake 순서·timeout420·wait60 두 번·첫 export300초 예산·전체화면 A/B/A2를 유지했다. 직전 첫 export183007.7304ms는 외부 run6의 수치다.

최초 UPM IPC exit1, 기존 클라이언트 entitlement exit198을 보존했다. 클라이언트 자연 종료 후 에디터 직접 실행으로 통과했다. 로그인·라이선스 변경·강제종료는 없었다. fixture 경로 오기의 미실행/exit2도 보존하고 실제 경로로 검사했다. 근거·SHA256은 `round8-manifest.json`에 있다.

**수정 후 게임 G1·수명·복원은 미측정, 실시간 소유/원본평가·Transform쓰기 생략/재생전1회준비는 여전히 미구현이며 G2·GPU timestamp도 미측정이다.** 이번 수정은 정지 진단의 색 전달 차이를 다룬다. 실시간 비용 성공이나 전체 목표 완료를 주장하지 않는다.

`GPUDECO_G0_OFFLINE=PASS measured=3/3 primary=2/2 auxiliary=PASS`

`GPUDECO_G1=ERROR G0=PASS game_pixels=UNMEASURED reason=post_fix_runtime_pixels_required prior_run6=PIXEL_FAIL`

---

# 이전 gpudeco-1 결과 — codex-al-gg-6 (2026-10-22)

**Unity 6000.3.10f1 직접 실행·D3D11·AssetBundle 경로를 유지했다. 실제 v2 세 파일의 오프라인 G0는 통과했다. 게임을 실행하지 않고 정지 화면 G1 연결과 새 시험 JSON까지 구현했으며, 게임 픽셀 G1은 미측정이다.** 게임 실행·설치·커밋·푸시·config 수정·로그인·강제 종료는 하지 않았다.

## 실제 원인과 수정

직전 run5는 첫 export **182794.6272ms**, bake **508.5029ms**에 v2 파일 세 개를 생성했다. 이번 입력은 `exports/run-c8cc4448c4f34c249594694aeab12a85/`의 원본이며 덮어쓰거나 정답에서 앵커를 역산하지 않았다. 준비 순서와 300초 예산을 유지했고, 실패한 전역 시계 201후보·1,000메시 제출 경로는 반복하지 않았다.

1. 첫 실제 v2 측정에서 앵커링 후 정점 최대는 **262초 0.0326676px / 270초 22.1796161px**였다. 270초의 큰 논리 오차는 전부 실제 시계가 없는 표 폴백이었다. 회전 2개와 알파 100개에서 발견됐으며, 첫 거부 채널로 집계하면 회전 2·위치 50·알파 50개다. 공통 `MotionWithoutClock`는 **현재 진행 중인 비상수 표 구간에 실제 앵커가 없으면 그 장식을 원본 경로로 남긴다.** Actual/World/정답 색으로 소유를 고르지 않는다. exporter와 lab이 같은 규칙을 쓴다. 소유율 70% 조건은 유지했다.
2. exporter는 `!Rec.Started`를 일괄 버렸다. 원본과 현재 `DecoAnim` 코드를 대조하면 scalar/color 시작값은 생성 때 이미 저장되고, scale 시작값은 첫 Startup에서 읽는다. 따라서 아직 첫 Update를 받지 않은 scalar/color/parallax Rec는 실제 진행 0으로 내보내고, pending scale의 오래된 VStart는 사용하지 않도록 수정했다. **이 pending 보완의 새 실제 export 효과는 아직 미측정**이며, 기존 파일을 고쳐 채우지 않았다. 디컴파일과 [설치 DLL IL](../../tools/gpudeco-lab/evidence/round7-move-il.txt)을 교차확인했다.
3. 262초의 4정점 오차는 index342의 폭이 큰 180도 장식이었다. CPU quaternion의 w는 `-4.37113883e-8`인데 D3D11 native cos 반각은 0이었다. 단순 반각 치환만으로 해결되지 않았고, 일반 float 다항식도 90도 대형 회귀에서 **0.0703125px** 실패했다. 최종 셰이더는 반각 범위 축소·다항식을 **SM5 double 산술**로 계산한 뒤 float quaternion으로 돌아온다. float sqrt의 1 바로 아래 반올림 경계도 보존한다. RTX4060Ti/D3D11에서 실제 실행을 확인했다. 다른 GPU의 double 지원·비용은 보장하지 않는다.

중간 실패는 `round7-residual`, `round7-quaternion`, `round7-policy`, `round7-rotation-*`, `round7-rotations-*`에 보존했다. 단순 float 근사를 통과로 바꾸거나 허용 오차를 늘리지 않았다.

## 실제 G0 최종 수치

현재 게임/랩 공용 `GpuDecoGpu.cs`로 실행한 [최종 로그](../../tools/gpudeco-lab/evidence/round7-shared-verify.txt)와 [Editor 로그](../../tools/gpudeco-lab/evidence/round7-shared-verify-editor.txt)는 exit0이다. 전후 비교는 **같은 최종 소유 집합**을 사용한다. 모든 16채널의 최대·평균·화면 환산·초과 수와 국소 위상 ms는 로그에 있다.

|시점|최종 소유 / awake|곡 시계 정점 최대 / 평균 px|실제 앵커 정점 최대 / 평균 px|0.01px 초과|최종 RGBA 최대 / 평균|
|---|---|---|---|---|---|
|262 주|1730/2134, 81.0684%|42.51154843 / 1.92565580|0.00408344949 / 0.00000118019|0/6920|7.45058e-9 / 1.25044e-10|
|270 주|1773/2269, 78.1401%|176.14624709 / 2.42965828|0.00779684167 / 0.00000439754|0/7092|3.72529e-9 / 5.16405e-11|
|276 보조|17/24, 70.8333%|0 / 0|0 / 0|0/68|0 / 0|

RGBA의 1/255 초과는 세 시점 모두 0이다. 빠른 앵커 소스는 **1641/1641, 1771/1771**이 평균 한 프레임 **9.22750 / 9.34693ms** 안이며, 소스 위상 비율은 100%다. 정지/완료/폴백의 엄격한 값 기준도 통과했다. 국소 값/속도 환산이며, 별도의 실제 트리거 시작 위상 전체 이력은 `UNMEASURED`다. 276초는 awake<800 보조이며 실패 원인으로 삼지 않았다.

회전 8,200각도(-720..720도)의 행렬 계수 최대/평균 오차는 **4.62989e-7 / 6.26776e-8**이다. 반폭5·가로배율1000의 0/90/180/270/-180도 다섯 합성 정점 사례는 모두 0px였다. 임의의 모든 초대형 장식 각도를 0.01px로 증명한 결과는 아니다. [회전/번들 재로드 로그](../../tools/gpudeco-lab/evidence/round7-bundle.txt).

## 정지 화면 G1 연결과 판정

새 `GpuDecoSnapshot.cs`는 AUTOTEST에만 들어간다. `gpudeco bundle <파일>`과 **같은 일시정지 시점의 새 export** 뒤 `gpudeco on`을 허용한다. GPU 평가기는 랩과 게임이 같은 파일을 사용한다. 실제 GPU 정점/색 G0와 복사 버퍼를 확인한 후에만 원본 `forceRenderingOff`를 설정한다.

- 전체 활성 렌더러의 실제 정렬 키·재질 ID로 경계를 만든다. 비소유 렌더러가 끼는 곳에서 MeshRenderer/Raw 버퍼를 나누고, 각 멤버는 서브메시로 원본 재질을 빌린다. 구간 bounds의 z는 GPU 첫 정점과 같다. `_MainTex`는 서브메시별 원본 스프라이트, `_RendererColor`는 white, `_Flip`은 one이다. 새 게임 재질을 복제하지 않는다.
- 같은 재질 동률·SortingGroup·지원하지 않는 정렬 축/렌더러 상태는 원본을 숨기기 전에 거부한다. 각 실제 구간 버퍼를 readback해 공용 GPU 정점과 position/color/UV를 정확 비교한다. 정점은 초기 0 외에는 CPU에서 업로드하지 않는다.
- `gpudeco draw off`는 진단 그리기만 해제하고 다음 시점의 소스 관찰을 유지한다. `off`, 재개, 세대/맵 변경, shutdown/unload/객체 파괴에서도 원본을 복구한다. 프레임마다 전체 장식을 훑지 않으며, 새 전체 census와 readback은 정지 진단 준비에서만 한다.
- `gpudeco pixel 0|1|2`는 mask 없이 전체 캡처의 잡음·A/A2 픽셀과 상태 복원을 확인한다. 262·270초는 최대 채널 차이≤3, 다른 픽셀≤0.3%, 소유율≥70%가 필요하다. 276초는 보조다. 누락·중복·주 시점 밀도 부족·G0 실패·불안정 기준·복구 실패로 PASS를 만들 수 없다. 차이가 크면 구역별 장식 ID/텍스처/재질/알파/z/블렌드 후보를 출력하며, 화면 bbox 겹침이라는 진단 한계도 표시한다.

새 [93단계 G1 JSON](../../tools/gpudeco-lab/evidence/round7-g1-steps.json)을 생성만 했다. 기본 생성기가 이제 실제 on과 끔→켬→끔, 전체 필터 체인을 포함한다. 이전 export 전용 JSON을 다시 실행해 G1 PASS를 기대하지 않는다. `--export-only`는 자료 수집만 필요한 경우에 남겼다. 게임 실행·설치는 외부 실행자의 범위라 이번에는 수행하지 않았다.

**이 연결은 일시정지 G1 진단용이다.** 진단 준비는 각 일시정지 시점에 일어나고 그 비용은 `prepare_scope=paused_diagnostic`로 별도 출력한다. 재생 중 원본 Rec 생성/Transform 쓰기 생략, Dormancy 전환 소유 구동, 재생 전 1회 준비 및 프레임≤0.3ms의 실시간 경로는 아직 구현/측정되지 않았다. 재개 때 GPU 소유를 해제한다. 정지 G1 PASS가 나와도 실시간 장식 CPU 제거·G2 성능 완료로 해석하면 안 된다.

## 검증과 남은 한계

공용 실행기 재검증: 합성 32,000채널/8,000정점 통과, Actual 정답 입력 독립·정지 시계 불변·한 소스 +7ms 분리 통과. 소스257/2000개 업로드+1dispatch CPU 제출 **0.0078056 / 0.0123070ms**. 80 Raw 버퍼/1,200서브메시/81dispatch 제출 평균 **0.1473152ms**, 다섯 묶음 **0.137851~0.167101ms**, 복사 불일치0. 큰 분리/통합 및 정렬 장난감 픽셀은 0, 음성 재질 역전은 2,304픽셀로 실패했다. 이는 합성 `Sprites/Default`이며 새 게임 렌더러/실제 셰이더/게임 FPS의 증거가 아니다. 서로 다른 제출 비용을 합산하지 않았다. GPU timestamp는 없다.

번들은 **55,640B**를 생성·재로드하고 이징101,475개 및 회전 경계를 통과했다. 3종 Release·LoadCheck·데이터/픽셀 판정·bake/live·DecoSim·framecmp·ld2a/geometry/delta·steps·SM5 검사 근거는 `round7-*`와 manifest에 있다. 일반 Player에는 GpuDeco 데이터/실행기/정지 진단이 없다. 게임 수명·원본 복구·실제 G1은 오프라인 검사로 대체하지 않았다.

|최종 검사|결과|
|---|---|
|Release Dev / Player / PlayerAuto|오류0/0/0, 경고38/4/38|
|LoadCheck|277/229/277곳, 불일치0/0/0|
|일반 Player / Dev / Auto의 정지 진단 타입|없음 / 없음 / 있음, 공용 GPU 실행기는 일반 Player에 없음|
|데이터·소유·픽셀 판정 계약|51개 통과, 기준 누락/중복/밀도 대체/복구 실패/허용 초과 음성 대조 포함|
|랩 C# / SM5|C# 오류·경고0, EaseProbe/Evaluate/CopyRun/RotationProbe 컴파일 통과|
|게임 G1 / 재생 수명·성능|미측정, 생성한 JSON도 실행하지 않음|

첫 샌드박스 실행의 UPM IPC exit1, 다음 entitlement exit198도 보존했다. 클라이언트 자연 종료 후 직접 Editor가 성공했다. 권한 확장으로 독립 랩만 실행했으며 게임·설치·라이선스 변경·강제 종료는 없었다.

`GPUDECO_G0_OFFLINE=PASS measured=3/3 primary=2/2 auxiliary=PASS`

`GPUDECO_G1=ERROR G0=PASS game_pixels=UNMEASURED reason=paused_runtime_pixels_required`

---

# 이전 gpudeco-1 결과 — codex-al-gg-5 (2026-10-22)

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
