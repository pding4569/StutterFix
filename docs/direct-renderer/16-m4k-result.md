# M4K 구현·오프라인 검증 결과

2026-11-16. `E:\StutterFixWork\wt-m4`, 브랜치 `claude/drfull`, 기준 HEAD `4a7a23881d00f97f29d6c9c00549e78dc4e0ca4b`.
`16-m4k-spec.md`, `15-m4j-spec.md`, `15-m4j-result.md`를 확인했다.

**자원 수명·업로드 패턴 수정, 컬링 뒤 상수 준비, collect의 반복 메타데이터 탐색 제거, 실제 draw 범위에 맞춘 진단 및 GPU 자원 구간 분리를 구현했다. 4종 빌드 오류 0, export 74/74 보존, 기존·신규 WARP fixture 통과다.**

**Exit B≥A, DD(1024) B≥A, 정상 VRAM의 GPU 추가 0.6~1.2ms 제거, main 0.83ms·render 1.19ms의 원본 이하 도달, 초과 VRAM에서 붕괴 제거는 실게임 미측정이다.** 게임 실행 금지 조건을 지켰으며 이 목표들을 달성했다고 보고하지 않는다. DD 비용은 기존 실게임 파일에서 Grab 셰이더 draw들까지 좁혔지만, 복사·텍스처 읽기·페이지 이동 중 어느 것이 지배하는지는 새 계측으로 확인해야 한다.

커밋, 게임 실행·조작·종료, 설치 DLL 교체·복원은 하지 않았다. 저장소의 `native/sfnative.dll`과 `bin`의 로컬 산출물만 빌드했다. 설치 DLL 두 개의 전후 SHA-256은 동일하다. 기존 M4~M4J 산출물은 삭제하지 않았다.

## 1. 기존 DD 진단에서 실제 표본을 복구했다

설치 폴더에 남아 있던 기존 TSV를 읽기 전용으로 조사했다. `0000/0256`은 모든 데이터가 사라진 상태가 아니었다. **GPU 시간 본문은 각각 1,024행·224행이 남아 있고, 느린 draw의 텍스처 메타데이터가 NRE 행으로 대체돼 있었다.**

파일은 `out/direct-renderer/m4k/prior-game-evidence`에 복사하고 원본 경로·SHA-256을 기록했다. 각 시간 덤프를 바로 앞의 **같은 판 probe 목록**에 연결했다. 판 사이 native ID는 재사용되므로 서로 연결하지 않았다. 실제로 490/848/849는 두 판에서 서로 다른 셰이더를 가리킨다.

| 기존 덤프·측정 범위 | GPU segment 평균 | 측정된 Grab 셰이더 draw 합계 | 표본 |
|---|---:|---:|---:|
| `160141-236`, ordinal 0..255, segment 0 | 16,330.656µs | **15,020.800µs**, 5 draw | 4프레임 |
| `160341-926`, segment 0의 ordinal 256 이후 일부 | 16,316.408µs | 0µs — 앞쪽 Grab은 이 창 밖 | 4프레임 |
| 같은 `160341-926`, segment 2 | 22,104.064µs | **19,825.920µs**, 6 draw | 4프레임 |

첫 판의 140..144는 `5P emcc.png`의 `Hidden/BlendModes/SpritesDefault/Grab`으로, 각각 약 **3,003~3,006µs**다. 두 번째 판에서는 `5P square.png`, `nes_nonglow_letterbox.png`, `gr3.png`, `lb.png`의 Grab draw들이 크다. 첫 창에서 Grab 비중은 약 92.0%, 두 번째 판 segment 2에서는 약 89.7%다.

이 수치는 **기존 M4J 실게임의 draw setup + Grab 복사 + 실제 draw 경과 시간**이다. M4K 성능 측정이나 복사만의 3ms라는 뜻이 아니다. 서로 다른 판의 segment를 합쳐 한 프레임의 정확한 41ms로 만들지 않는다. 일반 `Sprites/Tile`의 `nev_circle_line.png`도 약 1,860µs가 있어 모든 비용을 Grab에 귀속하지 않는다.

현재 Player.log의 DD 상태는 native ID 약 1,052개, `Stats.Draws/Frames` 약 315회/프레임이었다. 후자는 composite를 포함하고, 남은 시간 덤프에서 관찰한 native ordinal은 0..311이다. 따라서 ID 수를 보고 512/768/1024 창을 계속 열면 비어 있게 된다. 새 상태는 실제 native ordinal의 마지막/최대 개수를 따로 표시한다.

재현:

```powershell
py -3.12 tools/direct-renderer/analyze-m4k-existing.py
```

분석 본문: `out/direct-renderer/m4k/prior-game-evidence/analysis.json`.

## 2. 자원을 계속 붙잡던 수명을 수정했다

기존 `Fill`은 텍스처·상태·기하 구성이 바뀔 때 새 native draw ID를 만들었다. 옛 draw는 `draws`에 계속 남았고, `textureViews`도 강한 COM 참조를 보관했다. 따라서 **목록에서 사라진 옛 draw와 SRV 캐시가 과거 텍스처를 계속 보유할 수 있었다.** 이것은 코드로 확인한 자원 수명 문제다. 정지 DD의 현재 40ms가 이 누적 보유 때문에 발생했다고 입증한 것은 아니다.

수정:

- append-only `ReplaceDraw` 연산으로 교체 관계를 전달한다. 옛 ID는 교체 packet이 소비되기 전까지 유효하다.
- 렌더 스레드가 새 목록을 소비하고 옛 ID가 빠진 것을 확인한 뒤 옛 texture/SRV lease·CB 참조·색 버퍼를 놓는다. 구조 변경으로 없어진 pass도 같은 시점에 retire한다.
- SRV 캐시는 weak lease를 사용한다. 현재 draw끼리 같은 SRV를 공유하면서도 캐시가 텍스처를 영구 보유하지 않는다. 만료된 키는 기존 주기 정리에서 제거한다.
- shader/geometry와 안정된 GPU 슬롯 주소는 유지한다. 아직 사용하는 draw나 GPU 명령의 자원을 임의로 파괴하거나, 현재 draw 목록을 재정렬하지 않는다.
- `retired_draws`를 상태에 추가했다.

WARP는 **옛 목록을 다시 제출해도 보존 → 교체 목록 소비 후 옛 텍스처 항목 0 → 후속 pass 제거 시 추가 retire**를 검증했다. retire=2. Unity가 다른 곳에서 가진 텍스처 참조까지 해제하거나 실제 VRAM 사용량이 그 바이트만큼 감소했다고 주장하지 않는다.

## 3. 정상 VRAM에서도 불필요한 일을 줄였다

### 상수 준비와 collect

- native GPU 경로의 컬링 결과를 segment당 한 번 만들고 상수 준비와 실제 제출에서 공유한다. 기존 배치 경계·보수적 bounds·draw 순서는 유지한다.
- 화면 밖 draw의 object CB를 patch·비교·arena 복사하지 않는다. 다시 보이면 현재 pose/material/camera/time dependency로 갱신한다.
- managed bind 때 material 변수의 쓰기 위치와 tint 위치를 미리 계산한다. 색 변경 때 CB 전체 변수 목록을 다시 탐색하지 않고, 일반 Fill에서도 매 변수의 `Dynamic(name)` 분류를 반복하지 않는다.
- CB의 property ID, shader 기본 property의 ID 사전, 정적 render state를 준비 단계에서 보관한다. 동적 state와 mask 전환은 다시 평가한다. 변경 통지·정렬 키·MPB 검사를 임의로 생략하지 않았다.

200개 draw 중 100개가 화면 밖에서만 움직이는 fixture에서 32프레임 동안 **추가 ring 업로드 0바이트**였다. 후보 CB 6,800개 중 visible CB는 3,400개였다. 재진입 시 업로드가 발생하고 결과 픽셀은 동일했다. M4J의 6MiB 상수 집합 정지 16프레임 업로드 0, 희소 변경·arena 순환·다중 segment 검증도 통과했다.

이 변경은 불필요한 작업을 제거한 것이다. 실게임 collect 451→148µs 복귀나 main/render 목표 달성을 측정한 결과는 아니다.

### pose/draw 업로드와 바인딩

- immediate 경로는 pose/draw를 고정 DEFAULT 버퍼의 실제 변경 범위에 `UpdateSubresource`한다. 이 두 스트림의 매 프레임 명시적 `WRITE_DISCARD`와 dense dynamic SRV 직접 바인딩을 제거했다.
- deferred 경로는 기존 staging/복사 경로를 유지한다. [공식 UpdateSubresource 문서](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-updatesubresource)의 deferred nonzero box offset 문제를 피한다.
- 셰이더가 바뀌면 다음 셰이더가 사용하지 않는 이전 VS/PS SRV를 해제한다. Grab 직전에는 16개만 지우던 처리를 전체 128개 slot으로 고쳤다.

3개 source가 8프레임 움직이는 대조에서 고정 업로드는 default update 9회·2,016바이트, 명시적 DISCARD 10회였다. 이전 rename 대조는 default update 0회·DISCARD 19회였다. 두 경로와 pretransform-off 대조의 픽셀은 동일했다. slot 127의 이전 SRV 해제와 Grab 복사도 별도 검증했다.

`UpdateSubresource` 내부 복사·드라이버 allocation 전략은 별개다. 위 카운터가 페이지 이동 제거, GPU 시간 감소 또는 실제 FPS 향상을 입증하지 않는다. 현재 경로는 원본 셰이더·이미지 품질·필요한 unnamed Grab 횟수를 줄이지 않는다.

## 4. drawgpu가 표본과 자원 구간을 남기도록 바꿨다

```text
drfull off
drfull resources default
drfull constants ring
drfull drawgpu on 0 128 4
drfull on
drfull wait
# 실제 렌더가 충분히 진행된 뒤, on 상태에서:
drfull drawgpu dump
drfull drawgpu assert
```

`first/count`는 native ID나 source 수가 아닌 **컬링·배치 후 실제 native 제출 ordinal**이다. 일반 ABBA에서는 `drfull drawgpu off`를 쓴다.

- 해당 창의 draw가 없는 segment는 query slot을 소비하지 않는다. 슬롯 8개 중 비어 있는 것을 찾고, 전부 pending이면 건너뛴다. pending 작업을 덮지 않는다.
- 빈 첫 segment에서도 프레임 경계·query 회수를 처리한다. interval은 새 계획에서 64→4로 줄였다.
- `last_visible`, `max_visible`, `empty_windows`, `sampled_segments`, `completed_segments`를 제공한다. 빈 결과는 `OUT_OF_RANGE` 또는 `NO_COMPLETED_SAMPLES`로 구분하고 `assert`는 실패한다.
- 기존 GPU 시간 본문을 먼저 저장하고, 별도 `-draws.tsv`에 경로·셰이더·Grab 이름을 남긴다. 텍스처 행 하나의 Unity 메타데이터 접근 실패로 시간 본문과 전체 identity를 잃지 않는다. 행별 예외에는 스택을 포함한다.
- `-status.txt`, `-textures.tsv`도 남긴다. retired ID는 메타데이터가 없을 수 있다.

새 TSV 열:

| 열 | 의미 |
|---|---|
| `gpu_us` | 기존 draw 전체 구간. Grab·바인딩·실제 draw 포함 |
| `prepare_gpu_us` | 해당 segment의 GPU pretransform/pose·draw 업로드/index 준비 구간 |
| `prepare_constants_us` | 해당 segment의 CB 준비·업로드 구간 |
| `grab_us` | 해당 Grab draw의 복사 준비·복사 구간. `gpu_us`의 부분집합 |

prepare 두 열은 같은 segment의 각 행에 반복되므로 **frame/segment당 한 번만 합산**한다. Grab도 draw 전체에 다시 더하지 않는다. `summarize-m4k-drawgpu.py`가 이 규칙과 시간 범위를 검사한다.

```powershell
py -3.12 tools/direct-renderer/summarize-m4k-drawgpu.py <덤프.tsv> --output <요약.json>
```

실제 production WARP 경로에서 315개 draw·10개 segment·빈 첫 segment를 사용했다. 프레임 사이 GPU readback 없이 first=0/256에서 각각 296/236행, invalid=0을 회수했다. first=512는 0행·sampled segment=0·skipped=0이었다. 이는 합성 315개 조건이며 실제 DD의 ordinal 수라고 대체하지 않는다.

fixture는 마지막 readback으로 GPU를 완료시킨 뒤 빈 segment에서 회수한다. production은 Flush·spin·blocking GetData를 하지 않는다. [DONOTFLUSH](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/ne-d3d11-d3d11_async_getdata_flag)는 미제출 명령을 강제로 실행하지 않으므로 pending/skipped를 숨기지 않는다. timestamps는 GPU 경과 시간이며 GPU busy나 VidMm migration 횟수가 아니다.

## 5. 한 요소씩 비교할 계획과 남은 판정

`out/direct-renderer/m4k/plans`: 일반 **24개**, 진단 **32개**. 모두 생성만 했고 실행하지 않았다.

- HELLO 50s / Exit(`sh`) 88s / DD 50s / WF 258s: plan·pixel·A1/B1/B2/A2. 3440×1440, safe=0, 원래 pixel/noise gate 유지, JALib 비활성 전제.
- DD drawgpu 창 0/128/256, count=128, interval=4. dump 뒤 `assert`로 표본 0을 성공 처리하지 않는다. 범위 확대는 `max_visible`에 근거한다.
- DD ImageMaxSide 1024 pixel+ABBA 5개. 매번 맵을 열기 전에 적용하고 실제 texture 크기·DXGI usage/budget을 확인해야 한다.
- DD full/1024 각각 Grab-off, ring-off, pretransform-off, rename 업로드, stale binding 대조의 동일 정지 장면 A/B/B/A. 매 arm 전에 자원 설정과 ring을 초기화한다.
- Grab-off는 그림을 바꾸는 원인 분리용이다. FPS 승리나 픽셀 합격으로 채택하지 않는다.
- HELLO/Exit/DD/WF bisection, local precision, Unity Grab route 및 particle 대조를 보존했다. 일반 계획은 resources/default와 drawgpu/off를 명시한다.

진단 스위치는 off에서만 설정한다:

```text
drfull resources default
drfull resources grab off
drfull constants legacy
drfull resources pretransform off
drfull resources stable off
drfull resources trim off
```

한 번에 하나만 바꾼다. 초기화는 `drfull resources default`, `drfull constants ring`, `drfull drawgpu off`다.

남은 원인 판정 순서:

1. 기존에 찾은 Grab draw에서 `grab_us`와 `gpu_us - grab_us`를 분리한다. 복사가 크면 Grab destination의 읽기/다시 쓰기·RT 전환과 residency를, 나머지가 크면 sprite texture/SRV/sampler 읽기를 우선 조사한다.
2. full/1024에서 같은 자원 대조를 비교한다. 고정 업로드·trim·retire의 카운터도 확인한다. 드라이버 페이지 이동 확정에는 VidMm/ETW 또는 PIX가 필요하다.
3. **원본 Unity의 실제 Grab 횟수·draw/cull 개수는 아직 측정하지 않았다.** native `grab_copies`/`culled_sources`로 Unity 횟수를 대신하지 않는다. 같은 장면의 원본 프레임 캡처와 비교해야 한다.
4. Exit/DD(1024)는 진단을 모두 끄고 pixel/noise gate 후 ABBA로 목표를 판정한다.
5. HELLO 잔여 0.65%, WF 대각선 가장자리, Exit ±1~2의 실제 장식별 원인은 미확정이다. 기존 rounding fixture의 중간 UNORM8 양자화 결과를 모든 실제 픽셀의 원인으로 확대하지 않았다. 현재 공용 `framecmp/d0.png` 등은 다른 로드/오류 화면이 남아 있어 이번 잔여 픽셀의 근거로 사용하지 않았다.

## 6. 최종 검증

최종 일괄 명령:

```powershell
& tools/direct-renderer/check-m4k.ps1 *> out/direct-renderer/m4k/check-final.log
```

| 검사 | 결과 |
|---|---|
| native x64 | 성공, 오류 0 |
| 개발자 Release | 오류 0, 기존 경고 86 |
| Player | 오류 0, 기존 경고 63 |
| PlayerAuto | 오류 0, 기존 경고 65 |
| 기존 export 이름·ordinal·hint | **74/74 동일**, `sf_z_drfull_v1` 포함 |
| native 내장 리소스 | managed 3종 모두 현재 native DLL 포함 |
| 진단/catalog 경계 | DEV/PlayerAuto 포함, 일반 Player 제외 |
| 원본 resource loader / ABI | 21 shaders·1,012 variants·2,024 stages 검증, 새 payload 크기 확인 |
| 기존 production WARP | 48 cases, maxChannel=1·differingPixels=28로 기존 허용 범위 유지 |
| immediate ring / deferred / immediate legacy | default·Grab·named Grab 각 8 cases, 픽셀 차이 0 |
| batch / 얇은 선·변환 / state 복원 | 기존 fixture 통과, steady C++ heap 할당 0, 실패 경로 복원 유지 |
| M4J 상수 링 / 상태 enum | 통과, 6MiB 정지 업로드 0·희소/순환/다중 segment 보존 |
| M4K 신규 fixture | 범위·빈 segment·지연 회수·컬링 CB·slot127·retire·업로드 대조 통과 |
| 분석기 | prep 중복 합산/Grab 중복 합산 방지, 잘못된 입력 6종 거부 |
| lifecycle·layer/order·pixel metrics | 기존 오프라인 검증 통과 |
| 설치 DLL SHA-256 | **2/2 동일** |

핵심 산출물: `out/direct-renderer/m4k/check-final.log`, `m4k.log`, `m4k-audit.json`, `final-hashes.json`, `validation/exports-m4d-result.txt`, `validation/installed-result.txt`.

현재 로컬 `native/sfnative.dll` SHA-256은 `ECE9A371880812C01F4B77FDBF84C38D85A3DB114968E0F6016FF86F3C90F566`이다. 이 DLL을 설치 폴더에 복사하지 않았다.
