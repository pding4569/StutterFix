# gpudeco 독립 랩

게임을 열지 않는 **Unity 6000.3.10f1 / D3D11** 프로젝트다. prepare.ps1은 무시되는 project/에 소스와 내장 모듈 참조를 구성한다. 로컬 DOTween.dll은 CPU 정답 참조이며 번들에 넣지 않는다. 제품 Compile 목록에는 랩이 들어가지 않는다.

최신 실제 export 3개는 **262·270초 주 판정 G0 실패 / 276초 보조 측정 통과**다. `OWNED_LOW`는 awake≥800이고 소유율<70%일 때만이다. 276초 소유17/awake24의 정점 최대·평균은0px이며 주 판정에 포함하지 않는다. 현재 곡 시각 표의 한계를 기록하고 게임 연결을 중단했다. 주 판정 정점 최대 오차81.501349/65.618709px, 평균1.920398/2.890432px다. 전역 시계201후보에서도 통과하지 못했다. [랩 결과](../../docs/handoff/codex-gpudeco-lab-result.md)를 본다.

기존 이징·합성 정점·Raw 메시·번들 재로드 및 **80메시×15서브메시**의 통과는 합성 범위다(81 dispatch 평균0.1522106ms, 합성 픽셀차이0; 이전 측정). 실제 G0/G1 성공을 뜻하지 않는다. 이번 HLSL 수정 뒤 합성 수치 회귀는 통과했고, 새 번들/게임 적재는 실제 G0 실패로 진행하지 않았다. 이전 비용/서브메시 시험을 그대로 되풀이하지 않는다.

성공한 에디터 직접 호출 형태다. unity run·로그인 대신 이 경로를 쓴다. GPU 시험에 -nographics를 넣으면 Null Device로 실패한다. 각 실행은 5분 안쪽으로 제한하고, 종료를 확인한 후 다음 명령을 실행한다.

~~~powershell
& tools/gpudeco-lab/prepare.ps1
$lab = 'E:\StutterFixWork\wt-gpu\tools\gpudeco-lab'
$editor = 'E:\Unity\Editor\6000.3.10f1\Editor\Unity.exe'
& $editor -batchmode -quit -force-d3d11 -projectPath "$lab\project" -executeMethod GpuDecoLab.Smoke -logFile "$lab\evidence\smoke-editor-next.txt" | Out-Null
$LASTEXITCODE
~~~

Smoke는 이징 101,475개, 합성 장식 2,000개·표 구간 20,000개, 36바이트 Raw 정점·CPU 정답·그리기를 검사한다. Runs는 같은 입력을 **독립 Mesh 1,000개**로 나눠 GPU 복사·그리기·제출을 검사한다. UInt32 인덱스를 쓰고 GPU 버퍼를 얻기 전에 CPU 사본을 폐기한다. CommandBuffer를 재사용해도 1,000개 GPU copy dispatch가 필요하며 제출 예산 미달이다. 그대로 성능 성공을 기대해 반복하지 않는다.

~~~powershell
& $editor -batchmode -quit -force-d3d11 -projectPath "$lab\project" -executeMethod GpuDecoLab.Runs -logFile "$lab\evidence\runs-editor-next.txt" | Out-Null
& $editor -batchmode -quit -force-d3d11 -projectPath "$lab\project" -executeMethod GpuDecoLab.Bundle -logFile "$lab\evidence\bundle-editor-next.txt" | Out-Null
~~~

새 `GpuDecoLab.Submeshes`는 80개 Raw 버퍼(25장식·15서브메시씩)에 평가 결과를 복사한다. 500회 CPU 제출, 실제 버퍼 readback, 분리/통합 메시 픽셀, 원본 SpriteRenderer/같은 정렬 키/비소유 중간 경계의 6가지 생성 순서, 재질 역전 음성 대조, Camera.Render wall을 검사한다. `Sprites/Default`의 합성 장면이며 게임 셰이더와 FPS 검증은 아니다. 비용 측정에서 바뀐 시각을 복원한 뒤 픽셀 정답과 대조한다.

~~~powershell
& $editor -batchmode -quit -force-d3d11 -projectPath "$lab\project" -executeMethod GpuDecoLab.Submeshes -logFile "$lab\evidence\submeshes-editor-next.txt" | Out-Null
~~~

Bundle은 Windows64/D3D11 번들 생성 후 같은 에디터에서 LoadFromFile → LoadAsset<ComputeShader> → 101,475개 이징 대조를 한다. **com.unity.modules.assetbundle 내장 모듈 참조**가 필요하다. 생성 파일만으로 성공 처리하지 않는다. 게임에서의 재로드는 미검증이다.

실제 입력은 별도 권한을 가진 실행자가 아래 생성기의 JSON을 실행해 얻는다. 생성기는 게임을 실행하지 않는다.

~~~powershell
py -3 tools/optimization/gpudeco-steps.py --chart '<Arche *FULL.adofai>' --output '<steps.json>' --export-dir '<새 출력 폴더>'
~~~

steps는 timeout 420을 쓴다. 편집 open은 PrepVfx를 호출하지 않으므로 **select 0 → play → gpudeco scheduled → pause**로 실제 첫 준비를 시작/관찰한 뒤 wait 60 두 번과 state/ready를 확인한다. 준비 후 resume/stop하고 262·270·276초를 seek/export한다. gpudeco prepare는 open 이전부터 첫 export까지 **300초 예산**을 추적한다. 기존 외부 시험의 실제 첫 export는182998.1268ms였다. 기본 출력은 매 생성마다 고유한 `exports/run-<UUID>/`다. 명시한 출력 폴더에 `.gpudeco`가 있으면 생성 단계에서 거부하며 런타임 CreateNew도 유지한다. 기존 고정 경로 JSON은 재사용하지 않는다. awake≥800에서 소유율70% 미만이면 OWNED_LOW로 중단하며, awake<800이면 보조 측정을 계속한다. GPU on/G1 PASS 단계는 없다. 아래는 재현 설명이며, 현재 한계를 확인한 같은 게임 시험을 다시 요청하는 절차가 아니다.

확보된 실제 262·270·276초 export 대조 재현(현재 판정은 의도한 **exit1**, 게임 실행 없음):

~~~powershell
& $editor -batchmode -quit -force-d3d11 -projectPath "$lab\project" -executeMethod GpuDecoLab.Verify -gpudecoInput '<export 폴더>' -logFile "$lab\evidence\verify-editor-next.txt" | Out-Null
~~~

Verify와 Audit는 `GpuDecoAudit.cs`의 같은 본문을 사용한다. 실제 논리값·월드 정점·최종 색과 GPU 값을 채널별 대조하고, CPU 표·실제 필드·GPU 포즈의 오차 및 전역 시계 진단을 분리한다. Flash/custom은 원본 후보로 제외하고 awake≥800에서 잔여 소유율70% 미만이면 중단한다. 실제 G0/소유 실패를 exit1로 반환한다. 역산 시각·201개 시계 후보는 읽기 전용 진단이며 통과 판정/제품 보정에는 사용하지 않는다. 실행 실패 시 이전 `verify.txt`가 남을 수 있으므로 새 에디터 로그의 `primary=2/2`와 함께 결과의 갱신을 확인한다.

파일에는 16채널 표·실제 논리값/색·월드 정점·코너/UV/삼각형·변환 기저·잠금/시차·카메라·시각·정렬/재질 식별자가 들어간다. 현재 v1에는 Source/Replacement·실제 트윈 시작/진행 상태·과거 dt가 없다. export는 원본 소유권이나 Transform을 변경하지 않는다. 시작 위상은 스냅샷 도달 지연으로 판정하지 않고 UNMEASURED로 기록한다. 게임 픽셀·소유/복구도 별도 검증 사항이다.

게임/에디터를 실행하지 않는 검사:

~~~powershell
dotnet build tools/gpudeco-lab/compilecheck/CompileCheck.csproj -c Release
py -3 tools/gpudeco-lab/check-shader.py
dotnet run --project tools/gpudeco-lab/data-check/DataCheck.csproj -c Release
py -3 tools/optimization/test-gpudeco-steps.py
~~~
