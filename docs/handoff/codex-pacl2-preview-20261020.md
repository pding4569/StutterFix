# Codex 과제 P1: PACL2 편집기 미리보기(`PACL2.Preview`, `PACL2.SequenceInfo`)가 효과를 어떻게 "한꺼번에/임의 시각에" 계산하는지 분석 (읽기 전용, 코드 복사 금지)

작업 폴더 E:\StutterFixWork\StutterFix (여기서 **파일 편집 금지**, 결과 문서 `docs/handoff/codex-pacl2-preview-20261020-result.md` 하나만 쓴다). 커밋·게임 실행·설치 금지. 사용량 절약: 필요한 클래스만.

## 배경
우리는 장식·타일 애니메이션을 "이벤트가 곡 중에 시작해 트윈을 만든다" 대신 **맵을 열 때 곡 시각의 함수(표)로 구워 한 번에 계산**하려 한다(`docs/optimization-36.md` 의 decosim 항목, 새 장식 파이프라인). 사용자가 "PACL2 에 편집기에서 이펙트를 한꺼번에 보여주는 기능이 있었는데 비슷한 것 같다"고 했다. 모드 파일: `D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\ModsOff\PACL2\PACL2.dll`(꺼진 상태). 유형 목록을 보면 `PACL2.Preview`(PreviewManager, Previewer, PreviewerMoveDecoration{Core,Image,Object,Particle,Text}, PreviewerMoveTrack, PreviewerRecolorTrack, PreviewerFilter*, PreviewerFlash, PreviewerMoveCamera, PreviewerBloom, PreviewerScreenScroll/Tile, PreviewerHallOfMirrors, PreviewerCustomBackground, PreviewerVideoBackground)와 `PACL2.SequenceInfo`(SeqMoveDecorationsInfo, SeqMoveTrackInfo, SeqRecolorTrackInfo, SeqEventInfo, SeqDurationEventInfo, SeqInstanceInfo, Const*Info …)가 있다.
도구: 저장소의 `tools/ilscan`(CLAUDE.md 사용법), 설치돼 있으면 `ilspycmd`(디컴파일: `ilspycmd -t <타입> <dll>`), 없으면 ILScan 으로 타입별 메서드·호출 목록.

## 알고 싶은 것 (우리 설계 관점)
1. **데이터 모델**: `Seq*Info`/`Const*Info` 가 무엇을 담는가(시작 시각 단위: 타일/각도/초?, 지속, 이징, 대상 태그, 시작값이 "현재값에서"인지 이벤트 인자인지, 상대 이동). 맵의 이벤트 목록을 어떻게 이 시퀀스로 바꾸는가(어느 메서드), 정렬·같은 대상 겹침(끊기/재시작) 처리 규칙.
2. **임의 시각 계산**: 미리보기가 시각 t 의 장식/타일/카메라/필터 상태를 어떻게 구하는가 — 처음부터 이어서 시뮬레이션하는가, 키프레임/캐시를 쓰는가, t 로 바로 점프하는가(그리고 되감기). 비용을 줄이려고 쓴 기법(캐시, 증분, 병렬?), 정확하지 않게 근사한 곳(문서·주석이 아니라 코드에서 보이는 것만).
3. **장식 적용 방식**: Previewer 가 계산한 값을 장식 개체에 어떻게 쓰는가(Transform/SpriteRenderer 직접? 게임 함수 호출?). 한꺼번에 많은 장식을 어떻게 처리하는가.
4. **타일(MoveTrack/RecolorTrack)**: 타일 이동·색을 어떻게 시뮬레이션하는가(우리 `FloorAnim` 처럼 직접 값을 쓰는가).
5. **함정**: 우리가 같은 접근을 할 때 조심할 의미상 함정(예: 이벤트 순서 규칙, tag 대상 확장, `repeat`, `Ease` 목록, 시각 기준이 beat/angle 인 점).
## 규칙
- 라이선스·윤리: 분석과 개념 요약만. **코드를 그대로 옮겨 적지 않는다**(결과 문서에 의사코드·구조·수식 설명은 OK, 줄 단위 복사 X). 우리 설계에 쓰라고 하는 게 아니라 "어떤 모델링이 가능한지" 참고용이다.
- 확신 없는 건 "미확인"으로, 근거(타입·메서드 이름)를 같이 적는다.
## 결과 문서
위 1~5 순서로 요약 + "우리 파이프라인(맵 열 때 표 굽기, 값 대조)에 주는 시사점" 한 단락 + 더 읽어야 할 곳 목록.
