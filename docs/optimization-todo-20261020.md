# 최적화 할 일 정리 (2026-10-20) — 릴리스(2.8.0)는 미룸

기준: 알파(Unity 6000.3.25f1), 3440×1440, 자동 플레이, Quartz 없음, 기본 설정. 구간 시험은 `wait`+`retry` 방식(65초, 약 1.5분), 곡 전체 지표는 전곡.

## A. 효과가 확인된 것 — 마무리 중
1. **히트음 예약 묶기(HitSoundBatch, 실험·기본 꺼짐)**: HELLO 앞 65초 ABBA 평균 200→259(+29%), 1% low 91→132(+45%), 12ms 초과 49→20~26개. 형식 2그룹(모노/스테레오), 폴백 0.
   - 남은 일: ① **귀로 확인**(타이밍·음량·일시정지/재개·다시 하기·실패·곡 끝, 사용자) ② 전곡 A/B(HELLO 전곡, Arche) ③ 일반 버전에서 Quartz 병용 시 우리 것 우선(이중 재생 없음) 확인 ④ 에디터 중간 시작(`seek`)·곡 속도·커스텀 히트음 경계 ⑤ 메모리(PCM 8.7MB, 곡마다 해제) ⑥ 기본 켬 여부 결정(소리가 바뀔 수 있어 사용자 판단).
2. **boot.config 초기화 안내 카드**: 구현됨(`BootConfig`/`RestartAdvisor`/`SettingsWindow`), 화면 확인 남음(`bootnotice show` + `shot`). 초기화 직후 첫 실행은 평균 −13%, 12ms 초과 3.6배(알파 HELLO 전곡).

## B. 조사 끝, 측정으로 가설 확인이 필요한 후보 (`docs/engine-calls-20261020.md`)
3. **필터 `Material.SetFloat/SetColor` 정리**(HELLO 후반 7.4% ≈ 0.44ms/6ms 프레임이 상한, 실이득 미측정): ① 문자열→정수 ID 치환 단독 A/B ② 전용 머티리얼 필터의 동일 값 쓰기 생략(외부 쓰기·다중 패스·머티리얼 교체 보호, `_TimeX` 같은 매 프레임 값은 통과) — 먼저 문자열 해시 비용 vs 네이티브 쓰기 비용 분리.
4. **지휘자의 추가 tick/hold 음원 예약 묶기**(`extraTicksCountdown`/`holdSoundsData`; HitSoundBatch 가 맡는 `hitSoundsData` 밖). 호출량·이득 미확인.
5. **Quartz 나머지 후보 분리 측정**: `RemoveBallCoreParticles`(행성 중심·불꽃 파티클), 체크포인트 표시, 행성 색 계산 중복. 히트음만 끄면 단독 수준으로 돌아가서 영향이 작거나 없다고 보이지만 개별 측정은 못 함(시험 중단).
6. **UI `GUIStyle` 크기 측정 호출**(h170 0.8% ≈ 0.048ms): 소유자(우리 모니터/설정 창인지) 먼저 확인.
7. 특수 타일 색 반복 쓰기(`FloorMeshRenderer.set_color` 2.0% ≈ 0.12ms) 좁히기 — 위험 높음(공유 재질), 일반 색 캐시 금지(과거 FloorFix 사고).
8. `scrParallax.SetTrans` 공통 카메라 읽기 — TightLate 와 중복이라 후순위.

## C. 환경·호환
9. **일반 버전(3.3)과 알파(3.4) 양쪽 호환**: `croppedPivotOffset` 직접 참조(`DecoAnim` 꺼짐 사고)와 `targetSongTick` long/ulong 수정 완료. `tools/CompatCheck`(게임 멤버 참조 전수 검사)를 `pack.sh` 에 넣어 두 버전의 `Assembly-CSharp` 사본(`E:\StutterFixWork\gamerefs\`)으로 검사하는 단계 추가 필요. 직접 필드 참조는 `FieldRef`/존재 확인 도우미로.
10. `AllocFix` 비동기 키 입력 후킹: 알파 새 입력 API 에서는 꺼짐(곡 중 쓰레기 0.45MB/s 수준이라 영향 작음). 필요하면 새 모양(`GetSpecialInput` void, `RDInput` 정적 버퍼)에 맞춰 재작성.
11. **같은 프로세스에서 맵을 바꾸거나 반복하면 FPS 가 내려감**(2.7.3 에도 있음, Arche 한 번 연 뒤 HELLO −7~11%, 사용자 실플레이는 더 큼): 원인 미확인(힙 잔여 약 2배, 객체 수?). `objcensus`/`census`/`profsamp` 로 플레이 횟수별 추적.
12. Quartz 는 알파에서 `MissingMethodException`(입력 API 변경)으로 프레임을 무너뜨림 → `ModsOff` 유지. 시험은 Quartz 제외 조건 고정.

## D. 이미 끝났거나 기각 (다시 하지 말 것)
- 장식 애니메이션 새 엔진(DecoEngine/GroupStep/PosFast/DecoBatch/DecoGpu): 본전 또는 손실. 비용은 계산이 아니라 장식별 엔진 쓰기. 시차 쓰기를 건드려도 최대 5%(약 0.06ms).
- 채택: TightLate(+8%), ColorFast(−0.12ms), EulerMemo(−0.07ms).
- 게임 업데이트(엔진) 자체는 20% 하락의 원인이 아님(일반 버전과 알파가 같음). 하락 원인은 ① Quartz 의 히트음 예약 묶기가 빠진 것 ② boot.config 초기화.

## E. 시험 방법 개선
- 구간 시험: `seek`(에디터) 로 시작 지점 앞 타일부터 재생하는 방식과 전곡 재생 중 같은 구간이 같은 조건인지 **한 번 동등성 확인**(깨어 있는 장식 수, `fuse` 갱신·LateUpdate 시간) 필요.
- 모니터 상태(3440×1440@165 vs 2560×1440@120)를 매 판 기록. Steam 이 `boot.config` 를 되돌린 직후 첫 실행은 비교에서 제외.

## F. 릴리스(2.8.0) — 보류
- 문서 초안(패치노트·README·공지)은 있음. HitSoundBatch/쉐이더 프리셋/boot 안내/일반 버전 호환 반영, 정식 ZIP 재검증 필요. 사용자 "올려" 전에는 병합·태그·릴리스 안 함.
