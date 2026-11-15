"""M4l plans only: four maps pixel + ABBA, DD1024 pixel + ABBA. Never run."""
import copy
import json
import subprocess
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[2]
out = root / 'out/direct-renderer/m4l/plans'
subprocess.run([sys.executable, str(Path(__file__).with_name('plan-m4c.py')),
                '--milestone', 'm4l', '--output', str(out)], check=True)
manifest = json.loads((out / 'manifest.json').read_text(encoding='utf-8'))
for filename in manifest['plans']:
    path = Path(filename)
    plan = json.loads(path.read_text(encoding='utf-8'))
    steps = plan['steps']
    at = steps.index('drfull off') + 1
    steps[at:at] = ['drfull resources default', 'drfull cache on', 'drfull drawgpu off']
    if '-dd-' in path.name:
        load = next(i for i, s in enumerate(steps) if s.startswith('open '))
        steps.insert(load, 'set ImageMaxSide 0')
        plan['settings']['ImageMaxSide'] = 0
    if path.stem.endswith('-pixel'):
        if '-dd-' in path.name:
            steps[steps.index('framecmp assertlimit d0 d1 0.1 3 png')] = 'framecmp assertpixels d0 d1'
            plan['measurement'].update(pixel_changed_percent_lt=0, pixel_max_channel_le=0)
        elif '-sh-' in path.name:
            steps[steps.index('framecmp assertlimit d0 d1 0.1 3 png')] = 'framecmp assertquality d0 d1 2 0 png'
            plan['measurement'].update(pixel_changed_percent_lt=None, pixel_max_channel_le=2)
    if '-hl50-' in path.name and path.stem.endswith('-pixel'):
        steps[steps.index('framecmp assertlimit d0 d1 0.1 3 png')] = 'framecmp assertquality d0 d1 255 42.8 png'
        plan['measurement'].update(pixel_changed_percent_lt=None, pixel_max_channel_le=255, pixel_psnr_min=42.8)
    steps[:] = [item for step in steps for item in ([step, 'drfull assert cache'] if step == 'drfull assert on' else [step])]
    plan['measurement'].update(cache_required=bool(plan['measurement']['candidate']),
        render_thread_target_us=100, main_target_us=150,
        scope='M4l original DXBC GPU pose cached command lists' if plan['measurement']['candidate'] else plan['measurement']['scope'])
    path.write_text(json.dumps(plan, ensure_ascii=False, indent=2), encoding='utf-8')

for arm in ('pixel', 'A1', 'B1', 'B2', 'A2'):
    source = out / f'm4l-dd-{arm}.json'
    plan = copy.deepcopy(json.loads(source.read_text(encoding='utf-8')))
    name = f'm4l-dd-imagemax1024-{arm}'
    plan['steps'] = [s.replace(plan['tag'], name).replace('set ImageMaxSide 0', 'set ImageMaxSide 1024') for s in plan['steps']]
    plan['tag'] = name
    plan['settings']['ImageMaxSide'] = 1024
    plan['measurement'].update(image_max_side=1024, require_usage_below_budget=True,
        require_observed_texture_dimensions=True, diagnostic=False)
    path = out / f'{name}.json'
    path.write_text(json.dumps(plan, ensure_ascii=False, indent=2), encoding='utf-8')
    manifest['plans'].append(str(path))
manifest.update(milestone='m4l', launch=False,
    targets={'render_thread_us': 100, 'main_us': 150, 'DD_max_channel': 0, 'Exit_max_channel': 2, 'HELLO_psnr_min': 42.8},
    prerequisites=['JALib off; safe=0; same resolution and quality',
        'No game launches, installs, commits or runs were performed by this generator',
        'Pixel/noise gate before ABBA; DD1024 is an independent quality condition',
        'Reject native GPU regression against M4k+correct Grab under the same paused scene',
        'Nonzero visible Grab draws require nonzero grab_copies unless grab_merged proves sharing',
        'State must show cache_segments>0, cache_replays>cache_records, cache_fallback_segments=0',
        'Compare cache_records deltas with actual order/state changes; do not count pose-only motion as re-recording',
        'Fresh load after ImageMaxSide; verify dimensions and DXGI headroom; settings restored by runner finally',
        'Reject concurrent user play, replacement DLLs or competing benchmarks'])
manifest['diagnostics'] = []
for key in ('hl50', 'sh', 'dd', 'wf'):
    for arm in ('pixel', 'B1', 'B2'):
        source = out / f'm4l-{key}-{arm}.json'
        plan = copy.deepcopy(json.loads(source.read_text(encoding='utf-8')))
        name = f'm4l-{key}-cache-off-{arm}'
        plan['steps'] = [step.replace(plan['tag'], name).replace('drfull cache on', 'drfull cache off')
                         for step in plan['steps'] if step != 'drfull assert cache']
        plan['tag'] = name
        plan['measurement'].update(cache_required=False, diagnostic=True, exclude_from_normal_ABBA=True,
                                   scope='M4l uncached native with the same corrected Grab; isolate cache GPU cost')
        path = out / f'{name}.json'
        path.write_text(json.dumps(plan, ensure_ascii=False, indent=2), encoding='utf-8')
        manifest['diagnostics'].append(str(path))
(out / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
assert len(manifest['plans']) == 29
assert len(manifest['diagnostics']) == 12
print('PLAN ONLY M4l: 24 four-map + 5 DD1024 + 12 cache-off diagnostic plans; no launch/install')
