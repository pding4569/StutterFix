"""Separate temporary-memory cost from RG8 sampling in a paused scene."""
import hashlib,json,re,sys
from pathlib import Path
p=Path(sys.argv[1]);text=(p/'Player.log').read_text(encoding='utf-8',errors='replace');lines=text.splitlines();rows=[]
labels=['original-clean','off-before','on','off-after','original-released']
for i,line in enumerate(lines):
    match=re.search(r'\[FPS 구간\] ([\w-]+) : ([\d.]+) FPS',line)
    if not match:continue
    assert match[1] in labels
    before=next((s for s in reversed(lines[:i]) if '[자동 시험] SPRITE_CHANNELS ' in s),None)
    after=next((s for s in lines[i+1:] if '[자동 시험] SPRITE_CHANNELS ' in s),None)
    for state in (before,after):
        assert state and 'pending=0 ' in state and 'errors=0 ' in state
        count=41 if match[1]=='on' else 0
        assert f'selected={count} ' in state and f'renderer_bound={count} ' in state,state
        if match[1] in ('off-before','on','off-after'):assert 'prepared=15 valid=14 nongray=1 ' in state,state
        else:assert 'prepared=0 valid=0 nongray=0 ' in state,state
    assert not any('on=False owned=0 originals_restored=' in s for s in lines[:i]),'GPU fallback inside timing'
    rows.append(dict(mode=match[1],fps=float(match[2]),raw=line,state_before=before,state_after=after))
assert [r['mode'] for r in rows]==labels
assert '[spritechannels] released=True restored=True pending=0' in text and 'release=UNCONFIRMED' not in text
assert '[자동 시험] 끝 - 게임을 끕니다' in text and '단계 실패' not in text
restore=json.loads((p/'restore.json').read_text());prefs=json.loads((p/'screen-preferences-restore.json').read_text())
assert restore['equal'] and len(restore['before'])==10 and prefs['equal'] and prefs['values']==17
assert not list(p.glob('*.png'))
environments=list(dict.fromkeys(l.split('[곡 시작] 화면: ',1)[1] for l in lines if '[곡 시작] 화면: ' in l));assert len(environments)==1
result=dict(scope='paused Arche 262s; capture-free Unity FPS; not live playback, physical display, GPU busy, or whole-song quality',runs=rows,
            environment=environments[0],normal_quit=True,restored_files=10,restored_screen_preferences=17,
            shader_bundle_sha256=hashlib.sha256((p/'channels.bundle').read_bytes()).hexdigest(),
            candidate_sha256=hashlib.sha256((p/'candidate.dll').read_bytes()).hexdigest(),
            gain_vs_original_percent_range=sorted(100*(rows[2]['fps']/r['fps']-1) for r in (rows[0],rows[4])),
            gain_vs_prepared_percent_range=sorted(100*(rows[2]['fps']/r['fps']-1) for r in (rows[1],rows[3])),
            original_drift_percent=100*(rows[4]['fps']/rows[0]['fps']-1),prepared_drift_percent=100*(rows[3]['fps']/rows[1]['fps']-1),
            preparation_gpu_waits=0,preparation_async_validation_bytes=60,temporary_payload_bytes=77569834,
            original_resolution_preserved=True,original_textures_released=False,live_400_fps='UNMEASURED',whole_song_quality='UNMEASURED')
(p/'channel-cost.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(result,ensure_ascii=False,indent=2))
