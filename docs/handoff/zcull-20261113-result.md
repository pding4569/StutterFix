# Z-cull 상한 측정 도구 결과 — 2026-11-13

작업 폴더 `E:\StutterFixWork\wt-v14prod`. `zcull census`와 실행 준비물을 구현했다. **게임 실행, 설치 DLL 교체, 커밋, 푸시는 수행하지 않았다.** 실제 맵의 가림 비율 및 성능 개선 여부는 아직 측정하지 않았다.

## 구현

- `ZCull.cs`, `ZCullPolicy.cs`: 명시적 일시정지 명령에서만 객체를 만들고 메인 `Camera`의 `onPreRender`에서 한 장면을 수집한다. 완료/실패/취소/모드 해제 시 콜백·RT·버퍼·재질·번들을 해제한다. 원본 렌더러/재질/텍스처/설정에는 쓰지 않는다. 명령이 끝난 뒤 상주 객체와 매 프레임 작업은 없다.
- 정렬은 기존 `SceneRenderOrder.Key`의 층 → 순서 → 큐 → 거리 내림차순 → 셰이더 순위 → 재질 순위를 재사용한다. 렌더러 ID는 결정적 출력용이며, `SameMaterialTie` 묶음 전체는 가리개에서 제외한다. 거리에는 카메라 축/CustomAxis와 SpriteSortPoint를 반영한다.
- `Occlude`의 알려진 `Sprites/Default`, `Sprites/Tile`·일반 혼합·색 알파 1·마스크/스텐실/SortingGroup/MPB 배제 조건을 따른다. 전체 화면 덮기와 텍스처 전체 불투명 사전 판정은 제거했다. 실제 sprite 메시와 UV로 픽셀 알파를 판정한다. 밉맵·Trilinear·비등방 필터·알파 분리·packed sprite는 가리개에서 제외한다. 구현하지 않은 키워드(점 샘플링, PixelSnap 등)와 Tile flip도 보수적으로 제외한다.
- `GpuDeco=0`, `RunLod=false`, 일시정지를 요구한다. 원근/스테레오 카메라와 원근 투명 정렬은 실패 처리한다.
- 카메라 해상도의 별도 RFloat/24비트 깊이 RT에 역순 깊이 선행 패스 → `ZTest Less` 생존 패스 → `Always` 전체 패스를 실행한다. 색 합산은 One/One이며 알파 0도 센다. 가리개별 덮은 픽셀 수는 별도 패스로 구하고, 겹친 삼각형이 중복 픽셀로 집계되지 않게 덮어쓴다.
- **자기 가림 방지:** 선행 깊이는 `(N-index)/(N+1)`, 세기 깊이는 `(N-index-0.25)/(N+1)`로 둔다. 같은 깊이에 그대로 `Less`를 적용하면 가리개 자신까지 빠지므로 자기 순번 안에서 1/4 간격을 당겼다. 앞 가리개의 깊이는 여전히 통과하지 못한다. reversed-Z도 처리한다.
- 렌더러마다 두 전체 크기 RT를 읽는 대신, 같은 coverage RT를 비우고 재사용하면서 GPU Reduce로 렌더러별 전체/생존/덮음 `uint`를 합산한다. 마지막에 작은 결과 버퍼 하나만 AsyncGPUReadback한다. CPU에서는 double로 합친다. 해상도와 draw 수에 상한을 두며, 200,000순번까지 24비트 깊이 간격을 검사했다.
- 요약은 인계문 형식 `[zcull] renderers=… occluders=… fragments_total=… fragments_alive=… culled=…%`. 잘린 조각 수 순 렌더러 상위 20개와 덮은 픽셀 수 순 가리개 상위 10개를 출력한다.
- `AutoTest`는 결과 수신까지 같은 `zcull census` 단계에 머문다. 그 뒤 `resume`, `quit`로 진행하며, 실패한 측정을 완료로 처리하지 않는다.

## 보수 처리와 수치 해석

GrabPass/화면 읽기 셰이더에 입력되는 뒤쪽 그림을 지우면, 가리개 밖 픽셀까지 달라질 수 있다. 따라서 알려진 일반 sprite 경로가 아닌 항목은 **장벽**으로 취급하고, 같은 층/순서/큐 묶음도 장벽으로 제외했다. 깊이는 장벽으로 나눈 구간마다 지워 구간 내부에서만 가림을 인정한다. SortingGroup은 바깥 그룹의 키를 사용하되 해당 묶음을 장벽으로 둔다. near/far 경계에 걸린 가리개도 제외한다.

이 때문에 `culled`는 **지원·검증한 일반 sprite 구간에서 확인한 가림 비율**이다. 모든 셰이더를 지원했을 때의 수학적 최대치와 같다고 주장하지 않는다. 값이 작더라도 `barriers`/`ambiguous`가 많으면 그것만으로 아이디어를 폐기하면 안 된다. `submitted` 로그에 장벽·동점 수와 측정 범위를 함께 남긴다.

수집 목록은 장면·enabled/active/forceRenderingOff·카메라 mask·프러스텀 조건을 통과한 후보이다. 엔진 내부 culling 결과를 직접 읽는 API나 하드웨어 fragment counter는 사용하지 않는다. 전체/생존 수는 **1샘플 기하 coverage**이며, 다음 항목은 원본 렌더와 정확한 일치가 미검증이다.

- 알 수 없는 셰이더의 정점 변형·다중 패스·GrabPass 비용, 일부 특수 렌더러의 원본 기하 처리
- 엔진 occlusion culling/LOD 등 내부 제출 결과와 실제 게임 카메라 콜백의 상태
- MSAA 샘플 수, 원본 깊이/스텐실/알파 테스트에 의한 fragment 실행 차이
- 실제 게임의 Sprites/Tile 재질·색·동적 renderer property 전달 전체 조합. UV 모델은 기존 연구 셰이더의 fragment-stage RepeatX/Y 방식을 따랐으며, 게임 실측 대조는 남아 있다.

따라서 결과는 가림 가능량을 검토하는 진단 자료이며 FPS/GPU 시간 개선의 증거가 아니다. 실제 셰이더 제품화와 전체 곡 픽셀 일치 검증은 범위 밖이다.

## 확인한 검증

| 검사 | 결과 | 증거 |
|---|---|---|
| Player AUTOTEST Release | 오류 0, 경고 57 | `out/zcull-auto-build.txt` |
| 일반 Player Release | 오류 0, 경고 63 | `out/zcull-player-build.txt` |
| 일반 Player UTF-8/UTF-16 문자열 검사 | zcull/ZCull/ResearchZCull/ZCullReduce 없음 | `out/zcull-package-check.txt` |
| AUTOTEST 진단 타입·임베디드 리소스 검사 | 있음 | 같은 파일 |
| CPU fixture | PASS, 10검사 + 200,000개 깊이 순번 | `out/zcull-fixture.txt` |
| Unity 6000.3.10f1/D3D11 번들 생성·재로드 | PASS, 4패스·Reduce 커널 | `out/zcull-gpu-fixture.txt` |
| 독립 Unity GPU 합성 장면 | PASS, 12사례, reversed-Z | 같은 파일 |
| 6맵 JSON·루트 스크립트 일치·결과 파서 | PASS | `out/zcull-package-check.txt` |
| 추가 일반 DEV 빌드 | 기존 오류 9개 재현 | `out/zcull-dev-build.txt`, `out/zcull-dev-baseline-build.txt` |

CPU fixture는 실제 사용하는 정렬/가리개 색 알파·혼합 정책과 두 겹친 사각형(72조각 중 16조각 가림), 동점 묶음 전체 제외를 검사한다. GPU fixture는 번들을 재로드한 셰이더/compute와 실제 SpriteRenderer로 Point/Bilinear × Default/Tile UV × 텍스처 알파255/254/0을 검사한다. 자기 픽셀 유지·뒤쪽 픽셀 탈락·전체 세기(알파0 포함)·가리개 픽셀 합계가 기대값과 일치했다. 게임을 실행한 검증은 아니다.

추가 DEV 빌드는 `AutoTest.cs:962`의 기존 `DecoBake.PhReport/SrcCheck/SrcDebug/SrcFrames/SrcActiveSum` 참조 누락으로 실패했다. HEAD의 csproj·AutoTest.cs·StutterFix.cs를 별도 `out/zcull-dev-baseline` 경로에서 빌드해 같은 9개 오류를 재현했다. zcull 범위 밖의 기존 코드에는 변경하지 않았다. 인계문에서 요구한 두 Player 구성은 통과했다.

## Claude가 이어서 실행

`E:\StutterFixWork\zcull_run.py` 생성 완료. 저장소 사본은 `tools/zcull/zcull_run.py`. 6개 steps JSON도 `night-results/zcull-<맵>-steps.json`에 준비했다.

| 맵 | 시점 |
|---|---:|
| hl | 50초 |
| dd | 55초 |
| s7 | 120초 |
| ar | 262초 |
| wf | 260초 |
| sh | 88초 |

```powershell
# Claude 실행 전용: GameLock 및 기존 실행기의 설치/복원 절차를 사용한다.
python E:/StutterFixWork/zcull_run.py
```

맵별 `.txt`·원본 `.log`·실행기 `-launcher.txt`를 남기고 `zcull-summary.tsv`로 표를 만든다. 실행 종료 코드·로그·완료 마커·파일 갱신을 검사하며, 누락/실패 시 집계에 `MISSING_OR_FAILED`와 종료 코드1을 남긴다. `--prepare-only`만 실행했고 게임 실행 분기는 호출하지 않았다. 빌드·재현 명령은 `tools/zcull/README.md` 참고.
