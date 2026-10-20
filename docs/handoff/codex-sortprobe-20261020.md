# Codex 과제 S3: `sortprobe` — 같은 sortingLayer·sortingOrder 를 가진 스프라이트의 실제 그리기 순서(타이브레이크) 규칙 실험 (개발자용/AutoTest 전용)

작업 폴더 E:\StutterFixWork\wt-decorender (브랜치 codex/night-decorender, 이전 과제들과 같은 규칙: 커밋·게임 실행·설치 금지, `#if DEV || AUTOTEST`, 새 파일 `SortProbe.cs` + AutoTest 명령 연결, csproj 조건부 Compile, 사용량 절약). 결과 문서 `docs/handoff/codex-sortprobe-20261020-result.md`.

## 배경: 왜 이 실험이 필요한가
`decorender`(장식을 그룹 메시로 그리기)는 `compare` 에서 대부분 최대 1~2/255 로 원본과 같지만 가끔 10~15/255 이상치가 난다. 원인은 **같은 (sortingLayerID, sortingOrder) 키를 가진 장식들끼리의 겹침 순서**다: 이 게임은 순서값 하나를 장식 수백 개(예: 키 0/1 에 34개 관리 대상 + 847개 다른 장식)가 공유하고, 그중 일부는 지금 안 그려지다가 나중에 보인다. 지금 그룹 메시 안 순서는 "원래 Renderer instanceID 오름차순"이라는 **가설**이다. 우리는 유니티가 실제로 같은 키 안에서 어떤 규칙으로 순서를 정하는지 알아야 한다(그래야 그룹 메시 안 순서와, 그룹 메시 대 남은 SpriteRenderer 간 순서를 똑같이 맞출 수 있다).
카메라 사실: 이 게임 카메라는 모두 정사영(orthographic), `transparencySortMode=Default`, 정렬축 (0,0,1), 카메라 4개(BGStaticCam/BGMovingCam/Camera/OverlayCam). 장식 셰이더 `Sprites/Tile`(queue 3000, 불투명 아님), 장식은 모두 z 거의 같음(2D).

## 실험 (게임 안, 곡 재생 중이 아닐 때, 활성 카메라 하나 필요: 편집기 열린 상태에서 실행)
`sortprobe` 가 임시 GameObject 들을 만들어 결과를 로그/표로 낸다. 방법: 겹치는 두 스프라이트(색 다름, 불투명 흰/검, 알파 1)를 같은 키에 두고 카메라로 `Render()`(Texture 에 그려 픽셀 읽기)해서 **어느 쪽이 위에 그려졌는지**를 센다. 한 번에 한 변수만 바꿔 규칙을 가린다. 각 케이스는 (A,B) 쌍과 3개 이상 묶음으로.
1. **생성 순서 / instanceID**: A 를 먼저 만들 때 vs B 를 먼저 만들 때. instanceID 오름차순/내림차순과 위 순서의 관계.
2. **씬 계층(형제 순서, 부모)**: 같은 부모 아래 siblingIndex 바꾸기, 부모가 다른 경우.
3. **z 위치/카메라와의 거리**: 월드 z 를 ±0.001/±0.1 등으로 바꿔 거리 규칙이 순서값 다음에 적용되는지.
4. **재질**: 같은 sharedMaterial vs 서로 다른 재질 인스턴스(장식은 인스턴스가 따로 있음), renderQueue 다르게.
5. **텍스처/스프라이트**가 다를 때(배칭 영향?).
6. **동적 변경**: enable/disable 을 바꾼 뒤, `forceRenderingOff` 토글 뒤, 위치 이동 뒤, `SetParent` 뒤에 순서가 어떻게 바뀌는지(순서가 "생성 시점 고정"인지 "매 프레임 재계산"인지).
7. 3개 이상: 6개 스프라이트를 무작위 속성으로 만들어 관측된 순서를 가설(예: instanceID 오름차순 단독, (거리, instanceID), (거리, siblingIndex) 등)과 비교 → 가설별 일치율.
8. **MeshRenderer vs SpriteRenderer** 같은 키: 같은 키에서 MeshRenderer(그룹 메시) 와 SpriteRenderer 의 상대 순서가 무엇으로 정해지는지(생성 순서? instanceID?) — 그룹 메시 렌더러를 장식들 사이에 끼워 넣을 수 있는지 판단하는 데 핵심.
각 실험은 50회 이상 무작위 반복해 일치율(%)로 보고. 규칙이 결정적이면 "규칙: ..."으로 정리하고, 비결정적(실행마다 다름)이면 그렇다고.

## 결과 문서에
실험별 표(일치율), 결론 규칙(가장 간단한 설명), 우리 `decorender` 의 순서 가설(instanceID 오름차순)이 맞는지, 그룹 메시를 어느 위치에 끼워 넣어야 같아지는지, 불가능하면 이유. 게임에서 돌릴 명령 순서(`open <HELLO>` 후 `sortprobe all` 등).
