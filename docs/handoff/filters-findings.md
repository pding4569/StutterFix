# 필터 GPU 조사

2026-10-15. 조사·검증·복원 완료. 새 플레이어용 필터 최적화 채택 0개. 커밋·푸시·릴리스 없음. FrameGen 및 ScreenEffects/셰이더 소스 변경 없음.

## 변경

- 설정창의 중립 필터 기능 제목·설명에 실제 ASCII 물음표가 들어 있었음. 영어 문구와 FilterIdentity의 동작에 따라 "중립 필터 건너뛰기", "픽셀 대조를 통과한 필터만 중립 값일 때 건너뜁니다."로 복구.
- FilterIdentityProbe의 관측 로그와 WasteProbe의 깨진 주석도 복구. 변경한 C#은 UTF-8 BOM.
- tools/check-ui-encoding.py: 문자열 토큰 안의 연속 물음표 검사. C#의 null 병합 연산자 ??는 제외. 빌드 후 실행하며 SFENC001 경고. 전체 소스 검사 통과.
- 한·영 ui 1.1 캡처를 실제로 열어 확인. filters-evidence/filters-warm-ko.png, filters-warm-en.png. 초기 ui 1.2 캡처는 다른 탭이므로 검증에서 제외.
- FilterGpuProbe 및 tools/filtergpu/query.cpp: 개발용 전용 filtergpu on|state|off|reset. D3D11 disjoint와 timestamp를 512슬롯에서 재사용. 플러그인 이벤트를 원래 OnRenderImage 앞뒤에 실행. DONOTFLUSH로 준비된 결과만 읽으며 대기 루프·Flush 없음. 독립 검증 프로그램에서만 Flush를 사용.
- DEV/AUTOTEST에서만 빌드. 별도 filtergpu.dll로 제공하며 정식 DLL에 포함하지 않음. 명시적 on 전에는 네이티브 호출·Harmony 패치·MonoBehaviour 없음. off 및 Main.Unload에서 패치·렌더 자원 해제.
- FilterHalfProbe: 같은 콜백의 원본 출력, 원본 반복 출력, 반해상도 출력을 비교. 필드 값·난수 상태를 호출 직전 상태로 맞추고 끝나면 원본 호출 이후 상태로 복원. 반복 원본 차이가 0인 표본만 판단에 사용.
- FilterHalfPerf: 읽기·reflection 대조를 하지 않는 별도의 개발용 반해상도 경로. 성능 판에서만 사용. 채택된 사용자 설정이 아님.

## 판정 기준과 한계

화질 채택 기준: 유효한 모든 표본에서 최대 채널 차이 <= 2/255이며 PSNR >= 45dB. 최소 8표본이 모두 통과해야 채택 후보. 문서의 PSNR 단독 통과는 예시였으며, 타일·글자의 국소적인 차이를 평균으로 가리지 않도록 이번 채택에는 최대 차이도 제한한다. PSNR이 45dB 이상이어도 최대 차이가 큰 후보는 채택하지 않는다. RGBA 8비트 읽기 결과에 대한 수치이며 HDR 원본 전체 범위 오차를 증명하지 않음. 불통과 후보는 사용자 설정으로 추가하지 않는다.

GPU timestamp 구간은 해당 필터가 제출한 GPU 작업의 경과 시간이다. 계측 이벤트·GPU 스케줄링의 영향을 포함할 수 있다. 특히 계측 판 FPS·30ms 프레임 수는 원래 게임 성능의 기준으로 사용하지 않는다. 픽셀 읽기 판 역시 성능에서 제외한다.

원래 게임의 Blur_Movie 정수 0 나누기 예외는 그대로 둔다. 예외가 난 호출도 finalizer에서 쿼리를 닫으며, 예외를 숨기거나 성능 해결로 주장하지 않는다.

첫 HELLO 판은 곡 종료 뒤 대기까지 분모에 포함된 탐색 판이고 독립 네이티브 시험도 종료 후 대기 구간에 실행됐다. 첫 Windflower 판은 180초 구간 판이다. 최종 통계는 final-hello.log, final-windflower.log, arche.log만 사용한다. 최종 HELLO/Windflower는 waitsong으로 실제 완주까지 기다린다.

필터 시간 비율은 같은 타입의 여러 인스턴스가 있더라도 프레임당 한 번만 unscaledDeltaTime을 합산한다. 동시에 켜진 수는 필터 콜백 수로서 서로 다른 카메라의 같은 타입도 각각 센다. 0개 프레임은 전체 재생 프레임에서 비영 분포를 뺀 값이며 마지막 프레임 집계 경계에 1프레임 오차가 있다. GPU 결과는 몇 프레임 늦게 준비되므로 종료 직전 미회수 표본을 기다려 강제로 읽지 않는다. 합계는 회수된 표본 기준이다.

프레임 전체 GPU 시간 대비 비중은 기존 FrameTiming GPU 평균(로그의 0.1ms 반올림값)과 재생 프레임 수로 추정한다. 필터 timestamp와 동일 쿼리의 분모가 아니므로 근삿값이며, 정밀한 절대 점유율로 해석하지 않는다.

## IL 조사와 대체 범위

top-filter-il.txt에 상위 예비 후보의 호출/필드를 보존. Radial·Noise·BrightContrastSaturation·WideScreenHorizontal은 한 번의 Blit 경로이며 없앨 중간 RT가 없다. Movie는 FastFilter와 임시 RT 경로가 있다. 반해상도 실행을 실제로 대조한다. 샘플 수 감소와 다른 필터와의 결합은 출력 셰이더를 바꾸거나 구현해야 하므로 이번 셰이더 변경 금지 범위에서 채택하지 않는다. 서로 다른 순서의 필터 합성이 같은 결과라고 가정하지 않는다.

## 증거

- build-auto.log / build-player.log: 각각 오류 0. 기존 경고는 남아 있음.
- native-check.txt: 599표본, skipped=0, invalid=0, reset_and_shutdown=ok.
- original.dll / Settings.original.xml: 실행 시작 시 원본 바이트 보존.

## 최종 집계

전 판 D3D11, 3440×1440 165Hz, vSync 0, FPS 제한 없음, FrameGenOutside=0. 세 맵 모두 OnLandOnPortal 완주 로그 있음. 아래 FPS·GPU 평균·30ms 수는 계측 판의 관측치이며 최적화 전후 성능 수치가 아니다.

| 곡 | 평균 FPS | 프레임 GPU 평균 ms | 30ms 초과 | CameraFilterPack GPU 합계 ms | GPU 비중 추정 |
|---|---:|---:|---:|---:|---:|
| final-hello | 184 | 3.0 | 0 | 13994.039 | 13.6% |
| final-windflower | 231 | 2.9 | 0 | 70032.556 | 29.0% |
| arche | 341 | 1.7 | 0 | 0.000 | 0.0% |

Arche에는 CameraFilterPack 필터가 관측되지 않았다. 맵 원문에는 구형 SetFilter 이벤트 1,538개가 있으며 그 구현의 GPU 비용을 0으로 측정한 것은 아니다.

### 전체 GPU 합계 상위 5개

| 순위 | 필터 | 합계 ms |
|---:|---|---:|
| 1 | Blur_Movie | 30692.232 |
| 2 | Blur_BlurHole | 11654.459 |
| 3 | AAA_SuperComputer | 8987.402 |
| 4 | Blur_Radial | 7176.105 |
| 5 | TV_WideScreenHorizontal | 3498.367 |

### 같은 프레임 화질 대조

각 8표본. 원본 반복의 최대 차이는 전 후보 0/255. 아래 PSNR은 RGBA 기준이다. 최대 차이가 큰 후보를 종합 PSNR만으로 승인하지 않았다.

| 필터 | 최대 차이 /255 | 표본 최저 PSNR dB | 종합 PSNR dB | 채택 |
|---|---:|---:|---:|---|
| Blur_Movie | 162 | 32.98 | 36.20 | 안 함 |
| Blur_BlurHole | 20 | 49.69 | 53.78 | 안 함 |
| AAA_SuperComputer | 9 | 56.71 | 57.16 | 안 함 |
| Blur_Radial | 43 | 49.06 | 55.66 | 안 함 |
| TV_WideScreenHorizontal | 198 | 35.86 | 39.90 | 안 함 |

### 별도 성능 ABBA

최종 근거는 abba-final.log만 사용. 상위 5개 각각 워밍업 1판을 버리고 A/B/B/A 4판, 총 20판. 같은 게임 세션, 후보마다 같은 선택 타일에서 20초씩. A=원래 경로, B=해당 필터 반해상도. GPU 쿼리 계측과 ReadPixels는 꺼져 있다. B판마다 실제 곡 중 호출 수 > 0 확인. 전체 곡 성능으로 일반화하지 않는다. Movie는 14번, BlurHole/Radial은 71번, SuperComputer는 1154번, WideScreen은 1번 타일에서 시작.

| 필터 | A 평균 FPS | B 평균 FPS | A GPU ms | B GPU ms | A 30ms 합 | B 30ms 합 | B 호출 수 (2판) |
|---|---:|---:|---:|---:|---:|---:|---|
| Blur_Movie | 292.0 | 292.5 | 2.20 | 1.90 | 0 | 0 | 2880, 2860 |
| Blur_BlurHole | 311.0 | 313.0 | 2.00 | 1.85 | 0 | 0 | 6408, 6475 |
| AAA_SuperComputer | 211.5 | 241.5 | 4.60 | 3.90 | 0 | 0 | 4630, 4616 |
| Blur_Radial | 314.5 | 315.5 | 2.00 | 1.90 | 0 | 0 | 4071, 4087 |
| TV_WideScreenHorizontal | 294.0 | 292.5 | 2.10 | 2.20 | 0 | 0 | 5520, 5544 |

프레임 GPU 평균은 원 로그에서 0.1ms로 반올림되므로 위 평균의 두 자리 표시는 정밀도가 늘었다는 뜻이 아니다. 반해상도의 추가 축소·확대 Blit 비용도 B에 포함된다. FPS 차이만으로 화면 대기·클럭 영향을 분리하지 않는다.

최종 채택은 **0개**. 화질 최대 차이 기준을 모두 넘었다. 새 사용자 설정은 추가하지 않았고 반해상도는 DEV/AUTOTEST 명령에서만 실행된다. 단일 Blit 제거·필터 결합은 금지된 셰이더 변경 없이 같은 결과를 증명할 대체가 없어 적용하지 않았다. 이는 다른 구현의 최적화가 불가능하다는 결론이 아니다.

카운터 없는 quality-abba.log의 첫 Movie ABBA와 정상 종료한 abba-extra.log 준비 판은 성능 근거에서 제외한다. 최종 20판은 별도이며 중간 판을 섞어 평균하지 않았다.

### 동시에 실행된 CameraFilterPack 수 분포

같은 타입이 여러 카메라에 있으면 각각 셈. 각 칸은 프레임 수 (%). 마지막 집계 경계 1프레임의 불확실성은 앞의 한계 절 참고.

| 수 | HELLO | Windflower | Arche |
|---:|---:|---:|---:|
| 0 | 2559 (7.47%) | 7007 (8.43%) | 105360 (100.00%) |
| 1 | 1888 (5.51%) | 7478 (8.99%) | 0 (0.00%) |
| 2 | 3492 (10.20%) | 11676 (14.04%) | 0 (0.00%) |
| 3 | 4473 (13.07%) | 9892 (11.90%) | 0 (0.00%) |
| 4 | 8865 (25.89%) | 20270 (24.38%) | 0 (0.00%) |
| 5 | 4234 (12.37%) | 5732 (6.89%) | 0 (0.00%) |
| 6 | 4541 (13.26%) | 3695 (4.44%) | 0 (0.00%) |
| 7 | 1956 (5.71%) | 4097 (4.93%) | 0 (0.00%) |
| 8 | 668 (1.95%) | 11834 (14.23%) | 0 (0.00%) |
| 9 | 1273 (3.72%) | 927 (1.11%) | 0 (0.00%) |
| 10 | 242 (0.71%) | 167 (0.20%) | 0 (0.00%) |
| 11 | 22 (0.06%) | 180 (0.22%) | 0 (0.00%) |
| 12 | 22 (0.06%) | 92 (0.11%) | 0 (0.00%) |
| 14 | 0 (0.00%) | 29 (0.03%) | 0 (0.00%) |
| 15 | 0 (0.00%) | 56 (0.07%) | 0 (0.00%) |
| 18 | 0 (0.00%) | 12 (0.01%) | 0 (0.00%) |
| 22 | 0 (0.00%) | 4 (0.00%) | 0 (0.00%) |

### 필터별 전체 표

켜진 프레임당 시간은 같은 타입의 여러 인스턴스/호출을 합친 GPU 합계 ÷ 해당 타입이 켜진 프레임 수. 호출당 평균과는 다르다. 회수 표본 기준이며 µs. 원자료는 metrics.json과 각 맵 .log.

| 곡 | 필터 | 등장 시간 % | 켜진 프레임당 GPU µs | GPU 합계 ms | 표본 |
|---|---|---:|---:|---:|---:|
| final-hello | Blur_Radial | 29.05 | 241.88 | 2507.862 | 10545 |
| final-hello | Blur_Movie | 5.87 | 1198.97 | 2321.201 | 1936 |
| final-hello | TV_WideScreenHorizontal | 62.40 | 47.88 | 1016.433 | 21327 |
| final-hello | Atmosphere_Rain_Pro | 15.98 | 176.17 | 828.692 | 4704 |
| final-hello | AAA_SuperComputer | 2.21 | 929.85 | 707.614 | 838 |
| final-hello | Blur_BlurHole | 6.32 | 366.40 | 665.753 | 1817 |
| final-hello | Color_Noise | 26.04 | 48.39 | 455.379 | 9412 |
| final-hello | TV_Chromatical2 | 23.89 | 60.34 | 424.571 | 7036 |
| final-hello | FX_Glitch2 | 11.52 | 100.94 | 389.337 | 3913 |
| final-hello | TV_Old_Movie_2 | 5.80 | 129.92 | 337.783 | 2600 |
| final-hello | Color_BrightContrastSaturation | 18.70 | 46.07 | 262.381 | 5732 |
| final-hello | Color_Contrast | 14.39 | 45.07 | 250.201 | 5551 |
| final-hello | TV_WideScreenVertical | 16.41 | 47.79 | 249.622 | 5263 |
| final-hello | Film_ColorPerfection | 14.65 | 49.66 | 242.080 | 4875 |
| final-hello | Distortion_Noise | 6.06 | 96.81 | 215.792 | 2229 |
| final-hello | TV_Chromatical | 12.23 | 54.82 | 208.801 | 3809 |
| final-hello | FX_Glitch1 | 6.79 | 70.16 | 203.745 | 2909 |
| final-hello | Glow_Glow_Color | 7.58 | 92.35 | 197.082 | 2134 |
| final-hello | VHS_Tracking | 6.79 | 72.72 | 178.518 | 2455 |
| final-hello | Light_Water | 9.85 | 49.56 | 139.805 | 2898 |
| final-hello | Colors_HUE_Rotate | 8.53 | 49.58 | 132.467 | 2672 |
| final-hello | Vision_Aura | 1.13 | 279.86 | 130.695 | 467 |
| final-hello | Blur_Noise | 7.32 | 62.25 | 129.296 | 2077 |
| final-hello | TV_WideScreenHV | 7.09 | 49.22 | 125.363 | 2642 |
| final-hello | TV_Artefact | 4.70 | 83.02 | 124.614 | 1501 |
| final-hello | Colors_Brightness | 8.85 | 44.54 | 114.746 | 2576 |
| final-hello | Glow_Glow | 3.85 | 73.57 | 104.027 | 1447 |
| final-hello | Distortion_Flag | 6.06 | 43.15 | 96.188 | 2229 |
| final-hello | Color_Chromatic_Aberration | 7.32 | 45.57 | 94.878 | 2082 |
| final-hello | TV_Distorted | 6.06 | 53.52 | 94.241 | 1761 |
| final-hello | Film_Grain | 6.06 | 46.53 | 87.435 | 1879 |
| final-hello | Color_Switching | 4.58 | 53.08 | 85.402 | 1609 |
| final-hello | Colors_BleachBypass | 4.04 | 44.84 | 84.119 | 1876 |
| final-hello | Distortion_Dream2 | 3.78 | 57.10 | 81.020 | 1419 |
| final-hello | FX_Glitch3 | 2.08 | 101.66 | 72.585 | 791 |
| final-hello | Light_Rainbow | 4.54 | 45.64 | 67.371 | 1476 |
| final-hello | TV_VHS_Rewind | 3.66 | 55.63 | 64.926 | 1167 |
| final-hello | Vision_AuraDistortion | 0.63 | 279.70 | 55.660 | 199 |
| final-hello | Color_GrayScale | 4.33 | 46.72 | 54.895 | 1175 |
| final-hello | Noise_TV_3 | 2.08 | 76.86 | 49.575 | 932 |
| final-hello | Vision_Drost | 1.51 | 85.67 | 47.977 | 560 |
| final-hello | Vision_Tunnel | 3.67 | 51.44 | 47.788 | 929 |
| final-hello | TV_PlanetMars | 2.49 | 44.15 | 34.877 | 790 |
| final-hello | Drawing_Manga_FlashWhite | 1.99 | 76.55 | 30.467 | 398 |
| final-hello | FX_DarkMatter | 0.26 | 272.84 | 27.557 | 121 |
| final-hello | TV_VHS | 0.91 | 70.81 | 23.580 | 349 |
| final-hello | Vision_Plasma | 1.01 | 56.77 | 21.346 | 376 |
| final-hello | Real_VHS | 0.74 | 58.38 | 15.529 | 278 |
| final-hello | Distortion_BlackHole | 0.80 | 48.00 | 12.721 | 265 |
| final-hello | Glitch_Mozaic | 0.50 | 44.60 | 8.741 | 196 |
| final-hello | Pixel_Pixelisation | 0.54 | 42.62 | 8.566 | 201 |
| final-hello | TV_LED | 0.77 | 46.81 | 7.256 | 155 |
| final-hello | TV_Noise | 0.43 | 45.80 | 7.053 | 154 |
| final-hello | Blur_Bloom | 0.26 | 156.09 | 6.868 | 44 |
| final-hello | Colors_Adjust_FullColors | 0.50 | 46.14 | 6.644 | 144 |
| final-hello | NightVision_4 | 0.33 | 49.36 | 6.120 | 140 |
| final-hello | TV_Vintage | 0.26 | 54.17 | 5.471 | 121 |
| final-hello | Noise_TV_2 | 0.21 | 59.09 | 4.372 | 81 |
| final-hello | TV_Vcr | 0.14 | 80.44 | 4.344 | 54 |
| final-hello | Drawing_Manga_Flash_Color | 0.51 | 44.11 | 3.926 | 89 |
| final-hello | Edge_Neon | 0.11 | 111.81 | 3.578 | 32 |
| final-hello | Distortion_FishEye | 0.12 | 45.01 | 2.745 | 61 |
| final-hello | TV_CompressionFX | 0.12 | 49.91 | 1.747 | 35 |
| final-hello | Noise_TV | 0.07 | 60.46 | 1.572 | 29 |
| final-hello | FX_Psycho | 0.07 | 43.39 | 1.041 | 24 |
| final-windflower | Blur_Movie | 30.14 | 1259.20 | 28371.031 | 22592 |
| final-windflower | Blur_BlurHole | 36.85 | 384.97 | 10988.707 | 29581 |
| final-windflower | AAA_SuperComputer | 12.15 | 890.20 | 8279.789 | 9301 |
| final-windflower | Blur_Radial | 21.58 | 246.48 | 4668.243 | 19873 |
| final-windflower | TV_WideScreenHorizontal | 62.57 | 49.18 | 2481.934 | 52574 |
| final-windflower | TV_Old_Movie_2 | 13.56 | 143.72 | 1873.806 | 13908 |
| final-windflower | Distortion_Dissipation | 2.07 | 894.32 | 1566.853 | 1752 |
| final-windflower | Distortion_Dream2 | 41.23 | 46.10 | 1415.621 | 31215 |
| final-windflower | Blur_GaussianBlur | 2.76 | 354.48 | 813.168 | 2302 |
| final-windflower | Atmosphere_Rain | 7.60 | 123.87 | 806.121 | 6553 |
| final-windflower | Blur_Radial_Fast | 9.58 | 113.41 | 724.124 | 6455 |
| final-windflower | Color_BrightContrastSaturation | 22.78 | 45.40 | 689.168 | 15268 |
| final-windflower | Atmosphere_Rain_Pro | 3.26 | 187.40 | 627.035 | 3682 |
| final-windflower | Film_Grain | 17.76 | 45.59 | 589.344 | 13007 |
| final-windflower | EXTRA_Rotation | 16.17 | 44.94 | 572.833 | 12783 |
| final-windflower | TV_Chromatical2 | 14.68 | 46.33 | 527.327 | 11391 |
| final-windflower | Glow_Glow_Color | 9.72 | 68.03 | 494.687 | 7314 |
| final-windflower | TV_Old_Movie | 4.58 | 121.00 | 481.812 | 4019 |
| final-windflower | Glow_Glow | 6.40 | 77.90 | 457.177 | 5869 |
| final-windflower | Light_Water | 12.15 | 48.25 | 448.805 | 9301 |
| final-windflower | Colors_RgbClamp | 12.15 | 45.53 | 423.465 | 9301 |
| final-windflower | TV_WideScreenVertical | 8.06 | 46.09 | 393.973 | 9003 |
| final-windflower | TV_BrokenGlass2 | 1.84 | 133.31 | 293.822 | 2320 |
| final-windflower | Color_GrayScale | 6.66 | 49.20 | 274.741 | 5694 |
| final-windflower | Blur_Tilt_Shift_V | 2.58 | 103.51 | 242.838 | 2346 |
| final-windflower | TV_Artefact | 2.01 | 87.26 | 194.146 | 2357 |
| final-windflower | Blur_Tilt_Shift | 2.40 | 70.16 | 163.955 | 2337 |
| final-windflower | TV_Chromatical | 3.68 | 49.21 | 144.526 | 3062 |
| final-windflower | Light_Water2 | 1.16 | 138.74 | 143.872 | 1037 |
| final-windflower | Color_Chromatic_Aberration | 5.16 | 50.55 | 130.816 | 2588 |
| final-windflower | TV_BrokenGlass | 2.67 | 75.04 | 119.164 | 1588 |
| final-windflower | Distortion_Water_Drop | 1.89 | 48.40 | 104.979 | 2301 |
| final-windflower | FX_EarthQuake | 1.65 | 48.51 | 73.299 | 1527 |
| final-windflower | FX_DigitalMatrixDistortion | 0.57 | 148.51 | 69.799 | 470 |
| final-windflower | FX_Glitch1 | 1.07 | 75.16 | 62.009 | 883 |
| final-windflower | Distortion_BlackHole | 0.92 | 88.89 | 42.665 | 480 |
| final-windflower | TV_VHS | 0.69 | 69.07 | 39.370 | 586 |
| final-windflower | Color_Contrast | 0.78 | 50.02 | 38.965 | 813 |
| final-windflower | FX_Glitch3 | 0.83 | 84.71 | 36.257 | 428 |
| final-windflower | Blur_Blurry | 0.27 | 129.08 | 32.658 | 253 |
| final-windflower | Blur_Regular | 0.80 | 79.81 | 26.658 | 334 |
| final-windflower | Blur_DitherOffset | 0.92 | 67.86 | 23.208 | 342 |
| final-windflower | Glitch_Mozaic | 0.58 | 51.70 | 21.094 | 408 |
| final-windflower | OldFilm_Cutting2 | 0.34 | 49.10 | 16.447 | 369 |
| final-windflower | Colors_Brightness | 0.31 | 43.70 | 13.680 | 313 |
| final-windflower | Colors_DarkColor | 0.31 | 42.68 | 11.993 | 281 |
| final-windflower | FX_Glitch2 | 0.12 | 97.22 | 9.916 | 106 |
| final-windflower | Pixel_Pixelisation | 0.12 | 46.69 | 4.249 | 91 |
| final-windflower | Colors_HUE_Rotate | 0.06 | 45.03 | 2.072 | 46 |
| final-windflower | Distortion_Aspiration | 0.01 | 47.84 | 0.335 | 7 |


## 최종 검증·복원

- PlayerAuto: 오류 0개. Player: 오류 0개. 기존 경고는 각각 4개/25개(최종 빌드 로그).
- 한·영 캡처의 UI 잘림 경고 0개. 전체 C# 문자열 검사 SFENC001 clean. 변경 C# 전부 UTF-8 BOM.
- 게임 프로세스 종료 확인.
- 설치 DLL SHA-256: CB634D735758EBE0E38A1E45320F0DD7D2919C7F7408E6BA0520814C3AD46858. 시작 시 원본과 동일.
- Settings.xml SHA-256: 5F80693259840F708D8AD891E335AE8A8F74B1A26551D57CF66DF182A5B6174E. 시작 시 보존본과 바이트 일치.
- 설치 폴더의 filtergpu.dll 제거, 대기 autotest.txt 없음.
- FrameGen*, native/sfnative/framegen*, ScreenEffects.cs, effects의 변경 없음. git diff --check 통과. 기존 .codex/config.toml 변경은 사용자 작업 그대로 보존.
- 커밋·푸시·릴리스 없음. 한글 수정과 진단 소스는 작업 폴더에만 남겼으며, 게임에는 요구된 원본 정식 DLL을 복원했다.
- 원시 근거와 최종 상태: filters-evidence/final-state.json, metrics.json, quality-metrics.json, 각 판 로그. report.py는 완주 3개·유효 화질 40표본·정확한 ABBA 20판·B판 대상 이름/호출 수를 검증한 뒤 표를 만든다.