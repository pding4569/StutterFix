# 이미지 소유 작업 장부

## 2026-10-25 — 코드·오프라인 검증 5회차

외부run4 직접37줄 원시순서대조. 등록185/주소149/누락36(직접36)·직접필요68/32·배열3/4·318.75MiB에서 live prepare 실패, 통합픽셀/FPS NA. 곡전체 ReadyQuad 등록을 소유 대상별 창[257.5,270.25] 이벤트+직전상태+기존물리스프라이트 집합으로 좁히고 coverage/collapsecheck도 동기화했다. 숨은소유 업로드대상은 유지·32/4상한/가드 유지·초과그룹로그 추가. 새accept는 활성화257.5초·333단계abba3-window 생성만, 기존명시12출력/기존JSON 보존. 격리3빌드 오류0/0/0·경고48/36/40·LoadCheck279/229/281곳 틀림0·실제timeline+추출3메서드227/생성기8검사 통과·보호14SHA유지. IMAGEOWN=FAIL reason=WINDOW_COVERAGE_NOT_RUN, 창축소후 registered/missing/direct_required/arrays_used·통합픽셀/FPS/끊김/전곡/설치복원NA. 게임·설치·커밋·푸시·장치·Editor·설정/제어변경0회. 새coverage가 넘치면 수치기록후 중단·소유자설계 인계, 상세결과/10줄/out/imageown5 참조.

## 2026-10-25 — 코드·오프라인 검증 4회차

`IMAGEOWN=FAIL fps_before=NA fps_after=NA gain_pct=NA pixel_max=NA independent_pixel_max=1 fallback=NA spikes_30ms=NA reason=TEXTURE_ADDRESS_MISSING`

외부 run3 직접35줄을 원시와 순서·중복 대조했다. 3440×1440@164.999, 준비82구간·포함14595·3배열117복사·318.75MiB·직접32/32·폴백0 뒤 live on의 image_texture_not_registered_in_collapse로 중단됐다. GPU28523 분류 뒤 Rec11821/11821 복원·종료native_contexts0. 통합픽셀/FPS는 NA다.

현재항목만 검사하는 준비 계약이 미래 이미지 주소 누락을 놓칠 수 있다. 이미지 훅에서 전체 등록 합집합 대 실제 주소를 준비 중 대조하고 직접/배열·요청/상한·누락변형 수를 기록, 누락이면 CPU기록을 떼기 전에 중단하게 수정했다. 허용 helper의 실패로그만 추가해 등록 누락과 자산ID변화를 구분한다. 누락ID가 옛 로그에 없어 실제 직접슬롯초과로 단정하지 않는다. 실제 수용량 증가·통합불가능 확정은 아니다.

3빌드 오류0/0/0·경고48/36/40, LoadCheck279/229/281곳 틀림0·일반Player 연구 제외, 실제메서드3개 추출20검사·기존 생성기8검사 통과. 보호14개SHA동일·renderer/policy/export 등 소유자 파일·생성기·기존JSON·bundle·설정/제어 유지. 새계획 없음·같은333단계 재시도 중단 필요, 주소배정/자산수명은 소유자에게 인계한다. 게임·설치·커밋·푸시·장치·Editor·설정/제어변경0회. 상세 결과·10줄 요약·out/imageown4 참조.

## 2026-10-25 — 코드·오프라인 검증 3회차

`IMAGEOWN=FAIL fps_before=NA fps_after=NA gain_pct=NA pixel_max=NA independent_pixel_max=1 fallback=NA spikes_30ms=NA reason=INTEGRATED_ABBA_NOT_RUN`

외부 run2 직접 증거78줄을 원시와 순서 대조했다. 3440×1440@164.999·260/262/270초 독립 이미지 소유 GPU/pose/CPU정점의 최대픽셀은 모두1이다. GPU changed384/172/61, pose도 동일, CPU정점489/280/258이며 반복/복원0이다. UpdatePosition·CPU정점 주입의 개선 근거가 없어 기본끔·기존 정점 수식을 유지했다. 잔여1 원인은 미확정이고 현재 허용3 이내다. 같은 diag1/diag2 반복 금지.

활성화6회 GPU28523·CPU fallback0, live off6회 Rec 각 기대값과 일치, 종료 native_contexts0이다. 통합/FPS/전곡 측정은 없다. 디컴파일 SetSprite/UpdatePosition 및 설치 DLL ILScan을 대조했다.

기존 이미지 등록2곳·32B 부분 갱신·알파 더티 큐를 재구현하지 않았다. 새 정지 collapsecheck는 현재/변형 텍스처 누락과 등록됐지만 제외된 항목/이미지를 구분하고 폴백이면 중단한다. 기존 renderer가 정렬 제외도 같은 폴백에 세므로 텍스처 부족으로 오인하지 않는다. renderer/policy 등 보호 파일은 변경하지 않았다.

새 기본 accept: abba2-preflight333단계 생성만. 최대7장으로 세 통합 픽셀·원본/G/B 반복·G복원·폴백0을 먼저 확인한 뒤 ABBA, 실패 시 진입 거부. 기존 명시3모드 출력12사례 및 기존JSON/번들/native/소유자 SHA 보존. 번들 지원 확인 전 실행 불가, 마스터 NEED_EDITOR_BUILD는 해제하지 않는다.

3빌드 오류0/0/0·경고48/36/40, LoadCheck279/229/281곳 틀림0·일반 Player 연구 제외, 생성기8검사 통과. 게임·설치·커밋·푸시·자동 장치·Editor·설정/제어 변경0회. 상세 결과·10줄 요약·out/imageown3 근거를 따른다.

## 2026-10-25 — 코드·오프라인 검증 2회차

`IMAGEOWN=FAIL fps_before=NA fps_after=NA gain_pct=NA pixel_max=NA fallback=NA spikes_30ms=NA reason=DIAG2_NOT_RUN`

외부 run1 직접 증거42줄을 원시 로그에 대조했다.3440×1440@164.999·GPU28,523·활성화 CPU fallback0, 정점주입286개·world delta0.00013647876, refreshpose 호출0/4,976을 관측했다. O/O2·G/N·D/DN·G/G2 반복/복원 차이는0이나 원본 O/G·O/D·O/U 비교는 **없다**.

중단 원인은 첫 시점 아홉 번째 캡처 UN의 **8장 상한 위반**이다. 원시580행 실패 뒤583행 pause 복원·585행 원본 보호이므로 보호 복귀를 선행 원인으로 오인하지 않는다. 기존 diag1은 반복하지 않는다.

기본 diag를165단계 diag2로 변경했다. CPU 반복 대조/저장 후 O2 재사용으로 최대8장, 후보별 원본 diff/PNG 즉시 기록, bounded GPUIMG_DIAG의 네 반복/복원 대조를 추가했다. 명시 diag1/abba 전체 출력8사례 동일·기존 JSON 해시 보존. 새 JSON은 생성만 했다.

SetSprite/UpdatePosition 디컴파일 및 설치 DLL ILScan 대조. renderer 연결2곳은 현재 이미 존재하며 수정하지 않았다. 픽셀 원인 미확정·ABBA 선행 조건 미충족, 소유 방식 불가능 판정은 아니다.

3빌드 오류0/0/0·경고48/36/40, LoadCheck279/229/281곳 틀림0·일반 Player 연구 제외, 생성기7검사 통과. 빌드 격리·큐 재검사 생략. 게임·설치·커밋·푸시·장치 호출·Editor·설정/제어 변경0회, 타 작업자 bundle 근거 변경 유지. 상세 결과와10줄 요약을 따른다.

## 2026-10-25 — 코드·오프라인 검증 1회차

`IMAGEOWN=FAIL fps_before=NA fps_after=NA gain_pct=NA pixel_max=NA fallback=NA spikes_30ms=NA reason=GAME_NOT_RUN_AND_COLLAPSE_OWNER_HOOKS_PENDING`

사용자 직접 지시와 `codex-imageown-20261025.md`를 적용했다. 게임·설치·커밋·푸시·자동 장치 호출·설정/제어 변경 0회다. 기존 사용자/Claude 변경을 유지했다.

1. 진단 (a)/(b): UpdatePosition 선택과 지속 CPU TransformPoint VB 주입, GPUIMG_DIAG 숫자 로그 및 잡음/복원 계획을 구현했다. **실제 진단 로그 없음**. 게임 금지로 원인 확정은 보류했다.
2. 원인 수정: 두 후보의 실제 차이가 없어 어느 수식도 확정 수정하지 않았다. 이미지 matrix/refreshpose는 명시 선택이며 새 pose 경로 기본 꺼짐을 유지했다. 픽셀 max<=3은 NA다.
3. 통합 준비: 소유 수 입구·32바이트 부분 갱신·호출과 알파 더티 감시를 구현했다. `GpuDecoDrawCollapseRenderer.cs` 수정 금지로 RegisterGpuImageCollapseVariants/RememberGpuImageCollapseAddresses 두 호출은 소유자 요청으로 남겼다. 연결되지 않은 후보를 통합 완료로 표시하지 않는다.
4. 수용 시험: 기본 diag1 156단계와 선행 조건이 붙은 abba1 320단계를 생성만 했다. 이전 템플릿/계획을 유지했다. ABBA는 이미지 소유 off/on 비교이며 기존 독립/병합 ABBA를 그대로 쓰지 않는다.
5. 검증: 최종 3종 빌드 오류0(경고48/36/40), LoadCheck279/229/281곳 틀림0, 실제 큐4495개·생성기5개 통과. 정적 대기4476개의 정상 프레임 감시0은 합성 상태 기계 결과이며 실FPS가 아니다.

상세 구현·소유자 요청·외부 실행 선행 조건·한계는 `codex-imageown-result.md`, 10줄 요약은 `codex-imageown-summary.txt`를 따른다. 같은 미완성 통합 계획을 반복 실행하지 말고, 먼저 기본 정지 진단 근거를 받아 원인별로 진행한다. 자동 장치를 직접 시작하거나 중지하지 않았다.
