# DecoTrack 조사 결과 — 2026-10-19

레포 내부용. 아래 A/H/W는 의뢰 문서의 세 실제 맵을 뜻한다. 맵 이름·경로·통계는 외부 공개용 자료에 옮기지 않는다.

## 1. 결론과 조사 범위

**새 효과와 자동 변환은 설계할 수 있다. 우선안은 기존 MoveDecorations의 의미를 유지하는 압축 실행 명령 + DecoEngine 장식별 실행기다.** 수천 장식을 하나의 효과 데이터로 묶어 태그 검색·효과 객체·속성 디코딩·호출 사슬을 줄일 수 있다. 서로 다른 장식의 최종 상태 계산과 Transform 반영까지 O(1)이 되는 것은 아니다. 보이는 장식 N개의 값이 달라지면 적어도 O(N)의 상태/출력 작업은 남는다.

첫 대상 A는 MoveDecorations 95,211개, 이벤트–장식 연결 229,536건이다. 단순 속성·비반복 후보는 94,867개(99.639%)지만, **완성된 시작값→끝값 구간을 정적으로 굽는 데 성공한 비율로 해석하면 안 된다.** 이번에는 파서·구조 통계와 소스/IL 의미를 조사했으며, 게임 실행이나 동일성 검증은 하지 않았다. 완전 정적 굽기의 실제 성공 비율은 미확정이다. 비트 동일 검증을 마친 변환 이벤트는 0개다.

PACL2의 선례는 실제 DLL에서 확인했다. 별도 enum의 큰 정수 값을 게임 enum처럼 사용하고, 이벤트 정의 표·이름 변환·컴포넌트 생성·아이콘·번역을 연결한다. 다만 새 이벤트만 저장한 파일은 모드 없는 환경에서 정상 작동한다고 볼 수 없다. 기본 파일에는 기존 이벤트를 유지하고, 압축 결과를 메모리/별도 캐시로 보관하는 방법을 우선 권한다.

이번 작업의 실행 범위: Python 읽기 전용 맵 분석, 기존 ILScan 실행, 추가 분기 확인을 위한 ilspycmd 원시 IL 읽기, 조사용 Python/작은 증거 텍스트/본 문서 작성. **.cs·csproj 변경, 게임 실행, Mods 설치, 빌드, 커밋은 하지 않았다.** 작업 시작 전부터 존재하던 DecoEngine 관련 변경은 유지했다. PACL2는 이름·등록 방식·호출 관계만 결과에 남겼으며, 원시 IL 임시 복사본은 제거했다. PACL2 소스나 에셋을 제품에 복사하지 않았다.

## 2. 입력과 재현 방법

주 분석기: `tools/decotrack-research/analyze.py`, `details.py`. 전체 분포는 `out/summary.json`, 보수적 후보·속성 조합·관련 이벤트·수식 후보는 `out/details.json`에 있다. 실행 명령은 다음과 같다.

```powershell
py -3 tools/decotrack-research/analyze.py
py -3 tools/decotrack-research/details.py
```

원본 맵을 UTF-8-sig로 읽고 문자열 바깥 후행 쉼표만 제거한다. A에는 actions 배열과 decorations 사이 쉼표가 누락되어 그 정확한 경계만 메모리에서 보정했다. W에는 문자열 안 실제 줄바꿈이 있어 `json.loads(..., strict=False)`로 읽었다. 원본은 쓰지 않았다. 태그는 공백으로 나눈 이름의 합집합이며, 한 이벤트가 여러 태그로 같은 장식을 가리켜도 연결은 한 번만 센다. 존재하지 않는 태그는 인덱스에 추가하지 않는다.

여기서 대상 수는 **파일상 태그 연결 수**다. 실제 게임에서 이미지 실패·비활성 장식·다른 모드·동적 태그 처리까지 반영한 살아 있는 Unity 객체 수가 아니다. RepeatEvents 확장 전 원본 actions를 센다. null 축은 속성 사용에서 제외한다. 예를 들어 `[null,null]`은 위치/시차 이동 0축이며, 0과 null은 다르게 처리한다. 색은 한 속성 슬롯, scale은 X/Y 슬롯, parallax 배율은 DecoAnim과 같이 Vector2 한 슬롯으로 센다. 길이 0에도 가상 슬롯 수를 계산했으므로 실제 생성 Rec 수와 같지 않다.

| 입력 | 바이트 | SHA-256 |
|---|---:|---|
| A: 의뢰 문서의 42MB FULL | 42,350,829 | `026873b89f05ea94b86b83634a938ac053593ebea6cb832d8134e4a8084f8064` |
| H: 의뢰 문서 폴더의 level.adofai | 4,935,616 | `11945eca2d39e1760a3d4b80325fd2497f6bc1c95666e4a8d2ec14d0e12b4bd8` |
| W: 의뢰 문서의 .adofai | 7,188,207 | `bbf26648bfe8781b0404fa44e17159eb8bf1f465a31cdcee09a9175857030e6f` |

게임 `Assembly-CSharp.dll` SHA-256: `6ba2cfb8d260962d3c8a3e4409f60e8e98293ca9b2d2eca4875096bd02d40230`. PACL2는 `ModsOff/PACL2/PACL2.dll` 525,312바이트, SHA-256 `387fad37bb25c54c0d5af0e925e969cf0e5ebcf128b624bfbbc26973d94f3ca4`를 읽었다. 현재 활성화된 모드라는 뜻은 아니다.

ILScan은 `METHOD=<정확한 타입 이름>`, `FIELDS=1`로 실행했다. 이 버전의 METHOD 필터는 타입 부분 문자열 검색이 아니다. `GlobalArray`/`EventButton` 검색 출력이 비었다고 해당 기능이 없다고 판단하지 않았다. 실제 확인된 정의 표는 `GCS.levelEventsInfo`, 버튼은 `ADOFAI.LevelEventButton`이다. ILScan은 문자열/분기 상수를 출력하지 않으므로 그 부분만 기존 ilspycmd의 원시 IL로 보완했다. 게임 원시 IL 발췌는 `out/game-raw-selected.txt`, `game-raw-extra.txt`, 재추출기는 `il_extract.py`다. 후자는 기존 `%TEMP%/decotrack-game-il.txt`를 입력으로 읽으며 DLL을 실행하지 않는다.

## 3. 실제 맵 구조

### 3.1 수량과 연결 분포

| 항목 | A | H | W |
|---|---:|---:|---:|
| 전체 actions | 108,617 | 18,818 | 6,876 |
| decorations | 28,835 | 2,531 | 6,290 |
| MoveDecorations | 95,211 | 2,867 | 2,257 |
| 장식이 실제 갖는 서로 다른 태그 | 32,035 | 1,172 | 766 |
| 태그당 장식 중앙값 / p95 / 최대 | 1 / 10 / 22,960 | 1 / 6 / 200 | 1 / 36 / 1,296 |
| 이벤트당 대상 중앙값 / p95 / 최대 | 1 / 10 / 14,269 | 1 / 11 / 247 | 1 / 15 / 1,296 |
| 이벤트–장식 연결 합계 | 229,536 | 11,374 | 19,352 |
| 장식당 받은 이벤트 중앙값 / p95 / 최대 | 8 / 8 / 139 | 3 / 11 / 44 | 3 / 5 / 122 |
| 장식당 이벤트 0건 | 11 | 18 | 8 |
| 한 이벤트에 적힌 태그 최대 | 28 | 87 | 264 |

| 이벤트당 대상 수 구간 | A | H | W |
|---|---:|---:|---:|
| 0 | 0 | 91 | 428 |
| 1 | 86,494 | 1,563 | 1,350 |
| 2–9 | 3,789 | 1,038 | 344 |
| 10–99 | 4,910 | 162 | 96 |
| 100–999 | 11 | 13 | 37 |
| 1,000 이상 | 7 | 0 | 2 |

| 장식당 받은 이벤트 수 구간 | A | H | W |
|---|---:|---:|---:|
| 0 | 11 | 18 | 8 |
| 1 | 54 | 41 | 89 |
| 2–9 | 27,619 | 2,329 | 6,146 |
| 10–99 | 1,150 | 143 | 46 |
| 100 이상 | 1 | 0 | 1 |

A의 주된 구조는 작은 태그/개별 장식 이벤트가 매우 많고, 일부 큰 공통 태그가 전체 장식을 건드리는 형태다. 최대 대상 14,269개가 있다고 평균 프레임마다 그만큼 처리한다는 뜻은 아니다. 의뢰 문서의 동시 약 3,000개는 이전 실측 단서이며 이번 정적 분석으로 재측정하지 않았다.

### 3.2 간격, duration, ease

| 파일상 좌표 통계 | A | H | W |
|---|---:|---:|---:|
| `(floor, angleOffset)` 정렬 후 floor 차 중앙값 / p95 / 최대 | 0 / 0 / 266 | 0 / 20 / 704 | 0 / 2 / 127 |
| 같은 floor 내 angleOffset 차 표본 수 | 93,865 | 2,314 | 2,023 |
| 같은 floor 내 차 중앙값 / p95 / 최대(도) | 0 / 36 / 5,760 | 0 / 180 / 17,280 | 0 / 90 / 9,999,999 |
| 같은 floor·같은 angleOffset 인접 쌍 | 84,407 | 2,038 | 1,039 |
| duration 0 / 양수 | 29,965 / 65,246 | 1,146 / 1,721 | 1,584 / 673 |

duration은 파일의 박자 값이다. A의 상위 값은 0:29,965, 16:27,420, 8:25,684, 4:8,074, 32:2,451. H는 0:1,146, 16:396, 8:246, 32:231, 24:148. W는 0:1,584, 1:164, 16:94, 8:63, 4:53. 매우 긴 값(A 최대 20,000, H 4,444,444, W 55,555)도 있어 곡 끝 전에 끝나지 않는 구간을 별도로 다뤄야 한다.

ease 상위는 A Linear 38,777 / OutCubic 29,778 / InCubic 16,322 / OutQuad 5,081 / InQuad 4,963, H Linear 1,478 / OutCirc 653 / InCirc 171 / OutElastic 167 / OutExpo 83, W Linear 1,718 / OutCirc 172 / OutCubic 62 / InCirc 39 / OutQuad 32다. W에는 Flash 계열도 있다. 단순 Linear 전용 구현만으로 세 맵을 대체할 수 없다. 전체 duration/ease 히스토그램은 summary.json에 남겼다.

**위 간격은 초 단위 실행 간격이 아니다.** 다른 floor에 큰 angleOffset이 붙으면 floor 순서와 실행 순서가 달라질 수 있다. 실제 시간은 scrFloor.entryTime·speed와 이벤트 생성/스크럽 경로를 써야 한다. 원시 IL의 `ffxPlusBase.SetStartTime`은 entryTime(double)에 degreeOffset의 라디안 환산 / PI × `60 / (bpm × floor.speed)`를 계산해 더한다. `ffxMoveDecorationsPlus.Decode`는 duration × crotchet를 쓴다. 반면 `StartEffectWithOffset`은 conductor BPM·song.pitch·floor.speed를 읽어 DelayedCall을 만든다. 수식을 대수적으로 단순화하면 반올림과 경로 차이를 놓친다.

이번에는 타일 전체의 entryTime을 독립 재구현하지 않았다. 따라서 초 단위 간격/전체 동시 활성 수는 미산출이다. A/H/W에 각각 SetSpeed 113/1,595/379, Twirl 966/5,160/136, Pause 8/47/19가 있고 W에는 Hold 21개도 있다. floor 차를 고정 BPM으로 나눠 초라고 제시할 수 없다. 자동 변환은 게임이 만든 타일 시간표와 RepeatEvents 확장 결과를 받은 뒤 하는 것이 우선이다.

### 3.3 실제 속성과 상대 모드

| null 축 제외한 주된 속성 조합 | A | H | W |
|---|---:|---:|---:|
| 위치+회전+크기 | 73,712 | 세부 JSON 참조 | 세부 JSON 참조 |
| 위치+회전 | 4,996 | 세부 JSON 참조 | 세부 JSON 참조 |
| 색만 | 14,832 | 56 | 31 |
| 위치만 | 134 | 827 | 86 |
| 불투명도만 | 345 | 590 | 385 |
| 크기만 | 277 | 227 | 85 |
| 회전만 | 33 | 207 | 112 |
| 피벗만 | 0 | 122 | 50 |
| 시차 오프셋만 | 0 | 49 | 35 |
| 위 수치 속성이 하나도 없음 | 267 | 488 | 1,232 |

마지막 행은 효과 전체가 빈다는 뜻이 아니다. visible·이미지·깊이·마스크만 바꾸기도 한다. H/W에서 parallaxOffset 필드 자체는 2,705/2,060개 이벤트에 있지만 `[null,null]`이 많아 필드 존재만 세면 이동량을 크게 부풀린다. H의 유효 조합 최다는 위치만 827개이며, W는 수치 속성 없음 1,232개다. details.json의 effective_combinations가 전체 분포다.

부가 필드: A 이미지 309·깊이 14. H visible 947·이미지 202·깊이 4·마스크 깊이 관련 각 6. W 이미지 727·visible 659·깊이 8·maskingType 3·useMaskingDepth 1. 한 이벤트에 중복될 수 있으므로 합산해서 제외 이벤트 수로 쓰지 않는다. A에는 pivotOffset/parallax/parallaxOffset가 없다. H에는 실제 피벗/시차 오프셋 사용이 있고 W에는 parallax 배율도 있다.

MoveDecorations의 relativeTo는 A 95,211개 전부 생략, H 2,867개 전부 생략, W 2,255개 생략·Global 2개다. 명시적 LastPosition과 랜덤 이동 필드 사용은 세 맵 모두 0이다. **생략을 Tile/Global로 고정하면 안 된다.** LevelEvent.Decode는 생략 속성을 disabled로 두면서 value_default를 채우고, 효과 Decode는 movementType 값과 movementTypeUsed를 따로 읽는다. FastMove.Run도 배치 변경 여부와 LastPosition 분기를 따로 판단한다. 굽기는 런타임 정의의 기본값과 disabled 상태를 함께 보존해야 한다. 이번 메타데이터 조사로 리소스의 기본값 자체를 확정하지 않았으므로 기본 상대 모드별 정적 성공률을 만들지 않았다.

초기 장식 relativeTo는 A Tile 28,716 / CameraAspect 72 / Camera 42 / Global 5, H Tile 1,694 / Camera 834 / RedPlanet 2 / Global 1, W Tile 6,095 / Camera 152 / Global 37 / CameraAspect 3 / RedPlanet 2 / BluePlanet 1이다. 이것은 초기 배치 분포이며 이후 효과나 엔진 eligible 수와 같지 않다.

### 3.4 반복, 필터, 동적 결합

| 항목 | A | H | W |
|---|---:|---:|---:|
| eventTag가 비어 있지 않은 이동 | 34 | 212 | 430 |
| RepeatEvents | 13 | 43 | 13 |
| 이동 eventTag와 RepeatEvents tag 교집합 이벤트 | 34 | 89 | 57 |
| SetFilter / SetFilterAdvanced | 1,538 / 0 | 1,014 / 830 | 608 / 535 |
| SetConditionalEvents | 0 | 0 | 1 |

교집합은 파일 전체 태그를 보수적으로 연결한 수치다. RepeatEvents가 실제 어느 범위에서 무엇을 몇 번 복제하는지는 확장하지 않았다. H/W에는 Beat/Floor 반복이 섞여 있어 repetitions만 곱하면 안 된다. 필터는 별도 이벤트로 순서를 유지한다. 같은 시각 필터와 이동이 있다고 필터가 장식 값의 원인이라고 판단하지 않는다.

MoveDecorations 내부 random 이름 필드는 모두 0개다. 맵 전체에서는 H randomTextureTiling 55·randomSeed 8, W 115·78이 있으나 주로 다른 장식/입자 속성의 값이다. 이 숫자를 랜덤 이동 제외 건수로 사용하지 않았다. 세 파일 actions에는 PACL2의 FollowDecoration/OrbitDecoration/Program/변수 이벤트 이름이 없다. 설치된 외부 모드의 런타임 쓰기까지 없다는 증명은 아니다. W의 조건 이벤트 1개와 히트박스 eventTag 실행은 정적 시간표와 별도 검토 대상이다.

## 4. 굽기 가능 비율과 의미 보존

### 4.1 계산 가능한 후보 비율

보수적 구조 후보를 다음처럼 정의했다: 유효 속성이 위치·회전·크기·색·불투명도 중 일부, 유효 피벗/시차 속성 없음, 이미지/visible/마스크/깊이 등 부가 필드 없음, relativeTo 명시 없음, RepeatEvents 태그 교집합 없음. 생략 기본값은 그대로 남기며 시작값은 실행 때 바인딩한다. 후보는 엔진 eligible 조건을 검사한 결과가 아니다. inactive·빈 대상은 원래 활성/태그 처리를 보존해야 하며 본 통계에서 전체 raw 이벤트 분모는 유지했다.

| 구조 후보 | A | H | W |
|---|---:|---:|---:|
| 위 조건의 이벤트 | 94,867 (99.639%) | 1,447 (50.471%) | 717 (31.768%) |
| 그중 duration 0 | 29,741 (31.237%) | 341 (11.894%) | 305 (13.514%) |
| 그중 duration 양수 | 65,126 (68.402%) | 1,106 (38.577%) | 412 (18.254%) |
| 후보 이벤트–장식 연결 | 222,259 | 4,489 | 8,230 |
| 후보 중 즉시 이동 연결 | 88,042 | 1,465 | 5,654 |

이 비율이 뒷받침하는 것은 **작은 실행 명령으로 미리 디코딩해 둘 수 있는 영역이 A에 크다**는 판단이다. `시작 시각+길이+ease+시작값+끝값`을 전부 고정하는 굽기 가능률은 별도다. 초기 Setup 상태, disabled 기본값, 반복 확장, 실제 실행 순서, Kill/Startup, 외부 쓰기를 재현하지 않고 99.639%를 완전 굽기 성공률로 부를 수 없다. duration 0도 상대 위치·Kill 완료·색/가시성 부작용 때문에 무조건 안전한 상수 대입이 아니다.

이번 조사로 완전 정적 굽기가 불가능하다고 판정한 비율도 확정하지 않았다. 특히 **겹침은 자동 탈락 사유가 아니다.** 현재 KillKey는 이전 키를 `Kill(true)`로 끝내므로, 이전 끝값을 재현할 수 있으면 겹친 효과의 다음 시작값도 정적으로 계산할 가능성이 있다. 반대로 단순히 이전 구간을 새 시작 시각에서 잘라 보간값을 채택하면 원래 의미와 달라진다.

### 4.2 꼭 보존할 코드 의미

| 경계 | 현재 증거와 설계 요구 |
|---|---|
| 태그 대상 순서 | FastMove는 태그 순서→각 태그 목록 순서→처음 등장한 장식 순서를 따른다. 분석기의 set는 건수 산출용이다. 실제 변환기에 set 순서를 사용하면 안 된다. |
| 이벤트 내부 순서 | FastMove.Run: 배치→위치X/Y→시차오프셋X/Y→피벗X/Y→회전→크기X/Y→색→불투명도→시차배율→보임→깊이→이미지 계열. 속성별로 전 이벤트를 모아 재정렬하지 않는다. |
| Kill 후 시작값 | DecoAnim.KillKey는 `Kill(true)`. Pos는 Kill 후 현재 pivotPosVec를 FStart로 읽고, 인자로 받은 startAxis+target을 FEnd로 둔다. LastPosition용 startAxis는 FastMove에서 그 전에 읽는다. 읽기 위치를 Kill 뒤로 모두 옮겨도 달라질 수 있다. |
| Startup | Pos/Rot/Col/Opa/Piv/ParOff는 생성 때 읽는 시작값이 있다. Scale X/Y는 첫 Startup에서 scaleVec를 읽는다. Par 배율은 생성 때 Vector2를 담는다. 모든 키에 같은 시작값 바인딩 시점을 적용하지 않는다. |
| 완료 | 위치·회전 등은 OnUpdate 뒤 OnComplete에서 FEnd를 다시 쓴다. Scale은 해당 OnComplete setter가 없고 Eval 결과를 유지한다. ease(duration)==1이라고 가정해 모든 끝값을 target으로 덮지 않는다. |
| 시간 | 현재 UpdatePrefix의 DOTween delta 선택·timeScale·정지 임계값과 `Pos += td` 누적을 유지한다. `songTime - bakedStart`나 프레임 번호로 바꾸는 것은 별도 방식 변경이며 비트 동일을 전제할 수 없다. |
| 프레임 순서 | 같은 프레임 여러 이벤트, 새 기록 생성 순서, 첫 Step/Startup, DOTween Update와 매니저 LateUpdate 순서를 보존한다. 다른 floor의 이벤트도 같은 프레임에 몰릴 수 있다. |
| 가시성 | GetVisible 변화·forceHide·색 alpha·opacity는 Update 단계의 즉시 setter/미루기와 Late의 위치 반영에 영향을 준다. 숨김 장식의 상태 시계까지 멈추면 안 된다. |
| 미룬 쓰기 | MoveApply.SkipForLate는 실제 지난 매니저 프레임, hitbox, GetVisible 등을 검사한다. Late 전에 숨겨지면 ManagerLatePostfix가 보충 갱신한다. 이 경계를 건너뛰는 출력은 별도 검증해야 한다. |
| 외부 소비자 | 마스크·히트박스·부모 타일·행성/카메라 상대 배치, 외부 setter 패치, PACL 추적 효과가 있으면 필드/Transform의 중간 상태도 관찰 가능하다. 최종 위치만 같아도 충분하지 않다. |
| 멈춤/되돌리기 | Kill(false)는 현재값에서 버리고 Kill(true)/Complete는 완료 경로다. PlayingTweens/스크럽/Goto가 보는 기록·프록시와 재생 시작 대기를 보존한다. |
| 재시작/맵 교체 | SceneReset에서 DropAll 후 ExitFix의 ResetScene 기준 스냅샷/가벼운 복원에 맞춘다. 맵 로드·장식 파괴·에디터 수정·Unload 시 트랙 커서/소유권/태그 연결도 폐기한다. 준비 끝 스냅샷으로 무조건 복원하지 않는다. |

주요 소스 위치: `FastMove.cs:536,586,607`, `DecoAnim.cs:190,238,271,318,362,395,460,477,519,539,585`, `MoveApply.cs:255,276,317`, `ExitFix.cs:133,147,161,215,342`. 파일은 병행 작업 중이므로 메서드 이름을 우선 기준으로 삼는다.

같은 장식·같은 키·같은 floor 안에서 파일 angle 좌표만 비교한 인접 쌍 중 nominal 겹침 후보는 A 15,449 / H 124 / W 54건, 같은 시작 좌표는 156,322 / 1,600 / 441건이다. 이는 duration×180 범위와 angleOffset을 비교한 정적 진단이다. 다른 floor·반복·실제 시간표·부가 setter까지 포함한 전체 겹침률은 아니며, 변환 불가능 건수로 쓰지 않는다.

Mono float 계산은 현행 ApplyE의 식 모양, 저장/반환 시점, `ZeroTween.Eval` 호출을 유지한다. .NET/Python에서 double로 endpoint를 미리 계산해 둔 결과를 곧바로 게임의 float 결과로 쓰지 않는다. easing 캐시는 ease뿐 아니라 Pos/Dur/Over/Period와 실제 부동소수점 비트·시작 세대가 같을 때만 공유한다.

## 5. 세 압축 형태의 비교와 비용

기호: E=원본 이동 이벤트, B=이벤트–장식 연결, S=속성 슬롯 연결, D=장식 수, A(t)=그 프레임 활성 장식 수, K(t)=활성 속성 수. 세 맵의 S는 595,267 / 10,392 / 26,331이다. 아래 바이트 수는 **추정 layout**에 통계를 대입한 것이며 실제 Mono 힙 측정값이 아니다. 참조/리스트/문자열/배열 헤더/정렬/프록시/반복 확장 메모리는 별도다.

| 구조 | 데이터·메모리 추정 | 프레임/열기 비용 추정 | 판단 |
|---|---|---|---|
| 1. 장식별 구간 배열+커서 | 슬롯 구간 48B, 장식 인덱스 (D+1)×4B 가정: A 27.359MiB / H 0.485 / W 1.229. 64B×D 활성 상태를 전 장식에 두면 별도 1.760 / 0.154 / 0.384MiB | 열기 O(B+S), 미정렬이면 장식별 정렬 추가. 매 프레임 O(A(t)+K(t)), 커서는 구간 전환 때만 이동. Transform O(보이는 대상) | 장식별 엔진에 적합하나 이벤트를 장식별로 펼치면 데이터가 오히려 늘 수 있다. 상태 의존 시작값은 런타임 바인딩 슬롯으로 둔다. |
| 2. 이벤트 한 항목+N개 대상 | 이벤트 64B+대상 int32 4B×B 가정: A 6.687MiB / H 0.218 / W 0.212. 대상별 시작값/실행 상태는 별도 | 열기 O(E+B), 같은 명령 내 디코딩/ease 메타데이터 공유. 프레임은 공유 시계 G(t)+대상별 상태·출력 O(K(t)+A(t)) | 우선안. 기존 한 이벤트가 N개 대상을 갖는 의미를 그대로 유지하면서 장식별 엔진에 연결한다. 시작값까지 한 벌로 공유할 수는 없다. |
| 3. 등차 등 수식 패턴 | 패턴당 기준값·증분·길이·대상 인덱스. 패턴이 정확하면 O(패턴 수+대상 목록), 정확하지 않으면 원본 예외 저장 | 패턴 탐색 O(E)~정렬 비용. 재생 중 수식 계산을 하면 곱셈·반올림 비용과 비트 위험, 열기 때 풀면 1/2와 같은 크기로 돌아감 | 현 자료에서는 후순위. 생성기가 원래 수식을 제공하는 새 효과에는 유용할 수 있다. |

동일 `(floor,angleOffset,duration,ease)` 그룹은 A 23,986 / H 1,777 / W 1,541개다. 메타데이터 공유 항목 수 기준으로 E 대비 74.81% / 38.02% / 31.72% 줄일 여지는 있다. **이징 호출/FPS가 그 비율만큼 줄었다는 뜻은 아니다.** 키 구성·Startup·실제 초 단위 시간·Over/Period·프레임 phase가 다를 수 있다.

파일상 연속 이벤트 중 clock와 키 구성이 같은 것을 합친 run 수는 93,354 / 2,498 / 1,998개, 감소율은 1.95% / 12.87% / 11.48%다. A에서 멀리 떨어진 이벤트들을 정렬해서 한 덩어리로 실행하면 더 많이 합쳐지지만 실행 순서를 바꿀 수 있다. **immutable clock/payload 표만 공유하고 원래 dispatch 순서는 남긴다.** floor/angle가 다르지만 실제 시각이 같은 그룹까지 찾은 수치는 아니다.

수식 후보는 같은 clock·유효 속성 모양의 단일 대상 이벤트를 파일 순서로 놓고, 모든 숫자 성분의 연속 차가 Python double에서 정확히 같은 길이 3 이상 구간만 찾았다. A 17구간/90이벤트, H 4/25, W 5/26이다. 일정값도 등차 0으로 포함하며 전체 패턴 존재량의 하한 탐색 결과다. 이 숫자는 의미 보존 압축 성공 건수가 아니다. 재생성 시 Mono float 비트, 대상 순서, 시작 상태까지 다시 검증해야 한다. 이 탐색 범위에서 수식화만으로 A의 95,211개를 크게 줄인다는 근거는 없다.

열기 비용은 `Tnew = Tparse + Ttimeline + Ttags + Tcompile + Tdecorate + Tother`로 나눠 측정해야 한다. 파싱 후 모든 LevelEvent와 ffx를 그대로 만든 다음 트랙까지 추가하면 열기가 빨라지리라는 보장이 없다. 처음에는 기능 동일성을 위해 그 방식을 허용하되, 열기 개선 단계에서는 기존 ffx/Rec의 **중복 생성을 실제로 생략**해야 한다. 장식 프리팹·이미지 해독·업로드 비용은 트랙으로 자동 제거되지 않는다. 이번 Python 실행 시간은 모드의 열기 비용 추정에 사용하지 않았다.

프레임 절약식은 `기존 태그/효과 시작 비용 + 기존 기록별 진행·setter 사슬 - 압축 dispatch - 엔진 상태 진행 - 남는 Transform - 폴백 비용`이다. 새 효과라는 이름 자체가 성능 개선의 근거가 되지 않는다. 장식당 µs와 FPS/끊김 감소량은 이번 작업에서 미측정이다.

## 6. 게임 이벤트 시스템과 PACL2 선례

### 6.1 게임 쪽 연결

현재 게임의 `ADOFAI.LevelEventType`은 정적 enum이다. 런타임에 CLR enum 멤버 자체를 추가하는 API를 기대할 수 없다. 대신 해당 정수 값을 enum 형식으로 취급하고 관련 변환/테이블을 연결하는 방식이 가능하다.

`GCS.levelEventsInfo`는 이름→LevelEventInfo, `GCS.levelEventTypeString`은 타입→문자열이다. LevelEventInfo는 type/name/propertiesInfo/categories/executionTime/allowFirstFloor/isDecoration/useGroups 등을 가진다. PropertiesPanel은 PropertyInfo의 타입·controlType·enumType·기본값·제약을 바탕으로 컨트롤을 만든다. 현재 버전은 GlobalArray라는 이름을 그대로 전제로 삼을 필요가 없다.

편집기 `scnEditor.LoadEditorProperties`는 카테고리를 준비하고 levelEventsInfo를 순회해 프리팹을 복제한 뒤 LevelEventButton.Init을 부른다. Init은 enum.ToString으로 info를 찾고 GCS.levelEventIcons[type]을 읽는다. 이름/아이콘이 빠지면 표에 이벤트 하나만 넣어도 안전하지 않다. 카테고리 탭에는 LevelEventCategory와 CategoryTab 경로가 연결된다.

`scnGame.ApplyEvent`가 floor·crotchet를 설정하고 ffxPlusBase.Decode, SetStartTime, plusEffects 등록, eventTag/히트박스 연결, runManually/sourceLevelEvent 처리를 한다. 실제 시작은 ffxPlusBase/플러스 효과 재생 경로에서 이뤄진다. 새로운 엔진을 붙일 때도 이 시간표·조건/수동 실행·원본 이벤트 연결을 보존해야 한다. scrLevelMaker의 타일 생성/entryTime 결정까지 DecoTrack 자체가 중복 구현하는 것은 초기 범위에서 제외한다.

### 6.2 PACL2에서 확인한 방식

근거는 `out/pacl-types.txt`, `pacl-CustomEventsFeature.txt`, `pacl-CustomEventsPatches.txt`, `pacl-CustomLevelEventInfoFactory.txt`, `pacl-LevelEventInfoBuilder.txt`의 이름/호출 요약이다. 구현 소스/원시 IL/에셋은 인용·재배포하지 않는다.

| 역할 | 확인된 PACL2 구성과 방식 |
|---|---|
| 고유 ID | CustomLevelEventType 33,333,333–33,333,342. FollowDecoration 33,333,339, OrbitDecoration 33,333,340. CustomLevelEventCategory 7,777–7,779. enum 정의 자체를 게임 DLL에 추가하지 않는다. |
| 등록/해제 | CustomEventsFeature.AddCustomEvents가 MakeLevelEventInfo를 호출하고 GCS.levelEventsInfo·levelEventTypeString에 추가, EditorConstants.soloTypes에 합친다. RemoveCustomEvents는 제거/ExceptWith. enable/disable 시 편집기 백업을 동반한 장면 재설정 경로를 호출한다. |
| 속성 정의 | CustomLevelEventInfoFactory.MakeLevelEventInfo와 LevelEventInfoBuilder.MakeProperty/Build. 기본값·범위·단위·제약·enum/텍스트를 게임 PropertyInfo로 구성한다. PropertiesPanel 전체를 새로 만들지 않는다. |
| 이벤트 생성 | CustomEventsPatches.ScnGameApplyEventTranspiler가 ApplyEvent의 컴포넌트 생성 부분에 CreateComponentFromEvent 연결을 넣는다. factory는 별도 ffxPlusBase 하위 타입을 AddComponent한다. 알려지지 않은 종류에는 null 반환 분기가 있다. |
| enum 문자열/열거 | RDUtils.ParseEnum의 LevelEventType/Category 특수 경로, System.Enum.GetValues/ToString에 대응하는 패치가 있다. 원래 구현 재진입을 제어하는 플래그도 있다. 전역 API 패치이므로 다른 모드와의 충돌·재진입 위험이 있다. |
| 타입 탐색 | Type.GetTypePrefix가 PACL2 어셈블리 타입 탐색을 보완한다. 기존 문자열 기반 enum/속성 타입 연결을 만족시키기 위한 선례다. StutterFix가 동일 범위를 전역 패치해야 한다는 결론은 아니다. |
| 아이콘/탭 | scnEditor.Start의 LoadLevelEventSprites/LoadLevelCategorySprites 로컬 함수들에 postfix를 등록해 GCS.levelEventIcons/eventCategoryIcons를 채운다. `pacl2_customEventIcon_` 계열 이름으로 자체 bundle의 자산을 불러온다. 자산 복제는 하지 않는다. |
| 번역 | RDString.GetWithCheck prefix에서 JALib Localization.TryGet을 이용한다. LevelEventInfoBuilder는 자체 번역 키를 구성한다. 새 효과는 별도의 StutterFix 키/번역/아이콘이 필요하다. |
| 저장/의존성 | LevelData.EncodePrefix가 UpdateRequiredMods를 호출한다. 커스텀 효과가 있으면 levelSettings.requiredMods에 PACL2 의존성을 반영한다. CheckModsDependency prefix는 지원하는 모드 이름을 제거해 게임 검사를 통과시키는 구조다. |

추가 확인: PACL2 FollowDecoration은 대상 태그들을 PrepVfx에서 준비하고, OrbitDecoration은 StartEffect에서 pivotPosVec·rotAngle을 직접 읽으며 별도 Tween을 만든다. 외부 모드가 없는 것으로 가정한 지연 필드 게시/고정 트랙과 충돌할 수 있다. DecoEngine 결과 문서의 “PACL DLL 미확보” 부분은 이번에는 ModsOff DLL을 실제 읽었으므로 후속 설계에서 보완할 수 있다. 현재 DecoEngine의 PACL 로드 시 전체 보류 정책을 이번 조사만으로 해제할 근거는 없다. 추적 콜백의 전체 읽기/쓰기 목록과 실게임 조합 검증은 별도로 필요하다.

### 6.3 모드가 없는 사람이 여는 경우

IL로 확인한 현재 버전의 경로는 다음과 같다. 실제 실행 시험은 하지 않았다.

1. `LevelData.Decode`는 requiredMods가 남아 있으면 CheckModsDependency 결과로 조기 반환한다. 기본 CheckModsDependency는 남은 배열 길이가 0보다 큰지 검사한다. 따라서 PACL2 의존성이 정상 저장된 맵은 모드 없는 기본 환경에서 의존성 문제로 로딩이 거부되는 경로를 가진다. 화면의 정확한 문구는 미검증이다.
2. requiredMods를 빼도 호환이 보장되지 않는다. LevelEvent.Decode는 알 수 없는 eventType 문자열을 ParseEnum에서 None(0)으로 받고 결과 1을 반환한다. Dictionary 생성자는 그 Decode 결과를 버리며 info를 채우지 않는다. 이어 LevelData.Decode가 info.taroDLCCheck/isActive를 읽는 경로가 있어 null 참조 위험이 있다. “알 수 없는 효과만 조용히 무시한다”는 설계를 해서는 안 된다.
3. 임의의 숫자를 문자열 eventType으로 저장하면 Enum.TryParse가 성공해도 info 사전에 그 문자열이 없어 조회 실패할 수 있다. 큰 정수만 지정하는 것으로 저장 호환이 해결되지 않는다.

권장 저장 정책은 **원본 .adofai의 구 MoveDecorations를 보존하고 압축 실행 결과는 메모리/내용 해시 기반 별도 캐시에 두는 것**이다. 캐시 무효화 키는 맵 내용·게임/모드 버전·기본 속성 스키마·반복/시간표·옵션·외부 writer 구성이다. 캐시 오류는 원본 실행으로 돌아간다. 에디터 변경 후 undo/redo에도 캐시 세대를 올린다.

“새 효과 + 구 효과를 actions에 둘 다 저장”은 그대로는 이중 실행/모드 없는 로딩 실패를 만든다. 새 효과 본체를 기존 게임이 무시한다고 확인되지 않은 actions에 넣지 않는다. 안전한 이중 표현 후보는 (a) 별도 sidecar에 편집용 묶음/압축 메타데이터, actions에는 구 효과만, (b) 저장 때 구 효과로 확장하는 호환 export다. sidecar가 없어도 움직임은 남고 편집용 묶음 정보만 사라지게 한다. 루트 사용자 확장 필드를 쓰는 대안은 게임 저장 후 보존 여부가 미검증이므로 기본안으로 채택하지 않는다.

## 7. 권장 구조와 DecoEngine 통합

가장 작은 런타임 변경 후보는 새 enum 도입 없이 기존 MoveDecorations를 읽고, 원본 LevelEvent/ffx와 연결된 불변 명령을 만드는 것이다. 명령에는 원본 순번·floor/time 참조·duration/ease/disabled 마스크·대상 순서 배열·raw target과 바인딩 정책을 둔다. 단순 구간은 컴파일하고, 이미지/마스크/조건/수동 실행/외부 writer는 원래 경로에 남긴다. 효과 시작 경계에서 이전 키 Complete와 현재값 바인딩을 한 뒤 엔진 슬롯에 공급한다.

저장 데이터는 2번 구조(이벤트별 명령+대상 목록), 실행 상태는 1번 구조(장식별 최대 키 슬롯+커서)를 결합한다. 이벤트 객체 하나에 장식 N개의 서로 다른 시작 상태까지 담아 공유하려 하지 않는다. 불변 descriptor는 공유하되 시계/완료/소유권은 의미가 같은 대상에만 공유한다.

DecoEngine 인계의 목표 키 1·2·5·7·8은 위치·회전·크기다. 현재 결과 문서의 1단계 구현은 **Rec 참조를 소유 객체의 슬롯에 연결하며, 첫 Step/Startup은 기존 경로로 실행하고 이후 입장**한다. 완성된 baked 배열을 직접 받는 API가 이미 있는 것은 아니다. 초기 연결은 기존 FastMove→Rec 생성→엔진 입장 경로를 이용해 디코딩/태그 압축만 검증한다. 이후 별도 승인된 구현 단계에서 descriptor→엔진 슬롯 바인딩 API를 추가해 Rec/프록시 중복 생성 자체를 제거한다. 기존 코드와 트랙의 매 프레임 정렬 병합 루프를 새로 만들지 않는다.

색 9·opacity 10·피벗 3/4·시차 11–13은 현재 엔진의 제한/기존 경로를 따른다. 알파가 변하는 색/불투명도와 피벗/시차가 섞이면 장식 전체가 엔진에서 제외될 수 있다. A의 구조 후보 99.639%나 초기 Tile 장식 수를 실제 엔진 소유율로 바꾸어 말하면 안 된다. 이탈 시 현재 상태·남은 구간·원본 순서·eventTweens 관찰 가능성을 원래 경로로 넘긴다. 중간에 명령이 일부 실행된 뒤에는 처음부터 원본을 재실행해 이중 적용하지 않는다.

새 효과를 이벤트 추가 칸에 넣는 단계에서는 PACL2와 같은 표/PropertyInfo/아이콘/번역/factory 연결 방식을 우선 참고한다. 충돌 검사한 별도 ID와 StutterFix 소유 키를 사용하고 가능하면 기존 카테고리를 재사용한다. System.Enum/Type 전체 패치 범위는 최소화하고, 패치/표 항목/아이콘/정적 참조를 Main.Unload에서 모두 해제한다. PACL2와 같은 ApplyEvent 부위에 transpiler를 둘 경우 순서·명령 패턴 검증·실패 시 원본 복귀가 필수다.

편집기에서의 가장 작은 사용자 기능은 “선택한 기존 이동들을 묶어 트랙 편집기로 표시, 저장은 구 이동 유지”다. 처음부터 기존 MoveDecorations 정의 전체를 바꾸거나 복제해서 같은 type으로 서로 다른 info를 등록하면 로딩·복사/붙이기·undo가 섞일 위험이 있다. 실제 신규 eventType 노출은 저장 정책을 정한 뒤 구현한다.

## 8. 4단계 로드맵과 검증 기준

아래는 이득 대비 구현 순서다. 가장 위험한 완전 고정 굽기를 먼저 제품에 넣지 않고, 큰 구조 이득을 시험할 수 있는 단계부터 진행한다. 모든 성능 이득은 추정이며 이번에는 실행하지 않았다.

| 단계 | 작업·주요 위험 | 예상 이득 식 / 통과 기준 |
|---|---|---|
| 1. 원본 보존 명령 컴파일 | 반복 확장·시간표 이후 대상/속성/순서를 압축한다. 시작값은 현재 경로에서 읽는다. 상대/조건/외부 writer는 폴백. 위험 중간 | 절약≈반복 디코딩·태그 순회·클로저/ffx 준비 중 실제 생략한 비용−컴파일 비용. 원본 순번·대상 순서·키/disabled·시간 비트 전수 비교 0, 열기 총시간/단계/추가 메모리 측정. |
| 2. DecoEngine 직접 공급 | 장식별 슬롯을 공유 descriptor에서 바인딩, 기존 기록별 호출 사슬·중복 객체를 제거한다. 첫 Startup/Complete/프록시 가시성이 큰 위험 | 절약≈기존 Rec/사전/호출 사슬−슬롯 bind/update/폴백. 기존 경로 대비 프레임 값·Transform/parallax 비트 비교 0. 실제 소유 장식/기록 비율과 이탈 비용 측정. 인계의 2.9ms→합계 1.8ms 기준은 이전 구간 목표이며 이번 성능 결과가 아니다. |
| 3. 증명 가능한 구간만 완전 정적 굽기 | Setup→키별 Kill(true)→Startup을 해석해 고정 시작/끝값을 구한다. 외부 쓰기/조건/스크럽/미확정 기본값은 남긴다. 위험 가장 큼 | 절약≈바인딩·키 교체·중복 상태 생성−상태 복원/캐시 비용. 변환율은 검증 통과 이벤트/전체 원본 및 확장 후 전체를 따로 기록. 한 비트라도 다르면 해당 구간/장식 폴백. 수식 압축은 실제 이득을 보일 때만 추가. |
| 4. 편집기 새 효과/묶음 UI | PACL2 방식으로 버튼·패널·아이콘·번역 연결, 기본 호환 export/sidecar. 형식·외부 배포 위험 큼 | 제작자가 만드는 E/B 감소와 열기/메모리 개선을 측정. 모드 유무·저장 재열기·복붙·undo/redo·reload/unload·버전 충돌을 검증. 모드 필수 파일은 사용자가 허용한 별도 모드에서만. |

검증은 기능/성능 판을 분리한다. 기능 판에서는 기존 경로와 같은 입력으로 Pos/Dur/ease·시작/끝·키 완료·필드·Transform·parallax 비트를 비교한다. 전체 상태 해시인 곡 끝 decosig도 대조하되, 중간 상태 차이를 놓치므로 그것 하나로 통과시키지 않는다. 동시 이벤트·겹침·동일 키 교체·duration 0/장기 구간·null 축·색 alpha/숨김 전환·피벗/시차·마스크/히트박스·LastPosition·카메라 상대·반복 Beat/Floor·조건/수동 실행을 경계 사례로 둔다.

수명 판은 일시정지/재개, 임의 타일부터 시작, seek/scrub, 실패/다시 하기, 에디터 복귀/수정/undo, 같은 맵 재열기, 다른 맵 교체, Ctrl+F5/Unload를 포함한다. Kill(false)/Kill(true)/Complete/PlayingTweens 관찰 결과, 태그 계획의 세대·파괴 참조·캐시 무효화를 비교한다. 외부 모드 조합은 특히 PACL2 활성/비활성 새 프로세스 비교가 필요하다.

성능 판은 검증/스크린샷 없이 같은 디스플레이·설정·빌드에서 새 프로세스 ABBA로 한다. 세 맵 전곡의 최악 프레임·끊김 수·프레임 분포, 열기 벽시계와 단계, 힙/할당/추가 캐시, 엔진 소유율·폴백율을 함께 낸다. 프로파일용 계측을 기록마다 상시 넣지 않는다. 표본/구간 집계로 측정 자체의 끊김을 구분한다. 이전 인계의 짧은 구간 결과를 새 효과의 전곡 개선 근거로 재사용하지 않는다.

## 9. 사용자 결정이 필요한 항목

이번 조사 중에는 결정을 요청하거나 구현을 진행하지 않았다. 다음 구현 전에 정할 항목은 다음과 같다.

1. 모드 없는 플레이를 기본 보장할지, 별도로 “StutterFix 필수 맵” 형식을 허용할지. 권장 기본은 원본 구 이벤트 유지다.
2. 새 효과가 기존 움직임과 비트 동일해야 하는지, 새로운 제작 전용 의미/시간 기준을 허용할지. 임의 오차 허용이나 시간 방식 변경은 조사자가 정하지 않는다.
3. 에디터 UX를 기존 이벤트 묶음 편집부터 제공할지, 실제 새 eventType 버튼까지 바로 노출할지.
4. 압축 캐시의 디스크/메모리 예산과 실패 시 원본 로딩 비용을 허용할지. 원본과 캐시를 동시에 유지하면 일시 메모리가 늘 수 있다.
5. PACL2 동적 추적/변수와의 호환을 최초 범위에 포함할지. 현재 엔진의 보수적 제외를 유지하면 지원 범위와 이득이 줄어든다.

## 10. 미확정 사항과 완료 확인

미확정: 완전 고정 시작→끝 구간의 실제 굽기 성공률, 반복 확장 후 이벤트 수·초 단위 간격·동시 활성 수, 리소스 기본 relativeTo 값, 실제 엔진 eligible 비율, 게임 실행 중 비트 동일성·열기/프레임 성능, 모드 없는 로딩 UI의 정확한 메시지. 구조 통계·IL 경로와 분리해서 후속 검증해야 한다.

조사 산출물은 원본 맵을 변경하지 않는다. out의 통계/메타데이터 증거는 1MiB 미만이다. 결과 문서는 apply_patch로 첫 문자 U+FEFF를 포함해 작성했고, 작성 후 UTF-8 BOM 바이트·엄격한 UTF-8 디코딩·한글 대표 문장·대체 문자 유무를 확인한다. 기존 소스 변경분과 설치 상태는 이 작업의 결과로 취급하지 않는다.
