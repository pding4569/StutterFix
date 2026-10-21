# 자동 반복 4회차

직전 게임 시험 결과(자동 수집): `docs/handoff/ga-autoloop-run3-evidence.txt`. 먼저 `docs/handoff/ga-autoloop-ledger.md` 장부를 읽고, 이미 실패한 접근은 되풀이하지 말고 결과에서 배운 점으로 다른/더 나은 접근을 택해라(같은 코드를 다시 돌리는 것은 금지). 게임 코드는 `E:/StutterFixWork/decompiled/Assembly-CSharp/<타입>.cs` 디컴파일본으로 읽어라(근사, ilscan 으로 교차확인). 성공 조건(정규식) `GATE2=PASS` 이 아직 안 나왔다. 결과를 분석해 원인을 코드로 찾아 고치고, 안 되는 이유가 설계 한계면 결과 문서에 구체적으로 적고 멈춰라. 아래는 원래 임무다.


---
# 자동 반복 2회차

직전 게임 시험 결과(자동 수집): `docs/handoff/ga-autoloop-run1-evidence.txt`. 먼저 `docs/handoff/ga-autoloop-ledger.md` 장부를 읽고, 이미 실패한 접근은 되풀이하지 말고 결과에서 배운 점으로 다른/더 나은 접근을 택해라(같은 코드를 다시 돌리는 것은 금지). 게임 코드는 `E:/StutterFixWork/decompiled/Assembly-CSharp/<타입>.cs` 디컴파일본으로 읽어라(근사, ilscan 으로 교차확인). 성공 조건(정규식) `GATE2=PASS` 이 아직 안 나왔다. 결과를 분석해 원인을 코드로 찾아 고치고, 안 되는 이유가 설계 한계면 결과 문서에 구체적으로 적고 멈춰라. 아래는 원래 임무다.


---
# 자동 반복 2회차

직전 게임 시험 결과(자동 수집): `docs/handoff/ga-autoloop-run1-evidence.txt`. 먼저 `docs/handoff/ga-autoloop-ledger.md` 장부를 읽고, 이미 실패한 접근은 되풀이하지 말고 결과에서 배운 점으로 다른/더 나은 접근을 택해라(같은 코드를 다시 돌리는 것은 금지). 게임 코드는 `E:/StutterFixWork/decompiled/Assembly-CSharp/<타입>.cs` 디컴파일본으로 읽어라(근사, ilscan 으로 교차확인). 성공 조건(정규식) `GATE2=PASS` 이 아직 안 나왔다. 결과를 분석해 원인을 코드로 찾아 고치고, 안 되는 이유가 설계 한계면 결과 문서에 구체적으로 적고 멈춰라. 아래는 원래 임무다.


---
# Gate 2 변형 A — 아틀라스 없음 (같은 텍스처 연속 구간 메시)

`E:/StutterFixWork/specs/gate2-common.md` 를 먼저 읽고 그대로 수행해라(워크트리 안 `docs/handoff/gate2-common.md` 에도 사본이 있다).
**이 변형의 구간(런) 정의**: 정렬 순서에서 소유 장식이 연속이면서 **같은 텍스처(와 같은 재질 서명)** 인 것만 한 구간. 텍스처가 바뀌면 구간을 끊는다 → 구간 수는 센서스 추정(아틀라스 가정 71~78)보다 훨씬 많다(~1,200 예상). 각 구간의 MeshRenderer 는 첫 멤버의 재질 인스턴스를 빌리고 `_MainTex`는 그 멤버의 것 그대로(UV 무변환 → 픽셀 오차 위험 최소). 이득은 주로 Transform 쓰기 제거에서 나와야 한다. 구간이 ~1,200개일 때 메시 갱신 호출 비용을 `cost` 로그에 분리해라. 결과 문서 이름 `codex-gate2-A-result.md`.


## 이어하기 안내
이전 2회차 Codex 는 중간에 끊겼다. 워크트리에 미커밋 수정이 있을 수 있으니 `git status`/`git diff` 로 확인하고 이어서 수정하라(처음부터 다시 쓰지 말 것). 장부와 직전 시험 증거를 먼저 읽을 것.
