# 판정 오차 막대 직접 종료 구현 결과

2026-10-27, `E:/StutterFixWork/wt-meter`. 게임 실행·게임 DLL 설치·커밋·푸시 없이 구현과 헤드리스 검사를 완료했다. 실제 FPS·게임 화면·게임 내 패치 설치는 Claude의 후속 확인 대상이다.

## 변경

- `HitMeterFade.cs`: Show의 두 Kill과 두 OnKill 연결만 트랜스파일러로 바꾼다. 눈금별 본체/외곽선 핸들을 약한 키 테이블에 기록한다. 완료 또는 Kill 시 핸들 참조를 먼저 놓고 게임의 원래 OnKill을 실행해 색 지우기를 유지한다.
- 기록이 없거나 ID가 바뀌면 원래 `DOTween.Kill(id)`로 돌아간다. 최초 검색으로 외곽선이 비었음도 기록하므로 외곽선이 한 번도 없던 눈금의 반복 검색도 사라진다. 끝난 핸들은 아무것도 죽이지 않는다.
- 손의 회전/이동 단축 호출만 바꾸고 호출자의 SetEase/SetId를 유지한다. 기존 손 트윈은 **새 트윈의 getter가 첫 업데이트의 현재값을 읽은 직후** `Kill(false)`한다. 생성 즉시 끊어서 다음 프레임의 시작값이 달라지는 것을 피한다. 직접 값 쓰기 풀은 도입하지 않았다.
- 손에 다른 OnKill/OnComplete/OnUpdate 등 콜백이 있거나 일시정지·반복·지연·길이/시간 배율 차이가 감지되면 조기 종료하지 않는다. 조기 종료 시 게임의 완료 콜백이나 외부 Kill 콜백을 호출하지 않으며 내부 핸들 정리만 남는다.
- `Settings.HitMeterDirectKill=false` 기본값, ApplyConfig 및 기존 `set` 명령 연결. OFF에서는 원래 Kill 및 OnKill 등록을 사용하고 새 추적 상태를 만들지 않는다. 전환 시 기존 추적 상태를 비운다.
- Main.Unload에서 Shutdown으로 상태·카운터를 비우고 대기 중 getter의 조기 종료를 무효화한다. Harmony 패치는 기존 Main.Unload의 소유 ID UnpatchAll로 제거된다. 게임 소유 트윈은 종료 시 그대로 진행한다.
- 설치 로그에 눈금 Kill `2/2곳`, 손 `2/2곳`을 출력한다. 모양을 인식하지 못한 Show는 직접 종료 치환을 하지 않는다. 전체 Prefix 대체는 없다.
- 제품 빌드의 기본 파일 후보에서 새 검사 도구의 bin/obj를 제외한다. 검사 전용 가짜 Unity DLL이 제품 참조로 잡히는 문제를 방지한다.

## 검사 결과

`tools/optimization/HitMeterDirectCheck`의 마지막 실행:

```text
Installed game IL: Show Kill=2 OnKill=2 DOColor=2; AddHit rotation=1 anchor=1; Reset global Kill sites=2
warm tick 100 hits: KillFast=200 KillSlow delta=0
HitMeterFade regression: 256 sequences / 6144 frames
PASS scenarios=2304 frames=276480 bit_values=12732916 contracts=272
comparison KillFast=466154 KillSlow=77174 HandKilled=159187
```

2,304개 시나리오의 프레임별 비트 대조 차이 0. 추가 16개 계약 검사와 기존 Fade 회귀 256개 시퀀스도 통과했다(`contracts=272`는 이 둘의 합). 기준 경로는 기존 Fade 적용 상태, 후보는 그 위에 직접 종료를 추가한 상태다. 페이드 자체의 원래 DOColor 대조는 별도 유효 알파 오차 1e-6 검사다.

전체 카운터는 OFF·최초 접근·ID 변경·설정 전환·Shutdown이 섞인 시나리오의 부분 누적 합이다. 게임 성능 측정값이 아니다. 준비된 눈금 100타격 구간에서는 두 Kill의 200회가 모두 직접 경로였고 `DOTween.Kill(id)` 추가 호출은 0회였다. 이 구간에서 Show가 호출하던 FilteredOperation 검색이 사라졌다는 코드 경로 근거이며, Reset의 전역 검색은 유지한다.

- 일반 Player 빌드: 오류 0개.
- Player + AutoTestBuild=1 빌드: 오류 0개.
- `git diff --check`: 통과.
- 전체 `tools/check-ui-encoding.py`는 기존 `out/image-matrix-candidate/scrDecoration.cs`의 UTF-16 인코딩에서 UnicodeDecodeError로 중단된다. 이 파일은 수정하지 않았다. 변경/신규 C# 소스 7개의 UTF-8 및 SFENC001 의심 문자열 검사는 별도로 통과했다(`out/hitmeter-direct/check-changed-encoding.py`).

로그: `out/hitmeter-direct/check.log`, `build-player.log`, `build-player-auto.log`. 검사 실행법은 `tools/optimization/HitMeterDirectCheck/README.md` 참고. 인계에서 예로 든 EventAdmissionCheck와 기존 HitMeterFade 전용 검사는 현재 체크아웃에 없어, 새 도구에 관련 회귀를 포함했다.

## 알려진 차이와 검증 한계

1. **이미 핸들을 아는 눈금과 같은 ID로 다른 모드가 만든 트윈**: 직접 경로에서는 그 외부 트윈을 죽이지 않는다. 원래 전역 Kill은 죽인다. 인계에 명시된 정책대로이며 별도 계약 검사로 확인했다. 기록 없는 눈금은 전역 Kill로 돌아가 외부 트윈도 죽인다. Reset은 원래 전역 Kill을 유지한다.
2. **손 트윈 수명**: 이전 트윈은 원래의 0.25초 종료 시점보다 일찍 끝난다. 최종 손 값의 동일성은 기본 게임 체인의 검사 범위에서 확인했다. 외부 모드가 이후 트윈의 업데이트 그룹/방향/목표값/콜백 등을 추가로 바꾸는 모든 조합의 동일성은 보장하지 않는다.
3. 기존 Fade의 정점 색 대 CanvasRenderer 알파 표현 차이와 색 양자화 한계는 이번 변경 전부터 존재한다. 실제 Unity 픽셀 동일성은 검사하지 않았다.
4. 실제 설치된 DOTween.dll을 실행하지만 Unity 객체/수학/UI와 IL 실행 환경은 가짜 관리 어댑터 및 .NET 8이다. 게임 내 Harmony 설치, 네이티브 Quaternion 계산, Unity Mono 및 실제 FPS/화면은 실행하지 않았다.

## Claude 후속 확인

PlayerAuto DLL을 사용할 때 기존 자동 시험의 `set HitMeterDirectKill true` / `set HitMeterDirectKill false`로 전환한다. 묶음 끝에는 기존 set 복구 규칙이 적용된다. 설치 로그의 `눈금 Kill 2/2곳, 손 2/2곳`을 먼저 확인한다.

곡 요약의 `Kill 직접 .../검색 ..., 손 종료 ...`는 누적 카운터다. 최초 눈금 접근·ID 변경·전환 직후에는 검색이 남을 수 있다. 안정 구간의 전후 차이를 비교하고, Reset을 끼워 눈금 본체/외곽선과 손의 화면 동작, 고 BPM 실제 FPS를 확인한다. 기본값은 계속 OFF다.
