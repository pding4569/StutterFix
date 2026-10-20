# Codex 과제 S2d: decorender — 같은 정렬값을 쓰는 외부 렌더러(타일 등)와의 겹침 순서 문제 제거 + 정점 색 float 옵션

같은 규칙(E:\StutterFixWork\wt-decorender, 커밋·게임 실행·설치 금지, `#if DEV || AUTOTEST`, 사용량 절약). 결과 문서 `docs/handoff/codex-decorender-4-20261020-result.md`.

## 게임에서 확인한 결과 (Arche, 움직이는 장식 1,795개·그룹 61개·아틀라스 2장 선택, `compare` 곡 264~273초)
- 대부분의 비교는 거의 같다: 최대 채널 차이 1~2/255, 다른 픽셀 7천~2만(전체 368만 중 0.2~0.6%).
- **이상치**: 곡 267.7초에 최대 15/255, 29만 픽셀(8%)이 달랐고 그중 19만이 8 초과. 차이 이미지(diff16)를 보면 **타일이 놓인 길(트랙) 모양 영역에만** 줄무늬로 차이가 난다. 곡 252초(선택 16개일 때)에도 최대 10/255 이상치. 즉 어떤 반투명 장식이 타일과 겹치는 곳에서 **장식 대 타일 그리기 순서가 원본과 다르게 뒤집혔다**고 본다(같은 sortingLayer/sortingOrder 를 쓰는 비관리 렌더러와의 순서는 보존 안 된다고 이미 적어 둔 한계).

## 고칠 것
1. **`on` 때 정렬 키 충돌 조사**: 씬의 모든 `Renderer`(타일의 MeshRenderer/SpriteRenderer, 행성, 기타 비관리 장식 포함. `Object.FindObjectsOfType<Renderer>()` 한 번, 개발자 도구라 괜찮다)를 읽어 키 `(sortingLayerID, sortingOrder)` 별로 (a) 관리 대상 장식 수, (b) 비관리 렌더러 수(종류별: tile/planet/other-deco/…)를 센다. 로그: 충돌하는 키 수와 관련 장식 수, 처음 몇 개 키의 상세(어떤 렌더러 이름/종류가 같은 키를 쓰는지).
2. **제외 규칙 추가**: (i) 관리 장식의 키를 비관리 렌더러도 쓰면(`shared-order`) 그 키의 관리 장식을 관리에서 뺀다(원래 SpriteRenderer 로 둔다), (ii) 같은 키의 관리 장식이 서로 다른 그룹(다른 아틀라스 페이지/재질)로 갈라지면(`split-order`) 그 키 전체를 뺀다. 제외 개수를 `state` 에 넣는다. 반투명 장식과 타일의 순서가 문제가 된다면 이게 사라져야 한다.
3. **정점 색 float 옵션**: 지금은 `Mesh.SetColors` → 기본 8비트 정점 색. `decorender on 20 floatcolor` 처럼 옵션(두 번째 인수 `floatcolor`)을 주면 정점 색 속성을 `VertexAttributeFormat.Float32`(또는 Float16)로 두고(`SetVertexBufferParams` 로 위치/색/UV 선언, 색은 `Color` float4) 채운다. 셰이더 `Sprites/Tile` 이 정점 색을 float 로 받는지 확인(되면 그대로 8비트 반올림이 사라진다). 안 되면 이유를 결과 문서에. 기본은 지금처럼 8비트.
4. 나머지(관찰 20프레임, 움직이는 장식만, 개별 복구, compare, 선택 영역 안/밖)는 그대로.

## 결과 문서에
바꾼 점, 새 `state` 항목(shared-order/split-order 제외 수, 충돌 키 상세), 빌드 결과, 게임에서 돌릴 순서(`decorender on 20`, `compare`×N, `state`, `off`, 그리고 `on 20 floatcolor` 로 한 번 더).
