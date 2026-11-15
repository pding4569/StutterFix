"""Audit current M4i offline evidence. No launch/install/commit paths."""
import hashlib
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[2]
out = root/'out/direct-renderer/m4i'
validation = out/'validation'

def read(path):
    b = path.read_bytes()
    return b.decode('utf-16') if b.startswith((b'\xff\xfe',b'\xfe\xff')) else b.decode('utf-8-sig')

assert '74/74 including sf_z_drfull_v1' in read(validation/'exports-m4d-result.txt')
assert 'OK ' in read(validation/'build-native.log')
native = (root/'native/sfnative.dll').read_bytes()
original = (root/'effects/direct-full/original-shaders.bin').read_bytes()
for edition, directory in [('dev','Release'),('player','Player'),('playerauto','PlayerAuto')]:
    assert re.search(r'(오류\s*0개|0 Error\(s\))',read(validation/f'build-{edition}.log')), edition
    binary = (root/f'bin/{directory}/StutterFix.dll').read_bytes()
    assert native in binary, 'stale embedded native: '+edition
    assert (original in binary) == (edition != 'player'), 'diagnostic resource boundary: '+edition
for file, marker in [('contract.log','PASS production resource loader'),('lifecycle.log','PASS preparation completion/failure'),
                     ('policy-final.log','oracle_edits=7000'),('pixel-metrics.log','19 checks passed')]:
    assert marker in read(validation/file),file
assert 'PASS installed DLL hashes preserved: 2/2' in read(validation/'installed-result.txt')
before=json.loads(read(validation/'installed-before.json'));after=json.loads(read(validation/'installed-after.json'))
assert {r['Path']:r['Hash'] for r in before}=={r['Path']:r['Hash'] for r in after}

main = read(validation/'warp.log')
assert 'PASS cases=48 maxChannel=1 differingPixels=28 controls=5' in main
for marker in ('PASS SPSC packets=128','PASS M4h timeline samples=3 auxiliary_samples=3',
               'PASS M4i state IA=8 shaders=6','late_config=rejected','PASS M4i identical-list packets=16',
               'PASS M4i failed-segment state=restored errors=1'):
    assert marker in main,marker
micro=[]
for mode in ('','deferred-','immediate-legacy-'):
    for name in ('default','grab','named-grab'):
        log=read(out/f'{mode}{name}.log')
        assert 'PASS cases=8 maxChannel=0 differingPixels=0' in log,(mode,name)
        if name=='default':
            rows=re.findall(r'PASS M4f batch (\S+) sources=3 draws=1 changed=(\d+) max=(\d+)',log)
            assert len(rows)==8 and all(x[1:]==('0','0') for x in rows)
            assert len(re.findall(r'PASS M4g thin .*transforms=24 differing_channels=0',log))==8
            assert 'index_bytes_per_frame=0 order_bytes_per_frame=84000 cpp_heap_calls=0' in log
            assert 'PASS M4i identical-list packets=16 order_upload_bytes=0 order_dispatches=0' in log
            row=re.search(r'PASS M4i singles sources=345 frames=128 draws_per_frame=345 cpp_heap_calls=0 ring_maps=(\d+) ring_bindings=(\d+) render_us=([\d.]+) immediate=(\d+) deferred=(\d+)',log)
            assert row,(mode,'missing single fixture')
            maps,bindings,us,immediate,deferred=row.groups()
            if mode=='':assert int(maps)<=128 and int(bindings)>0 and int(immediate)>0 and int(deferred)==0
            elif mode=='deferred-':assert int(immediate)==0 and int(deferred)>0 and int(bindings)==0
            else:assert int(immediate)>0 and int(deferred)==0 and int(bindings)==0
            micro.append(dict(mode=mode or 'immediate-ring',render_us=float(us),ring_maps=int(maps),ring_bindings=int(bindings),scope='WARP synthetic 345 singles; NOT Exit'))
    assert 'PASS M4h Grab memory allocated=98304 idle_bytes=0 restored=98304' in read(out/f'{mode}grab.log')
    for name in ('grab','named-grab'):
        assert 'PASS M4i active-scope-error MSAA_Grab=rejected state=restored errors=1' in read(out/f'{mode}{name}.log')
for marker in ('PASS WARP composite=24','shared-stencil=9','PASS M4h rounding controls=6',
               'PASS M4h shared-depth renders=6'):
    assert marker in read(out/'composite.log'),marker
rows=re.findall(r'PASS M4f batch (\S+) sources=3 draws=1 changed=(\d+) max=(\d+)',main)
assert len(rows)==48 and all(x[1:]==('0','0') for x in rows)
assert len(re.findall(r'PASS M4g thin .*transforms=24 differing_channels=0',main))==48

manifest=json.loads(read(out/'plans/manifest.json'))
assert len(manifest['plans'])==24 and len(manifest['diagnostics'])==19 and manifest['launch'] is False
for path in manifest['plans']:
    plan=json.loads(read(Path(path)));steps=plan['steps']
    for cmd in ('drfull submit immediate','drfull constants ring','drfull particles explicit','drfull probe clear','drfull pressure restore'):
        assert cmd in steps,(path,cmd)
    assert steps[-2:]==['drfull cold','quit']
    if plan['measurement']['candidate']:
        assert 'drfull wait' in steps and steps.index('pause')<steps.index('drfull on')
    if path.endswith('-pixel.json'):
        assert 'framecmp assertzero d0 d00' in steps and 'framecmp assertlimit d0 d1 0.1 3 png' in steps
for path in manifest['diagnostics']:
    plan=json.loads(read(Path(path)));steps=plan['steps']
    shots=set()
    for step in steps:
        if step=='framecmp clear':shots.clear()
        elif step.startswith('framecmp grab '):shots.add(step.split()[-1])
        elif step=='drfull probe bisect':shots.add('probe_test')
        elif step.startswith('framecmp drop '):shots.remove(step.split()[-1])
        assert len(shots)<=8,(path,'FrameCompare capture limit exceeded',shots)
    assert steps[-1]=='quit' and plan['measurement']['diagnostic'] and not plan['measurement']['pixel_gate']
    if path.endswith('-bisect.json'):
        assert steps.index('framecmp grab onref')<steps.index('framecmp grab offref')<steps.index('drfull probe bisect')
        assert steps.count('drfull probe list')==1 and 'drfull probe clear' in steps
    if 'crossover' in path:
        assert [s for s in steps if s in ('drfull submit deferred','drfull submit immediate')][-5:-1]==['drfull submit deferred','drfull submit immediate','drfull submit immediate','drfull submit deferred']
        assert 'drfull pressure restore' in steps

files=['DirectFull.cs','DirectFull.Render.cs','DirectFull.Native.cs','DirectFull.Diagnostics.cs','DirectFull.Probes.cs',
       'AutoTest.cs','FrameCompare.cs','StutterFix.csproj',
       'native/sfnative/drfull.cpp','native/sfnative/drfull.h','tools/direct-renderer/drfull-warp.cpp',
       'tools/direct-renderer/plan-m4i.py','tools/direct-renderer/plan-m4c.py','tools/direct-renderer/check-m4i.ps1','tools/direct-renderer/check-m4i-warp.ps1','tools/direct-renderer/check-m4i-artifacts.py',
       'native/sfnative.dll','bin/Release/StutterFix.dll','bin/Player/StutterFix.dll','bin/PlayerAuto/StutterFix.dll']
(out/'final-hashes.json').write_text(json.dumps({p:hashlib.sha256((root/p).read_bytes()).hexdigest() for p in files},indent=2),encoding='utf-8')
audit=dict(builds=4,exports=74,batch_exact=72,thin_transforms_exact=1728,grab_exact=48,plans=24,diagnostic_plans=19,
           index_host_upload_bytes=0,same_list_gpu_rebuilds=0,microbench=micro,game_run=False,install_write=False,commit=False,
           dd_residency_hypothesis='UNMEASURED in game/hardware',exit_render_target_600us='UNMEASURED in game',
           hello_missing_renderer='UNMEASURED; runtime pixel-guided bisection implemented',decoration_rounding_attribution='UNMEASURED in game')
(out/'m4i-audit.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2),encoding='utf-8')
print('PASS M4i builds=4 exports=74 batch_exact=72 thin=1728 Grab=48 plans=24+19 index_host_upload=0 installed=2/2')
print('HOLD hardware DD residency / Exit <600us / HELLO identity / actual decoration attribution: no game execution')
