import hashlib, json, re, shutil
from pathlib import Path

root=Path(__file__).resolve().parents[3]
fg=root/'tools/framegenlab'
fx=root/'tools/effects'
full=json.loads((fg/'out/ch29-full-deadline/summary.json').read_text(encoding='utf8'))
assert len(full['runs'])==4 and all(r['completion_confirmed'] and not r['game_metrics']['errors'] and r['safety']['worker_error']=='0' and r['safety']['frame_begins']==r['safety']['frame_ends'] for r in full['runs'])
(fg/'validation-29/full-song.json').write_text(json.dumps(full,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
normal=json.loads((fg/'out/ch29-normal-refresh/summary.json').read_text(encoding='utf8'))
(fg/'validation-29/normal-refresh.json').write_text(json.dumps(normal,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
mod=Path('D:/SteamLibrary/steamapps/common/A Dance of Fire and Ice/Mods/StutterFix')
expected={'StutterFix.dll':'dd9ca8cadd52bb61aad3e3ab41a54f07e693fb8367653c27513bdef07fc193e1','sfnative.dll':'ad2dfdc5ed4d5cd416488bb8e1d96863601d9c3a57690cc25756c5f71eec89d1','Settings.xml':'fa6e781728a4c72c53aa1969a4d08707d837f15932bcf4e5c912bbbaf28d8cd5'}
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert {n:sha(mod/n) for n in expected}==expected
zipsha=sha(root/'dist/FrameGenLab-2026-10-07-tester.zip')
assert zipsha=='689de906e1b0513c526c34c0aff33dbff6b1d12fe9544adf78b8295a4932c134'
(fg/'validation-29/restoration.json').write_text(json.dumps(dict(installed_sha256=expected,existing_tester_zip_sha256=zipsha,new_zip=0),indent=2)+'\n',encoding='utf8')
table=['| 맵·모드 | 측정 길이초 | 진짜 장면 FPS | 성공 제출/s | 최악 원본/제출 간격ms | 원본/제출33.33ms초과 |','|---|---:|---:|---:|---:|---:|']
for r in full['runs']:
    n=r['full_native'];label=('HELLO' if r['map']=='hello' else 'Arche')+(' 마감' if r['mode']==9 else '4배')
    table.append(f"| {label} |{n['seconds']:.3f}|{n['scene_fps']:.3f}|{n['output_fps']:.3f}|{n['source_worst_ms']:.3f}/{n['output_worst_ms']:.3f}|{n['source_over_33ms']}/{n['output_over_33ms']}|")
doc=root/'docs/framegen-research.md'
text=doc.read_text(encoding='utf8')
marker='### 29장 후속 검증 상태'
assert text.count(marker)==1
text=text[:text.index(marker)]+'''### 29장 전체 완주와 일반 빌드 확인

두 곡 각각 마감/4배를 새 게임으로 끝까지 돌린4판이다. 첫/끝 화면 조건은
3440×1440·165Hz(종료 실제164.999Hz)·동기0으로4/4 일치했다.
기존5초 이후 집계를 같은 저장 자료의0초부터 다시 계산했다. 기본5초 집계의
기존 수치 동일성4/4 확인이며, 게임 재실행0이다. `full_deadline.py --analyze-only`는
완료한4판이 없으면 실패하며 게임을 새로 실행하지 않는다.
자료는 `tools/framegenlab/validation-29/full-song.json`이다.

'''+ '\n'.join(table)+'''

Arche 마감 원본 긴 간격은219.601336초62.3888ms·219.654666초44.6139ms,
4배는219.602023초66.3593ms·219.670519초47.1821ms다. 원인은 확인 안 됨이다.
성공 제출의 긴 간격은 물리 표시 간격이 아니다. 전체끔 대조가 없어 전체 원본
손실은 확인 안 됨이며, 전체 완주를 픽셀 움직임 품질 통과로 해석하지 않는다.
GPU 새 그림 계수는5~45초이며, motion 기록65536개 한도 이후도 렌더링은 계속되므로
후반 전체의 시각 품질을 계수로 확정하지 않는다.

완주 연구 DLL SHA는0965eb201865ec9132f03af9211d3f34bd75db0e1be25761fb627236ea888f94,
native는짧은판과같은c2db7d568f8736e56014ca8c5c2708341d723dbb0bdea54579f18c9ca1cc8526이다.
짧은판 뒤 추가된 명시적 자동시험 명령/종료화면 기록 때문에 managed는 다른 바이너리다.
일반 빌드 마감 기능 확인은캡처없는35.009832초, 진짜192.631604FPS·성공제출198.801300/s·
제출/원본1.032028배·집계오류0이다. 활성 연결1→끔 뒤 연결0을 확인했다.
`runtime_initialized=True`는 '한 번이라도 static 초기화'된 sticky 표시다.
끔 뒤 살아 있는 RT 개수/정리 실패를 나타내는 값으로 사용하지 않는다.
자료는 `normal-refresh.json`, 설치 복원3SHA와 기존ZIP SHA는 `restoration.json`이다.

| 결론 항목 | 수치 |
|---|---:|
| 새 끔/4배/끔/마감/끔 대조 |10/10|
| 두 곡 마감/4배 전체 완주 |4/4|
| 전체 집계 오류/worker 오류/잠금 불균형 |0/0/0|
| Arche 원본33.33ms초과 마감/4배 |2/2|
| 마감 원본 손실0.5~2.8% 범위 통과 |0/2|
| 3440·64px fixture 새 그림 flip0/1 |3/3·3/3|
| 일반 마감 활성/끔 뒤 원래 Present 연결 복원 |1/1·1/1|
| 실제 표시 FPS·최악 표시 간격·표시 지연 평균/최악 |확인 안 됨|
| 전체끔 대비 원본손실/전체 움직임 품질 |확인 안 됨|
| 설치3원본/기존ZIP SHA 복원·보존 |3/3·1/1|
| main 합치기·태그·릴리스·새 ZIP |0/0/0/0|
'''
doc.write_text(text,encoding='utf8')

public=fx/'validation-13';public.mkdir(exist_ok=True)
data={}
for name in ['hello','arche']:
    src=fx/f'out/ch29-{name}-presets2'
    d=json.loads((src/'summary.json').read_text(encoding='utf8'))
    assert len(d['captures'])==4 and len(d['fx_states'])==4 and not d['game_metrics']['errors']
    for c in d['captures']:
        source=src/(c['profile']+'.png');dest=public/(name+'-'+c['profile']+'.png')
        assert sha(source)==c['sha256']
        shutil.copyfile(source,dest)
        assert sha(dest)==c['sha256']
    shutil.copyfile(src/'comparison.png',public/(name+'-comparison.png'))
    (public/(name+'-presets.json')).write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    data[name]=d
failed=(fx/'out/ch29-hello-presets/game.log').read_text(encoding='utf8')
rejected=dict(accepted_png=0,requested_captures=4,reason='Preset fields changed without Main.ApplyConfig; three active materials remained ready=False. Excluded, logs preserved locally.',fx_states=re.findall(r'\[화면효과상태\] ([^\r\n]+)',failed))
(public/'excluded-helper-trial.json').write_text(json.dumps(rejected,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
rows=['| 맵 | 끔·先명·또렷·네온 캡처 요청 곡초 | 각 원본 PNG | 활성 ready/실행 확인 | 프레임 생성 연결/생성 |','|---|---|---:|---:|---:|']
for name,d in data.items():
    times='/'.join(f"{c['request_song_s']:.6f}" for c in d['captures'])
    rows.append(f"| {'HELLO 2026' if name=='hello' else 'Arche'} |{times}|3440×1440·4장|3/3|0/0|")
shader='''

## 13. 같은 곡 시간의 미리 설정 원본 비교 (2026-10-09)

한 맵씩 새 게임 실행, 각 미리 설정은 완전 다시 하기 뒤 자동 플레이했다.
맵 연출을 유지하고 프레임 생성·FSR·HalfRender를 끈 시각 판이다. 요청 목표는
두 곡 모두20초이며,8요청 각각 실제창/모니터3440×1440·164.999Hz·동기0을 확인했다.
끔0/선명1/또렷2/네온3은 기존 `ScreenEffects.Preset` 값 그대로다.
셰이더 코드/AssetBundle/미리 설정 값 변경0이다.

'''+ '\n'.join(rows).replace('先명','선명')+'''

원본8장은 `tools/effects/validation-13/{hello,arche}-{off,sharp,clear,neon}.png`이며,
`*-presets.json`에 요청 곡 시각/프레임·해상도·원본SHA·FX/연결 상태를 저장했다.
`*-comparison.png`는2×2 나란한 비교다(위 끔/선명, 아래 또렷/네온).
각 그림을 가로/세로4배 축소한 미리보기이며, 판단용3440 원본PNG의 바이트는 그대로 보존했다.
요청과 GPU 캡처 완료 시각은 같다고 확정하지 않는다. 같은 목표 곡 시각이지만
각 실행의 그림/행성/파티클까지 동일하다는 뜻이 아니며, 픽셀 동일성 대조가 아니다.
모니터의 순간 FPS/판정/키 상태 글자는 별도 실행/프레임에 따라 다를 수 있다.
이 판의 FPS/긴 간격은 성능/표시 지연 대조에 사용하지 않는다.

FX상태는 각 캡처 요청2초 뒤의 누적 실행 계수다. 활성6상태는ready=True·coordinator=True·
완료frames>0·실패False였으나, 캡처된 바로 그 프레임의 글로우 실행 여부는 확인 안 됨이다.
네온의 맵Bloom에 따른 글로우 휴식 누적은HELLO3945·Arche7542였으며,
이 캡처를 모든 네온 패스가 동시에 실행한 증거로 사용하지 않는다.
사용자 판단/선택은 확인 안 됨이며 의견 전 셰이더 조정0을 유지한다.

사전HELLO 도구 판1개는Preset 필드만 바꾸고Main.ApplyConfig를 누락했다.
활성3상태 모두ready=False·실행frames0이므로채택0/4, 원본로그는out에 보존했다.
자동 명령을 기존설정창/set과같은ApplyConfig로 연결하고 되돌릴Fx 필드도 저장했다.
같은게임retry는곡시작로그가1개라4개를 요구하던 도구검사도각 예약 요청의실제화면검사로 고쳤다.
PNG는 유효성 검사보다 먼저 보존하여 제외판의 그림을 지우지 않도록 했다.
이 수정은DEV/AUTOTEST 명시적 명령에만 있으며 일반 매프레임 계측 추가0이다.

| 결론 항목 | 수치 |
|---|---:|
| 같은20초 목표 원본 PNG/검증 해상도 |8/8·3440×1440|
| 캡처 요청 목표 뒤 최대 경과 |0.002334초|
| 활성 미리 설정 material ready/실행 확인 |6/6|
| 새 실행 기본 프레임 생성 연결/생성/초기화 |0/0/0·8/8|
| 집계 오류 |0/2|
| 셰이더 코드/AssetBundle/미리 설정 값 조정 |0/0/0|
| 첫 도구 판 채택 |0/4|
| 캡처 프레임 글로우 실행/픽셀 UI동일성/표시 지연 |확인 안 됨|
| 사용자 선택/조정 후 품질 |확인 안 됨|
| PlayerAuto LoadCheck 틀림/참조 수 |0/220|
| 설치3원본/기존ZIP SHA 복원·보존 |3/3·1/1|
| main 합치기·태그·릴리스·새 ZIP |0/0/0/0|
'''
doc=root/'docs/shader-research.md'
text=doc.read_text(encoding='utf8');assert '## 13.' not in text
doc.write_text(text+shader,encoding='utf8')
claude=root/'CLAUDE.md'
text=claude.read_text(encoding='utf8');marker='## 설정 창 디자인'
assert text.count(marker)==1
note='''- **29장 완주 집계**: 같은 저장 자료의5초 이후/0초 이후를 구분한다. 기본5초 집계 동일성4/4를 확인하고0초부터 재계산(재실행0), 완주4/4·worker오류0이어도Arche219.6초 원본62.39~66.36ms와44.61~47.18ms가 남는다. 전체끔 대조/물리표시/전체픽셀품질은 확인 안 됨이다. 일반끔 뒤runtime_initialized는ever-initialized 정적 표시이며 살아있는RT 개수로 읽지 않는다.
- **미리 설정 시각 도구의 적용/증거**: Preset 필드만 바꾸면 material ready=False·실행0인채 그림이 저장된다. 기존Main.ApplyConfig와 설정 원상복구를 함께 쓰고 실제ready/coordinator/frames 및각요청화면조건을 검증한다. retry의곡시작로그는1개일수있다. 요청곡20초±2.334ms는GPU캡처완료/완전히동일한그림시각의증명이아니다.2초뒤누적FX상태로캡처바로그프레임의글로우실행을확정하지않는다.8원본PNG는시각자료이며성능대조에넣지않는다(셰이더13장).

'''
claude.write_text(text.replace(marker,note+marker),encoding='utf8')
print('Final evidence saved:whole4,PNG8,restoration3+ZIP1')
