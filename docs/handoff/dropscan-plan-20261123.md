# 12맵 전곡 프레임 드랍 분석과 최적화 계산 (2026-11-23)

사용자 요청: "모든 프레임 드랍 구간 싹 다 분석해서 한번에 최적화할걸 계산해봐".

## 방법
- 맵마다 곡 전체 1판(2560x1600 144Hz, 자동 플레이, snap-v3ds = claude/v3-282port). `dropscan begin` 으로 프레임마다 곡 시각·프레임 시간, `profsamp begin 250 native full` 로 메인 스레드 스택(관리+엔진) 250Hz.
- `tools/dropscan/analyze.py` 가 끊김(프레임 > max(20ms, 중앙값×2.5))과 드랍 구간(0.5초 창 FPS < 곡 중앙값 80% 가 1초 이상)을 찾는다.
- 원인 나누기(`E:\StutterFixWork\tools-sym\dropscan_decomp.py`): 표본마다 겹치지 않는 원인 하나(GPU 대기 / 가장 바깥 관리 함수 / 엔진 하위 시스템)로 정하고,
  구간마다 "그 원인의 구간 시간 − 평소 비율 × 평소 속도 시간"을 더한다. 1위 후보에 구간 전체를 몰아주는 묶기는 부풀린다(HELLO 타일 색: 몰아주기 51% 대 실제 약 1/4).

## 결과 (초과 시간 합 266초)
| 원인 | 비율 | 주요 맵 |
|---|---|---|
| GPU 대기 | 35.3% | WF 20.6초, Lost 20.6, DD 20.3, 7777 11.9, Arche 8.0 |
| 장식 갱신(`scrDecorationManager.LateUpdate` = 모드 TightLate) | 11.2% | PLUM 7.1, WF 5.8, Exit 5.2, DD 4.3 |
| `Camera::CustomRender`(그리기 준비) | 7.5% | WF, PLUM, Timeline |
| `scrFloor.Update`(타일 색) | 7.4% | PLUM 7.3, Lost 4.6, 7777 2.4 |
| DOTween(대부분 모드 DecoAnim·FloorAnim) | 6.8% | 7777, DD, scam |
| `UpdateAllRenderers` | 3.6% | 7777 |

**상한**: 드랍 구간을 전부 평소 속도로 만들어도 평균 FPS 가 맵마다 +3~14%(7777 13.6, WF 13.3, Timeline 10.0, Arche 9.7, DD 9.2, HELLO 8.6, scam·Lost 7.8, PLUM 5.7, Exit 5.1, Flower 3.1, eLadia 0). 원인별 상한: GPU 최대 +6.2%(WF), 장식 +3.8%(7777), 그리기 준비 +3.0%(Timeline), 타일 +1.5%(PLUM).

## GPU 구간 (run_gpusec.py, night-results/gpusec)
- GPU 3D 96~100%(게임 95~99%). 본 카메라 GPU: DD 1.94→4.27ms, Lost 2.22→4.21ms, HELLO 1.87→2.60ms(평소→GPU 구간).
- 화면 전체 크기 배경 장식(Sprites/Tile) 수십 장 + 필터(Blur_Movie, AAA_SuperComputer, Blur_BlurHole). 필터는 10-15 조사(filters-findings.md)에서 반해상도 FPS 같음·화질 차이로 채택 0.
- gpucensus 의 알파 0 넓이는 GPU 비용이 아니다: 알파 0 SpriteRenderer(topGlow) 는 유니티가 원래 안 그린다(근거 아래 GlowSkip).

## 기각
- 타일 색 재질 복사(COW) 피하기: 같은 값 쓰기 33/124,117, MaterialPropertyBlock 0.885us = SetColor 0.86~0.97us. 프레임당 약 100번.
- GlowSkip(알파 0 topGlow forceRenderingOff): 7777 451→204, HELLO 226→176 FPS. 원래 끔 판에서 그 타일 스크립트가 꺼짐 = 원래 isVisible=false. 코드는 저장소 밖(patches/glowskip-rejected).
- FloorAnim localScale 캐시: 같은 타일의 X/Y 크기 효과가 다른 축을 지키는 데 필요, 이득 작음.

## 채택 (모드 탓 끊김·낭비)
1. TileCull 음수 줌(orthographicSize<0, 화면 뒤집기): 칸 0개로 곡 중 전부 켜고 멈춤→다시 만들기. Abs. 7777 100.5초 29ms(멈춤 3번) → 멈춤 0. culltest 4시점 다름 0. (ca101e3)
2. FlashSkip 에 FlashEndscreen(곡 끝 번쩍임, 재질 알파 0 전체 화면) 추가: 픽셀 다름 0/4,096,000, GPU -0.02~0.03ms, WF·Exit +0.3~0.5%. (ca101e3)
3. FloorAnim: 실제로 위치·회전·크기가 바뀌거나 이전 애니메이션을 끝내는 타일만 TileCull 에 알림, Mathf.Approximately 를 icall 없는 같은 식(approxcheck 2,000,484쌍 다름 0), thisTransform, TileCull.PinArg 는 floorIndex. Lost 528.4초 타일 8,171개 효과 35.5→20ms, culltest 다름 0.
4. GPU 장식 MasterSlotList.Remove O(n²)→O(1)(빈 칸 표시, 위치 연산 직전 정리, 그림자 List 대조 831,517번 불일치 0), Anchor 구조체를 배열 칸에 바로 쓰기(Buffer.Memcpy 엔진 호출 제거). Arche 219.6초 29.1→23.7~27.7ms(판마다 흔들림).

## 남은 것
- Arche 219.6초: 장식 이동 효과가 GPU 장식 표에 장식 수천 개를 한 프레임에 넣는 일(약 15ms) + 렌더러 반영(약 5ms). 효과 시점을 지키며 나누는 방법 필요.
- HELLO 140.3초 22ms: 색 나누기 조각 + 카메라 이동으로 타일 다시 켜기 + 타일 각도가 겹침. 근거 더 필요.
- Lost 528.4초 20ms: 실제로 움직이는 타일 다시 켜기(약 1/3) + 애니메이션 기록. 이동 경로를 알면 화면 밖 타일은 켜지 않을 수 있음(경로 상자 등록).
- PLUM 곡 시작 1,015ms(press 직후, 곡 시작 연출 구간): 별도 조사.
