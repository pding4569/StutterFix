"""Prepare census and frozen-pixel plans only; never launch a game or runner."""
import importlib.util
import json
from pathlib import Path

WT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('engine_hot_bench', WT.parent / 'bench_v14.py')
b = importlib.util.module_from_spec(spec)
spec.loader.exec_module(b)
out = WT / 'out/engine-hot/plans'
out.mkdir(parents=True, exist_ok=True)
count = 0
for key in ('hl', 'sh', 'ar'):
    path, at = b.MAPS[key]
    cfg, commands = b.CFG2['rlNR']
    start = ['timeout 900', 'wait 2', 'auto on', 'fpscap 0', 'rtsize 3440 1440', 'open ' + b.M + path]
    start += list(commands) + ['phasewindow on', f'seek {at} 8', f'waitsongpos {at} 400']
    variants = {'census': ['engknob census', 'engknob observe on', 'wait 12', 'engknob assert observe', 'engknob off']}
    for knob in ('occ', 'jobs1', 'jobs2', 'jobs4', 'jobs8'):
        variants['pixels-' + knob] = [
            'pause', 'wait 2', 'framecmp filters on', 'framecmp mask none',
            'framecmp grab a', 'framecmp grab a2', 'framecmp assertzero a a2',
            f'engknob {knob} on', 'wait 1', f'engknob assert {knob}',
            'framecmp grab b', 'framecmp assertzero a b png', 'engknob off',
            'wait 1', 'framecmp grab restored', 'framecmp assertzero a restored png',
            'framecmp result', 'framecmp clear', 'resume']
    for name, commands in variants.items():
        tag = f'engine-hot-{key}-{name}'
        plan = dict(steps=start + commands + ['phasewindow off', 'quit'], settings=dict(b.CLEAN, **cfg), timeout_min=15, tag=tag)
        (out / (tag + '.json')).write_text(json.dumps(plan, ensure_ascii=False, indent=2), encoding='utf-8')
        count += 1
print(f'Prepared {count} plans; no game/runner/install invoked: {out}')
