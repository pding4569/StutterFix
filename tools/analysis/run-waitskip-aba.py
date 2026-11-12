"""Fresh-process on/off/on; no hot patch toggles during a timed scene."""
import hashlib,importlib.util,json,re
from pathlib import Path
root=Path(__file__).resolve().parents[2];destination=root/'out/waitskip-arche-aba.json';assert not destination.exists()
spec=importlib.util.spec_from_file_location('wait_runner',Path(__file__).with_name('continue-runlod.py'));runner=importlib.util.module_from_spec(spec);spec.loader.exec_module(runner)
result=dict(scope='capture-free live Arche Unity FPS; waiting logs/counters are not CPU execution or GPU busy attribution',requested_song_seconds=[262,274],planned_order=['on','off','on'],runs=[])
try:
    for mode in result['planned_order']:
        p=runner.run('ar','reuse-gpuonly-map-on-waitskip-'+mode+'-perf',windowed=True,isolated_mods=True)
        text=(p/'Player.log').read_text(encoding='utf-8',errors='replace');lines=text.splitlines();timed=[(i,l) for i,l in enumerate(lines) if '[FPS 구간]' in l];assert len(timed)==1
        index,line=timed[0];assert 'waitskip-'+mode in line
        enabled=[s for s in lines if '[엔진 패치]' in s and 'WaitSkip v5' in s and '켬' in s]
        assert bool(enabled)==(mode=='on') and 'WaitSkip v5 바이트 불일치' not in text
        assert not any('on=False owned=0 originals_restored=' in l for l in lines[:index]),'GPU fallback before end'
        environment=list(dict.fromkeys(l.split('[곡 시작] 화면: ',1)[1] for l in lines if '[곡 시작] 화면: ' in l));assert len(environment)==1
        restore=json.loads((p/'restore.json').read_text());prefs=json.loads((p/'screen-preferences-restore.json').read_text());metrics=json.loads((p/'metrics.json').read_text())
        assert restore['equal'] and len(restore['before'])==10 and prefs['equal'] and prefs['values']==17 and metrics['errors']==0
        assert not list(p.glob('*.png'))
        row=dict(mode=mode,fps=float(re.search(r': ([\d.]+) FPS',line)[1]),raw=line,path=str(p.relative_to(root)),environment=environment[0],
                 presentation_warnings=text.count('PresentFrame() was called without calling'),candidate_sha256=hashlib.sha256((p/'candidate.dll').read_bytes()).hexdigest(),normal_quit=True,restored_files=10,restored_preferences=17)
        for prior in result['runs']:
            for key in ('environment','candidate_sha256'):assert prior[key]==row[key]
        result['runs'].append(row);destination.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print('ABA_ROW '+json.dumps(row,ensure_ascii=False),flush=True)
    result['stock_gain_percent_range']=sorted(100*(result['runs'][1]['fps']/r['fps']-1) for r in (result['runs'][0],result['runs'][2]))
    result['on_drift_percent']=100*(result['runs'][2]['fps']/result['runs'][0]['fps']-1)
except Exception as ex:result['error']=repr(ex);raise
finally:destination.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
