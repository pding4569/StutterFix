# 네이티브 트윈 이어받기 — 속도 (Codex 작업 지시)

## 게임 측정 (2026-10-26, Arche 곡 254.4초 에디터 중간 시작, 방금 만든 다채널 구현)
정확성은 통과: 이어받기 247개(장식) / 채널 707개 / 값 불일치 0 / CPU 처리 장식 247 -> 0.
그러나 켜는 데 **2.4초**가 걸린다(이어받기 없을 때 시작 ~200ms):

켜기 단계: 소유분류 **763ms**(원래 ~25ms) | 초기상태캡처 **786ms**(원래 ~55ms) | (StartGpuLive 끝) **727ms**(원래 0ms)

즉 이어받기 검사가 3군데(ClassifyGpuActivationOwnership, CaptureInitialGpuLiveState, StartGpuLive 끝 쪽 검증/commit)에서
각각 장식·채널마다 느린 방식(리플렉션 GetField 반복, Harmony.GetPatchInfo 반복, AccessTools.Method 반복, LINQ/배열 할당 등)으로 돈다.
채널 하나에 약 1ms 수준.

## 목표
이어받기 전체 추가 비용 **합계 20ms 이하**(247장식/707채널 기준, 채널당 30µs 이하). 동작/안전 규칙은 바꾸지 말 것.

## 방법 (순서대로)
1. 활성화 한 번당 한 번만 계산하는 전역 검사(Compat.QuartzOptimizer, ForeignSetter, DOTween.Version,
   NativePatched(...) 의 메서드별 결과, nativeUpdateMethod 패치 여부, links/isUpdateLoop 등)를 **활성화 시작 때 한 번** 캐시한다.
2. FieldInfo/MethodInfo/Type 조회는 정적 캐시(Dictionary 또는 정적 readonly 필드)로. 가능하면 필드 읽기를
   `System.Linq.Expressions`/DynamicMethod 로 만든 델리게이트 또는 `AccessTools.FieldRefAccess` 로 바꾼다
   (이 레포는 DynamicMethod 생성자 호출기를 이미 쓴 선례가 있음: `FloorAnim`).
3. 장식당 검증 결과(NativeAdoptSpec 묶음)를 **한 번 만들어 활성화 끝까지 재사용**: 분류/캡처/검증에서 같은 트윈을 다시 읽지 말고,
   변하지 않았는지는 가벼운 비교(Elapsed/값 비트)로만 다시 확인한다. 단, "commit 직전 현재 값 비트 일치" 검사는 유지(채널당 몇 µs).
4. 로그(AUTOTEST)에 이어받기 단계 시간을 따로 찍는다: `이어받기 검사 Xms / 캡처 Yms / commit Zms`.

## 규칙
- 게임 실행·DLL 설치·커밋 금지. 빌드만(두 가지, 오류 0): `-c Release -p:Edition=Player -p:AutoTestBuild=1`, `-c Release -p:Edition=Player`.
- 안전 규칙(MVID+MetadataToken 고정, 부분 이어받기 금지, commit 뒤 Kill, 기본 꺼짐)은 그대로.
- 끝나면 `docs/research/native-tween-adopt-impl-notes.md` 끝에 "속도" 절로 무엇을 캐시했는지 적는다.
