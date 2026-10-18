# FastLane 작업 결과 — 2026-10-19

## 결론과 적용 범위

**실사용에 남긴 것은 제거할 기록이 없는 프레임의 Compact 패스 생략이다.** 기본 켬이며 모드 폴더 `fastlane-off` 파일을 로드 때 한 번 읽어 끈다. 회전·크기 인라인과 장식 판정 캐시는 오프라인 벤치에서 기존 경로보다 느려 실사용에서 제외했다. 해당 후보는 Dev/AutoTest 벤치 및 오프라인 비교용으로만 남겼다. Player에는 후보 단계 함수가 컴파일되지 않는다. 위치 쌍은 변경하지 않았다.

인계의 `recs[w++]` 인플레이스 융합은 채택하지 않았다. NewRec의 InList 재사용, 실행 중 추가, 앞서 진행한 기록의 취소, KillAll의 recs 순회에 영향을 주지 않도록 **완료·취소·예외가 발생한 프레임에는 기존 프레임 끝 Compact를 그대로 실행**한다. 따라서 지시 1의 구현 방식과 다르며, 매 프레임 정리 비용을 반드시 0으로 만드는 구현은 아니다. 지시 2·3은 후보 측정 후 미채택이다. 요청 전체를 성능 개선 성공으로 판정하지 않는다.

게임 실행·Mods 설치·커밋·푸시는 하지 않았다. 실게임 비트 동일성, late-verify/tightlate/decosig 동시 검증, Mono 성능은 미확인이다.

## 변경 파일

- `DecoAnim.cs`: 기록이 끝나거나 취소되거나 예외로 중단될 때 sticky dirty 표시. GroupStep 실행 때는 보수적으로 dirty. dirty가 없을 때만 정리 생략. 기존 정리 본체를 CompactRecords로 분리.
- `DecoAnim.FastLane.cs`: 로드 설정, 정리 생략 조건, 표본 검증, 상태·벤치 명령. 실패한 회전·크기 후보는 진단 빌드/fixture에 한정.
- `AutoTest.cs`: `fastlane bench|verify|verify off|state`. 기존 decosamp/decoablate 명령이 Dev에서 AUTOTEST 전용 필드를 참조해 발생하던 빌드 오류 5개를 `#if AUTOTEST`로 수정.
- `StutterFix.csproj`: 새 partial 파일 포함.
- `tools/GroupStepCheck/{Program.cs,run.py,GroupStepCheck.csproj}`: 실제 소스 단계 함수·UpdatePrefix·Complete·Compact 추출 및 FastLane 연결, 계산·완료·예외·순서·콜백·할당 대조와 벤치. 기존 fixture에 빠져 있던 PosFast 비활성 대역도 추가.

기존 `.codex/config.toml` 변경 및 시작 당시 존재하던 미추적 파일은 수정하지 않았다.

## 먼저 수행한 후보 벤치와 선택

첫 후보 벤치에서 회전 0.0247→0.0361µs, 크기 X 0.0244→0.0397µs, 크기 Y 0.0201→0.0274µs로 느려졌다. 이를 확인한 뒤 실사용 Step 연결을 제거하고 정리 생략만 남겼다. 캐시 검사·후보 가드·별도 단계 호출 비용이 단순 관리 코드 대역에서 이득을 넘는 것으로 **추정**한다. 원래 게임의 판정 비용을 재현한 벤치는 아니므로 Mono에서 같은 비율이라고 해석할 수 없다.

후속 전체 검증 실행의 측정값:

| 항목 | 기존 µs/호출 | 후보/생략 µs/호출 | 판단 |
|---|---:|---:|---|
| 회전 5 | 0.0239 | 0.0350 | 실사용 미채택 |
| 크기 X 7 | 0.0243 | 0.0387 | 실사용 미채택 |
| 크기 Y 8 | 0.0196 | 0.0270 | 실사용 미채택 |
| 위치 쌍 14 | 0.0518 | 0.0477 | 같은 코드, 순서·런타임 잡음 포함; 개선으로 해석 금지 |
| 루프 뼈대 15 | 0.0022 | 0.0021 | 같은 코드, 개선으로 해석 금지 |
| 정리 16 | 0.0095 | 0.0000 | 전부 Running인 경우 패스 생략; 반올림된 0 |

N=2,150, 반복 200, 표본 7개 중앙값. .NET 8 Release, 관리 setter 대역, 실제 DOTween 이징 사용. 측정은 반복 전체 앞뒤 Stopwatch 한 쌍이며 호출마다 Stopwatch를 붙이지 않았다. 위치 쌍 대역에는 fixture의 짝 Dictionary 조회가 포함된다. 정리의 생략 값은 조건 분기만 남긴 하한이며 실제 프레임 전체 비용과 다르다.

실제 소스 UpdatePrefix + 6,417개 회전 기록, 200프레임 × 7표본 중앙값: 끔 **0.1627ms**, 켬 **0.1240ms**. 양쪽 측정 구간 관리 할당 **0바이트**. Unity Transform·Mono·실맵 부하는 포함하지 않는다. 이 값으로 실게임 FPS를 예측하지 않는다.

`fastlane bench`는 별도로 2,150개 GetUninitializedObject 더미 이미지 장식과 실제 더미 Transform을 만들고, live recs를 진행시키지 않는다. 회전·크기는 성공한 판정을 미리 넣은 hot-cache 후보와 필드 setter 대역을 비교한다. 실제 최초 DeferTransform 판정 비용은 제외한다. 위치 쌍은 pivot 필드와 localPosition만 쓰는 대역이며, 루프·전체 Running 정리도 출력한다. Prof=true인 Dev에서는 인라인 대신 baseline을 두 번 재므로 후보 판단에는 **PlayerAuto**에서 실행한다. 이 명령은 이번 작업에서 실행하지 않았다. 벤치의 더미 생성은 측정 창 밖에서만 실행할 것.

## 순서·재사용·콜백 검토

- NewRec는 recs.Add로 추가하며 `!oldR.InList`일 때만 기존 Rec를 재사용한다. 실사용 변경은 목록을 진행 중 재배열하거나 InList를 일찍 해제하지 않는다. 동적 Count를 따르는 기존 순회도 유지한다.
- Step/StepPair 완료, Complete(Kill/Complete), Kill(false), KillAll, 예외 종료, 이미 중단된 슬롯을 만났을 때 dirty. GroupStep은 부분 완료·그룹 예외·i 증가 경로를 모두 보수적으로 기존 Compact로 보낸다.
- Dev는 기존 UpdatePostfix의 Shadow 비교 후 Compact를 유지한다. AUTOTEST AblateMask의 continue는 기록을 완료시키지 않으며, 다른 완료·취소는 sticky dirty가 잡는다. 작은 dt로 진행하지 않는 프레임도 이전 dirty를 유지한다.
- Compact는 사용자 콜백을 호출하지 않고 Running/Shadow 판정과 목록 이동·InList 해제만 한다. dirty가 없으면 전부 Running이므로 원래 패스도 목록 내용과 InList를 바꾸지 않는다.
- ILScan METHOD=scrDecoration 호출 목록에서 SetColor/SetOpacity→ApplyColor, SetPosition 축→SetPosition, 회전·크기→Transform 경로를 확인했다. 외부 Harmony 패치까지 포함한 무재진입 증명은 하지 않았다. 실사용 정리 생략은 무재진입을 전제로 하지 않는다. 콜백이 추가·취소하더라도 기존 순회와 끝 정리를 따른다.

미채택 캐시 후보는 성공한 DeferTransform 판정만, 연속된 회전·크기의 필드 쓰기 사이에서 재사용한다. 색·불투명도·위치·피벗·시차·GroupStep·폴백·예외 앞에서는 무효화한다. 따라서 GetVisible/마스크/숨김/Dormancy를 바꿀 수 있는 게임 setter를 캐시 범위 밖에 둔다. 일반 MoveApply.SkipForLate나 lateSkippedSet의 등록 방식을 바꾸지 않았다. Eval 재사용은 추가하지 않았다.

## 검증 결과

명령: `py -3 tools/GroupStepCheck/run.py`.

- 기존 scalar 3,240건 + 위치 쌍 300건 통과. 상태 가드·중간 setter 변이·예외 폴백·검증 예외 대조 통과.
- 미채택 FastLane 계산 후보 1,440건 비트 대조 통과. 후보 표본 계산/판정 검사 30건, 불일치 0. fixture의 판정 대역 검증이며 게임 판정 검증은 아니다.
- 정리 마스크 256건: 생존 순서, 완료 기록 InList의 프레임 끝 해제, 생략 조건 통과.
- dirty 표시 누락을 의도적으로 넣은 표본에서 검증 실패 감지 및 FastLane 자동 끔 통과.
- 실제 UpdatePrefix 80조합: 켬/끔의 위치 쌍·GroupStep·dt·예외 결과 일치.
- setter 콜백 중 앞선 기록 Complete + 새 기록 추가: 켬/끔 결과와 InList 해제 시점 일치.
- 기존 probe-off 10,000프레임 및 FastLane 실제 갱신 벤치 구간: 관리 할당 0바이트.

빌드(설치 훅 호출 없음):

| 명령 | 오류 | 경고 |
|---|---:|---:|
| `dotnet build StutterFix.csproj -v q --nologo -clp:ErrorsOnly` | 0 | 35 |
| 위 명령 + `-p:Edition=Player` | 0 | 33 |
| 위 명령 + `-p:Edition=Player -p:AutoTestBuild=1` | 0 | 35 |

경고에는 참조 어셈블리 버전 충돌과 미사용 필드가 포함된다. 오류 0을 무경고로 표현하지 않는다.

## 상태·검증·계측

실사용은 `live=compact-only`, `compact/cache/inline=True/False/False`가 정상이다. `candidateCompatible=False`는 실사용 캐시·인라인을 채택하지 않았다는 뜻이다. compactSkipped/compactDirty/compactOff로 생략·dirty 폴백·비활성 폴백을 구분한다. inline/fallback/cacheHits/invalidations는 후보 전용이며 실사용에서는 0이다.

`fastlane verify`는 생략 후보 프레임 64개 중 1개에서 실제 모든 기록의 Running을 대조한다. 불일치 시 세고 FastLane을 끄고 그 프레임도 Compact로 처리한다. Dev에서는 Shadow를 위해 원래 정리 패스를 유지하므로 생략 검증은 PlayerAuto에서 한다. `fastlane verify off`로 표본 순회를 끈다. 정상 플레이 때 검증용 순회·할당·호출별 타이머·새 Harmony 패치는 없다.

실사용 인라인이 없으므로 기존 decosamp Step/StepPair 바깥 측정이 모든 실제 단계를 계속 포괄한다. sampTicks 확장은 필요 없다. 정리 항목은 생략 시 분기와 카운터만 포함한다. late-verify/tightlate verify/decosig 구현은 바꾸지 않았으나 게임 안 동시 실행 결과는 미확인이다.

## 예상 이득과 Claude 실행 순서

**추정식**: 1프레임 절감 ≈ `0.246ms × 정리 생략 프레임 비율 p − dirty 표식/분기 비용`. p=1이면 약 0.246ms가 상한 성격의 추정치다. Arche에서는 완료·색 이벤트가 자주 겹치면 p가 낮아질 수 있다. 회전·크기·위치 계산의 절감은 0으로 둔다. 상태 통계에서 p=`compactSkipped/(compactSkipped+compactDirty)`를 계산하되 비활성·Dev 실행은 제외한다.

다음은 Claude가 이후 별도 승인 범위에서 수행할 절차이며 이번에는 실행하지 않았다.

1. PlayerAuto 빌드로 bench를 한 번 별도 실행하고 결과를 보관한다. 이후 프로세스를 끝내 벤치의 JIT·더미 부하를 성능 판에 섞지 않는다.
2. 동일 해상도·주사율·모드·설정·맵 경로로 **새 프로세스 ABBA**. A=fastlane-off 존재, B=없음. 로드 때 읽으므로 실행 중 파일 토글이나 reload로 대체하지 않는다. 기존 파일 존재 상태는 마지막에 복구한다.
3. 각 판 Arche 257.8~278.8초 같은 창에서 `fuse begin` + `decosamp begin`, 21초 후 `decosamp end` + `fuse end`, 이어 `fastlane state`. 이 성능 판은 fastlane/tightlate/late 표본 verify를 꺼서 잰다.
4. fuse의 DecoAnim 시간과 프레임 수, decosamp의 Compact 시간·루프 전체·Step/StepPair 호출당 비용, 생략 비율을 함께 비교한다. Step/StepPair는 그대로이므로 감소를 필수 성과로 요구하지 않는다. 검증 창 밖 콜드 이벤트와 전체 프레임 악화도 확인한다.
5. 별도 검증 판에서 fastlane-off 없이 `fastlane verify`, `tightlate verify`, 기존 `late-verify`를 모두 켠다. fastlane checks>0/mismatch=0, TightLate/Late 불일치 0 확인. 실제 검증 횟수 0이면 통과라고 하지 않는다.
6. 모든 판 곡 끝 동일 상태에서 `decosig A1`/`B1`/`B2`/`A2`를 저장해 비교한다. 유한 시점 대조이므로 곡 전체 모든 프레임 비트 동일성의 증명으로 확대하지 않는다.
7. 두 B판 모두 Compact/DecoAnim 시간이 안정적으로 줄고 프레임·최악 프레임 회귀가 없으며 상태 대조가 모두 0일 때 실게임 채택을 판단한다. 차이가 판간 변동 안이면 이득 미확인. dirty가 잦아 효과가 없거나 회귀하면 FastLane 정리 생략도 되돌린다.

결과 문서는 apply_patch로 UTF-8 BOM을 포함해 작성했다. `Get-Content -Encoding UTF8`로 한글 문장을 읽었고, BOM 바이트 EF BB BF·엄격 UTF-8 디코딩·대체문자 없음 확인을 통과했다. 마지막 반복 실행도 모든 대조를 통과했으며 전체 Update 중앙값은 끔 0.1632ms / 켬 0.1235ms였다.
