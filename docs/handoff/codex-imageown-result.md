IMAGEOWN=FAIL fps_before=NA fps_after=NA gain_pct=NA pixel_max=NA fallback=NA spikes_30ms=NA reason=GAME_NOT_RUN_AND_COLLAPSE_OWNER_HOOKS_PENDING

# 이미지 소유 작업 결과 — 2026-10-25

게임 실행·설치·커밋·푸시·자동 장치 호출·설정/제어 파일 변경은 이번 호출에서 모두 0회다. 사용자 금지를 유지해 실제 픽셀·FPS·폴백·끊김은 측정하지 않았다. 원인 확정이나 성능 성공으로 판정하지 않는다.

## 지시 순서별 상태

| 순서 | 이번에 완료한 준비 | 남은 근거/작업 |
|---|---|---|
| 1(a) | `images refreshpose on/off`와 `GPUIMG_DIAG` 로그. `RefreshGpuImageGeometry` 시작에서 선택적으로 `d.UpdatePosition()` 1회 | 실제 260/262/270초 정지 픽셀 비교 미실행 |
| 1(b) | `images locate cpuvertices on/off`. 신선한 정지 활성화에서 CPU `TransformPoint` 위치만 GPU VB에 주입 | 실제 원본/GPU/CPU 정점 주입 픽셀 미실행 |
| 2 | 원인별 비교·잡음·복원 검사 계획 준비 | 원인 미확정. 수식/정렬을 추측으로 수정하지 않았고 maxdiff<=3 미증명 |
| 3 | 소유 수 입구 23547/28523, 32바이트 텍스처 부분 갱신 헬퍼·호출, 알파 더티 감시 구현 | Claude 소유 `PrepareDrawCollapse`의 두 연결 호출 필요. 이미지 draw 통합은 미완성 |
| 4 | 이미지 소유 자체를 비교하는 ABBA 및 3시점 픽셀 판정 명령 준비 | 실측 0회. 새 후보 +20%, 폴백 0, 30ms 이상 0 모두 NA |

## 구현과 진단 범위

- `GpuDecoImagePlayback.cs`: 기본 꺼짐인 pose 진단 선택, 이미지 교체/Flush 시 텍스처 슬롯 갱신 호출, 알파 이벤트 소비, 대기/감시/검사 수 로그를 추가했다.
- `GpuDecoImageGeometryFixture.cs`: 기존 기하 fixture와 별도로 정지 진단 및 `images begin/end/pixel/restore/result` 판정을 추가했다. 미완성 `images isolated`와 새 `ImagePixelProbe`는 사용하지 않는다. csproj·compute·번들은 변경하지 않았다.
- `GpuDecoImageTimeline.cs`: 실제 재생 코드가 사용하는 `GpuDecoImageWakeQueue`를 분리했다. 준비 단계에 HashSet 용량을 예약한다.
- `LazySprite.cs`: AUTOTEST 이미지 소유일 때 원래 setter 직전에 현재 GPU 논리 필드를 동기화한다. 이미지 setter는 한 번만 실행한다. 사전 동기화 실패는 CPU 복귀 후 원래 setter로 이어진다.
- `GpuDecoDrawCollapse.cs`: 허용된 소유 수 검사와 `UpdateDrawCollapseTextureSlot` 헬퍼만 수정했다. 준비용 등록/주소 보존 함수와 필드는 이미지 파일에 두었다. 독립 렌더러 MPB도 갱신해 draw 통합을 끌 때 최신 이미지를 보존한다.

CPU 정점 주입은 `GpuLiveOwned=28523`, CPU fallback=0, 정지한 활성화 직후, 독립 native copy, 성능 창 밖에서만 허용한다. 색/UV/비이미지 정점은 유지한다. 일반 GPU 평가는 정지 프레임에도 실행되므로 단발 VB 쓰기 대신 main camera pre-cull에서 정상 평가 뒤 주입/복사를 반복한다. 해제하면 정상 평가·복사를 다시 실행한다. 재생/소유권/곡 시각이 바뀌면 주입을 해제한다. **이 코드는 게임에서 아직 실행하지 않았다.**

`GPUIMG_DIAG`는 O=CPU, G=GPU 수식, D=CPU 정점 주입, U=UpdatePosition 선택의 변경 픽셀/최댓값을 남긴다. O/O2, G/N, D/DN, U/UN 잡음과 G/G2 복원을 `assertzero`로 검사한다. D가 같아지는 결과는 해당 정지 표본의 기하 경로를 좁히는 근거이며, 전곡·모든 원인의 배제를 자동 선언하지 않는다.

## 알파 감시 변경

기존 4,476개 미룸 집합의 매 프레임 복사/전수 평가를 제거했다. `GpuLiveSlice.pendingAlpha`를 cached FieldRef로 읽어 `FlushDirty`가 소비하기 전에 해당 미룸 장식만 깨운다. 이 목록을 수정하거나 소유 파일을 패치하지 않는다.

`MarkDirty`는 알파 효과의 시작/완료에만 생긴다. 시작 프레임 alpha=0인 애니메이션은 다음 프레임에도 감시해야 하므로, **미룸 중이며 알파가 애니메이션 중인 항목만** 감시 집합에 남긴다. 정적 비양수 알파는 다음 이벤트까지 잠든다. 따라서 감시 0은 정적 대기 조건에 한한 수치다. 모든 알파가 애니메이션 중이면 감시 수가 늘어날 수 있으며 실제 비용 절감은 미측정이다.

## Claude 파일 소유자에게 필요한 연결

`GpuDecoDrawCollapseRenderer.cs`는 수정하지 않았다. 해당 소유자가 `PrepareDrawCollapse`에 아래 두 호출을 연결해야 한다.

1. 초기 텍스처 수집 루프 뒤, 배열 티어/직접 슬롯을 계산하기 전에:

```csharp
#if AUTOTEST
RegisterGpuImageCollapseVariants(textures);
#endif
```

2. `gpuCollapseTexture = new ComputeBuffer(...); gpuCollapseTexture.SetData(info);` 뒤:

```csharp
#if AUTOTEST
RememberGpuImageCollapseAddresses(addresses);
#endif
```

등록은 공유 텍스처 wrap을 미리 바꾸지 않는다. 원래 V15 setter/Defer가 원래 이벤트 시점에 Clamp를 적용한다. 지원하지 않는 mip/wrap이나 미등록 주소는 CPU 복귀로 처리하며, 연결 누락을 통과로 숨기지 않는다. 주소는 해당 ComputeBuffer 객체와 짝지어 이전 준비의 주소를 재사용하지 않는다. 주소 사전은 숫자만 보관하며 맵 해제 때 비운다.

`GpuDecoGpu.BeginCollapse/DispatchCollapse`의 `_ImageShape` 바인딩과 canonical compute의 `EvaluateCollapse -> WriteVerticesAt` 안 `Flags&64` 경로는 소스에서 확인했다. 새 이미지 draw 결과의 게임 검증은 아니다. 기존 drawcollapse 게임 게이트에는 23547 검사도 남아 있으므로 이미지 ABBA는 별도 `images ...` 판정을 사용한다.

## 외부 시험용 계획

- `tools/optimization/out/imageown-diag1-steps.json`: 156단계, 기본 모드. 두 가설 및 260/262/270 정지 비교를 먼저 수행한다. 새 managed DLL과 검증된 이미지 커널 번들이 필요하다.
- `tools/optimization/out/imageown-abba1-steps.json`: 320단계, **실행 선행 조건 미충족**. 픽셀 원인 수정·maxdiff<=3 확인과 위 두 연결을 끝낸 뒤에만 외부에서 실행한다.

생성기는 게임·설치·장치를 호출하지 않는다. `--mode diag|abba`, `--bundle`, `--refreshpose`, `--self-test`를 지원한다. 기존 템플릿/JSON은 수정하지 않았다. 입력 맵은 기존 계획의 Arche 경로를 재사용했다.

기존 생성기는 open 전에 `images own on`을 넣었고 playback 선택/compile이 빠졌다. 새 계획은 snapshot 전 candidate 선택, 정지 CPU에서 compile/own/matrix 선택, 이후 준비 순서를 따른다. 각 준비에서 drawcollapse reset·unsafe off를 명시한다.

ABBA는 A=이미지 candidate off+GPU23547+draw 통합, B=이미지 candidate/own on+GPU28523+draw 통합이다. 각 팔을 재열어 준비해 의도적 CPU fallback 4,976개가 생기는 비교를 피했다. 260..270초 측정 창에는 정지·캡처·readback이 없으며 재개/seek/stop 전에 소유권을 반환한다. 3440×1440@165 검사만 하고 디스플레이를 변경하지 않는다. B/A>=1.20을 요구한다(실제 A가 202이면 B>=242.4FPS). CPU/런타임 폴백과 B의 텍스처 제외도 0을 요구하며, 제외 수는 별도로 기록한다. 모든 창에서 30ms 이상 프레임 0, 세 시점 maxdiff<=3, 잡음/복원 0, native 오류 0, 네 소유 해제 복원이 있어야 게임 게이트 PASS다. 설치 복원·전곡 수명은 별도 미검증이다.

## 오프라인 검증

| 검사 | 결과 | 증거 |
|---|---|---|
| DEV 빌드 | 오류 0, 경고 48 | `out/imageown1-dev-final.txt` |
| 일반 Player 빌드 | 오류 0, 경고 36 | `out/imageown1-player-build.txt` |
| PlayerAuto 빌드 | 오류 0, 경고 40 | `out/imageown1-playerauto-final.txt` |
| LoadCheck DEV/Player/PlayerAuto | FieldRef 279/229/281곳, 틀림 0/0/0 | `out/imageown1-loadcheck-*.txt` |
| 실제 큐 클래스 검사 | 4,495개 통과 | `out/imageown1-queue-check.txt` |
| 계획 생성기 검사 | 5개 통과 | `out/imageown1-generator-tests.txt` |

빌드는 `out/imageown1/build/{dev,player,playerauto}`와 별도 intermediate에 격리해 기존 bin 산출물을 덮어쓰지 않았다. 일반 Player 연구 제외를 LoadCheck로 확인했다. LoadCheck는 필드 형식/이름과 후크 계약의 오프라인 검사이며 실제 Mono/Harmony/게임 실행 증명이 아니다. 큐 검사는 실제 소스 클래스를 링크해 정적 대기 4,476개, 0→양수·음수 오버슈트·완료·중복 이벤트·외부 flush·맵 정리를 검사했다. 픽셀이나 FPS 검사가 아니다.

이번 수정 범위는 이미지 파일 3개, `LazySprite.cs`, 허용된 drawcollapse 검사/헬퍼, 이미지 계획 생성기, 이미지 문서와 자체 `out/imageown1*` 검증 산출물이다. Claude 소유 renderer/policy/export/Dormancy/TightLate/FastMove, csproj, canonical shader, 기존 번들, 설정/제어 파일은 수정하지 않았다. 실제 원인 확정과 수용 기준 달성은 외부 시험 결과 및 파일 소유자 연결 뒤 남아 있다.
