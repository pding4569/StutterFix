# 화면 필터 합치기 연구 (2026-11-24 시작)

사용자 요청: 필터를 하나로 합치고 8비트 반올림 문제를 해결하거나 새로 만들기.

## 문제
GPU 병목 구간(예: Lost 120초)에서 본 카메라 필터가 10~15개 동시에 켜져 프레임당 2.75ms(본 카메라 4.8ms 중). 필터마다 화면 전체(2560x1600)를 읽고 쓰는 패스 하나.

## 같은 그림을 지키는 원리
- 중간 결과가 8비트(UNORM) 텍스처에 써질 때의 반올림을, 합친 셰이더 안에서 같은 규칙으로 재현한다:
  saturate → ×255 → GPU 와 같은 반올림 → i/255 의 정확한 float(표 256칸, 하드웨어 UNORM→float 변환과 같은 값).
  GPU 의 float→UNORM8 반올림 규칙과 UNORM8→float 값은 시험 그림으로 실측해 맞춘다. 중간 형식이 ARGBHalf 면 f32→f16→f32 로 재현.
- 픽셀 단위 필터(텍스처를 자기 위치에서만 읽음: 블릿에서 텍셀 중심 = 정확한 값)끼리만 합친다. 주변을 읽는 필터(블러·왜곡·색수차·외곽선)는 경계.
- 각 필터 계산은 게임 셰이더의 DXBC 역어셈블(d3dcompiler_47 D3DDisassemble)과 같은 명령 순서로 다시 작성한다(원본 코드는 배포하지 않음).
  위험: 원본 DXBC 의 refactoringAllowed 로 드라이버가 mul+add 를 FMA 로 합칠 수 있음 → 원본·재작성 모두 시험 그림 바이트 대조로 확인, 0 아니면 그 필터는 제외.

## 도구
- `E:\StutterFixWork\shaderlab\scripts\list_shaders.py`: 게임 셰이더 목록(CameraFilterPack 338개, resources.assets).
- `E:\StutterFixWork\shaderlab\scripts\dxbc_dump.py <이름...>`: D3D11 블롭 LZ4 풀기 → DXBC 찾기 → 역어셈블 `shaderlab\dump\*.asm`.
- 게임 안 `filtergpu on|state|off`: 필터별 GPU 시간(timestamp), `camfx`: 카메라별 켜진 필터 목록.

## 단계
1. 맵별 필터 GPU 시간·동시 켜짐·순서 조사 (filtergpu, camfx).
2. 상위 필터 역어셈블 → 픽셀 단위 판정, 사용 상수(_MainTex_ST 등) 정리.
3. GPU 반올림 규칙 실측(시험 그림 fixture).
4. 필터별 재작성 → 원본 머티리얼과 바이트 대조(무작위 그림·매개변수).
5. 연속된 픽셀 단위 필터 묶음을 한 패스로(OnRenderImage 사슬 가로채기), 맵 실측(ABBA) + culltest 식 픽셀 대조.

## 결과 (2026-11-24 저녁)
- 1단계(filterchain, 5맵 전곡): 중간 RT 는 모두 ARGB32. 연달아 놓인 픽셀 단위 필터를 합쳐 줄일 수 있는 패스는 필터 있는 프레임당 WF 0.32·Lost 0.56·HELLO 0.79·7777 0.85개, 패스 하나 약 39µs → 곡 GPU 시간의 0.4~0.9% 상한. 합치기는 보류.
- 분류(shaderlab/classify.py): 338개 중 픽셀 단위 64, 공간 211, 샘플 없음 21(휴리스틱).
- 큰 비용은 공간 필터(WF Blur_Movie 곡 GPU 34초=곡 시간 9.4%). 하드웨어 쌍선형 보간+비선형 가중치라 같은 결과로 빠르게 만들기 어렵다.
- 대신: WF 에서 Blur_Movie 가 Radius=0(효과 없음)으로 켜진 호출이 32%. Radius==0·FastFilter==1·Factor>=0 이면 100샘플이 같은 텍셀 → Σ(c·w)/Σw = c(1±6e-6) → 8비트로 원래 바이트와 같음(증명), 같은 입력 대조 83표본 최대 0/255.
  FilterIdentity 에 "증명된 건너뛰기"(ProvenEnabled 기본 켬, 인증서 불필요, TimeX 부수효과 재현, ARGB32·같은 크기·대상 null 아님만).
  WF 전곡 ABBA(끔/끔/켬/켬/끔, 첫 판 데우기): 381/392/394/382 → +3.0%, 1% low 181→188, 건너뜀 곡당 약 1.9만 번.
