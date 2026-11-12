"""One fresh-process ABBA, gated by exact-input and actual PNG equivalence."""
import hashlib,importlib.util,json,re
from pathlib import Path
root=Path(__file__).resolve().parents[2];destination=root/'out/clock-memo-arche-abba.json'
assert not destination.exists(),'Preserve completed/failed tests; no blind retry'
proofs=sorted((root/'out').glob('continue-*-ar-reuse-gpuonly-map-on-clock*-visual/pixels.json'))
assert proofs,'No actual clock-cache PNG proof'
proof=proofs[-1];pixels=json.loads(proof.read_text());assert pixels['exact_pass'] and pixels['visible_control_pass']
expected=hashlib.sha256((proof.parent/'candidate.dll').read_bytes()).hexdigest()
assert hashlib.sha256((root/'bin/PlayerAuto/StutterFix.dll').read_bytes()).hexdigest()==expected,'Proof DLL differs'
quality=(proof.parent/'Player.log').read_text(encoding='utf-8',errors='replace')
shadow=re.findall(r'\[gpudeco\] clockmemo enabled=True hits=(\d+) misses=(\d+) compared=(\d+) mismatch=(\d+) verify=True',quality)
assert len(shadow)==1 and int(shadow[0][2])>100000 and int(shadow[0][3])==0,'Live shadow failed'
assert re.search(r'reference_compared=[1-9]\d* mismatch=0 paused=True clocks_advanced=0',quality),'Reference upload failed'
spec=importlib.util.spec_from_file_location('clock_runner',Path(__file__).with_name('continue-runlod.py'));runner=importlib.util.module_from_spec(spec);spec.loader.exec_module(runner)
result=dict(scope='capture-free live Arche Unity FPS; elapsed managed cost is not thread CPU or GPU busy',song_seconds=[262,274],quality_proof=str(proof.relative_to(root)),order=['off','on','on','off'],runs=[])
try:
    for mode in result['order']:
        p=runner.run('ar','reuse-gpuonly-map-on-clockmemo-'+mode+'-perf',windowed=True,isolated_mods=True)
        lines=(p/'Player.log').read_text(encoding='utf-8',errors='replace').splitlines()
        timed=[(i,l) for i,l in enumerate(lines) if '[FPS 구간]' in l];assert len(timed)==1;index,line=timed[0]
        assert 'clockmemo-'+mode in line
        states=[l for l in lines if '[gpudeco] clockmemo enabled=' in l and 'hits=' in l];assert len(states)==2
        assert all(('enabled=True' if mode=='on' else 'enabled=False') in s and 'verify=False' in s and 'mismatch=0' in s for s in states)
        assert not any('[gpudeco] on=False owned=0 originals_restored=' in l for l in lines[:index]),'Early GPU fallback'
        assert not list(p.glob('*.png'))
        restore=json.loads((p/'restore.json').read_text());prefs=json.loads((p/'screen-preferences-restore.json').read_text());metrics=json.loads((p/'metrics.json').read_text())
        assert restore['equal'] and len(restore['before'])==10 and prefs['equal'] and prefs['values']==17 and metrics['errors']==0
        environments=list(dict.fromkeys(l.split('[곡 시작] 화면: ',1)[1] for l in lines if '[곡 시작] 화면: ' in l));assert len(environments)==1
        cost=[l for l in lines[index+1:] if '[gpudeco] cost scope=manual ' in l];assert len(cost)==1
        def count(s,key):return int(re.search(r'\b'+key+r'=(\d+)\b',s)[1])
        row=dict(mode=mode,path=str(p.relative_to(root)),fps=float(re.search(r': ([\d.]+) FPS',line)[1]),raw_window=line,
                 hits=count(states[1],'hits')-count(states[0],'hits'),misses=count(states[1],'misses')-count(states[0],'misses'),
                 managed_elapsed_cost=cost[0],environment=environments[0],candidate_sha256=hashlib.sha256((p/'candidate.dll').read_bytes()).hexdigest(),restored_files=10,restored_preferences=17)
        assert row['candidate_sha256']==expected
        if mode=='on':assert row['hits']>0 and row['misses']>0
        for previous in result['runs']:assert row['environment']==previous['environment']
        result['runs'].append(row);destination.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print('CLOCK_ABBA '+json.dumps(row,ensure_ascii=False),flush=True)
    result['off_mean_fps']=sum(r['fps'] for r in result['runs'] if r['mode']=='off')/2
    result['on_mean_fps']=sum(r['fps'] for r in result['runs'] if r['mode']=='on')/2
    result['gain_percent']=100*(result['on_mean_fps']/result['off_mean_fps']-1)
    result['off_drift_percent']=100*(result['runs'][-1]['fps']/result['runs'][0]['fps']-1)
    result['target400_met']=all(r['fps']>=400 for r in result['runs'] if r['mode']=='on')
except Exception as e:result['error']=repr(e);raise
finally:destination.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
