# 게임 함수의 흔한 낭비 후보

작성: 2026-10-14. `codex-tasks.md` 작업 2의 **정적 IL 조사 결과**다. 아래 순위는 후속 검토 순서이며, 실제 비용이나 끊김 원인의 순위가 아니다. 게임 실행·A/B·할당량 측정은 하지 않았다. 모든 후보의 전/후 측정값과 실행 표본 수는 없음(0판)이다.

## 조사 범위와 재현

- 대상: `D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\A Dance of Fire and Ice_Data\Managed\Assembly-CSharp.dll`
- DLL SHA-256: `6BA2CFB8D260962D3C8A3E4409F60E8E98293CA9B2D2ECA4875096BD02D40230`
- 모드 검토 기준: 로컬 `codex/shader`, HEAD `b4334eb8db2b2d7239c70fcbb380f3be3a59a524`. 원격 fetch·브랜치 변경은 하지 않았다. 사용자의 “결과 파일만” 지시에 따라 작업 목록의 일반 절차보다 산출물 범위를 우선했다.
- 도구: 기존 `tools/ilscan/bin/Release/net8.0/ILScan.exe`. 모드와 스캐너 소스 수정·빌드·설치는 하지 않았다.
- `TYPES=scr/scn/ffx`로 형식을 열거한 뒤, 중첩 형식을 제외한 scr 계열 209개, scn 계열 14개, ffx 계열 84개(합계 307개)의 메서드별 호출·필드를 읽었다. 이 안의 `Update/LateUpdate/FixedUpdate/OnGUI` 137개(scr 115, scn 17, ffx 5)를 훑었다. 추가로 `PlanetSprite`, `PlanetRenderer`, `TileFlash`, `SettingsMenu`, `ADOFAI.FPSCounter`, `ADOFAI.KeyIndicator`, `dfxFollowPlanet` 등 관련 형식과 호출된 보조 함수를 확인했다.
- DLL 전체에서 `GetComponent`, `GetComponents`, `GetComponentsInChildren`, `FindObjectsOfType`, `FindObjectOfType`, `FindObjectsByType`, `get_main`, `ToString`, `Concat`, `Format`, `Where`, `Select`, `ToList`, `ToArray`, `Find`, `FindAll`, `GetEnumerator`, `set_text`, `set_color`, `get_material`의 호출자를 검색했다.
- 중첩 생성 형식은 초기 출력 파일명에 Windows 금지 문자가 있어 저장되지 않은 것이 있다. 해당 형식 내부까지 전수 조사했다는 주장은 하지 않는다. 부모 함수에서 보이는 생성·델리게이트 참조는 조사했다. 스캔 출력은 임시 폴더에만 두었으며 게임 구현 코드나 IL 덤프를 저장소에 넣지 않았다.

재현할 때 PowerShell에서 `$env:METHOD='scrPlanet'; $env:FIELDS='1'`을 설정한 뒤 `ILScan.exe <DLL> ZZZ`를 실행한다. 호출자 검색은 두 환경 변수를 제거하고 `ILScan.exe <DLL> OverlapCircleAll`처럼 실행한다. 형식 열거는 `$env:TYPES='ffx'` 등의 방식이다. 환경 변수는 조회 후 지운다.

**근거의 한계:** 이 스캐너는 같은 호출 대상을 함수 안에서 중복 제거한다. 출력 한 줄은 호출 지점 1개나 실제 실행 1회를 뜻하지 않는다. 분기·루프의 정확한 범위, 제네릭 인자, 오버로드, 값의 동일성은 이 출력만으로 확정할 수 없다. 프레임 함수라는 사실과 그 안의 특정 호출이 매 프레임 실행된다는 사실을 구분했다. 구조체의 `.ctor`나 `List<T>.Enumerator`를 곧바로 힙 할당으로 세지 않았다.

## 우선 검토할 상위 10개

| 순위 | 함수 | IL에서 확인한 사실 / 낭비 후보 | 호출 빈도 추정 | 고치기 쉬움 |
|---|---|---|---|---|
| 1 | `scrPlanet.Update` | `Physics2D.OverlapCircleAll` → 부모 Transform 조회 → `GetComponent` → 장식 히트박스 조건·`HitboxTriggerAction`. 충돌 결과 배열과 결과별 컴포넌트 조회가 후보다. | 활성 행성의 매 프레임 Update 중 히트박스 검사 분기. 조회 횟수는 행성 수, 뒤 처리는 검출된 Collider 수에 비례할 가능성. | **어려움**: 결과 순서·개수·재진입 보존 필요 |
| 2 | `scrBgbar.Update` | `GetComponent` → `Renderer.material` → `Material.color`, Transform 크기 읽기·쓰기, 색 Lerp. 배경 막대별 반복 조회와 색 변화가 끝난 뒤의 같은 색 쓰기가 후보다. | 활성 막대마다 매 프레임. 커스텀 맵에서 이 구형 막대가 실제 쓰이는지 미확인. | **중간**: Renderer 캐시는 비교적 쉬움, 색 생략은 소유권 확인 필요 |
| 3 | `scnGame.Update` → `RDExtensions.ScaleXY` | 카메라 크기·종횡비를 읽고 배경/전경 플래시, 엔드스크린 플래시 및 장면 플래시의 Transform 크기를 갱신하는 참조가 있다. `ScaleXY`는 `localScale` 읽기·쓰기를 한다. 카메라 크기가 그대로인 구간의 반복 쓰기가 후보다. | 게임 장면 매 프레임. 실제 갱신 대상 수·분기 통과 수 미확인. | **중간**: 대상별 Z 보존과 다른 작성자 확인 필요 |
| 4 | `scrCamera.Update` | 위치·회전·배경색 쓰기, RT 경로의 quad 크기 및 `material.mainTexture` 쓰기. 정지한 카메라·끝난 색 보간에서 같은 값의 엔진 쓰기와 RT 참조 재설정이 후보다. | 활성 카메라 매 프레임, `skipUpdate/useRTCam/enableCustomFPS` 등 조건에 따라 다름. | **어려움**: 카메라 효과·에디터·다른 모드와 공유 |
| 5 | `scrFlash.Update` | 색 타이머 갱신 → `flashRenderer.material` → 색 Lerp → `Material.color`. 플래시가 끝나 같은 색으로 안정된 구간의 조회·쓰기가 후보다. | 활성 플래시 객체 매 프레임. 정적 색 상태를 사용하므로 인스턴스 간 관계 확인 필요. | **중간**: 페이드 도중은 원래대로 유지 |
| 6 | `scrTextBonusColor.Update` | `Persistence.GetIsHighestPossibleAcc` → `GetComponent` → `goldTextColor` → `Graphic.color`. 최고 정확도 상태가 유지되는 동안의 컴포넌트 조회와 동일 색 쓰기가 후보다. | 해당 HUD 객체가 활성인 매 프레임 중 정확도 조건 분기. | **중간**: 컴포넌트 캐시는 쉬움, 외부 색 변경을 놓치면 안 됨 |
| 7 | `scrConductor.Update` | 예약 판정음 목록의 `HitSoundsData.hitSound`를 읽고 `Object.ToString`·`String.Concat`을 거쳐 `AudioManager.Play`로 이어진다. 종류별 이름 문자열 재생성이 후보다. | **예약 판정음마다**. Update 안에 있어도 매 프레임 문자열 생성이라고 볼 수 없음. | **중간**: 실제 형식·이름 규칙을 확인한 뒤 종류별 캐시 |
| 8 | `PlanetSprite.LateUpdate` | `meshRenderer.material` 조회 후 `unscaledTime`으로 `Material.SetFloat`. 변하는 시간 쓰기는 필요할 수 있지만 반복 머티리얼 조회는 캐시 후보다. | 활성 행성 Sprite마다 매 프레임. 더미 행성 포함 여부·개수 미확인. | **중간**: 머티리얼 교체 시 캐시 무효화 필요 |
| 9 | `TileFlash.Update` | 시간·지속시간을 읽고 `GetComponent` → `SpriteRenderer.color`, 종료 시 Destroy. 살아 있는 플래시마다 같은 Renderer를 다시 찾는 비용이 후보다. | 플래시 객체마다 생존 프레임 수만큼. 한 프레임에 많이 겹치는 경우 우선 측정. | **쉬움~중간**: Start/초기화 때 Renderer 확보, 객체 교체 확인 |
| 10 | `scrDecorationManager.LateUpdate` → `UpdateHitboxSizes` | 선택 장식 목록 순회 중 `Camera.main.orthographicSize`, Sprite bounds, 크기·위치·회전 쓰기. 호출당 공통 카메라 정보 재조회와 변화 없는 선택 장식의 반복 쓰기가 후보다. | **에디터의 선택 장식마다**, LateUpdate에서 보조 함수 호출. 실제 표시·선택 조건에 따라 다름. | **중간**: 우선 함수 호출 한 번 안에서만 카메라 정보 공유 |

### 후보별 검토·검증 포인트

1. **행성 충돌 조회:** 배열을 재사용하는 API로 대체하는 방안을 검토할 수 있다. 고정 길이 버퍼로 Collider를 잘라서는 안 된다. 버퍼 확장 때 누락 없이 다시 조회하고, 반환 순서와 트리거 호출 순서·횟수, 부모에 장식이 없는 Collider, 콜백 중 상태 변경, 다중 행성·무적·죽은 행성을 원래 결과와 대조해야 한다. 히트박스 없는 맵에서도 배열 조회 분기에 진입하는지는 미확인이다. `Dormancy`의 장식 목록 최적화와 별도 경로다.
2. **배경 막대:** `scrBgbarnew`와 다른 형식이다. 맵에서 생성되는 형식을 먼저 확인한다. `Renderer.material`이 호출마다 새 머티리얼을 만든다고 단정하지 않는다. 조회 캐시와 색 쓰기 생략을 별도로 측정하고 `Flash` 호출, 실패 높이 변화, 머티리얼 교체를 검증한다.
3. **장면 플래시 크기:** 카메라 zoom/aspect, 해상도 변경, 다시 하기 및 에디터 복귀가 모두 크기를 다시 반영해야 한다. 지난 입력만 기억하면 다른 코드가 바꾼 Transform을 놓칠 수 있다. 먼저 대상별 현재 값과 예정 값의 동일 비율을 제한된 표본으로 확인한다.
4. **카메라:** 일반 Transform·색 경로와 RT 경로를 분리해 검토한다. `RenderTexture` 생성 참조도 있지만 `camRTNeedsRecreation`이 있어 매 프레임 할당으로 분류하지 않았다. 시간·카메라 흔들림·이동 보간을 멈추거나 효과를 끄는 수정은 제안하지 않는다.
5. **플래시 색:** 종료된 구간만 조사하며, 박자마다 다시 시작하는 플래시·공유 정적 상태·다른 모드의 재질 변경을 확인한다. 끝난 페이드의 값을 다시 쓰는 것에도 의미가 있을 수 있다.
6. **보너스 글자:** `TextFix`의 `scrHUDText` 그림자 색 중복 처리와 다른 함수다. 색을 다른 코드가 덮어쓴 뒤 원래 Update가 금색으로 복구하는 경우를 보존해야 한다. setter 자체가 같은 값을 이미 거르는지도 확인한다.
7. **판정음 이름:** `SoundWarm`은 최초 오디오 로드·압축 풀기를 해결한다. 여기서는 이후에도 남을 수 있는 이름 만들기만 별도 후보로 보았다. 입력 시간·소리 예약 시각·볼륨은 그대로여야 한다. 몰리는 판정음의 할당량이 작으면 보류한다.
8. **행성 머티리얼:** 시간값은 매 프레임 달라지므로 같은 값 생략 대상으로 삼지 않는다. 캐시만으로 이득이 작은 경우가 예상된다. `MatReuse`의 FloorRenderer 머티리얼 누수 처리와 대상이 다르다. `sharedMaterial`로 바꿔 인스턴스별 효과를 공유시키면 안 된다.
9. **타일 플래시:** 색 페이드 자체와 Destroy 시각은 보존한다. `Start`에도 같은 종류의 조회가 보이므로 캐시 지점 검토가 가능하다. 아직 실제 생성 빈도와 초고사양 커스텀 맵의 사용 여부는 확인하지 않았다.
10. **히트박스 표시:** 충돌 판정 전체를 생략하는 제안이 아니다. 우선 한 번의 함수 실행 안에서 공통 카메라 크기를 읽는 횟수만 줄이는 방안을 검토한다. 카메라 변경, 장식 선택·드래그·확대·회전, Sprite 교체 시 표시가 원래와 같아야 한다. 곡 중 끊김보다 편집기 비용 후보여서 10위다.

## 추가 후보와 낮춘 이유

| 함수 | 관찰 / 빈도 추정 | 난이도·보류 이유 |
|---|---|---|
| `ADOFAI.KeyIndicator.LateUpdate` | 선택 UI의 `GetComponent`, 키 표시 graphics의 색 쓰기. 에디터 표시마다 프레임 단위. | 중간. 입력 상태 전환 때만 갱신 가능성은 있으나 UI 작성자·선택 변경 확인 필요. 편집기 중심. |
| `ffxMDHammer.Update` | 회전 갱신 뒤 `GetComponent`와 Sprite 색 쓰기. 활성 망치 효과의 moving/hit 분기. | 쉬움~중간. Renderer 캐시 후보지만 커스텀 맵에서 사용되는지 미확인. |
| `ADOFAI.FPSCounter.Update` | `ToString`·Concat·TMP text 쓰기, anchoredPosition·Z 쓰기. FPS 표시/기록 및 샘플 시간 조건 있음. | 중간. 문자열 갱신이 이미 주기 제한되어 있을 가능성. 프레임마다 문자 생성으로 단정하지 않음. |
| `SettingsMenu.Update` | `RDInput.GetMainPressKeys` → LINQ Select·ToList·First. 키 설정 편집 분기. | 쉬움~중간. 생성 델리게이트에는 정적 캐시 참조가 있어 매번 람다 할당한다고 볼 수 없음. ToList 결과 할당·입력 순서만 검토. 곡 중 우선순위 낮음. |
| `scrDateCountdown.Update` | DateTime·정수 ToString·Concat·Format·Text 쓰기. 날짜 카운트다운 표시 프레임. | 중간. 동일 표시 초에서 재포맷 후보지만 실제 호출 조건 확인 필요. 플레이용 맵과 관련성이 낮음. |
| `scrBadgeContainer.Update` | GetComponent, DisplayClass·TweenCallback 생성 참조, 지역화 문자열·TMP 쓰기. | 중간. 배지 상태 변경 분기일 수 있어 지속 프레임 할당으로 확정 못 함. |
| `scrFloor.UpdateGlowSortingOrder/UpdateIconSprite` | GetComponent 호출 확인. | 중간. 콜백은 아니므로 **실제 호출 빈도부터** 조사할 대상. 기존 타일 최적화와 겹치는지 추가 확인 필요. |

## 후보에서 제외하거나 과장하지 않은 항목

- `scrTextDecoration.SetCollider`, PACL2 동일 글자 재설정, `scrHUDText` 그림자 색: `TextFix`와 중복.
- `scrFloor.Update` 첫 DisplayClass 생성: `AllocFix`가 볼륨 모드 외 재사용을 이미 처리한다. 볼륨 모드의 콜백은 값을 붙잡으므로 단순 공유 불가.
- 비동기/레거시 키보드·마우스 입력의 LINQ·목록 할당: `AllocFix`의 기존 입력 처리와 중복.
- 장식 전체 LogicUpdate/CheckHitboxHit 순회, 위치 재계산: `Dormancy`·`MoveApply`·`InvisibleSkip`과 중복. 10번은 별도의 **에디터 히트박스 표시** 경로다.
- `scrParticleDecoration.Update`의 shape scale·simulation speed 반복 쓰기: `ParticleFix`와 중복.
- DOTween 재정렬, 타일 이동 트윈, 타일 메시 최초 생성, 최초 효과음 로드, 필터 Type.GetType, JIT, 히트미터 fade, 이미지 로드·압축, 장식 생성·재설정·누수: AGENTS.md와 현재 구현의 기존 해결을 새 후보로 재기재하지 않았다.
- `scrConductor.PropagateOnBeat`의 타일 순회: `BeatFix`가 이미 처리한다. Update가 호출한다는 이유로 모든 목록이 매 프레임 전체 순회된다고 보지 않았다. 예약음 목록에는 다음 인덱스 필드가 있다.
- `scrFloor.LateUpdate`: opacity/glow/위치의 지난 프레임 값과 변화 플래그가 이미 보인다. 더미 행성 목록 순회가 있다는 이유만으로 무조건 전체 타일 비용이라고 분류하지 않았다.
- `scnLevelSelect.Update`의 `FindObjectsByType` 및 `FindAnyObjectByType`: 특수 치트 입력 관련 참조와 함께 있어 상시 플레이 비용 후보에서 제외. DLL 전체 `FindObjectsOfType` 호출자는 0곳이었다. 이것이 간접 호출이나 다른 DLL까지 없음을 뜻하지는 않는다.
- `scrDifficultyText.Update/scrOffsetText.Update`: `GetKeyDown`·설정 저장/색 변경 경로라 조회·문자 만들기가 프레임마다 발생한다고 확정할 수 없다.
- `ffxJerkCam.Update`: `awakeFrame`과 현재 frameCount 조건이 보인다. 초기화 직후 한정일 수 있어 상시 Blur 컴포넌트 조회 후보에서 제외.
- `dfxFollowPlanet.Update`: 위치·시간은 매 프레임 변하는 작업이며, GetComponent/DOFade에는 `fadeTriggered` 상태가 있다. 종료 페이드 때 한 번일 가능성이 있어 반복 조회 상위 후보에서 제외.
- 같은 타일 스타일 건너뛰기(`FloorFix`)는 재제안하지 않는다. 입력이 같아도 다른 코드가 재질을 바꿀 수 있다는 AGENTS.md의 실패 사례를 모든 setter 생략 후보에 적용한다.
- 필터의 효과 없는 값/Blit 생략은 작업 1이며 이번 범위에서 제외. 화면을 끄거나 여러 기능을 광범위하게 비활성화하는 해결책은 제안하지 않는다.

## Claude 후속 검토 순서

먼저 1번의 충돌 결과 배열이 정상 플레이에서 실제 얼마나 생성되는지와 2번의 막대 형식·개수를 확인한다. 그다음 3~6번은 값이 같은 프레임 비율과 현재 setter의 중복 방지 여부, 다른 코드의 작성 지점을 확인한다. 조회 캐시와 동일 값 쓰기 생략은 독립적으로 검토한다.

측정은 자주 호출되는 함수 전체를 Stopwatch로 감싸지 않는다. 필요한 최소 집계 또는 제한된 표본을 사용하고, 곡 중 GC 중지 조건에서 힙 증가량을 할당량의 근거로 삼는다. 수정 후에는 같은 화면·입력·소리·충돌 결과 대조와 동일 조건의 `sf_ab` 4판 이상을 별도 실행한다. 평균 FPS뿐 아니라 곡 중 최악 프레임과 끊김 수를 본다. 새 Harmony/정적 이벤트/PlayerLoop/캐시를 추가할 경우 `Main.Unload`의 해제·초기화도 함께 검토한다.

이번 작업에서 저장소에 만든 파일은 이 문서 하나다. 코드 변경, 게임 실행, 빌드·설치, 커밋·푸시, 작업 목록 결과 절 변경은 하지 않았다. 후보의 실제 활성 빈도·할당량·비용·화면 동일성은 모두 후속 검증 대상이다.

## 실측 결과 (2026-10-14, HELLO 2026 곡 전체 170초, 60 FPS 제한, 자동 플레이, 3440x1440)

`wasteprobe`(호출 수 전부, 시간은 64번에 한 번) 결과. 합계는 표본 평균 x 호출 수의 추정이며 안쪽 호출 시간을 포함한다.

| 함수 | 초당 호출 | 한 번 | 프레임당 | 판단 |
|---|---:|---:|---:|---|
| scrConductor.Update | 60 | 206 µs | 0.206 ms | 대부분 자동 플레이의 입력·타일 판정 처리(UpdateInput 0.209 ms, Hit 0.14 ms). 낭비 아님 |
| scrPlanet.Update | 1,439 (프레임당 24) | 1.0 µs | 0.025 ms | 작음 |
| PlanetSprite.LateUpdate | 1,439 (프레임당 24) | 1.4 µs | 0.034 ms | 작음 |
| scrCamera.Update | 60 | 5.7 µs | 0.006 ms | 무시 |
| scnGame.Update | 60 | 4.0 µs | 0.004 ms | 무시 |
| scrFlash.Update | 60 | 3.0 µs | 0.003 ms | 무시 |
| scrBgbar / scrTextBonusColor / TileFlash / UpdateHitboxSizes | 0 | - | 0 | 이 맵에서 안 불림 |

**결론**: 후보 10개의 합이 프레임당 약 0.07 ms(곡 중 60 FPS 기준 0.4%)로, 고칠 값이 없다. 이 정적 후보 방식은 접는다.
**따로 나온 것**: `AudioSource.GetSpectrumData` 가 프레임당 1번, 72.6 µs(0.073 ms)다. 기존 옵션 `LowNoFft`(음악 반응 계산 끄기, 기본 끔)가
이것을 없앤다(켜면 곡 중 11,322번 중 4번만 돎). 프레임당 이득 0.07 ms, 400 FPS(프레임 2.5 ms)에서는 3%.
