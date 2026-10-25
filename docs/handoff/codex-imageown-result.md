IMAGEOWN=FAIL fps_before=NA fps_after=NA gain_pct=NA pixel_max=5 CPU_fallback=0 nonimage_fallback=14 image_excluded=0 reason=COLLAPSE_PIXEL_SCOPE_LIMIT game_reason=INTEGRATED_PIXEL_PREFLIGHT_FAILED

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

최신 run3 직접79줄은 원시158~844행과 순서 일치다. 260/262초 통합 changed27870/23002·max2/5·반복/복원0이며 262초에서 중단했다. 등록93/주소93/누락0·직접34/48·배열4/4·GPU28523/CPU fallback0·이미지제외0·비이미지 정렬보호14개다. 최신 run3의 O/G·G/B 분해는 없으며 이전 run2의 통합전환 국소화와 구분한다. 정확한 픽셀 내부 원인은 미확정이다. 이미지 등록/부분갱신 및 픽셀기준3을 확인했고, 통합renderer/셰이더는 수정 소유 밖이므로 현재 허용범위의 설계경계에서 중단한다. 전체설계 불가능 판정은 아니다. 새계획/같은333단계 반복요청/코드변경/빌드반복0·보호285파일 SHA256 유지다. 게임·설치·커밋·푸시·장치·Editor·설정/제어변경0회. 270초/ABBA/FPS/+20%/끊김/전곡/설치복원NA. IMAGEOWN=FAIL reason=COLLAPSE_PIXEL_SCOPE_LIMIT·상세결과/out/imageown12 참조.

---

IMAGEOWN=FAIL fps_before=NA fps_after=NA gain_pct=NA pixel_max=4 CPU_fallback=0 nonimage_fallback=14 image_excluded=0 reason=COLLAPSE_PIXEL_SCOPE_LIMIT game_reason=INTEGRATED_PIXEL_PREFLIGHT_FAILED

## 자동 반복 재시작 3회차(누적11회차) — 통합 전환 픽셀 오차 국소화·수정 소유 경계에서 중단

`codex-al-im-3.md` 최상단의 **최신 run2**를 우선했다. 중첩 run8 및 직전 run1의 FPS/끊김은 과거 이력이다. 게임 실행·설치·커밋·푸시·자동 장치 호출/중지/예약·Unity Editor 실행·설정/제어 파일 변경은 모두 **0회**다.

### 최신 외부 시험

`im-autoloop-run2-evidence.txt` 직접79줄은 `E:/StutterFixWork/night-results/al-im-2.log` 원시158~838행과 순서·중복까지 일치했다. 시작 선언만1500자 접두부다. 환경은3440×1440@164.999다.

| 원시 행 | 관측 |
|---|---|
| 380 | 등록93/주소93/누락0·직접34/48·배열4/4·변형누락0 |
| 383 | 준비82구간·포함14595개·304414720B(290.312500MiB)·복사59 |
| 475 | GPU28523·CPU fallback0 |
| 530 | collapsecheck: 이미지제외0·비이미지정렬제외14·누락0·valid=True |
| 606 | 260초 changed28818·maxdiff3·반복/복원0·valid=True |
| 611 | live off·Rec11821/기대11821·미관측순서0 |
| 815, 826~828 | 262초 changed22961·maxRGBA4,4,4,0·반복/복원0·valid=False·픽셀선행중단 |
| 836, 838 | 종료복구Rec0/기대0·미관측순서0·native_contexts0 |

270초·ABBA4창·FPS/+20%·30ms이상끊김·새코드성능·전곡수명·설치복원은 **NA**다. 지난run1의 B2 끊김4회나 FPS를 이번수치로 옮기지않았다. 원시 `game_reason=INTEGRATED_PIXEL_PREFLIGHT_FAILED`와 이번 분석분류 `reason=COLLAPSE_PIXEL_SCOPE_LIMIT`를 구분한다.

### 새로 확정한 오차 단계 — 기존 PNG 오프라인 대조

첫 분석시 설치폴더의 기존 `framecmp/O,O2,G,N,B,BN,G2.png` 7장을 **읽기전용**으로 분석했다. 모든크기3440×1440·O/B수치가원시815행과정확히일치하고 반복/복원4쌍도0임을검증한뒤 `out/imageown11/pixel-analysis.json`에수치를저장하고 `preserved.json`에당시7파일SHA256을기록했다. 이미지를새로생성/수정하거나게임을호출하지않았다.

| 비교 | 다른 픽셀 | maxRGBA |
|---|---:|---|
| CPU 원본 O → 독립 이미지 GPU G | 3 | 1,1,1,0 |
| 독립 이미지 GPU G → 통합 B | 22958 | 4,4,4,0 |
| CPU 원본 O → 통합 B | 22961 | 4,4,4,0 |
| O/O2·G/N·B/BN·G/G2 | 각각0 | 각각0,0,0,0 |

**O/B가허용3을넘는픽셀은3개이며, 그 3개는모두O=G(max0)·G/B=max4다.** 따라서 해당실패3픽셀의오차는 독립이미지GPU까지존재하던오차가누적된것이아니라 **통합전환에서발생**했다. 정렬·샘플링·색/블렌드정밀도중어느것이정확한원인인지는이자료만으로분리할수없다. 장식ID/텍스처ID귀속도없다.

그뒤재분석시공유PNG가외부에서덮어써진것을감지했다(O/B수치불일치·G/G2복원불일치). 검사는즉시거부했고 최초저장JSON을덮어쓰지않았다. 후속PNG/후속외부시험을run2와섞지않았으며, 원래7장사본은보관되기전에덮어써져 **원본PNG를다시계산하는재현은현재불가**하다. 최초계산수치/원시일치/당시SHA는보존되어있다. `analysis.txt`는최초JSON과원시에서재구성한검증보고이며, `stale-capture-rejected.txt`는후속거부기록이다. 공유PNG의변경은이작업의쓰기행위가아니다.

### 코드 대조와 현재 허용 범위의 설계 경계

1. `GpuDecoImagePlayback.cs:188`의RefreshGpuImageGeometry는동일이미지정점/UV/재질을독립GPU에반영하고허용helper로통합텍스처주소를부분갱신한다. 이번coverage와collapsecheck는이미지/주소누락0이다. 독립GPU원본대비max1도새로확인됐으므로·이전UpdatePosition/CPU정점주입/공유wrap변경을되풀이할근거가없다.
2. `GpuDecoDrawCollapseRenderer.cs:94,418,429,657`은별도의GpuDecoBatch셰이더/공유재질·Float32색/60바이트정점·DispatchCollapseSpan경로로바꾼다. `GpuDeco.compute:233,282~291,303`은공통월드정점수식뒤통합전용UNorm8색반올림/텍스처주소/정점배치를기록한다. `GpuDecoBatch.shader:135~146,200`은별도정점변환·배열UV/샘플러·색곱/프리멀티플라이를수행한다. **통합전용의변경지점**은확정하되·그중특정연산이틀렸다고단정하지않는다.
3. 기존 `GpuDecoDrawCollapseProbe.cs:85,88,107,112`의40텍스처합성검사는 **Sprites/Default**를기준으로 **정점색white·단일quad**를비교하며·`game_Sprites/Tile=UNVERIFIED`를명시한다. 그40통과가실제안개중첩/게임셰이더와최대차이3이하의증명이아님을코드로확인했다. 정렬보호14개가원인이라는증거도없어보호/카운터를유지한다.
4. 게임 `scrVisualDecoration.cs` 근사디컴파일과설치Assembly-CSharp.dll읽기전용ILScan으로 SetSprite→SetPointSamplingVariant/Clamp호출을교차확인했다. 게임셰이더native산술/정렬전체는이IL로확정되지않는다.

**중단이유:** 이실패를고칠대상은독립이미지소유의앞단이아니라통합렌더링경로다. 해당renderer/정책/셰이더/번들은소유규칙밖이며, 허용된GpuDecoDrawCollapse.cs입구1줄/텍스처helper는정렬·블렌딩·프래그먼트연산을교체할권한이아니다. 이미지정점을일부러보정해통합오차를상쇄하면이미검증한독립경로/복원을바꾸게되며·그근거도없다. **현재허용범위에서의설계경계에서중단한다. 전체통합설계가불가능하다는판정은아니다.**

소유자에게구체적으로인계한다: 통합renderer/셰이더영역에서동일정점/UV/색/텍스처/카메라/정렬을고정하고 **실제Sprites/Tile와GpuDecoBatch를반투명색·여러겹안개조건으로대조**해샘플링/색반올림/블렌드와정렬을분리해야한다. 위3개초과픽셀은O=G라는대조기준을유지한다. 정책상한확장·KeepAfterBarrier재활성화·이미지제외·픽셀4허용·실제카운터덮어쓰기는해법으로취급하지않는다. 정확한원인수정전같은333단계/옛diag1/diag2반복요청은없다.

### 검증과 보존

런타임코드/생성기/기존JSON/tag/native/셰이더/번들/설정/제어파일변경0회다. 새계획/DLL/번들은만들지않았다. 코드변경이없으므로3빌드/LoadCheck/기존검사를반복하지않았다. 보호292개중읽기전용공유PNG7개를분리한285개의SHA256은유지한다. 외부PNG변화는검증보고에별도기록하며보존성공으로세지않는다. 원시/증거/픽셀통계·게임IL·문서5개정합성·10줄요약·기존이력보존·git diff --check를검증한다. 수치와자체분석은 `out/imageown11/`에있다. task-observer3.5.0의프로젝트별체크포인트만남겼으며공유스킬/메모리는수정하지않았다.

최신 run2 직접79줄을 원시158~838행과 순서 대조했다. 통합260/262초 changed28818/22961·max3/4·반복/복원0이며 262초에서 중단, 270초/ABBA/FPS/끊김은NA다. 로그와 일치한 첫 PNG 오프라인 분석: O/G 3픽셀·max1, G/B 22958픽셀·max4, O/B 22961픽셀·max4; 초과3픽셀 모두 O=G이며 G/B=4다. 실패를 독립 이미지 소유가 아닌 통합 전환 단계로 국소화했다. 정확한 정렬/샘플링/셰이더 산술 원인은미확정이다. renderer가 별도 GpuDecoBatch 재질/셰이더로 교체하고 기존40합성검사는 Sprites/Default·흰 정점색만대조해 게임 Sprites/Tile·중첩안개의동일성을증명하지못한다. 필요한통합renderer/셰이더수정은소유규칙밖이므로 현재허용범위의설계경계에서중단·소유자에게인계한다. 전체설계불가능판정아님. 기준3/정렬보호/48슬롯/이미지소유/실제카운터유지·코드/생성기/기존JSON변경0·새계획/반복시험요청0·빌드/LoadCheck반복0이다. 보호292개중코드/게임DLL/번들/설정등285개SHA유지, 공유PNG는외부덮어쓰기감지로보호수에서분리·후속분석거부했다. 게임·설치·커밋·푸시·장치·Editor·설정/제어변경0회. IMAGEOWN=FAIL reason=COLLAPSE_PIXEL_SCOPE_LIMIT·상세결과/out/imageown11참조.

---

IMAGEOWN=FAIL fps_before=NA fps_after=NA gain_pct=NA pixel_max=3 A0_fps=211.823440 B1_fps=208.592248 B2_fps=197.047153 A3_fps=NA spikes_30ms=4 CPU_fallback=0 nonimage_fallback=14 image_excluded=0 reason=ABBA_INCOMPLETE_AFTER_B2_SPIKES new_code=NOT_RUN

## 자동 반복 재시작 2회차(누적 10회차) — 실패 창 완료순번 수정·끊김 진단 추가

최상단 `codex-al-im-2.md`가 지정한 **최신 run1**을 우선했다. 중첩된 run8과 이전9회차 결과는 이력이며 이번 측정으로 섞지 않았다. 사용자 지시에 따라 게임 실행·설치·커밋·푸시·자동 장치 호출·Unity Editor 실행·설정/제어 파일 변경은 모두0회다.

### 외부 측정과 원시 대조

`im-autoloop-run1-evidence.txt` 직접289줄을 `E:/StutterFixWork/night-results/al-im-1.log` 158~1971행과 순서·중복까지 대조했다. 시작 선언만1500자 접두부이며 나머지는 전체 줄 일치다. 실제 환경은3440×1440@164.999다.

| 원시 행 | 관측값 |
|---|---|
| 382 | 등록93/주소93/누락0·직접35/48·배열4/4·변형누락0 |
| 617 | 260초 통합 원본 대비 changed28603·maxdiff3·반복/복원0·valid=True |
| 844 | 262초 changed22550·maxdiff3·반복/복원0·valid=True |
| 1064 | 270초 changed9186·maxdiff2·반복/복원0·valid=True |
| 1271 | A0:211.823440FPS·2118프레임/관측2118·30ms이상0·CPU fallback0·정렬보호14·valid=True |
| 1506 | B1:208.592248FPS·2085프레임/관측2085·30ms이상0·CPU fallback0·정렬보호14·valid=True |
| 1745 | B2:197.047153FPS·1970프레임/관측1970·30ms이상4·CPU fallback0·정렬보호14·valid=False |
| 1280/1515/1754 | 완료한 세 창의 복원 valid=True |
| 1964/1967 | `images begin 3`의 clean A/B/B/A 순번 검사 실패, A3 미측정 |
| 1970~1971 | 종료 복원Rec0/기대0·미관측순서0·native_contexts0 |

픽셀 선행3개와 이미지 무폴백은 해당 표본에서 통과했다. 정지 픽셀은 전체 연속재생 화질의 증명이 아니다. B1 대 A0는−1.525418%이며 **완성된 ABBA 증가율이 아니다**. A3가 없어 before/after 평균과+20%는NA다. A0만 사용한 가상의 수용하한은254.188128FPS이고 B1은미달이지만 최종ABBA 하한을 대신하지 않는다. 기존 약202FPS와 최신창의 평균을 섞지 않았다.

원시에는4회 끊김의 곡시각·길이·GC/엔진원인 자료가 없다. `cost scope=live_off`는 누적 managed CPU 범위이고 실제 프레임 지연의 원인 귀속이 아니다. 알파더티큐는이미구현되어있고 B2 종료의alpha_watch0/alpha_checks8106/alpha_dirty6450은 누적수치다. 4476개전수순회가현재원인이라고되풀이하거나, GC/텍스처업로드가원인이라고단정하지않았다.

### 코드로 확정한 중단 원인과 수정

`GpuDecoImageGeometryFixture.cs`의 begin은 `index==imageOwnWindows.Count(x=>x)`를 요구했다. end는 B2의4회끊김 때문에 `imageOwnWindows[2]=false`를 저장한다. 따라서 성공수는2로남아정상다음순번3이거부된다. GPU28523·텍스처커버리지·픽셀은 이미 통과했으며 이 순번오류가 최신 자동시험의 마지막 중단원인이다. 이 수정이 끊김이나성능저하를해결한것은아니다.

- `imageOwnNextWindow`로 완료순번과합격여부를분리했다. 실패창도 end후순번을소비하며, 직전창복원이확인된경우에만다음창을시작한다. 같은실패창재시도·순번건너뛰기·미완료창복원·측정뒤모드변경은거부한다.
- 네창이끝났으면실패창을포함한측정FPS/증가율/끊김합계와 `completed=4/4 windows=3/4`를기록한다. `imageOwnFailed`와개별유효성은유지하므로끊김이있으면최종FAIL이다. 자료가다모였는데실패한경우 `ACCEPTANCE_FAILED`, 미완료면 `EVIDENCE_INCOMPLETE`다.
- 프레임중일시적GPU/정렬/일시정지/소유권오류도현재창의valid=False에반영한다. 전역실패판정도유지한다.
- 끊김최대16개를사전할당struct배열에저장한다. 곡시각은지연을관측한시점이고원인발생시점의증명은아니다. 길이·프레임·창시작이후GC0/1/2누계를보존한다. 로그문자열/출력은창종료후이며초과표본수도기록한다. GC횟수동시관측만으로원인확정하지않도록 `attribution=UNPROVEN`을명시한다. 매프레임추가전수스캔/캡처/읽기되돌림은없고최대프레임길이만추가갱신한다. 실제진단오버헤드는미측정이다.
- 회귀검사에서정확히+20%인120/100이 `(1.2-1)*100` 반올림으로19.999999…가되어실패하는오류를발견했다. `after>=before*1.20`로동일한포함하한을직접비교한다. +20%미만은계속거부한다.
- 진입소스의 `ImageOwnPixelMax=4`/완화주석을지시서의최대3으로되돌렸다. 최신실측3/3/2는이기준안이다. 픽셀기준·30ms이상0·CPU/이미지/텍스처누락0·비이미지정렬보호<=1%·순서보호는완화하지않았다.

게임 `scrVisualDecoration.cs`의원래SetSprite/포인트샘플링/공유wrap설정을읽고설치Assembly-CSharp.dll의읽기전용ILScan으로호출을교차확인했다. 이경로를우회하거나다른장식과공유하는텍스처wrap을바꾸지않았다. 실제끊김원인과이미지방식의설계한계는현재자료로미확정이다. renderer/policy/Dormancy/TightLate/FastMove/Export/셰이더/native/번들영역은수정하지않았다.

### 오프라인 검증과 남은 조건

| 검사 | 최종 결과 | 근거(`out/imageown10/`) |
|---|---|---|
| DEV/Player/PlayerAuto 격리Release 빌드 | 오류0/0/0·경고48/4/40 | verify.ps1, build-*.txt |
| LoadCheck | FieldRef279/229/281곳·틀림0/0/0·일반Player 연구제외 | loadcheck-*.txt |
| 실제 판정메서드5개+CPU어댑터 | 56검사통과 | check-gate.py, gate-check.txt |
| 계획생성기 | 8검사통과·과거명시12출력동일 | generator-tests.txt, plan-check.txt |
| 보호파일 | 의도한코드2개외282개SHA256유지 | preserved.json, preserved-after.json |

56검사는기존이미지/누락/폴백/픽셀가드와실패B2뒤A3완료·미복원차단·실패재시도차단·최종FAIL유지·정상PASS양성대조·+20%포함경계·미달거부·완료/합격표시·미완료평균NA·중복카메라표본제거·16개상한/초과수·일시적런타임실패를포함한다. 실제Unity렌더링/픽셀/GC 동작/FPS의증명은아니다. 첫어댑터검사에서+20%경계오류를발견해고친뒤최종소스로3빌드/LoadCheck를재검증했다.

새 `tools/optimization/out/imageown-abba5-completion-steps.json`은333단계**생성만**했다. 명령순서는기존abba4와같지만새managed완료/실패상태·끊김진단코드와새tag를요구한다. 기존JSON과명시diag/diag1/abba출력은보존했다. 수정DLL은 `out/imageown10/build/playerauto/StutterFix.dll`에격리되어있다. 설치DLL/기존bin/번들은교체하지않았다.

새진단은빠진A3와4회끊김의최소상세정보를얻기위한변경이며성능개선완료가아니다. 동일실패코드무변경반복이나옛diag1/diag2재실행요청은없다. 외부실행자가새managed코드로네창/복원과새 `GPUIMG_DIAG mode=windowspike`를얻어야끊김귀속과다음성능수정근거가생긴다. 이작업에서게임을실행하거나장치를호출/중지/예약하지않았다. 실제+20%·끊김해결·수정후픽셀·전곡수명·설치복원은NA이며**IMAGEOWN=FAIL**이다. 공유스킬/메모리는수정하지않았다.

---

IMAGEOWN=FAIL fps_before=NA fps_after=NA gain_pct=NA pixel_max=NA registered=93 addressed=93 missing=0 direct_required=34 direct_limit=48 arrays_used=4 array_limit=4 GPU_owned=28523 CPU_fallback=0 previous_fallback=14 previous_image_excluded=0 spikes_30ms=NA reason=ACCEPTANCE_FIX_NOT_RUN

## 자동 반복 9회차 — 17:15 지시 적용, 이미지 무폴백 판정 일관성 수정

`codex-al-im-9.md` 끝의 **2026-10-25 17:15 추가 지시**가 이전8회차의 전체폴백0 중단 판단을 대체한다. 비이미지 정렬 보호14개는 독립 GPU renderer로 유지하고, 이미지 장식은 하나라도 제외되면 실패한다. 기존 준비/정렬/샘플링/소유권 가드를 끄거나 실제 폴백 카운터를0으로 덮지 않았다. 게임 실행·설치·커밋·푸시·자동 장치 호출·Unity Editor 실행·설정/제어 파일 변경은 **모두0회**다.

### 지정된 외부 run8 분석

`im-autoloop-run8-evidence.txt`의 직접53줄을 `E:/StutterFixWork/night-results/al-im-8.log`의158~544행과 순서·중복까지 대조했다. 시작 선언만1500자 접두부이며 나머지는 전체 줄 일치다. 지난 run7 수치를 새 측정으로 옮기지 않았다.

| 원시 행 | 외부 run8 관측값 |
|---|---|
| 383 | 등록93/주소93/누락0·직접34/48·배열4/4·변형누락0 |
| 387 | 준비82구간/포함14595개·304414720B=290.312500MiB·복사59·텍스처폴백0 |
| 478 | GPU 소유28523·CPU fallback0 |
| 524, 528 | 정렬 제외14·재계획80구간/포함14581개·장벽38992·배열4재사용/추가복사0 |
| 532~533 | 현재/변형 텍스처누락0·등록됐지만제외14·이미지제외0·전체폴백14·valid=False·COLLAPSE_PREFLIGHT_FAILED |
| 542, 544 | 종료FastMoveOriginal복구·Rec0/기대0·미관측순서0·originals_restored=True·native_contexts0 |

14595−14581=14이며이미지제외0이므로14개 모두 비이미지다. CPU fallback0은 독립GPU renderer 제외0과 다르다. 원시에 통합B 픽셀/IMAGEOWN_PIXEL/IMAGEOWN_WINDOW/PASS가 없으므로 통합픽셀·ABBA FPS·+20%·30ms이상 끊김·전곡수명·설치복원은NA다. 반복캡처0/종료복구를 해당 통과로 쓰지 않는다. 여유VRAM은UNVERIFIED다.

게임 `scrVisualDecoration.cs` 근사 디컴파일의 SetDepth에서 sortingOrder=-depth/정렬레이어 대입을 읽고 설치 Assembly-CSharp.dll의 읽기 전용ILScan으로 setter호출을 교차확인했다. 엔진 native 동점순서 전체의 증명은 아니다. 원시대조/게임IL/해시는 `out/imageown9/analyze.py`, `analysis.txt`, `game-scrVisualDecoration-il.txt`에 남겼다.

### 수정과 판정 범위

- 진입 시점 소스에는 collapsecheck의 이미지제외0·누락0·비이미지1%이하 허용이 이미 있었다. 하지만 ObserveImageOwnWindow/이미지end/픽셀 판정이 여전히 전체폴백0을 요구했다. 선행검사만 통과해도 뒤에서 같은14개로 실패하므로 이미지용fixture의 해당 판정을 함께 수정했다.
- `CaptureImageOwnOrderingFallbacks`는 측정 전, 텍스처 주소가 등록된 비이미지 폴백만 저장한다. 실제 마스크 개수와 카운터가 일치해야 하며 최대 소유수1%(28523개일 때285개)다. 한 이미지 제외·주소누락·카운터불일치·초과·stale버퍼·collapse오류는 거부한다.
- 측정 중에는 승인된 인덱스와 실제 개수/마스크참조/버퍼소유자를 확인한다. 개수가 같은 채 비이미지 하나가 빠지고 이미지 하나가 들어와도 거부한다. 전수28523개 스캔은 픽셀/선행/창시작 시점에만 하며 매 프레임은 승인집합(직전14개)만 확인한다. 실제 비용/FPS 개선은 아직 미측정이다. 승인집합 변화는 보수적으로 실패 처리한다.
- 기존CPU fallback0·순서무시끔·픽셀max<=3·반복/복원0·오류0·30ms이상0·ABBA+20%·세픽셀/네창/네복원 필수 조건을 유지한다. 전체폴백수는 기존texture_fallback 필드에 그대로 기록하며 이미지제외와 구별한다. 픽셀조건별 진단로그도 보존하고 ordering_ok를 단락평가 이전에 갱신해 과거값을 표시하지 않게 했다.
- renderer/policy/Dormancy/TightLate/FastMove/Export/셰이더/native/번들/공유wrap은 수정하지 않았다. 새로운 기본옵션이나 일반Player 연구 포함도 없다.

### 오프라인 검증과 외부 시험 인계

| 검사 | 결과 | 근거 (`out/imageown9/`) |
|---|---|---|
| DEV/Player/PlayerAuto 격리 Release 빌드 | 오류0/0/0·경고4/4/40 | verify.ps1, build-*.txt |
| LoadCheck | FieldRef279/229/281곳·틀림0/0/0·일반Player 연구제외 | loadcheck-*.txt |
| 실제 판정메서드5개 추출+CPU어댑터 | 28검사 통과 | check-gate.py, gate-check.txt |
| 계획생성기 | 8검사 통과·기존명시모드12출력동일 | generator-tests.txt, plan-check.txt |

28검사는14개허용·마스크/카운터불변·이미지1개거부·누락현재/미래텍스처거부·1%경계285/286개·개수불일치·오류/stale주소·같은개수의이미지교체·런타임추가폴백/마스크교체·픽셀4/노이즈/30ms/CPU복귀 거부·reset을 검사한다. CPU어댑터이며 실제Unity렌더링/샘플링/픽셀/FPS/복원의 증명은 아니다. 검증도구의독립케이스 초기화와Windows경로정규화 오류는 도구에서 바로잡았으며 게임오류가 아니다. 최종소스 변경을 반영해3빌드/LoadCheck를 재검증했다.

기존333단계abba3 JSON과명시diag/diag1/abba출력을 보존했다. 새 `tools/optimization/out/imageown-abba4-orderguard-steps.json`은333단계이고 명령순서는기존과같으나 **새managed판정/새tag/새수용설명**을 사용한다. 준비창257.5~270.25·48직접/4배열 소유자번들을전제로 하며 실제상한증가는없다. 수정된 PlayerAuto DLL은 `out/imageown9/build/playerauto/StutterFix.dll`에만 있다. 기존bin·설치DLL·번들은교체하지않았다.

게임시험은사용자금지로실행하지않았다. 외부실행자는수정된managed DLL로새계획을실행해260/262/270초통합max<=3·ABBA>=+20%·이미지제외0/주소누락0·승인비이미지<=1%·30ms이상0을측정해야한다. 같은실패코드의무변경재시험과구분하며 자동장치를직접호출/중지/예약하지않았다. **IMAGEOWN=FAIL reason=ACCEPTANCE_FIX_NOT_RUN**을유지한다. 이전8회차의ORDERING_FALLBACK_DESIGN_LIMIT는17:15지시적용전이력이다.

보호283개중의도한fixture/생성기2개만변경하고나머지281개SHA256유지를검증한다. 문서5개·10줄요약·diff검증은verification.txt를따른다. 공유스킬/메모리는수정하지않았다.

## 자동 반복 8회차 — 외부 run7 분석, 비이미지 정렬 제외14개로 중단

지정된 지시서·마스터·두 장부·이전 이미지 인계·제미나이 분석을 읽었다. 외부 run7은 지난 UV 검사 수정 뒤 **GPU 소유28523개 활성화 성공**, 등록93/주소93/누락0이다. 새 실패는 `images collapsecheck`의 **전체 통합 폴백14개**이며 이미지 제외는0개다. 원시 판정 `COLLAPSE_PREFLIGHT_FAILED`와 이 문서의 원인 분류 `ORDERING_FALLBACK_DESIGN_LIMIT`를 구분한다.

현재 정렬 보호 설계는14개를 통합 구간에서 빼고 독립 GPU renderer에 남긴다. 수용 기준은 전체폴백0개이며, 필요한 renderer·정렬 정책은 **Claude 소유의 수정 금지 파일**이다. 이미지 등록/UV/알파 큐 수정으로 해소할 근거가 없으므로 지시된 설계 한계 중단 조건을 적용한다. **현재 설계와 허용 수정 범위의 한계이며, 다른 모든 통합 설계가 불가능하다는 판정은 아니다.**

### 외부 run7 원시 증거

`im-autoloop-run7-evidence.txt`의 직접 증거53줄을 `E:/StutterFixWork/night-results/al-im-7.log`의158~543행과 순서·중복까지 대조했다. 시작 선언은1500자 접두부, 나머지는 전체 줄이 일치한다. 들여쓴 과거 요약은 이번 측정에 섞지 않았다. 재현 근거는 `out/imageown8/analyze.py`, `analysis.txt`다.

| 원시 행 | 외부 run7 관측값 |
|---|---|
| 377~380 | 창257.5~270.25초·선택변형10/제외93·등록93/주소93/누락0·직접34/48·배열4/4·필요38/선언52슬롯 |
| 379, 384 | 배열304414720B=290.312500MiB·준비82구간/포함14595개·복사59·텍스처폴백0 |
| 468, 473 | CPU 원본 O/O2 반복 changed0/4953600·maxRGBA0,0,0,0 |
| 476~478 | GPU 소유28523·CPU fallback0·on=True·활성 소스2135·활성화163.4953ms |
| 506, 511 | 독립 GPU G/N 반복 changed0/4953600·maxRGBA0,0,0,0 |
| 523~527 | 정렬 제외14·활성묶음251·재계획80구간/포함14581개·배열4 재사용/추가복사0·장벽38992 |
| 531~532 | 현재/변형 텍스처 누락0·등록됐지만 제외14·이미지 제외0·전체폴백14·valid=False·선행 검사 실패 |
| 539~543 | 시험 중단 후 FastMoveOriginal 복구·Rec0/기대0·미관측순서0·originals_restored=True·종료native_contexts0 |

준비 포함14595−재계획 포함14581=14로 정렬 제외와 일치한다. `image_excluded=0`과 전체폴백14에 따라 **14개 모두 비이미지 장식**이다. `CPU_fallback=0`은 CPU 소유권 복귀가 없다는 뜻이며 독립 GPU renderer 제외0과 다르다. 이번48직접 슬롯 전제에서 용량은 충분하다. 지난32상한 초과 문제나 이전 run6의 Rec11821/11821 rollback을 이번 결과에 옮기지 않는다. 여유VRAM은 UNVERIFIED다.

O/O2·G/N 각각의 반복0은 **CPU 원본↔독립GPU 동일성 또는 통합 픽셀 통과가 아니다**. 원시에 drawcollapse_on=True·통합 B 캡처·IMAGEOWN_PIXEL·IMAGEOWN_WINDOW·IMAGEOWN=PASS가 없다. 통합 최대픽셀차이·ABBA FPS/+20%·30ms 이상 끊김·전곡 수명·설치복원은 모두NA다. 복구 Rec0/0은 시험 종료 시점의 로그 범위다.

### 코드로 확인한 실패 경로

1. `GpuDecoDrawCollapseRenderer.cs:363`의 BuildDrawCollapseSpans는 활성 장벽과 `(Layer,Order,Queue,Distance)`가 같은 소유 장식을 검사한다. 허용한 앞쪽/조건부 뒤쪽 구간에 남기지 못하면 included=false, TextureFallback=true, 카운터 증가, 독립 renderer의 새 장벽 등록 후 TIE_EXCLUDED를 기록한다. **텍스처 주소 누락용 카운터를 정렬 제외에도 공유**한다. 준비폴백0→정렬제외14→카운터14의 원시 순서와 일치한다.
2. `DrawCollapsePolicy.cs:84`의 KeepAfterBarrier=false를 유지한다. 기존 주석은 켰을 때262초 최대차이4로 허용3을 초과하고 FPS 이득이 없었다고 기록한다. 이는 이번 run7의 새 측정이 아니다. 원시에는14개의 item/재질키·장벽 min/max가 없어 전부 뒤쪽인지/동점인지/장벽 사이인지까지 확정하지 않는다. 실패 스위치를 다시 켜는 접근은 반복하지 않는다.
3. `GpuDecoImageGeometryFixture.cs:148`의 선행 검사는 registeredExcluded==0과 전체폴백0을 요구한다. image_excluded만0이라고 통과시키면 수용 기준을 완화한다. 같은 파일의 ObserveImageOwnWindow와 images end도 B의 전체폴백0을 요구하므로 **선행 검사만 제거해도 ABBA 성공이 되지 않는다**. 모든 가드를 유지한다.
4. `GpuDecoDrawCollapseRenderer.cs:663`은 전체폴백이 있으면 CopyMask 조건에 따라 독립 DispatchEvaluate/native copy/CommandBuffer 경로를 유지한다. 카운터만0으로 덮으면 독립 정점 갱신을 생략할 수 있다. 허용된 이미지 슬롯 helper에서 inclusion/카운터를 지우는 우회도 하지 않는다.
5. 게임 근사 디컴파일 `E:/StutterFixWork/decompiled/Assembly-CSharp/scrVisualDecoration.cs:327`의 SetDepth에서 sortingLayerName과 sortingOrder=-depth 적용을 읽었다. 설치 Assembly-CSharp.dll의 읽기 전용 ILScan으로 해당 setter 호출을 교차확인했다(`out/imageown8/game-scrVisualDecoration-il.txt`). 게임의 정렬 설정 경로 확인이며 Unity native 동점 순서 전체의 증명은 아니다.

### 소유자에게 필요한 작업과 중단 범위

Claude의 renderer/policy 영역에서14개의 item·소유종류·정렬키·장벽 최소/최대 재질키·제외 분기를 확인하고, 원래 순서를 보존하면서 통합할 설계를 수정해야 한다. 장벽 무시·게이트 완화·카운터 조작은 해법으로 채택하지 않는다. **소유자 수정으로 전체폴백0을 만들기 전 같은333단계 계획 재실행 요청 없음·새계획 없음**이다. 변경 후 외부 실행자가260/262/270초 통합 maxdiff≤3·ABBA≥+20%·전체폴백0·30ms 이상 끊김0을 확인해야 한다. 외부 메시지를 보내거나 자동 장치를 직접 중지하지 않았다.

### 이번 검증과 보존

런타임 코드·생성기·기존 JSON/태그·셰이더·번들·설정/제어 파일을 수정하지 않았다. 따라서3빌드·LoadCheck·지난330/8검사는 반복하지 않았다. 문서5개와 자체 `out/imageown8/` 분석/검증 산출물만 작성했다. 외부53줄 순서·계수·미측정 항목 부재·게임 IL 대조, 보호283개 SHA256, 문서 정합성·10줄 요약·diff 검사를 검증했다(`verification.txt`). 처음 분석 스크립트의 IL 제목/CRLF 파싱과 D:/E:간 상대경로 처리를 실제 형식에 맞게 고친 뒤 전체 검사를 통과시켰다. 프로젝트/게임 코드 오류로 오인하지 않는다.

게임 실행·설치·커밋·푸시·자동 장치 호출·Unity Editor 실행·설정/제어 파일 변경은 모두0회다. 아래7회차의 SAMPLING_FIX_NOT_RUN은 외부 run7 수신 전 이력이며 이번 활성화 성공으로 갱신한다.

## 자동 반복 7회차 — 17:05 추가 지시에 따른 wrap 검사 대체, 게임 미실행

`codex-al-im-7.md` 끝의 **2026-10-25 17:05 추가 지시**를 적용했다. 이전 16:50의 직접32 상한/초과 시 중단을 이번 회차에 그대로 적용하지 않는다. Claude가 담당한 정책48/번들 변경을 전제로 이미지의 wrap 검사를 UV 범위 검사로 바꾸라는 최신 구체 지시가 우선한다. 이번에 정책·셰이더·번들을 수정하거나 상한을 추가로 늘리지는 않았다. 지시서의 설명상 필요36과 달리 지정된 외부run6 실제 로그의 직접필요는 **34**다.

이번 게임 실행·설치·커밋·푸시·자동 장치 호출·Unity Editor 실행·설정/제어 파일 변경은 **모두 0회**다. 동일 코드로 게임 시험을 반복하지 않았고, 기존333단계 계획/생성기/태그를 보존했다. 새 managed 코드만 별도 디렉터리에서 빌드·오프라인 검사했다. 최종 픽셀/FPS 성공을 선언하지 않는다.

### 외부 run6 증거와 실패 위치

`im-autoloop-run6-evidence.txt`의 직접 증거 **38줄**을 `E:/StutterFixWork/night-results/al-im-6.log` **158~491행**과 순서·중복까지 대조했다. 시작 선언만 1,500자 접두부이며 나머지는 전체 줄이 일치한다. 들여쓴 뒤쪽 요약은 과거 시험의 발췌이므로 이번38줄에 섞지 않았다. 재현 코드/출력은 `out/imageown7/analyze.py`, `analysis.txt`에 있다.

| 원시 행 | 외부 run6의 관측값 |
|---|---|
| 377 | 창257.5~270.25초·소유28523·선택변형10/제외93·등록93 |
| 378~380 | 정확크기배열7680×4320×4·등록93/주소93/누락0·직접필요34/상한48·배열4/4·필요38/선언52슬롯 |
| 379, 384 | 배열계획304,414,720B=290.312500MiB·준비82구간/14595개·복사59·텍스처폴백0 |
| 475, 480 | CPU 원본 O/O2 반복: changed0/4,953,600·최대RGBA0,0,0,0 (같은 대조의 두 로그) |
| 483~484 | GPU 소유분류28523·CPU fallback0 뒤 활성화 rollback·Rec11821/11821·continuity=RESTORED |
| 486, 490~491 | `gpudeco live on`의 `image collapse changed mip/wrap` 실패·종료native_contexts0 |

`valid=True`는 **48직접 슬롯을 쓰는 외부 준비 단계**의 커버리지 결과다. GPU 활성화 성공, 통합 픽셀, FPS 승인으로 확대하지 않는다. 379행은 할당 전 계획이고384행에는 배열4/복사59가 보고된다. 여유VRAM은 UNVERIFIED다. 이번 로그에는 크기·포맷별 전체 목록이 없으므로 이전run5의44그룹/누락4개를 이번run6의 실제 목록으로 재사용하지 않는다.

실제 오류 검사는 `RefreshGpuImageGeometry → UpdateImageGeometry → UpdateDrawCollapseTextureSlot`에 있다. 기존 슬롯 갱신이 `mipmapCount==1 && wrapU==Clamp && wrapV==Clamp`를 요구해 준비된 주소가 있어도 거부할 수 있었다. 게임 `TextureManager`는 초기 Repeat, `scrVisualDecoration.SetSprite`는 V15 조건에서 Clamp, `LazySprite.Defer`도 같은 조건에서 Clamp를 대입한다. 구형 타일링 관리자는 disableV15Features 조건에서 wrapU/V를 갱신한다. 근사 디컴파일과 설치 DLL의 읽기 전용 ILScan3종을 대조했다(`out/imageown7/game-*-il.txt`). **실패 텍스처 ID·실제 mip/wrap 값은 기존 로그에 없으므로 특정 자산/쓰기 주체를 원인으로 확정하지 않는다.**

### 구현 범위

- `GpuDecoDrawCollapse.cs`는 허용된 기존 RequireLive 문 하나를 `RequireGpuImageCollapseSampling(item,texture);` 한 줄로 대체했다. 주소누락·버퍼소유자·슬롯검사와 32바이트 부분 업로드는 유지한다.
- `GpuDecoImagePlayback.cs`에 mip1 검사와 네 UV쌍의 전체8성분 검사를 추가했다. 유한한 각 성분이 **[-1e-4, 1+1e-4] 양끝 포함** 범위여야 한다. null·길이오류·NaN·Infinity·범위초과는 거부한다.
- `ImageFixtureItem`은 `GpuDecoImageVariant.UV`를 `next.UV`에 복사하고 `UpdateImageGeometry`는 실제 업로드 직후 같은 `slice.Data.Items[item]`을 next로 바꾼다. 새 검사는 그 **현재 업로드 UV**를 읽으므로 이전 스프라이트의 UV를 검사하지 않으며 추가 sprite getter/배열 할당도 없다.
- UV 오류는 `image_uv_outside_unit_range`, mip 오류는 `image collapse changed mips`로 구분한다. UV 거부 시 `GPUIMG_DIAG mode=collapse_uv_rejected`에 item/texture_id를 남긴다. 실패는 기존 활성화 rollback 또는 이미지효과/Flush의 RecoverGpuLive 경로로 전달한다. 새 개별항목 복구 설계를 추가하지 않았다.
- 공유 텍스처의 wrap을 미리 바꾸지 않는다. 셰이더·샘플러·정점 수식·알파 큐·renderer/policy/export/Dormancy/TightLate/FastMove는 수정하지 않았다. 범위 내 UV 허용은 최신 지시의 구현이며 Repeat와 Clamp의 실제 픽셀 동일성은 외부 시험으로 확인해야 한다.

### 검증과 남은 조건

| 검사 | 이번 결과 | 근거 (`out/imageown7/`) |
|---|---|---|
| DEV/Player/PlayerAuto 격리 Release 빌드 | 오류0/0/0·경고48/36/40 | build-*.txt, verify.ps1 |
| LoadCheck | FieldRef279/229/281곳·틀림0/0/0·일반 Player 연구 제외 | loadcheck-*.txt |
| 실제 수정 메서드3개 추출 검사 | **330검사 통과**·Unity 실행 없음 | check-sampling.py, sampling-check.txt |
| 기존 계획 생성기 | **8검사 통과**·기존333단계 JSON 유지 | generator-tests.txt |
| 원시/게임 코드 대조 | 직접38줄 순서일치·설치 DLL ILScan3종 exit0 | analysis.txt, game-*-il.txt |

330검사는 wrapU/V의 Clamp/Repeat/Mirror 조합 허용·공유값 보존, 모든8성분의 양끝/범위 밖 바로 다음 float/NaN/Infinity, UV 길이오류, mip 거부, 올바른 item/교체된 데이터 참조, 오류 전 슬롯쓰기0, 주소누락/잘못된 버퍼소유자 거부, point bit/배열주소/UV scale/half-texel/부분업로드를 검사한다. CPU 어댑터 검사이며 실제 Unity 샘플링/CPU 복귀/픽셀 성능을 증명하지 않는다.

새 관리 DLL은 `out/imageown7/build/{dev,player,playerauto}/`에 격리했다. 기존 bundle/native/설치 DLL과 기존 bin은 교체하지 않았다. `out/imageown7/preserved.json`의282개 중 의도한 이미지 코드/허용helper2개만 변경하고 나머지280개 SHA256을 유지했음을 최종 검증했다. 문서5개·10줄요약·diff 검사도 통과했다(`verification.txt`). 공유 스킬/메모리는 수정하지 않았다.

외부 실행 시에는 수정된 PlayerAuto managed DLL과 소유자가 준비한48슬롯 지원 번들로 기존 `imageown-abba3-window-steps.json`(333단계, SHA256=`41b790f93035f51623bdd6aaee43d4643d5468235ca27065a98291a9f592e2bd`)을 사용한다. 이번에 새 계획을 생성하거나 실행하지 않았다. 코드가 바뀌었으므로 이전 실패 코드의 무변경 재실행과 구분된다. **260/262/270초 통합 최대차이≤3·ABBA≥+20%·폴백0·30ms 이상 끊김0·전곡 수명/설치복원은 여전히 NA**다. CPU 원본 반복0/활성화rollback 성공을 이 수용조건의 통과로 쓰지 않는다.

아래는 이전6회차 이력이다. 당시32상한 중단은 이전 지시/외부run5에 대한 판단이며 이번17:05 지시와 외부run6 결과로 대체한다.

## 자동 반복 6회차 — 창 축소 후에도 슬롯 초과, 지정된 설계 한계에서 중단

이번 게임 실행·설치·커밋·푸시·자동 장치 호출·Unity Editor 실행·설정/제어 파일 변경은 **모두 0회**다. 코드·시험 생성기·기존 JSON을 수정하지 않았고 새 계획이나 동일 시험 재실행을 요청하지 않았다. 자동 장치를 직접 정지한 것도 아니다. 코드 변경이 없어 3빌드·LoadCheck·기존 단위 검사는 반복하지 않았다.

### 외부 run5의 실제 결과

`im-autoloop-run5-evidence.txt`의 직접 증거 **79줄**을 `E:/StutterFixWork/night-results/al-im-5.log`의 **158~432행**과 순서·중복까지 대조했다. 시작 선언만 1,500자 접두부이고 나머지는 전체 줄이 일치한다. 아래는 외부의 기존 시험이며 이번 호출의 새 게임 측정은 없다. 근거: `out/imageown6/analyze.py`, `analysis.txt`, `groups.json`.

| 원시 행 | 관측값 |
|---|---|
| 377 | 소유 후보28,523·창257.5~270.25초·선택 변형10·제외93·등록93·초기 물리 텍스처 유지 |
| 378~379 | 정확크기 DXT5 배열7680×4320×4·전체 계획4배열·직접32·활성/선언36·배열296,026,112B=282.312500MiB |
| 380~384 | 등록93·주소89·누락4(직접4/배열0)·직접필요36/32·배열4/4·누락변형4·valid=False |
| 429~432 | `IMAGEOWN=FAIL reason=COLLAPSE_TEXTURE_COVERAGE`·`gpudeco live prepare` 실패·종료 요청 |

**외부 run5 배정 방식에서 필요 슬롯은 4배열+36직접=40개이고 선언은36개라 직접4슬롯이 부족하다.** 작은 DXT5 배열 대상53개+정확크기 배열4개+직접36개=등록93개이며, 주소가 있는 항목은53+4+32=89개다. 379행의 `arrays_allocated=0 copy_calls=0`은 할당 전 계획 로그다. 이후384행에는 배열4개가 보고되지만 추가 메모리의 실사용/여유VRAM을 별도 측정한 증거는 없다.

실제 live on·통합 픽셀·ABBA 결과는 원시에 없다. FPS/+20%·260/262/270초 통합 최대차이·폴백·30ms 이상 끊김·전곡 수명·Rec/설치 복원·종료 native_contexts는 **NA**다. `종료 요청`을 정상 종료/복원 성공으로 확대하지 않는다. 과거run2의 독립 소유 max1은 이번 통합의 결과가 아니다.

### 코드로 확인한 제한과 중단 판단

`GpuDecoImageTimeline.SelectWindowVariants`와 `RegisterGpuImageCollapseVariants`는 이미 소유 대상별 창 내 이벤트+창 직전 상태를 선택하고 초기 물리 텍스처를 유지한다. 실제 선택10/제외93은 이 경로가 실행됐음을 보인다. `PrepareDrawCollapse`는 작은 DXT5 tier별 배열과 가장 큰 동일크기 DXT5 그룹 하나를 배열에 배정하고 나머지는 `Take(DirectBindings)`로 직접 슬롯에 넣는다. `RememberGpuImageCollapseAddresses`가 요청 합집합과 주소를 대조해 누락을 잡고 CPU 기록 분리 전 준비를 거부했다. 이 결과는 누락 검사 오작동이나 정점 수식 실패의 근거가 아니다.

숨은 소유 항목도 `CaptureGpuImageActivation`/이미지 교체/Flush에서 `RefreshGpuImageGeometry → UpdateDrawCollapseTextureSlot`의 대상이다. 단순히 `VisibleInWindow` 밖이라는 이유로 등록을 더 버리면 기존 업로드 계약을 깨뜨릴 수 있다. 게임 `scrVisualDecoration.SetSprite` 근사 디컴파일(206~245행)과 설치 DLL ILScan을 대조해 sprite/마스크 대입·ResizeCollider·샘플링 재질·V15 Clamp 호출을 확인했다(`out/imageown6/game-visual-il.txt`). LazySprite는 물리 sprite 적용을 지연할 수 있다. 이 정적 대조만으로 초기 물리 텍스처가 불필요하다거나 픽셀 원인이 확정됐다고 판단하지 않는다.

지시서의 **“그래도 넘치면 숫자(포맷별·크기별 개수, 필요 슬롯 수)를 결과 문서에 적고 멈춰라”** 조건이 충족됐다. 외부 run5의4배열+32직접 배정 방식의 이 창 수용량 제한으로 중단한다. GPU 이미지 소유/통합 설계 전체가 불가능하다는 결론은 아니다. **소유자 요청:** 아래 수치로 형식별 은행/아틀라스 설계를 검토하고 정확한 포맷·픽셀·원래 순서·부분 업로드·끄기 복원을 보존하라. 상한 증가·누락검사 해제·누락 업로드 생략은 이번 지시의 해결책으로 수행하지 않는다. 설계 수정 없이 같은333단계 계획을 재시도하지 않는다.

### 포맷·크기별 필수 텍스처

포맷별 등록/주소/누락: DXT5 **65/64/1**, RGBA8 **20/17/3**, RGB8 **5/5/0**, DXT1 **1/1/0**, 원시 포맷표기`88` **2/2/0**. `88`의 실제 포맷 이름은 이 로그로 확정하지 않았다. 총 **44그룹·93/89/4**다. tier0이어도7680×4320 DXT5 4개는 정확크기 배열에 배정됐으므로 직접 슬롯에 중복 계산하지 않는다.

누락 ID: `-2775148`=2500×2500 DXT5, `-2775156`=1489×1489 RGBA8, `-2775164`=682×682 RGBA8, `-2775224`=7677×4320 RGBA8. 표의 주소/누락은 고유 텍스처 계수이며 장식 개수나 가시 픽셀 수가 아니다.

| 크기 | 포맷 | tier | 등록 | 주소 | 누락 |
|---|---|---:|---:|---:|---:|
| 872x872 | RGBA_DXT5_UNorm | 1024 | 1 | 1 | 0 |
| 150x1500 | R8G8B8_UNorm | 0 | 1 | 1 | 0 |
| 100x100 | RGBA_DXT5_UNorm | 512 | 1 | 1 | 0 |
| 1024x1024 | RGBA_DXT5_UNorm | 1024 | 1 | 1 | 0 |
| 1500x1500 | RGBA_DXT5_UNorm | 2048 | 1 | 1 | 0 |
| 3840x3840 | RGBA_DXT5_UNorm | 0 | 1 | 1 | 0 |
| 960x720 | RGBA_DXT5_UNorm | 1024 | 1 | 1 | 0 |
| 1920x1080 | RGBA_DXT5_UNorm | 2048 | 5 | 5 | 0 |
| 6016x4000 | R8G8B8_UNorm | 0 | 1 | 1 | 0 |
| 2048x2048 | RGBA_DXT5_UNorm | 2048 | 9 | 9 | 0 |
| 2048x2048 | R8G8B8A8_UNorm | 0 | 1 | 1 | 0 |
| 2880x1620 | RGBA_DXT5_UNorm | 0 | 1 | 1 | 0 |
| 2880x1620 | R8G8B8A8_UNorm | 0 | 1 | 1 | 0 |
| 1920x1080 | R8G8B8A8_UNorm | 0 | 1 | 1 | 0 |
| 8000x4500 | RGBA_DXT5_UNorm | 0 | 1 | 1 | 0 |
| 7680x4320 | RGBA_DXT1_UNorm | 0 | 1 | 1 | 0 |
| 7677x4320 | R8G8B8A8_UNorm | 0 | 2 | 1 | 1 |
| 7680x4320 | RGBA_DXT5_UNorm | 0 | 4 | 4 | 0 |
| 171x171 | R8G8B8A8_UNorm | 0 | 4 | 4 | 0 |
| 150x150 | R8G8B8A8_UNorm | 0 | 3 | 3 | 0 |
| 150x79 | R8G8B8A8_UNorm | 0 | 1 | 1 | 0 |
| 2500x2500 | RGBA_DXT5_UNorm | 0 | 3 | 2 | 1 |
| 1920x1920 | RGBA_DXT5_UNorm | 2048 | 1 | 1 | 0 |
| 3113x2428 | R8G8B8A8_UNorm | 0 | 1 | 1 | 0 |
| 2000x2000 | RGBA_DXT5_UNorm | 2048 | 2 | 2 | 0 |
| 2692x2833 | 88 | 0 | 1 | 1 | 0 |
| 1080x1080 | RGBA_DXT5_UNorm | 2048 | 16 | 16 | 0 |
| 800x800 | RGBA_DXT5_UNorm | 1024 | 1 | 1 | 0 |
| 5001x3334 | R8G8B8A8_UNorm | 0 | 1 | 1 | 0 |
| 682x682 | R8G8B8A8_UNorm | 0 | 2 | 1 | 1 |
| 14024x3506 | R8G8B8_UNorm | 0 | 1 | 1 | 0 |
| 512x512 | RGBA_DXT5_UNorm | 512 | 9 | 9 | 0 |
| 300x300 | RGBA_DXT5_UNorm | 512 | 1 | 1 | 0 |
| 3543x1772 | R8G8B8_UNorm | 0 | 1 | 1 | 0 |
| 3332x2665 | R8G8B8A8_UNorm | 0 | 1 | 1 | 0 |
| 1200x1200 | RGBA_DXT5_UNorm | 2048 | 1 | 1 | 0 |
| 5000x5000 | RGBA_DXT5_UNorm | 0 | 2 | 2 | 0 |
| 5000x3333 | 88 | 0 | 1 | 1 | 0 |
| 1000x2000 | RGBA_DXT5_UNorm | 2048 | 1 | 1 | 0 |
| 3222x1891 | R8G8B8A8_UNorm | 0 | 1 | 1 | 0 |
| 1280x1280 | RGBA_DXT5_UNorm | 2048 | 1 | 1 | 0 |
| 1000x1000 | RGBA_DXT5_UNorm | 1024 | 1 | 1 | 0 |
| 2000x1055 | R8G8B8_UNorm | 0 | 1 | 1 | 0 |
| 1489x1489 | R8G8B8A8_UNorm | 0 | 1 | 0 | 1 |

### 동시 작업과 검증 범위

진행 중 다른 작업자의 `DrawCollapsePolicy.cs`에 `DirectBindings=48`, `GpuDecoBatch.shader` 및 별도 Editor 로그 변경이 관측됐다. **이 작업이 만든 변경이 아니며 수정·복원·채택하지 않았다.** 외부run5는명시적으로32직접/4배열/선언36인 이전 실행이다. 현재 소스48을 run5의 시험 상한으로 바꾸거나 새 번들/수용량/성능 성공으로 간주하지 않는다. 다른 작업자의 변경을 되돌리면 충돌하므로 보존하며, 소유자가 이 지시의32상한 금지와 별도로 정합성을 확인해야 한다.

검증은 원시79줄 순서·필수계수·44그룹 합산·결과 부재 확인, 설치 DLL 읽기 전용 ILScan, 이미지 소스/허용helper/생성기/기존계획15개 SHA256 보존, 문서 정합성·10줄 요약·`git diff --check`다. 증거 추출 스크립트의 첫 실행은 게임 DLL 폴더명을 잘못 써 파일 존재 검사에서 멈췄고 csproj의 실제 경로로 바로잡았다. 게임/설치 쓰기는 없었다. 기존 빌드 성공은5회차 이력으로만 남기며 이번 성공으로 재표기하지 않는다.

아래는 이전 회차 이력이며 `WINDOW_COVERAGE_NOT_RUN`은 외부run5 수신 전 상태다.

## 자동 반복 5회차 — 외부 run4 분석, 창 범위 이미지 등록

이번 게임 실행·설치·커밋·푸시·자동 장치 호출·Unity Editor 실행·설정/제어 파일 변경은 **모두 0회**다. 아래의 새 계획은 생성만 했으며 실제 슬롯 수·통합 픽셀·FPS·끊김은 NA다. 게임 실행 금지는 인계의 재시험 지시보다 우선한다.

### 외부 증거로 확정된 원인

`im-autoloop-run4-evidence.txt`의 직접 **37줄**을 `E:/StutterFixWork/night-results/al-im-4.log`에 순서·중복을 보존해 대조했다(시작 선언은 1,500자 접두부). 원시379행은 배열3·318.75MiB·직접32·활성35/선언36, 380~387행의 첫8개 누락은 모두3840×2160 RGBA_DXT5_UNorm이다. 388행은 **등록185·주소149·누락36(직접36/배열0)·직접필요68/상한32·배열3/4·누락변형36**이다. 390/392행에서 live prepare가 실패했으며 live on·통합 픽셀·ABBA에는 도달하지 않았다. 새 게임 성공이나 복원 결과를 이 로그에서 추론하지 않는다.

`RegisterGpuImageCollapseVariants`가 곡 전체의 ReadyQuad를 등록하고 Remember와 collapsecheck도 전체 변형을 검사한 것이 창 밖 텍스처를 요구하는 코드 원인이다. **곡 전체 등록 집합의 직접 슬롯 부족은 이번 로그로 확인되지만, 창으로 좁힌 집합도 초과한다는 증거는 아직 없다.** 상한32를 늘리거나 누락 검사를 끄지 않았다. 과거 run2의 독립 소유 max1/1/1은 재측정하지 않았고 정점/pose 코드는 유지했다.

### 수정한 범위와 시간 계약

- `GpuDecoImageTimeline.SelectWindowVariants(start,end,owned)`가 실제 GPU 소유 대상별로 **창 시작 전 마지막 이미지 이벤트**와 **[257.5,270.25] 양 끝을 포함한 이벤트**의 선택자를 모은다. 같은 시각의 원래 이벤트 순서·중복 대상은 보존하고, 창 밖에서만 쓰이는 이전/미래 변형·소유 대상 없는 변형은 제외한다. 초기 물리 스프라이트 텍스처는 renderer가 이미 등록한 집합을 그대로 보존한다.
- `GpuDecoImagePlayback.cs`는 그 선택으로만 변형을 추가하며 `GPUIMG_DIAG mode=collapsewindow`에 선택/제외 수와 창을 기록한다. 준비 coverage와 정지 collapsecheck도 같은 선택을 검사한다. 초기 물리 텍스처 합집합·현재 항목 주소 검사·미래 필수 주소 검사는 유지한다.
- 숨은 소유 대상도 activation/Flush의 슬롯 갱신 대상이므로 `VisibleInWindow`로 추가 제거하지 않는다. 그 함수는 renderer의 **그리기 포함** 판정에 이미 쓰이며 슬롯 업로드 대상과 같지 않다. 이 작업에서 renderer의 포함/장벽·정렬·폴백 정책은 바꾸지 않았다.
- coverage는 누락0·실제 직접주소 수≤32·배열≤4여야 통과한다. 이미 정확한 크기 배열에 배정된 큰 텍스처는 직접 슬롯으로 잘못 세지 않는다. 누락 때 크기/포맷/tier별 고유 텍스처 수와 주소 수를 `collapsecoverage_group`에 기록한다. 매 프레임 새 전수 순회는 없다.
- 기존 ABBA는 256초에 GPU 소유권을 켰다. 창 밖 중간 변형 업로드를 피하려고 **새 accept의 A/B 모두257.5초부터 활성화**한다. draw 통합 준비258.5초·측정260~270초와 픽셀 선행 관문은 유지한다. 기존 명시 diag/diag1/abba는 이력 보존용이며 새 창 범위 시험에는 사용하지 않는다.

이미지 tape는 게임의 최종 정렬 목록을 복사하며 Scheduled는 `startTime-startEffectOffset`이다. `scnGame.PrepVfx` 근사 디컴파일의 동일 기준 정렬과 `scrVfxPlus.Update`의 시각 검사/StartEffect, `scrVisualDecoration.SetSprite`의 sprite·ResizeCollider·wrap 갱신을 확인했다. 설치 DLL도 ILScan으로 호출 관계를 대조했다(`out/imageown5/game-{scheduler,visual}-il.txt`). 임의 재정렬 없이 동시각 순서를 보존하며, 비단조 tape면 준비 실패한다. ILScan 호출 목록은 정지 픽셀 동등성 증명이 아니다.

### 오프라인 검증

| 검사 | 결과 | 근거 |
|---|---|---|
| Release DEV/Player/PlayerAuto 격리 빌드 | 오류0/0/0·경고48/36/40 | out/imageown5/build-*.txt |
| LoadCheck | FieldRef279/229/281곳·틀림0/0/0·일반 Player 연구 제외 | loadcheck-*.txt |
| 실제 timeline + 등록/주소/부분업로드3메서드 추출 | **227검사 통과** | check-window.py, window-check.txt |
| 생성기 | 8검사 통과·과거 명시 출력12사례 전체 dict 일치 | generator-tests.txt, check-evidence.py |
| 보호 파일 | 소유자/공용helper/셰이더/번들/기존JSON/설정14개 SHA256 유지 | preserved-before.json, preserved-after.json |

227검사는 대상별 진입 상태·동시각 마지막 이벤트·양끝 경계·타깃 없는 이벤트·중복·비단조 거부·200개 재생 참조 사례, 창 밖 변형 제외 후 정상 coverage, 필수 미래 누락의 준비 실패, 32직접/4배열 상한, 기존32B 부분 업로드와 point/wrap 가드를 확인한다. 가짜 Unity 자원을 쓰므로 **실제 맵의 registered/missing/direct_required/arrays_used나 픽셀·FPS 결과가 아니다.** 기존 알파 큐는 변경하지 않아 4,495검사를 반복하지 않았다.

### 생성만 한 새 계획과 다음 중단 조건

`tools/optimization/out/imageown-abba3-window-steps.json`: **333단계**, tag=`imageown-260-270-abba3-window`, SHA256=`41b790f93035f51623bdd6aaee43d4643d5468235ca27065a98291a9f592e2bd`. 기존 abba2/abba1/diag JSON은 덮어쓰지 않았다. 새 기본 생성기는 이 계획을 선택한다. 새 managed DLL과 이미지·collapse 지원이 확인된 번들이 필요하다. 이번에 번들을 빌드·설치·재로드하지 않았으며 과거 번들 보류를 이 작업에서 해제하지 않는다.

외부 실행자는 먼저 새 `collapsewindow` 및 `collapsecoverage` 수치를 확인한다. **missing=0, direct_required≤32, arrays_used≤4** 뒤에만 통합 픽셀260/262/270·폴백0을 검사하고, 세 픽셀 관문을 통과한 뒤 ABBA(+20%·30ms 이상0)로 진행한다. 여전히 넘치면 그룹별 수치·필요 슬롯 수를 결과에 옮기고 **같은 코드/계획 재시도를 중단**, renderer/policy/은행·아틀라스 설계는 Claude에게 인계한다. 상한 증가·재압축·누락 업로드 생략·가드 해제는 하지 않는다.

선택은 논리 이벤트의 상태 범위다. LazySprite 때문에 활성화 시 물리 스프라이트가 오래된 변형을 유지하는지, 수명 중 자산ID가 달라지는지, 재질/정렬이 바뀌는지는 이번 오프라인 검사로 증명하지 못했다. 필수 등록에 없는 실제 업로드는 기존 `collapse_runtime_missing`/복구로 실패하며 누락을 무시하지 않는다. **창 축소 후 실제 수용량·물리 상태·통합 픽셀/성능이 아직 미측정이므로 IMAGEOWN=FAIL**이다. 통합 설계 전체의 불가능 판정은 하지 않는다.

아래는 이전 회차의 이력이다.

## 자동 반복 4회차 — 외부 run3 분석, 준비 단계의 이미지 주소 검증 보강

이번 게임 실행·설치·커밋·푸시·자동 장치 호출·Unity Editor 실행·설정/제어 파일 변경은 **모두 0회**다. 새 게임 실측과 새 시험 계획 생성은 없다. **이미지 통합은 아직 성공하지 않았다.** 동일한 abba2 계획을 다시 요청하지 않으며, 정점 수식·pose 진단도 반복하지 않는다.

### 실제 중단 원인과 확인 범위

`docs/handoff/im-autoloop-run3-evidence.txt`의 직접 증거 **35줄**을 `E:/StutterFixWork/night-results/al-im-3.log`에 순서·중복을 보존해 대조했다. 시작 줄은 원시 선언의 1,500자 접두부다. 확인 스크립트와 출력은 `out/imageown4/check-evidence.py`, `evidence-and-preservation.txt`에 있다.

| 원시 행 | 확인된 값 |
|---|---|
| 378 | 배열3개·복사117회에 해당하는 준비, 직접 텍스처32개, 활성 슬롯35/선언36, 추가 배열318.75MiB(334,233,600B) |
| 382 | 준비 구간82·포함14,595·texture_fallback_items=0 |
| 474~475 | 소유 분류GPU28,523·CPU fallback0 뒤 활성화 실패, CPU Rec11,821/11,821 복원·continuity=RESTORED |
| 477,481 | `gpudeco live on`: `image_texture_not_registered_in_collapse` |
| 482 | 종료 native_contexts=0 |

260초 원본 O/O2 반복은 차이0이지만 GPU G와 통합 B 캡처 전에 중단됐다. 통합 픽셀·FPS·30ms 이상 끊김·설치 복원·전곡 수명은 **NA**다. 3회차 문서에 있는 독립 이미지 최대차이1은 **이전 run2의 한정된 정지 경계 결과**이며 이번 통합 성공으로 해석하지 않는다.

실패 경로는 `StartGpuLive → CaptureGpuImageActivation → RefreshGpuImageGeometry → UpdateDrawCollapseTextureSlot`이다. Refresh는 실제 sprite/texture·재질·renderer 대응을 검사한 뒤 현재 이미지를 넘긴다. helper에서 현재 texture ID의 주소를 얻지 못해 활성화가 롤백됐다. 이는 정점 산술이나 FastMove 보호 복귀를 원인으로 고칠 근거가 아니다.

준비 코드에는 **현재 항목만 검사하는 구멍**이 있다. `RegisterGpuImageCollapseVariants(textures)`는 전체 변형을 요청 사전에 추가하지만, renderer는 작은 DXT5 그룹을 최대4배열에 배정하고 나머지는 `Take(32)`로 직접 텍스처를 선택한다. 이후 `gpuCollapseTextureFallback`은 **준비 당시 Data.Items의 TextureId만** 검사한다. 아직 어떤 현재 항목에도 쓰이지 않는 미래 이미지 변형이 주소에서 빠져도 폴백0·준비 성공이 가능하다. seek 뒤 그 이미지를 적용하면 비로소 helper가 실패한다.

다만 기존 로그에는 실패한 item/texture ID·총 요청 텍스처 수·누락 종류가 없다. 직접 슬롯32개가 꽉 찼다는 사실만으로 **그 실패 텍스처가 33번째 직접 텍스처였다고 확정하지 않는다**. 배열 그룹 초과 또는 등록 후 자산 ID 변화와도 구분할 수 없다. 이 메커니즘의 재현 검사는 아래와 같고, 실제 누락 원인의 세부 귀속은 미확정이다.

### 수정한 코드와 오프라인 검증

- `GpuDecoImagePlayback.cs`: 등록된 **현재+미래 변형 전체 합집합**을 보존한다. `RememberGpuImageCollapseAddresses`에서 실제 주소 배정 결과와 대조해 고유 누락 수·직접/배열 분류·직접 요청/상한·배열 사용/상한·누락 변형 엔트리 수를 `GPUIMG_DIAG mode=collapsecoverage`로 기록한다. 누락 상세는 최대8개다. 누락 시 `IMAGEOWN=FAIL reason=COLLAPSE_TEXTURE_COVERAGE`를 남기고 **live prepare 안에서 CPU 기록을 떼기 전에 중단**한다. 실패한 주소는 게시하지 않는다. 매 프레임 검사는 추가하지 않았다.
- 허용된 `GpuDecoDrawCollapse.cs`의 texture helper에만 실패 로그5줄을 추가했다. 런타임 누락이면 item/texture ID·치수·format·tier와 `registered=True/False`를 남겨 등록됐지만 주소 없는 경우와 등록 목록에도 없는 자산을 구분한다. 기존 주소·mip/wrap·재질·범위 검사는 유지한다.
- 준비 시 texture wrap 변경, 이미지 setter/Flush 순서 변경, 슬롯 상한 완화, 임의 재압축/리샘플링, 누락 업로드 생략, 정렬·폴백 보호 해제는 하지 않았다.

| 검사 | 이번 결과 | 근거 |
|---|---|---|
| DEV/Player/PlayerAuto 격리 빌드 | 오류0/0/0·경고48/36/40 | out/imageown4/build-*.txt |
| LoadCheck | FieldRef279/229/281곳·틀림0, 일반 Player 연구 제외 | out/imageown4/loadcheck-*.txt |
| 실제 수정 메서드3개 추출 검사 | **20검사 통과**, Unity 실행 없음 | out/imageown4/check-coverage.py, coverage-check.txt |
| 기존 생성기 | 8검사 통과, 생성기 자체·기존JSON 유지 | out/imageown4/generator-tests.txt |
| 증거/소유권 | 외부35줄 일치·보호14개SHA256 유지, 공용 파일은 허용 helper만 변경 | out/imageown4/evidence-and-preservation.txt |

추출 검사는 실제 Register/Remember/Update 메서드를 가짜 Unity 자원과 실행한다. 현재32개가 모두 주소를 받았어도 미래33번째가 빠지면 준비 실패하는 회귀 사례, 직접/배열 누락 분류, 중복 변형과 고유 텍스처 계수 구분, 주소 게시/업로드 금지, 32B 부분 갱신·point bit·wrap 보호, 런타임 자산 ID 변화 구분을 검증했다. **실제 게임의 누락 수를33 또는1로 측정한 시험은 아니다.**

생성기 검사 첫 PowerShell 래퍼는 unittest의 정상 stderr를 오류로 처리했다. 빌드/LoadCheck는 세 종류 모두 먼저 통과했고, 래퍼를 Python 캡처로 수정한 뒤 생성기8검사의 exit0을 확인했다. 불필요한 빌드 반복은 하지 않았다.

게임 `scrVisualDecoration.SetSprite`와 `TextureManager` 근사 디컴파일을 읽고 **설치 DLL의 ILScan을 새로 대조**했다(`out/imageown4/game-{visual,texture}-il.txt`). SetSprite의 sprite/마스크·재질 샘플링·V15 Clamp와 TextureManager의 이미지 로드 호출은 확인되지만, 정적 호출 관계만으로 이번 누락 texture ID를 복원할 수 없다.

### 남은 설계 제약과 담당자 인계 — 같은 시험 중단

**이번 수정은 누락을 조기에 검출하는 준비 계약 수정이며, 텍스처를 실제로 더 수용하는 수정은 아니다.** 현재 방식은4배열+32직접 슬롯이다. 전체 필수 합집합이 실제 주소를 받지 못하면 폴백0 조건으로 진행할 수 없다. 정확한 누락 텍스처와 세부 원인은 기존 증거로 미확정이므로 통합 방식 전체가 불가능하다는 판정도 하지 않는다.

등록/배정은 Claude 소유 `GpuDecoDrawCollapseRenderer.cs`와 `DrawCollapsePolicy.cs`, 번들 셰이더가 담당한다. 해당 파일은 변경하지 않았다. **소유자 요청:** 새 coverage가 보고하는 전체 필수 합집합을 현재와 미래 이미지까지 수용하도록 배정/형식별 은행 설계를 검토하고, `registered=False` 런타임 실패면 자산 재사용/수명도 확인하라. 32상한 증가나 누락 업데이트 생략으로 폴백0을 꾸미지 말고, 텍스처 정확도·정렬·부분 갱신·끄기 복원을 유지해야 한다. 이미지 재질 변경에 대한 기존 Replan 검사도 담당자 범위다.

새 ABBA/진단 계획을 만들지 않았고 기본 생성기·기존 abba2/diag JSON·tag는 그대로다. **renderer의 주소 배정 또는 자산 수명 원인 수정 전 기존333단계의 자동 재시도는 중단해야 한다.** 자동 장치를 직접 조작하거나 정지한 것은 아니다. 준비 단계 수정만으로 `IMAGEOWN=PASS`를 만들지 않는다. 마스터의 번들 관련 과거 보류를 이 작업에서 해제하지 않는다. 아래는 이전 회차의 이력이다.

## 자동 반복 3회차 — 외부 run2 분석과 통합 선행 검사

이번 게임 실행·설치·커밋·푸시·자동 장치 호출·Unity Editor 실행·설정/제어 파일 변경은 **0회**다. 외부 측정은 기존 run2이며, 이번 호출의 새 게임 실측은 없다. **독립 이미지 GPU 소유의 정지 픽셀은 허용치 이내지만 draw 통합과 +20% 성능 조건은 미증명**이다.

### 외부 증거와 두 가설의 결론

`im-autoloop-run2-evidence.txt` 직접 증거 **78줄**을 원시 `E:/StutterFixWork/night-results/al-im-2.log`와 순서·중복까지 대조했다. 시작 선언만 1,500자 접두부이고 나머지는 전체 줄이 일치한다. 근거는 `out/imageown3/check-evidence.py`, `evidence-and-compatibility.txt`다.

| 곡 시각 / 원시 행 | GPU 수식 changed/max | UpdatePosition changed/max | CPU 정점 주입 changed/max |
|---|---:|---:|---:|
| 260.019671 / 642 | 384 / 1 | 384 / 1 | 489 / 1 |
| 262.014330 / 925 | 172 / 1 | 172 / 1 | 280 / 1 |
| 270.025012 / 1204 | 61 / 1 | 61 / 1 | 258 / 1 |

세 시점 모두 GPU·정점 주입·pose 반복과 G 복원이 changed=0이고 controls=True다. 별도 CPU O/O2 대조도 계획의 assertzero를 통과했다. 실제 환경은 **3440×1440@164.999**이고 활성화 6회 모두 GPU28,523·CPU fallback0이다. 두 pose 모드별 UpdatePosition 호출은 0/4,976, CPU 정점 주입은 가시 이미지286/354/340개·최대 world delta0.00013647876이다. 여섯 live off 모두 Rec 복원이 각 기대값과 일치(11,821/11,186/12,301, 각 두 번), 종료 native_contexts=0이다.

- `UpdatePosition()` 호출은 이 표본의 픽셀 수/최대 차이를 개선하지 않았다. 매 이미지 갱신의 실제 Transform 쓰기를 정상 경로에 추가할 근거가 없어 기본 off를 유지했다.
- CPU 정점 주입도 차이를 0으로 만들지 않았고 오히려 changed가 많다. **정점 수식이 유일한 원인이라는 가설은 지지되지 않는다.** 주입은 이미지 정점만 교체하며 비이미지·합성 화면 차이가 남을 수 있어, 정점 문제가 전혀 없거나 정렬/텍스처 중 하나가 확정 원인이라는 역추론도 하지 않는다.
- 최대 차이 **1≤허용3**이므로 독립 소유의 이 세 정지 경계에는 산술 수식 변경이 필요하지 않다. 이전 2560×1440@120 표본의 최대10~25와 개선률로 비교하지 않는다. 잔여1의 원인은 UNCONFIRMED이고 연속 재생·전곡·draw 통합 픽셀은 아직 미검증이다. 같은 diag1/diag2 반복은 요청하지 않는다.

게임 `scrVisualDecoration.SetSprite` / `scrDecoration.UpdatePosition` 근사 디컴파일을 읽고 설치 DLL을 ILScan으로 재확인했다. SetSprite는 스프라이트/마스크·ResizeCollider·샘플링 재질·V15 Clamp를 바꾸며 UpdatePosition을 호출하지 않는다. UpdatePosition은 위치/시차·회전·크기를 갱신한다. `out/imageown3/game-{visual,decoration}-il.txt`는 호출 관계 근거이며 잔여 픽셀 원인 증명이 아니다.

### 코드와 새 계획

현재 이미지 통합 연결은 이미 존재한다: 준비 단계 변형 등록·주소 보존, 23,547/28,523 소유 입구, 이미지 갱신 때 `_CollapseTexture` 32바이트 부분 업로드, 기존 알파 더티 큐 기반 대기 감시다. 이 코드를 다시 구현하지 않았다. 기존 큐4,495검사는 큐 변경이 없어 반복하지 않았다.

`GpuDecoImageGeometryFixture.cs`에 정지 전용 `images collapsecheck`를 추가했다. 현재 텍스처 누락, 주소가 있는데 제외된 항목, 그중 이미지 항목, 변형 엔트리 주소 누락, 전체 폴백·구간 수를 `GPUIMG_DIAG`로 기록한다. 제외 계수는 `GpuDecoDrawCollapseRenderer.cs`의 정렬 보호에서도 증가하므로 **주소 등록된 제외를 텍스처 용량 부족으로 단정하지 않는다**. 폴백/누락이 있으면 `IMAGEOWN=FAIL reason=COLLAPSE_PREFLIGHT_FAILED`를 기록하고 ABBA 전에 중단한다. 이 검사는 정지 단계에서만 전수 순회하며 측정 창에 새 스캔을 넣지 않는다.

새 `images gate preflight on`은 세 통합 픽셀 관문 완료 전 ABBA begin을 거부한다. `images pixelchecked`는 기존 원본 O/B 최대3·G 반복/복원 검사에 **O/O2 및 B/BN 반복0·통합 폴백0**을 더한다. 실패는 `INTEGRATED_PIXEL_PREFLIGHT_FAILED`로 기록하고 성능 측정 진입을 막는다. 기존 pixel 명령에도 폴백0 판정을 추가했다. 원본·정렬·오류 복구 가드는 완화하지 않았다.

새 기본 `--mode accept`: **`imageown-260-270-abba2-preflight`, 333단계**다. 260/262/270초 통합 픽셀·누락/폴백을 먼저 검사한 뒤 이미지 off/on/on/off ABBA를 측정한다. 캡처는 최대7장(O/O2/G/N/B/BN/G2)이며 PNG를 판정 전에 저장한다. 성능 창에는 정지·캡처·readback이 없다. 정상 경로는 refreshpose off, CPU 정점 주입도 없다. A는 GPU23,547+통합, B는 GPU28,523+통합이며 수용은 B/A≥1.20·최대픽셀3·폴백0·30ms 이상0이다. 202FPS는 새 A 실측이 아니며 새 수치로 대체해야 한다.

`tools/optimization/out/imageown-abba2-steps.json`은 **생성만** 했다. 기존 명시 `diag`/`diag1`/`abba` 전체 출력12사례와 기존 diag1/diag2/abba1 JSON 해시를 보존했다. 기본 생성기가 새 계획을 선택하므로 완료 진단 반복을 피한다.

### 오프라인 검증과 인계 조건

| 검사 | 결과 | out/imageown3 근거 |
|---|---|---|
| DEV/Player/PlayerAuto 빌드 | 오류0/0/0·경고48/36/40 | build-dev.txt, build-player.txt, build-playerauto.txt |
| LoadCheck | FieldRef279/229/281곳·틀림0/0/0·일반 Player 연구 제외 | loadcheck-*.txt |
| 생성기 | 소유권·수명·실제8장 상한·7장 의존/저장·선행 관문·연속 측정 창8검사 통과 | generator-tests.txt |
| 증거/호환 | 외부78줄 순서 일치·과거 명시 출력12사례 동일 | evidence-and-compatibility.txt |
| 소유자/기존 자산 | renderer/policy/Dormancy/TightLate/FastMove/export/헬퍼·기존JSON·native·bundle SHA256 전후 동일 | preserved-before.json, preserved-after.json |

빌드는 `out/imageown3/build/{dev,player,playerauto}`와 별도 obj에 격리했다. 이 검사들은 새 런타임 명령의 실제 Unity 동작·통합 픽셀·FPS·전곡 수명 증명이 아니다. 새 계획 실행에는 이번 managed 수정과 **EvaluateImageLive/EvaluateCollapse/GpuDecoBatch를 모두 지원하는 번들**이 필요하다. 이번 bundle/manifest/Editor 근거는 변경하지 않았고 새 Editor 빌드도 하지 않았다. 현재 파일 SHA256은 아래와 같으며, 외부 run2의 독립 이미지 시험 성공이 새 draw 통합 지원까지 증명하지 않는다. 마스터의 NEED_EDITOR_BUILD를 이 작업에서 해제하지 않는다.

- 현재 bundle: `4ecdbe6c69a33defa422dbcd944ac18f96e45d0786e705f1001b7d5a07440ad4`.
- 새 abba2 JSON: `d95a82ca60b5360597794c1257e0c584ec6d54ebf1dc3fa0fe6bfa39f55f589e`.

**Claude 소유자 요청:** 선행 검사에서 registered_but_excluded/image_excluded가 나오면 renderer의 정렬 보호·구간 준비를 확인해야 한다. 현 renderer는 정렬 제외를 gpuCollapseTextureFallback에 함께 넣고 Replan에서 재질 불변을 검사한다. 이미지 교체 후 재질/정렬·숨은 장벽의 기상도 외부 통합 시험으로 확인해야 하며 보호 제거로 폴백0을 만들면 안 된다. 본 작업은 renderer/policy/export 등을 수정하지 않았다. 현재 결과만으로 병합 방식 자체의 불가능을 확정하지 않는다. 선행 관문 실패 시 같은 계획 재시도 대신 이 수치와 원인을 담당자에게 넘긴다.

FPS·통합 픽셀 최대·통합 폴백·30ms 이상·설치 복원·전곡 수명은 NA다. 새 계획/명령을 실행하거나 자동 장치/설정·제어 파일을 조작하지 않았다. 아래 2회차의 DIAG2_NOT_RUN과 ABBA 선행 조건 설명은 외부 run2 수신 전 이력이다.

## 자동 반복 2회차 — 외부 run1 분석과 캡처 상한 수정

이번 호출의 게임 실행·설치·커밋·푸시·자동 장치 호출·Unity Editor 실행·설정/제어 변경은 모두 **0회**다. 외부 run1을 읽었으며 새 실측은 없다. 픽셀 오차의 원인은 아직 미확정이고 +20% 수용 기준도 미증명이다.

### 확인된 실패 원인

- `im-autoloop-run1-evidence.txt`의 직접 증거42줄을 `E:/StutterFixWork/night-results/al-im-1.log`에 대조했다. 시작 줄은1,500자 접두부이며 나머지는 원문 일치다. `out/imageown2/check-evidence.py`, `evidence-and-compatibility.txt`에 보존했다.
- 원시578행까지 O,O2,G,N,D,DN,G2,U **8장**을 채웠고579~580행에서 아홉 번째 UN을 거부했다. `FrameCompare.cs`는 MaxShots=8이며 새 이름만 상한 검사하고 기존 이름 대입은 허용한다. 기존156단계 계획은 첫 시점부터 끝낼 수 없었다.
- 453/458행 O/O2,493/498행 G/N,529/534행 D/DN,554행 G/G2는 모두 changed=0·maxRGBA=0,0,0,0이다. 이는 반복/복원 대조이며 **O/G·O/D·O/U의 원본 대비 차이0을 뜻하지 않는다**. 그 세 비교 및 최종 GPUIMG_DIAG second=는 실행되지 않았다.262/270초도 도달하지 못했다.
- 실제 환경3440×1440@164.999, 활성화 두 번 모두 GPU28,523·CPU fallback0, refreshpose off/on 호출0/4,976이다. CPU 정점 주입은 이미지/활성 renderer286/286·최대 world delta0.00013647876이다. 월드 오차를 픽셀 오차로 환산하지 않는다.
- 580행 캡처 실패→583행 pause 복원→584행 종료 선언→585행 FastMove 원본 보호→588행 CPU 복귀→590행 native_contexts=0 순서다. 첫 live off 복원 Rec11,821/11,821, 종료 복귀0/0이다. **보호 복귀가 먼저 진단을 망친 것이 아니다.** 종료 중 해당 원본 효과가 호출된 스택은 없으며 가드를 수정하지 않았다.

### 수정한 새 접근

기본 diag는 `imageown-260-270-diag2-bounded` **165단계**다. CPU O/O2를 assertzero·PNG 저장한 뒤 마지막 pose 반복 캡처가 기존 이름 **O2를 덮어쓴다**. 최대8장으로 두 가설을 모두 비교하며 원본 O는 유지한다. 마지막 O2 PNG는 U의 반복이므로 CPU 반복으로 해석하지 않는다. 최초 CPU 반복 수치는 덮어쓰기 전 로그에 남는다.

O/G·O/D·O/U는 각 후보의 반복 대조 직후 framecmp diff로 기록하고 PNG도 다음 단계 전에 저장한다. 다음 단계 실패에도 완료한 비교 근거가 남는다. `GpuDecoImageGeometryFixture.cs`의 새 `images diag pixel bounded`는 O2를 pose 반복으로 읽고 GPU/CPU정점/pose 반복 및 G 복원을 함께 GPUIMG_DIAG로 출력한다. cause=UNCONFIRMED를 유지한다.

예전 `--mode diag1`과 명시적 `--mode abba`는 이전 생성기와 **전체 dict8사례 동일**하다. 기존 diag1/abba1 JSON을 덮어쓰지 않았다. diag1은 실패 이력 보존용이며 반복 실행 대상이 아니다. 기존 shader·정점 수식·원본 보호 경로를 추측으로 수정하지 않았다.

### 게임 코드 대조와 통합 현황

근사 디컴파일 scrVisualDecoration.SetSprite와 scrDecoration.UpdatePosition을 읽고 설치 DLL을 tools/ilscan으로 대조했다. SetSprite는 SpriteRenderer/마스크·ResizeCollider·샘플링 재질·V15 wrapMode를 바꾸고 UpdatePosition 호출은 없다. UpdatePosition은 pivot 위치·parallax·회전·크기 경로를 실행한다. `out/imageown2/game-visual-il.txt`, `game-decoration-il.txt`에 보존했다. 호출 관계만으로 stale Transform이나 정점 산술을 실제 픽셀 원인으로 확정할 수 없다.

현재 `GpuDecoDrawCollapseRenderer.cs`에는 RegisterGpuImageCollapseVariants(textures)와 RememberGpuImageCollapseAddresses(addresses)가 **이미 연결돼 있다**.1회차의 연결 대기는 과거 상태다. renderer/policy/export/Dormancy/TightLate/FastMove 및 기존 텍스처 헬퍼·알파 큐는 이번에 수정하지 않았다. 소유자 파일 SHA256 전후 일치다. 연결 존재는 실제 이미지 무폴백·정렬·픽셀 통과 증명이 아니다.

### 오프라인 검증과 외부 인계

| 검사 | 결과 | out/imageown2 근거 |
|---|---|---|
| DEV/Player/PlayerAuto 빌드 | 오류0/0/0·경고48/36/40 | build-dev.txt,build-player.txt,build-playerauto.txt |
| LoadCheck | FieldRef279/229/281곳·틀림0/0/0·일반 Player 연구 제외 | loadcheck-*.txt |
| 생성기 | 실제 캡처 상한·이름 존재·덮어쓰기 전 대조/저장·소유권·측정 창7검사 통과 | generator-tests.txt |
| 과거 출력/증거 | 전체 출력8사례 호환·직접 증거42줄 일치 | evidence-and-compatibility.txt |

기존 큐4,495검사는 해당 코드 변경이 없어 반복하지 않았다. 빌드는 out/imageown2/build와 obj에 격리했다. 이 검증은 Mono 게임 픽셀·FPS 증명이 아니다.

새 `tools/optimization/out/imageown-diag2-steps.json`은 **생성만** 했다. 외부 실행자는 새 managed DLL과 EvaluateImageLive 지원 번들을 확인해야 한다. 이번 번들 SHA256은 `4ecdb...40ad4`(전체 값은 out/imageown2/source-after-build.txt)이며 새 번들의 실제 실행 성공은 별도 미검증이다. 번들/manifest/Editor 근거에는 쓰지 않았다. 작업 중 타 작업자가 바꾼 bundle 로그·manifest·editor-image3.txt를 유지했다.

diag2로 세 시점 원본 대비 수치를 얻어 원인별 수정 후 maxdiff<=3을 확인해야 한다. 그 전에는 abba1을 실행 대상으로 승격하지 않는다. FPS·폴백·30ms 이상 끊김·전곡 수명·설치 복원은 NA다. **확정 한계는 기존9장 진단의 상한 위반이며 이미지 GPU 소유 방식 자체의 불가능이 아니다.** 같은 diag1 반복은 중단한다.

JSON SHA256은 다음과 같다.

- 기존 diag1: `4b052dbbe5502d8078c2b5f01a282b0b103d931a66e222ab6c86f19bc26297c8`
- 기존 abba1: `53638954fce0fdcee8814c2c5223314d56bc076fe3ac21f4258e01b4dcea2c69`
- 새 diag2: `6aed5565a7d6101a260385289a0af40ce35f466f1859db7a7900f6ae3acdc6e4`

아래는1회차 과거 기록이다.

# 이미지 소유 작업 결과 — 2026-10-25

게임 실행·설치·커밋·푸시·자동 장치 호출·설정/제어 파일 변경은 이번 호출에서 모두 0회다. 사용자 금지를 유지해 실제 픽셀·FPS·폴백·끊김은 측정하지 않았다. 원인 확정이나 성능 성공으로 판정하지 않는다.

## 지시 순서별 상태

| 순서 | 이번에 완료한 준비 | 남은 근거/작업 |
|---|---|---|
| 1(a) | `images refreshpose on/off`와 `GPUIMG_DIAG` 로그. `RefreshGpuImageGeometry` 시작에서 선택적으로 `d.UpdatePosition()` 1회 | 실제 260/262/270초 정지 픽셀 비교 미실행 |
| 1(b) | `images locate cpuvertices on/off`. 신선한 정지 활성화에서 CPU `TransformPoint` 위치만 GPU VB에 주입 | 실제 원본/GPU/CPU 정점 주입 픽셀 미실행 |
| 2 | 원인별 비교·잡음·복원 검사 계획 준비 | 원인 미확정. 수식/정렬을 추측으로 수정하지 않았고 maxdiff<=3 미증명 |
| 3 | 소유 수 입구 23547/28523, 32바이트 텍스처 부분 갱신 헬퍼·호출, 알파 더티 감시 구현 | Claude 소유 `PrepareDrawCollapse`의 두 연결 호출 필요. 이미지 draw 통합은 미완성 |
| 4 | 이미지 소유 자체를 비교하는 ABBA 및 3시점 픽셀 판정 명령 준비 | 실측 0회. 새 후보 +20%, 폴백 0, 30ms 이상 0 모두 NA |

## 구현과 진단 범위

- `GpuDecoImagePlayback.cs`: 기본 꺼짐인 pose 진단 선택, 이미지 교체/Flush 시 텍스처 슬롯 갱신 호출, 알파 이벤트 소비, 대기/감시/검사 수 로그를 추가했다.
- `GpuDecoImageGeometryFixture.cs`: 기존 기하 fixture와 별도로 정지 진단 및 `images begin/end/pixel/restore/result` 판정을 추가했다. 미완성 `images isolated`와 새 `ImagePixelProbe`는 사용하지 않는다. csproj·compute·번들은 변경하지 않았다.
- `GpuDecoImageTimeline.cs`: 실제 재생 코드가 사용하는 `GpuDecoImageWakeQueue`를 분리했다. 준비 단계에 HashSet 용량을 예약한다.
- `LazySprite.cs`: AUTOTEST 이미지 소유일 때 원래 setter 직전에 현재 GPU 논리 필드를 동기화한다. 이미지 setter는 한 번만 실행한다. 사전 동기화 실패는 CPU 복귀 후 원래 setter로 이어진다.
- `GpuDecoDrawCollapse.cs`: 허용된 소유 수 검사와 `UpdateDrawCollapseTextureSlot` 헬퍼만 수정했다. 준비용 등록/주소 보존 함수와 필드는 이미지 파일에 두었다. 독립 렌더러 MPB도 갱신해 draw 통합을 끌 때 최신 이미지를 보존한다.

CPU 정점 주입은 `GpuLiveOwned=28523`, CPU fallback=0, 정지한 활성화 직후, 독립 native copy, 성능 창 밖에서만 허용한다. 색/UV/비이미지 정점은 유지한다. 일반 GPU 평가는 정지 프레임에도 실행되므로 단발 VB 쓰기 대신 main camera pre-cull에서 정상 평가 뒤 주입/복사를 반복한다. 해제하면 정상 평가·복사를 다시 실행한다. 재생/소유권/곡 시각이 바뀌면 주입을 해제한다. **이 코드는 게임에서 아직 실행하지 않았다.**

`GPUIMG_DIAG`는 O=CPU, G=GPU 수식, D=CPU 정점 주입, U=UpdatePosition 선택의 변경 픽셀/최댓값을 남긴다. O/O2, G/N, D/DN, U/UN 잡음과 G/G2 복원을 `assertzero`로 검사한다. D가 같아지는 결과는 해당 정지 표본의 기하 경로를 좁히는 근거이며, 전곡·모든 원인의 배제를 자동 선언하지 않는다.

## 알파 감시 변경

기존 4,476개 미룸 집합의 매 프레임 복사/전수 평가를 제거했다. `GpuLiveSlice.pendingAlpha`를 cached FieldRef로 읽어 `FlushDirty`가 소비하기 전에 해당 미룸 장식만 깨운다. 이 목록을 수정하거나 소유 파일을 패치하지 않는다.

`MarkDirty`는 알파 효과의 시작/완료에만 생긴다. 시작 프레임 alpha=0인 애니메이션은 다음 프레임에도 감시해야 하므로, **미룸 중이며 알파가 애니메이션 중인 항목만** 감시 집합에 남긴다. 정적 비양수 알파는 다음 이벤트까지 잠든다. 따라서 감시 0은 정적 대기 조건에 한한 수치다. 모든 알파가 애니메이션 중이면 감시 수가 늘어날 수 있으며 실제 비용 절감은 미측정이다.

## Claude 파일 소유자에게 필요한 연결

`GpuDecoDrawCollapseRenderer.cs`는 수정하지 않았다. 해당 소유자가 `PrepareDrawCollapse`에 아래 두 호출을 연결해야 한다.

1. 초기 텍스처 수집 루프 뒤, 배열 티어/직접 슬롯을 계산하기 전에:

```csharp
#if AUTOTEST
RegisterGpuImageCollapseVariants(textures);
#endif
```

2. `gpuCollapseTexture = new ComputeBuffer(...); gpuCollapseTexture.SetData(info);` 뒤:

```csharp
#if AUTOTEST
RememberGpuImageCollapseAddresses(addresses);
#endif
```

등록은 공유 텍스처 wrap을 미리 바꾸지 않는다. 원래 V15 setter/Defer가 원래 이벤트 시점에 Clamp를 적용한다. 지원하지 않는 mip/wrap이나 미등록 주소는 CPU 복귀로 처리하며, 연결 누락을 통과로 숨기지 않는다. 주소는 해당 ComputeBuffer 객체와 짝지어 이전 준비의 주소를 재사용하지 않는다. 주소 사전은 숫자만 보관하며 맵 해제 때 비운다.

`GpuDecoGpu.BeginCollapse/DispatchCollapse`의 `_ImageShape` 바인딩과 canonical compute의 `EvaluateCollapse -> WriteVerticesAt` 안 `Flags&64` 경로는 소스에서 확인했다. 새 이미지 draw 결과의 게임 검증은 아니다. 기존 drawcollapse 게임 게이트에는 23547 검사도 남아 있으므로 이미지 ABBA는 별도 `images ...` 판정을 사용한다.

## 외부 시험용 계획

- `tools/optimization/out/imageown-diag1-steps.json`: 156단계, 기본 모드. 두 가설 및 260/262/270 정지 비교를 먼저 수행한다. 새 managed DLL과 검증된 이미지 커널 번들이 필요하다.
- `tools/optimization/out/imageown-abba1-steps.json`: 320단계, **실행 선행 조건 미충족**. 픽셀 원인 수정·maxdiff<=3 확인과 위 두 연결을 끝낸 뒤에만 외부에서 실행한다.

생성기는 게임·설치·장치를 호출하지 않는다. `--mode diag|abba`, `--bundle`, `--refreshpose`, `--self-test`를 지원한다. 기존 템플릿/JSON은 수정하지 않았다. 입력 맵은 기존 계획의 Arche 경로를 재사용했다.

기존 생성기는 open 전에 `images own on`을 넣었고 playback 선택/compile이 빠졌다. 새 계획은 snapshot 전 candidate 선택, 정지 CPU에서 compile/own/matrix 선택, 이후 준비 순서를 따른다. 각 준비에서 drawcollapse reset·unsafe off를 명시한다.

ABBA는 A=이미지 candidate off+GPU23547+draw 통합, B=이미지 candidate/own on+GPU28523+draw 통합이다. 각 팔을 재열어 준비해 의도적 CPU fallback 4,976개가 생기는 비교를 피했다. 260..270초 측정 창에는 정지·캡처·readback이 없으며 재개/seek/stop 전에 소유권을 반환한다. 3440×1440@165 검사만 하고 디스플레이를 변경하지 않는다. B/A>=1.20을 요구한다(실제 A가 202이면 B>=242.4FPS). CPU/런타임 폴백과 B의 텍스처 제외도 0을 요구하며, 제외 수는 별도로 기록한다. 모든 창에서 30ms 이상 프레임 0, 세 시점 maxdiff<=3, 잡음/복원 0, native 오류 0, 네 소유 해제 복원이 있어야 게임 게이트 PASS다. 설치 복원·전곡 수명은 별도 미검증이다.

## 오프라인 검증

| 검사 | 결과 | 증거 |
|---|---|---|
| DEV 빌드 | 오류 0, 경고 48 | `out/imageown1-dev-final.txt` |
| 일반 Player 빌드 | 오류 0, 경고 36 | `out/imageown1-player-build.txt` |
| PlayerAuto 빌드 | 오류 0, 경고 40 | `out/imageown1-playerauto-final.txt` |
| LoadCheck DEV/Player/PlayerAuto | FieldRef 279/229/281곳, 틀림 0/0/0 | `out/imageown1-loadcheck-*.txt` |
| 실제 큐 클래스 검사 | 4,495개 통과 | `out/imageown1-queue-check.txt` |
| 계획 생성기 검사 | 5개 통과 | `out/imageown1-generator-tests.txt` |

빌드는 `out/imageown1/build/{dev,player,playerauto}`와 별도 intermediate에 격리해 기존 bin 산출물을 덮어쓰지 않았다. 일반 Player 연구 제외를 LoadCheck로 확인했다. LoadCheck는 필드 형식/이름과 후크 계약의 오프라인 검사이며 실제 Mono/Harmony/게임 실행 증명이 아니다. 큐 검사는 실제 소스 클래스를 링크해 정적 대기 4,476개, 0→양수·음수 오버슈트·완료·중복 이벤트·외부 flush·맵 정리를 검사했다. 픽셀이나 FPS 검사가 아니다.

이번 수정 범위는 이미지 파일 3개, `LazySprite.cs`, 허용된 drawcollapse 검사/헬퍼, 이미지 계획 생성기, 이미지 문서와 자체 `out/imageown1*` 검증 산출물이다. Claude 소유 renderer/policy/export/Dormancy/TightLate/FastMove, csproj, canonical shader, 기존 번들, 설정/제어 파일은 수정하지 않았다. 실제 원인 확정과 수용 기준 달성은 외부 시험 결과 및 파일 소유자 연결 뒤 남아 있다.
