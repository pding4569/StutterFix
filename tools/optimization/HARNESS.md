# StutterFix evidence harness

2026-10-23 사용자 후속: CPU0.5ms는 이후 설계의 참고 지표로 두고, 소유 장식의 CPU 트윈/Transform 제거·그림/정렬·실제 FPS/끊김·전곡/폴백을 우선한다. 아래 검증기는 **기존 G2 기록의 고정 기준**을 보존한다. 새 방향을 이유로 과거 raw FAIL을 PASS로 바꾸지 않으며, 이 도구의 CPU 실패만으로 같은 게임 판을 반복하지 않는다. CPU 장식비용0은 별도 측정이 필요하다.

Harness²의 Propose–Probe–Compose 방식에서 착안한 **자체 로컬 검증기**다. Google Harness²를 설치하거나 연결한 것은 아니며 모델 재학습/재귀 에이전트 실행을 하지 않는다. 외부 코드 복사0, 외부 API 호출0, 게임/관리자 창 실행0, AGENTS/CLAUDE/사용자 메모리 자동 수정0이다.

공식 Harness²는 Linux/Python3.12 중심이며 모델 기반 반복에는 API 비용이 필요하다. 현재 Windows Unity/D3D11 게임 측정에 그대로 적용하는 이득은 확인되지 않아 설치하지 않았다. 참고: https://github.com/google-research/harness2 (2026-10-23 확인).

비공식 `3ae3ae/harness2-cli`는 자기 프로젝트와 구독 기반 CLI를 위한 fork라고 설명한다(https://github.com/3ae3ae/harness2-cli). 이 fork도 반복 모델 호출로 구독 한도/비용을 사용한다고 명시한다. 여기서는 fork 설치·로그인·중첩 에이전트 실행·성능 향상 확인을 하지 않았다. 로컬 검증기를 Harness² 자체나 모델 성능 향상으로 표현하지 않는다.

1. Propose: 한 후보/가설과 고정된 비교 조건을 기존 steps JSON에 기록한다. 원시 결과의 임계값/픽셀 오차를 후보가 바꾸지 않는다.
2. Probe: 기존 owned runner로 의미 있는 시험1묶음만 한다. 설치 DLL/native/Settings/boot/자동시험8상태를 보존·복원한다. 순수 데이터는 .NET fixture, GPU는 독립 D3D11/Unity lab, 게임 연결과 최종 성능은 실제 Player로 나눈다.
3. Compose: `harness_gate.py`가 원시 G2·4창·CPU·끊김·안정성·진단 여부·복원·짝 픽셀 판의3종SHA를 재검사한다. 실패는 실패 데이터로 보존한다. 통과도 G2 근거로만 표시하며 전체 곡/정식 채택으로 확장하지 않는다.

```powershell
python tools/optimization/harness_gate.py tools/optimization/out/<G2판> --paired tools/optimization/out/<픽셀판>/manifest.json
python -m unittest discover -s tools/optimization -p test_harness_gate.py
```

종료0은 조건을 충족한 G2 근거, 종료2는 불충족/미확인이다. 짝 판의 SHA 일치는 그 픽셀 판 자체의 성공을 증명하지 않으며, 전체 G1/전곡/해당 UI·필터·다른 모드 호환성은 별도로 확인해야 한다. 고정 기준은 FPS+25%·관리+native평균CPU≤0.5ms·30ms초과0이다. 이 도구는 기존 raw verdict의 실패를 통과로 바꾸지 않는다.
