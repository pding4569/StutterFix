# sortprobe runtime report

Status: complete

Order = back to front. Rates use valid trials only; invalid trials are excluded explicitly. A 100% match is bounded evidence, not an engine guarantee.
mesh-before-sprite hypothesis checks all mesh/sprite cross pairs (mesh behind sprite); within-type ties are unconstrained. material-id checks full signed material-ID order.

|case/n|total|valid|stable|changed|no-mutation changed|id-asc|id-desc|creation-asc|creation-desc|sibling-asc|sibling-desc|distance-id|distance-sibling|queue-distance-id|material-id|mesh-before-sprite|priority-asc|priority-desc|
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
|priority-unc/2|50|50|100.00% (50/50)|0.00% (0/50)|0.00% (0/50)|40.00% (20/50)|60.00% (30/50)|60.00% (30/50)|40.00% (20/50)|60.00% (30/50)|40.00% (20/50)|40.00% (20/50)|60.00% (30/50)|40.00% (20/50)|100.00% (50/50)|100.00% (50/50)|52.00% (26/50)|48.00% (24/50)|
|priority-unc/3|50|50|100.00% (50/50)|0.00% (0/50)|0.00% (0/50)|18.00% (9/50)|16.00% (8/50)|16.00% (8/50)|18.00% (9/50)|16.00% (8/50)|18.00% (9/50)|18.00% (9/50)|16.00% (8/50)|18.00% (9/50)|100.00% (50/50)|100.00% (50/50)|16.00% (8/50)|14.00% (7/50)|
|priority-unc/6|50|50|100.00% (50/50)|0.00% (0/50)|0.00% (0/50)|0.00% (0/50)|0.00% (0/50)|0.00% (0/50)|0.00% (0/50)|0.00% (0/50)|0.00% (0/50)|0.00% (0/50)|0.00% (0/50)|0.00% (0/50)|100.00% (50/50)|100.00% (50/50)|0.00% (0/50)|0.00% (0/50)|
## 무효 표본 원인 (표본당 한 줄)
없음


## 표본 내 가장 간단한 설명 후보

- priority-unc/2: 100% 후보 다수; 구별 불가: material-id, mesh-before-sprite
- priority-unc/3: 100% 후보 다수; 구별 불가: material-id, mesh-before-sprite
- priority-unc/6: 100% 후보 다수; 구별 불가: material-id, mesh-before-sprite

decorender instanceID 오름차순 가설: 29/150 valid observations. 전체 케이스를 설명하지 못함. 같은 거리/재질/queue 케이스별 표를 따로 확인할 것.

가설은 관측과의 일치율이다. 동률 가설이 여럿이면 규칙은 미확정. invalid>0 또는 stable<100%이면 결정적 규칙으로 결론내리지 않는다.
전체 순서는 pairwise tournament + full-group top + peeling으로 교차 검사한다. 검사 자체가 enabled 상태를 바꾸므로 native 초기 삽입 순서 및 프레임을 넘긴 동적 갱신은 이 시험만으로 확정할 수 없다.
고정 seed 재실행과 다른 seed/새 프로세스 결과를 비교해야 실행 간 비결정성을 평가할 수 있다. MeshRenderer 한 개의 정렬 키로 외부 SpriteRenderer를 메시 내부 삼각형 사이에 삽입할 수는 없다. 실제 장식 재질/카메라 compare는 별도 검증이다.
