"""Capture-free paused cost; original image and shader are restored at the end."""
import json,re,sys,hashlib
from pathlib import Path
p=Path(sys.argv[1]);text=(p/'Player.log').read_text(encoding='utf-8',errors='replace');lines=text.splitlines();rows=[]
labels=['original-clean','off-before','on','off-after','original-released']
def value(s,k):return int(re.search(r'\b'+k+r'=(\d+)\b',s)[1])
for i,line in enumerate(lines):
    match=re.search(r'\[FPS 구간\] ([\w-]+) : ([\d.]+) FPS',line)
    if not match:continue
    before=next((s for s in reversed(lines[:i]) if '[자동 시험] SPRITE_CHANNELS ' in s),None)
    after=next((s for s in lines[i+1:] if '[자동 시험] SPRITE_CHANNELS ' in s),None)
    for state in (before,after):
        assert state and value(state,'pending')==value(state,'errors')==0
        n=value(state,'selected');assert value(state,'renderer_bound')==value(state,'source_sampler_valid')==n
        assert n>0 if match[1]=='on' else n==0
        if match[1] in ('off-before','on','off-after'):
            assert 'representation=conservative_empty_cells ' in state
            assert value(state,'prepared')>0 and value(state,'valid')>0 and value(state,'nongray')==0
        else:assert value(state,'prepared')==0
    assert value(before,'selected')==value(after,'selected')
    assert not any('on=False owned=0 originals_restored=' in s for s in lines[:i])
    rows.append(dict(mode=match[1],fps=float(match[2]),raw=line,state_before=before,state_after=after))
assert [r['mode'] for r in rows]==labels
assert '[spritechannels] released=True restored=True pending=0' in text and 'release=UNCONFIRMED' not in text
assert '[자동 시험] 끝 - 게임을 끕니다' in text and '단계 실패' not in text and not list(p.glob('*.png'))
restore=json.loads((p/'restore.json').read_text());prefs=json.loads((p/'screen-preferences-restore.json').read_text())
assert restore['equal'] and len(restore['before'])==10 and prefs['equal'] and prefs['values']==17
environment=list(dict.fromkeys(l.split('[곡 시작] 화면: ',1)[1] for l in lines if '[곡 시작] 화면: ' in l));assert len(environment)==1
result=dict(scope='paused Arche 262s, Unity FPS; live playback, physical display, whole-song quality UNMEASURED',runs=rows,environment=environment[0],
    normal_quit=True,restored_files=10,restored_screen_preferences=17,temporary_payload_bytes=value(rows[2]['state_before'],'prepared_payload_bytes'),
    active_renderers=value(rows[2]['state_before'],'selected'),gain_vs_original_percent_range=sorted(100*(rows[2]['fps']/r['fps']-1) for r in (rows[0],rows[4])),
    gain_vs_prepared_percent_range=sorted(100*(rows[2]['fps']/r['fps']-1) for r in (rows[1],rows[3])),original_drift_percent=100*(rows[4]['fps']/rows[0]['fps']-1),
    candidate_sha256=hashlib.sha256((p/'candidate.dll').read_bytes()).hexdigest(),bundle_sha256=hashlib.sha256((p/'channels.bundle').read_bytes()).hexdigest())
(p/'mask-cost.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(result,ensure_ascii=False,indent=2))
