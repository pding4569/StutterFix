# Codex 과제 N4: `meshbench` — 장식을 "그리기 순서값별 MeshRenderer 하나(동적 메시)"로 그리는 비용 (개발자용/AutoTest 전용)

작업 폴더 E:\StutterFixWork\wt-animbench (브랜치 codex/night-animbench, InstBench.cs·MicroBench.cs 가 이미 있다. 같은 규칙: `#if DEV || AUTOTEST`, 곡 재생 중 거부, 끝나면 정리). 커밋·게임 실행·설치 금지. 빌드 `dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1`. 사용량 절약: 필요한 부분만 읽는다. 결과 문서 docs/handoff/codex-meshbench-20261020-result.md.

## 배경 (측정 결과, 같은 장면)
`instbench`: SpriteRenderer 2,000개를 갱신하는 기준선 대비, 인스턴싱 2~64회 그리기는 메인 스레드 구간 -0.37ms, **전체 프레임 -2.0~-2.3ms**. 즉 SpriteRenderer 개체 수 자체(엔진의 Transform 변경 처리·컬링·렌더 객체 만들기)가 장식 하나당 약 1µs 비싸다.
문제는 그리기 순서: 실제 Arche 의 움직이는 이미지 장식 약 1,100개는 (sortingLayer, sortingOrder) 값이 42가지, 텍스처 10가지, 재질 종류 1가지다. 인스턴싱 그리기(Graphics.DrawMeshInstanced 등)는 유니티 2D 정렬에 끼지 못하지만 **MeshRenderer 는 SpriteRenderer 와 같은 sortingLayerID/sortingOrder 로 정렬된다**. 그래서 "같은 (sortingLayer, sortingOrder, 텍스처) 장식을 한 개의 MeshRenderer 에 모아 동적 메시(장식 하나당 쿼드 4정점)로 그린다"를 시험한다.

## 명령 `meshbench <N=2000> <측정프레임=300> <G=42> <T=10>` 
- N 개의 가짜 장식을 G 개 그리기 순서값 × T 개 텍스처(1x1 또는 4x4 임의 색) 조합 그룹으로 나눠(결정적 분배, 빈 그룹은 만들지 않는다) 그룹마다 GameObject(MeshFilter+MeshRenderer, 재질 `Sprites/Default` 복제본에 그 텍스처, MeshRenderer.sortingOrder = 그룹의 순서값, 같은 sortingLayerID).
- 프레임마다 장식 값(위치 x,y / 회전 z / 크기 x,y / 색 rgba, 결정적)을 갱신하고 그룹 메시를 다시 채운다. 정점 = 쿼드 4개(위치는 CPU 에서 TRS 로 계산해 월드 좌표로, uv 고정, 정점 색). 채우는 방법 두 가지 비교: (a) `Mesh.SetVertexBufferData(NativeArray<Vertex>)` + `MeshUpdateFlags.DontRecalculateBounds|DontValidateIndices|DontNotifyMeshUsers`, 인덱스는 처음 한 번만, 경계는 큰 고정 값; (b) 가능하면 `Mesh.MarkDynamic` + `SetVertices/SetColors` 리스트 방식. (a)가 더 빠를 것으로 본다. 가능하면 NativeArray 채우기를 `IJobParallelFor` 로 워커 스레드에서 하는 변형(c)도(메시 쓰기는 메인에서).
- 비교 기준선: 같은 N 으로 `instbench ... renderers` 와 같은 조건(같은 카메라, 같은 값) — 이미 있는 코드 재사용. 결과로 "renderers 대비 Δ메인, Δ전체프레임(평균/p99)"를 같은 형식으로 로그.
- 로그 `[마이크로벤치] meshbench ...`: 그룹 수, 총 정점, 방식(a/b/c)별 메인 구간 ms(값 계산+정점 채우기+메시 업로드 호출)와 전체 프레임 ms, renderers 대비 차이.
- 여러 설정 시험: (N=2000,G=42,T=10), (N=1100,G=42,T=10), (N=2000,G=1,T=1)(최선), (N=2000,G=400,T=10)(그룹 많음), 마스크/정렬 반영 안 된 점은 결과 문서에 명시.
- 이 시험이 보려는 것: 그룹별 동적 메시가 개별 SpriteRenderer 보다 전체 프레임에서 얼마나 싸고(목표 -1.5ms 이상이면 유망), 메인 스레드 비용(CPU 로 정점을 채우는 값)이 얼마인지.

## 결과 문서에 
사용법, 빌드 결과, 방식별 가능/불가와 이유, 막힌 것.
