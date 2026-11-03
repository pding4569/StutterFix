# G1 코드 구조 — 합격 전 후보

2026-11-03. 이 문서는 현재 구현을 설명하며 G1 합격 증명은 아니다.
[결과와 중단 근거](g1.md): Arche 통합 그리기에서 허용치 초과가 재현되어 중단했다.
G2 Camera·행성, 실제 깊이 변경, 크롭/원래크기의 실제 변경, 반복/직접 마스크 처리는 확장하지 않았다.

## 파일별 역할

| 파일 | 역할 |
|---|---|
| `DecoBake.cs` | Setup 시 16채널과 부가 상태 수집. 전체 PrepVfx 순서/태그별 대상 수집. 조건 이벤트·외부 setter·초기 마스크 등 제외 유지. 필드 사용 여부만 보던 가시성/깊이/마스크 등의 거부를 대상별 상태 판정으로 옮김 |
| `DecoBake.G1.cs` | G1 부가 상태와 그리기 상태의 소유자. 상수 필드 판정, 원본 부가 이벤트 입력 가드, 논리 visible과 GPU 그리기 분리, 전역 통합 맵의 숨김 슬롯 반영 |
| `DecoBake.Core.cs` | Unity 없는 순수 표 컴파일러. 연속 16채널 유지. 12/13 시차 끝값도 scale과 같은 float 플러그인 산술 사용 |
| `FastMove.cs` | 이벤트 대상 수집·실행 분기. 원본 경로의 소유 대상 교집합 확인. 없는 이미지의 이미지 장식/파티클 예외 구분. 같은 크롭 값일 때만 미구현 크롭 writer를 생략 |
| `GpuDecoRecovery.cs` | 이벤트 시작 전 G1 부가 입력이 준비 시와 같은지 확인. 기존 PrepareGpuEffect/복귀/재개 계획 유지 |
| `GpuDecoExport.cs` | GPU 소유·채널 상태·슬라이스. 가시성 이벤트를 연속 채널 커밋 뒤 실행. 숨김을 독립 렌더러와 통합 슬롯 양쪽에 적용. CPU 복원 때 최신 논리 visible 및 카메라 잠금 pose 반영 |
| `GpuDecoScene.cs` | 준비 시 원본 renderer.enabled와 rendererEnabled 일치 검사. 논리적으로 숨겨진 장식도 후보가 될 수 있게 함. 기존 재질/정렬/기하/활성 계층 검사 유지 |
| `GpuDecoPlayer.cs` | 제품 생명주기, 준비/활성화/재시작/종료. 매 프레임 표본 안전 검사에서 억제된 enabled와 계획된 빈 스프라이트를 구분 |
| `GpuDecoImagePlayback.cs` | 이미지 변형 입장·실제 setter·물리 geometry 갱신. Missing 변형을 빈 그리기로 지원. 준비 후 이미지가 새로 생기거나 자산이 바뀌면 복귀 |
| `GpuDecoPackets.cs` | 네이티브 일괄 채널 처리. image/visible 이벤트는 대상 순서의 부가 동작을 보장하도록 일괄 경로 우회 |
| `GpuDecoDrawCollapseRenderer.cs` | 기존 통합 그리기·장벽 감시. 동적으로 독립 렌더러로 내려간 항목의 숨김 판정에도 G1 상태 반영. 기존 정렬/자원 안전 검사 유지 |
| `GpuDecoRenderMerge.cs` | 기존 생략/가시성 가드에도 G1 숨김 상태 반영 |
| `tools/decobake-contract-check/Program.cs` | 실제 컴파일러를 연결한 12/13 끝값 상쇄 반례 추가. 실게임 성능/그리기 검사는 아님 |
| `tools/optimization/GpuDecoPlayerCheck/Program.cs` | 생명주기·정책·최종 PE/내장 번들·진단 경계 검사. 기존 제품 기본값과 어긋난 검사 기대만 정정 |
| `tools/optimization/g1-evidence.py` | 게임 종료 후 기존 로그·실행 JSON·마지막 대조 PNG의 보존/해시 및 제한된 수치 추출. 게임 실행 기능 없음 |

## 실행 흐름

1. `SetupPostfix`가 원본 초기값과 `G1Initial`을 기록한다. seek가 초기값을 덮지 않게 기존 initial 캐시를 따른다.
2. `Collect`가 전체 예약 순서를 보존하고, false visible 쓰기 가능 대상과 이미지 쓰기 대상을 먼저 수집한다.
   대상별 부가 목표가 초기 상태를 바꾸면 그 장식을 곡 전체에서 제외한다. 한 번이라도 바뀌면 제외하므로,
   모든 쓰기가 Setup 값과 같은지 보는 방식으로 곡 전체 상수 여부를 판정할 수 있다.
3. `SetMaskingType`은 같은 값이어도 renderer를 다시 켜므로 초기 visible이 참이고 false 쓰기가 전혀 없는 대상에만 같은 값 생략을 허용한다.
   부가 효과의 used 비트/목표 스냅샷은 `G1EventGuard`에 별도 보존한다. 연속 채널 표에 혼합하지 않는다.
4. 기존 GPU 준비가 이미지/기하/재질/텍스처 랩/바인딩 예산/정렬 검사를 수행한다. 표 지원 수는 실제 소유 수가 아니다.
5. `StartGpuLive`가 현재의 논리 그리기 visible을 저장하고 원본 renderer를 억제한다. 기존 CPU 기록의 분류/분리/롤백도 수행한다.
6. 이벤트 실행은 `FastMove.Run → SkipGpuOrAdopt → GpuLiveSkip`을 따른다.
   부가 입력 가드와 이미지 자산 검증을 먼저 수행하고 연속 채널을 커밋한 뒤 visible을 원본 setter로 논리 상태에 적용한다.
   원본 renderer는 다시 억제한다. 이미지 setter는 기존 대상 순서에서 별도로 수행한다.
7. 실제 그리기 숨김은 `g1DrawVisible` 또는 `g1ImageEmpty`로 판단한다. 독립 렌더러/복사 마스크를 갱신하고,
   통합 경로에서는 기존 `_CollapseMap` 슬롯을 `uint.MaxValue`로 바꿔 정점을 비운다. 논리 opacity/색 채널은 덮지 않는다.
8. 매 프레임 기존 source 이징 진행 및 GPU 산술을 유지한다. 표 이진 탐색 방식으로 바꾸지 않았다.
9. FastMove 원본 경로로 가면 현재 대상과 소유 집합의 교집합을 확인한다. 소유 대상 0이면 원본 효과만 실행한다.
   소유 대상이 있거나 대상 판정이 불명확하면 기존 전체 복귀를 수행하고 사유를 기록한다.
10. `StopGpuLive`는 최신 채널과 재개 기록을 CPU에 돌린다. renderer.enabled는 저장된 논리 visible로 복원한다.
    잠금/시차 축 플래그가 있으면 논리값이 같아도 `UpdatePosition`을 호출해 현재 카메라를 반영한다.

## 주요 상태와 플래그

| 이름 | 의미 |
|---|---|
| `Main.Config.GpuDeco` | 제품 GPU 장식 처리. **기본 0 유지** |
| `Main.Config.GpuDecoImages` | 기존 이미지 소유 옵션. 기본 1 유지 |
| `g1Initial` | Setup 시 초기 visible·depth·crop·textureScale·smoothing·mask 종류/범위/대상·초기 이미지 크기 |
| `g1EventGuards` | 준비 이후 unsupported 부가 이벤트 인자가 바뀌지 않았는지 검사하는 값 스냅샷. 불일치는 복귀 |
| `g1DrawVisible` | rendererEnabled에 대응하는 논리 그리기 켜짐. 물리 원본 enabled 억제와 별개 |
| `g1ImageExpectedEmpty` | 현재 이미지 setter가 계획된 Missing을 적용할 수 있다는 증거 |
| `g1ImageEmpty` | 계획된 setter가 실제 물리 sprite를 비운 상태. 다음 정상 이미지가 geometry를 갱신할 때 해제 |
| `DrawCollapsePolicy.GlobalVerts` | 현재 기본 true. G1 통합 슬롯 숨김은 이 전역 맵 경로만 지원하며 다른 통합 모드에서는 거부 |
| `DrawCollapsePolicy.DisableOwnedRenderers` | 시작 커밋부터 기본 true. 원본 enabled 억제 및 해제 시 논리 상태 복원 |
| `DrawCollapsePolicy.KeepAfterBarrier` | 기존 기본 true 유지. 픽셀 차단의 관련 후보이나 이번 실험으로 단독 원인을 확정하지 못함 |
| `GpuDecoPlayerPolicy.MinimumOwned` | 기존 64개 유지. 비율/구간당 평균 수의 새 활성화 조건은 아직 없음 |

## 안전장치와 한계

- 초기 마스크, 마스크 대상, 실제 깊이/크롭/원래크기 변경, Camera·행성 배치 등 기존 범위 밖 동작은 계속 제외한다.
  이미지 쓰기가 있는 대상의 원래크기 쓰기는 이미지 차원 의존성이 있어 보수적으로 제외한다.
- Vector2 목표 비교는 Unity의 근사 `==` 대신 `.Equals`로 수행한다. 미세한 실제 변경을 상수로 오인하지 않는다.
- depth, crop, size, smoothing, mask 부가 필드의 준비 후 변경을 매 이벤트 처음 파싱할 때 검증한다.
  새/모호한 source, 외부 자산 변경, 알 수 없는 원본 대상은 조용히 통과시키지 않는다.
- 입장 단계는 초기 스프라이트/쿼드가 있는 이미지 장식만 대상으로 한다. 처음부터 빈 장식, 빈 이름 NullArgument는 기존 CPU 입장 정책을 유지한다.
  Missing은 물리 geometry를 새로 만들지 않고 마지막 유효 항목을 숨기며, 정상 이미지 복귀 시 기존 갱신 경로를 사용한다.
- 파티클의 없는 이름은 게임의 사전 `[]` 예외/중간 중단 의미가 있어 원본 경로를 유지한다.
- visibility/image 이벤트는 네이티브 일괄 채널 경로를 우회하므로 CPU 비용이 늘 수 있다. 구간당 평균 수와 소유 비율에 따른 손익 기준은 미측정이다.
- 기존 장벽 감시/복귀 검사는 제거하지 않았다. 전수 감시의 CPU 비용이 남아 있다.
- 정지 프레임 보조 대조는 CPU 원본의 독립 완주를 대신하지 않는다. 최신판 6맵 전체/다시 하기/가시성 전환 직후/ABBA는 미완료다.
- **통합 그리기는 Arche에서 >3 픽셀이 남는다.** 기본값을 끄는 진단은 원인 분리에만 사용했고 제품 수정으로 채택하지 않았다.
- 일반 활성화 조건을 포함해 G1을 완료한 버전으로 취급하면 안 된다. 설치는 정식 Player DLL로 복원되어 있다.
