# NO_DIRECT_TEXTURES 구현 결과 (2026-10-26)

`docs/handoff/codex-nobig-20261026.md`를 끝까지 읽고 구현했다.
작업 트리는 `E:\StutterFixWork\wt-nobig`, 브랜치는 `codex/nobig`이다.
비교 기준 HEAD: `7df70fab0b1cbb502505ebc6d37cf6efb0e017a3`.

코드·셰이더 소스 변경과 오프라인 검증을 완료했다. 커밋, 푸시, 게임, Unity, sfmeasure 실행은 하지 않았다. 번들은 재생성하거나 교체하지 않았다.

## 바꾼 파일

| 파일 | 변경 내용 |
| --- | --- |
| `GpuDecoDrawCollapseRenderer.cs` | 최종 배열 주소를 쓰는 아이템의 판정 보관, 번들 키워드 지원 검사, 구간별 변형 선택, 공유 머티리얼 키 분리, `_Big*` 설정 생략, ordered 복제 키워드 검사, 새 구간 수 로그 |
| `tools/gpudeco-lab/generate-drawcollapse-shader.py` | `#pragma multi_compile_local _ NO_DIRECT_TEXTURES` 추가, 직접 텍스처 선언과 분기 트리의 조건부 컴파일, 배열 전용 분기 트리 생성 |
| `tools/gpudeco-lab/GpuDecoBatch.shader` | 수정한 생성기를 실행한 결과. 이 파일을 직접 편집하지 않았다. |
| `tools/optimization/DrawCollapseShaderCheck/Program.cs` | 두 변형의 정점·픽셀 셰이더 컴파일, 디스어셈블 결과의 실제 텍스처 리소스 수 검사, 선택적 변경 전 셰이더와 바이트코드 비교 |
| `docs/handoff/codex-nobig-20261026-result.md` | 이 결과 문서 |

검증 로그와 보조 감사 스크립트는 `out/nobig-20261026/`에 있다.

## 판정 규칙과 안전장치

1. `addresses`에서 최종 할당된 `address.x`를 사용한다. 배열 주소는 `0 <= address.x < gpuCollapseArrays.Count`이면서 `address.x < DrawCollapsePolicy.ArrayBindings`인 경우다. 직접 주소는 기존과 같이 `ArrayBindings + gpuCollapseDirect`의 인덱스다.
2. `CollapseSize()`나 이미지 크기로 직접 텍스처 여부를 추측하지 않는다. `exactGroup`으로 들어간 큰 텍스처도 최종 주소가 `_Tex*`이므로 배열로 판정한다. `info[i*2].x`에 붙이는 포인트 샘플링 플래그 `128` 이전의 주소를 사용한다.
3. 주소를 못 찾은 아이템은 배열 전용 판정의 기본값 `false`를 유지한다. 기존 텍스처 초과 폴백도 유지한다.
4. 이미지 교체 대상 `GpuImageActor(gpuLiveOwnedIndices[source.MasterItems[i]])`는 보수적으로 `false`로 처리한다. `UpdateDrawCollapseTextureSlot`이 재생 중 주소를 바꿀 수 있으므로 현재 주소만으로 미래의 `_Big*` 사용을 배제하지 않는다. 따라서 이미지 교체 아이템이 하나라도 있는 구간은 이번 변형을 쓰지 않는다. 미래 이미지도 전부 배열인 구간을 추가로 최적화하는 일은 하지 않았다.
5. `gpuCollapseShader.keywordSpace.FindKeyword("NO_DIRECT_TEXTURES").isValid`가 참이고, 구간의 모든 아이템이 배열 전용으로 판정될 때만 키워드를 켠다. 이 판정 목록은 재계획에서도 재사용하고 전체 정리 때 해제한다.
6. 키워드를 켠 머티리얼에는 `_Tex*`만 설정하고 `_Big*` 설정을 생략한다. 공유 `matKey` 끝에 변형 여부를 넣어 기본 변형과 배열 전용 변형이 같은 머티리얼을 공유하지 않게 했다.
7. `new Material(sp.Material)`은 기존처럼 사용한다. 복제 직후 `IsKeywordEnabled`가 원본과 같은지 검사해 ordered 머티리얼에서도 변형이 유지되는지 확인한다.
8. 이전 번들에는 키워드가 없으므로 5번이 거짓이 된다. 이때 키워드를 활성화하지 않고 기존 `_Big*` 바인딩과 기본 변형을 사용한다. 실제 이전 번들을 로드한 게임 시험은 하지 않았으며, 이번 확인 범위는 분기 검토와 실제 게임 관리 어셈블리에 대한 빌드다.

새 로그는 준비/재계획의 구간 생성 완료 때 한 줄 추가한다.

```text
DRAWCOLLAPSE_NODIRECT spans=N of M
```

`N`은 키워드를 실제 선택한 구간 수, `M`은 전체 구간 수다. 기존 번들이면 `N=0`이다. 기존 `DRAWCOLLAPSE_*` 로그 접두 문자열 10개와 순서는 그대로 유지했다.

셰이더 기본 변형은 기존 52슬롯 이진 트리를 그대로 사용한다. 새 변형은 `_Big0..47` HLSL 선언과 기존 트리를 컴파일에서 제외하고 `_Tex0..3` 트리만 사용한다. ShaderLab Properties는 두 변형이 공유하되, 컴파일된 새 픽셀 셰이더의 직접 텍스처 리소스는 0개임을 확인했다.

## 검증 결과

| 검사 | 결과 | 로그 |
| --- | --- | --- |
| `dotnet build -c Release -p:Edition=Player` | 성공, 오류 0개 / 경고 38개 | `out/nobig-20261026/player-build.txt` |
| `dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1` | 성공, 오류 0개 / 경고 42개 | `out/nobig-20261026/playerauto-build.txt` |
| `py -3 tools/gpudeco-lab/check-shader.py` | compute 엔트리 7개 SM5 컴파일 통과 | `out/nobig-20261026/compute-check.txt` |
| `dotnet run --project tools/optimization/DrawCollapseShaderCheck -- . out/nobig-20261026/GpuDecoBatch-before.shader` | compute 2개 + 두 변형의 vertex/fragment 4개, 총 6개 통과 | `out/nobig-20261026/shader-check.txt` |
| `py -3 out/nobig-20261026/audit.py` | 금지 파일/번들 해시, 기존 로그, 생성기 재현성, `git diff --check` 통과 | `out/nobig-20261026/audit.txt` |

컴파일된 픽셀 셰이더의 텍스처 리소스 수:

| 변형 | 배열 | 직접 | 합계 | 바이트코드 크기 |
| --- | ---: | ---: | ---: | ---: |
| 기본 | 4 | 48 | 52 | 15,288 bytes |
| `NO_DIRECT_TEXTURES` | 4 | 0 | 4 | 2,484 bytes |

기본 변형의 vertex/fragment 바이트코드는 HEAD의 변경 전 셰이더와 각각 바이트 단위로 동일했다. 두 변형의 vertex 크기는 모두 1,288 bytes다. 이 수치는 Unity include 중 `UnityObjectToClipPos`만 대체한 오프라인 D3DCompile 검사이며, 최적화 생략 옵션을 사용했다. Unity 전처리·임포트·번들 변형 포함 여부·실제 장치 실행·픽셀 동일성·성능 개선은 아직 검증하지 않았다. 오프라인 HLSL 경고도 로그에 보존했다.

빌드 산출물은 `bin/Player/StutterFix.dll`, `bin/PlayerAuto/StutterFix.dll`이다. 두 DLL에는 아직 기존 번들이 포함되어 있어 이번 최적화는 활성화되지 않는다.

`GpuDecoPlayer.cs`, `GpuDecoExport.cs`, `DrawCollapsePolicy.cs`, 기존 `drawcollapse5/bundle/stutterfix_gpudeco`는 작업 시작 때와 SHA-256이 같다. 정책 기본값을 변경하지 않았다.

## Claude가 번들을 만든 뒤 확인할 것

1. 생성된 셰이더를 실제 번들 제작 경로에 반영하고 두 로컬 변형이 포함되는지 확인한다. 런타임 생성 머티리얼을 위해 `multi_compile_local`을 유지한다. 새 번들을 임베드한 Player/AUTOTEST DLL도 다시 빌드해야 한다.
2. 이전 번들 + 새 코드 조합에서는 키워드가 무효이고 `DRAWCOLLAPSE_NODIRECT spans=0 of M`이며, 준비·ready 재계획·재생·원본 복귀가 기존처럼 동작하는지 먼저 확인한다.
3. 새 번들에서는 `keywordSpace.FindKeyword(...).isValid`가 참인지 확인한다. 배열만 쓰는 구간에 키워드가 켜지고 `_Big*` 설정이 없으며, 직접/혼합/이미지 교체 구간은 기본 변형을 유지하는지 확인한다. `exactGroup` 큰 텍스처의 정적 구간도 포함한다.
4. 같은 큐·블렌드의 배열 전용 구간과 직접 텍스처 구간이 서로 다른 공유 머티리얼을 쓰는지, 같은 정렬 묶음의 ordered 복제에서도 원래 변형과 그리기 순서를 유지하는지 확인한다.
5. 준비 직후와 ready 재계획 후의 `N of M`을 기록한다. 포인트/선형 샘플링, 작은 배열, exact 배열, 직접 텍스처, 이미지 교체, 숨김→표시, collapse off/재진입의 픽셀과 복구 상태를 확인한다.
6. Arche 30초 동일 구간에서 렌더 스레드 시간과 `SetTextures`/`SetShaderResources` 비용을 전후 비교한다. 실제 새 변형 패스의 텍스처 바인딩 수를 확인하고, FPS와 픽셀 결과도 함께 기록한다. 52→4는 이번 오프라인 리소스 검사 결과이며, 성능 개선량으로 환산하지 않는다.
