# 게임 스크립트 경로 분석·상한 플래그 구현 결과 — 2026-11-13

작업 위치 `E:\StutterFixWork\wt-v14prod`, 브랜치 `claude/adofai-renderer`, 기준 HEAD `34c0f8a`. 인계문이 요구한 정적 분석, AUTOTEST 상한 3종, 정확한 후보 1종과 같은 프레임 대조 명령, bench 설정 연결, 오프라인 검증을 완료했다. **게임 실행·설치 DLL 교체·커밋·푸시는 하지 않았다. 실제 상한/FPS, 게임 안 비트 대조, 화면·판정·소리 일치는 아직 미측정이다.** 아래 실행은 Claude 담당이다.

## 1. 이어받은 상태와 증거

- 기존 GPU 가림/겹침 압축/타일 인스턴싱 기각 분석은 다시 구현하지 않았다. 기존 작업 파일과 out 증거를 보존했다. 이번 과제의 `scriptskip` 구현과 결과 문서는 시작 시 없었다.
- 설치된 `Assembly-CSharp.dll`을 ilspycmd로 직접 디컴파일했다. SHA256 `EC0D7C3889696C5605D6DB4F443FD387F8668A90BA7B4DD2A1A2DA64B5ADA5BC`. 정확한 후보는 이 파일과 다르면 실행을 거부한다.
- Rewired/UI/TMP/UMM은 해당 설치 DLL을 따로 읽었다. [DLL 해시](../../out/scriptpath/dll-hashes.json), [디컴파일 원문 폴더](../../out/scriptpath/decompiled), [내부 경로 재집계](../../out/scriptpath/stacks.txt), [원시 집계 JSON](../../out/scriptpath/stacks.json).
- 원본 표본 `E:\StutterFixWork\night-results\nat-hl.resolved.txt` 12,025개, `nat-sh.resolved.txt` 12,021개. [집계기](../../tools/analysis/scriptpath.py)는 읽기만 한다. 진입점 이후 **포함** 호출 비율이므로 내부 행을 더하지 않는다. 표본의 owner 분류도 중첩될 수 있다.
- 아래 ms는 요청대로 **양쪽 모두 표본 비율 × 4.35ms**로 환산했다. Exit의 실측 프레임 시간이라는 뜻도, callback 직접 계측값이라는 뜻도 아니다. 표본은 RunLod 켬이므로 제품 기본 끔 상태의 비중은 달라질 수 있다. Camera.FireOnPreCull 비용을 이번 후보에 더하지 않았다.

## 2. 기존 시도를 먼저 확인한 결과

| 기존 작업 | 현재 근거와 이번 판단 |
|---|---|
| BeatSkipNoChange | `StutterFix.cs` 기본 true, `BeatFix.cs`의 PropagateOnBeat 경로. Conductor의 박자 전체 비용을 새 이득으로 세지 않음. |
| HoldSkip | 기본 true, `HoldFix.cs`에 입력 동일성·원본 대조가 있음. Exit의 HoldRenderer 표본을 미구현 후보로 반복 제안하지 않음. |
| HitMeterDirectKill | 기본 true, `HitMeterFade.cs`의 트윈 핸들 종료. 이번 Conductor 안의 ShowHitText/타격 경로와 구별. |
| TileCull/RecolorDefer | Floor.Update/LateUpdate 앞 `ScriptPrefix`가 미룬 색을 먼저 FlushFloor. 정확한 후보는 이 prefix를 건너뛰면 안 됨. |
| MouseEventsSkip | 기본 true지만 엔진 SendMouseEvents 단계 대상. uGUI EventSystem.ProcessMouseEvent/RaycastAll은 별도라 비용이 남음. |
| turbo safe2 | `docs/render-system/night-20261108.md`: 카메라 깊이 유지가 필요. UI 마스크·전체 캔버스 생략을 같은 결과인 것처럼 확대하지 않음. |
| FloorFix 삭제 | `CLAUDE.md`의 삭제 사유: 같은 스타일 인자여도 중간에 다른 코드가 재질 값을 바꿈. 스타일/색 마지막 값 메모 재도입 안 함. |
| 타일 색 같은 값/MPB | 같은 문서: 색 생략 기각, MPB 변동 수준. 이번 표본도 실제 SetColor가 대부분이라 반복하지 않음. |
| FloorLateFix·AllocFix·TextFix | 다중 행성 없는 타일 Transform 읽기, Volume 아닌 타일 closure 재사용, 같은 Shadow 색 쓰기 생략이 이미 존재. 해당 제거 이득을 중복 계산하지 않음. |

외부 `bench_v14.py`에는 `rlPc0/1/2/12`와 TileCpu* 설정명이 이미 있지만 현재 체크아웃에 그 설정/구현은 없다. 기존 항목은 보존하고, 이번 구현에 연결된 `sp*` 항목을 별도로 만들었다.

## 3. 13개 진입점의 후보·불변 조건·위험

표의 기대 이득은 해당 진입점 전체를 제거한다고 가정한 환산 한계 내의 범위다. **정확한 버전의 실측 이득이 아니다.** 상한 3개는 HL 진입점 비중 순으로 Floor.Update / Conductor.Update / Canvas를 선택했다. LateUpdate는 별도 함수로 남겼다.

| 진입점 | HL % / 환산 ms | Exit % / 환산 ms | 후보 | 같은 결과의 불변 조건 | 깨지는 경우·위험 | 기대 이득 |
|---|---:|---:|---|---|---|---|
| scrFloor.Update | 5.339 / 0.2322 | 6.139 / 0.2671 | Start 완료 + Single/Stripes + 비애니메이션 아이콘의 빈 본문 생략 **구현** | 매 호출 현재 필드로 판정; 본문에 출력 쓰기가 없음; 외부 패치 유지 | 특수색·애니메이션·Start 전은 원본; 미확인 transpiler면 거부 | 0~전체 경로 한계, 실제는 훨씬 작을 수 있음 |
| scrConductor.Update | 4.175 / 0.1816 | 2.246 / 0.0977 | 전체 생략 **상한만**; 예약 커서·입력 하위 경로 별도 조사 | 시계·입력·오디오 예약이 바뀌지 않는 하위 작업만 분리해야 함 | DSP 동일해도 시간 보간·키 큐 소비·자동 홀드/타격 진행이 필요 | 전체 상한 0.1816/0.0977ms, 정확 버전 미확정 |
| Canvas.SendWillRenderCanvases | 3.426 / 0.1490 | 3.452 / 0.1502 | 전체 이벤트 생략 **상한만**; dirty 원인부터 줄이기 | layout/geometry/culling/외부 구독에 실제 변화가 없음을 모두 확인해야 함 | TMP 내부 갱신·마스크 이동·동적 UI/다른 모드; 큐 미소비 누적 | 전체 상한 0.1490/0.1502ms, 정확 버전 미확정 |
| scrFloor.LateUpdate | 2.669 / 0.1161 | 2.512 / 0.1093 | 변화 없는 부분 분리 | opacity/glow/holdLength/다중 행성 선 및 이전 값 갱신이 모두 불필요 | nextfloor null이면 holdLength=-1; isFading 플래그와 마지막 값도 출력. 단순 early-return 금지 | 0~0.1161/0.1093ms, 기존 FloorLateFix 뒤 잔여만 |
| scrVfxPlus.Update | 1.830 / 0.0796 | 0.815 / 0.0355 | due 효과 없음 + 비디오 작업 없음인 프레임의 앞단 단축 | 다음 효과의 triggered/startTime-offset/커서와 영상 준비 상태를 매번 확인 | 시간 역행/seek, 영상 준비 완료, 커서 외부 변경. StartEffect는 실제 효과 | 0~0.0796/0.0355ms; HL 대부분 이미 FloorAnim |
| Rewired.InputManager_Base.Update | 1.198 / 0.0521 | 1.980 / 0.0861 | 무입력 장치만 별도 조사 | 장치 폴링·연결/해제·Update 설정과 입력 edge 소비가 없어야 함 | 키보드·컨트롤러·UI·외부 모드 입력. 비활성 화면만으로 생략 불가 | 정확 이득 미확정; 전체 0.0521/0.0861ms |
| UMM UI.Update | 1.156 / 0.0503 | 1.531 / 0.0666 | 모드별 OnUpdate 내부 불필요 일 분리 | 각 callback과 단축키의 의미가 보존됨 | 창이 닫혀도 StutterFix/Quartz Tick 실행. 전체 생략하면 자동 시험 자체도 멈춤 | 정확 이득 미확정; 전체 0.0503/0.0666ms |
| EventSystem.Update | 1.123 / 0.0488 | 1.081 / 0.0470 | 불필요 raycast 대상 줄이기 | 입력 module·selection·pointer/raycast 결과 보존 | 메뉴/오버레이 마우스·키보드·패드; MouseSkip과 다른 경로 | 0~0.0488/0.0470ms, 정확 버전 미확정 |
| scrHUDText.Update | 0.782 / 0.0340 | 1.065 / 0.0463 | Quartz 제목 표시 소유권 조정 | Quartz의 TakesOverTitle/InGame/isTitle 조건, Graphic 최종 상태·콜백까지 보존 | 게임이 켠 Graphic을 Quartz가 다시 끔; OnEnable/Disable 부작용, 실행 순서 | 0~0.0340/0.0463ms 및 파생 Canvas 비용, 중복 합산 금지 |
| PlanetSprite.LateUpdate | 0.757 / 0.0329 | 0.482 / 0.0210 | 매번 문자열 shader ID 해석 줄이기 검토 | 같은 material에 같은 float 비트 저장 | unscaledTime 기반 12fps%11이 **float**; 정수 프레임으로 반올림하면 다름. 재질 교체/외부 모드 | 0~0.0329/0.0210ms, setter 자체는 남음 |
| SteamController.Update | 0.657 / 0.0286 | 1.065 / 0.0463 | 안전한 반복 제거 근거 없음 | callback 큐가 비었음을 동일 API 의미로 보장해야 함 | overlay/license/실적 비동기 callback, 메시지 지연 | 현재 정확 후보 없음 |
| scnEditor.Update | 0.615 / 0.0268 | 0.923 / 0.0402 | 반복 UI 문자열/표시 상태 갱신을 세분화 | 편집 재생·입력·자동 저장·오류막대·외부 패치 보존 | 게임 모양이어도 편집기 playMode인 표본일 수 있음. 클래스 생존만으로 불필요 아님 | 0~0.0268/0.0402ms, 상태별 미측정 |
| PerfOverlay.LateUpdate | 0.615 / 0.0268 | 0.940 / 0.0409 | 정적 위젯 layout/native 조회 줄이기 | scale/화면/모드/show/open/slide/토스트 및 표시 값 동일 | 전환·리사이즈·hover·실시간 통계. Update의 FPS 수집과 분리해야 함 | 0~0.0268/0.0409ms; 화면 숨긴 진단을 제품 이득으로 세지 않음 |

### 호출 경로와 다른 패치와의 연결

1. **Floor.Update**: 엔진 CommonUpdate → Harmony wrapper → TileCull.ScriptPrefix(RecolorDefer.FlushFloor) → AllocFix transpiler가 있는 원본 → Start 필요 검사 → 시간/색 switch → 애니메이션 아이콘. HL 진입점 표본 642개 중 FloorMeshRenderer.set_color 463개(72.1%), SetColorImpl 427개(66.5%); Exit 738개 중 set_color 479개(64.9%). 색 쓰기 비용이 커서 빈 경로 생략만으로 전체 5~6%를 얻는다고 기대하면 안 된다. Volume은 None 판정 전에 Sequence를 생성하지만 이를 지우면 DOTween 등록/전역 상태도 달라질 수 있어 이번 정확 후보에서 제외했다.
2. **Conductor**: CommonUpdate → UpdateInput → ProcessKeyInputs → Simulated_PlayerControl_Update → OttoHoldHit/Hit → Planet.SwitchChosen/ShowHitText. HL 502개 중 UpdateInput 475개(94.6%), ProcessKeyInputs 406개(80.9%), OttoHoldHit 347개(69.1%). Exit는 UpdateInput 209/270(77.4%). 실제 판정 진행이 주비용이다. DSP/async clock 갱신 뒤 5초 앞 소리 예약 커서를 전진시키고 곡 시각·박자를 갱신한다. BeatFix/HitSoundBatch/LowNoFft가 이미 맡는 부분과 구별했다. 입력 없는 것처럼 보여도 자동 타격이 실행되므로 함수를 묶거나 저주기로 돌리는 것은 정확 버전이 아니다.
3. **Canvas**: 엔진 WillRenderCanvases → SendWillRenderCanvases → delegate → CanvasUpdateRegistry.PerformUpdate / TMP_UpdateManager.DoRebuilds. HL PerformUpdate 363/412(88.1%), Graphic.Rebuild 183/412(44.4%), Clipper.Cull 114/412(27.7%). Exit PerformUpdate 317/415(76.4%), Clipper.Cull 169/415(40.7%), TMP DoRebuilds 92/415(22.2%). 인계 집계의 TMP owner 29%/40%는 하위 스택 전체의 owner 포함 비율이며 DoRebuilds 단일 함수 비율과 다르다. TMP는 layout/graphic dirty 큐뿐 아니라 InternalUpdate 등록 목록도 순회한다. TextFix/HitMeterFade/UiDraw 이후에도 실제 geometry/culling이 남는다.
4. **Floor.LateUpdate**: nextfloor/게임 화면 조건 → isTweening/isFading/isChangingGlowMult 계산 → alpha/icon/glow/outline/dummy planet/다중 행성 선 갱신 → 마지막 위치·크기·opacity/glow 저장. HL SetFloatImpl 71/321(22.1%), SetIconColor 67/321(20.9%); Exit 48/302(15.9%), 29/302(9.6%). FloorLateFix의 기존 네 Transform 읽기 생략과 겹치지 않게 봐야 한다. FloorAnim의 opacity 변화가 이곳의 쓰기를 실제로 유발한다.
5. **VfxPlus**: paused/곡 미시작 반환 → quality 정책 → currentVfxIndex에서 due 효과만 전진 → StartEffect → 영상 준비/시작. HL StartEffect 190/220(86.4%), FloorAnim.StartPrefix 112/220(50.9%), FloorAnim.OpaOnly 90/220(40.9%). Exit StartEffect 38/98(38.8%), TweenFix.Begin 25/98(25.5%), FastMove.Prefix 18/98(18.4%). effects 전체를 매 프레임 처음부터 훑는 구조가 아니다. 이미 FloorAnim/FastMove/TweenFix/Precheck가 참여한다.
6. **Rewired**: 초기화·파괴·에디터/수동 업데이트 조건 → DoUpdate → CheckRecompile → ReInput 업데이트 loop. DoUpdate 포함 HL 143/144(99.3%), Exit 234/238(98.3%); 내부 키보드·버튼 경로가 남는다. async keyboard와 Rewired를 같은 입력 처리로 간주하거나 중복이라 제거할 수 없다.
7. **UMM**: KeyBinding.BindKeyboard → Active 모드 OnUpdate → 창/모드 단축키. HL StutterFix.OnUpdate 74/139(53.2%), Quartz.LoaderUmm 54/139(38.8%); Exit 89/184(48.4%), 80/184(43.5%). 창 그리기 자체보다 callback이 주비용이다. StutterFix 안에는 AutoTest.Tick, GC/TileCull/스케줄링/복구 등이 있어 UMM 전체 상한은 이번에 만들지 않았다.
8. **EventSystem**: interop → current 확인 → module tick/선택 → StandaloneInputModule.Process → ProcessMouseEvent → GetMousePointerEventData → RaycastAll. HL Process 107/135(79.3%), RaycastAll 74/135(54.8%); Exit 101/130(77.7%), 65/130(50%). 단순 마우스 정지 조건으로 selection/navigation/hover 의미까지 같다고 보장되지 않는다.
9. **HUD**: 게임이 HUD flag를 계산해 Graphic.enabled/color/Shadow 색을 쓴 뒤 Quartz HideGameTitlePatch.Postfix가 제목을 숨긴다. HL Behaviour.SetEnabled 77/94(81.9%), Exit 98/128(76.6%). Quartz `SongTitleOverlay`도 현재 설치 버전을 디컴파일했다([원문](../../out/scriptpath/decompiled/QuartzSongTitle.cs)): isTitle && TakesOverTitle && GameStats.InGame이면 같은 Graphic을 다시 끈다. **게임의 켜기와 Quartz의 끄기가 충돌하는 경로를 소스로 확인했다.** 표본만으로 각 Graphic별 반복 횟수는 확정하지 않았다. 같은 Shadow 색 최적화는 이미 TextFix에 있으므로 새 후보가 아니다. 다음 확장 우선순위는 이 표시 소유권과 Canvas dirty의 연결을 별도 상한으로 확인하는 것이다.
10. **PlanetSprite**: 엔진 LateUpdate → renderer.material → SetFloat("_Frame", unscaledTime*12f%11f). HL material SetFloat 41/91(45.1%), GetMaterial 34/91(37.4%). 값은 매번 변하므로 프레임 정수값 캐시를 동일 결과라 할 수 없다. Quartz PlanetRenderer.LateUpdate와 FrameCompare의 정지용 freeze 패치도 별도다. 재질 캐시는 외부 교체와 인스턴싱 의미를 확인해야 한다.
11. **Steam**: Update는 RunCallbacks 한 줄 → Dispatch.Frame → SteamAPI_ManualDispatch_GetNextCallback. HL 79개 모두 RunCallbacks, Exit 128개 모두 RunCallbacks. 함수를 얇게 합쳐도 native callback 처리는 남는다.
12. **Editor**: 실제 Update는 `scnGame.instance==null || pauseMenu.activeSelf`에서만 즉시 반환한다. 편집 재생 중에는 UpdateSelectedFloor/Otto/refresh 목록, Escape/Space, backup 타이머, pointer/scroll/drag, controlsTip/ottoCanvas, error meter 등을 처리한다. HL RDString.Get 24/74(32.4%), Exit 46/111(41.4%). **기존 bench의 open→seek는 편집 재생 경로**이고 순수 game→press와 다르다. 이번 표본에서 editor가 도는 이유를 이것으로 설명할 수 있지만, 실행 당시 scene 기록을 새로 측정하지 않았으므로 확정은 아니다. BENCH_GAME=1 대조가 필요하다.
13. **Overlay**: Update가 수집한 통계를 LateUpdate가 show/화면 크기/scale/모드로 배치해 UiDraw에 넘긴다. HL DrawIcon 44/74(59.5%), Exit 62/113(54.9%), UiDraw.Next와 Component.gameObject가 반복된다. 문자열은 이미 저주기여도 그리기 경로는 매 프레임이다. 통계 수집을 끄면 FPS 측정과 진단이 달라지므로 이번 상한 대상에서 제외했다.

## 4. 구현과 안전 범위

[ScriptPath.cs](../../ScriptPath.cs)는 **AUTOTEST만** 컴파일한다. 일반 DEV와 Player에는 타입 자체가 없고 제품 설정을 추가/저장하지 않는다. 기본 off일 때 패치도 없다.

```text
scriptskip floor on|off
scriptskip conductor on|off
scriptskip canvas on|off
scriptskip assert floor|conductor|canvas
scriptskip exact on|verify|off|assert
scriptskip state
scriptskip off
```

- 상한은 대상 함수의 prefix를 false로 반환한다. 화면/판정/소리가 바뀌는 진단이며 최적화가 아니다. `assert`는 실제 intercepted 호출 >0을 요구한다. 엔진의 callback dispatch 자체는 남는다.
- conductor 전체 생략은 곡 시계를 멈추므로 **seek/waitsongpos 뒤에만 켠다.** 음악/다른 컴포넌트가 계속 돌고 입력·효과 작업량이 연쇄 변화하므로 전후 FPS 차이는 해당 함수만의 순수 비용이나 동일 장면 성능 상한으로 엄밀하게 해석할 수 없다. 제어 흐름 진단으로만 쓴다.
- 정확한 Floor 후보는 bool prefix 생략 대신 **원본 본문 첫 위치에 가드/early return을 삽입**한다. 따라서 TileCull의 지연 색 FlushFloor와 외부 prefix/postfix는 원래처럼 한 번 실행된다. __runOriginal 의미도 유지된다. AllocFix 이외의 미확인 transpiler가 있으면 적용하지 않고 assert 실패로 남긴다. 게임 DLL 해시와 예외 블록 형태도 확인한다.
- 매 호출 현재 didRunStart/isPlaying/specialColorType/floorIcon을 읽는다. Start 전, 특수색 5종, 애니메이션 4종이면 원본으로 보낸다. 스타일·재질의 마지막 인자를 메모하지 않는다.
- `exact verify`는 후보의 **항등 결과(상태 그대로)**를 스냅샷으로 보관하고 같은 프레임에 원본 본문을 한 번 실행한 뒤 각 원래 return 앞에서 대조한다. 외부 postfix 실행 전 비교이며 판정/오디오 함수를 이중 실행하지 않는다. float/double는 signed zero와 NaN payload까지 비트로 비교하고 struct는 필드별 재귀 비교, 참조는 동일성을 비교한다. 불일치 시 다음 호출부터 원본으로 복귀하고 assert가 실패한다.
- 대조 범위는 scrFloor와 관리 기반 클래스의 인스턴스 필드이며, 참조 객체 내부/Unity native 상태/전체 정적 상태의 전수 스냅샷은 아니다. 해당 원본 분기에 setter/외부 작업이 없다는 코드 근거와 함께 해석한다. **게임 내 비트 일치/픽셀/판정/소리의 전 범위 증명은 아직 없다.** `verify`는 reflection/할당이 크므로 FPS 판과 섞지 않는다. `exact on` 본문에는 검증용 return callback을 넣지 않는다.
- 모드 끄기/Unload, SceneReset, AutoTest Finish 및 keep EndBatch에 Stop을 연결했다. 진단별 Harmony ID만 해제하고 다른 모드 패치는 건드리지 않는다. off는 변경된 게임 상태를 시간 역행시키는 기능은 아니다.

## 5. bench 연결과 Claude 실행 순서

외부 파일 **`E:\StutterFixWork\bench_v14.py`도 직접 수정했다.** 체크아웃 git diff에는 포함되지 않는다. 변경 내용은 rlNR의 dict/list 독립 복사와 `CFG_MID/CFG_POST/CFG_ACCEPT`, steps의 선택적 post 인자, 로그 필터/수락 검사다. 기존 설정은 보존했다.

| CFG2 | seek 뒤 명령 | FPS 측정 뒤 검사 |
|---|---|---|
| spFloor | scriptskip floor on | assert floor → off |
| spConductor | scriptskip conductor on | assert conductor → off |
| spCanvas | scriptskip canvas on | assert canvas → off |
| spExact | scriptskip exact on | exact assert → off |
| spVerify | scriptskip exact verify | exact assert → off; FPS 해석 금지 |

수락 로그가 해당 설정과 일치하지 않거나 `단계 실패`/exact INVALID가 있으면, 앞에서 FPS 줄이 출력됐어도 bench에서 무효 처리한다. 대상 0개·해시 불일치·미지원 패치가 성능 향상으로 둔갑하지 않게 했다. 반복 실패 시 기존 측정기처럼 `?`가 남는다.

다음은 **준비만 하는 명령**이다(이번에 실행 완료):

```powershell
Set-Location E:\StutterFixWork\wt-v14prod
py -3.12 tools/analysis/prepare-scriptpath.py
```

[준비기](../../tools/analysis/prepare-scriptpath.py)는 실행 옵션 자체가 없다. HL50/Exit88/Arche262/Windflower260/7777 120/DD55의 verify·pixels JSON 12개를 [plans 폴더](../../out/scriptpath/plans)에 쓴다. pixels 계획은 baseline 두 장 차이 0 → 후보 차이 0 → off 복구 차이 0을 각각 assert한다. 화면 안정성 검사를 통과하지 못한 결과는 제외한다.

**Claude의 실제 실행 예시**(Codex는 실행하지 않음):

```powershell
Set-Location E:\StutterFixWork
$env:BENCH_WT='wt-v14prod'
# 테스트 DLL 빌드/설치는 기존 run_locked 소유권/복구 절차에 따름
py -3.12 run_locked.py wt-v14prod/out/scriptpath/plans/scriptpath-hl-verify.json wt-v14prod scriptpath-hl-verify --keys 'scriptskip,단계 실패,오류로' --tail 12
py -3.12 run_locked.py wt-v14prod/out/scriptpath/plans/scriptpath-hl-pixels.json wt-v14prod scriptpath-hl-pixels --keys 'scriptskip,framecmp,단계 실패,오류로' --tail 12
# 각 후보의 A/B/B/A. 판마다 tag를 달리해 모든 원본 로그를 보존한다.
# spExact는 verify/pixels 통과 후에만 목록에 추가한다.
foreach ($candidate in @('spFloor','spConductor','spCanvas')) {
    py -3.12 bench_v14.py "$candidate-hl-a1" hl50 rlNR
    py -3.12 bench_v14.py "$candidate-hl-b1" hl50 $candidate
    py -3.12 bench_v14.py "$candidate-hl-b2" hl50 $candidate
    py -3.12 bench_v14.py "$candidate-hl-a2" hl50 rlNR
}
```

주의: 기존 bench는 같은 tag-map-config 파일명을 재사용한다. `rlNR,spFloor,spFloor,rlNR`처럼 같은 tag 안에 반복 설정을 넣으면 TSV만 네 행이고 txt/log는 덮인다. 위 예시는 판별 tag를 분리했다. 재실행 때에도 새 tag를 사용하고, 현재의 오래된 파일을 이번 실행 성공으로 읽지 않도록 파일 시각·종료 마커·실패 여부를 확인한다. baseline/후보 각각 새 게임 실행, 동일 모드 집합/렌더 해상도/프레임 통계 조건을 유지한다.

Exit/Arche는 각각 sh/ar로 반복하고, BENCH_GAME=1로 편집 재생과 순수 게임 경로를 구분한다. 상한이 유의하더라도 정밀 후보의 eligible 비율·guard 비용·실제 프레임 시간은 별도로 비교한다. 꼬리 지연/곡 전체/다른 맵 이득으로 확대하지 않는다.

## 6. 수행한 검증과 남은 일

| 검증 | 결과·한계 |
|---|---|
| PlayerAuto 빌드 | 오류 0, 경고 57. [로그](../../out/scriptpath/build-auto.txt) |
| Player 빌드 | 오류 0, 경고 63. [로그](../../out/scriptpath/build-player.txt) |
| DEV 빌드 | 오류 0, 경고 84. [로그](../../out/scriptpath/build-dev.txt) |
| 산출물 메타데이터 | PlayerAuto에 ScriptPath 존재; DEV/Player에는 0개. `out/scriptpath/types-*.txt` |
| 연결된 실제 ScriptPath 소스의 .NET fixture | 192 가드 조건, 상한 3종 진입/0회 거부/해제, DLL 해시 거부, 동적 IL 실행/원래 return 라벨 보존, 같은 프레임 항등 비교, signed-zero 불일치 음성 대조와 원본 복귀, NaN/struct 비트 비교, 미확인 transpiler 거부, Stop 초기화 통과. **Unity와 Harmony 등록은 stub이며 게임 Mono 실행 증거가 아님.** [결과](../../out/scriptpath/fixture.txt) |
| 측정 계획 | rlNR 독립 복사 5종, editor/game 10계획, seek 뒤 활성화/FPS 뒤 검사·해제, baseline 보존, 누락/잘못된 수락·실패 로그 거부 통과. [결과](../../out/scriptpath/plans-check.txt) |
| git diff --check | 통과 |
| 실제 상한·FPS·픽셀·판정·소리 | **미실행. 인계문 금지 범위여서 Claude가 수행해야 함.** |

재현 가능한 오프라인 검사:

```powershell
dotnet run --project tools/scriptpath-fixture/ScriptPathFixture.csproj -c Release
py -3.12 tools/analysis/check-scriptpath-plan.py
py -3.12 tools/analysis/scriptpath.py
```

남은 우선순위: (1) verify에서 compared>0, eligible 대비 비교 누락 0, mismatches=0 확인, (2) 정지 장면 baseline/후보/복구 차이 0, (3) 세 상한을 변형된 작업량이라는 한계와 함께 측정, (4) exact 자연 재생 ABBA에서 유효 이득 확인. 정확한 후보 이득이 없으면 기본 off를 유지한다. 다음 큰 경로는 HUD/Quartz의 enabled 왕복과 Canvas dirty이며, 타일 색 동일값 캐시나 Conductor 입력 저주기화는 재도입하지 않는다.
