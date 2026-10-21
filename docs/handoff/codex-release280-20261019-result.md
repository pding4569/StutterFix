# StutterFix 2.8.0 릴리스 문서 검토 메모

작성일: 2026-10-19. 지시서: `docs/handoff/codex-release280-20261019.md`.

## 작성 결과와 작업 범위

- `docs/patch-notes-2.8.0.md`: 사용자용 상세 변경점, 실제 기본값, 설정 창에 보이는 새 실험 옵션, 알려진 주의 사항.
- `docs/announcement-2.8.0.md`: 제목 포함 9줄 디스코드 공지. 자동 업데이트·로그 내보내기·v2.8.0 릴리스 링크 포함.
- `README.md`: 목차와 최신 변경 절을 2.8.0으로 교체. 핵심 측정과 상세 패치노트 링크 포함.
- `CHANGELOG.md`: README의 2.7.3 절을 내용 그대로 맨 위의 이전 버전으로 이동.
- 본 메모를 포함한 5개 문서는 모두 `apply_patch`로 작성하고 UTF-8 BOM을 넣었다. 기존 README·CHANGELOG 본문은 해당 절과 첫 BOM 외에 바꾸지 않았다.
- 게임 실행·Mods 설치·빌드·커밋·태그·푸시·릴리스 생성은 수행하지 않았다. 기존 게임 로그를 새 측정으로 읽거나 성능 시험을 돌리지 않았다.
- `Info.json`·`repository.json`을 수정하지 않았다. 시작 때 이미 수정되어 있었으며, 검토 중 HEAD가 외부 작업으로 `50d73d6`에서 `656e09d`(버전 메타데이터 커밋)로 이동한 것을 확인했다. 두 파일의 작업 시작/검사 시 SHA-256은 각각 동일했다. 이 커밋을 이 작업의 결과로 세지 않는다.
- 기존 `.codex/config.toml` 변경과 미추적 파일들은 보존했다. 검토 중 새로 보인 `tools/ReleaseCheck/out/full_game_280.py`도 이 작업에서 만들거나 실행한 파일이 아니다.

## 근거와 기본값 확인

변경 범위는 `git log v2.7.3..HEAD`로 확인했다. 수치는 `docs/optimization-36.md`의 아래 절에서만 가져왔고, 커밋 메시지는 변경 식별에 썼다. 초기 감사 표보다 뒤의 후속 측정과 현재 코드가 우선이다. 공개 문서의 맵 이름은 Arche·HELLO로 제한했다.

| 공개 문서 항목 | 근거 파일·커밋 | 코드상 기본값·적용 조건 |
|---|---|---|
| 메인·렌더 우선순위 | `optimization-36.md:690` 이후 후속 3건, `a384bd4` | `StutterFix.cs:1108`의 `ThreadSchedulingMode=1`, `ThreadRenderScheduling=true`. `StutterFix.cs:801`에서 두 모듈에 전달. `SettingsWindow.cs:1116`의 우선순위/오디오 방식 선택과 렌더 적용 토글 확인. |
| 장식 마무리 빠른 경로(TightLate) | `optimization-36.md:836` 절, `99e2c60`, `05af9bc` | `StutterFix.cs:62`의 `tightlate-off`가 없으면 켬. `TightLate.cs`의 형식·부모·히트박스·마스크·시차 조건을 통과할 때만 사용. |
| 색 직통 경로(ColorFast) | `optimization-36.md:890` 절, `258a187`, `96aab3d` | `ColorFast.cs:23`의 Configure에서 `colorfast-off`가 없으면 켬. 계속 보이는 정확한 이미지 장식 형식 및 호환 조건 확인. 색 쓰기 자체는 생략하지 않음. |
| 같은 각도 회전 재사용 | `optimization-36.md:904` 절, `a24b108` | `StutterFix.cs:63`의 `eulermemo-off`가 없으면 켬. 게임 업데이트 뒤 Quartz를 끈 A/B/A 조건을 공개 문서에도 명시. |
| 위치 X·Y 짝 합치기 | `optimization-36.md:763` 절, `6e41c56` | `DecoAnim.cs:84`의 `PairXY=true`, `StutterFix.cs:101`의 `pairxy-off`로 끔. `PairAllowed()`가 다른 모드의 위치 함수 패치를 검사. 실제 결합 경로는 `!Prof` 조건도 있음. |
| 보이는 장식 회전·크기 위임 | `optimization-36.md:751` 절, `f423971`, `5be0f89` | `MoveApply.cs:243`의 `DeferRotScale=true`, `StutterFix.cs:102`의 `rotscale-off`로 끔. 정확한 이미지 장식 형식·마스크 제외·`SkipForLate` 조건. |
| 투명 장식 이미지 교체 미루기 | `optimization-36.md:739` 절 및 `769` 정적 검토 반영, `8e574fb`, `5be0f89` | `LazySprite.cs:23`의 `Enabled=true`, `StutterFix.cs:103`의 `lazysprite-off`로 끔. 투명·히트박스/마스크 없음·처음 크기 0 제외 등 조건. |
| 화면 밖 타일 색 바꾸기 | `optimization-36.md:709` 절 및 `769` 정적 검토 반영, `4c4ca61`, `5be0f89` | `StutterFix.cs:1121`의 `RecolorDefer=true`를 `856`에서 적용. `SettingsWindow.cs:1061`에 사용자 토글. TileCull 관리 조건에서만 미룸. |
| 불투명도 전용 타일 이동 | `optimization-36.md:718` 및 `724` 절, `9bed600`, `5f30c66` | `FloorAnim.cs:205`의 `FastOpa=true`, `StutterFix.cs:107`의 `fastopa-off`로 끔. `FloorAnim=true`가 기본이며 위치·회전·크기를 쓰지 않는 효과만 빠른 경로. |
| 화면 밖 발광·불필요한 타일 위치 읽기 | `optimization-36.md:724` 절, `8b3a4a2`, `3ead380` | `TileCull.cs:50`의 `CullGlow=true`, `FloorLateFix.cs:17`의 `Enabled=true`. `StutterFix.cs:104`의 `floorlate-off` 및 `105`의 `tc-no-glow`로 끔. 연결선 없는 경우만 위치 읽기 생략. |
| 맵 문자열 재사용 | `optimization-36.md:65`, `682` 이후 마무리, `83facdf` | `StutterFix.cs:1115`의 `JsonStringPool=true`, `795`에서 `FastJsonParser.PoolStrings`에 전달. `SettingsWindow.cs:1082` 토글. |
| 로딩 업로드 예산 | `optimization-36.md:67`, `682` 이후 마무리, `83facdf` | `StutterFix.cs:1114`의 `AsyncUploadBudget=true`, `794`의 Configure, `UploadBudget.cs`의 적용·복원. `SettingsWindow.cs:1087` 토글. |
| 정수 태그 색인 | `optimization-36.md:682` 이후 마무리, `83facdf` | `StutterFix.cs:1103`의 `TagIndex=true`, `799`의 Configure, `TagIndex.cs`의 표 변경 시 폴백. `SettingsWindow.cs:1103` 토글. 재생 준비 최적화이며 LevelEvent 디코딩 제거로 설명하지 않음. |
| 디스크 캐시 | `optimization-36.md:693`, `2837ab1` | `StutterFix.cs:1116`의 `AssetDiskCache=true`, `AssetDiskCacheMB=3072`, 빈 경로는 기본 사용자 캐시. `796`의 Configure, `AssetDiskCache.cs`의 공간 보호. `SettingsWindow.cs:1084` 토글. |
| 재질 속성 번호·그래픽 미리 준비 보강 | `optimization-36.md:75`, `94`의 14·33번 및 후속, `91571e7`, `79242f4` | `MaterialPropertyIds.cs`의 런타임 ID 사용은 별도 토글 없음. `ShaderWarm.cs`의 미해결 필터 재시도, `StutterFix.cs:1126`의 `ShaderWarm=true`. 이 변경만의 FPS 수치는 쓰지 않음. |
| 게임 업데이트 입력 대응 | `optimization-36.md:904` 절, `55c680e`, `a24b108` | `AllocFix.cs:85`의 `InstallInput`: `GetSpecialInput` 반환형이 기대한 목록과 다르거나 메서드를 찾지 못하면 로그 후 해당 후킹 중단. API가 달라도 입력을 강제로 대체한다고 쓰지 않음. |

수치 해석: TightLate의 약 8%는 2560×1440·120Hz, 모니터 꺼짐 상태의 Arche 270초 구간이다. 측정 창은 257.7~278.8초, 같은 DLL·새 프로세스 ABBA이다. ColorFast는 별도 252~273초 ABBA이고, 회전 재사용은 업데이트 뒤 Quartz 끔 A/B/A이다. 서로 다른 측정의 개선율을 합산하지 않았다. LazySprite의 15→6.1ms는 효과 처리 시간이다. RecolorDefer는 HELLO PlayerAuto ABBA의 정확한 102·102→66·70을 사용했다.

## 기본 꺼짐이며 설정 창에 보이는 새 선택

다음은 `StutterFix.cs`의 Settings 선언과 `SettingsWindow.cs`의 실제 노출을 함께 확인했다. 내부 파일 플래그만 있는 연구 코드는 여기에 포함하지 않았다.

| 설정 필드·실제 기본값 | 설정 창 위치 | 공개 문서 설명 |
|---|---|---|
| `GcDynamicBudget=false` | `SettingsWindow.cs:1079` | 메모리 한도 자동 조절 |
| `MoveEpsilon=false` | `1093` | 미세 위치 쓰기 생략 |
| `NativeMotion=false` | `1096` | 장식 숫자 계산 묶기 |
| `NativeMotionArena=false` | `1099` | 계산 버퍼를 비관리 메모리에 |
| `NativeMotionPinArena=false` | `1101` | 계산 버퍼를 RAM에 고정, 상위 두 선택 필요 |
| `TagIndexNativePlans=false` | `1106` | 태그 번호만 비관리 메모리에 |
| `FrameQueueDelay=false` | `1107` | 입력 프레임 대기 보정, 기존 비동기 입력에는 추가 적용 없음 |
| `ParticleCatchup=false` | `1109` | 입자 복귀 따라잡기, 화면 밖 입자 멈추기와 함께 사용 |
| `ThreadPerformanceCores=false` | `1120` | 고성능 코어만 사용 |
| `ThreadAvoidLogicalZero=false` | `1121` | 0번 논리 코어 제외 |
| `PhysicsPause=false` | `1122` | 곡 중 물리 시뮬레이션 중지, 다른 모드 영향 주의 |
| `MapProfilesEnabled=false` | `1128` | 맵별 화면 설정 저장 |
| `HitchHeatmap=false` | `1132` | 끊긴 타일 상위 5개 기록 |
| `TimerHalfMs=false` | `1820` | 타이머 0.5ms 요청 |
| `FrameGenConstantRing=false` | `1713` | 보간 업로드 버퍼 재사용, 프레임 늘리기를 켰을 때 노출 |

기본값 선언은 일반 실험 항목이 `StutterFix.cs:1098` 부근, 물리·타이머는 `1154`·`1155`, 보간 버퍼는 `1174`에 있다. `게임 스레드 예약`의 `오디오 방식`은 켬/끔 토글과 구분해서 안내했다. 기본 선택은 1(우선순위)이며 오디오 방식 선택의 성능 채택 근거는 없다.

## 문서·코드 사이의 차이와 제외 판단

- `optimization-36.md:58`의 초기 문장과 `682`의 마무리는 모든 새 기능 기본 끔 또는 디스크 캐시 끔으로 읽힐 수 있다. 이후 `690` 후속에서 우선순위·캐시가 기본 켬이 되었고, 현재 Settings 선언도 켬이다. 문자열 풀·업로드 예산·태그 색인도 현재 기본 켬으로 적었다.
- `SettingsWindow.cs:1077`의 로딩·메모리 실험 부제는 여전히 전부 기본 꺼짐이라고 한다. 그 아래 문자열 풀·디스크 캐시·업로드 예산은 켬이다. `(실험)` 이름과 실제 기본값을 구분해 공개 문서에 알렸고 UI 코드는 이번 범위에서 수정하지 않았다.
- `SettingsWindow.cs:2733` 이후의 전체 초기화 코드는 일부 신규 기본 켬 실험 설정을 끈다. 새 Settings 생성 기본값과 동일한 동작으로 보장하지 않았다. 공개 문서에는 기존 저장값을 유지한다는 점을 안내했다.
- `TightLate.SkipSame` 선언이 true여도 `StutterFix.cs:64`에서 `tightskip-on`이 있어야 켜진다. 실제 기본 꺼짐이며 사용자 토글이 없고 이득도 작아 공개 변경점에서 제외했다. EulerMemo도 초기 실험의 `eulermemo-on` 기록보다 현재 `eulermemo-off` 로직을 우선했다.
- GroupStep·PosFast·DecoBatch·DecoEngine·DecoGpu·FuseLate·재질 공유·아틀라스·투명 여백 크롭은 성능 이득이 없거나 동일성 반례가 있거나 개발/측정 전용이다. 기본 성능 개선으로 소개하지 않았다. profsamp·fuse·decosamp·decogpu 등 측정 명령도 공개 문서에서 뺐다.
- 상수 버퍼 링은 설정 창에 있으므로 실험 옵션 한 줄로만 포함했다. 기능 검증 통과를 FPS·지연 개선으로 표현하지 않았다. 숫자 계산 묶기·RAM 고정·코어 제한·입력 대기 보정도 같은 기준을 적용했다.
- 물리 중지의 HELLO 226.5 대 226.5 FPS, 0.5ms 타이머의 이미 짧은 환경 측정은 이득을 뒷받침하지 않는다. 기본 꺼짐 옵션의 기능 설명과 제한만 썼다.
- 디스크 캐시의 다른 맵 측정은 공개 문서의 이름 범위를 좁히기 위해 생략했다. HELLO 재방문 12.9→11.6초와 시험한 맵들의 첫 열기 추가 비용 기록만 사용했다. 문자열 풀의 독립 할당 시험 수치를 실제 맵 로딩 단축으로 바꾸지 않았다.
- 맵별 프로필·히트맵은 선택 기능만 안내했다. 입력 보정의 물리 키 판정 개선, 전 해상도 품질, 모든 모드 조합 호환을 확정하지 않았다.
- Quartz 입력 예외와 모드 reload 뒤 맵 열기 종료는 `optimization-36.md:904` 및 `788` 기록 범위에서만 주의에 넣었다. 이번 변경이 원인이라고 단정하지 않았고, 현재 새 실행에서 재현 여부도 검사하지 않았다.
- 일반 Player의 순수 진단 분리·업데이트 트랜잭션·자원 소유권 공통화는 내부 정리다. 이번 문서에서는 사용자 체감 속도 수치나 완전한 복구 보장으로 별도 홍보하지 않았다.

## 확인 결과와 확인하지 않은 것

- 5개 문서 모두 첫 3바이트 `EF BB BF`, 엄격한 UTF-8 디코딩 성공, BOM 정확히 하나, 대체 문자 U+FFFD 없음, 한글 존재 검사를 통과했다. 한국어 본문·옵션명·공지 전문을 다시 읽어 깨지지 않은 것을 확인했다. 기존 2.7.3의 문자 깨짐 예시 `???`는 CHANGELOG로 그대로 옮긴 인용이므로 새 깨짐으로 세지 않는다.
- README의 최신 절·목차, CHANGELOG의 2.7.3 절 원문 보존, 기존 README 기능 본문과 CHANGELOG 2.7.2 이하의 보존을 정적으로 대조해 통과했다. 공지는 9줄이며 릴리스 URL·자동 업데이트·로그 내보내기 안내를 확인했다. 상세 패치노트 경로도 실제 작성 파일과 맞는다.
- 추적 문서의 `git diff --check`는 오류 없이 통과했다. 수치·기본값은 위 근거와 대조했다. 개인 시험 맵 이름·사용자 이메일·게임 DLL·디컴파일 본문·다른 모드 코드의 신규 포함은 없다.
- 게임 실행·Mods 설치·자동 업데이트 배포·실제 설정 창 렌더링은 수행하지 않았다. 문서만 바꿨으므로 패키지 빌드도 하지 않았다. 이번 문서의 작성은 릴리스 실행·공개 승인을 뜻하지 않는다.
- 특히 최신 게임 업데이트 뒤 전체 곡·전체 모드 조합을 새로 통과한 결과가 아니다. 이전에 통과한 구간 검증을 최신 전체 환경의 보장으로 확대하지 않았다.

## 사용자 또는 릴리스 담당자가 결정할 것

- 현재 초안은 순간 끊김 감소와 장식 처리 개선을 우선 소개한다. 디스크 캐시를 같은 수준으로 강조할지는 선택 사항이다. 기본값 자체는 현재 코드대로 작성했으므로 이번 문서에서 변경 결정이 필요하지 않다.
- 설정 창의 기본 꺼짐 부제와 전체 초기화 값의 불일치는 코드/UI 작업에서 따로 맞출지 결정해야 한다. 본 작업은 문서만 수정했다.
- 릴리스 실행, 최종 게임 업데이트 이후의 실제 실행 확인, 버전·태그·푸시는 담당자의 별도 작업이다. 이 작업에서 실행하거나 자동으로 이어서 진행하지 않는다.

## 수정 이력

### 2026-10-19 작업 W — 최신 기본값·실험 탭·내 프리셋 반영

지시서: `codex-release280b-20261019.md`. 위쪽은 초안 작성 당시의 검토 기록이다. 특히 세 기능의 기본 켬, 실험 이름과 기본값의 불일치, 보간 버퍼의 옛 위치에 관한 설명은 이번 수정 이력과 현재 코드로 대체한다.

무엇을 고쳤는지:

- 상세 패치노트·공지·README 2.8.0 절에서 `JsonStringPool`·`AsyncUploadBudget`·`TagIndex`의 새 설정 기본값을 꺼짐으로 정정했다. 기존 저장값은 유지하며 세 기능은 2.7.3 정식에 없었다는 cleanup 결과를 반영했다. `DecoFastCompact=false`도 사용자 이름인 `장식 목록 정리 생략 (실험)`으로 안내했다.
- `StutterFix.cs:1109` 이후 선언과 `799` 이후 적용 연결, `SettingsWindow.cs:1198` 이후 실험 분기 및 `1293`의 탭 목록을 직접 확인했다. 플레이 다섯 번째 실험 탭의 제어 21개를 실제 UI 문구로 표에 맞췄다. 중립 필터·오디오 방식·장식 목록 정리 생략도 포함했고, 보간 버퍼를 예전 그래픽 페이지의 조건부 항목으로 설명하던 문장을 고쳤다.
- `AssetDiskCache=true`, 상한 3072MB, `ThreadSchedulingMode=1`, `ThreadRenderScheduling=true`는 유지됨을 확인했다. `tightlate-off`·`eulermemo-off`·`colorfast-off`가 없으면 켜지는 현재 코드도 확인했다. 디스크 캐시와 기본 성능 개선을 실험 기본 꺼짐 목록과 구분했다.
- `codex-fxslots-20261019-result.md`, `SettingsWindow.cs:1601` 이후·`1683` 이후 및 `FxSlots.cs`의 저장 한도·공통 로직을 대조했다. 쉐이더 > 켜짐 > 내 프리셋의 최대 12개 저장·적용·덮어쓰기·삭제, 모두 끄기 뒤 다시 켜기를 사용자 말로 추가했다. 상세 문서와 README에는 LUT 파일·경로 제외도 적었다.
- 6개 연구 활성 파일 읽기가 `DEV || AUTOTEST` 안에 있는지 해당 소스에서 확인했다. 공개 패치노트에는 개발자용 한 줄만 남겼다. 일반 Player가 해당 파일을 읽지 않는다는 cleanup 결과와 일치한다.
- `tools/ReleaseCheck/out/full-game-2.7.3.json`·`full-game-2.8.0.json`의 HELLO 전곡 평균 195·192 FPS, 양쪽 끊김 0, 3440×1440·165Hz, `performance_ab=false`를 확인했다. 시점이 다른 실행 확인이며 성능 A/B가 아니라는 제한과 함께 반영했다. 2.8.0 기록의 곡 시작 연출 뒤 최장 프레임은 33ms이므로 끊김 0을 30ms 초과 프레임 0으로 바꾸지 않았다.
- 공지의 약 +1~8%는 서로 다른 기능별 Arche 구간 시험의 요약이다. `optimization-36.md:892`의 색 경로 약 +1.6%, `906`의 회전 재사용 창 프레임 수, 기존 마무리 경로 약 +8%를 참고했다. 252~273초와 257.7~278.8초를 구분하고, 약 8%의 2560×1440·120Hz 조건과 합산 불가를 공지에도 명시했다. 전곡·버전 전체 개선율로 표현하지 않았다.
- 게임 업데이트 뒤 Quartz 입력 패치 예외와 일시 제외 후 정상 진행은 `optimization-36.md:905` 및 인계 지시의 확인 범위로 한 문장에 정리했다. 다른 모드의 입력 패치 문제를 StutterFix 문제로 표현하지 않았다.
- 공지는 제목 포함 9줄을 유지하며 자동 업데이트·완전 재시작·로그 내보내기·릴리스 링크를 보존했다. CHANGELOG는 이전 버전 기록이므로 이번에는 수정하지 않았다.

근거 확인 못 한 것:

- 새 실험 탭·프리셋의 실제 게임 화면, 한글 IME, UMM 저장 후 재시작 유지, 다른 모드 단축키와의 상호작용은 이번에 실행 확인하지 않았다. fxslots 결과의 오프라인 검증을 실제 게임 검증으로 확대하지 않았다.
- 세 로딩 실험과 Compact 생략 각각의 실게임 이득, 전체 곡·다른 PC·모든 해상도의 성능 개선은 새로 입증하지 않았다. 저장값 유지와 2.7.3에 세 필드가 없었다는 사실은 cleanup 결과와 현재 적용 코드에 근거하며 과거 XML 시험을 재실행하지 않았다.
- Quartz의 모든 버전·다른 모드 전체의 호환성은 확인하지 않았다. HELLO 두 정식 ZIP 기록은 같은 시점에 반복한 A/B가 아니므로 195→192를 성능 회귀율로 확정하지 않는다.

작업 범위와 문서 검증:

- 문서 4개만 `apply_patch`로 수정했다. 소스 코드·Info.json·repository.json·CHANGELOG는 수정하지 않았으며 게임 실행·빌드·Mods 설치·커밋·태그·푸시는 하지 않았다.
- 수정한 문서의 UTF-8 BOM(`EF BB BF`)·엄격 UTF-8 디코딩·BOM 하나·대체 문자 없음과 한글 제목·옵션 문구를 검사하고 본문을 다시 읽었다. 공지 9줄, 실험 옵션 21개와 UI 이름 일치, README의 2.8.0 절 밖 보존도 확인했다.
