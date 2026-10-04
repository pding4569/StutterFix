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
| Arche | 에디터에서 죽고 다시 하기 (전환 기록, 플레이어용) | 1.36초 → **0.87초** (2.5.0) |
| Arche | 커스텀 맵 목록에서 연 맵의 다시 하기 (두 번째부터) | 1.15~1.2초 → **0.46~0.48초** (2.5.0) |
| Arche | 편집으로 나가기 | 2.3~3.2초 → **1.4~1.7초** (2.4.0) |
| Hello (BPM) 2026 | 맵 불러오기 | 12.2초 → **8.1초** |
| Hello (BPM) 2026 | 첫 판 곡 중 끊김 | 10번(최악 133ms) → **2번(최악 35ms)** |
| Lost Requiem (타일 1.5만 개) | 타일 이동 효과 한 프레임 | 56ms → **26ms** (2.5.0) |
| Windflower | 곡 중 끊김 (타일 모양·효과음 처음 만들기) | 41~44ms·29~30ms → **없음** (2.5.0) |
| 이미지 원본 약 100GB 맵 | 깨진 이미지로 안 열리던 맵 / 곡 중 최악 프레임 | 열림, 66ms → **26ms**, 끊김 3 → 0 (2.5.0) |

## 목차

- [설치](#설치)
- [사용법](#사용법)
- [2.5.0 에서 바뀐 것](#250-에서-바뀐-것) · [2.4.7](#247-에서-바뀐-것) · [2.4.6](#246-에서-바뀐-것) · [2.4.5](#245-에서-바뀐-것) · [이전 버전](CHANGELOG.md)
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

- **Insert**: 설정 창. 화면 오른쪽 끝의 아이콘 줄에서 아이콘을 누르면 그 기능 패널이 펼쳐지고, 바깥을 누르거나 Esc 로 닫힙니다. 기능 줄을 누르면 자세한 설명이 펼쳐지고, 스위치를 누르면 켜고 끕니다.
- **Shift+Insert**: 실시간 모니터 (아이콘 → 미니 → 상세 → 끔)
- 두 단축키는 설정 창 홈에서 바꿀 수 있고, 한국어/English를 고를 수 있습니다.
- 아이콘 줄 맨 아래 버튼으로 게임을 다시 켤 수 있습니다. 에디터에서 맵을 열어 둔 채라면 **이 맵으로 재시작**으로 다시 켠 뒤 그 맵을 바로 엽니다(저장 안 한 편집이 있으면 한 번 알리고, 한 번 더 누르면 저장하지 않고 재시작). 다시 켜면 좋은 때(설정 변경, 모드 업데이트, 메모리를 많이 씀, 오래 켜 둠)는 아이콘 옆 점으로 알려 줍니다.

## 2.5.0 에서 바뀐 것

### 곡 중 끊김

| 무엇 | 원인 | 결과 (플레이어용) |
|---|---|---|
| **타일 애니메이션 직접 처리** | 타일 이동 효과가 타일마다 DOTween 애니메이션을 만들고 이전 것을 끝냄(타일 8천 개면 트윈 2만 개) | Lost Requiem 531.8초 56 → 26ms, HELLO 2026 41 → 25ms, 끊김 1 → 0번. 진짜 DOTween 과 나란히 돌려 4개 맵 프레임 47만 번 다름 0 |
| **함수 미리 컴파일** | 효과가 처음 나올 때 모드·게임 함수가 그 자리에서 컴파일(JIT)됨 | HELLO 2026 곡 5.1초 42~43ms → 없음 (A/B 4판). 게임 켤 때 약 0.5초 |
| **효과음 미리 불러오기** | 박자 소리를 처음 쓸 때 소리 파일을 메인 스레드에서 풀어 불러옴 | Windflower 곡 22.7초 29~30ms → 없음 (A/B 4판), 재생 준비 +0.06초 |
| **타일 모양 미리 만들기** | 타일 색 바꾸기가 스타일을 바꾸면 처음 보는 타일 메시를 곡 중에 만듦 | Windflower 94.7초 43ms·99.0초 41~44ms → 없음, 재생 준비 +0.3초 |
| **고급 필터 빠르게 끄기** | "다른 필터 끄기"가 쓴 필터마다 형식을 이름으로 새로 찾음(한 번 약 0.7ms) | HELLO 2026 곡 중 최악 48~55 → 24ms, 재생 시작 3.3~3.6 → 1.9초 |

### 맵 열기·다시 하기·메모리

- **깨진 이미지가 있으면 맵이 안 열리던 것 (게임 버그)**: PNG 끝이 잘린 파일이 있으면 게임이 "불러오기 성공"과 함께 빈 이미지를 돌려줘 장식 불러오기가 오류로 멈췄습니다. 그 파일은 없는 파일처럼 건너뜁니다(로그에 파일마다 한 번 적음). 이미지 원본이 약 100GB 인 맵 3개가 이제 열립니다.
- **큰 이미지 자동 줄이기 더 깊게**: 긴 변 3072 로 줄여도 필요한 그래픽 메모리가 비어 있는 VRAM 의 5배를 넘으면 처음부터 2048 / 1536 / 1024 까지 줄입니다. 이미지 1,520장 맵: FPS 214 → 281, 곡 전체 최악 프레임 66 → 26ms, 끊김 3 → 0번, 맵 열기 108 → 90초.
- **죽고 다시 하기 빠르게**: 편집으로 나가기처럼, 판 중에 안 바뀐 장식은 가볍게 다시 설정합니다. 에디터 Arche 1.36 → 0.87초, 커스텀 맵 목록에서 연 맵은 두 번째 다시 하기부터 1.15 → 0.47초. 원래 방식과 결과가 같은지 개발자용으로 대조(에디터 20번, 게임 화면 8번, 다름 0).
- **편집기에서 맵을 새로 열 때 지난 맵이 메모리에 남던 것**: 게임이 편집기를 시작할 때 걸어 둔 종료 확인 콜백을 메뉴로 나갈 때만 풀어서, 지난 편집기·맵 데이터가 통째로 남았습니다(PACL2 의 글자 장식 목록, 이 모드의 기록도 함께). 새 편집기 시작 때 지난 것을 풉니다. Arche 를 연 뒤 HELLO 2026 을 열었을 때 정리 뒤 힙 1,630 → 694MB.

### 설정 창 새 디자인

- 어두운 반투명 유리 창(뒤 화면이 흐리게 비침, 곡 중에는 비치지 않는 짙은 바탕), 상자·테두리를 걷어 낸 목록, 긴 페이지는 위쪽 글자 탭.
- 기능마다 한 줄 설명만 보이고, 줄을 누르면 자세한 설명이 펼쳐집니다.
- 홈: 실시간 프레임 그래프(크게 튄 프레임은 빛나는 흰 막대)와 최근 끊김.

### 그 밖

- (저사양, 기본 끔) **타일 이동 나눠 처리**: 타일 1000개 넘게 옮기는 효과를 가까운 타일부터 몇 프레임에 나눕니다. Lost Requiem 531.8초 효과 22 → 6ms.
- (실험, 기본 끔) **화면 지연 줄이기**: 전체 화면을 독점 전체 화면으로 바꿔 윈도우 화면 합성을 건너뜁니다. HELLO 2026(165Hz): 프레임이 화면에 나오기까지 15.8 → 7.9ms, FPS 209 → 187.

## 2.4.7 에서 바뀐 것

- **효과 몰림 비용이 늘었던 것 (2.4.4 부터)**: 화면 밖 타일 끄기가 타일 등장·사라짐 효과가 시작될 때마다 리플렉션으로 필드를 찾고 있었습니다(사라짐 효과는 그 필드가 없어 매번 끝까지 찾음). 필드를 직접 읽게 고쳤습니다. 동작은 같습니다.

## 2.4.6 에서 바뀐 것

### 큰 맵 편집 화면에서 누를 때마다 끊기던 것
편집 화면에서 마우스를 누를 때마다(화면 끌기 시작, 타일 고르기) 게임은 마우스 아래 물체를 찾으려고 맵의 모든 타일 위치를 하나씩 읽어 마우스와의 거리를 잽니다. 9만 타일 맵에서는 한 번에 56~61ms 라서, 화면을 조금씩 끌 때마다 "게임 처리"로 끊겼습니다.
이제 타일 위치를 유니티 잡으로 한꺼번에(여러 스레드) 읽어 마우스 근처(반경보다 조금 넓게) 타일만 그 반복에 넘깁니다. 거리 판정과 그 뒤 처리는 게임 코드 그대로라 결과가 같습니다. 준비(9만 타일 약 0.1초)는 맵을 열거나 타일을 다시 만드는 순간에 미리 합니다.
- 측정(9만 타일 맵): 누를 때 **56ms → 13~15ms**.
- 검증(개발자용): 누를 때마다 원래 방식으로 가까운 타일을 구해 비교, 96번 중 빠뜨린 것 0.
- 타일 2천 개 이상인 맵에서만, "장식 순회 줄이기"에 포함됩니다.

### 효과 재사용이 판마다 쌓이던 것
게임의 트랙 변경 효과가 타일 등장·사라짐 효과를 효과 붙이기 밖에서 직접 새로 붙여서, 옛 것이 다시 쓰이지 않고 판마다 남았습니다(2026 맵 판당 약 1만 9천 개, 최대 약 8만 개까지). 이제 이 모드가 붙인 효과만 남겨 두고 나머지는 원래처럼 바로 지웁니다. 남겨 둔 수가 판마다 같게 유지됨을 확인(나간 뒤 14,853개, 재생 뒤 0개), 재생 시작·나가기 시간은 그대로.

## 2.4.5 에서 바뀐 것

2.4.4 에서 생긴 문제를 고쳤습니다.

- **곡이 끝나자마자 끊기던 것**: 두 가지였습니다. (1) 화면 밖 타일 끄기가 곡이 끝나는 순간 꺼 둔 타일 수만 개를 한 프레임에 다시 켰습니다. 이제 곡이 끝나도 카메라를 따라 계속 돌리다가, 어차피 멈추는 순간(편집으로 나가기·다시 하기·장면 전환)에 켭니다. (2) 완주 3초 뒤 미뤄 둔 메모리 정리를 해서 큰 맵에서 0.36~0.47초 멈췄습니다. 이제 다음 전환(편집으로 나가기·다시 하기·메뉴) 때 같이 합니다.
- **곡 시작 직후 메모리 정리**: 편집으로 나갈 때 정리를 건너뛴 뒤 재생 준비가 쓰레기를 많이 만들면, 유니티 자동 GC 가 곡 1초 무렵 돌아 0.2초 멈췄습니다. 재생·다시 하기를 누른 순간부터 곡이 돌기 시작할 때까지 자동 GC 를 막습니다(쌓인 것은 다음 전환 때 치움).
- **편집으로 나간 뒤 편집 화면이 끊기던 것**: 나간 뒤 메모리를 프레임마다 조금씩 치우던 것이 큰 맵(힙 1GB 안팎)에서는 한 조각에 40~46ms 씩 걸렸고, 쌓인 양이 적다고 정리를 건너뛰면 몇 프레임 뒤 유니티 자동 GC 가 돌았습니다(9만 타일 맵 69~79ms). 이제 나가기 작업이 끝나는 순간(아직 멈춘 동안) 한 번에 치웁니다. 9만 타일 맵에서 나가기가 약 0.6초 길어지는 대신 편집 화면에서는 끊기지 않습니다.
- **히트사운드가 바뀌던 것**: 효과 컴포넌트 다시 쓰기(2.4.4)가 남겨 둔 히트사운드 효과를 타일이 계속 가리켜서, 히트사운드 이벤트를 옮기거나 지운 타일이 옛 소리를 썼습니다. 원래처럼 비웁니다.
- **박자 알림 목록 다시 만들기**: 9만 타일 맵에서 한 번에 68ms 걸리던 것을 한 번 훑기로 16ms 로 줄였고, 알림을 받는 물체가 새로 생기면 새 것에만 번호를 매깁니다(목록이 다르게 바뀌면 원래대로 다시 만듦).

검증(개발자용 자동 시험, 5천·9만 타일 맵): 박자 알림 목록을 원래 방식과 대조 75번 틀림 0, 화면 밖 타일 끄기 "화면과 겹친 꺼진 타일" 0번·"꺼진 채 움직인 타일" 0번(곡이 끝난 뒤 포함), 완주 뒤·곡 시작 뒤·편집 화면에서 메모리 정리 끊김 0번, 편집 화면에서 타일 위로 카메라를 옮길 때 프레임 평균 3.6~4.3ms.

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
| 타일 애니메이션 직접 처리 | 타일 이동(MoveTrack) 효과는 타일마다 DOTween 애니메이션을 최대 6개 만들고 끝냅니다(타일 수천 개면 한 프레임 수십 ms). 장식처럼 모드가 애니메이션을 직접 돌리고, 길이 0 인 즉시 이동은 같은 계산으로 값을 바로 씁니다. 진짜 DOTween 과 나란히 돌려 비트 단위로 같은 것을 확인했습니다. | Lost Requiem 531.8초 56ms → 26ms, HELLO 2026 125.5초 26ms 끊김 없어짐 (곡 시작 연출 뒤 최악 41 → 25ms) |
| 함수 미리 컴파일 | 모드와 게임의 효과·장식·타일 함수는 처음 불릴 때 컴파일(JIT)됩니다. 효과가 처음 나오는 곡 초반 프레임이 그 값을 냈습니다. 게임을 켤 때 미리 컴파일합니다(약 0.5초). | HELLO 2026 곡 5.1초 42~43ms 끊김 → 없음 (A/B 4판) |
| 효과음 미리 불러오기 | 게임은 박자 소리·누르는 박자 소리를 처음 쓸 때 소리 파일을 통째로 풀어 불러옵니다(하나 수 ms~24ms). 맵이 쓸 수 있는 소리를 재생 준비 때 같은 함수로 미리 불러 둡니다. 소리는 같습니다. | Windflower: 곡 22.7초 29~30ms 끊김 → 없음 (A/B 4판), 재생 준비 +0.06초 |
| 타일 모양 미리 만들기 | 타일 색 바꾸기가 스타일을 바꾸면 타일마다 새 모양(메시)이 필요한데, 처음 보는 모양은 곡 중에 만들었습니다(하나 약 45µs). 그 효과들이 곡 중에 만들 모양을 재생 준비 때 같은 계산으로 미리 만들어 둡니다. 모양은 같습니다. | Windflower(목록에서 연 맵): 곡 94.7초 43ms·99.0초 41~44ms 끊김 → 없음, 재생 준비 +0.3초 |
| 고급 필터 빠르게 끄기 | 고급 필터의 "다른 필터 끄기"는 지금까지 쓴 필터마다 형식을 이름으로 새로 찾습니다(한 번 약 0.7ms). 그 결과를 기억해 둡니다. 결과는 같습니다. | HELLO (BPM) 2026: 곡 중 가장 긴 프레임 48~55 → 24ms(끊김 1 → 0번), 재생 시작 3.3~3.6 → 1.9초 |
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
| 에디터 재생 시작 빠르게 | 이미지 파일 수정 시각을 파일마다 한 번만 읽고, 장식이 하나도 안 바뀌었으면 장식 전체 다시 설정을 두 번 대신 한 번만 하고, 에디터 클릭용 충돌 상자를 넣은 반대 순서로 끕니다(2.2.0). 죽고 다시 할 때는 충돌 상자를 아예 켜지 않고, 맵 파일 읽기와 같은 이미지 장식 목록도 빠르게 합니다(2.3.0). 2.5.0: 죽고 다시 할 때도 편집으로 나가기처럼 판 중에 안 바뀐 장식은 가볍게 다시 설정합니다(에디터, 그리고 커스텀 맵 목록에서 연 맵은 두 번째 다시 하기부터). 개발자용 검증: 건너뛴 다시 설정의 차이 0, 가볍게 한 다시 설정 에디터 20번·게임 화면 8번(Arche, Windflower, HELLO 2026) 모두 원래 방식과 같음. | Arche 8.6 → 4.3초, 죽고 다시 하기 9.1 → 3.2초 |
| 게임 메모리 누수 막기 | 게임의 사용자 지정 FPS 효과가 켤 때마다 새로 만들고 풀지 않던 화면 크기 버퍼(4K 에서 약 40MB)를 풀고, 재시작마다 게임 화면 버퍼를 괜히 다시 만드는 것을 막습니다(2.2.0). 편집기에서 맵을 새로 열 때 지난 편집기·맵이 메모리에 남던 것도 풉니다(2.5.0). | Arche 뒤 HELLO 2026 열기: 정리 뒤 힙 1,630 → 694MB |
| 큰 이미지 줄이기 (기본 자동) | 필요한 VRAM 이 크게 넘칠 맵은 첫 판부터 긴 변 3072(그래도 비어 있는 VRAM 의 5배를 넘으면 2048 / 1536 / 1024 까지, 2.5.0), 그래도 VRAM 이 가득 차 끊기면 기억해 두었다가 한 단계씩(2048 → 1536 → 1024) 줄입니다. 화면에 보이는 크기는 그대로이고 선명도만 조금 낮아집니다. 설정 창에서 기억한 맵을 지울 수 있습니다. | 이미지 2,000장 맵(VRAM 8GB) 150~200ms 멈춤이 3072 에서 사라짐 |
| 깨진 이미지 건너뛰기 | 끝이 잘린 PNG 처럼 풀 수 없는 이미지는 없는 파일처럼 건너뜁니다. 원래 게임은 이런 파일 하나로 장식 불러오기가 멈춰 맵이 안 열렸습니다(2.5.0). | 이미지 원본 약 100GB 맵 3개가 열림 |
| 필터 셰이더 미리 준비 | 맵에서 쓰는 필터(일반·고급)의 셰이더를 불러오기 끝에 미리 만들어 둡니다. | 2026 필터 109개 약 200ms |

### 그래픽

| 기능 | 하는 일 | 측정 |
|---|---|---|
| 멀티스레드 그리기 | 게임 폴더의 `boot.config`에 `force-gfx-jobs=legacy` 한 줄을 넣어 그리기 준비를 여러 코어에 나눕니다. 원래 파일은 백업해 두고, 모드를 끄면 되돌립니다. | D3D11 140 → 160 FPS |
| 블렌드 장식 빠르게 그리기 | 더하기(Linear Dodge) 블렌드 장식을 화면 복사 없이 그래픽카드 기본 섞기로 그립니다. 원래는 장식 하나마다 화면 전체를 복사했습니다. | 1,500개 장면 약 11 FPS → 끊김 없음, 픽셀 차이 0 |
| 첫 판 FPS 떨어짐 막기 | 이 게임에서만 윈도우의 "응답 없음" 창을 끕니다. 큰 맵에서 5초 넘게 멈춘 뒤 그 판 내내 FPS 가 떨어지던 것을 막습니다(2.3.0). 끄면 다음 실행부터. | Arche 첫 판 약 200 → 300 FPS |
| (실험, 기본 끔) 화면 지연 줄이기 (독점 전체 화면) | 전체 화면일 때 독점 전체 화면으로 바꿔 윈도우의 화면 합성을 건너뜁니다(Independent Flip). 수직동기를 끄면 찢어짐이 보일 수 있고, Alt+Tab 하면 최소화됩니다. 끄면 다음 실행부터 원래 전체 화면 창으로 돌아갑니다. | HELLO 2026 165Hz: 프레임이 화면에 나오기까지 15.8 → 7.9ms, 209 → 187 FPS |
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
| 타일 이동 나눠 처리 | 타일 1000개 넘게 옮기는 효과를 지금 타일에서 가까운 것부터 몇 프레임에 나눠 처리합니다. 늦게 시작한 타일은 지난 시간만큼 당겨 두어 끝나는 순간은 같고, 먼 타일이 처음 1~몇 프레임 늦게 움직이는 것만 다릅니다. Lost Requiem 531.8초 효과 22 → 6ms. |
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

두 버전을 빌드해 `dist/`에 UMM 설치용 zip 을 만듭니다. 정식 릴리스 전에 테스터에게 줄 zip 은 `./pack.sh test 1`(번호는 테스트 회차)로 만듭니다. 플레이어용과 같은 DLL 이고, UMM 목록에 "테스터 2.4.7.1" 처럼 보이며, 다음 정식 릴리스가 나오면 자동 업데이트로 넘어갑니다. 하나만 빌드하려면:

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

**2.5.0:** fewer mid-song hitches: tile move effects (MoveTrack) are animated by the mod instead of creating DOTween tweens per tile (Lost Requiem 56 → 26 ms, HELLO 2026 41 → 25 ms; run side by side with real DOTween on 4 levels, 470k frames identical); mod and game effect functions are JIT-compiled at launch instead of on first use (HELLO 2026 42–43 ms → none, ~0.5 s at launch); hitsounds the level can use are loaded during play setup (Windflower 29–30 ms → none); tile meshes that recolor events would build mid-song are built during setup (Windflower 41–44 ms → none); the advanced filter "disable other filters" caches type lookups (HELLO 2026 worst 48–55 → 24 ms, play start 3.3–3.6 → 1.9 s). Levels with a truncated PNG now open (the game returned an empty texture marked "successful" and decoration loading stopped); the automatic image cap now goes down to 2048/1536/1024 when 3072 still needs over 5× the free VRAM (1,520-image level: worst frame 66 → 26 ms, hitches 3 → 0). Faster retries (editor 1.36 → 0.87 s, levels opened from the custom level list 1.15 → 0.47 s from the second retry), and opening a new level in the editor now frees the previous one (game bug: the old editor stayed referenced by a quit callback). New settings window: dark translucent glass, list rows without boxes, one-line descriptions that expand on click. Off by default: split large tile moves over frames (low-end page), exclusive fullscreen for lower display latency (experimental).

**2.4.7:** effect bursts are cheaper again: off-screen tile culling looked up a field by reflection on every tile appear/disappear effect start (since 2.4.4); now a direct field read.

**2.4.6:** clicking in the editor on huge levels no longer stalls: the game measured the mouse distance to every tile on each mouse press (90k tiles: 56–61 ms); tile positions are now read in bulk by a Unity job and only tiles near the mouse are handed to the game loop, which still does the exact distance check (56 → 13–15 ms, verified identical). Also: effect reuse no longer piles up the tile appear/disappear effects that the game adds outside the effect pass (up to ~80k kept components on some levels).

**2.4.5:** fixes for 2.4.4. No more hitch right when a level ends (off-screen tile culling no longer re-enables tens of thousands of renderers in one frame at the end, and the deferred memory cleanup after a clear now runs at the next transition instead of 3 s after the portal); no automatic GC right after the song starts (blocked from pressing Play until the song runs); no GC hitches in the editor after leaving play mode (cleanup runs once at the end of the exit freeze instead of in 40–46 ms slices or a Unity GC a few frames later); hitsounds no longer keep an old SetHitsound after the event is moved or removed (kept effect components are unlinked from the tile like a destroyed one); the beat listener index is rebuilt in one pass and only new entries are indexed when objects are added (90k tiles 68 → 16 ms, verified 0 mismatches).

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
