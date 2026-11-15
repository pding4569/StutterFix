# M4j 구현·오프라인 검증 결과

2026-11-16. 작업 폴더 `E:\StutterFixWork\wt-m4`, 브랜치 `claude/drfull`, 기준 HEAD `ad27acc`.
명세 `15-m4j-spec.md`, 선행 `14-m4i-spec.md`, `14-m4i-result.md`, `13-hello-color-notes.md` 및 M4h→M4i 실제 코드 차이를 확인했다.

상수 링의 전체 재업로드 회귀를 수정했고, Unity/D3D11 상태 변환을 재검사해 추가 오류를 고쳤다. DD용 draw별 GPU 시간·텍스처/SRV/샘플러 덤프와 분석기, ImageMaxSide 1024 대조 계획을 구현했다. **실게임 DD 41ms의 실제 draw 이름과 남은 실제 장식 픽셀의 원인 확정은 미측정이다.** 게임 실행 금지 범위에서 합성 시험을 실제 DD/Exit 결과로 바꾸어 보고하지 않는다.

커밋, 게임 실행·조작, 설치 DLL 교체·복원은 하지 않았다. 저장소 안의 `native/sfnative.dll`과 로컬 빌드 산출물만 갱신했다. 시작 시 존재한 과거 `out/direct-renderer/m4*` 산출물은 삭제하지 않았다.

## 1. 상수 링: 6MB 전체 재업로드 제거

직접 원인은 `prepareConstants`가 segment마다 `ringData`를 비우고 모든 CB를 다시 직렬화한 뒤, 전체 바이트열이 하나라도 다르면 `WRITE_DISCARD`로 전부 올리는 구조였다. 시간·카메라 등 소수의 상수 변화도 불변 material/object CB 전체의 재업로드를 일으켰다. draw 순서가 바뀌면 같은 CB들의 직렬화 순서도 달라져 전체 비교가 실패할 수 있었다. 단순히 마지막 링과 전체 비교하는 방식으로는 희소 변경과 순서 변경을 분리할 수 없었다.

수정 경로:

- 각 `ConstantStorage`가 GPU 오프셋과 arena 세대를 유지한다. 기존 dependency 버전 판정과 바이트 비교로 내용이 바뀐 CB만 추가한다. 순서·segment 변경은 동일 세대의 기존 오프셋을 재사용한다.
- 256바이트 정렬 append arena를 사용한다. 첫 기록은 `WRITE_DISCARD`, 이후 미사용 범위는 `WRITE_NO_OVERWRITE`로 쓴다. 이전 GPU 명령이 참조한 범위를 덮지 않는다. 변경분을 모아 segment당 최대 한 번 Map/Unmap한다.
- 기본 arena는 8MiB이며, 한 segment의 최대 상수 집합이 더 크면 그 크기까지 확장한다. 꼬리 공간이 부족할 때만 새 세대로 전환하고, 사용되는 CB를 지연 재배치한다. **순환 시 드문 재업로드는 남으며 매 프레임 전체 복사는 제거했다.** 전체 CB가 실제로 매 프레임 바뀌면 그 전체 바이트는 여전히 필요하다.
- `ConstantBufferOffsetting`과 `MapNoOverwriteOnDynamicConstantBuffer`가 모두 있어야 사용한다. deferred/기능 미지원/CPU batch 경로는 legacy CB로 돌아간다. 오류 시 세대를 무효화해 실패한 업로드의 오프셋이 다음 프레임에 재사용되지 않게 했다.
- 원본 VS/PS DXBC와 draw 순서, geometry/pose 주소, 기존 CPU 인덱스 업로드 제거는 유지했다.

이 구현은 [D3D11 기능 확인](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/ns-d3d11-d3d11_feature_data_d3d11_options)과 [NO_OVERWRITE의 미사용 범위 조건](https://learn.microsoft.com/en-us/windows/win32/direct3d11/how-to--use-dynamic-resources)을 따른다.

새 production WARP fixture는 384개 서로 다른 16KiB CB, 합계 **6,291,456바이트**를 사용한다.

| 검사 | 결과 |
|---|---|
| 정지 16프레임 | 추가 상수 업로드 **0바이트** |
| 한 CB만 바꾸는 192프레임 | dirty CB 16,384바이트; 합계 **9,420,800바이트**, arena 순환에 따른 live 집합 재업로드 최대 1회 포함 |
| 수정 전 방식의 같은 희소 변경 | 192 × 6,291,456 = 1,207,959,552바이트를 올리는 구조 |
| 순환 뒤 불변 색 | 픽셀 완전 동일 |
| 실제 material 색 수정·복원 | 변경이 픽셀에 나타나고 복원 후 완전 동일 |
| 두 segment 정지 반복 | 추가 상수 업로드 0바이트, 픽셀 동일 |

`cb_ring_bytes`, `cb_ring_maps`, `upload_constant_bytes`, `rt_constants_us`로 실게임 적용 결과를 확인할 수 있다. 위 바이트 수는 합성 fixture이며 Exit의 기존 6.30GB 누적치를 실게임에서 다시 측정한 값이 아니다. Exit B≥A, render <600µs, main <300µs는 이번 실행에서 확인하지 않았다.

## 2. collect 회귀 조사

`8fc50a5`(M4h)와 `5fc0080`(M4i)의 `DirectFull.Render.cs` 차이를 확인했다. `FullPreCull`의 collect 구간 자체에는 상수 링 재직렬화 루프가 들어오지 않았다. 추가된 `UpdateParticleMeshes`는 collect 이후 sort 구간이고 기본 explicit 모드에서는 bake를 하지 않는다. 따라서 **collect 148→451µs를 링 함수가 메인 스레드에서 실행된 결과라고 귀속하면 틀리다.**

반복 `Refresh/Fill`, 새 draw rebind, source promotion, render-thread 복사와의 자원 경쟁 등은 같은 실게임 자료 없이는 구분되지 않는다. `collect_signals`, `collect_tint_signals`, `collect_full_signals`, `fill_calls`와 64프레임당 표본의 `collect_refresh_sample_us_total`, `collect_promote_sample_us_total`, `fill_sample_us_total`을 추가했다. 기존 `why`의 writer/replan 원인, packet 바이트, GC, draw rebind 수와 함께 읽는다. 이 표본 누적 시간은 기존 전 프레임 평균 `collect_us`와 단위·분모가 다르며 그대로 빼거나 합산하지 않는다. 근거 없는 변경 감지 생략으로 동작을 바꾸지는 않았다.

## 3. Unity ↔ D3D11 상태 전수 검사

설치된 게임의 `UnityEngine.CoreModule.dll`을 실행하지 않고 PE 메타데이터로 읽어 10종 enum의 이름·정숫값을 대조했다. 결과는 `out/direct-renderer/m4j/unity-enums.json`이다. Unity 현재 공개 [enum 소스](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Graphics/GraphicsEnums.cs)도 교차 확인했다.

| 대상 | 확인·수정 |
|---|---|
| ColorWriteMask | Claude가 고친 A=1/B=2/G=4/R=8 → D3D R=1/G=2/B=4/A=8 유지. 모든 16개 마스크의 실제 픽셀 채널 보존 검사, RGB/A/0 포함 |
| BlendMode 0..10 | **OneMinusSrcColor(6)가 INV_SRC_ALPHA로 매핑되던 추가 오류 수정**. RGB는 INV_SRC_COLOR |
| alpha blend factor | D3D alpha에 금지되는 COLOR 인자를 대응 ALPHA 인자로 변환. SrcAlphaSaturate의 alpha 성분은 One. [D3D alpha 인자 제한](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/ns-d3d11-d3d11_render_target_blend_desc) 확인 |
| BlendOp 0..4 | Add/Sub/ReverseSub/Min/Max 명시 매핑. **One/Zero 인자만 보고 blending을 끄던 Min/Max 오류 수정**. 논리 연산 등 D3D11 미지원 값은 거부 |
| CompareFunction 0..8 | Disabled/Always 처리 및 depth의 Less↔Greater, LEqual↔GEqual reversed-Z 변환 확인. stencil 비교에는 reversed-Z를 적용하지 않음 |
| StencilOp 0..7 | Keep/Zero/Replace/증감 saturate/Invert/증감 wrap 명시 매핑. common/front/back 선택 유지 |
| CullMode | Off/Front/Back → NONE/FRONT/BACK, clockwise front 유지 |
| ZWrite/ZClip/AlphaToCoverage | 0/1 범위 검사. stencil mask/ref도 0..255 검사 |
| TextureWrapMode | Repeat/Clamp/Mirror/MirrorOnce → WRAP/CLAMP/MIRROR/MIRROR_ONCE, 알 수 없는 값은 임의 MirrorOnce로 보내지 않고 거부 |
| FilterMode/aniso | Point/Bilinear/Trilinear 확인. 전역 Disable/Enable/ForceEnable을 반영하도록 수정. raw aniso=0 예외 및 ForceEnable 9..16 규칙 반영. D3D aniso 사용 시 trilinear sampler 적용 |
| mip bias | 기존 Texture payload가 보존하지 못하던 nonzero `mipMapBias`는 원본 Unity 경로로 fallback. 0으로 조용히 바꾸지 않음 |
| VertexAttributeFormat | Float32 1..4, Float16 2/4, UNorm/SNorm8 2/4, UNorm/SNorm16 2/4, UInt/SInt32 1..4의 DXGI 매핑 확인. UInt/SInt8·16 등 미지원 조합은 기존 fallback. 차원 0/5 이상을 Float4로 보내던 범위 누락 차단 |
| IndexFormat | Unity UInt16/UInt32 → DXGI R16_UINT(57)/R32_UINT(42), native 측 허용값 검사 확인 |

aniso 전역 규칙은 [Unity Texture.anisoLevel](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/texture/anisolevel)을 확인했다. `drfull-state.h`의 명시 변환 함수에 native 경로와 fixture를 연결했다. WARP는 enum 테이블뿐 아니라 비대칭 mask, OneMinusSrcColor, Min/Max의 실제 픽셀을 독립 식과 대조한다.

WARP는 비-aniso sampler의 사용하지 않는 MaxAnisotropy를 GetDesc에서 0으로 정규화했다(생성 요청은 1). 따라서 사용되지 않는 descriptor 값의 원시 숫자 차이를 품질·성능 차이라고 단정하지 않는다.

현재 21개 원본 셰이더 catalog의 RT0 기본 상태를 검사했을 때 factor 6 및 동적 blend 상태 후보는 **0개**였다. 그러므로 발견한 blend6/MinMax 결함을 HELLO 잔여 0.65%의 주원인으로 보고하지 않는다. aniso 차이는 실제 사용 설정·draw를 덤프로 확인해야 귀속할 수 있다.

## 4. DD: draw별 GPU 구간과 자원 상태

```text
drfull drawgpu on 0 256 64
# off 상태에서 설정 후 drfull on / drfull wait
# 진단 중 render를 충분히 진행한 뒤 on 상태에서:
drfull drawgpu dump
# drfull off 뒤:
drfull drawgpu off
```

`on <first-visible-native-draw> <count 1..256> <sample-frame-interval>`이다. GPU query는 opt-in일 때만 생성한다. immediate/deferred 모두 실제 제출에 timestamp를 넣는다. batch를 분해하지 않고 실제 native draw 묶음의 list 시작·source 수·index 수를 남긴다. 첫 구간이 빈 segment여도 프레임 ordinal을 초기화한다. culled draw와 Unity explicit draw는 native ordinal에 포함하지 않는다.

4개 비동기 slot, slot당 최대 256 draw, 4,096행 이력으로 제한했다. `DONOTFLUSH`로 완료된 결과만 받으며 Flush·spin·GPU 대기는 없다. pending slot은 덮지 않고 skipped로 센다. disjoint·역순 timestamp·실패 scope는 폐기한다. 덤프는 안정된 CPU snapshot을 만들어 행을 읽는 동안 render가 진행돼도 순서가 바뀌지 않는다.

두 TSV를 저장한다.

- 본문: frame/segment/ordinal/native draw ID/list range/source·index 수/draw GPU µs/segment GPU µs/같은 진단 표본의 DXGI usage·budget.
- `-textures.tsv`: draw ID를 renderer 경로·shader·Unity texture 속성에 연결하고 native 실제 Texture2D 크기·resource/SRV format·mip 수·MostDetailedMip·SRV mip 범위·sampler filter/address/aniso/bias/MinLOD/MaxLOD를 기록한다. 과거에 교체된 native ID는 retired로 표시될 수 있으므로 같은 정지 장면의 probe list와 함께 쓴다.

`gpu_us`는 해당 draw의 상태·상수 바인딩, 필요한 Grab 복사, 실제 draw를 포함한 GPU timestamp 경과 시간이다. segment 준비의 compute·상수 upload 등은 `segment_us`에 들어가고 개별 draw 합계에서는 빠질 수 있다. GPU busy, Present 대기, page fault 시간과 같지 않다. 진단 query 오버헤드가 있으므로 일반 ABBA에서는 반드시 꺼 둔다.

분석 명령:

```powershell
py -3.12 tools/direct-renderer/summarize-m4j-drawgpu.py <덤프.tsv> --output <요약.json>
```

draw별 표본 수·평균·최대·합계와 텍스처 정보를 정렬하고, frame/segment별 측정 draw 합계와 미측정 잔여를 계산한다. segment envelope는 행 수만큼 중복 합산하지 않는다. 빈 덤프·중복 ordinal·시간 범위 위반은 실패시킨다. 합성 41,000µs 입력에서 39,000µs draw와 나머지 1,000µs를 정확히 분리하는 self-test를 포함했다. **이는 실제 DD의 41ms를 분리해 관측한 값이 아니다.**

실제 원인 판정 순서:

1. `m4j-dd-drawgpu-0000`부터 256개 window를 진행해 느린 draw/texture를 찾는다. 0..2047 계획을 제공했으며 ordinal이 더 있으면 `first`를 늘린다. pending/skipped/overwritten 행을 보고 진단 시간을 늘린다.
2. 완전한 draw 범위 합계가 segment 41ms를 설명하는지 확인한다. 설명하지 못하면 prepareGpu/constants/Grab 또는 미측정 구간을 조사한다.
3. 같은 texture를 그리는 원본 Unity draw와 native draw를 RenderDoc/PIX에서 대조한다. Unity 실제 SRV·sampler는 이 덤프만으로 읽었다고 주장하지 않는다. resource/SRV sRGB format, MostDetailedMip, mip 수, sampler aniso/MaxLOD/MinLOD/bias, 실제 shader sampling instruction을 확인한다.
4. M4I 실게임에서 deferred/immediate 차이가 없었으므로 command-list 상주 가설은 원인 확정에서 제외한다. 텍스처에 mip이 없는 `pressure mip 2`도 저압박 증거로 쓰지 않는다. 필요하면 VidMm/ETW로 migration을 별도 확인한다.

## 5. ImageMaxSide 1024 대조 및 픽셀 계획

`out/direct-renderer/m4j/plans`에 실행 없는 JSON **일반 24개 + 진단 24개**를 만들었다.

- HELLO 50s / Exit(`sh`) 88s / DD 50s / WF 258s: plan·pixel·A1/B1/B2/A2 각 6개. 3440×1440, JALib 비활성 전제, safe=0, 동일 seek·설정·12초 ABBA, pixel noise 0 및 기존 pixel gate 유지.
- DD `drawgpu-0000..1792`: 8개 window 진단.
- DD `imagemax1024-{pixel,A1,B1,B2,A2}`: **맵 열기 전에** `set ImageMaxSide 1024`. A/B 모두 같은 크기 조건으로 매번 새로 로드한다. 기존 큰 이미지가 캐시에 남으면 새 프로세스 또는 완전한 맵 unload/reload로 대조한다. 단순히 열린 맵에서 설정만 바꾸지 않는다.
- 4맵 same-session bisect, HELLO particle 대조, Exit/DD/WF local-precision·Unity-Grab 대조 계획을 M4j 접두사로 유지했다. obsolete mip2/submission crossover 계획은 새 진단에서 제외했다.

1024라는 설정명만으로 저압박 판정하지 않는다. native 실제 texture 크기와 **DXGI usage < budget 및 headroom**을 확인한다. 계속 초과하면 A/B를 함께 512로 낮춰 다시 로드한다. 원본 크기 ABBA와 1024 ABBA는 각각 기록하며, 품질이 다른 두 조건의 픽셀을 parity 합격에 섞지 않는다. 1024 조건 내부 pixel/noise 통과 후 FPS를 해석한다.

AutoTest `set`은 종료/finally에서 로드 당시 값을 복구한다. 계획 실행기는 실행 전 설정 파일도 저장·복구해야 한다. 품질 복구 후 원래 큰 텍스처를 되찾으려면 맵 재로드가 필요하다. 이 작업에서는 계획을 실행하거나 설정을 변경하지 않았다.

## 6. 남은 실제 픽셀의 귀속 범위

- **HELLO >10 0.65%:** committed ColorWriteMask 수정 후의 자료를 출발점으로 삼는다. blend6/alpha/MinMax·sampler 변환 결함은 고쳤지만 현재 catalog상 blend6는 쓰지 않는다. 같은 세션의 onref/offref/offnoise·colors/probe와 sampler 덤프로 해당 경로의 실제 상태를 확인해야 한다. 현재 실제 남은 renderer 이름은 미측정이다.
- **WF 대각선 빛 가장자리 최대 36:** local precision과 batch transform 경로, 실제 sampler/filtered edge, Unity-Grab 대조를 순서대로 분리한다. 검증되지 않은 vertex/half 연산을 임의 수정하지 않았다. 기존 얇은 geometry transform fixture는 원본 DXBC 대비 exact를 유지한다. 이 합성 결과가 WF 실제 빛의 귀속을 대신하지는 않는다.
- **Exit ±2 약 11%:** 기존 동일 셰이더·순서 합성에서 중간 UNORM8 양자화만으로 ±1 및 합성 최대 2가 재현된다. 이번에도 이 대조를 유지했다. 하지만 실제 Exit 장식이 그 합성 경로라는 직접 증거는 없으므로 원인을 양자화로 확정하지 않았다. Canvas Graphic 포함 same-session bisect leaf와 local/Unity-Grab 결과를 함께 남기도록 했다.

실제 캡처 없이 잔여 차이를 모두 해결했다고 보고하지 않는다. 기존 bisect는 단일 개선 후보를 고르는 greedy 도구이므로 복수 층·상쇄 효과는 leaf 개별 대조로 확인해야 한다.

## 7. 재현·검증

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/direct-renderer/check-m4j.ps1
```

이 스크립트는 로컬 빌드·fixture·계획 생성만 수행한다. 게임 launch/install/commit 경로는 없다. 설치 DLL 두 개는 hash를 읽어 전후 비교한다. 내보내기 목록은 name/ordinal/hint 기준 74/74를 보존한다. 기존 opcode/payload 크기를 유지하고 새 진단 opcode 24..26만 추가했다(DrawProbeConfigure 16B, Row 64B, Readback 104B, TextureInfo 80B; 기존 Configure는 8B 유지).

검증 근거는 `out/direct-renderer/m4j/check-final.log`, `validation/`, `m4j.log`, `m4j-audit.json`, `final-hashes.json`이다. 4종 빌드와 최종 fixture 결과는 이 문서 말미의 최종 확인 기록을 따른다. 처음 추가한 lifecycle 진단 명령 stub 누락은 수정했고, 첫 계획 생성에서 M4j B setup pause 확장이 빠진 것은 계획 감사가 검출해 수정했다. 기존 빌드 경고를 오류 0과 혼동하지 않았다.


## 최종 확인 기록

최종 `check-m4j.ps1` 프로세스 **exit 0**. 최종 코드로 아래 검사를 완료했다.

| 검사 | 최종 판정 |
|---|---|
| native / DEV / Player / PlayerAuto | **4종 오류 0**, 기존 managed 참조·미사용 필드 및 ISPC 경고는 로그에 보존 |
| export | **name/ordinal/hint 74/74 보존**, 새 export 없음 |
| 설치 DLL | **SHA-256 2/2 일치**, 설치 쓰기·복원 없음 |
| managed/native 계약 | 기존 ABI 및 새 진단 구조 크기 확인, 원본 shader 21개·variant 1,012개·stage 2,024개·변수 6,594개·texture binding 2,407개 일치 |
| Unity enum | 설치된 CoreModule의 **10종** 값 확인 |
| 새 WARP 회귀 | mask 16종, blend6/alpha 변환, Min/Max, 6MiB 정지 업로드 0, 희소 변경·arena 순환·material 수정/복원·다중 segment 통과 |
| GPU 진단 | 실제 timestamp 완료 행 8개 검증, invalid 0; SRV/mip/실제 sampler descriptor 조회 통과 |
| 기존 WARP | 원본 shader 48사례 최대 채널 1·다른 픽셀 28; 부정 대조 5개 유지. 3모드 default/Grab/named-Grab 각 8사례 모두 최대 채널 0 |
| 배치·얇은 도형 | 배치 대조 72개 exact, 얇은 transform 대조 1,728개 exact, Grab 대조 48개 exact |
| 합성·복원 | composite 24개 float max 1.19209e-7 / RGBA8 max 2, shared stencil 9개, depth 대조 6개, 전체 pipeline·실패 segment 복원 통과 |
| 순서·수명 | layer/order 20,115개, lifecycle/refusal 25개, 증분 준비/실패/취소 및 100,000 steady 검사 통과 |
| pixel 도구 | metrics 19 + mask 15 + state 15 + control 17개 오프라인 검사 통과 |
| 계획·분석기 | M4j 일반 24 + 진단 24, capture 상한 8, candidate setup pause, 1024 로딩 전 설정 감사 통과. 분석기 합성 41ms/빈 덤프 거부 통과 |
| git·게임 | HEAD `ad27accdfa4463b94077f40bcdf7600ad0968d29` 유지, 커밋·게임 실행 없음 |

`m4j-audit.json`과 `final-hashes.json`에 최종 판정 및 소스/빌드 SHA-256을 저장했다. 이 결과가 보장하는 범위는 구현과 오프라인 회귀 검증이다. **실게임 Exit 회복 FPS·collect 회귀의 동적 원인·DD 41ms의 실제 draw/texture·HELLO/WF/Exit 잔여 픽셀의 실제 장식 귀속은 미측정**으로 유지한다.
