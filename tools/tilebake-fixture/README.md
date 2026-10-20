# tilebake 오프라인 검사

`dotnet run --project tools/tilebake-fixture -c Release -- --bench`

실제 `FloorBake.Core.cs`/`DecoBake.Core.cs`와 설치된 게임 DOTween EaseManager를 링크해 교체·길이 0·기준 상대·NaN 축·제외·3,465개 중간 이징값을 검사한다. 게임을 실행하거나 설치 파일을 바꾸지 않는다.

합성은 8,171/90,000 타일, 각 4,097 이벤트다. 첫 이벤트는 전체 타일, 이후 이벤트는 각각 32개 타일을 대상으로 한다. 컴파일/입력 생성/대략 payload/GC 전 heap 변화/평가 중앙·p99를 구분한다. .NET 8 합성이며 실맵·Unity Mono·최악 fanout 성능을 나타내지 않는다.

상세 계약/제외/실게임 실행 순서/API 설계는 [결과 보고서](../../docs/handoff/codex-tilebake-20261020-result.md)를 참고한다.
