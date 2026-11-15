"""Verify M4k current artifacts/embedded binaries, not historical success logs."""
import hashlib
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[2]
out = root/'out/direct-renderer/m4k'
validation = out/'validation'


def read(path):
    value = path.read_bytes()
    return value.decode('utf-16') if value.startswith((b'\xff\xfe',b'\xfe\xff')) else value.decode('utf-8-sig')


assert '74/74 including sf_z_drfull_v1' in read(validation/'exports-m4d-result.txt')
assert 'OK ' in read(validation/'build-native.log')
native = (root/'native/sfnative.dll').read_bytes()
for edition, directory in [('dev','Release'),('player','Player'),('playerauto','PlayerAuto')]:
    assert re.search(r'(오류\s*0개|0 Error\(s\))', read(validation/f'build-{edition}.log')), edition
    binary = (root/f'bin/{directory}/StutterFix.dll').read_bytes()
    assert native in binary, 'embedded native stale: '+edition
assert 'PASS installed DLL hashes preserved: 2/2' in read(validation/'installed-result.txt')
before = json.loads(read(validation/'installed-before.json')); after = json.loads(read(validation/'installed-after.json'))
assert {r['Path']:r['Hash'] for r in before} == {r['Path']:r['Hash'] for r in after}
for filename, marker in [('contract.log','PASS production resource loader'),('lifecycle.log','PASS preparation completion/failure'),
                         ('policy-final.log','oracle_edits=7000'),('pixel-metrics.log','19 checks passed')]:
    assert marker in read(validation/filename)
assert 'PASS cases=48 maxChannel=1 differingPixels=28 controls=5' in read(validation/'warp.log')
for mode in ('','deferred-','immediate-legacy-'):
    for kind in ('default','grab','named-grab'):
        log = read(out/f'{mode}{kind}.log')
        assert 'PASS cases=8 maxChannel=0 differingPixels=0' in log
        if kind == 'default':
            assert len(re.findall(r'PASS M4f batch .* sources=3 draws=1 changed=0 max=0',log)) == 8
            assert len(re.findall(r'PASS M4g thin .*transforms=24 differing_channels=0',log)) == 8
            assert 'index_bytes_per_frame=0 order_bytes_per_frame=84000 cpp_heap_calls=0' in log
            assert re.search(r'PASS M4i singles .*cpp_heap_calls=0',log)
        else:
            assert 'PASS M4i active-scope-error MSAA_Grab=rejected state=restored errors=1' in log
assert 'PASS M4j ring live_bytes=6291456 static_frames=16 static_upload=0' in read(out/'m4j.log')
assert 'probe_invalid=0' in read(out/'m4j.log')
fixture = read(out/'m4k.log')
for first in (0,256):
    assert re.search(fr'PASS M4k window first={first} actual_visible=315 rows=[1-9]\d* invalid=0',fixture)
assert 'first=512 actual_visible=315 rows=0 invalid=0 sampled_segments=0 skipped=0' in fixture
for marker in ('invisible_motion_upload=0 reentry=uploaded parity=exact','slot=127 stale_unbinds=1 skip_grab=0 copies=1 skipped=0',
               'slot=127 stale_unbinds=1 skip_grab=1 copies=0 skipped=1','replacement_consumed=released removed_pass=released retired=2',
               'uploads mode=0 default_updates=9 default_bytes=2016 discard_maps=10',
               'uploads mode=1 default_updates=0 default_bytes=0 discard_maps=19',
               'uploads mode=2 default_updates=0 default_bytes=0 discard_maps=1 gpu_dispatches=0 parity=exact'):
    assert marker in fixture, marker
assert 'invalid_inputs_rejected=6' in read(out/'draw-analyzer.log')
manifest = json.loads(read(out/'plans/manifest.json'))
assert manifest['launch'] is False and len(manifest['plans'])==24 and len(manifest['diagnostics'])==32
for filename in manifest['plans'] + manifest['diagnostics']:
    plan = json.loads(read(Path(filename))); steps = plan['steps']
    assert steps[-1]=='quit' and 'drfull resources default' in steps
    assert steps.index('drfull resources default') < steps.index('drfull warm') if 'drfull warm' in steps else True
    for setting in ('drfull submit immediate','drfull constants ring','drfull particles explicit','drfull probe clear','drfull pressure restore'):
        assert setting in steps, (filename, setting)
    if 'imagemax1024' in filename or 'resources-1024' in filename:
        load = next(i for i,s in enumerate(steps) if s.startswith('open '))
        assert steps.index('set ImageMaxSide 1024') < load and plan['settings']['ImageMaxSide']==1024
    if '-resources-' in filename:
        assert steps.count('drfull drawgpu assert')==4 and steps.count('drfull drawgpu dump')==4
        assert plan['measurement']['diagnostic'] and not plan['measurement']['pixel_gate']
    if filename in manifest['plans']:
        assert 'drfull drawgpu on' not in '\n'.join(steps)
        if filename.endswith('-pixel.json'):
            assert 'framecmp assertzero d0 d00' in steps and 'framecmp assertlimit d0 d1 0.1 3 png' in steps
        if plan['measurement']['candidate']:
            assert 'drfull wait' in steps and steps.index('pause') < steps.index('drfull on')
sources = ['DirectFull.cs','DirectFull.Render.cs','DirectFull.Native.cs','DirectFull.Diagnostics.cs','DirectFull.Shaders.cs',
           'native/sfnative/drfull.cpp','native/sfnative/drfull.h','native/sfnative/drfull-draw-probe.h',
           'tools/direct-renderer/drfull-m4k-warp.cpp','tools/direct-renderer/plan-m4k.py','tools/direct-renderer/check-m4k.ps1',
           'tools/direct-renderer/check-m4k-warp.ps1','tools/direct-renderer/summarize-m4k-drawgpu.py','tools/direct-renderer/analyze-m4k-existing.py']
hashes = {p:hashlib.sha256((root/p).read_bytes()).hexdigest() for p in sources}
(out/'final-hashes.json').write_text(json.dumps(hashes,indent=2),encoding='utf-8')
audit = dict(builds=4,exports=74,embedded_native=3,installed_unchanged=2,regular_plans=24,diagnostic_plans=32,
             game_run=False,install_write=False,commit=False,probe_visible=315,invisible_constant_upload=0,
             replacement_retirement='PASS WARP',fixed_vs_renamed_upload='PASS WARP exact',
             real_game_m4k_FPS='UNMEASURED',real_game_m4k_GPU='UNMEASURED',page_migration_cause='UNMEASURED')
(out/'m4k-audit.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2),encoding='utf-8')
print('PASS M4k builds=4 exports=74 embedded_native=3 installed_unchanged=2 WARP=PASS plans=24+32')
print('HOLD real M4k FPS/GPU/page-migration/remaining-pixel acceptance: no game execution')
