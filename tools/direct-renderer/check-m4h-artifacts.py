"""M4h evidence audit. Offline builds, WARP and generated JSON only."""
import hashlib, json, re
from pathlib import Path

root = Path(__file__).resolve().parents[2]
out = root / 'out/direct-renderer/m4h'
validation = out / 'validation'

def read(path):
    b = path.read_bytes()
    return b.decode('utf-16') if b.startswith((b'\xff\xfe', b'\xfe\xff')) else b.decode('utf-8-sig')

assert '74/74 including sf_z_drfull_v1' in read(validation / 'exports-m4d-result.txt')
assert 'OK ' in read(validation / 'build-native.log')
native = (root / 'native/sfnative.dll').read_bytes()
resource = (root / 'effects/direct-full/original-shaders.bin').read_bytes()
for edition, binary in [('dev', 'Release'), ('player', 'Player'), ('playerauto', 'PlayerAuto')]:
    assert re.search(r'(오류\s*0개|0 Error\(s\))', read(validation / f'build-{edition}.log')), edition
    b = (root / f'bin/{binary}/StutterFix.dll').read_bytes()
    assert native in b, f'{edition}: stale embedded native'
    assert (resource in b) == (edition != 'player'), f'{edition}: shader resource boundary'
for log, required in [('contract.log', 'PASS production resource loader'),
                      ('lifecycle.log', 'PASS preparation completion/failure'),
                      ('policy-final.log', 'PASS M4h explicit pass policy forward=3 excluded=8 unknown_rejected=3'),
                      ('pixel-metrics.log', '19 checks passed')]:
    assert required in read(validation / log), log

for path, n in [(validation / 'warp.log', 48), (out / 'default.log', 8)]:
    log = read(path)
    rows = re.findall(r'PASS M4f batch (\S+) sources=3 draws=1 changed=(\d+) max=(\d+)', log)
    assert len(rows) == n and all(row[1:] == ('0', '0') for row in rows)
    assert len(re.findall(r'PASS M4g thin .*transforms=24 differing_channels=0', log)) == n
    assert len(re.findall(r'PASS M4g delta .*pixels=identical', log)) == n
    assert len(re.findall(r'PASS M4g batch-cull .*offscreen_draws=0', log)) == n
    for required in ('PASS M4h footprint sources=7000',
                     'PASS M4g sparse sources=7000 changed=131 frames=256 pose_bytes_per_frame=10480',
                     'PASS M4g reorder sources=7000 frames=64 geometry_bytes=0 pose_bytes=0 compute_dispatches=0'):
        assert required in log, (path, required)
main_warp = read(validation / 'warp.log')
for required in ('PASS SPSC packets=128', 'PASS M4h memory ', 'PASS M4h constants ',
                 'PASS M4h timeline samples=3 auxiliary_samples=3 conservation=exact categories=4 phases=6 invalid_overflow=rejected'):
    assert required in main_warp, required
for name, named in [('grab', 0), ('named-grab', 1)]:
    log = read(out / f'{name}.log')
    assert 'PASS cases=8 maxChannel=0 differingPixels=0' in log
    assert f'PASS M4h Grab memory allocated=98304 idle_bytes=0 restored=98304 offscreen_copies=0 pixels=identical named={named}' in log
composite = read(out / 'composite.log')
assert 'PASS WARP composite=24' in composite and 'shared-stencil=9' in composite
assert 'PASS M4h shared-depth renders=6 dedicated_vs_shared=identical stale_depth_stencil_control=rejected' in composite
assert 'PASS M4h rounding controls=6' in composite
rounding = [line.split('\t') for line in read(out / 'composite/rounding.tsv').splitlines()[1:]]
assert len(rounding) == 6 and all(int(row[1]) > 100 and 0 < int(row[2]) <= 2 and int(row[4]) <= 1 for row in rounding)

manifest = json.loads(read(out / 'plans/manifest.json'))
assert manifest['launch'] is False and len(manifest['plans']) == 24 and len(manifest['diagnostics']) == 6
assert manifest['measure_order'] == ['A1', 'B1', 'B2', 'A2']
for key, at in [('hl50', 50), ('wf', 258), ('dd', 50), ('sh', 88)]:
    for label in ('plan', 'pixel', 'A1', 'B1', 'B2', 'A2'):
        path = out / f'plans/m4h-{key}-{label}.json'
        plan = json.loads(read(path)); steps = plan['steps']
        assert str(path) in manifest['plans'] and f'seek {at} 8' in steps
        assert 'drfull explicit on' in steps and 'drfull explicit forward' in steps and 'drfull precision batch' in steps
        candidate = label == 'pixel' or label.startswith('B')
        assert ('drfull warm' in steps) == candidate
        assert steps[-2:] == ['drfull cold', 'quit']
        if label == 'pixel':
            assert 'framecmp assertzero d0 d00' in steps and 'drfull assert clean' in steps
        elif label != 'plan':
            begin, end = steps.index('fpsmark start'), steps.index(f'fpsmark end m4h-{key}-{label}')
            assert steps[begin+1:end] == ['wait 12', 'drfull assert clean']
for path in manifest['diagnostics']:
    plan = json.loads(read(Path(path))); steps = plan['steps']
    assert 'drfull explicit on' in steps and 'drfull explicit forward' in steps and 'drfull precision batch' in steps
    if 'skip-explicit' in path:
        assert steps.index('drfull skip explicit') < steps.index('drfull on')
        assert plan['measurement']['pixel_gate'] is False
    elif 'explicit-all' in path:
        assert steps.index('drfull explicit all') < steps.index('drfull on')
    else:
        assert steps.index('drfull precision local') < steps.index('drfull on')

files = ['AutoTest.cs', 'DirectFull.cs', 'DirectFull.Render.cs', 'DirectFull.Diagnostics.cs', 'DirectFull.ExplicitPolicy.cs',
         'DirectFull.Canvas.cs', 'DirectFull.Native.cs', 'DirectSequenceChanges.cs', 'StutterFix.csproj',
         'native/sfnative/drfull.cpp', 'native/sfnative/drfull.h', 'native/sfnative/drfull-memory.h', 'native/sfnative/drfull-timeline.h',
         'tools/direct-renderer/drfull-warp.cpp', 'tools/direct-renderer/drfull-m4d-warp.cpp',
         'tools/direct-renderer/check-m4h.ps1', 'tools/direct-renderer/check-m4h-warp.ps1', 'tools/direct-renderer/check-m4h-artifacts.py',
         'tools/direct-renderer/plan-m4c.py', 'tools/direct-renderer/plan-m4h.py',
         'tools/direct-renderer/m4-check/Stubs.cs', 'tools/direct-renderer/m4-contract/Program.cs',
         'tools/direct-renderer/m4d-policy/Program.cs', 'tools/direct-renderer/m4d-policy/M4dPolicy.csproj',
         'docs/direct-renderer/12-m4h-spec.md', 'docs/direct-renderer/12-m4h-result.md',
         'native/sfnative.dll', 'bin/Release/StutterFix.dll', 'bin/Player/StutterFix.dll', 'bin/PlayerAuto/StutterFix.dll']
hashes = {p: hashlib.sha256((root / p).read_bytes()).hexdigest() for p in files}
(out / 'final-hashes.json').write_text(json.dumps(hashes, indent=2), encoding='utf-8')
result = dict(builds=4, exports=74, batch_exact=56, thin_transforms_exact=1344, grab_exact=16,
              shared_depth_exact=6, rounding_controls=6, plans=24, diagnostic_plans=6,
              rounding_cause_proven='synthetic same-shader intermediate UNORM8 quantization',
              live_dd_cause='UNMEASURED', live_exit_budget='UNMEASURED', live_exit_rounding_cause='UNMEASURED',
              game_run=False, install_write=False, commit=False)
(out / 'm4h-audit.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print('PASS M4h builds=4 exports=74 batch=56 thin=1344 Grab=16 shared-depth=6 rounding-controls=6 plans=24+6')
print('HOLD live DD VRAM attribution / Exit total <0.95 ms / actual Exit rounding attribution require game evidence; no game run')
