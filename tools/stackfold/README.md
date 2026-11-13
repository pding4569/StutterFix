# StackFold 오프라인 조사

게임 실행·설치·DLL 교체 없이 실행한다. Python 3, NumPy, Pillow가 필요하다.

```powershell
python tools/stackfold/compare.py --size 512
python tools/stackfold/compare.py --texture tools/stackfold/nonexistent-circle.png --size 256 --out tools/stackfold/results-soft-circle
python -m unittest discover -s tools/stackfold -p test_compare.py -v
python tools/stackfold/check_shader.py
```

`--texture` 파일이 없으면 부드러운 원을 생성한다. 기본 경로에서는 실제 HELLO 이미지가 사용됐다.
`--rounding half_up`으로 UNORM 반올림 동점 규칙의 민감도를 따로 조사할 수 있다.

## 입력과 수치 모델

- 150장, 크기 `20 - 0.1*i`(20.0→5.1), 알파 0.02, 회전 `i*0.37`도,
  이동 `(0.18*sin(i*0.31), 0.18*cos(i*0.27))`. 앞에서 뒤로 배열한 것이 아니라 **실제 그리는 순서**로 i가 증가한다.
- 정사각 정사영 시야 24단위, 검정·흰색·체크와 그라데이션 배경. 실제 게임 변환을 추출한 입력은 아니다.
- 원본 RGBA 전체를 쌍선형 샘플한다. 정규화 UV의 texel 중심은 `(i+0.5)/size`,
  clamp-to-edge, mip 0, 사각형 바깥은 미포함이다. 실제 tight sprite mesh, Repeat,
  압축·축소 텍스처, sRGB 변환, MSAA, FMA, GPU의 블렌더 내부 정밀도는 재현하지 않는다.
- ARGB32: 매 블렌드 후 `[0,1]` clamp → `round(value*255)/255`.
  기본은 nearest-even이다. ARGBHalf: 매 블렌드 후 IEEE binary16 저장 후 float32 복원.
  한 번 계산하는 쪽은 같은 샘플로 float32 누적 후 끝에 한 번 저장한다.
- 반올림은 **가정한 저장 모델**이다. Unity/드라이버의 실제 반올림 동점 규칙을 측정했다는 뜻이 아니다.
- `straight`: RGB는 `tex.rgb*tint.rgb*a + dst.rgb*(1-a)`,
  alpha는 `a*a + dst.a*(1-a)`(RGBA에 SrcAlpha/OneMinusSrcAlpha).
  `premultiplied`: RGB는 같고 alpha는 `a + dst.a*(1-a)`.
  RGB 공식이 같아도 alpha 공식이 다르므로 두 결과를 나눠 기록한다.
- JSON에 RGB/RGBA 각각 최대 오차(1/255 단위), 픽셀의 최대 채널 차이가 >0/>1/>2인 비율,
  모든 채널의 MSE에서 얻은 PSNR(dB)을 저장한다. `psnr_db: null`은 동일해서 무한대라는 뜻이다.
  ARGBHalf 통계는 PNG로 내보내기 **전** half 값끼리 비교한다.

## 실제 이미지의 차이

`pt2_circle1.png`는 4642×4642 흰 RGB의 방사형 선 무늬다. 가운데는 투명하다.
중앙의 알파=1 원 반지름은 **0**이므로 찬 원의 `T=0.98^k` 내부 생략을 적용하면 안 된다.
코드가 구한 보수적인 알파=1 반지름은 bilinear footprint를 빼며,
중앙 투명 반지름과 외곽 투명 반지름은 반대 방향으로 여유를 준다.
90도 회전 비교와 32×32 texel 사각형(+모든 방향 1texel 여유)의 알파 min/max도 기록한다.
일반 이미지의 투명 사각형 분해는 가능하지만 이 파일에서 알파=1 사각형은 0개다.
분해율은 화면 조각 절약률이나 GPU 성능 측정값이 아니다.

## 셰이더 계약

`StackFold.shader`는 배포 번들/게임에 포함되지 않은 초안이다. 최대 256장의 화면UV→spriteUV
역변환 2행, 개별 색과 공통 텍스처를 받는다. `_DstTex`는 더미 직전의 별도 배경 복사본이다.
배경 RT 자체를 읽고 동시에 쓰면 안 된다. 복사 비용·대역폭은 미측정이다.
back-to-front float 누적 후 한 번 출력하므로 매 층 RT 저장 반올림과 같다고 보장하지 않는다.

`_Core.xy=(0.5,0.5)`, `_Core.z=opaque_radius_uv`. RGB까지 일정한 원 내부가 확인됐을 때만
`_Core.w=1`, `_CoreColor=그 RGB`로 둔다. 실제 이미지는 `_Core.w=0`을 사용해야 한다.
`_EmptyInnerRadius`는 중앙 투명 반지름, `_OuterRadius`는 외곽 투명 반지름이다.
사용하지 않을 때는 `_EmptyInnerRadius=0`, `_OuterRadius=0`, `_Core.w=0`으로 둔다.
sampler는 원본과 동일한 bilinear/clamp, mip 0을 사용한다.
`_Premultiplied`는 실제 소스 셰이더의 alpha 블렌드 의미에 맞춘다.

알파=1 원 내부는 샘플을 생략하지만, 루프의 변환·내부 판정은 여전히 층마다 한다.
실제 방사형 이미지는 일반 샘플 경로로 돌아간다. packed/atlas/tight mesh/Repeat,
sprite flip, 머티리얼 tint·property block, 화면 좌표/RT 뒤집기는 호스트에서 별도 대응이 필요하다.
현재 census 적격이라고 바로 이 셰이더에 연결할 수 있다는 뜻은 아니다.

`check_shader.py`는 Windows `d3dcompiler_47.dll`로 fragment를 ps_5_0 컴파일한다.
Unity include 대신 UV 구조체를 제공하므로 HLSL 문법/자원 한도 확인 범위다.
ShaderLab import·게임 픽셀·실제 GPU 시간 검증은 아니다.

전체 결과와 Claude 실행 인계는 `docs/handoff/stack-20261113-result.md`를 참고한다.
