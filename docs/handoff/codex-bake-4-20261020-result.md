# bake-4 결과: 정적 Tile 초기 배치 지원

2026-10-20. 작업 폴더 `E:\StutterFixWork\wt-bake`, 브랜치 `codex/night-bake`, 시작 HEAD `20b28f1`. 게임 실행·설치·커밋은 하지 않았다. 그림자 모드와 기본 꺼짐을 유지하며 기존 애니메이션·렌더 경로를 변경하지 않았다. 새 Harmony 패치와 `Module.ResolveSignature` 호출은 없다.

**파일상 조건부 후보는 bake-3 5개(0.0173%) → bake-4 23,527개(81.5918%), 순증 23,522개다.** 정적 Tile 초기 배치를 지원했다. Camera/CameraAspect는 초기 좌표가 고정돼도 화면 투영이 동적이므로 제외했다. 실제 런타임 지원률·성능·최종 화면 오차는 미측정이다.

## 배치 의미와 판단

설치된 DLL을 ILSpy로 정적 역컴파일했다. `Assembly-CSharp.dll` SHA256은 `F692210F9A342F7EED4BE40AAD463286AB40BF046599959823942EA02F4CA3FF`로 bake-3 근거와 같다. 게임을 실행하지 않았다. 원문은 [scrDecoration](bake-evidence/bake4-decoration.cs.txt), [scrParallax](bake-evidence/bake4-parallax.cs.txt), [StartEffect/Decode](bake-evidence/bake4-move.cs.txt)에 보관했다.

| 확인 항목 | 코드 의미와 결론 |
|---|---|
| Tile의 초기 `startPos` | `SetPlacementType` 521행부터: 원본 position × tileSize에 원본 floor를 clamp한 타일의 **그 호출 시점** transform.position을 더한다. Setup이 이를 호출하고 마지막에 `SetPosition(startPos, pivotOffset)`을 한다. 평소 `UpdatePosition`은 Tile이라는 이유로 타일 위치를 다시 읽지 않는다. |
| 타일 이동 추종 | `UpdatePosition` 403행부터: `stickToFloor`일 때만 `parentFloor.transform.position - startPos`를 더한다. 기본 Tile은 타일 이동만으로 초기 원점을 갱신하지 않는다. `stickToFloor`, 행성 추종, 회전/크기 고정은 계속 장식 전체 제외다. |
| 이동 효과 기준 | `StartEffect` 139행부터: 사용 중인 relativeTo가 LastPosition 이외이면 **위치 사용 여부·forceDontTweenMovement 검사보다 먼저** `SetPlacementType`을 호출한다. LastPosition 목표는 Kill 전 pivotPosVec + targetPos, 나머지는 당시 startPos + targetPos다. LastPosition은 기준값을 재설정하지 않는다. |
| 명시적 Global | 초기 Tile의 Setup 원점과 다르다. 원본 position × tileSize로 startPos를 다시 설정한다. 이후 relativeTo가 disabled면 그 원점이 유지된다. 따라서 모든 효과에 Setup 원점을 고정하는 구현은 틀리며, 이번에 Global 원점 전환을 별도로 반영했다. |
| 효과의 Tile | 다시 `SetPlacementType(Tile)`을 호출하므로 움직인 타일의 효과 시점 위치를 재독한다. 해당 대상 전체를 `Tile-동적 효과 시점 재배치`로 제외한다. 타일이 우연히 안 움직이는 경우를 증명해 허용하는 분석은 이번 범위 밖이다. |
| Camera/CameraAspect | SetPlacementType의 초기 값은 `(원본 position × tileSize) / tileSize`다. `UpdateScreenClamp`는 CameraAspect X에 Screen.height/Screen.width를 곱하고, `(pivotPosVec + pivotOffsetVec) / 20 + 0.5`를 화면 비율 위치로 만든다. `SetTrans`는 pixelRect와 `ScreenToWorldPoint`를 매번 읽는다. 카메라 이동·회전·줌·화면 비율 변화에 종속되므로 초기 스냅샷만으로 정적인 월드 위치를 보장할 수 없다. |
| 시차 | 비clamp 경로는 `(cameraPosition - posCamAtStart) * multiplier + startPosition`이다. UpdatePosition은 startPosition/posCamAtStart에 현재 pivot 기반 좌표를 넣는다. Tile/Global도 시차 100(배율1)이면 카메라를 따라갈 수 있다. Camera clamp 경로는 이 배율 식 대신 화면 투영을 사용한다. |

[framegen-research.md 22장](../framegen-research.md)은 실제 Tile/시차1 장식이 카메라 위치를 따라갔던 기존 관측을 기록한다. 이번 판단은 위 DLL 코드와 함께 확인했다. **굽기의 위치 채널은 pivotPosVec이며 최종 월드 Transform이나 렌더 위치가 아니다.** 정적 Tile의 기준 좌표를 지원한다고 시차까지 정적인 화면이라고 주장하지 않는다. 기존 parallax 채널과 원래 화면 합성은 유지한다. 시차 부품 없음/축 고정/화면 고정은 기존처럼 제외한다.

Camera의 정적 지원 갈래는 확인되지 않았다. fixture에는 고정된 Camera 원시 좌표라도 줌 변화로 월드 투영이 달라지는 예와 Camera/CameraAspect 전체 제외를 넣었다. Camera 지원 성공 사례로 바꾸어 기록하지 않았다.

## 구현과 검사

- `DecoBake.cs`: Setup 직후 실제 startPos/pivotPosVec를 그대로 수집한다. 원본 sourceLevelEvent.position × tileSize도 읽어 Global 기준값으로 보관한다. 원본 위치 없음·원점 비유한은 제외한다. 초기 정적 Tile을 허용하고 동적 배치 사유를 구분한다. 기존 Setup/PrepVfx 후크와 FastMove 시작 관찰만 사용한다.
- `DecoBake.Core.cs`: `ResetGlobalOrigin`이 설정된 이벤트부터 GlobalX/Y를 절대 위치 목표 기준으로 쓴다. 위치 채널이 없는 효과도 기준값을 바꾸며, 생략된 relativeTo는 현재 기준을 유지한다. LastPosition 목표 캡처/Kill 완료/NaN 축/동일 시각 순서/되감기 계약은 유지한다. payload 추정에 원점 float 두 개를 반영했다.
- `decobake verify`: `Tile-동적 바닥 추종`, `Tile-동적 효과 시점 재배치`, `Camera-동적 화면 투영`, `Camera-동적 배치 변경`, `초기 배치 미지원`을 0건도 출력한다. 사유는 해당 장식 전체에 전파되고 다른 채널도 만들지 않는다. 기존 지원 수·중복 가능한 사유별 통계와 크기/위치 대조는 유지한다.
- production Core를 직접 링크하는 fixture에 Tile 원점/Global 전환/위치 없는 기준 전환/생략/LastPosition/되감기와 초기·후속 동적 갈래 전체 제외를 추가했다. 추정 도구에는 MoveTrack·MoveCamera가 있어도 정적 Tile을 제외하지 않는 예와 동적 Tile/Camera 예를 추가했다.

| 오프라인 검사 | 결과 | 근거 |
|---|---|---|
| Release 개발 빌드 | 오류 0, 경고 38 | [개발](bake-evidence/bake4-build-dev.txt) |
| Release `-p:Edition=Player` | 오류 0, 경고 4 | [Player](bake-evidence/bake4-build-player.txt) |
| Release `-p:Edition=Player -p:AutoTestBuild=1` | 오류 0, 경고 38 | [AutoTest](bake-evidence/bake4-build-playerauto.txt) |
| LoadCheck 개발/Player/AutoTest | FieldRefAccess 266/229/266, 틀림 모두 0, 동적 이름 각각 5곳 | [개발](bake-evidence/bake4-loadcheck-dev.txt), [Player](bake-evidence/bake4-loadcheck-player.txt), [AutoTest](bake-evidence/bake4-loadcheck-playerauto.txt) |
| 타입/후크 | 일반 Player 굽기 타입 제외, 개발/AutoTest 포함. Setup/PrepVfx 정확일치 각1 | 같은 LoadCheck 로그 |
| 계약 fixture | 새 배치 검사 통과, 기존 위치/크기·교체·취소 검사 통과. 이징 위치 3,465개·크기 3,600개 | [fixture](bake-evidence/bake4-contract-check.txt) |
| 추정 도구 self-test | 문자열 보존·태그 OR·나중 제외·배치 검사 통과 | [self-test](bake-evidence/bake4-estimator-selftest.txt) |

## 새 오프라인 지원률 표

[갱신 도구](../../tools/optimization/bake-support-estimate.py), [전체 출력/미정 표](bake-evidence/bake4-support-estimate.md). bake-3와 같은 단일 Arche 파일만 읽었다. SHA256 `026873b89f05ea94b86b83634a938ac053593ebea6cb832d8134e4a8084f8064`는 재집계 후에도 같다. 원본 문법은 메모리에서만 후행 쉼표 102,058곳/구분 쉼표 누락1곳을 보정했다.

분모는 소스 장식 행 28,835개(AddDecoration 28,828 + AddText 7), MoveDecorations 95,211개, 대상 없는 원본 이벤트 0개다. 태그 OR 합집합/Distinct, 소스 행 구분, 반복의 고유 대상 수만 집계하는 bake-3 기준을 유지한다. 게임의 실제 생성 인스턴스 수와 다르다.

| 사유·구분(사유 간 중복 가능) | bake-3 | bake-4 |
|---|---:|---:|
| 초기 Global 이외 배치 일괄 제외 | 28,830 (99.9827%) | 폐지 |
| Camera 동적 화면 투영 | 기존 일괄 사유에 포함 | 114 (0.3954%) |
| Tile 동적 바닥 추종/효과 시점 재배치 | 기존 사유에 포함 | 각 0 |
| Camera 동적 배치 변경/기타 초기 배치 | 기존 사유에 포함 | 각 0 |
| 이미지/원래크기/크롭/wrap | 5,087 (17.6418%) | 5,087 (17.6418%) |
| 가시성/깊이 변경 | 22 (0.0763%) | 22 (0.0763%) |
| 이미지 이외 장식 | 7 (0.0243%) | 7 (0.0243%) |
| 기타 파일상 제외 사유 | 각 0 | 각 0 |
| **파일상 제외 없음·추가 미정 없음(조건부 후보)** | **5 (0.0173%)** | **23,527 (81.5918%)** |
| 파일상 제외 있음 | 28,830 (99.9827%) | 5,204 (18.0475%) |
| components 추가 미정(위 제외와 중복) | 228 (0.7907%) | 228 (0.7907%) |
| 최종 런타임 판정 미정 | 28,835 (100%) | 28,835 (100%) |

제외 5,204개와 후보 23,527개 사이의 나머지 104개는 파일상 제외가 없어도 components 추가 미정인 행이다. 지원으로 세지 않았다. components, 외부 모드 직접 쓰기/패치, 시차 부품/축 고정, 이미지 로딩/실제 초기값, disabled 기본값/버전 변환, High·고정 시계·조건 연결 등은 런타임에서 확인해야 한다. 조건부 실제 지원률 범위는 **0~81.5918%**이며 81.5918%를 실게임 확정 지원으로 해석하지 않는다.

### 다음 확장 상위 5개

앞 순위 완화를 누적할 때 늘어나는 파일상 후보 수로 정렬했다. components 미정은 제외 사유와 구분되는 연구 항목이며, 여기에서만 이를 해소하는 반사실을 계산한다.

| 순위 | 해당 행 | 단독 해제 순증 | 누적 시 추가 후보 | 코드 근거·필요한 다음 작업 |
|---|---:|---:|---:|---|
| 1. 이미지/원래크기/크롭/wrap | 5,087 | 5,053 | 5,053 | Reasons의 imageFilenameUsed/originalSizeUsed/smoothingUsed/cropPivotOffsetUsed. 이 파일은 이미지 이벤트가 원인. SetSprite·textureScaleMultiplier·croppedPivotOffset·비동기 로딩의 채널 영향부터 확인 |
| 2. components 미정 | 228 | 104 | 112 | Setup의 AddComponent/FieldInfo.SetValue 경로. 실제 타입/필드별 작성자와 실행 시점 확인·허용 목록 필요 |
| 3. Camera/CameraAspect | 114 | 4 | 114 | UpdateScreenClamp/SetTrans의 화면 투영. 카메라/비율을 입력으로 받는 별도 평가 계약 필요. 초기 스냅샷 허용만으로 해결 불가 |
| 4. 가시성/깊이 | 22 | 8 | 22 | Reasons visibleUsed/depthUsed, SetVisible/SetDepth. 이 파일은 depth가 원인. 렌더 정렬·숨김 조건 채널 의미 확인 |
| 5. 이미지 이외 장식 | 7 | 7 | 7 | SetupPostfix scrVisualDecoration 제한. AddText setter·폰트/크기/피벗 계약 확인 |

위 다섯 반사실을 모두 합치면 소스 행 전부가 파일상 후보가 되지만 런타임 미정은 남는다. 이번에는 이 다섯 제한을 추가로 해제하지 않았다.

## 내가 돌릴 순서

**아래는 이번에 실행하지 않은 향후 실게임 확인 순서다.** bake-3 순서를 유지한다. 기존 기본 DecoAnim 경로·High에서 open 전에 on, play가 PrepVfx를 재구성할 수 있으므로 재생 후 ready를 기다린다.

```text
decobake on
open D:\얼불춤 맵 파일\Arche\[𝐃𝐎 𝐍𝐎𝐓 𝐎𝐏𝐄𝐍 𝐓𝐇𝐈𝐒 𝐀𝐓 𝐇𝐎𝐌𝐄] FULL.adofai
select 0
play
wait 1
decobake ready
decosim capture 12
wait 13
stop
decobake verify
decobake bench
decobake off
```

먼저 ready 준비 로그의 실제 장식/효과/**지원 수**와 verify의 사유별 장식 수를 확인한다. 파일상 후보 23,527개는 런타임 목표값이 아니므로 차이가 나면 components/실제 스키마·초기 상태 등 미정 조건과 대조한다. Tile 동적 두 사유와 Camera 동적 사유도 확인한다. 표본 n=0은 오차0으로 해석하지 않는다. 짧은 구간에 교체/크기 효과가 없으면 필요한 구간을 별도로 캡처한다.

기존 위치/색/회전/불투명도/시차 절대 오차·시작 위상 분포·교체 장식 오차, 크기 X/Y 절대/상대 n·최대·p99·상위8·실제0 상대NA·명목축길이px/환산불가를 확인한다. pivotPosVec/명목 크기 오차는 최종 시차·피벗·투영 합성 렌더 오차가 아니다. bench는 지원 수와 함께 굽기 시간/수집 CPU/최대 메인 slice/완료 wall 시간/메모리/평가 중앙·p99를 확인한다. 메인16ms·로딩2초·최종화면0.01px 승인 자료는 아직 없다.
