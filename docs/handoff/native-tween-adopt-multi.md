# 네이티브 트윈 이어받기 — 여러 채널로 넓히기 (Codex 작업 지시)

## 왜 (게임 측정, 2026-10-26)
`GpuDecoNativeAdopt.cs` 는 장식에 PositionX 트윈 **하나만** 돌 때만 이어받는다. Arche 곡 254.4초 에디터 중간 시작에서
`TryReadNativePosition` 거절 집계(코드 2 = 채널이 둘 이상이거나 PositionX 가 아닌 것):

- [PositionX+PositionY+Rotation (모두 네이티브)] = 171
- [PositionX+PositionY+Rotation+Color] = 36
- [Color] = 30, [ScaleX+ScaleY] = 9, [PositionX+PositionY] = 1  (합 247)

즉 PositionX 단독 장식은 0개 → 이어받기 0개. 이어받으려면 **한 장식이 가진 모든 실행 중 네이티브 채널을 한꺼번에**
이어받아야 한다(일부만 이어받고 나머지를 CPU 에 남기면 그 장식은 소유할 수 없다).

## 할 일
1. `ffxMoveDecorationsPlus.StartEffect`(및 같은 길로 트윈을 만드는 게임 코드)가 PositionX/PositionY/Rotation/ScaleX/ScaleY/Color
   각 채널에 만드는 DOTween 의 닫힘 구조(getter/setter/onUpdate/onComplete 델리게이트의 메서드 토큰, 닫힘 필드 이름,
   plugin 종류: float/Color, end 값 계산식, 상대/절대)를 IL 로 확인한다 (`tools/ilscan`, METHOD=ffxMoveDecorationsPlus 등).
   PositionX 에서 쓴 방식(MVID+MetadataToken 고정, 닫힘 필드 비교)을 채널마다 같은 엄격함으로 확장한다.
2. `TryReadNativePosition` → 채널 종류별 읽기. 한 장식의 모든 실행 중 채널(네이티브)이 각각 안전하면 채널 전체를 한 묶음으로 성공,
   하나라도 거절이면 장식 전체를 CPU 에 남긴다(부분 이어받기 금지).
   Color 채널(plugin ColorPlugin / Color 값)은 현재 GPU 소유 쪽이 지원하는 키(`Key`) 번호와 맞춘다 (`DecoAnim` Rec 키 표 참고:
   `CaptureInitialGpuLiveState`, `ClassifyGpuActivationOwnership` 의 key 변환식 `first<=4?first+1: ...`).
3. 활성화 흐름은 PositionX 때와 같다: 캡처 → 검증(현재 값 비트 일치) → commit 뒤에 네이티브 트윈 `Kill(false)`.
   commit 전에 실패하면 원본 네이티브 트윈은 그대로 두고 롤백. 채널 묶음 단위로 원자적이어야 한다.
4. 진단: AUTOTEST 로그 `[GPU 장식] 이어받기: N개(장식), 채널 M개, 값 불일치 K개, 거절 …` 에 채널 조합별 거절 개수를 이미 있는
   `nativeCombos`/`NativeRejectSummary` 방식으로 유지한다.
5. 기본은 꺼짐(`DrawCollapsePolicy.AdoptNativeTweens=false`) 유지. 켜면 위 동작.

## 규칙
- 게임 실행·DLL 설치·커밋 금지(Claude 가 한다). 빌드만: `dotnet build -c Release -p:Edition=Player -p:AutoTestBuild=1` 과 `-p:Edition=Player`, 오류 0.
- 추측으로 닫힘 필드 이름을 쓰지 말고 IL 에서 확인. 모르면 거절(CPU 에 남김)이 정답.
- 끝나면 `docs/research/native-tween-adopt-impl-notes.md` 에 채널별 확인한 구조와 남은 위험을 적는다.
