# 2026-11-10 작업 F 결과 — RunLod

인계 기준 HEAD `63968c1`, 작업 트리 `E:\StutterFixWork\wt-v14prod`에서 구현했다. 시작 시 이번 변경은 미구현 상태였다. 기존 다른 미추적 파일은 수정하지 않았다. 커밋·푸시·게임 실행·게임 설치 폴더로 DLL 복사는 하지 않았다.

## 구현

- `RunLod.cs`: `Cand.R`을 `Renderer`로 일반화하고 기존 SpriteRenderer 참조는 별도로 유지했다. 파티클은 `runlod particles on|off`로 제어하며 기본 off다. 기존 RunLod 설정과 기본 Sprite 동작은 유지한다.
- 파티클 조건: 부모 `scrParticleDecoration`, Billboard, 마스크 없음, SortingGroup 없음, 단일 재질, Sprites/Default 또는 Sprites/Tile, `_SrcBlend`/`_DstBlend`는 각각 없거나 1/10. 트레일 모듈 또는 trailMaterial이 있으면 제외한다. 여러 재질 검사는 재사용 List와 `GetSharedMaterials`를 사용한다.
- 파티클 확대율은 startSize의 최소값, sizeOverLifetime/sizeBySpeed의 최소 배율, 스케일, ppw, 텍스처 칸 크기로 계산한다. 3D 크기는 축별 최소값을 쓴다. 텍스처 시트는 Grid만 지원하며 칸 수로 텍셀 크기를 나눈다. RepeatX/Y, 스케일링 모드, maxParticleSize도 보수적으로 반영한다. 확대율이 MinMag 미만이면 묶지 않는다.
- 곡선은 키 배열 복사나 샘플링 대신 키별 Hermite 구간의 도함수 근을 풀어 최솟값을 구한다. weighted curve, 비정상 값, 0 이하 크기, 지원하지 않는 시트 모드는 제외한다. 양수 곡선 최솟값에는 부동소수점 오차를 위한 작은 하향 여유를 둔다.
- **기존 방출 입자 보호:** main.startSize는 새로 방출될 입자의 크기다. 크기가 커지는 트윈 중 이전의 작은 입자를 놓치지 않도록 살아 있는 입자의 startSize3D도 확인한다. 모으기 때 공용 Particle 버퍼를 한 번 확보하며, 실제 입자 수가 버퍼를 초과하면 제외한다. 버퍼 상한은 65,536개다. 이 검사는 파티클 CPU 비용을 추가하므로 실제 FPS 이득은 아직 추정할 수 없다.
- 게임의 가시성 알림, 정렬 키, renderer.bounds 화면 사각형, DrawRenderer 경로를 함께 사용한다. 파티클의 depth 변경으로 바뀌는 원래 layer도 프레임마다 확인한다.
- 재질·셰이더·복수 재질·SortingGroup 조건은 기존과 같은 엇갈린 32프레임 재검사로 갱신하고, 같은 재질의 텍스처 교체·크기 트윈·깊이·renderMode·마스크·트레일은 프레임마다 확인한다. 재검사에서 제외된 파티클도 전체 해상도 항목으로 정렬 목록에 남겨, 그 앞뒤 장식을 가로질러 묶지 않는다.

확인한 게임 소스는 인계에서 지정한 세 파일이다. `scrParticleDecoration.SetSprite`는 `particleRenderer.material.mainTexture`를 교체하고, `SetDepth`는 sortingLayer·sortingOrder·GameObject layer를 함께 바꾼다. 초기 설정은 startSize·수명 크기 곡선·시트 칸 수를 설정한다. `ffxSetParticlePlus.StartEffect`는 main.startSize를 DOTween으로 갱신하고 sizeOverLifetime도 교체한다. `ffxEmitParticlePlus.StartEffect`는 기존 시스템에 Emit(count)만 호출하며 별도 재질·정렬 교체는 없다. 따라서 재질 객체 ID만으로 텍스처·현재 크기의 불변성을 가정하지 않았다.

## 프레임 비용과 대조

- `runlod cpu on|off` 추가, 기본 off. 장벽의 키 정렬 배열에서 각 장벽의 최종 판정 결과를 **같은 카메라 콜백 안에서만** 재사용한다. 완전히 확인한 빈 32개 블록은 이후 범위 질의에서 건너뛴다. 다음 콜백에는 stamp가 바뀌어 visibility/enabled/forceRenderingOff/layer를 다시 판정한다. 프레임 사이의 동적 상태를 추측해서 캐시하지 않는다.
- visibility 이벤트만으로 장벽 누적값을 유지하는 방식은 사용하지 않았다. enabled·layer·forceRenderingOff 변경은 visibility 알림만으로 확실하게 추적할 수 없기 때문이다. 프레임 내 중복 제거로 범위를 제한했다.
- 기본 경로는 기존 묶음 루프와 Flush를 유지한다. cpu/verify 사용 시 별도 계획 버퍼를 사용하며, 버퍼 용량은 모으기 때 확보한다. CommandBuffer는 기존처럼 재사용한다.
- 후보당 읽은 bounds·정렬층 ID·정렬 순서를 합성 사각형에도 재사용한다. 사각형·해상도·정렬 계산식은 유지한다.
- `runlod verify on|off` 추가, 기본 off. 같은 프레임의 변경 전 장벽 탐색과 새 탐색으로 만든 묶음 목록을 렌더러 상태 변경 전에 비교한다. 양쪽이 같은 불변 정렬 Item 목록을 참조하므로 시작 인덱스와 개수의 일치는 전체 렌더러 ID 열의 일치를 뜻한다. 사각형 네 좌표, 해상도 나눗수, 절약량도 정확히 비교한다. 다르면 `검증다름`을 증가시키고 처음 10건까지 `[묶음 LOD] 검증 다름 ...`을 기록한다.
- `state`에 `particles=설정/후보수`, `파티클구성원/프레임`, `cpu`, `verify=설정/비교프레임수`, `검증다름`을 추가했다. 기존 전체/후보/정렬/묶음 ms 계측을 유지했다. 프레임 분모는 기존과 같이 실제로 후보를 처리한 Frames이며, 쉬는 중 건너뛴 프레임은 제외한다. 검증을 켜면 양쪽 계산 비용이 계측에 포함된다.
- Pause는 레이어·CommandBuffer·합성 표시를 복구하며 실험 플래그는 재개용으로 유지한다. ForgetLevel도 즉시 CommandBuffer와 합성 표시를 비우고 원래 레이어를 복구한다. Off/Unload는 실험 플래그를 끄고, 이전 맵에서 분리된 알림 컴포넌트와 합성 메시·RT·재질도 정리한다.

## Unity getter 할당 대응

설치된 UnityEngine.ParticleSystemModule.dll을 읽어 확인했다. 공개 MinMaxCurve getter는 내부 MinMaxCurveBlittable을 변환하며 AnimationCurve 래퍼를 `new`한다. 공개 getter를 매 프레임 쓰는 구현은 할당 0 조건을 만족하지 못한다.

이를 피하려고 실험 기능을 사용할 때만 private blittable getter와 AnimationCurve의 포인터 기반 키 조회 함수를 delegate로 연결한다. DynamicMethod는 반환 구조체의 각 필드 이름·형식을 검사하고 값을 복사한다. 구조체 메모리 레이아웃을 재해석하지 않는다. 초기화가 실패하면 파티클 후보를 제외하며 `runlod particles on` 응답의 `api=False`로 확인할 수 있다. 실제 Unity/Mono에서 이 연결과 네이티브 호출의 실행 확인은 Claude 측정에 남아 있다.

정상 프레임 경로에 새 managed 배열·곡선 래퍼·LINQ·계획 리스트 증설을 넣지 않았다. 초기 모으기, 최초 장벽 태그 부착, 최초 묶음 GPU 자원 생성 및 해상도 변경에는 기존처럼 생성 비용이 있다. **실제 Unity에서 프레임당 총 할당 0을 측정한 것은 아니다.** 아래 할당 0 결과는 오프라인 계획 루프와 IL 복사 delegate에 한정한다.

## 확인 결과

| 확인 | 결과 |
| --- | --- |
| `dotnet build StutterFix.csproj -c Release -p:Edition=Player -p:AutoTestBuild=1` | 오류 0, 경고 50 |
| `dotnet build StutterFix.csproj -c Release -p:Edition=Player` | 오류 0, 경고 60 |
| `py -3 tools/runlod-fixture/check.py` | 통과 |
| 장벽 질의 무작위 대조 | 180,000건 일치, null/미부착 태그/가시성/활성/layer/프레임 변경 포함 |
| 원래 Flush와 새 묶음 계획 | 1,200프레임의 구성원 구간·사각형·해상도 일치 |
| 같은 콜백의 빈 장벽 전체 범위 1,000회 질의 | 1,024개 장벽을 총 1,024회 확인 |
| .NET 오프라인 안정 상태 관리 할당 | 계획 루프 및 IL 복사 delegate 각각 0바이트 |
| 곡선 계산 | 내부 최솟값, 선형, weighted 제외, 상수/랜덤 상수/음수 제외 확인 |
| `rlvis3.py` | 문법 검사와 s7/hl 시험 JSON 생성·명령 순서 확인, 게임 실행 없음 |

로그: `out/runlod-f-auto-build.txt`, `out/runlod-f-player-build.txt`, `out/runlod-f-fixture.txt`. 테스트는 실제 RunLod 메서드 본문을 추출하고 Unity 엔진 호출만 스텁으로 대체한다. Unity의 DrawRenderer 결과·픽셀 동일성·실제 FPS·실제 GC 할당·해제 복구의 게임 실행 검증을 대신하지 않는다.

## Claude 측정 준비

`E:\StutterFixWork\bench_v14.py`의 기존 `_c, _cmd = CFG2["rlP"]` 뒤에 추가할 줄이다. 원본 측정기는 수정하지 않았다.

```python
CFG2["rlPp"] = (dict(_c), list(_cmd) + ["runlod particles on"])
CFG2["rlPcpu"] = (dict(_c), list(_cmd) + ["runlod cpu on"])
CFG2["rlPpcpu"] = (dict(_c), list(_cmd) + ["runlod particles on", "runlod cpu on"])
CFG2["rlPverify"] = (dict(_c), list(_cmd) + ["runlod cpu on", "runlod verify on"])
CFG2["rlPpverify"] = (dict(_c), list(_cmd) + ["runlod particles on", "runlod cpu on", "runlod verify on"])
```

먼저 verify 설정으로 동일성 및 파티클 `api=True`, 후보수, 구성원/프레임이 0보다 큰지 확인한다. 이어서 verify를 끈 설정으로 HELLO의 CPU 비용과 7777의 실제 FPS를 비교한다. 아직 FPS 개선 수치나 화질 통과 결과는 없다.

그림 비교는 `E:\StutterFixWork\rlvis3.py`에 사본을 두었다. `FrameCompare.Pause/FreezePlanets`가 재생 중인 파티클 시스템을 Pause(false)하고 원래 상태를 복구하므로 파티클 정지 기능을 새로 만들 필요는 없었다. 다만 rlvis2는 기준 캡처 전에 RunLod 중지를 명시하지 않으므로, 사본에서는 `pause` 직후 `runlod pause`를 넣었다. 기준 두 장이 RGBA에서 완전히 같아야 비교를 계속하며, 실행마다 고유 캡처명을 사용해 오래된 파일을 읽지 않는다. 상태 문자열을 자르지 않고 출력하고, 종료 코드·이미지 누락·기준 이동은 실패로 처리한다. 비교 구간의 verify도 켠다.

```powershell
# 맵 키: 7777 = s7, HELLO 46초 = hl, HELLO 50초 = hl50
py -3 E:\StutterFixWork\rlvis3.py s7 rlPp
py -3 E:\StutterFixWork\rlvis3.py s7 rlPpcpu
py -3 E:\StutterFixWork\rlvis3.py hl rlPcpu

# 게임 없이 측정 JSON만 생성
py -3 E:\StutterFixWork\rlvis3.py s7 rlPpcpu --prepare-only
```

rlvis3에는 위 세 설정의 로컬 fallback을 넣어 bench_v14.py에 줄을 추가하기 전에도 사용할 수 있게 했다. 준비 모드로 만든 파일은 `E:\StutterFixWork\rlvis3-s7-rlPpcpu-prepare.json`, `rlvis3-hl-rlPcpu-prepare.json`이다. 일반 실행은 기존 run_locked.py 경로로 wt-v14prod를 사용한다.
