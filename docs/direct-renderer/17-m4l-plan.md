# M4l 계획 (Claude, 2026-11-16 새벽) — 렌더 스레드를 거의 0 으로: 캐시한 명령 목록 + GPU 포즈

## 병목 분석 (M4j, 3440 렌더, Exit 88초, phase_window, 프레임당 ms)
| 단계 | 원본 A | 직통 B | 상한(맵 안 그림) |
|---|---|---|---|
| 합계 | 2.68 | 3.76 | 2.09 |
| PostLateUpdate/FinishFrameRendering | 0.78 | **1.53** | 0.57 |
| Update/ScriptRunBehaviourUpdate | 0.92 | 0.94 | 0.62 |
| PreLateUpdate/ScriptRunBehaviourLateUpdate | 0.41 | 0.57 | 0.30 |
| presentMeanUs (화면 대기) | 0.074 | 0.082 | 0.069 |
| 메인 카메라 GPU | 2.49 | 2.49 | 2.54 |
- **CPU 병목**(Present 대기 0). 메인 카메라 GPU 는 맵을 빼도 2.54ms 로 그대로 → GPU 는 맵 내용이 거의 아님.
- B 의 손실은 **FinishFrameRendering +0.75ms = 메인 스레드가 렌더 스레드를 기다림**(우리 native 렌더 스레드 일 1.19ms: rt_constants 0.67, apply 0.29, cull 0.18…) + LateUpdate +0.16(우리 PreCull 쪽 collect/pose/sort).
- 상한까지 가면 원본보다 +28%(2.68→2.09). 그 이상(압도적)은 스크립트(Update 0.62, LateUpdate 0.30)와 FinishFrameRendering 나머지(0.57: 다른 카메라·필터·UI)를 줄여야 한다.

## 핵심 구조 변경 (목표: 우리 렌더 스레드 일 ≤ 0.1ms, 메인 ≤ 0.15ms)
1. **명령 목록 캐시**: 그리기 순서·상태·바인딩이 바뀌지 않는 한 지난 프레임의 deferred `ID3D11CommandList` 를 그대로 `ExecuteCommandList` 로 다시 실행한다(다시 기록 금지). 순서가 바뀐 구간만 다시 기록(구간별 목록 여러 개로 나눠 바뀐 구간만 재기록).
2. **포즈는 GPU 에서**: 정점 사전 변환 compute 가 포즈 구조화 버퍼(슬롯 고정)를 읽어 월드 정점을 만든다 → draw 의 cbuffer 에는 단위 행렬과 재질 상수만(대부분 정적). 매 프레임 바뀐 포즈만 `UpdateSubresource`(또는 NO_OVERWRITE 링)로 올리고 dispatch 한 번. 명령 목록은 버퍼 객체만 참조하므로 내용이 바뀌어도 재기록 불필요.
3. **재질 상수 변화(색 트윈 등)**: 상수 버퍼 내용만 갱신(명령 목록 밖에서, 실행 전에). draw 마다 별도 cbuffer 대신 큰 cbuffer 배열 + `VSSetConstantBuffers1`/`PSSetConstantBuffers1` 오프셋(D3D11.1)을 목록에 기록해 두면 내용 갱신만으로 끝.
4. **켜짐/꺼짐**: 목록은 그대로 두고 꺼진 것은 사전 변환에서 정점을 퇴화(0 넓이)시키거나 간접 인자(DrawIndexedInstancedIndirect 인자 버퍼의 instance/index count 를 0)로 — 재기록 없이.
5. **화면 밖 컬링**: GPU compute 에서(간접 인자 0) — CPU 컬링 비용 제거.
6. 메인 스레드: 변경 수집은 후크·더티 비트만, 포즈 읽기는 TransformAccessArray 잡(이미 있음)을 렌더 직전 Complete. 프레임당 할당 0.
- Unity 그리기 순서(정렬층→순서→큐→거리→셰이더 ID→재질 ID) 재정렬이 필요한 경우만 해당 구간 목록 재기록. 거리 키 변화가 실제 순서를 바꾸지 않으면 재기록 없음.
- 픽셀 동일성은 지금 수준 유지(DD 비트 동일, Exit ≤2).

## 다음 (압도적으로 가려면)
- 스크립트: scrDecorationManager.LateUpdate·행성·타일 Update 를 우리 계산으로 대체(장식 이동 효과를 GPU 포즈 버퍼에 직접 계산 — DR2/deco-v2 의 채널 방식) → Update/LateUpdate 0.9ms 중 상당 부분 제거.
- FinishFrameRendering 나머지 0.57ms: 다른 카메라(BG/Overlay)·필터·UI 캔버스.
