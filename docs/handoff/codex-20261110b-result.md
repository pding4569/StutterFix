# 2026-11-10 코덱스 작업 B 결과

`E:\StutterFixWork\wt-v14prod`에서 불투명 정보, `occlude` 진단, Claude용 측정 스크립트를 추가했다. 기존 `AutoTest.cs`의 `decotexmin` 명령과 `GpuHide.cs`의 텍스처 진단 변경 및 기존 미추적 자료를 유지했다. 커밋·푸시·게임 실행·게임 설치 DLL 교체는 하지 않았다.

실게임 성능 및 무손실 판정은 **미측정**이다. 아래 검사는 CPU 계산과 빌드에 한정되며, GPU 절감량·실제 가리개 개수·픽셀 차이·런타임 할당 0을 입증하지 않는다. 제품 설정에는 연결하지 않았다.

## 구현

### 이미지 해독 작업 스레드

- `ShapeCellsCpu.Compute(..., out OpaqueInfo)`에서 기존 점유 칸과 별도로 같은 16×16/32×32 격자의 완전 불투명 비트를 계산한다. 각 칸의 실제 텍셀을 모두 검사하며 알파 254도 불투명으로 인정하지 않는다. 기존 점유 칸의 건너뛰기 알고리즘을 불투명 검사에 사용하지 않는다.
- RGB24는 알파 1, RGBA32/ARGB32는 해당 알파 바이트를 검사한다. 작은 이미지에도 적용한다. 다른 형식·실패·빈 포인터는 모름이다.
- `ImagePrefetch.Item`에 결과를 전달하고 텍스처 바인딩 시 `RegisterOpaque`로 등록한다. 메인 스레드에는 픽셀 검사·GPU 읽기·격자 재계산을 추가하지 않았다. 등록 자체의 사전/참조 처리 비용은 있다.
- `TryGetOpaque(tex, out bits, out allOpaque)`로 조회한다. 손실 압축된 텍스처 및 크기/형식 불일치는 제외한다. 텍스처 참조는 약한 참조로 보관한다.
- 게임의 최초 `Apply` 이전에 등록되므로 첫 조회에서 `Texture.updateCount`를 고정하고, 이후 변경되면 사용하지 않는다. 최초 조회 이전 외부 픽셀 변경까지 검증하는 구조는 아니다.

### `occlude census`, `occlude hide on|off`

`Occlude.cs`와 순수 계산 `OccludePolicy.cs`를 DEV/AUTOTEST에서만 컴파일한다. 명령 시 현재 씬의 렌더러와 셰이더 정보를 준비하고, 이름이 `Camera`인 메인 카메라의 `onPreCull`에서 현재 상태로 판단한다. 카메라가 여러 개로 모호하면 거절한다. census는 한 번 실행하며 콜백이 없으면 10초 후 종료한다.

가리개는 다음 조건을 모두 만족하는 SpriteRenderer다.

1. 현재 켜짐·활성·카메라 레이어·절두체 조건을 만족하고 `forceRenderingOff`가 아니다. 부모 변경도 다시 검사한다.
2. `GpuCensus.IsDecoration`이며 `scrVisualDecoration`이 존재한다. 마스크·SortingGroup·blendMode·활성 blendModeEffect는 제외한다.
3. 단일 재질/패스, renderQueue 3000, `Sprites/Default` 또는 `Sprites/Tile`이다. 패스가 켜져 있어야 한다. 로컬 및 적용 가능한 글로벌 키워드는 점 샘플링/PixelSnap만 허용한다. 알려진 스텐실 프로퍼티도 거절한다.
4. SpriteRenderer 색, 재질 `_Color` 및 `_RendererColor`의 알파가 정확히 1이다. PropertyBlock이 있는 가리개는 해석 불확실성을 피하려고 제외한다. 추가 재질 `_Flip` 변환도 제외한다.
5. 미리 계산한 전체 텍스처가 완전 불투명하거나, 일반 Sprites/Default의 sprite rect와 쌍선형 1텍셀 여유에 걸친 칸이 모두 불투명하다. 후자는 Clamp 및 기본 UV 배율/오프셋만 허용한다. Tile/반복은 전체 텍스처가 불투명할 때만 허용한다.
6. mipmap, 높은 이방성, 분리 알파, atlas packed sprite, sliced/tiled drawMode는 제외한다.
7. 실제 sprite 메시가 사각형을 채우는지 확인한다. Tight도 네 모서리/두 삼각형으로 꽉 찬 사각형이면 허용한다. 두 삼각형의 공유 변이 대각선인지 검사하여 겹친 삼각형의 면적 합만으로 통과하지 않게 했다. 그 밖의 복잡한 Tight/사용자 메시를 추정해 통과시키지 않는다.
8. 실제 변환과 flip을 적용한 네 꼭짓점이 near/far 안에 있고, 투영한 볼록 사각형이 화면 네 모서리에서 **바깥쪽 1픽셀**까지 포함한다. 회전된 AABB만으로 판단하지 않는다.

Tile의 반복은 UV 샘플링 범위이며 별도의 화면 덮임 증거가 아니다. `repeatX/Y` 필드로 사각형을 부풀리지 않고 실제 sprite 메시/변환을 사용한다. 로컬 게임 코드의 `SetTile` 및 기존 `GpuDecoBlend.EffectiveRepeat`도 확인했다. 셰이더의 유효 반복 값과 장식 필드가 다를 수 있다.

가리개 중 sortingLayer 값/순서가 가장 아래인 항목을 골라, 그보다 **엄격히 먼저** 그려지는 항목만 센다. 같은 층/순서는 모두 유지한다. 모든 가리개 자신도 숨기지 않는다. 장식 수·투영 AABB 면적 합/화면 면적과 비장식 수를 따로 출력한다. 면적은 실제 픽셀 셰이더 호출 수가 아닌 상한 추정이다.

**보수적 범위:** 가려지는 쪽도 stencil/GrabPass 등 부작용을 검증할 수 있는 위 두 단일 패스 스프라이트 셰이더만 허용했다. Renderer 자체는 SpriteRenderer 외 MeshRenderer 등도 조사한다. 다른 셰이더의 행성·입자·타일은 카운트/숨김에서 빠질 수 있으므로 census의 비장식 숫자를 씬 전체의 가려진 비장식 총수로 해석하면 안 된다. 알 수 없는 셰이더의 하드코딩된 스텐실 쓰기를 추정해 숨기지 않았다.

hide는 해당 장식의 `forceRenderingOff`만 바꾼다. 원래 값은 카메라 렌더 직후 복원하고, 다른 카메라의 PreCull 진입 때도 먼저 복원한다. off·실패·컴포넌트 비활성/파괴·메인 카메라 파괴·AutoTest 종료·모드 해제에서도 복원한다. `gpuhide`/`gpucensus`/`gpusplit`/pixeldiff의 실험 세션은 서로 배제한다.

매 프레임에는 준비한 목록/배열/List를 재사용하며 키워드 배열/씬 전체 검색/픽셀 읽기를 하지 않는다. census의 일회성 면적 계산은 별도다. off 로그에 `mean_us`, `callback_alloc_bytes`, `last_covers`, `last_hidden`을 남긴다. **할당 0은 실게임 로그에서 확인해야 한다.** 준비 시점 이후 새 객체/새 sprite는 자동 등록하지 않으며 다시 on을 호출하면 갱신한다. 재질 슬롯 수가 준비 시점보다 커지는 등 동적 변경 시 List 확장 가능성도 로그로 확인해야 한다.

## Claude가 실행할 스크립트

- 정본: `tools/occl1.py`
- 요청 위치 복사본: `E:\StutterFixWork\occl1.py`
- 실행 예: `python E:\StutterFixWork\occl1.py s7 rlP`
- 맵/설정은 기존 `bench_v14.MAPS`/`CFG2`를 사용한다. 7777(`s7`)의 기준 위치는 인계 문서대로 124초로 덮어쓴다.

순서: 일시정지 → all GPU 창 및 PNG → census → hide on GPU 창 및 PNG → off → all2 GPU 창 및 PNG → 인접한 hide on/grab/save/clear → off/grab/save/clear를 두 번 반복 → 명시적 off 및 종료. 각 GPU 창은 reset 후 4초이며 비싼 framecmp 작업은 창 뒤에 수행한다. 실행 직전 해당 이름의 이전 PNG를 지워 오래된 결과 사용을 막는다. 실패/누락된 GPU 통계/누락 PNG는 오류로 처리한다.

비교는 RGBA 전체에서 픽셀별 최대 채널 차를 사용하여 `>0`, `>8` **픽셀 수와 비율**, 최대 차이를 출력한다. all-hide 외에 all-all2 시간 변화와 인접한 두 쌍을 따로 표시한다. 시간에 따라 변하는 화면에서 이 결과만으로 원인을 가림으로 단정하지 않는다.

`FrameCompare.Grab` 코드 확인: 캡처 옵션 적용 → `yield return null` → `WaitForEndOfFrame` → 독립 RenderTexture에 화면 복사 → AsyncGPUReadback 완료 → 이름별 shot 등록 순서다. `FrameCompare.Step`은 읽기가 끝나기 전까지 false를 반환한다. 따라서 다음 `save`나 hide 전환은 이전 `grab`이 완료된 뒤에 실행되고, 저장이 한 이름씩 밀리지 않는다. 다만 인접 두 촬영도 같은 게임 프레임은 아니며, 상태 수집/읽기 대기 시간 동안 화면이 변할 가능성은 남는다. sourceFrame/completionFrame은 기존 framecmp 로그에 기록된다.

스크립트는 실행하지 않았다. AST에서 `steps_for` 함수만 분리해 합성 경로로 순서를 검사했으며, `bench_v14`/게임 실행기/측정 main은 실행하지 않았다.

## 검증

- 요청 빌드: `dotnet build StutterFix.csproj -c Release -p:Edition=Player -p:AutoTestBuild=1` — 오류 0. 세부 로그 `out/occlude-build.txt`.
- 일반 Player 빌드도 오류 0이며, 진단 파일은 제품 빌드에서 제외된다. 로그 `out/occlude-player-build.txt`. 최종 AutoTest 빌드 경고는 50개, 일반 Player는 58개이며 경고 없는 빌드라는 의미는 아니다.
- `dotnet run --project tools/occlude-fixture/OccludeFixture.csproj -c Release` — 불투명 정보 126개 지원 형식 사례/1,393개 검사, 화면 포함/회전/반전/1픽셀 여유/비정상 도형/정렬 동점 등 14개 검사 통과. 실제 `ShapeCellsCpu.cs`와 `OccludePolicy.cs`를 링크한 CPU 전용 검사이며 Unity API는 stub이다. 로그 `out/occlude-fixture.txt`.
- Python 문법과 7개 grab/save/clear 세트, 인접 on/off 두 쌍의 명령 순서 검사 통과. 로그 `out/occlude-script-check.txt`.
- `git diff --check` 통과. 기존 변경을 되돌리거나 커밋하지 않았다.

## 남은 위험과 다음 단계

- 정렬 동점·SortingGroup은 미지원이며 그대로 유지한다. 하드코딩된 스텐실/화면 복사 부작용을 알 수 없는 셰이더도 제외한다.
- BGStaticCam/BGMovingCam 등은 hide를 적용하지 않는다. 동일 renderer의 여러 카메라 사용과 중첩 Camera.Render의 실제 콜백 순서, 게임/다른 모드가 같은 시점에 forceRenderingOff를 다시 쓰는 경우는 실게임 확인이 필요하다.
- 필터는 기본 경로를 유지하고 PNG에 포함한다. 시간 의존 필터/화면 변동은 인접 쌍에도 남을 수 있다. 최초로 무손실을 주장하려면 안정적인 대조 프레임 및 복원 결과가 필요하다.
- 텍스처의 첫 조회 이전 변조, 같은 Sprite 인스턴스의 준비 이후 OverrideGeometry, 준비 후 새 객체 생성은 제품 수준 추적이 아니다. 현재 목록 기반 진단을 계속 켜 두는 장기 제품 기능으로 보지 않는다.
- GpuDeco가 이미 `forceRenderingOff=true`로 소유한 원본은 건드리지 않는다. 해당 GPU 구간 자체는 줄이지 않으며, GpuDeco 프록시도 검증된 sprite 가리개로 취급하지 않는다. 원본 경로와 `gBsSp`를 각각 측정하고 소유 항목 누락을 분리해야 한다.
- 다음 측정은 Claude가 스크립트로 수행한다. 우선 가리개/숨김 수, 콜백 할당 및 판단 비용, main Camera GPU ms/PS 호출, 인접 PNG 차이와 all-all2 변화를 함께 본다. 실제 개선이 없거나 픽셀이 다르면 제품 경로에 채택하지 않는다.
- 제품화는 가려진 장식을 GpuDeco 구간/패킷에서 제외하는 방식과 원본 경로의 CullingGroup/카메라별 culling 경로를 검토한다. 동적 장식/재질/이미지/카메라 변화 추적과 복원 검증을 먼저 갖춰야 한다.
