# Codex 조사 결과 인계 — 엔진 호출, 2026-10-20

**결론: 필터 ID 치환을 먼저 단독 측정하고, 동일 값 생략은 전용 단일 패스 필터 몇 종으로 제한해 검증한다. HELLO 후반 5% 이득은 미확인이다. 추가 tick/hold 음원 묶기는 HitSoundBatch 밖의 새 후보다.**

본 보고서: [docs/engine-calls-20261020.md](../engine-calls-20261020.md).

## 완료한 조사

- 제공된 세 프로파일의 관리 포함 시간을 비교하고, 원본 함수와 Harmony/우리 모드 비용을 분리했다.
- 게임 CSharp/firstpass의 Material.SetFloat/Color/Vector/Texture 직접 호출자를 기존 ILScan으로 전수 분류했다. API별 메서드-목적지 조합은 407 / 67 / 316 / 152개이며 동적 호출 횟수가 아니다.
- Unity Material string/int 오버로드를 확인했다. string은 PropertyToID 뒤 같은 Impl로 가므로 **SetFloatImpl 5.7%를 문자열 해시 제거 이득으로 계산할 수 없다.** SetVector도 SetColorImpl을 쓴다.
- HELLO 원본의 SetFilter 1,014개/31종, SetFilterAdvanced 830개/70종을 확인했다. 고급 70종 모두 전용 SCMaterial 생성·OnDisable 정리 패턴이 있다.
- 실제 h170 활성 개수는 미확인이다. 단순 원본 이벤트 재생 추정 최대 12개(전경 10+배경 2)는 RepeatEvents/실제 타임라인 등을 재현하지 않은 보조값이다.
- Glow_Glow의 패스 사이 _Amount 변경을 확인했다. “호출 위치마다 지난 값 기억”은 다음 프레임 시작 값을 복구하지 못할 수 있으므로 금지한다.
- FloorMeshRenderer.set_color는 이미 정수 ID다. scrFloor 알파는 isFading 조건, 아이콘 sprite는 기존 동일 값 검사가 있다. 일반 FloorFix 재도입 근거가 없다.
- Rewired 난독화 0.9%는 Rewired_Core.dll의 ReInput 하위 경로다. Update/DoUpdate/ReInput 수치는 서로 중첩된다.
- GUIStyle 0.8%의 호출 소유자는 미확인이다. PerfOverlay.OnGUI는 입력 전용이며 SettingsWindow/UMM/게임 UI를 구분해야 한다.
- extraTicksCountdown/holdSoundsData는 Conductor의 별도 개별 예약 루프다. 현재 HitSoundBatch는 hitSoundsData만 처리한다.

## 성능 수치의 경계

HELLO 후반 FloatImpl 5.7% + ColorImpl 1.7% = 7.4%는 6ms 프레임 가정에서 0.444ms 상당이다. 필터·타일·장식·vector가 섞인 예산이며, 여기에서 제거 가능한 몫은 미확인이다. ID 변환의 독립 시간과 엔진의 동일 값 처리도 미측정이다. 평균 FPS 향상이나 끊김 제거를 확정하지 않았다.

Arche의 큰 DOTweenComponent 래퍼 비용은 DecoAnim을 포함한다. 이를 원본 DOTween의 낭비로 계산하지 않는다. 장식 Transform 쓰기를 음원처럼 소수 호출로 합칠 수 있다는 새 근거는 없다.

## 작업 경계와 파일

소스(.cs/.csproj) 변경·빌드·게임 실행·Mods 설치·커밋은 하지 않았다. 다른 모드 코드를 이번 조사에서 열거나 인용하지 않았다. 게임 디컴파일/IL 자료는 `%TEMP%\sf-enginecalls-20261020`에만 저장했다. 보고서에는 메타데이터 이름·분석 요약만 썼다. TEMP는 지워질 수 있으므로 본 보고서에 재현 명령과 입력 해시를 남겼다.

ILScan 주의: 현재 FindCallers는 짧은 이름 완전 일치다. `Material::SetFloat`는 0곳이며 `SetFloat`로 검색한 뒤 목적지를 필터링해야 한다. 기존 실행 파일을 사용했으며 도구를 다시 빌드하지 않았다.

요청 결과 두 문서는 apply_patch로 UTF-8 BOM을 포함해 작성했다. 두 파일의 EF BB BF, 엄격한 UTF-8 디코딩, 한글 표제/핵심 문장 정상 표시, 대체문자 없음, 상대 링크 대상 존재, 소스 diff 없음을 확인했다. 사용자 기존 작업 트리 변경은 보존했다.

## Claude가 게임에서 확인할 최소 순서

이 절은 **후속 작업 절차**이며 이번 조사에서 실행하지 않았다. 구현·실행을 할 수 있는 후속 승인 범위에서 진행한다.

1. 현재 DLL 지문과 실행 상태(알파 버전·해상도·모니터/UMM/설정창·Quartz·다른 모드·HitSoundBatch 실제 상태)를 기록한다. 위 프로파일 당시 DLL 지문은 없으므로 동일 버전이라고 가정하지 않는다. 기존 DLL/설정 등 시험 대상은 백업하고 종료 때 복원한다.
2. HELLO h50/h170, Arche a262에서 활성 필터 인스턴스 수와 필터별 OnRenderImage 평가 수를 구간 계수한다. Float/Color/Vector/Texture string/int 호출, 타일/장식 기여를 분리한다. _TimeX와 일반 속성의 값 변화/동일 비율을 센다. 작은 허용 목록의 정수 카운터만 쓰고 구간 끝에 한 번 출력한다. **고빈도 Stopwatch/StackTrace/boxing/프레임별 로그/수백 패치 설치 금지.** 계수기 자체의 비용은 켬/끔으로 먼저 확인한다.
3. FloatImpl이 대부분 필터라는 가설이 확인되면 **상수 문자열→ID만** 치환한 A/B를 먼저 한다. 값·순서·횟수는 유지한다. 계수기를 끄고 매 판 새 프로세스 ABBA 최소 2묶음으로 같은 구간 프레임 시간·최악/p99/p99.9·끊김·GPU 대기를 비교한다.
4. 다음은 Color_Contrast/FX_Glitch 계열처럼 검토한 단일 패스의 변하지 않는 속성만 생략한다. “실제 재질의 마지막 쓰기”를 키로 관리하고 _TimeX는 유지한다. Glow 등 다중 패스, 텍스처, 외부 패치/외부 쓰기가 있는 재질은 최초 대상에서 제외한다. ID만 버전과 ID+생략 버전을 별도로 비교한다.
5. 필터 필드/재질 값/RT 조건/타일·장식 상태/전곡 끝 상태와 같은 시점 픽셀을 대조한다. 시간·seed를 맞추고, disable/enable·교체·seek·일시정지·재시도·씬 전환·해상도/속도 변경·모드 끔/reload/Unload·다른 모드 병용에서 캐시를 검증한다. decosig만으로 화면 동등성을 판정하지 않는다.
6. GUIStyle 소유자는 창/UMM 닫기와 모니터 0/1 비교로 먼저 가린다. 소유자가 확인된 UI의 크기 계산만 캐시한다. 입력이나 Object 생존 검사를 전역으로 줄이는 실험은 하지 않는다.
7. 필터 실험 이득이 작으면 extra tick/hold의 실제 예약 건수와 몰림을 별도 계수한다. 충분히 많을 때만 extra tick부터 PCM 묶기를 검토한다. hold는 종료·반복·pitch·취소 검증을 별도로 수행한다.
8. 최종은 전곡에서 가장 느린 프레임과 끊김 수를 확인한다. 패치/비교/캐시 비용이 이득을 덮거나 화면·음성 차이/복구 실패가 있으면 승격하지 않는다. 결과는 수치와 관측 한계를 함께 기록한다.
