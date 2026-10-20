# matshare 결과 — 2026-10-21

개발 전용 센서스, Run 재질 공유 프로토타입, 실제 장식 쌍의 정렬 비교, SortProbe fixture, 측정 steps 생성기를 추가했다. 기본 꺼짐이며 Player와 PlayerAuto 양쪽에서 제외했다. 게임 실행·설치·커밋은 하지 않았다. 시작할 때 있던 GPU 진단 변경과 산출물은 보존했다.

**실제 장면 센서스, GPU/draw/SetPass, 화면 동일성, Unity 정렬 fixture 결과는 미측정이다.** 아래 오프라인 검사는 해당 결과의 대체 증거가 아니다. 기본 켬·Player 이식·성능 개선 채택은 보류한다.

## 1. 명령과 센서스

```text
matshare census
matshare on
matshare state
matshare compare
matshare off
```

`census`는 공유가 꺼진 상태에서 실행한다. `Resources.FindObjectsOfTypeAll<scrVisualDecoration>` 중 유효한 장면의 `spriteRenderer`를 읽어 비활성·강제 숨김 장식도 포함한다. `renderer.material` getter는 사용하지 않는다. 로그 내용:

- 전체 이미지 수와 live 수. live는 enabled·activeInHierarchy·forceRenderingOff 아님이며 frustum 판정은 포함하지 않는다.
- 원래 재질 instance ID 고유 수, 재질 내용 서명 고유 수와 상위 10개 빈도/내용.
- 서명: shader ID, 정렬한 활성 keywords, renderQueue, instancing/GI 상태, shader pass 활성 상태, shader가 선언한 모든 float/range/int/color/vector/texture 값과 texture scale/offset. float는 비트 단위 문자열이다. `_SrcBlend/_DstBlend/_BlendOp/_ZTest/_ZWrite/_Cull`은 존재 여부도 기록한다.
- sortingLayer 값/ID → sortingOrder → queue → 거리 내림차순 → **부호 있는 재질 ID 오름차순**으로 정렬한다. 거리 계산은 카메라/전역 transparencySortMode·custom axis·SpriteRenderer pivot/center 설정을 반영한다. NaN/Infinity는 거절한다.
- `(정렬 키 + 서명 + sprite.texture ID)`별 `runs_all`, `runs_live`를 각각 기록한다. shader property와 별개인 sprite texture를 생략하지 않는다.
- 속성별 서로 다른 값 개수 상위 5개와 후보/제외 사유 분포. 이는 변이 개수 순위이며 특정 속성 하나를 없앴을 때 줄어드는 draw 수의 인과 분석은 아니다.

서명은 현재 material의 선언 속성 비교다. MPB는 서명에 합쳐서 동등하다고 간주하지 않고 프로토타입에서 제외한다. shader에 선언되지 않은 임의의 native/모드 상태까지 전부 열거했다는 뜻은 아니다.

Run 수는 **이미지 장식만의 이론적 묶음 수**다. 비이미지 렌더러 끼어들기, 실제 batching 제한, 마스크, 카메라별 culling과 pass 때문에 실제 draw 수와 일치하거나 상한이라는 보장은 없다. 로그에도 `estimate_only=1`, `excludes_nonimage_interleaving=1`을 남겼다. 인계의 1,840 draw와 715~744 구간은 이번에 재측정하지 않았다.

## 2. IL 정밀 분석

[전체 IL 및 어셈블리 SHA256](codex-matshare-20261021-il.txt), [호출처](codex-matshare-20261021-callers.txt), [시각 장식 호출/필드](codex-matshare-20261021-visual-il.txt), [manager 호출/필드](codex-matshare-20261021-manager-il.txt)를 남겼다. `tools/optimization/MatShareAudit`는 DLL의 metadata/IL만 읽으며 게임 메서드를 실행하지 않는다. ILScan의 호출처 검색은 가상 호출 구현을 모두 확장한 call graph는 아니다.

| 메서드 | 정확한 재질/텍스처 조작 | 장식별 값/상수 | 호출 조건과 빈도 |
|---|---|---|---|
| `SetSprite(Sprite)` / `SetSprite(CustomSprite,bool)` | sprite 교체, `spriteRenderer.material`을 `RDUtils.SetPointSamplingVariant`에 전달. `USE_POINT_SAMPLING` 활성/비활성. 구버전 기능 경로는 sprite texture의 wrapMode도 변경 | keyword는 `!smoothing`; 텍스처는 해당 sprite | `scrDecoration.Setup`; CustomSprite 오버로드는 `ffxMoveDecorationsPlus.StartEffect`의 이미지 변경/복구 분기에서도 호출 |
| `SetTile(Vector2)` | repeatX/Y 저장. `disableV15Features==false` 경로는 `ShaderProperty_RepeatX/RepeatY`에 SetFloat. true 경로는 source event의 `decorationImage` 키로 `UpdateDecorationTiling` 예약 | 입력 Vector2의 장식별 x/y | 직접 호출처는 Setup 1곳. 이 DLL에서 별도 곡 중 SetTile 호출처는 찾지 못했음. 다른 모드 호출은 미확정 |
| `SetBlendMode(DecorationBlendMode)` | 모드>0이면 sprite/mesh BlendModeEffect를 켜고 BlendMode와 sprite UsePointSampling 설정. 기본 모드는 `spriteRenderer.material.shader=tileShader`. meshRendererMat이 없으면 visualDecoShader Material 생성 후 mesh에 할당 | 모드와 smoothing은 장식별; tileShader/visualDecoShader는 공통 | 직접 호출처는 Setup. 이 DLL에서 별도 곡 중 SetBlendMode 호출처는 찾지 못했음. 곡 중 블렌드 이벤트가 반드시 이 메서드를 재호출한다고 단정하지 않음 |
| `UpdateShader(bool)` | 세 get_material은 모두 **meshRenderer** 대상(0037/030B/03B1). mesh mainTexture에서 RT를 확인하고 색/opacity/tile vector/mainTexture 설정. 효과·마스크 분기에서는 임시 RT와 공통 spriteAlphaMask 재질의 RGB/A texture 및 VisibleOutsideMask=0 사용 | color/opacity/repeat/texture/RT는 장식별; 마스크 플래그의 0은 상수 | manager LateUpdate → decoration LogicUpdate → virtual UpdateShader. bool이 false이고 mask가 아니거나 sprite가 없으면 mesh를 끄고 조기 반환. 따라서 모든 일반 sprite가 매 프레임 material getter를 실행하는 구조가 아님 |
| `UpdateDecorationTilingLateUpdate()` | `spriteRenderer.material != null` 확인만 한다. Material.Set*는 없음. repeatX/Y != 1을 누적해 같은 texture의 wrapModeU/V를 Repeat(0)/Clamp(1)로 설정. 끝에 textureKeysToUpdate.Clear | repeat 필요 여부는 장식별, wrap 설정은 텍스처 객체 단위 | manager LateUpdate에서 매 프레임 호출되지만 disableV15Features가 false면 즉시 반환. true여도 **예약 키가 있을 때만** 장식 순회/get_material. SetTile → UpdateDecorationTiling(string)이 키를 등록 |

중요한 정정: `.material`은 공유 재질에서 장식용 인스턴스를 얻는 과정에서 복제할 수 있지만 이미 인스턴스화된 재질을 매번 새로 복제한다는 뜻은 아니다. IL만으로 native 복제 횟수나 CPU 낭비 ms를 측정할 수 없다. tiling 경로의 null 검사에 getter를 쓰는 것은 인스턴스화가 불필요해 보이는 지점이지만 이번에는 게임 코드를 패치하지 않았다. `UpdateShader`의 반복 getter도 sprite 재질 복제의 직접 증거로 합산하면 안 된다.

`ffxMoveDecorationsPlus.StartEffect`에서 depthUsed/targetDepth → SetDepth와 이미지 변경 → SetSprite를 확인했다. 프로토타입은 아래처럼 모든 태그 대상을 보수적으로 제외해 이벤트 종류의 누락에 기대지 않는다.

## 3. Run 공유 프로토타입

구현: `MatShare.cs`, 순수 정책 `MatSharePolicy.cs`. 새 Harmony 패치가 없다. `on`은 로딩 완료 후 정지한 에디터에서만 허용하며 다른 GPU 숨김/캡처 진단이 실행 중이면 거절한다. 매 프레임 자동 생성/재계산은 하지 않는다.

후보는 정확한 scrVisualDecoration, 단일 pass Sprites/Tile, 반복 1×1, 일반 sprite이며 mask/hitbox/mesh/SortingGroup/split-alpha/활성 BlendModeEffect/MPB는 제외한다. manager의 `taggedDecorations`에 들어 있는 **모든 장식**을 잠재적인 이미지·블렌드·타일링·깊이 변경 대상으로 취급해 제외한다. 이벤트가 없는 태그도 제외하는 과잉 배제다. 태그 정보가 없으면 거절한다. 알 수 없는 모드의 태그 없는 런타임 변경까지 안전하다는 보장은 없다.

원래 순서에서 A→B→A를 서로 다른 3개 Run으로 유지하며, 숨김 객체도 경계 계산에 포함한다. 여기에는 두 가지 중요한 제한을 추가했다.

1. **새 Run 재질과 개별 유지 재질을 같은 동률 정렬 묶음에 섞지 않는다.** 새로운 material ID가 기존 material ID보다 한쪽에 몰리면 개별 대상의 상대 순서를 넘을 수 있다. 같은 sortingLayer/order/queue/거리 묶음에 제외 대상이나 다른 종류의 Renderer가 하나라도 있으면 묶음 전체를 원래 재질로 유지한다. 외부 검사는 숨김 포함 전체 장면 Renderer 대상이다. 따라서 실제 후보 수는 원시 census Run 수보다 훨씬 적거나 0일 수 있다. 후보가 0이면 명시적으로 실패하고 원복한다.
2. 생성 순서가 ID 감소 순서라는 가정에 의존하지 않는다. 각 Run용 Material을 원본에서 복제한 뒤 **실제 signed ID를 정렬해** 뒤→앞 Run 순서에 배정한다. 원본과 복제본의 내용 서명도 비교한다. single-renderer Run도 동일 묶음에 들어 있으면 함께 재배정해 순서를 유지한다.

같은 Run 안에서 재질 ID라는 구별 키가 사라지므로 **Run 내부 장식 순서는 여전히 미확정**이다. 같은 texture/material이라도 서로 다른 vertex color/alpha와 겹침이 있으면 픽셀이 달라질 수 있다. signed ID로 Run 경계를 보존하는 것만으로 화면 동일성은 증명되지 않는다. 이를 SortProbe와 실제 pair 비교로 드러내도록 했으며 자동 채택하지 않는다.

`off`/`Main.Unload` 경로의 `UninstallAll`/AutoTest 종료/컴포넌트 파괴/기존 SceneReset prefix에서 복원한다. SceneReset에는 기존 prefix의 본문 호출만 추가했으며 새 패치는 없다. 원래 material 객체를 유지해 정상 복원 시 원래 ID도 되돌린다. 외부 코드가 원본을 Destroy한 예외 상황에는 renderer별 clone으로 복구하고 `original_id_order_not_recoverable=1`을 기록한다. 이 경우 원래 ID 순서의 완전 복원은 불가능하다.

LateUpdate에서 renderer/material/sprite/sortingLayer/order/repeat 변경과 맵 reset을 감지하면 중단·복원한다. 모든 shader 속성/transform/다른 모드의 이후 callback을 매 프레임 전수 감시하는 기능은 아니다. 이 실험의 CPU 순회 비용도 성능 측정에 포함된다. `seek`/Play가 reset이나 renderer 재설정을 유발해 공유가 해제될 수 있으므로 **ON 창 양끝의 matshare state가 on=True인지 확인해야 하며**, 해제된 창은 성능 통과 자료가 아니다.

## 4. 직접 관측과 Unity fixture

`MatShareCompare.cs`는 선택지 (a)를 구현했다. 별도 camera와 full-size ARGB32/depth24 RT를 만들고, 선택한 **실제 SpriteRenderer 두 개**를 렌더러가 없는 임시 layer로 잠깐 이동한다. source 카메라의 transform/view/projection/sort 설정을 사용하되 isolated camera의 clear는 SolidColor로 설정한다. 본 카메라의 Nothing clear와 누적 depth를 재현한다고 주장하지 않는다.

최대 8개의 연속 live 후보 쌍 중 bounds가 교차하고 sortingLayer/order가 같은 쌍을 검사한다. 같은 synchronous frame에서:

1. 원래 재질 A0/A1 렌더.
2. sortingOrder를 일시 변경해 각각 앞에 오도록 두 번 렌더. 두 결과가 실제로 다른지 검사하고 A1이 어느 쪽과 일치하는지로 `original_front` 판독.
3. 각 장식 단독 렌더로 alpha가 겹치는 실제 픽셀이 있는지 확인.
4. 공유 재질 B 렌더, 강제 순서 대조군과 비교해 `candidate_front` 판독.
5. 원래 재질 A2 렌더. A0/A1/A2 제어 차이가 0인지 검사.

로그는 frame, renderer ID, same_run, original_front/candidate_front, overlap_pixels, sensitivity, RGBA 최대 차이/다른 픽셀 수, controls, valid를 남긴다. 순서에 둔감한 쌍·픽셀 겹침 없음·제어 불안정·원본 앞뒤 판독 불가이면 invalid다. tested=0은 성공이 아니다. layer/material/enabled/sortingOrder/active RT는 finally로 복원하고 임시 객체를 해제한다.

한계: 실제 두 장식의 isolated composition이다. 가려진 장식·나머지 Run·전체 프레임·BGStatic→BGMoving→본 카메라→Overlay 합성과 depth/마스크/후처리 동일성의 증거가 아니다. shader는 원래 shader를 쓰므로 vertex color가 다른 같은 Run 내부의 순서 변화도 검출할 수 있지만, 8쌍이 모든 장식을 대표하지 않는다. 다른 모드의 camera callback 부작용을 완전히 되돌리지는 못한다. GPU readback/RT 렌더 비용이 크므로 해당 실행은 성능 창에서 분리한다.

`SortProbe.cs`에 DEV 케이스를 추가했다(`sortprobe all 50 20261021`).

- `material-reassign`: 모든 원래 재질이 다른 장식의 관측 순서를 얻고, 실제 signed ID 순서로 새 재질을 배정한 후 전체 순서 일치를 검사한다.
- `matshare-runs`: 원래 순서에서 2개씩 공유해 Run 경계 보존과 Run 내부 순서 변화 여부를 관측한다.
- 생성된 material ID 목록과 decreasing 여부를 CSV details에 기록한다. pairwise tournament/full top/peeling/반복 렌더는 기존 SortProbe 절차를 재사용한다.

이 Unity fixture는 **준비만 했으며 실행하지 않았다**. 정책 fixture의 25,401 checks가 실제 Unity 생성 ID 방향이나 정렬을 검증했다는 뜻은 아니다. 내부 순서가 바뀌는 결과가 나오면 화면 동일성 후보로 채택하지 않는다.

## 5. Claude 실행용 steps

```powershell
py -3 tools/optimization/matshare-steps.py --chart '<Arche FULL.adofai 실제 경로>' --output tools/optimization/out/matshare-steps.json
```

생성기는 JSON만 작성하며 실행/설치/sfmeasure 호출을 하지 않는다. [예시 JSON](codex-matshare-20261021-steps-example.json)은 가상 경로를 넣은 형식 예시다. 실제 경로로 다시 생성해야 한다. 생성 결과의 steps/timeout_min/tag를 sf_run에 전달한다.

69 steps: 초기 정지 상태 census → OFF/ON/OFF 동일 구간 3창 → 별도 compare 반복 → 정지 상태 SortProbe. 매번 seek 262 12, 258초 준비, 262초 gpuprobe reset, 266초 state를 사용한다. on은 재생 전 정지 상태에서만 요청한다. compare는 별도 재생의 264초에 요청하며 gpuprobe는 꺼져 있다. 센서스/픽셀 readback/fixture는 타이밍 창 밖이다.

판정 표를 채울 항목:

| 항목 | 현재 결과 |
|---|---|
| 전체/live material ID 수, 서명 수, Run 수 | 미측정 |
| 실제 공유 renderer/Run 수 및 ON 창 유지 | 미측정 |
| OFF 앞 / ON / OFF 뒤 카메라별 GPU ms | 미측정 |
| 전체 프레임 draw/SetPass, CPU frame time | 미측정 |
| 실제 pair valid/invalid/changed, RGBA 차이 | 미측정 |
| material ID 생성 방향/Run 경계/내부 순서 fixture | 미측정 |
| 전체 화면 동일성 | 미입증 |
| 채택 | 보류 |

gpuprobe draw/SetPass는 **전체 프레임** 카운터다. 앞뒤 기준과 다른 카메라 시간의 안정성도 함께 확인해야 한다. 인계의 GPU 1~1.5ms 절감은 가설이며 이번 결과로 보고하지 않는다. pair 비교가 모두 같아도 전체 화면 동일 증명 없이 기본 켬/Player 이식하지 않는다.

## 6. 오프라인 검증

| 검사 | 결과/근거 |
|---|---|
| Release DEV | 오류 0, 경고 38 — [빌드](codex-matshare-20261021-build-dev.log) |
| Release Player | 오류 0, 경고 36 — [빌드](codex-matshare-20261021-build-player.log) |
| Release PlayerAuto | 오류 0, 경고 38 — [빌드](codex-matshare-20261021-build-playerauto.log) |
| LoadCheck DEV / Player / PlayerAuto | 266 / 229 / 266곳, 각각 틀림 0. 동적 이름 5곳은 실행 중 확인 영역 — [DEV](codex-matshare-20261021-loadcheck-dev.log), [Player](codex-matshare-20261021-loadcheck-player.log), [PlayerAuto](codex-matshare-20261021-loadcheck-playerauto.log) |
| MatShareCheck | 25,401 checks, errors=0. A/B/A 분리, barrier, 빈 입력, 1,000개 무작위 Run/부호 있는 ID 배정 — [fixture](codex-matshare-20261021-fixture.log) |
| steps 구조 검사 | 69 steps, 동일 구간 OFF/ON/OFF 및 compare/fixture의 gpuprobe 분리 — [검사 로그](codex-matshare-20261021-steps-check.log) |
| edition 검사 | MatShare/Compare/Policy DEV 존재, Player/PlayerAuto 모두 부재 — [IL 로그 마지막 EDITION 행](codex-matshare-20261021-il.txt) |
| git diff --check | 통과. 기존 파일의 LF→CRLF 안내만 출력 |
| 새 Harmony / Module.ResolveSignature | 추가 없음 |

LoadCheck는 FieldRefAccess metadata 검사이며 실제 Unity 로드/실행 시험은 아니다. 기존 assembly 버전 충돌/obsolete/미사용 필드 경고는 남아 있다.

추가/수정 소스는 MatShare 3파일, AutoTest 명령/종료, Main 종료 복원, 기존 SceneReset 복원 호출, SortProbe fixture, csproj의 DEV include, MatShareAudit/MatShareCheck/steps 생성·구조 검사다. 기존 GpuCensus/GpuHide/GpuProbe/GpuSplit/GpuSplitPolicy/PixelDiff 및 이전 산출물은 이번 작업 시작 전 변경을 유지했다.

## 관찰 보고

task-observer 3.5.0을 읽고 기존 사용자 범위 관찰 frontmatter/원칙을 확인했다. 해당 로그는 허용된 쓰기 경로 밖이므로 변경하지 않았다. 이번 관찰은 이 인계에 남긴다: 같은 내용/텍스처로 재질을 공유해도 기존의 material-ID 타이브레이커가 사라져 Run 내부 순서가 변할 수 있다. 또 픽셀 비교는 순서에 민감한 양성 대조군 없이 0 차이만 얻으면 순서 보존을 판정할 수 없다. 설치된 스킬과 메모리는 수정하지 않았다.
