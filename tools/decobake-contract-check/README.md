# 굽기 시간 계약 검사

`dotnet run --project tools/decobake-contract-check -c Release`

게임을 실행하지 않는 .NET 8 콘솔이다. 새 굽기 구현의 성공 검사가 아니라,
기존 프레임 이력 기반 출력과 곡 시각만 사용하는 출력 사이의 계약 충돌을 재현한다.

- `DecoAnim.cs`의 `Update`/`Step`, `DecoSim.cs`의 `DecoBatchEval.EvaluateRange`와 같은 float 누적식에 서로 다른 프레임 분할을 넣는다.
- 입력 float 델타들의 정확한 합은 같지만 누적 결과 비트는 다르다.
- 예약 시각 이후 첫 프레임에 시작하는 모형은 추가적인 시작 양자화 차이를 보인다. 이 모형은 게임의 스크립트 실행 순서를 측정한 것이 아니다.
- 위치 `LastPosition`의 상대 기준 캡처 → 이전 기록 `Kill(true)` → 새 시작값 캡처 순서를 별도로 예시한다.

근거는 `docs/handoff/bake-evidence/clock-read-sites.txt`와 결과 보고서에 있다.
Mono/Unity 실행 결과, GPU 정확도, 실제 맵 성능을 증명하지 않는다.

bake-6는 production `SongClock`도 직접 검사한다. `bake5early-evidence.txt`의 고정 12행은
raw 첫 시각으로 cold 초기화하고 actual/RecPos를 보정에 쓰지 않는다. 최대/RMS 오차와
프레임 환산을 출력한다. 600초@480FPS 균등 ±6ms/버퍼 계단형 합성은 최대 2프레임과
말미 평균 오차를 검사하며, 정지/재개/seek/pitch/스냅샷 세대 변경/활성 커서 되감기도 확인한다.
`--bench`는 희소 합성 evalAll/evalActive 비용을 덧붙인다. 실게임 성능 증거가 아니다.
