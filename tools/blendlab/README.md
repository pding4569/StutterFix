# blendlab

독립 D3D11 정확도 실험실. 게임 실행·DLL 설치·제품 설정 변경 없음. GPU 시간/FPS 벤치가 아니다.

```powershell
cmd /c tools\blendlab\build.bat
& tools/blendlab/blendlab.exe "$((Get-Location).Path)\tools\blendlab\lab.hlsl" > tools/blendlab/result.txt
py -3.12 tools/blendlab/analyze.py
py -3.12 tools/blendlab/inspect-assets.py
```

`build.bat`은 기존 native 실험실의 로컬 MSVC/SDK 경로를 사용한다. 실행 파일은 현재 디렉터리와 무관하게 HLSL 절대 경로를 인자로 받는다. 하드웨어 어댑터만 사용하며 WARP로 대체하지 않는다. API/셰이더 오류·반복 LUT 불일치는 비정상 종료한다. 산술 후보 불일치 자체는 조사 결과다.

`result.txt`는 원시 계수·고정점 목록, `mismatches.csv`는 첫 실패 입력/최대 오차를 포함한 200조건 표, `summary.json`은 집계다. analyze.py는 PowerShell 5의 UTF-16 리다이렉션도 읽어 UTF-8로 정규화한다. 에셋 검사는 이미 있는 `out/asset-reader-deps`의 UnityPy를 쓰며 패키지를 설치하지 않는다.

모드: 0=PMA One/InvSrcAlpha, 1=straight SrcAlpha/InvSrcAlpha 비교군, 2=Legacy SrcAlpha/One RGB-only, 3=GpuDeco 가산 (s*a,0) One/InvSrcAlpha, 4=One/One 비교군.

입력: dst와 source color 0..255, alpha 0..255를 모두 조합한 16,777,216픽셀/모드. 추가 alpha float는 5.1f/255, 0.1234567f, 0.5001f이며 각각 65,536픽셀. PMA RGB는 PS에서 sourceColor*alpha로 출력한다. RGBA의 동일 dst 바이트를 사용하며 RGB 채널은 같은 값이다. 독립 RGBA 패턴, signed/HDR/sRGB/MSAA, 텍스처 필터링, 셰이더 fixed/half 출력과 모든 가능한 float 값은 이 실험의 검증 범위 밖이다.

후보: 0=float mul/add 뒤 round-even, 1=+0.5 floor, 2=소스/인자 255단계 양자화, 3/4/5=256/512/1024단계 양자화, 6=8비트 정수 곱합/255, 7=8비트 인자를 0..256으로 확장한 shift, 8=float 곱항별 반올림 후 합, 9=8비트 정수 곱항별 반올림 후 합. 모든 식은 포화한다. 이 후보군 전체의 실패는 다른 식의 존재를 부정하는 증명이 아니다.

반복: float 알파 3종 × 모드 5종 × source color/dst 256²의 측정 ROP 전이표를 만든다. k=1은 그 전이표 정의이며 독립 검증이 아니다. k=2..255에서 매번 실제 draw 후 readback한 RGB/A와 CPU LUT 합성을 비교한다. LUT 생성/합성의 GPU 실행·성능은 검증하지 않았다. 고정점 목록은 RGB 채널, source color 0/128/255 기준이다.
