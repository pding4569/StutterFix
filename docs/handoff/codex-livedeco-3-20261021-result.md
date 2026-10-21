# livedeco-3 자동 반복 2회차 결과 (2026-10-21)

**판정: 설계 한계로 중단. GATE1=INCONCLUSIVE 유지, 성공 미달성.**
`codex-al-ld-2.md`의 '안 되는 이유가 설계 한계면 결과 문서에 구체적으로 적고 멈춰라'를 적용했다. 이번에는 게임 실행·설치·커밋·푸시·재측정을 하지 않았다. 소스와 빌드 산출물, `.codex/config.toml`, 외부 steps JSON, 게임 파일은 변경하지 않았다. 같은 코드의 빌드·fixture·게임 시험을 반복하지 않았다.

## 새로 전달된 실측 분석

출처: [1회차 자동 수집 증거](ld-autoloop-run1-evidence.txt). 아래는 전달받은 측정값이며 이번 세션에서 측정한 값이 아니다. 두 센서스 모두 classifier_version=3이다.

| 항목 | 262.028491초 | 270.017840초 |
|---|---:|---:|
| live | 2,079 | 2,266 |
| basic / owned 잠정 후보 | 2,040 / 2,040 | 2,223 / 2,223 |
| coverage_pct | 98.124 | 98.102 |
| mesh_runs (기준 ≤200) | 78 | 71 |
| ambiguous_material_ties | 0 | 0 |
| renderer:_MainTex:texture | 2,079 | 2,266 |
| effective:_MainTex:texture | 2,079 | 2,266 |
| renderer 비어 있지 않음 / slot0 비어 있음 | 2,079 | 2,266 |
| property_block_provisional_candidates | 2,040 | 2,223 |
| numeric_gate | PASS | PASS |
| GATE1 | INCONCLUSIVE | INCONCLUSIVE |
| census_cpu_ms | 571.476 | 542.772 |

`property_block_names=`는 두 시점에 모두 출력되었다. 관측된 이름·타입은 `_MainTex:texture` 한 종류이며, renderer와 effective는 동일 장식에 대한 두 집계이므로 합산하지 않는다. `property_block_custom=` 줄은 없으며, 알려진 질의 이름 중 커스텀 속성이 검출되지 않았다는 뜻이다. 미지의 이름까지 포함한 커스텀 속성 총수가 0이라는 증거는 아니다. 나머지 제외 사유는 각각 keywords 1개와 repeat 38/42개다. 모두 `property-block-unverified`로 제외되던 이전 문제는 해소되었고, 이번 미통과 원인은 별도의 검증 조건이다.

## 코드상 직접 원인

`LiveDeco.cs:262`는 기본 조건과 이름별 검사를 통과한 유효 MPB가 비어 있지 않으면 provisionalBlocks를 늘린다. `LiveDeco.cs:293`은 `provisionalBlocks == 0`을 검증 인자로 넘기며, `LiveDecoPolicy.cs:65`는 검증되지 않은 후보가 있으면 INCONCLUSIVE를 반환한다. 따라서 두 시점의 2,040/2,223개가 최종 통과를 막는다. 분포 부족이나 mesh_runs 초과가 원인이 아니다.

기본값 검사와 알려진 커스텀 이름별 제외, 슬롯 우선순위, 센서스 출력은 기존 구현을 유지했다. PASS를 만들기 위해 검증 인자를 true로 바꾸거나, 기본 이름이 하나 보인다는 이유로 나머지 이름의 부재를 가정하지 않았다.

## 이번에 추가로 검토한 경로와 한계

기존의 이름별 질의 확장 대신 **게임의 MPB 작성 경로를 추적하여 속성 집합이 닫혀 있음을 증명할 수 있는지** 검토했다. 읽기 전용 디컴파일 C#과 설치된 DLL의 IL 메서드·호출 목록을 교차 확인했다. 신규 근거: [오프라인 메타데이터 감사](livedeco-3-run2-property-audit.log).

- `BlendModes/ComponentExtension.cs:142`의 GetPropertyBlock은 null을 반환하고, 147행의 SetPropertyBlock은 빈 구현이다. RendererExtension<T>, SpriteRendererExtension, MeshRendererExtension, MeshRendererVisualDecoExtension의 IL 메서드 목록에도 이 두 메서드의 재정의가 없다. 따라서 이 계층에 대해서는 BlendModeEffect의 MPB setter 분기 호출만 보고 실제 렌더러 MPB 작성이라고 볼 수 없다. 기존 [BlendModeEffect IL](livedeco-3-blend-il.log)의 호출 존재와 실제 구현을 구분했다.
- 이것은 SpriteRenderer 내부 네이티브 구현이나 다른 어셈블리의 작성 경로까지 배제하는 증명이 아니다. `_MainTex`가 보인다는 실측과 위 관리 코드만으로 '엔진 기본 속성만 존재한다'는 결론을 확정할 수 없다.
- 현재 UnityEngine.CoreModule.dll의 MaterialPropertyBlock 전체 메서드 목록을 새로 확인했다. 이름/ID별 Has·Get, isEmpty, Clear는 있으나 전체 키 이름/ID 열거 및 단일 키 제거 API가 없다. `GetFloatArrayCountImpl(int32)` 등은 지정한 속성의 배열 길이이며 전체 속성 개수가 아니다. m_Ptr은 네이티브 포인터여서 관리 필드 열거로 내용을 확인할 수 없다.
- 유한한 이름 집합을 Q라고 하면, `{_MainTex}`와 `{_MainTex, Q에 없는 이름의 값}`은 현재의 모든 Has/Get 질의와 isEmpty에서 같은 관측 결과를 낼 수 있다. 셰이더 선언 목록도 MPB의 저장 키 전체 목록이 아니다. 질의 이름을 더 추가하는 접근으로 이 구별 불가능성을 제거할 수 없다.

게임 DLL SHA256은 `F692210F9A342F7EED4BE40AAD463286AB40BF046599959823942EA02F4CA3FF`로 디컴파일 README의 원본 접두와 일치한다. Unity CoreModule SHA256은 `9F5141E8DD6C34B82BB03F151E6F0773129F55A7A2E816FE1E31E549737F4A71`이다. IL 도구는 DLL 메타데이터만 읽었고 게임 코드를 실행하지 않았다.

**중단 사유는 다수의 커스텀 속성이 실측되었기 때문이 아니라, 요구한 '기본 속성만 있음'을 현 방식으로 증명할 수 없기 때문이다.** 새 자료는 수치 기준 통과를 확정하지만 완전 열거의 공백을 메우지 못한다. 같은 코드/steps 재시험으로 PASS를 보장할 근거가 없으므로 자동 반복 재시험을 권고하지 않는다. 네이티브 내부 검사나 '현재 셰이더가 소비하는 속성만 검증'으로 성공 정의를 바꾸는 작업은 수행하지 않았다.

## 검증 상태

이번 변경은 결과 문서·장부·10줄 요약·오프라인 IL 근거뿐이다. 빌드 및 fixture를 다시 실행하지 않았으며, 아래는 **직전 회차 저장 로그 확인 결과**다.

| 검사 | 직전 결과 | 근거 |
|---|---|---|
| Release DEV / Player / PlayerAuto | 오류 0 / 0 / 0, 경고 38 / 4 / 38 | livedeco-3-build-dev.log / livedeco-3-build-player.log / livedeco-3-build-auto.log |
| LoadCheck DEV / Player / Auto | 268 / 229 / 268곳, 틀림 모두 0 | livedeco-3-loadcheck-dev.log / livedeco-3-loadcheck-player.log / livedeco-3-loadcheck-auto.log |
| LiveDeco fixture | 22,952 checks 통과 | livedeco-3-fixture.log |
| edition 메타데이터 | 10 checks 통과, 일반 Player에 실험 타입·참조 없음 | livedeco-3-fixture.log |
| MatShare fixture | 25,401 checks, 오류 0 | livedeco-3-matshare-fixture.log |
| steps fixture | 25단계, 독립 시점, UTF-8 CLI 통과 | livedeco-3-steps-fixture.log |

신규 IL 감사 명령은 종료 코드 0이다. 이번 최종 `git diff --check`도 통과했다. 정책 fixture와 메타데이터 검사는 실제 Unity MPB·픽셀·성능의 증명이 아니다. 기존 조건부 컴파일과 일반 Player 제외 설정은 변경하지 않았다.

## 작업 관찰 반환

task-observer를 적용했다. 기존 사용자 범위 관찰 저장소는 쓰기 허용 범위 밖이므로 수정하지 않고 이 문서로 반환한다. 문제: 수치상 통과와 최종 검증 미완료를 같은 실패로 취급하면 동일 시험을 반복하게 된다. 개선: 실제 중단 인자와 미해결 증명 조건을 추적해 새 측정이 그 조건을 해결할 수 있는지 먼저 판단한다. 원칙: 재시험은 불확실성을 줄일 새 가설이 있을 때 수행한다. 스킬·메모리는 수정하지 않았다.
