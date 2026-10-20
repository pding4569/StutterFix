# Codex 임무: Arche 258~278초 구간의 GPU 시간 줄이기 연구

2026-10-20. 작업 폴더 `E:\StutterFixWork\wt-archegpu` (브랜치 `codex/night-archegpu`). 게임 실행·설치·커밋 금지(Claude 가 한다). 출력: 결과 문서 `docs/handoff/codex-archegpu-20261020-result.md` + `-o` 파일.

## 배경 (측정으로 확정)
- 사용자 PC: i5-9400F / RTX 4060 Ti 8GB / 3440×1440 165Hz. 게임은 D3D11, Unity 6000.3, Mono. 모드는 UMM+Harmony.
- Arche 258~278초 구간은 프레임이 10~11ms(약 90 FPS). CPU의 장식 처리 약 5.5ms는 이미 줄일 만큼 줄였거나 더 줄일 코드 레버가 없음(docs/optimization-36.md 끝, `docs/roadmap-deco-pipeline-20261020.md`). 남은 바닥은 **GPU 5~6.6ms**(PresentMon `MsGPUBusy`)와 엔진 CPU 약 5ms.
- 장식 렌더러를 전부 숨기는 진단도 +6%뿐이었다 → GPU 시간의 대부분은 장식 개수가 아니라 **화면 픽셀 채우기·겹쳐 그리기·화면 필터(카메라 필터팩/Bloom 등)·해상도**일 가능성이 큼. 이를 **측정 근거로 가려내는 것**이 1순위.
- 이미 있는 자료(다시 하지 말 것): `docs/handoff/filters-findings.md`, `filter-identity.md`(필터 항등 검사), `codex-gpu-20261018-result.md`~`gpu3`, `codex-gpufree-20261019-result.md`, `codex-decogpu*-result.md`(장식 GPU 이동은 이득 없음). 읽고 중복을 피한다.

## 목표
Arche 258~278초 구간의 **GPU 프레임 시간**(PresentMon MsGPUBusy)을 줄일 수 있는 후보를 찾고, 화면 결과가 **같거나 눈에 안 띄게 같은** 것만 구현한다.

## 해 볼 것 (우선순위)
1. **GPU 비용 분해 계획**: 그 구간에 실제로 어떤 카메라·패스·필터가 도는지 코드/로그(이벤트 파일 `.adofai`는 `D:\얼불춤 맵 파일\` 아래 Arche, 구간 이벤트의 SetFilter/Bloom/카메라 이벤트 목록)로 정리. 이 모드에 이미 있는 계측(`EngineStages`, `GpuClock`, `Recorder` 시도 흔적, `FrameScan`, 프로필 `[엔진 단계]`)을 확인하고, **GPU 시간을 패스별로 재는 최소 계측**(dev/AutoTest 전용, 기본 꺼짐, Player 제외)을 설계·구현. 예: 카메라별 `Camera.Render` 전후 `FrameTiming`/`GL.Flush` 없는 방식 불가 → `FrameTimingManager`, 또는 필터/패스 하나씩 끄는 A/B 스위치(`fxoff <이름>`)로 **끈 판과의 GPU ms 차**로 분해. 스위치는 AutoTest 명령으로 만들어 내가 `sf_run` 으로 돌릴 수 있게 한다.
2. **줄일 후보 목록**(근거와 함께): (a) 화면 크기 렌더 타깃 개수/크기·불필요한 복사(Blit) 제거, (b) 항등/영향 없는 필터 패스 건너뛰기(기존 `filter-identity.md` 와 겹치지 않는 것), (c) Bloom 반경·반복 수 등 비용 큰 패스의 비트 동일한 최적화, (d) 겹쳐 그리기 줄이기(완전 투명/화면 밖 스프라이트 컬링 — 이미 한 것과 겹치지 않게), (e) 장식 쿼드의 정점 수·셰이더 단순화(출력 비트 동일일 때만), (f) MSAA/깊이/스텐실 클리어 불필요분, (g) 반투명 정렬로 인한 배칭 깨짐.
3. 구현은 **기본 꺼짐 + `Settings` 항목 + `Main.Unload` 되돌리기**, Player 빌드에 들어갈 때도 연구 전용 코드는 `#if DEV || AUTOTEST`. 새 Harmony 패치는 이름+서명으로 정확히 하나만 고르고 `AccessTools.DeclaredMethod` 이름 조회는 오버로드가 있으면 예외이므로 쓰지 않는다. (참고: 서명 blob 비교용 `Module.ResolveSignature(MetadataToken)` 는 Mono 에서 안 된다 — 이름+인자 수+호출 규약으로 고를 것.)

## 규칙
- 화면 결과는 원본과 **비트 동일**이거나, 아니면 사용자가 허락한 오차(눈에 안 띄는 1~2/255)를 **자동 비교로 증명**한다. 증명 도구(Frame diff 등)가 이미 있으면 재사용(`StateDump`, `RenderVerify`, `CompareFrames` 등 grep).
- 헤르츠 맵 이름·사용자 비공개 프로젝트를 문서/코드/로그에 쓰지 않는다. 다른 모드 코드를 복사하지 않는다.
- 빌드 확인: `dotnet build StutterFix.csproj -c Release` (개발자), `-p:Edition=Player`, `-p:Edition=Player -p:AutoTestBuild=1`. 오류 0. LoadCheck 통과.
- 측정은 내가 한다. 너는 **후보별로 내가 돌릴 AutoTest 단계 목록**(`sf_run` 스텝: open Arche / seek 262 8 / waitsongpos … / 새 명령 on/off / log)과 **무엇이 얼마나 줄 것으로 예상하는지(추정 근거 포함, 단정 금지)**를 결과 문서에 적는다.
- 끝나면 `-o` 파일에 한국어 요약 10줄 이내.
