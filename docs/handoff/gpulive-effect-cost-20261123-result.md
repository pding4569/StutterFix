# GPU live 장식 이동 효과 비용 작업 결과 (2026-11-23)

작업 위치: `E:\StutterFixWork\wt-v14prod`, 브랜치 `claude/v3-282port`, 시작 HEAD `d6d8b3e`.
게임 실행·종료, 설치 DLL 교체, 커밋·푸시는 수행하지 않았다. 기존 `StutterFix.cs` 작업 상태와 미추적 자료를 보존했다. 게임 디컴파일 자료를 새로 만들거나 저장소에 넣지 않았다.

## 완료한 범위와 현재 판정

이미 구현되어 있던 `MasterSlotList` 제거 최적화, `WriteMasterSlot` 직접 쓰기, `in Anchor` 전달, `effectcache`를 이어받았다. 효과별 캐시를 중복 구현하지 않고 **장식당 슬라이스 일괄 전파와 정확한 더티 구간 합치기**, 원래 계산과 비교하는 AUTOTEST 대조 모드를 추가했다.

**실험 모드 기본 끔. 실게임 동일성·프레임 시간 개선은 미측정이며 제품 기본값으로 승격하지 않았다.** 오프라인 더티 시험과 빌드는 통과했다. 다음 게임 시험은 Claude 담당이다.

## 변경 파일

| 명시적 경로 | 변경 |
|---|---|
| `E:\StutterFixWork\wt-v14prod\GpuDecoExport.cs` | `MarkEffectDirty`, 대조용 스냅샷, 채널 쓰기의 선택적 전파, 효과 시작·종료 대조 연결 |
| `E:\StutterFixWork\wt-v14prod\GpuDecoEffectCost.cs` | 새 파일. 슬라이스별 일괄 전파, 원래 16채널 계산의 복사본 oracle, 장식별 및 효과 전체 배열 비교, AUTOTEST 명령 |
| `E:\StutterFixWork\wt-v14prod\StutterFix.csproj` | 새 소스 파일 컴파일 등록 |
| `E:\StutterFixWork\wt-v14prod\tools\optimization\check-gpulive-effect-dirty.py` | 실제 두 더티 메서드를 추출·컴파일해 비교하는 Unity 비의존 시험 |
| `E:\StutterFixWork\wt-v14prod\docs\handoff\gpulive-effect-cost-20261123-result.md` | 이 결과 문서 |

## 변경의 동일성 근거

1. `ApplyGpuLiveEffectToTarget`의 채널 순서, 부동소수점 식, 상대 위치의 Kill 전 값 읽기, Kill 후 `.end` 사용, 소스 detach/add, `WriteMasterSlot`, 복구용 생성 순서 기록을 유지한다. 일괄 모드는 슬라이스에 복사하는 시점만 같은 장식 처리 함수의 채널 루프 직후로 옮긴다. `ApplyG1Visible`·`ApplyG1Depth` 호출 전에 전파가 끝난다. 프레임이나 효과를 지연하지 않는다.
2. `PropagateEffectToSlices`는 동일한 `gpuLiveMasterToSliceItem`의 동일 슬라이스/장식/채널 위치에 쓴다. 네이티브 커널이면 fallback anchor를 건드리지 않는 기존 조건도 유지한다. 채널·앵커 값은 기존 setter가 기록한 배열에서 가져온다.
3. 장식의 16채널은 256채널 더티 블록 경계를 넘지 않는다. `MarkEffectDirty`는 실제 마스크의 최저·최고 채널을 이용하므로 기존 `MarkDirty` 반복과 정확히 같은 `dirtyFirst/dirtyEnd`, 블록 bool, 최소·최대 블록을 만든다. 전체 16칸이나 블록 전체로 넓히지 않는다. 알파 10·11 중 어느 하나가 포함되면 기존처럼 그 장식을 한 번만 pending 목록 끝에 넣는다. 장식 간 처리 순서는 그대로다.
4. `GpuRecoveryTesting`에서는 채널별 즉시 전파를 유지한다. `target_after_channel` 오류 주입·롤백이 요구하는 중간 상태를 일괄 경로로 바꾸지 않는다.
5. 렌더러 활성화, visible/depth 처리, 소스 수명·멤버십 및 생성 순서 관련 코드·로그를 재정렬하지 않는다. `GpuLiveFrame`, `ApplyAlpha`도 그대로다. 정상 실행에는 새 로그가 없고 명령/불일치 때만 진단 로그가 추가된다. 이 항목은 코드 경로 근거이며 렌더러·로그의 실게임 독립 대조 완료를 의미하지 않는다.

## 후보별 판단과 예상 절감

`N`=한 효과의 GPU 대상 장식 수, `K`=실제로 쓰는 채널 수, `S`=검사하는 슬라이스 수, `P`=해당 장식이 들어 있는 슬라이스 수. 단가는 Mono 실측 전이므로 아래는 호출 수 기준이며 ms 예측은 하지 않는다.

| 후보 | 처리·같음 근거 | 예상 절감과 한계 |
|---|---|---|
| 효과별 채널/이징 캐시 | 기존 `GpuDecoRecovery.cs:CacheGpuEffect` 재사용. `BeginGpuSource`마다 parsed 상태가 초기화되고 `PrepareGpuEffect`의 동일 효과 참조 guard 뒤에서 한 번 계산된다. `gpudeco effectcache on` | 채널 선택 검사 `16N → 16` + 실제 K채널 순회, 해당 이징 끝값 계산 `N → 1`. 절감은 `(N-1)×t_ease` 및 사라진 NaN 검사 비용. Prepare 자체는 이미 조기 반환하므로 없던 대규모 파싱 절감을 주장하지 않는다. |
| 슬라이스 전파 일괄 | 이번에 구현. master 계산·바인딩 순서를 유지하고 슬라이스에 같은 값을 복사 | 맵 조회 `NK → N`, 슬라이스 방문 `NKS → NS`, 커널 조건 조회 `NKP → NP`. 배열 원소 복사 `NKP`는 유지. 별도 마스크 순회가 생기므로 작은 K에서는 이득이 작거나 손해일 수 있다. |
| 더티 표시 합치기 | 이번에 구현. 블록 범위 및 알파 대기 순서 보존 | `MarkDirty` `NKP → NP`; 약 `NP(K-1)×t_dirty` 절감에서 마스크 최저/최고 탐색 비용을 빼야 한다. K=16이면 호출 수 93.75% 감소이며 프레임 시간 93.75% 감소를 뜻하지 않는다. |
| decoration 사전 대신 정수 인덱스 | 보류. `gpuLiveMasterByDeco` 값은 현재 GPU 소유 배열의 인덱스다. `table.Decorations` 인덱스와 같지 않고 fallback/재준비 수명 검증이 필요하다. 외부 게임 타입에 필드를 직접 추가할 수 없다. | 대체 테이블도 사전이면 lookup 1회를 제거하지 못한다. 타깃 목록의 위치를 재사용하려면 현재 준비 세대·참조·목록 변경 guard가 필요하다. 목표는 `N×t_dictionary`지만 이번 변경은 이 비용을 제거하지 않는다. 기존 packet target/guard 경로를 추가 조사할 후보로 남긴다. |
| Renderer.enabled 읽기 캐시 | 적용하지 않음. `GpuDecoPlayer.cs`의 모드 정리와 `GpuDecoRecovery.cs:RecoverGpuLive`의 강제 정리도 slice renderer를 직접 끈다. `GpuLiveSlice.ApplyAlpha` 한 곳만 바꾼 캐시는 충분하지 않다. | `ApplyAlpha`당 엔진 getter 1회가 상한이며 writer 전체·외부 변경 소유권 확인 없이는 정확한 캐시를 주장할 수 없다. 이번 변경은 프로파일의 해당 약 5ms 묶음을 해결하지 않는다. |
| 추가: 전체 효과 일괄 커밋 대신 장식 경계 사용 | 기존 `GpuDecoPackets.cs`는 별도 준비·타깃 검증·네이티브 경로를 갖는다. 이번에는 그 경로를 제품에 강제하지 않는다. | GPU 대상이 섞인 FastMove의 원래 처리 순서와 복구 경계를 유지하면서, 장식 내부 반복 비용만 줄이는 선택이다. |

## 대조 모드

새 명령은 모두 `#if AUTOTEST`에서만 노출된다. `AutoTest.cs`의 기존 `gpudeco` 분기 → `GpuStep`으로 들어가므로 별도 최상위 명령 추가는 필요하지 않다.

```text
gpudeco effectbatch on|off
gpudeco effectcheck on|off
gpudeco effectcheck reset
gpudeco effectcheck status
```

- `effectbatch`는 GPU 소유가 비활성일 때만 변경 가능하다. 기본 false다.
- `effectcheck`는 새 계산 전 상태를 복사하고, 원래 0..15 채널 순회와 NaN 검사·이징 계산·채널별 더티 표시를 별도로 계산한다. 기존 effectcache를 끄거나 켜도 oracle은 캐시를 사용하지 않는다.
- 장식별 16개 master 채널/48바이트 anchor와 대응 slice row를 원시 바이트로 비교한다. placement와 startPos 비트, 더티 구간·블록·알파 pending의 추가 순서도 비교한다.
- 효과의 첫 GPU 대상에서 전체 master/slice 배열·더티 배열·알파 pending을 한 번 복사한다. 원래 계산 결과를 복사본에 누적하고 `EndGpuSource`에서 **전체 배열**과 pending 목록을 비교한다. 장식당 전체 배열을 복사하는 O(N²) 검증을 피한다.
- `checks`, `mismatch`, `whole_checks`, `whole_mismatch`를 보고한다. 표본 수 0은 성공이 아니다. 이 비교는 소스 멤버 목록·렌더러 상태·화면 픽셀을 독립 재실행하지 않으므로 기존 `slotcheck`, 복구/픽셀 검증도 필요하다.
- 대조 켬은 할당·전체 배열 복사·이징 재계산을 포함한다. FPS/끊김 측정에 사용하지 않는다.

Claude 실행 순서 예시(현재 runner의 Arche 절대 경로를 `<Arche FULL.adofai>`에 사용):

```text
timeout 750
auto on
gpudeco packets off
gpudeco effectcache on
gpudeco effectbatch on
gpudeco effectcheck reset
gpudeco effectcheck on
slotcheck on
game <Arche FULL.adofai>
press
waitsongpos 280 700
gpudeco effectcheck status
slotcheck
gpudeco effectcheck off
quit
```

`effectcache`/`effectbatch`를 `game` 앞에서 설정해 자동 GPU 소유 활성화 이전 조건을 만족시킨다. `timeout 750`은 기본 단계 제한 180초가 `waitsongpos 280 700`을 먼저 끊지 않게 한다. 기존 runner에서 PlayerAuto를 선택하고 현재 GPU 기본 설정을 유지한다. `checks>0`, `whole_checks>0`, 두 mismatch=0, slotcheck 불일치 0 및 GPU 복구/예외 로그 없음이 최소 통과 기준이다. **이 순서는 작성·소스 검토만 했으며 실행하지 않았다.**

성능은 별도 프로세스 ABBA에서 `effectcheck off`, `slotcheck off`, `costprobe off`로 측정한다. ① cache off/batch off 기준, ② cache on/batch off, ③ cache off/batch on, ④ cache on/batch on을 분리해 219.6초·274.8초 프레임을 비교한다. 대조용 packets는 off로 유지한다. 정상 프레임 비용·최악 프레임·해당 효과 대상 수/채널 수와 기존 픽셀 기준도 함께 확인한다. 본 작업에서는 측정값을 추가하지 않았다.

## 수행한 검증

```powershell
py -3 tools/optimization/check-gpulive-effect-dirty.py
dotnet build StutterFix.csproj -p:Edition=Player --nologo -v:q
dotnet build StutterFix.csproj -p:Edition=Debug --nologo -v:q
dotnet build StutterFix.csproj -p:Edition=Player -p:AutoTestBuild=1 --nologo -v:q
git diff --check
```

| 검증 | 결과·기록 |
|---|---|
| 실제 소스 더티 비교 (.NET 8) | 6,291,360 cases, mismatch=0. 65,535개 비어 있지 않은 채널 마스크 × 8개 알파 기능 조합 × 4개 블록 경계 장식 × 3개 기존 더티/대기 상태. 전체 dirty/alpha 배열 및 pending 순서 비교. `out/gpulive-effect-dirty-fixture.txt` |
| Player | 오류 0, 경고 67. `out/gpulive-effect-cost-player-build.txt` |
| Debug | 오류 0, 경고 87. `out/gpulive-effect-cost-debug-build.txt` |
| Player + AUTOTEST | 오류 0, 경고 65. `out/gpulive-effect-cost-autotest-build.txt` |
| diff 공백 검사 | 통과 |

## 남은 위험과 다음 우선순위

1. Mono 실게임에서 oracle mismatch=0 및 두 고비 프레임의 실제 개선은 미검증이다. 오프라인 시험은 Unity 렌더링이나 Mono 산술의 증명이 아니다. 기본값은 계속 끔이다.
2. 동기 함수 안에 렌더/업로드 콜백이 끼어들지 않는 현재 호출 구조를 전제로 한다. 정상 경로의 배열 접근 실패·예상하지 못한 예외로 장식 중간에 중단되면 일괄 경로는 기존 채널별 전파와 다른 중간 상태가 될 수 있다. 오류 주입은 그래서 기존 경로를 사용한다. 대조/복구/재시도 시험 없이 기본 활성화하지 않는다.
3. 더티 계산은 보존했으나 `ApplyAlpha` 엔진 읽기와 master 사전 조회는 남는다. 현재 프로파일 25.5ms 전체를 이번 절감량으로 해석하지 않는다.
4. 대조 모드의 전체 배열 스냅샷은 큰 맵에서 상당한 시간·메모리를 쓴다. 대조 판의 프레임 수치를 성능 결과로 사용하지 않는다.
5. Claude가 대조 판 → 픽셀·복구 판 → capture 없는 성능 ABBA 순서로 검증하고, 이득이 확인된 부분만 별도로 승격 판단한다.
