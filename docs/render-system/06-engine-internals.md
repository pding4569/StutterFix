# 엔진 안쪽 (UnityPlayer 6000.3.25f1, 심볼과 디스어셈블)

공개 심볼 96,054개는 `night-results/unityplayer-syms.tsv`(저장소 밖). 함수 위치는 이 엔진 버전 기준이다. 어셈블리를 읽은 것이라 **필드 이름은 추정**이며, 순서 자체는 명령어에서 직접 읽었다.

## 투명 물체 정렬 순서

`ForwardShaderRenderLoop::RenderObjectSorter<0>::operator()`(RVA 0x3cc870). 거리를 앞쪽에서 비교하므로 투명 경로로 본다. `<1>`(0x3cc680)은 상태를 먼저 비교하고 거리를 뒤에 비교한다(불투명으로 추정).

| 차례 | 비교 대상 | 먼저 그리는 쪽 | 해석 |
|---|---|---|---|
| 1 | RO+0x2c u32 | 작은 값 | 정렬 레이어+순서(`GlobalLayeringData`) |
| 2 | RO+0x10 i16 | 작은 값 | 렌더 큐 |
| 3 | RO+0x20 float | 작은 값 | 거리 키 |
| 4 | RO+0x30 상위 비트 (0x3ba620) | 작은 값 | 정렬 그룹 |
| 5 | 항목+0x1c u16 | 작은 값 | 하위 메시·패스 계열(추정) |
| 6 | 항목+0xc 0x1000000 비트 | 비트가 큰 쪽 | 추정 불가 |
| 7 | 항목+0x16 u16이 0이 아니면 정적 배칭 순서 | | 배칭 |
| 8 | 항목+0x10 u32 | 작은 값 | **셰이더 ID** (측정과 일치) |
| 9 | RO+0 포인터가 다르면 `[포인터+8]` 부호 있는 정수 | 작은 값 | **재질 인스턴스 ID** (측정과 일치) |
| 10 | RO+8 u32 | 작은 값 | 추정 불가 |
| 11 | 항목+0xc 하위 바이트 | 작은 값 | 패스 번호(추정) |
| 12 | 항목+8 int | 작은 값 | 노드 번호(최종 순서) |

**읽은 것:**

- 메모리의 "동점은 셰이더 ID → 재질 ID, 거리 차이가 순서를 강제함"이 코드와 맞는다.
- 재질 비교는 **부호 있는 오름차순**이다. 측정에서 실행 중 만든 객체 ID가 음수였으므로(`-903166` 등), 음수가 계속 작아진다면 **나중에 복제한 재질이 먼저(아래에) 그려진다**. ID가 생성 순서대로 줄어드는지와 복제 시점은 **미측정**이다.
- 재질이 같으면 RO+8과 패스를 거쳐 노드 번호까지 간다. 대체 경로에서 묶음 안 순서를 재현하려면 재질 ID 순서로 묶음을 나누면 된다는 근거다.

## Transform 계층

Unity는 루트와 그 아래 전체를 계층 하나로 묶어 저장한다([hierarchyCapacity 문서](https://docs.unity3d.com/Documentation/ScriptReference/Transform-hierarchyCapacity.html)). Unity 엔지니어의 [포럼 설명](https://discussions.unity.com/t/complexity-of-hierarchy-affects-performance/148298): Transform 하나가 바뀌면 그 계층이 더러워지고, 변경 목록을 만들 때 **더러운 계층 안의 Transform을 전부 훑으며, 큰 계층은 스레드 하나로 처리된다**. 접근 동기화도 계층마다 하나다. 오래된 글이고 지금 버전에서 같은지는 확인이 필요하다.

**이 게임의 구조(코드):**

- 장식 전부: `CreateDecoration`이 `Instantiate(prefab, base.transform)`으로 `Decoration Container` 아래에 만든다. Arche는 장식당 켜진 Transform 5개면 약 14만 개가 계층 하나다.
- 타일 전부: `GetFloorContainer`의 `Floors` 아래.
- 곡 중에는 거의 매 프레임 그 계층의 무언가가 움직인다.

**이 엔진 버전의 코드(`TransformChangeDispatch::ScanAndHandleChangesJob`, 0x675a50):** 작업 단위는 (계층, 시작 번호)이고, 계층의 합친 변경 비트가 시스템 마스크와 겹치면 시작 번호부터 최대 0x2000개(8,192)의 변경 비트를 **하나씩 읽는다**. 전부 훑는다는 설명은 맞다. 다만 큰 계층도 8,192개씩 나눠 작업에 태우므로 "스레드 하나"라는 옛 설명은 이 버전에 맞지 않을 수 있다. 꺼진 Transform도 계층에 들어 있다면 Arche는 장식 9개 구조 × 2만 8천으로 약 26만 개까지 볼 수 있다(실제 수는 `rtcensus`로 확인).

프로파일에서 `TransformChangeDispatch` 관련 메인 스레드 비율은 2~4%다. 작업 스레드 몫은 표본에 없다. 그릴 때 `CalculateGlobalPosition`·`SetGlobalT` 등도 관련될 수 있다.

**시험:** 자동 시험 `flatten deco|floor`(`HierarchyFlat.cs`)가 곡 시작 전에 자식을 루트로 옮긴다. 비교 항목은 FPS, 메인 스레드 단계 비용, 끊김이다.

**제품으로 넣을 때 걸리는 것:**

- 부모가 기본값이 아니면 `pivotTrans.localPosition` 의미가 바뀐다. 시험 로그로 확인한다.
- `ClearDecorations`가 `GetComponentsInChildren<Transform>`으로 지운다. 떼어 낸 장식은 따로 지워야 한다.
- `GameObject.Find("Floors")`와 타일 지우는 곳도 같다.
- 편집기의 `transform.parent` 사용, 다른 모드.
- 계층이 수만 개로 늘 때 작업 분배 비용이 오히려 커질 수 있다.

## 셰이더 비용 (D3D11 바이트코드)

`tools/rendersys/scan_shaders.py`가 픽셀 셰이더 명령어와 텍스처 샘플을 센다. 표는 `data/shader-stats.tsv`. 명령어 수는 분기·루프 횟수를 반영하지 않으며 실제 GPU 시간이 아니다.

| 셰이더 | 명령어 | 샘플 | 비고 |
|---|---:|---:|---|
| `Sprites/Tile` (이미지 장식) | 8~10 | 1 | 가볍다 |
| `Legacy Particles/Additive` (더하기 블렌드) | 12~16 | 1~2 | |
| `Hidden/BlendModes/SpritesDefault/Grab` | 16 중앙, 최대 102 | 2~3 | 모드마다 다름 |
| `ADOFAI/FloorMesh` (타일) | **92** | **5** | 장식의 약 10배 |
| `ADOFAI/HoldShader` | 18 | 1 | |
| `Legacy Color_Chromatic_Aberration` | 11 | 3 | 필터 빈도 1위 |
| `Hidden/FisheyeShader` | 8 | 1 | 빈도 2위 |
| `Legacy Color_GrayScale` | 7 | 1 | |
| `Legacy Distortion_Wave_Horizontal` | 12 | 1 | |
| `Legacy Blur_GaussianBlur` | 24 | 5 | |
| `Blur_Movie` | 36 | 1 | 루프 2 |
| `AAA_Super_Computer` | 162 | 1 | 루프 5 |
| `RainFX` | 219 | 9 | 드문 필터 |
| `Deep_OilPaintHQ` | 211 | 48 | 드문 필터 |

**읽은 것:** 자주 쓰는 필터는 대부분 가볍다. 무거운 건 일부 특수 필터다. 타일 셰이더는 픽셀당 비용이 높아서, 타일이 화면을 많이 덮는 맵에서 GPU 시간이 늘 수 있다(DD에서 타일을 숨기면 GPU가 줄었다는 측정과 방향은 맞다).
