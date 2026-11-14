# 직통 렌더러 M2 구현·오프라인 검증 결과

2026-11-15, `E:\StutterFixWork\wt-v14prod`, `claude/adofai-renderer`, 시작 HEAD `51f2731`.
지시서: [05-m2-spec.md](05-m2-spec.md). 기존 M1b/M1c에서 이어서 구현했다.

**일반 Renderer의 단일 패스 제한·중간 정렬 층 제외·소유 이미지 양끝 제한과 반환 렌더러의 경계 연쇄를 제거했다. Canvas/Unity 마스크 예외가 없으면 메인 카메라 Renderer 전체를 하나의 차례로 그린다. 예외가 있으면 명세의 1단계 대안으로 해당 범위를 Unity에 남긴다. 모든 맵에서 경계 0을 달성했다는 결과는 아니다.**

DEV / Player / PlayerAuto / native는 오류 0, export 73개 보존, WARP·후크 IL 검사·정렬/가시성 자료구조 검사는 통과했다. 실게임 drPix·ABBA와 전체 직통 비용 <50µs/프레임·Unity Mono 전체 경로 할당 0은 이 작업에서 실행하거나 합격 판정하지 않았다.

이 세션은 커밋·게임 실행·설치 DLL 복사를 하지 않았다. 다만 작업 중 설치 파일과 게임 프로세스에 동시 외부 변경이 관측됐다. 아래 보존 검사 절을 반드시 확인해야 한다.

## 차례와 소유

구현: [DirectDeco.cs](../../DirectDeco.cs), [DirectDeco.M2.cs](../../DirectDeco.M2.cs), [DirectSequenceChanges.cs](../../DirectSequenceChanges.cs).

- 이미지 장식은 기존 네이티브 인스턴스 구간을 유지한다. 타일·행성·파티클·선/Trail·TMP·일반/SkinnedMesh 등을 같은 목록에 넣는다. Renderer 종류와 단일 패스 여부로 일반 렌더러를 거절하던 제한을 제거했다. 비활성 풀의 빈 재질 슬롯도 준비하며 재질이 생기면 변경 알림으로 갱신한다.
- 남는 렌더러는 `DrawRenderer(renderer, material, submesh, -1)`로 모든 패스를 기록한다. 여러 재질은 각각의 queue/shader/material/submesh 키로 정렬하며, Mesh/SkinnedMesh의 여분 재질은 마지막 submesh에 대응시킨다.
- 정렬은 층 → order → queue → 거리 → shader → material → renderer → material slot의 기존 `SceneRenderOrder` 관계를 따른다. 직교 거리에서 모든 항목에 공통인 카메라 위치 항만 생략했다. 따라서 2D 카메라 이동만으로 전수 재정렬하지 않는다. 방향/정렬 축이 바뀌면 키를 갱신한다. 이 모델 자체의 동점 규칙 한계는 유지된다.
- Canvas/마스크 예외가 없을 때 모든 구간은 `BeforeForwardAlpha`의 하나의 CommandBuffer에 들어간다. Bg/Default 사이의 인위적인 분할도 없앴다. 명령은 그대로 재사용하고 순서·가시성·구조가 바뀔 때만 다시 기록한다.
- 일반 이미지의 정적 실패는 해당 원래 Renderer를 차례 안에 넣는다. Canvas/마스크 예외 때문에 Unity로 반환된 일반 Renderer는 **그릴 수 있음(Seq)** 상태를 보존한다. 반환 자체가 다음 번 경계가 되지 않는다. 예외 범위 밖으로 이동하면 다시 차례에 들어갈 수 있다.

## Canvas 조사와 남긴 예외

기존 `dp6-{hl50,wf,dd,sh}-drPix.log`를 읽었다. HELLO 14,923 / WF 2,226 / DD 5,500 / Exit 2,805라는 수는 이전 코드가 예약한 루트 Canvas 수다. 그 로그에는 각 Canvas의 실제 Graphic·도형·알파·cull 정보가 없으므로, 전부 실제 그리기라고 해석하지 않았다. 이전 기록은 [prior-m1c-history.json](../../out/direct-renderer/m2/prior-m1c-history.json)에 별도로 보관했다.

설치 게임 DLL의 메타데이터에서 `scrFloor.editorNumText`와 `showStatusText`를 확인했다([floor-canvas-api.txt](../../out/direct-renderer/m2/floor-canvas-api.txt)). 타일 번호/상태 글자 계통이 존재한다는 증거이며, 위 Canvas 전부의 정체를 특정한 증거는 아니다.

`dr2 canvases`를 추가했다. 다음을 기록한다.

- 루트/활성 Canvas 수, 활성 Graphic의 실제 타입별 수.
- 빈 mesh/재질 없음, cull, 상속 알파/Graphic 알파 0, 화면 밖, 화면 내 후보 수.
- 화면 내 후보 상위 10개의 루트/Graphic 이름·타입·정점 수·상속 알파.

경계 판정은 `isActiveAndEnabled`, 실제 CanvasRenderer mesh/정점·재질·알파·cull을 확인한다. 빈/꺼진 Canvas 전체를 미리 경계로 예약하지 않는다. 화면 밖 여부는 진단으로 기록하되 위치만으로 영구 제외하지 않는다. UI dirty/cull 콜백·수명 콜백·managed 쓰기를 통해 **바뀐 Canvas만** 다시 판정한다. 생성/재부모화 때만 바뀐 하위 구조를 조사한다.

실제 그릴 Canvas가 남으면 해당 정렬 범위를 원래 메인 카메라에 남기고 그 바깥을 앞/뒤 CommandBuffer로 그린다. 이전에 예약한 범위는 곡 중 임의로 축소하지 않는다. 그 범위에 남긴 일반 렌더러가 또 새 경계가 되지는 않는다. 따라서 state의 `canvasBounds`가 나중에 0이 돼도 과거에 예약한 범위 때문에 `sequence=canvas-mask-islands`가 유지될 수 있다.

**별도 Canvas 카메라/RT 합성은 채택하지 않았다.** 개별 루트의 격리, Graphic/RectMask2D의 clip/stencil, 중첩 Canvas의 순서와 RT 알파 합성까지 보존해야 한다. 투명 RT에 그려 단순 Blit하는 것만으로 원래 UI 합성을 증명할 수 없고, 이번 게임 실행 금지 범위에서는 그 검증을 하지 못한다. 실제 그리는 Canvas의 종류/수는 다음 실행에서 `dr2 canvases`로 확인해야 한다.

## 마스크·Grab

모든 마스크/대상을 네이티브가 표현할 수 있는 경우 기존 enter/leave·inside/outside 스텐실 경로를 유지한다. Unity 마스크가 필요한 남는 대상이 있으면 마스크 소유를 혼합하지 않고 관련 범위를 Unity에 남긴다. 그 범위 안 색 렌더러는 원래 카메라가 한 덩어리로 그리며, 바깥 렌더러만 숨긴다. 외부 마스크의 생성/범위 변경은 예약을 확대하고 혼합 소유를 해제한다. 새 기본 전체 범위 마스크는 소유 대부분을 반환시킬 수 있다. 마스크 자체가 일반 순서 경계 연쇄를 일으키는 방식은 사용하지 않는다.

DrawRenderer 대상의 임의 셰이더에 우리 stencil test를 강제로 주입하는 재질 프록시는 구현하지 않았다. 엔진이 생성하는 SpriteMask 상태를 임의 셰이더의 재질 프로퍼티만으로 대체할 수 있다고 가정하지 않았다. 정렬 층별 별도 카메라도, 마스크 범위가 여러 층을 넘으면 카메라 사이 stencil 공유·clear와 색 합성 순서를 해결해야 하므로 채택하지 않았다. 이는 명세에서 허용한 **1단계 Unity 범위 대안**이며 외부 마스크까지 완전 소유한 결과는 아니다.

알려진 `Hidden/BlendModes/SpritesDefault/Grab`은 차례의 해당 DrawRenderer 직전에 CameraTarget을 임시 RT로 복사해 `_GrabTexture`에 바인딩한다. 실제 카메라 RT의 형식/크기를 사용하고 MSAA는 복사본에서 해제한다. 소비 draw 뒤 RT를 해제한다. 기존 네이티브 이미지 Grab 경로도 유지한다. Unity DrawRenderer·GrabPass·Legacy Diffuse 다중 패스의 실제 결과는 WARP 네이티브 시험만으로 검증되지 않으므로 drPix 필수다.

## 변경 추적과 비용

매 프레임 활성/비활성 Renderer 전수를 확인하던 루프를 제거했다. 저장소의 `LiveDecoChanges` 접근을 DR2 전용으로 분리한 `DirectSequenceChanges`를 사용한다. Unity internal-call setter를 직접 Harmony 패치하지 않고 게임/이 모드의 managed 호출 지점을 래퍼로 바꾼다.

Renderer의 enabled/forceRenderingOff·정렬·재질·마스크, 재질 쓰기, Transform·부모 변경, 풀 꺼내기, Instantiate/AddComponent/Destroy 계통을 알림으로 모은다. Renderer·재질·Transform 조상 색인을 쓰며 프레임 안 중복은 제거한다. 생성/재부모화 때만 해당 구조를 조사한다. 숨김 층의 객체를 복제할 때 숨김 층/네이티브 소유 forceRenderingOff가 새 객체에 잘못 상속되지 않도록 복제본을 복구한다. 원래 GameObject 층과 forceRenderingOff는 종료 시 복원한다.

`dr2 off`는 변경 후크도 해제하므로 다음 대조 판에 활성 후보의 호출 지점 후크를 남기지 않는다. 후크 설치 실패 시 `on`/`now`가 소유를 시작하지 않는다.

새 state: `sequence`, `canvasRoots`, `canvasDirty`, `rendererDirty`, `drawRenderer`, `cb_record_us`, `sequenceGrab`, `mutationMethods`, `mutationFailures`. `cbBuilds`는 누적 재기록 수이며 `cb_record_us`는 재기록 1회 평균이다. 기존 commit 평균/최대, prep, poll 지표도 유지한다.

**비용 한계:** 구조/재질 배열이 실제로 바뀌면 준비 자료구조와 명령을 다시 만들 수 있다. 방향/거리 순서가 바뀌는 많은 Renderer와 Canvas가 동시에 dirty면 비용이 커진다. <50µs나 전체 경로 할당 0을 측정한 결과는 아니다. 제3자 모드의 별도 어셈블리, Unity 네이티브 애니메이션 등 managed 후크 밖의 변경까지 자동 감지한다고 보장하지 않는다. `seqcheck`가 누락/낡은 키를 발견하면 실패로 기록해야 한다.

## `dr2 seqcheck`

`dr2 verify`를 유지하고 `dr2 seqcheck`를 추가했다. 진단 시에만 전수 검색한다. 원래 카메라 층을 복원해 해석하고, 현재 실제 Renderer/재질별 키로 독립 목록을 만든 뒤 실제 차례와 비교한다. native 이미지, 여러 재질, 풀의 고정 슬롯을 포함한다. 허용한 Unity 예외 범위는 따로 센다.

출력: `entries`, `stale`, `missing`, `unregistered`, `unityIsland`, `orderMismatch`, `sameMaterialTies`, `pass`.
`stale/missing/unregistered/orderMismatch=0`이 합격 조건이다. 이것은 `SceneRenderOrder` 모델과의 일치이며 실제 Unity 동일 재질 동점의 화면 순서까지 입증하지 않는다. verify/seqcheck/canvases는 할당을 수반하는 진단으로 성능 판에서 제외한다.

## 네이티브 구간 수

전체 차례는 이미지/남는 Renderer의 교차가 많아져 기존 4,000구간을 넘을 수 있다. 내부 이벤트를 token 15비트 + segment 16비트로 바꾸고 65,000구간을 예약한다. native 패킷 8개와 렌더측 구간 벡터를 준비 때 예약한다. 65,001구간은 commit을 거부하며 token 세대 32,767을 소진하면 재사용하지 않고 create를 거부한다. export/함수 인자/FrameIn 레이아웃은 바꾸지 않았다.

**이벤트 인코딩은 변경됐으므로 관리 DLL과 native는 이번 빌드의 일치하는 쌍이어야 한다.** 관리 DLL 3종에 현재 native 바이트가 내장됐음을 검사했다.

## 검증 증거

증거 디렉터리: [out/direct-renderer/m2](../../out/direct-renderer/m2).

| 검사 | 결과 |
|---|---|
| DEV / Player / PlayerAuto / native | 오류 0. `build-{dev,player,playerauto,native}.log`. 기존 관리 경고 있음 |
| HLSL 9변형 | 컴파일 통과 |
| export 이름/ordinal/hint | 73개 보존. `exports-{before,after,result}.txt` |
| 내장 native | 관리 DLL 3종 모두 현재 로컬 native 포함. `embedded-native.json` |
| WARP 블렌드 | 정상+12모드 × 1/2연속, 26조건 최대 채널 차 2 |
| WARP UV | 5,120픽셀 정확히 일치, 방향/축 교환 통과 |
| sRGB / 영역 복사 | 통과, 8×8 장식의 여백 포함 복사 12×12 |
| 실제 VS 대 독립 TRS | 40조건, 최대 월드 오차 7.62939453e-06 |
| 스텐실 | 빈 segment 0 초기화·inside/leave/outside, 64/1,024픽셀 통과 |
| 준비 | 8패킷/목록/도형 버퍼 사전 확보, 24프레임 버퍼 교체 없음 |
| M2 대규모 구간 | 65,000구간 사전 예약, event 64,999 정확히 1회 draw, 초과/세대 소진 거부 |
| 후크 IL/래퍼 | 2,085메서드·5,468호출 지점·249종 래퍼 검사 통과 |
| 정렬 | 20,000개 키를 독립 8필드 정렬과 비교, 일치 |
| 가시성 | 기존 110,000회 회귀 및 20,000엔트리 순회 통과, .NET 8 자료구조 할당 0 |

후크 검사는 게임/모드 DLL을 읽고 실제 production transpiler의 delegate binding·generic 제약·예외 블록 이동을 검사했다. 패치를 설치하거나 게임 메서드를 호출하지 않았다. 게임 Mono에서의 실제 후크 설치와 수명/Canvas/DrawRenderer 실행 검증은 별도다. WARP는 게임에 연결하지 않는 소프트웨어 D3D11 장치다.

## 설치 파일 보존 검사: HOLD

시작에 기록한 설치 파일 SHA-256과 마지막 검사 값이 달랐다. `installed-before.json`, `installed-after.json`, `final-hashes.json`에 기록했다. `check-m2-artifacts.py`는 이 경우 **exit 2**를 반환한다. 다른 검사가 통과했다고 설치 보존까지 합격 처리하지 않는다.

| 파일 | 시작 SHA-256 | 03:57경 관측 SHA-256 |
|---|---|---|
| 설치 StutterFix.dll | `4CFDAA88F5CD44B1327BEA22BDF7CD1AB36DB8174564A20062F448242A00DF89` | `F393E53014D83CAFA9D586F9A5EB986B81BB5BF4112ABADECE054165F68926A1` |
| 설치 sfnative.dll | `981B336803D1E469578F440F5DFE5B911262B89D4D9DBECFD86404552188CD1D` | `E7A7CF6026892C3082224C2CB2C45D3DC8B2D35F9045DEBE81E1241590ACE05A` |

03:57경 확인한 파일 수정 시각은 각각 03:55:33 / 03:55:47이었고, 게임 PID 10836의 시작 시각은 03:55:37이었다. 그때의 설치 값은 당시 로컬 PlayerAuto/native 산출물과 일치했다. 이후 설치 StutterFix.dll은 다시 `B0EFE68D6CCCE3217310DA0AD65CBA4048C19D2D25F738C97F14D7822898C795`로 바뀐 것이 마지막 읽기에서 관측됐다. 설치 경로의 동시 변경이 계속되고 있으므로 마지막 읽기 시점의 값은 `installed-after.json`을 기준으로 한다. 로컬 관리 DLL에도 예외 범위 갱신 최적화를 추가하고 3종을 다시 빌드했으므로 최종 산출물 해시는 `final-hashes.json`을 기준으로 한다. 이 세션에서 호출한 빌드 스크립트의 복사 목적지는 저장소 `native/sfnative.dll`이며, 설치 Mods 경로에 쓰거나 게임을 실행한 명령은 없다. 실행 주체는 식별하지 않았으므로 외부 변경의 정확한 출처를 단정하지 않는다. 동시 작업을 덮어쓰거나 게임을 종료하지 않았다.

## 재현 및 Claude 측 bench

다음 빌드/오프라인 검사는 설치/게임 실행을 하지 않는다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/direct-renderer/check-m1b.ps1 -NativeBuild -OutputDirectory out/direct-renderer/m2
dotnet build StutterFix.csproj -c Release
dotnet build StutterFix.csproj -c Release -p:Edition=Player
dotnet build StutterFix.csproj -c Release -p:Edition=Player -p:AutoTestBuild=1
dotnet run --project tools/direct-renderer/m2-check -c Release -- .
dotnet run --project tools/direct-renderer/m1c-policy -c Release
py -3.12 tools/direct-renderer/check-m2-artifacts.py
```

마지막 artifact 검사는 원래 `installed-before.json`을 유지한다. 이번 동시 변경 상태에서는 exit 2가 정상적인 HOLD다. 새로운 독립 실행의 기준선을 만들 때만 실행 시작 시점의 설치 해시를 새 증거 디렉터리에 기록한다.

Claude 측 실게임은 기존 `drPix`, `drOn`, `ppGpuCam` 프리셋을 사용한다. 이 세션은 아래 명령을 게임에 보내지 않았다.

```text
dr2 force off
dr2 only 0
dr2 seqtest off
dr2 seq on
dr2 seqparticles on
dr2 tight on
dr2 masks on
dr2 timing off
dr2 on
```

각 맵 새 재생, HELLO 50초 / WF 258초 / DD 50초 / Exit 88초에서 state → canvases → seqcheck → verify → drPix를 수집한다. Canvas 실제 타입/상위 이름과 `sequence`, `owned`, `released`, `unityIsland`, `drawRenderer`, `cbBuilds`, `cb_record_us`, `commit_max_us`를 함께 보관한다. 풀 꺼내기/반환·부모 활성화·재질/깊이·마스크 범위·Canvas 알파/텍스트·재시작/맵 전환/off/Unload·생성/복제의 전후도 확인한다.

drPix 목표는 다른 픽셀 <0.1%, 최대 채널 ≤3이다. 이어서 A=ppGpuCam / B=drOn ABBA를 같은 곡·RT·구간·창 조건과 매 판 새 재생으로 실행한다. FPS·메인/렌더 스레드·GC·최장 프레임과 전체 직통 후크 비용을 측정한다. `<50µs`는 commit 숫자 하나로 판단하지 않는다. 후크 비용과 commit은 중첩될 수 있으므로 단순 합산하지 않는다. 성능 판에는 verify/seqcheck/canvases/캡처를 넣지 않는다.

실게임 화면·성능 또는 실제 후크 설치가 실패하면 기본 꺼짐을 유지하고 해당 증거를 실패로 기록한다. 이 결과만으로 전체 경계 0·모든 맵 완전 소유·성능 개선이나 기본 켬을 선언하지 않는다.
