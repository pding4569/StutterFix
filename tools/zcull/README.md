# Z-cull 일회성 진단

`ZCull.cs`와 전용 번들은 DEV/AUTOTEST에만 포함한다. 게임에 설치하지 않고 다음 순서로 준비한다.

```powershell
& tools/zcull/build.ps1
dotnet build StutterFix.csproj -c Release -p:Edition=Player -p:AutoTestBuild=1
dotnet build StutterFix.csproj -c Release -p:Edition=Player
dotnet run --project tools/zcull-fixture/ZCullFixture.csproj -c Release
Copy-Item tools/zcull/zcull_run.py E:/StutterFixWork/zcull_run.py
python E:/StutterFixWork/zcull_run.py --prepare-only
python tools/zcull/verify.py
```

번들은 기존 `tools/gpudeco-lab/README.md`의 **Unity 6000.3.10f1 직접 실행 / D3D11 / BuildPipeline.BuildAssetBundles / 재로드 검증** 방식을 따른다. 소규모 독립 프로젝트 `project/`를 생성하며, 진행 중인 에디터나 게임에는 연결하지 않는다. `-nographics`는 사용하지 않는다. `BuildZCull.Bundle`은 Windows64/D3D11 번들 생성 → 재로드 → 4패스·Reduce 커널 확인 → 실제 GPU 합성 12사례 검사를 수행한다. 전용 번들을 기존 제품용 GpuDeco/RunLod 번들과 분리해 일반 Player 유입을 막았다. `bundle/stutterfix_zcull`만 버전 관리 대상이다. 셰이더 변경 후에는 번들과 PlayerAuto DLL을 모두 다시 빌드한다.

Claude가 실행할 명령:

```powershell
python E:/StutterFixWork/zcull_run.py
# 일부만 실행 / 기존 결과만 집계
python E:/StutterFixWork/zcull_run.py --maps hl,dd
python E:/StutterFixWork/zcull_run.py --summarize
```

실행 분기는 기존 `run_locked.py` → `GameLock` → `run_json.py`를 사용한다. 이 분기는 게임 실행과 시험 DLL 설치/복원을 수행하므로 Codex 준비 단계에서 호출하지 않는다. `--prepare-only`는 JSON 준비만, `--summarize`는 기존 로그 읽기만 한다. 맵 경로·설정은 현재 `bench_v14.MAPS`, `CLEAN`, `CFG2["rlNR"]`에서 읽고, 시점만 인계문의 값으로 덮어쓴다. 결과는 `night-results/zcull-<맵>.txt`, 원본 `.log`, 실행기 `-launcher.txt`, 집계 `zcull-summary.tsv`다. 완료 마커·요약·실패 로그·이번 실행 파일 갱신 여부를 검사한다.

게임 명령은 `pause` → `wait 2` → `zcull census` → `resume`. `zcull census` 단계는 AsyncGPUReadback 완료까지 대기하며 실패 시 자동 시험을 실패 처리한다. 취소는 `zcull stop`, 모드 해제는 `Main.Unload`에서 자원을 정리한다. 실행 중 별도 렌더 진단을 겹치지 않는다.

측정값의 범위와 미검증 항목은 [결과 문서](../../docs/handoff/zcull-20261113-result.md)에 기록했다.
