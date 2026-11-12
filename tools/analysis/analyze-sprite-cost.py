"""Retain a bounded paused A/B/A; never call it whole-song or display FPS."""
import hashlib
import json
import re
import sys
from pathlib import Path

p=Path(sys.argv[1]);text=(p/'Player.log').read_text(encoding='utf-8',errors='replace');lines=text.splitlines()
rows=[]
for i,line in enumerate(lines):
    match=re.search(r'\[FPS 구간\] (off-before|on|off-after) : ([\d.]+) FPS',line)
    if not match:continue
    near=[s for s in lines[max(0,i-40):i+15] if 'SPRITE_DISCARD loaded=True' in s]
    # Commands may be separated by many pre-existing engine warnings. Find
    # the actual preceding state rather than guessing within a time radius.
    before=next((s for s in reversed(lines[:i]) if '[자동 시험] SPRITE_DISCARD ' in s),None)
    after=next((s for s in lines[i+1:] if '[자동 시험] SPRITE_DISCARD ' in s),None)
    active=match[1]=='on';count=42 if active else 0
    for state in (before,after):
        assert state and all(f'{k}={count} ' in state for k in ('selected','bound','renderer_bound')),state
    assert not any('on=False owned=0 originals_restored=' in s for s in lines[:i]),'GPU fallback before timing'
    rows.append(dict(mode=match[1],fps=float(match[2]),raw=line,state_before=before,state_after=after))
assert [r['mode'] for r in rows]==['off-before','on','off-after'],'incomplete timing windows'
restore=json.loads((p/'restore.json').read_text());prefs=json.loads((p/'screen-preferences-restore.json').read_text())
assert restore['equal'] and len(restore['before'])==10 and prefs['equal'] and prefs['values']==17
assert '[spritediscard] released=True restored=True' in text and 'release=UNCONFIRMED' not in text
assert '[자동 시험] 끝 - 게임을 끕니다' in text and '단계 실패' not in text
assert not list(p.glob('*.png')),'capture contaminated a performance-only run'
environment=list(dict.fromkeys(s.split('[곡 시작] 화면: ',1)[1] for s in lines if '[곡 시작] 화면: ' in s))
assert len(environment)==1
result=dict(scope='paused Arche 262s, capture-free Unity FPS; not live playback, GPU busy or physical display FPS',
            environment=environment[0],runs=rows,normal_quit=True,restored_files=10,restored_screen_preferences=17,
            shader_bundle_sha256=hashlib.sha256((p/'sprite.bundle').read_bytes()).hexdigest(),
            candidate_sha256=hashlib.sha256((p/'candidate.dll').read_bytes()).hexdigest(),
            off_drift_percent=100*(rows[2]['fps']/rows[0]['fps']-1),
            gain_percent_range=sorted(100*(rows[1]['fps']/r['fps']-1) for r in (rows[0],rows[2])),
            engine_presentation_warnings=text.count('PresentFrame() was called without calling'),
            live_400_fps='UNMEASURED',whole_song_quality='UNMEASURED')
(p/'sprite-cost.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(result,ensure_ascii=False,indent=2))
