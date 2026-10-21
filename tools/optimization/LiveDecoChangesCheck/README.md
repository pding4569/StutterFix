# 변경 통지 오프라인 검사

게임·Unity 엔진·Harmony 패치를 실행하지 않는다. `LiveDecoChanges.cs`를 직접 연결하고,
렌더러의 Queue/ObservingWrites/HasOwnedWatcher/BeforeWrite/AfterWrite는 본문을 그대로 추출한다.

```powershell
py -3 tools/optimization/LiveDecoChangesCheck/extract.py
dotnet run --project tools/optimization/LiveDecoChangesCheck -c Release
```

원래 setter 호출·인자·예외·IL 레이블/예외 구간을 보존하면서 같은 값의 통지만 생략하는지,
실제 값 변경과 소유 중인 stale Transform 쓰기는 계속 통지하는지 검사한다.
`forceRenderingOff`는 같은 true 값도 외부 소유 경계이므로 생략하지 않는다.
자기 장식의 Behaviour.enabled는 Before/After 모두 큐에 넣지 않고 외부 스크립트·shape 쓰기는 유지한다.
10000회 반복 경로의 관리 할당도 검사한다. 엔진 getter 비용·실게임 무할당·FPS·픽셀·수명 증명은 아니다.

음성 대조는 `extract.py <renderer.cs>`와 `-p:ChangesSource=<absolute path>`로
생산 파일을 바꾸지 않고 넣는다. 각 대조는 빌드가 성공한 뒤 의도한 assertion에서 실패해야 한다.
마지막에는 기본 소스를 다시 추출하고 재검증한다.
