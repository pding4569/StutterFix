"""M4d offline artifact/plan audit. No game, install, or shared lock writes."""
import hashlib
import json
from pathlib import Path
import runpy
import sys

root = Path(__file__).resolve().parents[2]
output = root / (sys.argv[1] if len(sys.argv) > 1 else 'out/direct-renderer/m4d')
runpy.run_path(str(root / 'tools/direct-renderer/check-m4-artifacts.py'), run_name='__main__')

def read(path):
    b = path.read_bytes()
    return b.decode('utf-16') if b.startswith((b'\xff\xfe', b'\xfe\xff')) else b.decode('utf-8-sig')

assert '74/74 including sf_z_drfull_v1' in read(output / 'exports-m4d-result.txt')
warp = json.loads(read(output / 'warp-m4d/summary.json'))
assert warp['composition_cases'] == 24 and warp['float_max_error'] < 2e-6
assert warp['rgba8_max_channel'] <= 2 and warp['naive_alpha_rejections'] == 3
assert warp['shared_stencil_cases'] == 9 and warp['Unity_internal_state'] == 'UNMEASURED'
assert 'checks=20115 stable_layer_allocated=0' in read(output / 'policy-final.log')
assert '19 checks passed' in read(output / 'pixel-metrics.log')
manifest = json.loads(read(output / 'plans/manifest.json'))
assert manifest['launch'] is False and len(manifest['plans']) == 24
assert manifest['workspace'] == str(root) and manifest['measure_order'] == ['A1', 'B1', 'B2', 'A2']
counts = dict(plan=0, pixel=0, ABBA=0)
for map_key, at in [('hl50', 50), ('wf', 258), ('dd', 50), ('sh', 88)]:
    for label in ['plan', 'pixel', 'A1', 'B1', 'B2', 'A2']:
        p = output / f'plans/m4d-{map_key}-{label}.json'
        assert str(p) in manifest['plans']
        plan = json.loads(read(p)); steps = plan['steps']
        assert f'seek {at} 8' in steps and f'waitsongpos {at} 400' in steps
        assert plan['settings']['GpuDeco'] == 0 and plan['settings']['RunLod'] is False
        assert 'rtsize 3440 1440' in steps
        assert not any(s.startswith(('hidegroup', 'scriptskip', 'drx ', 'dr bench')) for s in steps)
        if label == 'plan':
            assert 'drfull plan' in steps and 'drfull census' in steps and 'drfull on' not in steps
            assert 'fpsmark start' not in steps and steps[-2:] == ['drfull assert off', 'quit']
            counts['plan'] += 1
        elif label == 'pixel':
            assert steps.index('pause') < steps.index('drfull on') < steps.index('framecmp grab d1')
            assert steps.index('framecmp grab d1') < steps.index('framecmp grab d0') < steps.index('framecmp grab d00')
            assert 'framecmp assertzero d0 d00' in steps and 'framecmp assertlimit d0 d1 0.1 3 png' in steps
            assert plan['measurement']['candidate'] is True and 'fpsmark start' not in steps
            counts['pixel'] += 1
        else:
            candidate = label.startswith('B')
            assertion = 'drfull assert on' if candidate else 'drfull assert off'
            begin = steps.index('fpsmark start'); end = steps.index(f'fpsmark end m4d-{map_key}-{label}')
            assert assertion in steps[:begin] and assertion in steps[end:]
            assert steps[begin+1:end] == ['wait 12']
            assert 'cpuwindow begin' in steps[:begin] and 'cpuwindow end' in steps[end:]
            assert ('drfull on' in steps) == candidate
            counts['ABBA'] += 1
assert counts == dict(plan=4, pixel=4, ABBA=16)
sources = ['DirectFull.cs', 'DirectFull.Render.cs', 'DirectFull.Canvas.cs', 'DirectFull.ObjectLayers.cs',
           'DirectFull.Census.cs', 'DirectFull.Native.cs', 'native/sfnative/drfull.cpp', 'native/sfnative/drfull.h',
           'FrameCompare.cs', 'FrameCompareMetrics.cs', 'tools/direct-renderer/plan-m4c.py']
hashes = {p: hashlib.sha256((root / p).read_bytes()).hexdigest() for p in sources}
(output / 'source-hashes.json').write_text(json.dumps(hashes, indent=2), encoding='utf-8')
(output / 'm4d-audit.json').write_text(json.dumps(dict(plans=counts, warp=warp, game_run=False,
    install_write=False, real_game_pixels='UNMEASURED', real_game_FPS='UNMEASURED'), indent=2), encoding='utf-8')
print('PASS M4d composition/stencil + production layer/order policy; plans=4+4+16; game/installation untouched by this checker')
