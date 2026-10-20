# Codex 과제 S2c: decorender — `Sprites/Tile` 셰이더 장식을 허용 (실측 속성 반영)

같은 규칙(E:\StutterFixWork\wt-decorender, 커밋·게임 실행·설치 금지, `#if DEV || AUTOTEST`, 사용량 절약). 결과 문서 `docs/handoff/codex-decorender-3-20261020-result.md`.

## 게임에서 확인한 사실
이 게임의 **이미지 장식 재질은 전부 셰이더 `Sprites/Tile`** 이다(`Sprites/Default` 가 아님). 앞 단계의 분류(tiling-shader=2173)가 이걸 전부 제외해 선택이 0개가 됐다. 실제 속성(`decocensus props` 로 읽음, Arche 곡 264초):
```
shader=Sprites/Tile keywords= queue=3000 passes=1 props: _MainTex(Texture) st=(1,1)(0,0)  _Color(Color)=(1,1,1,1)  PixelSnap(Float)=0  RepeatX(Float)=1  RepeatY(Float)=1 | wrap=Clamp
shader=Sprites/Tile keywords= ...                                                         (같은 속성)                                       | wrap=Repeat
shader=Sprites/Tile keywords=USE_POINT_SAMPLING ... (같은 속성)                                                                              | wrap=Clamp
```
즉 타일링은 `RepeatX/RepeatY` 두 float(기본 1) + 텍스처 wrap 모드, 점 샘플링은 `USE_POINT_SAMPLING` 키워드 변형.

## 고칠 것
1. **허용 규칙**: 셰이더 `Sprites/Tile` 이어도 `RepeatX==1 && RepeatY==1 && PixelSnap==0 && _MainTex ST 가 (1,1,0,0) && 키워드 없음 && 텍스처 wrap 이 Clamp` 이면 아틀라스 가능(`Sprites/Default` 계열도 기존대로 허용). 제외: `USE_POINT_SAMPLING`(셰이더가 텍스처 크기를 써서 아틀라스에서 달라질 수 있음 — 이유 항목 `point-sampling` 으로 개수 기록), Repeat≠1, wrap≠Clamp, 그 밖의 키워드.
2. 그룹 재질: 원래 장식 재질을 복제해(셰이더 `Sprites/Tile` 그대로) `_MainTex` 만 아틀라스로 바꾸고 `RepeatX/RepeatY=1`, `PixelSnap=0` 유지. 정점 색은 SpriteRenderer.color. 이 셰이더가 정점 색·`_Color`·`_RendererColor` 를 어떻게 쓰는지(코드/로그로 알 수 있는 한) 확인해서 `_Color`/렌더러 색 처리를 원래와 같게 맞춘다(알 수 없으면 `state`/결과 문서에 "미확인"으로 적고 compare 결과로 판단하게 둔다).
3. 배제 이유 집계의 `tiling-shader` 항목은 새 규칙에 맞게 `repeat-not-one`, `wrap`, `point-sampling`, `pixel-snap` 등으로 나눈다.
4. 나머지(관찰 20프레임, 움직이는 장식만, 변화 장식 개별 복구, compare 안/밖 집계)는 그대로.

## 결과 문서에
바꾼 규칙, 새 배제 항목, 빌드 결과, 게임에서 돌릴 순서(`decorender on 20`, `compare`×N, `state`, `off`).
