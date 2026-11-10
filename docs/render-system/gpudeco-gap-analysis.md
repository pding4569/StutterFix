# GpuDeco와 게임 장식 전수 비교: 거부 없는 통합 설계

작성: 2026-11-10. 대상 작업 트리: `E:\StutterFixWork\wt-v14prod`, 브랜치 `claude/adofai-renderer`.

## 1. 판정과 조사 범위

**현재 GpuDeco는 모든 장식을 지원하지 않는다. 상태 계산과 렌더 소유권을 분리하고, 전체 장식을 등록하는 통합 순서 계획을 만들어야 한다.** T(시간표/관측된 이벤트 소스), L(게임이 계산한 최종 상태), N(원래 렌더러를 실행하는 순서 노드)을 하나의 계획에 넣는 방향은 타당하다. 다만 L을 붙이는 것만으로 마스크·반복·Canvas·파티클·그리기 동점이 해결되지는 않는다. 또한 모든 장식의 호환성 지원과 모든 장식의 GPU 배치 처리는 서로 다른 합격 항목이다. N은 명시된 기능 경로로 등록·정렬·수명 관리해야 하며, 알 수 없는 장식을 조용히 목록 밖에 남기는 거부 목록이어서는 안 된다.

**최적화의 확실성은 아직 입증할 수 없다.** L은 원래 스크립트를 유지하므로 Update/LateUpdate 절감이 없을 수 있고, Grab은 원래처럼 화면 복사가 필요하며, LOD는 손실이 있다. 이 문서는 설계·코드 분석이고 새 성능 측정 결과가 아니다. 인계 지시대로 빌드·게임 실행·제품 코드 변경·커밋을 하지 않았다. 시작 시 이미 수정돼 있던 `RunLod.cs`와 기존 진단 산출물은 유지했다. 결과 문서가 없어서 기존 코드·명세·로그에 이어 이 문서를 작성했다.

근거 표기: 저장소 파일은 루트 기준 `파일:줄`, 게임 코드는 **G/** = `E:\StutterFixWork\decompiled\Assembly-CSharp\`, 로그는 **R/** = `E:\StutterFixWork\night-results\`이다. 아래 범위 표기는 시작 줄을 가리킨다. 분석 시점의 줄 번호이며 후속 코드 변경 시 이동할 수 있다.

증거 수준:

- **코드 확인**: 읽은 C#·셰이더·기존 분석의 동작. 비용을 실행시간으로 환산하지 않는다.
- **기존 실측**: 11-10 인계와 기존 로그/문서에 기록된 값. 실행 조건이 다른 수치를 합치지 않는다.
- **제안/추정**: 구현 방안·복잡도·바이트 산술. FPS·ms 예상값이 아니다.
- 디컴파일 `scrDecoration.cs` 수정 시각은 11-04 18:42, 설치 DLL은 11-04 03:00이었다. 추가로 **설치 DLL을 `ilspycmd -t scrController`로 읽어 `disableV15Features = true` 초기값을 확인**했다. 전체 디컴파일과 DLL의 바이트 동등성까지 검증한 것은 아니다. 명세의 “앞으로도 항상 죽은 코드” 주장은 확장 설계의 전제가 될 수 없다. 현재 기본 true 분기와 false 분기를 모두 별도 계약으로 다룬다.

### 1.1 기존 실측을 다시 읽은 기준선

`gBsSp` 로그는 `live_prepare_candidates`의 admitted를 최종 소유로 읽으면 안 된다. 그 수는 후보 판정 결과이고 뒤의 사전 내리기가 별도다(`GpuDecoExport.cs:1058`, `GpuDecoExport.cs:1078`). 사유는 중복되며 “단독” 수만 해당 원인 하나 제거의 제한된 후보 상한으로 쓸 수 있다. 창 가시성·후속 정렬·텍스처 제한이 있으므로 단독 수도 최종 소유 증가를 보장하지 않는다.

| 맵 | 전체 / 표 지원 | 우선 원인(사유 수/단독) | 준비 판정과 최종 결과 | 직접 근거 |
|---|---:|---|---|---|
| Arche | 28,835 / 28,806 | 깊이 22/22, 비이미지 7/7 | repeat-tile 110, 직접 슬롯 95>48로 33개 내림, 소유 약 28,600 | R/n1110-ar-gBsSp.log:7069, :7076, :7077; 인계의 동점 44 |
| DD | 6,314 / 2,724 | 깊이 3,429/3,420, 초기 마스크 128/59, 대상 65/0, 비이미지 30/0, Tile 재배치 8/8 | repeat 188, runlod-split 1,268, admitted/소유 1,182 | R/n1110-dd-gBsSp.log:9155, :9163 |
| HELLO | 2,531 / 2,036 | 초기 마스크 384/153, 대상 231/0, 비이미지 105/14, 초기 부가 없음 89/0, 깊이 4/3 | repeat 241, split 1,635, admitted 156 → 소유 14 → 최소 64로 꺼짐 | R/n1110-hl-gBsSp.log:9169, :9177, :9178 |
| Windflower | 6,290 / 4,363 | 초기 마스크 997/337, 비이미지 687/6, 초기 부가 없음 681/0, 대상 658/0, 깊이 172/169, 마스크 종류 84/71 | repeat 102, split 4,010, admitted 45 → 소유 13 → 꺼짐 | R/n1110-wf-gBsSp.log:8957, :8966, :8967 |
| Exit(로그 sh) | 3,524 / 3,341 | 초기 마스크 103/24, 대상 78/0, 비이미지 41/0, 종류 35/25, 예약 밖 8/8, 깊이 4/4 | repeat 100, split 3,088, 직접 74>48로 1개 내림, admitted 71 → 소유 2 → 꺼짐 | R/n1110-sh-gBsSp.log:9329, :9337, :9338, :9339 |
| 7777(로그 s7) | 498 / 316 | 종류 107/89, 초기 마스크 26/7, 예약 밖 24/3, 비이미지 24/4, 깊이 18/10, 충돌 13/0, 조건 연결 8/0 | repeat 117, split 155, admitted 43 → 소유 13 → 꺼짐 | R/n1110-s7-gBsSp.log:9307, :9315, :9316 |

“깊이를 고치면 DD 소유가 3,420개 늘어난다”는 확정 예측은 하지 않는다. 정확한 대상 ID 교집합을 재집계해야 한다. “초기 부가 상태 없음”은 비이미지와 겹친다. G1 초기 스냅샷은 이미지 전용이므로 이 숫자를 별도의 이미지 미지원 수로 더하면 안 된다(`DecoBake.G1.cs:17`, :37).

인계의 GPU 수치(7777 메인 2.9ms 중 비소유 장식 1.95ms, Arche 5.08ms 중 비소유 45개 2.79ms)는 **제공된 기존 실측**으로만 사용한다. 이 턴에서 GPU 캡처를 재생하지 않았다. 이 값 전체를 배칭만으로 절감할 수 있다는 뜻도 아니다. 같은 픽셀을 그대로 칠하면 덧칠 비용은 남는다.

## 2. 게임이 실제로 실행하는 전체 기능

### 2.1 종류: 사용자에게 보이는 네 종류와 Prefab 예외

| 종류 | 게임의 표현·동작 | 현재 GpuDeco | 통합 설계와 주의점 |
|---|---|---|---|
| Image | SpriteRenderer, SpriteMask, 대체 MeshRenderer, BlendModeEffect, 콜라이더. G/scrVisualDecoration.cs:10, :272, :327, :397, :475 | 제한된 Simple 사각형만. 종류를 정확히 비교해 파생형도 제외(GpuDecoScene.cs:177) | T/L 사각형·일반 메시 어댑터 + 반복·마스크·혼합. 물리와 렌더 소유권 분리 |
| Text | `UnityEngine.UI.Text`와 Canvas. SetText 뒤 코루틴이 다음 프레임 TextGenerator/콜라이더를 갱신, 언어별 FontData 적용. G/scrTextDecoration.cs:50, :58, :64, :79, :105 | 표 단계 비이미지. Renderer 전수 탐색만으로 CanvasRenderer 순서를 전부 모델링할 수 없음 | 우선 Canvas N 노드, 캔버스 전체의 정렬·클리핑 계약. 정적 텍스트 래스터 캐시는 폰트·언어·스타일·해상도·변환이 고정인 경우에만 별도 합격 후 |
| Particle | ParticleSystem과 Renderer. shape.scale 및 pitch×simulationSpeed 매 Update, Play/Stop/Emit/Scrub, seed·lifetime·simulationSpace. G/scrParticleDecoration.cs:47, :74, :98, :116, :141 | 비이미지 | 시뮬레이션은 원래 실행, 렌더 N. 파티클 한 시스템 내부 정렬·trails·랜덤 상태를 한 이미지 쿼드로 대신할 수 없음 |
| Object | Planet/Floor/Bubble 활성 객체 전환, 내부 여러 Sprite/Mesh/Particle, 꼬리·불꽃·아이콘·글로우·컬러 애니메이션. G/scrObjectDecoration.cs:71, :139, :172, :211, :217, :264, :296 | 비이미지 | 복합 N 노드(실제 여러 정렬키 유지). 하위 렌더러를 하나로 합쳐 다른 장식 사이를 건너뛰지 말 것 |
| Prefab | AddDecoration의 `prefab:`가 공식 레벨/Unity Editor에서 PublicPrefabs를 Instantiate. G/scrDecorationManager.cs:235, :278 | 비이미지/파생형·외부 스크립트 제외 | 반드시 등록하는 N 복합 노드. G/scrPrefabDecoration.cs:15 이하의 SetDepth/ApplyColor/SetVisible/SetTile은 비어 있음. 이미지와 동일한 setter 의미를 강요하면 원래 동작을 바꿈 |

생성은 `G/scnGame.cs:1419` → `G/scrDecorationManager.cs:218` → Instantiate/Setup/UpdateHitbox/목록·태그 등록이다. 숨은 장식도 등록해야 한다. 삭제·맵 변경은 `G/scrDecorationManager.cs:313`, :648, :660 및 `G/scrDecoration.cs:512`의 Tween Kill/필터 정리까지 소유 수명에 포함한다.

### 2.2 공통 필드·컴포넌트 대응

아래는 동작별 목록이다. 정확한 개별 선언·Setup 입력 키·이벤트 키는 부록 B에 모두 추출했다. 편집기용 필드도 버리지 않고 별도 분류한다.

| 필드/컴포넌트 묶음 | 실제 읽기/쓰기 근거 | 현재 대응 | 필요한 경로 |
|---|---|---|---|
| pivotPosVec(X/Y), pivotOffsetVec(X/Y), rotAngle, scaleVec(X/Y), color(RGBA), opacity, parallax.multiplier(X/Y), parallaxOffset(X/Y) | G/scrDecoration.cs:229, :271, :283, :298, :305, :316; G/ffxMoveDecorationsPlus.cs:137 | 16채널(T). live는 소스의 CPU ease 결과를 GPU 보간에 사용 | T 또는 L. 표로 예측했다는 이유만으로 이미 실행된 setter 순서/현재값을 대체하지 않음 |
| startPos/startRot, placementType, scaleMultiplier/textureScaleMultiplier/croppedPivotOffset, lockRotation/lockScale/lockPosX/lockPosY | G/scrDecoration.cs:178, :283, :392, :403, :521, :553; G/scrParallax.cs의 SetTrans | 일부 초기 상수·카메라 투영 지원, 동적 배치 변경 제외 | 단순한 것은 T 계단; Tile 재배치/행성/바닥/외부 쓰기는 L 최종 행렬 |
| followPlanet, parentFloorNum/parentFloor, stickToFloor, syncFloorDepth | G/scrDecoration.cs:220, :403, :521; G/scrVisualDecoration.cs:327 | 거부 | L, parent Transform과 opacity·호출 시점의 sorting 값 유지 |
| rendererEnabled, forceHide, rendererColor, GetVisible, spriteRenderer.enabled, spriteMask.enabled, forceRenderingOff, activeInHierarchy | G/scrVisualDecoration.cs:385, :414, :431, :475 | G1의 논리 visible/렌더 억제 분리만 일부 지원 | 논리 업데이트 여부·실제 그리기·최적화 소유 억제를 별도 저장. 마스크 재활성화의 오래된 Transform까지 보존 |
| sprite/CustomSprite, spriteUnscaledSize, originalSize, smoothing, repeatX/Y, drawMode/size/flip, UV/triangle/bounds | G/scrVisualDecoration.cs:206, :227, :247, :397, :438; G/scrDecorationManager.cs:408 | 이미지 timeline 일부, nonquad/Tiled/split-alpha/동적 크롭 등 제외 | T/L 자산·도형 버전 + Simple/Sliced/Tiled/일반 메시 어댑터. 준비 시점 텍스처 값 고정은 충분하지 않음 |
| maskingType, SpriteMask customRange/front/back layer/order, alphaCutoff, maskInteraction, SpriteAlphaMask.targetTag/cache/RT | G/scrVisualDecoration.cs:347, :475, :513; G/SpriteAlphaMaskUtils.cs; G/SpriteAlphaMask.cs | 표·장면 양쪽 거부, 맵 자체는 PartialMaskAdmit로 허용 | 활성 엔진 스텐실과 alpha-RT 분기 별도 그리기 기능. N 제공자를 남겨도 소비자 통합 mesh가 자동으로 stencil 참여하지 않음 |
| BlendModeEffect/meshBlendModeEffect, blendMode, material shader/keywords/pass/queue, MPB, cfpCache | G/scrVisualDecoration.cs:88, :450; G/BlendModes/BlendModeEffect.cs:270, :390, :465 | 일부 혼합, 제한 shader/MPB만 허용 | 검증된 상태는 D(그리기 기능), 임의 셰이더/필터는 N. L은 재질 동작을 복제하지 않음 |
| hitbox/useHitbox, type/scale/offset/rotation, activeCollider, detectTarget/DecoTags/EventTags, hitOnce/lastHitFrame/lastTriggerTime/repeatInterval, conditional/hitEffects/eventTweens | G/scrDecoration.cs:430, :467, :487, :959; G/scrVisualDecoration.cs:272 | 거부, 현재 소유 경로는 Transform·CPU 트윈 생략 | L 또는 N으로 물리·Tween 콜백 유지. GameObject layer를 LOD 전용 31로 옮기면 물리 계약도 검토 필요 |
| selectionBorders/hitboxBorders/editorCollider/drag·hover·selection 상태 | G/scrDecoration.cs:333, :384, :950; G/scrDecorationManager.cs:466, :602 | editor-borders 거부 | 에디터 N. 편집용 표시가 실제 렌더 장벽이면 계획에 포함 |
| ToggleVisible, scrParallax, 외부 MonoBehaviour, SortingGroup, manager parent, renderer/child hierarchy | LiveDecoScripts.cs:14; GpuDecoScene.cs:212; LiveDecoGeometry.cs:70 | 알려진 바인딩만 허용, 외부 스크립트 거부 | L로 행렬 읽기만 할 때도 원래 script를 유지. SortingGroup은 그룹 내부 순서와 외부 원자성을 모델링하는 N/전용 어댑터 |

### 2.3 장식을 건드리는 이벤트 전수

`G/**/ffx*.cs`에서 장식 타입·GetTaggedDecorations·decManager 참조를 검색했다. 직접 관련 효과는 아래 일곱 파일이다(`ffxRecolorFloorPlus`는 관련 타입 참조 확인 결과 실질 쓰기 여부도 아래 구분). 그 외 카메라·타일·행성 효과는 위 공통 필드가 참조하는 외부 입력을 바꾸므로 무관하다고 버리지 않는다.

| 효과 | 입력·실행과 범위 | 현재/설계 |
|---|---|---|
| MoveDecorations (`G/ffxMoveDecorationsPlus.cs:115`, :376, :433) | positionOffset/pivotOffset/rotationOffset/scale/color/opacity/parallax/parallaxOffset, relativeTo, visible, depth, decorationImage, originalSize, cropPivotOffset, imageSmoothing, maskingType/Target/useMaskingDepth/front/back, duration/ease/angleOffset/tag. `forceDontTweenMovement`도 순서에 포함. scalar/vector scale 및 AxisConstraint.X/Y, NaN 축 생략 | 16채널+G1/이미지 일부 T; 그 밖의 상태 L→D/N. 즉시 필드 적용 순서 visible→depth→image→size→crop→smoothing→mask를 보존 |
| SetText (`G/ffxSetTextPlus.cs:25`, :46) | decText(로컬라이즈), tag; 해당 텍스트의 SetText, 시각 품질 조건 | N. 텍스트 내용 변경 때 캐시 무효화, 정적 캐시 사용 시도도 SetCollider 코루틴을 제거하지 않음 |
| SetObject (`G/ffxSetObjectPlus.cs:104`, :237) | planetColor/TailColor; trackAngle/ColorType/Color/secondaryColor/colorAnimDuration/Opacity/Style/Icon/IconAngle/IconFlipped/RedSwirl/GraySetSpeedIcon/GlowEnabled/GlowColor/IconOutlines; tag, duration/ease | 복합 N/L. 내부 floor 컬러/아이콘/메시 변경을 이미지 색 하나로 축소 금지 |
| SetParticle (`G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:90`, :271) | color/colorOverLifetime, playMode, maxParticles, lifetime/size/sizeOverLifetime/rotationOverTime/velocity/limitVelocity, shapeType/radius, emissionRate, simulationSpeed, arc/arcMode, lockRotation/lockScale, 각 use 플래그와 tag/duration/ease | N. ParticleSystem 모듈·실제 생존 입자 상태 유지 |
| EmitParticle (`G/ADOFAI.FloorFX/ffxEmitParticlePlus.cs`, 부록 B) | 태그 대상 입자 방출 파라미터·burst 실행 | N. 발행 시점과 seed/RNG를 보존, 시간표의 최종 위치만으로 재현 불가 |
| SetFilterAdvanced (`G/ffxSetFilterAdvancedPlus.cs:118`, :161) | targetType(Camera/Decoration), plane, decorationTag, filterName, enable/disableOthers, reflection public field + Tween | 컴포넌트 필터 제외. N에서 원래 RT·OnRenderImage 파이프라인 유지; true 분기에선 장식 RT 적용이 비활성인 경우도 원래대로 유지 |
| RecolorFloor (`G/ffxRecolorFloorPlus.cs`, 부록 B 참조) | 주로 floor 색 효과. 장식의 parentFloor opacity/color 및 Object의 내장 floor 변화와 함께 확인 | 직접 이미지 16채널로 추가할 효과가 아님. 실제 parentFloor/내장 floor 값은 L/N이 관측 |

전역 `ffxSetFilterPlus` 등 카메라 필터, `ffxCameraPlus`, 타일 이동·색/opacity/scale·깊이, 행성 변화는 렌더 대상·카메라·부모 입력을 바꾼다. 통합 렌더러는 카메라별 cullingMask/RT/viewport·필터 위치를 유지해야 한다(`docs/render-system/02-render.md:7`, `G/scrDecoration.cs:403`). 전역 화면 효과를 장식에 미리 구워 넣으면 적용 순서를 바꾼다.

선언과 실제 Decode도 구분한다. `maskingTargetUsed`/`targetmaskingTarget` 필드와 StartEffect 분기는 있지만, 읽은 `G/ffxMoveDecorationsPlus.cs:433`의 Decode에는 이 두 필드 대입이 없고 대상 디컴파일 전체 대입 검색도 일치가 없었다. 초기 Setup은 `maskingTarget`을 읽는다(`G/scrDecoration.cs:615`). 그러므로 동적 target 변경을 일반 맵 이벤트가 항상 발생시키는 것처럼 세면 안 된다. 외부/직접 필드 설정 시의 실행 경로는 지원 계약에 포함한다. `forceDontTweenMovement`도 선언·실행 분기와 직렬화 Decode 여부를 구분한다.

### 2.4 명세 7절의 미확인 항목 보완

1. **생성 및 RepeatEvents/조건 전개**: `G/scnGame.cs:970`은 repeatType(Beat/Floor), repetitions/floorCount, interval, executeOnCurrentFloor, gapLength를 태그별로 모은다. :1045의 반복은 floor/각도 offset을 달리해 ffx를 만든다. :993의 조건 태그는 perfect/early/late/veryEarly/veryLate/tooEarly/tooLate/loss/checkpoint 9종이며 :1060에서 effect에 연결한다. :1374의 runManually, :1794의 conditional 목록, :1812의 수동/타격 분리 후 :1834의 `(startTime-startEffectOffset, floor.seqID)` 정렬을 거친다. 따라서 원본 JSON만 반복해서 읽는 표는 부족하고 **실제 전개된 effect 목록과 실제 발행 시각**이 T 출처여야 한다. 수동/타격/조건은 L로 보낼 수 있다.
2. **종류와 Prefab**: 2.1에서 보완했다. 원래 렌더러를 유지해도 Canvas/복합 렌더러 장벽까지 등록해야 완료다.
3. **DOTween AxisConstraint**: 게임 호출은 X/Y `SetOptions`이고 축별 NaN을 생략한다(`G/ffxMoveDecorationsPlus.cs:296`). 내부 Vector2Plugin 소스는 이번 대상 디컴파일 폴더에서 찾지 못했다. GPU가 같은 수식을 쓴다는 주장만으로 내부 산술까지 확인했다고 하지 않는다. 진행 중 축 교체·Kill(true)·0길이·음수/0 scale의 비트/프레임 대조가 남는다. 미지원/사용자 ease는 L이면 원래 Tween을 계속 실행할 수 있다.
4. **다른 모드 setter**: `DecoBake.cs:413`, `GpuDecoPlayer.cs:87`은 외부 패치를 검출해 차단한다. 이번 실행에서 패치 목록을 수집하지 않았으므로 PACL2 FollowDecoration의 모든 구현을 확인했다고 하지 않는다. L은 필드뿐 아니라 **실제 renderer 행렬·재질/MPB**를 읽고 원래 callback을 유지해야 한다. Transform을 읽는 모드에는 T 소유 중에도 최신 CPU mirror가 필요할 수 있다. 임의 외부 native render hook까지 “항상 지원”을 증명할 수 없으므로 불명 상태도 N 등록·관측 결과로 명시한다.
5. **sortingOrder 경계**: 게임은 int depth를 `sortingOrder=-depth`로 직접 쓴다(`G/scrVisualDecoration.cs:327`). 저장소의 `SceneRenderOrder`도 int이며 Unity native setter의 clamp/wrap 결과를 대신 증명하지 않는다. 32767/32768/-32768/int 경계는 **원래 렌더러 setter 후 읽은 실제 Layer/Order**를 정렬키로 써서 L/N 호환을 확보한다. T 사전 계획은 그 정규화 계약을 별도 측정 전에는 가정하지 않는다.

## 3. 거부·내리기·전체 포기 경로의 대응표

표의 구현 크기는 상대 평가이며 공수 약속이 아니다. 비용 표기: **C1**=변경 시 계단/키 갱신, **CL**=L 최종 상태 업로드(4절), **CD**=원래 픽셀·pass/복사 비용 유지 + 구간 제출, **CN**=원래 스크립트/renderer 비용 유지 + 장벽 계획, **CP**=로딩/재계획 시 전수 검사. 어떤 표기도 측정된 ms가 아니다. `D`는 그리기 기능을 뜻한다. 동일한 문자열이 두 곳에서 나오는 경우 부록 A에 발생 위치를 각각 남긴다.

### 3.1 DecoBake의 모든 의미적 사유

`Reasons.Add` 외에도 `Reasons(f)`의 `r.Add`, PlacementReason/MovementReason 반환 문자열과 `목표값 비유한`을 포함했다. 이유 전파용 `foreach reason`은 새 사유가 아니다.

| 정확한 사유(함께 표기된 것들도 개별 경로) | 현재 원인 / 게임 동작 근거 | 거부 없는 목적지 | 크기·위험 / 비용 |
|---|---|---|---|
| Global 기준 원본 위치 없음 | 초기 원점 snapshot 불충분(DecoBake.cs:241); G/scrDecoration.cs:521 | L로 실제 startPos/행렬 캡처, T 초기화 증거 확보 후 승격 | 중간·원점/중간시작 / CL |
| 이미지 이외 장식 | DecoBake.cs:251; 2.1의 각 구현 | 종류별 N, 정적 글자 캐시는 별도 D | 큼·Canvas/복합순서 / CN |
| 히트박스/충돌; 히트박스/조건 연결 | DecoBake.cs:252, :415; G/scrDecoration.cs:430, :467 | L/N. 물리·Transform·이벤트 실행 유지 | 큼·판정 변화 / CL+원래 물리 |
| 초기 마스크; 마스크 대상 | DecoBake.cs:253, :416; G/scrVisualDecoration.cs:475 | L/T 마스크 상태 + D stencil/alpha-RT 또는 N | 큼·중첩/범위 / CD |
| 시차 부품 없음; 바닥 부착; 행성 추종 | DecoBake.cs:254, :257, :258; G/scrDecoration.cs:403 | L 실제 최종 행렬, 누락 부품의 원래 no-op도 보존 | 중간·부모/충돌 / CL |
| 초기값 비유한; 목표값 비유한; 길이 비유한/음수; 크기 산술 비유한 | DecoBake.cs:259, :359; DecoBake.Core.cs:344, :376 | 원래 게임 실행을 N으로 유지, 잘못된 수를 GPU 계획/버퍼로 흘리지 않음. 무조건 GPU 소유라는 뜻이 아님 | 중간·NaN 정렬/드라이버 / CN |
| Camera-동적 화면 투영; 시차 화면 고정 | DecoBake.Core.cs:315; DecoBake.cs:414; G/scrDecoration.cs:392 | 현재 ScreenFixed T 상수입력 지원, 비표준 카메라는 L/N | 중간·viewport/lock / C1+공통 카메라 |
| Tile-동적 바닥 추종; 초기 배치 미지원 | DecoBake.Core.cs:316; G/scrDecoration.cs:521 | L 행렬. 미지원 enum도 N에 등록 | 중간·pause/parent / CL |
| Tile-동적 효과 시점 재배치; Camera-동적 배치 변경; Global 이외 배치 변경 | DecoBake.Core.cs:322; G/scrDecoration.cs:521 | 이벤트 시점 game setter 실행→L. 증명된 경우 T 계단 | 중간·현재 위치 캡처 / C1+CL |
| 외부 모드 장식 setter 패치 | DecoBake.cs:413; setter의 원래 호출은 G/scrDecoration.cs:229 이하 | L/N, 외부 script는 실행. 전역 차단 제거는 수명·쓰기 관측 확보 후 | 큼·임의 side effect / CL+CN |
| 다른 장식 효과/태그 변경 가능 | DecoBake.cs:451, :469; 2.3의 SetText/Object/Particle/Filter | effect 대상별 L/N, 태그 레지스트리 버전 무효화 | 중간·전역 거부 확대 방지 / C1+CP |
| 예약 목록 밖/조건/수동/타격; 조건/수동/타격 | DecoBake.cs:464, :325; G/scnGame.cs:1794; G/ffxPlusBase.cs:105 | 실제 callback→L. 이벤트별 대상 단위 전환, 다른 대상 T 유지 | 큼·중복 실행/타이밍 / CL |
| 시각 효과 설정 조건 | DecoBake.cs:326; G/ffxPlusBase.cs:187 | 원래 조건 통과 이벤트만 T 발행, 또는 L | 중간·Full/Minimum/품질 / C1 |
| 이미지 변경 | DecoBake.cs:330; G/ffxMoveDecorationsPlus.cs:384 | 기존 이미지 계획 확대 + L 자산 버전, 실패는 N | 큼·빈 이미지/재질/콜라이더 / C1+자산 업로드 |
| 미확인/사용자 이징 | DecoBake.cs:333; G/ffxMoveDecorationsPlus.cs:156 | L 원래 Tween. 유한값 상태만 GPU 전달 | 작음~중간·콜백 / CL |
| 초기 부가 상태 없음 | DecoBake.G1.cs:37, 스냅샷 :17; G/scrDecoration.cs:553 | 타입별 초기 레코드/N. 비이미지에 Image snapshot 강제 금지 | 중간·타입별 초기화 / CP |
| 깊이 변경; 깊이 변경 많음 | DecoBake.G1.cs:42; G/scrVisualDecoration.cs:327 | T/L sorting 상태 + D 재정렬/사전 인스턴스 | 큼·동점/동적 깊이 / C1, 계획 크기 O(깊이 수) |
| 크롭 변경; 부드럽게 변경; 원래크기 변경/이미지 의존 | DecoBake.G1.cs:50; G/ffxMoveDecorationsPlus.cs:397 | T 계단 또는 L 최종 도형/샘플러 갱신 | 중간·이미지 변경 순서 / C1+CL |
| 마스크 종류/숨김 재활성화; 마스크 대상 변경; 마스크 범위 사용 변경; 마스크 깊이 변경 | DecoBake.G1.cs:60; G/scrVisualDecoration.cs:347, :475, :513 | L/T 논리 상태와 실제 그리기 분리 + D/N(5절) | 큼·숨김 Transform/범위 / C1+CD |

### 3.2 GpuDecoScene / LiveDecoRenderer의 문자열 전수

두 파일은 동일 역할의 다른 구현이다. LiveDecoRenderer는 CPU에서 최종 정점을 채워 mesh run을 올리는 기존 연구 경로이지, 그 자체로 제안 L의 제품 구현이 아니다(`LiveDecoRenderer.cs:1014`, :1057).

| 정확한 reject/release 문자열 계열 | 현재 제약과 게임 근거 | 목적지 | 크기·위험 / 비용 |
|---|---|---|---|
| camera-layer | 카메라가 원래 안 그리는 레이어(GpuDecoScene.cs:176; G/scrVisualDecoration.cs:327) | 카메라별 D 필터. 등록은 유지, 그 카메라에서만 안 그림 | 중간·다중 카메라 / C1 |
| subclass | 정확한 타입만 허용(Scene :177, Geometry :215; 종류 2.1) | 파생형 L/N 어댑터 | 중간·override / CL/CN |
| mask; hitbox; hitbox-mask | Scene :178, :179, :216; 게임 2.2 | 마스크 D/N, 충돌 L/N | 큼 / CD+CL |
| mesh-live:blend=…; mesh-mode | Scene :182, :219; G/scrVisualDecoration.cs:88 | 실제 mesh/filter N, 검증된 mesh D | 큼·RT/필터 / CN |
| sorting-group | Scene :183; 원래 Unity 그룹 정렬 | 그룹 N 또는 계층 정렬 D. 그룹 자체를 스캔에서 건너뛰는 것만으로 지원 아님 | 큼·원자성 / CN+CP |
| missing-sprite-texture; sprite-texture | Scene :184, :235; G/scrVisualDecoration.cs:206 | 비어 있음은 등록된 empty draw; 자산 교체 시 재활성화. 잘못된 자산은 N | 중간·빈→유효 / C1 |
| split-alpha | Scene :185; Sprite의 외부 alpha texture | D 외부 alpha 두 샘플 또는 N | 중간·색/알파 / CD |
| sliced-or-tiled; repeat; repeat-tile | Scene :186, :187, :218; G/scrVisualDecoration.cs:397; SpriteRenderer drawMode는 별도 엔진 기능 | D Simple-repeat와 Sliced/Tiled 메시 분리(6절) | 큼·샘플/경계 / CD |
| blend-effect; blend | Scene :189, :220; G/scrVisualDecoration.cs:450 | 모드별 D/N(6절) | 큼·배경 의존 / CD |
| material-slots | Scene :190; Object/Prefab은 여러 재질 가능 | 각 submesh/pass D/N, 단일 item에 강제 금지 | 큼·pass순서 / CD |
| shader-pass-queue; shader-pass-queue:이름:pass:queue; disabled-pass; keywords | Scene :195, :196, :202, :203, :206~208 | 알려진 shader D; 나머지 N. disabled pass는 그 pass만 실행 안 함 | 큼·재질 뜻 / CD/CN |
| property-block-custom:속성; property-block-unresolved | Scene :157; BlendModeEffect MPB stencil/overlay(G/BlendModes/BlendModeEffect.cs:465) | 유효 renderer/slot MPB를 D 또는 N에서 보존 | 큼·열거 불완전 / C1+CN |
| parent-follow | Scene :217; G/scrDecoration.cs:403; syncFloorDepth :327 | L 최종 행렬/색/키 | 중간 / CL |
| runlod-split | Scene :221, Live :481; RunLod.cs:42 | 소유권 중재. RunLod도 통합 노드로 계수, 마지막 단계에서 단일 LOD로 통합 | 중간·소유 충돌 / C1+CN |
| parallax-binding; pivot-parent; child-missing; nonplanar-child; camera-binding | Scene :222~229; G/scrDecoration.cs:229, :403 | 기존 가정 대신 L 최종 matrix/mesh. 유효하지 않은 객체 N | 중간~큼·좌표계/3D / CL |
| component-filter; live-child-renderer; editor-borders | Scene :230~234; 게임 필터 :88, 경계 :333 | 복합 N, 하위 draw마다 키 등록 | 큼·누락 draw / CN |
| inactive | Scene :245; G/scrVisualDecoration.cs:414, :475 | 수명 레코드 유지, actual draw flag만 변경. 깨울 때 현재 상태 먼저 업로드 | 중간·재활성 / C1+CL |
| child-state-changed | Live :487; 외부 child scale/rotation/flip 변경 | L 도형 버전 갱신 | 중간·dirty 누락 / CL |
| visibility-node; visibility-target:타입; foreign-child-script:타입 | LiveDecoScripts.cs:26~32; ToggleVisible가 다른 script/Transform을 참조할 수 있음 | 원래 script 유지하는 L/N | 큼·읽기 side effect / CN+CL |
| nonquad; child-chain-unsupported; vertex-error-px; bounds-error-px | Live :578, :582, :610; Geometry.Capture :70 | D 일반 topology/L matrix, 원래 N도 가능. 정확도 검사를 삭제하지 않음 | 큼·culling/래스터 / CL+CD |
| destroyed; renderer-disabled; inactive-hierarchy; logical-invisible; scene-or-foreign-force-off | Live :465~470 | 수명/논리/소유 flags 분리, 파괴 객체만 등록 해제 | 중간·꺼짐/파괴 구분 / C1 |
| write-종류; boundary:메서드; static-invalidated; material-tie; alpha-zero; scene-reset | Live :342, :702, :727, :759, :838, :897 | 변경을 L dirty/재계획으로 수용. alpha 0에서 영구 반납하지 않음. reset은 정상 수명 종료 | 큼·setter 재진입/순서 / C1+CL+CP |
| boundary(기본 release) | Live :673 | 대상별 표현 전환 트랜잭션 | 중간·두 번 그림/누락 / C1 |

### 3.3 사전 내리기·자원 제한·지도 전체 비활성화

| 경로 / 코드 근거 | 왜 현재 못 맡나 / 게임 동작 | 통합 설계 | 크기·위험 / 비용 |
|---|---|---|---|
| `material/mask`, `keywords`, `tint/opacity`, `texture-changed`, `mipmap`, `wrap` (GpuDecoPerItemPrepare.cs:12) | shader/색은 상수, 텍스처 ID·mip 1·UV 조건만 허용. 원래 재질/텍스처가 최종 결과를 결정 | D에 실제 tint/sampler/mip/외부 alpha 포함, 불명 shader N | 큼·이중 tint/색공간 / C1+CD |
| `direct-texture-overflow` (GpuDecoDirectTexturePrepare.cs:77, :100) | 배열 4 + 직접 48 바인딩. 일부 제외 후 최소 수 판정. shader 매 픽셀 직접 분기 | **정렬 연속 구간 내부에서 자원 페이지로 추가 분할**. 모든 texture format/크기/mips를 보존하는 페이지. 같은 크기·포맷 배열 우선, 남은 것은 한 텍스처 draw도 정식 D. 모든 맵 이미지를 무조건 한 배열로 패딩하지 않음 | 큼·VRAM/드로우 증가 / CD+CP |
| 동점 묶음 사전 정리 (GpuDecoDrawCollapseRenderer.cs:141) | 활성 장벽의 같은 bucket/셰이더 사이에 섞인 후보 제거. KeepBefore/After 및 nudge는 제한적 우회 | 실제 원래 순서의 draw 노드별 계획. 같은 재질까지 같아 엔진 최종 tie를 모르면 해당 bucket N으로 등록; 임의 ID 정렬/깊이 nudge를 정답으로 삼지 않음 | 큼·알파 비가환 / CP+CN |
| 화면 복사 혼합 묶음 내리기 (:115) | Grab이면 다른 renderer가 같은 bucket에 하나만 있어도 제거. non-Grab blend도 다른 shader가 있으면 제거 | 원래 copy 지점/패스 순서별 N/D. 공유 Grab을 전체 프레임 한 번으로 바꾸면 다른 그림 | 큼·복사 시점 / CD |
| Collapse 미포함/texture fallback (:368, :422, :650, :1160) | 자원·배열 slices·미지원 형상·tie·깊이 등에 따라 독립 GPU renderer 유지. **항상 CPU 복귀가 아님** | 배치/독립 GPU/N을 명시적 표현으로 계수. 독립 fallback의 compute/native copy 비용도 측정 | 큼·이중 소유 / CD |
| CPU fallback→장벽 (:1001) | 활성 native Tween 등이 있으면 원래 renderer와 CPU 실행 유지 | L로 같은 item을 공급하거나 N, 다른 item은 유지 | 중간~큼·현재 Tween 상태 / CL/CN |
| DepthInstances 추가 깊이 모호 (:632), 미준비 깊이 (:748) | 예정된 깊이 8개 초과·장벽 충돌·미준비 깊이는 통합 제외/전체 복귀 | 이벤트 시점 L sorting 키 + 해당 bucket 재계획. 사전 예약은 최적화일 뿐 지원 조건으로 쓰지 않음 | 큼·새 깊이 / C1+CP |
| 최소 64: 표 지원/사전 rows/최종 indices (GpuDecoPlayer.cs:549, :600; GpuDecoPreparation.cs:11; Export :1081) | 작은 남은 묶음이 배치 이득 없다고 전체 끔 | 등록·지원 임계값 제거. 비용 따라 표현 선택, N만 있어도 전체 기능 작동. 이 변경 자체의 FPS 이득은 없음 | 작음~중간·상시 비용 / CN |
| workload calibration (GpuDecoPlayerPolicy.cs:30, Player :563) | ownership/visibleApplications/visibleMoving의 과거 특정 창 문턱 | 성능 정책만으로 사용. 지원 가능 여부와 분리. 다른 맵 수익성 증명 아님 | 중간·오판 / CP |
| platform/bundle/compute/native copy (Player :38; Export :887, :900, :1207; CollapseRenderer :262, :325) | D3D11·compute·array copy·slices·VRAM·native callback 제약 | 정식 N backend. 자원 실패를 장식 미지원으로 숨기지 않고 표현 전환, 복원 검증 유지 | 큼·자원 실패 / CN+CP |
| 전역 foreign setter/카메라/SortingGroup/마스크 gate (Player :85~95; Export :889~896; CollapseRenderer :205) | 기반 FastMove/DecoAnim, identity manager, 직교 Z 정렬/viewport, PartialMaskAdmit | L/N으로 해당 객체/카메라만 경로 선택. 알려지지 않은 전체 장면 순서면 카메라 N backend 유지 | 큼·플랫폼/다중 카메라 / CL+CN |
| MaxSpans=3000, 텍스처 배열 4, 정렬키/material/layer/blend state 일치 검사 (DrawCollapsePolicy.cs:164; CollapseRenderer :1199, :1220, :1227) | 구간 수/재질 상태 제약 | 페이지·노드 예산은 성능 정책. 넘으면 일부 노드 N; 재질·layer가 다른 draw를 강제로 합치지 않음 | 큼·리소스 급증 / CD+CP |
| LiveDeco 진입·실행 throw (LiveDecoRenderer.cs:167, :438, :631, :667, :853, :865, :1014) | playing legacy, 경쟁 owner 없음, Built-in/직교 Z/identity, 유한 bounds, live SortingGroup·불명 material 미지원 | 기능은 N, renderer-only/L의 범위를 명시. 오류·메모리 레이아웃·잘못된 index는 정상 지원 정책으로 완화 금지 | 중간~큼·복원 / CN |

`PartialMaskAdmit=true`가 현재 기본이므로 “마스크가 있으면 언제나 맵 전체 거부”는 현재 제품 설명으로 틀리다(`DrawCollapsePolicy.cs:13`, `GpuDecoPlayer.cs:94`). 반대로 마스크 제공자를 그냥 두었다는 사실은 통합 mesh의 마스크 소비자 지원을 뜻하지 않는다.

### 3.4 활성화 거절·재시도·CPU 복귀의 전체 분류

부록 A는 조건문과 오류 문자열을 위치별로 남겨 추적할 수 있게 했다. 정상 수명 종료·연구 gate 실패·제품 복구를 서로 구분해야 한다.

| 경로 | 현재 조건·효과 | 거부 없는 구조에서 처리 / 크기·위험·비용 |
|---|---|---|
| 설정·lifetime (GpuDecoPlayer.cs:32, :77, :137) | GpuDeco!=1, 준비 안 됨/Blocked/Attempted, 게임 상태·pause/시작·에디터·catch-up이면 진입 안 함. 실패 후 해당 맵 재시도 제한 | 지원 레지스트리 수명과 배치 활성화 수명 분리. 정상 N 재생을 계속 유지 / 중간·재진입 / C1 |
| native 이어받기 (GpuDecoActivation.cs:20~81) | 죽은 객체, 활성 native Tween 미포착/Rec 불일치/길이<=0/지원 ease 아님 → CPU fallback. 미초기화 native는 최대 30프레임 재시도, fallback은 독립 renderer 필요, safe GPU target 0이면 거절 | 대상별 L/N, 이어받기 검증된 T만 전환. native identity·Elapsed·IsPlaying·callback 보존 / 큼·트윈 연속성 / CL |
| 준비 자료·세대·시간 (DecoBake.cs:167; Export :872, :1660; Player :417, :652) | 표·bundle·baseline·snapshot/hash·scope 미일치, static native combine 미검증, recoveryBlocked | 자료를 대상별 다시 수집하거나 N, 되감기/맵교체는 세대 전환. 미확정 상태를 T로 추측하지 않음 / 큼 / CP+CN |
| 효과 발행 실패 (GpuDecoRecovery.cs:54; Export :331; FastMove.cs:183, :235) | G1 상수 필드 변경, 미지원 effect/ease/NaN·source pool 고갈, native 등장, FastMove 원래 처리 필요 → 전체 RecoverGpuLive | 해당 actor T→L 전환을 원래 effect 실행 전에 완료. 현재 구조의 전체 복귀를 그냥 target 하나로 축소하면 이미 진행된 다른 target 중복 실행 위험 / 큼 / C1+CL |
| frame 검사 (Player :734~760) | 4프레임마다 최대 4개 순환 검사: 카메라/manager, FastMove/DecoAnim, copy errors, renderer·texture·mask/group·material/keywords·원본 억제 변화 | dirty observation과 L/N 전환. 현재 검사는 프레임마다 전수 보장 아님. 변경 즉시 처리해야 같은 프레임 정확 / 큼 / CL+CP |
| 순서/창/환경 변화 (CollapseRenderer :1523, :1642; Export :1362) | prepared window 밖, 포함 밖 visible, 깊이 미준비, 정렬 상태 변경·pending reason·시간 위반 → 제품 throw→LateUpdate 복귀 | 해당 bucket 재계획 또는 N. 순서 위반 로그만 남기고 계속 그리는 설정은 합격 증거가 아님 / 큼 / CP+CN |
| 이미지 상태 staging/flush (GpuDecoImagePlayback.cs:306, :316, :325) | 자산/selector/geometry 변경 계약 실패 → recovery | 자산 업로드 전까지 N, 교체는 한 프레임 트랜잭션 / 큼·빈 이미지/복원 / C1+CD |
| 활성화 도중 예외 (Export :1650~1735; Activation :91~132) | undo 기록·native adoption 검증·원본 억제·GPU 렌더러 켜기에서 실패, commit 전 rollback/후 recovery | 렌더 표현별 undo. 원래 Tween을 먼저 파괴하지 않음 / 큼·검은 프레임/이중 그림 / C1 |
| 복구 실패 (GpuDecoRecovery.cs:93~123; Export :1770) | Resume 사전 검증 실패면 DEGRADED, 원본 렌더러 억제 해제·GPU 정지·blocked | 이 안전장치 유지. “거부 없음” 때문에 부정확한 Tween 경로를 발명하지 않음. 가능하면 L/N 원본 실행 유지로 복구할 상태 자체를 줄임 / 큼·연속성 / CN |
| 정상 종료/clear (Player :187, :615; Export :417, :1891, :1920; SceneReset.cs:40) | round_end, GameCallback, 결과 전환, 맵교체, shutdown/unload, Application.quitting, Dispose, off/reset | 거부가 아닌 정상 lifecycle. DeferEndOnResult 조건도 보존, 원래 렌더·콜라이더/플래그 복원 / 중간 / C1+CP |
| 연구/진단 gate (GpuDecoData.cs:20; GpuDecoDrawCollapseGate.cs:148; LiveDecoPolicy.cs:73) | 픽셀>3, 변경비율>0.3%, low ownership, primary density/표본 부족, restore 불일치·INCONCLUSIVE | 결과를 FAIL/미확인으로 그대로 유지. 제품 기능 입장 조건과 혼동하지 않음 / 작음 / 진단 시만 |

## 4. L 경로 구현 가능성, 비용, 갱신 시점

### 4.1 기존 구조에 재사용할 수 있는 것과 새로 필요한 것

**가능하지만 상태 공급 인터페이스를 추가해야 한다.** `GpuDecoGpu.LiveChannel`은 float start/change/end와 int sourceIndex로 16B이며 sourceIndex<0이면 end 상수를 쓴다(`GpuDecoGpu.cs:20`). 현재의 live라는 이름은 “게임 최종 상태를 매 프레임 읽는 L”이 아니라 **이벤트 시작/완료 때 채널 descriptor를 쓰고 CPU에서 실제 ease source를 진행시키는 구조**다(`GpuDecoExport.cs:1360`, :1388; `GpuDecoGpu.cs:223`). 기존 소스 시계·채널 참조/dirty 처리와 연결해서 L 값을 상수 end로 넣을 수는 있다. 게임 스크립트를 생략하는 현재의 `GpuLiveSkip`/TightLate 소유 판정과는 분리해야 한다.

`WriteItemSlots`는 4B 항목 인덱스/빈 슬롯과 깊이를 갱신할 뿐이다(`GpuDecoDrawCollapseRenderer.cs:703`). 위치·색 업로드 API가 아니다. 변경 슬롯마다 `SetData(...,1)`할 수 있으며 `UpBytes`에 포함되지 않는다. `UpBytes`는 현재 채널 16B와 source float 4B만 더한다(`GpuDecoGpu.cs:228`, :238). image static/UV/matrix 쓰기(:176, :196), slot map, mesh upload, 텍스처 upload, native copy를 전부 뜻하지 않는다. L 성능을 잴 때 카운터 확장이 필요하다.

T를 순수한 곡 시각 함수로만 일반화할 수 없는 사례도 있다. LastPosition은 현재 pivotPosVec를 **Kill(true) 전에** 끝점 기준으로 읽고, 트윈 시작값은 이전 트윈 완료 콜백을 실행한 뒤 읽는다(`G/ffxMoveDecorationsPlus.cs:146`). 효과가 실제로 발행된 프레임과 실행 순서에 따라 값이 달라진다. 현재 live가 실제 source를 관측하는 이유이며, 이 정보를 확보하지 못하면 L로 보내야 한다. offline 예약 표와 actual-source live는 서로 다른 정확도 계약이다.

제안 인터페이스는 actor identity/generation과 `StateProvider(T/L)`, `DrawAdapter(D/N)`, 논리 업데이트 flag, 실제 draw flag, 도형/자산/재질/정렬 버전으로 구성한다. 원본 renderer 억제는 등록 및 데이터 업로드 성공 후 commit하며, 실패하면 그 프레임은 N이다. 이미지가 비어 있거나 alpha 0이어도 actor를 없애지 않는다.

L을 두 수준으로 구분한다.

- **L 논리 채널**: 원래 script가 16채널을 계산하고 GPU가 기존 변환식을 적용. CPU Transform 소비자가 없고 최종 그림이 이 식과 같다는 검증이 필요하다.
- **L 최종 렌더 상태**: 원래 script/Transform/물리를 실행하고 Renderer의 최종 localToWorld·sprite topology·색·MPB·정렬을 읽는다. 외부 parent/3D/동적 크롭/잠금/행성·바닥에 더 일반적이다. 현재 16채널/상수 형식에는 완전한 final-matrix override가 없으므로 새 descriptor 또는 kernel 분기가 필요하다. N은 그보다 일반적인 원래 draw 실행이다.

### 4.2 항목당 비용: 숫자는 메모리 산술만

| 방식 | 갱신 payload 산술(제안) | 이 숫자에 들어 있지 않은 비용 |
|---|---|---|
| 기존 LiveChannel 16개를 모두 상수화 | 16×16=**256B/변경 actor/업로드 대상 slice** | getter/분기/dirty 관리, 원래 CPU 계산·Transform 쓰기, API 호출, slot/자산/도형 |
| 전용 logical values buffer | 16×float=**64B/변경 actor** | 새 kernel/모드 태그·alignment, 위치 상수/행렬·재질 |
| 최종 행렬+색 | 4×4 float 64B + float4 색 16B = **80B/변경 actor**의 단순 필드 합 | 도형/UV/flag/정렬/자산 ID와 padding은 별도. GPU struct 실제 stride는 구현 후 검증 |
| 기존 CPU 정점 경로(LiveDecoRenderer) | position 12 + float color 16 + UV 8 = 36B ×4=**144B/quad** | run 단위 upload로 dirty 이외 이웃도 전송, 인덱스·CPU 계산·native 복사 |
| 깊이/visible slot | **4B×바뀐 예약 자리 수** | 현재 즉시 SetData 호출 비용, 배치 dirty 범위 확대, 비활성 예약 자리 메모리 |

예를 들어 3,420개 모두 매 프레임 변경한다고 **가정**하면 256B 경로는 875,520B/frame, 64B 경로는 218,880B/frame, 80B 최소 필드 합은 273,600B/frame이다. DD의 깊이 단독 거부 수로 한 산술 예일 뿐, 그 actor가 모두 움직이거나 L이 필요하다는 실측이 아니다. FPS/PCIe 대역폭만으로 ms를 예측할 수 없다. 같은 slice의 dirtyMin..dirtyMax 사이가 넓으면 **실제 변경 항목보다 더 많이** 올린다(`GpuDecoExport.cs:166`, `GpuDecoGpu.cs:226`). dirty run 병합과 호출 수 상한을 같이 재야 한다.

### 4.3 언제 읽는가, 꺼짐/켜짐과 전환

현재 dispatch는 `scrDecorationManager.LateUpdate` postfix의 `TightLate.ManagerEnd`에서 한 프레임 한 번이며 PreCull은 그 프레임 dispatch가 없을 때만 보충한다(`GpuDecoExport.cs:1483`, :1519). 기존 execution-order 자료는 Camera=-400, Floor=-10, Manager=-5, 기본 script=0, Controller=400이다(`docs/render-system/data/script-order.tsv:14`, :23; `docs/render-system/04-profile-and-candidates.md` 실행 순서). **“Manager LateUpdate 뒤”가 “모든 모드의 LateUpdate 뒤”라는 뜻은 아니다.** 기본 순서 0의 외부 writer는 나중에 쓸 수 있다.

L은 카메라별 실제 culling 전 최종 상태를 확정하는 지점을 쓰고, 그보다 늦은 OnPreCull/OnWillRenderObject writer가 있다면 그 callback과의 계약 또는 N 경로가 필요하다. 단순히 manager에서 먼저 dispatch하고 PreCull을 frame guard로 생략하면 당일 프레임의 L 상태가 늦어진다. 제안 순서: game updates/late → 등록된 writer 완료 → 카메라별 L snapshot/dirty upload → sort plan 확정 → GPU evaluate → 원래 같은 순서 draw. 다중 카메라/수동 Render/custom FPS에는 카메라마다 snapshot version이 필요하며 Time.frameCount 하나만으로 중복 방지하면 안 된다.

T→L은 현재 GPU 소스 상태를 CPU Tween/논리 필드에 정확히 이어주고 해당 actor의 소스 참조를 해제한 뒤 원래 effect를 한 번만 실행한다. 이미 해당 프레임에 실행된 대상은 재실행하지 않는다. L→T는 다음 증명된 이벤트 경계에서 actual start/clock/중간 Tween을 확보한 경우만 허용한다. 임의 프레임의 추정값으로 승격하지 않는다(`GpuDecoActivation.cs:91`, `GpuDecoRecovery.cs:64`).

꺼짐/켜짐은 `rendererEnabled`, 실제 SpriteRenderer/SpriteMask enabled, activeInHierarchy, 최적화 억제 상태를 별개로 추적한다. MaskingType은 visible=false 뒤에도 실제 렌더러를 켤 수 있다. GetVisible=false일 때 멈춘 **원래의 오래된 Transform**을 L이 새 수식으로 다시 계산하면 그림을 바꾼다(`G/scrVisualDecoration.cs:414`, :475; `G/scrDecoration.cs:959`). 재활성화 commit 전에 그 프레임의 texture/geometry/state/slot을 모두 올린다. collision/외부 Transform 소비자는 원래 논리를 계속 실행한다.

## 5. 마스크와 깊이 변경 가설 검증

### 5.1 마스크

코드 확인: `SetMaskingDepth`는 nullable front/back 중 생략된 값을 기존 order에서 복원하고, back<front면 교환하며, 각각 Bg/Default layer를 선택한다. frontOrder=`-frontDepth+1`, backOrder=`-backDepth`이다(`G/scrVisualDecoration.cs:353`). SpriteRenderer에는 None/VisibleInsideMask/VisibleOutsideMask가 들어간다(:475). provider는 일반 spriteRenderer를 끄고 SpriteMask를 켠다. 그 과정에서 visible과 무관하게 활성화할 수 있다. alpha-RT 분기의 targetTag는 별도로 cache를 무효화한다(:513). **현재 stencil 분기의 범위와 tag 기반 alpha-RT 분기를 같은 마스크 모델로 취급하면 안 된다.**

구현 방안(제안): 우선 원래 SpriteMask provider를 유지하는 N 노드, 소비자는 native SpriteRenderer N으로 완전 지원한다. GPU 소비자 D를 넣을 때는 카메라별 정렬키 sweep으로 활성 마스크 집합을 계산하고, 같은 마스크 집합/interaction/패스 상태끼리만 연속 구간을 만든다. 엔진이 SpriteRenderer에 적용하는 stencil 값을 mesh가 자동으로 받는다고 가정하지 말고, 명시 stencil pass 또는 mask coverage RT를 사용한다. 임의 많은 마스크를 고유 stencil bit 하나씩 배정하면 유한 bit 수에서 실패한다. 범위 변화 지점별 stencil 재구성 또는 coverage 합성 방식, 중첩·겹침·alpha cutoff·inside/outside·SortingGroup별 격리를 원래 방식과 대조해야 한다. 무조건 마지막 mask만 쓰면 틀린다.

coverage RT를 쓰더라도 일반 mask의 임계값/edge sampling, provider sprite geometry, flip/색 영향, 여러 provider가 겹친 영역의 엔진 동작을 코드만으로 전부 확정할 수 없다. 게임 C#에는 Unity 내부 stencil 할당·중첩 알고리즘이 없다. RenderDoc stencil/call capture 또는 합성 fixture로 확정할 잔여 항목이다. 이 단계에서 “마스크 수가 많아도 무조건 같은 그림”이라고 약속하지 않는다. 비용은 provider raster + 상태 변경/RT pass + 소비자 sample/stencil이며 **마스크 지원만으로 덧칠이 줄었다고 할 수 없다**.

false 분기의 alpha-RT 동작은 별도로 확인된다. `G/SpriteAlphaMaskUtils.cs:34`는 장식 태그에 연결된 provider 집합을 합치고 activeSelf를 거른다. :56은 OBB 접촉을 먼저 검사하고 provider별 크기/위치/회전을 RT 좌표로 바꾼다. :85 이후는 여러 mask texture를 묶어 ping-pong RT로 합성한다. 즉 depth-range 스텐실 sweep만 구현해 이 분기까지 대체했다고 할 수 없다. 원래 N 구현을 보존하거나 태그·OBB·회전·복수 provider 합성·inside/outside까지 별도 D를 만들며, 원래 분기가 비활성인 설정에서 새 효과를 켜지 않는다.

### 5.2 깊이: DepthInstances가 DD를 바로 해결하지 못하는 이유

`DepthInstances=false`, `MaxDepthInstances=8`은 실제 기본값이다(`DrawCollapsePolicy.cs:28`). 코드상 명시 이유는 “검증된 보편적인 깊이 지원”이 아니라 사전 예약된 위치들 중 현재 깊이만 켜는 프로토타입이라는 점이다. **false로 정한 역사적 결정/성능 원인은 코드만으로 확인할 수 없다.** DD 3,420개에 적용한 FPS·그림 합격 기록을 이 조사에서 찾지 못했으므로 추측을 확정 이유로 쓰지 않는다.

남은 제약은 확인된다. G1은 syncFloorDepth를 제외하며 깊이 수 상한을 넘기면 거부한다(`DecoBake.G1.cs:42`). 예약 깊이가 활성 장벽 또는 이미 점유한 정렬 위치와 충돌하면 통합에서 내린다(`GpuDecoDrawCollapseRenderer.cs:632`). 런타임 SetDepth는 독립 renderer의 layer/order까지 맞추지만 미준비 depth면 pending fallback으로 전체 복귀할 수 있다(:724). 중간시작 시 현재 order로 예약 슬롯을 동기화한다(:676). slots가 늘면 GPU map·span 수·검증 부담이 늘고 실제 draw가 1개만 활성이라는 사실은 예약 메모리/계획 비용을 없애지 않는다.

거부 없는 D에서는 **실제 SetDepth 후 읽은 Layer/Order/GameLayer**를 authoritative key로 하고 해당 bucket을 재구성한다. 정적으로 알려진 8개 이하 깊이 예약은 성능 최적화로 남길 수 있으나 그보다 많거나 임의 깊이여도 L/N으로 표현한다. DD의 depth-only 3,420은 우선 측정 가치가 크지만 대부분이 RunLod 분담/마스크/동점/자원 제한과 어떻게 겹치는지 별도 ID 집계 후 소유 목표를 정한다.

## 6. Repeat, 혼합, 동점, 자원과 LOD

### 6.1 7777 사슬 차이: 좁힐 수 있는 후보

코드에서 확인한 서로 다른 세 문제를 하나의 repeat 토글로 묶지 않는다.

1. `repeat-tile`에는 **decoration.repeatX/Y≠1**과 **SpriteDrawMode!=Simple**이 모두 들어 있다(`GpuDecoScene.cs:218`). `SpriteDrawMode.Tiled`는 원래 SpriteRenderer가 size/border/tileMode로 만든 메시이며 UV만 곱한 쿼드가 아니다. Sliced도 같은 거부에 섞인다. 크기/테두리/Adaptive threshold를 재현하는 geometry 경로가 필요하다.
2. 장식 필드 repeat와 **실제 material RepeatX/Y**가 다를 수 있다. `GpuDecoBlend.cs:26` 주석과 EffectiveRepeat(:28), export(:988)는 실제 material 값을 읽도록 이미 보완됐다. `G/scrVisualDecoration.cs:397`은 disableV15Features가 true일 때만 shader RepeatX/Y를 쓴다. 기존 주석의 “최신 게임은 값을 안 쓴다”는 표현은 조건을 생략한 설명이다. true 기본값인 설치 DLL에서 field≠material이라는 일반 결론을 내릴 수 없다. 혼합 shader에 해당 속성이 없거나 재질이 나중에 바뀐 경우도 구분해야 한다.
3. **샘플러 차이는 아직 코드에 남는다.** `tools/gpudeco-lab/GpuDecoBatch.shader:156`은 frac(UV)를 한 뒤 point/linear **clamp**로 읽는다. Bilinear Repeat는 가장자리에서 반대쪽 texel과 보간하지만 frac+Clamp는 경계 texel에 붙으므로 이음매에서 같지 않다. 배열 패딩의 address scale/offset까지 겹치면 차이가 커질 수 있다. Point·음수 UV·0 반복·비정수 반복·wrapU/V 분리·경계 도형·색공간도 대조해야 한다. 포맷/크기별 native repeat sampler 또는 정확한 wrapped bilinear texel fetch/주변 패딩 설계를 사용한다.

공유 텍스처 wrap은 `G/scrDecorationManager.cs:408`에서 같은 이미지 그룹을 순회하며 U/V Repeat 여부를 누적해서 같은 Texture에 기록한다. 마지막 상태는 전체 그룹의 영향을 받는다. export의 WrapU/V 스냅샷만 고정하면 후속 SetTile/자산 교체/공유 tex 변경에 낡을 수 있다. 이는 L 재질·자산 버전의 관측 대상이다.

**원인 확정은 미완료**다. 이 분석은 사슬의 실제 renderer별 shader/drawMode/material/UV/capture를 새로 수집하지 않았다. EffectiveRepeat 보완이 이미 있는 상태에서도 false 기본값과 사슬 차이 주석이 남아 있다. “반복 필드만 고치면 해결”, “랩만 고치면 해결”을 모두 확정하지 않는다. 124초 혼합 동점의 밝기 차이도 별도 원인 후보다(:115의 주석).

### 6.2 혼합/동점의 완전 지원

게임 공개 DecorationBlendMode는 None, Screen, LinearDodge, Overlay, SoftLight, Difference, Multiply다(`G/DecorationBlendMode.cs:1`). GpuDecoBlend의 테이블은 그 비None 여섯 종류를 모두 포함하고 다른 BlendModes 라이브러리 이름도 추가한다(`GpuDecoBlend.cs:13`). **enum 값 지원과 실제 효과 상태 지원은 다르다.** `Takeable`은 MaskMode.Disabled + RenderMode.SelfWithScreen + Grab/Additive shader만 허용하고, stencil mask·TextureWithSelf·overlay MPB는 제외한다(:46). 따라서 “혼합 이름은 다 있으니 완전 지원”은 틀리다.

LinearDodge의 `(s*a,0)` premultiplied 출력은 FastBlend Additive에 맞춘 최적화다(:33). Screen 등 배경 의존 모드는 각 원래 draw 지점의 배경이 필요하다. `DrawCollapsePolicy.Plan`은 grab 항목 전후를 끊는다(:180). 화면 복사를 덜 하는 묶음은 **같은 배경을 읽어도 결과가 같다는 증명**이 있을 때만 가능하다. 비가환 합성을 임의 texture/material 순서로 재정렬하지 않는다.

동점은 layer/order/queue/distance 뒤 shader ID·material ID 규칙이 실측에 있고, multi-pass/Grab의 pass 계열 순서가 추가된다(`SceneRenderOrder.cs:9`, :33; `docs/render-system/06-engine-internals.md:7`). 엔진 comparator에는 그 뒤 node/패스 관련 키도 있다. renderer ID는 현재 코드의 진단 tie-break일 뿐 엔진의 같은 material 최종 tie 증거가 아니다(`SceneRenderOrder.cs:5`). **순서가 모호한 bucket은 N으로 실행하는 것도 정식 지원**이지만 그 최적화 이득은 없다고 보고해야 한다. 이를 넘어서 모든 draw를 직접 실행하려면 native draw-list의 정확한 순서를 확보하는 더 큰 설계가 필요하다.

### 6.3 직접 텍스처 48과 페이지 설계

현재는 array 4/direct 48이며 큰 같은 크기 DXT5 이미지는 ExactBigArray로 일부 승격한다(`DrawCollapsePolicy.cs:16`, :147; `GpuDecoDirectTexturePrepare.cs:59`). 이미지 timeline의 곡 전체 selector도 사전 제한에 들어간다. 따라서 지금 보이는 텍스처만 세어 48 이하라고 판정하면 이후 교체에 실패한다(:34).

거부를 없애려면 순서를 유지한 draw node 열에 texture page ID를 붙여 **연속한 구간에서 바인딩이 찰 때만 끊는다**. 같은 format/size/mip 배열 page, 필요한 원본 texture 단일 binding draw까지 수용한다. 모든 임의 크기를 같은 대형 배열로 강제하면 VRAM 낭비·포맷 변환·sample edge 차이를 만든다. 준비 때 사용량 산출/예산을 하고, 물리 VRAM 부족은 native N으로 계속 렌더한다. 슬롯 수의 제한은 **그리기 횟수 제한 문제**로 바뀌며, 이 변화가 FPS 이득이라는 보장은 없다. 직접 분기 셰이더를 줄일 가능성과 페이지별 추가 draw를 함께 재야 한다.

### 6.4 RunLod와 내부 renderlod

RunLod 분담의 실제 식은 `RunLodSplit && Main.Config.RunLod && baselineObjects.Length < 12000`이다(`GpuDecoExport.cs:932`). `SplitAllTargets=150000` 상수는 남아 있지만 **현재 식에서 쓰지 않는다**. 바로 옆 주석은 DD/Exit의 대상 수도 커서 기준에서 뺐다고 설명한다. 상수 선언만 읽으면 잘못된 결론을 낼 수 있다. CandidateRenderer 자체는 SpriteRenderer/drawMode·마스크·shader·블렌드 조건만 사용하고 실제 RunLod 수집 단계가 추가 조건을 적용한다(`RunLod.cs:42`). 현재 작업 트리의 RunLod에는 기존 미커밋 Tiled 확장이 있으며 `runlod tiled on`은 **기본 off 연구 기능**이다. 이번 조사에서 수정하지 않았다.

내부 LOD의 RT 전환 가설은 **메커니즘은 확인, 인과의 크기는 미확정**이다. `GpuDecoLod.cs:132`는 구간마다 RT를 만들고 `SetRenderTarget`, viewport, clear draw, 본 draw, composite property block을 처리한다(:171~187). RunLod도 구간 RT를 쓴다(`RunLod.cs:495`), 그러므로 “RT를 쓴다는 사실만”으로 175 대 213 FPS의 차이를 전부 설명할 수 없다. 내부 구간이 shader/order/장벽별로 더 쪼개지는 수, clear/composite 화면 면적, CPU 명령 작성, RT working set, LOD 배율 readback 지연·후보 차이를 분리해야 한다. 기존 통합 설계 문서의 141 sort 종류·42 장벽·전체 화면 합치기 실패 분석도 참고한다(`docs/render-system/adofai-renderer.md`, 11-08 결과).

현재 코드에는 **이미 화면 bounds 합성/부분 clear 개선이 있다**. `tools/gpudeco-lab/GpuDecoLod.compute:36`은 구간 화면 범위를 모으고 4픽셀 여유를 더하며, `tools/gpudeco-lab/GpuDecoBatch.shader:124`와 `tools/gpudeco-lab/GpuDecoLodClear.shader:17`이 그 범위만 합성/clear한다. RT 자체는 여전히 구간별 전체 화면 축소 크기다(`GpuDecoLod.cs:149`). 따라서 예전의 전체 화면 합치기 문제만 새로 고치면 된다는 제안은 중복이다. 이 코드가 인계의 175 대 213 측정 DLL에 실제 포함됐는지는 MVID/번들 대응을 확인해야 한다.

추가 LOD 통합 제안은 같은 최종 draw 순서에서 **연속하고 합성 가능한 항목**을 더 긴 구간으로 만드는 일, CPU 계획/RT 전환 횟수와 RT working set을 줄이는 일이다. 이미 있는 bounds 최적화를 기준선으로 둔다. mask/Grab/pass/카메라 경계를 넘어 합치지 않는다. dirty replan을 제한하고 RT pool/atlas는 수명·clear 범위를 검증한다. bilinear 축소·확대는 원래 픽셀과 같지 않다. 손실 허용 LOD 정책과 무손실 기능 지원 합격을 구분해야 한다. RunLod보다 싸지면 분담을 없애며, 그 전에는 RunLod를 지원하는 통합 노드로 남긴다.

## 7. 무엇이 줄고, 어디까지 이득을 말할 수 있나

| 변경 | Update/Late script | 렌더 준비 CPU / draw | 덧칠·GPU | 현재 말할 수 있는 것 |
|---|---|---|---|---|
| 깊이 T 확대 | 원래 Tween/Transform 쓰기 생략 가능 | 소유 renderer 줄지만 예약 span/키 갱신 증가 | 같은 픽셀 수 | DD 단독 거부 3,420은 기회 규모. 새 FPS는 미측정 |
| L 지원 | **원래 계산 유지**. 소비자가 있으면 Transform도 유지 | 연속 batch면 renderer/draw 준비 절감 가능, getter/upload 비용 추가 | 일반적으로 같은 픽셀 수 | 지원 범위를 넓히는 효과만 확실. CPU 순이득 미확정 |
| Repeat/Tiled 지원 | T이면 script 절감, L이면 아님 | 큰 배경 장벽 제거로 구간 결합 가능, Tiled geometry 비용 발생 | 같은 면적이면 덧칠 그대로 | Arche 잔여 큰 배경 비용이 커 우선 측정 가치. 비용 2.79ms 전부 절감 주장 불가 |
| 마스크 D | 상태가 T면 일부 절감 | native sprite render 준비/마스크 node 비용 변화 가능 | provider/소비자 stencil/coverage 비용 여전히 필요 | 겹침·경계 동일성부터 검증. FPS 이득 미확정 |
| texture page | script에 영향 없음 | binding/property/분기 부담 감소 가능, 추가 page draw 증가 | 원래 sampling 비용에 접근 가능 | 이전 DIRECT_FEW+property 제거 개선은 해당 HELLO 구간의 기존 실측이며 새 page 설계 보증 아님 |
| N text/particle/object/prefab | **유지** | **유지** + 경계 계획 | **유지** | 기능 누락 방지. 이것만으로 최적화됐다고 보고하지 않음 |
| Grab 모드 | T이면 일부 script 절감 | 상태/복사 제출 통합 가능 | 원래 배경 copy·blend 유지 | 복사 시점 보존 필요. 보이지 않는 proxy의 불필요 Grab 방지가 별도 기회 |
| LOD | script 거의 유지 | 구간·RT 제출 증가 가능 | 확대된 반복 픽셀을 줄일 가능성 | 기존 RunLod가 유리한 구간이 있음. renderlod 우위/무손실은 입증 안 됨 |
| 최소64/전역 gate 제거 | 기본 관리비 증가 가능 | 작은 batch가 손해일 수 있음 | 거의 그대로 | 지원 정책 개선. 성능 주장과 분리 |

기존 결과는 버전·해상도·설정·창이 다르다. 11-08 문서의 HELLO 175FPS, 인계의 내부 LOD 175 대 RunLod 213, 11-10 소유 로그를 한 ABBA 표로 합치지 않는다. Update/렌더 스레드/GPU queue wait를 서로 같은 시간으로 더하지 않는다. 소유 수를 많이 늘려도 작은 수의 큰 배경 픽셀이 병목이면 FPS가 안 늘 수 있다. 모든 장식 100% GPU 소유와 최적화 이득 둘 다를 현 자료로 확정할 수 없다.

## 8. 후속 실험: 실행하지 않은 구체 명령과 판정

아래는 측정 중인 Claude가 독점 실행 잠금을 확보하고 **현재 실행 종료 후** 수행할 목록이다. 이 문서를 작성하는 동안은 어떤 명령도 게임에 보냈지 않았다. 기존 명령 이름은 `AutoTest.cs:959`, `GpuDecoDrawCollapseGate.cs:40`, `OwnProbe.cs:162`, `FrameCompare.cs`, `GpuDecoLod.cs`, `RunLod.cs`의 파서 기준이다. 새 계측은 **추가 필요**로 표시한다. 기본 설정 `gBsSp`의 실제 대문자 의미는 실행 스크립트를 확인한 뒤 동일하게 기록하며 임의 해석하지 않는다.

공통 절차: 6맵 동일 맵 파일/hash·DLL MVID·설정·해상도·효과·자동입력 조건을 고정하고 판마다 새 프로세스 ABBA. 실제 무거운 구간 mean/p50/p95/p99 frame time·FPS·>30ms·메인/렌더 CPU·GPU active를 기록한다. 주기별 CPU/GPU clock과 대기도 기록한다. 진단 계측은 별도 판에서 하고 성능 창에서는 비용 probe/readback/framecmp를 끈다. 작은 맵/장식 0·1개도 추가해 최소64 제거의 상시비용을 잰다.

| 실험 | 기존 명령/설정 | 새로 기록할 값 / 질문 |
|---|---|---|
| 기준 admission | `gpudeco state`, `gpudeco live state`, 기존 gBsSp 로그 | 전체 actor = T+L+N+empty, original/independent/collapse/RunLod 각 소유 ID; 사유별 **교집합 ID** dump 추가 필요 |
| DD 깊이 | `depthinst on` / `depthinst off`, 필요 시 `depthinst 8` | 지원 증가→최종 renderer 소유 증가 구분, 예약 자리/span 증가·depth 전환·중간 시작·미준비 depth. 3,420 기대 상한과 실제 차이 이유 |
| 반복 분해 | `repeat on` / `repeat off`, 별도 `runlod tiled on/off` | 7777 사슬·124초 blend tie와 Arche 큰 배경에서 renderer별 drawMode/size/border/material repeat/actual wrap/UV dump 추가. Point/Bilinear·U/V·음수/비정수 경계 fixture |
| 텍스처 페이지 | 기존 `fewdirect on` / `fewdirect off`, `globaldirect on` / `globaldirect off`는 원인 분리용; 새 page는 구현 후 새 설정명 정의 | 48 초과 이미지 교체 누락 0, page/format/slices/VRAM/추가 draw, shader CPU/GPU 비용. 기존 토글은 새 페이지 기능이 아님 |
| RunLod 분담/내부 LOD | `gpudeco drawcollapse policy runlodsplit on/off`; `runlod on/off`; `renderlod on/off`, `renderlod 1.5` | 최종 후보를 맞춘 비교, LOD구간 수/RT전환/clear·composite 면적·draw/async readback·CPU기록시간. RT 횟수·픽셀 계측 추가 필요 |
| L 업로드 | `ownprobe up0` → 일정 창 → `ownprobe up`; `gpudeco costprobe on/off`(진단 별도) | 기존 카운터 이외 L snapshot CPU/getter, dirty actor/범위, slot/map/geometry/texture/native-copy bytes/calls 추가 필요. L only와 T only를 같은 대상에서 비교 |
| 같은 프레임 원래↔후보↔복원 | pause 후 `framecmp clear`, `framecmp ui exclude`, `framecmp filters on`, `framecmp mask none`, `framecmp grab O`; 후보 on→`framecmp grab B`; off→`framecmp grab R`; `framecmp diff O B 0 png`, `framecmp diff O R 0 png` | 정확한 원래 기준은 **GPU live까지 off한 O**. collapse off만으로 원래 SpriteRenderer 기준이라고 부르지 않음. noise 재촬영 포함, maxRGBA·변경비율·차이 crop 저장 |
| mask/visibility/조건/충돌·수명 | 구현 후 fixture(추가 필요); 기존 `gpudeco live on/off`·되감기·중간시작·재시작·맵교체·unload | visible false→mask type→inside/outside, front/back 역전·범위 경계·중첩·같은 material tie·provider on/off, hitOnce/perTouch/repeat·수동/타격·외부 Late writer 순서, 원래 이벤트 콜백 횟수·판정 및 collider/layer/seed 보존 |

주의: 위 on/off는 표기상 선택이며 실제 한 명령 문자열로 `on/off`를 보내는 것이 아니다. 정책은 **준비 전에** 바꾸고 map 준비를 다시 한다. framecmp 캡처를 위해 시간을 고정해도 트윈 콜백·마스크·수명 전환은 별도로 실제 재생 검증한다. 기존 pixel gate는 dense primary에서 max≤3/255 및 changed≤0.3%다(`GpuDecoData.cs:29`). 이는 “완전 동일”이 아니며 무손실 주장에는 noise를 통제한 RGBA diff=0이 필요하다. LOD는 이 기준과 별도의 허용 정책이 필요하고 기존 max 22/30~65 값을 무손실 합격으로 쓰지 않는다.

## 9. 구현 순서와 단계별 합격 기준

이 순서는 하나의 통합 설계를 구현·검증하는 내부 순서다. 구버전 요구를 임의로 여러 미완성 제품으로 출시하자는 뜻이 아니다. 모든 단계의 FPS 기준은 **목표**이며 지금 달성한 결과가 아니다.

| 순서 | 구현 단위와 우선 맵 | 합격 기준 |
|---|---|---|
| 1 | 전체 actor 레지스트리 + T/L/N 공급·그리기 구분, 통합 수명·카메라별 계획. 최소64/전역 foreign gate는 N 지원과 함께 분리 | 6맵의 28,835/6,314/2,531/6,290/3,524/498 전부가 한 표현에 정확히 등록(동적 생성은 세대별 갱신), 사라진 장식/중복 소유 0. GPU 소유 증가를 이 단계 합격으로 요구하지 않음. baseline N만 켠 오버헤드는 ABBA에서 유의한 회귀 없을 것 |
| 2 | L 최종 상태와 대상별 T↔L 전환 + depth D. DD 깊이 단독 3,420, WF 169, AR 22 우선 | 조건/충돌/외부 writer/중간시작 포함 모든 actor 기능 지원. DD depth-only 대상마다 T/L/N 목적지가 남고 미준비 깊이로 지도 전체 꺼짐 0. GPU 소유 목표는 해당 ID 집합의 다른 제약을 해소한 만큼(최대3,420은 후보 상한). 같은 프레임 및 실제 depth 이벤트 순서 그림·복원 합격; 원래 대비 FPS 회귀 없고 순이득 측정 |
| 3 | Repeat·Tiled/Sliced 도형 + 정확 sampler·자산 페이지. AR repeat110/직접33, 다른 맵 repeat100~241 대상 | repeat-tile/direct-texture-overflow **지원 누락 0**, 페이지/독립 D/N까지 모두 계수. 사슬과 큰 배경의 pixel·wrap·image 교체 합격, 원래 대비 FPS 측정. AR 기존 약28,600 GPU 소유를 떨어뜨리지 않고 대상별 증가량 확인 |
| 4 | mask provider/consumer D + 숨김 재활성화, 복합 N/Canvas 순서 보강. WF 초기997/대상658, HL384/231 우선 | mask 원인 actor 누락0, 중첩/범위/알파·타입 전환·Canvas 경계 합격, 기본 N과 그림·판정 동일. 비이미지687(WF)/105(HL)는 GPU image 소유가 아닌 N 정식 지원 수로 공개. 6맵 원래 대비 FPS와 renderer preparation CPU 확인 |
| 5 | 혼합/pass/tie 순서 및 동적 bucket 재계획, 임의 shader/MPB의 N 완성. 7777 124초 등 | 잘못된 순서·배경 copy 시점 0, 불명 final tie의 N이 전체 bucket을 보존. 모든 공개 blend enum+효과 mask/overlay 조합에 D/N 계약 존재. max3 넘는 차이는 통합 무손실 채택 불가; 원래 대비 성능 회귀 없고 draw/복사 비용 실측 |
| 6 | 단일 LOD 실행기로 RunLod 통합, 관측 비용/구간/RT 최소화 | RunLod 분담 누락·이중 소유0, HELLO/WF/7777에서 기존 RunLod보다 빠른 ABBA 결과 및 화질 정책 합격. 늦은 장벽 가시성/카메라 급변에도 순서 보존. 이 기준 전에는 RunLod N 노드 유지 |
| 7 | 최종 6맵 전곡 + 보통 맵·장식0/1·공식 prefab·모드 호환·복구/자원 검증 | 모든 종류/필드/이벤트가 T/L/D/N의 명시 경로, 제약 때문에 통째 map 비활성 0(장치 실패시 정식 N), 손실 없는 모드 pixel 기준과 판정/이벤트/순서/복원 합격. FPS는 원래 방식 대비 맵별 mean/p95/p99를 공개하고 특정 맵 회귀를 평균으로 가리지 않음. “확실한 최적화”는 실제 유의한 이득이 확인된 조건에만 사용 |

사용자의 요구를 충족했다는 판정은 **현재는 미달**이다. 이 문서는 누락을 없애기 위한 경로와 실험을 구체화한다. 통합 레지스트리에 넣는 것, GPU가 맡는 것, 렌더 CPU를 줄이는 것, GPU 픽셀 비용을 줄이는 것을 각각 수치로 보고해야 한다.


## 부록 A. 위치별 거부·검증·복귀 인벤토리

본문 3절은 의미와 게임 대응을 설명하고, 이 부록은 같은 문자열이 다른 경로에 있어도 빠지지 않도록 발생 위치를 보존한다. 진단 명령의 인수 오류·시험 합격 조건도 포함한 보수적 인벤토리이며 모든 줄이 제품의 장식 거부라는 뜻은 아니다. 불변식/복원 검증은 제거하지 않는다. 코드의 런타임 접미 문자열은 실행 전 숫자로 채우지 않는다.

### A.1 표 사유와 장면/Live 문자열 반환

| 위치 | 원문 조건/사유 | 대응 |
|---|---|---|
| `DecoBake.Core.cs:62` | `foreach (string reason in d.Reasons) c.Reasons.Add(reason);` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.Core.cs:315` | `if ((placement == "Camera" &#124;&#124; placement == "CameraAspect") && !ScreenFixedSupported) return "Camera-동적 화면 투영";` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.Core.cs:316` | `if (placement == "Tile" && stickToFloor) return "Tile-동적 바닥 추종";` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.Core.cs:317` | `return placement == "Global" &#124;&#124; placement == "Tile" &#124;&#124; (ScreenFixedSupported && (placement == "Camera" &#124;&#124; placement == "CameraAspect")) ? null : "초기 배치 미지원";` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.Core.cs:322` | `if (placement == "Tile") return "Tile-동적 효과 시점 재배치";` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.Core.cs:323` | `if (placement == "Camera" &#124;&#124; placement == "CameraAspect") return "Camera-동적 배치 변경";` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.Core.cs:324` | `return "Global 이외 배치 변경";` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.Core.cs:343` | `foreach (string reason in e.Reasons) decorations[d].Reasons.Add(reason);` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.Core.cs:344` | `if (!Finite(e.Duration) &#124;&#124; e.Duration < 0) decorations[d].Reasons.Add("길이 비유한/음수");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.Core.cs:376` | `{ dec.Reasons.Add("크기 산술 비유한"); break; }` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.cs:241` | `else row.Reasons.Add("Global 기준 원본 위치 없음");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.cs:251` | `if (!(d is scrVisualDecoration)) row.Reasons.Add("이미지 이외 장식");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.cs:252` | `if (d.useHitbox &#124;&#124; d.hitboxEvents.Count != 0) row.Reasons.Add("히트박스/충돌");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.cs:253` | `if (d.sourceLevelEvent != null && d.sourceLevelEvent.TryGet<MaskingType>("maskingType", out var mask) && mask != MaskingType.None) row.Reasons.Add("초기 마스크");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.cs:254` | `if (d.parallax == null) row.Reasons.Add("시차 부품 없음");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.cs:256` | `if (placementReason != null) row.Reasons.Add(placementReason);` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.cs:257` | `if (d.stickToFloor) row.Reasons.Add("바닥 부착");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.cs:258` | `if (d.followPlanet != null) row.Reasons.Add("행성 추종");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.cs:259` | `if (row.Initial.Any(v => !DecoBakeCore.Finite(v))) row.Reasons.Add("초기값 비유한");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.cs:261` | `&#124;&#124; !DecoBakeCore.Finite(row.GlobalX) &#124;&#124; !DecoBakeCore.Finite(row.GlobalY)) row.Reasons.Add("초기값 비유한");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.cs:325` | `if (Conditional(f)) r.Add("조건/수동/타격");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.cs:326` | `if (f.disableIfMinFx &#124;&#124; f.disableIfMaxFx) r.Add("시각 효과 설정 조건");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.cs:331` | `r.Add("이미지 변경");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.cs:333` | `if (movementReason != null) r.Add(movementReason);` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.cs:334` | `if (!Enum.IsDefined(typeof(Ease), f.ease) &#124;&#124; f.ease == Ease.INTERNAL_Custom &#124;&#124; f.ease == Ease.Unset) r.Add("미확인/사용자 이징");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.cs:335` | `if (!DecoBakeCore.Finite(f.duration) &#124;&#124; f.duration < 0) r.Add("길이 비유한/음수");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.cs:359` | `if (e.Values.Any(x => float.IsInfinity(x))) e.Reasons = e.Reasons.Concat(new[] { "목표값 비유한" }).ToArray();` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.cs:412` | `foreach (var reason in source.Reasons) row.Reasons.Add(reason);` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.cs:413` | `if (foreignWriter) row.Reasons.Add("외부 모드 장식 setter 패치");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.cs:414` | `if (d.parallax != null && d.parallax.clampToScreen && !DrawCollapsePolicy.ScreenFixed) row.Reasons.Add("시차 화면 고정");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.cs:415` | `if (d.hitboxEvents.Count != 0 &#124;&#124; d.hasConditionalChange) row.Reasons.Add("히트박스/조건 연결");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.cs:416` | `if (d is scrVisualDecoration v && v.spriteRenderer != null && v.spriteRenderer.maskInteraction != SpriteMaskInteraction.None) row.Reasons.Add("마스크 대상");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.cs:451` | `foreach (var d in decs) { d.Reasons.Add("다른 장식 효과/태그 변경 가능"); if (slice.Elapsed.TotalMilliseconds >= 4) { yield return null; slice.Restart(); } }` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.cs:464` | `decs[id].Reasons.Add("예약 목록 밖/조건/수동/타격");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.cs:469` | `foreach (var d in decs) { d.Reasons.Add("다른 장식 효과/태그 변경 가능"); if (slice.Elapsed.TotalMilliseconds >= 4) { yield return null; slice.Restart(); } }` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.G1.cs:37` | `if(!g1Initial.TryGetValue(d,out var s)){row.Reasons.Add("초기 부가 상태 없음");return;}` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.G1.cs:46` | `if(set.Count>DrawCollapsePolicy.MaxDepthInstances)row.Reasons.Add("깊이 변경 많음");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.G1.cs:48` | `else row.Reasons.Add("깊이 변경");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.G1.cs:50` | `if(f.cropPivotOffsetUsed && !(f.targetCropPivotOffset/100f).Equals(s.Crop))row.Reasons.Add("크롭 변경");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.G1.cs:51` | `if(f.smoothingUsed && f.targetSmoothing!=s.Smooth)row.Reasons.Add("부드럽게 변경");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.G1.cs:58` | `row.Reasons.Add("원래크기 변경/이미지 의존");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.G1.cs:60` | `if(f.maskingTypeUsed && (f.targetMaskingType!=s.Mask &#124;&#124; !alwaysVisible))row.Reasons.Add("마스크 종류/숨김 재활성화");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.G1.cs:61` | `if(f.maskingTargetUsed && f.targetmaskingTarget!=s.Target)row.Reasons.Add("마스크 대상 변경");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.G1.cs:62` | `if(f.useMaskingDepthUsed && f.targetUseMaskingDepth!=s.CustomRange)row.Reasons.Add("마스크 범위 사용 변경");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `DecoBake.G1.cs:71` | `row.Reasons.Add("마스크 깊이 변경");` | 3.1: T 대신 L/D/N; C1·CL·CN |
| `GpuDecoScene.cs:100` | `yield return "_Texture" + i;` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:102` | `foreach (string suffix in new[] { "Scale", "Pos", "Rot" }) yield return "_Texture" + i + suffix;` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:103` | `yield return "_DisableTexture" + i;` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:158` | `if (firstCustom != null) return "property-block-custom:" + firstCustom;` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:177` | `if ((camera.cullingMask & (1 << r.gameObject.layer)) == 0) return "camera-layer";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:178` | `if (d.GetType() != typeof(scrVisualDecoration)) return "subclass";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:179` | `if (d.isMask() &#124;&#124; r.maskInteraction != SpriteMaskInteraction.None) return "mask";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:180` | `if (d.hitbox != 0) return "hitbox";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:183` | `if ((d.meshRenderer && d.meshRenderer.enabled && d.meshRenderer.gameObject.activeInHierarchy && !d.meshRenderer.forceRenderingOff)) return "mesh-live:blend=" + Blend(d) + "(" + (int)Blend(d) + ")";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:184` | `if (r.GetComponentInParent<SortingGroup>()) return "sorting-group";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:185` | `if (!r.sprite &#124;&#124; !r.sprite.texture) return "missing-sprite-texture";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:186` | `if (r.sprite.associatedAlphaSplitTexture) return "split-alpha";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:187` | `if (r.drawMode != SpriteDrawMode.Simple) return "sliced-or-tiled";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:188` | `if ((d.repeatX != 1 &#124;&#124; d.repeatY != 1) && !DrawCollapsePolicy.Repeat) return "repeat";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:190` | `if (d.blendModeEffect && d.blendModeEffect.enabled && !blendOk) return "blend-effect";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:191` | `if (materials.Length != 1 &#124;&#124; !materials[0] &#124;&#124; !materials[0].shader) return "material-slots";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:196` | `if (m.renderQueue != 3000 &#124;&#124; m.passCount != 1) return "shader-pass-queue";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:197` | `if (m.shaderKeywords.Length != 0) return "keywords";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:203` | `if (m.renderQueue != 3000 &#124;&#124; m.passCount != 2) return "shader-pass-queue";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:204` | `if (!m.shaderKeywords.All(k => k.StartsWith("BLENDMODES_MODE_") &#124;&#124; k == "FORCE_POINT_SAMPLING")) return "keywords";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:208` | `if (!m.GetShaderPassEnabled(m.GetPassName(0))) return "disabled-pass";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:209` | `if (!m.shaderKeywords.All(k => k == "USE_POINT_SAMPLING")) return "keywords";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:215` | `if (!d &#124;&#124; !r &#124;&#124; d.GetType() != typeof(scrVisualDecoration)) return "subclass";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:216` | `if (d.hitbox != 0 &#124;&#124; d.isMask() &#124;&#124; r.maskInteraction != SpriteMaskInteraction.None) return "hitbox-mask";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:217` | `if (d.followPlanet &#124;&#124; d.stickToFloor &#124;&#124; d.syncFloorDepth) return "parent-follow";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:218` | `if (((d.repeatX != 1 &#124;&#124; d.repeatY != 1) && !DrawCollapsePolicy.Repeat) &#124;&#124; r.drawMode != SpriteDrawMode.Simple) return "repeat-tile";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:219` | `if (MeshOn(d) &#124;&#124; (d.meshRenderer && d.meshRenderer.enabled && d.meshRenderer.gameObject.activeInHierarchy)) return "mesh-mode";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:220` | `if (((int)Blend(d) != 0 &#124;&#124; (d.blendModeEffect && d.blendModeEffect.enabled)) && !GpuDecoBlend.Takeable(d, r)) return "blend";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:221` | `if (DrawCollapsePolicy.SplitNow && RunLod.CandidateRenderer(r)) return "runlod-split";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:223` | `&#124;&#124; !ReferenceEquals(Cached(p), d.pivotTrans) &#124;&#124; p.enabled) return "parallax-binding";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:224` | `if (!d.pivotTrans &#124;&#124; d.pivotTrans.parent != expectedManager.transform) return "pivot-parent";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:225` | `if (!d.childTransform) return "child-missing";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:227` | `if (cr.x != 0 &#124;&#124; cr.y != 0) return "nonplanar-child";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:228` | `if (!ADOBase.controller &#124;&#124; p.cameraTransform != ADOBase.controller.camy.transform) return "camera-binding";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:229` | `if (d.cfpCache != null && d.cfpCache.Length != 0) return "component-filter";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:232` | `&& !dependent.forceRenderingOff) return "live-child-renderer";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:234` | `&#124;&#124; (d.hitboxBordersObject && d.hitboxBordersObject.activeInHierarchy)) return "editor-borders";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:235` | `if (!r.sprite &#124;&#124; !r.sprite.texture &#124;&#124; r.sprite.associatedAlphaSplitTexture) return "sprite-texture";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoScene.cs:244` | `&#124;&#124; (r.forceRenderingOff && !InvisibleSkip.IsHidden(d))) return "inactive";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:342` | `if (i != null && i.Owned) Release(i, "write-" + kind);` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:465` | `if (!i.R &#124;&#124; !i.D) return "destroyed";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:466` | `if (!i.R.enabled) return "renderer-disabled";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:467` | `if (!i.R.gameObject.activeInHierarchy) return "inactive-hierarchy";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:468` | `if (!i.D.GetVisible()) return "logical-invisible";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:469` | `if ((camera.cullingMask & (1 << i.R.gameObject.layer)) == 0) return "camera-layer";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:470` | `return "scene-or-foreign-force-off";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:475` | `if (!d &#124;&#124; !r &#124;&#124; d.GetType() != typeof(scrVisualDecoration)) return "subclass";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:476` | `if (d.hitbox != 0 &#124;&#124; d.isMask() &#124;&#124; r.maskInteraction != SpriteMaskInteraction.None) return "hitbox-mask";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:477` | `if (d.followPlanet &#124;&#124; d.stickToFloor &#124;&#124; d.syncFloorDepth) return "parent-follow";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:478` | `if (((d.repeatX != 1 &#124;&#124; d.repeatY != 1) && !DrawCollapsePolicy.Repeat) &#124;&#124; r.drawMode != SpriteDrawMode.Simple) return "repeat-tile";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:479` | `if (MeshOn(d) &#124;&#124; (d.meshRenderer && d.meshRenderer.enabled && d.meshRenderer.gameObject.activeInHierarchy)) return "mesh-mode";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:480` | `if (((int)Blend(d) != 0 &#124;&#124; (d.blendModeEffect && d.blendModeEffect.enabled)) && !GpuDecoBlend.Takeable(d, r)) return "blend";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:481` | `if (DrawCollapsePolicy.SplitNow && RunLod.CandidateRenderer(r)) return "runlod-split";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:483` | `&#124;&#124; !ReferenceEquals(Cached(p), d.pivotTrans) &#124;&#124; p.enabled) return "parallax-binding";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:484` | `if (!d.pivotTrans &#124;&#124; d.pivotTrans.parent != (expectedManager ? expectedManager : manager).transform) return "pivot-parent";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:485` | `if (!d.childTransform) return "child-missing";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:487` | `&#124;&#124; r.flipX != i.FlipX &#124;&#124; r.flipY != i.FlipY)) return "child-state-changed";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:489` | `if (cr.x != 0 &#124;&#124; cr.y != 0) return "nonplanar-child";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:490` | `if (!ADOBase.controller &#124;&#124; p.cameraTransform != ADOBase.controller.camy.transform) return "camera-binding";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:491` | `if (d.cfpCache != null && d.cfpCache.Length != 0) return "component-filter";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:494` | `&& !dependent.forceRenderingOff) return "live-child-renderer";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:496` | `&#124;&#124; (d.hitboxBordersObject && d.hitboxBordersObject.activeInHierarchy)) return "editor-borders";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:497` | `if (!r.sprite &#124;&#124; !r.sprite.texture &#124;&#124; r.sprite.associatedAlphaSplitTexture) return "sprite-texture";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:507` | `&#124;&#124; (r.forceRenderingOff && !InvisibleSkip.IsHidden(d))) return "inactive";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:533` | `if (!r &#124;&#124; !d.pivotTrans &#124;&#124; !r.enabled &#124;&#124; !r.gameObject.activeInHierarchy &#124;&#124; r.forceRenderingOff) return "inactive";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:541` | `static void Reject(Image i, string why)` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:557` | `if (reason != null) { Reject(i, reason); return; }` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:560` | `if (reason != null) { Reject(i, reason); return; }` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:564` | `if (reason != null) { Reject(i, reason); return; }` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:571` | `if (reason != null) { Reject(i, reason); return; }` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:575` | `if (reason != null) { Reject(i, reason); return; }` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:578` | `if (i.Corners.Length != 4 &#124;&#124; i.UV.Length != 4 &#124;&#124; i.Indices.Length != 6) { Reject(i, "nonquad"); return; }` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:582` | `if (i.Geometry == null) { Reject(i, "child-chain-unsupported"); return; }` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:610` | `if (!LiveDecoPolicy.WithinPixelError(vertexError, pixelsPerWorld)) { checkRejected++; Reject(i, "vertex-error-px"); return; }` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:611` | `if (!LiveDecoPolicy.WithinPixelError(boundsError, pixelsPerWorld)) { checkRejected++; Reject(i, "bounds-error-px"); return; }` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:673` | `static void Release(Image i, string why = "boundary")` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:702` | `if (SceneReset.Resetting) { Release(i, "scene-reset"); return false; }` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:727` | `if (Rendered(d).a <= 0) Release(images[d], "alpha-zero"); // Return hidden bookkeeping to InvisibleSkip/Dormancy.` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:759` | `Release(i, "boundary:" + __originalMethod.Name);` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:838` | `Release(image, "static-invalidated");` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:842` | `if (!live) { if (image.Owned) Release(image, NotLiveReason(image)); }` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:897` | `Release(tied.Image, "material-tie"); Reject(tied.Image, "material-tie"); tied.Owned = false;` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoRenderer.cs:1132` | `try { Release(i, "stop"); } catch (Exception e)` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoScripts.cs:27` | `if (toggle.transform != renderer.transform) return "visibility-node";` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoScripts.cs:29` | `return "visibility-target:" + (toggle.scriptToToggle ? toggle.scriptToToggle.GetType().FullName : "missing");` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `LiveDecoScripts.cs:32` | `return "foreign-child-script:" + component.GetType().FullName;` | 3.2: L/D/N 또는 수명; CL·CD·CN |
| `GpuDecoPerItemPrepare.cs:17` | `&& sprite.maskInteraction==SpriteMaskInteraction.None))return "material/mask";` | 3.3: D/N; C1·CD |
| `GpuDecoPerItemPrepare.cs:18` | `if(!row.M.shaderKeywords.All(k=>k=="USE_POINT_SAMPLING" &#124;&#124; (blendMat && (k.StartsWith("BLENDMODES_MODE_") &#124;&#124; k=="FORCE_POINT_SAMPLING"))))return "keywords";` | 3.3: D/N; C1·CD |
| `GpuDecoPerItemPrepare.cs:20` | `&& (!row.M.HasProperty("_Opacity") &#124;&#124; row.M.GetFloat("_Opacity")==1)))return "tint/opacity";` | 3.3: D/N; C1·CD |
| `GpuDecoPerItemPrepare.cs:22` | `if(!t &#124;&#124; t.GetInstanceID()!=item.TextureId)return "texture-changed";` | 3.3: D/N; C1·CD |
| `GpuDecoPerItemPrepare.cs:23` | `if(t.mipmapCount!=1)return "mipmap";` | 3.3: D/N; C1·CD |
| `GpuDecoPerItemPrepare.cs:24` | `if((t.wrapModeU!=TextureWrapMode.Clamp &#124;&#124; t.wrapModeV!=TextureWrapMode.Clamp) && !GpuImageCollapseUvInUnitRange(item.UV))return "wrap";` | 3.3: D/N; C1·CD |

A.1 발생 줄 수: 137. 동적으로 전파되는 reason 변수는 본문 3.1의 원천 사유를 공유한다.

### A.2 전역/준비/활성화/프레임/복원/연구 gate 발생 위치

아래는 RequireLive/Require/guard.Check/checks.Check/throw/recovery/Stop/Skip 및 pending fallback 문장을 원문 그대로 모은 것이다. 각 행의 조건이 현재 차단 이유다. 대응 칸은 본문의 게임 동작·설계·위험·비용 표에 연결한다. 테스트/명령 오류는 사용자가 잘못된 명령을 보냈을 때의 거절도 남겨 제품의 기능 거부와 혼동하지 않게 했다.

| 위치 | 현재 조건·메시지 원문 | 목적지·구현 크기·위험·비용 근거 |
|---|---|---|
| `DecoBake.Adopt.cs:45` | `if (!ids.TryGetValue(d, out int target)) throw new InvalidOperationException("adopt 대상 변경");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.Adopt.cs:85` | `if (arg != "on") throw new ArgumentException("adopt on&#124;off&#124;state");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `DecoBake.Adopt.cs:88` | `throw new InvalidOperationException("adopt 준비는 정지/일시정지에서만 허용: catch-up 종료 후 게임 일시정지, adopt on, 재개");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.Adopt.cs:89` | `if (!ready &#124;&#124; table == null &#124;&#124; table.Supported == 0 &#124;&#124; baselineObjects == null) throw new InvalidOperationException("같은 세대 ready/O>0 필요");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.Adopt.cs:91` | `if (ForeignSetter() &#124;&#124; Compat.QuartzOptimizer) throw new InvalidOperationException("Compat: 외부 장식 setter/Quartz optimizer");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.Adopt.cs:93` | `throw new InvalidOperationException("FrameGen 장식 힌트는 Rec 목록을 요구하므로 채택과 동시 사용 불가");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.Adopt.cs:96` | `throw new InvalidOperationException("FastMove/DecoAnim/InstantMove/ZeroTween 경로 필요, catch-up 종료 후 켜기");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `DecoBake.Adopt.cs:147` | `throw new InvalidOperationException("adopt 미확인 시작 소스");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.Adopt.cs:163` | `if (baselineObjects[d] == null) throw new InvalidOperationException("adopt 장식 소멸");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.Adopt.cs:184` | `throw new InvalidOperationException("adopt 기반 경로/세대 변경");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.Adopt.cs:186` | `throw new InvalidOperationException("FrameGen 장식 힌트 활성화");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.Adopt.cs:187` | `if ((adoptFrames % 120) == 0 && ForeignSetter()) throw new InvalidOperationException("외부 setter 패치 변경");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.Adopt.cs:255` | `if (obj == null) throw new InvalidOperationException("canary destroyed");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.AdoptSig.cs:30` | `if (!ready &#124;&#124; table == null &#124;&#124; !TightLate.CaptureReady) throw new InvalidOperationException("adoptsig: ready/장식 LateUpdate 후크 필요");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.AdoptSig.cs:34` | `&#124;&#124; parts[0] == "." &#124;&#124; parts[0] == "..") throw new ArgumentException("adoptsig 이름 [초=8] [표시Hz=120]");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `DecoBake.AdoptSig.cs:38` | `&#124;&#124; !DecoBakeCore.Finite(signatureHz) &#124;&#124; signatureHz < 20 &#124;&#124; signatureHz > 1000) throw new ArgumentException("초 0~30, Hz 20~1000");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `DecoBake.AdoptSig.cs:40` | `if (signatureIndices.Length == 0) throw new InvalidOperationException("adoptsig: O=0");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.AdoptSig.cs:59` | `throw new InvalidOperationException("서명 중 세대/채택 상태 변경");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.AdoptSig.cs:62` | `if (!DecoBakeCore.Finite(time)) throw new InvalidOperationException("서명 시각 비유한");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.AdoptSig.cs:65` | `if (time < signatureStart &#124;&#124; signatureCount >= 2000) throw new InvalidOperationException("서명 되감기/2000프레임 상한");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.AdoptSig.cs:67` | `if (cam == null) throw new InvalidOperationException("서명 카메라 없음");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.AdoptSig.cs:74` | `if (d == null) throw new InvalidOperationException("서명 장식 소멸");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.AdoptSig.cs:77` | `if (pivot == null) throw new InvalidOperationException("서명 pivot 없음");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.AdoptSig.cs:112` | `foreach (float value in signatureValues) if (!DecoBakeCore.Finite(value)) throw new InvalidOperationException("서명 값 비유한");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.Core.cs:42` | `if (source == null &#124;&#124; source.Count > maximum) throw new InvalidOperationException("효과 목록/상한");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `DecoBake.Core.cs:71` | `throw new SnapshotChangedException();` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.Core.cs:135` | `if (!Finite(now)) throw new InvalidOperationException("adopt binding clock");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `DecoBake.Core.cs:141` | `if (baseline[index].T0 > now) throw new InvalidOperationException("adopt future schedule changed");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.Core.cs:144` | `if (index == baseline.Length) throw new InvalidOperationException("adopt unmatched scheduled effect");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `DecoBake.Core.cs:148` | `if (baseline[index++].T0 > now) throw new InvalidOperationException("adopt future effect removed");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `DecoBake.Core.cs:244` | `if (!Finite(value)) throw new InvalidOperationException("adopt nonfinite slot=" + slot);` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.Core.cs:251` | `if (!Finite(time)) throw new InvalidOperationException("adopt clock nonfinite");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `DecoBake.Core.cs:279` | `try { if (!Finite(v)) throw new InvalidOperationException("adopt recovery nonfinite"); write(slot, v); Last[slot] = v; }` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `DecoBake.Core.cs:282` | `if (first != null) throw new InvalidOperationException("adopt recovery setter", first);` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `DecoBake.Core.cs:328` | `if (!Finite(pitch) &#124;&#124; pitch <= 0 &#124;&#124; !Finite(tweenScale) &#124;&#124; tweenScale <= 0 &#124;&#124; ease == null) throw new ArgumentException("clock/ease");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `DecoBake.Core.cs:339` | `if (!Finite(e.T0) &#124;&#124; e.T0 < previous) throw new ArgumentException("unsorted/invalid event time");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `DecoBake.Core.cs:360` | `if (!Finite(target)) throw new ArgumentException("nonfinite target");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `DecoBake.cs:65` | `throw new InvalidOperationException("기준 스냅샷 재사용 거부: 장식 개수/순서/인스턴스 ID/객체 변경; 맵 다시 open 필요");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.cs:117` | `if (found.Length != 1) throw new InvalidOperationException(type.Name + "." + name + " 후보=" + found.Length);` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.cs:160` | `if (!enabled &#124;&#124; status.StartsWith("중단:")) throw new InvalidOperationException("decobake ready: " + status);` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.cs:167` | `if(GpuLiveActive)RecoverGpuLive("snapshot_generation_change",false);` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `DecoBake.cs:232` | `if (baselineObjects != null) throw new InvalidOperationException("기준 스냅샷 재사용 거부: 새 장식 객체 Setup; 맵 다시 open 필요");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.cs:233` | `if (initial.Count >= MaxDecorations && !initial.ContainsKey(d)) throw new InvalidOperationException("장식 상한");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.cs:297` | `if (manager == null &#124;&#124; manager != scrDecorationManager.instance &#124;&#124; initial.Count == 0) throw new InvalidOperationException("open 전 on 필요: Setup 초기값 없음");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.cs:298` | `if (ADOBase.isOfficialLevel &#124;&#124; ADOBase.levelIsMikoSkip) throw new InvalidOperationException("커스텀 일반 맵만 지원");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.cs:299` | `if (ADOBase.controller.visualQuality != VisualQuality.High) throw new InvalidOperationException("High 시각 설정만 지원");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.cs:307` | `throw new InvalidOperationException("전체 PrepVfx 예약 스냅샷 없음/목록 교체; 맵 다시 open 필요");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.cs:382` | `if (count > MaxDecorations) throw new InvalidOperationException("장식 상한");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.cs:406` | `if (d == null &#124;&#124; ids.ContainsKey(d)) throw new InvalidOperationException("null/중복 장식");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.cs:408` | `if (!initial.TryGetValue(d, out var source)) throw new InvalidOperationException("초기 Setup 누락");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `DecoBake.cs:436` | `if (!ids.TryGetValue(d, out int id)) throw new InvalidOperationException("대상/초기값 불일치");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.cs:437` | `targets.Add(id); if (++refs > MaxTargetRefs) throw new InvalidOperationException("대상 참조 상한");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.cs:463` | `if (!ids.TryGetValue(d, out int id)) throw new InvalidOperationException("조건 대상 불일치");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.cs:472` | `if (epoch != generation &#124;&#124; manager.allDecorations.Count != count &#124;&#124; effectList != scrVfxPlus.instance.effects) throw new DecoBakeCore.SnapshotChangedException();` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `DecoBake.cs:524` | `throw new InvalidOperationException("기준 재굽기 지원 수 불일치: 최초=" + baselineSupported + " 현재=" + table.Supported);` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.cs:550` | `if (GpuDecoPlayer.Product) { GpuDecoPlayer.Skip("재생 준비 뒤 효과 목록이 바뀜"); ReleasePlayerLevel(); return; }` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `DecoBake.cs:561` | `CancelBuild(); status = "중단: " + why; Log(status); if (GpuDecoPlayer.Product) GpuDecoPlayer.Skip(why); }` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `DecoBake.G1.cs:108` | `if(g1EventGuards.TryGetValue(id,out var previous))RequireLive(previous.Equals(value),"ambiguous G1 no-op event source");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `DecoBake.G1.cs:113` | `RequireLive(g1EventGuards.TryGetValue(f.GetInstanceID(),out var saved) && saved.Equals(new G1EventGuard(f)), "G1 constant-field event changed after preparation");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `DecoBake.G1.cs:124` | `RequireLive(!gpuCollapseOn &#124;&#124; DrawCollapsePolicy.GlobalVerts,"G1 discrete visibility requires global collapse map");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `DecoBake.Live.cs:65` | `if (!DecoBakeCore.Finite(value)) throw new InvalidOperationException("live nonfinite");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DecoBake.Live.cs:88` | `if (source.Started) throw new InvalidOperationException("duplicate live source");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `DecoBake.Live.cs:111` | `if (!DecoBakeCore.Finite(time) &#124;&#124; !DecoBakeCore.Finite(dt) &#124;&#124; dt < 0) throw new InvalidOperationException("live clock");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `DecoBake.Live.cs:144` | `if (!DecoBakeCore.Finite(value)) throw new InvalidOperationException("live recovery nonfinite");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `DrawCollapsePolicy.cs:46` | `if(maxSlices<=0)throw new ArgumentOutOfRangeException(nameof(maxSlices));` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DrawCollapsePolicy.cs:50` | `if(counts[i]<0)throw new ArgumentOutOfRangeException(nameof(counts));` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DrawCollapsePolicy.cs:168` | `throw new InvalidOperationException("drawcollapse ambiguous material tie layer="+rows[i].Key.Layer+" order="+rows[i].Key.Order+" queue="+rows[i].Key.Queue+" dist="+rows[i].Key.Distance.ToString("R")+" mat="+rows[i].Key.Material+" owned="+rows[i].Owned+"/"+rows[i-1].Owned+" dist0="+rows[i-1].Key.Distance.ToString("R"));` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `DrawCollapsePolicy.cs:195` | `throw new ArgumentOutOfRangeException();` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DrawCollapsePolicy.cs:202` | `if(width==1 && height==1 && mip+1<mips)throw new ArgumentException("excess mip count");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `DrawCollapsePolicy.cs:210` | `if(width<=0 &#124;&#124; height<=0)throw new ArgumentOutOfRangeException();` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DrawCollapsePolicy.cs:217` | `if(spans<0)throw new ArgumentOutOfRangeException(nameof(spans));` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `DrawCollapsePolicy.cs:238` | `if(!collect)throw new InvalidOperationException(reason);` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `DrawCollapsePolicy.cs:247` | `throw new InvalidOperationException(all);` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `FastMove.cs:183` | `if (GpuDecoPlayer.Active) DecoBake.RecoverGpuLive("FastMoveException:"+ex.GetType().Name);` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `FastMove.cs:235` | `DecoBake.RecoverGpuLive("FastMoveOriginal",false);` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `FastMove.cs:252` | `if(!fx &#124;&#124; !fx.decManager &#124;&#124; fx.targetTags==null)throw new InvalidOperationException("missing target manager/tags");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoAblation.cs:21` | `RequireLive(g2Window<0,"ablation cannot be used in G2 acceptance windows");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoAblation.cs:22` | `RequireLive(mode=="off"&#124;&#124;mode=="copy"&#124;&#124;mode=="draw"&#124;&#124;mode=="both"&#124;&#124;mode=="root"&#124;&#124;mode=="rootcopy","ablate off&#124;copy&#124;draw&#124;both&#124;root&#124;rootcopy&#124;state");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoAblation.cs:23` | `RequireLive(mode=="off"&#124;&#124;gpuLiveActive,"ablation requires active GPU ownership");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoAblation.cs:26` | `RequireLive(GpuDecoCopy.Enabled&&GpuDecoCopy.MaskedEnabled,"copy ablation requires masked native copies");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoAblation.cs:27` | `foreach(var s in gpuLiveSlices)RequireLive(s.NativeCopy!=null&&s.NativeCopy.Ready,"missing native copy context");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoAblation.cs:30` | `RequireLive(gpuAblationRestoreErrors==0,"ablation renderer restore failed");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoAblation.cs:37` | `RequireLive(gpuLiveRoot,"missing replacement renderer root");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoActivation.cs:77` | `if(gpuActivationDeferred++<30){Array.Clear(gpuLiveCpuFallback,0,gpuLiveCpuFallback.Length);gpuLiveCpuFallbackCount=0;throw new GpuActivationDeferredException();}` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoActivation.cs:80` | `RequireLive(slice.Independent,"native tween CPU fallback requires independent renderers");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoActivation.cs:81` | `RequireLive(gpuLiveOwnedSet.Count>0,"no safe GPU targets at this activation boundary");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoActivation.cs:117` | `if(resumed==null)throw new InvalidOperationException("activation rollback record missing");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoActivation.cs:145` | `RequireLive(gpuLiveActive && gpuLivePrepared && g2Window==-1,"resource mark requires active GPU outside performance windows");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoActivation.cs:163` | `RequireLive(data.tokens.Length>0 && data.tokens.Length<=64 && data.objects.Length>0,"resource mark must be nonempty/bounded");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoActivation.cs:164` | `RequireLive(33L+4L*(data.tokens.Length+data.objects.Length)<=RenderMergeLayoutPolicy.ResourceFileLimit, "resource mark exceeds bounded probe file");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoActivation.cs:179` | `RequireLive(file.Exists && file.Length>0 && file.Length<=RenderMergeLayoutPolicy.ResourceFileLimit,"resource probe file missing/oversized");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoActivation.cs:186` | `RequireLive(data.version==1 && data.process==process.Id && n>0 && n<=64,"resource probe wrong process/schema/token count");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoActivation.cs:188` | `n=reader.ReadInt32();RequireLive(RenderMergeLayoutPolicy.ValidResourceObjects(n,stream.Length-stream.Position),` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoActivation.cs:192` | `RequireLive(stream.Position==stream.Length,"resource probe trailing data");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoActivation.cs:201` | `RequireLive(!gpuLiveActive && !gpuLivePrepared && g2Window==-1,"release prepared ownership before checking resources");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoActivation.cs:203` | `RequireLive(!reload &#124;&#124; different,"new DLL MVID was not loaded");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoActivation.cs:211` | `RequireLive(alive==0 && nativeAlive==0,"marked Unity resources or retired native token still accessible");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoActivation.cs:217` | `RequireLive(FrameCompare.PlaybackContinuity && !FrameCompare.Paused && !Hitch.Playing, "same-frame stale playing condition not reproduced");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoActivation.cs:223` | `RequireLive(RecoverGpuLive("resume_fixture",false) && !gpuLiveActive && gpuLastRestoreComplete, "same-frame CPU continuation not restored");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoActivation.cs:233` | `RequireLive(fx!=null,"no supported owned-target effect for resume fixture");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoActivation.cs:236` | `RequireLive(!original && gpuLiveActive && gpuLiveSkippedRecs>before,"supported resume effect lost GPU ownership");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoActivation.cs:243` | `RequireLive(FrameCompare.Paused && gpuLiveActive && gpuLiveCurrentSlice>=0 && g2Window==-1, "GPU value readback is paused diagnostic only");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoActivation.cs:284` | `RequireLive(gpuRecoveryFaultStage==null && !gpuLiveActive && CountUnrestoredOwned()==0, "activation fault not consumed or ownership unrestored");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoActivation.cs:286` | `foreach(var n in natives)RequireLive(n.D.eventTweens.TryGetValue(n.Key,out var t) && ReferenceEquals(t,n.Tween) && t.active && BitConverter.SingleToInt32Bits(t.Elapsed(false))==BitConverter.SingleToInt32Bits(n.Position) && t.IsPlaying()==n.Playing, "native CPU fallback identity/position/playing changed");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoActivation.cs:301` | `else RequireLive(GpuStep(kind),"CPU transition command failed");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoActivation.cs:302` | `RequireLive(!gpuLiveActive && CountUnrestoredOwned()==0,"CPU transition left GPU ownership");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoCopy.cs:62` | `&#124;&#124; sf_deco_copy_version()!=1)throw new InvalidOperationException("D3D11/native copy contract unavailable");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoCopy.cs:68` | `if(targets[item].count*targets[item].stride!=144)throw new InvalidOperationException("quad target size changed");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoCopy.cs:79` | `if(result.token==0 &#124;&#124; callback==IntPtr.Zero)throw new InvalidOperationException("native context rejected buffer descriptors");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoCopy.cs:117` | `if(AblateDestinations) {if(!masked)throw new InvalidOperationException("unmasked copy ablation");low=high=0;}` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoCopy.cs:154` | `if(sf_deco_copy_stats(instance.token,out var value)==0)throw new InvalidOperationException("native stats unavailable");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoCopy.cs:173` | `if(id<=0)throw new ArgumentOutOfRangeException(nameof(tokens));` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoCopyMask.cs:17` | `if(items<0 &#124;&#124; items>ItemLimit)throw new ArgumentOutOfRangeException(nameof(items));` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoCopyMask.cs:21` | `if(order.Length!=items)throw new ArgumentException("copy order length");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoCopyMask.cs:26` | `if((uint)item>=(uint)items &#124;&#124; seen[item])throw new ArgumentException("copy order is not a permutation");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoData.cs:114` | `static void Check(double v) { if (!DecoBakeCore.Finite(v)) throw new InvalidDataException("nonfinite GPU decoration data"); }` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoData.cs:115` | `static int Count(BinaryReader r, int max) { int n = r.ReadInt32(); if (n < 0 &#124;&#124; n > max) throw new InvalidDataException("GPU decoration count"); return n; }` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoData.cs:118` | `if (Schema != Version) throw new InvalidDataException("legacy export cannot be relabeled as source-clock evidence");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoData.cs:157` | `if (r.ReadInt32() != 0x31445047) throw new InvalidDataException("GPU decoration magic");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoData.cs:158` | `int version = r.ReadInt32(); if (version != 1 && version != Version) throw new InvalidDataException("GPU decoration schema");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoData.cs:162` | `if (data.Pitch <= 0 &#124;&#124; data.TweenScale <= 0 &#124;&#124; data.PixelsPerWorld <= 0) throw new InvalidDataException("GPU decoration clock/projection");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoData.cs:170` | `for (int j = 0; j < 6; j++) { d.Triangles[j] = r.ReadInt32(); if ((uint)d.Triangles[j] >= 4) throw new InvalidDataException("GPU decoration triangle"); }` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoData.cs:174` | `if (totalSegments > 4000000) throw new InvalidDataException("GPU decoration segment budget");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoData.cs:182` | `if (s.T0 < previous &#124;&#124; s.Duration < 0) throw new InvalidDataException("GPU decoration track order");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoData.cs:193` | `if (s.Duration < 0 &#124;&#124; (s.Duration > 0 ? a.Clock < 0 : a.Clock != -1)) throw new InvalidDataException("GPU decoration anchor");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoData.cs:204` | `if (s.StartFrame < 0 &#124;&#124; s.StartFrame > data.Frame &#124;&#124; s.Progress < 0) throw new InvalidDataException("GPU decoration source clock");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoData.cs:207` | `if (a.Valid && a.Segment.Duration > 0 && a.Clock >= data.Sources.Length) throw new InvalidDataException("GPU decoration anchor clock index");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoData.cs:211` | `throw new InvalidDataException("GPU decoration frame interval");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoData.cs:212` | `if (data.MeanProgressStep < 0 &#124;&#124; (data.FrameSamples == 0) != (data.MeanProgressStep == 0)) throw new InvalidDataException("GPU decoration progress interval");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoData.cs:214` | `if (stream.Position != stream.Length) throw new InvalidDataException("GPU decoration trailing bytes");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoDirectTexturePrepare.cs:34` | `RequireLive(gpuImageTimeline!=null && gpuImageTimeline.SelectorCount==gpuImageVariants.Length, "image collapse timeline missing");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDirectTexturePrepare.cs:41` | `RequireLive(t && t.mipmapCount==1,"image collapse variant texture/mips unsupported");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapse.cs:17` | `RequireLive(ReferenceEquals(gpuImageCollapseAddressOwner,gpuCollapseTexture)&&gpuImageCollapseAddresses!=null, "image collapse address hooks not connected in PrepareDrawCollapse");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapse.cs:24` | `RequireLive(texture&&material&&(uint)item<(uint)gpuCollapseIncluded.Length &&gpuImageCollapseAddresses.TryGetValue(texture.GetInstanceID(),out _),"image_texture_not_registered_in_collapse");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapse.cs:40` | `checks.Check((gpuLiveActive &#124;&#124; gpuPrePlan) && gpuLivePrepared && (gpuLiveCurrentSlice>=0 &#124;&#124; gpuPrePlan) && gpuScope.WholeSong && gpuLiveSlices.Count==1 && gpuLiveSlices[0].Independent && !gpuRenderMergeOn, "drawcollapse requires independent whole-song GPU ownership");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoDrawCollapse.cs:43` | `checks.Check((GpuDecoPlayer.Product ? (GpuLiveOwned>0 &#124;&#124; gpuPrePlan && gpuLiveOwnedIndices.Length>0) : GpuLiveOwned==23547 &#124;&#124; GpuLiveOwned==28523) && (gpuLiveCpuFallbackCount==0 &#124;&#124; GpuDecoPlayer.Product && gpuLiveCpuFallbackCount==gpuCollapsePlannedFallback), "drawcollapse requires GPU owned=23547 or 28523, CPU fallback=0");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapse.cs:46` | `checks.Check(!GpuDecoCostProbe.Enabled, // SingleSubmission/GpuListCaching 은 draw 통합과 독립이므로 함께 켜도 된다(스크립트 비용 시험)  "drawcollapse must isolate the original independent baseline");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoDrawCollapse.cs:50` | `checks.Check(GpuDecoCopy.ReadTotals().Errors==0,"drawcollapse native copy errors");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoDrawCollapse.cs:60` | `RequireLive(!gpuLiveActive && !gpuLivePrepared,"drawcollapse prepare must precede live prepare");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoDrawCollapse.cs:61` | `RequireLive(!gpuRenderMergePrepare,"drawcollapse and rendermerge preparations are exclusive");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoDrawCollapse.cs:94` | `RequireLive(GpuPlaybackState.Paused && g2Window==-1,"drawcollapse readback is paused diagnostic only");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoDrawCollapse.cs:96` | `RequireLive(song>=259.75 && song<=270.25,"drawcollapse diagnostic requires song 260..270");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoDrawCollapse.cs:98` | `RequireLive(cam && cam.orthographic,"drawcollapse requires the prepared orthographic camera");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoDrawCollapse.cs:111` | `RequireLive(original && original.sprite && proxy,"drawcollapse missing owned renderer/texture");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapse.cs:113` | `RequireLive(texture && texture.GetInstanceID()==slice.Data.Items[i].TextureId, "drawcollapse texture changed since preparation");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapse.cs:121` | `RequireLive(DecoBakeCore.Finite(p.position.x) && DecoBakeCore.Finite(p.position.y) && DecoBakeCore.Finite(p.position.z) && DecoBakeCore.Finite(p.color.a),"nonfinite drawcollapse vertex");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoDrawCollapseGate.cs:20` | `RequireLive(Screen.width==collapseWidth && Screen.height==collapseHeight && Math.Abs(hz-collapseHz)<.01, "drawcollapse display changed inside one comparison");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoDrawCollapseGate.cs:40` | `RequireLive(!gpuLiveActive && collapseWindow<0,"drawcollapse reset requires inactive measurement");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoDrawCollapseGate.cs:70` | `default:throw new ArgumentException("policy tight&#124;keepafter&#124;clip&#124;skiporder&#124;skipcompute&#124;globalverts on&#124;off");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoDrawCollapseGate.cs:116` | `RequireLive(i>=0 && i<4 && collapseWindow<0 && i==collapseGate.Windows.Count(x=>x) && gpuCollapseOn==(i==1&#124;&#124;i==2) && !FrameCompare.Paused && Hitch.Playing && Math.Abs(MergeSong-260)<1.6 && gpuLiveCpuFallbackCount==0,"drawcollapse begin requires ABBA at 260s");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoDrawCollapseGate.cs:126` | `RequireLive(collapseWindow>=0,"drawcollapse window not open");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoDrawCollapseGate.cs:148` | `RequireLive(i>=0 && i<3 && gpuLiveActive && !gpuCollapseOn && FrameCompare.Paused && Math.Abs(MergeSong-targets[i])<.6,"drawcollapse pixel requires paused 260/262/270 original snapshots");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoDrawCollapseRenderer.cs:76` | `gpuCollapseOn=false;gpuCollapsePendingFallback=null;` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoDrawCollapseRenderer.cs:194` | `if(!MergeHidden(item))gpuCollapsePendingFallback="visible_item_outside_prepared_window item="+item;` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoDrawCollapseRenderer.cs:200` | `RequireLive(!gpuRenderMergePrepare && gpuScope.WholeSong && gpuLiveSlices.Count==1 && gpuLiveSlices[0].Independent && (GpuPlaybackState.Paused &#124;&#124; GpuDecoPlayer.Preparing),"drawcollapse needs independent paused song preparation");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:202` | `RequireLive(gpuLiveShader.HasKernel("EvaluateCollapse"),"drawcollapse bundle missing EvaluateCollapse");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:205` | `RequireLive(gpuLiveCamera.orthographic && (sortMode==TransparencySortMode.Default &#124;&#124; sortMode==TransparencySortMode.Orthographic), "drawcollapse requires orthographic camera sorting");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:207` | `RequireLive(UnityEngine.Object.FindObjectsByType<SortingGroup>(FindObjectsInactive.Include,FindObjectsSortMode.None) .All(g=>!g.enabled &#124;&#124; !g.gameObject.activeInHierarchy),"drawcollapse unsupported active SortingGroup");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:209` | `RequireLive(DrawCollapsePolicy.PartialMaskAdmit &#124;&#124; UnityEngine.Object.FindObjectsByType<SpriteMask>(FindObjectsInactive.Include,FindObjectsSortMode.None) .All(m=>!m.enabled &#124;&#124; !m.gameObject.activeInHierarchy),"drawcollapse unsupported active SpriteMask");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:214` | `RequireLive(gpuCollapseShader && gpuCollapseShader.isSupported,"drawcollapse bundle missing/unsupported GpuDecoBatch.shader");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:230` | `RequireLive(!row.R.enabled &#124;&#124; !row.R.gameObject.activeInHierarchy &#124;&#124; row.R.forceRenderingOff, "drawcollapse admitted auxiliary is visible");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoDrawCollapseRenderer.cs:250` | `RequireLive(sprite && sprite.sprite && row.M && (row.M.shader.name=="Sprites/Tile" &#124;&#124; (DrawCollapsePolicy.BlendModes && GpuDecoBlend.ShaderFits(row.M.shader,source.Data.Items[i].BlendMode))) && sprite.maskInteraction==SpriteMaskInteraction.None,"drawcollapse unsupported owned material/mask");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:252` | `RequireLive(row.M.shaderKeywords.All(k=>k=="USE_POINT_SAMPLING" &#124;&#124; (source.Data.Items[i].BlendMode>0 && (k.StartsWith("BLENDMODES_MODE_") &#124;&#124; k=="FORCE_POINT_SAMPLING"))),"drawcollapse unsupported shader keywords: "+string.Join(",",row.M.shaderKeywords));` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:253` | `RequireLive((!row.M.HasProperty("_Color") &#124;&#124; row.M.GetColor("_Color")==Color.white) && (!row.M.HasProperty("_Opacity") &#124;&#124; row.M.GetFloat("_Opacity")==1), "drawcollapse unsupported material tint/opacity");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:257` | `RequireLive(t && t.GetInstanceID()==source.Data.Items[i].TextureId && t.mipmapCount==1 && ((t.wrapModeU==TextureWrapMode.Clamp && t.wrapModeV==TextureWrapMode.Clamp) &#124;&#124; GpuImageCollapseUvInUnitRange(source.Data.Items[i].UV)), "drawcollapse texture changed or unsupported mip/wrap");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:262` | `RequireLive(SystemInfo.supports2DArrayTextures && (SystemInfo.copyTextureSupport&CopyTextureSupport.DifferentTypes)!=0, "drawcollapse Texture2D-to-array copy unsupported");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:273` | `RequireLive(SystemInfo.maxTextureArraySlices>0,"drawcollapse texture array slices unavailable");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:314` | `RequireLive(SystemInfo.maxTextureArraySlices>0,"drawcollapse texture array slices unavailable");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:325` | `RequireLive(SystemInfo.graphicsMemorySize>0 && total<(long)SystemInfo.graphicsMemorySize*1048576, "drawcollapse array budget exceeds total VRAM"); // Free VRAM still needs external measurement.` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:327` | `RequireLive(arrays<=DrawCollapsePolicy.ArrayBindings && direct.Length<=DrawCollapsePolicy.DirectBindings, "drawcollapse texture binding budget exceeds 4 arrays + 32 borrowed 2D");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:334` | `RequireLive(SystemInfo.maxTextureArraySlices>0 && gpuCollapseArrays.Count<DrawCollapsePolicy.ArrayBindings,"drawcollapse exceeds four array bindings");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:341` | `RequireLive(a.graphicsFormat==t.graphicsFormat,"drawcollapse array format conversion rejected");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:351` | `RequireLive(gpuCollapseArrays.Count<DrawCollapsePolicy.ArrayBindings,"drawcollapse exact array exceeds array bindings");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:356` | `RequireLive(ea.graphicsFormat==t0.graphicsFormat,"drawcollapse exact array format conversion rejected");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:452` | `RequireLive(slice.NativeCopy!=null,"native copy unavailable after reorder: "+GpuDecoCopy.Status);` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:748` | `if(!prepared){gpuCollapsePendingFallback=gpuCollapsePendingFallback??("소유 장식 깊이 변경(준비 안 한 깊이) item="+it+" depth="+depth);return;}` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:751` | `catch(Exception e){gpuCollapsePendingFallback=gpuCollapsePendingFallback??("소유 장식 깊이 변경 처리 예외: "+e.Message);}` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoDrawCollapseRenderer.cs:1014` | `if(gpuCollapsePlannedFallback!=gpuLiveCpuFallbackCount)throw new InvalidOperationException("CPU 처리 장식 개수 불일치");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoDrawCollapseRenderer.cs:1081` | `RequireLive(row.R && material,"drawcollapse replan owned renderer lost item="+i);` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:1082` | `RequireLive(material==row.M,"drawcollapse replan owned material changed item="+i);` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:1105` | `RequireLive(row.R && material,"drawcollapse replan barrier lost");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:1199` | `RequireLive(plan.Count>0 && plan.Count<=DrawCollapsePolicy.MaxSpans,"drawcollapse conservative prepared spans="+plan.Count+" exceeds renderer budget");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:1220` | `RequireLive(items.All(i=>SpanGameLayer(i)==spanLayer), "drawcollapse span crosses camera culling layers");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:1227` | `RequireLive(items.All(i=>!CRow(i).M.HasProperty(p) &#124;&#124; CRow(i).M.GetFloat(p)==expected), "drawcollapse incompatible blend states "+p);` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:1297` | `RequireLive(s.Mesh.subMeshCount==1 && (DrawCollapsePolicy.GlobalVerts &#124;&#124; s.Mesh.GetVertexBufferStride(0)==60),"drawcollapse raw mesh layout");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoDrawCollapseRenderer.cs:1308` | `RequireLive(s.Renderer.sharedMaterials.Length==1,"drawcollapse requires one material");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:1351` | `RequireLive(m.IsKeywordEnabled(CollapseNoDirectKeyword)==sp.Material.IsKeywordEnabled(CollapseNoDirectKeyword) && m.IsKeywordEnabled(CollapseFewKeyword)==sp.Material.IsKeywordEnabled(CollapseFewKeyword), "drawcollapse ordered material lost texture variant");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:1401` | `RequireLive(!gpuCollapseOn && gpuCollapseTexture!=null && gpuCollapseShader, "drawcollapse ready requires prepared textures and collapse off");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:1524` | `guard.Check(prepared,"drawcollapse prepare before live prepare required");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoDrawCollapseRenderer.cs:1525` | `guard.Check(gpuLiveCpuFallbackCount==0 &#124;&#124; GpuDecoPlayer.Product && gpuLiveCpuFallbackCount==gpuCollapsePlannedFallback,"drawcollapse requires zero CPU fallback; disable before fallback");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:1526` | `guard.Check(gpuLiveCamera,"drawcollapse camera lost");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoDrawCollapseRenderer.cs:1534` | `&& dot>=0.999999f))guard.Check(false,"drawcollapse camera sort axis/mode changed dot="+dot.ToString("R",Inv));` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:1539` | `guard.Check(gpuCollapseIncluded[i] &#124;&#124; DrawCollapseTextureFallback(i) &#124;&#124; MergeHidden(i), "drawcollapse visible item outside prepared window item="+i);` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:1550` | `guard.Check(!r.R &#124;&#124; !r.R.enabled &#124;&#124; !r.R.gameObject.activeInHierarchy &#124;&#124; r.R.forceRenderingOff, "drawcollapse omitted auxiliary became visible");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoDrawCollapseRenderer.cs:1584` | `guard.Check(span.Renderer && span.Material,"drawcollapse representative lost");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:1608` | `if(!((before && key.CompareTo(proxy)<0) &#124;&#124; (after && key.CompareTo(proxy)>0)))guard.Check(false, "drawcollapse proxy/barrier order changed renderer="+r.GetInstanceID() +" name="+r.name+" key="+DcKey(key)+" first="+DcKey(firstKey)+" last="+DcKey(lastKey)+" proxy="+DcKey(proxy) +" span_items="+span.Items.Length +" plan_key="+DcKey(barrier.Key)+" judge="+DcKey(judge)+" order_now="+r.sortingOrder+" layer_now="+r.sortingLayerID+" mat_now="+(r.sharedMaterial?r.sharedMaterial.name:"null")+" mat_plan="+(barrier.M?barrier.M.name:"null") +" active="+DrawCollapseBarrierActive(barrier)+" watch_idx="+gpuWatchRows.IndexOf(barrier)+" in_dict="+(gpuBarrierRowById!=null && gpuBarrierRowById.TryGetValue(r.GetInstanceID(),out var dr) && ReferenceEquals(dr,barrier))+" deco_owned="+(r.GetComponentInParent<scrVisualDecoration>() is scrVisualDecoration pv && gpuLiveMasterByDeco.ContainsKey(pv))+" parent="+(r.transform.parent?r.transform.parent.name:"-"));` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:1630` | `if(row==null &#124;&#124; !row.R &#124;&#124; !row.M){guard.Check(false,"drawcollapse owned item="+item+" lost");return;}` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoDrawCollapseRenderer.cs:1635` | `guard.Check(false,"drawcollapse owned item="+item+" sort key changed name="+row.R.name +" layer="+row.Key.Layer+"->"+now.Layer+" order="+row.Key.Order+"->"+now.Order +" queue="+row.Key.Queue+"->"+now.Queue+" material="+row.Key.Material+"->"+now.Material +" distance_delta="+(row.Key.Distance-now.Distance).ToString("R",Inv)+" camera_shift="+shift.ToString("R",Inv));` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:1649` | `RequireLive(GpuDecoPlayer.Product &#124;&#124; time>=257.5 && time<=270.25,"outside_prepared_260..270_window");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoDrawCollapseRenderer.cs:1659` | `if (GpuDecoPlayer.Product) throw new InvalidOperationException(reason);` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoExport.cs:360` | `StopGpuLive("target_rollback_rejected",false);` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoExport.cs:366` | `RecoverGpuLive("effect:"+ex.GetType().Name,!(ex is NotSupportedException));` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoExport.cs:417` | `StopGpuLive("GameCallback:"+method,method=="OnLandOnPortal");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoExport.cs:458` | `throw new InvalidOperationException("gpudeco: open부터 첫 export까지 300초 예산 초과");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:463` | `if (!ok) throw new InvalidOperationException("gpudeco G2: " + reason);` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:503` | `throw new InvalidOperationException("gpudeco source_pool_exhausted: no unreferenced source; unfinished animation retained");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoExport.cs:872` | `RequireLive(!GpuDecoNativeBatch.StaticCombine,"static_batch_live_not_validated; use paused storage comparison");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoExport.cs:873` | `RequireLive(ready && table != null && baselineObjects != null && manager != null, "decobake ready 필요");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:874` | `RequireLive(GpuDecoPlayer.Product &#124;&#124; !string.IsNullOrEmpty(gpuBundlePath) && File.Exists(gpuBundlePath), "gpudeco bundle <path> 필요");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoExport.cs:887` | `RequireLive(SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 && SystemInfo.supportsComputeShaders, "D3D11 compute required");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoExport.cs:889` | `RequireLive(cam && cam.orthographic && camy, "orthographic camera required");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:895` | `RequireLive(Mathf.Abs(axis.x) < 1e-6f && Mathf.Abs(axis.y) < 1e-6f && Mathf.Abs(axis.z) > 0, "Z sorting axis required");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoExport.cs:896` | `RequireLive(manager.transform.position == Vector3.zero && manager.transform.rotation == Quaternion.identity && manager.transform.lossyScale == Vector3.one, "manager transform must be identity");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:900` | `RequireLive(gpuLiveBundle, "bundle load failed");PrepMark("번들");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:902` | `RequireLive(gpuLiveShader, "compute asset missing");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoExport.cs:1067` | `RequireLive(!masterRows[i-1].Key.SameMaterialTie(masterRows[i].Key) &#124;&#124; masterRows[i-1].Item<0 && masterRows[i].Item<0,"rendermerge ambiguous material tie before barrier compaction");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoExport.cs:1199` | `RequireLive(GpuDecoNativeBatch.Enabled && !GpuDecoNativeBatch.StaticCombine,"native copy requires independent renderers");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoExport.cs:1207` | `RequireLive(slice.NativeCopy!=null,"native copy unavailable: "+GpuDecoCopy.Status);` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoExport.cs:1306` | `for(int i=1;i<mats.Length;i++)RequireLive(mats[i-1].GetInstanceID()<mats[i].GetInstanceID(),"merge submesh material IDs must increase strictly");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoExport.cs:1362` | `if (s < 0) {StopGpuLive("outside_prepared_scope");return;}` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:1515` | `RecoverGpuLive("LateUpdateException:"+e.GetType().Name);` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:1581` | `throw new NotSupportedException("native tween appeared during GPU activation");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoExport.cs:1650` | `RequireLive(!gpuRecoveryBlocked,"GPU recovery requires live prepare before enabling again");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoExport.cs:1653` | `RequireLive(!GpuDecoPlayer.Product, "?? ? GPU ??? ??");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoExport.cs:1660` | `RequireLive(FindGpuLiveSlice(BakeTime)>=0,"time outside prepared scope; originals retained");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:1729` | `if(activationCommitted)RecoverGpuLive("activation_committed:"+ex.GetType().Name);` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoExport.cs:1735` | `if(!GpuRecoveryTesting)throw new InvalidOperationException("GPU activation refused; CPU rollback attempted",ex);` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoExport.cs:1891` | `try {StopGpuLive("Application.quitting",false);DisposeGpuLive();GpuLog("shutdown_disposed=True native_contexts="+GpuDecoCopy.ActiveContexts);}` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoExport.cs:1920` | `try {StopGpuLive("Dispose");}` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoExport.cs:1926` | `StopGpuLive("dispose_forced",false);` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoExport.cs:2021` | `StopGpuLive("g2_reset");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:2038` | `if (a[1] != "on" && a[1] != "off") throw new ArgumentException("gpudeco g2 ready on&#124;off");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoExport.cs:2040` | `RequireLive(GpuPlaybackState.Paused && gpuLiveActive == on && (!on &#124;&#124; GpuLiveOwned > 0) && Math.Abs(song - 258.0) <= 0.6, "ready requires paused state at 258s with matching mode");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoExport.cs:2055` | `RequireLive(!GpuDecoCostProbe.Enabled && g2Window == -1 && idx >= 0 && idx <= 3 && nextMode == expectedMode && !GpuPlaybackState.Paused && gpuLiveActive == nextMode && (!nextMode &#124;&#124; (GpuLiveOwned > 0 && warmup >= 3f)) && Math.Abs(song - 262.0) <= 0.6, "invalid ABBA window begin idx=" + idx + " mode=" + a[1] + " song=" + song.ToString("F6", Inv) + " warmup=" + warmup.ToString("F3", Inv));` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:2077` | `RequireLive(g2Window >= 0 && g2Window <= 3, "no active G2 window");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:2176` | `throw new ArgumentException("gpudeco g2 stability pause_resume&#124;song_end&#124;retry_restart&#124;reopen_toggle");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoExport.cs:2232` | `throw new ArgumentException("gpudeco g2 reset&#124;ready&#124;begin&#124;end&#124;stability&#124;result");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoExport.cs:2242` | `if (!enabled &#124;&#124; status.StartsWith("중단:")) throw new InvalidOperationException("gpudeco scheduled: " + status);` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:2250` | `if (!File.Exists(gpuBundlePath)) throw new FileNotFoundException("gpudeco bundle", gpuBundlePath);` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:2258` | `RequireLive(!gpuLiveActive && g2Window<0,"stop playback ownership before changing scope");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:2265` | `if (command == "live off") { StopGpuLive("live_off"); return true; }` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoExport.cs:2278` | `if(command=="nombp on"&#124;&#124;command=="nombp off"){RequireLive(!gpuLiveActive,"nombp must be set before live prepare/on");GpuDecoNativeBatch.NoMpb=command.EndsWith("on");GpuLog("nombp="+GpuDecoNativeBatch.NoMpb);return true;}` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoExport.cs:2283` | `RequireLive(!gpuLiveActive && g2Window==-1,"single submission toggle requires inactive GPU ownership");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:2293` | `RequireLive(gpuLiveActive && GpuLiveOwned==23547 && Screen.width==3440 && Screen.height==1440 && Math.Abs(hz-165)<.1,"modcpu single submission requires owned=23547 and 3440x1440@165");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:2295` | `RequireLive(!TightLate.GpuListCaching && !gpuRenderMergeOn,"modcpu must isolate single submission");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:2296` | `RequireLive(GpuDecoCopy.ReadTotals().Errors==0,"modcpu native/managed copy errors");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoExport.cs:2297` | `RequireLive(!GpuDecoCopy.SingleSubmission &#124;&#124; GpuDecoCopy.SingleExecutions>0 && GpuDecoCopy.SingleFallbacks==0, "single submission unavailable or fell back; not candidate evidence");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:2321` | `RequireLive(!gpuLiveActive, "late cache toggle requires inactive GPU ownership");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:2330` | `RequireLive(gpuLiveActive && GpuLiveOwned == 23547 && Screen.width == 3440 && Screen.height == 1440 && Math.Abs(hz - 165) < .1, "modcpu requires owned=23547 and 3440x1440@165; actual="` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:2333` | `RequireLive(!TightLate.GpuListCaching &#124;&#124; TightLate.GpuListReady, "modcpu late cache unavailable; fallback is not candidate evidence");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:2348` | `RequireLive(!gpuLiveActive && !gpuLivePrepared,"admission mode requires before preparation");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:2357` | `RequireLive(!gpuLiveActive,"effect cache toggle requires inactive GPU ownership");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoExport.cs:2370` | `RequireLive(GpuPlaybackState.Paused && !Hitch.Playing && gpuLiveActive && g2Window==-1, "resume fixture requires paused active GPU and stale playing sample");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoExport.cs:2376` | `RequireLive(GpuPlaybackState.Paused && gpuLiveActive && g2Window==-1,"transition fixture requires paused GPU ownership");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoExport.cs:2378` | `RequireLive(new[]{"off","reset","generation","shutdown"}.Contains(kind),"unknown CPU transition");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoExport.cs:2384` | `RequireLive(GpuPlaybackState.Paused && !gpuLiveActive && gpuLivePrepared && g2Window==-1,"activation fixture requires paused prepared CPU ownership");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoExport.cs:2386` | `RequireLive(new[]{"activation_after_record","activation_after_capture","activation_after_dispatch"}.Contains(stage),"unknown activation stage");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoExport.cs:2391` | `RequireLive(GpuPlaybackState.Paused && gpuLiveActive && g2Window==-1,"recovery fixture requires paused active ownership");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoExport.cs:2394` | `RequireLive(new[]{"render_before","render_after","preflight","target_after_channel"}.Contains(stage),"unknown recovery fixture");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoExport.cs:2401` | `RequireLive(gpuLivePrepared && g2Window==-1,"CPU census is a preparation/boundary diagnostic only");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:2426` | `RequireLive(GpuPlaybackState.Paused && !gpuLiveActive && g2Window==-1,"key fixture requires paused CPU ownership");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoExport.cs:2428` | `RequireLive(!gpu &#124;&#124; gpuLivePrepared,"GPU key fixture requires prepared scope");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoExport.cs:2431` | `RequireLive(idle!=null,"key fixture has no idle decoration; not measured");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoExport.cs:2432` | `GpuLog(DecoAnim.GpuResumeKeyFixture(idle,gpu?(Action)(()=>{StartGpuLive();StopGpuLive("live_off");}):null));return true;` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoExport.cs:2436` | `RequireLive(GpuPlaybackState.Paused && gpuLiveActive && g2Window==-1,"resume handoff fixture requires paused GPU ownership");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoExport.cs:2439` | `GpuLog(DecoAnim.TestGpuResumeHandoff(plan,()=>StopGpuLive("live_off")));return true;` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoExport.cs:2443` | `RequireLive(GpuPlaybackState.Paused && !gpuLiveActive && gpuLivePrepared && g2Window==-1,"resume fixture requires paused prepared CPU playback");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoExport.cs:2444` | `GpuLog(DecoAnim.TestGpuResumeRoundtrip(()=>{StartGpuLive();StopGpuLive("live_off");},d=>gpuLiveMasterByDeco.ContainsKey(d)));return true;` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoExport.cs:2449` | `RequireLive(result.Contains("frames=12 ") && result.Contains("mismatch=0 ") && !result.Contains("continued=0 "),"resume continuation audit failed");return true;` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoExport.cs:2455` | `RequireLive(g2Window==-1,"cost probe is not a G2 acceptance measurement");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:2457` | `RequireLive(!gpuLiveActive &#124;&#124; GpuPlaybackState.Paused,"cost probe changes require stopped or paused playback");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:2460` | `else throw new ArgumentException("costprobe on&#124;off&#124;reset&#124;report");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoExport.cs:2465` | `RequireLive(GpuPlaybackState.Paused && gpuLiveActive && gpuLivePreparedMasked,"paused masked-copy ownership required");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoExport.cs:2504` | `RequireLive(g2Window==-1 && (!gpuLiveActive &#124;&#124; GpuPlaybackState.Paused),"zeroalpha changes require stopped or paused playback");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:2506` | `RequireLive(!next &#124;&#124; GpuDecoNativeBatch.Enabled,"zeroalpha requires independent renderer path");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoExport.cs:2513` | `RequireLive(g2Window==-1,"threadcpu changes require no active G2 window");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:2515` | `RequireLive(gpuMicroTick==0,"threadcpu changes require no active micro window");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:2528` | `InvalidateGpuSnapshot(); StopGpuLive("reset");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:2536` | `if (command == "off") { InvalidateGpuSnapshot(); StopGpuLive("off"); gpuCapture = false; gpuSource = null; gpuPrepareWatch = null; GpuLog("on=False owned=0"); return true; }` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoExport.cs:2554` | `RequireLive(GpuPlaybackState.Paused && gpuLiveActive && !gpuZeroAlpha,"alpha pixel requires restored unculled live state");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoExport.cs:2559` | `RequireLive(diff.Changed==0 && diff.Max.Max()==0 && restored==0 && stable,"zeroalpha paired pixels changed");return true;` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoExport.cs:2565` | `RequireLive(!gpuLiveActive && !GpuDecoSnapshot.Active,"stop drawing before changing native copy mode");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoExport.cs:2567` | `RequireLive(!enabled &#124;&#124; (GpuDecoNativeBatch.Enabled && !GpuDecoNativeBatch.StaticCombine),"native copy requires nativebatch on");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoExport.cs:2575` | `RequireLive(!gpuLiveActive && !GpuDecoSnapshot.Active,"stop drawing before changing native batch mode");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoExport.cs:2613` | `throw new InvalidOperationException("diagnostic pixel requires draw off + unchanged paused export");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoExport.cs:2635` | `if (string.IsNullOrEmpty(gpuBundlePath)) throw new InvalidOperationException("gpudeco: fresh export + bundle <path> required");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:2644` | `throw new InvalidOperationException("gpudeco: fresh export + bundle <path> required");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:2651` | `{ gpuPixelGate.Error = "invalid_pixel_state"; GpuLog("GPUDECO_G1=ERROR stage=pixel reason=invalid_pixel_state"); throw new InvalidOperationException("gpudeco pixel: draw off + same paused export/sample required"); }` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoExport.cs:2666` | `throw new InvalidOperationException("gpudeco on: live prepare or AUTOTEST paused G1 gate required");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoExport.cs:2668` | `throw new ArgumentException("gpudeco off&#124;state&#124;scheduled&#124;ready&#124;live prepare&#124;live on&#124;live off&#124;live state&#124;g2 ...&#124;diag5&#124;bisect&#124;oracle&#124;export <새 파일 경로>(AUTOTEST 전용)");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoExport.cs:2680` | `RequireLive(stream != null, "GPU 장식 번들 없음");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:2686` | `if (count == 0) throw new EndOfStreamException("GPU 장식 번들 읽기 실패");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:2748` | `throw new InvalidOperationException("gpudeco diag5 requires paused frame after gpudeco export + bundle");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoExport.cs:2847` | `throw new InvalidOperationException("gpudeco bisect requires paused frame after gpudeco export + bundle");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoExport.cs:2959` | `if (!completed && clocks[clock].Progress != r.Pos) throw new InvalidOperationException("export: 같은 소스의 실제 진행값 불일치 source=" + r.GpuSource);` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoExport.cs:2995` | `if (!gpuCapture) throw new InvalidOperationException("export: gpudeco prepare 후 실제 효과 시작 관찰 필요");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:2999` | `throw new IOException("export: 기존 정답 파일 보존; 생성기로 새 출력 폴더를 지정하세요: " + output);` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:3002` | `if (!ready &#124;&#124; table == null &#124;&#124; Adopting &#124;&#124; LiveDecoRenderer.Active) throw new InvalidOperationException("export: bake ready, adopt/livedeco off 필요");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:3003` | `if (!ADOBase.controller &#124;&#124; !ADOBase.controller.paused &#124;&#124; Time.timeScale != 0) throw new InvalidOperationException("export: framecmp pause 필요");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:3006` | `if (!cam &#124;&#124; !cam.orthographic &#124;&#124; !camy &#124;&#124; !Dormancy.LiveTrackingReady) throw new InvalidOperationException("export: 직교 카메라/Dormancy 필요");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:3008` | `throw new InvalidOperationException("export: 관리자 변환이 단위 변환이 아님");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoExport.cs:3050` | `if (ts.x == 0 &#124;&#124; ts.y == 0 &#124;&#124; d.textureScaleMultiplier.x == 0 &#124;&#124; d.textureScaleMultiplier.y == 0) throw new InvalidOperationException("export: zero static scale");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoExport.cs:3098` | `throw new InvalidOperationException("GPUDECO_G1=OWNED_LOW; 이후 시험 중단");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoGpu.cs:91` | `if(a.Valid) Require(Supported(s0.Ease),"unsupported anchor ease " + s0.Ease);` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoGpu.cs:99` | `Require(Supported(s.Ease),"unsupported ease " + s.Ease + " decoration="+d.Id);` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoGpu.cs:112` | `Require(Marshal.SizeOf<Anchor>()==48,"anchor GPU stride");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoGpu.cs:114` | `Require(Marshal.SizeOf<LiveChannel>()==16,"live channel GPU stride");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoGpu.cs:136` | `Require(Marshal.SizeOf<Vertex>()==36 && Mesh.GetVertexBufferStride(0)==36,"raw vertex stride");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoGpu.cs:151` | `Require((uint)index<(uint)data.Items.Length && next!=null,"image item index");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoGpu.cs:153` | `Require(next.Shape?.Length==12 && next.OffsetBasis?.Length==9 && next.Constants?.Length==8 && next.UV?.Length==8 && next.Triangles?.Length==6,"image geometry layout");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoGpu.cs:155` | `Require(next.Id==prior.Id && next.Index==prior.Index && ReferenceEquals(next.Initial,prior.Initial) && ReferenceEquals(next.Tracks,prior.Tracks) && ReferenceEquals(next.Anchors,prior.Anchors),"image update changed logical channels");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoGpu.cs:159` | `for(int i=0;i<6;i++)Require(next.Triangles[i]==prior.Triangles[i],"image topology changed");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoGpu.cs:160` | `foreach(float v in next.Shape)Require(DecoBakeCore.Finite(v),"nonfinite image shape");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoGpu.cs:161` | `foreach(float v in next.OffsetBasis)Require(DecoBakeCore.Finite(v),"nonfinite image basis");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoGpu.cs:162` | `foreach(float v in next.Constants)Require(DecoBakeCore.Finite(v),"nonfinite image constants");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoGpu.cs:163` | `foreach(float v in next.UV)Require(DecoBakeCore.Finite(v),"nonfinite image UV");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoGpu.cs:164` | `Require(next.Constants[0]!=0 && next.Constants[1]!=0 && next.Constants[2]!=0 && next.Constants[3]!=0 && (next.Flags&~((hasImageGeometry?127:63)&#124;128&#124;256))==0,"image geometry constants");   // 128/256 = 화면 고정(시차 clampToScreen / CameraAspect)` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoGpu.cs:191` | `Require(hasImageGeometry&&imageShapeBuffer!=null&&(uint)index<(uint)data.Items.Length&&corners!=null&&corners.Length==4,"image matrix unsupported");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoGpu.cs:192` | `for(int i=0;i<16;i++)Require(DecoBakeCore.Finite(local[i]),"nonfinite image matrix");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoGpu.cs:193` | `for(int i=0;i<4;i++)Require(DecoBakeCore.Finite(corners[i].x)&&DecoBakeCore.Finite(corners[i].y),"nonfinite image corner");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoGpu.cs:270` | `Require(UseLiveMode && shader.HasKernel("EvaluateCollapse"),"drawcollapse bundle lacks EvaluateCollapse");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoGpu.cs:292` | `Require(UseLiveMode && shader.HasKernel("EvaluateCollapse"),"drawcollapse bundle lacks EvaluateCollapse");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoGpu.cs:357` | `Require(UseLiveMode && shader.HasKernel("EvaluateCollapse"),"drawcollapse bundle lacks EvaluateCollapse");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoGpu.cs:375` | `Require(UseLiveMode && shader.HasKernel("EvaluateCollapse"),"drawcollapse bundle lacks EvaluateCollapse");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoGpu.cs:389` | `Require(UseLiveMode && hasLiveKernel, "single submission requires native live kernel");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoImageGeometryFixture.cs:24` | `RequireLive(gpuLiveActive&&FrameCompare.Paused&&gpuImageCandidate&&gpuImageOwn&&g2Window<0 &&!gpuCollapseOn&&!gpuRenderMergeOn&&!GpuDecoCopy.SingleSubmission &&GpuLiveOwned==28523&&gpuLiveCpuFallbackCount==0&&gpuLiveSlices.Count==1&&Math.Abs(BakeTime-gpuLiveTurnOnBakeTime)<=1e-6, "CPU vertex injection requires fresh paused image activation, independent submission and no performance gate");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:30` | `RequireLive(slice.Independent&&slice.SharedBatch==null&&slice.Gpu!=null&&slice.NativeCopy!=null, "CPU vertex injection requires native independent copies");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:38` | `RequireLive(r&&r.sprite&&r.sprite.vertices.Length==4,"CPU vertex diagnostic sprite missing");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:46` | `RequireLive(DecoBakeCore.Finite(expected.x)&&DecoBakeCore.Finite(expected.y)&&DecoBakeCore.Finite(expected.z),"nonfinite CPU vertex");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:52` | `RequireLive(images>0&&visible>0,"CPU vertex injection must include visible images");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:75` | `RequireLive(slice.NativeCopy.Execute(slice.CopyMask?.Low??ulong.MaxValue,slice.CopyMask?.High??ulong.MaxValue), "CPU vertex diagnostic native copy failed");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:80` | `StopGpuImageVertexDiagnostic(false);RecoverGpuLive("image_cpu_vertex_diagnostic",false);throw;` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:88` | `RequireLive(FrameCompare.Paused&&!gpuLiveActive&&gpuImageCandidate,"refreshpose selection requires paused image CPU state");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:95` | `RequireLive(FrameCompare.Paused,"restore CPU vertex diagnostic while paused");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:100` | `RequireLive(FrameCompare.Paused&&gpuLiveActive&&!GpuImageVertexDiagnosticActive,"finish vertex injection before diagnostic comparisons");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:175` | `RequireLive(gpuLiveActive&&FrameCompare.Paused&&gpuImageCandidate&&gpuImageOwn&&GpuLiveOwned==28523 &&gpuLiveCpuFallbackCount==0&&!gpuCollapseOn&&gpuCollapseTexture!=null&&gpuLiveSlices.Count==1, "image collapsecheck requires paused prepared image ownership before collapse on");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:178` | `RequireLive(ReferenceEquals(gpuImageCollapseAddressOwner,gpuCollapseTexture)&&gpuImageCollapseAddresses!=null, "image collapse address hooks missing");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:188` | `RequireLive(gpuImageCollapseVariants!=null,"image collapse window selection missing");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:200` | `RequireLive(valid,"image collapse preflight failed; retain ordering/texture guards and inspect GPUIMG_DIAG");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:204` | `RequireLive(Screen.width==3440&&Screen.height==1440&&Math.Abs(Screen.currentResolution.refreshRateRatio.value-165)<.02, "imageown requires 3440x1440@165; do not change the display automatically");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:236` | `RequireLive(!gpuLiveActive&&imageOwnWindow<0,"imageown gate reset requires inactive GPU");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:245` | `RequireLive(!gpuLiveActive&&imageOwnWindow<0&&imageOwnNextWindow==0,"select image preflight before ABBA");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:251` | `RequireLive(!gpuLiveActive&&imageOwnWindow<0&&imageOwnNextWindow==0,"select measured pose mode before ABBA");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:258` | `RequireLive(!imageOwnRequirePixelPreflight&#124;&#124;imageOwnPixels.All(x=>x),"complete all three integrated pixel checks before ABBA");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:262` | `RequireLive(index>=0&&index<4&&imageOwnWindow<0&&index==imageOwnNextWindow &&(index==0&#124;&#124;imageOwnRestores[index-1])&&gpuLiveActive &&gpuCollapseOn&&GpuLiveOwned==(images?28523:23547)&&gpuLiveCpuFallbackCount==0 &&gpuImageCandidate==images&&(!images&#124;&#124;gpuImageOwn)&&gpuImageRefreshPose==(images&&imageOwnGatePose) &&!GpuImageVertexDiagnosticActive&&!DrawCollapseGuard.IgnoreOrderViolations&&!FrameCompare.Paused&&Hitch.Playing&&Math.Abs(MergeSong-260)<.6, "imageown begin requires clean A/B/B/A 260s image ownership with collapse");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:268` | `RequireLive(!images&#124;&#124;CaptureImageOwnOrderingFallbacks(),"imageown begin found image/unregistered/excess collapse fallback");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:278` | `RequireLive(imageOwnWindow>=0,"imageown window not open");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:309` | `RequireLive(index>=0&&index<4&&imageOwnWindow<0&&index==imageOwnNextWindow-1,"imageown restore requires the last completed window");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:319` | `RequireLive(index>=0&&index<3&&gpuImageCandidate&&gpuImageOwn&&gpuLiveActive&&GpuLiveOwned==28523 &&gpuLiveCpuFallbackCount==0&&gpuImageRefreshPose==imageOwnGatePose&&FrameCompare.Paused&&!gpuCollapseOn &&!GpuImageVertexDiagnosticActive&&Math.Abs(MergeSong-targets[index])<.6,"imageown pixels require frozen 260/262/270 comparison");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:346` | `RequireLive(imageOwnPixels[index],"integrated image pixels failed; do not start ABBA");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:384` | `RequireLive(FrameCompare.Paused&&!gpuLiveActive&&!gpuLivePrepared&&!gpuImageShadow&&g2Window<0,"image fixture requires paused CPU and no prepared GPU resources");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:385` | `RequireLive(gpuImageVariants!=null&&!string.IsNullOrEmpty(gpuBundlePath),"image fixture requires compiled variants and bundle");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:387` | `RequireLive(variants.Length>0,"no quad image variants");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:399` | `RequireLive(d,"no valid image fixture child chain");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:404` | `bundle=AssetBundle.LoadFromFile(System.IO.Path.GetFullPath(gpuBundlePath));RequireLive(bundle,"image fixture bundle load");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:405` | `var shader=bundle.LoadAsset<ComputeShader>("Assets/Shaders/GpuDeco.compute");RequireLive(shader,"image fixture compute asset");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:448` | `RequireLive(threw,"invalid image geometry accepted");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:450` | `RequireLive(ReferenceEquals(prior,data.Items[0]),"rejected image metadata published");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageGeometryFixture.cs:463` | `RequireLive(updated>0&&bad==0&&rejected==12&&pointerChanges==0,"image geometry fixture failed");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageIsolatedFixture.cs:14` | `RequireLive(gpuLiveActive&&FrameCompare.Paused&&gpuImageCandidate,"image isolated requires paused activation");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageIsolatedFixture.cs:15` | `RequireLive(gpuLiveShader.HasKernel("ImagePixelProbe"),"pixel probe bundle required");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageIsolatedFixture.cs:32` | `RequireLive(camera.targetTexture.width==a.width&&camera.targetTexture.height==a.height,"isolated fixture viewport changed");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageIsolatedFixture.cs:55` | `RequireLive(counters[2]>0&&counters[3]>0,"isolated fixture one draw blank");samples++;pixels+=counters[0];maximum=Math.Max(maximum,counters[1]);` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImagePlan.cs:109` | `RequireLive(!gpuLiveActive&&g2Window<0,"image plan requires CPU ownership outside performance gates");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlan.cs:110` | `RequireLive(ready&&baselineEvents!=null&&baselineObjects!=null,"image snapshot missing");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlan.cs:118` | `RequireLive(gpuImageTimeline!=null&&gpuImageVariants!=null&&gpuImageTimeline.RepeatedSources==0,"unique resolved image plan required; do not guess repeated Source IDs");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlan.cs:128` | `RequireLive(command=="images compile"&#124;&#124;command=="images state","images compile&#124;state&#124;shadow on&#124;off&#124;state");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImagePlan.cs:129` | `RequireLive(FrameCompare.Paused&&!gpuImageShadow,"image compilation requires paused CPU ownership with shadow off");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImagePlan.cs:133` | `RequireLive(ready&&baselineEvents!=null&&baselineVisualFlags!=null&&baselineImageSelectors!=null &&baselineEvents.Length==baselineVisualFlags.Length&&baselineEvents.Length==baselineImageSelectors.Length,"image snapshot metadata missing");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlan.cs:145` | `RequireLive(gpuImageTimeline!=null&&gpuImageVariants!=null,"resolved image plan not compiled");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlayback.cs:40` | `RequireLive(texture.mipmapCount==1,"image collapse changed mips");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlayback.cs:48` | `RequireLive(valid,"image_uv_outside_unit_range");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlayback.cs:57` | `RequireLive(gpuImageTimeline!=null&&gpuImageTimeline.SelectorCount==gpuImageVariants.Length, "image collapse timeline missing");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlayback.cs:68` | `RequireLive(t&&t.mipmapCount==1,"image collapse variant texture/mips unsupported");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlayback.cs:89` | `RequireLive(gpuImageCollapseRegisteredTextures!=null&&gpuImageCollapseVariants!=null,"image collapse texture registry missing");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlayback.cs:120` | `RequireLive(valid,"image collapse texture coverage incomplete; inspect GPUIMG_DIAG and change texture-bank preparation before retry");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlayback.cs:142` | `RequireLive(gpuScope.WholeSong&&GpuDecoNativeBatch.Enabled&&!GpuDecoNativeBatch.StaticCombine&&GpuDecoCopy.Enabled, "image playback requires song scope and independent native copy");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlayback.cs:144` | `RequireLive(gpuImageTimeline!=null&&gpuImageVariants!=null&&gpuImageTimeline.RepeatedSources==0&&!gpuImageShadow,"compile unique image variants before GPU preparation");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlayback.cs:173` | `RequireLive(gpuImageCandidate&&gpuImageOwn&&ImageOnlyEvent(fx)&&GpuImageActor(gpuLiveOwnedIndices[master]),"unprepared GPU image target");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlayback.cs:174` | `RequireLive(gpuImageSourceMap.TryGetValue(fx.GetInstanceID(),out int slot) &&gpuImageTimeline.HeaderMatches(slot,FastMove.GpuImageSelector(fx),1) &&gpuImagePlannedTargets[fx.GetInstanceID()].Contains(gpuLiveOwnedIndices[master]),"image event changed after preparation");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlayback.cs:180` | `RequireLive(GpuDecoImageVariant.Capture(FastMove.GpuImageSelector(fx)).Status==GpuDecoImageVariant.Kind.Missing, "missing image became available after preparation");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlayback.cs:184` | `RequireLive(FastMove.TryGpuImageSprite(FastMove.GpuImageSelector(fx),out var source)&&ReferenceEquals(source,v.Source) &&source!=null&&source.sprite&&source.sprite.GetInstanceID()==v.SpriteId&&source.sprite.texture&&source.sprite.texture.GetInstanceID()==v.TextureId, "image asset changed after preparation");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlayback.cs:193` | `RequireLive(DecoBakeCore.Finite(gpuImageFields[c]),"nonfinite image logical state");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlayback.cs:203` | `RequireLive(g!=null,"image fixture unsupported child chain");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImagePlayback.cs:228` | `RequireLive(g1ImageExpectedEmpty[master],"unexpected missing GPU image sprite: "+d.name+" master="+master+" blend="+GpuDecoBlend.Id(d)+" renderer="+(d.spriteRenderer?d.spriteRenderer.enabled.ToString():"null"));` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlayback.cs:235` | `RequireLive(source&&source.sprite&&source.sprite.texture&&!source.sprite.associatedAlphaSplitTexture,"GPU image missing physical sprite");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlayback.cs:237` | `RequireLive(variant.Status==GpuDecoImageVariant.Kind.ReadyQuad,"GPU image no longer a quad");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlayback.cs:244` | `RequireLive(slice.Independent&&slice.Gpu!=null&&slice.Gpu.HasNativeLiveKernel&&slice.Renderers.Count==slice.Data.Items.Length,"GPU image renderer mapping");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlayback.cs:246` | `RequireLive(renderer&&source.sharedMaterial&&renderer.sharedMaterial &&(source.sharedMaterial.shader==renderer.sharedMaterial.shader &#124;&#124; prior.BlendMode>0 &#124;&#124; GpuDecoBlend.ShaderFits(source.sharedMaterial.shader,prior.BlendMode)) &&source.sharedMaterial.renderQueue==renderer.sharedMaterial.renderQueue &&source.sortingLayerID==renderer.sortingLayerID&&source.sortingOrder==renderer.sortingOrder,"GPU image material/order changed");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlayback.cs:256` | `RequireLive(slice.Gpu.HasImageGeometry&&geometry.SupportsImageMatrix,"image matrix chain unsupported");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlayback.cs:262` | `if(gpuImageUploadFault){gpuImageUploadFault=false;throw new InvalidOperationException("injected_image_upload");}` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlayback.cs:306` | `gpuImageRestores++;RecoverGpuLive("image_stage"+stage+":"+ex.GetType().Name,false);` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlayback.cs:316` | `catch(Exception ex){gpuImageRestores++;RecoverGpuLive("image_flush:"+ex.GetType().Name,false);throw;}` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlayback.cs:325` | `gpuImageRestores++;RecoverGpuLive("image_preflush:"+ex.GetType().Name,false);` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlayback.cs:362` | `RequireLive(gpuLiveActive&&FrameCompare.Paused&&gpuImageCandidate,"image audit requires paused active GPU");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImagePlayback.cs:400` | `RequireLive(gpuLiveActive&&FrameCompare.Paused&&gpuImageCandidate,"image locate requires paused activation");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImagePlayback.cs:442` | `{RequireLive(FrameCompare.Paused&&!gpuLiveActive,"image matrix toggle requires paused CPU");gpuImageMatrix=command.EndsWith(" on",StringComparison.Ordinal);return true;}` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImagePlayback.cs:445` | `RequireLive(!ready&&!gpuLiveActive&&!gpuLivePrepared,"select image candidate before map snapshot");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlayback.cs:450` | `RequireLive(FrameCompare.Paused&&!gpuLiveActive&&gpuImageCandidate,"image ownership toggle requires paused CPU");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImagePlayback.cs:456` | `if(command=="images isolated"){throw new NotSupportedException("images isolated: unfinished draft (excluded from build)");}` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImagePlayback.cs:458` | `{LogGpuImagePlayback();RequireLive(gpuLiveActive&&gpuImageSetters>0&&gpuImageRestores==0&&GpuDecoCopy.ReadTotals().Errors==0,"GPU image window not retained");return true;}` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlayback.cs:460` | `{LogGpuImagePlayback();RequireLive(!gpuLiveActive&&gpuImageRestores>0&&gpuLastRestoreComplete,"GPU image failure did not restore CPU");return true;}` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImagePlayback.cs:462` | `{RequireLive(gpuLiveActive&&gpuImageCandidate,"active image candidate required");gpuImageUploadFault=true;return true;}` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoImageTextureFixture.cs:15` | `RequireLive(FrameCompare.Paused&&!gpuLiveActive&&!gpuLivePrepared&&!gpuImageShadow&&g2Window<0,"texture fixture requires paused CPU and no prepared GPU resources");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageTextureFixture.cs:16` | `RequireLive(gpuImageVariants!=null&&!string.IsNullOrEmpty(gpuBundlePath),"texture fixture requires variants and bundle");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageTextureFixture.cs:18` | `RequireLive(variants.Length>0,"texture fixture has no quads");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageTextureFixture.cs:26` | `RequireLive(source,"texture fixture has no source material");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageTextureFixture.cs:34` | `bundle=AssetBundle.LoadFromFile(System.IO.Path.GetFullPath(gpuBundlePath));RequireLive(bundle,"texture fixture bundle");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageTextureFixture.cs:35` | `var shader=bundle.LoadAsset<ComputeShader>("Assets/Shaders/GpuDeco.compute");RequireLive(shader,"texture fixture compute");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageTextureFixture.cs:43` | `RequireLive(target.Create(),"texture fixture RT creation");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageTextureFixture.cs:77` | `RequireLive(extent>0&&DecoBakeCore.Finite(extent),"texture fixture projection");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageTextureFixture.cs:107` | `RequireLive(samples>0&&different==0&&nonempty>0&&changedImages>0,"image texture pixel fixture failed");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageTimeline.cs:72` | `if((uint)index>=(uint)DecorationCount)throw new ArgumentOutOfRangeException(nameof(index));` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoImageTimeline.cs:81` | `&#124;&#124;end<start&#124;&#124;owned==null)throw new ArgumentException("invalid image texture window");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageTimeline.cs:85` | `if((uint)d>=(uint)DecorationCount)throw new ArgumentException("invalid image window target");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageTimeline.cs:95` | `if(row.Scheduled<previous)throw new ArgumentException("nonmonotone image texture window schedule");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageTimeline.cs:112` | `if(decorations<0&#124;&#124;decorations>100000&#124;&#124;input==null&#124;&#124;input.Count>300000)throw new ArgumentException("image tape budget");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageTimeline.cs:120` | `&#124;&#124;(e.VisualFlags&~15)!=0&#124;&#124;(e.Selector!=null)!=((e.VisualFlags&1)!=0))throw new ArgumentException("invalid image occurrence");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageTimeline.cs:122` | `foreach(int d in e.Targets)if((uint)d>=(uint)decorations)throw new ArgumentException("invalid image target");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageTimeline.cs:124` | `if(e.Selector.Length>32768&#124;&#124;targetList.Count>4000000-e.Targets.Length)throw new ArgumentException("image tape payload budget");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoImageTimeline.cs:127` | `if(ids.Count>=4096)throw new ArgumentException("image selector budget");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoNativeAdopt.cs:66` | `var field=type.GetField(name,NativeFields&#124;BindingFlags.Static)??throw new MissingFieldException(type.FullName,name);` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoNativeAdopt.cs:67` | `if(!typeof(T).IsAssignableFrom(field.FieldType))throw new InvalidCastException("native field type: "+name);` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoNativeAdopt.cs:179` | `RequireLive(!gpuLiveActive,"native adoption toggle requires inactive GPU ownership");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoNativeAdopt.cs:183` | `RequireLive(nativeUpdateMethod!=null,"native update boundary unavailable");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoNativeAdopt.cs:495` | `RequireLive(NativeAdoptionUnchanged(s),"native adoption changed before capture");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoNativeAdopt.cs:522` | `RequireLive(DecoBakeCore.Finite(end),"native adoption nonfinite endpoint");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoNativeAdopt.cs:525` | `RequireLive(NativeNear(gpuMasterChannels[slot].change,s.Change[c]) && NativeNear(EvalMasterSlotCurrent(slot),s.Current[c]),"native adoption channel mismatch");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoNativeAdopt.cs:615` | `RequireLive(NativeAdoptionUnchanged(s), "native adoption changed before commit");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoNativeAdopt.cs:619` | `RequireLive(channel.Replacement!=null,"native adoption proxy missing before commit");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoNativeAdopt.cs:628` | `RequireLive(mismatch==0,"native adoption current value mismatch");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoNativeAdopt.cs:638` | `RequireLive(!channel.Tween.active,"native tween kill refused");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoNativeBatch.cs:39` | `Require(parent.localToWorldMatrix==Matrix4x4.identity,"identity batch root required");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoNativeBatch.cs:40` | `Require(originals.Length==data.Items.Length && initial.Length==originals.Length*4,"input count changed");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoNativeBatch.cs:41` | `Require(exactState==null&#124;&#124;exactState.Length==originals.Length,"state count changed");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoNativeBatch.cs:50` | `Renderer state=exactState==null?(Renderer)original:exactState[i];Require(state,"missing renderer state");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoNativeBatch.cs:62` | `Require(state.sharedMaterials.Length==1,"multi-material renderer state unsupported");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoNativeBatch.cs:79` | `var camera=GpuDecoScene.FindMain();Require(camera,"missing sort camera");int bucketCount=1;` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoNativeBatch.cs:109` | `Require(GpuDecoScene.SortKey(state,state.sharedMaterial,camera).SameBucket(GpuDecoScene.SortKey(renderer,renderer.sharedMaterial,camera)),"restored static sort bucket changed");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoNativeBatch.cs:122` | `foreach(var renderer in renderers) {renderer.enabled=false;Require(splitSortBuckets&#124;&#124;renderer.isPartOfStaticBatch,"native batching unavailable");}` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoNativeBatch.cs:127` | `Require(mesh.GetVertexBufferStride(0)==36 && mesh.GetVertexAttributeOffset(VertexAttribute.Color)==12 && mesh.GetVertexAttributeFormat(VertexAttribute.Color)==VertexAttributeFormat.Float32 && mesh.GetVertexAttributeOffset(VertexAttribute.TexCoord0)==28,"combined vertex layout unsupported");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoNativeBatch.cs:141` | `Require(submesh.indexCount==6,"combined draw range unsupported");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoNativeBatch.cs:146` | `Require(destination>=0 && destination<map.Length,"combined index outside vertex buffer");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoNativeBatch.cs:147` | `Require(map[destination]==uint.MaxValue &#124;&#124; map[destination]==from,"combined vertex alias changes animation ownership");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoNativeBatch.cs:151` | `Require(map.All(x=>x!=uint.MaxValue),"unmapped combined vertices");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoNativeBatch.cs:157` | `Require(seed[i].position.Equals(expected.position) && seed[i].uv.Equals(expected.uv) && seed[i].color.Equals(rawColor?expected.color:GpuDecoGpu.DrawColor(expected.color)),"combined vertex map does not match evaluated source");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoNativeBatch.cs:178` | `Require(actual[i].position.Equals(expected.position) && actual[i].uv.Equals(expected.uv) && (rawColor?actual[i].color.Equals(expected.color):GpuDecoGpu.EquivalentDrawColor(actual[i].color,expected.color)),"mapped GPU copy mismatch");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoOrderProbe.cs:25` | `Require(!scene &#124;&#124; (sourceGroups.Length>0 && sourceGroups.Sum(g=>g.Length)>=3),"scene materials unavailable");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoOrderProbe.cs:28` | `Require(layer>=8,"no unused diagnostic layer");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoOrderProbe.cs:29` | `var shader=Shader.Find("Sprites/Tile");Require(shader,"Sprites/Tile not loaded");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoOrderProbe.cs:76` | `var expected=capture();Require(expected.Any(c=>c.r!=0 &#124;&#124; c.g!=0 &#124;&#124; c.b!=0),"blank positive control");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoOrderProbe.cs:126` | `var restored=capture();Require(expected.SequenceEqual(restored),"native A/A2 unstable");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoOrderProbe.cs:150` | `Require(expected.Length==actual.Length,"capture length changed");changed=0;maximum=0;` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoOrderProbe.cs:160` | `Require(mesh.GetVertexAttributeStream(VertexAttribute.Position)==0 && mesh.GetVertexAttributeFormat(VertexAttribute.Position)==VertexAttributeFormat.Float32,"static position layout unsupported");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoOrderProbe.cs:172` | `Require(original.Length==words && oldStride==destination.stride && oldCount==destination.count,"static buffer layout changed");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:68` | `Require(a.Length==b.Length&&ad.Length==bd.Length,"shape mismatch");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:69` | `for(int i=0;i<a.Length;i++)Require(a[i].Source==b[i].Source&&Bits(a[i].Start,b[i].Start)` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:72` | `for(int i=0;i<ad.Length;i++)Require(ad[i]==bd[i],"dirty bits");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:79` | `Require(Marshal.SizeOf<GpuDecoGpu.Anchor>()==48,"static anchor ABI");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:94` | `Require(sf_deco_packet_commit_static_v1(m,a,b,n,e,s,sn,t,si,targets.Length,mask,d,dirty.Length)==1,"static commit");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:96` | `Require(dirty[0]&&dirty[1]&&!dirty[2],"static dirty blocks");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:101` | `Require(v.valid==(written?1u:0u)&&v.clock==0&&seg.hi==0&&seg.lo==0&&seg.duration==0 &&seg.over==0&&seg.period==0&&seg.ease==(written?1u:0u) &&seg.scale==(written&&(ch==5&#124;&#124;ch==6&#124;&#124;ch==12&#124;&#124;ch==13)?1u:0u) &&Bits(seg.start,written?expected[i].End:0)&&Bits(seg.end,seg.start)&&Bits(seg.target,seg.start),"static anchor "+i);` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:111` | `Require(sf_deco_packet_commit_static_v1(m,a,b,n,e,s,sn,t,si,targets.Length,mask,d,dirty.Length)==-3,"static rejection");si[2]=saved;` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:114` | `fixed(GpuDecoGpu.Anchor* old=anchorBefore)for(int i=0;i<anchors.Length*48;i++)Require(x[i]==((byte*)old)[i],"static rejected anchor byte");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:115` | `for(int i=0;i<dirty.Length;i++)Require(dirty[i]==dirtyBefore[i],"static rejected dirty byte");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:116` | `Require(sf_deco_packet_commit_static_v1(m,a,b,n,m,s,sn,t,si,targets.Length,mask,d,dirty.Length)==-2,"static alias rejection");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:123` | `Require(Marshal.SizeOf<Animation>()==16,"animated ABI");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:138` | `Require(Call(expected,sources,p,targets,origins,scratch,flags)==1,"animated arithmetic");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:154` | `Require(sf_deco_packet_commit_animated_v1(m,a,b,n,e,s,sn,t,si,targets.Length,&p,&animation,clocks,sources.Length,o,origins.Length, work,targetScratch.Length,d,dirty.Length)==-3,"animated last target rejection");si[2]=saved;` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:156` | `Require(!dirty[0]&&!dirty[1]&&!dirty[2],"animated rejected dirty");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:157` | `for(int i=0;i<master.Length;i++)Require(b[i]==-1&&a[i].valid==0,"animated rejected metadata");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:159` | `Require(sf_deco_packet_commit_animated_v1(m,a,b,n,e,s,sn,t,si,targets.Length,&p,&animation,clocks,sources.Length,o,origins.Length, o,origins.Length,d,dirty.Length)==-2,"animated scratch alias rejection");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:162` | `Require(sf_deco_packet_commit_animated_v1(m,a,b,n,e,s,sn,t,si,targets.Length,&p,&animation,clocks,sources.Length,o,origins.Length, work,targetScratch.Length,d,dirty.Length)==1,"animated commit");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:165` | `Require(dirty[0]&&dirty[1]&&!dirty[2],"animated dirty");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:166` | `fixed(GpuDecoGpu.Anchor* w=wanted)for(int i=0;i<anchors.Length*48;i++)Require(((byte*)a)[i]==((byte*)w)[i],"animated anchor byte "+i);` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:167` | `for(int i=0;i<bindings.Length;i++)Require(b[i]==(Array.IndexOf(targets,(uint)(i/16))>=0&&(p.Mask&(1u<<(i&15)))!=0?7:-1),"animated binding");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:174` | `Require(Marshal.SizeOf<Channel>()==16&&Marshal.SizeOf<Source>()==16&&Marshal.SizeOf<Packet>()==80,"ABI size");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:197` | `Require(Call(actual,sources,p,targets,origins,scratch,dirty)==1,"valid packet rejected");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:204` | `throw new InvalidOperationException(ex.Message+" step="+step+" old="+old.ToString("R",CultureInfo.InvariantCulture) +" operand="+value.ToString("R",CultureInfo.InvariantCulture)+" ease="+p.EndEase +" double="+BitConverter.SingleToInt32Bits(full).ToString("X8"));` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:218` | `for(int i=0;i<3;i++){p.Relative=(uint)(i&1);Require(Call(actual,sources,p,targets,origins,scratch,dirty)==1,"signed zero rejected");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:227` | `Require(result==code,"rejection status "+result+" expected "+code);` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:230` | `reject(bad,new uint[]{1,1},scratch,dirty,-3); // Never sort/deduplicate silently.` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:231` | `reject(bad,new uint[]{1,128},scratch,dirty,-3);` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:232` | `reject(bad,new uint[]{1,2},new Channel[16],dirty,-1);` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:233` | `reject(bad,new uint[]{1},scratch,Array.Empty<ulong>(),-1);` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:234` | `reject(bad,new uint[]{1},actual,dirty,-2);` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:235` | `bad.Operand[0]=float.NaN;reject(bad,new uint[]{1},scratch,dirty,-4);bad.Operand[0]=0;` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:236` | `actual[16].Source=sourceCount;reject(bad,new uint[]{0,1},scratch,dirty,-4);actual[16].Source=-1;` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:237` | `bad.Source=sourceCount;reject(bad,new uint[]{1},scratch,dirty,-1);bad.Source=-1;` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:238` | `bad.Mask=65536;reject(bad,new uint[]{1},scratch,dirty,-1);bad.Mask=1;` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:239` | `bad.Relative=2;reject(bad,new uint[]{1},scratch,dirty,-1);bad.Relative=0;` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:240` | `float saved=origins[2];origins[2]=float.NaN;reject(bad,new uint[]{0,1},scratch,dirty,-4);origins[2]=saved;` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:241` | `origins[2]=float.MaxValue;bad.Operand[0]=float.MaxValue;reject(bad,new uint[]{0,1},scratch,dirty,-4);origins[2]=saved;` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:249` | `for(int i=0;i<16;i++)Require(Call(bench,sources,bp,benchTargets,benchOrigins,benchScratch,benchDirty)==1,"benchmark warmup");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketFixture.cs:250` | `long begin=Stopwatch.GetTimestamp();for(int i=0;i<128;i++)Require(Call(bench,sources,bp,benchTargets,benchOrigins,benchScratch,benchDirty)==1,"benchmark packet");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPackets.cs:298` | `if(index<0)throw new InvalidOperationException("packet source membership missing");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoPackets.cs:307` | `{gpuPacketRejectOnce=false;inject=true;throw new InvalidOperationException("explicit packet precommit fault");}` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoPackets.cs:324` | `if(result!=1)throw new InvalidOperationException("static commit status "+result);` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoPackets.cs:372` | `RecoverGpuLive("packet_postcommit:"+ex.GetType().Name);return run.Masters.Length;` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoPackets.cs:390` | `if(!gpuLiveActive&#124;&#124;(gpuPacketMode!=2&&gpuPacketMode!=4))throw new InvalidOperationException("packet rollback requires live diagnostic mode");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoPackets.cs:399` | `throw new InvalidOperationException("packet shadow not verified");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoPackets.cs:402` | `if(gpuLiveActive)throw new InvalidOperationException("packet mode requires inactive playback");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoPackets.cs:403` | `if(command!="packets shadow" && command!="packets static" && command!="packets native" && command!="packets all" && command!="packets replay" && command!="packets compiled" &&command!="packets compiledcheck"&& command!="packets off")throw new ArgumentException("packets shadow&#124;static&#124;native&#124;all&#124;replay&#124;compiled&#124;compiledcheck&#124;off&#124;state&#124;verify");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoPacketTargets.cs:37` | `if(av==packetTagVersion(a)&#124;&#124;bv==packetRowVersion(b))throw new NotSupportedException("list replacement not versioned");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoPacketTargets.cs:39` | `if(av==packetTagVersion(a)&#124;&#124;bv==packetRowVersion(b))throw new NotSupportedException("list clear not versioned");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoPacketTargets.cs:117` | `if(!PacketVersions())throw new NotSupportedException("packet list versions unavailable");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoPacketTargets.cs:127` | `RequireLive(MatchPacketTargetCollections(g,dict,tags),"guard baseline");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoPacketTargets.cs:128` | `Action test=()=>{RequireLive(!MatchPacketTargetCollections(g,dict,tags),"mutation was accepted");rejects++;};` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoPacketTargets.cs:136` | `g=snapshot();RequireLive(!MatchPacketTargetCollections(g,new Dictionary<string,List<scrDecoration>>(dict),tags),"dictionary identity");rejects++;` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoPacketTargets.cs:137` | `RequireLive(!MatchPacketTargetCollections(g,dict,new List<string>(tags)),"tag list identity");rejects++;` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoPlayer.cs:38` | `throw new NotSupportedException("D3D11 컴퓨트 셰이더를 사용할 수 없음");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoPlayer.cs:44` | `if (setup == null &#124;&#124; prep == null &#124;&#124; update == null) throw new MissingMethodException("재생 준비 후크 없음");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoPlayer.cs:62` | `if (method == null) throw new MissingMethodException(pair.Item1.Name + "." + pair.Item2);` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoPlayer.cs:85` | `throw new InvalidOperationException("장식 이동 최적화가 꺼져 있음");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoPlayer.cs:87` | `if (DecoBake.ForeignSetter()) throw new InvalidOperationException("다른 모드의 장식 처리와 겹침");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoPlayer.cs:88` | `if (Screen.width <= 0 &#124;&#124; Screen.height <= 0) throw new InvalidOperationException("화면 크기를 확인할 수 없음");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoPlayer.cs:91` | `throw new InvalidOperationException("지원하지 않는 카메라 설정 (직교 "+camera.orthographic+", 렌더 텍스처 "+(camera.targetTexture?camera.targetTexture.name:"없음")+", VR "+camera.stereoEnabled+", 영역 "+camera.rect+")");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoPlayer.cs:93` | `throw new InvalidOperationException("정렬 그룹 사용 중");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoPlayer.cs:95` | `throw new InvalidOperationException("스프라이트 마스크 사용 중");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoPlayer.cs:462` | `RequireLive(ready && table != null, status);` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoPlayer.cs:536` | `RequireLive(ready && table != null, status);` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoPlayer.cs:549` | `RequireLive(table.Supported >= GpuDecoPlayerPolicy.MinimumOwned, "지원되는 장식이 적음 (64개 미만)");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoPlayer.cs:563` | `RequireLive(!gpuWorkloadGate &#124;&#124; admit,"workload below measured beneficial calibration");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoPlayer.cs:600` | `RequireLive(gpuLiveOwnedIndices.Length >= GpuDecoPlayerPolicy.MinimumOwned, "GPU 소유가 가능한 장식이 적음 (64개 미만)");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoPlayer.cs:618` | `try { StopGpuLive("round_end"); }` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoPlayer.cs:639` | `RequireLive(baselineEvents != null && baselineVisualFlags != null && baselineImageSelectors != null, "이미지 예약 정보 없음");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoPlayer.cs:652` | `RequireLive(gpuLivePrepared && !gpuRecoveryBlocked, "곡 준비가 취소됨");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoPlayer.cs:658` | `throw new InvalidOperationException("그리기 통합을 쓸 수 없어 원본 유지");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoPlayer.cs:735` | `RequireLive(manager && manager==scrDecorationManager.instance && gpuLiveCamera && gpuLiveCamera.orthographic && gpuLiveCamera.rect==new Rect(0,0,1,1) && Mathf.Abs(gpuLiveCamera.transform.forward.z)>.999999f && ADOBase.controller && ADOBase.controller.camy, "카메라 또는 장식 관리자 변경");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoPlayer.cs:739` | `RequireLive(FastMove.Enabled && DecoAnim.Active, "장식 이동 처리 중단");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoPlayer.cs:740` | `RequireLive(GpuDecoCopy.ReadTotals().Errors==0, "GPU 버퍼 복사 오류");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoPlayer.cs:750` | `RequireLive(original && (texture &#124;&#124; G1ExpectedEmpty(master)) && item>=0 && slice.Renderers[item], "소유 장식 또는 GPU 렌더러 소실");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoPlayer.cs:751` | `RequireLive(original.enabled==(DrawCollapsePolicy.DisableOwnedRenderers?false:!G1DrawHidden(master)) && original.gameObject.activeInHierarchy && original.forceRenderingOff, "다른 처리에서 소유 렌더러 상태 변경");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoPlayer.cs:753` | `RequireLive(original.maskInteraction==SpriteMaskInteraction.None && !original.GetComponentInParent<SortingGroup>(), "소유 장식 마스크 또는 정렬 그룹 변경");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoPlayer.cs:756` | `RequireLive(material && shader && ((shader.name=="Sprites/Tile" && material.passCount==1) &#124;&#124; GpuDecoBlend.ShaderFits(shader,slice.Data.Items[item].BlendMode)) // 혼합 장식은 에셋이 재질을 새로 만들거나 FastBlend 가 더하기 재질로 바꿔 끼울 수 있다: 모드가 그대로면 같은 그림이다 && (material.GetInstanceID()==slice.Data.Items[item].MaterialId &#124;&#124; (slice.Data.Items[item].BlendMode>0 && GpuDecoBlend.Id(original.GetComponentInParent<scrVisualDecoration>())==slice.Data.Items[item].BlendMode)), "소유 장식 재질 변경: " + original.name + " shader=" + (shader?shader.name:"null") + " item_blend=" + slice.Data.Items[item].BlendMode + " now_blend=" + GpuDecoBlend.Id(original.GetComponentInParent<scrVisualDecoration>()) + " mat_same=" + (material && material.GetInstanceID()==slice.Data.Items[item].MaterialId));` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoPlayer.cs:760` | `RequireLive(texture.GetInstanceID()==slice.Data.Items[item].TextureId, "준비하지 않은 텍스처 변경");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoPreparation.cs:13` | `if(GpuDecoPlayer.Product)GpuDecoPlayer.Skip(reason);` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoRecovery.cs:61` | `throw new NotSupportedException("GPU effect outside prepared contract reasons="+string.Join(",",e.Reasons)+" ease="+e.Ease+" over="+e.Over+" period="+e.Period+" color="+fx.colorUsed+" parallax="+fx.parallaxUsed+" values="+e.Values[7]+","+e.Values[8]+","+e.Values[9]+","+e.Values[10]+","+e.Values[12]+","+e.Values[13]);` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoRecovery.cs:97` | `if (GpuDecoPlayer.Product) GpuDecoPlayer.Skip("원본 처리로 복귀: " + reason);` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoRecovery.cs:102` | `try {GpuRecoveryFault("resume_preflight");StopGpuLive("recovery:"+reason);continued=gpuLastRestoreComplete;}` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoRecovery.cs:135` | `RequireLive(gpuLiveActive&&Hitch.Playing&&!FrameCompare.Paused&&!GpuDecoCostProbe.Enabled,"micro window requires playing GPU ownership without costprobe");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoRecovery.cs:138` | `RequireLive(gpuMicroTick==0,"micro window already open");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoRecovery.cs:153` | `RequireLive(edge=="end"&&gpuMicroTick!=0,"micro window is not open");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoRecovery.cs:169` | `RequireLive(!gpuLiveActive,"effect cache fixture requires inactive GPU ownership");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoRecovery.cs:178` | `RequireLive(at<gpuEffectChannelCount&&gpuEffectChannels[at++]==c,"cached effect channel order changed");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoRecovery.cs:179` | `RequireLive(at==gpuEffectChannelCount,"cached effect channel count changed");masks++;` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoRecovery.cs:190` | `RequireLive(InstantMove.Bits(a)==InstantMove.Bits(gpuEffectEaseEnd) &&InstantMove.Bits(b)==InstantMove.Bits(gpuEffectEaseDurEnd),"cached effect easing bits changed");eases++;` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoRecovery.cs:199` | `RequireLive(!gpuLiveActive,"Flash fixture requires inactive ownership");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoRecovery.cs:218` | `RequireLive(InstantMove.Bits(position)==InstantMove.Bits(expectedPosition) && InstantMove.Bits(scale)==InstantMove.Bits(expectedScale), "Flash DOTween mismatch ease="+ease+" fraction="+fraction+" over="+over);` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoRecovery.cs:233` | `throw new InvalidOperationException("injected_gpu_recovery_"+stage);` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoRecovery.cs:251` | `RequireLive(fx!=null,"no supported game effect for recovery fixture");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoRecovery.cs:254` | `try {RequireLive(!GpuLiveSkip(fx,d,0),"injected target unexpectedly stayed GPU owned");}` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoRecovery.cs:257` | `RequireLive(gpuRecoveryFaultStage==null && !gpuLiveActive && CountUnrestoredOwned()==0,"recovery fault not consumed or renderers unrestored");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoRecovery.cs:268` | `bool continued=RecoverGpuLive("injected_invalid_resume",false);` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoRecovery.cs:269` | `RequireLive(!continued && !gpuLiveActive && CountUnrestoredOwned()==0,"forced handback contract");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoRecovery.cs:271` | `RequireLive(GpuDecoCopy.ActiveContexts==0 && gpuLiveSlices.Count==0 && !gpuLiveRoot,"forced disposal still owns resources");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoRenderMerge.cs:68` | `throw new InvalidOperationException("rendermerge: "+why);` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoRenderMerge.cs:73` | `RequireLive(GpuPlaybackState.Paused,"rendermerge allocations require paused pre-playback preparation");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoRenderMerge.cs:74` | `RequireLive(gpuScope.WholeSong && gpuLiveSlices.Count==1 && GpuDecoNativeBatch.Enabled && !GpuDecoNativeBatch.NoMpb, "rendermerge requires song scope, independent baseline and nombp off");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoRenderMerge.cs:80` | `RequireLive(!masterRows[i-1].Key.SameMaterialTie(masterRows[i].Key) &#124;&#124; masterRows[i-1].Item<0 && masterRows[i].Item<0,"rendermerge ambiguous material tie");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoRenderMerge.cs:134` | `RequireLive(GpuDecoScene.SortKey(rows[a].R,rows[a].M,gpuLiveCamera).SameBucket( GpuDecoScene.SortKey(renderer,renderer.sharedMaterial,gpuLiveCamera)),"rendermerge sort bucket changed");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoRenderMerge.cs:275` | `RequireLive(!gpuLiveActive,"rendermerge prepare requires inactive ownership");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoRenderMerge.cs:281` | `RequireLive(mergeWindow<0,"close rendermerge measurement before checking layouts");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoRenderMerge.cs:283` | `RequireLive(gpuLiveActive && !gpuRenderMergeOn && gpuMergeRows!=null, "rendermerge ready requires independent GPU ownership");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoRenderMerge.cs:291` | `RequireLive(GpuPlaybackState.Paused,"rendermerge fallback layout changed; prepare while paused before next playback");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoRenderMerge.cs:300` | `RequireLive(mergeWindow<0,"close rendermerge measurement before changing mode");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoRenderMerge.cs:302` | `bool on=sub=="on";RequireLive(!on &#124;&#124; gpuRenderMergePrepared,"rendermerge prepare/live prepare required");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoRenderMerge.cs:303` | `RequireLive(!on &#124;&#124; !gpuLiveActive &#124;&#124; GpuMergeFallbackMatches(), "rendermerge fallback layout changed; ready required before enabling");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoRenderMerge.cs:315` | `throw new ArgumentException("rendermerge prepare&#124;on&#124;off");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoRenderMergeGate.cs:54` | `RequireLive((Screen.width==2560 && Screen.height==1440 && Math.Abs(hz-120)<.5) &#124;&#124; (Screen.width==3440 && Screen.height==1440 && Math.Abs(hz-165)<1.5), "rendermerge acceptance scope requires 2560x1440@120 or 3440x1440@165; actual="+Screen.width+"x"+Screen.height+"@"+hz.ToString("F3",Inv));` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoRenderMergeGate.cs:58` | `RequireLive(Screen.width==mergeScreenWidth && Screen.height==mergeScreenHeight && Math.Abs(hz-mergeScreenHz)<.01, "rendermerge display changed within ABBA; do not mix display environments");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoRenderMergeGate.cs:71` | `RequireLive(!FrameCompare.Paused && !gpuLiveActive,"playbackready requires unpaused CPU playback");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoRenderMergeGate.cs:93` | `RequireLive(mergeWindow<0 && !gpuLiveActive,"rendermerge reset requires no live measurement/ownership");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoRenderMergeGate.cs:106` | `RequireLive(i>=0 && i<4 && i==mergeGate.Seen.Count(x=>x) && mergeWindow<0 && !mergeGate.Seen[i] && gpuLiveActive && gpuRenderMergeOn==on && Hitch.Playing && !FrameCompare.Paused && Math.Abs(MergeSong-260)<.6 && !GpuDecoCostProbe.Enabled, "rendermerge begin requires ABBA GPU ownership at 260s");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoRenderMergeGate.cs:112` | `RequireLive(mergeGate.Population(i,on,allocated,owned,fallback,independentVisible), "rendermerge population requires >=20000 GPU owners, nonempty A and identical ABBA ownership; allocated="` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoRenderMergeGate.cs:125` | `RequireLive(mergeWindow>=0,"rendermerge window not open");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoRenderMergeGate.cs:150` | `RequireLive(i>=0 && i<2 && FrameCompare.Paused && gpuLiveActive && !gpuRenderMergeOn && Math.Abs(MergeSong-target)<.6, "rendermerge pixel requires restored independent GPU at 262/270s");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoRenderMergeGate.cs:164` | `RequireLive(gpuLiveActive && gpuRenderMergeOn && gpuRenderMergePrepared,"watch lifecycle requires active merged ownership");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoRenderMergeGate.cs:168` | `else throw new ArgumentException("rendermerge watch song_end&#124;retry&#124;reopen");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoRenderMergeGate.cs:186` | `else throw new ArgumentException("rendermerge stability song_end&#124;retry&#124;reopen&#124;unload");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoRenderMergeGate.cs:198` | `throw new ArgumentException("rendermerge prepare&#124;ready&#124;on&#124;off&#124;reset&#124;begin 0..3&#124;end&#124;pixel 0..1&#124;watch KIND&#124;stability KIND&#124;result");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoResume.cs:26` | `if(s>=0){if(source>=0 && source!=s)throw new InvalidOperationException("GPU resume mixed source group");source=s;}` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoResume.cs:30` | `RequireLive(clock.Active && clock.Pos>=0f && clock.Pos<clock.Dur && DecoBakeCore.Finite(clock.Pos) && DecoBakeCore.Finite(clock.Dur) && DecoBakeCore.Finite(clock.Over) && DecoBakeCore.Finite(clock.Period) && GpuDecoGpu.LiveSupported((int)clock.Ease),"GPU resume source contract");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoResume.cs:51` | `RequireLive(channel.sourceIndex==source && gpuMasterSlotSource[at+c]==source && anchor.valid!=0 && DecoBakeCore.Finite(channel.start) && DecoBakeCore.Finite(channel.change) && DecoBakeCore.Finite(anchor.segment.target),"GPU resume channel contract");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoResume.cs:58` | `throw new InvalidOperationException("GPU resume ownership changed externally");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoScene.cs:65` | `if (float.IsNaN(distance) &#124;&#124; float.IsInfinity(distance)) throw new InvalidOperationException("nonfinite sort distance");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `GpuDecoScene.cs:74` | `if (main) throw new InvalidOperationException("gpusplit: ambiguous main Camera");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoScene.cs:77` | `if (!main) throw new InvalidOperationException("gpusplit: main Camera missing");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoScopePlan.cs:31` | `throw new InvalidOperationException("gpudeco song item budget exceeded: "+items);` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoSnapshot.cs:50` | `Require(!instance, "stop drawing before selecting diagnostic");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoSnapshot.cs:51` | `Require(mode == "off" &#124;&#124; mode == "world" &#124;&#124; mode == "single" &#124;&#124; mode == "floatcolor" &#124;&#124; mode == "unorm8" &#124;&#124; mode == "localz" &#124;&#124; mode == "float_single_localz", "diagnostic off&#124;world&#124;single&#124;floatcolor&#124;unorm8&#124;localz&#124;float_single_localz required");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoSnapshot.cs:72` | `Require(!instance, "already drawing");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoSnapshot.cs:73` | `Require(FrameCompare.Paused && ADOBase.controller && ADOBase.controller.paused && Time.timeScale == 0, "framecmp pause required");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoSnapshot.cs:74` | `Require(data != null && data.Schema == 2 && data.Items.Length > 0, "fresh export required");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoSnapshot.cs:75` | `Require(Math.Abs(scrConductor.instance.songposition_minusi - data.RawTime) < 1e-6, "export song time changed");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoSnapshot.cs:76` | `Require(excludedItemIds != null &#124;&#124; !GpuDecoCoverage.OwnedLow(data.Awake, data.Items.Length), "OWNED_LOW");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoSnapshot.cs:86` | `Require(SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 && SystemInfo.supportsComputeShaders, "D3D11 compute required");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoSnapshot.cs:87` | `var camera = GpuSplit.FindMain(); Require(camera && camera.orthographic, "orthographic camera required");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoSnapshot.cs:93` | `Require(Mathf.Abs(axis.x) < 1e-6f && Mathf.Abs(axis.y) < 1e-6f && Mathf.Abs(axis.z) > 0, "Z sorting axis required");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoSnapshot.cs:100` | `Require(d && d.GetInstanceID() == item.Id && d.spriteRenderer, "export object changed");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoSnapshot.cs:102` | `Require(r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff && r.sharedMaterial && r.sharedMaterial.GetInstanceID() == item.MaterialId && r.sprite && r.sprite.texture.GetInstanceID() == item.TextureId, "export renderer changed");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoSnapshot.cs:112` | `Require(!r.GetComponentInParent<SortingGroup>(), "live SortingGroup unsupported");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoSnapshot.cs:113` | `var materials = r.sharedMaterials; Require(materials.Length > 0, "missing material slot");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoSnapshot.cs:116` | `var m = materials[slot]; Require(m && m.renderQueue > 2500, "opaque/missing material in transparent census renderer=" + r.GetInstanceID() + " slot=" + slot);` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoSnapshot.cs:118` | `Require(!owned &#124;&#124; materials.Length == 1, "owned material slots changed");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoSnapshot.cs:129` | `Require(rows.Count(r => r.Item >= 0) == candidates.Count, "owned census changed");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoSnapshot.cs:141` | `Require(rows[i - 1].Item < 0 && rows[i].Item < 0, "ambiguous owned material tie material=" + rows[i].Key.Material + " renderers=" + rows[i - 1].R.GetInstanceID() + "," + rows[i].R.GetInstanceID());` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoSnapshot.cs:173` | `bundle = AssetBundle.LoadFromFile(Path.GetFullPath(bundlePath)); Require(bundle, "bundle load failed");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoSnapshot.cs:174` | `var shader = bundle.LoadAsset<ComputeShader>("Assets/Shaders/GpuDeco.compute"); Require(shader, "compute asset missing");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoSnapshot.cs:183` | `Require(DecoBakeCore.Finite(error), "nonfinite GPU vertex"); maximum = Math.Max(maximum, error); sum += error; if (error > .01) over++;` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoSnapshot.cs:186` | `error = Math.Abs((double)v.color[c] - d.Color[c]); Require(DecoBakeCore.Finite(error), "nonfinite GPU color");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoSnapshot.cs:200` | `Require(g0 &#124;&#124; GpuDecoCoverage.Auxiliary(data.Awake), "G0_FAIL before renderer ownership");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoSnapshot.cs:208` | `Require(DiagnosticMode=="off","native batch requires default draw mode");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoSnapshot.cs:234` | `Require(GpuDecoNativeBatch.Enabled && !GpuDecoNativeBatch.StaticCombine && DiagnosticMode=="off", "native copy requires independent default renderers");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoSnapshot.cs:236` | `Require(nativeCopy!=null && nativeCopy.Execute(), "native copy unavailable: "+GpuDecoCopy.Status);` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoSnapshot.cs:279` | `Require(actual[i].position.Equals(expected.position) && DrawColorMatches(actual[i].color, expected.color, quantizeColor) && actual[i].uv.Equals(expected.uv), "GPU run copy mismatch");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoSnapshot.cs:375` | `Require(originalKey.SameBucket(replacementKey),"replacement sort-reference bucket changed");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoSnapshot.cs:395` | `Require(instance && instance.activeData != null, "start GpuDecoSnapshot before running oracle");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoStorage.cs:25` | `RequireLive(mode=="shared"&#124;&#124;mode=="sharedraw"&#124;&#124;mode=="sharedbuckets"&#124;&#124;mode=="independent"&#124;&#124;mode=="state","storage shared&#124;sharedraw&#124;sharedbuckets&#124;independent&#124;state");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoStorage.cs:30` | `RequireLive(gpuLivePrepared&&FrameCompare.Paused&&g2Window<0,"storage swap requires paused prepared ownership outside performance windows");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoStorage.cs:32` | `RequireLive(gpuStorageBackup==null&&gpuLiveSlices.Count==1,"storage swap requires one independent slice");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoStorage.cs:33` | `RequireLive(!GpuDecoCopy.AblateDestinations&&gpuAblationDrawUndo.Count==0&&gpuLiveRoot&&gpuLiveRoot.activeSelf,"storage comparison cannot use an ablated scene");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `GpuDecoStorage.cs:35` | `RequireLive(s.Independent&&s.SharedBatch==null&&s.NativeCopy!=null,"independent native backend required");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `GpuDecoThreadCpu.cs:48` | `if(snapshot==new IntPtr(-1))throw new InvalidOperationException("Thread inventory unavailable");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoThreadCpu.cs:53` | `if(!Thread32First(snapshot,ref entry))throw new InvalidOperationException("Thread inventory empty");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `GpuDecoThreadCpu.cs:74` | `if(samples.Count==0)throw new InvalidOperationException("Thread counters unavailable");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `LiveDeco.cs:40` | `throw new InvalidOperationException("livedeco census requires natural playback with matshare off and framecmp resumed");` | 3.4·4: 대상 L/N 전환·복구 검증 유지; 큼·연속성 위험; C1/CL/CN |
| `LiveDeco.cs:42` | `throw new InvalidOperationException("scene resetting or GPU diagnostics busy");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `LiveDeco.cs:44` | `if (!scrDecorationManager.instance) throw new InvalidOperationException("decoration manager not ready");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `LiveDeco.cs:51` | `if (error != null) throw new InvalidOperationException("livedeco census failed", error);` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `LiveDeco.cs:53` | `default: throw new ArgumentException("livedeco census&#124;on&#124;off&#124;state");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `LiveDeco.cs:117` | `if (decorations.ContainsKey(id)) throw new InvalidOperationException("multiple image owners for renderer " + id);` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `LiveDeco.cs:123` | `if (meshOwners.ContainsKey(meshId)) throw new InvalidOperationException("multiple image owners for mesh " + meshId);` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `LiveDeco.cs:176` | `throw new InvalidOperationException("census partition mismatch");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `LiveDecoChanges.cs:42` | `if (read == null &#124;&#124; read.ReturnType != value) throw new MissingMethodException(t.FullName, getter);` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `LiveDecoChanges.cs:150` | `if (signature.Length > 5) throw new NotSupportedException("change writer arity: " + target);` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `LiveDecoGate2.cs:57` | `if (profiling) throw new InvalidOperationException("profile already started");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `LiveDecoGate2.cs:58` | `if (LiveDecoRenderer.Active &#124;&#124; FrameCompare.Paused) throw new InvalidOperationException("profile baseline must be off and playing");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `LiveDecoGate2.cs:67` | `if (!profiling) throw new InvalidOperationException("profile not started");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `LiveDecoGate2.cs:89` | `if (a[1] != "on" && a[1] != "off") throw new ArgumentException("mode on&#124;off");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `LiveDecoGate2.cs:91` | `throw new InvalidOperationException("preparation requires paused matching renderer, outside FPS window");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `LiveDecoGate2.cs:95` | `throw new InvalidOperationException("invalid ABBA preparation song=" + song.ToString("F6", Inv) + " owned=" + LiveDecoRenderer.Owned);` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `LiveDecoGate2.cs:107` | `throw new InvalidOperationException("pixel requires paused, restored sample at requested song time");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `LiveDecoGate2.cs:118` | `if (window != -1) throw new InvalidOperationException("window already open");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `LiveDecoGate2.cs:120` | `if (a[1] != "on" && a[1] != "off") throw new ArgumentException("mode on&#124;off");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `LiveDecoGate2.cs:126` | `throw new InvalidOperationException("invalid ABBA window start index=" + nextWindow + " mode=" + a[1] + " song=" + nextSong.ToString("F6", Inv) + " paused=" + FrameCompare.Paused + " active=" + LiveDecoRenderer.Active + " ready=" + LiveDecoRenderer.Ready + " owned=" + LiveDecoRenderer.Owned + " warmup_s=" + LiveDecoRenderer.EnabledSeconds.ToString("F3", Inv) + " transition_ms=" + LiveDecoRenderer.TransitionMs.ToString("F6", Inv));` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `LiveDecoGate2.cs:140` | `if (window == -1) throw new InvalidOperationException("no FPS window");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `LiveDecoGate2.cs:166` | `throw new ArgumentException("livedeco2 reset&#124;profilebegin&#124;profileend&#124;ready on/off&#124;pixel 262/270/276&#124;begin on/off index&#124;end&#124;result");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `LiveDecoPolicy.cs:44` | `if (i > 0 && rows[i - 1].Key.CompareTo(a.Key) > 0) throw new ArgumentException("unsorted rows");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `LiveDecoRenderer.cs:160` | `if (methods.Length != 1) throw new MissingMethodException(type.Name + "." + name + " count=" + methods.Length);` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `LiveDecoRenderer.cs:173` | `throw new InvalidOperationException("livedeco requires playing legacy chart; competing diagnostics/engine off");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `LiveDecoRenderer.cs:174` | `if (GraphicsSettings.currentRenderPipeline != null) throw new InvalidOperationException("Built-in pipeline required");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `LiveDecoRenderer.cs:177` | `throw new InvalidOperationException("requires orthographic Z sorting and identity manager parent");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `LiveDecoRenderer.cs:178` | `if (DecoGpu.Active) throw new InvalidOperationException("competing owner");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `LiveDecoRenderer.cs:193` | `throw new InvalidOperationException("foreign decoration hook: " + method.Name);` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `LiveDecoRenderer.cs:246` | `if (ownedCount == 0) throw new InvalidOperationException("no eligible images; see rejection counts");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `LiveDecoRenderer.cs:449` | `throw new InvalidOperationException("sort-context-changed axis=" + sortAxisZ + "->" + axis.z + " camera_z=" + sortCameraZ + "->" + z + " mode=" + sortMode + "->" + mode + " mask=" + sortMask + "->" + camera.cullingMask);` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `LiveDecoRenderer.cs:557` | `if (reason != null) { Reject(i, reason); return; }` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `LiveDecoRenderer.cs:560` | `if (reason != null) { Reject(i, reason); return; }` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `LiveDecoRenderer.cs:564` | `if (reason != null) { Reject(i, reason); return; }` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `LiveDecoRenderer.cs:571` | `if (reason != null) { Reject(i, reason); return; }` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `LiveDecoRenderer.cs:575` | `if (reason != null) { Reject(i, reason); return; }` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `LiveDecoRenderer.cs:578` | `if (i.Corners.Length != 4 &#124;&#124; i.UV.Length != 4 &#124;&#124; i.Indices.Length != 6) { Reject(i, "nonquad"); return; }` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `LiveDecoRenderer.cs:582` | `if (i.Geometry == null) { Reject(i, "child-chain-unsupported"); return; }` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `LiveDecoRenderer.cs:610` | `if (!LiveDecoPolicy.WithinPixelError(vertexError, pixelsPerWorld)) { checkRejected++; Reject(i, "vertex-error-px"); return; }` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `LiveDecoRenderer.cs:611` | `if (!LiveDecoPolicy.WithinPixelError(boundsError, pixelsPerWorld)) { checkRejected++; Reject(i, "bounds-error-px"); return; }` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `LiveDecoRenderer.cs:631` | `if (!Finite(pixelsPerWorld) &#124;&#124; pixelsPerWorld <= 0) throw new InvalidOperationException("invalid screen pixel scale");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `LiveDecoRenderer.cs:667` | `throw new InvalidOperationException("nonfinite/out-of-fixed-bounds vertex");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `LiveDecoRenderer.cs:792` | `if (n >= ordered.Count &#124;&#124; !ReferenceEquals(ordered[n], row)) throw new InvalidOperationException("row index lost");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `LiveDecoRenderer.cs:853` | `if (s.Group) throw new InvalidOperationException("live SortingGroup not supported");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `LiveDecoRenderer.cs:865` | `if (!material &#124;&#124; material.renderQueue <= 2500) throw new InvalidOperationException("unsupported/missing live material");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `LiveDecoRenderer.cs:897` | `Release(tied.Image, "material-tie"); Reject(tied.Image, "material-tie"); tied.Owned = false;` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `LiveDecoRenderer.cs:937` | `throw new InvalidOperationException("run index lost");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `LiveDecoRenderer.cs:991` | `throw new InvalidOperationException("Float32 color vertex layout mismatch");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `LiveDecoRenderer.cs:1017` | `throw new InvalidOperationException("playback/lifecycle changed");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `LiveDecoRenderer.cs:1018` | `if (!SortSupported() &#124;&#124; !Identity(manager.transform)) throw new InvalidOperationException("sort/parent changed");` | 3.2~3.3·5~6: D/N 및 L 상태; 큼·그림/순서/자원 위험; CD/CL/CP |
| `LiveDecoRenderer.cs:1019` | `if (DecoGpu.Active &#124;&#124; MatShare.Active &#124;&#124; DecoEngine.Enabled) throw new InvalidOperationException("competing owner enabled");` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |
| `SceneRenderOrder.cs:25` | `if (float.IsNaN(distance) &#124;&#124; float.IsInfinity(distance)) throw new ArgumentException("nonfinite distance");` | 3.4 연구/명령 gate 유지; 제품 미지원 수에 합산 금지; 작음·검증 누락 위험·진단 때만 |
| `SceneReset.cs:40` | `DecoBake.StopGpuLive("SceneReset", false);` | 3.3~3.4: 정상 N backend/수명 또는 불변식 유지; 중간~큼·누락/자원 위험; CN/CP |

A.2 문장 수: 679. 단순 return false/continue 중 기능·자원 경로는 다음 A.3과 본문 3절에서 따로 설명한다. 구문 인벤토리는 분기 도달성 증명이나 실행 결과가 아니다.

### A.3 문자열 없이 제외하는 경로와 부가 기능

| 위치 | 현재 제외/내리기 조건 | 게임 동작 / 거부 없는 목적지 / 크기·위험·비용 |
|---|---|---|
| `GpuDecoExport.cs:939` | 표 Reasons 존재, GpuImageAdmit 실패, window mask=0 | 실제 장식/이미지/가시성은 게임 2절. 전체 등록 유지; window는 draw만 생략; L/N으로 조건·자산 처리 / 중간~큼·숨은 재활성 누락·C1/CL/CN |
| `GpuDecoExport.cs:951` | clamp-or-null | null은 정상 파괴인지 분리; clamp는 ScreenFixed T 또는 L/N / 중간·투영·CL |
| `GpuDecoExport.cs:970` | 4 vertices/4 UV/6 triangles 아님, Geometry.Capture null, trueScale/textureScale 0 | 원래 SpriteRenderer geometry와 영 크기 보존. D 일반 topology/최종행렬 L; 0 scale 복귀 때 재활성 / 큼·래스터/역행렬·CL/CD |
| `GpuDecoExport.cs:3026` | bake-unsupported, clamp-screen, oversized-world-quad, nonquad, child-chain (paused export 진단) | 게임 trueScale/실제 sprite는 2절. export 전용 25world 한계를 제품 제한으로 오인 금지; D 일반 topology/L 또는 N / 중간~큼·bounds·CL/CD |
| `GpuDecoImageVariants.cs:20` | NullArgument/Missing/Invalid/NonQuad/SplitAlpha 상태 | 게임 SetSprite(null)과 누락 자산 의미를 보존. empty actor, D 일반도형/외부alpha 또는 N / 큼·자산/재활성·C1/CD |
| `GpuDecoImagePlayback.cs:24` | UV unit-range, mipmapCount!=1, 준비 안 한 selector/address | 게임 원래 sprite/texture swap을 L 자산 버전+D/N; 교체 시 콜라이더 유지 / 큼·샘플/원복·C1/CD |
| `GpuDecoGpu.cs:26` | Supported 0..31 또는36, LiveSupported 0..36, raw stride/kernel/buffer 제약 | 게임 원래 Tween/ease는 L, 유효 GPU backend 실패는 N. FP64/precision·UNorm8 반올림은 유지 / 중간~큼·색/계산 차이·CL/CN |
| `GpuDecoShape.cs:139` | 형태 칸 없음/MaxRects 초과/keep 비율/ShapeMaxItems/kernel/버퍼용량/UV 모양 실패 | 장식 지원 거부가 아니라 선택적 도형 최적화 취소; 원래 quad D 유지. 형태 칸 raster/필터 여유 검증 / 중간·경계 손실·CD |
| `GpuDecoTrim.cs:40` | small/mips/kernel/cache/UV/코너/면적비율/완전투명 조건으로 trim 생략 | 원래 sprite geometry D 유지. alpha>0 bounds+sampling margin이 그림을 보존하는지 검증 / 중간·래스터 차이·CD |
| `GpuDecoLod.cs:46` | LOD off/빈구간/bundle 또는 clear shader 미지원/LodBlendOk 실패/MaxSpans48/배율 미달 | LOD만 포기하고 full resolution D 유지. 현재 bounds 합성/부분 clear는 이미 구현; 기능 누락 아님 / 중간·RT/합성·CD |
| `RunLod.cs:42` | Candidate shader/drawMode/mask/blend + 실제 수집 단계 필터, sleeping/수익성·후보 상한 | 통합 register에는 전부 유지, RunLod/원래 N 선택. Tiled 기능 기존 미커밋 변경은 기본off / 중간~큼·물리 layer/보임 지연·CN/CD |
| `GpuDecoDrawCollapseRenderer.cs:786` | DynamicOrderTick/ExcludedVisibilityTick가 장벽 키/visible 변화로 span 제외·복원 | 숨은 장벽이 새로 켜짐은 정상 게임 동작. L dirty→bucket 재계획/N, 전체 draw 순서 유지 / 큼·한 프레임 오순서·C1/CP |
| `GpuDecoCopyMask.cs:1` | 알파/활성 여부에 따른 native copy chunk mask, 연구 bypass | 원래 상태가 숨은 경우만 그리기/복사 생략. L 변경·mask·충돌 소비자는 별도 유지; chunk에 하나라도 필요하면 복사 / 중간·stale vertex·C1/CD |
| `GpuDecoAlphaPolicy.cs:1` | alpha earlyout/정적 투명 span 또는 independent renderer cull | 그림 생략만 하고 actor/logic을 없애지 않음. shader alpha 반올림/음수/색 range 및 마스크 예외 대조 / 중간·잘못 숨김·C1/CD |

### A.4 compute 및 Python 생성 경로의 범위

`tools/gpudeco-lab/GpuDeco.compute:24`의 LiveChannel/SourceE, :47의 ParallaxWorld, :392의 CollapseMap을 확인했다. T와 L 논리 채널은 같은 버퍼를 재사용할 수 있지만 최종행렬 override는 별도 형식이 필요하다. AlphaBounds/AlphaCells는 형상 최적화이고 mask/particle 시뮬레이션 대체가 아니다. `tools/gpudeco-lab/GpuDecoLod.compute:1`은 화면 확대율 최소값·가시 bounds를 계산하며 그 결과만으로 임의 다른 blend/mask 사이 합성을 허용하지 않는다.

이 폴더의 Python은 `generate-drawcollapse-shader.py`와 `check-shader.py` 두 개다. 전자는 DrawCollapsePolicy에서 배열/직접 슬롯 수를 읽어 shader 분기/Clamp sampler를 생성하므로 생성 shader만 수정하면 다음 생성 때 되돌아간다(해당 파일:7, :16). 후자는 D3DCompile로 일부 kernel만 구문 확인하는 오프라인 도구다(:1, :23). 게임 지원·이미지 동일성·새 collapse/LOD kernel 전체·성능 검증이 아니며 이번 지시에 따라 둘 다 실행하지 않았다.

## 부록 B. 게임 선언 필드·이벤트/Setup 입력 키 인벤토리

선언 위치를 전부 남긴 검색 인덱스다. 필드 행의 현재 대응/경로는 2절의 기능표를 세분한 것이며, 참조/편집용 필드도 삭제해도 된다는 뜻이 아니다. 메서드 내부 로컬 변수는 포함하지 않는다. public/private/protected 필드와 자동 속성, expression property를 수집했고 메서드 본문은 2~6절의 동작 근거에서 다뤘다.

### B.1 scrDecoration.cs

| 위치 | 선언 원문 | 현재 대응 / 설계 목적지 |
|---|---|---|
| `G/scrDecoration.cs:25` | `private static readonly Color hoverColor = Color.cyan;` | 현재 16채널 T; L도 같은 값 공급 가능, 소비자 Transform 유지; 2.2·4 |
| `G/scrDecoration.cs:28` | `public Vector2 startPos;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrDecoration.cs:30` | `public scrPlanet followPlanet;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrDecoration.cs:32` | `public Transform pivotTrans;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrDecoration.cs:34` | `public Vector2 pivotPosVec;` | 현재 16채널 T; L도 같은 값 공급 가능, 소비자 Transform 유지; 2.2·4 |
| `G/scrDecoration.cs:36` | `public Vector2 pivotOffsetVec;` | 현재 16채널 T; L도 같은 값 공급 가능, 소비자 Transform 유지; 2.2·4 |
| `G/scrDecoration.cs:38` | `public float startRot;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrDecoration.cs:40` | `public float rotAngle;` | 현재 16채널 T; L도 같은 값 공급 가능, 소비자 Transform 유지; 2.2·4 |
| `G/scrDecoration.cs:42` | `public bool lockRotation;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrDecoration.cs:44` | `public Vector2 scaleVec;` | 현재 16채널 T; L도 같은 값 공급 가능, 소비자 Transform 유지; 2.2·4 |
| `G/scrDecoration.cs:46` | `public float scaleMultiplier = 1f;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrDecoration.cs:48` | `public Vector2 textureScaleMultiplier = Vector2.one;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/scrDecoration.cs:50` | `public Vector2 croppedPivotOffset;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrDecoration.cs:52` | `public bool lockScale;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrDecoration.cs:55` | `public Transform childTransform;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrDecoration.cs:57` | `public Color color;` | 현재 16채널 T; L도 같은 값 공급 가능, 소비자 Transform 유지; 2.2·4 |
| `G/scrDecoration.cs:59` | `public float opacity = 1f;` | 현재 16채널 T; L도 같은 값 공급 가능, 소비자 Transform 유지; 2.2·4 |
| `G/scrDecoration.cs:61` | `public bool syncFloorDepth;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrDecoration.cs:63` | `public scrParallax parallax;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrDecoration.cs:65` | `public Vector2 parallaxOffset;` | 현재 16채널 T; L도 같은 값 공급 가능, 소비자 Transform 유지; 2.2·4 |
| `G/scrDecoration.cs:67` | `public Dictionary<TweenType, Tween> eventTweens = new Dictionary<TweenType, Tween>();` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrDecoration.cs:70` | `public GameObject selectionBordersObject;` | 편집기 상태/장벽, 현재 editor-borders 제외; N 유지; 2.2 |
| `G/scrDecoration.cs:72` | `public GameObject hitboxBordersObject;` | 현재 충돌/조건 거부; 원래 logic/물리 유지 L/N; 2.2·4 |
| `G/scrDecoration.cs:75` | `public Vector2 cachedBorderSize;` | 편집기 상태/장벽, 현재 editor-borders 제외; N 유지; 2.2 |
| `G/scrDecoration.cs:78` | `public LevelEvent sourceLevelEvent;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrDecoration.cs:80` | `public DecPlacementType placementType;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrDecoration.cs:82` | `public bool stickToFloor;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrDecoration.cs:85` | `public string decorationTag;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrDecoration.cs:88` | `public bool forceHide;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrDecoration.cs:91` | `public bool forceLock;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrDecoration.cs:94` | `public bool hasConditionalChange;` | 현재 충돌/조건 거부; 원래 logic/물리 유지 L/N; 2.2·4 |
| `G/scrDecoration.cs:97` | `public List<ffxPlusBase> hitEffects = new List<ffxPlusBase>();` | 현재 충돌/조건 거부; 원래 logic/물리 유지 L/N; 2.2·4 |
| `G/scrDecoration.cs:100` | `public Vector2 scaleAtDragStart;` | 편집기 상태/장벽, 현재 editor-borders 제외; N 유지; 2.2 |
| `G/scrDecoration.cs:103` | `public Vector2 sizeAtDragStart;` | 편집기 상태/장벽, 현재 editor-borders 제외; N 유지; 2.2 |
| `G/scrDecoration.cs:106` | `public HitboxType hitbox;` | 현재 충돌/조건 거부; 원래 logic/물리 유지 L/N; 2.2·4 |
| `G/scrDecoration.cs:108` | `public HitboxDetectTarget hitboxDetectTarget;` | 현재 충돌/조건 거부; 원래 logic/물리 유지 L/N; 2.2·4 |
| `G/scrDecoration.cs:110` | `public HitboxTargetPlanet hitboxTargetPlanet;` | 현재 충돌/조건 거부; 원래 logic/물리 유지 L/N; 2.2·4 |
| `G/scrDecoration.cs:112` | `public HashSet<string> hitboxDecoTags;` | 현재 충돌/조건 거부; 원래 logic/물리 유지 L/N; 2.2·4 |
| `G/scrDecoration.cs:114` | `public HitboxTriggerType hitboxTriggerType;` | 현재 충돌/조건 거부; 원래 logic/물리 유지 L/N; 2.2·4 |
| `G/scrDecoration.cs:116` | `public float hitboxRepeatInterval;` | 현재 충돌/조건 거부; 원래 logic/물리 유지 L/N; 2.2·4 |
| `G/scrDecoration.cs:118` | `public bool hitOnce;` | 현재 충돌/조건 거부; 원래 logic/물리 유지 L/N; 2.2·4 |
| `G/scrDecoration.cs:120` | `public int lastHitFrame;` | 현재 충돌/조건 거부; 원래 logic/물리 유지 L/N; 2.2·4 |
| `G/scrDecoration.cs:122` | `public float lastTriggerTime;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrDecoration.cs:124` | `public List<string> hitboxEventTags = new List<string>();` | 현재 충돌/조건 거부; 원래 logic/물리 유지 L/N; 2.2·4 |
| `G/scrDecoration.cs:127` | `public List<ffxPlusBase> hitboxEvents = new List<ffxPlusBase>();` | 현재 충돌/조건 거부; 원래 logic/물리 유지 L/N; 2.2·4 |
| `G/scrDecoration.cs:130` | `public BoxCollider2D editorCollider;` | 현재 충돌/조건 거부; 원래 logic/물리 유지 L/N; 2.2·4 |
| `G/scrDecoration.cs:132` | `public BoxCollider2D damageBox;` | 현재 충돌/조건 거부; 원래 logic/물리 유지 L/N; 2.2·4 |
| `G/scrDecoration.cs:134` | `public CircleCollider2D damageCircle;` | 현재 충돌/조건 거부; 원래 logic/물리 유지 L/N; 2.2·4 |
| `G/scrDecoration.cs:136` | `public CapsuleCollider2D damageCapsule;` | 현재 충돌/조건 거부; 원래 logic/물리 유지 L/N; 2.2·4 |
| `G/scrDecoration.cs:138` | `public Collider2D activeCollider;` | 현재 충돌/조건 거부; 원래 logic/물리 유지 L/N; 2.2·4 |
| `G/scrDecoration.cs:140` | `public Hitbox hitboxType;` | 현재 충돌/조건 거부; 원래 logic/물리 유지 L/N; 2.2·4 |
| `G/scrDecoration.cs:142` | `public float hitboxRotation;` | 현재 충돌/조건 거부; 원래 logic/물리 유지 L/N; 2.2·4 |
| `G/scrDecoration.cs:144` | `public Vector2 hitboxScale = Vector2.one;` | 현재 충돌/조건 거부; 원래 logic/물리 유지 L/N; 2.2·4 |
| `G/scrDecoration.cs:146` | `public Vector2 hitboxOffset = Vector2.zero;` | 현재 충돌/조건 거부; 원래 logic/물리 유지 L/N; 2.2·4 |
| `G/scrDecoration.cs:148` | `public bool canHitPlanets;` | 현재 충돌/조건 거부; 원래 logic/물리 유지 L/N; 2.2·4 |
| `G/scrDecoration.cs:151` | `public int parentFloorNum = -1;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrDecoration.cs:153` | `public scrFloor parentFloor;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrDecoration.cs:156` | `public HashSet<string> tags;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrDecoration.cs:159` | `public DecorationType decType;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrDecoration.cs:162` | `public scrDecorationManager manager;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrDecoration.cs:164` | `protected Color rendererColor = Color.white;` | 현재 16채널 T; L도 같은 값 공급 가능, 소비자 Transform 유지; 2.2·4 |
| `G/scrDecoration.cs:166` | `protected bool rendererEnabled = true;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/scrDecoration.cs:168` | `public MonoBehaviour[] cfpCache;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/scrDecoration.cs:170` | `public virtual string gameObjectName => "";` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrDecoration.cs:172` | `public virtual string decorationName => "<i>none</i>";` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrDecoration.cs:174` | `public bool useHitbox => hitbox != HitboxType.None;` | 현재 충돌/조건 거부; 원래 logic/물리 유지 L/N; 2.2·4 |
| `G/scrDecoration.cs:176` | `public Vector2 trueScaleMultiplier => scaleMultiplier * textureScaleMultiplier;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/scrDecoration.cs:178` | `public Vector2 camScaleMultiplier => (lockScale ? (ADOBase.controller.camy.camobj.orthographicSize * 0.2f / (ADOBase.customLevel.levelData.camZoom / 100f)) : 1f) * trueScaleMultiplier;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |

Setup/Decode에서 참조하는 문자열 키(같은 줄의 enum 해석용 문자열도 포함):

| 키 | 최초 위치 | 현재 대응 / 목적지 |
|---|---|---|
| `</noparse>` | `G/scrDecoration.cs:663` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `<noparse>` | `G/scrDecoration.cs:663` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `autoPlay` | `G/scrDecoration.cs:912` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `blendMode` | `G/scrDecoration.cs:611` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `bubbleAppearEndOffset` | `G/scrDecoration.cs:642` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `bubbleAppearStartOffset` | `G/scrDecoration.cs:641` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `bubbleDisappearOffset` | `G/scrDecoration.cs:643` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `bubbleSpawnOffset` | `G/scrDecoration.cs:644` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `color` | `G/scrDecoration.cs:687` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `components` | `G/scrDecoration.cs:665` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `cropPivotOffset` | `G/scrDecoration.cs:610` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `decorationImage` | `G/scrDecoration.cs:601` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `decText` | `G/scrDecoration.cs:767` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `depth` | `G/scrDecoration.cs:669` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `failHitboxOffset` | `G/scrDecoration.cs:836` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `failHitboxRotation` | `G/scrDecoration.cs:837` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `failHitboxScale` | `G/scrDecoration.cs:832` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `failHitboxType` | `G/scrDecoration.cs:831` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `font` | `G/scrDecoration.cs:768` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `hitbox` | `G/scrDecoration.cs:812` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `hitboxDecoTag` | `G/scrDecoration.cs:845` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `hitboxDetectTarget` | `G/scrDecoration.cs:838` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `hitboxEventTag` | `G/scrDecoration.cs:817` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `hitboxRepeatInterval` | `G/scrDecoration.cs:841` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `hitboxTargetPlanet` | `G/scrDecoration.cs:839` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `hitboxTriggerType` | `G/scrDecoration.cs:840` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `imageSmoothing` | `G/scrDecoration.cs:608` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `lockRotation` | `G/scrDecoration.cs:673` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `lockScale` | `G/scrDecoration.cs:674` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `maskingBackDepth` | `G/scrDecoration.cs:616` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `maskingFrontDepth` | `G/scrDecoration.cs:615` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `maskingTarget` | `G/scrDecoration.cs:613` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `maskingType` | `G/scrDecoration.cs:612` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `NO TAG` | `G/scrDecoration.cs:613` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `objectType` | `G/scrDecoration.cs:620` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `opacity` | `G/scrDecoration.cs:688` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `originalSize` | `G/scrDecoration.cs:609` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `parallax` | `G/scrDecoration.cs:670` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `parallaxOffset` | `G/scrDecoration.cs:672` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `pivotOffset` | `G/scrDecoration.cs:702` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `planetColor` | `G/scrDecoration.cs:622` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `planetColorType` | `G/scrDecoration.cs:621` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `planetTailColor` | `G/scrDecoration.cs:623` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `position` | `G/scrDecoration.cs:524` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `relativeTo` | `G/scrDecoration.cs:699` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `rotation` | `G/scrDecoration.cs:667` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `scale` | `G/scrDecoration.cs:668` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `scaleMultiplier` | `G/scrDecoration.cs:678` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `secondaryTrackColor` | `G/scrDecoration.cs:627` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `stickToFloor` | `G/scrDecoration.cs:675` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `syncFloorDepth` | `G/scrDecoration.cs:676` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `tag` | `G/scrDecoration.cs:666` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `tile` | `G/scrDecoration.cs:607` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `trackAngle` | `G/scrDecoration.cs:625` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `trackColor` | `G/scrDecoration.cs:626` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `trackColorAnimDuration` | `G/scrDecoration.cs:629` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `trackColorType` | `G/scrDecoration.cs:628` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `trackGlowColor` | `G/scrDecoration.cs:633` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `trackGlowEnabled` | `G/scrDecoration.cs:632` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `trackGraySetSpeedIcon` | `G/scrDecoration.cs:638` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `trackIcon` | `G/scrDecoration.cs:634` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `trackIconAngle` | `G/scrDecoration.cs:635` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `trackIconFlipped` | `G/scrDecoration.cs:636` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `trackIconOutlines` | `G/scrDecoration.cs:640` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `trackOpacity` | `G/scrDecoration.cs:630` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `trackRedSwirl` | `G/scrDecoration.cs:637` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `trackSetSpeedIconBpm` | `G/scrDecoration.cs:639` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `trackStyle` | `G/scrDecoration.cs:631` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `trackType` | `G/scrDecoration.cs:624` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `useMaskingDepth` | `G/scrDecoration.cs:614` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |

### B.2 scrVisualDecoration.cs

| 위치 | 선언 원문 | 현재 대응 / 설계 목적지 |
|---|---|---|
| `G/scrVisualDecoration.cs:10` | `public SpriteRenderer spriteRenderer;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/scrVisualDecoration.cs:12` | `public MeshRenderer meshRenderer;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/scrVisualDecoration.cs:14` | `public GameObject meshRendererObj;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/scrVisualDecoration.cs:16` | `private Material meshRendererMat;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/scrVisualDecoration.cs:18` | `public BlendModeEffect blendModeEffect;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/scrVisualDecoration.cs:20` | `public BlendModeEffect meshBlendModeEffect;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/scrVisualDecoration.cs:22` | `public SpriteMask spriteMask;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/scrVisualDecoration.cs:24` | `public SpriteAlphaMask spriteAlphaMask;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/scrVisualDecoration.cs:27` | `public List<SpriteAlphaMask> alphaMaskCache;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/scrVisualDecoration.cs:30` | `public RenderTexture spareRT1;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/scrVisualDecoration.cs:33` | `public RenderTexture spareRT2;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/scrVisualDecoration.cs:36` | `public SpriteRenderer bordersRenderer;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/scrVisualDecoration.cs:40` | `public SpriteRenderer hitboxRenderer;` | 현재 충돌/조건 거부; 원래 logic/물리 유지 L/N; 2.2·4 |
| `G/scrVisualDecoration.cs:43` | `public float repeatX = 1f;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/scrVisualDecoration.cs:46` | `public float repeatY = 1f;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/scrVisualDecoration.cs:48` | `private DecorationBlendMode blendMode;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/scrVisualDecoration.cs:50` | `private MaskingType maskingType;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/scrVisualDecoration.cs:52` | `private TextureManager.CustomSprite _sprite;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/scrVisualDecoration.cs:54` | `public bool smoothing;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/scrVisualDecoration.cs:56` | `private bool meshRendererEnabled;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/scrVisualDecoration.cs:58` | `public Vector2 spriteUnscaledSize { get; private set; }` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/scrVisualDecoration.cs:60` | `public override string gameObjectName => sourceLevelEvent.GetString("decorationImage")?.RemoveRichTags();` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrVisualDecoration.cs:62` | `public override string decorationName => gameObjectName.NullIfEmpty() ?? PropertyControl_DecorationsList.stringItemNoImage;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |

Setup/Decode에서 참조하는 문자열 키(같은 줄의 enum 해석용 문자열도 포함):

| 키 | 최초 위치 | 현재 대응 / 목적지 |
|---|---|---|
| `decorationImage` | `G/scrVisualDecoration.cs:60` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |

### B.3 scrTextDecoration.cs

| 위치 | 선언 원문 | 현재 대응 / 설계 목적지 |
|---|---|---|
| `G/scrTextDecoration.cs:10` | `public Text text;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrTextDecoration.cs:12` | `public Canvas canvas;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrTextDecoration.cs:15` | `public Image bordersRenderer;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrTextDecoration.cs:18` | `public Vector2 bordersSetSize;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrTextDecoration.cs:20` | `private int startingSize;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrTextDecoration.cs:22` | `private float startingSpacing;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrTextDecoration.cs:24` | `private int startingResizeTextMaxSize;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrTextDecoration.cs:26` | `private int startingResizeTextMinSize;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrTextDecoration.cs:28` | `private FontData fontData;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrTextDecoration.cs:30` | `private float width;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrTextDecoration.cs:32` | `private float height;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrTextDecoration.cs:34` | `public static Dictionary<FontName, Font> nameToFont;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrTextDecoration.cs:36` | `public override string gameObjectName => sourceLevelEvent.GetString("decText")?.RemoveRichTags();` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrTextDecoration.cs:38` | `public override string decorationName => gameObjectName.NullIfEmpty() ?? PropertyControl_DecorationsList.stringItemNoText;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |

Setup/Decode에서 참조하는 문자열 키(같은 줄의 enum 해석용 문자열도 포함):

| 키 | 최초 위치 | 현재 대응 / 목적지 |
|---|---|---|
| `decText` | `G/scrTextDecoration.cs:36` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |

### B.4 scrParticleDecoration.cs

| 위치 | 선언 원문 | 현재 대응 / 설계 목적지 |
|---|---|---|
| `G/scrParticleDecoration.cs:8` | `public ParticleSystem particleSystem;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrParticleDecoration.cs:10` | `public Transform particleTransform;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrParticleDecoration.cs:12` | `public SpriteRenderer selectionBorders;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrParticleDecoration.cs:14` | `public GameObject gizmo;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrParticleDecoration.cs:16` | `public Transform gizmoTransform;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrParticleDecoration.cs:18` | `public bool isPreview;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrParticleDecoration.cs:20` | `public Vector2 scale;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrParticleDecoration.cs:22` | `public ParticleSystem.MinMaxGradient colorGradient;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrParticleDecoration.cs:24` | `public override string gameObjectName => sourceLevelEvent.GetString("decorationImage")?.RemoveRichTags();` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrParticleDecoration.cs:39` | `public ParticleSystemRenderer particleRenderer => particleSystem.GetComponent<ParticleSystemRenderer>();` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrParticleDecoration.cs:41` | `public bool autoPlay { get; set; }` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrParticleDecoration.cs:43` | `public bool atStart { get; set; }` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrParticleDecoration.cs:45` | `public float simulationSpeed { get; set; }` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |

Setup/Decode에서 참조하는 문자열 키(같은 줄의 enum 해석용 문자열도 포함):

| 키 | 최초 위치 | 현재 대응 / 목적지 |
|---|---|---|
| `arc` | `G/scrParticleDecoration.cs:170` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `arcMode` | `G/scrParticleDecoration.cs:171` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `color` | `G/scrParticleDecoration.cs:155` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `colorOverLifetime` | `G/scrParticleDecoration.cs:188` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `decorationImage` | `G/scrParticleDecoration.cs:24` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `emissionRate` | `G/scrParticleDecoration.cs:173` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `loop` | `G/scrParticleDecoration.cs:153` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `maxParticles` | `G/scrParticleDecoration.cs:158` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `particleLifetime` | `G/scrParticleDecoration.cs:159` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `particleSize` | `G/scrParticleDecoration.cs:160` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `playDuration` | `G/scrParticleDecoration.cs:151` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `randomSeed` | `G/scrParticleDecoration.cs:149` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `randomTextureTiling` | `G/scrParticleDecoration.cs:200` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `rotationOverTime` | `G/scrParticleDecoration.cs:194` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `scale` | `G/scrParticleDecoration.cs:169` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `shapeRadius` | `G/scrParticleDecoration.cs:168` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `shapeType` | `G/scrParticleDecoration.cs:162` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `simulationSpace` | `G/scrParticleDecoration.cs:205` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `simulationSpeed` | `G/scrParticleDecoration.cs:157` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `sizeOverLifetime` | `G/scrParticleDecoration.cs:184` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `startRotation` | `G/scrParticleDecoration.cs:204` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `velocity` | `G/scrParticleDecoration.cs:176` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `velocityLimitOverLifetime` | `G/scrParticleDecoration.cs:182` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |

### B.5 scrObjectDecoration.cs

| 위치 | 선언 원문 | 현재 대응 / 설계 목적지 |
|---|---|---|
| `G/scrObjectDecoration.cs:9` | `public GameObject planetObject;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrObjectDecoration.cs:11` | `public GameObject floorObject;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrObjectDecoration.cs:13` | `public GameObject bubbleObject;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrObjectDecoration.cs:15` | `public PlanetRenderer planetRenderer;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrObjectDecoration.cs:17` | `public scrFloor floor;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrObjectDecoration.cs:19` | `public PlayerBubble bubble;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrObjectDecoration.cs:22` | `private GameObject activeObject;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrObjectDecoration.cs:25` | `public PolygonCollider2D polygonCollider;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrObjectDecoration.cs:27` | `private Collider2D activeEditorCollider;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrObjectDecoration.cs:30` | `public ObjectDecorationType objectType;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrObjectDecoration.cs:32` | `public Dictionary<ObjectDecorationTweenType, Tween> objectTweens = new Dictionary<ObjectDecorationTweenType, Tween>();` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrObjectDecoration.cs:35` | `public SpriteRenderer bordersRenderer;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrObjectDecoration.cs:38` | `public Vector2 bordersScaleMultiplier = Vector2.one;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrObjectDecoration.cs:40` | `public PlanetDecorationColorType planetColorType;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrObjectDecoration.cs:42` | `public Color planetTailColor = Color.white;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrObjectDecoration.cs:44` | `public Color floorGlowColor = Color.white;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrObjectDecoration.cs:46` | `public CustomFloorIcon floorCustomIcon;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrObjectDecoration.cs:48` | `public float floorIconAngle;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrObjectDecoration.cs:50` | `public int appearStartOffset = 8;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrObjectDecoration.cs:52` | `public int appearEndOffset = 2;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrObjectDecoration.cs:54` | `public int disappearOffset = 4;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrObjectDecoration.cs:56` | `public int spawnOffset = 3;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrObjectDecoration.cs:58` | `public override string gameObjectName => sourceLevelEvent["objectType"]?.ToString().RemoveRichTags() ?? "";` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |

Setup/Decode에서 참조하는 문자열 키(같은 줄의 enum 해석용 문자열도 포함):

| 키 | 최초 위치 | 현재 대응 / 목적지 |
|---|---|---|
| `objectType` | `G/scrObjectDecoration.cs:58` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |

### B.6 scrPrefabDecoration.cs

| 위치 | 선언 원문 | 현재 대응 / 설계 목적지 |
|---|---|---|
| `G/scrPrefabDecoration.cs:6` | `public PublicPrefabType prefabType;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrPrefabDecoration.cs:8` | `public override string gameObjectName => sourceLevelEvent.GetString("decorationImage")?.RemoveRichTags();` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/scrPrefabDecoration.cs:10` | `public override string decorationName => "<color=#9D00FF>" + gameObjectName.Replace("prefab:", "", StringComparison.OrdinalIgnoreCase) + "</color>";` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |

Setup/Decode에서 참조하는 문자열 키(같은 줄의 enum 해석용 문자열도 포함):

| 키 | 최초 위치 | 현재 대응 / 목적지 |
|---|---|---|
| `decorationImage` | `G/scrPrefabDecoration.cs:8` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |

### B.7 scrDecorationManager.cs

| 위치 | 선언 원문 | 현재 대응 / 설계 목적지 |
|---|---|---|
| `G/scrDecorationManager.cs:10` | `public static int ShaderProperty_RepeatX;` | 현재 snapshot/태그/소유 일부; 전체 등록·수명·카메라별 N/D 계획; 2.1·4 |
| `G/scrDecorationManager.cs:12` | `public static int ShaderProperty_RepeatY;` | 현재 snapshot/태그/소유 일부; 전체 등록·수명·카메라별 N/D 계획; 2.1·4 |
| `G/scrDecorationManager.cs:14` | `public static int ShaderProperty_MainTex;` | 현재 snapshot/태그/소유 일부; 전체 등록·수명·카메라별 N/D 계획; 2.1·4 |
| `G/scrDecorationManager.cs:16` | `public static int ShaderProperty_Color;` | 현재 snapshot/태그/소유 일부; 전체 등록·수명·카메라별 N/D 계획; 2.1·4 |
| `G/scrDecorationManager.cs:18` | `public static int ShaderProperty_Opacity;` | 현재 snapshot/태그/소유 일부; 전체 등록·수명·카메라별 N/D 계획; 2.1·4 |
| `G/scrDecorationManager.cs:20` | `public static int ShaderProperty_Tile;` | 현재 snapshot/태그/소유 일부; 전체 등록·수명·카메라별 N/D 계획; 2.1·4 |
| `G/scrDecorationManager.cs:22` | `private static scrDecorationManager _instance;` | 현재 snapshot/태그/소유 일부; 전체 등록·수명·카메라별 N/D 계획; 2.1·4 |
| `G/scrDecorationManager.cs:24` | `public static Shader tileShader;` | 현재 snapshot/태그/소유 일부; 전체 등록·수명·카메라별 N/D 계획; 2.1·4 |
| `G/scrDecorationManager.cs:26` | `public static Shader visualDecoShader;` | 현재 snapshot/태그/소유 일부; 전체 등록·수명·카메라별 N/D 계획; 2.1·4 |
| `G/scrDecorationManager.cs:28` | `public Dictionary<string, List<scrDecoration>> taggedDecorations;` | 현재 snapshot/태그/소유 일부; 전체 등록·수명·카메라별 N/D 계획; 2.1·4 |
| `G/scrDecorationManager.cs:30` | `public List<scrDecoration> allDecorations;` | 현재 snapshot/태그/소유 일부; 전체 등록·수명·카메라별 N/D 계획; 2.1·4 |
| `G/scrDecorationManager.cs:32` | `public HashSet<string> hitboxEventTags = new HashSet<string>();` | 현재 snapshot/태그/소유 일부; 전체 등록·수명·카메라별 N/D 계획; 2.1·4 |
| `G/scrDecorationManager.cs:34` | `public Dictionary<string, List<scrDecoration>> hitboxEventTagDecorations;` | 현재 snapshot/태그/소유 일부; 전체 등록·수명·카메라별 N/D 계획; 2.1·4 |
| `G/scrDecorationManager.cs:36` | `public Dictionary<Collider2D, scrDecoration> hitboxCollidersToDecorations = new Dictionary<Collider2D, scrDecoration>();` | 현재 snapshot/태그/소유 일부; 전체 등록·수명·카메라별 N/D 계획; 2.1·4 |
| `G/scrDecorationManager.cs:39` | `public TextureManager imageHolder;` | 현재 snapshot/태그/소유 일부; 전체 등록·수명·카메라별 N/D 계획; 2.1·4 |
| `G/scrDecorationManager.cs:42` | `public Sprite defaultSprite;` | 현재 snapshot/태그/소유 일부; 전체 등록·수명·카메라별 N/D 계획; 2.1·4 |
| `G/scrDecorationManager.cs:44` | `public Sprite notFoundSprite;` | 현재 snapshot/태그/소유 일부; 전체 등록·수명·카메라별 N/D 계획; 2.1·4 |
| `G/scrDecorationManager.cs:47` | `public GameObject prefab_visualDecoration;` | 현재 snapshot/태그/소유 일부; 전체 등록·수명·카메라별 N/D 계획; 2.1·4 |
| `G/scrDecorationManager.cs:49` | `public GameObject prefab_textDecoration;` | 현재 snapshot/태그/소유 일부; 전체 등록·수명·카메라별 N/D 계획; 2.1·4 |
| `G/scrDecorationManager.cs:51` | `public GameObject prefab_prefabDecoration;` | 현재 snapshot/태그/소유 일부; 전체 등록·수명·카메라별 N/D 계획; 2.1·4 |
| `G/scrDecorationManager.cs:53` | `public GameObject prefab_objectDecoration;` | 현재 snapshot/태그/소유 일부; 전체 등록·수명·카메라별 N/D 계획; 2.1·4 |
| `G/scrDecorationManager.cs:55` | `public GameObject prefab_particleDecoration;` | 현재 snapshot/태그/소유 일부; 전체 등록·수명·카메라별 N/D 계획; 2.1·4 |
| `G/scrDecorationManager.cs:58` | `public LevelEvent hoveredDecoration;` | 현재 snapshot/태그/소유 일부; 전체 등록·수명·카메라별 N/D 계획; 2.1·4 |
| `G/scrDecorationManager.cs:60` | `private Dictionary<string, List<scrVisualDecoration>> decorationsWithSameTexture;` | 현재 snapshot/태그/소유 일부; 전체 등록·수명·카메라별 N/D 계획; 2.1·4 |
| `G/scrDecorationManager.cs:62` | `private HashSet<string> textureKeysToUpdate = new HashSet<string>();` | 현재 snapshot/태그/소유 일부; 전체 등록·수명·카메라별 N/D 계획; 2.1·4 |
| `G/scrDecorationManager.cs:64` | `private HashSet<scrVisualDecoration> decorationsToUpdateAlphaMaskCache = new HashSet<scrVisualDecoration>();` | 현재 snapshot/태그/소유 일부; 전체 등록·수명·카메라별 N/D 계획; 2.1·4 |
| `G/scrDecorationManager.cs:66` | `public Dictionary<string, MethodInfo> filterOnRenderImageCache = new Dictionary<string, MethodInfo>();` | 현재 snapshot/태그/소유 일부; 전체 등록·수명·카메라별 N/D 계획; 2.1·4 |

Setup/Decode에서 참조하는 문자열 키(같은 줄의 enum 해석용 문자열도 포함):

| 키 | 최초 위치 | 현재 대응 / 목적지 |
|---|---|---|
| `decorationImage` | `G/scrDecorationManager.cs:227` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `decText` | `G/scrDecorationManager.cs:249` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `objectType` | `G/scrDecorationManager.cs:261` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |

### B.8 scrParallax.cs

| 위치 | 선언 원문 | 현재 대응 / 설계 목적지 |
|---|---|---|
| `G/scrParallax.cs:6` | `public Vector3 posCamAtStart;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrParallax.cs:8` | `public float multiplier_x;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrParallax.cs:10` | `public float multiplier_y;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrParallax.cs:13` | `public Transform cameraTransform;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrParallax.cs:16` | `public bool dontAlterX;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrParallax.cs:19` | `public bool dontAlterY;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrParallax.cs:21` | `public bool clampToScreen;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrParallax.cs:23` | `public Vector2 screenRelativePos;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrParallax.cs:25` | `private Vector2 startPosition;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrParallax.cs:27` | `private Vector2 endPosition;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrParallax.cs:29` | `private Transform cachedTransform;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrParallax.cs:31` | `private bool isDecoration;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrParallax.cs:34` | `public scrDecoration decoration;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/scrParallax.cs:36` | `private bool inited;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |

### B.9 SpriteAlphaMask.cs

| 위치 | 선언 원문 | 현재 대응 / 설계 목적지 |
|---|---|---|
| `G/SpriteAlphaMask.cs:5` | `public static int ShaderProperty_Texture1 = Shader.PropertyToID("_Texture1");` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:7` | `public static int ShaderProperty_DisableTexture1 = Shader.PropertyToID("_DisableTexture1");` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:9` | `public static int ShaderProperty_Texture2 = Shader.PropertyToID("_Texture2");` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:11` | `public static int ShaderProperty_Texture2Scale = Shader.PropertyToID("_Texture2Scale");` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:13` | `public static int ShaderProperty_Texture2Pos = Shader.PropertyToID("_Texture2Pos");` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:15` | `public static int ShaderProperty_Texture2Rot = Shader.PropertyToID("_Texture2Rot");` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:17` | `public static int ShaderProperty_Texture3 = Shader.PropertyToID("_Texture3");` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:19` | `public static int ShaderProperty_Texture3Scale = Shader.PropertyToID("_Texture3Scale");` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:21` | `public static int ShaderProperty_Texture3Pos = Shader.PropertyToID("_Texture3Pos");` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:23` | `public static int ShaderProperty_Texture3Rot = Shader.PropertyToID("_Texture3Rot");` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:25` | `public static int ShaderProperty_DisableTexture3 = Shader.PropertyToID("_DisableTexture3");` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:27` | `public static int ShaderProperty_Texture4 = Shader.PropertyToID("_Texture4");` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:29` | `public static int ShaderProperty_Texture4Scale = Shader.PropertyToID("_Texture4Scale");` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:31` | `public static int ShaderProperty_Texture4Pos = Shader.PropertyToID("_Texture4Pos");` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:33` | `public static int ShaderProperty_Texture4Rot = Shader.PropertyToID("_Texture4Rot");` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:35` | `public static int ShaderProperty_DisableTexture4 = Shader.PropertyToID("_DisableTexture4");` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:37` | `public static int ShaderProperty_Texture5 = Shader.PropertyToID("_Texture5");` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:39` | `public static int ShaderProperty_Texture5Scale = Shader.PropertyToID("_Texture5Scale");` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:41` | `public static int ShaderProperty_Texture5Pos = Shader.PropertyToID("_Texture5Pos");` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:43` | `public static int ShaderProperty_Texture5Rot = Shader.PropertyToID("_Texture5Rot");` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:45` | `public static int ShaderProperty_DisableTexture5 = Shader.PropertyToID("_DisableTexture5");` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:47` | `public static int ShaderProperty_RGB = Shader.PropertyToID("_MainTex");` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:49` | `public static int ShaderProperty_A = Shader.PropertyToID("_AlphaTex");` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:51` | `public static int ShaderProperty_ARot = Shader.PropertyToID("_ARot");` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:53` | `public static int ShaderProperty_VisibleOutsideMask = Shader.PropertyToID("_VisibleOutsideMask");` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:55` | `public Sprite sprite;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:57` | `public string targetTag;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:59` | `public Transform pivotTrans;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:61` | `public Transform childTrans;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMask.cs:63` | `private int _lastDecoCacheUpdate;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |

### B.10 SpriteAlphaMaskUtils.cs

| 위치 | 선언 원문 | 현재 대응 / 설계 목적지 |
|---|---|---|
| `G/SpriteAlphaMaskUtils.cs:7` | `private static int lastCache;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMaskUtils.cs:9` | `private static List<SpriteAlphaMask> alphaMaskCache = new List<SpriteAlphaMask>();` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMaskUtils.cs:11` | `private static Dictionary<string, List<SpriteAlphaMask>> taggedAlphaMaskCache = new Dictionary<string, List<SpriteAlphaMask>>();` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/SpriteAlphaMaskUtils.cs:13` | `public static bool doRefreshMaskCache = false;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |

### B.11 BlendModes/BlendModeEffect.cs

| 위치 | 선언 원문 | 현재 대응 / 설계 목적지 |
|---|---|---|
| `G/BlendModes/BlendModeEffect.cs:11` | `private string shaderFamily;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/BlendModes/BlendModeEffect.cs:14` | `private BlendMode blendMode;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/BlendModes/BlendModeEffect.cs:17` | `private RenderMode renderMode;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/BlendModes/BlendModeEffect.cs:20` | `private MaskMode maskMode;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/BlendModes/BlendModeEffect.cs:23` | `private MaskBehaviour maskBehaviour;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/BlendModes/BlendModeEffect.cs:26` | `private Color overlayColor = Color.white;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/BlendModes/BlendModeEffect.cs:29` | `private Texture overlayTexture;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/BlendModes/BlendModeEffect.cs:32` | `private Vector2 overlayTextureOffset = Vector2.zero;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/BlendModes/BlendModeEffect.cs:35` | `private Vector2 overlayTextureScale = Vector2.one;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/BlendModes/BlendModeEffect.cs:38` | `private bool framebufferEnabled;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/BlendModes/BlendModeEffect.cs:41` | `private bool unifiedGrabEnabled;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/BlendModes/BlendModeEffect.cs:44` | `private bool shareMaterial;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/BlendModes/BlendModeEffect.cs:47` | `private Material blendMaterial;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/BlendModes/BlendModeEffect.cs:50` | `private ComponentExtensionState componentExtensionState = new ComponentExtensionState();` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/BlendModes/BlendModeEffect.cs:53` | `private bool usePointSampling;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/BlendModes/BlendModeEffect.cs:57` | `private static ShaderResources cachedShaderResources;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/BlendModes/BlendModeEffect.cs:59` | `private bool isMaterialDirty = true;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/BlendModes/BlendModeEffect.cs:61` | `private ComponentExtension сomponentExtension;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/BlendModes/BlendModeEffect.cs:232` | `private static ShaderResources ShaderResources => cachedShaderResources ?? (cachedShaderResources = ShaderResources.Load());` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |

### B.12 ffxMoveDecorationsPlus.cs

| 위치 | 선언 원문 | 현재 대응 / 설계 목적지 |
|---|---|---|
| `G/ffxMoveDecorationsPlus.cs:10` | `public scrDecorationManager decManager;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:12` | `public DecPlacementType movementType;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:14` | `public bool movementTypeUsed = true;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:16` | `public Vector2 targetPos;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:18` | `public bool positionUsed = true;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:20` | `public float targetRot;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:22` | `public bool pivotUsed = true;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:24` | `public Vector2 targetPivot;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:26` | `public bool rotationUsed = true;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:28` | `public float targetScale = float.NaN;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:30` | `public Vector2 targetScaleV2;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:32` | `public bool scaleUsed = true;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:34` | `public Color targetColor;` | 현재 16채널 T; L도 같은 값 공급 가능, 소비자 Transform 유지; 2.2·4 |
| `G/ffxMoveDecorationsPlus.cs:36` | `public bool colorUsed = true;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:38` | `public float targetOpacity = 1f;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:40` | `public bool opacityUsed = true;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:42` | `public Vector2 targetParallax;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:44` | `public Vector2 targetParallaxOffset;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:46` | `public bool parallaxOffsetUsed = true;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:48` | `public bool parallaxUsed;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:50` | `public bool visible = true;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:52` | `public bool visibleUsed;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:54` | `public string targetImageFilename;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:56` | `public bool imageFilenameUsed;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:58` | `public Vector2 targetOriginalSize;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:60` | `public bool originalSizeUsed;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:62` | `public Vector2 targetCropPivotOffset;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:64` | `public bool cropPivotOffsetUsed;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:66` | `public int targetDepth;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:68` | `public bool depthUsed;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:70` | `public bool targetSmoothing;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/ffxMoveDecorationsPlus.cs:72` | `public bool smoothingUsed;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/ffxMoveDecorationsPlus.cs:74` | `public MaskingType targetMaskingType;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/ffxMoveDecorationsPlus.cs:76` | `public bool maskingTypeUsed;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/ffxMoveDecorationsPlus.cs:78` | `public string targetmaskingTarget;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/ffxMoveDecorationsPlus.cs:80` | `public bool maskingTargetUsed;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/ffxMoveDecorationsPlus.cs:82` | `public bool targetUseMaskingDepth;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/ffxMoveDecorationsPlus.cs:84` | `public bool useMaskingDepthUsed;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/ffxMoveDecorationsPlus.cs:86` | `public int targetMaskingFrontDepth;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/ffxMoveDecorationsPlus.cs:88` | `public bool maskingFrontDepthUsed;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/ffxMoveDecorationsPlus.cs:90` | `public int targetMaskingBackDepth;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/ffxMoveDecorationsPlus.cs:92` | `public bool maskingBackDepthUsed;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/ffxMoveDecorationsPlus.cs:94` | `public List<string> targetTags = new List<string>();` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |
| `G/ffxMoveDecorationsPlus.cs:96` | `public bool forceDontTweenMovement;` | 현재 표 이벤트 일부 T; 미지원 필드/조건은 L+D/N; 2.3·3.1 |

Setup/Decode에서 참조하는 문자열 키(같은 줄의 enum 해석용 문자열도 포함):

| 키 | 최초 위치 | 현재 대응 / 목적지 |
|---|---|---|
| `color` | `G/ffxMoveDecorationsPlus.cs:463` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `cropPivotOffset` | `G/ffxMoveDecorationsPlus.cs:444` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `decorationImage` | `G/ffxMoveDecorationsPlus.cs:440` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `depth` | `G/ffxMoveDecorationsPlus.cs:446` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `duration` | `G/ffxMoveDecorationsPlus.cs:435` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `ease` | `G/ffxMoveDecorationsPlus.cs:483` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `imageSmoothing` | `G/ffxMoveDecorationsPlus.cs:467` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `maskingBackDepth` | `G/ffxMoveDecorationsPlus.cs:475` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `maskingFrontDepth` | `G/ffxMoveDecorationsPlus.cs:473` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `maskingType` | `G/ffxMoveDecorationsPlus.cs:469` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `opacity` | `G/ffxMoveDecorationsPlus.cs:465` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `originalSize` | `G/ffxMoveDecorationsPlus.cs:442` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `parallax` | `G/ffxMoveDecorationsPlus.cs:448` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `parallaxOffset` | `G/ffxMoveDecorationsPlus.cs:450` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `pivotOffset` | `G/ffxMoveDecorationsPlus.cs:456` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `positionOffset` | `G/ffxMoveDecorationsPlus.cs:453` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `relativeTo` | `G/ffxMoveDecorationsPlus.cs:436` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `rotationOffset` | `G/ffxMoveDecorationsPlus.cs:459` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `scale` | `G/ffxMoveDecorationsPlus.cs:461` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `tag` | `G/ffxMoveDecorationsPlus.cs:477` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `useMaskingDepth` | `G/ffxMoveDecorationsPlus.cs:471` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |
| `visible` | `G/ffxMoveDecorationsPlus.cs:438` | 2.2·2.3의 공통/이미지 표: T 지원 필드 외 L/D/N |

### B.13 ffxSetTextPlus.cs

| 위치 | 선언 원문 | 현재 대응 / 설계 목적지 |
|---|---|---|
| `G/ffxSetTextPlus.cs:8` | `public scrDecorationManager decManager;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetTextPlus.cs:10` | `public string targetString;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetTextPlus.cs:12` | `public List<string> targetTags = new List<string>();` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |

Setup/Decode에서 참조하는 문자열 키(같은 줄의 enum 해석용 문자열도 포함):

| 키 | 최초 위치 | 현재 대응 / 목적지 |
|---|---|---|
| `decText` | `G/ffxSetTextPlus.cs:46` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `tag` | `G/ffxSetTextPlus.cs:47` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |

### B.14 ffxSetObjectPlus.cs

| 위치 | 선언 원문 | 현재 대응 / 설계 목적지 |
|---|---|---|
| `G/ffxSetObjectPlus.cs:10` | `public scrDecorationManager decManager;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:12` | `public List<string> targetTags = new List<string>();` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:14` | `public Color planetColor;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:16` | `public bool planetColorUsed = true;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:18` | `public Color planetTailColor;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:20` | `public bool planetTailColorUsed = true;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:22` | `public float trackAngle;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:24` | `public bool trackAngleUsed = true;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:26` | `public FloorDecorationColorType trackColorType;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:28` | `public bool trackColorTypeUsed = true;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:30` | `public Color trackColor;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:32` | `public bool trackColorUsed = true;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:34` | `public Color secondaryTrackColor;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:36` | `public bool secondaryTrackColorUsed = true;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:38` | `public float trackColorAnimDuration;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:40` | `public bool trackColorAnimDurationUsed = true;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:42` | `public float trackOpacity;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:44` | `public bool trackOpacityUsed = true;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:46` | `public TrackStyle trackStyle;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:48` | `public bool trackStyleUsed = true;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:50` | `public CustomFloorIcon trackIcon;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:52` | `public bool trackIconUsed = true;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:54` | `public float trackIconAngle;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:56` | `public bool trackIconAngleUsed = true;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:58` | `public bool trackIconFlipped;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:60` | `public bool trackIconFlippedUsed = true;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:62` | `public bool trackRedSwirl;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:64` | `public bool trackRedSwirlUsed = true;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:66` | `public bool trackGraySetSpeedIcon;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:68` | `public bool trackGraySetSpeedIconUsed = true;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:70` | `public bool trackGlowEnabled;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:72` | `public bool trackGlowEnabledUsed = true;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:74` | `public Color trackGlowColor;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:76` | `public bool trackGlowColorUsed = true;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:78` | `public bool trackIconOutlines;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ffxSetObjectPlus.cs:80` | `public bool trackIconOutlinesUsed = true;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |

Setup/Decode에서 참조하는 문자열 키(같은 줄의 enum 해석용 문자열도 포함):

| 키 | 최초 위치 | 현재 대응 / 목적지 |
|---|---|---|
| `duration` | `G/ffxSetObjectPlus.cs:241` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `ease` | `G/ffxSetObjectPlus.cs:248` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `planetColor` | `G/ffxSetObjectPlus.cs:249` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `planetTailColor` | `G/ffxSetObjectPlus.cs:251` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `secondaryTrackColor` | `G/ffxSetObjectPlus.cs:259` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `tag` | `G/ffxSetObjectPlus.cs:242` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `trackAngle` | `G/ffxSetObjectPlus.cs:253` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `trackColor` | `G/ffxSetObjectPlus.cs:257` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `trackColorAnimDuration` | `G/ffxSetObjectPlus.cs:261` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `trackColorType` | `G/ffxSetObjectPlus.cs:255` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `trackGlowColor` | `G/ffxSetObjectPlus.cs:279` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `trackGlowEnabled` | `G/ffxSetObjectPlus.cs:277` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `trackGraySetSpeedIcon` | `G/ffxSetObjectPlus.cs:275` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `trackIcon` | `G/ffxSetObjectPlus.cs:267` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `trackIconAngle` | `G/ffxSetObjectPlus.cs:269` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `trackIconFlipped` | `G/ffxSetObjectPlus.cs:271` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `trackIconOutlines` | `G/ffxSetObjectPlus.cs:281` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `trackOpacity` | `G/ffxSetObjectPlus.cs:263` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `trackRedSwirl` | `G/ffxSetObjectPlus.cs:273` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `trackStyle` | `G/ffxSetObjectPlus.cs:265` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |

### B.15 ADOFAI.FloorFX/ffxSetParticlePlus.cs

| 위치 | 선언 원문 | 현재 대응 / 설계 목적지 |
|---|---|---|
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:12` | `public List<string> targetTags;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:14` | `public scrDecorationManager decManager;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:16` | `public ParticleSystem.MinMaxGradient color;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:18` | `public ParticleSystem.MinMaxGradient colorOverLifetime;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:20` | `public ParticlePlayMode targetMode;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:22` | `public int maxParticles;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:24` | `public ParticleSystem.MinMaxCurve sizeOverLifetime;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:26` | `public ParticleSystem.MinMaxCurve particleLifetime;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:28` | `public ParticleSystem.MinMaxCurve particleSize;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:30` | `public ParticleSystem.MinMaxCurve rotationOverTime;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:32` | `public Tuple<Vector2, Vector2> velocity;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:34` | `public ParticleShape shapeType;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:36` | `public float shapeRadius;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:38` | `public int emissionRate;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:40` | `public float simulationSpeed;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:42` | `public float arc;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:44` | `public ParticleSystemShapeMultiModeValue arcMode;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:46` | `public ParticleSystem.MinMaxCurve limitVelocityOverLifetime;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:48` | `public bool lockRotation;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:50` | `public bool lockScale;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:52` | `public bool useColor;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:54` | `public bool useColorOverLifetime;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:56` | `public bool useTargetMode;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:58` | `public bool useMaxParticles;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:60` | `public bool useParticleLifetime;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:62` | `public bool useParticleSize;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:64` | `public bool useSizeOverLifetime;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:66` | `public bool useRotationOverTime;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:68` | `public bool useVelocity;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:70` | `public bool useShapeType;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:72` | `public bool useShapeRadius;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:74` | `public bool useEmissionRate;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:76` | `public bool useSimulationSpeed;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:78` | `public bool useArc;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:80` | `public bool useArcMode;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:82` | `public bool useLimitVelocityOverLifetime;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:84` | `public bool useLockRotation;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:86` | `public bool useLockScale;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:88` | `protected override IEnumerable<Tween> eventTweens => decManager.GetTaggedDecorations(targetTags).SelectMany((scrDecoration d) => d.eventTweens.Values);` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |

Setup/Decode에서 참조하는 문자열 키(같은 줄의 enum 해석용 문자열도 포함):

| 키 | 최초 위치 | 현재 대응 / 목적지 |
|---|---|---|
| `arc` | `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:288` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `arcMode` | `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:289` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `color` | `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:274` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `colorOverLifetime` | `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:275` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `duration` | `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:273` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `ease` | `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:318` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `emissionRate` | `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:286` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `lockRotation` | `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:291` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `lockScale` | `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:292` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `maxParticles` | `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:278` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `particleLifetime` | `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:279` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `particleSize` | `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:281` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `rotationOverTime` | `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:282` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `shapeRadius` | `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:285` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `shapeType` | `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:284` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `simulationSpeed` | `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:287` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `sizeOverLifetime` | `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:280` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `tag` | `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:311` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `targetMode` | `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:277` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `velocity` | `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:283` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `velocityLimitOverLifetime` | `G/ADOFAI.FloorFX/ffxSetParticlePlus.cs:290` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |

### B.16 ADOFAI.FloorFX/ffxEmitParticlePlus.cs

| 위치 | 선언 원문 | 현재 대응 / 설계 목적지 |
|---|---|---|
| `G/ADOFAI.FloorFX/ffxEmitParticlePlus.cs:9` | `public List<string> targetTags;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxEmitParticlePlus.cs:11` | `public scrDecorationManager decManager;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |
| `G/ADOFAI.FloorFX/ffxEmitParticlePlus.cs:13` | `public int count;` | 현재 비이미지 거부; N(타입별 원래 실행/렌더), 공통 변환은 L 가능; 2.1·2.3 |

Setup/Decode에서 참조하는 문자열 키(같은 줄의 enum 해석용 문자열도 포함):

| 키 | 최초 위치 | 현재 대응 / 목적지 |
|---|---|---|
| `count` | `G/ADOFAI.FloorFX/ffxEmitParticlePlus.cs:26` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `tag` | `G/ADOFAI.FloorFX/ffxEmitParticlePlus.cs:27` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |

### B.17 ffxSetFilterAdvancedPlus.cs

| 위치 | 선언 원문 | 현재 대응 / 설계 목적지 |
|---|---|---|
| `G/ffxSetFilterAdvancedPlus.cs:11` | `public static readonly string[] blacklistedFilterKeywords = new string[3] { "Blend2Camera_", "Antialiasing_FXAA", "Colors_Adjust_PreFilters" };` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/ffxSetFilterAdvancedPlus.cs:13` | `public FilterTargetType targetType;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/ffxSetFilterAdvancedPlus.cs:15` | `public FilterPlane plane;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/ffxSetFilterAdvancedPlus.cs:17` | `public string decorationTag;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/ffxSetFilterAdvancedPlus.cs:19` | `public string filterName;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/ffxSetFilterAdvancedPlus.cs:21` | `public bool enableFilter;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/ffxSetFilterAdvancedPlus.cs:23` | `public bool disableOthers;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/ffxSetFilterAdvancedPlus.cs:25` | `public Dictionary<string, object> filterProperties;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/ffxSetFilterAdvancedPlus.cs:27` | `private List<GameObject> targetObjects;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/ffxSetFilterAdvancedPlus.cs:29` | `private static Dictionary<FilterPlane, Camera> planeToCamera = new Dictionary<FilterPlane, Camera>();` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/ffxSetFilterAdvancedPlus.cs:31` | `private static Dictionary<GameObject, HashSet<string>> addedFilters = new Dictionary<GameObject, HashSet<string>>();` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/ffxSetFilterAdvancedPlus.cs:33` | `private static Dictionary<GameObject, HashSet<string>> usedFilters = new Dictionary<GameObject, HashSet<string>>();` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/ffxSetFilterAdvancedPlus.cs:35` | `private static Dictionary<GameObject, HashSet<string>> modifiedFilters = new Dictionary<GameObject, HashSet<string>>();` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/ffxSetFilterAdvancedPlus.cs:37` | `private static Dictionary<GameObject, Dictionary<string, Dictionary<string, object>>> filterOriginalValues = new Dictionary<GameObject, Dictionary<string, Dictionary<string, object>>>();` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/ffxSetFilterAdvancedPlus.cs:39` | `private static Dictionary<GameObject, Dictionary<string, Dictionary<string, Tween>>> filterFieldTweens = new Dictionary<GameObject, Dictionary<string, Dictionary<string, Tween>>>();` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/ffxSetFilterAdvancedPlus.cs:41` | `private static Dictionary<GameObject, HashSet<string>> initializedFilters = new Dictionary<GameObject, HashSet<string>>();` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/ffxSetFilterAdvancedPlus.cs:43` | `private Type filterType;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/ffxSetFilterAdvancedPlus.cs:45` | `private FieldInfo[] filterFields;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/ffxSetFilterAdvancedPlus.cs:47` | `private Dictionary<GameObject, Component> filterComponents = new Dictionary<GameObject, Component>();` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/ffxSetFilterAdvancedPlus.cs:49` | `private List<GameObject> isAddedComponent = new List<GameObject>();` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/ffxSetFilterAdvancedPlus.cs:51` | `private Dictionary<GameObject, MonoBehaviour> filterMonoBehaviours = new Dictionary<GameObject, MonoBehaviour>();` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/ffxSetFilterAdvancedPlus.cs:53` | `private bool saveOriginalValue;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |
| `G/ffxSetFilterAdvancedPlus.cs:66` | `private static bool variableSetupDone => planeToCamera.GetValueOrDefault(FilterPlane.Foreground) != null;` | 현재 제한 지원/거부; 원래 효과 N 또는 상태 L+그리기 D; 2.2·5·6 |

Setup/Decode에서 참조하는 문자열 키(같은 줄의 enum 해석용 문자열도 포함):

| 키 | 최초 위치 | 현재 대응 / 목적지 |
|---|---|---|
| `disableOthers` | `G/ffxSetFilterAdvancedPlus.cs:442` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `duration` | `G/ffxSetFilterAdvancedPlus.cs:435` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `ease` | `G/ffxSetFilterAdvancedPlus.cs:436` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `enabled` | `G/ffxSetFilterAdvancedPlus.cs:441` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `filter` | `G/ffxSetFilterAdvancedPlus.cs:440` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `filter_` | `G/ffxSetFilterAdvancedPlus.cs:446` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `plane` | `G/ffxSetFilterAdvancedPlus.cs:438` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `targetTag` | `G/ffxSetFilterAdvancedPlus.cs:439` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |

### B.18 ffxRecolorFloorPlus.cs

| 위치 | 선언 원문 | 현재 대응 / 설계 목적지 |
|---|---|---|
| `G/ffxRecolorFloorPlus.cs:9` | `private scrLevelMaker levelMaker;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/ffxRecolorFloorPlus.cs:11` | `public int start;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/ffxRecolorFloorPlus.cs:13` | `public int end;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/ffxRecolorFloorPlus.cs:15` | `public Color color1 = Color.clear;` | 현재 16채널 T; L도 같은 값 공급 가능, 소비자 Transform 유지; 2.2·4 |
| `G/ffxRecolorFloorPlus.cs:17` | `public Color color2 = Color.clear;` | 현재 16채널 T; L도 같은 값 공급 가능, 소비자 Transform 유지; 2.2·4 |
| `G/ffxRecolorFloorPlus.cs:19` | `public TrackColorType colorType;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/ffxRecolorFloorPlus.cs:21` | `public float colorAnimDuration;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/ffxRecolorFloorPlus.cs:23` | `public TrackColorPulse pulseType;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/ffxRecolorFloorPlus.cs:25` | `public int pulseLength = 10;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/ffxRecolorFloorPlus.cs:27` | `public TrackStyle style;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/ffxRecolorFloorPlus.cs:29` | `public string trackTexture;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/ffxRecolorFloorPlus.cs:31` | `public float trackTextureScale;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/ffxRecolorFloorPlus.cs:33` | `public float glowMult = 1f;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/ffxRecolorFloorPlus.cs:35` | `public int gapLength;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/ffxRecolorFloorPlus.cs:37` | `public bool usedColor1 = true;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/ffxRecolorFloorPlus.cs:39` | `public bool usedColor2 = true;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/ffxRecolorFloorPlus.cs:41` | `public bool usedColorType = true;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/ffxRecolorFloorPlus.cs:43` | `public bool usedColorAnimDuration = true;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/ffxRecolorFloorPlus.cs:45` | `public bool usedPulseType = true;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/ffxRecolorFloorPlus.cs:47` | `public bool usedPulseLength = true;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/ffxRecolorFloorPlus.cs:49` | `public bool usedStyle = true;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/ffxRecolorFloorPlus.cs:51` | `public bool usedTrackTexture = true;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/ffxRecolorFloorPlus.cs:53` | `public bool usedTrackTextureScale = true;` | 현재 자산/도형/재질 제한; T계단 또는 L상태 + D/N; 2.2·5·6 |
| `G/ffxRecolorFloorPlus.cs:55` | `public bool usedGlowMult = true;` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |
| `G/ffxRecolorFloorPlus.cs:63` | `protected override IEnumerable<Tween> eventTweens => levelMaker.listFloors.Where((scrFloor f) => f.seqID >= start && f.seqID <= end).SelectMany((scrFloor f) => from t in f.moveTweens` | 현재 초기상수/바인딩/수명 일부; T상수 또는 L/N에서 원래 의미 보존; 2.2·4 |

Setup/Decode에서 참조하는 문자열 키(같은 줄의 enum 해석용 문자열도 포함):

| 키 | 최초 위치 | 현재 대응 / 목적지 |
|---|---|---|
| `duration` | `G/ffxRecolorFloorPlus.cs:132` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `ease` | `G/ffxRecolorFloorPlus.cs:133` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `endTile` | `G/ffxRecolorFloorPlus.cs:135` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `gapLength` | `G/ffxRecolorFloorPlus.cs:136` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `secondaryTrackColor` | `G/ffxRecolorFloorPlus.cs:138` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `startTile` | `G/ffxRecolorFloorPlus.cs:134` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `trackColor` | `G/ffxRecolorFloorPlus.cs:137` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `trackColorAnimDuration` | `G/ffxRecolorFloorPlus.cs:139` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `trackColorPulse` | `G/ffxRecolorFloorPlus.cs:141` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `trackColorType` | `G/ffxRecolorFloorPlus.cs:140` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `trackGlowIntensity` | `G/ffxRecolorFloorPlus.cs:155` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `trackPulseLength` | `G/ffxRecolorFloorPlus.cs:142` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `trackStyle` | `G/ffxRecolorFloorPlus.cs:143` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `trackTexture` | `G/ffxRecolorFloorPlus.cs:144` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |
| `trackTextureScale` | `G/ffxRecolorFloorPlus.cs:145` | 위 타입별 필드와 동일한 N 또는 L+D 경로 |

B 선언 인벤토리: 411개, 대상 파일 18개. 키의 존재는 사용 횟수/프레임 비용 측정이 아니다.


## 부록 C. Native Tween 이어받기의 숫자 코드 거절 경로

문자열 throw 외에 NativeReject enum으로 거절하는 경로도 전부 분리했다. 게임은 G/ffxMoveDecorationsPlus.cs:156 이하에서 DOTween과 closure/OnUpdate/OnComplete를 생성한다. 현재 이어받기는 그 정확한 구조만 추출하려 하므로 아래 조건을 거절한다. 모든 행의 설계 목적지는 **원래 Tween/콜백을 계속 실행하는 L 또는 N**이다. 이 거절 조건을 삭제해 T로 강제 이관하는 안이 아니다. 크기는 공통적으로 중간~큼, 위험은 Tween identity/콜백·시간 연속성, 비용은 원래 CPU Tween+CL 또는 CN이다(4절).

| 위치 | 현재 거절 조건 원문 | 설계 목적지 / 게임 근거 |
|---|---|---|
| `GpuDecoNativeAdopt.cs:132` | `if(ForeignSetter())nativeGlobalReject=NativeReject.ForeignSetter;` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:133` | `else if(DOTween.Version!="1.2.765")nativeGlobalReject=NativeReject.Version;` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:134` | `else if(NativePatched(nativeUpdateMethod,true))nativeGlobalReject=NativeReject.UpdatePatched;` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:137` | `if(nativeGlobalReject==0 && nativeLinks==null)nativeGlobalReject=NativeReject.LinksUnavailable;` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:138` | `if(nativeGlobalReject==0 && nativeManagerUpdating)nativeGlobalReject=NativeReject.ManagerUpdating;` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:145` | `catch(Exception ex){nativeGlobalReject=NativeReject.ReadException;nativeLastEx=ex.GetType().Name+":"+ex.Message;}` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:277` | `NR(NativeReject.ReadException);RecordNativeCombo(d);return false; // Unknown schema/state stays on CPU.` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:284` | `if(nativeUpdateRunning)return NR(NativeReject.UpdateRunning);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:285` | `if(!NativePaused() && nativeHookFrame==Time.frameCount)return NR(NativeReject.HookFrame);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:301` | `if(!t.active)return NR(NativeReject.Inactive);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:302` | `if(t.id!=null)return NR(NativeReject.Id);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:303` | `if(t.target!=null)return NR(NativeReject.Target);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:304` | `if(t.stringId!=null)return NR(NativeReject.StringId);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:305` | `if(t.intId!=-999)return NR(NativeReject.IntId);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:306` | `if(!t.IsInitialized()){nativeUninitSeen++;return NR(NativeReject.Uninitialized);}` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:307` | `if(!t.IsPlaying())return NR(NativeReject.NotPlaying);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:308` | `if(t.IsComplete())return NR(NativeReject.Complete);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:309` | `if(t.isRelative)return NR(NativeReject.Relative);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:310` | `if(t.isBackwards)return NR(NativeReject.Backwards);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:311` | `if(!NativeBits(t.timeScale,1f))return NR(NativeReject.TimeScale);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:313` | `if(!DecoBakeCore.Finite(delay))return NR(NativeReject.DelayNonfinite);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:314` | `if(delay<0f)return NR(NativeReject.DelayNegative);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:315` | `if(!DecoBakeCore.Finite(elapsed))return NR(NativeReject.ElapsedDelayNonfinite);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:316` | `if(elapsed<0f)return NR(NativeReject.ElapsedDelayNegative);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:317` | `if(elapsed<delay)return NR(NativeReject.DelayRemaining);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:318` | `if(!NativeBits(elapsed,delay))return NR(NativeReject.DelayElapsedMismatch);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:319` | `if(t.Loops()!=1)return NR(NativeReject.Loops);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:320` | `if(t.CompletedLoops()!=0)return NR(NativeReject.CompletedLoops);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:321` | `if(nativeGlobalReject!=0)return NR(nativeGlobalReject);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:323` | `if(NativeRead<bool>(t,NativeFlags[i],typeof(Tween)))return NR((NativeReject)((int)NativeReject.Inverted+i));` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:324` | `if(NativeRead<UpdateType>(t,"updateType",typeof(Tween))!=UpdateType.Normal)return NR(NativeReject.UpdateType);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:325` | `if(NativeRead<LoopType>(t,"loopType",typeof(Tween))!=LoopType.Restart)return NR(NativeReject.LoopType);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:328` | `if(!NativeRead<bool>(t,"delayComplete",typeof(Tween)))return NR(NativeReject.DelayIncomplete);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:329` | `if(!NativeRead<bool>(t,"autoKill",typeof(Tween)))return NR(NativeReject.AutoKill);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:330` | `if(NativeRead<object>(t,"sequenceParent",typeof(Tween))!=null)return NR(NativeReject.SequenceParent);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:331` | `if(Convert.ToInt32(NativeRead<object>(t,"specialStartupMode",typeof(Tween)))!=0)return NR(NativeReject.SpecialStartup);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:332` | `if(NativeRead<object>(t,"customEase",typeof(Tween))!=null)return NR(NativeReject.CustomEase);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:333` | `if(NativeRead<object>(t,"onStart",typeof(ABSSequentiable))!=null)return NR(NativeReject.OnStart);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:334` | `if(t.onKill!=null)return NR(NativeReject.OnKill);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:335` | `if(t.onPlay!=null)return NR(NativeReject.OnPlay);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:336` | `if(t.onPause!=null)return NR(NativeReject.OnPause);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:337` | `if(t.onRewind!=null)return NR(NativeReject.OnRewind);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:338` | `if(t.onStepComplete!=null)return NR(NativeReject.OnStepComplete);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:339` | `if(t.onWaypointChange!=null)return NR(NativeReject.OnWaypointChange);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:340` | `if(nativeLinks==null)return NR(NativeReject.LinksUnavailable);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:341` | `if(nativeLinks.Contains(t))return NR(NativeReject.Linked);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:342` | `if(nativeManagerUpdating)return NR(NativeReject.ManagerUpdating);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:344` | `if(plugin?.GetType()!=pluginType)return NR(NativeReject.PluginType);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:345` | `if(NativePatched(NativeMethod(pluginType,"EvaluateAndApply")))return NR(NativeReject.PluginPatched);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:346` | `if(NativePatched(NativeMethod(NativeManager,"Despawn")))return NR(NativeReject.DespawnPatched);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:347` | `if(NativePatched(nativeKillMethod,true))return NR(NativeReject.KillPatched);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:366` | `default:return NR(NativeReject.UnsupportedKey);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:374` | `if(t==null &#124;&#124; t.GetType()!=typeof(TweenerCore<Vector2,Vector2,VectorOptions>))return NR(NativeReject.ScaleType);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:375` | `if(t.plugOptions.snapping)return NR(NativeReject.ScaleSnapping);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:376` | `if(t.plugOptions.axisConstraint!=(first==5?AxisConstraint.X:AxisConstraint.Y))return NR(NativeReject.ScaleAxis);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:380` | `if(!NativeBits(t.changeValue[1-axis],0f))return NR(NativeReject.ScaleUnusedChange);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:387` | `if(t==null &#124;&#124; t.GetType()!=typeof(TweenerCore<Color,Color,ColorOptions>))return NR(NativeReject.ColorType);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:388` | `if(t.plugOptions.alphaOnly)return NR(NativeReject.ColorAlphaOnly);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:397` | `if(t==null &#124;&#124; t.GetType()!=typeof(TweenerCore<float,float,FloatOptions>))return NR(NativeReject.FloatType);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:398` | `if(t.plugOptions.snapping)return NR(NativeReject.FloatSnapping);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:404` | `if(!NativeDelegate(getter,get))return NR(NativeReject.Getter);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:405` | `if(!NativeDelegate(setter,set,getter.Target))return NR(NativeReject.Setter);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:410` | `if(tween.onUpdate!=null)return NR(NativeReject.ScaleOnUpdate);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:411` | `if(tween.onComplete!=null)return NR(NativeReject.ScaleOnComplete);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:416` | `if(!NativeDelegate(tween.onUpdate,update,s.Closure))return NR(NativeReject.OnUpdate);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:417` | `if(!NativeDelegate(tween.onComplete,complete))return NR(NativeReject.OnComplete);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:419` | `if(!ReferenceEquals(s.CompleteClosure,tween.onComplete.Target))return NR(NativeReject.CompleteClosure);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:422` | `if(!ReferenceEquals(NativeRead<object>(s.Root,"dec"),d))return NR(NativeReject.Decoration);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:424` | `if(!s.Fx)return NR(NativeReject.Fx);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:452` | `if(!DecoBakeCore.Finite(s.Pos))return NR(NativeReject.PositionNonfinite);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:453` | `if(!DecoBakeCore.Finite(s.Dur))return NR(NativeReject.DurationNonfinite);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:454` | `if(!DecoBakeCore.Finite(s.Over))return NR(NativeReject.OvershootNonfinite);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:455` | `if(!DecoBakeCore.Finite(s.Period))return NR(NativeReject.PeriodNonfinite);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:456` | `if(s.Dur<=0f)return NR(NativeReject.Duration);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:457` | `if(s.Pos<0f)return NR(NativeReject.PositionNegative);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:458` | `if(s.Pos>=s.Dur)return NR(NativeReject.PositionAtEnd);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:459` | `if(s.Ease==Ease.Unset)return NR(NativeReject.EaseUnset);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:460` | `if(!GpuDecoGpu.Supported((int)s.Ease))return NR(NativeReject.EaseUnsupported);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:464` | `if(!DecoBakeCore.Finite(s.Start[c]))return NR(NativeReject.StartNonfinite);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:465` | `if(!DecoBakeCore.Finite(s.End[c]))return NR(NativeReject.EndNonfinite);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:466` | `if(!DecoBakeCore.Finite(s.Change[c]))return NR(NativeReject.ChangeNonfinite);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:467` | `if(!DecoBakeCore.Finite(s.Current[c]))return NR(NativeReject.CurrentNonfinite);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:468` | `if(!DecoBakeCore.Finite(expected[c]))return NR(NativeReject.ExpectedNonfinite);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:469` | `if(!DecoBakeCore.Finite(logical[c]))return NR(NativeReject.LogicalNonfinite);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:470` | `if(!NativeBits(expected[c],s.End[c]))return NR(NativeReject.EndMismatch);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:471` | `if(!NativeBits(s.End[c]-s.Start[c],s.Change[c]))return NR(NativeReject.ChangeMismatch);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:473` | `if(!NativeNear(s.Current[c],value))return NR(NativeReject.ClosureValueMismatch);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |
| `GpuDecoNativeAdopt.cs:474` | `if(!NativeNear(logical[c],value))return NR(NativeReject.LogicalValueMismatch);` | L/N 원래 실행; G/ffxMoveDecorationsPlus.cs:156, :296, :324; 4절 CL/CN |

직접 거절 문장 88개. enum 플래그 연속값 검사와 모든 숫자 의미는 아래 선언·플래그 목록으로 보존한다.

```csharp
        enum NativeReject
        {
            None=0, UpdateRunning=1, ReadException=16,
            HookFrame=20, RunningRec, UnsupportedKey,
            Inactive=30, Id, Target, StringId, IntId, Uninitialized, NotPlaying, Complete,
            Relative, Backwards, TimeScale, DelayNonfinite, DelayNegative,
            ElapsedDelayNonfinite, ElapsedDelayNegative, DelayRemaining, DelayElapsedMismatch, Loops, CompletedLoops,
            Inverted=50, IndependentUpdate, From, Blendable, SpeedBased, Sequenced,
            UpdateType=60, LoopType, DelayIncomplete, AutoKill, SequenceParent, SpecialStartup,
            CustomEase, OnStart, OnKill, OnPlay, OnPause, OnRewind, OnStepComplete, OnWaypointChange,
            LinksUnavailable=80, Linked, ManagerUpdating, PluginType, PluginPatched, DespawnPatched, KillPatched,
            ScaleType=90, ScaleSnapping, ScaleAxis, ColorType, ColorAlphaOnly, FloatType, FloatSnapping,
            Getter=100, Setter, ScaleOnUpdate, ScaleOnComplete, OnUpdate, OnComplete, CompleteClosure,
            Decoration=110, Fx,
            PositionNonfinite=120, DurationNonfinite, OvershootNonfinite, PeriodNonfinite,
            StartNonfinite, EndNonfinite, ChangeNonfinite, CurrentNonfinite, ExpectedNonfinite, LogicalNonfinite,
            Duration=130, PositionNegative, PositionAtEnd, EaseUnset, EaseUnsupported, ScaleUnusedChange,
            EndMismatch=140, ChangeMismatch,
            ClosureValueMismatch=150, LogicalValueMismatch,
            QuartzOptimizer=160, ForeignSetter, Version, UpdatePatched
        }
        static NativeReject nativeGlobalReject;
        static long nativeCheckTicks,nativeCaptureTicks,nativeCommitTicks;
```

그 외 반환 false도 재검증에 쓰인다. `GpuDecoNativeAdopt.cs:534`의 NativeAdoptionUnchanged는 hook/update 진행 상태·실제 native channel 집합·clock/type/closure/값의 변경을 검사한다. commit 직전 바뀌면 기존 Tween을 그대로 두고 해당 대상 L/N으로 이관한다. `GpuDecoResume.cs:26`의 mixed source group, :51의 채널/anchor 불일치, :58의 외부 소유 변경은 복원 불변식이므로 제거하지 않는다.

## 문서 자체 검증 기록

- 인계의 분석 전용 범위 준수: 게임 실행·자동화 제어·빌드·shader 생성/컴파일·제품 코드 수정·커밋 없음. 설치 DLL은 읽기 전용 ILSpy 조회만 했다.
- DecoBake 계열의 직접 reason 문자열 리터럴 30종이 문서에 전부 존재함을 검사했다. PlacementReason/MovementReason·목표값 추가 문자열·장면 reject·의미적 무문자열 제외는 본문/부록에서 별도로 확인했다.
- 문서의 파일:줄 인용 1,620개 고유 위치가 실제 파일 범위 안에 있음을 검사했다. 이것은 인용 위치의 존재 검증이며 의미의 실행 증명은 아니다.
- Markdown 표 열 수, 코드 블록 짝, UTF-8 대체 문자 없음, 행 끝 공백 없음을 검사했다. 실행 테스트는 수행하지 않았다.
- 시작 시 이미 수정된 RunLod.cs의 SHA-256은 종료 확인에서도 `2C94858FA3C7138D9A51547102E02F1858ADE6277FECCC29F89B7E2334FB43C3`으로 같았다. 기존 handoff/out/evidence 산출물을 삭제·덮어쓰지 않았다.