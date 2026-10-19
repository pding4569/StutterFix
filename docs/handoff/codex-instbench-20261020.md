# Codex 과제 N3: `instbench` — 장식 N개를 GPU 인스턴싱으로 그리는 비용 마이크로벤치 (개발자용/AutoTest 전용)

작업 폴더 E:\StutterFixWork\wt-animbench (브랜치 codex/night-animbench, MicroBench.cs 가 이미 있음 - 같은 파일에 추가해도 되고 InstBench.cs 새 파일도 됨). 커밋·게임 실행·설치 금지. 빌드 `dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1`. 새 코드는 `#if DEV || AUTOTEST`. 사용량 절약: 필요한 부분만 읽고, 결과 문서는 docs/handoff/codex-instbench-20261020-result.md.

## 목적
Arche 곡 270초대는 움직이는 이미지 장식 약 2,100개를 매 프레임 SpriteRenderer/Transform 으로 쓴다(메인 스레드 약 4~5ms). 장식을 개체 대신 "인스턴스 데이터 배열 + 인스턴싱 그리기 몇 번"으로 그리면 메인 스레드 비용이 얼마나 되는지, GPU 쪽 비용은 어떤지 잰다. (장식 이동 값은 곡 시각의 함수라 나중에 GPU 에서 계산할 수도 있다 - 이번엔 CPU 가 채운 인스턴스 배열을 올리는 비용만.)

## 명령 `instbench <N=2000> <프레임수=300> <방식>` (animbench 와 같은 규칙: 곡 재생 중 거부, 끝나면 정리, 카메라 필요)
공통: 부모 없는 1x1 흰 스프라이트(또는 쿼드 메시) N개 분량의 인스턴스, 프레임마다 결정적 값(위치 x,y / 회전 z / 크기 x,y / 색 rgba)을 갱신. 스프라이트 종류 K 개(`K` 는 방식 뒤 선택 인수, 기본 1, 4, 16, 64 시험)로 나눠 텍스처가 K 가지인 경우도 잰다(텍스처는 임의 색 1x1 또는 4x4).
- `renderers` : 기준선. SpriteRenderer N개 + Transform 쓰기(animbench main 과 동일), 비교용.
- `drawinst` : `Graphics.DrawMeshInstanced`(1023개씩 배치) 로 쿼드 메시를 그림. 인스턴스별 Matrix4x4[] 와 색(MaterialPropertyBlock Vector4[] / 또는 인스턴싱 지원 셰이더의 프로퍼티). 가능한 셰이더: `Sprites/Default`(인스턴싱 변형이 있는지 확인), 없으면 `Unlit/Texture`·`Hidden/Internal-Colored` 등 Shader.Find 가능한 것. 사용 불가 시 이유를 적고 건너뜀.
- `renderparams` : `Graphics.RenderMeshInstanced`(RenderParams) 같은 계열이 이 엔진 버전에 있으면.
- `drawindirect`/컴퓨트: 이 엔진/그래픽 API(D3D11)에서 `ComputeBuffer` 인스턴스 데이터 + `Graphics.RenderMeshIndirect` 가 되는지만 확인(되면 N 개 데이터를 ComputeBuffer 로 올리는 비용과 그리기 비용을 잰다, 안 되면 이유).
- 텍스처가 K 종류면 텍스처별로 그리기 호출을 나눈다(그리기 횟수 = K × ceil(N/K/1023)). Texture2DArray 를 쓰는 변형이 가능하면 하나 추가(`TextureArray` 인스턴스 인덱스 프로퍼티).
측정: 방식·K마다 워밍업 60 + 측정 300프레임, 프레임당 (a) 메인 스레드 우리 구간 ms(배열 채우기+그리기 호출), (b) 전체 프레임 ms 평균/p99. GPU 시간이 필요하니 `Time` 으로만 보지 말고, 메인 스레드 비용과 전체 프레임을 둘 다 적고 "GPU 가 병목인지" 는 렌더러 방식 대비 전체 프레임 차이로 해석할 수 있게 한 줄 요약을 로그에 남긴다. 로그 접두사 `[마이크로벤치] instbench ...`.
정렬 순서(sortingOrder)는 이번 범위가 아니다(단일 정렬 슬롯으로 그림) — 결과 문서에 "정렬 순서 미반영"이라고 명시.

## 결과 문서에
- 사용법, 빌드 결과, 방식별 가능/불가와 이유(셰이더 인스턴싱 지원, 엔진 API 존재), 막힌 것.
