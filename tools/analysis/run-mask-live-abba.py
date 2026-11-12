"""One fresh-process ABBA. Endpoint quality does not prove every live frame."""
import hashlib,importlib.util,json,re
from pathlib import Path
root=Path(__file__).resolve().parents[2];destination=root/'out/mask-live-arche-abba.json'
assert not destination.exists(),'Preserve the completed or failed ABBA; no automatic retries'
spec=importlib.util.spec_from_file_location('mask_runner',Path(__file__).with_name('continue-runlod.py'));runner=importlib.util.module_from_spec(spec);spec.loader.exec_module(runner)
def number(s,key):return int(re.search(r'\b'+key+r'=(\d+)\b',s)[1])
result=dict(scope='capture-free live Unity FPS; physical display and all-frame quality UNMEASURED',map='Arche',requested_song_seconds=[262,274],planned_order=['off','on','on','off'],other_mods_isolated=True,runs=[])
try:
    for mode in result['planned_order']:
        p=runner.run('ar','reuse-gpuonly-map-on-mask-live-'+mode+'-perf',windowed=True,isolated_mods=True)
        text=(p/'Player.log').read_text(encoding='utf-8',errors='replace');lines=text.splitlines();windows=[(i,l) for i,l in enumerate(lines) if '[FPS 구간]' in l]
        assert len(windows)==1;index,raw=windows[0]
        assert 'live-mask-'+mode in raw
        before=next(l for l in reversed(lines[:index]) if '[자동 시험] SPRITE_CHANNELS ' in l)
        after=next(l for l in lines[index+1:] if '[자동 시험] SPRITE_CHANNELS ' in l)
        for state in (before,after):
            assert 'representation=conservative_empty_cells ' in state
            assert number(state,'pending')==number(state,'errors')==0
            n=number(state,'selected');assert n>0 if mode=='on' else n==0
            assert number(state,'renderer_bound')==number(state,'source_sampler_valid')==n
        retired_delta=number(after,'callback_restored')-number(before,'callback_restored')
        assert number(before,'selected')-number(after,'selected')==retired_delta>=0,'Unexplained active renderer change'
        assert 'callback_error=none ' in before and 'callback_error=none ' in after
        assert not any('on=False owned=0 originals_restored=' in l for l in lines[:index]),'GPU fell back before timing completed'
        assert '[spritechannels] released=True restored=True pending=0' in text and 'release=UNCONFIRMED' not in text
        assert '[자동 시험] 끝 - 게임을 끕니다' in text and '단계 실패' not in text and not list(p.glob('*.png'))
        restore=json.loads((p/'restore.json').read_text());prefs=json.loads((p/'screen-preferences-restore.json').read_text());metrics=json.loads((p/'metrics.json').read_text())
        assert restore['equal'] and len(restore['before'])==10 and prefs['equal'] and prefs['values']==17 and metrics['errors']==0
        environments=list(dict.fromkeys(l.split('[곡 시작] 화면: ',1)[1] for l in lines if '[곡 시작] 화면: ' in l));assert len(environments)==1
        row=dict(mode=mode,path=str(p.relative_to(root)),fps=float(re.search(r': ([\d.]+) FPS',raw)[1]),raw_window=raw,state_before=before,state_after=after,
                 environment=environments[0],candidate_sha256=hashlib.sha256((p/'candidate.dll').read_bytes()).hexdigest(),bundle_sha256=hashlib.sha256((p/'channels.bundle').read_bytes()).hexdigest(),
                 normal_quit=True,restored_files=10,restored_screen_preferences=17)
        row.update(active_before=number(before,'selected'),active_after=number(after,'selected'),event_restored_renderers=retired_delta)
        for prior in result['runs']:
            for key in ('environment','candidate_sha256','bundle_sha256'):assert prior[key]==row[key],'Comparison condition changed: '+key
        result['runs'].append(row);destination.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print('ABBA_ROW '+json.dumps(row,ensure_ascii=False),flush=True)
    means={mode:sum(r['fps'] for r in result['runs'] if r['mode']==mode)/2 for mode in ('off','on')}
    result.update(mean_fps=means,gain_percent=100*(means['on']/means['off']-1),heavy_window_400_fps=all(r['fps']>=400 for r in result['runs'] if r['mode']=='on'))
except Exception as error:result['error']=repr(error);raise
finally:destination.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
