# 단일 제출 오프라인 검사

저장소 루트에서 `py -3 tools/optimization/SingleSubmitCheck/extract.py` 후
`dotnet run --project tools/optimization/SingleSubmitCheck -c Release`를 실행한다.

실제 `GpuDecoCopy.cs` 전체와 `GpuDecoGpu.RecordLiveEvaluation`을 추출한다.
네이티브 함수와 Unity API만 managed adapter로 바꾸며 게임 DLL을 로드하지 않는다.
평가→정규화→콜백 순서, 프레임별 입력·마스크, 명령 객체 재사용,
기록 실패 전 큐 보존, 제출 거부, 자원 정리를 검사한다.
실제 Unity 명령 실행·GPU 값·픽셀·성능·복구 화면 동일성은 증명하지 않는다.
