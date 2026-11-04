# 이미지 장식 볼록 메시 — 2026-11-04

**채택하지 않는다.** DD의 실제 삼각형 이미지 200개를 바꾼 같은 정지 프레임에서 두 메시 API·4/6/8정점 모두 무오차 픽셀 조건에 실패했다. 패딩 4·8 texel도 실패했다. 일반 Player에는 B 기능을 추가하지 않았고 `GpuDeco=0` 기본값을 유지한다. 새 설정 플래그도 없다. 푸시·릴리스하지 않았다.

## 순서와 구현 범위

A를 `0860fae`, Arche FULL 정정 측정을 `2e8945d`로 커밋한 뒤 B 게임 시험을 실행했다. A의 압축 완료 대기를 사용하며, 실제 적용한 B 두 판은 DD 미리 압축 넣음 16장·처음부터 압축 2648장·압축 지각 0이었다. 이 상태의 비교만 해석했다.

`TightSpriteProbe`는 DEV/AUTOTEST 전용 수동 진단이다. 정상 Player에는 본문이 없으며 곡 중 자동 순회나 이미지 읽기를 설치하지 않는다. 오프라인 Pillow로 alpha>0의 픽셀 사각형을 취하고, Repeat 반대 가장자리까지 포함해 정사각형 반경 2 texel로 팽창한 후 볼록 껍질을 계산했다. 정점 제한은 인접 지지 직선의 교점을 선택하는 보수적인 바깥 근사다. 원래 껍질의 모든 점을 포함하는지 검사하며, 복잡한 껍질은 경계 사각형으로 보낸다. 최소 면적 해법이라고 주장하지 않는다.

참고한 [Humus Particle Trimmer 설명](https://www.humus.name/index.php?ID=266)의 소스는 복사하지 않았다. [Sprite.OverrideGeometry 문서](https://docs.unity3d.com/jp/2019.4/ScriptReference/Sprite.OverrideGeometry.html)의 Sprite Rect 픽셀 좌표 규칙으로 변환했다. 대체 API는 SpriteDataAccess의 position/UV/indices다. 원본 Sprite를 복제하고 원본 rect·pivot·pixelsPerUnit·정렬·재질·텍스처 속성을 유지했다. 사용하지 않는 원래 좌하/우상 정점 2개를 추가해 bounds를 보존했다. 따라서 다각형 4/6/8정점은 실제 버퍼 정점 **6/8/10개**, 삼각형 **2/4/6개**다.

## 오프라인 면적 조사

비율은 각 이미지의 원래 사각형 면적=100%이며, 소스 초기 AddDecoration 개수로 가중했다. 화면 면적·실제 그리는 픽셀·곡 중 보임·교체 이미지 빈도는 가중하지 않았다. DD는 모든 참조 이미지를 읽었고 나머지는 초기 인스턴스 8개 이상 이미지를 표본으로 삼았다. 누락된 표본을 맵 전체 결과로 부르지 않는다.

| 맵 | 조사 초기 장식/전체 | 읽은 이미지 | 2px 경계 사각형 | 2px 볼록 껍질 | 4정점 바깥 근사 |
|---|---:|---:|---:|---:|---:|
| DD | 6256/6284 | 2735 | 45.24% | 39.49% | 45.22% |
| Windflower | 4858/5603 | 37 | 90.28% | 79.13% | 88.95% |
| HELLO | 1536/2426 | 57 | 83.29% | 70.87% | 81.55% |
| 7777 | 166/474 | 11 | 66.41% | 57.79% | 65.55% |
| Exit | 2849/3483 | 15 | 65.24% | 64.40% | 65.33% |
| Arche FULL | 28536/28828 | 22 | 86.74% | 75.27% | 91.93% |

이 표의 면적 감소는 GPU 절감이 아니다. Arche의 탐욕 근사는 경계 사각형보다 나쁠 수도 있다. 최적해 주장은 하지 않으며 실제 적용은 원래 사각형의 80% 미만 후보에만 한정했다. [전체 표와 소스 SHA256](evidence/tight-audit-summary.json), 맵별 `tight-*.json`·`tight-*.tsv`에 좌표와 입력 파일 검사합이 있다. 중복 basename은 진단에서 제외했다.

주요 대상 `thin_triangle.png`(375×375)는 원래 alpha>0 경계 사각형보다 볼록 껍질이 유리했다. Repeat 2 texel 포함 껍질 30.3292%, 4/6/8정점 근사 30.3591/30.3311/30.3298%다. 색이 있으나 alpha=0인 픽셀은 이 이미지에서 0개다. 별도 인공 검사에서는 색 있는 alpha=0, alpha=1, Repeat 맞은편 가장자리 지원을 확인했다.

## 실제 DD 픽셀 비교

2560×1440 D3D11/Gamma, 전체 필터 유지, UI 제외, 비교 영역 마스크 없음, 허용차 0. 각 판은 기준 잡음 0 및 rendererStateEqual=True였다. 같은 판의 같은 정지 장면에서 A→메시→원상 복구를 비교했다. 프로세스 사이 프레임이 완전히 같다고 주장하지 않는다.

기존 `dd-detail.py`는 이름상 “50초”이나 census는 실제 `waitsongpos 52` 뒤다. 50초 정지 장면에서는 선택한 안전 조건의 RGBA Additive 후보가 없었다. 52초에서 census와 같은 thin_triangle 200개를 확인한 후 실제 적용을 시험했다.

| 판 | API | 다각형 정점 | 패딩 texel | 실제 교체 | bounds 차이 | 달라진 픽셀/3,686,400 | 최대 RGBA 차이 | 끈 뒤 차이 |
|---|---|---:|---:|---:|---:|---:|---|---:|
| v6 | OverrideGeometry | 4 | 2 | 200 | 0 | 3814 | 2,3,2,0 | 0 |
| v6 | OverrideGeometry | 6 | 2 | 200 | 0 | 3680 | 2,2,2,0 | 0 |
| v6 | OverrideGeometry | 8 | 2 | 200 | 0 | 3632 | 2,3,2,0 | 0 |
| v6 | SpriteDataAccess | 4 | 2 | 200 | 0 | 3809 | 2,3,2,0 | 0 |
| v6 | SpriteDataAccess | 6 | 2 | 200 | 0 | 3680 | 2,2,2,0 | 0 |
| v6 | SpriteDataAccess | 8 | 2 | 200 | 0 | 3631 | 2,3,2,0 | 0 |
| v7 | 원형 그대로 복제 대조 | 원래대로 | 해당 없음 | 200 | 0 | **0** | 0,0,0,0 | 0 |
| v7 | OverrideGeometry | 4 | 4 | 200 | 0 | 3966 | 2,2,2,0 | 0 |
| v7 | OverrideGeometry | 4 | 8 | 200 | 0 | 3765 | 2,2,2,0 | 0 |

각 실제 적용의 원본 Sprite는 남겨 두었고 200개 모두 원상 복구했다(`externally_replaced=0`). clone.bounds·rect·pivot은 정확한 Equals, pixelsPerUnit은 정확한 값 비교를 통과했다. renderer.bounds도 성분 비교에서 같았다. 원형 복제만으로는 픽셀이 바뀌지 않았다. 여유를 늘려도 계속 차이가 나므로 **잘린 alpha>0 가장자리만의 문제로 설명되지 않는다.** 메시 재테셀레이션에 따른 UV 보간·샘플링/래스터 연산 차이가 남는 것으로 판단한다. GPU 내부 UV 계산과 동적 배칭 변환의 세부 원인은 분리하지 못했다. alpha 임계값을 올리거나 허용차를 늘려 통과시키지 않는다.

`framecmp`의 자동 종합 verdict는 이 수동 실험이 `assertzero`·합성 order/hide control을 실행하지 않아 다른 칸에 FAIL/NA를 남긴다. 이를 통과 증거로 인용하지 않는다. 표의 근거는 직접 diff, 잡음, 실제 설치 개수, 복원 로그다. 후보는 명백한 비영 차이로 탈락했으므로 긍정 채택에 필요한 추가 control을 수행하지 않았다.

## 실패와 확대하지 않은 검사

총 7프로세스의 B 시험을 보존했다. 최초 JSON 로더 null, TSV의 빈 다각형 파싱 실패는 유효한 메시 시험이 아니다. v3은 정확한 타입 제한, v4/v5는 50초 안전 조건 제한으로 실제 교체 0개였다. 이 판들의 차이 0을 통과로 세지 않는다. v6/v7만 실제 200개를 바꾼 시험이다. [판별 원시 추출 및 실행 단계](evidence/tight-sprite/summary.json), 전체 로그 `E:/StutterFixWork/night-results/tight-dd-*.log`.

| 요구 변수 | 확인 범위/남은 한계 |
|---|---|
| 쌍선형·Repeat | alpha>0·반대 가장자리·2/4/8 texel 검사. 패딩 확대로 무오차가 되지 않음 |
| 셰이더·마스크 | Legacy Particles/Additive만 허용. Tile·Grab·premultiply·마스크·다른 셰이더는 안전 미입증으로 제외 |
| 정렬·컬링 | 원래 bounds extrema 2개로 Sprite/renderer bounds 유지. 원형 복제 픽셀 0. 전 맵·곡 전체의 순서/컬링 미검증 |
| 게임·다른 모드 | 원본 Sprite 복제, hitbox·Tiled/Sliced·packed·border 제외. rect/pivot/ppu 보존. 실제 그리는 MainTex 속성 블록 우선. 곡 중 교체·flip·pivot 변화와 외부 모드 임의 쓰기 전반은 미검증 |
| 동적 배칭·CPU | 실제 버퍼 정점 6/8/10개. draw call·메인/렌더 스레드·GPU 이득은 **미측정** |
| 맵 열기·메모리 | 픽셀 해독은 전부 오프라인. Unity 메인 스레드 픽셀 읽기·DXT 재해독 없음. 압축/축소 이미지 제외. 제품 로딩 통합을 하지 않아 B의 열기 시간/메모리 비용은 미측정 |
| 측정 상태 | 적용한 DD 두 판 모두 압축 지각 0, 같은 정지 프레임 A/B/R. 과거 76 FPS는 A에서 미재현 |
| 해제·공유 | 원본 공유 Sprite·재질을 수정하지 않음. 모든 교체 200개 off 복원·복원 픽셀 0. Unload/배치 종료 정리 코드는 있으나 활성 상태 hot reload 시험은 미실행 |

**6맵 성능 ABBA 4판, 다른 5맵의 활성 메시 픽셀 시험, 정점 수별 GPU/CPU/draw call 비교는 실행하지 않았다.** DD의 필수 무오차 조건에서 두 API와 패딩 대안이 실패해 후보를 폐기했기 때문이다. 불합격 후보의 FPS를 채택 근거로 만들지 않았다. 모든 전후 성능 칸은 미측정이며 면적 감소를 절감 수치로 바꾸지 않는다. 자동 적용·기본 켬 후보·새 플래그를 남기지 않았다.

## 재현

게임이 꺼져 있을 때 `tight-sprite-audit.py <chart> --min-instances 8 --output <json>`으로 표본을 만들고 `tight-sprite-manifests.py`로 TSV와 작은 JSON을 만든다. DD의 처음 전체 감사 원본은 `E:/StutterFixWork/night-results/tight-raw-audits`에 보존했다. `tight-sprite-one.py <image> --pad 4|8 --output <tsv>`로 여유 대안을 만든다. 모든 알고리즘은 이 저장소 코드이며 외부 소스를 복사하지 않았다.

`dotnet build -p:Edition=Player -p:AutoTestBuild=1` 뒤 작업 루트의 `run_locked.py`를 호출하는 `python tools/optimization/tight-sprite-runs.py dd` 또는 `... dd diagnose`로 같은 진단을 실행한다. 실행기는 차트 경로·SHA256·TSV 숫자·빈 다각형을 먼저 검사한다. `test-tight-sprite-audit.py`의 2개 검사와 기존 `test-sprite-alpha-audit.py` 1개 검사를 통과했다. PNG는 게임 Mods의 `framecmp` 및 `E:/StutterFixWork/night-results/tight-dd-v6-png`에 남아 있으며, 최종 복원 검사는 아래 기록을 따른다.

## 최종 상태

PlayerAuto와 일반 Player 빌드 오류 0. 일반 Player 바이너리에 TightSpriteProbe·ColdOpenProbe·MonoOptProbe 타입 이름이 포함되지 않는 것도 확인했다. [최종 복원 검사](evidence/final-restoration.json): 설치 DLL/참조 DLL SHA256 일치, gpuprobe.dll 없음, GpuDeco=0, 원래 asset-cache 경로 복원, 시험 게임/잠금 없음. 새 시험 캐시는 파일 수·바이트 합 보존을 확인하고 E 드라이브로 옮겼다. B의 제품 기본 동작 변경은 없다. 작업 시작 때 남아 있던 MonoOptProbe 및 Load/csproj 변경은 커밋에 섞지 않고 그대로 보존했다.
