# 네이티브 트윈 GPU 이어받기 구현 (Codex 명세)

작업 폴더 `E:\StutterFixWork\wt-adopt` (브랜치 `claude/native-adopt-research`, 기준 5e8ea58). 먼저 `docs/research/native-tween-adopt.md`(당신이 쓴 타당성 조사)와 `docs/handoff/native-tween-adopt.md` 를 다시 읽어라.
**게임 실행·커밋 금지.** 소스 수정은 허용(이 작업은 구현이다). 빌드만 확인: `dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1` 와 `-p:Edition=Player` 둘 다 오류 0. 게임 실험은 우리가 한다.

## 목표
에디터에서 곡 중간(약 254초)부터 재생하면, 따라잡기 중 게임 원래 코드가 장식에 네이티브 DOTween 트윈을 만들어(위치X 197, 색 22, 크기X 8 = 227~231개) 켜는 순간 그 장식들이 CPU 처리로 남는다(`GpuDecoActivation.cs` `ClassifyGpuActivationOwnership` 의 fallback 조건 `t.active && (r==null || !r.Running)`). 이 중 **PositionX(채널 0, TweenType.PositionX) 트윈만** 켜는 순간 GPU 소스로 이어받아 CPU 처리 장식을 줄인다. 색·크기X 는 이번 범위가 아니다(그대로 CPU 처리).

## 설계 (조사 문서의 결론을 따른다)
1. 설정/정책 플래그 `DrawCollapsePolicy.AdoptNativeTweens` (기본 **false**, AUTOTEST 명령 `gpudeco light adoptnative on|off` 를 `GpuDecoExport.cs` 의 `light` 명령 처리부에 추가).
2. `ClassifyGpuActivationOwnership` 에서 fallback 판정 전에, 장식 d 의 `eventTweens` 에 **활성 네이티브 PositionX 트윈 1개뿐**이고 안전 조건(조사 문서 5절 기준표: 지연 없음, 반복 없음, 상대 이동 아님 또는 해결 가능, 지원 이징(`GpuDecoGpu.Supported`), 정방향, `TweenerCore<float,float,FloatOptions>`, onComplete/onUpdate 콜백이 이어받기 전에 안전하다고 확인된 것만)을 모두 만족하면 "이어받기 후보"로 표시한다. 하나라도 어긋나면 지금처럼 fallback.
3. `CaptureInitialGpuLiveState`(GpuDecoExport.cs, 이미 진행 중인 `DecoAnim.Rec` 를 `GpuLiveSource` 로 바꾸는 코드 `recSourceMap` 근처)에서 이어받기 후보의 네이티브 트윈에서 시작값·끝값·경과시간·길이·이징(·over·period)을 읽어 **그 장식의 채널 0 에 대한 `GpuLiveSource`** 를 만든다(`Pos=경과, Dur=길이`). 채널값은 `SetMasterSlotAnimated` 와 같은 방식. 이어받은 뒤 원래 트윈은 **콜백 없이** 죽인다(`Kill(false)`). 죽이기 직전 값 일치(원래 트윈의 현재값 == GPU 소스의 현재값)를 AUTOTEST 에서 검사해 로그(`[GPU 장식] 이어받기: N개, 값 불일치 M개`).
4. **실패하면 반드시 원래대로**: `BeginGpuActivationUndo`/`RollbackGpuActivation` 이 이어받기도 되돌려야 한다(죽인 네이티브 트윈 복구는 불가능하므로 **죽이는 것은 활성화가 확정(`CommitGpuActivationUndo`)된 뒤에**, 그 전에는 읽기만 한다). 활성화 중 예외가 나면 이어받기 후보는 건드리지 않은 상태로 원본 유지.
5. 곡 끝·정지·`StopGpuLive` 에서 GPU→CPU 복원(`BuildGpuResumePlan`)이 이어받은 소스를 `DecoAnim.Rec` 로 되돌리는 경로가 이미 일반 소스와 같은 방식으로 동작하는지 확인하고, 이어받은 소스도 같은 `GpuResumeSpec` 으로 흘러가게 한다(이 경로의 `r.Key`/`r.D` 요구를 만족시킬 것).
6. 진단: `ClassifyGpuActivationOwnership` 의 AUTOTEST "CPU 처리 이유" 로그에 이어받기 개수를 추가.

## 지킬 것
- 기본 꺼짐. 꺼져 있으면 동작·성능이 변경 전과 완전히 같아야 한다(분류 코드에 분기 하나만).
- 비트 동일성: 이어받은 소스의 채널 값이 원래 트윈과 비트 단위로 같아야 한다(조사 문서 4절).
- 위험한 가정을 코드에 숨기지 말고 `docs/research/native-tween-adopt-impl-notes.md` 에 "구현에서 확인 못 한 것" 으로 적어라.
- 최종 보고(한국어): 바꾼 파일·줄, 빌드 결과, 우리가 게임에서 확인해야 할 시험 순서(켜기 전/후 CPU 처리 장식 수, 같은 일시정지 장면 켠 채/`gpudeco live off` 픽셀 비교, 곡 끝 복원).
