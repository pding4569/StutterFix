"""Offline M4e final artifact and plan checks; never launch or install."""
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parents[2]
out = root / 'out/direct-renderer/m4e'


def read(path):
    b = path.read_bytes()
    return b.decode('utf-16') if b.startswith((b'\xff\xfe', b'\xfe\xff')) else b.decode('utf-8-sig')


warp = read(out / 'validation/warp.log')
assert 'PASS SPSC packets=128 alternating-color-pixels=identical errors=0 dropped=0' in warp
assert 'PASS packed-poses dense-count=1 sparse-native-id=5 pixels=identical legacy-commit=preserved' in warp
assert 'PASS stable-frame uploads=0 pixels=identical layout-cache-hit=1' in warp
assert 'PASS preparation completion/failure/budget-yield/cancel/stale-driver/camera-drift' in read(out / 'validation/lifecycle.log')
grab = json.loads(read(out / 'grab/warp-summary.json'))
assert grab['passed'] == 8 and grab['max_channel'] == 0 and grab['differing_pixels'] == 0
manifest = json.loads(read(out / 'plans/manifest.json'))
assert manifest['launch'] is False and len(manifest['plans']) == 24
for map_key, at in [('hl50', 50), ('wf', 258), ('dd', 50), ('sh', 88)]:
    for label in ['plan', 'pixel', 'A1', 'B1', 'B2', 'A2']:
        plan = json.loads(read(out / f'plans/m4e-{map_key}-{label}.json'))
        steps = plan['steps']
        assert f'seek {at} 8' in steps and f'waitsongpos {at} 400' in steps
        assert 'drfull grab native' in steps
        candidate = label == 'pixel' or label.startswith('B')
        assert ('drfull wait' in steps) == candidate
        if candidate:
            assert steps.index('drfull on') < steps.index('drfull wait')
        if label == 'pixel':
            assert steps.index('pause') < steps.index('drfull on') < steps.index('framecmp grab d1')
            assert 'drfull colors' in steps and 'framecmp assertzero d0 d00' in steps
            assert 'framecmp assertlimit d0 d1 0.1 3 png' in steps
        if label not in ('plan', 'pixel'):
            first = steps.index('fpsmark start'); last = steps.index(f'fpsmark end m4e-{map_key}-{label}')
            assert steps[first+1:last] == ['wait 12']
            assert steps.index('pause') < steps.index('resume') < first
            if candidate:
                assert steps.index('pause') < steps.index('drfull on') < steps.index('drfull wait') < steps.index('resume')

paths = ['DirectFull.cs', 'DirectFull.Render.cs', 'DirectFull.Canvas.cs', 'DirectFull.Census.cs',
         'DirectFull.Native.cs', 'DirectSequenceChanges.cs', 'FrameCompare.cs', 'AutoTest.cs',
         'native/sfnative/drfull.cpp', 'native/sfnative/drfull.h', 'native/sfnative.dll',
         'bin/Release/StutterFix.dll', 'bin/Player/StutterFix.dll', 'bin/PlayerAuto/StutterFix.dll']
hashes = {p: hashlib.sha256((root / p).read_bytes()).hexdigest() for p in paths}
(out / 'final-hashes.json').write_text(json.dumps(hashes, indent=2), encoding='utf-8')
(out / 'm4e-audit.json').write_text(json.dumps(dict(
    legacy_fixtures='PASS', original_exports_preserved=74, packed_pose_sparse_id=5,
    spsc_pixel_packets=128, stable_uploads=0, grab=grab, plans=24,
    HELLO_cause='UNPROVEN', HELLO_fixed='UNVERIFIED',
    game_run=False, installation_write=False, live_main_us='UNMEASURED', live_render_us='UNMEASURED'
), indent=2), encoding='utf-8')
print('PASS M4e packed poses / concurrent packets / stable uploads / preparation pump / Grab 8 / plans 24')
print('HOLD HELLO causal proof and live performance require actual source/MPB/order capture; WARP is not Unity parity')
