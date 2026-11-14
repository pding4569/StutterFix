# 직통 렌더러 M3 결과 — Unity 대리 렌더러 정렬

2026-11-15, `E:\StutterFixWork\wt-v14prod`, 기준 HEAD `8491ed6`.

M2 구현에서 이어 M3 대리 MeshRenderer 경로를 구현했다. **구현·로컬 빌드·오프라인 검증 완료, 실게임 성능/픽셀 판정은 미실시** 상태다. 기본 꺼짐을 유지한다. 커밋, 게임 실행, 설치 DLL 교체를 하지 않았다.

## 구현

- [DirectDeco.M3.cs](../../DirectDeco.M3.cs): 모든 `dr2 on/now` 활성화를 M3로 연결했다. M2의 경계·숨김 층·RetireSequence·DrawRenderer·플러그인 이벤트 경로는 M3에서 호출하지 않는다. M2 소스는 비교 진단용으로 남겼다.
- [DirectProxyPolicy.cs](../../DirectProxyPolicy.cs): 원본 정렬 키로 후보와 남는 Renderer의 재질별 항목을 정렬하고, 남는 항목이 사이에 없는 연속 구간만 묶는다. 텍스처·블렌드·마스크 시험·점 샘플링·카메라 층·sortingLayer/order·renderQueue가 같은 경우에만 합친다. Grab은 장식마다 새 화면 복사가 필요하므로 항목별 구간으로 분리한다.
- 구간마다 MeshRenderer 하나, 슬롯 목록 ComputeBuffer 하나를 만든다. 메시의 사각형 네 정점은 `SV_VertexID / 4`로 슬롯을 찾고 전역 채널 16개·정적 32개를 읽는다. 바뀐 슬롯만 갱신하며 인접 dirty 슬롯은 한 번의 `SetData`로 합친다. native ABI를 추가하거나 바꾸지 않았다. 이번 경로는 명세가 허용한 **관리 측 부분 SetData 방식**이다.
- 정점/블렌드 식은 [generate-proxy-shader.py](../../tools/direct-renderer/generate-proxy-shader.py)가 production `dr2.hlsl`에서 생성한다. 독립적으로 옮겨 적은 별도 정점 식을 유지하지 않는다. 공통 수학 파일도 같은 원본에서 복사한다.
- Unity 셰이더 `StutterFix/DirectDeco`, `StutterFix/DirectDecoGrab`을 6000.3.10f1/D3D11 번들로 만들었다. [번들](../../effects/direct-deco/stutterfix_directdeco)은 관리 DLL 3종에 내장된다. 선곱·더하기·12종 Grab·점 샘플링·스텐실 Ref/Comp를 지원한다. Grab은 Unity의 실제 익명 `GrabPass`를 사용한다.
- 원래 장식 오브젝트/스크립트를 유지하고, 맡은 SpriteRenderer만 forceRenderingOff 한다. 기존 Poll/SkipLate/LogicUpdate 경로로 필드를 읽으며 원본 Transform 쓰기를 건너뛴다. 종료/반환 때 원본 forceRenderingOff 및 위치/색 경로를 복구한다. 풀 활성/비활성, 생성/복제, 파괴, 부모/정렬/재질 쓰기는 기존 알림 경로에 연결했다. DestroyImmediate 전 알림도 추가했다.
- 변하지 않은 구간은 Renderer·메시·재질·슬롯 버퍼를 재사용한다. 재계획 때 남는 렌더러의 키/재질도 dirty 알림으로 갱신한 캐시를 사용한다. 계층 생성/변경 시에만 장면 색인을 다시 만든다. 안정 프레임마다 Renderer 전수 검색이나 DrawRenderer 명령 재기록을 하지 않는다.
- 구간 bounds는 보이는 장식의 CPU 식으로 구한 사각형 합을 포함한다. 정렬 중심을 첫 장식의 원래 Center/Pivot에 고정하기 위해 대칭으로 넓히고, 수학 반올림 여유를 붙였다. 따라서 최소 AABB보다 넓을 수 있으나 장식을 잘라내지 않는 보수적 컬링이다. 모두 안 보이면 대리를 끈다.

## 동점·마스크 및 보수적 반환

동일 `(sortingLayer, sortingOrder, queue, distance)` 묶음 전체가 소유 후보이며 하나의 호환 구간으로 표현될 때만 묶는다. 남는 렌더러가 섞이거나 텍스처/변형이 달라 여러 대리가 필요한 묶음은 해당 묶음의 후보를 Unity로 돌린다. 같은 재질의 동점은 Unity의 Renderer 순서 규칙이 확립되지 않았으므로 전체 반환한다. 새 셰이더/재질 ID로 동점 순서를 추정하거나 거리를 조금 바꾸지는 않는다.

SpriteMask **자체는 원래 Unity 렌더러/스크립트에 남긴다**. 대리는 inside=`Ref 0 / NotEqual`, outside=`Ref 0 / Equal`, 쓰기 없음으로 Unity가 배치하는 스텐실과 연동한다. 따라서 외부 마스크나 남는 Unity 대상 때문에 마스크 묶음 전체를 반환하던 M2 경계는 쓰지 않는다. 마스크 자체의 Transform 비용까지 제거한 구현은 아니다. 에디터에서 원래 SpriteRenderer를 forceRenderingOff 한 상태의 마스크 연동과 custom range를 검증했다.

Canvas/SortingGroup은 개별 Renderer 키와 다른 정렬 단위다. 같은 층/순서의 후보만 보수적으로 반환한다. 후보 자신이 SortingGroup에 속한 경우도 반환한다. 비활성 남는 Renderer와 Canvas도 미래의 활성화를 위해 보수적으로 예약한다. 곡 중 반환한 항목을 자동으로 다시 소유하지는 않는다.

초기 LazySprite 빈 슬롯은 소유 슬롯을 유지하되 유효한 sprite/정적 자료가 생길 때까지 대리를 만들지 않는다. 새로운 장식은 원래 Unity 경로에서 장벽으로 등록한다. 기존 히트박스·타일/행성 따라감·비호환 셰이더 등의 분류도 유지한다. 이유별 수는 `reasons`에 모두 출력한다.

이번 대리 메시는 전체 사각형을 사용한다. native 경로의 빈 칸 제거 도형은 M3에 적용하지 않았다. `tight` 설정이 켜져 있어도 M3는 사각형이다. 명세의 사각형 선택에 해당하며, 과도한 투명 영역의 GPU 비용은 실게임에서 따로 확인해야 한다.

## M2 NullReferenceException 원인 기록

`RetireSequence`에는 `movedLayers`에 남은 GameObject를 Unity 생존 검사 없이 `go.layer = original`로 복원하는 경로가 있다. 파괴된 Unity 오브젝트도 관리 딕셔너리의 키로 남을 수 있어, M2 로그의 `GameObject.set_layer ← RetireSequence ← ApplyBoundaries`와 일치하는 실패 경로다. 게임 재현은 하지 않았으므로 파괴한 호출자와 정확한 파괴 시점은 확정하지 않았다. M3는 남는 렌더러의 layer를 바꾸거나 이 반환 경로를 사용하지 않는다.

## 검증 결과

증거: [out/direct-renderer/m3](../../out/direct-renderer/m3).

| 검사 | 결과 |
|---|---|
| DEV / Player / PlayerAuto / native | 모두 오류 0. 기존 관리 경고 84 / 63 / 63개 |
| Unity 셰이더 번들 | 6000.3.10f1/D3D11 빌드 통과, 20,598바이트 |
| Unity 실제 대리 셰이더 기본 대조 | 64×64에서 원래 SpriteRenderer와 다른 픽셀 0 |
| 부분 버퍼 업로드 | 알파 슬롯 부분 갱신 후 전체 검정 확인 |
| SpriteMask inside / outside | 각각 기준의 유색 픽셀 1,024 / 3,072개, 다른 픽셀 0. 빈 화면끼리의 비교가 아님 |
| custom mask range 밖 | 다른 픽셀 0 |
| Unity Grab 12모드 | CPU 화면 블렌드 기준과 최대 채널 차 0 |
| 2슬롯 / 역순 슬롯 | 최대 채널 차 0 / 다른 픽셀 0 |
| M3 순서 정책 | 고정 9조건 + 무작위 200,000행 통과. 슬롯 누락·장벽 횡단·모호한 동점 소유 없음 |
| 호출 지점/래퍼 IL 감사 | 2,085메서드, 5,468호출 지점, 249래퍼 통과. 패치 설치/게임 메서드 호출은 하지 않음 |
| 기존 정렬 회귀 | 20,000개 키를 독립 8필드 기준과 비교, 일치 |
| 기존 WARP 수학/색/UV/스텐실 회귀 | 통과. native VS 40조건 최대 월드 오차 `7.62939453e-06`, 블렌드 최대 채널 차 2 |
| export | 이름/ordinal/hint 73개 보존 |
| 내장 자산 | 관리 DLL 3종에 현재 native와 proxy 번들 포함 |
| 설치 보존 | 시작/종료 SHA-256 2/2 일치, [artifacts.log](../../out/direct-renderer/m3/artifacts.log) |

Unity 에디터 fixture는 실제 D3D11 카메라 렌더를 사용하지만 게임을 실행하거나 연결하지 않는다. 정책 fixture의 반환 수 89,395 / 200,000행은 무작위 입력의 시험 결과이며 실제 맵의 맡는 비율이 아니다. WARP native VS 회귀는 공통 식에 대한 증거이고 M3 게임 통합 시험을 대체하지 않는다.

## 아직 판정하지 않은 항목

`dr2 verify`와 M3 `dr2 seqcheck` 명령을 구현/유지했지만 실제 게임에서 실행하지 않았다. seqcheck는 독립 장면 스냅샷의 재질별 키와 현재 대리 슬롯 순서를 비교하고 stale/missing/orderMismatch/동점/Canvas·SortingGroup 충돌을 보고한다. 합격에는 stale/missing/orderMismatch/reservedOrderConflicts 모두 0이 필요하다. 이 명령과 verify는 성능 측정에서 제외한다.

HELLO 준비 <300ms, commit_max <2ms, 통상 직통 비용 <50µs/프레임, 이미지 소유 비율 목표는 **미측정**이다. dirty 자료가 많거나 카메라 이동/사용자 정렬 축 때문에 bounds·순서가 변하면 CPU 식 계산과 구간 재계획 비용이 생긴다. Unity SetData/Renderer 생성 비용까지 포함한 목표 달성을 선언하지 않는다. 실게임의 복잡한 중첩 마스크·그룹·다른 모드의 변경 후크·여러 카메라·풀/맵 전환·전체 곡 픽셀/충돌 보존도 추가 검증 대상이다.

## 재현

다음 명령은 저장소 산출물과 임시 Unity 에디터 프로젝트만 사용한다. 설치 DLL 복사나 게임 실행을 포함하지 않는다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/direct-renderer/build-proxy.ps1 -RunFixture
powershell -NoProfile -ExecutionPolicy Bypass -File tools/direct-renderer/check-m1b.ps1 -NativeBuild -OutputDirectory out/direct-renderer/m3
dotnet build StutterFix.csproj -c Release
dotnet build StutterFix.csproj -c Release -p:Edition=Player
dotnet build StutterFix.csproj -c Release -p:Edition=Player -p:AutoTestBuild=1
dotnet run --project tools/direct-renderer/m3-check -c Release
dotnet run --project tools/direct-renderer/m2-check -c Release -- .
py -3.12 tools/direct-renderer/check-m3-artifacts.py
```

실제 로그는 `build-{bundle,dev,player,playerauto,native}.log`, `unity-fixture.{txt,log}`, `policy.log`, `hooks.log`, `fixture.log`, `exports-result.txt`, `artifacts.json`, `final-hashes.json`에 있다. 재현 시 stdout을 해당 로그에 저장해야 마지막 artifact 검사가 새 로그를 확인한다. 설치 보존 검사는 이번 시작 기준 `installed-before.json`을 유지한다. 별도 세션의 새 기준선은 작업 시작 시 새 증거 폴더에 기록해야 한다.

## Claude 측 실게임 인계

로컬 최종 산출물: `bin/PlayerAuto/StutterFix.dll`, `native/sfnative.dll`. 이 작업에서는 설치하지 않았다. 기준 A=`ppGpuCam`, 후보 B=`drOn`으로 현재 프리셋의 정상 모드/RT/구간을 유지한다. `dr2 state`가 `active=True mode=M3-proxy`인지 반드시 확인한다. 이전 M2 상태를 M3 결과로 집계하지 않는다.

HELLO 50초 / Windflower 258초 / DD 50초 / Exit 88초에서 새 재생마다 state → seqcheck → verify → drPix를 수집한다. 이후 풀 반환/재활성화·깊이/재질/마스크 범위 변경·생성/파괴·재시작/맵 전환/off/Unload를 확인한다. M3 state의 `owned/imageTotal/total`, `proxies/visibleProxies`, `reasons`, `returnedTies`, `createdProxies/reusedProxies`, `replans`, `uploadBytes`, `prep_us`, `commit_us/commit_max_us`를 함께 보관한다. 원래 SpriteMask 수와 실제 대리 masked 수를 구분한다.

drPix 목표는 다른 픽셀 <0.1%, 최대 채널 ≤3이다. 그 뒤 진단/캡처 없는 ABBA를 실행해 FPS·메인/렌더 스레드·GC·최장 프레임을 비교한다. Poll을 포함한 통상 직통 비용은 별도 timing 판으로 측정하며 중첩 시간을 단순 합산하지 않는다. 실패/목표 미달은 해당 항목 그대로 남기고 기본 꺼짐을 유지한다.
