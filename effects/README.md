# 화면 효과

직접 작성한 MIT 셰이더다. ReShade/Shadertoy 소스를 포함하지 않는다. 라이선스는 `license.txt`다.

Unity 6000.3.10f1의 빈 프로젝트에 `ScreenEffects.shader`를
`Assets/Effects/ScreenEffects.shader`, `BuildEffects.cs.txt`를
`Assets/Editor/BuildEffects.cs`로 복사한다. `BuildEffects.Build`를 실행하면
`EffectsOutput/stutterfix_effects`가 생긴다. 이 파일을 이 폴더에 복사한 뒤
모드를 다시 빌드한다. Windows D3D11용 AssetBundle이다.

LUT는 N²×N PNG(N=2~64)다. 가로로 파랑 슬라이스, 각 슬라이스의
가로가 빨강이며 PNG 맨 위 행이 초록 0이다. `identity16.png`는
직접 만든 무변환 표다. 모드의 `luts` 폴더에 넣고 설정에서 파일을 적용한다.
색감·비네트·LUT는 한 패스에 합친다.

맵 필터/FSR 뒤의 게임 그림을 별도 RT에 처리하고 UI 앞에 붙인다.
반만 그리기로 원본이 갱신되지 않은 프레임에는 지난 결과를 재사용한다.
프레임 늘리기의 캡처는 그 결과를 사용한다. 모든 새 효과는 기본 꺼짐이다.

글로우는 1/4 그림에서 하이라이트를 퍼뜨린다. 맵의 활성 Bloom 구성 요소가
있으면 쉰다. 실험 주변 비추기는 밝은 색을 어두운 주변에 넓게 더하는
화면 공간 효과이며, 깊이·차폐·화면 밖 광원은 계산하지 않는다.

개발자/자동 시험 명령: `shaders`, `fxstate`, `fxfixture`, `fxcapture`,
`fxbench passthrough|color|combined|sharp|fxaa|glow|light|off`, `fxbenchreport`.
GPU fixture는 효과/프레임 늘리기를 끈 별도 판에서 실제 3440×1440 그림을
고정해 개인 출력 RT에만 실행한다. 30프레임 준비 뒤 비동기 8칸 timestamp
query로 재며 GPU 대기/그림 읽기를 하지 않는다. 실제 게임 FPS나 물리적
표시 지연을 대신하는 수치는 아니다. `fxcapture`/`fxfixture`의 픽셀 읽기는
성능 판에서 실행하지 않는다.
