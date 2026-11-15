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

## 추가 (Claude, M4k 실측 02:00): DD VRAM 붕괴 = Grab 복사
- `m4k-dd-resources-full-grab-off`(이미지 원래 크기, VRAM 14.1/7.53GB 초과) 같은 세션 교차: **Grab 켬 → 메인 카메라 GPU 52~64ms(native 51~64ms), Grab 끔(skip_grab=1) → 6.0~8.0ms(native 5.3~7.2ms)**. 즉 초과 상태 붕괴의 주원인은 우리 이름 없는 Grab 복사(프레임당 약 11회 × 3440x1440 전체 19.8MB).
- 고칠 것(픽셀 동일 유지): (1) Grab 텍스처에 `SetEvictionPriority(DXGI_RESOURCE_PRIORITY_MAXIMUM)`, (2) **복사 범위를 그 draw 가 실제로 읽는 화면 사각형(정점 화면 범위 + 필터 여유)으로 줄이기**(CopySubresourceRegion) — Unity 원래 셰이더는 grab UV 를 화면 좌표로 읽으므로 그 사각형 밖은 안 읽는다, (3) 연속한 Grab draw 사이에 camRT 가 안 바뀌었으면 복사 생략, (4) 가능하면 Unity 의 Grab 임시 RT 와 같은 풀/형식. 결과를 grab_bytes/grab_copies 로 state 에.
- M4k Exit: A 327.6/329.7, B 241.6/233.3 (main 0.91~0.95ms, render 1.11~1.18ms, replans ~520) — Codex 가 같은 PC 에서 빌드 중이라 절대 FPS 는 낮음, 비율 ~72%. DD 1024: A 245.4/236.9, B 199.4/197.5 (~82%), GPU 메인 카메라 B 4.41~4.62ms(native 3.91~4.14).

## 추가 2 (Claude, 03:00): Grab 실측 정정 — 꼭 읽을 것
- Claude 가 drfull.cpp 에 Grab 수정을 넣었다(브랜치 `claude/drfull-grab` baf443a, wt-m4 작업 트리에도 이미 들어 있음): 이름 없는 Grab(grabId==1)은 `grabRect()`(draw 월드 경계 → 화면 사각형 +4px, 위아래 두 방향 합집합)만 `CopySubresourceRegion`, Grab 텍스처 `SetEvictionPriority(MAXIMUM)`.
- **M4k 커밋 + 이 수정만**(wt-grab): DD 픽셀 비트 동일, Exit 픽셀 그대로(최대 2), Exit 일시정지 native GPU 1.86ms(정상). DD 원래 크기(VRAM 초과) B1 = **41.7 FPS**(24.5 에서), native GPU 24ms(41~68 에서), 복사 평균 6.3MB(19.8 에서), 프레임당 ~11회.
- **너의 미완성 M4l 코드 + 이 수정** 빌드에서는 DD B1 170.7/167.8 FPS 였지만 **grab_copies=0** 이고 DD 픽셀이 최대 8 다르게 나왔다(0.065%) → **미완성 M4l 이 Grab 복사를 건너뛰고 있다(버그)**. 또 그 빌드는 Exit 일시정지 native GPU 가 1.8 → 8.2ms, HELLO 5.7 → 17.3ms 로 늘었다. 고쳐라.
- 남은 DD 초과 붕괴(복사 끄면 6~8ms, 켜면 24ms): 복사 자체보다 "복사마다 RT 바인딩 해제·SRV 전부 해제 → 드라이버 명령 분할 → 초과 상태에서 분할마다 상주 처리" 가설. 시험: 연속한 Grab draw 를 묶어 한 번만 복사(그 사이 그리기가 다른 Grab draw 의 사각형과 안 겹치면 한 번의 합집합 복사로 충분), SRV 128개 전부 해제 대신 실제 묶인 슬롯만 해제, 복사를 같은 명령 묶음 안에 유지. Unity 원본은 같은 초과에서 3.5ms 이므로 Unity 가 grab 을 어떻게 하는지(RenderDoc 없이: 원본 실행 중 D3D11 호출 수를 우리 Present 후크로 세는 진단) 비교.
- **주의: Claude 의 grabRect 는 HELLO 를 나쁘게 만든다** — 같은 M4k+Grab 수정 빌드 HELLO 픽셀 PSNR 42.8 → **39.8**, 최대 (155,59,136). 일부 Grab draw 의 사각형이 실제로 그리는/읽는 범위보다 작다(경계 상자가 실제 정점과 다름? 타일링/스크롤 스프라이트? 투영 방향?). Grab draw 별로 우리 사각형과 실제 정점 화면 범위(사전 변환 결과)를 비교하는 검증을 넣고, 확실하지 않은 draw 는 전체 복사로. HELLO PSNR 이 42.8 아래로 내려가면 안 된다. Exit/DD 는 이 수정으로 픽셀이 그대로였다.
- 깨끗한 측정(M4k+Grab 만): Exit A 332.1 / B 233.1 (main 0.95, render 1.17, native GPU 1.49ms), DD 원래 크기 A 242.8 / B 41.7.
