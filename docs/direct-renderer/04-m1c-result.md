# 직통 렌더러 M1c 구현·오프라인 검증 결과

2026-11-15, `E:\StutterFixWork\wt-v14prod`, 브랜치 `claude/adofai-renderer`, 시작 HEAD `4bfb4bc`.
지시서: [04-m1c-spec.md](04-m1c-spec.md). M1b에서 이어서 구현했다. 커밋·게임 실행·설치 DLL 교체는 하지 않았다. 직통 렌더러의 기본 끔도 유지했다.

**부분 정적 실패의 전체 취소, 가시성 변경의 전체 재활성화, 비활성 풀 256개 제한을 제거했다. DEV / Player / PlayerAuto / native 오류 0, export 73개 보존, WARP 회귀와 희소 가시성 자료구조 검증은 통과했다. 실게임 화질·전체 직통 비용 <50µs/프레임·전체 관리 할당 0은 이번 작업에서 측정하지 않았다.** 아래의 오프라인 자료구조 할당 0을 게임 전체 경로의 합격으로 해석하면 안 된다.

## 원인 확인

기존 4개 `dp5-*-drPix.log`를 직접 읽었다. 집계는 `out/direct-renderer/m1c/m1b-log-history.json`에 있다. 이것은 **기존 M1b 기록**이며 M1c 실행 결과가 아니다.

| 이전 로그 | 확인한 내용 |
|---|---|
| HELLO / WF / DD | 각각 정적 검증 실패로 전체 비활성화 1회. `Activate()`가 `released > 0`이면 전체를 포기했다. |
| Exit 전체 파일 | 1,039개 활성화 로그 30회, 44개 활성화 로그 11회, `순서 경계 변경` 비활성화 40회. 명세에 요약된 6회보다 전체 파일의 반복 횟수가 많다. |
| Exit 1,039 → 44 | `SetVisible` / `ApplyColor`로 경계 가시성이 바뀔 때 `Commit()`이 `Deactivate(); Activate()`를 실행했다. 그 시점의 Canvas·마스크·재질 상태로 다시 분류하면서 소유 집합도 달라졌다. 활성화 로그와 최종 state는 서로 다른 활성화 세대였다. |
| Exit 196,083µs | PreCull의 시간 측정 안에서 전체 활성화가 수행됐다. 정적 값·텍스처 등록·형태 칸 도형·전체 Renderer/Canvas 검색을 다시 하는 약 180–250ms가 commit 통계에 들어갔다. 단순한 네이티브 commit 비용이 아니었다. |
| 판정 글자 등 풀 | MeshRenderer / ParticleSystemRenderer / TrailRenderer / SpriteRenderer 자체는 이미 후보였다. 외부 렌더러가 256개를 넘으면 **꺼진 후보를 강제로 Seq=false**로 바꾸는 코드 때문에 수천 개 풀 전체가 경계로 남았다. 기존 로그의 셰이더별 이름만으로 다중 패스라고 단정할 수 없었다. |

## 구현

### 부분 실패와 마스크

`DirectDeco.Refresh()`에서 실패한 일반 이미지 하나는 `ReleaseItem()`으로 소유에서 빼고, 원래 Renderer를 정렬 키 위치에 끼워 그린다. 비사각형·비 affine UV처럼 네이티브가 표현하지 못하는 스프라이트도 Unity의 실제 도형을 사용한다. 이 때문에 다른 유효한 장식까지 전체 해제하지 않는다.

끼울 수 없는 재질·마스크라면 해당 키를 경계에 추가하고 경계를 넘는 소유만 돌린다. 원래 마스크와 네이티브 마스크의 혼합을 막기 위해 마스크/대상 중 하나가 반환되거나 경계에서 탈락하면 해당 마스크 소유 집합을 함께 Unity로 돌린다. 실패 직전 `Flags`가 아직 채워지지 않은 경우도 `SpriteRenderer.maskInteraction`으로 판정한다.

정적 실패 상위 10개는 `정적 실패 topN name=... slot=... field/value=...`로 기록한다. 누락 스프라이트, packed 스프라이트, 변환 체인, 비사각형/비 affine UV와 정점 수, scale / textureScale / repeat 값, 비유한 static 슬롯과 값, 텍스처 등록 실패, native statics 실패를 구분한다. 성공 사례의 구체적인 실패 이름·값은 게임을 새로 실행하지 않았으므로 아직 없다.

### 준비와 경계

- 같은 levelData를 이미 맡은 상태의 중복 PrepVfx는 다시 활성화하지 않는다. 재시작·편집/맵 전환·off/now는 기존 종료 경로를 유지한다.
- 정상 `Commit()`에서 전체 `Activate()`를 호출하던 경로를 제거했다. 텍스처 슬롯·도형·네이티브 객체·고정 버퍼·정렬 엔트리·첫 그리기 목록은 PrepVfx 후에 준비한다.
- 끼울 수 없는 렌더러, Canvas, 소유 밖 마스크는 **비활성 상태의 미래 켜짐도 포함하는 보수적 경계**를 준비한다. SetVisible/ApplyColor의 켜짐 변경만으로 경계를 재분류하지 않는다. 이 정책은 켤 수 있는 장식 수보다 순서 보존을 우선한다.
- 이후 SetDepth 이동에도 대응하도록, 소유가 있는 쪽의 안전 구간에 속한 남는 렌더러를 미리 끼우기 대상으로 준비한다. 초기 최상단/최하단 장식에 인접한 것만 준비하지 않는다.
- 실제 구조/재질/깊이가 달라지면 저장해 둔 Renderer·Canvas 목록으로 경계를 갱신한다. 전역 검색·텍스처/형태 재등록·전체 활성화는 하지 않으며 경계는 곡 중 완화하지 않는다. 안전 구간 밖으로 나간 끼우기 렌더러도 원래 층으로 반환한다.
- native 생성 때 큐의 8개 패킷에 채널·목록·run·최대 4,000구간 용량을 모두 예약한다. 목록 GPU 버퍼도 생성한다. 마지막 전체 statics 업로드 때 도형 GPU 버퍼를 준비한다. D3D11 즉시 컨텍스트의 실제 업로드는 렌더 스레드에 남긴다.

**한계:** 일반 켜짐 변경에 따른 전체 재컴파일 스파이크는 코드 경로에서 제거했다. 예외적인 구조 변화로 안전 구간이 줄어들 때는 캐시 목록 순회·부분 반환·정렬·명령 재기록이 필요하다. 대량 구조 변경의 최악 프레임 비용과 Grab RT 최초 생성 비용까지 무스파이크라고 검증한 것은 아니다. `commit_max_us`와 구조 변화 구간의 GC/최장 프레임을 별도로 측정해야 한다.

### 남는 렌더러와 풀

마스크가 없고 모든 사용 재질이 단일 패스인 SpriteRenderer, MeshRenderer, ParticleSystemRenderer(`seqparticles on`), LineRenderer, TrailRenderer를 `CommandBuffer.DrawRenderer(..., submesh, 0)`으로 끼운다. TMP / ADOFAI/Frames 등의 셰이더 이름 때문에 거절하지 않는다. 거절 이유는 종류·셰이더에 더해 `sequence-off`, `mask`, 재질 누락, 재질 인덱스별 passCount, 빈 재질 목록, 렌더러 종류/파티클 설정을 구분한다. 실제 각 맵 셰이더의 passCount와 새 소유 수는 다음 게임 로그에서 확인해야 한다.

각 끼우기 Renderer의 GO에 Update가 없는 `DirectDecoLifetime`을 준비한다. `OnEnable` / `OnDisable` / `OnDestroy`가 활성 목록을 갱신한다. 비활성 풀 전체를 매 프레임 확인하지 않는다. 활성 GO만 Renderer.enabled / forceRenderingOff, 사용 재질·패스 수·마스크·정렬·거리 변화를 확인한다. 재질 조회에는 준비한 List를 재사용한다. 준비 이후 활성 Renderer 수가 많으면 비용도 증가하므로 `seqAwake`와 함께 측정해야 한다.

`DirectRenderVisibilityIndex`는 64비트 단위의 희소 켜짐 목록이다. 명령 재기록은 네이티브 이벤트와 켜진 Renderer만 순서대로 방문하며, 그리기 목록 작성은 별도의 네이티브 엔트리 목록을 사용한다. 재질 배열·정렬 엔트리·고정 용량 컬렉션을 재사용한다. 준비된 풀의 켜짐/꺼짐은 관리 컬렉션을 새로 만들지 않는다.

GO가 여러 Renderer를 공유하면 층 반환도 같이 처리한다. 종료 시 수명 콜백을 해제하고 추가한 컴포넌트를 제거한다. 저장한 원래 GO 층과 원래 forceRenderingOff를 복원하며 InvisibleSkip의 현재 숨김도 반영한다.

### state

| 필드 | 의미 |
|---|---|
| `generation` | 실제 활성화 시도 세대. 평범한 가시성 전환에서는 증가하지 않아야 함 |
| `preparedSlots` | 이번 활성화에서 확보한 최초 슬롯 수. `owned + released`와 같아야 함 |
| `activationOwned` | 준비의 부분 실패/경계 처리를 마친 직후 소유 수 |
| `owned`, bg / fg / mask / masked / add | 지금 소유하는 항목에서 다시 집계한 값 |
| `released`, `staticFailures`, `rejected` | 이번 활성화의 반환 수, 정적 실패 수, 전체 미소유 수 |
| `seqRenderers`, `seqAwake` | 등록된 끼우기 렌더러 전체 수 / 활성 GO의 폴링 대상 수. 화면에 보이는 수와는 다름 |
| `boundaryChanges`, `boundaries`, `canvasBounds`, bg/fgBoundary | 캐시 경계 갱신 횟수, 남는 이유, 예약 Canvas 수와 Renderer 경계 layer/order |
| `prep_us`, `commit_us`, `commit_max_us` | 준비 시간, PreCull commit 평균과 최대. 준비 시간은 commit에 포함하지 않음 |
| `pollTiming`, `poll_us/frame` | `dr2 timing on`일 때만 장식 Poll 계측. 기본 끔. 계측 자체의 타이머 비용이 있음 |

`commit_us`는 바깥 장식 후크 비용 전부를 포함하지 않는다. 따라서 이 값만 <50µs여도 전체 메인 비용의 합격은 아니다. Poll 계측값과 commit은 일부 중첩되므로 무조건 합산하지 않는다. 성능 ABBA에서는 timing off를 쓰고 별도 진단 판에서 timing on을 쓴다.

## 오프라인 검증

증거 디렉터리: `out/direct-renderer/m1c/`.

| 검증 | 결과 / 증거 |
|---|---|
| DEV / Player / PlayerAuto / native | 모두 오류 0. `build-{dev,player,playerauto,native}.log`. 기존 관리 경고는 남음 |
| HLSL | 9개 변형 컴파일 통과 |
| export 이름·ordinal·hint | 73개 보존, 추가 없음. `exports-{before,after,result}.txt` |
| 현재 native 내장 | 최종 관리 DLL 3종에 현재 로컬 native DLL 바이트 포함. `embedded-native.json` |
| 설치된 DLL | StutterFix / sfnative SHA-256 전후 동일. `installed-{before,after}.json` |
| WARP 정상+12 블렌드, 연속 1/2개 | 26조건, 최대 채널 차 2 |
| UV 방향·축 교환 | 5,120픽셀 정확히 일치 |
| sRGB / 제한 영역 복사 | 통과, 8×8 장식의 여백 포함 복사 12×12 |
| 실제 VS 대 독립 TRS | 40조건, 최대 월드 오차 7.62939453e-06 |
| 빈 배경 stencil 초기화·안/밖·leave | 64 / 1,024픽셀 통과, 즉시 컨텍스트 상태 복원 통과 |
| M1c native 준비 | 첫 렌더 전 8개 패킷 용량·목록/도형 GPU 버퍼 준비 확인. 24프레임 동안 버퍼 교체 없음 |
| 희소 가시성 집합 | 110,000회 무작위 켜짐/꺼짐, 64비트 경계, 크기 축소/재확장, 4,200개 풀 순서 검증 통과 |
| 자료구조 할당 | .NET 8에서 100,000회 갱신·순회 0바이트. Unity Mono / 전체 DirectDeco 할당의 증거가 아님 |

WARP는 게임에 연결하지 않는 소프트웨어 D3D11 장치다. 수명 콜백·게임 후크·실제 파티클/TMP의 화면 순서·실게임의 부분 실패 복원은 여기서 실행하지 않았다.

재현 명령:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/direct-renderer/check-m1b.ps1 -NativeBuild -OutputDirectory out/direct-renderer/m1c
dotnet run --project tools/direct-renderer/m1c-policy/PolicyCheck.csproj -c Release
dotnet build StutterFix.csproj -c Release
dotnet build StutterFix.csproj -c Release -p:Edition=Player
dotnet build StutterFix.csproj -c Release -p:Edition=Player -p:AutoTestBuild=1
py -3.12 tools/direct-renderer/check-m1c-artifacts.py
```

마지막 artifact 검사에는 이번 디렉터리의 `installed-before.json`이 필요하다. native 빌드 후 관리 3종을 빌드해야 내장 native가 일치한다. M1b 증거 디렉터리는 덮어쓰지 않았다.

## Claude 실게임 bench

기존 실행기의 `drPix` / `drOn` / `ppGpuCam` 프리셋을 사용한다. 3440 렌더, 실제 RT/곡/시각 동일, 매 판 새 재생. 후보 공통 설정은 다음과 같다. 이 문서의 명령은 이번 작업에서 게임에 보내지 않았다.

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

| 프리셋 | 절차 |
|---|---|
| drPix | 후보를 **다음 PrepVfx부터** 켠 뒤 목표 시각 일시정지 → state → verify → d1 → off → d0 → framecmp. 상태 확인용 now를 꼭 써야 하면 명시적 재활성화이므로 그 세대는 별도 기록 |
| drOn | 후보 설정으로 새 재생. FPS·메인/렌더 CPU·GC·최장 프레임·commit 평균/최대·prep·generation·owned/seqAwake/경계/run/grab 기록. verify와 캡처는 성능 판에서 제외 |
| ppGpuCam | 기존 대조 설정 그대로, dr2 off 유지. 동일 조건으로 대조 수집 |

성능 ABBA: **A=ppGpuCam, B=drOn**, 각 판 새 재생. 같은 RT·창 설정·표시 설정·모드 구성과 측정 구간을 유지한다. 진단 timing on은 ABBA와 별도 판에서 사용한다.

| 맵 / 시각 | 우선 확인과 기대 지표 |
|---|---|
| HELLO 50초 | staticFailures가 생겨도 active 유지, 해당 이름/필드·값과 fallback 확인. 유효한 나머지까지 전체 취소되지 않아야 함 |
| WF 258초 | 같은 부분 실패 확인, drPix 우선. staticFailures / released / owned / seq 수를 함께 보관 |
| DD 50초 | 부분 실패와 Grab 순서 확인. grab_copies·pixels, CPU/렌더 비용 동반 확인 |
| Exit 88초 | 평범한 켜짐/꺼짐 동안 generation 일정, 활성화 로그 1회, owned는 구조 반환이 없으면 일정. 준비 수백 ms가 첫 commit에 포함되지 않아야 함. 기존 화면 차이 0 유지 여부 확인 |

합격 목표는 다른 픽셀 비율 <0.1%, 최대 채널 차 ≤3, **안정 구간 전체 직통 메인 비용 <50µs/프레임, 관리 할당 0**이다. <50µs는 commit·장식 후크·활성 풀 확인을 포함하는 별도 프로파일로 판단한다. state/verify/최초 준비 로그의 할당은 안정 구간에서 제외하고 전환 프레임은 따로 기록한다. 정확한 소유 수와 FPS 개선 폭은 예측하지 않는다. 보수적으로 비활성 경계까지 예약하므로 일부 맵의 소유 수가 M1b 최초 활성화보다 작을 수 있다.

회귀 필수: 판정 글자 풀 꺼내기/반환·Renderer.enabled만 변경·파티클/Trail/행성·비활성 부모 켜기·재질/깊이/마스크 변경·부분 정적 실패·off/now·재시작·맵 전환·Unload. 등록 이후 새로 만들어진 외부 Renderer와 사용 재질 배열의 구조 변경도 확인한다. 전역 검색은 준비 때만 수행하므로 준비 이후 새로 생성되는 외부 Renderer까지 자동 등록하는 것은 보장하지 않는다. 화면 순서·복원이 틀리면 해당 증거를 실패로 남기고 원래 경로로 돌린다.

실게임 금지 조건 때문에 위 판정은 Claude 측 실행에 남아 있다. 이 결과로 기본 켬이나 실게임 M1c 목표 달성을 선언하지 않는다.
