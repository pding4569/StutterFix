# GPU 장식 효과 몰림 프레임 줄이기 (2026-11-24, Codex 작업 문서)

작업 위치 `E:\StutterFixWork\wt-v14prod`, 브랜치 `claude/v3-282port`, 시작 HEAD `db8c67e`.
**게임 실행·설치 DLL 교체·커밋·푸시 금지.** 게임 대조·프레임 측정은 Claude 가 한다. 디컴파일 게임 코드는 저장소 밖(`E:\StutterFixWork\decompiled-20261124`)만 읽고 저장소에 넣지 않는다.

## 목표

곡 중 장식 이동 효과가 몰리는 프레임에서 우리 코드(FastMove → DecoBake GPU 장식 경로)의 메인 스레드 비용을 줄인다. **결과(GPU 버퍼 값·렌더러 상태·순서)는 바이트까지 같아야 한다.** 같음을 증명하지 못하는 후보는 기본 끔으로 두고 이유를 적는다.

## 측정 근거 (Claude, 2kHz profsamp, 플레이어+자동시험 빌드, GPU 장식 켬)

Arche 258.00초 한 프레임 21ms. 그중 `FastMove.Prefix`(ffxMoveDecorationsPlus.StartEffect) 69%.
- `DecoBake.RunGpuImageEffect` 43% (하위: `SyncGpuImageFields` 17%, `PreflightGpuImageTarget` 9.5%, `RefreshGpuImageGeometry` 7%, `InvisibleSkip.SyncGpuImageColor` 7%, `DecoAnim.CopyGpuImageFields` 4.8%, `GpuDecoGpu.UpdateImageGeometry`(ComputeBuffer.SetData), `ResolveImageArrayAddress`, `GpuDecoData/Item:.ctor` 배열 할당)
- `DecoBake.SkipAdopt` → `GpuLiveSkip` 19% (`Dictionary<scrDecoration,int>.FindEntry` — 기본 비교자는 UnityEngine.Object.Equals 라 엔진 생존 확인을 부른다)
- `DecoBake.Reasons` 안에서 `System.MonoCustomAttrs:GetPseudoCustomAttributes` (곡 중 리플렉션 — 결과를 캐시할 수 있는지)
- `FastMove.ImageBlock` 안 `Material.DisableKeyword`, `ShaderPropertySheet::SetTextureWithNoAuxiliaryProperties`
- `HashSet<int>.Contains`(PreflightGpuImageTarget), `Dictionary<SpriteRenderer,int>.FindEntry`(InvisibleSkip.Work), `FastMove.CropUsed`, `FastMove.TryGpuImageSprite`(SequenceEqual)
Arche 219.6초(22ms)는 `ApplyGpuLiveEffectToTarget`·`SetMasterSlotStatic`·`WriteMasterSlot` 쪽, Exit 1.04초(29ms)는 첫 히트 효과 몰림(FastMove 약 10ms).
원자료(Claude 가 만든 요약): `E:\StutterFixWork\night-results\plumstart\arche-258-top.txt`, 프로파일 `arche-258.txt`(심볼: `E:\StutterFixWork\tools-sym\symb_lib.py` 의 name()).
11-23 결과 문서 `docs/handoff/gpulive-effect-cost-20261123-result.md` 의 남은 후보(사전 대신 정수 인덱스, Renderer.enabled 읽기 캐시)도 참고.

## 할 일

1. 위 함수들을 읽고 항목마다: 무엇을 반복하는지, 같은 결과를 더 싸게 얻는 방법, 같음 근거(모든 쓰기 경로·수명·무효화)를 적는다.
2. 같음이 확실한 것부터 구현한다. 예: Unity 객체 키 사전/집합에 참조 비교자(`RuntimeHelpers.GetHashCode` + `ReferenceEquals`)를 쓸 수 있는지(같은 네이티브 객체 = 같은 C# 객체; 파괴된 키 처리 동일한지 확인), 리플렉션 결과 캐시, 효과 단위로 한 번만 해도 되는 준비를 장식마다 반복하는지, 같은 값 다시 쓰기, 할당 없애기.
   주의: 2026-11-24 에 `gpuLiveOwnedSet`/`gpuLiveMasterByDeco` 를 참조 비교로 바꾼 것만으로는 Timeline/Exit/WF ABBA ±0(약간 -0.4%)이었다(`patches/gpudeco-ownercache-noGain`). 프레임당 항상 도는 곳이 아니라 **몰림 프레임 한 번의 비용**이 목표다.
3. 각 최적화는 AUTOTEST 에서 켬/끔 비교가 되게 정적 스위치를 둔다(예: `gpudeco imagefast on|off`). 원래 경로와 결과 비교(oracle)를 추가하거나 기존 `gpudeco effectcheck`, `slotcheck` 를 확장해 새 경로도 검사되게 한다. 표본 수 0 은 성공이 아님을 출력에서 알 수 있게.
4. 세 빌드 모두 오류 0:
   `dotnet build StutterFix.csproj -c Release -p:Edition=Player`, `-p:Edition=Debug`, `-p:Edition=Player -p:AutoTestBuild=1`.
5. 결과 문서 `docs/handoff/gpu-image-burst-20261124-result.md`: 바꾼 파일(명시 경로), 항목별 같음 근거, 예상 절감(호출 수 기준), 남은 위험, Claude 가 돌릴 대조·측정 명령(맵·곡 시각·명령 순서).

## 하지 말 것
- 프레임·효과를 미루거나 순서를 바꾸는 변경(그림이 달라질 수 있음).
- 게임 실행, 설치 폴더 쓰기, git commit/push, `git add -A`.
- 막히면 비슷한 시도를 반복하지 말고 다른 각도의 아이디어를 짧게 시험해 본다(오프라인 마이크로 벤치 등).
