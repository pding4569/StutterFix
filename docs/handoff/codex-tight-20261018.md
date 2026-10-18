# Codex 작업 4: 안개 이미지 Tight 메시(완전 투명 영역만 건너뛰기) 독립 D3D11 시험 — 게임 실행·Mods 설치·커밋 금지

이어서 읽을 것: `docs/handoff/codex-gpu3-20261018-result.md`(독립 Unity Editor D3D11 fixture 방식, 재현 절차, 임시 프로젝트 `%TEMP%/stutterfix-gpu3-20261018/UnityFixture`, `tools/gpuprobe/Gpu3Fixture.cs`). 같은 방식으로 새 fixture(`tools/gpuprobe/Gpu4Tight.cs` 등, `#if UNITY_EDITOR`, 제품 Compile 목록에 넣지 말 것)를 만들고 결과를 `docs/handoff/codex-tight-20261018-result.md`(한국어, 추측은 "추정")에 써라. 제품 C# 파일은 건드리지 말 것(다른 작업이 진행 중).

## 배경 (실측, Claude)

Arche 258~268초 본 카메라 GPU 3.7ms 중 약 78%(2.9ms)가 픽셀 수에 비례(해상도 50% 에서 1.58ms). 그리는 것은 `Sprites/Tile` 셰이더의 큰 반투명 안개: `fog.png`(682×682, alpha==0 인 픽셀 20%, alpha≤8 58%), `nes_large_fog.png`(1489×1489, 0: 49%, ≤8: 73%, alpha>0 bbox 64%), `nes_black_fog.png`(682×682, fog 와 비슷), `nev_fog.png`(100×100). 이미지는 `D:\얼불춤 맵 파일\Arche\` 아래에서 찾는다. 안개 스프라이트는 수백 개가 화면에서 겹친다(census 경계 상자 합 = 화면 93배, 상한).
지금 이 스프라이트는 게임이 만든 FullRect 메시(사각형 2삼각형)다. 완전 투명 픽셀(alpha==0)이 있는 부분의 삼각형을 빼면 래스터 면적이 줄어 GPU 시간이 준다. 단, 그림이 한 byte 라도 달라지면 폐기한다(사용자 결정).

## 사용자가 정한 규칙

- alpha ≤ 8 영역을 건너뛰는 것은 하지 않는다(그림이 달라짐). **alpha == 0 영역만** 건너뛴다. 이미지 데이터는 RGBA 8비트 원본 PNG 와 게임이 실제 올리는 형식(큰 이미지는 긴 변 1024 로 줄인 RGBA32, 압축 켜면 DXT5 — 가능한 범위에서 둘 다) 각각 알파를 재서 쓴다.
- Bilinear 보간 경계 안전 여유 **2~4px 이상**(텍셀 단위로 alpha>0 영역을 팽창, 줄인 텍스처는 줄인 크기 기준) 포함.
- 오차 **0 byte** 가 아니면 즉시 폐기. 1 byte 라도 다르면 그 조건·원인 추정을 적고 해당 방식을 폐기, 다른 지오메트리 방식(아래)으로 한 번 더 시도.

## 할 일

1. **지오메트리 후보**(같은 텍스처·같은 UV 매핑(정점 UV 가 텍스처 공간에서 같은 선형 함수여야 함), 같은 sprite pivot·ppu·rect 유지, `Sprite.OverrideGeometry(vertices, triangles)` 또는 Sprite.Create 의 Tight 메시):
   a. alpha>0 bbox(팽창 후) 하나의 직사각형(삼각형 2개 그대로, 크기만 작음).
   b. 격자 셀 마스크(예: 16×16 텍셀 셀 중 팽창한 alpha>0 와 겹치는 셀만 사각형 2개씩): 셀 경계가 공유 정점을 쓰는지/T-접합이 없는지 확인, 삼각형 수 상한.
   c. Unity 의 Tight 메시(alphaTolerance 조절).
   각 후보의 면적 비(FullRect 대비), 삼각형 수, 정점 수.
2. **0 byte 확인** (이게 핵심): 실제 안개 이미지 4종 × 지오메트리 후보 × 팽창 2/3/4/8px × 변환(회전 0/17/63°, 스케일 0.7/1.3/3.0 및 x 반전, 서로 다른 pivot) × 보간(Bilinear, Point) × mip 없음(게임 기본)/있음 × 겹침(같은 위치에 안개 2~3장 반투명) × 블렌드(Sprites/Default 알파 블렌드; 실제 Tile 셰이더는 Editor 에 없을 수 있으니 불가하면 이유와 한계를 적고, 게임의 `Sprites/Tile` 셰이더가 하는 일(IL/셰이더 파일에서 찾을 수 있으면 그 내용)을 적는다). 큰 해상도(RT 3440×1440 과 1720×720)도. 반복 렌더 불일치 0 과 양성 대조(일부러 alpha 1 영역을 잘라내면 검출)를 같이 보고.
   삼각형 정점이 바뀌면 UV 보간 기울기 반올림 때문에 불투명 내부에서도 1 byte 차이가 날 수 있다고 사용자가 우려했다 — 이 부분을 직접 판정: 차이가 난 픽셀의 위치(내부/경계), 값 차이.
3. **GPU 절약 추정** (0 byte 통과한 후보만): (i) 같은 Editor 에서 D3D11 타임스탬프로 FullRect vs 후보를 안개 N장 겹쳐(예 100장, 3440×1440) 그린 GPU 시간, (ii) 실제 Arche census(`%LOCALAPPDATA%\StutterFix\gpu-census-20261018-220315-835.tsv`, 안개 draw 행의 renderer 경로는 있으나 변환 행렬은 없음 — 경계 상자 픽셀 수 `aabb_pixels` 를 쓸 수 있다)로 면적 비를 곱한 추정 픽셀 감소 비율. 본 카메라 GPU 3.7ms 중 픽셀 비례 2.9ms 에 곱해 상한 절약 ms 를 식과 함께.
4. **게임 적용 시 필요한 것(설계만, 구현 금지)**: 게임이 스프라이트를 만드는 곳(`TextureManager`/`CustomSprite`, `ImagePrefetch` 가 줄인 크기에서 `Sprite.Create` 하는 곳, `TexCompress`)을 IL/소스로 찾아 어디서 OverrideGeometry 를 걸 수 있는지, 같은 이미지를 쓰는 장식이 스프라이트 하나를 공유하는지(수백 개가 한 스프라이트 → 지오메트리 계산 비용은 이미지당 한 번), `spriteUnscaledSize`/`GetDecorationWorldSize`/`ResizeCollider`(sprite.bounds 사용)/ 편집기 클릭 상자/ SpriteMask 에 미치는 영향(bounds 가 작아지면 달라짐 — 이게 있으면 bounds 는 그대로 두는 방법이 있는지), 모드 `LazySprite`/`ImagePrefetch` 와의 상호작용. 동적 배치/컬링: 메시가 작아지면 `Renderer.bounds` 도 작아져 가장자리에서 컬링이 달라지는지(Unity 는 Sprite bounds 로 컬링).

빌드 셋(`dotnet build -c Debug -p:Edition=Player -p:AutoTestBuild=1` 등)은 제품 변경이 없으니 가볍게 확인만. 임시 Editor 프로젝트는 `%TEMP%` 에 두고 출력은 `docs/handoff/codex-tight-20261018-*` 로 저장.
