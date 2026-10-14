# 공 따로 그리기 층 조사 (2026-10-14)

게임 실행 없이 FrameGen.cs 및 게임 DLL의 scrCamera/scrPlanet/PlanetRenderer IL을 조사했다.

- PlanetRenderer.SetLayer는 GameObject.layer가 아니라 sortingLayerID를 바꾼다. 기존 행성 전용 culling layer를 가정할 수 없다. 실제 프리팹의 숫자 layer 및 shader blend는 IL만으로 확정되지 않는다.
- PlanetRenderer는 PlanetSprite(meshRenderer), glow, ring, tailParticles/tailParticlesCoop, coreParticles, sparks, samuraiSprite, faceSprite/faceDetails를 관리한다. SpriteRenderer만 수집하면 커스텀 공을 놓친다. 행성 하위 Renderer 전체를 수집한다.
- FrameGen의 worldCamera는 Overlaycam을 복제한다. 이는 camRT를 표시하는 quad를 찍으므로 world에 공과 게임 필터가 이미 포함된다. Overlaycam 렌더 직전에 행성을 꺼도 배경에서 공은 없어지지 않는다.
- 공 없는 화면은 별도 scene RT에 게임 카메라 체인(Bgcamstatic, BGcam, camobj)을 다시 렌더하고 quad의 텍스처를 일시적으로 그 RT로 바꾸어 캡처해야 한다. targetTexture/quad/renderer 상태는 finally에서 복원한다. 원래 camRT는 쓰지 않는다.
- matte 카메라는 CopyFrom으로 투영을 복제하지만 필터 컴포넌트는 복제하지 않는다. 블룸/왜곡 등 비선형 후처리를 거친 배경과 raw 공 층의 합은 원래 최종 화면과 일반적으로 같지 않다. 따라서 후처리가 있는 장면의 픽셀 일치는 미확인이고 정식 승인은 불가하다. 이 실험의 배경 재렌더 비용도 설계의 2회 공 렌더 비용 외에 추가된다.
- 두 공을 합친 matte는 겹침 순서를 보존하지만 서로 다른 이동을 완전히 분리할 수 없다. 이동 샘플은 가까운 행성의 변위를 사용한다. 긴 꼬리/교차/겹침은 Claude의 영상 검증이 필요하다.

## 구현 경계

작업 4의 Settings 정의(StutterFix.cs), SettingsWindow, AutoTest, csproj를 수정하지 않는다. 설정은 별도 `FrameGen-ball-layer.xml`의 `FrameGenBallLayer` 값으로 읽으며 기본 false, 프레임 늘리기를 새로 켤 때 읽는다. 향후 Claude가 기존 Settings에 통합할 수 있다.
ABI는 두 pointer 추가로 168바이트, block version 6. 네이티브 DLL 교체 후 게임 재시작이 필요하다. 게임/설치/커밋/푸시는 수행하지 않는다.

## 구현 결과와 Claude 시험 절차

변경: FrameGen.cs, native/sfnative/framegen.h, framegen.cpp, framegen_block.h 및 빌드된 native/sfnative.dll. 오프라인 검사기는 tools/framegenlab/check-ball-layer.py. 설정 예시는 FrameGen-ball-layer.example.xml.

- `FrameGenBallLayer`: 기본 false. Mods/StutterFix/FrameGen-ball-layer.xml에 예시 XML을 복사하고 true로 바꾼 뒤 프레임 늘리기를 껐다 켠다. 기존 Settings.xml/UI/AutoTest에는 통합하지 않았다(작업 4 파일 보호).
- ARGB32의 흰 matte는 가산 빛을 1에서 잘라 투과율을 잘못 계산한다. 따라서 공 6장에는 ARGBHalf를 쓴다. 3440×1440의 색 버퍼만 약 227MiB이고, 깊이 버퍼와 네이티브 복사 6장은 추가 비용이다. 이 메모리 증가와 배경 카메라 재렌더 때문에 GPU 0.2ms 목표 달성은 미확인이다.
- 후처리 컴포넌트가 켜진 카메라 또는 quad의 mainTexture가 camRT가 아닌 경우(FSR/화면 효과/사용자 지정 FPS)에는 기존 경로를 사용한다. 필터가 곡 중 추가되는 경우도 매 프레임 재사용 List로 검사하고, 타입별 reflection 결과를 기억한다. 필터를 끄거나 설정을 바꾸지는 않는다.
- 공 층 유무가 바뀐 프레임 쌍은 네이티브 보간을 무효화해 서로 다른 배경을 섞지 않는다. 알파는 RGB 채널별 투과율로 계산한다. 공 위치는 기존 orbit 계산을 쓰며, 겹친 두 공의 합쳐진 층은 가까운 공의 변위로 이동한다. 두 공 각각의 독립적인 겹침/꼬리 변형까지 정확한 것은 아니다.
- FrameGen 종료 경로의 FreeTextures에서 카메라/RT/참조를 해제한다. targetTexture, quad 텍스처, renderer enabled/layer, RenderTexture.active, FrameGen 카메라 기록은 finally에서 복원한다. 추가 Harmony/이벤트/PlayerLoop 등록은 없다.

Claude 시험:
1. 새 관리 DLL과 native/sfnative.dll을 설치하고 게임을 새로 실행한다(ABI 168바이트/block version 6, reload만으로 교체 불가).
2. XML false로 기존 경로를 확인한다. true로 바꾸고 프레임 늘리기를 껐다 켠 뒤 먼저 필터/FSR 없는 정지 장면을 확인한다. `fgstate`의 ball_layer/captures/filter_skips로 실제 공 층 사용 여부를 확인한다. captures가 0인 시험을 on 성능 표본으로 세지 않는다.
3. 3440×1440, 60 FPS 제한, 4배에서 h90000/HELLO 2026의 연속 8장을 dupcap으로 받는다. 몸통 밝기/크기, 빛 중복/어두운 고리, 회전·줌·교차·꼬리·코옵·이모지·숨긴 공을 확인한다. 정지 off/on 픽셀 비교와 진짜 Present 화면 보존은 아직 게임에서 검증하지 않았다.
4. 필터가 켜지는 구간에서 filter_skips 증가 및 기존 경로 복귀를 확인한다. 이 구현은 후처리 활성 장면의 공 일그러짐 해결을 제공하지 않는다. 비선형 후처리까지 일반화하려면 별도 설계가 필요하다.
5. 재시작 A/B 4판 이상으로 PresentMon/GPU timestamp 및 최악 프레임·VRAM을 확인한다. 게임 내 측정 표본은 이번 작업에서 0판이다.
6. 맵 전환·해상도 변경·UMM unload·설정 false 복귀 시 원래 카메라/공 상태와 리소스 해제를 확인한다.

오프라인 검증: PlayerAuto 빌드 오류 0(경고 25개), native/build-sfnative.bat 성공, HLSL VS/PS/Reduce/Match/Metric 컴파일, 알파/가산/불투명/채널별 곱 매트 합성식 검사 통과. 게임 실행, DLL 설치, 커밋, 푸시 없음.


## 첫 게임 시험 결과 (Claude, 2026-10-14) — 개선 아님, 수정 필요

조건: h90000, 60 FPS 제한, 고정 4배, 3440x1440, `FrameGenBallLayer=true`, 일반 창. `ball_layer=True captures=555`(사용됨), `filter_skips=0`.
캡처: `docs/handoff/ball-layer-run1.png` (dupcap 전체 해상도 연속 8장, 공 부근 520x340).

관찰 (이전 방식 대비):
- 4·5·7번째 장(생성 프레임)에서 공 둘레에 **속이 빈 흰 고리**가 생긴다(공 몸통이 가운데 비어 보이거나, 윤곽만 하얗다).
- 공·꼬리 부근에 **점무늬 얼룩**(노이즈 같은 반투명 점)이 생긴다. 이전 방식에는 없었다.
- 정지에 가까운 장(1·2·3번째)은 공이 진짜와 같은 크기·밝기로 잘 나온다.
- 비용: 이 장면 진짜 GPU 완료 1.6ms(PresentMon 대조 안 함).

의심: (1) 공 없는 배경을 다시 렌더한 것과 진짜 `world` 의 배경이 파티클(꼬리·타일 빛) 난수/시간 때문에 픽셀이 다르다 → 점무늬.
(2) 두 공을 합친 매트가 겹치는 장에서 투과율 계산이 어긋나 속 빈 고리가 된다. (3) 사이 위치로 옮길 때 매트와 배경의 샘플 좌표가 달라 가장자리만 남는다.
부탁: 원인을 가려(오프라인 검사기로 재현 가능하면 좋다) 고친다. 해결 안 되면 공 층을 "공 몸통 불투명 부분만"으로 줄이는 안(빛·꼬리는 기존 가산 방식 유지)을 시도한다.
