# 코덱스 작업: 직통 렌더러 M1b — 장식 화면 일치와 맡는 범위 넓히기 (2026-11-15)

작업 폴더 `E:\StutterFixWork\wt-v14prod` (브랜치 claude/adofai-renderer). 커밋·게임 실행·설치 DLL 교체 금지(Claude 가 한다). 한국어 보고.
먼저 읽기: `docs/direct-renderer/00-design.md`. 결과: `docs/direct-renderer/03-m1b-result.md`.

## 지금 구조 (Claude 가 만든 것, 미커밋)
- `native/sfnative/dr2.cpp`, `dr2.hlsl`, `dr2_shader_bytes.h`: 네이티브 인스턴스 그리기. 장식마다 채널 16개(pivotPos, pivotOffset, rotAngle, scaleVec, color, opacity, parallax multiplier, parallaxOffset) + 정적 32개(스프라이트 지역→피벗 지역 대응, trueScale, crop, startRot, z, textureScale, repeat, flags, UV, tint, 마스크 기준, 텍스처 크기). 정점 셰이더가 scrParallax.SetTrans / scrDecoration.UpdatePosition·SetRotation·SetScale 식을 따라 화면 위치를 낸다. 구간(seg)마다 카메라 CommandBuffer 플러그인 이벤트(BeforeForwardAlpha = 배경 층 Bg, AfterForwardAlpha = 앞 층 Default). 스텐실 마스크(들어감/나옴/안/밖)·더하기 블렌드 변형 있음. 빌드: Claude 스크래치패드의 build_dr2_native.sh 방식(fxc 로 헤더 생성 → native/build-sfnative.bat).
- `DirectDeco.cs`: 분류(Classify) → 남는 렌더러/캔버스로 순서 경계 → 맡을 장식 → 구간 → 매 프레임 바뀐 채널만 올림(Poll). TightLate.Process·LogicUpdate·InvisibleSkip.Work 에서 맡은 장식은 건너뜀. AUTOTEST `dr2 on|off|now|state|seq|tight|masks|force|only N|seqtest`.
- 측정(3440 렌더, 순서를 무시하고 모두 맡긴 `force`): HELLO 205→229 FPS(빈 칸 뺀 도형 켬), 메인 스레드 직통 비용 약 10µs/프레임, 렌더 이벤트 약 60µs.

## 실게임 문제 (정상 모드 `drPix`: 일시정지 → 켬 화면 d1 → 끔 화면 d0 → framecmp diff)
| 맵 | 맡은 장식 | 다른 픽셀 | 주된 거부 이유 |
|---|---|---|---|
| HELLO 50초 | 34 / 2531 | 0.02% (최대 7) | 순서 경계(배경 1445, 앞 406), 마스크 361, Grab 블렌드 180 |
| Windflower 258초 | 5 / 6290 | **81%** | 순서 경계 4531, 마스크 992 |
| DD 50초 | 51 / 6314 | 0.01% | 순서 경계 4402, Grab 1753 |
| Exit 88초 | 6 / 3524 | **13.5%** (큰 이미지 하나 영역이 통째로 다름) | 순서 경계 3300 |
- 로그의 "끼울 수 없는 것: 캔버스 14923/2226/5500/2805" → `FindObjectsOfTypeAll<Canvas>` 에서 **꺼진(비활성) 캔버스까지 경계로 셈**(isActiveAndEnabled 확인 없음). 이게 순서 경계 대부분의 원인으로 보인다.
- Grab 블렌드(Hidden/BlendModes/SpritesDefault/Grab, 더하기 이외 모드)는 끼울 수 없어서 경계가 된다.
- 몇 개만 맡았는데 화면이 크게 다름(WF 81%, Exit 13.5%) → **정적 값/식이 틀린 장식이 있다**(위치·크기·UV·크롭·반복·텍스처 방향 등).

## 할 일
1. **자체 검증 명령 `dr2 verify`**(AUTOTEST): 맡은 장식마다 같은 프레임에서 (a) Unity 가 원래 경로로 계산했을 네 모서리 월드 좌표(원래 렌더러를 잠깐 forceRenderingOff=false 로 하지 않고, 장식의 UpdatePosition 을 한 번 불러 Transform 을 갱신한 뒤 spriteRenderer.localToWorldMatrix × sprite.vertices 로) vs (b) 셰이더와 같은 식을 C# 으로 계산한 모서리, 그리고 색·UV·텍스처를 비교해 최대 오차와 상위 10개 장식(이름·배치·시차·크롭·반복·flip·텍스처 크기·원래 크기)을 로그. 셰이더 식을 바꾸면 C# 복제도 같이 바꾼다. 원인을 찾아 고쳐라(Exit/WF 의 큰 이미지 차이).
2. **경계 줄이기**:
   - 캔버스는 활성(isActiveAndEnabled)인 것만 경계. 꺼진 캔버스가 곡 중 켜질 수 있으면(글자 장식 SetVisible 등) 그 장식의 SetVisible 후크로 경계를 다시 계산하는 방식으로.
   - 남는 렌더러 중 앞으로도 그려지지 않는 것(GameObject 비활성 + 그 장식이 맡을 수 없는 이유가 영구)과 이미 끈 것은 경계에서 빼고, 켜질 수 있는 것은 켜질 때(후크) 다시 계산.
   - **Grab 블렌드를 네이티브로**: 구간 안에서 해당 장식의 화면 범위(사각형)만 camRT 에서 복사(CopySubresourceRegion)해 Blend Modes 에셋과 같은 식으로 섞는다(`tools/gpudeco-lab/GpuDecoBatchBlend.shader` 의 모드 식, `GpuDecoBlend.cs` 모드 번호 참고, 지원 모드부터). 그러면 Grab 장식도 맡고 경계가 아니다.
   - 마스크: 지금은 "마스크·대상이 전부 맡을 수 있을 때만" 맡는다. 남는 마스크 대상이 생기는 원인(파티클·다른 셰이더 등)을 줄여라.
3. 매 프레임 비용 유지(메인 스레드 직통 비용 < 50µs/프레임, 할당 0). 4종 빌드 오류 0, native export 보존(새 export 는 뒤에).
4. Claude 가 잴 bench 설정: 기존 `drPix`(정상 모드 픽셀 대조), `drOn`(성능), `ppGpuCam` 대조. 결과 문서에 기대 지표(맡은 수, 다른 픽셀 비율 목표 < 0.1%·최대 채널 차 ≤ 3)를 적어라.

## 막히면
GpuDeco(`GpuDecoGpu.cs`, `tools/gpudeco-lab/GpuDeco.compute`)는 같은 식을 비트 단위로 맞춘 이력이 있다(RotationSinCos·CpuQuotient·ImageRotation·crop·flags 128/256/64). 그 식을 그대로 가져와도 된다.
