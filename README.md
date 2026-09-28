# Stutter Fix

얼불춤(A Dance of Fire and Ice) 고사양 커스텀 맵에서 **플레이 중 순간적으로 멈추는 현상**과 **맵 로딩 시간**을 줄이는 Unity Mod Manager 모드입니다.

> **원칙: 연출·판정·소리는 원래 게임과 똑같이.**
> 게임이 일을 처리하는 순서와 방법만 바꿉니다. 결과가 달라질 수 있는 기능(저사양 페이지)은 전부 기본으로 꺼져 있습니다.
> 모든 기능은 실제 맵에서 끊긴 순간을 하나씩 측정해 원인을 찾은 뒤 만들었고, 원래 게임과 같은 결과인지 자동으로 대조해 확인했습니다.

made by **naro** & **Claude**

**디스코드 서버: [discord.gg/csys9ZAeD6](https://discord.gg/csys9ZAeD6)** — 버그 제보, 기능 아이디어, 질문 모두 여기로 받습니다.

## 한눈에 보기

| 맵 | 바뀐 것 | 측정 |
|---|---|---|
| Arche (장식 28,835개) | 매 프레임 훑는 장식 | 28,835개 → 평균 325개, 곡 평균 107 → 170 FPS |
| Arche | 효과가 몰리는 프레임 | 91 → 67ms |
| 블렌드 장식 1,500개 (3440×1440) | 화면 복사 없이 그리기 | 약 11 FPS 로 떨어지던 구간이 끊김 없이 |
| Arche | 에디터 재생 시작 | 8.6초 → 4.3초 (2.2.0) |
| Arche | 에디터 Play 직후 첫 판 FPS | 약 200 → **약 300 FPS** (2.3.0) |
| Arche | 에디터에서 죽고 다시 하기 | 9.1초 → 3.2초 (2.3.0) → **2.1초** (2.3.1) |
| Arche | 편집으로 나가기 | 2.3~3.2초 → **1.4~1.7초** (2.4.0) |
| Hello (BPM) 2026 | 맵 불러오기 | 12.2초 → **8.1초** |
| Hello (BPM) 2026 | 첫 판 곡 중 끊김 | 10번(최악 133ms) → **2번(최악 35ms)** |

## 목차

- [설치](#설치)
- [사용법](#사용법)
- [2.4.4 에서 바뀐 것](#244-에서-바뀐-것) · [2.4.3](#243-에서-바뀐-것) · [2.4.2](#242-에서-바뀐-것) · [2.4.1](#241-에서-바뀐-것) · [2.4.0](#240-에서-바뀐-것) · [2.3.4](#234-에서-바뀐-것) · [2.3.3](#233-에서-바뀐-것) · [2.3.2](#232-에서-바뀐-것) · [2.3.1](#231-에서-바뀐-것) · [2.3.0](#230-에서-바뀐-것)
- [기능](#기능) — [플레이](#플레이) · [맵 불러오기](#맵-불러오기) · [그래픽](#그래픽) · [저사양](#저사양) · [편의](#편의) · [다른 모드와 함께](#다른-모드와-함께)
- [실시간 모니터](#실시간-모니터)
- [문제 보고](#문제-보고) · [그래도 끊긴다면](#그래도-끊긴다면)
- [두 가지 버전](#두-가지-버전) · [빌드](#빌드) · [환경](#환경) · [라이선스](#라이선스와-사용한-외부-코드)
- [English](#english)

## 설치

1. [Releases](https://github.com/pding4569/StutterFix/releases)에서 `StutterFix-x.y.z-player.zip`을 받습니다.
2. Unity Mod Manager의 **Mods** 탭에서 **Install Mod**로 zip을 고르거나, 압축을 풀어 `A Dance of Fire and Ice/Mods/StutterFix/` 폴더에 넣습니다.
3. 게임을 켜면 적용됩니다. **게임을 한 번 더 껐다 켜면** 멀티스레드 그리기까지 적용됩니다.

## 사용법

- **Insert**: 설정 창. 화면 오른쪽 끝의 아이콘 줄에서 아이콘을 누르면 그 기능 패널이 펼쳐지고, 바깥을 누르면 닫힙니다.
- **Shift+Insert**: 실시간 모니터 (아이콘 → 미니 → 상세 → 끔)
- 두 단축키는 설정 창 홈에서 바꿀 수 있고, 한국어/English를 고를 수 있습니다.
- 아이콘 줄 맨 아래 버튼으로 게임을 다시 켤 수 있습니다. 에디터에서 맵을 열어 둔 채라면 **이 맵으로 재시작**으로 다시 켠 뒤 그 맵을 바로 엽니다(저장 안 한 편집이 있으면 한 번 알리고, 한 번 더 누르면 저장하지 않고 재시작). 다시 켜면 좋은 때(설정 변경, 모드 업데이트, 메모리를 많이 씀, 오래 켜 둠)는 주황색 표시로 알려 줍니다.

## 2.4.4 에서 바뀐 것

### 에디터 재생 시작이 빨라짐
편집으로 나가기(2.4.0)에 쓰던 "안 바뀐 장식은 가볍게 다시 설정"을 에디터 재생 시작에도 씁니다. 편집 화면에서 손대지 않은 장식은 지난 다시 설정(나가기) 뒤 그대로이므로, 재생 준비 끝의 장식 전체 다시 설정에서 결과가 같은 설정을 건너뜁니다. 편집으로 바뀐 장식(설정 함수가 불렸거나 값·이벤트 데이터가 바뀜)은 원래대로 합니다.
측정(플레이어용, Arche): 두 번째 재생부터 **3.3~3.9초 → 2.8~3.0초**. 검증(개발자용, 4개 맵에서 재생 시작 16번·나가기 16번을 원래 방식과 같은 프레임에 비교): 모두 같음.

### 효과 컴포넌트 다시 쓰기 (재생 시작·나가기 더 빨라짐)
나가기·재생 시작 때마다 게임은 타일의 효과 컴포넌트(Arche 11만 개)를 전부 지웠다가 새로 붙입니다. 지우고 붙이는 것 자체가 유니티 안쪽 비용이라, 지우지 않고 남겨 뒀다가 같은 종류를 다시 붙일 때 **새로 만든 것과 똑같은 상태로 되돌려** 씁니다(필드를 모두 지우고 생성자·Awake 를 다시 실행). 남겨 둔 것은 게임의 효과 찾기에서 빠지고, 찾기 결과는 원래 붙는 순서로 돌려줍니다. Start·OnDestroy 가 있는 효과(트랙 이동, 고급 필터)는 원래대로 지우고 붙입니다.
측정(플레이어용, Arche): 재생 시작 **3.1~3.2초 → 2.4~2.6초**(두 번째부터), 나가기 **1.5~1.6초 → 1.0~1.3초**. 검증(개발자용, 4개 맵, 붙이기마다 같은 프레임에 원래 방식으로 한 번 더 해서 효과 11만 개의 모든 필드를 비교): 모두 같음.

### 곡 중 쓰레기 더 줄임 (1시간 맵 1.45 → 1.19MB/s, 2.4.2 대비 -46%)
- 비동기 입력을 써도 게임이 매 프레임 부르는 레거시 키보드 입력 확인에서 람다·임시 목록을 없앰(개발자용 매 호출 비교 18,943번, 다름 0).
- 이 모드의 모니터·설정 창이 쓰지 않는 GUILayout 준비를 유니티가 프레임마다 하던 것을 끔(모니터는 늘, 설정 창은 닫혀 있고 곡 중일 때). 곡 중 설정 창을 열고 닫아도 정상으로 그려짐을 확인.

### 자동 업데이트
"켤 때 새 버전 확인"이 **자동 업데이트**가 됐습니다. 게임을 켜면(그 뒤로 3시간마다) 새 버전을 확인하고, 있으면 알아서 받아 설치합니다. 게임을 다시 켜면 새 버전이 적용됩니다.
곡(에디터 재생 포함) 중에는 확인도 설치도 하지 않고 곡이 끝난 뒤에 합니다. 받기는 네트워크 스레드에서, 압축 풀기·파일 쓰기는 작업 스레드에서 해서 메인 스레드가 멈추지 않습니다. 끄면 예전처럼 알림만 보고 버튼으로 받습니다.

### 에디터의 쓰지 않는 썸네일 카메라 끄기
에디터에 있는 동안(곡 중 포함) 게임이 워크숍 썸네일용 512x512 이미지를 매 프레임 다시 그리고 있었습니다. 이 이미지는 썸네일을 저장할 때만 직접 한 번 그려서 쓰므로 카메라만 꺼 둡니다(프레임당 메인 스레드 0.07~0.09ms, 그래픽카드도 한 장 덜 그림). "투명한 장식 그리지 않기"에 포함됩니다.
검증: 같은 프레임에 카메라를 켠 채/끈 채 만든 썸네일이 3개 맵 모두 바이트까지 같음.

### 화면 밖 타일 끄기 (타일이 많은 맵의 평균 FPS)
타일이 많은 맵은 평균 FPS 자체가 낮았습니다(같은 PC에서 5천 타일 402 FPS, 9만 타일 295 FPS). 늘어난 시간은 거의 다 유니티가 카메라 4개마다 화면 밖 타일까지 모두 "보이나?" 검사하는 값이었습니다. 타일이 3천 개 넘는 맵에서는 곡 중에 화면에서 먼 타일의 렌더러를 꺼서 그 검사에서 빼고, 카메라가 다가가면 그리기 직전에 다시 켭니다.
- 게임과 다른 모드가 렌더러를 켜고 끄는 곳(게임 시작 뒤 첫 맵 로딩 때 찾아 바꿔 끼움, 약 190곳)에는 꺼 둔 타일도 원래 값으로 보이고, 그 사이 바꾼 값은 다시 켤 때 반영합니다.
- 움직이거나 모양이 바뀌는 타일(트랙 이동·등장·사라짐, 회전, 메시 다시 만들기)은 바로 켜고 빼지 않습니다. 1초 넘게 안 움직이면 새 크기로 다시 넣습니다.
- 측정(플레이어용, 9만 타일 시험 맵, 번갈아 2번씩): **295·297 → 418·425 FPS** (CPU 3.4 → 2.4ms/프레임).
- 검증(개발자용, Windflower·scam·Arche·9만 타일 맵, 다시 하기 포함): 꺼 둔 타일 중 실제 화면과 겹친 것 0번, 꺼진 채 움직인 것 0번, 곡 중에 "지금 상태"와 "전부 켠 상태"로 타일을 그려 비교한 픽셀 차이 0개.
- "투명한 장식 그리지 않기"에 포함됩니다.

### 박자 알림 가볍게 (고BPM·타일 많은 맵, 큰 맵을 연 직후 편집 화면 끊김)
게임은 박자마다 모든 타일에 박자 알림(OnBeat)을 보냅니다. 게다가 타일은 다시 설정될 때마다 알림 목록에 자기를 또 넣어서, 맵을 열고 편집할수록 같은 타일이 여러 번 들어 있습니다. 고BPM 에서는 이 알림이 매 프레임 불리고, 큰 맵을 연 직후에는 밀린 박자를 따라잡느라 수십 초 동안 매 프레임 불려 편집 화면이 "게임 처리"로 끊겼습니다(9만 타일: 프레임당 약 6ms).
아직 안 지나간 타일은 알림을 받아도 아무것도 하지 않으므로(타일 스프라이트 덮어쓰기 설정이 꺼져 있을 때), 무언가를 하는 항목(타일이 아닌 것, 지나간 타일)의 번호를 원래 순서대로 들고 있다가 그것만 부릅니다. 지나간 타일은 원래 순서·횟수대로 불러 무작위 색의 난수 순서까지 같습니다.
- 측정(플레이어용, 9만 타일 시험 맵, 화면 밖 타일 끄기 켠 상태): BPM 32000 **166 → 287 FPS**, BPM 100000 **112 → 159 FPS**. 큰 맵을 연 직후 편집 화면 프레임 9.2 → 3.9ms.
- 검증(개발자용): 64박자마다 알림 목록·타일 전부와 대조해 틀린 것 0번. 앞선 방식으로 건너뛴 호출 222만 번을 실제로 불러 봐도 난수 상태·스프라이트·색이 바뀐 것 0번.
- "장식 순회 줄이기"에 포함됩니다.

### 맵을 열 때마다 쌓이던 타일 머티리얼 풀기
에디터에서 다른 맵을 열면 게임은 타일을 새로 만드는데, 지워진 옛 타일의 머티리얼 복제본이 풀리지 않고 남았습니다(에셋 정리로도 안 풀림, 이 모드 없이도 같음). 9만 타일 맵이면 맵을 열 때마다 머티리얼 9만 개(약 128MB)씩 쌓였습니다.
타일 렌더러마다 가진 머티리얼을 기록해 두었다가, 타일 만들기가 끝나면 지워진 타일의 것(지금 타일이 쓰지 않는 것만)을 풉니다.
- 측정: 9만 타일 맵을 세 번 열었을 때 머티리얼 90,899 → 180,944 → 270,990개(384MB)였던 것이 90,278 → 90,322 → 90,367개(128MB)로 그대로.
- 확인: 풀어 준 뒤 재생·편집·다른 맵 열기에서 오류 0건.
- "게임 메모리 누수 막기"에 포함됩니다.

## 2.4.3 에서 바뀐 것

### 곡 중 쓰레기 줄이기 (1시간 맵 초당 2.21MB → 1.45MB, -34%)

곡 중에는 메모리 정리를 미루므로 쓰레기가 적을수록 긴 곡에서 한계에 늦게 닿습니다(한계 근처에서는 2.4.2 부터 입력이 없는 틈에만 치움). 곡 중 프레임 단계별·함수별로 힙이 느는 양을 재서(개발자용) 게임 코드의 큰 곳 셋을 고쳤습니다. 모두 결과가 원래와 같게 임시 객체만 없앴습니다.

| 곳 | 원래 | 고친 것 |
|---|---|---|
| 타일 업데이트(scrFloor.Update) | 볼륨 색 모드에서만 쓰는 람다용 객체를 모든 타일이 매 프레임 만듦(초당 0.38MB) | 볼륨 모드(또는 Start 전) 타일만 새로 만들고 나머지는 하나를 돌려씀 |
| 비동기 키 입력 | 부를 때마다 HashSet·LINQ·람다·특수 키 목록(초당 0.45MB) | 같은 순서로 다시 쓰는 목록에 모으고, 특수 키 목록은 누른 키가 있을 때만 원래 함수로. 결과 목록은 원래처럼 새로 |
| 마우스 입력 | 프레임마다 LINQ·람다로 목록(초당 0.14MB) | 같은 순서의 반복문으로 |

검증(개발자용, 곡 중 실제 키를 보내며 부를 때마다 원래 코드와 비교): 비동기 키 입력 69,190번(키가 있던 것 6,157번), 마우스 입력 69,190번, 다른 결과 0번. Arche 재생 정상(319 FPS).
남은 것: 화면 그리기 마무리 단계 0.47MB/s(모드·게임의 그리기 콜백에서는 안 잡힘, 유니티 내부로 보임), 입력 처리 안의 람다 등 약 0.5MB/s.

## 2.4.2 에서 바뀐 것

### 곡 중 메모리 정리를 입력이 없는 틈에만

아주 긴 곡에서 힙이 한계(RAM 의 40%, 최대 6GB)에 닿아 곡 중에 치워야 할 때, 이제 **판정에 영향이 없는 순간**에 치웁니다.

- 한계의 80% 부터 "다음 타일까지 예상 멈춤 + 0.15초 이상 비는 틈" 이나 일시정지를 기다렸다가, 그 순간 한 번에 치웁니다. 멈춤이 틈 안에서 끝나므로 다음 타일 입력과 겹치지 않습니다.
- 예상 멈춤은 GB 당 50ms(6GB 면 0.3초)와 지난번 실측의 1.2배 중 큰 값입니다. 틈이 한계까지 끝내 없을 때만 2.4.1 방식(점진적 GC 를 한 번 시작시킴)으로 치웁니다.
- RAM 상한은 그대로입니다. 저사양 PC 는 RAM 이 작아 상한을 올려도 한계에 닿기 때문에, 한계에 닿아도 판정과 겹치지 않게 하는 쪽을 택했습니다.

측정(개발자용 자동 시험, 1시간 맵에서 곡 중 쓰레기를 초당 40MB 로 만들어 6GB 한계 근처를 세 번 지나게 함): 세 번 모두 틈에서 치움(다음 타일까지 0.51 / 1.79 / 2.79초, 필요 0.41~0.43초), 멈춤 195~206ms 가 그 틈 안에서 끝남, 6GB → 0.65GB. 한계까지 간 적 0번.
참고: 같은 조건에서 방식별 멈춤 — 한 번에 크게 조각 치우기 180~206ms(부른 그 순간 끝남), GC.Collect 265ms, 작게 시작만 시키기 95ms(대신 0.7초 안 어느 순간에 옴).

곡 중 쓰레기가 어디서 생기는지도 쟀습니다(개발자용): 이 1시간 맵은 초당 2.2MB, 그중 76% 가 스크립트 업데이트 단계(게임 입력 처리 쪽 0.79MB/s, 타일 색 애니메이션 0.37MB/s 등), 21% 가 화면 그리기 마무리 단계입니다. 게임 코드 여러 곳에 흩어져 있어 이번에는 고치지 않았습니다.

## 2.4.1 에서 바뀐 것

### 아주 긴 곡에서 메모리 정리가 끝없이 되풀이되던 것 고침

곡 중에는 메모리 정리(GC)를 미루다가 힙이 한계(RAM 의 40%, 최대 6GB)에 닿으면 멈추지 않게 점진적 GC 로 치웁니다. 1시간짜리 맵에서 곡 31분에 한계에 닿은 뒤 **0.3초마다 74ms 씩 멈추기가 나갈 때까지 계속**됐습니다(제보, 47초 동안 116번).

원인: 유니티의 점진적 GC 는 한 번 시작시키면 그 바퀴를 알아서 끝까지 돌리고, 모드에게는 늘 "남은 일 있음" 만 돌려줍니다. 모드는 그걸 모르고 몇 프레임마다 새 바퀴를 또 시작시켰고, 바퀴마다 끝에서 게임을 한 번 세웠습니다. 실제로는 첫 바퀴 뒤 1초 안에 힙이 이미 줄어 있었습니다(6GB → 0.6GB).

이제 한계에 닿으면 **한 번만 시작시키고 힙이 줄었는지만 봅니다**. 줄면 끝, 30초 동안 다시 시작하지 않습니다. 치운 뒤에도 한계 가까이면(살아 있는 메모리가 많은 맵) 그 곡 동안 한계를 올려 되풀이하지 않습니다. 원래 한계의 1.5배를 넘으면 메모리가 우선이라 한 번 멈춰 치우는 안전장치는 그대로입니다.

재현·확인(개발자용 자동 시험, 같은 1시간 맵, 곡 중 쓰레기를 초당 40MB 로 만들어 6GB 한계에 세 번 닿게 함): 예전 방식은 한계 뒤 곡 끝까지 **59번** 끊김, 새 방식은 한계에 닿을 때마다 **1번(91~95ms)**, 6GB → 0.64GB 가 0.6~0.7초 만에 끝남. 실제 이 맵(초당 약 2.5MB)이면 곡 31분쯤 한 번입니다.

## 2.4.0 에서 바뀐 것

### 편집으로 나가기가 빨라짐

플레이하다 에디터로 나갈 때 잠깐 멈추던 것(제보)을 줄였습니다. Arche 에서 나가기 멈춤을 나눠 보면 장식 28,835개 다시 설정 1.5초, 타일에 붙은 효과 컴포넌트 11만 개 지우기 0.76초, 그리고 한동안 플레이했으면 곡 중에 미뤄 둔 메모리 정리 0.7~1초였습니다.

- **판 중에 안 바뀐 장식은 가볍게 다시 설정**: 게임은 나갈 때 모든 장식을 이벤트 값으로 처음부터 다시 설정(Setup)합니다. 이제 재생 시작 때 장식마다 값(효과가 바꾸는 필드 전부)을 찍어 두고, 나갈 때 그대로이고 필드에 흔적이 안 남는 설정(깊이, 마스크 깊이, 히트박스)도 불린 적 없는 장식은, 결과가 지금과 같은 설정은 건너뛰고 달라질 수 있는 것(놓는 위치, 위치·시차·회전·크기, 색, 보임, 태그 목록, 마스크·타일링 갱신 알림, 필터 목록, 에디터 클릭 상자)만 게임 함수로 다시 합니다. 바뀐 장식과 글자·오브젝트·파티클·블렌드·마스크·히트박스 장식, 장식 데이터가 바뀐 경우는 원래대로 합니다. 장식 순서대로 섞어 부르므로 태그 목록 순서도 원래와 같습니다.
- **미뤄 둔 메모리 정리를 나눠서**: 곡 중에 미뤄 둔 쓰레기가 많이 쌓였으면 나갈 때 한꺼번에 치웠습니다(자동 시험 기록 0.68~1.0초). 이제 편집 화면에서 프레임마다 3ms 씩 조금씩 치웁니다. 끝나기 전에 다시 재생하면 거기서 멈춥니다.
- **장식 데이터 지문을 한 프레임에 한 번만**: 에디터 재생 시작이 한 프레임에 세 번 재던 장식 데이터 지문(Arche 한 번 약 0.1초)을 한 번만 잽니다.
- 타일 효과 컴포넌트 지우기(0.76초)는 뒤에서부터 지우기, 그동안 타일 충돌체 끄기 모두 시간이 같아(유니티의 컴포넌트 지우기 값 자체) 그대로 둡니다.

측정(플레이어용, 사람 없이 자동으로 Arche 재생 → 나가기, 이전 코드와 같은 PC·같은 시나리오):

| | 이전 | 2.4.0 |
|---|---|---|
| 한동안 플레이한 뒤 나가기 (메모리 정리가 걸림) | 3.18초 (정리 0.72초 포함) | **1.60~1.63초** (정리는 뒤이어 2.1~2.2초 동안 한 프레임 최대 약 39ms, 합계 0.13~0.15초) |
| 그 밖의 나가기 | 2.28~2.57초 | **1.42~1.73초** |
| 에디터 재생 시작 | 3.50~4.05초 | 3.32~3.65초 |

50~110초 판 뒤에도 판 중에 바뀐 장식은 28,835개 중 41~74개였습니다. 나눠 치우기 뒤 다음 판 FPS 도 그대로(340~364 FPS, 곡 중 가장 긴 프레임 46ms 이하).

검증(개발자용 자동 비교): 나가기마다 가볍게 한 결과를 넓게 찍고(장식 스크립트의 모든 필드, 자식까지 모든 트랜스폼, 모든 렌더러의 켜짐·정렬·스프라이트·색·마스크·재질(셰이더 속성 값 전부), 충돌체, 다른 스크립트의 필드, 매니저의 태그·알림 목록), 같은 프레임에 원래 방식으로 다시 설정해 이름별로 비교합니다. Arche, Windflower, DDONGSSADA3302, Hello (BPM) 2026, QuomodocunquizE, Battle Against A True Hero, 7777, Plum - Timeline 에서 재생 → 나가기, 재생 → 다시 하기 → 나가기: 가볍게 한 장식(맵마다 134~28,812개) 전부 원래 방식과 같음, 매니저 목록도 같음. 검증이 잡아 고친 것: 게임의 Setup 은 태그 없는 장식을 "NO TAG" 목록에 넣었다가 곧바로 다시 빼는데, 처음 만든 가벼운 처리는 이것을 따라 하지 않았습니다(Plum - Timeline). 검증기 자체 시험: 500개마다 하나씩 정렬·위치·색·태그 목록을 일부러 틀리게 하면 네 종류 모두 잡아냄.

### 타일이 아주 많은 맵(헤르츠 맵) 열기가 빨라짐

게임은 타일을 만들 때마다 "Floors" 라는 정리용 오브젝트 밑으로 옮기는데, 옮길 때마다 이미 있는 타일 전부가 든 계층에 합치느라 **타일 수의 제곱으로** 느려졌습니다. 타일이 3만 개 이상이면 새 타일을 옮기지 않고 각자 둡니다(정리용 오브젝트는 원점에 있는 빈 오브젝트라 위치·모습은 같고, 게임 코드 전체에서 이 오브젝트를 쓰는 곳은 타일 만들기뿐).

| 타일 수 (시험 맵) | 원래 열기 | 2.4.0 열기 |
|---|---|---|
| 9만 | 18.4초 | 12.1초 |
| 36만 | 294초 | 60초 |

Play·편집 복귀 시간은 원래와 같습니다. 원인 찾기에서 효과가 없었던 것: 계층 용량 미리 잡기, 만드는 동안 GC 멈춤. "처음부터 Floors 밑에 만들기"는 열기는 빨라졌지만 Play·편집 복귀가 3배 느려져 뺐습니다.

타일이 20만 개 이상인 맵은 타일마다 붙은 에디터 **타일 번호 표시**(꺼져 있는 Canvas + UI 글자, 타일 하나 컴포넌트의 절반)를 없애 메모리를 줄입니다. 36만 타일에서 9.6GB → 7.1GB. 이런 맵에서는 에디터의 타일 번호 보기가 나오지 않습니다. 100만 타일 맵은 추정 약 27GB → 20GB 입니다.

### PC 맞춤 자동 설정

처음 켤 때 메뉴에서 0.2초쯤 CPU 한 스레드 속도, GPU 메모리 대역(화면 크기 복사), 내장 그래픽인지, RAM, 주사율을 재고, **약한 쪽에 맞는 저사양 기능만** 켭니다. 강한 PC 는 아무것도 바꾸지 않습니다. 홈 화면에 알림(확인/되돌리기)이 뜨고, 저사양 페이지 맨 위에서 다시 재거나 되돌릴 수 있습니다.

- **내장 그래픽**(인텔 UHD·Iris, AMD Radeon (TM) Graphics·Vega·680M/780M 등): 그래픽 메모리를 시스템 RAM 과 나눠 쓰고 CPU 와 전력도 나눠 써서, GPU 일을 줄이는 쪽이 CPU 에도 좋습니다. 자동 해상도(목표 FPS 를 못 맞출 때만 게임 화면을 작게 그리고 FSR 1 로 늘림), 장식 이미지 압축, 메뉴·에디터 FPS 60 제한을 켭니다. 최신 화면 출력 방식(Flip)은 실험 기능이라 권하기만 합니다.
- **느린 CPU**: 효과 몰림 잘게 나누기, 우선순위 높임, 절전 제한 끄기, 음악 반응 계산 건너뛰기, 화면 밖 파티클 멈춤.
- **RAM 12GB 미만 / VRAM 3GB 미만**: 장식 이미지 압축.

검증(개발자용 자동 시험): 같은 PC 에서 세 번 재어 오차 1~2%, 약한 PC 인 척 8개를 켰다가 되돌린 뒤 설정 전체가 전과 같음.

## 2.3.4 에서 바뀐 것

### 판마다 FPS 가 들쑥날쑥하던 것(첫 판 FPS 떨어짐) 진짜 원인 고침

어떤 판은 처음부터 끝까지 프레임마다 "화면 대기" 1.7ms 가 붙어 FPS 가 크게 떨어지던 것(예: 447 → 245 FPS, Arche 약 320 → 200 FPS)의 원인은 **이 모드의 "곡 중 메모리 정리 미루기"** 였습니다. 곡 중에 GC 를 완전히 꺼 버리는 방식(GCMode.Disabled)을 쓰면, 판이 시작될 때의 GC 상태에 따라 그 판 내내 유니티가 프레임마다 화면 대기 자리에서 GC 일을 하려다 막히는 것으로 보입니다. 이제 자동 GC 만 끄는 방식(Manual)으로 멈춥니다. 곡 중에 GC 가 멈춰 있는 것은 같습니다.

개발자용 자동 시험(사람 없이 되풀이):
- 작은 맵에서 다시 하기 26번: 예전 방식 **26판 중 9판 느림**(3판마다 한 번, GC 주기의 같은 자리) → 새 방식 **0판**. 곡 중 GC 끊김 0, 쓰레기는 판 사이에만 정리
- Arche 에디터 Play 15번: 전부 빠름(331~345 FPS)
- 참고: 곡 중 메모리 정리 미루기를 끄면 0판, 남은 GC 일을 끝낸 뒤 완전히 끄면 26판 모두 느림

바로잡기: 2.3.0~2.3.2 에서 원인으로 본 "응답 없음" 창, 멈춘 창 판정, 5~6초 넘는 멈춤은 원인이 아니었습니다. 긴 멈춤과 겹쳐 보였던 것은 멈춤 동안 GC 가 도는 경우가 많았기 때문입니다.

## 2.3.3 에서 바뀐 것

### 한 번 죽은 뒤 곡 중 메모리 정리를 미루지 않던 것 고침

게임은 에디터에서 다시 Play 할 때 "죽음/클리어" 상태 값을 되돌리지 않습니다. 그래서 에디터 밖에서 한 번 죽거나 깬 뒤로는 에디터 Play 가 모두 "이미 끝난 곡"으로 보여, 곡 중 메모리 정리 미루기가 켜지지 않았습니다(실시간 모니터의 메모리 정리가 계속 "대기", 곡 중 50~67ms 끊김). 이제 Play 가 시작될 때 이미 남아 있던 상태는 무시하고, 곡 중에 바뀔 때만 끝으로 봅니다. 죽음·클리어는 원래대로 게임의 종료 함수를 직접 가로채서도 잡습니다.

확인(개발자용 자동 시험): 상태를 죽음/클리어로 남겨 둔 채 에디터 Play → 둘 다 곡 중 정리 미룸. 자동 플레이 없이 죽는 판은 그대로 곡 끝으로 잡힘.

## 2.3.2 에서 바뀐 것

### 맵 연 뒤 첫 에디터 Play 가 빨라짐

에디터 Play 는 장식 전체를 두 번 연달아 다시 설정합니다(Arche: 1.35초 + 1.46초). 이 모드는 두 번째 Play 부터 첫 번째를 건너뛰었지만, 맵을 막 연 뒤 첫 Play 는 비교할 기준이 없어 늘 두 번 했습니다. 맵 열기는 장식을 모두 새로 만들며 같은 설정을 하므로 "막 다시 설정한 상태"와 같습니다. 이제 첫 Play 도 한 번 건너뜁니다.

- 측정(개발자용 자동 시험, Arche): 첫 Play **5.5초 → 4.7~5.4초**, 플레이어용은 더 짧음
- 검증(개발자용 자동 비교, 맵마다): 다시 설정 1번 뒤 대 2번 뒤 장식 28,835개 중 다른 것 0개, 태그·히트박스 목록 같음. 실제 Play 준비 끝 상태도 건너뜀 대 안 건너뜀 차이가 원래 흔들림(안 보이는 장식 73개)과 같음. 다르면 그 실행 동안 저절로 끔
- 첫 판 FPS 떨어짐과의 관계: 자동 시험 약 70번 Play 에서 느린 상태(화면 대기 1.7ms)는 **약 6초 멈춘 Play 에서만** 새로 생겼고(27번 중 5번), 5.4초 이하로 멈춘 Play 에서는 한 번도 생기지 않았습니다. Play 멈춤을 줄이는 것이 지금까지 가장 확실한 대책입니다.

### 재시작·종료 버튼

에디터에 저장 안 된 편집이 있으면 첫 번째 누름은 알리고 막습니다. 알림이 떠 있는 동안 한 번 더 누르면 저장하지 않고 재시작·종료합니다(게임의 "저장 안 하고 나가기"와 같은 방식이라 게임이 끄기를 막지 않음).

### 긴 곡 중반부터 계속 멈추던 것 고침

곡 중에는 메모리 정리(GC)를 미루다가 힙이 한계(RAM 의 40%, 최대 6GB)에 닿으면 "조금씩 치우기"로 바꾸는데, 한 번 바뀌면 힙이 줄어든 뒤에도 곡이 끝날 때까지 계속 치웠습니다. 치우기 한 바퀴가 끝날 때마다 잠깐 멈춰서, 25분 넘는 곡에서 약 8초마다 80ms 씩 멈췄습니다(사용자 로그, 곡 끝 힙은 1.2GB). 이제 한계의 절반 아래로 내려오면 다시 곡 중 정리를 멈춥니다.

### 그 밖

- (개발자용) 자동 시험: 모드 폴더의 `autotest.txt` 명령(맵 열기, 자동 플레이, 재생, 멈춤, 끄기 등)으로 사람이 누르지 않고 되풀이해 잽니다
- 긴 멈춤 중 화면 다시 내보내기를 시험했지만 효과가 없어 뺐습니다(개발자용 파일로만 켬)
- 바로잡기: 2.3.1 에서 "긴 멈춤 중 입력 큐 확인으로 멈춘 창 판정을 막는다"고 했지만, 자동 시험에서 판정이 여전히 가끔 나는 것을 확인했습니다. 첫 판 FPS 떨어짐과는 관계없었고(빠른 판에도 똑같이 있음), "응답 없음" 창은 계속 뜨지 않습니다.

## 2.3.1 에서 바뀐 것

### 에디터 전환이 빨라짐 (MAIJEUN 님 PR #1 추가분)

| 바꾼 것 | 내용 |
|---|---|
| 장식 이미지 버리지 않기 | 편집으로 나가거나 에디터에서 죽고 다시 할 때, 게임은 "안 쓰는 이미지 치우기"에서 장식 이미지를 전부 내렸다가 곧바로 디스크에서 다시 읽고 풉니다. 이 두 경우에만 치우기를 건너뛰고 쓰던 이미지를 그대로 씁니다(파일이 바뀌었으면 게임이 원래대로 다시 읽음). 큰 이미지 줄이기로 줄인 이미지가 편집으로 나가면 원본으로 돌아가던 것도 사라집니다. |
| 다시 하기 장식 설정 한 번 줄이기 | 에디터에서 죽고 다시 할 때 장식 전체 다시 설정을 연달아 두 번 합니다. 그 사이 코드는 장식을 읽지 않으므로 첫 번째를 건너뜁니다. 두 번째가 안 불리면 끝에서 대신 부릅니다(그때도 에디터 클릭용 충돌 상자는 꺼 둠). |
| 전환 시간 기록 | 편집으로 나가기, 에디터 재생 시작, 다시 하기에 걸린 시간을 로그에 `[전환]` 으로 남깁니다. |

설정의 "에디터 재생 시작·전환 빠르게" 에 묶여 있습니다.
측정: **Arche 에디터에서 죽고 다시 하기 3.3초 → 2.1초**, Hello (BPM) 2026 0.77~0.81초 → 0.38~0.55초. 편집으로 나갈 때 이미지 304장(Arche)을 다시 읽지 않음.
검증 (개발자용 자동 비교): 건너뜀 대 안 건너뜀의 다시 하기 뒤 장식 상태 차이가 안 건너뜀끼리의 차이(안 보이는 장식의 실제 위치 흔들림, Arche 73개)와 같음.

### 첫 판 FPS 떨어짐 막기 보강

2.3.0 이후 더 시험해 보니 "응답 없음" 창을 끄는 것만으로는 모자랐습니다. 윈도우는 창이 입력을 확인하지 않고 오래 멈춰 있으면 "응답 없음" 창과 별개로 멈춘 창으로 판정하는데, 이제 긴 멈춤(맵 불러오기, 에디터 Play) 동안에도 0.5초마다 **입력 큐를 확인만** 해서 이 판정을 막습니다. 입력을 꺼내지 않고, 다른 프로그램이 보낸 창 메시지도 그 자리에서 처리하지 않아 게임 동작에 끼어들지 않습니다(따로 만든 시험 프로그램으로 확인). 맵 불러오기 중 게임 창이 "응답 없음"이 되던 것도 사라집니다.

솔직한 현황: 곡이 시작되기까지 **5초 미만으로 멈춘 판은 한 번도 느려지지 않았지만**(약 15번, 플레이어용 Arche Play 4.7초 포함), 5초를 넘게 멈춘 판(개발자용 Arche 5.2~5.8초)은 위 대책을 모두 켜도 가끔 느려졌습니다(최근 7판 중 3판). 정확한 원인은 계속 찾고 있고, 느려져도 죽고 다시 하면 풀립니다. 다음 작업은 Play 시작 자체를 더 빠르게 해서 5초 아래로 내리는 것입니다.

### 고친 것

- 곡 요약의 GPU·CPU 평균에 엉뚱한 값(예: "GPU 평균 40256469.5ms")이 섞이던 것
- (개발자용) 필터 추적 설치 오류

## 2.3.0 에서 바뀐 것

[MAIJEUN](https://github.com/MAIJEUN) 님의 기여([PR #1](https://github.com/pding4569/StutterFix/pull/1): 장면 정리·소리 보호·필터 셰이더 준비 고침, libdeflate, 첫 판 VRAM 예측, 장식 애니메이션 확장)와 그 뒤의 작업을 합친 버전입니다. 감사합니다!
"최신 화면 출력 방식(실험)"만 빼고 전부 기본으로 켜져 있고, 화면·판정·소리는 원래 게임과 같습니다.

### 첫 판 FPS 떨어짐 고침

큰 맵을 에디터에서 Play 하면 곡이 시작되기까지 게임이 한 프레임에 5초 넘게 멈춥니다(Arche 5.2~5.8초). 이때 윈도우가 게임 창을 **"응답 없음" 창으로 바꿔치기**했고, 그 뒤로는 그 판 내내 프레임마다 1.7ms 를 더 기다려 FPS 가 크게 떨어졌습니다(Arche 약 320 → 200 FPS, 화면에 나오기까지도 약 5ms 늦음). 죽고 다시 하면(3초대 멈춤) 정상이라 "첫 판만 느리다"로 보였습니다. 원래 게임에서도 생기는 현상입니다.

이제 이 게임에서만 "응답 없음" 창을 끕니다(설정 → 그래픽 → **첫 판 FPS 떨어짐 막기**). 게임이 정말 멈췄을 때 "응답 없음" 표시가 안 뜨는 것 말고는 달라지는 것이 없습니다.

측정: 맵을 연 뒤 첫 에디터 Play 13번 중 10번이 약 200 FPS → 끈 뒤 새로 켠 게임에서 첫 Play 두 번 모두 약 300 FPS(화면 대기 0). 2.3.1 에서 보강했지만 완전히 없어지지는 않았습니다(위의 "첫 판 FPS 떨어짐 막기 보강" 참고).
원인이 아니었던 것: 화면 출력 방식, 멀티스레드 그리기, 프레임 시간 통계, NVIDIA 저지연 모드, 게임 위에 겹친 창, 디스코드. 곡 중에는 무엇을 해도(수직동기·프레임 제한·대기 단계 건너뛰기 등 10가지) 상태가 바뀌지 않았고, 판이 시작되기 전의 5초 넘는 멈춤만 공통이었습니다.

### 에디터에서 죽고 다시 하기 9초 → 3초

원래 게임은 에디터에서 **죽고 다시 할 때** 장식마다 에디터 클릭용 충돌 상자를 다시 켜 두었습니다(Play 로 시작할 때는 끔). Arche 는 2만 8천 개가 켜진 채로 남아 재시작 한 프레임이 5~6초, 곡 중 물리 계산이 한 프레임 최대 17.6ms 였습니다. 다시 하기 동안에는 켜지 않게 해서 **9.1초 → 3.2초**, 다시 한 판도 처음처럼 약 300 FPS 입니다.

### 맵 불러오기가 빨라짐

| 바꾼 것 | 내용 |
|---|---|
| **libdeflate 로 압축 풀기** | PNG 압축 풀기를 게임의 zlib 대신 [libdeflate](https://github.com/ebiggers/libdeflate)(MIT)로 합니다. 압축 풀기만 넣어 공식 소스에서 직접 빌드한 DLL 이 모드 DLL 안에 들어 있어 따로 챙길 파일이 없습니다. DLL 을 못 불러오거나 결과 크기가 맞지 않으면 원래 방식으로 다시 풉니다. |
| **JPG 도 여러 코어에서** | JPG 장식도 [libjpeg-turbo](https://github.com/libjpeg-turbo/libjpeg-turbo) 3.2.0 으로 작업 스레드에서 풉니다(예전에는 게임이 메인 스레드에서 한 장씩). 맵 폴더의 JPG 2,068장 전부 유니티가 푼 결과와 **바이트까지 같음**을 확인했고, 오류·CMYK·너무 큰 이미지는 원래 방식으로 풉니다. |
| **PNG 필터 되돌리기를 네이티브로** | 이 모드의 C 코드(SSE2)로 합니다. 맵 폴더 PNG 29,524장의 모든 줄(3,337만 줄)이 이전 C# 코드와 같음. |
| **DXT 미리 압축 3.4배** | PACL2 손실 압축을 대신하는 미리 압축을 [ISPC](https://github.com/ispc/ispc) 로 블록 여러 개씩 동시에 합니다(CPU 에 따라 AVX2 / SSE4.1 / SSE2). 결과는 이전 압축과 **바이트까지 같음**(맵 폴더 PNG 23,447장, 블록 33억 개 다름 0). MEGAMIX: 압축이 제때 안 끝나 게임이 메인 스레드에서 압축한 이미지 181 → 66장. |
| 맵 파일 읽기 | 게임의 JSON 해석기를 결과가 같은 빠른 해석기로 바꾸고(맵 파일 425개·망가뜨린 입력 2,584개·모든 글자 경우에서 결과와 예외 전부 같음), 이벤트를 읽을 때의 Enum 변환을 캐시합니다. Arche 6.4 → 5.2초. |
| 같은 이미지 장식 목록 | 게임이 장식마다 목록 전체를 훑던 것(제곱 시간)을 옆에 둔 집합으로. Arche 1.8초 → 0.02초. |
| 해독 중 복사 없애기 | 압축을 결과 메모리에 바로 풀고 그 자리에서 PNG 필터를 되돌립니다(2026 기준 약 10GB 의 복사가 사라짐). |
| 인터레이스 PNG 도 여러 코어에서 | 인터레이스(Adam7) PNG 도 작업 스레드에서 풉니다(예전에는 메인 스레드에서 한 장씩). |
| 해독 스레드 | 코어 수 - 1 (최대 8). 6코어 CPU 에서 코어 수만큼 쓰면 메인 스레드가 오히려 느려졌습니다. |

측정 (Hello (BPM) 2026, 긴 변 1536 같은 조건): 압축 풀기(작업 스레드 합계) 45.7초 → 5.2초, **전체 12.2초 → 8.1초**.
검증: 2025·2026 의 PNG 1,047장을 PIL 과 픽셀 단위로 비교, 원래 zlib 길과 libdeflate 길 모두 **다름 0**. 개발자용은 불러올 때마다 네이티브 결과 일부를 C# 과, JPG 일부를 유니티와 계속 대조합니다.

### 첫 판부터 VRAM 부족 막기

예전 "큰 이미지 줄이기(자동)"는 VRAM 이 가득 차 **한 번 끊긴 뒤에야** 다음부터 줄였습니다. 이제는 맵을 불러오기 직전에 이미지 파일의 머리 정보만 읽어 원본 크기를 어림하고, **비어 있는 VRAM 의 1.25배를 넘으면 첫 판부터 긴 변 3072** 를 씁니다(3072 보다 큰 이미지만 줄어듦). 그보다 더 내리는 것은 지금처럼 실제로 끊겼을 때만 합니다.

> 예전에 뺀 "전체가 VRAM 에 들어갈 때까지 줄이기"와는 다릅니다. 그 방식은 원본으로도 잘 돌던 CICADA3302 를 1024 까지 줄였습니다(그래픽카드는 그 순간 쓰는 이미지만 올려 두므로 전체 양으로는 끊김을 예측할 수 없음). 여기서는 첫 단계(3072)만 씁니다.

측정 (Hello (BPM) 2026 첫 판): 원본 11.4GB / 비어 있는 VRAM 5.3GB → 3072, 87장 줄임. 곡 중 VRAM 끊김 0, 곡 시작 뒤 가장 긴 프레임 35ms.
(전: 첫 판에 VRAM 이 가득 차 130ms 끊김 여러 번, 시스템 RAM 으로 5.5GB 넘침)

### 필터 셰이더 미리 준비 고침

필터가 처음 켜지는 순간 그래픽 드라이버가 셰이더를 만드느라 100ms 넘게 멈추는 것을 막는 기능이, 실제로는 거의 동작하지 않고 있었습니다.

- 일반 필터(Grayscale, Pixelate, Waves 등, 클래스 `CameraFilterPackLegacy_*`)는 한 번도 준비되지 않았습니다.
- 고급 필터는 게임의 효과 객체에서 찾다가 상황에 따라 0개를 찾았고, 셰이더 이름을 클래스 이름으로 짐작해 일부가 틀렸습니다(`AAA_SuperComputer` → 실제 `AAA_Super_Computer`).

이제 필터 목록은 맵 데이터에서 읽고, 셰이더 이름은 필터 코드(IL)가 실제로 부르는 `Shader.Find("…")` 문자열에서 읽습니다(고급 49종 + 일반 37종 전부 확인). 준비는 곡 시작이 아니라 맵 불러오기 끝에 해서 곡 시작 첫 프레임도 가벼워졌습니다. 측정: Hello (BPM) 2026 필터 109개를 불러오기 때 약 200ms 에 준비.

### 소리·판정은 절대 늦추지 않음

"효과 몰림 나누기"가 어떤 효과든 다음 프레임으로 미룰 수 있었습니다. 소리 재생(`PlaySound`)은 오디오 시계로 예약되는데 늦게 불리면 그 시각이 지나 **소리가 늦게 납니다**. 이제는 게임 코드로 확인한 **화면 전용 효과 20종만** 미루고, 소리·판정·진행 효과(플레이어 죽이기, 체크포인트, 오프셋, 속도, 입력, 프레임 제한 등)는 원래 프레임에 그대로 실행합니다. 히트박스 장식이 있는 맵에서는 장식 이동도 미루지 않습니다.

### 최신 화면 출력 방식 (실험, 기본 끔)

게임은 D3D11 에서 윈도우가 게임 화면을 통째로 복사해 합성하는 옛 방식(BitBlt)으로 화면을 내보냅니다(게임의 `boot.config` 에 `force-d3d11-bitblt-model=` 줄이 들어 있음). 설정 → 그래픽 → **최신 화면 출력 방식**을 켜면 이 줄을 빼서 최신 방식(Flip)으로 바꿉니다. 측정: 같은 구간 300 → 318 FPS, 화면에 나오기까지 약 6.2 → 4.4ms. 수직동기를 끈 채 게임 위에 다른 창이 없으면 화면이 가로로 찢어져 보일 수 있어 기본은 끔입니다. 끄거나 모드를 끄면 원래 줄을 되살립니다(다음 실행부터).

### 게임 밖 원인 알려 주기

곡 중에 윈도우가 화면을 넘겨받느라 기다리는 시간이 늘면(오버레이·항상 위 창, 원격 데스크톱·녹화 프로그램 등), 설정 창 홈에 **원인 프로그램 이름과 그것이 없을 때의 FPS** 를 보여 줍니다(닫기 가능).

### 장식 애니메이션 넓히기

- "장식 애니메이션 직접 처리"가 **피벗·시차 오프셋·시차 배율**도 맡습니다(예전에는 이것이 섞인 효과는 통째로 DOTween). 개발자용 대조: 진짜 DOTween 과 나란히 41,586프레임, **다름 0**.
- 끝난 애니메이션 기록을 같은 자리에서 다시 써서, 곡 중 메모리가 덜 쌓입니다(2026: 41,117개 중 17,182개 재사용).

### 고친 버그

| 증상 | 원인과 수정 |
|---|---|
| 곡 중간에 에디터로 나가면 일부 장식이 남고, 다시 플레이해도 그대로 나옴 | 모드가 직접 돌리는 애니메이션과 밀린 효과가 게임의 정리(`scnGame.ResetScene`)에 잡히지 않고 계속 돌았음. 장면을 되돌릴 때 모드 쪽도 함께 정리 |
| 곡 중 일시정지(ESC) 뒤 다시 하면 일부 효과·타일 색이 원래와 다르게 남음 | 일시정지 해제를 "곡 새로 시작"으로 봐서 밀린 효과를 버렸음. 진짜 새로 시작할 때만 비움 |
| "효과 몰림 나누기"를 끄면 이미 밀린 효과가 영영 실행 안 됨 | 꺼도 밀린 것은 끝까지, 새 효과보다 먼저 실행 |
| 곡 중에 모드를 끄면 장식이 움직이다 굳음 | 내리기 전에 밀린 효과·애니메이션을 모두 마무리 |
| 효과 하나가 예외를 던지면 효과 나누기가 다음 재시작까지 꺼짐 | 효과 감싸기 뒷정리를 예외에도 반드시 도는 방식(finalizer)으로 |
| 에디터로 돌아가면 선택 테두리가 곡 중 위치에 남음 / 글꼴을 바꿔도 글자 테두리 크기가 안 바뀜 | 되돌리는 순간과 편집 중에는 원래 동작 |
| 개발자용이 오히려 끊기고 연출이 튐 | 20초마다 픽셀 비교(230ms 멈춤, 시간으로 움직이는 필터가 한 번 더 진행)와 느린 함수 찾기(5초마다 장면 전체 훑기)를 기본으로 끔 |
| 곡 중 실시간 모니터 위에서 마우스로 치면 그 입력이 무시될 수 있음 | 게임은 마우스를 누른 순간 포인터가 화면의 UI 위면 그 프레임 입력을 버림. 곡 중에는 모니터가 마우스를 받지 않음(곡 중 보기 바꾸기는 Shift+Insert) |
| 곡이 없는 맵·음악이 먼저 끝나는 맵에서 곡 중에 메모리 정리가 돌 수 있음 | "10초간 조용함" 을 음악뿐 아니라 타일 진행으로도 판단 |
| `boot.config` 의 줄 끝이 바뀜(LF → CRLF) | 원래 줄 끝 그대로 씀 |

### 그 밖

- **디스코드 서버**: [discord.gg/csys9ZAeD6](https://discord.gg/csys9ZAeD6) — 버그 제보, 기능 아이디어, 질문. 설정 창 정보 페이지의 "디스코드 서버 열기" 버튼, UMM 모드 목록의 홈페이지 링크로도 갑니다.
- 문제 보고용 로그 zip 은 디스코드 서버에 올리거나 narooh 에게 DM 으로 보내 주세요.

## 기능

기본으로 전부 켜져 있고, 설정 창에서 하나씩 끌 수 있습니다.

### 플레이

| 기능 | 하는 일 | 측정 |
|---|---|---|
| 메모리 정리 미루기 | 플레이 중 GC(메모리 정리)로 멈추는 것을 막고, 곡이 끝나고 몇 초 뒤 한 번에 정리합니다. 실패·재시작 때는 쌓인 양이 클 때만 정리하고, 맵 파일을 읽는 동안에도 정리를 멈춥니다(RAM 12GB 이상이고 넉넉할 때만, 2.2.0). | 평균 106 → 124 FPS, 33ms 넘는 구간 118 → 12 (125구간 중). 재시작 정리 0.2~0.5초 → 전체 약 0.1초, Arche 맵 파일 읽기 7.6 → 6.3초 |
| 효과 몰림 나누기 | 한 박자에 화면 효과 수십 개가 몰리면 몇 프레임에 나눠 시작합니다. 소리·판정 효과는 나누지 않습니다. | |
| 타일 색 바꾸기 나누기 | 타일 수천 개의 색을 바꾸는 이벤트를 조금씩 나눠 칠합니다. 먼 타일이 몇 프레임 늦게 바뀔 뿐 결과는 같습니다. | 한 번에 41ms → 4ms |
| 애니메이션 목록 재정렬 막기 | 효과가 많을 때 DOTween 이 목록을 반복 재정렬하느라 멈추는 것을 막습니다. | 한 프레임 435ms 중 382ms 였던 재정렬 제거 |
| 글자 장식 최적화 | 같은 글자를 매 프레임 다시 넣는 글자 장식을 건너뜁니다. PACL2 같은 모드와 함께 쓸 때 효과가 큽니다. | |
| 즉시 이동 최적화 | 즉시 옮기는 장식 이벤트를 애니메이션 없이 바로 처리하고, 위치 마무리 계산을 한 번으로 묶고, 값이 그대로인 쓰기를 건너뜁니다. | 장식 14,416개 프레임 148 → 93~99ms |
| 즉시 이동 직접 처리 | 효과가 몰리는 순간, 속성마다 애니메이션 객체 5개를 만드는 과정 없이 "이전 것 끝내기 + 최종 값 한 번"만 합니다. | Arche 효과 몰림 68 → 36ms |
| 투명 장식 빠른 처리 | 투명한 장식을 옮기거나 같은 색을 다시 넣을 때 게임 함수 사슬을 건너뜁니다(결과가 같은 경우만). | Arche 효과 몰림 36 → 29ms |
| 장식 이동 루프 | 길이 0 장식 이동 효과를 게임 코드 대신 모드의 루프로 돕니다(같은 순서, 같은 설정 함수). 이미지·마스크를 바꾸는 효과도 맡습니다. | Arche 가장 무거운 효과 프레임 27 → 25ms |
| 장식 애니메이션 직접 처리 | 길이 있는 장식 이동(위치·피벗·시차·회전·크기·색·불투명도)을 DOTween 객체 없이 모드가 진행합니다. 시간, 이징, 콜백 순서를 DOTween 과 똑같이 맞췄습니다. | Arche 62~64초 25~28 → 약 22ms |
| 미리 확인 | 몇 초 뒤 시작할 큰 장식 이동 효과를 미리 검사해 두고, 아무것도 안 바꾸는 효과면 대상을 훑지 않고 넘어갑니다. | Arche 장식 1만 4천 개 프레임 25 → 19ms |
| 장식 위치 계산 줄이기 | 위치 마무리 계산을 묶고, 플레이 중 필요 없는 편집기 작업과 같은 프레임에 게임이 다시 하는 계산을 건너뜁니다. | |
| 투명한 장식 그리지 않기 | 투명도 0 인 이미지 장식을 그리기에서 빼고, 보이게 되는 순간 바로 다시 그립니다. 투명한 장식의 위치는 보일 때 한 번 반영합니다. | Arche 평균 95 → 105 FPS, GPU 7.6 → 3.4ms |
| 장식 순회 줄이기 | 안 보이고 바뀔 일이 없는 장식을 매 프레임 갱신 목록에서 빼 두고, 바뀌는 순간 다시 넣습니다. 히트박스 판정도 히트박스 있는 장식만 봅니다. | Arche 훑는 장식 28,835 → 평균 325개, 평균 107 → 170 FPS |
| 변화 없는 파티클 갱신 건너뛰기 | 파티클 장식이 값이 그대로여도 매 프레임 엔진에 다시 넣는 모양 크기와 속도를, 지난번과 같으면 건너뜁니다(2.2.0). | |

<details>
<summary>원래 게임과 같은지 어떻게 확인했나 (개발자용 자동 대조)</summary>

- 즉시 이동: 게임 결과와 비트 단위로 26만 개 비교, 효과 몰림 대조 3,217번 다름 0
- 투명 장식 빠른 처리: 대조 6,109번 다름 0
- 장식 이동 루프: 루프 뒤 같은 효과를 원래 코드로 한 번 더 돌려 아무것도 안 바뀌는지 1,897번, 장식 3,419개 다름 0
- 장식 애니메이션: 진짜 DOTween 을 옆에 같이 돌려 매 프레임 비트 단위 비교, 32만 프레임 다름 0 (피벗·시차 포함 41,586프레임 다름 0)
- 미리 확인: 건너뛴 효과를 실제로 돌려 봐도 바뀐 장식 0
- 투명한 장식 그리지 않기: 같은 프레임을 두 방식으로 그려 3440×1440 전체 픽셀 차이 0, 위치 대조 14,623개 일치
- 장식 순회 줄이기: 안전망 검사 422번에 놓친 깨움 0, 위치 대조 90,408개 다름 0

</details>

### 맵 불러오기

| 기능 | 하는 일 | 측정 |
|---|---|---|
| 이미지 빠르게 불러오기 | 장식 이미지(PNG, 2.3.0 부터 JPG 도)를 여러 코어에서 동시에 풉니다. 압축 풀기는 libdeflate, JPG 는 libjpeg-turbo, 필터 되돌리기와 DXT 압축은 네이티브 코드(2.3.0). 흑백+알파·16비트 PNG 도 유니티와 바이트까지 같게 미리 풀고(2.2.0), PACL2 의 이미지 손실 압축이 켜져 있으면 그 압축(메인 스레드에서 한 장씩)을 여러 코어에서 미리 해 둔 것으로 대신합니다(압축 오차는 시험한 모든 이미지에서 유니티 압축 이하, 2.2.0). | 700장 맵 67 → 38초, 2026 12.2 → 8.1초, PACL2 와 함께 Arche 장식 준비 25.3 → 18.5초 |
| 불필요한 정리 건너뛰기 | 편집으로 돌아올 때 게임이 부르는 에셋 정리(한 번에 120~200ms)를 건너뜁니다. 맵을 새로 열 때의 정리는 이전 맵 메모리를 풀기 위해 그대로 둡니다(2.2.0). | |
| 에디터 재생 시작 빠르게 | 이미지 파일 수정 시각을 파일마다 한 번만 읽고, 장식이 하나도 안 바뀌었으면 장식 전체 다시 설정을 두 번 대신 한 번만 하고, 에디터 클릭용 충돌 상자를 넣은 반대 순서로 끕니다(2.2.0). 죽고 다시 할 때는 충돌 상자를 아예 켜지 않고, 맵 파일 읽기와 같은 이미지 장식 목록도 빠르게 합니다(2.3.0). 개발자용 검증: 건너뛴 다시 설정의 차이 0. | Arche 8.6 → 4.3초, 죽고 다시 하기 9.1 → 3.2초 |
| 게임 메모리 누수 막기 | 게임의 사용자 지정 FPS 효과가 켤 때마다 새로 만들고 풀지 않던 화면 크기 버퍼(4K 에서 약 40MB)를 풀고, 재시작마다 게임 화면 버퍼를 괜히 다시 만드는 것을 막습니다(2.2.0). | |
| 큰 이미지 줄이기 (기본 자동) | 필요한 VRAM 이 크게 넘칠 맵은 첫 판부터 긴 변 3072, 그래도 VRAM 이 가득 차 끊기면 기억해 두었다가 한 단계씩(2048 → 1536 → 1024) 줄입니다. 화면에 보이는 크기는 그대로이고 선명도만 조금 낮아집니다. 설정 창에서 기억한 맵을 지울 수 있습니다. | 이미지 2,000장 맵(VRAM 8GB) 150~200ms 멈춤이 3072 에서 사라짐 |
| 필터 셰이더 미리 준비 | 맵에서 쓰는 필터(일반·고급)의 셰이더를 불러오기 끝에 미리 만들어 둡니다. | 2026 필터 109개 약 200ms |

### 그래픽

| 기능 | 하는 일 | 측정 |
|---|---|---|
| 멀티스레드 그리기 | 게임 폴더의 `boot.config`에 `force-gfx-jobs=legacy` 한 줄을 넣어 그리기 준비를 여러 코어에 나눕니다. 원래 파일은 백업해 두고, 모드를 끄면 되돌립니다. | D3D11 140 → 160 FPS |
| 블렌드 장식 빠르게 그리기 | 더하기(Linear Dodge) 블렌드 장식을 화면 복사 없이 그래픽카드 기본 섞기로 그립니다. 원래는 장식 하나마다 화면 전체를 복사했습니다. | 1,500개 장면 약 11 FPS → 끊김 없음, 픽셀 차이 0 |
| 첫 판 FPS 떨어짐 막기 | 이 게임에서만 윈도우의 "응답 없음" 창을 끕니다. 큰 맵에서 5초 넘게 멈춘 뒤 그 판 내내 FPS 가 떨어지던 것을 막습니다(2.3.0). 끄면 다음 실행부터. | Arche 첫 판 약 200 → 300 FPS |
| (실험, 기본 끔) 최신 화면 출력 방식 | `boot.config` 의 `force-d3d11-bitblt-model=` 줄을 빼 D3D11 화면 출력을 Flip 방식으로 바꿉니다. 수직동기를 끄면 찢어짐이 보일 수 있습니다(2.3.0). | 300 → 318 FPS, 화면까지 6.2 → 4.4ms |

### 저사양

약한 컴퓨터를 위한 기능입니다. 게임 밖 설정을 바꾸거나 화면이 조금 달라지는 것을 감수하므로 **전부 기본 꺼짐**입니다. 설정 창의 속도계 아이콘(저사양) 페이지에서 켭니다.

| 기능 | 하는 일 |
|---|---|
| 게임 우선순위 높이기 | 다른 프로그램보다 게임이 CPU 를 먼저 씁니다. 끄면 원래대로 돌아갑니다. |
| 윈도우 절전 제한 끄기 | 윈도우 11 이 게임을 "효율 모드"로 느린 코어에 몰지 않게 하고, 타이머 정밀도를 1ms 로 올립니다. 노트북에 효과가 큽니다. |
| 음악 반응 계산 끄기 | 매 프레임 하는 음악 주파수 분석을, 그 값을 쓰는 타일 색 방식(Volume)이 없을 때 건너뜁니다. 소리 재생에는 영향이 없습니다. |
| 효과 몰림 더 잘게 나누기 | 몰린 화면 효과를 더 짧게 끊어(10 → 5 / 3ms) 나눕니다. 뒤쪽 효과가 몇 프레임 늦을 수 있습니다. |
| 게임 화면 해상도 (10~100%) | 게임 화면만 작게 그려 늘려 보여 줍니다(HUD·설정 창은 선명). 늘리는 방식: 부드럽게 / **FSR 1** / 도트처럼. 50% 에서 GPU 시간 약 절반. |
| FSR 1 늘리기 | AMD FidelityFX Super Resolution 1(MIT)로 늘립니다. 자동 검증(Arche, 50%): 선명도가 원래와 같음(보통 늘리기는 74%). |
| 자동 해상도 | 그래픽카드가 바빠 목표 FPS 를 못 맞출 때만 해상도를 10% 씩 낮추고, 여유가 생기면 다시 올립니다. |
| 메뉴·에디터 FPS 제한 | 플레이 중이 아닐 때 FPS 를 30 / 60 으로 묶어 발열을 줄입니다. 곡을 시작하면 바로 원래대로. |
| 장식 이미지 최대 크기 | 장식 이미지 긴 변을 1024 / 512 로 줄입니다. 다음에 여는 맵부터. |
| 화면 밖 파티클 멈추기 | 파티클 장식이 화면 밖에 있는 동안 시뮬레이션을 멈춥니다. 다시 들어오면 멈춘 곳부터 이어가서 모양·시점이 조금 다를 수 있습니다(2.2.0). |
| 이미지 압축해서 불러오기 | 장식 이미지를 DXT 로 압축해 올립니다. 그래픽 메모리가 4분의 1(투명 없는 이미지는 8분의 1)로 줄고, 압축은 불러오는 동안 여러 코어에서 미리 합니다. 손실 압축이라 가까이서 보면 조금 뭉개질 수 있습니다(2.2.0). |
| (실험) 늘린 화면 선명도 보정 | 해상도를 낮췄을 때 게임에 들어 있는 Sharpen 셰이더로 보정합니다. |
| (실험) 반만 그리기 + 카메라 보정 | 게임 화면을 두 프레임에 한 번만 그리고, 사이 프레임은 카메라가 움직인 만큼 옮겨 보여 줍니다. 행성·장식·필터는 절반 속도로 갱신됩니다. |

### 편의

- **재시작 메뉴**: 게임 재시작 / 이 맵으로 재시작(메인 메뉴에서는 에디터에서 마지막으로 연 맵) / **게임 종료**. 저장 안 된 편집이 있으면 하지 않습니다.
- **자동 업데이트**: 게임을 켜고 15초 뒤(그 뒤로 3시간마다) GitHub 최신 릴리스를 확인해, 새 버전이 있으면 곡 밖에서 알아서 받아 설치하고 **패치노트**를 보여 줍니다. 게임을 다시 켜면 적용됩니다. 곡 중에는 아무것도 하지 않습니다. 정보 페이지에서 끄면 알림만 보고 버튼으로 받습니다.

### 다른 모드와 함께

- **겹치는 기능 알아서 쉬기** (2.2.0): Quartz 최적화 모듈과 PACL2 메모리 최적화의 설정을 읽어서, 같은 일을 하는 이 모드 기능은 쉬게 하고 설정 창 홈에 무엇이 겹치는지 알려 줍니다. 다른 모드의 설정 파일은 바꾸지 않습니다.
- **자동 보호** (2.2.0): 이 모드 기능에서 오류가 5번 반복되면 그 기능만 이번 실행 동안 끄고, 같은 게임 함수를 고치는 모드를 함께 알려 줍니다. 게임이 두 번 연속 비정상 종료되면 다음 실행은 **안전 모드**(게임에 깊이 관여하는 기능을 끔)로 켜집니다. 비정상 종료 감지를 위해 실행 중에는 모드 폴더에 `session.lock` 이 생기고, 정상 종료하면 지워집니다.

## 실시간 모니터

게임 화면 옆에 FPS, CPU, GPU, VRAM, RAM 사용량과 끊김 알림을 띄웁니다. 설정 창 "모니터"에서 위치, 크기, 투명도, 보여 줄 항목, 알림 방식을 고릅니다. 켜 둬도 프레임당 약 0.1ms 입니다.

끊기면 원인을 추정해 알려 줍니다(메모리 정리, 효과 몰림, GPU 과부하, 게임 처리, 모드 작업, 게임 바깥). 맵·모드 로딩, 모드 창을 쓰는 동안, 곡 시작 직후 첫 타일 전의 멈춤은 끊김으로 세지 않고 회색으로 따로 적습니다.

## 문제 보고

끊기거나 오류가 났다면 설정 창 **정보 → 로그 파일 만들기**(또는 UMM 모드 설정의 **문제 보고용 로그 만들기**)를 누르세요. 바탕화면에 `StutterFix-log-날짜.zip`이 생깁니다. 이 파일을 [모드 디스코드 서버](https://discord.gg/csys9ZAeD6)에 올리거나 디스코드 **narooh** 에게 DM 으로 보내 주세요. 기능 아이디어나 질문도 디스코드 서버로 받습니다.

들어가는 것: 컴퓨터 사양, 이 모드 설정, 설치된 모드 목록, 게임 로그(이번 실행과 직전 실행), 실시간 모니터의 끊김 기록. 로그 안의 윈도우 사용자 이름은 가려지고, 자동으로 어디에 올리지는 않습니다.

## 모드를 끄면

UMM 에서 끄면 모든 변경을 즉시 되돌립니다(패치, GC 상태, 작업 스레드, 설정 창). 곡 중에 끄면 밀린 효과와 장식 애니메이션을 마무리한 뒤 내립니다. 멀티스레드 그리기와 최신 화면 출력 방식은 다음 실행부터 원래대로 돌아가고, "응답 없음" 창 끄기(첫 판 FPS 떨어짐 막기)는 윈도우에 되돌리는 기능이 없어 게임을 다시 켤 때까지 그대로입니다.

## 그래도 끊긴다면

- 전체 화면 필터가 아주 많이 겹치는 구간은 그래픽카드 성능 한계입니다.
- 백그라운드 프로그램이 순간적으로 CPU 를 가져가 끊길 수 있습니다(저사양 페이지의 "게임 우선순위 높이기").
- 원격 데스크톱(StarDesk 등)·화면 녹화 프로그램이 켜져 있으면 화면을 캡처하는 동안 게임이 매 프레임 화면을 넘기며 기다려 FPS 가 크게 떨어질 수 있습니다(측정: 같은 구간 250 → 186 FPS, 끄자 6판 모두 280~293 FPS). 게임할 때는 끄세요. 2.2.1 부터 곡 중에 이런 대기가 생기면 그때 게임 창 위에 겹친 창과 GPU 를 쓰는 다른 프로그램을 로그에 남기고, 2.3.0 부터는 설정 창 홈에 그 프로그램 이름을 보여 줍니다.
- Steam 실행 옵션에 `-force-d3d12 -force-gfx-jobs native`가 있으면 곡 중 60~80ms 씩 멈출 수 있습니다. 빼는 것을 권합니다.
- 맵에 **SetFrameRate** 이벤트가 있으면 그 구간의 낮은 FPS 는 맵이 의도한 연출입니다.

## 두 가지 버전

| | 플레이어용 | 개발자용 |
|---|---|---|
| 위의 모든 기능 | O | O |
| 끊김 기록, 엔진 단계별·함수별 시간, 원래 게임과의 자동 대조, 진단 단축키(F6~F11), Ctrl+F5 다시 불러오기 | | O |

일반 플레이에는 **플레이어용**을 쓰세요. 개발자용은 대조 검증 때문에 조금 무겁고, 끊김 원인을 추적할 때 씁니다. 픽셀 비교와 느린 함수 찾기는 플레이를 방해해서 설정 창에서 켰을 때만 돕니다.

## 빌드

.NET SDK 8.0 이상. `StutterFix.csproj`의 `GameManaged` 경로를 게임 설치 경로에 맞게 고칩니다.

```bash
./pack.sh
```

두 버전을 빌드해 `dist/`에 UMM 설치용 zip 을 만듭니다. 하나만 빌드하려면:

```bash
dotnet build -p:Edition=Player   # 플레이어용 -> bin/Player/StutterFix.dll
dotnet build                     # 개발자용 -> bin/Debug/StutterFix.dll
```

`native/libdeflate.dll`은 [libdeflate](https://github.com/ebiggers/libdeflate) 1.24(태그 `v1.24`, 커밋 `96836d7`)의 압축 풀기 부분만 MSVC 14.44 x64 로 빌드한 것이고, 모드 DLL 안에 넣어 배포됩니다. 빌드 방법은 `native/build-libdeflate.bat` 에 있습니다(Visual Studio 의 MSVC 와 Windows SDK 가 필요, SDK 는 NuGet 의 `Microsoft.Windows.SDK.CPP` 패키지를 풀어 써도 됨). 핵심 명령:

```bat
cl /O2 /GL /MT /LD /Brepro /DLIBDEFLATE_DLL /I. lib\deflate_decompress.c lib\zlib_decompress.c lib\adler32.c lib\utils.c lib\x86\cpu_features.c /Fe:libdeflate.dll /link /LTCG /Brepro
```

`/Brepro` 로 빌드 시각이 빠져 같은 도구면 매번 똑같은 파일이 나옵니다. 지금 들어 있는 DLL: 120,320 바이트, SHA-256 `212D8BA3B6B0826A41AB002B7C1727FCF92682CC11703060F44293A9AF2A78C2` (MSVC 14.44.35207, Windows SDK 10.0.26100.4188).

검증: 맵 폴더의 PNG 29,524장(압축 9.8GB, 풀린 양 약 248GB)을 .NET 의 zlib 과 이 DLL 로 각각 풀어 바이트 단위로 비교, 다름 0.

`native/sfnative.dll`은 이 모드가 직접 쓴 C 코드(`native/sfnative/sfnative.c`)로, 이미지 불러오기의 PNG 필터 되돌리기(SSE2)와 DXT 압축을 네이티브로 합니다. 같은 도구로 `native/build-sfnative.bat` 로 빌드합니다(`/Brepro`). 검증: 필터 되돌리기는 맵 폴더 PNG 전부의 모든 줄을 C# 코드와 바이트 비교, DXT 는 Arche 이미지 251장의 블록 6,280만 개가 C# 과 모두 같음. DLL 을 못 불러오면 C# 으로 동작합니다.

DXT 압축은 [ISPC](https://github.com/ispc/ispc)(인텔 SPMD 컴파일러, v1.31.0, BSD-3-Clause)로 같은 계산을 블록 여러 개씩 동시에 합니다(`native/sfnative/sfdxt.ispc`). SSE2 / SSE4.1 / AVX2 코드가 한 DLL 에 들어 있고 CPU 에 맞는 것을 실행 중에 고릅니다. 실수 계산 순서를 C 코드와 같게 두려고 `--opt=disable-fma` 로 빌드합니다. 검증: 세 경로 모두 Arche 블록 6,280만 개가 이전 C 코드·C# 과 바이트까지 같음. 속도(10억 픽셀): 이전 11.9초 → AVX2 3.5초, SSE4.1 5.9초, SSE2 8.4초. 빌드에는 `ispc.exe` 경로가 더 필요합니다(`build-sfnative.bat` 의 4번째 인자).

`native/turbojpeg.dll`은 [libjpeg-turbo](https://github.com/libjpeg-turbo/libjpeg-turbo) 3.2.0(태그 `3.2.0`, 커밋 `c85e6b9`)을 SIMD(NASM 3.02) 포함, 정적 CRT 로 빌드한 것이고 JPG 장식 이미지를 작업 스레드에서 풉니다. 빌드는 `native/build-turbojpeg.bat`(CMake 4.4.3 + NMake, `/Brepro`). 지금 들어 있는 DLL: 1,165,312 바이트, SHA-256 `6AB563B85C6A032620E285D23B25B8D9B48D90FC86DD998BF3BE7F2D7AE3C61F`. 검증: 맵 폴더의 JPG 2,068장을 유니티 6000.3.10f1 LoadImage 결과와 바이트 단위로 비교, 기본 설정(정확한 DCT, 부드러운 업샘플)에서 다름 0. 오류·경고가 나는 파일, CMYK, 최대 텍스처 크기를 넘는 이미지는 원래 방식(유니티)으로 풉니다.

## 환경

- ADOFAI r148 / Unity 6000.3.10f1 (Mono)
- Unity Mod Manager 0.32.5
- Windows x64 (libdeflate 를 못 불러오면 원래 zlib 로 동작)

## 라이선스와 사용한 외부 코드

- [libdeflate](https://github.com/ebiggers/libdeflate) 1.24 — MIT, `native/libdeflate-LICENSE.txt`
- [libjpeg-turbo](https://github.com/libjpeg-turbo/libjpeg-turbo) 3.2.0 — IJG License + Modified (3-clause) BSD License + zlib License, `native/libjpeg-turbo-LICENSE.md`, `native/libjpeg-turbo-README.ijg`. This software is based in part on the work of the Independent JPEG Group.
- AMD FidelityFX Super Resolution 1 — MIT, `fsr/license.txt`
- 빌드 도구로만 쓴 것: [ISPC](https://github.com/ispc/ispc) 1.31.0 (BSD-3-Clause, DXT 압축 코드 컴파일), CMake, NASM

---

## English

Stutter Fix reduces mid-play hitches and level loading times on heavy custom levels in A Dance of Fire and Ice. **Visuals, judgement and audio stay identical to the vanilla game**; features that may change how things look (the low-end page) are off by default. Every feature was built after measuring a real hitch, and dev builds cross-check the results against the original game code.

**Install:** download `StutterFix-x.y.z-player.zip` from [Releases](https://github.com/pding4569/StutterFix/releases) and install it with Unity Mod Manager (Install Mod), or extract it to `A Dance of Fire and Ice/Mods/StutterFix/`. Restart the game once more to enable multithreaded rendering. Press **Insert** for the settings window (Korean/English) and **Shift+Insert** for the live monitor.

**2.4.4:** faster editor Play (the light decoration reset from 2.4.0 now also runs at play start for decorations untouched in the editor; Arche 3.3–3.9 s → 2.8–3.0 s from the second Play, verified identical on 4 levels), and less garbage during play (legacy keyboard check without lambdas/temp lists; no per-frame GUILayout setup for the mod's own overlay and closed settings window): 1-hour level 1.45 → 1.19 MB/s. Also: **floor effect components are reused** instead of destroyed and re-added on every editor exit / play start (reset to a freshly-constructed state; lookups hide kept components and return the original order): Arche play start 3.1 → 2.5 s, exit 1.5 → 1.1 s, verified field-by-field identical on 4 levels; **far off-screen tiles are left out of rendering** on levels with over 3,000 tiles (Unity was testing every tile for every camera each frame): 90k-tile level 295 → 420 FPS, verified with 0 on-screen culled tiles and 0-pixel render differences; the per-beat OnBeat broadcast now only calls tiles where it does something (floors re-register themselves on every reset, so the list held each tile several times): 90k tiles BPM 32000 166 → 287 FPS, BPM 100000 112 → 159 FPS, and no more editor stutter right after opening a big level; the old tiles' materials that piled up on every level open in the editor (~128 MB per open for 90k tiles, even without mods) are now freed; **automatic updates** (checks at launch and every 3 hours, downloads and installs a new version by itself outside of levels, applied on the next launch; install work runs off the main thread), and the editor's unused workshop thumbnail camera no longer redraws a 512x512 image every frame (0.07–0.09 ms main thread per frame; saved thumbnails byte-identical).


**2.4.3:** less garbage during play (1-hour level 2.21 → 1.45 MB/s): the tile update no longer allocates a closure object for the volume color mode on every tile every frame, and async keyboard / mouse input no longer build HashSets, LINQ iterators and lambdas on every call. Results are identical; dev builds compared every call against the original code while real keys were sent (69,190 calls each, 0 differences).

**2.4.2:** in-song GC now happens only in input-free gaps. From 80% of the heap limit the mod waits for a gap where the next tile is at least "predicted pause + 0.15 s" away (or the game is paused) and cleans up in one step right there (6 GB heap: 195–206 ms, finished inside the gap), so the pause never overlaps a tile input. Only if no such gap appears before the limit does it fall back to 2.4.1's single incremental start. Dev test (1-hour level, 40 MB/s synthetic garbage): all three cleanups landed in gaps (0.51 / 1.79 / 2.79 s), none at the limit.

**2.4.1:** fixed endless GC hitches in very long songs. When the heap hit the in-song limit (40% of RAM, max 6 GB), the mod kept starting new incremental GC cycles every few frames because Unity finishes a started cycle on its own and always reports "more work left"; each cycle stopped the game once at the end (1-hour level: 74 ms every 0.3 s until leaving). Now one cycle is started and the mod only watches the heap drop (6 GB → 0.6 GB in 0.7 s); 1 hitch per limit hit instead of 59 in the dev reproduction.

**2.4.0:**
- **Faster return to the editor** (reported hitch when leaving play mode): the game re-runs Setup on every decoration when you stop playing (Arche: 28,835 decorations, 1.5 s). Values are now recorded at play start; decorations that did not change during the run (all fields the effects change are equal, and no setter that leaves no field behind — depth, mask depth, hitbox — was called) get a light reset that skips the settings that would come out the same and redoes, through the game's own functions, only what can differ (placement, position/parallax/rotation/scale, color, visibility, tag lists, mask/tiling notifications, filter list, editor click collider). Changed decorations, text/object/particle/blend/mask/hitbox decorations and changed level data use the original Setup. GC work postponed during play is now done in 3 ms slices in the editor instead of one 0.7–1 s freeze, and the decoration data fingerprint is computed once per frame instead of three times on editor Play. Player build, Arche (automated, same PC): leaving after a long run 3.18 s → 1.60 s, other exits 2.28–2.57 s → 1.42–1.73 s, editor Play 3.50–4.05 s → 3.32–3.65 s. Dev builds compare the light result with the original Setup in the same frame (all script fields, all transforms, renderer/material properties, colliders, manager lists): identical on 8 levels, including after retries.
- **Huge levels (hundreds of thousands of tiles) open much faster:** tile creation was O(n²) because every new tile was re-parented under one "Floors" object; levels with 30,000+ tiles keep tiles at the root (360k tiles: 294 s → 60 s). Levels with 200,000+ tiles drop the hidden per-tile editor number canvas (360k tiles: 9.6 GB → 7.1 GB).
- **Automatic PC tuning:** a 0.2 s benchmark on first launch (CPU, GPU bandwidth, integrated GPU, RAM, refresh rate) turns on only the low-end features that match the weak side; nothing changes on strong PCs. Undo from the home card or the low-end page.
- **2.3.2–2.3.4:** the first-run FPS drop's real cause fixed (GC is now paused with Manual instead of Disabled mode), deferred GC no longer stops working after dying outside the editor, long-song GC slicing stops once the heap is back under half the limit, restart/quit buttons warn once about unsaved edits.

**2.3.1:**
- **Faster editor transitions** (more from [PR #1](https://github.com/pding4569/StutterFix/pull/1) by MAIJEUN): decoration images are kept instead of unloaded and re-read from disk when returning to the editor or retrying in the editor, and the retry resets decorations once instead of twice. Arche editor retry 3.3 s → 2.1 s.
- **First-run FPS drop, more mitigation:** during long freezes (level load, editor Play) the input queue is checked every 0.5 s without removing anything, so Windows no longer marks the game window as hung (verified with a standalone test; sent messages are not processed mid-frame). Honest status: runs whose Play freeze stayed under 5 s were never slow (about 15 runs), but runs with a freeze over 5 s can still occasionally start slow (3 of the last 7 in the dev build); a retry clears it. Next: make Play start faster so it stays under 5 s.
- Fixed bogus values in the song summary's GPU/CPU averages.

**2.3.0** (includes [PR #1](https://github.com/pding4569/StutterFix/pull/1) by [MAIJEUN](https://github.com/MAIJEUN) — thank you!):
- **First-run FPS drop fixed:** when a big level froze the game for more than 5 s after pressing Play in the editor, Windows swapped the window for a "Not responding" ghost window, and every frame of that run then waited an extra 1.7 ms (Arche about 320 → 200 FPS, about 5 ms more latency). Window ghosting is now disabled for this game only (Graphics → *Prevent first-run FPS drop*). First editor Play was slow 10 times out of 13 before, fast every time after.
- **Editor retry 9.1 s → 3.2 s:** the game re-enabled 28,835 editor click colliders on every retry (up to 17.6 ms of 2D physics per frame); they now stay off, and runs after a retry stay at full FPS.
- **Faster level loading:** JPG decoding on worker threads with libjpeg-turbo 3.2.0 (all 2,068 test JPGs byte-identical to Unity), native PNG unfiltering (SSE2) and DXT pre-compression with ISPC (3.4× faster, byte-identical output on 3.3 billion blocks from 23,447 PNGs), a faster drop-in JSON parser for level files (identical results and exceptions), and an O(n²) decoration list check replaced by a side set (Arche 1.8 s → 0.02 s).
- **Modern presentation (experimental, off by default):** switches D3D11 from BitBlt to flip model (300 → 318 FPS, frame-to-screen 6.2 → 4.4 ms; tearing possible with vsync off).
- **Outside causes shown:** when Windows composition waits grow during play, the settings home card names the overlay or capture program responsible.
- **Gameplay-safety fixes:** the live monitor never takes mouse input during play (the game drops a frame's input when the mouse is pressed over UI); GC "quiet" detection also looks at tile progress; `boot.config` keeps its original line endings.
- **Discord server** link in the settings window, UMM mod list and README.

**Since 2.2.1 (from PR #1):**
- **Faster level loading:** PNG inflate with libdeflate (MIT, embedded), decoding straight into the output buffer, interlaced PNGs decoded on worker threads. Hello (BPM) 2026: 12.2 s → 8.1 s. 1,047 PNGs verified pixel-identical against PIL.
- **VRAM overflow prevented on the first play:** image sizes are read from file headers before loading; if the originals would exceed 1.25× the free VRAM, the largest images are capped at 3072 px from the first play (only this first step). Hello (BPM) 2026: no VRAM hitches on the first play (was several 130 ms hitches).
- **Filter shader warm-up fixed:** shader names are read from each filter's IL (`Shader.Find`), legacy filters are included, and warm-up happens at level load (109 filters in about 200 ms).
- **Audio and judgement are never delayed:** effect burst splitting now only defers 20 visual-only effect types.
- **Decoration animator** also handles pivot / parallax offset / parallax multiplier (41,586 frames vs real DOTween, 0 differences).
- **Bug fixes:** decorations left behind after leaving play mode, queued effects dropped after pausing, queued effects lost when a feature is turned off or the mod is unloaded mid-song, editor selection borders and text borders, and dev-build diagnostics that caused hitches.

**Features:** deferred GC during play, spreading effect bursts and large tile recolors over several frames, a DOTween re-sort guard, skipping redundant text updates, direct handling of instant and animated decoration moves (bit-identical to DOTween), look-ahead skipping of no-op effects, not drawing fully transparent decorations, skipping dormant decorations in the per-frame loop, drawing additive blend-mode decorations with hardware blending (pixel-identical), parallel PNG decoding, skipping asset unloads, automatic image downscaling on VRAM overflow, and multithreaded rendering via `boot.config`. The low-end page (off by default) adds process priority, power throttling off, render scale with FSR 1, auto resolution, a menu FPS cap and experimental half-rate rendering.

**2.2.0:** faster editor play start (Arche 8.6 s → 4.3 s); GC on quick retries only when a lot has built up; gray+alpha and 16-bit PNGs decoded byte-identically to Unity; images pre-compressed on worker threads in place of PACL2's main-thread lossy compression; a fix for a game buffer leak; skipping unchanged particle writes; low-end options to pause off-screen particles and to load images DXT-compressed; detection of overlaps with Quartz/PACL2; automatic protection (a feature that keeps throwing errors is turned off for the session, and two abnormal exits in a row start the next launch in safe mode).

**Live monitor:** FPS, CPU/GPU/VRAM/RAM and hitch alerts with an estimated cause.

**Bug reports:** Settings window → About → *Create log file* makes `StutterFix-log-<date>.zip` on your desktop (your Windows user name is hidden). Post that file on the [mod's Discord server](https://discord.gg/csys9ZAeD6) or send it to **narooh** on Discord (DM).

**Discord server:** [discord.gg/csys9ZAeD6](https://discord.gg/csys9ZAeD6) — bug reports, feature ideas and questions are all welcome.

**Third-party code:** libdeflate 1.24 (MIT), libjpeg-turbo 3.2.0 (IJG / BSD-3-Clause / zlib; this software is based in part on the work of the Independent JPEG Group), AMD FidelityFX Super Resolution 1 (MIT).
