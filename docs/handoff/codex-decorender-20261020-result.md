# S2 decorender 구현 결과 — 2026-10-20

## 완료 범위와 검증 경계

`E:\StutterFixWork\wt-decorender`에서 `decorender on|off|compare|state`를 구현했다. 게임 실행·설치·커밋은 하지 않았다. **실제 Arche 화면 동일성 수치는 아직 없다.** 아래 비교 명령을 사용자가 게임에서 실행해야 한다.

- 새 파일: `DecoRender.cs`, 파일 전체 `#if DEV || AUTOTEST`.
- `StutterFix.csproj`: 기존 MeshBench와 같은 조건부 Compile.
- `AutoTest.cs`: `DecoRender.Step(arg, modDir)` 연결. compare는 완료될 때까지 해당 AutoTest 단계에서 기다린다.
- `StutterFix.cs`: `Main.Unload` 정리에 `DecoRender.Stop` 추가.
- `SceneReset.cs`: DEV/AUTOTEST에서 ResetScene 진입 시 즉시 Stop.

## 빌드

```powershell
dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1
dotnet build -c Release -p:Edition=Player
```

두 빌드 모두 성공: AutoTest 경고 37개 / 오류 0개, 일반 Player 경고 36개 / 오류 0개. 경고는 참조 어셈블리 버전 충돌, 기존 deprecated API·미사용 필드 등이며 `DecoRender.cs` 경고는 없다. 빌드 로그는 로컬 `obj/decorender-build-autotest.log`, `obj/decorender-build-player.log`.

실행용 산출물은 **`bin/PlayerAuto/StutterFix.dll`**. 일반 `bin/Player/StutterFix.dll`에는 이 실험 코드가 제외된다. 설치는 사용자가 한다. `git diff --check` 통과. Unity 렌더링·GPU CopyTexture·PNG 출력·복원 동작은 빌드만으로 검증되지 않는다.

ILSpy의 빌드 DLL 타입 목록으로 PlayerAuto의 `StutterFix.DecoRender` 포함과 일반 Player의 제외를 확인했다. AutoTest DLL 크기는 3,449,344 bytes, SHA256은 `4DEDF3C02E2D7384FF8B016BB4EC43EEFFDCFEC388D39DDA041C57AA55D3B57A`. `py -3 tools/check-ui-encoding.py`도 `SFENC001: clean`으로 통과했다.

## 명령

| 명령 | 동작 |
| --- | --- |
| `decorender on` | 현재 장식 집합을 고정 선택하고 아틀라스/그룹 메시 준비. 처음 카메라 렌더부터 대체한다. 이미 active면 state 출력. |
| `decorender compare` | 다음 본 카메라 PostRender에서 같은 상태로 원본/그룹을 동기 Render 두 번. 저장 완료까지 AutoTest 단계 유지. |
| `decorender state` | active, 장식/그룹/아틀라스/정점 수, 아틀라스 크기·밉 수, Build 시간, 관측 카메라 수, 마지막 compare·중단 사유. |
| `decorender off` | 그룹 즉시 비활성화, 원본 forceRenderingOff 복원, 메시·재질·아틀라스·세션 오브젝트 Destroy. 반복 호출 가능. |

`on` 시 정확히 `scrVisualDecoration`인 장식 중 활성 GameObject, 켜진 SpriteRenderer, 존재하는 Sprite, rendererEnabled와 알파가 보이는 것을 선택한다. hitbox, 마스크, 메시 장식, maskInteraction, Simple 이외 drawMode, 부모 SortingGroup은 제외한다. `isVisible`의 이전 카메라 결과에 의존하지 않으며 화면 밖이라도 이 조건을 만족하면 포함된다. 이후 위치·색·flip과 enabled/알파 변화는 매 렌더에 반영한다. 선택하지 않았던 장식이 나중에 나타나면 원래 SpriteRenderer로 계속 그린다. 새 집합은 `off → on`으로 선택한다.

sprite/원본 재질/재질 속성/정렬/Unity layer/반복 횟수가 바뀌거나 선택 장식이 파괴되면 자동 off한다. manager/장식 리스트 교체·개수 변화, 본 카메라 교체, 활성 장면 변경, 재생 종료, SceneReset도 off 사유다. 자동 off 후 `state`의 stop 사유를 확인하고 필요하면 다시 on한다.

## 아틀라스

원본 텍스처 전체를 대상으로 하며 Sprite별 재추출/쿼드화/축소를 하지 않는다.

- 밉맵 없는 텍스처는 sampler가 같을 때 shelf packing. RGBA32, 가장자리 4 texel 패딩. 패딩 픽셀은 원본 wrap U/V(Clamp/Repeat/Mirror/MirrorOnce)에 따라 만든다. 빈 공간은 power-of-two 크기로 줄인다.
- 밉맵이 있거나 반복 횟수가 1이 아닌 텍스처, 패딩까지 넣으면 최대 크기를 넘는 텍스처는 개별 아틀라스. 원본 크기·정규화 UV·밉 개수·wrap을 유지한다. 따라서 실제 sampler/원본 크기에 따라 아틀라스가 한 장보다 많아질 수 있다.
- 원본 filterMode, wrap U/V, anisoLevel, mipMapBias, sRGB/linear 구분을 유지한다. mipless 공유 아틀라스는 mipless다.
- 압축·읽기 불가 원본도 GPU Blit → ARGB32 RenderTexture → ReadPixels로 디코드한다. 밉맵 원본은 각 mip를 같은 포맷의 mipless 임시 Texture2D에 Graphics.CopyTexture한 다음 Blit하여 해당 mip를 명시적으로 고른다. 원본 필터/밉 bias를 수정하지 않는다. Apply(false)로 원본 mip를 임의 재생성하지 않는다.
- 아틀라스 업로드 후 CPU 읽기 복사본을 해제한다. 원본 텍스처별 wrap/filter/mipmapCount/isReadable/포맷/크기/aniso/bias, 아틀라스별 크기·밉 수·RGBA32 이론 GPU bytes/MiB·패딩·원본 수를 로그에 남긴다. 4096² mipless RGBA32는 64 MiB이며 원본과 디코드용 임시 자원은 별도다.

GPU CopyTexture가 지원하지 않는 압축 포맷/작은 mip, streaming/global mip 제한, GPU 포맷 변환, 감마 변환·양자화의 실제 동작은 실행 확인이 필요하다. API 예외가 나면 원본 렌더 상태로 복구한다. 비표준 shader가 UV를 재가공하거나 강한 비등방 필터가 패딩 밖을 샘플링하는 경우에는 공유 아틀라스와 차이가 날 수 있다. 아틀라스 생성 비용은 on에서만 발생한다.

## 메시·재질과 카메라

`sprite.vertices/triangles/uv`를 저장하고, 각 카메라의 `Camera.onPreRender`에서 `SpriteRenderer.transform.localToWorldMatrix`를 읽는다. flipX/Y를 로컬 정점에 적용한 다음 월드 좌표로 변환한다. 정점 색은 그 시점 SpriteRenderer.color, UV는 원본 텍스처 픽셀 좌표를 아틀라스 사각형으로 재매핑한다. 월드 정점을 담으므로 그룹 GameObject는 부모 없이 identity Transform이다. UInt32 인덱스의 동적 Mesh를 사용한다.

그룹 기본 키는 `(sortingLayerID, sortingOrder, atlas)`이며 Unity GameObject layer와 실효 재질 속성이 다르면 추가 분리한다. 이 추가 분리는 서로 다른 카메라 cullingMask/재질의 동작을 보존하기 위해 필요하다. MeshRenderer에 원본 sortingLayerID/sortingOrder와 Unity layer를 적용한다. 따라서 42개 sortingOrder가 있어도 실제 그룹 수가 반드시 42개인 것은 아니다.

같은 상태의 재질 인스턴스는 합친다. 원본 Material을 복제하여 shader/keywords/renderQueue를 유지하고, shader에 선언된 Color/Vector/Texture/Float/Int의 MaterialPropertyBlock 실효 값을 반영한다. `_MainTex`만 아틀라스로 대체한다. 색은 정점에서 공급하므로 `_RendererColor=white`, flip은 CPU에서 수행하므로 `_Flip=(1,1,1,1)`. `_Color`는 원본 값을 유지한다. Sprites/Default의 정점 색 × `_Color` × `_RendererColor`에서 색/flip이 이중 적용되지 않게 하는 의도다. 원본은 spriteRenderer.color를 게임 ApplyColor에서 갱신한다는 것을 로컬 게임 DLL의 `scrVisualDecoration.ApplyColor`로 확인했다.

split alpha, 외부 알파, 비표준 MPB RendererColor/Flip, MainTex override/비항등 ST는 지원하지 않아 거절한다. shader에 선언되지 않은 사용자 uniform/MPB 속성은 완전 열거할 수 없어 재현을 보장하지 않는다. multipass, renderer 전용 엔진 속성, 커스텀 shader의 추가 정점 채널도 픽셀 비교가 필요하다. 셰이더를 새로 만들거나 DecoGpu 번들을 쓰지 않는다.

Unity는 onPreRender 전에 컬링하므로 onPreCull에서 원본 장식 bounds를 합쳐 그룹의 명시적 world bounds로 지정하고 renderer를 활성화한다. 첫 렌더 전에 빈 메시로 컬링되지 않도록 topology를 먼저 준비한다. 정점의 최종 월드 좌표는 그 뒤 onPreRender에서 쓴다. 이 시점 관계의 실제 게임 동작도 실행 검증 대상이다.

모든 관측 카메라마다 메시 순서를 갱신하며 같은 Unity layer를 통해 해당 카메라에 그려진다. 처음 관측한 카메라 ID/name/mask/투영/투명 정렬 모드를 로그에 남긴다. `state`에는 Build의 마지막/평균 카메라당 ms와 현재/평균 프레임당 누적 ms를 남긴다. 이는 검증·정렬·정점 작성·메시 업로드의 Stopwatch CPU 시간이며 GPU 시간이나 실제 프레임 개선 수치가 아니다. on/compare와 onPreCull의 비용은 이 Build 시간에 포함하지 않는다.

## 같은 sortingOrder 내부 순서 조사

[Unity 공식 2D Sorting 문서](https://docs.unity3d.com/2022.3/Documentation/Manual/2DSorting.html)는 sorting layer/order, render queue, 카메라 거리, SortingGroup, material/shader, 마지막 내부 tiebreaker를 설명한다. 마지막 동률 순서는 내부 처리이므로 사용자가 제어할 수 있는 안정된 renderer ID 규칙으로 보장되지 않는다.

구현 규칙은 각 그룹 안에서 **카메라 거리 내림차순 → 원래 Renderer instanceID 오름차순**이다. 원본 SpriteSortPoint(center/pivot)를 반영하고 perspective에서는 거리 제곱, orthographic에서는 카메라 forward 투영, CustomAxis에서는 지정 축 투영을 사용한다. 동일 그룹의 renderQueue·shader·재질은 이미 같다. 위 규칙을 시작 로그에 명시한다.

**instanceID 오름차순은 실험 가설이다. Unity 내부 최종 순서를 확인했다는 뜻이 아니다.** 게임/별도 렌더 실험은 이번 지시에서 금지되어 실행하지 않았다. 같은 순서값의 외부 renderer가 선택 장식 사이에 끼어드는 경우, 서로 다른 아틀라스/재질 그룹이 서로 섞여야 하는 경우, 그룹 대표 bounds 중심의 거리 정렬은 단일 메시로 완전히 보존하지 못한다. 이 문제가 compare의 검증 대상이다. 따라서 화면 동일 판정은 픽셀 수치 없이 내리지 않는다.

## 비교 출력과 유효 범위

비교는 본 카메라의 PostRender에서 수행한다. 그 시점 최종 상태로 한 번 메시를 채운 후, disabled 임시 Camera에 본 카메라 설정·Transform·view/projection을 복사한다. 본 카메라의 viewport 픽셀 크기 ARGB32 RT를 두 번 동일하게 초기화한다. 첫 렌더는 원본 forceRenderingOff 복원 + 그룹 비활성, 두 번째는 대체 상태. 두 렌더 사이 Update/LateUpdate/곡 진행을 실행하지 않으며 장식 UpdatePosition/색 계산도 직접 호출하지 않는다. 본 카메라 targetTexture를 바꾸지 않는다.

`shots/decorender-<UTC>-f<frame>-...`:

- `original.png`: 선택 장식을 원본 SpriteRenderer로 그린 전체 카메라 영상.
- `group.png`: 선택 장식만 그룹 메시로 바꾼 영상.
- `diff16.png`: 채널 절대차 ×16, 255 포화. alpha-only 차이도 보이도록 B에 max(B차,A차)를 표시하고 출력 alpha는 255.
- `metrics.txt`: frame/song/해상도, 채널별 maxRGBA, 전체 최대차, 최대차 위치(좌하단 원점), 임계값 >0/>1/>2/>4/>8인 픽셀 수/전체 수/비율, snapshotStable, 저장 경로.

픽셀 임계값은 RGBA 중 최대 차이를 기준으로 센다. 두 렌더 전후 선택 장식 행렬/색/flip/스프라이트/재질/정렬/visibility와 장면을 확인한다. `snapshotStable=False`면 같은 상태의 유효 표본으로 채택하지 않는다. 카메라 Render 콜백이 외부 효과나 전역 상태를 바꿀 가능성은 별도 한계다.

임시 Camera는 **원본 Camera의 스크립트 이미지 효과/command buffer/다른 카메라의 합성을 복제하지 않는다.** 두 비교 영상은 같은 단일 카메라 조건이지만 실제 최종 게임 화면 전체의 동등성을 증명하지 않는다. 전체 cullingMask로 장식 외 대상도 두 번 동일하게 렌더한다. sortingLayer는 GameObject layer와 다르므로 sortingLayer를 cullingMask로 잘못 필터링하지 않는다. 비교가 끝나면 대체 상태를 유지하고 임시 자원을 정리한다. 화면 동일성을 보려면 PNG뿐 아니라 실제 게임 화면에서 on/off도 확인해야 한다.

## 사용자가 실행할 순서

설치 후 AutoTest 입력 예시(아래 `<Arche 경로>`는 실제 `.adofai` 파일 경로로 치환):

```text
keep
open <Arche 경로>
auto on
seek 262 8
waitsongpos 264
decorender on
decorender compare
decorender compare
decorender compare
decorender compare
decorender compare
decorender state
decorender off
decorender state
```

compare 하나가 끝난 다음 다음 줄로 넘어가므로 각 비교는 서로 다른 게임 프레임/곡 시각일 수 있지만, 각 비교의 두 영상은 동기 렌더다. 비교 부하는 요청한 비교 프레임에만 들어간다. 여러 시각을 따로 보려면 compare 사이에 `waitsongpos 268`, `waitsongpos 272` 등을 둔다. 중간에 자동 off되었다면 stop 사유 확인 후 on을 다시 호출한다.

1. 시작 로그에서 선택 장식 수·원본 텍스처 수·그룹 수·아틀라스 GPU MiB·shader/queue/keywords를 확인한다.
2. 각 metrics에서 snapshotStable=True인지, 각 임계값 픽셀 수가 얼마인지 기록한다. max=0만 완전 동일 표본이다.
3. diff16에서 겹침 가장자리/색/반복/샘플링 차이를 구분한다. 원인 확인 없이 차이를 허용하거나 동일하다고 결론내리지 않는다.
4. off 직후 실제 게임 화면과 state의 active=False를 확인한다. 추가로 on → 편집기 복귀/다시 열기, on → Unload에서 자동 복구를 확인한다.

이번 세션은 구현·두 빌드·조건부 포함 확인까지만 수행했다. 실제 장식 수/아틀라스 크기/동일성 픽셀 수/프레임당 CPU ms는 실행 로그에서 채워야 하며 측정값을 미리 가정하지 않았다.
