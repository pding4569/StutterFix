# Codex 임무 livedeco: 활성(보이고 움직이는) 장식을 Unity 오브젝트 없이 그리는 렌더러 — 2단계, 게이트 방식

작업 폴더 `E:\StutterFixWork\wt-livedeco` (브랜치 `codex/night-livedeco`, `codex/night-framecmp` 위; framecmp 완성본 포함). 게임 실행·설치·커밋 금지(Claude 가 한다; 시험은 `E:\StutterFixWork\run_json.py <steps.json> wt-livedeco <이름> --probe --keys ...` 로 돌리므로 **steps JSON 생성기** `tools/optimization/livedeco-steps.py --chart <Arche FULL> --output x.json` 를 제공할 것). 결과 `docs/handoff/codex-livedeco-20261021-result.md` + `-o`(10줄). **먼저 `docs/ROADMAP-EXTREME-20261021.md`, `docs/optimization-36.md` 끝 절들, `docs/handoff/codex-matshare-20261021-result.md`, `codex-gpusplit-3-20261021-result.md`, `codex-framecmp-*-result.md` 를 읽을 것.**

## 목표와 근거
Arche 262→276초: 원본 약 125 FPS(8.0ms), 정지 장식 441 FPS, 장식 0개 546 FPS. 프레임의 71%(5.7ms)가 **활성 장식 약 2,000개**와 그 처리. 이 활성 집합을 SpriteRenderer/Transform 오브젝트 대신 소수의 메시 draw 로 그리고(오브젝트 쓰기 제거), 이후 GPU/잡 애니메이션으로 가는 것이 3~4주 로드맵의 2단계. 지금까지: draw 약 1,840(장식당 재질 복제), 재질 내용 서명 18종(99.4% 동일), 텍스처 301종이 순서상 엇갈려 재질 공유만으로는 live 2,126→1,227 구간(−42%); **텍스처를 합치면 구간 수는 "비소유 렌더러(타일·행성·텍스트 등)가 끼는 횟수"로만 결정**된다. 정렬 규칙: (sortingLayer, sortingOrder, 큐, 거리) 같으면 재질 인스턴스 ID 부호 있는 오름차순이 뒤→앞(`rendererPriority` 무관). 화면 동일 증명은 `framecmp`(VERDICT PASS, 중앙 20% 제외)로.

## 게이트 1 — 센서스 (게임 시험 없이 설계 + 내가 돌릴 steps)
`livedeco census`: 재생 중(262·270초)의 live 이미지 장식 전체 렌더러를 Unity 정렬 순서(위 규칙; matshare census 의 정렬 구현 재사용)로 줄 세우고, **우리가 소유할 후보**(이미지 장식 중 live, 재질 서명이 "기본형"[RepeatX=RepeatY=1, 키워드 없음, shader Sprites/Tile 기본], 마스크/히트박스/특수 없음)를 표시한 뒤, 소유 후보 사이에 **비소유 렌더러(타일, 행성, 텍스트, 파티클, 비기본 서명 장식 등)가 끼는 횟수 = 필요한 메시 draw 수(런 수)**, 소유 후보 수, 비소유로 남는 live 장식 수와 이유별 분류를 로그. **통과 기준: 런 수 ≤ 200 이고 소유 후보가 live 의 ≥80%** 이면 게이트 2 진행. 아니면 결과 문서에 "여기서 중단 + 구체적 이유"를 쓰고 멈춘다(거짓 낙관 금지).
## 게이트 2 — 정지 화면 동일 프로토타입 (`livedeco on|off|state`, 개발 전용, 기본 꺼짐)
- 소유 후보의 텍스처를 한 장의 아틀라스(또는 배열)로: 우선 **비트 정확**(압축 블록 정렬 복사 `Graphics.CopyTexture` 로 DXT 블록 그대로, 4px 정렬, 테두리는 셰이더 쪽 수동 클램프/이중선형으로 처리) 설계 가능성을 따져 구현, 불가하면 무압축 RGBA32 아틀라스(VRAM 은 시험이라 허용)로 대체하고 차이를 `framecmp` 수치로 보고. 포인트 샘플링 키워드 장식은 별도 처리/제외.
- 런마다 MeshRenderer 1개: 런의 **첫 멤버의 sortingLayer/order/z 와 재질(빌린 재질: 그 멤버의 재질 인스턴스 ID 위치 유지)**로 정렬 위치를 점유(런은 비소유 렌더러가 끼지 않는 연속 구간이므로 첫 멤버 키로 충분 — 이 주장을 정렬 규칙으로 검증하는 fixture), 멤버의 SpriteRenderer 는 `forceRenderingOff`(복원 보장). 멤버의 변환·색·스프라이트 지오메트리·피벗·크기·알파(premultiplied 처리 포함 `Sprites/Tile` 의 출력 식과 동일)를 정점에 굽는다. 우선 CPU 에서 매 프레임 SpriteRenderer/Transform 값을 읽어 메시를 갱신(비용 계측), 셰이더는 게임의 `Sprites/Tile` 와 같은 출력 식(`PixelSnap`, 색 곱셈, 알파 처리)을 쓰는 우리 셰이더(AssetBundle 로 번들, `fsr/` 의 기존 FSR 번들 빌드 방식 참고).
- 장식이 켜지고 꺼질 때(live 집합 변화)·소유 멤버 전환: 런 재계산(증분), 전환 프레임 비용 분할.
- **검증**: Arche 264초 정지(`pause`)에서 `framecmp grab A`(livedeco 끔) → `livedeco on` → `grab B` → `diff A B`(제외 영역 밖) → `off` → `grab A2`; 기대 `ab_out=0`(또는 사용자가 허용한 눈에 안 띄는 차이 ≤1~2/255 의 극소수). 차이가 나면 위치(상위 영역 좌표)별 원인 분류.
- **성능**: 재생 중(`pause` 없이) 262→276초 `fuse` 창에서 `livedeco on` 대 끔 ABBA(프레임 수/FPS, 프레임당 livedeco 비용 분해). 기대 하한 목표: 활성 렌더러의 엔진 비용 약 2.5ms 감소.
## 규칙
빌드 3종(`-c Release`; `-p:Edition=Player`; `-p:Edition=Player -p:AutoTestBuild=1`) 오류 0 + LoadCheck + fixture. Player 제외·기본 꺼짐. `Module.ResolveSignature` 금지, 새 Harmony 패치는 이름+인자 수+호출 규약으로 정확히 하나. 헤르츠 맵 이름·비공개 프로젝트 언급 금지, 100만 타일 맵 열기 금지. 막히는 지점은 **이유와 필요한 다음 관측**을 구체적으로. 게이트 1 에서 멈춰도 결과 문서는 완결된 형태로.
