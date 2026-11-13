# 겹친 층 더미 조사·오프라인 시제품 결과 (2026-11-13)

요청한 로컬 준비를 완료했다. 커밋·푸시·게임 실행·설치 DLL 교체는 하지 않았다.
기존 `71d10ed`의 zcull 계측을 재사용했으며, 게임에서 7개 맵을 실행하는 단계는 인계대로 Claude에게 남겼다.

## 결론

**float로 합쳐 마지막에 한 번 저장하는 경로는 층마다 RT에 저장하는 경로와 픽셀 동등하지 않다.**
실제 PNG를 사용한 512×512 모사에서 ARGB32 무늬 배경의 RGB 최대 오차는 10/255,
차이 >2인 픽셀은 5.349%였다. ARGBHalf에서도 같은 입력의 RGB 최대 오차가 0.374/255로 남았다.
이는 오프라인 저장 모델의 결과이며, 실제 게임 픽셀·성능 측정은 아니다.

또한 **실제 `pt2_circle1.png`는 속이 찬 원이 아니다.** 가운데가 빈 방사형 선 무늬다.
이 이미지에 `원의 내부 alpha=1 → T=0.98^k` 생략을 적용할 수 없다.
파일 이름이나 기존 설명만으로 찬 원을 가정하지 않도록 실제 알파 분석과 대조용 부드러운 원 시험을 함께 남겼다.

## 구현 내용

### 1. DEV/AUTOTEST 전용 더미 census

`ZCull.cs`의 기존 GPU readback 결과에서 `Total`을 재사용한다. 별도 GPU 패스는 추가하지 않았다.

- 기존 정렬 순서에서 바로 이웃한 draw만 연결한다. 같은 sharedMaterial 객체·sprite.texture 객체·색 RGB가 필요하다.
- 기존 `SafeSprite`가 인정한 일반 sprite 경로만 허용한다. 정렬 동점, 정렬 그룹, 비일반 블렌드,
  마스크, 알 수 없는 셰이더/키워드/패스, property block, packed/alpha split 등은 보수적으로 제외한다.
- 부적격 draw는 더미를 끊는다. 알파는 유한한 [0,1] 값이어야 하며 RGB는 정확히 비교한다.
- 2층 이상 더미마다 층 수·geometry fragment 합계·전체 대비 비율·알파 histogram·월드 AABB 폭/높이 범위·order·재질/텍스처 ID를 기록한다.
- 요약은 **4층 이상** 더미만 합산한다.

```text
[stack] fragments_total=F in_stacks=S (x%) stacks=N top: tex=... layers=... frags=...
```

추가로 메인 카메라의 `targetTexture.format`, `graphicsFormat`, `sRGB`, `allowHDR`와
`scrCamera.camRT` 자체의 형식·graphicsFormat·sRGB를 기록한다.
기존 계측과 동일하게 fragments는 single-sample **기하 커버리지**다. 투명 texel도 포함하며 하드웨어 fragment counter가 아니다.
분류된 draw가 실제로 서로 겹치는 면적, 텍스처 RGBA의 공간적 일정성, 시제품 연결 적격성은 증명하지 않는다.
property block 등 제외 때문에 실제 가능한 더미를 적게 셀 수 있다. 0개가 나오면 제외 조건부터 확인한다.

### 2. 실행기 준비

`E:\StutterFixWork\zcull_run.py`를 수정했다. 기존 hl/dd/s7/wf/sh/ar 키도 유지한다.

| 새 키 | 맵 | 시각 |
|---|---|---:|
| hl50 | HELLO (BPM) 2026 | 50초 |
| hl86 | HELLO (BPM) 2026 | 86초 |
| dd55 | DDONGSSADA3302 asansis remake | 55초 |
| s7120 | 7777 | 120초 |
| wf260 | Windflower | 260초 |
| sh88 | Exit from SHiP | 88초 |
| ar262 | Arche | 262초 |

맵 경로는 기존 `bench_v14.MAPS`를 참조한다. 새 기본값은 위 7개다.
필터에 `[stack]`을 추가했고, 상세 로그가 `--tail 40` 밖으로 밀려도 전체 `.log`에서 요약을 읽도록 고쳤다.
TSV에 `stack_fragments`, `stack_percent`, `stacks_ge4`를 추가했다.
stack 요약이 없는 과거 로그나 총 조각 수가 일치하지 않는 로그는 새 결과로 인정하지 않는다.

`--prepare-only`로 7개 계획을 생성했고, 시각·GpuDeco=0·RunLod=false·census 명령을 검증했다.
실행은 하지 않았다. Claude 실행 명령:

```powershell
python E:\StutterFixWork\zcull_run.py --maps hl50,hl86,dd55,s7120,wf260,sh88,ar262 --worktree wt-v14prod
python E:\StutterFixWork\zcull_run.py --summarize --maps hl50,hl86,dd55,s7120,wf260,sh88,ar262
```

위 첫 명령은 게임·DLL 설치/복원 수명주기를 실행하므로 **Claude가 실행할 것**.
계획 파일: `E:\StutterFixWork\night-results\zcull-<key>-steps.json`.
결과: 같은 폴더의 `zcull-<key>.log`, `zcull-summary.tsv`.

### 3. 오프라인 오차 시험

`tools/stackfold/compare.py`, `test_compare.py`, `README.md`를 추가했다.
150장·색 흰색·알파 0.02·크기 20.0→5.1·작은 회전/이동을 사용한다.
`20-0.1*i` 150장이므로 마지막은 5.1이다. 5.0까지 포함하면 151장이 된다.
쌍선형 RGBA 샘플과 각 층 float32 블렌드 후 UNORM8 또는 binary16 저장을 비교한다.
반대편은 같은 샘플과 순서로 float32 누적 후 한 번 저장한다.

실제 이미지:

```text
D:\얼불춤 맵 파일\HELLO (BPM) 2026\pt2_circle1.png
SHA256 ee80118b6f3c3c8615610929c71138c3bdf3adc0742e856d50f0cc2009f18564
4642 x 4642, RGB 전체 255
```

결과는 `tools/stackfold/results/results.json`과 sequential/folded/diff8x PNG 36개에 저장했다.
아래는 **RGB** 통계다. SrcAlpha/OneMinusSrcAlpha와 premultiplied 관례의 RGB 결과가 같으므로 묶었다.
최대 오차 단위는 1/255, 비율은 픽셀에서 최대 채널 차이가 해당 값을 초과하는 비율이다.

| RT 모델 | 배경 | 최대 차이 | >0 비율 | >1 비율 | >2 비율 | PSNR dB |
|---|---|---:|---:|---:|---:|---:|
| ARGB32 | 검정 | 3.000 | 7.731% | 0.279% | 0.003% | 58.795 |
| ARGB32 | 흰색 | 0 | 0% | 0% | 0% | ∞ |
| ARGB32 | 무늬 | 10.000 | 26.021% | 10.994% | 5.349% | 50.674 |
| ARGBHalf | 검정 | 0.125 | 6.649% | 0% | 0% | 91.870 |
| ARGBHalf | 흰색 | 0 | 0% | 0% | 0% | ∞ |
| ARGBHalf | 무늬 | 0.374 | 19.957% | 0% | 0% | 77.496 |

**RGBA도 별도로 JSON에 기록했다.** RGBA 전체에 SrcAlpha를 쓰면 source alpha 기여가 `a²`이고,
premultiplied Blend One이면 `a`다. 흰 배경에서 RGB가 동일해도 straight 관례에서는
alpha 오차가 ARGB32 최대 3/255, ARGBHalf 최대 0.374/255다.
premultiplied 관례에서는 위 입력의 RGBA 최대 오차가 RGB 최대 오차와 같다.
따라서 RGB의 흰색 결과 0을 전체 RGBA 동등성으로 해석하면 안 된다.

UNORM 기본 반올림은 nearest-even, half는 IEEE binary16이다.
이는 Unity GPU의 저장을 가정한 모델이며 실제 하드웨어의 동점 처리·내부 블렌드 정밀도·FMA까지 검증한 것은 아니다.
sRGB 변환·압축/축소 텍스처·tight sprite mesh·실제 카메라/변환/시각은 재현하지 않았다.
실제 게임의 오차 크기나 PSNR로 옮겨 쓰면 안 된다.

대조용 부드러운 원(256×256 화면)도 `results-soft-circle/`에 남겼다.
ARGB32 검정 최대 13/255, 무늬 최대 24/255; ARGBHalf 무늬 최대 1.743/255였다.
내부 알파=1인 픽셀 2,117,431개 샘플에서 분석적 내부 판정의 알파 오차는 0이었다.
찬 원에서는 내부 샘플 생략이 가능해도 저장 반올림 동등성은 별개라는 근거다.

### 4. 실제 텍스처 형태와 다른 분해 시도

- 알파가 0보다 큰 texel 3.4731%, 정확히 255인 texel 2.9864%.
- 중앙 알파=1 원 반지름 **0**. bilinear 여유를 뺀 중앙 투명 반지름은 UV 0.0855166.
- nonzero texel과 bilinear footprint를 포함하는 외곽 반지름은 UV 0.5004481.
- 90도 회전 시 알파 불일치 texel 6.9462%: 90도 회전 동일 이미지로 대체할 수 없다.
- 일반 이미지 대안으로 32×32 texel 사각형에 사방 1texel 여유를 붙여 검사했다.
  전체 21,316개 중 완전 투명 18,198개(85.37%), 완전 불투명 0개, 혼합 3,118개.
  투명 영역은 사각 분해로 건너뛸 여지가 있으나, 알파=1 사각 내부 생략은 이 분해 크기에서는 없다.
  별도 분류표 읽기/기하 분해·제출 비용과 더 작은 사각형은 미측정이다.

위 수치는 텍스처 공간 분석이다. geometry fragments 중 실제 절약률이나 성능 이득이 아니다.

### 5. GPU 시제품 초안

`tools/stackfold/StackFold.shader`는 최대 256개의 역변환 2행·층별 색·공통 RGBA 텍스처를 받아 순서대로 float 누적한다.
보수적인 찬 원 내부에서만 RGBA 샘플을 생략하고, 중앙 투명 원과 외곽 바깥도 생략할 수 있다.
실제 이미지는 opaque core를 끄고 일반 샘플로 처리해야 한다.

별도 `_DstTex`에 더미 직전 배경을 받아 Blend Off로 최종 RGBA를 쓴다.
SrcAlpha의 alpha 제곱과 premultiplied alpha를 선택할 수 있게 했다.
배경 복사, RT 좌표 뒤집기, 머티리얼/MPB/atlas/tight mesh/Repeat 연결은 미구현이다.
게임·프로젝트 리소스 번들에 연결하지 않았다.

`check_shader.py`로 D3DCompile ps_5_0의 fragment HLSL 컴파일을 통과했다(3080바이트).
Unity include 대신 UV 구조체를 제공한 **독립 fragment** 검사다. ShaderLab import·GPU 실행·속도는 확인하지 않았다.
내부 샘플 생략 시에도 층별 변환/판정 루프는 남으므로 한 패스가 더 빠르다고 단정하지 않는다.

## 검증 및 상태

| 검사 | 결과 | 근거 |
|---|---|---|
| DEV 빌드 | 오류 0, 경고 84 | `out/stackfold-dev-build.txt` |
| Player 빌드 | 오류 0, 경고 57 | `out/stackfold-player-build.txt` |
| Player AUTOTEST 빌드 | 오류 0, 경고 57 | `out/stackfold-auto-build.txt` |
| 샘플/반올림/half/alpha/지표 독립 검사 | 5개 통과 | `python -m unittest discover -s tools/stackfold -p test_compare.py -v` |
| 실제 이미지 오프라인 12조건 | 완료 | `tools/stackfold/results/results.json`, `out/stackfold-compare.txt` |
| 대조용 부드러운 원 12조건 | 완료 | `tools/stackfold/results-soft-circle/results.json` |
| 셰이더 독립 HLSL 컴파일 | 통과 | `out/stackfold-hlsl-check.txt` |
| 실행 계획 7개 | 시각/설정/명령 검사 통과 | `night-results/zcull-*-steps.json` |
| 실행기 요약 파싱 | tail 밖 전체 로그·stack 열·명시적 실패 거부 검사 통과 | 임시 디렉터리 합성 로그로 검사, 실제 결과 변경 없음 |
| git diff --check | 통과 | 변경 소스 공백 검사 |
| 게임 census/GPU 시간/FPS/픽셀 대조 | **미실행** | Claude 실행 단계 |

DEV 빌드 확인 중 기존 `srcdebug` 명령이 AUTOTEST 전용 `DecoBake` 멤버를 무조건 참조해서 오류 9개가 발생했다.
`AutoTest.cs`의 해당 case에 동일한 `#if AUTOTEST` 조건을 추가해 수정했다.
프로파일 멤버나 런타임 동작을 확장하지 않았다.

실제 camRT 생성은 설치된 게임 `scrCamera.Update`를 읽어 확인했다:
`new RenderTexture(Screen.width, Screen.height, 24)`.
설치된 UnityEngine.CoreModule의 같은 3인자 생성자는 `RenderTextureFormat.Default`로 위임한다.
**명시적 ARGBHalf가 아니며, 코드만으로 최종 GPU graphicsFormat을 ARGB32로 확정하지 않았다.**
read-only 근거는 `out/stackfold-scrCamera.txt:306`, `out/stackfold-RenderTexture.txt:921`.
실행 시 새 `[stack] game_camRT=...` 로그로 최종 형식을 확인해야 한다.

남은 실측은 Claude의 7개 맵 census다. 그 결과로 더미 기하 비중과 최종 RT 형식을 판단할 수 있다.
현재 결과로 한 번 float 누적하는 경로를 픽셀 보존 최적화로 채택할 근거는 없다.
