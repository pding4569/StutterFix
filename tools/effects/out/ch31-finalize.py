import json,re,hashlib,shutil
from pathlib import Path
root=Path('E:/StutterFixWork/StutterFix'); raw=root/'tools/framegenlab/out/ch31-refresh-full'; public=root/'tools/framegenlab/validation-31';public.mkdir(exist_ok=False)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
full=json.loads((raw/'summary.json').read_text(encoding='utf8'))
for r in full['runs']:
 log=(raw/r['map']/'game.log').read_text(encoding='utf8')
 assert '[상태] Won' in log and '[자동 시험] 곡 끝남' in log
 r['won_confirmed']=True
 conditions=re.findall(r'화면: ([^\r\n]+)',log)
 assert conditions and all(c==r['conditions'] for c in conditions)
 r['start_end_conditions_equal']=True
 r['dotween_captured_errors']=int(re.search(r'safe mode captured (\d+) errors',log)[1])
 r['reported_hitches']=[dict(ms=int(ms),song_s=float(t),cause_unconfirmed=True) for ms,t in re.findall(r'\[모니터\] (\d+)ms[^\r\n]*?곡 ([\d.]+)초',log)]
 r['final_connection']=json.loads((raw/r['map']/'summary.json').read_text(encoding='utf8'))['states'][-1]['installed']
 assert r['final_connection']==0 and r['errors']==0
 full['scope']='Two fresh PlayerAuto Release full songs, FX OFF, Won and normal quit confirmed. Rounded original-FPS song summary; not whole-song physical-display or visual-quality proof. 35s sample is separate.'
(public/'full-refresh.json').write_text(json.dumps(full,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
for filename,src in [('package.json',root/'tools/effects/out/ch31-package.json'),('default-off.json',root/'tools/framegenlab/out/ch31-default-off/summary.json')]:
 data=json.loads(src.read_text(encoding='utf-8-sig'));(public/filename).write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
mod=Path('D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice/Mods/StutterFix')
rest={n:dict(sha256=sha(mod/n),matches_original=(mod/n).read_bytes()==(raw/(n+'.original')).read_bytes()) for n in ['StutterFix.dll','sfnative.dll','Settings.xml']}
old=root/'dist/FrameGenLab-2026-10-07-tester.zip';rest['previous_zip']=dict(sha256=sha(old),matches_original=sha(old)=='689de906e1b0513c526c34c0aff33dbff6b1d12fe9544adf78b8295a4932c134')
assert all(v['matches_original'] for v in rest.values())
(public/'restoration.json').write_text(json.dumps(rest,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
package=json.loads((public/'package.json').read_text(encoding='utf8'))
assert package['zip_sha256']==sha(root/'dist/StutterFix-2.5.3.1-tester.zip')
rows=[]
for r in full['runs']:
 s=r['game_metrics']['songs'][0];rows.append(f"| {'HELLO 2026' if r['map']=='hello' else 'Arche'} |1|0|{r['errors']}|{s['fps']:.0f}|{s['worst']:.0f}|{s['count']}|{r['dotween_captured_errors']}|")
chapter='''

## 31. 주사율 맞춤 기본화·전체 완주·테스터 (2026-10-09)

사용자 요청으로 새 설정의 주사율 맞춤을 첫 추천으로 두고 마감+저장 휴식을 한 모드로 묶었다.
기능 전체 기본값은 FrameGenOutside0이며 새 FrameGenRefresh 기본true는 켜기 요청이 아니다.
기존 XML의 명시적 FrameGenRefresh=false/2~8배·마지막 배율은 유지한다.
FrameGenRefreshRest는 옛 XML 호환 필드이며 mode9의 휴식을 끄지 않는다. 기존false를 준 두 판에서도
마감이 활성화됐고 Arche의45초 상태에서 resting=True를 확인했다. 8연속0.8/Hz 미만→휴식·가까워짐→
재개, 첫 예상 밖 끊김을 채우지 못하는 한계는30장과 같다. stage1 3RT·variant3·native 변경0이다.

| Before | After | Why |
|---|---|---|
| 새 설정은2배 선택·주사율 맞춤에 별도 휴식 후보 스위치 | 주사율 맞춤을 첫 추천·휴식 포함,2~8배 선택 유지 | 기본 꺼짐에서 켠 뒤 추천 모드를 바로 선택 |

### 새 일반 완주2판

실제3440×1440·165Hz·D3D11·동기0·전체화면창·큐2, 시작/끝 화면 조건 일치2/2다.
새 실행마다 전체 곡을 재생했고 PlayerControl→곡 끝남→Won·정상quit를 확인했다.
FX/FSR/Half는끔·렌더100%, 맵 연출은 유지했다. 캡처/PM/연구 캡처0이다.
PlayerAuto Release managed `c7350faf`/native `2feb4eda`, 자료 `validation-31/full-refresh.json`이다.

| 맵 | 완주 | 비정상 종료 | SF집계오류 | 곡 요약 진짜FPS | 최악 원본ms | 기록된 끊김 | 기존 DOTween 오류집계 |
|---|---:|---:|---:|---:|---:|---:|---:|
'''+ '\n'.join(rows)+'''

Arche225.3초67ms에 효과55.1ms·장식 이동 효과30ms 로그가 겹쳤다. 독립 ETW 원인은 확인 안 됨이다.
0ms GPU/CPU는 FrameStats끔에서 수집0이라는 뜻이며 GPU 비용0의 증거가 아니다.
DOTween 두 판 각각775(대상18/시작757)는 기존15장과 같은 집계이며 게임 전체 오류0으로 쓰지 않는다.
SF 오류0·비정상 종료0과 모든 화면 품질/끊김0은 서로 다른 항목이다. 이 판의 전체끔 대조·물리표시FPS·
추가표시지연·전체픽셀 품질은 확인 안 됨이다. 중간35초 제출 표본을 전체 곡 표시FPS로 확대하지 않는다.

### 기본 꺼짐·테스터 검사

새 일반PlayerAuto 메뉴 시험1판은 주사율 선호true·기능끔에서 runtime_initialized/연결/원본/생성0,
효과readyFalse·집계오류0이다(`default-off.json`). 수정한 상태 명령은 명시적 호출 때만 문자열을 만든다.
기존 gate 경계25조건실패0이며 native 픽셀fixture60은30장의 변경 없는 자료다(재시험0).

`./pack.sh test 1`로 `dist/StutterFix-2.5.3.1-tester.zip`을 만들었다.
일반Player Debug DLL·Info.json·자체MIT 셰이더 라이선스3파일이다.
Player LoadCheck219·오류0, PlayerAuto220·오류0이다. 완주 DLL은AutoTest Release이고 ZIP은 일반
Debug이므로 ZIP 자체를 두 맵 자동 완주한 것으로 쓰지 않는다. ZIP DLL/내장native·effects·FSR은
실제 파일과 바이트 일치, MeasureBuild/AutoTestBuild/Edition.AutoTest/Edition.Dev=false,
새 숫자 입력 시험 훅 포함0, stage1/variant3이다(`package.json`).

TesterCheck로 실제 ZIP의 Settings를 생성하고 빈 XML·기존4배/refreshfalse XML도 읽었다.
기능끔/효과boolean18개끔·미리설정0·모니터자동−1, 새주사율선호true·기존배율유지·리소스일치 실패0이다.
검증기 사전 실패2건은 문화권 정렬의 엔트리 순서, 기존 비활성 AutoTest 문자열의 잘못된 포함 판정이었다.
정렬을ordinal로 고치고 Edition의 실제상수와 신규시험훅 부재를 검사했다. 기존 진단 타입은 두 빌드에
컴파일되지만 일반에서는 설치·호출되지 않는 Edition.cs 설계를 유지했다. ZIP/게임 코드를 고친 실패가 아니다.

ZIP SHA256: `'''+package['zip_sha256']+'''`.
설치3원본/기존ZIP4SHA 일치(`restoration.json`), 게임 정상종료2/2+메뉴1/1이다.
화면 효과 미리설정/강도값 변경0, 기존2~8배 구현변경0, main/태그/릴리스0/0/0이다.

| 결론 | 수치 |
|---|---:|
| 새 완주/비정상 종료/SF집계오류 |2/0/0|
| 기존 DOTween 집계 HELLO/Arche |775/775|
| 꺼짐 뒤 원래Present 연결 |0/0|
| 새 기본꺼짐 초기화/연결/원본/생성 |0/0/0/0|
| ZIP LoadCheck/설정·리소스 검사 오류 |0/0|
| 새 테스터ZIP/설치·기존ZIP복원SHA |1/4·4|
| 전체 표시FPS·추가표시지연·전체그림품질 |확인 안 됨|
| main/태그/릴리스 |0/0/0|
'''
p=root/'docs/framegen-research.md';text=p.read_text(encoding='utf8');text=text.replace('1~10장은 클라우드에서', '최신: 31장. 주사율 맞춤=마감+저장 휴식·첫 추천, 기능 기본 꺼짐. 두 맵 새 완주2/2·비정상 종료0·SF오류0, 기존 DOTween775/775는 별도. 테스터2.5.3.1 생성·검사 완료. 아래1~10장의 결론은 초기 조사이며 이후 블록 보간 검증과 구분한다.\n\n1~10장은 클라우드에서',1);p.write_text(text+chapter,encoding='utf8')
p=root/'docs/shader-research.md';text=p.read_text(encoding='utf8');text=text.replace('최신: 15장.', '최신: 16장. 사용자 요청으로 현재 미리설정 값을 변경 없이 테스터에 채택했다. 효과 기본꺼짐·숫자 강도 입력 유지, 셰이더/번들/프리셋 값 변경0.\n\n직전15장:',1);text+='''

## 16. 현재 값 채택·테스터 준비 (2026-10-09)

사용자가 현재 미리설정 값을 그대로 넣도록 승인했다. 기존0~3/직접조절4/강한 값5·6을 유지했다.
효과boolean18개false·미리설정0이며15장의 슬라이더/숫자 입력과 Custom 저장을 그대로 쓴다.
셰이더 코드/AssetBundle/프리셋 수치/렌더 순서 변경0이다. 새 게임 캡처0이며14/15장 캡처를 새 결과로
다시 쓰지 않았다. 이전 미확인 움직임/물리 입력/효과 전체GPU PM·추가지연을 새 통과로 바꾸지 않는다.

`pack.sh test 1`에 셰이더MIT 라이선스 동봉을 추가했다. ZIP 실제검사는 프레임 연구31장과
`tools/framegenlab/validation-31/package.json`에 있다. 테스터2.5.3.1은 일반Player/측정false·
자동시험false·입력시험훅포함0, 효과번들SHA `8ed7aeba` 일치다. 완주2판은FX끔의 마감+휴식 검증이며
네온 전체 곡 품질 시험의 대용이 아니다. main/태그/릴리스 변경0이다.

| 결론 | 수치 |
|---|---:|
| 프리셋/셰이더/번들 수치 변경 |0/0/0|
| 기본 켜진 효과/미리설정 |0/0|
| ZIP 내 셰이더 라이선스/리소스 불일치 |1/0|
| 일반 LoadCheck/패키지 검사 오류 |0/0|
| 새 셰이더 시각 표본 |0|
| 전체 효과품질/전체GPU PM/추가표시지연/물리입력 |확인 안 됨|
''';p.write_text(text,encoding='utf8')
p=root/'CLAUDE.md';text=p.read_text(encoding='utf8');anchor='## 측정에서 배운 것 (반복하지 말 것)\n';text=text.replace(anchor,anchor+'\n- **31장 주사율 기본과 ZIP 검증**: 새 주사율 선호true는 FrameGenOutside0의 기본꺼짐을 바꾸지 않는다. 휴식은mode9에 포함하고 기존 명시적false의2~8배는 유지한다. 실제 ZIP Settings/빈XML/기존4배XML·내장리소스를 읽어 확인한다. AutoTest 문자열 존재만으로 자동시험 빌드라고 하지 않는다(Edition은 진단타입을 양쪽에 컴파일). 실제 상수/신규시험훅과 기본꺼짐 런타임0을 검사한다. 일반PlayerAuto 완주2/2·SF오류0은 기존 DOTween775/775 오류0이나 모든 끊김0이 아니다. ZIP은 일반Debug, 완주는AutoTestRelease라는 빌드 차이도 남긴다.\n',1);p.write_text(text,encoding='utf8')
print(json.dumps(dict(full_songs=len(full['runs']),errors=sum(r['errors'] for r in full['runs']),restored=all(v['matches_original'] for v in rest.values()),zip=package['version']),ensure_ascii=False))
