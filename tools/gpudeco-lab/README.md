# gpudeco 독립 랩

게임을 열지 않는 Unity **6000.3.10f1 / D3D11** 검사 프로젝트다. 소스는 이 폴더에 두고 `prepare.ps1`이 무시되는 `project/` 아래에 Editor 프로젝트를 만든다. 모드의 제품 Compile 목록에는 랩 코드를 넣지 않는다. `DOTween.dll`은 로컬 CPU 정답용 참조로만 복사하며 번들에 넣지 않는다.

현재 **Editor 라이선스 오류(exit 198)**로 실제 GPU 실행·번들은 미검증이다. 오프라인 C# / HLSL 컴파일 통과는 GPU 충실도나 성능 통과를 뜻하지 않는다.

```powershell
& tools/gpudeco-lab/prepare.ps1
unity run tools/gpudeco-lab/project --editor-version 6000.3.10f1 --editor-path 'E:\Unity\Editor\6000.3.10f1\Editor\Unity.exe' --timeout 240 --log-file 'E:\StutterFixWork\wt-gpu\tools\gpudeco-lab\evidence\smoke-editor.txt' --no-tail -- -force-d3d11 -executeMethod GpuDecoLab.Smoke
```

`Smoke`는 이징 101,475표본(0..31·36 × 3조건 × 1,025시각), 합성 장식 2,000개·구간 20,000개, 36바이트 Raw 메시 버퍼 쓰기와 그리기를 검사하도록 작성했다. 표 평가와 정점·색 오차를 출력한다. CPU 제출·동기 readback 시간은 GPU timestamp와 구분한다. 현재 이 숫자들은 **시험 입력 규모**이며 실행 결과가 아니다. 하나의 메시만 쓰는 합성 부하라 실제 구간 약 1,000개의 제출 비용을 대표하지 않는다.

실제 입력은 **이미 게임 실행 권한을 가진 실행자**가 `tools/optimization/gpudeco-steps.py --chart <Arche *FULL.adofai> --output <steps.json>`으로 만든 steps를 실행해 얻는다. 이 생성기는 JSON만 쓴다. `--export-dir`로 새 출력 폴더를 지정하며 기존 파일은 덮어쓰지 않는다. 262·270·276초에 800개 미만이면 export가 OWNED_LOW를 기록하고 시험을 중단한다. 현재 단계에는 G1 렌더러 연결이 없어 마지막 결과는 `ERROR stage=export-lab`이다.

실제 export가 세 개 모이면 다음 명령으로 랩에서만 읽는다.

```powershell
unity run tools/gpudeco-lab/project --editor-version 6000.3.10f1 --editor-path 'E:\Unity\Editor\6000.3.10f1\Editor\Unity.exe' --timeout 240 --log-file 'E:\StutterFixWork\wt-gpu\tools\gpudeco-lab\evidence\verify-editor.txt' --no-tail -- -force-d3d11 -executeMethod GpuDecoLab.Verify -gpudecoInput 'E:\StutterFixWork\wt-gpu\tools\gpudeco-lab\exports'
```

각 파일은 구운 표 fingerprint, raw/tb 시각, 카메라, Dormancy 활성 수, 교집합 후보별 16채널 구간·논리값, 실제 SpriteRenderer 정점·색, 코너/UV/삼각형, flip, 2축 텍스처 배율, 피벗, 자식 체인 변환 기저, 배치 종류, 시차·회전/배율 잠금, 정렬 키와 재질/텍스처 ID를 담는다. 반복 타일·마스크·부모 추종 등 ld2a 제외 범위는 후보에서 빠진다. export는 소유권이나 Transform을 변경하지 않는다. 실제 소유권 전환을 검증한 자료로 간주하지 않는다.

Verify는 실제 논리 필드와 GPU 값을 16채널별 최대·평균·초과 수로 비교한다. 기하 채널은 다른 실제 채널을 고정한 채 해당 채널만 대입해 정점 이동을 화면 픽셀로 환산한다. 실제 월드 정점·최종 렌더러 색도 따로 비교한다. Flash/custom 트랙은 장식 전체를 원본 경로 후보로 분류하고, 남는 수가 800 미만 또는 활성 대비 70% 미만이면 중단한다. 실제 이징 분포는 아직 없다. 시작 프레임 위상 자료는 별도로 필요하다.

번들 제작 진입점은 `GpuDecoLab.Bundle`이다. `BuildPipeline.BuildAssetBundles`로 Windows64/D3D11 compute만 빌드하고, 같은 Editor에서 `AssetBundle.LoadFromFile`→`LoadAsset<ComputeShader>`→이징 재검사를 수행하도록 작성했다. 출력은 `evidence/bundle/`이다. 실제 export G0, 번들 생성/로드, 시작 위상, 여러 구간 부하가 확인되기 전에는 게임 연결을 진행하지 않는다. 향후 게임 연결에서 게임 `Sprites/Tile`·재질 인스턴스·구간 순서·Dormancy 소유/복구를 별도로 구현하고 G1을 검증해야 한다.

게임/Editor 실행 없이 가능한 검사:

```powershell
dotnet build tools/gpudeco-lab/compilecheck/CompileCheck.csproj -c Release
py -3 tools/gpudeco-lab/check-shader.py
dotnet run --project tools/gpudeco-lab/data-check/DataCheck.csproj -c Release
py -3 tools/optimization/test-gpudeco-steps.py
```
