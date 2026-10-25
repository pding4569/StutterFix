# 인계: 그리기 통합의 텍스처 바인딩 줄이기 (직접 텍스처가 필요 없는 구간용 셰이더 변형)

작업 트리: `E:\StutterFixWork\wt-nobig` (브랜치 `codex/nobig`). **코드만 짠다. 게임·Unity·sfmeasure 실행, 커밋, 푸시는 하지 않는다**(Claude 가 한다). `dotnet build` 와 정적 점검(`tools/gpudeco-lab/check-shader.py`, `tools/optimization/DrawCollapseShaderCheck`)은 해도 된다. 한국어로 보고한다.

## 왜
Arche 곡 30초 구간(가벼운 구간) PerfView 비교: 켠 쪽 렌더 스레드가 프레임당 0.98ms, 끈 쪽 0.61ms(+0.37ms). 늘어난 몫의 대부분이 그리기 82번(구간 82개)의 상태 설정이다: `GfxDeviceD3D11Base::DrawBuffers` +0.18, `IASetVertexBuffers/IASetIndexBuffer` +0.11, `D3D11CommonShader::ApplyGpuProgram` +0.06, `SetTextures`/`SetShaderResources` +0.09. 구간마다 `GpuDecoBatch.shader` 를 쓰는데, 셰이더가 `_Tex0..3`(텍스처 배열 4칸) + `_Big0..47`(직접 텍스처 48칸) = 52칸을 선언하고 있어 그리기마다 52개 SRV 를 다시 붙인다. 대부분의 구간은 배열 텍스처만 쓰고 직접 텍스처(`_Big*`)는 쓰지 않는다.

## 할 일
1. `tools/gpudeco-lab/generate-drawcollapse-shader.py` 가 `GpuDecoBatch.shader` 를 만든다(생성 결과 파일을 직접 고치지 말고 생성기를 고친다). 키워드 `NO_DIRECT_TEXTURES`(`#pragma multi_compile_local _ NO_DIRECT_TEXTURES`; 런타임에 코드로 만든 머티리얼도 그 변형이 번들에 들어가야 하므로 shader_feature 가 아니라 multi_compile)를 추가한다. 키워드가 켜지면 `_Big0.._Big47` 선언과 그 분기(이진 트리)를 컴파일에서 뺀다(그 분기에 들어올 일이 없는 구간용). 키워드 없을 때의 동작은 **지금과 완전히 같아야 한다**.
2. `GpuDecoDrawCollapseRenderer.cs` 의 구간 만들기(`BuildDrawCollapseSpans`, 공유 머티리얼 `matKey` 부분, "ordered" 머티리얼 복제 부분)에서: 구간의 아이템이 하나도 직접 텍스처(`_Big` 슬롯, 배열 아닌 것)를 쓰지 않으면 그 구간의 머티리얼에 `EnableKeyword("NO_DIRECT_TEXTURES")` 를 켜고 `_Big*` 텍스처 설정도 생략한다. 공유 머티리얼 키(`matKey`)에 이 변형 여부를 넣는다. "ordered" 복제(`new Material(sp.Material)`)는 키워드를 그대로 가져가는지 확인한다. 직접 텍스처를 쓰는 구간은 지금과 같다.
3. 직접 텍스처 사용 여부는 슬롯 테이블을 만드는 코드(`gpuCollapseDirect`, `exactGroup`, 텍스처 주소 `addresses`/`info`)를 읽어 정확히 판정한다. 틀리면 화면이 깨지므로 판정이 모호하면 변형을 쓰지 않는다(보수적으로).
4. 번들 변경이 필요한 일이다(셰이더). 번들을 다시 만들 수 없으니 **코드와 셰이더 소스만** 바꾸고, 새 번들 없이도(키워드가 번들에 없을 때) 기존 번들에서 문제없이 도는지 확인하는 안전장치를 넣는다: 번들의 셰이더가 키워드를 지원하는지(`Shader.keywordSpace`/`LocalKeyword` 존재 여부)로 확인해서 없으면 변형을 쓰지 않고 지금 방식으로 동작한다.
5. 플레이어/AUTOTEST 두 빌드 오류 0 (`dotnet build -c Release -p:Edition=Player` 와 `... -p:AutoTestBuild=1`). 시험 명령 경로 로그 문자열(`DRAWCOLLAPSE_*`)은 바꾸지 말고, 변형을 쓴 구간 수를 새 로그 한 줄(`DRAWCOLLAPSE_NODIRECT spans=N of M`)로 추가한다.
6. 끝나면 `docs/handoff/codex-nobig-20261026-result.md` 에 바꾼 파일, 판정 규칙, Claude 가 번들을 만든 뒤 확인할 것을 적는다.

## 건드리지 말 것
`GpuDecoPlayer.cs`, `GpuDecoExport.cs`(Claude 가 같은 시각에 고친다). `DrawCollapsePolicy` 의 기존 기본값.
