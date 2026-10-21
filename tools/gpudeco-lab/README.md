# gpudeco 독립 랩

게임을 열지 않는 **Unity 6000.3.10f1 / D3D11** 프로젝트다. prepare.ps1은 무시되는 project/에 소스와 내장 모듈 참조를 구성한다. 로컬 DOTween.dll은 CPU 정답 참조이며 번들에 넣지 않는다. 제품 Compile 목록에는 랩이 들어가지 않는다.

현재 이징·합성 정점·Raw 메시 그리기·번들 재로드와 **80메시×15서브메시**가 통과했다. 서브메시 경로의 81 dispatch 제출 평균은 0.1522106ms, 합성 픽셀 차이는 0이다. 실제 export가 없어 게임 G0/G1은 미검증이다. 이전 1,000메시의 제출 예산 실패와 구분한다. [랩 결과](../../docs/handoff/codex-gpudeco-lab-result.md)를 본다.

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

steps는 timeout 420을 쓴다. 편집 open은 PrepVfx를 호출하지 않으므로 **select 0 → play → gpudeco scheduled → pause**로 실제 첫 준비를 시작/관찰한 뒤 wait 60 두 번과 state/ready를 확인한다. 준비 후 resume/stop하고 262·270·276초를 seek/export한다. gpudeco prepare는 open 이전부터 첫 export까지 **300초 예산**을 추적한다. 초과 시 실패하며 이 시간 안에 실제 준비된다는 실측 근거는 아직 없다. 기존 export를 덮어쓰지 않는다. 후보 800개 미만은 OWNED_LOW로 중단한다. GPU on/G1 PASS 단계는 없다.

실제 262·270·276초 export 세 파일이 모이면:

~~~powershell
& $editor -batchmode -quit -force-d3d11 -projectPath "$lab\project" -executeMethod GpuDecoLab.Verify -gpudecoInput '<export 폴더>' -logFile "$lab\evidence\verify-editor-next.txt" | Out-Null
~~~

Verify는 실제 논리값·월드 정점·최종 색과 GPU 값을 채널별 대조한다. Flash/custom을 원본 후보로 제외하고 잔여 800개 미만 또는 활성 대비 70% 미만이면 중단한다. 파일에는 16채널 표·실제 논리값/색·월드 정점·코너/UV/삼각형·변환 기저·잠금/시차·카메라·시각·정렬/재질 식별자가 들어간다. export는 원본 소유권이나 Transform을 변경하지 않는다. 시작 위상·게임 픽셀·소유/복구는 별도 검증 사항이다.

게임/에디터를 실행하지 않는 검사:

~~~powershell
dotnet build tools/gpudeco-lab/compilecheck/CompileCheck.csproj -c Release
py -3 tools/gpudeco-lab/check-shader.py
dotnet run --project tools/gpudeco-lab/data-check/DataCheck.csproj -c Release
py -3 tools/optimization/test-gpudeco-steps.py
~~~
