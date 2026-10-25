from pathlib import Path

root = Path(__file__).resolve().parents[2]
out = Path(__file__).resolve().parent
docs = root/'docs/handoff'
status = 'IMAGEOWN=FAIL fps_before=NA fps_after=NA gain_pct=NA pixel_max=5 CPU_fallback=0 nonimage_fallback=14 image_excluded=0 reason=COLLAPSE_PIXEL_SCOPE_LIMIT game_reason=INTEGRATED_PIXEL_PREFLIGHT_FAILED'
brief = ('최신 run3 직접79줄은 원시158~844행과 순서 일치다. 260/262초 통합 changed27870/23002·max2/5·반복/복원0이며 262초에서 중단했다. '
         '등록93/주소93/누락0·직접34/48·배열4/4·GPU28523/CPU fallback0·이미지제외0·비이미지 정렬보호14개다. '
         '최신 run3의 O/G·G/B 분해는 없으며 이전 run2의 통합전환 국소화와 구분한다. 정확한 픽셀 내부 원인은 미확정이다. '
         '이미지 등록/부분갱신 및 픽셀기준3을 확인했고, 통합renderer/셰이더는 수정 소유 밖이므로 현재 허용범위의 설계경계에서 중단한다. 전체설계 불가능 판정은 아니다. '
         '새계획/같은333단계 반복요청/코드변경/빌드반복0·보호285파일 SHA256 유지다. '
         '게임·설치·커밋·푸시·장치·Editor·설정/제어변경0회. 270초/ABBA/FPS/+20%/끊김/전곡/설치복원NA. '
         'IMAGEOWN=FAIL reason=COLLAPSE_PIXEL_SCOPE_LIMIT·상세결과/out/imageown12 참조.')
section = f'''{status}

## 자동 반복 재시작 4회차(누적12회차) — 최신 run3 확인·통합 수정 소유 경계 유지

`codex-al-im-4.md` 최상단의 최신 `im-autoloop-run3-evidence.txt`를 우선했다. 중첩 run8과 이전 회차는 과거 이력이다. 마스터·이전 이미지 인계·제미나이 분석·두 장부를 읽었다. 사용자 지시대로 게임 실행·설치·커밋은 하지 않았다. 푸시·자동 장치 호출/중지/예약·Unity Editor·설정/제어 파일 변경도 0회다.

### 이번 외부 증거

직접79줄을 `E:/StutterFixWork/night-results/al-im-3.log` 원시158~844행과 순서·중복까지 대조했다. 시작 선언만1500자 접두부다. 3440×1440@164.999의 외부 실측이며 이번 작업에서 새 게임 측정을 한 것은 아니다.

| 원시 행 | 관측 |
|---|---|
| 386 | 등록93·주소93·누락0·직접34/48·배열4/4·변형누락0·valid=True |
| 481,703 | GPU28523·CPU fallback0 |
| 536,757 | 이미지제외0·비이미지 정렬보호14/허용285·주소누락0·80구간·valid=True |
| 601,612 | 260초 changed27870·maxRGBA2,2,2,0·valid=True |
| 617 | live off·Rec11821/기대11821·미관측순서0 |
| 821,832 | 262초 changed23002·maxRGBA5,5,5,0·valid=False |
| 823,825,827,829 | G/N·G/G2·O/O2·B/BN 각각차이0 |
| 831,833 | max_ok=False, 다른9개 조건True·INTEGRATED_PIXEL_PREFLIGHT_FAILED |
| 842,844 | 종료복구Rec0/기대0·미관측순서0·native_contexts0 |

260초도 반복/복원 대조가 모두0이다. 262초 최대차이5가 허용3을 넘어 선행 검사에서 중단됐다. 270초·ABBA·FPS/+20%·30ms이상 끊김·전곡수명·설치복원은 **NA**다. 이전 run1의 FPS/B2끊김4회를 이번 결과로 옮기지 않는다. 이후 `FastMoveOriginal` 복구는 픽셀 검사 실패 뒤 종료 과정이며 최초 실패 원인으로 취급하지 않는다.

이번 로그에는 O/G·G/B 분해 비교가 없다. 직전 run2의 최초 PNG 분석에서 확인한 O/G=3픽셀·max1, G/B=22958픽셀·max4, 허용초과3픽셀 모두O=G라는 국소화는 **이전 표본의 증거**다. 최신 run3에서 같은 수치를 얻었다고 주장하지 않는다. 외부에서 덮어쓰는 공유PNG는 이번에 읽거나 복사/분석하지 않았고, 새 표본 귀속이나 초과픽셀 개수도 주장하지 않는다. 서로 다른 표본의 max4→5를 코드 회귀량으로 계산하지 않는다.

### 코드 대조·중단 이유

- `GpuDecoImagePlayback.cs`의 `RefreshGpuImageGeometry`에는 독립GPU 정점/UV 갱신과 `UpdateDrawCollapseTextureSlot` 부분갱신이 이미 연결돼 있다. `GpuDecoDrawCollapseRenderer.cs`의 등록/주소기억 연결도 존재하고 외부coverage는누락0이다. 이미 해결된 슬롯 부족/등록/UV조건/비이미지폴백 검사를 다시 수정할 근거가 없다.
- `GpuDecoImageGeometryFixture.cs`의 `ImageOwnPixelMax=3`과 선행실패시ABBA차단이 현재 요구대로다. 최신로그는 최대차이 조건만False다. 기준5허용·이미지제외·실제fallback카운터 덮어쓰기·14개 정렬보호제거는 하지 않았다.
- 통합renderer는 별도 `GpuDecoBatch.shader` 재질과 `DispatchCollapseSpan` 경로를 사용한다. `GpuDeco.compute`의 공통정점 뒤 통합분기에는 색의 `floor(saturate(color)*255+0.5)/255` 변환이 있고, 배치셰이더는 별도텍스처배열/UV/clamp샘플링·색곱/프리멀티플라이 경로다. 이런 차이는 조사대상이며 **특정 연산이 최신 실패의 원인이라고 확정할 자료는 없다.**
- 기존 `GpuDecoDrawCollapseProbe.cs`는 `Sprites/Default`·흰정점색·단일quad로40사례를검사한다. 실제게임 `Sprites/Tile`·반투명색·중첩안개와 동일성은 그 검사로 증명되지 않는다. 게임 `scrVisualDecoration.cs` 근사디컴파일의 SetSprite→SetPointSamplingVariant 및 Clamp 호출은 현재 설치DLL의 읽기전용ILScan과 교차확인했다. 이IL이 native셰이더 산술이나 전체정렬을 증명하지는 않는다.

**현재 허용범위의 설계 경계에서 중단한다.** 직전 표본은 실패를 통합전환에 국소화했고 최신run3도 누락/복원/정렬검사는통과한채 통합픽셀에서실패했다. 통합renderer/정책/셰이더/번들은 지시서의 이미지파일 소유범위 밖이다. 허용된 입구1줄/텍스처helper를 렌더링산술 교체권한으로 확대하지 않는다. 독립이미지 정점을 임의보정해 통합오차를 상쇄할 근거도 없다. 이는 전체통합방식의 불가능 판정이 아니라 **현재 증거와 수정 권한의 경계**다. 원인확정이나 코드수정 없이 같은333단계/옛diag1/diag2를 다시 요청하지 않는다.

소유자 후속작업은 실제 `Sprites/Tile`와 `GpuDecoBatch`를 동일정점/UV/색/텍스처/카메라/순서로 고정한 반투명색·다중안개 대조에서 비교하는 것이다. 색반올림·샘플링/블렌드와정렬을분리하고, 실패표본의 O/G·G/B도 이름만같은 공유파일 대신 원시로그와일치하는 불변캡처로검증해야한다. 이것은 이번 게임시험 요청/예약이 아니다. 기준3·정렬보호·48슬롯은 유지한다.

### 보존·오프라인 검증

코드/생성기/기존JSON/tag/native/셰이더/번들/설정/제어파일 변경0·새계획0·3빌드/LoadCheck/기존검사 반복0이다. 코드변경 없는 반복빌드로 게임픽셀 근거를 대신하지 않았다. 보호285파일의 작업 전후SHA256·직접79줄 원시순서·문서5개 최신수치/기존이력 보존·10줄요약·git diff --check를 확인한다. 분석/설치DLL 읽기전용ILScan 결과는 `out/imageown12/`다. 공유스킬/메모리는 수정하지 않았으며 task-observer 체크포인트만 자체분석폴더에 남겼다.

{brief}

---

'''
def old(name):
    return (out/('before-'+name)).read_text(encoding='utf-8-sig')
(docs/'codex-imageown-result.md').write_text(section+old('codex-imageown-result.md'),encoding='utf-8')
for name in ('im-autoloop-ledger.md','imageown-autoloop-ledger.md'):
    previous=old(name)
    first,rest=previous.split('\n',1)
    (docs/name).write_text(first+'\n\n## 재시작 4회차(누적12회차) — 최신 run3 분석·통합 수정 소유 경계\n\n'+brief+'\n'+rest,encoding='utf-8')
previous=old('codex-master-20261022.md')
first,rest=previous.split('\n',1)
(docs/'codex-master-20261022.md').write_text(first+'\n\n**최신 imageown 재시작4회차(누적12회차, 2026-10-25):** '+brief+'\n'+rest,encoding='utf-8')
summary=[status,
'최신run3 직접79줄 원시158~844행순서대조; 260/262초 changed27870/23002·max2/5; 262초선행중단.',
'O/O2·G/N·B/BN·G/G2 반복/복원각0; 최대차이조건만False·허용기준3유지.',
'등록93/주소93/누락0·직접34/48·배열4/4·GPU28523/CPU fallback0·이미지제외0·비이미지정렬보호14.',
'최신O/G·G/B분해없음; 이전run2 통합전환국소화와구분·이번공유PNG읽기/분석없음.',
'이미지부분갱신/등록연결존재·통합별도색반올림/셰이더경로확인; 정확한픽셀내부원인미확정.',
'통합renderer/정책/셰이더/번들은소유밖: 현재허용범위설계경계중단·전체설계불가능판정아님.',
'소유자후속: 실제Sprites/Tile와Batch를반투명색/중첩안개로대조; 같은333단계반복요청0.',
'코드/생성기/JSON/tag유지·새계획0·빌드/LoadCheck반복0·보호285SHA유지·문서5개검증.',
'게임/설치/커밋/푸시/장치/Editor/설정/제어변경0회; 270초/ABBA/FPS/+20%/끊김/전곡/설치복원NA.']
assert len(summary)==10
(docs/'codex-imageown-summary.txt').write_text('\n'.join(summary)+'\n',encoding='utf-8')
print('Updated five handoff documents; summary exactly10 lines; old result and ledger histories retained.')
