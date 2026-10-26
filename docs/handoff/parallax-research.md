# 장식 시차(parallax) 한 번에 계산하기 연구 (Codex 명세)

작업 폴더 `E:\StutterFixWork\wt-par` (브랜치 `claude/parallax-research`). **기존 소스 파일은 고치지 않는다.** 결과 문서 `docs/research/parallax-grouping.md` 와 오프라인 도구(`tools/parallax-lab/`)만 새로 만든다. 게임 실행·커밋 금지(빌드·오프라인 실행만).

## 질문
사용자 아이디어: "시차(parallax)가 같은 장식은 한 번에 계산해서 넘기면 되지 않나?"
같은 시차 값(parallax x,y 와 그에 딸린 값)을 가진 장식은 카메라 의존 항(카메라 위치·줌·회전에서 나오는 오프셋)이 같으므로, 장식마다가 아니라 시차 값 종류마다 한 번 계산해 공유하면 CPU(또는 GPU) 일이 줄어드는가?

## 알아낼 것 (근거와 함께, 추측 금지)
1. **게임 쪽 계산 위치**: 게임이 시차를 프레임마다 어디서 적용하는지. `tools/ilscan`(CLAUDE.md 참고) 또는 ilspy 로 `Assembly-CSharp.dll`(`D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\A Dance of Fire and Ice_Data\Managed`)에서 `scrDecoration`/`scrVisualDecoration` 의 parallax·`Update`·`LateUpdate`·위치 적용 경로를 찾고, 장식당 프레임당 하는 일(필드 읽기, 카메라 접근, 곱셈, transform 쓰기)을 나열한다. 카메라 의존 항이 장식마다 따로 계산되는지, 공유 가능한지 판단한다.
2. **모드 쪽 현황**: 이 저장소의 `FastMove.cs`, `DecoAnim*.cs`, `DecoBake*.cs`, `DecoEngine.cs`, `GpuDeco*.cs`, `tools/gpudeco-lab/GpuDeco.compute` 에서 시차를 어디서 어떻게 처리하는지(GPU 소유 장식은 커널 안에서 장식마다 계산하는지, CPU 경로는 어떤지) 정리한다. 이미 같은 값끼리 묶는 곳이 있으면 지적.
3. **맵 통계**(오프라인, `.adofai` JSON 읽기): 아래 맵에서 장식(`AddDecoration`/`AddText`/`AddObject`)의 시차 필드와 `MoveDecorations` 이벤트의 시차 필드를 모아, **고유한 시차 값 조합 수**, 가장 큰 묶음 크기 분포, 시차가 0 이 아닌 장식 비율, 시차 값이 시간에 따라 바뀌는 비율을 낸다.
   - Arche: `D:\얼불춤 맵 파일\Arche\[𝐃𝐎 𝐍𝐎𝐓 𝐎𝐏𝐄𝐍 𝐓𝐇𝐈𝐒 𝐀𝐓 𝐇𝐎𝐌𝐄] FULL.adofai` (42MB, 장식 약 2.9만)
   - HELLO 2026: `D:\얼불춤 맵 파일\HELLO (BPM) 2026\level.adofai`
   - Windflower: `D:\얼불춤 맵 파일\Windflower\Windflower.adofai`
   - Hertz 폴더의 맵은 **절대 열지 말 것**(비공개).
4. **절감 상한 추정**: 가능하면 오프라인 마이크로벤치마크(C# 콘솔 .NET 8)로 "장식 N개마다 카메라 항을 곱·삼각함수 계산" 대 "고유 값 K개만 계산 후 공유"의 비용을 재서, 실제 게임에서 장식당 비용 대비 몇 %가 카메라 의존 항인지 추정한다. 게임 안 실측은 우리가 한다(요청하지 말 것).
5. **결론**: (가) 공유가 의미 있는 곳 (나) 이미 GPU 가 장식마다 계산하고 비용이 무시할 수준이면 그렇다고 (다) 구현한다면 어디에 어떻게 끼울지(함수·후크 후보)와 검증 방법(같은 결과 비트 단위 대조). 이득이 없으면 없다고 쓴다.

## 규칙
- 모든 숫자에 출처(파일·줄, 맵·이벤트 수, 벤치 명령)를 붙인다. 모르면 "확인 못 함".
- 새로 만드는 파일 이외 수정 금지. 큰 맵은 스트리밍으로 읽어도 되고, 메모리 24GB PC 이므로 2GB 이내로.
- 최종 보고는 한국어, 결론을 맨 위 5줄 안에.
