### round59b~61 reload 원인·수정 검증 완료

round59b는 GPU/DecoBake를 켜지 않은 대조에서도 새DLL reload 후 새맵open 중 StackOverflow를 재현했다. 최초C00000FD mini dump의 JIT 반환주소0x169f81e560b가 80바이트 간격으로 반복된다. 바로 앞call r11에서 다시 같은 함수로 들어가는 스택이 관측됐다. 최종AV 때의 Steam/Mono 스택을 원인으로 삼지 않았다. 정상quit이 아니라 비정상종료이며 원본9상태는 종료 후9/9복원했다.

캐시의 어셈블리 키에 `StutterFix1:734869a4-aaf5-43af-963e-86bc927bb5b5`처럼 reload 사본이 포함될 수 있음을 확인했다(실제 실패59b는MVID734869a4-aaf5-43af-963e-86bc927bb5b5). 당시 TileCull.Skip은 `an == "StutterFix"`만 제외했다. UMM이 이름을 바꾼 자기 모듈을 순회하면 TileCull.SetEnabled/GetEnabled의 Renderer.enabled 호출까지 자기 래퍼로 바꿔 재귀할 수 있다. 원본디스크 DLL과 UMM이 다시 쓴 runtime token의 메서드 순서는 같다고 가정하지 않는다. offline token-map 결과는 정확한 runtime 메서드 식별로 사용하지 않는다.

round60 단일수정은 자기 어셈블리를 `StartsWith("StutterFix", Ordinal)`로 제외하고 최종patch 경계에서도 같은 검사를 한다. 기존게임/타모드의 바깥Renderer.enabled 중계와 설정/UI/정렬/충돌체는 그대로다. 명시적AUTOTEST `tilecullreloadcheck`만 Harmony의 실제외부패치가1개이상이고 자기모듈패치가0인지 검사한다. 새상시후크·매프레임패치목록스캔0이다. A/B는 AUTOTEST marker만 다르고 정상Player는 바이트동일이며 소스A를 되돌렸다. 각각3빌드오류0·LoadCheck277/229/277불일치0·관리Player연구제외다.

round61 실제3440×1440/144Hz/D3D11/동기0:

| 항목 | 구DLL A | 새DLL B |
|---|---:|---:|
| 모듈 이름 | StutterFix | StutterFix1 |
| MVID | 91e0cdf0-f01d-4e08-ab18-3e2c2500214c | b7d45269-a6da-490f-b446-fe97278835f1 |
| 자기 TileCull transpiler | 0 | 0 |
| 외부 TileCull transpiler | 189 | 189 |
| 재생10초 GPU소유 | 23547 | 23547 |
| native 제출/실행 | 5685/5685 | 5564/5564 |
| native 오류 | 0 | 0 |
| 표시한 Unity객체 해제 뒤 생존 | 0/47098 | 0/47098 |
| 옛native token조회불가 | 1/1 | 1/1 |

실제 A→B 교체·Main.Unload·새DLL 설치·새맵open·GPU재활성화를 마쳤다. 첫묶음84.5313초/새DLL자원검사10.0616초/재열기재활성61.3353초, 명령실패0·stability reopen_toggle통과·native/SF오류0·정상quit·설치8+probe9/9원본복원이다. ProcDump의 StackOverflow/AV dump0이다. 전체Unload가 기존정책대로 진행중Rec를 FinishAll하는 것은 향후궤적동일성 증명이 아니다. 이 reload 표본은10초의 실제GPU제출/비어있지않은자원검사이며 무거운구간 FPS대조나 전곡품질승인이 아니다. 과거58/58b/59b 실패는 보존한다.

round62는 다음 원인조사 후보다. source.MasterSlots.Remove의 탐색·뒤칸복사 상한을 명시적costprobe에서만 센다. IndexOf1회+동일인덱스RemoveAt으로 같은순서를 유지하며 진단끔은 원래List.Remove를 그대로 쓴다. 개별장식 Stopwatch0·새후크0·일반Player포함0이다. 탐색량 상한은 SIMD/실제CPU시간을 측정한 비교수가 아니다. 0초 자연재생에서210/219/221초 누적보고 차이를 확인해220.4초의 효과비용과 관련 있는지 먼저 본다. 원인 확정/성능개선 주장0, 순서보존인덱스 초안 채택0이다. 최신3빌드오류0·LoadCheck/연구제외를 통과했고 아직 게임결과 전이다.

무거운구간400FPS는 미달이다. 옛 clean262~276초 대조123.031607→156.765290FPS(+27.418713%), 56의260~270초는FG2/4에서158/130FPS다. 전곡384/370 또는 옛 FG끔445FPS를 무거운구간 FPS로 말하지 않는다. 다음 독립검증은 최신core HELLO 자연전곡이며 old40을 새DLL의 통과로 쓰지 않는다.
