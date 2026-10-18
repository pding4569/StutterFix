# Codex 작업 C: 화면 결과가 100% 같은 GPU 절약 점검 (조사 + 확실한 것만 구현) — 게임 실행·Mods 설치·커밋 금지

선행: 작업 A·B 가 끝난 뒤. 결과: `docs/handoff/codex-gpufree-20261019-result.md` (**apply_patch 로 UTF-8 BOM 으로 쓰고 한글이 남아 있는지 `Get-Content -Encoding UTF8` 로 확인**), 한국어, 추측은 "추정". 빌드 셋 오류 0. 구현하는 것은 모두 `gpufree-off` 파일로 끌 수 있고 기본 켬은 **그림이 비트 동일**함을 증명한 것만.

## 배경 (Claude 실측, Arche 곡 258~268초, 3440×1440 D3D11, 필터 포함 GPU 약 4.7ms)

- `gpuprobe`: 본 카메라(`Camera`) GPU 3.7ms(프레임 전체 3.9), 필터 합 약 0.8ms(각 0.04~0.15), BGMoving/Overlay/BGStatic 합 0.17ms. 해상도 50% 시험(카메라 RT 1720×720 확인): 본 카메라 3.74 → 1.58ms → **픽셀 비례 약 2.9ms(78%)**, 고정 약 0.9ms. census: 후보 390~1,159개, 전부 `MaterialPropertyBlock` 1개 + `Sprites/Tile` 셰이더, 재질 351개·텍스처 36종(주로 fog.png 110·nes_large_fog 94 반투명 안개), 경계 상자 합 = 화면 93배(상한).
- 이미 폐기(0 byte 규칙): 재질 공유·텍스처 아틀라스(동률 정렬 144조건 중 99/96 불일치, 표본화 1,782/2,592 불일치), Tight/격자 메시(990조건 중 832 불일치), alpha≤8 건너뛰기(그림이 달라짐), 화면 밖 건너뛰기(CPU 쪽, 화면 안이 77%). 그림이 조금이라도 달라지는 것은 하지 않는다.
- 이미 있는 것: `FilterIdentity`(효과 없는 필터 건너뜀), `InvisibleSkip`(알파 0 장식 forceRenderingOff), `TileCull`(화면 밖 타일 렌더러 끔), `ShaderWarm`.

## 점검할 것 (각각: 비트 동일 가능성, 예상 절약 ms, 위험; 확실하고 그림이 같은 것만 구현)

1. **카메라 RT 의 깊이/스텐실 버퍼**: census 의 `depth=32`. 2D 스프라이트 장면이 깊이 쓰기/테스트를 쓰나(`Sprites/Tile`·`FloorMesh` 셰이더의 ZWrite/ZTest/Stencil, SpriteMask 가 스텐실을 쓰나)? 안 쓴다면 깊이 비트를 줄이거나 0 으로 만들면 대역폭·클리어가 준다 — 그림은 같아야 한다. `scrCamera.SetupRTCam`(IL: `docs/handoff/codex-gpu-20261018-camera-il.txt` 참고)에서 RT 를 어떻게 만드는지, 바꿀 수 있는 안전한 지점(모드 Harmony)과 모든 카메라(BGStatic/BGMoving/Camera)에 대해 필요 여부.
2. **RT 형식·클리어**: 카메라별 클리어 플래그(`Camera.clearFlags`, 배경색 불투명 단색이면 클리어 후 첫 불투명 전체화면 그리기 하나로 대체 가능한 경우 등), RT 를 매 프레임 `Discard`/`DontCare` 로 시작할 수 있나(타일링 GPU 에서 이득; 데스크톱 NVIDIA 에서는 이득 작음 — 근거 적기). MSAA 꺼짐 확인.
3. **블릿 체인**: 최종 화면까지 `Graphics.Blit`/`OnRenderImage` 가 몇 번 도는지(필터 약 10개 각 전체화면 복사 + 최종 출력 + FPS 화면 + 오버레이 카메라). 필터 사이 임시 RT 할당·복사가 줄일 수 있는 것(같은 결과를 내는 병합: 예를 들어 같은 해상도·같은 형식에서 두 번 복사하는 부분)과 `RenderTexture.GetTemporary` 풀 동작. 필터 하나하나(`CameraFilterPackLegacy_*`)의 셰이더가 실제로 하는 일을 보고 설정에 따라 **출력이 입력과 비트 동일**이 되는 경우(`FilterIdentity` 가 이미 처리하는 목록과 비교, 빠진 것).
4. **텍스처 샘플러**: 안개 이미지(fog.png 682×682 RGBA32 mips=1 등)의 이방성/필터/밉맵 설정이 불필요하게 비싼가(Anisotropic 1 이므로 아님), 텍스처 형식이 DXT 가 아니라 RGBA32 라 대역폭이 큰 것(DXT 압축은 손실 → 하지 않음. 대신 RGBA32 에서 같은 값을 더 작은 형식으로 무손실로 줄일 수 있나: 알파 전용/R8 같은 형식이 가능한 이미지(회색조+알파)? 안개는 흰색 RGB + 알파일 가능성 — 실제 픽셀 검사(`D:\얼불춤 맵 파일\Arche\` 의 fog.png, nes_large_fog.png, nev_fog.png, nes_black_fog.png): RGB 가 모든 픽셀에서 같은 상수이고 알파만 변하면 알파 전용 텍스처(R8)+상수 색 셰이더로 대역폭 1/4 이지만 셰이더 키워드/게임 코드(SetPointSamplingVariant, 색 곱) 때문에 그림이 같은지 증명해야 함 — 가능성 평가만, 구현은 증명이 확실할 때만).
5. **프레임 제출·플립 모드**(이미 조사됨 `CLAUDE.md` "화면 출력 실험": 독점 전체화면 Independent Flip 시작→화면 15.8→7.9ms 이나 FPS 209→187): 이 구간처럼 CPU 병목일 때는 무관, GPU 병목으로 바뀌면 의미. 참고만.
6. **GPU 클럭/전력 상태·PCIe/VRAM 이동**: 모드로 바꿀 수 없는 것은 "불가" 로 분류만.
7. 그 밖에 Codex 가 `gpuprobe`/`gpucensus` 결과(`%LOCALAPPDATA%\StutterFix\gpu-census-*.tsv`), `docs/handoff/codex-gpu*-result.md` 를 읽고 찾은 **그림이 같은** 절약 후보.

각 항목에 대해 (a) 절약 가능 ms 추정과 근거(해상도 모델 `C(s)=A+B·s²`, A≈0.9, B≈2.9ms), (b) 그림이 같음을 어떻게 증명/검증하는지(독립 Unity Editor D3D11 fixture 가 필요하면 `tools/gpuprobe/Gpu3Fixture.cs`·`Gpu4Tight.cs` 방식으로 비트 비교), (c) 구현 위치와 위험, (d) 구현했다면 `gpufree verify` 같은 검증 명령. 가능한 것이 없다면 "불가"로 명확히 결론 내리고 근거를 적어라(밤새 헛수고를 막기 위해).

Claude 가 게임에서 돌릴 순서(Arche 258~268초 `gpuprobe` 새 프로세스 A/B 켬/끔, 같은 곡 시각 `shotat` 두 장 픽셀 비교 — 같은 입력 상태 문제는 결과에 따라)를 적어라.
