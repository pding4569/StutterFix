# GPU→CPU 진행 중 애니메이션 복원 — round29, 2026-10-23

후속 round30~32의 히트음 첨부 수정·12키 CPU/6키 GPU 왕복·미관측 순서 표식 보존 결과는 `codex-gpudeco-followup-20261023.md`를 먼저 읽는다. 아래는 round29 당시 DLL의 근거이며 새 DLL의 성능 수치가 아니다.

## 구현과 한계

현재 `codex/night-gpu`, 연구 기본 꺼짐·관리 Player 제외. `DecoAnim.GpuResume.cs`/`GpuDecoResume.cs`에서 현재값을 새 시작값으로 바꾸지 않고 원래 start/target/change/duration/float Pos/ease/over/period를 CPU Rec에 등록한다. 색4채널과 시차2채널은 같은 source인지 전부 확인한 후 GPU 소유권을 해제한다. 원래 FastMove의 키 생성 순서(위치→시차 오프셋→피벗→회전→크기→색→불투명도→시차 배율)를 따로 기록한다. 기존 CPU Rec의 생성 순서와 합쳐 복원하며 렌더러/정렬/충돌체는 변경하지 않는다.

- 이미 끝난 키는 재생성하지 않는다. Scale은 Started=true로 초기화해 첫 갱신의 현재값으로 시작값이 덮이지 않게 한다. target과 최종 이징값을 구분한다.
- Batch/DecoEngine 저장을 경계에서 풀고 정상 NewRec 등록 경로를 쓴다. Kill(true)/새 효과 교체는 기존 대역으로 돌아간다. 이번 시험은 모든 교체 상황의 검증이 아니다.
- 같은 paused 곡 시각의 즉시 켬/끔에서도 Rec를 복원한다. 이때 기존 원본의 현재 논리값은 다시 덮지 않는다. 게임이 이미 CPU Update를 지난 경계에서만 GPU clock을 한 번 따라잡고, 예정된 CPU Update를 미리 진행하지 않는다.
- 포탈/SceneReset/종료에서 불필요한 Rec를 되살리지 않는다. Unload의 엄격한 preflight 실패 시에도 callback/버퍼를 해제하는 경로를 둔다. 이 강제 경로의 실제 오류 주입 검증은 확인 안 됨이다.
- table에서 잡은 native tween은 원래 Rec 삽입 순서를 관측하지 못했다. 실제 판에서 6개가 남았고 `unobserved_order=6`으로 기록한다. **이 6개의 원본 순서/완전 동일성은 확인 안 됨**이다. 동작을 현재 GPU clock에서 잇는 구현과 원래 native tween의 완전 동일성을 구분한다. 일반 채택0.

## 실제 게임 검증

실제3440×1440·144Hz·D3D11·동기0, Arche262초. 명시적인 audit는 실제 Rec와 CPU 논리값을 비교하며, resident 자료를 매 프레임 한 번 동기화한다. 성능/픽셀 시험의 대용이 아니고 성능 판에서 켜지 않는다.

| 판/범위 | 기록 | 다음 CPU 프레임 | 논리값 비교 | metadata/미래값 불일치 | 교체로 제외 |
|---|---:|---:|---:|---|---:|
| round29 사전, 같은 paused 시각 즉시 왕복 | 512 | 12 | 1,524 | 0/0 | 0 |
| round29c, 키별64개 한도·즉시 왕복 | 384 | 12 | 1,218 | 0/0 | 0 |
| round29c, GPU258→262초 뒤 CPU 인계 | 384 | 12 | 1,218 | 0/0 | 0 |

round29c 두 audit에서 각각 키1/2/5/7/8/9(위치X/Y·회전·크기X/Y·색) 64개다. 키3/4/10/11/12/13의 실제 계속 재생·교체/완료 검증은 확인 안 됨이다. 실제 계속된 기록-프레임 수816씩, 미관측 순서6개는 미래값 audit에서 제외했다. 전체 재등록은 두 경우 모두9,960/9,960이다. 재등록 숫자를 전체 시각 동일성이나 전곡 통과로 쓰지 않는다.

paused 경계의 복원 CPU 시간은 즉시11.0538ms·GPU 재생 뒤33.5471ms였다. 이는 audit/준비가 있는 정지 판의 경계 시간이며 정상 플레이 프레임 시간/입력 지연의 대용이 아니다. 전환 비용0이라고 하지 않는다.

같은 round29c DLL의 별도 paused 픽셀 대조: 마스크 복사/전체 복사0, 마스크 반복0, 원본 반복0(각4,953,600픽셀); 원본 SpriteRenderer/GPU는36픽셀·최대R/B=2로 완전0이 아니다. 원시PNG5장을 보존한다. 정식3시점 G0/G1·새 DLL의 G2·전체곡·HELLO·FG동시·물리표시/GPU시간은 확인 안 됨이다. 이전 round26의+26.743351%/CPU0.505004ms를 이 DLL에 붙이지 않는다.

두 판 모두 오류0, reopen_toggle 잔존 버퍼/메시0·SF오류0, 정상 종료·설치8상태 원본 바이트 복원8/8. 최종3빌드 오류0/0/0, LoadCheck277/229/277 불일치0, 일반 관리 연구타입0이다. 마지막 도구 수정은 Python evidence 집계뿐으로 DLL을 바꾸지 않았다.

원시/설치백업은 ignored out의 round29-resume·round29c-resume, 공개 수치는 evidence/song-summary.json을 따른다. 커밋/푸시/main/태그/릴리스/ZIP0.

## Harness² 검토와 로컬 적용

사용자가 Harness²가 적합하면 도입하라고 했다. 공식본과 비공식 CLI fork를 확인했으며, Linux 중심 벤치/반복 모델 호출의 이번 Windows 게임 검증 이득은 확인하지 못했다. 설치/모델 재학습/외부 API 호출/중첩 에이전트 실행0이다.

대신 자체 `tools/optimization/harness_gate.py`를 집계기에 연결했다. 고정된 G2 기준, 4창, 오류/진단, 안정성, 설치8상태 복원, 짝 픽셀 판의 DLL/native/번들 SHA를 확인한다. 실패를 raw PASS로 바꾸지 않고 전체곡/정식 채택은 항상 별도로 둔다. 경계·원시 실패·진단 혼입·다른 DLL·복원 누락·창 중복 등13개 검사 통과. 실제 round26은 CPU초과/raw FAIL, round28은 FPS/CPU초과/raw FAIL로 자동 거부했다. 이 도구는 Harness² 자체가 아니며 속도/모델 역량 개선을 실측한 것도 아니다. AGENTS/사용자 메모리/사용량 설정 자동 수정0.

다음은 미관측 native 순서6개와 아직 나오지 않은6키, 교체/완료/배속, 새 DLL 시각·성능/전곡 확인이다. 새 근거 없이 같은 표본을 반복하지 않는다. Unity Editor lab/독립 .NET·D3D11 fixture에서 분리 가능한 검사부터 하고 실제 게임 연결을 마지막에 검증한다.
