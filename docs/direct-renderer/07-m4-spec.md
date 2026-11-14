# 코덱스 작업: 직통 렌더러 M4 — 메인 카메라 비우기 + 원래 셰이더로 네이티브 전체 그리기 (2026-11-15)

작업 폴더 `E:\StutterFixWork\wt-v14prod`. 커밋·게임 실행·설치 DLL 교체 금지. 한국어 보고. 결과: `docs/direct-renderer/07-m4-result.md`.
사용자 목표: "근본부터 새 렌더러, 원본보다 **압도적으로** 빠르게". 화면은 원본과 같게(픽셀 대조로 증명).

## 구조
1. **메인 카메라("Camera")의 cullingMask 에서 맵 내용 층(장식 Bg=9/Default=7, 타일 Floor=9·TopGlow=14 등, 행성 Planet=8 — 실제 층 번호는 `drx census`/코드로 확인)을 뺀다.** Unity 의 컬링·정렬·그리기 명령 생성 비용이 렌더러 수만큼 통째로 사라진다. 카메라·고급 필터(OnRenderImage)·UI 카메라는 그대로.
2. 우리 네이티브 렌더러가 camRT 에 맵 내용 전체를 정렬 순서대로 그린다(카메라 CommandBuffer, 반투명 그리기 자리 = 필터 앞).
3. **게임 원래 셰이더 바이트코드를 그대로 쓴다**: 게임 에셋(sharedassets*.assets)에서 쓰이는 셰이더의 DXBC 프로그램을 꺼낸다(이미 `out/original-tile-shader` 에 Sprites/Tile 을 꺼낸 방법과 도구가 있음: UnityPy/reader). 대상: ADOFAI/FloorMesh(키워드 변형 포함), Sprites/Tile, Sprites/Default, Legacy Particles/Additive, ADOFAI/Frames, ADOFAI/TiledSpriteWithGap, Hidden/BlendModes/*, ADOFAI/HoldShader 등 실제 맵에서 나오는 것(`fillcensus`/`drx census` 셰이더 목록). 각 프로그램의 cbuffer 배치(이름·오프셋)를 반영 정보로 읽어 Unity 와 같은 값(unity_ObjectToWorld, unity_MatrixVP, _Color, _MainTex_ST, 재질 속성, 키워드 변형)을 채운다. → 셰이더를 다시 만들지 않으니 픽셀이 원본과 같다.
4. 도형: 타일 MeshRenderer 는 Unity Mesh 의 네이티브 정점/인덱스 버퍼(`Mesh.GetNativeVertexBufferPtr/IndexBufferPtr`, 정점 형식은 `Mesh.GetVertexAttributes`)를 그대로 묶는다. 스프라이트는 sprite.vertices/uv/triangles 로 우리 정적 버퍼(바뀔 때만). 변환은 Transform 값 또는 장식 채널(지금 DR2 식).
5. 상태 변화 추적: 매 프레임 전수 확인 금지. 바뀐 것만(Transform hasChanged 일괄 확인은 네이티브 TransformAccessArray/Job 으로 병렬 가능, 재질 속성은 게임이 쓰는 지점 후크, 켜짐/꺼짐은 후크). 그리기 목록은 바뀔 때만 다시 정렬.
6. **우리가 못 그리는 것**(파티클·TextMeshPro·월드 캔버스·매 프레임 모양이 바뀌는 것): 그 렌더러들만 보조 카메라(같은 투영, cullingMask = 그 렌더러들만 옮긴 전용 층)로 별도 RT 에 그리고, 우리 순서 안의 제자리에서 합성. 어떤 것을 넘길지와 순서 묶음을 정하는 규칙(정렬 구간 단위로 RT 하나씩, 구간 수 최소화). 넘긴 렌더러의 층 이동이 물리(타일 Floor 층 충돌 판정 등)에 영향 없게 — 렌더러 GameObject 가 콜라이더를 가지면 층을 옮기지 말고 다른 방법(renderingLayerMask, 렌더러만 자식으로 분리 등) 검토.
7. 마스크: SpriteMask 의 스텐실 규칙을 우리 그리기 안에서 재현.

## 단계 (각 단계 끝에 Claude 가 잴 AUTOTEST 명령과 bench 설정을 결과에 적기)
- **M4a 상한 측정 도구**: `drfull ceil` — 맵 내용 층을 cullingMask 에서 빼기만 하고(아무것도 안 그림) FPS 를 재는 진단. 그리고 `drfull census`: 맵 내용 렌더러를 종류·셰이더·키워드·정점 형식별로 세고, 원래 셰이더 DXBC 를 꺼낼 수 있는지 표.
- **M4b 원래 셰이더 로더**: 셰이더 DXBC 추출(오프라인 도구 + 결과를 모드 리소스로) + 네이티브에서 cbuffer 반영 정보로 값 채우기. WARP 로 Sprites/Tile·FloorMesh 한 프레임을 Unity 캡처와 대조하는 fixture.
- **M4c 타일 + 장식 네이티브 전체 그리기**: 1~5. `drfull on`. 못 그리는 것은 일단 Unity 에 남기되(메인 카메라에 그 층만 남김) 순서가 맞는 경우만 켜고 아니면 거부 사유 로그.
- **M4d 보조 카메라 합성**: 6.

## 원칙
- 매 프레임 할당 0, 메인 스레드 직통 비용 목표 < 0.3ms(장식+타일 수만 개에서), 렌더 스레드 그리기 기록 < 1ms.
- 기본 꺼짐, Main.Unload·맵 전환·재시작·편집기 복귀에서 cullingMask·층·렌더러 상태 원복.
- 4종 빌드 오류 0, export 보존. 막히면 비정상적인 아이디어도 빠르게 시도하고 무엇이 미확인인지 구분.
