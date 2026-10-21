# Codex 임무: "맵 열 때 굽기(bake)" + 렌더 경로 설계 — 장식 트레이스의 완성형

2026-10-20. 작업 폴더 `E:\StutterFixWork\wt-bake` (브랜치 `codex/night-bake`). 게임 실행·설치·커밋 금지(Claude 가 한다). 결과 문서 `docs/handoff/codex-bake-20261020-result.md` + `-o` 파일(10줄 이내 한국어).

## 목표 (사용자 요구)
지금은 곡 중에 이벤트가 올 때마다 장식 애니메이션(트윈 비슷한 기록 `Rec`)을 만들고, 같은 장식에 새 이벤트가 오면 이전 것을 교체/Kill 하고, 매 프레임 갱신한다(Arche 258~278초에서 장식 처리 약 5.5ms/프레임, 교체 시작 약 14만 번/14초). 이것을 **맵을 열 때 한 번에 전부 표로 굽고**(이벤트×장식 쌍을 구간 표로), 곡 중에는 **곡 시간만 보고 표에서 값을 꺼내는** 방식으로 바꾸고 싶다. 용량은 Arche 기준 구간 약 41만 개 ≈ 17MB (`docs/roadmap-deco-pipeline-20261020.md` 마지막 절).

## 이미 있는 것 (읽고 재사용, 다시 만들지 말 것)
- `DecoSim.cs`(개발/자동 시험): 라이브 `Rec` 테이프 기록 + SoA 배치 평가(`DecoBatchEval`)가 `DecoAnim` 출력과 **비트 동일**(약 94만 비교 불일치 0, 평가 0.5~0.8ms). 즉 "구간 표 → 값" 평가 의미는 검증됐다. 모자란 것은 **표를 이벤트 파일에서 미리 굽는 컴파일러**.
- `DecoAnim.cs`, `DecoAnim.Batch.cs`, `DecoAnim.GroupStep.cs`, `DecoAnim.FastLane.cs`: 현재 장식 애니메이션의 의미(이징, 상대/절대, 태그, 길이 0, 반복, 교체 규칙). 의미는 반드시 이것과 같아야 한다.
- 브랜치 `codex/night-decopipe`(DecoPipe*.cs), `codex/night-decorender2`(DecoRender*.cs, SortProbe.cs): 표+그룹 메시 프로토타입. 값은 맞았지만 프레임당 관리 비용이 예산(≤2ms)의 수 배였고 켤 때 14.6초 정지, 그룹 메시는 정렬 동률 규칙 때문에 가져갈 수 있는 장식이 6~108개뿐이라 접었다(`docs/optimization-36.md` 끝 4개 절 반드시 읽을 것). `git show codex/night-decopipe:<파일>` 로 볼 수 있다.

## 파트 A — 굽기 컴파일러 (구현)
`DecoBake.cs`(`#if DEV || AUTOTEST`, 기본 꺼짐, Player 제외, `Main.Unload` 되돌리기):
1. 입력: 게임이 맵 열 때 만든 이벤트 목록(`LevelEvent` MoveDecorations/SetText/Event 태그/`RepeatEvents`/`Twirl` 등 시간을 바꾸는 것까지)과 타일의 `entryTime` 계산 결과, 태그→장식 조회(`scrDecorationManager`). 곡 시간 변환은 게임이 쓰는 값을 **그대로 읽는다**(직접 다시 계산하지 말 것; 계산 의미를 모르겠으면 읽는 지점을 문서에 적고 멈출 것).
2. 출력: 장식(인스턴스)별·속성(위치 x/y, 회전, 크기 x/y, 색 RGBA, 불투명도, 기타 지원 속성)별 시간순 구간 표(시작 시각, 길이, 시작값/끝값, 이징 id, 상대/절대 기준, 교체 규칙 반영). **교체**(같은 장식 같은 속성에 새 이벤트가 오면 이전 것 중단)는 표를 미리 잘라 둔다. 길이 0 즉시 값은 구간 하나로.
3. 평가기: 곡 시간 t 에서 표를 이진 탐색(또는 인덱스 순회)해 값을 낸다. `DecoBatchEval` 재사용 가능하면 재사용. **무상태**(같은 t → 같은 값, 되감기·seek 무관).
4. 검증 명령(자동 시험, `decobake verify`): 라이브 `DecoSim` 테이프(같은 프레임 시각)와 굽기 평가를 프레임마다 대조해 불일치 수·최대 오차·불일치 장식 상위 8개를 로그. 비트 동일이 목표이고, 안 되면 원인별 분류(교체 규칙/이징/상대 기준/부동소수 합산 순서)를 문서에.
5. 비용 측정 명령(`decobake bench`): 굽기 시간(Arche 28,828장식·95,211이벤트 목표 ≤ 2초, 메인 스레드 한 프레임 ≤ 16ms로 분할 또는 작업 스레드), 메모리, 평가 시간(프레임당).
6. **지원 못 하는 장식은 이유별로 센다**(이미지 교체 SetSprite, 마스크, 히트박스, 태그가 곡 중 바뀜 등) — 그 장식은 굽지 않고 기존 경로로 둔다. 굽은 장식 비율을 로그.
7. 빌드 3종(개발 `-c Release`, `-p:Edition=Player`, `-p:Edition=Player -p:AutoTestBuild=1`) 오류 0 + LoadCheck. 새 Harmony 패치는 이름+인자 수+호출 규약으로 정확히 하나 고른다(**`Module.ResolveSignature`/`ResolveMethod(MetadataToken)` 금지** — Mono 에서 안 된다). `AccessTools.DeclaredMethod` 이름 조회는 오버로드가 있으면 예외.

## 파트 B — 렌더 경로 설계 보고서 (코드 없음, 문서만)
굽기만으로는 CPU 쪽(트윈 생성/교체/갱신)이 줄 뿐 Transform 쓰기와 SpriteRenderer 그리기는 그대로다. 사용자가 원한 완성형은 "값을 GPU 가 계산하고 CPU 쓰기까지 없애는 것"이고, 그러려면 장식을 SpriteRenderer 대신 그룹 메시로 그려야 하는데 **정렬 동률 규칙에서 막혔다**. 다음을 근거(기존 census/sortprobe 자료·코드 읽기)와 함께 답한다:
1. 정렬 규칙(sortingLayer, sortingOrder, render queue, 카메라 거리, 재질 인스턴스 ID)을 정확히 정리하고, **움직이는 장식만이 아니라 모든 이미지 장식(Arche 약 2만 8천)을 그룹 메시로 가져가면** 순서를 우리가 완전히 정의할 수 있는지(=동률 문제가 사라지는지), 그때 막히는 것(재질/블렌드/셰이더/마스크/필터 카메라/히트박스·충돌체/투명 장식의 Dormancy)을 목록으로.
2. GPU 평가 설계: 표를 StructuredBuffer/텍스처로 올리고 정점 셰이더가 곡 시간으로 평가하는 방식 vs CPU 평가(0.5~0.8ms) 후 정점 버퍼만 갱신하는 방식의 장단(Unity 6000.3 Mono, D3D11, Sprites/Tile 셰이더 호환). 어느 쪽이 프레임당 CPU ≤ 0.5ms 에 닿는지 계산으로.
3. 예상 이득: CPU 쪽(장식 갱신 2.5ms + Late 1.5ms + 이벤트 시작)이 얼마나 줄 수 있는지 `docs/optimization-36.md` 의 측정값으로 환산하고, GPU 쪽은 `docs/handoff/archegpu-evidence/arche-258-278-gpuprobe.txt`(프레임 GPU 4.24ms, 장면 약 3.4ms)와 "CPU 를 없애면 GPU 4~6ms+엔진 CPU 5ms 가 바닥(150~200 FPS)"이라는 정정(`docs/optimization-36.md` 끝)을 반영.
4. 결론: 파트 A 만 할 때와 파트 B 의 전체 경로까지 갈 때 각각 **예상 FPS·끊김 효과, 구현 기간(단계별), 위험, 실패 시 되돌리기**를 표로. 근거 없는 낙관 금지(옛 결론 "코드만으로는 이득 없음"이 왜 이번엔 다른지 구체적으로).

## 규칙
- 장식 화면 결과는 원본과 비트 동일이거나 사용자가 허락한 눈에 안 띄는 오차(1~2/255)를 자동 비교로 증명해야 채택 가능. 이번 파트 A 는 값(Transform/색) 대조까지다.
- 헤르츠 맵 이름·비공개 프로젝트 언급 금지, 100만 타일 맵 열기 금지, 다른 모드 코드 복사 금지.
- 결과 문서에 "내가 돌릴 순서"(Arche `open` → `seek 262 8` → `decobake verify`/`bench`)를 적는다. 맵 경로는 `D:\얼불춤 맵 파일\Arche\*FULL.adofai`.
