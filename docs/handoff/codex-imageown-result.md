IMAGEOWN=FAIL fps_before=NA fps_after=NA gain_pct=NA pixel_max=NA independent_pixel_max=1 registered=NA missing=NA direct_required=NA arrays_used=NA fallback=NA spikes_30ms=NA reason=WINDOW_COVERAGE_NOT_RUN

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
