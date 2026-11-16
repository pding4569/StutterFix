# 일 자체를 없애는 구조 (2026-11-17, 사용자 승인)

직통 렌더러(관찰형)는 게임이 유니티 오브젝트에 쓰는 것을 C# 에서 하나하나 따라 해야 해서 원본을 넘지 못했다
(HELLO 77%, Exit 64%, WF 70%, 7777 54%, Arche 40%). 방향: 맵 내용(장식·타일)의 겉모습을 우리가 곡 시간으로
직접 계산해 렌더러로 바로 보내고, 유니티 오브젝트 쓰기·스크립트·감시를 없앤다.

## 근거 1: 쓰기 조사 (원본, 프레임당, `drfull sites` + drfull-census.txt)
| 맵 | 장식 Transform | 장식 색 | 파티클 장식 | 타일(색·아이콘·반짝임·이동) | UI 페이드 |
|---|---|---|---|---|---|
| HELLO | 약 1,080 | - | 165 | 약 180 | 120 |
| Exit | 약 417 | - | - | 약 320 (set_color 179) | 108 |
| Windflower | 약 2,630 | - | 345 | 약 96 | 50 |
| 7777 | 약 240 | - | 60 | 약 211 (set_color 211) | 80 |
| Arche | 약 4,850 | 약 1,110 | - | 약 200 | 110 |
장식 쓰기 출처: MoveApply.SetLocal(위치), scrDecoration.SetRotation/SetScale, scrParallax.SetTrans, ColorFast/ApplyColor.
타일: FloorMeshRenderer.set_color, scrFloor.SetIconColor/LateUpdate(색·SetFloat), FloorAnim.Apply(이동), SetTrackStyle.

## 근거 2: 천장 (원본 + 맵 안 그림(drfull ceil) + scriptskip deco/floor/floorlate, 3440)
| 맵 | 원본 | 천장 | 배수 |
|---|---|---|---|
| HELLO | 약 200 | 401.5 | 2.0 |
| Windflower | 약 221 | 439.7 | 2.0 |
| 7777 | 약 327 | 553.9 | 1.7 |
| Exit | 약 343 | 531.0 | 1.55 |
| Arche | 약 150 | 246.2 이상(DOTween 장식 이동은 안 멈춤, 메인 4.06ms 남음) | 1.64+ |

## 단계
- R1 타일 겉모습: scrFloor.Update/LateUpdate·FloorMeshRenderer 의 겉모습 쓰기(색·아이콘·반짝임·트랙 스타일)를 곡 시간·타일 상태의 함수로 계산해 렌더러 상수로 직접. 게임 진행에 쓰이는 값(타일 위치·각도·판정 상태)은 그대로 유지. 목표 맵 Exit·7777.
- R2 장식: GpuDeco 굽기 표(00-deco-spec.md 의 매 프레임 식)로 장식 pose·색·보임을 계산해 렌더러에 직접. 유니티 Transform 쓰기·DOTween 장식 트윈·TightLate 제거. 히트박스·글자·오브젝트·파티클·혼합 모드는 기존 경로. 목표 맵 HELLO·WF·Arche.
- R3 렌더러 감시 최소화: 소유 대상은 감시·Refresh·포즈 읽기 제외. 남는 감시는 행성·캔버스·파티클 등.
- 검증: `drfull constcheck`(소유 상수 대 원본이 썼을 값), 재생 뒤 픽셀(`pp-*-st`), 그림자 모드(일부 프레임만 원본 스크립트를 돌려 값 비교), 장식은 decobake verify 기준(시작 1프레임·0.01px).

## 주의 (과거 실패)
- deco-v2: 자체 셰이더 합치기에서 픽셀 >3/255 어긋남 → 이번엔 원본 셰이더(직통 렌더러) 사용.
- GpuDeco 그리기는 3440 WF 에서 GPU 두 배(원인 미확인) → 그리기는 직통 렌더러로.
- 보임 콜백(OnBecameVisible/isVisible)에 묶인 게임 갱신은 Visibility 대행 유지.
