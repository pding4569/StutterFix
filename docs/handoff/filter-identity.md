# 효과 없는 카메라 필터 검증 인계

2026-10-14. 게임 실행·DLL 설치·커밋·푸시는 하지 않았다. 측정 표본은 **0**이며 승인 파일을 배포하지 않는다.

`FilterIdentity` 설정은 기본 켬. 승인 전에는 원래 필터를 실행한다. 여섯 후보의 정확한 중립값만 검사하며 컴포넌트 enabled는 바꾸지 않는다. 필터 OnRenderImage에 prefix를 설치하고 승인한 입력 조건에서만 Graphics.Blit으로 대체한다. 정상 경로에는 픽셀 읽기, 필드 reflection, 할당, Stopwatch가 없다. Main.Unload/모드 끄기에서 공용 Harmony ID로 패치를 제거하고 상태를 비우며, 시험용 별도 ID도 제거한다.

`filteridentity on|off|state|approve`는 DEV/AUTOTEST 전용. on은 이전 결과를 지우고 실제 필터 콜백에 대조를 설치한다. 원래 필터를 **한 번만** 실행하며 같은 프레임의 같은 입력에 대한 단순 Blit과 원래 출력을 임시 RenderTexture에서 RGBA 바이트로 비교한다. enabled/필터 값/게임 시간은 변경하지 않는다. 원래 출력은 원래 목적지로 전달한다. on 동안 최적화는 비활성화된다. 120프레임마다 읽으며 각 필터의 전체 콜백 수/중립값 콜백 수도 state로 출력한다. 이 값은 2초 간격 filterstate 스냅샷 등장 횟수와는 다른 통계다.

approve는 대조를 종료하고 16표본 이상, 실패 0, 모든 표본의 최대 채널 차이 <=1/255인 필터·렌더 조건만 `Mods/StutterFix/filteridentity-approved.txt`에 기록한다. 실패·미관측 후보는 승인되지 않는다. 승인 파일은 개발용/정식 DLL이 공통으로 읽는다. GPU/그래픽 API/Unity 버전/색공간/게임 필터 어셈블리 MVID와 입력 크기·포맷·sRGB·MSAA가 일치해야 한다. 현재 ARGB32 입력만 지원한다. 목적지가 다른 포맷·sRGB·크기면 건너뛰지 않는다. 파일 삭제 후 게임 재시작하면 승인이 해제된다. 파일은 배포 패키지에 넣지 않는다. 이 인계에서는 어떤 후보도 화면 동등성을 확정하지 않았다. 시간·노이즈 효과나 특정 입력에서 차이가 나는 후보는 제외한다. 유한한 실제 맵 표본만으로 모든 가능한 입력/셰이더 변경까지 보장할 수 없으므로 다른 맵과 그래픽 조건에서도 다시 확인한다.

## Claude 실행 순서

게임을 완전히 종료하고 Mods DLL을 백업한다. PlayerAuto DLL을 설치한다. 같은 DLL reload로 A/B 하지 않는다.

```python
chart = "D:/얼불춤 맵 파일/HELLO (BPM) 2026/level.adofai"
sf_run(steps=["auto on", "game " + chart, "filteridentity on", "press",
              "waitend 900", "filteridentity state", "filteridentity off", "quit"],
       timeout_min=20, tag="filteridentity-observe")
```

로그를 먼저 검토한다. 통과 후보가 있다면 새로운 실행에서 같은 검증을 하고 승인한다. 위 첫 판에서 off 해도 결과는 남지만 프로세스 종료하면 없어지므로 승인 판이 필요하다.

```python
sf_run(steps=["auto on", "game " + chart, "filteridentity on", "press",
              "waitend 900", "filteridentity state", "filteridentity approve", "quit"],
       timeout_min=20, tag="filteridentity-approve")
```

원래 화면과 최적화 코드 경로를 대조하기 위해 승인 파일을 그대로 두고 **새 판에서** 다시 on/state/off를 실행한다. on은 최적화를 우회하고 원래 함수 출력과 정식 빌드와 같은 Graphics.Blit 경로를 대조한다. 실패하면 승인 파일을 삭제하거나 approve를 새로 실행해 실패 조건을 제외한다. 짧은 구간만 통과했다고 곡 전체를 통과한 것으로 처리하지 않는다. 상태 로그에 안 나온 후보/조건은 승인되지 않았다.

2초마다 filterstate 스냅샷이 필요하면 별도 판에서 다음 steps를 사용한다(곡 길이에 맞게 반복 수 조절).

```python
steps = ["auto on", "game " + chart, "press"]
for _ in range(180):
    steps += ["wait 2", "filterstate"]
steps += ["waitend 900", "quit"]
sf_run(steps=steps, timeout_min=20, tag="filterstate-2sec")
```

대조가 꺼진 별도 ABBA 4판으로 FPS/GPU 비용을 잰다.

```python
sf_ab(steps=["auto on", "game " + chart, "press", "waitend 900", "quit"],
      setting="FilterIdentity", a=False, b=True, repeats=2,
      timeout_min=20)
```

GPU 평균은 sfmeasure의 presentmon 인자와 기존 PresentMon 설정으로 별도 수집한다. 이득 <0.1ms면 그 값 그대로 기록하고 Claude가 채택을 결정한다. 시험용 픽셀 읽기 판의 FPS는 성능 근거로 사용하지 않는다.

마지막에 게임 종료 후 정식 `bin/Player/StutterFix.dll`로 교체하고 승인 파일을 유지한 채 같은 맵을 수동 완주한다. **정식 빌드는 AutoTest.Init/Tick이 꺼져 있어서 sf_run 자동 명령을 실행할 수 없다.** sf_run으로 정식 빌드 자체를 검증했다고 기록하면 안 된다. 위 PlayerAuto 판은 정식과 동일한 FilterIdentity 최적화 구현을 검증하는 자동 시험이며 정식 DLL 최종 확인은 수동이다. 설정 켬/끔 양쪽의 화면, 중립값이 아닌 구간, 종료 및 UMM 비활성화 후 원래 동작도 확인한다.

## 빌드 및 남은 항목

요청한 PlayerAuto/Player 빌드 각각 오류 0(기존 경고 25). 정식 Compile 목록에서 FilterIdentityProbe 제외, ILScan 정식 DLL에서 FilterIdentityProbe 0개 형식. git diff --check 통과. 화면/시간·노이즈 안정성/성능/전체 곡 및 다른 맵 검증은 Claude 실행 대기. git fetch는 .git/FETCH_HEAD 쓰기 권한 거부로 실패해 작업 브랜치를 새로 만들지 못했고 현재 codex/shader 파일에만 변경했다. 커밋·푸시는 하지 않았다.

## 결과 (Claude, 2026-10-14, HELLO 2026 곡 전체 1판)

| 필터(값) | 표본 | 대조 결과 |
|---|---:|---|
| Distortion_Dream2 (Distortion=0) | 2 | 실패, 최대 차이 255/255 |
| TV_Chromatical (Intensity=0) | 7 | 실패, 최대 차이 204/255 |
| Colors_Brightness (_Brightness=1) | 4 | 실패 0, 차이 0 (표본 16 미만) |
| Color_Chromatic_Aberration (Offset=0) | 5 | 실패 0, 차이 0 (표본 16 미만) |
| Color_Contrast (Contrast=1) | 0 | 표본 없음 |

"값이 0 이면 효과 없음" 가정은 두 필터에서 틀렸다(값 0 에서도 화면을 바꿈). 통과한 두 필터는 곡 전체에서 4~5번만 나와 승인 기준(16표본)에 못 미치고,
켜진 시간도 짧아 프레임당 0.03ms 이하로 어림된다. `FilterIdentity` 를 기본 끔으로 두고 개발용 대조 도구만 남긴다. 내장 그래픽 같은 느린 GPU 에서 필터가 매 프레임 도는 맵이면 다시 검토.
