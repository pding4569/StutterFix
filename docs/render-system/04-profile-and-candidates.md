# 프로파일로 본 실제 비용과 후보

자료: `night-results/ep-*.frames.txt`(메인 스레드 1000Hz 표본, 곡 15초 구간)를 함수별로 다시 집계. 값은 **그 함수 아래를 포함한 표본 %**다. 표는 `data/profile/`.
주의: 2560x1440, Quartz 등 설치 모드와 성능 오버레이가 켜진 상태. 대기 표본도 전체에 포함된다. 표본 비율은 실제 절감을 부풀리는 경향이 있었다(2026-10-20: 4.7% 예측이 0.05~0.1ms). 렌더 스레드는 표본에 없다.

## 함수별 비율

| 묶음 | Exit | HELLO | DD | 7777 | Windflower | Arche |
|---|---:|---:|---:|---:|---:|---:|
| 장식 트윈 `DecoAnim:UpdatePrefix` | 2.4 | 3.2 | 3.1 | 1.7 | 7.3 | 31.1 |
| 장식 LateUpdate `TightLate:List` | 4.5 | 6.3 | 5.9 | 4.1 | 14.2 | 16.6 |
| `Dormancy:Prepare` | 0.5 | 0.5 | 0.7 | 0.5 | 2.9 | 2.8 |
| 홀드 `scrHoldRenderer:Update` | 5.0 | 0 | 0 | 0 | 2.5 | 0 |
| 타일 색 `FloorMeshRenderer:set_color` | 2.4 | 1.2 | 0.4 | 2.7 | 0.5 | 0.5 |
| DOTween `TweenManager:Update` | 2.1 | 1.9 | 0.7 | 1.7 | 1.2 | 0.9 |
| 캔버스 `SendWillRenderCanvases` | 2.6 | 2.1 | 1.5 | 2.1 | 1.7 | 1.4 |
| 그중 `TextMeshProUGUI:Rebuild` | 1.9 | 1.5 | 1.0 | 1.5 | 1.1 | 1.0 |
| 입력 `Rewired InputManager:Update` | 2.0 | 1.4 | 1.1 | 1.5 | 1.3 | 1.0 |
| 입력 `scrController:UpdateInput` | 1.4 | 1.9 | 0.9 | 1.4 | 1.0 | 0.8 |
| FFT `FMOD::DSPFFT::process`(자기 비용) | 1.4 | 1.1 | 0.9 | 1.4 | 1.3 | 0.8 |
| `memcpy_repmovs`(SpriteMask 렌더 노드) | 6.5 | 1.1 | 0.6 | 1.3 | 0.3 | 0.2 |
| `UnityModManager/UI:Update` | 1.2 | 1.1 | 0.7 | 0.9 | 1.0 | 0.9 |
| `PerfOverlay:LateUpdate` | 0.9 | 0.6 | 0.5 | 0.8 | 0.6 | 0.3 |
| `EventSystem:Update` | 0.6 | 0.7 | 0.4 | 0.6 | 0.7 | 0.3 |
| `scrPlanet:Update` | 0.7 | 0.6 | 0.4 | 0.4 | 0.3 | 0.2 |
| `scrController:Update` | 0.4 | 0.3 | 0.2 | 0.3 | 0.2 | 0.1 |

## 읽은 것

1. **게임 핵심 로직은 아주 작다.** 공, 판정, 컨트롤러 본문이 각각 1% 안팎이다. 시간을 쓰는 건 **맵 시스템**(장식, 트윈, 타일 색, 홀드)과 엔진이다. 맵 시스템을 Unity 객체 밖으로 빼자는 방향과 맞는 자료다.
2. **히트박스 물리는 후보가 아니다.** `Physics2D`는 상위 500개에 없고 `scrPlanet:Update` 전체가 0.2~0.7%다. 앞 문서의 "새 후보"를 철회한다.
3. **캔버스 TMP 재생성은 대부분 설치 모드 때문으로 보인다.** Quartz의 Combo·Panels·KeyViewer 오버레이가 매 프레임 돈다. 다른 모드를 끄면 어떻게 되는지 재야 한다(**미확인**).
4. **이 설치는 `LowNoFft`가 꺼져 있다.** 코드 기본값은 true인데 `Settings.xml`이 false다. 그동안 측정에 FFT 1~2%가 포함됐다. `getSpectrum`을 쓰는 곳은 Volume 색과 공식 레벨 일부(`scrVolumeTrackerScale`, `scrBgbarnew`)다.
5. **모드 자신의 비용**: `PerfOverlay`와 `Main:OnUpdate`로 최대 약 1.5%. 측정 빌드에서 오버레이가 켜져 있어 사용자에 따라 다르다.

## 스크립트 실행 순서 (코드, MonoScript의 값)

`InputManager`(-32000) → Rewired → `EventSystem`(-1000) → `scrConductor`(-500) → `scrCamera`(-400) → TMP(-110~-100) → `scnGame`(-60) → `scrFloor`(-10) → `scrDecorationManager`(-5) → 기본값(0: `scrPlanet`, `scrVfxPlus`, DOTween 등) → `scrController`(400).
같은 값끼리 순서는 정해져 있지 않다.

## 설정값 (코드)

- 고정 시간 간격 50Hz. `Physics2D`는 FixedUpdate 시뮬레이션, `AutoSyncTransforms` 켜짐.
- 오디오: DSP 버퍼 1024, 샘플 48000, 실제 음성 255.
- 품질: 그림자·실시간 반사 없음, `streamingMipmapsActive` 끔, 이방성 끔, 비동기 업로드 2ms/4MB.

## 프리팹 (코드, `data/prefabs.txt`)

이미지 장식 하나는 GameObject 9개다. 켜져 있는 것: 루트(`scrVisualDecoration`, `scrParallax`), `child`, `sprite`(`SpriteRenderer`, `ToggleVisible`, `BlendModeEffect`), `spriteMask`(`SpriteMask` 꺼짐, `SpriteAlphaMask`), `failHitbox`(콜라이더 3개 꺼짐). 꺼져 있는 것: `renderer`, `selectionBorders`, `editorCollider`, `hitboxBorders`.
콜라이더는 기본으로 꺼져 있어서 물리 동기화 후보는 약하다. Arche 2만 8,835개면 활성 Transform이 약 14만 개다. 프리팹을 줄이면 변환 전파와 맵 열기 비용이 얼마나 주는지는 **미측정**이다.

## 후보

| 순위 | 후보 | 근거 | 위험 | 상태 |
|---|---|---|---|---|
| 1 | `Dormancy` 목록 거르기를 새로 잠든 장식이 있을 때만 | Windflower·Arche 2.9%. 잠든 장식이 하나라도 있으면 매 프레임 깨어 있는 장식 전부를 해시로 확인했다 | 낮음(논리적으로 같음) | 코드 수정함, 시험 전 |
| 2 | 일 없는 홀드 건너뛰기 | Exit 5.0%, Windflower 2.5%. 화면 밖·지난 홀드도 매 프레임 재질 값 6개 이상 쓴다 | 움직이는 타일과 bounds | 시험 필요. 재질 조회만 줄인 이전 후보는 효과 없음 |
| 3 | SpriteMask 렌더 노드 재할당 줄이기 | Exit 6.5% | 마스크 정렬 | 시험 필요 |
| 4 | 타일 색 같은 값 쓰기 건너뛰기 | Exit·7777 약 2.5% | 낮음 | 트윈이 실제로 값을 바꾸는지 확인 필요 |
| 5 | `MoveApply:RegisterLate`·`SkipForLate`의 해시를 표시 값으로 | Arche 2.6~3.4% | 낮음 | 시험 필요 |
| 6 | `DecoAnim:Compact` 주기 조정 | Arche 3.4% | 낮음 | 시험 필요 |
| 7 | 곡 중 Rewired 갱신 줄이기 | 1.0~2.0% | 키 입력 놓침. 곡 중 Esc 등이 Rewired를 쓴다 | 조심해서 조사 |
| 8 | FFT 끄기 설정 확인 | 1~2% | 없음(기존 옵션) | 시험 때 설정 통일 |
| 9 | 곡 중 `EventSystem`, `SendMouseEvents` 줄이기 | 0.3~1% | 일시정지 UI | 조사 |

위 %는 줄어들 수 있는 상한이며 실제 FPS는 ABBA로 확인한다.
