# 직통 렌더러 M1b 구현·오프라인 검증 결과 (2026-11-15)

작업 폴더: `E:\StutterFixWork\wt-v14prod`, 브랜치 `claude/adofai-renderer`, 시작 HEAD `85cb38f`.
지시서: [03-m1b-spec.md](03-m1b-spec.md), 설계: [00-design.md](00-design.md).

지시서의 코덱스 담당 구현과 오프라인 검증을 수행했다. **WF 81%·Exit 13.5%의 실게임 차이가 해소됐는지, 실제 맡은 수와 <50µs/프레임·할당 0 합격 여부는 아직 미측정**이다. 해당 숫자는 아래 Claude 측정 절차로 확인해야 한다. 오프라인 통과를 실게임 합격으로 집계하지 않는다.

게임 실행·프로세스 종료·설치 DLL 교체·커밋은 하지 않았다. 시작할 때 이미 실행 중이던 게임은 그대로 두었다. 기존 `AutoTest.cs`, `TileSig.cs` 수정과 Silentroom 자료도 보존했다. 로컬 `native/sfnative.dll`을 빌드하고, 이 DLL을 내장하는 관리 DLL 3종을 그 뒤에 다시 빌드했다.

## 구현

### 같은 프레임 검증: `dr2 verify`

`DirectDeco.Command`에 명령을 연결했다. 기존 AUTOTEST `dr2` 라우터를 사용한다.

- 각 소유 장식의 `UpdatePosition()`을 직접 한 번 실행하고, 원래 SpriteRenderer/SpriteMask의 `localToWorldMatrix × sprite.vertices`를 구한다. SpriteRenderer의 flip은 원래 정점에 적용한다.
- 같은 채널과 정적 값으로 `dr2.hlsl` VS의 C# 복제 식을 계산해 월드 좌표 오차를 비교한다. `ShaderCorner`와 HLSL 식을 함께 변경했다.
- 실제 스프라이트 UV와 **현재 재질의 repeat**, SpriteRenderer 색의 UNorm8 값·현재 재질 tint, 스프라이트 텍스처 슬롯을 별도로 비교한다.
- 최대 월드·UV·색 오차, 텍스처 불일치 수, 예외 수와 상위 10개를 로그한다. 이름, 슬롯, 배치, 시차, crop, repeat, flip, 텍스처 크기, 역산한 원래 이미지 크기, bounds, trueScale, tint, 색 공간, 셰이더를 함께 기록한다.
- `forceRenderingOff`를 변경하지 않는다. 진단 후 pivot의 local position/rotation/scale과 parallax의 start/end/posCamAtStart를 복원한다. 진단 명령 자체의 배열·문자열 할당은 성능 측정에서 제외한다.

사용 예:

```text
dr2 force off
dr2 now
dr2 verify
dr2 state
```

일시정지한 목표 프레임에서 실행한다. `verify`의 월드 오차가 작아도 카메라 RT 투영·필터링·정렬·합성까지 검증된 것은 아니므로 `drPix`도 반드시 실행한다.

### 좌표·UV·색 공간

확인 가능한 기존 코드의 손실 지점을 보완했다.

1. 스프라이트 UV의 min/max만 저장하던 방식을 실제 정점↔UV 대응의 affine 식으로 바꿨다. 뒤집힌 UV와 축 교환을 보존하며, 실제 사각형 네 모서리가 아닌 도형은 임의 사각형으로 확장하지 않는다. 새 off-diagonal UV 값은 기존 정적 슬롯의 빈 `ux.w`, `uy.w`를 사용하므로 32-float ABI는 유지된다.
2. 회전은 GpuDeco의 `RotationSinCos`, `ImageRotation`, `CpuQuotient`를 이식했다. Unity 이미지 회전의 반각 quaternion과 정규화를 사용하고, TRS 열을 합성한 뒤 정점을 곱하도록 바꿨다. include는 `dr2_math.hlsli`이다. 화면 고정 위치의 sincos 식은 기존 GpuDeco 방식과 같다.
3. ScreenToWorldPoint에 쓰는 가로세로 비율은 실제 `Camera.aspect`로 읽는다. 논리 시차 위치·잠금 회전은 원래 게임처럼 `controller.camy.transform`을 기준으로 읽는다.
4. Linear 색 공간의 sRGB 이미지 여부를 기존 texture API의 filter 인자 상위 비트로 전달한다. typeless 네이티브 텍스처를 sRGB SRV로 읽을 수 있게 했다. export와 함수 인자 수는 유지했다.
5. 알파 분리 텍스처, 사용자 프로퍼티 블록, 지원 밖 시차 카메라/3D 자식 회전 등은 원래 경로에 남긴다. Unity가 넣은 기본 스프라이트 프로퍼티 블록은 기존 `BlockCensus` 검사로 허용한다.
6. 정적 값/텍스처 검증 실패 시 장식을 숨긴 채 잃어버리지 않도록 원래 경로로 돌린다. 준비 중 부분 실패나 실행 중 지원 밖 구조 변경은 직통 경로를 해제한다. LazySprite의 아직 생성되지 않은 투명 스프라이트는 빈 슬롯으로 기다린다.

**위 항목 중 어느 것이 기존 WF/Exit 차이의 주원인이었는지는 실게임의 `verify`·`drPix` 전후 로그가 없으므로 확정하지 않았다.**

### 순서 경계

- 월드 캔버스는 `isActiveAndEnabled`인 경우만 경계에 포함한다.
- 비활성 캔버스도 소속 장식별로 보관한다. `GetComponentInParent(..., true)`로 비활성 부모까지 찾으며, SetVisible 후크에서 실제 캔버스 활성 상태가 바뀌었는지 확인한다.
- 끼울 수 없는 비활성/forceRenderingOff 장식 렌더러는 경계에서 제외하고, SetVisible·ApplyColor에서 가시 여부가 바뀌거나 구조/재질이 바뀌면 경계를 다시 계산한다.
- 재활성화 후크를 보장할 수 없는 외부 렌더러는 보수적으로 경계에 남긴다. v15 비활성 맵에서 쓰지 않는 장식 필터 메시 렌더러는 제외한다.
- 경계가 바뀌면 다음 메인 카메라 PreCull에서 원래 상태로 복원한 뒤 전체 소유·순서·명령 버퍼를 다시 만든다. 매 프레임 전체 Canvas/Renderer 검색은 추가하지 않았다.

경계 전환 시 재컴파일은 로딩과 같은 할당/비용이 있다. 안정 프레임의 추가 경로는 사전 생성된 컬렉션 조회만 수행한다. **장식 활성 상태가 자주 바뀌는 구간의 재컴파일 빈도·할당·스파이크는 Claude 실측 대상**이다.

### Grab 블렌드

다음 12개를 구현했다. 모드 번호는 GpuDecoBlend와 같다.

| 모드 | 번호 |
|---|---:|
| Darken / Multiply / LinearBurn | 1 / 2 / 4 |
| Lighten / Screen / LinearDodge | 6 / 7 / 9 |
| Overlay / SoftLight | 11 / 12 |
| Difference / Exclusion / Subtract / Divide | 18 / 19 / 20 / 21 |

- `GpuDecoBatchBlend.shader`의 혼합 식을 사용한다. BlendModeEffect가 Disabled mask + SelfWithScreen인 Grab 재질을 맡는다. 기존 Additive 재질은 화면 복사 없이 처리한다.
- 각 Grab 장식 직전에 현재 카메라 RTV에서 해당 장식의 화면 사각형만 `CopySubresourceRegion`으로 복사한다. 좌표 계산 오차용 2픽셀 여백을 둔다.
- 복사용 텍스처는 RT와 같은 크기로 재사용하지만 매번 복사하는 영역은 장식 사각형이다. `SV_Position` 픽셀 좌표로 `Load`하므로 복사 영역 바깥을 필터링해 읽지 않는다.
- 화면 스냅샷은 장식마다 갱신한다. 같은 재질의 인스턴스도 이전 장식이 그려진 결과를 읽는다. 겹친 장식이 한 스냅샷을 공유하는 방식으로 배칭하지 않는다.
- 지연 컨텍스트에서 unbind→copy→rebind→draw를 순서대로 기록하며 `ExecuteCommandList(..., TRUE)`로 Unity 즉시 컨텍스트 상태를 복원한다.
- 단일 sample, 2D, mip 0 RTV를 지원한다. MSAA는 관리 쪽에서 Grab 후보를 거절한다. 지원 밖 effect mask/render mode/블렌드 모드는 원래 경로에 남긴다.
- `dr2 state`에 `grab_copies/frame`, `grab_pixels/frame`을 추가했다. `sf_dr2_stat`의 9/10번 지표이며 export 추가는 없다.

### 마스크

- Additive의 마스크 대상과 지원 Grab의 마스크 대상을 네이티브 stencil 시험으로 처리할 수 있게 했다.
- 후크로 추적하는 비활성 마스크/대상과 사용하지 않는 필터 메시가 전체 마스크 소유를 막는 경우를 줄였다.
- 순서 경계 필터 뒤 마스크 대상이 탈락했으면 마스크/대상 소유도 함께 해제한다. Unity와 네이티브의 불완전한 마스크 혼용을 허용하지 않는다.
- Unity로 돌린 마스크 대상은 일반 장식의 순서 경계에도 다시 포함한다. 후보 목록에서 빼면서 경계까지 사라져 원래 렌더러와 직통 장식이 뒤섞이는 경우를 막았다.
- 배경 구간 0이 비었더라도 이후 구간의 마스크를 위해 매 프레임 stencil을 초기화하도록 수정했다. 이전 코드는 구간이 비면 초기화 전에 반환했다.
- 활성 파티클/지원 밖 셰이더 등 남는 마스크 대상이 있으면 전체 마스크를 원래 경로에 남기는 정책은 유지한다.

## 오프라인 검증

재현 명령:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/direct-renderer/check-m1b.ps1 -NativeBuild
dotnet build StutterFix.csproj -c Release
dotnet build StutterFix.csproj -c Release -p:Edition=Player
dotnet build StutterFix.csproj -c Release -p:Edition=Player -p:AutoTestBuild=1
```

`check-m1b.cpp`는 실제 `dr2.cpp`와 생성된 HLSL bytecode를 사용한다. 게임을 실행하거나 연결하지 않는 D3D11 WARP 소프트웨어 장치로 검사한다. 성능 벤치마크는 아니다.

| 검증 | 결과 |
|---|---|
| 일반 + 12개 혼합 모드, 연속 장식 1/2개 | 26조건, 최대 채널 차 2 |
| 비대칭 2×2 이미지 UV 네 방향 + 축 교환 | 5,120픽셀 정확히 일치 |
| Linear 경로 sRGB 이미지 디코딩 | 통과 |
| 8×8 장식의 복사 영역 | 여백 포함 12×12 = 144픽셀, 전체 RT 1,024픽셀 |
| 실제 VS stream-output 대 독립 CPU TRS | 40조건, 최대 월드 오차 7.62939453e-06 |
| 빈 구간 초기화 + enter/inside/leave/outside | 안쪽 64픽셀, 이후 전체 1,024픽셀 일치 |
| 즉시 컨텍스트 topology/RT 복원 | 통과 |
| HLSL VS/PS 변형 | 9개 컴파일 통과 |
| DEV / Player / PlayerAuto / native | 오류 0; 관리 빌드의 기존 경고는 남음 |
| native export 이름·ordinal·hint | 73개 보존, 추가 없음 |

증거: `out/direct-renderer/m1b/fixture.log`, `check-native.log`, `build-{dev,player,playerauto,native}.log`, `exports-{before,after,result}.txt`.
최종 관리 DLL 3종에 현재 `native/sfnative.dll` 바이트가 내장돼 있는지도 확인했다(`embedded-native.json`).
설치된 StutterFix/sfnative DLL SHA-256은 `installed-before.json`, `installed-after.json`으로 비교했다. 이 기록 사이 값은 동일했다.

## Claude 실게임 bench 설정과 합격 기준

기존 `drPix`, `drOn`, `ppGpuCam` 프리셋을 사용한다. 프리셋 실행기의 새 명령 구문은 만들지 않았다. 공통 조건은 3440 렌더, 매 판 새 재생, 실제 곡 시작 화면/RT 크기 확인, 정상 순서 모드이다.

직통 후보 공통 설정:

```text
dr2 force off
dr2 only 0
dr2 seqtest off
dr2 seq on
dr2 seqparticles on
dr2 tight on
dr2 masks on
dr2 on
```

| 프리셋 | 실행·기록 |
|---|---|
| drPix | 목표 시각 일시정지 → `dr2 now` → `dr2 verify` → `dr2 state` → d1 → `dr2 off` → d0 → 기존 framecmp. 복원 후 d0도 이전 원본과 비교 |
| drOn | 위 정상 모드로 다음 재생 준비부터 활성화. verify/화면 캡처 없이 FPS·메인/렌더 CPU·직통 비용·GC·owned/runs/segs/grab 통계를 기록 |
| ppGpuCam | 기존 대조 프리셋 그대로. `dr2 off`가 유지되는지 확인하고 같은 맵/시각/RT 조건에서 측정 |

성능은 기존 대조 대 drOn의 ABBA로 비교하고 각 판 새로 켠다. 기존 force 수치 205→229 FPS는 정상 모드의 합격 근거로 재사용하지 않는다.

| 맵/시각 | 기존 소유 수 | 기대 확인 항목 |
|---|---:|---|
| HELLO 50초 | 34 / 2531 | 비활성 Canvas 경계 제거와 Grab 지원으로 34보다 증가하는지 |
| Windflower 258초 | 5 / 6290 | 소유 수 증가, 기존 81% 차이 해소 여부 우선 확인 |
| DD 50초 | 51 / 6314 | Grab 지원으로 51보다 증가하는지, 합성 비용 동반 기록 |
| Exit 88초 | 6 / 3524 | 소유 수 증가, 기존 13.5% 큰 이미지 차이와 verify 상위 항목 확인 |

정확한 새 소유 수는 실게임 분류 결과를 받기 전 예측하지 않는다. 각 맵 목표는 다른 픽셀 비율 **<0.1%**, 최대 채널 차 **≤3**, 안정 프레임 직통 메인 비용 **<50µs/프레임**, 관리 할당 **0**이다. `commit_us`에는 각 장식의 Poll/상태 후크 시간이 전부 포함되지 않으므로 단독으로 메인 비용 합격을 선언하지 않는다.

추가 회귀: 비활성 글자를 곡 중 켜기/끄기, Grab 모드 변경, Sprite/크롭/반복 변경, 마스크 대상 활성화, off/now 반복, 재시작·맵 전환·Unload. 경계 재컴파일 프레임의 스파이크/할당도 별도로 기록한다. `verify`가 통과하는데 화면 차이가 크면 실제 RT의 Y 투영, 남는 렌더러의 정렬, 텍스처 샘플링·카메라 효과를 이어서 조사한다.

이 실게임 기록 전에는 기본 켬이나 M1b 화질·성능 목표 달성을 선언하지 않는다. 직통 렌더러는 기본 끔 상태다.
