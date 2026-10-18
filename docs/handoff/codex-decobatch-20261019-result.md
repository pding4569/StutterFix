# DecoBatch 1단계 결과 — 2026-10-19

요청한 그림자 검증 경로를 구현했다. 제품 애니메이션 계산·setter·완료 순서는 유지했다. 게임 실행 0회, Mods 설치 0회, 커밋 0회다. 2단계 제품 구현은 하지 않았다.

Developer / Player / PlayerAuto 빌드 오류는 각각 0개다. 실제 Mono·Arche 구간의 비트 동일성과 0.05ms / 0.3ms 목표 달성 여부는 미측정이다. 아래 오프라인 검증은 실제 게임 성능 근거가 아니다.

## 구현

- `DecoBatchShadow.cs`: 상주 수치 배열, 슬롯·장식 인덱스 관리, 관리 루프, 네이티브 호출, 비트 비교, 읽기 전용 필드 접근 측정, 명령과 종료 처리.
- `native/sfnative/decobatch.cpp`: 새 export `sf_decobatch_step`. 별도 번역 단위에 `/O2 /fp:strict /arch:SSE2 /MT /Brepro /W4 /WX`를 적용했다. 기존 PNG·프레임 생성 코드의 부동소수점 옵션은 바꾸지 않았다. 새 계산은 FMA를 사용하지 않는다.
- `DecoAnim.cs`: Startup, 프레임 시작·끝, 강제 완료·Kill·KillAll·Compact에 그림자 연결. `Rec.BatchShadowSlot`은 DEV/AUTOTEST 전용이다.
- `DecoAnim.GroupStep.cs`, `DecoAnim.FastLane.cs`: 크기 계산 직전의 현재 다른 축을 관찰하는 연결 각 1줄. 기존 분기와 setter는 그대로다.
- `AutoTest.cs`, `StutterFix.csproj`, `StutterFix.cs`: 명령, FuseProbe/CpuBreak와 같은 컴파일 조건, UninstallAll의 Stop 연결 각 1줄.
- `tools/DecoBatchCheck`: 새 코드를 직접 링크하는 오프라인 산술·생명주기·할당·빌드 격리·리소스 검사 도구.
- `tools/GroupStepCheck/run.py`: 실제 소스 추출 시 새 partial 선언도 함께 가져오는 1줄.

일반 Player에는 그림자 타입·partial 메서드가 없다. 따라서 해당 호출과 DEV 전용 슬롯 필드도 컴파일에서 빠진다. DEV/AUTOTEST에서는 명시적으로 begin하기 전 On=false다. 새 Harmony 패치·정적 이벤트·PlayerLoop 등록은 없다. 그림자 예외는 그림자만 중단시키고 제품 Step으로 전파하지 않는다.

## 배열과 생명주기

최대 65,536개 슬롯을 begin 때 미리 할당한다. 키·Ease 코드·Dur·관리 Pos·네이티브 Pos·Over·Period·시작/변화 성분·출력·완료 여부·장식 인덱스를 배열로 유지한다. 성분은 슬롯당 최대 4개를 연속 저장하며, 각 루프는 Rec/델리게이트에 접근하지 않는다. Rec 참조 배열은 등록·수명 관리와 비교에만 사용한다.

장식 참조는 별도 인덱스 배열에 둔다. 슬롯별 장식 인덱스와 장식별 참조 수를 유지하고, 마지막 슬롯이 없어지면 사전과 참조 배열에서 장식을 놓는다. 장식 인덱스도 재사용하므로 재시작·반복 생성 때 인덱스가 계속 증가하지 않는다.

Startup이 시작값·변화량을 정한 뒤 슬롯을 등록한다. 이미 진행 중인 기록은 begin 때 한 번만 받아들인다. 각 계산기의 Pos는 이후 독립적으로 누적한다. 매 프레임 전체 Rec를 다시 수집하거나 입력 Pos를 덮어쓰지 않는다. 새 기록은 프레임 중 Startup에서도 등록된다.

자연 완료는 프레임 끝에서 값·Pos·done을 비교한 뒤 해제한다. 강제 Complete/Kill(true)는 정상 dt 진행과 달라 비교 대상에서 빼고 슬롯을 해제한다. 시작 전 Complete가 Startup을 부르면 생긴 슬롯도 다시 해제한다. Kill(false), KillAll, Compact 제거 역시 해제한다. 스왑 제거로 옮긴 Rec의 슬롯 번호를 고치므로 재사용과 이중 제거가 안전하다. 배열 순서는 제품 처리 순서로 사용하지 않는다.

ResetScene/곡 종료/설정 끄기는 기존 DropAll/FinishAll/Kill 경로의 해제를 따른다. 모드 내리기와 shadow end는 Stop으로 남은 슬롯·장식 참조·배열을 놓는다. 용량 초과는 overflow로 세고 제품 처리를 계속한다. overflow가 있으면 전체 기록 검증으로 채택하면 안 된다.

## 지원 범위와 비교

관리·네이티브 양쪽이 지원하는 Ease는 Linear, InQuad, OutQuad, InCubic, OutCubic, InOutQuad, InOutCubic이다. 키는 1·2·5·7·8·9·10이다. 그 밖의 키/Ease 조합은 등록 횟수별 미지원 목록에 남기고 계산하지 않는다. 제품은 원래 경로로 계속 처리한다. 네이티브 done=-1도 미지원 표시다.

각 계산기는 `Pos + td`를 float로 저장하고, Dur 이상이면 Dur로 고정한 뒤 같은 순서로 이징·값을 계산한다. 기존 Rec의 FLast / VLast / CLast와 새 Pos, `!Running`을 SingleToInt32Bits로 비교한다. ±0도 구분한다. 색은 4성분, 크기는 2성분을 비교한다. 첫 불일치에는 경로·키·Ease·성분·td·Pos·Dur·값·비트·done을 남긴다.

크기의 계산하지 않는 다른 축은 **기존 코드가 scaleVec을 읽은 바로 그 시점의 값**을 관찰해 배열에 담는다. 예를 들어 Y의 VLast.x는 X가 먼저 쓴 값일 수 있다. 이는 제품 필드를 쓰지 않고 이번 프레임의 전체 VLast를 대조하기 위한 입력이다. 아직 별도 SoA 상태만으로 X/Y 의존과 시작값을 재현했다는 증거는 아니다. 위치 X/Y는 FLast를 개별 비교하며, StepPair의 실제 setPos 인자·OnComplete 필드 결과까지 이 검사 하나로 증명하지 않는다.

decoablate가 켜져 있으면 begin을 거부한다. 도중 켜지면 그림자만 중단하고 shadowError에 남긴다. 네이티브 로딩/entry point/호출 실패는 nativeOK=false와 nativeError로 남겨, 미실행을 불일치 0건 통과로 읽지 않게 했다.

## 비용 출력과 해석

`decobatch shadow begin|end|state`를 추가했다. state는 프레임 수, 현재/최대 슬롯, 등록/해제, overflow, 양쪽 비교/불일치 수, 미지원 목록, 첫 불일치, 오류와 다음 프레임당 시간을 보고한다.

| 항목 | 측정 범위 |
|---|---|
| managed | 수치 배열 전체를 한 번 순회하는 관리 계산, Stopwatch 1쌍 |
| native(PInvoke+fixed) | 배열 고정 진입부터 P/Invoke 반환까지, Stopwatch 1쌍 |
| maintenance | Startup 등록·명시적 해제의 이벤트별 시간 합과 프레임 말 해제 스캔의 일괄 시간 |
| maintenancePeak | FrameBegin 이후 해당 프레임 안에서 누적한 유지 시간의 최댓값 |
| readonlyField | 장식 인덱스→참조→기존 FieldRef 접근을 한 루프로 읽는 시간, Stopwatch 1쌍 |
| compare | 실제 Rec와 양쪽 출력의 비트 비교 전체, Stopwatch 1쌍 |
| managedTotal / nativeTotal | 각 계산 시간 + maintenance + readonlyField의 합 |

네이티브는 fixed로 주소를 넘긴다. Boehm GC가 비이동이라는 가정에 기대어 pin을 생략하지 않았다. 고정·호출 비용을 native 시간에 포함한다. Over/Period도 보존·전달하되 현재 지원 다항식은 사용하지 않는다.

maintenance는 매 프레임 모든 기록에 Stopwatch를 감싸지 않는다. 생성·취소 이벤트에서만 한 쌍씩 재고, 자연 완료의 해제 스캔은 한 쌍으로 잰다. 다량의 기록이 동시에 시작되는 프레임에는 이벤트 타이머 자체의 비용도 생긴다. begin 시 배열 할당·최초 수집은 프레임 유지 평균에서 제외한다. 프레임 사이의 Kill 비용은 누적 평균에는 들어가지만 maintenancePeak에는 안 들어간다.

readonlyField는 실제 장식 필드를 읽기만 한다. 출력 쓰기, setter, ApplyColor, Transform 반영, lateSkipped 등록 시간은 아니다. 크기의 다른 축을 관찰하는 연결과 각 연결의 분기/호출 비용도 총합에 완전히 포함하지 않는다. 따라서 Total은 **후보 비용의 부분 추정**이며 제품 구현 전체 비용으로 판정하면 안 된다. 특히 색·불투명도 setter의 엔진 비용을 이 숫자에서 빠뜨려 개선으로 주장하면 안 된다.

shadow FrameEnd가 Compact 앞에서 실행되므로 기존 LastFrameMs, fuse의 애니메이션 집계, decosamp의 전체 루프/정리 집계에 그림자 비용이 섞인다. 이들을 shadow와 겹쳐 실행한 판의 FPS·전체 갱신 시간은 제품 성능 근거가 아니다. state 출력·첫 오류 문자열·begin 할당·첫 JIT도 끊김을 만들 수 있다. 성능 기준 측정은 그림자를 끈 별도 판에서 한다.

## 검증 결과

빌드 명령은 저장소에서 dotnet build만 실행했다. 자동 설치 훅과 게임 자동화는 호출하지 않았다.

| 빌드 | 명령 | 오류 | 경고 |
|---|---|---:|---:|
| Developer | `dotnet build -v q --nologo` | 0 | 35 |
| Player | `dotnet build -v q --nologo -p:Edition=Player` | 0 | 33 |
| PlayerAuto | `dotnet build -v q --nologo -p:Edition=Player -p:AutoTestBuild=1` | 0 | 35 |

네이티브는 native/build-sfnative.bat로 빌드했고, 그 뒤 관리 빌드 셋을 다시 만들었다. 새 export 번역 단위의 컴파일 오류 0개다. 기존 ISPC 성능 경고는 존재한다.

저장소 native/sfnative.dll의 SHA256과 세 관리 DLL의 **실제 manifest resource에서 추출한** 내장 sfnative.dll SHA256이 모두 다음으로 일치했다.

`30E8A8306EB3F3565FC6D09C94DB4F3EB85B6E6254228710F000739399437766`

설치가 금지되어 설치 후 해시는 검사하지 않았다. 위 해시는 저장소 산출물과 내장 리소스에 대한 검증이다.

```powershell
dotnet run --project tools/DecoBatchCheck -c Release -- native/sfnative.dll bin/Player/StutterFix.dll bin/Debug/StutterFix.dll bin/PlayerAuto/StutterFix.dll
py -3 tools/GroupStepCheck/run.py
```

새 검사 도구: .NET 8.0.6에서 assertions 13,075,049개 통과. Ease 7종×키 7종×4,096개×12회 진행을 실제 게임 DOTween.dll의 EaseManager.Evaluate와 대조했다. 음수 dt·0·아주 작은 dt·완료점·±0, 미지원 Ease/키, 빈 입력·잘못된 개수도 검사했다. 관리·네이티브 값/Pos/done 불일치 0건이다.

수명 검사는 시작된 기록 인수, 프레임 중 등록, 크기 X/Y 순서, 자연 완료 후 해제, 취소, 스왑 이동, 재사용, 중복 Stop, 65,536개 용량 초과와 장식 인덱스 재사용을 통과했다. 색 g에 의도적으로 넣은 오차는 양쪽에서 검출했고 첫 불일치 lane=1을 확인했다. 실행 중 ablation도 그림자만 중단했다.

추가로 4,096개 상주 기록을 32프레임 예열한 뒤 256프레임 실행: 해당 256프레임 추가 관리 할당 0바이트. 예열 포함 288프레임에서 관리·네이티브 각각 1,179,648회 Rec 비교, 불일치 0건이다. Unity 객체를 흉내 낸 관리 stub 환경의 결과다.

기존 GroupStepCheck는 실제 DecoAnim 메서드를 추출한 scalar 3,240조건, pair 300조건, FastLane 1,440조건, Compact 256조건, 실제 UpdatePrefix 80조건 및 콜백 중 기록 추가/완료 검사를 통과했다. 그림자 꺼짐 10,000프레임 할당 0바이트였다. 도구가 함께 출력한 마이크로벤치 FPS/시간은 제품 성능 근거로 채택하지 않았다.

메타데이터 검사로 일반 Player의 DecoBatchShadow 및 BatchShadow 메서드 부재, Developer/PlayerAuto의 포함을 확인했다. 실제 Unity Mono·실맵의 setter 콜백·외부 모드·전체 곡·화면 품질 검증은 남아 있다.

## 2단계 설계 — 구현하지 않음

첫 대상은 보이는 이미지 장식의 키 1·2·5·7·8과 위 지원 Ease다. 숨김·투명·마스크·미지원 Ease·Prof·개발자 Shadow 기록은 기존 경로로 둔다. 입장 조건은 매 프레임 필요한 가시성·마스크 변화까지 확인한다. 판단할 수 없으면 그 장식 전체를 기존 경로로 돌린다.

슬롯에는 단조 증가 순서 번호와 세대를 둔다. 스왑 제거된 물리 순서로 setter를 실행하지 않는다. 같은 장식에 양 경로가 섞이면 원래 생성 순서에 따라 처리하며 의존 구간에서 일괄 처리를 끊는다. 첫 제품 후보에서는 장식 단위로 전부 일괄/전부 기존 중 하나를 택하는 편이 안전하다. 이는 추정 설계이며 실제 허용 비율과 이득은 측정해야 한다.

크기 Y의 Startup은 X 처리 후 scaleVec에서 시작값을 읽어야 한다. 위치 X/Y도 현재 반대 축과 pivotOffset을 읽는 경계를 보존한다. 이번 그림자의 관찰된 다른 축 입력을 제품에서 그대로 가져올 수는 없으므로, 장식별 수치 미러에 X 적용→Y Startup/적용 순서를 재현해야 한다. 원래 StepPair의 허용 조건과 다른 모드 setter 패치 검사를 유지한다.

NewRec 재사용 때 이전 슬롯을 세대로 무효화하고 새 시작 시점에 결합한다. Kill/Complete/KillAll/OnComplete는 일괄 출력 소비 전후 어느 시점에 발생해도 원래 끝값 쓰기와 부수 효과를 먼저 처리하고 슬롯을 무효화한다. 곡 끝·재시작·모드 끄기·ResetScene은 같은 해제 함수를 사용한다. 콜백이 취소하거나 새로 만든 기록에는 오래된 계산 출력을 쓰면 안 된다. 중간 setter 예외가 나면 이미 적용한 기록은 재실행하지 않고, 아직 적용하지 않은 기록만 원래 경로로 잇는다.

UpdatePrefix가 끝나기 전 pivotPosVec/rotAngle/scaleVec은 최신이어야 한다. TightLate와 다른 코드가 이 값을 읽는다. DeferTransform의 조건과 매 프레임 필요한 MoveApply.lateSkipped 등록도 보존한다. 네이티브 계산 종료를 프레임 뒤로 미루거나 렌더 시점에만 필드를 맞추는 방식은 이 설계에 포함하지 않는다.

색 9·불투명도 10은 첫 제품 대상에서 제외한다. 값 계산만 일괄화하고 ApplyColor를 부르는 setter를 원래 순서대로 유지하는 변형은 별도 평가한다. 현재 Eval이 싸므로 이 변형의 이득은 작을 가능성이 있다는 추정이며, 실제 수치로 확인해야 한다.

이득 추정식은 `대체된 기존 관리 시간 - (일괄 계산 + 등록/해제 + 순서/의존 처리 + 필드 동기화 + lateSkipped + 잔존 setter/폴백 비용)`이다. 인계 기준 위치·회전·크기 약 1.2ms + 정리·뼈대 약 0.55ms 중 전부를 실제로 대체하고 새 비용이 0.3ms이면 약 1.45ms/프레임의 감소 여지가 있다는 **추정**이다. 그림자 Total만으로 해당 감소를 확인했다고 할 수 없다. 특히 기존 Compact를 같이 없애지 못하면 0.55ms 전부를 빼면 안 된다.

위험은 순서 의존, 중간 취소/재사용, 끝값 setter, 마스크·가시성 변화, 외부 Harmony 콜백, Mono의 float 반올림 차이, 용량 초과·첫 할당/JIT, 낮은 일괄 허용 비율이다. 작업량은 추정 4~7일: 입장/순서·수명 1~2일, 필드/lateSkipped 연결 1~2일, 오프라인·실맵·다른 모드 대조와 성능 A/B 2~3일. 실맵 반례에 따라 늘 수 있다.

## Claude의 실게임 확인 순서

아래는 후속 담당자의 절차다. 이번 작업에서는 실행하지 않았다.

1. 새 PlayerAuto 관리 DLL의 내장 네이티브가 위 해시인지 확인하고, 후속 작업에서 설치를 허용받은 범위에 따라 실제 설치 파일 해시도 기록한다. 이전 네이티브 DLL이 이미 로드된 게임 프로세스는 새 export가 없어 실패할 수 있으므로 후속 시험 환경에서 새 프로세스를 사용한다.
2. 별도 기준 판에서 그림자를 끈 채 Arche 250초 전에 준비한다. 257.8초 부근 `fuse begin`, `decosamp begin`, 278.8초 부근 `decosamp end`, `fuse end`로 기존 귀속 수치를 얻는다. 기존 FastLane/GroupStep/PosFast/TightLate/NativeMotion 설정과 해상도·주사율도 기록한다.
3. 그림자 판에서 Arche 250초 부근 `decobatch shadow begin`으로 배열·호출 경로를 예열한다. 이 판의 FPS는 성능 근거로 쓰지 않는다. fuse/decosamp와 겹치지 않는다.
4. 257.8초 부근 `decobatch shadow state`로 A를, 278.8초 부근 같은 명령으로 B를 저장한다. `decobatch shadow end`로 종료·해제한다. 순수 구간 시간은 각 누적 시간 `ms/frame × frames`의 B−A를 frames의 B−A로 나눠 구한다. 출력 반올림 오차가 있으므로 경계값에 여유를 둔다. begin 직후 할당·예열 구간의 평균을 최종 구간 값으로 사용하지 않는다.
5. 양쪽 checks>0, frames 증가, mismatch=0, nativeOK=true, nativeError/shadowError 비어 있음, overflow=0을 확인한다. 미지원 종류·등록/해제·최대 슬롯도 기록한다. 끝·취소·재시작 때 slots=0이 되는지 별도로 확인한다.
6. 실제 Mono 구간의 유지 비용 0.05ms 이하와 계산+유지+필드 접근 추정 0.3ms 이하인지 본다. 이 값이 낮아도 2단계의 순서/필드 쓰기/lateSkipped 비용은 추가 검증해야 한다. 이 값과 기준 판의 대체 가능한 부분을 근거로 다음 단계의 가치를 판단한다.

기존 사용자 변경인 `.codex/config.toml`과 시작 시 존재한 미추적 디렉터리·파일은 보존했다. 결과 문서는 apply_patch로 UTF-8 BOM을 넣어 작성했다. BOM 바이트 EF-BB-BF, UTF-8 엄격 디코딩, 대체 문자 없음, Get-Content -Encoding UTF8의 정상 한글 출력을 확인했다.
