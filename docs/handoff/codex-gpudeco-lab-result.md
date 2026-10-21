# gpudeco 독립 랩 결과 — 2026-10-21

**선택: Unity 6000.3.10f1 AssetBundle 경로. 현재 실행 불가: Editor가 라이선스 확인 단계에서 exit 198로 종료했다. G0 GPU 실측과 번들 생성은 미완료다.**

선택 근거는 `fsr/BuildFsr.cs.txt`의 `BuildPipeline.BuildAssetBundles`와 `docs/shader-research.md`의 기존 번들 적재 구조다. 설치 실행 파일 `E:/Unity/Editor/6000.3.10f1/Editor/Unity.exe`가 존재하며 실행 로그도 버전 `6000.3.10f1`을 확인한다. 새 번들 호환성까지 확인했다는 뜻은 아니다. 네이티브 `sfnative.dll`은 변경하지 않았다.

## 실행 결과

근거 폴더: [tools/gpudeco-lab/evidence](../../tools/gpudeco-lab/evidence/).

|항목|실제 결과|
|---|---|
|Editor 시작|실패, 종료 코드 **198**|
|실패 위치|자산 임포트/fixture 실행 전 라이선스 초기화|
|오류|`Access token is unavailable`, `com.unity.editor.headless was not found`, 유효 라이선스 없음|
|GPU 이징 표본|**0**, 최대/평균 오차 미측정|
|GPU ↔ CPU Table.Eval 표본|**0**, 채널별 최대/평균 오차 미측정|
|실제 논리값·SpriteRenderer 정점 대조|**0**, 위치·회전·크기·색·화면 픽셀 오차 미측정|
|GPU dispatch / timestamp / 실제 구간 부하|모두 미측정|
|번들 제작·로드|**0회 완료**|
|시작 위상 / G1 픽셀 / G2 FPS|모두 미측정|
|랩 C# 정적 컴파일|**오류 0, 경고 0**, 실제 Editor 6000.3.10f1 참조 DLL 사용|
|HLSL SM5 컴파일|**2/2 통과**: EaseProbe **11,096B**, Evaluate **182,852B**|
|진단 파일 계약 fixture|**14/14 통과**|

첫 샌드박스 실행은 Package Manager IPC 연결 오류로 종료했다. 권한 검토를 거쳐 같은 독립 랩 명령을 다시 실행했으나 위 라이선스 오류가 확인됐다. 계정 로그인·라이선스 활성화·Editor 설치를 시도하지 않았다. 같은 명령을 반복해 통과 수치를 찾지 않았다. 로그: [smoke-editor.txt](../../tools/gpudeco-lab/evidence/smoke-editor.txt).

## 준비한 코드와 남은 범위

- `GpuDeco.compute`: 16채널 이진 탐색, double 시각의 hi/lo 분할 입력, DOTween ID 0..31·36 이징, scale의 Target/End 구분, 시차·2축 텍스처 배율·피벗/자식 변환·회전/배율 잠금, Float32 위치/색/UV **36바이트 Raw 정점 버퍼** 쓰기. Flash/custom은 원본 후보로 남긴다. 실제 표의 이징 분포가 없어 전체 지원률은 미측정이다.
- `GpuDecoLab.cs`: 실제 export 3파일 검증, 채널별 값/화면 환산·실제 월드 정점/최종 색 비교, 800 미만/70% 미만 중단, 번들 제작/재로드 진입점. 정적 컴파일만 검증했다. 이 코드의 실행 경로와 GPU 결과는 미검증이다.
- 합성 Smoke의 예정 입력은 **101,475 이징 표본**, 장식 **2,000개**, 구간 **20,000개**, 메시 **1개**다. 이들은 결과 수치가 아니다. 실제 약 1,000구간 부하/재질 순서, 게임 `Sprites/Tile` 픽셀을 증명하지 않는다.
- `GpuDecoData.cs`: 버전 1 진단 형식. 실제 export가 없어 파일 크기·내보내기/맵 열기 준비시간·실제 소유 교집합 수는 미측정이다.

재개 절차와 명령은 [랩 README](../../tools/gpudeco-lab/README.md)에 있다. 유효한 Editor 라이선스와 권한을 가진 실행자가 만든 실제 export가 필요하다. 이 선행 조건을 해결하기 전에는 게임 GPU 소유/그리기 연결로 진행하지 않는다.

`GPUDECO_G1=ERROR stage=lab reason=editor_license_exit_198_and_real_export_missing`
