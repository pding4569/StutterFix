# 이벤트 패킷 재생 — 2026-10-24

새 트레이스에서 대체될 옛 반복문의 미세 튜닝은 넘기고 이벤트 표의 일괄 재생에 집중한다. 기본 끔·일반 관리 Player 제외·예약 삭제 상태다. 최신 결과/진행 중 manifest는 `gg-autoloop-ledger.md` 첫 절부터 확인한다.

## 구현

- `GpuDecoPackets.cs`: 준비 때 이벤트 피연산자 mask/원래 대상 순서/연속 GPU 소유 구간/슬라이스 목적지/채널 인덱스를 컴파일한다. 현재 Arche74636이벤트·187289소유참조·최대연속14269대상. CPU 대상은 원래 위치에 둔다.
- Source ID만 믿지 않고 세대/현재 대상 객체 순서/피연산자/시간·조건 필드도 검사한다. 미계획/불일치는 기존 경로다. 중복을 임의 정렬·제거하지 않는다.
- `native/sfnative/deco_packet.h`: 요청 전체 사전검증/별도 임시 채널 표 계산/길이0 master·48B anchor·소스바인딩·native-live 슬라이스·dirty commit.
- `native/sfnative/deco_packet_anim.h`: 실제 이전 소스 시각과 easing으로 상대 목표를 계산하고 길이있는 metadata를 전이한다. 복귀용 목표를 start+change로 역산하지 않는다. double 중간 계산과 별도 float change 저장을 구분한다.
- source membership detach/AddRange와 원래 장식·키 순서의 Rec/Engine 생성 순번은 관리측에 남는다. 클럭 전진·정렬·SpriteRenderer·충돌체·shader·원래 swapchain은 바꾸지 않았다.
- commit 전 실패는 원래 소스 목록 위치를 역순 복구하고 기존 구현을 한 번 실행한다. commit 뒤 처리한 대상을 재적용하지 않는다. P/Invoke는 길이를 아는 자체 관리 배열을 고정해서만 호출한다. 임의 포인터의 안전성을 보장하는 API가 아니다.

## 명시적 연구 명령

GPU가 꺼져 있을 때 모드를 고른다. 일반 설정에 추가하지 않았다.

| 명령 | 범위 |
|---|---|
| `gpudeco packetfixture` | 실제 Mono 산술/metadata/거부 원자성 |
| `gpudeco packets off` | 기본 경로 |
| `gpudeco packets shadow` | native 별도 계산/기존 실제 적용과 대조 |
| `gpudeco packets static` | 길이0 native commit + oracle |
| `gpudeco packets native` | 길이0 native commit, oracle 없음 |
| `gpudeco packets all` | 길이0/길이있음 native commit + oracle |
| `gpudeco packets replay` | 길이0/길이있음 native commit, oracle 없음 |
| `gpudeco packets compiled` | native commit + 변경 감지한 준비 대상 목록 사용 |
| `gpudeco packets compiledcheck` | 원래 Take 대상으로 준비 목록/commit 대조 |
| `gpudeco packetguardfixture` | 목록 교체/추가/인덱스 수정 등 9거부 검사 |
| `gpudeco packets rollbackanim` | 검증 모드 다음 animated commit 직전 거부1회 |
| `gpudeco packets verify` | 누적 불일치0/비어있지 않은 적용 검사 |

## 완료 근거와 제한

round73 pure: 산술1024/덮어쓰기6/거부12·static commit128/거부256·animated commit128/거부256·불일치0. 실제Arche214~222초: static1085묶음/24910대상 + animated1846묶음/3874대상, 누적163511채널/metadata 대조0·animated 거부1회·CPU2884/2884복귀·원래순서미관측0·자원0·정상quit/9복원. oracle 비용을 포함하므로 성능판이 아니다.

round71/72 길이0만 비계측 한쌍:218~222초312.525→314.713FPS(약+0.70%). 단1쌍이며 목표260~270초가 아니다. 큰 개선으로 취급하거나 유리한 값을 찾으려 반복하지 않는다. 원시 summary는 `tools/optimization/out/round68-72-packet-summary.json`.

round74/75 같은 DLL 전체패킷 비계측 한쌍:218~222초 313.246→316.779FPS,260~270초 167.760→168.822FPS(약+0.63%). 270초 GPU유지/오류0·CPU10854/10854 복귀·정상quit/9복원 각각. 400FPS 미달이며 큰 성능 개선으로 취급하지 않는다. 원래 경로와 패킷 모두 같은 전체곡 단일 slice를 쓴다.

round76 실제 원래 Take 대조: 준비목록 대상선택2929회/채널·anchor·바인딩163511 비교 불일치0. 목록 변경 거부 fixture9조건/순수연산 fixture 불일치0.222초GPU유지/native오류0·CPU2834/2834복귀·원래순서미관측0/종료자원0·정상quit/9복원. compiledcheck는 원래 Take를 실행하므로 캐시 반환 성능 증명이 아니다. round77 compiled 실제 반환2929회/불일치0·CPU2884/2884복귀·정상quit/9복원을 확인했다.218~222초305.403FPS는 단독표본이며 성능개선으로 채택하지 않는다.

400FPS·새후보 픽셀·CPU 복귀 뒤 미래 연속성·자연0초 전곡·배속·중간재개·다른 맵은 확인 안 됨. 옛 G2/완주는 옛 core의 범위다. 관리측 membership/순번까지 없어졌다고 하지 않는다. 커밋·푸시·정식 채택·배포는 하지 않았다.
