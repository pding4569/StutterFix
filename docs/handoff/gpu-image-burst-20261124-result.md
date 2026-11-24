# GPU 이미지 효과 몰림 비용: 구현·검증 결과 (2026-11-24)

작업 위치: `E:\StutterFixWork\wt-v14prod`  
브랜치: `claude/v3-282port`  
시작 및 종료 HEAD: `db8c67e5e26e012810d0e11ffe05072824600ce0`

`docs/handoff/gpu-image-burst-20261124.md`에 따라 세 후보를 구현했다. **모두 기본 끔이다. 실제 게임·Mono·GPU에서의 동일성과 몰림 프레임 개선은 아직 미검증이다.** 게임 대조·측정은 아래 명령으로 Claude가 수행한다. 이번 작업은 FPS 개선이나 제품 기본 활성화를 승인하는 결과가 아니다.

게임을 실행하거나 종료하지 않았고, 설치 폴더 쓰기·설치 DLL 교체·커밋·푸시·스테이징을 하지 않았다. 빌드 산출물은 이 작업 트리 안에만 썼다. 기존 `AutoTest.cs`의 `planetcensus` 7줄 변경과 미추적 자료를 보존했다. 디컴파일 자료는 `E:\StutterFixWork\decompiled-20261124`에서 읽기만 했고 저장소에 복사하지 않았다.

## 구현한 후보와 보존 근거

### 1. `imagefast metadata`: 불변 enum 메타데이터

`Reasons`의 `Enum.IsDefined(typeof(Ease), f.ease)`와 배치 enum `ToString()`을 불변 메타데이터 표로 대체한다. `Enum.GetValues`와 정의된 배치 이름의 `ToString()`은 스위치를 켤 때 먼저 실행한다. 정의되지 않은 배치는 원래 `ToString()`으로 돌아간다. `movementTypeUsed=false`에서는 원래 `MovementReason`이 항상 null을 반환하므로 이름 변환도 생략한다.

- 캐시 대상은 효과 인스턴스나 `Reasons` 배열이 아니라 **로드된 enum 타입의 정의와 이름**이다. 효과의 조건·길이·이미지 사용·이징 값은 매번 원래대로 읽는다.
- `Ease.INTERNAL_Custom`/`Ease.Unset` 거부, 비유한 값 검사, 이유 문자열과 추가 순서는 유지한다. 정의되지 않은 값도 원래 정의 검사와 같은 false다.
- enum 정의는 해당 어셈블리 수명 동안 고정되어 있다. 맵·대상·소스 세대에 묶인 무효화가 필요하지 않으며 모드 어셈블리 재생성 시 정적 표도 새로 생성된다.
- `imagecheck on`이면 새 반환값을 원래 `Enum.IsDefined`/`ToString` 경로와 각 호출에서 비교한다. 대조가 꺼진 정상 경로에서는 원래 리플렉션을 병행하지 않는다.

절감 상한: `Reasons` 호출 E회당 정의 검사 E회와 정의된 배치 이름 변환 최대 E회를 준비 시점의 enum 열거 한 번으로 바꾼다. `PrepareGpuEffect`는 이미 효과당 한 번 파싱하므로 이 절감을 장식 대상 수 N배로 계산하면 안 된다.

### 2. `imagefast allocations`: 곧 덮어쓰는 기본 배열 3개 제거

`CaptureGpuImageItem`의 기존 object initializer는 `Item`이 먼저 만든 `Initial[16]`, `Constants[8]`, `Triangles[6]`를 즉시 새 배열로 덮어쓴다. 이 경로에서만 선택적 생성자로 세 기본 배열 할당을 생략한다.

- 기존 기본 생성자는 여전히 세 배열을 만든다. 준비·복구·파일 읽기·다른 fixture 호출에는 기본 생성자를 유지했다.
- 빠른 생성자는 아직 외부에 게시되지 않은 새 객체에서만 사용한다. 기존 initializer가 세 필드를 같은 순서·같은 식으로 모두 채운 다음 반환한다. 공유 풀이나 지난 이미지의 객체·배열을 재사용하지 않는다.
- 나머지 필드의 기본값·배열 길이, 기하 계산, 삼각형 변환, 16개 track 초기화, slice별 `Initial`/`Tracks`/`Anchors` 참조 교체를 유지했다. 이미지가 없는 경로는 그대로다.
- `imagecheck`는 같은 장식·variant에서 기존 생성자로 항목을 별도 캡처한다. 모든 항목 필드·배열의 바이트와 image matrix/지원 조건을 새 결과와 비교한다. 이 비교는 slice에 올리기 전에 실행된다.

절감: 비어 있지 않은 이미지 기하 캡처 N회당 배열 3N개, 원소 payload 120N바이트를 없앤다. .NET 8 x64 시험에서 헤더·정렬을 포함한 차이는 **캡처용 항목 생성당 192바이트**였다. Mono의 할당 바이트 수나 전체 `RefreshGpuImageGeometry` 할당량으로 확대하지 않는다. `LiveDecoGeometry`, sprite getter 배열, 실제로 남는 항목 배열 할당은 여전히 있다.

### 3. `imagefast uploads`: 이미 같은 GPU 기하·UV 행 쓰기 생략

`UpdateImageGeometry`는 검증과 기존 packing을 그대로 수행한 뒤, 마지막 성공한 업로드의 원시 바이트와 각각 비교한다. `_Static` 160바이트 행과 `_UV` 32바이트 행을 독립적으로 판단한다. float 수치 동등 비교나 Unity vector `==`를 사용하지 않으므로 `+0/-0`도 구분한다.

- 캐시는 **각 `GpuDecoGpu` 버퍼 소유자 인스턴스**에만 속한다. `data.Items`의 가변 객체·배열을 캐시로 쓰지 않는다. 새 소유자의 모든 행은 invalid이며 이미지 경로의 첫 업데이트는 반드시 두 버퍼를 쓴다.
- 정상 후보 선택에서는 캐시 저장 공간을 GPU 생성/준비 중 할당한다. 곡 중 첫 효과에서 전체 저장 공간을 만들지 않는다. AUTOTEST 스위치는 GPU 준비 전에만 바꿀 수 있다.
- 소스 감사상 해당 버퍼의 쓰기는 생성자의 초기 `Buffer(...)`와 `UpdateImageGeometry`뿐이다. 컴퓨트 셰이더의 `_Static`·`_UV`는 읽기 전용 `StructuredBuffer`다. 기존 mesh/VB를 교체하거나 topology를 바꾸지 않는다.
- **`SetData`가 정상 반환한 뒤에만 그 행을 commit**한다. static 쓰기 성공 후 UV 쓰기가 실패해도 성공한 행만 기록한다. 기존 호출자의 CPU 복구 경로는 그대로다. 느린 경로로 쓰면 두 캐시를 무효화한다. 소유자를 새로 만들면 지난 캐시는 전달되지 않는다.
- 두 buffer의 실제 쓰기 순서와 `data.Items[index]=next` 게시 위치는 유지한다. 달라진 행은 원래 자리에서 즉시 업로드한다. 같은 행을 다음 프레임에 올리도록 예약하지 않는다.
- `UpdateImageMatrix`, texture array copy/lease 해석, collapse texture/shape 갱신, material·MPB 쓰기, 렌더러 상태, visible/depth 및 알파 wake 순서는 그대로다. 생략하는 것은 이미 동일한 바이트가 저장된 두 입력 행의 재전송뿐이다.
- `imagecheck on`이면 생략 여부와 무관하게 실제 두 GPU 행을 `GetData`로 읽어 원래 경로가 무조건 올렸을 packed payload와 바이트 비교한다. 기하 재계산과 GPU readback 때문에 **대조 판을 성능 판으로 쓰면 안 된다.**

절감: 정상 업데이트 U회, 같은 static 행 S회, 같은 UV 행 V회이면 `SetData`는 `2U → 2U-S-V`회, payload는 `192U → 160(U-S)+32(U-V)`바이트다. 첫 업데이트에는 절감이 없다. 비교·commit 복사 비용이 추가되므로 실제 이득은 실게임에서 측정해야 한다.

저장 공간은 준비된 slice 항목 합계 M개당 payload 192M바이트와 유효성 bool 2M개, 배열/객체 헤더다. 28,523항목이면 약 5.28MiB + 헤더다. 이 추가 메모리는 기본 끔에서 할당하지 않는다.

## 요청된 병목 항목별 조사·보류 판정

| 항목 | 반복 작업 / 판정 / 같은 결과를 위해 남긴 조건 |
|---|---|
| `RunGpuImageEffect` | 대상마다 master 사전을 다시 조회하고 논리 필드 동기화→원래 이미지 setter→pending/기하 갱신을 한다. 이 순서와 stage별 복구·setter 1회 규칙은 유지했다. out 정수 인덱스 전달은 별도 후보다. 준비/복구 세대와 CPU 채택 분기를 통과하는 호출 계약까지 바꿔야 하므로 이번에는 추가하지 않았다. |
| `GpuLiveSkip` / `SkipAdopt` | 현재 `gpuLiveMasterByDeco`, owned set/visual set은 Unity 기본 동등 비교다. 참조 비교 변경은 하지 않았다. 모든 C# wrapper가 항상 정규화되고 파괴된 키·복구 때도 동등하다는 증거가 없다. 인계 문서의 기존 ownercache ABBA 무이득은 이번 측정값이 아니다. |
| `PreflightGpuImageTarget` | 매 대상의 준비 범위·header/selector·대상 집합·현재 sprite/texture ID를 검사한다. 효과 단위로 통째로 캐시하면 앞 대상의 setter/외부 콜백 뒤 자산·메타데이터 변경을 놓칠 수 있다. 기존 검사를 유지했다. `HashSet<int>.Contains` 제거에는 대체 인덱스의 준비 세대와 실제 대상 참조 guard가 필요하다. |
| `SyncGpuImageFields` | 16개 현재 채널 평가·finite 검사와 논리 필드 복사는 유지했다. 같은 프레임이라도 앞 효과가 slot/source를 바꿀 수 있으므로 프레임 캐시는 안전하지 않다. 이 프로파일의 약 17%를 제거했다고 주장하지 않는다. |
| `DecoAnim.CopyGpuImageFields` | 9개 논리 필드/시차 multiplier를 기존 FieldRef로 쓴다. 게임 setter가 뒤에서 읽는 값이다. 마지막 값 캐시에는 CPU 복구·외부 writer·배치 변경까지 포괄한 무효화가 필요하므로 쓰기 생략을 하지 않았다. |
| `InvisibleSkip.SyncGpuImageColor` / `Work` | 렌더러 색·현재 색, hidden/보임 계수, LazySprite flush와 강제 숨김의 순서에 영향이 있다. 원래 처리를 유지했다. **현재 `hidden` 사전과 `rejected` 집합은 이미 `SpriteRenderer` 참조 비교자를 사용한다.** 같은 최적화를 다시 구현하지 않았다. |
| `RefreshGpuImageGeometry` / `Item` | 변환 계층·flip·source sprite/UV를 매번 캡처하고 material/order 검사를 한다. 자산 ID만으로 variant/geometry를 장기 캐시하지 않았다. 위의 버려지는 배열 3개만 생략한다. |
| `GpuDecoGpu.UpdateImageGeometry` | topology·finite·논리 배열 참조 검사를 모두 유지하고 위의 정확한 두 행 업로드 생략만 구현했다. mutable `Item` 비교에 의존하지 않는다. |
| `ResolveImageArrayAddress` | lease `Plan.Resolve`와 `Loaded[slice]`를 읽고 필요할 때 texture를 복사한다. 같은 texture ID라는 이유만으로 호출을 건너뛰면 다른 actor가 재사용한 slice를 놓칠 수 있어 유지했다. texture/collapse MPB 갱신도 이번 생략 대상이 아니다. |
| `Reasons` 리플렉션 | 불변 enum 메타데이터만 캐시했다. 조건이나 이유 전체를 효과 인스턴스별로 기억하지 않는다. `GetPseudoCustomAttributes`가 전체 몰림 비용에서 차지한 절대 시간은 이번에 측정하지 않았다. |
| `FastMove.ImageBlock`의 keyword/texture 쓰기 | 디컴파일 `scrVisualDecoration.SetSprite`는 같은 sprite여도 sampling variant와 wrapMode를 갱신한다. setter 전체나 keyword 호출을 생략하는 변경은 하지 않았다. mask·material 공유 상태까지 독립 대조해야 한다. |
| `FastMove.CropUsed` | 선택적 FieldInfo 읽기를 유지했다. `cropPivotOffsetUsed`가 없는 게임도 다루는 경로이므로 접근자 생성 실패 시 원래 읽기로 돌아가는 별도 후보로 남겼다. |
| `TryGpuImageSprite` / `SequenceEqual` | 현재 HEAD의 `FastMove.TryGpuImageSprite` 본문은 dictionary lookup이며 `SequenceEqual`을 호출하지 않는다. 삼각형 `SequenceEqual`은 `PrepareGpuImageAdmission`에 있다. 프로파일 인라인 귀속/다른 빌드 가능성을 구분하며, 소스에 없는 hot-path 검사를 지우지 않았다. |
| 11-23의 정수 인덱스·`Renderer.enabled` 캐시 | 기존 후속 후보다. master 인덱스와 table 인덱스를 혼동하지 않아야 하며 recovery/모드 정리에서도 renderer.enabled를 쓴다. 전체 writer·무효화 증명 없이 확대하지 않았다. |

## 변경 파일 (모두 이 작업 트리 기준 절대 경로)

| 경로 | 내용 |
|---|---|
| `E:\StutterFixWork\wt-v14prod\GpuDecoImageBurst.cs` | 새 파일. 독립 스위치, enum 표, imagecheck 명령·계수·항목 oracle |
| `E:\StutterFixWork\wt-v14prod\GpuImageUploadRows.cs` | 새 파일. 소유자별 행의 원시 바이트 비교·성공 commit |
| `E:\StutterFixWork\wt-v14prod\DecoBake.cs` | `Reasons`의 enum 검사/배치 이름을 선택적 메타데이터 경로로 연결 |
| `E:\StutterFixWork\wt-v14prod\GpuDecoData.cs` | 기본 생성자를 보존하며 배열 덮어쓰기 전용 생성자 추가 |
| `E:\StutterFixWork\wt-v14prod\GpuDecoImagePlayback.cs` | 선택적 생성자와 기존 캡처 결과 대조 연결 |
| `E:\StutterFixWork\wt-v14prod\GpuDecoGpu.cs` | 준비 때 행 저장소 생성, 두 입력 버퍼 재전송 생략, AUTOTEST 실제 GPU readback 대조 |
| `E:\StutterFixWork\wt-v14prod\GpuDecoExport.cs` | 기존 `gpudeco` 명령 분기에 연결 |
| `E:\StutterFixWork\wt-v14prod\GpuDecoEffectCost.cs` | 기존 effectcheck에도 `NO_SAMPLES`/`MISMATCH`/`OBSERVED_EQUAL` 판정 출력 |
| `E:\StutterFixWork\wt-v14prod\StutterFix.csproj` | 두 새 소스 컴파일 등록 |
| `E:\StutterFixWork\wt-v14prod\tools\optimization\check-gpu-image-burst.py` | 실제 소스 추출/직접 컴파일 + 설치 어셈블리의 실제 enum을 읽는 오프라인 대조 |
| `E:\StutterFixWork\wt-v14prod\docs\handoff\gpu-image-burst-20261124-result.md` | 이 문서 |

## 수행한 검증과 한계

```powershell
py -3 tools/optimization/check-gpu-image-burst.py
dotnet build StutterFix.csproj -c Release -p:Edition=Player
dotnet build StutterFix.csproj -c Release -p:Edition=Debug
dotnet build StutterFix.csproj -c Release -p:Edition=Player -p:AutoTestBuild=1
git diff --check
```

| 검증 | 결과 / 기록 |
|---|---|
| 실제 enum 메타데이터 | 정의된 모든 Ease/DecPlacementType 값 + -4096..4096 + int 최솟값/최댓값, used 양쪽을 포함해 24,723건, 불일치 0 |
| 실제 `Item` 생성자/항목 oracle | 10,000건, 불일치 0. .NET 8 x64 할당 20,720,000 → 18,800,000바이트 (건당 192바이트 절감) |
| 실제 `UpdateImageGeometry` + 모형 buffer | 32항목에 변경/반복/부호 있는 0/유한 경계값을 섞은 24,000회. 매번 전체 static/UV buffer와 게시 항목 비교, 불일치 0. static 쓰기 24,000→13,030회, UV 쓰기 24,000→2,798회. **이 분포는 인공 입력이며 Arche 절감률이 아니다.** |
| 실패·수명 | static/UV 각각 복사 전·후 실패 4조건, 재시도, 비유한 기하/토폴로지/0 상수 거부 3조건, 소유자 교체, 스위치 off→on, 이웃 행 격리, 최초 쓰기, ±0/NaN payload 구분 통과. NaN 기하는 실제 메서드에서 계속 거부한다. |
| 오프라인 로그 | `out/gpu-image-burst-20261124/offline-check.txt`. 실제 Unity Transform, GPU/driver, Mono는 실행하지 않는다. 따라서 로그의 게임용 `imagecheck` item 표본은 0이고 `verdict=NO_SAMPLES`가 정상이다. `offline_image_burst=PASS`와 게임 검증 통과를 혼동하지 않는다. |
| Release Player | 오류 0, 경고 69. `out/gpu-image-burst-20261124/player-build.txt` |
| Release Debug | 오류 0, 경고 89. `out/gpu-image-burst-20261124/debug-build.txt` |
| Release Player + AUTOTEST | 오류 0. 최종 증분 빌드 경고 4 (`autotest-build.txt`); 같은 최종 소스의 앞선 재컴파일은 경고 66, 오류 0이었다. 빌드 경고는 0이라고 주장하지 않는다. |
| 공백·충돌 검사 | `git diff --check` 통과. 기존 AutoTest.cs 7줄 변경 보존 |

세 빌드 DLL의 경로와 SHA-256은 `out/gpu-image-burst-20261124/artifacts.json`에 기록했다. Claude 측 시험 전에 실제 사용하는 PlayerAuto DLL과 대조한다.

## Claude 실행: 대조 → 픽셀/복구 → 성능

다음은 작성·소스 검토한 명령이며 **이번 작업에서는 실행하지 않았다.** 같은 `bin/PlayerAuto/StutterFix.dll`로 별도 프로세스를 사용한다. 기존 runner의 안전한 설치·복원 절차를 사용하고 `GpuDeco=1`, `GpuDecoImages=1` 및 나머지 기존 GPU/array/collapse 설정을 판마다 같게 유지한다. `effectcache`와 `effectbatch`는 현재 HEAD의 기본값인 on을 유지한다. 새 스위치는 반드시 `game` 앞에서 선택한다.

### A. Arche 실제 버퍼·항목·효과 대조

아래 경로는 로컬에서 존재를 읽기 확인했다(42,350,829바이트). 이미지 checker가 매우 느리므로 대기 제한을 충분히 둔다. timeout은 대기 단계의 최대 시간이며 곡을 seek하는 명령이 아니다.

```text
timeout 1800
auto on
gpudeco packets off
gpudeco effectcache on
gpudeco effectbatch on
gpudeco costprobe off
gpudeco imagefast on
gpudeco imagecheck reset
gpudeco imagecheck on
gpudeco effectcheck reset
gpudeco effectcheck on
slotcheck on
game D:\얼불춤 맵 파일\Arche\[𝐃𝐎 𝐍𝐎𝐓 𝐎𝐏𝐄𝐍 𝐓𝐇𝐈𝐒 𝐀𝐓 𝐇𝐎𝐌𝐄] FULL.adofai
press
waitsongpos 280 1700
gpudeco images playback state
gpudeco images requireactive
gpudeco imagecheck status
gpudeco imagecheck require
gpudeco effectcheck status
slotcheck
gpudeco imagecheck off
gpudeco effectcheck off
slotcheck off
quit
```

위 통과 조건은 **metadata/item/upload의 각 fast_checks>0, static_skipped+uv_skipped>0, mismatch=0 및 verdict=OBSERVED_EQUAL**이다. `imagecheck require`는 후보 또는 생략 표본이 없으면 실패한다. effectcheck도 checks>0·whole_checks>0·두 mismatch=0, slotcheck의 기존 불일치 0·shadow_checks>0이어야 한다. GPU active·이미지 setter>0·recoveries=0·native copy 오류 없음도 확인한다. 어떤 checker든 표본 0이면 동일성 성공으로 세지 않는다.

새 명령 전체:

```text
gpudeco imagefast on|off
gpudeco imagefast metadata on|off
gpudeco imagefast allocations on|off
gpudeco imagefast uploads on|off
gpudeco imagefast status
gpudeco imagecheck on|off
gpudeco imagecheck reset
gpudeco imagecheck status
gpudeco imagecheck require
```

모두 AUTOTEST 전용이다. 부분 후보 대조에서는 활성 후보의 fast_checks와 mismatch를 개별 판정한다. `imagecheck require`의 종합 판정은 세 후보를 모두 실제로 실행해야 통과하므로 부분 후보 판에는 그대로 적용하지 않는다.

### B. 픽셀·렌더러·복구 확인

새 checker는 기하·UV 입력과 캡처 항목을 검증한다. 원래 setter를 두 번 실행하거나 렌더러 상태/순서를 독립 재생하지 않는다. 기존 framecmp/복구 runner에서 같은 곡 시각에 **imagefast off/on**을 별도 판으로 비교해야 한다.

- Arche 219.6초(스칼라 효과 몰림), 258.00초(이미지 몰림), 이후 270.25초/280초 경계를 포함한다. 기존 G0/픽셀 허용 기준을 낮추지 않는다. 가능하면 같은 기준의 차이 0을 확인하고, 버퍼·순서·복구 상태에는 근사 허용을 도입하지 않는다.
- Exit: `D:\얼불춤 맵 파일\Exit from ship\Exit from SHiP_T2_.adofai`(존재 확인, 4,171,628바이트), 1.04초와 직후 경계. A 절차에서 맵과 도달 시각을 `waitsongpos 5 120`으로 바꾼 판을 추가한다. 유효 이미지 표본이 없으면 해당 맵 검증은 미성립으로 남긴다.
- 기존 paused 복구 명령 `gpudeco recovery preflight`, `gpudeco recovery target_after_channel`, `gpudeco recovery render_before`, `gpudeco recovery render_after`는 각자 새로 준비한 판에서 실행한다. 이미지 업로드 복구는 GPU가 살아 있는 이미지 효과 직전에 `gpudeco images failupload`를 넣고 효과 직후 `gpudeco images requirefallback`으로 CPU 복귀 완료를 확인한다. 주입 판의 FPS는 사용하지 않는다. 실패 후 동일 효과가 두 번 적용되지 않는지, 다음 맵/재시작/종료 복원을 확인한다.

### C. 계측용 호출 분해 (성능 승인용 판과 분리)

A의 준비 부분을 사용하되 `imagecheck off`, `effectcheck off`, `slotcheck off`로 한다. `waitsongpos 217 700` 다음 `profsamp begin 2000 full max=200000` → `waitsongpos 280 700` → `profsamp end frames` → `gpudeco imagecheck status` → `quit` 순서다. 219.6초·258초·274.8초의 동일 프레임/효과 대상 수와 포함·리프 표본을 비교한다. `profsamp`의 스레드 정지 비용 때문에 이 판의 FPS나 ms를 실제 개선치로 쓰지 않는다.

Exit는 `press` 앞에서 sampler를 시작하고 `waitsongpos 5 120` 뒤 끝내 1.04초 첫 몰림을 잡는다. 이 판도 FPS 승인용이 아니다. `max`에 도달하거나 프레임/곡 시각이 맞지 않는 자료는 완전한 구간 대조로 세지 않는다.

### D. capture 없는 성능 ABBA

후보별 각 4판을 독립 프로세스로 실행한다. A=세 후보 off, B=해당 후보만 on, 실행 순서는 **A-B-B-A**다. metadata·allocations·uploads를 각각 분리한 뒤 결합 on도 따로 비교한다. 기존 effectcache/effectbatch/array/collapse 및 표시/그래픽 설정은 고정한다. 기본 스위치 묶음은 다음과 같다.

```text
timeout 900
auto on
gpudeco packets off
gpudeco effectcache on
gpudeco effectbatch on
gpudeco costprobe off
gpudeco imagefast off
gpudeco imagecheck reset
gpudeco imagecheck off
gpudeco effectcheck off
slotcheck off
game D:\얼불춤 맵 파일\Arche\[𝐃𝐎 𝐍𝐎𝐓 𝐎𝐏𝐄𝐍 𝐓𝐇𝐈𝐒 𝐀𝐓 𝐇𝐎𝐌𝐄] FULL.adofai
press
waitsongpos 217 800
fpsmark start
waitsongpos 222 120
fpsmark end imageburst-219
waitsongpos 257.5 120
fpsmark start
waitsongpos 260 120
fpsmark end imageburst-258
waitsongpos 280 120
gpudeco images playback state
gpudeco images requireactive
gpudeco imagecheck status
quit
```

B 판에는 공통 `gpudeco imagefast off` 바로 뒤에 해당 후보의 `gpudeco imagefast metadata on`, `gpudeco imagefast allocations on`, `gpudeco imagefast uploads on`, 또는 결합 `gpudeco imagefast on` 한 줄을 넣는다. Exit는 맵을 위 Exit 경로로 바꾸고 `press` 뒤 `waitsongpos 0 120` → `fpsmark start` → `waitsongpos 5 120` → `fpsmark end imageburst-exit-first` → 상태/requireactive → `quit`으로 한다.

FPS 구간 평균은 보조값이다. 목표는 해당 몰림 프레임의 시간과 최악 프레임이다. 기존 runner/Player.log의 프레임 시각·효과 비용·곡 최악/정상 프레임을 함께 비교한다. 필요하면 별도 진단 판의 `slowfx arm 217 280 0` → 재생 → `slowfx report`로 대상 효과와 대상 수를 확인한다(`dropped>0`이면 전체 효과 증거가 아니다). outer StartEffect 경과시간을 CPU 실행시간이나 GPU wait로 단정하지 않는다. 메인/렌더/GPU 시간도 서로 합산하지 않는다.

성능 판의 `imagecheck status`는 checker가 꺼져 있으므로 fast_checks=0/NO_SAMPLES가 정상이다. 이때 static/UV uploads/skipped는 호출 수 참고값이며, A에서 별도로 통과한 동일성 판을 대신하지 않는다. 실게임 결과가 나오기 전에는 세 후보를 계속 기본 끔으로 둔다.

## Claude 게임 확인 (2026-11-24)
- 대조(Arche 280초까지, imagefast 셋 다 켬): imagecheck metadata 197,580·item 5,148·upload 5,148 불일치 0 (OBSERVED_EQUAL), 업로드 생략 96/5,148(2%). effectcheck 1,878/1,098 불일치 0, slotcheck shadow 3,858 불일치 0.
- 몰림 프레임 ABBA(끔/켬/켬/끔, dropscan 215~280초): 219.6초 22.6/24.6/26.1/25.2ms, 258.0초 21.0/20.8/20.4/22.4ms, 274.8초 18.6/18.3/19.1/18.4ms, 평균 프레임 2.576/2.578/2.584/2.593ms → 잡음 안, 이득 없음.
- 판정: 기각. 코드는 patches/gpu-image-burst-noGain 에 보관하고 작업 트리에서 되돌림.
