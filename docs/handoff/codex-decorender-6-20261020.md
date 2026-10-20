# Codex 과제 S2g: decorender — 키 안 "가장자리 블록"만 가져가기(두 방향 옵션) + 메시 대 스프라이트 재질 시험 추가

같은 규칙(E:\StutterFixWork\wt-decorender, 커밋·게임 실행·설치 금지, `#if DEV || AUTOTEST`, 사용량 절약). 결과 문서 `docs/handoff/codex-decorender-6-20261020-result.md`.

## 게임에서 얻은 결과
- 키 전체를 가져가는 규칙(S2f)은 Arche 에서 **선택 0개**였다: `key-not-fully-eligible=11311, shared-order=16676, atlas-budget=372, static=462`. 정렬 키 하나를 가져갈 수 없는 장식과 가져갈 수 있는 장식이 섞여 있다.
- sortprobe(`trials.csv` 참고): 같은 큐·거리·같은 재질일 때 **MeshRenderer 는 SpriteRenderer 보다 항상 뒤(먼저 그려짐)** 였다(mesh-first/mesh-last 둘 다 뒤→앞 순서가 "mesh, sprite"). 재질이 다르면 재질 인스턴스 ID 규칙이 98~100% 였다. 메시 대 스프라이트에서 **재질이 서로 다를 때** 어느 규칙이 이기는지는 아직 모른다.
- 지난 `compare`(1,795개 가져감, 키 안 순서는 renderer id 가설): 곡 264~273초 대부분 최대 1~2/255 이지만 2/16 은 10~15/255 이상치(타일 위 반투명 영역).

## 만들 것
1. **키 안 가장자리 블록 가져가기** `decorender on 20 edge=low|high` (기본 high 말고 둘 다 구현, 인수로 선택):
   - 각 정렬 키의 모든 렌더러(관리 후보·비관리 장식·타일 등 전부, 안 그려지는 것(`forceRenderingOff`·알파 0·비활성)도 일단 포함)를 `sharedMaterial.GetInstanceID()` 로 정렬한다(재질 없으면 비관리 취급, 키 제외). 
   - `edge=low`: 관리 후보 장식이 재질 ID 정렬에서 **가장 낮은 쪽 연속 구간**(그 아래에 비관리가 하나도 없는 맨 앞 연속 블록)이면 그 블록만 가져간다. `edge=high`: **가장 높은 쪽 연속 블록**. 블록 밖 후보는 제외 이유 `not-edge-block`. 연속 블록 안에 불가능한(제외 대상) 장식이 끼면 거기서 블록을 끊는다.
   - 비관리 렌더러가 나중에 보이게 되는 경우(지금 `forceRenderingOff`)도 위 규칙에 이미 포함돼 있으므로 순서가 안전해야 한다.
   - 그룹 메시의 재질 인스턴스 ID 가 관리 블록의 재질들 사이(또는 그 바깥)에 어떻게 놓이는지 로그에 남긴다: 그룹 메시 재질을 만들 때 `new Material(source)` 의 `GetInstanceID()` 와 블록의 최소/최대 장식 재질 ID 비교. (메시가 만들어진 순서/ID 로 순서가 정해지는 줄 알았으나 sortprobe 는 메시가 항상 뒤였으므로, 메시의 위치가 "type 규칙(뒤)"인지 "재질 ID 규칙"인지 가려지도록, 가능하면 그룹 재질 ID 를 블록 재질 ID 들의 바깥쪽에 두는 옵션 `matid=above|below|none` 도 둔다 — 새로 만든 재질이 기존보다 큰/작은 ID 를 갖는다는 보장이 없으니 만들 때마다 비교해 반대면 재질을 버리고 다시 만들기를 최대 N회 시도, 실패하면 로그).
2. **sortprobe 보강** (`sortprobe mesh-material`): 실험 케이스 추가 — 메시 대 스프라이트 n=2: 서로 다른 재질에서 (a) 메시 재질 ID < 스프라이트 재질 ID, (b) 메시 재질 ID > 스프라이트 재질 ID (재질을 새로 만들며 ID 비교로 맞춘다, 만들어진 ID 를 기록), 각 50회; n=6(메시 3 + 스프라이트 3, 재질 ID 무작위) 50회. 목표: 메시 대 스프라이트 동률 순서가 type 으로 정해지는지(메시 항상 뒤) 재질 ID 로 정해지는지(재질 ID 순) 확정. 기존 무효(valid=0) 케이스의 무효 원인을 `report.md` 에 한 줄씩 적는다(예: 동일 재질 동률이라 `before/after` 순서가 두 렌더에서 달라졌다 등 `details` 로 판단 가능한 것).
3. `state` 에 키 통계 추가: 관리 키 수, 키별 (후보 수, 관리 블록 크기, 제외 이유), 가져간 장식 수(보이는/안 보이는).

## 결과 문서에
구현, 새 인수, 빌드 결과, 게임에서 돌릴 순서(`sortprobe mesh-material`, `decorender on 20 edge=low`, `edge=high`, `matid=...` 조합).
