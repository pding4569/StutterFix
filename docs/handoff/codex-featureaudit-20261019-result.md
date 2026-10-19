# 기능·토글 감사 결과 인계

작성일: 2026-10-19  
결과: [feature-audit-2.8.0.md](../feature-audit-2.8.0.md)  
요청: [codex-featureaudit-20261019.md](codex-featureaudit-20261019.md)  
최종 검토 소스 내용 기준: 642c28c(d362352의 프리셋 기능과 후속 입력칸·여백 커밋 포함).

## 완료 범위

- Settings 선언·로드·이행·Resilience·PcTune·ApplyConfig·설치 순서·상위 기능 연결을 추적했다. 별도 Settings.cs는 없으며 StutterFix.cs 내부 Settings가 대상이다.
- 최신 Settings 필드 **235개 전부**를 표로 작성했다. bool뿐 아니라 수치·문자열·단축키·표시/복구 상태·새 List<FxSlot>도 포함했다.
- StutterFix.cs의 모든 has(...) 플래그와 직접 File.Exists 플래그, 모듈 Configure의 파일 스위치를 조사했다. 일반 Player 공통 파일과 Dev/PlayerAuto 전용 파일을 구분했다.
- 현재 설정창의 페이지·탭·FxExtras 동적 연결, 기존 저사양/실험 탭을 확인했다.
- DEV/AUTOTEST/Edition.Dev/MeasureBuild/AutoTestBuild 분기와 sfmeasure/server.py·open_ab.py·AutoTest.cs를 읽고, 결과/성능에 영향을 주는 차이를 **18개 분류**로 정리했다.
- 이득을 시험했지만 배포 기본은 끔이거나 시험에서 빠른 경로가 차단되는 후보를 별도 표로 작성했다.
- 각 Settings 행에 실제 기본 상태·제어 위치·UI·설명·근거·위험·권고를 붙였다. 성능 이득/비트 동일/정책 검사/단순 무오류는 분리했다.
- 맵 이름·개인 경로/정보·게임 DLL 내용은 결과에 싣지 않았다. 기존 측정 수치는 맵 이름을 생략하고 인용했다.

## 중요한 판단

1. **“시험했는데 제품 기본 끔”에는 의도적인 미채택이 많다.** GroupStep·상주 DecoAnim Batch·DecoEngine은 느려졌고, PosFast·TightLate.SkipSame는 이득이 작았다. 기본 켬으로 바꾸자는 결론이 아니다.
2. TightLate.SkipSame는 선언 true와 달리 Load에서 tightskip-on 유무로 덮어써 **실제 기본 끔**이다.
3. Dev의 Prof=true는 PairXY/GroupStep을 차단한다. Dev 검증 판을 Player의 성능 판으로 볼 수 없다. Shadow·원래 경로 재실행·Reset 교대·Dormancy 검사 주기·이미지 대조 실패 폴백도 다르다.
4. JSON 문자열 풀·업로드 예산·태그 색인·디스크 캐시는 현재 기본 켬이다. 설정창의 공통 “기본으로 꺼져 있습니다” 설명과 맞지 않는다. 디스크 캐시는 이후 개선된 재방문 근거가 있고, 나머지의 독립 끊김 개선 근거는 약하다.
5. 일반 Player에서 무시하는 Dev+Auto 전용 off 파일로 정식 배포 A/B를 했다고 읽으면 잘못된 결론을 낼 수 있다.
6. sf_run은 자동 플레이를 자체 강제하지 않는다. auto on 명령을 넣은 경우에만 RDC.auto를 바꾸고 재생 때 맞춘다. 자동 플레이의 UI·입력·실패 경로는 직접 플레이와 다르다.
7. sf_run의 XML 복구와 sf_live의 setOrig 복구는 범위가 다르다. on/off 파일·boot·캐시·VRAM학습·직접 엔진 FPS 설정까지 전부 복구하는 장치는 아니다. open_ab.py는 DLL을 직접 교체하고 원본 복구 finally가 없다.
8. 현재 안전 모드 임계치는 연속 비정상 종료 **2회**다. 과거 7회 기록을 현재 동작으로 옮기지 않았다.
9. 새 FxSlots/FxLastOn은 기본 빈 상태이며 자동으로 효과를 켜지 않는다. 저장/적용/모두 끄기/복원 UI와 입력 보호를 추가로 조사했다. 자동시험 fxslot 명령은 Config.Save를 호출하므로 임시 set 복구와 구분했다.

## 권고 요약 — 실제 변경 없음

- 기본 유지 제안: 기존 핵심 최적화, TightLate·EulerMemo·ColorFast, 메인+렌더 우선순위 조합.
- 기본 정책 재검토: JsonStringPool·AsyncUploadBudget·TagIndex·FrameStats·FastLane Compact. 표에서 독립 이득 부족을 명시했다.
- 기존 유지하되 근거 범위 재검토: SkipIdleParticles·HitTextFade·ShaderWarm·LowNoFft, 일부 시스템/컬링/캐시 기능.
- 기본 끔 실험으로 유지: NativeMotion 계열·FrameGen/ConstantRing·입력 보정·자동해상도/재투영·필터 인증·프로필/히트맵 등.
- 제품 제거 제안: 손실/미미한 이득 후보와 미사용 호환 필드·대기열 후보. 과거 증거·독립 검증 도구까지 지우자는 뜻은 아니다.
- 실험 페이지 통합 제안은 구조 변경 범위만 적었다. 기존 subSel 길이·page clamp·DockOrder·아이콘·제목도 함께 고려해야 한다.

## 작성·검증

두 결과 문서는 apply_patch로 작성했으며 첫 문자를 U+FEFF로 넣었다. 별도 인코딩 재저장 명령은 사용하지 않았다.

주 감사 문서 검증 결과:

| 검사 | 결과 |
|---|---:|
| UTF-8 BOM 첫3바이트 | EF BB BF |
| 엄격한 UTF-8 디코딩 | 통과 |
| Settings 소스 필드 / 표 행 | 235 / 235 |
| 누락 / 초과 / 중복 행 | 0 / 0 / 0 |
| 행 번호 1~235 연속 | 통과 |
| 차이 표 D01~D18 | 18행 |
| StutterFix.cs has(...) 플래그의 문서 누락 | 0 |
| 로컬 문서/소스 링크 대상 누락 | 0 |
| U+FFFD 대체 문자 / 연속 물음표 손상 패턴 | 0 / 0 |

한글은 엄격 디코딩 검사 외에 Get-Content -Encoding UTF8 출력으로 제목·요약·표 일부를 직접 읽었다. 결과 메모도 같은 검사를 마지막에 수행한다.

## 동시 작업과 변경 금지 준수

- 이 작업이 쓴 파일은 요청한 결과 Markdown **2개뿐**이다. 소스(.cs/.csproj) 편집·빌드·게임 실행·Mods 설치·커밋 명령을 실행하지 않았다.
- 조사 초기 C#·csproj **336개**의 SHA-256을 보관했다. 도중 AutoTest.cs·Hotkey.cs·SettingsWindow.cs·StutterFix.cs·StutterFix.csproj **5개**가 바뀌고 FxSlots.cs **1개**가 추가된 것을 감지했다.
- git show로 외부 커밋 d362352의 해당 변경을 확인했다. 초기 5개 파일의 해시는 그 이전 6faf690 소스 내용과 **5/5 일치**했다(체크아웃 줄바꿈 차이 고려). 이 변경을 되돌리거나 커밋하지 않았다.
- 신규 필드 FxSlots/FxLastOn을 반영해 233개에서 **235개**로 감사 표를 갱신했다. 소스 기준을 다시 잡은 뒤 **337개** C#·csproj의 첫 해시 재검사에서 변경 **0개**였다. 이후 검사에서는 SettingsWindow.cs **1개**의 추가 동시 변경을 감지했다. 0e977f1·642c28c의 입력칸 스타일·32자 UI 제한·여백 변경임을 git diff로 읽었고, 설정 필드/기본값 추가 변화가 없어 감사 결론은 유지했다.
- 다른 작업이 계속 커밋할 수 있으므로 Git HEAD가 고정됐다고 주장하지 않는다. 감사 기준은 위의 소스 내용이며, 실제 배포/설치된 바이너리 일치는 미확인이다.
- 기존 .codex/config.toml·README·CHANGELOG 및 기존 미추적 파일은 그대로 보존했다.
- 기본 실행 샌드박스가 환경 오류로 시작되지 않아 읽기 전용 PowerShell 검사를 실행 권한 대체 경로로 수행했다. 소스 변경이나 실행 범위를 확대한 것은 아니다.
- 사용자가 지정한 두 결과 문서 범위에 맞춰 별도 스킬 관찰 파일·메모리 업데이트는 작성하지 않았다.

## 하지 않은 검증 / 다음 작업

- 새 A/B·게임 완주·수동 입력·픽셀 캡처·DLL/ZIP 검사를 수행하지 않았다. 문서의 수치는 기존 기록을 인용한 것이다.
- 권고는 정책 제안이며 자동 채택하지 않았다. 사용자가 켤 것/실험으로 보낼 것/지울 것을 고르면 이후 별도 작업으로 구현해야 한다.
- 최우선 후속 검증은 같은 PlayerAuto/정식 설정·파일·화면·맵 진입 경로에서 런타임 실제 사용 상태를 맞춘 대조다. 이 조사에서는 실행하지 않았다.

