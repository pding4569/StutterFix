# Codex 과제 S2: `decorender` — 실제 장식을 "순서값별 그룹 메시 + 텍스처 아틀라스"로 그렸을 때 화면이 같은가 (새 장식 파이프라인 2단계, 개발자용/AutoTest 전용)

작업 폴더 E:\StutterFixWork\wt-decorender (브랜치 codex/night-decorender, 현재 main 과 같음). 여기서만 편집·빌드. **커밋·게임 실행·설치 금지**(내가 한다). 빌드 `dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1`. 새 코드는 전부 `#if DEV || AUTOTEST`. 새 파일 `DecoRender.cs`(csproj 조건부 Compile), AutoTest.cs 에 명령 연결. 기존 `MeshBench.cs`/`InstBench.cs`/`DecoGpu.cs`(예전 일괄 렌더링 프로토타입, 셰이더 Hidden/StutterFix/DecoGpu)/`DecoCensus.cs` 를 읽어 재사용할 것이 있으면 재사용. 사용량 절약: 필요한 부분만 읽는다. 결과 문서 docs/handoff/codex-decorender-20261020-result.md.

## 배경과 이번 단계의 질문
새 파이프라인 = (1) 장식 상태를 배치 계산(`decosim` 으로 비트 동일·0.5~0.8ms 확인) → (2) 그 값으로 **그룹 메시**에 정점을 써서 그린다. 그룹 = (sortingLayerID, sortingOrder, 아틀라스) 하나당 MeshRenderer 하나. MeshRenderer 는 SpriteRenderer 와 같은 sortingLayerID/sortingOrder 로 정렬된다. 같은 장면 마이크로벤치(`meshbench`): 장식 1,100개를 그룹 42개로 그리면 전체 프레임이 개별 SpriteRenderer 보다 -1.0~-1.4ms.
**이번 단계의 질문은 비용이 아니라 화면 동일성**이다: 실제 게임 장식(Arche 곡 약 264초 장면)을 SpriteRenderer 대신 그룹 메시로 그렸을 때 픽셀이 얼마나 같은가. 값(행렬)은 지금 게임이 이미 계산한 장식 Transform 에서 읽는다(성능은 이번 범위 밖, 배치 계산 연결은 다음 단계).
실측 사실(Arche 264~278초): 보이는 이미지 장식 약 1,140개, 서로 다른 스프라이트/텍스처 10종, sortingOrder 값 42개, 재질 종류 사실상 1종(재질 인스턴스는 장식마다 따로), 전부 `scrVisualDecoration`. 값이 바뀌는 건 위치+회전+크기 881개 등, 색도 바뀜.

## 만들 것: `decorender on|off|compare|state`
1. **on**: 지금 곡 중(또는 편집기 재생 중) 가져갈 장식 집합을 고른다: 정확히 `scrVisualDecoration`, `spriteRenderer` 있음·보임(`TightLate.Eligible` 의 조건을 참고하되 마스크/히트박스/메시 장식은 제외), 스프라이트가 있음. 가져간 장식의 SpriteRenderer 는 `forceRenderingOff = true`(게임 로직을 안 건드리는 방법)로 끄고, 대신 그룹 메시가 그린다.
   - **아틀라스**: 가져간 장식이 쓰는 스프라이트 텍스처들을 한 장(또는 소수)의 RGBA32 아틀라스 Texture2D 로 모은다(GPU Blit/CopyTexture; 압축 텍스처일 수 있으니 RenderTexture 로 디코드 후 복사, 여백(패딩)과 wrap 모드/필터 모드/밉맵 여부를 원본과 같게; 각 텍스처의 wrapMode, filterMode, mipmapCount, isReadable 를 로그로 남긴다). 스프라이트별 UV 사각형 재매핑. 아틀라스가 크면(원본 텍스처 크기가 크다) 그 크기와 VRAM 을 로그.
   - **메시**: 장식 하나당 그 스프라이트의 실제 윤곽 메시(`sprite.vertices`/`triangles`/`uv`)를 그대로 쓴다(쿼드가 아니라 SpriteRenderer 가 그리는 것과 같은 정점). 정점 위치는 장식의 최종 월드 행렬(`spriteRenderer.transform.localToWorldMatrix` 를 그 프레임 LateUpdate 이후 `Camera.onPreRender` 시점에 읽기, `flipX/flipY` 반영)로 변환해서 월드 좌표로 쓴다(CPU). 정점 색 = SpriteRenderer.color. 그룹마다 MeshRenderer(sortingLayerID, sortingOrder 를 장식의 것과 같게), 재질은 원래 장식 재질과 같은 셰이더/키워드(재질 인스턴스를 복제해 텍스처만 아틀라스로, `_RendererColor` 등 SpriteRenderer 가 엔진 쪽에서 넣는 속성이 어떤 경우 필요한지 확인해 맞춘다. `Sprites/Default` 는 정점색 × `_Color` × `_RendererColor`).
   - 같은 sortingOrder 안의 장식들 겹침 순서: SpriteRenderer 는 같은 순서값끼리 (sortingLayer, sortingOrder, 거리/렌더러 id…) 규칙이 있다 — 그룹 메시 안 장식 순서를 어떻게 정해야 같아지는지 알아보고(코드/실험), 우리가 정한 순서를 로그와 결과 문서에 쓴다.
2. **compare**: 같은 프레임의 같은 상태를 두 번 그려 픽셀 차이를 낸다. 구현 제안: `Camera.main`(scrCamera.instance.camobj) 을 `CopyFrom` 한 임시 카메라(활성 아님)로 렌더 텍스처에 `Render()` 두 번 — 한 번은 가져가기 해제 상태(forceRenderingOff=false, 그룹 메시 렌더러 비활성), 한 번은 가져가기 상태. 게임의 카메라 스택/필터가 복잡하면 장식이 속한 sortingLayer 만 보이도록 culling mask 를 쓰는 대신 전체를 그리되 두 번 모두 같은 조건으로. 차이: 채널별 최대 차이, 차이 >0/>1/>2/>4/>8 인 픽셀 수와 비율, 가장 큰 차이 위치. 차이 이미지(PNG, 차이×16 증폭)와 두 장을 모드 폴더 `shots/` 에 저장. 곡 시각 몇 곳에서 반복(예: compare 를 3~5번 연속 호출, 호출 사이 곡이 흘러가도 됨).
3. **off**: 완전히 원상복구(forceRenderingOff=false, 메시/아틀라스/재질 Destroy).
4. **state**: 가져간 장식 수, 그룹 수, 아틀라스 크기, 정점 수, 마지막 compare 결과.
- 곡 재생 중 호출하며, 호출한 프레임에만 비교 부하가 들어가게 하고 on 상태에서 프레임마다 메시를 다시 채우는 비용(CPU, 정점 변환)은 이번엔 측정하지 않아도 되지만 `Stopwatch` 로 프레임당 ms 를 `state` 에 남겨라.

## 주의
- 게임 동작·장식 상태를 바꾸지 않는다(오직 렌더 대체). off 후 원래와 같아야 한다.
- 파괴/전환(맵 다시 열기, 편집기 복귀) 때 자동으로 off. `Main.Unload` 에서도 정리.
- 다중 카메라: 장식을 그리는 카메라가 여럿일 수 있다(`Camera.allCamerasCount`=3 확인됨). 메시 레이어는 장식과 같은 레이어(SpriteRenderer 의 gameObject.layer)로 설정.

## 결과 문서에
명령 사용법, 빌드 결과, 아틀라스·메시·재질 구현 방식과 한계, 같은 순서값 안 겹침 순서 규칙 조사, 내가 게임에서 돌릴 순서(예: `open <Arche>`, `seek 262 8`, `waitsongpos 264`, `decorender on`, `decorender compare` ×N, `decorender state`, `decorender off`).
