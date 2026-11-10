# 인게임 전체 실행 경로와 그리기 순서 병목 분석

2026-11-10 작업 E. **새 병목의 존재를 확정하는 성능 시험은 하지 않았다.** 코드와 보관된 측정으로 조사 범위를 좁혔으며, 실제 제출 순서가 없는 곳은 추정으로 남겼다. 빌드·게임 실행·C# 수정·설치·커밋·새 RenderDoc 캡처는 하지 않았다.

## 1. 판정과 범위

**셰이더·재질·텍스처 전환이 많은 구조는 확인된다. 그러나 “순서를 바꾸면 큰 폭으로 빨라진다”는 인과와 무손실 재배치 가능성은 아직 입증되지 않았다.** 같은 sortingOrder라도 투명 이미지가 겹치면 순서가 그림이다. Unity는 더 앞선 정렬 키가 모두 같을 때 이미 셰이더/재질별로 정렬한다. 그 키를 무시한 전역 묶기는 최적화의 근거가 될 수 없다.

이번에 추가한 근거는 다음과 같다.

- 세 소스 폴더 **2,530개 C# 파일**을 전수 검색했다(게임 1,235, firstpass 1,294, DOTween 1). 프레임 관련 콜백 선언 1,128개, OnRenderImage 선언 378개를 인덱싱했다. 이것은 **파일/호출 지점 전수 검색**이며 모든 분기 의미를 실행 검증했다는 뜻은 아니다. DOTween 폴더에는 `DOTweenComponent.cs` 하나만 있어 TweenManager 본문 전수 분석을 했다고 하지 않는다.
- `%LOCALAPPDATA%/StutterFix`의 보관 census 8개 중 draw 열이 있는 7개에서 실제로 저장된 **근사 열의 전환 수**를 계산했다. 가장 최근 파일의 887행에서 셰이더 이름 전환 592, 재질 839, 주텍스처 632회다. GPU 실측 전환 수와 구별해야 한다(5절).
- 장식별 `UpdateShader`의 필터/RT 경로는 기본 `disableV15Features=true` 아래에서는 실행되지 않는다. 예전 `01-frame-flow.md`·`02-render.md`의 일반 설명을 활성 비용으로 읽으면 안 된다(3.7절).
- `n1110-*-rlP` 원자료는 인계 표와 일부 다르다. 아래에 파일의 실제 값을 별도로 기록했다. 목표 수치나 과거 다른 판으로 덮어쓰지 않았다.

### 근거 표기

`G/` = `E:/StutterFixWork/decompiled/Assembly-CSharp/`, `F/` = 그 옆 `Assembly-CSharp-firstpass/`, `D/` = 그 옆 `DOTween/`, `M/` = 현재 작업 트리, `R/` = `E:/StutterFixWork/night-results/`. `파일:줄`은 이번에 읽은 스냅샷의 줄이다.

현재 설치 `Assembly-CSharp.dll` SHA256은 `ec0d7c3889696c5605d6db4f443fd387f8668a90ba7b4dd2a1a2da64b5ada5bc`이며 디컴파일 `README.txt:2`의 기록 접두어와 일치한다. firstpass/DOTween의 원본 DLL과 디컴파일 전체의 동일성까지 확인한 것은 아니다. 파일별 소스 SHA와 정적 호출 인덱스는 다음 부록에 있다.

| 부록 | 내용 |
|---|---|
| [ingame-source-index.md](data/ingame-source-index.md) | 모든 파일의 SHA·콜백 줄·반복문/할당/Transform/재질/검색/렌더 호출 줄 수 |
| [ingame-render-state-index.md](data/ingame-render-state-index.md) | 모든 소스에서 재질 생성·셰이더/키워드·텍스처·MPB·정렬 쓰기 지점 |
| [ingame-filter-index.md](data/ingame-filter-index.md) | OnRenderImage 378개와 파일별 Blit/GetTemporary 지점 |
| [ingame-order-counts.md](data/ingame-order-counts.md) | census별 현재/완화 최솟값, 모델의 제한 |
| [ingame-runtime-evidence.md](data/ingame-runtime-evidence.md) | 재사용 런타임 원자료의 SHA와 파일:줄 발췌 |

재현: `python tools/analysis/ingame_audit.py all`. 파일 읽기와 Markdown 생성만 한다. 현재 측정과 함께 반복 실행할 벤치마크가 아니다.

기존 `wt-partial/docs/render-system/00~07`, 현재 트리의 같은 문서, `adofai-renderer.md`, `gpudeco-gap-analysis.md`, `docs/deco-spec/00-deco-spec.md`, `docs/engine-calls-20261020.md`, `CLAUDE.md`의 해결/기각 기록을 이어받았다. GpuDeco의 지원 범위 확대 설계는 기존 gap 문서의 주제이므로 여기서 새 발견으로 재포장하지 않는다.

## 2. 기존 측정이 말하는 한계

다음은 `R/n1110-{hl,wf,sh,s7,dd,ar}-rlP.txt:7`의 FPS와 `:8`의 phase_window다. 3440×1440 조건의 보관 자료이며 이번 새 측정은 아니다. 구간은 약 12초, 맵당 해당 파일 1판이다. 반복 ABBA 결과가 아니므로 회귀/향상 판정에 쓰지 않는다.

| 맵 | FPS | phase 합계 ms | FinishFrameRendering | Update | LateUpdate |
|---|---:|---:|---:|---:|---:|
| HELLO | 213.1 | 4.69 | 1.46 | 1.37 | 0.72 |
| Windflower | 193.8 | 5.16 | 2.06 | 1.21 | 0.81 |
| Exit | 322.7 | 3.09 | 0.87 | 1.00 | 0.45 |
| 7777 | 245.6 | 4.07 | 2.04 | 0.93 | 0.37 |
| DD | 251.0 | 3.98 | 1.54 | 0.90 | 0.61 |
| Arche | 97.9 | 10.21 | 2.03 | 4.51 | 2.07 |

인계 `codex-20261110e.md:24` 이하의 HELLO 217/Windflower 183/Arche 99 등은 **인계 제공값**으로 보존하되 위 판과 섞지 않는다. phase 로그에 `CPU_time=False GPU_time=False includes_waits=True`가 명시돼 있다. FinishFrameRendering은 메인 스레드 구간의 경과 시간이며, GPU 실행 시간이나 순수 렌더 CPU 시간으로 바꿔 읽을 수 없다. `n1110-s7-rlP.txt:7`의 GPU 0.00도 GPU 작업이 없다는 증거가 아니다.

장식 숨기기 비교는 다른 gp2 판의 정지 장면이다. HELLO 메인 카메라 1.4278→0.9767ms(`R/gp2-hl-rlP.txt:13`, `:39`), 7777 2.9062→0.9529ms(`R/gp2-s7-rlP.txt:13`, `:57`)처럼 장식 그리기 몫은 확인할 수 있다. 다만 숨기면 드로우·픽셀·상태 전환을 한꺼번에 지우므로 **그 차이가 전부 셰이더 전환 비용은 아니다**. 카메라 시간 안에 필터가 중첩되므로 둘을 합산하지 않는다(`R/gp2-s7-rlP.txt:43`).

렌더 스레드 표본에서도 상태 처리 경로는 보인다: `R/rts/ss-hl-gfx.sym40.txt:3`의 stream 대기 6.8%, `:6`의 GPU 프로그램 값 적용 1.0%, `:15`의 텍스처 설정 0.3%. 이 자료는 오래된 별도 판이며 리프 표본 %를 현재 프레임 ms나 절감량으로 환산하지 않는다. ntdll 리프만으로 GPU 대기 원인을 확정하지도 않는다.

## 3. 프레임 한 장의 실행 흐름

### 3.1 순서와 빈도

흐름은 입력 수집 → 곡 시계/입력 소비 → 게임 상태·행성·효과·트윈 → LateUpdate의 최종 위치/색 → 렌더러/캔버스 갱신 → 카메라별 cull/draw/필터 → 출력이다. 오디오 믹싱은 별도 오디오 스레드/DSP 시계로 진행하며 이 화살표 마지막에서만 실행되는 것이 아니다.

기존 추출한 MonoScript 실행 순서는 `M/docs/render-system/04-profile-and-candidates.md:54` 이하를 따른다. InputManager(-32000), EventSystem(-1000), conductor(-500), camera(-400), scnGame(-60), floor(-10), decoration manager(-5), 기본값 0(planet/vfx/DOTween 등), controller(400). **같은 값 사이 세부 순서는 보장되지 않는다.** FixedUpdate는 렌더 프레임마다 정확히 한 번이 아니라 누적 고정 시간에 따라 0회 이상이다. 새 실행 순서 추적을 수집한 것은 아니다.

| 단계/대표 진입점 | 빈도·반복량 | 할당·엔진 경계 | 기존 담당 / 남은 영역 |
|---|---|---|---|
| 입력, `scrController.UpdateInput` | async 활성 시 conductor에서 프레임당 호출. 입력 큐를 빌 때까지 소비 | DateTime/Ticks, concurrent queue→우선순위 큐, 입력 마스크 갱신 | 입력 생략 후보로 삼지 않음. 이벤트 수에 비례하는 처리 유지 |
| `scrConductor.Update` | 프레임당 1회, 5초 앞까지 새로 예약할 tick/hold/hit 수 | AudioSettings.dspTime·AudioSource 속성, hit 이름 문자열, 선택 FFT | SoundWarm·HitSoundBatch·LowNoFft. tick/hold 예약은 남음 |
| `scrController.Update`, `scrPlayer` | 컨트롤러 1회 + 플레이어/판정 이벤트 수 | 상태 기계, 입력/행성 이동, 게임 UI·사운드 | 게임 진행/판정은 유지. 모드/엔진 포함 래퍼 시간을 원본 계산으로 오인 금지 |
| `scrPlanet.Update` | 활성 행성마다 1회, hittable일 때 질의 | OverlapCircleAll 배열, Transform 크기/각도, ParticleSystem 모듈 쓰기 | HitboxQuery: 히트박스가 없는 조건만 질의 생략. 충돌 있는 장면은 남음 |
| `scrVfxPlus.Update`와 `ffx*` | due 이벤트 커서 전진, 한 프레임에 몰리면 해당 수만큼 | 이벤트별 타일/장식 태그 반복, DOTween 클로저·Sequence·Kill, 타입/컴포넌트 검색 | TagIndex·FastMove·DecoAnim·FloorAnim·ZeroTween·TypeCache·EffectBudget 등. 미지원 효과/콜백은 원본 |
| `DOTweenComponent` | Normal Update, Late, Fixed 각 활성 종류 | 활성 트윈 수/콜백 수, 재조직·완료·Kill | TweenFix와 직접 애니메이션 경로. 새 효과/잔여 트윈은 계속 실행 |
| `scrFloor.Update` | enabled 타일마다, 특수 색·애니메이션 아이콘 | 색 재질 쓰기, Volume Sequence, icon texture 갱신 | TileCull·RecolorDefer, FloorAnim·MeshWarm. Volume의 빈 Sequence 후보 별도 |
| `scrDecorationManager.Update` | 편집 중이 아니면 전체 장식 수 | 장식 히트박스 조기 반환 또는 Overlap/태그 Any | Dormancy/HitboxQuery 등과 구분. 장식 간 히트박스는 의미 보존 필요 |
| manager LateUpdate | 전체 장식 LogicUpdate + 변경 tiling/mask cache | GetVisible 뒤 피벗/시차/회전/크기 쓰기, 마스크 갱신 | Dormancy·TightLate·MoveApply·GpuDeco. 소유 밖 글자/입자/오브젝트/특수 장식은 남음 |
| `scrFloor.LateUpdate`, controller LateUpdate | 활성 타일 + 요구 메시 수 | opacity/glow 변화 반영, 위치/크기 읽기, FloorMesh.UpdateAllRequired | FloorLateFix·MeshWarm. 색/스타일의 실제 변화는 유지 |
| `scrCamera.Update/LateUpdate`, parallax | 카메라/시차 컴포넌트마다 | Transform·카메라 크기/색, 조건부 RT 재생성·수동 Render | 일반 화면 카메라 경로 남음. 크기 변경 RT 생성은 상시 할당 아님 |
| 행성 표시/홀드/입자 | 활성 컴포넌트 수 | PlanetSprite 재질 _Frame, HoldShader 값, 입자 시뮬레이션 속도 | HoldFix(HoldSkip) 이미 존재. 새 홀드 최적화로 다시 제안하지 않음 |
| Canvas/UI/HUD | 프레임 UI callback + dirty 요소 rebuild | Graphic 색·Shadow, TMP 메시/Canvas geometry, 레이아웃 | TextFix·HitMeterFade/DirectKill·UiDraw 등. 게임 HUD/외부 모드 소유 구분 필요 |
| AudioManager/엔진 오디오 | 프레임 source 정리 + 오디오 믹서 주기 | live source 반환, clip 해제/활성화, FMOD | SoundWarm·HitSoundBatch. Unity UpdateAudio 전체를 script 예약 시간으로 보지 않음 |

각 행의 직접 코드 근거와 주의점은 아래에 있다. 모든 파일의 반복문/할당/검색 지점은 전수 인덱스에서 확인할 수 있다. 인덱스의 `new`는 객체 생성뿐 아니라 값 형식 초기화도 포함하므로 GC 할당 횟수가 아니다.

### 3.2 입력·곡 시계·오디오

`G/scrConductor.cs:792`에서 dspTime 변화를 읽고 같으면 focused/비정지 조건에서 unscaled time으로 보간한다. async 입력이면 `:823`에서 controller.UpdateInput. `G/scrController.cs:2860`의 입력 갱신은 async keyboard가 없으면 반환하며 `:2882`, `:2888`의 큐를 순서대로 소비한다. 매 프레임 전체 키를 무조건 훑는 코드라고 단순화하면 안 된다.

`G/scrConductor.cs:833`, `:843`, `:860`은 각각 extraTicksCountdown/holdSoundsData/hitSoundsData 예약 커서다. 곡 전체 목록을 매 프레임 처음부터 순회하지 않고 새 5초 구간 항목만 처리한다. `:868` 주변의 `"snd" + hitSound`는 문자열 생성 가능 지점이다. `:921` FFT는 getSpectrum 분기 뒤에만 온다. LowNoFft를 이미 쓰는 현재 조건에 FFT 제거 이득을 다시 더하면 안 된다.

`M/HitSoundBatch.cs:15`는 **hitSoundsData만** 맡는다고 명시한다. extra tick/hold 묶기는 기존 `docs/engine-calls-20261020.md`의 열린 후보이지 이번에 새로 확인된 큰 비용은 아니다. `G/AudioManager.cs:96`은 liveSources 앞에서 재생 완료분을 반환하고 musicTracks를 순회한다. 오디오 예약 수, 활성 source 수, DSP 부하를 각각 구분해야 한다.

### 3.3 게임 상태·행성·충돌

`G/scrController.cs:1640` Update와 `G/scrPlayer.cs:410` Simulated_PlayerControl_Update가 판정/자동 진행을 담당한다. 후자는 midspin/자동 타격을 여러 번 처리할 수 있어 “프레임당 정수 계산 몇 번”으로 환산하지 않는다. `G/scrPlanet.cs:235` Update의 `:251` OverlapCircleAll은 hittable 분기이고, 별도 질의 `:738`도 있다. `M/StutterFix.cs:989`–`:990`이 HoldFix/HitboxQuery 설정을 연결한다. 이 호출 경로를 이미 해결 전의 비용으로 후보에 더하지 않는다.

### 3.4 효과 시작·DOTween

`G/scrVfxPlus.cs:181`은 paused/곡 미시작이면 반환한다. `:208` while은 currentVfxIndex로 due 이벤트만 전진하고 `:225` 부근 StartEffect를 호출한다. 이벤트 하나가 처리하는 대상 수가 큰 경우 그 내부 반복이 스파이크가 된다. 이벤트 총수와 매 프레임 실행수는 다르다.

DOTween의 스케줄 진입은 `D/DOTweenComponent.cs:61`, `:91`, `:99`이며 각각 Normal/Late/Fixed의 TweenManager.Update를 부른다. 실제 플레이어 설정과 Harmony 패치 때문에 DOTween 래퍼 안에 DecoAnim 비용이 들어갈 수 있다. 이전 프로파일의 래퍼 32.9%를 원본 DOTween 32.9%라고 해석하지 않는다(`M/docs/engine-calls-20261020.md:69` 이하). 기존 FloorAnim·DecoAnim의 증명을 반복 설명하는 대신 소유 밖 이벤트와 잔여 callback을 미확인 영역으로 둔다.

### 3.5 타일·홀드

`G/scrFloor.cs:1234` Update는 특수 색 분기에서 색을 실제로 바꾼다. Volume 분기는 `:1273` 근처에서 v를 캡처하고 **pulse=None 판정 전에 Sequence를 생성**한다. None일 때는 즉시 색을 쓰고 break하므로 빈 Sequence 생성/등록이 남을 가능성이 있다. 실제 Volume 타일 수·빈 Sequence 수·기존 패치 후 잔여 비용은 미측정이다. `ZeroTween`의 길이 0 처리와 이 생성 자체의 제거는 서로 다르다.

`G/scrFloor.cs:1316` LateUpdate는 opacity/glow/위치 변화 검사를 한다. `G/scrController.cs:2715` LateUpdate는 음량/음소거 UI와 `FloorMesh.UpdateAllRequired`를 실행한다. `M/FloorLateFix.cs:10`은 다중 행성 선이 없는 타일의 불필요한 Transform 읽기 네 지점을 이미 줄인다. 메시 첫 생성은 MeshWarm 담당이다.

`G/scrHoldRenderer.cs:133` Update→`:280` UpdateMesh→`:310` UpdateColor 경로는 재질 크기/위치/폭/완료도/알파를 쓴다. `M/HoldFix.cs:92` 이하가 입력 동일성 검사와 원본 비교를 이미 구현했다. `M/StutterFix.cs:1220`에는 HoldSkip 기본 true와 이전 검증 결과가 기록돼 있다. “일 없는 홀드 생략”을 아직 안 한 큰 후보로 올리는 옛 문서는 현재 상태가 아니다.

### 3.6 장식 관리자·Transform

`G/scrDecorationManager.cs:160`의 전체 CheckHitboxHit, `:173` LateUpdate의 tiling/mask 갱신과 `:188` 전체 LogicUpdate가 원본 경로다. `G/scrDecoration.cs:467`은 useHitbox/타겟 종류가 다르면 즉시 반환하고, 필요한 경우 CollectionPool의 리스트로 Overlap 후 태그 Any를 검사한다. 항상 새 충돌 리스트를 만든다고 쓰면 틀린다.

`G/scrDecoration.cs:959` LogicUpdate는 보이면 UpdatePosition(`:403`), customLevel이면 UpdateShader, useHitbox이면 상태 갱신이다. UpdatePosition은 followPlanet/stickToFloor를 읽고 pivot.localPosition, parallax.SetTrans, 회전·크기를 쓴다. 값이 같아도 native setter 경계를 지나는 원본 구조지만 Dormancy/TightLate/MoveApply/GpuDeco가 이미 상당 부분 맡는다. `M/TightLate.cs:171`은 disableV15Features 등 안전 조건을 확인한다. Arche의 큰 Update/LateUpdate를 “아직 전혀 손대지 않은 원인”이라 부를 수 없다.

### 3.7 실행되지 않는 장식 필터 경로를 비용에서 제외

`G/scrController.cs:332`의 `disableV15Features=true` 초기값 외에, 게임 소스 전수 검색에서 이 controller 필드의 쓰기를 찾지 못했다. `G/ADOFAI/LevelData.cs:840`/`:844`는 **별도의 LevelData 필드**다. 이 구분은 `M/docs/deco-spec/00-deco-spec.md:6` 이하와도 일치한다. 외부 모드가 reflection으로 필드를 바꾸는 경우까지 부정하는 것은 아니다.

`G/scrVisualDecoration.cs:88`의 `!disable && !isMask()` 아래 RT/필터 반복은 현재 기본 경로에서 빠진다. 따라서 `:135` 원본→temp Blit, `:143` 부근 reflection Invoke의 `new object[2]`, `:150`/`:156` 알파 복원, `:166` 마스크 입력 복사 등을 모든 장식의 매 프레임 비용으로 세지 않는다. enabled mesh 경로/비활성 경로의 material 조회와 실제 RT 실행을 구별한다.

외부 변경으로 이 경로가 살아 있는 경우에만, 활성 장식 필터 k개의 내부 Blit 합을 B라 할 때 사전 복사 k회 + 필터 B회 + 필터 결과 알파 복원 2회가 생긴다. 알파 마스크가 추가되면 또 사전 복사와 ApplyAlphaMaskToRT 내부 패스가 필요하다. 이 수식은 **해당 분기 실행 조건부**이며 현재 카메라 필터 체인의 수식과 섞지 않는다.

### 3.8 UI·추가 컴포넌트

`G/scnGame.cs:158`은 화면판 네 개의 ScaleXY를 매 프레임 쓴다. `G/scrHUDText.cs:23`은 표시 여부가 다를 때만 gui.enabled를 쓰지만 표시 중 gui.color/shadow.effectColor는 쓴다. `G/PlanetRenderer.cs:89`는 emoji/rainbow/expo 조건부, `G/PlanetSprite.cs:82`는 매 LateUpdate `_Frame` 재질 값을 갱신한다. `G/scrParticleDecoration.cs:116`은 shape.scale과 song.pitch를 반영한 simulationSpeed를 매 Update 쓴다.

UI/입자/오디오의 실제 활성 개수는 scene census를 써야 한다. 예전 HELLO 표본 `R/rts/rtcensus-hlbase.txt:154` 이하에는 Canvas 51·Graphic 262, 살아 있는 입자 시스템 21, 오디오 재생 source 26 등이 있다. 전체 prefab/로드 객체 수를 매 프레임 그리는 수라고 해석하지 않는다. 그 표본은 2560×1440이고 현재 n1110과 다르다. Quartz의 VideoBloom/ScreenTile/ScreenScroll 패치도 `:488` 이하에 기록돼 있으므로 원본 코드만으로 현재 호출을 확정하지 않는다.

## 4. 투명 그리기 순서와 재질 상태

### 4.1 정렬 규칙

일반 장식 깊이 ≥0은 sorting layer Bg와 GameObject layer 9, 깊이 <0은 Default와 layer 7, order=-depth다(`G/scrVisualDecoration.cs:327`). GameObject layer는 카메라 cullingMask이고 sorting layer와 별개다. syncFloorDepth면 부모 타일의 정렬값을 복사한다. 글자 `G/scrTextDecoration.cs:79`, 입자 `G/scrParticleDecoration.cs:74`, 오브젝트 `G/scrObjectDecoration.cs:139`도 각각 Canvas/ParticleSystemRenderer/내장 floor·planet 렌더러에 적용한다. Prefab 장식의 `SetDepth`는 빈 함수다(`G/scrPrefabDecoration.cs:16`); 내부 임의 렌더러에 같은 규칙이 적용된다고 추정하지 않는다.

타일 본체의 `SetSortingOrder`는 outline/icon/glow의 상대 순서도 바꾼다(`G/scrFloor.cs:1760`, `:1771`, `:1788`). 그래서 타일 본체만 재정렬해서는 안 된다. 행성은 `G/PlanetRenderer.cs:346` SetLayer와 appearance renderers를 함께 본다.

기존 엔진 비교기 분석 `M/docs/render-system/06-engine-internals.md:5`와 현재 `M/SceneRenderOrder.cs:20`–`:43`을 합치면 해당 투명 경로 모델은 다음과 같다.

1. sorting layer의 실제 값 + sortingOrder.
2. renderQueue.
3. 투명 거리 키(뒤→앞). orthographic/custom axis/pivot/center 등 입력 확인 필요.
4. SortingGroup·서브메시·배칭/패스 계열 등 엔진 중간 키.
5. 같은 조건에서 셰이더 인스턴스 ID, 그 다음 **부호 있는 재질 인스턴스 ID**.
6. 그마저 같으면 엔진의 추가 키/노드 순서. 모드의 Renderer ID fallback은 진단 결정성을 위한 모델이며 게임의 최종 tie 증명이 아니다.

**큐→층 순이라고 일반화하지 않는다.** GpuCensus는 근사 코드에서 queue를 먼저 정렬하지만(`M/GpuCensus.cs:242` 부근), 현재 SceneRenderOrder는 layer/order를 먼저 비교한다. 제공 인계의 “같은 (큐, 층, 순서, 거리)에서 shader→material”이라는 동점 관찰은 이 선행 키의 우선순위 전체를 증명하지 않는다.

다중 패스/Grab 셰이더가 단일 패스보다 뒤에 그려진 관찰은 `M/SceneRenderOrder.cs:9`–`:15`에 있다. 여기서 `passCount>1` 가산은 **관찰을 재현하는 휴리스틱**이다. 모든 multipass가 GrabPass라는 뜻도, 모든 셰이더에 보편적인 API 계약이라는 뜻도 아니다. 임의 셰이더에 적용하려면 다시 확인해야 한다.

### 4.2 종류별 재질·셰이더·텍스처

| 종류 | 생성/공유 방식, 상태 입력 | 순서/배칭 영향과 근거 |
|---|---|---|
| 이미지 장식 | spriteRenderer.material로 개별 인스턴스 접근, tileShader 사용. smoothing에 USE_POINT_SAMPLING. Sprite의 texture가 주입됨 | `G/scrVisualDecoration.cs:219`, `:464`, `G/RDUtils.cs:1289`. 셰이더 같아도 material ID와 Sprite texture/MPB가 다를 수 있음 |
| 장식 mesh 대체 경로 | visualDecoShader로 new Material, RT/mainTexture, color/opacity/tile | `G/scrVisualDecoration.cs:467`; 3.7절 기본 비활성 분기. 현재 큰 비용으로 세지 않음 |
| 타일 본체 | FloorRenderer.Awake가 renderer.material을 한 번 얻어 필드에 보관. customWorldMaterial은 new Material 복제. FloorMeshRenderer.color는 그 필드에 SetColor | `G/FloorRenderer.cs:51`, `G/FloorMeshRenderer.cs:10`, `G/scrFloor.cs:639`. `.material` 문자열이 보인다고 매번 복제된다고 쓰면 틀림 |
| 타일 텍스처/상태 | _IconTex, _IconOutlineTex, _MainTex, 스타일/shadow/color/flash/opacity | `G/scrFloor.cs:1068`, `:1109`, `:1597`, `:1932`. 단순 tint 하나로 공유 키를 만들 수 없음 |
| 홀드 | renderer.material의 HoldShader 파라미터, 끝 원에는 Sprites/Default 새 재질 | `G/scrHoldRenderer.cs:76`, `:280`, `:310`, `:360`. 타일/홀드 기하와 완료도 독립 |
| 행성 | PlanetSprite mesh 재질의 _PlanetTex/_AlphaMask/_Frame, 표시 여러 renderer와 꼬리/링/입자 | `G/PlanetSprite.cs:24`, `:60`, `:82`, `G/PlanetRenderer.cs:346`. 보이는 행성 하나가 draw 하나라는 보장 없음 |
| 글자 장식 | Canvas와 UI Text/font 재질, 생성 메시와 Canvas 정렬. TextGenerator는 collider 구성 | `G/scrTextDecoration.cs:79`, `:106`. 이미지 SpriteRenderer와 같은 재질 공유 대상이 아님. TextFix 기존 해결 |
| 입자 장식 | ParticleSystemRenderer.material.mainTexture, 색은 main.startColor, maskInteraction | `G/scrParticleDecoration.cs:89`, `:95`, `:98`. 엔진 입자 geometry/정렬 별도 |
| 오브젝트 장식 | 내장 Floor/Planet을 구성하고 관련 모든 표시 renderer에 정렬 적용 | `G/scrObjectDecoration.cs:139`, `:277`. 단일 이미지 한 장으로 치환 금지 |
| prefab 장식 | 내부 prefab 고유 shader/material/renderer 집합 | `G/scrPrefabDecoration.cs:16`. 전수 정적 인덱스와 런타임 submesh/pass 조사 필요 |
| BlendModes | dirty 때만 SetMaterialProperties. 공유 허용이면 SharedMaterials, 아니면 CreateBlendMaterial. BLENDMODES_MODE_* variant, overlay/stencil/point sampling | `G/BlendModes/BlendModeEffect.cs:384`, `:399`, `:417`, `:481`, `:536`; `G/BlendModes/ShaderUtilities.cs:35`, `:70`. 공유 기능이 이미 있으므로 “블렌드 전부 매 프레임 new Material” 주장은 틀림 |
| 카메라 필터 | 필터 컴포넌트별 lazy Material(SCShader), 문자열 setter, OnDisable DestroyImmediate | `F/CameraFilterPack_Blur_Movie.cs:21`, `:40`, `:82`. 비활성→재활성 첫 Material 생성과 상시 비용 구분 |

전수 literal·대입 지점은 [render-state 인덱스](data/ingame-render-state-index.md)에 있다. shader asset 427개/material 237개 같은 수는 기존 에셋 표 `data/shaders.tsv`·`data/materials.tsv`의 스냅샷이지 현재 프레임 활성 셰이더 수가 아니다. 런타임 공유/MPB/키워드는 census로 별도 확인해야 한다.

`Renderer.material`의 **첫 접근/복제**와 캐시된 Material의 setter를 분리해야 한다. `sharedMaterial`로 읽는 것은 복제를 피하지만 그 객체를 바꾸면 사용자 전체에 전파된다. MPB가 있더라도 블록 존재 여부만으로 값이 같다고 판단할 수 없다. index별 MPB는 renderer-wide 블록과 별도로 처리한다(`M/GpuCensus.cs:205` 부근). Sprite texture 역시 material.mainTexture만 읽으면 틀릴 수 있다.

### 4.3 왜 기존 자동 배칭만으로 해결되지 않는가

- **동적 배칭:** 정렬상 인접해야 하며 material/variant/texture/패스·마스크 상태가 호환돼야 한다. 개별 재질과 서로 다른 Sprite texture, 타일 정렬·기하, Canvas/입자/멀티패스 경계가 있다. “작은 quad니까 모두 배칭된다”도 “MPB 하나면 모든 배칭이 금지된다”도 코드만으로 단정할 수 없다. 실제 절단 이유는 프레임 디버거/Draw 열이 필요하다.
- **SRP Batcher:** 보관 census의 camera path는 Forward이며 게임은 Built-in OnRenderImage/GrabPass 경로다. SRP를 쓰는 게임이라고 볼 자료가 없다. SRP Batcher 스위치만 켜는 제안은 현 구조의 해법이 아니다. 파이프라인 교체는 결과/필터 계약을 바꾸는 별도 대공사다.
- **인스턴싱:** 원본 렌더러가 Instanced draw를 제출한다는 증거가 없고 셰이더도 instance별 색/행렬/UV/스텐실 입력을 지원해야 한다. enableInstancing만 true로 바꾸어 해결되지 않는다(`M/InstBench.cs:230`의 경고와 일치). 정렬 연속 구간·텍스처 묶음·외부 장벽을 유지해야 한다. 기존 GpuDeco/RunLod가 이미 접근하는 영역이다.
- **GrabPass:** 직전까지 그려진 framebuffer가 다음 물체의 입력이다. UnifiedGrab 한 번으로 바꾸면 중간 draw의 결과가 빠진다. FastBlend는 지원되는 제한된 모드만 이미 처리한다(`M/FastBlend.cs:8`–`:24`). 비지원 블렌드까지 확대하는 것은 무손실 상식적 변경이 아니다.

## 5. 실제 전환 계산과 해석

### 5.1 계산 결과

`gpu-census-20261108-074301-801.tsv`의 **Camera** draw 열 `:313`–`:1199`를 파일 순서대로 셌다. 선택한 이전 파일도 함께 보인다. 맵 이름/곡 시각은 이 파일만으로 확정하지 않아 날짜로 표기한다. 전체 표는 부록에 있다.

| 원자료 | 후보 material slot | 연속 선행 키 구간 | 셰이더 이름 전환 현재→완화최소 | 재질 전환 현재→완화최소 | 주텍스처 전환 현재→완화최소 |
|---|---:|---:|---:|---:|---:|
| 2026-10-18 22:16:00 | 1,159 | 181 | 53→31 | 1,143→1,132 | 764→219 |
| 2026-11-04 08:22:53 | 387 | 303 | 93→59 | 373→357 | 111→74 |
| 2026-11-08 07:43:01 | 887 | 520 | 592→298 | 839→692 | 632→334 |

**현재→완화최소는 안전한 최적화 결과가 아니다.** 현재는 GpuCensus의 queue→layer→order→distance→Renderer ID 근사 열이다. 실제 엔진 shader/material tie와 다르다. 따라서 최근 파일의 592회 중 일부는 **추정 정렬기가 만든 교대**일 수 있으며 현재 GPU shader bind 592회라고 쓸 수 없다.

최소 계산은 인접한 (queue, layer, order, distance) 구간 안에서 상태별 모든 배치를 허용하는 동적 계획이다. 각 구간의 서로 다른 상태를 k개라 할 때 시작/끝 상태가 다르면 내부 최소 k-1회, 같으면 k>1이고 그 상태의 행이 두 개 이상일 때 k회 전환을 허용하고 구간 경계 전환까지 최소화한다. 셰이더/재질/텍스처를 **각각 독립 최적화**했다. 실제 하나의 순서가 세 열의 최솟값을 동시에 달성한다는 뜻이 아니다.

원자료 distance는 소수 셋째자리로 반올림돼 있다. 동일 문자열은 실제 float 거리 동일성의 증거가 아니다. MPB 값·SortingGroup·패스·정확한 shader ID·스텐실·겹침을 무시했다. 가장 최근 표본의 MPB 행은 564개다. 그러므로 이 표는 **제약을 완화한 가능성 조사**이고 무손실로 가능한 절감 숫자는 아직 없다. 현재 순서를 그대로 지키는 무변경 대조군의 전환 수는 현재 열과 같다.

### 5.2 실제로 측정된 draw/SetPass

다음은 같은 시각에 정지한 gp2/gp6 판에서 baseline all 구간의 whole-frame 평균이다. 카메라별/개별 shader bind 횟수가 아니다.

| 판 | Draw Calls/프레임 | SetPass/프레임 | 직접 근거 |
|---|---:|---:|---|
| HELLO rlP | 233.613 | 147.000 | `R/gp2-hl-rlP.txt:3`–`:4` |
| 7777 rlP | 495.393 | 433.000 | `R/gp2-s7-rlP.txt:3`–`:4` |
| DD rlP | 454.256 | 387.000 | `R/gp6-dd-rlP.txt:3`–`:4` |

SetPass가 draw에 가까운 판이 있다는 것은 배칭/상태 조사 동기다. 그러나 draw≈SetPass만으로 원인이 material 복제라고 확정할 수 없다. 다중 pass/키워드/마스크/필터도 기여한다. 위 census와는 날짜/장면/빌드가 달라 수치 대조나 비율 결합을 하지 않는다.

### 5.3 그림을 보존하며 줄일 수 있는 조건

1. **기존 순서 유지 + 연속 호환 draw만 통합:** 원래 순서의 geometry/item 순서를 유지하고 같은 shader variant·모든 uniform·texture/sampler·blend/depth/stencil·pass·RT 상태만 묶는다. 배열/아틀라스가 있다면 texture 선택을 item에 넣는다. 원본 최종 tie가 불명확하거나 mask/Grab 경계가 있으면 원래 경로를 남긴다. 이것은 gap 문서의 DrawAdapter/연속 구간 설계의 실행 조건을 구체화한 것이며 새로운 독립 최적화라고 세지 않는다.
2. **재질 공유:** 값/키워드가 같아도 material ID를 합치면 tie의 최종 순서가 바뀔 수 있다. 원래 material 순서의 render packet을 별도로 보존하거나, 실제 비겹침/연산 교환 가능성을 증명한 구간에만 적용한다. 재질 사용자의 미래 setter/수명까지 감시해야 한다.
3. **텍스처 배열/아틀라스:** draw 순서는 고정하고 인접 항목의 texture 바인딩만 줄이는 방식이 가능하다. 색공간·포맷·mip·point/bilinear·wrap·UV border/crop·원본 크기·VRAM을 보존해야 한다. 큰 이미지를 일괄 패딩하거나 mip/압축을 바꾸는 방식은 무손실로 분류하지 않는다.
4. **shader ID 활용:** 동점이면 엔진이 이미 shader ID 순서를 고르므로 ID만 유리하게 만들겠다고 셰이더를 복제하는 것은 shader/variant 수를 늘릴 수 있다. 다른 shader 사이 알파 겹침 순서가 바뀌므로 생성 순서 조작을 해결책으로 채택하지 않는다. 실행마다 같은 ID가 된다는 보장도 없다.
5. **같은 sortingOrder 안 재배치:** 전체 선행 키와 그룹/패스 조건이 같고, 해당 순열의 모든 겹치는 쌍이 교환 가능해야 한다. 서로 겹치지 않음이 픽셀 영향 범위까지 증명되거나 동일 연산의 교환성이 rounding까지 성립해야 한다. 단순 AABB 비겹침은 보수적 제외 조건으로 쓸 수 있지만 Grab/이웃 샘플 필터의 영향까지 포함해야 한다. 일반 SrcAlpha 섞기는 비가환이므로 기본 금지다. depth nudge도 원래 순서 보존 증명으로 쓰지 않는다.

## 6. 카메라·필터·RT 체인

### 6.1 네 카메라와 수동 Render

`G/scrCamera.cs:283` SetupRTCam은 BGStaticCam/BGMovingCam/Camera의 targetTexture를 **같은 camRT**로 설정하고 Overlaycam/Quad를 켠다. 보통 배경→움직이는 배경→메인 콘텐츠가 camRT에 쌓이고 Quad가 화면에 보여 준다. 실제 clear/depth/부가 camera는 장면 census로 확인한다. `R/rts/rtcensus-hlbase.txt:149`–`:153`은 BGStatic SolidColor, BGMoving Depth, Camera Nothing, Overlay는 화면으로 기록한다. 동일 depth의 카메라가 있으므로 표의 나열만으로 동점 카메라 실제 제출 순서를 확정하지 않는다.

해상도 변경 때 `G/scrCamera.cs:300` 주변에서 이전 RT Release 후 new RenderTexture(Screen.width, Screen.height,24)와 Quad scale을 갱신한다. 매 프레임 새 RT를 만드는 코드가 아니다. 실제 census의 RT depth/포맷/MSAA가 생성자 인자와 다를 수 있어 런타임 descriptor를 우선한다.

custom FPS는 `G/scrCamera.cs:587`에서 별도 표시 RT를 만들고, `:606` lockCustomFrameUpdate 검사 후 `:618`에서 BGStatic/BGMoving/Main을 Render하고 camRT→표시 RT로 **추가 Blit 1회** 한다. manager도 `G/scrDecorationManager.cs:196`에서 CheckUpdateFrameRateScreen을 부른다. 두 진입점이 있다고 매 프레임 2회 렌더한다고 단정할 수 없다. lock/마지막 시각이 중복을 막는다. 프레임 제한 효과의 의도적 화면 유지 복사를 일반 모드에서 무조건 없애면 효과가 바뀐다.

### 6.2 OnRenderImage는 한 종류가 아니다

| 경로 | 실행당 Blit/임시 RT | 주의점 |
|---|---|---|
| 단일 패스 필터 | 일반적으로 material Blit 1회, shader 없음이면 copy 1회 | 예: `F/CameraFilterPack_AAA_SuperComputer.cs:51`. 성능은 shader 내부 루프/해상도에 좌우됨 |
| Blur_Movie | FastFilter>1이면 작은 temp 1개 + shader Blit + upsample copy = 2회. 아니면 1회 | `F/CameraFilterPack_Blur_Movie.cs:40`–`:72`. 파일의 Blit 지점 4개가 매번 4회 실행되는 뜻은 아님 |
| ScreenTile/Scroll | 각 shader 경로 또는 copy 중 하나로 1회 | `G/ScreenTile.cs:31`, `G/ScreenScroll.cs:33`. 필드가 항등이어도 enabled면 callback에 들어올 수 있음 |
| VideoBloom 중립 | amount들이 0이거나 MasterAmount=0이면 copy 1회 | `G/VideoBloom.cs:177`–`:187`. component disabled와 enabled 중립값은 다름 |
| VideoBloom 활성 | threshold/downsample 1~2회 + BloomBlit 반복 + 마지막 합성 1회 | `G/VideoBloom.cs:190`–`:237`. ARGBHalf temp 풀 획득과 blur radius/큰 bloom 여부로 수가 달라짐 |
| BlendModeEffect의 OnRenderImage | extension에 위임 | `G/BlendModes/BlendModeEffect.cs:528`. 모든 sprite 컴포넌트가 카메라 전체 화면 Blit를 하는 것으로 세면 틀림 |

CameraFilterPack 수백 종 전체의 존재는 전수 필터 인덱스에서 확인한다. **378은 모든 OnRenderImage 선언 수**이며 CameraFilterPack 378종 또는 동시 활성 378개가 아니다. Blur·motion blur·rain·feedback·downsample 등 구조가 달라 모두 한 패스라고 가정하지 않는다.

VideoBloom은 `G/VideoBloom.cs:108` BloomBlit에서 radius1==0이면 source→blur1 복사 후 반환한다. 아니면 초기 copy 1회, num2번 blur0, medium 출력 1회, large 출력이 있으면 추가 (num3-num2)회 blur0와 large 출력 1회다. 따라서 radius1≠0인 helper의 Blit 수는 `num2+2`(large 없음), `num3+3`(large 있음)이다. 바깥 threshold/downsample 1~2회와 최종 합성 1회를 더한다. 이는 코드식이며 특정 맵의 실제 radius/활성 분기를 재지 않아 프레임당 확정 숫자로 쓰지 않는다. GetTemporary는 풀 획득이지 매번 OS/VRAM 새 할당을 뜻하지 않는다.

`VideoBloom.cs:145`, `:166` 주변에는 직전 반복 temp를 Release한 뒤 참조를 계속 쓰는 듯한 디컴파일 표현도 있다. 이번에는 IL을 대조하지 않았고 정상/버그/최적화 근거 중 어느 것으로도 확정하지 않는다. RT 수명 변경 전 설치 IL과 원래 동작을 확인해야 한다.

### 6.3 비활성·중립·순서에 따른 복사

- **component disabled:** Unity의 정상 카메라 image effect 호출 대상에서 빠진다. 카메라 체인 실행 비용이 수백 비활성 필터 모두에 든다고 쓰지 않는다. 다만 component 메모리/필터 관리 목록, OnDisable의 재질 파괴·다시 활성화 시 생성은 별개다. 수동 reflection 호출은 별도 조건이다.
- **enabled지만 효과값 0:** 코드가 copy Blit를 하거나 shader를 그대로 돌릴 수 있다. FilterIdentity는 승인된 필터/환경만 처리하는 기존 실험이다(`M/StutterFix.cs:1248`). 같은 필드를 0으로 보는 것만으로 승인 범위를 넓히지 않는다.
- **연속 카메라 필터:** 순서가 결과다. Blur 앞/뒤의 색·노이즈·왜곡은 교환 가능하지 않다. `GetComponents` 순서와 등록 순서만으로 GPU 실제 순서를 대체하지 않는다. callback trace 또는 기존 RDC의 event/RT 연결이 필요하다.
- **복사 융합:** 같은 픽셀만 읽는 색 필터라 해도 중간 RT의 UNorm 반올림·sRGB·clamp를 보존해야 한다. motion blur/feedback/noise의 시간·이전 프레임 입력은 별도다. 기존 `moonshots-20261107.md:45`의 후보를 이어서 검증할 수 있지만 새로 측정된 이득은 아니다.

실제 필터 GPU 구간 예: 7777 `R/gp2-s7-rlP.txt:16` SuperComputer 0.7336ms, `:17` Blur_Radial 0.1824ms, `:18` Rain_Pro 0.1363ms, `:28` Camera VideoBloom 0.0578ms. 같은 판에서 BGMovingCam 전체는 `:12`의 1.3221ms다. 이 비용은 현재 카메라 안에 이미 포함되어 있다. 다른 시각에 0.97/1.5ms였다는 예전 측정과 평균내지 않는다.

## 7. 후보와 상위 다섯 가지 조사

**새로 검증된 대형 미해결 원인 5개를 찾았다는 결론은 아니다.** 이미 손댄 장식/Transform 경로와 과거 열린 설계를 제외하면, 이번 코드 조사만으로 큰 이득을 보장할 후보는 없다. 순서는 제거 가능성과 남은 조사 가치 기준이고 FPS 기대치가 아니다.

| 순위 | 후보·기존 작업과의 관계 | 줄이는 대상 | 수치 근거/미확인 | 무손실 조건 | 구현 크기·검증 |
|---|---|---|---|---|---|
| 1 | **원래 draw 순서를 유지하는 연속 packet의 상태 공유/텍스처 선택**. gap 설계의 후속 검증이며 전역 재정렬과 구분 | 렌더 스레드 CPU, draw 제출; 일부 GPU | whole-frame SetPass 147/433/387. 실제 호환 연속 길이·절감 ms 없음 | item·pass·mask·Grab 순서, MPB/variant/texture 전부 보존 | 큼. 정확한 draw trace→호환 run 분포→정지/동적 픽셀→ABBA·복원 |
| 2 | **FloorMesh 셰이더에서 실제 상수 상태별 동일 출력 특수화**. 타일 스타일 setter 생략/MPB 실험과 다름 | GPU | 기존 바이트코드 92명령·5샘플 vs Sprites/Tile 8~10·1. 절감량은 미측정 | icon/outline/shadow/flash 등 실제 상수 증명, 분기 전환 시 즉시 원본, float/색공간 동일 | 큼. 바이트코드/상수경로 대조, 모든 스타일·전환·pixel 비교 후 타일 GPU 구간 A/B |
| 3 | **활성 카메라의 연속 pointwise 필터 융합**. 기존 moonshot을 정확한 RT 계약으로 좁힘 | GPU, 일부 렌더 CPU | 여러 활성 필터와 개별 0.03ms대 구간 존재. 호환 연속 필터 수 미확인 | 순서·중간 포맷/반올림·시간 상태 보존. 이웃 샘플/feedback 제외 | 큼. 실행 체인 수집→소수 허용 목록→각 중간/최종 pixel→전환/resize·ABBA |
| 4 | **Volume+None의 빈 Sequence 생성 제거**. 코드상 생성 위치를 좁힌 후보 | 메인 CPU/할당·DOTween 관리 | `G/scrFloor.cs:1273` 근처 구조. 실제 해당 타일 수·생성 비용 없음 | 빈 트윈의 전역 카운트/외부 callback 관측 여부 확인, 색 쓰기는 그대로 | 작음~중간. 새 계측 설계로 잔여 생성수 확인, 원본 상태/전곡·할당 A/B |
| 5 | **hit 외 extra tick/hold 예약의 호출 경계 줄이기**. 10-20 열린 후보의 잔여 범위 확인 | 메인 CPU/오디오 예약 | conductor Update 비용은 있으나 해당 하위 비중 미분리 | DSP 예약 시각·피치·재시작/정지·음량·voice 수 동일 | 중간~큼. 두 cursor별 예약 count/time→사운드 예약 표 대조→긴 hold/일시정지 ABBA |

2번은 셰이더 전체를 수학적으로 단순화하면 같다는 주장이 아니다. 실제 상수 상태에서 원본 출력과 같은 variant를 선택하는 연구이며 shader 변경도 현재 작업 범위에서는 하지 않았다. 4·5번은 크기가 작을 수 있어 우선 **개수 계측으로 중단 여부를 판단**한다. 수치가 없는데 5%·1ms 목표를 붙이지 않는다.

후순위: 필터 문자열→ID 치환은 `docs/engine-calls-20261020.md`의 기존 저위험 후보다. UI dirty 쓰기·particle shape/simulationSpeed 동일 값 생략도 호출 수는 있지만 소유자/잔여 비용이 확인되지 않아 대형 원인으로 올리지 않는다. RunLod.PreCull은 `R/rts/mt1-hl-rlP.sym60.txt`에 보이지만 현재 `RunLod.cs`가 다른 작업의 수정 대상이므로 여기서는 변경하거나 별도 새 최적화 성과로 주장하지 않는다.

다시 제안하지 않는 것: 타일 스타일 건너뛰기, 타일 색 MPB/같은 값 생략, 알파 0 discard, 밉맵/화질 저하, 불투명 가리기, D3D12, 단순 홀드 재질 조회 캐시, 이미 구현된 HoldSkip/HitboxQuery/TypeCache/HitSoundBatch. 예전 기각/채택 표가 엇갈리면 현재 `CLAUDE.md`, `night-20261108.md:33` 이하와 현재 모듈을 우선했다.

## 8. 부족한 자료와 후속 측정 스크립트

### 8.1 현재 없는 증거

보관 `night-results` 아래 `.rdc`를 재귀 확인했지만 파일을 찾지 못했다. `E:/StutterFixWork/rd-hl.out:3`도 `RD_DONE rd-hl []`다. RenderDoc 설치는 있지만 실행 성공/캡처 존재를 뜻하지 않는다. 기존 `rd_events.py`는 GPU duration 내림차순으로 정렬해 쓰고 shader/texture 상태를 내보내지 않으므로, 그 결과만 있어도 연속 bind 순서 분석에는 부족하다. 이번에는 새 캡처를 하지 않았다.

또한 `R/deep-queue.tsv`는 0바이트였다. 결과가 없는 요약 파일에서 성공/실패 수치를 복원하지 않았다. `gp3-wf-rlP.txt`처럼 selected가 많아도 changed_renderable/main_candidates가 0이거나 숨김 enforcement 비용이 큰 비교는 셰이더별 인과 근거로 쓰지 않는다.

### 8.2 order1.py 사용과 출력

`E:/StutterFixWork/order1.py`를 작성했다. **이번에는 계획 출력만 검증했고 --run은 실행하지 않았다.** 현재 장시간 측정이 끝난 뒤 Claude가 기존 runner로 별도 판을 돌리는 용도다. 기본 실행은 JSON 계획을 stdout에 표시하며 파일/게임/설정에 손대지 않는다. `--run`일 때만 `run_locked.py`를 호출한다. runner는 빌드하지 않지만 미리 존재하는 PlayerAuto DLL 설치와 게임 실행/복원을 하므로 현재 조사 중 실행해서는 안 된다.

```powershell
# 계획만 보기 (게임 실행 없음)
python E:\StutterFixWork\order1.py hl rlP

# 현재 측정 종료 후 별도 진단 판: rlP 원본 렌더 경로, gBsSp 대체 경로
python E:\StutterFixWork\order1.py hl rlP --run
python E:\StutterFixWork\order1.py s7 gBsSp --run

# 별도 에디터 정지 상태의 합성 정렬 실험
python E:\StutterFixWork\order1.py hl rlP --sort-probe --run
```

scene 계획은 기존 gp2/own4 형식대로 open→설정 명령→seek/waitsongpos→pause→gpuprobe 표본→probe off→재질/순서/census를 실행한다. 긴 waitsongpos는 기존 명령의 400초 한도를 쓰고 일반 wait는 최대 4초다. census/재질 전수 검색은 성능 표본 뒤에 둔다. 정지 장면의 GPU 시간은 동적 재생 FPS/Update 비용을 대체하지 않는다. seek로 조건 이벤트 이력이 달라질 수 있으므로 중요한 결론은 자연 재생에서도 확인한다.

| 기존 명령 | 실제 출력과 한계 | 코드 근거 |
|---|---|---|
| gpuprobe on/reset/state/off | 카메라·효과 GPU elapsed 구간과 whole-frame Draw/SetPass. nested 합산 금지 | `M/AutoTest.cs`의 gpuprobe 분기, 기존 `gp2.py` |
| gpucensus | 다음 Camera onPreRender 한 번. 후보 renderer/material slot·bounds/texture/MPB·활성 OnRenderImage, 로컬 TSV | `M/GpuCensus.cs:99`, `:153` 이하. GPU visibility/실제 제출 열 아님 |
| ownprobe natorder | 활성·isVisible·forceOff·mask·transparent 큐 필터 뒤 SceneRenderOrder 정렬 목록 | `M/OwnProbe.cs:119`. isVisible은 다른 카메라/직전 프레임 영향, sharedMaterial 한 슬롯, 거리 F3, 실제 submit trace 아님 |
| countmats | SFTestMat와 현재 타일 재질 이름 기반 Instance 잔여수 | `M/AutoTest.cs:549`. 모든 draw의 재질 전환/개별 생성 횟수 아님 |
| matstate | MatProbe 누적 new Material(Shader)/Shader.Find 시간·횟수 | `M/ParticleDump.cs:11`. `M/StutterFix.cs:126`의 matprobe 파일 조건으로 hook이 설치된 판에만 의미 있음. clone 생성자 Material(Material)·Renderer.material 내부 복제 전체를 측정하는 기능 아님 |
| sortprobe all 50 seed | own 합성 장면의 반복 실험, trials.csv와 report. stopped editor만 허용 | `M/SortProbe.cs:47`–`:88`. 실제 맵 연속 draw 열 아님. 명령이 완료될 때까지 false를 반환 |
| rdcapture | 주입된 RenderDoc API TriggerCapture로 다음 프레임 캡처 요청 | `M/RuntimeCensus.cs:141` 부근. 이번 스크립트에는 넣지 않음. API 요청 성공도 결과 파일/실제 draw 확인과 별개 |

runner의 프로세스 exit 0만 성공으로 쓰지 않도록 `order1.py`는 새 로그의 단계 실패/필수 출력도 검사한다. 실제 진단 판을 수행한 뒤 DLL/probe/설정 복원은 기존 runner 결과와 설치 상태로 다시 확인한다. 이번 작업에서는 설치 상태를 바꾸지 않았으므로 “복원 시험 통과”라고 보고하지 않는다.

`order1.py`는 matprobe 설치 파일을 만들지 않는다. 설치 로그에 hook 활성 증거가 없으면 matstate의 0을 “곡 중 재질 생성 0회”로 해석하지 않는다. sortprobe는 complete trials 출력까지 있어야 스크립트 검사를 통과하며, 완주해도 report의 invalid/stable 항목은 별도로 검토한다.

### 8.3 새 C#이 필요한 경우의 설계만

기존 명령만으로 **정확한 GPU shader/texture bind 열**은 얻을 수 없다. 이후 새 계측을 허용할 때도 C# renderer 전수 목록을 실제 draw 열이라고 이름 붙이지 않는다.

- managed snapshot: generation/frame/camera/RT·viewport·sort mode, renderer/submesh/SortingGroup/pass, 정확한 float distance 비트, signed material ID와 shader ID, variant keywords, effective texture와 sampler·per-slot MPB 값, mask/stencil/depth/blend를 구조화한다. 초기화/진단 프레임은 제외한다.
- 실제 제출: 기존 RDC가 생기면 eventId 증가 순서로 draw/dispatch/copy/clear와 RT를 내보내고 각 draw 시점의 VS/PS·SRV/sampler·blend/depth/stencil을 읽는다. copy/Grab/RT switch를 장벽으로 유지한다. duration 내림차순 자료는 시간 순서로 되돌려도 상태가 없으면 충분하지 않다.
- 결과 지표: shader object/variant 전환, material 의미 상태 전환, 전체 texture/sampler 바인딩 전환을 각각 센다. 원본 순서 유지의 compatible run 길이 분포와 불호환 이유를 먼저 낸다. 메인 카메라/다른 카메라/필터/Canvas를 분리한다.
- CPU 후보: Volume None 타일 count·Sequence 생성/등록/정리 수, tick/hold 예약 수·경과 시간만 진단 창에서 센다. main/render/GPU 각각 적합한 타이머를 쓴다. ETW ThreadTime 없이 경과 시간을 실행 CPU라고 부르지 않는다.

성능 판단은 같은 빌드·설정·해상도·곡 구간의 자연 재생 ABBA와 프레임 분포/최악 구간으로 한다. 무손실 후보는 정지 장면 외에도 겹침·마스크·깊이/이미지/필터 켬끔·일시정지/재시작/맵 교체의 픽셀·상태·원본 복귀를 검증한다. 이 문서는 해당 합격을 대신하지 않는다.

## 9. 이번 작업의 완료 범위

프레임 주요 경로의 의미 분석, 제공 소스 전체의 정적 인덱스, 재질/필터 전수 호출 지점, 보관 후보 열의 전환 계산, 실제 SetPass와의 구분, 기존 해결/기각 후보 분리, 후속 진단 스크립트 작성을 완료했다. 새 측정이 필요한 셰이더 전환 인과·정확한 draw 열·무손실 packet/특수화·실제 FPS 이득은 미확인으로 남긴다. 현재 다른 작업의 C# 변경을 이어 구현하거나 커밋하는 것은 이 인계의 범위가 아니다.

최종 오프라인 검증: Python 두 파일 AST, 본문의 파일:줄 참조와 로컬 링크, UTF-8 치환문자 부재, 전환 최소화 알고리즘의 작은 입력 4종 전순열 대조, 6맵×2설정×2모드의 계획 24종을 확인했다. 계획 검사에는 게임 실행이 없다. `python tools/analysis/ingame_audit.py validate`로 재현한다. 작업 관찰 스킬 3.5.0의 관찰 점검에서는 이번 산출물 외에 새로 보존할 범용 개선 기록을 추가하지 않았다. 현재 인계의 문서·Python 한정을 따라 별도 관찰 로그/스킬 변경도 하지 않았다.
