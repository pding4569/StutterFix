# Gemini D3D11 회복 설계 검토 — 2026-10-23

사용자가 첨부한 `Gpudeco D3d11 Bottleneck And Recovery Plan.md`를 현재 코드와 원시 기록에 대조했다. 외부 문서의 결론을 승인/측정 결과로 간주하지 않는다.

## 질문에 대한 코드 근거

1. **명령 목록은 준비 때 한 번 기록한다.** `gpudeco_copy.h`의 create 경로에서 deferred context로 기록/FinishCommandList하며 replay는 ExecuteCommandList(TRUE)만 한다. source/target은 COM 참조를 보유한다. 매 프레임 명령 목록을 다시 만드는 구조가 아니다.
2. **대체 메시의 보임은 Renderer.enabled다.** `GpuLiveSlice.ApplyAlpha`는 원래 값과 다를 때만 쓰며 pendingAlpha로 갱신한다. forceRenderingOff는 원본 SpriteRenderer 소유권/복구에 사용한다. 262~276초 토글 횟수는 아직 측정하지 않았다.
3. **프로토콜은 matshare off를 명시한다.** 원본 SpriteRenderer를 유지하며 MatShare 명령은 끈 상태다. 다만 이번 clean ABBA에 render census를 추가하지 않았으므로 다른 경로를 포함한 실제 재질 공유/엔진 배칭 여부는 확인 안 됨이다. 설정 이름만으로 실제 배칭을 확정하지 않는다.
4. **색 half-up 양자화는 이미 적용한다.** `GpuDecoCopy.TryCreate`의 CopyRun `_CopyQuantizeColor=1`이 normalized 버퍼를 만들고 native는 그 144바이트를 복사한다. native 복사 커널에 다시 양자화를 넣지 않는다. 기존 색 수식/셰이더를 바꾸는 근거로 삼지 않는다.

## 채택할 조사와 정정할 주장

- 복사/그리기/dirty 업로드를 따로 측정하는 방향은 유효하다. skipcopy/skipdraw는 그림이 달라지는 진단이므로 G1/G2 채택 판과 구분해야 한다. 그리기를 빼면 GPU 픽셀 부하/가림/배칭도 함께 달라지므로 한 수치만으로 Unity CPU 드로우 제출을 유일 원인으로 확정하지 않는다.
- 관리+native CPU 합과 프레임 시간의 차이는 **아직 계측되지 않은 구간**이다. 다른 스레드와의 중첩, GPU/동기화/엔진 비용을 차이값만으로 분해하지 않는다. 고정 FPS 임계값 100/140이나 dirty 업로드 0.15ms만으로 인과를 판정하지 않는다.
- 46묶음 거의 모두가 매 프레임 실행됐다는 수치는 이전 후보에서 수집하지 않았다. 새 정지 시점의 1/46·5/46은 확인했으나 14초 전체 평균이 아니다. 근접성 변경 ABBA가 빨라져도 유일한 병목 확정으로 확대하지 않는다.
- SPSC masked/scatter 경로의 독립 fixture와, 그 경로가 없던 round24 기존 native8 전곡 완주는 **서로 다른 후보**다. 이를 합쳐 새 masked/scatter 후보의 전곡 안정성 검증이라고 하지 않는다.
- CopySubresourceRegion은 `sf_deco_copy_create`에 이미 구현되어 있으며 과거 round13의 on 경로와 on8 경로를 구분한다. 신규 후보를 만든다면 동일한 마스크/정점 정규화 조건으로 비교해야 한다. 공식 API는 버퍼 범위/타입/format/매핑 조건을 요구하며 성능 향상을 보장하지 않는다.
- 동일 layer/order/Z라고 해도 Unity 투명 정렬의 다른 기준과 비소유 renderer 장벽을 모두 보존했다는 증명은 아니다. 기존 병합의 픽셀 실패를 무시하고 SubMesh 병합을 즉시 적용하지 않는다.
- GPU→CPU 전환 후 진행 중 Rec 재등록 누락은 코드에서 확인했다. 현재 논리 필드 복구와 다음 프레임 이후 연속성은 별도다. 현재 ABBA는 off 직후 stop/seek/reset하므로 문서가 말한 'off3가 반드시 정지해 왜곡됨'을 이번 프로토콜의 사실로 간주하지 않는다.
- Rec 복구 시 일반 생성 함수를 현재 값으로 호출하고 필드만 나중에 덮으면 엔진/배치/시작값 부작용이 생길 수 있다. 원래 start/change/end/누적 float Pos/이징/완료 및 교체 의미를 검증하는 전용 복구 경로가 필요하다. 같은 paused 시각에서 즉시 끄는 경우도 복구 대상이다.

공식 근거: [CopySubresourceRegion](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-copysubresourceregion), [ExecuteCommandList](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-executecommandlist).

round26 clean ABBA 완료 후 +26.743351%·CPU0.505004ms로 CPU 관문 실패를 기록했다. round27의 실제 구간 활성 묶음은 평균8.624197·최대12이므로 5/46 정지 표본으로 구간 전체 비용을 추정하지 않는다. 다음 round28은 같은 마스크의 CopySubresourceRegion 비교 후보이며 기존 8-UAV를 제거하지 않는다. 상세 수치/미확인 항목은 `codex-gpudeco-song-result.md`를 따른다. 연구 기본 꺼짐/Player 제외, 후킹 추가 0, main/커밋/푸시/태그/릴리스/ZIP 0을 유지한다.

후속 round28의 실제 같은 마스크 하드웨어 복사는 +11.208226%·CPU0.531239ms로 관문2개 실패다. 같은 GPU 화면의 복사 생략 대조는0픽셀이지만, 더 빠른 기본 경로로 채택하지 않는다. '하드웨어 복사 지원'과 '이 게임에서 더 빠름'은 별도다. 원본/GPU1픽셀·최대2 차이와 미구현 Rec복구를 남겨 둔 채 완성형이라고 부르지 않는다.
