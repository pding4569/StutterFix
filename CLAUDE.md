# StutterFix

얼불춤(A Dance of Fire and Ice) 초고사양 커스텀 맵의 끊김을 없애는 Unity Mod Manager 모드.
사용자 목표: **플레이 중 순간 끊김을 아예 없애는 것** (평균 FPS보다 끊김 제거가 우선). 한국어로 대화한다.

## 경로

| 무엇 | 경로 |
|---|---|
| 게임 DLL (참조) | `D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\A Dance of Fire and Ice_Data\Managed` |
| 설치 위치 | `D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\Mods\StutterFix\` |
| 로그 | `%USERPROFILE%\AppData\LocalLow\7th Beat Games\A Dance of Fire and Ice\Player.log` (`[StutterFix]` 접두어) |
| IL 스캐너 | `tools/ilscan` (.NET 8, System.Reflection.Metadata). 예전 `%TEMP%\ilscan` 은 윈도우 임시 파일 정리에 지워졌다(2026-10-01 무렵) |
| 원격 | GitHub `pding4569/StutterFix` |

Unity 6000.3.10f1, Mono, UMM 0.32.5, HarmonyLib, DOTween. 모드 대상은 netstandard2.1.

## 작업 순환

1. 고친다 → `.claude/hooks/build-install.sh` 훅이 자동으로 빌드하고 Mods에 복사한다.
   게임에서 **Ctrl+F5**(또는 설정창의 "모드 다시 불러오기")를 누르면 게임을 켠 채로 새 DLL이 적용된다.
   새로 게임에 무언가를 걸면(Harmony ID, 정적 이벤트, PlayerLoop, 다른 모드 감싸기) `Main.Unload`에 되돌리는 코드도 반드시 넣는다.
2. 사용자가 게임을 켜고 맵을 돌린 뒤 "됐어"라고 한다.
3. `/log` 스킬로 최근 판만 요약해 본다.
   측정 MCP `sfmeasure`(`tools/sfmeasure`)가 붙어 있으면 2~3을 직접 한다: `sf_run`/`sf_ab` 로 게임을 켜서 돌리고 요약을 받는다. A/B 는 판마다 게임을 새로 켜는 `sf_ab` 로.
4. `/ship` 은 사용자가 부를 때만: 빌드, 설치, 한국어 커밋(측정 근거 포함), 푸시.

## IL 스캐너 (디컴파일러 대신)

```bash
cd tools/ilscan && dotnet build -c Release            # 처음 한 번
bin/Release/net8.0/ILScan.exe <dll> <호출이름>          # 그 이름을 부르는 곳 전부
TYPES=scrController bin/Release/net8.0/ILScan.exe <dll> ZZZ   # 타입의 필드/메서드(서명) 목록
METHOD=scrCamera bin/Release/net8.0/ILScan.exe <dll> ZZZ     # 타입의 메서드별 호출/정적필드 목록
```
인스턴스 필드(ldfld)는 `FIELDS=1` 일 때만 나온다. 디컴파일 결과(스크래치패드)도 %TEMP% 아래라 지워질 수 있다. 제네릭 호출은 `제네릭 X::Y` 로 풀린다.

## 확정된 원인과 해결 (측정값)

| 원인 | 측정 | 해결 |
|---|---|---|
| GC 정리 | A/B 125쌍: 평균 106→124fps | 곡 중 GC 멈춤 (`GcControl`) |
| `scrTextDecoration.SetCollider`가 매번 `new TextGenerator()` | 96MB/s, 초당 3891회 | 하나를 재사용 (`TextFix`) → 할당 110MB/s→4MB/s, 6GB 한계 도달 곡당 5~7회→0회 |
| DOTween `ReorganizeActiveTweens` O(n²) | 한 프레임 382ms (4981회) | 효과 도는 동안 `isUpdateLoop=true` (`TweenFix`) |
| PACL2가 매 프레임 글자 장식 34개를 같은 내용으로 다시 넣음 (`VariableStateManager.UpdateTexts`) | 호출 경로로 확인, 모드 끄면 0회 | 같은 글자면 `SetText` 건너뛰기 (`TextFix`) |
| 편집 복귀 시 `UnloadUnusedAssets` | 매번 120ms | 건너뛰기 |
| 에디터에서 죽고 다시 하기의 장식 전체 다시 설정(`ResetDecorations`, 장식마다 `Setup` 약 45µs) | Arche 1.3~1.45초씩 | 나가기의 "안 바뀐 장식은 가볍게"(`ExitFix`)를 다시 하기의 ResetScene 시점에도. 에디터 1.36→0.87초, 게임 화면(목록에서 연 맵) 두 번째부터 1.15→0.47초 |
| 타일 색 바꾸기가 스타일을 바꾸면 `FloorMesh.UpdateAllRequired`(scrController.LateUpdate)가 처음 보는 메시를 곡 중에 만듦(하나 약 45µs) | Windflower 99.0초 1,102개 중 604개 새로 만들어 32ms (엔진 단계 LateUpdate 33ms) | 재생 준비 때 같은 키·같은 필드로 미리 만들기 (`MeshWarm`) → 곡 중 새 메시 0개, A/B 4판: 41~44ms 끊김 → 없음, 준비 +0.27초 |
| 게임이 효과음을 처음 쓸 때 `AudioManager.FindOrLoadAudioClip` 이 메인 스레드에서 `Resources.Load` + Vorbis 압축 풀기 | Windflower 곡 22.7초: sndHeldbeatStartFuse 7ms + sndHeldbeatLoopFuse 24ms 한 프레임 (PerfView: 38ms 내내 FMOD 압축 풀기) | 재생 준비 때 맵이 쓸 수 있는 소리(맵·타일·자유 이동·행성 수·ffxPlaySound·누르는 박자 소리) 미리 불러오기 (`SoundWarm`) → 곡 중 새로 불러온 소리 0개, A/B 4판: 끔 29~30ms(두 판 모두 22.7초) → 켬 없음, 준비 +56ms |
| 타일 이동 효과(`ffxMoveFloorPlus`)가 타일마다 DOTween 을 만들고 이전 것을 Kill | Lost Requiem 531.8초 효과 하나 53~55ms(타일 8,171개, 트윈 2만 개), HELLO 125.6초 길이 0 이동 22ms(타일 8,933개) | 모드가 직접 돌림 (`FloorAnim`, DecoAnim 과 같은 표 방식). 길이 0 은 ZeroTween 식으로 바로 씀. 검증: 4개 맵 진짜 DOTween 과 나란히 프레임 47만 번 다름 0, 길이 0 원래 코드 결과와 다름 0. 플레이어용: LR 56→26ms, HELLO 41→25ms, 끊김 1→0 |
| 곡 초반에 처음 쓰는 효과의 JIT (모드 함수·게임 효과 함수가 처음 불릴 때 컴파일) | HELLO 2026 5.1초: 첫 장식 이동(장식 1개) 8ms, 첫 타일 이동(타일 1개) 6ms, 프레임 42~43ms | 켤 때 `MethodHandle.GetFunctionPointer` 로 미리 컴파일 (`JitWarm`, 모드 1,848개 + 게임 효과·장식·타일·행성·지휘자 1,110개, 약 0.47초). **Mono 에서 `RuntimeHelpers.PrepareMethod` 는 아무것도 안 한다**(1,843개 23ms, 비용 그대로). 플레이어용 A/B 4판: 끔 42·43ms → 켬 끊김 없음 |
| 편집기에서 맵을 새로 열 때 지난 맵이 안 풀림 (게임: `scnEditor.Start` 가 `Application.wantsToQuit` 에 더하고 `QuitToMenu` 에서만 뺌 → 옛 편집기·옛 scnGame·옛 LevelData 가 정적 이벤트에 남음. PACL2 `_textDecorationsOnPlay` 도 옛 글자 장식 → 옛 타일·장식 관리자로 이어짐. 모드도 `ExitFix.bases`·`FfxReuse.age`·`Dormancy`·`EffectBudget`·`LoadFix` 등이 지난 맵 장식·효과를 붙잡음) | Arche 뒤 HELLO: 정리 뒤 힙 1,630MB (HELLO 만 열면 약 0.3~0.5GB), Arche 다시 열면 2,578MB | 새 편집기 시작 때 지워진 편집기 콜백을 빼고 그 옛 scnGame.levelData·옛 장식 관리자 목록을 비움(`LeakGuard`), 새 맵 파일 열 때 모드 쪽 기록을 놓음(`ExitFix.LoadLevelPrefix`). → 694MB / 1,735MB. 찾는 도구: 자동 시험 `whoholds <형식>`, `whoholdsev`(지금 맵에 없는 LevelEvent), `listtail` |
| 깨진 이미지(PNG 끝이 잘림)가 있으면 게임 `LoadTexture` 가 null 텍스처에 "성공" 상태를 돌려줘 `GetOrAddSprite` 가 null 예외 → 장식 불러오기가 멈춰 **맵이 안 열림** | ALPHA WYSI EX 장식 이동 45867번 `nevCTF/nes_leeeeeeeeeeeetterbox_2-3-5.428571pp.png` (유니티 LoadImage 도 실패). 찾은 도구: `DecoDiag`(개발자용, `decodiag.txt`) | 상태를 오류로 바꿔 없는 파일처럼 건너뜀 (`ImagePrefetch.BrokenImagePostfix`) → 맵 열림 108초, 곡 끝까지 351 FPS |
| 이미지 원본이 매우 큰 맵에서 3072 로 줄여도 VRAM 이 모자람 (자동 한도가 첫 단계 3072 만 썼음) | ALPHA WYSI EX(이미지 1520장, 원본 약 100GB, 3072 에서 약 47GB = 비어 있는 VRAM 의 7.7배): 190~193초 그리기·GPU 대기로 31~42ms 프레임 연속 | 3072 로도 비어 있는 VRAM 의 5배를 넘으면 2048/1536/1024 까지 더 줄임 (`PredictDeepRatio`). sf_ab 2쌍: FPS 214→281, 곡 중 최악 42→17ms. 곡 전체: 최악 66→26ms, 끊김 3→0, 맵 열기 108→90초 |
| 큰 이미지를 줄이는 맵의 열기: C# 줄이기 + 원본 크기 그림·풀린 원본 줄을 메모리에 다 쓰고 다시 읽음 (ALPHA Arche: PNG 2.75GB 가 풀면 737GB, 대부분 0) | WYSI ALPHA 작업 스레드 합계 줄이기 291초 / 풀기·필터 82초. Arche ALPHA 열기 487초 | 네이티브 줄이기(`sf_downscale`, 정수 합계라 C# 과 바이트까지 같음) → 필터·줄이기 한 줄씩(`sf_unfilter_downscale`) → libdeflate 1.24 를 고친 흘려 풀기(`native/sfnative/stream_template.h`, `sf_png_shrink`) → 필터 없는 줄은 제자리에서, 0 인 64바이트 덩어리·칸 건너뜀, 합계 0 인 칸은 나눗셈 없이 0. 검증: 실제 PNG 14,710장 오프라인 다름 0, 개발자용 게임 안 전부 대조 다름 0. 열기: Arche ALPHA 487→71초, WYSI 89→30초, Finale 80→25초 |
| PACL2 손실 압축이 켜진 맵에서 원본 RGBA 를 텍스처에 넣었다가 Compress 순간 DXT 로 바꿔 넣음(원본 넣기가 버려짐) | WYSI ALPHA 메인 스레드 넣기 11.5초 | 처음 16장이 원래 길로 모두 압축되면 나머지는 DXT 를 바로 넣음(`TexCompress.DirectAllowed`, LoadTexture 의 maxSideSize 가 있으면 원래 길). 넣기 11.5→3.0초, 열기 30.1→21.7초, 바로 넣은 1363장 모두 뒤이어 압축 불림 |
| 깨진 이미지를 장식마다 다시 읽음 (게임은 실패한 이미지를 기억하지 않음) | ALPHA Arche 깨진 3장을 70번, 메인 스레드 7.2초 | 이번 불러오기에서 깨졌다고 확인된 파일은 1바이트 표시로 바로 실패(`ImagePrefetch.BrokenMarker`). 73.3→66.2초. 안내창에는 "알 수 없는 오류" 대신 "깨진 파일"(`MarkBrokenResult` + 안내창 만드는 동안만 `RDString.Get` 바꿈) |
| 이벤트로 바꾸기(new LevelEvent 13만 7천 개)를 한 스레드에서 | Arche 3.85초 | 작업 스레드가 미리 만들고 원래 반복문은 순서대로 받음(`ParallelDecode`, 메인 스레드 전용일 수 있는 두 값은 미리 구함). 2.5~2.9초(GC 할당 잠금으로 1.4배). 대조 6,634개 다름 0 |
| 이미지 장식 프리팹 복제가 하나 약 100us | Arche 2만 8,835개 약 4.2초 | 연달아 나오는 이미지 장식 구간을 `InstantiateAsync(프리팹, 개수, 부모)` 로(`DecoBatch`, 형제 순서 유지). 장식 단계 9.1→7.85초 |
| 고급 필터 `ResetFilters`("다른 필터 끄기")가 쓴 필터마다 `Type.GetType("이름, Assembly-CSharp-firstpass")` | 한 번 0.7ms, 효과 하나 2~18ms. HELLO 2026 35초 한 프레임 22ms | 결과 기억 (`TypeCache`) → A/B 4판: 곡 중 최악 48~55→24ms, 재생 시작 3.3~3.6→1.9초 |
| 판정 오차 막대 눈금이 사라질 때 `Image.DOColor` 로 매 프레임 눈금 그림을 다시 만듦(최대 60개) | HELLO 2026 자동 플레이 | 정점 색은 그대로 두고 CanvasRenderer 투명도만(`HitMeterFade`, 화면 투명도 같음). A/B 4판: 232·234 → 254·255 FPS (+9%) |
| "드라이버 쪽" 끊김 PLUM MEGAMIX 566초 36ms (첫 판만): 맵 연 뒤 VRAM 89%(게임 6GB). 숨어 있던 장식이 처음 보일 때 VidMm 이 밀려나 있던 그림을 다시 올리고(PageInOneAllocation) 다른 것을 내보내느라(Evict) GPU 가 멈춤 → 출력 대기열이 차서 메인 스레드가 28ms 대기(dxgkrnl SignalPresentLimitSemaphore 가 깨움). GPU 19ms 라 "게임 처리"로 잘못 분류돼 VRAM 학습이 안 걸렸다 | PerfView ThreadTime + `tools/etwstall` | 맵 연 뒤 VRAM 88%↑(절반 넘게 게임 몫)이면 다음 열기부터 한 단계 줄임, 이 이유로는 2048 까지만(`VramGuard.AfterOpen`). 3072: 36·33ms → 2048: 20·23ms |
| 한 프레임에 효과 수십 개 몰림 | | 프레임당 예산으로 분산 (`EffectBudget`) |
| 박자마다 60~80ms (28~40초 구간) | PerfView: 끊긴 60ms 동안 게임 전체 CPU 16ms, 메인 스레드는 `RenderOffscreenCameras` → `CullScene` → `ujob_wait_for`에서 잠듦. GPU도 대기. VRAM 7.0/8GB, 게임 공유메모리 599MB로 넘침 | **Steam 실행 옵션 `-force-d3d12 -force-gfx-jobs native` 제거** (D3D11). 그 구간 끊김 사라짐. 모드는 옵션이 있으면 경고만 띄운다 |

## 측정에서 배운 것 (반복하지 말 것)

- **화면 고정 필터는 장면과 같이 옮기지 않는다 (연구20장)**: HELLO20초 WideScreenHV 한 곳의 실제 입력/출력에서 테두리0→좌우49·상하28px, 밝음→검음278,992px를 확인했다. Size0.68/Smooth0/StretchX=Y=1에서는 원래 material로 흰 그림을 그린 마스크×입력과 출력이 전체픽셀 오차0이다. 이 분해는 해당 설정·표본 근거이며 다른 필터·설정까지 일반화하지 않는다. GPU 복사는 표본에서만, 읽기는 native 종료 뒤로 분리했다. 연결은 연구·기본false이며 Main.Unload와 Finish finally에서 해제한다. 오류0은 StutterFix 집계이지 기존 DOTween 경고까지 없는 뜻이 아니다.
- **필터 테두리의 재투영 (연구19장)**: HELLO20초 원본 안의 검은 테두리49px가 생성에서51~57px로 움직이는 픽셀 근거를 얻었다. CPU 셰이더 근사와 GPU가 전체 픽셀 오차3 이내로 맞았으며 밝은 CLAMP가 BORDER로 검어진 표본은0개였다. 지연 보간만으로 화면 고정 필터 부분까지 옮기는 문제는 해결되지 않는다. 실제 카메라 rect·quad는 전체 화면이고 WideScreenHV가 활성이다. 필터 입출력 대조 전에는 필터 원인을 확정하지 않는다. 지연 진단은 실제 지난 이미지 슬롯을 저장하고 시작 전 곡 시간/되감기를 구분한다. 새2배 완주도 Arche67ms 게임 효과55.8ms·출력42.6ms가 남아 완주를 끊김 없음으로 쓰지 않는다.
- **FPS 자동 기본값 (연구 18장 후속)**: `OverlayFpsSource=-1`은 생성 켬에 출력·끔에 원본을 표시한다. 기존 수동 원본 0·출력 1·둘 다 2의 저장 값은 유지하고 생성 전환 때 덮어쓰지 않는다. 일반 빌드 6개 표시 상태와 새 기본 꺼짐 판에서 연결·생성·런타임 초기화 0을 확인했다. 읽기는 기존 4Hz 갱신 안에서만 한다.
- **재투영 가장자리 (연구 18장)**: 현재 sampler는 BORDER라 저장 그림 밖 UV가 검게 나온다. 카메라 되돌림이 0이어도 새로 드러난 화면 밖 내용까지 복원한 것은 아니다. 사용자가 본 가장자리 흔들림에서 이 문제가 차지하는 정도는 아직 확인 안 됨이며 별도 픽셀 근거 없이 원인 확정이나 품질 통과로 쓰지 않는다.
- **출력 FPS 모니터와 카메라 되돌림 (2026-10-08, 연구 18장)**: 출력 FPS는 선택 배율×원본이 아니라 성공한 native 원본/생성 Present 누적 수의 차이를 실제 시간으로 나눈다. 기존 4Hz 글자 갱신에서만 읽고 꺼짐에는 관찰 연결을 설치하지 않는다. 둘 다 표시는 같은 시간 구간의 Unity 수를 함께 쓴다. FPS·GPU·완주·정지 캡처가 좋아도 움직임 성공은 아니다. HELLO의 native 표시 상수에서 게임 shake·줌·회전 없는 단순 질주만 고르면, 예측의 다음 진짜 출력에서 뒤로 바로잡는 표본 17/147(최대 18.56px)이 남았다. 한 진짜 그림을 늦춰 이미 아는 카메라 사이를 보간하고 원래 Present도 같은 지연 시점을 쓰는 연구 후보는 0/149였다. 표시 지연·다른 움직임·필터·전체 품질을 별도 검증해야 하며 일반 빌드로 승격하지 않는다. 실제 20초 world/screen 쌍의 중앙 차이 후보 0%로 전체 UI 오분류 가설의 해당 표본 근거는 없었다. ResizeBuffers 때는 멈추고 요청을 기록하며 재연결 우회를 쌓지 않는다.

- **확대 필터와 저장 화면의 픽셀 크기 (2026-10-08, 연구 17장)**: FSR은 작은 camRT를 이미 화면 크기로 늘려 게임 출력 quad에 넣는다. 프레임 늘리기에서 그 quad를 다시 camRT 크기로 저장하면 재확대로 장면 픽셀이 달라져 장면 가장자리를 UI로 잘못 고정할 수 있다. 저장 RT를 최종 Screen 크기로 맞춘다. HELLO FSR 50%의 50초 이후 같은 원본 world/screen 쌍을 비교한 CPU 분류 근사: 차이 후보 전체 16.20→4.17%, 중앙 6.82→0%. 실제 GPU UI 마스크의 전수 검증으로 해석하지 않는다. 추가 GPU 읽기는 성능 구간 뒤에만 한다.

- **일반 빌드의 꺼짐 대조 (2026-10-08, 연구 17장)**: 꺼짐에서 Present를 세려고 연결을 설치하면 기본 꺼짐의 실제 경로를 바꾼다. 명시적 상태 명령 두 번의 Unity 프레임 번호/Stopwatch로 원본을 재고, 꺼짐 출력은 추정임을 구분한다. 활성 출력만 native 성공 누적 수를 읽는다. 비교 중 120→144Hz가 바뀐 판은 같은 해상도여도 합치지 않는다. EnumDisplaySettingsEx의 실제 활성 화면 모드 목록에 3440×1440이 없으면 현재 게임의 3440 검증은 미완료다.

- **실험 설정의 기본 꺼짐과 배율 일반화 (2026-10-08, 연구 17장)**: Mono의 `MethodHandle.GetFunctionPointer` 예열은 사용하지 않는 클래스의 정적 초기화까지 실행했다. 프레임 늘리기의 무거운 런타임과 중첩 타입은 JitWarm 대상에서 제외하고, 꺼짐 상태의 실제 `runtime_initialized=False`, native 연결 0, 생성 0을 게임에서 확인한다. 같은 출력 식 `원본 주기/(N-1)`을 다른 정수에 쓸 수 있지만 원하는 배수를 무제한 보장하지 않는다. HELLO·Arche 3/5배는 약 3.01/4.95~5.03배, 8배는 약 7.69~7.93배이며 원본 FPS 손실 10%를 넘었다. 동기 0은 활성 생성에만 적용하고, 꺼짐의 원래 동기 값을 보존한다. HalfRender가 유지한 오래된 카메라를 새 원본으로 읽으면 다음 렌더의 예측 속도가 두 배가 될 수 있어 프레임 늘리기만 중단한다. 이는 기존 흔들림의 확정 원인이 아니다. 사용자 승인으로 일반 빌드에도 기본 꺼짐으로 추가하며 계측·캡처와 큰 기록 배열·GPU 시간 쿼리는 연구 빌드에서만 켠다. 3440 게임 검증과 릴리스 승인은 별개다.

- **실제 화면 조건과 반복 비교 (2026-10-08, 연구 16장)**: `Win32_VideoController`는 어댑터마다 3440×1440을 보고했지만 실제 바탕화면 `Screen.AllScreens.Bounds`와 게임 로그는 2560×1440이었다. 드라이버 보고만으로 게임 시험 해상도를 확정하지 않는다. 끔→2배→4배→4배→2배→끔처럼 앞뒤에 대조를 두고 판마다 새로 켜며, 모든 표본을 경과 시간으로 합친다. 원본 FPS의 콜백 누락 범위는 통계적 신뢰구간이 아니다. 움직임 캡처의 생성/진짜 표본 비율은 읽기 시각에 좌우되므로 실제 출력 배율로 해석하지 않는다. 밖에서 볼 자료는 연구 브랜치의 고정 커밋 원본 링크도 남기고 실제 접근을 확인한다.

- **유니티 바깥 출력의 잠금·시계·흔들림 (2026-10-08, 연구 15장)**: offscreen camRT 렌더까지 잠그면 생성 기회 대부분이 막힌다. 첫 화면 대상 카메라의 onPreRender에서 렌더 스레드 이벤트로 잠그고 원래 Present 뒤 풀어, offscreen 렌더와 출력이 겹치게 한다. 새 원본마다 생성 시계를 다시 시작하거나 잠금을 기다리기 전 시각으로 판단해도 출력이 사라진다. 원본 출력 1회를 빼고 생성 주기를 `원본 주기/(배율-1)`로 잡아 시계를 이어 간다. 기존 방식의 '누락'은 만들었다가 버린 그림이 아니라 실행하지 못한 예정 생성 기회다. 잠금 구간·다음 원본 도착·그 밖 지연을 나누며, 그 밖을 OS 타이머 하나의 원인으로 단정하지 않는다. 카메라 값은 실제 카메라 렌더 완료 때 저장한다. 게임의 `pos+shake`를 그대로 속도로 예측하면 임의 흔들림까지 증폭한다. 흔들림을 뺀 이동만 예측하고 마지막 진짜 흔들림은 유지한다. 박자 확대는 남은 선형 진행을 끝값에서 제한하고 실제 카메라 크기로 정규화한다. 곡 시계는 재생 중에도 프레임 사이에 짧게 뒤로 갈 수 있다. 카메라 속도는 단조 증가하는 원본 프레임 시각으로 나누고 순간 이동은 지속 속도로 읽지 않는다. 모니터가 꺼지면 이 PC가 2560×1440 120Hz로 바뀔 수 있으므로 매 판 실제 화면 조건을 기록한다. 출력 제출 배율·끔 대비 증가·물리적 표시 FPS를 구분한다. 완주 뒤 다시 하기는 메뉴로 돌아갈 수 있어, 안전 시험은 배율마다 새 실행과 실제 곡 끝 로그로 확인한다.

- **유니티 바깥 출력, 공 포함 (2026-10-07, 연구 14장)**: 공·꼬리·빛·필터까지 진짜 장면 그림에 저장해 카메라로 함께 옮긴다. 공 자체의 상태는 진짜 프레임에서만 갱신된다. 게임 원래 스왑체인과 보통 Present 호출 연결을 사용하고, 렌더 시작 플러그인 이벤트부터 진짜 Present 끝까지 잠근다. 배율 설정 2/4를 실제 출력 배수로 읽지 않는다. 함수표 연결에서 그리기 호출을 하나도 못 봤다면 '위반 0회'도 안전성 근거가 아니다. 그리기 함수 가로채기·MinHook·vendor는 제거했으며 안전 확인은 맵 전체 완주와 캡처로 한정한다. 캡처는 곡 50초 이후에만 하고 성능은 5~45초에서 잰다. 카메라 완료 콜백 누락이 있으면 Unity 프레임 끝 수를 정확한 새 장면 FPS로 단정하지 않는다(Arche 전체 실행 61회, 시점·이유 미확인). 다시 하기 뒤 모니터의 경과 시간이 이어질 수 있으므로 conductor 곡 시계가 다시 시작한 구간으로 나눠 비교한다. 연구 빌드에만 넣고 원본 설치 세 파일을 복원한다.

- **게임 안 프레임 생성 2단계 (2026-10-07, 연구 13장, 실험 기본 끔)**: 실제 엔진은 6000.3.21f1이라 기존 6000.3.10f1 주소 기반 GfxProbe가 거부됐다. 연구 연결은 임시 DXGI 스왑체인의 Present 함수표에서 실제 게임 창을 찾고, 렌더 스레드에서 상태를 보존해 합성/Present(0)한다. 입력·판정·카메라·행성 로직은 매 출력마다 유지하고 무거운 장면 카메라만 2/4프레임마다 그리므로, 출력/원본 비율 2/4를 끔 대비 성능 증가로 읽지 않는다. 메인 스레드가 멈추면 이 생성도 멈춘다. camRT와 최종 화면은 방향/출력 재질 차이가 있어 직접 픽셀 차이를 UI로 잡으면 96~99%를 덮는다. Overlay AfterEverything도 UI가 이미 담겼다. 별도 RT에 게임 출력 재질을 그린 UI 없는 그림과 비교하고 진짜 프레임 UI 픽셀을 고정했다(캡처의 선택 영역 RGB 차이 0, 전체 HUD/필터 호환은 미확인). 행성의 검정/흰 바탕 합성은 8비트 흰 바탕이 가산 효과를 잘라 ARGBHalf로 바꿨다(Arche 흰 결과 최대 1.225). Unity native RT는 typeless일 수 있어 SRV에 R16G16B16A16_FLOAT를 지정한다. conductor의 곡 시계는 카운트다운에 이전 값을 갖고 음악 시작 때 0으로 돌아간다. 마지막 음악 구간의 5~45초만 비교하고, Present/Unity 번호와 2/4 간격·GPU 쿼리 전수 회수를 확인한다. 원본 관리 DLL뿐 아니라 내장 네이티브 DLL이 추출해 바꾸는 설치 sfnative.dll·Settings.xml도 바이트로 복원한다. 연구 전용 소스 사본에만 연결하고 정식 빌드·기존 테스터 ZIP은 보존한다.

- **프레임 생성 추가 실험 (2026-10-07, 연구 12장)**: 3440×1440, Present(0)+tearing, 합성 장면 원본 400 FPS의 4배는 1593.47 Present/s·원본 399.60 FPS·생성 GPU 평균 0.101ms. 200/100에서도 원본 유지. 이는 출력 제출률이며 물리적 표시 FPS·게임 GPU 비용은 확인 안 됨. GPU timestamp/disjoint는 미완료 값을 기다리지 않고 DONOTFLUSH로 회수한다. 게임 상태는 WaitForEndOfFrame 숫자 버퍼→종료 CSV; 본 카메라 post-render 프레임과 대조. UI는 layer 0/5의 Overlay Canvas와 WorldSpace 표시가 섞이므로 레이어 5만 분리하면 빠진다. Overlay 루트 14개를 임시 카메라로 옮겨 투명 UI 그림은 얻었지만 레이아웃 재계산/반올림으로 RectTransform 76개 차이가 남았다(Canvas·layer 복원 차이 0). 이 방법을 정식 기능으로 쓰지 않는다. 카메라 depth만 보고 Overlay가 항상 지난 camRT를 낸다고 단정하지 않는다(재생 표본에서는 본 카메라→Overlay). ReadPixels·PNG 저장으로 약 1초 진단 끊김이 생기므로 성능 표본에서 구분한다. 이번 코드와 측정 DLL은 정식 빌드·테스터 ZIP에 넣지 않으며 기존 ZIP을 보존한다.

- **프레임 생성 1a·0단계 (2026-10-07, `tools/framegenlab`, `docs/framegen-research.md` 11장)**: 보통 Windows 대기 타이머로는 144Hz 출력을 맞추지 못했다(5초에 404회 출력, 316칸 놓침). 고해상도 대기 타이머 뒤 2/3/4배의 관찰한 연속 표시 간격 약 6.95ms. GPU UV 변환 32카메라·512점은 CPU 계산과 허용 오차 1/255 안. DXGI 표시 통계의 관찰에서 빠진 Present 번호를 모두 드롭으로 세거나 간격 역수를 전체 표시 FPS로 단정하지 않는다. **PresentMon 검증 통과와 구분**한다(실시간 CSV 없음, DXGI ETL 오프라인 Present 파싱은 됨). 게임 입력 지연·GPU 시간은 미검증. HELLO 초반(`wait 45`, 시작 5초 제외) CPU 스크립트 단계 41.6% / 그리기 준비 48.4%, Arche 32.3% / 56.3%. 두 코어 + 부하(duty 0.65, 코어별 한 프로세스, 게임과 같은 High 프로세스 우선순위 + 부하 스레드 +2)로 HELLO 64.4 FPS: 스크립트 19.4% / 그리기 30.2% / 대기 43.5%. CPU affinity는 모든 게임 스레드를 묶어 작업 스레드 대기를 늘리므로 낮은 클럭과 같지 않다. Windows CPU job 상한 시험은 게임이 응답하지 않아 제외·제거했다. 스크립트 단계에는 그리기 준비도 섞여 순수 로직으로 건너뛸 수 있는 몫이 아니다. PlayerLoop 맨 끝→첫 표시의 바깥 시간도 합쳐 프레임 전체와 일치시켰다. 측정 전용 PlayerAuto는 소스 사본에만 연결하며 정식 빌드·테스터 ZIP에 넣지 않는다. 원본 DLL·설정 사본은 디스크에도 먼저 남겨 강제 중단 뒤 복구할 수 있게 한다. 목표는 카메라 재투영 + 행성만 다시 그리기이며, 독립 시험 ZIP을 실제 게임에 통합된 기능으로 표시하지 않는다.
- **호출이 많은 함수에 Stopwatch를 감싸면** 측정 비용이 실제 비용을 덮는다. `ColorFloor`를 범인으로 잘못 짚었다. 차단 A/B나 할당량 측정을 쓴다.
- 곡 중에는 GC가 멈춰 있어서 **힙 증가량 = 할당량**이다. 할당 추적은 이 성질로 깨끗하게 된다.
- `Time.deltaTime`은 0.333초에서 잘린다. 프레임 시간은 Stopwatch로 직접 잰다.
- **일시정지 중에는 게임 시간이 0**이다. 실시간 타이머는 `Time.unscaledDeltaTime`. 이걸 몰라서 GC가 영영 안 풀렸다.
- 곡 시작 시 Harmony 패치 수백 개는 **4초**가 걸린다. 진단 도구가 초반 끊김의 정체였다. `SlowScan`은 기본으로 꺼 둔다.
- `AccessTools.DeclaredMethod`는 오버로드가 있으면 예외를 던진다. 이름으로 `GetMethods` 해서 하나씩 감싼다. 이것 때문에 설치가 절반만 된 채 측정한 적이 있다.
- 필터는 박자마다 토글되므로 "끊긴 순간 필터가 바뀜"은 **상관이지 인과가 아니다**. 필터를 다 꺼도 끊김은 그대로였다.
- `scrController.currentState`는 이 버전에서 재생 중에도 `None`이다. 종료 감지는 실제 종료 함수(`OnLandOnPortal`, `FailAction`, `QuitToMainMenu` 등)를 가로챈다.
- 이 릴리스 빌드는 유니티 내부 계측점(Recorder)이 거의 막혀 있다(44개 중 쓸 만한 것 없음). GPU 시간은 PresentMon으로 잰다 (`MsGPUBusy`).
- 고급 필터(`ffxSetFilterAdvancedPlus`)가 길이 있는 효과에서 정수 필드를 0부터 트윈해서, `CameraFilterPack_Blur_Movie.OnRenderImage`가 그동안 매 프레임 0으로 나누기 예외를 던진다(원래 게임 버그, Windflower 60초에 약 420번). 예외만 삼키는 수정으로 A/B 4판: FPS 278/277 같음, 곡 중 최악 29~30ms 같음. **성능 영향 없음, 넣지 않았다.** 던지기 전에 막는 수정(원래 코드의 예외 직전까지만 따라 함)도 해 봤다: 예외 0번이 됐지만 Windflower 17.2초 31ms 프레임은 그대로였다. 그 프레임은 예외도 셰이더(미리 데움 93개)도 아니고 엔진 단계 바깥이다(드라이버/그래픽 메모리 쪽, PerfView 로 볼 것).
- 장식 "가볍게 다시 설정"(`ExitFix`)은 **ResetScene 시점에서만** 맞다. ResetScene 쪽을 건너뛰고 재생 준비 끝(`FinishCustomLevelLoading`)에서 재생 시작 때 찍은 값으로 가볍게 하면 그 사이 바뀐 장식 변환을 놓친다(검증: Windflower 10개, HELLO 2026 180~190개 회전·위치·크기 다름).
- **ALPHA 맵 3개 (2026-10-04, 테스터가 준 더 무거운 판, `D:\얼불춤 맵 파일\ALPHA`)**: 셋 다 깨진 PNG 가 있어 원래는 맵이 안 열렸다(위 표). 플레이어용 곡 전체:
  WYSI EX(이미지 1520장, 자동 2048) 358 FPS, 곡 중 최악 26ms, 끊김 0, 열기 90초 / Finale Destination(1288장, 2048) 430 FPS, 15ms, 끊김 0, 열기 80초 /
  Arche CDF(10442장, 자동 1024 에서도 VRAM 7.85/8GB) 323 FPS, 끊김 2(19.5초 33ms, 294.2초 92ms, 둘 다 엔진 단계 바깥 = 드라이버 쪽 VRAM, 294초는 개발자용에서 재현 안 됨), **열기 487초**.
  Arche 열기: 이미지 해독이 작업 스레드 합계 602초인데 5개가 평균 1.3개만큼만 일함(메인이 374초 기다림). D: 는 USB SSD 라 디스크 탓 아님. 원인 미확인.
  Arche 를 768 로 더 줄이면 VRAM 은 맞지만 화면이 흐려진다(사용자 결정 대기).
- **로그의 단계 시간 합이 전체와 안 맞으면 빠진 단계부터 찾는다.** ALPHA 맵 열기에서 "해독 시간"(풀기+필터 602초)만 보고 작업 스레드가 1.3개만 일한다고 잘못 읽었다. 실제로는 로그에 없던 C# 줄이기가 작업 스레드 시간의 78% 였다.
- 큰 이미지 줄이기를 바꾸면 `tools/PngCheck` 로 실제 PNG 를 원래 길과 바이트 비교한다(ALPHA 3개 + Windflower·HELLO 2026·Arche 14,710장). 줄 처리 단계별 시간은 sfnative 에 임시 계측(QueryPerformanceCounter)을 넣어 오프라인으로 잰다.
- **일반 무거운 맵 열기 나눠 보기 (플레이어용 Arche, 2026-10-05)**: 열림 약 23초 = 편집기 전환 등 + 맵 파일 읽기 약 4초(그중 이벤트로 바꾸기 3.85초 → 여러 코어 2.5~2.9초, `ParallelDecode`. 5 스레드인데 1.4배: Mono GC 할당 잠금) + 장식 만들기 2만 8,835개 8.8초(텍스처 불러오기 2.5초, Setup 약 2.0초, 나머지 약 4.2초는 프리팹 복제 - 장식 하나 약 100us, 갈수록 느려지지 않음). `InstantiateAsync(프리팹, 개수, 부모)` 로 한 번에 복제해도 100→77~98us(20%)라 넣지 않았다. 개발자용 [시작시간]의 두 번째 `ResetDecorations` 는 개발자용 검증(`LoadFix.VerifyOpen`)이다.
- **측정 중 게임을 강제로 끄면(taskkill) 비정상 종료로 세어** 연속 7번이면 모드가 안전 모드로 켜진다. 안전 모드는 이미지 줄이기도 꺼서, ALPHA 처럼 이미지 원본이 100GB 인 맵은 메모리 부족으로 다시 꺼지는 악순환이 된다. 로그 `[안정성] 안전 모드로 켬` 을 먼저 확인하고, 깨끗하게 한 번 끄면(`sf_quit`) 풀린다. 메모리 상한 감시로 끌 때도 같다.
- **모드 다시 불러오기(reload) 뒤 무거운 맵을 다시 열면** 메모리가 두 배로 올라 꺼졌다(WYSI ALPHA 19GB). 무거운 맵 A/B 는 판마다 새로 켜는 `sf_ab` 로.
- 같은 게임에서 `ImageMaxSide` 를 자동(-1)으로 되돌려도 같은 맵은 그 실행에서 정한 한도(`VramGuard.CurrentCap`)를 다시 쓴다. 한도 A/B 는 값을 둘 다 숫자로(3072/2048).
- 맵 열기는 한 프레임(100초 넘게)이라 메인 스레드 로그로는 단계가 안 보인다. 자동 시험이 열기 중 3초마다 다른 스레드에서 힙·프로세스 메모리(개발자용은 `StartProbe` 단계 이름도)를 적는다.
- **켜 둔 게임 다시 쓰기**: `sf_live`(묶음 하나, 게임 계속 켜 둠, `settings` 는 게임 안 `set` 으로 바꾸고 묶음 끝에 되돌림, `reload` 는 새 DLL 일 때만 Ctrl+F5) · `sf_ab_live`(첫 판 버림, ABBA, 실패하면 새로 켜서 그 판만) · `sf_quit`. 재시작이 필요한 설정과 최종 확인은 `sf_run`/`sf_ab`. 같은 DLL 을 다시 불러오면 Mono 가 같은 어셈블리로 여겨 패치가 깨진다(그래서 건너뜀). MCP 를 다시 붙이기 전에는 `python -c "import server as S; ..."` 로 직접 부른다. `seek <곡 초> [앞 여유=8]` 은 그 시점 조금 앞 타일부터 재생(곡 시간은 재생 시작 기준으로 로그에 찍힘).
- 자동 시험 `game <맵>` / `press` / `retry`: 커스텀 맵 목록에서 연 것처럼 게임 화면(scnGame)에서 시험한다. 게임 화면은 곡 시작과 다시 하기 뒤에 키 누름(`press`)이 필요하다.
- **개발자용에서 재현되지 않는 끊김**은 측정용 플레이어 빌드(`Main.MeasureBuild = true`, 배포 전 false)로 잰다. Windflower 94.7초 끊김은 개발자용에서 안 났고 측정용 빌드로 원인을 잡았다(엔진 단계 → 모든 LateUpdate 를 프레임마다 합쳐 재기 → `FloorMesh.UpdateAllRequired`).
- 모드 폴더에 `startprobe-cold.txt`를 두면 dev [시작시간]이 자주 불리는 함수를 감싸지 않아 단계 나눔이 정확해진다.
- **타일 이동 효과(MoveTrack, `ffxMoveFloorPlus`) 하나가 45ms** (Lost Requiem 531.9초, 타일 1.5만 개 맵). 범위 안 타일마다 DOTween 을 새로 만든다(타일당 약 3µs, 그 순간 살아 있는 트윈 2만 1천 개). 효과 하나라 몰림 나누기(`EffectBudget`)로는 못 막는다(진단: `[효과나누기] 무거운 효과`, 개발자용). 길이 0 즉시 이동(7777 타일 0~8903, 10~20ms)은 **트윈을 하나도 만들지 않는다**(위치가 이미 같아 건너뜀). 비용은 타일마다 위치·회전·크기 읽기와 비교(타일당 2.1µs, 엔진 호출). ZeroTween 대역을 타일에도 걸어 봤지만(대조 111개 다름 0) 곡 전체에서 97~6910개만 탔고 무거운 효과 시간은 그대로라 넣지 않았다. → (2) 모드가 직접 돌리기로 해결(`FloorAnim`, 위 표). (1) 나누기는 저사양 옵션(`LowFloorSplit`, 기본 끔): 1000개 넘는 효과를 가까운 타일부터 프레임 예산만큼, 늦게 만든 애니메이션은 지난 시간만큼 당겨 둠. 같은 프레임에 타일 이동이 여러 개 오면(LR 531.9초 3개) 전부 끝내 버리면 소용없어서, 다른 효과가 건드린 타일만 원래 순서대로 먼저 만든다. 거리 순 정렬은 람다 정렬이 4ms 라 지금 타일 자리에서 양쪽으로 펼치는 O(n) 으로. 남은 비용(타일당 약 2.5us)은 transform 읽기·쓰기 같은 엔진 호출이다. 처음 만든 FloorAnim 은 꺼진 DOTween 객체를 `AccessTools.CreateInstance` 로 만들어 하나 약 2us 였다(효과 하나 7천 개) → DynamicMethod 생성자 호출기로. 모드 표의 Kill 은 Harmony 패치 여러 겹을 거치지 않고 바로 처리, 끝난 진짜 DOTween 에는 Kill 을 안 부른다(DOTween Kill 은 active 가 아니면 바로 돌아옴).
- 곡 전체 조사(게임 화면 경로, 2026-10-04 밤): Timeline 6ms, Battle Against 20ms, scam 21ms, 7777 26ms, HELLO 2026 27ms, DDONGSSADA 36ms, PLUM MEGAMIX 39ms, Windflower 41ms, Arche 42ms(5.9초 효과 몰림), Lost Requiem 54ms(타일 이동). 사용자가 자는 동안 모니터가 꺼져 화면이 2560x1440 120Hz 로 바뀐 채 잰 판이 있다(FPS 비교는 같은 조건끼리). 타일 모양 미리 만들기(`MeshWarm`) 뒤: HELLO 2026 27→24ms, Windflower 41→29ms(끊김 1→0), Lost Requiem 272·283초 34ms 프레임 없어짐(531.8초 타일 이동 55ms 만 남음), PLUM MEGAMIX 526초 38ms 없어짐. 남은 것: 타일 이동 효과 하나(결정 대기), 엔진 단계 바깥(드라이버 쪽: Windflower 17·22초 29~31ms, PLUM 567초 36ms, PerfView 관리자 권한 필요).
- HELLO 2026에서 곡이 갈수록 FPS가 떨어지는 것(245→140)은 **맵 내용 탓**이다. 타일 2500에서 시작해도 첫 10초가 172 FPS, 5000에서 시작하면 145→191. 쌓이는 문제 아님. "곡이 갈수록 느려짐"은 중간 타일에서 시작(`select <타일>` + `play`)해서 가린다.
- dev 빌드 [시작시간]은 `Enum.ToObject` 같은 많이 불리는 함수까지 감싸 부풀려진다. 맵 열기·재생 시작 시간은 플레이어용+자동 시험 빌드(`-p:Edition=Player -p:AutoTestBuild=1` → `bin/PlayerAuto`)로 잰다.

- **가끔 나오는 UI 캔버스 23~28ms** (`PlayerUpdateCanvases`): PerfView(Lost Requiem, 타일 애니메이션 끔) 로 `UI::CanvasManager::WillRenderCanvases` 안의 C# 코드(절반 가까이 Mono 런타임)까지 좁히고, `Canvas.willRenderCanvases` 콜백마다 재서 **`TMPro.TMP_UpdateManager.DoRebuilds` 22.7~24ms → 판정 글자 `HitTexts/HitTextMesh(Clone)`(3글자, godoMaum SDF) 의 `TextMeshPro.Rebuild` 22.7ms** 로 잡았다. 그 프레임 힙 +2MB(보통 프레임 20KB). 처음 부른 것이 아니다(DoRebuilds 9만 번째). 판정 글자 풀은 판정 종류마다 100개(1,100개). 더 안쪽(GenerateTextMesh·UpdateFontAssetsInUpdateQueue 등)을 감싼 뒤 두 판 연속 재현 안 됨. 측정 코드는 개발자용 + 모드 폴더 `uiprobe.txt`. 타일 애니메이션 직접 처리를 켠 LR 에서는 안 보였고 HELLO 165.4초에 한 번.
- 효과 시간에 **빈 함수(`ffxSetHitsound.StartEffect`)가 7ms** 로 찍힌 적이 있다(HELLO 165.4초, 99번째 사용). 효과 자체 비용이 아니라 그 순간 다른 것(힙 늘리기 등)이 겹친 것. 효과 이름별 시간 하나만 보고 범인을 짚지 않는다.

- **화면 출력 실험 (2026-10-04, PresentMon, HELLO 2026 곡 전체, 3440x1440 165Hz, 수직동기 끔)**: 지금(전체 화면 창 + Flip) 출력은 `Composed: Flip`(윈도우가 한 번 더 합성), 프레임 시작→화면 15.8ms, present→화면 10.8ms, 209 FPS. 앞서 준비하는 프레임 2→1(`MaxQueuedFrames`)은 FPS 209→128, 시작→화면 15.8→19.1ms 로 **둘 다 나빠져** 버림. 독점 전체 화면(`ExpFullscreen=1`)은 `Hardware: Independent Flip` 이 되어 시작→화면 15.8→7.9ms, present→화면 10.8→2.7ms 로 절반이지만 FPS 209→187, 찢어짐 허용(AllowsTearing 1). 둘 다 설정 창에 없는 실험 설정이다. 관리자 PresentMon 은 `C:\Users\Public\StutterFixTrace\agent.ps1` 의 `pm <이름> <초>` 명령(분석 `pmstat.py`).

## 설정 창 디자인 (2026-10-04, `SettingsWindow.cs`)

- 사용자가 고른 방향: **Linear / Raycast** (3D·입체·행성은 해 보고 접음). 거슬렸던 것: 글자가 너무 많음, 상자·테두리가 많음, 배치·크기.
- 색: 아주 어두운 바탕 `101012`, 글자 `EEEFF1` / `8A8F98` / `5E636B`, 선은 흰색 6%, 마우스 올림 흰색 3.5%. 색은 끊긴 프레임 하나(`FF5F45`)에만.
- 배치: 오른쪽 아이콘 줄(여는 단추)은 그대로, 창 안은 `StutterFix / 페이지` 제목줄 + 긴 페이지(플레이·모니터·저사양)는 글자 탭(고른 것 밑에 가는 선). 상자 없이 목록 줄만, 줄 사이 선 없음.
- 기능 줄: 제목 13 + 설명 첫 문장 한 줄(12, 흐림) + 작은 스위치. 나머지 설명은 줄을 누르면 펼쳐진다(`SplitDesc`, '자세히'). 스위치를 누르면 켜고 끈다.
- 한글은 `P()` 가 띄어쓰기 자리에서만 줄을 바꾼다. 모서리는 작은 것 6, 창 12.
- 확인: 자동 시험 `ui <페이지>.<갈래>` + `shot <이름>` 캡처. 사용자 범위 디자인 스킬: taste-skill, redesign-skill, impeccable(바이너리 실행기 없이), emil-design-eng 등, frontend-design, ui-ux-pro-max.

## 사용자 PC

i5-9400F / RTX 4060 Ti / DDR4-2666 24GB / 3440x1440 164Hz / Windows 10 Atlas OS. 다른 모드 9개 동시 사용(Quartz, AdofaiTweaks, XPerfect 등).
`ModWatch`는 다른 모드의 **OnUpdate만** 잰다. Harmony로 게임 함수 안에 끼어든 비용은 전혀 안 잡힌다.
학교 PC에서 TextGenerator 폭주가 티 나지 않은 건 **같은 SSD를 들고 가서** 돌린 것이라 소프트웨어는 같았다. 차이는 하드웨어(더 빠른 CPU/RAM)다.
PACL2가 매 프레임 글자 34개를 다시 넣는 것은 양쪽 모두에서 일어난다.
모드 탓을 가리려면 다른 모드를 전부 끄고(게임 재시작) 같은 구간을 돌려 비교한다.

## 엔진 안쪽 보기 (PerfView)

**끊긴 프레임 하나 보기 (2026-10-05)**: 자동 시험 `trace <이름> <초>` 가 관리자 에이전트에 ThreadTime 기록을 시작시키고(에이전트가 받을 때까지 기다림), 측정용 빌드의 끊김 줄에 "시각" 이 찍힌다.
PerfView 가 만든 etlx(%TEMP%\PerfView\<이름>_*.etlx)를 `tools/etwstall` 로 본다: `EtwStall <etlx> <PID> <HH:mm:ss.fff> [앞ms] [뒤ms]` → 스레드별 잠든 구간·이유·깨운 스레드와 그 스택, CPU 샘플 스택. PID 4(System)로 돌리면 VidMm 같은 커널 작업이 보인다.
PerfView `SaveCPUStacks` 는 프로세스 이름을 못 찾거나(게임이 기록 끝 전에 꺼지면) 모든 프로세스 심볼을 서버에서 찾느라 40분 넘게 걸렸다. etwstall 은 로컬 심볼만 써서 5초. 게임 PID 는 기록 안의 것(etwstall ETW_PROCS=1)으로. Git Bash 에서 PerfView 를 부르면 `/AcceptEULA` 가 경로로 바뀐다(PowerShell 로).


C# 측정으로 안 보이는 끊김(엔진 네이티브, 드라이버, OS)은 PerfView로 본다. 도구와 결과는 `C:\Users\Public\StutterFixTrace`(한글 경로에선 PerfView 기록이 실패했다).

```
PerfView.exe /AcceptEULA /NoGui /NoView /Zip:false /Merge:true /MaxCollectSec:120 /CircularMB:3000 /KernelEvents:Default /ClrEvents:None /LogFile:...\collect.log collect ...\stall.etl   (관리자)
PerfView.exe /AcceptEULA /NoGui /LogFile:...\save.log UserCommand SaveCPUStacks ...\stall.etl "A Dance of Fire and Ice"
```
- 유니티 심볼은 PerfView가 서버에서 안 받아와서 직접 받았다:
  `https://symbolserver.unity3d.com/UnityPlayer_Win64_player_mono_x64.pdb/<GUID+Age>/UnityPlayer_Win64_player_mono_x64.pdb` → `symbols\` 캐시 구조로 둔다.
- 결과 XML에서 메인 스레드(CPU 가장 많은 Thread)의 샘플 사이가 크게 벌어진 곳 = 무언가를 기다리며 잠든 곳이다.
  그 순간 게임 전체 CPU가 거의 0이면 원인은 게임 바깥(드라이버, VRAM 이동, OS)이다.
- 끊김 로그의 실제 시각과 기록 시작 시각을 빼서 위치를 맞춘다.
- VRAM 확인: `Win32_PerfFormattedData_GPUPerformanceCounters_GPUProcessMemory` (게임의 SharedUsage가 0보다 크면 VRAM이 넘친 것).

## D3D12 곡 중 멈춤 (조사 기록)

- ThreadTime 기록(CSwitch/ReadyThread, `tracerpt`로 CSV 변환)으로 확인: 멈춘 60~70ms 동안 **메인 스레드가 커널 대기(이유 0, Executive)**,
  하드웨어 인터럽트(스레드 0)가 깨움. 작업 스레드 6개는 메인 스레드를 기다림(이유 37).
- 잠들기 직전 스택: `RenderOffscreenCameras → ParticleSystemGeometryJob::ScheduleJobs → DynamicVBOBufferManager::AcquireExclusive
  → D3D12DynamicVBOScratchMemory::Reserve → GfxDeviceD3D12::ReserveScratchMemorySlow → CreateCommittedResource
  → dxgkrnl CreateAllocation → dxgmms2 VidMm CommitLocalBackingStore`. 곡 중 초당 5~9번 새 그래픽 메모리를 잡는다.
- 공유 메모리 590MB는 넘침이 아니라 D3D12 업로드 힙(원래 시스템 램). 곡 내내 변하지 않았고 복사 엔진도 0%였다.
- 유니티 숨은 옵션(엔진 기계어로 단위 확인): `-d3d12-min-scratch-memory <바이트, 4MiB 올림>`,
  `-d3d12-min-client-scratch-memory <바이트, 1MiB 올림>`, `-d3d12-scratch-release-delay <프레임>`.
  세 개 다 넣어도(64MB/256MB/30000) 해결되지 않았고 프레임이 떨어졌다. 쓰지 않는다.
- `-force-gfx-jobs` 값: off / split / legacy / native. 지원 안 되는 조합은 "is not supported ... Reverting" 로그 후 되돌린다.
- 결과: D3D12+native → 28~40초와 127~130초 멈춤 / D3D12만 → 28~40초 멈춤 / D3D11 → 28~40초 깨끗하나 프레임 140.

## 지운 것: 같은 타일 스타일 건너뛰기 (FloorFix)

타일마다 SetTrackStyle 의 마지막 인자를 기억해 두고 같으면 건너뛰었다(48% 건너뜀).
그런데 **타일 색이 이상해졌고**, legacy 그래픽 작업과 함께 쓰면 125~130초에 1초 간격으로
유니티 루프 바깥에서 60~75ms 멈춤이 생겼다(켬 160번 / 끔 94번, 같은 세션 비교).
"인자가 같으면 결과도 같다"는 가정이 틀렸다. 다른 코드가 그사이 같은 재질 값을 바꾼다. 다시 만들지 않는다.
