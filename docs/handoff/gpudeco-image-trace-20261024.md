# 이미지 변경 트레이스 — round97~101

## 현재 구현과 측정 범위

- AUTOTEST 전용 `GpuDecoImageTimeline`은 기존 예약 순서/대상 순서/중복 대상을 보존한 불변 이미지 선택자 표다. 같은 곡 시각의 이벤트도 합치지 않으며 Source ID를 occurrence key로 사용하지 않는다. 이름은 메모리에만 보관하고 로그/문서로 내보내지 않는다.
- `null`은 이미지 필드 미사용, 빈 문자열은 사용된 빈 선택자다. 빈 선택자가 실제 게임 그림을 지우는지는 확인 안 됨이다. `ClearEvents`라는 초기 계측 명칭은 최종 코드에서 `EmptySelectorEvents`로 명확히 했다. 원래 SetSprite(null)의 동작을 추정하거나 바꾸지 않는다.
- 표는 미래 GPU 이미지/UV/shape 갱신에 필요한 **선택자/대상 메타데이터**다. 텍스처 해석, 기하 갱신, GPU 이미지 재생, CPU 장식 소유권 확대는 아직 연결하지 않았다. 전역 곡 시각으로 이미지를 예측하는 코드도 없다.
- 준비 중 입력/대상/유한 시간/예산을 검증한다. 별도 순수 검사20/20: 동일 시각과 반복 Source, 중복 대상, 이름 대소문자, 빈 이름/미사용 구별, CSR 대상별 순서, 입력 배열 비공유, 거부/예산 경계. 픽셀/성능 증명이 아니다.

## 실제 게임 결과

| 판 | 범위 | 결과 |
|---|---|---|
| 97 | 맵 준비 후 명시적 컴파일/상태 | 이미지 이벤트1833, 선택자104, 대상 참조8751, 이미지 이벤트 대상 장식5087, 빈 선택자 이벤트1, Source ID 재사용0 |
| 97 | 표 payload 하한 | 247540바이트. 배열/객체/string header, 준비 중 임시 메모리/전체 상주 힙 제외 |
| 98 | 같은 맵을 seek한 뒤 요청 곡256~270초의 `FastMove.Run` 경로 | 관측973/헤더·전체 대상 순서 일치973/모르는 Source0/불일치0/예외0 |
| 97/98 | 종료 | 각 exit0·게임 정상quit·설치9상태 원본9/9복원 |

98은 원래 CPU 이미지 setter를 그대로 둔 읽기 전용 그림자 검사다. 필터/해상도/렌더러/충돌체/원래 이벤트 실행을 바꾸지 않았다. `FastMove.Run` 밖의 원래 코드 경로는 관측하지 않았고, 실제 SetSprite 뒤 그림·UV·원래 크기·LazySprite pending 결과를 대조한 것도 아니다. 전체1833개가 실제로 모두 관측되었다는 뜻이 아니다. 캡처 없는 FPS 판으로 쓰지 않는다.

## 생명주기와 격리

- 새 Harmony/PlayerLoop/네이티브/셰이더 변경0. 기존 FastMove의 이미지 이벤트 경계에서만, 명시적 shadow on 때 헤더/대상 비교를 한다. 예외는 계측 실패로 세고 shadow를 끄며 원래 효과 실행 흐름을 유지한다.
- Shadow는 기본false, 일반 Player/Dev 빌드 제외다. `ClearBaseline`→`ClearGpuImagePlan`에서 표/대상·Source 사전/활성 플래그를 놓는다. 기존 `DecoBake.Shutdown`/Main.Unload 경로를 따른다.
- 최종3빌드와 LoadCheck 확인. 98 게임 뒤 최종 변경은 빈 선택자 명칭 명확화뿐이며, 최종 DLL의 추가 게임 재시험은 하지 않았다. 자료 `out/round97-98-image-plan-summary.json`, `out/round98-build/`.

## 다음 구현 단위

1. 이미 불러온 `TextureManager.CustomSprite`를 읽기 전용으로 해석해 선택자별 Sprite/Texture/UV/정점/삼각형 메타데이터를 검증한다. 현재 설치판 IL에서 `CustomSprite.sprite`는 필드임을 확인했다. 빈/없는 이미지, non-quad, topology 변경은 별도 거부/CPU 경로로 남긴다.
2. 원래 CPU 이미지 setter와 LazySprite pending/즉시 적용의 실제 결과를 구분해 대조한다. 같은 파일 이름 또는 같은 물리 Sprite만으로 noop이라고 판단하지 않는다. 특히 wrapMode는 텍스처 전체의 공유 값이라 원래 이벤트 순서/부작용을 보존해야 한다.
3. 그 증거 뒤에만 원래 setter 실행과 GPU의 기하/UV/텍스처 갱신을 연결한다. 오류 시 진행 중 효과를 두 번 실행하거나 CPU 트윈/시계를 추정 복구하지 않는다. 여전히400FPS/장식 전체 비용0/완성형 완료는 확인 안 됨이다.

## round99 — 자산 사전 검증과 실제 setter 결과

- 이미 로드된 선택자104개: 정상 quad103/빈 선택자 null 인자1/없는 파일0/non-quad0/split-alpha0/무효0. Sprite/Texture/PPU/정점4/UV4/삼각형6을 읽기 전용으로 캡처했다. 빌려온 Unity 자산 Destroy0, 원래 이미지 읽기·SetSprite·LazySprite 순서 변경0이다.
- 요청 곡256~270초: 이미지 경계973/헤더·전체 대상 일치973, 자산 재확인973/불일치0. 실제 이미지 대입6052회 중 즉시1137/지연4915, 요청 자산 또는 지연 pending과 불일치0/모르는 Source0/예외0. null 인자59회는 삭제/유지 결과를 추론하지 않았다.
- 실제 texture wrap 표본 Clamp1813/그 외4180. wrapMode를 무조건 Clamp라고 가정하지 않는다. 기존 게임의 disableV15Features 분기·이벤트 순서와 LazySprite 부작용을 유지해야 한다.
- 관측은 FastMove.Run 이미지 대입 직후, 후속 originalSize/smoothing/mask 블록 전이다. 이후 기하·물리 픽셀·전체곡·GPU 텍스처 바인딩·FPS 개선은 확인 안 됨. 메타데이터 getter/배열 수집 비용이 있어 성능판으로 쓰지 않는다.
- dev/player/auto3빌드·LoadCheck 모두0오류, 연구 코드는 AUTOTEST 전용. 정상quit/exit0/설치9상태9/9복원·게임 프로세스 종료 확인. 자료 out/round99-image-results-summary.json, out/round99-build/.

## round100 — GPU 모양·UV 부분 갱신

- AUTOTEST 전용 `GpuDecoGpu.UpdateImageGeometry`는 `_Static`10개·UV4개를 대상 슬롯에만 SetData한다. 메시/인덱스/버텍스 버퍼를 재생성하지 않고 기존 네이티브 포인터의 수명을 유지한다. 채널 Initial/Tracks/Anchors 참조와 대상 ID/Index가 달라지면 거부한다.
- 모든 입력을 쓰기 전 검사한다. 토폴로지 변경/잘못된 길이/비유한 값/0 나눗셈 상수/허용 밖 flags/대상 범위/채널 교체를 거부한다. GPU 업로드 자체가 중간에 예외를 내면 호출자가 CPU 복귀 또는 Dispose해야 한다. GPU 쓰기2개가 원자적이라고 주장하지 않는다. 아직 실제 소유권 경로에 연결하지 않았다.
- 맵의 실제 quad103개를 양쪽 슬롯에서206번 갱신: 새 GPU 생성 결과와 버텍스 불일치0, 이웃 슬롯 변경0, 토폴로지 거부0, 버텍스 버퍼 포인터 변경0. 잘못된 입력12종 거부12/12·거부 뒤 GPU 결과 변경0, 원래 모양 복구 후 불일치0.
- 실제 장식 setter/표시 렌더러 변경0, 텍스처 바인딩·실제 화면 픽셀·FPS 확인 안 됨. paused fixture는 GPU GetData 대기를 포함하므로 성능판0이다. 신규 native/셰이더 수정0.
- 3빌드/LoadCheck 오류0, exit0·정상quit·게임 종료·설치9원본9/9복원. 자료 out/round100-image-geometry-summary.json, out/round100-build/.

## round101 — 실제 스프라이트와 갱신 GPU의 격리 픽셀 대조

- 원래 맵 장식은 변경하지 않는다. 임시 SpriteRenderer와 MeshRenderer, 원래 재질을 복제한 재질1개, 이미 로드된 Sprite/Texture를 사용한다. 원래 텍스처 wrap/filter·공유 재질/충돌체/정렬·GPU 소유권 변경0이다.
- GPU는 같은 버텍스 버퍼에 모양·UV를 갱신하고, 임시 MeshRenderer의 property block에 해당 텍스처를 연결한다. 전체 화면 카메라나 native copy를 건드리지 않고 명시적 CommandBuffer로 양쪽을 따로 그린다.
- 256×256·흰 정점색·원래 복제 재질의 pass0·직교 투영에서103개 비교: 다른 그림0/다른 RGBA 픽셀0/최대차0. 비균일 그림102개, 첫 그림과 다른 그림102개, 토폴로지 거부0. 빈 렌더끼리의 대조만 통과한 것이 아니다.
- 이 격리 대조는 실제 맵의 원래 셰이더/모든 패스/정렬/마스크/색/카메라/필터와의 픽셀 동일성, 실제 native copy 포인터를 연결한 상태, 연속 이벤트 또는 LazySprite 지연 적용의 증명이 아니다. CPU 장식 setter/실제 GPU 소유권 확대0, FPS·전체 화면 품질·400FPS 확인 안 됨.
- 픽셀 읽기/GPU 대기/일시 객체 할당이 있는 paused fixture이므로 성능 결과로 쓰지 않는다. 임시 객체·재질·GPU 자원을 finally에서 해제하며 빌려온 Sprite/Texture는 파괴하지 않는다. RenderTexture.active를 원래 값으로 복원한다.
- 최종3빌드/LoadCheck 오류0, exit0·정상quit·게임 종료·설치9원본9/9복원. 자료 out/round101-image-texture-summary.json, out/round101-build/. native/셰이더 수정0·기존 G1/G2 원시 보존·커밋/푸시/배포0.

## 현재 다음 연결 범위

- 실제 효과의 scalar 채널/깊이와 이미지 setter 순서, stale CPU 값 동기화, LazySprite pending이 실제로 적용되는 경계, 원래 재질/property block 변경과 공유 texture wrap 부작용을 함께 보존한 뒤에만 이미지 대상 소유권을 확대한다.
- 현재 `GpuLiveSkip`는 이미지 블록 전 target 전체를 건너뛴다. 이미지 제외 이유만 지우면 이미지 setter까지 사라지므로 그렇게 활성화하지 않는다. 업데이트 API는 업로드 실패 시 기존 CPU 복귀와 함께 연결해야 한다.
