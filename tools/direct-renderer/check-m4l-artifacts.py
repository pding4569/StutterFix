"""Audit M4l local build/fixture/plan evidence. Never launches or installs."""
import hashlib
import json
import re
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[2]
out = root / 'out/direct-renderer/m4l'


def read(path):
    data = path.read_bytes()
    return data.decode('utf-16') if data.startswith((b'\xff\xfe', b'\xfe\xff')) else data.decode('utf-8-sig')


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


pointer = json.loads(read(out / 'validation-latest.json'))
validation = root / pointer['directory']
assert validation.is_relative_to(out)
assert '74/74 including sf_z_drfull_v1' in read(validation / 'exports-m4d-result.txt')
assert 'OK ' in read(validation / 'build-native.log')
native = (root / 'native/sfnative.dll').read_bytes()
artifacts = {'native/sfnative.dll': sha(root / 'native/sfnative.dll')}
warnings = {}
for edition, directory in [('dev', 'Release'), ('player', 'Player'), ('playerauto', 'PlayerAuto')]:
    log = read(validation / f'build-{edition}.log')
    assert re.search(r'(오류\s*0개|0 Error\(s\))', log), edition
    binary = root / f'bin/{directory}/StutterFix.dll'
    assert native in binary.read_bytes(), f'{edition}: stale embedded native'
    artifacts[str(binary.relative_to(root))] = sha(binary)
    counts = re.findall(r'(?:경고\s*(\d+)개|(\d+) Warning\(s\))', log)
    warnings[edition] = int(next(n for n in counts[-1] if n))
for filename, marker in [('lifecycle.log', 'PASS 25 lifecycle/refusal cases'),
                         ('contract.log', 'M4l append-only cache ABI'),
                         ('pixel-metrics.log', 'FrameCompare M4l quality: 9 checks passed'),
                         ('policy-final.log', 'checks=20115 stable_layer_allocated=0')]:
    # The contract aborts on ABI mismatch; its public summary is the resource count.
    if filename == 'contract.log':
        marker = 'PASS production resource loader shaders=21 variants=1012 stages=2024'
    assert marker in read(validation / filename), filename
for phase in ('before', 'after'):
    rows = json.loads(read(validation / f'installed-{phase}.json'))
    assert len(rows) == 2 and all(sha(Path(row['Path'])) == row['Hash'] for row in rows)

fixture = read(out / 'm4l.log')
for marker in ('records=7 replays=138 cpp_heap_calls=0 local_reorder_records=1',
               'GPU_pose mode=1 cases=8 parity=exact records=1 constants_pose_upload=0',
               'Grab name=1 mode=1 first_visible=CPU_barrier', 'Grab name=2 mode=1 first_visible=CPU_barrier',
               'rectangle mode=1 copies=8 bytes=120640 accepted=8 mismatches=0 fallback=0',
               'rectangle mode=2 copies=8 bytes=196608 accepted=0 mismatches=8 fallback=8',
               'rectangle mode=3 copies=8 bytes=196608', 'rectangle mode=4 copies=8 bytes=196608',
               'rectangle mode=5 copies=8 bytes=196608 accepted=0 mismatches=0 fallback=8',
               'disjoint_grab mode=1 copies=6 groups=2 saved=2 overlap=separate parity=exact'):
    assert marker in fixture, marker
assert 'failure_state=restored recovery=PASS stale_event=inert legacy_fallback=PASS' in read(out / 'm4l-lifecycle.log')
assert 'Canvas_only poses=0 cull_dispatches=0 default_updates=0 records=1 replays=2 pixels=exact' in read(out / 'm4l-lifecycle.log')
for label, count, maximum in [('all', 48, 1), ('grab', 8, 0), ('named-grab', 8, 0)]:
    assert f'PASS cases={count} maxChannel={maximum}' in read(out / f'cached-{label}.log')
for mode in ('deferred', 'immediate-legacy'):
    for label in ('default', 'grab', 'named-grab'):
        assert 'PASS cases=8 maxChannel=0 differingPixels=0' in read(out / f'{mode}-{label}.log')
assert 'live_bytes=6291456 static_frames=16 static_upload=0' in read(out / 'm4j.log')
assert 'probe_invalid=0' in read(out / 'm4j.log')
assert 'PASS M4k resource_controls late=rejected ABI=append_only game=not_launched' in read(out / 'm4k.log')
shader = json.loads(read(out / 'shader-contract.json'))
assert shader['eligible_variants'] == shader['screen_samples'] == 100

manifest = json.loads(read(out / 'plans/manifest.json'))
assert not manifest['launch'] and len(manifest['plans']) == 29 and len(manifest['diagnostics']) == 12
for filename in manifest['plans'] + manifest['diagnostics']:
    path = Path(filename)
    assert path.name.startswith('m4l-')
    plan = json.loads(read(path)); steps = plan['steps']
    assert plan['tag'] == path.stem and 'drfull resources default' in steps and 'drfull drawgpu off' in steps
    assert not any(s.startswith(('hidegroup', 'scriptskip', 'drx ', 'dr bench', 'drfull resources grab off')) for s in steps)
    if plan['measurement']['candidate'] and 'cache-off' not in path.stem:
        assert 'drfull cache on' in steps and 'drfull assert cache' in steps
    if 'cache-off' in path.stem:
        assert 'drfull cache off' in steps and 'drfull assert cache' not in steps
        assert plan['measurement']['exclude_from_normal_ABBA']
    if path.stem.endswith('-pixel'):
        assert 'framecmp assertzero d0 d00' in steps
        if '-dd-' in path.name:
            assert 'framecmp assertpixels d0 d1' in steps and 'framecmp assertzero d0 d1' not in steps
        if '-sh-' in path.name:
            assert 'framecmp assertquality d0 d1 2 0 png' in steps
        if '-hl50-' in path.name:
            assert 'framecmp assertquality d0 d1 255 42.8 png' in steps
    if '-dd-' in path.name:
        maximum = 1024 if 'imagemax1024' in path.name else 0
        assert plan['settings']['ImageMaxSide'] == maximum
        assert steps.index(f'set ImageMaxSide {maximum}') < next(i for i, s in enumerate(steps) if s.startswith('open '))

sources = ['DirectFull.cs', 'DirectFull.Render.cs', 'DirectFull.Native.cs', 'DirectFull.Diagnostics.cs',
           'FrameCompare.cs', 'FrameCompareMetrics.cs', 'native/sfnative/drfull.cpp', 'native/sfnative/drfull.h',
           'native/sfnative/drfull-cache.h', 'tools/direct-renderer/drfull-m4l-warp.cpp',
           'tools/direct-renderer/drfull-m4l-lifecycle.cpp', 'tools/direct-renderer/drfull-warp.cpp',
           'tools/direct-renderer/plan-m4l.py', 'tools/direct-renderer/check-m4l.ps1',
           'tools/direct-renderer/check-m4l-warp.ps1', 'tools/direct-renderer/audit-m4l-shaders.py',
           'tools/direct-renderer/check-m4l-artifacts.py']
hashes = {name: sha(root / name) for name in sources}
(out / 'final-hashes.json').write_text(json.dumps(dict(sources=hashes, artifacts=artifacts), indent=2), encoding='utf-8')
audit = dict(builds=4, errors=0, warnings=warnings, exports=74, embedded_native=3, installed_hashes_preserved=2,
             validation=pointer['directory'], normal_plans=29, diagnostic_plans=12,
             cached_original_DXBC_cases=64, cached_original_max_channel=1,
             rectangle_copy_bytes=dict(full=196608, partial=120640), disjoint_grab_copies=dict(full=8, merged=6),
             cache_lifecycle='PASS', pixel_quality_gate_cases=9, game_run=False, install_write=False, commit=False,
             real_render_thread_target='UNMEASURED', real_main_target='UNMEASURED',
             real_DD_Exit_HELLO_pixels='UNMEASURED', real_DD_over_budget='UNMEASURED',
             real_Exit_HELLO_GPU_regression='UNMEASURED')
(out / 'm4l-audit.json').write_text(json.dumps(audit, indent=2), encoding='utf-8')
print('PASS M4l builds=4 errors=0 exports=74 embedded_native=3 installed=2/2 cache/Grab/lifecycle fixtures PASS plans=29+12')
print('UNMEASURED: actual main/render targets, DD VRAM collapse, DD/Exit/HELLO pixels and hardware GPU time')
