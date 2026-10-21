# gpudeco 독립 랩

게임을 열지 않는 **Unity 6000.3.10f1 / D3D11** 프로젝트다. prepare.ps1은 무시되는 project/에 소스와 내장 모듈 참조를 구성한다. 로컬 DOTween.dll은 CPU 정답 참조이며 번들에 넣지 않는다. 제품 Compile 목록에는 랩이 들어가지 않는다.

최신 경로는 **소스별 실제 float Rec.Pos + 실제 현재 구간을 받는 export v2**다. 독립 합성32,000채널·8,000정점 시험이 통과했고 정점 최대0.0000309467px, RGBA 최대0.0000002384였다. 최신 실제 run4 파일3개는 v1·sources0이므로 새 방식의 실제 G0/G1은 미측정이다. 이전81.50/65.62px는 과거 입력의 곡 시계 경로 수치이며 최신 run4를 새로 평가한 값이 아니다. [랩 결과](../../docs/handoff/codex-gpudeco-lab-result.md)를 본다.

v2 export는 gpudeco prepare 뒤 실제 FastMove.Run에서 생성한 Rec만 관찰한다. export 때 현재 eventTweens의 실제 구간/진행값을 읽고, 같은 소스의 진행값이 다르면 중단한다. 완료 끝값은 보존하고 미관찰/native/direct/미시작은 표 폴백으로 센다. v1은 읽되 v2로 재저장하거나 새 실제 G0의 근거로 쓸 수 없다. OWNED_LOW는 awake≥800이고 소유율<70%일 때만이며276초는 보조다. 실제 v2 G0 전에는 새 번들/게임 연결을 하지 않는다.

기존 이징·합성 정점·Raw 메시·번들 재로드 및 **80메시×15서브메시**의 통과는 합성 범위다(81 dispatch 평균0.1522106ms, 합성 픽셀차이0; 이전 측정). 실제 G0/G1 성공을 뜻하지 않는다. 새 번들/게임 적재는 실제 v2 G0 전이라 진행하지 않았다. 이전 비용/서브메시 시험을 그대로 되풀이하지 않는다.

새 `GpuDecoLab.Anchors`는 합성 v2 파일 왕복, CPU↔GPU 채널/Raw 정점, 완료 끝값, 곡 시각 변화에 대한 정지 불변성, 한 소스만 진행시키기, Actual 정답 입력 독립성, 기존 곡 시계 음성 대조를 검사한다. 257/2,000개 업로드+1dispatch 비용은0.0075288/0.0132740ms였으며 복사run0·GPUtimestamp없음·게임 비용 제외다. Anchors에 `-gpudecoInput <v1 폴더>`를 주면 실제 v1 입력 거부 음성 대조도 한다. Verify에는 실제 v2 폴더를 준다.

성공한 에디터 직접 호출 형태다. unity run·로그인 대신 이 경로를 쓴다. GPU 시험에 -nographics를 넣으면 Null Device로 실패한다. 각 실행은 5분 안쪽으로 제한하고, 종료를 확인한 후 다음 명령을 실행한다.

~~~powershell
& tools/gpudeco-lab/prepare.ps1
$lab = 'E:\StutterFixWork\wt-gpu\tools\gpudeco-lab'
$editor = 'E:\Unity\Editor\6000.3.10f1\Editor\Unity.exe'
& $editor -batchmode -quit -force-d3d11 -projectPath "$lab\project" -executeMethod GpuDecoLab.Anchors -logFile "$lab\evidence\anchors-editor-next.txt" | Out-Null
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

Verify와 Audit는 GpuDecoAudit.cs의 같은 본문을 사용한다. 실제 논리값·월드 정점·최종 색과 GPU 값을 채널별로 앵커 전후 대조하고 국소 위상ms와 프레임 평균을 남긴다. 기존 전역 시계201후보는 다시 실행하지 않는다. Flash/custom은 원본 후보로 제외하고 awake≥800에서 잔여 소유율70% 미만이면 중단한다. v1/실제 소스·프레임 표본 누락/G0/소유 실패는 exit1이다. 모든 실제 G0가 통과해도 게임 픽셀 없이는 G1 PASS를 출력하지 않는다. 실행 실패 시 이전 verify.txt가 남을 수 있으므로 새 Editor 로그와 결과 갱신을 함께 확인한다.

파일에는 16채널 표·실제 논리값/색·월드 정점·코너/UV/삼각형·변환 기저·잠금/시차·카메라·시각·정렬/재질 식별자가 들어간다. v2는 Source/Replacement·실제 Rec 시작 구간/진행값/트리거 프레임·최근 프레임/tween dt 평균을 추가한다. 과거 dt 전체 이력은 없다. v1에는 이 정보가 없어 앵커 시험을 거부한다. export는 원본 소유권이나 Transform을 변경하지 않는다. 실제 게임의 시작 위상·픽셀·소유/복구는 별도 검증 사항이다.

게임/에디터를 실행하지 않는 검사:

~~~powershell
dotnet build tools/gpudeco-lab/compilecheck/CompileCheck.csproj -c Release
py -3 tools/gpudeco-lab/check-shader.py
dotnet run --project tools/gpudeco-lab/data-check/DataCheck.csproj -c Release
py -3 tools/optimization/test-gpudeco-steps.py
~~~
