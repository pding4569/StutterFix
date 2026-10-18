# Arche GPU 그리기 호출 조사 2 — 2026-10-18

`gpucensus`의 판정/출력을 보강하고 `gpuhide` 진단을 추가했다. **게임 실행·Mods 설치·커밋은 하지 않았다.** 제품 렌더링 최적화는 적용하지 않았다. 실제 Arche 후보 수·GPU 절약·복원 동작은 게임에서 미검증이며, 아래 명령은 Claude가 후속 실행할 절차다.

## 변경 파일

- `GpuCensus.cs`: 요청 후 다음 본 카메라 `Camera.onPreRender`에서 한 번 조사. 명시적 cullingMatrix, Sprite bounds 대조, 면적 0과 컬링 분리, 이유별 출력, 순서별 재질/텍스처/MPB/키워드와 가정별 run 수.
- `GpuHide.cs`(신규): 그룹별 일시 숨김, 전/중/후 gpuprobe 집계, 원래 forceRenderingOff 저장·복원, 취소·해제 처리.
- `GpuProbe.cs`: 기존 쿼리를 읽는 Snapshot API. probe 종료 때 진행 중인 숨김 취소. 숨김 중 probe 재설치 금지.
- `AutoTest.cs`: gpuhide 명령 1줄. `StutterFix.csproj`: DEV/AUTOTEST 조건부 Compile 1줄. `StutterFix.cs`: 기존 조건부 Unload에 GpuHide.Stop/GpuCensus.Stop 2줄.
- 네이티브 gpuprobe/sfnative, FuseLate 및 제품 최적화 파일은 변경하지 않았다. 빌드 산출물은 저장소 bin/obj에만 생성했다.

## census 오류: 확인한 문제와 아직 확인 못 한 원인

기존 `gpu-census-20261018-213606-940.tsv`를 직접 읽었다. Camera의 후보 6개, mask 제외 12개, outside 2,695개를 확인했다. 이 TSV에는 **어떤 렌더러가, 어떤 bounds/깊이로, 두 검사 중 어디서 탈락했는지 기록이 없다.** 따라서 2,695개가 모두 특정 버그 때문에 탈락했다는 결론은 오프라인 자료만으로 확정할 수 없다.

코드에서 확인한 문제는 다음과 같다.

1. 자동 시험 Update 명령 안에서 즉시 조사했다. 게임 LateUpdate/렌더 직전 갱신과 다른 시점의 카메라·장식 상태를 비교할 수 있다. 이것이 이번 표본의 직접 원인인지는 **추정**이다. 다음 본 카메라 onPreRender에서 조사하도록 바꿨다. 다른 카메라는 그 시점의 참고 스냅샷으로 남고 `own_callback=False`로 구별한다.
2. `CalculateFrustumPlanes(Camera)`에 의존하던 검사를 `CalculateFrustumPlanes(c.cullingMatrix)`로 명시했다. 사용자 지정 컬링 행렬과 projection×view의 판정 차이를 `culling_vs_projection_disagree`로 센다. 이 게임에서 해당 행렬이 실제 달랐다는 증거는 아직 없다. [Unity cullingMatrix](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/camera/cullingmatrix)
3. 기존 코드는 프러스텀을 통과해도 WorldToViewportPoint의 z/near/far로 계산한 면적이 0이면 outside로 버렸다. **면적 추정 실패를 컬링 실패와 합친 것이 확인된 진단 결함**이다. 이제 해당 후보는 유지하고 `area_zero_kept`로 센다. 면적은 계속 상한 추정이며, 커스텀 투영에서는 정확하지 않을 수 있다.
4. SpriteRenderer의 네이티브 world bounds와 sprite.bounds를 localToWorldMatrix로 변환한 bounds를 대조한다. 서로 다른 판정은 `sprite_bounds_disagree`, native 밖/local sprite 안은 `conservative_sprite_recovered`로 표시하고 보수적 후보로 포함한다. 이는 엔진 bounds를 바꾸는 수정이 아니다. sliced/tiled/custom bounds/셰이더 변형은 local sprite bounds와 다를 수 있어, 복구 수를 실제 draw 수라 부르면 안 된다.

출력에는 Camera 위치·near/far·view/projection/culling 행렬, 처음 96개 outside의 계층 경로·렌더러 형식·GameObject layer·world/local/sprite bounds·viewport center/z·이전 visibility를 남긴다. `isVisible`은 다른 카메라도 포함하므로 단독 증거로 쓰지 않는다. cullingMask와 renderer enabled/active/forceRenderingOff/ShadowsOnly 검사는 유지했다. **sortingLayer는 그리기 순서이며 GameObject layer cullingMask와 혼동하지 않았다.**

후속 판에서 area_zero_kept가 크면 3번, conservative_sprite_recovered가 크면 bounds 경로, culling_vs_projection_disagree가 크면 행렬 경로로 좁힐 수 있다. 셋 다 0인데 계속 장식 후보가 없으면 rejected 행의 실제 z/클립/위치와 갱신 시점을 확인해야 한다. 현재 상태는 **판정 결함 수정 + 원인 구분 도구 준비, 게임 재현을 통한 원인 확정은 미완료**다.

## census의 새 출력 해석

요청한 순서인 renderQueue → sortingLayer 값 → sortingOrder → 카메라 거리로 정렬한다. 거리에는 SpriteSortPoint(Pivot/Center), 카메라/GraphicsSettings 투명 정렬 모드·축을 반영한다. 같은 값은 재현 가능한 출력용 instance ID/slot 순서다. Unity 내부 동률 규칙을 재현한다는 뜻은 아니다.

- `runs_key_presence_est`: MPB 유무만 비교하는 요청 키의 연속 묶음 수. renderQueue/정렬층/순서/렌더러 형식/셰이더/키워드도 경계로 둔다.
- `runs_current_est`: 위 조건에 더해 MPB가 있는 slot은 각각 분리. 서로 다른 MPB 값이 같다고 가정하지 않는 보수적 수치.
- `runs_shared_material_est`: 재질 ID만 같게 만든 가정. 텍스처·셰이더·키워드·정렬 조건·MPB 경계는 남긴다.
- `runs_atlas_est`: 재질 ID와 주 텍스처 ID까지 같게 만든 가정. 같은 셰이더·키워드 등의 경계는 남긴다.

모두 실제 draw-call 수가 아니다. 단일 pass·정렬 보존·충분한 vertex 한도·기타 상태 호환을 가정한 배치 가능성 추정이다. 가정 모드에서는 재질의 숫자 값·보조 텍스처·UV 변환·stencil 차이를 무시하므로 **실제로 그만큼 병합할 수 있다는 증명이 아니다.** 기본 그룹 표에는 전체 텍스처 프로퍼티가 그대로 남는다.

`draw` 행은 renderer 경로/ID, SpriteRenderer·MeshRenderer 등 형식, scrDecoration 소유 여부, 재질 이름/ID, **해당 카메라 후보 안에서** 같은 재질을 쓰는 렌더러 수, runtime ID 음수 힌트, MPB, 셰이더/정렬한 키워드, queue/정렬층/순서/거리/주 텍스처를 기록한다. 재질 사용자 1개 또는 이름의 `(Instance)`/음수 ID만으로 에셋 기원까지 확정하지 않는다. 실제 참조 공유와 게임 IL의 getter 경로를 함께 본다.

주 텍스처는 SpriteRenderer.sprite.texture 및 MPB override를 우선 고려한다. 고유 텍스처 수와 정확한 width×height별 고유 수, filter/wrap/aniso/mip/Unity 형식·가능하면 GPU descriptor를 출력한다. 전체 slot의 텍스처 개수와 고유 텍스처 개수를 혼동하지 않는다.

**정렬 한계:** Unity의 2D 문서는 Sorting Layer/Order → Queue 등의 우선순위 및 SortingGroup·재질·내부 동률 규칙도 설명한다. 따라서 요청한 queue-first 나열은 분석용 모델이다. mixed queue/SortingGroup에서는 실제 렌더 순서와 다를 수 있다. [Unity 2D 정렬](https://docs.unity3d.com/2022.3/Documentation/Manual/2DSorting.html)

## gpuhide 명령과 복원·측정 범위

```text
gpuhide deco 2
gpuhide deco-even 2
gpuhide layer:정확한 정렬층 이름 2
gpuhide shader:정확한 셰이더 이름 2
gpuhide size:0-512 2
gpuhide size:513-1024 2
gpuhide size:1025-2048 2
gpuhide size:2049-16384 2
gpuhide tiles 2
gpuhide planets-particles 2
gpuhide off
```

숫자는 **각 측정 단계의 실시간 초**(1~30, 소수 가능)다. 그룹 문자열의 마지막 공백 뒤만 시간으로 파싱하므로 셰이더/층 이름 안의 공백을 허용한다. layer/shader/size는 scrDecoration 소유 렌더러로 한정한다. tiles는 scrFloor 하위이며 장식 소유가 아닌 대상, planets-particles는 scrPlanet 하위 또는 ParticleSystemRenderer이며 장식 소유가 아닌 대상이다. 장식 하위 파티클은 deco에 들어간다.

`deco-even`은 명령 시점 scrDecoration instance ID를 정렬한 인덱스 짝수다. 하나의 장식의 Sprite/Mesh 자식은 함께 선택된다. 이는 **맵 파일의 장식 인덱스가 아니며 판 사이 영구 식별자도 아니다.** 한 판 안의 고정된 대략 절반 표본이다. 처음에 찾은 장면 렌더러 집합을 고정하며 명령 뒤 새로 생성된 렌더러는 포함하지 않는다. 정확한 selected/changed_renderable/main_candidates 수를 함께 기록한다.

시작 전 `gpuprobe on`이 필요하다. 플레이 중 자동으로 패치를 설치하지 않는다. 그룹 탐색 뒤 0.5초 회복 → before T초 → 숨김 후 0.5초 회복 → hidden T초 → 복원 후 0.5초 회복 → after T초 순서다. 따라서 기본 2초 판은 **약 7.5초 + 일회성 탐색/로그 시간**이다. 자동 시험 명령은 즉시 다음 줄로 넘어가므로 별도의 wait가 필요하다.

각 단계는 기존 gpuprobe epoch reset을 사용한다. 미완료 이전 표본은 네이티브에서 폐기되고 query를 새로 만들지 않는다. snapshot은 DONOTFLUSH 기반 기존 통계 사본이며 busy 때 최대 8프레임에 걸쳐 재시도한다. 완료 표본 0 또는 읽기 실패면 절약량을 만들지 않는다. 각 단계에 gpuprobe 품질 카운터·모든 카메라/효과 평균, 시작/종료 realtime/frame을 남긴다. 마지막 약 3프레임 이상은 아직 회수 전일 수 있다.

숨김은 forceRenderingOff만 바꾼다. 원래 enabled/재질/색/정렬을 바꾸지 않는다. 명령 시작의 플래그를 저장하고 정상 종료·`gpuhide off`·`gpuprobe off`·Main.Unload·호스트 disable/destroy 때 복원한다. 파괴된 Unity 객체는 건너뛴다. 숨김의 카메라 정적 이벤트는 복원 때 제거하고, census 대기 이벤트도 Unload/취소/10초 timeout에 제거한다.

**중요한 한계:** InvisibleSkip은 ApplyColor 뒤 forceRenderingOff를 다시 쓸 수 있다. 숨김 중 onPreCull에서 프레임당 한 번 선택 집합을 확인해 다시 끈다. 이 루프의 평균 CPU ms와 외부 unhide 쓰기 수를 기록한다. 이것은 숨김 판에만 있는 CPU 작업이므로 GPU timestamp 유휴를 바꿀 수 있다. 비용이 크면 해당 판을 근거로 채택하지 않는다. 보관한 원래 값을 복원하므로 숨김 사이 알파 상태가 바뀌면 복원 값이 현재 게임 상태와 어긋날 수 있다. `external_unhide_writes>0` 판은 특히 after 대조/인과 추정에서 제외하고, 진단 후 새 게임으로 확인한다. 게임 내 복원과 예외 동작은 이번 작업에서 실행 검증하지 않았다.

스크립트/타일·장식 애니메이션은 계속 돈다. 순수 GPU busy가 아니라 GPU 경과 timestamp이며 CPU 제출 지연·다른 context 영향도 섞인다. 효과/행성까지 가려서 실제 플레이 화면과 달라지는 **진단 전용 시험**이다. 모든 새 코드는 DEV/AUTOTEST에만 컴파일된다. 명령을 켜지 않으면 새 GameObject/코루틴/정적 이벤트/프레임 루프는 0개다. census와 숨김은 동시 실행을 거절한다.

## 절약량 계산과 후보 설계

인계 수치를 2점 모델 C(s)=A+B·s²로 맞추면 B=(6.21−4.96)/0.75=1.667ms, A=4.543ms다. A는 **해상도와 무관한 부분의 추정**이다. 이것이 전부 draw-call 비용이거나 전부 제거 가능하다는 증거는 없다. 형상/정점 처리, GPU 유휴/제출, 고정 pass, RT 관련 작업 등도 섞일 수 있다. 4.543ms를 최적화 약속으로 쓰지 않는다.

숨김 측정에서 B0/H/A0를 해당 본 카메라 before/hidden/after 평균이라 하자.

- 추정 감소 Δ=(B0+A0)/2−H, 제어 변화 D=A0−B0. 부호를 보존한다.
- 짝수 절반에서 **숨기기 시작 시** 본 카메라 mask/frustum 후보 수를 N이라 할 때 장식 후보 하나당 추정 비용 p=Δ/N. 실제 draw 수나 픽셀 부하가 같다는 가정은 아니다. 후보 0이면 NaN.
- 예: N=1,000, Δ=2.0ms가 **실제로 측정된다면** p=0.002ms/후보다. 현재 측정 결과가 아니라 계산 예시다.
- material/atlas run을 R0/Rm/Ra라고 하면, 균일한 호출 비용이라는 추가 가정 아래 절약 추정은 p·(R0−Rm), p·(R0−Ra). hide는 픽셀·정점 비용까지 줄이므로 이 식은 예산 선별용이며 배치 실측을 대체하지 않는다.
- |D|가 Δ와 비슷하거나, 외부 플래그 재쓰기/품질 오류/큰 enforcement CPU 비용이 있으면 인과 수치로 채택하지 않는다. 같은 곡 시각의 무숨김 판과 숨김 판을 새 게임 ABBA로 반복해 시간 변화 편향을 확인한다.

| 후보 | 동일성 조건/깨지는 지점 | 예상 절약 근거 | 위험·구현 크기(추정) |
|---|---|---|---|
| 재질 공유로 Unity 배치 회복 | 같은 shader/pass/keywords·전체 재질 프로퍼티·보조 텍스처·UV/ST·stencil·queue·blend가 같고 원래 렌더 순서를 보존. 이후 setter가 공유 재질 전체를 바꾸면 안 됨 | R0−Rm과 hide 기반 p로 위 식. 실제 연속 같은 텍스처 수는 새 census 필요 | 중간~큼. setter 경로별 copy-on-write/소유·Unload 정리 필요. 기본 적용 0 |
| 런타임 atlas | 원래 geometry/pivot/vertex color/정렬·마스크를 유지하면서 정확한 샘플링을 재현해야 함 | R0−Ra가 낙관적 기회, 실제 page가 여러 개면 그보다 작음 | 큼. UV 반올림·DXT/색 공간·mip 경계·메모리 중복 위험. 적용 0 |
| DrawMeshInstanced 대체 | 같은 mesh·instancing shader 필요. 투명 순서와 기존 Renderer들 사이 끼워 넣는 순서를 보존해야 함 | 병합 가능한 연속 구간별 draw 감소 수에 위 식. 최대 1,023개/호출은 API 한도이지 이 맵의 안전 그룹 크기 아님 | 매우 큼. 개별 투명 정렬·SpriteMask·정렬층·필터 RT·컬링 재현 필요. 적용 0 |

### 게임 IL에서 확인한 배치 방해 경로

근거: `codex-gpu2-20261018-decoration-il.txt`, `keywords-il.txt`.

- 두 SetSprite 오버로드가 Renderer.material을 얻어 RDUtils.SetPointSamplingVariant를 호출한다. RDUtils 쪽은 EnableKeyword/DisableKeyword를 호출한다.
- SetTile도 Renderer.material을 얻고 repeatX/repeatY 계열 float를 바꾼다. 단순히 셰이더와 키워드가 같아도 반복 값이 다를 수 있다.
- SetBlendMode는 SpriteRenderer의 Renderer.material → set_shader 경로가 있고, meshRendererMat을 new Material로 만들어 별도 MeshRenderer에 넣는 경로도 있다.
- UpdateShader는 MeshRenderer.material, RT/Blit/마스크 경로를 사용하며 Material.SetColor/SetFloat/SetVector/mainTexture를 쓴다.
- ApplyColor는 scrDecoration.rendererColor를 계산해 SpriteRenderer.color에 넣는다. 이 메서드에서 Material.SetColor/MPB 호출은 없다. SpriteRenderer.color는 Unity 문서상 vertex color 경로다. [SpriteRenderer.color](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SpriteRenderer-color.html)
- Renderer.material getter는 공유 재질이면 해당 렌더러용 재질을 인스턴스화할 수 있다. 따라서 위 경로는 개별 재질을 만드는 정적 근거다. 실제 어느 장식이 그 경로를 탔고 지금 몇 개를 공유하는지는 새 census가 필요하다. [Renderer.material](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Renderer-material.html)

Unity Built-in의 동적 배치는 같은 재질/텍스처와 호환 상태, 정점/속성 한도, 정렬 인접성 등의 조건을 요구한다. 일반 MPB의 다른 값은 병합을 깨뜨릴 수 있지만 SpriteRenderer 내부 per-renderer 데이터까지 전부 “MPB 존재=배치 불가”로 확정하지 않는다. 현재 census는 MPB 유무 모델과 개별 분리 모델을 둘 다 남긴다. Unity 6 문서는 동적 배치의 CPU 비용이 draw 제출 비용보다 클 수 있다고 경고하며, 스프라이트 전용 내부 묶음과 MeshRenderer 경로를 같은 것으로 단정해서도 안 된다. **공유 재질을 쓰면 이 게임의 Sprite 배치가 살아나는지는 실제 Frame Debugger/픽셀 대조로 확인할 가설**이다. [Unity 6 배치](https://docs.unity3d.com/6000.0/Documentation/Manual/DrawCallBatching.html), [동적 배치 조건(2022.3)](https://docs.unity3d.com/2022.3/Documentation/Manual/dynamic-batching.html)

### atlas에서 그림이 같으려면

1. 투명 여백을 자르지 않고 원본 texel·sprite mesh/pivot·정점 색을 유지한다. 회전 패킹도 우선 제외한다. `optimization-36.md` 12번의 Bilinear 8조건 각 9~31픽셀 차이/총 171픽셀 차이를 수식 동일성으로 무시하지 않는다.
2. Clamp 경계를 재현하려면 **RGBA 전체 가장자리 texel**을 패딩으로 복제한다. 투명 RGB도 버리면 안 된다. Bilinear 한 레벨의 footprint뿐 아니라 mip/aniso가 읽는 범위까지 고려해야 한다. 단순 1px padding이면 충분하다는 주장은 하지 않는다.
3. 원본별 wrap/filter/aniso/mip/색 공간·압축 형식이 다르면 별도 page/셰이더 처리가 필요하다. Repeat/미러 및 셰이더의 수동 UV 변환/point-sampling keyword/보조 텍스처는 초기 제외 후보다. 하나의 atlas sampler로 서로 다른 sampling 상태를 그냥 공유할 수 없다.
4. NPOT도 가능한 하드웨어라고 샘플이 같아지는 것은 아니다. 원본 texel center→atlas texel center의 변환과 UV 보간/부동소수점 반올림, 회전·축소·음수 scale에서의 래스터 결과를 실제 GPU로 대조해야 한다.
5. DXT 블록 정렬을 지켜도 재압축하면 값이 바뀔 수 있다. CPU PNG decode 결과와 GPU 압축 텍스처를 다르게 취급한다. CopyTexture 호환성과 compressed block/padding 제약, sRGB 해석을 확인해야 한다.
6. 메모리는 page별 width×height×bytes-per-pixel×mip 계수 + staging + 기존 텍스처 동시 보유량이다. 예: RGBA32 8192² 한 page 기본 레벨은 256MiB, full mip이면 약 341MiB. DXT 페이지면 다르다. 이미 VRAM이 빡빡한 Arche에서 여러 page/원본 중복은 끊김을 악화시킬 수 있다. 총 atlas 크기는 새 census의 고유 texture 분포와 실제 packing 효율을 본 뒤 산정한다.
7. 로딩 중 pack/복사/업로드/새 Sprite 생성/참조 대체 비용과 원본 해제 시점을 별도 측정한다. 이전 Sprite에 다른 코드가 접근할 수 있으므로 원본을 바로 버리지 않는다. 회전/배율/피벗/마스크/반복/Point·Bilinear/색 공간/화면 가장자리 조합에서 원본 vs 대체 픽셀 불일치 0이 채택 조건이다.

### instancing의 순서 문제

DrawMeshInstanced는 묶음을 하나로 컬링·정렬하며 인스턴스별 투명 정렬을 하지 않는다. 같은 셰이더라고 떨어진 장식을 한 그룹으로 모으면 사이에 있던 다른 장식·타일과의 투명 합성 순서가 바뀐다. 기존 SpriteRenderer 기능과 pass/마스크/필터 입력도 자동으로 재현되지 않는다. 안전성을 증명할 수 있는 연속 구간만 별도 조사하고, 그 구간조차 원본과 실제 GPU 대조가 필요하다. 이번에는 구현하지 않았다. [Unity DrawMeshInstanced](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Graphics.DrawMeshInstanced.html)

## Claude 실행 순서

설치가 허용된 후속 담당자가 PlayerAuto와 독립 gpuprobe.dll을 준비한다. 이번 작업에서는 둘 다 Mods에 복사하지 않았다. D3D11/3440×1440, LowRenderScale=100과 vSync/프레임 제한/보간/FuseLate를 고정한다. probe 설치는 **맵 열고 press 전**에 한다. 아래 각 블록은 별도의 새 게임 판이다. wait는 전부 180초 이하이며 실시간이므로 실제 곡 시각을 로그로 맞춘다.

census 판(약 258초, 시간 측정 판과 분리):

```text
auto on
game D:/얼불춤 맵 파일/Arche/[𝐃𝐎 𝐍𝐎𝐓 𝐎𝐏𝐄𝐍 𝐓𝐇𝐈𝐒 𝐀𝐓 𝐇𝐎𝐌𝐄] FULL.adofai
press
wait 150
wait 108
gpucensus
wait 5
```

`queued` 뒤 실제 TSV 생성 로그를 확인한다. `timeout`이면 본 카메라 이름/수동 Render 여부부터 확인한다. 후보가 계속 6개이면 곧바로 성능 판으로 넘어가지 말고 위 원인 카운터와 rejected 행을 먼저 본다. TSV는 `%LOCALAPPDATA%/StutterFix/gpu-census-*.tsv`다. census의 긴 프레임 및 회복 구간은 타이밍 자료에서 제외한다.

숨김 판(약 257초에 시작해 258~265초 주변의 세 창 측정):

```text
auto on
game D:/얼불춤 맵 파일/Arche/[𝐃𝐎 𝐍𝐎𝐓 𝐎𝐏𝐄𝐍 𝐓𝐇𝐈𝐒 𝐀𝐓 𝐇𝐎𝐌𝐄] FULL.adofai
gpuprobe on
wait 5
press
wait 150
wait 107
gpuhide deco-even 2
wait 12
gpuhide off
gpuprobe off
```

최초 `deco-even` 판으로 명령/복원·후보 수·품질/CPU enforcement 비용을 확인한다. 다음 새 판에서는 같은 마지막 gpuhide 한 줄만 `deco`, `layer:실제이름`, `shader:실제이름`, `size:...`, `tiles`, `planets-particles`로 바꾼다. 없는 층 이름은 오류, 없는 셰이더/크기 그룹은 selected=0일 수 있다. 실제 census 표에 나온 이름을 쓴다. 진단 중 별도 gpuprobe reset/on/off나 census/캡처를 끼워 넣지 않는다. 중단 필요 시 gpuhide off는 즉시 복원용이다.

무숨김 제어 판은 동일한 probe on/press/wait 순서에서 gpuhide부터 다음으로 교체한다.

```text
gpuprobe reset
wait 8
gpuprobe state
gpuprobe off
```

이 8초 제어는 전체 구간/표본 품질 확인용이다. 세 짧은 창의 엄밀한 시간 일치를 대신하지 않는다. 본 카메라 before/hidden/after 시간, 실제 곡 위치, main_candidates/selected, control_drift, external_unhide_writes, enforcement CPU 및 각 단계 ready/completed/invalid/ring_full/overflow/errors/unknown/effect_outside_camera를 보관한다. 타임스탬프 감소만으로 순수 GPU busy·FPS 개선을 선언하지 않는다.

## 오프라인 검증과 남은 범위

PlayerAuto/Player/DEV 빌드와 FieldRefAccess 검사·진단 타입 제외 검사 결과는 아래 검증 파일에 남겼다. 실제 게임 실행, 숨김/복원, census 후보 회복 수, 픽셀 동일성, 장식별 GPU 비용, 배치 최적화 절약은 미측정이다. 기존 6.21/4.96ms는 인계에서 받은 값이며 이번 새 측정으로 표시하지 않았다.

게임 실행·Mods 설치·커밋/푸시 없이 종료한다. 자동 build-install 훅은 호출하지 않았고, 직접 로컬 dotnet build만 사용했다.
### 최종 검증 수치

| 검사 | 결과 | 근거 파일 (`docs/handoff/` 아래) |
|---|---|---|
| PlayerAuto 로컬 빌드 | 오류 0 / 기존 경고 35 | `codex-gpu2-20261018-playerauto-build.txt` |
| 일반 Player 로컬 빌드 | 오류 0 / 기존 경고 19 | `codex-gpu2-20261018-player-build.txt` |
| DEV 로컬 빌드 | 오류 0 / 기존 경고 35 | `codex-gpu2-20261018-dev-build.txt` |
| Player FieldRefAccess | 215곳, 틀림 0 / 동적 이름 5곳은 실행 중 확인 필요 | `codex-gpu2-20261018-loadcheck.txt` |
| PlayerAuto FieldRefAccess | 236곳, 틀림 0 / 동적 이름 5곳은 실행 중 확인 필요 | `codex-gpu2-20261018-playerauto-loadcheck.txt` |
| GpuCensus/GpuHide/GpuProbe 최상위 타입 | Player 각각 0 / PlayerAuto 각각 1 | `codex-gpu2-20261018-{player,playerauto}-types.txt` |
| 변경한 추적 파일 git diff --check | 오류 0 | 전체 검사 로그에는 기존 `.codex/config.toml`의 LF/CRLF 경고만 존재 |
| 설치 DLL·Settings.xml 및 native query.cpp SHA256 | 작업 전후 세 파일 모두 동일 | `codex-gpu2-20261018-preservation-{before,after}.txt` |

새 GPU 소스의 컴파일 경고는 없다. 기존 경고가 남으므로 전체 빌드 경고 0이라고 하지는 않는다. 새 Harmony 설치·네이티브 변경이 없어 이전 WARP 시험은 반복하지 않았다. 오프라인 컴파일/IL/참조 검사는 Unity 런타임 실행 검증이 아니다.

정적 복원/오버헤드 검토에서는 자동 진단 객체 생성 없음, DEV/AUTOTEST 조건부 연결, hide 정상 종료·취소·probe off·Unload·호스트 disable/destroy 경로, census callback·timeout 정리를 확인했다. 예외적 코루틴 중단 및 다른 모드와의 실시간 상호작용까지 시험 통과한 것으로 표현하지 않는다. GPU 비용 결과를 채택하기 전에 반드시 위 첫 짧은 게임 판에서 검증해야 한다.