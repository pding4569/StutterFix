# Silentroom 큰 타일 투명화 오프라인 조사 결과 — 2026-11-15

## 결론과 범위

**원인 모듈과 수정 성공은 아직 확정하지 않았다.** 원본과 달라지는 제품 결함을 입증하지 못해 이번 조사에서 제품 동작이나 기본값을 바꾸지 않았다. 기존 FloorAnim → TileCull 알림 수정은 유지했다. 인계문의 TileCull culltest 차이 0 결론도 뒤집지 않는다.

대신 원인 후보를 상당히 좁혔다. **큰 타일 330~564는 준비 시 크기 150%, opacity 100%, 등장 애니메이션 None이다. 약 1.837초에 330~565를 opacity 0으로 만드는 MoveTrack이 맵에 명시돼 있다.** 따라서 이 시점 이후 사라진다는 사실만으로 모드 버그라고 판단할 수 없다. 그 이전에 사라지는지, 원본에서 더 오래 보이는지, 이후 복원이 달라지는지를 대조해야 한다.

게임 실행·설치 DLL 교체·커밋은 수행하지 않았다. 읽기 전용 `floorstate` 진단, 오프라인 효과 목록, 처음부터 재생하는 기능별 대조 JSON 14개를 준비했다. Dev/Player/PlayerAuto/native 4종 빌드 오류 0. 실제 게임 픽셀·재현·수정 성공은 **미검증**이다.

작업 트리: `E:\StutterFixWork\wt-v14prod`, 브랜치 `claude/adofai-renderer`. 작업 중 외부에서 HEAD가 `85cb38f`로 이동했고 기존 DR2·FloorAnim 변경이 커밋된 것을 관찰했다. 이 조사에서는 커밋 명령을 실행하지 않았다. 다른 작업의 변경은 되돌리지 않았다.

## 산출물과 재현

- [맵 분석기](../../tools/analysis/silentroom-tiles.py): 게임·런처를 불러오지 않고 JSON만 읽는다.
- [전체 목록 JSON](../../out/silentroom-tiles/timeline.json): 원본 이벤트 index, 반복 번호, 시각, 대상 범위, 사용한 값, 준비 이벤트, 자동 등장/퇴장, 모든 타일 초기 scale/opacity.
- [직접 실행 효과 TSV](../../out/silentroom-tiles/timeline.tsv), [요약](../../out/silentroom-tiles/summary.json).
- [대조 계획 생성기](../../tools/analysis/prepare-silentroom-tiles.py), [준비된 계획 폴더](../../out/silentroom-tiles/plans).
- [검증 결과](../../out/silentroom-tiles/verification.json), [증거 일관성 검사기](../../tools/analysis/check-silentroom-tiles.py).
- 설치 게임 DLL의 [MoveFloor IL 호출/필드](../../out/silentroom-tiles/game-move-il.txt), [Scrub IL 호출/필드](../../out/silentroom-tiles/game-scrub-il.txt).

```powershell
py -3 tools/analysis/silentroom-tiles.py
py -3 tools/analysis/prepare-silentroom-tiles.py
py -3 tools/analysis/check-silentroom-tiles.py
```

맵 SHA256: `40f049ebb8df76657a2587d9b144b234317ad1d7a2560069c42a90b54fc136b3`.
설치 `Assembly-CSharp.dll` SHA256: `EC0D7C3889696C5605D6DB4F443FD387F8668A90BA7B4DD2A1A2DA64B5ADA5BC`.
angleData 8,052개 + 시작 타일 = **8,053타일**. actions 18,936개.

## 시각 계산과 실행 순서

시각은 `waitsongpos`가 읽는 **songposition_minusi** 좌표다. 100% pitch, countdownTicks 4, countdownSpeedMultiplier 1을 전제로 원본의 angleData 생성·Twirl·SetSpeed·Pause·entryTime·angleOffset 공식을 따라 계산했다. 이 맵에는 MultiPlanet/FreeRoam/Hold와 비영 angleOffset SetSpeed가 없음을 검사한다. 반복 이벤트는 같은 floor의 tag lookup을 적용했다. 초반 목록에는 수동 tag 효과나 반복된 타일 효과가 없다.

이 값은 **예약 시각의 오프라인 추정**이며, 실제 실행 프레임·스킵·자동 플레이 시작 지연·EffectBudget 지연은 실측하지 않았다. 별도 카운트다운 좌표와 음악 파일 좌표에는 약 0.870초 차이가 있다. AUTOTEST `auto on`은 fastTakeoff로 초기 카운트다운을 건너뛸 수 있으므로 첫 `waitsongpos 0.5`가 정확히 0.5초 프레임을 잡는다고 가정하지 않는다. `floorstate`의 실제 song_s/frame을 함께 확인한다.

원본 `scnGame.cs:1823-1842`는 먼저 `ScrubToTime(0)`을 하고 **그 뒤** startTime/floor 순으로 정렬한다. 처음 스크럽은 floor별 plusEffects의 구성 순서를 따른다. 음수 angleOffset이라고 무조건 이 스크럽에 들어가는 것이 아니다.

| 항목 | 곡 시각 추정 | 의미 |
|---|---:|---|
| floor 1 진입 | 0.869565 | 이후 Pause 19박이 반영됨 |
| floor 70 진입 | 13.913043 | PositionTrack은 이때 실행하는 효과가 아니라 미리 적용되는 준비 설정 |
| floor 330 진입 | 31.304348 | 큰 타일들의 원래 진행 위치 |
| floor 565 진입 | 40.000000 | scale 0/100 쌍이 붙은 뒤쪽 구간 |
| floor 70, angleOffset -9999 | **1.836957** | 타일 330~565 opacity 0 |
| floor 565, angleOffset -9999 | 약 33.962 | 초기 0~15초에 해당하지 않음(속도 2배) |

### 직접 실행되는 타일 효과 전체: 0~15초

시작 스크럽에 RecolorTrack 1개, 그 뒤 RecolorTrack 2개와 MoveTrack 17개다. **모두 duration 0**이다. source index는 actions의 0-based index이며 원본 값은 TSV에 보존했다.

| 시각(초) | source index / floor | 대상 | 값 |
|---:|---|---|---|
| 0 (스크럽) | 1 / 0 | 0~50 | Neon, 흰색 |
| 0.869565 | 12 / 1 | 1~501 | Standard, `262629ff` |
| **1.836957** | **100 / 70** | **330~565** | **opacity 0**, Linear |
| 3.527176 | 2985 / 1186 | 1186 | scale [0,0], opacity 0, OutCirc |
| 3.536233 | 2992 / 1187 | 1187 | 동일 |
| 3.545291 | 2998 / 1188 | 1188 | 동일 |
| 3.853262 | 3074 / 1194 | 1194 | 동일 |
| 3.862320 | 3080 / 1195 | 1195 | 동일 |
| 3.871378 | 3086 / 1196 | 1196 | 동일 |
| 4.070654 | 3092 / 1197 | 1197 | 동일 |
| 4.125002 | 3098 / 1198 | 1198 | 동일 |
| 4.179349 | 3104 / 1199 | 1199 | 동일 |
| 4.188407 | 3110 / 1200 | 1200 | 동일 |
| 4.197465 | 3116 / 1201 | 1201 | 동일 |
| 4.288045 | 3123 / 1202 | 1202 | 동일 |
| 4.396741 | 3129 / 1203 | 1203 | 동일 |
| 4.405799 | 3135 / 1204 | 1204 | 동일 |
| 4.414857 | 3141 / 1205 | 1205 | 동일 |
| 4.505436 | 3147 / 1206 | 1206 | 동일 |
| 13.913043 | 76 / 70 | 70~1070 | Single, 흰색 |

16개 단일 타일 효과는 훨씬 뒤 floor 1186~1206에 붙어 있지만 큰 음수 angleOffset으로 위 시각에 실행 대상이 된다. floor 967 등의 `-99999` 효과도 예약 시각을 계산해 필터링했으며, floor 번호만으로 제외하지 않았다.

자동 생성 효과도 포함했다. 이 창에서 Appear **94개(타일 0~93)**, Disappear **76개(타일 0~75)**가 실행 대상이다. 모두 이 구간의 Fade이며 개별 시각/길이는 JSON `generated`에 있다. 큰 타일 330~564에는 이 구간의 자동 Fade가 없다. `ffxFloorAppearPlus.ScrubToTime`은 미래 효과라도 FloorSetup을 먼저 실행하여 Fade 타일 opacity를 0으로 준비한다(`ffxFloorAppearPlus.cs:119-173`). 따라서 초기 준비 opacity와 스크럽 뒤 opacity는 구분해야 한다.

### PositionTrack과 큰 타일 초기값

| 타일 | 초기 scale | 초기 opacity | 등장 |
|---|---:|---:|---|
| 0~69 | 100% | 100% | Fade |
| 70~329 | 0% | 100% | Fade |
| **330~564** | **150%** | **100%** | **None** |
| 565 | 100% | 100% | Fade |

`scnGame.cs:509-562`의 PositionTrack은 맵 준비 중 적용된다. floor 70의 editorOnly scale100/opacity50은 게임 재생에서 건너뛴다. floor330의 PositionTrack은 위치 [-25,15], scale150, opacity100을 설정한다. floor565에서는 위치 [0,4], scale100, opacity100으로 바뀐다. `justThisTile=false`의 전파와 이후 덮어쓰기를 반영해야 한다. 최종 transform.localScale/startScale 대입은 `scnGame.cs:671-678`이다.

## 코드 후보별 판정

원본 기준 파일은 `E:\StutterFixWork\decompiled\Assembly-CSharp\` 아래다. 설치 DLL에서도 관련 호출/필드를 ILScan으로 재확인했다. 아래의 배제는 **제시된 초기 창과 기본 설정에 한정**되며 전체 맵의 픽셀 동등성 증명이 아니다.

| 경로 | 원본과의 관계 / 이번 판정 |
|---|---|
| FloorAnim.OpaOnly / FastOpa | index100이 해당. material 읽기 → 이전 Opacity Kill(true) → Approximately 비교 → opacity 직접 쓰기 → dontChangeMySprite=true 순서가 원본과 같다. 0으로 쓰는 값을 바꾸는 차이는 찾지 못했다. 이 분기는 기존 PinTileCull 호출보다 앞에서 반환하지만 불투명도만 바꾸므로 화면 경계를 이동시키지 않는다. |
| FloorAnim.Zero | 단일 타일 16개가 해당. position/rotation은 사용 안 함, scale X→Y→opacity 순서. 원본처럼 이전 같은 속성을 완료한 뒤 비교한다. 크기 0은 이징 끝점으로 계산하고 opacity0은 직접 쓴다. **ZeroTween 설정을 꺼도 FloorAnim의 Zero 경로는 꺼지지 않는다**(`ZeroOk`는 CanEase만 검사). FloorAnim 자체를 꺼 대조해야 한다. |
| FloorAnim 장기 Rec / 시작값 / Start 지연 | 초기 창 MoveTrack이 모두 duration0이므로 장기 New/Startup/Step은 사용하지 않는다. 따라서 초기 큰 타일 문제를 이 경로로 설명할 근거가 없다. |
| FloorAnim.RunSplit | 기본 LowFloorSplit=false, 기준 SplitMin=1000. 여기 MoveTrack 최대 236타일이므로 켜도 나눔 조건에 못 미친다. c.Start/job.Elapsed도 사용되지 않는다. |
| ZeroTween / InstantMove | 설치 대상은 ffxMoveDecorationsPlus이다. 타일 MoveTrack·TweenOpacity를 패치하지 않는다. 장식이 타일을 덮는 시각적 문제일 가능성은 별개라 단독 대조 계획은 남겼다. |
| TweenGuard (=TweenFix) | scrVfxPlus.Update 동안 DOTween isUpdateLoop를 켜 Kill 시 정리/재정렬을 미룬다. Kill(true)의 완료 setter를 생략하지 않는다. 같은 속성의 이전 트윈을 완료하지 않고 덮는 결함은 확인하지 못했다. DOTween 내부 목록 순서와 실제 렌더 상태는 런타임 검증 필요. |
| MoveFinish (=MoveApply) | scrDecoration.SetPosition/관리자 경로다. scrFloor 위치·크기·opacity에는 직접 적용되지 않는다. |
| EffectSplit (=EffectBudget) | 화면 효과를 예산 뒤로 미뤄 **실제 실행 프레임은 달라질 수 있다**. 큐는 먼저 밀린 것부터 처리한다. 시작 유예/60프레임과 첫 타일 전 구간에는 통과. 1.837초 opacity0이 지연되면 오히려 큰 타일이 더 오래 보일 수 있다. 원래보다 먼저 투명해지는 직접 근거는 못 찾았다. 정확한 실행 시각은 floorstate의 pending과 실제 song_s로 확인해야 한다. |
| RecolorSplit | 0~50은 51개, 1~501은 501개, 70~1070은 1001개다. 기본 ChunkTiles400에서 뒤 두 효과는 catchup이 끝났다면 나뉠 수 있다. 두 번의 컬러가 겹치면 이전 조각을 먼저 처리한다. 초기 값의 색 알파는 모두 ff이므로 직접 opacity0으로 바꾸지 않는다. 스타일/메시가 늦게 반영되는 가능성은 남긴다. |
| RecolorDefer | TileCull.IsCulled인 FloorMeshRenderer만 미룬다. 관리 색 필드는 즉시, Angle/Style/ColorFloor 엔진 작업은 나중에 한다. TileCull.Uncull/스크립트 consumer 앞에 Flush가 있다. opacity 필드와 dontChangeMySprite를 바꾸지 않으며 SetTrackStyle도 `_Alpha`를 쓰지 않는다. 관찰을 위해 기존 tilesig를 쓰면 FlushAll이 개입하므로 새 floorstate는 이를 하지 않는다. |
| MeshWarm | 실제 타일 opacity/scale/position을 쓰지 않고 비활성 scratch FloorMesh로 캐시를 준비한다. Standard/Neon별 width/length 계산은 원본 SetTrackStyle과 일치한다. 잘못된 캐시 내용으로 형상이 달라질 가능성을 일반적으로 배제할 수는 없으므로 단독 끔 대조 포함. |
| PositionTrack scale0 / opacity50 | 준비 단계의 원본 경로다. 게임에서는 editorOnly opacity50을 적용하지 않는다. 모드가 PositionTrack 적용 자체를 바꿔 opacity50을 남기는 경로는 찾지 못했다. 큰 타일들은 floor330의 scale150/opacity100 덮어쓰기가 따로 있다. |
| FloorLateFix | multiplanetLine 없는 타일의 위치/크기 읽기를 대체한다. opacity 비교와 `_Alpha` 쓰기는 남긴다. 알파 반영이 누락되는 직접 근거 없음. |
| BeatFix / AllocFix | OnBeat 호출 생략/Update 할당 감소. OnBeat의 dontChangeMySprite 조건은 원본과 연결된다. FloorAnim의 직접 쓰기는 TweenOpacity postfix를 거치지 않지만 매 프레임 should-call 조건도 현재 dontChangeMySprite를 읽는다. 직접 opacity0/scale0을 쓰는 경로는 아님. |
| FfxReuse / MatReuse / BigLevel | 효과 상태·논리 순서 재구성, 버려진 머티리얼 정리, 타일 부모 분할의 준비 경로다. 반복 시도만 다르면 추가 조사할 후보다. MoveTrack은 Start 메시지가 있어 재사용 대상에서 제외한다. 이 조사에서 초기 큰 타일의 값이 변하는 구체적 결함은 입증하지 못했다. |
| DormantSkip / SkipSame / FastLoop / FastBlend | 주 대상은 장식 업데이트/장식 렌더링이다. 타일 필드 직접 변경 원인으로 확인하지 못했다. 장식이 타일처럼 보이거나 타일을 덮는 경우를 구분해야 한다. |
| TileCull | 기존 별도 culltest에서 꺼 둔 타일을 다시 켜도 차이0이라는 증거를 유지. 이번에는 게임 재시험하지 않았다. 4060 Ti/1080p의 테스터 판 전체에 대한 일반적 배제를 새로 증명한 것은 아니다. |

### 같은 프레임의 duration0 / Kill 의미

원본 `ffxMoveFloorPlus.cs:97-188`은 속성별 **이전 Kill(true) → 완료된 현재값과 목표 비교 → 새 처리**다. scale은 X/Y 처리 전 localScale을 한 번 보관하지만 각 축 setter는 축 제한으로 현재 다른 축을 유지한다. FloorAnim의 Prev/ZeroScale도 이 순서를 따른다. opacity는 TweenOpacity(duration0)가 즉시 필드에 쓰며 새 Tween을 반환하지 않으므로 사전에 이전 죽은 Tween이 남을 수 있다. 이것을 무조건 새 Tween이나 null로 바꾸면 원본과 달라진다.

예: 같은 타일 `scale[0,0]` 다음 `scale[100,100]`이면 호출 순서대로 0→1이 되어야 한다. 뒤의 opacity 효과가 0이면 최종 scale1이어도 투명할 수 있다. scale 복원은 opacity 복원이 아니다. floor565의 쌍은 이 패턴이지만 0~15초 실행 목록에는 없다. 위 초기 목록에는 **같은 타일의 0/100 복원 쌍 자체가 없다**. 저프레임/EffectBudget으로 여러 예약 시각이 한 프레임에 모일 때도 큐의 순서를 함께 확인해야 한다.

`ffxPlusBase.cs:123-170`의 ScrubToTime은 StartEffect 후 종료 시각을 지났으면 Kill(true), 진행 중이면 Goto를 쓴다. FloorAnim은 catchup60프레임 동안 원본으로 보낸다. 이 시간 가정 밖의 외부 ScrubToTime에서 비활성 proxy의 Goto가 필요한 상황은 일반적인 추가 감사 대상이지만, 이 초기 창에는 장기 MoveTrack이 없어 이번 제보의 확정 원인으로 삼지 않았다.

### opacity 필드와 화면 알파 구분

요청에 적힌 `scrFloor.opacity setter`는 이 설치 버전에는 없다. `scrFloor.cs:165`의 public float 필드다. `TweenOpacity:1696-1711`은 값을 쓰고 dontChangeMySprite를 켠다. 화면의 `_Alpha`는 **LateUpdate:1316-1388**에서 `opacityLastFrame != opacity`일 때 반영한다. `OnBecameInvisible:1412` 부근에서 스크립트가 꺼질 수 있다.

따라서 원인 판단에 `opacity=1`만으로 충분하지 않다. 아래 구분이 필요하다.

- opacity가 이미 0: 맵 효과/효과 순서·시각부터 조사.
- opacity=1인데 재질 alpha=0: LateUpdate 실행·타일 script enabled·재질 교체 시점 조사.
- opacity/alpha 모두 1인데 scale0: PositionTrack/scale 효과의 초기값·복원 조사.
- 위 값이 정상인데 renderer=false 또는 culled=true: 렌더러 비활성화 경로 조사.
- 값과 renderer가 정상: `_Color.a`, 메시/재질/정렬·가리는 장식·후처리를 조사.

## 이번 코드 변경

`AutoTest.cs`에 `floorstate` 명령 한 개, `TileSig.cs`에 읽기 전용 스냅샷을 추가했다. Dev/PlayerAuto에만 포함되고 Player에는 포함되지 않는다. 1~32개 tile ID를 먼저 검증하고 다음을 출력한다.

```text
floorstate 0 1 50 69 70 329 330 400 564 565 1186
```

실제 song_s/frame, catchup, 효과/색 조각 큐 길이, 타일 enabled/active/culled, opacity/opacityVal/dontChangeMySprite, position/localScale/startScale, style, renderer.enabled/isVisible, 재질 이름/`_Alpha`/`_Color`를 읽는다. 상태 setter·미뤄 둔 작업 Flush·메시 강제 갱신은 하지 않는다. `Renderer.material` 대신 게임이 보관한 `FloorRenderer.material` 필드를 읽는다. 명령 때만 비용이 있으며 평상시 매 프레임 후크는 없다. 로그 수집 자체의 시간 비용은 있으므로 성능 판과 분리한다. 명령의 실제 게임 실행은 아직 하지 않았다.

## Claude의 다음 실행 순서

먼저 이미 진행 중이던 `srA/srB` 결과를 확인한다. 이번 턴에 확인한 `srv.out`은 비어 있었고 `night-results/srv*` 완료 결과도 없어서 결과를 추정하지 않았다. `srA/srB`는 rlNR의 추가 최적화를 상속하며 srB는 TileCull도 함께 끄므로, 차이가 나더라도 7개 중 하나로 바로 단정할 수 없다.

새 계획은 **1920×1080, BENCH_GAME=1, game+press로 처음부터** 재생한다. 초기 설정 비교용 defaults는 CLEAN을 바탕으로 조사 대상 9개를 명시적으로 켠다. 기존 rlNR의 추가 설정은 existingA에서만 별도로 비교한다. 테스터 Settings.xml 원본 전체를 확보한 것은 아니므로 defaults를 테스터 설정과 완전히 동일하다고 부르지 않는다.

각 계획에는 0.5/1/1.5/1.75/**1.95**/2.2/3/5/10/13.5/14/15초에 다음 순서가 들어 있다.

```text
timeout 900
wait 2
auto on
fpscap 0
rtsize 1920 1080
game D:/얼불춤 맵 파일/[노가다 크루] Silentroom - 1KXYZO/level.adofai
phasewindow on
press
waitsongpos 0.1 400
waitsongpos 1.75 400
floorstate 0 1 50 69 70 329 330 400 564 565 1186
framecmp grab before
framecmp save before
framecmp clear
waitsongpos 1.95 400
floorstate 0 1 50 69 70 329 330 400 564 565 1186
framecmp grab after
framecmp save after
framecmp clear
```

`1.75/1.95`에서 index100의 전후를 확인한다. 캡처/로그가 길어지면 목표 시각을 넘길 수 있으므로 실제 song_s를 확인하고 필요하면 **한 판당 시점 하나**로 줄인다. 서로 다른 실행의 움직이는 프레임을 그대로 픽셀차이0이라고 평가하지 않는다. 같은 시각의 상태와 시각적 재현을 먼저 확인한다.

권장 순서:

1. `silentroom-defaults.json`, `silentroom-alloff.json`에서 차이가 있는지 확인. alloff는 조사 대상9개만 끄며 TileCull은 유지한다.
2. 차이가 있으면 `flooranimoff`, `recolordeferoff`, `recolorsplitoff`, `effectsplitoff`, `meshwarmoff`, `tweenguardoff`를 각 새 판에서 확인.
3. `zerotweenoff`, `movefinishoff`, `dormantskipoff`로 장식 측 영향을 확인. 타일용 FloorAnim.Zero는 flooranimoff로 비교한다.
4. `tileculloff`, `waitskipoff`, `existingA`는 별도 보조 대조. 기존 무차이 culltest를 반복하기보다 최초 큰 타일의 opacity/alpha/scale을 우선 읽는다.
5. 처음에는 정상인데 반복 재시도에서만 달라지면 같은 조합으로 재시작을 비교하고 FfxReuse/재질 상태를 추가 감사한다. 완전한 무모드 대조는 Claude가 별도 판에서 준비한다.

실행은 **Claude가** 작업 폴더가 아닌 상위 공용 런처 폴더에서 한다(이 문서 작성 과정에서는 실행하지 않음).

```powershell
Set-Location E:\StutterFixWork
py -3 run_locked.py E:\StutterFixWork\wt-v14prod\out\silentroom-tiles\plans\silentroom-defaults.json wt-v14prod silentroom-defaults --keys 'floorstate,tile=,framecmp,단계 실패,오류로' --tail 4
```

기존 bench_v14에 추가할 경우 예시(생성기와 같은 원리):

```python
os.environ['BENCH_WT'] = 'wt-v14prod'
os.environ['BENCH_GAME'] = '1'
os.environ['BENCH_RT'] = '1920x1080'
B.CFG2['srFloorOff'] = (dict(B.CFG2['srA'][0], FloorAnim=False), [])
B.CFG_MID['srFloorOff'] = [
    'waitsongpos 1.75 400', 'floorstate 330 400 564 565',
    'framecmp grab off175', 'framecmp save off175', 'framecmp clear',
    'waitsongpos 1.95 400', 'floorstate 330 400 564 565',
    'framecmp grab off195', 'framecmp save off195', 'framecmp clear']
# MAPS['sr0']의 시작 목표 0.5초를 사용한다. seek는 사용하지 않는다.
```

## 빌드와 검증

| 対象 | 結果 | 証拠 |
|---|---|---|
| Developer Release | 오류0 / 경고84 | [로그](../../out/silentroom-tiles/build-dev.log) |
| Player Release | 오류0 / 경고4 | [로그](../../out/silentroom-tiles/build-player.log) |
| PlayerAuto Release | 오류0 / 경고63 | [로그](../../out/silentroom-tiles/build-auto.log) |
| native | exit0 | [로그](../../out/silentroom-tiles/build-native.log) |
| native exports | DEF 이름/ordinal 73/73 일치 | [exports](../../out/silentroom-tiles/native-exports.txt) |
| 계획/목록 일관성 | 14개 계획의 game 경로·seek 부재·12회 캡처 확인, 초기 목록 sanity 통과 | [검증 JSON](../../out/silentroom-tiles/verification.json) |

```powershell
dotnet build StutterFix.csproj -c Release --nologo -v minimal
dotnet build StutterFix.csproj -c Release -p:Edition=Player --nologo -v minimal
dotnet build StutterFix.csproj -c Release -p:Edition=Player -p:AutoTestBuild=1 --nologo -v minimal
& native/build-sfnative.bat 'C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.44.35207' 'C:\SFBundle\winsdk\microsoft.windows.sdk.cpp\c\Include\10.0.26100.0' 'C:\SFBundle\winsdk\microsoft.windows.sdk.cpp.x64\c' 'C:\SFBundle\tools\ispc-v1.31.0-windows\bin'
```

native DLL SHA256는 빌드 전후 동일: `9d31190df7ec648d7c4b0cc92e0b196e5e8a33f2d17c50224d651e933482e4b7`.
경고0을 주장하지 않는다. 오프라인 표는 실제 게임 픽셀검증을 대신하지 않는다. 이 결과로 “투명화 버그 수정 완료”라고 보고하면 안 된다.
