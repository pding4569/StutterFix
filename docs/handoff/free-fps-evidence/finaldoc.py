from pathlib import Path
import json,re,datetime
p=Path('docs/handoff/free-fps-findings.md');s=p.read_text();s=s.replace('2026-10-15. 진행 중. 측정·복원 완료 뒤 최종 상태로 갱신한다.','''2026-10-15. 조사 및 복원 완료. 새 플레이어용 최적화 채택은 **0개**다. 정지 배경 비용이 구현 기준에 못 미쳤고, 작은 RT 후보는 화면이 달라지거나 실제로 ARGB32로 대체됐다. 6개 후보군을 조사했다. 카메라 성능 탐색 16판은 제한을 기록하고 채택 근거에서 제외했으며, 교정 ABBA 16판(HELLO 12/Windflower 4)을 별도로 남겼다. 픽셀 대조는 6시점 중 반복 대조군 차이 0인 5시점만 유효하게 사용했다. HELLO 필터 조사 판은 완주했다.

성능 표본은 약 첫 20초(HELLO)/30초(Windflower)의 구간 시험이며 전체 곡 FPS 개선 수치가 아니다. Arche/h90000 추가 성능 판은 하지 않았다. 화면을 그대로 유지하는 채택 후보가 없으므로 확대 A/B를 진행하지 않았다. 모든 가능한 그래픽 기법을 불가능하다고 결론 내리지 않는다.''')
s=s.replace('## 현재 값','## 첫 세션 현재 값');s=s.replace('## 후보별 판단표 (진행 중)','## 후보별 판단표');s=s.replace('HELLO 카메라 ABBA 4판, Windflower 추가 확인','HELLO/Windflower 카메라 ABBA 각 4판');s=s.replace('이득 기준 충족 전 구현하지 않음','GPU 평균 차이 0, 10% 구현 기준 미달로 구현하지 않음');s=s.replace('같은 프레임 24 반복 대조 후 각 1표본','5개 유효 시점의 같은 프레임 24 반복 대조 후 비교');s=s.replace('같은 프레임 1표본','5개 유효 시점의 같은 프레임 비교');s=s.replace('첫 표본 1개 | 차이 0 | 표본에서는 같음 | 실제 할당 포맷 및 재생 표본 추가 확인 전 보류','5개 유효 시점 | 차이 0; 실제 ARGB32로 대체 | 유효 표본에서는 같음 | 더 가벼운 버퍼가 아님. 기각');s=s.replace('첫 표본 1개','5개 유효 시점');s=s.replace('처음 16','처음 16')
s=s.replace('| Camera | 275→353.5 | 2.0→0.75 | 62.5% | 0→0 | 4 | 아니오 | 주 비용은 실제 게임 화면. 내용 삭제 채택 불가 |','''| Camera | 275→353.5 | 2.0→0.75 | 62.5% | 0→0 | 4 | 아니오 | 주 비용은 실제 게임 화면. 내용 삭제 채택 불가 |
| Windflower BGStaticCam | 294→296 | 1.7→1.7 | 0% | 0→0 | 4 | 아니오 | 두 번째 맵에서도 캐싱 기준 미달 |''')
s+='''

## 실제 형식과 추가 픽셀 근거

요청한 24비트 깊이는 이 PC에서 실제 depth=32, depthStencilFormat=D32_SFloat_S8_UInt로 할당됐다. depth=16은 D16_UNorm, depth=0은 None이었다. RGB565는 실제 B5G6R5_UNormPack16. **ARGB4444는 실제 format=ARGB32 / graphicsFormat=R8G8B8A8_UNorm**으로 대체됐으므로 바이트가 같아도 저장량·대역폭 절약이 없다.

HELLO 두 유효 editor 시점에서 깊이 0/16은 각각 1,692,230 및 10,923,487바이트가 달랐다. 추가 실행의 editor 시점도 1,692,264바이트가 달랐다. Windflower 첫 시점과 HELLO 약 14초 시점에서는 깊이 0/16 차이 0이었지만 맵/순간에 따라 달라지므로 일반 적용하지 않는다. RGB565는 유효 5시점 모두 차이가 났다(236,200 / 337,075 / 2,672,958 / 338,300 / 8,381,020바이트). 마지막 HELLO 약 30초 시점은 24 반복 대조군 자체에 1,007바이트 차이가 있어 후보 대조 전체를 폐기했다. 모든 입력·마스크를 검증했다고 주장하지 않는다.

## 렌더 카운터와 배치

처음 Recorder.Get의 valid=True/sampleBlockCount=0은 드로 콜 0이라는 뜻이 아니었다. 별도 진단 실행에서 Unity.Profiling.ProfilerRecorder를 2초 동안 켜고 값을 읽은 뒤 Dispose했다. 정식 빌드에는 들어가지 않는다.

| 상태 | 주요 렌더 카메라 | Draw calls | SetPass | Batches | 표본 |
|---|---:|---:|---:|---:|---|
| HELLO editor | RT 경로의 scene 3 + Overlay 1 | 232 | 126 | 232 | 2초 수집 뒤 마지막 한 프레임 |
| HELLO 재생 약 14초 | scene 3 + Overlay 1 | 423 | 273 | 409 | 2초 수집 뒤 마지막 한 프레임 |

이 숫자는 평균/곡 전체 최대치가 아니다. HELLO snapshot은 enabled Renderer 15,540개, 고유 material ID 15,239개, MPB 사용 804개. Windflower snapshot은 각각 7,640 / 7,168 / 5,594개. 많은 material ID는 바꿀 여지가 있을 수 있으나 현재 게임은 타일별 색·스타일을 바꾸므로 공유로 원래 결과를 보장하지 못한다. 실제로 동시에 그려지는 renderer 수·draw calls와 전체 material 수를 혼동하지 않는다. 안전한 공유 대체나 불필요한 호출이라는 근거를 확보하지 못해 새 공유 패치를 넣지 않았다.

## 빌드와 종료·복원

- PlayerAuto: 오류 0, 경고 30. Player: 오류 0, 경고 25. 빌드 로그는 free-fps-evidence/build-auto.log 및 build-player.log. 경고는 참조 버전 충돌·deprecated API·미사용 필드 등이다.
- FreeFpsProbe는 #if DEV || AUTOTEST 안에만 있다. 새 Harmony/정적 이벤트는 설치하지 않으며, 카메라 mask는 restore/Main.Unload에서 복원하고 네이티브 카운터도 Dispose한다. 일시적인 targetTexture/RenderTexture.active 변경은 finally에서 되돌린다. 매 프레임 managed 진단 코드를 추가하지 않았다.
- 최종 게임 프로세스 없음 확인. 모든 게임 종료는 자동 시험 quit로 정상 종료; taskkill 사용 없음. 마지막 sfmeasure 종료 대기 중 발생할 수 있는 '묶음 도중 게임이 꺼짐'은 실제 프로세스 종료를 확인해 정상 종료로 처리했다.
- dotnet build -c Debug -p:Edition=Player 결과 bin/Player/StutterFix.dll을 Mods에 복원했다. 설치 DLL과 빌드 DLL 및 원본의 SHA-256은 모두 CB634D735758EBE0E38A1E45320F0DD7D2919C7F7408E6BA0520814C3AD46858.
- Settings.xml 원본 복원 확인: 원본/복원본 SHA-256 모두 A73F5C606FB838641BA3B6268BD7C88A3DC1BD9E01B2A399559CC836F6DDCE4A.
- 커밋·푸시·릴리스 없음. FrameGen*·framegen*.h 및 셰이더 소스 수정 없음. 기존 .codex/config.toml 등 사용자의 변경은 건드리지 않았다.

원시 로그와 실행 스크립트, camera-final.json(각 판 화면 조건 포함), filter-final.json, IL 결과, 원본 DLL·설정은 docs/handoff/free-fps-evidence/에 있다. 이번 결과는 새 FPS 개선의 성공 보고가 아니라, 화면을 유지하며 적용할 수 있는 후보가 없음을 확인한 조사 결과다.
'''
p.write_text(s,encoding='utf-8')
