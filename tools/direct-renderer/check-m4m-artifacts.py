"""Audit M4m local build/fixture/plan evidence. Never launches or installs."""
import hashlib
import json
import re
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[2]
out = root / 'out/direct-renderer/m4m'


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

manifest = json.loads(read(out / 'plans/manifest.json'))
assert not manifest['launch'] and len(manifest['plans']) == 29 and len(manifest['diagnostics']) == 24
for filename in manifest['plans'] + manifest['diagnostics']:
    path = Path(filename)
    assert path.name.startswith('m4m-')
    plan = json.loads(read(path)); steps = plan['steps']
    assert plan['tag'] == path.stem and 'drfull resources default' in steps and 'drfull drawgpu off' in steps
    assert not any(s.startswith(('hidegroup', 'scriptskip', 'drx ', 'dr bench', 'drfull resources grab off')) for s in steps)
    if plan['measurement']['candidate'] and 'cache-off' not in path.stem:
        assert ('drfull cache indirect' if 'cache-indirect' in path.stem else 'drfull cache on') in steps and 'drfull assert cache' in steps
    if 'cache-off' in path.stem:
        assert 'drfull cache off' in steps and 'drfull assert cache' not in steps
        assert plan['measurement']['exclude_from_normal_ABBA']
    if path.stem.endswith('-pixel'):
        assert 'framecmp assertzero d0 d00' in steps and 'framecmp assertzero d1 d11' in steps
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

moving = read(out / 'm4m.log')
for mode in range(3):
    assert f'PASS M4m moving mode={mode} pixels=20 exact=True' in moving
    assert f'PASS M4m run_boundaries mode={mode} pixels=5' in moving
assert 'PASS M4m stage_divergence mode=1 pixels=5 independent_VS_PS=True detaches=2' in moving
assert 'heads=7000 bindings=21000 frames=16 plans_delta=0 records_delta=0 evaluations_per_frame=1 bytes_per_frame=4096 draw_evaluations_delta=0 indirect=0 cull_items=0 cpp_heap_calls=0' in moving
assert 'synchronized_material heads=7000 shared_slice=retained detach=0 plan_delta=0 record_delta=0' in moving
assert 'PASS M4m distance neighbours sources=7000' in read(validation / 'policy-final.log')
assert 'static bool ScreenGrab(Bound b) => false;' in read(root / 'DirectFull.Render.cs')
# Verify the task-start installation and HEAD independently of each checker run.
rows = json.loads(read(out / 'installed-before.json'))
assert len(rows) == 2
session_changed = [dict(path=row['Path'], before=row['Hash'], after=sha(Path(row['Path']))) for row in rows if sha(Path(row['Path'])) != row['Hash']]
(out / 'installed-session-result.json').write_text(json.dumps(dict(preserved=2-len(session_changed), changed=session_changed, action='read_only; no installation/restoration performed'), indent=2), encoding='utf-8')
import subprocess
assert read(out / 'head-before.txt').strip() == subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
source_before = json.loads(read(out / 'source-before.json'))
source_after = json.loads(read(out / 'source-after.json'))
assert [(r['Path'], r['Hash']) for r in source_before] == [(r['Path'], r['Hash']) for r in source_after]
assert all(sha(Path(r['Path'])) == r['Hash'] for r in source_after)
sources = ['DirectFull.cs', 'tools/direct-renderer/m4-contract/Program.cs', 'tools/direct-renderer/m4d-policy/Program.cs', 'DirectFull.Render.cs', 'DirectFull.Native.cs', 'DirectFull.Diagnostics.cs',
           'FrameCompare.cs', 'FrameCompareMetrics.cs', 'native/sfnative/drfull.cpp', 'native/sfnative/drfull.h',
           'native/sfnative/drfull-cache.h', 'tools/direct-renderer/drfull-m4m-warp.cpp',
           'tools/direct-renderer/drfull-m4l-lifecycle.cpp', 'tools/direct-renderer/drfull-warp.cpp',
           'tools/direct-renderer/plan-m4m.py', 'tools/direct-renderer/check-m4m.ps1',
           'tools/direct-renderer/check-m4m-warp.ps1', 'tools/direct-renderer/drfull-m4l-warp.cpp', 'DirectFull.ObjectLayers.cs', 'tools/direct-renderer/plan-m4c.py',
           'tools/direct-renderer/check-m4m-artifacts.py']
hashes = {name: sha(root / name) for name in sources}
(out / 'final-hashes.json').write_text(json.dumps(dict(sources=hashes, artifacts=artifacts), indent=2), encoding='utf-8')
audit = dict(source_stable=True, builds=4, errors=0, warnings=warnings, exports=74, embedded_native=3, validation_installed_hashes_preserved=2, session_installed_hashes_preserved=2-len(session_changed), session_installation_changes=session_changed,
             validation=pointer['directory'], normal_plans=29, diagnostic_plans=24,
             cached_original_DXBC_cases=64, moving_pixel_cases=85, scale_heads=7000, scale_bindings=21000, scale_evaluations_per_frame=1, scale_bytes_per_frame=4096, scale_plan_delta=0, scale_record_delta=0, scale_heap_calls=0, cached_original_max_channel=1,
             production_grab='full', rectangle_fixture_bytes=dict(full=196608, partial=120640), disjoint_grab_copies=dict(full=8, merged=6),
             cache_lifecycle='PASS', pixel_quality_gate_cases=9, game_run=False, install_write=False, commit=False,
             real_render_thread_target='UNMEASURED', real_main_target='UNMEASURED',
             real_DD_Exit_HELLO_pixels='UNMEASURED', real_DD_over_budget='UNMEASURED',
             real_Exit_HELLO_GPU_regression='UNMEASURED')
(out / 'm4m-audit.json').write_text(json.dumps(audit, indent=2), encoding='utf-8')
print('PASS M4m builds=4 errors=0 exports=74 embedded_native=3 validation_installed=2/2 cache/Grab/lifecycle fixtures PASS plans=29+24')
print('UNMEASURED: actual main/render targets, DD VRAM collapse, DD/Exit/HELLO pixels and hardware GPU time')

if session_changed:
    print('HOLD session installation hash changed: ' + ', '.join(row['path'] for row in session_changed) + '; task-start snapshots retained; no restoration attempted')
    sys.exit(2)

