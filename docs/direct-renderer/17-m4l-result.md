# M4l 결과 — 명령 목록 캐시·GPU 포즈·Grab 수정

2026-11-16, `E:/StutterFixWork/wt-m4`, 브랜치 `claude/drfull`.
기준 HEAD: `b80faede9e931b353b8d045fb89c17448eb7fb4c`. 커밋하지 않았다.

**중단된 구현을 이어서 완성하고 오프라인 검증을 통과했다. 실게임 목표 달성은 미판정이다.**
게임 실행·설치 DLL 교체를 하지 않았으므로 렌더 스레드 0.1ms, 메인 0.15ms,
DD VRAM 초과 붕괴 해소, 실제 DD/Exit/HELLO 픽셀·GPU 시간은 이번 결과로 확정할 수 없다.
이 문서의 WARP 결과는 합성 장면과 원본 DXBC에 대한 근거다.

| 요구 | 현재 판정 |
|---|---|
| native / 개발자 / Player / PlayerAuto 오류 0 | 통과 |
| 기존 export 보존 | 이름·ordinal·hint **74/74 동일** |
| fixture / 상태 복원 / 자원 수명 | 통과 |
| 우리 렌더 스레드 ≤100µs, 메인 ≤150µs | 실게임 미측정 |
| DD 비트 동일, Exit 최대 채널 차이 ≤2 | 측정 계획에 강제 gate. 실제 맵 미측정 |
| HELLO PSNR ≥42.8, Exit/HELLO GPU 회귀 없음 | 측정 계획에 반영. 실제 맵 미측정 |
| DD 원래 크기·VRAM 초과 상태 붕괴 해소 | 복사·제출 구조 수정 완료. 실기 해소 여부 미측정 |
| 커밋 / 게임 실행 / 설치 교체 금지 | 준수. 설치 DLL SHA-256 2/2 보존 |

## 1. 중단 당시 상태와 수정한 Grab 경로

시작 시 `git diff`와 미추적 `native/sfnative/drfull-cache.h`, M4l fixture·계획을 확인했다.
기존 작업과 Claude가 넣은 `17-m4l-plan.md`의 추가·추가 2를 유지했다.
재개 전 변경분은 `out/direct-renderer/m4l/resume/before.patch`, 캐시 헤더는
`resume/drfull-cache-before.h`에 보관했다.

중단된 캐시는 Grab마다 1×1 타깃에 간접 draw를 하고 occlusion predicate로
`CopyResource`를 조건부 실행했다. 복사 호출의 `grab_copies`·`grab_bytes` 집계도 없었다.
따라서 예전 `grab_copies=0`만으로 실제 복사가 모두 생략됐다고 단정할 수는 없다.
그러나 계획에 기록된 DD 최대 차이 8은 그 빌드를 픽셀 합격으로 쓸 수 없다는 근거다.

최종 경로에서는 다음과 같이 바꿨다.

- Grab predicate·작은 draw·전용 RT·named-Grab 선택용 두 번째 compute dispatch를 제거했다.
- Grab을 명시적 경계로 두고, 그 draw 직전 immediate context에서 스냅샷을 복사한 뒤 그리기 목록을 실행한다.
- 이름 없는 Grab은 매 필요 시점에 복사한다. named Grab은 프레임 전체의 첫 visible 사용에서 복사하고 다른 segment에서도 그 스냅샷을 재사용한다.
- 실제 복사 호출에서 `grab_copies`·`grab_bytes`를 집계한다. `skipGrab`은 정규 경로에서 사용하지 않는다.
- `ExecuteCommandList(..., FALSE)`로 각 목록 뒤의 내부 바인딩을 기본 상태로 돌린다. Unity 전체 상태는 segment 바깥의 `SwapDeviceContextState` scope가 원래대로 복원한다.
- 캐시 목록 기록 시 이전 draw가 사용한 SRV slot만 정리한다. 기존 immediate Grab 경로도 실제 바인딩된 slot만 해제하며 slot 127을 보존 검사한다.

이는 GPU 증가를 만들 수 있는 쿼리·작은 draw·목록별 상태 복원 작업을 제거한 것이다.
Claude의 Exit 1.8→8.2ms / HELLO 5.7→17.3ms 증가를 쿼리 하나의 원인으로 확정하거나,
새 빌드에서 그 증가가 해소됐다고 주장하지 않는다. 같은 수정 Grab을 쓰는 `cache off`
진단 계획으로 캐시 자체의 GPU 비용을 분리할 수 있게 했다.

## 2. 사각형 복사에 검증과 전체 복사 fallback을 넣었다

Claude의 월드 bounds 8개 꼭짓점 투영, ±4px 여유, 상하 방향 합집합 방식을 유지하면서
**실제 indexed vertex의 화면 범위와 비교한 뒤** 부분 복사를 허용한다.
검증에는 해당 geometry의 정점, pose, Sprite `_Flip`, 현재 VP를 사용한다.
Renderer bounds보다 정점 범위가 크거나 투영을 확신할 수 없으면 전체 복사한다.

부분 복사의 적용 조건은 좁혔다.

- 원본 `Hidden/BlendModes/SpritesDefault/Grab`의 확인한 screen XY/W 샘플링 경로.
- 읽을 수 있는 canonical CPU geometry와 실제 index 집합.
- 전체 타깃을 쓰는 viewport, 단일 mip 타깃. 여러 mip가 있으면 전체 복사.
- `PIXELSNAP_ON`, 다른 셰이더·변형, geometry 미확인, named Grab은 전체 복사.

`audit-m4l-shaders.py`는 해당 원본 DXBC의 비 PixelSnap 변형 **100개**가 같은
VS를 사용함을 확인하고, PS의 Grab 샘플 좌표를 레지스터 성분별로 추적해
screen X/W·Y/W인지 검사했다. 100/100 통과했다.

production의 정점 범위 검사는 **CPU에서 실제 입력 정점을 투영하는 보수적 검사**다.
GPU 정점 버퍼를 읽어오는 검사가 아니며 GPU readback·Flush·대기를 추가하지 않는다.
합성 fixture는 원본 DXBC의 부분 복사 결과도 검사하지만, 실제 HELLO 장식의 GPU
출력 범위를 캡처한 근거는 아니다. HELLO는 별도 PSNR gate를 통과해야 한다.

Grab destination에는 `SetEvictionPriority(DXGI_RESOURCE_PRIORITY_MAXIMUM)`을 적용했다.
이 우선순위가 실제 상주나 VRAM 예산 초과 해소를 보장하지는 않는다.

추가로 연속한 unnamed Grab의 검증된 사각형이 서로 겹치지 않을 때만 한 번의
합집합 복사를 공유한다. 앞선 draw의 쓰기 범위가 뒤 draw의 읽기 범위에 닿으면
합치지 않는다. 일반 draw·named Grab·segment·불확실한 범위를 넘지 않고,
합집합 면적이 개별 면적 합의 125%를 넘으면 분리한다. 그리기 순서는 유지한다.

상태에 `grab_rect_tests`, `grab_rect_mismatches`, `grab_rect_fallbacks`,
`grab_rect_copies`, `grab_groups`, `grab_merged`를 추가했다.
`grab_rect_copies`는 **검증에 합격한 사각형 수**이고, 실제 복사 횟수는
기존 `grab_copies`다. 그룹 후보 검사 때문에 두 값이 일치할 필요는 없다.

### 합성 Grab 검증

매 프레임 배경색을 바꿔 오래된 스냅샷으로도 통과하는 경우를 방지했다.

| 대조 | 결과 |
|---|---|
| 8프레임 전체 복사 | 8회, 196,608 bytes |
| 검증된 부분 복사 | 8회, **120,640 bytes**, 전체 복사와 픽셀 동일 |
| 의도적으로 작은 bounds | 8/8 불일치 감지, 8/8 전체 복사 fallback, 픽셀 동일 |
| 외부 geometry / 알 수 없는 샘플링 계약 / 다중 mip | 전체 복사 fallback, 픽셀 동일 |
| 연속 Grab, 비중첩·중첩을 번갈아 적용 | 전체 8회 → **6회**, 공유 그룹 2개, 중첩 시 분리, 픽셀 동일 |
| named/unnamed, 첫 draw 화면 밖, 여러 segment | 첫 visible 스냅샷·정확한 복사 횟수·픽셀 동일 |

이 바이트·횟수 감소를 실제 DD의 감소율로 환산하지 않는다. Unity의 Grab 임시 RT
풀과 자원을 공유하거나 Unity 원본의 D3D 호출을 세는 진단은 추가하지 않았다.
원본 게임 실행이 필요한 비교는 이번 금지 범위에 들어간다.

## 3. 명령 목록 캐시와 GPU 포즈

`drfull-cache.h`의 구간 계획은 기본 32개 제출 단위의 chunk로 나뉜다.
같은 순서·상태·바인딩·GPU 자원·타깃·viewport에서는 기존 명령 목록을 실행한다.
순서가 바뀌면 chunk의 실제 연산을 비교해 바뀐 chunk만 다시 기록한다.
Grab·Canvas는 독립 경계다. RT·viewport·참조 버퍼가 바뀌면 관련 목록을 갱신한다.

재질 상수는 64KiB DEFAULT CB page의 고정 slice와 D3D11.1 offset binding을 사용한다.
내용은 목록 밖에서 먼저 갱신하므로 색 변화만으로 목록을 다시 기록하지 않는다.
동적 page는 변경된 page 전체를 순서대로 갱신한다. 이전 프레임이 참조하는 slice에
NO_OVERWRITE로 덮어쓰지 않는다. 구형 deferred offset emulation용 null-bind 우회도 유지했다.

검증된 Tile/FloorMesh/Sprites 경로는 한 개짜리 run도 GPU world-vertex 경로를 쓴다.
포즈·live 변경은 고정 슬롯 버퍼와 dirty vertex block을 갱신하고, normalized CB는
object identity를 유지한다. 일반 목록의 visibility·간접 인자는 GPU에서 갱신한다.
**모든 원본 셰이더를 강제로 GPU 포즈로 바꾼 것은 아니다.** Grab·마스크·미확인
프로그램은 기존 object CB 경로를 보존하며, Grab 복사 여부는 소수의 CPU 경계에서 판단한다.

메인은 TransformAccess job 결과를 NativeQueue로 받아 변경된 슬롯만 처리한다.
정렬 속성만 바뀐 통지는 전체 material Fill 대신 key 갱신으로 처리한다.
named Grab 소유권 검사는 바인딩·수명·live 변경 때 무효화한다.
job 자체는 Transform 집합을 읽는다. 실제 Unity의 job Complete 비용·GC 0·150µs 달성은 미측정이다.

캐시를 쓸 수 없는 장치·제출/상수/자원 설정·drawgpu 진단은 원래 제출 경로로 간다.
`drfull assert cache`는 캐시 사용 가능 여부, 실제 replay, fallback 0, Grab 비활성화·누락을 검사한다.
차가운 첫 프레임만으로 캐시 성공을 선언하지 않는다.

주요 fixture 결과:

- 정지·포즈·material·live·화면 밖/재진입: 픽셀 동일. 포즈·색·live 변화에 목록 재기록 없음.
- 96개 draw·빈 첫 segment·국소 순서 변경: 해당 chunk **1개만** 추가 기록. 해당 시점 records=7, replays=138.
- 안정화 후 producer+renderer 32프레임의 C++ heap allocation 호출 **0**.
- 검증된 GPU 포즈 8사례: 목록 1개 유지, 포즈/live 변경에 normalized CB 추가 업로드 0.
- 교체 전 옛 목록 유지 → 교체 목록 소비 후 retire 1. 타깃 변경 시 재기록.
- 잘못된 타깃에서 상태 복원, 다음 정상 이벤트 회복, destroy 후 stale event 무시, legacy fallback 통과.
- Canvas만 있는 pose 0개 장면: GPU 업로드·컬링 0, 목록 1개를 2회 replay, 픽셀·상태 복원 통과.

ABI는 기존 export 안의 opcode 32/33(`CacheConfigure`/`CachePerf`)를 덧붙였다.
payload는 각각 8/192 bytes이며 managed/native 계약 검사가 통과했다. 기존 payload 크기는 유지했다.

## 4. 측정 계획과 픽셀 gate

실제 측정용 파일은 `out/direct-renderer/m4l/plans`에 생성만 했다.
**정규 29개 + cache-off 진단 12개**, 모두 `m4l-` 접두사다.

- HELLO 50s / Exit(`sh`) 88s / DD 50s / WF 258s: plan·pixel·A1/B1/B2/A2 24개.
- DD ImageMaxSide 1024: pixel + ABBA 5개. 원래 크기와 독립 조건이다.
- 4개 맵의 cache-off pixel/B1/B2 12개: 수정 Grab을 유지한 채 캐시 비용을 분리한다. 정규 ABBA에서 제외한다.

3440×1440, safe=0, JALib 비활성, resources/default, drawgpu/off, Grab 활성,
캐시 replay 확인, 노이즈 0, 실제 texture 크기와 DXGI usage/budget 확인을 전제로 한다.
VRAM 초과 DD와 1024의 예산 내 DD를 섞어서 승리를 선언하지 않는다.

gate 구현도 맞췄다.

- DD: `framecmp assertpixels d0 d1`. 교체 때문에 달라지는 renderer 상태와 픽셀 비교를 분리한다.
  기존 `assertzero d0 d1`은 `forceRenderingOff` 상태 차이까지 실패시킬 수 있었다.
- Exit: `framecmp assertquality d0 d1 2 0 png`. 모든 채널 최대 2를 검사하며 changed 비율로 다른 제한을 추가하지 않는다.
- HELLO: `framecmp assertquality d0 d1 255 42.8 png`. PSNR 42.8 미만을 거부한다.
- off/off 노이즈는 기존 `assertzero d0 d00`으로 픽셀과 상태를 엄격히 검사한다.
- 새 quality gate의 경계·NaN·빈 증거·alpha 회귀 등 오프라인 검사 9개가 통과했다.

실제 측정에서는 픽셀 gate 후 cache-on/off native GPU를 먼저 비교하고,
정상 VRAM의 main/render 목표와 DD 초과 상태를 각각 판정해야 한다.
shader sampling·복사량 감소만으로 FPS나 VidMm 상주 처리의 원인을 확정하지 않는다.

## 5. 최종 오프라인 검사와 재현

```powershell
& tools/direct-renderer/check-m4l.ps1 *> out/direct-renderer/m4l/check-final.log
```

이 명령은 로컬 빌드, 창 없는 WARP, metadata/ABI·자원·계획 검사만 수행한다.
게임 실행·설치·커밋 경로는 없다. 실행마다 새 validation 디렉터리를 만들고
`validation-latest.json`으로 가리켜, 중단 전 설치 스냅샷을 현재 실행의 기준으로 오인하지 않게 했다.

| 검사 | 결과 |
|---|---|
| native x64 | 성공, 오류 0. 기존 ISPC performance warning 존재 |
| 개발자 Release / Player / PlayerAuto | 오류 0. 기존 경고 86 / 63 / 65 |
| export | 이름·ordinal·hint 74/74 동일, `sf_z_drfull_v1` 포함 |
| managed 3종 내장 native | 현재 DLL 3/3 동일 |
| 원본 리소스·계약 | 21 shaders, 1,012 variants, 2,024 stages 및 새 cache payload 통과 |
| 원본 production WARP | 48 cases, maxChannel=1, differingPixels=28: 기존 범위 유지 |
| 캐시 + 원본 DXBC | 48 + Grab 8 + named Grab 8: maxChannel=1/0/0, 기존 결과 유지 |
| immediate / deferred / immediate legacy | default·Grab·named Grab 각각 픽셀 차이 0 |
| M4J / M4K 회귀 | 상수 링 정지 업로드 0, 희소 변경·순환·다중 segment·probe·컬링·retire·자원 대조 통과 |
| 합성 compositor / stencil | 기존 24개 조합·9개 stencil 사례 통과, RGBA8 최대 2 |
| lifecycle / 순서·레이어 / 비교 metrics | 기존 검사와 신규 cache 수명·quality 검사 통과 |
| 설치 DLL | 세션 시작 및 최종 SHA-256 2/2 동일 |

핵심 근거는 `out/direct-renderer/m4l/check-final.log`, `m4l.log`, `m4l-lifecycle.log`,
`shader-contract.json`, `m4l-audit.json`, `final-hashes.json`이다.
4종 빌드·export·설치 스냅샷은 `validation-latest.json`이 가리키는 폴더에 있다.
최종 로컬 native DLL SHA-256은 `2B56F929C0EF5F9356DFE8F15E84813BA43E87FC0EA13FCAD9F17EE3EBE40DC6`이다.
과거 `validation` 폴더의 스냅샷은 이번 세션 시작 시점과 달라 초기 검사에서 HOLD가 났다.
실제 세션 시작 파일 `installed-resume-before.json`과 현재 설치 파일은 동일하며,
설치 파일을 복구하거나 덮어쓰지 않았다.

**남은 판정은 실기 측정이다.** 이번에 생성한 계획은 실행하지 않았다.
실제 DD 비트 동일·Exit 최대 2·HELLO PSNR 42.8 및 GPU 회귀 없음,
render ≤100µs·main ≤150µs, 원래 크기 DD 초과 붕괴 해소를 확인하기 전까지
해당 목표를 달성했다고 표시하지 않는다.
