# HitMeterDirectCheck

게임을 실행하지 않고 설치된 `DOTween.dll`과 제품의 `HitMeterFade.cs`를 직접 연결하는 검사다.

```powershell
dotnet restore tools/optimization/HitMeterDirectCheck -p:NuGetAudit=false --ignore-failed-sources
dotnet run --project tools/optimization/HitMeterDirectCheck --no-restore -v q
```

다른 설치 위치에서는 빌드 참조와 게임 IL 입력을 모두 지정한다.

```powershell
dotnet run --project tools/optimization/HitMeterDirectCheck -p:GameManaged="D:/path/to/Managed" -- "D:/path/to/Managed"
```

검사 범위:

- 설치된 `Assembly-CSharp.dll` 메타데이터로 Show의 Kill/OnKill/DOColor 각 2곳, AddHit의 회전/이동 각 1곳, Reset의 전역 Kill 2곳을 확인한다.
- Show/AddHit의 관련 호출을 재현한 메서드에 **제품 트랜스파일러**를 적용하고, 바뀐 IL을 DynamicMethod로 실행한다. 기존 페이드만 적용한 기준 경로와 직접 종료를 추가한 경로를 비교한다.
- 2,304개 시드 × 120프레임. 앞 96프레임의 타격 후 24프레임은 남은 트윈을 진행시킨다. 직선/곡선 손, 프레임당 여러 타격, 저빈도 타격, 간격 0/1⁄240/1⁄60/0.071/0.3초, 눈금 재사용, 외곽선 유무, 길이 0, 투명 시작색, 풀 재활용, Reset, 설정 전환, Shutdown을 포함한다.
- 프레임마다 손 위치/회전, 눈금/외곽선 RGBA 및 CanvasRenderer 알파를 float 비트로 대조한다. `HitMeterFade` ON/OFF도 포함한다.
- 별도 16개 계약 검사: 기본 OFF, 치환 수, 예상 밖 Show 형태의 원래 경로 유지, 준비된 눈금의 검색 0, 다른 모드의 같은 ID, ID 변경, 완료/재활용, 설정 전환, 종료 정리, 손의 콜백/일시정지/길이/시간 배율 보호.
- 기존 Fade 회귀 256개 시퀀스 × 24프레임. 원래 DOColor와 유효 알파를 비교하며 허용 오차는 1e-6이다. 이 기존 최적화 자체는 정점 색과 렌더러 알파가 다르므로 비트 일치를 요구하지 않는다.

Unity 객체·수학·UI 단축 함수는 검사 전용 관리 코드다. 곡선 손의 Quaternion.Slerp는 System.Numerics를 사용한다. 실제 DOTween의 생성/시작값 읽기/업데이트 순서/이징/플러그인/종료/재활용은 그대로 실행한다. 게임의 UI 단축 함수 본문과 같은 DOColor/DOAnchorPos 어댑터를 사용한다.

게임에 포함된 Harmony의 패치·IL 읽기는 .NET 8에서 Mono 전용 API 때문에 실행되지 않는다. 따라서 작은 IL reader/emitter를 사용하되, 실제 Harmony `CodeInstruction`과 제품 트랜스파일러는 그대로 사용한다. 게임 내 Harmony 설치 성공, Unity 네이티브 계산·픽셀, Mono, FPS는 이 검사로 증명하지 않는다.

가짜 UnityEngine DLL은 이 검사 폴더의 bin/obj에만 생성된다. 제품 프로젝트는 이 출력 폴더를 기본 파일 후보에서 제외해 실제 Unity 참조와 섞이지 않게 한다.

최신 실행 결과와 알려진 동작 차이는 [결과 인계 문서](../../../docs/handoff/hitmeter-direct-result.md)에 있다.
