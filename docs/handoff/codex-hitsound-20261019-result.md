# 히트음 예약 묶기 구현 결과 — 2026-10-19

기본 꺼짐인 독립 실험 기능 `HitSoundBatch`를 추가했다. 게임 실행·제어, Mods 설치, 커밋·푸시는 하지 않았다. PCM 산술 및 정적 호환 검사는 통과했으나, 실제 맵에서의 캡처 성공·청감·성능·누수·Quartz 병용은 아직 검증하지 않았다. 이 문서는 게임 시험을 이어 받기 위한 결과다.

## 변경 파일과 사용 방법

- `HitSoundBatch.cs`: 목록 캡처, 작업 스레드, 음원 풀, 예약, 복귀와 정리.
- `HitSoundMix.cs`: Unity와 분리한 PCM 합산 및 독립 대조 계산.
- `Compat.cs`: Quartz의 정확한 히트음 postfix 일시 제외·복원, 기존 Optimizer 설정 읽기에 `RenderAllHitSounds` 표시 추가.
- `StutterFix.cs`, `StutterFix.csproj`: 설치·Unload·설정 연결. `Settings.HitSoundBatch = false`.
- `SettingsWindow.cs`: **플레이 > 실험 > 히트음 예약 묶기 (실험)**. 한국어·영어 `T()` 적용.
- `AutoTest.cs`: DEV/AUTOTEST의 `hitsound state`, `hitsound verify`, `hitsound compare`.
- `tools/HitSoundCheck`: 제품 합산 코드를 직접 연결하는 오프라인 검증 도구.

SoundWarm과 별도 설정이며 SoundWarm의 켜짐·꺼짐을 바꾸지 않는다. 실험을 켜도 진행 중인 Quartz 재생을 중간에 인수하지 않는다. 다음 `PlayHitTimes` 준비부터 적용한다.

## 게임 동작과 IL 근거

현재 Managed의 `Assembly-CSharp.dll`을 ilspycmd로 정적 분석했다. 게임 디컴파일 및 IL은 `%TEMP%/sf-hitsound-*`에만 보관했으며 저장소에 넣지 않았다. 다른 모드 바이너리나 소스는 이번 구현에서 디컴파일하거나 복사하지 않았다. Quartz의 기존 동작은 인계 및 저장소의 기존 분석 문서를 입력으로 삼았다.

| 접점 | 확인 내용 |
|---|---|
| `scrConductor.HitSoundsData` | private 값 형식에 `HitSound hitSound`, `double time`, `float volume`, `bool played`. 준비가 만든 목록만 읽는다. |
| `PlayHitTimes` | 이미 소리를 준비한 적이 있으면 `AudioManager.StopAllSounds`. 보정·연습 구간·체크포인트·타일 SetHitsound·자유 이동·행성 수 변경을 반영해 목록을 만든다. `IL_028f`에서 `nextHitSoundToSchedule = 0`. |
| 시각 | 기본 타일은 `dspTimeSongPosZero + entryTimePitchAdj - hitSoundOffsets[key]`. `dspTimeSongPosZero`에 `addoffset / song.pitch`가 포함된다. 원본의 절대 DSP 시각을 그대로 받으며 오프셋을 다시 더하지 않는다. |
| 음량 | 맵의 `hitsoundVolume * 0.01`, 타일 SetHitsound.volume, 자유 이동의 가중치, 행성 변경의 0.6 배율이 목록에 반영된다. 합산 전 개별 AudioSource.volume의 0..1 범위로 제한한다. 합산 결과는 제한·정규화하지 않는다. |
| `scrConductor.Update` | `hasSongStarted && isGameWorld`, 실패 상태가 아닐 때 목록을 진행한다. 히트음 루프 `IL_024a..02b9`, `IL_0263`의 5초 선행 비교, `IL_02a4`의 `AudioManager.Play`, `IL_02b3`의 인덱스 증가를 확인했다. |
| `AudioManager._Play` | `pitch = 1` (`IL_000f`), 지정 그룹 또는 fallback 그룹, 개별 volume (`IL_0035`), priority 128, `PlayScheduled(time)` (`IL_0044`). 곡 피치를 히트음 PCM에 다시 곱하지 않는다. |
| 범위 | 기본/SetHitsound/커스텀 소리 모두 원본과 같은 `FindOrLoadAudioClip("snd" + hitSound)` 결과를 받는다. GetData로 읽을 수 없는 커스텀 파일이면 전체 인수를 포기한다. |
| 홀드·카운트다운 | `holdSoundsData`, `extraTicksCountdown`과 시작/끝 심벌은 별도 경로다. 이 목록과 인덱스·음원은 손대지 않는다. |
| 일시정지 | controller.audioPaused가 `AudioListener.pause`를 바꾼다. 소유 음원은 `ignoreListenerPause=false`이고 DSP 예약과 함께 동결된다. 재개 때 시각을 새로 더하지 않는다. |
| 중간 시작 | ScrubMusicToTime이 원본 소리를 멈추고 기준 시각을 변경한다. Scrub/DesyncFix가 다시 PlayHitTimes를 호출한다. 새 준비마다 이전 세대를 취소한다. |
| 정리 | StopAllSounds, Rewind, Restart, FailAction, OnLandOnPortal, QuitToMainMenu, SwitchToEditMode, 씬 변경과 오디오 장치 변경에서 해제한다. KillAllSounds 코루틴의 두 StopAllSounds도 이 경로를 탄다. |

SoundWarm은 더 이른 ApplyEventsToFloors 뒤 클립을 로드한다. FfxReuse의 PlayHitTimes transpiler는 원본 준비 내부에 적용된다. 새 기능은 원본 준비가 끝난 뒤 완성된 목록만 읽으므로 이 두 기능을 대체하지 않는다.

## 설계와 처리 상한

1. 메인 스레드가 새 목록·절대 시각·음량과 클립 PCM을 확보한다. 모든 조건을 통과하고 최초 PCM이 준비되어 예약에 성공한 뒤에만 원본 인덱스를 목록 끝으로 옮긴다. 실패하면 원본 목록과 예약 경로를 유지한다.
2. 한 세대당 BelowNormal 작업 스레드 하나가 약 0.5초 구간을 합산한다. 구간을 넘는 클립 꼬리도 다음 구간에 넣는다. Unity API를 작업 스레드에서 호출하지 않는다.
3. 최초 준비에서 최대 250ms만 작업 결과를 기다린다. 최초 두 구간이 준비되지 않거나 DSP 여유가 50ms 미만이면 포기한다. Update에서는 작업 결과를 기다리지 않는다.
4. 최대 약 6초 앞을 예약한다. 메인 스레드의 SetData/PlayScheduled는 **Time.frameCount 기준 합계 최대 2회**다. 준비 직후 같은 프레임의 Update도 같은 예산을 쓴다. 20ms 이내로 예약 시한이 다가오면 늦은 구간을 재생하지 않고 복귀한다.
5. 풀은 AudioSource 16개와 재사용 AudioClip 16개다. 원본 프리팹을 복제해 원본의 나머지 속성을 유지하고 그룹·pitch 1·volume 1·priority 128을 지정한다. 재사용은 이전 구간 종료 후 50ms 이상 지난 슬롯에 한한다.
6. 클립들은 동일한 샘플 주파수·동일한 1/2채널이어야 한다. 자체 리샘플링/채널 변환은 하지 않는다. 공통 원본 주파수로 만든 합산 클립의 장치 출력 변환은 Unity에 맡긴다. 개별 원본과 구간 클립의 리샘플러 경계까지 동치인지는 미검증이다.

입력 PCM 캐시는 **64MiB, 고유 클립 최대 64개**, 클립 하나 최대 8초다. 목록은 최대 200,000개, 전체 구간은 최대 2시간, 주파수는 8~96kHz다. 최대 96kHz/stereo일 때 작업 버퍼 16개와 Unity 구간 클립 16개의 PCM은 약 **11.72MiB**다. DEV 검증은 별도 두 버퍼 약 0.73MiB와 목록별 검증 자료/구간 해시를 사용한다. 이벤트 배열·객체·Unity 관리 비용과 기존 게임 클립 메모리는 이 PCM 숫자에 포함하지 않는다. 곡 전체 PCM은 만들지 않는다.

워커는 취소를 4096샘플마다 확인한다. 종료 시 소유한 소스·생성 클립·루트만 멈추고 Destroy한다. 백그라운드 정리가 워커 종료를 기다린 뒤 핸들과 캡처 참조를 놓는다. 게임 메인 스레드에서는 Join하지 않는다. 이전 세대 정리가 끝나지 않았으면 새 캡처를 거절한다. Unload는 자체 Harmony ID와 정적 이벤트도 해제한다. Unity Destroy는 프레임 끝 처리이고 워커 해제도 비동기이므로, 실제 게임에서 메모리 복귀와 누수 0 확인은 남아 있다.

## 미지원·복귀 조건

| 조건 | 동작 |
|---|---|
| 빈 목록, 이미 진행된 인덱스, 히트음 비활성, 게임 월드 아님 | 인수하지 않음 |
| 과거/비정상 시각, 비정상 음량, 시간순이 아닌 목록 | 인수하지 않음. 원본 목록을 정렬해 바꾸지 않음 |
| 스트리밍/압축 유지 클립, 미로드, GetData 실패, 비정상 PCM | 인수하지 않음. 클립 로드 설정은 바꾸지 않음 |
| 혼합 주파수·혼합 채널, 3채널 이상, 캐시/길이/목록 상한 초과 | 인수하지 않음 |
| 원본 프리팹의 3D/spatialize, 루프, pan, playOnAwake, listener 무시 | 인수하지 않음 |
| 프리팹 reverbZoneMix가 0이 아님, 추가 컴포넌트·자식 존재 | 보수적으로 인수하지 않음. **실제 기본 프리팹이 이 조건에 걸리는지 미확인** |
| AudioManager.Play/_Play/MakeSource에 다른 Harmony 패치 존재 | 그 음원 경로를 재현하지 않고 인수하지 않음 |
| 재생 중 곡 pitch·DSP 기준·hitSoundVolume·mixer 그룹·장치 출력 주파수·목록 참조/개수 변경 | 소유 예약 해제 후 원본 경로로 복귀 |
| 워커 오류, 초기 준비 시간 초과, SetData 실패, 구간 예약 시한 초과 | 원본 경로로 복귀하고 이유·횟수 기록 |
| 알 수 없는 Quartz 히트음 postfix 구조/중복 소유권 | 우리 캡처를 포기하고 해당 패치를 임의 제거하지 않음 |

중도 해제는 원본 목록에서 현재 DSP 시각 이후 첫 항목으로 인덱스를 돌린다. 설정 끄기·일반 런타임 복귀에서는 이미 울리는 클립 꼬리를 원본 AudioManager.Play와 timeSamples로 이어 주는 경로가 있다. 미래 개별 예약은 원래 Update가 이어서 한다. 실패·재시작·곡 끝·씬 전환·장치 변경처럼 원래 멈추는 경로에서는 꼬리를 다시 만들지 않는다. **중도 복귀의 DSP 버퍼 경계, timeSamples 적용, 다음 Update까지의 짧은 공백은 청감 확인이 필요하다.**

일시정지는 파괴·재예약 대신 Unity의 DSP 동결을 사용한다. 재생 중 목록 원소를 외부 모드가 같은 참조·같은 개수로 직접 바꾸는 경우는 매 프레임 전수 검사하지 않으므로 미지원이다. 이 경우 원본 PlayHitTimes 재준비가 필요하다.

## Quartz 병용과 순서

실제 확인 기준은 `Harmony.GetPatchInfo(PlayHitTimes)`의 owner `quartz.module.optimizer`와 `HitSoundRendererPatches.PlayHitTimesPatch.Postfix`의 정확한 형식이다. 우리 설정이 켜졌을 때만 해당 postfix **하나**를 일시 제외한다. owner 전체·다른 메서드는 해제하지 않는다. Optimizer 옵션이 꺼져 있어도 같은 postfix가 등록돼 있으면 제외하지만, 그 옵션 값과 설정 파일은 바꾸지 않는다.

- Quartz가 먼저 설치됨: 우리 설정 적용/설치 때 postfix를 제외하고 다음 준비부터 우리 캡처를 시도한다.
- 우리가 먼저 설치됨: PlayHitTimes prefix에서 늦게 등록된 Quartz postfix를 알아채 제외한다. 이미 실행 중인 이전 Harmony wrapper에는 그 postfix가 남을 수 있으므로 **그 호출에서는 우리 캡처를 미룬다**. Quartz 또는 원본이 그 판을 맡고 다음 준비부터 우리 기능을 시도한다. 이 분기는 이중 예약을 피하기 위한 것으로, 최초 판부터 항상 우리가 우선한다는 뜻은 아니다.
- 우리 캡처 실패: Quartz postfix는 우리 설정이 켜진 동안 제외된 상태로 남으며 **게임 원본 개별 예약**을 사용한다.
- 우리 기능 끄기/Unload: 우리 예약을 먼저 해제·복귀하고 저장한 Quartz owner·메서드·priority·before/after·debug를 복원한다. 동일 메서드가 이미 등록돼 있으면 중복 등록하지 않는다. Quartz/optimizer가 내려가 있으면 다시 켜지 않는다. 같은 우선순위의 과거 등록 index까지 복원하는 API는 쓰지 않으므로 그 상대 순서는 바뀔 수 있다.

진행 중인 Quartz 작업의 정리는 기존 분석에 있는 원본 PlayHitTimes→StopAllSounds와 Quartz의 StopAll 처리에 의존한다. 런타임 모듈 재설치·예외·로드 순서까지 실제로 이중 재생이 없는지는 후속 게임 시험 대상이다. 정상 두 로드 순서의 코드상 인수 조건은 분리했지만 실행 검증을 마쳤다고 주장하지 않는다.

이번 세션에서 Quartz UserData 파일은 **읽지 않았다**. 새 런타임 파일 읽기 경로를 추가하지 않았으며 기존 Compat의 `Optimizer.json` 파싱에 플래그 하나만 추가했다. Discord.*, credentials, key, secret 등 인증 관련 파일은 접근하지 않았다.

## 검증 결과

| 검사 | 결과 |
|---|---|
| Dev `dotnet build --no-restore` | 오류 0, 경고 35 |
| Player `-p:Edition=Player` | 오류 0, 경고 34 |
| PlayerAuto `-p:Edition=Player -p:AutoTestBuild=1` | 오류 0, 경고 35 |
| LoadCheck Dev / Player / PlayerAuto | FieldRefAccess 261 / 229 / 261곳, 틀림 모두 0. 동적 이름 5곳은 별도 실행 확인 대상 |
| CompatCheck Dev / Player / PlayerAuto | 게임 멤버 참조 341 / 302 / 341개, 못 찾은 것 모두 0 |
| `py -3 tools/check-ui-encoding.py` | SFENC001: clean |
| `dotnet run --project tools/HitSoundCheck -c Release` | assertion 649,129개, 샘플 649,053개, 최대 오차 0, 다른 샘플 0 |
| 재사용 버퍼 합산 1,000회 | 해당 스레드 추가 할당 0바이트 |

오프라인 검사는 mono/stereo, 여러 구간 길이, 경계 임펄스, 구간을 넘는 꼬리, 겹침, 음수 시작, 무음, 1 초과 합산, 의도적인 샘플 손상 검출, 취소 및 반 샘플 반올림을 포함한다. 실제 맵/Unity/FMOD 출력 녹음 대조는 아니다. 빌드 경고는 0으로 만들지 않았으며 주로 기존 참조 충돌·구형 API 경고 등이다.

## 진단 명령의 의미

`hitsound state`는 활성 설정, 최근 이유, 목록 수, 성공 수, 구간 수, 렌더·예약 수, 사용 풀, 마지막 업로드 프레임 횟수/최대 횟수, 복귀 수, 캐시 바이트, Quartz 제외 상태를 출력한다. `enabled=True`만으로 적용 성공을 뜻하지 않는다. **captures 증가와 reason=captured를 먼저 확인**한다.

`hitsound verify` 첫 호출은 명시적 BelowNormal 검증 스레드를 시작하고 이후 호출은 진행 상황을 보여 준다. 원본 절대 시각·볼륨 스냅샷으로 별도 순회 합산을 하고 제품 렌더와 샘플별 비교한다. 실제 렌더 워커가 만든 버퍼의 해시도 독립 예상 버퍼 해시와 비교한다. `max_error`, `differing_samples`, `max_time_error_samples`, `actual_PCM_hashes`, `hash_mismatches`, `pending_actual`을 확인한다. 시각 양자화는 이론상 최대 0.5 입력 샘플이며 장치 출력 지연을 측정한 숫자가 아니다.

미래 구간은 아직 실제 렌더되지 않았으므로 **pending_actual을 누락 0으로 해석하면 안 된다**. 순수 전체 계산 완료와 실제 렌더 해시 확인은 별개다. 곡 종료 시 해당 시점의 결과를 문자열 스냅샷으로 남겨 종료 뒤 verify에서도 볼 수 있다. 필요한 마지막 구간이 생성되기 전에 게임이 곡을 종료하면 미확인 구간이 남는다. 해시는 충돌 가능성이 있는 보조 검증이다.

`hitsound compare`는 PCM 대조만 가능하다는 안내를 반환한다. AudioListener.GetOutputData/OnAudioFilterRead는 곡·홀드·UI와 mixer 처리를 합친 출력을 받으며, 별도 원본/합산 두 판의 DSP·믹서 상태를 고정해야 한다. 이번에는 두 판 녹음 도구를 구현하지 않았다. 성능 측정 중 verify는 켜지 말 것. 고빈도 함수에 Stopwatch 패치는 추가하지 않았다. 일반 Player 빌드에는 PCM 해시/검증 계산이 없다.

## Claude의 후속 게임 시험 순서 — 이번에는 실행하지 않음

1. 먼저 설치/게임 실행 허가가 있는 후속 세션에서 진행한다. 본 작업에서는 게임 실행·설치가 금지돼 있으므로 여기까지의 검증으로 배포하지 않는다.
2. Quartz 없이 DEV/AUTOTEST, 짧은 맵으로 켬/끔 준비를 확인한다. `hitsound state`에서 실제 캡처와 미지원 이유를 확인한다. 기본 프리팹 reverb/컴포넌트·클립 포맷 때문에 거절되면 그 원인을 먼저 조사한다. 캡처 0인 판을 성능 개선 판으로 세지 않는다.
3. 준비 직후 `hitsound verify`, 이어서 다시 verify해 순수 계산 완료·오차 0·해시 불일치 0을 확인한다. 재생 후 실제 확인 구간 수와 pending_actual도 확인한다. 검증을 켠 판은 성능 A/B에서 제외한다.
4. 기본음·타일 SetHitsound·커스텀음·음량 0/중간/최대·겹치는 긴 꼬리·자유 이동·행성 변경·홀드·카운트다운을 확인한다. 특히 0.5초 경계에서 클릭·음량·소리 누락을 청음/녹음한다. 출력 주파수가 원본과 다르면 리샘플러 경계도 확인한다.
5. 일시정지 10초 후 재개, 반복 일시정지, 실패/재시작, 에디터 중간 시작/seek, 속도 변경, 곡 중 음량/그룹 변경, 설정 끄기, 모드 끄기/Unload, 씬 이동, 장치 변경을 확인한다. 해제 후 소유 소스/클립·스레드·메모리가 돌아오는지 별도 확인한다.
6. Quartz 없음, 동일 DLL·맵·3440×1440·설정·PC 상태로 HELLO 2026 전곡 **끔/켬을 판마다 새 프로세스**로 비교한다. `state`로 캡처를 확인하고 verify 없이 잰다. 평균·1% low·12ms 초과 수·최악 프레임·준비 시간·메모리·소리 품질을 함께 기록한다.
7. 그 다음 Quartz 있음 + RenderAllHitSounds 켠 상태에서 우리 기능 켬/끔을 비교한다. Quartz 먼저/우리 먼저의 두 순서, 늦은 모듈 설치 후 첫 준비의 인수 유예, 다음 재시작의 인수, 우리 끄기/Unload 후 Quartz postfix 복원을 확인한다. 같은 박자 소리가 두 번 울리거나 원본 개별 예약과 합산이 동시에 돌지 않아야 한다.
8. 인계의 비교 기준 **194→224 FPS, 1% low 99→118, 12ms 초과 89→64**는 Claude가 전달한 과거 결과다. 새 구현에서 재현한 숫자가 아니다. 비슷한 향상 여부와 함께 순간 끊김·음질 악화를 판정한다. 청감·타이밍·병용 검증을 통과하기 전에는 기본 꺼짐을 유지한다.

## 남은 위험과 범위

샘플 합산이 일치해도 Unity의 SetData 값 처리, float 1 초과 데이터, 장치 리샘플러의 구간 경계, FMOD 믹싱 순서·가상 음원 우선순위, 설정 중도 해제의 꼬리 재생은 원본과 다를 수 있다. 채널·주파수·reverb 등의 보수적 거절 때문에 실제 HELLO에서 적용되지 않을 가능성도 있다. 구간 예약 시한을 놓치면 복귀 과정에 짧은 끊김/클릭이 생길 수 있다. 실제 캡처 성공·향상·오디오 동치·이중 재생 없음·누수 0은 아직 미확인이다.

기존 사용자 변경을 보존했다. 설치 훅·ship 스킬은 실행하지 않았다. 결과 문서는 apply_patch로 작성했다. BOM `EF BB BF`, 엄격한 UTF-8 디코드 통과, 대체문자 0개와 한국어 표본의 정상 표시를 확인했다.
