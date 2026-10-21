# gpudeco 독립 랩

**최신 결과는 round9다.** 외부 run1의 두 v2 파일(262/270초)을 새56,364B번들로 대조했다. 큰 끝값 선형 보간에서 SM5 나눗셈과 중간 곱의 반올림을 고쳐270초 정점최대0.498907→0.00779543px·초과4→0,262초0.00401120px·초과0을 확인했다. 이전run6 완전한3파일 회귀도G0 PASS다. 최신276초는 수집되지 않았으며 최신두파일을 전체3시점판정으로 바꾸지 않는다. 수정후 게임G1은 미측정이다. [round9 결과](../../docs/handoff/codex-gpudeco-lab-result.md)를 본다.

`GpuDecoLab.Precision`은 선형 65,537표본·큰 끝값 보간·`-gpudecoInput` 폴더의 일부 실제 파일·이징·회전을 검사한다. `GpuDecoLab.BundleSnapshots`는 생성된 번들을 재로드해 같은 실제 파일의 G0를 검사한다. 둘 다 기존 직접 Unity.exe 호출 형태를 쓰며 게임을 실행하지 않는다. `Residual`은 일부 파일 진단으로 전체 게이트 판정을 출력하지 않고, 파일 중 G0 실패가 있으면 exit1이다. 다음 시험 준비물은 `evidence/round9-g1-steps.json`이며 생성만 했다. 아래 run6/round8 설명은 이전 근거다.

게임을 열지 않는 **Unity 6000.3.10f1 / D3D11** 프로젝트다. prepare.ps1은 무시되는 project/에 소스와 내장 모듈 참조를 구성한다. 로컬 DOTween.dll은 CPU 정답 참조이며 번들에 넣지 않는다. 제품 Compile 목록에는 랩이 들어가지 않는다.

최신 run6의 **v2 3파일 오프라인 G0는 통과**했다. 소유율262초81.0684%·270초79.9729%, 정점 최대0.00817266/0.00779688px·초과0, 276초 보조0px다. 원본 파일은 보존했다. 시계 없는 동적 표는 기존 메타데이터 소유 규칙으로 원본에 남긴다. [최신 결과](../../docs/handoff/codex-gpudeco-lab-result.md)를 본다.

run6 게임 G1은418684/136779픽셀·최대7/5로 실패했다. 이번에는 SpriteRenderer의 draw 색이 half-up UNorm8인 것을 랩에서 확인하고 **GPU CopyRun의 draw 색만 `floor(float(saturate(c)*255)+.5)/255`로 변환**했다. Evaluate의 논리색/G0와36B Float32레이아웃은 유지한다. Color32/HLSL round는 중간값에서 틀리므로 사용하지 않는다. Colors의36조건·256중간값/인접값6격자는 모두0픽셀, 실제3파일 GPU그리기색 바이트불일치0이다. Sprites/Default 랩이며 수정 후 게임 Sprites/Tile G1은 미측정이다. 새번들55748B 생성·재로드에서도 검사했다.

`GpuDecoGpu.cs`는 게임과 랩이 공유한다. 회전은 SM5 double 범위 축소/다항식 후 float quaternion을 쓴다. RTX4060Ti/D3D11에서 확인했으며 다른 GPU의 double 지원/비용은 별도다. 이전 회차8,200각도/대형 경계 검증을 통과했고 이번 새 번들에서도 회전 검사를 통과했다.

v2 export는 gpudeco prepare 뒤 실제 FastMove.Run에서 생성한 Rec만 관찰한다. export 때 현재 eventTweens의 실제 구간/진행값을 읽고, 같은 소스의 진행값이 다르면 중단한다. 완료 끝값은 보존한다. 새 pending scalar/color는 생성 시 시작값+실제 진행0을 내보내며 pending scale의 오래된 VStart는 거부한다. 이 pending 보완의 새 실제 export는 미측정이다. 시계 없는 현재 비상수 구간은 공통 소유 정책에서 제외한다. v1은 새 앵커 시험의 근거로 쓸 수 없다. OWNED_LOW는 awake≥800이고 소유율<70%일 때만이다.

AUTOTEST `gpudeco bundle <파일>` → 같은 일시정지의 export → `on`은 G0를 확인한 뒤 원본을 숨기고 Raw 구간/서브메시를 그리는 **정지 G1 진단**이다. `draw off`가 원본을 복구하며 재개/맵 변경/해제에도 복구한다. steps는 전체 화면의 잡음·A/B/A2·3시점 판정을 포함한다. 수정 후 게임 G1은 실행하지 않았다. 재생 중 소유/원본 평가 생략·재생 전1회 준비·실시간 성능은 미구현/미측정이며 정지 진단 준비 비용과 구분한다.

공용 실행기 추출 뒤 **80메시×15서브메시**를 재검증했다. 81dispatch CPU 평균0.1473152ms·묶음0.137851~0.167101ms, 복사 불일치0·합성 픽셀차이0이다. 게임 G1/엔진 비용의 증거가 아니며 GPU timestamp도 없다. 실패한1,000메시 경로는 반복하지 않는다.

`GpuDecoLab.Anchors`는 합성 v2 왕복, CPU↔GPU 채널/Raw 정점, 완료 끝값, 정지 시계 불변, 한 소스 분리, Actual 정답 입력 독립, 곡 시계 음성 대조를 검사한다. 공용 실행기의257/2000소스 업로드+1dispatch 비용은0.0078056/0.0123070ms이며 복사run0·GPUtimestamp없음·게임 비용 제외다. Anchors의 선택적 `-gpudecoInput`에는 v1만 주고, 실제 v2 대조는 Verify로 실행한다.

성공한 에디터 직접 호출 형태다. unity run·로그인 대신 이 경로를 쓴다. GPU 시험에 -nographics를 넣으면 Null Device로 실패한다. 각 실행은 5분 안쪽으로 제한하고, 종료를 확인한 후 다음 명령을 실행한다.

~~~powershell
& tools/gpudeco-lab/prepare.ps1
$lab = 'E:\StutterFixWork\wt-gpu\tools\gpudeco-lab'
$editor = 'E:\Unity\Editor\6000.3.10f1\Editor\Unity.exe'
& $editor -batchmode -quit -force-d3d11 -projectPath "$lab\project" -executeMethod GpuDecoLab.Anchors -logFile "$lab\evidence\anchors-editor-next.txt" | Out-Null
$LASTEXITCODE
~~~

Smoke는 이징 101,475개, 합성 장식 2,000개·표 구간 20,000개, 36바이트 Raw 정점·CPU 정답·그리기를 검사한다. Runs는 같은 입력을 **독립 Mesh 1,000개**로 나눠 GPU 복사·그리기·제출을 검사한다. UInt32 인덱스를 쓰고 GPU 버퍼를 얻기 전에 CPU 사본을 폐기한다. CommandBuffer를 재사용해도 1,000개 GPU copy dispatch가 필요하며 제출 예산 미달이다. 그대로 성능 성공을 기대해 반복하지 않는다.

`GpuDecoLab.Colors`는 실제 SpriteRenderer와 CPU half-up/GPU CopyRun 색을 비교한다. 36조건·6중간값 격자를 검사하고 기존Float32 및 managedColor32의 차이도 로그한다. 선택적 `-gpudecoInput <v2 폴더>`는 실제 export의 그리기 색 바이트와 GPU 버퍼를 대조한다. Bundle은 재로드한 ComputeShader에도 같은 색 검사를 실행하므로 소스 셰이더만 맞고 번들은 오래된 경우를 놓치지 않는다. 게임 G1 통과를 출력하지 않는다.

~~~powershell
& $editor -batchmode -quit -force-d3d11 -projectPath "$lab\project" -executeMethod GpuDecoLab.Colors -gpudecoInput '<실제 v2 폴더>' -logFile "$lab\evidence\colors-editor-next.txt" | Out-Null
~~~

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
# 자료 수집만 하려면 --export-only를 덧붙인다. 생성 자체는 게임을 실행하지 않는다.
~~~

steps는 timeout420·실제play 준비 관찰·wait60 두 번·첫 export300초 예산을 유지한다. 최신 외부 run6의 첫 export183007.7304ms다. 기본 출력은 매번 고유한 `exports/run-<UUID>/`이며 기존 파일은 거부한다. 기본 G1 모드는 새 번들을 지정하고262·270·276초 각각에서 export→잡음→A→on→B→draw off→A2→판정한다. 중앙 마스크는 쓰지 않는다. awake≥800 소유율70% 미만만 OWNED_LOW이며276초는 보조다. 현재 준비물은 `evidence/round8-g1-steps.json`이며 생성만 했다. draw 색과 번들이 달라진 시험이다.

확보된 최신 v2 세 파일 대조 재현(현재 **G0 PASS/exit0**, 게임 실행 없음):

~~~powershell
& $editor -batchmode -quit -force-d3d11 -projectPath "$lab\project" -executeMethod GpuDecoLab.Verify -gpudecoInput '<export 폴더>' -logFile "$lab\evidence\verify-editor-next.txt" | Out-Null
~~~

Verify와 Audit는 같은 본문을 쓰며 실제 채널·월드 정점·최종 색을 앵커 전후 대조한다. 전역 시계201후보는 반복하지 않는다. Flash/custom 및 실제 시계 없는 동적 구간은 원본 후보로 제외하고 잔여 소유율70%를 검사한다. v1/실제 소스·프레임 표본 누락/G0/소유 실패는 exit1이다. G0가 통과해도 게임 픽셀 없이는 G1 PASS를 출력하지 않는다. 새 Editor 로그와 결과 갱신을 함께 확인한다.

파일에는 16채널 표·실제 논리값/색·월드 정점·코너/UV/삼각형·변환 기저·잠금/시차·카메라·시각·정렬/재질 식별자가 들어간다. v2는 Source/Replacement·실제 Rec 시작 구간/진행값/트리거 프레임·최근 프레임/tween dt 평균을 추가한다. 과거 dt 전체 이력은 없다. v1에는 이 정보가 없어 앵커 시험을 거부한다. export는 원본 소유권이나 Transform을 변경하지 않는다. 실제 게임의 시작 위상·픽셀·소유/복구는 별도 검증 사항이다.

게임/에디터를 실행하지 않는 검사:

~~~powershell
dotnet build tools/gpudeco-lab/compilecheck/CompileCheck.csproj -c Release
py -3 tools/gpudeco-lab/check-shader.py
dotnet run --project tools/gpudeco-lab/data-check/DataCheck.csproj -c Release
py -3 tools/optimization/test-gpudeco-steps.py
~~~
