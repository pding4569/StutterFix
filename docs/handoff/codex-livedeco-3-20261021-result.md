# livedeco-3 결과 (2026-10-21)

구현·오프라인 검증을 마쳤다. **실측 분포에 따른 확정과 GATE1 재측정은 미완료**다. 게임 실행·설치·커밋·푸시를 하지 않았고 `.codex/config.toml`, 외부 측정 JSON, 게임 파일을 수정하지 않았다. 기존 인계 문서 변경과 livedeco-2 로그는 보존했다.

## 변경

- `LiveDeco.cs`의 일괄 `property-block-unverified` 제외를 없애고, `HasPropertyBlock()` 이후 `GetPropertyBlock()`으로 실제 블록을 읽는다. `classifier_version=3`이다.
- 렌더러 전체와 재질 슬롯 0 블록을 각각 읽는다. 슬롯 블록이 비어 있지 않으면 전체 블록 대신 슬롯 블록을 판정한다. 다중 재질 장식은 기존 `material-slots` 조건으로 제외하며, 그 장식의 이름 진단 범위도 슬롯 0까지다.
- 질의 집합은 기본 후보 8종, 게임 코드에서 회수한 BlendModes·장식·마스크 속성 이름, 현재 셰이더 선언 속성 및 선언된 텍스처의 `_ST`다. 블록 자체의 전체 이름 열거는 아니다.
- 매 센서스에 `property_block_names=`를 반드시 출력한다. 각 항목은 `renderer|slot0|effective:<속성이름>:<타입들>=<렌더러수>`다. 기본 검사에서 먼저 제외된 장식까지 포함한 전체 sprite-live 집계이며, 같은 장식은 각 범위에 별도로 들어간다. 타입별 Has 질의가 겹치면 `vector,color`처럼 한 항목에 함께 기록한다. 아무 이름도 관측되지 않으면 `none-observed`다.
- `HasTexture/HasFloat/HasVector/HasColor/HasMatrix` 외 integer/buffer/constant-buffer도 질의한다. `HasProperty`는 있는데 해당 타입이 없으면 `other`로 기록한다. 이름·타입 집계와 함께 `property_block_state=`에 has/renderer_empty/slot0_empty/effective별 개수를 남긴다.
- 알려진 기본 이외의 유효 속성은 `property-block-custom:<이름>`으로 제외한다. 기본 이름이어도 텍스처·색·flip·ST·외부 알파 값이 아래 조건과 다르면 같은 사유로 제외한다. 여러 속성이 있으면 첫 이름을 제외 사유로 쓰고, **모든 해당 이름**의 개수를 `property_block_custom=<이름> count=<수>`에 기록한다. 이 개수는 기본 검사 통과 여부와 무관한 전체 sprite-live 유효 슬롯 0 집계다.
- 빈 블록은 후보를 유지한다. 비어 있지 않지만 관측 이름이 하나도 없는 블록은 `property-block-unresolved`로 제외한다. 관측된 기본 속성이 모두 아래 조건에 맞는 비어 있지 않은 블록은 **잠정 후보**를 유지한다.

| 기본 후보 이름 | 잠정 호환 조건 |
|---|---|
| `_MainTex` | Texture 타입, 현재 sprite.texture와 동일 |
| `_MainTex_ST`, `_AlphaTex_ST` | Vector 타입, `(1,1,0,0)` |
| `_Color`, `_RendererColor` | Color 타입, white 또는 현재 renderer.color와 정확히 동일 |
| `_Flip` | Vector 타입, x/y가 현재 flipX/flipY에 따른 ±1과 동일 |
| `_EnableExternalAlpha` | Float 타입, 0 |
| `_AlphaTex` | Texture 타입, 참조가 null |

이 표는 센서스의 잠정 호환 검사이며, 엔진이 이 모든 이름·값을 실제로 쓴다는 실측 결론이 아니다. 실측 없이 허용 이름이나 개수를 추가하지 않았다. 기존 material signature·정렬 장벽·메시 live·mask·hitbox·split-alpha 등 검사는 유지했다. 렌더러 상태·재질·블록을 설정하거나 Harmony 패치를 만들지 않는다. 센서스용 MPB 두 개는 생성한다.

## 설계 한계와 게이트

설치된 Unity DLL의 [MPB 메서드 목록](livedeco-3-mpb-api.log)과 [Unity 공식 MPB API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MaterialPropertyBlock.html)에는 블록의 전체 속성 이름을 열거하는 API가 없다. 알려진 이름에 대한 Has 질의는 그 이름의 존재만 검사하므로, **기본 속성만 있는 블록과 기본 속성+미지의 커스텀 속성이 있는 블록을 구분할 수 없다.** 셰이더 선언 목록도 MPB의 전체 목록을 대신하지 못한다. 따라서 요청한 '기본 속성만 있음'의 완전한 증명은 이 방식으로 불가능하다.

이 한계를 숨기지 않도록 `name_enumeration_complete=0`, `property_block_provisional_candidates=<수>`, `numeric_gate=<판정>`을 기록한다. `owned_candidates`, coverage, mesh_runs는 잠정 후보를 포함한 수치 모델이다. mesh_runs≤200·coverage≥80%와 기존 정렬 검사로 계산한 값은 `numeric_gate`이고, 잠정 블록 후보가 하나라도 있으면 **최종 `GATE1=INCONCLUSIVE`**다. 잠정 블록 후보가 없을 때는 기존 게이트 판정을 따른다. 통과를 만들기 위해 미지의 속성을 없다고 간주하지 않았다.

인계 문서의 설계 한계 중단 조건에 따라 완전 열거·자동 PASS 보장은 여기서 중단한다. 추가 게임 실행이나 다른 판정 방식으로 진행하지 않았다. 실제 커스텀 속성이 장식마다 있는지, 어떤 이름이 몇 개인지는 아직 측정하지 않아 적을 수 없다.

## 근거와 다음 실측

게임 속성 이름: [읽기 전용 소스 발췌·DLL SHA256](livedeco-3-property-source.txt). BlendModeEffect의 Get/SetPropertyBlock 및 overlay/mask setter 호출과 사용하는 정적 필드는 [게임 IL](livedeco-3-blend-il.log)로 교차 확인했다. 이 호출 존재만으로 해당 효과가 현재 sprite에 MPB를 적용한다고 단정하지 않는다. 실제 적용은 다음 분포가 확인한다.

기존 저장 로그에는 262초 live/basic/owned=2079/2040/0, 270초=2269/2226/0이며, 각각 MPB 일괄 제외가 2040/2226개다. **속성 이름별 분포는 기존 로그에 없다.** 수정 후 각 시점의 이름·타입별 개수, 커스텀 제외 개수, 잠정 후보 수, coverage, mesh_runs, numeric_gate, GATE1은 모두 미측정이다. 예측 개수로 채우지 않았다.

자동 루프는 기존 `E:\StutterFixWork\night-results\ld-steps.json`을 그대로 사용할 수 있다. 25단계와 262/270초 센서스 시점을 오프라인 검사했고 파일을 수정하지 않았다. SHA256=`30036b99ad1284bdae1ea97c2183478671784df0b99225a19d5c207ba014a439` ([검사 로그](livedeco-3-existing-steps.log)). `classifier_version=3`과 필수 `property_block_names=` 줄을 확인해야 한다. 실측 후에도 `name_enumeration_complete=0`을 완전 열거로 해석하면 안 된다.

## 오프라인 검증

| 검사 | 결과 | 로그 |
|---|---|---|
| Release DEV | 오류 0, 경고 38 | [빌드](livedeco-3-build-dev.log) |
| Release Player | 오류 0, 경고 4 | [빌드](livedeco-3-build-player.log) |
| Release PlayerAuto | 오류 0, 경고 38 | [빌드](livedeco-3-build-auto.log) |
| LoadCheck DEV / Player / Auto | 268 / 229 / 268곳, 틀림 모두 0 | [DEV](livedeco-3-loadcheck-dev.log), [Player](livedeco-3-loadcheck-player.log), [Auto](livedeco-3-loadcheck-auto.log) |
| LiveDeco fixture | 22,952 checks 통과 | [fixture](livedeco-3-fixture.log) |
| edition 메타데이터 | 10 checks 통과, 일반 Player 타입·참조 없음 | 같은 fixture 로그 |
| MatShare fixture | 25,401 checks, 오류 0 | [fixture](livedeco-3-matshare-fixture.log) |
| Python steps | 25단계·독립 시점·UTF-8 CLI 통과 | [fixture](livedeco-3-steps-fixture.log) |
| git diff --check | 통과 | 최종 로컬 검사 |

추가 fixture 두 건은 MPB가 미검증이면 수치상 PASS/FAIL 모두 최종 INCONCLUSIVE임을 검사한다. 실제 Unity MPB 내용·네이티브 API 동작·픽셀·성능 검증은 아니다. LoadCheck는 기존 FieldRefAccess 검증이며 MPB의 런타임 값 검증이 아니다. 동적 이름 5곳과 기존 빌드 경고는 남는다.

## 작업 관찰

task-observer v3.5.0의 사용자 범위 관찰과 활성 원칙을 읽었다. 저장소는 쓰기 허용 범위 밖이므로 변경하지 않았다. 관찰은 이 결과 문서로 반환한다: 문제 — 이름별 Has 질의를 전체 열거로 오인하면 미지의 override를 누락한다. 개선 — 질의 범위·관측 개수·열거 불완전성을 함께 기록하고 최종 판정에 반영한다. 원칙 — 관측되지 않은 것과 존재하지 않는 것을 구분한다. 스킬·메모리는 수정하지 않았다.
