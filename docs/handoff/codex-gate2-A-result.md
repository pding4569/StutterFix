# Gate 2 A 결과 — 2026-10-21

변형 A의 개발 전용·기본 꺼짐 프로토타입과 자동 검증 생성기를 구현했다. **실게임 Gate 2는 미측정이며 PASS를 선언하지 않는다.** 게임 실행·설치·커밋·푸시는 하지 않았다. 기준 HEAD는 `5bf7413`이다. 작업 중 공통 인계에 추가된 논리 필드 전용 갱신 지시도 반영했다. 게이트 3나 워커 분할은 만들지 않았다.

## 구현

- `livedeco on|off|state`는 `LiveDecoRenderer.cs`를 호출한다. 기존 `census`와 Gate 1 정책은 유지했다. 다른 소유 렌더러·장식 엔진과의 동시 실행, SRP, 원근/비-Z 정렬, 비항등 관리자 부모는 거부한다. 시작 로그에 `currentRenderPipeline=null`을 남긴다.
- 모든 live 렌더러/재질 슬롯을 장벽으로 포함한다. 정렬된 행에서 소유 장식이 연속이고 정렬 버킷·Unity layer·재질 서명·실제 텍스처가 같을 때만 같은 구간이다. 떨어진 같은 텍스처는 합치지 않는다. **재질 동률은 소유끼리라도 검증되지 않은 순서를 만들지 않고 제외한다.** 첫 멤버의 실제 재질 인스턴스, sortingLayerID, sortingOrder, Z를 빌린 씬 MeshRenderer를 쓴다. 재질·텍스처 복제와 UV 재배치는 없다.
- 디컴파일 `scrDecoration.UpdatePosition/SetPosition/SetRotation/SetScale/camScaleMultiplier`, `scrParallax.SetTrans`와 설치 DLL의 IL을 대조했다. 논리 위치·피벗/크롭 오프셋·PPU가 반영된 실제 sprite 정점·child 회전/스케일·시차·카메라 회전/배율 잠금·화면 고정·축 고정·flip을 계산한다. 바닥 부착·행성 따라가기·비평면 자식·반복/타일·비쿼드·특수 블렌드·마스크·히트박스·활성 종속 렌더러/외부 자식 스크립트는 제외한다.
- 소유 진입 전 원본을 갱신한 뒤 계산 정점 4개와 `TransformPoint` 정점 및 SpriteRenderer.bounds의 min/max를 비교한다. `[livedeco] vertexcheck max_err=... count=... excluded=... threshold=0.0001`을 남기고, 비유한 값이나 1e-4 초과는 제외한다. 이 검사는 진입 시 검사이며 모든 미래 프레임의 동일성 증명이 아니다.
- `TightLate.Process`의 직통 Transform 경로와 Harmony `UpdatePosition`을 모두 막는다. `SetPosition`은 논리 위치/피벗과 screen clamp 필드만, 회전/크기는 논리 필드만 갱신한다. 색/불투명도 setter·ApplyColor·`ColorFast.TrySet`도 소유 중 엔진 색 쓰기를 생략하고 `rendererColor`까지 갱신한다. `DecoAnim.Apply/OnUpdate/OnComplete`의 다음 효과 시작값을 유지한다. 알파가 0 이하로 넘어가면 원본에 돌려 숨김/깨우기 bookkeeping을 처리한다.
- 색은 `color.WithAlpha(color.a * opacity * 1f)`의 기존 순서로 보존하고 Color32 정점에 넣는다. 원래 `Sprites/Tile` 셰이더를 그대로 빌려 텍스처 샘플·색 곱·프리멀티플라이를 셰이더에서 한 번 수행한다. 반복은 1인 경우만 소유한다. `Sprites/Default` 등 센서스 기본형 밖 셰이더는 제외한다. Color32 양자화, Quaternion 정규화 차이, SpriteRenderer와 메시의 내부 처리 차이는 실픽셀 검증 대상이다.
- 위치/Color32/UV를 인터리브한 공용 상주 구조체 배열 하나에 모든 구간을 채운 뒤 구간별 source offset으로 `SetVertexBufferData` 및 지정된 3개 플래그를 쓴다. 인덱스는 원본 sprite 인덱스와 구간 순서를 유지한다. 큰 고정 bounds를 쓰고 범위 밖 정점은 오류로 중단한다. 같은 Z, 활성 상태, forceRenderingOff를 매 프레임 다시 쓰지 않는다.
- 행의 진입/이탈/키 변경은 이진 삽입/제거, 구간은 기존 첫 멤버·멤버 버전 대조로 **바뀐 메시만 교체**한다. `transition`에 생성/재사용/폐기 수와 시간을 기록한다. 순서 장벽을 추적하는 캐시된 전체 렌더러 스캔과 구간 설명의 선형 순회는 남는다. 장식 Setup/경계 변경 시 레지스트리를 보충한다. 임의 외부 코드가 별도로 생성하는 비장식 렌더러를 전부 실시간 발견한다는 보장은 없다.
- `cost`는 최대 120프레임 묶음의 **프레임당 평균** 정점 계산/업로드/재구성/스캔 ms, upload_calls를 분리한다. 약 1,200구간이라는 예상치를 실측으로 쓰지 않는다. 메인 스레드 allocated_bytes, steady_allocated_bytes/steady_samples, 힙 증감을 남긴다. 비용·정점 로그 자체의 할당은 카운터 밖이고 전환 할당은 따로 포함된다. **실게임 GC=0은 아직 확인하지 못했다.**
- off, AutoTest 종료/오류, SceneReset, Main.Unload, 루트 파괴에서 원본 forceRenderingOff·현재 논리 상태의 child Transform/위치/색을 복원하고 패치·메시·오브젝트를 정리한다. 빌린 재질/텍스처는 파괴하지 않는다. 복원 오류는 최종 ERROR에 반영한다. 실제 재시작/사망/맵 재열기/언로드의 누수 검사는 미실행이다.

## Transform 소비자 감사

게임 DLL SHA256은 `F692210F9A342F7EED4BE40AAD463286AB40BF046599959823942EA02F4CA3FF`이다. [해시](gate2-A-game-hash.txt), [장식 IL](gate2-A-decoration-il.log), [시차 IL](gate2-A-parallax-il.log), [읽기 IL](gate2-A-readers-il.log), [쓰기 IL](gate2-A-guards-il.log)을 남겼다. IL 도구는 메타데이터만 읽었고 게임 메서드를 실행하지 않았다.

직접 Transform 필드/크기/시차 위치 관련 읽기 참조 16행을 수집했다. `GetDecorationWorldSize` 호출자는 gizmo LateUpdate/Drag 2곳, `GetPositionWithParallax` 호출자는 gizmo LateUpdate/DragStart/Drag 3곳이다. gizmo LateUpdate는 `isEditingLevel`에서만 실행한다. 활성 선택/히트박스 테두리, 히트박스, 마스크, 바닥/부모 의존 장식은 소유에서 제외한다. `SpriteAlphaMask.maskSize`와 `SpriteAlphaMaskUtils.ApplyAlphaMaskToRT`/closure는 마스크 Transform을 읽으므로 마스크 및 마스킹된 이미지를 제외한다. 관리자 테두리/히트박스 크기 계산도 디컴파일에서 확인했다.

`ffxMoveDecorationsPlus`의 시작값은 pivotPosVec, pivotOffsetVec, rotAngle, scaleVec, color, opacity 등 논리 상태다. 해당 필드는 계속 갱신한다. 텍스처 전체 크기를 읽는 함수는 텍스처를 바꾸지 않아 유지된다. 이 감사는 설치 게임 관리 코드의 직접 참조를 조사한 것이며 네이티브 내부·반사·임의 외부 모드의 alias 소비자 전체를 증명하지 않는다.

## ProfApply / ProfSet와 자동 검증

**설정 비중은 미측정이다.** 현재 저장 근거에서 새 구간의 ProfApply/ProfSet 수치가 없어 Gemini의 “60% 이상”을 결과로 쓰지 않았다. 생성기의 첫 별도 재생에서 262→276초 `livedeco2 profilebegin/profileend`로 누적 카운터 차이를 구한다. `[livedeco2] PROFILE key=... apply_us=... set_us=...` 및 표본 가중 `set_pct`를 출력한다. 이 카운터는 Step의 Apply/OnUpdate 표본이며 StepPair·OnComplete·setter 이후 엔진 처리 전체를 포함하지 않는다. Prof가 꺼져 있으면 samples=0/valid=False로 표시한다. 프로파일 재생은 픽셀/FPS 측정 창 밖이다.

```powershell
py -3 tools/optimization/livedeco2-steps.py --chart '<Arche FULL.adofai 절대 경로>' --output x.json
```

이 명령은 JSON만 만든다. 생성기 [livedeco2-steps.py](../../tools/optimization/livedeco2-steps.py)는 총 129단계다. 262/270/276초 각각 독립 seek → pause → 잡음 바닥 → A → on → B → off → A2를 캡처한다. 중앙 가로/세로 각 20% 제외 영역 밖 픽셀 수·최대 채널 차이·상위 영역 좌표·복원·상태 안정성을 `PIXEL`에 남긴다. 1/255 차이도 PIXEL_NEAR로만 보고한다.

그 뒤 같은 게임에서 네 번 독립 seek, 252초 모드 설정, 262초 측정 시작, 276초 측정 종료로 **off/on/on/off** 순서를 지킨다. pause 없이 fuse begin/end와 FPS·ms·실제 시작/끝 곡 시각·프레임 수를 기록한다. 두 on FPS 평균이 두 off 평균보다 15% 이상이고 세 픽셀 표본이 정확히 0일 때만 PASS다. 누락/중복/순서 오류·불안정한 기준/복원 실패·창 중 렌더러 중단은 ERROR다. 정상 결과 및 AutoTest 오류 종료 모두 끔으로 끝난다.

| 실게임 항목 | 결과 |
|---|---|
| vertexcheck 최대 오차·소유 수·구간 수 | 미측정 |
| 262/270/276초 ab_out / aa2_out / maxdiff | 미측정 |
| ABBA 네 창 FPS / 향상률 | 미측정 |
| 정점/업로드/구간 재구성/스캔 ms 및 GC | 미측정 |
| ProfApply/ProfSet 설정 비중 | 미측정 |
| Gate 2 | 미측정, PASS 아님 |

## 오프라인 검증

| 검사 | 결과 | 근거 |
|---|---|---|
| Release DEV / Player / PlayerAuto 빌드 | 오류 0 / 0 / 0 | gate2-A-build-dev.log / gate2-A-build-player.log / gate2-A-build-auto.log |
| LoadCheck DEV / Player / PlayerAuto | 276 / 229 / 276곳, 틀림 0 / 0 / 0 | gate2-A-loadcheck-*.log |
| LiveDeco 구간·첫 멤버 앵커 모델 | 106,460 checks 통과 | gate2-A-fixture.log |
| Gate 2 판정 경계 fixture | 18 checks 통과 | 같은 로그 |
| edition 메타데이터 | 22 checks 통과, 일반 Player 실험 타입/참조 없음 | 같은 로그 |
| 설치 게임 패치 대상 유일성 | 17개 통과 | 같은 로그 |
| MatShare 기존 fixture | 25,401 checks, errors=0 | gate2-A-matshare-fixture.log |
| FrameCompare 기존 fixture | 11+15+14+17 checks 통과 | gate2-A-framecmp-fixture.log |
| Gate 1 기존 steps / 새 Gate 2 steps | 25단계 / 129단계, UTF-8 CLI 포함 통과 | gate2-A-gate1-steps-fixture.log / gate2-A-steps-fixture.log |

LoadCheck의 동적 이름 5곳과 Harmony의 실제 Mono 패치 실행은 런타임 확인 대상이다. 모델 fixture·메타데이터 검사로 Unity 렌더링·부동소수점·픽셀·성능·누수 검증을 대체하지 않는다. 기존 deprecated/미사용/어셈블리 참조 경고는 남는다. `.codex/config.toml`은 수정하지 않았다. 공통 인계 파일에 외부에서 추가된 지시는 보존했다.

## 작업 관찰 반환

task-observer v3.5.0의 사용자 범위 헤더와 원칙을 읽었다. 저장소의 스킬·메모리는 수정하지 않고 여기로 반환한다. 문제: 일반 setter Harmony 패치만으로는 TightLate/ColorFast의 직통 엔진 쓰기가 남는다. 개선: 논리 setter와 직통 경로를 함께 추적하고 렌더러 소유권으로 같은 차단 조건을 적용한다. 원칙: 우회 경로를 포함해 엔진 쓰기의 실제 종착점을 감사한다. steps CLI의 임시 파일 검증은 제한된 외부 Temp 대신 워크스페이스의 고유 파일로 실행하고 즉시 지운다.
