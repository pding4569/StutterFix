# Codex 작업 AB: 히트음 예약 묶기 — 클립 형식이 섞여 있어도(모노/스테레오) 형식별로 묶어 예약 — 게임 실행·Mods 설치·커밋 금지

결과: 코드 + `docs/handoff/codex-hitsound2-20261019-result.md`(**apply_patch UTF-8 BOM**, 한글 확인). 빌드 셋 오류 0, `tools/HitSoundCheck`(오프라인 PCM 검증) 확장·통과, `tools/check-ui-encoding.py`·LoadCheck·CompatCheck 통과. 기본 꺼짐 유지. 기준: 현재 HEAD(`HitSoundBatch.cs` 최신 커밋 포함).

## Claude 가 실게임(알파, HELLO)에서 확인한 사실 (진단 메시지로 직접 확인)
`HitSoundBatch` 가 계속 "원래 경로로 복귀"했다. 순서대로 막힌 이유와 이미 고친 것:
1. 음원 프리팹 거절: 게임 `audioSourcePrefab` 은 유니티 기본값 그대로(`playOnAwake=True`, `reverbZoneMix=1`, comps=Transform+AudioSource) → `playOnAwake`/`reverbZoneMix` 거절 조건 제거, 대신 장면에 `AudioReverbZone` 이 없을 것을 요구(이미 수정됨).
2. 클립 거절: 게임 히트음 클립은 `loadType=CompressedInMemory`, `loadState=Unloaded`(예: `sndKick loadType=CompressedInMemory samples=9499 freq=44100 ch=1 sec=0.22`) → 스트리밍만 제외하고 `Unloaded` 면 `LoadAudioData()` 후 최대 200ms 대기하도록 수정(이미 수정됨).
3. **지금 막힌 이유**: `mixed sample rate/channel layout (no guessed resampling) [first=44100Hz/1ch, this=sndFireTile 44100Hz/2ch, out=48000Hz]`. 즉 히트음 목록에 **모노 클립(`sndKick` 등)과 스테레오 클립(`sndFireTile` 등)이 섞여 있다**(샘플 레이트는 44100 으로 같음, 출력은 48000). 현재 구현은 `Session.Rate/Channels` 가 하나라서 포기한다.

## 해야 할 일
1. **형식 그룹 지원**: 목록의 클립을 `(frequency, channels)` 형식별 그룹으로 나눠 **그룹마다 따로 합산·따로 클립·따로 음원 풀**로 예약한다. 같은 시각(`PlayScheduled` 의 dsp 시각 동일, 구간 시작 정렬 동일)에 각 그룹의 음원을 예약한다. 모노 그룹은 모노 클립(1채널), 스테레오 그룹은 스테레오 클립(2채널)로 `SetData` 해서 **원본과 같은 형식 그대로 FMOD 가 업믹스·리샘플**하게 한다(우리가 모노→스테레오 변환이나 리샘플링을 추정으로 하지 않는다 — 음량·위치가 원본과 달라질 수 있는 부분이므로). 그룹 수 상한(예: 4) 초과, 채널 3 이상, 서로 다른 샘플 레이트가 섞이면(`Rate` 가 그룹 키라면 허용 가능) 그 곡은 원래 경로로 복귀.
2. 음원 풀(최대 32개 또는 현재 상한)을 그룹별로 나누거나 공유 풀에서 형식에 맞게 클립을 바꾸는 방식 중 안전한 쪽을 설계: 같은 시각 예약 2~4개를 한 프레임에 `SetData` 하므로 프레임당 상한·구간 크기를 재검토(한 프레임에 몰리지 않게). 메인 스레드 비용이 원본 개별 예약보다 커지지 않는지 `hitsound state` 에 그룹 수·프레임당 `SetData` 횟수/바이트(peak)를 표시.
3. **검증**: `tools/HitSoundCheck` 와 `hitsound verify` 를 그룹 구조로 확장. 모노 8~10개·스테레오 8~10개가 섞인 합성 목록(겹침·꼬리가 다음 구간으로 넘어가는 경우·시작 오프셋 0)으로 기대 합산 PCM 과 렌더 결과를 그룹별로 비교(차이 0 샘플)하고, 그룹 간 시각 정렬(샘플 단위) 일치를 확인. 실제 게임 목록이 14,262개(HELLO)이므로 메모리·시간 상한도 확인.
4. 결과 문서에 그룹 처리 설계, 새 `state` 항목, Claude 가 돌릴 순서(HELLO 짧은 시험 `hitsound state` 로 `captures>=1`, 이어서 전곡 A/B: `HitSoundBatch` 끔/켬, Quartz 없음; 기대치 평균 190→224 근처).

## 규칙
- 기본 꺼짐, 소유한 것만 정리, 불확실하면 원래 경로로 복귀. 소리가 달라질 수 있는 부분은 숨기지 말고 문서에.
- 다른 모드 코드 복사 금지(Quartz 코드는 읽지 않아도 된다).
