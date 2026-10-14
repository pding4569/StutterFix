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

## 첫 시험 뒤 수정 — 몸통만 보정 (Codex, 2026-10-14)

**이 절이 위의 전체 공 매트 구현 설명을 대체한다. 게임 시각 검증은 Claude에게 남긴다.** run1 PNG는 확인했지만 당시 black/white/background/screen RT 원본은 없으므로 아래는 코드와 합성 모형으로 가린 원인이다. 특정 파티클의 실제 난수 변화나 당시 UV 방향을 실측했다고 해석하면 안 된다.

### 가설별 근거

1. **픽셀별 진짜 화면 복귀가 고리·점무늬를 만드는 경로는 재현됨.** 기존 공 층 분기는 옮긴 매트를 합성하기 직전, 원래 화면 좌표의 재구성 결과와 oldScreen/newScreen 차이가 `2.5/255`보다 크면 그 픽셀만 nearest true frame으로 돌렸다. 재구성에는 ARGBHalf HDR 값이 있지만 screen은 LDR이라, 예컨대 재구성 1.4 / screen 1.0은 같은 포화된 빛이어도 실패한다. 배경·파티클·가장자리의 작은 차이도 같은 판정을 뒤집는다. 움직인 몸통 중심에서만 실패시키면 속 빈 고리, 격자 형태로 실패시키면 점무늬가 된다. 검사기의 합성 원판 317픽셀 중 중심 구멍 145픽셀, 점무늬 복귀 156픽셀로 각각 재현했다. 실제 run1에서 어느 차이가 얼마였는지는 RT 원본 없이는 확정 불가.
2. **검정/흰색 사이 파티클이 달라지면 매트 식은 성립하지 않음.** 고정된 가산 파티클이면 `white-black=1`이다. 검정 렌더 S=.4 / 흰색 렌더 S=.43이면 1.03이 되어 saturate에 잘리고, 반대 방향 차이는 가짜 가려짐이 된다. 반면 같은 상태의 두 공이 겹치는 것 자체는 `S=S1*T2+S2, T=T1*T2`로 정확히 합성된다. 따라서 "겹치면 투과율 계산부터 틀린다"는 가설은 고정 상태의 affine blend에서는 기각한다. 서로 다르게 움직이는 두 공을 합친 한 장에 한 변위로 적용하는 문제는 실제 구조적 한계다.
3. **배경과 매트의 좌표가 다른 것 자체는 오류가 아님.** 배경은 카메라 이동, 공은 카메라+공 이동을 따라야 한다. 불투명 몸통은 T=0이라 어느 배경 좌표를 쓰든 그 색에 영향을 받지 않는다. 평행 이동/줌/회전 좌표 왕복 검사를 통과했다. 다만 실제 quad UV/상하 반전은 모형 검사로 확정할 수 없으므로 게임 확인 항목으로 남긴다.

픽셀 복귀만 지워도 움직이는 파티클 매트와 두 공의 서로 다른 변위 문제는 남는다. 전체 공 매트를 그대로 유지하는 수정보다 요청한 대안을 적용했다.

### 바뀐 동작

- world는 **원래 camRT를 그대로 snapshot**한다. 공 없는 배경 RT, 배경 카메라 체인 3회 재렌더, quad 텍스처 교체를 제거했다. 실제 Present 화면을 수정하지 않는다.
- `PlanetRenderer.sprite.meshRenderer`, samuraiSprite, faceSprite, faceDetails만 별도 매트로 수집한다. `appearanceRenderers`에는 core/tail/sparks/glow/ring도 들어 있으므로 쓰지 않는다(게임 DLL IL로 확인). 파티클·빛·고리·꼬리는 매트 카메라의 별도 layer에 옮기지 않는다. 숨김/비활성/카메라 culling은 계속 따른다.
- 네이티브는 기존 `colorAt`를 `baseColorAt`로 보존해 빛·꼬리를 포함한 기존 합성을 먼저 계산한다. 그 위에 **양 끝 프레임 모두 거의 불투명한 몸통만** 덮는다. 채널별 투과율의 최댓값이 .002 이하이면 전부 덮고, .002~.02는 부드럽게 줄이며 .02 이상은 기존 결과 그대로다. 가산 빛(T=1), 반투명 몸통, 한 끝에만 있는 몸통은 추가 보정을 하지 않는다. 몸통 보정에서 screen 차이로 픽셀을 떼어내는 판정은 제거했다. 기존 경로의 UI 판정은 그대로다.
- 선택한 공의 샘플이 다른 공 몸통 반경의 1.1배 안에 있으면 별도 매트 보정을 포기하고 기존 결과를 유지한다. 합친 매트에서 다른 공까지 잘못 옮기는 것을 피하는 보수적인 처리다. 완전한 교차/겹침 해결은 아니며, 큰 커스텀 모양과 UI가 몸통 위를 가리는 경우도 게임 확인이 필요하다. 특별한 스킨의 새 렌더러가 현재 수집 목록에 없으면 그 부분은 기존 합성으로 남는다.
- 기존 설정 XML과 기본 false 유지. fgstate에 `body_only=True` 추가. 패킷 크기는 168바이트 그대로지만 world 의미가 달라져 **block version 7**로 관리/네이티브 양쪽을 맞췄다. 구형 버전 6과 섞어 쓰면 시작을 거부한다. 두 DLL을 함께 교체하고 게임을 새로 시작해야 한다.
- RT 6장은 여전히 ARGBHalf다. 배경 RT는 제거했지만 고해상도 매트 메모리 비용은 남는다. 새 이벤트/패치/PlayerLoop 등록 없음. 렌더러 enabled/layer, RenderTexture.active 및 카메라 기록은 finally에서 복원하고 FreeTextures→BallLayer.Free에서 리소스를 해제한다. 매 프레임 새 목록/배열/리플렉션/진단 타이머는 추가하지 않았다.

### 오프라인 확인과 다음 게임 시험

- `dotnet build -v q --nologo -p:Edition=Player -p:AutoTestBuild=1`: 오류 0, 기존 경고 25개.
- `native/build-sfnative.bat`(MSVC 14.44.35207, SDK 10.0.26100.0, ISPC 1.31.0): 성공. 출력 `native/sfnative.dll` 재생성.
- `py -3 tools/framegenlab/check-ball-layer.py`: HLSL VS/PS/Reduce/Match/Metric 컴파일, 기존 합성식, HDR 복귀 실패, 고리/점무늬 모형, 새 불투명 몸통 보정, 가산/반투명/단일 끝/채널별 마스크, 좌표 왕복, 겹침 처리 및 기본값/복원/버전 코드 계약 검사. 수학 모형과 셰이더 컴파일 검사이며 게임 RT를 실제 GPU로 돌린 픽셀 대조는 아니다.
- 게임 실행·설치·커밋·푸시 없음. Settings/SettingsWindow/AutoTest/csproj 변경 없음.

Claude는 먼저 run1과 같은 조건에서 `body_only=True`, captures 증가를 확인하고 8장 연속 캡처를 받는다. 고리·점무늬 제거 여부, 몸통 밝기(몸통 위 가산 빛의 그리기 순서에 따른 차이 포함), 교차 시 기존 경로로 넘기는 부분, 꼬리/코옵/이모지/사무라이/골드/Overseer/숨김, 몸통 위 UI, 카메라 회전·줌·화면 밖 출입을 확인한다. true/false 정지 화면, 필터/FSR 우회, 해상도 변경·맵 전환·unload 복원도 확인한다. GPU/VRAM 성능 수치는 이번 작업에서 측정하지 않았다.

## 두 번째 게임 시험 (Claude, 2026-10-14, 블록 버전 7, 몸통만 공 층 `body_only=True`)

같은 조건(h90000, 60 FPS 제한, 4배, 3440x1440), `captures=553`. 캡처: `docs/handoff/ball-layer-run2.png`.
- 첫 시험의 속 빈 흰 고리와 점무늬 얼룩은 **사라졌다**. 8장 모두 공이 진짜와 같은 크기의 흰 원이다.
- 6번째 장 왼쪽 공 둘레에 어두운 호가 한 장 남는다(두 공이 겹치는 장면).
- 이전 방식(공 층 끔)과 비교하면 같은 수준이거나 약간 깨끗하다. 뚜렷한 승리는 아니다. 비용(GPU, 메모리 227MiB)은 측정 안 함.
- 판단: 기본 끔 유지. 정식에 넣을 만큼의 이득은 아직 확인 못 함.
