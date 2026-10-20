# S2i decorender 결과 — 정적 후보 포함과 프레임 단위 증분 메시

`DecoRender.cs`에 구현했다. `#if DEV || AUTOTEST`와 csproj의 조건부 Compile을 유지했다. **커밋·게임 실행·설치는 하지 않았다. 실제 Arche run 수, 프레임 CPU 시간, 화면 동일성, FPS 이득은 미측정**이다.

## 명령과 후보

```text
decorender on [frames=20] [scope=all|moving] [atlas=128] [floatcolor]
decorender compare
decorender state
decorender off
```

- 기본 `scope=all`: 정적/움직이는 장식을 모두 적격 후보로 받아 즉시 선택한다. 20프레임 관찰 대기를 생략하며 `decorender on 0 scope=all`이 유효하다. 변화 감지는 계속한다.
- `scope=moving`: 기존처럼 관찰 기간의 행렬·색·flip 변화가 있는 장식만 남긴다. frames는 1~600, 기본 20이다. all에서는 0~600을 받아도 관찰 대기는 하지 않는다.
- `atlas=`: 정수 MiB, 기본 128, 허용 1~256. 0·257 이상·잘못된 옵션은 사용법을 출력하고 시작하지 않는다.
- 기존 정확한 `scrVisualDecoration`, Sprites/Tile 기본 조건 또는 Sprites/Default, Clamp, 한 변 2048 이하, repeat=1, 마스크/히트박스/점샘플링 제외 등의 조건을 유지했다. 소스 보임은 후보 조건이 아니다.

## 아틀라스 예산과 정적 텍스처

실제 계획된 RGBA32 페이지의 패딩·power-of-two 여유·밉 전체 바이트 합계에 새 예산을 적용한다. 이전의 별도 `sourcePixels <= 4096*4096` 제한을 제거했다. 원본 텍스처·CPU 디코딩 버퍼·메시 메모리는 이 GPU 아틀라스 예산에 포함되지 않는다.

초과하면 원본 면적이 가장 큰 텍스처부터 그 텍스처를 쓰는 장식만 제외하고 다시 계획한다. 면적 동률은 texture instance ID 순이다. 같은 sorting key의 다른 텍스처 장식은 남긴다. 제외 장식은 `atlas-budget`으로 기록되며, 원본 렌더러가 정렬 줄에 남아 run 장벽이 된다. 텍스처 크기·제외 장식 수·재계획 전 바이트도 로그에 남긴다.

`static-textures`/`static-texture` 로그는 텍스처 종류 수와 면적 상위 20개의 ID·이름·가로세로·장식 수를 출력한다. 시작 때와 명시적 `state` 때 출력한다. 여기서 static은 **세션에서 샘플링한 행렬·색·flip이 아직 변하지 않은 장식**이다. 시작 로그는 관찰 0프레임이므로 모든 최초 후보를 포함한다. 이후 state 로그는 현재 남은 후보 중 변화 미관측 장식만 포함한다. 미래 이벤트까지 없는 영구 정적 장식이라는 뜻은 아니다. all의 즉시 시작을 위해 별도 20프레임 분류를 기다리지 않는다.

## 프레임당 한 번 갱신

첫 카메라 `onPreCull`에서 `Time.frameCount`를 확인해 그 프레임에 한 번만 변화 검사·외부 렌더러 조사·정렬·run 재구성·정점 갱신을 수행한다. 컬링 전에 월드 정점과 명시적 월드 bounds가 준비된다. 다른 카메라는 Unity layer/cullingMask를 통해 같은 MeshRenderer/Mesh를 렌더한다. 카메라마다 월드 정점을 다시 계산하거나 업로드하지 않는다.

- 장식의 마지막 행렬과 색을 float 비트 단위로 비교하고 flip·보임도 저장한다.
- 메시 생성 때 장식별 정점 오프셋과 인덱스 토폴로지를 고정한다. 새 run은 전체를 한 번 쓰고, 기존 run에서는 바뀐 장식의 정점만 다시 계산한다.
- 숨은 장식은 해당 정점들을 원점의 퇴화 삼각형으로 바꾼다. 따라서 숨김/재등장에 인덱스를 다시 만들 필요가 없다. run 전체가 숨으면 MeshRenderer를 끈다.
- run별 첫 변경 정점~마지막 변경 정점을 하나의 연속 범위로 합쳐 `SetVertexBufferData`를 한 번 호출한다. 사이의 안 바뀐 정점은 저장된 버퍼 값을 그대로 업로드할 수 있다. 변경이 없으면 호출하지 않는다.
- 일반 색은 명시적 `UNorm8x4` 버퍼, floatcolor는 `Float32x4` 버퍼다. 실제 색 attribute 형식·차원·stride를 생성 시 검사한다. 매 프레임 Mesh.Clear/SetTriangles/SetVertexBufferParams를 반복하지 않는다.
- 정렬 comparator의 큐·거리·재질 ID를 렌더러당 한 번 읽어 캐시한다. run 재사용은 첫 멤버 ID로 찾아 멤버 순서를 검증한다. 이전 run 전체를 매번 선형 탐색하던 제곱 비용과 GroupKey 반복 생성을 줄였다.

카메라 목록의 모든 로드된 씬 카메라(비활성/disabled 포함)를 확인한다. 하나라도 원근 또는 z축 이외 정렬이면 보수적으로 singleton run을 사용한다. 카메라별로 별도 메시를 만들지 않는다. 이 경우 run 수 감소가 작을 수 있다. 비교용 임시 clone camera는 목록에서 제외한다.

원본 재질 대여와 forceRenderingOff는 카메라 렌더 동안 적용하고 PostRender에서 복원한다. 기존 sprite/material/정렬/마스크 등 변화 감지는 해당 장식을 복원·제외하며, 다음 run 계획에서 원본이 장벽이 된다. 영향 없는 메시·재질은 유지한다.

compare도 그 프레임에 이미 만든 메시를 재사용한다. compare 직전 소스가 프레임 스냅샷과 다르면 실패 사유를 남기고 중단한다. 비교 도중의 변화는 기존 `snapshotStable` 검사로 확인한다. 기존 clone camera 한계(원본 이미지 효과·command buffer·다른 카메라 미복제)는 유지한다.

## 중단 조건 조사와 수정

이전 `ValidSession()`은 아래를 하나의 포괄적 transition 사유로 처리했다.

1. manager 파괴/instance 교체.
2. mainCamera 파괴 또는 scrCamera.camobj 교체.
3. SceneReset.Resetting 또는 Reset 횟수 변경.
4. 활성 scene handle 변경.
5. manager.allDecorations의 목록 객체 교체.
6. 한 번이라도 Hitch.Playing이 true였는데 현재 false.

6번은 실제 맵 전환 자체가 아니다. `Hitch.Playing`은 `Hitch.wasPlaying`이며 `GcControl.IsPlayingFast()` 쪽에서 gameworld·paused·editor playMode·controller state·종료 hook 등을 합쳐 갱신한다. 긴 프레임 길이를 직접 검사하는 코드는 없었다. **기존 포괄 로그만으로 과거 중단이 정확히 어느 조건이었는지는 확정할 수 없다.** 긴 프레임 뒤 재생 모니터 상태가 바뀌었다는 가능성과 실제 전환을 분리해야 한다.

새 코드는 Hitch.Playing 의존을 제거하고 각 실제 변경 사유를 개별 로그로 남긴다. 1~5는 유지한다. 시작 시 편집기 재생이었다면 실제 `scnEditor.playMode`의 편집 복귀도 검사한다. 첫 PreCull의 프레임 준비에서 씬 카메라 instance ID 집합 변경도 검사한다. 카메라의 위치·활성 여부·cullingMask·그리기 횟수 변화만으로 목록 변경이라고 판정하지 않는다. 일시정지·monitor lag·단순 장시간 프레임은 단독 중단 조건이 아니다.

기존 `SceneReset.Prefix()`의 `DecoRender.Stop()` 직접 호출도 유지한다. 실제 다시 시작/편집 복귀 reset에서는 즉시 원본 재질과 forceOff를 복구한다. 이 hook의 사유 표시는 기존 `off/unload/reset`이다.

## state 통계

| 항목 | 의미 |
|---|---|
| scope, candidates | 범위와 최초 적격 후보 수(예산·추가 안전 제외 전) |
| moving-candidates | 현재 남은 후보 중 행렬·색·flip 변화가 관측된 수 |
| taken / runs | 현재 관리 장식 수 / 현재 메시 수 |
| average-run / max-run | 관리 장식 수÷run 수 / 최대 멤버 수 |
| run-sizes | 1, 2~5, 6~20, 21~100, 101+의 run 개수 |
| unmanaged-breaks | 한 키에서 후보 사이에 비관리 렌더러가 끼어 끊긴 간격 수 |
| barrier-renderers | 관련 키의 비관리 렌더러별 제외 이유/종류 수. large-texture, repeat-not-one, wrap, mask-hitbox-mesh, atlas-budget, shared-material, changed, non-decoration:형식 등 |
| keys | 현재 관리 장식이 있는 정렬 키 수 |
| build-ms/last-frame, mean-frame | 프레임 준비의 마지막/평균 CPU 시간 |
| compute-ms / upload-ms | 마지막 프레임의 업로드 외 계산·조사 / SetVertexBufferData 호출 CPU 시간 |
| changed-items / uploaded-vertices | 값이 바뀐 장식 수 / 실제 업로드 범위의 정점 수 |
| prepared-frame / builds | 마지막 준비 frameCount / 프레임 준비 실행 횟수 |

barrier-renderers는 관련 키의 앞/뒤 비관리 렌더러도 포함하므로 unmanaged-breaks와 합계가 같지 않다. unsupported-order 키의 렌더러는 별도 이유로 집계한다. 서로 다른 아틀라스·실효 재질 상태·z·Unity layer 등 관리 후보 사이의 분할도 runs에 반영된다. 기존 excluded-keys는 개별 예산 제외 건수가 아니다.

시간은 PruneChanged+RebuildRuns+Build 전체를 포함하고 업로드 시간을 별도로 뺀 계산값이다. 카메라 목록 조사, 카메라마다의 대여/복구, 초기 아틀라스 생성, compare 캡처 비용은 포함하지 않는다. 업로드 ms는 CPU 호출 시간이며 GPU 완료 시간은 아니다. 외부 렌더러 조사·정렬은 여전히 프레임마다 한 번 전수 수행하므로 전체 비용이 작다고 보장할 수 없다.

## 검증

- Release DEV / PlayerAuto / Player: 모두 빌드 오류 0. 기존 참조 버전 충돌·obsolete·미사용 필드 경고는 남는다.
- MSBuild Compile 평가: DecoRender.cs는 Player=0, PlayerAuto=1.
- 실제 소스 추출 run 검사: 29건 통과. 기존 복원·재질·정렬 사례에 정적 포함 150개→1 run, 개별 atlas-budget 장벽, 카메라 비호환 singleton을 추가했다.
- 실제 Build/PrepareFrame/ValidSession 소스 추출 검사: 38건 통과. 두 색 버퍼 형식에서 4개 카메라의 프레임 준비 1회, 변경 없음→업로드 0회, 장식 1개 변경→그 slice만 업로드, 숨김·재등장·flip·색·새 run 재기록, float 비트 비교, manager/list/scene/reset/mainCamera/카메라 목록/편집 복귀를 확인했다.
- 실제 예산 계획 루프/명령 parser 소스 추출 검사: 16건 통과. 128MiB에서 예전 64MiB source-pixel 한도를 넘는 후보 유지, 큰 텍스처의 장식만 제외·같은 키의 나머지 유지, 전부 제외된 빈 계획, 기본/최대/잘못된 인수를 확인했다. 아틀라스 배치 API는 모형이므로 실제 페이지 패킹·GPU 검증이 아니다.
- `py -3 tools/check-ui-encoding.py`: clean. `git diff --check`: 통과.

검사는 Unity 씬·렌더러·Mesh API를 모형으로 대체한다. **Unity 콜백 순서·컬링·GPU·픽셀 동일성 검증을 대신하지 않는다.** 초기 증분 fixture는 실제 Begin과 달리 마지막 색을 초기화하지 않아 motion counter에서 실패했다. fixture를 실제 초기 스냅샷과 맞춘 뒤 위 38건을 통과했다.

빌드 로그: `obj/decorender-8-build-dev-final.log`, `obj/decorender-8-build-autotest.log`, `obj/decorender-8-build-player.log`. 검사 생성기: `obj/decorender-8-check/{generate,incremental,budget}.py`. 최종 검사 로그: `obj/decorender-8-check-final.log`, `obj/decorender-8-incremental-verified.log`, `obj/decorender-8-budget.log`. obj 산출물은 git 비추적이다.

## 게임에서 돌릴 순서

사용자가 빌드를 설치한 뒤 Arche의 같은 재현 구간에서 실행한다.

```text
decorender on 0 scope=all
decorender compare
decorender compare
decorender compare
decorender state
decorender off
decorender state
```

compare는 필요한 N회 반복한다. 각 compare의 song/frame·snapshotStable·임계값별 픽셀 차이와 원본/그룹/diff 이미지를 확인한다. state에서 candidates/taken 대비 runs, 평균/최대/분포, atlas-budget 장벽, compute/upload ms를 기록한다. **성공 판단은 실제 runs가 후보 수보다 충분히 작고 수백 이하인지**이며, 오프라인 150→1 사례로 게임 결과를 추정하지 않는다. off 뒤 원본 보임·재질 복원도 확인한다.

예산을 바꿔 비교하려면 먼저 off 후 `decorender on 0 scope=all atlas=256`으로 새 세션을 연다. 색 정밀도 대조는 `decorender on 0 scope=all floatcolor`, 기존 움직임 범위 대조는 `decorender on 20 scope=moving`을 쓴다. 화면 compare를 포함한 판은 별도 FPS 성능 근거로 사용하지 않는다.
