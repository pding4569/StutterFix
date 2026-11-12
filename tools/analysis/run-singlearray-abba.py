"""One predeclared fresh-process ABBA, no capture or GPU readback in timed runs."""
import hashlib
import importlib.util
import json
import re
import sys
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('singlearray_runner',Path(__file__).with_name('continue-runlod.py'))
runner=importlib.util.module_from_spec(spec);spec.loader.exec_module(runner)
direct='--direct' in sys.argv
lean='--lean' in sys.argv
isolated='--isolated-mods' in sys.argv
destination=ROOT/('out/singletexture-lean-arche-abba.json' if lean else 'out/singletexture-direct-arche-abba.json' if direct else 'out/singlearray-arche-abba.json')
assert not destination.exists(),'A result already exists; do not repeat or overwrite a completed ABBA'
result=dict(scope='Unity FPS, not physical display FPS; new shader generic vs static '+('array or Texture2D' if direct else 'single-array')+' specialization',
            planned_order=['off','on','on','off'],map='Arche',requested_conductor_seconds=[262,274],
            capture=False,physical_display_latency='UNMEASURED',runs=[])
result['comparison']='specialized original VS vs lean VS' if lean else result['scope']
result['other_mods_isolated']=isolated
try:
    for mode in result['planned_order']:
        folder=runner.run('ar','reuse-gpuonly-map-on-singlearray-'+('direct-lean-' if lean else 'direct-' if direct else '')+mode+'-perf',windowed=True,isolated_mods=isolated)
        text=(folder/'Player.log').read_text(encoding='utf-8',errors='replace')
        lines=text.splitlines();windows=[(i,l) for i,l in enumerate(lines) if '[FPS 구간]' in l]
        assert len(windows)==1,'Missing or duplicate timing window'
        index,line=windows[0]
        assert not any('on=False owned=0 originals_restored=' in l for l in lines[:index]),'GPU returned to CPU before timing finished'
        assert 'SINGLE_ARRAY_RELEASE bundle_unloaded=True' in text,'No confirmed explicit bundle unload'
        assert 'SINGLE_ARRAY_RELEASE bundle_unloaded=UNCONFIRMED' not in text
        environment=list(dict.fromkeys(l.split('[곡 시작] 화면: ',1)[1] for l in lines if '[곡 시작] 화면: ' in l))
        assert len(environment)==1,'Display changed inside a run'
        fps=float(re.search(r': ([\d.]+) FPS',line)[1])
        state=[l for l in lines if 'SINGLE_ARRAY_STATE loaded=True reference=False control=False' in l and re.search(r'selected_spans=[1-9]',l)]
        assert state and all('optimize='+('True' if mode=='on' or lean else 'False') in l for l in state),'Wrong shader policy'
        assert all('actual_material_mismatches=0' in l for l in state),'Material keyword/texture mismatch'
        if lean:
            assert all(' lean='+('True' if mode=='on' else 'False') in l for l in state),'Wrong lean vertex state'
            assert all('actual_specialized_spans=500' in l and 'actual_lean_spans='+('500' if mode=='on' else '0') in l for l in state),'Specialized variant not bound'
        metrics=json.loads((folder/'metrics.json').read_text(encoding='utf-8'))
        assert metrics['errors']==0,'SF metrics errors'
        restore=json.loads((folder/'restore.json').read_text(encoding='utf-8'))
        prefs=json.loads((folder/'screen-preferences-restore.json').read_text(encoding='utf-8'))
        expected_files=10 if isolated else 9
        assert restore['equal'] and len(restore['before'])==expected_files and prefs['equal'] and prefs['values']==17
        row=dict(mode=mode,path=str(folder.relative_to(ROOT)),fps=fps,raw_window=line,environment=environment[0],
                 candidate_sha256=hashlib.sha256((folder/'candidate.dll').read_bytes()).hexdigest(),
                 shader_bundle_sha256=hashlib.sha256((folder/'singlearray.bundle').read_bytes()).hexdigest(),
                 normal_quit=True,restored_files=expected_files,restored_screen_preferences=17,explicit_bundle_unload=True,
                 state=state,filter_divide_by_zero=text.count('DivideByZeroException: Attempted to divide by zero.'))
        result['runs'].append(row)
        if len(result['runs'])>1:
            for key in ('environment','candidate_sha256','shader_bundle_sha256'):
                assert all(r[key]==row[key] for r in result['runs']),'Changed comparison condition: '+key
        destination.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
        print('ABBA_ROW '+json.dumps(dict(mode=mode,fps=fps,path=row['path'])),flush=True)
    means={m:sum(r['fps'] for r in result['runs'] if r['mode']==m)/2 for m in ('off','on')}
    result['mean_fps']=means;result['gain_percent']=100*(means['on']/means['off']-1)
    result['heavy_window_400_fps']=all(r['fps']>=400 for r in result['runs'] if r['mode']=='on')
except Exception as error:
    result['error']=repr(error);raise
finally:
    destination.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
