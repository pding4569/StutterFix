# Parallax Lab

장식 시차 공유 가능성을 조사하는 오프라인 전용 도구. 결과 해석은 [연구 보고서](../../docs/research/parallax-grouping.md)에 있다.

게임·Unity 어셈블리를 실행하거나 참조해 로드하지 않는다. 게임 DLL은 ILSpy로 정적으로 읽으며 맵은 `Program.cs`에 지정한 세 파일만 읽는다. 폴더 검색은 하지 않는다. 원본 맵, 기존 모드 소스, 게임 설치 폴더에 쓰지 않는다.

필요 도구: 로컬 .NET 8 SDK/reference pack, Python 3 표준 라이브러리, 설치된 `ilspycmd`.
NuGet 패키지 의존성은 없으며 `NuGet.Config`는 패키지 원격 소스를 비운다. ILSpy 업데이트 확인도 끈다.

저장소 루트의 PowerShell에서 전체 재현:

```powershell
& .\tools\parallax-lab\run-offline.ps1
```

부분 실행:

```powershell
dotnet restore tools/parallax-lab/ParallaxLab.csproj --configfile tools/parallax-lab/NuGet.Config
dotnet build tools/parallax-lab/ParallaxLab.csproj -c Release --no-restore
dotnet tools/parallax-lab/bin/Release/net8.0/ParallaxLab.dll selftest
dotnet tools/parallax-lab/bin/Release/net8.0/ParallaxLab.dll stats
$env:DOTNET_TieredCompilation='0'
$env:DOTNET_EnableHWIntrinsic='0'
dotnet tools/parallax-lab/bin/Release/net8.0/ParallaxLab.dll bench 28835 2091 600 9
py -3 tools/parallax-lab/validate_stats.py
py -3 tools/parallax-lab/summarize.py
```

부분 실행의 환경 변수는 현재 PowerShell 프로세스에 적용된다. 전체 재현 스크립트는 기존 값을 복구한다.
전체 스크립트는 이 도구의 생성 증거만 덮어쓴다. 기존 연구 시점의 숫자를 보존하려면 재실행 전에 `evidence/`를 따로 복사한다. 보고서 본문은 자동 갱신하지 않는다.

`stats`는 공백 표현을 제거한 float32 비트 키로 작성값을 센다. 누락값을 기본값으로 가정하지 않는다.
태그는 공백으로 분리한 정확 일치의 합집합이며, 빈 태그는 게임의 `Setup`/`Decode`처럼 `NO TAG`로 센다.
오프셋 `[null,null]`은 필드 존재에는 세지만 유효 변경에는 세지 않는다.
Arche의 루트 쉼표 누락과 Windflower의 문자열 제어문자는 메모리에서만 정규화하고 적용 수를 JSON에 남긴다.

`bench N K frames rounds`는 N/K만 통계와 맞춘 합성 데이터다. 실제 맵 좌표·배율·묶음 빈도를 재생하지 않는다.
완전한 피연산자 일치 공유와 배율만을 공유하는 재결합을 나눠 측정하며, 후자는 원식과 비트가 다를 수 있다.
sin/cos 비교는 가상 작업이다. 일반 시차 위치 수식에는 sin/cos가 없다.
게임 FPS, Mono/Transform 비용 비율, GPU 실행 시간은 측정하지 않는다.

`evidence/summary.md`는 간결한 결과 색인, `map-stats.json`은 전체 묶음 크기 히스토그램과 필드 분포,
`bench-*.json`은 라운드별 시간·비트 비교, `provenance.json`은 DLL·기존 소스·도구 해시를 담는다.
독립 Python 검증은 원본 세 맵을 다시 파싱해 float32 그룹 수와 태그 대상 등을 교차 확인한다.
벤치 비교의 동일공유/inline 불일치가 생기면 해당 결과는 일치 통과로 해석하지 않는다.
