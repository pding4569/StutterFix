# 2026-11-10 Codex 작업 결과

작업 트리: `E:\StutterFixWork\wt-v14prod`, 브랜치: `claude/adofai-renderer`.
인계 문서대로 커밋·푸시·게임 실행은 하지 않았다. 독립 Unity 실험실만 실행했다.
기존 미커밋 형태 칸/정렬 관련 변경을 유지하고 그 위에서 이어서 작업했다.

## 1. 빌드·번들: 확인한 결과

- `tools/gpudeco-lab`에서 `python generate-drawcollapse-shader.py` 실행 완료.
- 루트에서 `powershell -ExecutionPolicy Bypass -File tools/gpudeco-lab/prepare.ps1` 실행 완료.
- 지정 Unity 6000.3.10f1, D3D11, `GpuDecoLab.CollapseBundle` 실행 완료. 셰이더·컴퓨트 컴파일 오류 없음. 기존 컴퓨트 경고와 `GpuTileComposite`의 초기화 관련 경고는 남아 있다.
- 실행 전체는 인계 문서에 명시된 기존 `direct draw color` 실험실 검사에서 실패한다. 이를 전체 PASS로 보고하지 않는다. 번들은 그 검사 전에 정상 생성·재로드되었으며, 별도 아래 LOD 검사는 이 최종 번들을 읽어 통과했다.
- 번들 `tools/gpudeco-lab/evidence/drawcollapse5/bundle/stutterfix_gpudeco`: 시작 전 수정 시각 **2026-11-09T06:02:30Z**, 최종 **2026-11-09T18:14:54.2477732Z**. 최종 크기 **101,767 bytes**.
- 최종 번들 SHA-256: `C5236E0DD25166E17194B8D4D2318403114A6839EF7F9D1A39608C10A1F611A7`.
- `dotnet build StutterFix.csproj -c Release -p:Edition=Player -p:AutoTestBuild=1`: **오류 0, 경고 50**, 최종 소스 빌드 통과. 출력 DLL은 `bin/PlayerAuto/StutterFix.dll`. 게임 폴더에 설치하지 않았다.

증거: [번들 실행 로그](../../tools/gpudeco-lab/evidence/collapsebundle-codex.txt), [빌드 로그](../../tools/gpudeco-lab/evidence/build-codex.txt).

## 2. renderlod 화면 범위 합성

### 확인·수정

1. `_LodBox` 초기화/계산 dispatch는 매 프레임 실행된다. `lodInFlight < 3`은 CPU 읽기 요청에만 적용된다. 합성 정점과 부분 지우기는 이번 프레임 GPU 버퍼를 직접 읽는다. 과거 준비 세대의 readback 콜백이 현재 세대의 진행 개수를 감소시키던 경로도 차단했다.
2. D3D의 위→아래 픽셀 `y=(0.5-ndc.y*0.5)*h`와 합성 정점의 `ndc.y=1-y/h*2`는 서로 역변환이다. 픽셀 셰이더의 `SV_POSITION.xy/(w,h)`는 저해상도 RT의 같은 정규화 위치를 읽는다. 여기서 합성 정점에 추가 Y 반전을 넣으면 어긋난다.
3. 저해상도 원본 그리기에는 `_LodMode=2`, 명시적인 `_LodDrawVP`를 사용하게 했다. compute와 같은 `GL.GetGPUProjectionMatrix(camera.projectionMatrix, camera.targetTexture != null) * worldToCameraMatrix`를 쓴다. 중간 RT로 `SetRenderTarget`할 때의 암묵적 카메라 행렬 변화에 기대지 않는다. 최종 합성은 `_LodMode=1`이다.
4. 기존 셰이더에는 `_LodMode` uniform만 있고 ShaderLab Properties 항목이 없었다. `FindPropertyIndex("_LodMode")` 지원 검사와 맞도록 숨김 속성을 추가했다.
5. 기존 범위는 원래 기하의 floor/ceil까지만 포함했다. 선형 확대 필터는 저해상도 유색 픽셀을 기하 경계 밖에서도 샘플하므로, 최대 축소 배율 4에 맞춰 화면 4픽셀 여유를 포함했다.
6. `ClearRenderTarget` 전체 지우기를 `GpuDecoLodClear.shader`의 범위 지우기로 교체했다. 현재 합성 범위를 저해상도 좌표로 바꿔 내림/올림하고 양쪽에 1텍셀 여유를 확보한다. 홀수 해상도에서도 실제 RT 크기 비율을 사용한다. 구간 이동·축소·재등장 시 새로 샘플할 영역을 먼저 지우므로 이전 프레임 영역 전체를 지울 필요는 없다.
7. 저해상도 타깃 viewport를 명시하고 끝에서 `camera.pixelRect`를 복원한다. 전체 타깃과 다른 부분 viewport는 원래 메시로 복귀한다. 새 지우기 셰이더가 없는 번들은 LOD를 활성화하지 않는다.

### GPU 검증 결과

새 `GpuDecoLab.LodCompositeProbe`를 추가했다. 최종 번들을 직접 읽는 독립 실험이며 게임을 열지 않는다.

| 검사 | 결과 | 범위 |
|---|---|---|
| Unity 기본 `UnityObjectToClipPos` 대 명시적 GPU 투영 | **2개 PASS, RGBA 최대 오차 0** | 실제 `Camera.Render` + `targetTexture`, 1/2·1/4 RT |
| 전체 화면 합성·전체 지우기 대 범위 합성·부분 지우기 | **80개 PASS, RGBA 최대 오차 0** | 화면/텍스처 GPU 투영, 홀수/짝수 크기, 1/2·1/4 축소, point/linear 원본 샘플 |
| 잔상/경계/빈 구간 검사 | 위 80개에 포함 | 비대칭 도형 이동, 화면 가장자리, 빈 구간, 재등장; 부분 지우기 전 RT 전체를 자홍색으로 오염 |

가시 사례는 출력 알파가 실제 0보다 큰지도 검사해 빈 그림끼리 비교하는 오류를 막았다.
이 검사는 **기존의 같은 저해상도 그림을 전체 화면에 합성하던 결과와의 동등성**이다. 원래 전체 해상도 Sprite 그림과 LOD 그림이 바이트 같다는 뜻은 아니다. 실게임 전체 맵, 실제 화면 출력, HDR/MSAA/후처리 조합, 성능 개선량은 미측정이다. 부분 지우기는 draw 한 번을 사용하므로 하드웨어 fast clear보다 항상 빠르다고 주장하지 않는다.

증거: [검사 요약](../../tools/gpudeco-lab/evidence/lod-composite-codex.txt), [Unity 로그](../../tools/gpudeco-lab/evidence/lodprobe-codex.txt).

재실행: 기존 prepare 후 같은 Unity 인수에서 `-executeMethod GpuDecoLab.LodCompositeProbe -logFile tools/gpudeco-lab/evidence/lodprobe-codex.txt`를 사용한다. 먼저 새 번들을 빌드해야 한다.

좌표 판단의 외부 근거: [Unity GPU 투영 변환](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/gl/getgpuprojectionmatrix), [Microsoft SV_Position 정의](https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/dx-graphics-hlsl-semantics). 실제 설치 버전에서의 targetTexture 동작은 위 GPU 검사로 별도 확인했다.

## 3. GC 멈춤과 자동 시험 seek

### 확정한 코드 원인과 수정

`GcControl.Tick`은 `Enabled=false`이면 `Hitch.Tick` 전에 반환했다. 따라서 `GcPause=false`로 시작한 경우 `Hitch.Playing`이 갱신되지 않는다. `waitsongpos`는 실제 `songposition_minusi`가 목표에 도달해도 `Hitch.Playing`이 false이면 실패한다. 재생 도중 설정을 바꾸면 이전 값이 남을 수도 있다.

이것은 자동 시험만의 문제가 아니다. `Hitch.Tick`은 플레이어용에서도 곡 시작/끝에 실제 처리를 한다. **`IsPlaying()` 판단과 `Hitch.Tick`을 GC 설정 분기 앞으로 옮겨** GC를 끈 경우에도 곡 수명 처리가 실행되게 했다. 자동 시험에서 GC를 강제로 켜거나 게임의 시계 계산을 바꾸지는 않았다.

`AutoTest.cs`도 다음을 보완했다.

- `waitsongpos` 실패 시 실제 곡 위치, `Hitch.Playing`, GC 기능 상태, 마지막 게임 상태를 함께 기록한다.
- `seek` 타일 검색 실패 때 마지막 타일로 대체하던 코드를 없앴다. 마지막 포털 타일만 선택되는 요청도 명시적 오류로 종료한다.
- 음수/NaN/무한대 시각·여유 및 재생 중 seek를 거부한다. 재생 중에는 먼저 `stop`해야 한다.

게임 DLL 확인: `scnEditor.Play`는 선택한 타일 seqID를 저장하고 `customLevel.Play(num)`에 전달한다. `scrLevelMaker.CalculateFloorEntryTimes`는 conductor의 BPM/카운트다운과 타일 속도·홀드·extraBeats로 entryTime을 계산한다. 게임 코드에서 GcControl을 참조하지 않는다.

**한계:** Windflower/HELLO를 실행하지 않았으므로 이번 수정으로 그 실제 시험이 통과했다고 보고하지 않는다. HELLO가 끝 타일 5037을 고른 정확한 사유가 요청 범위 초과인지, entryTime 준비 문제인지, 다른 맵 상태인지는 제공된 로그만으로 확정할 수 없다. 다만 기존 검색 실패가 마지막 타일 선택으로 바뀌는 경로는 소스에서 확정했고 차단했다.

### 효과 차이에 연결되는 경로: 확인과 추정 구분

| 구분 | 확인한 경로 | 판단 |
|---|---|---|
| **확정된 설정 간섭, 수정함** | `GcControl.Tick → Hitch.Tick → SongStarted/SongEnded` 누락 | GC 토글이 `ShaderWarm`, `EffectBudget.Suspend`, `LowEnd`, `PhysicsPause`, `RunLod`, `PlayTurbo` 등 곡 수명 처리까지 바꾸고 있었다. 효과 차이의 구체적 후보이며 사용자 장면의 단일 원인인지는 재현 필요 |
| **확정된 간접 분기, 위 수정 대상** | `ParticleFix.UpdatePrefix`의 `PauseOffscreen && Hitch.Playing`, `LowEnd`의 자동 해상도/메뉴 FPS 판정 | GC 끔으로 Playing이 false에 머무르면 파티클 화면 밖 정지, 해상도, 메뉴 FPS 제한 동작도 달라질 수 있음 |
| **확정된 별도 설정 묶음, 유지함** | `Main.ApplyConfig`: `GcControl.Enabled`와 `AllocFix.Enabled` 둘 다 `GcPause`에서 설정 | GC만 바꾸는 A/B가 아님. AllocFix는 scrFloor.Update의 클로저 재사용과 비동기 키보드/마우스/키보드 입력 경로를 바꿈. 화면 오류라는 증거는 없어 임의 분리하지 않음 |
| **확정된 시간 의존 코드, 영향은 추정** | `EffectBudget`의 실시간/프레임 유예, `scrConductor.Update`의 DSP/오디오 시간, DOTweenComponent의 deltaTime/realtimeSinceStartup | GC 정지가 프레임 간격을 바꾸면 효과 시작/밀린 효과 처리/샘플 시점이 달라질 수 있음. GCMode를 검사해서 효과 종류를 바꾸는 게임 경로를 찾은 것은 아님 |
| **확정된 약한 참조, 오류 미확인** | `ParticleFix.states` CWT, `culled` WeakReference; `HitMeterFade` 핸들 CWT; `LoadFix.sameTexSets`; `TagIndex.plans`; `MapProfiles.keys`; 일반 Player의 `GpuDecoRecordStore.stamps` | 자동 GC를 미루면 죽은 키/대상의 정리가 늦어진다. 살아 있는 키의 항목이 임의로 없어지는 구조는 아님. 명시적 초기화/복원도 있으며, 이 때문에 효과가 달라진다는 증거는 못 찾음 |
| **확정된 종료자, 오류 미확인** | 모드 `AlignedArena.~AlignedArena` → native memory Release; 게임 `RDMP3Stream` 종료자 → 스트림 Unload | GC 지연은 미해제 객체의 자원 반환을 늦출 수 있음. 모드에는 Dispose 경로가 있고 MP3에는 EOF Unload도 있음. 장식 효과를 종료자에서 켜고 끄는 경로는 확인 못 함 |
| **확정된 모드 차이** | 기본 PauseMode=manual; Disabled는 진단 선택. ParsePause/ImagePrefetch는 로딩 중 별도 Disabled 사용. 힙 한계 처리 등은 GCMode를 일시적으로 복구해 수집 | `GcControl.Paused`와 실제 GCMode가 항상 같은 뜻은 아님. Manual은 명시적 수집을 허용하는 정책, Disabled는 다르게 취급해야 하며 실시간/메모리 압박 영향은 재현 필요 |

`GcControl.Enabled`/`Paused`를 직접 읽는 모드 파일은 전부 확인했다. `AllocScan` 경고, `EffectScan`의 floor-appear 설명 로그, `ParticleTextWatch` 스택 채집, `RenderWatch` 필터 변화 로그, `PerfOverlay` 상태 표시는 진단 경로다. `ImagePrefetch`는 GC 모드 전환과 로딩 후 수집을 직접 수행하며, `AbTest`는 Enabled를 토글한다. `Compat`의 GcPause 참조는 Quartz 중복 안내이며 설정을 자동으로 바꾸는 코드가 아니다.

게임 측 직접 호출 조사 범위: 현재 설치의 `Assembly-CSharp.dll`, `Assembly-CSharp-firstpass.dll`, `DOTween.dll`, `DOTweenPro.dll`, `RDTools.dll`. WeakReference/CWT, GarbageCollector 모드, GC.Collect/WaitForPendingFinalizers/CollectionCount/GetTotalMemory 직접 호출은 해당 5개에서 발견하지 못했다. `RDMP3Stream` 종료자는 별도 디컴파일로 확인했다. 이는 reflection·엔진 native 구현·나머지 라이브러리까지 없다는 증명이 아니다.

증거: [게임 IL 호출 검색](../../tools/gpudeco-lab/evidence/gc-game-codex-il.txt), [모드 직접 참조 목록](../../tools/gpudeco-lab/evidence/gc-mod-readers-codex.txt). 읽기 전용 디컴파일은 `tools/gpudeco-lab/evidence/codex-decompiled/`에 남겼다. 게임 DLL은 수정하지 않았다.

## 4. 픽셀 셰이더 비용

기존 fmod 제거 변경을 유지했다. 추가로 info.x의 float→uint 변환과 `DIRECT_FEW`의 `_DirectMap` 조회/재매핑을 정점 단계로 이동했다. 전달 정보는 기존 2성분에서 늘리지 않고 `nointerpolation uint2`로 전달하며, 배열 레이어는 asuint/asfloat로 원래 float 비트를 보존한다. 바인딩 하위 7비트만 바꾸므로 point/repeat/blend 비트는 보존된다. 동일 사각형의 텍스처 메타데이터는 정점마다 같고, 원래도 nointerpolation이었으므로 샘플 좌표/샘플러/섞기 수식은 같다.

region clamp와 wrap은 픽셀 단계에 유지했다. 특히 frac/clamp는 비선형이므로 정점에서 적용한 뒤 보간하면 반복 경계나 패딩 경계의 그림이 바뀐다. UV 곱셈을 옮기는 것도 부동소수점 보간/반올림 차이를 만들 수 있어 이번에는 하지 않았다.

**확인:** 최종 셰이더 번들 컴파일과 위 LOD GPU 검사 통과. **미확인:** 전체 바인딩/혼합 모드에 대한 구버전과 신버전의 실게임 픽셀 비교, 1.2ms→0.6ms 목표 달성 또는 FPS 향상. 그 수치를 달성했다고 주장하지 않는다.

## Claude가 이어서 확인할 항목

1. 새 DLL과 **새 번들(부분 지우기 셰이더 포함)**을 함께 사용한다. 배포/설치는 아직 하지 않았다.
2. Windflower에서 `set GcPause false`, `stop`, `seek 146 8`, `waitsongpos 150 40` 재시험. 이전과 같은 모니터 타일 이동뿐 아니라 새 `Hitch.Playing`/상태 로그를 확인한다.
3. HELLO는 실제 맵 타임라인 안의 구간을 사용한다. 범위 오류가 나면 entryTime과 요청 시각을 먼저 확인한다. 끝 타일로 자동 대체하지 않는다.
4. 동일 곡 위치 고정 장면에서 LOD 전체 화면/범위 합성 비교와 on/off 복원, 화면/targetTexture/후처리 조합, 부분 지우기 GPU 비용을 확인한다. 이번 82개 실험실 사례를 전체 맵 검증으로 확대 해석하지 않는다.
5. 사용자 효과 차이는 우선 같은 나머지 설정을 유지한 GC on/off로 재현한다. AllocFix까지 함께 바뀌는 점과 효과 처리의 실시간/프레임 유예를 고려한다.
