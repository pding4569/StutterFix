# 장식 시차 공유 연구
결론: 같은 시차 배율만으로 최종 위치 계산을 공유하는 후보는 비트 일치 조건을 충족하지 못했다. [B1]
작성값 기준 N/K는 Arche 28,835/2,091, HELLO 2,476/400, Windflower 6,175/355이다. [S1]
전체 피연산자가 같은 합성 표본에서는 공유 이득이 있었으나, 배율만 공유한 후보는 비등가이고 이번 벤치에서 더 느렸다. [B1]
카메라 읽기·줌·일부 회전/이징은 이미 공유된다. 실제 게임의 CPU 절감률·GPU 비용 비중·FPS 이득은 확인 못 함. [M1–M5]

## 조사 범위와 재현

인계 명세에 따라 기존 소스·원본 맵을 수정하지 않고 이 문서와 `tools/parallax-lab/`만 추가했다. 게임 실행, 모드 빌드·설치, 커밋은 하지 않았다. 아래의 빌드는 독립 콘솔 도구만 대상으로 한다. 지정된 Arche·HELLO 2026·Windflower 파일만 열었으며 다른 맵 폴더는 탐색하지 않았다.

조사 시점의 브랜치·HEAD, 설치 DLL의 크기·SHA256, 조사한 기존 소스와 도구의 SHA256은 [provenance.json](../../tools/parallax-lab/evidence/provenance.json)에 있다. 디컴파일은 ILSpy의 정적 읽기이며 DLL 코드를 실행하지 않았다. DLL 버전이 달라지면 보존된 디컴파일 줄을 기준으로 이 보고서를 읽어야 한다.

전체 재현 명령은 저장소 루트의 PowerShell에서 다음과 같다. 필요 환경과 개별 명령은 [도구 README](../../tools/parallax-lab/README.md)에 정리했다.

```powershell
& .\tools\parallax-lab\run-offline.ps1
```

원자료의 라운드별 시간·필드별 분포는 그대로 보존했다. 숫자의 간결한 출처는 [결과 색인](../../tools/parallax-lab/evidence/summary.md)이며 아래 [S1]·[B1]은 해당 파일의 줄 번호를 가리킨다. 게임 디컴파일과 기존 소스의 근거 위치는 문서 끝에 모았다.

## 게임의 실제 계산 경로

일반 커스텀 맵의 주 경로는 다음과 같다. `scrVisualDecoration`이 독자적인 시차 수식을 갖는 것이 아니라 공통 `scrDecoration` 경로를 사용한다. [G1–G3]

```text
scrDecorationManager.LateUpdate
  → allDecorations를 원래 순서로 순회
    → scrDecoration.LogicUpdate
      → GetVisible()이면 UpdatePosition
        → pivotPosVec + 행성/타일 추종 위치
        → pivotTrans.localPosition 쓰기
        → parallax.SetNewStartPosition / posCamAtStart 갱신
        → scrParallax.SetTrans
        → SetRotation / SetScale
```

`scrParallax.LateUpdate`도 `SetTrans`를 호출하지만, 커스텀 레벨에서는 `scrDecoration.Awake`가 이 컴포넌트를 비활성화한다. 따라서 두 LateUpdate가 항상 중복 실행된다고 세면 안 된다. 반대로 이동·피벗·시차 오프셋 setter가 `UpdatePosition` 또는 `SetTrans`를 직접 호출하므로, 변경 이벤트가 있는 프레임의 실행 횟수를 장식당 한 번으로 고정할 수도 없다. [G1:112–115, G2:199–208,229–280, G3:173–198]

### 일반 SetTrans가 하는 일

`C`는 카메라 위치, `A`는 `posCamAtStart`, `S`는 `startPosition`, `M`은 시차 배율, `O`는 시차 오프셋, `T`는 텍스처 크기 배율이다. 코드의 괄호와 순서를 유지하면 다음과 같다. [G1:79–105, G2:176–178]

```text
U = (C - A).xy * M + S
camScale = (lockScale ? orthographicSize * 0.2f / (camZoom / 100f) : 1f)
           * (scaleMultiplier * textureScaleMultiplier)
end = U + parallaxOffset * camScale / textureScaleMultiplier
```

| 작업 | 장식별 실행 내용 | 근거 |
|---|---|---|
| 참조·필드 | 초기화 가드, cachedTransform, startPosition, posCamAtStart, multiplier_x/y, 축 고정·화면 고정 플래그, decoration 참조 | G1:56–105 |
| Transform 읽기 | 현재 장식 world position, 일반 분기의 cameraTransform.position | G1:82,92 |
| 일반 위치 산술 | XY 각각 뺄셈→곱셈→덧셈. 선행 Vector3 뺄셈에는 버리는 Z 연산도 있다 | G1:92, G5 |
| 추가 오프셋 | 장식별 parallaxOffset·camScale·textureScale를 읽어 성분별 곱셈→나눗셈→덧셈 | G1:102–104, G2:176–178 |
| 결과 쓰기 | endPosition 필드, cachedTransform.position. 원래 Z는 보존 | G1:103–104 |
| 화면 고정 분기 | pixelRect와 screenRelativePos로 ScreenToWorldPoint. 일반 M 수식을 사용하지 않음 | G1:84–90 |
| 별도 회전·크기 | startRot+rotAngle, 필요하면 바닥/카메라 각도, Quaternion.Euler; scaleVec·camScale·부모 크기 적용 | G2:283–302 |

즉 보이는 장식의 일반 `UpdatePosition`은 localPosition·world position·rotation·localScale 쓰기 **네 번**을 포함한다. 이는 소스상 호출 수이며 Unity 네이티브 작업의 시간 비율을 뜻하지 않는다. `camScaleMultiplier`도 시차 오프셋과 SetScale에서 각각 평가된다. [G2:403–422, G1:102–104, G2:298–302]

### 무엇이 정말 공통인가

카메라의 위치·줌 인자·각도 자체는 같은 카메라/같은 호출 시점이면 공통이다. 그러나 `(C-A)*M`에는 장식별 A가 들어간다. 일반 `UpdatePosition`은 A와 S를 현재 장식 기준 위치로 다시 설정한다. 따라서 M이 같아도 U는 같지 않다. `relativeTo=Tile`은 원본 position에 해당 타일의 world position을 더하므로 JSON position까지 같아도 A가 같다는 보장은 없다. [G2:403–416,521–551]

수학적으로는 `C*M + (S-A*M)`로 분리할 수 있고, A=S이면 `C*M + A*(1-M)`이다. 하지만 원식의 뺄셈·곱셈·덧셈 반올림 지점이 바뀐다. 이번 오프라인 비교도 불일치를 확인했다. 큰 좌표의 상쇄, 음수 시차, 부호 있는 영, NaN을 포함하는 키를 허용 오차로 합쳐서는 비트 일치를 보장할 수 없다. [B1, 도구 Program.cs의 Exact/Regroup/SelfTest]

**일반 시차 위치 수식에는 sin/cos가 없다.** 회전 변환은 별도 작업이다. 같은 시차라는 이유만으로 장식의 최종 회전각·크기·텍스처 배율까지 같아지지는 않는다. [G1:79–105, G2:283–302]

## 모드의 현황

| 경로 | 시차 처리와 공유 상태 | 근거 |
|---|---|---|
| FastMove | MoveDecorations의 배율 목표를 정규화하고 장식별 애니메이션 레코드를 만든다. 오프셋은 사용 축만 생성. 같은 배율인 서로 다른 장식의 카메라 항을 묶는 캐시는 찾지 못함 | FastMove.cs:680–688,716–747; DecoAnim.cs:319–339 |
| DecoAnim 계열 | 배율 키는 multiplier를 직접 갱신하고, 오프셋 키는 게임 setter를 부른다. 같은 M을 가진 장식끼리의 공유는 아님 | DecoAnim.cs:410,431–449 |
| DecoAnim.GroupStep | 같은 **장식 객체**의 같은 시각/이징 등 레코드를 묶는 기능. GroupKey에도 시차 배율·오프셋은 들어 있지 않음 | DecoAnim.GroupStep.cs:37–58,97–112 |
| MoveApply / TightLate | 중복 위치 적용을 LateUpdate로 미루는 기존 경로가 있다. TightLate.List는 카메라 위치를 manager 호출당 한 번 읽지만 Apply는 각 장식에서 원래 시차 수식을 계산 | MoveApply.cs:220–259; TightLate.cs:161–180,384–420 |
| TightLate 회전 | 직전 최종 각도의 비트와 Quaternion 결과를 메모한다. 같은 시차 그룹이 아니라 연속된 같은 각도 재사용 | TightLate.cs:36–37,400–407 |
| DecoEngine | 상태를 장식별로 관리하고 Late에서 TightLate.EngineApply를 호출한다. CheckLate도 장식별 원식을 재계산해 검증 | DecoEngine.cs:578–610; TightLate.cs:340–341 |
| DecoBake 계열 | 장식별 논리 채널에 multiplier XY와 parallaxOffset XY를 저장하고 이벤트로 갱신한다. 베이크/재개·서명 대조에서도 장식별 값을 유지 | DecoBake.cs:235–247,346–350,399; DecoAnim.Bake.cs:25,78–81; DecoBake.AdoptSig.cs:75–88 |
| GPU 입력 | 카메라 XY/각도/줌 인자를 CPU에서 만들어 uniform으로 공유. kernel이 각 장식의 현재 multiplier/position/offset을 사용 | GpuDecoExport.cs:1312–1322; GpuDecoGpu.cs:241–246; GpuDeco.compute:246–260 |
| GPU live 이징 | source별로 ZeroTween.Eval을 계산해 `_SourceE`에 공급하고 각 채널이 참조한다. 이미 공유하는 부분이나 시차 값별 그룹과는 다름 | GpuDecoExport.cs:614–655,1306; GpuDeco.compute:23–25,357–371 |

GPU의 `Evaluate`, `EvaluateLive`, `EvaluateImageLive` 등은 장식 인덱스로 실행되고 `WriteVerticesAt`에서 시차를 계산한다. 시차 값으로 묶는 중간 결과 버퍼는 이 경로에서 찾지 못했다. 일반 world 식은 장식별 벡터 연산이고, 이미지 분기에는 별도의 정확도 경로에서 위치·오프셋 계산이 다시 등장한다. 회전은 별도 `Rotation`/`ImageRotation`이며 이미지에는 보정된 나눗셈도 있다. [M4:246–285,376–440]

GPU가 이미 카메라 uniform을 받으므로 CPU에서 같은 카메라를 장식 수만큼 조회하는 비용을 GPU 그룹화의 이득으로 다시 셀 수 없다. 반대로 GPU의 실제 시차 비용이 무시 가능하다고 확정할 자료도 없다. 셰이더 정적 코드만으로 GPU 시간 비중·추가 버퍼 읽기·dispatch/barrier 비용을 정량화하지 않았다. [M4, M5]

## 맵 통계

모든 AddDecoration/AddText/AddObject를 `decorations`와 `actions`에서 수집했다. 비활성 추가 이벤트도 전체 N에 포함하되 이번 세 파일의 추가 이벤트는 전부 활성이다. 누락 필드를 임의의 기본값으로 치환하지 않는다. 시차 쌍은 float32 비트로 비교하므로 공백·숫자 표기의 차이는 합쳐지고 부호 있는 영은 구별된다. [S1, map-stats.json의 ActiveAdds/MissingOrInvalidParallax]

| 맵 | 장식 N | 배율 쌍 K | 관련 필드 조합 K | 최대 묶음 | 시차 비영 장식 |
|---|---:|---:|---:|---:|---:|
| Arche | 28,835 | 2,091 | 2,095 | 618 | 28,217 / 97.85677% |
| HELLO 2026 | 2,476 | 400 | 489 | 843 | 1,633 / 65.95315% |
| Windflower | 6,175 | 355 | 485 | 340 | 5,835 / 94.49393% |

출처: [S1] `summary.md:5–15`. 관련 조합은 `parallax, parallaxOffset, relativeTo, lockScale, lockRotation, scaleMultiplier, stickToFloor`의 작성값이다. 관련 조합의 최대 묶음은 같은 출처에서 각각 **618 / 755 / 230**이다. 최종 world 위치의 동일 그룹 수가 아니다.

장식 종류의 세부 수는 Arche 이미지/글자 **28,828/7**, HELLO 이미지/글자/객체 **2,426/34/16**, Windflower **5,603/38/534**이다. 이미지 장식만을 대상으로 한 GPU 경로의 N으로 위 전체 N을 사용하면 안 된다. [S1:5–9]

묶음 분포를 보면 Arche는 **618개 묶음 하나, 501개 묶음 아홉, 359개 묶음 마흔아홉**이 있다. HELLO의 상위 크기는 **843,452,91,61,56**, Windflower는 **340,283,270,243,68**이다. 단독 묶음 수도 각각 **351/185/63**이다. 전체 크기별 묶음 수와 그에 속한 장식 수는 `map-stats.json -> Maps[].Data.InitialParallax.SizeHistogram`에 있다. [S1:11–15; Arche의 501/359 분포는 해당 JSON 경로]

추가로 position·pivotOffset·scale·rotation·image·originalSize 작성값까지 나눈 proxy K는 **4,263/2,252/5,593**이다. 이것도 타일 좌표, 실제 텍스처 배율, 부모 Transform, 시각별 보간값을 포함하지 않으므로 비트 안전 캐시 키나 실제 런타임 K로 사용하지 않는다. [S1:5–9, G2:521–551]

### 시간에 따라 바뀌는 값

게임은 MoveDecorations의 공백 구분 태그를 정확 일치로 합집합 선택한다. 빈 태그는 커스텀 맵 Setup과 이벤트 Decode 모두 `NO TAG`로 처리한다. 이벤트 필드가 있어도 오프셋 축이 null이면 그 축은 변경하지 않는다. 두 축이 모두 null인 이벤트를 변화로 세지 않았다. [G2:727–745, G3:109–123, G4:190–229,486–492]

| 맵 | Move 총수 | 배율 필드 이벤트 / 대상 수 | 유효 오프셋 이벤트 / 대상 수 | 관련항 대상 합집합 |
|---|---:|---:|---:|---:|
| Arche | 95,211 | 0 / 0 (0%) | 0 / 0 (0%) | 0 (0%) |
| HELLO 2026 | 2,867 | 0 / 0 (0%) | 91 / 148 (5.97738%) | 148 (5.97738%) |
| Windflower | 2,257 | 34 / 988 (16%) | 53 / 192 (3.10931%) | 1,149 (18.60729%) |

출처: [S2] `summary.md:17–23`. 관련항은 배율·유효 오프셋·LastPosition 이외의 배치 기준이다. HELLO의 비활성 이동 이벤트 **하나**는 대상 집계에서 제외했다. 오프셋 필드 존재 이벤트는 HELLO **2,704개**, Windflower **2,060개**이나, 각각 **2,613/2,007개**는 `[null,null]`이라 유효 변경에서 제외됐다. [S2; map-stats.json의 MoveFields.parallaxOffset.Values]

이 표의 비율은 **변경될 수 있는 대상 비율의 상한**이다. 값이 같은 목표, 중간 덮어쓰기, 조건 이벤트의 미실행, 실제로 끝나지 않은 보간을 모두 실제 변화로 확정하지 않았다. Windflower의 배율 목표는 `[0,0]`, `[100,100]`, `[-33,-33]`의 **세 종류**이고, 대상 중 **910개**는 초기값과 다른 목표가 적어도 한 번 작성되어 있다. 그렇다고 실제 재생 변화율을 910/N 또는 988/N으로 확정할 수는 없다. **실제로 시간이 지나며 다른 비트값을 갖는 장식 비율은 확인 못 함.** [S2:21, S3:29; map-stats.json의 MoveFields.parallax.Values]

작성 이벤트만 보면 Arche·HELLO에는 배율 변경이 없고 Windflower에는 있다. 하지만 이동·회전·스케일 이벤트는 별개로 있으므로, 배율이 정적이라는 사실이 완전 피연산자 그룹도 정적이라는 뜻은 아니다. RepeatEvents는 각각 **13/43/13개**, Windflower에는 조건 이벤트도 있다. 반복은 새 목표값을 만들지 않을 수 있어도 시간별 동시 그룹 수에는 영향을 주며, 이 도구는 반복·조건·easing의 전곡 시간표를 복제하지 않는다. 초기+이동 목표의 고유 배율 수는 **2,091/400/355**이며 보간 중간값의 종류 수는 아니다. [S3:25–29; map-stats.json의 ControlEvents]

### 원시 JSON과 실제 로딩값의 차이

Arche는 버전 **11**, 비어 있지 않은 components가 **228개**다. 게임의 구버전 components 마이그레이션은 lockPos에 따라 배율을 덮어쓰거나 회전·스케일 잠금 값을 바꿀 수 있다. 이 연구의 K는 원시 작성 필드 통계로 유지했으며 마이그레이션·임의 컴포넌트 실행 결과를 런타임 K로 주장하지 않는다. HELLO와 Windflower의 버전 및 components 수도 원자료에 남겼다. [S3:25–29, G6:449–478]

Arche의 `actions` 닫는 괄호 다음에는 루트 속성 구분 쉼표가 빠져 있다. 도구는 이 위치만 검사해 메모리상 쉼표를 보충했다. Windflower의 문자열 내부 제어문자 **하나**는 동등한 이스케이프로 바꿔 파싱했다. 원본 바이트 SHA256은 정규화 전 값이며 파일에는 쓰지 않았다. 통계 프로세스 peak working set은 **710,537,216 bytes / 677.62MiB**였다. [S3:27–31; 원본 Arche:108687–108690; 도구 Stats/EscapeStringControls]

## 오프라인 마이크로벤치와 절감 한계

측정은 .NET 콘솔의 순수 산술·배열 쓰기다. N/K만 맵과 맞췄으며 실제 맵의 좌표·배율 분포·활성 장식 수를 넣지 않았다. 각 표본은 **600프레임씩 9라운드**, seed **726**이며 warmup 후 측정 순서를 순환시켰다. 런타임은 **.NET 8.0.6 / Windows 10.0.19045 x64**, tiering과 HW intrinsics를 껐다. 이 설정과 각 라운드·최솟값·최댓값은 원자료에 있다. Unity Mono나 실제 GPU 성능으로 옮길 수 없다. [B1:33–44]

벤치 명령(저장소 루트, 빌드 후):

```powershell
$env:DOTNET_TieredCompilation='0'
$env:DOTNET_EnableHWIntrinsic='0'
dotnet tools/parallax-lab/bin/Release/net8.0/ParallaxLab.dll bench 28835 2091 600 9
dotnet tools/parallax-lab/bin/Release/net8.0/ParallaxLab.dll bench 2476 400 600 9
dotnet tools/parallax-lab/bin/Release/net8.0/ParallaxLab.dll bench 6175 355 600 9
dotnet tools/parallax-lab/bin/Release/net8.0/ParallaxLab.dll bench 28835 28835 600 9
```

| 합성 조건 | 개별 원식 ms/프레임 | 완전 피연산자 공유 ms/프레임 | 배율만 공유 ms/프레임 | 원식→완전공유 차이 |
|---|---:|---:|---:|---:|
| Arche N/K | 0.296392 | 0.113513 | 0.352046 | 0.182879ms |
| HELLO N/K | 0.023630 | 0.012135 | 0.030435 | 0.011495ms |
| Windflower N/K | 0.057443 | 0.023105 | 0.069027 | 0.034339ms |
| Arche N=K 대조 | 0.314826 | 0.443237 | 0.611820 | −0.128410ms |

출처: [B1] `summary.md:35–40`. 표의 개별 원식은 같은 입력이 반복되는 표본이다. 배율만 공유 후보의 공정한 위치상이 원식 기준값은 각각 **0.302546/0.023455/0.057900/0.304078ms**로 같은 출처에 따로 기록했다. 그 기준과 비교해도 이번 표본의 배율만 공유 후보는 더 느렸다.

작업은 XY 결과만 쓰며 Unity Transform·색·정점·draw는 없다. 미리 정한 group ID 조회의 나머지 연산 비용은 포함하되, dictionary 해시/키 확인/그룹 재구축/이벤트 무효화/동기화 비용은 제외했다. 완전공유 열은 **위치·오프셋·배율 등 모든 산술 입력이 K종으로 반복되는 유리한 합성 조건**이다. 실제 맵의 시차 K만 보고 이 열의 절감량을 예상 이득으로 사용할 수 없다.

모든 라운드가 같은 시간인 것도 아니다. 예를 들어 Arche 개별 원식은 **0.284606–0.302788ms**, 완전공유는 **0.109823–0.120695ms**, 배율만 공유는 **0.338023–0.423524ms** 범위였다. 이 차이를 게임의 성능 변동으로 해석하지 않는다. [bench-arche.json의 Samples.*.MinMsPerFrame/MaxMsPerFrame]

### 비트 비교 결과

| 표본 | 재결합 불일치 / XY 비교쌍 | 완전 피연산자 캐시 결과 불일치 |
|---|---:|---:|
| Arche N/K | 367,433 / 490,195 | 0 |
| HELLO N/K | 31,555 / 42,092 | 0 |
| Windflower N/K | 77,896 / 104,975 | 0 |

출처: [B1:37–39]. 비교는 성분의 float 비트 기준이며 XY 중 하나라도 다르면 해당 쌍을 불일치로 센다. scalar 연산별 반올림을 고정한 원식과 재결합 후보를 비교했고, 실제 캐시 배열의 결과 및 inlined 원식도 별도로 대조했다. 이 데이터는 .NET의 합성 계산 근거이며 게임 Mono·Unity 네이티브 Transform·셰이더의 비트 일치 증명이 아니다. 각 JSON의 `Parity.First`에는 첫 반례의 좌표·입력·출력 비트가 있다.

요청된 sin/cos 비교도 가상 커널로 넣었다. Arche 합성 표본은 개별 **0.555736ms**, 공유 **0.131992ms**였지만 일반 시차 위치에 이 작업이 없으므로 이 절감을 게임의 시차 이득에 더하지 않는다. GPU의 최종 회전각에는 장식별 각도도 들어간다. [B1:37, G2:283–295, M4:256–261]

### 게임의 절감 상한으로 말할 수 있는 것

K종만 계산할 수 있는 독립 공통 항이 정말 있고 관리 비용이 없다면, 그 항의 **연산 횟수** 감소율은 `1-K/N`이다. 위 작성값으로는 **92.74840% / 83.84491% / 94.25101%**다. 이는 전체 장식 갱신이나 프레임 시간의 감소율이 아니다. [S1:11–15]

기존 프레임 CPU 시간 중 해당 공통 항의 비율을 `f`, 그룹 관리 추가 시간을 기존 프레임 시간으로 나눈 값을 `h`라 하면 단순 모델의 시간 절감은 최대 `f*(1-K/N)-h`이다. 실제로는 화면 고정 분기·장식별 기준점·피연산자 차이 때문에 이 K조차 안전 그룹 K가 아닐 수 있다. **f와 h, 따라서 게임 장식당 비용 중 카메라 의존 항의 실제 %는 확인 못 함.** 네이티브 Transform 접근·회전·가시성·렌더러·GPU 시간 없이 그 %를 오프라인 산술로 추정하지 않았다.

## 구현한다면 넣을 위치와 검증 기준

현재 근거로는 같은 M을 키로 한 최종 위치 캐시를 채택할 수 없다. 연구를 이어갈 경우에는 다음 범위가 타당하다. 아래는 구현 후보이며 이번 조사에서 기존 코드에 적용하지 않았다.

1. **카메라 조회·줌 인자 공유**: CPU 후보는 `TightLate.List`가 만든 호출 시점 snapshot과 `TightLate.Apply`. 카메라 위치 snapshot은 이미 있으므로 추가 이득으로 중복 계상하지 않는다. 확대된 CPU fallback의 줌/각도 조회를 공유하려면 기존 Eligible/Compatible 검사와 읽기 시점을 보존해야 한다. [M1, TightLate.cs:265–284,352–377]
2. **완전한 피연산자 일치의 결과 공유**: 기존 일반 빠른 경로의 허용 조건 안에서 `A/S/M/O/camScale/textureScale`의 실제 비트가 모두 같은 경우만 후보로 삼을 수 있다. 가능하면 전체 결과보다 동일 부분식의 입력을 비교한다. 화면 고정·축 고정·부모/행성 추종은 별도 경로로 두며 Transform 쓰기 순서와 각 장식의 endPosition 갱신을 유지한다. 단순한 시차·작성 position 키는 부족하다. [G1, G2, M1]
3. **회전 재사용**: 같은 최종 각도 재사용은 별도 후보이나 CPU에는 이미 EulerMemo가 있다. GPU에서 연구한다면 M이 아니라 `startRot+angle+lockRotation의 카메라 각도` 결과의 비트를 키로 삼아야 하며, 기존 Rotation/ImageRotation의 서로 다른 경로를 보존해야 한다. [M1:400–407, M4:141–185,256–261]
4. **GPU 시차 공유 버퍼**: 넣을 위치는 `WriteVerticesAt` 직전이며 입력은 장식별 현재 live 채널이다. 단순 벡터 산술을 줄이는 대신 그룹 인덱스·결과 버퍼·dispatch/barrier가 늘어난다. 이번에는 GPU 실행 시간과 손익분기점을 확인하지 못했으므로 우선순위를 올릴 근거가 없다. [M4:246–285, M5]

지속 캐시라면 `DecoAnim.Par/ParOff`, FastMove의 즉시/애니메이션/다음 이벤트 덮어쓰기, GPU live 이벤트의 target 갱신, 위치·피벗·배치 기준·부모·이미지 교체·텍스처 배율 변경, 카메라 객체·줌·시점, seek/reset/reload와 소유권 복귀를 반영해야 한다. 프레임 번호만 같다는 이유로 setter 사이의 다른 값을 공유해서는 안 된다. 장식 목록을 시차별로 재정렬해 부작용 순서를 바꾸는 방법도 이 연구의 등가 후보가 아니다. [G2:229–280,521–551, M1–M5]

후속 구현의 합격 기준은 원식과 후보를 같은 입력 snapshot에서 계산해 `startPosition`, `posCamAtStart`, `endPosition`, Transform setter에 넣는 위치/Quaternion/크기를 float 비트로 대조하는 것이다. 기존 `TightLate.Compare`·`DecoEngine.CheckLate`의 비교 지점이 참고가 된다. signed zero·큰 좌표 상쇄·음수/단위 배율·오프셋의 null 축·동일 프레임 연속 이벤트·이미지 교체·zoom/rotation·복귀 경계를 포함하고, GPU에서는 논리 채널과 정점 버퍼 비교 후 픽셀 일치도 별도로 요구한다. 현재의 오프라인 일치 표본을 픽셀·전곡·복귀·FPS 통과로 확대하지 않는다. [TightLate.cs:425–456, DecoEngine.cs:589–610, B1]

## 근거 색인

- **[S1]** [summary.md](../../tools/parallax-lab/evidence/summary.md):5–15. 원자료 [map-stats.json](../../tools/parallax-lab/evidence/map-stats.json)의 `Maps[].Data`와 `InitialParallax/InitialAuthoredRelatedTuples/Fields`.
- **[S2]** 같은 색인:17–23. 원자료 `MoveEvents`, `MoveFields`, `Potentially*`, `AuthoredParallaxTargetDifferentFromInitialDecorations`.
- **[S3]** 같은 색인:25–31. 버전·파서 정규화·반복 이벤트·메모리.
- **[B1]** 같은 색인:33–47. [bench-arche.json](../../tools/parallax-lab/evidence/bench-arche.json), [bench-hello.json](../../tools/parallax-lab/evidence/bench-hello.json), [bench-windflower.json](../../tools/parallax-lab/evidence/bench-windflower.json), [N=K 대조](../../tools/parallax-lab/evidence/bench-arche-no-sharing.json). 실행 구현은 [Program.cs](../../tools/parallax-lab/Program.cs)의 Bench/Exact/Regroup.
- **[G1]** [scrParallax 디컴파일](../../tools/parallax-lab/evidence/scrParallax.cs.txt):56–115.
- **[G2]** [scrDecoration 디컴파일](../../tools/parallax-lab/evidence/scrDecoration.cs.txt):176–178,199–208,229–314,392–422,521–551,670–678,727–745,959–974.
- **[G3]** [scrDecorationManager 디컴파일](../../tools/parallax-lab/evidence/scrDecorationManager.cs.txt):109–123,173–198. [scrVisualDecoration 디컴파일](../../tools/parallax-lab/evidence/scrVisualDecoration.cs.txt)도 보존.
- **[G4]** [ffxMoveDecorationsPlus 디컴파일](../../tools/parallax-lab/evidence/ffxMoveDecorationsPlus.cs.txt):190–229,364–374,431–492. [scrCamera 디컴파일](../../tools/parallax-lab/evidence/scrCamera.cs.txt):399–421.
- **[G5]** [UnityEngine.Vector2](../../tools/parallax-lab/evidence/UnityEngine.Vector2.cs.txt):694–730,788–802; [UnityEngine.Vector3](../../tools/parallax-lab/evidence/UnityEngine.Vector3.cs.txt):928–945. 벡터 연산의 근거.
- **[G6]** [ADOFAI.LevelEvent 디컴파일](../../tools/parallax-lab/evidence/ADOFAI.LevelEvent.cs.txt):292–321,348–365,449–501. 기본값·null 축·구버전 마이그레이션.
- **[M1]** 기존 [TightLate.cs](../../TightLate.cs):161–180,265–284,340–341,352–420,425–456.
- **[M2]** 기존 [DecoAnim.cs](../../DecoAnim.cs):319–339,410–449; [GroupStep](../../DecoAnim.GroupStep.cs):37–58,97–112; [FastMove.cs](../../FastMove.cs):680–688,716–747.
- **[M3]** 기존 [DecoBake.cs](../../DecoBake.cs):235–247,346–350,399; [DecoEngine.cs](../../DecoEngine.cs):578–610. 조사 파일 해시는 provenance.json의 Sources.
- **[M4]** 기존 [GpuDeco.compute](../../tools/gpudeco-lab/GpuDeco.compute):141–185,235–285,345–440. 출력 정점과 live 채널 경로.
- **[M5]** 기존 [GpuDecoExport.cs](../../GpuDecoExport.cs):614–655,1306–1322; [GpuDecoGpu.cs](../../GpuDecoGpu.cs):231–246; [GpuDecoScene.cs](../../GpuDecoScene.cs):197–217.

## 완료 검증과 남은 미확인

독립 도구 빌드는 경고·오류 없이 통과했고, 자체 fixture와 별도의 Python 원본 맵 교차검증도 통과했다. fixture **9개**, 독립 검사 **30개/세 맵**의 결과 파일을 보존했다. [build.txt](../../tools/parallax-lab/evidence/build.txt), [selftest.json](../../tools/parallax-lab/evidence/selftest.json), [independent-validation.json](../../tools/parallax-lab/evidence/independent-validation.json), [S/B 색인:46–47]

실제 시간별 안전 그룹 K, 조건/보간을 모두 실행한 실변화 비율, Mono의 전체 장식당 카메라 비용 비중, GPU 시간, 픽셀·전곡·복귀·FPS는 확인 못 함. 이번 결과의 범위는 정적 호출 경로·원본 JSON 작성값 통계·명시한 합성 .NET 산술 비교다.
