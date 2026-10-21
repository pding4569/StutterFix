# StutterFix 극한 최적화 로드맵 (2026-10-21 기준, 이 문서 하나로 이어 갈 수 있게)

## 1. 확정된 측정 (Arche `*FULL.adofai`, 262→276초, 2560×1440 120Hz, 판마다 새로 켠 ABBA)
- 원본 약 **125 FPS(프레임 8.0ms)**, 장식은 두되 정지(이동 이벤트 제거) 약 **441 FPS(2.27ms)**, 장식 0개 약 **546 FPS(1.82ms)**.
- 프레임 구성: 바닥(타일·필터·엔진) 1.82ms + 장식 존재(28,828개 오브젝트) 0.45ms + **활성 장식(켜져 보이고 움직이는 약 2,000개)과 처리 5.7ms(71%)**. 모드가 재는 부분은 갱신 1.9 + 관리자 LateUpdate 1.16 + 시작 0.13, 나머지 ≈2.5ms 는 활성 렌더러의 엔진 비용.
- GPU(본 카메라) 5.73ms 중 이미지 장식 2.4~2.9ms; 원인은 **draw call 약 1,840개**(삼각형 3,760개뿐). 장식마다 재질이 복제돼서(`Renderer.material` 호출처: `scrVisualDecoration` SetSprite·SetTile·SetBlendMode·UpdateShader, `scrDecorationManager.UpdateDecorationTilingLateUpdate`).
- 정렬 규칙: 같은 (sortingLayer, sortingOrder, 큐, 거리)에서는 **재질 인스턴스 ID 부호 있는 오름차순이 뒤→앞**. `rendererPriority` 는 무관(150회 100% 재질 ID). 재질 내용 서명은 18종, 99.4% 동일. 텍스처 301종이 순서상 엇갈려 구간(Run)이 live 2,126개 → 1,227개(−42%)뿐 → 재질 공유만으로는 draw 약 −40%, 텍스처 합치기(아틀라스/배열)가 필요.
- 굽기: 지원 23,631/28,835(81.95%), 시간 오차는 곡 시계(`songposition_minusi`) 지터가 원인 → 매끄러운 단조 시계 `tb`(p99 6.8~12.7ms); 앵커 구동(bake-9) 구현됨. **CPU 표 구동(채택) 모드는 +0.45ms 손해**(쓰기 비용이 그대로라서). 타일 굽기 0%, T3 타일 건너뛰기 접음.

## 2. 결론: 한 세트가 필요하다
(굽기로 애니메이션을 데이터화) + (활성 장식을 Unity 오브젝트 없이 그리는 렌더러: 텍스처 아틀라스/배열 + 우리가 정한 그리기 순서 + 인스턴싱/메시 소수 draw + GPU/잡 애니메이션). 예상 FPS: 2단계 약 180, 3단계 약 400(이상적), 현실 200~300. 가장 큰 위험: 겹침 순서와 픽셀 동일성.

## 3. 단계 (각 단계는 Codex 작업 + 내가 `run_json.py` 로 돌림)
1. **framecmp — 완료**(`codex/night-framecmp`): 실제 카메라 체인 최종 이미지 같은 프레임 비교, pause/resume, 잡음 바닥·양성/음성 대조군. VERDICT 전부 PASS(중앙 20% 제외 영역).
2. **LiveDeco 정지 렌더러**: 켜진 장식을 비트 정확 아틀라스(압축 블록 `CopyTexture` 정렬 복사 또는 셰이더 수동 클램프·이중선형)로 묶고, 정렬 순서대로 소수 메시/인스턴싱 draw, 원본 렌더러는 `forceRenderingOff`. framecmp 로 화면 동일 증명 → 프레임 시간 측정.
3. **GPU/잡 애니메이션**: 굽기 표(앵커 시계 `startTau`)를 셰이더/Burst 가 평가, Unity 오브젝트 쓰기 제거.
4. 수명·호환: 장식이 켜지고 꺼지는 전환, 히트박스·마스크·블렌드·시차·태그, 에디터, 다른 모드.
5. 릴리스: 기본 꺼짐 → 실측 확인 뒤 사용자 승인으로 켬.

## 4. 도구와 명령 (E:\StutterFixWork)
- `run_json.py <steps.json> <워크트리> <이름> [--probe] [--keys a,b] [--tail N]` : 범용 실행기(정식 DLL 자동 복원). Codex 가 `tools/optimization/*-steps.py --chart <Arche FULL> --output x.json` 형태로 steps 를 만든다.
- `mk_nodeco.py`/`mk_staticdeco.py`: 장식 제거 맵 사본(같은 폴더 임시 파일, 시험 뒤 삭제; 원본 유지).
- 워크트리/브랜치: `wt-bake`(`codex/night-bake`, DecoBake·adopt), `wt-tilebake`, `wt-archegpu`(`codex/night-archegpu`: archegpu/gpusplit/matshare), `wt-decorender2`(SortProbe), `wt-framecmp`(`codex/night-framecmp`).
- 결과 근거: `docs/optimization-36.md` 끝 절들, `docs/handoff/night-results-20261021/`, `docs/handoff/codex-*-result.md`.

## 5. 지켜야 할 규칙
기본 꺼짐·Player 제외(개발 전용)로 시작, 화면 동일 증명 전 릴리스 금지, 릴리스는 사용자 "올려" 뒤에만, `Module.ResolveSignature` 금지(Mono), 새 Harmony 패치는 이름+인자 수+호출 규약으로 하나, 헤르츠 맵 이름·비공개 프로젝트 언급 금지, 100만 타일 맵 열기 금지, 시험 뒤 정식 DLL 복원.
