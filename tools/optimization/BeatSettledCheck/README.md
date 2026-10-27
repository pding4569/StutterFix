# BeatSkipNoChange 헤드리스 검사

저장소 루트에서 실행:

```powershell
dotnet run --project tools/optimization/BeatSettledCheck -c Release
```

실제 `BeatFix.cs`와 개발자용 `BeatNoChangeSnapshot.cs`를 연결한다. Unity/Harmony/게임은
관리 코드 어댑터이며 설치된 게임 코드는 실행하지 않는다. Harmony 패치의 실제 부착은
검사하지 않으며, postfix를 게임의 필드 변경 직후 직접 호출한다.

4,096개 초기 조건마다 16개 이벤트를 진행하고, 정책 끔/켬을 각각 독립적인 전체 호출
대조군과 비교한다. 비교 대상은 타일/글로우 색과 스프라이트, 난수번호, 난수 상태,
실제 일을 하는 호출의 순서, 박자 프레임 번호다. 중복 listener, 스타일/난수 경로,
LightUp/Reset/TweenOpacity/여러 타일 StartEffect, 알파와 vfx 변경을 포함한다.

별도 검사는 256박자 재검사와 제외 타일 복귀, 정상 경로 불일치 0, 렌더러 읽기 0,
색/스프라이트/난수번호 스냅샷 검출, 64박자 검증 표본, 검증 실패 시 중단,
on/off/맵 재구축/unload, 외부 패치/훅 누락/파생 타일 폴백을 검사한다.

이는 상태기계의 동등성 검사다. Unity 화면의 픽셀 동일성, 실제 Harmony 연결,
FPS, 전곡 안정성은 게임 검증이 필요하다.
