# 굽기 시간 계약 조사와 전체 렌더 경로 설계

2026-10-20, `E:\StutterFixWork\wt-bake`, `codex/night-bake`.

## 판정과 작업 범위

**A는 시간 의미 관문에서 보류, B는 설계 보고 완료.** 게임 실행·설치·커밋은 하지 않았다. 새 `DecoBake.cs`, Harmony 패치, `decobake` 명령은 추가하지 않았다. 기존 게임 경로는 그대로다. 구현을 마쳤다고 보고할 수 없다.

인계 문서 A1의 “계산 의미를 모르겠으면 읽는 지점을 문서에 적고 멈출 것”을 적용했다. 게임이 계산한 예약 시각은 찾았지만, 그 시각을 **기존 Rec의 진행 위치와 비트 동일하게 연결하는 변환**은 확립할 수 없었다. 오히려 기존 코드에서 프레임 이력 의존성을 확인했다. 이를 무시하고 `t-startTime`으로 바꾸면 요청한 의미 보존을 충족하지 못한다. 이 문제는 구간 검색이나 이징 구현으로 해결되지 않는다.

추가물은 독립 오프라인 계약 반례(`tools/decobake-contract-check`), 정적 읽기 지점 증거, 빌드/LoadCheck 로그, 이 보고서, 10줄 이내 요약이다. 관찰 결과는 이 인계에만 남기고 전역 스킬·메모리는 변경하지 않았다.

## A: 게임에서 그대로 읽을 곳

현재 설치된 `Assembly-CSharp.dll` SHA256:
`F692210F9A342F7EED4BE40AAD463286AB40BF046599959823942EA02F4CA3FF`.
게임을 켜지 않고 ILSpy로 다음 타입을 읽었다. 핵심 발췌는 [clock-read-sites.txt](bake-evidence/clock-read-sites.txt)에 있다. 발췌의 행 번호는 임시 디컴파일 결과 기준이며 게임 소스 원본 번호가 아니다.

| 단계 | 정확한 지점 | 읽을 값과 주의점 |
|---|---|---|
| 타일 시간 | `scrLevelMaker.CalculateFloorEntryTimes()` 실행 후 | `scrFloor.entryTime`/`entryTimePitchAdj`. Twirl·Pause·Hold·속도 처리를 자체 재계산하지 않는다. |
| 반복 전개 | `scnGame.ApplyEventsToFloors(...)`의 RepeatEvents 전개와 `ApplyEvent(...)` 호출 완료 후 | 같은 `sourceLevelEvent`로 여러 효과 인스턴스가 생긴다. 원본 이벤트 하나를 구간 하나로 취급하면 누락한다. |
| 시간·길이 확정 | `scnGame.ApplyEvent(LevelEvent,float,float,List<scrFloor>,float,int?)` 반환 시 | `ffxPlusBase.startTime`, `startEffectOffset`, `crotchet`, `duration`, `floorID`, `sourceLevelEvent`. `SetStartTime`과 `Decode`가 이미 호출된 객체를 읽는다. `ffxMoveDecorationsPlus.Decode`는 `duration = evnt.GetFloat("duration") * crotchet`를 수행한다. |
| 실행 대상과 순서 | `scnGame.PrepVfx(...)` 말미 | `scrVfxPlus.instance.effects`가 `startTime-startEffectOffset`, `floor.seqID` 순으로 정렬된다. 같은 키는 원본 열거 순서를 유지하는 LINQ 정렬이다. 임의 ID로 재정렬하지 않는다. |
| 실제 시작 | `scrVfxPlus.Update()` | `cond.songposition_minusi >= startTime-startEffectOffset`가 된 첫 Update에서 `StartEffect()`를 호출한다. 이 시각은 예약 시각과 같다는 보장이 없다. |
| seek | `ffxPlusBase.ScrubToTime(float)` | 별도 경로다. `(t-(float)startTime)/song.pitch-calibration_i`를 계산하고 `Goto`/`Kill(true)`를 사용한다. `DecoAnim`도 scrub 중 일반 Rec 생성을 우회한다. 일반 재생과 같은 공식이라고 가정할 수 없다. |

실제 후속 구현은 **초기 장식 상태의 읽기 시점도 분리**해야 한다. `PrepVfx`는 정렬 직전에 `ScrubToTime(0)` 등을 수행하므로, 그 후의 장식 필드를 맵 초기값으로 쓰면 이미 적용된 효과를 다시 굽을 수 있다. 초기 장식 생성 완료 직후 스냅샷과, 게임이 만든 최종 효과 목록을 서로 대응시켜야 한다. 이 두 단계의 생명주기 연결은 이번에 설치하거나 실게임 검증하지 않았다.

새 후킹을 하게 되면 선언 타입·이름·인자 수·타입·정적/인스턴스·호출 규약으로 후보를 걸러 **정확히 하나일 때만** 설치해야 한다. Mono 런타임에 `Module.ResolveSignature`/`ResolveMethod(MetadataToken)`를 쓰면 안 된다. 이번 작업에는 새 후킹이 없다.

## A: 관문을 통과하지 못한 구체적인 이유

1. `DecoAnim.Update`는 `(DOTween.useSmoothDeltaTime ? Time.smoothDeltaTime : Time.deltaTime) * DOTween.timeScale`을 쓴다. `Step`은 `float to = r.Pos + td`를 매 프레임 반복한다. `DecoBatchEval.EvaluateRange`도 같은 누적이다. 같은 델타 테이프를 넣은 94만 비교 불일치 0은 **이 누적을 재현했다는 증거**이지 곡 시각의 무상태 함수라는 증거가 아니다.
2. 새 이벤트의 실제 시작 프레임은 Update 스케줄과 프레임 길이에 따라 달라진다. 길이 0 이벤트도 실행 프레임이 양자화된다. 시작 프레임을 미리 모르는 맵 열기 시점에는 그 이력을 구울 수 없다. smoothDeltaTime·배속·일시정지·seek까지 포함하면 단순 시간 차감과 더 멀어진다.
3. 교체는 단순히 이전 구간을 현재 보간값에서 자르는 동작이 아니다. `ffxMoveDecorationsPlus.StartEffect`의 위치는 상대 목표 기준을 **Kill 이전**에 잡고, `Kill(true)`로 이전 값을 끝값까지 완료한 **이후** 새 시작값을 잡는다. 예: 직전 현재 4, 이전 끝값 10, LastPosition +2라면 새 시작 10, 새 끝값 6이 가능하다. “현재 4에서 6으로 연결”이나 “10에서 12로 연결”은 모두 틀리다.
4. 크기 X/Y의 시작 벡터는 `DecoAnim.Startup`에서 첫 갱신 때 읽는다. 생성 순서·같은 프레임의 다른 축/이벤트·완료 콜백이 입력 의미에 포함된다. 단일 속성 구간으로 바꾸더라도 해당 읽기 순서를 보존해야 한다.
5. Event 태그 중 hitbox 연결은 `runManually`, 판정 조건은 `conditionalInfo`, 그 밖에 `runOnHit`가 있다. 이런 효과는 시간만의 함수가 아니다. `PrepVfx`는 조건/수동 실행 효과를 일반 예약 효과처럼 실행하지 않는다. `sourceLevelEvent`만 훑으면 이 구분을 잃는다.

[contract-check.txt](bake-evidence/contract-check.txt)의 오프라인 반례:

| 항목 | 이력 A | 이력 B |
|---|---:|---:|
| 입력 델타들의 정확한 합(double로 합계 비교) | 1.0000000521540642초 | 1.0000000521540642초 |
| 프레임 수 | 60 | 120 |
| float 누적 Pos | 0.9999997 / `0x3F7FFFFB` | 0.9999994 / `0x3F7FFFF6` |
| 선형 0→100, 길이 2 출력 | 49.999985 / `0x4247FFFC` | 49.99997 / `0x4247FFF8` |

이 검사는 .NET 8의 float 계약 반례이고 Mono 성능·실게임 오차 분포를 측정하지 않았다. 별도 예약 모형에서는 동일 곡시각 .25, 예약 .1에서 두 프레임 이력의 진행 위치가 .25/.1875로 달라진다. 이 모형은 가능한 호출 순서를 명시한 예시이며 게임의 실제 PlayerLoop 순서를 측정한 결과가 아니다. 첫 번째 float 반례만으로도 임의 이력의 비트 동일성을 무상태 시각 함수에 요구할 수 없다는 점은 드러난다.

**보류 해제에 필요한 결정**은 둘 중 하나다. 곡 시각 기반을 새 의미로 정의하고 기존 경로 대비 값·픽셀 허용 오차를 실게임으로 승인하거나, 기존 의미를 유지하고 실제 프레임 델타/시작 순서를 입력으로 받는다. 후자는 무상태·맵 열기 한 번 굽기 조건을 충족하지 못한다. 현재 허용된 1~2/255는 자동 증명이 필요한 화면 허용치이며 위치·시간 의미 변경까지 포괄하는 승인이 아니다. 이번에는 어느 쪽도 임의로 채택하지 않았다.

| A 요구 | 이번 상태 |
|---|---|
| 이벤트→구간 컴파일러, 잘린 구간 표, 무상태 평가 | 시간 관문에서 보류, 미구현 |
| `decobake verify`, 최대 오차·상위 8개·원인별 분류 | 미구현·미측정. 위 반례는 라이브 비교 결과가 아니다. |
| `decobake bench`, ≤2초/≤16ms | 미구현·미측정. 목표 달성 주장 없음. |
| 미지원 장식 이유별 개수·굽기 비율 | 실제 스냅샷이 없어 미집계. 0개 지원이나 100% 폴백으로 꾸미지 않음. |
| Player 제외·기본 꺼짐·Unload 복구 | 새 게임 코드가 없으므로 설치·소유 상태 자체가 없음. |

후속 미지원 집계는 장식 단위 고유 개수와 중복 사유 수를 분리해야 한다. 이미지/원래 크기/크롭·wrap 변경, 마스크/마스크 대상, hitbox/충돌, 텍스트/파티클/객체 장식, 곡 중 태그 변경, 조건/수동/외부 모드 쓰기, 배치/시차/피벗/색 외 지원하지 않는 속성, 미확인 이징을 모두 열거하고 **그 장식 전체**를 기존 경로에 남겨야 한다. 조건부 이벤트의 가능한 대상도 포함한다. 태그→장식 조회는 기존 `scrDecorationManager.GetTaggedDecorations` 의미(중복·빈 태그 포함)를 재사용하고 자체 문자열 매칭으로 바꾸지 않는다.

## B1: 정렬과 전체 이미지 장식 이관

Unity의 공식 우선순위는 sorting layer의 설정상 순서 → order in layer → render queue → 카메라 거리 → shader/material 묶음 → 내부 동률 처리다. layer의 정수 ID를 크기 비교하면 안 된다. 거리도 단순 world z가 아니라 카메라 투영/투명 정렬 모드·축과 Sprite sort point를 따른다. SortingGroup은 외부에서 하나의 단위로 취급된다. 마지막 내부 동률 순서는 공식적으로 보장되지 않는다. [Unity 6000.3 정렬 설명](https://docs.unity.com/en-us/engine/6000.3/manual/unity2d/sprite/sort-sprites/sort-sprites)

게임의 기존 `sortprobe` 2,550회 기록 요약은 같은 layer/order에서 queue 오름 → 거리 뒤→앞 → **signed material instance ID 오름**이 98~100% 맞았다고 한다. 같은 재질의 메시/스프라이트는 메시가 뒤에 오는 결과가 있었다. 이는 해당 Unity/카메라/셰이더 표본의 관측이다. 공개 API의 전순서 보장이 아니며 재시작·엔진 업데이트에서도 항상 같다는 뜻이 아니다. 원시 CSV는 이 체크아웃에 없고, `codex/night-decorender2:DecoRender.cs`에도 원시 CSV 부재와 50/50·49/50·50/50 근거 한계가 명시돼 있다.

**모든 이미지 장식 약 28,828개를 가져가면 이미지 장식 상호 간의 미관리 장벽은 없앨 수 있다. 그러나 원본 순서를 우리가 마음대로 정의할 수 있는 것은 아니다.** 원본의 관측된 순서대로 삼각형/인덱스를 배치해야 화면을 유지한다. A 이미지–B 텍스트–C 이미지가 같은 키·거리 안에서 교차한다면 이미지 A/C를 한 draw로 합칠 수 없다. 타일·행성·텍스트·파티클·객체 장식·마스크·외부 모드 렌더러는 여전히 남는다. 숨김/비활성 장식도 나중에 나타날 수 있으므로 조사에 포함한다.

후속 `night-decorender2`는 기존 재질을 **빌리고**, 전역 정렬에서 연속한 관리 장식만 구간으로 묶는다. 이 때문에 예전 “새 재질은 끝 ID여서 6~108개만 가능”은 절대 한계가 아니다. 다만 빌린 재질의 텍스처를 바꾸는 `Borrow`는 카메라별 복구와 공유 사용자 검사가 필요하고, 같은 재질의 미관리 렌더러가 있으면 장벽이 된다. 이 후속에서도 검사·사전·소유 관리 비용 때문에 DecoPipe가 실패했다. 전체 이관 제안은 그 비용까지 맵 열기 단계로 옮겨야 다르다.

| 남는 장애 | 전체 이관 시 필요한 처리 |
|---|---|
| 재질·큐·shader keyword·MPB·blend | 유효 상태가 같은 연속 구간만 결합. 색/알파곱·premultiply·색공간·fog·ZTest/ZWrite·stencil·pass를 보존. 재질 ID만 같게 만들어 해결했다고 볼 수 없음. |
| 텍스처·이미지 전환 | 아틀라스는 기존 기록에서 mip 없음도 byte 차이, mip 있으면 최대 57 차이. 원본 UV·필터·wrap·mip를 유지하는 다중 텍스처 또는 동일 규격 Texture2DArray가 후보. 규격 변환/리샘플은 별도 오차. 전환 시 sprite 윤곽·피벗·원래 크기·정렬 구간도 바뀜. |
| 마스크·필터 카메라 | SpriteMask/게임 마스크의 깊이·stencil·대상 관계 보존. 카메라마다 layer mask·투명 정렬·필터 입력·render target이 다르므로 동일 전역 draw 한 번으로 가정하지 않음. |
| 깊이/재질/거리 변경 | 맵에서 예측 가능한 변화만 렌더 구간 경계로 굽기. 움직이는 거리의 역전·CustomAxis·SortingGroup·외부 쓰기는 별도 증명이 없으면 폴백. 정렬 이벤트가 많으면 draw 계획 수와 메모리 증가. |
| 히트박스·충돌체·편집기 선택 | 읽는 코드가 있는 장식은 Transform을 갱신하거나 CPU에 정확한 상태 미러 필요. GPU readback으로 매 프레임 동기화하면 목표를 잃음. 객체별 제외 또는 별도 CPU 경로. |
| 투명·Dormancy | 숨김도 향후 효과와 정렬 자리를 예약. 재등장/이미지 변경 때 정확한 상태 복원. 28,828개 전부 매 프레임 CPU 검사하면 종전 실패를 반복. |
| 경계·컬링 | GPU 이동은 Unity가 원본 Transform 기반 bounds를 알 수 없음. 구간별 보수적 bounds 또는 shader 전용 컬링이 필요. 지나치게 큰 bounds는 원래 안 그리던 픽셀/장식을 그려 GPU 비용 증가. |

결론적으로 전체 이미지 이관은 **가능 대상을 넓히는 조건**이며 동률 문제의 자동 해소 조건이 아니다. 외부 장벽이 없는 닫힌 정렬 구간을 사전 증명해야 한다. 전체 가져가기 후의 실제 구간 수는 아직 측정하지 않았으므로 42개라고 단정하지 않는다. 기존 census의 42는 sortingOrder 수이고 최소 연속 구간은 715~744개였다. 후속 Pipe의 450개도 다른 조건의 측정이다.

## B2: 평가/업로드 설계와 CPU 0.5ms 예산

시간 관문을 해결했다는 가정의 설계다. CPU 그림 경로 코드나 셰이더는 이번에 만들지 않았다.

| 방식 | 구조 | 이점과 제한 |
|---|---|---|
| CPU SoA 평가 후 정점 버퍼 갱신 | 장식/속성 header의 구간 범위에서 이진 검색, 값·피벗·시차·정점을 worker에서 계산하고 메인에서 업로드 | 기존 이징을 재사용하기 쉬움. seek는 커서 재설정 또는 이진 검색. 기존 `DecoBatchEval`은 상태 있는 delta 누적기라 그대로 무상태 검색기로 사용할 수 없음. 0.48~0.80ms는 테이프 재생 비용이며 새 검색·정점 생성·업로드까지의 수치가 아님. |
| 정점 셰이더 평가 | 정적 구간 buffer와 장식별 header/원본 정점·UV를 업로드. 정점이 decoration ID로 해당 속성 구간을 검색하고 곡 시각·카메라 상수로 변형 | CPU는 시각/카메라 상수·실제 경계 이벤트만 갱신. 대신 정점마다 검색/이징 반복, GPU 부하·bounds·이징과 float 정밀도 검증 필요. 원본 Sprites/Tile shader가 buffer를 읽지 않으므로 호환 셰이더와 원본 fragment/pass 의미 보존이 필요. |
| GPU compute 평가 후 정점 셰이더 소비 | 장식별 한 번 평가해 pose buffer 생성, 정점은 이를 읽음 | 정점별 중복 계산 감소. dispatch/리소스 전환 비용 및 shader 호환 검증 추가. 현재 목표에서 먼저 필요한 단계인지 벤치로 결정. |

StructuredBuffer는 이진 검색에 적합하다. D3D11에서 Structured와 vertex/index target을 같은 버퍼로 겸용할 수 없으므로 구간 buffer는 별도로 두고 vertex shader SRV로 읽는다. stride는 HLSL/C# 레이아웃을 맞추며 명시적으로 패킹한다. 정점 output에 compute가 직접 쓰려면 별도 Raw buffer 경로를 검토해야 한다. [Unity GraphicsBuffer.Structured](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/GraphicsBuffer.Target.Structured.html)

텍스처 대안은 float32 데이터 텍스처의 정수 `Load`이고 필터/mip/sRGB를 끈다. half/UNorm으로 곡 시간·계수를 저장하면 비트 동일을 유지할 수 없다. GPU의 FMA·sin/pow·이징 끝값 처리도 CPU와 다를 수 있어 `precise`만으로 동일성을 선언하지 않는다. 완료 시 목표값 직접 적용, 길이 0, NaN 미사용 축, 교체 전후 시작값은 별도 표현이 필요하다.

로드맵의 414,743구간 × 40B = **16,589,720B = 15.82MiB**는 payload 추정이다. cutoff·원래 duration·start/end/change·이징 매개변수·시각 정밀도·완료 flags·색 벡터·정렬/이미지 트랙에 따라 stride가 늘 수 있다. 48/64B면 각각 18.99/25.31MiB다. 장식×10 scalar 채널 ×8B header는 약 2.20MiB가 추가되며 원본 정점·인덱스·양쪽 CPU/GPU 사본·준비 중 임시 메모리는 별도다. 17MB로 전체 상주 메모리를 보장하지 않는다.

CPU 예산을 수치로 나누면 다음과 같다.

- 기존 표본은 프레임당 평가 약 11,500개에서 단일 평가 0.711ms, 병렬 평가 0.388ms, 이벤트·시작 포함 0.80/0.48ms다. CPU 방식은 최선 표본도 0.5ms에서 **0.02ms만 남아** 정점 계산·업로드·제출을 포함할 여유가 없다. 이벤트·시작을 제거해도 0.388ms에서 남는 몫은 0.112ms다. 28,828개 전 채널을 매 프레임 훑는 구조는 이 표본보다 훨씬 커진다. 정적 채널·완료 채널을 사전 분리해야 한다.
- 1,100장식 ×4정점 ×36B(위치/UV/Float32색 예시)는 약 0.151MiB/프레임, 200FPS에서 30.2MiB/s다. 모든 이미지 28,828개면 약 3.96MiB/프레임, 200FPS에서 792MiB/s다. 실제 윤곽 정점 수·layout에 따라 달라진다. 전송 대역폭만으로 메인 스레드 map/복사/동기화 시간을 증명할 수 없다.
- GPU 방식의 메인 CPU는 `상수 갱신 + 경계 이벤트 + 카메라별 draw 관리 + 폴백 동기화`여야 한다. 예산 가정으로 고정 0.10ms·경계 0.05ms를 놓으면 draw당 1µs에서 ≤350구간, 5µs에서 ≤70구간이어야 0.5ms다. 42구간이면 0.192~0.360ms, 450구간이면 0.600~2.400ms다. **1/5µs는 실측값이 아닌 민감도 가정**이다. 고정 MeshRenderer로 엔진에 맡기는 경우 draw당 C# 호출은 사라져도 엔진 CPU 비용은 남는다.
- 따라서 **CPU 평가+정점 업로드는 현 근거로 ≤0.5ms에 닿는다고 할 수 없다. GPU 평가가 그 목표를 노릴 수 있는 구조**지만, 실제 구간 수·카메라 수·동적 경계·폴백 비용까지 재어야 한다. 과거 Pipe의 매 프레임 3.5~20.9ms 관리 비용을 그대로 얹으면 어느 방식도 실패한다.

맵 열기 ≤2초도 실측 전 목표다. 게임 객체 접근/태그 조회는 메인에서 작은 스냅샷 단계로 쪼개고, 순수 데이터 정렬/구간 생성은 worker, Unity 자원 생성/업로드는 별도 분할한다. 단계마다 실제 경과 시간을 검사하고 16ms보다 작은 예산에서 양보해야 한다. 큰 태그 하나를 통째로 펼치는 작업도 중단 가능해야 한다. 2초 내 처리량은 약 20.7만 구간/초 이상이며 수집·업로드·GC까지 포함해야 한다. 다른 스레드에서 Unity 객체를 읽는 편법은 쓰지 않는다.

## B3: 이득 계산과 GPU 바닥

`docs/optimization-36.md` 마지막 네 절, 기존 census/sortprobe·DecoSim 기록, `codex/night-decopipe:DecoPipe.Static.cs`/`DecoPipe.Timing.cs`, `codex/night-decorender2:DecoRender.cs`를 확인했다. 최근 GPU 증거의 377프레임 표본은 전체 4.2437ms, 본 카메라 4.0649ms, 필터 합 약 0.65ms, 장면 약 3.4ms다. 최대 GPU 프레임은 7.6462ms였다. [원시 로그](archegpu-evidence/arche-258-278-gpuprobe.txt)

10~11ms CPU 프레임에서 장식 약 5.5ms를 완전히 제거해도 엔진 CPU 약 5ms와 GPU 약 4.2~6.6ms가 남는다. CPU/GPU는 겹쳐 실행되므로 **시간을 더하지 않고 max(CPU,GPU)**로 본다. 이상적 프레임 5~6.6ms, 약 150~200FPS다. 계측 판 FPS를 성능 결과로 쓰지 않고, 2560×1440 모니터 끔 결과와 3440×1440 모니터 켬 결과도 한 A/B로 섞지 않았다.

다른 측정의 장식 갱신 2.5ms + Late 1.5ms = 4.0ms와 장식 총 5.5ms는 계측 범위/조건이 다르다. 이벤트 시작과 엔진 내부 비용이 겹칠 수 있으므로 4.0+5.5로 더하지 않는다. 엔진 쓰기 방식만 바꿀 때 상한 0.35~0.45ms, 렌더러 숨김 진단 약 0.5ms는 전체 교체와 중복되는 몫이다. 그룹 메시가 안개의 겹침 픽셀을 없애지는 않으므로 GPU 3.4ms 장면 비용이 자동 소멸하지 않는다.

| 경로 | 예상 프레임/FPS와 끊김 | 기간 추정(관문 통과 후 순 작업일) | 위험과 되돌리기 |
|---|---|---|---|
| A만, 기존 setter/Transform/SpriteRenderer 유지 | 동일 10~11ms 가정에서 순절감 0.5/1.0/1.5ms면 각각 약 95~105 / 100~111 / 105~118FPS. 절감량은 가정이며 A 실측 없음. 기존 Rec 관리·시작 spike 일부를 없앨 수 있으나 쓰기·Late·SetSprite·엔진/OS 끊김은 남음. | 시간 계약·기준 상태 1~2일, 컴파일/평가 2~3일, 값 대조/성능·seek·복구 2~3일: 합 5~8일 | 상대 기준/완료/동시 이벤트/축 시작값·시간 오차. shadow 전용으로 시작하고 승인 전 소유권 이전 금지. 실패 시 굽기 세대 폐기, 기존 경로 계속 사용. |
| A+B 전체 경로, 장식 처리 잔여 0.5~1ms | 10~11ms 중 5.5ms 대체 가정이면 CPU 5~6.5ms. GPU 4.2~6.6ms와 max를 취해 대략 **150~200FPS**, 현실 잔여 비용에 따라 더 낮을 수 있음. 장식 시작/갱신 spike는 줄일 후보이나 끊김 0 보장 없음. 400FPS 근거 없음. | A 이후 정렬 폐쇄/이미지 전환 2~4일, shader·buffer·카메라 3~5일, 화면/전곡/편집기·공존·복구 3~5일: 추가 8~14일, 총 13~22일 | 정렬/마스크/표본화·GPU 정밀도, 큰 구간 수, 숨김 부하, CPU 상태 소비자. 카메라 렌더 전 원본 재동기화→원본 renderer 복구→빌린 재질 복구→buffer/mesh 폐기. 불완전 복구면 해당 세션 시작 자체를 거부. |

기간은 확약이 아닌 설계 복잡도 추정이며 시간 계약 결정 대기·실게임 실패 재작업은 제외했다. 효과 시작 스파이크를 미리 계산해도 runtime 경계에서 전체 장식을 순회하거나 큰 sprite 전환/업로드가 생기면 끊김이 다시 생긴다.

예전 “코드만으로는 이득 없음”은 기존 Rec/소유권/검사를 유지하면서 일부 쓰기·렌더러만 바꾸던 실험에 해당한다. 새 제안은 **Rec 생성·교체·갱신·딕셔너리 검사·Late·Transform 쓰기를 함께 제거하고**, 맵별 계획을 선계산해 시간/카메라 상수만 보내는 다른 작업량이다. 이 구조 차이는 이득 가능성의 근거이며 이득 측정 결과가 아니다. 시간 동일성 관문과 외부 정렬 장벽이 해결되지 않으면 옛 실험보다 낫다는 결론도 성립하지 않는다.

## 검사 결과

| 검사 | 결과 | 근거 |
|---|---|---|
| 시간 계약 반례 | 통과, 기존 의미와 무상태 계약의 차이 확인 | `bake-evidence/contract-check.txt` |
| 기존 DecoSim fixture | 12검사 통과, 의도한 손상 검출 포함 | `bake-evidence/decosim-fixture.txt` |
| 개발 Release 빌드 | 오류 0, 경고 38 | `bake-evidence/build-dev.txt` |
| Player Release 빌드 | 오류 0, 경고 36 | `bake-evidence/build-player.txt` |
| PlayerAuto Release 빌드 | 오류 0, 경고 38 | `bake-evidence/build-playerauto.txt` |
| LoadCheck 개발 / Player / PlayerAuto | 266 / 229 / 266곳, 틀림 모두 0; 동적 이름 5곳은 실행 중 확인 필요 | `bake-evidence/loadcheck-*.txt` |

빌드는 기존 모드 소스의 상태 검사다. 새 게임 기능은 추가하지 않았으며 새 기능의 Player 제외/Mono 설치 성공을 증명한 것이 아니다. LoadCheck는 FieldRefAccess 메타데이터 검사이고 게임 로딩/실제 Harmony 실행 검사가 아니다. warning을 오류 0과 혼동하지 않았다. 라이브 값/화면 비교, Arche 굽기 시간/메모리/평가, 채택 비율, FPS A/B는 **모두 미실행**이다.

## 내가 돌릴 순서

현재 `decobake verify`/`decobake bench`는 존재하지 않는다. 다음 절차는 시간 계약을 결정하고 A를 구현한 뒤 Claude가 실행할 인수 시험 순서이며 지금 실행 가능한 성공 경로가 아니다. 이번에는 autotest 파일이나 설치 파일을 만들지 않았다.

1. `D:\얼불춤 맵 파일\Arche\*FULL.adofai`에서 실제 파일을 하나 선택한다. 모호한 wildcard를 `open`에 넘기지 않는다.
2. `open <선택한 FULL.adofai 절대 경로>` → 맵 열기 단계 굽기 종료/지원률·이유별 집계·메모리·최대 메인 단계 시간 확인. 기본 경로가 계속 살아 있는 shadow 상태여야 한다.
3. `seek 262 8` → 같은 프레임 시각의 `DecoSim` 테이프와 굽기 출력을 함께 기록 → `decobake verify`. 단일/병렬, scalar/TRS/색, 불일치 수·최대 오차·상위 장식 8개·교체/시작/이징/float/seek 원인을 확인한다. 작은 시간 오차를 단순 float 오차로 뭉개지 않는다.
4. 재생 정지 후 `decobake bench`. 수집/굽기/업로드 각각과 합계, 메인 프레임 최대, peak/상주 메모리, 같은 평가 시각을 넣은 p50/p99를 분리한다. 계측·비교와 FPS 시험은 분리한다.
5. 되감기/앞뒤 seek·일시정지·배속·다시 하기·맵 전환·Unload를 검증한다. 값 관문 통과 후에만 소유권 이전과 화면 비교, 그 뒤 동일 해상도/모니터/설정의 새 프로세스 ABBA 및 전곡 시험으로 간다. B는 카메라별 같은 프레임 원본/대체 렌더 비교가 추가로 필요하다.

이미 존재하는 진단만 확인하려면 `open` → `seek 262 8` 뒤 `decosim capture 4`로 별도 표본을 얻고 완료 후 `decosim verify`, 정지 후 `decosim bench`를 사용할 수 있다. 이것은 굽기 컴파일러 검증의 대체물이 아니다.
