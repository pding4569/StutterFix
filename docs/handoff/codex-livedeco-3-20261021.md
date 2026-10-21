# livedeco-3: property-block-unverified 판정 고치기 (게이트 1 재시도)

## 상황
livedeco-2 수정 뒤 게임 실측(Arche 262 s/270 s, 증거 `E:\StutterFixWork\night-results\livedeco2.txt`):
live=2269, basic_candidates=2226(98.1%) 이지만 **전부 `property-block-unverified`** 로 소유 후보 0 → GATE1=FAIL, coverage 0%.
`LiveDeco.cs:110` 의 `if (r.HasPropertyBlock()) return "property-block-unverified";` 가 원인으로 보인다.
유니티 SpriteRenderer 는 스프라이트 텍스처(_MainTex 등)를 내부 MaterialPropertyBlock 으로 넣기 때문에 `HasPropertyBlock()` 이 거의 항상 true 일 수 있다(추정, 확인 필요).

## 할 일
1. `HasPropertyBlock()` 이 true 일 때 `GetPropertyBlock(block)` 로 읽어, 블록이 **스프라이트 기본 속성(_MainTex, _MainTex_ST 계열, _Color, _RendererColor, _Flip, _EnableExternalAlpha, _AlphaTex 등 SpriteRenderer 가 스스로 넣는 것)만** 들어 있는지 판별한다. 속성 이름 목록이 필요하면 `block.isEmpty`, 그리고 알려진 속성 이름 후보에 `HasTexture/HasFloat/HasVector/HasColor/HasMatrix` 로 질의해 **실제로 어떤 속성이 있는지 센서스 로그에 속성 이름별 개수 분포로 남겨라**(`GpuCensus.cs:201` 과 `MatShare.cs:126` 참고). 코드에서 쓰는 속성 이름은 `DecoGpu.cs`, `GpuCensus.cs`, `BlendModes`(게임) IL 에서 찾아라.
2. 기본 속성만 있는 블록은 소유 후보 판정에서 막지 않는다(= 후보 유지). 기본 이외의 속성이 있는 장식만 `property-block-custom:<이름>` 사유로 막고, 이름별 개수를 로그로 남겨라. 추정으로 채우지 말고 실측 분포에서 결정한다.
3. 빌드 3종(Dev/Player/PlayerAuto: `dotnet build StutterFix.csproj -c Release -p:Edition=Player -p:AutoTestBuild=1` 등) 오류 0, 기존 fixture/LoadCheck 통과 유지. 일반 Player 에는 포함 금지 유지.
4. 같은 steps(`E:\StutterFixWork\night-results\ld-steps.json`)로 다시 재면 GATE1 이 바뀌어야 한다. 센서스 줄에 `property_block_names=` 분포가 반드시 나오게 하라.

## 금지
게임 실행·설치·커밋·푸시 금지(자동 루프가 한다). `.codex/config.toml` 수정 금지. 장식 이름·헤르츠 맵 이름 등 비공개 사항 언급 금지.
설계 한계로 안 되는 경우(예: 장식마다 서로 다른 커스텀 속성이 실제로 있음)는 결과 문서에 속성 이름·개수로 구체적으로 적고 멈춰라.
결과는 `docs/handoff/codex-livedeco-3-20261021-result.md`, 10줄 요약은 `-o` 파일 하나로.
