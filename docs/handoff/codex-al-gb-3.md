# 자동 반복 3회차

직전 게임 시험 결과(자동 수집): `docs/handoff/gb-autoloop-run2-evidence.txt`. 먼저 `docs/handoff/gb-autoloop-ledger.md` 장부를 읽고, 이미 실패한 접근은 되풀이하지 말고 결과에서 배운 점으로 다른/더 나은 접근을 택해라(같은 코드를 다시 돌리는 것은 금지). 게임 코드는 `E:/StutterFixWork/decompiled/Assembly-CSharp/<타입>.cs` 디컴파일본으로 읽어라(근사, ilscan 으로 교차확인). 성공 조건(정규식) `GATE2=PASS` 이 아직 안 나왔다. 결과를 분석해 원인을 코드로 찾아 고치고, 안 되는 이유가 설계 한계면 결과 문서에 구체적으로 적고 멈춰라. 아래는 원래 임무다.


---
# 자동 반복 2회차

직전 게임 시험 결과(자동 수집): `docs/handoff/gb-autoloop-run1-evidence.txt`. 먼저 `docs/handoff/gb-autoloop-ledger.md` 장부를 읽고, 이미 실패한 접근은 되풀이하지 말고 결과에서 배운 점으로 다른/더 나은 접근을 택해라(같은 코드를 다시 돌리는 것은 금지). 게임 코드는 `E:/StutterFixWork/decompiled/Assembly-CSharp/<타입>.cs` 디컴파일본으로 읽어라(근사, ilscan 으로 교차확인). 성공 조건(정규식) `GATE2=PASS` 이 아직 안 나왔다. 결과를 분석해 원인을 코드로 찾아 고치고, 안 되는 이유가 설계 한계면 결과 문서에 구체적으로 적고 멈춰라. 아래는 원래 임무다.


---
# 자동 반복 2회차

직전 게임 시험 결과(자동 수집): `docs/handoff/gb-autoloop-run1-evidence.txt`. 먼저 `docs/handoff/gb-autoloop-ledger.md` 장부를 읽고, 이미 실패한 접근은 되풀이하지 말고 결과에서 배운 점으로 다른/더 나은 접근을 택해라(같은 코드를 다시 돌리는 것은 금지). 게임 코드는 `E:/StutterFixWork/decompiled/Assembly-CSharp/<타입>.cs` 디컴파일본으로 읽어라(근사, ilscan 으로 교차확인). 성공 조건(정규식) `GATE2=PASS` 이 아직 안 나왔다. 결과를 분석해 원인을 코드로 찾아 고치고, 안 되는 이유가 설계 한계면 결과 문서에 구체적으로 적고 멈춰라. 아래는 원래 임무다.


---
# 자동 반복 2회차

직전 게임 시험 결과(자동 수집): `docs/handoff/gb-autoloop-run1-evidence.txt`. 먼저 `docs/handoff/gb-autoloop-ledger.md` 장부를 읽고, 이미 실패한 접근은 되풀이하지 말고 결과에서 배운 점으로 다른/더 나은 접근을 택해라(같은 코드를 다시 돌리는 것은 금지). 게임 코드는 `E:/StutterFixWork/decompiled/Assembly-CSharp/<타입>.cs` 디컴파일본으로 읽어라(근사, ilscan 으로 교차확인). 성공 조건(정규식) `GATE2=PASS` 이 아직 안 나왔다. 결과를 분석해 원인을 코드로 찾아 고치고, 안 되는 이유가 설계 한계면 결과 문서에 구체적으로 적고 멈춰라. 아래는 원래 임무다.


---
# Gate 2 변형 B — 아틀라스 (구간 수 ≤ ~80)

`E:/StutterFixWork/specs/gate2-common.md` 를 먼저 읽고 그대로 수행해라(워크트리 안 `docs/handoff/gate2-common.md` 에도 사본이 있다).
**이 변형의 구간(런) 정의**: 정렬 순서에서 소유 장식이 연속인 것 전체(텍스처 무관) → 센서스 71~78개. 텍스처 301종을 아틀라스 한 장(또는 같은 크기끼리 Texture2DArray)으로 합친다. 픽셀 위험 완화: ① 압축 블록 정렬 복사(`Graphics.CopyTexture`, 4px 정렬)로 가능하면 DXT 바이트 그대로, ② 불가하면 무압축 RGBA32(VRAM 시험용 허용), ③ 스프라이트마다 최소 2px Extrude 패딩, 짝수 픽셀 정렬, UV 변환은 `u = uMin + u0*(uMax-uMin)` 를 double 로 계산해 float 로 한 번만 내림, ④ 필터 모드·밉맵·wrap 이 서로 다른 텍스처는 같은 아틀라스에 넣지 말고 분리(구간이 늘어남) — 소유 제외 사유 분포를 로그로, ⑤ 아틀라스에서 1/255 차이가 나면 같은 크기끼리 Texture2DArray(UV 무변환, `uv.z=slice`)로 전환하는 대안을 결과 문서에 비교로 적어라. 우리 셰이더가 필요하면 게임 `Sprites/Tile` 출력 식과 같게 쓰고(AssetBundle 번들링은 `fsr/` 방식 참고) 셰이더 키워드는 최소로. 결과 문서 이름 `codex-gate2-B-result.md`.


## 이어하기 안내
이전 2회차 Codex 는 중간에 끊겼다. 워크트리 상태를 `git status`/`git diff` 로 확인하고 이어서 수정하라(처음부터 다시 쓰지 말 것). 장부와 직전 시험 증거를 먼저 읽을 것.
