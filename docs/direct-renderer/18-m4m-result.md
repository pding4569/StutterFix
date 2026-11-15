# M4m 결과 — 재생 중 상수·계획 캐시 유지

2026-11-16, `E:/StutterFixWork/wt-m4`, 브랜치 `claude/drfull`.
시작 HEAD: `e4ccb3aed261b7878bf9c95309c5140838588ff2`. 커밋하지 않았다.

구현과 최종 오프라인 검증을 완료했다. **4종 빌드 오류 0, 기존 export 74/74 보존,
캐시·픽셀·정렬·수명 fixture 통과**다. 세션 전체 설치 해시 비교는 managed DLL의
관측된 변경으로 별도 HOLD이며, 이를 빌드·fixture 성공으로 덮지 않았다.
게임 실행·설치 DLL 교체·복구를 수행하지 않았다.
**실게임 render ≤100µs, main ≤150µs 및 DD/Exit/HELLO 품질·GPU 목표는 미측정이다.**

## 1. 실제 값이 같으면 다시 계획하지 않는다

M4l은 `listChanged` 통지를 받으면 실제 목록이 같아도 캐시 계획을 무효화했다.
재질 키가 변하면 배치 구간 전체를 재계산했고, `cacheBuildPlan()`은 활성 draw를
모두 상수 재평가 대상으로 넣었다. 그 결과 일시정지 fixture의 성공이 재생 중
캐시 유지로 이어지지 않았다.

수정 후에는 다음 조건을 구분한다.

- 네이티브 draw ID 순서와 segment 경계가 실제로 달라질 때 계획을 갱신한다.
  동일한 목록 재전송은 `cache_same_lists`에만 집계한다.
- 재질 변경은 모든 변경을 적용한 후, 해당 draw 양옆의 실제 배치 호환성을
  검사한다. 배치 경계가 같으면 `buildBatchRuns()`와 계획 재작성을 모두 생략한다.
  실제 분할·병합은 다시 계획한다. 단순 색·시간 변경을 순서 변경으로 취급하지 않는다.
- 계획을 다시 만드는 경우에도 기존 상수를 일괄 dirty로 만들지 않는다.
  바뀐 연산·바인딩을 포함한 chunk만 다시 기록한다.
- 메인은 거리 변경을 Unity의 layer/material 속성 재조회 없이 갱신한다.
  모든 거리 키를 함께 바꾼 뒤 변경된 entry의 앞뒤만 검사한다.
  상대 순서가 유지되면 트리 제거·재삽입과 전체 identity 순회도 생략한다.
  교차가 있으면 이전 키를 먼저 복구한 뒤 기존 정렬 경로로 간다.
  그룹·마스크·새 패스·Canvas·명시적 표시 변경은 이 빠른 경로에서 제외한다.

`cache_order_plans`, `cache_batch_plans`, `cache_same_runs`,
`distance_order_skips`로 통지와 실제 재계획을 구분할 수 있다.

## 2. 프레임 상수를 draw 수만큼 평가하지 않는다

캐시 상수 저장소를 바인딩 자체와 분리했다. 카메라·시간에만 의존하는 cbuffer는
**정확한 재질 바이트와 상대 patch 배치가 같은 경우 stage·slot·shader가 달라도
하나의 slice를 공유한다.** 원본 DXBC를 변경하지 않으므로 `$Globals` 안에
시간과 재질이 섞여 있어도 같은 내용의 버퍼는 한 번만 평가·업로드한다.
카메라·시간 알림은 draw 목록이 아니라 고유 저장소 목록을 순회한다.

프레임 page와 draw page를 분리하고 동적 page를 4KiB 단위로 구성했다.
재생 중 시간 변화가 draw의 정적 page까지 64KiB씩 다시 올리는 일을 막았다.
업로드는 명령 목록 밖에서 DEFAULT buffer의 순서 보장된 갱신으로 수행한다.
이전 GPU 프레임이 참조하는 slice를 NO_OVERWRITE로 덮어쓰지 않는다.

재질 변화에는 두 경우가 있다.

- 공유 사용자 전체가 같은 값으로 바뀌면 같은 slice를 갱신한다. 목록을 다시 기록하지 않는다.
- 일부 사용자만 달라지면 그 바인딩을 한 번 분리하고 해당 chunk만 다시 기록한다.
  이후 애니메이션은 분리한 slice를 계속 갱신한다. VS와 PS의 내용이 따로 변하는
  경우도 구별한다. draw ID만으로 분리 버퍼를 공유하지 않는다.

검증된 GPU 포즈 경로의 object 행렬은 identity로 고정하고 실제 포즈·live·색은
기존 GPU stream에서 처리한다. 미검증 셰이더·Grab·마스크의 원본 object 경로는
유지한다. 원본 cbuffer가 미검증 object 계산에 의존하면 해당 draw의 갱신은 필요하다.

`drfull state`에 다음을 추가했다.

- `cache_frame_evaluations`, `cache_draw_evaluations`, `cache_frame_bytes`, `cache_draw_bytes`.
- `unity_ObjectToWorld`, `unity_WorldToObject`, VP/V/Projection/MVP,
  camera/screen/projection/ortho, `_Time`, `_SinTime`, `_CosTime`, `unity_DeltaTime`,
  `_RendererColor`, `_Flip`의 **이름별 평가 수·실제 바이트 변경 수**.
- 다른 재질 필드는 `cache_var_material_*`로 묶어 집계한다.
  이 값은 원본 DXBC의 모든 재질 변수 이름별 계수는 아니다.

### 재생형 fixture

| 입력·변화 | 결과 |
|---|---|
| 128 heads, VS/PS의 동일한 혼합 Globals, 카메라·시간·포즈 변경, 목록 재전송 | 첫 프레임 뒤 7프레임 평가 총 7회, 업로드 총 28KiB, 계획·기록 증가 0 |
| 일부러 잘못된 source bounds / 카메라만 변경 | 실제 정점으로 클리핑해 픽셀 보존, 카메라 변화로 world 정점 재bake 0 |
| 일부 재질만 분리 후 계속 변경 | 분리 저장소 1개, 해당 chunk 기록 1회, 다른 사용자 픽셀 보존 |
| 실제 배치 분할 → 동일 값 유지 → 병합 → 유지 | 캐시 계획·기록 각각 정확히 2회 증가 |
| 같은 draw의 VS/PS 재질이 독립적으로 변화 | 별도 slice 2개, 픽셀 일치, 이후 내용 갱신만 수행 |
| **7,000 heads·21,000 bindings, 매 프레임 7,000 포즈·카메라·시간 변경** | 안정화 후 16프레임, **프레임당 평가 1회·업로드 4,096 bytes**, draw 평가 추가 0, 계획·기록 추가 0, C++ heap 호출 0 |
| 7,000 heads의 재질을 함께 변경 | 공유 slice 유지, 분리 0, 계획·기록 증가 0 |

이는 합성 입력의 불변 조건 검증이다. 명세의 실제 Exit 13,700회·약 5MB와
동일한 게임 장면을 재측정한 감소율이나 CPU 시간으로 환산하지 않는다.

## 3. 검증된 GPU 포즈 draw의 간접 호출을 없앴다

검증된 world-vertex 경로는 캐시 목록 안에서 `DrawIndexed`를 직접 재사용한다.
live 변경은 기존 GPU bake의 정점 퇴화를 사용하고, 화면 밖 판정은 실제 정점을
원본 VS로 투영한 뒤 GPU의 정상 클리핑에 맡긴다. 부정확한 Renderer bounds로
보이는 source를 퇴화시키지 않는다. 카메라만 바뀌면 공유 VP 상수만 갱신한다.
world 정점의 재bake·bounds stream·별도 frustum compute를 추가하지 않는다.
화면 재진입은 현재 VP로 즉시 반영하고 기존 호환 draw는 같은 배치로 계속 묶는다.

이 경로는 source별 간접 인자 0 생성과 `DrawIndexedInstancedIndirect` 호출을
추가하지 않는다. 별도 간접 컬링 dispatch의 작업 목록에는 미검증 single만 넣는다.
배치·Canvas 경계와 그리기 순서는 유지한다. 기본 chunk를 32 → 128로 늘리고
동일 shader·상수 바인딩 및 chunk 내 RT/viewport 설정의 중복 기록도 줄였다.

원래 bake shader와 32-byte `GpuDraw`를 유지했다. 색 하나 변경 시 64 bytes를
올리는 중간 구현은 제거했다. 기존 희소 업로드·자원 대조도 검사한다.

동일 캐시·상수·Grab에서 제출·컬링 방식의 비용을 비교하는 `drfull cache indirect`를 추가했다.
이 모드는 실행 전에만 설정할 수 있다. 기본 `drfull cache on`은 직접 제출을 사용한다.
간접 대조에는 인자 생성과 bounds 컬링도 포함되므로 두 모드의 전체 GPU 시간 차이를
간접 API 한 호출의 비용으로 단정하지 않는다.

128 heads × 20프레임에서 직접 모드는 direct=2,560 / indirect=0,
대조 모드는 direct=0 / indirect=2,560이고 legacy와 모든 픽셀이 일치했다.
7,000 heads 스트레스에서는 indirect=0, 별도 cull items=0이다.
**화면 밖 직접 draw도 정점 처리 비용은 남는다.** 이 수치를 GPU 비용 0이나
실제 Exit의 4.19ms → 1.49ms 회복으로 해석하지 않는다. 동일 조건의 GPU 시간은
생성한 direct/indirect 대조 계획으로 측정해야 한다.

## 4. HELLO와 Grab

명세에 작업 중 추가된 DD 결과도 반영했다. M4l의 원래 크기 DD B1/B2
179.8/180.1 FPS 및 native GPU 약 4.38ms는 기존 측정 근거다.
이를 회복시킨 **명령 목록 밖 immediate Grab 복사** 경로를 유지했다.
목록 안의 predicate·작은 draw·쿼리로 되돌리지 않았다.

다만 기존 HELLO 로그는 PSNR 40.06209이고, off/off도 187픽셀·최대 RGBA
(3,2,4,0)으로 실패했다. 이 실행은 픽셀 합격 근거가 될 수 없다.
큰 차이를 작은 노이즈만으로 설명하거나 해결됐다고 처리하지 않았다.

M4m의 managed production은 `ScreenGrab` 부분 복사 승인을 비활성화했다.
검증한 sampler·CPU 정점 사각형만으로 실제 HELLO의 읽기 범위를 보장하지 않고,
**전체 복사**를 사용한다. named Grab의 첫 visible 사용·프레임 스냅샷 재사용,
실제 복사 카운터와 자원 우선순위는 유지했다.
부분 복사·불확실 bounds fallback·비중첩 공유 구현은 기존 합성 회귀 fixture에서만 검증한다.
전체 복사 전환은 DD 복사 바이트를 늘릴 수 있으므로 DD 성능 보존도 실기 재검증 대상이다.

계획은 `out/direct-renderer/m4m/plans`에 **정규 29개 + 진단 24개**를 생성했다.
모두 `m4m-` 접두사이며 실행하지 않았다.

- HELLO/Exit/DD/WF의 계획·픽셀·ABBA 24개, DD1024 독립 조건 5개.
- cache-off 12개, 같은 캐시의 indirect 대조 12개. 정규 ABBA에서 제외한다.
- DD 비트 동일, Exit 최대 채널 차이 2, HELLO PSNR 42.8 gate를 유지한다.
- off/off `d0/d00`에 더해 on/on `d1/d11` 잡음 대조도 강제한다.
- 재생 구간의 frame/draw 평가·업로드·계획·기록 증가량을 프레임 수로 나눈다.
  VRAM 초과 DD와 DD1024를 섞지 않는다.

## 5. 검증·ABI

재현 명령:

```powershell
& tools/direct-renderer/check-m4m.ps1 *> out/direct-renderer/m4m/check-final-clipper.log
```

이 검사는 로컬 빌드, 창 없는 WARP, metadata/ABI·정렬·상태·수명·계획 검사만 수행한다.
native opcode 34/35에 400-byte `CacheDetails`, 4-byte `CacheTune`을 추가했다.
기존 `CachePerformance` 192 bytes와 기존 payload·export는 확대하지 않았다.
검사 전후 source hash도 비교해 실행 중 소스가 바뀐 근거를 받아들이지 않는다.

| 최종 검사 | 결과 |
|---|---|
| native x64 | 성공, 오류 0. 기존 ISPC performance warning 유지 |
| 개발자 Release / Player / PlayerAuto | 오류 0, 기존 경고 86 / 63 / 65 |
| export 이름·ordinal·hint | 기존 74/74 동일, `sf_z_drfull_v1` 포함 |
| managed 3종 내장 native | 최종 native와 3/3 동일 |
| 리소스·ABI | 21 shaders / 1,012 variants / 2,024 stages, 기존 payload 및 신규 400/4-byte 계약 통과 |
| 원본 production WARP | 48 cases, maxChannel=1, differingPixels=28: 기존 범위 유지 |
| 캐시 + 원본 DXBC | 48 + Grab 8 + named Grab 8, maxChannel=1 / 0 / 0 |
| immediate / deferred / immediate legacy | default·Grab·named Grab 픽셀 차이 0 |
| 재생형 대조 | legacy 참조를 포함한 85프레임, 직접·간접·재질 분리·VS/PS 독립 변경·실제 배치 경계 대조 통과 |
| 7,000 heads 스트레스 | 모든 포즈·카메라·시간 변화에 평가 1회·4KiB/프레임, 계획·기록·draw 평가 증가 0, C++ heap 0 |
| 거리 정렬 | 7,000 sources, 공통 이동 제자리 유지, 교차 시 rollback·재삽입, 10,000 neighbour 검사 할당 0 |
| M4J/M4K 회귀 | 정지 상수 업로드 0, 희소 변경·링 순환·다중 segment·컬링·probe·retire·자원 대조 통과 |
| compositor / stencil | 24 조합·9 stencil 사례, RGBA8 최대 2 유지 |
| 상태·수명·quality gate | 기존 거부·복원·회복·stale event·Canvas-only 및 quality 경계 9개 통과 |
| 컴파일 입력 소스 | 검사 시작·종료·audit 시 hash 동일 |
| 검사 구간 설치 DLL | 2/2 동일 |
| 세션 전체 설치 DLL | 1/2 동일, managed 변경으로 **HOLD** |

최종 증거는 `out/direct-renderer/m4m/check-final-clipper.log`, `m4m.log`,
`m4l.log`, `m4l-lifecycle.log`, `m4m-audit.json`, `source-before.json`,
`source-after.json`, `final-hashes.json`이다. `validation-latest.json`은
`validation-20261116-044241-485`를 가리킨다.

최종 로컬 native DLL SHA-256:
`3D48BAE1AFE12157E135B6965833E3A783CF4B0432C6B8CA9470B4D9177D01DF`.
설치 native와는 다른 로컬 빌드이며 설치하지 않았다.

audit는 빌드·fixture 결과를 JSON에 저장한 뒤 세션 설치 변경 때문에 **exit 2**를
반환한다. 상위 PowerShell checker도 이 HOLD를 비정상 종료로 전달한다.
최종 로그의 비정상 종료는 컴파일·픽셀 fixture 실패가 아니라 아래 설치 세션 비교다.
시작 스냅샷을 덮거나 HOLD를 무시하는 옵션은 추가하지 않았다.

## 6. 설치 상태와 금지 작업

이번 작업의 명령은 커밋·게임 실행·설치 DLL 쓰기·복구를 하지 않았다.
`18-m4m-spec.md`에 추가된 외부 변경도 수정하거나 되돌리지 않았다.

설치 상태는 두 범위로 구분해야 한다.

- 세션 시작(04:03:27)과 첫 빌드 검사 시작(04:14:13)의 managed hash는
  `E1951774F652186BCCE6BC05263774F7738DFCACE7D438267AED9D6AB074AD7B`였다.
- 설치 managed 파일의 수정 시각은 04:22:04이며 이후 hash는
  `B0EFE68D6CCCE3217310DA0AD65CBA4048C19D2D25F738C97F14D7822898C795`다.
  이 작업의 빌드 스크립트·프로젝트에는 설치 쓰기 동작이 없다.
- 설치 native는 세션 시작부터
  `2B56F929C0EF5F9356DFE8F15E84813BA43E87FC0EA13FCAD9F17EE3EBE40DC6`로 동일하다.
- 개별 최종 빌드 검사 전후는 2/2 동일하지만, **세션 전체 비교는 managed 1개
  불일치로 HOLD**다. 시작 스냅샷을 새 값으로 덮어 합격 처리하지 않는다.
  설치 파일을 원복하거나 교체하지 않았다.

세션 비교는 `out/direct-renderer/m4m/installed-session-result.json`,
개별 빌드 결과는 `validation-latest.json`이 가리키는 폴더에 남긴다.
게임 성능·픽셀 목표와 설치 세션 HOLD는 오프라인 구현·빌드 통과와 별도로 판정한다.
