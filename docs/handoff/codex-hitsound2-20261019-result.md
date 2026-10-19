# 히트음 형식별 예약 묶기 작업 결과

작성: 2026-10-20. 기준: 작업 시작 시 HEAD의 HitSoundBatch 최신 구현.

## 완료 범위

- `HitSoundBatch.cs`: 모노·스테레오를 형식별로 분리해 합산·업로드·예약하고, 그룹별 자원 정리와 진단/검증을 지원한다.
- `HitSoundMix.cs`: 지원 형식을 명시적으로 검사하는 순수 PCM 정책을 추가했다.
- `tools/HitSoundCheck/Program.cs`: 혼합 목록·그룹 정렬·지원하지 않는 형식·14,262개 이벤트 검증을 추가했다.
- `StutterFix.cs`의 `HitSoundBatch = false`를 유지했다.
- 게임 실행, Mods 설치, 커밋, 푸시를 하지 않았다. 다른 모드 코드를 읽거나 복사하지 않았다. 기존 사용자 변경은 유지했다.

## 그룹과 예약 설계

한 곡의 입력 샘플 레이트가 같을 때만 허용한다. 따라서 유효한 형식 키는 `(공통 frequency, channels)`이고, channels 1과 2가 각각 별도 그룹이 된다. 최대 2그룹이며, 서로 다른 입력 레이트·3채널 이상·8,000~96,000Hz 범위 밖·홀수 레이트는 원래 경로로 복귀한다. 홀수 레이트 제외는 0.5초 구간을 정수 샘플 경계로 고정하기 위한 보수적인 제한이다. 출력 48,000Hz와 입력 44,100Hz의 차이는 허용한다.

각 그룹은 원래 순서를 유지한 Voice 배열, 완료 큐, 재사용 PCM 버퍼, AudioClip 16개와 AudioSource 16개를 가진다. 모노는 모노, 스테레오는 스테레오 클립에 그대로 SetData 한다. 업믹스와 출력 레이트 변환은 FMOD에 맡긴다. 직접 채널 변환·리샘플링·정규화·클리핑은 추가하지 않았다.

모든 그룹이 같은 Origin, Rate, Frames=Rate/2, 총 구간 수를 공유한다. 구간 n의 시작은 항상 `Origin + (double)n * Frames / Rate`이다. 원래 목록에서 얻은 시작 샘플도 공통 Origin으로 반올림한다. 따라서 그룹 간 구간 경계 차이는 0샘플이며, 개별 이벤트의 원래 절대 시각과 샘플 시각 사이에는 기존처럼 최대 약 0.5샘플의 양자화가 있다.

작업 스레드 하나가 같은 구간의 그룹들을 순서대로 렌더한다. 메인은 가장 이른 미예약 구간부터, 동률이면 그룹 생성 순서로 업로드한다. 두 그룹을 서로 다른 프레임에서 올려도 PlayScheduled에 전달하는 시작 시각은 동일하다. 최초 인계 전에는 모든 그룹의 첫 구간이 실제로 예약됐는지 확인한다. 준비 실패 때 일부 예약만 남지 않도록 전체 소유 자원을 정리한다.

## 비용과 실패 상한

| 항목 | 상한/동작 |
|---|---|
| 음원·클립 | 그룹당 16개, 전체 최대 32개 |
| 구간 | 0.5초, 그룹별 같은 샘플 경계 |
| SetData | 전체 그룹 합계 프레임당 최대 2회, 최대 768,000바이트 |
| 44,100Hz 모노+스테레오 한 구간 | 합계 264,600바이트 |
| 예약 선행 | 최대 6초, 풀은 8초 분량, 재사용 전 50ms 여유 |
| 기한 | 시작까지 20ms 미만인 미예약 구간은 복귀 |
| 최초 렌더 대기 | 준비 중만 최대 250ms, Update에서는 기다리지 않음 |
| 입력 클립 | 최대 64종, 클립당 최대 8초, 로딩 대기는 기존처럼 클립당 최대 200ms |
| 목록/길이 | 기존 상한 200,000개 / 2시간 |
| PCM 예산 | 입력 캐시 + 재사용 float 버퍼 + 생성 AudioClip의 float PCM 추정 합계 64MiB |

PCM_bytes는 기존 입력 캐시에 더해 풀의 관리/네이티브 PCM 추정치도 포함하도록 바뀌었다. 이 예산은 Unity/FMOD 전체 메모리, 원본 AudioClip의 내부 메모리, 객체/목록/큐/진단 배열의 오버헤드를 모두 포함하는 프로세스 메모리 상한은 아니다. 예산 검사는 풀을 만들기 전에 한다.

매 프레임 새 PCM 배열이나 Voice 배열을 만들지 않는다. 그룹 선택은 최대 2그룹만 순회하며 Stopwatch 계측은 추가하지 않았다. 큐와 버퍼는 재사용한다. 다만 이 사실과 업로드 바이트 상한만으로 실게임 메인 스레드 비용이 원래 개별 예약보다 작다고 확정할 수는 없다.

정지·재시작·오디오 설정 변경·언로드에서는 모든 그룹의 소유 음원만 정지하고 소유 클립·루트를 정리한다. 작업/검증 스레드 종료 대기는 기존처럼 메인 밖에서 한다. 런타임 복귀 시 꼬리 재생 오프셋은 각 원본 클립의 frequency로 계산한다.

## state와 verify

`hitsound state`의 추가/변경 항목:

- `groups`: 활성 형식 그룹 수.
- `formats`: 예: `44100Hz/1ch:next=12,44100Hz/2ch:next=12`. next는 각 그룹의 다음 미예약 구간이다.
- `pool`: 그룹을 합친 사용 중 음원 수 / 전체 음원 수.
- `SetData_last`, `SetData_peak`: 전체 그룹을 합친 최근 Pump 프레임의 호출 수와 설치 이후 최고치. 기존 항목을 그룹 전체에 적용한다.
- `SetData_bytes_last`, `SetData_bytes_peak`: 같은 기준의 업로드 바이트 수와 최고치. 시간 측정값은 아니다.
- `segments`: 공통 시간 구간 수. `rendered`, `scheduled`는 그룹별 처리 건수의 합이므로 완료 시 `segments * groups`가 된다.
- `PCM_bytes`: 위 예산에 포함되는 입력 캐시와 풀 PCM의 추정 합계.

state/verify 명령은 기존처럼 DEV 또는 AUTOTEST 빌드에서 제공한다. `hitsound verify`는 그룹마다 원래 목록의 절대 시각·볼륨으로 기준 Voice를 재구성하고 PCM을 대조한다. 실제 렌더 작업 스레드의 PCM 해시도 그룹별로 보관·비교한다. `group_segments`, `groups`, `group_alignment_samples=0`, `differing_samples`, `hash_mismatches`, `pending_actual`로 진행을 확인한다. group_alignment_samples는 공통 경계 설계에 따른 값이며 오디오 장치 출력 측정값이 아니다. 아직 선행 렌더되지 않은 구간의 pending_actual은 정상적으로 남을 수 있다.

## 오프라인 검증 결과

| 검사 | 결과 |
|---|---|
| Dev / Player / PlayerAuto 빌드 | 각각 오류 0, 경고 36 |
| LoadCheck Dev / Player / PlayerAuto | FieldRefAccess 261 / 229 / 261곳, 틀림 각각 0 |
| LoadCheck 동적 이름 | 각 빌드 5곳은 실행 중 확인 대상이라는 기존 한계 유지 |
| CompatCheck Dev / Player / PlayerAuto | 게임 멤버 341 / 302 / 341개, 누락 각각 0 |
| 기본 PCM 검증 | 649,053샘플 대조, 차이 0, 재사용 렌더 할당 0바이트 |
| 혼합 PCM 검증 | 각 레이트별 모노 10개+스테레오 10개, 전체 누적 대조 2,119,803샘플, 차이 0 |
| 그룹 정렬 | 8,000 / 44,100 / 48,000 / 96,000Hz를 각각 검사, 경계 차이 0샘플 |
| 거절 정책 | 입력 레이트 혼합, 3채널, 홀수 레이트, 범위 밖 레이트 모두 거절 |
| 14,262개 합성 이벤트 | 2그룹, 그룹 구간 1,142개, 차이 0, Render 할당 0바이트 |
| 합성 스트레스 시간 | 전체 1,295.24ms, Render 합계 531.99ms, 단일 Render 최대 1.866ms |
| 합성 PCM+풀 추정 | 8,581,188바이트. PCM은 그룹별 재사용, 이벤트 메타데이터는 별도 |
| check-ui-encoding.py | SFENC001: clean |
| git diff --check (변경 코드) | 통과 |

혼합 검증에는 시작 오프셋 0, 겹침, 다음 구간으로 이어지는 꼬리를 넣었다. 기대값은 전체 타임라인에 직접 누적하며, 구간 렌더 결과와 샘플별로 비교한다. 독립 Verify 경로와의 비교도 수행했다. 스트레스 목록은 20ms 간격과 9,499프레임 클립을 사용한 합성이며 실제 HELLO 목록을 읽은 결과는 아니다. 시간은 이 PC의 .NET 8 오프라인 단회 관측이며 Mono·Unity·FMOD 성능을 보장하지 않는다. 테스트의 할당 0은 순수 Render 호출에 한정한다.

실행 명령:

```powershell
dotnet build StutterFix.csproj -v q --nologo
dotnet build StutterFix.csproj -v q --nologo -p:Edition=Player
dotnet build StutterFix.csproj -v q --nologo -p:Edition=Player -p:AutoTestBuild=1
dotnet run --project tools/HitSoundCheck -c Release
py -3 tools/check-ui-encoding.py
# 각 bin/Debug, bin/Player, bin/PlayerAuto/StutterFix.dll에 대해:
dotnet run --project tools/LoadCheck -c Release -- <DLL>
dotnet run --project tools/CompatCheck -c Release -- <DLL> <게임Managed폴더>
```

## Claude가 이어서 확인할 순서

1. Quartz 없이, 별도로 승인된 실게임 환경에서 새 빌드를 설치한다. 이 작업에서는 설치하지 않았다. 기본 꺼짐을 확인하고 시험 설정에서만 HitSoundBatch를 켠다.
2. HELLO 짧은 시험 후 `hitsound state`: `captures>=1`, `reason=captured`, 혼합 목록이면 `groups=2`, 형식이 44100Hz/1ch와 44100Hz/2ch인지 확인한다. SetData_peak<=2, SetData_bytes_peak<=768000, pool 전체<=32와 fallbacks 증가 여부를 확인한다. captures는 누적이므로 현재 reason과 그룹도 함께 본다.
3. 별도 정확성 시험에서 `hitsound verify`를 실행하고 진행 상태, PCM 차이 0, hash_mismatches=0을 확인한다. 진단 스레드가 CPU를 사용하므로 성능 A/B에는 verify를 함께 돌리지 않는다.
4. 실제 청취로 모노/스테레오 음량·중앙 위치·겹침·꼬리·구간 접합부를 확인하고, 일시정지/재개·다시 하기·언로드 및 복귀 시 누락/중복도 확인한다.
5. Quartz 없이 같은 조건으로 HELLO 전곡 HitSoundBatch 끔/켬 A/B를 각각 새 게임에서 수행한다. 평균 FPS와 함께 최악 프레임, 끊김 수, state의 실제 캡처/복귀/업로드 최고치를 기록한다. 인계 문서의 평균 190→224 근처는 기대값이며 이번 작업의 측정 결과가 아니다.

입력 형식을 유지해도 여러 원본 음원을 그룹별 소수 음원으로 합산하므로 FMOD의 음원별 처리·부동소수점 합산 순서·가상 음원 선택·클립 경계 리샘플링이 달라질 가능성은 남는다. PCM 차이 0과 공통 예약 시각은 최종 출력음의 동등성을 증명하지 않는다. 청취와 출력 측정에서 문제가 확인되면 기본 꺼짐과 원래 경로 복귀를 유지한다.

## 문서 인코딩

이 문서는 apply_patch로 첫 글자에 U+FEFF를 넣어 UTF-8 BOM으로 작성했다. 저장 후 EF BB BF, 엄격한 UTF-8 디코딩, 한글 문구 및 대체 문자 0개를 확인했다. check-ui-encoding.py도 SFENC001: clean으로 통과했다.
